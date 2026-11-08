using Xunit;
using SLANGCompiler.Lexer;

namespace SLANGCompiler.Tests;

public class LexerTests
{
    private List<Token> Tokenize(string source) => new Lexer.Lexer(source).Tokenize();

    [Fact]
    public void SimpleDeclaration()
    {
        var tokens = Tokenize("VAR X, Y;");
        Assert.Equal(TokenKind.Var, tokens[0].Kind);
        Assert.Equal(TokenKind.Identifier, tokens[1].Kind);
        Assert.Equal("X", tokens[1].Text);
        Assert.Equal(TokenKind.Comma, tokens[2].Kind);
        Assert.Equal(TokenKind.Identifier, tokens[3].Kind);
        Assert.Equal(TokenKind.Semicolon, tokens[4].Kind);
    }

    [Fact]
    public void NumberFormats()
    {
        var tokens = Tokenize("123 $FF 0FFH 1010B 0x1A");
        Assert.Equal(123, tokens[0].IntValue);
        Assert.Equal(0xFF, tokens[1].IntValue);
        Assert.Equal(0xFF, tokens[2].IntValue);
        Assert.Equal(0b1010, tokens[3].IntValue);
        Assert.Equal(0x1A, tokens[4].IntValue);
    }

    [Fact]
    public void DotOperators()
    {
        var tokens = Tokenize("A .>=. B");
        Assert.Equal(TokenKind.Identifier, tokens[0].Kind);
        Assert.Equal(TokenKind.SignedGe, tokens[1].Kind);
        Assert.Equal(TokenKind.Identifier, tokens[2].Kind);
    }

    [Fact]
    public void DotOperatorNoDot()
    {
        // .>=-.51 の境界ケース → .>=. + - + 51
        var tokens = Tokenize("A.>=-.51");
        Assert.Equal(TokenKind.Identifier, tokens[0].Kind);
        Assert.Equal(TokenKind.SignedGe, tokens[1].Kind);
        Assert.Equal(TokenKind.Minus, tokens[2].Kind);
        Assert.Equal(TokenKind.IntegerLiteral, tokens[3].Kind);
        Assert.Equal(51, tokens[3].IntValue);
    }

    [Fact]
    public void Keywords()
    {
        var tokens = Tokenize("IF THEN ELSE WHILE FOR CASE AND OR MOD");
        Assert.Equal(TokenKind.If, tokens[0].Kind);
        Assert.Equal(TokenKind.Then, tokens[1].Kind);
        Assert.Equal(TokenKind.Else, tokens[2].Kind);
        Assert.Equal(TokenKind.While, tokens[3].Kind);
        Assert.Equal(TokenKind.For, tokens[4].Kind);
        Assert.Equal(TokenKind.Case, tokens[5].Kind);
        Assert.Equal(TokenKind.And, tokens[6].Kind);
        Assert.Equal(TokenKind.Or, tokens[7].Kind);
        Assert.Equal(TokenKind.Mod, tokens[8].Kind);
    }

    [Fact]
    public void StringLiteral()
    {
        var tokens = Tokenize("\"Hello\\n\"");
        Assert.Equal(TokenKind.StringLiteral, tokens[0].Kind);
        Assert.Equal("Hello\r", tokens[0].StringValue); // \n → $0D
    }

    [Theory]
    // 仕様書のエスケープ文字の表 (\N と \0 は大文字・数字で書かれている)
    [InlineData("\"A\\NB\"", "A\rB")]
    [InlineData("\"A\\nB\"", "A\rB")]
    [InlineData("\"A\\0B\"", "A\0B")]
    [InlineData("\"\\/\\C\\R\\L\\U\\D\"", "\r\f\x1c\x1d\x1e\x1f")]
    [InlineData("\"\\\\\\\"\\'\"", "\\\"'")]
    public void StringLiteral_EscapeTable(string source, string expected)
    {
        var tokens = Tokenize(source);
        Assert.Equal(TokenKind.StringLiteral, tokens[0].Kind);
        Assert.Equal(expected, tokens[0].StringValue);
    }

    [Theory]
    [InlineData("'\\N'", 0x0D)]
    [InlineData("'\\0'", 0x00)]
    public void CharLiteral_EscapeTable(string source, int expected)
    {
        var tokens = Tokenize(source);
        Assert.Equal(TokenKind.CharLiteral, tokens[0].Kind);
        Assert.Equal(expected, tokens[0].IntValue);
    }

    [Fact]
    public void ArrayBracketContext()
    {
        // 識別子の直後の [ は ArrayBracketOpen
        var tokens = Tokenize("ARR[3]");
        Assert.Equal(TokenKind.Identifier, tokens[0].Kind);
        Assert.Equal(TokenKind.ArrayBracketOpen, tokens[1].Kind);
        Assert.Equal(TokenKind.IntegerLiteral, tokens[2].Kind);
        Assert.Equal(TokenKind.RBracket, tokens[3].Kind);
    }

    [Theory]
    // ( と , の直後の [ は添字ではない (CODE([式], …) の [式])。名前・] の直後は今までどおり添字
    [InlineData("CODE([K], $00)", 2)]
    [InlineData("CODE(X,[K])", 4)]
    [InlineData("CODE($00,[K])", 4)]
    public void BracketAfterParenOrComma_IsNotSubscript(string source, int index)
    {
        var tokens = Tokenize(source);
        Assert.Equal(TokenKind.LBracket, tokens[index].Kind);
    }

    [Fact]
    public void BracketAfterNameInsideParen_IsSubscript()
    {
        var tokens = Tokenize("F(A[1], B[2])");
        Assert.Equal(TokenKind.ArrayBracketOpen, tokens[3].Kind);
        Assert.Equal(TokenKind.ArrayBracketOpen, tokens[8].Kind);
    }

    [Fact]
    public void Comments()
    {
        var tokens = Tokenize("X // comment\n Y /* block */ Z (* pascal *) W");
        Assert.Equal(4, tokens.Count(t => t.Kind == TokenKind.Identifier));
    }
}
