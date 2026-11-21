using System.Diagnostics;
using System.Text;
using SLANGCompiler.Build;
using SLANGCompiler.Runtime;
using SLANGCompiler.SlfsPack;
using Xunit;

namespace SLANGCompiler.Tests;

/// <summary>x1native_hufs のローダの形のディスク (HuLoader・HuDiskImage.SetMainWithLoader・slangbuild --hufs-loader)</summary>
public class HuLoaderTests
{
    private static byte[] Body(int n)
    {
        var b = new byte[n];
        // レコードごとに並びが違う (256 バイトごとにくり返さない) ので、レコードの順の取り違えも分かる
        for (int i = 0; i < n; i++) b[i] = (byte)(i * 7 + 3 + (i >> 8) * 31);
        return b;
    }

    private static ushort Word(byte[] b, int off) => (ushort)(b[off] | b[off + 1] << 8);

    // ------------------------------------------------------------------ ローダのバイト列

    /// <summary>runtime/x1/hufsboot.asm を組んだものが HuLoader のバイト列と同じで、768 バイト以下</summary>
    [SkippableFact]
    public void LoaderBytes_MatchSource()
    {
        var repo = TestTools.RepoRoot();
        var asmTool = Path.Combine(repo, "tools", OperatingSystem.IsWindows() ? "AILZ80ASM.exe" : "AILZ80ASM");
        Skip.IfNot(File.Exists(asmTool), "AILZ80ASM がない (make setup-tools で取得)");
        var dir = Path.Combine(Path.GetTempPath(), "slang_hufsboot_" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(dir);
        try
        {
            var src = Path.Combine(dir, "hufsboot.asm");
            File.Copy(Path.Combine(repo, "runtime", "x1", "hufsboot.asm"), src);
            var bin = Path.Combine(dir, "hufsboot.bin");
            var psi = new ProcessStartInfo(asmTool)
            {
                WorkingDirectory = dir,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
            };
            foreach (var a in new[] { "-i", src, "-bin", bin, "-f" }) psi.ArgumentList.Add(a);
            using var proc = Process.Start(psi)!;
            var so = proc.StandardOutput.ReadToEndAsync();
            var se = proc.StandardError.ReadToEndAsync();
            if (!proc.WaitForExit(120_000))
            {
                try { proc.Kill(entireProcessTree: true); } catch { }
                Assert.Fail("AILZ80ASM timed out");
            }
            Assert.True(proc.ExitCode == 0 && File.Exists(bin), so.Result + se.Result);
            Assert.Equal(File.ReadAllBytes(bin), HuLoader.Bytes);
        }
        finally
        {
            try { Directory.Delete(dir, true); } catch { }
        }
    }

    [Fact]
    public void LoaderBytes_FitInThreeRecords()
    {
        Assert.InRange(HuLoader.Bytes.Length, 1, HuLoader.MaxSize);
        Assert.Equal(768, HuLoader.MaxSize);
        Assert.Equal(0x0100, HuLoader.Address);
        Assert.Equal(0x1000, HuLoader.MinLoadAddress);
    }

    // ------------------------------------------------------------------ ディスクの並び

    [Fact]
    public void SetMainWithLoader_Layout()
    {
        var disk = HuDiskImage.CreateBlank();
        var body = Body(20000);                  // 5 クラスタ
        var (loader, main) = disk.SetMainWithLoader("GAME", body, 0x1000, 0x1234);

        // レコード 0 の IPL の印はローダを指す
        var ipl = disk.ReadRecord(0);
        Assert.Equal(0x01, ipl[0]);
        Assert.Equal("GAME         ", Encoding.ASCII.GetString(ipl, 1, 13));
        Assert.Equal("Sys", Encoding.ASCII.GetString(ipl, 0x0E, 3));
        Assert.Equal(HuLoader.Bytes.Length, Word(ipl, 0x12));
        Assert.Equal(0x0100, Word(ipl, 0x14));
        Assert.Equal(0x0100, Word(ipl, 0x16));
        Assert.Equal(2 * HuDiskImage.ClusterSize, ipl[0x1D] | ipl[0x1E] << 8 | ipl[0x1F] << 16);

        // ローダの項目: Sys、$41、クラスタ 2
        Assert.Equal("GAME.Sys", loader.DisplayName);
        Assert.Equal(0x41, loader.Type);
        Assert.Equal(2, loader.StartCluster);
        Assert.Equal(HuLoader.Bytes, disk.Extract("GAME.Sys"));

        // 本体の項目: Bin、$41、番地・大きさ
        Assert.Equal("GAME.Bin", main.DisplayName);
        Assert.Equal(0x41, main.Type);
        Assert.Equal(0x1000, main.LoadAddress);
        Assert.Equal(0x1234, main.ExecAddress);
        Assert.Equal(body.Length, main.Size);
        Assert.Equal(3, main.StartCluster);
        Assert.Equal(body, disk.Extract("GAME.Bin"));
        var (chain, last) = disk.Chain(main.StartCluster);
        Assert.Equal(new[] { 3, 4, 5, 6, 7 }, chain);
        Assert.Equal((20000 + 255) / 256 - 4 * 16, last);

        Assert.Equal(new[] { "GAME.Sys", "GAME.Bin" }, disk.Entries().Select(e => e.DisplayName).ToArray());
    }

    /// <summary>半角カナの起動名: レコード 0 の名前はディレクトリの項目と同じバイト (ローダはこれで本体を探す)</summary>
    [Fact]
    public void SetMainWithLoader_KanaNameMatchesDirectory()
    {
        var disk = HuDiskImage.CreateBlank();
        var (_, main) = disk.SetMainWithLoader("ｹﾞｰﾑ", Body(100), 0x1000, 0x1000);
        Assert.Equal(main.NameBytes, disk.ReadRecord(0).AsSpan(1, 13).ToArray());
    }

    /// <summary>空きが先に埋まっていても、本体は続いていないクラスタに置ける</summary>
    [Fact]
    public void SetMainWithLoader_BodyNeedNotBeContiguous()
    {
        var disk = HuDiskImage.CreateBlank();
        // クラスタ 2〜6 を埋めてから 3 と 5 を空ける: ローダは 3、本体 (3 クラスタ) は 5・7・8
        foreach (var f in new[] { "A", "B", "C", "D", "E" }) disk.AddFile(f, Body(10));
        disk.Delete("B");
        disk.Delete("D");
        var (loader, main) = disk.SetMainWithLoader("GAME", Body(9000), 0x2000, 0x2000);
        Assert.Equal(3, loader.StartCluster);
        var (chain, _) = disk.Chain(main.StartCluster);
        Assert.Equal(new[] { 5, 7, 8 }, chain);
        Assert.Equal(Body(9000), disk.Extract("GAME.Bin"));
        Assert.Equal(3 * HuDiskImage.ClusterSize, disk.ReadRecord(0)[0x1D] | disk.ReadRecord(0)[0x1E] << 8);
    }

    [Theory]
    [InlineData(0x1000, 61441)]   // 10000H を越える
    [InlineData(0x0F00, 100)]     // 1000H より下
    [InlineData(0x0100, 100)]
    [InlineData(0x1000, 0)]       // 空
    [InlineData(0xF000, 4097)]
    public void SetMainWithLoader_RejectsBadAddressOrSize(int load, int size)
    {
        var disk = HuDiskImage.CreateBlank();
        Assert.Throws<ArgumentException>(() => disk.SetMainWithLoader("GAME", Body(size), (ushort)load, (ushort)load));
    }

    [Theory]
    [InlineData(0x1000, 61440)]   // ちょうど 10000H まで
    [InlineData(0xF000, 4096)]
    [InlineData(0x1000, 1)]
    public void SetMainWithLoader_AcceptsLimits(int load, int size)
    {
        var disk = HuDiskImage.CreateBlank();
        var body = Body(size);
        disk.SetMainWithLoader("GAME", body, (ushort)load, (ushort)load);
        Assert.Equal(body, disk.Extract("GAME.Bin"));
    }

    /// <summary>空きが足りずに失敗したら、ディスクは 1 バイトも変わらない (ローダだけが残らない)</summary>
    [Fact]
    public void SetMainWithLoader_LeavesImageUnchangedOnFailure()
    {
        var disk = HuDiskImage.CreateBlank();
        // 空きを 2 クラスタだけ残す: ローダは置けるが、本体 (2 クラスタ) は置けない
        disk.AddFile("FILL", Body((HuDiskImage.Clusters - HuDiskImage.FirstDataCluster - 2) * HuDiskImage.ClusterSize));
        Assert.Equal(2, disk.FreeClusters());
        var before = disk.ToRaw();
        Assert.Throws<IOException>(() => disk.SetMainWithLoader("GAME", Body(5000), 0x1000, 0x1000));
        Assert.Equal(before, disk.ToRaw());
        Assert.Null(disk.Find("GAME.Sys"));
        // 同じ名前でもう一度試せる (入る大きさなら通る)
        disk.SetMainWithLoader("GAME", Body(100), 0x1000, 0x1000);
        Assert.Equal(Body(100), disk.Extract("GAME.Bin"));
    }

    [Theory]
    [InlineData("GAME.Sys")]
    [InlineData("GAME.Bin")]
    public void SetMainWithLoader_RejectsExistingName(string existing)
    {
        var disk = HuDiskImage.CreateBlank();
        disk.AddFile(existing, Body(10));
        Assert.Throws<InvalidOperationException>(() => disk.SetMainWithLoader("GAME", Body(100), 0x1000, 0x1000));
    }

    /// <summary>env の main_load・main_exec が 16 ビットを越えたら、回り込ませずにエラーにし、ディスクを書かない</summary>
    [Theory]
    [InlineData(0x11000, null, true)]
    [InlineData(0x1000, 0x10000, true)]
    [InlineData(0x11000, null, false)]
    [InlineData(-1, null, false)]
    public void DiskImageBuilder_RejectsOutOfRangeAddresses(int load, int? exec, bool loader)
    {
        var dir = Path.Combine(Path.GetTempPath(), "slang_huaddr_" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(dir);
        try
        {
            var bin = Path.Combine(dir, "main.bin");
            File.WriteAllBytes(bin, Body(100));
            var d88 = Path.Combine(dir, "out.d88");
            var disk = new DiskConfig { Format = "d88", Tool = "hufs-pack", MainLoad = load, MainExec = exec };
            var builder = new DiskImageBuilder(disk, hufsLoader: loader);
            Assert.NotEqual(0, builder.Build(bin, new List<string>(), d88));
            Assert.False(File.Exists(d88));
        }
        finally
        {
            try { Directory.Delete(dir, true); } catch { }
        }
    }

    /// <summary>HuPack: ローダの形でも、足すファイルは本体の後ろに名前の順に並ぶ。ローダなしは今までどおり</summary>
    [Fact]
    public void HuPack_LoaderOption()
    {
        var dir = Path.Combine(Path.GetTempPath(), "slang_huloader_" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(dir);
        try
        {
            File.WriteAllBytes(Path.Combine(dir, "z.dat"), Body(300));
            File.WriteAllBytes(Path.Combine(dir, "a.dat"), Body(5000));
            var adds = new[] { HuPack.ParseAddSpec($"ZZ.DAT={Path.Combine(dir, "z.dat")}"),
                               HuPack.ParseAddSpec($"AA.DAT={Path.Combine(dir, "a.dat")}") };
            var body = Body(9000);

            var withLoader = HuDiskImage.FromD88(HuPack.Build(body, "MAIN", 0x1000, 0x1000, adds, loader: true));
            Assert.Equal(new[] { "MAIN.Sys", "MAIN.Bin", "AA.DAT", "ZZ.DAT" },
                withLoader.Entries().Select(e => e.DisplayName).ToArray());
            Assert.Equal(body, withLoader.Extract("MAIN.Bin"));

            var plain = HuDiskImage.FromD88(HuPack.Build(body, "MAIN", 0x1000, 0x1000, adds));
            Assert.Equal(new[] { "MAIN.Sys", "AA.DAT", "ZZ.DAT" }, plain.Entries().Select(e => e.DisplayName).ToArray());
            Assert.Equal(body, plain.Extract("MAIN.Sys"));
        }
        finally
        {
            try { Directory.Delete(dir, true); } catch { }
        }
    }
}
