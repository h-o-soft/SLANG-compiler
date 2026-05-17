using SLANGCompiler.Build;
using SLANGCompiler.Runtime;
using Xunit;

namespace SLANGCompiler.Tests;

/// <summary>
/// <see cref="OscarInvoker"/> の単体テスト。
/// 引数列の組み立てと binary 解決のみ確認 (= 実際の oscar64 spawn は CI で
/// 動かさない、OS 依存と oscar64 install を前提にしないため)。
/// </summary>
public class OscarInvokerTests : IDisposable
{
    private readonly string _tempDir;

    public OscarInvokerTests()
    {
        _tempDir = Path.Combine(Path.GetTempPath(), $"oscar_inv_{Guid.NewGuid():N}");
        Directory.CreateDirectory(_tempDir);
    }

    public void Dispose()
    {
        if (Directory.Exists(_tempDir))
        {
            try { Directory.Delete(_tempDir, true); } catch { }
        }
    }

    private static EnvironmentConfig MakeC64Env(
        string? machine = null, string? format = null,
        bool petscii = true, string? optimize = null,
        List<string>? runtimeFiles = null, List<string>? includes = null,
        string? oscarPath = null)
    {
        return new EnvironmentConfig
        {
            Name = "c64",
            Backend = BackendKind.OscarC,
            OutputFormat = "c_source",
            OscarMachine = machine,
            OscarFormat = format,
            OscarOptimize = optimize,
            OscarPetscii = petscii,
            OscarPath = oscarPath,
            CRuntimeFiles = runtimeFiles ?? new List<string> { "/run/slang_runtime.c" },
            CRuntimeIncludes = includes,
        };
    }

    // === BuildArgs ===

    [Fact]
    public void BuildArgs_DefaultC64_HasExpectedShape()
    {
        var env = MakeC64Env();
        var args = OscarInvoker.BuildArgs("/in.c", "/out.prg", env);
        Assert.Equal("-tm=c64", args[0]);
        Assert.Equal("-tf=prg", args[1]);
        Assert.Equal("-psci", args[2]);
        // includes は無いので飛ばす
        Assert.Contains("/in.c", args);
        Assert.Contains("/run/slang_runtime.c", args);
        Assert.Equal("-o=/out.prg", args.Last());
    }

    [Fact]
    public void BuildArgs_NoPetscii_OmitsPsciFlag()
    {
        var env = MakeC64Env(petscii: false);
        var args = OscarInvoker.BuildArgs("/in.c", "/out.prg", env);
        Assert.DoesNotContain("-psci", args);
    }

    [Fact]
    public void BuildArgs_CustomMachineAndFormat()
    {
        var env = MakeC64Env(machine: "c128", format: "crt");
        var args = OscarInvoker.BuildArgs("/in.c", "/out.crt", env);
        Assert.Contains("-tm=c128", args);
        Assert.Contains("-tf=crt", args);
    }

    [Fact]
    public void BuildArgs_OptimizePrefixedWithDash()
    {
        var env = MakeC64Env(optimize: "O3");
        var args = OscarInvoker.BuildArgs("/in.c", "/out.prg", env);
        Assert.Contains("-O3", args);
    }

    [Fact]
    public void BuildArgs_IncludesProducedAsDashI()
    {
        var env = MakeC64Env(includes: new List<string> { "/a/inc", "/b/inc" });
        var args = OscarInvoker.BuildArgs("/in.c", "/out.prg", env);
        Assert.Contains("-i=/a/inc", args);
        Assert.Contains("-i=/b/inc", args);
    }

    [Fact]
    public void BuildArgs_RuntimeFilesAppendedAsPositional()
    {
        var env = MakeC64Env(runtimeFiles: new List<string> { "/r1.c", "/r2.c" });
        var args = OscarInvoker.BuildArgs("/in.c", "/out.prg", env);
        var inputIdx = args.IndexOf("/in.c");
        var r1Idx = args.IndexOf("/r1.c");
        var r2Idx = args.IndexOf("/r2.c");
        var outIdx = args.IndexOf("-o=/out.prg");
        Assert.True(inputIdx < r1Idx && r1Idx < r2Idx && r2Idx < outIdx,
            $"Expected order: in.c < r1.c < r2.c < -o=, got {string.Join(" ", args)}");
    }

    [Fact]
    public void BuildArgs_DefaultsWhenMachineFormatNull()
    {
        var env = MakeC64Env(machine: null, format: null);
        var args = OscarInvoker.BuildArgs("/in.c", "/out.prg", env);
        Assert.Contains("-tm=c64", args);
        Assert.Contains("-tf=prg", args);
    }

    // === FindOscarBinary ===

    [Fact]
    public void FindOscarBinary_CliOverride_ExistingPath()
    {
        var dummy = Path.Combine(_tempDir, "oscar_dummy");
        File.WriteAllText(dummy, "");
        var env = MakeC64Env();
        Assert.Equal(dummy, OscarInvoker.FindOscarBinary(dummy, env));
    }

    [Fact]
    public void FindOscarBinary_CliOverride_NonExisting_ReturnsNull()
    {
        var env = MakeC64Env();
        var result = OscarInvoker.FindOscarBinary("/nope/no-such-oscar", env);
        Assert.Null(result);
    }

    [Fact]
    public void FindOscarBinary_EnvFile_ExistingPath()
    {
        var dummy = Path.Combine(_tempDir, "oscar_dummy");
        File.WriteAllText(dummy, "");
        var env = MakeC64Env(oscarPath: dummy);
        Assert.Equal(dummy, OscarInvoker.FindOscarBinary(null, env));
    }

    [Fact]
    public void FindOscarBinary_AllNull_FallsBackToPathOrNull()
    {
        var env = MakeC64Env();
        // OSCAR64 env を干渉除外
        var prev = Environment.GetEnvironmentVariable("OSCAR64");
        Environment.SetEnvironmentVariable("OSCAR64", null);
        try
        {
            // result is either a path or null depending on whether oscar64 is on PATH.
            // どちらでも例外なく動くこと (= 単体テストのスモーク確認)
            var result = OscarInvoker.FindOscarBinary(null, env);
            // 文字列なら File.Exists が true のはず
            if (result != null) Assert.True(File.Exists(result));
        }
        finally
        {
            Environment.SetEnvironmentVariable("OSCAR64", prev);
        }
    }
}
