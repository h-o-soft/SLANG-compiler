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
    // 試験用の小さなランタイム (A は B を、B は ZXN_MODULE を、DIVHLDE は DIVHLDE8 を @calls で使う。BIT は RBIT の別名。
    // LOC は @resident local)
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

; @name RBIT
; @alias BIT
LD HL,5
RET

; @name DIVHLDE
; @calls DIVHLDE8
CALL DIVHLDE8
RET

; @name DIVHLDE8
LD HL,6
RET

; @name LOC
; @resident local
LD HL,7
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
        var (asm, _, diag) = BuildAll(source, direct);
        return (asm, diag);
    }

    private static (string Asm, List<(string Name, string Asm)> Overlays, DiagnosticBag Diag) BuildAll(string source, bool direct = false)
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
        if (direct) return (codeGen.Generate(), new List<(string, string)>(), diag);
        var (mainAsm, overlays) = codeGen.GenerateAll();
        return (mainAsm, overlays, diag);
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
    public void UserFunctionSameName_Error()
    {
        // 無視すると、#ASM の CALL MULHLDE が黙って利用者の関数を呼ぶので、エラーにする
        var (_, diag) = Build("#ASM\n; @calls MULHLDE\n#END\nMULHLDE(X, Y)\nBEGIN\n  RETURN X;\nEND;\n" + EmptyMain);
        var e = Assert.Single(diag.Diagnostics, d => d.Severity == DiagnosticSeverity.Error);
        Assert.Equal("; @calls MULHLDE: MULHLDE is a user function, so the #ASM CALL would not reach the runtime function", e.Message);
    }

    [Fact]
    public void AliasOfUserFunction_Error()
    {
        // BIT は RBIT の別名。ランタイムの入口 RBIT が利用者の関数なので、名前そのものが当たるときと同じくエラー
        var (_, diag) = Build("#ASM\n; @calls BIT\n\tCALL RBIT\n#END\nRBIT(X)\nBEGIN\n  RETURN X;\nEND;\n" + EmptyMain);
        var e = Assert.Single(diag.Diagnostics, d => d.Severity == DiagnosticSeverity.Error);
        Assert.Equal("; @calls BIT: RBIT is a user function, so the #ASM CALL would not reach the runtime function", e.Message);
    }

    [Fact]
    public void ModuleFunctionCalledFromMain_Error()
    {
        // 本体が呼ぶモジュールの関数は、本体の ASM から見える
        var (_, diag) = Build("#ASM\n; @calls MULHLDE\n#END\nMAIN()\nBEGIN\n  MULHLDE(1, 2);\nEND;\n#MODULE $C000\nMULHLDE(X, Y)\nBEGIN\n  RETURN X;\nEND;\n#END\n");
        Assert.Contains(diag.Diagnostics, d => d.Severity == DiagnosticSeverity.Error && d.Message.StartsWith("; @calls MULHLDE: MULHLDE is a user function"));
    }

    [Fact]
    public void ModuleOnlyFunction_DoesNotBlock()
    {
        // 本体が呼ばないモジュールの関数は、別のアセンブルの単位なので、本体のランタイムとぶつからない
        var (asm, diag) = Build("#ASM\n; @calls MULHLDE\n#END\n" + EmptyMain + "#MODULE $C000\nMULHLDE(X, Y)\nBEGIN\n  RETURN X;\nEND;\n#END\n");
        Assert.False(diag.HasErrors, string.Join("; ", diag.Diagnostics));
        Assert.Empty(Warnings(diag));
        Assert.Equal(1, Defs(asm, "MULHLDE"));
    }

    [Fact]
    public void DependencyIsUserFunction_WarnsAndIgnored()
    {
        // DIVHLDE は @calls で DIVHLDE8 を使う。利用者の DIVHLDE8 とラベルが 2 つになる
        var (asm, diag) = Build("#ASM\n; @calls DIVHLDE\n#END\nDIVHLDE8(X)\nBEGIN\n  RETURN X;\nEND;\n" + EmptyMain);
        Assert.Equal(0, Defs(asm, "DIVHLDE"));
        Assert.Equal(1, Defs(asm, "DIVHLDE8"));
        Assert.Equal(new[] { "; @calls DIVHLDE is ignored: needs DIVHLDE8, which is a user function" }, Warnings(diag));
        Assert.False(diag.HasErrors);
    }

    [Fact]
    public void AliasOnly_BringsCanonical()
    {
        var (asm, diag) = Build("#ASM\n; @calls BIT\n#END\n" + EmptyMain);
        Assert.Equal(1, Defs(asm, "RBIT"));
        Assert.Empty(Warnings(diag));
    }

    [Fact]
    public void Alias_BringsCanonicalOnce()
    {
        var (asm, diag) = Build("#ASM\n; @calls BIT, RBIT\n#END\n" + EmptyMain);
        Assert.Equal(1, Defs(asm, "RBIT"));
        Assert.Empty(Warnings(diag));
    }

    [Fact]
    public void ResidentModule_LocalRuntime_UsesMainCopy()
    {
        // RESIDENT のモジュールが使う @resident local の関数は、ふつうはモジュールの中に写しを持つ。
        // 本体の `; @calls` で本体に入れると、本体の SLANG から呼んだときと同じく、モジュールは本体のものを使う
        const string module = "#MODULE $C000 RESIDENT\nFM()\nBEGIN\n#ASM\n\tCALL\tLOC\n#END\nEND;\n#END\n";
        var (mainA, ovA, diagA) = BuildAll(EmptyMain + module);
        var (mainB, ovB, diagB) = BuildAll("#ASM\n; @calls LOC\n#END\n" + EmptyMain + module);
        Assert.Empty(Warnings(diagA));
        Assert.Empty(Warnings(diagB));
        Assert.Equal(0, Defs(mainA, "LOC"));
        Assert.Equal(1, Defs(ovA.Single().Asm, "LOC"));
        Assert.Equal(1, Defs(mainB, "LOC"));
        Assert.Equal(0, Defs(ovB.Single().Asm, "LOC"));
        Assert.Matches(@"(?m)^\s*CALL\s+LOC\b", ovB.Single().Asm);
        // 本体の番地を取り込む印 (slangbuild が読むもの)
        Assert.DoesNotContain("LOC", SLANGCompiler.Build.OverlayImportsBuilder.ExtractExternNames(ovA.Single().Asm));
        Assert.Contains("LOC", SLANGCompiler.Build.OverlayImportsBuilder.ExtractExternNames(ovB.Single().Asm));
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
        var paths = new PathResolver(new List<string> { Path.Combine(_repo, "include") }, new List<string> { _repo });
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
    public void Library_UserFunctionSameName_Error()
    {
        // 同梱のライブラリと、ランタイムと同じ名前 (MULHLDE) の利用者の関数: ライブラリの掛け算が利用者の関数を呼ばないよう、止める
        var (r, log, diag) = Compile("x1", "U.SL", "#INCLUDE TILELIB.LIB\nMULHLDE(X, Y)\nBEGIN\n  RETURN X;\nEND;\n" + EmptyMain);
        Assert.False(r.Ok);
        Assert.Contains(diag.Diagnostics, d => d.Severity == DiagnosticSeverity.Error && d.Message.StartsWith("; @calls MULHLDE: MULHLDE is a user function"));
    }

    [Theory]
    [InlineData("RND", "RND_INITIALIZE")]   // RND の @init_code が出すラベル
    [InlineData("RND", "RND_SEED1")]        // RND の @works
    [InlineData("ABS", "ABSNEGCALL")]       // ABS の本体の中のラベル
    public void RuntimeGeneratedLabelIsUserFunction_WarnsAndIgnored(string runtime, string userFunc)
    {
        var (r, log, diag) = Compile("x1", "R.SL", $"#ASM\n; @calls {runtime}\n#END\n{userFunc}()\nBEGIN\nEND;\n" + EmptyMain);
        Assert.True(r.Ok, log);
        Assert.Equal(0, Defs(r.MainAsm!, runtime));
        Assert.Equal(1, Defs(r.MainAsm!, userFunc));
        Assert.Equal(new[] { $"; @calls {runtime} is ignored: needs {userFunc}, which is a user function" }, Warnings(diag));
    }

    [Fact]
    public void Library_ModuleOnlyMulhlde_Builds()
    {
        // 本体の TILELIB.LIB と、モジュールの中だけにある MULHLDE という関数は、別のアセンブルの単位なのでぶつからない
        // (モジュールの中で SLANG から呼ぶと、モジュールにランタイムの MULHLDE も入って 2 つになるのは前から)
        var (r, log, diag) = Compile("x1", "M.SL", "#INCLUDE TILELIB.LIB\nMAIN()\nBEGIN\nEND;\n#MODULE $C000\nMULHLDE(X, Y)\nBEGIN\n  RETURN X;\nEND;\n#END\n");
        Assert.True(r.Ok, log);
        Assert.False(diag.HasErrors);
        Assert.Equal(1, Defs(r.MainAsm!, "MULHLDE"));
    }

    [Fact]
    public void X1_Slanginit_WarnsAndIgnored()
    {
        var (r, log, diag) = Compile("x1", "S.SL", "#ASM\n; @calls SLANGINIT\n#END\n" + EmptyMain);
        Assert.True(r.Ok, log);
        Assert.Equal(0, Defs(r.MainAsm!, "SLANGINIT"));
        Assert.Equal(new[] { "; @calls SLANGINIT is ignored: SLANGINIT is the program's startup code, not a routine" }, Warnings(diag));
    }

    [Theory]
    [InlineData("SLANGINIT__NOROM", "SLANGINIT__NOROM is the program's startup code, not a routine")]
    [InlineData("PRT__NOROM", "PRT__NOROM is a replacement version of PRT")]
    public void Zxn_StartupOrVariant_WarnsAndIgnored(string name, string reason)
    {
        var (r, log, diag) = Compile("zxn", "V.SL", $"#ASM\n; @calls {name}\n#END\n" + EmptyMain);
        Assert.True(r.Ok, log);
        Assert.Equal(0, Defs(r.MainAsm!, name));
        Assert.DoesNotContain("<<CALLINITIALIZER>>", r.MainAsm);
        Assert.Equal(new[] { $"; @calls {name} is ignored: {reason}" }, Warnings(diag));
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
