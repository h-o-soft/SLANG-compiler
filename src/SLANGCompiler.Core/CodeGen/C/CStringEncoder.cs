using System.Text;

namespace SLANGCompiler.CodeGen.C;

/// <summary>
/// SLANG 文字列リテラル → C 文字列リテラル (oscar64 入力) への encode。
///
/// PETSCII 変換は oscar64 の <c>-psci</c> オプション任せ (=
/// <see cref="Runtime.EnvironmentConfig.OscarPetscii"/> = true で default 付与)。
/// この encoder は **C のソース構文として安全な文字列に変換するだけ** が責務。
/// 不可視文字・引用符・バックスラッシュなどを C escape sequence に変換する。
///
/// v1 制約: ASCII printable 中心 (0x20-0x7E)。日本語・SJIS・カナ等の高位
/// バイトは <c>\xNN</c> で出力されるが、oscar64 の charmap 変換対象外になり
/// 表示は崩れる (= README に明記)。
/// </summary>
public static class CStringEncoder
{
    /// <summary>
    /// SLANG 文字列を C 文字列リテラル (両端ダブルクォート付き) に変換。
    /// </summary>
    public static string Encode(string raw)
    {
        var sb = new StringBuilder(raw.Length + 2);
        sb.Append('"');
        foreach (var ch in raw)
        {
            switch (ch)
            {
                case '\\': sb.Append("\\\\"); break;
                case '"':  sb.Append("\\\""); break;
                case '\n': sb.Append("\\n"); break;
                case '\r': sb.Append("\\r"); break;
                case '\t': sb.Append("\\t"); break;
                case '\0': sb.Append("\\0"); break;
                default:
                    if (ch >= 0x20 && ch <= 0x7E)
                    {
                        sb.Append(ch);
                    }
                    else
                    {
                        // 高位バイトは \xNN で。oscar64 charmap 変換対象外なので
                        // 表示は崩れる可能性あり (v1 制約として許容)。
                        sb.Append($"\\x{(int)ch:x2}");
                    }
                    break;
            }
        }
        sb.Append('"');
        return sb.ToString();
    }
}
