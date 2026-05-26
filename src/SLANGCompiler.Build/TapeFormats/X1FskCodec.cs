namespace SLANGCompiler.Build.TapeFormats;

/// <summary>
/// FSK codec for SHARP X1 cassette format.
///
/// The X1 cassette interface records each tape bit as a single square-wave cycle:
///   "0" bit = 4 kHz cycle (period 250 us)
///   "1" bit = 2 kHz cycle (period 500 us)
///
/// The IPL ROM decodes by looking 184 us after each rising edge and reading the
/// signal level. At 8 kHz sample rate that maps to "one sample later". A bit
/// period therefore spans either 2 samples ("0") or 4 samples ("1") at 8 kHz.
///
/// Encoding emits the canonical phase: each bit period starts HIGH.
///   "0" pattern (2 samples): 1 0
///   "1" pattern (4 samples): 1 1 0 0
/// Decoding measures the interval between consecutive rising edges, classifying
/// by closeness to the expected sample counts (handles boundary frequency
/// transitions gracefully).
/// </summary>
public static class X1FskCodec
{
    public const double FreqZeroHz = 4000.0;
    public const double FreqOneHz = 2000.0;

    /// <summary>
    /// Demodulate FSK samples into a sequence of tape bits.
    /// Mirrors the X1 IPL ROM's hardware detection: for every rising edge, look
    /// at the signal level ~184us later and emit the bit accordingly (low =>
    /// "0" bit, high => "1" bit). After emitting a bit, advance into the bit
    /// period by 80 % so that the immediately-following rising edge counts as
    /// the start of the *next* bit (avoids re-triggering on the second half-
    /// cycle of a "0" bit while still tolerating phase jitter ±20 %).
    /// </summary>
    public static byte[] Demodulate(ReadOnlySpan<byte> samples, uint sampleRate)
    {
        // 184us in samples (IPL probes signal level this long after each rising edge)
        int probeOffset = Math.Max(1, (int)Math.Round(184e-6 * sampleRate));
        int samplesForZero = (int)Math.Round(sampleRate / FreqZeroHz);  // 2 at 8kHz
        // Refractory period: skip the rest of the just-decoded bit's period so we
        // don't re-trigger inside it. Use the shorter "0" period * 0.8 to keep
        // headroom against jitter.
        int refractory = Math.Max(1, (int)Math.Floor(samplesForZero * 0.8));

        var bits = new List<byte>(samples.Length / 4);
        int i = 1;
        while (i < samples.Length)
        {
            if (samples[i] == 1 && samples[i - 1] == 0)
            {
                int probe = i + probeOffset;
                if (probe >= samples.Length) break;
                bits.Add(samples[probe]);
                i += refractory;  // jump past the bit's first half-cycle
                continue;
            }
            i++;
        }
        return bits.ToArray();
    }

    /// <summary>
    /// Modulate a tape-bit sequence into a sample stream.
    /// Each "0" bit emits one cycle at 4 kHz; each "1" bit emits one cycle at 2 kHz.
    /// All cycles start HIGH so successive bits flow naturally.
    /// </summary>
    public static byte[] Modulate(ReadOnlySpan<byte> bits, uint sampleRate)
    {
        int samplesForZero = (int)Math.Round(sampleRate / FreqZeroHz);
        int samplesForOne = (int)Math.Round(sampleRate / FreqOneHz);
        if (samplesForZero < 2 || samplesForOne < 2)
            throw new InvalidOperationException($"sample rate {sampleRate} too low for FSK");

        int totalSamples = 0;
        foreach (var b in bits)
            totalSamples += b == 0 ? samplesForZero : samplesForOne;

        var samples = new byte[totalSamples];
        int p = 0;
        foreach (var b in bits)
        {
            int period = b == 0 ? samplesForZero : samplesForOne;
            int halfHigh = period / 2;
            for (int i = 0; i < period; i++)
                samples[p++] = (byte)(i < halfHigh ? 1 : 0);
        }
        return samples;
    }

    internal static List<int> FindRisingEdges(ReadOnlySpan<byte> samples)
    {
        var edges = new List<int>(samples.Length / 4);
        for (int i = 1; i < samples.Length; i++)
        {
            if (samples[i] == 1 && samples[i - 1] == 0)
                edges.Add(i);
        }
        return edges;
    }
}
