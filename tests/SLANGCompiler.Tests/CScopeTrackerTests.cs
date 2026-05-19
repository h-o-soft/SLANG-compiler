using SLANGCompiler.CodeGen.C;
using SLANGCompiler.Semantics;
using Xunit;

namespace SLANGCompiler.Tests;

/// <summary>
/// <see cref="CScopeTracker"/> の単体テスト。
/// 関数 enter/leave で local 型が見える/消える挙動と、global 委譲を中心に検証。
/// </summary>
public class CScopeTrackerTests
{
    [Fact]
    public void NoFunction_ResolvesGlobalsOnly()
    {
        var globals = new SymbolTable();
        globals.Define("G", SymbolKind.Variable, SlangType.Word);

        var tracker = new CScopeTracker(globals);
        Assert.Equal(SlangType.Word, tracker.Resolve("G"));
        Assert.Null(tracker.Resolve("nope"));
        Assert.False(tracker.IsLocal("G"));
    }

    [Fact]
    public void EnterFunction_LocalShadowsGlobal()
    {
        var globals = new SymbolTable();
        globals.Define("X", SymbolKind.Variable, SlangType.Word);

        var tracker = new CScopeTracker(globals);
        tracker.EnterFunction();
        tracker.DeclareLocal("X", SlangType.Byte);

        Assert.Equal(SlangType.Byte, tracker.Resolve("X"));
        Assert.True(tracker.IsLocal("X"));
    }

    [Fact]
    public void LeaveFunction_LocalDropped()
    {
        var globals = new SymbolTable();
        globals.Define("X", SymbolKind.Variable, SlangType.Word);

        var tracker = new CScopeTracker(globals);
        tracker.EnterFunction();
        tracker.DeclareLocal("X", SlangType.Byte);
        tracker.LeaveFunction();

        // local が消えて global X (Word) が見えるようになる
        Assert.Equal(SlangType.Word, tracker.Resolve("X"));
        Assert.False(tracker.IsLocal("X"));
    }

    [Fact]
    public void LeaveFunction_OutsideAnyScope_Throws()
    {
        var tracker = new CScopeTracker(null);
        Assert.Throws<InvalidOperationException>(() => tracker.LeaveFunction());
    }

    [Fact]
    public void DeclareLocal_OutsideFunction_Throws()
    {
        var tracker = new CScopeTracker(null);
        Assert.Throws<InvalidOperationException>(() => tracker.DeclareLocal("x", SlangType.Byte));
    }

    [Fact]
    public void NullGlobals_Resolves_Locals_Only()
    {
        var tracker = new CScopeTracker(null);
        tracker.EnterFunction();
        tracker.DeclareLocal("Y", SlangType.Float);

        Assert.Equal(SlangType.Float, tracker.Resolve("Y"));
        Assert.Null(tracker.Resolve("Z"));
    }

    [Fact]
    public void IsLocal_True_For_Local_Only()
    {
        var globals = new SymbolTable();
        globals.Define("G", SymbolKind.Variable, SlangType.Word);

        var tracker = new CScopeTracker(globals);
        tracker.EnterFunction();
        tracker.DeclareLocal("L", SlangType.Byte);

        Assert.True(tracker.IsLocal("L"));
        Assert.False(tracker.IsLocal("G"));
    }

    [Fact]
    public void Resolve_CaseInsensitive_Within_Local()
    {
        // SLANG は基本 case-insensitive (SymbolTable も同じ慣行)
        var tracker = new CScopeTracker(null);
        tracker.EnterFunction();
        tracker.DeclareLocal("X", SlangType.Word);

        Assert.Equal(SlangType.Word, tracker.Resolve("x"));
        Assert.Equal(SlangType.Word, tracker.Resolve("X"));
    }
}
