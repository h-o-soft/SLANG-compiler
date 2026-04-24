using Xunit;
using SLANGCompiler.Build;

namespace SLANGCompiler.Tests;

/// <summary>
/// PR-B の核心 OverlayImportsBuilder の挙動を検証。対象セクション特定 +
/// EXTERN 抽出 + main.sym 交集合の出力確認。
/// </summary>
public class OverlayImportsBuilderTests
{
    [Fact]
    public void ExtractsExternsFromTargetSectionsOnly()
    {
        // 3 つの対象セクション全部から EXTERN を拾えること、それ以外は無視
        var asm = @"
; === Overlay Module 0 ===
    ORG $8000
SUB:
    CALL FOO
; some random comment with EXTERN BAR  ← これは無視されるべき
    RET

; === Shared Symbols (from main) ===
; EXTERN _V_X  ; address resolved at link time
; EXTERN _V_Y

; === String references (from main) ===
; EXTERN STR_HELLO

; === Shared Runtime References (resolved via two-stage assembly) ===
; EXTERN MPRNT  ; [lib].MPRNT
; EXTERN P10
";
        var names = OverlayImportsBuilder.ExtractExternNames(asm);
        Assert.Contains("_V_X", names);
        Assert.Contains("_V_Y", names);
        Assert.Contains("STR_HELLO", names);
        Assert.Contains("MPRNT", names);
        Assert.Contains("P10", names);
        Assert.DoesNotContain("BAR", names); // 対象セクション外
        Assert.DoesNotContain("FOO", names); // 通常コード行
    }

    [Fact]
    public void DeduplicatesExternNames()
    {
        // 同名が複数セクションに現れても重複しない (= main 実体は 1 個保証)
        var asm = @"
; === Shared Symbols (from main) ===
; EXTERN MPRNT

; === Shared Runtime References (resolved via two-stage assembly) ===
; EXTERN MPRNT
; EXTERN MPRNT
";
        var names = OverlayImportsBuilder.ExtractExternNames(asm);
        Assert.Single(names);
        Assert.Equal("MPRNT", names[0]);
    }

    [Fact]
    public void IgnoresExternInUnrecognizedSections()
    {
        // 既存の "; === Overlay 0 Private Work Area ===" 等は対象外。
        // 万が一そこに EXTERN 風文字列があっても拾わない。
        var asm = @"
; === Overlay 0 Private Work Area ===
; EXTERN HIDDEN_LABEL
__WORK_M0__:
_V_M0_X EQU (__WORK_M0__ + 0)
";
        var names = OverlayImportsBuilder.ExtractExternNames(asm);
        Assert.Empty(names);
    }

    [Fact]
    public void BuildWritesFilteredImports()
    {
        // 一時ファイル経由で end-to-end の Build メソッドを検証
        var tmpDir = Path.Combine(Path.GetTempPath(), $"slang_oib_{Guid.NewGuid():N}");
        Directory.CreateDirectory(tmpDir);
        try
        {
            var symPath = Path.Combine(tmpDir, "main.sym");
            File.WriteAllText(symPath, @";*** AILZ80ASM *** SYM:Minimal_Equ
MPRNT equ $1234
P10 equ $5678
UNUSED equ $9ABC
_V_MAIN_VAL equ $0207
");

            var asmPath = Path.Combine(tmpDir, "overlay.asm");
            File.WriteAllText(asmPath, @"
; === Overlay Module 0 ===
    ORG $8000
SUB:
    CALL MPRNT
    RET

; === Shared Symbols (from main) ===
; EXTERN _V_MAIN_VAL

; === Shared Runtime References (resolved via two-stage assembly) ===
; EXTERN MPRNT
; EXTERN MISSING_FUNC
");

            var outPath = Path.Combine(tmpDir, "overlay.imports.asm");
            var (resultPath, unresolved) = OverlayImportsBuilder.Build(symPath, asmPath, outPath);

            Assert.Equal(outPath, resultPath);
            Assert.Single(unresolved);
            Assert.Equal("MISSING_FUNC", unresolved[0]);

            var content = File.ReadAllText(outPath);
            Assert.Contains("MPRNT equ $1234", content);
            Assert.Contains("_V_MAIN_VAL equ $0207", content);
            Assert.DoesNotContain("UNUSED", content);          // overlay が呼んでないので含まれない
            Assert.DoesNotContain("MISSING_FUNC equ", content); // main.sym にないので含まれない
            Assert.DoesNotContain("P10", content);              // overlay が呼んでない
        }
        finally
        {
            try { Directory.Delete(tmpDir, true); } catch { }
        }
    }
}
