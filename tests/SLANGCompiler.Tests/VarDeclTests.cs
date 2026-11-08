using System.Text.RegularExpressions;
using Xunit;
using SLANGCompiler.Lexer;
using SLANGCompiler.Parser;
using SLANGCompiler.IR;
using SLANGCompiler.CodeGen;
using SLANGCompiler.Semantics;

namespace SLANGCompiler.Tests;

/// <summary>
/// VAR の固定アドレス (ラベル・CONST・式) と、間接変数の初期値
/// </summary>
public class VarDeclTests
{
    /// <summary>ASM まで作る。エラーがあれば (null, 診断)</summary>
    private static (string? Asm, DiagnosticBag Diag) Compile(string source)
    {
        var diag = new DiagnosticBag();
        var tokens = new Preprocessor(diag).Process(new Lexer.Lexer(source).Tokenize(), ".");
        var ast = new Parser.Parser(tokens, diag).ParseCompilationUnit();
        if (diag.HasErrors) return (null, diag);
        var analyzer = new SemanticAnalyzer(diag);
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

    private const string Asm = "#ASM\nBYTECOUNTER: DB 0\n#END\n";

    [Theory]
    // 整数のリテラル以外の番地も固定アドレスにする (いままでは黙って __WORK__ に置いていた)
    [InlineData("VAR BYTE BC:BYTECOUNTER;", "_V_BC", "BYTECOUNTER")]
    [InlineData("VAR BC:BYTECOUNTER + 1;", "_V_BC", "BYTECOUNTER+$0001")]
    [InlineData("CONST BASE = $C000; VAR BC:BASE + 4;", "_V_BC", "$C004")]
    [InlineData("CONST BASE = $C000, OTHER = $10; VAR BC:BASE-(OTHER+2);", "_V_BC", "$BFEE")]
    [InlineData("VAR BC:$C000;", "_V_BC", "$C000")]
    public void VarAddress_LabelAndExpressions(string decl, string label, string equ)
    {
        var asm = CompileOk($"{decl} MAIN() BEGIN BC = 1; END;\n{Asm}");
        Assert.Contains($"{label}\tEQU\t{equ}\n", asm);
        Assert.DoesNotMatch($@"{label} EQU \(__WORK__", asm);
    }

    [Fact]
    public void AsmAddress_KeepsGrouping()
    {
        // A-(B+C) の右を括弧でくくる (並べると A-B+C になる)。ARRAY の番地も同じ
        var asm = CompileOk($"CONST OTHER = $10; ARRAY BYTE T[1]:BYTECOUNTER-(OTHER+2); MAIN() BEGIN T[0] = 1; END;\n{Asm}");
        Assert.Contains("_V_T\tEQU\tBYTECOUNTER-($0010+$0002)\n", asm);
    }

    [Theory]
    // 番地と初期値を両方書ける (いままでは = 7 が番地の式に取り込まれ、黙って __WORK__ に置いていた)
    [InlineData("VAR X:$C000 = 7;", "$C000")]
    [InlineData("VAR X:BYTECOUNTER = 7;", "BYTECOUNTER")]
    public void VarAddressWithInitializer(string decl, string equ)
    {
        var asm = CompileOk($"{decl} MAIN() BEGIN X = X + 1; END;\n{Asm}");
        Assert.Contains($"_V_X\tEQU\t{equ}\n", asm);
        Assert.Matches(@"LD\tHL,\$0007\s*\n\s*LD\t\(_V_X\),HL", asm);
    }

    [Fact]
    public void PointerInitializer_Global()
    {
        // VAR P[] = 番地: 変数そのもの (指す番地) の初期値。起動時に代入する
        var asm = CompileOk("CONST BASE = $C000; VAR P[] = $C000; VAR BYTE Q[] = BASE + 2; MAIN() BEGIN P[0] = 1; Q[0] = 2; END;");
        Assert.Matches(@"LD\tHL,\$C000\s*\n\s*LD\t\(_V_P\),HL", asm);
        Assert.Matches(@"LD\tHL,\$C002\s*\n\s*LD\t\(_V_Q\),HL", asm);
    }

    [Fact]
    public void PointerInitializer_Local()
    {
        // 局所の間接変数の初期値は、宣言の所で書く (単純変数の局所の初期値と同じ)
        var asm = CompileOk("FN() BEGIN VAR L[] = $D000; L[0] = 1; END; MAIN() BEGIN FN(); END;");
        var body = asm.Split("\nFN:")[1].Split("_FN_EXIT")[0];
        Assert.Matches(@"LD\t\(IY\+\$\w\w\),\$00\s*\n\s*LD\t\(IY\+\$\w\w\),\$D0", body);
    }

    [Theory]
    [InlineData("VAR T[3] = 5;", "with a size cannot have a single-value initializer")]
    [InlineData("ARRAY BYTE A[] = $C000;", "cannot have a single-value initializer")]   // ARRAY の添字なしは配列 (間接変数でない)
    [InlineData("ARRAY T[3] = 5;", "with a size cannot have a single-value initializer")]
    [InlineData("VAR X:\"ABC\";", "Unsupported expression")]
    public void Errors(string decl, string message)
    {
        var (_, diag) = Compile($"{decl} MAIN() BEGIN END;");
        Assert.Contains(diag.Diagnostics, d => d.Message.Contains(message));
    }

    [Fact]
    public void AsmAddress_ConstExpansionKeepsGrouping()
    {
        // 右が CONST の名前で、その中身が OTHER+2 (ラベルの式) のときも括弧でくくる
        var asm = CompileOk("CONST DELTA = OTHER + 2; VAR X:BASE-DELTA; MAIN() BEGIN X = 1; END;\n"
            + "#ASM\nBASE: DB 0\nOTHER: DB 0\n#END\n");
        Assert.Contains("_V_X\tEQU\tBASE-(OTHER+$0002)\n", asm);
    }

    [Theory]
    // BYTE の初期値は 1 バイトだけ書く (2 バイト書くと、隣を壊す)
    [InlineData("VAR BYTE B = 9;", "_V_B", "$09")]
    [InlineData("VAR BYTE B:BYTECOUNTER = 7;", "_V_B", "$07")]
    public void ByteInitializer_WritesOneByte(string decl, string label, string value)
    {
        var asm = CompileOk($"{decl} MAIN() BEGIN B = 1; END;\n{Asm}");
        Assert.Matches($@"LD\tA,\{value}\s*\n\s*LD\t\({label}\),A", asm);
    }

    [Fact]
    public void PointerInitializer_NonConstant_Warns()
    {
        // 起動時の代入は定数だけ (単純変数と同じ警告)
        var (_, diag) = Compile("VAR Y; VAR P[] = &Y; MAIN() BEGIN P[0] = 1; END;");
        Assert.Contains(diag.Diagnostics, d => d.Message.Contains("Non-constant initializer for static/global variable 'P'"));
    }

    [Theory]
    // 番地だけの MACHINE はラベルを出さないので、VAR の番地の式には使えない (ランタイムの名前は IntegrationTests)。
    // CONST の中身 (アセンブラの式として展開される) にあるときも
    [InlineData("MACHINE ROM(0):$1234; VAR X:ROM;")]
    [InlineData("MACHINE ROM(0):$1234; CONST ADDR = ROM; VAR X:ADDR;")]
    [InlineData("MACHINE ROM(0):$1234; CONST A1 = ROM + 1; CONST A2 = A1; VAR X:A2;")]
    public void VarAddress_AddressOnlyMachine_Error(string decls)
    {
        var (_, diag) = Compile($"{decls} MAIN() BEGIN X = 1; END;");
        Assert.Contains(diag.Diagnostics, d => d.Message.Contains("cannot use MACHINE 'ROM' without a CODE body"));
    }

    [Theory]
    // 間接変数の初期値は番地 (整数)。FLOAT の式はエラー (途中の小数を整数で畳み込んで、黙って違う番地にしない)
    [InlineData("VAR P[] = 1024.0; MAIN() BEGIN P[0] = 1; END;")]
    [InlineData("VAR P[] = (1.0/2.0)*4096.0; MAIN() BEGIN P[0] = 1; END;")]
    [InlineData("FN() BEGIN VAR L[] = (1.0/2.0)*4096.0; L[0] = 1; END; MAIN() BEGIN FN(); END;")]
    public void PointerInitializer_Float_Errors(string source)
    {
        var (_, diag) = Compile(source);
        Assert.Contains(diag.Diagnostics, d => d.Message.Contains("must be an integer expression, not FLOAT"));
    }

    // ==== 実装レビューで見つかった形 ====

    [Theory]
    // 局所の初期値が引数を読むとき、引数のオフセットが決まってから出す (いまの main でも、単純変数で IY+$00 を読んでいた)
    [InlineData("FN(BASE) BEGIN VAR P[] = BASE; P[0] = 1; END; MAIN() BEGIN FN($C000); END;")]
    [InlineData("VAR R; FN(BASE) BEGIN VAR X = BASE; R = X; END; MAIN() BEGIN FN(5); END;")]
    public void LocalInitializer_ReadsParameterAtFinalOffset(string source)
    {
        var asm = CompileOk(source);
        var body = asm.Split("\nFN:")[1].Split("_FN_EXIT")[0];
        Assert.DoesNotContain("(IY+$00)", body);
        Assert.Matches(@"LD\tL,\(IY\+\$6C\)\s*\n\s*LD\tH,\(IY\+\$6D\)", body);
    }

    [Fact]
    public void PointerInitializer_LocalShadowsGlobalConst()
    {
        // 局所の BASE は大域の CONST BASE を隠す (その場で決まる値として $C000 にしない)
        var asm = CompileOk("CONST BASE = $C000; FN() BEGIN VAR BASE = $D000; VAR P[] = BASE; P[0] = 1; END; MAIN() BEGIN FN(); END;");
        var body = asm.Split("\nFN:")[1].Split("_FN_EXIT")[0];
        Assert.DoesNotMatch(@"LD\t\(IY\+\$\w\w\),\$C0", body);      // $C000 を即値で書かない
        Assert.DoesNotContain("$C000", body);
    }

    [Theory]
    // 固定アドレスの配列の並びの初期値は、どの出力も書かないのでエラー (前は構文エラーだった形)
    [InlineData("ARRAY BYTE BUF[3]:$C000 = {1,2,3};")]
    [InlineData("VAR BYTE BUF[3]:$C000 = {1,2,3};")]
    public void FixedAddressArrayInitializer_Errors(string decl)
    {
        var (_, diag) = Compile($"{decl} MAIN() BEGIN BUF[0] = 1; END;");
        Assert.Contains(diag.Diagnostics, d => d.Message.Contains("with a fixed address cannot have an initializer"));
    }

    [Fact]
    public void PointerInitializer_IsCheckedLikeAssignment()
    {
        // 間接変数の初期値の式も、意味解析で調べる (配列への代入は禁止)
        var (_, diag) = Compile("ARRAY T[3]; FN() BEGIN VAR P[] = (T = $C000); P[0] = 1; END; MAIN() BEGIN FN(); END;");
        Assert.True(diag.HasErrors);
    }

    [Fact]
    public void LocalInitializer_SeesNamesVisibleAtDeclaration()
    {
        // 後で出す局所の初期値も、宣言の所で見えていた名前で引く: X の初期値の BASE は大域の CONST (7)。
        // 後ろで宣言する局所の BASE (9) ではない
        var asm = CompileOk("CONST BASE = 7; VAR R; FN() BEGIN VAR X = BASE; VAR BASE = 9; R = X + BASE; END; MAIN() BEGIN FN(); END;");
        var body = asm.Split("\nFN:")[1].Split("_FN_EXIT")[0];
        Assert.Matches(@"LD\t\(IY\+\$\w\w\),\$07", body);
    }

    [Fact]
    public void PointerInitializer_IntegerLocalShadowsFloatConst_Ok()
    {
        // 局所の整数の BASE は、大域の FLOAT の CONST BASE を隠す (FLOAT の式としてエラーにしない)
        var (_, diag) = Compile("CONST BASE = 0.5; FN() BEGIN VAR BASE = $D000; VAR P[] = BASE; P[0] = 1; END; MAIN() BEGIN FN(); END;");
        Assert.DoesNotContain(diag.Diagnostics, d => d.Message.Contains("not FLOAT"));
    }

    [Theory]
    // VAR の番地も整数。FLOAT の式はエラー (途中の小数を整数で畳み込んで、黙って 0 番地にしない)
    [InlineData("VAR BYTE X:(1.0/2.0)*4096.0;")]
    [InlineData("VAR BYTE X:((1.0/2.0) > 0.0) ? $C000 : $D000;")]   // 条件の中の FLOAT の比較も
    [InlineData("VAR X:1024.0;")]
    [InlineData("VAR X:$C000 + HIGH((1.0/2.0)*4096.0);")]   // HIGH・LOW の中の FLOAT も
    public void VarAddress_Float_Errors(string decl)
    {
        var (_, diag) = Compile($"{decl} MAIN() BEGIN X = 1; END;");
        Assert.Contains(diag.Diagnostics, d => d.Message.Contains("must be an integer expression, not FLOAT"));
    }

    [Fact]
    public void VarAddress_FloatVariableLabel_Ok()
    {
        // 番地の式の FLOAT の変数の名前は、その番地 (整数のラベル)。FLOAT の式としてエラーにしない
        var asm = CompileOk("VAR FLOAT VALUE; VAR X:VALUE; MAIN() BEGIN X = 1; END;");
        Assert.Contains("_V_X\tEQU\t_V_VALUE\n", asm);
    }
}
