using SLANGCompiler;
using SLANGCompiler.CodeGen.C;
using SLANGCompiler.Lexer;
using SLANGCompiler.Parser;
using SLANGCompiler.Runtime;
using SLANGCompiler.Semantics;
using Xunit;

namespace SLANGCompiler.Tests;

/// <summary>
/// v3a Joystick binding の signature drift / 呼出 emit を golden 化する。
/// env c_bindings: と bridge header (runtime/c64/slang_joystick.h) の
/// 一致確認も兼ねる。
/// </summary>
public class JoystickBindingTests
{
    private static EnvironmentConfig MakeC64EnvWithJoystick()
    {
        // 実 runtime/env/c64.env をパースする代わりに、テスト独立性のために
        // 必要 binding だけ手組み (= env file 解析テストは EnvCBindingsTests で別途網羅)。
        return new EnvironmentConfig
        {
            Name = "c64",
            Backend = BackendKind.OscarC,
            OutputFormat = "c_source",
            CBindings = new List<CBindingDef>
            {
                new() { Name = "JOY_POLL", CName = "slang_joy_poll",
                        Params = new List<CBindingType> { CBindingType.Byte },
                        Return = CBindingType.Void },
                new() { Name = "JOY_X", CName = "slang_joy_x",
                        Params = new List<CBindingType> { CBindingType.Byte },
                        Return = CBindingType.Word },
                new() { Name = "JOY_Y", CName = "slang_joy_y",
                        Params = new List<CBindingType> { CBindingType.Byte },
                        Return = CBindingType.Word },
                new() { Name = "JOY_B", CName = "slang_joy_b",
                        Params = new List<CBindingType> { CBindingType.Byte },
                        Return = CBindingType.Word },
                new() { Name = "JOY_DIR", CName = "slang_joy_dir",
                        Params = new List<CBindingType> { CBindingType.Byte },
                        Return = CBindingType.Word },
            },
        };
    }

    private static string TranspileWithEnv(string source, EnvironmentConfig env, out DiagnosticBag diag)
    {
        diag = new DiagnosticBag();
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
        if (diag.HasErrors) return "";
        var transpiler = new CTranspiler(analyzer.Symbols, env, diag);
        return transpiler.Transpile(ast);
    }

    [Fact]
    public void JoyAll_EmitsExternsAndCalls()
    {
        var src = TranspileWithEnv("""
            MAIN() {
                VAR D;
                JOY_POLL(0);
                D = JOY_DIR(1);
                IF JOY_X(0) == $FFFF THEN D = D + 1;
                IF JOY_Y(0) == 1     THEN D = D + 1;
                IF JOY_B(1)          THEN D = D + 1;
            }
            """, MakeC64EnvWithJoystick(), out var diag);

        Assert.False(diag.HasErrors,
            $"errors: {string.Join("; ", diag.Diagnostics.Select(d => d.Message))}");

        // bridge header (runtime/c64/slang_joystick.h) の signature と
        // env c_bindings: が drift していないことを extern 出力で確認
        Assert.Contains("extern void slang_joy_poll(unsigned char);", src);
        Assert.Contains("extern unsigned int slang_joy_x(unsigned char);", src);
        Assert.Contains("extern unsigned int slang_joy_y(unsigned char);", src);
        Assert.Contains("extern unsigned int slang_joy_b(unsigned char);", src);
        Assert.Contains("extern unsigned int slang_joy_dir(unsigned char);", src);

        // 呼出が C 関数として展開される
        Assert.Contains("slang_joy_poll(", src);
        Assert.Contains("slang_joy_dir(", src);
        Assert.Contains("slang_joy_x(", src);
        Assert.Contains("slang_joy_y(", src);
        Assert.Contains("slang_joy_b(", src);
    }

    [Fact]
    public void JoyX_FFFFCompare_GeneratedCorrectly()
    {
        // $FFFF (= signed -1) 判定パターンが SLANG コードのまま CEmitter に
        // 渡って (unsigned int)0xFFFFu の literal 展開になることを確認。
        var src = TranspileWithEnv("""
            MAIN() {
                VAR D;
                IF JOY_X(1) == $FFFF THEN D = 1;
            }
            """, MakeC64EnvWithJoystick(), out var diag);
        Assert.False(diag.HasErrors);
        Assert.Contains("0xFFFFu", src);
    }

    [Fact]
    public void RealC64Env_HasAllJoyBindings()
    {
        // 実 runtime/env/c64.env を EnvironmentLoader でパースして JOY_* 5 entries
        // と c_runtime_files に slang_joystick.c が含まれていることを確認。
        // 手組み env では捕捉できない drift (= env file 編集忘れや typo) を防ぐ。
        var repoRoot = FindRepoRoot();
        var envPath = Path.Combine(repoRoot, "runtime", "env", "c64.env");
        Assert.True(File.Exists(envPath), $"runtime/env/c64.env not found at {envPath}");

        var config = EnvironmentLoader.Load(envPath);
        Assert.Equal(BackendKind.OscarC, config.Backend);

        // c_runtime_files に slang_joystick.c
        Assert.NotNull(config.CRuntimeFiles);
        Assert.Contains(config.CRuntimeFiles!,
            p => Path.GetFileName(p).Equals("slang_joystick.c", StringComparison.OrdinalIgnoreCase));

        // c_bindings に JOY_* 5 entry がすべて含まれる
        Assert.NotNull(config.CBindings);
        var bindingNames = config.CBindings!.Select(b => b.Name).ToHashSet(StringComparer.OrdinalIgnoreCase);
        Assert.Contains("JOY_POLL", bindingNames);
        Assert.Contains("JOY_X",    bindingNames);
        Assert.Contains("JOY_Y",    bindingNames);
        Assert.Contains("JOY_B",    bindingNames);
        Assert.Contains("JOY_DIR",  bindingNames);

        // signature (= c_name + 型) も期待値と一致
        var joyDir = config.CBindings!.First(b => b.Name == "JOY_DIR");
        Assert.Equal("slang_joy_dir", joyDir.CName);
        Assert.Single(joyDir.Params);
        Assert.Equal(CBindingType.Byte, joyDir.Params[0]);
        Assert.Equal(CBindingType.Word, joyDir.Return);

        var joyPoll = config.CBindings!.First(b => b.Name == "JOY_POLL");
        Assert.Equal("slang_joy_poll", joyPoll.CName);
        Assert.Equal(CBindingType.Void, joyPoll.Return);
    }

    /// <summary>
    /// テスト実行 cwd から SLANG-compiler repo root を遡って探す。bin/Debug/net8.0
    /// から実行されるため複数階層上に上がる必要あり。
    /// </summary>
    private static string FindRepoRoot()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir != null)
        {
            if (File.Exists(Path.Combine(dir.FullName, "runtime", "env", "c64.env")))
                return dir.FullName;
            dir = dir.Parent;
        }
        throw new InvalidOperationException("Could not locate SLANG-compiler repo root containing runtime/env/c64.env");
    }
}
