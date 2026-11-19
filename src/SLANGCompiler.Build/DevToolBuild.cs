using System.Diagnostics;

namespace SLANGCompiler.Build;

/// <summary>
/// 開発の木で組み立てた道具の、この呼び出しだけの写し。Directory は、この呼び出しのために作ったフォルダ (動かし終わったら Dispose で消す)
/// </summary>
internal sealed class BuiltDevTool : IDisposable
{
    internal BuiltDevTool(string dll, string directory)
    {
        Dll = dll;
        Directory = directory;
    }

    /// <summary>動かす dll (Directory の中)</summary>
    internal string Dll { get; }

    /// <summary>この呼び出しのために作ったフォルダ</summary>
    internal string Directory { get; }

    public void Dispose() => DevToolBuild.DeleteQuietly(Directory);
}

/// <summary>
/// 開発の木の道具 (slangc など) を、その木の SDK の決まりで組み立てて動かす。
/// - `dotnet run --project` は、呼んだ人の作業フォルダの global.json で SDK を選ぶので、古い SDK に決めたフォルダから呼ぶと
///   net10.0 を組み立てられない。そこで、組み立ては csproj のフォルダを作業フォルダにして行い (その木の global.json が効く)、
///   出来た dll を、呼んだ人の作業フォルダのまま `dotnet &lt;dll&gt;` で動かす (dll を動かすときは global.json を見ない)
/// - 組み立ては、プロセスをまたいだ錠 (bin/devtool/.lock) を取ってから 1 つずつ行う。錠の中で、組み立ての置き場 (bin/devtool/build) を
///   空にしてから組み立て、そこに出来た dll をちょうど 1 つ見つけ、置き場ごと、この呼び出しだけのフォルダ (bin/devtool/run/&lt;新しい名前&gt;) に写す。
///   動かすのはその写し。だから、MSBuild の出力先の設定 (OutDir・GenerateProjectSpecificOutputFolder・TargetPath など) で置き場がずれても
///   前の残りの dll は動かさず、同時に呼ばれたほかの組み立て (incremental clean など) に写しを消されることもない
/// </summary>
internal static class DevToolBuild
{
    /// <summary>組み立ての物を置く所 (csproj のフォルダから)</summary>
    internal static readonly string OutputSubdir = Path.Combine("bin", "devtool");

    /// <summary>錠を待つ長さの上限 (ほかの組み立てが終わるのを待つ)</summary>
    private static readonly TimeSpan LockTimeout = TimeSpan.FromMinutes(10);

    /// <summary>
    /// csproj を Release で組み立て、出来た &lt;assemblyName&gt;.dll を含む写しを返す (使い終わったら Dispose)。
    /// 組み立てに失敗したか、dll がちょうど 1 つでなければ null (log に組み立ての出力)
    /// </summary>
    internal static BuiltDevTool? Build(string dotnet, string csproj, string assemblyName, out string log)
    {
        var projectDir = Path.GetDirectoryName(Path.GetFullPath(csproj))!;
        var root = Path.Combine(projectDir, OutputSubdir);
        var buildDir = Path.Combine(root, "build");
        System.IO.Directory.CreateDirectory(root);
        using var lockFile = AcquireLock(Path.Combine(root, ".lock"));
        if (lockFile == null)
        {
            log = $"{root}/.lock を {LockTimeout.TotalMinutes} 分待っても取れませんでした" + Environment.NewLine;
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
        log = stdout + stderrTask.Result;
        if (proc.ExitCode != 0) return null;

        var found = System.IO.Directory.Exists(buildDir)
            ? System.IO.Directory.GetFiles(buildDir, assemblyName + ".dll", SearchOption.AllDirectories)
            : Array.Empty<string>();
        if (found.Length != 1)
        {
            log += $"{assemblyName}.dll が {buildDir} の中に {found.Length} 個あります (1 個のはず)" + Environment.NewLine;
            return null;
        }
        var runDir = Path.Combine(root, "run", Guid.NewGuid().ToString("N"));
        CopyTree(buildDir, runDir);
        return new BuiltDevTool(Path.Combine(runDir, Path.GetRelativePath(buildDir, found[0])), runDir);
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

    private static void CopyTree(string from, string to)
    {
        foreach (var f in System.IO.Directory.EnumerateFiles(from, "*", SearchOption.AllDirectories))
        {
            var dst = Path.Combine(to, Path.GetRelativePath(from, f));
            System.IO.Directory.CreateDirectory(Path.GetDirectoryName(dst)!);
            File.Copy(f, dst);
        }
    }

    internal static void DeleteQuietly(string dir)
    {
        try { if (System.IO.Directory.Exists(dir)) System.IO.Directory.Delete(dir, true); }
        catch (IOException) { }
        catch (UnauthorizedAccessException) { }
    }

    /// <summary>
    /// コマンド行のプロパティの値の中の % ; , を MSBuild の書き方 (%25 %3B %2C) にする
    /// (; と , はプロパティの区切りになるので、そのままだと木の道にこれらがあると組み立てられない)
    /// </summary>
    internal static string MsBuildEscape(string value)
        => value.Replace("%", "%25").Replace(";", "%3B").Replace(",", "%2C");

    /// <summary>
    /// 見つけた道具を動かす ProcessStartInfo (args を付けたもの)。DotnetRun なら先に組み立てて `dotnet &lt;写しの dll&gt; args` にし、
    /// built に写しを返す (動かし終わったら Dispose して消す)。
    /// 組み立てに失敗したら null (buildLog に組み立ての出力)。作業フォルダは決めない (呼んだ人のもの)
    /// </summary>
    internal static ProcessStartInfo? CreateStartInfo(ResolvedTool tool, string assemblyName, IEnumerable<string> args,
                                                      out string buildLog, out BuiltDevTool? built)
    {
        buildLog = "";
        built = null;
        var psi = new ProcessStartInfo { FileName = tool.Path, UseShellExecute = false };
        if (tool.Kind == ResolutionKind.DotnetRun)
        {
            built = Build(tool.Path, tool.ProjectPath!, assemblyName, out buildLog);
            if (built == null) return null;
            psi.ArgumentList.Add(built.Dll);
        }
        foreach (var a in args) psi.ArgumentList.Add(a);
        return psi;
    }
}
