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
            if (sb.ToString().Trim(' ').Length > 0)
                lines.Add(new Line(i + 1, sb.ToString()));
        }
        return new HuSource(lines);
    }

    /// <summary>
    /// X1 の文字コードのバイト列 (X1 でアスキーセーブしたファイル) から作る。1 バイトが X1 の 1 文字
    /// (グラフィックの文字・カナ・カーソルの制御コードもそのまま)。CR・LF・CR LF で行を分け、0x1A で終わる
    /// </summary>
    public static HuSource FromX1Bytes(byte[] bytes)
    {
        var lines = new List<Line>();
        var sb = new StringBuilder();
        int lineNo = 1;
        void Flush()
        {
            // 空白は X1 の 0x20 だけ (0x85・0xA0 などは X1 の文字なので、Unicode の空白として扱わない)
            if (sb.ToString().Trim(' ').Length > 0) lines.Add(new Line(lineNo, sb.ToString()));
            sb.Clear();
            lineNo++;
        }
        for (int i = 0; i < bytes.Length; i++)
        {
            var b = bytes[i];
            if (b == 0x1A) break;
            if (b == 0x0D)
            {
                if (i + 1 < bytes.Length && bytes[i + 1] == 0x0A) i++;
                Flush();
                continue;
            }
            if (b == 0x0A) { Flush(); continue; }
            sb.Append(b == '\t' ? ' ' : (char)b);
        }
        if (sb.Length > 0) Flush();
        return new HuSource(lines);
    }

    /// <summary>
    /// ファイルを読む。encoding は auto (既定。UTF-8 として読めなければ X1 の文字コードとして読み、warning)・utf8・sjis・x1。
    /// UTF-8 として読めないファイルを utf8 で読むと、例外ではなく error HB0111 にする (auto の warning は HB0110)
    /// </summary>
    public static HuSource FromFile(string path, string encoding, HuDiagnosticBag diag)
        => FromBytes(File.ReadAllBytes(path), encoding, diag);

    /// <summary>ファイルの中身のバイト列を読む。encoding と診断は <see cref="FromFile"/> と同じ</summary>
    public static HuSource FromBytes(byte[] bytes, string encoding, HuDiagnosticBag diag)
    {
        string text;
        if (encoding == "x1") return FromX1Bytes(bytes);
        if (encoding == "sjis")
        {
            Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);
            text = Encoding.GetEncoding(932).GetString(bytes);
        }
        else
        {
            int bad = FirstInvalidUtf8(bytes);
            if (bad >= 0)
            {
                var pos = PosOfByte(bytes, bad);
                if (encoding == "auto")
                {
                    diag.Warning("HB0110", $"UTF-8 として読めないので (${bytes[bad]:X2})、X1 の文字コード (アスキーセーブしたファイル) として読みました", pos);
                    return FromX1Bytes(bytes);
                }
                diag.Error("HB0111", $"UTF-8 として読めないバイト ${bytes[bad]:X2} があります (桁は行の中のバイトの位置)。" +
                                     "X1 でアスキーセーブしたファイルなら --input-encoding x1 を指定してください", pos);
                return new HuSource(new List<Line>());
            }
            text = new UTF8Encoding(false, true).GetString(bytes);
        }
        return FromText(text, diag);
    }

    /// <summary>UTF-8 として読めない最初のバイトの位置 (すべて読めれば -1)</summary>
    private static int FirstInvalidUtf8(byte[] bytes)
    {
        var span = bytes.AsSpan();
        int at = 0;
        while (at < span.Length)
        {
            if (Rune.DecodeFromUtf8(span[at..], out _, out int used) != System.Buffers.OperationStatus.Done) return at;
            at += used;
        }
        return -1;
    }

    /// <summary>バイトの位置の行と桁 (どちらも 1 から。行は CR LF・CR・LF で数える。桁は行の中のバイトの位置)</summary>
    private static HuPos PosOfByte(byte[] bytes, int index)
    {
        int line = 1, start = 0;
        for (int i = 0; i < index; i++)
        {
            if (bytes[i] == 0x0D)
            {
                if (i + 1 < index && bytes[i + 1] == 0x0A) i++;
                line++;
                start = i + 1;
            }
            else if (bytes[i] == 0x0A)
            {
                line++;
                start = i + 1;
            }
        }
        return new HuPos(line, index - start + 1, 0);
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
