namespace SLANGCompiler.Build.TapeFormats;

/// <summary>
/// Frame a SHARP X1 cassette byte stream from a tape-bit sequence (and vice versa).
///
/// Per X1 IPL ROM (CMT_LOADIFB / MT_RDBYTE), the on-tape structure is:
///
///   [LEADER]    long run of "1" bits (information block: ~1000+; data body: shorter)
///   [SYNC 0s]   continuous "0" bits (info: 40 / data: 20)
///   [SYNC 1s]   continuous "1" bits (info: 41 / data: 21)
///   [BYTE]*     each byte = 1 start bit "0" + 8 data bits MSB first
///                (no stop bits — the next byte's start bit follows immediately)
///   [CHECKSUM]  2 bytes (16-bit additive checksum) appended to information block
///                and to data block payloads.
///
/// The classic "information block" carries the file header (boot flag, filename,
/// extension, sizes, addresses, date, ...) in a 32-byte payload + 2-byte checksum.
/// The "data block" carries the actual program bytes + 2-byte checksum.
/// </summary>
public static class X1TapeFraming
{
    public const int InfoBlockPayloadSize = 32;
    public const int ChecksumSize = 2;
    public const int InfoBlockTotalSize = InfoBlockPayloadSize + ChecksumSize;

    public const int InfoBlockSyncZeros = 40;
    public const int InfoBlockSyncOnes = 41;
    public const int DataBlockSyncZeros = 20;
    public const int DataBlockSyncOnes = 21;

    public const int DefaultInfoLeaderOnes = 8000;  // ~1 sec at 8 kHz
    public const int DefaultDataLeaderOnes = 4000;  // ~0.5 sec

    public const int InfoBlockMinSyncZeros = 20;  // IPL is tolerant
    public const int InfoBlockMinSyncOnes = 20;

    public sealed class SyncMatch
    {
        public int LeaderEndIndex;
        public int ZeroRunLength;
        public int OneRunLength;
        public int DataStartIndex;
    }

    /// <summary>
    /// Locate the next sync pattern in the bit stream starting at <paramref name="startAt"/>:
    /// skip leading 1s, then count the next 0-run and 1-run.
    /// Returns null if no valid sync (zeros and ones both ≥ minimums) is found.
    /// </summary>
    public static SyncMatch? FindNextSync(ReadOnlySpan<byte> bits, int startAt, int minZeros, int minOnes)
    {
        int i = startAt;
        while (true)
        {
            // skip 1-bits
            while (i < bits.Length && bits[i] == 1) i++;
            if (i >= bits.Length) return null;
            int zeroStart = i;
            while (i < bits.Length && bits[i] == 0) i++;
            int zeroCount = i - zeroStart;
            int oneStart = i;
            while (i < bits.Length && bits[i] == 1) i++;
            int oneCount = i - oneStart;
            if (zeroCount >= minZeros && oneCount >= minOnes)
            {
                return new SyncMatch
                {
                    LeaderEndIndex = zeroStart,
                    ZeroRunLength = zeroCount,
                    OneRunLength = oneCount,
                    DataStartIndex = i,
                };
            }
            // Not a valid sync — keep scanning past these 0s+1s.
            if (oneCount == 0) return null;  // bit stream ended in a zero run
        }
    }

    /// <summary>
    /// Read <paramref name="count"/> framed bytes (1 start bit + 8 data bits MSB-first)
    /// from <paramref name="bits"/> starting at <paramref name="start"/>.
    /// Throws if a start bit is not "0".
    /// </summary>
    public static byte[] ReadBytes(ReadOnlySpan<byte> bits, int start, int count, out int endIndex)
    {
        var bytes = new byte[count];
        int pos = start;
        for (int n = 0; n < count; n++)
        {
            if (pos + 9 > bits.Length)
                throw new InvalidDataException($"unexpected end of bit stream while reading byte {n}/{count} at bit {pos}");
            if (bits[pos] != 0)
                throw new InvalidDataException($"missing start bit at position {pos} (byte {n}/{count})");
            int v = 0;
            for (int k = 0; k < 8; k++)
                v = (v << 1) | bits[pos + 1 + k];
            bytes[n] = (byte)v;
            pos += 9;
        }
        endIndex = pos;
        return bytes;
    }

    /// <summary>
    /// Write <paramref name="bytes"/> as framed tape bits (1 start + 8 data MSB first per byte).
    /// </summary>
    public static byte[] WriteBytes(ReadOnlySpan<byte> bytes)
    {
        var bits = new byte[bytes.Length * 9];
        int p = 0;
        foreach (var b in bytes)
        {
            bits[p++] = 0;  // start bit
            for (int k = 7; k >= 0; k--)
                bits[p++] = (byte)((b >> k) & 1);
        }
        return bits;
    }

    public static byte[] BuildSync(int leaderOnes, int syncZeros, int syncOnes)
    {
        var bits = new byte[leaderOnes + syncZeros + syncOnes];
        int p = 0;
        for (int i = 0; i < leaderOnes; i++) bits[p++] = 1;
        for (int i = 0; i < syncZeros; i++) bits[p++] = 0;
        for (int i = 0; i < syncOnes; i++) bits[p++] = 1;
        return bits;
    }

    /// <summary>
    /// 16-bit additive checksum used by the X1 IPL: sum of payload bytes,
    /// stored big-endian (high byte then low byte) at the end of the block.
    /// </summary>
    public static ushort ComputeChecksum(ReadOnlySpan<byte> payload)
    {
        ushort s = 0;
        foreach (var b in payload) s += b;
        return s;
    }
}
