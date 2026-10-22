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
        Assert.StartsWith("BAS_PSTR,BAS_STMP,BAS_PSTR,BAS_STMP,BAS_PNL,BAS_SSET,BAS_STMP,BAS_SSET,BAS_STMP", text);
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

    [Fact]
    public void Slang_DataTables()
    {
        var sl = Transpile("10 DEFINT A-Z:READ A:RESTORE 20\n20 DATA 7").Output!;
        Assert.Contains("BAS_DATAINIT(H_DATA, H_DLINE);", sl);
        Assert.Contains("ARRAY H_DLINE[] = { 20,0,$FFFF,2 };", sl);
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
        var sl = Transpile(Stage1Program).Output!;
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
                         "-c", "Release", "--", sl, "-E", "x1native", "--asm", asmPath, "-o", Path.Combine(dir, "PROG") })
                psi.ArgumentList.Add(a);
            using var proc = Process.Start(psi)!;
            var so = proc.StandardOutput.ReadToEndAsync();
            var se = proc.StandardError.ReadToEndAsync();
            Assert.True(proc.WaitForExit(180_000), "slangbuild timed out");
            Assert.True(proc.ExitCode == 0, $"slangbuild failed:\n{so.Result}\n{se.Result}");
            var bin = Path.Combine(dir, "PROG.bin");
            Assert.True(File.Exists(bin), "PROG.bin がない");
            // x1native: $1000 から予約領域 $FFE0 の手前に収まる
            Assert.True(0x1000 + new FileInfo(bin).Length < 0xFFE0);
        }
        finally
        {
            try { Directory.Delete(dir, true); } catch { }
        }
    }
}
