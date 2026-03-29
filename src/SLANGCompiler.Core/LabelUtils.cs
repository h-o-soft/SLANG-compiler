using System.Text;

namespace SLANGCompiler;

/// <summary>
/// ソース識別子→ASM安全ラベル名の変換ユーティリティ。
/// アセンブラ(AILZ80ASM)で使用可能な文字（英数字+_）のみで構成されるラベルに変換する。
/// </summary>
internal static class LabelUtils
{
    /// <summary>ソース識別子→ASM安全ラベル名。</summary>
    public static string SanitizeLabel(string name)
    {
        // 既にASM安全ならそのまま返す（高速パス）
        bool safe = true;
        foreach (var c in name)
        {
            if (!((c >= 'A' && c <= 'Z') || (c >= 'a' && c <= 'z') || (c >= '0' && c <= '9') || c == '_'))
            {
                safe = false;
                break;
            }
        }
        if (safe) return name;

        var sb = new StringBuilder(name.Length);
        foreach (var c in name)
        {
            if (c == '@') sb.Append("_AT_");
            else if (c == '^') sb.Append("_CR_");
            else if ((c >= 'A' && c <= 'Z') || (c >= 'a' && c <= 'z') || (c >= '0' && c <= '9') || c == '_')
                sb.Append(c);
            else
                sb.Append($"_U{(int)c:X4}_");
        }
        return sb.ToString();
    }

    /// <summary>ユーザー変数/配列のASMラベル</summary>
    public static string UserVarLabel(string name) => $"_V_{SanitizeLabel(name)}";

    /// <summary>関数内静的変数のASMラベル</summary>
    public static string StaticVarLabel(string funcName, string varName)
        => $"_V_{SanitizeLabel(funcName)}_{SanitizeLabel(varName)}";

    /// <summary>ユーザー定義ラベル(LABEL/GOTO)のASMラベル</summary>
    public static string UserLabel(string name) => $"_LBL_{SanitizeLabel(name)}";
}
