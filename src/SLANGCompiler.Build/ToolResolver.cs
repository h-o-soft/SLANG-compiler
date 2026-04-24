using System.Runtime.InteropServices;

namespace SLANGCompiler.Build;

/// <summary>
/// slangc / AILZ80ASM の実行ファイルパスを決定論的に解決する。
///
/// - cwd 基準ではなく `AppContext.BaseDirectory` を起点にする (壊れにくさ優先)
/// - 配布スクリプト (publish.sh / Makefile.dist) では `--asm` / `--slangc` を
///   明示指定して再現性を担保する想定。本クラスのフォールバックは開発時 +
///   緊急時の救済として位置付ける
/// </summary>
public class ToolResolver
{
    private static bool IsWindows => RuntimeInformation.IsOSPlatform(OSPlatform.Windows);
    private static string ExeSuffix => IsWindows ? ".exe" : "";

    private readonly string _baseDir;

    public ToolResolver(string? baseDir = null)
    {
        _baseDir = baseDir ?? AppContext.BaseDirectory;
    }

    /// <summary>
    /// slangc 実行ファイルを解決して返す。
    /// 解決順:
    /// 1) cliOverride (--slangc 引数)
    /// 2) bundled: {baseDir}/slangc(.exe), {baseDir}/bin/slangc(.exe)
    /// 3) PATH 上の slangc
    /// 4) dev fallback: dotnet (= caller が `dotnet run --project ...` を組み立てる)
    /// </summary>
    public ResolvedTool ResolveSlangc(string? cliOverride)
    {
        var name = $"slangc{ExeSuffix}";
        var path = ResolveExecutable(cliOverride, name, includeBundledTools: false);
        if (path != null) return new ResolvedTool(path, ResolutionKind.DirectExe);

        // dev fallback: dotnet run --project <repo>/src/SLANGCompiler.CLI/...
        var dotnet = FindOnPath($"dotnet{ExeSuffix}");
        if (dotnet != null)
        {
            var cliCsproj = LocateRepoFile("src/SLANGCompiler.CLI/SLANGCompiler.CLI.csproj");
            if (cliCsproj != null)
                return new ResolvedTool(dotnet, ResolutionKind.DotnetRun, cliCsproj);
        }

        throw new FileNotFoundException(
            "slangc not found. Tried: --slangc, bundled bin, PATH, and dev `dotnet run` fallback. "
            + "Specify --slangc <path> explicitly.");
    }

    /// <summary>
    /// AILZ80ASM 実行ファイルを解決。
    /// 解決順:
    /// 1) cliOverride (--asm)
    /// 2) AILZ80ASM_PATH 環境変数
    /// 3) PATH 上の AILZ80ASM
    /// 4) {baseDir}/tools/AILZ80ASM(.exe)
    /// 5) repo root 基準 tools/AILZ80ASM(.exe) (dev fallback)
    /// </summary>
    public ResolvedTool ResolveAilz80Asm(string? cliOverride)
    {
        if (!string.IsNullOrEmpty(cliOverride) && File.Exists(cliOverride))
            return new ResolvedTool(cliOverride, ResolutionKind.DirectExe);

        var envPath = Environment.GetEnvironmentVariable("AILZ80ASM_PATH");
        if (!string.IsNullOrEmpty(envPath) && File.Exists(envPath))
            return new ResolvedTool(envPath, ResolutionKind.DirectExe);

        var name = $"AILZ80ASM{ExeSuffix}";
        var onPath = FindOnPath(name);
        if (onPath != null) return new ResolvedTool(onPath, ResolutionKind.DirectExe);

        // 配布物レイアウト:
        //   <root>/bin/slangbuild       (= AppContext.BaseDirectory)
        //   <root>/tools/AILZ80ASM      (1 つ上)
        // 開発時の publish 直下レイアウト (= bin/Release/<rid>/publish/slangbuild + tools/) も
        // 念のためサポートするため両方を探す。
        var bundledSibling = Path.Combine(_baseDir, "tools", name);
        if (File.Exists(bundledSibling)) return new ResolvedTool(bundledSibling, ResolutionKind.DirectExe);
        var bundledParent = Path.Combine(_baseDir, "..", "tools", name);
        if (File.Exists(bundledParent))
            return new ResolvedTool(Path.GetFullPath(bundledParent), ResolutionKind.DirectExe);

        var repoTools = LocateRepoFile($"tools/{name}");
        if (repoTools != null) return new ResolvedTool(repoTools, ResolutionKind.DirectExe);

        throw new FileNotFoundException(
            "AILZ80ASM not found. Tried: --asm, $AILZ80ASM_PATH, PATH, bundled "
            + "{baseDir}/tools, {baseDir}/../tools, and repo root. "
            + "Specify --asm <path> explicitly.");
    }

    private string? ResolveExecutable(string? cliOverride, string fileName, bool includeBundledTools)
    {
        if (!string.IsNullOrEmpty(cliOverride) && File.Exists(cliOverride))
            return cliOverride;

        // bundled (slangc は build dir 直下、AILZ80ASM は tools/ 配下に置く慣習)
        var direct = Path.Combine(_baseDir, fileName);
        if (File.Exists(direct)) return direct;
        var binDir = Path.Combine(_baseDir, "bin", fileName);
        if (File.Exists(binDir)) return binDir;

        if (includeBundledTools)
        {
            var tools = Path.Combine(_baseDir, "tools", fileName);
            if (File.Exists(tools)) return tools;
        }

        return FindOnPath(fileName);
    }

    private static string? FindOnPath(string fileName)
    {
        var path = Environment.GetEnvironmentVariable("PATH");
        if (string.IsNullOrEmpty(path)) return null;
        foreach (var dir in path.Split(Path.PathSeparator))
        {
            if (string.IsNullOrEmpty(dir)) continue;
            try
            {
                var candidate = Path.Combine(dir, fileName);
                if (File.Exists(candidate)) return candidate;
            }
            catch { /* invalid path entry */ }
        }
        return null;
    }

    /// <summary>
    /// dev 環境用: AppContext.BaseDirectory を起点に上位を辿って repo root を
    /// 推定し、相対パスのファイルを探す。bin/Release/net8.0 配下から実行されている
    /// 想定で、最大 7 階層上まで探索。
    /// </summary>
    private string? LocateRepoFile(string relPath)
    {
        var dir = new DirectoryInfo(_baseDir);
        for (int i = 0; i < 8 && dir != null; i++, dir = dir.Parent)
        {
            var candidate = Path.Combine(dir.FullName, relPath);
            if (File.Exists(candidate)) return candidate;
        }
        return null;
    }
}

public enum ResolutionKind
{
    /// <summary>実行ファイルを直接 spawn (slangc / AILZ80ASM)</summary>
    DirectExe,
    /// <summary>dotnet run --project &lt;csproj&gt; 経由 (dev fallback)</summary>
    DotnetRun,
}

/// <summary>
/// 解決結果。`Kind == DotnetRun` の場合 `Path` は dotnet の絶対パス、
/// `ProjectPath` は --project に渡す csproj のパス。
/// </summary>
public record ResolvedTool(string Path, ResolutionKind Kind, string? ProjectPath = null);
