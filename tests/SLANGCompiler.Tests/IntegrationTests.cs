using System.Diagnostics;
using Xunit;
using SLANGCompiler;

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
        var (asm, _) = CompileWithCliDetail(source, env);
        return asm;
    }

    /// <summary>コンパイル結果(ASM)とstderr両方を返す。失敗時は例外送出。</summary>
    private (string Asm, string Stderr) CompileWithCliDetail(string source, string env = "lsx")
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
        return (File.ReadAllText(outputPath), stderr);
    }

    /// <summary>コンパイル失敗を期待する。stderr を返す。</summary>
    private string CompileExpectError(string source, string env = "lsx")
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

        Assert.NotEqual(0, proc.ExitCode);
        return stderr;
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

    // ---- 関数引数/戻り値の型対応テスト ----

    [Fact]
    public void UserFunc_FloatArg_CompilesAndPassesAsThreeBytes()
    {
        // FLOAT引数がIY+offsetに3バイトで格納されること
        var asm = CompileWithCli(@"
            FX:FLOAT(FLOAT X) BEGIN RETURN X * X; END;
            VAR FLOAT R;
            MAIN() BEGIN R = FX(2.5); END;");
        // 呼び出し側: 2.5のFLOAT即値 + FX 呼び出し
        Assert.Contains("CALL\tFX", asm);
        // 関数内: IY+offsetから3バイト(mantissa L/H + exponent A)を読む
        var fxSection = asm.Substring(asm.IndexOf("FX:"));
        Assert.Matches(@"LD\s+L,\(IY\+\$[0-9A-F]+\)\s*\n\s*LD\s+H,\(IY\+\$[0-9A-F]+\)\s*\n\s*LD\s+A,\(IY\+\$[0-9A-F]+\)", fxSection);
    }

    [Fact]
    public void UserFunc_FloatReturn_CallMarksDataSize3()
    {
        // FLOAT戻り値の関数呼び出しで、戻り値が3バイトとしてstoreされること
        var asm = CompileWithCli(@"
            PI:FLOAT() BEGIN RETURN 3.14; END;
            VAR FLOAT R;
            MAIN() BEGIN R = PI(); END;");
        // MAINでPIを呼んだ後、AHLを_V_R+2にも格納すること (FLOAT戻り値)
        var mainSection = asm.Substring(asm.IndexOf("MAIN:"));
        Assert.Contains("CALL\tPI", mainSection);
        Assert.Contains("LD\t(_V_R+2),A", mainSection);
    }

    [Fact]
    public void UserFunc_IntToFloatAutoConversion()
    {
        // 整数引数→FLOAT引数で i16tof24 自動挿入
        var asm = CompileWithCli(@"
            FX:FLOAT(FLOAT X) BEGIN RETURN X; END;
            MAIN() BEGIN FX(3); END;");
        // MAIN内で引数評価時に i16tof24 が呼ばれること
        var mainSection = asm.Substring(asm.IndexOf("MAIN:"));
        Assert.Contains("CALL\ti16tof24", mainSection);
    }

    [Fact]
    public void UserFunc_WordReturnRegression_NoChange()
    {
        // 既存のWORD-only関数が変わらずコンパイルできること(regression)
        var asm = CompileWithCli(@"
            ADD1(WORD X) BEGIN RETURN X + 1; END;
            VAR WORD R;
            MAIN() BEGIN R = ADD1(10); END;");
        // CALL後にAHL書き込みが無いこと (WORD戻り値なので)
        var mainSection = asm.Substring(asm.IndexOf("MAIN:"));
        Assert.Contains("CALL\tADD1", mainSection);
        Assert.DoesNotContain("LD\t(_V_R+2),A", mainSection);
    }

    [Fact]
    public void UserFunc_FloatToWordArg_Error()
    {
        // FLOAT値をWORD引数に渡すとコンパイルエラー
        var stderr = CompileExpectError(@"
            FW(WORD X) BEGIN RETURN X + 1; END;
            MAIN() BEGIN FW(2.5); END;");
        Assert.Contains("Cannot pass FLOAT", stderr);
    }

    [Fact]
    public void UserFunc_ArgCountMismatch_Error()
    {
        // 引数個数不一致でコンパイルエラー
        var stderr = CompileExpectError(@"
            ADD2:FLOAT(FLOAT A, FLOAT B) BEGIN RETURN A + B; END;
            MAIN() BEGIN ADD2(1.0); END;");
        Assert.Contains("expects 2 arguments", stderr);
    }

    [Fact]
    public void UserFunc_FloatReturnFromWord_Error()
    {
        // WORD戻り値関数でFLOATをRETURNするとエラー
        var stderr = CompileExpectError(@"
            BAD(WORD X) BEGIN RETURN 2.5; END;
            MAIN() BEGIN END;");
        Assert.Contains("Cannot return FLOAT", stderr);
    }

    [Fact]
    public void MachineFunc_ReturnTypeSpecifier_Error()
    {
        // MACHINE関数 (FOO:type(N) 形式) で戻り値型を指定するとエラー
        // (現状WORD固定でしか動かないため、誤動作を防ぐため拒否する)
        var stderr = CompileExpectError(@"
            FOO:FLOAT(2);
            MAIN() BEGIN END;");
        Assert.Contains("MACHINE functions", stderr);
    }

    // ==== FLOAT 配列 ====

    [Fact]
    public void FloatArray_WordByteRegression_NoChange()
    {
        // T0: WORD/BYTE 配列は変化しないこと (2バイト/1バイト格納のまま)
        var asm = CompileWithCli(@"
            ARRAY WORD WA[3];
            ARRAY BYTE BA[3];
            MAIN() BEGIN WA[0] = 100; BA[0] = 7; END;");
        var mainSection = asm.Substring(asm.IndexOf("MAIN:"));
        // WORD: LD HL,$0064; LD (_V_WA),HL
        Assert.Contains("LD\t(_V_WA),HL", mainSection);
        Assert.DoesNotContain("(_V_WA+2)", mainSection);
        // BYTE: LD A,$07; LD (_V_BA),A
        Assert.Contains("(_V_BA)", mainSection);
    }

    [Fact]
    public void FloatArray_GlobalConstIndex_Store()
    {
        // T1: グローバル FLOAT 配列への定数インデックス代入 = 3バイト格納 (HL+A)
        var asm = CompileWithCli(@"
            ARRAY FLOAT GA[3];
            MAIN() BEGIN GA[0] = 1.5; GA[1] = 2.5; END;");
        var mainSection = asm.Substring(asm.IndexOf("MAIN:"));
        // GA[0]: _V_GA に mantissa と exponent
        Assert.Contains("LD\t(_V_GA),HL", mainSection);
        Assert.Contains("LD\t(_V_GA+2),A", mainSection);
        // GA[1]: _V_GA+3 (FLOAT ストライド 3)
        Assert.Contains("LD\t(_V_GA+3),HL", mainSection);
        Assert.Contains("LD\t(_V_GA+3+2),A", mainSection);
    }

    [Fact]
    public void FloatArray_GlobalConstIndex_Load()
    {
        // T2: グローバル FLOAT 配列からの定数インデックス読み込み (HL+A 両方)
        var asm = CompileWithCli(@"
            ARRAY FLOAT GA[3];
            VAR FLOAT R;
            MAIN() BEGIN R = GA[1]; END;");
        var mainSection = asm.Substring(asm.IndexOf("MAIN:"));
        Assert.Contains("LD\tHL,(_V_GA+3)", mainSection);
        Assert.Contains("LD\tA,(_V_GA+3+2)", mainSection);
    }

    [Fact]
    public void FloatArray_GlobalDynamicIndex_Store()
    {
        // T3: 動的インデックスでの代入 = IndirStore dataSize=3
        //     PUSH AF + PUSH HL → POP DE + POP AF → LD (HL),E / INC HL / LD (HL),D / INC HL / LD (HL),A
        var asm = CompileWithCli(@"
            ARRAY FLOAT GA[5];
            VAR I;
            MAIN() BEGIN FOR I = 0 TO 4 BEGIN GA[I] = I + 0.5; END; END;");
        var mainSection = asm.Substring(asm.IndexOf("MAIN:"));
        // 動的 IndirStore path で POP AF と INC HL + LD (HL),A が出現
        Assert.Contains("POP\tAF", mainSection);
        Assert.Contains("LD\t(HL),A", mainSection);
    }

    [Fact]
    public void FloatArray_GlobalDynamicIndex_Load()
    {
        // T4: 動的インデックスでの読み込み = IndirLoad dataSize=3
        //     LD E,(HL) / INC HL / LD D,(HL) / INC HL / LD A,(HL) / EX DE,HL
        var asm = CompileWithCli(@"
            ARRAY FLOAT GA[5];
            VAR FLOAT R;
            VAR I;
            MAIN() BEGIN R = GA[I]; END;");
        var mainSection = asm.Substring(asm.IndexOf("MAIN:"));
        // 3バイト読み込みパターン
        Assert.Contains("LD\tE,(HL)", mainSection);
        Assert.Contains("LD\tD,(HL)", mainSection);
        Assert.Contains("LD\tA,(HL)", mainSection);
    }

    [Fact]
    public void FloatArray_LocalConstIndex_Store()
    {
        // T5: ローカル FLOAT 配列への定数インデックス代入
        var asm = CompileWithCli(@"
            FOO() BEGIN VAR I; ARRAY FLOAT LA[3]; LA[1] = 3.5; END;
            MAIN() BEGIN FOO(); END;");
        // IY+offset への 3バイト格納 (mantissa+exponent)
        var fooSection = asm.Substring(asm.IndexOf("FOO:"));
        fooSection = fooSection.Substring(0, fooSection.IndexOf("_FOO_EXIT"));
        // ローカル FLOAT 配列: LD (IY+off),L / LD (IY+off+1),H / LD (IY+off+2),A
        Assert.Matches(@"LD\s+\(IY\+\$[0-9A-F]+\),L", fooSection);
        Assert.Matches(@"LD\s+\(IY\+\$[0-9A-F]+\),H", fooSection);
        Assert.Matches(@"LD\s+\(IY\+\$[0-9A-F]+\),A", fooSection);
    }

    [Fact]
    public void FloatArray_IntToFloat_AutoConversion()
    {
        // T9: 整数→FLOAT 配列への代入で i16tof24 が挿入されること
        var asm = CompileWithCli(@"
            ARRAY FLOAT GA[3];
            MAIN() BEGIN GA[0] = 7; END;");
        var mainSection = asm.Substring(asm.IndexOf("MAIN:"));
        Assert.Contains("CALL\ti16tof24", mainSection);
        Assert.Contains("LD\t(_V_GA),HL", mainSection);
        Assert.Contains("LD\t(_V_GA+2),A", mainSection);
    }

    [Fact]
    public void FloatArray_StaticArray_Store()
    {
        // T12: 関数内 static ARRAY FLOAT (BEGIN 前宣言) のストア
        var asm = CompileWithCli(@"
            FOO() ARRAY FLOAT SA[3]; BEGIN SA[0] = 1.5; SA[1] = 2.5; END;
            MAIN() BEGIN FOO(); END;");
        // 関数内 static は __FOO_SA のようなラベル。3バイト格納が 2 要素分出る
        var fooSection = asm.Substring(asm.IndexOf("FOO:"));
        fooSection = fooSection.Substring(0, fooSection.IndexOf("_FOO_EXIT"));
        Assert.Matches(@"LD\s+\(_V_FOO_SA\),HL", fooSection);
        Assert.Matches(@"LD\s+\(_V_FOO_SA\+2\),A", fooSection);
        Assert.Matches(@"LD\s+\(_V_FOO_SA\+3\),HL", fooSection);
        Assert.Matches(@"LD\s+\(_V_FOO_SA\+3\+2\),A", fooSection);
    }

    // ==== ARRAY FLOAT 初期値付き宣言 ====

    /// <summary>FLOAT 値の f24 バイト列を DB 出力期待文字列 "$XX,$YY,..." に変換</summary>
    private static string ExpectedFloatBytes(params double[] values)
    {
        var allBytes = values.SelectMany(v => LabelUtils.ConvertToF24(v));
        return string.Join(",", allBytes.Select(b => $"${b:X2}"));
    }

    [Fact]
    public void FloatArrayInit_WordArrayRegression_NoChange()
    {
        // T0: 既存 BYTE/WORD CODE ブロック初期値が変わらないこと
        // 1,2,3 は BYTE で 1byte ずつ、%4/%5 は WORD で 2byte ずつ ($04,$00 / $05,$00)
        var asm = CompileWithCli(@"
            ARRAY ARI[5] = {1, 2, 3, %4, %5};
            MAIN() BEGIN END;");
        Assert.Contains("DB\t$01,$02,$03,$04,$00,$05,$00", asm);
    }

    [Fact]
    public void FloatArrayInit_FloatLiterals()
    {
        // T1: グローバル ARRAY FLOAT に FloatLiteral 初期値
        var asm = CompileWithCli(@"
            ARRAY FLOAT FA[3] = {1.5, 2.5, 3.5};
            MAIN() BEGIN END;");
        var expected = ExpectedFloatBytes(1.5, 2.5, 3.5);
        Assert.Contains($"DB\t{expected}", asm);
    }

    [Fact]
    public void FloatArrayInit_IntegerToFloat_AutoConversion()
    {
        // T2: IntegerLiteral が FLOAT に自動変換される
        var asm = CompileWithCli(@"
            ARRAY FLOAT FA[3] = {1, 2, 3};
            MAIN() BEGIN END;");
        var expected = ExpectedFloatBytes(1.0, 2.0, 3.0);
        Assert.Contains($"DB\t{expected}", asm);
    }

    [Fact]
    public void FloatArrayInit_ConstAndExpression()
    {
        // T3: CONST 参照と FLOAT 定数式 (CONST に型指定構文は無いので CONST PI = 3.14)
        var asm = CompileWithCli(@"
            CONST PI = 3.14;
            ARRAY FLOAT FA[2] = {PI, PI / 2.0};
            MAIN() BEGIN END;");
        var expected = ExpectedFloatBytes(3.14, 3.14 / 2.0);
        Assert.Contains($"DB\t{expected}", asm);
    }

    [Fact]
    public void FloatArrayInit_PartialInit_ZeroPadding()
    {
        // T4: 要素数不足分は 0.0 で埋める (3 バイトの 0 = $00,$00,$00)
        var asm = CompileWithCli(@"
            ARRAY FLOAT FA[5] = {1.5, 2.5};
            MAIN() BEGIN END;");
        // 5 要素 (dim=6) × 3 = 18 バイト。1.5 と 2.5 で 6 バイト + 残り 12 バイトの 0
        var head = ExpectedFloatBytes(1.5, 2.5);
        // 残り 12 バイト分の 0 が続く
        var zeros = string.Join(",", Enumerable.Repeat("$00", 12));
        Assert.Contains($"DB\t{head},{zeros}", asm);
    }

    [Fact]
    public void FloatArrayInit_NonConstantExpr_Error()
    {
        // T5: 非定数式 (定義済み変数の参照等) はエラー
        var stderr = CompileExpectError(@"
            VAR X;
            ARRAY FLOAT FA[1] = {X};
            MAIN() BEGIN END;");
        Assert.Contains("FLOAT array initializer must be a constant expression", stderr);
    }

    [Fact]
    public void FloatArrayInit_TopLevelCastExpr_Error()
    {
        // T5b: トップレベル要素として CastExpr (%X 等) を置くのは禁止
        // (BYTE/WORD 要素混在の意図を防ぐため)
        var stderr = CompileExpectError(@"
            ARRAY FLOAT FA[1] = {%5};
            MAIN() BEGIN END;");
        Assert.Contains("Cast expression not allowed in FLOAT array initializer", stderr);
    }

    [Fact]
    public void FloatArrayInit_CastInsideExpression_OK()
    {
        // T5c: 式の内部に CastExpr が含まれていても、結果が定数式として
        // double 評価できれば許容する (混在禁止はトップレベル要素のみが対象)
        var asm = CompileWithCli(@"
            ARRAY FLOAT FA[1] = {(%5) + 1.0};
            MAIN() BEGIN END;");
        var expected = ExpectedFloatBytes(6.0);
        Assert.Contains($"DB\t{expected}", asm);
    }
}
