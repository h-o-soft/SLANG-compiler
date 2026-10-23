using SLANGCompiler.Parser.Ast;

namespace SLANGCompiler.Semantics;

/// <summary>
/// 定数式を評価する。CONST宣言や配列サイズなどで使用。
/// ASTの式ノードを受け取り、コンパイル時に評価可能なら整数値を返す。
/// </summary>
public class ConstEvaluator
{
    private readonly SymbolTable? _symbols;

    public ConstEvaluator(SymbolTable? symbols = null)
    {
        _symbols = symbols;
    }

    /// <summary>
    /// 式を評価し、定数であれば値を返す。定数でなければnull。
    /// </summary>
    public int? Evaluate(Expression expr)
    {
        return expr switch
        {
            IntegerLiteral lit => (int)lit.Value,
            FloatLiteral flt when flt.Value == Math.Truncate(flt.Value) => (int)flt.Value,
            IdentifierExpr id => EvaluateIdentifier(id),
            UnaryExpr unary => EvaluateUnary(unary),
            BinaryExpr binary => EvaluateBinary(binary),
            HighLowExpr hl => EvaluateHighLow(hl),
            CastExpr cast => Evaluate(cast.Operand),         // %expr → 値はそのまま
            ConditionalExpr cond => EvaluateConditional(cond),
            AddressOfExpr addr => null,                       // &var は実行時
            StringLiteral => null,                            // 文字列は定数式不可
            _ => null,
        };
    }

    private int? EvaluateIdentifier(IdentifierExpr id)
    {
        // TRUE/FALSE
        if (id.Name.Equals("TRUE", StringComparison.OrdinalIgnoreCase)) return 1;
        if (id.Name.Equals("FALSE", StringComparison.OrdinalIgnoreCase)) return 0;

        // シンボルテーブルから定数を検索
        var sym = _symbols?.Resolve(id.Name);
        if (sym?.Kind == SymbolKind.Constant && sym.ConstValue is int val)
            return val;

        return null;
    }

    private int? EvaluateUnary(UnaryExpr expr)
    {
        var operand = Evaluate(expr.Operand);
        if (operand == null) return null;

        return expr.Op switch
        {
            UnaryOp.Negate => -operand.Value,
            UnaryOp.Plus => operand.Value,
            UnaryOp.Not => operand.Value == 0 ? 1 : 0,
            UnaryOp.Cpl => ~operand.Value & 0xFFFF,
            _ => null,
        };
    }

    private int? EvaluateBinary(BinaryExpr expr)
    {
        var left = Evaluate(expr.Left);
        var right = Evaluate(expr.Right);
        if (left == null || right == null) return null;

        int l = left.Value, r = right.Value;
        return expr.Op switch
        {
            BinaryOp.Add => (l + r) & 0xFFFF,
            BinaryOp.Sub => (l - r) & 0xFFFF,
            BinaryOp.Mul => (l * r) & 0xFFFF,
            BinaryOp.Div => r != 0 ? l / r : null,
            BinaryOp.Mod => r != 0 ? l % r : null,
            BinaryOp.And => l & r,
            BinaryOp.Or => l | r,
            BinaryOp.Xor => l ^ r,
            BinaryOp.Shl => (l << r) & 0xFFFF,
            BinaryOp.Shr => (l >> r) & 0xFFFF,
            BinaryOp.Eq => l == r ? 1 : 0,
            BinaryOp.Neq => l != r ? 1 : 0,
            BinaryOp.Lt => (ushort)l < (ushort)r ? 1 : 0,
            BinaryOp.Gt => (ushort)l > (ushort)r ? 1 : 0,
            BinaryOp.Le => (ushort)l <= (ushort)r ? 1 : 0,
            BinaryOp.Ge => (ushort)l >= (ushort)r ? 1 : 0,
            BinaryOp.SLt => (short)l < (short)r ? 1 : 0,
            BinaryOp.SGt => (short)l > (short)r ? 1 : 0,
            BinaryOp.SLe => (short)l <= (short)r ? 1 : 0,
            BinaryOp.SGe => (short)l >= (short)r ? 1 : 0,
            BinaryOp.LogAnd => (l != 0 && r != 0) ? 1 : 0,
            BinaryOp.LogOr => (l != 0 || r != 0) ? 1 : 0,
            _ => null,
        };
    }

    /// <summary>
    /// 式に FLOAT が関わるか (FloatLiteral・FLOAT 定数・FLOAT へのキャストを含むか)
    /// </summary>
    public bool InvolvesFloat(Expression expr)
    {
        return expr switch
        {
            FloatLiteral => true,
            IdentifierExpr id => _symbols?.Resolve(id.Name) is { Kind: SymbolKind.Constant, ConstFloatValue: not null },
            CastExpr cast => cast.TargetSize == DataSize.Float || InvolvesFloat(cast.Operand),
            UnaryExpr unary => InvolvesFloat(unary.Operand),
            BinaryExpr binary => InvolvesFloat(binary.Left) || InvolvesFloat(binary.Right),
            HighLowExpr hl => InvolvesFloat(hl.Operand),
            ConditionalExpr cond => InvolvesFloat(cond.Condition) || InvolvesFloat(cond.TrueExpr) || InvolvesFloat(cond.FalseExpr),
            _ => false,
        };
    }

    /// <summary>FoldFloatArithmetic の結果の種類</summary>
    public enum FloatFoldKind
    {
        /// <summary>整数の即値 (IntValue)</summary>
        Int,
        /// <summary>FLOAT の定数 (FloatValue。f24 で正確に表せ、実行時の計算と同じ値)</summary>
        Float,
        /// <summary>
        /// 畳み込まない (実行時に f24 で計算する)。定数や途中の値が f24 で正確に表せず、コンパイル時の計算が実行時と
        /// 同じ値になると言えないとき。FloatValue は double で求めた値
        /// </summary>
        Runtime,
    }

    public readonly record struct FloatFold(FloatFoldKind Kind, int IntValue, double FloatValue);

    /// <summary>
    /// Evaluate が整数として畳み込む式のうち、FLOAT が関わる四則演算の正しい値。
    /// Evaluate は小数部のない FloatLiteral を整数として読むので、5.0/2.0 を 2、40000.0+40000.0 を 14464 にしてしまう。
    /// 実行時と同じ規則で求め直して、次の順に決める:
    /// (0) 定数・途中の値のどれかが f24 で正確に表せない → Runtime (畳み込まない)。
    /// (1) 小数部がなく -32768〜65535 で、Evaluate の値と 16 ビットで同じ → Evaluate の値そのもの (今までと同じコード)。
    /// (2) 小数部がなく -32768〜32767 → その整数 (FLOAT に入れても符号付きの変換で同じ値に戻る)。
    /// (3) それ以外 → FLOAT の値 (32768〜65535 を整数にすると、FLOAT に入れたときに負の数になるため)。
    /// 当たらない式 (FLOAT が関わらない・四則演算と単項の +/- 以外を含む・Evaluate が値を返さない・0 で割る) は null
    /// (呼び出し側は今までどおり Evaluate を使う)。
    /// </summary>
    public FloatFold? FoldFloatArithmetic(Expression expr)
    {
        if (!InvolvesFloat(expr) || Evaluate(expr) is not int iv) return null;
        bool exact = true;
        if (EvaluateFloatArithmetic(expr, ref exact) is not double d) return null;
        if (!exact) return new FloatFold(FloatFoldKind.Runtime, 0, d);
        if (d == Math.Truncate(d))
        {
            if (d >= -32768 && d <= 65535 && ((long)iv - (long)d) % 65536 == 0) return new FloatFold(FloatFoldKind.Int, iv, d);
            if (d >= -32768 && d <= 32767) return new FloatFold(FloatFoldKind.Int, (int)d, d);
        }
        return new FloatFold(FloatFoldKind.Float, 0, d);
    }

    /// <summary>
    /// FLOAT が関わる四則演算の定数式を、実行時と同じ規則で求める。FLOAT の値は double のまま、FLOAT が関わらない
    /// 部分は整数の規則 (16 ビット) で求めて符号付きとして読む (実行時の i16tof24 と同じ)。
    /// FLOAT の定数・途中の結果が f24 で正確に表せなければ exact を偽にする (実行時は f24 に丸めながら計算するため)
    /// </summary>
    private double? EvaluateFloatArithmetic(Expression expr, ref bool exact)
    {
        double? v;
        if (!InvolvesFloat(expr))
            v = Evaluate(expr) is int i ? (short)(i & 0xFFFF) : null;
        else
        {
            switch (expr)
            {
                case FloatLiteral flt:
                    v = flt.Value;
                    break;
                case IdentifierExpr id:
                    v = _symbols?.Resolve(id.Name)?.ConstFloatValue;
                    break;
                case UnaryExpr { Op: UnaryOp.Negate } u:
                    v = -EvaluateFloatArithmetic(u.Operand, ref exact);
                    break;
                case UnaryExpr { Op: UnaryOp.Plus } u:
                    v = EvaluateFloatArithmetic(u.Operand, ref exact);
                    break;
                case BinaryExpr b when b.Op is BinaryOp.Add or BinaryOp.Sub or BinaryOp.Mul or BinaryOp.Div
                                             or BinaryOp.SMul or BinaryOp.SDiv:
                {
                    var l = EvaluateFloatArithmetic(b.Left, ref exact);
                    var r = EvaluateFloatArithmetic(b.Right, ref exact);
                    if (l is not double x || r is not double y) return null;
                    v = b.Op switch
                    {
                        BinaryOp.Add => x + y,
                        BinaryOp.Sub => x - y,
                        BinaryOp.Mul or BinaryOp.SMul => x * y,
                        _ => y != 0 ? x / y : null,
                    };
                    break;
                }
                default:
                    return null;
            }
        }
        if (v is double d && !IsExactF24(d)) exact = false;
        return v;
    }

    /// <summary>double の値が f24 (LabelUtils.ConvertToF24) で正確に表せるか</summary>
    public static bool IsExactF24(double v)
    {
        if (v == 0) return true;
        if (double.IsNaN(v) || double.IsInfinity(v)) return false;
        var b = LabelUtils.ConvertToF24(v);
        int e = b[2] & 0x7F;
        if (e == 0 || e == 0x7F) return false;               // 0 か無限大に丸まった
        double m = 1 + (b[0] | (b[1] << 8)) / 65536.0;
        double back = Math.ScaleB(m, e - 63) * ((b[2] & 0x80) != 0 ? -1 : 1);
        return back == v;
    }

    private int? EvaluateHighLow(HighLowExpr expr)
    {
        var operand = Evaluate(expr.Operand);
        if (operand == null) return null;
        return expr.IsHigh ? (operand.Value >> 8) & 0xFF : operand.Value & 0xFF;
    }

    private int? EvaluateConditional(ConditionalExpr expr)
    {
        var cond = Evaluate(expr.Condition);
        if (cond == null) return null;
        return cond.Value != 0 ? Evaluate(expr.TrueExpr) : Evaluate(expr.FalseExpr);
    }

    /// <summary>
    /// 式をFLOAT定数として評価。定数であればdouble値を返す。
    /// </summary>
    public double? EvaluateFloat(Expression expr)
    {
        return expr switch
        {
            IntegerLiteral lit => (double)lit.Value,
            FloatLiteral flt => flt.Value,
            IdentifierExpr id => EvaluateFloatIdentifier(id),
            UnaryExpr unary => EvaluateFloatUnary(unary),
            BinaryExpr binary => EvaluateFloatBinary(binary),
            CastExpr cast => EvaluateFloat(cast.Operand),
            _ => null,
        };
    }

    private double? EvaluateFloatIdentifier(IdentifierExpr id)
    {
        var sym = _symbols?.Resolve(id.Name);
        if (sym?.Kind == SymbolKind.Constant)
        {
            if (sym.ConstFloatValue.HasValue) return sym.ConstFloatValue.Value;
            if (sym.ConstValue is int val) return (double)val;
        }
        return null;
    }

    private double? EvaluateFloatUnary(UnaryExpr expr)
    {
        var operand = EvaluateFloat(expr.Operand);
        if (operand == null) return null;
        return expr.Op switch
        {
            UnaryOp.Negate => -operand.Value,
            UnaryOp.Plus => operand.Value,
            _ => null,
        };
    }

    private double? EvaluateFloatBinary(BinaryExpr expr)
    {
        var left = EvaluateFloat(expr.Left);
        var right = EvaluateFloat(expr.Right);
        if (left == null || right == null) return null;

        double l = left.Value, r = right.Value;
        return expr.Op switch
        {
            BinaryOp.Add => l + r,
            BinaryOp.Sub => l - r,
            BinaryOp.Mul => l * r,
            BinaryOp.Div => r != 0 ? l / r : null,
            _ => null,
        };
    }
}
