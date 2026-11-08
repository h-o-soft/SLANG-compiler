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

    private static IEnumerable<string> Errors(string source)
        => Generate(source).Diag.Diagnostics.Where(d => d.Severity == DiagnosticSeverity.Error).Select(d => d.Message);

    // ==== C2: ランタイムの関数の引数の型 ====

    [Fact]
    public void RuntimeFloatParameter_ConvertsInteger()
    {
        // FSIN(1) は整数の 1 を FLOAT に変えて渡す (今までは HL の 1 をそのまま AHL として渡していた)
        var asm = MainAsm("FLOAT F; MAIN() BEGIN F = FSIN(1); END;");
        Assert.Contains(Lines("LD\tHL,$0001", "CALL\ti16tof24", "CALL\tFSIN"), asm);
    }

    [Fact]
    public void RuntimeFloatParameter_FloatValueUnchanged()
    {
        var asm = MainAsm("FLOAT F; FLOAT G; MAIN() BEGIN F = FSIN(G); END;");
        Assert.Contains(Lines("LD\tHL,(_V_G)", "LD\tA,(_V_G+2)", "CALL\tFSIN"), asm);
        Assert.DoesNotContain("i16tof24", asm);
    }

    [Theory]
    [InlineData("FPOW")]
    [InlineData("FLOGY")]
    public void RuntimeTwoFloatParameters_PassXInAhlAndYInCde(string func)
    {
        var asm = MainAsm($"FLOAT F; FLOAT G; MAIN() BEGIN F = {func}(G, 2); END;");
        Assert.Contains(Lines("LD\tHL,(_V_G)", "LD\tA,(_V_G+2)", "PUSH\tAF", "PUSH\tHL",
            "LD\tHL,$0002", "CALL\ti16tof24", "PUSH\tAF", "PUSH\tHL",
            "POP\tDE", "POP\tAF", "LD\tC,A", "POP\tHL", "POP\tAF", $"CALL\t{func}"), asm);
    }

    [Fact]
    public void Flogy_DividesLogXByLogY()
    {
        // FLOGY は log(x)/log(y)。今までは最後の割り算がなく、次の FLOG に落ちていた
        var flogy = Rt.Functions["FLOGY"].Code;
        Assert.EndsWith("ld c,b\njp f24div", flogy.Replace("\r", "").TrimEnd());
    }

    [Theory]
    [InlineData("F = FSIN(1, 2);", "expects 1 arguments, got 2")]
    [InlineData("F = FPOW(F);", "expects 2 arguments, got 1")]
    [InlineData("F = ITOF(1.0);", "Cannot pass FLOAT value to non-FLOAT parameter")]
    [InlineData("F = UTOF(F);", "Cannot pass FLOAT value to non-FLOAT parameter")]
    public void RuntimeParameter_Errors(string body, string message)
    {
        Assert.Contains(Errors($"FLOAT F; MAIN() BEGIN {body} END;"), m => m.Contains(message));
    }

    [Fact]
    public void UserMachineWithRuntimeName_KeepsItsOwnConvention()
    {
        // 同じ名前の MACHINE を宣言していれば、ランタイムの注釈 (引数・戻り値の FLOAT) を使わない
        var asm = MainAsm("MACHINE FSIN(1); FSIN(1)[CODE($AF,$C9);] MAIN() BEGIN VAR I = FSIN(5); PRINT(I, FSIN(5)); END;");
        Assert.DoesNotContain("i16tof24", asm);
        Assert.DoesNotContain("FTOI", asm);
        Assert.Contains(Lines("LD\tHL,$0005", "CALL\t_FSIN"), asm);
    }

    [Theory]
    [InlineData("F = FSIN(5.0 / 2.0);", "LD\tHL,$4000", "LD\tA,$40", "CALL\tFSIN")]                 // 2.5
    [InlineData("F = FSQRT(20000.0 * 2.0);", "LD\tHL,$3880", "LD\tA,$4E", "CALL\tFSQRT")]          // 40000.0
    [InlineData("F = 20000.0 * 2.0;", "LD\tHL,$3880", "LD\tA,$4E", "LD\t(_V_F),HL")]             // 40000.0 (今までは -25536)
    [InlineData("F = FX(5.0 / 2.0);", "LD\tHL,$4000", "LD\tA,$40", "PUSH\tAF")]                   // ユーザーの関数の FLOAT の引数
    [InlineData("PRINT(FL$(5.0 / 2.0));", "LD\tHL,$4000", "LD\tA,$40", "CALL\tPFLOAT")]
    public void FloatValueContext_FoldsLikeAssignment(string body, params string[] lines)
    {
        var asm = MainAsm($"FLOAT F; FX:FLOAT(FLOAT A) BEGIN RETURN A; END; MAIN() BEGIN {body} END;");
        Assert.Contains(Lines(lines), asm);
    }

    [Fact]
    public void PrintFl_ConvertsInteger()
    {
        var asm = MainAsm("MAIN() BEGIN PRINT(FL$(5)); END;");
        Assert.Contains(Lines("LD\tHL,$0005", "CALL\ti16tof24", "CALL\tPFLOAT"), asm);
    }

    [Theory]
    [InlineData("FX:FLOAT() BEGIN RETURN 5.0 / 2.0; END;")]
    [InlineData("FX:FLOAT() BEGIN END(5.0 / 2.0);")]
    public void FloatReturnValue_FoldsLikeAssignment(string func)
    {
        var asm = Asm($"{func} MAIN() BEGIN END;");
        Assert.Contains(Lines("LD\tHL,$4000", "LD\tA,$40"), asm.Split("FX:")[1]);
    }

    [Theory]
    [InlineData("F = G + (1.0 / 2.0);", "f24add")]   // 右を整数の 0 と見て消さない
    [InlineData("F = G + (1.0 / 3.0);", "f24div")]   // f24 で正確に表せない辺は実行時に計算する
    public void FloatValueContext_NormalizationKeepsFloatSide(string body, string call)
    {
        var asm = MainAsm($"FLOAT F; FLOAT G; MAIN() BEGIN {body} END;");
        Assert.Contains($"CALL\t{call}", asm);
        Assert.Contains("CALL\tf24add", asm);
    }

    [Theory]
    [InlineData("F = G * (1.0 == 1.0);")]   // 比較の辺は今までどおり整数の 1 と見る
    [InlineData("F = G + (2.0 - 2.0);")]    // 整数として正しい辺 (0) は今までどおり消す
    public void FloatValueContext_NormalizationUnchanged(string body)
    {
        var asm = MainAsm($"FLOAT F; FLOAT G; MAIN() BEGIN {body} END;");
        Assert.Contains(Lines("LD\tHL,(_V_G)", "LD\tA,(_V_G+2)", "LD\t(_V_F),HL"), asm);
    }

    [Theory]
    [InlineData("F = 2.0 - 3.0;", "LD\tHL,$FFFF", "CALL\ti16tof24")]           // 整数の -1 を FLOAT に (今までどおり)
    [InlineData("I = 20000.0 * 2.0;", "LD\tHL,$9C40", "LD\t(_V_I),HL")]        // 整数の代入先は今までどおり 40000
    public void IntegralFold_Unchanged(string body, params string[] lines)
    {
        var asm = MainAsm($"VAR I; FLOAT F; MAIN() BEGIN {body} END;");
        Assert.Contains(Lines(lines), asm);
    }

    [Fact]
    public void WholeRuntimeFold_StaysRuntime()
    {
        // 262145.0 は f24 で 262144 になるので、実行時に計算して 0 (畳み込んで 2.0 にしない)
        var asm = MainAsm("FLOAT F; MAIN() BEGIN F = 2.0 * (262145.0 - 262144.0); END;");
        Assert.Contains("CALL\tf24sub", asm);
        Assert.Contains("CALL\tf24mul", asm);
    }

    // ==== C3: FLOAT を受け付けない所 ====

    private const string Decls = "VAR I, W; FLOAT F; FLOAT G; VAR A[3]; FLOAT FA[3]; MACHINE M1(1):$1234; MACHINE M2(2):$1234; ";

    [Theory]
    [InlineData("IF F THEN I = 1;")]
    [InlineData("WHILE F [ I = 1; ]")]
    [InlineData("REPEAT I = 1; UNTIL F;")]
    [InlineData("I = F ? 1 : 2;")]
    [InlineData("I = F && W;")]
    [InlineData("I = W || F;")]
    [InlineData("I = NOT F;")]
    public void FloatCondition_IsError(string body)
    {
        Assert.Contains(Errors($"{Decls}MAIN() BEGIN {body} END;"), m => m.Contains("cannot be used as a condition"));
    }

    [Theory]
    [InlineData("F = F MOD G;")]
    [InlineData("I = F AND 1;")]
    [InlineData("I = F OR 1;")]
    [InlineData("I = F XOR 1;")]
    [InlineData("I = F << 1;")]
    [InlineData("I = F >> 1;")]
    [InlineData("I = F .<. G;")]
    [InlineData("I = CPL F;")]
    [InlineData("I = HIGH F;")]
    [InlineData("I = LOW F;")]
    [InlineData("A[F] = 1;")]
    [InlineData("I = A[F];")]
    [InlineData("MEM[F] = 1;")]
    [InlineData("I = MEM[F];")]
    [InlineData("PORT[F] = 1;")]
    [InlineData("I = PORT[F];")]
    public void FloatInIntegerOperation_IsError(string body)
    {
        Assert.Contains(Errors($"{Decls}MAIN() BEGIN {body} END;"), m => m.Contains("cannot be used in this operation"));
    }

    [Theory]
    [InlineData("PRINT(F);", "cannot be printed directly")]
    [InlineData("PRINT(HEX4$(F));", "cannot be printed directly")]
    [InlineData("FOR F = 1 TO 10 DO I = 1;", "FOR variable must not be FLOAT")]
    [InlineData("FOR I = F TO 10 DO W = 1;", "start of FOR must not be FLOAT")]
    [InlineData("FOR I = 1 TO F DO W = 1;", "end of FOR must not be FLOAT")]
    [InlineData("I = %F;", "type cast does not convert")]
    [InlineData("I = &MEM[F];", "cannot be used as an address")]
    [InlineData("I = &PORT[F];", "cannot be used as an address")]
    [InlineData("I = M2(F, 1);", "only as its single argument")]
    [InlineData("I = M2(F);", "only as its single argument")]
    [InlineData("I = CODE([F]);", "cannot be used in CODE")]
    public void FloatInOtherPlaces_IsError(string body, string message)
    {
        Assert.Contains(Errors($"{Decls}MAIN() BEGIN {body} END;"), m => m.Contains(message));
    }

    [Theory]
    [InlineData("F = F * 2; F = F .*. G; F = F ./. G; F = -F; F = +F;")]
    [InlineData("I = F < G; I = F == 1; I = F <> 0.0; IF F > 0.5 THEN I = 1;")]
    [InlineData("F = W ? F : 1; F = 1; I = F; FA[I] = F; MEM[I] = F; A[I] = F;")]
    [InlineData("F += 0.5; F++; F--; F -= 1; F *= 2; F /= 2;")]
    [InlineData("F = FSIN(1); F = FPOW(F, 2); PRINT(FL$(F), FL$(5)); I = M1(F);")]
    [InlineData("IF 1.0 THEN I = 1; I = A[1.0];")]
    public void AcceptedFloatForms_AreNotErrors(string body)
    {
        var src = $"{Decls}MAIN() BEGIN {body} END;";
        Assert.Empty(Errors(src));
    }

    [Theory]
    [InlineData("FX:FLOAT() BEGIN RETURN 1.5; END; VAR X = 3;")]
    public void IntegerGlobalInitializer_AfterFloat_IsNotError(string decls)
    {
        Assert.Empty(Errors($"{decls} MAIN() BEGIN END;"));
    }
}
