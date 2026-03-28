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
            var irGen = new IrGenerator(diagnostics);
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
            var codeGen = new CodeGenerator(irModule);
            var asmOutput = codeGen.Generate();

            // Output
            var outPath = outputPath ?? Path.ChangeExtension(filePath, ".ASM");
            File.WriteAllText(outPath, asmOutput);
            Console.Error.WriteLine($"; Output: {outPath}");
        }

        if (diagnostics.HasErrors)
        {
            diagnostics.WriteTo(Console.Error);
            return 1;
        }

        return 0;
    }
}
