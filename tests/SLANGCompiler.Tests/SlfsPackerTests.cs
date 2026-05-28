using Xunit;
using SLANGCompiler.SlfsPack;
using System.Text;

namespace SLANGCompiler.Tests;

public class SlfsPackerTests
{
    private static byte[] DummyMain(int size = 256)
    {
        var buf = new byte[size];
        for (int i = 0; i < size; i++) buf[i] = (byte)(i & 0xFF);
        return buf;
    }

    // ====================== SlfsBootHeader ======================

    [Fact]
    public void BootHeader_Size_Is32()
    {
        Assert.Equal(32, SlfsBootHeader.Size);
    }

    [Fact]
    public void BootHeader_ToBytes_FieldLayout()
    {
        var h = new SlfsBootHeader
        {
            FileName = "HELLO",
            DataSize = 0x03F1,
            LoadAddress = 0x1000,
            ExecuteAddress = 0x1000,
            DiskOffset = 0x000100,
        };
        var b = h.ToBytes();
        Assert.Equal(0x01, b[0]);                                 // BootFlag
        Assert.Equal("HELLO        ", Encoding.ASCII.GetString(b, 1, 13));
        Assert.Equal("Sys", Encoding.ASCII.GetString(b, 0x0E, 3));
        Assert.Equal(0x20, b[0x11]);                              // Password default
        Assert.Equal(0xF1, b[0x12]); Assert.Equal(0x03, b[0x13]); // DataSize LE
        Assert.Equal(0x00, b[0x14]); Assert.Equal(0x10, b[0x15]); // LoadAddress LE
        Assert.Equal(0x00, b[0x16]); Assert.Equal(0x10, b[0x17]); // ExecuteAddress LE
        // +1D-1F: DiskOffset 3 byte LE = 0x000100
        Assert.Equal(0x00, b[0x1D]);
        Assert.Equal(0x01, b[0x1E]);
        Assert.Equal(0x00, b[0x1F]);
    }

    [Fact]
    public void BootHeader_DiskOffset_OutOf24Bit_Throws()
    {
        var h = new SlfsBootHeader { DiskOffset = 0x1000000 };
        Assert.Throws<InvalidOperationException>(() => h.ToBytes());
    }

    // ====================== SlfsSuperblock ======================

    [Fact]
    public void Superblock_Magic_IsSlfs()
    {
        var sb = new SlfsSuperblock { Sides = 2, Tracks = 40, SectorsPerTrack = 16 };
        var b = sb.ToBytes();
        Assert.Equal("SLFS", Encoding.ASCII.GetString(b, 0, 4));
    }

    [Fact]
    public void Superblock_FieldLayout()
    {
        var sb = new SlfsSuperblock
        {
            Version = 1, Sides = 2, Tracks = 40, SectorsPerTrack = 16,
            DirStartSector = 2, DirEntryCount = 5,
            DataAreaStartSector = 9, SaveAreaStartSector = 100, SaveSectorCount = 64,
            VolumeName = "GAMEDISK",
        };
        var b = sb.ToBytes();
        Assert.Equal(256, b.Length);
        Assert.Equal(1, b[0x04]);
        Assert.Equal(2, b[0x05]);
        Assert.Equal(40, b[0x06]);
        Assert.Equal(16, b[0x07]);
        Assert.Equal(0x02, b[0x08]); Assert.Equal(0x00, b[0x09]); // DirStart
        Assert.Equal(0x05, b[0x0A]); Assert.Equal(0x00, b[0x0B]); // DirEntryCount
        Assert.Equal(0x09, b[0x0C]); Assert.Equal(0x00, b[0x0D]); // DataAreaStart
        Assert.Equal(0x64, b[0x0E]); Assert.Equal(0x00, b[0x0F]); // SaveAreaStart
        Assert.Equal(0x40, b[0x10]); Assert.Equal(0x00, b[0x11]); // SaveSectorCount
        Assert.Equal("GAMEDISK        ", Encoding.ASCII.GetString(b, 0x12, 16));
    }

    // ====================== SlfsDirEntry ======================

    [Fact]
    public void DirEntry_Size_Is16()
    {
        Assert.Equal(16, SlfsDirEntry.Size);
    }

    [Fact]
    public void DirEntry_FieldLayout()
    {
        var e = new SlfsDirEntry
        {
            FileName = "ASSET",
            Type = 1,
            StartSector = 100,
            ByteSize = 256,
        };
        var b = e.ToBytes();
        Assert.Equal("ASSET      ", Encoding.ASCII.GetString(b, 0, 11));
        Assert.Equal(1, b[0x0B]);
        Assert.Equal(100, b[0x0C]); Assert.Equal(0, b[0x0D]);
        Assert.Equal(0x00, b[0x0E]); Assert.Equal(0x01, b[0x0F]);
    }

    [Fact]
    public void DirEntry_NormalizeFileName_PadsTo11WithSpaces()
    {
        var n = SlfsDirEntry.NormalizeFileName("ABC");
        Assert.Equal(11, n.Length);
        Assert.Equal((byte)'A', n[0]);
        Assert.Equal((byte)'B', n[1]);
        Assert.Equal((byte)'C', n[2]);
        Assert.Equal((byte)' ', n[3]);
    }

    [Fact]
    public void DirEntry_NormalizeFileName_TruncatesAt11()
    {
        var n = SlfsDirEntry.NormalizeFileName("ABCDEFGHIJKL"); // 12 char
        Assert.Equal(11, n.Length);
        Assert.Equal((byte)'K', n[10]);
    }

    [Fact]
    public void DirEntry_CompareNormalized_OrdinalOrder()
    {
        var a = SlfsDirEntry.NormalizeFileName("APPLE");
        var b = SlfsDirEntry.NormalizeFileName("BANANA");
        Assert.True(SlfsDirEntry.CompareNormalizedFileName(a, b) < 0);
        Assert.True(SlfsDirEntry.CompareNormalizedFileName(b, a) > 0);
        Assert.Equal(0, SlfsDirEntry.CompareNormalizedFileName(a, a));
    }

    // ====================== SlfsPackerLibrary ======================

    [Fact]
    public void Packer_RejectsEmptyMain()
    {
        Assert.Throws<ArgumentException>(() =>
            new SlfsPackerLibrary(new SlfsPackerLibrary.Options { MainBinary = Array.Empty<byte>() }));
    }

    [Fact]
    public void Packer_RejectsTooLargeMain()
    {
        Assert.Throws<ArgumentException>(() =>
            new SlfsPackerLibrary(new SlfsPackerLibrary.Options { MainBinary = new byte[0x10000] }));
    }

    [Fact]
    public void Packer_RejectsTooManyAssets()
    {
        var assets = new List<SlfsPackerLibrary.AssetEntry>();
        for (int i = 0; i < 257; i++)
            assets.Add(new() { Name = $"F{i:D3}", Data = new byte[1] });
        var ex = Assert.Throws<ArgumentException>(() =>
            new SlfsPackerLibrary(new SlfsPackerLibrary.Options { MainBinary = DummyMain(), Assets = assets }));
        Assert.Contains("too many assets", ex.Message);
    }

    [Fact]
    public void Packer_RejectsEmptyAsset()
    {
        var assets = new List<SlfsPackerLibrary.AssetEntry>
        {
            new() { Name = "EMPTY", Data = Array.Empty<byte>() }
        };
        Assert.Throws<ArgumentException>(() =>
            new SlfsPackerLibrary(new SlfsPackerLibrary.Options { MainBinary = DummyMain(), Assets = assets }));
    }

    [Fact]
    public void Packer_RejectsTooLargeAsset()
    {
        var assets = new List<SlfsPackerLibrary.AssetEntry>
        {
            new() { Name = "BIG", Data = new byte[0x10000] }
        };
        Assert.Throws<ArgumentException>(() =>
            new SlfsPackerLibrary(new SlfsPackerLibrary.Options { MainBinary = DummyMain(), Assets = assets }));
    }

    [Fact]
    public void Packer_SortedAssets_ByOrdinalFileName()
    {
        var assets = new List<SlfsPackerLibrary.AssetEntry>
        {
            new() { Name = "BANANA", Data = new byte[1] },
            new() { Name = "APPLE",  Data = new byte[1] },
            new() { Name = "CHERRY", Data = new byte[1] },
        };
        var packer = new SlfsPackerLibrary(new SlfsPackerLibrary.Options { MainBinary = DummyMain(), Assets = assets });
        var sorted = packer.SortedAssets();
        Assert.Equal("APPLE", sorted[0].Name);
        Assert.Equal("BANANA", sorted[1].Name);
        Assert.Equal("CHERRY", sorted[2].Name);
    }

    [Fact]
    public void Packer_DetectsFilenameCollision()
    {
        var assets = new List<SlfsPackerLibrary.AssetEntry>
        {
            new() { Name = "GREETING.TXT", Data = new byte[1] }, // → "GREETING.TX" (11 char)
            new() { Name = "GREETING.TX",  Data = new byte[1] }, // → "GREETING.TX"
        };
        var packer = new SlfsPackerLibrary(new SlfsPackerLibrary.Options { MainBinary = DummyMain(), Assets = assets });
        Assert.Throws<InvalidOperationException>(() => packer.SortedAssets());
    }

    [Fact]
    public void Packer_Build_BootSectorAndSuperblock()
    {
        var opts = new SlfsPackerLibrary.Options
        {
            MainBinary = DummyMain(1024),
            MainLoadAddress = 0x1000,
            MainExecuteAddress = 0x1000,
            MainFileName = "MAIN",
            VolumeName = "TEST",
            Assets = new List<SlfsPackerLibrary.AssetEntry>
            {
                new() { Name = "A", Data = new byte[10] },
                new() { Name = "B", Data = new byte[10] },
            },
        };
        var packer = new SlfsPackerLibrary(opts);
        var img = packer.Build();

        var r = new D88Reader(img);
        // sector 0 = boot header
        var boot = r.ReadSector(0, 0, 1);
        Assert.Equal(0x01, boot[0]); // BootFlag
        Assert.Equal("MAIN", Encoding.ASCII.GetString(boot, 1, 4));

        // sector 1 = superblock
        var sb = r.ReadSector(0, 0, 2);
        Assert.Equal("SLFS", Encoding.ASCII.GetString(sb, 0, 4));
        Assert.Equal(2, sb[0x0A]); // DirEntryCount
        Assert.Equal(0, sb[0x0B]);

        // sector 2 = directory (2 entry sorted)
        var dir = r.ReadSector(0, 0, 3);
        Assert.Equal("A          ", Encoding.ASCII.GetString(dir, 0, 11));
        Assert.Equal("B          ", Encoding.ASCII.GetString(dir, 16, 11));
    }
}
