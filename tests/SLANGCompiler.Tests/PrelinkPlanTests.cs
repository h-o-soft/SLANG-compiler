using Xunit;
using SLANGCompiler.Build;

namespace SLANGCompiler.Tests;

/// <summary>
/// PR-B2 PrelinkPlan の検証 (cross-ref 集計、IsTrivial 判定、imports 生成)。
/// </summary>
public class PrelinkPlanTests : IDisposable
{
    private readonly string _tempDir;

    public PrelinkPlanTests()
    {
        _tempDir = Path.Combine(Path.GetTempPath(), $"slang_pp_{Guid.NewGuid():N}");
        Directory.CreateDirectory(_tempDir);
    }

    public void Dispose()
    {
        try { Directory.Delete(_tempDir, true); } catch { }
    }

    private string WriteAsm(string fileName, string content)
    {
        var path = Path.Combine(_tempDir, fileName);
        File.WriteAllText(path, content);
        return path;
    }

    [Fact]
    public void Build_NoCrossRef_IsTrivial()
    {
        // overlay の Imports セクションが無い (or User Function References が空)
        var mainAsm = WriteAsm("main.ASM", @"
; === Exported User Functions ===
; FUNC MAIN

; === Shared Symbols (from main) ===
; EXTERN _V_X
");
        var plan = PrelinkPlan.Build(new[] { ("main", mainAsm) });
        Assert.True(plan.IsTrivial);
        Assert.Single(plan.Targets);
        Assert.Empty(plan.Targets[0].UserFunctionImports);
        Assert.Single(plan.Targets[0].SharedImports);
    }

    [Fact]
    public void Build_WithUserFunctionImports_NotTrivial()
    {
        var mainAsm = WriteAsm("main.ASM", @"
; === Exported User Functions ===
; FUNC MAIN

; === User Function References ===
; EXTERN MYSUB    ; defined in overlay 0
");
        var overlayAsm = WriteAsm("main._m0.ASM", @"
; === Exported User Functions ===
; FUNC MYSUB
");
        var plan = PrelinkPlan.Build(new[]
        {
            ("main", mainAsm),
            ("overlay 0", overlayAsm),
        });
        Assert.False(plan.IsTrivial);
        Assert.Equal(2, plan.Targets.Count);
        Assert.Contains("MYSUB", plan.Targets[0].UserFunctionImports);
    }

    [Fact]
    public void WriteDummyImports_AllExternsAt0000()
    {
        var target = new PrelinkPlan.TargetInfo
        {
            Label = "test",
            AsmPath = Path.Combine(_tempDir, "test.ASM"),
            UserFunctionImports = new List<string> { "MYSUB", "HELPER" },
            SharedImports = new List<string> { "MPRNT", "_V_X" },
        };
        var dummyPath = Path.Combine(_tempDir, "test.dummy.imports.asm");
        PrelinkPlan.WriteDummyImports(target, dummyPath);

        var content = File.ReadAllText(dummyPath);
        Assert.Contains("MYSUB equ $0000", content);
        Assert.Contains("HELPER equ $0000", content);
        Assert.Contains("MPRNT equ $0000", content);
        Assert.Contains("_V_X equ $0000", content);
    }

    [Fact]
    public void WriteRealImports_ResolvesFromExportTableAndMainSym()
    {
        var target = new PrelinkPlan.TargetInfo
        {
            Label = "main",
            AsmPath = Path.Combine(_tempDir, "main.ASM"),
            UserFunctionImports = new List<string> { "MYSUB" },
            SharedImports = new List<string> { "MPRNT" },
        };
        var exported = new ExportedFunctionTable();
        exported.Add("overlay 0", new[] { "MYSUB" },
            new Dictionary<string, int> { ["MYSUB"] = 0x3000 });

        var mainPass1Sym = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase)
        {
            ["MPRNT"] = 0x0150,
        };

        var realPath = Path.Combine(_tempDir, "real.imports.asm");
        var (_, unresolved) = PrelinkPlan.WriteRealImports(target, exported, mainPass1Sym, realPath);

        Assert.Empty(unresolved);
        var content = File.ReadAllText(realPath);
        Assert.Contains("MYSUB equ $3000", content);
        Assert.Contains("MPRNT equ $0150", content);
    }

    [Fact]
    public void WriteRealImports_UnresolvedNamesReported()
    {
        var target = new PrelinkPlan.TargetInfo
        {
            Label = "main",
            AsmPath = Path.Combine(_tempDir, "main.ASM"),
            UserFunctionImports = new List<string> { "MISSING_FUNC" },
            SharedImports = new List<string> { "MISSING_SYM" },
        };
        var exported = new ExportedFunctionTable();
        var realPath = Path.Combine(_tempDir, "real.imports.asm");
        var (_, unresolved) = PrelinkPlan.WriteRealImports(target, exported,
            new Dictionary<string, int>(), realPath);

        Assert.Contains("MISSING_FUNC", unresolved);
        Assert.Contains("MISSING_SYM", unresolved);
    }
}
