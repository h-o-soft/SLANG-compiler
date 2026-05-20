using SLANGCompiler;
using SLANGCompiler.CodeGen.C;
using SLANGCompiler.Lexer;
using SLANGCompiler.Parser;
using SLANGCompiler.Runtime;
using SLANGCompiler.Semantics;
using Xunit;

namespace SLANGCompiler.Tests;

/// <summary>
/// v3b-D oscar_c backend での `ARRAY BYTE NAME[N] = { 値, %値, ... }` 初期化
/// 展開を golden 化する。BYTE/WORD 混在 (= `%` prefix で WORD) を LE byte 列に
/// 展開する。 FLOAT / ARRAY WORD / ARRAY FLOAT / 非定数 / StringLiteral は scope
/// 外で error になることも確認。
/// </summary>
public class ArrayInitOscarCTests
{
    private static EnvironmentConfig MakeC64Env()
    {
        return new EnvironmentConfig
        {
            Name = "c64",
            Backend = BackendKind.OscarC,
            OutputFormat = "c_source",
            CBindings = new List<CBindingDef>(),
        };
    }

    private static string TranspileWithEnv(string source, EnvironmentConfig env, out DiagnosticBag diag)
    {
        diag = new DiagnosticBag();
        var lexer = new Lexer.Lexer(source, "<test>");
        var tokens = lexer.Tokenize();
        var preproc = new Preprocessor(diag, new List<string>());
        preproc.DefineConst("BACKEND", 1);
        preproc.DefineConst("ENV_TYPE", 7);
        tokens = preproc.Process(tokens, ".");
        var parser = new Parser.Parser(tokens, diag);
        var ast = parser.ParseCompilationUnit();
        var analyzer = new SemanticAnalyzer(diag);
        analyzer.Analyze(ast);
        if (diag.HasErrors) return "";
        var transpiler = new CTranspiler(analyzer.Symbols, env, diag);
        return transpiler.Transpile(ast);
    }

    [Fact]
    public void GlobalArrayByte_AllByteValues_EmitsCArrayInit()
    {
        // 全 BYTE 要素の ARRAY BYTE init = 各値が 1 byte の hex literal で出る
        var src = TranspileWithEnv("""
            ARRAY BYTE DATA[3] = { 1, 2, 3, 4 };
            MAIN() { PRINT(DATA[0]); }
            """, MakeC64Env(), out var diag);

        Assert.False(diag.HasErrors,
            $"errors: {string.Join("; ", diag.Diagnostics.Select(d => d.Message))}");

        // ARRAY BYTE A[3] は SLANG 仕様で 4 要素確保 (= index 0..3)
        Assert.Contains("static unsigned char V_DATA[4] = {0x01, 0x02, 0x03, 0x04};", src);
    }

    [Fact]
    public void GlobalArrayByte_WordPrefixMix_ExpandsAsLittleEndian()
    {
        // `%` prefix の WORD を LE 2 byte に展開、BYTE 要素と混在可能
        // SIDFX struct のような C struct を SLANG ARRAY BYTE で書ける流儀の検証
        var src = TranspileWithEnv("""
            ARRAY BYTE MIXED[5] = { %$1234, $56, %$7890, $AB };
            MAIN() { PRINT(MIXED[0]); }
            """, MakeC64Env(), out var diag);

        Assert.False(diag.HasErrors,
            $"errors: {string.Join("; ", diag.Diagnostics.Select(d => d.Message))}");

        // %$1234 → 0x34, 0x12 (LE) / $56 / %$7890 → 0x90, 0x78 (LE) / $AB の 6 byte
        // ARRAY BYTE A[5] は 6 要素確保 + 6 byte 初期値 = ぴったり
        Assert.Contains("static unsigned char V_MIXED[6] = {0x34, 0x12, 0x56, 0x90, 0x78, 0xAB};", src);
    }

    [Fact]
    public void GlobalArrayByte_ConstReference_EvaluatesAtCompileTime()
    {
        // ARRAY init 内に CONST 参照 + OR 式があっても ConstEvaluator が静的に評価
        var src = TranspileWithEnv("""
            CONST FLAG_A = $01;
            CONST FLAG_B = $80;
            ARRAY BYTE FLAGS[2] = { FLAG_A, FLAG_B, FLAG_A OR FLAG_B };
            MAIN() { PRINT(FLAGS[0]); }
            """, MakeC64Env(), out var diag);

        Assert.False(diag.HasErrors,
            $"errors: {string.Join("; ", diag.Diagnostics.Select(d => d.Message))}");

        // FLAG_A=$01, FLAG_B=$80, OR=$81 → 0x01, 0x80, 0x81 (3 byte)
        // ARRAY BYTE A[2] は 3 要素確保
        Assert.Contains("static unsigned char V_FLAGS[3] = {0x01, 0x80, 0x81};", src);
    }

    [Fact]
    public void LocalStaticArrayByte_InitCode_EmitsCArrayInit()
    {
        // 関数内 static (BEGIN 前) ARRAY BYTE 宣言の InitialCode も同様に展開
        var src = TranspileWithEnv("""
            MAIN()
                ARRAY BYTE LOCAL[2] = { %$ABCD, $EF };
            BEGIN
                PRINT(LOCAL[0]);
            END;
            """, MakeC64Env(), out var diag);

        Assert.False(diag.HasErrors,
            $"errors: {string.Join("; ", diag.Diagnostics.Select(d => d.Message))}");

        // %$ABCD → 0xCD, 0xAB / $EF の 3 byte、ARRAY BYTE A[2] = 3 要素確保
        Assert.Contains("static unsigned char V_MAIN_LOCAL[3] = {0xCD, 0xAB, 0xEF};", src);
    }

    [Fact]
    public void ArrayFloat_InitCode_StillRejected()
    {
        // v3b-D scope: ARRAY BYTE 限定。ARRAY FLOAT の InitialCode は明示 error
        var src = TranspileWithEnv("""
            ARRAY FLOAT FA[2] = { 1.0, 2.0, 3.0 };
            MAIN() { PRINT(FA[0]); }
            """, MakeC64Env(), out var diag);

        Assert.True(diag.HasErrors, "ARRAY FLOAT init は v3b-D scope 外として error 期待");
        Assert.Contains(diag.Diagnostics,
            d => d.Message.Contains("ARRAY BYTE", StringComparison.OrdinalIgnoreCase));
    }
}
