using System.Collections.Generic;
using SLANGCompiler;
using SLANGCompiler.Lexer;
using SLANGCompiler.Parser;
using SLANGCompiler.Parser.Ast;

namespace SLANGCompiler.Semantics;

/// <summary>
/// SLANG ARRAY 宣言の <c>= { ... }</c> (= CODE list) が emit する byte 数を
/// 計算する共通 helper (Issue #190)。
///
/// <para>SLANG spec / user 設計レビュー (#190) 確定の責務:</para>
/// <list type="bullet">
/// <item><description>容量超過 (= byte 数 &gt; <c>type.ByteSize</c>) と FLOAT 配列の
/// トップレベル CastExpr / 非 FLOAT 配列の default 非定数 BYTE のように
/// 「**semantic で reject すべき仕様違反**」のみ <see cref="DiagnosticBag"/> に
/// 出す。値の最終解決 (= 個別 CONST / asm label が link で resolve されるか) は
/// backend に委任する。</description></item>
/// <item><description>非 FLOAT 配列 = CODE byte stream 解釈で 3 系統:
/// (1) default item は ConstEvaluator で定数性のみ確認、評価不能なら error;
/// (2) typed item (CastExpr/StringLiteral/CodeLabelRef) は構文から byte 数のみ
/// 計算 (= BYTE 1, WORD 2, FLOAT 3, StringLiteral = SJIS byte 長, CodeLabelRef 2);
/// (3) は FLOAT 配列専用 path で各要素 3 byte fixed + トップレベル CastExpr 禁止。</description></item>
/// </list>
///
/// <para>戻り値: 計算成功時の byte 数。仕様違反は diagnostic 出して <c>null</c>
/// を返す (= 呼出側 SemanticAnalyzer は容量超過判定を skip)。</para>
/// </summary>
public static class ArrayInitialCodeSizer
{
    /// <summary>
    /// CODE list の展開 byte 数を計算する。 呼出側は戻り値 (= byte 数) と
    /// 配列容量 (<see cref="SlangType.ByteSize"/>) を比較して超過 error を出す。
    /// </summary>
    /// <param name="code">ARRAY decl の InitialCode (= 各要素 Expression の list)</param>
    /// <param name="elementSize">配列要素型 (BYTE/WORD/FLOAT)、FLOAT 配列のみ別 path</param>
    /// <param name="globals">CONST 参照解決用 (ConstEvaluator に渡す)</param>
    /// <param name="diagnostics">仕様違反時の error 出力先</param>
    /// <returns>計算成功 byte 数 / 仕様違反は null</returns>
    public static int? CalculateByteCount(
        List<Expression> code, DataSize elementSize,
        SymbolTable? globals, DiagnosticBag diagnostics)
    {
        return elementSize == DataSize.Float
            ? CalculateFloatArrayBytes(code, diagnostics)
            : CalculateCodeStreamBytes(code, globals, diagnostics);
    }

    /// <summary>
    /// 非 FLOAT 配列 (= CODE byte stream 解釈) の byte 数計算。
    /// default item は ConstEvaluator で定数性確認、 typed item / StringLiteral /
    /// CodeLabelRef は構文から byte 数判定 (= 値解決は backend)。
    /// </summary>
    private static int? CalculateCodeStreamBytes(
        List<Expression> code, SymbolTable? globals, DiagnosticBag diagnostics)
    {
        var constEval = globals != null ? new ConstEvaluator(globals) : null;
        int total = 0;
        bool sawError = false;
        foreach (var expr in code)
        {
            int? itemBytes = ClassifyCodeStreamItem(expr, constEval, diagnostics);
            if (itemBytes == null) { sawError = true; continue; }
            total += itemBytes.Value;
        }
        return sawError ? (int?)null : total;
    }

    /// <summary>
    /// 非 FLOAT 配列の 1 要素の byte 数を判定する。
    /// </summary>
    private static int? ClassifyCodeStreamItem(
        Expression expr, ConstEvaluator? constEval, DiagnosticBag diagnostics)
    {
        // typed item: CastExpr で型明示 → 構文から byte 数確定、値解決は backend
        if (expr is CastExpr cast)
        {
            return cast.TargetSize switch
            {
                DataSize.Byte => 1,
                DataSize.Word => 2,
                DataSize.Float => 3, // 非 FLOAT 配列内の `%%` は f24 を 3 byte stream に流す (許可)
                _ => 1,
            };
        }

        // typed item: StringLiteral = SJIS byte 列の長さ
        if (expr is StringLiteral slit)
        {
            // StringEncoder は Z80 既存 helper (LabelUtils.cs)、 SJIS 変換失敗時は
            // diagnostics に error が積まれる (= ToShiftJisBytes 仕様)。
            var bytes = StringEncoder.ToShiftJisBytes(slit.Value, diagnostics);
            return bytes.Length;
        }

        // typed/special item: CodeLabelRef = label address WORD (= 2 byte)
        if (expr is CodeLabelRef)
        {
            return 2;
        }

        // default item: 素の式は 1 byte だが、 ConstEvaluator で定数評価できないと
        // 「非定数 BYTE は error」 = 値解決が backend に届かない (= byte 数確定しない)
        // ConstEvaluator が null (= globals 未提供) の場合は IntegerLiteral だけ許可、
        // それ以外は判定不能 = error
        int? evaluated = null;
        if (constEval != null) evaluated = constEval.Evaluate(expr);
        if (expr is IntegerLiteral ilit) evaluated = (int)ilit.Value;
        if (evaluated.HasValue) return 1;

        diagnostics.Error(
            "ARRAY initializer item must be a compile-time constant or use a typed prefix (`%`/`WORD,`/`%%`/`FLOAT,`/`BYTE,`) / StringLiteral / CodeLabelRef",
            expr.Span);
        return null;
    }

    /// <summary>
    /// FLOAT 配列 (= f24 element stream 解釈) の byte 数計算。
    /// 各要素 3 byte fixed、 トップレベル CastExpr は error。
    /// 値評価 (EvaluateFloat) は backend に残す (= helper は byte 数のみ責任)。
    /// </summary>
    private static int? CalculateFloatArrayBytes(
        List<Expression> code, DiagnosticBag diagnostics)
    {
        bool sawError = false;
        foreach (var expr in code)
        {
            if (expr is CastExpr)
            {
                // 既存 IrGenerator の同 error message と整合させる
                // (= IntegrationTests.FloatArrayInit_TopLevelCastExpr_Error の期待値)
                diagnostics.Error(
                    "Cast expression not allowed in FLOAT array initializer",
                    expr.Span);
                sawError = true;
            }
        }
        // FLOAT 配列の各要素は 3 byte fixed (= cast 禁止だけ check して count は素直に length × 3)
        return sawError ? (int?)null : code.Count * 3;
    }
}
