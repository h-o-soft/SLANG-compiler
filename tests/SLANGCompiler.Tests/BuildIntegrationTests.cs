using System.Diagnostics;
using Xunit;
using SkippableFactAttribute = Xunit.SkippableFactAttribute;

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
        => RunSlangbuildWithSlangHome(_projectRoot, inputPath, extraArgs);

    private (int ExitCode, string Stdout, string Stderr) RunSlangbuildWithSlangHome(
        string slangHome, string inputPath, string[] extraArgs)
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
        // SLANG_HOME を override: 既定は _projectRoot (= installed `~/.config/SLANG` の
        // 古い env を参照しないようにし、新 disk: セクション込みの repo 内 lsx.env を
        // 読ませる)。InstalledEnv テスト等は mock install dir を渡す
        psi.Environment["SLANG_HOME"] = slangHome;
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

    /// <summary>repo root の tools/ndc(.exe) パスを返す。EmitDisk テスト用。</summary>
    private string NdcPath()
    {
        var name = OperatingSystem.IsWindows() ? "ndc.exe" : "ndc";
        return Path.Combine(_projectRoot, "tools", name);
    }

    /// <summary>repo root の tools/HuDisk.exe パスを返す。Sos テスト用。
    /// 配布 fork が .NET assembly のみのため OS 問わず .exe 拡張子。</summary>
    private string HudiskPath()
        => Path.Combine(_projectRoot, "tools", "HuDisk.exe");

    /// <summary>HuDisk.exe + (non-Windows なら mono) が揃っているか。
    /// 揃っていなければ sos 系テストは Skip。</summary>
    private bool HudiskAvailable()
    {
        if (!File.Exists(HudiskPath())) return false;
        if (OperatingSystem.IsWindows()) return true;
        // Linux/macOS では mono が PATH にあるか確認
        var path = Environment.GetEnvironmentVariable("PATH");
        if (string.IsNullOrEmpty(path)) return false;
        return path.Split(Path.PathSeparator).Any(d =>
            !string.IsNullOrEmpty(d) && File.Exists(Path.Combine(d, "mono")));
    }

    /// <summary>
    /// 出力 d88 の root entry に <paramref name="entryName"/> が含まれるかを
    /// ndc list (= `ndc &lt;d88&gt; 0`) で検証する。
    /// </summary>
    private bool D88ContainsEntry(string d88, string entryName)
    {
        var psi = new ProcessStartInfo
        {
            FileName = NdcPath(),
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
        };
        psi.ArgumentList.Add(d88);
        psi.ArgumentList.Add("0");
        using var proc = Process.Start(psi)!;
        var stdout = proc.StandardOutput.ReadToEnd();
        proc.WaitForExit(15_000);
        // ndc は entry 行を `<NAME>\t<attrs>\t<size>\t<date>` の形式で出す
        return stdout.Split('\n').Any(line => line.StartsWith(entryName + "\t"));
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

    // ---- PR-B2 prelink E2E (関数 cross-reference) ----

    /// <summary>bin 内に「CALL <expectedAddr>」(`CD lo hi`) バイト列があるかを判定。
    /// 起動コード等の他の CALL に紛れずに「特定のアドレスを指す CALL」の存在だけを
    /// 確認する用途。</summary>
    private static bool BinaryContainsCall(byte[] bin, int expectedAddr)
    {
        byte lo = (byte)(expectedAddr & 0xFF);
        byte hi = (byte)((expectedAddr >> 8) & 0xFF);
        for (int i = 0; i < bin.Length - 2; i++)
        {
            if (bin[i] == 0xCD && bin[i + 1] == lo && bin[i + 2] == hi) return true;
        }
        return false;
    }

    [Fact]
    public void Prelink_MainCallsOverlayFunction_ResolvesToOverlayAddress()
    {
        // main から overlay 内 SLANG 関数 MYSUB を呼ぶ → main.bin の CALL が
        // overlay の MYSUB アドレス ($3000 起点) を指す
        var slPath = Path.Combine(_tempDir, "m2o.SL");
        File.WriteAllText(slPath, @"
MAIN() BEGIN MYSUB(); END;
#MODULE $3000
MYSUB() BEGIN END;
#END
");
        var (code, _, stderr) = RunSlangbuild(slPath, "--keep-asm");
        Assert.True(code == 0, $"slangbuild failed (exit {code}). stderr: {stderr}");

        // imports.asm が prelink モードで生成されている
        var imports = File.ReadAllText(Path.Combine(_tempDir, "m2o.imports.asm"));
        Assert.Contains("MYSUB equ $3000", imports);

        // main.bin に「CALL $3000」(MYSUB を呼ぶ命令) が含まれる
        var mainBin = File.ReadAllBytes(Path.Combine(_tempDir, "m2o.bin"));
        Assert.True(BinaryContainsCall(mainBin, 0x3000),
            "main.bin should contain CALL $3000 (= MYSUB in overlay)");
    }

    [Fact]
    public void Prelink_OverlayCallsMainFunction_ResolvesToMainAddress()
    {
        // overlay から main 内 SLANG 関数 HELPER を呼ぶ → overlay.bin の CALL が
        // main の HELPER アドレスを指す
        var slPath = Path.Combine(_tempDir, "o2m.SL");
        File.WriteAllText(slPath, @"
HELPER() BEGIN END;
MAIN() BEGIN HELPER(); END;
#MODULE $3000
MYSUB() BEGIN HELPER(); END;
#END
");
        var (code, _, stderr) = RunSlangbuild(slPath, "--keep-asm");
        Assert.True(code == 0, $"slangbuild failed (exit {code}). stderr: {stderr}");

        // overlay の imports.asm に HELPER の本物アドレスが入る
        var ovImports = File.ReadAllText(Path.Combine(_tempDir, "o2m._m0.imports.asm"));
        var match = System.Text.RegularExpressions.Regex.Match(ovImports,
            @"HELPER\s+equ\s+\$([0-9A-Fa-f]+)");
        Assert.True(match.Success);
        var helperAddr = Convert.ToInt32(match.Groups[1].Value, 16);

        // overlay.bin に「CALL <helperAddr>」(main 内 HELPER を呼ぶ命令) が含まれる
        var ovBin = File.ReadAllBytes(Path.Combine(_tempDir, "o2m._m0.bin"));
        Assert.True(BinaryContainsCall(ovBin, helperAddr),
            $"overlay.bin should contain CALL ${helperAddr:X4} (= HELPER in main)");
    }

    [Fact]
    public void Prelink_OverlayCallsOtherOverlay_ResolvesAcrossOverlays()
    {
        // overlay 0 から overlay 1 の関数を呼ぶ → overlay 0.bin の CALL が
        // overlay 1 の本物アドレス ($4000 起点) を指す
        var slPath = Path.Combine(_tempDir, "o2o.SL");
        File.WriteAllText(slPath, @"
MAIN() BEGIN END;
#MODULE $3000
M0FUNC() BEGIN M1FUNC(); END;
#END
#MODULE $4000
M1FUNC() BEGIN END;
#END
");
        var (code, _, stderr) = RunSlangbuild(slPath, "--keep-asm");
        Assert.True(code == 0, $"slangbuild failed (exit {code}). stderr: {stderr}");

        // overlay 0 の imports に M1FUNC の本物アドレスが入る
        var imports = File.ReadAllText(Path.Combine(_tempDir, "o2o._m0.imports.asm"));
        Assert.Contains("M1FUNC equ $4000", imports);

        // overlay 0 .bin に「CALL $4000」(M1FUNC を呼ぶ命令) が含まれる
        var ov0Bin = File.ReadAllBytes(Path.Combine(_tempDir, "o2o._m0.bin"));
        Assert.True(BinaryContainsCall(ov0Bin, 0x4000),
            "overlay 0.bin should contain CALL $4000 (= M1FUNC in overlay 1)");
    }

    [Fact]
    public void Prelink_NotTriggered_WhenNoCrossRef()
    {
        // overlay は使うが cross-ref 無し (= overlay 内関数を main から呼ばない、
        // overlay → main も呼ばない) → 単段モード (PR-B 既存パス) で動く。
        // dummy.imports.asm が出ない (= prelink Pass 1 が走らなかった証拠)。
        var slPath = Path.Combine(_tempDir, "trivial.SL");
        File.WriteAllText(slPath, @"
MAIN() BEGIN END;
#MODULE $3000
MYSUB() BEGIN END;
#END
");
        var (code, _, stderr) = RunSlangbuild(slPath, "--keep-asm");
        Assert.True(code == 0, $"slangbuild failed (exit {code}). stderr: {stderr}");

        // prelink モードなら <prefix>.dummy.imports.asm が生成される
        Assert.False(File.Exists(Path.Combine(_tempDir, "trivial.dummy.imports.asm")),
            "Single-stage mode expected; dummy imports should not be produced");
        Assert.False(File.Exists(Path.Combine(_tempDir, "trivial.pass1.sym")),
            "Single-stage mode expected; pass1 sym should not be produced");
    }

    [Fact]
    public void MissingInputFile_ReportsError()
    {
        var (code, _, stderr) = RunSlangbuild(Path.Combine(_tempDir, "nonexistent.SL"));
        Assert.NotEqual(0, code);
        Assert.Contains("not found", stderr);
    }

    // ---- Issue #157 Phase 1: --emit disk ----

    private static string ComputeSha256(string path)
    {
        using var sha = System.Security.Cryptography.SHA256.Create();
        using var s = File.OpenRead(path);
        var bytes = sha.ComputeHash(s);
        return Convert.ToHexString(bytes);
    }

    [Fact]
    public void EmitDisk_LsxEnv_ProducesD88WithMainAndOverlay()
    {
        // overlay 付き SL を `--emit disk` で build → out.d88 内に
        // PROG.COM (main) と M0.BIN (overlay 0) が入る (= disk.main_name /
        // disk.overlay_name で staging copy → ndc P により D88 内 entry 名が
        // 配布物用名前になる)。
        var slPath = Path.Combine(_tempDir, "diskprog.SL");
        File.WriteAllText(slPath, @"
MAIN() BEGIN MYSUB(); END;
#MODULE $3000
MYSUB() BEGIN END;
#END
");
        var diskOut = Path.Combine(_tempDir, "out.d88");
        var (code, _, stderr) = RunSlangbuild(slPath,
            "-E", "lsx",
            "--ndc", NdcPath(),
            "--emit", "disk",
            "--disk-image", diskOut);
        Assert.True(code == 0, $"slangbuild --emit disk failed (exit {code}). stderr: {stderr}");
        Assert.True(File.Exists(diskOut), $"disk image not produced at {diskOut}");
        Assert.True(D88ContainsEntry(diskOut, "PROG.COM"),
            "D88 should contain PROG.COM (main)");
        Assert.True(D88ContainsEntry(diskOut, "M0.BIN"),
            "D88 should contain M0.BIN (overlay 0)");
    }

    [Fact]
    public void EmitDisk_TemplateNotMutated()
    {
        // Codex 重要指摘: template d88 (= images/templates/LSXPROG.D88) を
        // direct mutate してはいけない。build 前後で SHA-256 が一致することを
        // CI で保証する。
        var template = Path.Combine(_projectRoot, "images", "templates", "LSXPROG.D88");
        Assert.True(File.Exists(template), $"template not found: {template}");
        var hashBefore = ComputeSha256(template);

        var slPath = Path.Combine(_tempDir, "tmpl.SL");
        File.WriteAllText(slPath, @"
MAIN() BEGIN MYSUB(); END;
#MODULE $3000
MYSUB() BEGIN END;
#END
");
        var (code, _, stderr) = RunSlangbuild(slPath,
            "-E", "lsx",
            "--ndc", NdcPath(),
            "--emit", "disk",
            "--disk-image", Path.Combine(_tempDir, "tmpl_out.d88"));
        Assert.True(code == 0, $"slangbuild failed (exit {code}). stderr: {stderr}");

        var hashAfter = ComputeSha256(template);
        Assert.Equal(hashBefore, hashAfter);
    }

    [Fact]
    public void EmitDisk_NoDiskConfig_FailsWithError()
    {
        // disk: セクション無しの env で `--emit disk` 指定 → error 終了。
        // fixture env (twostage_env) は disk: が無いので、これを使って検証。
        var fixtureDir = Path.Combine(_projectRoot, "tests", "SLANGCompiler.Tests",
                                       "fixtures", "twostage_env");

        var slPath = Path.Combine(_tempDir, "nodisk.SL");
        File.WriteAllText(slPath, "MAIN() BEGIN END;\n");
        var (code, _, stderr) = RunSlangbuild(slPath,
            "-E", "twostage",
            "-L", fixtureDir,
            "--ndc", NdcPath(),
            "--emit", "disk");
        Assert.NotEqual(0, code);
        Assert.Contains("disk:", stderr);
    }

    [Fact]
    public void EmitDisk_DiskTemplateOverride_UsesCustomPath()
    {
        // --disk-template で env の disk.template を override できる。
        // override 用に pristine template を別 path にコピーして渡す。
        var customTemplate = Path.Combine(_tempDir, "custom_template.D88");
        File.Copy(Path.Combine(_projectRoot, "images", "templates", "LSXPROG.D88"),
                  customTemplate);

        var slPath = Path.Combine(_tempDir, "ovrtmpl.SL");
        File.WriteAllText(slPath, @"
MAIN() BEGIN MYSUB(); END;
#MODULE $3000
MYSUB() BEGIN END;
#END
");
        var diskOut = Path.Combine(_tempDir, "ovrtmpl.d88");
        var (code, _, stderr) = RunSlangbuild(slPath,
            "-E", "lsx",
            "--ndc", NdcPath(),
            "--emit", "disk",
            "--disk-template", customTemplate,
            "--disk-image", diskOut);
        Assert.True(code == 0, $"slangbuild failed (exit {code}). stderr: {stderr}");
        Assert.True(File.Exists(diskOut));
        Assert.True(D88ContainsEntry(diskOut, "PROG.COM"));
        Assert.True(D88ContainsEntry(diskOut, "M0.BIN"));
    }

    [Fact]
    public void EmitDisk_DiskTemplateOverride_RequiresEmitDisk()
    {
        // --disk-template 単独 (= --emit disk なし) → error
        var slPath = Path.Combine(_tempDir, "tplsolo.SL");
        File.WriteAllText(slPath, "MAIN() BEGIN END;\n");
        var (code, _, stderr) = RunSlangbuild(slPath,
            "--disk-template", "/tmp/anything.D88");
        Assert.NotEqual(0, code);
        Assert.Contains("--disk-template requires --emit disk", stderr);
    }

    [Fact]
    public void EmitDisk_DiskTemplateOverride_PreservesCustomTemplate()
    {
        // override 経由でも template の SHA-256 は build 前後で一致する
        // (Codex Phase 1 重要指摘の継承: template direct mutate 禁止)
        var customTemplate = Path.Combine(_tempDir, "preserve.D88");
        File.Copy(Path.Combine(_projectRoot, "images", "templates", "LSXPROG.D88"),
                  customTemplate);
        var hashBefore = ComputeSha256(customTemplate);

        var slPath = Path.Combine(_tempDir, "preserve.SL");
        File.WriteAllText(slPath, @"
MAIN() BEGIN MYSUB(); END;
#MODULE $3000
MYSUB() BEGIN END;
#END
");
        var (code, _, stderr) = RunSlangbuild(slPath,
            "-E", "lsx",
            "--ndc", NdcPath(),
            "--emit", "disk",
            "--disk-template", customTemplate,
            "--disk-image", Path.Combine(_tempDir, "preserve_out.d88"));
        Assert.True(code == 0, $"slangbuild failed (exit {code}). stderr: {stderr}");
        Assert.Equal(hashBefore, ComputeSha256(customTemplate));
    }

    [Fact]
    public void InstalledEnv_TemplateResolves()
    {
        // installed 環境 (= ~/.config/SLANG/runtime/env/lsx.env) でも env file の
        // disk.template (= ../../images/templates/LSXPROG.D88) が install dir 配下の
        // images/templates に解決できることを mock で保証する。
        // → make install で images/ コピーが抜けると即 fail する CI gate。
        var installDir = Path.Combine(_tempDir, "mock_install");
        var runtimeDir = Path.Combine(installDir, "runtime");
        var envDir = Path.Combine(runtimeDir, "env");
        var imagesTemplatesDir = Path.Combine(installDir, "images", "templates");
        Directory.CreateDirectory(envDir);
        Directory.CreateDirectory(imagesTemplatesDir);

        // env file (全 env) と runtime asm を mock install dir に staging
        foreach (var f in Directory.GetFiles(Path.Combine(_projectRoot, "runtime", "env")))
            File.Copy(f, Path.Combine(envDir, Path.GetFileName(f)));
        foreach (var f in Directory.GetFiles(Path.Combine(_projectRoot, "runtime"), "*.asm"))
            File.Copy(f, Path.Combine(runtimeDir, Path.GetFileName(f)));
        // template も install dir 配下に配置 (= make install で images/ コピーされた状態を再現)
        File.Copy(
            Path.Combine(_projectRoot, "images", "templates", "LSXPROG.D88"),
            Path.Combine(imagesTemplatesDir, "LSXPROG.D88"));

        var slPath = Path.Combine(_tempDir, "instchk.SL");
        File.WriteAllText(slPath, @"
MAIN() BEGIN MYSUB(); END;
#MODULE $3000
MYSUB() BEGIN END;
#END
");
        var diskOut = Path.Combine(_tempDir, "instchk.d88");
        // SLANG_HOME = mock install dir、即ち PathResolver は env も template も
        // install dir 配下を引く
        var (code, _, stderr) = RunSlangbuildWithSlangHome(installDir, slPath, new[]
        {
            "-E", "lsx",
            "--ndc", NdcPath(),
            "--emit", "disk",
            "--disk-image", diskOut,
        });
        Assert.True(code == 0, $"slangbuild failed (exit {code}). stderr: {stderr}");
        Assert.True(File.Exists(diskOut),
            $"disk image not produced from installed env. stderr: {stderr}");
        Assert.True(D88ContainsEntry(diskOut, "PROG.COM"));
        Assert.True(D88ContainsEntry(diskOut, "M0.BIN"));
    }

    [SkippableFact]
    public void EmitDisk_SosEnv_ProducesD88WithMain()
    {
        // sos env で HuDisk 経由の D88 生成 (Linux/macOS では mono 経由)。
        // template (= images/templates/SOSPROG.D88) は setup-tools で生成済の
        // 前提。HuDisk.exe + mono が揃っていなければ Skip (= setup-tools 未実行
        // の dev 環境 / CI で適切にハンドル)。
        Skip.IfNot(HudiskAvailable(),
            "sos test requires HuDisk.exe (run `make setup-tools`) + mono on PATH");
        Skip.IfNot(File.Exists(Path.Combine(_projectRoot, "images", "templates", "SOSPROG.D88")),
            "sos test requires images/templates/SOSPROG.D88 (run `make setup-tools`)");

        var slPath = Path.Combine(_tempDir, "sostest.SL");
        File.WriteAllText(slPath, "MAIN() BEGIN END;\n");
        var diskOut = Path.Combine(_tempDir, "sosout.d88");
        var (code, _, stderr) = RunSlangbuild(slPath,
            "-E", "sos",
            "--hudisk", HudiskPath(),
            "--emit", "disk",
            "--disk-image", diskOut);
        Assert.True(code == 0, $"slangbuild --emit disk (sos) failed (exit {code}). stderr: {stderr}");
        Assert.True(File.Exists(diskOut), $"sos disk image not produced at {diskOut}");
        // sos の disk listing は HuDisk で行う必要があるが、ndc では読めない場合
        // も多い (HuDisk fs)。ここでは「ファイル生成」+「テンプレート不変」を
        // 主検証とし、内容詳細は EmitDisk_SosTemplateNotMutated で別途確認
    }

    [SkippableFact]
    public void EmitDisk_SosTemplateNotMutated()
    {
        // sos でも template (images/templates/SOSPROG.D88) は build 前後で
        // SHA-256 一致 (Codex Phase 1 重要指摘の継承)。
        Skip.IfNot(HudiskAvailable(),
            "sos test requires HuDisk.exe + mono");
        var template = Path.Combine(_projectRoot, "images", "templates", "SOSPROG.D88");
        Skip.IfNot(File.Exists(template),
            "sos test requires images/templates/SOSPROG.D88 (run `make setup-tools`)");

        var hashBefore = ComputeSha256(template);

        var slPath = Path.Combine(_tempDir, "sostpl.SL");
        File.WriteAllText(slPath, "MAIN() BEGIN END;\n");
        var (code, _, stderr) = RunSlangbuild(slPath,
            "-E", "sos",
            "--hudisk", HudiskPath(),
            "--emit", "disk",
            "--disk-image", Path.Combine(_tempDir, "sostpl_out.d88"));
        Assert.True(code == 0, $"slangbuild failed (exit {code}). stderr: {stderr}");

        Assert.Equal(hashBefore, ComputeSha256(template));
    }

    [Fact]
    public void Pc80mk2x_CmtConcat_ProducesConcatenatedCmt()
    {
        // pc80mk2x env (= `output: cmt` + `cmt_concat: [../templates/XBIOS.CMT]`)
        // で slangbuild → 結合済 `<prefix>.cmt` が出ること、bytes が
        // (main.cmt + XBIOS.CMT + overlay._mN.cmt) の concat と一致すること、
        // 結合に消費された overlay._mN.cmt は intermediate cleanup で消える
        // ことを検証する。
        //
        // bytes 比較は **pc80mk2x.env と同じ libraries で `cmt_concat` だけ
        // 無い一時 fixture env** を作って main.cmt を取得 (= pc80mk2 の
        // libraries は違うので pc80mk2 main.cmt とは比較できない、Codex 指摘)。
        var xbiosCmt = Path.Combine(_projectRoot, "runtime", "templates", "XBIOS.CMT");
        Skip.IfNot(File.Exists(xbiosCmt),
            "pc80mk2x test requires runtime/templates/XBIOS.CMT (= obsolete から救出済の bootstrap binary)");

        var slPath = Path.Combine(_tempDir, "p80x_test.SL");
        File.WriteAllText(slPath, @"
MAIN() BEGIN MYSUB(); END;
#MODULE $1000
MYSUB() BEGIN END;
#END
");

        // === pc80mk2x で結合 cmt 取得 ===
        var (codeX, _, stderrX) = RunSlangbuild(slPath, "-E", "pc80mk2x");
        Assert.True(codeX == 0,
            $"slangbuild (pc80mk2x) failed (exit {codeX}). stderr: {stderrX}");

        var concatCmt = Path.Combine(_tempDir, "p80x_test.cmt");
        Assert.True(File.Exists(concatCmt),
            $"concatenated cmt not produced at {concatCmt}");

        // overlay は cleanup で消えていること
        var overlayCmt = Path.Combine(_tempDir, "p80x_test._m0.cmt");
        Assert.False(File.Exists(overlayCmt),
            $"overlay should be cleanup-ed (consumed by concat): {overlayCmt}");

        // === bytes 比較用: 一時 fixture env で cmt_concat だけ無い同等 build ===
        // pc80mk2x.env を読んで、cmt_concat 行だけ抜いた fixture を temp に作り、
        // env 検索 path を temp に向けて再 build。同じ libraries で main.cmt
        // だけが出る (= 結合前の素の main.cmt)。
        var origEnv = File.ReadAllText(
            Path.Combine(_projectRoot, "runtime", "env", "pc80mk2x.env"));
        // cmt_concat: ブロック (= 行 + 次のリスト要素) を除去
        var lines = origEnv.Split('\n');
        var filtered = new List<string>();
        bool skipping = false;
        foreach (var line in lines)
        {
            if (skipping)
            {
                // 続くリスト要素 (`  - ` で始まる行) も skip、それ以外で再開
                if (line.TrimStart().StartsWith("- ")) continue;
                skipping = false;
            }
            if (line.TrimStart().StartsWith("cmt_concat:"))
            {
                skipping = true;
                continue;
            }
            filtered.Add(line);
        }

        // fixture dir 構成: <fixture>/env/p80x_nocat.env + libraries/templates は
        // _projectRoot 側を流用 (= -L で path 追加して resolve 可能)
        var fixtureEnvDir = Path.Combine(_tempDir, "fxenv", "env");
        Directory.CreateDirectory(fixtureEnvDir);
        var fixtureEnvPath = Path.Combine(fixtureEnvDir, "p80x_nocat.env");
        File.WriteAllText(fixtureEnvPath, string.Join("\n", filtered));

        var slPath2 = Path.Combine(_tempDir, "p80x_nocat.SL");
        File.WriteAllText(slPath2, @"
MAIN() BEGIN MYSUB(); END;
#MODULE $1000
MYSUB() BEGIN END;
#END
");

        var (code2, _, stderr2) = RunSlangbuild(slPath2,
            "-E", "p80x_nocat",
            "-L", Path.Combine(_tempDir, "fxenv"));
        Assert.True(code2 == 0,
            $"slangbuild (p80x_nocat fixture) failed (exit {code2}). stderr: {stderr2}");

        var rawMainCmt = Path.Combine(_tempDir, "p80x_nocat.cmt");
        Assert.True(File.Exists(rawMainCmt),
            $"raw main cmt not produced at {rawMainCmt}");
        var rawOverlayCmt = Path.Combine(_tempDir, "p80x_nocat._m0.cmt");
        Assert.True(File.Exists(rawOverlayCmt),
            $"raw overlay cmt not produced at {rawOverlayCmt}");

        // === bytes 比較: concat = raw main + XBIOS + raw overlay ===
        var expected = File.ReadAllBytes(rawMainCmt)
            .Concat(File.ReadAllBytes(xbiosCmt))
            .Concat(File.ReadAllBytes(rawOverlayCmt))
            .ToArray();
        var actual = File.ReadAllBytes(concatCmt);
        Assert.Equal(expected.Length, actual.Length);
        Assert.Equal(expected, actual);
    }

    [Fact]
    public void Pc80mk2xsd_ProducesIndividualFiles()
    {
        // pc80mk2xsd env (= `output: cmt` + `overlay_output_format: bin` +
        // `overlay_name: M{index}.BIN` + `cmt_assets: [../templates/XBIOS.CMT]`)
        // で slangbuild → 結合せず main.cmt + M0.BIN + XBIOS.CMT が output dir
        // に**個別**で揃うこと、overlay は raw binary (= AILZ80ASM `-bin` 出力)
        // で出ること、`<prefix>._m0.cmt` は rename 後存在しないことを検証する。
        var xbiosCmt = Path.Combine(_projectRoot, "runtime", "templates", "XBIOS.CMT");
        Skip.IfNot(File.Exists(xbiosCmt),
            "pc80mk2xsd test requires runtime/templates/XBIOS.CMT");

        var slPath = Path.Combine(_tempDir, "p80sd_test.SL");
        // SL 側 CONST ASM 不要 (= env file の defines: { PC8001_SD: 1 } が
        // slangc / AILZ80ASM の両方に同名定数を自動 inject するので、user は
        // env を選ぶだけで SD 経路が有効化される)。
        File.WriteAllText(slPath, @"
MAIN() BEGIN MYSUB(); END;
#MODULE $1000
MYSUB() BEGIN END;
#END
");

        var (code, _, stderr) = RunSlangbuild(slPath, "-E", "pc80mk2xsd");
        Assert.True(code == 0,
            $"slangbuild (pc80mk2xsd) failed (exit {code}). stderr: {stderr}");

        // main.cmt が出る (CMT 形式)
        var mainCmt = Path.Combine(_tempDir, "p80sd_test.cmt");
        Assert.True(File.Exists(mainCmt),
            $"main cmt not produced at {mainCmt}");

        // M0.BIN が出る (= rename 済 + raw binary)
        var m0Bin = Path.Combine(_tempDir, "M0.BIN");
        Assert.True(File.Exists(m0Bin),
            $"renamed overlay not at {m0Bin}");

        // XBIOS.CMT が output dir にコピーされている
        var xbiosCopied = Path.Combine(_tempDir, "XBIOS.CMT");
        Assert.True(File.Exists(xbiosCopied),
            $"cmt_assets did not copy XBIOS.CMT to {xbiosCopied}");
        // bytes も元と一致 (= File.Copy で改変なし)
        Assert.Equal(File.ReadAllBytes(xbiosCmt), File.ReadAllBytes(xbiosCopied));

        // rename 後 `_m0.cmt` / `_m0.bin` は存在しない (= rename 済)
        Assert.False(File.Exists(Path.Combine(_tempDir, "p80sd_test._m0.cmt")),
            "old overlay path with .cmt should not exist (rename済)");
        Assert.False(File.Exists(Path.Combine(_tempDir, "p80sd_test._m0.bin")),
            "old overlay path with .bin should not exist (rename済)");
    }

    [Fact]
    public void Pc80mk2xsd_DefinesActivateSdBranch_NoUserConstNeeded()
    {
        // pc80mk2xsd env が `defines: { PC8001_SD: 1 }` を持っているので、
        // ユーザー側 SL に `CONST ASM PC8001_SD = 1;` を書かなくても、
        // slangc 側の Preprocessor で `#IF PC8001_SD==1` が SD branch を取る。
        // 同時に slangbuild が AILZ80ASM に `-dl PC8001_SD=1` を pass するので
        // ASM 側 `#IF exists PC8001_SD` も活きる。
        //
        // 検証方法: SL に MAIN から呼ぶ関数名を `#IF` で分岐させ、出力 ASM に
        // SD 側関数 label のみが含まれて CMT 側関数 label は含まれないこと
        // を確認 (--keep-asm で .ASM 残す)。
        var slPath = Path.Combine(_tempDir, "sd_branch.SL");
        File.WriteAllText(slPath, """
MAIN()
BEGIN
#IF PC8001_SD==1
  PROC_SD_MARKER();
#ELSE
  PROC_CMT_MARKER();
#ENDIF
END;
#IF PC8001_SD==1
PROC_SD_MARKER() BEGIN END;
#ELSE
PROC_CMT_MARKER() BEGIN END;
#ENDIF
""");

        var (code, _, stderr) = RunSlangbuild(slPath, "-E", "pc80mk2xsd", "--keep-asm");
        Assert.True(code == 0,
            $"slangbuild (pc80mk2xsd) failed (exit {code}). stderr: {stderr}");

        var asmPath = Path.Combine(_tempDir, "sd_branch.ASM");
        Assert.True(File.Exists(asmPath), $"main ASM not preserved at {asmPath}");
        var asmContent = File.ReadAllText(asmPath);

        Assert.Contains("PROC_SD_MARKER", asmContent);
        Assert.DoesNotContain("PROC_CMT_MARKER", asmContent);
    }

    [Fact]
    public void Pc80mk2x_DefinesAreEnvScoped_CmtBranchKept()
    {
        // pc80mk2x (= CMT 結合 env) は defines: を持たないので、PC8001_SD は
        // 未定義のまま CMT branch が取られる。pc80mk2xsd の defines 機能が
        // pc80mk2x 経路に漏れないことの regression guard。
        var slPath = Path.Combine(_tempDir, "cmt_branch.SL");
        File.WriteAllText(slPath, """
MAIN()
BEGIN
#IF PC8001_SD==1
  PROC_SD_MARKER();
#ELSE
  PROC_CMT_MARKER();
#ENDIF
END;
#IF PC8001_SD==1
PROC_SD_MARKER() BEGIN END;
#ELSE
PROC_CMT_MARKER() BEGIN END;
#ENDIF
""");

        var (code, _, stderr) = RunSlangbuild(slPath, "-E", "pc80mk2x", "--keep-asm");
        Assert.True(code == 0,
            $"slangbuild (pc80mk2x) failed (exit {code}). stderr: {stderr}");

        var asmPath = Path.Combine(_tempDir, "cmt_branch.ASM");
        Assert.True(File.Exists(asmPath));
        var asmContent = File.ReadAllText(asmPath);

        Assert.Contains("PROC_CMT_MARKER", asmContent);
        Assert.DoesNotContain("PROC_SD_MARKER", asmContent);
    }

    [Fact]
    public void CmtConcat_MissingFile_ReturnsFriendlyError()
    {
        // `cmt_concat:` の path が存在しない fixture env で build → exit 1 +
        // `slangbuild: cmt_concat: file not found: ...` (= silent wrong 防止)。
        var fixtureEnvDir = Path.Combine(_tempDir, "missing_env", "env");
        Directory.CreateDirectory(fixtureEnvDir);
        var fixtureEnvPath = Path.Combine(fixtureEnvDir, "missing.env");
        // 既存の lsx libraries を流用 (= 最小 build が通る)、cmt_concat だけ
        // 存在しない path を指す
        File.WriteAllText(fixtureEnvPath, """
env_type: 0
os_type: 0
default_org: "$100"
output: cmt
cmt_concat:
  - nonexistent_xbios.cmt
libraries:
  - runtime.yml
""");

        var slPath = Path.Combine(_tempDir, "missing_test.SL");
        File.WriteAllText(slPath, "MAIN() BEGIN END;\n");

        var (code, _, stderr) = RunSlangbuild(slPath,
            "-E", "missing",
            "-L", Path.Combine(_tempDir, "missing_env"));
        Assert.NotEqual(0, code);
        Assert.Contains("cmt_concat", stderr);
        Assert.Contains("file not found", stderr);
    }

    [Fact]
    public void Pc80mk2_OutputCmt_ProducesCmtNotBin()
    {
        // pc80mk2 env (= `output: cmt`) で slangbuild → `<prefix>.cmt` が出ること、
        // および `.bin` が出ないこと (= Driver の `.bin` hardcode 漏れを CI で検出)。
        // AILZ80ASM の `-cmt -gap 0` 引数は env 経由で自動的に付与される。
        var slPath = Path.Combine(_tempDir, "pc80mk2_smoke.SL");
        File.WriteAllText(slPath, "MAIN() BEGIN END;\n");

        var (code, _, stderr) = RunSlangbuild(slPath, "-E", "pc80mk2");
        Assert.True(code == 0,
            $"slangbuild (pc80mk2) failed (exit {code}). stderr: {stderr}");

        var cmtPath = Path.Combine(_tempDir, "pc80mk2_smoke.cmt");
        var binPath = Path.Combine(_tempDir, "pc80mk2_smoke.bin");
        Assert.True(File.Exists(cmtPath),
            $"main cmt not produced at {cmtPath}. stderr: {stderr}");
        Assert.False(File.Exists(binPath),
            $"unexpected `.bin` produced at {binPath} — `.bin` hardcode regression");
    }

    [Fact]
    public void EmitDisk_DefaultDiskImagePath_DerivesFromOutputPrefix()
    {
        // --disk-image 省略時は <output_prefix>.d88 に出る。
        var slPath = Path.Combine(_tempDir, "defp.SL");
        File.WriteAllText(slPath, @"
MAIN() BEGIN MYSUB(); END;
#MODULE $3000
MYSUB() BEGIN END;
#END
");
        var outBase = Path.Combine(_tempDir, "outdir", "MYPROG");
        Directory.CreateDirectory(Path.GetDirectoryName(outBase)!);
        var (code, _, stderr) = RunSlangbuild(slPath,
            "-E", "lsx",
            "--ndc", NdcPath(),
            "-o", outBase,
            "--emit", "disk");
        Assert.True(code == 0, $"slangbuild failed (exit {code}). stderr: {stderr}");
        Assert.True(File.Exists(outBase + ".d88"),
            $"default disk image not at {outBase}.d88");
    }
}
