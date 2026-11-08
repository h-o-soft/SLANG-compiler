using System.Text.RegularExpressions;
using Xunit;
using SLANGCompiler.Lexer;
using SLANGCompiler.Parser;
using SLANGCompiler.IR;
using SLANGCompiler.CodeGen;
using SLANGCompiler.Semantics;

namespace SLANGCompiler.Tests;

/// <summary>
/// EXIT・EXIT(n)・EXIT TO・CONTINUE の行き先。CASE はループとして数えない
/// </summary>
public class ControlFlowTests
{
    /// <summary>ASM まで作る。エラーがあれば (null, 診断)</summary>
    private static (string? Asm, DiagnosticBag Diag) Compile(string source)
    {
        var diag = new DiagnosticBag();
        var tokens = new Preprocessor(diag).Process(new Lexer.Lexer(source).Tokenize(), ".");
        var ast = new Parser.Parser(tokens, diag).ParseCompilationUnit();
        if (diag.HasErrors) return (null, diag);
        var analyzer = new SemanticAnalyzer(diag);
        analyzer.Analyze(ast);
        if (diag.HasErrors) return (null, diag);
        var ir = new IrGenerator(diag, analyzer.Symbols).Generate(ast);
        if (diag.HasErrors) return (null, diag);
        return (new CodeGenerator(ir).Generate(), diag);
    }

    private static string MainBody(string source)
    {
        var (asm, diag) = Compile(source);
        Assert.False(diag.HasErrors, string.Join("; ", diag.Diagnostics));
        return asm!.Split("\nMAIN:")[1].Split("_MAIN_EXIT")[0];
    }

    /// <summary>印の行 (; MARK) の直後の JP の行き先</summary>
    private static string JumpAfterMark(string body, string mark)
        => Regex.Match(body, $@"; {mark}\s*\n\s*JP\t(\w+)").Groups[1].Value;

    /// <summary>ラベルの行の直後に続く行 (空でない最初の行)</summary>
    private static string LineAfterLabel(string body, string label)
        => Regex.Match(body, $@"(?m)^{label}:\s*\n(?:\w+:\s*\n)*\s*(.+)$").Groups[1].Value.Trim();

    [Fact]
    public void ExitLevel_LeavesOuterLoop()
    {
        // EXIT(2) は、内側から 2 番目のループ (外の FOR) の後ろへ
        var body = MainBody(@"
VAR I, J, N;
MAIN()
BEGIN
  FOR I = 0 TO 3 DO [
    FOR J = 0 TO 3 DO [
      IF J == 1 THEN [ #ASM
; X1
#END
        EXIT(2); ]
    ];
    N = 7;
  ];
  N = 9;
END;");
        var target = JumpAfterMark(body, "X1");
        Assert.Matches(@"LD\tHL,\$0009", LineAfterLabel(body, target));
    }

    [Fact]
    public void Exit_SameAsExitOne()
    {
        var body = MainBody(@"
VAR I, J, N;
MAIN()
BEGIN
  FOR I = 0 TO 3 DO [
    FOR J = 0 TO 3 DO [
      IF J == 1 THEN [ #ASM
; X1
#END
        EXIT(1); ]
      IF J == 2 THEN [ #ASM
; X0
#END
        EXIT; ]
    ];
    N = 7;
  ];
END;");
        var t1 = JumpAfterMark(body, "X1");
        Assert.Equal(t1, JumpAfterMark(body, "X0"));
        Assert.Matches(@"LD\tHL,\$0007", LineAfterLabel(body, t1));
    }

    [Fact]
    public void ExitAndContinue_InCase_ActOnEnclosingLoop()
    {
        // CASE の中の EXIT・CONTINUE は、CASE でなく外のループに効く。EXIT(n) の段にも CASE を数えない
        var body = MainBody(@"
VAR I, J, N;
MAIN()
BEGIN
  FOR I = 0 TO 9 DO [
    FOR J = 0 TO 9 DO [
      CASE J OF {
        2: [ #ASM
; C
#END
             CONTINUE; ]
        5: [ #ASM
; E
#END
             EXIT; ]
        6: [ #ASM
; E2
#END
             EXIT(2); ]
      }
      N = 1;
    ];
    N = 2;
  ];
  N = 3;
END;");
        // CONTINUE: 内の FOR の、足す所 (J を読み直す)
        Assert.Matches(@"LD\tHL,\(_V_J\)", LineAfterLabel(body, JumpAfterMark(body, "C")));
        // EXIT: 内の FOR の後ろ (N = 2)
        Assert.Matches(@"LD\tHL,\$0002", LineAfterLabel(body, JumpAfterMark(body, "E")));
        // EXIT(2): 外の FOR の後ろ (N = 3)
        Assert.Matches(@"LD\tHL,\$0003", LineAfterLabel(body, JumpAfterMark(body, "E2")));
    }

    [Fact]
    public void ExitTo_UsesUserLabel()
    {
        // EXIT TO は GOTO と同じラベル (_LBL_)
        var body = MainBody(@"
VAR I;
MAIN()
BEGIN
  LOOP [ I++; IF I == 5 THEN EXIT TO DONE; ];
DONE:
  I = 0;
END;");
        Assert.Contains("JP\t_LBL_DONE", body);
        Assert.Contains("_LBL_DONE:", body);
    }

    [Fact]
    public void Continue_InRepeat_GoesToUntil()
    {
        // REPEAT の中の CONTINUE は UNTIL の判定へ (本体の頭へ戻らない)
        var body = MainBody(@"
VAR I;
MAIN()
BEGIN
  REPEAT [
    I++;
    IF I == 2 THEN [ #ASM
; C
#END
      CONTINUE; ]
    I = I + 10;
  ] UNTIL I >= 30;
END;");
        var target = JumpAfterMark(body, "C");
        // 行き先は、本体 (I = I + 10) の後ろの、UNTIL の条件 (I >= 30) の始まり
        Assert.Matches(@"LD\tHL,\(_V_I\)", LineAfterLabel(body, target));
        Assert.Matches($@"(?s)\$000A.*\n{target}:.*\$001E.*JP\tC,", body);
    }

    [Fact]
    public void Repeat_WithoutContinue_LabelsUnchanged()
    {
        // CONTINUE のない REPEAT は、いままでどおりのラベル (頭と後ろの 2 つ) だけ
        var body = MainBody("VAR I; MAIN() BEGIN REPEAT I++; UNTIL I >= 3; I = 0; END;");
        Assert.Equal(2, Regex.Matches(body, @"(?m)^_L\d+:").Count);
    }

    [Theory]
    [InlineData("FOR I = 0 TO 3 DO [ EXIT(0); ];", "EXIT level must be a constant of 1 or more")]
    [InlineData("FOR I = 0 TO 3 DO [ EXIT(I); ];", "EXIT level must be a constant of 1 or more")]
    [InlineData("FOR I = 0 TO 3 DO [ EXIT(2); ];", "EXIT(2) exceeds the loop nesting")]
    [InlineData("FOR I = 0 TO 3 DO [ CASE I OF { 1: EXIT(2); } ];", "EXIT(2) exceeds the loop nesting")]
    [InlineData("CASE I OF { 1: EXIT; }", "EXIT outside loop")]
    [InlineData("CASE I OF { 1: CONTINUE; }", "CONTINUE outside loop")]
    [InlineData("EXIT;", "EXIT outside loop")]
    public void Exit_Errors(string stmts, string message)
    {
        var (_, diag) = Compile($"VAR I; MAIN() BEGIN {stmts} END;");
        Assert.Contains(diag.Diagnostics, d => d.Message.Contains(message));
    }

    [Fact]
    public void ExitLevel_ConstName()
    {
        // n は CONST の名前でもよい
        var body = MainBody(@"
CONST OUT = 2;
VAR I, J, N;
MAIN()
BEGIN
  FOR I = 0 TO 3 DO [
    FOR J = 0 TO 3 DO [ #ASM
; X
#END
      EXIT(OUT); ];
  ];
  N = 9;
END;");
        Assert.Matches(@"LD\tHL,\$0009", LineAfterLabel(body, JumpAfterMark(body, "X")));
    }
}
