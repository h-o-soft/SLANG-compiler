using System.Diagnostics;
using Xunit;

namespace SLANGCompiler.Tests;

/// <summary>
/// CLI統合テスト: 実際にdotnet runでコンパイラを実行し、出力ASMを検証
/// </summary>
public class IntegrationTests : IDisposable
{
    private readonly string _tempDir;
    private readonly string _projectRoot;

    public IntegrationTests()
    {
        _tempDir = Path.Combine(Path.GetTempPath(), $"slangtest_{Guid.NewGuid():N}");
        Directory.CreateDirectory(_tempDir);

        // テストプロジェクトからプロジェクトルートを導出
        // tests/SLANGCompiler.Tests/ → ../../
        _projectRoot = Path.GetFullPath(Path.Combine(
            AppContext.BaseDirectory, "..", "..", "..", "..", ".."));
    }

    public void Dispose()
    {
        if (Directory.Exists(_tempDir))
            Directory.Delete(_tempDir, true);
    }

    private string CompileWithCli(string source, string env = "lsx")
    {
        var inputPath = Path.Combine(_tempDir, "test.sl");
        var outputPath = Path.Combine(_tempDir, "test.asm");
        File.WriteAllText(inputPath, source);

        var cliProject = Path.Combine(_projectRoot, "src", "SLANGCompiler.CLI", "SLANGCompiler.CLI.csproj");

        var psi = new ProcessStartInfo
        {
            FileName = "dotnet",
            Arguments = $"run --project \"{cliProject}\" -c Release -- -E {env} -o \"{outputPath}\" \"{inputPath}\"",
            WorkingDirectory = _projectRoot,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
        };

        using var proc = Process.Start(psi)!;
        var stderr = proc.StandardError.ReadToEnd();
        proc.WaitForExit(30000);

        Assert.Equal(0, proc.ExitCode);
        Assert.True(File.Exists(outputPath), $"Output file not created. stderr: {stderr}");
        return File.ReadAllText(outputPath);
    }

    /// <summary>
    /// オーバーレイ付きコンパイル。メインASMとオーバーレイASMを両方返す。
    /// </summary>
    private (string MainAsm, Dictionary<string, string> Overlays) CompileWithOverlays(string source, string env = "lsx")
    {
        var inputPath = Path.Combine(_tempDir, "test.sl");
        var outputPath = Path.Combine(_tempDir, "test.asm");
        File.WriteAllText(inputPath, source);

        var cliProject = Path.Combine(_projectRoot, "src", "SLANGCompiler.CLI", "SLANGCompiler.CLI.csproj");

        var psi = new ProcessStartInfo
        {
            FileName = "dotnet",
            Arguments = $"run --project \"{cliProject}\" -c Release -- -E {env} -o \"{outputPath}\" \"{inputPath}\"",
            WorkingDirectory = _projectRoot,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
        };

        using var proc = Process.Start(psi)!;
        var stderr = proc.StandardError.ReadToEnd();
        proc.WaitForExit(30000);

        Assert.Equal(0, proc.ExitCode);
        Assert.True(File.Exists(outputPath), $"Output file not created. stderr: {stderr}");

        var mainAsm = File.ReadAllText(outputPath);
        var overlays = new Dictionary<string, string>();

        // オーバーレイファイルを収集 (test._m1.ASM, test._m2.ASM, ...)
        foreach (var file in Directory.GetFiles(_tempDir, "test._m*.ASM"))
        {
            overlays[Path.GetFileNameWithoutExtension(file)] = File.ReadAllText(file);
        }

        return (mainAsm, overlays);
    }

    [Fact]
    public void LsxEnvironment_HasSLANGINIT()
    {
        var asm = CompileWithCli("MAIN() BEGIN END;");
        Assert.Contains("LD HL,($0001)", asm);      // WBOOTBK save
        Assert.Contains("WORK ZERO CLEAR", asm);     // Work init
        Assert.Contains("CALL RUNTIME_INIT", asm);   // Runtime init
        Assert.Contains("JP 0", asm);                // CP/M warm boot
    }

    [Fact]
    public void LsxEnvironment_HasEnvType()
    {
        var asm = CompileWithCli("MAIN() BEGIN END;");
        Assert.Contains("ENV_TYPE EQU 0", asm);
        Assert.Contains("OS_TYPE EQU 0", asm);
    }

    [Fact]
    public void LsxEnvironment_HasWorkLayout()
    {
        var asm = CompileWithCli("VAR X; MAIN() BEGIN END;");
        Assert.Contains("__WORK__:", asm);
        Assert.Contains("__WORKEND__", asm);
        Assert.Contains("__IYWORK EQU (__WORK__", asm);
    }

    [Fact]
    public void LsxEnvironment_HasRuntimeInit()
    {
        var asm = CompileWithCli("MAIN() BEGIN END;");
        Assert.Contains("RUNTIME_INIT:", asm);
    }

    [Fact]
    public void LsxEnvironment_WorkVariables()
    {
        // INPUT/GETL等で使われるsKBFAD等がWORKに含まれること
        var asm = CompileWithCli("MAIN() BEGIN INPUT(); END;");
        Assert.Contains("sKBFAD EQU (__WORK__", asm);
        Assert.Contains("_CARRY EQU (__WORK__", asm);
    }

    [Fact]
    public void LsxEnvironment_RuntimeFunctionsLinked()
    {
        // PRINT文で必要なランタイムが自動リンクされること
        var asm = CompileWithCli("MAIN() BEGIN PRINT(\"Hello\", 42, /); END;");
        Assert.Contains("MPRNT:", asm);  // 文字列表示（インライン方式）
        Assert.Contains("P10:", asm);    // 数値表示
        Assert.Contains("PCRONE:", asm); // 改行
        Assert.Contains("VTOS:", asm);   // 数値→文字列変換
    }

    [Fact]
    public void Overlay_NoSLANGINIT()
    {
        var source = @"
VAR X;
MAIN() BEGIN X=1; END;
#MODULE $8000
OVLFUNC() BEGIN X=2; END;
";
        var (mainAsm, overlays) = CompileWithOverlays(source);

        // メインASMにはSLANGINITの内容が含まれること
        Assert.Contains("LD HL,($0001)", mainAsm);
        Assert.Contains("JP 0", mainAsm);

        // オーバーレイにはSLANGINITが出ないこと
        Assert.True(overlays.Count > 0, "No overlay files generated");
        foreach (var (name, asm) in overlays)
        {
            Assert.DoesNotContain("SLANGINIT", asm);
            Assert.DoesNotContain("<<CALLINITIALIZER>>", asm);
            Assert.DoesNotContain("WORK ZERO CLEAR", asm);
        }
    }

    [Fact]
    public void Overlay_OnlyOwnRuntime()
    {
        // メイン=BEEP, オーバーレイ=PRINT("X") で、ランタイムが分離されること
        var source = @"
MAIN() BEGIN BEEP(); END;
#MODULE $8000
SUB() BEGIN PRINT(""X""); END;
";
        var (mainAsm, overlays) = CompileWithOverlays(source);

        // メインにBEEPのランタイムがあること
        Assert.Contains("BEEP:", mainAsm);

        // オーバーレイにはMPRNTがあるがBEEPは含まれないこと
        Assert.True(overlays.Count > 0, "No overlay files generated");
        foreach (var (name, asm) in overlays)
        {
            Assert.Contains("MPRNT:", asm);
            Assert.DoesNotContain("BEEP:", asm);
        }
    }

    [Fact]
    public void MsxRomEnvironment_TemplateInit()
    {
        var asm = CompileWithCli(
            "VAR X; ARRAY ARI[4]={1,2,3,4,5}; MAIN() BEGIN ARI[0]=99; END;",
            env: "msxrom");
        // ROM環境: テンプレートとLDIRが出力される
        Assert.Contains("__INIT_TEMPLATE:", asm);
        Assert.Contains("__INIT_TEMPLATE_END:", asm);
        Assert.Contains("LD HL,__INIT_TEMPLATE", asm);
        Assert.Contains("LD DE,__WORK__", asm);
        Assert.Contains("LD BC,__INIT_TEMPLATE_END-__INIT_TEMPLATE", asm);
        // 配列はWORK内にEQU配置
        Assert.Contains("_V_ARI EQU (__WORK__", asm);
    }

    [Fact]
    public void LsxEnvironment_ArrayInlineDB()
    {
        // RAM環境: 配列はコード領域にDB直接配置、テンプレートなし
        var asm = CompileWithCli(
            "ARRAY ARI[4]={1,2,3}; MAIN() BEGIN END;");
        Assert.Contains("_V_ARI:", asm);
        Assert.DoesNotContain("__INIT_TEMPLATE", asm);
    }

    // ---- FLOAT比較演算テスト ----

    [Fact]
    public void Float_CmpGt_HalfDirect_UsesFusedJump()
    {
        // halfDirectOps FLOAT: f24add結果(AHL) > 定数(CDE) → fusedCompareJump
        // IF (A*A+B*B) > 4.0 パターン（FMANDEL.SLで問題になったケース）
        var asm = CompileWithCli(@"
            VAR FLOAT A, FLOAT B;
            MAIN() {
                A = 0.6875; B = 0.5;
                IF (A*A+B*B) > 4.0 THEN PRINT(""GT"");
            }");
        // f24cmpが使われること（整数SBCではなく）
        Assert.Contains("f24cmp", asm);
        // 融合ジャンプ: JP C/JP Z パターンが出ること（0/1変換のLD HL,$0000ではなく）
        Assert.DoesNotContain("JR\tNC,$+3", asm);
    }

    [Fact]
    public void Float_CmpGt_Variable_UsesFusedJump()
    {
        // 一般パス: FLOAT変数 > FLOAT定数 → EmitCompareGt経由でf24cmp
        var asm = CompileWithCli(@"
            VAR FLOAT A;
            MAIN() {
                A = 3.0;
                IF A > 2.0 THEN PRINT(""GT"");
            }");
        Assert.Contains("f24cmp", asm);
    }

    [Fact]
    public void Float_CmpEq_ReverseHalfDirect_UsesFusedJump()
    {
        // reverseHalfDirectOps FLOAT: 定数(simple) == f24演算結果(complex)
        // SLANGでは == が等値比較
        var asm = CompileWithCli(@"
            VAR FLOAT X;
            MAIN() {
                X = 0.0;
                IF 1.0 == FCOS(X) THEN PRINT(""EQ"");
            }");
        Assert.Contains("f24cmp", asm);
    }

    [Fact]
    public void Float_CmpNeq_ReverseHalfDirect_UsesFusedJump()
    {
        // reverseHalfDirectOps FLOAT: 定数 <> f24演算結果
        var asm = CompileWithCli(@"
            VAR FLOAT X;
            MAIN() {
                X = 0.0;
                IF 0.0 <> FCOS(X) THEN PRINT(""NEQ"");
            }");
        Assert.Contains("f24cmp", asm);
    }

    [Fact]
    public void Float_LoadFloatConst_ImmediateLoad()
    {
        // FLOAT定数がconstant poolではなく即値ロードされること
        var asm = CompileWithCli(@"
            VAR FLOAT A;
            MAIN() { A = 1.5; }");
        // LoadFloatConst: LD HL,$xxxx; LD A,$xx で即値ロード
        Assert.DoesNotContain("_FC0:", asm);  // constant poolラベルがないこと
    }

    [Fact]
    public void Float_ConstExpr_Compiles()
    {
        // CONST FLOAT式（定数同士の演算）が正常コンパイルされること
        var asm = CompileWithCli(@"
            CONST DEG2RAD = 3.1415926 / 180.0;
            VAR FLOAT R;
            MAIN() { R = DEG2RAD; }");
        Assert.DoesNotContain("error", asm.ToLower());
    }

    [Fact]
    public void Float_HalfDirect_Arithmetic()
    {
        // halfDirectOps FLOAT: f24関数結果 * FLOAT定数 → PUSH/POP不要
        var asm = CompileWithCli(@"
            VAR FLOAT A;
            MAIN() {
                A = 0.5;
                A = FCOS(A) * 9.0;
            }");
        // FCOS後にPUSH AF/PUSH HLがないこと（halfDirectで直接CDE→f24mul）
        var mainSection = asm.Substring(asm.IndexOf("MAIN:"));
        var fcosIdx = mainSection.IndexOf("CALL\tFCOS");
        Assert.True(fcosIdx >= 0, "CALL FCOS not found");
        var afterFcos = mainSection.Substring(fcosIdx, 80);
        Assert.DoesNotContain("PUSH\tAF", afterFcos);
    }
}
