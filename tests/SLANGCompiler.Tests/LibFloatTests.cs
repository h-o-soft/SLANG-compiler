using SLANGCompiler.Runtime;
using Xunit;

namespace SLANGCompiler.Tests;

/// <summary>libfloat のメタデータ (@calls) の点検</summary>
public class LibFloatTests
{
    /// <summary>libfloat の @calls に書いた名前は、x1native の環境のどれかのライブラリの関数名 (@name)</summary>
    [Fact]
    public void LibFloat_CallsReferToFunctionNames()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir != null && !File.Exists(Path.Combine(dir.FullName, "runtime", "env", "x1native.env")))
            dir = dir.Parent;
        Assert.NotNull(dir);
        var root = dir!.FullName;
        var names = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var lib in EnvironmentLoader.Load(Path.Combine(root, "runtime", "env", "x1native.env")).Libraries)
        {
            var lp = Path.Combine(root, "runtime", Path.GetFileNameWithoutExtension(lib) + ".asm");
            if (File.Exists(lp))
                foreach (var f in RuntimeParser.Parse(File.ReadAllText(lp), lp)) names.Add(f.Name);
        }
        var path = Path.Combine(root, "runtime", "libfloat.asm");
        var missing = RuntimeParser.Parse(File.ReadAllText(path), path)
            .SelectMany(f => f.Dependencies.Select(d => (f.Name, d)))
            .Where(x => !names.Contains(x.d))
            .Select(x => $"{x.Name} → {x.d}")
            .ToList();
        Assert.True(missing.Count == 0, "関数名でない @calls: " + string.Join(", ", missing));
    }

    /// <summary>ファイルの頭 (最初の @name より前) のライセンスの注釈は、どの関数のコード・init・メタデータにも入らない (出力を変えない)</summary>
    [Theory]
    [InlineData("libfloat.asm")]
    [InlineData("libcompress.asm")]
    public void FileHeaderComment_DoesNotChangeParsedFunctions(string file)
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir != null && !File.Exists(Path.Combine(dir.FullName, "runtime", "env", "x1native.env")))
            dir = dir.Parent;
        Assert.NotNull(dir);
        var path = Path.Combine(dir!.FullName, "runtime", file);
        var text = File.ReadAllText(path);
        var first = text.IndexOf("\n; @name", StringComparison.Ordinal) + 1; // 行の頭の最初の @name
        Assert.True(first > 0, "@name がありません");
        Assert.Contains("License", text[..first]);
        Assert.Equal(Describe(RuntimeParser.Parse(text[first..], path)), Describe(RuntimeParser.Parse(text, path)));
    }

    private static List<string> Describe(List<RuntimeFunction> functions) =>
        functions.Select(f => string.Join("|", f.Name, f.ParamCount, string.Join(",", f.Dependencies), f.Code, f.InitCode ?? "-",
            f.LibName ?? "-", f.LoadOrder, f.Works == null ? "-" : string.Join(",", f.Works.Select(w => $"{w.Label}:{w.Size}")),
            f.WorksAlignment, f.CalleeCleanup, string.Join(",", f.Aliases), f.ResultType ?? "-",
            f.ParamTypes == null ? "-" : string.Join(",", f.ParamTypes), f.Residency, string.Join(",", f.Requires))).ToList();
}
