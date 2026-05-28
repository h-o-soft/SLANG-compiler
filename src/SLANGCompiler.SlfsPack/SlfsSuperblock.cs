using System.Buffers.Binary;
using System.Text;

namespace SLANGCompiler.SlfsPack;

/// <summary>
/// SLFS superblock (= D88 cyl 0 head 0 sector 2、 logical sector 1) 256 byte。
///
/// layout:
///   +00 4   Magic = "SLFS"
///   +04 1   Version (= 1)
///   +05 1   Sides
///   +06 1   Tracks
///   +07 1   SectorsPerTrack
///   +08 2   DirStartSector (logical sector index, LE)
///   +0A 2   DirEntryCount (LE)
///   +0C 2   DataAreaStartSector (LE)
///   +0E 2   SaveAreaStartSector (LE)
///   +10 2   SaveSectorCount (LE)
///   +12 16  VolumeName (ASCII space-padded)
///   +22 0xDE Reserved (zero-fill)
/// </summary>
public sealed class SlfsSuperblock
{
    public const int Size = 256;
    public static readonly byte[] Magic = { (byte)'S', (byte)'L', (byte)'F', (byte)'S' };

    public byte Version { get; set; } = 1;
    public byte Sides { get; set; }
    public byte Tracks { get; set; }
    public byte SectorsPerTrack { get; set; }
    public ushort DirStartSector { get; set; }
    public ushort DirEntryCount { get; set; }
    public ushort DataAreaStartSector { get; set; }
    public ushort SaveAreaStartSector { get; set; }
    public ushort SaveSectorCount { get; set; }
    public string VolumeName { get; set; } = "";

    public byte[] ToBytes()
    {
        var buf = new byte[Size];
        Array.Copy(Magic, 0, buf, 0, 4);
        buf[0x04] = Version;
        buf[0x05] = Sides;
        buf[0x06] = Tracks;
        buf[0x07] = SectorsPerTrack;
        BinaryPrimitives.WriteUInt16LittleEndian(buf.AsSpan(0x08, 2), DirStartSector);
        BinaryPrimitives.WriteUInt16LittleEndian(buf.AsSpan(0x0A, 2), DirEntryCount);
        BinaryPrimitives.WriteUInt16LittleEndian(buf.AsSpan(0x0C, 2), DataAreaStartSector);
        BinaryPrimitives.WriteUInt16LittleEndian(buf.AsSpan(0x0E, 2), SaveAreaStartSector);
        BinaryPrimitives.WriteUInt16LittleEndian(buf.AsSpan(0x10, 2), SaveSectorCount);
        WriteAsciiPadded(buf.AsSpan(0x12, 16), VolumeName, (byte)' ');
        return buf;
    }

    private static void WriteAsciiPadded(Span<byte> dst, string s, byte pad)
    {
        var bytes = Encoding.ASCII.GetBytes(s ?? "");
        int copy = Math.Min(bytes.Length, dst.Length);
        bytes.AsSpan(0, copy).CopyTo(dst);
        for (int i = copy; i < dst.Length; i++) dst[i] = pad;
    }
}

/// <summary>
/// SLFS directory entry (= 16 byte)。
///
/// layout:
///   +00 11  FileName (ASCII space-padded、 8.3 風 or 自由)
///   +0B 1   Type (= 0 raw, 1 圧縮等 reserved)
///   +0C 2   StartSector (logical sector index, LE)
///   +0E 2   ByteSize (= 実 byte サイズ, LE, 範囲 1..65535)
/// </summary>
public sealed class SlfsDirEntry
{
    public const int Size = 16;

    public string FileName { get; set; } = "";
    public byte Type { get; set; } = 0;
    public ushort StartSector { get; set; }
    public ushort ByteSize { get; set; }

    public byte[] ToBytes()
    {
        var buf = new byte[Size];
        WriteAsciiPadded(buf.AsSpan(0x00, 11), FileName, (byte)' ');
        buf[0x0B] = Type;
        BinaryPrimitives.WriteUInt16LittleEndian(buf.AsSpan(0x0C, 2), StartSector);
        BinaryPrimitives.WriteUInt16LittleEndian(buf.AsSpan(0x0E, 2), ByteSize);
        return buf;
    }

    /// <summary>filename を 11 byte ASCII space-padded で normalize (= sort + collision check 用)</summary>
    public static byte[] NormalizeFileName(string name)
    {
        var buf = new byte[11];
        var bytes = Encoding.ASCII.GetBytes(name ?? "");
        int copy = Math.Min(bytes.Length, 11);
        bytes.AsSpan(0, copy).CopyTo(buf);
        for (int i = copy; i < 11; i++) buf[i] = (byte)' ';
        return buf;
    }

    /// <summary>normalized filename ordinal 比較 (= byte 単位)</summary>
    public static int CompareNormalizedFileName(byte[] a, byte[] b)
    {
        for (int i = 0; i < 11; i++)
        {
            int diff = a[i] - b[i];
            if (diff != 0) return diff;
        }
        return 0;
    }

    private static void WriteAsciiPadded(Span<byte> dst, string s, byte pad)
    {
        var bytes = Encoding.ASCII.GetBytes(s ?? "");
        int copy = Math.Min(bytes.Length, dst.Length);
        bytes.AsSpan(0, copy).CopyTo(dst);
        for (int i = copy; i < dst.Length; i++) dst[i] = pad;
    }
}
