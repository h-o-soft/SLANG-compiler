namespace SLANGCompiler.Runtime;

/// <summary>
/// SLANG ランタイム / ライブラリ / インクルード ファイルの検索パスを統一的に
/// 提供する。slangc / slangbuild の双方から利用される (= env file 探索などで
/// 検索順がズレないように Core に集約)。
/// </summary>
public class PathResolver
{
    private readonly List<string> _extraIncludePaths;
    private readonly List<string> _extraLibPaths;
    private readonly List<string> _defaultPaths;

    /// <summary>ユーザー設定ディレクトリ (~/.config/SLANG)</summary>
    public static string UserConfigDir =>
        Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), ".config", "SLANG");

    public PathResolver(List<string> extraIncludePaths, List<string> extraLibPaths)
    {
        _extraIncludePaths = extraIncludePaths;
        _extraLibPaths = extraLibPaths;
        _defaultPaths = BuildDefaultPaths();
    }

    /// <summary>デフォルト検索パスを構築</summary>
    private static List<string> BuildDefaultPaths()
    {
        var paths = new List<string>();

        // $SLANG_HOME
        var slangHome = Environment.GetEnvironmentVariable("SLANG_HOME");
        if (!string.IsNullOrEmpty(slangHome) && Directory.Exists(slangHome))
            paths.Add(slangHome);

        // ~/.config/SLANG
        var configDir = UserConfigDir;
        if (Directory.Exists(configDir))
            paths.Add(configDir);

        // <executable>/../share/slang (システムインストール)
        var exeDir = Path.GetDirectoryName(Environment.ProcessPath);
        if (exeDir != null)
        {
            var shareDir = Path.Combine(exeDir, "..", "share", "slang");
            var resolved = Path.GetFullPath(shareDir);
            if (Directory.Exists(resolved))
                paths.Add(resolved);
        }

        return paths;
    }

    /// <summary>#INCLUDE 用の検索パスリスト</summary>
    public List<string> GetIncludePaths(string sourceDir)
    {
        var paths = new List<string>();
        paths.Add(sourceDir);                   // 1. ソースファイルのディレクトリ
        paths.AddRange(_extraIncludePaths);      // 2. -I で指定されたパス
        foreach (var d in _defaultPaths)         // 3-5. デフォルトパス
            paths.Add(Path.Combine(d, "include"));
        return paths;
    }

    /// <summary>lib/ (env 定義ファイル等) の検索パスリスト</summary>
    public List<string> GetLibPaths()
    {
        var paths = new List<string>();
        paths.Add("lib");                        // CWD の lib (開発時)
        paths.AddRange(_extraLibPaths);          // -L で指定されたパス
        foreach (var d in _defaultPaths)
            paths.Add(Path.Combine(d, "lib"));
        return paths;
    }

    /// <summary>ランタイム (.asm) の検索パスリスト</summary>
    public List<string> GetRuntimePaths()
    {
        var paths = new List<string>();
        paths.Add("runtime");                    // CWD の runtime (開発時)
        foreach (var lp in _extraLibPaths)
        {
            paths.Add(lp);                       // -L パス直接 (runtime/ を直接指定した場合)
            paths.Add(Path.Combine(lp, "runtime")); // -L 配下の runtime/
        }
        foreach (var d in _defaultPaths)
            paths.Add(Path.Combine(d, "runtime"));
        return paths;
    }
}
