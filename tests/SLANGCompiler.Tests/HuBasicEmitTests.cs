using System.Diagnostics;
using System.Text.RegularExpressions;
using SLANGCompiler.HuBasic;
using SLANGCompiler.HuBasic.Lowering;
using SLANGCompiler.Runtime;
using Xunit;
using SkippableFactAttribute = Xunit.SkippableFactAttribute;

namespace SLANGCompiler.Tests;

/// <summary>
/// hubasic2sl の共通の展開と SLANG 生成、生成コードの slangc 通過、ランタイムの契約のテスト。
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
    /// <summary>段階 F1b の文字列と画面の命令・関数をひととおり使う小さなプログラム (自作)</summary>
    private const string StringScreenProgram = """
        10 DEFINT A-Z:CLS:A$="ABCDEF":MID$(A$,2,3)="xy":MID$(A$,5)="12"
        20 B$=LEFT$(A$,2)+STRING$(3,"AB")+STRING$(2,65)+CHR$(29,29,65)+CHARACTER$(0,0)
        30 LINE(1,1)-(5,5),"#",BF:LINE(1,1)-(5,5),"*",B:LINE(1,1)-(5,5),"-"
        40 END
        """;

    /// <summary>段階 F1c の入力と音の命令・関数をひととおり使う小さなプログラム (自作)</summary>
    private const string InputSoundProgram = """
        10 DEFINT A-Z:CLS:KBUF ON:SCREEN 0,0
        20 INPUT "NAME";N$:INPUT "X",A,B$:INPUT C(1)
        30 I$=INPUT$(1)+INKEY$(0):SOUND 7,62:SOUND 8,15:BEEP 1:BEEP 0:BEEP
        40 END
        """;

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

    /// <summary>段階 F2a の浮動小数の変数と配列をひととおり使う小さなプログラム (自作)</summary>
    private const string FloatVarProgram = """
        10 CLS:DEFDBL D:DIM R(20)
        20 A=1.5:B=A*2-A/4:I%=A:X=I%:R(I%)=A/B:R(2)=R(1)+X:D=A:PRINT A;B;-A;R(1);D;D!
        30 IF A THEN IF A<B THEN ON A GOTO 40,40
        40 INPUT C,R(3):C%=C+R(3):WHILE A<4:A=A+1:WEND
        50 FOR X=0 TO 1 STEP .25:FOR I%=1 TO 2:NEXT I%:NEXT:FOR X=C TO C*2:NEXT X
        60 END
        """;

    /// <summary>段階 F3a の数の関数と ^ をひととおり使う小さなプログラム (自作)</summary>
    private const string MathProgram = """
        10 DEFINT I:I=-5:X=2.5
        20 PRINT ABS(I);ABS(X);SGN(I);SGN(X);FIX(I);FIX(X);CINT(X);FRAC(X);CSNG(I);CDBL(X)
        30 PRINT SQR(X);SIN(X);COS(X);TAN(X);ATN(X);LOG(X);EXP(X);PAI(1);RAD(X);X^I;2^.5;-2^2
        40 END
        """;

    /// <summary>段階 F3b の文字列の関数・TAB・RANDOMIZE をひととおり使う小さなプログラム (自作。PLAY と TAB を一緒に使う)</summary>
    private const string StringFuncProgram = """
        10 DEFINT I:A$="ABCABC":I=2
        20 PRINT LEN(A$);INSTR(A$,"C");INSTR(I,A$,"B");HEX$(I*100);"[";SPACE$(I);"]";TAB(10);"X"
        30 RANDOMIZE:RANDOMIZE I:RANDOMIZE 40000:PRINT RND
        40 PLAY "CDE":END
        """;

    /// <summary>段階 G1 の 8 種類の命令・関数をひととおり使う小さなプログラム (自作。REPEAT ON/OFF と REPEAT のループを一緒に使う)</summary>
    private const string G1Program = """
        10 DEFINT I:I=2:X=1.5:A$="AB":B$="C":DIM C(3),D%(3)
        20 PLAY 144:PLAY X:PLAY "CDE":CGEN 1:CGEN:KEY 0,"":KEY I,A$:REPEAT OFF:REPEAT ON
        30 PRINT "AB";CHR$(29);"X";SCRN$(0,0,2);SCRN$(I,X,I+1)
        40 SWAP X,Y:SWAP A$,B$:SWAP C(I),C(I+1):SWAP D%(1),I:SWAP X,C(2)
        50 PALET 3,5:PALET:PALET I,X:PRW 2:PRW:PRW I
        60 REPEAT:I=I+1:UNTIL I>5:END
        """;

    /// <summary>段階 G2 の文字列の配列をひととおり使う小さなプログラム (自作。DIM あり・なし、代入・式・READ・INPUT・SWAP)</summary>
    private const string G2Program = """
        10 DEFINT I:I=2:A$="S":DIM P$(1),N$(20)
        20 P$(0)="ABCD":P$(1)="pqrs":P$(I-1)=P$(I-1)+A$
        30 PRINT P$(0);LEN(P$(1));P$(0)<P$(1):SWAP P$(0),P$(1):SWAP A$,P$(1)
        40 READ N$(I):INPUT N$(20):IF N$(I)="X" THEN PRINT RIGHT$(N$(20),1)
        50 Q$(10)="Q":DATA X
        """;

    /// <summary>段階 G3 のグラフィックの LINE をひととおり使う小さなプログラム (自作。テキスト版の LINE と一緒に使う)</summary>
    private const string G3Program = """
        10 DEFINT C:C=3:X=1.5:WIDTH 40:CLS 4
        20 LINE(143,0)-(176,8),PSET,7,B:LINE(0,0)-(10,10),PSET:LINE(X,1)-(5,C),PRESET
        30 LINE(1,2)-(3,4),PSET,B:LINE(1,2)-(3,4),PSET,,B:LINE(1,2)-(3,4),PSET,C,B:LINE(1,2)-(3,4),PRESET,5,B
        40 LINE(0,0)-(5,5),"*",B:END
        """;

    /// <summary>段階 G4 のグラフィックの BF と PAINT をひととおり使う小さなプログラム (自作)</summary>
    private const string G4Program = """
        10 DEFINT C:C=3:X=1.5:WIDTH 40:CLS 4
        20 LINE(10,10)-(50,30),PSET,3,BF:LINE(0,0)-(5,5),PSET,BF:LINE(1,2)-(3,4),PRESET,C,BF:LINE(1,2)-(3,4),PSET,,BF
        30 LINE(10,10)-(100,60),PSET,4,B:PAINT(50,30),2,4:PAINT(X,C+1),5:PAINT(0,0),1,1,2,3,4,5,6,7
        40 PAINT(1,1),ASC(MID$("AB",C,1))-64,C:END
        """;

    /// <summary>段階 G5 の命令 (区切りなし・LABEL・POKE・PEEK・POKE@・PEEK@・CALL・TIME・はみ出す線) をひととおり使う小さなプログラム (自作)</summary>
    private const string G5Program = """
        10 DEFINT C:C=3:X=1.5:WIDTH 40 INIT:CLS 4
        20 GOTO "S":LABEL"B":POKE &HF010,1,C,X:POKE@&H3000+C,65,66
        30 A=PEEK(&HF010)+PEEK@(&H3000):CALL &HF020:TIME=X:TIME=60000:T=TIME+TIME:C=TIME
        40 ON C GOSUB "B",50:END
        50 LABEL"S":LINE(-10,-10)-(400,300),PSET,7:LINE(0-X,5)-(10,5),PRESET:RETURN
        """;

    /// <summary>MIRROR$・スペース入りの HEXCHR$・MUSIC (PLAY の別名) を使う小さなプログラム (自作)</summary>
    private const string MirrorProgram = """
        10 A$=HEXCHR$("0102 0304 5"):B$=MIRROR$(A$)+MIRROR$(HEXCHR$("83")):PRINT LEN(B$)
        20 C$="CDE":MUSIC C$:MUSIC 120:X=150:MUSIC X:PLAY "C":MUSIC "D":END
        """;

    /// <summary>2 次元の配列と BIN$ を使う小さなプログラム (自作)</summary>
    private const string TwoDimProgram = """
        10 DEFINT A-Z:DIM A(2,20),B$(3,4):A(1,2)=C(3,4)+A(0,0):B$(1,1)="X":SWAP B$(1,1),B$(2,2):SWAP A(1,1),A(2,2)
        20 READ A(0,1),B$(0,1):DIM F!(3,3):F!(1,2)=1.5:X=F!(.5,1):PRINT BIN$(A(1,2));BIN$(-1);B$(2,2)
        30 S$=CHR$(1):A(ASC(S$)\128,(ASC(MIRROR$(S$))\128)+0.0)=1:FOR I=0 TO 3:A(I,LEN(STR$(I)))=1:NEXT
        40 DATA 7,Y
        """;

    /// <summary>CONSOLE (テキストの窓) と CHR$(14)・CHR$(15) を使う小さなプログラム (自作)</summary>
    private const string ConsoleProgram = """
        10 CONSOLE 5,10,3,20:PRINT "A","B";CHR$(14);CHR$(15):LOCATE 3,14:CLS
        20 A=2:CONSOLE A,A+1:CONSOLE:PRINT "Z":END
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
        // VAL は浮動小数なので、PRINT USING は BAS_PUSINGF
        Assert.StartsWith("BAS_PUFMT,BAS_STMP,BAS_PUSINGF,BAS_STMP,BAS_PUSINGF,BAS_STMP,BAS_PNL,BAS_STMP,BAS_PSPC,BAS_STMP,BAS_PSPC,BAS_STMP,BAS_PNL", text);
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
    [InlineData("10 SWAP A%(LEN(CHR$(49))),B%(1):GOTO 10")]       // SWAP の左の添字 (段階 G1)
    [InlineData("10 SWAP A%(1),B%(LEN(CHR$(49))):GOTO 10")]       // SWAP の右の添字
    [InlineData("10 A$(LEN(CHR$(49)))=\"X\":GOTO 10")]            // 文字列の配列の代入先の添字 (段階 G2)
    [InlineData("10 SWAP A$(LEN(CHR$(49))),A$(1):GOTO 10")]       // 文字列の配列の要素の SWAP
    [InlineData("10 IF A$(LEN(CHR$(49)))=\"\" THEN 10")]          // 式の中の要素の添字
    [InlineData("10 LINE(LEN(CHR$(49)),0)-(1,1),PSET:GOTO 10")]   // グラフィックの LINE の座標 (段階 G3)
    [InlineData("10 LINE(0,0)-(1,1),PSET,LEN(CHR$(49)),B:GOTO 10")] // グラフィックの LINE の色
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

    [Theory]
    [InlineData(HuBasicEmitTests_FullProgramForExprs)]
    [InlineData(G1Program)]
    [InlineData(G2Program)]
    [InlineData(G3Program)]
    [InlineData(G4Program)]
    [InlineData(G5Program)]
    [InlineData(MirrorProgram)]
    [InlineData(ConsoleProgram)]
    public void Lowering_StatementExprsCoversEveryExpressionField(string program)
    {
        // 構文木の文の型のうち式を持つものは、すべて StatementExprs で式を返す
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

    // ------------------------------------------------------------ 文字列と画面 (段階 F1b)

    [Fact]
    public void Slang_StageF1bCalls()
    {
        var sl = Body(Transpile(StringScreenProgram).Output!);
        Assert.Contains("BAS_SMIDSET(S_A, 2, 3, K_", sl);
        Assert.Contains("BAS_SMIDSET(S_A, 5, 255, K_", sl);              // 長さの省略は 255
        Assert.Contains("BAS_SMID(S_A, 1, 2)", sl);                       // LEFT$ は MID$(s, 1, n)
        Assert.Contains("BAS_SSTR(3, K_", sl);
        Assert.Contains("BAS_SSTR(2, BAS_SCHR(65))", sl);                // 文字コードは 1 文字にしてから繰り返す
        Assert.Matches(@"H_I1=BAS_SALLOC\(3\);\s+BAS_SPUTC\(H_I1, 1, 29\);\s+BAS_SPUTC\(H_I1, 2, 29\);\s+BAS_SPUTC\(H_I1, 3, 65\);", sl);
        Assert.Contains("BAS_SCHARA(0, 0)", sl);
        Assert.Matches(@"BAS_TLINE\(1, 1, 5, 5, K_\d+, 2\);", sl);      // BF は 2、B は 1、線は 0
        Assert.Matches(@"BAS_TLINE\(1, 1, 5, 5, K_\d+, 1\);", sl);
        Assert.Matches(@"BAS_TLINE\(1, 1, 5, 5, K_\d+, 0\);", sl);
    }

    [Fact]
    public void Slang_StageF1cCalls()
    {
        var sl = Body(Transpile(InputSoundProgram).Output!);
        Assert.Matches(@"BAS_INPUT\(K_\d+, 1\);\s+BAS_SSET\(S_N, BAS_INFS\(\)\);\s+BAS_STMP\(\);", sl);
        Assert.Matches(@"BAS_INPUT\(K_\d+, 0\);\s+V_A=BAS_FTOIR\(BAS_INFN\(\)\);", sl);  // 数は浮動小数で読んで丸める
        Assert.Contains("BAS_INPUT(0, 1);", sl);                         // 案内なしは「? 」だけ
        Assert.Contains("BAS_SCAT(BAS_SINPUT(1), BAS_SINKEY0())", sl);
        Assert.Contains("BAS_SOUND(7, 62);", sl);
        Assert.Contains("BAS_BEEPN(1);", sl);
        Assert.Contains("BAS_BEEPN(0);", sl);
        Assert.Contains("BAS_BEEP();", sl);
        Assert.DoesNotContain("KBUF", sl);
        Assert.DoesNotContain("SCREEN", sl);
    }

    [Fact]
    public void Runtime_KeyInputDoesNotAllocateWhileWaiting()
    {
        // キーを待つ INPUT・INPUT$ は、一時文字列を作らない BAS_KEYIN で待つ (待つ間に一時領域があふれないように)
        var path = Path.Combine(RepoRoot(), "runtime", "libx1_basic.asm");
        var fs = RuntimeParser.Parse(File.ReadAllText(path), path).ToDictionary(f => f.Name);
        Assert.Contains("BAS_KEYIN", fs["BAS_INPUT"].Dependencies);
        Assert.Contains("BAS_KEYIN", fs["BAS_SINPUT"].Dependencies);
        Assert.DoesNotContain("BAS_SINKEY", fs["BAS_INPUT"].Dependencies);
        Assert.DoesNotContain("BAS_TALLOC", fs["BAS_KEYIN"].Dependencies);
    }

    /// <summary>MID$ の文の書き込みは前から写す (自分自身を重ねて代入したとき、HuBASIC と同じく書いた文字をまた読む)</summary>
    [Fact]
    public void Runtime_MidSetCopiesForwardLikeHuBasic()
    {
        var path = Path.Combine(RepoRoot(), "runtime", "libx1_basic.asm");
        var f = RuntimeParser.Parse(File.ReadAllText(path), path).Single(x => x.Name == "BAS_SMIDSET");
        var code = f.Code.Split('\n').Select(l => l.Split(';')[0].Trim()).Where(l => l.Length > 0).ToList();
        Assert.Contains("LDIR", code);
        Assert.DoesNotContain("LDDR", code);
    }

    /// <summary>MML の O・V の後に数字がなければ、エラーにせずオクターブ・音量をそのままにする (HuBASIC の実測: "v5cvc" は 2 つとも音量 5)</summary>
    [Fact]
    public void Runtime_MmlOctaveAndVolumeWithoutDigitKeepValue()
    {
        var path = Path.Combine(RepoRoot(), "runtime", "libx1_basic.asm");
        var f = RuntimeParser.Parse(File.ReadAllText(path), path).Single(x => x.Name == "BAS_PLNOTE");
        var code = f.Code.Split('\n').Select(l => l.Split(';')[0].Trim()).Where(l => l.Length > 0).ToList();
        foreach (var label in new[] { ".bpn_oct", ".bpn_vol" })
        {
            int i = code.IndexOf(label);
            Assert.True(i >= 0, label);
            Assert.Equal("CALL .bpn_digit", code[i + 1]);
            Assert.Equal("JP C, .bpn_parse", code[i + 2]);
        }
    }

    /// <summary>INPUT の数の読み取り (BAS_SVALF) は、符号の前でも後でも &H・&O・&B を整数の VAL に任せる (-&H10 は -16)</summary>
    [Fact]
    public void Runtime_SvalfDelegatesSignedRadix()
    {
        var path = Path.Combine(RepoRoot(), "runtime", "libx1_basic.asm");
        var f = RuntimeParser.Parse(File.ReadAllText(path), path).Single(x => x.Name == "BAS_SVALF");
        var code = f.Code.Split('\n').Select(l => l.Split(';')[0].Trim()).Where(l => l.Length > 0).ToList();
        int sign = code.IndexOf(".bsv_sg1");
        int digits = code.IndexOf(".bsv_int");
        Assert.True(sign >= 0 && digits > sign);
        Assert.Contains("CP '&'", code.GetRange(sign, digits - sign));       // 符号の後
        Assert.Contains("CP '&'", code.GetRange(0, sign));                   // 符号の前
        Assert.Contains("BAS_SVAL", f.Dependencies);
        // 小さくするときは 10^k で割る (10^-k は f24 で正確でなく、0.50000 が 0.5 を下回るため)
        Assert.Contains("f24div", f.Dependencies);
        Assert.Contains(".bsv_tz", code);                                   // 仮数の後ろの 0 を先に除く
    }

    /// <summary>引数の多い CHR$ も、結果の長さ分だけを確保する (42 個でも、1 行に書ける最大の 110 個でも一時領域があふれない)</summary>
    [Theory]
    [InlineData(42)]
    [InlineData(110)]
    public void Slang_ManyArgumentChrAllocatesOnce(int n)
    {
        var args = string.Join(",", Enumerable.Repeat("9", n));
        var sl = Body(Transpile($"10 DEFINT A-Z:A$=CHR$({args})").Output!);
        Assert.Contains($"BAS_SALLOC({n})", sl);
        Assert.DoesNotContain("BAS_SCAT", sl);
        Assert.Equal(n, Regex.Matches(sl, @"BAS_SPUTC\(").Count);
    }

    [Fact]
    public void Lowering_MidSetResetsTemps()
    {
        // MID$ の文の右辺で一時文字列を作るなら、文の頭で一時領域を空に戻す
        var ops = Transpile("10 DEFINT A-Z:MID$(A$,1)=CHR$(65):GOTO 10").Flat!.Ops.OfType<FCall>().Select(c => c.Function).ToList();
        int i = ops.IndexOf("BAS_SMIDSET");
        Assert.True(i > 0 && ops.GetRange(0, i).Contains("BAS_STMP"));
    }

    // ------------------------------------------------------------ 浮動小数 (段階 F1a)

    private static string Body(string sl) => sl[sl.IndexOf("MAIN()", StringComparison.Ordinal)..];

    [Fact]
    public void Float_IntegerOnlyProgramHasNoFloatCode()
    {
        // 整数だけのプログラムには、浮動小数の宣言・定数の表・関数が一切出ない (VAL と数の READ を使わないもの。
        // VAL と数の READ は HuBASIC と同じく浮動小数で読む: 設計 1 章の意図した変更 2・5)
        foreach (var prog in new[] { Stage1Program, FullProgram.Replace("VAL(", "ASC(").Replace("READ D$,M:", "READ D$:") })
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
        Assert.Equal(4, table.Split(", ").Length);                // 1E18・2.5・40000・.000123 (同じ値は 1 つにまとめる)
        Assert.Contains("2.5", table.Split(", "));
        Assert.Contains("40000.0", table.Split(", "));
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
    [InlineData("1.84466E19", false)]   // 丸めると最大の値
    [InlineData("1.84467E19", true)]    // 2^64 より小さいが、丸めで仮数が繰り上がって無限大
    [InlineData("1.8447E19", true)]
    [InlineData("1E20", true)]
    public void Float_LiteralRangeMatchesF24(string literal, bool rejected)
    {
        var r = HuTranspiler.Transpile($"10 DEFINT A-Z:PRINT {literal}");
        Assert.Equal(rejected, r.Diagnostics.Items.Any(d => d.Code == "HB0103"));
    }

    /// <summary>定数の表の綴りは f24 の値を正確に表す (SLANG が f24 に直しても同じ値になる)</summary>
    [Theory]
    [InlineData(0.4999980926513671875, "0.5")]          // f24 では .5
    [InlineData(2.5, "2.5")]
    [InlineData(40000, "40000.0")]
    [InlineData(1e18, "1000001427415760896.0")]          // f24 での 1E18 の値 (仮数 16 ビット)
    [InlineData(0.1, "0.100000381469726562")]
    public void Float_LiteralSpellsExactF24Value(double v, string expected)
    {
        var text = SLANGCompiler.HuBasic.Emit.Slang.SlangBackend.FloatLiteral(v);
        Assert.StartsWith(expected, text);
        var back = double.Parse(text, System.Globalization.CultureInfo.InvariantCulture);
        Assert.Equal(SLANGCompiler.HuBasic.HuF24.Quantize(v), back);             // 綴りを読み直すと f24 の値そのもの
        Assert.Equal(back, SLANGCompiler.HuBasic.HuF24.Quantize(back));          // SLANG が丸め直しても変わらない
    }

    /// <summary>10 進の定数は、INPUT (BAS_SVALF) と同じく有効 9 桁で読み、f24 へ 1 回だけ四捨五入する</summary>
    [Theory]
    [InlineData("0.5", 0.5)]
    [InlineData("0.50000", 0.5)]
    [InlineData("5E-1", 0.5)]
    [InlineData("0.4999980927", 0.4999961853027344)]   // 10 桁目は捨てる (INPUT と同じ。0.5 にはならない)
    [InlineData("52431.5", 52431.5)]
    [InlineData("524315E-1", 52431.5)]
    [InlineData("12345678901", 12345671680.0)]            // 整数部の 10 桁目以降も捨てて 10 倍に数える (12345678900 の f24)
    [InlineData("1D3", 1000.0)]
    public void Float_DecimalLiteralReadLikeInput(string text, double expected)
    {
        Assert.Equal(expected, SLANGCompiler.HuBasic.HuF24.ParseDecimal(text));
        Assert.Equal(expected, SLANGCompiler.HuBasic.HuF24.Quantize(expected));    // f24 の値そのもの
    }

    [Fact]
    public void Float_ConstantFoldingMatchesRuntimeAtBoundary()
    {
        // 変換のときに丸める A と、実行時に丸める B が同じ値の f24 を使う (有効 9 桁で読むので .5 にはならず、どちらも 0)
        var sl = Transpile("10 DEFINT A-Z:A=.4999980926513671875:B=.4999980926513671875*1.0").Output!;
        Assert.Contains("V_A=0;", Body(sl));
        Assert.Contains("ARRAY FLOAT H_FK[] = { 0.499996185302734375, 1.0 };", sl);
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
        Assert.Contains("BAS_PFLT(BAS_FFLOOR(H_F1));", sl);           // INT は浮動小数を返す (HuBASIC と同じ)
        Assert.Contains("V_G=V_X;", Body(Transpile("10 DEFINT A-Z:G=INT(X)").Output!)); // 整数に入れる INT(整数) は何もしない
    }

    /// <summary>
    /// F3a: 値が整数で済む関数は Int (ABS・FIX は引数が Int のときだけ)、ほかは Float。数学の関数は libfloat をそのまま、
    /// 範囲を調べる関数と ^ はランタイムの BAS_ 関数を呼ぶ
    /// </summary>
    [Fact]
    public void Math_FunctionsTypesAndCalls()
    {
        var sl = Body(Transpile(MathProgram).Output!);
        foreach (var expected in new[]
        {
            "BAS_PNUM(BAS_IABS(V_I));", "BAS_PFLT(FABS(F_X));", "BAS_PNUM(BAS_ISGN(V_I));", "BAS_PNUM(BAS_FSGN(F_X));",
            "BAS_PNUM(V_I);", "BAS_PFLT(BAS_FFIX(F_X));", "BAS_PNUM(BAS_FTOIR(F_X));", "BAS_PFLT(BAS_FFRAC(F_X));",
            "BAS_PFLT(F_X);", "BAS_PFLT(BAS_FSQR(F_X));", "BAS_PFLT(FSIN(F_X));", "BAS_PFLT(FCOS(F_X));",
            "BAS_PFLT(FTAN(F_X));", "BAS_PFLT(FATAN(F_X));", "BAS_PFLT(BAS_FLOG(F_X));", "BAS_PFLT(FEXP(F_X));",
            "BAS_PFLT((F_X*H_FK[", "BAS_FPOW(&F_X, &H_F1)",
        })
            Assert.Contains(expected, sl);
        Assert.Matches(@"BAS_PFLT\(\(H_FK\[\d+\]-BAS_FPOW\(", sl);         // -2^2 は -(2^2)
        // CINT は整数に入れる位置と同じ (定数は変換のときに丸める)、FIX・ABS の Int の引数は整数のまま
        var sl2 = Body(Transpile("10 DEFINT A-Z:A=CINT(2.5):B=FIX(C)+ABS(D):E=SGN(-.1)").Output!);
        Assert.Contains("V_A=3;", sl2);
        Assert.Contains("V_B=((V_C)+(BAS_IABS(V_D)));", sl2);
        Assert.Contains("V_E=BAS_FSGN(", sl2);
        // ^ はいつも Float (2^3 も)。整数に入れるときに丸める
        Assert.Contains("V_A=BAS_FTOIR(BAS_FPOW(", Body(Transpile("10 DEFINT A-Z:A=2^3").Output!));
        // PAI・RAD は予約語: 式の中の PAI(1) は配列ではなく π の関数 (設計 1 章の 8 の b)
        var sl3 = Transpile("10 DEFINT A-Z:PRINT PAI(1)").Output!;
        Assert.DoesNotContain("A_PAI", sl3);
        Assert.Matches(@"BAS_PFLT\(\(H_FK\[\d+\]\*H_FK\[\d+\]\)\);", sl3);
    }

    /// <summary>F3b: LEN・INSTR は Int、HEX$・SPACE$ は一時文字列。TAB は PRINT の項目、RANDOMIZE は式を評価して捨てる</summary>
    [Fact]
    public void StringFunctions_TabAndRandomize()
    {
        var sl = Body(Transpile(StringFuncProgram).Output!);
        foreach (var expected in new[]
        {
            "BAS_PNUM(BAS_SLEN(S_A));", "BAS_PNUM(BAS_SINSTR(1, S_A, K_1));", "BAS_PNUM(BAS_SINSTR(V_I, S_A, K_2));",
            "BAS_PSTR(BAS_SHEXS(((V_I)*(100))));", "BAS_PSTR(BAS_SSTR(V_I, BAS_SCHR(32)));", "BAS_PRTAB(10);",
            "BAS_RANDOMIZE();", "BAS_RANDOMIZE(H_F1);", "BAS_RANDOMIZE(H_FK[",
        })
            Assert.Contains(expected, sl);
        // INSTR の n・HEX$・SPACE$ の引数は整数に入れる位置と同じに丸める。TAB も同じ
        var sl2 = Body(Transpile("10 X=1.5:PRINT INSTR(X,\"AB\",\"B\");HEX$(X);SPACE$(X);TAB(X)").Output!);
        Assert.Contains("BAS_SINSTR(BAS_FTOIR(F_X), K_", sl2);
        Assert.Contains("BAS_SHEXS(BAS_FTOIR(F_X))", sl2);
        Assert.Contains("BAS_SSTR(BAS_FTOIR(F_X), BAS_SCHR(32))", sl2);
        Assert.Contains("BAS_PRTAB(BAS_FTOIR(F_X));", sl2);
        // LEN の引数に数、TAB を PRINT の外に書くのはエラー
        Assert.True(HuTranspiler.Transpile("10 A=LEN(1)").Diagnostics.HasErrors);
        Assert.True(HuTranspiler.Transpile("10 A=TAB(1)").Diagnostics.HasErrors);
        Assert.True(HuTranspiler.Transpile("10 RANDOMIZE \"A\"").Diagnostics.HasErrors);
    }

    /// <summary>段階 G1: 各命令の呼び出し。SWAP は添字を A・B の順に一時変数へ入れ、大きさは整数 2・浮動小数 3・文字列 0</summary>
    [Fact]
    public void Slang_StageG1Calls()
    {
        var sl = Body(Transpile(G1Program).Output!);
        foreach (var expected in new[]
        {
            "BAS_TEMPO(144);", "BAS_TEMPO(BAS_FTOIR(F_X));", "BAS_PLAY(K_", "BAS_CGEN(1);", "BAS_CGEN(0);",
            "BAS_KEY(0, K_", "BAS_KEY(V_I, S_A);", "BAS_KREPEAT(0);", "BAS_KREPEAT(1);",
            "BAS_PSTR(BAS_SCHR(29));", "BAS_PSTR(BAS_SSCRN(0, 0, 2));", "BAS_PSTR(BAS_SSCRN(V_I, BAS_FTOIR(F_X), ((V_I)+(1))));",
            "BAS_SWAP(&F_X, &F_Y, 3);", "BAS_SWAP(S_A, S_B, 0);",
            "H_I1=BAS_AIDX(V_I, &H_DIM_F_C, &H_UB_F_C);\n  H_I2=BAS_AIDX(((V_I)+(1)), &H_DIM_F_C, &H_UB_F_C);\n  BAS_SWAP(&FA_C[H_I1], &FA_C[H_I2], 3);",
            "BAS_SWAP(&A_D[H_I1], &V_I, 2);", "BAS_SWAP(&F_X, &FA_C[H_I1], 3);",
            "BAS_PALET(3, 5);", "BAS_PALRST();", "BAS_PALET(V_I, BAS_FTOIR(F_X));", "BAS_PRW(2);", "BAS_PRW(0);", "BAS_PRW(V_I);",
        })
            Assert.Contains(expected, sl.Replace("\r\n", "\n"));
        // REPEAT ON/OFF はループに数えない (REPEAT〜UNTIL は 1 つだけ)
        Assert.Single(Regex.Matches(sl, @"^QL\d+:", RegexOptions.Multiline));
    }

    /// <summary>
    /// 段階 G2: 文字列の配列は 1 要素 256 バイトの SA_名前、隠し変数は H_DIM_S_名前・H_UB_S_名前。要素は
    /// (SA_名前+(添字<<8)) の番地で、文字列の変数と同じ呼び出しに渡す。SWAP は添字を A・B の順に一時変数へ入れる
    /// </summary>
    [Fact]
    public void Slang_StageG2Calls()
    {
        var sl = Transpile(G2Program).Output!.Replace("\r\n", "\n");
        foreach (var expected in new[]
        {
            "ARRAY BYTE SA_P[511];", "ARRAY BYTE SA_N[5375];", "ARRAY BYTE SA_Q[2815];", "ARRAY BYTE S_A[255];",
            "H_DIM_S_P=0;\n  H_UB_S_P=1;", "H_UB_S_N=20;", "BAS_DIM(&H_DIM_S_P, &H_UB_S_P, 1);",
            "BAS_SSET((SA_P+(BAS_AIDX(0, &H_DIM_S_P, &H_UB_S_P)<<8)), K_",
            "BAS_SSET((SA_P+(BAS_AIDX(((V_I)-(1)), &H_DIM_S_P, &H_UB_S_P)<<8)), BAS_SCAT((SA_P+(BAS_AIDX(((V_I)-(1)), &H_DIM_S_P, &H_UB_S_P)<<8)), S_A));",
            "BAS_PNUM(BAS_SLEN((SA_P+(BAS_AIDX(1, &H_DIM_S_P, &H_UB_S_P)<<8))));",
            "H_I1=BAS_AIDX(0, &H_DIM_S_P, &H_UB_S_P);\n  H_I2=BAS_AIDX(1, &H_DIM_S_P, &H_UB_S_P);\n  BAS_SWAP((SA_P+(H_I1<<8)), (SA_P+(H_I2<<8)), 0);",
            "H_I1=BAS_AIDX(1, &H_DIM_S_P, &H_UB_S_P);\n  BAS_SWAP(S_A, (SA_P+(H_I1<<8)), 0);",
            "BAS_SSET((SA_N+(BAS_AIDX(V_I, &H_DIM_S_N, &H_UB_S_N)<<8)), BAS_READS());",
            "BAS_SSET((SA_N+(BAS_AIDX(20, &H_DIM_S_N, &H_UB_S_N)<<8)), BAS_INFS());",
            "BAS_SSET((SA_Q+(BAS_AIDX(10, &H_DIM_S_Q, &H_UB_S_Q)<<8)), K_",
        })
            Assert.Contains(expected, sl);
    }

    /// <summary>配列の置き場は DIM の上限の分だけ (10 未満でも。DIM が複数なら最大)。初めの H_UB も同じ値、DIM なしは 10</summary>
    [Fact]
    public void Slang_ArrayStorageFollowsDim()
    {
        var sl = Transpile("10 DEFINT A:DIM P$(0),A(3):IF A(0) THEN DIM S$(1) ELSE DIM S$(5)\n20 Q$(1)=P$(0):A(2)=1").Output!
            .Replace("\r\n", "\n");
        foreach (var expected in new[]
        {
            "ARRAY BYTE SA_P[255];", "ARRAY A_A[3];", "ARRAY BYTE SA_S[1535];", "ARRAY BYTE SA_Q[2815];",
            "H_UB_S_P=0;", "H_UB_A=3;", "H_UB_S_S=5;", "H_UB_S_Q=10;",
            "BAS_DIM(&H_DIM_S_S, &H_UB_S_S, 1);", "BAS_DIM(&H_DIM_S_S, &H_UB_S_S, 5);",
        })
            Assert.Contains(expected, sl);
    }

    /// <summary>
    /// --string-len N: 文字列の変数と配列の要素は N+1 バイト (2 の累乗ならシフト、そうでなければ掛け算で要素の番地)。
    /// 代入・READ・INPUT は長さを調べる BAS_SSETL(先, 元, N)。SWAP と MID$ の文は今のまま
    /// </summary>
    [Theory]
    [InlineData(63, "<<6")]
    [InlineData(100, "*101")]
    [InlineData(1, "<<1")]
    [InlineData(254, "*255")]
    public void Slang_StringLen(int n, string offset)
    {
        var sl = Transpile(G2Program + "\n60 MID$(A$,1)=\"Z\"", new HuTranspileOptions(StringLen: n)).Output!.Replace("\r\n", "\n");
        int s = n + 1;
        foreach (var expected in new[]
        {
            $"ARRAY BYTE SA_P[{2 * s - 1}];", $"ARRAY BYTE SA_N[{21 * s - 1}];", $"ARRAY BYTE SA_Q[{11 * s - 1}];", $"ARRAY BYTE S_A[{n}];",
            $"BAS_SSETL((SA_P+(BAS_AIDX(0, &H_DIM_S_P, &H_UB_S_P){offset})), K_",
            $"BAS_SSETL((SA_N+(BAS_AIDX(V_I, &H_DIM_S_N, &H_UB_S_N){offset})), BAS_READS(), {n});",
            $"BAS_SSETL((SA_N+(BAS_AIDX(20, &H_DIM_S_N, &H_UB_S_N){offset})), BAS_INFS(), {n});",
            $"BAS_SWAP((SA_P+(H_I1{offset})), (SA_P+(H_I2{offset})), 0);",
            "BAS_SMIDSET(S_A, ",
        })
            Assert.Contains(expected, sl);
        Assert.DoesNotContain("BAS_SSET(", sl);
    }

    /// <summary>--string-len 255 (上限そのもの) は既定と 1 文字も同じ</summary>
    [Fact]
    public void Slang_StringLen255IsDefault()
    {
        Assert.Equal(Transpile(G2Program).Output, Transpile(G2Program, new HuTranspileOptions(StringLen: 255)).Output);
    }

    /// <summary>--string-len で文字列の配列の上限の添字 (16384 / (N+1) - 1) と、静的なデータの見積り (N+1 バイトで数える) が変わる</summary>
    [Fact]
    public void Analyzer_StringLenBoundsAndStaticBytes()
    {
        var opt = new HuTranspileOptions(StringLen: 63);
        Assert.NotNull(Transpile("10 DIM X$(255)", opt).Output);
        var r = HuTranspiler.Transpile("10 DIM X$(256)", opt);
        Assert.Null(r.Output);
        Assert.Contains(r.Diagnostics.Items, d => d.Code == "HB0305");
        Assert.Null(HuTranspiler.Transpile("10 DIM X$(64)").Output);   // 既定は 63 まで
        long Bytes(string p, HuTranspileOptions o) => Transpile(p, o).Program!.StaticDataBytes;
        Assert.Equal(11 * 64 + 4 + 64, Bytes("10 A$(1)=B$", opt) - Bytes("10 PRINT 1", opt));
        Assert.Equal(11 * 256 + 4 + 256, Bytes("10 A$(1)=B$", new()) - Bytes("10 PRINT 1", new()));
    }

    /// <summary>CLI の --string-len は 1〜255 の整数だけ (それ以外は終了コード 2)</summary>
    [Fact]
    public void Cli_StringLen()
    {
        var dir = Path.Combine(Path.GetTempPath(), $"hubasic2sl_{Guid.NewGuid():N}");
        Directory.CreateDirectory(dir);
        try
        {
            var bas = Path.Combine(dir, "T.bas");
            File.WriteAllText(bas, "10 A$=\"X\"\n");
            var main = typeof(SLANGCompiler.HuBasic.CLI.Program).GetMethod("Main",
                System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Static)!;
            int Run(params string[] a) => (int)main.Invoke(null, new object[] { a })!;
            Assert.Equal(0, Run(bas, "-o", Path.Combine(dir, "A.SL"), "--string-len", "31"));
            Assert.Contains("ARRAY BYTE S_A[31];", File.ReadAllText(Path.Combine(dir, "A.SL")));
            foreach (var bad in new[] { "0", "-1", "256", "X", "99999999999" })
                Assert.Equal(2, Run(bas, "-o", Path.Combine(dir, "B.SL"), "--string-len", bad));
            Assert.Equal(2, Run(bas, "-o", Path.Combine(dir, "B.SL"), "--string-len"));
        }
        finally
        {
            try { Directory.Delete(dir, true); } catch { }
        }
    }

    /// <summary>
    /// 段階 G3: グラフィックの LINE は BAS_GLINE(x1, y1, x2, y2, 色, 形)。形は bit 0 = 箱、bit 1 = PRESET、bit 2 = 色なし
    /// (色なしの色の引数は 0)。テキスト版の LINE は今までどおり BAS_TLINE
    /// </summary>
    [Fact]
    public void Slang_StageG3Calls()
    {
        var sl = Body(Transpile(G3Program).Output!);
        foreach (var expected in new[]
        {
            "BAS_GLINE(143, 0, 176, 8, 7, 1);", "BAS_GLINE(0, 0, 10, 10, 0, 4);", "BAS_GLINE(BAS_FTOIR(F_X), 1, 5, V_C, 0, 6);",
            "BAS_GLINE(1, 2, 3, 4, 0, 5);", "BAS_GLINE(1, 2, 3, 4, V_C, 1);", "BAS_GLINE(1, 2, 3, 4, 5, 3);",
            "BAS_TLINE(0, 0, 5, 5, K_",
        })
            Assert.Contains(expected, sl);
        Assert.Equal(2, Regex.Matches(sl, @"BAS_GLINE\(1, 2, 3, 4, 0, 5\);").Count); // ,B と ,,B は同じ
    }

    /// <summary>
    /// 段階 G4: BF は BAS_GLINE の形の bit 3 (bit 0 も立てる)。PAINT は BAS_PAINT(x, y, 色, 境界色…, 境界色の数)。
    /// 引数の数は文ごとに変わる
    /// </summary>
    [Fact]
    public void Slang_StageG4Calls()
    {
        var sl = Body(Transpile(G4Program).Output!);
        foreach (var expected in new[]
        {
            "BAS_GLINE(10, 10, 50, 30, 3, 9);", "BAS_GLINE(0, 0, 5, 5, 0, 13);", "BAS_GLINE(1, 2, 3, 4, V_C, 11);",
            "BAS_GLINE(1, 2, 3, 4, 0, 13);", "BAS_GLINE(10, 10, 100, 60, 4, 1);",
            "BAS_PAINT(50, 30, 2, 4, 1);", "BAS_PAINT(BAS_FTOIR(F_X), ((V_C)+(1)), 5, 0);",
            "BAS_PAINT(0, 0, 1, 1, 2, 3, 4, 5, 6, 7, 7);",
        })
            Assert.Contains(expected, sl);
        // 色の式の一時文字列は文の頭で解放する
        var paint = sl.Split('\n').First(l => l.Contains("BAS_PAINT(1, 1,"));
        Assert.Contains("BAS_SASC(", paint);
        Assert.EndsWith(", V_C, 1);", paint.Trim());
        Assert.Contains("BAS_STMP();", sl[..sl.IndexOf(paint, StringComparison.Ordinal)]);
    }

    /// <summary>同梱のサンプル (examples/hubasic) は、どれも error なしで変換できる</summary>
    [Fact]
    public void Examples_HubasicSamplesTranspile()
    {
        var dir = Path.Combine(RepoRoot(), "examples", "hubasic");
        var samples = Directory.GetFiles(dir, "*.BAS");
        Assert.NotEmpty(samples);
        foreach (var f in samples)
        {
            var r = HuTranspiler.Transpile(File.ReadAllText(f));
            Assert.False(r.Diagnostics.HasErrors, Path.GetFileName(f) + ": " +
                string.Join("; ", r.Diagnostics.Items.Select(d => d.Code + " " + d.Message)));
        }
    }

    /// <summary>
    /// examples/hubasic のファイル (README.md・MANIFEST.txt を除く) は、MANIFEST.txt の一覧とちょうど一致する
    /// (このリポジトリのために書いたものだけを置く約束。pre-commit も同じことを調べる)
    /// </summary>
    [Fact]
    public void Examples_HubasicManifestListsEveryFile()
    {
        var dir = Path.Combine(RepoRoot(), "examples", "hubasic");
        var listed = File.ReadAllLines(Path.Combine(dir, "MANIFEST.txt"))
            .Where(l => l.Trim().Length > 0 && !l.StartsWith('#'))
            .Select(l => l.Split('\t')[0]).OrderBy(x => x, StringComparer.Ordinal).ToList();
        Assert.All(File.ReadAllLines(Path.Combine(dir, "MANIFEST.txt")).Where(l => l.Trim().Length > 0 && !l.StartsWith('#')),
            l => Assert.True(l.Split('\t').Length >= 2 && l.Split('\t')[1].Trim().Length > 0, $"出どころがない: {l}"));
        // 変換した SLANG (*.SL) は .gitignore で除外しているので、一覧の対象にしない
        var files = Directory.GetFiles(dir).Select(f => Path.GetFileName(f))
            .Where(n => n is not ("README.md" or "MANIFEST.txt") && !n.EndsWith(".SL", StringComparison.OrdinalIgnoreCase))
            .OrderBy(x => x, StringComparer.Ordinal).ToList();
        Assert.Equal(listed, files);
    }

    /// <summary>ランタイムの関数の名前は重ならない (同じ名前は後のもので上書きされる。TAB を BAS_PTAB にすると PLAY の音程の表を壊す)</summary>
    [Fact]
    public void Runtime_FunctionNamesAreUnique()
    {
        var path = Path.Combine(RepoRoot(), "runtime", "libx1_basic.asm");
        var dups = RuntimeParser.Parse(File.ReadAllText(path), path).GroupBy(f => f.Name, StringComparer.OrdinalIgnoreCase)
            .Where(g => g.Count() > 1).Select(g => g.Key).ToList();
        Assert.True(dups.Count == 0, "重なった名前: " + string.Join(", ", dups));
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

    /// <summary>
    /// VSYNC 文 (HuBASIC にない命令) は --wait によらず BAS_VSYNCS。引数なしは 1、数は今の規則で整数にする。
    /// '#VSYNC のコメントは今までどおり frame のときだけ BAS_VSYNC、BAS_WLINE は line のときだけ
    /// </summary>
    [Theory]
    [InlineData(HuWaitMode.None)]
    [InlineData(HuWaitMode.Line)]
    [InlineData(HuWaitMode.Frame)]
    public void Slang_VsyncStatement(HuWaitMode wait)
    {
        var r = Transpile("10 VSYNC:VSYNC 2:A%=3:VSYNC A%*2:VSYNC 0:VSYNC -1:VSYNC .5:VSYNC 1.5:X=2.5:VSYNC X:'#VSYNC 5",
            new HuTranspileOptions(Wait: wait, WaitArg: 7));
        var sl = Body(r.Output!);
        foreach (var expected in new[]
        {
            "BAS_VSYNCS(1);\n  BAS_VSYNCS(2);", "BAS_VSYNCS(((V_A)*(2)));", "BAS_VSYNCS(0);", "BAS_VSYNCS((0-(1)));",
            "BAS_VSYNCS(1);\n  BAS_VSYNCS(2);\n  F_X=", "BAS_VSYNCS(BAS_FTOIR(F_X));",
        })
            Assert.Contains(expected, sl.Replace("\r\n", "\n"));
        Assert.Equal(wait == HuWaitMode.Frame, sl.Contains("BAS_VSYNC(5);"));
        Assert.Equal(wait == HuWaitMode.Line, sl.Contains("BAS_WLINE(7);"));
        // 32768 は -32768 (待たない)、-65535 は回り込んで 1、-65536 は実行時の整数化 (Overflow) に任せる
        var edge = Body(Transpile("10 VSYNC 32768:VSYNC -65535:VSYNC -65536").Output!).Replace("\r\n", "\n");
        Assert.Contains("BAS_VSYNCS((0-32768));\n  BAS_VSYNCS(1);\n  BAS_VSYNCS(BAS_FTOIR(", edge);
        // 引数なしの '#VSYNC は今までどおり 1 (frame:N の N は使わない)
        var noArg = Body(Transpile("10 '#VSYNC", new HuTranspileOptions(Wait: HuWaitMode.Frame, WaitArg: 5)).Output!);
        Assert.Contains("BAS_VSYNC(1);", noArg);
    }

    /// <summary>VSYNC は名前の頭でだけ予約語 (AVSYNC は今までどおり名前、VSYNCX=1 は VSYNC 文)</summary>
    [Fact]
    public void Slang_VsyncKeywordOnlyAtNameStart()
    {
        var a = Body(Transpile("10 AVSYNC=1").Output!);
        Assert.DoesNotContain("BAS_VSYNCS", a);
        var b = Body(Transpile("10 VSYNCX=1").Output!);
        Assert.Contains("BAS_VSYNCS(", b);
    }

    /// <summary>行の頭・GOSUB と ON GOSUB の戻り先・LABEL の所で行番号を書くプログラム</summary>
    private const string LineNumberProgram = """
        10 DEFINT A-Z:GOSUB 100:ON A GOSUB 100,110:'#VSYNC 2
        20 GOTO "S"
        30 LABEL "S":END
        100 A=A+1:RETURN
        110 RETURN
        """;

    /// <summary>--no-line-numbers: 実行中の行番号を書かず (隠し変数・BAS_LNINIT もなし)、ラベルとほかのオプションの出力は残る</summary>
    [Theory]
    [InlineData(HuWaitMode.None, false)]
    [InlineData(HuWaitMode.Line, false)]
    [InlineData(HuWaitMode.Frame, true)]
    public void Lowering_NoLineNumbers(HuWaitMode wait, bool lineComments)
    {
        var opt = new HuTranspileOptions(Wait: wait, WaitArg: 7, LineComments: lineComments);
        var with = Transpile(LineNumberProgram, opt);
        var r = Transpile(LineNumberProgram, opt with { LineNumbers = false });
        var flat = r.Flat!;
        Assert.DoesNotContain(flat.Ops, o => o is FAssignHidden { Hidden: HuLowerer.CurrentLine });
        Assert.DoesNotContain(flat.Ops.OfType<FCall>(), c => c.Function == "BAS_LNINIT");
        Assert.DoesNotContain(HuLowerer.CurrentLine, flat.HiddenVars);
        Assert.DoesNotContain("H_LINE", r.Output!);
        Assert.DoesNotContain("BAS_LNINIT", r.Output!);
        // 既定では、行の頭 5 つ・GOSUB の戻り先・ON GOSUB の戻り先・LABEL の 8 か所で書く
        Assert.Equal(8, with.Flat!.Ops.Count(o => o is FAssignHidden { Hidden: HuLowerer.CurrentLine }));
        // 消えるのは行番号の分 (書く行・BAS_LNINIT・隠し変数の宣言) だけで、ほかは同じ
        var expected = Regex.Replace(with.Output!.Replace("\r\n", "\n"), @"^\s*(H_LINE=\d+|BAS_LNINIT\(&H_LINE\));\n", "",
            RegexOptions.Multiline).Replace("H_T, H_LINE", "H_T");
        Assert.Equal(expected, r.Output!.Replace("\r\n", "\n"));
        foreach (var label in new[] { "L10:", "L100:", "L110:", "LB0:", "R0:", "R1:" })
            Assert.Contains(label, r.Output!);
        Assert.Equal(wait == HuWaitMode.Line, r.Output!.Contains("BAS_WLINE(7);"));
        Assert.Equal(wait == HuWaitMode.Frame, r.Output!.Contains("BAS_VSYNC(2);"));
        Assert.Equal(lineComments, r.Output!.Contains("// BAS 30: 30 LABEL"));
    }

    /// <summary>CLI の --no-line-numbers がオプションに渡る</summary>
    [Fact]
    public void Cli_NoLineNumbers()
    {
        var dir = Path.Combine(Path.GetTempPath(), $"hubasic2sl_{Guid.NewGuid():N}");
        Directory.CreateDirectory(dir);
        try
        {
            var bas = Path.Combine(dir, "T.bas");
            File.WriteAllText(bas, "10 PRINT 1\n20 GOTO 10\n");
            var main = typeof(SLANGCompiler.HuBasic.CLI.Program).GetMethod("Main",
                System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Static)!;
            Assert.Equal(0, main.Invoke(null, new object[] { new[] { bas, "-o", Path.Combine(dir, "A.SL") } }));
            Assert.Equal(0, main.Invoke(null, new object[] { new[] { bas, "-o", Path.Combine(dir, "B.SL"), "--no-line-numbers" } }));
            Assert.Contains("H_LINE=10;", File.ReadAllText(Path.Combine(dir, "A.SL")));
            Assert.DoesNotContain("H_LINE", File.ReadAllText(Path.Combine(dir, "B.SL")));
        }
        finally
        {
            try { Directory.Delete(dir, true); } catch { }
        }
    }

    private static int RunCli(params string[] args)
    {
        var main = typeof(SLANGCompiler.HuBasic.CLI.Program).GetMethod("Main",
            System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Static)!;
        return (int)main.Invoke(null, new object[] { args })!;
    }

    private static string NewTempDir()
    {
        var dir = Path.Combine(Path.GetTempPath(), $"hubasic2sl_{Guid.NewGuid():N}");
        Directory.CreateDirectory(dir);
        return dir;
    }

    /// <summary>--emit と一緒に書くオプションの誤りは、変換も組み立てもせずに終了コード 2</summary>
    [Fact]
    public void Cli_EmitOptionErrors()
    {
        var dir = NewTempDir();
        try
        {
            var bas = Path.Combine(dir, "T.bas");
            File.WriteAllText(bas, "10 PRINT 1\n");
            var none = Path.Combine(dir, "none");
            foreach (var bad in new[]
            {
                new[] { bas, "--emit" }, new[] { bas, "--emit", "bin" },
                new[] { bas, "--disk-main-name", "X" }, new[] { bas, "--tape-name", "X" }, new[] { bas, "--wav" },
                new[] { bas, "--slangbuild", none },
                new[] { bas, "--emit", "disk", "--tape-name", "X" }, new[] { bas, "--emit", "disk", "--wav" },
                new[] { bas, "--emit", "tape", "--disk-main-name", "X" },
                new[] { bas, "--emit", "disk", "--disk-main-name" }, new[] { bas, "--emit", "tape", "--tape-name" },
                new[] { bas, "--emit", "disk", "--slangbuild" },
                new[] { bas, "--emit", "disk", "--env", "x1native" }, new[] { bas, "--emit", "tape", "--env", "x1native_slfs" },
                new[] { bas, "--emit", "disk", "-o", Path.Combine(dir, "A.tap") },
                new[] { bas, "--emit", "tape", "-o", Path.Combine(dir, "A.D88") },
                new[] { "--emit", "disk", "--check-bin", Path.Combine(dir, "A.bin") },
            })
                Assert.Equal(2, RunCli(bad));
            Assert.Empty(Directory.GetFiles(dir, "*.SL"));
        }
        finally
        {
            try { Directory.Delete(dir, true); } catch { }
        }
    }

    /// <summary>
    /// slangbuild は --slangbuild、hubasic2sl と同じ所、その下の bin/、開発の publish (今の OS の RID、Release と Debug の新しい方) の順に探す。
    /// ない所を指した --slangbuild は見つからない (次へ行かない)
    /// </summary>
    [Fact]
    public void Cli_SlangbuildResolution()
    {
        var dir = NewTempDir();
        try
        {
            var name = OperatingSystem.IsWindows() ? "slangbuild.exe" : "slangbuild";
            // リポジトリの形を真似る: <dir>/src/SLANGCompiler.HuBasic/bin/Release/net8.0 が hubasic2sl の置き場
            var baseDir = Path.Combine(dir, "src", "SLANGCompiler.HuBasic", "bin", "Release", "net8.0");
            Directory.CreateDirectory(baseDir);
            var runner = new SLANGCompiler.HuBasic.CLI.SlangbuildRunner(baseDir);
            var os = OperatingSystem.IsWindows() ? "win" : OperatingSystem.IsMacOS() ? "osx" : "linux";
            var arch = System.Runtime.InteropServices.RuntimeInformation.OSArchitecture.ToString().ToLowerInvariant();
            string Publish(string config)
            {
                var d = Path.Combine(dir, "src", "SLANGCompiler.Build", "bin", config, "net8.0", $"{os}-{arch}", "publish");
                Directory.CreateDirectory(d);
                File.WriteAllText(Path.Combine(d, name), "");
                return Path.Combine(d, name);
            }
            var release = Publish("Release");
            var debug = Publish("Debug");
            File.SetLastWriteTimeUtc(release, DateTime.UtcNow.AddHours(-1));
            Assert.Equal(debug, runner.Resolve(null, out _)!.FileName);
            File.SetLastWriteTimeUtc(release, DateTime.UtcNow.AddHours(1));
            Assert.Equal(release, runner.Resolve(null, out _)!.FileName);

            Directory.CreateDirectory(Path.Combine(baseDir, "bin"));
            File.WriteAllText(Path.Combine(baseDir, "bin", name), "");
            Assert.Equal(Path.Combine(baseDir, "bin", name), runner.Resolve(null, out _)!.FileName);
            File.WriteAllText(Path.Combine(baseDir, name), "");
            Assert.Equal(Path.Combine(baseDir, name), runner.Resolve(null, out _)!.FileName);
            var other = Path.Combine(dir, "other");
            File.WriteAllText(other, "");
            Assert.Equal(other, runner.Resolve(other, out _)!.FileName);
            Assert.Null(runner.Resolve(Path.Combine(dir, "none"), out var tried));
            Assert.Contains("none", tried);
        }
        finally
        {
            try { Directory.Delete(dir, true); } catch { }
        }
    }

    /// <summary>
    /// --emit の段: slangbuild の代わりの sh (Windows では飛ばす) で、渡す引数 (空白・日本語・引用符もそのまま 1 つ) と、
    /// 変換・組み立て・大きさの検査のどこで止まっても終了コード 1 でイメージを残さないこと、変換で止まったら slangbuild を呼ばないことを見る
    /// </summary>
    [Fact]
    public void Cli_EmitStages()
    {
        if (OperatingSystem.IsWindows()) return;
        var root = NewTempDir();
        var dir = Path.Combine(root, "作業 1");
        Directory.CreateDirectory(dir);
        try
        {
            var bas = Path.Combine(dir, "ゲーム 1.bas");
            File.WriteAllText(bas, "10 PRINT 1\n");
            var argsFile = Path.Combine(root, "args.txt");
            string Fake(string name, string body, bool exec = true)
            {
                var path = Path.Combine(dir, name);
                File.WriteAllText(path, "#!/bin/sh\nprintf '%s\\n' \"$@\" > '" + argsFile + "'\nfor a; do p=$a; done\n" + body + "\n");
                File.SetUnixFileMode(path, exec ? UnixFileMode.UserRead | UnixFileMode.UserWrite | UnixFileMode.UserExecute
                                                : UnixFileMode.UserRead | UnixFileMode.UserWrite);
                return path;
            }
            const string image = "case \"$2\" in disk) : > \"$p.d88\" ;; tape) : > \"$p.tap\" ;; esac; " +
                                 "case \" $* \" in *\" --wav \"*) : > \"$p.wav\" ;; esac";
            string Built(string workEnd) => $"printf x > \"$p.bin\"; echo '  {workEnd}  __WORKEND__ EQU $' > \"$p.LST\"; " + image;
            var ok = Fake("ok 1.sh", Built("2000"));
            var prefix = Path.Combine(dir, "ゲーム 1");
            string[] Args() => File.ReadAllLines(argsFile);

            Assert.Equal(0, RunCli(bas, "--emit", "disk", "--slangbuild", ok, "--disk-main-name", "A \"B\"", "--wait", "line:480"));
            Assert.Equal(new[] { "--emit", "disk", "-E", "x1native_slfs", "--disk-main-name", "A \"B\"", prefix + ".SL", "-o", prefix }, Args());
            Assert.True(File.Exists(prefix + ".d88"));
            Assert.Contains("BAS_WLINE(480)", File.ReadAllText(prefix + ".SL"));

            var outPrefix = Path.Combine(dir, "out");
            Assert.Equal(0, RunCli(bas, "--emit", "tape", "--wav", "--tape-name", "T", "-o", outPrefix + ".tap", "--slangbuild", ok));
            Assert.Equal(new[] { "--emit", "tape", "-E", "x1native", "--tape-name", "T", "--wav", outPrefix + ".SL", "-o", outPrefix }, Args());
            Assert.True(File.Exists(outPrefix + ".tap") && File.Exists(outPrefix + ".SL"));

            // 作業領域の終わりが予約領域の頭 $FFE0 ちょうどは通る
            Assert.Equal(0, RunCli(bas, "--emit", "disk", "--slangbuild", Fake("edge.sh", Built("FFE0"))));

            // 止まったら 1。前の回のイメージも、できかけのイメージも残さない
            foreach (var tool in new[]
            {
                Fake("fail.sh", "exit 3"),
                Fake("tapefail.sh", ": > \"$p.d88\"; exit 1"),
                Fake("noimage.sh", "printf x > \"$p.bin\"; echo '  2000  __WORKEND__ EQU $' > \"$p.LST\""),
                Fake("noexec.sh", Built("2000"), exec: false),
                Fake("big.sh", Built("FFE1")),
                Fake("nolst.sh", "printf x > \"$p.bin\"; rm -f \"$p.LST\"; " + image),
                Fake("noworkend.sh", "printf x > \"$p.bin\"; echo '' > \"$p.LST\"; " + image),
                Path.Combine(dir, "none"),
            })
            {
                File.WriteAllText(prefix + ".d88", "old");
                Assert.Equal(1, RunCli(bas, "--emit", "disk", "--slangbuild", tool));
                Assert.False(File.Exists(prefix + ".d88"), tool);
            }
            File.WriteAllText(outPrefix + ".wav", "old");
            Assert.Equal(1, RunCli(bas, "--emit", "tape", "--wav", "-o", outPrefix, "--slangbuild", Fake("wavfail.sh", ": > \"$p.tap\"; exit 1")));
            Assert.False(File.Exists(outPrefix + ".tap") || File.Exists(outPrefix + ".wav"));

            // 変換で止まったら slangbuild を呼ばず、前の回のイメージも消えている
            File.Delete(argsFile);
            File.WriteAllText(prefix + ".d88", "old");
            File.WriteAllText(bas, "10 CIRCLE\n");
            Assert.Equal(1, RunCli(bas, "--emit", "disk", "--slangbuild", ok));
            Assert.False(File.Exists(argsFile));
            Assert.False(File.Exists(prefix + ".d88"));

            // 入力と作るファイルが同じなら 2 で、入力は消えない
            foreach (var (name, emit) in new[] { ("G.tap", "tape"), ("G.d88", "disk"), ("G.bin", "disk"), ("G.SL", "disk"), ("G.asm", "disk"), ("G.sym", "tape"), ("G.inc", "disk") })
            {
                var input = Path.Combine(dir, name);
                File.WriteAllText(input, "10 PRINT 1\n");
                Assert.Equal(2, RunCli(input, "--emit", emit, "--slangbuild", ok));
                Assert.True(File.Exists(input), name);
            }
        }
        finally
        {
            try { Directory.Delete(root, true); } catch { }
        }
    }

    // ------------------------------------------------------------ SLANG 生成

    [Fact]
    public void Slang_NamesAreSeparatedAndArraysChecked()
    {
        // DEFINT の下の A と A% は同じ変数 V_A (HuBASIC と同じ。以前は V_A と V_A_I に分けていた)
        var sl = Transpile("10 DEFINT A-Z:A=1:A(2)=A:A%=3:A$=\"Q\"").Output!;
        Assert.Contains("VAR V_A, H_T", sl);
        Assert.Contains("V_A=3;", sl);
        Assert.Contains("ARRAY A_A[10];", sl);
        Assert.Contains("ARRAY BYTE S_A[255];", sl);
        Assert.Contains("A_A[BAS_AIDX(2, &H_DIM_A, &H_UB_A)]=V_A;", sl);
        Assert.Contains("BAS_SSET(S_A, K_0);", sl);
    }

    [Fact]
    public void Slang_FloatVariablesAndArrays()
    {
        var sl = Transpile("10 A=1.5:B=A*2:DIM C(3):C(1)=A/B:I%=A:C(2)=I%:PRINT A;C(1)").Output!;
        Assert.Contains("VAR V_I, H_T, H_LINE, H_DIM_F_C, H_UB_F_C;", sl);
        Assert.Contains("VAR FLOAT F_A, FLOAT F_B;", sl);
        Assert.Contains("ARRAY FLOAT FA_C[3];", sl);
        Assert.Contains("F_A=H_FK[0];", sl);
        Assert.Contains("F_B=(F_A*H_FK[1]);", sl);
        // 浮動小数の割り算は変数の番地をそのまま渡す。代入先の添字は値より先に調べる (HuBASIC と同じ)
        Assert.Contains("H_I1=BAS_AIDX(1, &H_DIM_F_C, &H_UB_F_C);\n  FA_C[H_I1]=BAS_FDIV(&F_A, &F_B);", sl);
        Assert.Contains("V_I=BAS_FTOIR(F_A);", sl);
        // 整数の値を浮動小数にする前置きの代入が出るときも、添字の検査を先に出す
        Assert.Contains("H_I1=BAS_AIDX(2, &H_DIM_F_C, &H_UB_F_C);\n  H_F1=V_I;\n  FA_C[H_I1]=H_F1;", sl);
        Assert.Contains("BAS_PFLT(F_A);", sl);
        Assert.Contains("BAS_PFLT(FA_C[BAS_AIDX(1, &H_DIM_F_C, &H_UB_F_C)]);", sl);
    }

    /// <summary>
    /// 添字の検査を先に出すときも、添字の式は 1 回だけ作る (添字の前置きの代入より後ろで読む。R1-F1: 添字を作り直すと、
    /// 前置きの代入より前に一時変数を読んでいた)
    /// </summary>
    [Fact]
    public void Slang_HoistedArrayIndexKeepsItsPrerequisites()
    {
        var sl = Transpile("10 I%=2:J=3:A(I%+J)=I%").Output!;
        Assert.Contains("  H_F1=V_I;\n  H_I1=BAS_AIDX(BAS_FTOIR((H_F1+F_J)), &H_DIM_F_A, &H_UB_F_A);\n  H_F2=V_I;\n  FA_A[H_I1]=H_F2;", sl);
        // 整数の配列に浮動小数の変数を入れるときも、整数にする (Overflow になりうる) より先に添字を調べる (R2-F1)
        var sl3 = Transpile("10 X=70000:DIM A%(1):A%(2)=X").Output!;
        Assert.Contains("H_I1=BAS_AIDX(2, &H_DIM_A, &H_UB_A);\n  A_A[H_I1]=BAS_FTOIR(F_X);", sl3);
        // 副作用のある添字 (キーの入力) も 1 回だけ評価する
        var sl2 = Transpile("10 A(ASC(INKEY$))=I%").Output!;
        Assert.Single(Regex.Matches(sl2, @"BAS_SINKEY\(\)"));
    }

    /// <summary>
    /// 浮動小数の FOR (段階 F2b): 上限と増分は番地で BAS_FFORPUSH に渡す (定数は表の要素、式は一時変数)。浮動小数の FOR が
    /// あるプログラムの NEXT は、整数の FOR の NEXT も BAS_FFORNEXT にする
    /// </summary>
    [Fact]
    public void Slang_FloatForLoops()
    {
        var sl = Transpile("10 FOR X=0 TO 1 STEP .25:FOR I%=1 TO N%*2:NEXT I%:NEXT\n20 FOR Y=1 TO X+N%:NEXT Y").Output!;
        Assert.Contains("F_X=H_FK[0];\n  H_T=BAS_FFORPUSH(&F_X, (H_FK+3), (H_FK+6), 0);", sl);
        Assert.Contains("H_T=BAS_FORPUSH(&V_I, ((V_N)*(2)), 1, 1);", sl);
        Assert.Contains("H_T=BAS_FFORNEXT(&V_I);", sl);
        Assert.Contains("H_T=BAS_FFORNEXT(0);", sl);
        Assert.Contains("H_F1=V_N;\n  H_F2=(F_X+H_F1);\n  H_T=BAS_FFORPUSH(&F_Y, &H_F2, (H_FK+3), 2);", sl);
        Assert.Contains("H_T=BAS_FFORNEXT(&F_Y);", sl);
        Assert.DoesNotContain("BAS_FORNEXT(", sl);
        // 整数だけのプログラムの NEXT は、いままでどおり BAS_FORNEXT
        var si = Transpile("10 DEFINT A-Z:FOR I=1 TO 2:NEXT").Output!;
        Assert.Contains("H_T=BAS_FORNEXT(0);", si);
        Assert.DoesNotContain("BAS_FF", si);
    }

    /// <summary>
    /// 段階 F2c: VAL は浮動小数 (BAS_SVALF)、数の READ は BAS_READF、STR$ は浮動小数にしてから BAS_SFSTR、
    /// PRINT USING の浮動小数は BAS_PUSINGF (整数はいままでどおり BAS_PUSING)
    /// </summary>
    [Fact]
    public void Slang_StageF2cFunctions()
    {
        var sl = Transpile("10 A=VAL(B$):C%=VAL(\"1.6\"):READ D,E%,F(1):S$=STR$(A)+STR$(C%)\n" +
                           "20 PRINT USING \"##.##\";A;C%;40000\n30 DATA 1,2,3").Output!;
        Assert.Contains("F_A=BAS_SVALF(S_B);", sl);
        Assert.Contains("V_C=BAS_FTOIR(BAS_SVALF(K_0));", sl);
        Assert.Contains("F_D=BAS_READF();", sl);
        Assert.Contains("V_E=BAS_FTOIR(BAS_READF());", sl);
        // 配列の要素への READ は、添字を先に調べる (本物: DIM A(5):READ A(9) は DATA がなくても Subscript out of range)
        Assert.Contains("H_I1=BAS_AIDX(1, &H_DIM_F_F, &H_UB_F_F);\n  FA_F[H_I1]=BAS_READF();", sl);
        // 左の STR$ を先に評価する (右の引数を浮動小数にする前置きの代入より前に出す)
        Assert.Contains("H_I1=BAS_SFSTR(F_A);\n  H_F1=V_C;\n  BAS_SSET(S_S, BAS_SCAT(H_I1, BAS_SFSTR(H_F1)));", sl);
        Assert.Contains("BAS_PUSINGF(F_A);", sl);
        Assert.Contains("BAS_PUSING(V_C);", sl);
        Assert.Contains("BAS_PUSINGF(H_FK[", sl);   // 32768 以上の 10 進の数は浮動小数 (以前は HB0407)
    }

    [Fact]
    public void Slang_SingleDoubleIntAndStrWithSameNameAreSeparate()
    {
        var sl = Transpile("10 A=1:A!=2:A#=3:A%=4:A$=\"X\":DIM A(2),A#(2),A%(2)").Output!;
        Assert.Contains("VAR V_A, H_T, H_LINE, H_DIM_F_A, H_UB_F_A, H_DIM_D_A, H_UB_D_A, H_DIM_A;", sl);
        Assert.Contains("VAR FLOAT F_A, FLOAT D_A;", sl);
        Assert.Contains("ARRAY FLOAT FA_A[2];", sl);
        Assert.Contains("ARRAY FLOAT DA_A[2];", sl);
        Assert.Contains("ARRAY A_A[2];", sl);
        Assert.Contains("ARRAY BYTE S_A[255];", sl);
        Assert.Contains("F_A=H_FK[1];", sl);   // A と A! は同じ変数
    }

    [Fact]
    public void Slang_IntArrayAssignmentKeepsItsShape()
    {
        // 整数だけの代入は、添字の検査を先に出す形にしない (生成するコードを変えない)。INPUT の項目も同じ
        var sl = Transpile("10 DEFINT A-Z:A(I)=B\\C:INPUT A(J)").Output!;
        Assert.Contains("A_A[BAS_AIDX(V_I, &H_DIM_A, &H_UB_A)]=BAS_IDIV(V_B, V_C);", sl);
        Assert.Contains("A_A[BAS_AIDX(V_J, &H_DIM_A, &H_UB_A)]=BAS_FTOIR(BAS_INFN());", sl);
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
        Assert.Contains("V_A=BAS_FTOIR(BAS_READF());", sl);   // 数の READ は VAL と同じ読み方で浮動小数にして丸める
        Assert.Contains("BAS_RESTORE(20);", sl);
    }

    /// <summary>生成した SLANG が既存のコンパイラ (字句 → 構文 → 意味 → IR → コード生成) をエラーなく通る</summary>
    [Fact]
    public void Slang_GeneratedCodeCompilesWithSlangc()
    {
        foreach (var program in new[] { Stage1Program, FloatVarProgram })
            AssertCompilesWithSlangc(Transpile(program).Output!);
    }

    private static void AssertCompilesWithSlangc(string sl)
    {
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
        var sl = Transpile(Stage1Program).Output! + Transpile(FullProgram).Output! + Transpile(FloatProgram).Output!
                 + Transpile(StringScreenProgram).Output! + Transpile(InputSoundProgram).Output!
                 + Transpile(FloatVarProgram).Output! + Transpile(G5Program).Output! + Transpile(MirrorProgram).Output!
                 + Transpile(ConsoleProgram).Output!;
        var called = Regex.Matches(sl, @"\b(BAS_[A-Z0-9_]+)\(").Select(m => m.Groups[1].Value).Distinct().ToList();
        Assert.NotEmpty(called);
        foreach (var name in called)
        {
            Assert.True(functions.ContainsKey(name), $"{name} が libx1_basic.asm にない");
            var f = functions[name];
            Assert.Equal(RuntimeResidency.Shared, f.Residency);
            // @calls に書いた依存がすべて (どこかのランタイムに) 存在するかは組み立ての通しで確かめる
        }
        Assert.True(functions["BAS_FORPUSH"].CalleeCleanup, "4 引数の BAS_FORPUSH は callee cleanup");
        Assert.True(functions["BAS_FFORPUSH"].CalleeCleanup, "4 引数の BAS_FFORPUSH は callee cleanup");
        Assert.True(functions["BAS_TLINE"].CalleeCleanup, "6 引数の BAS_TLINE は callee cleanup");
        Assert.True(functions["BAS_GLINE"].CalleeCleanup, "6 引数の BAS_GLINE は callee cleanup");
        // BAS_PAINT は引数の数が文ごとに 4〜11 で変わる (最後の数を見て自分で取り除く)。待ち行列は自分の作業領域に置く
        Assert.True(functions["BAS_PAINT"].CalleeCleanup, "可変個の引数の BAS_PAINT は callee cleanup");
        // 段階 G5b: 引数の数と、TIME の 3 つの関数が同じ作業領域を使うこと
        foreach (var (name, n) in new[] { ("BAS_BYTE", 1), ("BAS_CALL", 1), ("BAS_TIMESET", 1), ("BAS_TIMEH", 0), ("BAS_TIMEL", 0) })
            Assert.Equal(n, functions[name].ParamCount);
        // MIRROR$ と HEXCHR$ は文字列を 1 つ受け取る
        Assert.Equal(1, functions["BAS_SMIRROR"].ParamCount);
        Assert.Equal(1, functions["BAS_SMIRRORV"].ParamCount);
        Assert.Contains("BAS_SMIRRORV", functions["BAS_SMIRROR"].Dependencies);
        Assert.Equal(1, functions["BAS_SHEX"].ParamCount);
        // CONSOLE: 引数なし・2 つは HL/DE、4 つはスタックで関数が後始末する
        Assert.Equal(0, functions["BAS_CONSOLE0"].ParamCount);
        Assert.Equal(2, functions["BAS_CONSOLEY"].ParamCount);
        Assert.True(functions["BAS_CONSOLE"].CalleeCleanup, "4 引数の BAS_CONSOLE は callee cleanup");
        Assert.Contains("BAS_CONSOLE", functions["BAS_CONSOLEY"].Dependencies);
        Assert.Contains(("BAS_WIN", 4), functions["BAS_WORK"].Works!);
        Assert.Contains(("BAS_TBASE", 3), functions["BAS_TIMEH"].Works!);
        Assert.Contains("BAS_TIMEH", functions["BAS_TIMEL"].Dependencies);
        Assert.Contains("BAS_TIMEH", functions["BAS_TIMESET"].Dependencies);
        Assert.Contains("PAINT1", functions["BAS_PAINT"].Dependencies);
        Assert.Contains(("BAS_PNTBUF", 1056), functions["BAS_PAINT"].Works!);
        Assert.True(functions["BAS_SMIDSET"].CalleeCleanup, "4 引数の BAS_SMIDSET は callee cleanup");
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

    /// <summary>
    /// ランタイムの関数が CALL・JP で呼ぶほかの関数は、すべて @calls に書いてある (組み込みは @calls だけをたどるので、
    /// 書き漏らすと、その関数だけを使うプログラムで名前が解決できない。F2b R1-F1)
    /// </summary>
    [Fact]
    public void Runtime_CalledFunctionsAreDeclared()
    {
        var path = Path.Combine(RepoRoot(), "runtime", "libx1_basic.asm");
        var functions = RuntimeParser.Parse(File.ReadAllText(path), path).ToList();
        var names = functions.Select(f => f.Name).ToHashSet(StringComparer.OrdinalIgnoreCase);
        var root = RepoRoot();
        foreach (var lib in EnvironmentLoader.Load(Path.Combine(root, "runtime", "env", "x1native.env")).Libraries)
        {
            var lp = Path.Combine(root, "runtime", Path.GetFileName(lib));
            if (File.Exists(lp))
                foreach (var f in RuntimeParser.Parse(File.ReadAllText(lp), lp)) names.Add(f.Name);
        }
        var missing = new List<string>();
        foreach (var f in functions)
        {
            var deps = f.Dependencies.ToHashSet(StringComparer.OrdinalIgnoreCase);
            foreach (var line in f.Code.Split('\n'))
            {
                var m = Regex.Match(line.Split(';')[0], @"^\s*(?:CALL|JP)\s+(?:(?:N?Z|N?C|P|M|PE|PO)\s*,\s*)?([A-Za-z_][A-Za-z0-9_]*)\s*$", RegexOptions.IgnoreCase);
                if (!m.Success) continue;
                var target = m.Groups[1].Value;
                if (names.Contains(target) && !string.Equals(target, f.Name, StringComparison.OrdinalIgnoreCase) && !deps.Contains(target))
                    missing.Add($"{f.Name} → {target}");
            }
        }
        Assert.True(missing.Count == 0, "@calls に書いていない呼び出し: " + string.Join(", ", missing));
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
        // hubasic2sl は DATA がなければ BAS_DATAINIT を呼ばないが、RESTORE は受け付ける
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

    /// <summary>組の印: ランタイムの BAS_ABI_n はちょうど 1 つで、番号は SlangBackend.BasAbi と同じ。出力は MAIN の最初にそれを呼ぶ</summary>
    [Fact]
    public void Runtime_BasAbiMarkerMatchesBackend()
    {
        var path = Path.Combine(RepoRoot(), "runtime", "libx1_basic.asm");
        var markers = RuntimeParser.Parse(File.ReadAllText(path), path).Where(f => Regex.IsMatch(f.Name, @"^BAS_ABI_\d+$")).ToList();
        var marker = Assert.Single(markers);
        Assert.Equal($"BAS_ABI_{SLANGCompiler.HuBasic.Emit.Slang.SlangBackend.BasAbi}", marker.Name);
        Assert.Equal(RuntimeResidency.Shared, marker.Residency);
        var sl = Transpile(Stage1Program).Output!.Replace("\r\n", "\n");
        Assert.Contains($"MAIN()\nBEGIN\n  BAS_ABI_{SLANGCompiler.HuBasic.Emit.Slang.SlangBackend.BasAbi}();\n  BAS_INIT();\n", sl);
    }

    /// <summary>組の印の番号が違うと、アセンブルで未定義のラベルになって止まる (slangc は知らない関数をそのまま CALL にする)</summary>
    [SkippableFact]
    public void Build_BasAbiMismatchStopsAtAssembly()
    {
        var root = RepoRoot();
        var asmPath = Path.Combine(root, "tools", OperatingSystem.IsWindows() ? "AILZ80ASM.exe" : "AILZ80ASM");
        Skip.IfNot(File.Exists(asmPath), "AILZ80ASM がない (make setup-tools で取得)");
        var dir = Path.Combine(Path.GetTempPath(), $"hubasic2sl_{Guid.NewGuid():N}");
        Directory.CreateDirectory(dir);
        try
        {
            var mark = $"BAS_ABI_{SLANGCompiler.HuBasic.Emit.Slang.SlangBackend.BasAbi}();";
            var text = Transpile(Stage1Program).Output!;
            Assert.Contains(mark, text);
            var sl = Path.Combine(dir, "ABI.SL");
            File.WriteAllText(sl, text.Replace(mark, "BAS_ABI_999();"));
            var (code, output) = RunSlangbuildRaw(root, dir, sl, asmPath, Path.Combine(dir, "ABI"));
            Assert.NotEqual(0, code);
            Assert.Contains("BAS_ABI_999", output);
            Assert.Contains("E0004", output);
        }
        finally
        {
            try { Directory.Delete(dir, true); } catch { }
        }
    }

    /// <summary>hubasic2sl の出力を slangbuild で AILZ80ASM まで通し、BAS_ 関数がリンクされた bin ができる</summary>
    [SkippableFact]
    public void Build_TranspiledProgramAssemblesForX1Native()
    {
        var root = RepoRoot();
        var asmPath = Path.Combine(root, "tools", OperatingSystem.IsWindows() ? "AILZ80ASM.exe" : "AILZ80ASM");
        Skip.IfNot(File.Exists(asmPath), "AILZ80ASM がない (make setup-tools で取得)");
        var dir = Path.Combine(Path.GetTempPath(), $"hubasic2sl_{Guid.NewGuid():N}");
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
            var sl4 = Path.Combine(dir, "STR.SL");
            File.WriteAllText(sl4, Transpile(StringScreenProgram).Output!);
            RunSlangbuild(root, dir, sl4, asmPath, Path.Combine(dir, "STR"));
            AssertBuilt(Path.Combine(dir, "STR"));
            var sl5 = Path.Combine(dir, "INP.SL");
            File.WriteAllText(sl5, Transpile(InputSoundProgram).Output!);
            RunSlangbuild(root, dir, sl5, asmPath, Path.Combine(dir, "INP"), "x1native_slfs");
            AssertBuilt(Path.Combine(dir, "INP"));
            // 浮動小数の変数と配列 (段階 F2a) も両方で組み立てまで通る
            var sl6 = Path.Combine(dir, "FVAR.SL");
            File.WriteAllText(sl6, Transpile(FloatVarProgram).Output!);
            RunSlangbuild(root, dir, sl6, asmPath, Path.Combine(dir, "FVAR"));
            AssertBuilt(Path.Combine(dir, "FVAR"));
            RunSlangbuild(root, dir, sl6, asmPath, Path.Combine(dir, "FVARS"), "x1native_slfs");
            AssertBuilt(Path.Combine(dir, "FVARS"));
            // 数の関数と ^ (段階 F3a。libfloat の LOG・ATN・FPOW を含む) も両方で組み立てまで通る
            var sl7 = Path.Combine(dir, "MATH.SL");
            File.WriteAllText(sl7, Transpile(MathProgram).Output!);
            RunSlangbuild(root, dir, sl7, asmPath, Path.Combine(dir, "MATH"));
            AssertBuilt(Path.Combine(dir, "MATH"));
            RunSlangbuild(root, dir, sl7, asmPath, Path.Combine(dir, "MATHS"), "x1native_slfs");
            AssertBuilt(Path.Combine(dir, "MATHS"));
            // 文字列の関数・TAB・RANDOMIZE (段階 F3b) も両方で組み立てまで通る
            var sl8 = Path.Combine(dir, "SFN.SL");
            File.WriteAllText(sl8, Transpile(StringFuncProgram).Output!);
            RunSlangbuild(root, dir, sl8, asmPath, Path.Combine(dir, "SFN"));
            AssertBuilt(Path.Combine(dir, "SFN"));
            RunSlangbuild(root, dir, sl8, asmPath, Path.Combine(dir, "SFNS"), "x1native_slfs");
            AssertBuilt(Path.Combine(dir, "SFNS"));
            // 段階 G1 の命令 (SWAP の配列の要素の番地・SCRN$・PALET など) も両方で組み立てまで通る
            var sl9 = Path.Combine(dir, "G1.SL");
            File.WriteAllText(sl9, Transpile(G1Program).Output!);
            RunSlangbuild(root, dir, sl9, asmPath, Path.Combine(dir, "G1"));
            AssertBuilt(Path.Combine(dir, "G1"));
            RunSlangbuild(root, dir, sl9, asmPath, Path.Combine(dir, "G1S"), "x1native_slfs");
            AssertBuilt(Path.Combine(dir, "G1S"));
            // 段階 G2 の文字列の配列 (要素の番地・SWAP) も両方で組み立てまで通る
            var sl10 = Path.Combine(dir, "G2.SL");
            File.WriteAllText(sl10, Transpile(G2Program).Output!);
            RunSlangbuild(root, dir, sl10, asmPath, Path.Combine(dir, "G2"));
            AssertBuilt(Path.Combine(dir, "G2"));
            RunSlangbuild(root, dir, sl10, asmPath, Path.Combine(dir, "G2S"), "x1native_slfs");
            AssertBuilt(Path.Combine(dir, "G2S"));
            // 段階 G3 のグラフィックの LINE (libx1_grp の LINE を呼ぶ) も両方で組み立てまで通る
            var sl11 = Path.Combine(dir, "G3.SL");
            File.WriteAllText(sl11, Transpile(G3Program).Output!);
            RunSlangbuild(root, dir, sl11, asmPath, Path.Combine(dir, "G3"));
            AssertBuilt(Path.Combine(dir, "G3"));
            RunSlangbuild(root, dir, sl11, asmPath, Path.Combine(dir, "G3S"), "x1native_slfs");
            AssertBuilt(Path.Combine(dir, "G3S"));
            // 段階 G4 の BF と PAINT (libx1_grp の塗りを呼ぶ) も両方で組み立てまで通る
            var sl12 = Path.Combine(dir, "G4.SL");
            File.WriteAllText(sl12, Transpile(G4Program).Output!);
            RunSlangbuild(root, dir, sl12, asmPath, Path.Combine(dir, "G4"));
            AssertBuilt(Path.Combine(dir, "G4"));
            RunSlangbuild(root, dir, sl12, asmPath, Path.Combine(dir, "G4S"), "x1native_slfs");
            AssertBuilt(Path.Combine(dir, "G4S"));
            // 段階 G5 の命令 (POKE・PEEK・CALL・TIME と LABEL) も両方で組み立てまで通る
            var sl13 = Path.Combine(dir, "G5.SL");
            File.WriteAllText(sl13, Transpile(G5Program).Output!);
            RunSlangbuild(root, dir, sl13, asmPath, Path.Combine(dir, "G5"));
            AssertBuilt(Path.Combine(dir, "G5"));
            RunSlangbuild(root, dir, sl13, asmPath, Path.Combine(dir, "G5S"), "x1native_slfs");
            AssertBuilt(Path.Combine(dir, "G5S"));
            // MIRROR$・HEXCHR$・MUSIC も両方で組み立てまで通る
            var slm = Path.Combine(dir, "MIR.SL");
            File.WriteAllText(slm, Transpile(MirrorProgram).Output!);
            RunSlangbuild(root, dir, slm, asmPath, Path.Combine(dir, "MIR"));
            AssertBuilt(Path.Combine(dir, "MIR"));
            RunSlangbuild(root, dir, slm, asmPath, Path.Combine(dir, "MIRS"), "x1native_slfs");
            AssertBuilt(Path.Combine(dir, "MIRS"));
            // 2 次元の配列と BIN$ も両方で組み立てまで通る
            var sl2d = Path.Combine(dir, "TWO.SL");
            File.WriteAllText(sl2d, Transpile(TwoDimProgram).Output!);
            RunSlangbuild(root, dir, sl2d, asmPath, Path.Combine(dir, "TWO"));
            AssertBuilt(Path.Combine(dir, "TWO"));
            RunSlangbuild(root, dir, sl2d, asmPath, Path.Combine(dir, "TWOS"), "x1native_slfs");
            AssertBuilt(Path.Combine(dir, "TWOS"));
            // CONSOLE と CHR$(14)・CHR$(15) も両方で組み立てまで通る
            var slc = Path.Combine(dir, "CON.SL");
            File.WriteAllText(slc, Transpile(ConsoleProgram).Output!);
            RunSlangbuild(root, dir, slc, asmPath, Path.Combine(dir, "CON"));
            AssertBuilt(Path.Combine(dir, "CON"));
            RunSlangbuild(root, dir, slc, asmPath, Path.Combine(dir, "CONS"), "x1native_slfs");
            AssertBuilt(Path.Combine(dir, "CONS"));
            // VSYNC 文 (BAS_VSYNC) も組み立てまで通る
            var sl14 = Path.Combine(dir, "VS.SL");
            File.WriteAllText(sl14, Transpile("10 A=2:VSYNC:VSYNC A:GOTO 10").Output!);
            RunSlangbuild(root, dir, sl14, asmPath, Path.Combine(dir, "VSS"), "x1native_slfs");
            AssertBuilt(Path.Combine(dir, "VSS"));
            // 行番号を書かない出力 (--no-line-numbers) も両方で組み立てまで通る
            var sl15 = Path.Combine(dir, "NL.SL");
            File.WriteAllText(sl15, Transpile(LineNumberProgram, new HuTranspileOptions(LineNumbers: false)).Output!);
            RunSlangbuild(root, dir, sl15, asmPath, Path.Combine(dir, "NL"));
            AssertBuilt(Path.Combine(dir, "NL"));
            RunSlangbuild(root, dir, sl15, asmPath, Path.Combine(dir, "NLS"), "x1native_slfs");
            AssertBuilt(Path.Combine(dir, "NLS"));
            // --string-len の出力 (シフトの 63 と掛け算の 100) も組み立てまで通る
            var sl16 = Path.Combine(dir, "SL.SL");
            File.WriteAllText(sl16, Transpile(G2Program, new HuTranspileOptions(StringLen: 63)).Output!);
            RunSlangbuild(root, dir, sl16, asmPath, Path.Combine(dir, "SL"));
            AssertBuilt(Path.Combine(dir, "SL"));
            var sl17 = Path.Combine(dir, "SM.SL");
            File.WriteAllText(sl17, Transpile(G2Program, new HuTranspileOptions(StringLen: 100)).Output!);
            RunSlangbuild(root, dir, sl17, asmPath, Path.Combine(dir, "SMS"), "x1native_slfs");
            AssertBuilt(Path.Combine(dir, "SMS"));
        }
        finally
        {
            try { Directory.Delete(dir, true); } catch { }
        }
    }

    private static void RunSlangbuild(string root, string dir, string sl, string asmPath, string outBase, string env = "x1native")
    {
        var (code, output) = RunSlangbuildRaw(root, dir, sl, asmPath, outBase, env);
        Assert.True(code == 0, $"slangbuild failed:\n{output}");
    }

    /// <summary>slangbuild を動かして、終了コードと stdout・stderr をつないだものを返す</summary>
    private static (int Code, string Output) RunSlangbuildRaw(string root, string dir, string sl, string asmPath, string outBase, string env = "x1native")
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
        return (proc.ExitCode, so.Result + "\n" + se.Result);
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

    /// <summary>
    /// 段階 G5a: 名前への飛び先は LABEL の文の直後のラベル LB番号 で、そこで実行中の行番号を入れ直す
    /// (行の途中に飛んでも実行時エラーの行番号が合う)。同じ名前の後ろの方もラベルは出すが、飛び先は前の方
    /// </summary>
    [Fact]
    public void Slang_StageG5aLabels()
    {
        var sl = Body(Transpile("10 DEFINT A-Z:GOTO \"B\"\n20 PRINT 1:LABEL\"B\":ON A GOSUB \"B\",20\n30 LABEL\"B\":GOSUB \"B\"").Output!);
        Assert.Contains("GOTO LB0;", sl);
        Assert.Contains("LB0:\n  H_LINE=20;", sl.Replace("\r\n", "\n"));
        Assert.Contains("LB1:\n  H_LINE=30;", sl.Replace("\r\n", "\n"));
        Assert.Contains("1: GOTO LB0; 2: GOTO L20;", sl);
        Assert.DoesNotContain("GOTO LB1;", sl);
    }

    /// <summary>
    /// 段階 G5b: POKE・POKE@ は番地を H_PK に 1 回だけ入れて、値ごとに MEM[]・PORT[] に BAS_BYTE (0〜255 の検査) で書く。
    /// PEEK・PEEK@ は MEM[]・PORT[]。CALL は BAS_CALL。TIME= は小数を切り捨てて BAS_TIMESET。式の TIME は
    /// BAS_TIMEH・BAS_TIMEL を続けて読んで ×4 + で組み立てる (TIME が 2 つあっても組は混ざらない)
    /// </summary>
    /// <summary>入力のファイル名 (拡張子なし) が、頭のコメントの `// disk-main-name:` になる (slangbuild が IPL の起動名にする)</summary>
    [Theory]
    [InlineData("GAME.bas", "GAME", false)]
    [InlineData("mygame.BAS", "mygame", false)]                    // 大文字小文字はそのまま
    [InlineData("LongFileNameGame1.bas", "LongFileNameG", true)]  // 13 文字に切る
    [InlineData("ABCDEFGHIJKL MN.bas", "ABCDEFGHIJKL", true)]     // 切った後ろの空白は除く
    [InlineData("\u30b2\u30fc\u30e0.bas", "", true)]                 // ASCII 以外の字があれば使わない
    [InlineData("ABCDEFGHIJKLM\u3042.bas", "", true)]               // 13 文字より後ろの字も調べる
    [InlineData(".bas", "", false)]
    [InlineData("   .bas", "", false)]
    [InlineData("", "", false)]
    public void Slang_DiskMainName(string sourceName, string expected, bool warns)
    {
        var r = Transpile("10 END", new HuTranspileOptions(SourceName: sourceName));
        var head = r.Output!.Replace("\r\n", "\n").Split("\n\n")[0];
        if (expected.Length > 0)
            Assert.EndsWith($"\n// disk-main-name: {expected}", head);
        else
            Assert.DoesNotContain("disk-main-name", head);
        Assert.Equal(warns, r.Diagnostics.Items.Any(d => d.Code == "HB0112" && d.Severity == HuSeverity.Warning));
    }

    /// <summary>
    /// MIRROR$ は、引数が変数・配列の要素なら BAS_SMIRRORV (その場で書き換える。本物と同じ)、定数・式なら BAS_SMIRROR (写してから)。
    /// MUSIC は PLAY と同じく、文字列なら BAS_PLAY、数なら BAS_TEMPO
    /// </summary>
    [Fact]
    public void Slang_MirrorAndMusic()
    {
        var sl = Body(Transpile(MirrorProgram).Output!);
        foreach (var expected in new[]
        {
            "BAS_SSET(S_A, BAS_SHEX(K_0));", "BAS_SCAT(BAS_SMIRRORV(S_A), BAS_SMIRROR(BAS_SHEX(K_1)))",
            "BAS_PLAY(S_C);", "BAS_TEMPO(120);", "BAS_TEMPO(BAS_FTOIR(F_X));", "BAS_PLAY(K_3);", "BAS_PLAY(K_4);",
        })
            Assert.Contains(expected, sl);
        var kinds = Body(Transpile("10 DIM N$(3):A$=MIRROR$(\"A\")+MIRROR$(N$(1))+MIRROR$(S$+\"\")").Output!);
        Assert.Contains("BAS_SMIRROR(K_0)", kinds);
        Assert.Contains("BAS_SMIRRORV((SA_N+(BAS_AIDX(1, &H_DIM_S_N, &H_UB_S_N)<<8)))", kinds);
        Assert.Contains("BAS_SMIRROR(BAS_SCAT(S_S, K_1))", kinds);
    }

    /// <summary>
    /// 値が変数を書き換える MIRROR$ を含む代入と OUT は、本物と同じく添字・ポートを先に読む
    /// (本物の実測: N%(ASC(S$))=ASC(MIRROR$(S$)) は N%(1) に入れる)。含まないときの出力は前と同じ
    /// </summary>
    [Fact]
    public void Slang_InPlaceMirrorReadsIndexFirst()
    {
        var sl = Body(Transpile("10 DIM N%(200):N%(ASC(S$))=ASC(MIRROR$(S$)):OUT ASC(S$),ASC(MIRROR$(S$)):OUT 5,ASC(MIRROR$(S$))").Output!)
            .Replace("\r\n", "\n");
        Assert.Contains("H_I1=BAS_AIDX(BAS_SASC(S_S), &H_DIM_N, &H_UB_N);\n  A_N[H_I1]=BAS_SASC(BAS_SMIRRORV(S_S));", sl);
        Assert.Contains("H_I1=BAS_SASC(S_S);\n  PORT[H_I1]=BAS_SASC(BAS_SMIRRORV(S_S));", sl);
        Assert.Contains("PORT[5]=BAS_SASC(BAS_SMIRRORV(S_S));", sl);
        // 添字・ポートの側で書き換えるときも先に読む (本物では N%(128)=128)
        var rev = Body(Transpile("10 DIM N%(128):N%(ASC(MIRROR$(S$)))=ASC(S$):OUT ASC(MIRROR$(S$)),ASC(S$)").Output!).Replace("\r\n", "\n");
        Assert.Contains("H_I1=BAS_AIDX(BAS_SASC(BAS_SMIRRORV(S_S)), &H_DIM_N, &H_UB_N);\n  A_N[H_I1]=BAS_SASC(S_S);", rev);
        Assert.Contains("H_I1=BAS_SASC(BAS_SMIRRORV(S_S));\n  PORT[H_I1]=BAS_SASC(S_S);", rev);
        var plain = Body(Transpile("10 DIM N%(200):N%(ASC(S$))=ASC(MIRROR$(S$+\"\")):OUT ASC(S$),ASC(S$)").Output!);
        Assert.Contains("A_N[BAS_AIDX(BAS_SASC(S_S), &H_DIM_N, &H_UB_N)]=BAS_SASC(BAS_SMIRROR(BAS_SCAT(S_S, K_0)));", plain);
        Assert.Contains("PORT[BAS_SASC(S_S)]=BAS_SASC(S_S);", plain);
    }

    /// <summary>CONSOLE は引数なし・2 つ・4 つで BAS_CONSOLE0・BAS_CONSOLEY・BAS_CONSOLE を呼ぶ (引数は整数にする)</summary>
    [Fact]
    public void Slang_Console()
    {
        var sl = Body(Transpile(ConsoleProgram).Output!);
        foreach (var expected in new[]
        {
            "BAS_CONSOLE(5, 10, 3, 20);", "BAS_CONSOLEY(BAS_FTOIR(F_A), BAS_FTOIR((F_A+H_FK[1])));", "BAS_CONSOLE0();",
        })
            Assert.Contains(expected, sl);
    }

    /// <summary>
    /// 2 次元の配列: 起動のときに上限を min(10, 置き場の上限) で入れ、DIM は BAS_DIM と 2 つめの上限の代入、要素は次元ごとに
    /// BAS_AIDX で調べて「1 つめ × 置き場の 2 つめの大きさ + 2 つめ」
    /// </summary>
    [Fact]
    public void Slang_TwoDimArrays()
    {
        var r = Transpile("10 DEFINT A-Z:DIM A(2,20):A(1,2)=A(2,3)+B(4,5):DIM C$(1,1):C$(1,0)=\"X\"");
        var sl = Body(r.Output!).Replace("\r\n", "\n");
        foreach (var expected in new[]
        {
            "H_DIM_A=0;\n  H_UB_A=2;\n  H_UB2_A=10;", "H_UB_B=10;\n  H_UB2_B=10;", "H_UB_S_C=1;\n  H_UB2_S_C=1;",
            "BAS_DIM(&H_DIM_A, &H_UB_A, 2);\n  H_UB2_A=20;",
            "A_A[(BAS_AIDX(1, &H_DIM_A, &H_UB_A)*21+BAS_AIDX(2, &H_DIM_A, &H_UB2_A))]=((A_A[(BAS_AIDX(2, &H_DIM_A, &H_UB_A)*21+BAS_AIDX(3, &H_DIM_A, &H_UB2_A))])+(A_B[(BAS_AIDX(4, &H_DIM_B, &H_UB_B)*11+BAS_AIDX(5, &H_DIM_B, &H_UB2_B))]));",
            "BAS_SSET((SA_C+((BAS_AIDX(1, &H_DIM_S_C, &H_UB_S_C)*2+BAS_AIDX(0, &H_DIM_S_C, &H_UB2_S_C))<<8)), K_0);",
        })
            Assert.Contains(expected, sl);
        var head = r.Output!.Split("MAIN()")[0];
        Assert.Contains("H_UB2_A", head);
        Assert.Contains("ARRAY A_A[62];", head);    // 3×21 要素
        Assert.Contains("ARRAY A_B[120];", head);   // DIM なしは 11×11
        // 文字列の要素の番地: 1 要素がシフトになる長さ (64) と掛け算になる長さ (41)
        sl = Body(Transpile("10 DIM C$(1,1):C$(1,0)=\"X\"", new HuTranspileOptions(StringLen: 63)).Output!);
        Assert.Contains("BAS_AIDX(0, &H_DIM_S_C, &H_UB2_S_C))<<6)", sl);
        sl = Body(Transpile("10 DIM C$(1,1):C$(1,0)=\"X\"", new HuTranspileOptions(StringLen: 40)).Output!);
        Assert.Contains("BAS_AIDX(0, &H_DIM_S_C, &H_UB2_S_C))*41)", sl);
    }

    /// <summary>
    /// 2 次元の添字は左から決める: 2 つめが前置きの代入を出す (浮動小数・その場の MIRROR$) と、1 つめを先に一時変数へ入れる。
    /// 前置きがなければ 1 つの式のまま
    /// </summary>
    [Fact]
    public void Slang_TwoDimIndexOrder()
    {
        var sl = Body(Transpile("10 S$=CHR$(1):A%(ASC(S$)\\128,(ASC(MIRROR$(S$))\\128)+0.0)=1").Output!).Replace("\r\n", "\n");
        Assert.Contains("H_I1=BAS_AIDX(((BAS_SASC(S_S))./.(128)), &H_DIM_A, &H_UB_A);\n  H_F1=((BAS_SASC(BAS_SMIRRORV(S_S)))./.(128));\n" +
                        "  A_A[(H_I1*11+BAS_AIDX(BAS_FTOIR((H_F1+H_FK[0])), &H_DIM_A, &H_UB2_A))]=1;", sl);
        // その場の MIRROR$ だけ (前置きなし) でも 1 つめを先に入れる
        sl = Body(Transpile("10 S$=CHR$(1):A%(ASC(S$),ASC(MIRROR$(S$)))=1").Output!).Replace("\r\n", "\n");
        Assert.Contains("H_I1=BAS_AIDX(BAS_SASC(S_S), &H_DIM_A, &H_UB_A);\n  A_A[(H_I1*11+", sl);
        // ループの中で 2 つめだけが一時文字列を作る
        sl = Body(Transpile("10 FOR I%=0 TO 3:A%(I%,LEN(STR$(I%)))=1:NEXT").Output!).Replace("\r\n", "\n");
        Assert.Contains("BAS_STMP();\n  H_I1=BAS_AIDX(V_I, &H_DIM_A, &H_UB_A);", sl);
        // 前置きがなければ一時変数を使わない
        sl = Body(Transpile("10 A%(1,B%+1)=A%(2,3)").Output!);
        Assert.DoesNotContain("H_I1", sl);
    }

    [Fact]
    public void Slang_BinS()
    {
        var sl = Body(Transpile("10 DEFINT A-Z:A$=BIN$(5):PRINT BIN$(X)").Output!);
        Assert.Contains("BAS_SBINS(5)", sl);
        Assert.Contains("BAS_SBINS(V_X)", sl);
    }

    /// <summary>WIDTH の 2 つめから後は読み飛ばし (1 つめは今までどおり渡す)、KEYLIST・KLIST・KMODE は何も出さない</summary>
    [Fact]
    public void Slang_TurboForms()
    {
        var sl = Body(Transpile("10 DEFINT A-Z:W=80:WIDTH 40,25:WIDTH W,25,0,1:KEYLIST 0:KLIST 1:KMODE 0:PRINT 1").Output!);
        Assert.Contains("BAS_WIDTH(40);", sl);
        Assert.Contains("BAS_WIDTH(V_W);", sl);
        Assert.Equal(2, sl.Split("BAS_WIDTH(").Length - 1);
        Assert.DoesNotContain("25", sl);
        Assert.DoesNotContain("KLIST", sl);
        Assert.DoesNotContain("KMODE", sl);
    }

    [Fact]
    public void Slang_StageG5bCalls()
    {
        var sl = Body(Transpile(G5Program).Output!).Replace("\r\n", "\n");
        foreach (var expected in new[]
        {
            "H_PK=$F010;\n  MEM[H_PK]=BAS_BYTE(1);\n  MEM[H_PK+1]=BAS_BYTE(V_C);\n  MEM[H_PK+2]=BAS_BYTE(BAS_FTOIR(F_X));",
            "H_PK=((12288)+(V_C));\n  PORT[H_PK]=BAS_BYTE(65);\n  PORT[H_PK+1]=BAS_BYTE(66);",
            "((MEM[$F010])+(PORT[12288]))", "BAS_CALL($F020);", "BAS_TIMESET(BAS_FTOIR(BAS_FFLOOR(F_X)));",
            "H_F1=BAS_TIMEH();\n  H_F2=BAS_TIMEL();\n  H_F5=(H_F1*H_FK[2]+H_F2);\n  H_F3=BAS_TIMEH();\n  H_F4=BAS_TIMEL();\n  F_T=(H_F5+(H_F3*H_FK[2]+H_F4));",
            "V_C=BAS_FTOIR((H_F1*H_FK[2]+H_F2));",
        })
            Assert.Contains(expected, sl);
        Assert.Contains("VAR ", Transpile(G5Program).Output!.Split("MAIN()")[0]);
        Assert.Contains("H_PK", Transpile(G5Program).Output!.Split("MAIN()")[0]);
        // 値は整数にしてから (±65535 は 16 ビットに回り込む) 0〜255 を調べる。本物も同じ (実測: POKE a,-65535 は 1、-65281 は 255 を書き、
        // -65280 と 65535 は Illegal function call。POKE@ も同じ)
        var neg = Body(Transpile("10 POKE &HF000,-65535:POKE@&H3000,-65471:X=-65535:POKE 0,X").Output!);
        Assert.Contains("MEM[H_PK]=BAS_BYTE(1);", neg);
        Assert.Contains("PORT[H_PK]=BAS_BYTE(65);", neg);
        Assert.Contains("MEM[H_PK]=BAS_BYTE(BAS_FTOIR(F_X));", neg);
        // POKE のないプログラムは H_PK を宣言しない (今までの出力を変えない)
        Assert.DoesNotContain("H_PK", Transpile(Stage1Program).Output!);
    }
}
