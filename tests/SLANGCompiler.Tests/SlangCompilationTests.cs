using System.Text.RegularExpressions;
using SLANGCompiler.Compilation;
using SLANGCompiler.Runtime;
using Xunit;

namespace SLANGCompiler.Tests;

/// <summary>
/// slangc の手順 (SlangCompilation)。CLI とブラウザの入口が同じものを通る。
/// CLI が stderr に書いていた文 (env の error・診断・ランタイムが無い warning・features の error) は、同じ順で log に書く
/// </summary>
public class SlangCompilationTests : IDisposable
{
    private readonly string _repo = TestTools.RepoRoot();
    private readonly string _dir;

    public SlangCompilationTests()
    {
        _dir = Path.Combine(Path.GetTempPath(), "slang_compilation_" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(Path.Combine(_dir, "lib", "env"));
    }

    public void Dispose()
    {
        try { Directory.Delete(_dir, true); } catch { }
    }

    /// <summary>リポジトリの runtime/ と env を -L で見る (CLI の -L と同じ)</summary>
    private PathResolver Paths(params string[] extraLib)
        => new(new List<string>(), new List<string> { _repo }.Concat(extraLib).ToList());

    private string Source(string name, string text)
    {
        var path = Path.Combine(_dir, name);
        File.WriteAllText(path, text);
        return path;
    }

    private (SlangCompileResult Result, string Log) Compile(string env, string path, PathResolver? paths = null)
    {
        paths ??= Paths();
        using var log = new StringWriter();
        var resolution = SlangCompilation.ResolveEnvironment(env, paths, log);
        Assert.True(resolution != null, log.ToString());
        var result = SlangCompilation.CompileFile(path, resolution.Value.Config, paths, new DiagnosticBag(), log);
        return (result, log.ToString());
    }

    [Fact]
    public void ResolveEnvironment_UnknownName_WritesCliMessage()
    {
        using var log = new StringWriter();
        Assert.Null(SlangCompilation.ResolveEnvironment("nosuchenv", Paths(), log));
        Assert.Equal("Error: Unknown environment 'nosuchenv'. No 'nosuchenv.env' found in runtime/env/ or lib/env/." + Environment.NewLine,
                     log.ToString());
    }

    [Fact]
    public void ResolveEnvironment_BrokenFile_WritesCliMessage()
    {
        File.WriteAllText(Path.Combine(_dir, "lib", "env", "broken.env"), "env_type: 1\nos_type: [broken\n");
        using var log = new StringWriter();
        Assert.Null(SlangCompilation.ResolveEnvironment("broken", Paths(Path.Combine(_dir, "lib")), log));
        Assert.StartsWith("Error: Failed to load env file for 'broken': ", log.ToString());
    }

    [Fact]
    public void CompileFile_Success_ReturnsMainAsmOnly()
    {
        var (r, log) = Compile("x1", Source("OK.SL", "VAR J;\nMAIN()\nBEGIN\n  J = 1;\nEND;\n"));
        Assert.True(r.Ok, log);
        Assert.Contains("MAIN:", r.MainAsm);
        Assert.Empty(r.Overlays);
        Assert.Null(r.ModulesLines);
        Assert.Null(r.SharedSymbolsInc);
        Assert.Null(r.CSource);
        Assert.Equal("", log);
    }

    [Fact]
    public void CompileFile_Error_WritesDiagnosticsAndFails()
    {
        var path = Source("SYN.SL", "VAR J;\nMAIN()\nBEGIN\n  J = ;\nEND;\n");
        var (r, log) = Compile("x1", path);
        Assert.False(r.Ok);
        Assert.Null(r.MainAsm);
        Assert.Contains(path, log);
        Assert.Matches(@"(?i)error", log);
    }

    [Fact]
    public void CompileFile_MissingRuntimeLibrary_WarnsAndContinues()
    {
        File.WriteAllText(Path.Combine(_dir, "lib", "env", "missinglib.env"),
            "env_type: 99\nos_type: 99\nlibraries:\n  - nosuchlib.yml\n  - runtime.yml\n");
        var (r, log) = Compile("missinglib", Source("OK.SL", "VAR J;\nMAIN()\nBEGIN\n  J = 1;\nEND;\n"), Paths(Path.Combine(_dir, "lib")));
        Assert.True(r.Ok, log);
        Assert.Equal("; Warning: Runtime not found: nosuchlib.asm" + Environment.NewLine, log);
    }

    [Fact]
    public void CompileFile_MissingFeature_WritesSlangcError()
    {
        var (r, log) = Compile("zxs", Source("FEAT.SL", "VAR J;\nMAIN()\nBEGIN\n  J = SPR_SET(0,0,0,0);\nEND;\n"));
        Assert.False(r.Ok);
        Assert.Contains("slangc: error: SPR_SET needs ZX Spectrum Next (env zxs)", log);
    }

    [Fact]
    public void CompileFile_OscarC_ReturnsCSource()
    {
        var sample = Directory.GetFiles(Path.Combine(_repo, "examples", "c64"), "*.SL").OrderBy(f => f, StringComparer.Ordinal).First();
        var (r, log) = Compile("c64", sample);
        Assert.True(r.Ok, log);
        Assert.False(string.IsNullOrEmpty(r.CSource));
        Assert.Null(r.MainAsm);
    }

    [Fact]
    public void CompileFile_PagedModules_ReturnsOverlaysModulesAndInc()
    {
        var (r, log) = Compile("zxn", Path.Combine(_repo, "examples", "zxn", "MODULE.SL"));
        Assert.True(r.Ok, log);
        Assert.NotEmpty(r.Overlays);
        Assert.NotNull(r.ModulesLines);
        Assert.Equal(new[] { "0 $C000 40", "1 $C000 41", "2 $0010 30" }, r.ModulesLines);
        Assert.StartsWith("; SLANG Shared Symbols (auto-generated)", r.SharedSymbolsInc);
    }

    [Fact]
    public void SlangVersion_MatchesCliProjectVersion()
    {
        var csproj = File.ReadAllText(Path.Combine(_repo, "src", "SLANGCompiler.CLI", "SLANGCompiler.CLI.csproj"));
        Assert.Equal(Regex.Match(csproj, "<Version>([^<]+)</Version>").Groups[1].Value, SlangVersion.Version);
    }
}
