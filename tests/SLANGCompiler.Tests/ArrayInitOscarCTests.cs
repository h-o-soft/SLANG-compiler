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

    // (v3b-D 時代の `ArrayFloat_InitCode_StillRejected` は v3b-E (1b) で ARRAY FLOAT
    //  InitialCode 対応により撤回、 後続の `GlobalArrayFloat_*` test 群で置換)

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
    public void ArrayByte_StringLiteralAllowedNewline_Accepted()
    {
        // SLANG `\n` (lexer 解釈で CR=0x0D) は scope 内で許可、 通過すること。
        // SLANG `\r` `\t` `\0` は SLANG lexer 仕様では別 char (= 0x1C / 't' / '0')
        // になり、 `\r` のみ ASCII printable 外で reject 対象 (= scope 外)。
        var src = TranspileWithEnv("""
            ARRAY BYTE MSG[] = { "hi\n" };
            MAIN() { PRINT(MSG[0]); }
            """, MakeC64Env(), out var diag);

        Assert.False(diag.HasErrors,
            $"errors: {string.Join("; ", diag.Diagnostics.Select(d => d.Message))}");
        // SLANG `"hi\n"` (= 3 char with 改行=CR) + NUL = 4 byte、 C 出力で `\r`
        Assert.Contains("static unsigned char V_MSG[4] = \"hi\\r\";", src);
    }

    // === v3b-E (Issue #194) (1b): ARRAY FLOAT InitialCode の oscar_c 対応 ===
    // oscar64 native float32 mapped、 各 element を C float literal で emit する。
    // SLANG semantic は f24 (3 byte/elem) 基準で容量計算するが、 oscar_c は
    // element 数ベースで C 配列確保 (= float32 で 4 byte/elem)、 容量整合は
    // element 数ベースで偶然 OK。 user 指示: oscar 側 float のみ考慮、 f24 layout 無視。

    [Fact]
    public void GlobalArrayFloat_AllFloatLiterals_EmitsCArrayInit()
    {
        // ARRAY FLOAT FA[2] = 3 要素確保、 init 3 element でぴったり
        var src = TranspileWithEnv("""
            ARRAY FLOAT FA[2] = { 1.0, 2.5, 3.14 };
            MAIN() { PRINT(FA[0]); }
            """, MakeC64Env(), out var diag);

        Assert.False(diag.HasErrors,
            $"errors: {string.Join("; ", diag.Diagnostics.Select(d => d.Message))}");
        Assert.Contains("static float V_FA[3] = {1, 2.5, 3.14};", src.Replace("1.0", "1"));
        // 念のため形のみ pin (= 1.0 / 1 どちらの表現でも OK、 oscar64 で float に promote)
        Assert.Contains("static float V_FA[3] =", src);
    }

    [Fact]
    public void GlobalArrayFloat_IntegerPromotion_EmitsAsFloatLiteral()
    {
        // IntegerLiteral element (= `42`) は ConstEvaluator.EvaluateFloat で
        // double 化、 `.0` 補完で float literal `42.0` として emit
        var src = TranspileWithEnv("""
            ARRAY FLOAT FA[2] = { 42, 1.5 };
            MAIN() { PRINT(FA[0]); }
            """, MakeC64Env(), out var diag);

        Assert.False(diag.HasErrors,
            $"errors: {string.Join("; ", diag.Diagnostics.Select(d => d.Message))}");
        Assert.Contains("static float V_FA[3] = {42.0, 1.5};", src);
    }

    [Fact]
    public void GlobalArrayFloat_Underfilled_ImplicitZeroFillByC()
    {
        // ARRAY FLOAT FA[3] = 4 要素確保、 init 1 element、 残り 3 element は C
        // implicit 0 fill (= 既存 BYTE/WORD/String と同じ流儀)
        var src = TranspileWithEnv("""
            ARRAY FLOAT FA[3] = { 1.0 };
            MAIN() { PRINT(FA[0]); }
            """, MakeC64Env(), out var diag);

        Assert.False(diag.HasErrors,
            $"errors: {string.Join("; ", diag.Diagnostics.Select(d => d.Message))}");
        Assert.Contains("static float V_FA[4] = {1.0};", src);
    }

    [Fact]
    public void UnsizedArrayFloat_InitCode_DerivesCElementCount()
    {
        // 添字省略 + InitialCode = init element 数で C 配列長確定
        var src = TranspileWithEnv("""
            ARRAY FLOAT FA[] = { 1.0, 2.0 };
            MAIN() { PRINT(FA[0]); }
            """, MakeC64Env(), out var diag);

        Assert.False(diag.HasErrors,
            $"errors: {string.Join("; ", diag.Diagnostics.Select(d => d.Message))}");
        Assert.Contains("static float V_FA[2] = {1.0, 2.0};", src);
    }

    [Fact]
    public void ArrayFloat_Overflow_SemanticRejectsWithCapacity()
    {
        // ARRAY FLOAT FA[1] = 2 elem = 6 byte 容量、 init 3 elem = 9 byte は超過、
        // ArrayInitialCodeSizer で semantic reject (= byte 単位 6 vs 9 比較)
        var src = TranspileWithEnv("""
            ARRAY FLOAT FA[1] = { 1.0, 2.0, 3.0 };
            MAIN() { PRINT(FA[0]); }
            """, MakeC64Env(), out var diag);

        Assert.True(diag.HasErrors, "ARRAY FLOAT 容量超過は semantic で reject");
        Assert.Contains(diag.Diagnostics,
            d => d.Message.Contains("capacity", StringComparison.OrdinalIgnoreCase)
              || d.Message.Contains("多すぎ", StringComparison.Ordinal));
    }

    [Fact]
    public void ArrayFloat_TopLevelCastExpr_SemanticRejects()
    {
        // ARRAY FLOAT 内の `%expr` (= トップレベル CastExpr) は SLANG 仕様で禁止、
        // ArrayInitialCodeSizer.CalculateFloatArrayBytes で先 reject
        var src = TranspileWithEnv("""
            ARRAY FLOAT FA[2] = { %1.0 };
            MAIN() { PRINT(FA[0]); }
            """, MakeC64Env(), out var diag);

        Assert.True(diag.HasErrors, "FLOAT 配列内のトップレベル CastExpr は semantic 違反");
        Assert.Contains(diag.Diagnostics,
            d => d.Message.Contains("Cast expression not allowed in FLOAT array", StringComparison.Ordinal));
    }

    [Fact]
    public void ArrayFloat_NonConstantElement_OscarCRejectsWithRuntimeWorkaroundHint()
    {
        // 非定数 element (= VAR FLOAT 参照) は oscar64 static initializer 制約で reject、
        // (3b) と同じ「runtime 初期化に書き換え」 hint message
        var src = TranspileWithEnv("""
            VAR FLOAT FV;
            ARRAY FLOAT FA[2] = { FV };
            MAIN() { PRINT(FA[0]); }
            """, MakeC64Env(), out var diag);

        Assert.True(diag.HasErrors, "ARRAY FLOAT で非定数 element は oscar_c で reject");
        Assert.Contains(diag.Diagnostics,
            d => d.Message.Contains("compile-time constant", StringComparison.OrdinalIgnoreCase)
              && d.Message.Contains("oscar64", StringComparison.Ordinal));
    }

    [Fact]
    public void ArrayFloat_SmallValue_EmitsFixedPointNotation()
    {
        // .NET の R format は `0.00001` 等で exponent (= `1E-05`) に逃げるが、
        // oscar64 は exponent notation を float literal として受理しない
        // (= Codex review #199 High 指摘で実機確認、 parse error)。 共通 formatter
        // が exponent を検知して F17 trimEnd fallback で固定小数点表記にすること
        // を pin。
        var src = TranspileWithEnv("""
            ARRAY FLOAT FA[] = { 0.00001 };
            MAIN() { PRINT(FA[0]); }
            """, MakeC64Env(), out var diag);

        Assert.False(diag.HasErrors,
            $"errors: {string.Join("; ", diag.Diagnostics.Select(d => d.Message))}");
        // exponent notation `1E-05` 等は含まれず、 固定小数点 `0.00001` で emit
        Assert.Contains("static float V_FA[1] = {0.00001};", src);
        Assert.DoesNotContain("E-", src);
        Assert.DoesNotContain("e-", src);
    }

    [Fact]
    public void LocalStaticArrayFloat_InitCode_EmitsCArrayInit()
    {
        // 関数内 static ARRAY FLOAT の InitialCode も同じ logic で展開
        var src = TranspileWithEnv("""
            MAIN()
                ARRAY FLOAT FA[2] = { 1.0, 2.0 };
            BEGIN
                PRINT(FA[0]);
            END;
            """, MakeC64Env(), out var diag);

        Assert.False(diag.HasErrors,
            $"errors: {string.Join("; ", diag.Diagnostics.Select(d => d.Message))}");
        Assert.Contains("static float V_MAIN_FA[3] = {1.0, 2.0};", src);
    }

    // === v3b-E (Issue #194) (2): FLOAT prefix in ARRAY BYTE/WORD は parser でも reject ===
    // SLANG `%%` (= FLOAT cast prefix) は parser で非 FLOAT 配列の InitialCode 内では
    // 受理されない (= ArrayInitSemanticTests のコメント L85 で言及済)、 CEmitter の
    // FLOAT prefix reject guard は dead code として保険のみ。 oscar_c は oscar64 native
    // float32 mapped で f24 byte stream 表現を持たないため意味的にも対応不能、
    // ARRAY FLOAT を使う workaround を CEmitter message で促す (= 到達した場合の保険)。

    // === v3b-E (Issue #194) (4): multi-dim 添字省略 + InitialCode ===
    // SLANG `ARRAY BYTE A[][M] = { ... }` を C99 auto dim 推論
    // (= `static T A[][M+1] = {flat init};`) に乗せて emit する。 第 1 次元のみ
    // 省略可、 第 2 次元以降の `[]` は C99 仕様で不可。 ARRAY BYTE / WORD のみ対応
    // (= ARRAY FLOAT multi-dim は scope 外 reject)。

    [Fact]
    public void MultiDimArrayByte_UnsizedFirstDim_EmitsCArrayInit()
    {
        // ARRAY BYTE A[][3] (= 第 2 次元 = 4 element 確保 / 1 行) + 6 byte init
        // → `static unsigned char V_A[][4] = {0x01,...,0x06};` で oscar64 が
        // 第 1 次元 = ceil(6/4) = 2 行を auto 推論
        var src = TranspileWithEnv("""
            ARRAY BYTE A[][3] = { 1, 2, 3, 4, 5, 6 };
            MAIN() { PRINT(A[0][0]); }
            """, MakeC64Env(), out var diag);

        Assert.False(diag.HasErrors,
            $"errors: {string.Join("; ", diag.Diagnostics.Select(d => d.Message))}");
        Assert.Contains("static unsigned char V_A[][4] = {0x01, 0x02, 0x03, 0x04, 0x05, 0x06};", src);
    }

    [Fact]
    public void MultiDimArrayWord_UnsizedFirstDim_EmitsCArrayInit()
    {
        // ARRAY WORD W[][2] (= 第 2 次元 = 3 element / 1 行) + 6 WORD init
        var src = TranspileWithEnv("""
            ARRAY WORD W[][2] = { %$1234, %$5678, %$9ABC, %$DEF0, %$ABCD, %$FFFF };
            MAIN() { PRINT(W[0][0]); }
            """, MakeC64Env(), out var diag);

        Assert.False(diag.HasErrors,
            $"errors: {string.Join("; ", diag.Diagnostics.Select(d => d.Message))}");
        Assert.Contains("static unsigned int V_W[][3] = {0x1234, 0x5678, 0x9ABC, 0xDEF0, 0xABCD, 0xFFFF};", src);
    }

    [Fact]
    public void MultiDimArrayByte_ThreeDim_UnsizedFirstDim_EmitsCArrayInit()
    {
        // 3 次元: ARRAY BYTE A[][2][3] (= 第 2/3 次元 = 3*4=12 element / 1 行)
        var src = TranspileWithEnv("""
            ARRAY BYTE A[][2][3] = { 1, 2, 3, 4, 5, 6, 7, 8, 9, 10, 11, 12 };
            MAIN() { PRINT(A[0][0][0]); }
            """, MakeC64Env(), out var diag);

        Assert.False(diag.HasErrors,
            $"errors: {string.Join("; ", diag.Diagnostics.Select(d => d.Message))}");
        Assert.Contains("static unsigned char V_A[][3][4] = {0x01, 0x02, 0x03, 0x04, 0x05, 0x06, 0x07, 0x08, 0x09, 0x0A, 0x0B, 0x0C};", src);
    }

    [Fact]
    public void MultiDimArrayFloat_UnsizedFirstDim_RaisesError()
    {
        // ARRAY FLOAT multi-dim 添字省略 は本 PR scope 外 (= 別 PR / v3b-E (4-ext) 候補)
        var src = TranspileWithEnv("""
            ARRAY FLOAT FA[][2] = { 1.0, 2.0, 3.0, 4.0, 5.0, 6.0 };
            MAIN() { PRINT(FA[0][0]); }
            """, MakeC64Env(), out var diag);

        Assert.True(diag.HasErrors, "ARRAY FLOAT multi-dim 添字省略は scope 外 error");
        Assert.Contains(diag.Diagnostics,
            d => d.Message.Contains("ARRAY FLOAT", StringComparison.Ordinal)
              && d.Message.Contains("multi-dimensional", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void MultiDimArrayByte_SecondDimAlsoOmitted_RaisesError()
    {
        // 第 2 次元以降の `[]` 省略 は C99 仕様違反、 reject
        var src = TranspileWithEnv("""
            ARRAY BYTE A[][][3] = { 1, 2, 3, 4, 5, 6 };
            MAIN() { PRINT(A[0][0][0]); }
            """, MakeC64Env(), out var diag);

        Assert.True(diag.HasErrors, "第 2 次元以降省略は C99 違反 reject");
        Assert.Contains(diag.Diagnostics,
            d => d.Message.Contains("first dimension", StringComparison.OrdinalIgnoreCase)
              && d.Message.Contains("C99", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void LocalStaticMultiDimArrayByte_UnsizedFirstDim_EmitsCArrayInit()
    {
        // 関数内 static 経路も同じ multi-dim path で動くこと
        var src = TranspileWithEnv("""
            MAIN()
                ARRAY BYTE A[][3] = { 1, 2, 3, 4 };
            BEGIN
                PRINT(A[0][0]);
            END;
            """, MakeC64Env(), out var diag);

        Assert.False(diag.HasErrors,
            $"errors: {string.Join("; ", diag.Diagnostics.Select(d => d.Message))}");
        // C implicit zero fill: 4 byte init / 4 element per row → 1 行ぴったり (= 第 1 次元 = 1 を推論)
        Assert.Contains("static unsigned char V_MAIN_A[][4] = {0x01, 0x02, 0x03, 0x04};", src);
    }

    [Fact]
    public void ArrayBytePrefixDoublePercent_ParserRejects()
    {
        // ARRAY BYTE A[] = { %%1.5 } の `%%` は parser 文法上 ARRAY initializer 内では
        // 受理されない (= future syntax)。 parser reject を pin、 将来 parser に
        // `%%` InitialCode 受理が入った場合に test が落ちて気付ける
        var src = TranspileWithEnv("""
            ARRAY BYTE A[5] = { %%1.5 };
            MAIN() { PRINT(A[0]); }
            """, MakeC64Env(), out var diag);

        Assert.True(diag.HasErrors, "ARRAY BYTE 内の `%%` は parser で reject");
        Assert.Contains(diag.Diagnostics,
            d => d.Message.Contains("Expected", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void ArrayWithFixedAddressAndInitCode_ParserRejects()
    {
        // SLANG 文法レベルで `ARRAY x:address = { ... }` は parse error
        // (= `:address` の後に LBrace は来ない)。 つまり CEmitter の Address +
        // InitialCode 分岐は到達不能 = dead code。 v3b-E (5) で defensive guard を
        // 削除した際の回帰防止 (= 将来 parser に `:address = { ... }` 受理が入っても
        // この test が落ちて気付ける)。
        var src = TranspileWithEnv("""
            ARRAY BYTE A:$3000 = { 1, 2, 3 };
            MAIN() { PRINT(A[0]); }
            """, MakeC64Env(), out var diag);

        Assert.True(diag.HasErrors, "`ARRAY ...:address = { ... }` は SLANG parser で reject される文法");
        Assert.Contains(diag.Diagnostics,
            d => d.Message.Contains("Expected expression", StringComparison.OrdinalIgnoreCase)
              || d.Message.Contains("LBrace", StringComparison.OrdinalIgnoreCase));
    }

    // === v3b-E (Issue #194) (3b): ARRAY initializer 内の非定数 address 参照は
    //   oscar_c では permanent reject ===
    //
    // 当初は `%FUNC` / `%ARRAY` の address ref を `(unsigned int)F_xxx` /
    // `(unsigned int)V_xxx` で emit する MVP を試したが、 oscar64 が static integer
    // initializer で address-to-integer cast を constant initializer と認めない
    // (= error 3008 Constant initializer expected) ことを実機検証で確認した。
    // `void (*fp[])(void) = { foo }` 系の pointer-typed initializer なら通るが
    // SLANG `ARRAY WORD` の C 型は unsigned int[] で意味論的に変えられないため、
    // permanent backend gap として明示 reject + workaround メッセージを出す。
    // workaround: SLANG 側で runtime 初期化 (= ARRAY 宣言後 MAIN 冒頭等で
    //   `JT[0] = %FUNC; JT[1] = %ARRAY;`)。 Z80 backend は `DW LABEL` で対応。

    [Fact]
    public void ArrayWord_FunctionRef_OscarCRejectsWithRuntimeWorkaroundHint()
    {
        // ARRAY WORD JT[] = { %FUNC1 } = jump table の address ref、 oscar64 制約で
        // permanent reject、 SLANG runtime 初期化 workaround を message で誘導
        var src = TranspileWithEnv("""
            FUNC1() BEGIN END;
            ARRAY WORD JT[] = { %FUNC1 };
            MAIN() { PRINT(JT[0]); }
            """, MakeC64Env(), out var diag);

        Assert.True(diag.HasErrors, "ARRAY WORD への function address ref は oscar_c 制約で reject");
        Assert.Contains(diag.Diagnostics,
            d => d.Message.Contains("FUNC1", StringComparison.Ordinal)
              && d.Message.Contains("oscar64", StringComparison.Ordinal)
              && d.Message.Contains("error 3008", StringComparison.Ordinal));
    }

    [Fact]
    public void ArrayWord_ArrayRef_OscarCRejectsWithRuntimeWorkaroundHint()
    {
        // ARRAY WORD PT[] = { %BUF1 } = pointer table の address ref、 同制約で reject
        var src = TranspileWithEnv("""
            ARRAY BYTE BUF1[10];
            ARRAY WORD PT[] = { %BUF1 };
            MAIN() { PRINT(PT[0]); }
            """, MakeC64Env(), out var diag);

        Assert.True(diag.HasErrors, "ARRAY WORD への array address ref も同 oscar_c 制約で reject");
        Assert.Contains(diag.Diagnostics,
            d => d.Message.Contains("BUF1", StringComparison.Ordinal)
              && d.Message.Contains("oscar64", StringComparison.Ordinal));
    }

    [Fact]
    public void ArrayWord_ScalarGlobalVarRef_GenericNonConstError()
    {
        // scalar global var への %SVAR は Function/ARRAY 判定に hit せず generic
        // 「非定数 expression は static init で oscar64 error 3008」 message に流れる
        var src = TranspileWithEnv("""
            VAR WORD SVAR;
            ARRAY WORD W[] = { %SVAR };
            MAIN() { PRINT(W[0]); }
            """, MakeC64Env(), out var diag);

        Assert.True(diag.HasErrors, "scalar global var address も oscar_c で reject");
        Assert.Contains(diag.Diagnostics,
            d => d.Message.Contains("compile-time constant", StringComparison.OrdinalIgnoreCase)
              || d.Message.Contains("oscar64", StringComparison.Ordinal));
    }

    [Fact]
    public void ArrayByte_FunctionRef_OscarCRejectsWithRuntimeWorkaroundHint()
    {
        // ARRAY BYTE への %FUNC ref (= byte stream に address を 1 byte だけ詰める
        // SLANG 構文) も oscar64 制約で reject、 既存の非定数 reject path で oscar64
        // 制約 message に流れる。 名前を LabelRef ではなく FunctionRef にして実体
        // (= CastExpr(Word, IdentifierExpr) で関数参照) と一致させる (Codex review Low)。
        // 真の CodeLabelRef (`<LABEL>`) は別 SLANG 構文で別 test 対象。
        var src = TranspileWithEnv("""
            FUNC1() BEGIN END;
            ARRAY BYTE B[] = { %FUNC1 };
            MAIN() { PRINT(B[0]); }
            """, MakeC64Env(), out var diag);

        Assert.True(diag.HasErrors, "ARRAY BYTE への function ref も oscar_c で reject");
        Assert.Contains(diag.Diagnostics,
            d => d.Message.Contains("FUNC1", StringComparison.Ordinal)
              && d.Message.Contains("oscar64", StringComparison.Ordinal));
    }

    [Fact]
    public void ArrayByte_StringLiteralNul_RaisesError()
    {
        // NUL (= `\x00`) は CStringEncoder が `\0` C octal escape 短縮形で出すため、
        // 直後が 0..7 だと C 側で octal escape として連結し ARRAY 内容が壊れる
        // (= Codex review round 2 Medium 指摘: SLANG `"\x007"` → C `"\07"` = [0x07]
        //  と誤解釈、 期待 [0x00, '7', 0x00] にならない)。 安全策で NUL も scope 外 reject。
        var src = TranspileWithEnv("""
            ARRAY BYTE MSG[] = { "\x007" };
            MAIN() { PRINT(MSG[0]); }
            """, MakeC64Env(), out var diag);

        Assert.True(diag.HasErrors, "NUL 含む StringLiteral は v3b-E (3a) scope 外");
        Assert.Contains(diag.Diagnostics,
            d => d.Message.Contains("NUL", StringComparison.Ordinal)
              || d.Message.Contains("non-printable", StringComparison.OrdinalIgnoreCase));
    }
}
