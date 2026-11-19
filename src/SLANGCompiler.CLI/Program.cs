using SLANGCompiler;
using SLANGCompiler.Lexer;
using SLANGCompiler.Parser;
using SLANGCompiler.IR;
using SLANGCompiler.CodeGen;
using SLANGCompiler.CodeGen.C;
using SLANGCompiler.Runtime;
using SLANGCompiler.Compilation;

namespace SLANGCompiler.CLI;

class Program
{
    const string Version = SlangVersion.Version;

    static int Main(string[] args)
    {
        if (args.Length == 0 || args.Contains("--help") || args.Contains("-h"))
        {
            PrintUsage();
            return args.Length == 0 ? 1 : 0;
        }

        if (args.Contains("--version"))
        {
            Console.WriteLine($"slangc {Version}");
            return 0;
        }

        // --- オプション解析 ---
        string? outputPath = null;
        string envName = "lsx";
        bool dumpAst = false;
        bool dumpIr = false;
        var extraIncludePaths = new List<string>();
        var extraLibPaths = new List<string>();
        var inputFiles = new List<string>();

        for (int i = 0; i < args.Length; i++)
        {
            switch (args[i])
            {
                case "-o" when i + 1 < args.Length:
                    outputPath = args[++i];
                    break;
                case "-E" when i + 1 < args.Length:
                    envName = args[++i];
                    break;
                case "-I" when i + 1 < args.Length:
                    extraIncludePaths.Add(args[++i]);
                    break;
                case "-L" when i + 1 < args.Length:
                    extraLibPaths.Add(args[++i]);
                    break;
                case "--dump-ast":
                    dumpAst = true;
                    break;
                case "--dump-ir":
                    dumpIr = true;
                    break;
                default:
                    if (args[i].StartsWith("-"))
                    {
                        Console.Error.WriteLine($"Error: Unknown option: {args[i]}");
                        return 1;
                    }
                    inputFiles.Add(args[i]);
                    break;
            }
        }

        if (inputFiles.Count == 0)
        {
            Console.Error.WriteLine("Error: No input files specified.");
            return 1;
        }

        // --- パス解決 ---
        var pathResolver = new PathResolver(extraIncludePaths, extraLibPaths);

        // --- 環境解決（前段で 1 回だけ）---
        // 解決失敗は即エラー (「ファイルが無い」と「ファイルはあるが壊れている」を区別した文は SlangCompilation が書く)。
        // 後段の runtime ロードと preprocessor の ENV_TYPE/OS_TYPE 定義を同じ config から行う。
        var envResolution = SlangCompilation.ResolveEnvironment(envName, pathResolver, Console.Error);
        if (envResolution == null) return 1;
        var (envConfig, envPath) = envResolution.Value;
        Console.Error.WriteLine($"; Environment: {envConfig.Name} (type={envConfig.EnvType}) [{envPath}]");

        var diagnostics = new DiagnosticBag();
        var compileOptions = new SlangCompileOptions(dumpAst, dumpIr, Console.Out);

        foreach (var filePath in inputFiles)
        {
            if (!File.Exists(filePath))
            {
                Console.Error.WriteLine($"Error: File not found: {filePath}");
                return 1;
            }

            // コンパイルの手順はブラウザ向けの入口と共通 (SlangCompilation)。診断などの文は同じ順で stderr に書く
            var result = SlangCompilation.CompileFile(filePath, envConfig, pathResolver, diagnostics, Console.Error, compileOptions);
            if (!result.Ok) return 1;

            // OscarC backend: C source を書く (oscar64 invoke は slangbuild の責務)。overlay / RuntimePlan は使わない
            if (result.CSource != null)
            {
                var cOutPath = outputPath ?? Path.ChangeExtension(filePath, ".c");
                File.WriteAllText(cOutPath, result.CSource);
                Console.Error.WriteLine($"; Output: {cOutPath}");
                continue;
            }

            // Output
            var outPath = outputPath ?? Path.ChangeExtension(filePath, ".ASM");
            File.WriteAllText(outPath, result.MainAsm!);
            Console.Error.WriteLine($"; Output: {outPath}");

            // PAGE のあるモジュールの表 (slangbuild が組んだ後に、窓の大きさと本体との重なりを調べる)。PAGE が無ければ書かず、前の残りは消す
            var modulesPath = Path.ChangeExtension(outPath, ".modules");
            if (result.ModulesLines != null)
            {
                File.WriteAllLines(modulesPath, result.ModulesLines);
                Console.Error.WriteLine($"; Output: {modulesPath} (module pages)");
            }
            else if (File.Exists(modulesPath))
                File.Delete(modulesPath);

            if (result.Overlays.Count > 0)
            {
                foreach (var (name, asm) in result.Overlays)
                {
                    var overlayPath = Path.ChangeExtension(outPath, $"{name}.ASM");
                    File.WriteAllText(overlayPath, asm);
                    Console.Error.WriteLine($"; Output: {overlayPath} (overlay)");
                }

                var incPath = Path.ChangeExtension(outPath, ".inc");
                File.WriteAllText(incPath, result.SharedSymbolsInc!);
                Console.Error.WriteLine($"; Output: {incPath} (shared symbols)");
            }
        }

        if (diagnostics.Diagnostics.Count > 0)
            diagnostics.WriteTo(Console.Error);

        return diagnostics.HasErrors ? 1 : 0;
    }

    static void PrintUsage()
    {
        Console.Error.WriteLine($"SLANG Compiler v{Version}");
        Console.Error.WriteLine("Usage: slangc [options] <input.sl>");
        Console.Error.WriteLine();
        Console.Error.WriteLine("Options:");
        Console.Error.WriteLine("  -o <file>       Output file path");
        Console.Error.WriteLine("  -E <env>        Environment name (default: lsx)");
        Console.Error.WriteLine("  -I <path>       Add include search path (repeatable)");
        Console.Error.WriteLine("  -L <path>       Add library search path (repeatable)");
        Console.Error.WriteLine("  --dump-ast      Dump AST to stdout");
        Console.Error.WriteLine("  --dump-ir       Dump IR to stdout");
        Console.Error.WriteLine("  -h, --help      Show this help");
        Console.Error.WriteLine("  --version       Show version");
        Console.Error.WriteLine();
        Console.Error.WriteLine("Search paths (in order):");
        Console.Error.WriteLine("  1. Source file directory");
        Console.Error.WriteLine("  2. Paths from -I / -L flags");
        Console.Error.WriteLine("  3. $SLANG_HOME/{include,lib,runtime}");
        Console.Error.WriteLine($"  4. {PathResolver.UserConfigDir}/");
        Console.Error.WriteLine("  5. <compiler_dir>/../share/slang/");
    }
}

// PathResolver は SLANGCompiler.Runtime に移動 (slangbuild と共有)。
