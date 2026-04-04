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
