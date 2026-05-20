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

    [Fact]
    public void UnsizedArrayByte_InitCode_DerivesSizeFromInit()
    {
        // SLANG 仕様: 添字省略 `ARRAY BYTE A[]={...}` は初期値 byte 列長で配列サイズ決定
        // (= 「添字省略時はチェックしない」)。pointer 化せず固定配列として emit。
        // Codex review High 指摘 (= 旧実装で initializer silently dropped されてた) の回帰防止。
        var src = TranspileWithEnv("""
            ARRAY BYTE UNSIZED[] = { %$1234, $56 };
            MAIN() { PRINT(UNSIZED[0]); }
            """, MakeC64Env(), out var diag);

        Assert.False(diag.HasErrors,
            $"errors: {string.Join("; ", diag.Diagnostics.Select(d => d.Message))}");
        // %$1234 (= 2 byte LE) + $56 (= 1 byte) = 3 byte、ポインタ宣言ではなく固定配列
        Assert.Contains("static unsigned char V_UNSIZED[3] = {0x34, 0x12, 0x56};", src);
        // 旧実装で silently dropped されていた pointer 宣言は出ないこと
        Assert.DoesNotContain("static unsigned char *V_UNSIZED;", src);
    }

    [Fact]
    public void OverflowArrayByte_InitCode_RaisesError()
    {
        // SLANG 仕様: 「初期値が多すぎる場合はエラー」。
        // ARRAY BYTE A[1] は 2 要素確保 (= index 0..1)、 init 4 byte は超過 → error。
        // Codex review Medium 指摘 (= 旧実装で容量超過 check なしに無効 C 出力) の回帰防止。
        var src = TranspileWithEnv("""
            ARRAY BYTE OVER[1] = { 1, 2, 3, 4 };
            MAIN() { PRINT(OVER[0]); }
            """, MakeC64Env(), out var diag);

        Assert.True(diag.HasErrors, "容量超過 ARRAY BYTE init は SLANG 仕様で error");
        Assert.Contains(diag.Diagnostics,
            d => d.Message.Contains("multi", StringComparison.OrdinalIgnoreCase)
              || d.Message.Contains("capacity", StringComparison.OrdinalIgnoreCase)
              || d.Message.Contains("多すぎ", StringComparison.Ordinal));
    }

    [Fact]
    public void UnderfilledArrayByte_InitCode_Accepted_RestZeroFilledByC()
    {
        // SLANG 仕様: 「初期値が足りない場合は0で埋められる」。
        // ARRAY BYTE A[4] は 5 要素確保、init 2 byte は足りないが error にはならず、
        // C array init の挙動で残り 3 byte は 0 で埋まる (= oscar64 も同様)。
        var src = TranspileWithEnv("""
            ARRAY BYTE UNDER[4] = { 1, 2 };
            MAIN() { PRINT(UNDER[0]); }
            """, MakeC64Env(), out var diag);

        Assert.False(diag.HasErrors,
            $"errors: {string.Join("; ", diag.Diagnostics.Select(d => d.Message))}");
        // 配列宣言は 5 要素、 init は 2 byte だけ書く (= C コンパイラが残り 0 で fill)
        Assert.Contains("static unsigned char V_UNDER[5] = {0x01, 0x02};", src);
    }

    // 注: `ARRAY ...:address = { ... }` (fixed addr + initializer) は SLANG parser が
    // 既に reject する (= `:address = { ... }` の組合せは文法上 parse error)、
    // CEmitter の Address + InitialCode 分岐は文法的に到達不能。 念のため
    // CEmitter 側にも防御 error を残しているが、 unit test の必要なし。

    [Fact]
    public void AssignmentToArrayDecl_RaisesError()
    {
        // SLANG `ARRAY ...` 宣言の symbol への直接代入 (= `A = $3000;`) は
        // 配列実体の置換が SLANG 仕様で意味曖昧。oscar_c backend では C 側で
        // `static unsigned char V_A[N] = ...` という無効 C を出してしまうため、
        // VisitAssignExpr で IsArrayDecl を check して error にする。
        // Codex review Medium 指摘 (= unsized + InitialCode で代入が通ってしまう
        // 件) の修正、固定サイズ ARRAY も同じ理由で reject する。
        var src = TranspileWithEnv("""
            ARRAY BYTE A[10];
            MAIN()
            BEGIN
                A = $3000;
            END;
            """, MakeC64Env(), out var diag);

        Assert.True(diag.HasErrors, "ARRAY 宣言 symbol への代入は error 期待");
        Assert.Contains(diag.Diagnostics,
            d => d.Message.Contains("ARRAY", StringComparison.OrdinalIgnoreCase)
              && d.Message.Contains("assign", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void AssignmentToUnsizedArrayWithInitCode_RaisesError()
    {
        // 添字省略 + InitialCode の ARRAY も IsArrayDecl=true で reject される
        // (= Codex review Medium の主指摘ケース、static unsigned char V_A[3] に
        // V_A = ... の無効 C を出さないこと)。
        var src = TranspileWithEnv("""
            ARRAY BYTE A[] = { 1, 2, 3 };
            MAIN()
            BEGIN
                A = $3000;
            END;
            """, MakeC64Env(), out var diag);

        Assert.True(diag.HasErrors, "unsized + InitialCode の ARRAY 代入も error 期待");
        Assert.Contains(diag.Diagnostics,
            d => d.Message.Contains("ARRAY", StringComparison.OrdinalIgnoreCase)
              && d.Message.Contains("assign", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void AssignmentToVarPointer_StillWorks()
    {
        // VAR BYTE T[]; (= IsArrayDecl=false、ポインタ宣言) への代入は SLANG 仕様で
        // OK。oscar_c backend でも従来通り通る (= regression なし)。
        var src = TranspileWithEnv("""
            VAR BYTE T[];
            MAIN()
            BEGIN
                T = $3000;
            END;
            """, MakeC64Env(), out var diag);

        Assert.False(diag.HasErrors,
            $"errors: {string.Join("; ", diag.Diagnostics.Select(d => d.Message))}");
        Assert.Contains("static unsigned char *V_T;", src);
        Assert.Contains("V_T = ", src);
    }

    [Fact]
    public void AssignmentToFunctionStaticArrayDecl_RaisesError()
    {
        // 関数内 static ARRAY (= MAIN() ARRAY BYTE A[2]; BEGIN ... END;) の symbol
        // も IsArrayDecl=true で reject。global SymbolTable には登録されないので
        // _scope (= CScopeTracker) で ArrayType 判別する必要あり (= Codex review 2nd
        // round Medium 指摘の核心: _globals だけ見てた漏れの fix)。
        var src = TranspileWithEnv("""
            MAIN()
                ARRAY BYTE A[2];
            BEGIN
                A = $3000;
            END;
            """, MakeC64Env(), out var diag);

        Assert.True(diag.HasErrors, "関数内 static ARRAY 代入は error 期待");
        Assert.Contains(diag.Diagnostics,
            d => d.Message.Contains("ARRAY", StringComparison.OrdinalIgnoreCase)
              && d.Message.Contains("assign", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void AssignmentToFunctionStaticUnsizedArrayInit_RaisesError()
    {
        // 関数内 static + unsized + InitialCode の組合せも reject (= 同 Codex 指摘)。
        var src = TranspileWithEnv("""
            MAIN()
                ARRAY BYTE A[] = { 1, 2, 3 };
            BEGIN
                A = $3000;
            END;
            """, MakeC64Env(), out var diag);

        Assert.True(diag.HasErrors, "関数内 static unsized + InitialCode の ARRAY 代入は error 期待");
        Assert.Contains(diag.Diagnostics,
            d => d.Message.Contains("ARRAY", StringComparison.OrdinalIgnoreCase)
              && d.Message.Contains("assign", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void AssignmentToFunctionStaticVarPointer_StillWorks()
    {
        // 関数内 static VAR BYTE T[]; (= IsArrayDecl=false、 PointerType) は引き続き
        // 通る (= regression なし)。
        var src = TranspileWithEnv("""
            MAIN()
                VAR BYTE T[];
            BEGIN
                T = $3000;
            END;
            """, MakeC64Env(), out var diag);

        Assert.False(diag.HasErrors,
            $"errors: {string.Join("; ", diag.Diagnostics.Select(d => d.Message))}");
        Assert.Contains("static unsigned char *V_MAIN_T;", src);
        Assert.Contains("V_MAIN_T = ", src);
    }
}
