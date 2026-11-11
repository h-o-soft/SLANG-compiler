using System.Text.RegularExpressions;

namespace SLANGCompiler.Build;

/// <summary>
/// ZX Spectrum Next の NEX (V1.2) を組む (slangbuild --emit nex)。Next の SLANG IDE の EXTEND W と同じ形:
///   - 頭 512 バイト: +8 要る RAM (0 = 768K、1 = 1792K)、+9 バンクの数、+11 枠の色 7、+12 SP、+14 PC、+18〜 入れたバンクの印、+135〜 要るコアの版 3.1.10
///   - 続けて 16K のバンクを NEX の順 (5, 2, 0, 1, 3, 4, 6, 7, …) で。本体は CPU の番地でバンク 2 (8000H〜BFFFH)・0 (C000H〜FFFFH) へ。
///     #MODULE $addr PAGE n のモジュールはページ n の (addr AND 1FFFH) から。//#PAGE のデータはページの頭から (8K を越えたら次のページへ)
///   - バンクは先に 0 で取り、全部を置いてから書く (同じバンクのもう半分は 0)
/// </summary>
public static class NexImage
{
    public const int DataPageMin = 30, DataPageMax = 55, DataLinesMax = 16, DataNameMax = 34;
    /// <summary>1MB の Next で使えるページは 0〜95 (768K) と見る。これより上を使えば 1792K (2MB) が要る</summary>
    public const int Pages768K = 96;

    public record Module(int Index, int Org, int Page, byte[] Bin);
    public record DataFile(int Line, int Page, string Name, byte[] Bytes);

    /// <summary>
    /// ソースの //#PAGE の行を読む (IDE の docs/data.md と同じ形とエラーの文)。
    /// 行の頭から `//#PAGE`、空白、ページ (10 進、30〜55)、空白、ファイル名 (名前だけ、34 文字まで)、後ろは空白だけ。16 行まで。
    /// ファイルはソースと同じフォルダから読む。#INCLUDE したファイルの中は見ない
    /// </summary>
    public static List<DataFile> ReadDataPages(string sourcePath)
    {
        var result = new List<DataFile>();
        var lines = File.ReadAllLines(sourcePath);
        var form = new Regex(@"^//#PAGE[ \t]+([0-9]+)[ \t]+([^ \t/\\:]+)[ \t]*$");
        for (int i = 0; i < lines.Length; i++)
        {
            if (!lines[i].StartsWith("//#PAGE")) continue;
            var m = form.Match(lines[i]);
            if (!m.Success || !int.TryParse(m.Groups[1].Value, out var page) || page < DataPageMin || page > DataPageMax
                || m.Groups[2].Value.Length > DataNameMax)
                throw new InvalidDataException($"//#PAGE line {i + 1}: write //#PAGE page name, page {DataPageMin}-{DataPageMax}, a name only.");
            if (result.Count == DataLinesMax)
                throw new InvalidDataException($"//#PAGE: {DataLinesMax} lines at most.");
            var name = m.Groups[2].Value;
            var path = Path.Combine(Path.GetDirectoryName(Path.GetFullPath(sourcePath))!, name);
            byte[] bytes;
            try { bytes = File.ReadAllBytes(path); }
            catch (Exception e) when (e is IOException or UnauthorizedAccessException)
            {
                throw new InvalidDataException($"Data file {name}: cannot be read.");
            }
            result.Add(new DataFile(i + 1, page, name, bytes));
        }
        return result;
    }

    /// <summary>NEX を組む。lowRam は CONST __LOWRAM のページ (0 は無し)。重なりはエラー (InvalidDataException)</summary>
    public static byte[] Build(byte[] mainBin, int org, int sp, int pc, IReadOnlyList<Module> modules,
                               IReadOnlyList<DataFile> data, int lowRam)
    {
        var banks = new SortedDictionary<int, byte[]> { [2] = new byte[0x4000], [0] = new byte[0x4000] };
        byte[] Bank(int b) => banks.TryGetValue(b, out var x) ? x : banks[b] = new byte[0x4000];

        // 本体: CPU の番地でバンク 2・0 へ
        if (org < 0x8000 || org + mainBin.Length > 0x10000)
            throw new InvalidDataException($"NEX: the main code ${org:X4}-${org + mainBin.Length:X4} must be in 8000H-FFFFH");
        for (int i = 0; i < mainBin.Length; i++)
        {
            int a = org + i;
            Bank(a < 0xC000 ? 2 : 0)[a & 0x3FFF] = mainBin[i];
        }

        // ページの使い道の表 (モジュール・__LOWRAM・データ)
        var owner = new Dictionary<int, string>();
        void Use(int page, string who)
        {
            if (owner.TryGetValue(page, out var other))
                throw new InvalidDataException($"{who}: page {page} is already used by {other}.");
            owner[page] = who;
        }
        if (lowRam != 0)
        {
            owner[lowRam] = "__LOWRAM";
            owner[lowRam + 1] = "__LOWRAM";
        }
        foreach (var mod in modules)
        {
            var who = $"#MODULE {mod.Index}";
            // __LOWRAM の n は 0000H〜1FFFH の、n+1 は 2000H〜3FFFH のモジュールが使える (slangc が調べている)。ほかは使い道の表で重なりを調べる
            int slot = mod.Org >> 13;
            bool lowPage = lowRam != 0 && ((slot == 0 && mod.Page == lowRam) || (slot == 1 && mod.Page == lowRam + 1));
            if (!lowPage) Use(mod.Page, who);
            int off = mod.Org & 0x1FFF;
            if (off + mod.Bin.Length > 0x2000)
                throw new InvalidDataException($"{who}: the code does not fit page {mod.Page}.");
            mod.Bin.CopyTo(Bank(mod.Page / 2), (mod.Page % 2) * 0x2000 + off);
        }
        foreach (var d in data)
        {
            int pages = Math.Max(1, (d.Bytes.Length + 0x1FFF) / 0x2000);
            if (d.Page + pages - 1 > DataPageMax)
                throw new InvalidDataException($"Data file {d.Name}: goes past page {DataPageMax}.");
            for (int p = d.Page; p < d.Page + pages; p++)
            {
                if (owner.TryGetValue(p, out var other))
                    throw new InvalidDataException(other.StartsWith("Data file")
                        ? $"Data file {d.Name}: overlaps another //#PAGE."
                        : $"Data file {d.Name}: overlaps {other} (page {p}).");
                owner[p] = $"Data file {d.Name}";
            }
            for (int i = 0; i < d.Bytes.Length; i++)
            {
                int p = d.Page + i / 0x2000;
                Bank(p / 2)[(p % 2) * 0x2000 + i % 0x2000] = d.Bytes[i];
            }
        }

        var hdr = new byte[512];
        "NextV1.2"u8.CopyTo(hdr);
        int maxPage = owner.Keys.DefaultIfEmpty(0).Max();
        hdr[8] = (byte)(maxPage >= Pages768K ? 1 : 0);
        hdr[9] = (byte)banks.Count;
        hdr[11] = 7;
        hdr[12] = (byte)sp; hdr[13] = (byte)(sp >> 8);
        hdr[14] = (byte)pc; hdr[15] = (byte)(pc >> 8);
        foreach (var b in banks.Keys) hdr[18 + b] = 1;
        hdr[135] = 3; hdr[136] = 1; hdr[137] = 10;

        var ms = new MemoryStream();
        ms.Write(hdr);
        foreach (var b in new[] { 5, 2, 0, 1, 3, 4 }.Concat(Enumerable.Range(6, 106)))
            if (banks.TryGetValue(b, out var data8)) ms.Write(data8);
        return ms.ToArray();
    }
}
