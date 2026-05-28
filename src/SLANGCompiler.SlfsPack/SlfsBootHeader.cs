using System.Buffers.Binary;
using System.Text;

namespace SLANGCompiler.SlfsPack;

/// <summary>
/// SLFS boot sector (= D88 cyl 0 head 0 sector 1) の HuBASIC IPL header (32 byte)。
///
/// X1InfoBlock (= cassette tape info block) と互換 + disk 用拡張 (+1D-1F disk offset):
///   +00 1   BootFlag ($01 = 起動可能)
///   +01 13  FileName (ASCII space-padded)
///   +0E 3   Extension (ASCII space-padded、 "Sys" 推奨)
///   +11 1   Password ($20 = no password)
///   +12 2   DataSize byte (LE、 = main program 全 size)
///   +14 2   LoadAddress (LE)
///   +16 2   ExecuteAddress (LE)
///   +18 5   Date (5 byte、 tape は 6 byte だが disk では +1D を offset low byte と共用)
///   +1D 3   DiskOffset byte (LE、 main program の disk image 内 byte 開始位置)
///
/// X1 IPL ROM (= X1_compatible_rom.z80 L329-355) はこの header を sector 0 から
/// 読込み、 size / load_addr / exec_addr / disk_offset を取得して main program を
/// 自動 load + exec_addr に jp する (= boot loader code は不要)。
/// </summary>
public sealed class SlfsBootHeader
{
    public const int Size = 32;

    public byte BootFlag { get; set; } = 0x01;
    public string FileName { get; set; } = "";    // up to 13 chars
    public string Extension { get; set; } = "Sys"; // up to 3 chars
    public byte Password { get; set; } = 0x20;
    public ushort DataSize { get; set; }
    public ushort LoadAddress { get; set; }
    public ushort ExecuteAddress { get; set; }
    public byte[] Date { get; set; } = new byte[5]; // tape より 1 byte 短い (= +1D は disk offset と共用)
    /// <summary>main program body の disk image 内 byte 開始位置 (= sector index × 256 で配置)</summary>
    public int DiskOffset { get; set; }

    public byte[] ToBytes()
    {
        var buf = new byte[Size];
        buf[0] = BootFlag;
        WriteAsciiPadded(buf.AsSpan(0x01, 13), FileName, (byte)' ');
        WriteAsciiPadded(buf.AsSpan(0x0E, 3), Extension, (byte)' ');
        buf[0x11] = Password;
        BinaryPrimitives.WriteUInt16LittleEndian(buf.AsSpan(0x12), DataSize);
        BinaryPrimitives.WriteUInt16LittleEndian(buf.AsSpan(0x14), LoadAddress);
        BinaryPrimitives.WriteUInt16LittleEndian(buf.AsSpan(0x16), ExecuteAddress);
        var dateLen = Math.Min(Date.Length, 5);
        Date.AsSpan(0, dateLen).CopyTo(buf.AsSpan(0x18, 5));
        // +1D..+1F: 24-bit LE disk offset
        if (DiskOffset < 0 || DiskOffset > 0xFFFFFF)
            throw new InvalidOperationException($"DiskOffset out of 24-bit range: {DiskOffset}");
        buf[0x1D] = (byte)(DiskOffset & 0xFF);
        buf[0x1E] = (byte)((DiskOffset >> 8) & 0xFF);
        buf[0x1F] = (byte)((DiskOffset >> 16) & 0xFF);
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
