using System.Text;

namespace SLANGCompiler.CodeGen.C;

/// <summary>
/// SLANG の識別子 (関数名 / 変数名 / ラベル名) を oscar64 C で安全に使える
/// 識別子に変換する。
///
/// 変換規則:
///   - 英数字 + <c>_</c> はそのまま
///   - <c>@</c> → <c>_AT_</c> (LabelUtils と同じ慣行)
///   - <c>^</c> → <c>_CARET_</c> (C では <c>.</c> が使えないため LabelUtils と差分)
///   - その他は <c>_UXXXX_</c> (Unicode 4桁 hex)
///   - 先頭が数字なら <c>_</c> prefix を補う
///   - oscar64 / SLANG runtime の予約 prefix (<c>slang_</c>) で始まる user 名は
///     <c>usr_</c> prefix を被せて escape (= runtime 関数と衝突しないようにする)
///
/// 関数 / 変数 / ラベルの prefix (<c>F_</c> / <c>V_</c> / <c>L_</c>) は CEmitter 側で
/// 付与する。ここは純粋な ident sanitize のみ責任を持つ。
/// </summary>
public static class IdentifierMap
{
    private const string ReservedPrefix = "slang_";
    private const string UserEscape = "usr_";

    /// <summary>
    /// SLANG 識別子 → C 安全な ident 名。<see cref="ReservedPrefix"/> で始まる
    /// ユーザー名は <see cref="UserEscape"/> で escape。
    /// </summary>
    public static string Sanitize(string name)
    {
        if (string.IsNullOrEmpty(name)) return "_empty_";

        var core = SanitizeCore(name);

        // slang_ で始まる user 名は runtime と衝突するため usr_ で escape
        if (core.StartsWith(ReservedPrefix, StringComparison.OrdinalIgnoreCase))
        {
            core = UserEscape + core;
        }

        // 先頭が数字なら C 規約違反なので _ prefix を補う
        if (char.IsDigit(core[0]))
        {
            core = "_" + core;
        }

        return core;
    }

    private static string SanitizeCore(string name)
    {
        // 高速パス: 完全に英数字+_ で構成されているならそのまま返す
        bool safe = true;
        foreach (var c in name)
        {
            if (!((c >= 'A' && c <= 'Z') || (c >= 'a' && c <= 'z')
                || (c >= '0' && c <= '9') || c == '_'))
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
            else if (c == '^') sb.Append("_CARET_");
            else if ((c >= 'A' && c <= 'Z') || (c >= 'a' && c <= 'z')
                  || (c >= '0' && c <= '9') || c == '_')
                sb.Append(c);
            else
                sb.Append($"_U{(int)c:X4}_");
        }
        return sb.ToString();
    }
}
