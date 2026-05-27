using SLANGCompiler.Build.TapeFormats;
using Xunit;

namespace SLANGCompiler.Tests;

/// <summary>
/// vendored tape format library の最低限の安全網。 上流追従なし (= SLANG 一部
/// として保守) のため、 byte-exact roundtrip / WAV RIFF header 形式の regression
/// を検出可能にする。
/// </summary>
public class TapConvCoreRoundtripTests
{
    [Fact]
    public void TapFile_SaveLoad_BitExact()
    {
        var tmp = Path.Combine(Path.GetTempPath(), "tapconv_roundtrip.tap");
        try
        {
            // dummy 程度の sample stream を含む TapFile を construct
            var bin = new byte[20];
            for (int i = 0; i < bin.Length; i++) bin[i] = (byte)(i * 3 + 7);
            var program = new X1Program
            {
                Data = bin,
                Info = new X1InfoBlock
                {
                    BootFlag = 0x01,
                    FileName = "ROUNDTRIP".PadRight(13),
                    Extension = "BIN",
                    Password = 0x20,
                    DataSize = (ushort)bin.Length,
                    LoadAddress = 0x1234,
                    ExecuteAddress = 0x5678,
                },
            };
            var tap = program.Encode(sampleRate: 8000, tapeName: "RT");
            tap.Save(tmp);
            // load back → byte sequence 一致
            var loaded = TapFile.Load(tmp);
            Assert.Equal(tap.Header.SampleRate, loaded.Header.SampleRate);
            Assert.Equal(tap.Header.DataSizeBits, loaded.Header.DataSizeBits);
            Assert.Equal(tap.Samples.Length, loaded.Samples.Length);
            for (int i = 0; i < tap.Samples.Length; i++)
                Assert.Equal(tap.Samples[i], loaded.Samples[i]);
        }
        finally
        {
            if (File.Exists(tmp)) File.Delete(tmp);
        }
    }

    [Fact]
    public void X1Program_EncodeDecode_Roundtrip()
    {
        var bin = new byte[64];
        for (int i = 0; i < bin.Length; i++) bin[i] = (byte)(i ^ 0x5A);
        var program = new X1Program
        {
            Data = bin,
            Info = new X1InfoBlock
            {
                BootFlag = 0x01,
                FileName = "TEST".PadRight(13),
                Extension = "BIN",
                Password = 0x20,
                DataSize = (ushort)bin.Length,
                LoadAddress = 0x9000,
                ExecuteAddress = 0x9000,
            },
        };
        // encode → decode で同じ byte 列が復元される
        var tap = program.Encode(sampleRate: 8000, tapeName: "TEST");
        var decoded = X1Program.Decode(tap);
        Assert.Equal(bin.Length, decoded.Data.Length);
        for (int i = 0; i < bin.Length; i++)
            Assert.Equal(bin[i], decoded.Data[i]);
        Assert.Equal(0x9000, decoded.Info.LoadAddress);
        Assert.Equal(0x9000, decoded.Info.ExecuteAddress);
    }

    [Fact]
    public void X1Program_ConcatenatePrograms_Roundtrip()
    {
        // 多段 tape の bit-exact roundtrip: 2 段 encode → DecodeAll で各段独立復元
        var bin1 = new byte[8];
        for (int i = 0; i < bin1.Length; i++) bin1[i] = (byte)(i + 10);
        var bin2 = new byte[12];
        for (int i = 0; i < bin2.Length; i++) bin2[i] = (byte)(i ^ 0x33);

        var prog1 = new X1Program
        {
            Data = bin1,
            Info = new X1InfoBlock
            {
                BootFlag = 0x01,
                FileName = "STAGE1".PadRight(13),
                Extension = "BIN",
                Password = 0x20,
                DataSize = (ushort)bin1.Length,
                LoadAddress = 0x1000,
                ExecuteAddress = 0x1000,
            },
        };
        var prog2 = new X1Program
        {
            Data = bin2,
            Info = new X1InfoBlock
            {
                BootFlag = 0x01,
                FileName = "M0".PadRight(13),
                Extension = "BIN",
                Password = 0x20,
                DataSize = (ushort)bin2.Length,
                LoadAddress = 0x4000,
                ExecuteAddress = 0x4000,
            },
        };

        var tap = X1Program.ConcatenatePrograms(
            new List<X1Program> { prog1, prog2 }, sampleRate: 8000, tapeName: "MULTI");
        var decoded = X1Program.DecodeAll(tap);

        Assert.Equal(2, decoded.Count);
        Assert.Equal(bin1.Length, decoded[0].Data.Length);
        Assert.Equal(bin2.Length, decoded[1].Data.Length);
        for (int i = 0; i < bin1.Length; i++)
            Assert.Equal(bin1[i], decoded[0].Data[i]);
        for (int i = 0; i < bin2.Length; i++)
            Assert.Equal(bin2[i], decoded[1].Data[i]);
        Assert.Equal(0x1000, decoded[0].Info.LoadAddress);
        Assert.Equal(0x4000, decoded[1].Info.LoadAddress);
    }

    [Fact]
    public void X1Program_ConcatenatePrograms_StandardSyncOrder()
    {
        // 各段で標準 sync 仕様 (= info 40/41 leader 8000、 data 20/21 leader 4000) 維持
        // (= Codex 指摘反映、 短い inter-stage gap で sync 見失い回避)。
        // bit-stream を再 demodulate して 各段 sync が見つかる確認。
        var prog = new X1Program
        {
            Data = new byte[16],
            Info = new X1InfoBlock
            {
                BootFlag = 0x01,
                FileName = "T".PadRight(13),
                Extension = "BIN",
                Password = 0x20,
                DataSize = 16,
                LoadAddress = 0x2000,
                ExecuteAddress = 0x2000,
            },
        };
        var tap = X1Program.ConcatenatePrograms(
            new List<X1Program> { prog, prog, prog }, sampleRate: 8000, tapeName: "T");
        // 3 段全部 decode 成功 = 各段 sync が標準 仕様で取れてる
        var decoded = X1Program.DecodeAll(tap);
        Assert.Equal(3, decoded.Count);
    }

    [Fact]
    public void WavWriter_RiffHeader_Valid()
    {
        var bin = new byte[10];
        var tmp = Path.Combine(Path.GetTempPath(), "tapconv_riff.wav");
        try
        {
            var program = new X1Program
            {
                Data = bin,
                Info = new X1InfoBlock
                {
                    BootFlag = 0x01,
                    FileName = "WAVTEST".PadRight(13),
                    Extension = "BIN",
                    Password = 0x20,
                    DataSize = (ushort)bin.Length,
                    LoadAddress = 0x1000,
                    ExecuteAddress = 0x1000,
                },
            };
            var tap = program.Encode(sampleRate: 48000, tapeName: "WT");
            WavWriter.WriteFromTap(tmp, tap, targetSampleRate: 48000, bitsPerSample: 8);
            var data = File.ReadAllBytes(tmp);
            // RIFF<size>WAVE で始まる
            Assert.Equal((byte)'R', data[0]);
            Assert.Equal((byte)'I', data[1]);
            Assert.Equal((byte)'F', data[2]);
            Assert.Equal((byte)'F', data[3]);
            Assert.Equal((byte)'W', data[8]);
            Assert.Equal((byte)'A', data[9]);
            Assert.Equal((byte)'V', data[10]);
            Assert.Equal((byte)'E', data[11]);
            // fmt sub-chunk が続く (= "fmt ")
            Assert.Equal((byte)'f', data[12]);
            Assert.Equal((byte)'m', data[13]);
            Assert.Equal((byte)'t', data[14]);
            Assert.Equal((byte)' ', data[15]);
        }
        finally
        {
            if (File.Exists(tmp)) File.Delete(tmp);
        }
    }
}
