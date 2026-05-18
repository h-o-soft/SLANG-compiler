using System.Diagnostics;
using System.Text;
using SLANGCompiler.Runtime;

namespace SLANGCompiler.Build;

/// <summary>
/// oscar64 (C → 6502 / Commodore 64 用 C コンパイラ) を Process spawn で起動するヘルパ。
///
/// 既存 <see cref="AssemblerRunner"/> (AILZ80ASM 用) と並列の位置付け。
/// slangc が出した <c>.c</c> + runtime/c64/slang_runtime.c を oscar64 に渡して
/// <c>.prg</c> を生成する。
/// </summary>
public class OscarInvoker
{
    private readonly string _oscarPath;
    private readonly bool _verbose;

    public OscarInvoker(string oscarPath, bool verbose = false)
    {
        _oscarPath = oscarPath;
        _verbose = verbose;
    }

    /// <summary>
    /// oscar64 binary を解決する。優先順:
    ///   1) <paramref name="cliOverride"/> (= slangbuild の <c>--oscar-path</c>)
    ///   2) <paramref name="envConfig"/>.OscarPath (= env file の <c>oscar_path:</c>)
    ///   3) 環境変数 <c>$OSCAR64</c>
    ///   4) PATH 上の <c>oscar64</c>
    /// 見つからなければ null。
    /// </summary>
    public static string? FindOscarBinary(string? cliOverride, EnvironmentConfig envConfig)
    {
        if (!string.IsNullOrEmpty(cliOverride))
        {
            return File.Exists(cliOverride) ? cliOverride : null;
        }

        if (!string.IsNullOrEmpty(envConfig.OscarPath))
        {
            // env で絶対 path 指定 → そのまま、相対 / コマンド名なら PATH 解決
            if (File.Exists(envConfig.OscarPath)) return envConfig.OscarPath;
            var resolved = FindOnPath(envConfig.OscarPath);
            if (resolved != null) return resolved;
        }

        var envVar = Environment.GetEnvironmentVariable("OSCAR64");
        if (!string.IsNullOrEmpty(envVar))
        {
            if (File.Exists(envVar)) return envVar;
            var resolved = FindOnPath(envVar);
            if (resolved != null) return resolved;
        }

        return FindOnPath("oscar64");
    }

    /// <summary>
    /// oscar64 引数列を組み立てる (Process 起動は別 method)。
    /// 引数順序は oscar64 manual に従う:
    ///   oscar64 [-tm=...] [-tf=...] [-psci] [-OX] {-i=...} &lt;sources...&gt; -o=&lt;out.prg&gt;
    /// </summary>
    public static List<string> BuildArgs(
        string cInputPath, string prgOutPath, EnvironmentConfig envConfig,
        IReadOnlyList<string>? extraCSources = null)
    {
        var args = new List<string>();
        // target machine / format は env で override 可能、default は c64 / prg
        args.Add($"-tm={envConfig.OscarMachine ?? "c64"}");
        args.Add($"-tf={envConfig.OscarFormat ?? "prg"}");

        // PETSCII string encoding (default true)
        if (envConfig.OscarPetscii) args.Add("-psci");

        // Optimization (env override、未指定なら oscar64 既定 = `-O` 相当)
        if (!string.IsNullOrEmpty(envConfig.OscarOptimize))
            args.Add("-" + envConfig.OscarOptimize);

        // include path (= `-i=...` 形式、複数可)
        if (envConfig.CRuntimeIncludes != null)
        {
            foreach (var inc in envConfig.CRuntimeIncludes)
                args.Add($"-i={inc}");
        }

        // source files: positional 末尾。slangc が出した main の .c を先に、
        // runtime の .c を後ろに並べる (= main の宣言で runtime ヘルパを参照する
        // 順序になる、ただし oscar64 は order に依存しないので形式優先)。
        args.Add(cInputPath);
        if (envConfig.CRuntimeFiles != null)
        {
            foreach (var rt in envConfig.CRuntimeFiles)
                args.Add(rt);
        }
        // ユーザー追加 C (= --c-source 経由)。env runtime の後ろ、output flag の前。
        if (extraCSources != null)
        {
            foreach (var src in extraCSources)
                args.Add(src);
        }

        // 出力ファイル (= `-o=path` 形式、`-o path` ではない)
        args.Add($"-o={prgOutPath}");

        return args;
    }

    /// <summary>
    /// oscar64 を spawn して .prg を生成する。
    /// 失敗時は stdout / stderr を Console.Error に流して exit code != 0 を返す。
    /// </summary>
    public OscarResult Compile(string cInputPath, string prgOutPath, EnvironmentConfig envConfig,
                                IReadOnlyList<string>? extraCSources = null)
    {
        var args = BuildArgs(cInputPath, prgOutPath, envConfig, extraCSources);

        if (_verbose)
        {
            Console.Error.WriteLine($"slangbuild: oscar64 {string.Join(" ", args)}");
        }

        var psi = new ProcessStartInfo
        {
            FileName = _oscarPath,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true,
        };
        foreach (var a in args) psi.ArgumentList.Add(a);

        try
        {
            using var proc = Process.Start(psi)!;
            var stdoutTask = proc.StandardOutput.ReadToEndAsync();
            var stderrTask = proc.StandardError.ReadToEndAsync();
            // 60s timeout (= AILZ80ASM と同じ)
            if (!proc.WaitForExit(60_000))
            {
                try { proc.Kill(entireProcessTree: true); } catch { }
                return new OscarResult(false, -1, "", "oscar64 timed out (60s)");
            }
            var stdout = stdoutTask.Result;
            var stderr = stderrTask.Result;
            return new OscarResult(proc.ExitCode == 0, proc.ExitCode, stdout, stderr);
        }
        catch (Exception ex)
        {
            return new OscarResult(false, -1, "", $"oscar64 invocation failed: {ex.Message}");
        }
    }

    private static string? FindOnPath(string name)
    {
        var pathEnv = Environment.GetEnvironmentVariable("PATH");
        if (string.IsNullOrEmpty(pathEnv)) return null;
        var sep = OperatingSystem.IsWindows() ? ';' : ':';
        foreach (var dir in pathEnv.Split(sep))
        {
            if (string.IsNullOrEmpty(dir)) continue;
            var candidate = Path.Combine(dir, name);
            if (File.Exists(candidate)) return candidate;
            if (OperatingSystem.IsWindows())
            {
                var withExe = candidate + ".exe";
                if (File.Exists(withExe)) return withExe;
            }
        }
        return null;
    }
}

/// <summary>oscar64 invoke 結果</summary>
public record OscarResult(bool Success, int ExitCode, string Stdout, string Stderr);
