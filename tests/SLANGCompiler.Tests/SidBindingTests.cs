using SLANGCompiler;
using SLANGCompiler.CodeGen.C;
using SLANGCompiler.Lexer;
using SLANGCompiler.Parser;
using SLANGCompiler.Runtime;
using SLANGCompiler.Semantics;
using Xunit;

namespace SLANGCompiler.Tests;

/// <summary>
/// v3b-A SID register direct + 単発 SFX binding の signature drift / 呼出 emit を
/// golden 化する。env c_bindings: と bridge header (runtime/c64/slang_sid.h)
/// の一致確認も兼ねる。
/// </summary>
public class SidBindingTests
{
    private static EnvironmentConfig MakeC64EnvWithSid()
    {
        // 実 runtime/env/c64.env をパースする代わりに、テスト独立性のために
        // 必要 binding だけ手組み (= env file 解析テストは EnvCBindingsTests で別途網羅)。
        // v3b-A 9 entry (= register direct 8 + 単発 SFX wrapper 1) +
        // v3b-B 4 entry (= HVSC .sid disk load + player) = 計 13 entry を網羅。
        return new EnvironmentConfig
        {
            Name = "c64",
            Backend = BackendKind.OscarC,
            OutputFormat = "c_source",
            CBindings = new List<CBindingDef>
            {
                new() { Name = "SID_INIT_QUIET", CName = "slang_sid_init_quiet",
                        Params = new List<CBindingType>(),
                        Return = CBindingType.Void },
                new() { Name = "SID_VOLUME", CName = "slang_sid_volume",
                        Params = new List<CBindingType> { CBindingType.Byte },
                        Return = CBindingType.Void },
                new() { Name = "SID_FREQ", CName = "slang_sid_freq",
                        Params = new List<CBindingType> { CBindingType.Byte, CBindingType.Word },
                        Return = CBindingType.Void },
                new() { Name = "SID_PWM", CName = "slang_sid_pwm",
                        Params = new List<CBindingType> { CBindingType.Byte, CBindingType.Word },
                        Return = CBindingType.Void },
                new() { Name = "SID_ADSR", CName = "slang_sid_adsr",
                        Params = new List<CBindingType> { CBindingType.Byte, CBindingType.Byte, CBindingType.Byte },
                        Return = CBindingType.Void },
                new() { Name = "SID_CTRL", CName = "slang_sid_ctrl",
                        Params = new List<CBindingType> { CBindingType.Byte, CBindingType.Byte },
                        Return = CBindingType.Void },
                new() { Name = "SID_GATE_ON", CName = "slang_sid_gate_on",
                        Params = new List<CBindingType> { CBindingType.Byte },
                        Return = CBindingType.Void },
                new() { Name = "SID_GATE_OFF", CName = "slang_sid_gate_off",
                        Params = new List<CBindingType> { CBindingType.Byte },
                        Return = CBindingType.Void },
                new() { Name = "SID_SFX", CName = "slang_sid_sfx",
                        Params = new List<CBindingType> { CBindingType.Byte, CBindingType.Word, CBindingType.Byte, CBindingType.Byte, CBindingType.Byte },
                        Return = CBindingType.Void },
                // v3b-B: HVSC .sid disk load + BGM player
                new() { Name = "SID_LOAD_FROM_BUF", CName = "slang_sid_load_from_buf",
                        Params = new List<CBindingType> { CBindingType.BytePtr, CBindingType.Word },
                        Return = CBindingType.Word },
                new() { Name = "SID_PLAYER_INIT", CName = "slang_sid_player_init",
                        Params = new List<CBindingType> { CBindingType.Byte },
                        Return = CBindingType.Void },
                new() { Name = "SID_PLAYER_PLAY", CName = "slang_sid_player_play",
                        Params = new List<CBindingType>(),
                        Return = CBindingType.Void },
                new() { Name = "SID_PLAYER_READY", CName = "slang_sid_player_ready",
                        Params = new List<CBindingType>(),
                        Return = CBindingType.Word },
                new() { Name = "SID_LOAD_FROM_BUF_ADDR", CName = "slang_sid_load_from_buf_addr",
                        Params = new List<CBindingType> { CBindingType.Word, CBindingType.Word },
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
    public void SidAll_EmitsExternsAndCalls()
    {
        // SID 14 binding (= v3b-A 9 + v3b-B 5) を SLANG コード上で実呼出して
        // CTranspiler が extern を必ず emit するパスを通す。byte / word /
        // byte_ptr 引数の組合せ + void / word return を網羅。
        var src = TranspileWithEnv("""
            ARRAY BYTE BUF[16];
            MAIN() {
                VAR OK, R;
                SID_INIT_QUIET();
                SID_VOLUME(15);
                SID_FREQ(0, $1D44);
                SID_PWM(0, $0800);
                SID_ADSR(0, $10, $F8);
                SID_CTRL(0, $11);
                SID_GATE_ON(0);
                SID_GATE_OFF(1);
                SID_SFX(2, $22C9, $20, $A8, $80);
                OK = SID_LOAD_FROM_BUF(BUF, 16);
                OK = SID_LOAD_FROM_BUF_ADDR($C000, 1024);
                SID_PLAYER_INIT(0);
                SID_PLAYER_PLAY();
                R = SID_PLAYER_READY();
            }
            """, MakeC64EnvWithSid(), out var diag);

        Assert.False(diag.HasErrors,
            $"errors: {string.Join("; ", diag.Diagnostics.Select(d => d.Message))}");

        // bridge header (runtime/c64/slang_sid.h) の signature と env c_bindings: が
        // drift していないことを 13 entry すべての extern 出力で確認。
        // === v3b-A: register direct + SFX wrapper (9 個) ===
        Assert.Contains("extern void slang_sid_init_quiet(void);", src);
        Assert.Contains("extern void slang_sid_volume(unsigned char);", src);
        Assert.Contains("extern void slang_sid_freq(unsigned char, unsigned int);", src);
        Assert.Contains("extern void slang_sid_pwm(unsigned char, unsigned int);", src);
        Assert.Contains("extern void slang_sid_adsr(unsigned char, unsigned char, unsigned char);", src);
        Assert.Contains("extern void slang_sid_ctrl(unsigned char, unsigned char);", src);
        Assert.Contains("extern void slang_sid_gate_on(unsigned char);", src);
        Assert.Contains("extern void slang_sid_gate_off(unsigned char);", src);
        Assert.Contains("extern void slang_sid_sfx(unsigned char, unsigned int, unsigned char, unsigned char, unsigned char);", src);
        // === v3b-B: HVSC .sid disk load + BGM player (5 個) ===
        Assert.Contains("extern unsigned int slang_sid_load_from_buf(unsigned char *, unsigned int);", src);
        Assert.Contains("extern unsigned int slang_sid_load_from_buf_addr(unsigned int, unsigned int);", src);
        Assert.Contains("extern void slang_sid_player_init(unsigned char);", src);
        Assert.Contains("extern void slang_sid_player_play(void);", src);
        Assert.Contains("extern unsigned int slang_sid_player_ready(void);", src);

        // 呼出が C 関数として展開される (14 個すべて)
        Assert.Contains("slang_sid_init_quiet(",         src);
        Assert.Contains("slang_sid_volume(",             src);
        Assert.Contains("slang_sid_freq(",               src);
        Assert.Contains("slang_sid_pwm(",                src);
        Assert.Contains("slang_sid_adsr(",               src);
        Assert.Contains("slang_sid_ctrl(",               src);
        Assert.Contains("slang_sid_gate_on(",            src);
        Assert.Contains("slang_sid_gate_off(",           src);
        Assert.Contains("slang_sid_sfx(",                src);
        Assert.Contains("slang_sid_load_from_buf(",      src);
        Assert.Contains("slang_sid_load_from_buf_addr(", src);
        Assert.Contains("slang_sid_player_init(",        src);
        Assert.Contains("slang_sid_player_play(",        src);
        Assert.Contains("slang_sid_player_ready(",       src);
    }

    [Fact]
    public void RealC64Env_HasAllSidBindings()
    {
        // 実 runtime/env/c64.env を EnvironmentLoader でパースして SID_* entry
        // と c_runtime_files に slang_sid.c が含まれていることを確認。
        // 手組み env では捕捉できない drift (= env file 編集忘れや typo) を防ぐ。
        var repoRoot = FindRepoRoot();
        var envPath = Path.Combine(repoRoot, "runtime", "env", "c64.env");
        Assert.True(File.Exists(envPath), $"runtime/env/c64.env not found at {envPath}");

        var config = EnvironmentLoader.Load(envPath);
        Assert.Equal(BackendKind.OscarC, config.Backend);

        // c_runtime_files に slang_sid.c
        Assert.NotNull(config.CRuntimeFiles);
        Assert.Contains(config.CRuntimeFiles!,
            p => Path.GetFileName(p).Equals("slang_sid.c", StringComparison.OrdinalIgnoreCase));

        // c_bindings に SID_* 14 entry (= v3b-A 9 + v3b-B 5) がすべて含まれる
        Assert.NotNull(config.CBindings);
        var bindingNames = config.CBindings!.Select(b => b.Name).ToHashSet(StringComparer.OrdinalIgnoreCase);
        foreach (var expected in new[]
        {
            // v3b-A
            "SID_INIT_QUIET", "SID_VOLUME", "SID_FREQ", "SID_PWM", "SID_ADSR",
            "SID_CTRL", "SID_GATE_ON", "SID_GATE_OFF", "SID_SFX",
            // v3b-B
            "SID_LOAD_FROM_BUF", "SID_LOAD_FROM_BUF_ADDR",
            "SID_PLAYER_INIT", "SID_PLAYER_PLAY", "SID_PLAYER_READY",
        })
        {
            Assert.Contains(expected, bindingNames);
        }

        // signature ピン留め (= 代表的な entry の c_name + 型)
        var sidSfx = config.CBindings!.First(b => b.Name == "SID_SFX");
        Assert.Equal("slang_sid_sfx", sidSfx.CName);
        Assert.Equal(5, sidSfx.Params.Count);
        Assert.Equal(CBindingType.Byte, sidSfx.Params[0]);
        Assert.Equal(CBindingType.Word, sidSfx.Params[1]);
        Assert.Equal(CBindingType.Byte, sidSfx.Params[2]);
        Assert.Equal(CBindingType.Byte, sidSfx.Params[3]);
        Assert.Equal(CBindingType.Byte, sidSfx.Params[4]);
        Assert.Equal(CBindingType.Void, sidSfx.Return);

        var sidInit = config.CBindings!.First(b => b.Name == "SID_INIT_QUIET");
        Assert.Equal("slang_sid_init_quiet", sidInit.CName);
        Assert.Empty(sidInit.Params);
        Assert.Equal(CBindingType.Void, sidInit.Return);

        var sidFreq = config.CBindings!.First(b => b.Name == "SID_FREQ");
        Assert.Equal("slang_sid_freq", sidFreq.CName);
        Assert.Equal(2, sidFreq.Params.Count);
        Assert.Equal(CBindingType.Byte, sidFreq.Params[0]);
        Assert.Equal(CBindingType.Word, sidFreq.Params[1]);

        // v3b-B signature pinning
        var sidLoadFromBuf = config.CBindings!.First(b => b.Name == "SID_LOAD_FROM_BUF");
        Assert.Equal("slang_sid_load_from_buf", sidLoadFromBuf.CName);
        Assert.Equal(2, sidLoadFromBuf.Params.Count);
        Assert.Equal(CBindingType.BytePtr, sidLoadFromBuf.Params[0]);
        Assert.Equal(CBindingType.Word, sidLoadFromBuf.Params[1]);
        Assert.Equal(CBindingType.Word, sidLoadFromBuf.Return);

        var sidPlayerPlay = config.CBindings!.First(b => b.Name == "SID_PLAYER_PLAY");
        Assert.Equal("slang_sid_player_play", sidPlayerPlay.CName);
        Assert.Empty(sidPlayerPlay.Params);
        Assert.Equal(CBindingType.Void, sidPlayerPlay.Return);

        var sidPlayerReady = config.CBindings!.First(b => b.Name == "SID_PLAYER_READY");
        Assert.Equal("slang_sid_player_ready", sidPlayerReady.CName);
        Assert.Empty(sidPlayerReady.Params);
        Assert.Equal(CBindingType.Word, sidPlayerReady.Return);
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
