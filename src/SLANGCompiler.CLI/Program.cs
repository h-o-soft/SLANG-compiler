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
            // ランタイムマネージャ（存在するlibdefからランタイムを読み込み）
            var runtimeManager = new Runtime.RuntimeManager();
            LoadRuntimeLibraries(runtimeManager, baseDir);

            var codeGen = new CodeGenerator(irModule, runtimeManager);
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

        if (diagnostics.HasErrors)
        {
            diagnostics.WriteTo(Console.Error);
            return 1;
        }

        return 0;
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

    static void LoadRuntimeLibraries(Runtime.RuntimeManager manager, string baseDir)
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
