using Xunit;
using SLANGCompiler.Lexer;
using SLANGCompiler.Parser;
using SLANGCompiler.IR;
using SLANGCompiler.CodeGen;
using SLANGCompiler.Semantics;

namespace SLANGCompiler.Tests;

public class CodeGenTests
{
    private string Compile(string source)
    {
        var diag = new DiagnosticBag();
        var lexer = new Lexer.Lexer(source);
        var tokens = lexer.Tokenize();
        var preprocessor = new Preprocessor(diag);
        tokens = preprocessor.Process(tokens, ".");
        var parser = new Parser.Parser(tokens, diag);
        var ast = parser.ParseCompilationUnit();
        Assert.False(diag.HasErrors, $"Parse errors: {string.Join("; ", diag.Diagnostics)}");

        var analyzer = new SemanticAnalyzer(diag);
        analyzer.Analyze(ast);

        var irGen = new IrGenerator(diag, analyzer.Symbols);
        var irModule = irGen.Generate(ast);
        Assert.False(diag.HasErrors, $"IR errors: {string.Join("; ", diag.Diagnostics)}");

        var codeGen = new CodeGenerator(irModule);
        return codeGen.Generate();
    }

    [Fact]
    public void SimpleAddition_DirectLoad()
    {
        var asm = Compile("VAR X,Y,Z; MAIN() BEGIN Z=X+Y; END;");
        // 直接ロード最適化: PUSH/POP なし
        Assert.Contains("LD\tHL,(_X)", asm);
        Assert.Contains("LD\tDE,(_Y)", asm);
        Assert.Contains("ADD\tHL,DE", asm);
        Assert.DoesNotContain("PUSH\tHL", asm.Split("MAIN:")[1].Split("_MAIN_EXIT")[0]);
    }

    [Fact]
    public void ConstantFolding()
    {
        var asm = Compile("CONST MAX=100; MAIN() BEGIN VAR X; X=MAX; END;");
        // CONST値が即値になる
        Assert.Contains("$0064", asm); // 100 = $64
    }

    [Fact]
    public void LocalVariable_IYAccess()
    {
        var asm = Compile("MAIN() BEGIN VAR LOCAL; LOCAL=42; END;");
        // ローカル変数はIYオフセットアクセス
        Assert.Contains("(IY+", asm);
    }

    [Fact]
    public void GlobalVariable_WorkArea()
    {
        var asm = Compile("VAR GLOBAL; MAIN() BEGIN GLOBAL=1; END;");
        // グローバル変数は__WORK__内にEQU配置
        Assert.Contains("_GLOBAL EQU (__WORK__", asm);
    }

    [Fact]
    public void GlobalInit()
    {
        var asm = Compile("VAR X=42; MAIN() BEGIN END;");
        // エントリポイントでグローバル初期化
        var entry = asm.Split("MAIN:")[0];
        Assert.Contains("$002A", entry); // 42
        Assert.Contains("(_X)", entry);
    }

    [Fact]
    public void StringTable()
    {
        var asm = Compile("MAIN() BEGIN PRINT(\"Hello\"); END;");
        Assert.Contains("_S0:", asm);
        Assert.Contains("DB\t\"Hello\",0", asm);
    }

    [Fact]
    public void FunctionCall_IYArgs()
    {
        var asm = Compile("MAIN() BEGIN ADD(1,2); END; ADD(A,B) BEGIN END;");
        // ユーザー関数呼び出し: IY+$70に引数
        Assert.Contains("(IY+$70)", asm);
        Assert.Contains("(IY+$72)", asm);
        Assert.Contains("CALL\tADD", asm);
    }

    [Fact]
    public void ArrayInit_DBOutput()
    {
        var asm = Compile("ARRAY BYTE D[2]={10,20,30}; MAIN() BEGIN END;");
        Assert.Contains("DB\t$0A,$14,$1E", asm);
    }

    [Fact]
    public void EntryPoint()
    {
        var asm = Compile("MAIN() BEGIN END;");
        Assert.Contains("LD\tIY,__IYWORK", asm);
        Assert.Contains("CALL\tMAIN", asm);
        Assert.Contains("__IYWORK EQU (__WORK__", asm);
        Assert.Contains("SLANG_PROG_END:", asm);
        Assert.Contains("__WORK__:", asm);
        Assert.Contains("__WORKEND__", asm);
    }

    [Fact]
    public void OrgDirective()
    {
        var asm = Compile("ORG $8000; MAIN() BEGIN END;");
        Assert.Contains("ORG\t$8000", asm);
    }

    [Fact]
    public void PrintString_CallsPMSX()
    {
        var asm = Compile("MAIN() BEGIN PRINT(\"Hello\"); END;");
        Assert.Contains("CALL\tPMSX", asm);
        Assert.DoesNotContain("CALL\tPSTR", asm);
    }

    [Fact]
    public void PrintNumber_CallsP10()
    {
        var asm = Compile("VAR X; MAIN() BEGIN PRINT(X); END;");
        Assert.Contains("CALL\tP10", asm);
    }

    [Fact]
    public void LocalArray_IYFrameAdjustment()
    {
        var asm = Compile("MAIN() BEGIN ARRAY A[5]; A[0]=1; END;");
        var mainBody = asm.Split("MAIN:")[1].Split("_MAIN_EXIT")[0];
        Assert.Contains("ADD\tIY,BC", mainBody);
    }

    [Fact]
    public void Arithmetic_CallsMULHLDE()
    {
        var asm = Compile("VAR X,Y,Z; MAIN() BEGIN Z=X*Y; END;");
        Assert.Contains("CALL\tMULHLDE", asm);
    }

    [Fact]
    public void Arithmetic_CallsDIVHLDE()
    {
        var asm = Compile("VAR X,Y,Z; MAIN() BEGIN Z=X/Y; END;");
        Assert.Contains("CALL\tDIVHLDE", asm);
    }

    [Fact]
    public void Shift_CallsLSHIFTHLDE()
    {
        var asm = Compile("VAR X,Y,Z; MAIN() BEGIN Z=X<<Y; END;");
        Assert.Contains("CALL\tLSHIFTHLDE", asm);
    }

    [Fact]
    public void SignedCompare_CallsOPSLTHLDE()
    {
        var asm = Compile("VAR X,Y,Z; MAIN() BEGIN IF X.<.Y THEN Z=1; END;");
        Assert.Contains("CALL\tOPSLTHLDE", asm);
    }

    [Fact]
    public void PrintNewline_CallsPCRONE()
    {
        var asm = Compile("MAIN() BEGIN PRINT(/); END;");
        Assert.Contains("CALL\tPCRONE", asm);
    }
}
