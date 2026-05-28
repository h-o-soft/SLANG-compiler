using SLANGCompiler.SlfsPack;

namespace SLANGCompiler.Build;

/// <summary>
/// `--slfs-add` 引数 list (= file: spec or dir spec) を SlfsPackerLibrary.AssetEntry
/// list に解決する shared helper。
///
/// Driver (= compile 前 header 生成) と DiskImageBuilder (= D88 build) の両方が
/// 本 helper を経由することで、 header の asset ID 順と D88 内 ID 順が完全一致
/// する (= dir walk / sort / type / name 正規化の差 risk 解消)。
/// </summary>
public static class SlfsAssetResolver
{
    /// <summary>--slfs-add specs list を AssetEntry list に解決。
    /// dir 指定は non-recursive walk + ordinal sort で順序安定。</summary>
    public static List<SlfsPackerLibrary.AssetEntry> Resolve(IEnumerable<string> addSpecs)
    {
        var assets = new List<SlfsPackerLibrary.AssetEntry>();
        foreach (var spec in addSpecs)
            ResolveOne(spec, assets);
        return assets;
    }

    /// <summary>
    /// 1 spec を解決:
    ///   "name:path[:type]" = 単一 file
    ///   "dir/" or "dir"    = directory (= non-recursive walk、 各 file の name = basename)
    /// </summary>
    private static void ResolveOne(string arg, List<SlfsPackerLibrary.AssetEntry> assets)
    {
        if (Directory.Exists(arg))
        {
            var files = Directory.GetFiles(arg);
            Array.Sort(files, StringComparer.Ordinal);
            foreach (var f in files)
                assets.Add(new SlfsPackerLibrary.AssetEntry
                {
                    Name = Path.GetFileName(f),
                    Data = File.ReadAllBytes(f),
                    Type = 0,
                });
            return;
        }

        var parts = arg.Split(':', 3);
        if (parts.Length < 2)
            throw new ArgumentException(
                $"--slfs-add: invalid format '{arg}' (expected 'name:path[:type]' or dir)");
        var name = parts[0];
        var path = parts[1];
        byte type = parts.Length >= 3 ? byte.Parse(parts[2]) : (byte)0;
        assets.Add(new SlfsPackerLibrary.AssetEntry
        {
            Name = name,
            Data = File.ReadAllBytes(path),
            Type = type,
        });
    }
}
