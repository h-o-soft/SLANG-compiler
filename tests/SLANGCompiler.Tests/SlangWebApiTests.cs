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
        // #IF の -2147483648 / -1 は、+ - * と同じく回り込んで -2147483648 になる (例外で止まらない)。
        // 違う値なら K が無くなり、MAIN の K = 1 が error になる
        var path = Source("IF.SL", "#IF ($80000000 / -1) == $80000000\nVAR K;\n#ENDIF\nMAIN()\nBEGIN\n  K = 1;\nEND;\n");
        var r = Parse(SlangWebApi.Compile(path, Options("x1")));
        Assert.True(r.GetProperty("ok").GetBoolean(), r.GetRawText());
    }

    [Theory]
    [InlineData("- ", "")]          // 空白で区切る (続けると -- の字句になる)
    [InlineData("(", ")")]
    public void Compile_DeeplyNestedIfExpression_ReturnsErrorAndNextCompileWorks(string open, string close)
    {
        // 深すぎる #IF の式は、スタックを使い果たす前に error にする。そのあとも同じ口でコンパイルできる
        var deep = string.Concat(Enumerable.Repeat(open, 10000)) + "1" + string.Concat(Enumerable.Repeat(close, 10000));
        var path = Source("DEEP.SL", $"#IF {deep}\nVAR K;\n#ENDIF\nVAR J;\nMAIN()\nBEGIN\n  J = 1;\nEND;\n");
        var r = Parse(SlangWebApi.Compile(path, Options("x1")));
        Assert.False(r.GetProperty("ok").GetBoolean());
        Assert.Contains(Lines(r, "log"), l => l.Contains("#IF expression is nested too deeply"));

        var ok = Parse(SlangWebApi.Compile(Source("OK.SL", "VAR J;\nMAIN()\nBEGIN\n  J = 1;\nEND;\n"), Options("x1")));
        Assert.True(ok.GetProperty("ok").GetBoolean(), ok.GetRawText());
    }

    private static string Repeat(string s, int n) => string.Concat(Enumerable.Repeat(s, n));

    public static TheoryData<string, string> DeepSources => new()
    {
        // 式の括弧・単項の演算子 (空白で区切る。続けると -- の字句になる)・文の入れ子・#IF の入れ子
        { "parens", "VAR J;\nMAIN()\nBEGIN\n  J = " + Repeat("(", 10000) + "1" + Repeat(")", 10000) + ";\nEND;\n" },
        { "unary", "VAR J;\nMAIN()\nBEGIN\n  J = " + Repeat("- ", 10000) + "1;\nEND;\n" },
        { "blocks", "VAR J;\nMAIN()\nBEGIN\n" + Repeat("{\n", 10000) + "J = 1;\n" + Repeat("}\n", 10000) + "END;\n" },
        { "ifblocks", Repeat("#IF 1\n", 10000) + "VAR J;\n" + Repeat("#ENDIF\n", 10000) + "MAIN()\nBEGIN\n  J = 1;\nEND;\n" },
    };

    [Theory]
    [MemberData(nameof(DeepSources))]
    public void Compile_DeeplyNestedSource_ReturnsErrorAndNextCompileWorks(string kind, string text)
    {
        // 深すぎる入れ子は、スタックを使い果たす前に source の error にする。そのあとも同じ口でコンパイルできる
        var r = Parse(SlangWebApi.Compile(Source($"DEEP_{kind}.SL", text), Options("x1")));
        Assert.False(r.GetProperty("ok").GetBoolean());
        Assert.Contains(Lines(r, "log"), l => l.Contains("nested too deeply"));

        var ok = Parse(SlangWebApi.Compile(Source("OK.SL", "VAR J;\nMAIN()\nBEGIN\n  J = 1;\nEND;\n"), Options("x1")));
        Assert.True(ok.GetProperty("ok").GetBoolean(), ok.GetRawText());
    }

    [Theory]
    [InlineData("parens")]
    [InlineData("blocks")]
    public void Compile_NestingBelowLimit_StillCompiles(string kind)
    {
        // 上限より少し浅い入れ子は、後の段 (意味・IR・コード生成) も含めてふつうに通る
        var text = kind == "parens"
            ? "VAR J;\nMAIN()\nBEGIN\n  J = " + Repeat("(", 200) + "1" + Repeat(")", 200) + ";\nEND;\n"
            : "VAR J;\nMAIN()\nBEGIN\n" + Repeat("{\n", 200) + "J = 1;\n" + Repeat("}\n", 200) + "END;\n";
        var r = Parse(SlangWebApi.Compile(Source($"NEST_{kind}.SL", text), Options("x1")));
        Assert.True(r.GetProperty("ok").GetBoolean(), r.GetRawText());
    }

    [Fact]
    public void Compile_DeepConstInitializer_ReportsSourceError()
    {
        // CONST の右辺は前処理でも読む (#IF に使う値)。入れ子が深すぎても例外で止まらず、構文の error がその行を指す
        var path = Source("CONSTDEEP.SL", "VAR J;\nCONST A = " + Repeat("(", 10000) + "1" + Repeat(")", 10000) + ";\nMAIN()\nBEGIN\n  J = A;\nEND;\n");
        var r = Parse(SlangWebApi.Compile(path, Options("x1")));
        Assert.False(r.GetProperty("ok").GetBoolean());
        var line = Lines(r, "log").First(l => l.Contains("nested too deeply"));
        Assert.StartsWith(path + ":2:", line);
        Assert.DoesNotContain(Lines(r, "log"), l => l.StartsWith("slangc: error:"));
    }

    [Fact]
    public void Compile_DeepIfExpression_ErrorPointsAtDirective()
    {
        // #IF の式の入れ子の error は、#IF を書いたファイルと行を指す (式を字句にし直した仮の名前ではない)
        var path = Source("IFDEEP.SL", "VAR J;\n#IF " + Repeat("(", 1000) + "1" + Repeat(")", 1000) + "\nVAR K;\n#ENDIF\nMAIN()\nBEGIN\n  J = 1;\nEND;\n");
        var r = Parse(SlangWebApi.Compile(path, Options("x1")));
        var line = Lines(r, "log").First(l => l.Contains("#IF expression is nested too deeply"));
        Assert.StartsWith(path + ":2:", line);
    }

    [Fact]
    public void Version_ReportsSlangcVersion()
    {
        var v = Parse(SlangWebApi.Version());
        Assert.Equal(SlangVersion.Version, v.GetProperty("slangc").GetString());
        Assert.StartsWith(SlangVersion.Version, v.GetProperty("informational").GetString());
    }
}
