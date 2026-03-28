namespace SLANGCompiler.Lexer;

/// <summary>
/// ソースコード上の位置情報
/// </summary>
public record struct SourceLocation(string FileName, int Line, int Column)
{
    public static readonly SourceLocation Unknown = new("<unknown>", 0, 0);

    public override string ToString() => $"{FileName}:{Line}:{Column}";
}

/// <summary>
/// ソースコード上の範囲
/// </summary>
public record struct SourceSpan(SourceLocation Start, SourceLocation End)
{
    public static readonly SourceSpan Unknown = new(SourceLocation.Unknown, SourceLocation.Unknown);

    public override string ToString() => Start.ToString();
}
