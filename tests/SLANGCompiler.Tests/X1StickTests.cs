using System.Text.RegularExpressions;
using SLANGCompiler.Runtime;
using Xunit;

namespace SLANGCompiler.Tests;

/// <summary>
/// X1 の `STICK(n)` (libx1_base.asm) の約束:
/// - 引数 1 個。H が 0 でない・L が 2 以上なら、I/O も DI/EI もせずに $00FF を返す
/// - 0・1 なら PSG のレジスタ 14・15 を選んで ($1C00 に OUT) 読み ($1B00 から IN)、H を 0 にして返す
/// - PSG を選んで読む間だけ割り込みを止め (DI は OUT の前)、許可して戻る (EI は RET の前)
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
    public void Stick_ReadsPsgPortWithInterruptsDisabledOnlyAroundAccess()
    {
        var lines = CodeLines(LoadStick());
        int di = Array.IndexOf(lines, "DI");
        int outPsg = Array.FindIndex(lines, l => Regex.IsMatch(l, @"^OUT\s*\(\s*C\s*\)\s*,\s*A$"));
        int inPsg = Array.FindIndex(lines, l => Regex.IsMatch(l, @"^IN\s+L\s*,\s*\(\s*C\s*\)$"));
        int ei = Array.IndexOf(lines, "EI");
        Assert.True(di >= 0 && outPsg > di && inPsg > outPsg && ei > inPsg, string.Join(" / ", lines));
        Assert.Equal("RET", lines[ei + 1]);
        // レジスタ 14 (= $0E + 番号) を $1C00 で選び、$1Bxx で読む
        Assert.Contains("ADD A,$0E", lines);
        Assert.Contains("LD BC,$1C00", lines);
        Assert.Contains("LD B,$1B", lines);
        Assert.Contains("LD H,0", lines);
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
