namespace SLANGCompiler.Build.TapeFormats;

/// <summary>
/// Frame a SHARP X1 cassette byte stream from a tape-bit sequence (and vice versa).
///
/// Per X1 IPL ROM (CMT_LOADIFB / MT_RDBYTE)、 X1_compatible_rom 実装 (= 互換 IPL)
/// と整合する on-tape structure (= **純正 / 互換 X1 IPL 両 boot 成功確認済**):
///
///   [LEADER]    long run of "1" bits (info: ~1000+; data body: ~3000+)
///   [SYNC 0s]   continuous "0" bits (info: 40 / data: 20)
///   [SYNC 1s]   continuous "1" bits (info: **41** / data: **21**)
///                ← 互換 IPL MT_SKIP1 が 41 / 21 個 skip する実装と整合、
///                  40 / 20 だと byte 0 start "1" が MT_SKIP1 の +1 padding に
///                  消費されて IPL が boot 失敗 (= 「立ち上がり検出 1 個」 の正体)。
///   [BYTE]*     each byte = **1 start bit "1"** + 8 data bits MSB first = 9 bit/byte
///                (互換 IPL MT_RDBYTE は MT_RDBIT を 9 回 read で 1 byte)
///   [CHECKSUM]  2 bytes also framed as **9 bit/byte** (= 通常 byte と同じ start "1" +
///                8 data)。 互換 IPL CMT_LOADIFB は info 32 + checksum 2 = 34 byte を
///                MT_RDBYTE × 34 回 で 一括 read してる、 純正 X1 IPL も同じ framing。
///                (retropc tapeformat.html の「checksum は start '1' なし」 表記は誤読、
///                 実 X1 tape decode で 9 bit/byte 確定)
///
/// Checksum 値計算: payload 全 byte の bit "1" 個数 (= popcount) を 16-bit modular sum、
/// **big-endian** (= MSB first、 high byte 先) で block 末尾に書く (= 実 X1 tape 逆引きで
/// BE 確定、 LE と読むと checksum 一致しない)。 ただし info block の DataSize / LoadAddress
/// / ExecuteAddress 自体は逆に **little-endian** (= X1 IPL `ld hl,(...)` 互換、 X1InfoBlock 参照)。
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

    // X1 IPL tape spec (= X1_compatible_rom 互換 IPL 実装と整合):
    //   info: "1" × 1000+ leader + "0" × 40 + "1" × 41 + info bytes (9 bit/byte)
    //   data: "1" × 3000+ leader + "0" × 20 + "1" × 21 + data bytes (9 bit/byte)
    //
    // 互換 IPL は MT_SKIP0 (= 40/20 個 read & skip) + MT_SKIP1 (= 41/21 個 read &
    // skip) してから MT_RDBYTE で byte read 開始。 sync ones を 40/20 にすると
    // byte 0 の start "1" が MT_SKIP1 の 41/21 個目として消費されて、 次の
    // MT_RDBYTE が byte data bit7 (= boot flag 0x01 の MSB=0) を start bit と
    // 誤認して fail (= 「立ち上がり検出 1 個」 の正体はこの +1 padding)。
    public const int InfoBlockSyncZeros = 40;
    public const int InfoBlockSyncOnes = 41;
    public const int DataBlockSyncZeros = 20;
    public const int DataBlockSyncOnes = 21;

    public const int DefaultInfoLeaderOnes = 8000;  // ~1 sec at 8 kHz (spec: 1000+ 余裕)
    public const int DefaultDataLeaderOnes = 4000;  // ~0.5 sec (spec: 3000+ 余裕)

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
    /// skip leading 1s, then look for a 0-run (≥ <paramref name="minZeros"/>) followed by a
    /// 1-run (≥ <paramref name="syncOnes"/>).
    /// Returns null if no valid sync is found.
    ///
    /// **重要**: DataStartIndex は「sync zeros end + syncOnes (= exact count)」 を返す
    /// (= 1-run 全 length ではない)。 byte 0 start "1" が sync ones run と区別不能になる
    /// (= 連続 "1" として merge される) 問題を回避するため、 spec の syncOnes 数 ぴったりで
    /// 切る。 1-run が syncOnes より長くても許容 (= leader / sync tolerance)、 ただし byte
    /// 開始位置は spec 通り sync_zero_end + syncOnes に固定。
    /// </summary>
    public static SyncMatch? FindNextSync(ReadOnlySpan<byte> bits, int startAt, int minZeros, int syncOnes)
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
            if (zeroCount >= minZeros && oneCount >= syncOnes)
            {
                return new SyncMatch
                {
                    LeaderEndIndex = zeroStart,
                    ZeroRunLength = zeroCount,
                    OneRunLength = oneCount,
                    DataStartIndex = oneStart + syncOnes,  // exact syncOnes 後
                };
            }
            // Not a valid sync — keep scanning past these 0s+1s.
            if (oneCount == 0) return null;  // bit stream ended in a zero run
        }
    }

    /// <summary>
    /// X1 IPL spec (= 互換 IPL MT_RDBYTE 実装と一致): 通常 byte (= info / data) を
    /// framed bit 列で書き出す:
    ///   1 byte = "1" (start) + 8 data bits MSB first = 9 bit
    /// 「立ち上がり検出」 は FSK signal level の physical 同期動作で logical bit には
    /// 現れない (= IPL MT_RDBIT を 9 回 read、 start 1 + 8 data)。
    /// </summary>
    public static byte[] WriteBytes(ReadOnlySpan<byte> bytes)
    {
        var bits = new byte[bytes.Length * 9];
        int p = 0;
        foreach (var b in bytes)
        {
            bits[p++] = 1;  // start bit "1" (= spec 「最初に '1'」)
            for (int k = 7; k >= 0; k--)
                bits[p++] = (byte)((b >> k) & 1);
        }
        return bits;
    }

    /// <summary>
    /// Read <paramref name="count"/> framed normal bytes (= 9 bit/byte: "1" start +
    /// 8 data MSB)。 start bit が "1" でなければ throw。
    /// 互換 IPL MT_RDBYTE と同じ logic (= start 1 確認後 8 data 読み)。
    /// </summary>
    public static byte[] ReadBytes(ReadOnlySpan<byte> bits, int start, int count, out int endIndex)
    {
        var bytes = new byte[count];
        int pos = start;
        for (int n = 0; n < count; n++)
        {
            if (pos + 9 > bits.Length)
                throw new InvalidDataException($"unexpected end of bit stream while reading byte {n}/{count} at bit {pos}");
            if (bits[pos] != 1)
                throw new InvalidDataException($"missing start bit '1' at position {pos} (byte {n}/{count})");
            int v = 0;
            for (int k = 0; k < 8; k++)
                v = (v << 1) | bits[pos + 1 + k];
            bytes[n] = (byte)v;
            pos += 9;
        }
        endIndex = pos;
        return bytes;
    }

    // checksum byte も通常 byte と同じ 9 bit/byte framing (= start "1" + 8 data MSB)、
    // X1Program.Encode / Decode で WriteBytes / ReadBytes を info + checksum まとめて
    // 呼ぶため 専用 WriteChecksumBytes / ReadChecksumBytes は不要 (= 削除済)。
    // 純正 / 互換両 X1 IPL で動作確認済 (= retropc tapeformat.html の「最初の '1' なし」
    // 表記は誤読、 実 X1 tape 逆引きで 9 bit/byte 確定)。

    /// <summary>
    /// X1 IPL sync pattern を組み立てる:
    ///   leader "1" × leaderOnes + "0" × syncZeros + "1" × syncOnes
    /// 末尾の byte は通常 framing (= start "1") で、 sync ones 末尾と byte start "1" の
    /// 境界は互換 IPL が MT_RDBIT で edge 検出する。 logical bit としては境界 marker
    /// 不要 (= edge bit / silent を入れない方針)。
    /// </summary>
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
    /// X1 IPL spec 通りの checksum (= popcount 系):
    ///   payload 全 byte に含まれる **bit '1' の総数** を 16-bit modular sum で計算、
    ///   big-endian (high byte then low byte) で block 末尾に書く。
    /// 一般的な additive sum ではないことに注意 (= spec: 「読み込んだデータの
    /// 加算値ではなく '1' の個数」、 「2バイト範囲での加算なので、 65535 以上は
    /// 0 からカウントし直し」)。
    /// </summary>
    public static ushort ComputeChecksum(ReadOnlySpan<byte> payload)
    {
        int s = 0;
        foreach (var b in payload)
            s += System.Numerics.BitOperations.PopCount(b);
        return (ushort)s;  // 16-bit modular (= 自動で & 0xFFFF)
    }
}
