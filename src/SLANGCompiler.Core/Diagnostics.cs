using SLANGCompiler.Lexer;

namespace SLANGCompiler;

/// <summary>
/// コンパイルエラー/警告の重大度
/// </summary>
public enum DiagnosticSeverity
{
    Info,
    Warning,
    Error,
}

/// <summary>
/// 診断メッセージ
/// </summary>
public record Diagnostic(DiagnosticSeverity Severity, string Message, SourceSpan Span)
{
    public override string ToString()
    {
        var prefix = Severity switch
        {
            DiagnosticSeverity.Error => "error",
            DiagnosticSeverity.Warning => "warning",
            DiagnosticSeverity.Info => "info",
            _ => "note",
        };
        return $"{Span}: {prefix}: {Message}";
    }
}

/// <summary>
/// 診断メッセージの収集
/// </summary>
public class DiagnosticBag
{
    private readonly List<Diagnostic> _diagnostics = new();

    public const int MaxErrors = 30;

    public IReadOnlyList<Diagnostic> Diagnostics => _diagnostics;
    public bool HasErrors => _diagnostics.Any(d => d.Severity == DiagnosticSeverity.Error);
    public int ErrorCount => _diagnostics.Count(d => d.Severity == DiagnosticSeverity.Error);
    public bool HasReachedMaxErrors => ErrorCount >= MaxErrors;

    public void Report(DiagnosticSeverity severity, string message, SourceSpan span)
    {
        _diagnostics.Add(new Diagnostic(severity, message, span));
    }

    public void Error(string message, SourceSpan span)
    {
        if (HasReachedMaxErrors) return;
        Report(DiagnosticSeverity.Error, message, span);
    }

    public void Warning(string message, SourceSpan span) =>
        Report(DiagnosticSeverity.Warning, message, span);

    public void Info(string message, SourceSpan span) =>
        Report(DiagnosticSeverity.Info, message, span);

    public void WriteTo(TextWriter writer)
    {
        foreach (var diag in _diagnostics)
        {
            writer.WriteLine(diag);
        }
    }
}
