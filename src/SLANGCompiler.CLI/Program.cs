using SLANGCompiler;
using SLANGCompiler.Lexer;
using SLANGCompiler.Parser;
using SLANGCompiler.IR;
using SLANGCompiler.CodeGen;

namespace SLANGCompiler.CLI;

class Program
{
    const string Version = "0.13.0";

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

        var diagnostics = new DiagnosticBag();

        foreach (var filePath in inputFiles)
        {
            if (!File.Exists(filePath))
            {
                Console.Error.WriteLine($"Error: File not found: {filePath}");
                return 1;
            }

            var source = File.ReadAllText(filePath);
            var baseDir = Path.GetDirectoryName(Path.GetFullPath(filePath)) ?? ".";

            // Phase 1: Lexer
            var lexer = new Lexer.Lexer(source, filePath);
            var tokens = lexer.Tokenize();

            // Phase 1.5: Preprocessor (#INCLUDE展開, #IF/#ELSE/#ENDIF評価)
            var includePaths = pathResolver.GetIncludePaths(baseDir);
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
            var runtimeManager = new Runtime.RuntimeManager();
            var envConfig = LoadEnvironment(envName, runtimeManager, pathResolver);

            if (envConfig != null)
            {
                if (!irModule.OrgAddress.HasValue && envConfig.DefaultOrg > 0)
                    irModule.OrgAddress = envConfig.DefaultOrg;
                if (!irModule.WorkAddress.HasValue && envConfig.DefaultWork > 0)
                    irModule.WorkAddress = envConfig.DefaultWork;
            }

            var codeGen = new CodeGenerator(irModule, runtimeManager, envConfig, diagnostics);
            var (mainAsm, overlays) = codeGen.GenerateAll();

            if (diagnostics.HasErrors)
            {
                diagnostics.WriteTo(Console.Error);
                return 1;
            }

            // Output
            var outPath = outputPath ?? Path.ChangeExtension(filePath, ".ASM");
            File.WriteAllText(outPath, mainAsm);
            Console.Error.WriteLine($"; Output: {outPath}");

            if (overlays.Count > 0)
            {
                foreach (var (name, asm) in overlays)
                {
                    var overlayPath = Path.ChangeExtension(outPath, $"{name}.ASM");
                    File.WriteAllText(overlayPath, asm);
                    Console.Error.WriteLine($"; Output: {overlayPath} (overlay)");
                }

                var incPath = Path.ChangeExtension(outPath, ".inc");
                GenerateSharedSymbolsInc(incPath, irModule);
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

    static void GenerateSharedSymbolsInc(string incPath, IR.IrModule module)
    {
        using var writer = new StreamWriter(incPath);
        writer.WriteLine("; SLANG Shared Symbols (auto-generated)");
        writer.WriteLine("; Include this file from both main and overlay ASM files.");
        writer.WriteLine();

        writer.WriteLine("; --- Global Variables ---");
        foreach (var gv in module.GlobalVars)
        {
            if (gv.FixedAddress.HasValue)
                writer.WriteLine($"{gv.AsmLabel}\tEQU\t${gv.FixedAddress.Value:X4}");
            else
                writer.WriteLine($"; {gv.AsmLabel}\t; address assigned by linker/assembler");
        }

        writer.WriteLine();
        writer.WriteLine("; --- Functions ---");
        foreach (var func in module.Functions)
            writer.WriteLine($"; {func.Name}\t; defined in main");

        foreach (var overlay in module.Overlays)
            foreach (var func in overlay.Functions)
                writer.WriteLine($"; {func.Name}\t; defined in overlay {overlay.Index}");

        if (module.StringTable.Count > 0)
        {
            writer.WriteLine();
            writer.WriteLine("; --- String Labels ---");
            foreach (var label in module.StringTable.Keys)
                writer.WriteLine($"; {label}\t; string data in main");
        }
    }

    static Runtime.EnvironmentConfig? LoadEnvironment(
        string envName, Runtime.RuntimeManager manager, PathResolver paths)
    {
        // .envファイルを検索
        var envFile = $"{envName}.env";
        foreach (var dir in paths.GetLibPaths())
        {
            var envPath = Path.Combine(dir, "env", envFile);
            if (!File.Exists(envPath)) continue;

            try
            {
                var config = Runtime.EnvironmentLoader.Load(envPath);
                Console.Error.WriteLine($"; Environment: {config.Name} (type={config.EnvType})");

                // 環境が指定するランタイムライブラリをロード
                LoadRuntimeLibraries(config.Libraries, manager, paths);
                return config;
            }
            catch (Exception ex)
            {
                Console.Error.WriteLine($"; Warning: Failed to load env {envPath}: {ex.Message}");
            }
        }

        // .envが見つからない場合: runtimeディレクトリから直接ロード
        Console.Error.WriteLine($"; Warning: Environment '{envName}' not found, loading runtime directly");
        foreach (var dir in paths.GetRuntimePaths())
        {
            if (!Directory.Exists(dir)) continue;
            foreach (var file in Directory.GetFiles(dir, "*.asm"))
            {
                try { manager.LoadFromFile(file); }
                catch (Exception ex)
                {
                    Console.Error.WriteLine($"; Warning: Failed to load {file}: {ex.Message}");
                }
            }
        }
        return null;
    }

    static void LoadRuntimeLibraries(
        IEnumerable<string> libraries, Runtime.RuntimeManager manager, PathResolver paths)
    {
        foreach (var lib in libraries)
        {
            // .yml → .asm 読み替え（旧.envとの互換性）
            var asmLib = lib.EndsWith(".yml", StringComparison.OrdinalIgnoreCase)
                ? Path.ChangeExtension(lib, ".asm")
                : lib;

            bool found = false;
            foreach (var dir in paths.GetRuntimePaths())
            {
                var libPath = Path.Combine(dir, asmLib);
                if (File.Exists(libPath))
                {
                    manager.LoadFromFile(libPath);
                    found = true;
                    break;
                }
            }
            if (!found)
                Console.Error.WriteLine($"; Warning: Runtime not found: {asmLib}");
        }
    }
}

/// <summary>
/// インクルード/ライブラリ/ランタイムのパス解決。
/// 検索順: ソースディレクトリ → -I/-L指定 → $SLANG_HOME → ~/.config/SLANG → <compiler>/../share/slang
/// </summary>
class PathResolver
{
    private readonly List<string> _extraIncludePaths;
    private readonly List<string> _extraLibPaths;
    private readonly List<string> _defaultPaths;

    /// <summary>ユーザー設定ディレクトリ（~/.config/SLANG）</summary>
    public static string UserConfigDir =>
        Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), ".config", "SLANG");

    public PathResolver(List<string> extraIncludePaths, List<string> extraLibPaths)
    {
        _extraIncludePaths = extraIncludePaths;
        _extraLibPaths = extraLibPaths;
        _defaultPaths = BuildDefaultPaths();
    }

    /// <summary>デフォルト検索パスを構築</summary>
    private static List<string> BuildDefaultPaths()
    {
        var paths = new List<string>();

        // $SLANG_HOME
        var slangHome = Environment.GetEnvironmentVariable("SLANG_HOME");
        if (!string.IsNullOrEmpty(slangHome) && Directory.Exists(slangHome))
            paths.Add(slangHome);

        // ~/.config/SLANG
        var configDir = UserConfigDir;
        if (Directory.Exists(configDir))
            paths.Add(configDir);

        // <compiler_executable>/../share/slang （システムインストール）
        var exeDir = Path.GetDirectoryName(Environment.ProcessPath);
        if (exeDir != null)
        {
            var shareDir = Path.Combine(exeDir, "..", "share", "slang");
            var resolved = Path.GetFullPath(shareDir);
            if (Directory.Exists(resolved))
                paths.Add(resolved);
        }

        return paths;
    }

    /// <summary>#INCLUDE用の検索パスリスト</summary>
    public List<string> GetIncludePaths(string sourceDir)
    {
        var paths = new List<string>();
        paths.Add(sourceDir);                   // 1. ソースファイルのディレクトリ
        paths.AddRange(_extraIncludePaths);      // 2. -I で指定されたパス
        foreach (var d in _defaultPaths)         // 3-5. デフォルトパス
            paths.Add(Path.Combine(d, "include"));
        return paths;
    }

    /// <summary>lib/（env定義ファイル等）の検索パスリスト</summary>
    public List<string> GetLibPaths()
    {
        var paths = new List<string>();
        paths.Add("lib");                        // CWDのlib（開発時）
        paths.AddRange(_extraLibPaths);          // -L で指定されたパス
        foreach (var d in _defaultPaths)
            paths.Add(Path.Combine(d, "lib"));
        return paths;
    }

    /// <summary>ランタイム(.asm)の検索パスリスト</summary>
    public List<string> GetRuntimePaths()
    {
        var paths = new List<string>();
        paths.Add("runtime");                    // CWDのruntime（開発時）
        foreach (var lp in _extraLibPaths)
        {
            paths.Add(lp);                       // -Lパス直接（runtime/を直接指定した場合）
            paths.Add(Path.Combine(lp, "runtime")); // -L配下のruntime/
        }
        foreach (var d in _defaultPaths)
            paths.Add(Path.Combine(d, "runtime"));
        return paths;
    }
}
