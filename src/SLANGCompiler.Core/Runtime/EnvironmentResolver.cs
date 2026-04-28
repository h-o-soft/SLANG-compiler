namespace SLANGCompiler.Runtime;

/// <summary>
/// 環境名 (-E lsx 等) から .env ファイルを検索パス順に解決する。
/// slangc / slangbuild が同じ entry point を経由することで、検索順や
/// 失敗時の挙動が常に一致するようにする。
/// </summary>
public static class EnvironmentResolver
{
    /// <summary>
    /// envName から &lt;dir&gt;/env/&lt;envName&gt;.env を検索し、最初に見つかった
    /// ものをロードして返す。見つからない場合は null。ファイルは見つかった
    /// がロード失敗 (ファイル破損等) の場合は例外をそのまま伝播する。
    /// </summary>
    /// <param name="envName">環境名 (例: "lsx", "x1")</param>
    /// <param name="searchPaths">検索ディレクトリ (= runtime / lib paths)。
    /// 各 dir 配下の env/ サブディレクトリを順に探す。</param>
    public static (EnvironmentConfig Config, string EnvPath)? Resolve(
        string envName, IEnumerable<string> searchPaths)
    {
        var envFile = $"{envName}.env";
        foreach (var dir in searchPaths)
        {
            var envPath = Path.Combine(dir, "env", envFile);
            if (!File.Exists(envPath)) continue;
            var config = EnvironmentLoader.Load(envPath);
            return (config, Path.GetFullPath(envPath));
        }
        return null;
    }
}
