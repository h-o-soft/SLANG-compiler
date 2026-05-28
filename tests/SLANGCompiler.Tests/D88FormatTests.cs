using Xunit;
using SLANGCompiler.SlfsPack;

namespace SLANGCompiler.Tests;

public class D88FormatTests
{
    [Fact]
    public void Standard2D_Geometry_Has320KB()
    {
        var g = D88Format.Geometry.Standard2D;
        Assert.Equal(2, g.Sides);
        Assert.Equal(40, g.Tracks);
        Assert.Equal(16, g.SectorsPerTrack);
        Assert.Equal(256, g.SectorSize);
        Assert.Equal(2 * 40 * 16, g.LogicalSectorCount);  // 1280 sector = 320 KB
    }

    [Fact]
    public void LogicalToChs_FirstSector()
    {
        var g = D88Format.Geometry.Standard2D;
        var (c, h, s) = g.LogicalToChs(0);
        Assert.Equal((0, 0, 1), (c, h, s));
    }

    [Fact]
    public void LogicalToChs_SecondSector()
    {
        var g = D88Format.Geometry.Standard2D;
        var (c, h, s) = g.LogicalToChs(1);
        Assert.Equal((0, 0, 2), (c, h, s));
    }

    [Fact]
    public void LogicalToChs_SecondSide()
    {
        // sector 16 = (cyl 0, head 1, sector 1)
        var g = D88Format.Geometry.Standard2D;
        var (c, h, s) = g.LogicalToChs(16);
        Assert.Equal((0, 1, 1), (c, h, s));
    }

    [Fact]
    public void LogicalToChs_SecondCylinder()
    {
        // sector 32 = (cyl 1, head 0, sector 1) (= sides * sectors_per_track = 32)
        var g = D88Format.Geometry.Standard2D;
        var (c, h, s) = g.LogicalToChs(32);
        Assert.Equal((1, 0, 1), (c, h, s));
    }

    [Fact]
    public void Writer_BuildsEmpty2D_WithCorrectSize()
    {
        var w = new D88Writer(D88Format.Geometry.Standard2D);
        var img = w.Build();
        // 0x2B0 (header + track table) + 80 track × (16 sec × (16 + 256)) = 0x2B0 + 80 × 4352
        int expected = D88Format.DataAreaOffset + 80 * 16 * (D88Format.SectorHeaderSize + 256);
        Assert.Equal(expected, img.Length);
        Assert.Equal(D88Format.MediaType2D, img[0x1B]);
    }

    [Fact]
    public void WriterReader_RoundTrip_PreservesSectorData()
    {
        var w = new D88Writer(D88Format.Geometry.Standard2D);
        w.SetDiskName("TESTDISK");
        var payload = new byte[256];
        for (int i = 0; i < 256; i++) payload[i] = (byte)(i ^ 0x5A);
        w.WriteSector(0, payload);  // logical 0 = cyl 0 head 0 sector 1

        var img = w.Build();
        var r = new D88Reader(img);
        var read = r.ReadSector(0, 0, 1);
        Assert.Equal(payload, read);
        Assert.Equal("TESTDISK", r.DiskName);
        Assert.Equal(D88Format.MediaType2D, r.MediaType);
    }

    [Fact]
    public void WriterReader_RoundTrip_SecondSide()
    {
        var w = new D88Writer(D88Format.Geometry.Standard2D);
        var payload = new byte[256];
        for (int i = 0; i < 256; i++) payload[i] = (byte)(i + 1);
        w.WriteSector(16, payload);  // logical 16 = cyl 0 head 1 sector 1

        var img = w.Build();
        var r = new D88Reader(img);
        var read = r.ReadSector(0, 1, 1);
        Assert.Equal(payload, read);
    }

    [Fact]
    public void Reader_TrackOffsetTable_FirstTrack_Matches()
    {
        var w = new D88Writer(D88Format.Geometry.Standard2D);
        var img = w.Build();
        var r = new D88Reader(img);
        // track 0 (= cyl 0 head 0) は DataAreaOffset (= 0x2B0) から始まる
        Assert.Equal(D88Format.DataAreaOffset, r.GetTrackOffset(0));
    }

    [Fact]
    public void Reader_TrackOffsetTable_SecondTrack_OffsetByOneTrackSize()
    {
        var w = new D88Writer(D88Format.Geometry.Standard2D);
        var img = w.Build();
        var r = new D88Reader(img);
        int trackBlock = 16 * (D88Format.SectorHeaderSize + 256);  // 4352 byte
        Assert.Equal(D88Format.DataAreaOffset + trackBlock, r.GetTrackOffset(1));
    }
}
