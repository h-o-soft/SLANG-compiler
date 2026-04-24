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
    /// 3) dev publish: repo root を辿って
    ///    src/SLANGCompiler.CLI/bin/(Release|Debug)/net8.0/&lt;RID&gt;/publish/slangc(.exe)
    ///    が見つかればそれ (= dev 環境で publish 済みの最新 slangc を PATH 上の
    ///    旧版より優先)
    /// 4) PATH 上の slangc (= 配布版を OS にインストールしたケース)
    /// 5) dev fallback: dotnet (= caller が `dotnet run --project ...` を組み立てる)
    /// </summary>
    public ResolvedTool ResolveSlangc(string? cliOverride)
    {
        var name = $"slangc{ExeSuffix}";

        // 1) cliOverride / 2) bundled (PATH 検索なしバージョン)
        if (!string.IsNullOrEmpty(cliOverride) && File.Exists(cliOverride))
            return new ResolvedTool(cliOverride, ResolutionKind.DirectExe);
        var direct = Path.Combine(_baseDir, name);
        if (File.Exists(direct)) return new ResolvedTool(direct, ResolutionKind.DirectExe);
        var binDir = Path.Combine(_baseDir, "bin", name);
        if (File.Exists(binDir)) return new ResolvedTool(binDir, ResolutionKind.DirectExe);

        // 3) dev publish 物 (= dev 環境で `dotnet publish` 済みの最新 slangc)
        //    PATH 上の旧版に上書きされないよう、PATH 検索より前に挿入する
        var devPublish = LocateDevPublishedSlangc(name);
        if (devPublish != null) return new ResolvedTool(devPublish, ResolutionKind.DirectExe);

        // 4) PATH
        var onPath = FindOnPath(name);
        if (onPath != null) return new ResolvedTool(onPath, ResolutionKind.DirectExe);

        // 5) dev fallback: dotnet run --project <repo>/src/SLANGCompiler.CLI/...
        var dotnet = FindOnPath($"dotnet{ExeSuffix}");
        if (dotnet != null)
        {
            var cliCsproj = LocateRepoFile("src/SLANGCompiler.CLI/SLANGCompiler.CLI.csproj");
            if (cliCsproj != null)
                return new ResolvedTool(dotnet, ResolutionKind.DotnetRun, cliCsproj);
        }

        throw new FileNotFoundException(
            "slangc not found. Tried: --slangc, bundled bin, dev publish, PATH, and dev `dotnet run` fallback. "
            + "Specify --slangc <path> explicitly.");
    }

    /// <summary>
    /// dev 環境想定: repo root を起点に slangc の publish 物を探す。
    /// <c>src/SLANGCompiler.CLI/bin/(Release|Debug)/net8.0/&lt;RID&gt;/publish/slangc(.exe)</c>
    /// 配布物の `<root>/bin/slangc` パターンには触れない (= 通常解決順 2 が拾う想定)。
    ///
    /// **RID 選択** (Codex 指摘): 全 RID 走査で更新時刻最新を選ぶと、複数 RID 用に
    /// publish 物が並んでいる環境 (macOS で osx-arm64 + linux-x64 等) で OS の異なる
    /// バイナリを拾って exec format error になる。現在 OS の RID 候補リストを
    /// 順序付きで保持し、その範囲内でだけ「同一 RID で Release/Debug 両方ある場合は
    /// 更新時刻最新」で絞る。
    /// </summary>
    private string? LocateDevPublishedSlangc(string name)
    {
        var binRoot = LocateRepoDir("src/SLANGCompiler.CLI/bin");
        if (binRoot == null) return null;

        foreach (var rid in GetCurrentOsRidCandidates())
        {
            string? newest = null;
            DateTime newestTime = DateTime.MinValue;
            foreach (var config in new[] { "Release", "Debug" })
            {
                var candidate = Path.Combine(binRoot, config, "net8.0", rid, "publish", name);
                if (!File.Exists(candidate)) continue;
                var t = File.GetLastWriteTimeUtc(candidate);
                if (t > newestTime)
                {
                    newest = candidate;
                    newestTime = t;
                }
            }
            if (newest != null) return newest;
        }
        return null;
    }

    /// <summary>
    /// 現在 OS で実行可能な RID の候補リストを優先順で返す。
    /// 1 番目は `OS-arch` の正確一致、2 番目以降は同 OS の他 arch (= Rosetta 等の
    /// 互換実行を想定)。OS が違うものは含めない。
    /// </summary>
    private static IReadOnlyList<string> GetCurrentOsRidCandidates()
    {
        var archStr = RuntimeInformation.OSArchitecture switch
        {
            Architecture.X64   => "x64",
            Architecture.Arm64 => "arm64",
            Architecture.X86   => "x86",
            var other          => other.ToString().ToLowerInvariant(),
        };

        string[] family;
        string osPrefix;
        if (OperatingSystem.IsMacOS())
        {
            osPrefix = "osx";
            family = new[] { "osx-arm64", "osx-x64" };
        }
        else if (OperatingSystem.IsLinux())
        {
            osPrefix = "linux";
            family = new[] { "linux-x64", "linux-arm64", "linux-musl-x64" };
        }
        else if (OperatingSystem.IsWindows())
        {
            osPrefix = "win";
            family = new[] { "win-x64", "win-arm64", "win-x86" };
        }
        else
        {
            return Array.Empty<string>();
        }

        var primary = $"{osPrefix}-{archStr}";
        var ordered = new List<string> { primary };
        foreach (var rid in family)
            if (rid != primary) ordered.Add(rid);
        return ordered;
    }

    /// <summary>repo root を辿って指定相対ディレクトリを探す (LocateRepoFile の dir 版)</summary>
    private string? LocateRepoDir(string relPath)
    {
        var dir = new DirectoryInfo(_baseDir);
        for (int i = 0; i < 8 && dir != null; i++, dir = dir.Parent)
        {
            var candidate = Path.Combine(dir.FullName, relPath);
            if (Directory.Exists(candidate)) return candidate;
        }
        return null;
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
