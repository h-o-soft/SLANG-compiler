using System.Diagnostics;
using SLANGCompiler.Build;
using SLANGCompiler.HuBasic.CLI;
using Xunit;
using SkippableFactAttribute = Xunit.SkippableFactAttribute;

namespace SLANGCompiler.Tests;

/// <summary>
/// 開発の木の道具を組み立てる試験は、環境変数 OutDir を一時的に変えるので、ほかの試験と並べずに 1 つずつ動かす
/// </summary>
[CollectionDefinition("DevToolBuild", DisableParallelization = true)]
public class DevToolBuildCollection { }

/// <summary>
/// 開発の木の道具の最後の手 (slangbuild の slangc、hubasic2sl の slangbuild) は、
/// 呼んだ人のフォルダの global.json が別の SDK に決めていても動き、呼んだ人のフォルダからの相対の道を読む
/// (組み立ては木の SDK の決まりで行い、動かすのは呼んだ人のフォルダのまま)。
/// 組み立てた物の置き場は OutDir をコマンド行で決めるので、環境変数の OutDir や前の残りの dll に左右されず、
/// 木の道に , や ; があっても組み立てられる。
/// 試験は、無い SDK の版に決めたフォルダから呼び (`dotnet run --project` なら SDK が見つからずに止まる所)、
/// 組み立てるのは一時の木の写し (名前に , と ; を入れる) なので、開発の木の組み立てた物には触らない
/// </summary>
[Collection("DevToolBuild")]
public class DevToolFallbackTests : IDisposable
{
    private const string Source = "VAR J;\nMAIN()\nBEGIN\n  J = 1;\nEND;\n";
    private const string Basic = "10 PRINT \"HI\"\n20 END\n";

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
        File.WriteAllText(Path.Combine(_pinnedDir, "PROG.BAS"), Basic);
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

    /// <summary>
    /// 一時の木の写し: global.json と、指定したプロジェクトのフォルダ (bin・obj を除く)。名前に , と ; を入れる
    /// (MSBuild のプロパティの区切りの字。道に入っていても組み立てられることを見る)
    /// </summary>
    private string MakeCheckout(params string[] projects)
    {
        var root = NewTempDir("slang,check;out_");
        File.Copy(Path.Combine(_repo, "global.json"), Path.Combine(root, "global.json"));
        foreach (var p in projects)
            CopyTree(Path.Combine(_repo, "src", p), Path.Combine(root, "src", p));
        return root;
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

    private static string Csproj(string checkout, string project)
        => Path.Combine(checkout, "src", project, project + ".csproj");

    private static string DevtoolDir(string checkout, string project)
        => Path.Combine(checkout, "src", project, "bin", "devtool");

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

    /// <summary>
    /// MSBuild の出力先を変える環境変数を 1 つ決め、組み立ての置き場 (bin/devtool) に壊れた dll を置いてから body を動かす (終わったら戻す)。
    /// body には、別の出力先にした一時のフォルダを渡す (OutDir のとき。使わなければそのまま)
    /// </summary>
    private void WithMsBuildEnvAndStaleDll(string staleDll, string variable, string? value, Action<string> body)
    {
        var elsewhere = NewTempDir("slang_outdir_");
        Directory.CreateDirectory(Path.GetDirectoryName(staleDll)!);
        File.WriteAllBytes(staleDll, new byte[] { 0x53, 0x54, 0x41, 0x4C, 0x45 });   // 古い残り (dll として読めない)
        var old = Environment.GetEnvironmentVariable(variable);
        Environment.SetEnvironmentVariable(variable, value ?? elsewhere + Path.DirectorySeparatorChar);
        try { body(elsewhere); }
        finally { Environment.SetEnvironmentVariable(variable, old); }
    }

    /// <summary>
    /// 子のプロセスの PATH にする一時のフォルダ: dotnet へのリンクだけを置く (dotnet と同じフォルダに入れた slangc などを拾わない)。
    /// リンクを作れなければ null
    /// </summary>
    private string? IsolatedPath(string dotnet)
    {
        var dir = NewTempDir("slang_path_");
        try
        {
            File.CreateSymbolicLink(Path.Combine(dir, Path.GetFileName(dotnet)), dotnet);
            return dir;
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException or PlatformNotSupportedException)
        {
            return null;
        }
    }

    private string Assembler => Path.Combine(_repo, "tools", OperatingSystem.IsWindows() ? "AILZ80ASM.exe" : "AILZ80ASM");

    [Fact]
    public void MsBuildEscape_EscapesPropertySeparators()
    {
        Assert.Equal("/a%2Cb%3Bc%25d/", DevToolBuild.MsBuildEscape("/a,b;c%d/"));
        Assert.Equal("/a%2Cb%3Bc%25d/", SlangbuildRunner.MsBuildEscape("/a,b;c%d/"));
    }

    [Fact]
    public void PinnedFolder_CannotUseDotnetRun()
    {
        // 前提の確かめ: このフォルダからの dotnet run --project は、SDK が見つからずに止まる
        var psi = new ProcessStartInfo(Dotnet()) { WorkingDirectory = _pinnedDir, UseShellExecute = false };
        foreach (var a in new[] { "run", "--project", Csproj(_repo, "SLANGCompiler.CLI"), "-c", "Release", "--", "--version" })
            psi.ArgumentList.Add(a);
        Assert.NotEqual(0, RunToEnd(psi).Code);
    }

    [Theory]
    [InlineData("OutDir", null, "")]                                                    // 出力先を別の所に向ける
    [InlineData("GenerateProjectSpecificOutputFolder", "true", "SLANGCompiler.CLI")]   // 出力先の下にプロジェクトの名前のフォルダを作る
    public void SlangcFallback_LaunchesBuiltDllDespiteMsBuildOutputSettings(string variable, string? value, string subdir)
    {
        var checkout = MakeCheckout("SLANGCompiler.CLI", "SLANGCompiler.Core");
        var staleDll = Path.Combine(DevtoolDir(checkout, "SLANGCompiler.CLI"), "slangc.dll");
        WithMsBuildEnvAndStaleDll(staleDll, variable, value, elsewhere =>
        {
            // slangbuild が slangc を動かす道 (Driver.SpawnSlangc も DevToolBuild.CreateStartInfo で作る)
            var tool = new ResolvedTool(Dotnet(), ResolutionKind.DotnetRun, Csproj(checkout, "SLANGCompiler.CLI"));
            var psi = DevToolBuild.CreateStartInfo(tool, "slangc", new[] { "-E", "x1native", "-o", "OUT.ASM", "PROG.SL" }, out var log);
            Assert.True(psi != null, log);
            Assert.Equal(Path.Combine(DevtoolDir(checkout, "SLANGCompiler.CLI"), subdir, "slangc.dll"), psi!.ArgumentList[0]);
            Assert.False(File.Exists(Path.Combine(elsewhere, "slangc.dll")), "環境変数の OutDir に組み立てた");
            psi.WorkingDirectory = _pinnedDir;
            psi.Environment["SLANG_HOME"] = _repo;   // ランタイムと env はこの木のもの
            var (code, _, stderr) = RunToEnd(psi);
            Assert.True(code == 0, stderr);
            Assert.True(File.Exists(Path.Combine(_pinnedDir, "OUT.ASM")), stderr);
        });
    }

    [Theory]
    [InlineData("OutDir", null)]
    [InlineData("GenerateProjectSpecificOutputFolder", "true")]
    public void HuBasicFallback_LaunchesBuiltDllDespiteMsBuildOutputSettings(string variable, string? value)
    {
        // hubasic2sl が slangbuild を動かす道 (SlangbuildRunner.Run)。AILZ80ASM も slangc も要らない。
        // 置き場の壊れた dll を動かせば止まる
        var checkout = MakeCheckout("SLANGCompiler.Build", "SLANGCompiler.Core", "SLANGCompiler.SlfsPack");
        var staleDll = Path.Combine(DevtoolDir(checkout, "SLANGCompiler.Build"), "slangbuild.dll");
        WithMsBuildEnvAndStaleDll(staleDll, variable, value, elsewhere =>
        {
            var tool = new SlangbuildRunner.Resolved(Dotnet(), Csproj(checkout, "SLANGCompiler.Build"));
            Assert.Equal(0, SlangbuildRunner.Run(tool, new[] { "--version" }, _pinnedDir));
            Assert.False(File.Exists(Path.Combine(elsewhere, "slangbuild.dll")), "環境変数の OutDir に組み立てた");
        });
    }

    /// <summary>道具を一時の木の写しの bin/devtool に組み立てる (試験の下ごしらえ。本番と同じ DevToolBuild を使う)</summary>
    private static string BuildInto(string checkout, string project, string assembly)
    {
        var dll = DevToolBuild.Build(Dotnet(), Csproj(checkout, project), assembly, out var log);
        Assert.True(dll != null, log);
        return dll!;
    }

    [SkippableFact]
    public void Slangbuild_FromPinnedFolder_ReachesCheckoutFallbackAndCompilesRelativeInput()
    {
        // 本物の slangbuild の入口を、古い SDK に決めたフォルダから動かす。
        // 木の写しの中の slangbuild には、同梱の slangc も開発の publish も無く、PATH にも無い (PATH は dotnet へのリンクだけを置いたフォルダ) ので、
        // 最後の手 (木の CLI を組み立てて動かす) に行き着く
        Skip.IfNot(File.Exists(Assembler), "AILZ80ASM がない");
        var dotnet = Dotnet();
        Skip.IfNot(Path.IsPathRooted(dotnet), "dotnet の場所が分からない");

        var checkout = MakeCheckout("SLANGCompiler.CLI", "SLANGCompiler.Core", "SLANGCompiler.Build", "SLANGCompiler.SlfsPack");
        var slangbuild = BuildInto(checkout, "SLANGCompiler.Build", "slangbuild");
        var pathDir = IsolatedPath(dotnet);
        Skip.If(pathDir == null, "dotnet へのリンクを作れない");

        var psi = new ProcessStartInfo(dotnet) { WorkingDirectory = _pinnedDir, UseShellExecute = false };
        foreach (var a in new[] { slangbuild, "-E", "x1native", "PROG.SL", "--asm", Assembler })
            psi.ArgumentList.Add(a);
        psi.Environment["PATH"] = pathDir!;
        psi.Environment["SLANG_HOME"] = _repo;
        var (code, stdout, stderr) = RunToEnd(psi);
        Assert.True(code == 0, stdout + stderr);
        Assert.True(File.Exists(Path.Combine(DevtoolDir(checkout, "SLANGCompiler.CLI"), "slangc.dll")),
            "木の写しの CLI を組み立てていない (最後の手に行き着いていない)");
        Assert.True(File.Exists(Path.Combine(_pinnedDir, "PROG.bin")), stdout + stderr);
    }

    [SkippableFact]
    public void HuBasic_FromPinnedFolder_ReachesCheckoutFallbacksAndBuildsDisk()
    {
        // 本物の hubasic2sl --emit の入口を、古い SDK に決めたフォルダから動かす。
        // 木の写しの中の hubasic2sl には、同梱の slangbuild も開発の publish も無く、PATH にも無い (PATH は dotnet へのリンクだけを置いたフォルダ) ので、最後の手 (木の Build を組み立てて動かす) に行き着く。
        // その slangbuild も同じ理由で、木の写しの CLI を組み立てて slangc にする
        Skip.IfNot(File.Exists(Assembler), "AILZ80ASM がない");
        var dotnet = Dotnet();
        Skip.IfNot(Path.IsPathRooted(dotnet), "dotnet の場所が分からない");

        var checkout = MakeCheckout("SLANGCompiler.HuBasic", "SLANGCompiler.CLI", "SLANGCompiler.Core",
                                    "SLANGCompiler.Build", "SLANGCompiler.SlfsPack");
        var hubasic = BuildInto(checkout, "SLANGCompiler.HuBasic", "hubasic2sl");
        var pathDir = IsolatedPath(dotnet);
        Skip.If(pathDir == null, "dotnet へのリンクを作れない");

        var psi = new ProcessStartInfo(dotnet) { WorkingDirectory = _pinnedDir, UseShellExecute = false };
        foreach (var a in new[] { hubasic, "PROG.BAS", "--emit", "disk" })
            psi.ArgumentList.Add(a);
        psi.Environment["PATH"] = pathDir!;
        psi.Environment["SLANG_HOME"] = _repo;
        psi.Environment["AILZ80ASM_PATH"] = Assembler;
        var (code, stdout, stderr) = RunToEnd(psi);
        Assert.True(code == 0, stdout + stderr);
        Assert.True(File.Exists(Path.Combine(DevtoolDir(checkout, "SLANGCompiler.Build"), "slangbuild.dll")),
            "木の写しの Build を組み立てていない (最後の手に行き着いていない)");
        Assert.True(File.Exists(Path.Combine(DevtoolDir(checkout, "SLANGCompiler.CLI"), "slangc.dll")),
            "木の写しの CLI を組み立てていない");
        Assert.True(File.Exists(Path.Combine(_pinnedDir, "PROG.d88")), stdout + stderr);
    }
}
