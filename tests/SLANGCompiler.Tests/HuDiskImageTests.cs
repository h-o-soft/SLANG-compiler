using Xunit;
using SLANGCompiler.SlfsPack;
using System.Text;

namespace SLANGCompiler.Tests;

/// <summary>Hu-BASIC と同じ形のディスク (x1native_hufs) の道具の試験</summary>
public class HuDiskImageTests
{
    private const int R = HuDiskImage.RecordSize;

    private static byte[] Rec(HuDiskImage d, int rec) => d.ReadRecord(rec);

    [Fact]
    public void Blank_Layout_MatchesRealDisk()
    {
        var d = HuDiskImage.CreateBlank();
        for (int r = 1; r <= 13; r++) Assert.All(Rec(d, r), b => Assert.Equal(0xFF, b));
        var fat = Rec(d, 14);
        Assert.Equal(0x01, fat[0]);
        Assert.Equal(0x8F, fat[1]);
        for (int c = 2; c < 80; c++) Assert.Equal(0x00, fat[c]);
        for (int c = 80; c < 128; c++) Assert.Equal(0x8F, fat[c]);
        for (int c = 128; c < 256; c++) Assert.Equal(0x00, fat[c]);
        Assert.All(Rec(d, 15), b => Assert.Equal(0x00, b));
        for (int r = 16; r < 32; r++) Assert.All(Rec(d, r), b => Assert.Equal(0xFF, b));
        Assert.Equal(78, d.FreeClusters());
        Assert.Empty(d.Entries());
    }

    [Theory]
    [InlineData(0, 1, 0x80)]
    [InlineData(1, 1, 0x80)]
    [InlineData(255, 1, 0x80)]
    [InlineData(256, 1, 0x80)]
    [InlineData(257, 1, 0x81)]
    [InlineData(4095, 1, 0x8F)]
    [InlineData(4096, 1, 0x8F)]
    [InlineData(4097, 2, 0x80)]
    public void AddRaw_ChainAndLastMarker(int length, int clusters, int lastMarker)
    {
        var d = HuDiskImage.CreateBlank();
        var data = Enumerable.Range(0, length).Select(i => (byte)(i * 7)).ToArray();
        var e = d.AddFile("F.DAT", data);
        Assert.Equal(HuDiskImage.TypeAsc, e.Type);
        Assert.Equal(0, e.Size);
        var (chain, _) = d.Chain(e.StartCluster);
        Assert.Equal(clusters, chain.Count);
        Assert.Equal(lastMarker, d.Fat(chain[^1]));
        // Asc はレコード単位で取り出す (足りない所は 0)
        var back = d.Extract("F.DAT");
        Assert.Equal(0, back.Length % R);
        Assert.Equal(data, back.AsSpan(0, length).ToArray());
        Assert.All(back.Skip(length), b => Assert.Equal(0, b));
    }

    [Theory]
    [InlineData(0)]
    [InlineData(1)]
    [InlineData(256)]
    [InlineData(4097)]
    [InlineData(65535)]
    public void AddBin_SizeFieldAndExactExtract(int length)
    {
        var d = HuDiskImage.CreateBlank();
        var data = Enumerable.Range(0, length).Select(i => (byte)i).ToArray();
        var e = d.AddFile("B.BIN", data, HuDiskImage.AddKind.Bin, 0x1000, 0x2000);
        Assert.Equal(HuDiskImage.TypeBin, e.Type);
        Assert.Equal(length, e.Size);
        Assert.Equal(0x1000, e.LoadAddress);
        Assert.Equal(0x2000, e.ExecAddress);
        Assert.Equal(data, d.Extract("B.BIN"));
    }

    [Fact]
    public void AddBin_TooLarge_Throws()
    {
        var d = HuDiskImage.CreateBlank();
        Assert.Throws<ArgumentException>(() => d.AddFile("B.BIN", new byte[65536], HuDiskImage.AddKind.Bin));
    }

    [Fact]
    public void AddText_ConvertsNewlinesAndAppendsEof()
    {
        var d = HuDiskImage.CreateBlank();
        d.AddFile("T.TXT", Encoding.ASCII.GetBytes("A\nB\r\nC"), HuDiskImage.AddKind.Text);
        var raw = d.Extract("T.TXT");
        Assert.Equal(Encoding.ASCII.GetBytes("A\rB\rC\x1A"), raw.AsSpan(0, 6).ToArray());
        Assert.Equal("A\nB\nC", Encoding.ASCII.GetString(d.Extract("T.TXT", text: true)));
    }

    [Fact]
    public void Allocation_LowestFreeFirst_Fragments()
    {
        var d = HuDiskImage.CreateBlank();
        d.AddFile("A", new byte[4097]);     // クラスタ 2・3
        d.AddFile("B", new byte[1]);        // 4
        d.Delete("A");                      // 2・3 が空く
        var c = d.AddFile("C", new byte[3 * 4096]);
        Assert.Equal(new[] { 2, 3, 5 }, d.Chain(c.StartCluster).Chain);
        // 消した項目 (番号 0) を使い回す
        Assert.Equal(0, c.Index);
    }

    [Fact]
    public void DiskFull_Throws()
    {
        var d = HuDiskImage.CreateBlank();
        d.AddFile("BIG", new byte[78 * 4096]);
        Assert.Equal(0, d.FreeClusters());
        Assert.Throws<IOException>(() => d.AddFile("ONE", new byte[1]));
    }

    [Fact]
    public void Directory_Holds128_ThenThrows()
    {
        var d = HuDiskImage.CreateBlank();
        for (int i = 0; i < 78; i++) d.AddFile($"F{i}", new byte[1]);
        Assert.Throws<IOException>(() => d.AddFile("F78", new byte[1]));   // クラスタが尽きる方が先
        // ディレクトリだけを試す: 消しても項目の数は増えないので、別に 128 個の項目を作る
        var e = HuDiskImage.CreateBlank();
        var raw = e.ToRaw();
        for (int i = 0; i < 128; i++)
        {
            int off = 16 * R + i * 32;
            raw[off] = HuDiskImage.TypeAsc;
            Encoding.ASCII.GetBytes($"X{i,-12}").CopyTo(raw, off + 1);
            Encoding.ASCII.GetBytes("   ").CopyTo(raw, off + 14);
            raw[off + 0x1E] = 2;
        }
        raw[14 * R + 2] = 0x80;
        var full = HuDiskImage.FromRaw(raw);
        Assert.Equal(128, full.Entries().Count);
        Assert.Throws<IOException>(() => full.AddFile("Y", new byte[1]));
    }

    [Theory]
    [InlineData("ABCDEFGHIJKLM.DAT")]
    [InlineData("NOEXT")]
    [InlineData("0:abc.dat")]
    [InlineData("Start up.Bas")]
    [InlineData("ｶﾅ.TXT")]
    public void Name_Accepted(string name)
    {
        var d = HuDiskImage.CreateBlank();
        d.AddFile(name, new byte[1]);
        Assert.NotNull(d.Find(name));
    }

    [Theory]
    [InlineData("ABCDEFGHIJKLMN.DAT")]
    [InlineData("Q.DATX")]
    [InlineData(".DAT")]
    [InlineData("A\"B")]
    [InlineData("A:B")]
    [InlineData("あ")]
    public void Name_Rejected(string name)
    {
        var d = HuDiskImage.CreateBlank();
        Assert.Throws<ArgumentException>(() => d.AddFile(name, new byte[1]));
    }

    [Fact]
    public void Name_CaseSensitive_AndDuplicateRejected()
    {
        var d = HuDiskImage.CreateBlank();
        d.AddFile("abc.dat", new byte[1]);
        Assert.Null(d.Find("ABC.DAT"));
        Assert.NotNull(d.Find("0:abc.dat"));
        Assert.Throws<InvalidOperationException>(() => d.AddFile("abc.dat", new byte[1]));
    }

    [Fact]
    public void Chain_Broken_Throws()
    {
        var raw = HuDiskImage.CreateBlank().ToRaw();
        raw[14 * R + 2] = 3;
        raw[14 * R + 3] = 2;                // 輪
        raw[14 * R + 4] = 90;               // 範囲外
        raw[14 * R + 5] = 6;                // 空きを指す
        var d = HuDiskImage.FromRaw(raw);
        Assert.Throws<InvalidDataException>(() => d.Chain(2));
        Assert.Throws<InvalidDataException>(() => d.Chain(4));
        Assert.Throws<InvalidDataException>(() => d.Chain(5));
    }

    [Fact]
    public void SetMain_ContiguousFromCluster2_WithIplAndProtect()
    {
        var d = HuDiskImage.CreateBlank();
        var body = Enumerable.Range(0, 9000).Select(i => (byte)i).ToArray();
        var e = d.SetMain("GAME", body, 0x1000, 0x1000);
        Assert.Equal(HuDiskImage.TypeBin | HuDiskImage.AttrProtect, e.Type);
        Assert.Equal(new[] { 2, 3, 4 }, d.Chain(e.StartCluster).Chain);
        var ipl = Rec(d, 0);
        Assert.Equal(0x01, ipl[0]);
        Assert.Equal("GAME         Sys", Encoding.ASCII.GetString(ipl, 1, 16));
        Assert.Equal(9000, ipl[0x12] | ipl[0x13] << 8);
        Assert.Equal(2 * 4096, ipl[0x1D] | ipl[0x1E] << 8 | ipl[0x1F] << 16);
        Assert.Equal(body, d.Extract("GAME.Sys"));
        Assert.Throws<InvalidOperationException>(() => d.Delete("GAME.Sys"));
    }

    [Fact]
    public void D88_RoundTrip()
    {
        var d = HuDiskImage.CreateBlank();
        d.SetMain("M", new byte[300], 0x1000, 0x1000);
        d.AddFile("T.TXT", Encoding.ASCII.GetBytes("HI\n"), HuDiskImage.AddKind.Text);
        var back = HuDiskImage.FromD88(d.ToD88("X"));
        Assert.Equal(d.ToRaw(), back.ToRaw());
    }

    [Fact]
    public void Delete_UnknownBytesKeptOnOtherEntries_AndClustersFreed()
    {
        var d = HuDiskImage.CreateBlank();
        d.AddFile("A", new byte[5000]);
        d.AddFile("B", new byte[1]);
        int before = d.FreeClusters();
        d.Delete("A");
        Assert.Equal(before + 2, d.FreeClusters());
        Assert.Null(d.Find("A"));
        Assert.NotNull(d.Find("B"));
        Assert.Throws<FileNotFoundException>(() => d.Delete("A"));
    }

    /// <summary>
    /// 本物の Hu-BASIC (CZ-8FB01) が書いたディスクから、自作の試験で書いたファイルの項目・FAT・中身だけを写したもの。
    /// (日付は本物が書いた値。中身は最初のレコードの頭だけ写し、残りは 0)
    /// </summary>
    private static HuDiskImage RealWrittenSample()
    {
        var raw = HuDiskImage.CreateBlank().ToRaw();
        var files = new (string Entry, string Head)[]
        {
            ("04616263202020202020202020206461742000000000000026b4051140000400", "4c4f570d1a"),
            ("04454d50545920202020202020204441542000000000000026b4051140000500", "1a"),
            ("044142434445464748494a4b4c4d4441542000000000000026b4051140000600", "4c31330d1a"),
            ("044e4f45585420202020202020202020202000000000000026b4051140000700", "4e450d1a"),
            ("04434f4c532020202020202020204441542000000000000026b4051140000800",
             Convert.ToHexString(Encoding.ASCII.GetBytes("A       B           C\r 1  2   -3 \rPQ\r\x1A"))),
            ("04495420202020202020202020204441542000000000000026b4051140000900", "582c592c5a0d570d1a"),
        };
        for (int i = 0; i < files.Length; i++)
        {
            var entry = Convert.FromHexString(files[i].Entry);
            entry.CopyTo(raw, 16 * R + i * 32);
            int cluster = entry[0x1E];
            raw[14 * R + cluster] = 0x80;
            Convert.FromHexString(files[i].Head).CopyTo(raw, cluster * 4096);
        }
        return HuDiskImage.FromRaw(raw);
    }

    [Fact]
    public void RealWritten_ReadsLikeHuBasic()
    {
        var d = RealWrittenSample();
        Assert.Equal(new[] { "abc.dat", "EMPTY.DAT", "ABCDEFGHIJKLM.DAT", "NOEXT", "COLS.DAT", "IT.DAT" },
                     d.Entries().Select(e => e.DisplayName).ToArray());
        string Text(string n) => Encoding.ASCII.GetString(d.Extract(n, text: true));
        Assert.Equal("LOW\n", Text("abc.dat"));
        Assert.Equal("", Text("EMPTY.DAT"));
        Assert.Equal("L13\n", Text("ABCDEFGHIJKLM.DAT"));
        Assert.Equal("NE\n", Text("NOEXT"));
        Assert.Equal("A       B           C\n 1  2   -3 \nPQ\n", Text("COLS.DAT"));
        Assert.Equal("X,Y,Z\nW\n", Text("IT.DAT"));
        Assert.Null(d.Find("ABC.DAT"));
        // Asc はレコード単位 (1 レコード)
        Assert.Equal(256, d.Extract("abc.dat").Length);
        // 足しても壊さない (知らない欄・日付はそのまま)
        d.AddFile("NEW.DAT", new byte[10]);
        var abc = d.Find("abc.dat")!;
        Assert.Equal(new byte[] { 0x26, 0xB4, 0x05, 0x11, 0x40 }, abc.Date);
    }

    [Fact]
    public void PatchD88_KeepsHeaderSectorHeadersAndFollowingVolume()
    {
        var d = HuDiskImage.CreateBlank();
        d.AddFile("A.DAT", new byte[10]);
        var vol1 = d.ToD88("VOL1");
        vol1[0x1A] = 0x00;
        // 1 つめのセクタの見出しの状態 (+08) を変えておく
        int track0 = vol1[0x20] | vol1[0x21] << 8 | vol1[0x22] << 16;
        vol1[track0 + 8] = 0x10;
        var vol2 = HuDiskImage.CreateBlank().ToD88("VOL2");
        var multi = vol1.Concat(vol2).ToArray();

        var e = HuDiskImage.FromD88(multi);
        e.AddFile("B.DAT", new byte[20]);
        var patched = e.PatchD88(multi);

        Assert.Equal(multi.Length, patched.Length);
        Assert.Equal(vol2, patched.AsSpan(vol1.Length).ToArray());
        Assert.Equal("VOL1", Encoding.ASCII.GetString(patched, 0, 4));
        Assert.Equal(0x10, patched[track0 + 8]);
        var back = HuDiskImage.FromD88(patched);
        Assert.NotNull(back.Find("A.DAT"));
        Assert.NotNull(back.Find("B.DAT"));
    }

    [Fact]
    public void AddAtEndMarker_DoesNotExposeStaleEntriesBehindIt()
    {
        var raw = HuDiskImage.CreateBlank().ToRaw();
        // 項目 0 が終わりの印、その後ろに古い項目が残っているディスク
        int off1 = 16 * R + 32;
        raw[off1] = HuDiskImage.TypeAsc;
        Encoding.ASCII.GetBytes("OLD          DAT").CopyTo(raw, off1 + 1);
        raw[off1 + 0x1E] = 2;
        var d = HuDiskImage.FromRaw(raw);
        Assert.Empty(d.Entries());
        d.AddFile("NEW.DAT", new byte[1]);
        Assert.Equal(new[] { "NEW.DAT" }, d.Entries().Select(x => x.DisplayName).ToArray());
        Assert.Equal(HuDiskImage.EntryEnd, d.ReadRecord(16)[32]);
    }
}
