using SLANGCompiler.IR;
using SLANGCompiler.Lexer;
using SLANGCompiler.Parser;
using SLANGCompiler.Semantics;
using Xunit;

namespace SLANGCompiler.Tests;

/// <summary>
/// FLOAT の定数を含む式の畳み込み。小数部のない FLOAT の定数どうしの四則演算を整数として畳み込まない
/// (5.0/2.0 が 2、40000.0+40000.0 が 14464 になっていた)。整数が要る所の評価・単独の FLOAT の定数を整数として読む動き
/// (CONST FLDIV=64.0 など)・整数だけの式の畳み込みは変えない。あわせて、右辺が FLOAT の式のときに左辺の整数の
/// 変換が右辺の値に対して行われていた不具合 (I+(A*A)) を直す。
/// </summary>
public class FloatConstFoldTests
{
    private static (string Ir, SymbolTable Symbols, IrModule Module) Generate(string source)
    {
        var diag = new DiagnosticBag();
        var tokens = new Lexer.Lexer(source).Tokenize();
        tokens = new Preprocessor(diag).Process(tokens, ".");
        var ast = new Parser.Parser(tokens, diag).ParseCompilationUnit();
        Assert.False(diag.HasErrors, $"Parse errors: {string.Join("; ", diag.Diagnostics)}");
        var analyzer = new SemanticAnalyzer(diag);
        analyzer.Analyze(ast);
        var module = new IrGenerator(diag, analyzer.Symbols).Generate(ast);
        Assert.False(diag.HasErrors, $"IR errors: {string.Join("; ", diag.Diagnostics)}");
        return (module.ToString(), analyzer.Symbols, module);
    }

    private static string MainIr(string ir) => ir[ir.IndexOf("func MAIN:", StringComparison.Ordinal)..];

    private static List<IrInstruction> MainOps(IrModule module) => module.Functions.Single(f => f.Name == "MAIN").Instructions;

    /// <summary>MAIN の ASM (CodeGenerator まで通す)</summary>
    private static string MainAsm(string source)
    {
        var asm = new SLANGCompiler.CodeGen.CodeGenerator(Generate(source).Module).Generate();
        return asm.Split("MAIN:")[1].Split("_MAIN_EXIT")[0];
    }

    [Fact]
    public void FloatArithmetic_FoldsToCorrectFloatConstant()
    {
        var ir = MainIr(Generate("FLOAT B; MAIN() BEGIN B=5.0/2.0; B=40000.0+40000.0; END;").Ir);
        Assert.Contains("LoadFloatConst t0 #16384 #64", ir);   // 2.5 (今までは整数の 2)
        Assert.Contains("LoadFloatConst t1 #14464 #79", ir);   // 80000 (今までは 16 ビットに回り込んだ 14464)
        Assert.DoesNotContain("LoadConst", ir);
        Assert.DoesNotContain("i16tof24", ir);
    }

    [Fact]
    public void FloatArithmetic_IntegralResultStaysInteger()
    {
        // 小数部がなく 16 ビットに収まる結果は、今までと同じ整数の即値 (値は正しく求め直す: 6.0/4.0*2.0 は 3)
        var ir = MainIr(Generate("VAR C; MAIN() BEGIN C=6.0/4.0*2.0; C=64.0*2; END;").Ir);
        Assert.Contains("LoadConst t0 #3", ir);
        Assert.Contains("LoadConst t1 #128", ir);
    }

    [Fact]
    public void IntegerArithmetic_IsUnchanged()
    {
        var ir = MainIr(Generate("VAR C; VAR D; MAIN() BEGIN C=5/2; D=40000+40000; END;").Ir);
        Assert.Contains("LoadConst t0 #2", ir);
        Assert.Contains("#14464", ir);                 // 整数は今までどおり 16 ビットに回り込む
        Assert.DoesNotContain("Div", ir);
        Assert.DoesNotContain("Add", ir);
    }

    [Theory]
    [InlineData("B=I+5.0/2.0;", IrOp.Add)]
    [InlineData("B=I+(A*A);", IrOp.Add)]
    [InlineData("B=I*-(A+A);", IrOp.Mul)]
    [InlineData("B=I+(0,5.0/2.0);", IrOp.Add)]   // カンマ式は右の値
    [InlineData("B=I+(A*1);", IrOp.Add)]         // 何もしない演算の正規化で A (FLOAT) が残る
    [InlineData("B=I+(A=-(5.0/2.0));", IrOp.Add)] // 代入式は代入する値
    [InlineData("B=I+(A++);", IrOp.Add)]          // 後置の増分は元の値 (FLOAT)
    [InlineData("B=I+&(A*A);", IrOp.Add)]         // 変数・配列以外の & は中身の値
    [InlineData("B=I+&MEM[A*A];", IrOp.Add)]      // &MEM[x] は x の値
    public void MixedExpression_ConvertsLeftBeforeFloatRight(string src, IrOp op)
    {
        // 右辺が FLOAT の式のとき、左辺の整数は右辺を求める前に FLOAT にする (後から変換すると、右辺の値を変換してしまう)
        var ops = MainOps(Generate($"VAR I; FLOAT A; FLOAT B; MAIN() BEGIN {src} END;").Module);
        int load = ops.FindIndex(o => o.Op == IrOp.LoadVar && o.Src1.Name == "_V_I");
        Assert.True(load >= 0, src);
        var conv = ops[load + 1];
        Assert.True(conv.Op == IrOp.Call && conv.Src1.Name == "i16tof24", src);
        if (!src.Contains("++"))   // A++ は増分の結果を A に入れるときにも変換する
            Assert.Equal(1, ops.Count(o => o.Op == IrOp.Call && o.Src1.Name == "i16tof24"));
        var outer = ops.Last(o => o.Op == op);
        Assert.Equal(3, outer.DataSize);                       // FLOAT の演算
        Assert.Equal(conv.Dest, outer.Src1);                   // 左辺は変換した値
    }

    [Fact]
    public void MixedExpression_AsmConvertsSavesAndRestoresLeft()
    {
        // ASM でも、I を読んですぐ i16tof24 で FLOAT にして積んでおき、A*A を求めてから戻して f24add に渡す
        // (直す前は I を読んだ直後に A を読み、A*A の後で A*A の値を変換していた)
        var asm = MainAsm("VAR I; FLOAT A; FLOAT B; MAIN() BEGIN B=I+(A*A); END;");
        var lines = asm.Split('\n').Select(l => l.Trim()).Where(l => l.Length > 0 && !l.StartsWith(";")).ToArray();
        Assert.Equal(new[]
        {
            "LD\tHL,(_V_I)", "CALL\ti16tof24", "PUSH\tAF", "PUSH\tHL",          // 左辺を FLOAT にして積む
            "LD\tHL,(_V_A)", "LD\tA,(_V_A+2)", "CALL\tf24sqr",                 // 右辺 A*A
            "LD\tC,A", "EX\tDE,HL", "POP\tHL", "POP\tAF", "CALL\tf24add",      // 右辺を CDE、左辺を AHL に戻して足す
            "LD\t(_V_B),HL", "LD\t(_V_B+2),A",
        }, lines);
    }

    [Fact]
    public void MixedExpression_PrefixIncrementIsNotFloat()
    {
        // 前置の増分は結果の一時値に型を付けない (整数) ので、左辺を先に FLOAT にしない (後置との区別)
        var ops = MainOps(Generate("VAR I; FLOAT A; FLOAT B; MAIN() BEGIN B=I+(++A); END;").Module);
        int load = ops.FindIndex(o => o.Op == IrOp.LoadVar && o.Src1.Name == "_V_I");
        Assert.False(ops[load + 1].Op == IrOp.Call && ops[load + 1].Src1.Name == "i16tof24");
    }

    [Theory]
    [InlineData("VAR A; A=3; B=I+(A*A);")]       // 局所の整数 A は大域の FLOAT A より先 (f24sqr にしない)
    [InlineData("VAR A; A=3; B=I+(2*A);")]       // 同じく f24mul2 にしない
    public void MulSpecialCase_RespectsLocalShadowing(string body)
    {
        var ops = MainOps(Generate($"FLOAT A; VAR I; FLOAT B; MAIN() BEGIN {body} END;").Module);
        Assert.DoesNotContain(ops, o => o.Op == IrOp.Call && o.Src1.Name is "f24sqr" or "f24mul2");
        Assert.Equal(2, ops.Single(o => o.Op == IrOp.Mul).DataSize);
        Assert.Equal(2, ops.Single(o => o.Op == IrOp.Add).DataSize);   // I+(整数) は整数のまま (FLOAT へは代入のとき)
    }

    [Fact]
    public void MulSpecialCase_GlobalFloatScalarStillUsed()
    {
        // 隠されていない大域の FLOAT のスカラーは、今までどおり f24sqr・f24mul2 (3 バイトの BYTE 配列は対象外)
        var ops = MainOps(Generate("FLOAT A; FLOAT B; ARRAY BYTE G[2]; VAR C; MAIN() BEGIN B=A*A; B=2*A; C=2*G; END;").Module);
        Assert.Contains(ops, o => o.Op == IrOp.Call && o.Src1.Name == "f24sqr");
        Assert.Single(ops, o => o.Op == IrOp.Call && o.Src1.Name == "f24mul2");
    }

    [Fact]
    public void Identifier_ResolvedLikeAccept()
    {
        // 局所変数は大域の同じ名前より先 (局所の VAR A は整数)。局所の配列は 3 バイトでもアドレス (整数)
        foreach (var body in new[] { "VAR A; A=126; IF I+(A+2) THEN C=1;", "ARRAY BYTE L[2]; C=I+(L+1);" })
        {
            var ops = MainOps(Generate($"FLOAT A; VAR I; VAR C; MAIN() BEGIN {body} END;").Module);
            Assert.DoesNotContain(ops, o => o.Op == IrOp.Call && o.Src1.Name == "i16tof24");
            Assert.All(ops.Where(o => o.Op == IrOp.Add), o => Assert.Equal(2, o.DataSize));
        }
    }

    [Theory]
    [InlineData("IF I+(64.0*2) THEN C=1;")]   // 64.0*2 は整数 128 に畳み込まれるので、加算は整数のまま
    [InlineData("C=I+(1.0*2);")]
    public void MixedExpression_IntegerFoldStaysInteger(string src)
    {
        var ops = MainOps(Generate($"VAR I; VAR C; MAIN() BEGIN {src} END;").Module);
        Assert.DoesNotContain(ops, o => o.Op == IrOp.Call && o.Src1.Name == "i16tof24");
        Assert.Equal(2, ops.Single(o => o.Op == IrOp.Add).DataSize);
    }

    [Fact]
    public void Fold_UnsignedRange()
    {
        // 32768〜65535 の結果: 今の値が正しければ今までどおり整数 (20000.0*2.0 は 40000)。
        // 今の値が違えば FLOAT (整数の 40000 は FLOAT に入れると -25536 になるため)
        var ops = MainOps(Generate("VAR C; FLOAT B; MAIN() BEGIN C=20000.0*2.0; B=(40000.0+40000.0)/2.0; END;").Module);
        Assert.Contains(ops, o => o.Op == IrOp.LoadConst && o.Src1.ImmediateValue == 40000);
        var f = ops.Single(o => o.Op == IrOp.LoadFloatConst);
        Assert.Equal((14464, 78), (f.Src1.ImmediateValue, f.Src2.ImmediateValue)); // 40000 の f24
        Assert.Equal(3, f.DataSize);
    }

    [Fact]
    public void NegatedFloatArithmetic_IsFloat()
    {
        var ops = MainOps(Generate("FLOAT B; MAIN() BEGIN B=-(5.0/2.0); END;").Module);
        var c = ops.Single(o => o.Op == IrOp.LoadFloatConst);
        Assert.Equal((16384, 64), (c.Src1.ImmediateValue, c.Src2.ImmediateValue)); // 2.5 の f24
        var neg = ops.Single(o => o.Op == IrOp.Neg);
        Assert.Equal(3, neg.DataSize);                         // FLOAT の符号反転
        Assert.Equal(c.Dest, neg.Src1);
    }

    [Theory]
    [InlineData("REPEAT C=1; UNTIL 1.0-1.0/2.0;")]   // 条件 (分岐は HL だけで判定するので FLOAT の 0.5 を持ち込まない)
    [InlineData("REPEAT C=1; UNTIL (C=1.0-1.0/2.0);")] // 条件の中の代入式も (印を付けるのは代入の文だけ)
    [InlineData("IF I<5.0/2.0 THEN C=1;")]           // 比較の辺も代入する値の外なので今までどおり
    [InlineData("C=I+&(5.0/2.0);")]                  // & の中
    [InlineData("C=I+&MEM[5.0/2.0];")]
    [InlineData("C=(5.0/2.0) AND 3;")]               // ビット演算の辺
    public void OutsideAssignedValue_KeepsIntegerFold(string body)
    {
        // 代入する値の外では、今までどおり整数として畳み込む (FLOAT の定数を作らない)
        var ops = MainOps(Generate($"VAR I; VAR C; MAIN() BEGIN {body} END;").Module);
        Assert.DoesNotContain(ops, o => o.Op == IrOp.LoadFloatConst);
        Assert.DoesNotContain(ops, o => o.Op == IrOp.Call && o.Src1.Name == "i16tof24");
    }

    [Theory]
    [InlineData("B=(262145.0-262144.0)/2.0;")]   // 262145.0 は f24 で 262144 になるので、実行時は 0
    [InlineData("B=1.0/3.0;")]                   // 1/3 は f24 で正確に表せない
    [InlineData("B=1.0+1.0/3.0;")]               // 1.0/3.0 を整数の 0 と見て足し算を消さない
    public void InexactInF24_IsComputedAtRuntime(string body)
    {
        // 定数や途中の値が f24 で正確に表せないときは、畳み込まずに実行時に f24 で計算する (実行時と同じ値になる)
        var ops = MainOps(Generate($"FLOAT B; MAIN() BEGIN {body} END;").Module);
        var div = ops.Single(o => o.Op == IrOp.Div);
        Assert.Equal(3, div.DataSize);
        Assert.DoesNotContain(ops, o => o.Op == IrOp.LoadConst);
        Assert.All(ops.Where(o => o.Op is IrOp.Add or IrOp.Sub), o => Assert.Equal(3, o.DataSize));
    }

    [Theory]
    [InlineData("B=(1.0+1.0)/(2.0+1.0);", 0, 64, 32768, 64)]       // 2.0 / 3.0 (整数の 2/3 にしない)
    [InlineData("B=(20000.0*2.0)/3.0;", 14464, 78, 32768, 64)]     // 40000.0 / 3.0 (整数の 40000 は FLOAT にすると負になる)
    public void RuntimeOperation_KeepsFloatOperands(string body, int lm, int le, int rm, int re)
    {
        // 実行時に計算する演算の辺の定数の演算は、整数の結果でも FLOAT の定数にする
        var ops = MainOps(Generate($"FLOAT B; MAIN() BEGIN {body} END;").Module);
        var div = ops.Single(o => o.Op == IrOp.Div);
        Assert.Equal(3, div.DataSize);
        var l = ops.Single(o => o.Dest == div.Src1);
        var r = ops.Single(o => o.Dest == div.Src2);
        Assert.Equal((IrOp.LoadFloatConst, lm, le), (l.Op, (int)l.Src1.ImmediateValue, (int)l.Src2.ImmediateValue));
        Assert.Equal((IrOp.LoadFloatConst, rm, re), (r.Op, (int)r.Src1.ImmediateValue, (int)r.Src2.ImmediateValue));
    }

    [Theory]
    [InlineData("MEM[$3000]=5.0/2.0;")]   // MEM への代入は FLOAT からの変換がない
    [InlineData("PORT[$10]=5.0/2.0;")]    // PORT も同じ
    [InlineData("B=C=5.0/2.0;")]          // WORD を経由する連鎖代入 (C への FTOI が値を変えてから B に渡る)
    [InlineData("C=B=5.0/2.0;")]          // 代入先がすべて FLOAT のときだけ FLOAT の値を作る
    [InlineData("C=5.0/2.0;")]            // WORD の変数へは今までどおり整数の 2
    public void NonFloatTarget_DoesNotCreateFloatValue(string body)
    {
        var ops = MainOps(Generate($"VAR C; FLOAT B; MAIN() BEGIN {body} END;").Module);
        Assert.DoesNotContain(ops, o => o.Op == IrOp.LoadFloatConst);
        Assert.Contains(ops, o => o.Op == IrOp.LoadConst && o.Src1.ImmediateValue == 2);
    }

    [Theory]
    [InlineData("B=D=5.0/2.0;")]          // 代入先がすべて FLOAT の連鎖代入
    [InlineData("FA[1]=5.0/2.0;")]        // FLOAT の配列の要素
    public void FloatTarget_CreatesFloatValue(string body)
    {
        var ops = MainOps(Generate($"FLOAT B; FLOAT D; ARRAY FLOAT FA[3]; MAIN() BEGIN {body} END;").Module);
        var c = ops.Single(o => o.Op == IrOp.LoadFloatConst);
        Assert.Equal((16384, 64), ((int)c.Src1.ImmediateValue, (int)c.Src2.ImmediateValue));  // 2.5
        Assert.DoesNotContain(ops, o => o.Op == IrOp.LoadConst);
    }

    [Fact]
    public void NonConstantNormalization_IsUnchanged()
    {
        // 定数でない演算の何もしない演算の正規化は今までどおり (B=I*1.0 は I を変換して入れる)
        var ops = MainOps(Generate("VAR I; FLOAT B; MAIN() BEGIN B=I*1.0; END;").Module);
        Assert.DoesNotContain(ops, o => o.Op == IrOp.Mul);
    }

    [Fact]
    public void IntegerContexts_AreUnchanged()
    {
        // 整数が要る所 (IF の定数の条件・配列の大きさ) は、今までどおり整数の評価
        var ir = MainIr(Generate("VAR C; MAIN() BEGIN IF 1.0+1.0 THEN C=1; END;").Ir);
        Assert.DoesNotContain("JumpIfZero", ir);       // 定数の真 (f24 の 2.0 を HL で判定しない)
        var (_, _, module) = Generate("ARRAY BYTE A[64.0*2]; CONST Y=64.0*2; ARRAY BYTE B[Y+1]; MAIN() BEGIN END;");
        Assert.Equal(129, module.GlobalVars.Single(v => v.Name == "A").ByteSize);
        Assert.Equal(130, module.GlobalVars.Single(v => v.Name == "B").ByteSize);
    }

    [Fact]
    public void Const_IsUnchanged()
    {
        // CONST は今までどおり (直さない)。値を変えると、その CONST を参照するほかの CONST の評価が変わり、FLOAT の値を扱えない所
        // (MEM・PORT への代入・条件など) に FLOAT の値が届くことがあるため
        var (_, sym, _) = Generate(
            "CONST X=5.0/2.0; CONST FLDIV=64.0; CONST Y=64.0*2; CONST S=6.0/4.0*2.0; CONST V=40000.0+40000.0; CONST Q=1.5*2; " +
            "MAIN() BEGIN END;");
        Assert.Equal(2, sym.Resolve("X")!.ConstValue);
        Assert.Equal(64, sym.Resolve("FLDIV")!.ConstValue);
        Assert.Equal(128, sym.Resolve("Y")!.ConstValue);
        Assert.Equal(2, sym.Resolve("S")!.ConstValue);
        Assert.Equal(14464, sym.Resolve("V")!.ConstValue);
        Assert.Equal(3.0, sym.Resolve("Q")!.ConstFloatValue);
    }

    [Theory]
    [InlineData("MEM[$3000]=X;")]
    [InlineData("B=C=X;")]
    [InlineData("REPEAT C=1; UNTIL H;")]
    [InlineData("REPEAT C=1; UNTIL Z;")]                 // 参照する CONST (K) の値も変えないので、Z の評価も今までどおり
    [InlineData("MEM[$3000]=1.0/(7.0/2.0-K);")]
    public void Const_DoesNotBecomeFloat(string body)
    {
        // CONST は今までどおり整数なので、MEM への代入・WORD を経由する連鎖代入・条件に、CONST から FLOAT の値が届かない
        var src = "CONST X=5.0/2.0; CONST H=1.0-1.0/2.0; CONST K=6.0/4.0*2.0; CONST Z=1.0/(7.0/2.0-K); VAR C; FLOAT B; " +
                  $"MAIN() BEGIN {body} END;";
        var ops = MainOps(Generate(src).Module);
        Assert.DoesNotContain(ops, o => o.Op == IrOp.LoadFloatConst);
    }

    [Fact]
    public void ConstEvaluator_InvolvesFloat()
    {
        var (_, sym, _) = Generate("CONST Q=1.5*2; CONST N=3; MAIN() BEGIN END;");
        var ce = new ConstEvaluator(sym);
        var span = default(SLANGCompiler.Lexer.SourceSpan);
        var q = new SLANGCompiler.Parser.Ast.IdentifierExpr("Q", span);
        var n = new SLANGCompiler.Parser.Ast.IdentifierExpr("N", span);
        var f = new SLANGCompiler.Parser.Ast.FloatLiteral(1.0, span);
        var i = new SLANGCompiler.Parser.Ast.IntegerLiteral(1, span);
        Assert.True(ce.InvolvesFloat(q));
        Assert.False(ce.InvolvesFloat(n));
        Assert.True(ce.InvolvesFloat(f));
        Assert.False(ce.InvolvesFloat(i));
        Assert.True(ce.InvolvesFloat(new SLANGCompiler.Parser.Ast.UnaryExpr(SLANGCompiler.Parser.Ast.UnaryOp.Negate, f, span)));
        Assert.False(ce.InvolvesFloat(new SLANGCompiler.Parser.Ast.UnaryExpr(SLANGCompiler.Parser.Ast.UnaryOp.Negate, i, span)));
        Assert.True(ce.InvolvesFloat(new SLANGCompiler.Parser.Ast.BinaryExpr(SLANGCompiler.Parser.Ast.BinaryOp.Add, i, f, span)));
        Assert.False(ce.InvolvesFloat(new SLANGCompiler.Parser.Ast.BinaryExpr(SLANGCompiler.Parser.Ast.BinaryOp.Add, i, n, span)));
        Assert.True(ce.InvolvesFloat(new SLANGCompiler.Parser.Ast.ConditionalExpr(i, n, f, span)));
        Assert.False(ce.InvolvesFloat(new SLANGCompiler.Parser.Ast.ConditionalExpr(i, n, i, span)));
        Assert.True(ce.InvolvesFloat(new SLANGCompiler.Parser.Ast.HighLowExpr(true, f, span)));
        Assert.True(ce.InvolvesFloat(new SLANGCompiler.Parser.Ast.CastExpr(SLANGCompiler.Parser.Ast.DataSize.Float, n, span)));
        Assert.False(ce.InvolvesFloat(new SLANGCompiler.Parser.Ast.CastExpr(SLANGCompiler.Parser.Ast.DataSize.Word, n, span)));
    }
}
