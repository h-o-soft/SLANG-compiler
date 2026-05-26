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
}
