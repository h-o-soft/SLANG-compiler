using System.Text.RegularExpressions;
using Xunit;
using SLANGCompiler.Lexer;
using SLANGCompiler.Parser;
using SLANGCompiler.IR;
using SLANGCompiler.CodeGen;
using SLANGCompiler.Semantics;

namespace SLANGCompiler.Tests;

/// <summary>
/// VAR の固定アドレス (ラベル・CONST・式) と、間接変数の初期値
/// </summary>
public class VarDeclTests
{
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

    private static string CompileOk(string source)
    {
        var (asm, diag) = Compile(source);
        Assert.False(diag.HasErrors, string.Join("; ", diag.Diagnostics));
        return asm!;
    }

    private const string Asm = "#ASM\nBYTECOUNTER: DB 0\n#END\n";

    [Theory]
    // 整数のリテラル以外の番地も固定アドレスにする (いままでは黙って __WORK__ に置いていた)
    [InlineData("VAR BYTE BC:BYTECOUNTER;", "_V_BC", "BYTECOUNTER")]
    [InlineData("VAR BC:BYTECOUNTER + 1;", "_V_BC", "BYTECOUNTER+$0001")]
    [InlineData("CONST BASE = $C000; VAR BC:BASE + 4;", "_V_BC", "$C004")]
    [InlineData("CONST BASE = $C000, OTHER = $10; VAR BC:BASE-(OTHER+2);", "_V_BC", "$BFEE")]
    [InlineData("VAR BC:$C000;", "_V_BC", "$C000")]
    public void VarAddress_LabelAndExpressions(string decl, string label, string equ)
    {
        var asm = CompileOk($"{decl} MAIN() BEGIN BC = 1; END;\n{Asm}");
        Assert.Contains($"{label}\tEQU\t{equ}\n", asm);
        Assert.DoesNotMatch($@"{label} EQU \(__WORK__", asm);
    }

    [Fact]
    public void AsmAddress_KeepsGrouping()
    {
        // A-(B+C) の右を括弧でくくる (並べると A-B+C になる)。ARRAY の番地も同じ
        var asm = CompileOk($"CONST OTHER = $10; ARRAY BYTE T[1]:BYTECOUNTER-(OTHER+2); MAIN() BEGIN T[0] = 1; END;\n{Asm}");
        Assert.Contains("_V_T\tEQU\tBYTECOUNTER-($0010+$0002)\n", asm);
    }

    [Theory]
    // 番地と初期値を両方書ける (いままでは = 7 が番地の式に取り込まれ、黙って __WORK__ に置いていた)
    [InlineData("VAR X:$C000 = 7;", "$C000")]
    [InlineData("VAR X:BYTECOUNTER = 7;", "BYTECOUNTER")]
    public void VarAddressWithInitializer(string decl, string equ)
    {
        var asm = CompileOk($"{decl} MAIN() BEGIN X = X + 1; END;\n{Asm}");
        Assert.Contains($"_V_X\tEQU\t{equ}\n", asm);
        Assert.Matches(@"LD\tHL,\$0007\s*\n\s*LD\t\(_V_X\),HL", asm);
    }

    [Fact]
    public void PointerInitializer_Global()
    {
        // VAR P[] = 番地: 変数そのもの (指す番地) の初期値。起動時に代入する
        var asm = CompileOk("CONST BASE = $C000; VAR P[] = $C000; VAR BYTE Q[] = BASE + 2; MAIN() BEGIN P[0] = 1; Q[0] = 2; END;");
        Assert.Matches(@"LD\tHL,\$C000\s*\n\s*LD\t\(_V_P\),HL", asm);
        Assert.Matches(@"LD\tHL,\$C002\s*\n\s*LD\t\(_V_Q\),HL", asm);
    }

    [Fact]
    public void PointerInitializer_Local()
    {
        // 局所の間接変数の初期値は、宣言の所で書く (単純変数の局所の初期値と同じ)
        var asm = CompileOk("FN() BEGIN VAR L[] = $D000; L[0] = 1; END; MAIN() BEGIN FN(); END;");
        var body = asm.Split("\nFN:")[1].Split("_FN_EXIT")[0];
        Assert.Matches(@"LD\t\(IY\+\$\w\w\),\$00\s*\n\s*LD\t\(IY\+\$\w\w\),\$D0", body);
    }

    [Theory]
    [InlineData("VAR T[3] = 5;", "with a size cannot have a single-value initializer")]
    [InlineData("ARRAY T[3] = 5;", "with a size cannot have a single-value initializer")]
    [InlineData("VAR X:\"ABC\";", "Unsupported expression")]
    public void Errors(string decl, string message)
    {
        var (_, diag) = Compile($"{decl} MAIN() BEGIN END;");
        Assert.Contains(diag.Diagnostics, d => d.Message.Contains(message));
    }
}
