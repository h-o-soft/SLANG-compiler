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
}
