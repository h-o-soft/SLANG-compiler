using System.Diagnostics;
using Xunit;

namespace SLANGCompiler.Tests;

/// <summary>
/// PR-B 二段アセンブル driver (slangbuild) の E2E テスト。
///
/// 検証範囲 (PR-B プランの方針):
///  - slangbuild が main.bin を出すこと (overlay なしの単段フロー)
///  - slangbuild が main.bin + overlay._mN.bin を出すこと (二段フロー)
///  - 二段時に overlay._mN.imports.asm が生成され、必要シンボルだけ filtered
///  - エラー伝搬 (slangc 失敗時 / AILZ80ASM 失敗時の exit code 非ゼロ)
///  - --keep-asm で中間ファイルが残ること
///
/// 注: PR-A の `; @resident shared` 付き runtime での実 shared 検証は PR-C 後。
/// 本テストでは default Local ポリシーで「toolchain が動く」までを保証する。
/// </summary>
public class BuildIntegrationTests : IDisposable
{
    private readonly string _tempDir;
    private readonly string _projectRoot;
    private readonly string _slangcExePath;
    private readonly string _ailz80AsmPath;

    public BuildIntegrationTests()
    {
        _tempDir = Path.Combine(Path.GetTempPath(), $"slangbuild_{Guid.NewGuid():N}");
        Directory.CreateDirectory(_tempDir);
        _projectRoot = Path.GetFullPath(Path.Combine(
            AppContext.BaseDirectory, "..", "..", "..", "..", ".."));

        // CI / dev 共通: PATH 上の旧 slangc を避けるため必ずリポジトリ内の最新 publish 物
        // (なければ自動 publish) を使う
        _slangcExePath = EnsureSlangcExePath();
        // Windows では `AILZ80ASM.exe` が必要 (Codex 指摘)
        var asmName = OperatingSystem.IsWindows() ? "AILZ80ASM.exe" : "AILZ80ASM";
        _ailz80AsmPath = Path.Combine(_projectRoot, "tools", asmName);
    }

    public void Dispose()
    {
        if (Directory.Exists(_tempDir))
        {
            try { Directory.Delete(_tempDir, true); } catch { }
        }
    }

    private string EnsureSlangcExePath()
    {
        // 既存 publish 物があれば再利用 (テスト間で publish 重複を避ける)
        var rid = GetCurrentRid();
        var publishDir = Path.Combine(_projectRoot, "src", "SLANGCompiler.CLI",
                                       "bin", "Release", "net8.0", rid, "publish");
        var exeName = OperatingSystem.IsWindows() ? "slangc.exe" : "slangc";
        var exePath = Path.Combine(publishDir, exeName);
        if (File.Exists(exePath)) return exePath;

        // なければ自動 publish (初回 + CI で必要)
        var psi = new ProcessStartInfo
        {
            FileName = "dotnet",
            WorkingDirectory = _projectRoot,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
        };
        psi.ArgumentList.Add("publish");
        psi.ArgumentList.Add("src/SLANGCompiler.CLI/SLANGCompiler.CLI.csproj");
        psi.ArgumentList.Add("-c");
        psi.ArgumentList.Add("Release");
        psi.ArgumentList.Add("-r");
        psi.ArgumentList.Add(rid);
        psi.ArgumentList.Add("--self-contained");
        psi.ArgumentList.Add("true");
        psi.ArgumentList.Add("-p:PublishSingleFile=true");
        using var proc = Process.Start(psi)!;
        proc.WaitForExit(180_000);
        Assert.Equal(0, proc.ExitCode);
        Assert.True(File.Exists(exePath), $"slangc publish output not found: {exePath}");
        return exePath;
    }

    private static string GetCurrentRid()
    {
        // 簡易: macOS arm64 / x64, linux x64, windows x64 のみ対応 (CI 用途では十分)
        if (OperatingSystem.IsMacOS())
            return System.Runtime.InteropServices.RuntimeInformation.OSArchitecture
                == System.Runtime.InteropServices.Architecture.Arm64 ? "osx-arm64" : "osx-x64";
        if (OperatingSystem.IsLinux()) return "linux-x64";
        if (OperatingSystem.IsWindows()) return "win-x64";
        throw new PlatformNotSupportedException();
    }

    private (int ExitCode, string Stdout, string Stderr) RunSlangbuild(
        string inputPath, params string[] extraArgs)
    {
        var buildProject = Path.Combine(_projectRoot, "src", "SLANGCompiler.Build", "SLANGCompiler.Build.csproj");
        var psi = new ProcessStartInfo
        {
            FileName = "dotnet",
            WorkingDirectory = _tempDir,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
        };
        psi.ArgumentList.Add("run");
        psi.ArgumentList.Add("--project");
        psi.ArgumentList.Add(buildProject);
        psi.ArgumentList.Add("-c");
        psi.ArgumentList.Add("Release");
        psi.ArgumentList.Add("--");
        psi.ArgumentList.Add(inputPath);
        psi.ArgumentList.Add("--slangc");
        psi.ArgumentList.Add(_slangcExePath);
        psi.ArgumentList.Add("--asm");
        psi.ArgumentList.Add(_ailz80AsmPath);
        foreach (var a in extraArgs) psi.ArgumentList.Add(a);

        using var proc = Process.Start(psi)!;
        var stdout = proc.StandardOutput.ReadToEnd();
        var stderr = proc.StandardError.ReadToEnd();
        proc.WaitForExit(120_000);
        return (proc.ExitCode, stdout, stderr);
    }

    [Fact]
    public void SingleStage_NoOverlay_ProducesMainBin()
    {
        // overlay なしの SL でも単段フローで main.bin が出る
        var slPath = Path.Combine(_tempDir, "single.SL");
        File.WriteAllText(slPath, "MAIN() BEGIN END;\n");

        var (code, _, stderr) = RunSlangbuild(slPath);
        Assert.Equal(0, code);
        Assert.True(File.Exists(Path.Combine(_tempDir, "single.bin")),
            $"main bin not produced. stderr: {stderr}");
        // 中間 ASM はデフォルトで消える
        Assert.False(File.Exists(Path.Combine(_tempDir, "single.ASM")));
    }

    [Fact]
    public void TwoStage_WithOverlay_ProducesMainAndOverlayBins()
    {
        // #MODULE 付き SL → main.bin + overlay._m0.bin
        var slPath = Path.Combine(_tempDir, "twostage.SL");
        File.WriteAllText(slPath, @"
VAR MAIN_VAL=10;
MAIN() BEGIN END;
#MODULE $3000
VAR MVAL;
MYSUB() BEGIN MVAL = MAIN_VAL; END;
");

        var (code, _, stderr) = RunSlangbuild(slPath);
        Assert.Equal(0, code);
        Assert.True(File.Exists(Path.Combine(_tempDir, "twostage.bin")),
            $"main bin not produced. stderr: {stderr}");
        Assert.True(File.Exists(Path.Combine(_tempDir, "twostage._m0.bin")),
            $"overlay bin not produced. stderr: {stderr}");
    }

    [Fact]
    public void TwoStage_KeepAsm_RetainsIntermediateFiles()
    {
        // --keep-asm で main.ASM / .sym / overlay.ASM / imports.asm が残る
        var slPath = Path.Combine(_tempDir, "keep.SL");
        File.WriteAllText(slPath, @"
MAIN() BEGIN END;
#MODULE $3000
MYSUB() BEGIN END;
");

        var (code, _, _) = RunSlangbuild(slPath, "--keep-asm");
        Assert.Equal(0, code);

        Assert.True(File.Exists(Path.Combine(_tempDir, "keep.ASM")));
        Assert.True(File.Exists(Path.Combine(_tempDir, "keep.sym")));
        Assert.True(File.Exists(Path.Combine(_tempDir, "keep._m0.ASM")));
        Assert.True(File.Exists(Path.Combine(_tempDir, "keep._m0.imports.asm")));
        Assert.True(File.Exists(Path.Combine(_tempDir, "keep._m0.bin")));
    }

    [Fact]
    public void TwoStage_FilteredImportsContainsOnlyExternedSymbols()
    {
        // imports.asm が overlay の `; EXTERN` リストに対応するシンボルだけを含む。
        // shared runtime 関数は default Local なので含まれず、main globals のみ
        var slPath = Path.Combine(_tempDir, "filt.SL");
        File.WriteAllText(slPath, @"
VAR MAIN_VAL=10;
MAIN() BEGIN END;
#MODULE $3000
MYSUB() BEGIN MAIN_VAL = 1; END;
");

        var (code, _, stderr) = RunSlangbuild(slPath, "--keep-asm");
        Assert.True(code == 0, $"slangbuild failed (exit {code}). stderr: {stderr}");

        var imports = File.ReadAllText(Path.Combine(_tempDir, "filt._m0.imports.asm"));
        Assert.Contains("_V_MAIN_VAL equ", imports);
        // MPRNT 等の runtime 関数は overlay が呼んでいない + default Local なので含まれない
        Assert.DoesNotContain("MPRNT equ", imports);
    }

    [Fact]
    public void SlangcFailure_PropagatesNonZeroExit()
    {
        // 故意に壊した SL → slangc が失敗 → slangbuild も非ゼロ終了
        var slPath = Path.Combine(_tempDir, "broken.SL");
        File.WriteAllText(slPath, "VAR; MAIN() BEGIN broken_syntax END;\n");

        var (code, _, _) = RunSlangbuild(slPath);
        Assert.NotEqual(0, code);
        Assert.False(File.Exists(Path.Combine(_tempDir, "broken.bin")));
    }

    [Fact]
    public void OutputPrefix_RelativePath_ResolvesAgainstCwd_NotInputDir()
    {
        // Makefile.dist の `-o examples/PROG` 相当: cwd 基準で解決される。
        // 旧実装は inputDir に再結合して `<inputDir>/examples/PROG.bin` を出して
        // しまっていた (Codex 指摘)。修正後は cwd (= _tempDir) 基準で
        // `<tempDir>/sub/PROG.bin` に出る。
        var subDir = Path.Combine(_tempDir, "sub");
        Directory.CreateDirectory(subDir);

        var slPath = Path.Combine(_tempDir, "src.SL");
        File.WriteAllText(slPath, "MAIN() BEGIN END;\n");

        // -o sub/PROG (相対パス) を渡す。cwd は _tempDir なので sub/PROG = <tempDir>/sub/PROG
        var (code, _, stderr) = RunSlangbuild(slPath, "-o", "sub/PROG");
        Assert.True(code == 0, $"slangbuild failed (exit {code}). stderr: {stderr}");

        // 期待: <tempDir>/sub/PROG.bin に出ること
        Assert.True(File.Exists(Path.Combine(subDir, "PROG.bin")),
            $"Expected output at sub/PROG.bin not found");

        // 旧バグでは cwd/sub/sub/PROG.bin など二重ネスト。それが起きないこと
        Assert.False(Directory.Exists(Path.Combine(subDir, "sub")),
            "Output directory should not be nested twice");
    }

    [Fact]
    public void OutputPrefix_AbsolutePath_UsedAsIs()
    {
        var slPath = Path.Combine(_tempDir, "abs.SL");
        File.WriteAllText(slPath, "MAIN() BEGIN END;\n");

        var absPrefix = Path.Combine(_tempDir, "outputs", "myprog");
        Directory.CreateDirectory(Path.GetDirectoryName(absPrefix)!);

        var (code, _, stderr) = RunSlangbuild(slPath, "-o", absPrefix);
        Assert.True(code == 0, $"slangbuild failed (exit {code}). stderr: {stderr}");
        Assert.True(File.Exists(absPrefix + ".bin"),
            $"Expected output at {absPrefix}.bin not found");
    }

    [Fact]
    public void TwoStage_ResidentSharedRuntime_IsResolvedToMainAddress()
    {
        // PR-B の本丸: fixture runtime に @resident shared な BEEP を用意し、
        // `#MODULE RESIDENT` 配下の overlay から BEEP を呼ぶと、overlay.bin の
        // CALL アドレスが main 内の BEEP アドレスを指す (= 二段アセンブルで
        // shared 解決が実際に動く) ことをバイナリ単位で検証する。
        var fixtureDir = Path.Combine(_projectRoot, "tests", "SLANGCompiler.Tests",
                                       "fixtures", "twostage_env");

        var slPath = Path.Combine(_tempDir, "shared.SL");
        File.WriteAllText(slPath, @"
MAIN() BEGIN BEEP(); END;
#MODULE $3000 RESIDENT
MYSUB() BEGIN BEEP(); END;
#END
");

        var (code, _, stderr) = RunSlangbuild(slPath,
            "-E", "twostage",
            "-L", fixtureDir,
            "--keep-asm");
        Assert.True(code == 0, $"slangbuild failed (exit {code}). stderr: {stderr}");

        // main.sym から BEEP のアドレスを取得 (フォーマット: `BEEP equ $XXXX`)
        var sym = File.ReadAllText(Path.Combine(_tempDir, "shared.sym"));
        var match = System.Text.RegularExpressions.Regex.Match(sym,
            @"^BEEP\s+equ\s+\$([0-9A-Fa-f]+)\s*$",
            System.Text.RegularExpressions.RegexOptions.Multiline);
        Assert.True(match.Success, $"main.sym does not contain BEEP equ. content: {sym}");
        var beepAddr = Convert.ToInt32(match.Groups[1].Value, 16);

        // imports.asm に BEEP equ がある
        var imports = File.ReadAllText(Path.Combine(_tempDir, "shared._m0.imports.asm"));
        Assert.Contains($"BEEP equ ${beepAddr:X4}", imports);

        // overlay ASM 内に BEEP の本体 (`BEEP:` ラベル) は出ていない
        // (shared promoted されたので main 側にだけ出る)
        var overlayAsm = File.ReadAllText(Path.Combine(_tempDir, "shared._m0.ASM"));
        Assert.DoesNotMatch(@"^BEEP:\s*$", overlayAsm);

        // overlay.bin の CALL BEEP が main アドレスを指す。
        // overlay は ORG $3000、関数本体: MYSUB → CALL BEEP → RET → ...
        // CALL は CD <lo> <hi> で 3 バイト。最初の `CD` バイトの後ろがアドレス。
        var overlayBin = File.ReadAllBytes(Path.Combine(_tempDir, "shared._m0.bin"));
        int callIdx = Array.IndexOf(overlayBin, (byte)0xCD);
        Assert.True(callIdx >= 0 && callIdx + 2 < overlayBin.Length,
            "Could not find CALL opcode in overlay.bin");
        int callTarget = overlayBin[callIdx + 1] | (overlayBin[callIdx + 2] << 8);
        Assert.Equal(beepAddr, callTarget);
    }

    [Fact]
    public void MissingInputFile_ReportsError()
    {
        var (code, _, stderr) = RunSlangbuild(Path.Combine(_tempDir, "nonexistent.SL"));
        Assert.NotEqual(0, code);
        Assert.Contains("not found", stderr);
    }
}
