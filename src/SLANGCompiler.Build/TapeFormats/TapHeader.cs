using System.Buffers.Binary;
using System.Text;

namespace SLANGCompiler.Build.TapeFormats;

/// <summary>
/// SHARP X1 ".tap" file header (40 bytes, fixed layout).
///
/// Layout:
///   +00h 4    "TAPE" magic (0x45504154 little-endian = 'T','A','P','E')
///   +04h 17   Tape name (ASCII, null-padded)
///   +15h 5    Reserved (zero-filled)
///   +1Ah 1    Write protection notch (0x00 = writable, 0x10 = write-protected)
///   +1Bh 1    Format identifier (0x01 = constant-rate sampling — only supported value)
///   +1Ch 4    Sampling frequency in Hz (little-endian uint32)
///   +20h 4    Tape data size in BITS (little-endian uint32)
///   +24h 4    Current tape position in BITS (little-endian uint32)
/// </summary>
public sealed class TapHeader
{
    public const int SizeBytes = 0x28; // 40
    public const uint Magic = 0x45504154; // 'TAPE'
    public const byte FormatSampling = 0x01;
    public const byte ProtectionWritable = 0x00;
    public const byte ProtectionWriteProtected = 0x10;
    public const int NameMaxLength = 17;

    public string Name { get; set; } = "auto converted";
    public byte WriteProtect { get; set; } = ProtectionWritable;
    public byte Format { get; set; } = FormatSampling;
    public uint SampleRate { get; set; } = 8000;
    public uint DataSizeBits { get; set; }
    public uint PositionBits { get; set; }

    public static TapHeader Read(ReadOnlySpan<byte> src)
    {
        if (src.Length < SizeBytes)
            throw new ArgumentException($"header span too small ({src.Length} < {SizeBytes})");
        var magic = BinaryPrimitives.ReadUInt32LittleEndian(src);
        if (magic != Magic)
            throw new InvalidDataException($"not a SHARP X1 .tap file (magic = 0x{magic:X8})");

        var nameBytes = src.Slice(0x04, NameMaxLength).ToArray();
        int nul = Array.IndexOf(nameBytes, (byte)0);
        var name = Encoding.ASCII.GetString(nameBytes, 0, nul < 0 ? nameBytes.Length : nul);

        return new TapHeader
        {
            Name = name,
            WriteProtect = src[0x1A],
            Format = src[0x1B],
            SampleRate = BinaryPrimitives.ReadUInt32LittleEndian(src.Slice(0x1C)),
            DataSizeBits = BinaryPrimitives.ReadUInt32LittleEndian(src.Slice(0x20)),
            PositionBits = BinaryPrimitives.ReadUInt32LittleEndian(src.Slice(0x24)),
        };
    }

    public void Write(Span<byte> dst)
    {
        if (dst.Length < SizeBytes)
            throw new ArgumentException($"destination span too small ({dst.Length} < {SizeBytes})");
        dst.Slice(0, SizeBytes).Clear();
        BinaryPrimitives.WriteUInt32LittleEndian(dst, Magic);

        var nameBytes = Encoding.ASCII.GetBytes(Name);
        int copy = Math.Min(nameBytes.Length, NameMaxLength - 1); // leave room for trailing NUL
        nameBytes.AsSpan(0, copy).CopyTo(dst.Slice(0x04));
        // remaining name bytes already zeroed by Clear

        dst[0x1A] = WriteProtect;
        dst[0x1B] = Format;
        BinaryPrimitives.WriteUInt32LittleEndian(dst.Slice(0x1C), SampleRate);
        BinaryPrimitives.WriteUInt32LittleEndian(dst.Slice(0x20), DataSizeBits);
        BinaryPrimitives.WriteUInt32LittleEndian(dst.Slice(0x24), PositionBits);
    }

    public byte[] ToBytes()
    {
        var buf = new byte[SizeBytes];
        Write(buf);
        return buf;
    }
}
