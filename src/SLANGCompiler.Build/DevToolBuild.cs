using System.Diagnostics;

namespace SLANGCompiler.Build;

/// <summary>
/// 開発の木の道具 (slangc など) を、その木の SDK の決まりで組み立てて動かす。
/// `dotnet run --project` は、呼んだ人の作業フォルダの global.json で SDK を選ぶので、
/// 古い SDK に決めたフォルダから呼ぶと net10.0 を組み立てられない。
/// そこで、組み立ては csproj のフォルダを作業フォルダにして行い (その木の global.json が効く)、
/// 出来た dll を、呼んだ人の作業フォルダのまま `dotnet &lt;dll&gt;` で動かす (dll を動かすときは global.json を見ない)
/// </summary>
internal static class DevToolBuild
{
    /// <summary>組み立てた物の置き場 (csproj のフォルダから)。OutDir をコマンド行で決めるので (環境変数や Directory.Build.props の OutDir より強い)、MSBuild の出力先の設定に左右されない</summary>
    internal static readonly string OutputSubdir = Path.Combine("bin", "devtool");

    /// <summary>
    /// csproj を Release で &lt;csproj のフォルダ&gt;/bin/devtool に組み立てて、その中の &lt;assemblyName&gt;.dll を返す。
    /// 組み立てに失敗したら null (log に組み立ての出力)
    /// </summary>
    internal static string? Build(string dotnet, string csproj, string assemblyName, out string log)
    {
        var projectDir = Path.GetDirectoryName(Path.GetFullPath(csproj))!;
        var outDir = Path.Combine(projectDir, OutputSubdir);
        var psi = new ProcessStartInfo
        {
            FileName = dotnet,
            WorkingDirectory = projectDir,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
        };
        foreach (var a in new[] { "build", Path.GetFullPath(csproj), "-c", "Release", "-p:OutDir=" + MsBuildEscape(outDir + Path.DirectorySeparatorChar), "-nologo", "-v", "q" })
            psi.ArgumentList.Add(a);
        using var proc = Process.Start(psi)!;
        var stderrTask = proc.StandardError.ReadToEndAsync();
        var stdout = proc.StandardOutput.ReadToEnd();
        proc.WaitForExit();
        log = stdout + stderrTask.Result;
        var dll = Path.Combine(outDir, assemblyName + ".dll");
        return proc.ExitCode == 0 && File.Exists(dll) ? dll : null;
    }

    /// <summary>
    /// コマンド行のプロパティの値の中の % ; , を MSBuild の書き方 (%25 %3B %2C) にする
    /// (; と , はプロパティの区切りになるので、そのままだと木の道にこれらがあると組み立てられない)
    /// </summary>
    internal static string MsBuildEscape(string value)
        => value.Replace("%", "%25").Replace(";", "%3B").Replace(",", "%2C");

    /// <summary>
    /// 見つけた道具を動かす ProcessStartInfo (args を付けたもの)。DotnetRun なら先に組み立てて `dotnet &lt;dll&gt; args` にする。
    /// 組み立てに失敗したら null (buildLog に組み立ての出力)。作業フォルダは決めない (呼んだ人のもの)
    /// </summary>
    internal static ProcessStartInfo? CreateStartInfo(ResolvedTool tool, string assemblyName, IEnumerable<string> args, out string buildLog)
    {
        buildLog = "";
        var psi = new ProcessStartInfo { FileName = tool.Path, UseShellExecute = false };
        if (tool.Kind == ResolutionKind.DotnetRun)
        {
            var dll = Build(tool.Path, tool.ProjectPath!, assemblyName, out buildLog);
            if (dll == null) return null;
            psi.ArgumentList.Add(dll);
        }
        foreach (var a in args) psi.ArgumentList.Add(a);
        return psi;
    }
}
