using SLANGCompiler.Build;
using Xunit;

namespace SLANGCompiler.Tests;

/// <summary>NexImage (slangbuild --emit nex) の単体テスト。頭・バンクの置き方・//#PAGE・重なりのエラー</summary>
public class NexImageTests : IDisposable
{
    private readonly string _dir = Path.Combine(Path.GetTempPath(), "nextest_" + Guid.NewGuid().ToString("N"));
    public NexImageTests() => Directory.CreateDirectory(_dir);
    public void Dispose() { try { Directory.Delete(_dir, true); } catch { } }

    private static byte[] Bank(byte[] nex, int bank)
    {
        // バンクは NEX の順 (5, 2, 0, 1, 3, 4, 6, …) で、入れたものだけ
        int pos = 512;
        foreach (var b in new[] { 5, 2, 0, 1, 3, 4 }.Concat(Enumerable.Range(6, 106)))
        {
            if (nex[18 + b] == 0) continue;
            if (b == bank) return nex[pos..(pos + 0x4000)];
            pos += 0x4000;
        }
        throw new KeyNotFoundException($"bank {bank}");
    }

    [Fact]
    public void Header_MainAndModules()
    {
        var main = Enumerable.Range(0, 0x4100).Select(i => (byte)(i + 1)).ToArray();   // 8000H〜C0FFH (バンク 2 と 0 にまたがる)
        var mods = new[] { new NexImage.Module(0, 0xC000, 40, new byte[] { 0xC9, 0x01 }), new NexImage.Module(1, 0x0010, 30, new byte[] { 0xAA }) };
        var nex = NexImage.Build(main, 0x8000, 0xC000, 0x8000, mods, Array.Empty<NexImage.DataFile>(), 30);
        Assert.Equal("NextV1.2"u8.ToArray(), nex[..8]);
        Assert.Equal(0, nex[8]);                     // 768K
        Assert.Equal(4, nex[9]);                     // バンク 2・0・15 (ページ 30)・20 (ページ 40)
        Assert.Equal(7, nex[11]);
        Assert.Equal(0xC000, nex[12] | nex[13] << 8);
        Assert.Equal(0x8000, nex[14] | nex[15] << 8);
        Assert.Equal(new byte[] { 3, 1, 10 }, nex[135..138]);
        Assert.Equal(512 + 4 * 0x4000, nex.Length);
        Assert.Equal(main[..0x4000], Bank(nex, 2));
        Assert.Equal(main[0x4000..], Bank(nex, 0)[..0x100]);    // C000H〜 はバンク 0 の頭
        Assert.Equal(new byte[] { 0xC9, 0x01 }, Bank(nex, 20)[..2]);   // ページ 40 = バンク 20 の前半
        Assert.Equal(0xAA, Bank(nex, 15)[0x10]);                       // ページ 30 の 10H
    }

    [Fact]
    public void DataPages_ContinueToNextPage_AndRam()
    {
        File.WriteAllText(Path.Combine(_dir, "a.SL"), "//#PAGE 41 BIG.BIN\nMAIN() BEGIN END;\n");
        File.WriteAllBytes(Path.Combine(_dir, "BIG.BIN"), Enumerable.Range(0, 9000).Select(i => (byte)(i * 3)).ToArray());
        var data = NexImage.ReadDataPages(Path.Combine(_dir, "a.SL"));
        Assert.Single(data);
        Assert.Equal((1, 41, "BIG.BIN"), (data[0].Line, data[0].Page, data[0].Name));
        var nex = NexImage.Build(new byte[] { 0 }, 0x8000, 0xE000, 0x8000, Array.Empty<NexImage.Module>(), data, 0);
        Assert.Equal(data[0].Bytes[..0x2000], Bank(nex, 20)[0x2000..]);              // ページ 41 = バンク 20 の後半
        Assert.Equal(data[0].Bytes[0x2000..], Bank(nex, 21)[..(9000 - 0x2000)]);     // 続きはページ 42
        Assert.All(Bank(nex, 20)[..0x2000], b => Assert.Equal(0, b));               // 同じバンクのもう半分は 0
        // 96 以上のページを使えば 1792K
        var big = NexImage.Build(new byte[] { 0 }, 0x8000, 0xE000, 0x8000, new[] { new NexImage.Module(0, 0xC000, 100, new byte[] { 1 }) }, Array.Empty<NexImage.DataFile>(), 0);
        Assert.Equal(1, big[8]);
    }

    [Theory]
    [InlineData("//#PAGE 29 A.BIN", "//#PAGE line 1: write //#PAGE page name, page 30-55, a name only.")]
    [InlineData("//#PAGE 40 dir/A.BIN", "//#PAGE line 1: write //#PAGE page name, page 30-55, a name only.")]
    [InlineData("//#PAGE 40 A.BIN extra", "//#PAGE line 1: write //#PAGE page name, page 30-55, a name only.")]
    [InlineData("//#PAGE 40 NONE.BIN", "Data file NONE.BIN: cannot be read.")]
    [InlineData("//#PAGE 55 TWO.BIN", "Data file TWO.BIN: goes past page 55.")]
    [InlineData("//#PAGE 44 A.BIN\n//#PAGE 44 B.BIN", "Data file B.BIN: overlaps another //#PAGE.")]
    [InlineData("//#PAGE 40 MOD.BIN", "Data file MOD.BIN: overlaps #MODULE 0 (page 40).")]
    [InlineData("//#PAGE 31 LOW.BIN", "Data file LOW.BIN: overlaps __LOWRAM (page 31).")]
    public void DataPages_Errors(string lines, string message)
    {
        foreach (var n in new[] { "A.BIN", "B.BIN", "MOD.BIN", "LOW.BIN" }) File.WriteAllBytes(Path.Combine(_dir, n), new byte[] { 1 });
        File.WriteAllBytes(Path.Combine(_dir, "TWO.BIN"), new byte[0x2001]);
        File.WriteAllText(Path.Combine(_dir, "a.SL"), lines + "\nMAIN() BEGIN END;\n");
        var ex = Assert.Throws<InvalidDataException>(() =>
        {
            var data = NexImage.ReadDataPages(Path.Combine(_dir, "a.SL"));
            NexImage.Build(new byte[] { 0 }, 0x8000, 0xE000, 0x8000, new[] { new NexImage.Module(0, 0xC000, 40, new byte[] { 1 }) }, data, 30);
        });
        Assert.Equal(message, ex.Message);
    }

    [Fact]
    public void DataPages_SeventeenLines_Errors()
    {
        File.WriteAllBytes(Path.Combine(_dir, "A.BIN"), new byte[] { 1 });
        File.WriteAllText(Path.Combine(_dir, "a.SL"), string.Concat(Enumerable.Range(30, 17).Select(p => $"//#PAGE {p} A.BIN\n")));
        var ex = Assert.Throws<InvalidDataException>(() => NexImage.ReadDataPages(Path.Combine(_dir, "a.SL")));
        Assert.Equal("//#PAGE: 16 lines at most.", ex.Message);
    }
}
