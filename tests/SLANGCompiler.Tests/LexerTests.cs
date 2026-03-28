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

    [Fact]
    public void Comments()
    {
        var tokens = Tokenize("X // comment\n Y /* block */ Z (* pascal *) W");
        Assert.Equal(4, tokens.Count(t => t.Kind == TokenKind.Identifier));
    }
}
