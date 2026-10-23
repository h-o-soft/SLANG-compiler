using System.Diagnostics;
using System.Text.RegularExpressions;
using SLANGCompiler.HuBasic;
using SLANGCompiler.HuBasic.Lowering;
using SLANGCompiler.Runtime;
using Xunit;
using SkippableFactAttribute = Xunit.SkippableFactAttribute;

namespace SLANGCompiler.Tests;

/// <summary>
/// hub2sl の共通の展開と SLANG 生成、生成コードの slangc 通過、ランタイムの契約のテスト。
/// </summary>
public class HuBasicEmitTests
{
    private static string RepoRoot()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir != null && !File.Exists(Path.Combine(dir.FullName, "runtime", "env", "x1native.env")))
            dir = dir.Parent;
        Assert.NotNull(dir);
        return dir!.FullName;
    }

    private static HuTranspileResult Transpile(string program, HuTranspileOptions? opt = null)
    {
        var r = HuTranspiler.Transpile(program, opt);
        Assert.False(r.Diagnostics.HasErrors, string.Join("; ", r.Diagnostics.Items.Select(d => d.Code + " " + d.Message)));
        Assert.NotNull(r.Output);
        return r;
    }

    /// <summary>段階 1 の機能をひととおり使う小さなプログラム (自作)</summary>
    private const string Stage1Program = """
        10 DEFINT A-Z:CLS
        20 PRINT "A";1=1 AND 2;NOT 0=1;2+7\2;2+7 MOD 3;A MOD B;C\D
        30 FOR I=1 TO 3:PRINT I;:NEXT:FOR I=3 TO 1 STEP -1:NEXT I
        40 FOR I=1 TO 2:FOR J=2 TO 1:NEXT J,I
        50 GOSUB 200:ON A GOSUB 200,210:ON B GOTO 60,70
        60 N=0:REPEAT:N=N+1:UNTIL N=3:WHILE N<4:N=N+2:WEND
        70 IF N=4 THEN PRINT "T"; ELSE PRINT "F";
        80 DIM X(3):X(0)=10:Y(10)=X(0)/3:PRINT 1,2;SPC(3)
        90 LOCATE 30,22:COLOR 2:COLOR ,1:CLS 4:WIDTH 80:INIT:OUT &H3000,65:K=INP(&H3000)
        100 PAUSE 1:END
        200 K=K+1:RETURN
        210 RETURN 60
        """;

    /// <summary>MVP のすべての命令・関数を使うプログラム (自作)。ランタイムの契約と組み立ての通しに使う</summary>
    /// <summary>段階 F1a の浮動小数の式をひととおり使う小さなプログラム (自作。変数は整数と文字列だけ)</summary>
    private const string FloatProgram = """
        10 DEFINT A-Z:CLS
        20 PRINT 2.5;-.25;40000;1E18;1/3:A=5/2*2:B=7/2:C=X/Y*2:D=-2.5
        30 IF RND<.6 THEN E=INT(RND*3)-1
        40 IF .25 THEN 50 ELSE 60
        50 F=(1.5<X)+(X=2.5)-(NOT(X>.5)):G=INT(X)+INT(-2.5)
        60 ON 1.6 GOTO 70,70
        70 FOR I=1 TO 2.5:NEXT:H=7\2.6+(7 MOD 2.6)+(3.5 AND 6)
        80 END
        """;

    private const string FullProgram = """
        10 DEFINT A-Z:CLS:INIT:WIDTH 40:CLICK OFF:TEMPO 300
        20 A$="AB":B$=A$+CHR$(65):C$=MID$(A$,1,1)+MID$(A$,2)+RIGHT$(A$,1)+HEXCHR$("41")+CGPAT$(65)+INKEY$
        30 N=ASC(A$)+VAL("&H10")+STICK(0)+STRIG(1)+INP(&H3000):IF A$<B$ THEN N=N/3
        40 READ D$,M:RESTORE 900:ON N RESTORE 900,910:PRINT USING "**###";M;:PRINT#0 "X";SPC(2);N,D$
        50 COLOR 5,1:LOCATE 1,1:CFLASH 1:CFLASH:CSIZE 3:CSIZE:CGEN 1:CGEN 0
        60 LINE(0,0)-(5,5),"*",B:LINE(0,0)-(5,3),"#":DEFCHR$(130)=HEXCHR$("FF"):OUT &H3000,65
        70 PLAY "O4C5D#E:E:G":BEEP:PAUSE 1:X(1)=N\2+N MOD 3:DIM Y(4)
        80 FOR I=1 TO 2:NEXT:GOSUB 100:ON N GOSUB 100:ON N GOTO 90
        90 REPEAT:UNTIL 1:WHILE 0:WEND:END
        100 RETURN
        900 DATA AB,1
        910 DATA C
        """;

    // ------------------------------------------------------------ 展開

    [Fact]
    public void Lowering_ZeroTripForExitLabelIsAfterItsVariablePart()
    {
        var flat = Transpile("10 DEFINT A-Z:FOR I=1 TO 2:FOR J=2 TO 1:NEXT J,I").Flat!;
        var ops = flat.Ops.ToList();
        int IndexOf(Func<FlatOp, bool> p) => ops.FindIndex(o => p(o));
        int nextJ = IndexOf(o => o is FCall { Function: "BAS_FORNEXT" } c && c.Args[0] is FAddrArg { Var: SLANGCompiler.HuBasic.Parsing.HuVar { Name: "J" } });
        int nextI = IndexOf(o => o is FCall { Function: "BAS_FORNEXT" } c && c.Args[0] is FAddrArg { Var: SLANGCompiler.HuBasic.Parsing.HuVar { Name: "I" } });
        int exitJ = IndexOf(o => o is FLabel { Name: "QFX1" });
        int exitI = IndexOf(o => o is FLabel { Name: "QFX0" });
        Assert.True(nextJ < exitJ && exitJ < nextI && nextI < exitI, $"J={nextJ} xJ={exitJ} I={nextI} xI={exitI}");
    }

    [Fact]
    public void Lowering_PrintResetsTempsPerItemAndReadPerItem()
    {
        var flat = Transpile("10 DEFINT A-Z:PRINT A$+B$;C$+D$:READ E$,F$\n20 DATA X,Y").Flat!;
        var calls = flat.Ops.OfType<FCall>().Select(c => c.Function).ToList();
        var text = string.Join(",", calls.SkipWhile(c => c != "BAS_PSTR"));
        Assert.StartsWith("BAS_PSTR,BAS_STMP,BAS_PSTR,BAS_STMP,BAS_PNL,BAS_STMP,BAS_SSET,BAS_STMP,BAS_SSET,BAS_STMP", text);
    }

    /// <summary>PRINT USING と SPC の項目も、一時文字列を作るなら項目ごとに空に戻す</summary>
    [Fact]
    public void Lowering_PrintUsingAndSpcResetTempsPerItem()
    {
        var flat = Transpile("10 PRINT USING \"#\";VAL(A$+A$);VAL(A$+A$):PRINT SPC(VAL(A$+A$));SPC(VAL(A$+A$))").Flat!;
        var calls = flat.Ops.OfType<FCall>().Select(c => c.Function).ToList();
        var text = string.Join(",", calls.SkipWhile(c => c != "BAS_PUFMT"));
        Assert.StartsWith("BAS_PUFMT,BAS_STMP,BAS_PUSING,BAS_STMP,BAS_PUSING,BAS_STMP,BAS_PNL,BAS_STMP,BAS_PSPC,BAS_STMP,BAS_PSPC,BAS_STMP,BAS_PNL", text);
    }

    /// <summary>READ は項目を一時文字列で読むので、前の文の一時文字列が残らないよう、文の頭でも必ず空に戻す</summary>
    [Fact]
    public void Lowering_ReadResetsTempsBeforeFirstItem()
    {
        var flat = Transpile("10 A$=RIGHT$(B$,255):READ C$\n20 DATA X").Flat!;
        var calls = flat.Ops.OfType<FCall>().Select(c => c.Function).ToList();
        int first = calls.IndexOf("BAS_SSET"), second = calls.LastIndexOf("BAS_SSET");
        Assert.True(first >= 0 && second > first);
        // A$ の代入と READ C$ の間に、一時領域を空に戻す呼び出しがある
        Assert.Contains("BAS_STMP", calls.GetRange(first + 1, second - first - 1));
    }

    [Fact]
    public void Lowering_NumericReadResetsTemps()
    {
        // BAS_READN は項目を一時文字列で読むので、数値の READ の後にも一時領域を空に戻す
        var flat = Transpile("10 RESTORE:READ A%,B%:GOTO 10\n20 DATA 1,2").Flat!;
        var ops = flat.Ops.ToList();
        var reads = ops.Select((o, i) => (o, i)).Where(x => x.o is FAssign { Value: SLANGCompiler.HuBasic.Parsing.HuCall { Name: FlatPseudo.ReadNum } }).ToList();
        Assert.Equal(2, reads.Count);
        foreach (var (_, i) in reads)
            Assert.Equal("BAS_STMP", Assert.IsType<FCall>(ops[i + 1]).Function);
    }

    /// <summary>文が持つ式のどこで一時文字列を作っても、その文の頭で一時領域を空に戻す (繰り返し実行してもあふれない)</summary>
    [Theory]
    [InlineData("10 A%(VAL(CHR$(49)))=0:GOTO 10")]                 // 代入先の添字
    [InlineData("10 FOR I%=VAL(CHR$(49)) TO 2:NEXT:GOTO 10")]      // FOR の初期値
    [InlineData("10 FOR I%=1 TO VAL(CHR$(50)):NEXT:GOTO 10")]      // FOR の上限
    [InlineData("10 FOR I%=1 TO 2 STEP VAL(CHR$(49)):NEXT:GOTO 10")] // FOR の増分
    [InlineData("10 OUT VAL(CHR$(49)),0:GOTO 10")]
    [InlineData("10 LOCATE VAL(CHR$(49)),0:GOTO 10")]
    [InlineData("10 ON VAL(CHR$(49)) GOTO 10")]
    [InlineData("10 IF VAL(CHR$(49)) THEN 10")]
    [InlineData("10 REPEAT:UNTIL VAL(CHR$(49)):GOTO 10")]
    [InlineData("10 LINE(VAL(CHR$(49)),0)-(1,1),\"*\":GOTO 10")]
    [InlineData("10 DEFCHR$(VAL(CHR$(49)))=\"\":GOTO 10")]
    [InlineData("10 READ A%(VAL(CHR$(49))):RESTORE:GOTO 10\n20 DATA 1")]
    public void Lowering_EveryAllocatingStatementResetsTemps(string program)
    {
        var r = Transpile(program);
        var ops = r.Flat!.Ops.ToList();
        // 各行の頭からの命令列で、一時文字列を作る呼び出しより前に BAS_STMP があるか
        var sl = r.Output!;
        foreach (var line in sl.Split('\n').Where(l => l.Contains("BAS_SCHR(")))
        {
            var idx = sl.IndexOf(line, StringComparison.Ordinal);
            var before = sl[..idx];
            var lastLabel = Math.Max(before.LastIndexOf("L10:", StringComparison.Ordinal), before.LastIndexOf("QL", StringComparison.Ordinal));
            Assert.True(before.LastIndexOf("BAS_STMP();", StringComparison.Ordinal) > lastLabel,
                $"BAS_STMP が {line.Trim()} の前にない:\n{sl}");
        }
        Assert.Contains(ops, o => o is FCall { Function: "BAS_STMP" });
    }

    [Fact]
    public void Lowering_StatementExprsCoversEveryExpressionField()
    {
        // 構文木の文の型のうち式を持つものは、すべて StatementExprs で式を返す
        var program = HuBasicEmitTests_FullProgramForExprs;
        var prog = Transpile(program).Program!;
        foreach (var stmt in SLANGCompiler.HuBasic.Analysis.HuAnalyzer.AllStatements(prog.Lines))
        {
            var props = stmt.GetType().GetProperties().Where(p =>
                typeof(SLANGCompiler.HuBasic.Parsing.HuExpr).IsAssignableFrom(p.PropertyType) && p.Name != "Pos").ToList();
            var listed = HuLowerer.StatementExprs(stmt).ToList();
            foreach (var p in props)
                if (p.GetValue(stmt) is SLANGCompiler.HuBasic.Parsing.HuExpr e)
                    Assert.Contains(listed, x => ReferenceEquals(x, e));
        }
    }

    private const string HuBasicEmitTests_FullProgramForExprs = FullProgram;

    // ------------------------------------------------------------ 浮動小数 (段階 F1a)

    private static string Body(string sl) => sl[sl.IndexOf("MAIN()", StringComparison.Ordinal)..];

    [Fact]
    public void Float_IntegerOnlyProgramHasNoFloatCode()
    {
        // 整数だけのプログラムには、浮動小数の宣言・定数の表・関数が一切出ない
        foreach (var prog in new[] { Stage1Program, FullProgram })
        {
            var sl = Transpile(prog).Output!;
            Assert.DoesNotContain("FLOAT", sl);
            Assert.DoesNotContain("H_FK", sl);
            Assert.DoesNotMatch(@"BAS_F(TOIR|DIV|CMP|FLOOR|STR)\(|BAS_PFLT|FRAND", sl);
        }
    }

    [Fact]
    public void Float_DivisionUsesIdivrOnlyWhereRoundedImmediately()
    {
        var sl = Body(Transpile("10 DEFINT A-Z:A=B/C:D=B/C*2:E=(B/C)\\1").Output!);
        Assert.Contains("V_A=BAS_IDIVR(V_B, V_C);", sl);              // 整数に入れる位置の / は整数の割り算
        Assert.Matches(@"V_D=BAS_FTOIR\(\(BAS_FDIV\(&H_F\d, &H_F\d\)\*H_FK\[\d\]\)\);", sl); // それ以外は浮動小数
        Assert.Contains("V_E=((BAS_IDIVR(V_B, V_C))./.(1));", sl);    // \ の左右は整数に丸めてから
    }

    [Fact]
    public void Float_ConstantsComeFromTableWithoutExponent()
    {
        var sl = Transpile("10 DEFINT A-Z:PRINT 1E18;2.5;40000;2.5:A=X+.000123").Output!;
        var table = Regex.Match(sl, @"ARRAY FLOAT H_FK\[\] = \{ (.*) \};").Groups[1].Value;
        Assert.Equal(new[] { "1000000000000000000.0", "2.5", "40000.0", "0.000123" }, table.Split(", "));
        // 本体には FLOAT の定数を直接書かない (SLANG が小数部のない FLOAT の定数を整数として畳み込むため)
        Assert.DoesNotMatch(@"\d\.\d", Body(sl));
    }

    [Fact]
    public void Float_TempsAreDeclaredWithFloatEachAndBeforeTheLine()
    {
        var sl = Transpile("10 DEFINT A-Z:A=X/Y+Z/W").Output!;
        Assert.Contains("VAR FLOAT H_F1, FLOAT H_F2, FLOAT H_F3, FLOAT H_F4, FLOAT H_F5;", sl);
        // 一時変数への代入は、その文の行の前に、左から順に出る (左の割り算は、右の割り算の準備より先に計算する)
        var lines = Body(sl).Split('\n').Select(l => l.Trim()).Where(l => l.Length > 0).ToList();
        int a = lines.FindIndex(l => l.StartsWith("V_A="));
        Assert.Equal(new[] { "H_F1=V_X;", "H_F2=V_Y;", "H_F5=BAS_FDIV(&H_F1, &H_F2);", "H_F3=V_Z;", "H_F4=V_W;" },
            lines.GetRange(a - 5, 5));
    }

    /// <summary>定数を整数に丸めるときは、実行時と同じ f24 の値で丸める</summary>
    [Theory]
    [InlineData(".499999", 1)]      // f24 では .5 になる
    [InlineData("-.499999", -1)]
    [InlineData(".4999", 0)]
    [InlineData("1.4", 1)]
    [InlineData("2.5", 3)]
    public void Float_ConstantRoundingUsesF24Value(string literal, int expected)
    {
        var sl = Body(Transpile($"10 DEFINT A-Z:A={literal}").Output!);
        Assert.Contains($"V_A={(expected < 0 ? $"(0-{-expected})" : expected.ToString())};", sl);
        // 65535.4 は f24 では 65535.5 になり、丸めると範囲を超えるので、変換のときに丸めず実行時のエラー 6 に任せる
        Assert.Contains("V_A=BAS_FTOIR(H_FK[0]);", Body(Transpile("10 DEFINT A-Z:A=65535.4").Output!));
    }

    [Theory]
    [InlineData("1E19", false)]
    [InlineData("1.8E19", false)]
    [InlineData("1.8446E19", false)]    // 最大 (2^63 × 1.99998、約 1.84466E19) より少し下
    [InlineData("1.8447E19", true)]     // 丸めると無限大
    [InlineData("1E20", true)]
    public void Float_LiteralRangeMatchesF24(string literal, bool rejected)
    {
        var r = HuTranspiler.Transpile($"10 DEFINT A-Z:PRINT {literal}");
        Assert.Equal(rejected, r.Diagnostics.Items.Any(d => d.Code == "HB0103"));
    }

    [Fact]
    public void Float_QuantizeMatchesSlangEncoding()
    {
        Assert.True(double.IsPositiveInfinity(SLANGCompiler.HuBasic.HuF24.Quantize(1.8447e19)));
        Assert.Equal(SLANGCompiler.HuBasic.HuF24.MaxFinite, SLANGCompiler.HuBasic.HuF24.Quantize(SLANGCompiler.HuBasic.HuF24.MaxFinite));
        Assert.Equal(4.0, SLANGCompiler.HuBasic.HuF24.Quantize(3.99999));   // 仮数の繰り上がりで指数が 1 上がる
        Assert.Equal(0.5, SLANGCompiler.HuBasic.HuF24.Quantize(0.499999));
        Assert.Equal(65536.0, SLANGCompiler.HuBasic.HuF24.Quantize(65535.9));
        Assert.Equal(40000.0, SLANGCompiler.HuBasic.HuF24.Quantize(40000));
        Assert.Equal(-2.5, SLANGCompiler.HuBasic.HuF24.Quantize(-2.5));
    }

    /// <summary>整数の定数を浮動小数にするときは 16 ビットの符号付きの値 (&HFFFF は -1。変数に入れて比べたときと同じ)</summary>
    [Fact]
    public void Float_IntegerLiteralPromotionIsSigned()
    {
        var sl = Transpile("10 DEFINT A-Z:PRINT &HFFFF<0.5;&H8000*1.5").Output!;
        var table = Regex.Match(sl, @"ARRAY FLOAT H_FK\[\] = \{ (.*) \};").Groups[1].Value.Split(", ");
        Assert.Contains("-1.0", table);
        Assert.Contains("-32768.0", table);
        Assert.DoesNotContain("65535.0", table);
    }

    /// <summary>浮動小数の定数の表と一時変数も、静的なデータの上限に数える</summary>
    [Fact]
    public void Float_StorageCountsTowardDataBudget()
    {
        const string prog = "10 DEFINT A-Z:PRINT 1.5;2.5";
        var baseBytes = HuTranspiler.Transpile(prog).Program!.StaticDataBytes;  // 浮動小数の分を含まない
        var ok = HuTranspiler.Transpile(prog, new HuTranspileOptions(DataBudget: baseBytes + 6));
        Assert.False(ok.Diagnostics.HasErrors);                                  // 定数 2 つ = 6 バイトまでなら通る
        var ng = HuTranspiler.Transpile(prog, new HuTranspileOptions(DataBudget: baseBytes + 5));
        Assert.Contains(ng.Diagnostics.Items, d => d.Code == "HB0309");
        Assert.Null(ng.Output);
    }

    /// <summary>後ろの式が一時変数の前置きを出しても、前にある入力・乱数などの式が先に評価される</summary>
    [Fact]
    public void Float_HoistingPreservesEvaluationOrder()
    {
        var sl = Body(Transpile("10 DEFINT A-Z:A=ASC(INKEY$)\\(ASC(INKEY$)*1.0+1.0)").Output!);
        var lines = sl.Split('\n').Select(l => l.Trim()).Where(l => l.Length > 0).ToList();
        int first = lines.FindIndex(l => l.Contains("BAS_SINKEY()"));
        // 左の INKEY$ が整数の一時変数に先に入り、右の INKEY$ (浮動小数の一時変数への代入) はその後
        Assert.StartsWith("H_I1=BAS_SASC(BAS_SINKEY())", lines[first]);
        Assert.StartsWith("H_F1=BAS_SASC(BAS_SINKEY())", lines[first + 1]);
        Assert.Contains("V_A=BAS_IDIV(H_I1, ", sl);
        Assert.Contains("VAR H_I1;", Transpile("10 DEFINT A-Z:A=ASC(INKEY$)\\(ASC(INKEY$)*1.0+1.0)").Output!);
        // 実行時エラーになりうる左の式 (配列の要素) も先に評価する (添字の範囲外が 0 で割るより先に出る)
        var err = Body(Transpile("10 DEFINT A-Z:A=A(11)\\((1/0)/1)").Output!).Split('\n').Select(l => l.Trim()).ToList();
        int arr = err.FindIndex(l => l.StartsWith("H_I1=A_A[BAS_AIDX("));
        int div = err.FindIndex(l => l.Contains("BAS_FDIV("));
        Assert.True(arr >= 0 && arr < div, string.Join("\n", err));
        // 整数にするとエラーになる浮動小数の定数 (70000) も先に変換する (あふれが 0 で割るより先に出る)
        var ovf = Body(Transpile("10 DEFINT A-Z:A=70000\\((1.0/0.0)/1.0)").Output!).Split('\n').Select(l => l.Trim()).ToList();
        int conv = ovf.FindIndex(l => l.StartsWith("H_I1=BAS_FTOIR("));
        int div0 = ovf.FindIndex(l => l.Contains("BAS_FDIV("));
        Assert.True(conv >= 0 && conv < div0, string.Join("\n", ovf));
        // 整数にできる浮動小数の定数 (2.5) は変換のときに 3 になるので、そのまま
        Assert.DoesNotContain("H_I1", Body(Transpile("10 DEFINT A-Z:A=2.5\\(Y*1.5)").Output!));
        // 定数と単純変数の左の式は、そのまま (一時変数を増やさない)
        Assert.DoesNotContain("H_I1", Body(Transpile("10 DEFINT A-Z:A=X\\(Y*1.5)").Output!));
    }

    [Theory]
    [InlineData("10 DEFINT A-Z:PRINT RND(1)", false)]
    [InlineData("10 DEFINT A-Z:PRINT RND(-1.5)", false)]
    [InlineData("10 DEFINT A-Z:PRINT RND(X)", true)]      // 式の引数は評価を省けないので未対応
    [InlineData("10 DEFINT A-Z:PRINT RND(1/0)", true)]
    public void Float_RndArgumentMustBeConstant(string program, bool rejected)
    {
        var r = HuTranspiler.Transpile(program);
        Assert.Equal(rejected, r.Diagnostics.Items.Any(d => d.Code == "HB0302"));
    }

    /// <summary>整数の -X は整数で計算してから浮動小数にする (X が -32768 のとき値が変わらないように)</summary>
    [Fact]
    public void Float_IntegerNegationIsComputedBeforePromotion()
    {
        var sl = Body(Transpile("10 DEFINT A-Z:PRINT (-X)<.5;-2.5").Output!);
        Assert.Contains("H_F1=(0-(V_X));", sl);          // 整数で符号を変えてから一時変数へ
        Assert.Contains("BAS_PFLT((H_FK[", sl);           // 浮動小数の -2.5 は浮動小数で計算
    }

    /// <summary>浮動小数を含む定数式は、項ごとに丸めて整数として計算しない (\ の 0 の検査を外さない)</summary>
    [Fact]
    public void Float_ConstantExpressionsWithFloatAreNotFoldedAsIntegers()
    {
        var sl = Body(Transpile("10 DEFINT A-Z:A=7\\(.5-.4):B=7\\2.5:C=7 MOD (1.4*2)").Output!);
        Assert.Contains("V_A=BAS_IDIV(7, BAS_FTOIR((H_FK[", sl);   // 0 で割るかは実行時に調べる
        Assert.Contains("V_B=((7)./.(3));", sl);                    // 定数 1 つなら変換のときに丸めてよい (2.5 → 3)
        Assert.Contains("V_C=BAS_IMOD(7, BAS_FTOIR(", sl);
    }

    [Fact]
    public void Float_ComparisonAndConditionAndConversion()
    {
        var sl = Body(Transpile("10 DEFINT A-Z:IF X*.25 THEN 10\n20 C=(X<1.5):A=40000:B=-2.5:PRINT RND;INT(X):IF .25 THEN 10").Output!);
        Assert.Contains("IF (BAS_FTOIR((H_F1*H_FK[0])))==0 THEN", sl); // 浮動小数の条件は丸めて判定
        Assert.Contains("IF (0)==0 THEN", sl);                         // 定数の条件は変換のときに丸める (.25 → 0)
        Assert.Matches(@"BAS_FCMP\(&H_F1, \(H_FK\+\d+\)\) \.<\. 0", sl); // 比較は BAS_FCMP と 0
        Assert.Contains("V_A=(0-25536);", sl);                        // 定数の丸めは変換のときに (40000 → -25536)
        Assert.Contains("V_B=(0-3);", sl);
        Assert.Contains("BAS_PFLT(FRAND());", sl);
        Assert.Contains("BAS_PFLT(BAS_FFLOOR(H_F1));", sl);           // INT は浮動小数を返す (Hu-BASIC と同じ)
        Assert.Contains("V_G=V_X;", Body(Transpile("10 DEFINT A-Z:G=INT(X)").Output!)); // 整数に入れる INT(整数) は何もしない
    }

    [Fact]
    public void Lowering_WhileResetsTempsInsideLoopHead()
    {
        var flat = Transpile("10 DEFINT A-Z:WHILE INKEY$=\"\":WEND").Flat!;
        var ops = flat.Ops.ToList();
        int top = ops.FindIndex(o => o is FLabel { Name: "QL0" });
        Assert.IsType<FCall>(ops[top + 1]);
        Assert.Equal("BAS_STMP", ((FCall)ops[top + 1]).Function);
    }

    [Fact]
    public void Lowering_ClsWithoutArgumentIsTextOnly()
    {
        var flat = Transpile("10 CLS:CLS 0").Flat!;
        var cls = flat.Ops.OfType<FCall>().Where(c => c.Function == "BAS_CLS").ToList();
        Assert.Equal(-1, ((FIntArg)cls[0].Args[0]).Value);
    }

    [Fact]
    public void Lowering_FrameWaitDirective()
    {
        var flat = Transpile("10 A%=1:'#VSYNC 3", new HuTranspileOptions(Wait: HuWaitMode.Frame)).Flat!;
        var v = flat.Ops.OfType<FCall>().Single(c => c.Function == "BAS_VSYNC");
        Assert.Equal(3, ((FIntArg)v.Args[0]).Value);
        var none = Transpile("10 A%=1:'#VSYNC 3").Flat!;
        Assert.DoesNotContain(none.Ops.OfType<FCall>(), c => c.Function == "BAS_VSYNC");
    }

    // ------------------------------------------------------------ SLANG 生成

    [Fact]
    public void Slang_NamesAreSeparatedAndArraysChecked()
    {
        var sl = Transpile("10 DEFINT A-Z:A=1:A(2)=A:A%=3:A$=\"Q\"").Output!;
        Assert.Contains("VAR V_A, V_A_I, H_T", sl);
        Assert.Contains("ARRAY A_A[10];", sl);
        Assert.Contains("ARRAY BYTE S_A[255];", sl);
        Assert.Contains("A_A[BAS_AIDX(2, &H_DIM_A, &H_UB_A)]=V_A;", sl);
        Assert.Contains("BAS_SSET(S_A, K_0);", sl);
    }

    [Fact]
    public void Slang_ComparisonValueIsMinusOneAndConditionIsDirect()
    {
        var sl = Transpile("10 DEFINT A-Z:B=(A<1):IF A<1 THEN B=2").Output!;
        Assert.Contains("V_B=(0-((V_A) .<. (1)));", sl);
        Assert.Contains("IF ((V_A) .<. (1))==0 THEN GOTO", sl);
    }

    [Fact]
    public void Slang_DivisionVariants()
    {
        var sl = Transpile("10 DEFINT A-Z:A=B\\C:A=B\\4:A=B MOD C:A=B MOD 4:A=B/C").Output!;
        Assert.Contains("V_A=BAS_IDIV(V_B, V_C);", sl);
        Assert.Contains("V_A=((V_B)./.(4));", sl);
        Assert.Contains("V_A=BAS_IMOD(V_B, V_C);", sl);
        Assert.Contains("V_A=((V_B).MOD.(4));", sl);
        Assert.Contains("V_A=BAS_IDIVR(V_B, V_C);", sl);
    }

    [Fact]
    public void Slang_GosubReturnAndForDispatch()
    {
        var sl = Transpile("10 DEFINT A-Z:GOSUB 30:FOR I=1 TO 2:NEXT\n20 END\n30 RETURN").Output!;
        Assert.Contains("BAS_GPUSH(0);", sl);
        Assert.Contains("H_RET:", sl);
        Assert.Contains("H_T=BAS_GPOP();", sl);
        Assert.Contains("CASE H_T OF { 0: GOTO R0; };", sl);
        Assert.Contains("H_T=BAS_FORPUSH(&V_I, 2, 1, 0);", sl);
        Assert.Contains("CASE H_T OF { 0: GOTO QFT0; };", sl);
    }

    /// <summary>実行時エラーの「in 行番号」のため、各行の頭で行番号を記録する</summary>
    [Fact]
    public void Slang_RecordsCurrentLineForErrorMessages()
    {
        var sl = Transpile("10 A%=1\n20 A%=2").Output!;
        Assert.Contains("BAS_LNINIT(&H_LINE);", sl);
        Assert.Matches(@"L10:\s+H_LINE=10;", sl);
        Assert.Matches(@"L20:\s+H_LINE=20;", sl);
    }

    /// <summary>GOSUB・ON GOSUB から戻った所で、実行中の行番号を呼び出した行に戻す</summary>
    [Fact]
    public void Slang_RestoresCurrentLineAfterGosub()
    {
        var sl = Transpile("10 GOSUB 100:ON 1 GOSUB 100:A%=1\n20 END\n100 RETURN").Output!;
        var afterReturns = System.Text.RegularExpressions.Regex.Matches(sl, @"\bR\d+:\s+H_LINE=(\d+);");
        Assert.Equal(2, afterReturns.Count);
        Assert.All(afterReturns, m => Assert.Equal("10", m.Groups[1].Value));
    }

    [Fact]
    public void Slang_DataTables()
    {
        var sl = Transpile("10 DEFINT A-Z:READ A:RESTORE 20\n20 DATA 7").Output!;
        Assert.Contains("BAS_DATAINIT(H_DATA, H_DLINE);", sl);
        Assert.Contains("ARRAY H_DLINE[] = { %20,%0,%$FFFF,%2 };", sl);
        Assert.Contains("V_A=BAS_READN();", sl);
        Assert.Contains("BAS_RESTORE(20);", sl);
    }

    /// <summary>生成した SLANG が既存のコンパイラ (字句 → 構文 → 意味 → IR → コード生成) をエラーなく通る</summary>
    [Fact]
    public void Slang_GeneratedCodeCompilesWithSlangc()
    {
        var sl = Transpile(Stage1Program).Output!;
        var diag = new DiagnosticBag();
        var tokens = new Lexer.Lexer(sl).Tokenize();
        tokens = new Lexer.Preprocessor(diag).Process(tokens, ".");
        var ast = new Parser.Parser(tokens, diag).ParseCompilationUnit();
        Assert.False(diag.HasErrors, $"Parse errors: {string.Join("; ", diag.Diagnostics)}");
        var analyzer = new Semantics.SemanticAnalyzer(diag);
        analyzer.Analyze(ast);
        var ir = new IR.IrGenerator(diag, analyzer.Symbols).Generate(ast);
        Assert.False(diag.HasErrors, $"IR errors: {string.Join("; ", diag.Diagnostics)}");
        var asm = new CodeGen.CodeGenerator(ir).Generate();
        Assert.Contains("CALL\tBAS_INIT", asm);
    }

    // ------------------------------------------------------------ ランタイムの契約

    [Fact]
    public void Runtime_AllCalledFunctionsExistWithParamCounts()
    {
        var path = Path.Combine(RepoRoot(), "runtime", "libx1_basic.asm");
        var functions = RuntimeParser.Parse(File.ReadAllText(path), path).ToDictionary(f => f.Name);
        var sl = Transpile(Stage1Program).Output! + Transpile(FullProgram).Output! + Transpile(FloatProgram).Output!;
        var called = Regex.Matches(sl, @"\b(BAS_[A-Z0-9]+)\(").Select(m => m.Groups[1].Value).Distinct().ToList();
        Assert.NotEmpty(called);
        foreach (var name in called)
        {
            Assert.True(functions.ContainsKey(name), $"{name} が libx1_basic.asm にない");
            var f = functions[name];
            Assert.Equal(RuntimeResidency.Shared, f.Residency);
            // @calls に書いた依存がすべて (どこかのランタイムに) 存在するかは組み立ての通しで確かめる
        }
        Assert.True(functions["BAS_FORPUSH"].CalleeCleanup, "4 引数の BAS_FORPUSH は callee cleanup");
        Assert.True(functions["BAS_TLINE"].CalleeCleanup, "6 引数の BAS_TLINE は callee cleanup");
        // ランタイム内部の @calls もすべて、このファイルか x1native の env のライブラリにある
        var root = RepoRoot();
        var envLibs = EnvironmentLoader.Load(Path.Combine(root, "runtime", "env", "x1native.env")).Libraries;
        var all = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var lib in envLibs)
        {
            var lp = Path.Combine(root, "runtime", Path.GetFileName(lib));
            if (File.Exists(lp))
                foreach (var f in RuntimeParser.Parse(File.ReadAllText(lp), lp)) all.Add(f.Name);
        }
        foreach (var f in functions.Values)
            foreach (var dep in f.Dependencies)
                Assert.True(all.Contains(dep), $"{f.Name} の @calls {dep} がどのライブラリにもない");
    }

    /// <summary>DATA のないプログラムで RESTORE を呼んでも、初期化されていない行の表を走査しない</summary>
    [Fact]
    public void Runtime_RestoreGuardsMissingDataTable()
    {
        var path = Path.Combine(RepoRoot(), "runtime", "libx1_basic.asm");
        var f = RuntimeParser.Parse(File.ReadAllText(path), path).Single(x => x.Name == "BAS_RESTORE");
        var code = f.Code.Split('\n').Select(l => l.Split(';')[0].Trim()).Where(l => l.Length > 0).ToList();
        int load = code.IndexOf("LD HL, (BAS_DLTAB)");
        Assert.True(load >= 0 && load < 3, "BAS_RESTORE は最初に行の表があるかを見る");
        Assert.Equal("JP Z, .brs_end", code[load + 3]);
        // hub2sl は DATA がなければ BAS_DATAINIT を呼ばないが、RESTORE は受け付ける
        var sl = Transpile("10 RESTORE 10:READ A%:END").Output!;
        Assert.DoesNotContain("BAS_DATAINIT", sl);
        Assert.Contains("BAS_RESTORE(10);", sl);
    }

    [Theory]
    [InlineData("x1native")]
    [InlineData("x1native_slfs")]
    public void Runtime_EnvListsLibrary(string env)
    {
        var config = EnvironmentLoader.Load(Path.Combine(RepoRoot(), "runtime", "env", env + ".env"));
        Assert.Contains(config.Libraries, l => Path.GetFileName(l).StartsWith("libx1_basic"));
    }

    /// <summary>hub2sl の出力を slangbuild で AILZ80ASM まで通し、BAS_ 関数がリンクされた bin ができる</summary>
    [SkippableFact]
    public void Build_TranspiledProgramAssemblesForX1Native()
    {
        var root = RepoRoot();
        var asmPath = Path.Combine(root, "tools", OperatingSystem.IsWindows() ? "AILZ80ASM.exe" : "AILZ80ASM");
        Skip.IfNot(File.Exists(asmPath), "AILZ80ASM がない (make setup-tools で取得)");
        var dir = Path.Combine(Path.GetTempPath(), $"hub2sl_{Guid.NewGuid():N}");
        Directory.CreateDirectory(dir);
        try
        {
            var sl = Path.Combine(dir, "PROG.SL");
            File.WriteAllText(sl, Transpile(Stage1Program).Output!);
            var sl2 = Path.Combine(dir, "FULL.SL");
            File.WriteAllText(sl2, Transpile(FullProgram).Output!);
            var psi = new ProcessStartInfo("dotnet")
            {
                WorkingDirectory = dir,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
            };
            RunSlangbuild(root, dir, sl, asmPath, Path.Combine(dir, "PROG"));
            AssertBuilt(Path.Combine(dir, "PROG"));
            RunSlangbuild(root, dir, sl2, asmPath, Path.Combine(dir, "FULL"));
            AssertBuilt(Path.Combine(dir, "FULL"));
            // 浮動小数の式 (libfloat を呼ぶ) も、x1native と x1native_slfs の両方で組み立てまで通る
            var sl3 = Path.Combine(dir, "FLT.SL");
            File.WriteAllText(sl3, Transpile(FloatProgram).Output!);
            RunSlangbuild(root, dir, sl3, asmPath, Path.Combine(dir, "FLT"));
            AssertBuilt(Path.Combine(dir, "FLT"));
            RunSlangbuild(root, dir, sl3, asmPath, Path.Combine(dir, "FLTS"), "x1native_slfs");
            AssertBuilt(Path.Combine(dir, "FLTS"));
        }
        finally
        {
            try { Directory.Delete(dir, true); } catch { }
        }
    }

    private static void RunSlangbuild(string root, string dir, string sl, string asmPath, string outBase, string env = "x1native")
    {
        var psi = new ProcessStartInfo("dotnet")
        {
            WorkingDirectory = dir,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
        };
        psi.Environment["SLANG_HOME"] = root;
        psi.Environment["MSBUILDDISABLENODEREUSE"] = "1";
        foreach (var a in new[] { "run", "--project", Path.Combine(root, "src", "SLANGCompiler.Build", "SLANGCompiler.Build.csproj"),
                     "-c", "Release", "--", sl, "-E", env, "--asm", asmPath, "--keep-asm", "-o", outBase })
            psi.ArgumentList.Add(a);
        using var proc = Process.Start(psi)!;
        var so = proc.StandardOutput.ReadToEndAsync();
        var se = proc.StandardError.ReadToEndAsync();
        Assert.True(proc.WaitForExit(180_000), "slangbuild timed out");
        Assert.True(proc.ExitCode == 0, $"slangbuild failed:\n{so.Result}\n{se.Result}");
    }

    /// <summary>bin ができていて、作業領域 (__WORKEND__) まで含めて $1000〜予約領域 $FFE0 の手前に収まる</summary>
    private static void AssertBuilt(string outBase)
    {
        var bin = outBase + ".bin";
        Assert.True(File.Exists(bin), $"{bin} がない");
        var lst = outBase + ".LST";
        Assert.True(File.Exists(lst), $"{lst} がない");
        var workEnd = SLANGCompiler.HuBasic.CLI.Program.ReadWorkEnd(lst);
        Assert.NotNull(workEnd);
        Assert.True(0x1000 + new FileInfo(bin).Length < 0xFFE0);
        Assert.True(workEnd < 0xFFE0, $"作業領域の終わり ${workEnd:X4} が予約領域にかかる");
    }

    /// <summary>DATA の行の表は 2 バイトずつの要素としてアセンブルされる (初期値の並びの既定は 1 バイト)</summary>
    [Fact]
    public void Slang_DataLineTableIsAssembledAsWords()
    {
        var sl = Transpile("10 DEFINT A-Z:READ A\n900 DATA 7").Output!;
        var diag = new DiagnosticBag();
        var tokens = new Lexer.Preprocessor(diag).Process(new Lexer.Lexer(sl).Tokenize(), ".");
        var ast = new Parser.Parser(tokens, diag).ParseCompilationUnit();
        var analyzer = new Semantics.SemanticAnalyzer(diag);
        analyzer.Analyze(ast);
        var asm = new CodeGen.CodeGenerator(new IR.IrGenerator(diag, analyzer.Symbols).Generate(ast)).Generate();
        Assert.False(diag.HasErrors, string.Join("; ", diag.Diagnostics));
        var table = asm.Split('\n').SkipWhile(l => !l.Contains("_V_H_DLINE:")).Skip(1).First();
        Assert.Contains("$84,$03", table.Replace(" ", ""));   // 900 = $0384 (下位・上位の順)
        Assert.Contains("$FF,$FF", table.Replace(" ", ""));
    }
}
