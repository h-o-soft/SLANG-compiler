using Xunit;
using SLANGCompiler.Runtime;

namespace SLANGCompiler.Tests;

/// <summary>
/// RuntimeManager.ApplyVariant (X__NOROM で X の中身を差し替える) の単体テスト。
/// 名前と出力の順は X のまま、コードと属性は X__NOROM のものになる
/// </summary>
public class RuntimeVariantTests
{
    private const string Lib = @"
; @name A
; @calls B
LD A,1

; @name B
; @calls OLDDEP
LD A,2
RET

; @name C
RET

; @name OLDDEP
RET

; @name NEWDEP
RET

; @name B__NOROM
; @calls NEWDEP
; @works TBL:32
; @works_align 32
; @resident local
LD A,3
RET
";

    [Fact]
    public void ApplyVariant_KeepsNameAndOrder()
    {
        var rm = new RuntimeManager();
        rm.LoadFromString(Lib);
        Assert.Equal(new[] { "B" }, rm.ApplyVariant("__NOROM"));
        rm.MarkUsed("A");
        var outFuncs = rm.GetOutputFunctions().ToList();
        // A はそのまま隣の B へ進む (フォールスルー)。B の後ろへ移らない
        Assert.Equal(new[] { "A", "B", "NEWDEP" }, outFuncs.Select(f => f.Name));
        var b = outFuncs[1];
        Assert.Contains("LD A,3", b.Code);
        Assert.Equal(new[] { "NEWDEP" }, b.Dependencies);
        Assert.Equal(32, b.WorksAlignment);
        Assert.Equal(new[] { ("TBL", 32) }, b.Works!);
        Assert.Equal(RuntimeResidency.Local, b.Residency);
    }

    [Fact]
    public void NoApply_Unchanged()
    {
        // 差し替えなければ、__NOROM の断片は使われない
        var rm = new RuntimeManager();
        rm.LoadFromString(Lib);
        rm.MarkUsed("A");
        Assert.Equal(new[] { "A", "B", "OLDDEP" }, rm.GetOutputFunctions().Select(f => f.Name));
        Assert.Contains("LD A,2", rm.GetOutputFunctions().ElementAt(1).Code);
    }

    [Fact]
    public void ApplyVariant_ReplacesRequires()
    {
        // @requires は差し替え先の値で置き換える (元の値と合わせない)
        var rm = new RuntimeManager();
        rm.LoadFromString("; @name S\n; @requires next\nRET\n\n; @name S__ZXS\nRET\n\n; @name T\nRET\n\n; @name T__ZXS\n; @requires ay, next\nRET\n");
        Assert.Equal(new[] { "next" }, rm.Functions["S"].Requires);
        rm.ApplyVariant("__ZXS");
        Assert.Empty(rm.Functions["S"].Requires);
        Assert.Equal(new[] { "ay", "next" }, rm.Functions["T"].Requires);
    }

    [Fact]
    public void ApplyVariant_Orphan_Errors()
    {
        // 元の名前の断片が無い X__NOROM はランタイムの書き誤り
        var rm = new RuntimeManager();
        rm.LoadFromString("; @name ZZ__NOROM\nRET\n");
        var ex = Assert.Throws<InvalidDataException>(() => rm.ApplyVariant("__NOROM"));
        Assert.Contains("ZZ__NOROM", ex.Message);
    }
}
