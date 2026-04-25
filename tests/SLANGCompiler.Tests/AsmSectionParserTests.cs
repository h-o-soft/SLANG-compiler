using Xunit;
using SLANGCompiler.Build;

namespace SLANGCompiler.Tests;

/// <summary>
/// PR-B2: ASM ファイルの固定セクション内のラベル抽出を検証。
/// </summary>
public class AsmSectionParserTests
{
    private static readonly string[] ExportsHeaders =
        { "; === Exported User Functions ===" };
    private static readonly string[] ImportsHeaders =
        { "; === User Function References ===" };

    [Fact]
    public void ExtractFuncNames_TargetSectionOnly()
    {
        var asm = @"
; === Some Other Section ===
; FUNC IGNORED1
; FUNC IGNORED2

; === Exported User Functions ===
; FUNC MAIN
; FUNC HELPER

; === Some Trailing Section ===
; FUNC IGNORED3
";
        var names = AsmSectionParser.ExtractFuncNames(asm, ExportsHeaders);
        Assert.Equal(new[] { "MAIN", "HELPER" }, names);
    }

    [Fact]
    public void ExtractExternNames_TargetSectionOnly()
    {
        var asm = @"
; === Some Other Section ===
; EXTERN IGNORED

; === User Function References ===
; EXTERN MYSUB    ; defined in overlay 0
; EXTERN HELPER   ; defined in main

; === Tail ===
; EXTERN ALSO_IGNORED
";
        var names = AsmSectionParser.ExtractExternNames(asm, ImportsHeaders);
        Assert.Equal(new[] { "MYSUB", "HELPER" }, names);
    }

    [Fact]
    public void Deduplicates_PreservesFirstOrder()
    {
        var asm = @"
; === Exported User Functions ===
; FUNC A
; FUNC B
; FUNC A
";
        var names = AsmSectionParser.ExtractFuncNames(asm, ExportsHeaders);
        Assert.Equal(new[] { "A", "B" }, names);
    }

    [Fact]
    public void NoMatchingSection_ReturnsEmpty()
    {
        var asm = @"
; === Wrong Header ===
; FUNC FOO
";
        var names = AsmSectionParser.ExtractFuncNames(asm, ExportsHeaders);
        Assert.Empty(names);
    }
}
