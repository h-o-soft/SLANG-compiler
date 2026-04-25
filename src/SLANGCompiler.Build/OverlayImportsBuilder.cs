using System.Text;
using System.Text.RegularExpressions;
using SLANGCompiler.Runtime;

namespace SLANGCompiler.Build;

/// <summary>
/// PR-B の核心: overlay ASM 内の `; EXTERN <name>` リストと main.sym の交集合
/// から filtered sym (= overlay 専用 EQU 集) を生成する。
///
/// raw main.sym をそのまま AILZ80ASM に渡すと compiler 内部ラベル / string /
/// module private 等が overlay 側の同名ラベルと衝突するため、必ず必要分だけに
/// 絞った imports.asm を経由させる。
/// </summary>
public static class OverlayImportsBuilder
{
    // PR-A の overlay ASM 出力で「main からの参照」を列挙する固定セクション群。
    // これら以外のセクション内の `; EXTERN` は無視する (将来別コメント混入時の
    // 誤検出防止)。
    private static readonly string[] TargetSectionHeaders =
    {
        "; === Shared Runtime References (resolved via two-stage assembly) ===",
        "; === Shared Symbols (from main) ===",
        "; === String references (from main) ===",
    };

    // `; EXTERN <name>` または `; EXTERN <name>  ; <comment>`
    private static readonly Regex ExternLine = new(
        @"^\s*;\s*EXTERN\s+([A-Za-z_][A-Za-z0-9_.]*)\b",
        RegexOptions.Compiled);

    // セクション境界 (任意の `; ===` 行)
    private static readonly Regex SectionHeader = new(
        @"^\s*;\s*===.*===\s*$",
        RegexOptions.Compiled);

    /// <summary>
    /// overlay ASM から `; EXTERN` 名を抽出する。対象セクション内に限定。
    /// </summary>
    public static List<string> ExtractExternNames(string overlayAsmText)
    {
        var names = new List<string>();
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        bool inTargetSection = false;

        foreach (var rawLine in overlayAsmText.Split('\n'))
        {
            var line = rawLine.TrimEnd('\r');

            if (SectionHeader.IsMatch(line))
            {
                // 新しいセクションヘッダ。target かどうか判定し inTargetSection を更新
                inTargetSection = TargetSectionHeaders.Any(h =>
                    line.TrimStart().Equals(h, StringComparison.Ordinal));
                continue;
            }

            if (!inTargetSection) continue;

            var m = ExternLine.Match(line);
            if (m.Success)
            {
                var name = m.Groups[1].Value;
                if (seen.Add(name)) names.Add(name);
            }
        }

        return names;
    }

    /// <summary>
    /// overlay ASM の `; EXTERN` リストと main.sym の交集合を imports.asm として書き出す。
    /// 戻り値は (出力 path, 未解決ラベルのリスト)。
    /// </summary>
    public static (string OutputPath, List<string> Unresolved) Build(
        string mainSymPath, string overlayAsmPath, string outputPath)
    {
        var overlayText = File.ReadAllText(overlayAsmPath);
        var externs = ExtractExternNames(overlayText);
        var symbols = SymFileReader.ReadFile(mainSymPath);

        var unresolved = new List<string>();
        var sb = new StringBuilder();
        sb.AppendLine("; SLANG slangbuild — filtered imports for two-stage assembly");
        sb.AppendLine($"; source overlay: {Path.GetFileName(overlayAsmPath)}");
        sb.AppendLine($"; main sym:       {Path.GetFileName(mainSymPath)}");
        sb.AppendLine();

        foreach (var name in externs)
        {
            if (symbols.TryGetValue(name, out var addr))
            {
                sb.AppendLine($"{name} equ ${addr:X4}");
            }
            else
            {
                unresolved.Add(name);
            }
        }

        File.WriteAllText(outputPath, sb.ToString());
        return (outputPath, unresolved);
    }
}
