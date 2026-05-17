using SLANGCompiler.CodeGen.C;
using Xunit;

namespace SLANGCompiler.Tests;

/// <summary>
/// <see cref="IdentifierMap"/> の単体テスト。
/// </summary>
public class IdentifierMapTests
{
    [Theory]
    [InlineData("foo", "foo")]
    [InlineData("FooBar123", "FooBar123")]
    [InlineData("_under", "_under")]
    [InlineData("KBT2", "KBT2")]
    public void Sanitize_AlreadySafe_PassesThrough(string name, string expected)
    {
        Assert.Equal(expected, IdentifierMap.Sanitize(name));
    }

    [Fact]
    public void Sanitize_At_BecomesAtToken()
    {
        Assert.Equal("foo_AT_bar", IdentifierMap.Sanitize("foo@bar"));
    }

    [Fact]
    public void Sanitize_Caret_BecomesCaretToken()
    {
        // SLANG では `^A` のような名前が出る。C では `.` 不可なので _CARET_ に。
        Assert.Equal("_CARET_A", IdentifierMap.Sanitize("^A"));
    }

    [Fact]
    public void Sanitize_LeadingDigit_GetsUnderscorePrefix()
    {
        Assert.Equal("_2NDPLACE", IdentifierMap.Sanitize("2NDPLACE"));
    }

    [Fact]
    public void Sanitize_UnknownChar_BecomesUnicodeEscape()
    {
        // 全角 'あ' = U+3042
        Assert.Equal("x_U3042_y", IdentifierMap.Sanitize("xあy"));
    }

    [Fact]
    public void Sanitize_SlangPrefix_GetsUsrEscape()
    {
        // 既存 runtime と衝突防止
        Assert.Equal("usr_slang_print_str", IdentifierMap.Sanitize("slang_print_str"));
        Assert.Equal("usr_slang_foo", IdentifierMap.Sanitize("slang_foo"));
    }

    [Fact]
    public void Sanitize_SlangPrefix_CaseInsensitive()
    {
        Assert.Equal("usr_SLANG_X", IdentifierMap.Sanitize("SLANG_X"));
        Assert.Equal("usr_Slang_foo", IdentifierMap.Sanitize("Slang_foo"));
    }

    [Fact]
    public void Sanitize_NotSlangPrefix_NoEscape()
    {
        // "slangy" は slang_ ではない (slang + y、_ なし)
        Assert.Equal("slangy", IdentifierMap.Sanitize("slangy"));
    }

    [Fact]
    public void Sanitize_EmptyString_ReturnsSentinel()
    {
        Assert.Equal("_empty_", IdentifierMap.Sanitize(""));
    }
}
