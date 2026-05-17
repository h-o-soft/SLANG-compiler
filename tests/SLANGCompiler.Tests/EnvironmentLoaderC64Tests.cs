using SLANGCompiler.Runtime;
using Xunit;

namespace SLANGCompiler.Tests;

/// <summary>
/// EnvironmentLoader の C64 (oscar64) backend 関連テスト。
/// backend: oscar_c の YAML 解析と、Z80 / OscarC の排他検証を中心に検証する。
/// 外部 tool (oscar64) 不要なので CI で動く。
/// </summary>
public class EnvironmentLoaderC64Tests : IDisposable
{
    private readonly string _tempDir;
    private readonly string _envDir;
    private readonly string _runtimeDir;

    public EnvironmentLoaderC64Tests()
    {
        _tempDir = Path.Combine(Path.GetTempPath(), $"envloader_c64_{Guid.NewGuid():N}");
        _envDir = Path.Combine(_tempDir, "env");
        _runtimeDir = Path.Combine(_tempDir, "c64");
        Directory.CreateDirectory(_envDir);
        Directory.CreateDirectory(_runtimeDir);
        // 絶対化対象の参照先 (実在しなくても Path.GetFullPath は通るが、テストで
        // 実在の方が意図が明確なので touch しておく)
        File.WriteAllText(Path.Combine(_runtimeDir, "slang_runtime.c"), "/* stub */");
    }

    public void Dispose()
    {
        if (Directory.Exists(_tempDir))
        {
            try { Directory.Delete(_tempDir, true); } catch { }
        }
    }

    private string WriteEnv(string fileName, string yaml)
    {
        var path = Path.Combine(_envDir, fileName);
        File.WriteAllText(path, yaml);
        return path;
    }

    // === 成功ケース ===

    [Fact]
    public void OscarC_MinimalEnv_LoadsWithDefaults()
    {
        var envPath = WriteEnv("c64.env", """
env_type: 7
os_type: 6
backend: oscar_c
output: c_source
c_runtime_files:
  - ../c64/slang_runtime.c
""");

        var config = EnvironmentLoader.Load(envPath);

        Assert.Equal(BackendKind.OscarC, config.Backend);
        Assert.Equal("c_source", config.OutputFormat);
        Assert.Null(config.OscarPath);              // 未指定 → null (slangbuild が PATH 探索)
        Assert.Null(config.OscarMachine);           // null = default "c64" 扱い (caller 側)
        Assert.Null(config.OscarFormat);            // null = default "prg" 扱い (caller 側)
        Assert.Null(config.OscarOptimize);
        Assert.True(config.OscarPetscii);            // default true
        Assert.NotNull(config.CRuntimeFiles);
        Assert.Single(config.CRuntimeFiles!);
        // env file dir 基準で絶対化されること
        Assert.Equal(Path.GetFullPath(Path.Combine(_runtimeDir, "slang_runtime.c")),
                     config.CRuntimeFiles![0]);
    }

    [Fact]
    public void OscarC_AllFields_LoadsCorrectly()
    {
        var envPath = WriteEnv("c64.env", """
env_type: 7
os_type: 6
backend: oscar_c
output: c_source
oscar_path: /opt/oscar64/bin/oscar64
oscar_machine: c128
oscar_format: prg
oscar_optimize: O3
oscar_petscii: false
c_runtime_files:
  - ../c64/slang_runtime.c
c_runtime_includes:
  - ../c64
""");

        var config = EnvironmentLoader.Load(envPath);

        Assert.Equal(BackendKind.OscarC, config.Backend);
        Assert.Equal("/opt/oscar64/bin/oscar64", config.OscarPath);
        Assert.Equal("c128", config.OscarMachine);
        Assert.Equal("prg", config.OscarFormat);
        Assert.Equal("O3", config.OscarOptimize);
        Assert.False(config.OscarPetscii);
        Assert.NotNull(config.CRuntimeIncludes);
        Assert.Single(config.CRuntimeIncludes!);
        Assert.Equal(Path.GetFullPath(_runtimeDir), config.CRuntimeIncludes![0]);
    }

    [Fact]
    public void Z80_NoBackend_DefaultsToZ80()
    {
        var envPath = WriteEnv("z80.env", """
env_type: 0
os_type: 0
default_org: "$100"
libraries:
  - runtime.yml
""");

        var config = EnvironmentLoader.Load(envPath);
        Assert.Equal(BackendKind.Z80, config.Backend);
        Assert.Null(config.OutputFormat);
    }

    [Fact]
    public void Z80_ExplicitBackend_LoadsAsZ80()
    {
        var envPath = WriteEnv("z80.env", """
env_type: 0
os_type: 0
backend: z80
default_org: "$100"
libraries:
  - runtime.yml
""");

        var config = EnvironmentLoader.Load(envPath);
        Assert.Equal(BackendKind.Z80, config.Backend);
    }

    // === Reject ケース (typo / 排他検出) ===

    [Fact]
    public void Reject_OscarC_WithLibraries()
    {
        var envPath = WriteEnv("bad.env", """
backend: oscar_c
output: c_source
libraries:
  - runtime.yml
c_runtime_files:
  - ../c64/slang_runtime.c
""");

        var ex = Assert.Throws<InvalidDataException>(() => EnvironmentLoader.Load(envPath));
        Assert.Contains("libraries", ex.Message);
        Assert.Contains("oscar_c", ex.Message);
    }

    [Fact]
    public void Reject_OscarC_WithDisk()
    {
        var envPath = WriteEnv("bad.env", """
backend: oscar_c
output: c_source
c_runtime_files:
  - ../c64/slang_runtime.c
disk:
  format: d88
  template: ../../images/templates/X.D88
  tool: ndc
  main_name: PROG.COM
  overlay_name: M{index}.BIN
""");

        var ex = Assert.Throws<InvalidDataException>(() => EnvironmentLoader.Load(envPath));
        Assert.Contains("disk", ex.Message);
    }

    [Fact]
    public void Reject_OscarC_WithBinPadSize()
    {
        var envPath = WriteEnv("bad.env", """
backend: oscar_c
output: c_source
bin_pad_size: 16384
c_runtime_files:
  - ../c64/slang_runtime.c
""");

        var ex = Assert.Throws<InvalidDataException>(() => EnvironmentLoader.Load(envPath));
        Assert.Contains("bin_pad_size", ex.Message);
    }

    [Fact]
    public void Reject_OscarC_WithoutOutput()
    {
        var envPath = WriteEnv("bad.env", """
backend: oscar_c
c_runtime_files:
  - ../c64/slang_runtime.c
""");

        var ex = Assert.Throws<InvalidDataException>(() => EnvironmentLoader.Load(envPath));
        Assert.Contains("c_source", ex.Message);
    }

    [Fact]
    public void Reject_OscarC_WithBinOutput()
    {
        var envPath = WriteEnv("bad.env", """
backend: oscar_c
output: bin
c_runtime_files:
  - ../c64/slang_runtime.c
""");

        var ex = Assert.Throws<InvalidDataException>(() => EnvironmentLoader.Load(envPath));
        Assert.Contains("c_source", ex.Message);
    }

    [Fact]
    public void Reject_OscarC_WithoutCRuntimeFiles()
    {
        var envPath = WriteEnv("bad.env", """
backend: oscar_c
output: c_source
""");

        var ex = Assert.Throws<InvalidDataException>(() => EnvironmentLoader.Load(envPath));
        Assert.Contains("c_runtime_files", ex.Message);
    }

    [Fact]
    public void Reject_Z80_WithOscarFields()
    {
        var envPath = WriteEnv("bad.env", """
env_type: 0
os_type: 0
default_org: "$100"
libraries:
  - runtime.yml
oscar_machine: c64
""");

        var ex = Assert.Throws<InvalidDataException>(() => EnvironmentLoader.Load(envPath));
        Assert.Contains("oscar", ex.Message);
    }

    [Fact]
    public void Reject_Z80_WithCSourceOutput()
    {
        var envPath = WriteEnv("bad.env", """
env_type: 0
os_type: 0
default_org: "$100"
output: c_source
""");

        var ex = Assert.Throws<InvalidDataException>(() => EnvironmentLoader.Load(envPath));
        Assert.Contains("c_source", ex.Message);
    }

    [Fact]
    public void Reject_InvalidBackendValue()
    {
        var envPath = WriteEnv("bad.env", """
backend: cc65
output: c_source
""");

        var ex = Assert.Throws<InvalidDataException>(() => EnvironmentLoader.Load(envPath));
        Assert.Contains("backend", ex.Message);
    }

    [Fact]
    public void Reject_InvalidOutputValue()
    {
        var envPath = WriteEnv("bad.env", """
output: xex
""");

        var ex = Assert.Throws<InvalidDataException>(() => EnvironmentLoader.Load(envPath));
        Assert.Contains("output", ex.Message);
    }
}
