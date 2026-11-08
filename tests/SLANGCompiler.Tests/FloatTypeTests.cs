using SLANGCompiler.IR;
using SLANGCompiler.Lexer;
using SLANGCompiler.Parser;
using SLANGCompiler.Semantics;
using Xunit;

namespace SLANGCompiler.Tests;

/// <summary>
/// FLOAT の型の整理。二項演算の左の変換は、右を作った後で、実際の大きさで要るときだけ左の直後に差し込む
/// (右が FLOAT になるかを先に推定しない。推定が外れると、右の値が変換されて左の値が消えていた)。
/// 局所の初期値も、代入と同じく変数の型に変えて、割り当てた大きさで書く。
/// </summary>
public class FloatTypeTests
{
    /// <summary>libfloat を読んだランタイム (引数の型・戻り値の型の注釈を使う)</summary>
    private static Runtime.RuntimeManager LibFloat()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir != null && !File.Exists(Path.Combine(dir.FullName, "runtime", "libfloat.asm")))
            dir = dir.Parent;
        Assert.NotNull(dir);
        var path = Path.Combine(dir!.FullName, "runtime", "libfloat.asm");
        var rt = new Runtime.RuntimeManager();
        rt.LoadFromString(File.ReadAllText(path), path);
        return rt;
    }

    private static readonly Runtime.RuntimeManager Rt = LibFloat();

    private static (DiagnosticBag Diag, IrModule Module) Generate(string source)
    {
        var diag = new DiagnosticBag();
        var tokens = new Lexer.Lexer(source).Tokenize();
        tokens = new Preprocessor(diag).Process(tokens, ".");
        var ast = new Parser.Parser(tokens, diag).ParseCompilationUnit();
        Assert.False(diag.HasErrors, $"Parse errors: {string.Join("; ", diag.Diagnostics)}");
        var analyzer = new SemanticAnalyzer(diag);
        analyzer.Analyze(ast);
        var module = new IrGenerator(diag, analyzer.Symbols, Rt).Generate(ast);
        return (diag, module);
    }

    /// <summary>ASM 全体 (エラーがないこと)</summary>
    private static string Asm(string source)
    {
        var (diag, module) = Generate(source);
        Assert.False(diag.HasErrors, $"IR errors: {string.Join("; ", diag.Diagnostics)}");
        return new SLANGCompiler.CodeGen.CodeGenerator(module, Rt).Generate();
    }

    /// <summary>MAIN の ASM</summary>
    private static string MainAsm(string source) => Asm(source).Split("MAIN:")[1].Split("_MAIN_EXIT")[0];

    private static string Lines(params string[] lines) => string.Join("\n", lines.Select(l => "\t" + l));

    private static int Count(string s, string part) => s.Split(part).Length - 1;

    [Fact]
    public void IntLeft_FloatArrayElementRight_ConvertsLeft()
    {
        // 今までは右 (FA[I]) の値が変換されて N が消えていた
        var asm = MainAsm("VAR N, I; FLOAT F; FLOAT FA[3]; MAIN() BEGIN F = N - FA[I]; END;");
        Assert.Contains(Lines("LD\tHL,(_V_N)", "CALL\ti16tof24", "PUSH\tAF", "PUSH\tHL", "LD\tHL,(_V_I)"), asm);
        Assert.Equal(1, Count(asm, "i16tof24"));
        Assert.Contains("CALL\tf24sub", asm);
    }

    [Theory]
    [InlineData("I = I + TWO();", "I", "f24add")]
    [InlineData("F = N - TWO();", "N", "f24sub")]
    public void IntLeft_FloatFunctionRight_ConvertsLeft(string body, string left, string op)
    {
        var asm = MainAsm($"VAR N, I; FLOAT F; TWO:FLOAT() BEGIN RETURN 2.5; END; MAIN() BEGIN {body} END;");
        Assert.Contains(Lines($"LD\tHL,(_V_{left})", "CALL\ti16tof24", "PUSH\tAF", "PUSH\tHL", "CALL\tTWO"), asm);
        Assert.Equal(1, Count(asm, "i16tof24"));
        Assert.Contains($"CALL\t{op}", asm);
    }

    [Fact]
    public void IntLeft_ConditionalWithFloatElementRight_ConvertsLeft()
    {
        var asm = MainAsm("VAR I, W; FLOAT F; FLOAT FA[3]; MAIN() BEGIN F = I * (W ? FA[0] : 1); END;");
        Assert.Contains(Lines("LD\tHL,(_V_I)", "CALL\ti16tof24", "PUSH\tAF", "PUSH\tHL", "LD\tHL,(_V_W)"), asm);
        Assert.Contains(Lines("LD\tHL,$0001", "CALL\ti16tof24"), asm);   // 偽の側の変換
        Assert.Equal(2, Count(asm, "i16tof24"));
    }

    [Fact]
    public void InsertedConversion_BeforeFusedCompareAndJump()
    {
        var asm = MainAsm("VAR I; TWO:FLOAT() BEGIN RETURN 2.5; END; MAIN() BEGIN IF I < TWO() THEN PRINT(1); END;");
        Assert.Contains(Lines("LD\tHL,(_V_I)", "CALL\ti16tof24", "PUSH\tAF", "PUSH\tHL", "CALL\tTWO"), asm);
        Assert.Contains(Lines("CALL\tf24cmp", "JP\tNC,_L0"), asm);
    }

    [Fact]
    public void InsertedConversion_BeforeHalfDirectCompare()
    {
        // 右の FLOAT の要素は直接 CDE に読む。左の変換は、その前に入る
        var asm = MainAsm("VAR I, W; FLOAT FA[3]; MAIN() BEGIN W = I < FA[1]; END;");
        Assert.Contains(Lines("LD\tHL,(_V_I)", "CALL\ti16tof24", "EX\tAF,AF'", "LD\tA,(_V_FA+3+2)"), asm);
        Assert.Contains("CALL\tf24cmp", asm);
    }

    [Theory]
    [InlineData("F = I + G;")]
    [InlineData("F = I + (G = 1.5);")]
    [InlineData("F = I - (G + 1);")]
    public void IntLeft_FloatVariableRight_SameAsBefore(string body)
    {
        // 推定が当たっていた形: 変換は今までどおり左の直後 (同じ命令)
        var asm = MainAsm($"VAR I; FLOAT F; FLOAT G; MAIN() BEGIN {body} END;");
        Assert.Contains(Lines("LD\tHL,(_V_I)", "CALL\ti16tof24"), asm);
    }

    [Fact]
    public void LocalInitializer_ConvertsToVariableType()
    {
        var asm = MainAsm("MAIN() BEGIN VAR FLOAT F = 3; VAR I = 1.5; PRINT(I); END;");
        // FLOAT の変数へは整数を FLOAT に変えて 3 バイト書く (今までは指数を書かなかった)
        Assert.Contains(Lines("LD\tHL,$0003", "CALL\ti16tof24", "LD\t(IY+$6D),L", "LD\t(IY+$6E),H", "LD\t(IY+$6F),A"), asm);
        // 整数の変数へは FTOI (今までは FLOAT の仮数を書いていた)
        Assert.Contains(Lines("LD\tHL,$8000", "LD\tA,$3F", "CALL\tFTOI", "LD\t(IY+$6B),L", "LD\t(IY+$6C),H"), asm);
    }

    [Fact]
    public void LocalByteInitializer_WritesTwoBytes()
    {
        // 局所の BYTE は 2 バイトの場所 (読むときも 2 バイト)。代入と同じく 2 バイト書く (今までは下の 1 バイトだけ)
        var asm = MainAsm("MAIN() BEGIN VAR BYTE B = 5; VAR BYTE C = 1.5; PRINT(B, C); END;");
        Assert.Contains(Lines("LD\t(IY+$6E),$05", "LD\t(IY+$6F),$00"), asm);
        Assert.Contains(Lines("CALL\tFTOI", "LD\t(IY+$6C),L", "LD\t(IY+$6D),H"), asm);
    }

    [Fact]
    public void LocalFloatInitializer_FoldsLikeAssignment()
    {
        // FLOAT の局所の初期値は、代入の値と同じく正しい値で畳み込む (2.5)。整数の局所の初期値は今までどおり ((1.0/2.0)*2.0 は 0)
        var asm = MainAsm("MAIN() BEGIN VAR FLOAT G = 5.0 / 2.0; VAR J = (1.0 / 2.0) * 2.0; PRINT(J); END;");
        Assert.Contains(Lines("LD\tHL,$4000", "LD\tA,$40", "LD\t(IY+$6D),L", "LD\t(IY+$6E),H", "LD\t(IY+$6F),A"), asm);
        Assert.Contains(Lines("LD\t(IY+$6B),$00", "LD\t(IY+$6C),$00"), asm);
    }

    [Theory]
    [InlineData("VAR X = 3;")]
    [InlineData("VAR P[] = $C000;")]
    [InlineData("VAR X = 1 ? 2 : 3;")]
    public void IntegerGlobalInitializer_StillAccepted(string decl)
    {
        var (diag, _) = Generate($"{decl} MAIN() BEGIN END;");
        Assert.False(diag.HasErrors, string.Join("; ", diag.Diagnostics));
    }

    [Fact]
    public void GlobalInitializer_DiscardedArmWithAssignment_IsError()
    {
        // 捨てる側の形も、その場で決まる定数だけ (今までは捨てる側が FLOAT のときだけ止まった)
        var (diag, _) = Generate("VAR Y; VAR X = 1 ? 2 : (Y = 1); MAIN() BEGIN END;");
        Assert.Contains(diag.Diagnostics, d => d.Message.Contains("must be an integer constant"));
    }
}
