using System.Buffers.Binary;
using System.Text;

namespace SLANGCompiler.Build.TapeFormats;

/// <summary>
/// Decoded representation of the X1 "Information Block" (32-byte file descriptor
/// preceding the data block on tape). Field layout follows the IPL's FILEBUF.
///
/// Layout (32 bytes):
///   +00h 1   Boot flag (0x01 = machine code / executable)
///   +01h 13  File name (ASCII, space-padded)
///   +0Eh 3   Extension (ASCII, space-padded)
///   +11h 1   Password byte (typically 0x20 = "no password")
///   +12h 2   Data size in bytes (big-endian uint16)
///   +14h 2   Load address (big-endian uint16)
///   +16h 2   Execute address (big-endian uint16)
///   +18h 6   Date (BCD: YY MM DD HH MM weekday)
///   +1Eh 2   Reserved (zero-filled)
/// </summary>
public sealed class X1InfoBlock
{
    public const int FieldsSize = 32;

    public byte BootFlag { get; set; } = 0x01;
    public string FileName { get; set; } = "";       // up to 13 chars
    public string Extension { get; set; } = "";      // up to 3 chars
    public byte Password { get; set; } = 0x20;
    public ushort DataSize { get; set; }
    public ushort LoadAddress { get; set; }
    public ushort ExecuteAddress { get; set; }
    public byte[] Date { get; set; } = new byte[6];
    public byte[] Reserved { get; set; } = new byte[2];

    public static X1InfoBlock FromBytes(ReadOnlySpan<byte> src)
    {
        if (src.Length < FieldsSize)
            throw new ArgumentException($"info block too small ({src.Length} < {FieldsSize})");
        var ib = new X1InfoBlock
        {
            BootFlag = src[0],
            FileName = ReadAscii(src.Slice(0x01, 13)),
            Extension = ReadAscii(src.Slice(0x0E, 3)),
            Password = src[0x11],
            DataSize = BinaryPrimitives.ReadUInt16BigEndian(src.Slice(0x12)),
            LoadAddress = BinaryPrimitives.ReadUInt16BigEndian(src.Slice(0x14)),
            ExecuteAddress = BinaryPrimitives.ReadUInt16BigEndian(src.Slice(0x16)),
            Date = src.Slice(0x18, 6).ToArray(),
            Reserved = src.Slice(0x1E, 2).ToArray(),
        };
        return ib;
    }

    public byte[] ToBytes()
    {
        var buf = new byte[FieldsSize];
        buf[0] = BootFlag;
        WriteAsciiPadded(buf.AsSpan(0x01, 13), FileName, (byte)' ');
        WriteAsciiPadded(buf.AsSpan(0x0E, 3), Extension, (byte)' ');
        buf[0x11] = Password;
        BinaryPrimitives.WriteUInt16BigEndian(buf.AsSpan(0x12), DataSize);
        BinaryPrimitives.WriteUInt16BigEndian(buf.AsSpan(0x14), LoadAddress);
        BinaryPrimitives.WriteUInt16BigEndian(buf.AsSpan(0x16), ExecuteAddress);
        (Date.Length >= 6 ? Date.AsSpan(0, 6) : (Span<byte>)stackalloc byte[6]).CopyTo(buf.AsSpan(0x18, 6));
        (Reserved.Length >= 2 ? Reserved.AsSpan(0, 2) : (Span<byte>)stackalloc byte[2]).CopyTo(buf.AsSpan(0x1E, 2));
        return buf;
    }

    private static string ReadAscii(ReadOnlySpan<byte> span)
    {
        return Encoding.ASCII.GetString(span.ToArray()).TrimEnd(' ', '\0');
    }

    private static void WriteAsciiPadded(Span<byte> dst, string s, byte pad)
    {
        var bytes = Encoding.ASCII.GetBytes(s ?? "");
        int copy = Math.Min(bytes.Length, dst.Length);
        bytes.AsSpan(0, copy).CopyTo(dst);
        for (int i = copy; i < dst.Length; i++) dst[i] = pad;
    }
}
