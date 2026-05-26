using System.Buffers.Binary;

namespace SLANGCompiler.Build.TapeFormats;

/// <summary>
/// High-level decoded view of one program loaded from a SHARP X1 cassette tape:
/// the parsed information block plus the raw data block payload.
/// </summary>
public sealed class X1Program
{
    public X1InfoBlock Info { get; set; } = new();
    public byte[] Data { get; set; } = Array.Empty<byte>();
    public bool InfoChecksumOk { get; set; }
    public bool DataChecksumOk { get; set; }

    /// <summary>
    /// Decode the first program found in a TAP file's bit stream.
    /// </summary>
    public static X1Program Decode(TapFile tap)
    {
        var bits = X1FskCodec.Demodulate(tap.Samples, tap.Header.SampleRate);
        return DecodeFromBits(bits);
    }

    public static X1Program DecodeFromBits(byte[] bits)
    {
        // --- Information block ---
        var infoSync = X1TapeFraming.FindNextSync(
            bits, 0,
            X1TapeFraming.InfoBlockMinSyncZeros,
            X1TapeFraming.InfoBlockSyncOnes);   // exact syncOnes (= 40) で byte 境界確定
        if (infoSync is null)
            throw new InvalidDataException("information block sync not found");

        // info 32 byte + checksum 2 byte 全部 9 bit/byte framing (= 通常 byte と同じ)
        // checksum 値は popcount mod 65536、 byte order は **big-endian** (= 実 X1
        // tape decode で確認、 LE と読むと checksum 一致しない)
        var infoRaw = X1TapeFraming.ReadBytes(
            bits, infoSync.DataStartIndex,
            X1InfoBlock.FieldsSize + X1TapeFraming.ChecksumSize, out var afterInfo);
        var info = X1InfoBlock.FromBytes(infoRaw.AsSpan(0, X1InfoBlock.FieldsSize));
        ushort infoCkExpected = BinaryPrimitives.ReadUInt16BigEndian(
            infoRaw.AsSpan(X1InfoBlock.FieldsSize, X1TapeFraming.ChecksumSize));
        ushort infoCkActual = X1TapeFraming.ComputeChecksum(
            infoRaw.AsSpan(0, X1InfoBlock.FieldsSize));
        bool infoOk = infoCkExpected == infoCkActual;

        // --- Data block ---
        var dataSync = X1TapeFraming.FindNextSync(
            bits, afterInfo,
            X1TapeFraming.DataBlockSyncZeros,
            X1TapeFraming.DataBlockSyncOnes);   // exact syncOnes (= 20)
        if (dataSync is null)
            throw new InvalidDataException("data block sync not found");

        int dataLen = info.DataSize;
        // data N + checksum 2 byte 全部 9 bit/byte framing、 checksum BE
        var dataRaw = X1TapeFraming.ReadBytes(
            bits, dataSync.DataStartIndex,
            dataLen + X1TapeFraming.ChecksumSize, out _);
        ushort dataCkExpected = BinaryPrimitives.ReadUInt16BigEndian(
            dataRaw.AsSpan(dataLen, 2));
        ushort dataCkActual = X1TapeFraming.ComputeChecksum(dataRaw.AsSpan(0, dataLen));
        bool dataOk = dataCkExpected == dataCkActual;

        return new X1Program
        {
            Info = info,
            Data = dataRaw.AsSpan(0, dataLen).ToArray(),
            InfoChecksumOk = infoOk,
            DataChecksumOk = dataOk,
        };
    }

    /// <summary>
    /// Encode this program back into a complete TAP file.
    /// The resulting file is loadable by the X1 IPL.
    /// </summary>
    public TapFile Encode(uint sampleRate = 8000, string? tapeName = null)
    {
        // Keep the info block's data size in sync with the actual payload.
        Info.DataSize = (ushort)Data.Length;

        // Info block: payload 32 + checksum 2 = 34 byte 連結後、 一括 WriteBytes
        // (= 9 bit/byte、 checksum も同じ framing)。
        // checksum 値 = popcount mod 65536、 **byte order = big-endian** (= 実 X1
        // tape decode で逆引きして確認、 LE では emulator が checksum NG と判定)。
        var infoBytes = Info.ToBytes();  // 32 byte
        ushort infoCk = X1TapeFraming.ComputeChecksum(infoBytes);
        var infoWithCk = new byte[X1TapeFraming.InfoBlockTotalSize];
        infoBytes.CopyTo(infoWithCk, 0);
        BinaryPrimitives.WriteUInt16BigEndian(
            infoWithCk.AsSpan(X1InfoBlock.FieldsSize), infoCk);

        // Data block: 同じく一括 framing
        ushort dataCk = X1TapeFraming.ComputeChecksum(Data);
        var dataWithCk = new byte[Data.Length + X1TapeFraming.ChecksumSize];
        Data.CopyTo(dataWithCk, 0);
        BinaryPrimitives.WriteUInt16BigEndian(
            dataWithCk.AsSpan(Data.Length), dataCk);

        // Build full bit stream:
        //   [info leader 1×8000][sync 0×40][sync 1×41][info+ck 34 × 9 bit]
        //   [data leader 1×4000][sync 0×20][sync 1×21][data+ck (N+2) × 9 bit]
        //   [trailing 1×1000]  (tape stop 余裕)
        var bits = new List<byte>(capacity:
            X1TapeFraming.DefaultInfoLeaderOnes + 200 +
            infoWithCk.Length * 9 +
            X1TapeFraming.DefaultDataLeaderOnes + 200 +
            dataWithCk.Length * 9);

        bits.AddRange(X1TapeFraming.BuildSync(
            X1TapeFraming.DefaultInfoLeaderOnes,
            X1TapeFraming.InfoBlockSyncZeros,
            X1TapeFraming.InfoBlockSyncOnes));
        bits.AddRange(X1TapeFraming.WriteBytes(infoWithCk));    // 9 bit/byte × 34

        bits.AddRange(X1TapeFraming.BuildSync(
            X1TapeFraming.DefaultDataLeaderOnes,
            X1TapeFraming.DataBlockSyncZeros,
            X1TapeFraming.DataBlockSyncOnes));
        bits.AddRange(X1TapeFraming.WriteBytes(dataWithCk));    // 9 bit/byte × (N+2)

        // Pad with trailing silence / tape stop to be safe
        for (int i = 0; i < 1000; i++) bits.Add(1);

        var bitArr = bits.ToArray();
        var samples = X1FskCodec.Modulate(bitArr, sampleRate);

        var header = new TapHeader
        {
            Name = tapeName ?? "auto converted",
            WriteProtect = TapHeader.ProtectionWriteProtected,
            Format = TapHeader.FormatSampling,
            SampleRate = sampleRate,
            DataSizeBits = (uint)samples.Length,
            PositionBits = 0,
        };
        return new TapFile(header, samples);
    }
}
