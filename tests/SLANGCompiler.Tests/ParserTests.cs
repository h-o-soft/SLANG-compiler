using Xunit;
using SLANGCompiler.Lexer;
using SLANGCompiler.Parser;
using SLANGCompiler.Parser.Ast;

namespace SLANGCompiler.Tests;

public class ParserTests
{
    private CompilationUnit Parse(string source)
    {
        var lexer = new Lexer.Lexer(source);
        var tokens = lexer.Tokenize();
        var diag = new DiagnosticBag();
        var parser = new Parser.Parser(tokens, diag);
        var ast = parser.ParseCompilationUnit();
        Assert.False(diag.HasErrors, string.Join("\n", diag.Diagnostics));
        return ast;
    }

    [Fact]
    public void SimpleFunction()
    {
        var ast = Parse("MAIN() BEGIN END;");
        Assert.Single(ast.Definitions);
        var func = Assert.IsType<FuncDef>(ast.Definitions[0]);
        Assert.Equal("MAIN", func.Name);
        Assert.Empty(func.Parameters);
    }

    [Fact]
    public void FunctionWithParams()
    {
        var ast = Parse("ADD(A, B) BEGIN RETURN(A+B); END;");
        var func = Assert.IsType<FuncDef>(ast.Definitions[0]);
        Assert.Equal(2, func.Parameters.Count);
        Assert.Equal("A", func.Parameters[0].Name);
        Assert.Equal("B", func.Parameters[1].Name);
    }

    [Fact]
    public void VarDeclaration()
    {
        var ast = Parse("VAR X, Y; VAR BYTE Z;");
        Assert.Equal(2, ast.Definitions.Count);
    }

    [Fact]
    public void ArrayDeclaration()
    {
        var ast = Parse("ARRAY BYTE BUF[10]; ARRAY WORD T[5][3];");
        Assert.Equal(2, ast.Definitions.Count);
        var arr1 = Assert.IsType<ArrayDecl>(ast.Definitions[0]);
        Assert.Equal("BUF", arr1.Name);
        Assert.Equal(DataSize.Byte, arr1.Size);
        Assert.Single(arr1.Dimensions);

        var arr2 = Assert.IsType<ArrayDecl>(ast.Definitions[1]);
        Assert.Equal(2, arr2.Dimensions.Count);
    }

    [Fact]
    public void ConstDeclaration()
    {
        var ast = Parse("CONST MAX=100, MIN=0;");
        // CONST複数宣言はBlockとして返される
        var block = Assert.IsType<Block>(ast.Definitions[0]);
        Assert.Equal(2, block.Statements.Count);
    }

    [Fact]
    public void IfStatement()
    {
        var ast = Parse("MAIN() BEGIN IF X==1 THEN Y=2; ELIF X==3 THEN Y=4; ELSE Y=5; END;");
        var func = Assert.IsType<FuncDef>(ast.Definitions[0]);
        var ifStmt = Assert.IsType<IfStmt>(func.Body.Statements[0]);
        Assert.Equal(2, ifStmt.Branches.Count);
        Assert.NotNull(ifStmt.ElseBody);
    }

    [Fact]
    public void WhileLoop()
    {
        var ast = Parse("MAIN() BEGIN WHILE X>0 { X--; } END;");
        var func = Assert.IsType<FuncDef>(ast.Definitions[0]);
        Assert.IsType<WhileStmt>(func.Body.Statements[0]);
    }

    [Fact]
    public void ForLoop()
    {
        var ast = Parse("MAIN() BEGIN FOR I=0 TO 10 PRINT(I); END;");
        var func = Assert.IsType<FuncDef>(ast.Definitions[0]);
        var forStmt = Assert.IsType<ForStmt>(func.Body.Statements[0]);
        Assert.Equal("I", forStmt.Variable);
        Assert.False(forStmt.IsDownTo);
    }

    [Fact]
    public void CaseStatement()
    {
        var ast = Parse("MAIN() BEGIN CASE X { 1: Y=1; 2 TO 5: Y=2; OTHERS: Y=0; } END;");
        var func = Assert.IsType<FuncDef>(ast.Definitions[0]);
        var caseStmt = Assert.IsType<CaseStmt>(func.Body.Statements[0]);
        Assert.Equal(3, caseStmt.Branches.Count);
    }

    [Fact]
    public void MachineDeclaration()
    {
        var ast = Parse("MACHINE MSUB(2):$C000;");
        var machine = Assert.IsType<MachineDecl>(ast.Definitions[0]);
        Assert.Equal("MSUB", machine.Name);
        Assert.Equal(2, machine.ParamCount);
    }

    [Fact]
    public void PrintStatement()
    {
        var ast = Parse("MAIN() BEGIN PRINT(\"Hello\", X, /); END;");
        var func = Assert.IsType<FuncDef>(ast.Definitions[0]);
        var print = Assert.IsType<PrintStmt>(func.Body.Statements[0]);
        Assert.Equal(3, print.Arguments.Count);
    }

    [Fact]
    public void ArrayAccess()
    {
        var ast = Parse("MAIN() BEGIN X = ARR[I]; ARR[1][2] = 3; END;");
        var func = Assert.IsType<FuncDef>(ast.Definitions[0]);
        Assert.Equal(2, func.Body.Statements.Count);
    }

    // === Error Recovery Tests ===

    private (CompilationUnit Ast, DiagnosticBag Diag) ParseWithErrors(string source)
    {
        var lexer = new Lexer.Lexer(source);
        var tokens = lexer.Tokenize();
        var diag = new DiagnosticBag();
        var parser = new Parser.Parser(tokens, diag);
        var ast = parser.ParseCompilationUnit();
        return (ast, diag);
    }

    [Fact]
    public void ErrorRecovery_SuppressesChainErrors()
    {
        var (ast, diag) = ParseWithErrors("VAR ; VAR X; MAIN() BEGIN X=; PRINT(\"OK\",/); END;");
        Assert.True(diag.HasErrors);
        Assert.True(diag.ErrorCount <= 4, $"Too many errors ({diag.ErrorCount}): chain errors not suppressed");
    }

    [Fact]
    public void ErrorRecovery_ContinuesAfterSyntaxError()
    {
        var (ast, diag) = ParseWithErrors("MAIN() BEGIN X=; PRINT(\"OK\",/); END;");
        Assert.True(diag.HasErrors);
        var main = ast.Definitions.OfType<FuncDef>().FirstOrDefault(f => f.Name == "MAIN");
        Assert.NotNull(main);
        // PRINT文がASTに残っていること
        Assert.Contains(main.Body.Statements, s => s is PrintStmt);
    }

    [Fact]
    public void ErrorLimit_StopsAt30()
    {
        var diag = new DiagnosticBag();
        for (int i = 0; i < 50; i++)
            diag.Error($"error {i}", default);
        Assert.Equal(DiagnosticBag.MaxErrors, diag.ErrorCount);
    }

    [Fact]
    public void ErrorRecovery_ModuleBlock()
    {
        // #MODULE内のエラーでも同期リカバリが効くこと
        var (ast, diag) = ParseWithErrors(
            "MAIN() BEGIN END; #MODULE $8000 VAR ; SUB() BEGIN PRINT(\"OK\",/); END; #END");
        Assert.True(diag.HasErrors);
        // SUB関数がASTに残っていること（モジュール内のリカバリが効いている）
        Assert.True(ast.Definitions.Count >= 2);
    }

    [Fact]
    public void ErrorLimit_ParserStopsEarly()
    {
        // 大量エラーのソースをparserに食わせて30件で打ち切られること
        var sb = new System.Text.StringBuilder();
        sb.Append("MAIN() BEGIN ");
        for (int i = 0; i < 50; i++)
            sb.Append("=; ");  // 各行がエラー
        sb.Append("END;");
        var (ast, diag) = ParseWithErrors(sb.ToString());
        Assert.True(diag.HasErrors);
        Assert.True(diag.ErrorCount <= DiagnosticBag.MaxErrors,
            $"Expected at most {DiagnosticBag.MaxErrors} errors, got {diag.ErrorCount}");
    }
}
