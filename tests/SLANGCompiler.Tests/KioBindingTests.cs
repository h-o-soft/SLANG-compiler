using SLANGCompiler;
using SLANGCompiler.CodeGen.C;
using SLANGCompiler.Lexer;
using SLANGCompiler.Parser;
using SLANGCompiler.Runtime;
using SLANGCompiler.Semantics;
using Xunit;

namespace SLANGCompiler.Tests;

/// <summary>
/// v3c KERNAL file I/O (KIO_*) binding の signature drift / 呼出 emit を golden 化する。
/// env c_bindings: と bridge header (runtime/c64/slang_kio.h) の一致確認も兼ねる。
/// 主要型 (byte / word / byte_ptr / void) を網羅し、CTranspiler 出力の
/// extern と bridge 実装の signature が drift しないことを CI で検出可能にする。
/// </summary>
public class KioBindingTests
{
    private static EnvironmentConfig MakeC64EnvWithKio()
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
                new() { Name = "KIO_SETNAM", CName = "slang_kio_setnam",
                        Params = new List<CBindingType> { CBindingType.BytePtr },
                        Return = CBindingType.Void },
                new() { Name = "KIO_OPEN", CName = "slang_kio_open",
                        Params = new List<CBindingType> { CBindingType.Byte, CBindingType.Byte, CBindingType.Byte },
                        Return = CBindingType.Word },
                new() { Name = "KIO_OPEN_NAMED", CName = "slang_kio_open_named",
                        Params = new List<CBindingType> { CBindingType.Byte, CBindingType.Byte, CBindingType.Byte, CBindingType.BytePtr },
                        Return = CBindingType.Word },
                new() { Name = "KIO_CLOSE", CName = "slang_kio_close",
                        Params = new List<CBindingType> { CBindingType.Byte },
                        Return = CBindingType.Void },
                new() { Name = "KIO_CHKIN", CName = "slang_kio_chkin",
                        Params = new List<CBindingType> { CBindingType.Byte },
                        Return = CBindingType.Word },
                new() { Name = "KIO_CHKOUT", CName = "slang_kio_chkout",
                        Params = new List<CBindingType> { CBindingType.Byte },
                        Return = CBindingType.Word },
                new() { Name = "KIO_CLRCHN", CName = "slang_kio_clrchn",
                        Params = new List<CBindingType>(),
                        Return = CBindingType.Void },
                new() { Name = "KIO_CHRIN", CName = "slang_kio_chrin",
                        Params = new List<CBindingType>(),
                        Return = CBindingType.Word },
                new() { Name = "KIO_READ", CName = "slang_kio_read",
                        Params = new List<CBindingType> { CBindingType.Byte, CBindingType.BytePtr, CBindingType.Word },
                        Return = CBindingType.Word },
                new() { Name = "KIO_WRITE", CName = "slang_kio_write",
                        Params = new List<CBindingType> { CBindingType.Byte, CBindingType.BytePtr, CBindingType.Word },
                        Return = CBindingType.Word },
                new() { Name = "KIO_STATUS", CName = "slang_kio_status",
                        Params = new List<CBindingType>(),
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
    public void KioAll_EmitsExternsAndCalls()
    {
        // ARRAY と StringLiteral を byte_ptr binding に渡す典型パターン、
        // および void return / word return / 多引数 binding を網羅する SLANG。
        var src = TranspileWithEnv("""
            ARRAY BYTE BUF[16];
            MAIN() {
                VAR OK, N, ST;
                KIO_SETNAM("HISCORE,S,W");
                OK = KIO_OPEN(2, 8, 1);
                OK = KIO_OPEN_NAMED(2, 8, 0, "HISCORE,S,R");
                IF KIO_CHKIN(2) THEN N = 1;
                IF KIO_CHKOUT(2) THEN N = 1;
                N = KIO_READ(2, BUF, 16);
                N = KIO_WRITE(2, BUF, 16);
                KIO_CLRCHN();
                N = KIO_CHRIN();
                ST = KIO_STATUS();
                KIO_CLOSE(2);
            }
            """, MakeC64EnvWithKio(), out var diag);

        Assert.False(diag.HasErrors,
            $"errors: {string.Join("; ", diag.Diagnostics.Select(d => d.Message))}");

        // bridge header (runtime/c64/slang_kio.h) の signature と env c_bindings: が
        // drift していないことを extern 出力で確認。byte_ptr は `unsigned char *`、
        // word は `unsigned int`、byte は `unsigned char`、void は `void` にマッピング。
        Assert.Contains("extern void slang_kio_setnam(unsigned char *);", src);
        Assert.Contains("extern unsigned int slang_kio_open(unsigned char, unsigned char, unsigned char);", src);
        Assert.Contains("extern unsigned int slang_kio_open_named(unsigned char, unsigned char, unsigned char, unsigned char *);", src);
        Assert.Contains("extern void slang_kio_close(unsigned char);", src);
        Assert.Contains("extern unsigned int slang_kio_chkin(unsigned char);", src);
        Assert.Contains("extern unsigned int slang_kio_chkout(unsigned char);", src);
        Assert.Contains("extern void slang_kio_clrchn(void);", src);
        Assert.Contains("extern unsigned int slang_kio_chrin(void);", src);
        Assert.Contains("extern unsigned int slang_kio_read(unsigned char, unsigned char *, unsigned int);", src);
        Assert.Contains("extern unsigned int slang_kio_write(unsigned char, unsigned char *, unsigned int);", src);
        Assert.Contains("extern unsigned int slang_kio_status(void);", src);

        // 呼出が C 関数として展開される
        Assert.Contains("slang_kio_setnam(", src);
        Assert.Contains("slang_kio_open(",   src);
        Assert.Contains("slang_kio_open_named(", src);
        Assert.Contains("slang_kio_chkin(",  src);
        Assert.Contains("slang_kio_chkout(", src);
        Assert.Contains("slang_kio_clrchn(", src);
        Assert.Contains("slang_kio_chrin(",  src);
        Assert.Contains("slang_kio_read(",   src);
        Assert.Contains("slang_kio_write(",  src);
        Assert.Contains("slang_kio_status(", src);
        Assert.Contains("slang_kio_close(",  src);
    }

    [Fact]
    public void KioStatus_FFFFCompare_GeneratedCorrectly()
    {
        // 戻り値の error 判定パターン: SLANG WORD の sign-extended $FFFF (= signed -1)
        // を識別子相当の literal `0xFFFFu` で展開できることを確認。
        var src = TranspileWithEnv("""
            MAIN() {
                VAR N;
                N = KIO_READ(2, 0, 0);
                IF (N & $8000) THEN N = 0;
                IF N == $FFFF THEN N = 1;
            }
            """, MakeC64EnvWithKio(), out var diag);
        Assert.False(diag.HasErrors,
            $"errors: {string.Join("; ", diag.Diagnostics.Select(d => d.Message))}");
        Assert.Contains("0xFFFFu", src);
    }

    [Fact]
    public void RealC64Env_HasAllKioBindings()
    {
        // 実 runtime/env/c64.env を EnvironmentLoader でパースして KIO_* entry
        // と c_runtime_files に slang_kio.c が含まれていることを確認。
        // 手組み env では捕捉できない drift (= env file 編集忘れや typo) を防ぐ。
        var repoRoot = FindRepoRoot();
        var envPath = Path.Combine(repoRoot, "runtime", "env", "c64.env");
        Assert.True(File.Exists(envPath), $"runtime/env/c64.env not found at {envPath}");

        var config = EnvironmentLoader.Load(envPath);
        Assert.Equal(BackendKind.OscarC, config.Backend);

        // c_runtime_files に slang_kio.c
        Assert.NotNull(config.CRuntimeFiles);
        Assert.Contains(config.CRuntimeFiles!,
            p => Path.GetFileName(p).Equals("slang_kio.c", StringComparison.OrdinalIgnoreCase));

        // c_bindings に主要 KIO_* entry がすべて含まれる (= byte_ptr / *_ADDR 含む)
        Assert.NotNull(config.CBindings);
        var bindingNames = config.CBindings!.Select(b => b.Name).ToHashSet(StringComparer.OrdinalIgnoreCase);
        foreach (var expected in new[]
        {
            "KIO_SETNAM", "KIO_OPEN", "KIO_OPEN_NAMED", "KIO_CLOSE",
            "KIO_CHKIN", "KIO_CHKOUT", "KIO_CLRCHN",
            "KIO_CHRIN", "KIO_CHROUT", "KIO_GETCH", "KIO_PUTCH",
            "KIO_READ", "KIO_WRITE", "KIO_PUTS", "KIO_GETS",
            "KIO_STATUS",
            "KIO_SETNAM_ADDR", "KIO_OPEN_NAMED_ADDR",
            "KIO_READ_ADDR", "KIO_WRITE_ADDR",
        })
        {
            Assert.Contains(expected, bindingNames);
        }

        // signature (= c_name + 型) ピン留め: byte_ptr 主と word raw の両系統
        var kioOpenNamed = config.CBindings!.First(b => b.Name == "KIO_OPEN_NAMED");
        Assert.Equal("slang_kio_open_named", kioOpenNamed.CName);
        Assert.Equal(4, kioOpenNamed.Params.Count);
        Assert.Equal(CBindingType.Byte,    kioOpenNamed.Params[0]);
        Assert.Equal(CBindingType.Byte,    kioOpenNamed.Params[1]);
        Assert.Equal(CBindingType.Byte,    kioOpenNamed.Params[2]);
        Assert.Equal(CBindingType.BytePtr, kioOpenNamed.Params[3]);
        Assert.Equal(CBindingType.Word,    kioOpenNamed.Return);

        var kioWrite = config.CBindings!.First(b => b.Name == "KIO_WRITE");
        Assert.Equal("slang_kio_write", kioWrite.CName);
        Assert.Equal(3, kioWrite.Params.Count);
        Assert.Equal(CBindingType.BytePtr, kioWrite.Params[1]);

        var kioClrchn = config.CBindings!.First(b => b.Name == "KIO_CLRCHN");
        Assert.Equal("slang_kio_clrchn", kioClrchn.CName);
        Assert.Empty(kioClrchn.Params);
        Assert.Equal(CBindingType.Void, kioClrchn.Return);

        var kioStatus = config.CBindings!.First(b => b.Name == "KIO_STATUS");
        Assert.Equal("slang_kio_status", kioStatus.CName);
        Assert.Empty(kioStatus.Params);
        Assert.Equal(CBindingType.Word, kioStatus.Return);

        // raw address 版は word
        var kioReadAddr = config.CBindings!.First(b => b.Name == "KIO_READ_ADDR");
        Assert.Equal("slang_kio_read_addr", kioReadAddr.CName);
        Assert.Equal(3, kioReadAddr.Params.Count);
        Assert.Equal(CBindingType.Word, kioReadAddr.Params[1]);
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
