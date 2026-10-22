using SLANGCompiler.HuBasic.Lexing;
using SLANGCompiler.HuBasic.Text;

namespace SLANGCompiler.HuBasic.Parsing;

/// <summary>
/// Hu-BASIC の再帰下降の構文解析。
/// 優先順位 (強い順): 括弧・関数 / 単項 - + / * / / \ / MOD / + - / 比較 / NOT / AND / OR XOR
/// (Hu-BASIC で実測した順序)。IF は行末までを THEN 節・ELSE 節として吸収する。
/// </summary>
public sealed class HuParser
{
    public const int MaxLineLength = 255;
    public const int MaxLineNumber = 65529;

    /// <summary>MVP で対応する組み込み関数 (引数の個数の範囲)</summary>
    private static readonly Dictionary<string, (int Min, int Max)> Functions = new()
    {
        ["INP"] = (1, 1), ["MID$"] = (2, 3), ["RIGHT$"] = (2, 2), ["CHR$"] = (1, 1),
        ["ASC"] = (1, 1), ["VAL"] = (1, 1), ["HEXCHR$"] = (1, 1), ["CGPAT$"] = (1, 1),
        ["STICK"] = (1, 1), ["STRIG"] = (1, 1),
    };

    private readonly HuDiagnosticBag _diag;
    private List<HuToken> _tokens = new();
    private int _p;
    private HuPos _linePos;

    private HuParser(HuDiagnosticBag diag) => _diag = diag;

    public static HuAstProgram Parse(HuSource source, HuDiagnosticBag diag)
    {
        var parser = new HuParser(diag);
        var lines = new List<HuLine>();
        foreach (var line in source.Lines)
        {
            var parsed = parser.ParseLine(line);
            if (parsed != null) lines.Add(parsed);
        }
        return new HuAstProgram(lines);
    }

    /// <summary>1 行 (行番号つき) を解析する。テストからも使う。</summary>
    public static HuLine? ParseSingleLine(string text, HuDiagnosticBag diag)
        => new HuParser(diag).ParseLine(new HuSource.Line(1, text));

    private HuLine? ParseLine(HuSource.Line line)
    {
        var text = line.Text;
        var pos0 = new HuPos(line.SourceLine, 1, 0);
        if (text.Length > MaxLineLength)
            _diag.Error("HB0104", $"1 行が {MaxLineLength} 文字を超えています", pos0);

        int i = 0;
        while (i < text.Length && text[i] == ' ') i++;
        int numStart = i;
        while (i < text.Length && char.IsAsciiDigit(text[i])) i++;
        if (i == numStart)
        {
            _diag.Error("HB0201", "行番号がありません", pos0);
            return null;
        }
        if (!int.TryParse(text[numStart..i], out var number) || number < 1 || number > MaxLineNumber)
        {
            _diag.Error("HB0108", $"行番号は 1〜{MaxLineNumber} です", pos0);
            return null;
        }
        _linePos = new HuPos(line.SourceLine, numStart + 1, number);
        _tokens = new HuLexer(text[i..], i, _linePos, _diag).Tokenize();
        _p = 0;
        var stmts = ParseStatementList(stopAtElse: false);
        if (!Peek.Is(HuTokenKind.End))
            Error("HB0202", $"文の終わりに余分な '{Peek}' があります");
        return new HuLine(number, stmts, _linePos, text);
    }

    // ------------------------------------------------------------ トークン操作

    private HuToken Peek => _tokens[_p];
    private HuToken PeekAt(int k) => _tokens[Math.Min(_p + k, _tokens.Count - 1)];
    private HuToken Next() => _tokens[_p < _tokens.Count - 1 ? _p++ : _p];
    private HuPos PosOf(HuToken t) => _linePos with { Column = t.Column };
    private HuPos Here => PosOf(Peek);

    private void Error(string code, string message) => _diag.Error(code, message, Here);

    private bool AcceptSymbol(string s)
    {
        if (Peek.IsSymbol(s)) { _p++; return true; }
        return false;
    }

    private bool AcceptKeyword(string kw)
    {
        if (Peek.IsKeyword(kw)) { _p++; return true; }
        return false;
    }

    private void ExpectSymbol(string s)
    {
        if (!AcceptSymbol(s))
            Error("HB0203", $"'{s}' が必要です ('{Peek}' があります)");
    }

    private void ExpectKeyword(string kw)
    {
        if (!AcceptKeyword(kw))
            Error("HB0203", $"{kw} が必要です ('{Peek}' があります)");
    }

    /// <summary>文の終わり (':' / ELSE / 行末) か</summary>
    private bool AtStatementEnd => Peek.Is(HuTokenKind.End) || Peek.IsSymbol(":") || Peek.IsKeyword("ELSE");

    // ------------------------------------------------------------ 文

    private List<HuStmt> ParseStatementList(bool stopAtElse)
    {
        var list = new List<HuStmt>();
        while (true)
        {
            if (Peek.Is(HuTokenKind.End)) break;
            if (stopAtElse && Peek.IsKeyword("ELSE")) break;
            if (AcceptSymbol(":")) continue;
            int before = _p;
            var stmt = ParseStatement();
            if (stmt != null) list.Add(stmt);
            // IF とコメントは行末 (IF は ELSE の手前) までを吸収している
            if (stmt is HuIf || stmt is HuRem) break;
            if (Peek.Is(HuTokenKind.End)) break;
            if (stopAtElse && Peek.IsKeyword("ELSE")) break;
            if (!AcceptSymbol(":"))
            {
                Error("HB0202", $"文の区切り ':' が必要です ('{Peek}' があります)");
                SkipToStatementEnd();
                if (!AcceptSymbol(":")) break;
            }
            if (_p == before) Next(); // 念のため無限ループを防ぐ
        }
        return list;
    }

    private void SkipToStatementEnd()
    {
        while (!Peek.Is(HuTokenKind.End) && !Peek.IsSymbol(":")) Next();
    }

    private HuStmt? ParseStatement()
    {
        var t = Peek;
        var pos = Here;
        if (t.Kind == HuTokenKind.Comment)
        {
            Next();
            return MakeRem(t.Text, pos);
        }
        if (t.Kind == HuTokenKind.Ident)
            return ParseAssignment(pos);
        if (t.Kind != HuTokenKind.Keyword)
        {
            Error("HB0204", $"文として解釈できません ('{t}')");
            SkipToStatementEnd();
            return null;
        }

        Next();
        switch (t.Text)
        {
            case "REM":
                if (Peek.Kind == HuTokenKind.Comment) return MakeRem(Next().Text, pos);
                return new HuRem(null, 0, pos);
            case "LET":
                return ParseAssignment(pos);
            case "GOTO":
                return new HuGoto(ParseLineNumber(), pos);
            case "GOSUB":
                return new HuGosub(ParseLineNumber(), pos);
            case "RETURN":
                return new HuReturn(Peek.Is(HuTokenKind.Number) ? ParseLineNumber() : null, pos);
            case "ON":
                return ParseOn(pos);
            case "IF":
                return ParseIf(pos);
            case "FOR":
                return ParseFor(pos);
            case "NEXT":
            {
                var vars = new List<string>();
                if (!AtStatementEnd)
                {
                    do
                    {
                        vars.Add(ParseVarName());
                    } while (AcceptSymbol(","));
                }
                return new HuNext(vars, pos);
            }
            case "REPEAT":
                return new HuRepeat(pos);
            case "UNTIL":
                return new HuUntil(ParseExpr(), pos);
            case "WHILE":
                return new HuWhile(ParseExpr(), pos);
            case "WEND":
                return new HuWend(pos);
            case "END":
                return new HuEnd(pos);
            case "INIT":
                return new HuInit(pos);
            case "DIM":
                return ParseDim(pos);
            case "DEFINT":
                return ParseDefint(pos);
            case "DATA":
            {
                var data = Next();
                return new HuData(data.Kind == HuTokenKind.Data ? data.DataItems : Array.Empty<string>(), pos);
            }
            case "READ":
            {
                var targets = new List<HuExpr>();
                do
                {
                    targets.Add(ParseLValue());
                } while (AcceptSymbol(","));
                return new HuRead(targets, pos);
            }
            case "RESTORE":
                return new HuRestore(Peek.Is(HuTokenKind.Number) ? ParseLineNumber() : null, pos);
            case "OUT":
            {
                var port = ParseExpr();
                ExpectSymbol(",");
                return new HuOut(port, ParseExpr(), pos);
            }
            case "PRINT":
                return ParsePrint(pos);
            case "LOCATE":
            {
                var x = ParseExpr();
                ExpectSymbol(",");
                return new HuCommand("LOCATE", new HuExpr?[] { x, ParseExpr() }, pos);
            }
            case "COLOR":
            {
                HuExpr? f = null, b = null;
                if (!AtStatementEnd && !Peek.IsSymbol(",")) f = ParseExpr();
                if (AcceptSymbol(",")) b = ParseExpr();
                if (f == null && b == null) Error("HB0203", "COLOR に引数が必要です");
                return new HuCommand("COLOR", new[] { f, b }, pos);
            }
            case "CLS":
            case "CFLASH":
            case "CSIZE":
                return new HuCommand(t.Text, new[] { AtStatementEnd ? null : ParseExpr() }, pos);
            case "WIDTH":
            case "CGEN":
            case "TEMPO":
            case "PAUSE":
                return new HuCommand(t.Text, new HuExpr?[] { ParseExpr() }, pos);
            case "PLAY":
                return new HuCommand("PLAY", new HuExpr?[] { ParseExpr() }, pos);
            case "BEEP":
                return new HuCommand("BEEP", Array.Empty<HuExpr?>(), pos);
            case "CLICK":
            {
                bool on;
                if (AcceptKeyword("ON")) on = true;
                else if (AcceptKeyword("OFF")) on = false;
                else { Error("HB0203", "CLICK の後に ON か OFF が必要です"); on = false; }
                return new HuCommand("CLICK", new HuExpr?[] { new HuNum(on ? 1 : 0, pos) }, pos);
            }
            case "LINE":
                return ParseLine(pos);
            case "DEFCHR$":
            {
                ExpectSymbol("(");
                var code = ParseExpr();
                ExpectSymbol(")");
                ExpectSymbol("=");
                return new HuDefChr(code, ParseExpr(), pos);
            }
            default:
                _diag.Error("HB0301", $"未対応の命令 {t.Text}", pos);
                SkipToStatementEnd();
                return null;
        }
    }

    private static HuRem MakeRem(string text, HuPos pos)
    {
        var body = text.Trim();
        if (body.StartsWith("#VSYNC", StringComparison.OrdinalIgnoreCase))
        {
            var rest = body[6..].Trim();
            int arg = int.TryParse(rest, out var n) && n > 0 ? n : 1;
            return new HuRem("VSYNC", arg, pos);
        }
        return new HuRem(null, 0, pos);
    }

    private HuStmt ParseAssignment(HuPos pos)
    {
        var target = ParseLValue();
        ExpectSymbol("=");
        return new HuAssign(target, ParseExpr(), pos);
    }

    private HuExpr ParseLValue()
    {
        var t = Peek;
        if (t.Kind != HuTokenKind.Ident)
        {
            Error("HB0205", $"変数が必要です ('{t}' があります)");
            Next();
            return new HuVar("?", PosOf(t));
        }
        Next();
        if (AcceptSymbol("("))
        {
            var index = ParseExpr();
            if (Peek.IsSymbol(","))
            {
                Error("HB0303", "2 次元以上の配列は未対応です");
                while (AcceptSymbol(",")) ParseExpr();
            }
            ExpectSymbol(")");
            return new HuIndex(t.Text, index, PosOf(t));
        }
        return new HuVar(t.Text, PosOf(t));
    }

    private string ParseVarName()
    {
        var t = Peek;
        if (t.Kind != HuTokenKind.Ident)
        {
            Error("HB0205", $"変数名が必要です ('{t}' があります)");
            Next();
            return "?";
        }
        Next();
        return t.Text;
    }

    private int ParseLineNumber()
    {
        var t = Peek;
        if (t.Kind != HuTokenKind.Number)
        {
            Error("HB0206", $"行番号が必要です ('{t}' があります)");
            return 0;
        }
        Next();
        return t.Value;
    }

    private HuStmt ParseOn(HuPos pos)
    {
        var sel = ParseExpr();
        string kind;
        if (AcceptKeyword("GOTO")) kind = "GOTO";
        else if (AcceptKeyword("GOSUB")) kind = "GOSUB";
        else if (AcceptKeyword("RESTORE")) kind = "RESTORE";
        else
        {
            Error("HB0207", "ON の後に GOTO / GOSUB / RESTORE が必要です");
            SkipToStatementEnd();
            return new HuRem(null, 0, pos);
        }
        var lines = new List<int>();
        do
        {
            lines.Add(ParseLineNumber());
        } while (AcceptSymbol(","));
        return new HuOnJump(sel, kind, lines, pos);
    }

    private HuStmt ParseIf(HuPos pos)
    {
        var cond = ParseExpr();
        List<HuStmt> thenList;
        if (AcceptKeyword("THEN"))
            thenList = ParseIfBranch();
        else if (Peek.IsKeyword("GOTO"))
            thenList = ParseIfBranch();
        else
        {
            Error("HB0208", "IF の後に THEN が必要です");
            thenList = new List<HuStmt>();
        }
        List<HuStmt>? elseList = null;
        if (AcceptKeyword("ELSE"))
            elseList = ParseIfBranch();
        return new HuIf(cond, thenList, elseList, pos);
    }

    /// <summary>THEN / ELSE の後ろ: 行番号だけなら GOTO、そうでなければ ELSE か行末までの文</summary>
    private List<HuStmt> ParseIfBranch()
    {
        if (Peek.Is(HuTokenKind.Number))
        {
            var pos = Here;
            return new List<HuStmt> { new HuGoto(ParseLineNumber(), pos) };
        }
        return ParseStatementList(stopAtElse: true);
    }

    private HuStmt ParseFor(HuPos pos)
    {
        var name = ParseVarName();
        ExpectSymbol("=");
        var from = ParseExpr();
        ExpectKeyword("TO");
        var to = ParseExpr();
        HuExpr? step = null;
        if (AcceptKeyword("STEP")) step = ParseExpr();
        return new HuFor(name, from, to, step, pos);
    }

    private HuStmt ParseDim(HuPos pos)
    {
        var arrays = new List<(string, HuExpr)>();
        do
        {
            var name = ParseVarName();
            ExpectSymbol("(");
            var bound = ParseExpr();
            if (Peek.IsSymbol(","))
            {
                Error("HB0303", "2 次元以上の配列は未対応です");
                while (AcceptSymbol(",")) ParseExpr();
            }
            ExpectSymbol(")");
            arrays.Add((name, bound));
        } while (AcceptSymbol(","));
        return new HuDim(arrays, pos);
    }

    private HuStmt ParseDefint(HuPos pos)
    {
        var ranges = new List<(char, char)>();
        do
        {
            var a = ParseLetter();
            var b = a;
            if (AcceptSymbol("-")) b = ParseLetter();
            if (b < a) (a, b) = (b, a);
            ranges.Add((a, b));
        } while (AcceptSymbol(","));
        return new HuDefint(ranges, pos);
    }

    private char ParseLetter()
    {
        var t = Peek;
        if (t.Kind == HuTokenKind.Ident && t.Text.Length == 1 && char.IsAsciiLetterUpper(t.Text[0]))
        {
            Next();
            return t.Text[0];
        }
        Error("HB0209", $"DEFINT には英字 1 文字が必要です ('{t}' があります)");
        Next();
        return 'A';
    }

    private HuStmt ParsePrint(HuPos pos)
    {
        int? channel = null;
        if (AcceptSymbol("#"))
        {
            var ch = Peek;
            if (ch.Kind == HuTokenKind.Number) { Next(); channel = ch.Value; }
            else Error("HB0210", "PRINT# の後に番号が必要です");
            if (channel != 0) _diag.Error("HB0308", "PRINT# は #0 だけに対応しています", pos);
            AcceptSymbol(",");
        }
        HuExpr? usingFmt = null;
        if (AcceptKeyword("USING"))
        {
            usingFmt = ParseExpr();
            ExpectSymbol(";");
        }
        var items = new List<HuPrintItem>();
        while (!AtStatementEnd)
        {
            if (AcceptSymbol(";")) { items.Add(new HuPrintSep(';')); continue; }
            if (AcceptSymbol(",")) { items.Add(new HuPrintSep(',')); continue; }
            if (AcceptKeyword("SPC"))
            {
                ExpectSymbol("(");
                var n = ParseExpr();
                ExpectSymbol(")");
                items.Add(new HuPrintSpc(n));
                continue;
            }
            items.Add(new HuPrintExpr(ParseExpr()));
        }
        return new HuPrint(channel, usingFmt, items, pos);
    }

    private HuStmt ParseLine(HuPos pos)
    {
        ExpectSymbol("(");
        var x1 = ParseExpr();
        ExpectSymbol(",");
        var y1 = ParseExpr();
        ExpectSymbol(")");
        ExpectSymbol("-");
        ExpectSymbol("(");
        var x2 = ParseExpr();
        ExpectSymbol(",");
        var y2 = ParseExpr();
        ExpectSymbol(")");
        ExpectSymbol(",");
        var ch = ParseExpr();
        bool box = false;
        if (AcceptSymbol(","))
        {
            var t = Peek;
            if (t.Kind == HuTokenKind.Ident && t.Text == "B") { Next(); box = true; }
            else if (t.Kind == HuTokenKind.Ident && t.Text == "BF")
            {
                _diag.Error("HB0307", "LINE の BF (塗りつぶし) は未対応です", PosOf(t));
                Next();
            }
            else Error("HB0203", "LINE の最後の引数は B のみ対応しています");
        }
        return new HuLineText(x1, y1, x2, y2, ch, box, pos);
    }

    // ------------------------------------------------------------ 式

    public HuExpr ParseExpr() => ParseOrXor();

    private HuExpr ParseOrXor()
    {
        var left = ParseAnd();
        while (Peek.IsKeyword("OR") || Peek.IsKeyword("XOR"))
        {
            var op = Next();
            left = new HuBinary(op.Text, left, ParseAnd(), PosOf(op));
        }
        return left;
    }

    private HuExpr ParseAnd()
    {
        var left = ParseNot();
        while (Peek.IsKeyword("AND"))
        {
            var op = Next();
            left = new HuBinary("AND", left, ParseNot(), PosOf(op));
        }
        return left;
    }

    private HuExpr ParseNot()
    {
        if (Peek.IsKeyword("NOT"))
        {
            var op = Next();
            return new HuUnary("NOT", ParseNot(), PosOf(op));
        }
        return ParseCompare();
    }

    private static readonly string[] CompareOps = { "=", "<>", "<", ">", "<=", ">=" };

    private HuExpr ParseCompare()
    {
        var left = ParseAddSub();
        while (Peek.Kind == HuTokenKind.Symbol && CompareOps.Contains(Peek.Text))
        {
            var op = Next();
            left = new HuBinary(op.Text, left, ParseAddSub(), PosOf(op));
        }
        return left;
    }

    private HuExpr ParseAddSub()
    {
        var left = ParseMod();
        while (Peek.IsSymbol("+") || Peek.IsSymbol("-"))
        {
            var op = Next();
            left = new HuBinary(op.Text, left, ParseMod(), PosOf(op));
        }
        return left;
    }

    private HuExpr ParseMod()
    {
        var left = ParseIDiv();
        while (Peek.IsKeyword("MOD"))
        {
            var op = Next();
            left = new HuBinary("MOD", left, ParseIDiv(), PosOf(op));
        }
        return left;
    }

    private HuExpr ParseIDiv()
    {
        var left = ParseMulDiv();
        while (Peek.IsSymbol("\\"))
        {
            var op = Next();
            left = new HuBinary("\\", left, ParseMulDiv(), PosOf(op));
        }
        return left;
    }

    private HuExpr ParseMulDiv()
    {
        var left = ParseUnary();
        while (Peek.IsSymbol("*") || Peek.IsSymbol("/"))
        {
            var op = Next();
            left = new HuBinary(op.Text, left, ParseUnary(), PosOf(op));
        }
        return left;
    }

    private HuExpr ParseUnary()
    {
        if (Peek.IsSymbol("-") || Peek.IsSymbol("+"))
        {
            var op = Next();
            return new HuUnary(op.Text, ParseUnary(), PosOf(op));
        }
        return ParsePrimary();
    }

    private HuExpr ParsePrimary()
    {
        var t = Peek;
        var pos = PosOf(t);
        switch (t.Kind)
        {
            case HuTokenKind.Number:
                Next();
                return new HuNum(t.Value, pos);
            case HuTokenKind.Float:
                Next();
                _diag.Error("HB0401", $"小数・指数を含む数値 {t.Text} は未対応です (整数のみ)", pos);
                return new HuNum(0, pos);
            case HuTokenKind.String:
                Next();
                if (t.Text.Length > 255)
                    _diag.Error("HB0105", "文字列リテラルが 255 バイトを超えています", pos);
                return new HuStr(t.Text, pos);
            case HuTokenKind.Ident:
            {
                Next();
                if (AcceptSymbol("("))
                {
                    var index = ParseExpr();
                    if (Peek.IsSymbol(","))
                    {
                        Error("HB0303", "2 次元以上の配列は未対応です");
                        while (AcceptSymbol(",")) ParseExpr();
                    }
                    ExpectSymbol(")");
                    return new HuIndex(t.Text, index, pos);
                }
                return new HuVar(t.Text, pos);
            }
            case HuTokenKind.Symbol when t.Text == "(":
            {
                Next();
                var e = ParseExpr();
                ExpectSymbol(")");
                return e;
            }
            case HuTokenKind.Keyword:
                return ParseFunction(t, pos);
        }
        Error("HB0211", $"式が必要です ('{t}' があります)");
        if (!Peek.Is(HuTokenKind.End)) Next();
        return new HuNum(0, pos);
    }

    private HuExpr ParseFunction(HuToken t, HuPos pos)
    {
        Next();
        if (t.Text == "INKEY$")
            return new HuCall("INKEY$", Array.Empty<HuExpr>(), pos);
        if (!Functions.TryGetValue(t.Text, out var arity))
        {
            _diag.Error("HB0302", $"未対応の関数または予約語 {t.Text}", pos);
            // 引数があれば読み飛ばす
            if (AcceptSymbol("("))
            {
                int depth = 1;
                while (depth > 0 && !Peek.Is(HuTokenKind.End))
                {
                    if (Peek.IsSymbol("(")) depth++;
                    if (Peek.IsSymbol(")")) depth--;
                    Next();
                }
            }
            return new HuNum(0, pos);
        }
        var args = new List<HuExpr>();
        ExpectSymbol("(");
        do
        {
            args.Add(ParseExpr());
        } while (AcceptSymbol(","));
        ExpectSymbol(")");
        if (args.Count < arity.Min || args.Count > arity.Max)
            _diag.Error("HB0212", $"{t.Text} の引数の個数が違います", pos);
        return new HuCall(t.Text, args, pos);
    }
}

internal static class HuTokenExtensions
{
    public static bool Is(this HuToken t, HuTokenKind kind) => t.Kind == kind;
}
