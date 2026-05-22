using Xunit;
using SLANGCompiler.Lexer;
using SLANGCompiler.Parser;
using SLANGCompiler.Semantics;

namespace SLANGCompiler.Tests;

/// <summary>
/// Issue #190: SLANG ARRAY InitialCode capacity check + assignment guard が
/// 全 backend で揃うことを semantic 段階で固定する。 SemanticAnalyzer は env
/// 非依存のため、 環境 1 つだけで SLANG コードを流して DiagnosticBag を
/// 直接 assert する形 (= ArrayInitOscarCTests は oscar_c transpile golden、
/// 本テストは backend 手前の semantic level diagnostic golden で責務を分担)。
///
/// 仕様 (= plan / user 補正 完全準拠):
/// - 非 FLOAT 配列 = CODE byte stream (= default 1 byte / `%`/WORD 2 byte
///   / `%%`/FLOAT 3 byte / StringLiteral SJIS byte 長 / CodeLabelRef 2 byte)
/// - FLOAT 配列 = f24 element stream (= 各 3 byte、 トップレベル CastExpr 禁止)
/// - 容量超過は error、 不足は 0 fill 許可、 添字省略は容量判定なし
/// - ARRAY 宣言 symbol への AssignExpr / CompoundAssignExpr / IncrementExpr は error
/// - ArrayAccessExpr LHS / VAR BYTE T[] (= IsArrayDecl=false) は許可
/// </summary>
public class ArrayInitSemanticTests
{
    /// <summary>
    /// Lexer → Preprocessor → Parser → SemanticAnalyzer を通して
    /// DiagnosticBag を返す。 backend 非依存 (= env 指定なし、 default の
    /// lsx 相当で動作)。
    /// </summary>
    private static DiagnosticBag Analyze(string source)
    {
        var diag = new DiagnosticBag();
        var lexer = new Lexer.Lexer(source);
        var tokens = lexer.Tokenize();
        var preproc = new Preprocessor(diag);
        tokens = preproc.Process(tokens, ".");
        var parser = new Parser.Parser(tokens, diag);
        var ast = parser.ParseCompilationUnit();
        if (diag.HasErrors) return diag;  // parse error が出たらそれ以上進めない
        var analyzer = new SemanticAnalyzer(diag);
        analyzer.Analyze(ast);
        return diag;
    }

    private static string DiagMessages(DiagnosticBag diag) =>
        string.Join("; ", diag.Diagnostics.Select(d => d.Message));

    // === 容量判定: 非 FLOAT 配列 (= CODE byte stream 解釈) ===

    [Fact]
    public void ArrayByte_FixedCapacity_InitWithinCapacity_Pass()
    {
        // ARRAY BYTE A[3] は容量 4 byte (= 4 elements)、 init 4 byte で ぴったり満杯
        var diag = Analyze("ARRAY BYTE A[3] = {1,2,3,4}; MAIN() BEGIN END;");
        Assert.False(diag.HasErrors, $"errors: {DiagMessages(diag)}");
    }

    [Fact]
    public void ArrayByte_FixedCapacity_InitExceeds_Error()
    {
        // ARRAY BYTE A[3] は容量 4 byte、 init 5 byte で 1 byte 超過
        var diag = Analyze("ARRAY BYTE A[3] = {1,2,3,4,5}; MAIN() BEGIN END;");
        Assert.True(diag.HasErrors, "容量超過 (= 5 > 4) で error 期待");
        Assert.Contains(diag.Diagnostics,
            d => d.Message.Contains("capacity", System.StringComparison.OrdinalIgnoreCase)
              || d.Message.Contains("多すぎ", System.StringComparison.Ordinal));
    }

    [Fact]
    public void ArrayByte_FixedCapacity_InitShort_PassFor0Fill()
    {
        // ARRAY BYTE A[3] は容量 4 byte、 init 2 byte は不足だが SLANG 仕様で 0 fill 許可
        var diag = Analyze("ARRAY BYTE A[3] = {1,2}; MAIN() BEGIN END;");
        Assert.False(diag.HasErrors, $"不足は 0 fill 許可、 errors: {DiagMessages(diag)}");
    }

    [Fact]
    public void ArrayByte_OmittedSize_InitAny_Pass()
    {
        // 添字省略 ARRAY BYTE A[] は容量判定なし (= SLANG 仕様「チェックしない」)
        var diag = Analyze("ARRAY BYTE A[] = {1,2,3,4,5}; MAIN() BEGIN END;");
        Assert.False(diag.HasErrors, $"添字省略 + init は容量判定なし、 errors: {DiagMessages(diag)}");
    }

    // 注: 非 FLOAT 配列内に `%%` (FLOAT cast) を書く SLANG syntax は parser 未対応
    // (= L1175 周辺で `%` WORD と `BYTE,`/`WORD,` のみ CastExpr 生成)。
    // ArrayInitialCodeSizer 側に `CastExpr(TargetSize: Float) => 3 byte` 分岐は
    // 残置してあるが parser から到達しない dead path、 将来 parser 拡張時に自動対応。
    // そのため `%%1.5` の正常 case test は省略 (= parser error しか出ないため)。

    [Fact]
    public void ArrayWord_FixedCapacity_PlainItems_Pass()
    {
        // ARRAY WORD W[2] は容量 6 byte (= 3 elements × 2)、 init 3 plain item = 3 byte で OK
        // (= 各 plain item は default 1 byte、 WORD 配列でも CODE byte stream 解釈)
        var diag = Analyze("ARRAY WORD W[2] = {1,2,3}; MAIN() BEGIN END;");
        Assert.False(diag.HasErrors, $"errors: {DiagMessages(diag)}");
    }

    [Fact]
    public void ArrayWord_FixedCapacity_PercentItems_Pass()
    {
        // ARRAY WORD W[2] 容量 6 byte、 %item 3 個 = 6 byte でぴったり
        var diag = Analyze("ARRAY WORD W[2] = {%1,%2,%3}; MAIN() BEGIN END;");
        Assert.False(diag.HasErrors, $"errors: {DiagMessages(diag)}");
    }

    [Fact]
    public void ArrayWord_FixedCapacity_PercentItemsExceed_Error()
    {
        // ARRAY WORD W[2] 容量 6 byte、 %item 4 個 = 8 byte で超過
        var diag = Analyze("ARRAY WORD W[2] = {%1,%2,%3,%4}; MAIN() BEGIN END;");
        Assert.True(diag.HasErrors, "%item 4 個 = 8 byte > 容量 6 byte で error 期待");
    }

    // === 容量判定: FLOAT 配列 (= f24 element stream) ===

    [Fact]
    public void ArrayFloat_FixedCapacity_PlainItems_Pass()
    {
        // ARRAY FLOAT FA[3] は容量 12 byte (= 4 elements × 3)、 plain 3 個 = 9 byte で OK
        var diag = Analyze("ARRAY FLOAT FA[3] = {1.0,2.0,3.0}; MAIN() BEGIN END;");
        Assert.False(diag.HasErrors, $"errors: {DiagMessages(diag)}");
    }

    [Fact]
    public void ArrayFloat_TopLevelCastExpr_Error()
    {
        // FLOAT 配列のトップレベル `%` cast 混在は SLANG 仕様で禁止 (= 既存 IrGenerator 仕様)
        var diag = Analyze("ARRAY FLOAT FA[3] = {%1, 2.0, 3.0}; MAIN() BEGIN END;");
        Assert.True(diag.HasErrors, "FLOAT 配列のトップレベル cast 混在は error 期待");
    }

    // === 代入 guard: ARRAY 宣言 symbol への AssignExpr / Compound / Increment ===

    [Fact]
    public void Assignment_ToGlobalArrayDecl_Error()
    {
        // global ARRAY 宣言 symbol への直接代入は error (= 配列実体置換は SLANG では不可)
        var diag = Analyze("ARRAY BYTE A[10]; MAIN() BEGIN A = $3000; END;");
        Assert.True(diag.HasErrors, "ARRAY 宣言 symbol への代入は error 期待");
        Assert.Contains(diag.Diagnostics,
            d => d.Message.Contains("ARRAY", System.StringComparison.OrdinalIgnoreCase)
              && (d.Message.Contains("assign", System.StringComparison.OrdinalIgnoreCase)
                  || d.Message.Contains("代入", System.StringComparison.Ordinal)));
    }

    [Fact]
    public void Assignment_ToFunctionStaticArrayDecl_Error()
    {
        // 関数内 static ARRAY (= MAIN() ARRAY BYTE A[2]; BEGIN A = $3000; END;) も同様
        var diag = Analyze("MAIN() ARRAY BYTE A[2]; BEGIN A = $3000; END;");
        Assert.True(diag.HasErrors, "関数内 static ARRAY 代入は error 期待");
    }

    [Fact]
    public void CompoundAssignment_ToArrayDecl_Error()
    {
        // `A += 1` のような CompoundAssignExpr も ARRAY 宣言 symbol へは禁止
        var diag = Analyze("ARRAY BYTE A[10]; MAIN() BEGIN A += 1; END;");
        Assert.True(diag.HasErrors, "ARRAY 宣言 symbol への compound-assign は error 期待");
    }

    [Fact]
    public void Increment_ToArrayDecl_Error()
    {
        // `A++` (= IncrementExpr) も ARRAY 宣言 symbol へは禁止
        var diag = Analyze("ARRAY BYTE A[10]; MAIN() BEGIN A++; END;");
        Assert.True(diag.HasErrors, "ARRAY 宣言 symbol への increment は error 期待");
    }

    [Fact]
    public void Assignment_ToArrayAccessLhs_Pass()
    {
        // `A[i] = X` (= ArrayAccessExpr LHS) は当然許可 (= 要素代入)
        var diag = Analyze("ARRAY BYTE A[10]; MAIN() VAR I; BEGIN I = 0; A[I] = 1; END;");
        Assert.False(diag.HasErrors, $"ArrayAccessExpr LHS は許可、 errors: {DiagMessages(diag)}");
    }

    [Fact]
    public void Assignment_ToVarPointer_Pass()
    {
        // VAR BYTE T[]; (= IsArrayDecl=false、 PointerType) への代入は引き続き許可
        // = regression なし (= 既存 SLANG コードで広く使われるパターン)
        var diag = Analyze("VAR BYTE T[]; MAIN() BEGIN T = $3000; END;");
        Assert.False(diag.HasErrors, $"VAR BYTE T[] (PointerType) 代入は許可、 errors: {DiagMessages(diag)}");
    }
}
