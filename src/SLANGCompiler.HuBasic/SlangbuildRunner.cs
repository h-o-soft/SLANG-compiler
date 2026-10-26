using System.Diagnostics;
using System.Runtime.InteropServices;

namespace SLANGCompiler.HuBasic.CLI;

/// <summary>
/// `--emit disk|tape` で呼ぶ slangbuild を探して動かす。探す順は slangbuild が slangc を探すのと同じ
/// (slangbuild の ToolResolver.ResolveSlangc。Build と Core を参照すると単一ファイルが大きくなるので写した):
/// 1) --slangbuild
/// 2) 同梱: hubasic2sl と同じ所の slangbuild(.exe)、その下の bin/slangbuild(.exe) (配布の bin/、install で入れた所)
/// 3) 開発で publish したもの: src/SLANGCompiler.Build/bin/(Release|Debug)/net8.0/&lt;今の OS の RID&gt;/publish/slangbuild(.exe)
/// 4) PATH の上の slangbuild(.exe)
/// 5) dotnet run --project src/SLANGCompiler.Build/SLANGCompiler.Build.csproj -c Release --
/// </summary>
internal sealed class SlangbuildRunner
{
    private static string ExeSuffix => RuntimeInformation.IsOSPlatform(OSPlatform.Windows) ? ".exe" : "";

    /// <summary>見つけた slangbuild。Project があれば FileName は dotnet で、dotnet run --project Project で動かす</summary>
    internal sealed record Resolved(string FileName, string? Project = null);

    private readonly string _baseDir;

    internal SlangbuildRunner(string? baseDir = null)
    {
        _baseDir = baseDir ?? AppContext.BaseDirectory;
    }

    /// <summary>探して見つからなければ null と、探した所の説明</summary>
    internal Resolved? Resolve(string? cliOverride, out string tried)
    {
        var name = $"slangbuild{ExeSuffix}";
        if (cliOverride != null)
        {
            tried = $"--slangbuild {cliOverride}";
            return File.Exists(cliOverride) ? new Resolved(Path.GetFullPath(cliOverride)) : null;
        }
        tried = $"{_baseDir}、その下の bin/、開発の publish、PATH、dotnet run (開発)";

        var direct = Path.Combine(_baseDir, name);
        if (File.Exists(direct)) return new Resolved(direct);
        var binDir = Path.Combine(_baseDir, "bin", name);
        if (File.Exists(binDir)) return new Resolved(binDir);

        var devPublish = LocateDevPublished(name);
        if (devPublish != null) return new Resolved(devPublish);

        var onPath = FindOnPath(name);
        if (onPath != null) return new Resolved(onPath);

        var dotnet = FindOnPath($"dotnet{ExeSuffix}");
        var csproj = LocateRepo("src/SLANGCompiler.Build/SLANGCompiler.Build.csproj", File.Exists);
        if (dotnet != null && csproj != null) return new Resolved(dotnet, csproj);
        return null;
    }

    /// <summary>slangbuild を動かして終了コードを返す。stdout・stderr は受け取らずにそのまま流す</summary>
    internal static int Run(Resolved tool, IEnumerable<string> args)
    {
        var psi = new ProcessStartInfo { FileName = tool.FileName, UseShellExecute = false };
        if (tool.Project != null)
            foreach (var a in new[] { "run", "--project", tool.Project, "-c", "Release", "--" })
                psi.ArgumentList.Add(a);
        foreach (var a in args) psi.ArgumentList.Add(a);
        using var proc = Process.Start(psi)!;
        proc.WaitForExit();
        return proc.ExitCode;
    }

    /// <summary>開発で publish した slangbuild。今の OS の RID だけを見て、同じ RID に Release と Debug があれば新しい方</summary>
    private string? LocateDevPublished(string name)
    {
        var binRoot = LocateRepo("src/SLANGCompiler.Build/bin", Directory.Exists);
        if (binRoot == null) return null;
        foreach (var rid in CurrentOsRids())
        {
            var newest = new[] { "Release", "Debug" }
                .Select(c => Path.Combine(binRoot, c, "net8.0", rid, "publish", name))
                .Where(File.Exists)
                .OrderByDescending(File.GetLastWriteTimeUtc)
                .FirstOrDefault();
            if (newest != null) return newest;
        }
        return null;
    }

    /// <summary>今の OS で動く RID の候補 (1 番目は OS と CPU が合うもの。slangbuild と同じ順)</summary>
    private static IEnumerable<string> CurrentOsRids()
    {
        var arch = RuntimeInformation.OSArchitecture switch
        {
            Architecture.X64 => "x64",
            Architecture.Arm64 => "arm64",
            Architecture.X86 => "x86",
            var other => other.ToString().ToLowerInvariant(),
        };
        (string os, string[] family) = OperatingSystem.IsMacOS() ? ("osx", new[] { "osx-arm64", "osx-x64" })
            : OperatingSystem.IsLinux() ? ("linux", new[] { "linux-x64", "linux-arm64", "linux-musl-x64" })
            : OperatingSystem.IsWindows() ? ("win", new[] { "win-x64", "win-arm64", "win-x86" })
            : ("", Array.Empty<string>());
        if (os.Length == 0) return Array.Empty<string>();
        var primary = $"{os}-{arch}";
        return new[] { primary }.Concat(family.Where(r => r != primary));
    }

    /// <summary>hubasic2sl の置き場から上に 8 段まで辿って、リポジトリの中の相対パスを探す</summary>
    private string? LocateRepo(string relPath, Func<string, bool> exists)
    {
        var dir = new DirectoryInfo(_baseDir);
        for (int i = 0; i < 8 && dir != null; i++, dir = dir.Parent)
        {
            var candidate = Path.Combine(dir.FullName, relPath);
            if (exists(candidate)) return candidate;
        }
        return null;
    }

    private static string? FindOnPath(string fileName)
    {
        var path = Environment.GetEnvironmentVariable("PATH");
        if (string.IsNullOrEmpty(path)) return null;
        foreach (var dir in path.Split(Path.PathSeparator))
        {
            if (dir.Length == 0) continue;
            try
            {
                var candidate = Path.Combine(dir, fileName);
                if (File.Exists(candidate)) return candidate;
            }
            catch { /* PATH の壊れた項目 */ }
        }
        return null;
    }
}
