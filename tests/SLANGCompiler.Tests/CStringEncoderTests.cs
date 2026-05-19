using SLANGCompiler.CodeGen.C;
using Xunit;

namespace SLANGCompiler.Tests;

public class CStringEncoderTests
{
    [Theory]
    [InlineData("HELLO", "\"HELLO\"")]
    [InlineData("", "\"\"")]
    [InlineData("a b c", "\"a b c\"")]
    public void Encode_AsciiPrintable_PassesThrough(string input, string expected)
    {
        Assert.Equal(expected, CStringEncoder.Encode(input));
    }

    [Fact]
    public void Encode_Quote_Escaped()
    {
        Assert.Equal("\"say \\\"hi\\\"\"", CStringEncoder.Encode("say \"hi\""));
    }

    [Fact]
    public void Encode_Backslash_Escaped()
    {
        Assert.Equal("\"a\\\\b\"", CStringEncoder.Encode("a\\b"));
    }

    [Fact]
    public void Encode_Newline_Escaped()
    {
        Assert.Equal("\"a\\nb\"", CStringEncoder.Encode("a\nb"));
    }

    [Fact]
    public void Encode_HighByte_UsesHexEscape()
    {
        Assert.Equal("\"\\xff\"", CStringEncoder.Encode("\xff"));
    }

    [Fact]
    public void Encode_LowControl_UsesHexEscape()
    {
        // 0x01 は C escape 対応無し → \x01
        Assert.Equal("\"\\x01\"", CStringEncoder.Encode("\x01"));
    }
}
