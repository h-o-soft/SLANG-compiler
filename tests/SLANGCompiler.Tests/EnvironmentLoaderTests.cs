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
    public void OutputFormat_DeserializesAndNormalizes()
    {
        // `output: CMT` (大文字) も `cmt` に lowercase 正規化される。
        var envPath = WriteEnv("cmt_upper.env", """
env_type: 5
os_type: 3
default_org: "$C000"
output: CMT
libraries:
  - runtime.yml
""");

        var config = EnvironmentLoader.Load(envPath);
        Assert.Equal("cmt", config.OutputFormat);
    }

    [Fact]
    public void OutputFormat_NullByDefault()
    {
        // `output:` 未指定 env では OutputFormat == null (= bin default)。
        var envPath = WriteEnv("no_output.env", """
env_type: 0
os_type: 0
default_org: "$100"
libraries:
  - runtime.yml
""");

        var config = EnvironmentLoader.Load(envPath);
        Assert.Null(config.OutputFormat);
    }

    [Fact]
    public void OutputFormat_BinNormalizedToNull()
    {
        // `output: bin` は default と同じなので null に正規化 (= 内部表現統一)。
        var envPath = WriteEnv("bin_explicit.env", """
env_type: 0
os_type: 0
default_org: "$100"
output: bin
libraries:
  - runtime.yml
""");

        var config = EnvironmentLoader.Load(envPath);
        Assert.Null(config.OutputFormat);
    }

    [Fact]
    public void OutputFormat_InvalidValueThrows()
    {
        // `output: rom` 等の未知値は InvalidDataException で reject (= typo 早期検出)。
        var envPath = WriteEnv("bad_output.env", """
env_type: 0
os_type: 0
default_org: "$100"
output: rom
libraries:
  - runtime.yml
""");

        var ex = Assert.Throws<InvalidDataException>(() => EnvironmentLoader.Load(envPath));
        Assert.Contains("rom", ex.Message);
        Assert.Contains("bin", ex.Message);
        Assert.Contains("cmt", ex.Message);
    }

    [Fact]
    public void CmtConcat_DeserializesAndAbsolutizes()
    {
        // env file dir 基準の相対 path が絶対化されること。
        // env file = <tempDir>/env/foo.env、cmt_concat の path = ../templates/X.CMT
        // → 絶対化後 = <tempDir>/templates/X.CMT
        var envDir = Path.Combine(_tempDir, "env");
        Directory.CreateDirectory(envDir);
        Directory.CreateDirectory(Path.Combine(_tempDir, "templates"));

        var envPath = Path.Combine(envDir, "cmtconcat.env");
        File.WriteAllText(envPath, """
env_type: 5
os_type: 3
default_org: "$C000"
output: cmt
cmt_concat:
  - ../templates/X.CMT
  - ../templates/Y.CMT
libraries:
  - runtime.yml
""");

        var config = EnvironmentLoader.Load(envPath);

        Assert.NotNull(config.CmtConcat);
        Assert.Equal(2, config.CmtConcat!.Count);
        Assert.Equal(Path.GetFullPath(Path.Combine(_tempDir, "templates", "X.CMT")),
                     config.CmtConcat[0]);
        Assert.Equal(Path.GetFullPath(Path.Combine(_tempDir, "templates", "Y.CMT")),
                     config.CmtConcat[1]);
    }

    [Fact]
    public void CmtConcat_NullByDefault()
    {
        // `cmt_concat:` 未指定 env では CmtConcat == null。
        var envPath = WriteEnv("no_concat.env", """
env_type: 0
os_type: 0
default_org: "$100"
libraries:
  - runtime.yml
""");

        var config = EnvironmentLoader.Load(envPath);
        Assert.Null(config.CmtConcat);
    }

    [Fact]
    public void CmtConcat_RequiresOutputCmt_OtherwiseThrows()
    {
        // `cmt_concat:` 指定 + `output:` 未指定 (= bin default) は壊れた
        // ファイル生成 silent wrong になるので InvalidDataException で reject。
        var envPath = WriteEnv("concat_no_cmt.env", """
env_type: 0
os_type: 0
default_org: "$100"
cmt_concat:
  - somewhere.cmt
libraries:
  - runtime.yml
""");

        var ex = Assert.Throws<InvalidDataException>(() => EnvironmentLoader.Load(envPath));
        Assert.Contains("cmt_concat", ex.Message);
        Assert.Contains("output: cmt", ex.Message);
    }

    [Fact]
    public void CmtAssets_DeserializesAndAbsolutizes()
    {
        // env file dir 基準の相対 path が絶対化されること (= cmt_concat と同じ pattern)。
        var envDir = Path.Combine(_tempDir, "env");
        Directory.CreateDirectory(envDir);
        Directory.CreateDirectory(Path.Combine(_tempDir, "templates"));

        var envPath = Path.Combine(envDir, "assets.env");
        File.WriteAllText(envPath, """
env_type: 5
os_type: 3
default_org: "$C000"
output: cmt
cmt_assets:
  - ../templates/X.CMT
libraries:
  - runtime.yml
""");

        var config = EnvironmentLoader.Load(envPath);

        Assert.NotNull(config.CmtAssets);
        Assert.Single(config.CmtAssets!);
        Assert.Equal(Path.GetFullPath(Path.Combine(_tempDir, "templates", "X.CMT")),
                     config.CmtAssets[0]);
    }

    [Fact]
    public void OverlayName_DeserializesAndPreserves()
    {
        var envPath = WriteEnv("ovname.env", """
env_type: 5
os_type: 3
default_org: "$C000"
output: cmt
overlay_name: "M{index}.BIN"
libraries:
  - runtime.yml
""");

        var config = EnvironmentLoader.Load(envPath);
        Assert.Equal("M{index}.BIN", config.OverlayName);
    }

    [Fact]
    public void OverlayOutputFormat_DeserializesAndNormalizes()
    {
        // bin / cmt 以外は reject される。bin は そのまま、cmt は そのまま。
        var envPath = WriteEnv("ovfmt_bin.env", """
env_type: 5
os_type: 3
default_org: "$C000"
output: cmt
overlay_output_format: BIN
libraries:
  - runtime.yml
""");
        var config = EnvironmentLoader.Load(envPath);
        Assert.Equal("bin", config.OverlayOutputFormat);
    }

    [Fact]
    public void OverlayOutputFormat_InvalidValueThrows()
    {
        var envPath = WriteEnv("ovfmt_bad.env", """
env_type: 5
os_type: 3
default_org: "$C000"
output: cmt
overlay_output_format: rom
libraries:
  - runtime.yml
""");
        var ex = Assert.Throws<InvalidDataException>(() => EnvironmentLoader.Load(envPath));
        Assert.Contains("overlay_output_format", ex.Message);
    }

    [Fact]
    public void CmtAssets_AndCmtConcat_AreMutuallyExclusive()
    {
        // 同 env で両方指定すると build flow が排他なので reject。
        var envPath = WriteEnv("excl.env", """
env_type: 5
os_type: 3
default_org: "$C000"
output: cmt
cmt_concat:
  - x.cmt
cmt_assets:
  - y.cmt
libraries:
  - runtime.yml
""");
        var ex = Assert.Throws<InvalidDataException>(() => EnvironmentLoader.Load(envPath));
        Assert.Contains("cmt_concat", ex.Message);
        Assert.Contains("cmt_assets", ex.Message);
    }

    [Fact]
    public void OverlayName_RequiresIndexPlaceholder()
    {
        // {index} 無しは複数 overlay で全部上書き silent wrong になるので reject。
        var envPath = WriteEnv("noidx.env", """
env_type: 5
os_type: 3
default_org: "$C000"
output: cmt
overlay_name: "M.BIN"
libraries:
  - runtime.yml
""");
        var ex = Assert.Throws<InvalidDataException>(() => EnvironmentLoader.Load(envPath));
        Assert.Contains("{index}", ex.Message);
    }

    [Fact]
    public void OverlayName_RejectsPathSeparatorAndAbsolute()
    {
        // separator 含み (= output dir 外書きの予兆) は reject
        var envPath1 = WriteEnv("sep.env", """
env_type: 5
os_type: 3
default_org: "$C000"
output: cmt
overlay_name: "sub/M{index}.BIN"
libraries:
  - runtime.yml
""");
        Assert.Throws<InvalidDataException>(() => EnvironmentLoader.Load(envPath1));

        // absolute path も reject
        var envPath2 = WriteEnv("abs.env", """
env_type: 5
os_type: 3
default_org: "$C000"
output: cmt
overlay_name: "/etc/M{index}.BIN"
libraries:
  - runtime.yml
""");
        Assert.Throws<InvalidDataException>(() => EnvironmentLoader.Load(envPath2));
    }

    [Fact]
    public void CmtFields_RequireOutputCmt_OtherwiseThrows()
    {
        // cmt_assets / overlay_name / overlay_output_format も
        // output: cmt 必須。1 つでも違反すれば reject。
        var envPath = WriteEnv("nocmt.env", """
env_type: 0
os_type: 0
default_org: "$100"
overlay_name: "M{index}.BIN"
libraries:
  - runtime.yml
""");
        var ex = Assert.Throws<InvalidDataException>(() => EnvironmentLoader.Load(envPath));
        Assert.Contains("output: cmt", ex.Message);
    }

    [Fact]
    public void Defines_DeserializesIntegerValues()
    {
        // env file の defines: が Dictionary<string, int> として読まれること。
        var envPath = WriteEnv("defs.env", """
env_type: 5
os_type: 3
default_org: "$C000"
defines:
  PC8001_SD: 1
  DEBUG_LEVEL: 2
libraries:
  - runtime.yml
""");

        var config = EnvironmentLoader.Load(envPath);

        Assert.NotNull(config.Defines);
        Assert.Equal(2, config.Defines!.Count);
        Assert.Equal(1, config.Defines["PC8001_SD"]);
        Assert.Equal(2, config.Defines["DEBUG_LEVEL"]);
    }

    [Fact]
    public void Defines_NullByDefault()
    {
        // defines: 未指定 env では Defines == null。
        var envPath = WriteEnv("nodefs.env", """
env_type: 0
os_type: 0
default_org: "$100"
libraries:
  - runtime.yml
""");

        var config = EnvironmentLoader.Load(envPath);
        Assert.Null(config.Defines);
    }

    [Fact]
    public void Defines_InvalidName_Throws()
    {
        // 識別子規則 (= ^[A-Za-z_][A-Za-z0-9_]*$) に違反する名前は reject。
        // 数字始まり / 記号 / 空白等の名前は AILZ80ASM の `-dl` でも slangc の
        // Preprocessor でも識別子として扱えないため。
        var envPath = WriteEnv("baddef.env", """
env_type: 5
os_type: 3
default_org: "$C000"
defines:
  "1BAD_NAME": 1
libraries:
  - runtime.yml
""");

        var ex = Assert.Throws<InvalidDataException>(() => EnvironmentLoader.Load(envPath));
        Assert.Contains("defines", ex.Message);
        Assert.Contains("1BAD_NAME", ex.Message);
    }

    [Fact]
    public void BinPadSize_DeserializesIntegerValue()
    {
        var envPath = WriteEnv("padsize.env", """
env_type: 6
os_type: 5
default_org: "$0000"
bin_pad_size: 16384
libraries:
  - runtime.yml
""");

        var config = EnvironmentLoader.Load(envPath);
        Assert.Equal(16384, config.BinPadSize);
    }

    [Fact]
    public void BinPadSize_NullByDefault()
    {
        var envPath = WriteEnv("nopadsize.env", """
env_type: 0
os_type: 0
default_org: "$100"
libraries:
  - runtime.yml
""");

        var config = EnvironmentLoader.Load(envPath);
        Assert.Null(config.BinPadSize);
    }

    [Fact]
    public void BinPadSize_AndOverlayPadAlign_ZeroOrNegativeNormalizesToNull()
    {
        // 0 / 負数で指定すると null 相当 (= padding なし扱い、明示 reject
        // ではなく寛容に 0/負を null と同じ意味として扱う)。
        var envPathZero = WriteEnv("padzero.env", """
env_type: 6
os_type: 5
default_org: "$0000"
bin_pad_size: 0
overlay_pad_align: 0
libraries:
  - runtime.yml
""");
        var configZero = EnvironmentLoader.Load(envPathZero);
        Assert.Null(configZero.BinPadSize);
        Assert.Null(configZero.OverlayPadAlign);

        var envPathNeg = WriteEnv("padneg.env", """
env_type: 6
os_type: 5
default_org: "$0000"
bin_pad_size: -1
overlay_pad_align: -8192
libraries:
  - runtime.yml
""");
        var configNeg = EnvironmentLoader.Load(envPathNeg);
        Assert.Null(configNeg.BinPadSize);
        Assert.Null(configNeg.OverlayPadAlign);
    }

    [Fact]
    public void OverlayPadAlign_DeserializesIntegerValue()
    {
        var envPath = WriteEnv("ovalign.env", """
env_type: 6
os_type: 5
default_org: "$0000"
overlay_pad_align: 8192
libraries:
  - runtime.yml
""");

        var config = EnvironmentLoader.Load(envPath);
        Assert.Equal(8192, config.OverlayPadAlign);
    }

    [Fact]
    public void OverlayPadAlign_NullByDefault()
    {
        var envPath = WriteEnv("noovalign.env", """
env_type: 0
os_type: 0
default_org: "$100"
libraries:
  - runtime.yml
""");

        var config = EnvironmentLoader.Load(envPath);
        Assert.Null(config.OverlayPadAlign);
    }

    [Fact]
    public void BinPadSize_RequiresOutputBin_OtherwiseThrows()
    {
        // output: cmt env で bin_pad_size 指定すると header 込み bin に
        // padding する形になり意味不明なので reject。
        var envPath = WriteEnv("padcmt.env", """
env_type: 5
os_type: 3
default_org: "$C000"
output: cmt
bin_pad_size: 16384
libraries:
  - runtime.yml
""");

        var ex = Assert.Throws<InvalidDataException>(() => EnvironmentLoader.Load(envPath));
        Assert.Contains("bin_pad_size", ex.Message);
        Assert.Contains("output: cmt", ex.Message);
    }

    [Fact]
    public void OverlayPadAlign_RequiresOutputBin_OtherwiseThrows()
    {
        // output: cmt env で overlay_pad_align 指定も同じ理由で reject。
        var envPath = WriteEnv("ovaligncmt.env", """
env_type: 5
os_type: 3
default_org: "$C000"
output: cmt
overlay_pad_align: 8192
libraries:
  - runtime.yml
""");

        var ex = Assert.Throws<InvalidDataException>(() => EnvironmentLoader.Load(envPath));
        Assert.Contains("overlay_pad_align", ex.Message);
        Assert.Contains("output: cmt", ex.Message);
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
