using SLANGCompiler;
using SLANGCompiler.Lexer;
using SLANGCompiler.Parser;
using SLANGCompiler.Parser.Ast;
using SLANGCompiler.Semantics;
using Xunit;

namespace SLANGCompiler.Tests;

/// <summary>
/// CFUNC 宣言 (Commit 1 範囲: parser + semantic + Z80 backend error)。
/// C backend (CEmitter) の extern 出力 / 呼び出し変換は Commit 2 で別ファイル追加予定。
/// </summary>
public class CFuncDeclTests
{
    /// <summary>SLANG ソースを Lexer → Preprocessor → Parser → SemanticAnalyzer まで通す</summary>
    private static (CompilationUnit Ast, DiagnosticBag Diag, SymbolTable Symbols) ParseAndAnalyze(string source)
    {
        var diag = new DiagnosticBag();
        var lexer = new Lexer.Lexer(source, "<test>");
        var tokens = lexer.Tokenize();
        var preproc = new Preprocessor(diag, new List<string>());
        preproc.DefineConst("BACKEND", 1);
        preproc.DefineConst("ENV_TYPE", 7);
        preproc.DefineConst("OS_TYPE", 6);
        tokens = preproc.Process(tokens, ".");
        var parser = new Parser.Parser(tokens, diag);
        var ast = parser.ParseCompilationUnit();
        if (!diag.HasErrors)
        {
            var analyzer = new SemanticAnalyzer(diag);
            analyzer.Analyze(ast);
            return (ast, diag, analyzer.Symbols);
        }
        return (ast, diag, new SymbolTable());
    }

    private static CFuncDecl? FindCFuncDecl(CompilationUnit unit, string name)
    {
        foreach (var def in unit.Definitions)
        {
            if (def is CFuncDecl cf && cf.Name == name) return cf;
            if (def is Block b)
                foreach (var inner in b.Statements)
                    if (inner is CFuncDecl cf2 && cf2.Name == name) return cf2;
        }
        return null;
    }

    // === C1: 略式 CFUNC FOO(2):foo; ===

    [Fact]
    public void C1_Shortform_TwoParams_Registered()
    {
        var (ast, diag, syms) = ParseAndAnalyze("CFUNC FOO(2):foo;\nMAIN() {}\n");
        Assert.False(diag.HasErrors);

        var decl = FindCFuncDecl(ast, "FOO");
        Assert.NotNull(decl);
        Assert.Equal("foo", decl!.CName);
        Assert.Equal(2, decl.ParamCount);
        Assert.Null(decl.Parameters);          // 略式
        Assert.Null(decl.ReturnSize);          // WORD assumed
        Assert.False(decl.IsVoidReturn);

        var sym = syms.GlobalScope.Resolve("FOO");
        Assert.NotNull(sym);
        Assert.Equal(SymbolKind.CFunction, sym!.Kind);
        Assert.Equal("foo", sym.CName);
        var ft = Assert.IsType<FunctionType>(sym.Type);
        Assert.Equal(SlangType.Word, ft.ReturnType);
        Assert.Equal(2, ft.ParameterTypes.Count);
        Assert.All(ft.ParameterTypes, t => Assert.Equal(SlangType.Word, t));
    }

    // === C2: 略式 0 引数 ===

    [Fact]
    public void C2_Shortform_ZeroParams()
    {
        var (ast, diag, syms) = ParseAndAnalyze("CFUNC BAR(0):bar;\nMAIN() {}\n");
        Assert.False(diag.HasErrors);

        var decl = FindCFuncDecl(ast, "BAR");
        Assert.NotNull(decl);
        Assert.Equal(0, decl!.ParamCount);

        var sym = syms.GlobalScope.Resolve("BAR");
        var ft = Assert.IsType<FunctionType>(sym!.Type);
        Assert.Empty(ft.ParameterTypes);
    }

    // === C3: 型あり VOID 引数 + VOID return ===

    [Fact]
    public void C3_Typed_VoidParams_VoidReturn()
    {
        var (_, diag, syms) = ParseAndAnalyze("CFUNC FOO() VOID :foo;\nMAIN() {}\n");
        Assert.False(diag.HasErrors);

        var sym = syms.GlobalScope.Resolve("FOO");
        var ft = Assert.IsType<FunctionType>(sym!.Type);
        Assert.Equal(SlangType.Void, ft.ReturnType);
        Assert.Empty(ft.ParameterTypes);
    }

    // === C4: 型あり BYTE return ===

    [Fact]
    public void C4_Typed_WordParam_ByteReturn()
    {
        var (_, diag, syms) = ParseAndAnalyze("CFUNC PEEK(WORD addr) BYTE :peek;\nMAIN() {}\n");
        Assert.False(diag.HasErrors);

        var sym = syms.GlobalScope.Resolve("PEEK");
        var ft = Assert.IsType<FunctionType>(sym!.Type);
        Assert.Equal(SlangType.Byte, ft.ReturnType);
        Assert.Single(ft.ParameterTypes);
        Assert.Equal(SlangType.Word, ft.ParameterTypes[0]);
    }

    // === C5: 配列ポインタ引数 ===

    [Fact]
    public void C5_Typed_ArrayPointerParam()
    {
        var (_, diag, syms) = ParseAndAnalyze("CFUNC SI(BYTE s[]) VOID :spr_init;\nMAIN() {}\n");
        Assert.False(diag.HasErrors);

        var sym = syms.GlobalScope.Resolve("SI");
        var ft = Assert.IsType<FunctionType>(sym!.Type);
        Assert.Equal(SlangType.Void, ft.ReturnType);
        Assert.Single(ft.ParameterTypes);
        var pt = Assert.IsType<PointerType>(ft.ParameterTypes[0]);
        Assert.Equal(SlangType.Byte, pt.ElementType);
    }

    // === C6: 複数宣言 ===

    [Fact]
    public void C6_MultipleDeclarations_CommaSeparated()
    {
        var (_, diag, syms) = ParseAndAnalyze(
            "CFUNC A(1):a, B(WORD x) BYTE :b;\nMAIN() {}\n");
        Assert.False(diag.HasErrors);

        var a = syms.GlobalScope.Resolve("A");
        var b = syms.GlobalScope.Resolve("B");
        Assert.Equal(SymbolKind.CFunction, a!.Kind);
        Assert.Equal("a", a.CName);
        Assert.Equal(SymbolKind.CFunction, b!.Kind);
        Assert.Equal("b", b.CName);
        var bft = Assert.IsType<FunctionType>(b.Type);
        Assert.Equal(SlangType.Byte, bft.ReturnType);
    }

    // === C7: 不正な C ident reject (regex 違反) ===
    // 注: parser は識別子 token をまず消費するので $ 等は lexer で別 token 扱い、
    // ":" の次が IDENT でない場合に "Expected identifier" で reject される。
    // 実用上は regex 違反 は parser 側で 起き得る前にエラー化されるため、
    // 厳密な regex check は将来 cName preprocessing で別途 evaluate する余地あり。
    // C7 はここでは識別子そのものが不正な case (e.g. begin token) として確認。

    [Fact]
    public void C7_NonIdentifierAfterColon_Rejects()
    {
        // ":foo bar" のように 2 識別子が並ぶケースは parser が次に ";" を期待するので
        // ", BAR(..." or ";" 期待で broken。MACHINE 文法と同じく ":" 直後 IDENT 1 個。
        var (_, diag, _) = ParseAndAnalyze("CFUNC FOO():$1234;\nMAIN() {}\n");
        Assert.True(diag.HasErrors);
        Assert.Contains(diag.Diagnostics, d => d.Message.Contains("C function name"));
    }

    // === C9: 戻り型 typo (識別子として解釈される) ===

    [Fact]
    public void C9_InvalidReturnType_RejectedAtColon()
    {
        // BTYE が無効な型 keyword → DataSize/VOID にマッチしない → 直後 ':' を期待するが
        // 識別子 BTYE が来て先頭 token として消費される → 「expected ':'」エラー
        var (_, diag, _) = ParseAndAnalyze("CFUNC FOO() BTYE :foo;\nMAIN() {}\n");
        Assert.True(diag.HasErrors);
    }

    // === C11: Z80 backend で CFUNC build → IR error ===

    [Fact]
    public void C11_Z80Backend_CFuncDecl_Errors()
    {
        var (ast, diag, syms) = ParseAndAnalyze("CFUNC FOO(2):foo;\nMAIN() {}\n");
        Assert.False(diag.HasErrors);

        // IrGenerator を直接呼ぶ
        var rt = new SLANGCompiler.Runtime.RuntimeManager();
        var irGen = new SLANGCompiler.IR.IrGenerator(diag, syms, rt);
        irGen.Generate(ast);

        Assert.True(diag.HasErrors);
        Assert.Contains(diag.Diagnostics, d =>
            d.Message.Contains("CFUNC") && d.Message.Contains("Z80"));
    }

    [Fact]
    public void C11b_Z80Backend_CFuncCall_Errors()
    {
        var (ast, diag, syms) = ParseAndAnalyze(
            "CFUNC FOO(WORD x) VOID :foo;\nMAIN() { FOO(5); }\n");
        // CFUNC 宣言 + 呼出が同じファイルに存在。SemanticAnalyzer 段階では error なし
        Assert.False(diag.HasErrors);

        var rt = new SLANGCompiler.Runtime.RuntimeManager();
        var irGen = new SLANGCompiler.IR.IrGenerator(diag, syms, rt);
        irGen.Generate(ast);

        Assert.True(diag.HasErrors);
        Assert.Contains(diag.Diagnostics, d => d.Message.Contains("CFUNC"));
    }

    // === C14: case preserve ===

    [Fact]
    public void C14_CName_CasePreserved()
    {
        var (_, diag, syms) = ParseAndAnalyze("CFUNC FOO() VOID :fooBar;\nMAIN() {}\n");
        Assert.False(diag.HasErrors);

        var sym = syms.GlobalScope.Resolve("FOO");
        Assert.Equal("fooBar", sym!.CName);   // case preserved!
    }

    // === C15: セミコロン必須 ===

    [Fact]
    public void C15_MissingSemicolon_Rejects()
    {
        var (_, diag, _) = ParseAndAnalyze("CFUNC FOO(1):foo\nMAIN() {}\n");
        Assert.True(diag.HasErrors);
        Assert.Contains(diag.Diagnostics, d => d.Message.Contains(";"));
    }

    // === 追加: PointerType 引数の signature が正しく届く ===

    [Fact]
    public void C16_TypedArrayPointer_AddressableViaSymbolTable()
    {
        var (_, diag, syms) = ParseAndAnalyze(
            "CFUNC SPR_INIT(BYTE screen[]) VOID :spr_init;\nMAIN() {}\n");
        Assert.False(diag.HasErrors);

        var sym = syms.GlobalScope.Resolve("SPR_INIT");
        var ft = Assert.IsType<FunctionType>(sym!.Type);
        Assert.IsType<PointerType>(ft.ParameterTypes[0]);
    }
}
