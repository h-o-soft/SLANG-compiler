using System.Diagnostics;
using SLANGCompiler.Build;
using SLANGCompiler.HuBasic.CLI;
using Xunit;

namespace SLANGCompiler.Tests;

/// <summary>開発の木の道具を組み立てる試験は、同じ csproj を同時に組み立てないように 1 つずつ動かす</summary>
[CollectionDefinition("DevToolBuild", DisableParallelization = true)]
public class DevToolBuildCollection { }

/// <summary>
/// 開発の木の道具の最後の手 (slangbuild の slangc、hubasic2sl の slangbuild) は、
/// 呼んだ人のフォルダの global.json が別の SDK に決めていても動く
/// (組み立ては木の SDK の決まりで行い、動かすのは呼んだ人のフォルダのまま)。
/// ここでは、無い SDK の版に決めたフォルダから呼ぶ。`dotnet run --project` なら SDK が見つからずに止まる所
/// </summary>
[Collection("DevToolBuild")]
public class DevToolFallbackTests : IDisposable
{
    private readonly string _pinnedDir;
    private readonly string _repo;

    public DevToolFallbackTests()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir != null && !File.Exists(Path.Combine(dir.FullName, "SLANGCompiler.sln")))
            dir = dir.Parent;
        Assert.NotNull(dir);
        _repo = dir!.FullName;
        _pinnedDir = Path.Combine(Path.GetTempPath(), "slang_pinned_" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(_pinnedDir);
        File.WriteAllText(Path.Combine(_pinnedDir, "global.json"),
            "{\"sdk\":{\"version\":\"9.0.999\",\"rollForward\":\"disable\"}}");
    }

    public void Dispose()
    {
        try { Directory.Delete(_pinnedDir, true); } catch { }
    }

    private static string Dotnet()
        => Environment.GetEnvironmentVariable("DOTNET_HOST_PATH") is { Length: > 0 } p && File.Exists(p) ? p : "dotnet";

    [Fact]
    public void PinnedFolder_CannotUseDotnetRun()
    {
        // 前提の確かめ: このフォルダからの dotnet run --project は、SDK が見つからずに止まる
        var psi = new ProcessStartInfo(Dotnet())
        {
            WorkingDirectory = _pinnedDir,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
        };
        foreach (var a in new[] { "run", "--project", Path.Combine(_repo, "src", "SLANGCompiler.CLI", "SLANGCompiler.CLI.csproj"), "-c", "Release", "--", "--version" })
            psi.ArgumentList.Add(a);
        using var proc = Process.Start(psi)!;
        var err = proc.StandardError.ReadToEndAsync();
        proc.StandardOutput.ReadToEnd();
        proc.WaitForExit();
        Assert.NotEqual(0, proc.ExitCode);
    }

    [Fact]
    public void SlangbuildFallback_BuildsSlangcInRepoAndRunsFromCallerFolder()
    {
        var csproj = Path.Combine(_repo, "src", "SLANGCompiler.CLI", "SLANGCompiler.CLI.csproj");
        var dll = DevToolBuild.Build(Dotnet(), csproj, "slangc", out var log);
        Assert.True(dll != null, log);

        var psi = new ProcessStartInfo(Dotnet())
        {
            WorkingDirectory = _pinnedDir,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
        };
        psi.ArgumentList.Add(dll!);
        psi.ArgumentList.Add("--version");
        using var proc = Process.Start(psi)!;
        var stdout = proc.StandardOutput.ReadToEnd();
        var stderr = proc.StandardError.ReadToEnd();
        proc.WaitForExit();
        Assert.True(proc.ExitCode == 0, stderr);
        Assert.StartsWith("slangc ", stdout);
    }

    [Fact]
    public void HuBasicFallback_BuildsSlangbuildInRepoAndRunsFromCallerFolder()
    {
        var csproj = Path.Combine(_repo, "src", "SLANGCompiler.Build", "SLANGCompiler.Build.csproj");
        var tool = new SlangbuildRunner.Resolved(Dotnet(), csproj);
        Assert.Equal(0, SlangbuildRunner.Run(tool, new[] { "--version" }, _pinnedDir));
    }
}
