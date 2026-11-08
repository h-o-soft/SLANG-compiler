using System.Text.RegularExpressions;
using Xunit;
using SLANGCompiler.Lexer;
using SLANGCompiler.Parser;
using SLANGCompiler.IR;
using SLANGCompiler.CodeGen;
using SLANGCompiler.Semantics;

namespace SLANGCompiler.Tests;

/// <summary>
/// 式の値: 後置・前置の ++ --、メモリの上の代入の値、?:、FLOAT の ++ -- と複合代入
/// </summary>
public class ExpressionValueTests
{
    private const string Decls = "VAR A, C, I, J, W, X, FLAG, D, E, N, Y; VAR BYTE B; ARRAY T[3]; VAR P[]; VAR FLOAT F; VAR FLOAT FG; "
        + "ARRAY FLOAT FA[3]; G(Q) BEGIN RETURN Q + 1; END;";

    /// <summary>ASM まで作る。エラーがあれば (null, 診断)</summary>
    private static (string? Asm, DiagnosticBag Diag) Compile(string source)
    {
        var diag = new DiagnosticBag();
        var tokens = new Preprocessor(diag).Process(new Lexer.Lexer(source).Tokenize(), ".");
        var ast = new Parser.Parser(tokens, diag).ParseCompilationUnit();
        if (diag.HasErrors) return (null, diag);
        var analyzer = new SemanticAnalyzer(diag);
        analyzer.Analyze(ast);
        if (diag.HasErrors) return (null, diag);
        var ir = new IrGenerator(diag, analyzer.Symbols).Generate(ast);
        if (diag.HasErrors) return (null, diag);
        return (new CodeGenerator(ir).Generate(), diag);
    }

    private static string MainBody(string stmts)
    {
        var (asm, diag) = Compile($"{Decls} MAIN() BEGIN {stmts} END;");
        Assert.False(diag.HasErrors, string.Join("; ", diag.Diagnostics));
        return asm!.Split("\nMAIN:")[1].Split("_MAIN_EXIT")[0];
    }

    private static string Seq(params string[] lines) => string.Join(@"\s*\n\s*", lines);

    // ==== 後置 ++ -- の値 (件 3) ====

    [Fact]
    public void Postfix_ScalarValueIsOld()
    {
        var body = MainBody("A = 5; C = A++; W = A--;");
        Assert.Matches(Seq(@"INC\tHL", @"LD\t\(_V_A\),HL", @"DEC\tHL", @"LD\t\(_V_C\),HL"), body);
        Assert.Matches(Seq(@"DEC\tHL", @"LD\t\(_V_A\),HL", @"INC\tHL", @"LD\t\(_V_W\),HL"), body);
    }

    [Fact]
    public void Postfix_ByteValueIsOld()
    {
        // BYTE: 新しい値は上位を切る前の HL のままなので、引けば古い値
        var body = MainBody("C = B++;");
        Assert.Matches(Seq(@"INC\tHL", @"LD\tA,L", @"LD\t\(_V_B\),A", @"DEC\tHL", @"LD\t\(_V_C\),HL"), body);
    }

    [Fact]
    public void Postfix_ArrayElementValueIsOld()
    {
        var body = MainBody("W = T[I]++;");
        Assert.Matches(Seq(@"LD\t\(HL\),D", @"EX\tDE,HL", @"DEC\tHL", @"LD\t\(_V_W\),HL"), body);
    }

    [Theory]
    // 値を使わない ++ -- (式の文・カンマ式の左) は、作り直しも EX DE,HL も出さない (いままでどおり)
    [InlineData("A++;", @"INC\tHL", @"DEC\tHL")]
    [InlineData("B--;", @"DEC\tHL", @"INC\tHL")]
    [InlineData("A++, C = 1;", @"INC\tHL", @"DEC\tHL")]
    public void Postfix_ValueUnused_NoCompensation(string stmt, string step, string undo)
    {
        var body = MainBody(stmt);
        Assert.Single(Regex.Matches(body, step));
        Assert.DoesNotMatch(undo, body);
    }

    [Fact]
    public void Postfix_ArrayElementValueUnused_NoCompensation()
    {
        var body = MainBody("T[I]++;");
        Assert.DoesNotContain("EX\tDE,HL\n\tDEC", body);
        Assert.Matches(@"LD\t\(HL\),D\s*$", body);   // 書き込みで終わる
    }

    [Fact]
    public void Postfix_InBinaryExpression()
    {
        // 左・右どちらでも古い値を使う
        var body = MainBody("W = 3 - A++; W = A-- - I;");
        Assert.Matches(Seq(@"LD\tHL,\$0003", @"PUSH\tHL", @"LD\tHL,\(_V_A\)", @"INC\tHL", @"LD\t\(_V_A\),HL", @"DEC\tHL",
            @"POP\tDE", @"EX\tDE,HL", @"OR\tA", @"SBC\tHL,DE"), body);
        Assert.Matches(Seq(@"DEC\tHL", @"LD\t\(_V_A\),HL", @"INC\tHL", @"LD\tDE,\(_V_I\)", @"OR\tA", @"SBC\tHL,DE"), body);
    }

    // ==== メモリの上の代入・前置 ++ の値 (件 17・18) ====

    [Theory]
    [InlineData("C = ++T[I];")]
    [InlineData("C = ++P[2];")]
    [InlineData("C = T[I] = 5;")]
    [InlineData("C = (T[I] += 2);")]
    public void StoreToMemory_ValueIsStoredValue(string stmt)
    {
        // 書き込みの後 HL は番地。値は DE にあるので、使うなら EX DE,HL
        var body = MainBody(stmt);
        Assert.Matches(Seq(@"LD\t\(HL\),D", @"EX\tDE,HL", @"LD\t\(_V_C\),HL"), body);
    }

    [Fact]
    public void StoreToMem_ByteValue()
    {
        var body = MainBody("C = ++MEM[$C000];");
        Assert.Matches(Seq(@"POP\tDE", @"LD\t\(HL\),E", @"EX\tDE,HL", @"LD\t\(_V_C\),HL"), body);
    }

    [Theory]
    [InlineData("T[I] = 5;")]
    [InlineData("++T[I];")]
    [InlineData("MEM[$C000] = 1;")]
    public void StoreToMemory_ValueUnused_Unchanged(string stmt)
    {
        // 値を使わない書き込みは、書き込みで終わる (EX DE,HL を足さない)
        var body = MainBody(stmt);
        Assert.Matches(@"LD\t\(HL\),[DE]\s*$", body);
    }

    [Fact]
    public void StoreToMemory_ValueKeptAcrossCall()
    {
        // 値を左に置いた引き算で、右が関数の呼び出し: 値を積んでから呼ぶ
        var body = MainBody("W = (T[I] = 5) - G(1);");
        Assert.Matches(Seq(@"LD\t\(HL\),D", @"EX\tDE,HL", @"PUSH\tHL"), body);
        Assert.Matches(Seq(@"CALL\tG", @"POP\tDE", @"EX\tDE,HL", @"OR\tA", @"SBC\tHL,DE"), body);
    }

    // ==== ?: の値 (件 4) ====

    [Theory]
    [InlineData("W = C ? A : I;")]
    [InlineData("W = (C ? A : I) - G(1);")]
    [InlineData("W = A - (C ? 1 : 2);")]
    [InlineData("W = G(C ? A : I);")]
    [InlineData("W = C ? (A ? 1 : 2) : 3;")]
    [InlineData("IF (C ? A : I) == 5 THEN W = 1;")]
    [InlineData("W = (C ? 12 : 0) + A;")]
    public void Conditional_StackBalanced(string stmt)
    {
        // 側の値は写さず HL に残す。積んでいない値を POP しない (PUSH と POP の数が合う)
        var body = MainBody(stmt);
        Assert.DoesNotMatch(@"POP\tDE\s*\n\s*ADD\tHL,DE\s*\n\s*JP\t_L", body);
        Assert.Equal(Regex.Matches(body, @"\tPUSH\t").Count, Regex.Matches(body, @"\tPOP\t").Count);
    }

    [Fact]
    public void Conditional_ValueOfEachSide()
    {
        var body = MainBody("W = C ? A : I;");
        Assert.Matches(Seq(@"JP\tZ,(_L\d+)", @"LD\tHL,\(_V_A\)", @"JP\t(_L\d+)", @"\1:", @"LD\tHL,\(_V_I\)", @"\2:", @"LD\t\(_V_W\),HL"), body);
    }

    [Fact]
    public void Conditional_FloatWidensOtherSide()
    {
        // 片方が FLOAT なら、もう片方を FLOAT にする
        var body = MainBody("F = C ? 1.5 : 2;");
        Assert.Matches(Seq(@"LD\tHL,\$0002", @"CALL\ti16tof24", @"(_L\d+):", @"LD\t\(_V_F\),HL", @"LD\t\(_V_F\+2\),A"), body);
    }

    // ==== FLOAT の ++ -- と複合代入 (件 15) ====

    [Theory]
    [InlineData("F++;")]
    [InlineData("++F;")]
    [InlineData("F += 1;")]
    [InlineData("F -= 0.5;")]
    [InlineData("F *= 2;")]
    [InlineData("F /= 2;")]
    [InlineData("FG = ++F;")]
    public void Float_IncrementAndCompoundUseFloatArithmetic(string stmt)
    {
        // F = F op 値 と同じ (FLOAT の四則演算)。整数で足してから i16tof24 で変えない
        var body = MainBody($"F = 1.5; {stmt}");
        Assert.Matches(@"CALL\tf24(add|sub|mul|div)", body);
        Assert.DoesNotMatch(Seq(@"ADD\tHL,DE", @"CALL\ti16tof24"), body);
    }

    [Fact]
    public void Float_SameAsPlainAssignment()
    {
        Assert.Equal(MainBody("F = F + 1;"), MainBody("F += 1;"));
        Assert.Equal(MainBody("F = F - 1;"), MainBody("F--;"));
        // FLOAT の定数の四則演算も、代入の文と同じく正しい値で畳み込む (1.0/2.0 を整数の 0 にしない)
        Assert.Equal(MainBody("F = F * (1.0/2.0);"), MainBody("F *= (1.0/2.0);"));
        Assert.Equal(MainBody("F = F + 3.0/4.0;"), MainBody("F += 3.0/4.0;"));
    }

    [Fact]
    public void Float_PostfixValue_Errors()
    {
        var (_, diag) = Compile($"{Decls} MAIN() BEGIN FG = F++; END;");
        Assert.Contains(diag.Diagnostics, d => d.Message.Contains("postfix ++/-- on FLOAT"));
    }

    // ==== 設計レビューで見つかった形 ====

    [Fact]
    public void AssignValueInBinary_StoresRightValue()
    {
        // C = (A = 5) + X: 5 のロードを直接ロードで飛ばさない (A に 5 を書く)。いまの main でも壊れていた形
        var body = MainBody("C = (A = 5) + X;");
        Assert.Matches(Seq(@"LD\tHL,\$0005", @"LD\t\(_V_A\),HL", @"LD\tDE,\(_V_X\)", @"ADD\tHL,DE", @"LD\t\(_V_C\),HL"), body);
    }

    [Fact]
    public void MemStoreValueInBinary_StackBalanced()
    {
        var body = MainBody("C = (MEM[I] = 5) + X;");
        Assert.Matches(Seq(@"LD\tHL,\$0005", @"PUSH\tHL", @"LD\tHL,\(_V_I\)", @"POP\tDE", @"LD\t\(HL\),E", @"EX\tDE,HL",
            @"LD\tDE,\(_V_X\)", @"ADD\tHL,DE"), body);
    }

    [Fact]
    public void Conditional_StoreInBranch_KeepsValue()
    {
        var body = MainBody("C = FLAG ? (MEM[I] = 5) : 0;");
        Assert.Matches(Seq(@"LD\t\(HL\),E", @"EX\tDE,HL", @"JP\t_L\d+"), body);
    }

    [Fact]
    public void ReturnOfMemoryStore_ReturnsValue()
    {
        var (asm, diag) = Compile($"{Decls} H2() BEGIN RETURN(MEM[I] = 5); END; MAIN() BEGIN C = H2(); END;");
        Assert.False(diag.HasErrors, string.Join("; ", diag.Diagnostics));
        Assert.Matches(Seq(@"LD\t\(HL\),E", @"EX\tDE,HL"), asm!.Split("\nH2:")[1]);
    }

    [Theory]
    [InlineData("I++, J++;")]
    [InlineData("(0, I++);")]
    public void CommaStatement_DiscardedIncrement_NoCompensation(string stmt)
    {
        var body = MainBody(stmt);
        Assert.DoesNotContain("DEC\tHL", body);
    }

    [Fact]
    public void FloatStoreValue_ConvertedFromStoredValue()
    {
        // W = (FA[I] = 5): 書いた値 (FLOAT の 5.0) を HL・A に戻してから整数に変える
        var body = MainBody("W = (FA[I] = 5);");
        Assert.Matches(Seq(@"LD\t\(HL\),A", @"EX\tDE,HL", @"CALL\tFTOI", @"LD\t\(_V_W\),HL"), body);
    }

    [Fact]
    public void Conditional_ComparisonSideIsInteger()
    {
        // 比較の値は、辺が FLOAT でも 0/1 の整数
        var body = MainBody("C = FLAG ? (F > 1.0) : 0;");
        Assert.DoesNotContain("i16tof24", body);
        Assert.DoesNotContain("FTOI", body);
        var fbody = MainBody("FG = FLAG ? (F > 1.0) : 2.5;");
        Assert.Matches(Seq(@"INC\tHL", @"CALL\ti16tof24", @"JP\t_L\d+"), fbody);
    }

    [Theory]
    [InlineData("T[I++]++;")]
    [InlineData("T[I++] += 1;")]
    [InlineData("T[G(1)] += 1;")]
    [InlineData("MEM[I++]++;")]
    [InlineData("C = --T[J = 1];")]
    public void TargetEvaluatedTwice_WithSideEffect_Errors(string stmt)
    {
        var (_, diag) = Compile($"{Decls} MAIN() BEGIN {stmt} END;");
        Assert.Contains(diag.Diagnostics, d => d.Message.Contains("evaluated twice"));
    }

    [Theory]
    [InlineData("T[I]++;")]
    [InlineData("T[I + 1] += 2;")]
    [InlineData("T[I++] = 5;")]
    [InlineData("T[0] += (T[1] = 2);")]     // ほかの要素に書いても、代入先 T[0] は変わらない
    public void TargetWithoutSideEffect_Ok(string stmt)
    {
        var (_, diag) = Compile($"{Decls} MAIN() BEGIN {stmt} END;");
        Assert.False(diag.HasErrors, string.Join("; ", diag.Diagnostics));
    }

    // ==== 設計レビュー 2 周目で見つかった形 ====

    [Fact]
    public void StoreValue_UsedByPrint()
    {
        // PRINT は値を暗に (HL で) 受け取る。書いた値を HL に戻してから出す
        var body = MainBody("PRINT(MEMW[I] = 5);");
        Assert.Matches(Seq(@"LD\t\(HL\),D", @"EX\tDE,HL", @"CALL\tP10"), body);
    }

    [Fact]
    public void PortStoreValue_PushedForOuterOperation()
    {
        // PORT への書き込みの後、値は HL にある。外の足し算の右がまだ残っているので積む
        var body = MainBody("C = (PORTW[I] = A + D) + (E + Y);");
        Assert.Matches(Seq(@"OUT\t\(C\),H", @"PUSH\tHL", @"LD\tHL,\(_V_E\)"), body);
        Assert.Equal(Regex.Matches(body, @"\tPUSH\t").Count, Regex.Matches(body, @"\tPOP\t").Count);
    }

    [Fact]
    public void AssignValue_TypeIsTargetType()
    {
        // (F = 5) の値は FLOAT (書いた値)。左の整数は、右を求める前に FLOAT にする
        var body = MainBody("FG = N + (F = 5);");
        Assert.Matches(Seq(@"LD\tHL,\(_V_N\)", @"CALL\ti16tof24", @"PUSH\tAF", @"PUSH\tHL"), body);
    }

    [Fact]
    public void FloatComparison_ValueIsInteger()
    {
        // FLOAT の比較の値は 0/1 の整数 (FTOI をかけない)。いまの main でも壊れていた形
        var body = MainBody("W = F < FG;");
        Assert.DoesNotContain("FTOI", body);
        Assert.Matches(Seq(@"CALL\tf24cmp", @"LD\tHL,\$0000", @"JR\tNC,\$\+3", @"INC\tHL", @"LD\t\(_V_W\),HL"), body);
    }

    [Theory]
    [InlineData("MEMW[I] = F;")]
    [InlineData("PORTW[I] = F;")]
    public void FloatToMemOrPort_ConvertedToInteger(string stmt)
    {
        // FLOAT を MEM・PORT に書くときは整数に変える (変えないと AF も積まれて、スタックがずれる)
        var body = MainBody(stmt);
        Assert.Matches(Seq(@"LD\tA,\(_V_F\+2\)", @"CALL\tFTOI", @"PUSH\tHL"), body);
        Assert.DoesNotContain("PUSH\tAF", body);
    }

    [Theory]
    [InlineData("+I++;")]
    [InlineData("%I++;")]
    [InlineData("(I++, J++);")]
    public void DiscardedThroughWrappers_NoCompensation(string stmt)
    {
        var body = MainBody(stmt);
        Assert.DoesNotContain("DEC\tHL", body);
    }

    [Fact]
    public void ChainedScalarAssignment_Unchanged()
    {
        var body = MainBody("X = Y = 5;");
        Assert.Matches(Seq(@"LD\tHL,\$0005", @"LD\t\(_V_Y\),HL", @"LD\t\(_V_X\),HL"), body);
    }


    // ==== 設計レビュー 3 周目で見つかった形 ====

    [Theory]
    [InlineData("W = (F < FG) + (A + D);")]
    [InlineData("MEMW[I] = (F < FG);")]
    public void FloatComparisonResult_PushedAsWord(string stmt)
    {
        // FLOAT の比較の結果は 0/1 の整数なので、1 語だけ積む (AF も積むと、取り出しが 1 語なのでずれる)
        var body = MainBody(stmt);
        Assert.Matches(Seq(@"CALL\tf24cmp", @"LD\tHL,\$0000", @"JR\tNC,\$\+3", @"INC\tHL", @"PUSH\tHL"), body);
        Assert.DoesNotMatch(Seq(@"INC\tHL", @"PUSH\tAF"), body);
        Assert.Equal(Regex.Matches(body, @"\tPUSH\t").Count, Regex.Matches(body, @"\tPOP\t").Count);
    }

    [Fact]
    public void ScalarAssignInsideMemoryStore_Unchanged()
    {
        // T[I] = (A = 5): 中の代入はスカラーなので、値を積む位置はいままでどおり (A に書く前)
        var body = MainBody("T[I] = (A = 5);");
        Assert.Matches(Seq(@"LD\tHL,\$0005", @"PUSH\tHL", @"LD\t\(_V_A\),HL"), body);
    }

    [Fact]
    public void LocalStoreInConditionalArm_WithConversion_KeepsLoad()
    {
        // FG = FLAG ? (L = 5) : F; (L は局所の WORD): 真の側の値は L に書いた 5 を FLOAT に変えたもの。
        // 5 のロードを、局所への即値の書き込みの最適化で飛ばさない (i16tof24 が古い HL を変えてしまう)
        var (asm, diag) = Compile($"{Decls} R(N) BEGIN VAR L; IF N > 0 THEN R(N - 1); FG = FLAG ? (L = 5) : F; END; MAIN() BEGIN R(1); END;");
        Assert.False(diag.HasErrors, string.Join("; ", diag.Diagnostics));
        var body = asm!.Split("\n_F_R:")[1].Split("__F_R_EXIT")[0];
        Assert.Matches(Seq(@"LD\tHL,\$0005", @"LD\t\(IY\+\$\w\w\),L", @"LD\t\(IY\+\$\w\w\),H", @"CALL\ti16tof24"), body);
    }

    // ==== 実装レビューで見つかった形 ====

    [Theory]
    // FLOAT の配列の要素・FLOAT を返す関数も、?: の FLOAT の側 (結果を FLOAT にする)
    [InlineData("F = FLAG ? FA[0] : FA[1];")]
    [InlineData("F = FLAG ? FA[0] : 2;")]
    [InlineData("F = FLAG ? FG2() : 1;")]
    [InlineData("F = FLAG ? +FA[0] : 0;")]          // 値をそのまま返す単項の + の中
    [InlineData("F = FLAG ? 0 : FA[1];")]           // 偽の側だけ FLOAT (真の側を、真の側の終わりで広げる)
    public void Conditional_FloatArrayAndFunctionSides(string stmt)
    {
        var (asm, diag) = Compile($"{Decls} FG2:FLOAT() BEGIN RETURN 1.5; END; MAIN() BEGIN {stmt} END;");
        Assert.False(diag.HasErrors, string.Join("; ", diag.Diagnostics));
        var body = asm!.Split("\nMAIN:")[1].Split("_MAIN_EXIT")[0];
        Assert.Matches(Seq(@"LD\t\(_V_F\),HL", @"LD\t\(_V_F\+2\),A"), body);
        Assert.DoesNotContain("FTOI", body);
    }

    [Fact]
    public void Conditional_FloatConstantArithmeticInSide()
    {
        // ?: の側の FLOAT の定数の四則演算も、代入の文と同じく正しい値で畳み込む (5.0/2.0 は 2.5)
        var body = MainBody("F = FLAG ? 5.0/2.0 : 1.5;");
        var direct = MainBody("F = 5.0/2.0;");
        var load25 = System.Text.RegularExpressions.Regex.Match(direct, @"LD\tHL,\$\w{4}\s*\n\s*LD\tA,\$\w\w").Value;
        Assert.False(string.IsNullOrEmpty(load25));
        Assert.Contains(load25, body);
    }

    [Fact]
    public void Conditional_TrueSideWidenedAtEndOfTrueSide()
    {
        // 偽の側だけ FLOAT: 真の側の整数は、合流へ飛ぶ前 (真の側の終わり) で FLOAT にする
        var body = MainBody("F = FLAG ? 2 : 1.5;");
        Assert.Matches(Seq(@"LD\tHL,\$0002", @"CALL\ti16tof24", @"JP\t_L\d+"), body);
    }

    [Fact]
    public void Conditional_PartialFloatArrayIsAddress()
    {
        // 2 次元の FLOAT の配列の添字 1 つ (FA2[0]) は番地 (WORD)。?: の結果も WORD で、WORD の引数に渡せる
        var (asm, diag) = Compile($"{Decls} ARRAY FLOAT FA2[1][1]; MAIN() BEGIN C = G(FLAG ? FA2[0] : FA2[1]); END;");
        Assert.False(diag.HasErrors, string.Join("; ", diag.Diagnostics));
        Assert.DoesNotContain("i16tof24", asm!.Split("\nMAIN:")[1].Split("_MAIN_EXIT")[0]);
    }

    [Theory]
    [InlineData("T[I] += (I = I + 1);")]
    [InlineData("T[I] -= I++;")]
    [InlineData("T[I] += CODE([I = I + 1]);")]   // CODE の中の代入も
    [InlineData("T[T[0]] += (T[0] = 1);")]       // 添字が読む配列の要素に書く
    [InlineData("T[I] += (MEMW[&I] = 1);")]      // メモリを通して添字の変数を変える
    [InlineData("P[0] += (P = &W);")]            // 間接変数の値 (番地) を変える
    [InlineData("T[I] += (J = 3);")]             // 添字が変数なら、右辺に代入を書かない (どの変数を変えるかは見ない)
    [InlineData("T[PORT[I]]++;")]
    public void TargetChangedByRightSideOrPort_Errors(string stmt)
    {
        var (_, diag) = Compile($"{Decls} MAIN() BEGIN {stmt} END;");
        Assert.Contains(diag.Diagnostics, d => d.Message.Contains("evaluated twice"));
    }

    [Fact]
    public void TargetNotChangedByRightSide_Ok()
    {
        // 右辺の関数の呼び出しは見ない (関数の中で変わるものは見ない)。代入先の添字が定数なら、右辺の代入もよい
        var (_, diag) = Compile($"{Decls} MAIN() BEGIN T[I] += G(J); T[0] += (J = 3); END;");
        Assert.False(diag.HasErrors, string.Join("; ", diag.Diagnostics));
    }
}
