using Xunit;
using SLANGCompiler.Lexer;

namespace SLANGCompiler.Tests;

/// <summary>
/// プリプロセッサ: #IF の中の CONST の値 (右辺が式のもの)、#CHAIN、#IF の中の #INCLUDE の探し方
/// </summary>
public class PreprocessorTests : IDisposable
{
    private readonly string _tempDir;

    public PreprocessorTests()
    {
        _tempDir = Path.Combine(Path.GetTempPath(), $"slangtest_{Guid.NewGuid():N}");
        Directory.CreateDirectory(_tempDir);
    }

    public void Dispose()
    {
        if (Directory.Exists(_tempDir))
            Directory.Delete(_tempDir, true);
    }

    private string Write(string relPath, string text)
    {
        var path = Path.Combine(_tempDir, relPath);
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        File.WriteAllText(path, text);
        return path;
    }

    /// <summary>プリプロセッサだけを通し、残った識別子を並べる</summary>
    private (List<string> Names, DiagnosticBag Diag) Run(string source, string? baseDir = null)
    {
        var diag = new DiagnosticBag();
        var tokens = new Preprocessor(diag).Process(new Lexer.Lexer(source).Tokenize(), baseDir ?? _tempDir);
        return (tokens.Where(t => t.Kind == TokenKind.Identifier).Select(t => t.StringValue).ToList(), diag);
    }

    private static string IfMark(string cond) => $"\n#IF {cond}\nYES_{cond.Split(' ')[0]}\n#ELSE\nNO_{cond.Split(' ')[0]}\n#END\n";

    // ==== #IF の中の CONST (件 12) ====

    [Theory]
    // 右辺が式の CONST も値を覚える (いままでは最初の字句だけを見て A は 1、カンマの後の B は覚えなかった)
    [InlineData("CONST A = 1 + 1, B = 3;", "A == 2")]
    [InlineData("CONST A = 1 + 1, B = 3;", "B == 3")]
    [InlineData("CONST A = 2, B = A * 3;", "B == 6")]
    [InlineData("CONST A = (1, 2), B = 4;", "B == 4")]          // 括弧の中のカンマで区切らない
    [InlineData("CONST T = [1, 2], B = 5;", "B == 5")]          // CODE の並びの中のカンマで区切らない
    [InlineData("ARRAY BYTE X[1]; CONST T = [1 ? 0 : X[0], 2], B = 5;", "B == 5")]   // 並びの中の添字の [ ] も対にする
    [InlineData("CONST A = 2 + (1 << 16);", "A == 3")]      // 値の規則は CONST そのものと同じ (#259)
    [InlineData("CONST A = $10 + 2;", "A == 18")]
    public void ConstWithExpression_SeenByIf(string consts, string cond)
    {
        var (names, diag) = Run(consts + IfMark(cond));
        Assert.False(diag.HasErrors, string.Join("; ", diag.Diagnostics));
        var key = cond.Split(' ')[0];
        Assert.Contains($"YES_{key}", names);
        Assert.DoesNotContain($"NO_{key}", names);
    }

    [Theory]
    // 値が決まらないもの (後ろの CONST・FLOAT・ラベル) は今までどおり覚えない (#IF では 0)
    [InlineData("CONST A = B + 1; CONST B = 2;", "A == 0")]
    [InlineData("CONST A = 2.0;", "A == 0")]
    [InlineData("CONST A = 1.5 * 2;", "A == 0")]
    public void ConstWithoutValue_IsZeroInIf(string consts, string cond)
    {
        var (names, _) = Run(consts + IfMark(cond));
        Assert.Contains($"YES_{cond.Split(' ')[0]}", names);
    }

    [Theory]
    // 右辺の終わりが分からない CONST (#IF で始まる・式の後ろが #IF・#IF の中の字句の列の終わり) を #IF で使うとエラー
    // (値を 0 として黙って違う分岐を選ばない)
    [InlineData("CONST K =\n#IF 1\n2\n#ELSE\n3\n#END\n;")]
    [InlineData("CONST K = 1 + 1\n#IF 1\n+ 1\n#END\n;")]
    [InlineData("#IF 1\nCONST K = 1 + 1\n#END\n- 1;")]
    [InlineData("#IF 1\nCONST K = 1 + 1\n#END\n- 1; CONST J = K + 1;")]
    public void ConstValueUnknown_UsedInIf_Errors(string decl)
    {
        var key = decl.Contains("CONST J") ? "J" : "K";
        var (_, diag) = Run(decl + IfMark($"{key} == 1"));
        Assert.Contains(diag.Diagnostics, d => d.Message.Contains($"#IF uses CONST '{key}', whose value is not known here"));
    }

    [Fact]
    public void BoundaryLiteral_AliasIsUnknown()
    {
        // #IF の中で切れた CONST K=1 (続きの +1 が #END の後ろ) を、別名の FLAG に広げない: FLAG を #IF で使うとエラー
        var (_, diag) = Run("#IF 1\nCONST K = 1\n#END\n+ 1;\nCONST FLAG = K;" + IfMark("FLAG == 1"));
        Assert.Contains(diag.Diagnostics, d => d.Message.Contains("#IF uses CONST 'FLAG', whose value is not known here"));
    }

    [Fact]
    public void ConstValueUnknown_NotUsedInIf_NoDiagnostic()
    {
        var (_, diag) = Run("#IF 1\nCONST K = 1 + 1\n#END\n- 1;");
        Assert.Empty(diag.Diagnostics);
    }

    [Fact]
    public void ConstLiteralWithoutSemicolonBeforeIf_Recorded()
    {
        // ; を省いた整数のリテラル 1 つの後ろに #IF: いままでどおり値を覚える
        var (names, diag) = Run("CONST K = 1\n#IF K\nYES_K\n#ELSE\nNO_K\n#END\n");
        Assert.Contains("YES_K", names);
        Assert.Empty(diag.Diagnostics);
    }

    [Fact]
    public void ConstStartingWithDirective_DoesNotPickNamesInsideIf()
    {
        // 右辺が #IF で始まるとき、#IF の中 (選ばれないかもしれない) の , の後ろの J を覚えない
        var (names, _) = Run("CONST K =\n#IF 0\n2, J = 3\n#ELSE\n4\n#END\n;" + IfMark("J == 0"));
        Assert.Contains("YES_J", names);
    }

    [Fact]
    public void ConstThroughOtherConst_ReportsAllNamesUsedByIf()
    {
        // #IF FLAG の FLAG が LOCAL (関数の中の CONST かもしれない) から決まるとき、LOCAL も #IF で使った名前にする
        // (意味解析が、関数の中の CONST を #IF に使ったことを見つけられるように)
        var diag = new DiagnosticBag();
        var pre = new Preprocessor(diag);
        pre.Process(new Lexer.Lexer("CONST LOCAL = 1; CONST MID = LOCAL + 1; CONST FLAG = MID;\n#IF FLAG\n#END\n").Tokenize(), _tempDir);
        Assert.Contains("FLAG", pre.IdentifiersUsedByIf);
        Assert.Contains("MID", pre.IdentifiersUsedByIf);
        Assert.Contains("LOCAL", pre.IdentifiersUsedByIf);
    }

    // ==== #CHAIN (件 13) ====

    [Fact]
    public void Chain_ReadsNextFileAndStops()
    {
        Write("part2.sl", "PART2_A PART2_B");
        var (names, diag) = Run("MAIN_A\n#CHAIN part2.sl\nNOT_READ\n");
        Assert.Equal(new[] { "MAIN_A", "PART2_A", "PART2_B" }, names);
        Assert.Contains(diag.Diagnostics, d => d.Message.Contains("Text after #CHAIN is not read"));
        Assert.False(diag.HasErrors);
    }

    [Fact]
    public void Chain_AtEndOfFile_NoWarning()
    {
        Write("part2.sl", "PART2_A");
        var (names, diag) = Run("MAIN_A\n#CHAIN part2.sl\n");
        Assert.Equal(new[] { "MAIN_A", "PART2_A" }, names);
        Assert.Empty(diag.Diagnostics);
    }

    [Fact]
    public void Chain_InsideIf_StopsRestOfFile()
    {
        // #IF の中の #CHAIN も、そのファイルの残り (#IF の外も) を読まない
        Write("part2.sl", "PART2_A");
        var (names, _) = Run("CONST K = 1;\nMAIN_A\n#IF K == 1\n#CHAIN part2.sl\nIN_IF\n#END\nAFTER_IF\n");
        Assert.Equal(new[] { "K", "MAIN_A", "PART2_A" }, names);
    }

    [Fact]
    public void Chain_InIncludedFile_IncluderContinues()
    {
        // 取り込んだファイルの中の #CHAIN は、そのファイルの残りを読まないだけ。取り込んだ側は続ける
        Write("lib/inc.sl", "INC_A\n#CHAIN more.sl\nINC_NOT_READ\n");
        Write("lib/more.sl", "MORE_A");
        var (names, _) = Run("MAIN_A\n#INCLUDE lib/inc.sl\nMAIN_B\n");
        Assert.Equal(new[] { "MAIN_A", "INC_A", "MORE_A", "MAIN_B" }, names);
    }

    [Fact]
    public void Chain_Loop_Errors()
    {
        Write("a.sl", "A_PART\n#CHAIN b.sl\n");
        Write("b.sl", "B_PART\n#CHAIN a.sl\n");
        var (_, diag) = Run("#CHAIN a.sl\n");
        Assert.Contains(diag.Diagnostics, d => d.Message.Contains("#CHAIN loop"));
    }

    [Fact]
    public void Chain_SameTargetFromRepeatedIncludes_NotALoop()
    {
        // part.sl を 2 度取り込み、どちらも tail.sl へ移る: 輪ではない
        Write("part.sl", "PART_A\n#CHAIN tail.sl\n");
        Write("tail.sl", "TAIL_A");
        var (names, diag) = Run("#INCLUDE part.sl\n#INCLUDE part.sl\nMAIN_B\n");
        Assert.False(diag.HasErrors, string.Join("; ", diag.Diagnostics));
        Assert.Equal(new[] { "PART_A", "TAIL_A", "PART_A", "TAIL_A", "MAIN_B" }, names);
    }

    [Fact]
    public void Chain_NotFound_Errors()
    {
        var (_, diag) = Run("#CHAIN nothing.sl\n");
        Assert.Contains(diag.Diagnostics, d => d.Message.Contains("#CHAIN file not found: nothing.sl"));
    }

    [Fact]
    public void IncludeInsideIf_InIncludedFile_UsesThatFilesDirectory()
    {
        // 取り込んだファイルの #IF の中の #INCLUDE は、そのファイルの場所から探す (いままでは . から探していた)
        Write("lib/inc.sl", "#IF 1\n#INCLUDE inner.sl\n#END\n");
        Write("lib/inner.sl", "INNER_A");
        var (names, diag) = Run("#INCLUDE lib/inc.sl\n");
        Assert.False(diag.HasErrors, string.Join("; ", diag.Diagnostics));
        Assert.Equal(new[] { "INNER_A" }, names);
    }

    [Fact]
    public void Chain_DistinctFilesDifferingOnlyInCase_NotALoop()
    {
        // 大文字小文字を区別するファイルの仕組みでは、a.sl と A.sl は別のファイル (輪ではない)
        if (OperatingSystem.IsWindows() || OperatingSystem.IsMacOS()) return;
        Write("a.sl", "A_LOWER\n#CHAIN A.sl\n");
        Write("A.sl", "A_UPPER");
        var (names, diag) = Run("#CHAIN a.sl\n");
        Assert.False(diag.HasErrors, string.Join("; ", diag.Diagnostics));
        Assert.Equal(new[] { "A_LOWER", "A_UPPER" }, names);
    }

    [Fact]
    public void Chain_OversizedNumberAfterChain_DoesNotCrash()
    {
        // #CHAIN の後ろの読まない所に大きすぎる数があっても、字句解析で落ちない (warning だけ)
        Write("part2.sl", "PART2_A");
        var (names, diag) = Run("MAIN_A\n#CHAIN part2.sl\n9999999999999999999999999\n");
        Assert.Equal(new[] { "MAIN_A", "PART2_A" }, names);
        Assert.False(diag.HasErrors, string.Join("; ", diag.Diagnostics));
    }

    [Fact]
    public void ConstTableSpanningIf_DoesNotRecordFollowingConst()
    {
        // 並びの中に #IF があるとき、選ばれない側の ] の後ろの B を覚えない (#IF では 0)
        var (names, _) = Run("CONST T = [0,\n#IF 0\n1], B = 5;\n#ELSE\n2];\n#END\n" + IfMark("B == 0"));
        Assert.Contains("YES_B", names);
    }

    [Fact]
    public void Chain_MalformedNumberAfterChain_DoesNotCrash()
    {
        Write("part2.sl", "PART2_A");
        var (names, diag) = Run("MAIN_A\n#CHAIN part2.sl\n12AF\n");
        Assert.Equal(new[] { "MAIN_A", "PART2_A" }, names);
        Assert.False(diag.HasErrors, string.Join("; ", diag.Diagnostics));
    }
}
