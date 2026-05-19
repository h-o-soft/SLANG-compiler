namespace SLANGCompiler.Lexer;

/// <summary>
/// SLANG手書き字句解析器。
/// 現行のGplex定義を参考に、同等のトークン列を生成する。
/// </summary>
public class Lexer
{
    private readonly string _source;
    private readonly string _fileName;
    private int _pos;
    private int _line;
    private int _column;
    private bool _nextBraceIsArray;

    // キーワードテーブル（大文字小文字を区別しない）
    private static readonly Dictionary<string, TokenKind> Keywords = new(StringComparer.OrdinalIgnoreCase)
    {
        ["VAR"] = TokenKind.Var,
        ["BYTE"] = TokenKind.Byte,
        ["WORD"] = TokenKind.Word,
        ["ARRAY"] = TokenKind.Array,
        ["CONST"] = TokenKind.Const,
        ["MACHINE"] = TokenKind.Machine,
        ["CFUNC"] = TokenKind.Cfunc,
        ["VOID"] = TokenKind.Void,
        ["IF"] = TokenKind.If,
        ["THEN"] = TokenKind.Then,
        ["ELSE"] = TokenKind.Else,
        ["ELIF"] = TokenKind.Elif,
        ["ELSEIF"] = TokenKind.Elif,
        ["EF"] = TokenKind.Ef,
        ["ENDIF"] = TokenKind.EndIf,
        ["WHILE"] = TokenKind.While,
        ["DO"] = TokenKind.Do,
        ["WEND"] = TokenKind.Wend,
        ["REPEAT"] = TokenKind.Repeat,
        ["UNTIL"] = TokenKind.Until,
        ["CASE"] = TokenKind.Case,
        ["OTHERS"] = TokenKind.Others,
        ["OF"] = TokenKind.Of,
        ["LOOP"] = TokenKind.Loop,
        ["FOR"] = TokenKind.For,
        ["TO"] = TokenKind.To,
        ["DOWNTO"] = TokenKind.DownTo,
        ["NEXT"] = TokenKind.Next,
        ["EXIT"] = TokenKind.Exit,
        ["CONTINUE"] = TokenKind.Continue,
        ["RETURN"] = TokenKind.Return,
        ["GOTO"] = TokenKind.Goto,
        ["BEGIN"] = TokenKind.Begin,
        ["END"] = TokenKind.End,
        ["ORG"] = TokenKind.Org,
        ["WORK"] = TokenKind.Work,
        ["OFFSET"] = TokenKind.Offset,
        // #MODULE ヘッダのランタイム集約ポリシー
        ["RESIDENT"] = TokenKind.Resident,
        ["SELFCONTAIN"] = TokenKind.SelfContain,
        ["AUTO"] = TokenKind.Auto,
        ["PRINT"] = TokenKind.Print,
        ["CODE"] = TokenKind.Code,
        ["HIGH"] = TokenKind.High,
        ["LOW"] = TokenKind.Low,
        ["NOT"] = TokenKind.Not,
        ["CPL"] = TokenKind.Cpl,
        ["MOD"] = TokenKind.Mod,
        ["FLOAT"] = TokenKind.Float,  // FLOATもキーワードとして認識(%%と同等)
        ["ASM"] = TokenKind.Asm,
        ["AND"] = TokenKind.And,
        ["OR"] = TokenKind.Or,
        ["XOR"] = TokenKind.Xor,
        ["EF"] = TokenKind.Ef,
    };

    // 文字列関数名 (case-insensitive)
    private static readonly HashSet<string> StringFuncs = new(StringComparer.OrdinalIgnoreCase)
    {
        "FORM$", "DECI$", "PN$", "HEX2$", "HEX4$", "MSG$", "MSX$",
        "STR$", "CHR$", "SPC$", "CR$", "TAB$", "FL$",
    };

    public Lexer(string source, string fileName = "<input>")
    {
        _source = source;
        _fileName = fileName;
        _pos = 0;
        _line = 1;
        _column = 1;
    }

    public List<Token> Tokenize()
    {
        var tokens = new List<Token>();
        while (true)
        {
            var token = NextToken();
            tokens.Add(token);
            if (token.Kind == TokenKind.EOF) break;
        }
        return tokens;
    }

    private char Peek(int offset = 0)
    {
        int idx = _pos + offset;
        return idx < _source.Length ? _source[idx] : '\0';
    }

    private char Advance()
    {
        char c = _source[_pos];
        _pos++;
        if (c == '\n')
        {
            _line++;
            _column = 1;
        }
        else
        {
            _column++;
        }
        return c;
    }

    private bool IsAtEnd => _pos >= _source.Length;

    private SourceLocation CurrentLocation() => new(_fileName, _line, _column);

    private Token MakeToken(TokenKind kind, string text, SourceLocation start, object? value = null)
    {
        return new Token(kind, text, new SourceSpan(start, CurrentLocation()), value);
    }

    private void SkipWhitespace()
    {
        while (!IsAtEnd)
        {
            char c = Peek();
            if (c == ' ' || c == '\t' || c == '\r')
            {
                Advance();
                _nextBraceIsArray = false;
            }
            else if (c == '\n')
            {
                Advance();
                _nextBraceIsArray = false;
            }
            else if (c == '/' && Peek(1) == '/')
            {
                // Line comment
                while (!IsAtEnd && Peek() != '\n') Advance();
            }
            else if (c == '/' && Peek(1) == '*')
            {
                // Block comment /* ... */
                Advance(); Advance();
                while (!IsAtEnd && !(Peek() == '*' && Peek(1) == '/'))
                {
                    Advance();
                }
                if (!IsAtEnd) { Advance(); Advance(); }
            }
            else if (c == '(' && Peek(1) == '*')
            {
                // Block comment (* ... *)
                Advance(); Advance();
                while (!IsAtEnd && !(Peek() == '*' && Peek(1) == ')'))
                {
                    Advance();
                }
                if (!IsAtEnd) { Advance(); Advance(); }
            }
            else
            {
                break;
            }
        }
    }

    public Token NextToken()
    {
        SkipWhitespace();

        if (IsAtEnd)
            return MakeToken(TokenKind.EOF, "", CurrentLocation());

        var start = CurrentLocation();
        char c = Peek();

        // Preprocessor directives
        if (c == '#')
            return ReadPreprocessor(start);

        // Numbers (including .51 style floats, which can appear after dot-operator boundary cases)
        if (char.IsDigit(c) || (c == '$' && _pos + 1 < _source.Length && IsHexDigit(Peek(1))))
            return ReadNumber(start);

        // .数字 → 数値リテラルとして読む (0を補完: .51 → 0.51 → 整数51として扱う)
        // これは .>=-.51 のような記述で .>=.(閉じドット省略) + - + .51 となった場合の救済
        if (c == '.' && _pos + 1 < _source.Length && char.IsDigit(Peek(1)))
        {
            Advance(); // skip '.'
            return ReadNumber(start);
        }

        // String literals
        if (c == '"')
            return ReadString(start);

        // Char literals
        if (c == '\'')
            return ReadChar(start);

        // Dot operators (.op.)
        // ただし .数字 (例: .51) は数値リテラルの一部なのでスキップ
        if (c == '.' && _pos + 1 < _source.Length && !char.IsDigit(Peek(1)))
            return ReadDotOperator(start) ?? ReadOperator(start);

        // Identifiers and keywords
        if (IsIdentStart(c))
            return ReadIdentifier(start);

        // Operators and delimiters
        return ReadOperator(start);
    }

    private Token ReadNumber(SourceLocation start)
    {
        int startPos = _pos;

        // $ prefix hex
        if (Peek() == '$')
        {
            Advance();
            while (!IsAtEnd && IsHexDigit(Peek())) Advance();
            string text = _source[startPos.._pos];
            long val = Convert.ToInt64(text[1..], 16);
            return MakeToken(TokenKind.IntegerLiteral, text, start, (int)val);
        }

        // 0x prefix hex
        if (Peek() == '0' && (Peek(1) == 'x' || Peek(1) == 'X'))
        {
            Advance(); Advance();
            while (!IsAtEnd && IsHexDigit(Peek())) Advance();
            string text = _source[startPos.._pos];
            long val = Convert.ToInt64(text[2..], 16);
            return MakeToken(TokenKind.IntegerLiteral, text, start, (int)val);
        }

        // Read digit characters (数字のみ。hex suffix判定は後で)
        while (!IsAtEnd && char.IsDigit(Peek()))
        {
            Advance();
        }

        // Float: 小数点の後に数字が続く
        if (!IsAtEnd && Peek() == '.' && _pos + 1 < _source.Length && char.IsDigit(Peek(1)))
        {
            Advance(); // consume '.'
            while (!IsAtEnd && char.IsDigit(Peek())) Advance();
            string ftext = _source[startPos.._pos];
            double fval = double.Parse(ftext);
            return MakeToken(TokenKind.FloatLiteral, ftext, start, fval);
        }

        // Binary suffix (B/b): 0と1のみで構成されていればバイナリ
        string raw = _source[startPos.._pos];
        if (!IsAtEnd && (Peek() == 'B' || Peek() == 'b') && raw.All(ch => ch == '0' || ch == '1'))
        {
            Advance();
            string text = _source[startPos.._pos];
            long val = Convert.ToInt64(raw, 2);
            return MakeToken(TokenKind.IntegerLiteral, text, start, (int)val);
        }

        // Hex: 数字の後にa-f文字が続き、最後にHが付く → 全体をhex
        if (!IsAtEnd && IsHexDigit(Peek()) && Peek() != 'B' && Peek() != 'b')
        {
            // hex digits を追加で読む
            while (!IsAtEnd && IsHexDigit(Peek())) Advance();
            raw = _source[startPos.._pos];

            if (!IsAtEnd && (Peek() == 'H' || Peek() == 'h'))
            {
                Advance();
                string text = _source[startPos.._pos];
                long val = Convert.ToInt64(raw, 16);
                return MakeToken(TokenKind.IntegerLiteral, text, start, (int)val);
            }
            // H suffixがない場合、hex文字を戻す...複雑になるので
            // 実際にはSLANGのhex表記は常にH suffix必須なので、ここには来にくい
        }

        // Hex suffix (H/h): 純粋な数字列 + H
        if (!IsAtEnd && (Peek() == 'H' || Peek() == 'h'))
        {
            Advance();
            string text = _source[startPos.._pos];
            long val = Convert.ToInt64(raw, 16);
            return MakeToken(TokenKind.IntegerLiteral, text, start, (int)val);
        }

        // Decimal
        long dval = long.Parse(raw);
        return MakeToken(TokenKind.IntegerLiteral, raw, start, (int)dval);
    }

    private Token ReadString(SourceLocation start)
    {
        Advance(); // skip opening "
        var sb = new System.Text.StringBuilder();
        while (!IsAtEnd && Peek() != '"')
        {
            if (Peek() == '\n')
            {
                return MakeToken(TokenKind.Error, "Unterminated string", start);
            }
            if (Peek() == '\\')
            {
                Advance();
                sb.Append(ReadEscapeChar());
            }
            else
            {
                sb.Append(Advance());
            }
        }
        if (!IsAtEnd) Advance(); // skip closing "
        string text = sb.ToString();
        return MakeToken(TokenKind.StringLiteral, text, start, text);
    }

    private Token ReadChar(SourceLocation start)
    {
        Advance(); // skip opening '
        var sb = new System.Text.StringBuilder();
        while (!IsAtEnd && Peek() != '\'')
        {
            if (Peek() == '\n')
            {
                return MakeToken(TokenKind.Error, "Unterminated char", start);
            }
            if (Peek() == '\\')
            {
                Advance();
                sb.Append(ReadEscapeChar());
            }
            else
            {
                sb.Append(Advance());
            }
        }
        if (!IsAtEnd) Advance(); // skip closing '
        string text = sb.ToString();
        int charVal = text.Length > 0 ? (int)text[0] : 0;
        return MakeToken(TokenKind.CharLiteral, text, start, charVal);
    }

    private char ReadEscapeChar()
    {
        if (IsAtEnd) return '\\';
        char c = Advance();
        return c switch
        {
            'n' or '/' => '\r',
            'c' or 'C' => '\f',
            'r' or 'R' => (char)0x1c,
            'l' or 'L' => (char)0x1d,
            'u' or 'U' => (char)0x1e,
            'd' or 'D' => (char)0x1f,
            'x' or 'X' => ReadHexEscape(),
            _ => c,
        };
    }

    private char ReadHexEscape()
    {
        int val = 0;
        for (int i = 0; i < 2 && !IsAtEnd && IsHexDigit(Peek()); i++)
        {
            val = val * 16 + HexValue(Advance());
        }
        return (char)val;
    }

    private Token ReadIdentifier(SourceLocation start)
    {
        int startPos = _pos;
        while (!IsAtEnd && IsIdentPart(Peek())) Advance();

        // Check for string functions (e.g., FORM$)
        if (!IsAtEnd && Peek() == '$')
        {
            int savedPos = _pos;
            Advance();
            string withDollar = _source[startPos.._pos];
            if (StringFuncs.Contains(withDollar))
            {
                _nextBraceIsArray = false;
                return MakeToken(TokenKind.StringFunc, withDollar, start, withDollar);
            }
            _pos = savedPos; // backtrack
        }

        string text = _source[startPos.._pos];
        _nextBraceIsArray = true;

        // Check for FLOAT (%%) - this is handled separately in operator

        // Check keywords
        if (Keywords.TryGetValue(text, out var keyword))
        {
            // MODULE is special: handled as keyword
            return MakeToken(keyword, text, start);
        }

        return MakeToken(TokenKind.Identifier, text, start, text);
    }

    private Token? ReadDotOperator(SourceLocation start)
    {
        // Try to match .op. patterns
        int savedPos = _pos;
        int savedLine = _line;
        int savedCol = _column;

        if (TryMatchDotOp(out string? opText, out TokenKind opKind))
        {
            return MakeToken(opKind, opText!, start);
        }

        // Restore
        _pos = savedPos;
        _line = savedLine;
        _column = savedCol;
        return null;
    }

    private bool TryMatchDotOp(out string? text, out TokenKind kind)
    {
        text = null;
        kind = TokenKind.Error;

        // .op. 演算子のマッチング
        // 正規形: .>=. (開きドット + 演算子 + 閉じドット)
        // 許容形: .>=- (閉じドットが省略され、次のトークンに直結)
        //
        // 閉じドットの省略を許容する理由:
        //   VAL.>=-.51 のような記述で .>=. の閉じの . と
        //   -51 の間にスペースがない場合でも正しく認識するため。

        // 演算子パターン（開きドット無し、内側部分のみ。長いものから試す）
        var patterns = new (string Inner, string Full, TokenKind Kind)[]
        {
            (">=", ".>=.", TokenKind.SignedGe),
            ("<=", ".<=.", TokenKind.SignedLe),
            (">>", ".>>.", TokenKind.SignedShr),
            ("<<", ".<<.", TokenKind.SignedShl),
            (">",  ".>.",  TokenKind.SignedGt),
            ("<",  ".<.",  TokenKind.SignedLt),
            ("MOD",".MOD.",TokenKind.SignedMod),
            ("*",  ".*.",  TokenKind.SignedMul),
            ("/",  "./.",  TokenKind.SignedDiv),
        };

        // 先頭が . であることを確認
        if (Peek() != '.') return false;

        foreach (var (inner, full, k) in patterns)
        {
            int innerLen = inner.Length;
            int fullLen = 1 + innerLen; // 開きドット + inner

            // 開きドット + 内側演算子がマッチするか確認
            if (_pos + fullLen > _source.Length) continue;

            var innerSlice = _source.Substring(_pos + 1, innerLen);
            if (!innerSlice.Equals(inner, StringComparison.OrdinalIgnoreCase)) continue;

            // 閉じドットの処理
            //
            // SLANGの .op. 演算子は開きドット+演算子+閉じドットの3部構成だが、
            // 閉じドットの直後に演算子や数値が密着する場合がある。
            //   正規形:   VAL .>=. -51   (スペースで分離)
            //   密着形:   VAL.>=-.51     (閉じドットなし、-が直後)
            //   密着形2:  VAL.>=.51      (閉じドットの後に数値が密着)
            //
            // 方針: 閉じドットは「あれば消費、なくてもOK」とする。
            // これにより .>=- は .>=. と同等に扱われ、残りの -51 が正しくトークン化される。
            int consumeLen;
            if (_pos + fullLen < _source.Length && _source[_pos + fullLen] == '.')
            {
                // 閉じドットあり → 消費する
                consumeLen = fullLen + 1;
            }
            else
            {
                // 閉じドットなし → 内側部分だけで .op. として認識
                consumeLen = fullLen;
            }

            text = full; // 常に正規形のテキストを返す
            kind = k;
            for (int i = 0; i < consumeLen; i++) Advance();
            return true;
        }

        return false;
    }

    private Token ReadOperator(SourceLocation start)
    {
        char c = Advance();
        switch (c)
        {
            case '+':
                if (!IsAtEnd && Peek() == '+') { Advance(); return MakeToken(TokenKind.PlusPlus, "++", start); }
                if (!IsAtEnd && Peek() == '=') { Advance(); return MakeToken(TokenKind.PlusEq, "+=", start); }
                return MakeToken(TokenKind.Plus, "+", start);
            case '-':
                if (!IsAtEnd && Peek() == '-') { Advance(); return MakeToken(TokenKind.MinusMinus, "--", start); }
                if (!IsAtEnd && Peek() == '=') { Advance(); return MakeToken(TokenKind.MinusEq, "-=", start); }
                return MakeToken(TokenKind.Minus, "-", start);
            case '*':
                if (!IsAtEnd && Peek() == '=') { Advance(); return MakeToken(TokenKind.StarEq, "*=", start); }
                return MakeToken(TokenKind.Star, "*", start);
            case '/':
                if (!IsAtEnd && Peek() == '=') { Advance(); return MakeToken(TokenKind.SlashEq, "/=", start); }
                return MakeToken(TokenKind.Slash, "/", start);
            case '%':
                if (!IsAtEnd && Peek() == '%') { Advance(); return MakeToken(TokenKind.Float, "%%", start); }
                return MakeToken(TokenKind.Percent, "%", start);
            case '&':
                if (!IsAtEnd && Peek() == '&') { Advance(); return MakeToken(TokenKind.LogAnd, "&&", start); }
                return MakeToken(TokenKind.Ampersand, "&", start);
            case '|':
                if (!IsAtEnd && Peek() == '|') { Advance(); return MakeToken(TokenKind.LogOr, "||", start); }
                return MakeToken(TokenKind.Pipe, "|", start);
            case '=':
                if (!IsAtEnd && Peek() == '=') { Advance(); return MakeToken(TokenKind.EqEq, "==", start); }
                _nextBraceIsArray = false;
                return MakeToken(TokenKind.Eq, "=", start);
            case '<':
                if (!IsAtEnd && Peek() == '=') { Advance(); return MakeToken(TokenKind.Le, "<=", start); }
                if (!IsAtEnd && Peek() == '<') { Advance(); return MakeToken(TokenKind.Shl, "<<", start); }
                if (!IsAtEnd && Peek() == '>') { Advance(); return MakeToken(TokenKind.NotEq, "<>", start); }
                return MakeToken(TokenKind.Lt, "<", start);
            case '>':
                if (!IsAtEnd && Peek() == '=') { Advance(); return MakeToken(TokenKind.Ge, ">=", start); }
                if (!IsAtEnd && Peek() == '>') { Advance(); return MakeToken(TokenKind.Shr, ">>", start); }
                return MakeToken(TokenKind.Gt, ">", start);
            case '!':
                if (!IsAtEnd && Peek() == '=') { Advance(); return MakeToken(TokenKind.NotEq, "!=", start); }
                return MakeToken(TokenKind.Exclamation, "!", start);
            case '?':
                return MakeToken(TokenKind.Question, "?", start);
            case '(':
                return MakeToken(TokenKind.LParen, "(", start);
            case ')':
                _nextBraceIsArray = false;
                return MakeToken(TokenKind.RParen, ")", start);
            case '[':
                var brKind = _nextBraceIsArray ? TokenKind.ArrayBracketOpen : TokenKind.LBracket;
                _nextBraceIsArray = false;
                return MakeToken(brKind, "[", start);
            case ']':
                _nextBraceIsArray = true;
                return MakeToken(TokenKind.RBracket, "]", start);
            case '{':
                return MakeToken(TokenKind.LBrace, "{", start);
            case '}':
                return MakeToken(TokenKind.RBrace, "}", start);
            case '\uff62': // ｢
                return MakeToken(TokenKind.LAngleBracket, "｢", start);
            case '\uff63': // ｣
                return MakeToken(TokenKind.RAngleBracket, "｣", start);
            case ',':
                return MakeToken(TokenKind.Comma, ",", start);
            case ':':
                _nextBraceIsArray = false;
                return MakeToken(TokenKind.Colon, ":", start);
            case ';':
                _nextBraceIsArray = false;
                return MakeToken(TokenKind.Semicolon, ";", start);
            default:
                return MakeToken(TokenKind.Error, c.ToString(), start);
        }
    }

    private Token ReadPreprocessor(SourceLocation start)
    {
        int startPos = _pos;
        Advance(); // skip #

        // Read directive name
        while (!IsAtEnd && IsIdentPart(Peek())) Advance();
        string directive = _source[(startPos + 1).._pos];

        if (directive.Equals("INCLUDE", StringComparison.OrdinalIgnoreCase))
        {
            // Read filename
            SkipSpaces();
            int fnStart = _pos;
            while (!IsAtEnd && Peek() != '\n') Advance();
            string path = _source[fnStart.._pos].Trim().Trim('"');
            return MakeToken(TokenKind.PreprocInclude, path, start, path);
        }
        if (directive.Equals("IF", StringComparison.OrdinalIgnoreCase))
        {
            SkipSpaces();
            int exprStart = _pos;
            while (!IsAtEnd && Peek() != '\n') Advance();
            string expr = _source[exprStart.._pos].Trim();
            return MakeToken(TokenKind.PreprocIf, expr, start, expr);
        }
        if (directive.Equals("ELSE", StringComparison.OrdinalIgnoreCase))
        {
            return MakeToken(TokenKind.PreprocElse, "#ELSE", start);
        }
        if (directive.Equals("END", StringComparison.OrdinalIgnoreCase) ||
            directive.Equals("ENDIF", StringComparison.OrdinalIgnoreCase))
        {
            return MakeToken(TokenKind.PreprocEnd, "#END", start);
        }
        if (directive.Equals("ASM", StringComparison.OrdinalIgnoreCase))
        {
            // Read until #END
            SkipToEndOfLine();
            var sb = new System.Text.StringBuilder();
            while (!IsAtEnd)
            {
                if (Peek() == '#')
                {
                    int savedP = _pos;
                    Advance();
                    int dStart = _pos;
                    while (!IsAtEnd && IsIdentPart(Peek())) Advance();
                    string d = _source[dStart.._pos];
                    if (d.Equals("END", StringComparison.OrdinalIgnoreCase) ||
                        d.Equals("ENDIF", StringComparison.OrdinalIgnoreCase))
                    {
                        break;
                    }
                    sb.Append('#');
                    sb.Append(d);
                    continue;
                }
                sb.Append(Advance());
            }
            return MakeToken(TokenKind.Plain, sb.ToString(), start, sb.ToString());
        }
        if (directive.Equals("MODULE", StringComparison.OrdinalIgnoreCase))
        {
            return MakeToken(TokenKind.Module, "#MODULE", start);
        }

        // CCHK#IF special
        if (directive.Equals("CCHK#IF", StringComparison.OrdinalIgnoreCase))
        {
            return MakeToken(TokenKind.PreprocIf, "CCHK", start, "CCHK");
        }

        return MakeToken(TokenKind.Error, $"#{directive}", start);
    }

    private void SkipSpaces()
    {
        while (!IsAtEnd && (Peek() == ' ' || Peek() == '\t')) Advance();
    }

    private void SkipToEndOfLine()
    {
        while (!IsAtEnd && Peek() != '\n') Advance();
        if (!IsAtEnd) Advance();
    }

    // Character classification helpers
    private static bool IsHexDigit(char c) =>
        (c >= '0' && c <= '9') || (c >= 'a' && c <= 'f') || (c >= 'A' && c <= 'F');

    private static int HexValue(char c) => c switch
    {
        >= '0' and <= '9' => c - '0',
        >= 'a' and <= 'f' => c - 'a' + 10,
        >= 'A' and <= 'F' => c - 'A' + 10,
        _ => 0,
    };

    private static bool IsIdentStart(char c) =>
        char.IsLetter(c) || c == '_' || c == '@' || c == '^' ||
        (c >= '\u3041' && c <= '\u3096') ||  // ひらがな
        (c >= '\u30A1' && c <= '\u30FA') ||  // カタカナ
        c == '々' || c == '〇' || c == '〻' ||
        (c >= '\u3400' && c <= '\u9FFF') ||  // CJK統合漢字
        (c >= '\uF900' && c <= '\uFAFF');    // CJK互換漢字

    private static bool IsIdentPart(char c) =>
        IsIdentStart(c) || char.IsDigit(c);
}
