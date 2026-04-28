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
        // SLANG_HOME を _projectRoot に固定 (= installed `~/.config/SLANG` の古い env
        // を参照しないようにし、新 disk: セクション込みの repo 内 lsx.env を読ませる)
        psi.Environment["SLANG_HOME"] = _projectRoot;
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
