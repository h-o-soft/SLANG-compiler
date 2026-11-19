using System.Diagnostics;
using System.Runtime.InteropServices;
using SLANGCompiler.Build;
using SLANGCompiler.HuBasic.CLI;
using Xunit;
using SkippableFactAttribute = Xunit.SkippableFactAttribute;

namespace SLANGCompiler.Tests;

/// <summary>
/// 開発の木の道具を組み立てる試験は、同じ csproj を同時に組み立てず、環境変数 OutDir を一時的に変えるので、ほかの試験と並べずに 1 つずつ動かす
/// </summary>
[CollectionDefinition("DevToolBuild", DisableParallelization = true)]
public class DevToolBuildCollection { }

/// <summary>
/// 開発の木の道具の最後の手 (slangbuild の slangc、hubasic2sl の slangbuild) は、
/// 呼んだ人のフォルダの global.json が別の SDK に決めていても動き、呼んだ人のフォルダからの相対の道を読む
/// (組み立ては木の SDK の決まりで行い、動かすのは呼んだ人のフォルダのまま)。
/// ここでは、無い SDK の版に決めたフォルダから呼ぶ。`dotnet run --project` なら SDK が見つからずに止まる所。
/// 組み立てた物の置き場は OutDir をコマンド行で決めるので、環境変数の OutDir や前の残りの dll に左右されない
/// </summary>
[Collection("DevToolBuild")]
public class DevToolFallbackTests : IDisposable
{
    private const string Source = "VAR J;\nMAIN()\nBEGIN\n  J = 1;\nEND;\n";

    private readonly string _pinnedDir;
    private readonly string _repo;
    private readonly List<string> _cleanup = new();

    public DevToolFallbackTests()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir != null && !File.Exists(Path.Combine(dir.FullName, "SLANGCompiler.sln")))
            dir = dir.Parent;
        Assert.NotNull(dir);
        _repo = dir!.FullName;
        _pinnedDir = NewTempDir("slang_pinned_");
        File.WriteAllText(Path.Combine(_pinnedDir, "global.json"),
            "{\"sdk\":{\"version\":\"9.0.999\",\"rollForward\":\"disable\"}}");
        File.WriteAllText(Path.Combine(_pinnedDir, "PROG.SL"), Source);
    }

    public void Dispose()
    {
        foreach (var d in _cleanup)
            try { Directory.Delete(d, true); } catch { }
    }

    private string NewTempDir(string prefix)
    {
        var d = Path.Combine(Path.GetTempPath(), prefix + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(d);
        _cleanup.Add(d);
        return d;
    }

    /// <summary>dotnet の絶対の道 (dotnet test が渡す DOTNET_HOST_PATH、なければ PATH から)</summary>
    private static string Dotnet()
    {
        if (Environment.GetEnvironmentVariable("DOTNET_HOST_PATH") is { Length: > 0 } p && File.Exists(p)) return p;
        var name = OperatingSystem.IsWindows() ? "dotnet.exe" : "dotnet";
        foreach (var d in (Environment.GetEnvironmentVariable("PATH") ?? "").Split(Path.PathSeparator))
        {
            var c = Path.Combine(d, name);
            if (d.Length > 0 && File.Exists(c)) return c;
        }
        return name;
    }

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

    private string CliCsproj => Path.Combine(_repo, "src", "SLANGCompiler.CLI", "SLANGCompiler.CLI.csproj");
    private string BuildCsproj => Path.Combine(_repo, "src", "SLANGCompiler.Build", "SLANGCompiler.Build.csproj");

    /// <summary>環境変数 OutDir を別の所に向け、組み立ての置き場に壊れた dll を置いてから body を動かす (終わったら戻す)</summary>
    private void WithRedirectedOutDirAndStaleDll(string staleDll, Action<string> body)
    {
        var elsewhere = NewTempDir("slang_outdir_");
        Directory.CreateDirectory(Path.GetDirectoryName(staleDll)!);
        File.WriteAllBytes(staleDll, new byte[] { 0x53, 0x54, 0x41, 0x4C, 0x45 });   // 古い残り (dll として読めない)
        var old = Environment.GetEnvironmentVariable("OutDir");
        Environment.SetEnvironmentVariable("OutDir", elsewhere + Path.DirectorySeparatorChar);
        try { body(elsewhere); }
        finally { Environment.SetEnvironmentVariable("OutDir", old); }
    }

    [Fact]
    public void PinnedFolder_CannotUseDotnetRun()
    {
        // 前提の確かめ: このフォルダからの dotnet run --project は、SDK が見つからずに止まる
        var psi = new ProcessStartInfo(Dotnet()) { WorkingDirectory = _pinnedDir, UseShellExecute = false };
        foreach (var a in new[] { "run", "--project", CliCsproj, "-c", "Release", "--", "--version" })
            psi.ArgumentList.Add(a);
        Assert.NotEqual(0, RunToEnd(psi).Code);
    }

    [Fact]
    public void SlangcFallback_CompilesRelativeInputFromPinnedFolder()
    {
        // slangbuild が slangc を動かす道 (Driver.SpawnSlangc も DevToolBuild.CreateStartInfo で作る)
        var tool = new ResolvedTool(Dotnet(), ResolutionKind.DotnetRun, CliCsproj);
        var psi = DevToolBuild.CreateStartInfo(tool, "slangc", new[] { "-E", "x1native", "-o", "OUT.ASM", "PROG.SL" }, out var log);
        Assert.True(psi != null, log);
        Assert.Equal(Path.Combine(Path.GetDirectoryName(CliCsproj)!, "bin", "devtool", "slangc.dll"), psi!.ArgumentList[0]);
        psi.WorkingDirectory = _pinnedDir;
        psi.Environment["SLANG_HOME"] = _repo;   // ランタイムと env はこの木のもの
        var (code, _, stderr) = RunToEnd(psi);
        Assert.True(code == 0, stderr);
        Assert.True(File.Exists(Path.Combine(_pinnedDir, "OUT.ASM")), stderr);
    }

    [Fact]
    public void SlangcFallback_IgnoresOutDirFromEnvironmentAndStaleDll()
    {
        var staleDll = Path.Combine(Path.GetDirectoryName(CliCsproj)!, "bin", "devtool", "slangc.dll");
        WithRedirectedOutDirAndStaleDll(staleDll, elsewhere =>
        {
            var dll = DevToolBuild.Build(Dotnet(), CliCsproj, "slangc", out var log);
            Assert.True(dll != null, log);
            Assert.Equal(staleDll, dll);
            Assert.False(File.Exists(Path.Combine(elsewhere, "slangc.dll")), "環境変数の OutDir に組み立てた");
            var psi = new ProcessStartInfo(Dotnet()) { WorkingDirectory = _pinnedDir, UseShellExecute = false };
            psi.ArgumentList.Add(dll!);
            psi.ArgumentList.Add("--version");
            var (code, stdout, stderr) = RunToEnd(psi);
            Assert.True(code == 0, stderr);
            Assert.StartsWith("slangc ", stdout);
        });
    }

    [Fact]
    public void HuBasicFallback_IgnoresOutDirFromEnvironmentAndStaleDll()
    {
        // hubasic2sl が slangbuild を呼ぶ道 (SlangbuildRunner.Run)。AILZ80ASM も publish した slangc も要らない
        var staleDll = Path.Combine(Path.GetDirectoryName(BuildCsproj)!, "bin", "devtool", "slangbuild.dll");
        WithRedirectedOutDirAndStaleDll(staleDll, elsewhere =>
        {
            var tool = new SlangbuildRunner.Resolved(Dotnet(), BuildCsproj);
            Assert.Equal(0, SlangbuildRunner.Run(tool, new[] { "--version" }, _pinnedDir));
            Assert.False(File.Exists(Path.Combine(elsewhere, "slangbuild.dll")), "環境変数の OutDir に組み立てた");
        });
    }

    private static IEnumerable<string> CurrentRids()
    {
        var arch = RuntimeInformation.OSArchitecture == Architecture.Arm64 ? "arm64" : "x64";
        var os = OperatingSystem.IsWindows() ? "win" : OperatingSystem.IsMacOS() ? "osx" : "linux";
        yield return $"{os}-{arch}";
        if (OperatingSystem.IsMacOS() && arch == "arm64") yield return "osx-x64";   // Rosetta で動く
    }

    private string? PublishedSlangc()
    {
        var name = OperatingSystem.IsWindows() ? "slangc.exe" : "slangc";
        return CurrentRids()
            .Select(rid => Path.Combine(_repo, "src", "SLANGCompiler.CLI", "bin", "Release", "net10.0", rid, "publish", name))
            .FirstOrDefault(File.Exists);
    }

    private string Assembler => Path.Combine(_repo, "tools", OperatingSystem.IsWindows() ? "AILZ80ASM.exe" : "AILZ80ASM");

    [SkippableFact]
    public void HuBasicFallback_BuildsRelativeInputFromPinnedFolder()
    {
        // hubasic2sl が slangbuild を呼ぶ道 (SlangbuildRunner.Run) で、相対の道の SL を組み立てる
        var slangc = PublishedSlangc();
        Skip.If(slangc == null, "publish した slangc がない");
        Skip.IfNot(File.Exists(Assembler), "AILZ80ASM がない");

        var tool = new SlangbuildRunner.Resolved(Dotnet(), BuildCsproj);
        var code = SlangbuildRunner.Run(tool, new[] { "-E", "x1native", "PROG.SL", "--slangc", slangc!, "--asm", Assembler },
            _pinnedDir, new Dictionary<string, string> { ["SLANG_HOME"] = _repo });
        Assert.Equal(0, code);
        Assert.True(File.Exists(Path.Combine(_pinnedDir, "PROG.bin")));
    }

    private static void CopyTree(string from, string to)
    {
        foreach (var f in Directory.EnumerateFiles(from, "*", SearchOption.AllDirectories))
        {
            var rel = Path.GetRelativePath(from, f);
            var first = rel.Split(Path.DirectorySeparatorChar)[0];
            if (first is "bin" or "obj") continue;
            var dst = Path.Combine(to, rel);
            Directory.CreateDirectory(Path.GetDirectoryName(dst)!);
            File.Copy(f, dst);
        }
    }

    [SkippableFact]
    public void Slangbuild_FromPinnedFolder_ReachesCheckoutFallbackAndCompilesRelativeInput()
    {
        // 本物の slangbuild の入口を、古い SDK に決めたフォルダから動かす。
        // 木の写し (CLI・Core の csproj とソース、global.json) の中に置いた slangbuild には、同梱の slangc も開発の publish も無く、
        // PATH にも無い (PATH は dotnet のフォルダだけ) ので、最後の手 (木の CLI を組み立てて動かす) に行き着く
        Skip.IfNot(File.Exists(Assembler), "AILZ80ASM がない");
        var dotnet = Dotnet();
        Skip.IfNot(Path.IsPathRooted(dotnet), "dotnet の場所が分からない");

        var fake = NewTempDir("slang_checkout_");
        File.Copy(Path.Combine(_repo, "global.json"), Path.Combine(fake, "global.json"));
        CopyTree(Path.Combine(_repo, "src", "SLANGCompiler.CLI"), Path.Combine(fake, "src", "SLANGCompiler.CLI"));
        CopyTree(Path.Combine(_repo, "src", "SLANGCompiler.Core"), Path.Combine(fake, "src", "SLANGCompiler.Core"));
        var builtSlangbuild = DevToolBuild.Build(dotnet, BuildCsproj, "slangbuild", out var log);
        Assert.True(builtSlangbuild != null, log);
        var fakeBuildBin = Path.Combine(fake, "src", "SLANGCompiler.Build", "bin", "devtool");
        CopyTree(Path.GetDirectoryName(builtSlangbuild)!, fakeBuildBin);

        var psi = new ProcessStartInfo(dotnet) { WorkingDirectory = _pinnedDir, UseShellExecute = false };
        foreach (var a in new[] { Path.Combine(fakeBuildBin, "slangbuild.dll"), "-E", "x1native", "PROG.SL", "--asm", Assembler })
            psi.ArgumentList.Add(a);
        psi.Environment["PATH"] = Path.GetDirectoryName(dotnet)!;
        psi.Environment["SLANG_HOME"] = _repo;
        var (code, stdout, stderr) = RunToEnd(psi);
        Assert.True(code == 0, stdout + stderr);
        Assert.True(File.Exists(Path.Combine(fake, "src", "SLANGCompiler.CLI", "bin", "devtool", "slangc.dll")),
            "木の写しの CLI を組み立てていない (最後の手に行き着いていない)");
        Assert.True(File.Exists(Path.Combine(_pinnedDir, "PROG.bin")), stdout + stderr);
    }
}
