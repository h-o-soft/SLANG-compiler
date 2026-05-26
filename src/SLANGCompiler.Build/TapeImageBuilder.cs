using System;
using System.IO;
using SLANGCompiler.Build.TapeFormats;
using SLANGCompiler.Runtime;

namespace SLANGCompiler.Build;

/// <summary>
/// `--emit tape` で .bin → .tap (+ optional .wav) を生成する builder。
/// Phase B: X1 tape (.tap / .wav) 出力対応 (= OS なし boot を tape header の
/// load/exec addr で実現)。
/// </summary>
public class TapeImageBuilder
{
    /// <summary>
    /// env file `tape:` section + CLI option + default を merge した解決済 config。
    /// </summary>
    internal sealed record ResolvedTapeConfig(
        string Name, int Load, int Exec,
        int WavSampleRate, int WavBits);

    /// <summary>
    /// CLI option / env.Tape / default の優先順 (= CLI > env > default) で merge。
    /// default: Load = env.DefaultOrg / Exec = Load / Name = output basename。
    /// test しやすさのため internal static、 X1NativeTapeTests から呼ぶ。
    /// </summary>
    internal static ResolvedTapeConfig MergeTapeConfig(
        EnvironmentConfig env, Driver.Options opts, string outputBase)
    {
        int defaultLoad = opts.TapeLoad ?? env.Tape?.Load ?? env.DefaultOrg;
        int defaultExec = opts.TapeExec ?? env.Tape?.Exec ?? defaultLoad;
        return new ResolvedTapeConfig(
            Name: opts.TapeName
                ?? env.Tape?.Name
                ?? Path.GetFileNameWithoutExtension(outputBase),
            Load: defaultLoad,
            Exec: defaultExec,
            WavSampleRate: env.Tape?.WavSampleRate ?? 48000,
            WavBits: env.Tape?.WavBits ?? 8);
    }

    /// <summary>
    /// .bin → .tap (+ optional .wav) 生成。 成功時 0、 失敗時 1+ (stderr に message)。
    /// Driver からのみ呼ばれる (= public 不要、 ResolvedTapeConfig が internal なので
    /// Build も internal に揃える)。
    /// </summary>
    internal int Build(byte[] binData, ResolvedTapeConfig cfg, string outputBase,
                       bool emitWav, bool verbose)
    {
        // === Validation: silent truncate / wrong-output を完全排除 ===
        if (binData.Length == 0)
        {
            Console.Error.WriteLine("slangbuild: empty bin (0 byte) cannot be tape-encoded");
            return 1;
        }
        if (binData.Length > 0xFFFF)
        {
            Console.Error.WriteLine(
                $"slangbuild: bin size {binData.Length} byte exceeds X1 tape limit (= 65535 byte)");
            return 1;
        }
        if (cfg.Load < 0 || cfg.Load > 0xFFFF)
        {
            Console.Error.WriteLine($"slangbuild: tape.load ${cfg.Load:X} out of 16-bit range");
            return 1;
        }
        if (cfg.Exec < 0 || cfg.Exec > 0xFFFF)
        {
            Console.Error.WriteLine($"slangbuild: tape.exec ${cfg.Exec:X} out of 16-bit range");
            return 1;
        }
        if (cfg.Load + binData.Length - 1 > 0xFFFF)
        {
            Console.Error.WriteLine(
                $"slangbuild: load ${cfg.Load:X4} + size {binData.Length} overflows 16-bit memory");
            return 1;
        }
        if (cfg.WavBits != 8 && cfg.WavBits != 16)
        {
            Console.Error.WriteLine($"slangbuild: wav_bits must be 8 or 16 (got {cfg.WavBits})");
            return 1;
        }
        if (cfg.WavSampleRate <= 0)
        {
            Console.Error.WriteLine(
                $"slangbuild: wav_sample_rate must be > 0 (got {cfg.WavSampleRate})");
            return 1;
        }
        if (!IsValidX1FileName(cfg.Name))
        {
            Console.Error.WriteLine(
                $"slangbuild: tape name `{cfg.Name}` invalid " +
                "(= ASCII printable 0x20-0x7E + 1..13 char、 silent truncate しない仕様)");
            return 1;
        }

        // X1Program 構築 (= validation 後なので ushort cast 安全)
        var program = new X1Program
        {
            Data = binData,
            Info = new X1InfoBlock
            {
                BootFlag = 0x01,
                FileName = cfg.Name.ToUpperInvariant().PadRight(13),
                Extension = "BIN",
                Password = 0x20,
                DataSize = (ushort)binData.Length,
                LoadAddress = (ushort)cfg.Load,
                ExecuteAddress = (ushort)cfg.Exec,
            },
        };

        // tape header name (= TapHeader.Name 17 char) は cfg.Name と同じ ToUpper、
        // padding は tapcnv 側で処理 (= SLANG 側で truncate しない)。
        var tapeName = cfg.Name.ToUpperInvariant();

        // .tap (sampleRate 8000 で encode、 tapeName 引数を必ず渡す)
        var tap8k = program.Encode(sampleRate: 8000, tapeName: tapeName);
        tap8k.Save(outputBase + ".tap");

        // optional .wav (tapeName 同一、 sample rate / bits は cfg)
        if (emitWav)
        {
            var tapForWav = program.Encode(
                sampleRate: (uint)cfg.WavSampleRate, tapeName: tapeName);
            WavWriter.WriteFromTap(
                outputBase + ".wav", tapForWav,
                targetSampleRate: (uint)cfg.WavSampleRate,
                bitsPerSample: cfg.WavBits,
                amplitude: 0.8);
        }
        if (verbose)
        {
            Console.WriteLine(
                $"  generated: {outputBase}.tap" + (emitWav ? $" + {outputBase}.wav" : ""));
            Console.WriteLine(
                $"    tape name: {tapeName}, load: ${cfg.Load:X4}, exec: ${cfg.Exec:X4}");
        }
        return 0;
    }

    /// <summary>
    /// X1 FileName 検証: ASCII printable (0x20-0x7E) + 1..13 char。
    /// silent truncate は禁止 (= 仕様外なら明示 error で reject)。
    /// </summary>
    private static bool IsValidX1FileName(string name)
    {
        if (string.IsNullOrEmpty(name) || name.Length > 13) return false;
        foreach (var ch in name)
            if (ch < 0x20 || ch > 0x7E) return false;
        return true;
    }
}
