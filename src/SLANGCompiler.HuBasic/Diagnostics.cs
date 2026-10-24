namespace SLANGCompiler.HuBasic;

public enum HuSeverity { Warning, Error }

/// <summary>
/// 変換時の診断。BASIC の行番号と、入力ファイル上の行・桁を併記する。
/// コードは HB01xx 字句 / HB02xx 構文 / HB03xx 未対応 / HB04xx 型 / HB05xx 行番号 / HB06xx ループ。
/// </summary>
public sealed record HuDiagnostic(HuSeverity Severity, string Code, string Message, int SourceLine, int Column, int BasicLine)
{
    public string Format(string fileName)
    {
        var kind = Severity == HuSeverity.Error ? "error" : "warning";
        var basic = BasicLine > 0 ? $" (BASIC {BasicLine})" : "";
        return $"{fileName}:{SourceLine}:{Column}: {kind} {Code}{basic}: {Message}";
    }
}

public sealed class HuDiagnosticBag
{
    public const int MaxErrors = 50;

    private readonly List<HuDiagnostic> _items = new();

    public IReadOnlyList<HuDiagnostic> Items => _items;
    public bool HasErrors => _items.Any(d => d.Severity == HuSeverity.Error);
    public int ErrorCount => _items.Count(d => d.Severity == HuSeverity.Error);

    public void Error(string code, string message, HuPos pos)
    {
        if (ErrorCount >= MaxErrors) return;
        _items.Add(new HuDiagnostic(HuSeverity.Error, code, message, pos.SourceLine, pos.Column, pos.BasicLine));
    }

    public void Warning(string code, string message, HuPos pos)
        => _items.Add(new HuDiagnostic(HuSeverity.Warning, code, message, pos.SourceLine, pos.Column, pos.BasicLine));
}

/// <summary>ソース上の位置 (入力ファイルの行・桁と BASIC の行番号)。</summary>
public readonly record struct HuPos(int SourceLine, int Column, int BasicLine)
{
    public static readonly HuPos None = new(0, 0, 0);
}
