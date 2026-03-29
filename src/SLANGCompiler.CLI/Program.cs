using SLANGCompiler;
using SLANGCompiler.Lexer;
using SLANGCompiler.Parser;
using SLANGCompiler.IR;
using SLANGCompiler.CodeGen;

namespace SLANGCompiler.CLI;

class Program
{
    static int Main(string[] args)
    {
        if (args.Length == 0)
        {
            Console.Error.WriteLine("SLANG Compiler v0.13.0 (new architecture)");
            Console.Error.WriteLine("Usage: slangc [options] <input.sl>");
            Console.Error.WriteLine("Options:");
            Console.Error.WriteLine("  -o <file>    Output file path");
            Console.Error.WriteLine("  -E <env>     Environment name (default: lsx)");
            Console.Error.WriteLine("  --dump-ast   Dump AST to stdout");
            Console.Error.WriteLine("  --dump-ir    Dump IR to stdout");
            return 1;
        }

        string? outputPath = null;
        string envName = "lsx";
        bool dumpAst = false;
        bool dumpIr = false;
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
                case "--dump-ast":
                    dumpAst = true;
                    break;
                case "--dump-ir":
                    dumpIr = true;
                    break;
                default:
                    inputFiles.Add(args[i]);
                    break;
            }
        }

        if (inputFiles.Count == 0)
        {
            Console.Error.WriteLine("Error: No input files specified.");
            return 1;
        }

        var diagnostics = new DiagnosticBag();

        foreach (var filePath in inputFiles)
        {
            if (!File.Exists(filePath))
            {
                Console.Error.WriteLine($"Error: File not found: {filePath}");
                return 1;
            }

            var source = File.ReadAllText(filePath);

            // Phase 1: Lexer
            var lexer = new Lexer.Lexer(source, filePath);
            var tokens = lexer.Tokenize();

            // Phase 1.5: Preprocessor (#INCLUDE展開, #IF/#ELSE/#ENDIF評価)
            var baseDir = Path.GetDirectoryName(Path.GetFullPath(filePath)) ?? ".";
            var includePaths = new List<string> { baseDir, ".", "include" };
            var preprocessor = new Lexer.Preprocessor(diagnostics, includePaths);
            tokens = preprocessor.Process(tokens, baseDir);

            if (diagnostics.HasErrors)
            {
                diagnostics.WriteTo(Console.Error);
                return 1;
            }

            // Phase 2: Parser
            var parser = new Parser.Parser(tokens, diagnostics);
            var ast = parser.ParseCompilationUnit();

            if (diagnostics.HasErrors)
            {
                diagnostics.WriteTo(Console.Error);
                return 1;
            }

            if (dumpAst)
            {
                Console.WriteLine($"; AST for {filePath}");
                Console.WriteLine("; (AST printer not yet implemented)");
                // TODO: implement AST printer
            }

            // Phase 3: Semantic Analysis
            var analyzer = new Semantics.SemanticAnalyzer(diagnostics);
            analyzer.Analyze(ast);

            if (diagnostics.HasErrors)
            {
                diagnostics.WriteTo(Console.Error);
                return 1;
            }

            // Phase 4: IR Generation
            var irGen = new IrGenerator(diagnostics, analyzer.Symbols);
            var irModule = irGen.Generate(ast);

            if (diagnostics.HasErrors)
            {
                diagnostics.WriteTo(Console.Error);
                return 1;
            }

            if (dumpIr)
            {
                Console.WriteLine(irModule);
            }

            // Phase 5: Code Generation
            // 環境設定の読み込みとランタイムロード
            var runtimeManager = new Runtime.RuntimeManager();
            var envConfig = LoadEnvironment(envName, runtimeManager, baseDir);

            // 環境のデフォルトORG/WORKをIrModuleに反映（ソースで未指定の場合）
            if (envConfig != null)
            {
                if (!irModule.OrgAddress.HasValue && envConfig.DefaultOrg > 0)
                    irModule.OrgAddress = envConfig.DefaultOrg;
                if (!irModule.WorkAddress.HasValue && envConfig.DefaultWork > 0)
                    irModule.WorkAddress = envConfig.DefaultWork;
            }

            var codeGen = new CodeGenerator(irModule, runtimeManager, envConfig);
            var (mainAsm, overlays) = codeGen.GenerateAll();

            // Output main
            var outPath = outputPath ?? Path.ChangeExtension(filePath, ".ASM");
            File.WriteAllText(outPath, mainAsm);
            Console.Error.WriteLine($"; Output: {outPath}");

            // Output overlay modules
            if (overlays.Count > 0)
            {
                foreach (var (name, asm) in overlays)
                {
                    var overlayPath = Path.ChangeExtension(outPath, $"{name}.ASM");
                    File.WriteAllText(overlayPath, asm);
                    Console.Error.WriteLine($"; Output: {overlayPath} (overlay)");
                }

                // 共有シンボルの.incファイル生成
                var incPath = Path.ChangeExtension(outPath, ".inc");
                GenerateSharedSymbolsInc(incPath, irModule);
                Console.Error.WriteLine($"; Output: {incPath} (shared symbols)");
            }
        }

        // エラーがなくてもwarning等があれば出力
        if (diagnostics.Diagnostics.Count > 0)
            diagnostics.WriteTo(Console.Error);

        return diagnostics.HasErrors ? 1 : 0;
    }

    /// <summary>
    /// ランタイムライブラリファイル（新形式.asm）を探して読み込む
    /// </summary>
    /// <summary>
    /// 共有シンボル定義の.incファイル生成。
    /// メイン部とオーバーレイの両方からINCLUDEして使う。
    /// </summary>
    static void GenerateSharedSymbolsInc(string incPath, IR.IrModule module)
    {
        using var writer = new StreamWriter(incPath);
        writer.WriteLine("; SLANG Shared Symbols (auto-generated)");
        writer.WriteLine("; Include this file from both main and overlay ASM files.");
        writer.WriteLine();

        // グローバル変数
        writer.WriteLine("; --- Global Variables ---");
        foreach (var gv in module.GlobalVars)
        {
            if (gv.FixedAddress.HasValue)
                writer.WriteLine($"{gv.AsmLabel}\tEQU\t${gv.FixedAddress.Value:X4}");
            else
                writer.WriteLine($"; {gv.AsmLabel}\t; address assigned by linker/assembler");
        }

        // メイン部の関数
        writer.WriteLine();
        writer.WriteLine("; --- Functions ---");
        foreach (var func in module.Functions)
        {
            writer.WriteLine($"; {func.Name}\t; defined in main");
        }

        // オーバーレイの関数
        foreach (var overlay in module.Overlays)
        {
            foreach (var func in overlay.Functions)
            {
                writer.WriteLine($"; {func.Name}\t; defined in overlay {overlay.Index}");
            }
        }

        // 文字列テーブル
        if (module.StringTable.Count > 0)
        {
            writer.WriteLine();
            writer.WriteLine("; --- String Labels ---");
            foreach (var label in module.StringTable.Keys)
                writer.WriteLine($"; {label}\t; string data in main");
        }
    }

    /// <summary>
    /// 環境設定(.env)を読み込み、指定されたランタイムライブラリをロード
    /// </summary>
    static Runtime.EnvironmentConfig? LoadEnvironment(string envName, Runtime.RuntimeManager manager, string baseDir)
    {
        // .envファイルを探す
        var envSearchDirs = new[] {
            Path.Combine(baseDir, "lib", "env"),
            "lib/env",
        };

        foreach (var dir in envSearchDirs)
        {
            var envPath = Path.Combine(dir, $"{envName}.env");
            if (File.Exists(envPath))
            {
                try
                {
                    var config = Runtime.EnvironmentLoader.Load(envPath);
                    Console.Error.WriteLine($"; Environment: {config.Name} (type={config.EnvType})");

                    // 環境が指定するライブラリをロード
                    var runtimeDir = Path.Combine(Path.GetDirectoryName(envPath) ?? ".", "..", "..", "runtime");
                    var altRuntimeDir = "runtime";
                    foreach (var lib in config.Libraries)
                    {
                        var libPath = Path.Combine(runtimeDir, lib);
                        if (!File.Exists(libPath))
                            libPath = Path.Combine(altRuntimeDir, lib);
                        if (File.Exists(libPath))
                        {
                            manager.LoadFromFile(libPath);
                        }
                        else
                        {
                            Console.Error.WriteLine($"; Warning: Runtime not found: {lib}");
                        }
                    }

                    return config;
                }
                catch (Exception ex)
                {
                    Console.Error.WriteLine($"; Warning: Failed to load env {envPath}: {ex.Message}");
                }
            }
        }

        // .envが見つからない場合はruntimeディレクトリから直接ロード
        LoadRuntimeLibrariesFromDir(manager, baseDir);
        return null;
    }

    static void LoadRuntimeLibrariesFromDir(Runtime.RuntimeManager manager, string baseDir)
    {
        // 新形式の.asmランタイムファイルを探す
        var searchDirs = new[] {
            Path.Combine(baseDir, "lib", "runtime"),
            Path.Combine(baseDir, "runtime"),
            "lib/runtime",
            "runtime",
        };

        foreach (var dir in searchDirs)
        {
            if (!Directory.Exists(dir)) continue;
            foreach (var file in Directory.GetFiles(dir, "*.asm"))
            {
                try
                {
                    manager.LoadFromFile(file);
                }
                catch (Exception ex)
                {
                    Console.Error.WriteLine($"; Warning: Failed to load runtime {file}: {ex.Message}");
                }
            }
        }
    }
}
