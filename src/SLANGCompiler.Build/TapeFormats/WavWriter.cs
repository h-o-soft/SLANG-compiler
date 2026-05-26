using System.Buffers.Binary;

namespace SLANGCompiler.Build.TapeFormats;

/// <summary>
/// Minimal RIFF/WAVE writer for PCM mono output.
///
/// Used to convert an X1 cassette signal (a 1-bit sample stream) into a sound
/// file that real X1 hardware can read through its cassette input.
///
/// - The default 48000 Hz / 8-bit mono mirrors the format that the xmil
///   emulator's CMT input accepts ("8bit/mono/48KHz" per its WAV loader).
/// - 16-bit is supported for archival or for resampling against players that
///   prefer signed PCM.
/// - The X1 cassette interface is edge-triggered, so a clean square wave at
///   any reasonable amplitude works. We use 80 % of full scale by default.
/// </summary>
public static class WavWriter
{
    public const uint DefaultSampleRate = 48000;
    public const int DefaultBitsPerSample = 8;

    /// <summary>
    /// Modulate <paramref name="bits"/> (X1 tape bits, "0" = 4 kHz, "1" = 2 kHz)
    /// into a PCM WAV file. Generates the FSK waveform directly at
    /// <paramref name="sampleRate"/>.
    /// </summary>
    public static void WriteFromTapeBits(
        string path,
        ReadOnlySpan<byte> tapeBits,
        uint sampleRate = DefaultSampleRate,
        int bitsPerSample = DefaultBitsPerSample,
        double amplitude = 0.8)
    {
        var samples = X1FskCodec.Modulate(tapeBits, sampleRate);
        WriteFromBinarySamples(path, samples, sampleRate, bitsPerSample, amplitude);
    }

    /// <summary>
    /// Write a binary sample stream (each byte = 0 or 1, "high" or "low" tape
    /// signal) to a PCM WAV file.
    /// </summary>
    public static void WriteFromBinarySamples(
        string path,
        ReadOnlySpan<byte> samples,
        uint sampleRate,
        int bitsPerSample = DefaultBitsPerSample,
        double amplitude = 0.8)
    {
        if (bitsPerSample != 8 && bitsPerSample != 16)
            throw new ArgumentException($"bits per sample must be 8 or 16, got {bitsPerSample}");
        if (amplitude <= 0 || amplitude > 1.0)
            throw new ArgumentOutOfRangeException(nameof(amplitude), "must be in (0, 1]");

        int bytesPerSample = bitsPerSample / 8;
        int dataSize = samples.Length * bytesPerSample;

        using var fs = File.Create(path);
        using var w = new BinaryWriter(fs);

        // RIFF chunk
        w.Write("RIFF"u8);
        w.Write((uint)(36 + dataSize));        // file size - 8
        w.Write("WAVE"u8);

        // fmt sub-chunk
        w.Write("fmt "u8);
        w.Write((uint)16);                     // fmt chunk size
        w.Write((ushort)1);                    // PCM
        w.Write((ushort)1);                    // mono
        w.Write(sampleRate);                   // sample rate
        w.Write((uint)(sampleRate * bytesPerSample)); // byte rate
        w.Write((ushort)bytesPerSample);       // block align
        w.Write((ushort)bitsPerSample);

        // data sub-chunk
        w.Write("data"u8);
        w.Write((uint)dataSize);

        if (bitsPerSample == 8)
        {
            // 8-bit WAV is UNSIGNED (0..255), center at 128
            byte high = (byte)Math.Clamp(128 + (int)Math.Round(127 * amplitude), 0, 255);
            byte low = (byte)Math.Clamp(128 - (int)Math.Round(128 * amplitude), 0, 255);
            for (int i = 0; i < samples.Length; i++)
                w.Write(samples[i] != 0 ? high : low);
        }
        else
        {
            // 16-bit WAV is SIGNED little-endian
            short high = (short)Math.Round(32767 * amplitude);
            short low = (short)-Math.Round(32768 * amplitude);
            Span<byte> buf = stackalloc byte[2];
            for (int i = 0; i < samples.Length; i++)
            {
                BinaryPrimitives.WriteInt16LittleEndian(buf, samples[i] != 0 ? high : low);
                w.Write(buf);
            }
        }
    }

    /// <summary>
    /// Convert an in-memory <see cref="TapFile"/> to a WAV file.
    /// The tap's sample stream is written directly (resampled by nearest-neighbor
    /// repetition if <paramref name="targetSampleRate"/> differs from the tap's
    /// stored sample rate).
    /// </summary>
    public static void WriteFromTap(
        string path,
        TapFile tap,
        uint? targetSampleRate = null,
        int bitsPerSample = DefaultBitsPerSample,
        double amplitude = 0.8)
    {
        uint rate = targetSampleRate ?? tap.Header.SampleRate;
        ReadOnlySpan<byte> samples;
        byte[]? resampled = null;
        if (rate == tap.Header.SampleRate)
        {
            samples = tap.Samples;
        }
        else
        {
            resampled = Resample(tap.Samples, tap.Header.SampleRate, rate);
            samples = resampled;
        }
        WriteFromBinarySamples(path, samples, rate, bitsPerSample, amplitude);
    }

    /// <summary>
    /// Nearest-neighbor (sample-and-hold) resampling — adequate for square-wave
    /// 1-bit signals where there is no benefit to interpolation. Each input
    /// sample is held for ratio output samples.
    /// </summary>
    internal static byte[] Resample(ReadOnlySpan<byte> input, uint srcRate, uint dstRate)
    {
        if (srcRate == 0) throw new ArgumentException("srcRate must be > 0");
        long n = (long)input.Length * dstRate / srcRate;
        var output = new byte[n];
        for (long j = 0; j < n; j++)
        {
            long i = j * srcRate / dstRate;
            if (i >= input.Length) i = input.Length - 1;
            output[j] = input[(int)i];
        }
        return output;
    }
}
