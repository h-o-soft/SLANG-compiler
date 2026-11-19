using System.Text.Json;
using SLANGCompiler.Compilation;
using SLANGCompiler.Runtime;
using SLANGCompiler.Web;
using Xunit;

namespace SLANGCompiler.Tests;

/// <summary>
/// ブラウザから呼ぶ slangc の口 (SlangWebApi)。wasm ではなく同じコードを直接呼ぶ。
/// 結果の ASM・overlay・modules・inc・C は CLI と同じ SlangCompilation のもの、log は CLI の stderr の行
/// (`; Environment:`・`; Output:` の行は CLI だけのもので入らない)
/// </summary>
public class SlangWebApiTests : IDisposable
{
    private readonly string _repo = TestTools.RepoRoot();
    private readonly string _dir;

    public SlangWebApiTests()
    {
        _dir = Path.Combine(Path.GetTempPath(), "slang_webapi_" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(_dir);
    }

    public void Dispose()
    {
        try { Directory.Delete(_dir, true); } catch { }
    }

    private string Source(string name, string text)
    {
        var path = Path.Combine(_dir, name);
        File.WriteAllText(path, text);
        return path;
    }

    private string Options(string env) => JsonSerializer.Serialize(new { env, libPaths = new[] { _repo } });

    private static JsonElement Parse(string json) => JsonDocument.Parse(json).RootElement.Clone();

    private static string[] Lines(JsonElement e, string name) => e.GetProperty(name).EnumerateArray().Select(x => x.GetString()!).ToArray();

    /// <summary>CLI と同じ手順 (env の解決 → CompileFile → 成功なら残りの診断も書く) で、期待の結果と log の行を作る</summary>
    private (SlangCompileResult? Result, string[] Log) Expected(string env, string path)
    {
        var paths = new PathResolver(new List<string>(), new List<string> { _repo });
        using var log = new StringWriter();
        SlangCompileResult? result = null;
        var resolution = SlangCompilation.ResolveEnvironment(env, paths, log);
        if (resolution != null)
        {
            var diagnostics = new DiagnosticBag();
            result = SlangCompilation.CompileFile(path, resolution.Value.Config, paths, diagnostics, log);
            if (result.Ok && diagnostics.Diagnostics.Count > 0) diagnostics.WriteTo(log);
        }
        return (result, SlangWebApi.Lines(log.ToString()).ToArray());
    }

    [Fact]
    public void Compile_Success_MatchesCompilation()
    {
        var path = Source("OK.SL", "VAR J;\nMAIN()\nBEGIN\n  J = STICK(0);\nEND;\n");
        var r = Parse(SlangWebApi.Compile(path, Options("x1")));
        var (expected, log) = Expected("x1", path);
        Assert.True(r.GetProperty("ok").GetBoolean());
        Assert.Equal(expected!.MainAsm, r.GetProperty("asm").GetString());
        Assert.Empty(r.GetProperty("overlays").EnumerateArray());
        Assert.Equal(JsonValueKind.Null, r.GetProperty("modules").ValueKind);
        Assert.Equal(JsonValueKind.Null, r.GetProperty("inc").ValueKind);
        Assert.Equal(JsonValueKind.Null, r.GetProperty("c").ValueKind);
        Assert.Equal(log, Lines(r, "log"));
        Assert.Empty(Lines(r, "log"));
    }

    [Fact]
    public void Compile_WarningAfterSuccess_IsInLogLikeCli()
    {
        // 成功しても残る warning (#CHAIN の後ろの字句) は、CLI と同じく最後に log に入る
        Source("NEXT", "// 続きのソース\n");
        var path = Source("CHAIN.SL", "VAR J;\nMAIN()\nBEGIN\n  J = 1;\nEND;\n#CHAIN \"NEXT\"\nJUNK\n");
        var r = Parse(SlangWebApi.Compile(path, Options("x1")));
        var (_, log) = Expected("x1", path);
        Assert.True(r.GetProperty("ok").GetBoolean());
        Assert.Contains(Lines(r, "log"), l => l.Contains("Text after #CHAIN is not read"));
        Assert.Equal(log, Lines(r, "log"));
    }

    [Fact]
    public void Compile_Error_ReturnsDiagnosticsInLog()
    {
        var path = Source("SYN.SL", "VAR J;\nMAIN()\nBEGIN\n  J = ;\nEND;\n");
        var r = Parse(SlangWebApi.Compile(path, Options("x1")));
        var (_, log) = Expected("x1", path);
        Assert.False(r.GetProperty("ok").GetBoolean());
        Assert.Equal(JsonValueKind.Null, r.GetProperty("asm").ValueKind);
        Assert.NotEmpty(log);
        Assert.Equal(log, Lines(r, "log"));
    }

    [Fact]
    public void Compile_UnknownEnvAndMissingFile_WriteCliMessages()
    {
        var path = Source("OK.SL", "VAR J;\nMAIN()\nBEGIN\n  J = 1;\nEND;\n");
        var r = Parse(SlangWebApi.Compile(path, Options("nosuchenv")));
        Assert.False(r.GetProperty("ok").GetBoolean());
        Assert.Equal(new[] { "Error: Unknown environment 'nosuchenv'. No 'nosuchenv.env' found in runtime/env/ or lib/env/." }, Lines(r, "log"));

        var missing = Path.Combine(_dir, "NONE.SL");
        r = Parse(SlangWebApi.Compile(missing, Options("x1")));
        Assert.False(r.GetProperty("ok").GetBoolean());
        Assert.Equal(new[] { $"Error: File not found: {missing}" }, Lines(r, "log"));
    }

    [Fact]
    public void Compile_OscarC_ReturnsC()
    {
        var sample = Directory.GetFiles(Path.Combine(_repo, "examples", "c64"), "*.SL").OrderBy(f => f, StringComparer.Ordinal).First();
        var r = Parse(SlangWebApi.Compile(sample, Options("c64")));
        var (expected, _) = Expected("c64", sample);
        Assert.True(r.GetProperty("ok").GetBoolean());
        Assert.Equal(expected!.CSource, r.GetProperty("c").GetString());
        Assert.Equal(JsonValueKind.Null, r.GetProperty("asm").ValueKind);
    }

    [Fact]
    public void Compile_PagedModules_ReturnsOverlaysModulesAndInc()
    {
        var path = Path.Combine(_repo, "examples", "zxn", "MODULE.SL");
        var r = Parse(SlangWebApi.Compile(path, Options("zxn")));
        var (expected, _) = Expected("zxn", path);
        Assert.True(r.GetProperty("ok").GetBoolean());
        var overlays = r.GetProperty("overlays").EnumerateArray()
            .Select(o => (o.GetProperty("name").GetString()!, o.GetProperty("asm").GetString()!)).ToList();
        Assert.Equal(expected!.Overlays, overlays);
        Assert.Equal(new[] { "0 $C000 40", "1 $C000 41", "2 $0010 30" }, Lines(r, "modules"));
        Assert.Equal(expected.SharedSymbolsInc, r.GetProperty("inc").GetString());
    }

    [Theory]
    [InlineData("")]
    [InlineData("[]")]
    [InlineData("{")]
    [InlineData("{\"libPaths\":[]}")]
    [InlineData("{\"env\":\"\"}")]
    [InlineData("{\"env\":\"x1\",\"includePaths\":\"/a\"}")]
    [InlineData("{\"env\":\"x1\",\"libPaths\":[1]}")]
    [InlineData("{\"env\":\"x1\",\"output\":\"a\"}")]
    [InlineData("{\"env\":\"\\ud800\"}")]                         // 対になっていないサロゲート (値)
    [InlineData("{\"\\ud800\":\"x1\"}")]                          // 対になっていないサロゲート (項目の名前)
    [InlineData("{\"env\":\"x1\",\"libPaths\":[\"\\udc00\"]}")]
    public void Compile_BadOptions_ReturnsError(string options)
    {
        var r = Parse(SlangWebApi.Compile(Path.Combine(_dir, "OK.SL"), options));
        Assert.False(r.GetProperty("ok").GetBoolean());
        Assert.StartsWith("slangc: ", r.GetProperty("error").GetString());
    }

    [Fact]
    public void Compile_UnreadableRuntime_ReturnsLogInsteadOfThrowing()
    {
        // 書き方の違うランタイム (@resident の値が無い言葉) は、CLI では例外で止まる。
        // ブラウザの口は例外を JS に投げず、それまでの log に理由を足して ok:false を返す
        var lib = Path.Combine(_dir, "lib");
        Directory.CreateDirectory(Path.Combine(lib, "env"));
        File.WriteAllText(Path.Combine(lib, "env", "badlib.env"), "env_type: 99\nos_type: 99\nlibraries:\n  - runtime.yml\n  - bad.yml\n");
        File.WriteAllText(Path.Combine(lib, "bad.asm"), "; @name FOO\n; @resident nonsense\nRET\n");
        var path = Source("OK.SL", "VAR J;\nMAIN()\nBEGIN\n  J = 1;\nEND;\n");
        var r = Parse(SlangWebApi.Compile(path, JsonSerializer.Serialize(new { env = "badlib", libPaths = new[] { lib, _repo } })));
        Assert.False(r.GetProperty("ok").GetBoolean());
        Assert.Equal(JsonValueKind.Null, r.GetProperty("asm").ValueKind);
        Assert.StartsWith("slangc: error: ", Lines(r, "log").Last());
    }

    [Fact]
    public void Compile_LiteralLoneSurrogateInOptions_ReturnsError()
    {
        // JS の文字列は UTF-16 のまま渡るので、対になっていないサロゲートがそのまま入ることがある (UTF-8 にできない)
        foreach (var options in new[] { "{\"env\":\"\ud800\"}", "{\"\udc00\":\"x1\"}" })
        {
            var r = Parse(SlangWebApi.Compile(Path.Combine(_dir, "OK.SL"), options));
            Assert.False(r.GetProperty("ok").GetBoolean());
            Assert.StartsWith("slangc: ", r.GetProperty("error").GetString());
        }
    }

    [Fact]
    public void Compile_EmptyLibraryEntryInEnv_ReturnsEnvError()
    {
        var lib = Path.Combine(_dir, "lib");
        Directory.CreateDirectory(Path.Combine(lib, "env"));
        File.WriteAllText(Path.Combine(lib, "env", "nulllib.env"), "env_type: 99\nos_type: 99\nlibraries: [null]\n");
        var path = Source("OK.SL", "VAR J;\nMAIN()\nBEGIN\n  J = 1;\nEND;\n");
        var r = Parse(SlangWebApi.Compile(path, JsonSerializer.Serialize(new { env = "nulllib", libPaths = new[] { lib, _repo } })));
        Assert.False(r.GetProperty("ok").GetBoolean());
        Assert.StartsWith("Error: Failed to load env file for 'nulllib': `libraries` has an empty entry", Lines(r, "log").Single());
    }

    [Fact]
    public void Compile_PreprocessorDivisionOverflow_DoesNotThrow()
    {
        // #IF の -2147483648 / -1 は、+ - * と同じく回り込む (例外で止まらない)
        var path = Source("IF.SL", "#IF $80000000 / -1\nVAR K;\n#ENDIF\nVAR J;\nMAIN()\nBEGIN\n  J = 1;\nEND;\n");
        var r = Parse(SlangWebApi.Compile(path, Options("x1")));
        Assert.True(r.GetProperty("ok").GetBoolean(), r.GetRawText());
    }

    [Fact]
    public void Version_ReportsSlangcVersion()
    {
        var v = Parse(SlangWebApi.Version());
        Assert.Equal(SlangVersion.Version, v.GetProperty("slangc").GetString());
        Assert.StartsWith(SlangVersion.Version, v.GetProperty("informational").GetString());
    }
}
