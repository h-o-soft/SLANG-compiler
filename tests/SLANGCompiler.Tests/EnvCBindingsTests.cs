using SLANGCompiler;
using SLANGCompiler.CodeGen.C;
using SLANGCompiler.Lexer;
using SLANGCompiler.Parser;
using SLANGCompiler.Runtime;
using SLANGCompiler.Semantics;
using Xunit;

namespace SLANGCompiler.Tests;

/// <summary>
/// env file `c_bindings:` の YAML 解析 + Z80 排他 + CBindingRegistry 動作 +
/// CTranspiler/CEmitter 経由の C 出力までを通しで検証する。
/// </summary>
public class EnvCBindingsTests : IDisposable
{
    private readonly string _tempDir;
    private readonly string _envDir;
    private readonly string _runtimeDir;

    public EnvCBindingsTests()
    {
        _tempDir = Path.Combine(Path.GetTempPath(), $"env_cb_{Guid.NewGuid():N}");
        _envDir = Path.Combine(_tempDir, "env");
        _runtimeDir = Path.Combine(_tempDir, "c64");
        Directory.CreateDirectory(_envDir);
        Directory.CreateDirectory(_runtimeDir);
        File.WriteAllText(Path.Combine(_runtimeDir, "slang_runtime.c"), "/* stub */");
    }

    public void Dispose()
    {
        if (Directory.Exists(_tempDir))
        {
            try { Directory.Delete(_tempDir, true); } catch { }
        }
    }

    private string WriteEnv(string fileName, string yaml)
    {
        var path = Path.Combine(_envDir, fileName);
        File.WriteAllText(path, yaml);
        return path;
    }

    // === E1: 正常 YAML parse ===

    [Fact]
    public void E1_ValidYaml_LoadsAllEntries()
    {
        var envPath = WriteEnv("c64.env", """
backend: oscar_c
output: c_source
c_runtime_files:
  - ../c64/slang_runtime.c
c_bindings:
  - name: SPR_INIT
    c_name: slang_spr_init
    params: [word]
    return: void
  - name: SPR_MOVE
    c_name: slang_spr_move
    params: [byte, word, word]
    return: void
  - name: SPR_POSX
    c_name: slang_spr_posx
    params: [byte]
    return: word
""");
        var config = EnvironmentLoader.Load(envPath);
        Assert.NotNull(config.CBindings);
        Assert.Equal(3, config.CBindings!.Count);

        var sprInit = config.CBindings[0];
        Assert.Equal("SPR_INIT", sprInit.Name);
        Assert.Equal("slang_spr_init", sprInit.CName);
        Assert.Single(sprInit.Params);
        Assert.Equal(CBindingType.Word, sprInit.Params[0]);
        Assert.Equal(CBindingType.Void, sprInit.Return);

        var sprMove = config.CBindings[1];
        Assert.Equal(3, sprMove.Params.Count);
        Assert.Equal(CBindingType.Byte, sprMove.Params[0]);
    }

    // === E2: Z80 backend で c_bindings: → reject ===

    [Fact]
    public void E2_Z80Backend_WithCBindings_Rejected()
    {
        var envPath = WriteEnv("bad.env", """
env_type: 0
os_type: 0
default_org: "$100"
libraries:
  - runtime.yml
c_bindings:
  - name: FOO
    c_name: foo
    params: []
    return: void
""");
        var ex = Assert.Throws<InvalidDataException>(() => EnvironmentLoader.Load(envPath));
        Assert.Contains("c_bindings", ex.Message);
    }

    // === E3: 不明 type token (bool) → reject ===

    [Fact]
    public void E3_UnknownTypeToken_Rejected()
    {
        var envPath = WriteEnv("bad.env", """
backend: oscar_c
output: c_source
c_runtime_files:
  - ../c64/slang_runtime.c
c_bindings:
  - name: FOO
    c_name: foo
    params: [byte, bool]
    return: void
""");
        var ex = Assert.Throws<InvalidDataException>(() => EnvironmentLoader.Load(envPath));
        Assert.Contains("bool", ex.Message);
    }

    // === E4: params に void → reject ===

    [Fact]
    public void E4_VoidInParams_Rejected()
    {
        var envPath = WriteEnv("bad.env", """
backend: oscar_c
output: c_source
c_runtime_files:
  - ../c64/slang_runtime.c
c_bindings:
  - name: FOO
    c_name: foo
    params: [void]
    return: void
""");
        var ex = Assert.Throws<InvalidDataException>(() => EnvironmentLoader.Load(envPath));
        Assert.Contains("void", ex.Message);
    }

    // === E5: name 重複 (case-insensitive) → reject ===

    [Fact]
    public void E5_DuplicateName_CaseInsensitive_Rejected()
    {
        var envPath = WriteEnv("bad.env", """
backend: oscar_c
output: c_source
c_runtime_files:
  - ../c64/slang_runtime.c
c_bindings:
  - name: Spr_Set
    c_name: spr_set_a
    params: [byte]
    return: void
  - name: spr_set
    c_name: spr_set_b
    params: [byte]
    return: void
""");
        var ex = Assert.Throws<InvalidDataException>(() => EnvironmentLoader.Load(envPath));
        Assert.Contains("duplicate", ex.Message);
    }

    // === E6: c_name 重複で signature 一致 → alias OK ===

    [Fact]
    public void E6_DuplicateCName_SignatureMatch_AliasAllowed()
    {
        var envPath = WriteEnv("c64.env", """
backend: oscar_c
output: c_source
c_runtime_files:
  - ../c64/slang_runtime.c
c_bindings:
  - name: SPR_SET_A
    c_name: spr_set
    params: [byte, word]
    return: void
  - name: SPR_SET_B
    c_name: spr_set
    params: [byte, word]
    return: void
""");
        var config = EnvironmentLoader.Load(envPath);
        Assert.Equal(2, config.CBindings!.Count);
    }

    // === E7: c_name 重複で signature 不一致 → error ===

    [Fact]
    public void E7_DuplicateCName_SignatureMismatch_Rejected()
    {
        var envPath = WriteEnv("bad.env", """
backend: oscar_c
output: c_source
c_runtime_files:
  - ../c64/slang_runtime.c
c_bindings:
  - name: FOO_A
    c_name: foo
    params: [byte]
    return: void
  - name: FOO_B
    c_name: foo
    params: [word, word]
    return: void
""");
        var ex = Assert.Throws<InvalidDataException>(() => EnvironmentLoader.Load(envPath));
        Assert.Contains("signature", ex.Message);
    }

    // === E8: SLANG 側 CFUNC 宣言が env binding を override ===

    [Fact]
    public void E8_UserCFuncOverridesEnvBinding_EmitsInfoDiagnostic()
    {
        var diag = new DiagnosticBag();
        var source = """
CFUNC SPR_INIT(WORD addr) BYTE :user_spr_init_v2;
MAIN() { SPR_INIT(0); }
""";
        var lexer = new Lexer.Lexer(source, "<test>");
        var tokens = lexer.Tokenize();
        var preproc = new Preprocessor(diag, new List<string>());
        preproc.DefineConst("BACKEND", 1);
        preproc.DefineConst("ENV_TYPE", 7);
        tokens = preproc.Process(tokens, ".");
        var parser = new Parser.Parser(tokens, diag);
        var ast = parser.ParseCompilationUnit();
        var analyzer = new SemanticAnalyzer(diag);
        analyzer.Analyze(ast);
        Assert.False(diag.HasErrors);

        var env = new EnvironmentConfig
        {
            Name = "c64",
            Backend = BackendKind.OscarC,
            OutputFormat = "c_source",
            CBindings = new List<CBindingDef>
            {
                new() {
                    Name = "SPR_INIT",
                    CName = "slang_spr_init",   // env 提供
                    Params = new List<CBindingType> { CBindingType.Word },
                    Return = CBindingType.Void,
                },
            },
        };
        var transpiler = new CTranspiler(analyzer.Symbols, env, diag);
        var cSrc = transpiler.Transpile(ast);

        // SLANG 側 CFUNC (= user_spr_init_v2) が emit され、env binding (slang_spr_init)
        // は同名なので skip される
        Assert.Contains("user_spr_init_v2", cSrc);
        Assert.DoesNotContain("slang_spr_init", cSrc);

        // info diagnostics で override 通知
        Assert.Contains(diag.Diagnostics, d =>
            d.Severity == DiagnosticSeverity.Info
            && d.Message.Contains("SPR_INIT")
            && d.Message.Contains("overridden"));
    }

    // === 追加: env binding 経由の呼出が正しく C extern + call になる ===

    [Fact]
    public void E9_EnvBinding_EmitsExternAndCall_NoUserCFunc()
    {
        var diag = new DiagnosticBag();
        var source = """
MAIN() {
    SPR_INIT($0400);
    SPR_MOVE(0, 100, 50);
}
""";
        var lexer = new Lexer.Lexer(source, "<test>");
        var tokens = lexer.Tokenize();
        var preproc = new Preprocessor(diag, new List<string>());
        preproc.DefineConst("BACKEND", 1);
        preproc.DefineConst("ENV_TYPE", 7);
        tokens = preproc.Process(tokens, ".");
        var parser = new Parser.Parser(tokens, diag);
        var ast = parser.ParseCompilationUnit();
        var analyzer = new SemanticAnalyzer(diag);
        analyzer.Analyze(ast);
        Assert.False(diag.HasErrors);

        var env = new EnvironmentConfig
        {
            Name = "c64",
            Backend = BackendKind.OscarC,
            OutputFormat = "c_source",
            CBindings = new List<CBindingDef>
            {
                new() {
                    Name = "SPR_INIT", CName = "slang_spr_init",
                    Params = new List<CBindingType> { CBindingType.Word },
                    Return = CBindingType.Void,
                },
                new() {
                    Name = "SPR_MOVE", CName = "slang_spr_move",
                    Params = new List<CBindingType> { CBindingType.Byte, CBindingType.Word, CBindingType.Word },
                    Return = CBindingType.Void,
                },
            },
        };
        var transpiler = new CTranspiler(analyzer.Symbols, env, diag);
        var cSrc = transpiler.Transpile(ast);
        Assert.False(diag.HasErrors, $"errors: {string.Join(";", diag.Diagnostics.Select(d => d.Message))}");

        // env binding が extern として emit される
        Assert.Contains("extern void slang_spr_init(unsigned int);", cSrc);
        Assert.Contains("extern void slang_spr_move(unsigned char, unsigned int, unsigned int);", cSrc);
        // 呼出も正しい c_name で
        Assert.Contains("slang_spr_init(", cSrc);
        Assert.Contains("slang_spr_move(", cSrc);
    }
}
