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
    /// 1 stage .bin → .tap (+ optional .wav) 生成の overload (= 既存 caller 互換、 test 互換)。
    /// 多段 tape は新 signature (= additionalStages 引数あり) を使う。
    /// </summary>
    internal int Build(byte[] binData, ResolvedTapeConfig cfg, string outputBase,
                       bool emitWav, bool verbose)
        => Build(binData, cfg, null, outputBase, emitWav, verbose);

    /// <summary>
    /// .bin → .tap (+ optional .wav) 生成。 成功時 0、 失敗時 1+ (stderr に message)。
    /// additionalStages != null + Count > 0 で多段 tape (= main + overlay stages 連結) 生成。
    /// Driver からのみ呼ばれる (= public 不要、 ResolvedTapeConfig が internal なので
    /// Build も internal に揃える)。
    /// </summary>
    internal int Build(byte[] mainBin, ResolvedTapeConfig mainCfg,
                       List<(byte[] bin, ResolvedTapeConfig cfg)>? additionalStages,
                       string outputBase, bool emitWav, bool verbose)
    {
        // === main stage validation (= silent truncate / wrong-output を完全排除) ===
        var rc = ValidateStage(mainBin, mainCfg, "main");
        if (rc != 0) return rc;

        // === additional stages (= overlay) validation ===
        if (additionalStages != null)
        {
            for (int i = 0; i < additionalStages.Count; i++)
            {
                rc = ValidateStage(additionalStages[i].bin, additionalStages[i].cfg,
                                   $"stage[{i}] ({additionalStages[i].cfg.Name})");
                if (rc != 0) return rc;
            }
        }

        // X1Program 群 構築 (= validation 後なので ushort cast 安全)
        var programs = new List<X1Program> { BuildX1Program(mainBin, mainCfg) };
        if (additionalStages != null)
        {
            foreach (var (bin, cfg) in additionalStages)
                programs.Add(BuildX1Program(bin, cfg));
        }

        // tape header name (= TapHeader.Name 17 char) は main cfg.Name と同じ ToUpper
        var tapeName = mainCfg.Name.ToUpperInvariant();

        // .tap (sampleRate 8000 で encode、 多段なら ConcatenatePrograms で連結)
        var tap8k = programs.Count == 1
            ? programs[0].Encode(sampleRate: 8000, tapeName: tapeName)
            : X1Program.ConcatenatePrograms(programs, sampleRate: 8000, tapeName: tapeName);
        tap8k.Save(outputBase + ".tap");

        // optional .wav (= main sample rate / bits)
        if (emitWav)
        {
            var tapForWav = programs.Count == 1
                ? programs[0].Encode(sampleRate: (uint)mainCfg.WavSampleRate, tapeName: tapeName)
                : X1Program.ConcatenatePrograms(programs, sampleRate: (uint)mainCfg.WavSampleRate, tapeName: tapeName);
            WavWriter.WriteFromTap(
                outputBase + ".wav", tapForWav,
                targetSampleRate: (uint)mainCfg.WavSampleRate,
                bitsPerSample: mainCfg.WavBits,
                amplitude: 0.8);
        }
        if (verbose)
        {
            Console.WriteLine(
                $"  generated: {outputBase}.tap" + (emitWav ? $" + {outputBase}.wav" : ""));
            Console.WriteLine(
                $"    main stage: name={tapeName}, load=${mainCfg.Load:X4}, exec=${mainCfg.Exec:X4}");
            if (additionalStages != null)
            {
                for (int i = 0; i < additionalStages.Count; i++)
                {
                    var s = additionalStages[i];
                    Console.WriteLine(
                        $"    stage[{i}]: name={s.cfg.Name}, load=${s.cfg.Load:X4}, size={s.bin.Length}");
                }
            }
        }
        return 0;
    }

    /// <summary>
    /// 1 stage 分 validation (= main / overlay 共通)。 失敗時 stderr + 非 0 return。
    /// </summary>
    private static int ValidateStage(byte[] binData, ResolvedTapeConfig cfg, string label)
    {
        if (binData.Length == 0)
        {
            Console.Error.WriteLine($"slangbuild: {label} empty bin (0 byte) cannot be tape-encoded");
            return 1;
        }
        if (binData.Length > 0xFFFF)
        {
            Console.Error.WriteLine(
                $"slangbuild: {label} bin size {binData.Length} byte exceeds X1 tape limit (= 65535 byte)");
            return 1;
        }
        if (cfg.Load < 0 || cfg.Load > 0xFFFF)
        {
            Console.Error.WriteLine($"slangbuild: {label} tape.load ${cfg.Load:X} out of 16-bit range");
            return 1;
        }
        if (cfg.Exec < 0 || cfg.Exec > 0xFFFF)
        {
            Console.Error.WriteLine($"slangbuild: {label} tape.exec ${cfg.Exec:X} out of 16-bit range");
            return 1;
        }
        if (cfg.Load + binData.Length - 1 > 0xFFFF)
        {
            Console.Error.WriteLine(
                $"slangbuild: {label} load ${cfg.Load:X4} + size {binData.Length} overflows 16-bit memory");
            return 1;
        }
        if (cfg.WavBits != 8 && cfg.WavBits != 16)
        {
            Console.Error.WriteLine($"slangbuild: {label} wav_bits must be 8 or 16 (got {cfg.WavBits})");
            return 1;
        }
        if (cfg.WavSampleRate <= 0)
        {
            Console.Error.WriteLine(
                $"slangbuild: {label} wav_sample_rate must be > 0 (got {cfg.WavSampleRate})");
            return 1;
        }
        if (!IsValidX1FileName(cfg.Name))
        {
            Console.Error.WriteLine(
                $"slangbuild: {label} tape name `{cfg.Name}` invalid " +
                "(= ASCII printable 0x20-0x7E + 1..13 char、 silent truncate しない仕様)");
            return 1;
        }
        return 0;
    }

    /// <summary>
    /// 1 stage 分 X1Program 構築。 validation 後なので ushort cast 安全。
    /// </summary>
    private static X1Program BuildX1Program(byte[] binData, ResolvedTapeConfig cfg)
    {
        return new X1Program
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
