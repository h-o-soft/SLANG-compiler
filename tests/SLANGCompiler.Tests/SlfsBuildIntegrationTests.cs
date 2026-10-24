using Xunit;
using SLANGCompiler.SlfsPack;
using System.Diagnostics;
using System.Text;

namespace SLANGCompiler.Tests;

public class SlfsBuildIntegrationTests
{
    private static string RepoRoot()
    {
        // clean checkout 安全 marker = runtime/env/x1native.env (= tracked)。
        // 旧実装の src/src.sln は untracked のため clean checkout で壊れる。
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir != null && !File.Exists(Path.Combine(dir.FullName, "runtime", "env", "x1native.env")))
            dir = dir.Parent;
        Assert.NotNull(dir);
        return dir!.FullName;
    }

    private static (int Code, string Stdout, string Stderr) RunSlangbuild(string args)
        => RunSlangbuild(args.Split(' ', StringSplitOptions.RemoveEmptyEntries));

    /// <summary>引数を 1 つずつ渡す (= 空の引数・空白入りの引数も渡せる)</summary>
    private static (int Code, string Stdout, string Stderr) RunSlangbuild(params string[] args)
    {
        var repo = RepoRoot();
        var psi = new ProcessStartInfo("dotnet")
        {
            WorkingDirectory = repo,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
        };
        psi.Environment["MSBUILDDISABLENODEREUSE"] = "1";
        psi.ArgumentList.Add("run");
        psi.ArgumentList.Add("--project");
        psi.ArgumentList.Add(Path.Combine(repo, "src", "SLANGCompiler.Build"));
        psi.ArgumentList.Add("--no-build");
        psi.ArgumentList.Add("--");
        foreach (var a in args)
            psi.ArgumentList.Add(a);

        using var proc = Process.Start(psi)!;

        // async read で stdout/stderr を吸い続けつつ timeout 監視 (= subprocess hang
        // で ReadToEnd 永久待ちを回避、 timeout 時は kill して fail)
        var stdoutTask = proc.StandardOutput.ReadToEndAsync();
        var stderrTask = proc.StandardError.ReadToEndAsync();

        if (!proc.WaitForExit(120000))
        {
            try { proc.Kill(entireProcessTree: true); } catch { }
            Assert.Fail("slangbuild timed out after 120s");
        }
        return (proc.ExitCode, stdoutTask.GetAwaiter().GetResult(), stderrTask.GetAwaiter().GetResult());
    }

    [Fact]
    public void SlfsDemo_BuildsViaSlangbuild_AndProducesValidD88()
    {
        var repo = RepoRoot();
        var sample = Path.Combine(repo, "examples", "X1NATIVE_SLFS", "SLFSDEMO.SL");
        var assetsDir = Path.Combine(repo, "examples", "X1NATIVE_SLFS", "assets");
        var include = Path.Combine(repo, "include");
        var output = Path.Combine(Path.GetTempPath(), $"SLFSDEMO_test_{Guid.NewGuid():N}");
        var d88 = output + ".d88";

        try
        {
            var (rc, stdout, stderr) = RunSlangbuild(
                $"-E x1native_slfs -I {include} {sample} -o {output} --emit disk --slfs-add {assetsDir}");
            Assert.True(rc == 0, $"slangbuild failed exit={rc}\nstdout:\n{stdout}\nstderr:\n{stderr}");
            Assert.True(File.Exists(d88), "SLFSDEMO.d88 not generated");

            var r = D88Reader.FromFile(d88);

            // sector 0 = boot header
            var boot = r.ReadSector(0, 0, 1);
            Assert.Equal(0x01, boot[0]);
            Assert.Equal("SLFSMAIN", Encoding.ASCII.GetString(boot, 1, 8));

            // sector 1 = superblock
            var sb = r.ReadSector(0, 0, 2);
            Assert.Equal("SLFS", Encoding.ASCII.GetString(sb, 0, 4));
            Assert.Equal(2, sb[0x05]);   // sides
            Assert.Equal(40, sb[0x06]);  // tracks
            Assert.Equal(16, sb[0x07]);  // sec/track
            Assert.Equal(2, sb[0x0A]);   // DirEntryCount = 2 (GREETING + NUMBERS)
            Assert.Equal(0, sb[0x0B]);

            // sector 2 = directory、 sorted: GREETING.TXT → "GREETING.TX" / NUMBERS.BIN
            var dir = r.ReadSector(0, 0, 3);
            Assert.Equal("GREETING.TX", Encoding.ASCII.GetString(dir, 0, 11));
            Assert.Equal(17, dir[0x0E]); Assert.Equal(0, dir[0x0F]);  // byte_size = 17
            Assert.Equal("NUMBERS.BIN", Encoding.ASCII.GetString(dir, 16, 11));
            Assert.Equal(0, dir[0x1E]); Assert.Equal(1, dir[0x1F]);   // byte_size = 256
        }
        finally
        {
            foreach (var ext in new[] { ".d88", ".bin", ".sym", ".ASM", ".LST", ".inc" })
                if (File.Exists(output + ext)) File.Delete(output + ext);
        }
    }

    /// <summary>SL を書いて slangbuild を通す。 (exit code, stderr, d88 の中身 or null)</summary>
    private static (int Code, string Stderr, byte[]? D88) BuildSl(string sl, params string[] args)
    {
        var output = Path.Combine(Path.GetTempPath(), $"SLFSNAME_{Guid.NewGuid():N}");
        try
        {
            File.WriteAllText(output + ".SL", sl);
            var (rc, _, stderr) = RunSlangbuild(new[] { output + ".SL", "-o", output }.Concat(args).ToArray());
            return (rc, stderr, File.Exists(output + ".d88") ? File.ReadAllBytes(output + ".d88") : null);
        }
        finally
        {
            foreach (var ext in new[] { ".SL", ".d88", ".bin", ".sym", ".ASM", ".LST", ".tap" })
                if (File.Exists(output + ext)) File.Delete(output + ext);
        }
    }

    private static string BootName(byte[] d88)
    {
        var path = Path.Combine(Path.GetTempPath(), $"SLFSBOOT_{Guid.NewGuid():N}.d88");
        try
        {
            File.WriteAllBytes(path, d88);
            return Encoding.ASCII.GetString(D88Reader.FromFile(path).ReadSector(0, 0, 1), 1, 13);
        }
        finally { File.Delete(path); }
    }

    private const string EmptyMain = "MAIN() BEGIN END;\n";

    /// <summary>IPL の起動名 (= boot sector +01 の 13 char) を、引数 > SL の頭のコメント > env の順で決める</summary>
    [Theory]
    [InlineData("", null, "SLFSMAIN")]
    [InlineData("// disk-main-name: HINTED\n", null, "HINTED")]
    [InlineData("// disk-main-name: HINTED\n", "My Game", "My Game")]
    [InlineData("// disk-main-name: \u30b2\u30fc\u30e0\n", "OK", "OK")]                     // 有効な引数は不正なソースの名前を上書きできる
    [InlineData("", "ABCDEFGHIJKLM", "ABCDEFGHIJKLM")]                                // 13 文字ちょうど
    [InlineData("// a\r\n// disk-main-name:  First \r\n// disk-main-name: Second\r\n", null, "First")]  // 最初の行・前後の空白は除く
    [InlineData("\ufeff// disk-main-name: BOM\r\n", null, "BOM")]                       // BOM + CRLF
    [InlineData("// x\n\n// disk-main-name: LATE\n", null, "SLFSMAIN")]              // 頭のコメントの並びの外は読まない
    [InlineData("// x\n  // disk-main-name: LATE\n", null, "SLFSMAIN")]
    public void SlfsMainName_GoesToBootHeader(string head, string? cliName, string expected)
    {
        var args = new List<string> { "-E", "x1native_slfs", "--emit", "disk" };
        if (cliName != null) args.AddRange(new[] { "--disk-main-name", cliName });
        var (rc, stderr, d88) = BuildSl(head + EmptyMain, args.ToArray());
        Assert.True(rc == 0, $"slangbuild failed exit={rc}\nstderr:\n{stderr}");
        Assert.Equal(expected.PadRight(13), BootName(d88!));
    }

    [Theory]
    [InlineData("", new[] { "-E", "x1native_slfs", "--emit", "disk", "--disk-main-name", "ABCDEFGHIJKLMN" }, "invalid")]  // 14 文字
    [InlineData("", new[] { "-E", "x1native_slfs", "--emit", "disk", "--disk-main-name", "" }, "invalid")]                // 空
    [InlineData("// disk-main-name: \u30b2\u30fc\u30e0\n", new[] { "-E", "x1native_slfs", "--emit", "disk" }, "invalid")]  // ASCII 以外
    [InlineData("// disk-main-name:\n", new[] { "-E", "x1native_slfs", "--emit", "disk" }, "invalid")]                 // 名前なし
    [InlineData("", new[] { "-E", "x1native_slfs", "--disk-main-name", "A" }, "--disk-main-name requires --emit disk")]
    [InlineData("", new[] { "-E", "x1", "--emit", "disk", "--disk-main-name", "A" }, "--disk-main-name requires disk.tool: slfs-pack")]
    public void SlfsMainName_Rejected(string head, string[] args, string message)
    {
        var (rc, stderr, d88) = BuildSl(head + EmptyMain, args);
        Assert.NotEqual(0, rc);
        Assert.Contains(message, stderr);
        Assert.Null(d88);
    }

    /// <summary>名前を変えても、boot sector +01〜+0D の 13 byte 以外の D88 は同じ</summary>
    [Fact]
    public void SlfsMainName_ChangesOnlyBootName()
    {
        var (rc1, err1, a) = BuildSl(EmptyMain, "-E", "x1native_slfs", "--emit", "disk");
        var (rc2, err2, b) = BuildSl(EmptyMain, "-E", "x1native_slfs", "--emit", "disk", "--disk-main-name", "ANOTHERNAME12");
        Assert.True(rc1 == 0 && rc2 == 0, err1 + err2);
        Assert.Equal(a!.Length, b!.Length);
        var diff = Enumerable.Range(0, a.Length).Where(i => a[i] != b[i]).ToList();
        Assert.NotEmpty(diff);
        Assert.True(diff.Max() - diff.Min() < 13, $"differs at {diff.Min()}..{diff.Max()}");
        Assert.Equal("ANOTHERNAME12", Encoding.ASCII.GetString(b, diff.Min(), 13));
    }

    /// <summary>不正なソースの名前は、slfs-pack の disk 以外 (bin・tape) では読まない</summary>
    [Theory]
    [InlineData("x1native_slfs", "bin")]
    [InlineData("x1native", "tape")]
    public void SlfsMainName_SourceHintIgnoredOutsideSlfsDisk(string env, string emit)
    {
        var (rc, stderr, _) = BuildSl("// disk-main-name: \u30b2\u30fc\u30e0\n" + EmptyMain, "-E", env, "--emit", emit);
        Assert.True(rc == 0, $"slangbuild failed exit={rc}\nstderr:\n{stderr}");
    }

    /// <summary>hubasic2sl の出力 (Game.bas → PROG.SL) を別の出力名で組み立てても、起動名は BASIC のファイル名</summary>
    [Fact]
    public void SlfsMainName_FromHubasicSourceName()
    {
        var r = SLANGCompiler.HuBasic.HuTranspiler.Transpile("10 END",
            new SLANGCompiler.HuBasic.HuTranspileOptions(SourceName: "Game.bas"));
        Assert.NotNull(r.Output);
        var (rc, stderr, d88) = BuildSl(r.Output!, "-E", "x1native_slfs", "--emit", "disk");
        Assert.True(rc == 0, $"slangbuild failed exit={rc}\nstderr:\n{stderr}");
        Assert.Equal("Game".PadRight(13), BootName(d88!));
    }

    [Fact]
    public void SlfsDemo_Phase3_GeneratedHeader_IncludeAndCompile()
    {
        // SLFS Phase 3: SLFSDEMO.SL は #INCLUDE SLFSDEMO.assets.inc + CONST FILE_XXX
        // を使う。 slangbuild が compile 前に .inc 生成 + -I outputDir 追加で解決、
        // compile pass + D88 生成を pin。
        var repo = RepoRoot();
        var sample = Path.Combine(repo, "examples", "X1NATIVE_SLFS", "SLFSDEMO.SL");
        var assetsDir = Path.Combine(repo, "examples", "X1NATIVE_SLFS", "assets");
        var include = Path.Combine(repo, "include");
        var output = Path.Combine(Path.GetTempPath(), $"SLFSDEMO_p3_{Guid.NewGuid():N}");
        var incPath = Path.Combine(Path.GetTempPath(), "SLFSDEMO.assets.inc");
        try
        {
            // 旧残 cleanup
            if (File.Exists(incPath)) File.Delete(incPath);

            var (rc, stdout, stderr) = RunSlangbuild(
                $"-E x1native_slfs -I {include} {sample} -o {output} --emit disk --slfs-add {assetsDir} --keep-asm");
            Assert.True(rc == 0, $"slangbuild failed exit={rc}\nstdout:\n{stdout}\nstderr:\n{stderr}");
            Assert.True(File.Exists(output + ".d88"), "SLFSDEMO.d88 not generated");

            // generated .inc が input SL stem 基準で生成された事 確認
            Assert.True(File.Exists(incPath), "SLFSDEMO.assets.inc not generated (input SL stem 基準)");
            var hdr = File.ReadAllText(incPath);
            Assert.Contains("FILE_GREETING_TXT", hdr);
            Assert.Contains("FILE_NUMBERS_BIN", hdr);
            Assert.Contains("CONST", hdr);
        }
        finally
        {
            foreach (var ext in new[] { ".d88", ".bin", ".sym", ".ASM", ".LST", ".inc" })
                if (File.Exists(output + ext)) File.Delete(output + ext);
            if (File.Exists(incPath)) File.Delete(incPath);
        }
    }

    [Fact]
    public void SlfsDemo_Phase3_OutputBasename_DiffersFromSourceStem()
    {
        // Codex Medium 指摘の最重要 case: -o /tmp/OTHER ≠ source SL stem の場合、
        // generated .inc 名は **source SL stem 基準** (= SLFSDEMO.assets.inc)、
        // -o basename 「OTHER」 に依存しない事 pin。
        var repo = RepoRoot();
        var sample = Path.Combine(repo, "examples", "X1NATIVE_SLFS", "SLFSDEMO.SL");
        var assetsDir = Path.Combine(repo, "examples", "X1NATIVE_SLFS", "assets");
        var include = Path.Combine(repo, "include");
        var outDir = Path.Combine(Path.GetTempPath(), $"slfs_other_{Guid.NewGuid():N}");
        Directory.CreateDirectory(outDir);
        var output = Path.Combine(outDir, "OTHER");                 // -o basename = OTHER
        var expectedIncPath = Path.Combine(outDir, "SLFSDEMO.assets.inc");  // source SL stem 基準
        try
        {
            var (rc, stdout, stderr) = RunSlangbuild(
                $"-E x1native_slfs -I {include} {sample} -o {output} --emit disk --slfs-add {assetsDir} --keep-asm");
            Assert.True(rc == 0, $"slangbuild failed exit={rc}\nstdout:\n{stdout}\nstderr:\n{stderr}");
            Assert.True(File.Exists(output + ".d88"), "OTHER.d88 not generated");
            Assert.True(File.Exists(expectedIncPath),
                $"SLFSDEMO.assets.inc not generated at expected path {expectedIncPath} " +
                "(input SL stem 基準でなく -o basename 依存になってる疑い、 Codex 指摘の事故 case)");
        }
        finally
        {
            if (Directory.Exists(outDir)) Directory.Delete(outDir, recursive: true);
        }
    }

    [Fact]
    public void SlfsDemoSave_BuildsViaSlangbuild()
    {
        // Phase 2: FS_SAVE_R / FS_SAVE_W を呼ぶ sample (= SLFSDEMO_SAVE.SL) が
        // slangbuild --emit disk 経由で build 通る pin (= runtime link + 3 引数 ABI
        // + ARRAY BYTE buffer 渡しの assemble 確認、 emulator 動作確認は別途 user)。
        var repo = RepoRoot();
        var sample = Path.Combine(repo, "examples", "X1NATIVE_SLFS", "SLFSDEMO_SAVE.SL");
        var assetsDir = Path.Combine(repo, "examples", "X1NATIVE_SLFS", "assets");
        var include = Path.Combine(repo, "include");
        var output = Path.Combine(Path.GetTempPath(), $"SLFSSAVE_test_{Guid.NewGuid():N}");
        var d88 = output + ".d88";

        try
        {
            var (rc, stdout, stderr) = RunSlangbuild(
                $"-E x1native_slfs -I {include} {sample} -o {output} --emit disk --slfs-add {assetsDir}");
            Assert.True(rc == 0, $"slangbuild failed exit={rc}\nstdout:\n{stdout}\nstderr:\n{stderr}");
            Assert.True(File.Exists(d88), "SLFSSAVE.d88 not generated");

            var r = D88Reader.FromFile(d88);
            // superblock の save_area fields 確認 (= save_start > 0, save_count > 0)
            var sb = r.ReadSector(0, 0, 2);
            int saveStart = sb[0x0E] | (sb[0x0F] << 8);
            int saveCount = sb[0x10] | (sb[0x11] << 8);
            Assert.True(saveStart > 0, $"save_area_start_sector = {saveStart} (expected > 0)");
            Assert.True(saveCount > 0, $"save_sector_count = {saveCount} (expected > 0)");
        }
        finally
        {
            foreach (var ext in new[] { ".d88", ".bin", ".sym", ".ASM", ".LST", ".inc" })
                if (File.Exists(output + ext)) File.Delete(output + ext);
        }
    }
}
