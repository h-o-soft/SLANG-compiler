using SLANGCompiler.Build;
using SLANGCompiler.Runtime;
using Xunit;

namespace SLANGCompiler.Tests;

/// <summary>
/// x1native env の `tape:` section parse + TapeImageBuilder の validation /
/// MergeTapeConfig priority を pin する。
/// </summary>
public class X1NativeTapeTests
{
    private static string EnvFilePath()
    {
        var baseDir = AppContext.BaseDirectory;
        var dir = new DirectoryInfo(baseDir);
        while (dir != null && !File.Exists(Path.Combine(dir.FullName, "runtime", "env", "x1native.env")))
            dir = dir.Parent;
        Assert.NotNull(dir);
        return Path.Combine(dir!.FullName, "runtime", "env", "x1native.env");
    }

    [Fact]
    public void EnvironmentConfig_TapeSection_Parses()
    {
        // runtime/env/x1native.env の tape: section が TapeConfig に正しく parse
        var config = EnvironmentLoader.Load(EnvFilePath());
        Assert.NotNull(config.Tape);
        Assert.Equal("PROG", config.Tape!.Name);
        Assert.Equal(0x1000, config.Tape.Load);
        Assert.Equal(0x1000, config.Tape.Exec);
        // wav_sample_rate / wav_bits は env file で comment out 中 → null
        Assert.Null(config.Tape.WavSampleRate);
        Assert.Null(config.Tape.WavBits);
    }

    // === MergeTapeConfig 優先順 (CLI > env > default) ===

    private static Driver.Options MakeEmptyOpts() => new()
    {
        EmitMode = "tape",
    };

    [Fact]
    public void MergeTapeConfig_UsesEnvValues_WhenNoCli()
    {
        var env = EnvironmentLoader.Load(EnvFilePath());
        var opts = MakeEmptyOpts();
        var cfg = TapeImageBuilder.MergeTapeConfig(env, opts, "/tmp/HELLO");
        Assert.Equal("PROG", cfg.Name);          // env.Tape.Name
        Assert.Equal(0x1000, cfg.Load);          // env.Tape.Load
        Assert.Equal(0x1000, cfg.Exec);          // env.Tape.Exec
        Assert.Equal(48000, cfg.WavSampleRate);  // default (env で null)
        Assert.Equal(8, cfg.WavBits);            // default
    }

    [Fact]
    public void MergeTapeConfig_CLIOverridesEnv()
    {
        var env = EnvironmentLoader.Load(EnvFilePath());
        var opts = MakeEmptyOpts();
        opts.TapeName = "MYPROG";
        opts.TapeLoad = 0x8000;
        opts.TapeExec = 0x8100;
        var cfg = TapeImageBuilder.MergeTapeConfig(env, opts, "/tmp/HELLO");
        Assert.Equal("MYPROG", cfg.Name);
        Assert.Equal(0x8000, cfg.Load);
        Assert.Equal(0x8100, cfg.Exec);
    }

    [Fact]
    public void MergeTapeConfig_DefaultLoadFromEnvDefaultOrg_WhenNoTapeSection()
    {
        // env に tape: section が無い場合、 load は env.DefaultOrg にフォールバック
        var env = new EnvironmentConfig
        {
            EnvType = 1,
            OsType = 4,
            DefaultOrg = 0x2000,
            Tape = null,  // tape: 無し
        };
        var opts = MakeEmptyOpts();
        var cfg = TapeImageBuilder.MergeTapeConfig(env, opts, "/tmp/SAMPLE");
        Assert.Equal("SAMPLE", cfg.Name);       // output basename
        Assert.Equal(0x2000, cfg.Load);         // env.DefaultOrg fallback
        Assert.Equal(0x2000, cfg.Exec);         // = Load
    }

    [Fact]
    public void MergeTapeConfig_ExecDefaultsToLoad_WhenOnlyLoadGiven()
    {
        var env = new EnvironmentConfig
        {
            DefaultOrg = 0x1000,
            Tape = new TapeConfig { Load = 0x4000 },  // Exec 未指定
        };
        var opts = MakeEmptyOpts();
        var cfg = TapeImageBuilder.MergeTapeConfig(env, opts, "/tmp/X");
        Assert.Equal(0x4000, cfg.Load);
        Assert.Equal(0x4000, cfg.Exec);  // Load と同じ
    }

    // === TapeImageBuilder.Build validation ===

    private static TapeImageBuilder.ResolvedTapeConfig MakeCfg(
        string name = "PROG", int load = 0x1000, int exec = 0x1000,
        int wavRate = 48000, int wavBits = 8) =>
        new(name, load, exec, wavRate, wavBits);

    [Fact]
    public void Build_RejectsEmptyBin()
    {
        var rc = new TapeImageBuilder().Build(
            Array.Empty<byte>(), MakeCfg(), "/tmp/x", emitWav: false, verbose: false);
        Assert.NotEqual(0, rc);
    }

    [Fact]
    public void Build_RejectsOversizedBin()
    {
        var bin = new byte[0x10000];  // 65536 byte = X1 tape 上限 + 1
        var rc = new TapeImageBuilder().Build(
            bin, MakeCfg(), "/tmp/x", emitWav: false, verbose: false);
        Assert.NotEqual(0, rc);
    }

    [Fact]
    public void Build_RejectsOutOfRangeLoad()
    {
        var bin = new byte[10];
        var rc = new TapeImageBuilder().Build(
            bin, MakeCfg(load: 0x10000), "/tmp/x", emitWav: false, verbose: false);
        Assert.NotEqual(0, rc);
    }

    [Fact]
    public void Build_RejectsLoadOverflow()
    {
        // load + size - 1 > 0xFFFF (= 16-bit memory に収まらない)
        // load=$FF00 + size=257 → end = $FF00 + 256 = $10000 で 1 byte 超過
        var bin = new byte[257];
        var rc = new TapeImageBuilder().Build(
            bin, MakeCfg(load: 0xFF00), "/tmp/x", emitWav: false, verbose: false);
        Assert.NotEqual(0, rc);
    }

    [Fact]
    public void Build_RejectsInvalidWavBits()
    {
        var bin = new byte[10];
        var rc = new TapeImageBuilder().Build(
            bin, MakeCfg(wavBits: 24), "/tmp/x", emitWav: false, verbose: false);
        Assert.NotEqual(0, rc);
    }

    [Fact]
    public void Build_RejectsInvalidWavSampleRate()
    {
        var bin = new byte[10];
        var rc = new TapeImageBuilder().Build(
            bin, MakeCfg(wavRate: 0), "/tmp/x", emitWav: false, verbose: false);
        Assert.NotEqual(0, rc);
    }

    [Theory]
    [InlineData("")]                        // 空
    [InlineData("ABCDEFGHIJKLMN")]          // 14 char
    [InlineData("ABC\x01")]                 // 制御文字
    [InlineData("ABCあ")]               // 非 ASCII
    public void Build_RejectsInvalidFileName(string name)
    {
        var bin = new byte[10];
        var rc = new TapeImageBuilder().Build(
            bin, MakeCfg(name: name), "/tmp/x", emitWav: false, verbose: false);
        Assert.NotEqual(0, rc);
    }

    [Fact]
    public void Build_GeneratesTapFile_WithExpectedHeader()
    {
        var bin = new byte[16];
        for (int i = 0; i < bin.Length; i++) bin[i] = (byte)i;
        var tmp = Path.Combine(Path.GetTempPath(), "x1n_tape_test");
        var rc = new TapeImageBuilder().Build(
            bin, MakeCfg(name: "TEST", load: 0x1234, exec: 0x1234),
            tmp, emitWav: false, verbose: false);
        Assert.Equal(0, rc);
        Assert.True(File.Exists(tmp + ".tap"));
        var data = File.ReadAllBytes(tmp + ".tap");
        // TAP header magic 先頭 4 byte = "TAPE"
        Assert.Equal((byte)'T', data[0]);
        Assert.Equal((byte)'A', data[1]);
        Assert.Equal((byte)'P', data[2]);
        Assert.Equal((byte)'E', data[3]);
        // tape name 5-21 byte (= 17 char) は "TEST" + null padding
        Assert.Equal((byte)'T', data[4]);
        Assert.Equal((byte)'E', data[5]);
        Assert.Equal((byte)'S', data[6]);
        Assert.Equal((byte)'T', data[7]);
        File.Delete(tmp + ".tap");
    }

    [Fact]
    public void Build_GeneratesWavFile_WithRiffHeader()
    {
        var bin = new byte[8];
        var tmp = Path.Combine(Path.GetTempPath(), "x1n_wav_test");
        var rc = new TapeImageBuilder().Build(
            bin, MakeCfg(), tmp, emitWav: true, verbose: false);
        Assert.Equal(0, rc);
        Assert.True(File.Exists(tmp + ".wav"));
        var data = File.ReadAllBytes(tmp + ".wav");
        // RIFF...WAVE header
        Assert.Equal((byte)'R', data[0]);
        Assert.Equal((byte)'I', data[1]);
        Assert.Equal((byte)'F', data[2]);
        Assert.Equal((byte)'F', data[3]);
        Assert.Equal((byte)'W', data[8]);
        Assert.Equal((byte)'A', data[9]);
        Assert.Equal((byte)'V', data[10]);
        Assert.Equal((byte)'E', data[11]);
        File.Delete(tmp + ".wav");
        if (File.Exists(tmp + ".tap")) File.Delete(tmp + ".tap");
    }

    // === --tape-add spec parse (= file[@load[:exec]]) ===

    [Fact]
    public void ParseTapeAddSpec_FileOnly_LoadExecZero()
    {
        Assert.True(Driver.ParseTapeAddSpec("BGM.AKG", out var file, out var load, out var exec));
        Assert.Equal("BGM.AKG", file);
        Assert.Equal(0, load);          // load 省略 → 0
        Assert.Null(exec);              // exec 省略 → null (= 呼出側で load fallback)
    }

    [Fact]
    public void ParseTapeAddSpec_WithLoad_ExecNull()
    {
        Assert.True(Driver.ParseTapeAddSpec("DRV.bin@0x8000", out var file, out var load, out var exec));
        Assert.Equal("DRV.bin", file);
        Assert.Equal(0x8000, load);
        Assert.Null(exec);
    }

    [Fact]
    public void ParseTapeAddSpec_WithLoadExec()
    {
        Assert.True(Driver.ParseTapeAddSpec("DRV.bin@$4000:$4000", out var file, out var load, out var exec));
        Assert.Equal("DRV.bin", file);
        Assert.Equal(0x4000, load);
        Assert.Equal(0x4000, exec);
    }

    [Theory]
    [InlineData("DRV.bin@zzzz")]      // 不正 addr
    [InlineData("@0x8000")]            // file 名なし
    [InlineData("")]                   // 空
    public void ParseTapeAddSpec_Invalid_ReturnsFalse(string spec)
    {
        Assert.False(Driver.ParseTapeAddSpec(spec, out _, out _, out _));
    }

    // === --tape-add 多段 tape 生成 (= main + 追加 stage 連結) ===

    [Fact]
    public void Build_WithAdditionalStages_ConcatenatesAllStages()
    {
        var main = new byte[16];
        for (int i = 0; i < main.Length; i++) main[i] = (byte)i;
        var stage1 = new byte[] { 0xAA, 0xBB, 0xCC };
        var stage2 = new byte[] { 0x11, 0x22 };
        var stages = new List<(byte[] bin, TapeImageBuilder.ResolvedTapeConfig cfg)>
        {
            (stage1, MakeCfg(name: "STAGE1", load: 0x8000, exec: 0x8000)),
            (stage2, MakeCfg(name: "STAGE2", load: 0xB000, exec: 0xB000)),
        };
        var tmp = Path.Combine(Path.GetTempPath(), "x1n_tape_multistage");
        var rc = new TapeImageBuilder().Build(
            main, MakeCfg(name: "MAIN", load: 0x1000, exec: 0x1000),
            stages, tmp, emitWav: false, verbose: false);
        Assert.Equal(0, rc);
        Assert.True(File.Exists(tmp + ".tap"));
        // 多段連結なので 1 段だけの .tap より大きい (= stage 分の info+data block が増える)
        var multi = File.ReadAllBytes(tmp + ".tap");
        File.Delete(tmp + ".tap");

        var rcSingle = new TapeImageBuilder().Build(
            main, MakeCfg(name: "MAIN", load: 0x1000, exec: 0x1000),
            null, tmp, emitWav: false, verbose: false);
        Assert.Equal(0, rcSingle);
        var single = File.ReadAllBytes(tmp + ".tap");
        File.Delete(tmp + ".tap");

        Assert.True(multi.Length > single.Length,
            $"multi-stage tap ({multi.Length}) should be larger than single ({single.Length})");
    }
}
