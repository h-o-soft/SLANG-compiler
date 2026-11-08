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
    private const string Decls = "VAR A, C, I, J, W, X, FLAG; VAR BYTE B; ARRAY T[3]; VAR P[]; VAR FLOAT F; VAR FLOAT FG; "
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
    public void TargetWithoutSideEffect_Ok(string stmt)
    {
        var (_, diag) = Compile($"{Decls} MAIN() BEGIN {stmt} END;");
        Assert.False(diag.HasErrors, string.Join("; ", diag.Diagnostics));
    }
}
