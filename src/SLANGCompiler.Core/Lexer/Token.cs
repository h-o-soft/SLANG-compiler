namespace SLANGCompiler.Lexer;

/// <summary>
/// 字句解析で生成されるトークン
/// </summary>
public record struct Token(
    TokenKind Kind,
    string Text,
    SourceSpan Span,
    object? Value = null   // IntegerLiteral→int/long, FloatLiteral→double, StringLiteral→string
)
{
    public int IntValue => Value is int i ? i : Value is long l ? (int)l : 0;
    public double FloatValue => Value is double d ? d : 0.0;
    public string StringValue => Value as string ?? Text;

    public bool IsKeyword => Kind >= TokenKind.Var && Kind <= TokenKind.Cpl;

    public override string ToString() => $"{Kind}({Text}) at {Span}";
}
