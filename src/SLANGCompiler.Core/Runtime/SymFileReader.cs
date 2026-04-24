using System.Globalization;
using System.Text.RegularExpressions;

namespace SLANGCompiler.Runtime;

/// <summary>
/// AILZ80ASM が出力する `.sym` ファイル (`-sm minimal-equ` または `normal`) を
/// パースして label → address の dict を返す。
///
/// PR-B (二段アセンブル toolchain) で main.sym を読み、overlay 用 filtered sym
/// を生成する経路で使う。Core 配置は将来の他用途 (`slangbuild` 以外のツール、
/// 例えば disk image 生成 / debugger inspection 等) からの再利用を想定。
/// </summary>
public static class SymFileReader
{
    // minimal-equ: `<label> equ $XXXX`
    private static readonly Regex MinimalEquLine = new(
        @"^\s*([A-Za-z_][A-Za-z0-9_.]*)\s+equ\s+\$([0-9A-Fa-f]+)\s*$",
        RegexOptions.Compiled);

    // normal:      `<XXXX> <label>`
    private static readonly Regex NormalLine = new(
        @"^\s*([0-9A-Fa-f]{1,8})\s+([A-Za-z_][A-Za-z0-9_.]*)\s*$",
        RegexOptions.Compiled);

    /// <summary>パスからファイルを読んでパース。</summary>
    public static Dictionary<string, int> ReadFile(string path)
    {
        var text = File.ReadAllText(path);
        return Parse(text);
    }

    /// <summary>
    /// .sym のテキストをパース。両形式 (minimal-equ / normal) を自動判別。
    /// 同じラベルが重複していたら後勝ち。コメント行 (`;` 始まり) と空行は無視。
    /// </summary>
    public static Dictionary<string, int> Parse(string text)
    {
        var result = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
        foreach (var rawLine in text.Split('\n'))
        {
            var line = rawLine.TrimEnd('\r').TrimStart();
            if (line.Length == 0) continue;
            if (line.StartsWith(';')) continue;

            var m = MinimalEquLine.Match(line);
            if (m.Success)
            {
                if (int.TryParse(m.Groups[2].Value, NumberStyles.HexNumber,
                                  CultureInfo.InvariantCulture, out var addr))
                {
                    result[m.Groups[1].Value] = addr;
                }
                continue;
            }

            m = NormalLine.Match(line);
            if (m.Success)
            {
                if (int.TryParse(m.Groups[1].Value, NumberStyles.HexNumber,
                                  CultureInfo.InvariantCulture, out var addr))
                {
                    result[m.Groups[2].Value] = addr;
                }
            }
        }
        return result;
    }
}
