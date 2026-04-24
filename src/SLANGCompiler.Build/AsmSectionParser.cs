using System.Text.RegularExpressions;

namespace SLANGCompiler.Build;

/// <summary>
/// PR-A / PR-B / PR-B2 が ASM ファイルに出力する固定セクション内のラベル名を
/// regex 抽出する汎用パーサ。
///
/// セクションは `; === <Header> ===` で開始し、次のセクション (or EOF) で終わる。
/// 対象セクション内の指定 prefix (`; FUNC <name>` / `; EXTERN <name>`) の
/// 行から name を取り出す。対象セクション外の同形コメントは無視。
/// </summary>
public static class AsmSectionParser
{
    // セクションヘッダ行 `; === ... ===`
    private static readonly Regex SectionHeader = new(
        @"^\s*;\s*===.*===\s*$",
        RegexOptions.Compiled);

    /// <summary>
    /// `; FUNC <name>` 形式の行から name を抽出 (行先頭空白許容、後続コメント許容)
    /// </summary>
    private static readonly Regex FuncLine = new(
        @"^\s*;\s*FUNC\s+([A-Za-z_][A-Za-z0-9_.]*)\b",
        RegexOptions.Compiled);

    /// <summary>
    /// `; EXTERN <name>` 形式の行から name を抽出
    /// </summary>
    private static readonly Regex ExternLine = new(
        @"^\s*;\s*EXTERN\s+([A-Za-z_][A-Za-z0-9_.]*)\b",
        RegexOptions.Compiled);

    /// <summary>
    /// 指定セクション群内の `; FUNC <name>` 行から name を抽出 (重複排除、出現順保持)
    /// </summary>
    public static List<string> ExtractFuncNames(string asmText, IEnumerable<string> targetSectionHeaders)
        => Extract(asmText, targetSectionHeaders, FuncLine);

    /// <summary>
    /// 指定セクション群内の `; EXTERN <name>` 行から name を抽出
    /// </summary>
    public static List<string> ExtractExternNames(string asmText, IEnumerable<string> targetSectionHeaders)
        => Extract(asmText, targetSectionHeaders, ExternLine);

    private static List<string> Extract(string asmText, IEnumerable<string> targetSectionHeaders, Regex lineRegex)
    {
        var targets = new HashSet<string>(targetSectionHeaders, StringComparer.Ordinal);
        var names = new List<string>();
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        bool inTargetSection = false;

        foreach (var rawLine in asmText.Split('\n'))
        {
            var line = rawLine.TrimEnd('\r');
            if (SectionHeader.IsMatch(line))
            {
                inTargetSection = targets.Contains(line.TrimStart());
                continue;
            }
            if (!inTargetSection) continue;
            var m = lineRegex.Match(line);
            if (m.Success && seen.Add(m.Groups[1].Value))
                names.Add(m.Groups[1].Value);
        }
        return names;
    }
}
