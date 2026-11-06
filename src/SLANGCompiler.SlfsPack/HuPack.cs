namespace SLANGCompiler.SlfsPack;

/// <summary>
/// x1native_hufs のディスクを作る (slangbuild の disk.tool: hufs-pack と、slfs-pack hu pack の共通)。
/// 本体はクラスタ 2 から続けて置き、足すファイルは本体の後ろに名前の順で置く。日付はすべて 0
/// (組み立てるたびに同じ中身になるように)。
/// </summary>
public static class HuPack
{
    /// <summary>足すファイル: ディスクの上の名前・元のファイル・種類</summary>
    public sealed record AddSpec(string Name, string Path, HuDiskImage.AddKind Kind);

    /// <summary>
    /// "[名前=]ファイル[,bin|,text]" を読む。名前を省いたらファイル名 (ディレクトリを除いたもの)。
    /// ファイルがなければ FileNotFoundException、形がおかしければ ArgumentException
    /// </summary>
    public static AddSpec ParseAddSpec(string spec)
    {
        if (string.IsNullOrEmpty(spec)) throw new ArgumentException("足すファイルの指定が空です");
        var kind = HuDiskImage.AddKind.Raw;
        var body = spec;
        if (body.EndsWith(",bin", StringComparison.Ordinal)) { kind = HuDiskImage.AddKind.Bin; body = body[..^4]; }
        else if (body.EndsWith(",text", StringComparison.Ordinal)) { kind = HuDiskImage.AddKind.Text; body = body[..^5]; }
        string name, path;
        int eq = body.IndexOf('=');
        if (eq >= 0) { name = body[..eq]; path = body[(eq + 1)..]; }
        else { path = body; name = System.IO.Path.GetFileName(path); }
        if (name.Length == 0 || path.Length == 0) throw new ArgumentException($"足すファイルの指定がおかしいです: '{spec}'");
        if (!File.Exists(path)) throw new FileNotFoundException($"足すファイルがありません: {path}");
        HuName.Parse(name);   // 名前の決まりをここで確かめる
        return new AddSpec(name, path, kind);
    }

    /// <summary>ディスクを作って D88 を返す</summary>
    public static byte[] Build(byte[] mainBinary, string mainName, ushort load, ushort exec, IEnumerable<AddSpec> adds)
    {
        var disk = HuDiskImage.CreateBlank();
        disk.SetMain(mainName, mainBinary, load, exec);
        foreach (var a in adds.OrderBy(a => a.Name, StringComparer.Ordinal))
            disk.AddFile(a.Name, File.ReadAllBytes(a.Path), a.Kind);
        return disk.ToD88("HUFS");
    }
}
