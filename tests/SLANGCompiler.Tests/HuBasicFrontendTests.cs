using SLANGCompiler.HuBasic;
using SLANGCompiler.HuBasic.Analysis;
using SLANGCompiler.HuBasic.Lexing;
using SLANGCompiler.HuBasic.Parsing;
using SLANGCompiler.HuBasic.Text;
using Xunit;

namespace SLANGCompiler.Tests;

/// <summary>
/// hub2sl の前段 (字句・構文・解析) のテスト。SLANG の出力は見ない。
/// </summary>
public class HuBasicFrontendTests
{
    // ------------------------------------------------------------ 字句

    private static List<HuToken> Lex(string body)
    {
        var diag = new HuDiagnosticBag();
        var src = HuSource.FromText("10 " + body, diag);
        var text = src.Lines[0].Text[3..];
        var tokens = new HuLexer(text, 3, new HuPos(1, 1, 10), diag).Tokenize();
        Assert.False(diag.HasErrors, string.Join("; ", diag.Items.Select(d => d.Message)));
        return tokens;
    }

    private static string Kinds(List<HuToken> tokens)
        => string.Join(" ", tokens.Where(t => t.Kind != HuTokenKind.End).Select(t => t.Kind switch
        {
            HuTokenKind.Keyword => t.Text,
            HuTokenKind.Ident => "id:" + t.Text,
            HuTokenKind.Number => "n:" + t.Value,
            HuTokenKind.String => "s:" + t.Text,
            _ => t.Text,
        }));

    [Theory]
    [InlineData("PRINTUSING\"**#\";DM;", "PRINT USING s:**# ; id:DM ;")]
    [InlineData("LOCATE6,24", "LOCATE n:6 , n:24")]
    [InlineData("FOR I=DM TO 0 STEP-1", "FOR id:I = id:DM TO n:0 STEP - n:1")]
    [InlineData("RUNX", "RUN id:X")]
    [InlineData("XRUN=1", "id:XRUN = n:1")]
    [InlineData("A=&H8000+&HFF", "id:A = n:32768 + n:255")]
    [InlineData("print a$", "PRINT id:A$")]
    [InlineData("?A", "PRINT id:A")]
    [InlineData("IF A<>B THEN C=A><B", "IF id:A <> id:B THEN id:C = id:A <> id:B")]
    public void Lexer_SplitsKeywordsByLongestMatch(string body, string expected)
        => Assert.Equal(expected, Kinds(Lex(body)));

    [Theory]
    [InlineData("A%=&H10000000000000000")]
    [InlineData("A%=&H10000")]
    [InlineData("A%=&O777777777777777777777777")]
    [InlineData("A%=&B11111111111111111")]
    [InlineData("A%=99999999999999999999999")]
    public void Lexer_RejectsOversizedLiterals(string body)
    {
        var diag = new HuDiagnosticBag();
        HuParser.ParseSingleLine("10 " + body, diag);
        Assert.Contains(diag.Items, d => d.Code == "HB0103");
    }

    [Theory]
    [InlineData("A=40000", true)]
    [InlineData("A=65535", true)]
    [InlineData("A=32767", false)]
    [InlineData("A=&HFFFF", false)] // 16 進はビットの並びとして使うので警告しない
    public void Lexer_WarnsDecimalLiteralsAbove32767(string body, bool warns)
    {
        var diag = new HuDiagnosticBag();
        HuParser.ParseSingleLine("10 " + body, diag);
        Assert.Equal(warns, diag.Items.Any(d => d.Code == "HB0104" && d.Severity == HuSeverity.Warning));
        Assert.False(diag.HasErrors);
    }

    [Fact]
    public void Lexer_AcceptsLargestLiterals()
    {
        var t = Lex("A=&HFFFF+&O177777+&B1111111111111111+65535");
        Assert.Equal(4, t.Count(x => x.Kind == HuTokenKind.Number && x.Value == 65535));
    }

    [Fact]
    public void Lexer_UnclosedStringEndsAtLineEnd()
    {
        var t = Lex("PRINT \"ABC");
        Assert.Equal("ABC", t[1].Text);
    }

    [Fact]
    public void Source_ConvertsHalfWidthKanaToX1Codes()
    {
        var diag = new HuDiagnosticBag();
        var src = HuSource.FromText("10 PRINT\"ｱｲﾟ\"", diag);
        Assert.False(diag.HasErrors);
        Assert.Equal("10 PRINT\"±²ß\"", src.Lines[0].Text);
    }

    [Fact]
    public void Source_RejectsNonX1Characters()
    {
        var diag = new HuDiagnosticBag();
        HuSource.FromText("10 PRINT\"漢\"", diag);
        Assert.Contains(diag.Items, d => d.Code == "HB0101");
    }

    [Fact]
    public void Lexer_DataModeKeepsRawItems()
    {
        var t = Lex("DATA 1, \"A,B:C\" ,PRINT,,  X Y  :PRINT 1");
        var data = t.Single(x => x.Kind == HuTokenKind.Data);
        Assert.Equal(new[] { "1", "A,B:C", "PRINT", "", "X Y" }, data.DataItems);
        // DATA の後の ':' で続く文は普通に読まれる
        Assert.Contains(t, x => x.IsKeyword("PRINT"));
    }

    [Fact]
    public void Lexer_CommentDirective()
    {
        var line = HuParser.ParseSingleLine("10 A=1:'#VSYNC 2", new HuDiagnosticBag());
        var rem = Assert.IsType<HuRem>(line!.Statements[1]);
        Assert.Equal("VSYNC", rem.Directive);
        Assert.Equal(2, rem.DirectiveArg);
    }

    // ------------------------------------------------------------ 構文

    private static HuExpr ParseExpr(string expr)
    {
        var diag = new HuDiagnosticBag();
        var line = HuParser.ParseSingleLine("10 X=" + expr, diag);
        Assert.False(diag.HasErrors, string.Join("; ", diag.Items.Select(d => d.Message)));
        return ((HuAssign)line!.Statements[0]).Value;
    }

    private static string Show(HuExpr e) => e switch
    {
        HuNum n => n.Value.ToString(),
        HuVar v => v.Name,
        HuStr s => $"\"{s.Text}\"",
        HuIndex ix => $"{ix.Name}({Show(ix.Index)})",
        HuUnary u => $"({u.Op} {Show(u.Operand)})",
        HuBinary b => $"({Show(b.Left)} {b.Op} {Show(b.Right)})",
        HuCall c => $"{c.Name}[{string.Join(",", c.Args.Select(Show))}]",
        _ => "?",
    };

    /// <summary>Hu-BASIC で実測した優先順位: * / > \ > MOD > + - > 比較 > NOT > AND > OR XOR</summary>
    [Theory]
    [InlineData("2+7\\2", "(2 + (7 \\ 2))")]
    [InlineData("2+7 MOD 3", "(2 + (7 MOD 3))")]
    [InlineData("9\\2*2", "(9 \\ (2 * 2))")]
    [InlineData("1=1 AND 2", "((1 = 1) AND 2)")]
    [InlineData("NOT 0=1", "(NOT (0 = 1))")]
    [InlineData("NOT 1+1", "(NOT (1 + 1))")]
    [InlineData("1 OR 2 AND 0", "(1 OR (2 AND 0))")]
    [InlineData("1 XOR 3 AND 1", "(1 XOR (3 AND 1))")]
    [InlineData("A=B=C", "((A = B) = C)")]
    [InlineData("-7\\2", "((- 7) \\ 2)")]
    [InlineData("MF2*-(MF2<16)", "(MF2 * (- (MF2 < 16)))")]
    [InlineData("A MOD 3\\2", "(A MOD (3 \\ 2))")]
    public void Parser_OperatorPrecedenceMatchesHuBasic(string expr, string expected)
        => Assert.Equal(expected, Show(ParseExpr(expr)));

    [Fact]
    public void Parser_IfAbsorbsRestOfLineAndNestedElse()
    {
        var diag = new HuDiagnosticBag();
        var line = HuParser.ParseSingleLine("10 IF A THEN IF B THEN X=1 ELSE X=2:Y=3 ELSE X=4:Y=5", diag);
        Assert.False(diag.HasErrors);
        var outer = Assert.IsType<HuIf>(Assert.Single(line!.Statements));
        var inner = Assert.IsType<HuIf>(Assert.Single(outer.Then));
        Assert.Single(inner.Then);
        Assert.Equal(2, inner.Else!.Count);
        Assert.Equal(2, outer.Else!.Count);
    }

    [Fact]
    public void Parser_ThenLineNumberIsGoto()
    {
        var line = HuParser.ParseSingleLine("10 IF A THEN 100 ELSE 200", new HuDiagnosticBag());
        var f = (HuIf)line!.Statements[0];
        Assert.Equal(100, Assert.IsType<HuGoto>(f.Then[0]).Line);
        Assert.Equal(200, Assert.IsType<HuGoto>(f.Else![0]).Line);
    }

    [Fact]
    public void Parser_PrintItemsAndChannel()
    {
        var line = HuParser.ParseSingleLine("10 PRINT#0\"GO\";A,SPC(3);", new HuDiagnosticBag());
        var p = (HuPrint)line!.Statements[0];
        Assert.Equal(0, p.Channel);
        Assert.Collection(p.Items,
            i => Assert.IsType<HuPrintExpr>(i),
            i => Assert.Equal(';', ((HuPrintSep)i).Sep),
            i => Assert.IsType<HuPrintExpr>(i),
            i => Assert.Equal(',', ((HuPrintSep)i).Sep),
            i => Assert.IsType<HuPrintSpc>(i),
            i => Assert.Equal(';', ((HuPrintSep)i).Sep));
    }

    [Fact]
    public void Parser_TextLineAndOnStatements()
    {
        var diag = new HuDiagnosticBag();
        var line = HuParser.ParseSingleLine("10 LINE(0,0)-(39,23),\"&\",B:ON A GOSUB 20,30:ON B RESTORE 40", diag);
        Assert.False(diag.HasErrors);
        Assert.True(Assert.IsType<HuLineText>(line!.Statements[0]).Box);
        Assert.Equal("GOSUB", ((HuOnJump)line.Statements[1]).Kind);
        Assert.Equal("RESTORE", ((HuOnJump)line.Statements[2]).Kind);
    }

    [Theory]
    [InlineData("10 A=1.5", "HB0401")]
    [InlineData("10 CALL 100", "HB0301")]
    [InlineData("10 X=ABS(1)", "HB0302")]
    [InlineData("10 DIM A(2,3)", "HB0303")]
    [InlineData("10 LINE(0,0)-(1,1),\"A\",BF", "HB0307")]
    [InlineData("10 PRINT#1,A", "HB0308")]
    public void Parser_ReportsUnsupported(string line, string code)
    {
        var diag = new HuDiagnosticBag();
        HuParser.ParseSingleLine(line, diag);
        Assert.Contains(diag.Items, d => d.Code == code);
    }

    // ------------------------------------------------------------ 解析

    private static (HuProgram? Prog, HuDiagnosticBag Diag) Analyze(string program)
    {
        var diag = new HuDiagnosticBag();
        var prog = HuTranspiler.Analyze(program, diag);
        return (prog, diag);
    }

    private static HuProgram AnalyzeOk(string program)
    {
        var (prog, diag) = Analyze(program);
        Assert.False(diag.HasErrors, string.Join("; ", diag.Items.Select(d => d.Code + " " + d.Message)));
        return prog!;
    }

    private static void AssertError(string program, string code)
    {
        var (_, diag) = Analyze(program);
        Assert.Contains(diag.Items, d => d.Code == code && d.Severity == HuSeverity.Error);
    }

    [Theory]
    [InlineData("10 A=1", "HB0403")]                          // DEFINT なし → 浮動小数
    [InlineData("10 DEFINT A-Z:A!=1", "HB0403")]
    [InlineData("10 DEFINT A-Z:N=5:DIM A(N)", "HB0304")]      // 変数の上限
    [InlineData("10 DEFINT A-Z:DIM A(32768*65535)", "HB0305")] // 定数の計算があふれても範囲外として検出
    [InlineData("10 DEFINT A-Z:DIM A(65535*65535*65535)", "HB0305")]
    [InlineData("10 DEFINT A-Z:DIM A(8192)", "HB0305")]
    [InlineData("10 DEFINT A-Z:DIM A(-1)", "HB0305")]
    [InlineData("10 DEFINT A-Z:DIM A$(3)", "HB0306")]         // 文字列の配列
    [InlineData("10 DEFINT A-Z:A=5/2*2", "HB0402")]           // / の結果を算術に使う
    [InlineData("10 DEFINT A-Z:IF A/2>1 THEN 10", "HB0402")]  // / の結果を比べる
    [InlineData("10 DEFINT A-Z:A$=1", "HB0404")]
    [InlineData("10 DEFINT A-Z:A=\"X\"+1", "HB0404")]
    [InlineData("10 DEFINT A-Z:FOR I=1 TO 2:NEXT A$", "HB0404")] // NEXT の変数も整数
    [InlineData("10 FOR A$=1 TO 2:NEXT", "HB0404")]
    [InlineData("10 GOTO 20", "HB0503")]
    [InlineData("10 DEFINT A-Z:ON A GOSUB 10,99", "HB0503")]
    [InlineData("10 DEFINT A-Z:FOR I=1 TO 2", "HB0601")]
    [InlineData("10 UNTIL 1", "HB0602")]
    [InlineData("10 REPEAT", "HB0603")]
    public void Analyzer_ReportsErrors(string program, string code) => AssertError(program, code);

    [Fact]
    public void Analyzer_DuplicateLineNumberIsError()
        => AssertError("10 END\n10 END", "HB0502");

    [Fact]
    public void Analyzer_SortsLinesWithWarning()
    {
        var (prog, diag) = Analyze("20 END\n10 GOTO 20");
        Assert.False(diag.HasErrors);
        Assert.Contains(diag.Items, d => d.Code == "HB0501");
        Assert.Equal(new[] { 10, 20 }, prog!.Lines.Select(l => l.Number));
    }

    [Theory]
    [InlineData("10 DEFINT A-Z:A=5/2")]                // 代入の右辺の最上位
    [InlineData("10 DEFINT A-Z:A=B OR C/2")]           // ビット演算の被演算子
    [InlineData("10 DEFINT A-Z:X(A/2)=1")]             // 添字
    [InlineData("10 DEFINT A-Z:LOCATE A/2,0")]         // 整数の引数
    [InlineData("10 DEFINT A-Z:A=(B/2)")]              // 括弧は位置を変えない
    public void Analyzer_AllowsDivisionWhereConvertedToInt(string program) => AnalyzeOk(program);

    [Fact]
    public void Analyzer_LargestArrayBoundIsAccepted() => AnalyzeOk("10 DEFINT A-Z:DIM A(8191)");

    [Fact]
    public void Analyzer_ScalarAndArrayWithSameNameAreSeparate()
    {
        var prog = AnalyzeOk("10 DEFINT A-Z:A=1:A(2)=3:DIM B(20):B=4");
        Assert.Contains("A", prog.IntVars);
        Assert.Contains("B", prog.IntVars);
        Assert.Equal(10, prog.Arrays["A"]);   // DIM なし → 10
        Assert.Equal(20, prog.Arrays["B"]);
    }

    [Fact]
    public void Analyzer_ZeroTripForExitTargetsMatchingNextPart()
    {
        var prog = AnalyzeOk("10 DEFINT A-Z:FOR I=1 TO 2:FOR J=2 TO 1:NEXT J,I");
        // NEXT J,I は 1 つの文。FOR J の出口は 1 番目の部分 (J) の直後、FOR I は 2 番目 (I) の直後
        Assert.Equal(1, prog.ForExits.Single(kv => kv.Key.Var == "I").Value.Part);
        Assert.Equal(0, prog.ForExits.Single(kv => kv.Key.Var == "J").Value.Part);
    }

    [Fact]
    public void Analyzer_ZeroTripForSkipsInnerLoopsAndOtherNamedNext()
    {
        // FOR I の飛び先: 内側の FOR K…NEXT K を数えて飛ばし、名前の違う NEXT J も飛ばして NEXT I へ
        var prog = AnalyzeOk("10 DEFINT A-Z:FOR J=1 TO 2:FOR I=2 TO 1:FOR K=1 TO 2:NEXT K:NEXT J:NEXT I");
        var exit = prog.ForExits.Single(kv => kv.Key.Var == "I").Value;
        Assert.Equal("I", exit.Next.Vars[0]);
    }

    [Fact]
    public void Analyzer_DataTableAndRestoreLines()
    {
        var prog = AnalyzeOk("10 DEFINT A-Z:READ A$\n20 DATA AB,\"C,D\"\n30 DATA ,X");
        Assert.Equal(new[] { (20, 0), (30, 7) }, prog.DataLines);
        Assert.Equal(new byte[] { 2, 65, 66, 3, 67, 44, 68, 0, 1, 88 }, prog.DataBytes);
    }

    [Fact]
    public void Analyzer_DataBudgetIsChecked()
    {
        var diag = new HuDiagnosticBag();
        HuTranspiler.Analyze("10 DEFINT A-Z:DIM A(8000):B$=\"\"", diag, dataBudget: 1000);
        Assert.Contains(diag.Items, d => d.Code == "HB0309");
    }

    [Fact]
    public void Analyzer_GosubIdsAreAssignedPerSite()
    {
        var prog = AnalyzeOk("10 DEFINT A-Z:GOSUB 30:ON A GOSUB 30,30\n20 END\n30 RETURN");
        Assert.Equal(new[] { 0, 1 }, prog.GosubIds.Values.OrderBy(v => v));
    }
}
