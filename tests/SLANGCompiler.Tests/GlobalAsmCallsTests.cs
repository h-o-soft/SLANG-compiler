using System.Text.RegularExpressions;
using SLANGCompiler.CodeGen;
using SLANGCompiler.Compilation;
using SLANGCompiler.IR;
using SLANGCompiler.Lexer;
using SLANGCompiler.Parser;
using SLANGCompiler.Runtime;
using SLANGCompiler.Semantics;
using Xunit;

namespace SLANGCompiler.Tests;

/// <summary>
/// トップレベルの #ASM の `; @calls 名前` の行で、ランタイムの関数を求める
/// </summary>
public class GlobalAsmCallsTests : IDisposable
{
    // 試験用の小さなランタイム (A は B を、B は ZXN_MODULE を @calls で使う)
    private const string Lib = @"; @name MULHLDE
LD HL,1
RET

; @name MODHLDE
LD HL,2
RET

; @name A
; @calls B
CALL B
RET

; @name B
; @calls ZXN_MODULE
CALL ZXN_MODULE
RET

; @name ZXN_MODULE
LD HL,3
RET
";

    private readonly string _repo = TestTools.RepoRoot();
    private readonly string _dir;

    public GlobalAsmCallsTests()
    {
        _dir = Path.Combine(Path.GetTempPath(), "slang_asmcalls_" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(_dir);
    }

    public void Dispose()
    {
        try { Directory.Delete(_dir, true); } catch { }
    }

    /// <summary>試験用のランタイムで組む。direct なら GenerateAll を通さず Generate だけを呼ぶ (計画の無い道)</summary>
    private static (string Asm, DiagnosticBag Diag) Build(string source, bool direct = false)
    {
        var diag = new DiagnosticBag();
        var tokens = new Lexer.Lexer(source).Tokenize();
        tokens = new Preprocessor(diag).Process(tokens, ".");
        var ast = new Parser.Parser(tokens, diag).ParseCompilationUnit();
        var analyzer = new SemanticAnalyzer(diag);
        analyzer.Analyze(ast);
        var irModule = new IrGenerator(diag, analyzer.Symbols).Generate(ast);
        Assert.False(diag.HasErrors, string.Join("; ", diag.Diagnostics));

        var rm = new RuntimeManager();
        rm.LoadFromString(Lib);
        var codeGen = new CodeGenerator(irModule, rm, null, diag);
        var asm = direct ? codeGen.Generate() : codeGen.GenerateAll().MainAsm;
        return (asm, diag);
    }

    private static int Defs(string asm, string label) => Regex.Matches(asm, $@"(?m)^{label}:").Count;

    private static List<string> Warnings(DiagnosticBag diag)
        => diag.Diagnostics.Where(d => d.Severity == DiagnosticSeverity.Warning).Select(d => d.Message).ToList();

    private const string EmptyMain = "MAIN()\nBEGIN\nEND;\n";

    [Theory]
    [InlineData("; @calls MULHLDE")]
    [InlineData("  ;  @calls   MULHLDE  ")]
    [InlineData("; @CALLS mulhlde")]
    [InlineData("\t;@calls MULHLDE")]
    public void TopLevelLine_BringsRuntime(string line)
    {
        var (asm, diag) = Build($"#ASM\n{line}\n#END\n" + EmptyMain);
        Assert.Equal(1, Defs(asm, "MULHLDE"));
        Assert.Empty(Warnings(diag));
    }

    [Fact]
    public void TopLevelLine_SeveralNames()
    {
        var (asm, diag) = Build("#ASM\n;@calls MULHLDE, MODHLDE,MULHLDE\n#END\n" + EmptyMain);
        Assert.Equal(1, Defs(asm, "MULHLDE"));
        Assert.Equal(1, Defs(asm, "MODHLDE"));
        Assert.Empty(Warnings(diag));
    }

    [Fact]
    public void SameNameInTwoBlocks_OneDefinition()
    {
        var (asm, _) = Build("#ASM\n; @calls MULHLDE\n#END\n#ASM\n; @calls MULHLDE\n#END\n" + EmptyMain);
        Assert.Equal(1, Defs(asm, "MULHLDE"));
    }

    [Fact]
    public void WithoutLine_NoRuntime()
    {
        var (asm, _) = Build("#ASM\n; ただの注釈\n#END\n" + EmptyMain);
        Assert.Equal(0, Defs(asm, "MULHLDE"));
    }

    [Theory]
    [InlineData("\tld a,b ; @calls MULHLDE")]   // 命令の後ろの注釈
    [InlineData("; @calls")]                   // 名前が無い
    [InlineData("; x @calls MULHLDE")]         // ; の後が @calls でない
    [InlineData("; @callsMULHLDE")]            // @calls の後に空白が無い
    public void OtherComments_NotRead(string line)
    {
        var (asm, diag) = Build($"#ASM\n{line}\n#END\n" + EmptyMain);
        Assert.Equal(0, Defs(asm, "MULHLDE"));
        Assert.Empty(Warnings(diag));
    }

    [Fact]
    public void FunctionAsm_NotRead()
    {
        var (asm, diag) = Build("MAIN()\nBEGIN\n#ASM\n; @calls MULHLDE\n#END\nEND;\n");
        Assert.Equal(0, Defs(asm, "MULHLDE"));
        Assert.Empty(Warnings(diag));
    }

    [Fact]
    public void ExcludedBySlangIf_NotRead()
    {
        var (asm, _) = Build("CONST USEIT = 0;\n#IF (USEIT == 1)\n#ASM\n; @calls MULHLDE\n#END\n#ENDIF\n" + EmptyMain);
        Assert.Equal(0, Defs(asm, "MULHLDE"));
    }

    [Theory]
    [InlineData("NOSUCH")]
    [InlineData("M8ALIB.M8ALOAD")]
    [InlineData("NAME_SPACE_DEFAULT.MULHLDE")]
    public void NotRuntimeName_WarnsAndIgnored(string name)
    {
        var (asm, diag) = Build($"#ASM\n; @calls {name}\n#END\n" + EmptyMain);
        Assert.Equal(0, Defs(asm, "MULHLDE"));
        Assert.Equal(new[] { $"; @calls {name} is ignored: not a runtime function" }, Warnings(diag));
        Assert.False(diag.HasErrors);
    }

    [Fact]
    public void Warning_PointsAtAsmBlock()
    {
        var (_, diag) = Build("VAR X;\n\n#ASM\n; @calls NOSUCH\n#END\n" + EmptyMain);
        var w = Assert.Single(diag.Diagnostics, d => d.Severity == DiagnosticSeverity.Warning);
        Assert.Equal(3, w.Span.Start.Line);
    }

    [Fact]
    public void UserFunctionSameName_WarnsAndOneDefinition()
    {
        // (利用者の関数を呼ぶと、ランタイムの同じ名前のものも入るのは前から。ここでは呼ばずに `; @calls` だけを見る)
        var (asm, diag) = Build("#ASM\n; @calls MULHLDE\n#END\nMULHLDE(X, Y)\nBEGIN\n  RETURN X;\nEND;\n" + EmptyMain);
        Assert.Equal(1, Defs(asm, "MULHLDE"));
        Assert.Equal(new[] { "; @calls MULHLDE is ignored: MULHLDE is a user function" }, Warnings(diag));
    }

    [Theory]
    [InlineData("ZXN_MODULE")]
    [InlineData("A")]   // A → B → ZXN_MODULE と @calls でたどって届く
    [InlineData("B")]
    public void ReachesZxnModule_WarnsAndIgnored(string name)
    {
        var (asm, diag) = Build($"#ASM\n; @calls {name}\n#END\n" + EmptyMain);
        Assert.Equal(0, Defs(asm, "A"));
        Assert.Equal(0, Defs(asm, "B"));
        Assert.Equal(0, Defs(asm, "ZXN_MODULE"));
        Assert.Equal(new[] { $"; @calls {name} is ignored: reaches ZXN_MODULE; call ZXN_MODULE(n) from SLANG" }, Warnings(diag));
    }

    [Fact]
    public void GoodAndBadNamesOnOneLine()
    {
        var (asm, diag) = Build("#ASM\n; @calls NOSUCH, MULHLDE\n#END\n" + EmptyMain);
        Assert.Equal(1, Defs(asm, "MULHLDE"));
        Assert.Equal(new[] { "; @calls NOSUCH is ignored: not a runtime function" }, Warnings(diag));
    }

    [Fact]
    public void DirectGenerate_BringsRuntime()
    {
        // 計画を作らない道 (Generate だけ) でも入る。警告も 1 度だけ
        var (asm, diag) = Build("#ASM\n; @calls MULHLDE, NOSUCH\n#END\n" + EmptyMain, direct: true);
        Assert.Equal(1, Defs(asm, "MULHLDE"));
        Assert.Single(Warnings(diag));
    }

    [Fact]
    public void GenerateAll_WarnsOnce()
    {
        var (_, diag) = Build("#ASM\n; @calls NOSUCH\n#END\n" + EmptyMain);
        Assert.Single(Warnings(diag));
    }

    // ---- 本物のランタイムと env で (slangc と同じ手順) ----

    private (SlangCompileResult Result, string Log, DiagnosticBag Diag) Compile(string env, string name, string text)
    {
        var path = Path.Combine(_dir, name);
        File.WriteAllText(path, text);
        var paths = new PathResolver(new List<string>(), new List<string> { _repo });
        using var log = new StringWriter();
        var resolution = SlangCompilation.ResolveEnvironment(env, paths, log);
        Assert.True(resolution != null, log.ToString());
        var diag = new DiagnosticBag();
        var result = SlangCompilation.CompileFile(path, resolution.Value.Config, paths, diag, log);
        return (result, log.ToString(), diag);
    }

    [Fact]
    public void UnknownName_SameAsmAsPlainComment()
    {
        // 前からのプログラムに、ランタイムに無い名前の `; @calls` の注釈があっても、出力は変わらない (警告だけ)
        var (a, logA, diagA) = Compile("x1", "A.SL", "#ASM\n; @calls NOSUCH\n#END\n" + EmptyMain);
        var (b, logB, _) = Compile("x1", "B.SL", "#ASM\n; NOSUCH\n#END\n" + EmptyMain);
        Assert.True(a.Ok, logA);
        Assert.True(b.Ok, logB);
        Assert.Equal(b.MainAsm!.Replace("; NOSUCH", "; @calls NOSUCH"), a.MainAsm);
        Assert.Single(Warnings(diagA));
    }

    [Fact]
    public void Module_SameSplitAsSlangCall()
    {
        // 本体の `; @calls MULHLDE` は、本体の SLANG の掛け算と同じに分ける (本体に入り、モジュールは本体のものを使う)
        const string module = "#MODULE $C000\nFM(X, Y)\nBEGIN\n  RETURN X * Y;\nEND;\n#END\n";
        var (a, logA, _) = Compile("x1", "A.SL", "#ASM\n; @calls MULHLDE\n#END\nVAR P, Q;\nMAIN()\nBEGIN\n  P = FM(P, Q);\nEND;\n" + module);
        var (b, logB, _) = Compile("x1", "B.SL", "VAR P, Q;\nMAIN()\nBEGIN\n  P = FM(P, Q) + P * Q;\nEND;\n" + module);
        Assert.True(a.Ok, logA);
        Assert.True(b.Ok, logB);
        Assert.Equal(1, Defs(a.MainAsm!, "MULHLDE"));
        Assert.Equal(1, Defs(b.MainAsm!, "MULHLDE"));
        Assert.Equal(b.Overlays.Single().Asm, a.Overlays.Single().Asm);
    }

    [Fact]
    public void Zxn_ZxnModule_WarnsAndIgnored()
    {
        var (r, log, diag) = Compile("zxn", "Z.SL", "#ASM\n; @calls ZXN_MODULE\n#END\n" + EmptyMain);
        Assert.True(r.Ok, log);
        Assert.Equal(0, Defs(r.MainAsm!, "ZXN_MODULE"));
        Assert.Equal(new[] { "; @calls ZXN_MODULE is ignored: reaches ZXN_MODULE; call ZXN_MODULE(n) from SLANG" }, Warnings(diag));
    }
}
