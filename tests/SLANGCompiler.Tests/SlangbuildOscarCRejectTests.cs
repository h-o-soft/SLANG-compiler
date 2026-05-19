using SLANGCompiler.Build;
using SLANGCompiler.Runtime;
using Xunit;

namespace SLANGCompiler.Tests;

/// <summary>
/// slangbuild の OscarC backend で disk 系 option (--emit disk / --disk-image /
/// --disk-template) を指定したら early reject されることを確認 (codex review 反映)。
/// silent ignore で .prg だけ生成され disk image は作られない silent wrong 事故を防ぐ。
///
/// Driver.Run を呼ぶには env file 解決等の前段が必要なため、実行可能な最小経路として
/// Options だけ組み立てた Driver instance を作って Run を呼ぶ。env file 解決失敗で
/// exit 1 になっても disk reject の検証は Run 冒頭で先に来るので影響なし。
/// </summary>
public class SlangbuildOscarCRejectTests : IDisposable
{
    private readonly string _tempDir;
    private readonly string _envDir;
    private readonly string _runtimeDir;

    public SlangbuildOscarCRejectTests()
    {
        _tempDir = Path.Combine(Path.GetTempPath(), $"slangbuild_oc_{Guid.NewGuid():N}");
        _envDir = Path.Combine(_tempDir, "env");
        _runtimeDir = Path.Combine(_tempDir, "c64");
        Directory.CreateDirectory(_envDir);
        Directory.CreateDirectory(_runtimeDir);
        File.WriteAllText(Path.Combine(_runtimeDir, "slang_runtime.c"), "/* stub */");

        // 最小 c64 env (= backend: oscar_c で disk reject 検証を Run() で trigger)
        File.WriteAllText(Path.Combine(_envDir, "testc64.env"), """
env_type: 7
os_type: 6
backend: oscar_c
output: c_source
c_runtime_files:
  - ../c64/slang_runtime.c
""");
        // ダミー SLANG ソース (= reject は Run() 冒頭で起きるので中身は何でも良い)
        File.WriteAllText(Path.Combine(_tempDir, "dummy.SL"), "MAIN() {}\n");
    }

    public void Dispose()
    {
        if (Directory.Exists(_tempDir))
        {
            try { Directory.Delete(_tempDir, true); } catch { }
        }
    }

    private int RunWith(Driver.Options opts)
    {
        // stderr capture して console を汚さない
        var prevErr = Console.Error;
        try
        {
            Console.SetError(new StringWriter());
            return new Driver(opts).Run();
        }
        finally
        {
            Console.SetError(prevErr);
        }
    }

    private Driver.Options MakeOptions(string input)
    {
        var opts = new Driver.Options
        {
            InputPath = Path.Combine(_tempDir, input),
            Environment = "testc64",
        };
        opts.LibraryPaths.Add(_envDir);
        return opts;
    }

    [Fact]
    public void OscarC_EmitDisk_Rejected()
    {
        var opts = MakeOptions("dummy.SL");
        opts.EmitMode = "disk";
        Assert.Equal(1, RunWith(opts));
    }

    [Fact]
    public void OscarC_DiskImagePath_Rejected()
    {
        var opts = MakeOptions("dummy.SL");
        opts.EmitMode = "bin";
        opts.DiskImagePath = "/tmp/foo.d88";
        Assert.Equal(1, RunWith(opts));
    }

    [Fact]
    public void OscarC_DiskTemplatePath_Rejected()
    {
        var opts = MakeOptions("dummy.SL");
        opts.EmitMode = "bin";
        opts.DiskTemplatePath = "/tmp/template.d88";
        Assert.Equal(1, RunWith(opts));
    }
}
