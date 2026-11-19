using SLANGCompiler.Build;
using SLANGCompiler.HuBasic.CLI;
using Xunit;

namespace SLANGCompiler.Tests;

/// <summary>環境変数 PATH を一時的に変える試験は、ほかの試験と並べずに 1 つずつ動かす</summary>
[CollectionDefinition("PathEnvironment", DisableParallelization = true)]
public class PathEnvironmentCollection { }

/// <summary>
/// slangbuild の slangc、hubasic2sl の slangbuild が、--slangc / --slangbuild・同梱・開発の publish・PATH のどこにも無ければ、
/// 開発の木から組み立てて動かすことはせず、見つからないと言って止まる (publish の仕方か --slangc を案内する)
/// </summary>
[Collection("PathEnvironment")]
public class ToolNotFoundTests : IDisposable
{
    private readonly string _dir;
    private readonly string? _oldPath;

    public ToolNotFoundTests()
    {
        _dir = Path.Combine(Path.GetTempPath(), "slang_notfound_" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(_dir);
        _oldPath = Environment.GetEnvironmentVariable("PATH");
        Environment.SetEnvironmentVariable("PATH", _dir);   // 何も無いフォルダだけ
    }

    public void Dispose()
    {
        Environment.SetEnvironmentVariable("PATH", _oldPath);
        try { Directory.Delete(_dir, true); } catch { }
    }

    [Fact]
    public void Slangbuild_WithoutSlangc_StopsWithPublishHint()
    {
        // 開発の木の形 (src/SLANGCompiler.CLI/SLANGCompiler.CLI.csproj がある) でも、組み立てには行かない
        var cli = Path.Combine(_dir, "src", "SLANGCompiler.CLI");
        Directory.CreateDirectory(cli);
        File.WriteAllText(Path.Combine(cli, "SLANGCompiler.CLI.csproj"), "<Project />");
        var baseDir = Path.Combine(_dir, "src", "SLANGCompiler.Build", "bin", "Release", "net10.0");
        Directory.CreateDirectory(baseDir);

        var e = Assert.Throws<FileNotFoundException>(() => new ToolResolver(baseDir).ResolveSlangc(null));
        Assert.Contains("--slangc", e.Message);
        Assert.Contains("dotnet publish src/SLANGCompiler.CLI", e.Message);
        Assert.DoesNotContain("dotnet run", e.Message);
    }

    [Fact]
    public void HuBasic_WithoutSlangbuild_IsNotFound()
    {
        var build = Path.Combine(_dir, "src", "SLANGCompiler.Build");
        Directory.CreateDirectory(build);
        File.WriteAllText(Path.Combine(build, "SLANGCompiler.Build.csproj"), "<Project />");
        var baseDir = Path.Combine(_dir, "src", "SLANGCompiler.HuBasic", "bin", "Release", "net10.0");
        Directory.CreateDirectory(baseDir);

        Assert.Null(new SlangbuildRunner(baseDir).Resolve(null, out var tried));
        Assert.DoesNotContain("dotnet run", tried);
    }
}
