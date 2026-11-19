using System.Diagnostics;
using SLANGCompiler.Build;
using SLANGCompiler.HuBasic.CLI;
using Xunit;
using SkippableFactAttribute = Xunit.SkippableFactAttribute;

namespace SLANGCompiler.Tests;

/// <summary>開発の木の道具を組み立てる試験は、同じ csproj を同時に組み立てないように 1 つずつ動かす</summary>
[CollectionDefinition("DevToolBuild", DisableParallelization = true)]
public class DevToolBuildCollection { }

/// <summary>
/// 開発の木の道具の最後の手 (slangbuild の slangc、hubasic2sl の slangbuild) は、
/// 呼んだ人のフォルダの global.json が別の SDK に決めていても動き、呼んだ人のフォルダからの相対の道を読む
/// (組み立ては木の SDK の決まりで行い、動かすのは呼んだ人のフォルダのまま)。
/// ここでは、無い SDK の版に決めたフォルダから、本番と同じ道 (DevToolBuild.CreateStartInfo・SlangbuildRunner.Run) で呼ぶ。
/// `dotnet run --project` なら SDK が見つからずに止まる所
/// </summary>
[Collection("DevToolBuild")]
public class DevToolFallbackTests : IDisposable
{
    private const string Program = "VAR J;\nMAIN()\nBEGIN\n  J = 1;\nEND;\n";

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
        File.WriteAllText(Path.Combine(_pinnedDir, "PROG.SL"), Program);
    }

    public void Dispose()
    {
        try { Directory.Delete(_pinnedDir, true); } catch { }
    }

    private static string Dotnet()
        => Environment.GetEnvironmentVariable("DOTNET_HOST_PATH") is { Length: > 0 } p && File.Exists(p) ? p : "dotnet";

    private static (int Code, string Stdout, string Stderr) RunToEnd(ProcessStartInfo psi)
    {
        psi.RedirectStandardOutput = true;
        psi.RedirectStandardError = true;
        using var proc = Process.Start(psi)!;
        var err = proc.StandardError.ReadToEndAsync();
        var stdout = proc.StandardOutput.ReadToEnd();
        proc.WaitForExit();
        return (proc.ExitCode, stdout, err.Result);
    }

    [Fact]
    public void PinnedFolder_CannotUseDotnetRun()
    {
        // 前提の確かめ: このフォルダからの dotnet run --project は、SDK が見つからずに止まる
        var psi = new ProcessStartInfo(Dotnet()) { WorkingDirectory = _pinnedDir, UseShellExecute = false };
        foreach (var a in new[] { "run", "--project", Path.Combine(_repo, "src", "SLANGCompiler.CLI", "SLANGCompiler.CLI.csproj"), "-c", "Release", "--", "--version" })
            psi.ArgumentList.Add(a);
        Assert.NotEqual(0, RunToEnd(psi).Code);
    }

    [Fact]
    public void SlangbuildFallback_CompilesRelativeInputFromPinnedFolder()
    {
        // slangbuild が slangc を呼ぶのと同じ道 (Driver.SpawnSlangc も DevToolBuild.CreateStartInfo で作る)
        var tool = new ResolvedTool(Dotnet(), ResolutionKind.DotnetRun,
            Path.Combine(_repo, "src", "SLANGCompiler.CLI", "SLANGCompiler.CLI.csproj"));
        var psi = DevToolBuild.CreateStartInfo(tool, "slangc", new[] { "-E", "x1native", "-o", "OUT.ASM", "PROG.SL" }, out var log);
        Assert.True(psi != null, log);
        Assert.EndsWith(Path.Combine("bin", "devtool", "slangc.dll"), psi!.ArgumentList[0]);
        psi.WorkingDirectory = _pinnedDir;
        psi.Environment["SLANG_HOME"] = _repo;   // ランタイムと env はこの木のもの
        var (code, _, stderr) = RunToEnd(psi);
        Assert.True(code == 0, stderr);
        Assert.True(File.Exists(Path.Combine(_pinnedDir, "OUT.ASM")), stderr);
    }

    [SkippableFact]
    public void HuBasicFallback_BuildsRelativeInputFromPinnedFolder()
    {
        // hubasic2sl が slangbuild を呼ぶ道 (SlangbuildRunner.Run) で、相対の道の SL を組み立てる
        var rid = OperatingSystem.IsWindows() ? "win-x64" : OperatingSystem.IsMacOS() ? "osx-arm64" : "linux-x64";
        var slangc = Path.Combine(_repo, "src", "SLANGCompiler.CLI", "bin", "Release", "net10.0", rid, "publish",
                                  OperatingSystem.IsWindows() ? "slangc.exe" : "slangc");
        var asm = Path.Combine(_repo, "tools", OperatingSystem.IsWindows() ? "AILZ80ASM.exe" : "AILZ80ASM");
        Skip.IfNot(File.Exists(slangc), "publish した slangc がない");
        Skip.IfNot(File.Exists(asm), "AILZ80ASM がない");

        var tool = new SlangbuildRunner.Resolved(Dotnet(),
            Path.Combine(_repo, "src", "SLANGCompiler.Build", "SLANGCompiler.Build.csproj"));
        var code = SlangbuildRunner.Run(tool, new[] { "-E", "x1native", "PROG.SL", "--slangc", slangc, "--asm", asm },
            _pinnedDir, new Dictionary<string, string> { ["SLANG_HOME"] = _repo });
        Assert.Equal(0, code);
        Assert.True(File.Exists(Path.Combine(_pinnedDir, "PROG.bin")));
    }
}
