using SLANGCompiler.Lexer;

namespace SLANGCompiler.Parser.Ast;

/// <summary>
/// 式の基底
/// </summary>
public abstract class Expression : AstNode
{
    protected Expression(SourceSpan span) : base(span) { }
}

// -- Literals --

public class IntegerLiteral : Expression
{
    public long Value { get; }
    public IntegerLiteral(long value, SourceSpan span) : base(span) { Value = value; }
    public override T Accept<T>(IAstVisitor<T> visitor) => visitor.VisitIntegerLiteral(this);
}

public class FloatLiteral : Expression
{
    public double Value { get; }
    public FloatLiteral(double value, SourceSpan span) : base(span) { Value = value; }
    public override T Accept<T>(IAstVisitor<T> visitor) => visitor.VisitFloatLiteral(this);
}

public class StringLiteral : Expression
{
    public string Value { get; }
    public StringLiteral(string value, SourceSpan span) : base(span) { Value = value; }
    public override T Accept<T>(IAstVisitor<T> visitor) => visitor.VisitStringLiteral(this);
}

// -- References --

public class IdentifierExpr : Expression
{
    public string Name { get; }
    public IdentifierExpr(string name, SourceSpan span) : base(span) { Name = name; }
    public override T Accept<T>(IAstVisitor<T> visitor) => visitor.VisitIdentifier(this);
}

// -- Binary operations --

public enum BinaryOp
{
    Add, Sub, Mul, Div, Mod,
    SMul, SDiv, SMod,               // signed
    And, Or, Xor,                   // bitwise
    Shl, Shr, SShl, SShr,          // shift (unsigned/signed)
    Eq, Neq, Lt, Gt, Le, Ge,       // comparison (unsigned)
    SLt, SGt, SLe, SGe,            // comparison (signed)
    LogAnd, LogOr,                  // logical
}

public class BinaryExpr : Expression
{
    public BinaryOp Op { get; }
    public Expression Left { get; }
    public Expression Right { get; }

    public BinaryExpr(BinaryOp op, Expression left, Expression right, SourceSpan span) : base(span)
    {
        Op = op;
        Left = left;
        Right = right;
    }

    public override T Accept<T>(IAstVisitor<T> visitor) => visitor.VisitBinaryExpr(this);
}

// -- Unary operations --

public enum UnaryOp
{
    Negate,     // -
    Plus,       // + (no-op)
    Not,        // NOT (logical not)
    Cpl,        // CPL (bitwise complement)
}

public class UnaryExpr : Expression
{
    public UnaryOp Op { get; }
    public Expression Operand { get; }

    public UnaryExpr(UnaryOp op, Expression operand, SourceSpan span) : base(span)
    {
        Op = op;
        Operand = operand;
    }

    public override T Accept<T>(IAstVisitor<T> visitor) => visitor.VisitUnaryExpr(this);
}

// -- Assignment --

public class AssignExpr : Expression
{
    public Expression Target { get; }
    public Expression Value { get; }

    public AssignExpr(Expression target, Expression value, SourceSpan span) : base(span)
    {
        Target = target;
        Value = value;
    }

    public override T Accept<T>(IAstVisitor<T> visitor) => visitor.VisitAssignExpr(this);
}

public enum CompoundAssignOp
{
    AddAssign,  // +=
    SubAssign,  // -=
    MulAssign,  // *=
    DivAssign,  // /=
}

public class CompoundAssignExpr : Expression
{
    public CompoundAssignOp Op { get; }
    public Expression Target { get; }
    public Expression Value { get; }

    public CompoundAssignExpr(CompoundAssignOp op, Expression target, Expression value, SourceSpan span) : base(span)
    {
        Op = op;
        Target = target;
        Value = value;
    }

    public override T Accept<T>(IAstVisitor<T> visitor) => visitor.VisitCompoundAssignExpr(this);
}

// -- Increment/Decrement --

public class IncrementExpr : Expression
{
    public Expression Operand { get; }
    public bool IsIncrement { get; }    // true=++, false=--
    public bool IsPrefix { get; }       // true=prefix, false=postfix

    public IncrementExpr(Expression operand, bool isIncrement, bool isPrefix, SourceSpan span) : base(span)
    {
        Operand = operand;
        IsIncrement = isIncrement;
        IsPrefix = isPrefix;
    }

    public override T Accept<T>(IAstVisitor<T> visitor) => visitor.VisitIncrementExpr(this);
}

// -- Function call --

public class CallExpr : Expression
{
    public Expression Function { get; }
    public List<Expression> Arguments { get; }

    public CallExpr(Expression function, List<Expression> arguments, SourceSpan span) : base(span)
    {
        Function = function;
        Arguments = arguments;
    }

    public override T Accept<T>(IAstVisitor<T> visitor) => visitor.VisitCallExpr(this);
}

// -- Array access --

/// <summary>
/// 配列アクセス。多次元はネストではなくIndicesリストで表現。
/// a[i][j] → ArrayAccessExpr(a, [i, j])
/// </summary>
public class ArrayAccessExpr : Expression
{
    public Expression Array { get; }
    public List<Expression> Indices { get; }

    public ArrayAccessExpr(Expression array, List<Expression> indices, SourceSpan span) : base(span)
    {
        Array = array;
        Indices = indices;
    }

    public override T Accept<T>(IAstVisitor<T> visitor) => visitor.VisitArrayAccessExpr(this);
}

// -- Conditional --

public class ConditionalExpr : Expression
{
    public Expression Condition { get; }
    public Expression TrueExpr { get; }
    public Expression FalseExpr { get; }

    public ConditionalExpr(Expression condition, Expression trueExpr, Expression falseExpr, SourceSpan span) : base(span)
    {
        Condition = condition;
        TrueExpr = trueExpr;
        FalseExpr = falseExpr;
    }

    public override T Accept<T>(IAstVisitor<T> visitor) => visitor.VisitConditionalExpr(this);
}

// -- Comma expression --

public class CommaExpr : Expression
{
    public Expression Left { get; }
    public Expression Right { get; }

    public CommaExpr(Expression left, Expression right, SourceSpan span) : base(span)
    {
        Left = left;
        Right = right;
    }

    public override T Accept<T>(IAstVisitor<T> visitor) => visitor.VisitCommaExpr(this);
}

// -- Address-of --

public class AddressOfExpr : Expression
{
    public Expression Operand { get; }
    public AddressOfExpr(Expression operand, SourceSpan span) : base(span) { Operand = operand; }
    public override T Accept<T>(IAstVisitor<T> visitor) => visitor.VisitAddressOfExpr(this);
}

// -- HIGH/LOW --

public class HighLowExpr : Expression
{
    public bool IsHigh { get; }
    public Expression Operand { get; }

    public HighLowExpr(bool isHigh, Expression operand, SourceSpan span) : base(span)
    {
        IsHigh = isHigh;
        Operand = operand;
    }

    public override T Accept<T>(IAstVisitor<T> visitor) => visitor.VisitHighLowExpr(this);
}

// -- CODE expression --

public class CodeExpr : Expression
{
    public List<Expression> Values { get; }
    public CodeExpr(List<Expression> values, SourceSpan span) : base(span) { Values = values; }
    public override T Accept<T>(IAstVisitor<T> visitor) => visitor.VisitCodeExpr(this);
}

// -- Type cast (% prefix) --

public class CastExpr : Expression
{
    public DataSize TargetSize { get; }
    public Expression Operand { get; }

    public CastExpr(DataSize targetSize, Expression operand, SourceSpan span) : base(span)
    {
        TargetSize = targetSize;
        Operand = operand;
    }

    public override T Accept<T>(IAstVisitor<T> visitor) => visitor.VisitCastExpr(this);
}

// -- String functions (PRINT context) --

public class StringFuncExpr : Expression
{
    public string FuncName { get; }   // "FORM$", "DECI$", "/", "!", "%" etc.
    public List<Expression> Arguments { get; }

    public StringFuncExpr(string funcName, List<Expression> arguments, SourceSpan span) : base(span)
    {
        FuncName = funcName;
        Arguments = arguments;
    }

    public override T Accept<T>(IAstVisitor<T> visitor) => visitor.VisitStringFuncExpr(this);
}
