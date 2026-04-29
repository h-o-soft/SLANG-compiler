using SLANGCompiler.Runtime;
using Xunit;

namespace SLANGCompiler.Tests;

/// <summary>
/// EnvironmentLoader の YAML deserialize テスト。
/// 特に Phase 3 で追加された <c>disk.system_files</c> (udostool 用 IPL/SUB/SYS)
/// の env file dir 基準 path 絶対化を中心に検証する。外部 tool 不要なので CI で動く。
/// </summary>
public class EnvironmentLoaderTests : IDisposable
{
    private readonly string _tempDir;

    public EnvironmentLoaderTests()
    {
        _tempDir = Path.Combine(Path.GetTempPath(), $"envloader_{Guid.NewGuid():N}");
        Directory.CreateDirectory(_tempDir);
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
        var path = Path.Combine(_tempDir, fileName);
        File.WriteAllText(path, yaml);
        return path;
    }

    [Fact]
    public void DiskSystemFiles_DeserializesAndAbsolutizes()
    {
        // env file dir 基準の相対 path が絶対化されること。
        // env file = <tempDir>/env/foo.env、system_files の path = ../foo/ipl.bin
        // → 絶対化後 = <tempDir>/foo/ipl.bin
        var envDir = Path.Combine(_tempDir, "env");
        Directory.CreateDirectory(envDir);
        Directory.CreateDirectory(Path.Combine(_tempDir, "foo"));

        var envPath = Path.Combine(envDir, "foo.env");
        File.WriteAllText(envPath, """
env_type: 0
os_type: 0
default_org: "$1A00"
libraries:
  - runtime.yml
disk:
  format: d88
  template: ../../images/templates/X.D88
  tool: udostool
  main_name: "$1A00.$$$"
  overlay_name: M{index}.BIN
  system_files:
    - path: ../foo/ipl.bin
      flag: -IPL
    - path: ../foo/subsys.bin
      flag: -SUB
    - path: ../foo/iosys.bin
      flag: -SYS
""");

        var config = EnvironmentLoader.Load(envPath);

        Assert.NotNull(config.Disk);
        Assert.NotNull(config.Disk!.SystemFiles);
        Assert.Equal(3, config.Disk.SystemFiles!.Count);

        // 絶対 path 化 + flag が正しく取れる
        Assert.Equal(Path.GetFullPath(Path.Combine(_tempDir, "foo", "ipl.bin")),
                     config.Disk.SystemFiles[0].Path);
        Assert.Equal("-IPL", config.Disk.SystemFiles[0].Flag);

        Assert.Equal(Path.GetFullPath(Path.Combine(_tempDir, "foo", "subsys.bin")),
                     config.Disk.SystemFiles[1].Path);
        Assert.Equal("-SUB", config.Disk.SystemFiles[1].Flag);

        Assert.Equal(Path.GetFullPath(Path.Combine(_tempDir, "foo", "iosys.bin")),
                     config.Disk.SystemFiles[2].Path);
        Assert.Equal("-SYS", config.Disk.SystemFiles[2].Flag);

        // 順序保証 (= YAML 記述順 = リスト順)
        Assert.Equal(new[] { "-IPL", "-SUB", "-SYS" },
                     config.Disk.SystemFiles.Select(s => s.Flag).ToArray());

        // main_name は literal で取得 ("$1A00.$$$" の頭の $ も保持)
        Assert.Equal("$1A00.$$$", config.Disk.MainName);
    }

    [Fact]
    public void DiskSystemFiles_NullForOtherTools()
    {
        // ndc / hudisk 経路の env (= system_files 無し) では SystemFiles == null。
        // 既存挙動の不変を保証する。
        var envPath = WriteEnv("lsx_like.env", """
env_type: 0
os_type: 0
default_org: "$100"
libraries:
  - runtime.yml
disk:
  format: d88
  template: ../images/templates/X.D88
  tool: ndc
  main_name: PROG.COM
  overlay_name: M{index}.BIN
""");

        var config = EnvironmentLoader.Load(envPath);

        Assert.NotNull(config.Disk);
        Assert.Equal("ndc", config.Disk!.Tool);
        Assert.Null(config.Disk.SystemFiles);
    }

    [Fact]
    public void DiskSystemFiles_AbsentDiskSection_ReturnsNullDisk()
    {
        // disk: セクション自体が無い env (= --emit disk 非対応) では Disk == null。
        var envPath = WriteEnv("no_disk.env", """
env_type: 0
os_type: 0
default_org: "$100"
libraries:
  - runtime.yml
""");

        var config = EnvironmentLoader.Load(envPath);

        Assert.Null(config.Disk);
    }

    [Fact]
    public void DiskSystemFiles_EmptyListBehavesAsNull()
    {
        // YAML で `system_files: []` 空リスト指定時、null 同等の扱い (= count > 0
        // チェックで scan されないこと、絶対化 loop が走らないこと)。
        var envPath = WriteEnv("empty_sys.env", """
env_type: 0
os_type: 0
default_org: "$1A00"
libraries:
  - runtime.yml
disk:
  format: d88
  template: ../images/templates/X.D88
  tool: udostool
  main_name: "$1A00.$$$"
  system_files: []
""");

        var config = EnvironmentLoader.Load(envPath);

        Assert.NotNull(config.Disk);
        // 空 list は実装上 null 同等 (= count > 0 で fall through、SystemFiles 未代入)
        Assert.True(config.Disk!.SystemFiles == null
                    || config.Disk.SystemFiles.Count == 0);
    }

    [Fact]
    public void DiskSystemFiles_PathNormalization_RemovesDotSegments()
    {
        // env file dir 基準で `./xxx/../foo/ipl.bin` のような dotted path が
        // 正規化されて絶対 path に化けること。
        var envDir = Path.Combine(_tempDir, "env");
        Directory.CreateDirectory(envDir);
        Directory.CreateDirectory(Path.Combine(_tempDir, "foo"));

        var envPath = Path.Combine(envDir, "norm.env");
        File.WriteAllText(envPath, """
env_type: 0
os_type: 0
default_org: "$1A00"
libraries:
  - runtime.yml
disk:
  format: d88
  template: ../../images/templates/X.D88
  tool: udostool
  main_name: "$1A00.$$$"
  system_files:
    - path: ./../foo/./ipl.bin
      flag: -IPL
""");

        var config = EnvironmentLoader.Load(envPath);

        Assert.NotNull(config.Disk!.SystemFiles);
        // Path.GetFullPath が ./.. / 余計な ./ を吸収して正規化する
        Assert.Equal(Path.GetFullPath(Path.Combine(_tempDir, "foo", "ipl.bin")),
                     config.Disk.SystemFiles![0].Path);
    }
}
