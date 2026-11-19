using System.Diagnostics;

namespace SLANGCompiler.Build;

/// <summary>
/// 開発の木の道具 (slangc など) を、その木の SDK の決まりで組み立てる。
/// `dotnet run --project` は、呼んだ人の作業フォルダの global.json で SDK を選ぶので、
/// 古い SDK に決めたフォルダから呼ぶと net10.0 を組み立てられない。
/// そこで、組み立ては csproj のフォルダを作業フォルダにして行い (その木の global.json が効く)、
/// 出来た dll を、呼んだ人の作業フォルダのまま `dotnet &lt;dll&gt;` で動かす (dll を動かすときは global.json を見ない)
/// </summary>
internal static class DevToolBuild
{
    /// <summary>組み立てた物の置き場 (csproj のフォルダから)。TargetFramework と合わせる</summary>
    internal const string OutputSubdir = "bin/Release/net10.0";

    /// <summary>
    /// csproj を Release で組み立てて、&lt;csproj のフォルダ&gt;/bin/Release/net10.0/&lt;assemblyName&gt;.dll を返す。
    /// 組み立てに失敗したら null (log に組み立ての出力)
    /// </summary>
    internal static string? Build(string dotnet, string csproj, string assemblyName, out string log)
    {
        var projectDir = Path.GetDirectoryName(Path.GetFullPath(csproj))!;
        var psi = new ProcessStartInfo
        {
            FileName = dotnet,
            WorkingDirectory = projectDir,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
        };
        foreach (var a in new[] { "build", Path.GetFullPath(csproj), "-c", "Release", "-nologo", "-v", "q" })
            psi.ArgumentList.Add(a);
        using var proc = Process.Start(psi)!;
        var stderrTask = proc.StandardError.ReadToEndAsync();
        var stdout = proc.StandardOutput.ReadToEnd();
        proc.WaitForExit();
        log = stdout + stderrTask.Result;
        var dll = Path.Combine(projectDir, OutputSubdir, assemblyName + ".dll");
        return proc.ExitCode == 0 && File.Exists(dll) ? dll : null;
    }
}
