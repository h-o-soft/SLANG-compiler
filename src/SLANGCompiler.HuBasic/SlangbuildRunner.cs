using System.Diagnostics;
using System.Runtime.InteropServices;

namespace SLANGCompiler.HuBasic.CLI;

/// <summary>
/// `--emit disk|tape` で呼ぶ slangbuild を探して動かす。探す順は slangbuild が slangc を探すのと同じ
/// (slangbuild の ToolResolver.ResolveSlangc。Build と Core を参照すると単一ファイルが大きくなるので写した):
/// 1) --slangbuild
/// 2) 同梱: hubasic2sl と同じ所の slangbuild(.exe)、その下の bin/slangbuild(.exe) (配布の bin/、install で入れた所)
/// 3) 開発で publish したもの: src/SLANGCompiler.Build/bin/(Release|Debug)/net10.0/&lt;今の OS の RID&gt;/publish/slangbuild(.exe)
/// 4) PATH の上の slangbuild(.exe)
/// 5) 開発の木: src/SLANGCompiler.Build/SLANGCompiler.Build.csproj をその木の SDK の決まりで組み立て、dotnet &lt;slangbuild.dll&gt; で動かす
///    (dotnet run --project は呼んだ人のフォルダの global.json で SDK を選んでしまうので使わない)
/// </summary>
internal sealed class SlangbuildRunner
{
    private static string ExeSuffix => RuntimeInformation.IsOSPlatform(OSPlatform.Windows) ? ".exe" : "";

    /// <summary>見つけた slangbuild。Project があれば FileName は dotnet で、Project を組み立てた dll を dotnet で動かす</summary>
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

    /// <summary>
    /// slangbuild を動かして終了コードを返す。stdout・stderr は受け取らずにそのまま流す。
    /// workingDirectory を省けば今の作業フォルダ、environment は足す環境変数 (どちらも試験が使う)
    /// </summary>
    internal static int Run(Resolved tool, IEnumerable<string> args, string? workingDirectory = null,
                            IReadOnlyDictionary<string, string>? environment = null)
    {
        var psi = new ProcessStartInfo { FileName = tool.FileName, UseShellExecute = false };
        if (workingDirectory != null) psi.WorkingDirectory = workingDirectory;
        if (environment != null)
            foreach (var (k, v) in environment) psi.Environment[k] = v;
        string? runDir = null;
        try
        {
            if (tool.Project != null)
            {
                var dll = BuildDevSlangbuild(tool.FileName, tool.Project, out runDir);
                if (dll == null) return 1;
                psi.ArgumentList.Add(dll);
            }
            foreach (var a in args) psi.ArgumentList.Add(a);
            using var proc = Process.Start(psi)!;
            proc.WaitForExit();
            return proc.ExitCode;
        }
        finally
        {
            // 開発の木で組み立てたときは、動かし終わったら写しのフォルダを消す
            if (runDir != null) DeleteQuietly(runDir);
        }
    }

    /// <summary>組み立ての物を置く所 (csproj のフォルダから)</summary>
    internal static readonly string DevOutputSubdir = Path.Combine("bin", "devtool");

    /// <summary>錠を待つ長さの上限 (ほかの組み立てが終わるのを待つ)</summary>
    private static readonly TimeSpan LockTimeout = TimeSpan.FromMinutes(10);

    /// <summary>
    /// コマンド行のプロパティの値の中の % ; , を MSBuild の書き方 (%25 %3B %2C) にする
    /// (; と , はプロパティの区切りになるので、そのままだと木の道にこれらがあると組み立てられない)
    /// </summary>
    internal static string MsBuildEscape(string value)
        => value.Replace("%", "%25").Replace(";", "%3B").Replace(",", "%2C");

    /// <summary>
    /// 開発の木の slangbuild を、csproj のフォルダを作業フォルダにして (その木の global.json が効く) Release で組み立て、
    /// この呼び出しだけの写しの slangbuild.dll の道を返す (runDir は写しのフォルダ。動かし終わったら消す)。
    /// slangbuild の DevToolBuild と同じ作り: プロセスをまたいだ錠 (bin/devtool/.lock) の中で、組み立ての置き場 (bin/devtool/build) を
    /// 空にしてから組み立て、出来た dll をちょうど 1 つ見つけ、置き場ごと bin/devtool/run/&lt;新しい名前&gt; に写す。
    /// MSBuild の出力先の設定で置き場がずれても前の残りの dll は動かさず、同時に呼ばれたほかの組み立てに写しを消されることもない。
    /// 失敗したら組み立ての出力を stderr に書いて null
    /// </summary>
    private static string? BuildDevSlangbuild(string dotnet, string csproj, out string? runDir)
    {
        runDir = null;
        var projectDir = Path.GetDirectoryName(Path.GetFullPath(csproj))!;
        var root = Path.Combine(projectDir, DevOutputSubdir);
        var buildDir = Path.Combine(root, "build");
        Directory.CreateDirectory(root);
        using var lockFile = AcquireLock(Path.Combine(root, ".lock"));
        if (lockFile == null)
        {
            Console.Error.WriteLine($"hubasic2sl: {root}/.lock を {LockTimeout.TotalMinutes} 分待っても取れませんでした");
            return null;
        }

        DeleteQuietly(buildDir);
        var psi = new ProcessStartInfo
        {
            FileName = dotnet,
            WorkingDirectory = projectDir,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
        };
        foreach (var a in new[] { "build", Path.GetFullPath(csproj), "-c", "Release",
                                  "-p:OutDir=" + MsBuildEscape(buildDir + Path.DirectorySeparatorChar), "-nologo", "-v", "q" })
            psi.ArgumentList.Add(a);
        using var proc = Process.Start(psi)!;
        var stderrTask = proc.StandardError.ReadToEndAsync();
        var stdout = proc.StandardOutput.ReadToEnd();
        proc.WaitForExit();
        var found = proc.ExitCode == 0 && Directory.Exists(buildDir)
            ? Directory.GetFiles(buildDir, "slangbuild.dll", SearchOption.AllDirectories)
            : Array.Empty<string>();
        if (found.Length != 1)
        {
            Console.Error.Write(stdout + stderrTask.Result);
            if (proc.ExitCode == 0)
                Console.Error.WriteLine($"hubasic2sl: slangbuild.dll が {buildDir} の中に {found.Length} 個あります (1 個のはず)");
            Console.Error.WriteLine($"hubasic2sl: slangbuild を組み立てられませんでした ({csproj})");
            return null;
        }
        runDir = Path.Combine(root, "run", Guid.NewGuid().ToString("N"));
        foreach (var f in Directory.EnumerateFiles(buildDir, "*", SearchOption.AllDirectories))
        {
            var dst = Path.Combine(runDir, Path.GetRelativePath(buildDir, f));
            Directory.CreateDirectory(Path.GetDirectoryName(dst)!);
            File.Copy(f, dst);
        }
        return Path.Combine(runDir, Path.GetRelativePath(buildDir, found[0]));
    }

    /// <summary>プロセスをまたいだ錠 (ほかのプロセスが開いている間は開けない)。待っても取れなければ null</summary>
    private static FileStream? AcquireLock(string path)
    {
        var deadline = DateTime.UtcNow + LockTimeout;
        while (true)
        {
            try
            {
                return new FileStream(path, FileMode.OpenOrCreate, FileAccess.ReadWrite, FileShare.None);
            }
            catch (IOException) when (DateTime.UtcNow < deadline)
            {
                Thread.Sleep(200);
            }
            catch (IOException)
            {
                return null;
            }
        }
    }

    private static void DeleteQuietly(string dir)
    {
        try { if (Directory.Exists(dir)) Directory.Delete(dir, true); }
        catch (IOException) { }
        catch (UnauthorizedAccessException) { }
    }

    /// <summary>開発で publish した slangbuild。今の OS の RID だけを見て、同じ RID に Release と Debug があれば新しい方</summary>
    private string? LocateDevPublished(string name)
    {
        var binRoot = LocateRepo("src/SLANGCompiler.Build/bin", Directory.Exists);
        if (binRoot == null) return null;
        foreach (var rid in CurrentOsRids())
        {
            var newest = new[] { "Release", "Debug" }
                .Select(c => Path.Combine(binRoot, c, "net10.0", rid, "publish", name))
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
