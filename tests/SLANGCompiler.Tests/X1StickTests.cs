using System.Text.RegularExpressions;
using SLANGCompiler.Runtime;
using Xunit;

namespace SLANGCompiler.Tests;

/// <summary>
/// X1 の `STICK(n)` (libx1_base.asm) の約束:
/// - 引数 1 個。H が 0 でない・L が 2 以上なら、I/O も DI/EI もせずに $00FF を返す
/// - 0・1 なら PSG のレジスタ 7 のそのポートを入力にして、レジスタ 14・15 を選んで ($1C00 に OUT) 読み ($1B00 から IN)、レジスタ 7 を戻し、H を 0 にして返す
/// - PSG を使う間だけ割り込みを止め、割り込みの状態は呼ぶ前のまま戻す (NMOS の Z80 の LD A,I の癖も見て、許可だったときだけ EI)
/// - X1 の全部の env の libraries に libx1_base が入っている
/// 実 Z80 の動きはここでは確かめられないので、注釈と命令の並びを押さえる。
/// </summary>
public class X1StickTests
{
    private static string RepoRoot()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir != null && !File.Exists(Path.Combine(dir.FullName, "runtime", "env", "x1.env")))
            dir = dir.Parent;
        Assert.NotNull(dir);
        return dir!.FullName;
    }

    private static RuntimeFunction LoadStick()
    {
        var path = Path.Combine(RepoRoot(), "runtime", "libx1_base.asm");
        var functions = RuntimeParser.Parse(File.ReadAllText(path), path);
        var stick = functions.SingleOrDefault(f => f.Name == "STICK");
        Assert.True(stick != null, "libx1_base.asm に @name STICK が 1 個だけあるべき");
        return stick!;
    }

    /// <summary>コメント行と空行を除いた命令の行 (ラベルの行も残す)</summary>
    private static string[] CodeLines(RuntimeFunction f)
        => f.Code.Split('\n')
            .Select(l => l.Split(';')[0].Trim())
            .Where(l => l.Length > 0)
            .ToArray();

    [Fact]
    public void Stick_HasExpectedMetadata()
    {
        var stick = LoadStick();
        Assert.Equal(1, stick.ParamCount);
        Assert.Equal(RuntimeResidency.Shared, stick.Residency);
    }

    [Fact]
    public void Stick_RejectsOutOfRangeBeforeAnyIo()
    {
        var lines = CodeLines(LoadStick());
        Assert.Matches(@"^LD\s+A\s*,\s*H$", lines[0]);
        Assert.Matches(@"^OR\s+A$", lines[1]);
        Assert.Matches(@"^JR\s+NZ\s*,\s*\.STICK_INVALID$", lines[2]);
        Assert.Matches(@"^LD\s+A\s*,\s*L$", lines[3]);
        Assert.Matches(@"^CP\s+2$", lines[4]);
        Assert.Matches(@"^JR\s+NC\s*,\s*\.STICK_INVALID$", lines[5]);

        // 範囲外の道は $00FF を返すだけ (I/O も割り込みの操作もしない)
        int invalid = Array.IndexOf(lines, ".STICK_INVALID");
        Assert.True(invalid > 0, ".STICK_INVALID のラベルがない");
        Assert.Equal(new[] { "LD HL,$00FF", "RET" }, lines[(invalid + 1)..]);
    }

    [Fact]
    public void Stick_ReadsPsgPortAndRestoresInterruptState()
    {
        // 範囲の検査の後から .STICK_INVALID の前までは、この並びのとおり:
        // - 割り込みの状態: スタックのすぐ下に 0 を置いて LD A,I。P/V が 1 なら許可。0 なら、置いた所が
        //   割り込みの戻り先で上書きされたか (NMOS の Z80 の LD A,I) を見て、上書きされていれば許可。D に 1 (許可)・0 (止まっていた)
        // - DI してから、レジスタ 7 を読んで E に覚え、そのポートのビット (A は 6、B は 7) を 0 (入力) にして書く
        // - レジスタ (14 + 番号) を $1C00 で選んで、すぐ $1Bxx から読む
        // - レジスタ 7 を E に戻し、D が 0 ならそのまま戻り、1 なら EI して戻る
        var lines = CodeLines(LoadStick());
        int invalid = Array.IndexOf(lines, ".STICK_INVALID");
        Assert.Equal(new[]
        {
            "XOR A",
            "PUSH AF",
            "POP AF",
            "LD A,I",
            "JP PE,.STICK_ON",
            "DEC SP",
            "DEC SP",
            "POP AF",
            "OR A",
            "JR NZ,.STICK_ON",
            "LD D,A",
            "JR .STICK_READ",
            ".STICK_ON",
            "LD D,1",
            ".STICK_READ",
            "DI",
            "LD BC,$1C00",
            "LD A,7",
            "OUT (C),A",
            "LD B,$1B",
            "IN E,(C)",
            "LD A,L",
            "OR A",
            "LD A,$BF",
            "JR Z,.STICK_MASK",
            "LD A,$7F",
            ".STICK_MASK",
            "AND E",
            "OUT (C),A",
            "LD B,$1C",
            "LD A,L",
            "ADD A,$0E",
            "OUT (C),A",
            "LD B,$1B",
            "IN L,(C)",
            "LD B,$1C",
            "LD A,7",
            "OUT (C),A",
            "LD B,$1B",
            "OUT (C),E",
            "LD H,0",
            "LD A,D",
            "OR A",
            "RET Z",
            "EI",
            "RET",
        }, lines[6..invalid]);
        // DI・EI はこの 1 回ずつだけ (範囲外の道では割り込みにも PSG にも触らない)
        Assert.Single(lines, "DI");
        Assert.Single(lines, "EI");
    }

    [Theory]
    [InlineData("x1")]
    [InlineData("lsx")]
    [InlineData("sosx1")]
    [InlineData("cpm")]
    [InlineData("x1native")]
    [InlineData("x1native_slfs")]
    [InlineData("x1native_hufs")]
    public void Stick_IsAvailableInAllX1Envs(string env)
    {
        var config = EnvironmentLoader.Load(Path.Combine(RepoRoot(), "runtime", "env", env + ".env"));
        Assert.Contains(config.Libraries, l => Path.GetFileNameWithoutExtension(l) == "libx1_base");
    }
}
