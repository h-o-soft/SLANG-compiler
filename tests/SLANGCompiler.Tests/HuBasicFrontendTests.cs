using SLANGCompiler.HuBasic;
using SLANGCompiler.HuBasic.Analysis;
using SLANGCompiler.HuBasic.Lexing;
using SLANGCompiler.HuBasic.Parsing;
using SLANGCompiler.HuBasic.Text;
using Xunit;

namespace SLANGCompiler.Tests;

/// <summary>
/// hubasic2sl の前段 (字句・構文・解析) のテスト。SLANG の出力は見ない。
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

    /// <summary>32768 以上の 10 進の数は浮動小数の定数 (Hu-BASIC と同じく、変数に入れるまでそのままの値)。
    /// % を付けたものだけ 16 ビットの整数として警告する</summary>
    [Theory]
    [InlineData("A=40000", false, true)]
    [InlineData("A=65535", false, true)]
    [InlineData("A=99999999999", false, true)]
    [InlineData("A=32767", false, false)]
    [InlineData("A=40000%", true, false)]
    [InlineData("A=&HFFFF", false, false)] // 16 進はビットの並び (整数のまま)
    public void Lexer_LargeDecimalLiteralsAreFloat(string body, bool warns, bool isFloat)
    {
        var diag = new HuDiagnosticBag();
        var line = HuParser.ParseSingleLine("10 " + body, diag);
        Assert.Equal(warns, diag.Items.Any(d => d.Code == "HB0109" && d.Severity == HuSeverity.Warning));
        Assert.False(diag.HasErrors);
        var value = Assert.IsType<HuAssign>(line!.Statements[0]).Value;
        Assert.Equal(isFloat, value is HuFloat);
    }

    [Theory]
    [InlineData("A=1.5", 1.5)]
    [InlineData("A=.25", 0.25)]
    [InlineData("A=1E6", 1e6)]
    [InlineData("A=1.5D2", 150.0)]
    [InlineData("A=2!", 2.0)]
    [InlineData("A=3#", 3.0)]
    [InlineData("A=40000", 40000.0)]
    public void Parser_FloatLiterals(string body, double value)
    {
        var diag = new HuDiagnosticBag();
        var line = HuParser.ParseSingleLine("10 " + body, diag);
        Assert.False(diag.HasErrors);
        Assert.Equal(value, Assert.IsType<HuFloat>(Assert.IsType<HuAssign>(line!.Statements[0]).Value).Value);
    }

    [Fact]
    public void Parser_LargeLineNumbersInJumps()
    {
        // 32768 以上の行番号は浮動小数の字句になるが、飛び先としては行番号に読む
        var diag = new HuDiagnosticBag();
        var line = HuParser.ParseSingleLine("10 GOTO 40000:GOSUB 65000:IF A THEN 50000 ELSE 60000", diag);
        Assert.False(diag.HasErrors);
        Assert.Equal(40000, Assert.IsType<HuGoto>(line!.Statements[0]).Line);
        Assert.Equal(65000, Assert.IsType<HuGosub>(line.Statements[1]).Line);
        var f = Assert.IsType<HuIf>(line.Statements[2]);
        Assert.Equal(50000, Assert.IsType<HuGoto>(f.Then[0]).Line);
        Assert.Equal(60000, Assert.IsType<HuGoto>(f.Else![0]).Line);
    }

    /// <summary>同じ番号をエラーと警告の両方に使わない (別の意味の診断が同じ番号にならないように)</summary>
    [Fact]
    public void Diagnostics_CodesAreNotSharedAcrossSeverities()
    {
        var dir = Path.Combine(RepoRootForDiag(), "src", "SLANGCompiler.HuBasic");
        var re = new System.Text.RegularExpressions.Regex(@"\.(Error|Warning)\(""(HB\d{4})""");
        var kinds = new Dictionary<string, HashSet<string>>();
        foreach (var f in Directory.EnumerateFiles(dir, "*.cs", SearchOption.AllDirectories))
        {
            if (f.Contains($"{Path.DirectorySeparatorChar}obj{Path.DirectorySeparatorChar}")) continue;
            foreach (System.Text.RegularExpressions.Match m in re.Matches(File.ReadAllText(f)))
            {
                if (!kinds.TryGetValue(m.Groups[2].Value, out var set)) kinds[m.Groups[2].Value] = set = new();
                set.Add(m.Groups[1].Value);
            }
        }
        Assert.NotEmpty(kinds);
        Assert.All(kinds, kv => Assert.True(kv.Value.Count == 1, $"{kv.Key} がエラーと警告の両方に使われています"));
    }

    private static string RepoRootForDiag()
    {
        var d = new DirectoryInfo(AppContext.BaseDirectory);
        while (d != null && !File.Exists(Path.Combine(d.FullName, "SLANGCompiler.sln"))) d = d.Parent;
        return d!.FullName;
    }

    [Fact]
    public void Lexer_AcceptsLargestLiterals()
    {
        var t = Lex("A=&HFFFF+&O177777+&B1111111111111111+65535%");
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

    /// <summary>X1 の文字コードのファイル (アスキーセーブ): 1 バイトが X1 の 1 文字。CR LF・CR・LF で行を分け、0x1A でファイルを終える</summary>
    [Fact]
    public void Source_X1BytesKeepsEveryByte()
    {
        var bytes = new byte[]
        {
            (byte)'1', (byte)'0', (byte)' ', (byte)'P', (byte)'"', 0x87, 0x1D, 0xA0, 0x85, (byte)'\t', (byte)'"', 0x0D, 0x0A,
            0xA0, 0x0D,                                   // 0xA0 だけの行は空白の行ではない (X1 の文字)
            (byte)'2', (byte)'0', (byte)' ', (byte)'E', 0x0A,
            (byte)' ', (byte)' ', 0x0D, 0x0A,                // 空白だけの行は捨てる
            (byte)'3', (byte)'0', 0x1A, (byte)'4', (byte)'0', 0x0D, // 0x1A でファイル全体が終わる
        };
        var src = HuSource.FromX1Bytes(bytes);
        Assert.Equal(new[] { 1, 2, 3, 5 }, src.Lines.Select(l => l.SourceLine));
        Assert.Equal("10 P\"\u0087\u001D\u00A0\u0085 \"", src.Lines[0].Text);
        Assert.Equal("\u00A0", src.Lines[1].Text);
        Assert.Equal("20 E", src.Lines[2].Text);
        Assert.Equal("30", src.Lines[3].Text);
    }

    private static string TempFile(byte[] bytes)
    {
        var path = Path.Combine(Path.GetTempPath(), $"hubasic2sl_{Guid.NewGuid():N}.bas");
        File.WriteAllBytes(path, bytes);
        return path;
    }

    [Fact]
    public void Source_AutoReadsX1FileWithWarning()
    {
        // UTF-8 として読めないファイルは、既定 (auto) では X1 の文字コードとして読み、warning HB0110 (例外で落ちない)
        var path = TempFile(new byte[] { (byte)'1', (byte)'0', (byte)' ', (byte)'P', (byte)'R', (byte)'I', (byte)'N', (byte)'T',
                                         (byte)'"', 0x87, 0x1D, (byte)'"', 0x0D, 0x0A });
        try
        {
            var diag = new HuDiagnosticBag();
            var src = HuSource.FromFile(path, "auto", diag);
            Assert.False(diag.HasErrors);
            var w = Assert.Single(diag.Items, d => d.Code == "HB0110");
            Assert.Equal((1, 10), (w.SourceLine, w.Column));
            Assert.Equal("10 PRINT\"\u0087\u001D\"", src.Lines[0].Text);
            // 変換の全体も通る (文字列の中の X1 の文字はそのままのバイト)
            var r = HuTranspiler.TranspileFile(path, "auto", new HuTranspileOptions());
            Assert.False(r.Diagnostics.HasErrors);
            Assert.Contains("{ 2,135,29 }", r.Output!);
        }
        finally { File.Delete(path); }
    }

    [Fact]
    public void Source_Utf8ExplicitReportsErrorInsteadOfException()
    {
        var path = TempFile(new byte[] { (byte)'1', (byte)'0', 0x0D, 0x0A, (byte)'2', (byte)'0', (byte)' ', 0xF7, 0x0D, 0x0A });
        try
        {
            var diag = new HuDiagnosticBag();
            HuSource.FromFile(path, "utf8", diag);
            var e = Assert.Single(diag.Items, d => d.Code == "HB0111");
            Assert.Equal((2, 4), (e.SourceLine, e.Column));   // 2 行目の 4 バイト目
            Assert.True(diag.HasErrors);
        }
        finally { File.Delete(path); }
    }

    [Fact]
    public void Source_AutoKeepsUtf8Results()
    {
        // UTF-8 として正しいファイルは、auto でも utf8 と同じ (BOM・半角カナ・¥・行の中の 0x1A とその後の行)
        var text = "\uFEFF10 PRINT\"ｱ¥\":REM\u001AX\n20 END\n";
        var path = TempFile(System.Text.Encoding.UTF8.GetBytes(text));
        try
        {
            var d1 = new HuDiagnosticBag();
            var d2 = new HuDiagnosticBag();
            var a = HuSource.FromFile(path, "auto", d1);
            var b = HuSource.FromFile(path, "utf8", d2);
            Assert.Empty(d1.Items);
            Assert.Empty(d2.Items);
            Assert.Equal(b.Lines.Select(l => (l.SourceLine, l.Text)), a.Lines.Select(l => (l.SourceLine, l.Text)));
            Assert.Equal(2, a.Lines.Count);
        }
        finally { File.Delete(path); }
    }

    /// <summary>REM #VSYNC の数は 0x20 のほかの文字 (制御コードなど) を空白として読み飛ばさない</summary>
    [Theory]
    [InlineData("10 REM #VSYNC 2", 2)]
    [InlineData("10 REM #VSYNC\u000B2", 1)]
    [InlineData("10 REM #VSYNC 2\u000C", 1)]
    [InlineData("10 REM #VSYNC 2\u0000", 1)]
    [InlineData("10 REM #VSYNC +3", 3)]
    public void Parser_VsyncArgumentAcceptsOnlySpace(string line, int expected)
    {
        var diag = new HuDiagnosticBag();
        var rem = Assert.IsType<HuRem>(HuParser.ParseSingleLine(line, diag)!.Statements[0]);
        Assert.Equal(expected, rem.DirectiveArg);
    }

    [Fact]
    public void Lexer_DataKeepsX1Characters()
    {
        // 引用符のない DATA の項目は、X1 の文字もそのまま (空白は 0x20 だけ。本物の実測: <A0> は文字、ASC は 160)
        static string[] DataItems(string x1Text)
        {
            var diag = new HuDiagnosticBag();
            var tokens = new HuLexer(x1Text, 3, new HuPos(1, 1, 10), diag).Tokenize();
            Assert.False(diag.HasErrors);
            return tokens.Single(x => x.Kind == HuTokenKind.Data).DataItems!.ToArray();
        }
        Assert.Equal(new[] { "\u0087A", "\u00A0X", "\u0085 " }, DataItems("DATA \u0087A,\u00A0X, \u0085 "));
        // 引用符の前の 0xA0 は空白ではないので、引用符の項目にならない
        Assert.Equal(new[] { "\u00A0\"X\"" }, DataItems("DATA \u00A0\"X\""));
    }

    [Fact]
    public void Lexer_DataModeKeepsRawItems()
    {
        // 引用符のない項目は頭の空白だけを除き、後ろの空白は残す (Hu-BASIC の実測: DATA  X , は "X ")
        var t = Lex("DATA 1, \"A,B:C\" ,PRINT ,,  X Y  :PRINT 1");
        var data = t.Single(x => x.Kind == HuTokenKind.Data);
        Assert.Equal(new[] { "1", "A,B:C", "PRINT ", "", "X Y  " }, data.DataItems);
        Assert.Equal(new[] { " Q ", "R " }, Lex("DATA \" Q \" , R ").Single(x => x.Kind == HuTokenKind.Data).DataItems);
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
    // ^ は単項の - より強く、左結合。右の項には単項の - を付けられる (F3a の実測)
    [InlineData("-2^2", "(- (2 ^ 2))")]
    [InlineData("3^2^2", "((3 ^ 2) ^ 2)")]
    [InlineData("2^-2", "(2 ^ (- 2))")]
    [InlineData("-2^-2", "(- (2 ^ (- 2)))")]
    [InlineData("2^-2^2", "((2 ^ (- 2)) ^ 2)")]
    [InlineData("2*-3^2", "(2 * (- (3 ^ 2)))")]
    [InlineData("A^2*B", "((A ^ 2) * B)")]
    [InlineData("PAI(1)+RAD(A)", "(PAI[1] + RAD[A])")]
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
    [InlineData("10 A=1E99", "HB0103")]   // f24 で表せない大きさ
    [InlineData("10 CIRCLE(100,100),50", "HB0301")]   // CALL は段階 G5b で対応した
    [InlineData("10 X$=OCT$(1)", "HB0302")]
    [InlineData("10 DIM A(2,3)", "HB0303")]
    [InlineData("10 LINE(0,0)-(1,1),\"A\",X", "HB0203")]   // B と BF のほかは書けない
    [InlineData("10 PRINT#1,A", "HB0308")]
    public void Parser_ReportsUnsupported(string line, string code)
    {
        var diag = new HuDiagnosticBag();
        HuParser.ParseSingleLine(line, diag);
        Assert.Contains(diag.Items, d => d.Code == code);
    }

    /// <summary>PRINT の TAB(n) は SPC と同じく項目として読み、RANDOMIZE の式は省略できる</summary>
    [Fact]
    public void Parser_PrintTabAndRandomize()
    {
        var diag = new HuDiagnosticBag();
        var line = HuParser.ParseSingleLine("10 PRINT \"A\";TAB(5);SPC(2):RANDOMIZE:RANDOMIZE 3", diag);
        Assert.False(diag.HasErrors);
        var items = ((HuPrint)line!.Statements[0]).Items.OfType<HuPrintSpc>().ToList();
        Assert.True(items[0].Tab);
        Assert.False(items[1].Tab);
        Assert.Empty(((HuCommand)line.Statements[1]).Args);
        Assert.Single(((HuCommand)line.Statements[2]).Args);
    }

    /// <summary>PAI・RAD は本物と同じく予約語 (名前の頭に書けない。設計 1 章の 8)</summary>
    [Theory]
    [InlineData("10 RADIUS=5")]
    [InlineData("10 PAIR=5")]
    public void Parser_PaiRadAreReserved(string line)
    {
        var diag = new HuDiagnosticBag();
        HuParser.ParseSingleLine(line, diag);
        Assert.True(diag.HasErrors);
    }

    /// <summary>段階 G1: KEY・PALET・PRW・SWAP・REPEAT ON/OFF・引数なしの CGEN の構文 (設計 G1 の 2 章)</summary>
    [Fact]
    public void Parser_StageG1Statements()
    {
        var diag = new HuDiagnosticBag();
        var line = HuParser.ParseSingleLine("10 KEY0,\"\":PALET:PALET 3,5:PRW:PRW 2:SWAP A,B(1):CGEN:CGEN1:REPEAT ON:REPEAT OFF:REPEAT:UNTIL 1", diag);
        Assert.False(diag.HasErrors, string.Join("; ", diag.Items.Select(d => d.Message)));
        var s = line!.Statements;
        var key = Assert.IsType<HuCommand>(s[0]);
        Assert.Equal("KEY", key.Name);
        Assert.Equal(2, key.Args.Count);
        Assert.Empty(((HuCommand)s[1]).Args);
        Assert.Equal(2, ((HuCommand)s[2]).Args.Count);
        Assert.Null(Assert.Single(((HuCommand)s[3]).Args));
        Assert.NotNull(Assert.Single(((HuCommand)s[4]).Args));
        var sw = Assert.IsType<HuSwap>(s[5]);
        Assert.IsType<HuVar>(sw.A);
        Assert.IsType<HuIndex>(sw.B);
        Assert.Null(Assert.Single(((HuCommand)s[6]).Args));
        Assert.IsType<HuNum>(Assert.Single(((HuCommand)s[7]).Args));
        // REPEAT ON/OFF は命令、REPEAT だけはループの始まり
        Assert.Equal(1, ((HuNum)((HuCommand)s[8]).Args[0]!).Value);
        Assert.Equal(0, ((HuNum)((HuCommand)s[9]).Args[0]!).Value);
        Assert.IsType<HuRepeat>(s[10]);
    }

    /// <summary>段階 G1: 本物で Syntax error になる形は変換のときの error</summary>
    [Theory]
    [InlineData("10 PALET 3")]
    [InlineData("10 KEY 0")]
    [InlineData("10 SWAP A")]
    [InlineData("10 A$=SCRN$(1,0)")]
    public void Parser_StageG1SyntaxErrors(string line)
    {
        var diag = new HuDiagnosticBag();
        HuParser.ParseSingleLine(line, diag);
        Assert.True(diag.HasErrors);
    }

    /// <summary>SCRN$・PRW は本物と同じく予約語 (設計 G1 の 4 章の 9)</summary>
    [Fact]
    public void Parser_ScrnPrwAreReserved()
    {
        Assert.Equal("PRW id:X = n:1", Kinds(Lex("PRWX=1")));
        Assert.Equal("id:A$ = SCRN$ ( n:1 , n:2 , n:3 )", Kinds(Lex("A$=SCRN$(1,2,3)")));
        var diag = new HuDiagnosticBag();
        HuParser.ParseSingleLine("10 SCRN$=\"A\"", diag);
        Assert.True(diag.HasErrors);
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
    [InlineData("10 DIM A(5461)", "HB0305")]                   // 浮動小数の配列は 1 要素 3 バイト (上限 5460)
    [InlineData("10 DEFINT A-Z:DEFSNG X", "HB0410")]           // 同じ頭文字に違う DEFxxx
    [InlineData("10 DEFSTR S\n20 IF 0 THEN DEFINT R-T", "HB0410")] // IF の中の DEFxxx も数える
    [InlineData("10 DEFINT A-Z:S=\"X\"", "HB0404")]
    [InlineData("10 DEFINT A-Z:N=5:DIM A(N)", "HB0304")]      // 変数の上限
    [InlineData("10 DEFINT A-Z:DIM A(&H8000*&HFFFF)", "HB0305")] // 定数の計算があふれても範囲外として検出
    [InlineData("10 DEFINT A-Z:DIM A(&HFFFF*&HFFFF*&HFFFF)", "HB0305")]
    [InlineData("10 DEFINT A-Z:DIM A(8192)", "HB0305")]
    [InlineData("10 DEFINT A-Z:DIM A(-1)", "HB0305")]
    [InlineData("10 DIM A$(64)", "HB0305")]                    // 文字列の配列は 1 要素 256 バイト (上限 63。段階 G2)
    [InlineData("10 DEFINT A-Z:DIM A(40000)", "HB0305")]       // 32768 以上の 10 進 (浮動小数の定数) も範囲を検査
    [InlineData("10 DEFINT A-Z:DIM A(1.25+1.25)", "HB0313")]   // 浮動小数を含む計算の上限は未対応 (項ごとに丸めると値が違う)
    [InlineData("10 DEFINT A-Z:DIM A(+(1.25+1.25))", "HB0313")] // 符号で包んでも同じ
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
    public void Analyzer_DimBoundWithSingleFloatConstant()
    {
        // 浮動小数の定数 1 つの上限は、f24 に直した値を四捨五入 (2.5 → 3)
        var prog = AnalyzeOk("10 DEFINT A-Z:DIM A(2.5),B(12.4)");
        Assert.Equal(10, prog.Arrays["A%"]);   // 上限が 10 未満なら 10 を確保する
        Assert.Equal(12, prog.Arrays["B%"]);
    }

    [Fact]
    public void Parser_StageF1bStatementsAndFunctions()
    {
        var diag = new HuDiagnosticBag();
        var line = HuParser.ParseSingleLine("10 MID$(A$,2,3)=\"X\":MID$(B$,5)=C$:LINE(0,0)-(1,1),\"#\",BF:" +
            "D$=LEFT$(A$,2)+STRING$(3,\"AB\")+STRING$(2,65)+CHR$(29,29,65)+CHARACTER$(X,Y)", diag);
        Assert.False(diag.HasErrors, string.Join("; ", diag.Items.Select(d => d.Message)));
        var m = Assert.IsType<HuMidSet>(line!.Statements[0]);
        Assert.Equal("A$", m.Var);
        Assert.NotNull(m.Length);
        Assert.Null(Assert.IsType<HuMidSet>(line.Statements[1]).Length);
        var lt = Assert.IsType<HuLineText>(line.Statements[2]);
        Assert.True(lt.Box && lt.Fill);
    }

    [Fact]
    public void Parser_StageF1cStatementsAndFunctions()
    {
        var diag = new HuDiagnosticBag();
        var line = HuParser.ParseSingleLine("10 INPUT \"NAME\";N$:INPUT \"Y\",D,E$:INPUT A(1):SOUND 7,62:BEEP 0:BEEP:" +
            "KBUF ON:SCREEN 0,1:SCREEN:A$=INKEY$(0)+INKEY$+INPUT$(2)", diag);
        Assert.False(diag.HasErrors, string.Join("; ", diag.Items.Select(d => d.Message)));
        var a = Assert.IsType<HuInput>(line!.Statements[0]);
        Assert.Equal("NAME", a.Prompt);
        Assert.True(a.Question);                                  // ; なら「? 」を出す
        var b = Assert.IsType<HuInput>(line.Statements[1]);
        Assert.False(b.Question);                                 // , なら出さない
        Assert.Equal(2, b.Targets.Count);
        var c = Assert.IsType<HuInput>(line.Statements[2]);
        Assert.Null(c.Prompt);
        Assert.True(c.Question);
        Assert.Equal(2, Assert.IsType<HuCommand>(line.Statements[3]).Args.Count);
        Assert.Single(Assert.IsType<HuCommand>(line.Statements[4]).Args);   // BEEP 0
        Assert.Empty(Assert.IsType<HuCommand>(line.Statements[5]).Args);    // BEEP
    }

    [Theory]
    [InlineData("10 DEFINT A-Z:KBUF ON", "HB0701")]
    [InlineData("10 DEFINT A-Z:SCREEN 0,1", "HB0702")]
    [InlineData("10 DEFINT A-Z:SCREEN", "HB0702")]
    public void Analyzer_NoOpCommandsWarn(string program, string code)
    {
        var (prog, diag) = Analyze(program);
        Assert.NotNull(prog);
        Assert.Contains(diag.Items, d => d.Code == code && d.Severity == HuSeverity.Warning);
    }

    [Fact]
    public void Analyzer_ScreenDefaultPagesIsSilent()
    {
        // SCREEN 0,0 は INIT や WIDTH の後と同じ状態なので warning を出さない
        var (_, diag) = Analyze("10 DEFINT A-Z:SCREEN 0,0:SCREEN 0,0,1");
        Assert.DoesNotContain(diag.Items, d => d.Code == "HB0702");
    }

    [Theory]
    [InlineData("10 DEFINT A-Z:A$=INKEY$(1)", "HB0302")]   // INKEY$(1)・(2) は別の意味なので未対応
    [InlineData("10 DEFINT A-Z:SCREEN 0,0,ASC(INPUT$(1))-48", "HB0703")]   // 何もしない命令の引数は定数だけ
    [InlineData("10 DEFINT A-Z:SCREEN X", "HB0703")]
    [InlineData("10 DEFINT A-Z:A$=INPUT$(\"X\")", "HB0404")]
    public void Analyzer_StageF1cErrors(string program, string code) => AssertError(program, code);

    [Theory]
    [InlineData("10 DEFINT A-Z:MID$(A,1)=\"X\"", "HB0404")]   // MID$ の文の変数は文字列
    [InlineData("10 DEFINT A-Z:MID$(A$,1)=1", "HB0404")]
    [InlineData("10 DEFINT A-Z:A$=CHARACTER$(\"X\",1)", "HB0404")]
    public void Analyzer_StageF1bTypeErrors(string program, string code) => AssertError(program, code);

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
        Assert.Contains("A%", prog.IntVars);
        Assert.Contains("B%", prog.IntVars);
        Assert.Equal(10, prog.Arrays["A%"]);   // DIM なし → 10
        Assert.Equal(20, prog.Arrays["B%"]);
    }

    // ------------------------------------------------------------ 段階 F2a: 変数の型と鍵

    /// <summary>
    /// 変数の鍵は (名前, 型の種類)。接尾辞のない名前は頭文字の型 (既定は単精度)。Hu-BASIC の実測と同じ:
    /// DEFINT A の下の A と A% は同じ、既定の B と B! は同じ、C! と C# は別、DEFSTR S の S と S$ は同じ、DEFDBL D の D と D# は同じ
    /// </summary>
    [Fact]
    public void Analyzer_VariableKeysFollowHuBasicRules()
    {
        var prog = AnalyzeOk("10 DEFINT A:DEFSTR S:DEFDBL D:A=1:A%=2:B=1:B!=2:C!=1:C#=2:S=\"X\":S$=\"Y\":D=1:D#=2:D!=3");
        Assert.Equal(new[] { "A%" }, prog.IntVars);
        Assert.Equal(new[] { "B!", "C!", "C#", "D!", "D#" }, prog.FloatVars);
        Assert.Equal(new[] { "S$" }, prog.StrVars);
        Assert.Equal("A%", prog.Key("A"));
        Assert.Equal("B!", prog.Key("B"));
        Assert.Equal("D#", prog.Key("D"));
        Assert.Equal("S$", prog.Key("S"));
    }

    [Fact]
    public void Analyzer_ArraysAreKeyedByKind()
    {
        var prog = AnalyzeOk("10 DEFINT A:DIM A(3),B(20),C%(5):A%(1)=1:B!(2)=1.5:C(1)=2");
        Assert.Equal(new[] { "A%", "B!", "C%", "C!" }, prog.Arrays.Keys);
        Assert.Equal(20, prog.Arrays["B!"]);
        Assert.Equal(10, prog.Arrays["C!"]);   // C(1) は単精度の配列 C! (DIM C%(5) とは別)
        AnalyzeOk("10 DIM A(5460)");           // 浮動小数の配列の上限
    }

    /// <summary>
    /// --assume-defint は頭文字の既定の型だけを整数にする。ソースの DEFxxx と接尾辞が優先で、DEFxxx の衝突の検査にも、
    /// 前の行で使ったかの warning にも数えない
    /// </summary>
    [Theory]
    [InlineData("10 A=1", false, "A", "A!")]
    [InlineData("10 A=1", true, "A", "A%")]
    [InlineData("10 DEFINT A:A=1", false, "A", "A%")]
    [InlineData("10 DEFINT A:A=1", true, "A", "A%")]
    [InlineData("10 DEFSNG A:A=1", false, "A", "A!")]
    [InlineData("10 DEFSNG A:A=1", true, "A", "A!")]
    [InlineData("10 A!=1", true, "A!", "A!")]
    [InlineData("10 A#=1", true, "A#", "A#")]
    [InlineData("10 A$=\"X\"", true, "A$", "A$")]
    public void Analyzer_AssumeDefintChangesOnlyTheDefault(string program, bool assumeDefint, string name, string key)
    {
        var diag = new HuDiagnosticBag();
        var prog = HuTranspiler.Analyze(program, diag, assumeDefint: assumeDefint);
        Assert.False(diag.HasErrors, string.Join("; ", diag.Items.Select(d => d.Code + " " + d.Message)));
        Assert.Contains(key, prog!.IntVars.Concat(prog.FloatVars).Concat(prog.StrVars));
        Assert.Equal(key, prog.Key(name));
    }

    [Fact]
    public void Analyzer_AssumeDefintIsNotCountedAsDefint()
    {
        var diag = new HuDiagnosticBag();
        var prog = HuTranspiler.Analyze("10 A=1\n20 DEFSNG A:B=2", diag, assumeDefint: true);
        Assert.False(diag.HasErrors);
        Assert.Equal("A!", prog!.Key("A"));
        Assert.Equal("B%", prog.Key("B"));
        // 10 行の A は DEFSNG より前の行だが、DEFSNG の型と既定 (仮の整数) が違うかは数えず、前の行で使ったことだけを知らせる
        Assert.Single(diag.Items, d => d.Code == "HB0411");
    }

    [Fact]
    public void Analyzer_SameDefTwiceIsAllowed()
    {
        var prog = AnalyzeOk("10 DEFINT A:DEFINT A-C:A=1:C=2");
        Assert.Equal(new[] { "A%", "C%" }, prog.IntVars);
    }

    /// <summary>DEFxxx より前の行で、その頭文字の接尾辞のない変数を使うと warning (DEFxxx の行ごとに 1 つ)</summary>
    [Theory]
    [InlineData("10 A=1:B=2\n20 DEFINT A-B", 1)]
    [InlineData("10 A=1\n20 DEFINT A\n30 B=\"X\"\n40 DEFSTR B", 2)]
    [InlineData("10 DEFINT A\n20 A=1", 0)]
    [InlineData("10 A%=1:A!=2\n20 DEFINT A", 0)]       // 接尾辞付きは関係ない
    [InlineData("10 A=1:DEFINT A", 0)]                // 同じ行は前の行ではない
    public void Analyzer_WarnsUseBeforeDef(string program, int warnings)
    {
        var (_, diag) = Analyze(program);
        Assert.False(diag.HasErrors, string.Join("; ", diag.Items.Select(d => d.Code + " " + d.Message)));
        Assert.Equal(warnings, diag.Items.Count(d => d.Code == "HB0411" && d.Severity == HuSeverity.Warning));
    }

    [Fact]
    public void Analyzer_WarnsDoubleOncePerVariable()
    {
        var (_, diag) = Analyze("10 DEFDBL A:A=1:A=A+1:B#=2:PRINT B#");
        Assert.False(diag.HasErrors);
        Assert.Equal(2, diag.Items.Count(d => d.Code == "HB0412" && d.Severity == HuSeverity.Warning));
    }

    [Fact]
    public void Analyzer_ForAndNextMatchByKey()
    {
        // DEFINT の下の I と I% は同じ変数なので、NEXT I% は FOR I に対応する
        var prog = AnalyzeOk("10 DEFINT A-Z:FOR I=1 TO 0:NEXT I%");
        Assert.Single(prog.ForExits);
    }

    /// <summary>浮動小数の FOR (段階 F2b)。初めの値は整数に丸めない。FOR と NEXT は変数の鍵で対応する</summary>
    [Fact]
    public void Analyzer_FloatForLoops()
    {
        var prog = AnalyzeOk("10 FOR X=.5 TO 2 STEP .25:FOR I%=1 TO 2:NEXT I%,X!:FOR Y=1 TO 0:NEXT");
        Assert.Equal(3, prog.ForIds.Count);
        Assert.Equal(3, prog.ForExits.Count);   // 回らないときの飛び先 (FOR X は NEXT の X! の部分、FOR Y は最後の NEXT)
        Assert.Equal(1, prog.ForExits.Single(kv => kv.Key.Var == "X").Value.Part);
        var fx = prog.ForIds.Keys.Single(f => f.Var == "X");
        Assert.Equal(HuType.Float, fx.From.Type);
    }

    [Fact]
    public void Analyzer_StaticBytesCountFloatsAsThreeBytes()
    {
        // 浮動小数の変数 3 バイト + 浮動小数の配列 (21 × 3 + 4) + DATA の行の表の終わり 4
        var prog = AnalyzeOk("10 A=1:DIM B(20)");
        Assert.Equal(3 + 21 * 3 + 4 + 4, prog.StaticDataBytes);
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
    /// <summary>段階 G1: SWAP は型の種類 (単精度と倍精度も別) が違えば error。同じ種類なら配列の要素とも組める</summary>
    [Theory]
    [InlineData("10 SWAP A%,B")]
    [InlineData("10 SWAP A,B#")]
    [InlineData("10 SWAP A,B$")]
    [InlineData("10 DIM C%(3):SWAP C%(1),B")]
    public void Analyzer_SwapKindMismatchIsError(string program) => AssertError(program, "HB0404");

    [Theory]
    [InlineData("10 SWAP A,B")]
    [InlineData("10 SWAP A$,B$")]
    [InlineData("10 DIM C(3):SWAP C(1),C(2):SWAP A,C(3):SWAP A,A")]
    [InlineData("10 DEFINT A-Z:SWAP A,B%")]       // DEFINT の下の A と B% は同じ整数
    public void Analyzer_SwapSameKindIsAccepted(string program) => AnalyzeOk(program);

    /// <summary>
    /// 段階 G3: グラフィックの LINE の形。PSET の直後の ,B は箱の印 (色なし)、色の位置の変数は色の式。
    /// テキスト版の LINE (文字の式) は今までどおり
    /// </summary>
    [Fact]
    public void Parser_GraphicLineForms()
    {
        var prog = AnalyzeOk("10 LINE(0,0)-(1,1),PSET:LINE(0,0)-(1,1),PRESET,7:LINE(0,0)-(1,1),PSET,B:LINE(0,0)-(1,1),PSET,,B\n" +
                             "20 LINE(0,0)-(1,1),PSET,C%:LINE(X,Y)-(1.5,2),PSET,7,B:LINE(0,0)-(1,1),\"*\",B");
        var all = SLANGCompiler.HuBasic.Analysis.HuAnalyzer.AllStatements(prog.Lines).ToList();
        var g = all.OfType<HuLineGraphic>().ToList();
        Assert.Equal(6, g.Count);
        Assert.True(g[0] is { Preset: false, Box: false, Color: null });
        Assert.True(g[1] is { Preset: true, Box: false, Color: HuNum { Value: 7 } });
        Assert.True(g[2] is { Box: true, Color: null });
        Assert.True(g[3] is { Box: true, Color: null });
        Assert.True(g[4] is { Box: false, Color: HuVar { Name: "C%" } });
        Assert.True(g[5] is { Box: true, Color: HuNum { Value: 7 } });
        Assert.Single(all.OfType<HuLineText>());
        // 括弧で囲んだ B で始まる式は色、文の後ろの : や ELSE の前の B は箱の印
        prog = AnalyzeOk("10 LINE(0,0)-(1,1),PSET,(B+1):LINE(0,0)-(1,1),PSET,(B),B:LINE(0,0)-(1,1),PSET,B:PRINT\n" +
                         "20 IF 1 THEN LINE(0,0)-(1,1),PSET,B ELSE PRINT");
        g = SLANGCompiler.HuBasic.Analysis.HuAnalyzer.AllStatements(prog.Lines).OfType<HuLineGraphic>().ToList();
        Assert.Equal(4, g.Count);
        Assert.True(g[0] is { Box: false, Color: HuBinary { Op: "+" } });
        Assert.True(g[1] is { Box: true, Color: HuVar { Name: "B" } });
        Assert.True(g[2] is { Box: true, Color: null });
        Assert.True(g[3] is { Box: true, Color: null });
    }

    [Theory]
    [InlineData("10 LINE(0,0)-(1,1),PSET,7,X", "HB0203")]      // 箱の印の位置は B・BF だけ
    [InlineData("10 LINE(0,0)-(1,1),PSET,,X", "HB0203")]
    // 本物は PSET の後の B で始まる名前をすべて箱の印として読み、B 以外は Syntax error (実測。R1-F1 の確かめ)
    [InlineData("10 LINE(0,0)-(1,1),PSET,BX", "HB0203")]
    [InlineData("10 LINE(0,0)-(1,1),PSET,B(0)", "HB0202")]
    [InlineData("10 LINE(0,0)-(1,1),PSET,B+1", "HB0202")]
    [InlineData("10 LINE(0,0)-(1,1),PSET,B+5,B", "HB0202")]
    [InlineData("10 LINE(0,0)-(1,1),PSET,BF+1", "HB0202")]
    [InlineData("10 LINE(0,0)-(1,1),PSET,7,BF,\"AA\"", "HB0301")] // タイルパターンは未対応
    [InlineData("10 LINE(0,0)-(1,1),PSET,7,BX", "HB0203")]
    [InlineData("10 LINE(0,0)-(1,1),PSET,\"A\"", "HB0404")]    // 色は数
    [InlineData("10 LINE(A$,0)-(1,1),PSET", "HB0404")]         // 座標は数
    public void Parser_GraphicLineErrors(string program, string code) => AssertError(program, code);

    /// <summary>段階 G4: グラフィックの LINE の BF (塗りつぶした箱)。箱の印の位置ならどこでも、Box と Fill が真</summary>
    [Fact]
    public void Parser_GraphicLineFillForms()
    {
        var prog = AnalyzeOk("10 LINE(0,0)-(1,1),PSET,BF:LINE(0,0)-(1,1),PSET,7,BF:LINE(0,0)-(1,1),PSET,,BF:LINE(0,0)-(1,1),PRESET,BF\n" +
                             "20 LINE(0,0)-(1,1),PSET,7,B:LINE(0,0)-(1,1),\"*\",BF");
        var all = SLANGCompiler.HuBasic.Analysis.HuAnalyzer.AllStatements(prog.Lines).ToList();
        var g = all.OfType<HuLineGraphic>().ToList();
        Assert.Equal(5, g.Count);
        Assert.True(g[0] is { Box: true, Fill: true, Color: null });
        Assert.True(g[1] is { Box: true, Fill: true, Color: HuNum { Value: 7 } });
        Assert.True(g[2] is { Box: true, Fill: true, Color: null });
        Assert.True(g[3] is { Box: true, Fill: true, Preset: true });
        Assert.True(g[4] is { Box: true, Fill: false });
        Assert.True(all.OfType<HuLineText>().Single() is { Fill: true });
    }

    /// <summary>段階 G4: PAINT (x,y),色[,境界色…]。境界色は 0〜7 個</summary>
    [Fact]
    public void Parser_PaintForms()
    {
        var prog = AnalyzeOk("10 PAINT(50,30),2:PAINT(X,Y+1),C%,4:PAINT(0,0),1.5,1,2,3,4,5,6,7");
        var p = SLANGCompiler.HuBasic.Analysis.HuAnalyzer.AllStatements(prog.Lines).OfType<HuPaint>().ToList();
        Assert.Equal(3, p.Count);
        Assert.True(p[0] is { X: HuNum { Value: 50 }, Y: HuNum { Value: 30 }, Color: HuNum { Value: 2 }, Borders.Count: 0 });
        Assert.True(p[1] is { Color: HuVar { Name: "C%" }, Borders: [HuNum { Value: 4 }] });
        Assert.Equal(7, p[2].Borders.Count);
    }

    [Theory]
    [InlineData("10 PAINT(1,2)", "HB0301")]                     // 色の省略は未対応 (本物は前のグラフィックの命令の色)
    [InlineData("10 PAINT(1,2),,3", "HB0211")]                  // 式が必要 (本物も Syntax error)
    [InlineData("10 PAINT(1,2),1,2,3,4,5,6,7,0,1", "HB0203")]   // 境界色は 7 つまで (本物も 8 つは Syntax error)
    [InlineData("10 PAINT(1,2),\"AA\"", "HB0301")]              // タイルパターンは未対応
    [InlineData("10 PAINT(A$,2),1", "HB0404")]
    [InlineData("10 PAINT(1,2),1,A$", "HB0404")]
    [InlineData("10 PAINT STEP(1,2),1", "HB0203")]
    public void Parser_PaintErrors(string program, string code) => AssertError(program, code);

    /// <summary>B で始まる名前の error は、その名前を括弧で囲んだ形を案内する</summary>
    [Fact]
    public void Parser_GraphicLineBNameSuggestsParentheses()
    {
        var (_, diag) = Analyze("10 LINE(0,0)-(1,1),PSET,BX");
        Assert.Contains(diag.Items, d => d.Code == "HB0203" && d.Message.Contains("(BX)"));
    }

    /// <summary>段階 G2: 文字列の配列の要素も SWAP できる。数の変数・配列との組み合わせは型が違う</summary>
    [Theory]
    [InlineData("10 SWAP A$(0),A$(1)", false)]
    [InlineData("10 SWAP A$,A$(1)", false)]
    [InlineData("10 DIM B$(3):SWAP A$(I),B$(I+1)", false)]
    [InlineData("10 SWAP A$(0),B(0)", true)]
    [InlineData("10 SWAP A$(0),B%", true)]
    public void Analyzer_SwapOfStringArrayElement(string program, bool mismatch)
    {
        if (mismatch) AssertError(program, "HB0404");
        else AnalyzeOk(program);
    }

    /// <summary>
    /// 段階 G2: 文字列の配列 (DIM あり・なし、DEFSTR の頭文字)。A$ と A$(…)、A$(…) と A(…) は別のもの。
    /// 記憶域は 1 要素 256 バイトで、静的なデータに (上限 + 1) × 256 + 4 バイトとして数える
    /// </summary>
    [Fact]
    public void Analyzer_StringArrays()
    {
        var prog = AnalyzeOk("10 A$=\"S\":A$(0)=\"E\":A(0)=5:DIM B$(20),C(3):B$(20)=A$(0)+B$(1)");
        Assert.Equal(10, prog.Arrays["A$"]);
        Assert.Equal(20, prog.Arrays["B$"]);
        Assert.Equal(10, prog.Arrays["A!"]);
        Assert.Equal(63, AnalyzeOk("10 DIM D$(63)").Arrays["D$"]);
        Assert.Contains("A$", prog.StrVars);
        var bytes = AnalyzeOk("10 A$(1)=\"X\"").StaticDataBytes - AnalyzeOk("10 PRINT \"X\"").StaticDataBytes;
        Assert.Equal(11 * 256 + 4, bytes);
        prog = AnalyzeOk("10 DEFSTR A:DIM A(3):A(1)=\"X\":READ A(2):INPUT A(3)");
        Assert.Equal(10, prog.Arrays["A$"]);
    }

    /// <summary>段階 G1: PLAY は文字列なら MML、数ならテンポ。KEY は 2 つ目が文字列で、warning HB0704 を出す</summary>
    [Fact]
    public void Analyzer_PlayNumberAndKey()
    {
        AnalyzeOk("10 PLAY 144:A=2.5:PLAY A:PLAY \"C\"");
        var (_, diag) = Analyze("10 KEY 0,\"\"");
        Assert.False(diag.HasErrors);
        Assert.Contains(diag.Items, d => d.Code == "HB0704" && d.Severity == HuSeverity.Warning);
        AssertError("10 KEY 0,1", "HB0404");
        AssertError("10 KEY \"A\",\"B\"", "HB0404");
        AssertError("10 A=SCRN$(0,0,1)", "HB0404");
        AssertError("10 A$=SCRN$(\"A\",0,1)", "HB0404");
        AssertError("10 PALET \"A\",1", "HB0404");
    }

    // ------------------------------------------------------------ 段階 G5a

    /// <summary>段階 G5a: 文が終わった所で ':' がなくても次の文を読む (本物と同じ。warning も出さない)</summary>
    [Theory]
    [InlineData("10 A=1 B=2:PRINT A;B")]
    [InlineData("10 COLOR 6 LOCATE 0,5 PRINT \"X\"")]
    [InlineData("10 LOCATE 1,7PRINT\"X\"")]
    [InlineData("10 COLOR4LOCATE0,2")]
    [InlineData("10 FOR I=1 TO 2 PRINT I:NEXT")]
    [InlineData("10 GOTO 10 PRINT \"X\"")]
    [InlineData("10 WIDTH 80 INIT:CLICK OFF")]
    [InlineData("10 A=1 'comment")]
    [InlineData("10 IF A THEN B=1 C=2 ELSE D=3 E=4")]
    [InlineData("10 A$=\"X\" MID$(A$,1)=\"Y\"")]
    public void Parser_StatementsWithoutColon(string program)
    {
        var (_, diag) = Analyze(program);
        Assert.Empty(diag.Items);
    }

    /// <summary>文がまだ引数を読みにいく所 (PRINT の項目・CLS の省略できる引数) と、文の始まりになれない字句では止まる</summary>
    [Theory]
    [InlineData("10 PRINT \"A\" PRINT \"B\"", "HB0302")]
    [InlineData("10 CLS PRINT", "HB0302")]
    [InlineData("10 A=1 THEN", "HB0202")]
    [InlineData("10 A=1 2", "HB0202")]
    [InlineData("10 A=1 \"X\"", "HB0202")]
    [InlineData("10 A=1 RND", "HB0202")]
    public void Parser_StatementsWithoutColonStillFail(string program, string code) => AssertError(program, code);

    /// <summary>段階 G5a: LABEL と名前への GOTO・GOSUB・ON (行番号と混ぜてよい)。同じ名前は前の方で、後ろに warning</summary>
    [Fact]
    public void Analyzer_Labels()
    {
        var prog = AnalyzeOk("10 GOTO \"B\":ON A GOSUB \"S\",30:ON B GOTO 30,\"B\"\n20 PRINT 1:LABEL\"B\":IF A THEN LABEL \"S\":RETURN\n30 RETURN");
        Assert.Equal(2, prog.LabelIds.Count);
        Assert.Equal(0, prog.LabelTargets["B"]);
        Assert.Equal(1, prog.LabelTargets["S"]);
        var (dup, diag) = Analyze("10 LABEL\"D\":LABEL\"D\"\n20 GOTO \"D\"");
        Assert.False(diag.HasErrors);
        Assert.Contains(diag.Items, d => d.Code == "HB0505" && d.Severity == HuSeverity.Warning);
        Assert.Equal(0, dup!.LabelTargets["D"]);
        Assert.Equal(2, dup.LabelIds.Count);
    }

    [Theory]
    [InlineData("10 GOTO \"b\"\n20 LABEL\"B\"", "HB0504")]      // 大文字小文字を区別する (本物と同じ)
    [InlineData("10 GOTO \"B \"\n20 LABEL\"B\"", "HB0504")]     // 空白も区別する
    [InlineData("10 GOSUB \"X\"", "HB0504")]
    [InlineData("10 ON A GOTO 10,\"X\"", "HB0504")]
    [InlineData("10 GOTO A$", "HB0301")]                          // 名前の式は未対応 (本物は書ける)
    [InlineData("10 GOTO \"A\"+\"B\"", "HB0301")]
    [InlineData("10 LABEL A$", "HB0301")]
    [InlineData("10 RESTORE \"D\"", "HB0301")]
    [InlineData("10 RETURN \"D\"", "HB0301")]
    [InlineData("10 ON A RESTORE \"D\"", "HB0301")]
    [InlineData("10 LABELX=5", "HB0301")]                         // LABEL は予約語 (本物も Syntax error)
    public void Analyzer_LabelErrors(string program, string code) => AssertError(program, code);

    // ------------------------------------------------------------ 段階 G5b

    /// <summary>段階 G5b: POKE@・PEEK@ は @ までで 1 つの予約語</summary>
    [Theory]
    [InlineData("POKE@&H3000,65", "POKE@ n:12288 , n:65")]
    [InlineData("A=PEEK@(1)+PEEK(2)", "id:A = PEEK@ ( n:1 ) + PEEK ( n:2 )")]
    [InlineData("TIME=0", "TIME = n:0")]
    public void Lexer_PokePeekAt(string body, string expected)
        => Assert.Equal(expected, Kinds(Lex(body)));

    [Fact]
    public void Analyzer_PokePeekCallTime()
    {
        var prog = AnalyzeOk("10 POKE &HF000,1,2,3:POKE@&H3000,65:A=PEEK(&HF000)+PEEK@(1):CALL &HD000:TIME=0:T=TIME");
        var stmts = HuAnalyzer.AllStatements(prog.Lines).ToList();
        var pokes = stmts.OfType<HuPoke>().ToList();
        Assert.Equal(3, pokes[0].Values.Count);
        Assert.False(pokes[0].Io);
        Assert.True(pokes[1].Io);
        Assert.Contains(stmts, s => s is HuCommand { Name: "CALL" });
        Assert.Contains(stmts, s => s is HuCommand { Name: "TIME" });
        var t = (HuAssign)stmts.Last();
        Assert.Equal(HuType.Float, t.Value.Type);
    }

    [Theory]
    [InlineData("10 CALL &HD000,1", "HB0202")]      // 引数は取らない (本物も Syntax error)
    [InlineData("10 POKE 1", "HB0203")]
    [InlineData("10 POKE A$,1", "HB0404")]
    [InlineData("10 POKE 1,A$", "HB0404")]
    [InlineData("10 A=PEEK(A$)", "HB0404")]
    [InlineData("10 TIME=A$", "HB0404")]
    [InlineData("10 A$=TIME", "HB0404")]
    [InlineData("10 TIME 5", "HB0203")]
    public void Analyzer_PokePeekCallTimeErrors(string program, string code) => AssertError(program, code);
}
