using System.Diagnostics;
using System.Text.Json;
using SLANGCompiler.SlfsPack;
using Xunit;
using SkippableFactAttribute = Xunit.SkippableFactAttribute;

namespace SLANGCompiler.Tests;

/// <summary>
/// ブラウザから呼ぶ、Hu-BASIC と同じ形のディスクを作る口 (HuDiskWebApi)。
/// slangbuild の --emit disk (disk.tool: hufs-pack) と同じ HuPack.ParseAddSpec・HuPack.Build を通り、
/// load を省けば $1000、exec を省けば load の値 (slangbuild と同じ決まり)
/// </summary>
public class HuDiskWebApiTests : IDisposable
{
    private readonly string _dir;
    private static readonly byte[] Main = Enumerable.Range(0, 300).Select(i => (byte)i).ToArray();

    public HuDiskWebApiTests()
    {
        _dir = Path.Combine(Path.GetTempPath(), "slang_hudisk_" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(_dir);
    }

    public void Dispose()
    {
        try { Directory.Delete(_dir, true); } catch { }
    }

    private string File_(string name, byte[] data)
    {
        var path = Path.Combine(_dir, name);
        File.WriteAllBytes(path, data);
        return path;
    }

    private static JsonElement Parse(string json) => JsonDocument.Parse(json).RootElement.Clone();

    private static byte[] D88(JsonElement r)
    {
        Assert.True(r.GetProperty("ok").GetBoolean(), r.GetRawText());
        return Convert.FromBase64String(r.GetProperty("d88").GetString()!);
    }

    [Fact]
    public void BuildHuDisk_AddKinds_MatchHuPack()
    {
        var text = File_("greet.txt", "HELLO\nWORLD\r\n"u8.ToArray());
        var bin = File_("data.bin", new byte[] { 1, 2, 3, 4 });
        var raw = File_("RAW.DAT", new byte[] { 9, 8, 7 });
        var add = new[] { $"GREET.TXT={text},text", $"DATA.BIN={bin},bin", raw };
        var r = Parse(HuDiskWebApi.BuildHuDisk(Main, JsonSerializer.Serialize(new { mainName = "PROG", add })));
        var expected = HuPack.Build(Main, "PROG", 0x1000, 0x1000, add.Select(HuPack.ParseAddSpec));
        Assert.Equal(expected, D88(r));
    }

    [Theory]
    [InlineData("{\"mainName\":\"PROG\"}", 0x1000, 0x1000)]
    [InlineData("{\"mainName\":\"PROG\",\"load\":8192}", 0x2000, 0x2000)]
    [InlineData("{\"mainName\":\"PROG\",\"load\":8192,\"exec\":8448}", 0x2000, 0x2100)]
    [InlineData("{\"mainName\":\"PROG\",\"exec\":8448}", 0x1000, 0x2100)]
    public void BuildHuDisk_LoadExecDefaults_FollowSlangbuild(string options, int load, int exec)
    {
        var expected = HuPack.Build(Main, "PROG", (ushort)load, (ushort)exec, Array.Empty<HuPack.AddSpec>());
        Assert.Equal(expected, D88(Parse(HuDiskWebApi.BuildHuDisk(Main, options))));
    }

    [Theory]
    [InlineData("")]
    [InlineData("[]")]
    [InlineData("{\"load\":4096}")]
    [InlineData("{\"mainName\":\"\"}")]
    [InlineData("{\"mainName\":\"PROG\",\"load\":65536}")]
    [InlineData("{\"mainName\":\"PROG\",\"exec\":-1}")]
    [InlineData("{\"mainName\":\"PROG\",\"add\":\"X\"}")]
    [InlineData("{\"mainName\":\"PROG\",\"add\":[1]}")]
    [InlineData("{\"mainName\":\"PROG\",\"name\":\"X\"}")]
    [InlineData("{\"mainName\":\"\\ud800\"}")]                  // 対になっていないサロゲート (値)
    [InlineData("{\"\\ud800\":\"PROG\"}")]                       // 対になっていないサロゲート (項目の名前)
    [InlineData("{\"mainName\":\"PROG\",\"add\":[\"\\udc00\"]}")]
    public void BuildHuDisk_BadOptions_ReturnsError(string options)
    {
        var r = Parse(HuDiskWebApi.BuildHuDisk(Main, options));
        Assert.False(r.GetProperty("ok").GetBoolean());
        Assert.StartsWith("hufs: ", r.GetProperty("error").GetString());
    }

    [Fact]
    public void BuildHuDisk_LiteralLoneSurrogateInOptions_ReturnsError()
    {
        foreach (var options in new[] { "{\"mainName\":\"\ud800\"}", "{\"\udc00\":\"PROG\"}" })
        {
            var r = Parse(HuDiskWebApi.BuildHuDisk(Main, options));
            Assert.False(r.GetProperty("ok").GetBoolean());
            Assert.StartsWith("hufs: ", r.GetProperty("error").GetString());
        }
    }

    [Fact]
    public void BuildHuDisk_DrivePrefixedBootName_UsesSameNameInDirectoryAndIpl()
    {
        // "0:GAME" のドライブの "0:" は、ディレクトリの項目にも IPL の見出しにも書かない
        var r = Parse(HuDiskWebApi.BuildHuDisk(Main, "{\"mainName\":\"0:GAME\"}"));
        var disk = HuDiskImage.FromD88(D88(r));
        Assert.Contains("GAME.Sys", disk.Entries().Select(e => e.DisplayName));
        Assert.Equal("GAME         ", System.Text.Encoding.ASCII.GetString(disk.ReadRecord(0), 1, 13));
    }

    [Theory]
    [InlineData("ｱ")]                    // 表示できる ASCII でない (IPL の見出しに書けない)
    [InlineData("ABCDEFGHIJKLMN")]       // 14 文字
    [InlineData("TAB\tNAME")]
    public void BuildHuDisk_InvalidBootName_ReturnsSlangbuildError(string name)
    {
        var r = Parse(HuDiskWebApi.BuildHuDisk(Main, JsonSerializer.Serialize(new { mainName = name })));
        Assert.False(r.GetProperty("ok").GetBoolean());
        Assert.Equal($"hufs: disk main name '{name}' invalid ({BootName.Rule})", r.GetProperty("error").GetString());
    }

    [Fact]
    public void BuildHuDisk_DiskErrors_ReturnError()
    {
        // 足すファイルが無い・名前が決まりに合わない・本体の名前が長すぎる・本体が空
        foreach (var options in new[]
        {
            JsonSerializer.Serialize(new { mainName = "PROG", add = new[] { Path.Combine(_dir, "NONE.TXT") } }),
            JsonSerializer.Serialize(new { mainName = "PROG", add = new[] { $"A.LONG={File_("x.txt", new byte[] { 1 })}" } }),
            JsonSerializer.Serialize(new { mainName = "ABCDEFGHIJKLMNOP" }),
        })
        {
            var r = Parse(HuDiskWebApi.BuildHuDisk(Main, options));
            Assert.False(r.GetProperty("ok").GetBoolean(), options);
            Assert.StartsWith("hufs: ", r.GetProperty("error").GetString());
        }
        var empty = Parse(HuDiskWebApi.BuildHuDisk(Array.Empty<byte>(), "{\"mainName\":\"PROG\"}"));
        Assert.False(empty.GetProperty("ok").GetBoolean());
    }

    [SkippableFact]
    public void BuildHuDisk_SameAsSlangbuild_WithLoadOnlyEnv()
    {
        // main_load だけを書いた env ($2000、main_exec なし) で slangbuild --emit disk したディスクと、
        // その bin を load だけ渡して作ったディスクが同じ (exec は load と同じになる)
        var repo = TestTools.RepoRoot();
        var asm = Path.Combine(repo, "tools", OperatingSystem.IsWindows() ? "AILZ80ASM.exe" : "AILZ80ASM");
        Skip.IfNot(File.Exists(asm), "AILZ80ASM がない");

        var lib = Path.Combine(_dir, "lib");
        Directory.CreateDirectory(Path.Combine(lib, "env"));
        var envText = File.ReadAllText(Path.Combine(repo, "runtime", "env", "x1native_hufs.env")).Replace("\r\n", "\n")
            .Replace("default_org:\t\"$1000\"", "default_org:\t\"$2000\"")
            .Replace("  main_load: \"$1000\"\n", "  main_load: \"$2000\"\n")
            .Replace("  main_exec: \"$1000\"\n", "");
        Assert.Contains("main_load: \"$2000\"", envText);
        Assert.DoesNotContain("main_exec", envText);
        File.WriteAllText(Path.Combine(lib, "env", "hufs2000.env"), envText);
        var greet = File_("greet.txt", "HI\n"u8.ToArray());
        var sl = Path.Combine(_dir, "PROG.SL");
        File.WriteAllText(sl, "VAR J;\nMAIN()\nBEGIN\n  J = 1;\nEND;\n");
        var output = Path.Combine(_dir, "PROG");

        // この試験と同じ組み立ての slangbuild (試験の出力にある slangbuild.dll) を動かす
        var slangbuild = typeof(SLANGCompiler.Build.Driver).Assembly.Location;
        var psi = new ProcessStartInfo("dotnet")
        {
            WorkingDirectory = repo,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
        };
        foreach (var a in new[] { slangbuild,
                                  "-E", "hufs2000", "-L", lib, sl, "-o", output, "--emit", "disk",
                                  "--hufs-add", $"GREET.TXT={greet},text", "--disk-main-name", "PROG",
                                  "--asm", asm, "--slangc", TestTools.SlangcPath })
            psi.ArgumentList.Add(a);
        using var proc = Process.Start(psi)!;
        var so = proc.StandardOutput.ReadToEndAsync();
        var se = proc.StandardError.ReadToEndAsync();
        Assert.True(proc.WaitForExit(180_000), "slangbuild timed out");
        Assert.True(proc.ExitCode == 0, so.Result + se.Result);

        var bin = File.ReadAllBytes(output + ".bin");
        var r = Parse(HuDiskWebApi.BuildHuDisk(bin, JsonSerializer.Serialize(new
        {
            mainName = "PROG",
            load = 0x2000,
            add = new[] { $"GREET.TXT={greet},text" },
        })));
        Assert.Equal(File.ReadAllBytes(output + ".d88"), D88(r));
    }
}
