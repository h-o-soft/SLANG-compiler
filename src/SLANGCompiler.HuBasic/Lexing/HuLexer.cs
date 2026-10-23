using System.Text;

namespace SLANGCompiler.HuBasic.Lexing;

/// <summary>
/// 1 行分 (行番号を除いた本体) をトークンに分ける。
/// - 予約語は空白なしで詰めて書いても、識別子の位置で最長一致で切り出す
///   (Hu-BASIC の変数名は予約語で始められないため)。
/// - DATA の後ろは、引用符の外の ':' か行末までを生の項目として読む。
/// - ' と REM 以降は行末までコメントとして読み飛ばす。
/// </summary>
public sealed class HuLexer
{
    private static readonly string[] TwoCharSymbols = { "<>", "><", "<=", "=<", ">=", "=>" };
    private const string OneCharSymbols = "+-*/\\=<>(),;:#";

    private readonly string _text;
    private readonly int _columnOffset;
    private readonly HuPos _linePos;
    private readonly HuDiagnosticBag _diag;
    private int _pos;

    /// <param name="text">行番号の後ろの本体 (X1 文字コードの string)</param>
    /// <param name="columnOffset">本体の先頭が入力行の何桁目か (0 始まり)</param>
    public HuLexer(string text, int columnOffset, HuPos linePos, HuDiagnosticBag diag)
    {
        _text = text;
        _columnOffset = columnOffset;
        _linePos = linePos;
        _diag = diag;
    }

    public List<HuToken> Tokenize()
    {
        var tokens = new List<HuToken>();
        while (true)
        {
            SkipSpaces();
            if (_pos >= _text.Length) break;
            int start = _pos;
            char ch = _text[_pos];

            if (ch == '\'')
            {
                tokens.Add(new HuToken(HuTokenKind.Comment, _text[(_pos + 1)..], 0, Col(start)));
                _pos = _text.Length;
                break;
            }
            if (ch == '"')
            {
                tokens.Add(new HuToken(HuTokenKind.String, ReadString(), 0, Col(start)));
                continue;
            }
            if (char.IsAsciiDigit(ch) || (ch == '.' && _pos + 1 < _text.Length && char.IsAsciiDigit(_text[_pos + 1])))
            {
                tokens.Add(ReadNumber());
                continue;
            }
            if (ch == '&' && _pos + 1 < _text.Length && "HhOoBb".Contains(_text[_pos + 1]))
            {
                tokens.Add(ReadRadixNumber());
                continue;
            }
            if (ch == '?')
            {
                _pos++;
                tokens.Add(new HuToken(HuTokenKind.Keyword, "PRINT", 0, Col(start)));
                continue;
            }
            if (char.IsAsciiLetter(ch))
            {
                var kw = HuKeywords.MatchAt(_text, _pos);
                if (kw != null)
                {
                    _pos += kw.Length;
                    tokens.Add(new HuToken(HuTokenKind.Keyword, kw, 0, Col(start)));
                    if (kw == "REM")
                    {
                        tokens.Add(new HuToken(HuTokenKind.Comment, _text[_pos..], 0, Col(_pos)));
                        _pos = _text.Length;
                        break;
                    }
                    if (kw == "DATA")
                    {
                        tokens.Add(ReadDataItems());
                        continue;
                    }
                    continue;
                }
                tokens.Add(ReadIdent());
                continue;
            }
            var sym2 = _pos + 1 < _text.Length ? _text.Substring(_pos, 2) : "";
            if (TwoCharSymbols.Contains(sym2))
            {
                _pos += 2;
                var norm = sym2 switch { "><" => "<>", "=<" => "<=", "=>" => ">=", _ => sym2 };
                tokens.Add(new HuToken(HuTokenKind.Symbol, norm, 0, Col(start)));
                continue;
            }
            if (OneCharSymbols.Contains(ch))
            {
                _pos++;
                tokens.Add(new HuToken(HuTokenKind.Symbol, ch.ToString(), 0, Col(start)));
                continue;
            }
            _diag.Error("HB0102", $"文として解釈できない文字 '{Printable(ch)}'", At(start));
            _pos++;
        }
        tokens.Add(new HuToken(HuTokenKind.End, "", 0, Col(_pos)));
        return tokens;
    }

    private void SkipSpaces()
    {
        while (_pos < _text.Length && _text[_pos] == ' ') _pos++;
    }

    private int Col(int pos) => _columnOffset + pos + 1;
    private HuPos At(int pos) => _linePos with { Column = Col(pos) };

    private static string Printable(char ch) => ch >= 0x20 && ch < 0x7F ? ch.ToString() : $"\\x{(int)ch:X2}";

    // "..." 行末で閉じていなくてもよい (Hu-BASIC の規則)
    private string ReadString()
    {
        _pos++; // "
        var sb = new StringBuilder();
        while (_pos < _text.Length && _text[_pos] != '"')
            sb.Append(_text[_pos++]);
        if (_pos < _text.Length) _pos++; // 閉じの "
        return sb.ToString();
    }

    private HuToken ReadNumber()
    {
        int start = _pos;
        bool isFloat = false;
        while (_pos < _text.Length && char.IsAsciiDigit(_text[_pos])) _pos++;
        if (_pos < _text.Length && _text[_pos] == '.')
        {
            isFloat = true;
            _pos++;
            while (_pos < _text.Length && char.IsAsciiDigit(_text[_pos])) _pos++;
        }
        // 指数 (E / D) は、直後が数字か符号+数字のときだけ数値の一部とみなす
        if (_pos < _text.Length && "EeDd".Contains(_text[_pos]))
        {
            int p = _pos + 1;
            if (p < _text.Length && (_text[p] == '+' || _text[p] == '-')) p++;
            if (p < _text.Length && char.IsAsciiDigit(_text[p]))
            {
                isFloat = true;
                _pos = p;
                while (_pos < _text.Length && char.IsAsciiDigit(_text[_pos])) _pos++;
            }
        }
        // 型の接尾辞 (! # %) は数値リテラルにも付けられる
        if (_pos < _text.Length && (_text[_pos] == '!' || _text[_pos] == '#'))
        {
            isFloat = true;
            _pos++;
        }
        else if (_pos < _text.Length && _text[_pos] == '%')
        {
            _pos++;
        }
        var text = _text[start.._pos];
        if (isFloat)
            return new HuToken(HuTokenKind.Float, text, 0, Col(start));
        bool intSuffix = text.EndsWith('%');
        var digits = text.TrimEnd('%');
        // 32768 以上の 10 進の数は、Hu-BASIC と同じく浮動小数の定数 (変数に入れるまでそのままの値)
        if (!intSuffix && (!long.TryParse(digits, out var big) || big > 32767))
            return new HuToken(HuTokenKind.Float, text, 0, Col(start));
        if (!long.TryParse(digits, out var v) || v > 65535)
        {
            _diag.Error("HB0103", $"整数として大きすぎる数値 {digits}", At(start));
            v = 0;
        }
        else if (v > 32767)
        {
            // % の付いた 32768 以上の数は、16 ビットの整数として扱う
            _diag.Warning("HB0109", $"数値 {text} は 16 ビットの整数 {v - 65536} として扱います", At(start));
        }
        return new HuToken(HuTokenKind.Number, text, (int)v, Col(start));
    }

    private HuToken ReadRadixNumber()
    {
        int start = _pos;
        char radixCh = char.ToUpperInvariant(_text[_pos + 1]);
        int radix = radixCh switch { 'H' => 16, 'O' => 8, _ => 2 };
        _pos += 2;
        long v = 0;
        int digits = 0;
        bool tooLarge = false;
        while (_pos < _text.Length)
        {
            int d = DigitValue(_text[_pos]);
            if (d < 0 || d >= radix) break;
            // 足す前に 65535 を超えるかを見る (桁が多いと long でも回り込むため)。超えても残りの桁は読み切る
            if (!tooLarge)
            {
                v = v * radix + d;
                if (v > 65535) tooLarge = true;
            }
            digits++;
            _pos++;
        }
        if (digits == 0 || tooLarge)
        {
            _diag.Error("HB0103", $"不正な {radix} 進数 {_text[start.._pos]}", At(start));
            v = 0;
        }
        return new HuToken(HuTokenKind.Number, _text[start.._pos], (int)v, Col(start));
    }

    private static int DigitValue(char c)
    {
        if (char.IsAsciiDigit(c)) return c - '0';
        c = char.ToUpperInvariant(c);
        if (c >= 'A' && c <= 'F') return c - 'A' + 10;
        return -1;
    }

    private HuToken ReadIdent()
    {
        int start = _pos;
        var sb = new StringBuilder();
        while (_pos < _text.Length && char.IsAsciiLetterOrDigit(_text[_pos]))
            sb.Append(char.ToUpperInvariant(_text[_pos++]));
        if (_pos < _text.Length && "$%!#".Contains(_text[_pos]))
        {
            // '#' は「A#」の倍精度の接尾辞のときだけ。PRINT#0 の # は予約語の直後なのでここに来ない
            sb.Append(_text[_pos++]);
        }
        return new HuToken(HuTokenKind.Ident, sb.ToString(), 0, Col(start));
    }

    /// <summary>DATA の後ろを、引用符の外の ':' か行末まで生の項目として読む。</summary>
    private HuToken ReadDataItems()
    {
        int start = _pos;
        var items = new List<string>();
        var cur = new StringBuilder();
        bool quoted = false;      // 項目が引用符で始まった
        bool inQuote = false;
        bool sawQuoteEnd = false;
        while (_pos < _text.Length)
        {
            char ch = _text[_pos];
            if (inQuote)
            {
                if (ch == '"') { inQuote = false; sawQuoteEnd = true; }
                else cur.Append(ch);
                _pos++;
                continue;
            }
            if (ch == ':') break;
            if (ch == ',')
            {
                items.Add(FinishItem(cur, quoted));
                cur.Clear();
                quoted = false;
                sawQuoteEnd = false;
                _pos++;
                continue;
            }
            if (ch == '"' && !quoted && cur.ToString().Trim().Length == 0)
            {
                cur.Clear();
                quoted = true;
                inQuote = true;
                _pos++;
                continue;
            }
            if (!(quoted && sawQuoteEnd))
                cur.Append(ch);
            _pos++;
        }
        items.Add(FinishItem(cur, quoted));
        foreach (var it in items)
        {
            if (it.Length > 255)
                _diag.Error("HB0106", "DATA の項目が 255 バイトを超えています", At(start));
        }
        return new HuToken(HuTokenKind.Data, "DATA", 0, Col(start)) { DataItems = items };
    }

    /// <summary>引用符のない項目は頭の空白だけを除く (後ろの空白は残す。Hu-BASIC の実測: DATA  X , は "X ")</summary>
    private static string FinishItem(StringBuilder cur, bool quoted)
        => quoted ? cur.ToString() : cur.ToString().TrimStart(' ');
}
