using SLANGCompiler.Lexer;

namespace SLANGCompiler.Parser.Ast;

/// <summary>
/// 文の基底
/// </summary>
public abstract class Statement : AstNode
{
    protected Statement(SourceSpan span) : base(span) { }
}

/// <summary>
/// ブロック文 BEGIN...END / [...] / {...} / (...)
/// </summary>
public class Block : Statement
{
    public List<AstNode> Statements { get; }

    public Block(List<AstNode> statements, SourceSpan span) : base(span)
    {
        Statements = statements;
    }

    public override T Accept<T>(IAstVisitor<T> visitor) => visitor.VisitBlock(this);
}

/// <summary>
/// 式文
/// </summary>
public class ExpressionStmt : Statement
{
    public Expression Expr { get; }
    public ExpressionStmt(Expression expr, SourceSpan span) : base(span) { Expr = expr; }
    public override T Accept<T>(IAstVisitor<T> visitor) => visitor.VisitExpressionStmt(this);
}

/// <summary>
/// IF文
/// </summary>
public class IfStmt : Statement
{
    public List<(Expression Condition, AstNode Body)> Branches { get; }  // IF + ELIF
    public AstNode? ElseBody { get; }

    public IfStmt(List<(Expression Condition, AstNode Body)> branches, AstNode? elseBody, SourceSpan span) : base(span)
    {
        Branches = branches;
        ElseBody = elseBody;
    }

    public override T Accept<T>(IAstVisitor<T> visitor) => visitor.VisitIfStmt(this);
}

/// <summary>
/// WHILE文
/// </summary>
public class WhileStmt : Statement
{
    public Expression Condition { get; }
    public AstNode Body { get; }

    public WhileStmt(Expression condition, AstNode body, SourceSpan span) : base(span)
    {
        Condition = condition;
        Body = body;
    }

    public override T Accept<T>(IAstVisitor<T> visitor) => visitor.VisitWhileStmt(this);
}

/// <summary>
/// REPEAT...UNTIL文
/// </summary>
public class RepeatStmt : Statement
{
    public AstNode Body { get; }
    public Expression Condition { get; }

    public RepeatStmt(AstNode body, Expression condition, SourceSpan span) : base(span)
    {
        Body = body;
        Condition = condition;
    }

    public override T Accept<T>(IAstVisitor<T> visitor) => visitor.VisitRepeatStmt(this);
}

/// <summary>
/// LOOP文（無限ループ）
/// </summary>
public class LoopStmt : Statement
{
    public AstNode Body { get; }
    public LoopStmt(AstNode body, SourceSpan span) : base(span) { Body = body; }
    public override T Accept<T>(IAstVisitor<T> visitor) => visitor.VisitLoopStmt(this);
}

/// <summary>
/// FOR文
/// </summary>
public class ForStmt : Statement
{
    public string Variable { get; }
    public Expression From { get; }
    public Expression To { get; }
    public bool IsDownTo { get; }
    public AstNode Body { get; }

    public ForStmt(string variable, Expression from, Expression to, bool isDownTo, AstNode body, SourceSpan span) : base(span)
    {
        Variable = variable;
        From = from;
        To = to;
        IsDownTo = isDownTo;
        Body = body;
    }

    public override T Accept<T>(IAstVisitor<T> visitor) => visitor.VisitForStmt(this);
}

/// <summary>
/// CASE文のブランチ
/// </summary>
public class CaseBranch
{
    public Expression? Value { get; }           // null = OTHERS
    public Expression? RangeEnd { get; }        // TO 指定がある場合の終端
    public AstNode? Body { get; }               // null = カンマ先行値（次のbody付きブランチにフォールスルー）

    public CaseBranch(Expression? value, Expression? rangeEnd, AstNode? body)
    {
        Value = value;
        RangeEnd = rangeEnd;
        Body = body;
    }
}

/// <summary>
/// CASE文
/// </summary>
public class CaseStmt : Statement
{
    public Expression Expr { get; }
    public List<CaseBranch> Branches { get; }

    public CaseStmt(Expression expr, List<CaseBranch> branches, SourceSpan span) : base(span)
    {
        Expr = expr;
        Branches = branches;
    }

    public override T Accept<T>(IAstVisitor<T> visitor) => visitor.VisitCaseStmt(this);
}

/// <summary>
/// EXIT文
/// </summary>
public class ExitStmt : Statement
{
    public Expression? Level { get; }       // EXIT(n)
    public string? TargetLabel { get; }     // EXIT TO label

    public ExitStmt(Expression? level, string? targetLabel, SourceSpan span) : base(span)
    {
        Level = level;
        TargetLabel = targetLabel;
    }

    public override T Accept<T>(IAstVisitor<T> visitor) => visitor.VisitExitStmt(this);
}

/// <summary>
/// CONTINUE文
/// </summary>
public class ContinueStmt : Statement
{
    public ContinueStmt(SourceSpan span) : base(span) { }
    public override T Accept<T>(IAstVisitor<T> visitor) => visitor.VisitContinueStmt(this);
}

/// <summary>
/// RETURN文
/// </summary>
public class ReturnStmt : Statement
{
    public Expression? Value { get; }
    public ReturnStmt(Expression? value, SourceSpan span) : base(span) { Value = value; }
    public override T Accept<T>(IAstVisitor<T> visitor) => visitor.VisitReturnStmt(this);
}

/// <summary>
/// GOTO文
/// </summary>
public class GotoStmt : Statement
{
    public string Label { get; }
    public GotoStmt(string label, SourceSpan span) : base(span) { Label = label; }
    public override T Accept<T>(IAstVisitor<T> visitor) => visitor.VisitGotoStmt(this);
}

/// <summary>
/// ラベル文 (IDENTIFIER:)
/// </summary>
public class LabelStmt : Statement
{
    public string Label { get; }
    public LabelStmt(string label, SourceSpan span) : base(span) { Label = label; }
    public override T Accept<T>(IAstVisitor<T> visitor) => visitor.VisitLabelStmt(this);
}

/// <summary>
/// PRINT文
/// </summary>
public class PrintStmt : Statement
{
    public List<Expression> Arguments { get; }
    public PrintStmt(List<Expression> arguments, SourceSpan span) : base(span)
    {
        Arguments = arguments;
    }
    public override T Accept<T>(IAstVisitor<T> visitor) => visitor.VisitPrintStmt(this);
}
