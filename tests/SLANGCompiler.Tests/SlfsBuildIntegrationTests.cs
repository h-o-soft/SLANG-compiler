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
    {
        var repo = RepoRoot();
        var psi = new ProcessStartInfo("dotnet")
        {
            WorkingDirectory = repo,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
        };
        psi.ArgumentList.Add("run");
        psi.ArgumentList.Add("--project");
        psi.ArgumentList.Add(Path.Combine(repo, "src", "SLANGCompiler.Build"));
        psi.ArgumentList.Add("--no-build");
        psi.ArgumentList.Add("--");
        foreach (var a in args.Split(' ', StringSplitOptions.RemoveEmptyEntries))
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
}
