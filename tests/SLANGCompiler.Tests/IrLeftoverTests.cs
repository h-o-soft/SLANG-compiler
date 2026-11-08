using SLANGCompiler.IR;
using SLANGCompiler.Lexer;
using SLANGCompiler.Parser;
using SLANGCompiler.Semantics;
using Xunit;

namespace SLANGCompiler.Tests;

/// <summary>
/// 型の整理 (#270) の後に残した 4 件: 大域の CONST を隠す変数、本体が CODE だけの関数、書けない代入先、MACHINE の引数の数
/// </summary>
public class IrLeftoverTests
{
    private static (DiagnosticBag Diag, IrModule Module) Generate(string source)
    {
        var diag = new DiagnosticBag();
        var tokens = new Lexer.Lexer(source).Tokenize();
        tokens = new Preprocessor(diag).Process(tokens, ".");
        var ast = new Parser.Parser(tokens, diag).ParseCompilationUnit();
        Assert.False(diag.HasErrors, $"Parse errors: {string.Join("; ", diag.Diagnostics)}");
        var analyzer = new SemanticAnalyzer(diag);
        analyzer.Analyze(ast);
        var module = new IrGenerator(diag, analyzer.Symbols).Generate(ast);
        return (diag, module);
    }

    private static string Asm(string source)
    {
        var (diag, module) = Generate(source);
        Assert.False(diag.HasErrors, $"IR errors: {string.Join("; ", diag.Diagnostics)}");
        return new SLANGCompiler.CodeGen.CodeGenerator(module).Generate();
    }

    /// <summary>関数 name の ASM (ラベルから _name_EXIT まで)</summary>
    private static string FuncAsm(string source, string name) => Asm(source).Split($"\n{name}:")[1].Split($"_{name}_EXIT")[0];

    private static string Lines(params string[] lines) => string.Join("\n", lines.Select(l => "\t" + l));

    /// <summary>構文解析から IR までのエラー (構文解析のエラーがあっても先へ進める)</summary>
    private static IEnumerable<string> Errors(string source)
    {
        var diag = new DiagnosticBag();
        var tokens = new Preprocessor(diag).Process(new Lexer.Lexer(source).Tokenize(), ".");
        var ast = new Parser.Parser(tokens, diag).ParseCompilationUnit();
        var analyzer = new SemanticAnalyzer(diag);
        analyzer.Analyze(ast);
        new IrGenerator(diag, analyzer.Symbols).Generate(ast);
        return diag.Diagnostics.Where(d => d.Severity == DiagnosticSeverity.Error).Select(d => d.Message);
    }

    // ==== C1: 大域の CONST を隠す変数 ====

    [Fact]
    public void StaticVariableShadowingConst_IsReadAsVariable()
    {
        // 静的変数 X は CONST X を隠す。読み出し・条件・添字・演算とも変数 (いままでは CONST の 5 を使っていた)
        var asm = FuncAsm("CONST X = 5; VAR R; ARRAY A0[9]; " +
            "FZ() VAR X; BEGIN X = 0; IF X THEN R = 2; R = A0[X]; R = X * 3; R = X; END; MAIN() BEGIN FZ(); END;", "FZ");
        Assert.Contains(Lines("LD\tA,H", "OR\tL", "JP\tZ,_L0"), asm);       // IF X を消さない
        Assert.Contains(Lines("LD\tHL,(_V_FZ_X)", "ADD\tHL,HL", "LD\tDE,_V_A0"), asm);
        Assert.DoesNotContain("$000F", asm);                                 // X * 3 を 15 にしない
        Assert.DoesNotContain("(_V_A0+10)", asm);                            // A0[X] を A0[5] にしない
        Assert.DoesNotContain("$0005", asm);
    }

    [Theory]
    [InlineData("FX(X) BEGIN R = X + 1; END;", "FX(1)")]                 // 引数
    [InlineData("FX() BEGIN VAR X = 9; R = X + 1; END;", "FX()")]       // 局所変数
    [InlineData("FX() VAR X; BEGIN R = X + 1; END;", "FX()")]           // 静的変数
    public void VariableShadowingConst_IsNotFolded(string func, string call)
    {
        var asm = FuncAsm($"CONST X = 5; VAR R; {func} MAIN() BEGIN {call}; END;", "FX");
        Assert.Contains(Lines("INC\tHL", "LD\t(_V_R),HL"), asm);
        Assert.DoesNotContain("$0006", asm);
    }

    [Fact]
    public void VariableShadowingConst_MulByOneIsNotNormalizedAway()
    {
        // CONST ONE = 1 を隠す引数 ONE で R = Y * ONE は掛け算 (いままでは 1 と見て Y にしていた)
        var asm = FuncAsm("CONST ONE = 1; VAR R, Y; FX(ONE) BEGIN R = Y * ONE; END; MAIN() BEGIN FX(2); END;", "FX");
        Assert.Contains(Lines("LD\tHL,(_V_Y)", "LD\tE,(IY+$6E)", "LD\tD,(IY+$6F)", "CALL\tMULHLDE"), asm);
    }

    [Fact]
    public void FloatConstShadowedByWordVariable_IsWord()
    {
        // FLOAT の CONST を隠す WORD の静的変数は WORD (FLOAT にしない)
        var asm = FuncAsm("CONST X = 1.5; VAR R; FX() VAR X; BEGIN X = 2; R = X + 1; END; MAIN() BEGIN FX(); END;", "FX");
        Assert.DoesNotContain("i16tof24", asm);
        Assert.DoesNotContain("f24", asm);
        Assert.Contains(Lines("LD\tHL,$0002", "LD\t(_V_FX_X),HL", "INC\tHL"), asm);
    }

    [Fact]
    public void WordConstShadowedByFloatVariable_IsFloat()
    {
        var asm = FuncAsm("CONST X = 5; FLOAT F; FX() VAR FLOAT X; BEGIN X = 1.5; F = X + 1; END; MAIN() BEGIN FX(); END;", "FX");
        Assert.Contains(Lines("LD\tA,(_V_FX_X+2)", "LD\tC,A", "LD\tDE,(_V_FX_X)"), asm);
        Assert.Contains("CALL\tf24add", asm);
    }

    [Fact]
    public void CodeBlockConstShadowedByStatic_IsReadAsVariable()
    {
        // CODE の並びの CONST (いままでは番地を読んでいた) を隠す静的変数
        var asm = FuncAsm("CONST X = [1, 2]; VAR R; FX() VAR X; BEGIN X = 0; R = X; END; MAIN() BEGIN FX(); END;", "FX");
        Assert.DoesNotContain("LD\tHL,_X", asm);
        Assert.DoesNotContain("LD\tHL,X\n", asm);
        Assert.Contains("(_V_FX_X)", asm);
    }

    [Fact]
    public void CodeWordItemShadowedByParameter_IsRuntime()
    {
        // CODE(WORD, X) の X が引数なら、実行時のコード (いままでは CONST X の DW $0005)
        var asm = FuncAsm("CONST X = 5; VAR R; FX(X) BEGIN R = 0; CODE(WORD, X); END; MAIN() BEGIN FX(1); END;", "FX");
        Assert.DoesNotContain("$0005", asm);
        Assert.Contains("LD\tL,(IY+$6E)", asm);
    }

    [Fact]
    public void UnshadowedConst_StillFolded()
    {
        var asm = FuncAsm("CONST X = 5; VAR R; FX(Y) BEGIN R = X + 1; CODE(WORD, X); END; MAIN() BEGIN FX(1); END;", "FX");
        Assert.Contains(Lines("LD\tHL,$0006", "LD\t(_V_R),HL"), asm);
        Assert.Contains("DW\t$0005", asm);
    }

    [Theory]
    [InlineData("FX(N) ARRAY A[N]; BEGIN A[1] = 9; END;")]
    [InlineData("FX(X) ARRAY A[0]:X; BEGIN END;")]
    [InlineData("FX(X) ARRAY A[0] = {WORD, X}; BEGIN END;")]
    [InlineData("FX(X) ARRAY A[1] = {WORD, X, 1}; BEGIN END;")]
    [InlineData("FX(X) VAR B:X; BEGIN END;")]
    [InlineData("FX() VAR N; ARRAY A[N]; BEGIN END;")]
    public void VariableInDeclaration_IsError(string func)
    {
        Assert.Contains(Errors($"CONST X = 5; CONST N = 7; {func} MAIN() BEGIN END;"),
            m => m.Contains("cannot be used in a declaration"));
    }

    [Fact]
    public void ConstInDeclaration_StillAccepted()
    {
        Assert.Empty(Errors("CONST N = 7; FX() ARRAY A[N] = {WORD, N, 1}; BEGIN A[1] = 9; END; MAIN() BEGIN END;"));
    }

    [Theory]
    [InlineData("FX(TRUE) BEGIN END;")]
    [InlineData("FX() VAR FALSE; BEGIN END;")]
    [InlineData("FX() BEGIN VAR TRUE; END;")]
    [InlineData("FX() ARRAY TRUE[2]; BEGIN END;")]
    public void TrueFalseAsName_IsError(string func)
    {
        Assert.Contains(Errors($"{func} MAIN() BEGIN END;"), m => m.Contains("cannot be used as a variable or parameter name"));
    }

    // ==== C2: 本体が CODE だけの関数 ====

    [Theory]
    [InlineData("FX() BEGIN CODE($3E, $01); END;")]
    [InlineData("FX()[CODE($3E, $01);]")]
    [InlineData("FX:WORD()[CODE($3E, $01);]")]
    [InlineData("FX(0)[CODE($3E, $01);]")]
    public void ZeroArgumentCodeBody_StaysMachineCodeDefinition(string func)
    {
        // 引数 0・戻り値の型なし (か :WORD) は、いままでどおり MACHINE の CODE の定義 (_FX: に DB と RET)
        var asm = Asm($"VAR R; {func} MAIN() BEGIN R = FX(); END;");
        Assert.Contains("CALL\t_FX", asm);
        Assert.Contains("_FX:\n\tDB\t$3E,$01,$C9", asm);
    }

    [Fact]
    public void ZeroArgumentCodeBody_ConstantItemsAreBytes()
    {
        // MACHINE の CODE の定義の項は、定数をバイトにする (CODE(OP) は DB OP。ふつうの関数の CODE の文とは違う)
        var asm = Asm("CONST OP = $AF; FX() BEGIN CODE(OP, BYTE, OP + 1); END; MAIN() BEGIN FX(); END;");
        Assert.Contains("_FX:\n\tDB\t$AF,$B0,$C9", asm);
    }

    [Theory]
    [InlineData("FX(A) BEGIN CODE($3E, $01); END;", "FX(1)")]
    [InlineData("FX(A, B) [CODE($3E, $01);]", "FX(1, 2)")]
    [InlineData("FX:FLOAT() BEGIN CODE($3E, $01); END;", "FX()")]
    [InlineData("FX:BYTE() BEGIN CODE($3E, $01); END;", "FX()")]
    public void CodeBodyWithParameterNamesOrReturnType_IsError(string func, string call)
    {
        Assert.Contains(Errors($"VAR R; {func} MAIN() BEGIN {call}; END;"), m => m.Contains("MACHINE CODE definition"));
    }

    [Fact]
    public void StatementBeforeCode_IsOrdinaryFunction()
    {
        // CODE の前に文があれば、ふつうの関数 (引数を IY から読める)
        var asm = FuncAsm("VAR R; FX(A) BEGIN R = A; CODE($3E, $01); END; MAIN() BEGIN FX(1); END;", "FX");
        Assert.Contains("LD\tL,(IY+$6E)", asm);
        Assert.Contains("DB\t$3E", asm);
    }
}
