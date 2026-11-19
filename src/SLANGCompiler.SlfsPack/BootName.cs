namespace SLANGCompiler.SlfsPack;

/// <summary>
/// ディスクの IPL の起動名 (SLFS・Hu-BASIC の形のディスクの、本体のファイル名)。slangbuild の --disk-main-name と、
/// ブラウザ向けの HuDiskWebApi が同じ決まりで確かめる
/// </summary>
public static class BootName
{
    /// <summary>起動名の決まりの説明 (エラーの文に使う)</summary>
    public const string Rule = "ASCII printable 0x20-0x7E, 1..13 char";

    /// <summary>1〜13 文字の、表示できる ASCII (0x20〜0x7E) か</summary>
    public static bool IsValid(string? name)
    {
        if (string.IsNullOrEmpty(name) || name.Length > 13) return false;
        foreach (var ch in name)
            if (ch < 0x20 || ch > 0x7E) return false;
        return true;
    }
}
