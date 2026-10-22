using System.Text;

namespace SLANGCompiler.HuBasic.Text;

/// <summary>
/// 入力テキストを X1 の文字コード列に変換したソース。
/// 内部表現は「1 文字 = X1 の 1 バイト (0x00-0xFF)」の string とする
/// (半角カナは 0xA1-0xDF)。
/// </summary>
public sealed class HuSource
{
    public sealed record Line(int SourceLine, string Text);

    public IReadOnlyList<Line> Lines { get; }

    private HuSource(IReadOnlyList<Line> lines) => Lines = lines;

    public static HuSource FromText(string text, HuDiagnosticBag diag)
    {
        var lines = new List<Line>();
        var raw = text.Replace("\r\n", "\n").Replace('\r', '\n').Split('\n');
        for (int i = 0; i < raw.Length; i++)
        {
            var src = raw[i];
            if (src.Length > 0 && src[0] == '﻿') src = src[1..];
            if (src.Length == 0 || src[0] == '\x1A') continue;
            var sb = new StringBuilder(src.Length);
            for (int col = 0; col < src.Length; col++)
            {
                var ch = src[col];
                if (ch == '\x1A') break;
                var code = ToX1(ch);
                if (code < 0)
                {
                    diag.Error("HB0101", $"X1 の文字コードに変換できない文字 '{ch}' (U+{(int)ch:X4})",
                        new HuPos(i + 1, col + 1, 0));
                    continue;
                }
                sb.Append((char)code);
            }
            if (sb.ToString().Trim().Length > 0)
                lines.Add(new Line(i + 1, sb.ToString()));
        }
        return new HuSource(lines);
    }

    public static HuSource FromFile(string path, string encoding, HuDiagnosticBag diag)
    {
        var bytes = File.ReadAllBytes(path);
        string text;
        if (encoding == "sjis")
        {
            Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);
            text = Encoding.GetEncoding(932).GetString(bytes);
        }
        else
        {
            text = new UTF8Encoding(false, true).GetString(bytes);
        }
        return FromText(text, diag);
    }

    /// <summary>Unicode 1 文字を X1 の文字コードに。変換できなければ -1。</summary>
    public static int ToX1(char ch)
    {
        if (ch == '\t') return ' ';
        if (ch >= 0x20 && ch < 0x7F) return ch;
        if (ch >= '｡' && ch <= 'ﾟ') return 0xA1 + (ch - '｡');
        if (ch == '¥') return 0x5C; // ¥
        return -1;
    }
}
