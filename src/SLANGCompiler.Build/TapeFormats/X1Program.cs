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

    /// <summary>
    /// 複数 X1Program を 1 TapFile に連結 encode (= 多段 tape load 対応)。
    /// 全段で標準 sync 仕様 (= info 40/41 leader 8000、 data 20/21 leader 4000) を維持
    /// (= Codex 指摘反映、 短い inter-stage gap で sync 見失い回避)。
    /// 用途: SLANG #MODULE overlay → main + overlay._mN.bin を 1 .tap 自動連結。
    /// </summary>
    public static TapFile ConcatenatePrograms(
        List<X1Program> programs, uint sampleRate = 8000, string? tapeName = null)
    {
        if (programs == null || programs.Count == 0)
            throw new ArgumentException("programs list must contain at least one X1Program");

        var bits = new List<byte>();
        foreach (var program in programs)
        {
            // Keep info DataSize in sync with payload (= 各段独立 checksum + size)
            program.Info.DataSize = (ushort)program.Data.Length;

            var infoBytes = program.Info.ToBytes();   // 32 byte
            ushort infoCk = X1TapeFraming.ComputeChecksum(infoBytes);
            var infoWithCk = new byte[X1TapeFraming.InfoBlockTotalSize];
            infoBytes.CopyTo(infoWithCk, 0);
            BinaryPrimitives.WriteUInt16BigEndian(
                infoWithCk.AsSpan(X1InfoBlock.FieldsSize), infoCk);

            ushort dataCk = X1TapeFraming.ComputeChecksum(program.Data);
            var dataWithCk = new byte[program.Data.Length + X1TapeFraming.ChecksumSize];
            program.Data.CopyTo(dataWithCk, 0);
            BinaryPrimitives.WriteUInt16BigEndian(
                dataWithCk.AsSpan(program.Data.Length), dataCk);

            // 各段 標準 sync 仕様で emit (= 全段同じ leader / sync 数、 inter-stage
            // gap も 1 段目 と同 標準で実機 IPL の tolerance 範囲内)
            bits.AddRange(X1TapeFraming.BuildSync(
                X1TapeFraming.DefaultInfoLeaderOnes,    // = 8000
                X1TapeFraming.InfoBlockSyncZeros,        // = 40
                X1TapeFraming.InfoBlockSyncOnes));       // = 41
            bits.AddRange(X1TapeFraming.WriteBytes(infoWithCk));

            bits.AddRange(X1TapeFraming.BuildSync(
                X1TapeFraming.DefaultDataLeaderOnes,    // = 4000
                X1TapeFraming.DataBlockSyncZeros,        // = 20
                X1TapeFraming.DataBlockSyncOnes));       // = 21
            bits.AddRange(X1TapeFraming.WriteBytes(dataWithCk));
        }

        // trailing silence (= 全段共通、 tape stop 余裕)
        for (int i = 0; i < 1000; i++) bits.Add(1);

        var bitArr = bits.ToArray();
        var samples = X1FskCodec.Modulate(bitArr, sampleRate);
        var header = new TapHeader
        {
            Name = tapeName ?? "auto converted (multi-stage)",
            WriteProtect = TapHeader.ProtectionWriteProtected,
            Format = TapHeader.FormatSampling,
            SampleRate = sampleRate,
            DataSizeBits = (uint)samples.Length,
            PositionBits = 0,
        };
        return new TapFile(header, samples);
    }

    /// <summary>
    /// TapFile 内の 全 X1Program を順次 decode (= test 用、 ConcatenatePrograms 逆操作)。
    /// 既存 Decode は first only、 多段 .tap roundtrip 検証では本 method を使う。
    /// 各 program decode 失敗 (= 次 sync 見つからない) で break、 それまで取れた list 返却。
    /// </summary>
    public static List<X1Program> DecodeAll(TapFile tap)
    {
        var bits = X1FskCodec.Demodulate(tap.Samples, tap.Header.SampleRate);
        var result = new List<X1Program>();
        int pos = 0;
        while (pos < bits.Length)
        {
            X1Program? program;
            try
            {
                program = TryDecodeFromBits(bits, ref pos);
            }
            catch (InvalidDataException)
            {
                break;  // 次 sync 見つからない or block 不完全 = 終端
            }
            if (program == null) break;
            result.Add(program);
        }
        return result;
    }

    /// <summary>
    /// bits[pos..] から 1 X1Program decode、 pos を消費分進める (= DecodeAll 内部用)。
    /// 既存 DecodeFromBits を base に、 position 追跡 + null 返却で終端通知する版。
    /// </summary>
    private static X1Program? TryDecodeFromBits(byte[] bits, ref int pos)
    {
        var infoSync = X1TapeFraming.FindNextSync(
            bits, pos,
            X1TapeFraming.InfoBlockMinSyncZeros,
            X1TapeFraming.InfoBlockSyncOnes);
        if (infoSync is null) return null;

        var infoRaw = X1TapeFraming.ReadBytes(
            bits, infoSync.DataStartIndex,
            X1InfoBlock.FieldsSize + X1TapeFraming.ChecksumSize, out var afterInfo);
        var info = X1InfoBlock.FromBytes(infoRaw.AsSpan(0, X1InfoBlock.FieldsSize));
        ushort infoCkExpected = BinaryPrimitives.ReadUInt16BigEndian(
            infoRaw.AsSpan(X1InfoBlock.FieldsSize, X1TapeFraming.ChecksumSize));
        ushort infoCkActual = X1TapeFraming.ComputeChecksum(
            infoRaw.AsSpan(0, X1InfoBlock.FieldsSize));
        bool infoOk = infoCkExpected == infoCkActual;

        var dataSync = X1TapeFraming.FindNextSync(
            bits, afterInfo,
            X1TapeFraming.DataBlockSyncZeros,
            X1TapeFraming.DataBlockSyncOnes);
        if (dataSync is null) return null;

        int dataLen = info.DataSize;
        var dataRaw = X1TapeFraming.ReadBytes(
            bits, dataSync.DataStartIndex,
            dataLen + X1TapeFraming.ChecksumSize, out var afterData);
        ushort dataCkExpected = BinaryPrimitives.ReadUInt16BigEndian(
            dataRaw.AsSpan(dataLen, 2));
        ushort dataCkActual = X1TapeFraming.ComputeChecksum(dataRaw.AsSpan(0, dataLen));
        bool dataOk = dataCkExpected == dataCkActual;

        pos = afterData;  // 次 program decode 用に位置進める
        return new X1Program
        {
            Info = info,
            Data = dataRaw.AsSpan(0, dataLen).ToArray(),
            InfoChecksumOk = infoOk,
            DataChecksumOk = dataOk,
        };
    }
}
