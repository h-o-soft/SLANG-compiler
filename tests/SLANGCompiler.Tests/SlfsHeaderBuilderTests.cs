using Xunit;
using SLANGCompiler.SlfsPack;

namespace SLANGCompiler.Tests;

public class SlfsHeaderBuilderTests
{
    [Fact]
    public void Build_EmptyAssets_ReturnsEmptyString()
    {
        var hdr = SlfsHeaderBuilder.Build(new List<SlfsPackerLibrary.AssetEntry>());
        Assert.Equal("", hdr);
    }

    [Fact]
    public void Build_Format_CONST_Syntax()
    {
        var assets = new List<SlfsPackerLibrary.AssetEntry>
        {
            new() { Name = "GREETING.TXT", Data = new byte[1] },
            new() { Name = "NUMBERS.BIN",  Data = new byte[1] },
        };
        var hdr = SlfsHeaderBuilder.Build(assets);
        Assert.Contains("CONST ", hdr);
        Assert.Contains("FILE_GREETING_TXT", hdr);
        Assert.Contains("FILE_NUMBERS_BIN", hdr);
        // GREETING.TX (normalized) < NUMBERS.BIN → ID 0/1
        Assert.Contains("= 0", hdr);
        Assert.Contains("= 1", hdr);
        Assert.EndsWith(";\n", hdr);
    }

    [Fact]
    public void ToIdentifier_BasicConversion()
    {
        Assert.Equal("FILE_GREETING_TXT", SlfsHeaderBuilder.ToIdentifier("GREETING.TXT"));
        Assert.Equal("FILE_NUMBERS_BIN",  SlfsHeaderBuilder.ToIdentifier("NUMBERS.BIN"));
        Assert.Equal("FILE_LV1_MAP",      SlfsHeaderBuilder.ToIdentifier("LV1.MAP"));
    }

    [Fact]
    public void ToIdentifier_SpecialChars_To_Underscore()
    {
        Assert.Equal("FILE_MY_ASSET_1_BIN", SlfsHeaderBuilder.ToIdentifier("MY ASSET-1.BIN"));
        Assert.Equal("FILE_A_B_C",          SlfsHeaderBuilder.ToIdentifier("a-b.c"));
        // 英数字以外は全部 _ に変換
        Assert.Equal("FILE_X_Y_Z",          SlfsHeaderBuilder.ToIdentifier("x@y#z"));
    }

    [Fact]
    public void ToIdentifier_LowercaseToUpper()
    {
        // ToUpperInvariant() で locale 非依存
        Assert.Equal("FILE_ABC", SlfsHeaderBuilder.ToIdentifier("abc"));
        Assert.Equal("FILE_ABC", SlfsHeaderBuilder.ToIdentifier("Abc"));
        Assert.Equal("FILE_ABC", SlfsHeaderBuilder.ToIdentifier("ABC"));
    }

    [Fact]
    public void Build_IdentifierCollision_Throws()
    {
        // `FILE-A.BIN` と `FILE_A.BIN` は両方 `FILE_FILE_A_BIN` に変換 = collision
        var assets = new List<SlfsPackerLibrary.AssetEntry>
        {
            new() { Name = "FILE-A.BIN", Data = new byte[1] },
            new() { Name = "FILE_A.BIN", Data = new byte[1] },
        };
        var ex = Assert.Throws<InvalidOperationException>(() => SlfsHeaderBuilder.Build(assets));
        Assert.Contains("identifier collision", ex.Message);
    }

    [Fact]
    public void Build_SortedByNormalizedFilename()
    {
        // packer の SortedAssets と同 sort で id 割当
        var assets = new List<SlfsPackerLibrary.AssetEntry>
        {
            new() { Name = "ZULU",  Data = new byte[1] },
            new() { Name = "ALPHA", Data = new byte[1] },
            new() { Name = "BRAVO", Data = new byte[1] },
        };
        var hdr = SlfsHeaderBuilder.Build(assets);
        // ALPHA = 0, BRAVO = 1, ZULU = 2 を期待
        int alphaIdx = hdr.IndexOf("FILE_ALPHA");
        int bravoIdx = hdr.IndexOf("FILE_BRAVO");
        int zuluIdx  = hdr.IndexOf("FILE_ZULU");
        Assert.True(alphaIdx < bravoIdx && bravoIdx < zuluIdx,
            $"sort 順違反: alpha={alphaIdx}, bravo={bravoIdx}, zulu={zuluIdx}");
    }
}
