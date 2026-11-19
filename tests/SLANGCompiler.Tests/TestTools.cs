using System.Diagnostics;
using System.Runtime.InteropServices;
using Xunit;

namespace SLANGCompiler.Tests;

/// <summary>
/// 試験が slangbuild を動かすときに --slangc で渡す slangc。slangbuild は slangc を同梱・開発の publish・PATH から探し、
/// どこにも無ければ止まるので、試験は今の木の slangc を明示して渡す。
/// 試験のプロセスごとに 1 回、今の木の CLI をこのプロセスだけの一時のフォルダに publish する
/// (前に publish した古い slangc を使わず、同時に動くほかの試験のプロセスとも取り合わない。終わったら消す)
/// </summary>
internal static class TestTools
{
    private static readonly Lazy<string> _slangc = new(PublishSlangc, LazyThreadSafetyMode.ExecutionAndPublication);

    /// <summary>このプロセスで publish した slangc の道</summary>
    internal static string SlangcPath => _slangc.Value;

    internal static string RepoRoot()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir != null && !File.Exists(Path.Combine(dir.FullName, "SLANGCompiler.sln")))
            dir = dir.Parent;
        Assert.NotNull(dir);
        return dir!.FullName;
    }

    internal static string CurrentRid()
    {
        var arch = RuntimeInformation.OSArchitecture == Architecture.Arm64 ? "arm64" : "x64";
        if (OperatingSystem.IsMacOS()) return $"osx-{arch}";
        if (OperatingSystem.IsLinux()) return $"linux-{arch}";
        if (OperatingSystem.IsWindows()) return $"win-{arch}";
        throw new PlatformNotSupportedException();
    }

    private static string PublishSlangc()
    {
        var root = RepoRoot();
        var rid = CurrentRid();
        var outDir = Path.Combine(Path.GetTempPath(), $"slang_test_slangc_{Environment.ProcessId}_{Guid.NewGuid():N}");
        AppDomain.CurrentDomain.ProcessExit += (_, _) =>
        {
            try { Directory.Delete(outDir, true); } catch { }
        };
        var exePath = Path.Combine(outDir, OperatingSystem.IsWindows() ? "slangc.exe" : "slangc");

        var psi = new ProcessStartInfo
        {
            FileName = "dotnet",
            WorkingDirectory = root,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
        };
        psi.Environment["MSBUILDDISABLENODEREUSE"] = "1";
        foreach (var a in new[] { "publish", "src/SLANGCompiler.CLI/SLANGCompiler.CLI.csproj", "-c", "Release", "-r", rid,
                                  "--self-contained", "true", "-p:PublishSingleFile=true", "-o", outDir })
            psi.ArgumentList.Add(a);
        using var proc = Process.Start(psi)!;
        var stdoutTask = proc.StandardOutput.ReadToEndAsync();
        var stderrTask = proc.StandardError.ReadToEndAsync();
        if (!proc.WaitForExit(180_000))
        {
            try { proc.Kill(entireProcessTree: true); } catch { }
            Assert.Fail("dotnet publish timed out after 180s");
        }
        var output = stdoutTask.GetAwaiter().GetResult() + stderrTask.GetAwaiter().GetResult();
        Assert.True(proc.ExitCode == 0, $"slangc を publish できませんでした\n{output}");
        Assert.True(File.Exists(exePath), $"slangc publish output not found: {exePath}");
        return exePath;
    }
}
