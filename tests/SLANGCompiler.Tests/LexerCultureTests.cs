using System.Globalization;
using SLANGCompiler.Lexer;
using Xunit;

namespace SLANGCompiler.Tests;

/// <summary>小数の字句は、今の地域の書き方 (小数点が , の地域など) に左右されない (CLI とブラウザで同じ値)</summary>
public class LexerCultureTests
{
    [Theory]
    [InlineData("de-DE")]
    [InlineData("fr-FR")]
    [InlineData("ja-JP")]
    public void FloatLiteral_IsParsedWithInvariantCulture(string culture)
    {
        var old = CultureInfo.CurrentCulture;
        try
        {
            CultureInfo.CurrentCulture = new CultureInfo(culture);
            var tokens = new SLANGCompiler.Lexer.Lexer("A = 1.5;", "T.SL").Tokenize();
            var f = tokens.Single(t => t.Kind == TokenKind.FloatLiteral);
            Assert.Equal(1.5, f.FloatValue);
        }
        finally
        {
            CultureInfo.CurrentCulture = old;
        }
    }
}
