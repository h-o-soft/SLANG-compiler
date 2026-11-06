using System.Buffers.Binary;
using System.Text;

namespace SLANGCompiler.SlfsPack;

/// <summary>
/// Hu-BASIC (CZ-8FB01) と同じ形の 2D ディスク (x1native_hufs 環境のディスク。docs/HUFS.md)。
///
/// レコード = 256 バイト (logical sector と同じ番号)。クラスタ = 16 レコード (4 KB)、2D は 80 クラスタ。
///   レコード 0     : IPL (ブートセクタ。<see cref="SlfsBootHeader"/> と同じ形)
///   レコード 1〜13 : $FF
///   レコード 14    : FAT (クラスタ 1 つに 1 バイト。$00 空き、$01〜$4F 次のクラスタ、$80+n 最後で n+1 レコード使用)
///   レコード 15    : 0
///   レコード 16〜31: ディレクトリ (32 バイト × 128 項目。$FF ここから未使用、$00 消したもの)
/// クラスタ 0・1 はシステムの領域 (FAT は 01 8F)。FAT の 80〜127 番は $8F、128〜255 番は $00 (本物の Hu-BASIC のディスクと同じ。
/// 128 番から後ろを $8F にすると、HuDisk が空きを 0 と数える)。
/// </summary>
public sealed class HuDiskImage
{
    public const int RecordSize = 256;
    public const int RecordsPerCluster = 16;
    public const int ClusterSize = RecordSize * RecordsPerCluster;
    public const int Clusters = 80;
    public const int Records = Clusters * RecordsPerCluster;
    public const int FatRecord = 14;
    public const int DirFirstRecord = 16;
    public const int DirRecords = 16;
    public const int EntrySize = 32;
    public const int MaxEntries = DirRecords * RecordSize / EntrySize;
    /// <summary>ファイルを置けるはじめのクラスタ (0・1 はシステム)</summary>
    public const int FirstDataCluster = 2;

    public const byte TypeBin = 0x01;
    public const byte TypeBas = 0x02;
    public const byte TypeAsc = 0x04;
    public const byte AttrProtect = 0x40;
    public const byte EntryEnd = 0xFF;
    public const byte EntryDeleted = 0x00;

    private readonly byte[] _raw;

    private HuDiskImage(byte[] raw) { _raw = raw; }

    /// <summary>何も入っていないディスク (IPL は空)</summary>
    public static HuDiskImage CreateBlank()
    {
        var raw = new byte[Records * RecordSize];
        for (int r = 1; r <= 13; r++) Fill(raw, r, 0xFF);
        for (int r = DirFirstRecord; r < DirFirstRecord + DirRecords; r++) Fill(raw, r, 0xFF);
        int fat = FatRecord * RecordSize;
        raw[fat + 0] = 0x01;
        raw[fat + 1] = 0x8F;
        for (int c = Clusters; c < 128; c++) raw[fat + c] = 0x8F;
        return new HuDiskImage(raw);
    }

    /// <summary>レコードの並び (1280 × 256 バイト) から</summary>
    public static HuDiskImage FromRaw(byte[] raw)
    {
        if (raw.Length != Records * RecordSize)
            throw new ArgumentException($"ディスクの大きさが 2D ({Records * RecordSize} バイト) ではありません: {raw.Length}");
        return new HuDiskImage((byte[])raw.Clone());
    }

    /// <summary>D88 (2D) から</summary>
    public static HuDiskImage FromD88(byte[] d88)
    {
        var r = new D88Reader(d88);
        var g = D88Format.Geometry.Standard2D;
        var raw = new byte[Records * RecordSize];
        for (int rec = 0; rec < Records; rec++)
        {
            var (cyl, head, sec) = g.LogicalToChs(rec);
            var data = r.ReadSector(cyl, head, sec);
            if (data.Length != RecordSize)
                throw new InvalidDataException($"レコード {rec} の大きさが {data.Length} バイトです (256 のはず)");
            data.CopyTo(raw, rec * RecordSize);
        }
        return new HuDiskImage(raw);
    }

    public byte[] ToRaw() => (byte[])_raw.Clone();

    public byte[] ToD88(string diskName = "")
    {
        var w = new D88Writer(D88Format.Geometry.Standard2D).SetDiskName(diskName);
        var buf = new byte[RecordSize];
        for (int rec = 0; rec < Records; rec++)
        {
            Array.Copy(_raw, rec * RecordSize, buf, 0, RecordSize);
            w.WriteSector(rec, buf);
        }
        return w.Build();
    }

    /// <summary>
    /// 元の D88 (このディスクを読んだもの) の、レコードの中身だけを今のものに入れ替えて返す。
    /// 見出し・セクタの見出し (密度・状態など)・後ろにつながった別のディスク (複数ボリュームの D88) はそのまま残す
    /// </summary>
    public byte[] PatchD88(byte[] original)
    {
        var o = (byte[])original.Clone();
        var g = D88Format.Geometry.Standard2D;
        for (int rec = 0; rec < Records; rec++)
        {
            var (cyl, head, sec) = g.LogicalToChs(rec);
            int pos = FindSectorData(o, cyl, head, sec);
            Array.Copy(_raw, rec * RecordSize, o, pos, RecordSize);
        }
        return o;
    }

    /// <summary>最初のボリュームの中で、セクタ (cyl, head, sec) の中身の位置。256 バイトでなければ InvalidDataException</summary>
    private static int FindSectorData(byte[] img, int cyl, int head, int sec)
    {
        int track = BinaryPrimitives.ReadInt32LittleEndian(img.AsSpan(D88Format.TrackOffsetTableOffset + (2 * cyl + head) * 4, 4));
        if (track <= 0 || track >= img.Length)
            throw new InvalidDataException($"D88 にトラック (cyl {cyl} head {head}) がありません");
        int count = BinaryPrimitives.ReadUInt16LittleEndian(img.AsSpan(track + 4, 2));
        int pos = track;
        for (int i = 0; i < count && pos + D88Format.SectorHeaderSize <= img.Length; i++)
        {
            int size = BinaryPrimitives.ReadUInt16LittleEndian(img.AsSpan(pos + 0x0E, 2));
            if (img[pos] == cyl && img[pos + 1] == head && img[pos + 2] == sec)
            {
                if (size != RecordSize)
                    throw new InvalidDataException($"セクタ (cyl {cyl} head {head} sec {sec}) が {size} バイトです (256 のはず)");
                return pos + D88Format.SectorHeaderSize;
            }
            pos += D88Format.SectorHeaderSize + size;
        }
        throw new InvalidDataException($"D88 にセクタ (cyl {cyl} head {head} sec {sec}) がありません");
    }

    public byte[] ReadRecord(int rec) => _raw.AsSpan(rec * RecordSize, RecordSize).ToArray();

    // ------------------------------------------------------------------ FAT

    public byte Fat(int cluster) => _raw[FatRecord * RecordSize + cluster];
    private void SetFat(int cluster, byte v) => _raw[FatRecord * RecordSize + cluster] = v;

    /// <summary>空いているクラスタの数</summary>
    public int FreeClusters()
    {
        int n = 0;
        for (int c = FirstDataCluster; c < Clusters; c++) if (Fat(c) == 0) n++;
        return n;
    }

    /// <summary>
    /// 鎖をたどる: (クラスタの並び, 最後のクラスタで使っているレコード数)。
    /// 輪になる・範囲外・空きを指す鎖は InvalidDataException
    /// </summary>
    public (List<int> Chain, int LastRecords) Chain(int start)
    {
        var chain = new List<int>();
        int c = start;
        while (true)
        {
            if (c < FirstDataCluster || c >= Clusters)
                throw new InvalidDataException($"FAT の鎖が範囲外のクラスタ {c} を指しています");
            if (chain.Count >= Clusters)
                throw new InvalidDataException("FAT の鎖が輪になっています");
            chain.Add(c);
            byte v = Fat(c);
            if (v == 0)
                throw new InvalidDataException($"FAT の鎖が空きのクラスタ {c} を通ります");
            if (v >= 0x80)
            {
                int last = (v & 0x7F) + 1;
                if (last > RecordsPerCluster)
                    throw new InvalidDataException($"クラスタ {c} の最後の印 ${v:X2} が 16 レコードを越えています");
                return (chain, last);
            }
            c = v;
        }
    }

    /// <summary>空きのクラスタを番号の小さい方から n 個 (足りなければ IOException)</summary>
    private List<int> AllocateLowest(int n)
    {
        var got = new List<int>();
        for (int c = FirstDataCluster; c < Clusters && got.Count < n; c++)
            if (Fat(c) == 0) got.Add(c);
        if (got.Count < n) throw new IOException($"ディスクがいっぱいです (要るクラスタ {n}、空き {FreeClusters()})");
        return got;
    }

    /// <summary>番号の続いた空きのクラスタを n 個 (IPL で読む本体用。なければ IOException)</summary>
    private List<int> AllocateContiguous(int n)
    {
        for (int start = FirstDataCluster; start + n <= Clusters; start++)
        {
            bool ok = true;
            for (int c = start; c < start + n; c++) if (Fat(c) != 0) { ok = false; break; }
            if (ok) return Enumerable.Range(start, n).ToList();
        }
        throw new IOException($"続いた空きのクラスタが {n} 個ありません");
    }

    private void Link(List<int> chain, int lastRecords)
    {
        for (int i = 0; i < chain.Count - 1; i++) SetFat(chain[i], (byte)chain[i + 1]);
        SetFat(chain[^1], (byte)(0x80 + lastRecords - 1));
    }

    // ------------------------------------------------------------------ ディレクトリ

    private int EntryOffset(int index) => DirFirstRecord * RecordSize + index * EntrySize;

    /// <summary>使っている項目 (消したもの・終わりの後ろは除く)</summary>
    public List<HuDirEntry> Entries()
    {
        var list = new List<HuDirEntry>();
        for (int i = 0; i < MaxEntries; i++)
        {
            byte t = _raw[EntryOffset(i)];
            if (t == EntryEnd) break;
            if (t == EntryDeleted) continue;
            list.Add(HuDirEntry.Parse(i, _raw.AsSpan(EntryOffset(i), EntrySize)));
        }
        return list;
    }

    public HuDirEntry? Find(string name)
    {
        var (n, e) = HuName.Parse(name);
        return Entries().FirstOrDefault(x => x.NameBytes.AsSpan().SequenceEqual(n) && x.ExtBytes.AsSpan().SequenceEqual(e));
    }

    /// <summary>空き項目 (消したもの、なければ終わりの所) の番号。ないなら IOException</summary>
    private int FreeEntryIndex()
    {
        for (int i = 0; i < MaxEntries; i++)
        {
            byte t = _raw[EntryOffset(i)];
            if (t == EntryDeleted || t == EntryEnd) return i;
        }
        throw new IOException("ディレクトリがいっぱいです");
    }

    /// <summary>
    /// 項目を書く。終わりの印 ($FF) の所に書くときは、次の項目に終わりの印を置き直す
    /// (終わりの後ろに古い項目が残っているディスクで、それが見えるようにならないように)
    /// </summary>
    private void WriteEntry(HuDirEntry e)
    {
        bool wasEnd = _raw[EntryOffset(e.Index)] == EntryEnd;
        e.ToBytes().CopyTo(_raw, EntryOffset(e.Index));
        if (wasEnd && e.Index + 1 < MaxEntries)
            _raw.AsSpan(EntryOffset(e.Index + 1), EntrySize).Fill(EntryEnd);
    }

    // ------------------------------------------------------------------ ファイル

    /// <summary>ファイルを足す時の中身の種類</summary>
    public enum AddKind
    {
        /// <summary>中身そのまま、種類 Asc (大きさの欄 0。長さはレコード単位)</summary>
        Raw,
        /// <summary>LF・CRLF を CR にし、終わりに $1A を付ける。種類 Asc (Hu-BASIC の順ファイルと同じ形)</summary>
        Text,
        /// <summary>中身そのまま、種類 Bin。大きさの欄に長さ (64 KB 未満)</summary>
        Bin,
    }

    /// <summary>
    /// ファイルを足す。クラスタは空きのうち番号の小さい方から取る (Hu-BASIC と同じ)。
    /// 同じ名前があれば InvalidOperationException
    /// </summary>
    public HuDirEntry AddFile(string name, byte[] data, AddKind kind = AddKind.Raw, ushort load = 0, ushort exec = 0)
    {
        if (Find(name) != null) throw new InvalidOperationException($"同じ名前のファイルがあります: {name}");
        var (n, e) = HuName.Parse(name);
        byte[] body = kind == AddKind.Text ? ToHuText(data) : data;
        if (kind == AddKind.Bin && body.Length > 0xFFFF)
            throw new ArgumentException($"Bin のファイルは 65535 バイトまでです: {name} ({body.Length} バイト)");
        int index = FreeEntryIndex();
        var (chain, last) = PlaceData(body, contiguous: false);
        var entry = new HuDirEntry
        {
            Index = index,
            Type = kind == AddKind.Bin ? TypeBin : TypeAsc,
            NameBytes = n,
            ExtBytes = e,
            Size = kind == AddKind.Bin ? (ushort)body.Length : (ushort)0,
            LoadAddress = load,
            ExecAddress = exec,
            StartCluster = (byte)chain[0],
        };
        WriteEntry(entry);
        return entry;
    }

    /// <summary>
    /// IPL で起動する本体を置く。番号の続いたクラスタに置き (IPL ROM は続いたレコードしか読めない)、
    /// 種類 Bin・書き込み禁止の項目を作り、レコード 0 の IPL から指す
    /// </summary>
    public HuDirEntry SetMain(string mainName, byte[] body, ushort load, ushort exec)
    {
        if (body.Length == 0 || body.Length > 0xFFFF)
            throw new ArgumentException($"本体の大きさは 1〜65535 バイトです: {body.Length}");
        if (Find(mainName + ".Sys") != null) throw new InvalidOperationException($"同じ名前のファイルがあります: {mainName}.Sys");
        var (n, e) = HuName.Parse(mainName + ".Sys");
        int index = FreeEntryIndex();
        var (chain, _) = PlaceData(body, contiguous: true);
        var entry = new HuDirEntry
        {
            Index = index,
            Type = TypeBin | AttrProtect,
            NameBytes = n,
            ExtBytes = e,
            Size = (ushort)body.Length,
            LoadAddress = load,
            ExecAddress = exec,
            StartCluster = (byte)chain[0],
        };
        WriteEntry(entry);
        var ipl = new SlfsBootHeader
        {
            FileName = mainName,
            Extension = "Sys",
            DataSize = (ushort)body.Length,
            LoadAddress = load,
            ExecuteAddress = exec,
            DiskOffset = chain[0] * ClusterSize,
        };
        var rec0 = new byte[RecordSize];
        ipl.ToBytes().CopyTo(rec0, 0);
        rec0.CopyTo(_raw, 0);
        return entry;
    }

    /// <summary>データを置いて鎖を作る。空のデータも 1 クラスタ・1 レコードを取る (Hu-BASIC と同じ)</summary>
    private (List<int> Chain, int LastRecords) PlaceData(byte[] body, bool contiguous)
    {
        int records = Math.Max(1, (body.Length + RecordSize - 1) / RecordSize);
        int clusters = (records + RecordsPerCluster - 1) / RecordsPerCluster;
        int last = records - (clusters - 1) * RecordsPerCluster;
        var chain = contiguous ? AllocateContiguous(clusters) : AllocateLowest(clusters);
        for (int i = 0; i < records; i++)
        {
            int rec = chain[i / RecordsPerCluster] * RecordsPerCluster + i % RecordsPerCluster;
            var span = _raw.AsSpan(rec * RecordSize, RecordSize);
            span.Clear();
            int off = i * RecordSize;
            if (off < body.Length)
                body.AsSpan(off, Math.Min(RecordSize, body.Length - off)).CopyTo(span);
        }
        Link(chain, last);
        return (chain, last);
    }

    /// <summary>消す (種類を $00 にして、クラスタを空きに戻す)。書き込み禁止は InvalidOperationException</summary>
    public void Delete(string name)
    {
        var e = Find(name) ?? throw new FileNotFoundException($"ファイルがありません: {name}");
        if ((e.Type & AttrProtect) != 0) throw new InvalidOperationException($"書き込み禁止のファイルです: {name}");
        var (chain, _) = Chain(e.StartCluster);
        _raw[EntryOffset(e.Index)] = EntryDeleted;
        foreach (var c in chain) SetFat(c, 0);
    }

    /// <summary>
    /// 中身を取り出す。長さ: Bin は大きさの欄 (0 なら空)、それ以外はレコード単位 (鎖の長さ × 256)。
    /// text が真なら、$1A の前までにして CR を LF にする
    /// </summary>
    public byte[] Extract(string name, bool text = false)
    {
        var e = Find(name) ?? throw new FileNotFoundException($"ファイルがありません: {name}");
        var data = ReadChain(e);
        if (!text) return data;
        int end = Array.IndexOf(data, (byte)0x1A);
        if (end < 0) end = data.Length;
        var o = new byte[end];
        for (int i = 0; i < end; i++) o[i] = data[i] == 0x0D ? (byte)0x0A : data[i];
        return o;
    }

    private byte[] ReadChain(HuDirEntry e)
    {
        var (chain, last) = Chain(e.StartCluster);
        int records = (chain.Count - 1) * RecordsPerCluster + last;
        var all = new byte[records * RecordSize];
        for (int i = 0; i < records; i++)
        {
            int rec = chain[i / RecordsPerCluster] * RecordsPerCluster + i % RecordsPerCluster;
            Array.Copy(_raw, rec * RecordSize, all, i * RecordSize, RecordSize);
        }
        if ((e.Type & 0x07) == TypeBin)
        {
            int len = Math.Min(e.Size, all.Length);
            return all.AsSpan(0, len).ToArray();
        }
        return all;
    }

    /// <summary>LF・CRLF を CR にし、終わりに $1A を 1 つ付ける</summary>
    public static byte[] ToHuText(byte[] data)
    {
        var o = new List<byte>(data.Length + 1);
        for (int i = 0; i < data.Length; i++)
        {
            byte b = data[i];
            if (b == 0x0D && i + 1 < data.Length && data[i + 1] == 0x0A) continue;
            o.Add(b == 0x0A ? (byte)0x0D : b);
        }
        o.Add(0x1A);
        return o.ToArray();
    }

    private static void Fill(byte[] raw, int rec, byte v) => raw.AsSpan(rec * RecordSize, RecordSize).Fill(v);
}

/// <summary>ディレクトリの 1 項目 (32 バイト)。知らない欄 (+1D・+1F) も持ち、書くときにそのまま戻す</summary>
public sealed class HuDirEntry
{
    public int Index { get; set; }
    /// <summary>+00 種類と属性 (下 3 ビットが種類、$40 が書き込み禁止)</summary>
    public byte Type { get; set; }
    public byte[] NameBytes { get; set; } = new byte[13];
    public byte[] ExtBytes { get; set; } = new byte[3];
    public byte Password { get; set; } = 0x20;
    public ushort Size { get; set; }
    public ushort LoadAddress { get; set; }
    public ushort ExecAddress { get; set; }
    public byte[] Date { get; set; } = new byte[5];
    public byte Byte1D { get; set; }
    public byte StartCluster { get; set; }
    public byte Byte1F { get; set; }

    /// <summary>表示用の名前 ("名前.拡張子"。後ろの空白を除く。拡張子が空白なら名前だけ)。X1 の文字コードは ASCII と半角カナで読む</summary>
    public string DisplayName
    {
        get
        {
            var n = HuName.Decode(NameBytes).TrimEnd(' ');
            var e = HuName.Decode(ExtBytes).TrimEnd(' ');
            return e.Length == 0 ? n : n + "." + e;
        }
    }

    public static HuDirEntry Parse(int index, ReadOnlySpan<byte> b) => new()
    {
        Index = index,
        Type = b[0],
        NameBytes = b.Slice(0x01, 13).ToArray(),
        ExtBytes = b.Slice(0x0E, 3).ToArray(),
        Password = b[0x11],
        Size = BinaryPrimitives.ReadUInt16LittleEndian(b.Slice(0x12)),
        LoadAddress = BinaryPrimitives.ReadUInt16LittleEndian(b.Slice(0x14)),
        ExecAddress = BinaryPrimitives.ReadUInt16LittleEndian(b.Slice(0x16)),
        Date = b.Slice(0x18, 5).ToArray(),
        Byte1D = b[0x1D],
        StartCluster = b[0x1E],
        Byte1F = b[0x1F],
    };

    public byte[] ToBytes()
    {
        var b = new byte[HuDiskImage.EntrySize];
        b[0] = Type;
        NameBytes.CopyTo(b, 0x01);
        ExtBytes.CopyTo(b, 0x0E);
        b[0x11] = Password;
        BinaryPrimitives.WriteUInt16LittleEndian(b.AsSpan(0x12), Size);
        BinaryPrimitives.WriteUInt16LittleEndian(b.AsSpan(0x14), LoadAddress);
        BinaryPrimitives.WriteUInt16LittleEndian(b.AsSpan(0x16), ExecAddress);
        Date.CopyTo(b, 0x18);
        b[0x1D] = Byte1D;
        b[0x1E] = StartCluster;
        b[0x1F] = Byte1F;
        return b;
    }
}

/// <summary>
/// ファイル名の決まり (ランタイムと同じ。docs/HUFS.md): 頭の "0:" は飛ばす。最後の '.' で名前と拡張子に分ける
/// ('.' がなければ拡張子は空白)。名前 1〜13 バイト、拡張子 0〜3 バイト (超えたら切らずにエラー)。
/// 使えない字は $00〜$1F・'"'・':'。ホストの側は ASCII の印字できる字と半角カナ (X1 の $A1〜$DF) を受ける。
/// 比べるときは空白で埋めた 13+3 バイトを、大文字小文字を区別して比べる
/// </summary>
public static class HuName
{
    public static (byte[] Name, byte[] Ext) Parse(string s)
    {
        if (s.StartsWith("0:", StringComparison.Ordinal)) s = s[2..];
        var bytes = Encode(s);
        int dot = Array.LastIndexOf(bytes, (byte)'.');
        var name = dot < 0 ? bytes : bytes[..dot];
        var ext = dot < 0 ? Array.Empty<byte>() : bytes[(dot + 1)..];
        if (name.Length < 1 || name.Length > 13)
            throw new ArgumentException($"ファイル名は 1〜13 文字です: '{s}'");
        if (ext.Length > 3)
            throw new ArgumentException($"拡張子は 3 文字までです: '{s}'");
        return (Pad(name, 13), Pad(ext, 3));
    }

    private static byte[] Encode(string s)
    {
        var o = new byte[s.Length];
        for (int i = 0; i < s.Length; i++)
        {
            char ch = s[i];
            byte b;
            if (ch >= 0x20 && ch <= 0x7E) b = (byte)ch;
            else if (ch >= '｡' && ch <= 'ﾟ') b = (byte)(0xA1 + (ch - '｡'));
            else throw new ArgumentException($"ファイル名に使えない字があります: '{s}'");
            if (b == (byte)'"' || b == (byte)':')
                throw new ArgumentException($"ファイル名に使えない字があります: '{s}'");
            o[i] = b;
        }
        return o;
    }

    public static string Decode(byte[] b)
    {
        var sb = new StringBuilder(b.Length);
        foreach (var x in b)
            sb.Append(x >= 0xA1 && x <= 0xDF ? (char)('｡' + (x - 0xA1)) : x >= 0x20 && x <= 0x7E ? (char)x : '?');
        return sb.ToString();
    }

    private static byte[] Pad(byte[] b, int len)
    {
        var o = Enumerable.Repeat((byte)' ', len).ToArray();
        b.CopyTo(o, 0);
        return o;
    }
}
