namespace SLANGCompiler.Build;

/// <summary>
/// SLFS disk の main file 名 (= boot sector +01 の 13 char、 IPL が起動するプログラムの名前) の決定。
/// 優先順: `--disk-main-name` > 入力 SL 頭の `// disk-main-name: &lt;name&gt;` > env の disk.main_name > "SLFSMAIN"。
/// 頭のコメント行は hubasic2sl が BASIC のファイル名で書く。
/// </summary>
public static class SlfsMainName
{
    public const string Default = "SLFSMAIN";
    private const string HintPrefix = "// disk-main-name:";

    /// <summary>(名前, 出所 = "--disk-main-name" / "source" / "env" / "default")</summary>
    public static (string Name, string Source) Resolve(string? cliName, string? sourceName, string? envName)
    {
        if (cliName != null) return (cliName, "--disk-main-name");
        if (sourceName != null) return (sourceName, "source");
        if (!string.IsNullOrEmpty(envName)) return (envName, "env");
        return (Default, "default");
    }

    /// <summary>
    /// 入力 SL の頭のコメントの並び (= 1 行目から続く `//` 行) から `// disk-main-name: &lt;name&gt;` を読む。
    /// 無ければ null。 name は前後の空白を除く。
    /// </summary>
    public static string? ReadSourceHint(string slPath)
    {
        foreach (var line in File.ReadLines(slPath))
        {
            if (!line.StartsWith("//")) break;
            if (line.StartsWith(HintPrefix))
                return line.Substring(HintPrefix.Length).Trim();
        }
        return null;
    }

    /// <summary>
    /// X1 の FileName と同じ: ASCII printable (0x20-0x7E) + 1..13 char
    /// (= `--tape-name` と同じ規律、 silent truncate しない)。
    /// </summary>
    public static bool IsValid(string name)
    {
        if (string.IsNullOrEmpty(name) || name.Length > 13) return false;
        foreach (var ch in name)
            if (ch < 0x20 || ch > 0x7E) return false;
        return true;
    }
}
