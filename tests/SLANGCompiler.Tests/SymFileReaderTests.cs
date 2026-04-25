using Xunit;
using SLANGCompiler.Runtime;

namespace SLANGCompiler.Tests;

/// <summary>
/// AILZ80ASM .sym ファイルパーサ (PR-B 二段アセンブル toolchain で main.sym を
/// 読むのに使う) の挙動を検証。
/// </summary>
public class SymFileReaderTests
{
    [Fact]
    public void ParsesMinimalEquFormat()
    {
        // -sm minimal-equ で出力される形式
        var text = @";*** AILZ80ASM *** Z-80 Assembler, version 1.0.31.0, SYM:Minimal_Equ
MPRNT equ $1234
P10 equ $5678
_V_MAIN_VAL equ $0207
";
        var dict = SymFileReader.Parse(text);
        Assert.Equal(0x1234, dict["MPRNT"]);
        Assert.Equal(0x5678, dict["P10"]);
        Assert.Equal(0x0207, dict["_V_MAIN_VAL"]);
    }

    [Fact]
    public void ParsesNormalFormat()
    {
        // -sm normal で出力される形式
        var text = @";*** AILZ80ASM *** Z-80 Assembler, version 1.0.31.0, SYM:Normal
1234 MPRNT
5678 P10
";
        var dict = SymFileReader.Parse(text);
        Assert.Equal(0x1234, dict["MPRNT"]);
        Assert.Equal(0x5678, dict["P10"]);
    }

    [Fact]
    public void IgnoresCommentsAndBlankLines()
    {
        var text = @"
; this is a comment
;*** AILZ80ASM *** ...

MPRNT equ $1234

; another comment
P10 equ $5678

";
        var dict = SymFileReader.Parse(text);
        Assert.Equal(2, dict.Count);
        Assert.Equal(0x1234, dict["MPRNT"]);
        Assert.Equal(0x5678, dict["P10"]);
    }

    [Fact]
    public void IsCaseInsensitiveLookup()
    {
        var text = "MPRNT equ $1234\n";
        var dict = SymFileReader.Parse(text);
        Assert.Equal(0x1234, dict["mprnt"]);
        Assert.Equal(0x1234, dict["Mprnt"]);
    }

    [Fact]
    public void HandlesLabelsWithDots()
    {
        // ローカルラベル (.div81 等) は AILZ80ASM 内では完全名 (例: DIVHLDE8.div81) で出る
        var text = @"DIVHLDE8 equ $0100
DIVHLDE8.div81 equ $0103
";
        var dict = SymFileReader.Parse(text);
        Assert.Equal(0x0100, dict["DIVHLDE8"]);
        Assert.Equal(0x0103, dict["DIVHLDE8.div81"]);
    }
}
