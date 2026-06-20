using SLANGCompiler;
using SLANGCompiler.Lexer;
using SLANGCompiler.Parser;
using SLANGCompiler.IR;
using SLANGCompiler.CodeGen;
using SLANGCompiler.CodeGen.C;
using SLANGCompiler.Runtime;

namespace SLANGCompiler.CLI;

class Program
{
    const string Version = "0.27.0";

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
        // 解決失敗は即エラー。後段の runtime ロードと preprocessor の ENV_TYPE/OS_TYPE 定義を同じ config から行う。
        // 「ファイルが無い」と「ファイルはあるが壊れている」を区別してエラー表示。
        (Runtime.EnvironmentConfig Config, string EnvPath)? envResolution;
        try
        {
            envResolution = ResolveEnvironment(envName, pathResolver);
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine(
                $"Error: Failed to load env file for '{envName}': {ex.Message}");
            return 1;
        }
        if (envResolution == null)
        {
            Console.Error.WriteLine(
                $"Error: Unknown environment '{envName}'. " +
                $"No '{envName}.env' found in runtime/env/ or lib/env/.");
            return 1;
        }
        var (envConfig, envPath) = envResolution.Value;
        Console.Error.WriteLine($"; Environment: {envConfig.Name} (type={envConfig.EnvType}) [{envPath}]");

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

            // 環境定数をプリプロセッサに登録（#IF条件式で参照可能）
            // 前段で解決済みの envConfig をそのまま使用（二重解決を回避）
            preprocessor.DefineConst("ENV_TYPE", envConfig.EnvType);
            preprocessor.DefineConst("OS_TYPE", envConfig.OsType);
            // BACKEND: SLANG コードが Z80 / OscarC 別の実装を gate できるようにする
            // (= MACHINE / inline #ASM を含むファイルは BACKEND==1 で除外する想定)。
            // 値は <see cref="BackendKind"/>: 0=Z80, 1=OscarC。
            preprocessor.DefineConst("BACKEND", (int)envConfig.Backend);

            // env file の `defines:` で定義された名前を Preprocessor に注入
            // (= 例: pc80mk2xsd で PC8001_SD=1 が定義されると、SL 側の
            // `#IF PC8001_SD==1` が有効化される。ユーザーが SL に
            // `CONST ASM PC8001_SD = 1;` を書かなくても済む)。
            // 同時に slangbuild が AILZ80ASM 起動時に `-dl K=V` も pass する
            // ので、ASM 側の `#IF exists NAME` も活きる。
            if (envConfig.Defines != null)
            {
                foreach (var (name, value) in envConfig.Defines)
                    preprocessor.DefineConst(name, value);
            }

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

            // === Phase 3.5: Backend dispatch ===
            // OscarC backend は IR / RuntimeManager (Z80 専用) を通らず、
            // CTranspiler が AST 直接 → C source を生成する。
            // oscar64 invoke は slangc では行わない (= slangbuild 側の責務、
            // memory: slangc-vs-slangbuild-responsibility 参照)。
            if (envConfig.Backend == BackendKind.OscarC)
            {
                var transpiler = new CTranspiler(analyzer.Symbols, envConfig, diagnostics);
                var cSource = transpiler.Transpile(ast);

                if (diagnostics.HasErrors)
                {
                    diagnostics.WriteTo(Console.Error);
                    return 1;
                }

                var cOutPath = outputPath ?? Path.ChangeExtension(filePath, ".c");
                File.WriteAllText(cOutPath, cSource);
                Console.Error.WriteLine($"; Output: {cOutPath}");
                continue;  // OscarC では overlay / RuntimePlan は使わない
            }

            // Load runtime (needed for IR generation to know function return types)
            // 前段で解決済みの envConfig が指定する .asm 群をロード
            var runtimeManager = new Runtime.RuntimeManager();
            LoadRuntimeFromConfig(envConfig, runtimeManager, pathResolver);

            // Phase 4: IR Generation
            var irGen = new IrGenerator(diagnostics, analyzer.Symbols, runtimeManager);
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

            if (!irModule.OrgAddress.HasValue && envConfig.DefaultOrg > 0)
                irModule.OrgAddress = envConfig.DefaultOrg;
            if (!irModule.WorkAddress.HasValue && envConfig.DefaultWork > 0)
                irModule.WorkAddress = envConfig.DefaultWork;

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
                GenerateSharedSymbolsInc(incPath, irModule, codeGen.RuntimePlan, runtimeManager);
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

    static void GenerateSharedSymbolsInc(string incPath, IR.IrModule module,
        Runtime.RuntimePlan? plan = null, Runtime.RuntimeManager? runtime = null)
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

        // --- Shared Runtime Functions (resident in main) ---
        // PR-A の plan にある main resident な runtime 関数とその alias を export 候補
        // としてリストアップする。AILZ80ASM 側に EXTERN は無いため現状はコメントだが、
        // PR-B (二段アセンブル toolchain) が main の .sym を読んで overlay ASM に
        // EQU 注入する際の入力リストとして使う。
        if (plan != null && runtime != null && plan.MainResidentFunctions.Count > 0)
        {
            writer.WriteLine();
            writer.WriteLine("; --- Shared Runtime Functions (resident in main) ---");
            foreach (var name in plan.MainResidentFunctions.OrderBy(n => n,
                         StringComparer.OrdinalIgnoreCase))
            {
                if (!runtime.Functions.TryGetValue(name, out var func)) continue;
                var ns = func.LibName != null ? $" (lib={func.LibName})" : "";
                writer.WriteLine($"; {name}{ns}\t; main resident, addr resolved by toolchain (PR-B)");
                // alias 名も export 候補に並べる (overlay コードが alias で呼ぶ場合に備える)
                foreach (var alias in func.Aliases)
                    writer.WriteLine($"; {alias}{ns}\t; alias of {name}");
            }
        }
    }

    /// <summary>
    /// 環境名から .env を解決し、(EnvironmentConfig, envファイルの絶対パス) を返す。
    /// 検索順 (runtime/env/ → lib/env/) は <see cref="Runtime.EnvironmentResolver"/>
    /// に集約済み。slangbuild 側も同じ resolver を使用することで挙動が一致する。
    /// </summary>
    static (Runtime.EnvironmentConfig Config, string EnvPath)? ResolveEnvironment(
        string envName, PathResolver paths)
    {
        var envSearchPaths = paths.GetRuntimePaths().Concat(paths.GetLibPaths());
        return Runtime.EnvironmentResolver.Resolve(envName, envSearchPaths);
    }

    /// <summary>
    /// 解決済み EnvironmentConfig が指定するランタイムライブラリ (.asm) をロード。
    /// 旧版にあった「env 不在時の全 runtime/*.asm fallback」は廃止 (前段で必ず解決される前提)。
    /// </summary>
    static void LoadRuntimeFromConfig(
        Runtime.EnvironmentConfig config, Runtime.RuntimeManager manager, PathResolver paths)
    {
        LoadRuntimeLibraries(config.Libraries, manager, paths);
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

// PathResolver は SLANGCompiler.Runtime に移動 (slangbuild と共有)。
