using SLANGCompiler;
using SLANGCompiler.CodeGen.C;
using SLANGCompiler.Lexer;
using SLANGCompiler.Parser;
using SLANGCompiler.Runtime;
using SLANGCompiler.Semantics;
using Xunit;

namespace SLANGCompiler.Tests;

/// <summary>
/// v3b-C oscar64 audio/sidfx priority SFX overlay binding の signature drift /
/// 呼出 emit を golden 化する。env c_bindings: と bridge header
/// (runtime/c64/slang_sidfx.h) の一致確認、実 runtime/env/c64.env 全 7 binding
/// 列挙、SIDFX struct (= 14 byte) 相当を SLANG ARRAY BYTE で組み立てて raw
/// byte_ptr で渡せることも検証 (= v3b-D で導入した ARRAY init + WORD prefix の
/// 統合動作確認)。
/// </summary>
public class SidfxBindingTests
{
    private static EnvironmentConfig MakeC64EnvWithSidfx()
    {
        return new EnvironmentConfig
        {
            Name = "c64",
            Backend = BackendKind.OscarC,
            OutputFormat = "c_source",
            CBindings = new List<CBindingDef>
            {
                new() { Name = "SIDFX_INIT", CName = "slang_sidfx_init",
                        Params = new List<CBindingType>(),
                        Return = CBindingType.Void },
                new() { Name = "SIDFX_PLAY", CName = "slang_sidfx_play",
                        Params = new List<CBindingType> { CBindingType.Byte, CBindingType.BytePtr, CBindingType.Byte },
                        Return = CBindingType.Void },
                new() { Name = "SIDFX_STOP", CName = "slang_sidfx_stop",
                        Params = new List<CBindingType> { CBindingType.Byte },
                        Return = CBindingType.Void },
                new() { Name = "SIDFX_IDLE", CName = "slang_sidfx_idle",
                        Params = new List<CBindingType> { CBindingType.Byte },
                        Return = CBindingType.Word },
                new() { Name = "SIDFX_CNT", CName = "slang_sidfx_cnt",
                        Params = new List<CBindingType> { CBindingType.Byte },
                        Return = CBindingType.Word },
                new() { Name = "SIDFX_LOOP", CName = "slang_sidfx_loop",
                        Params = new List<CBindingType>(),
                        Return = CBindingType.Void },
                new() { Name = "SIDFX_LOOP_2", CName = "slang_sidfx_loop_2",
                        Params = new List<CBindingType>(),
                        Return = CBindingType.Void },
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
    public void SidfxAll_EmitsExternsAndCalls()
    {
        // 7 binding すべてを SLANG コード上で呼出して CTranspiler の extern + call
        // emit を網羅。SIDFX struct (= 14 byte) は ARRAY BYTE で組み立てて byte_ptr
        // で渡す (= v3b-D ARRAY init + WORD prefix の統合確認)。
        var src = TranspileWithEnv("""
            ARRAY BYTE FX[] = {
                %$1500, %$0000, $21, $10, $A8, %$FFC0, %$0000, $20, $00, 64
            };
            MAIN() {
                VAR R;
                SIDFX_INIT();
                SIDFX_PLAY(2, FX, 1);
                R = SIDFX_IDLE(0);
                R = SIDFX_CNT(1);
                SIDFX_STOP(2);
                SIDFX_LOOP();
                SIDFX_LOOP_2();
            }
            """, MakeC64EnvWithSidfx(), out var diag);

        Assert.False(diag.HasErrors,
            $"errors: {string.Join("; ", diag.Diagnostics.Select(d => d.Message))}");

        // SIDFX struct array (= 14 byte) が ARRAY BYTE init で LE 展開されることも
        // 同時確認 (= v3b-D の依存機能が壊れていないこと、SIDFX layout 通り)
        Assert.Contains("static unsigned char V_FX[14] = {0x00, 0x15, 0x00, 0x00, 0x21, 0x10, 0xA8, 0xC0, 0xFF, 0x00, 0x00, 0x20, 0x00, 0x40};", src);

        // extern signature 7 entry pin (= bridge header と env c_bindings の drift 防止)
        Assert.Contains("extern void slang_sidfx_init(void);", src);
        Assert.Contains("extern void slang_sidfx_play(unsigned char, unsigned char *, unsigned char);", src);
        Assert.Contains("extern void slang_sidfx_stop(unsigned char);", src);
        Assert.Contains("extern unsigned int slang_sidfx_idle(unsigned char);", src);
        Assert.Contains("extern unsigned int slang_sidfx_cnt(unsigned char);", src);
        Assert.Contains("extern void slang_sidfx_loop(void);", src);
        Assert.Contains("extern void slang_sidfx_loop_2(void);", src);

        // 呼出が C 関数として展開される
        Assert.Contains("slang_sidfx_init(",   src);
        Assert.Contains("slang_sidfx_play(",   src);
        Assert.Contains("slang_sidfx_stop(",   src);
        Assert.Contains("slang_sidfx_idle(",   src);
        Assert.Contains("slang_sidfx_cnt(",    src);
        Assert.Contains("slang_sidfx_loop(",   src);
        Assert.Contains("slang_sidfx_loop_2(", src);
    }

    [Fact]
    public void RealC64Env_HasAllSidfxBindings()
    {
        // 実 runtime/env/c64.env を EnvironmentLoader でパースして SIDFX_* 7 entry
        // と c_runtime_files に slang_sidfx.c が含まれていることを確認 (= 手組み env
        // では捕捉できない drift = env file 編集忘れや typo を防ぐ)。
        var repoRoot = FindRepoRoot();
        var envPath = Path.Combine(repoRoot, "runtime", "env", "c64.env");
        Assert.True(File.Exists(envPath), $"runtime/env/c64.env not found at {envPath}");

        var config = EnvironmentLoader.Load(envPath);
        Assert.Equal(BackendKind.OscarC, config.Backend);

        // c_runtime_files に slang_sidfx.c
        Assert.NotNull(config.CRuntimeFiles);
        Assert.Contains(config.CRuntimeFiles!,
            p => Path.GetFileName(p).Equals("slang_sidfx.c", StringComparison.OrdinalIgnoreCase));

        // c_bindings に SIDFX_* 7 entry 全部含まれる
        Assert.NotNull(config.CBindings);
        var bindingNames = config.CBindings!.Select(b => b.Name).ToHashSet(StringComparer.OrdinalIgnoreCase);
        foreach (var expected in new[]
        {
            "SIDFX_INIT", "SIDFX_PLAY", "SIDFX_STOP", "SIDFX_IDLE",
            "SIDFX_CNT", "SIDFX_LOOP", "SIDFX_LOOP_2",
        })
        {
            Assert.Contains(expected, bindingNames);
        }

        // 代表的な signature pin (= SIDFX_PLAY = byte_ptr 受け取り、 SIDFX_IDLE = word 返り)
        var sidfxPlay = config.CBindings!.First(b => b.Name == "SIDFX_PLAY");
        Assert.Equal("slang_sidfx_play", sidfxPlay.CName);
        Assert.Equal(3, sidfxPlay.Params.Count);
        Assert.Equal(CBindingType.Byte,    sidfxPlay.Params[0]);
        Assert.Equal(CBindingType.BytePtr, sidfxPlay.Params[1]);
        Assert.Equal(CBindingType.Byte,    sidfxPlay.Params[2]);
        Assert.Equal(CBindingType.Void,    sidfxPlay.Return);

        var sidfxIdle = config.CBindings!.First(b => b.Name == "SIDFX_IDLE");
        Assert.Equal("slang_sidfx_idle", sidfxIdle.CName);
        Assert.Single(sidfxIdle.Params);
        Assert.Equal(CBindingType.Word, sidfxIdle.Return);

        var sidfxLoop2 = config.CBindings!.First(b => b.Name == "SIDFX_LOOP_2");
        Assert.Equal("slang_sidfx_loop_2", sidfxLoop2.CName);
        Assert.Empty(sidfxLoop2.Params);
        Assert.Equal(CBindingType.Void, sidfxLoop2.Return);
    }

    /// <summary>
    /// テスト実行 cwd から SLANG-compiler repo root を遡って探す。
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
