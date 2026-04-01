using System.Text;

namespace SLANGCompiler;

/// <summary>文字列をShift-JISバイト列のDB形式に変換するヘルパー</summary>
public static class StringEncoder
{
    private static readonly Encoding ShiftJis;
    static StringEncoder()
    {
        Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);
        ShiftJis = Encoding.GetEncoding(932,
            EncoderFallback.ExceptionFallback, DecoderFallback.ExceptionFallback);
    }

    /// <summary>文字列をShift-JISバイト列に変換</summary>
    public static byte[] ToShiftJisBytes(string text, DiagnosticBag? diagnostics = null)
    {
        try
        {
            return ShiftJis.GetBytes(text);
        }
        catch (EncoderFallbackException)
        {
            diagnostics?.Error("String contains characters that cannot be encoded to Shift-JIS", default);
            return Encoding.ASCII.GetBytes(text);
        }
    }

    /// <summary>文字列をShift-JISバイト列のDB引数形式に変換（0終端なし）</summary>
    public static string ToAsmDbArgs(string text, DiagnosticBag? diagnostics = null)
    {
        // ASCII文字のみ → そのまま
        if (text.All(ch => ch >= 0x20 && ch < 0x7F && ch != '"'))
            return $"\"{text}\"";

        var bytes = ToShiftJisBytes(text, diagnostics);
        var parts = new List<string>();
        var strBuf = new StringBuilder();
        foreach (var b in bytes)
        {
            if (b >= 0x20 && b < 0x7F && b != (byte)'"')
                strBuf.Append((char)b);
            else
            {
                if (strBuf.Length > 0) { parts.Add($"\"{strBuf}\""); strBuf.Clear(); }
                parts.Add($"${b:X2}");
            }
        }
        if (strBuf.Length > 0) parts.Add($"\"{strBuf}\"");
        return string.Join(",", parts);
    }
}

/// <summary>
/// ソース識別子→ASM安全ラベル名の変換ユーティリティ。
/// アセンブラ(AILZ80ASM)で使用可能な文字（英数字+_）のみで構成されるラベルに変換する。
/// </summary>
internal static class LabelUtils
{
    /// <summary>ソース識別子→ASM安全ラベル名。</summary>
    public static string SanitizeLabel(string name)
    {
        // 既にASM安全ならそのまま返す（高速パス）
        bool safe = true;
        foreach (var c in name)
        {
            if (!((c >= 'A' && c <= 'Z') || (c >= 'a' && c <= 'z') || (c >= '0' && c <= '9') || c == '_'))
            {
                safe = false;
                break;
            }
        }
        if (safe) return name;

        var sb = new StringBuilder(name.Length);
        foreach (var c in name)
        {
            if (c == '@') sb.Append("_AT_");
            else if (c == '^') sb.Append(".");
            else if ((c >= 'A' && c <= 'Z') || (c >= 'a' && c <= 'z') || (c >= '0' && c <= '9') || c == '_')
                sb.Append(c);
            else
                sb.Append($"_U{(int)c:X4}_");
        }
        return sb.ToString();
    }

    /// <summary>ユーザー変数/配列のASMラベル</summary>
    public static string UserVarLabel(string name) => $"_V_{SanitizeLabel(name)}";

    /// <summary>関数内静的変数のASMラベル</summary>
    public static string StaticVarLabel(string funcName, string varName)
        => $"_V_{SanitizeLabel(funcName)}_{SanitizeLabel(varName)}";

    /// <summary>ユーザー定義ラベル(LABEL/GOTO)のASMラベル</summary>
    public static string UserLabel(string name) => $"_LBL_{SanitizeLabel(name)}";

    /// <summary>double→f24(24bit float)変換。旧コンパイラのSLANGCommonUtility.ValueToFloat24Byte準拠。
    /// f24形式: byte[0]=L, byte[1]=H, byte[2]=A。A=sign(1)+exponent(7,bias=63), HL=mantissa(16)。</summary>
    public static byte[] ConvertToF24(double value)
    {
        if (value == 0.0) return new byte[] { 0, 0, 0 };

        int sign = 0;
        if (value < 0) { sign = 1; value = -value; }
        if (double.IsInfinity(value)) return new byte[] { 0, 0, (byte)(127 + (sign << 7)) };

        int exp = 0;
        while (value < 1) { exp--; value += value; }
        while (value >= 2) { exp++; value /= 2; }
        if (exp > 63) return new byte[] { 0, 0, (byte)(127 + (sign << 7)) };
        if (exp < -62) return new byte[] { 0, 0, 0 };

        byte a = (byte)(exp + 63 + (sign << 7));
        value -= 1.0; // 暗黙1を除去
        value *= 256;
        byte h = (byte)value;
        value -= h;
        value *= 256;
        value += 0.5; // rounding
        byte l = (byte)value;
        // 繰り上がり処理
        if (l == 0 && value >= 256) { h++; if (h == 0) { a++; } }
        return new byte[] { l, h, a };
    }

    /// <summary>
    /// AST Expressionをアセンブラ式文字列に変換。ConstEvaluatorとは別で、整数評価ではなく文字列化。
    /// 戻り値のDepsはランタイム依存解決に使う基底ラベル名リスト。
    /// </summary>
    public static (string Expr, List<string> Deps)? ExprToAsmString(
        Parser.Ast.Expression expr, Semantics.SymbolTable? symbols, DiagnosticBag? diag = null)
    {
        var deps = new List<string>();

        string? Convert(Parser.Ast.Expression e)
        {
            switch (e)
            {
                case Parser.Ast.IntegerLiteral lit:
                    return $"${lit.Value:X4}";

                case Parser.Ast.IdentifierExpr id:
                    var sym = symbols?.Resolve(id.Name);
                    if (sym != null)
                    {
                        if (sym.ConstValue is int cv) return $"${cv:X4}";
                        // ConstAst未解決なら再帰的に解決・キャッシュ
                        if (sym.ConstAst != null && !sym.ConstAsmResolved)
                        {
                            sym.ConstAsmResolved = true;
                            var inner = ExprToAsmString(sym.ConstAst, symbols, diag);
                            if (inner.HasValue)
                            {
                                sym.ConstAsmExpr = inner.Value.Expr;
                                sym.ConstAsmDeps = inner.Value.Deps;
                            }
                        }
                        if (sym.ConstAsmExpr != null)
                        {
                            deps.AddRange(sym.ConstAsmDeps ?? []);
                            return sym.ConstAsmExpr;
                        }
                        if (sym.IsCodeBlock || sym.IsGlobal
                            || sym.Kind == Semantics.SymbolKind.Function
                            || sym.Kind == Semantics.SymbolKind.MachineFunction)
                        {
                            var label = sym.AsmLabel ?? SanitizeLabel(id.Name);
                            deps.Add(label);
                            return label;
                        }
                        diag?.Error($"'{id.Name}' cannot be used in MACHINE/CONST address expression", e.Span);
                        return null;
                    }
                    // シンボル未登録: ランタイム等の外部ラベル
                    var extLabel = SanitizeLabel(id.Name);
                    deps.Add(extLabel);
                    return extLabel;

                case Parser.Ast.BinaryExpr bin when bin.Op is Parser.Ast.BinaryOp.Add or Parser.Ast.BinaryOp.Sub:
                    var left = Convert(bin.Left);
                    var right = Convert(bin.Right);
                    if (left == null || right == null) return null;
                    return $"{left}{(bin.Op == Parser.Ast.BinaryOp.Add ? "+" : "-")}{right}";

                case Parser.Ast.BinaryExpr bin2:
                    // Mul等の定数演算: ConstEvaluatorで評価してリテラルに変換
                    var constEval = symbols != null ? new Semantics.ConstEvaluator(symbols) : null;
                    var constVal = constEval?.Evaluate(bin2);
                    if (constVal.HasValue)
                        return $"${constVal.Value & 0xFFFF:X4}";
                    diag?.Error($"Unsupported expression in MACHINE/CONST address: {e.GetType().Name}", e.Span);
                    return null;

                default:
                    diag?.Error($"Unsupported expression in MACHINE/CONST address: {e.GetType().Name}", e.Span);
                    return null;
            }
        }

        var result = Convert(expr);
        return result != null ? (result, deps) : null;
    }
}
