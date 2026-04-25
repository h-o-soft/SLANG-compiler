using Xunit;
using SLANGCompiler.Build;

namespace SLANGCompiler.Tests;

/// <summary>
/// PR-B2 ExportedFunctionTable の検証。
/// 全 sym union ではなく、各 target が export 宣言した関数だけを採用する
/// (= runtime 関数等の同名重複が衝突しない) ことを確認。
/// </summary>
public class ExportedFunctionTableTests
{
    [Fact]
    public void Add_ExportedNamesOnlyAreIncluded()
    {
        var table = new ExportedFunctionTable();
        // main は MAIN と HELPER を export 宣言。pass1.sym には他にも runtime 関数の
        // ラベルがあるが、export set に無いので無視される。
        var pass1 = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase)
        {
            ["MAIN"]    = 0x012C,
            ["HELPER"]  = 0x0150,
            ["MPRNT"]   = 0x0200,  // runtime 関数 = export 対象外
            ["WORK10"]  = 0x0300,  // local label = export 対象外
        };
        table.Add("main", new[] { "MAIN", "HELPER" }, pass1);

        Assert.Equal(0x012C, table.Resolve("MAIN"));
        Assert.Equal(0x0150, table.Resolve("HELPER"));
        Assert.Null(table.Resolve("MPRNT"));   // export されていないので無視
        Assert.Null(table.Resolve("WORK10"));
    }

    [Fact]
    public void Add_DifferentTargetsWithSameRuntimeLabel_NoConflict()
    {
        // runtime 関数 (例: MPRNT) は main と overlay 両方の pass1.sym に出るが、
        // どちらの target も export 宣言していないので衝突しない。
        var table = new ExportedFunctionTable();
        var mainPass1 = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase)
        {
            ["MAIN"]  = 0x012C,
            ["MPRNT"] = 0x0200,
        };
        var overlayPass1 = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase)
        {
            ["MYSUB"] = 0x3000,
            ["MPRNT"] = 0x3100,  // overlay 側にも MPRNT が複製されている (異アドレス)
        };
        table.Add("main", new[] { "MAIN" }, mainPass1);
        table.Add("overlay 0", new[] { "MYSUB" }, overlayPass1); // MPRNT は export 宣言なし

        Assert.Equal(0x012C, table.Resolve("MAIN"));
        Assert.Equal(0x3000, table.Resolve("MYSUB"));
        Assert.Null(table.Resolve("MPRNT")); // export されていないので衝突しない
    }

    [Fact]
    public void Add_DuplicateExportWithDifferentAddress_Throws()
    {
        // 異 target に同名 export がある (= SLANG semantic 違反、本来あり得ない)。
        // driver 側で防御的にエラー化することを確認。
        var table = new ExportedFunctionTable();
        var mainPass1 = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase)
        {
            ["DUP"] = 0x0100,
        };
        var overlayPass1 = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase)
        {
            ["DUP"] = 0x3000,
        };
        table.Add("main", new[] { "DUP" }, mainPass1);

        var ex = Assert.Throws<InvalidOperationException>(() =>
            table.Add("overlay 0", new[] { "DUP" }, overlayPass1));
        Assert.Contains("DUP", ex.Message);
        Assert.Contains("duplicate", ex.Message);
    }

    [Fact]
    public void Add_MissingPass1Symbol_SilentlySkips()
    {
        // export 宣言があるが pass1.sym に対応ラベル無し (= compiler バグ系)
        // → スキップ (driver 上位の警告に任せる)
        var table = new ExportedFunctionTable();
        var pass1 = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase)
        {
            ["FOO"] = 0x0100,
        };
        table.Add("main", new[] { "FOO", "MISSING" }, pass1);

        Assert.Equal(0x0100, table.Resolve("FOO"));
        Assert.Null(table.Resolve("MISSING"));
    }
}
