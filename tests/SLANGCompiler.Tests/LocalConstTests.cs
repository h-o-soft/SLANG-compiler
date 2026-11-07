using Xunit;
using SLANGCompiler.Lexer;
using SLANGCompiler.Parser;
using SLANGCompiler.IR;
using SLANGCompiler.CodeGen;
using SLANGCompiler.Semantics;

namespace SLANGCompiler.Tests;

/// <summary>
/// 関数の中の CONST (局所的な記号定数)。宣言したその場で値が決まる整数か FLOAT の定数だけを受け付け、
/// 扱いにくいもの (大域の名前と同じ名前・書き込み・CODE ブロック・CONST ASM など) はエラーにする
/// </summary>
public class LocalConstTests
{
    /// <summary>ASM まで作る。エラーがあれば (null, 診断)</summary>
    private static (string? Asm, DiagnosticBag Diag) Compile(string source, bool caseSensitive = false)
    {
        var diag = new DiagnosticBag();
        var preprocessor = new Preprocessor(diag);
        var tokens = preprocessor.Process(new Lexer.Lexer(source).Tokenize(), ".");
        var ast = new Parser.Parser(tokens, diag).ParseCompilationUnit();
        if (diag.HasErrors) return (null, diag);
        var analyzer = new SemanticAnalyzer(diag, caseSensitive) { IdentifiersUsedByPreprocessorIf = preprocessor.IdentifiersUsedByIf };
        analyzer.Analyze(ast);
        if (diag.HasErrors) return (null, diag);
        var ir = new IrGenerator(diag, analyzer.Symbols).Generate(ast);
        if (diag.HasErrors) return (null, diag);
        return (new CodeGenerator(ir).Generate(), diag);
    }

    private static string CompileOk(string source)
    {
        var (asm, diag) = Compile(source);
        Assert.False(diag.HasErrors, string.Join("; ", diag.Diagnostics));
        return asm!;
    }

    private static string Body(string asm, string func)
        => asm.Split($"\n{func}:")[1].Split($"_{func}_EXIT")[0];

    [Fact]
    public void StaticAndLocalSections()
    {
        // BEGIN の前 (静的宣言) と後 (局所宣言) のどちらにも書ける。前の CONST と大域の CONST を使う式も
        var asm = CompileOk(@"
CONST G = 100;
VAR X;
FN1(P)
CONST K = 5, K2 = K + 1;
BEGIN
  CONST L = K * 2 + G;
  X = K + K2 + L;
  RETURN K;
END;
MAIN() BEGIN X = FN1(1); END;");
        var body = Body(asm, "FN1");
        Assert.Contains("LD\tHL,$0079", body);   // 5 + 6 + 110 = 121
        Assert.Contains("LD\tHL,$0005", body);
    }

    [Fact]
    public void ZxslangReport_SingleConst()
    {
        // zxslang の報告: いままでは "Expected variable name" を 30 回出していた
        var asm = CompileOk("FN1() CONST K = 5; BEGIN RETURN K; END; MAIN() BEGIN FN1(); END;");
        Assert.Contains("LD\tHL,$0005", Body(asm, "FN1"));
    }

    [Fact]
    public void FloatConst()
    {
        var asm = CompileOk("FLOAT F; FN1() CONST KF = 1.5; BEGIN F = KF; END; MAIN() BEGIN FN1(); END;");
        var body = Body(asm, "FN1");
        Assert.Contains("LD\tHL,$8000", body);   // 1.5 の f24 (仮数 $8000、指数 $3F)
        Assert.Contains("LD\tA,$3F", body);
    }

    [Fact]
    public void ArraySizeAndFixedAddress()
    {
        // 局所の配列の大きさ、静的配列の固定アドレスの式 (局所の値 + 4)
        var asm = CompileOk(@"
FN1()
CONST BASE = $C000, N = 3;
ARRAY BYTE BUF[N]:BASE + 4;
BEGIN
  ARRAY BYTE LA[N];
  LA[0] = 1;
  BUF[0] = 2;
END;
MAIN() BEGIN FN1(); END;");
        Assert.Contains("_V_FN1_BUF\tEQU\t$C000+$0004", asm);
        Assert.Contains("LD\tBC,$0004", Body(asm, "FN1"));   // LA は 4 バイト (N + 1)
    }

    [Fact]
    public void ScalarVarFixedAddress()
    {
        // 静的な VAR の固定アドレスに関数の中の CONST (と、それを使った式)
        var asm = CompileOk(@"
FN()
CONST BASE = $C000;
VAR BYTE REG:BASE, REG2:BASE + 2;
BEGIN
  REG = 1;
  REG2 = 2;
END;
MAIN() BEGIN FN(); END;");
        Assert.Matches(@"_V_FN_REG\s+EQU\s+\$C000", asm);
        Assert.Matches(@"_V_FN_REG2\s+EQU\s+\$C002", asm);
    }

    [Fact]
    public void ScalarVarFixedAddress_LocalConstNotNeededForValue()
    {
        // 関数の中の CONST の名前を書いた式は、大域の名前だけで値が出ても (BASE を見ずに $D000) 固定アドレスにする
        var asm = CompileOk(@"
FN()
CONST BASE = $C000;
VAR BYTE REG:1 ? $D000 : BASE;
BEGIN
  REG = 1;
END;
MAIN() BEGIN FN(); END;");
        Assert.Matches(@"_V_FN_REG\s+EQU\s+\$D000", asm);
    }

    [Fact]
    public void CodeListItem_SameAsGlobalConst()
    {
        // 関数の本体の CODE(名前) は、大域の CONST でも局所の CONST でも同じ (名前の式は実行時の式として HL に入れる)
        var asm = CompileOk(@"
CONST OP = $C9;
VAR X;
FG() BEGIN X = 1; CODE(OP); END;
FL() CONST LOP = $C9; BEGIN X = 1; CODE(LOP); END;
MAIN() BEGIN FG(); FL(); END;");
        foreach (var f in new[] { "FG", "FL" })
        {
            Assert.Contains("LD\tHL,$00C9", Body(asm, f));
            Assert.DoesNotContain("DB", Body(asm, f));
        }
    }

    [Fact]
    public void NotVisibleFromOtherFunctions()
    {
        var (_, diag) = Compile("FN() CONST K = 5; BEGIN RETURN K; END; FN2() BEGIN RETURN K; END; MAIN() BEGIN FN(); FN2(); END;");
        Assert.Contains(diag.Diagnostics, d => d.ToString().Contains("Undefined variable: K"));
    }

    [Fact]
    public void ModuleFunctionConstDoesNotLeak()
    {
        // #MODULE の中の関数の CONST も、大域には出ない
        var (_, diag) = Compile("VAR X; MAIN() BEGIN X = SUB1() + J; END;\n#MODULE $8000\nSUB1() CONST J = 5; BEGIN RETURN J; END;");
        Assert.Contains(diag.Diagnostics, d => d.ToString().Contains("Undefined variable: J"));
    }

    [Theory]
    [InlineData("1")]
    [InlineData("-1")]        // プリプロセッサが値を取らない (整数のリテラルでない) もの
    [InlineData("2 + 3")]
    [InlineData("TRUE")]      // ほかの定数の別名
    public void UsedInPreprocessorIf_Errors(string value)
    {
        // #IF はファイル全体に効くので、関数の中の CONST をほかの関数の #IF で使わせない
        var (_, diag) = Compile(@"
VAR X;
FN() CONST FLAG = " + value + @"; BEGIN END;
FN2()
BEGIN
#IF FLAG==1
  X = 1;
#ELSE
  X = 2;
#ENDIF
END;
MAIN() BEGIN FN(); FN2(); END;");
        Assert.Contains(diag.Diagnostics, d => d.ToString().Contains("CONST 'FLAG' in a function cannot be used in #IF"));
    }

    [Fact]
    public void NotUsedInPreprocessorIf_Ok()
    {
        // #IF で使わなければ通る (#IF の名前が別の大域の CONST なら、その値)
        var asm = CompileOk(@"
CONST GFLAG = 1;
VAR X;
FN() CONST FLAG = 1; BEGIN X = FLAG; END;
FN2()
BEGIN
#IF GFLAG==1
  X = 3;
#ENDIF
END;
MAIN() BEGIN FN(); FN2(); END;");
        Assert.Contains("LD\tHL,$0003", Body(asm, "FN2"));
    }

    [Fact]
    public void GlobalLazyConstUnaffected()
    {
        // 大域の遅延式 (ラベルの CONST) は、局所の CONST のある関数から先に参照しても、大域だけで解決する
        var asm = CompileOk(@"
VAR TARGET, X;
CONST PTR = TARGET;
FN() CONST K = 1; BEGIN X = PTR + K; END;
FN2() BEGIN X = PTR; END;
MAIN() BEGIN FN(); FN2(); END;");
        Assert.Contains("LD\tHL,_V_TARGET", Body(asm, "FN2"));
    }

    [Fact]
    public void FixedAddress_FollowsGlobalForwardConst()
    {
        // 固定アドレスの式の中で、後ろで決まる大域の CONST の式もたどる (5 * (7 * 2) = 70)
        var asm = CompileOk(@"
CONST G = H * 2;
CONST H = 7;
FN()
CONST K = 5;
ARRAY BYTE T[1]:K * G;
BEGIN T[0] = 1; END;
MAIN() BEGIN FN(); END;");
        Assert.Contains("_V_FN_T\tEQU\t$0046", asm);
    }

    [Fact]
    public void FixedAddress_FollowedGlobalConstDoesNotSeeLocalConst()
    {
        // 大域の CONST の式は大域だけで解決する。たどった先の K は、関数の中の K ではない
        var (_, diag) = Compile(@"
CONST G = K * 2;
FN()
CONST K = 5;
ARRAY BYTE T[1]:K * G;
BEGIN T[0] = 1; END;
MAIN() BEGIN FN(); END;");
        Assert.Contains(diag.Diagnostics, d => d.ToString().Contains("Unsupported expression in MACHINE/CONST address"));
    }

    [Fact]
    public void UsedBeforeDeclaration_InInitializerItem()
    {
        // 初期値の要素に後ろの CONST (要素が定数でないというエラーも続けて出る)
        var (_, diag) = Compile("FN() ARRAY BYTE T[1] = {V, 1}; CONST V = 3; BEGIN END; MAIN() BEGIN END;");
        Assert.Contains(diag.Diagnostics, d => d.ToString().Contains("CONST 'V' is used before its declaration"));
    }

    [Fact]
    public void UsedBeforeDeclaration_WithInitializer()
    {
        // 初期値のある配列でも、1 要素で通らずにエラーになる (大きさが決まらないので、容量のエラーも続けて出る)
        var (_, diag) = Compile("FN() ARRAY BYTE BUF[N] = {1, 2}; CONST N = 1; BEGIN END; MAIN() BEGIN END;");
        Assert.Contains(diag.Diagnostics, d => d.ToString().Contains("CONST 'N' is used before its declaration"));
    }

    [Theory]
    // 大文字と小文字を区別する記号表でも、引数・局所変数・ほかの CONST と大文字小文字だけ違う CONST は受け付けない
    // (IR の局所変数の表は大文字小文字を区別しないため)
    [InlineData("FN(K) CONST k = 2; BEGIN RETURN k; END;")]
    [InlineData("FN() VAR K; CONST k = 2; BEGIN RETURN k; END;")]
    [InlineData("FN() CONST k = 2; BEGIN VAR K; RETURN k; END;")]
    [InlineData("FN() CONST K = 1, k = 2; BEGIN RETURN k; END;")]   // 後の方が黙って上書きしないこと
    public void CaseSensitive_ConstVsVariableDifferingInCase_Errors(string decls)
    {
        var (_, diag) = Compile(decls + " MAIN() BEGIN END;", caseSensitive: true);
        Assert.Contains(diag.Diagnostics, d => d.ToString().Contains("duplicate name"));
    }

    [Theory]
    [InlineData("FN() CONST K = 5; BEGIN K = 7; END;", "cannot assign to CONST 'K'")]
    [InlineData("FN() CONST K = 5; BEGIN K += 1; END;", "cannot compound-assign CONST 'K'")]
    [InlineData("FN() CONST K = 5; BEGIN K++; END;", "cannot increment CONST 'K'")]
    [InlineData("VAR X; FN() CONST K = 5; BEGIN X = &K; END;", "cannot take the address of CONST 'K'")]
    [InlineData("VAR X; FN() CONST K = 5; BEGIN X = K[0]; END;", "CONST 'K' is not an array")]
    [InlineData("FN() CONST K = 5; BEGIN FOR K = 1 TO 3 { } END;", "cannot use CONST 'K' as a FOR variable")]
    [InlineData("FN() CONST K = 5; BEGIN RETURN K = 7; END;", "cannot assign to CONST 'K'")]
    [InlineData("VAR X; FN() CONST K = 5; BEGIN END(K = 7);", "cannot assign to CONST 'K'")]
    [InlineData("FN() CONST K = 5; ARRAY BYTE BUF[K++]; BEGIN END;", "cannot increment CONST 'K'")]
    [InlineData("FN() ARRAY BYTE BUF[N]; CONST N = 1; BEGIN END;", "CONST 'N' is used before its declaration")]
    [InlineData("FN() ARRAY BYTE BUF[0]:BASE; CONST BASE = $C000; BEGIN END;", "CONST 'BASE' is used before its declaration")]
    [InlineData("FN() VAR W = V; CONST V = 3; BEGIN END;", "CONST 'V' is used before its declaration")]
    [InlineData("FN() BEGIN VAR W = CODE(WORD, V); CONST V = 3; END;", "CONST 'V' is used before its declaration")]
    [InlineData("FN() ARRAY BYTE BUF[N++]; CONST N = 3; BEGIN END;", "CONST 'N' is used before its declaration")]
    [InlineData("FN() CONST K = 5; VAR BYTE REG:K++; BEGIN END;", "cannot increment CONST 'K'")]
    [InlineData("FN() CONST K = 5; ARRAY BYTE BUF[0]:K++; BEGIN END;", "cannot increment CONST 'K'")]
    [InlineData("VAR X; FN() CONST K = 5; VAR BYTE REG:X + K; BEGIN END;", "fixed address using CONST in a function must be a compile-time constant")]
    [InlineData("VAR K; FN() CONST K = 5; BEGIN END;", "has the same name as a global name")]
    [InlineData("FN() CONST K = 5; BEGIN END; VAR K;", "has the same name as a global name")]
    [InlineData("FN() CONST K = 5; BEGIN END; K() BEGIN END;", "has the same name as a global name")]
    [InlineData("FN(K) CONST K = 5; BEGIN END;", "duplicate name 'K'")]
    [InlineData("FN() VAR K, J; CONST K = 5; BEGIN END;", "duplicate name 'K'")]
    [InlineData("FN() CONST K = 5; VAR K, J; BEGIN END;", "duplicate name 'K'")]
    [InlineData("FN() CONST K = 5, K = 6; BEGIN END;", "duplicate name 'K'")]
    [InlineData("FN() CONST K = 5; BEGIN VAR K = 7; END;", "duplicate name 'K'")]
    [InlineData("VAR X; FN() CONST K = X; BEGIN END;", "must be a compile-time integer or FLOAT constant")]
    [InlineData("FN() CONST T = [1, 2]; BEGIN END;", "CONST with a code block is not supported in a function")]
    [InlineData("FN() CONST ASM K = 1; BEGIN END;", "CONST ASM is only allowed at global scope")]
    [InlineData("FN() MACHINE M(0):$1234; BEGIN END;", "MACHINE is only allowed at global scope")]
    [InlineData("MACHINE MF(1); MF(1) CONST K = 1; [CODE($C9);]", "CONST is not allowed in a MACHINE declaration")]
    public void Rejected_SingleError(string decls, string message)
    {
        var (_, diag) = Compile(decls + " MAIN() BEGIN END;");
        var errors = diag.Diagnostics.Where(d => d.Severity == DiagnosticSeverity.Error).ToList();
        Assert.Single(errors);
        Assert.Contains(message, errors[0].ToString());
    }
}
