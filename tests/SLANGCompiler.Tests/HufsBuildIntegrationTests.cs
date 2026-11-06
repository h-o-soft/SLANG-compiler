using Xunit;
using SLANGCompiler.SlfsPack;
using System.Diagnostics;
using System.Text;

namespace SLANGCompiler.Tests;

/// <summary>slangbuild の x1native_hufs (disk.tool: hufs-pack) の試験</summary>
public class HufsBuildIntegrationTests
{
    private static string RepoRoot()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir != null && !File.Exists(Path.Combine(dir.FullName, "runtime", "env", "x1native.env")))
            dir = dir.Parent;
        Assert.NotNull(dir);
        return dir!.FullName;
    }

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
        foreach (var a in args) psi.ArgumentList.Add(a);
        using var proc = Process.Start(psi)!;
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
    public void HufsRaw_BuildsHuFormatDisk()
    {
        var repo = RepoRoot();
        var sample = Path.Combine(repo, "examples", "X1NATIVE_HUFS", "HUFSRAW.SL");
        var greet = Path.Combine(repo, "examples", "X1NATIVE_HUFS", "files", "GREET.TXT");
        var output = Path.Combine(Path.GetTempPath(), $"HUFSRAW_test_{Guid.NewGuid():N}");
        try
        {
            var (rc, stdout, stderr) = RunSlangbuild("-E", "x1native_hufs", sample, "-o", output, "--emit", "disk",
                "--hufs-add", $"GREET.TXT={greet},text", "--disk-main-name", "RAW TEST");
            Assert.True(rc == 0, $"slangbuild failed exit={rc}\nstdout:\n{stdout}\nstderr:\n{stderr}");
            var bin = File.ReadAllBytes(output + ".bin");
            var disk = HuDiskImage.FromD88(File.ReadAllBytes(output + ".d88"));

            var names = disk.Entries().Select(e => e.DisplayName).ToArray();
            Assert.Equal(new[] { "RAW TEST.Sys", "GREET.TXT" }, names);
            Assert.Equal(bin, disk.Extract("RAW TEST.Sys"));
            Assert.Equal(2, disk.Find("RAW TEST.Sys")!.StartCluster);
            Assert.Equal("HELLO\nWORLD\n", Encoding.ASCII.GetString(disk.Extract("GREET.TXT", text: true)));

            var ipl = disk.ReadRecord(0);
            Assert.Equal(0x01, ipl[0]);
            Assert.Equal("RAW TEST     ", Encoding.ASCII.GetString(ipl, 1, 13));
            Assert.Equal(bin.Length, ipl[0x12] | ipl[0x13] << 8);
            Assert.Equal(0x1000, ipl[0x14] | ipl[0x15] << 8);
            Assert.Equal(0x1000, ipl[0x16] | ipl[0x17] << 8);

            // slfs の関数はこの環境ではリンクしない
            Assert.DoesNotContain("FS_READ_BY_ID", File.Exists(output + ".sym") ? File.ReadAllText(output + ".sym") : "");
        }
        finally
        {
            foreach (var ext in new[] { ".d88", ".bin", ".SL", ".asm", ".sym", ".LST" })
                if (File.Exists(output + ext)) File.Delete(output + ext);
        }
    }

    [Fact]
    public void Hufs_RejectsSlfsAdd_AndHufsAddRequiresHufsPack()
    {
        var repo = RepoRoot();
        var sample = Path.Combine(repo, "examples", "X1NATIVE_HUFS", "HUFSRAW.SL");
        var greet = Path.Combine(repo, "examples", "X1NATIVE_HUFS", "files", "GREET.TXT");
        var output = Path.Combine(Path.GetTempPath(), $"HUFSERR_test_{Guid.NewGuid():N}");
        try
        {
            var (rc1, _, err1) = RunSlangbuild("-E", "x1native_hufs", sample, "-o", output, "--emit", "disk",
                "--slfs-add", $"G:{greet}");
            Assert.NotEqual(0, rc1);
            Assert.Contains("--slfs-add can't be used with disk.tool: hufs-pack", err1);

            var (rc2, _, err2) = RunSlangbuild("-E", "x1native_slfs", sample, "-o", output, "--emit", "disk",
                "--hufs-add", greet);
            Assert.NotEqual(0, rc2);
            Assert.Contains("--hufs-add requires --emit disk + disk.tool: hufs-pack", err2);

            var (rc3, _, err3) = RunSlangbuild("-E", "x1native_hufs", sample, "-o", output, "--emit", "disk",
                "--hufs-add", "TOOLONGNAMEXXXX.TXT=" + greet);
            Assert.NotEqual(0, rc3);
            Assert.Contains("hufs-pack error", err3);
        }
        finally
        {
            foreach (var ext in new[] { ".d88", ".bin", ".SL", ".asm", ".sym", ".LST" })
                if (File.Exists(output + ext)) File.Delete(output + ext);
        }
    }

    [Fact]
    public void Hufs_SlfsFunctionsAreNotLinkable()
    {
        var src = Path.Combine(Path.GetTempPath(), $"HUFSSLFS_{Guid.NewGuid():N}.SL");
        var output = Path.ChangeExtension(src, null);
        File.WriteAllText(src, "ARRAY BYTE B[255];\nVAR R;\nMAIN()\nBEGIN\n    R = FS_READ_BY_ID(0, B);\nEND;\n");
        try
        {
            var (rc, stdout, stderr) = RunSlangbuild("-E", "x1native_hufs", src, "-o", output, "--emit", "bin");
            Assert.True(rc != 0, $"FS_READ_BY_ID が x1native_hufs でリンクできてしまった\nstdout:\n{stdout}\nstderr:\n{stderr}");
        }
        finally
        {
            foreach (var f in Directory.GetFiles(Path.GetDirectoryName(src)!, Path.GetFileName(output) + "*"))
                File.Delete(f);
        }
    }
}
