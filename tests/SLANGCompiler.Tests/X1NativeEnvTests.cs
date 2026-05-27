using System.Diagnostics;
using System.Text.RegularExpressions;
using SLANGCompiler.Runtime;
using Xunit;

namespace SLANGCompiler.Tests;

/// <summary>
/// x1native env (= OS 非依存 X1 hardware 直接 access、 Phase A) の env load 系
/// テスト。 asm 生成 + LSX 痕跡 grep verify は実 slangc CLI 経由なので manual
/// verification script (= docs/X1.md) に任せ、 ここでは env file の YAML
/// deserialize 基本属性のみ pin する。
/// </summary>
public class X1NativeEnvTests
{
    private static string EnvFilePath()
    {
        // tests/SLANGCompiler.Tests/bin/Debug/net8.0/ から runtime/env/ に到達
        var baseDir = AppContext.BaseDirectory;
        // 7 階層上が repo root (= bin/Debug/net8.0/SLANGCompiler.Tests/) → adjust
        var dir = new DirectoryInfo(baseDir);
        while (dir != null && !File.Exists(Path.Combine(dir.FullName, "runtime", "env", "x1native.env")))
        {
            dir = dir.Parent;
        }
        Assert.NotNull(dir);
        return Path.Combine(dir!.FullName, "runtime", "env", "x1native.env");
    }

    [Fact]
    public void X1NativeEnv_LoadsSuccessfully()
    {
        var config = EnvironmentLoader.Load(EnvFilePath());
        Assert.NotNull(config);
        Assert.Equal("x1native", config.Name);
        // env_type=1 (= X1 hardware identity 維持、 既存 x1.env と同じ)
        Assert.Equal(1, config.EnvType);
        // os_type=4 (= 新規割当、 X1 native / no OS)
        Assert.Equal(4, config.OsType);
    }

    [Fact]
    public void X1NativeEnv_HasX1NativeDefine()
    {
        // SLANG `#IF X1NATIVE` 条件分岐用 define
        var config = EnvironmentLoader.Load(EnvFilePath());
        Assert.NotNull(config.Defines);
        Assert.True(config.Defines!.ContainsKey("X1NATIVE"),
            "x1native env should define `X1NATIVE: 1` for conditional compilation");
        Assert.Equal(1, config.Defines["X1NATIVE"]);
    }

    [Fact]
    public void X1NativeEnv_LibrariesIncludeNativeAsm()
    {
        // libraries 列に native asm 3 つ + libx1_base reuse が含まれる、
        // LSX 系 (liblsx_*) は含まれない (= LSX 切り離しの構造保証)
        var config = EnvironmentLoader.Load(EnvFilePath());
        Assert.Contains("libx1native_base.asm", config.Libraries);
        Assert.Contains("libx1native_input.asm", config.Libraries);
        Assert.Contains("libx1native_print.asm", config.Libraries);
        Assert.Contains("libx1_base.asm", config.Libraries);  // VSYNC reuse
        Assert.DoesNotContain("liblsx_base.asm", config.Libraries);
        Assert.DoesNotContain("liblsx_input.asm", config.Libraries);
        Assert.DoesNotContain("liblsx_print.asm", config.Libraries);
        Assert.DoesNotContain("liblsx_file.asm", config.Libraries);
    }

    [Fact]
    public void X1NativeEnv_DefaultOrgIsX1TapeBootAddress()
    {
        // X1 tape boot 慣習の load address $1000 (= Phase B で tap header と整合)
        var config = EnvironmentLoader.Load(EnvFilePath());
        // env file が "$1000" 文字列で書かれてる場合、 parser が int に変換するか
        // 文字列のままかは EnvironmentLoader 仕様依存、 ここでは property type に合わせる
        Assert.Equal(0x1000, config.DefaultOrg);
    }

    [Fact]
    public void X1NativeEnv_OutputIsBinDefault()
    {
        // Phase A = flat binary のみ (= D88 boot は scope 外、 tap は Phase B)。
        // env file の `output: bin` は EnvironmentLoader で null 正規化 (= bin が
        // default)、 OutputFormat = null で「 cmt / c_source ではない」 を pin。
        var config = EnvironmentLoader.Load(EnvFilePath());
        Assert.Null(config.OutputFormat);
    }

    // 注: 「実 SLANG → asm 生成 + 内容 grep」 系 test は CodeGenerator + RuntimeManager
    // setup の複雑さで scope 外、 docs/X1.md の manual verification 手順で代替。
    // ただし runtime/libx1native_*.asm の構造維持 (= @name annotation / @works 内
    // AT_WIDTH 等) は static file inspect で pin 可能、 以下で push する。

    private static string RuntimeAsmPath(string name)
    {
        var baseDir = AppContext.BaseDirectory;
        var dir = new DirectoryInfo(baseDir);
        while (dir != null && !File.Exists(Path.Combine(dir.FullName, "runtime", name)))
            dir = dir.Parent;
        Assert.NotNull(dir);
        return Path.Combine(dir!.FullName, "runtime", name);
    }

    [Theory]
    [InlineData("INIT_CRTC")]      // CRTC 80/40 mode 切替 helper
    [InlineData("AT_VRCALC")]      // Y*width+X VRAM offset 計算
    [InlineData("clear_screen")]   // text + attribute + kanji 3 plane 初期化
    [InlineData("_C8025L")]        // CRTC PARM 80 col Lo-res table
    [InlineData("_C4025L")]        // CRTC PARM 40 col Lo-res table
    [InlineData("_CRTCD")]         // CRTC 現在設定 work area (= R1 から動的 width 取得)
    public void Libx1NativeBase_DefinesRoutine(string name)
    {
        var content = File.ReadAllText(RuntimeAsmPath("libx1native_base.asm"));
        Assert.Contains($"; @name {name}", content);
    }

    [Theory]
    [InlineData("WIDTH")]          // 40/80 動的切替 public API
    [InlineData("LOCATE")]         // cursor 移動 public API
    [InlineData("SCREEN")]         // char code read public API
    [InlineData("PRMODE")]         // printer mode stub
    public void Libx1NativePrint_DefinesPublicApi(string name)
    {
        var content = File.ReadAllText(RuntimeAsmPath("libx1native_print.asm"));
        Assert.Contains($"; @name {name}", content);
    }

    [Fact]
    public void Libx1NativeBase_DeclaresAtWidthInWorks()
    {
        // sWORK の @works listing に AT_WIDTH:1 (= __WORK__ 内 1 byte BSS) が含まれる
        // (= WIDTH 動的化 / AT_VRCALC が読む現在 column 数 symbol、 既存 X1 系
        //  libx1_print.asm と同名で graphics native 化時に流用可能)
        var content = File.ReadAllText(RuntimeAsmPath("libx1native_base.asm"));
        Assert.Matches(@"; @works .*AT_WIDTH:1", content);
    }

    [Fact]
    public void Libx1NativePrint_SprintCallsAtVrcalcAndAtWidth()
    {
        // sPRINT が wrap 判定で AT_WIDTH を読み、 VRAM offset 計算で AT_VRCALC を
        // call するように更新されたか pin (= plan で hardcode 80 → 動的化を要求)
        var content = File.ReadAllText(RuntimeAsmPath("libx1native_print.asm"));
        Assert.Contains("LD A, (AT_WIDTH)", content);
        Assert.Contains("CALL AT_VRCALC", content);
    }

    [Fact]
    public void ClearScreen_UsesKanjiSelectorViaBit3()
    {
        // Codex review High fix: kanji plane は $10xx ではなく $38xx (= text
        // region 上位 + bit 3 set) 経由でアクセス。 clear_screen 内に OR $38
        // + DB $ED, $71 (= OUT (C), 0 で kanji=0 書込、 Z80 未定義命令) が
        // 存在することで、 plane selector が正しく組み立てられてることを pin。
        // 既存 libx1_print CTRL0C / libx1_sgl KANJI_VRAM_ADRS=$3800 と同戦略。
        var content = File.ReadAllText(RuntimeAsmPath("libx1native_base.asm"));
        Assert.Contains("OR $38", content);
        Assert.Contains("DB $ED, $71", content);
    }

    [Fact]
    public void ClearScreen_DoesNotUseLegacyBit5KanjiManipulation()
    {
        // 旧誤実装で RES 5, B (= $20 attribute → $00) してから SET 4, B
        // (= $00 → $10) で「kanji plane」 を扱おうとしてた pattern が runtime
        // asm から消えてることを pin。 正しい kanji 切替は bit 3 set ($38xx)、
        // memory map 上の $10xx は port-mapped I/O では別 region を指す。
        var content = File.ReadAllText(RuntimeAsmPath("libx1native_base.asm"));
        Assert.DoesNotContain("RES 5, B", content);
    }

    [Fact]
    public void ClearScreen_OuterCounterIsNotA()
    {
        // Codex review 2 巡目 で発覚した致命 bug の再発防止: clear_screen の
        // outer block counter (= 8) を A に置くと、 inner cell loop の
        // `LD A, B; OR $38` で破壊されて 8 回で抜けず周辺 I/O port を long に
        // 叩き続けて画面破壊する。 A 以外 (= 推奨 H、 inner で touch しない reg)
        // を outer counter にする。 pin: clear_screen routine 区間内に
        // `LD H, 8` + `DEC H` が存在 + `LD A, 8` (= 旧 bug pattern) が無い。
        var content = File.ReadAllText(RuntimeAsmPath("libx1native_base.asm"));
        var match = Regex.Match(content, @"; @name clear_screen\b(.*?)(?=; @name |\z)",
            RegexOptions.Singleline);
        Assert.True(match.Success, "clear_screen routine 区間が見つからない");
        var body = match.Groups[1].Value;
        Assert.Contains("LD H, 8", body);
        Assert.Contains("DEC H", body);
        Assert.DoesNotContain("LD A, 8", body);
    }

    // === graphics 系 library reuse 関連 (= libx1_pcg / grp / magic / sgl) ===

    [Fact]
    public void Libx1NativeBase_DefinesX1WorkAlias()
    {
        // graphics 系 (libx1_grp / libx1_pcg 等) が @calls X1WORK で link 上
        // declare する依存を満たす shim alias。 AT_COLORF / _WK1FD0 は libmag
        // (= 後続 PR) で reuse される予定の互換用 DB、 AT_WIDTH は sWORK 内
        // BSS で既に provide 済のため X1WORK alias には含めない。
        var content = File.ReadAllText(RuntimeAsmPath("libx1native_base.asm"));
        Assert.Contains("; @name X1WORK", content);
        Assert.Contains("AT_COLORF: DB", content);
        Assert.Contains("_WK1FD0:", content);
    }

    [Theory]
    [InlineData("libx1_pcg.asm")]    // supported now: PCGDEF (X1GRP / STARS_X1 で使用)
    [InlineData("libx1_grp.asm")]    // supported now: LINE / PAINT / GRPSETUP / GRDISP / GRCLS
    [InlineData("libx1_magic.asm")]  // registered (= 未使用時は selective link で無害)
    [InlineData("libx1_sgl.asm")]    // registered (= 動作確認は psg 整備後の別 PR)
    public void X1NativeEnv_RegistersGraphicsLibraries(string lib)
    {
        var config = EnvironmentLoader.Load(EnvFilePath());
        Assert.Contains(lib, config.Libraries);
    }

    [Fact]
    public void X1NativeEnv_DoesNotRegisterSglLsx()
    {
        // libx1_sgl_lsx は LSX 専用 wrapper、 x1native では使わない (= 境界保持、
        // x1native は libx1_sgl 本体のみ使う)。
        var config = EnvironmentLoader.Load(EnvFilePath());
        Assert.DoesNotContain("libx1_sgl_lsx.asm", config.Libraries);
    }

    [Fact]
    public void Sprint_DoesNotOverwriteAttribute()
    {
        // LSX / S-OS 慣例 + 既存 libx1_print.asm PRT 同様、 sPRINT は attribute
        // plane を上書きしない (= text + kanji=0 のみ書込)。 attribute は SLANGINIT
        // の clear_screen で初期 $07 fill 済、 scroll / CLEAR でも reset されるため
        // 通常表示影響なし。 attribute 上書きすると PCG flag 等 attribute 経由設定
        // (= STARS_X1.SL の PCG 表示等) が消えるため厳禁。
        // pin: sPRINT routine 区間内に attribute 書込 sequence (= RES 4, B
        // + LD A, $07) が無い (= scroll_up / clear_screen 等 他 routine では使用 OK)。
        // sPRINT 区間切り出し: `; @name sPRINT` から sPRINT 最終 `\nRET\n` まで
        // (= sp_do_cr / scroll_up 等 後続 routine は除外、 行頭 RET で区切る)。
        var content = File.ReadAllText(RuntimeAsmPath("libx1native_print.asm"));
        var match = Regex.Match(content, @"; @name sPRINT\b(.*?)\nRET\n",
            RegexOptions.Singleline);
        Assert.True(match.Success, "sPRINT routine 区間が見つからない (= 最終 RET まで)");
        var body = match.Groups[1].Value;
        Assert.DoesNotContain("RES 4, B", body);
        Assert.DoesNotContain("LD A, $07", body);
    }

    [Fact]
    public void Libx1Grp_DefinesGrdispAndGrclsForX1Native()
    {
        // GRDISP / GRCLS は元々 libmag に定義されてたが、 x1native では libmag を
        // 使わないため libx1_grp 側に新規追加 (= ユーザー指示「GRDISP/GRCLS は
        // graphics 責務なので libx1_grp に移すのがスジ」)。 既存 x1 env では
        // last-wins (= dictionary 上書き) で libmag.GRDISP/GRCLS が優先採用、
        // 既存挙動 維持。
        var content = File.ReadAllText(RuntimeAsmPath("libx1_grp.asm"));
        Assert.Contains("; @name GRDISP", content);
        Assert.Contains("; @name GRCLS", content);
    }

    // === CLI spawn build smoke (= 実 slangbuild を Process で起動して exit 0 +
    //     .tap 生成を pin、 user review feedback で「build smoke も入れる」 要求) ===

    private static string RepoRoot()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir != null && !File.Exists(Path.Combine(dir.FullName, "runtime", "env", "x1native.env")))
            dir = dir.Parent;
        Assert.NotNull(dir);
        return dir!.FullName;
    }

    private static (int exitCode, string stdout, string stderr) RunSlangBuild(string args)
    {
        var repoRoot = RepoRoot();
        var slangbuildProj = Path.Combine(repoRoot, "src", "SLANGCompiler.Build", "SLANGCompiler.Build.csproj");
        var psi = new ProcessStartInfo("dotnet", $"run --project \"{slangbuildProj}\" --no-build -- {args}")
        {
            WorkingDirectory = repoRoot,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
        };
        using var proc = Process.Start(psi)!;
        // pipe buffer 詰まり回避: WaitForExit より先に stdout/stderr の非同期 read 開始
        // (= 大量出力 + 同期 ReadToEnd 後置きだと子プロセスが書込ブロックで止まり、
        //  WaitForExit が timeout まで進まなくなる、 Codex review Low 指摘)
        var stdoutTask = proc.StandardOutput.ReadToEndAsync();
        var stderrTask = proc.StandardError.ReadToEndAsync();
        var exited = proc.WaitForExit(120 * 1000);  // 2 min timeout
        if (!exited)
        {
            try { proc.Kill(entireProcessTree: true); } catch { /* 既に exit 済等は無視 */ }
            Assert.Fail("slangbuild process timeout (= 2 min)、 kill 済み");
        }
        return (proc.ExitCode, stdoutTask.Result, stderrTask.Result);
    }

    private static void CleanupBuildArtifacts(string outputBase)
    {
        foreach (var ext in new[] { ".bin", ".tap", ".wav", ".LST", ".ASM" })
            if (File.Exists(outputBase + ext)) File.Delete(outputBase + ext);
    }

    [Fact]
    public void X1GrpSample_BuildsSuccessfully()
    {
        // examples/X1GRP.SL は libx1_grp + libx1_pcg + libx1native 全部を使う最小
        // graphics demo。 GRDISP / GRCLS / LINE / BFILL / PAINT / PCGDEF / WIDTH /
        // LOCATE / PRINT 等 全部 link されて tap 生成成功する pin (= regression
        // 検出価値高、 ユーザー review feedback 反映)。
        var repoRoot = RepoRoot();
        var sample = Path.Combine(repoRoot, "examples", "X1GRP.SL");
        var include = Path.Combine(repoRoot, "include");
        var output = Path.Combine(Path.GetTempPath(), $"X1GRP_test_{Guid.NewGuid():N}");
        try
        {
            var (rc, stdout, stderr) = RunSlangBuild(
                $"-E x1native -I \"{include}\" \"{sample}\" -o \"{output}\" --emit tape");
            Assert.True(rc == 0,
                $"X1GRP build failed exit={rc}\nstdout:\n{stdout}\nstderr:\n{stderr}");
            Assert.True(File.Exists(output + ".tap"), "X1GRP.tap not generated");
        }
        finally
        {
            CleanupBuildArtifacts(output);
        }
    }

    [Fact]
    public void StarsX1Sample_BuildsSuccessfully()
    {
        // examples/STARS_X1.SL は PCGDEF (= libx1_pcg) で星型 PCG 文字定義 +
        // STARS.SL 風 動作。 libx1_pcg + libx1native の最小組合せで動く pin。
        var repoRoot = RepoRoot();
        var sample = Path.Combine(repoRoot, "examples", "STARS_X1.SL");
        var include = Path.Combine(repoRoot, "include");
        var output = Path.Combine(Path.GetTempPath(), $"STARS_X1_test_{Guid.NewGuid():N}");
        try
        {
            var (rc, stdout, stderr) = RunSlangBuild(
                $"-E x1native -I \"{include}\" \"{sample}\" -o \"{output}\" --emit tape");
            Assert.True(rc == 0,
                $"STARS_X1 build failed exit={rc}\nstdout:\n{stdout}\nstderr:\n{stderr}");
            Assert.True(File.Exists(output + ".tap"), "STARS_X1.tap not generated");
        }
        finally
        {
            CleanupBuildArtifacts(output);
        }
    }
}
