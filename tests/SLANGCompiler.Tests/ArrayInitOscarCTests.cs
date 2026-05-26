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

    // === v3b-E (Issue #194) first PR: ARRAY WORD InitialCode 対応 ===
    // SLANG 仕様の「`= { ... }` は CODE byte stream」解釈を維持しつつ
    // C 型整合のため WORD literal に grouping して emit する。
    // 容量までの 0 fill は helper では行わず C implicit zero fill に委譲。

    [Fact]
    public void GlobalArrayWord_AllWordValues_EmitsCArrayInit()
    {
        // 全 %prefix WORD 要素: 各 2 byte LE → そのまま WORD literal に grouping
        var src = TranspileWithEnv("""
            ARRAY WORD W[2] = { %$1234, %$5678, %$9ABC };
            MAIN() { PRINT(W[0]); }
            """, MakeC64Env(), out var diag);

        Assert.False(diag.HasErrors,
            $"errors: {string.Join("; ", diag.Diagnostics.Select(d => d.Message))}");
        // ARRAY WORD W[2] は 3 要素確保 (= index 0..2)、 init 3 WORD = 6 byte で ぴったり
        Assert.Contains("static unsigned int V_W[3] = {0x1234, 0x5678, 0x9ABC};", src);
    }

    [Fact]
    public void GlobalArrayWord_DefaultByteItems_GroupedAsLE()
    {
        // default 1 byte item を 2 byte ずつ LE で grouping して WORD literal 化。
        // byte stream [0xAB, 0xCD, 0xEF, 0xFF] → WORD[2] = {0xCDAB, 0xFFEF}
        var src = TranspileWithEnv("""
            ARRAY WORD W[2] = { $AB, $CD, $EF, $FF };
            MAIN() { PRINT(W[0]); }
            """, MakeC64Env(), out var diag);

        Assert.False(diag.HasErrors,
            $"errors: {string.Join("; ", diag.Diagnostics.Select(d => d.Message))}");
        // 容量 3 WORD = 6 byte に対し init 4 byte = 2 WORD、 残り 1 WORD は C implicit 0 fill
        Assert.Contains("static unsigned int V_W[3] = {0xCDAB, 0xFFEF};", src);
    }

    [Fact]
    public void GlobalArrayWord_OddByteCount_PadsLastByteWithZero()
    {
        // 奇数 byte stream の場合は末尾を 0 padding して偶数化 (= WORD grouping のため)。
        // [0x34, 0x12, 0xAB] → +1 padding → [0x34, 0x12, 0xAB, 0x00] = WORD[2] = {0x1234, 0x00AB}
        var src = TranspileWithEnv("""
            ARRAY WORD W[1] = { %$1234, $AB };
            MAIN() { PRINT(W[0]); }
            """, MakeC64Env(), out var diag);

        Assert.False(diag.HasErrors,
            $"errors: {string.Join("; ", diag.Diagnostics.Select(d => d.Message))}");
        // ARRAY WORD W[1] は 2 要素確保、 init 2 WORD = ぴったり
        Assert.Contains("static unsigned int V_W[2] = {0x1234, 0x00AB};", src);
    }

    [Fact]
    public void GlobalArrayWord_Underfilled_ImplicitlyZeroFilledByC()
    {
        // 容量に満たない init は helper では 0 fill せず C implicit zero fill に委譲。
        // (= 既存 BYTE UnderfilledArrayByte_InitCode_Accepted_RestZeroFilledByC と同じ流儀)
        var src = TranspileWithEnv("""
            ARRAY WORD W[3] = { %$0001 };
            MAIN() { PRINT(W[0]); }
            """, MakeC64Env(), out var diag);

        Assert.False(diag.HasErrors,
            $"errors: {string.Join("; ", diag.Diagnostics.Select(d => d.Message))}");
        // ARRAY WORD W[3] は 4 要素確保、 init 1 WORD のみ書き残り 3 WORD は C が 0 埋め
        Assert.Contains("static unsigned int V_W[4] = {0x0001};", src);
    }

    [Fact]
    public void UnsizedArrayWord_InitCode_DerivesCElementCount()
    {
        // 添字省略 + InitialCode: C array 長は CElementCount (= byte 数 / 2)、
        // byte 数を直接使うと WORD で 2 倍になる事故が起きる (= Codex review High 指摘)。
        var src = TranspileWithEnv("""
            ARRAY WORD P[] = { %$1234, %$5678 };
            MAIN() { PRINT(P[0]); }
            """, MakeC64Env(), out var diag);

        Assert.False(diag.HasErrors,
            $"errors: {string.Join("; ", diag.Diagnostics.Select(d => d.Message))}");
        // byte 数 4 → CElementCount=2、 C array V_P[2]
        Assert.Contains("static unsigned int V_P[2] = {0x1234, 0x5678};", src);
        // 旧 byte-count 流用で V_P[4] が出ないこと (= Codex High 指摘の回帰防止)
        Assert.DoesNotContain("V_P[4]", src);
    }

    [Fact]
    public void UnsizedArrayWord_OddByteCount_PadsAndDerivesCElementCount()
    {
        // 添字省略で source byte=1 (奇数) → +1 padding → emitted byte=2、 CElementCount=1
        var src = TranspileWithEnv("""
            ARRAY WORD P[] = { $AB };
            MAIN() { PRINT(P[0]); }
            """, MakeC64Env(), out var diag);

        Assert.False(diag.HasErrors,
            $"errors: {string.Join("; ", diag.Diagnostics.Select(d => d.Message))}");
        Assert.Contains("static unsigned int V_P[1] = {0x00AB};", src);
    }

    [Fact]
    public void UnsizedArrayWord_AssignmentRaisesError()
    {
        // 添字省略 + InitialCode の ARRAY WORD も _unsizedArraysWithInit で tracking、
        // 代入は reject (= BYTE 配列と同様の guard が WORD でも効くこと)。
        var src = TranspileWithEnv("""
            ARRAY WORD P[] = { %$1234 };
            MAIN()
            BEGIN
                P = $3000;
            END;
            """, MakeC64Env(), out var diag);

        Assert.True(diag.HasErrors, "unsized ARRAY WORD + InitialCode の代入も error 期待");
        Assert.Contains(diag.Diagnostics,
            d => d.Message.Contains("ARRAY", StringComparison.OrdinalIgnoreCase)
              && d.Message.Contains("assign", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void LocalStaticArrayWord_InitCode_EmitsCArrayInit()
    {
        // 関数内 static ARRAY WORD の InitialCode も同じ logic で展開
        var src = TranspileWithEnv("""
            MAIN()
                ARRAY WORD W[2] = { %$1234, %$5678 };
            BEGIN
                PRINT(W[0]);
            END;
            """, MakeC64Env(), out var diag);

        Assert.False(diag.HasErrors,
            $"errors: {string.Join("; ", diag.Diagnostics.Select(d => d.Message))}");
        // ARRAY WORD W[2] は 3 要素確保、 init 2 WORD で残り 1 WORD は C implicit fill
        Assert.Contains("static unsigned int V_MAIN_W[3] = {0x1234, 0x5678};", src);
    }

    // === v3b-E (Issue #194) (3a): StringLiteral 単独 in ARRAY BYTE 対応 ===
    // oscar64 -psci で C string literal の PETSCII 自動変換を活かすため、
    // SLANG 仕様の SJIS byte 列 (= Z80 既存挙動) ではなく C string literal を
    // そのまま emit する (= 意図的 backend gap)。 mixed / WORD / FLOAT は scope 外。

    [Fact]
    public void UnsizedArrayByte_StringLiteralSingle_EmitsCStringWithNul()
    {
        // 添字省略 + 単独 StringLiteral: C 配列長 = SJIS bytes + NUL 1 byte
        var src = TranspileWithEnv("""
            ARRAY BYTE MSG[] = { "hello" };
            MAIN() { PRINT(MSG[0]); }
            """, MakeC64Env(), out var diag);

        Assert.False(diag.HasErrors,
            $"errors: {string.Join("; ", diag.Diagnostics.Select(d => d.Message))}");
        // hex literal 列ではなく C string literal で emit (= oscar64 -psci で PETSCII 化)
        Assert.Contains("static unsigned char V_MSG[6] = \"hello\";", src);
    }

    [Fact]
    public void FixedArrayByte_StringLiteralExactCapacity_EmitsCString()
    {
        // ARRAY BYTE M[5] は 6 要素確保 = "hello" (5 char) + NUL でぴったり
        var src = TranspileWithEnv("""
            ARRAY BYTE MSG[5] = { "hello" };
            MAIN() { PRINT(MSG[0]); }
            """, MakeC64Env(), out var diag);

        Assert.False(diag.HasErrors,
            $"errors: {string.Join("; ", diag.Diagnostics.Select(d => d.Message))}");
        Assert.Contains("static unsigned char V_MSG[6] = \"hello\";", src);
    }

    [Fact]
    public void FixedArrayByte_StringLiteralUnderfilled_ImplicitZeroFillByC()
    {
        // ARRAY BYTE M[9] は 10 要素確保、 "hi" (2 char) + NUL = 3 byte 書き、
        // 残り 7 byte は C implicit zero fill (= 既存 underfilled BYTE test と整合)
        var src = TranspileWithEnv("""
            ARRAY BYTE MSG[9] = { "hi" };
            MAIN() { PRINT(MSG[0]); }
            """, MakeC64Env(), out var diag);

        Assert.False(diag.HasErrors,
            $"errors: {string.Join("; ", diag.Diagnostics.Select(d => d.Message))}");
        Assert.Contains("static unsigned char V_MSG[10] = \"hi\";", src);
    }

    [Fact]
    public void FixedArrayByte_StringLiteralOverflow_SemanticErrorByCapacity()
    {
        // ARRAY BYTE M[1] は 2 要素確保、 "hello" SJIS 5 byte は超過 → semantic error
        // (= helper の StringLiteral path 自体は通すが ArrayInitialCodeSizer で先 reject)
        var src = TranspileWithEnv("""
            ARRAY BYTE MSG[1] = { "hello" };
            MAIN() { PRINT(MSG[0]); }
            """, MakeC64Env(), out var diag);

        Assert.True(diag.HasErrors, "StringLiteral 容量超過は SLANG semantic で reject");
        Assert.Contains(diag.Diagnostics,
            d => d.Message.Contains("capacity", StringComparison.OrdinalIgnoreCase)
              || d.Message.Contains("多すぎ", StringComparison.Ordinal));
    }

    [Fact]
    public void ArrayByte_StringLiteralMixedWithOther_RaisesError()
    {
        // mixed (= "hi" + 数値要素) は v3b-E first PR scope 外として error
        var src = TranspileWithEnv("""
            ARRAY BYTE MSG[] = { "hi", 0 };
            MAIN() { PRINT(MSG[0]); }
            """, MakeC64Env(), out var diag);

        Assert.True(diag.HasErrors, "StringLiteral と数値の mixed は scope 外 error");
        Assert.Contains(diag.Diagnostics,
            d => d.Message.Contains("StringLiteral", StringComparison.OrdinalIgnoreCase)
              && d.Message.Contains("mixed", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void ArrayWord_StringLiteralSingle_RaisesError()
    {
        // WORD 配列への StringLiteral は意味曖昧 = scope 外 error
        var src = TranspileWithEnv("""
            ARRAY WORD W[] = { "hi" };
            MAIN() { PRINT(W[0]); }
            """, MakeC64Env(), out var diag);

        Assert.True(diag.HasErrors, "ARRAY WORD への StringLiteral は scope 外 error");
        Assert.Contains(diag.Diagnostics,
            d => d.Message.Contains("StringLiteral", StringComparison.OrdinalIgnoreCase)
              && d.Message.Contains("ARRAY BYTE", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void LocalStaticArrayByte_StringLiteralSingle_EmitsCString()
    {
        // 関数内 static + 添字省略 + StringLiteral
        var src = TranspileWithEnv("""
            MAIN()
                ARRAY BYTE MSG[] = { "world" };
            BEGIN
                PRINT(MSG[0]);
            END;
            """, MakeC64Env(), out var diag);

        Assert.False(diag.HasErrors,
            $"errors: {string.Join("; ", diag.Diagnostics.Select(d => d.Message))}");
        Assert.Contains("static unsigned char V_MAIN_MSG[6] = \"world\";", src);
    }

    [Fact]
    public void ArrayByte_StringLiteral_EscapesQuoteAndBackslash()
    {
        // C string literal 出力で `"` `\` 等が escape されること (= CStringEncoder 経由)
        var src = TranspileWithEnv("""
            ARRAY BYTE MSG[] = { "a\"b\\c" };
            MAIN() { PRINT(MSG[0]); }
            """, MakeC64Env(), out var diag);

        Assert.False(diag.HasErrors,
            $"errors: {string.Join("; ", diag.Diagnostics.Select(d => d.Message))}");
        // SLANG `a"b\c` の 5 char + NUL = 6 byte、 C 出力で `"` `\` を escape
        Assert.Contains("static unsigned char V_MSG[6] = \"a\\\"b\\\\c\";", src);
    }

    [Fact]
    public void FixedArrayByte_StringLiteralExactSjisCapacity_RaisesNulFitError()
    {
        // ARRAY BYTE MSG[4] (= 容量 5 byte) に "hello" (SJIS 5 byte) は semantic では
        // ぴったり pass するが、 C string literal は NUL 含めて 6 byte 必要 →
        // NUL 終端が容量に入らず backend error (= Codex review Medium 指摘 (1) 対応、
        // C 上は NUL 落ちで compile 通るが SLANG 期待の終端保証が壊れる)
        var src = TranspileWithEnv("""
            ARRAY BYTE MSG[4] = { "hello" };
            MAIN() { PRINT(MSG[0]); }
            """, MakeC64Env(), out var diag);

        Assert.True(diag.HasErrors, "固定長で NUL 終端が容量に入らない StringLiteral は backend reject");
        Assert.Contains(diag.Diagnostics,
            d => d.Message.Contains("NUL", StringComparison.Ordinal)
              && d.Message.Contains("容量", StringComparison.Ordinal));
    }

    [Fact]
    public void LocalStaticArrayByte_StringLiteralExactSjisCapacity_RaisesNulFitError()
    {
        // 関数内 static 経路も同じ NUL 終端 check が効くこと
        var src = TranspileWithEnv("""
            MAIN()
                ARRAY BYTE MSG[4] = { "world" };
            BEGIN
                PRINT(MSG[0]);
            END;
            """, MakeC64Env(), out var diag);

        Assert.True(diag.HasErrors, "関数内 static でも NUL 終端 check");
        Assert.Contains(diag.Diagnostics,
            d => d.Message.Contains("NUL", StringComparison.Ordinal)
              && d.Message.Contains("容量", StringComparison.Ordinal));
    }

    [Fact]
    public void ArrayByte_StringLiteralNonAscii_RaisesError()
    {
        // 高位 byte (= "あ" 等) は SJIS bytes と char 数が一致せず、 C 配列長 / 内容が
        // 壊れる + CStringEncoder の `\xNN` escape が後続 hex digit を食う問題もある
        // (= Codex review Medium 指摘 (2) 対応)。 ASCII printable 限定スコープで reject。
        var src = TranspileWithEnv("""
            ARRAY BYTE MSG[] = { "あ" };
            MAIN() { PRINT(MSG[0]); }
            """, MakeC64Env(), out var diag);

        Assert.True(diag.HasErrors, "非 ASCII char は v3b-E (3a) scope 外");
        Assert.Contains(diag.Diagnostics,
            d => d.Message.Contains("non-ASCII", StringComparison.OrdinalIgnoreCase)
              || d.Message.Contains("ASCII printable", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void ArrayByte_StringLiteralControlChar_RaisesError()
    {
        // 制御文字 + 後続 hex digit (= "\x01A") は C `\xNN` escape が後続を食って
        // 0x1A になる仕様の罠 (= Codex review Low 指摘 (3) 対応)。 制御文字も reject。
        var src = TranspileWithEnv("""
            ARRAY BYTE MSG[] = { "\x01A" };
            MAIN() { PRINT(MSG[0]); }
            """, MakeC64Env(), out var diag);

        Assert.True(diag.HasErrors, "0x01 等の表示不可制御文字は v3b-E (3a) scope 外");
        Assert.Contains(diag.Diagnostics,
            d => d.Message.Contains("non-printable", StringComparison.OrdinalIgnoreCase)
              || d.Message.Contains("ASCII printable", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void ArrayByte_StringLiteralAllowedEscapes_Accepted()
    {
        // 既定 escape (= `\n` `\r` `\t` `\0`) は scope 内で許可、 通過すること。
        // SLANG parser は `\n` (改行) を CR (0x0D) として解釈する仕様 (= microcomputer
        // 文化、 既存 SLANG 動作)、 そのため C 出力では `\r` escape になる。
        var src = TranspileWithEnv("""
            ARRAY BYTE MSG[] = { "hi\n" };
            MAIN() { PRINT(MSG[0]); }
            """, MakeC64Env(), out var diag);

        Assert.False(diag.HasErrors,
            $"errors: {string.Join("; ", diag.Diagnostics.Select(d => d.Message))}");
        // SLANG `"hi\n"` (= 3 char with 改行=CR) + NUL = 4 byte、 C 出力で `\r`
        Assert.Contains("static unsigned char V_MSG[4] = \"hi\\r\";", src);
    }
}
