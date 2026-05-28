using Xunit;
using SLANGCompiler.SlfsPack;
using System.Text;

namespace SLANGCompiler.Tests;

public class SlfsReaderTests
{
    private static byte[] BuildSampleD88(out byte[] mainBin, out byte[] greeting, out byte[] numbers)
    {
        mainBin = new byte[512];
        for (int i = 0; i < mainBin.Length; i++) mainBin[i] = (byte)(i ^ 0x33);
        greeting = Encoding.ASCII.GetBytes("HELLO SLFS TEST");
        numbers = new byte[256];
        for (int i = 0; i < 256; i++) numbers[i] = (byte)i;

        var packer = new SlfsPackerLibrary(new SlfsPackerLibrary.Options
        {
            MainBinary = mainBin,
            MainLoadAddress = 0x2000,
            MainExecuteAddress = 0x2000,
            MainFileName = "TESTMAIN",
            VolumeName = "TEST",
            Assets = new List<SlfsPackerLibrary.AssetEntry>
            {
                new() { Name = "GREETING", Data = greeting },
                new() { Name = "NUMBERS",  Data = numbers },
            },
        });
        return packer.Build();
    }

    [Fact]
    public void Reader_ReadBootHeader_Matches()
    {
        var img = BuildSampleD88(out var mainBin, out _, out _);
        var r = new SlfsReader(img);
        var boot = r.ReadBootHeader();
        Assert.Equal(0x01, boot.BootFlag);
        Assert.Equal("TESTMAIN", boot.FileName);
        Assert.Equal("Sys", boot.Extension);
        Assert.Equal(mainBin.Length, boot.DataSize);
        Assert.Equal(0x2000, boot.LoadAddress);
        Assert.Equal(0x2000, boot.ExecuteAddress);
        // main は sector 3 = byte offset 768 に配置 (= sector 0 boot + 1 superblock + 1 dir = sector 3)
        Assert.Equal(3 * 256, boot.DiskOffset);
    }

    [Fact]
    public void Reader_ReadSuperblock_Matches()
    {
        var img = BuildSampleD88(out _, out _, out _);
        var r = new SlfsReader(img);
        var sb = r.ReadSuperblock();
        Assert.True(sb.MagicValid);
        Assert.Equal(1, sb.Version);
        Assert.Equal(2, sb.Sides);
        Assert.Equal(40, sb.Tracks);
        Assert.Equal(16, sb.SectorsPerTrack);
        Assert.Equal(2, sb.DirEntryCount);
        Assert.Equal("TEST", sb.VolumeName);
    }

    [Fact]
    public void Reader_ReadDirectory_SortedById()
    {
        var img = BuildSampleD88(out _, out _, out _);
        var r = new SlfsReader(img);
        var dir = r.ReadDirectory();
        Assert.Equal(2, dir.Count);
        Assert.Equal(0, dir[0].Id);
        Assert.Equal("GREETING", dir[0].FileName);  // G < N (= ordinal sort)
        Assert.Equal(1, dir[1].Id);
        Assert.Equal("NUMBERS", dir[1].FileName);
    }

    [Fact]
    public void Reader_ExtractAsset_ById_Matches()
    {
        var img = BuildSampleD88(out _, out var greeting, out _);
        var r = new SlfsReader(img);
        var data = r.ExtractAsset(0);
        Assert.Equal(greeting, data);
    }

    [Fact]
    public void Reader_ExtractAssetByName_Matches()
    {
        var img = BuildSampleD88(out _, out _, out var numbers);
        var r = new SlfsReader(img);
        var data = r.ExtractAssetByName("NUMBERS");
        Assert.Equal(numbers, data);
    }

    [Fact]
    public void Reader_ExtractAssetByName_NotFound_Throws()
    {
        var img = BuildSampleD88(out _, out _, out _);
        var r = new SlfsReader(img);
        Assert.Throws<InvalidOperationException>(() => r.ExtractAssetByName("MISSING"));
    }

    [Fact]
    public void Reader_ExtractAsset_OutOfRange_Throws()
    {
        var img = BuildSampleD88(out _, out _, out _);
        var r = new SlfsReader(img);
        Assert.Throws<ArgumentOutOfRangeException>(() => r.ExtractAsset(99));
    }

    [Fact]
    public void Reader_ExtractMain_Matches()
    {
        var img = BuildSampleD88(out var mainBin, out _, out _);
        var r = new SlfsReader(img);
        var data = r.ExtractMain();
        Assert.Equal(mainBin, data);
    }

    [Fact]
    public void Reader_ExtractSave_FullDump_Size()
    {
        var img = BuildSampleD88(out _, out _, out _);
        var r = new SlfsReader(img);
        var data = r.ExtractSave();
        // default save_sector_count = 64 → 64 * 256 = 16384 byte
        Assert.Equal(64 * 256, data.Length);
    }

    [Fact]
    public void Reader_ExtractSave_Partial_Size()
    {
        var img = BuildSampleD88(out _, out _, out _);
        var r = new SlfsReader(img);
        var data = r.ExtractSave(offsetSectors: 0, countSectors: 2);
        Assert.Equal(2 * 256, data.Length);
    }

    [Fact]
    public void Reader_ExtractSave_OutOfRange_Throws()
    {
        var img = BuildSampleD88(out _, out _, out _);
        var r = new SlfsReader(img);
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            r.ExtractSave(offsetSectors: 0, countSectors: 999));
    }

    [Fact]
    public void Reader_NonSlfsImage_Throws()
    {
        // 空 D88 (= SLFS magic なし)
        var emptyD88 = new D88Writer(D88Format.Geometry.Standard2D).Build();
        var r = new SlfsReader(emptyD88);
        var sb = r.ReadSuperblock();
        Assert.False(sb.MagicValid);
        Assert.Throws<InvalidOperationException>(() => r.ReadDirectory());
        Assert.Throws<InvalidOperationException>(() => r.ExtractMain());
    }
}
