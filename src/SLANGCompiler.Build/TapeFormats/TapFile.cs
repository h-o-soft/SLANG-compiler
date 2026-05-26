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
        var header = TapHeader.Read(raw);
        var body = raw.Slice(TapHeader.SizeBytes);
        var samples = UnpackSamples(body, (int)header.DataSizeBits);
        return new TapFile(header, samples);
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
