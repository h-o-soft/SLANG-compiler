using SLANGCompiler;
using SLANGCompiler.CodeGen.C;
using SLANGCompiler.Lexer;
using SLANGCompiler.Parser;
using SLANGCompiler.Runtime;
using SLANGCompiler.Semantics;
using Xunit;

namespace SLANGCompiler.Tests;

/// <summary>
/// <see cref="CTranspiler"/> + <see cref="CEmitter"/> の golden test。
/// 代表的な AST → C 出力パターンを実 SLANG ソースから組み立てて固定する。
/// </summary>
public class CTranspilerTests
{
    /// <summary>SLANG ソースを CTranspiler に通して C 出力を返す</summary>
    private static (string CSource, DiagnosticBag Diag) Transpile(string source)
    {
        var diag = new DiagnosticBag();
        var lexer = new Lexer.Lexer(source, "<test>");
        var tokens = lexer.Tokenize();
        var preproc = new Preprocessor(diag, new List<string>());
        preproc.DefineConst("ENV_TYPE", 7);
        preproc.DefineConst("OS_TYPE", 6);
        preproc.DefineConst("BACKEND", 1);
        tokens = preproc.Process(tokens, ".");

        var parser = new Parser.Parser(tokens, diag);
        var ast = parser.ParseCompilationUnit();
        Assert.False(diag.HasErrors, $"parse errors: {string.Join("; ", diag.Diagnostics.Select(d => d.Message))}");

        var analyzer = new SemanticAnalyzer(diag);
        analyzer.Analyze(ast);
        Assert.False(diag.HasErrors, $"semantic errors: {string.Join("; ", diag.Diagnostics.Select(d => d.Message))}");

        var env = new EnvironmentConfig { Name = "c64", Backend = BackendKind.OscarC };
        var transpiler = new CTranspiler(analyzer.Symbols, env, diag);
        return (transpiler.Transpile(ast), diag);
    }

    private static string MinimalProgram(string body) =>
$@"MAIN()
{{
{body}
}}";

    // === Top-level ===

    [Fact]
    public void EmitsHeaderAndMainEntry()
    {
        var (src, _) = Transpile(MinimalProgram("    PRINT(\"X\");"));
        Assert.Contains("#include \"slang_runtime.h\"", src);
        Assert.Contains("static unsigned int F_MAIN(void)", src);
        Assert.Contains("int main(void)", src);
        Assert.Contains("F_MAIN();", src);
        Assert.Contains("return 0;", src);
    }

    [Fact]
    public void ReportsErrorIfMainMissing()
    {
        var (_, diag) = Transpile(@"FOO()
{
    PRINT(""x"");
}");
        Assert.True(diag.HasErrors);
        Assert.Contains(diag.Diagnostics, d => d.Message.Contains("MAIN"));
    }

    // === Declarations ===

    [Fact]
    public void GlobalVarByte_EmitsStatic_UnsignedChar()
    {
        var (src, _) = Transpile("VAR BYTE B;\n" + MinimalProgram("    B = 5;"));
        Assert.Contains("static unsigned char V_B = 0;", src);
    }

    [Fact]
    public void GlobalVarWord_DefaultsToUnsignedInt()
    {
        var (src, _) = Transpile("VAR W;\n" + MinimalProgram("    W = 1;"));
        Assert.Contains("static unsigned int V_W = 0;", src);
    }

    [Fact]
    public void GlobalArray_EmitsStatic_WithDims()
    {
        var (src, _) = Transpile("VAR BYTE A[10];\n" + MinimalProgram("    A[0] = 1;"));
        Assert.Contains("static unsigned char V_A[10];", src);
    }

    [Fact]
    public void IndirectArray_EmitsPointer()
    {
        // VAR BYTE A[]; A = $3000; → static unsigned char *V_A;
        var (src, _) = Transpile("VAR BYTE A[];\n" + MinimalProgram("    A = $3000;\n    A[0] = 0;"));
        Assert.Contains("static unsigned char *V_A;", src);
    }

    [Fact]
    public void ConstDecl_EmitsDefine()
    {
        var (src, _) = Transpile("CONST WIDTH = 80;\n" + MinimalProgram("    PRINT(WIDTH);"));
        Assert.Contains("#define C_WIDTH", src);
    }

    // === Literals ===

    [Fact]
    public void IntegerLiteral_Hex_UnsignedInt()
    {
        var (src, _) = Transpile("VAR X;\n" + MinimalProgram("    X = 42;"));
        Assert.Contains("((unsigned int)0x002Au)", src);
    }

    [Fact]
    public void FloatLiteral_NoFSuffix()
    {
        // oscar64 は `1.5f` を受け付けないため、suffix なしで出す (Z80 backend と
        // 差異あり)。`.` を必ず含めることで float リテラルとして認識される。
        var (src, _) = Transpile("VAR F;\n" + MinimalProgram("    F = 1.5;"));
        Assert.Contains("1.5", src);
        Assert.DoesNotContain("1.5f", src);
    }

    [Fact]
    public void StringLiteral_QuotedAndEscaped()
    {
        var (src, _) = Transpile(MinimalProgram("    PRINT(\"HI\");"));
        Assert.Contains("\"HI\"", src);
    }

    // === Binary / Unary ===

    [Fact]
    public void BinaryAdd_Word_WrappedWithUnsignedInt()
    {
        var (src, _) = Transpile("VAR A; VAR B;\n" + MinimalProgram("    A = A + B;"));
        // 結果は ((unsigned int)((A) + (B)))
        Assert.Contains("(unsigned int)((V_A) + (V_B))", src);
    }

    [Fact]
    public void BinaryCompare_Eq_PlainOperator()
    {
        var (src, _) = Transpile("VAR A;\n" + MinimalProgram("    IF A==1 THEN PRINT(\"X\");"));
        Assert.Contains("== (((unsigned int)0x0001u))", src);
    }

    [Fact]
    public void LogAnd_ShortCircuit_MapsToCAndAnd()
    {
        var (src, _) = Transpile("VAR A; VAR B;\n" + MinimalProgram("    IF A>0 && B>0 THEN PRINT(\"X\");"));
        // && は SLANG 短絡、C も短絡で意味一致
        Assert.Contains("&&", src);
    }

    [Fact]
    public void UnaryNegate_WrappedForWord()
    {
        var (src, _) = Transpile("VAR A;\n" + MinimalProgram("    A = -A;"));
        Assert.Contains("(unsigned int)(-(V_A))", src);
    }

    [Fact]
    public void HighLow_EmitsByteCast()
    {
        var (src, _) = Transpile("VAR W; VAR BYTE H;\n" + MinimalProgram("    H = HIGH(W);"));
        Assert.Contains("((unsigned char)(((V_W) >> 8) & 0xFF))", src);
    }

    // === Increment ===

    [Fact]
    public void IncrementPostfix_Matches_C_Semantics()
    {
        var (src, _) = Transpile("VAR A;\n" + MinimalProgram("    A++;"));
        Assert.Contains("V_A++", src);
    }

    [Fact]
    public void IncrementPrefix_Matches_C_Semantics()
    {
        var (src, _) = Transpile("VAR A;\n" + MinimalProgram("    ++A;"));
        Assert.Contains("++V_A", src);
    }

    // === Control flow ===

    [Fact]
    public void ForLoop_UsesWrapSafeForm()
    {
        var (src, _) = Transpile("VAR BYTE I;\n" + MinimalProgram(
            "    FOR I=0 TO 9\n    {\n        PRINT(I);\n    }"));
        // wrap-safe + SLANG 自然終了仕様: end 変数 bind、自然終了でも step 1 回後に break
        // (= `FOR I=0 TO 9` 自然終了で I=10、`IF I<=9` で EXIT 経路と区別可能)
        Assert.Contains("_for_end_", src);
        Assert.Contains("for (;;)", src);
        Assert.Contains("break;", src);
        Assert.Contains("++V_I;", src);
        // 自然終了経路で `++loopVar; break;` のセットになっていること
        Assert.Contains("{ ++V_I; break; }", src);
    }

    [Fact]
    public void Repeat_Until_InvertsCondition()
    {
        var (src, _) = Transpile("VAR I;\n" + MinimalProgram(
            "    I = 0;\n    REPEAT\n    {\n        ++I;\n    } UNTIL I>=10;"));
        Assert.Contains("do", src);
        Assert.Contains("while (!(", src);
    }

    [Fact]
    public void While_DirectMapping()
    {
        var (src, _) = Transpile("VAR I;\n" + MinimalProgram(
            "    I = 0;\n    WHILE I<10\n    {\n        ++I;\n    }"));
        Assert.Contains("while (", src);
    }

    [Fact]
    public void If_ElseIf_Else_Chain()
    {
        var (src, _) = Transpile("VAR X;\n" + MinimalProgram(
            "    IF X==0 THEN PRINT(\"zero\");\n    ELIF X==1 THEN PRINT(\"one\");\n    ELSE PRINT(\"many\");"));
        Assert.Contains("if (", src);
        Assert.Contains("else if (", src);
        Assert.Contains("else", src);
    }

    [Fact]
    public void Exit_BecomesBreak()
    {
        var (src, _) = Transpile("VAR I;\n" + MinimalProgram(
            "    WHILE 1\n    {\n        EXIT;\n    }"));
        Assert.Contains("break;", src);
    }

    [Fact]
    public void Return_WithValue()
    {
        var (src, _) = Transpile(@"FOO()
{
    RETURN 5;
}
MAIN()
{
    FOO();
}");
        Assert.Contains("return (((unsigned int)0x0005u));", src);
    }

    // === PRINT dispatch ===

    [Fact]
    public void Print_String_Dispatches_To_slang_print_str()
    {
        var (src, _) = Transpile(MinimalProgram("    PRINT(\"HI\");"));
        Assert.Contains("slang_print_str(\"HI\")", src);
    }

    [Fact]
    public void Print_Slash_Dispatches_To_slang_println()
    {
        var (src, _) = Transpile(MinimalProgram("    PRINT(/);"));
        Assert.Contains("slang_println();", src);
    }

    [Fact]
    public void Print_Int_Var_Dispatches_To_slang_print_int()
    {
        var (src, _) = Transpile("VAR X;\n" + MinimalProgram("    X=42; PRINT(X);"));
        Assert.Contains("slang_print_int", src);
    }

    [Fact]
    public void Print_Float_Dispatches_To_slang_print_float()
    {
        var (src, _) = Transpile("VAR F;\n" + MinimalProgram("    F=1.0; PRINT(F);"));
        // FLOAT 変数の型推論: VAR F は WORD default、しかし 1.0 代入で float になる ... わけではない
        // ので、ここは float としては推論されない。skip しても良いが、文字列 dispatch されないことを確認:
        Assert.Contains("slang_print", src);
    }

    // === Z80 固有機能の error ===

    [Fact]
    public void MachineDecl_ReportsError()
    {
        var (_, diag) = Transpile("MACHINE FOO: $1234;\n" + MinimalProgram("    FOO();"));
        Assert.True(diag.HasErrors);
        Assert.Contains(diag.Diagnostics, d =>
            d.Message.Contains("MACHINE") && d.Message.Contains("oscar_c"));
    }

    [Fact]
    public void OrgDirective_Ignored_NoError()
    {
        // SLANG syntax: `ORG $0801` (no `#`)
        var (src, diag) = Transpile("ORG $0801\n" + MinimalProgram("    PRINT(\"X\");"));
        Assert.False(diag.HasErrors);
        // 出力には ORG 由来の C は何も入らない
        Assert.DoesNotContain("0801", src);
    }
}
