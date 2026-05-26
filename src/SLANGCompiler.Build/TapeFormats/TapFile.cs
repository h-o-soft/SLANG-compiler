namespace SLANGCompiler.Build.TapeFormats;

/// <summary>
/// In-memory representation of a SHARP X1 ".tap" file.
///
/// The file is a 40-byte header (<see cref="TapHeader"/>) followed by the recorded
/// cassette tape signal as a bit-packed sample stream:
///   - One bit per audio sample, MSB-first inside each byte
///   - Bit value 1 = high signal (logical "tape on"), 0 = low signal
///   - The sample rate is stored in the header (typically 8000 Hz)
///
/// We hold samples expanded (1 byte per sample, 0 or 1) so that filtering, FSK
/// demodulation, and encoding code can work on a flat array without packing math.
/// </summary>
public sealed class TapFile
{
    public TapHeader Header { get; }
    public byte[] Samples { get; }

    public TapFile(TapHeader header, byte[] samples)
    {
        Header = header;
        Samples = samples;
    }

    public static TapFile Load(string path)
    {
        var bytes = File.ReadAllBytes(path);
        return FromBytes(bytes);
    }

    public static TapFile FromBytes(ReadOnlySpan<byte> raw)
    {
        // CommonSourceCodeProject datarec.cpp 互換: 先頭 4 byte が "TAPE" magic なら
        // Extended format (40 byte header)、 それ以外は Simple format (= 先頭 4 byte
        // sample rate uint32 LE + body)。
        if (raw.Length >= 4 &&
            raw[0] == (byte)'T' && raw[1] == (byte)'A' &&
            raw[2] == (byte)'P' && raw[3] == (byte)'E')
        {
            var header = TapHeader.Read(raw);
            var body = raw.Slice(TapHeader.SizeBytes);
            var samples = UnpackSamples(body, (int)header.DataSizeBits);
            return new TapFile(header, samples);
        }
        else
        {
            // Simple format: 先頭 4 byte sample rate + payload (= 全 byte sample stream)
            var rate = (uint)(raw[0] | (raw[1] << 8) | (raw[2] << 16) | (raw[3] << 24));
            var body = raw.Slice(4);
            int nBits = body.Length * 8;
            var header = new TapHeader
            {
                Name = "(simple)",
                SampleRate = rate,
                DataSizeBits = (uint)nBits,
            };
            var samples = UnpackSamples(body, nBits);
            return new TapFile(header, samples);
        }
    }

    public void Save(string path)
    {
        // Sync header DataSizeBits with current sample count, then write
        Header.DataSizeBits = (uint)Samples.Length;
        var headerBytes = Header.ToBytes();
        var bodyBytes = PackSamples(Samples);

        using var fs = File.Create(path);
        fs.Write(headerBytes);
        fs.Write(bodyBytes);
    }

    public static byte[] UnpackSamples(ReadOnlySpan<byte> packed, int nBits)
    {
        var samples = new byte[nBits];
        int p = 0;
        foreach (var b in packed)
        {
            for (int i = 0; i < 8; i++)
            {
                if (p >= nBits) return samples;
                samples[p++] = (byte)((b & (0x80 >> i)) != 0 ? 1 : 0);
            }
        }
        return samples;
    }

    public static byte[] PackSamples(ReadOnlySpan<byte> samples)
    {
        int byteLen = (samples.Length + 7) / 8;
        var packed = new byte[byteLen];
        for (int i = 0; i < samples.Length; i++)
        {
            if (samples[i] != 0)
                packed[i >> 3] |= (byte)(0x80 >> (i & 7));
        }
        return packed;
    }
}
