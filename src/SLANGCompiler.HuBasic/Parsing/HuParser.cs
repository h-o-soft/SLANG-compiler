using SLANGCompiler.HuBasic.Lexing;
using SLANGCompiler.HuBasic.Text;

namespace SLANGCompiler.HuBasic.Parsing;

/// <summary>
/// Hu-BASIC の再帰下降の構文解析。
/// 優先順位 (強い順): 括弧・関数 / ^ / 単項 - + / * / / \ / MOD / + - / 比較 / NOT / AND / OR XOR
/// (Hu-BASIC で実測した順序)。IF は行末までを THEN 節・ELSE 節として吸収する。
/// </summary>
public sealed class HuParser
{
    public const int MaxLineLength = 255;
    public const int MaxLineNumber = 65529;

    /// <summary>MVP で対応する組み込み関数 (引数の個数の範囲)</summary>
    private static readonly Dictionary<string, (int Min, int Max)> Functions = new()
    {
        ["INP"] = (1, 1), ["MID$"] = (2, 3), ["RIGHT$"] = (2, 2), ["CHR$"] = (1, 255),
        ["ASC"] = (1, 1), ["VAL"] = (1, 1), ["HEXCHR$"] = (1, 1), ["CGPAT$"] = (1, 1),
        ["STICK"] = (1, 1), ["STRIG"] = (1, 1), ["RND"] = (0, 1), ["INT"] = (1, 1),
        ["LEFT$"] = (2, 2), ["STRING$"] = (2, 2), ["CHARACTER$"] = (2, 2), ["INPUT$"] = (1, 1),
        ["STR$"] = (1, 1),
        ["ABS"] = (1, 1), ["SGN"] = (1, 1), ["SQR"] = (1, 1), ["SIN"] = (1, 1), ["COS"] = (1, 1),
        ["TAN"] = (1, 1), ["ATN"] = (1, 1), ["LOG"] = (1, 1), ["EXP"] = (1, 1), ["FIX"] = (1, 1),
        ["CINT"] = (1, 1), ["FRAC"] = (1, 1), ["CSNG"] = (1, 1), ["CDBL"] = (1, 1), ["PAI"] = (1, 1),
        ["RAD"] = (1, 1), ["LEN"] = (1, 1), ["INSTR"] = (2, 3), ["HEX$"] = (1, 1), ["SPACE$"] = (1, 1),
        ["SCRN$"] = (3, 3), ["PEEK"] = (1, 1), ["PEEK@"] = (1, 1),
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
            // 本物は文が終わった所で ':' がなくても次の文を読む (A=1 B=2、COLOR 4 LOCATE 0,2 など)
            if (!AcceptSymbol(":") && !StartsStatement(Peek))
            {
                Error("HB0202", $"文の区切り ':' が必要です ('{Peek}' があります)");
                SkipToStatementEnd();
                if (!AcceptSymbol(":")) break;
            }
            if (_p == before) Next(); // 念のため無限ループを防ぐ
        }
        return list;
    }

    /// <summary>文の予約語でない予約語 (演算子・文の中の語と、式にしか出ない関数の名前)</summary>
    private static readonly HashSet<string> NonStatementKeywords = new(StringComparer.Ordinal)
    {
        "THEN", "TO", "STEP", "ELSE", "AND", "OR", "XOR", "NOT", "MOD", "EQV", "IMP", "USING", "SPC", "TAB", "OFF", "FN",
        "INKEY$", "RND", "PEEK", "ERL", "ERR", "CSRLIN", "FRE", "POS", "LPOS", "EOF", "LOC", "LOF", "DSKF", "FPOS",
        "VARPTR", "CVI", "CVS", "CVD", "MKI$", "MKS$", "MKD$", "OCT$", "BIN$", "ATTR$", "USR", "POINT",
    };

    /// <summary>区切りの ':' なしで次の文の始まりになれる字句か (名前・コメント・文の予約語)</summary>
    private static bool StartsStatement(HuToken t)
        => t.Kind is HuTokenKind.Ident or HuTokenKind.Comment
           || (t.Kind == HuTokenKind.Keyword && !NonStatementKeywords.Contains(t.Text)
               && !(Functions.ContainsKey(t.Text) && t.Text != "MID$"));

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
            {
                var (line, label) = ParseJumpTarget();
                return new HuGoto(line, pos) { Label = label };
            }
            case "GOSUB":
            {
                var (line, label) = ParseJumpTarget();
                return new HuGosub(line, pos) { Label = label };
            }
            case "RETURN":
                if (RejectLabelTarget("RETURN")) return null;
                return new HuReturn(AtLineNumber ? ParseLineNumber() : null, pos);
            case "LABEL":
            {
                if (!Peek.Is(HuTokenKind.String))
                {
                    _diag.Error("HB0301", "LABEL の名前は文字列の定数だけに対応しています", pos);
                    SkipToStatementEnd();
                    return null;
                }
                return new HuLabel(Next().Text, pos);
            }
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
                // REPEAT ON / REPEAT OFF はキーのリピート。それ以外は REPEAT〜UNTIL のループの始まり
                if (AcceptKeyword("ON")) return new HuCommand("REPEAT", new HuExpr?[] { new HuNum(1, pos) }, pos);
                if (AcceptKeyword("OFF")) return new HuCommand("REPEAT", new HuExpr?[] { new HuNum(0, pos) }, pos);
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
                return ParseDefType(HuVarKind.Int, t.Text, pos);
            case "DEFSNG":
                return ParseDefType(HuVarKind.Single, t.Text, pos);
            case "DEFDBL":
                return ParseDefType(HuVarKind.Double, t.Text, pos);
            case "DEFSTR":
                return ParseDefType(HuVarKind.Str, t.Text, pos);
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
                if (RejectLabelTarget("RESTORE")) return null;
                return new HuRestore(AtLineNumber ? ParseLineNumber() : null, pos);
            case "POKE":
            case "POKE@":
            {
                var addr = ParseExpr();
                ExpectSymbol(",");
                var values = new List<HuExpr>();
                do
                {
                    values.Add(ParseExpr());
                } while (AcceptSymbol(","));
                return new HuPoke(addr, values, t.Text == "POKE@", pos);
            }
            case "CALL":
                // CALL 番地 (引数は取らない。本物も Syntax error)
                return new HuCommand("CALL", new HuExpr?[] { ParseExpr() }, pos);
            case "TIME":
                // TIME = 秒 (式の TIME は関数)
                ExpectSymbol("=");
                return new HuCommand("TIME", new HuExpr?[] { ParseExpr() }, pos);
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
            case "CGEN":    // 引数なしは CGEN 0
            case "PRW":     // 引数なしは PRW 0
                return new HuCommand(t.Text, new[] { AtStatementEnd ? null : ParseExpr() }, pos);
            case "WIDTH":
            case "TEMPO":
            case "PAUSE":
                return new HuCommand(t.Text, new HuExpr?[] { ParseExpr() }, pos);
            case "PLAY":
                // PLAY 文字列 (MML) / PLAY 数 (TEMPO と同じ。解析で見分ける)
                return new HuCommand("PLAY", new HuExpr?[] { ParseExpr() }, pos);
            case "KEY":
            {
                // KEY n, 文字列 (ファンクションキーの定義。何もしない)
                var n = ParseExpr();
                ExpectSymbol(",");
                return new HuCommand("KEY", new HuExpr?[] { n, ParseExpr() }, pos);
            }
            case "PALET":
            {
                // PALET (既定に戻す) / PALET 色のコード, パレットの色
                if (AtStatementEnd) return new HuCommand("PALET", Array.Empty<HuExpr?>(), pos);
                var c = ParseExpr();
                ExpectSymbol(",");
                return new HuCommand("PALET", new HuExpr?[] { c, ParseExpr() }, pos);
            }
            case "SWAP":
            {
                var a = ParseLValue();
                ExpectSymbol(",");
                return new HuSwap(a, ParseLValue(), pos);
            }
            case "RANDOMIZE":
                // RANDOMIZE [式]。式は評価して捨てる (本物も同じ式で同じ乱数の並びにならない)
                return new HuCommand("RANDOMIZE", AtStatementEnd ? Array.Empty<HuExpr?>() : new HuExpr?[] { ParseExpr() }, pos);
            case "BEEP":
                // BEEP (短く 1 回) / BEEP 1 (鳴らし始める) / BEEP 0 (止める)
                return new HuCommand("BEEP", AtStatementEnd ? Array.Empty<HuExpr?>() : new HuExpr?[] { ParseExpr() }, pos);
            case "SOUND":
            {
                var reg = ParseExpr();
                ExpectSymbol(",");
                return new HuCommand("SOUND", new HuExpr?[] { reg, ParseExpr() }, pos);
            }
            case "KBUF":
            {
                bool on;
                if (AcceptKeyword("ON")) on = true;
                else if (AcceptKeyword("OFF")) on = false;
                else { Error("HB0203", "KBUF の後に ON か OFF が必要です"); on = false; }
                return new HuCommand("KBUF", new HuExpr?[] { new HuNum(on ? 1 : 0, pos) }, pos);
            }
            case "SCREEN":
            {
                // SCREEN [出す頁][,書く頁][,グラフィックのモード]。引数は省略できる
                var args = new List<HuExpr?>();
                if (!AtStatementEnd)
                {
                    do
                    {
                        args.Add(AtStatementEnd || Peek.IsSymbol(",") ? null : ParseExpr());
                    } while (AcceptSymbol(","));
                }
                return new HuCommand("SCREEN", args, pos);
            }
            case "INPUT":
                return ParseInput(pos);
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
            case "PAINT":
                return ParsePaint(pos);
            case "MID$":
            {
                // MID$(変数, 位置[, 長さ]) = 文字列
                ExpectSymbol("(");
                var name = ParseVarName();
                ExpectSymbol(",");
                var start = ParseExpr();
                HuExpr? len = AcceptSymbol(",") ? ParseExpr() : null;
                ExpectSymbol(")");
                ExpectSymbol("=");
                return new HuMidSet(name, start, len, ParseExpr(), pos);
            }
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

    /// <summary>
    /// GOTO・GOSUB・ON の飛び先: 行番号か、名前 (文字列の定数)。名前の式 (GOTO A$、GOTO "A"+"B") は、変換のときに飛び先を
    /// 決められないので未対応 (本物は書ける)
    /// </summary>
    private (int Line, string? Label) ParseJumpTarget()
    {
        if (Peek.Is(HuTokenKind.String))
        {
            var name = Next().Text;
            if (Peek.Kind == HuTokenKind.Symbol && !Peek.IsSymbol(":") && !Peek.IsSymbol(","))
            {
                Error("HB0301", "名前の飛び先は文字列の定数だけに対応しています");
                SkipToStatementEnd();
            }
            return (0, name);
        }
        if (Peek.Is(HuTokenKind.Ident) || Peek.IsSymbol("("))
        {
            Error("HB0301", "名前の飛び先は文字列の定数だけに対応しています");
            SkipToStatementEnd();
            return (0, null);
        }
        return (ParseLineNumber(), null);
    }

    /// <summary>RESTORE "名前"・RETURN "名前" は未対応 (HB0301 を出して文を読み飛ばす)</summary>
    private bool RejectLabelTarget(string keyword)
    {
        if (!Peek.Is(HuTokenKind.String)) return false;
        Error("HB0301", $"{keyword} の名前の飛び先は未対応です");
        SkipToStatementEnd();
        return true;
    }

    /// <summary>INPUT ["案内" ; または ,] 変数, …</summary>
    private HuStmt ParseInput(HuPos pos)
    {
        string? prompt = null;
        bool question = true;
        if (Peek.Is(HuTokenKind.String))
        {
            prompt = Next().Text;
            if (AcceptSymbol(",")) question = false;      // , なら「? 」を出さない
            else ExpectSymbol(";");
        }
        var targets = new List<HuExpr>();
        do
        {
            targets.Add(ParseLValue());
        } while (AcceptSymbol(","));
        return new HuInput(prompt, question, targets, pos);
    }

    private static HuRem MakeRem(string text, HuPos pos)
    {
        var body = text.Trim(' ');
        if (body.StartsWith("#VSYNC", StringComparison.OrdinalIgnoreCase))
        {
            var rest = body[6..].Trim(' ');
            // 空白は X1 の 0x20 だけ (Trim 済み)。符号と ASCII の数字だけの数を読む (制御コードや NUL を読み飛ばさない)
            int arg = System.Text.RegularExpressions.Regex.IsMatch(rest, "^[+-]?[0-9]+$")
                      && int.TryParse(rest, System.Globalization.NumberStyles.AllowLeadingSign,
                          System.Globalization.CultureInfo.InvariantCulture, out var n) && n > 0 ? n : 1;
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

    /// <summary>次の字句が行番号として読めるか (32768 以上の行番号は浮動小数の字句になっている)</summary>
    private bool AtLineNumber
        => Peek.Is(HuTokenKind.Number) || (Peek.Is(HuTokenKind.Float) && Peek.Text.All(char.IsAsciiDigit));

    private int ParseLineNumber()
    {
        var t = Peek;
        // 32768 以上の行番号は、字句解析では浮動小数の数になっている (数字だけの綴りなら行番号として読む)
        if (t.Kind == HuTokenKind.Float && t.Text.All(char.IsAsciiDigit) && int.TryParse(t.Text, out var big))
        {
            Next();
            return big;
        }
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
        var labels = new List<string?>();
        do
        {
            // 名前の飛び先 (GOTO・GOSUB だけ。RESTORE の名前は未対応)
            if (Peek.Is(HuTokenKind.String) && kind == "RESTORE")
            {
                Error("HB0301", "ON … RESTORE の名前の飛び先は未対応です");
                SkipToStatementEnd();
                return new HuRem(null, 0, pos);
            }
            var (line, label) = ParseJumpTarget();
            labels.Add(label);
            lines.Add(line);
        } while (AcceptSymbol(","));
        return labels.Any(l => l != null) ? new HuOnJump(sel, kind, lines, pos) { Labels = labels } : new HuOnJump(sel, kind, lines, pos);
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
        if (AtLineNumber)
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

    private HuStmt ParseDefType(HuVarKind kind, string keyword, HuPos pos)
    {
        var ranges = new List<(char, char)>();
        do
        {
            var a = ParseLetter(keyword);
            var b = a;
            if (AcceptSymbol("-")) b = ParseLetter(keyword);
            if (b < a) (a, b) = (b, a);
            ranges.Add((a, b));
        } while (AcceptSymbol(","));
        return new HuDefType(kind, ranges, pos);
    }

    private char ParseLetter(string keyword)
    {
        var t = Peek;
        if (t.Kind == HuTokenKind.Ident && t.Text.Length == 1 && char.IsAsciiLetterUpper(t.Text[0]))
        {
            Next();
            return t.Text[0];
        }
        Error("HB0209", $"{keyword} には英字 1 文字が必要です ('{t}' があります)");
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
            if (Peek.Is(HuTokenKind.Keyword) && Peek.Text is "SPC" or "TAB")
            {
                bool tab = Next().Text == "TAB";
                ExpectSymbol("(");
                var n = ParseExpr();
                ExpectSymbol(")");
                items.Add(new HuPrintSpc(n, tab));
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
        if (Peek.IsKeyword("PSET") || Peek.IsKeyword("PRESET"))
            return ParseLineGraphic(x1, y1, x2, y2, pos);
        var ch = ParseExpr();
        bool box = false, fill = false;
        if (AcceptSymbol(","))
        {
            var t = Peek;
            if (t.Kind == HuTokenKind.Ident && t.Text == "B") { Next(); box = true; }
            else if (t.Kind == HuTokenKind.Ident && t.Text == "BF") { Next(); box = fill = true; }
            else Error("HB0203", "LINE の最後の引数は B か BF です");
        }
        return new HuLineText(x1, y1, x2, y2, ch, box, pos) { Fill = fill };
    }

    /// <summary>
    /// グラフィックの LINE の PSET|PRESET から後: なし、,色 、,色,B|BF 、,B|BF 、,,B|BF。PSET の直後の ,B は (本物と同じく)
    /// 色の変数ではなく箱の印として読む。BF の後のタイルパターンは未対応
    /// </summary>
    private HuStmt ParseLineGraphic(HuExpr x1, HuExpr y1, HuExpr x2, HuExpr y2, HuPos pos)
    {
        bool preset = Next().Text == "PRESET";
        HuExpr? color = null;
        var mark = BoxMark.None;
        if (AcceptSymbol(","))
        {
            if (!IsBoxMark())
            {
                if (!Peek.IsSymbol(",")) color = ParseExpr();
                if (AcceptSymbol(",")) mark = ParseBoxMark();
            }
            else mark = ParseBoxMark();
        }
        if (mark == BoxMark.Fill && Peek.IsSymbol(","))
        {
            Error("HB0301", "グラフィックの LINE のタイルパターンは未対応です");
            SkipToStatementEnd();
        }
        return new HuLineGraphic(x1, y1, x2, y2, color, preset, mark != BoxMark.None, pos) { Fill = mark == BoxMark.Fill };
    }

    private enum BoxMark { None, Box, Fill }

    /// <summary>
    /// PSET の直後が箱の印か。本物は B で始まる名前をすべて箱の印として読む (実測: B+1・B(0)・BF+1・BX は Syntax error、
    /// (B) は色)。hubasic2sl も同じく読み、B 以外は ParseBoxMark で error にする
    /// </summary>
    private bool IsBoxMark() => Peek.Kind == HuTokenKind.Ident && Peek.Text.StartsWith('B');

    /// <summary>グラフィックの LINE の最後の B・BF を読む</summary>
    private BoxMark ParseBoxMark()
    {
        var t = Peek;
        if (t.Kind == HuTokenKind.Ident && t.Text == "B") { Next(); return BoxMark.Box; }
        if (t.Kind == HuTokenKind.Ident && t.Text == "BF") { Next(); return BoxMark.Fill; }
        if (t.Kind == HuTokenKind.Ident && t.Text.StartsWith('B'))
        {
            Next();
            Error("HB0203", $"グラフィックの LINE の {t.Text} は B (箱) として読まれます (本物も Syntax error)。" +
                            $"色の式にするなら ({t.Text}) のように括弧で囲んでください");
            return BoxMark.None;
        }
        Error("HB0203", "グラフィックの LINE の最後の引数は B か BF です");
        return BoxMark.None;
    }

    /// <summary>
    /// PAINT (x,y),色[,境界色 1[,…[,境界色 7]]]。色の省略 (本物は前のグラフィックの命令の色) は未対応。
    /// 境界色は本物と同じく 7 つまで (8 つは Syntax error)
    /// </summary>
    private HuStmt? ParsePaint(HuPos pos)
    {
        ExpectSymbol("(");
        var x = ParseExpr();
        ExpectSymbol(",");
        var y = ParseExpr();
        ExpectSymbol(")");
        if (!AcceptSymbol(","))
        {
            Error("HB0301", "PAINT の色の省略は未対応です");
            SkipToStatementEnd();
            return null;
        }
        var color = ParseExpr();
        var borders = new List<HuExpr>();
        while (AcceptSymbol(","))
        {
            if (borders.Count == MaxPaintBorders)
            {
                Error("HB0203", $"PAINT の境界色は {MaxPaintBorders} つまでです");
                SkipToStatementEnd();
                break;
            }
            borders.Add(ParseExpr());
        }
        return new HuPaint(x, y, color, borders, pos);
    }

    private const int MaxPaintBorders = 7;

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
        return ParsePower();
    }

    /// <summary>
    /// べき乗 (^)。単項の - + より強く、左結合 (実測: -2^2 は -4、3^2^2 は 81)。右の項には単項の - + を付けられる
    /// (2^-2 は .25)
    /// </summary>
    private HuExpr ParsePower()
    {
        var left = ParsePrimary();
        while (Peek.IsSymbol("^"))
        {
            var op = Next();
            left = new HuBinary("^", left, ParsePowerOperand(), PosOf(op));
        }
        return left;
    }

    private HuExpr ParsePowerOperand()
    {
        if (Peek.IsSymbol("-") || Peek.IsSymbol("+"))
        {
            var op = Next();
            return new HuUnary(op.Text, ParsePowerOperand(), PosOf(op));
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
            {
                Next();
                // ランタイムの INPUT と同じ読み方 (有効 9 桁、f24 へ 1 回だけ四捨五入) で f24 の値にする
                var fv = HuF24.ParseDecimal(t.Text);
                if (fv == null || double.IsInfinity(fv.Value))
                {
                    _diag.Error("HB0103", $"浮動小数として大きすぎる数値 {t.Text} (約 1.8E+19 まで)", pos);
                    fv = 0;
                }
                return new HuFloat(fv.Value, pos);
            }
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
        {
            // INKEY$(0) は押している間ずっとその文字を返す (INKEY$ は押した瞬間とリピートだけ)
            if (!AcceptSymbol("(")) return new HuCall("INKEY$", Array.Empty<HuExpr>(), pos);
            var a = ParseExpr();
            ExpectSymbol(")");
            return new HuCall("INKEY$", new[] { a }, pos);
        }
        if (t.Text == "RND" && !Peek.IsSymbol("("))
            return new HuCall("RND", Array.Empty<HuExpr>(), pos);
        if (t.Text == "TIME")
            return new HuCall("TIME", Array.Empty<HuExpr>(), pos);
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
