using System.Diagnostics;
using System.Runtime.InteropServices;
using Xunit;

namespace SLANGCompiler.Tests;

/// <summary>
/// 試験が slangbuild を動かすときに --slangc で渡す slangc。slangbuild は slangc を同梱・開発の publish・PATH から探し、
/// どこにも無ければ止まるので、試験は開発の木の slangc を publish したもの (無ければ、この試験のプロセスで 1 回だけ publish する) を明示して渡す
/// </summary>
internal static class TestTools
{
    private static readonly Lazy<string> _slangc = new(PublishSlangc, LazyThreadSafetyMode.ExecutionAndPublication);

    /// <summary>publish した slangc の道 (src/SLANGCompiler.CLI/bin/Release/net10.0/&lt;RID&gt;/publish/slangc(.exe))</summary>
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
        var exePath = Path.Combine(root, "src", "SLANGCompiler.CLI", "bin", "Release", "net10.0", rid, "publish",
                                   OperatingSystem.IsWindows() ? "slangc.exe" : "slangc");
        if (File.Exists(exePath)) return exePath;   // 前に publish したものを使う (試験のあいだで publish を重ねない)

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
                                  "--self-contained", "true", "-p:PublishSingleFile=true" })
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
