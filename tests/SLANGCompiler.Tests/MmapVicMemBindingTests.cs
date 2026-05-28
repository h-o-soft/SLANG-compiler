using SLANGCompiler;
using SLANGCompiler.CodeGen.C;
using SLANGCompiler.Lexer;
using SLANGCompiler.Parser;
using SLANGCompiler.Runtime;
using SLANGCompiler.Semantics;
using Xunit;

namespace SLANGCompiler.Tests;

/// <summary>
/// v0.25.0 C64 最低限ゲーム API binding の signature drift / 呼出 emit を golden 化する。
/// env c_bindings: と bridge header (runtime/c64/slang_memmap.h / slang_vic.h / slang_mem.h)
/// の一致確認も兼ねる。 全 6 entry (= MMAP_SET / MMAP_TRAMPOLINE / VIC_SETMODE / VIC_SETBANK
/// / MEMCPY / MEMSET) を網羅して signature drift を CI で検出可能にする。
/// </summary>
public class MmapVicMemBindingTests
{
    private static EnvironmentConfig MakeC64EnvWithMmapVicMem()
    {
        return new EnvironmentConfig
        {
            Name = "c64",
            Backend = BackendKind.OscarC,
            OutputFormat = "c_source",
            CBindings = new List<CBindingDef>
            {
                new() { Name = "MMAP_SET", CName = "slang_mmap_set",
                        Params = new List<CBindingType> { CBindingType.Byte },
                        Return = CBindingType.Byte },
                new() { Name = "MMAP_TRAMPOLINE", CName = "slang_mmap_trampoline",
                        Params = new List<CBindingType>(),
                        Return = CBindingType.Void },
                new() { Name = "VIC_SETMODE", CName = "slang_vic_setmode",
                        Params = new List<CBindingType> { CBindingType.Byte, CBindingType.Word, CBindingType.Word },
                        Return = CBindingType.Void },
                new() { Name = "VIC_SETBANK", CName = "slang_vic_setbank",
                        Params = new List<CBindingType> { CBindingType.Byte },
                        Return = CBindingType.Void },
                new() { Name = "MEMCPY", CName = "slang_memcpy",
                        Params = new List<CBindingType> { CBindingType.Word, CBindingType.Word, CBindingType.Word },
                        Return = CBindingType.Void },
                new() { Name = "MEMSET", CName = "slang_memset",
                        Params = new List<CBindingType> { CBindingType.Word, CBindingType.Byte, CBindingType.Word },
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
    public void MmapVicMemAll_EmitsExternsAndCalls()
    {
        // 全 6 binding を SLANG コード上で実呼出して CTranspiler が extern を
        // 必ず emit するパスを通す。 word/byte/void return + 0/1/3 引数 を網羅。
        var src = TranspileWithEnv("""
            MAIN() {
                VAR OLD;
                MMAP_TRAMPOLINE();
                OLD = MMAP_SET($36);
                VIC_SETMODE(0, $0400, $1000);
                VIC_SETBANK(0);
                MEMCPY($0400, $3000, 1000);
                MEMSET($0400, 32, 1000);
                MMAP_SET(OLD);
            }
            """, MakeC64EnvWithMmapVicMem(), out var diag);

        Assert.False(diag.HasErrors,
            $"errors: {string.Join("; ", diag.Diagnostics.Select(d => d.Message))}");

        // bridge header (runtime/c64/slang_memmap.h / slang_vic.h / slang_mem.h) の
        // signature と env c_bindings: が drift していないことを 6 entry すべての
        // extern 出力で確認。
        Assert.Contains("extern unsigned char slang_mmap_set(unsigned char);", src);
        Assert.Contains("extern void slang_mmap_trampoline(void);", src);
        Assert.Contains("extern void slang_vic_setmode(unsigned char, unsigned int, unsigned int);", src);
        Assert.Contains("extern void slang_vic_setbank(unsigned char);", src);
        Assert.Contains("extern void slang_memcpy(unsigned int, unsigned int, unsigned int);", src);
        Assert.Contains("extern void slang_memset(unsigned int, unsigned char, unsigned int);", src);

        // 呼出が C 関数として展開される (6 個すべて)
        Assert.Contains("slang_mmap_set(",        src);
        Assert.Contains("slang_mmap_trampoline(", src);
        Assert.Contains("slang_vic_setmode(",     src);
        Assert.Contains("slang_vic_setbank(",     src);
        Assert.Contains("slang_memcpy(",          src);
        Assert.Contains("slang_memset(",          src);
    }

    [Fact]
    public void RealC64Env_HasAllMmapVicMemBindings()
    {
        // 実 runtime/env/c64.env を EnvironmentLoader でパースして 6 entry
        // と c_runtime_files に slang_memmap.c / slang_vic.c / slang_mem.c
        // が含まれていることを確認。 手組み env では捕捉できない drift
        // (= env file 編集忘れや typo) を防ぐ。
        var repoRoot = FindRepoRoot();
        var envPath = Path.Combine(repoRoot, "runtime", "env", "c64.env");
        Assert.True(File.Exists(envPath), $"runtime/env/c64.env not found at {envPath}");

        var config = EnvironmentLoader.Load(envPath);
        Assert.Equal(BackendKind.OscarC, config.Backend);

        // c_runtime_files に 3 file
        Assert.NotNull(config.CRuntimeFiles);
        foreach (var expectedFile in new[] { "slang_memmap.c", "slang_vic.c", "slang_mem.c" })
        {
            Assert.Contains(config.CRuntimeFiles!,
                p => Path.GetFileName(p).Equals(expectedFile, StringComparison.OrdinalIgnoreCase));
        }

        // c_bindings に 6 entry がすべて含まれる
        Assert.NotNull(config.CBindings);
        var bindingNames = config.CBindings!.Select(b => b.Name).ToHashSet(StringComparer.OrdinalIgnoreCase);
        foreach (var expected in new[]
        {
            "MMAP_SET", "MMAP_TRAMPOLINE",
            "VIC_SETMODE", "VIC_SETBANK",
            "MEMCPY", "MEMSET",
        })
        {
            Assert.Contains(expected, bindingNames);
        }

        // signature (= c_name + 型) pin: 各 entry 個別
        var mmapSet = config.CBindings!.First(b => b.Name == "MMAP_SET");
        Assert.Equal("slang_mmap_set", mmapSet.CName);
        Assert.Single(mmapSet.Params);
        Assert.Equal(CBindingType.Byte, mmapSet.Params[0]);
        Assert.Equal(CBindingType.Byte, mmapSet.Return);   // 旧 mmap 値 (= 復元用)

        var mmapTramp = config.CBindings!.First(b => b.Name == "MMAP_TRAMPOLINE");
        Assert.Equal("slang_mmap_trampoline", mmapTramp.CName);
        Assert.Empty(mmapTramp.Params);
        Assert.Equal(CBindingType.Void, mmapTramp.Return);

        var vicSetmode = config.CBindings!.First(b => b.Name == "VIC_SETMODE");
        Assert.Equal("slang_vic_setmode", vicSetmode.CName);
        Assert.Equal(3, vicSetmode.Params.Count);
        Assert.Equal(CBindingType.Byte, vicSetmode.Params[0]);
        Assert.Equal(CBindingType.Word, vicSetmode.Params[1]);
        Assert.Equal(CBindingType.Word, vicSetmode.Params[2]);
        Assert.Equal(CBindingType.Void, vicSetmode.Return);

        var vicSetbank = config.CBindings!.First(b => b.Name == "VIC_SETBANK");
        Assert.Equal("slang_vic_setbank", vicSetbank.CName);
        Assert.Single(vicSetbank.Params);
        Assert.Equal(CBindingType.Byte, vicSetbank.Params[0]);
        Assert.Equal(CBindingType.Void, vicSetbank.Return);

        var memcpy = config.CBindings!.First(b => b.Name == "MEMCPY");
        Assert.Equal("slang_memcpy", memcpy.CName);
        Assert.Equal(3, memcpy.Params.Count);
        Assert.Equal(CBindingType.Word, memcpy.Params[0]);
        Assert.Equal(CBindingType.Word, memcpy.Params[1]);
        Assert.Equal(CBindingType.Word, memcpy.Params[2]);
        Assert.Equal(CBindingType.Void, memcpy.Return);

        var memset = config.CBindings!.First(b => b.Name == "MEMSET");
        Assert.Equal("slang_memset", memset.CName);
        Assert.Equal(3, memset.Params.Count);
        Assert.Equal(CBindingType.Word, memset.Params[0]);
        Assert.Equal(CBindingType.Byte, memset.Params[1]);
        Assert.Equal(CBindingType.Word, memset.Params[2]);
        Assert.Equal(CBindingType.Void, memset.Return);
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
