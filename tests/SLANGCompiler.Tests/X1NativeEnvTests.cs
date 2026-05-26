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
}
