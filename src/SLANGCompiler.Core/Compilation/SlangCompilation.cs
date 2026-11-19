using SLANGCompiler.CodeGen;
using SLANGCompiler.CodeGen.C;
using SLANGCompiler.IR;
using SLANGCompiler.Runtime;

namespace SLANGCompiler.Compilation;

/// <summary>1 つのソースをコンパイルした結果 (ファイルには書かない。書くのは CLI)</summary>
public sealed class SlangCompileResult
{
    /// <summary>エラーなしに終わったか</summary>
    public bool Ok { get; init; }
    /// <summary>本体の ASM (Z80 の env で成功したとき)</summary>
    public string? MainAsm { get; init; }
    /// <summary>overlay (#MODULE) の ASM。名前は CLI が `出力.名前.ASM` に使うもの</summary>
    public IReadOnlyList<(string Name, string Asm)> Overlays { get; init; } = Array.Empty<(string, string)>();
    /// <summary>PAGE のあるモジュールの表の行 (`.modules`)。PAGE のあるモジュールが無ければ null</summary>
    public IReadOnlyList<string>? ModulesLines { get; init; }
    /// <summary>overlay があるときの共有の記号 (`.inc`)。無ければ null</summary>
    public string? SharedSymbolsInc { get; init; }
    /// <summary>OscarC の env で成功したときの C のソース</summary>
    public string? CSource { get; init; }

    internal static SlangCompileResult Failed { get; } = new() { Ok = false };
}

/// <summary>dump の指定 (CLI の --dump-ast・--dump-ir)</summary>
public sealed record SlangCompileOptions(bool DumpAst = false, bool DumpIr = false, TextWriter? DumpOut = null);

/// <summary>
/// slangc の手順 (字句 → 前処理 → 構文 → 意味 → ランタイム → IR → 差し替え → コード生成 → features の検査)。
/// CLI (slangc) とブラウザの入口 (SlangWebApi) が同じものを通る。
/// CLI が stderr に書いていた文 (診断・ランタイムが無い warning・features の error・env の error) は、同じ順で log に書く
/// </summary>
public static class SlangCompilation
{
    /// <summary>
    /// env の名前から .env を探して読む。見つからない・壊れているときは、CLI と同じ文を log に書いて null
    /// </summary>
    public static (EnvironmentConfig Config, string EnvPath)? ResolveEnvironment(string envName, PathResolver paths, TextWriter log)
    {
        (EnvironmentConfig Config, string EnvPath)? resolution;
        try
        {
            resolution = EnvironmentResolver.Resolve(envName, paths.GetRuntimePaths().Concat(paths.GetLibPaths()));
        }
        catch (Exception ex)
        {
            log.WriteLine($"Error: Failed to load env file for '{envName}': {ex.Message}");
            return null;
        }
        if (resolution == null)
        {
            log.WriteLine(
                $"Error: Unknown environment '{envName}'. " +
                $"No '{envName}.env' found in runtime/env/ or lib/env/.");
            return null;
        }
        return resolution;
    }

    /// <summary>
    /// 1 つのソースをコンパイルする。ファイルが有るかは呼ぶ側が確かめる。
    /// diagnostics は呼ぶ側のもの (CLI は入力のファイルのあいだで 1 つを使い回す)。
    /// エラーで止まったら、それまでの diagnostics を log に書いて Ok=false を返す (CLI はそこで終了コード 1)。
    /// 成功したときは、warning などの診断は diagnostics に残ったまま log には書かない。呼ぶ側が最後に diagnostics.WriteTo で書く
    /// (CLI は全部の入力のファイルの後に 1 度書く。SlangWebApi も同じ)
    /// </summary>
    public static SlangCompileResult CompileFile(string filePath, EnvironmentConfig envConfig, PathResolver pathResolver,
                                                 DiagnosticBag diagnostics, TextWriter log, SlangCompileOptions? options = null)
    {
        options ??= new SlangCompileOptions();
        var dumpOut = options.DumpOut ?? TextWriter.Null;

        var source = File.ReadAllText(filePath);
        var baseDir = Path.GetDirectoryName(Path.GetFullPath(filePath)) ?? ".";

        // Phase 1: Lexer
        var lexer = new Lexer.Lexer(source, filePath);
        var tokens = lexer.Tokenize();

        // Phase 1.5: Preprocessor (#INCLUDE展開, #IF/#ELSE/#ENDIF評価)
        var includePaths = pathResolver.GetIncludePaths(baseDir);
        var preprocessor = new Lexer.Preprocessor(diagnostics, includePaths);

        // 環境定数をプリプロセッサに登録（#IF条件式で参照可能）
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

        if (diagnostics.HasErrors) return Fail(diagnostics, log);

        // Phase 2: Parser
        var parser = new Parser.Parser(tokens, diagnostics);
        var ast = parser.ParseCompilationUnit();

        if (diagnostics.HasErrors) return Fail(diagnostics, log);

        if (options.DumpAst)
        {
            dumpOut.WriteLine($"; AST for {filePath}");
            dumpOut.WriteLine("; (AST printer not yet implemented)");
        }

        // Phase 3: Semantic Analysis
        var analyzer = new Semantics.SemanticAnalyzer(diagnostics);
        analyzer.IdentifiersUsedByPreprocessorIf = preprocessor.IdentifiersUsedByIf;
        analyzer.Analyze(ast);

        if (diagnostics.HasErrors) return Fail(diagnostics, log);

        // === Phase 3.5: Backend dispatch ===
        // OscarC backend は IR / RuntimeManager (Z80 専用) を通らず、
        // CTranspiler が AST 直接 → C source を生成する。
        // oscar64 invoke は slangc では行わない (= slangbuild 側の責務)。
        if (envConfig.Backend == BackendKind.OscarC)
        {
            var transpiler = new CTranspiler(analyzer.Symbols, envConfig, diagnostics);
            var cSource = transpiler.Transpile(ast);

            if (diagnostics.HasErrors) return Fail(diagnostics, log);
            return new SlangCompileResult { Ok = true, CSource = cSource };  // OscarC では overlay / RuntimePlan は使わない
        }

        // Load runtime (needed for IR generation to know function return types)
        // 前段で解決済みの envConfig が指定する .asm 群をロード
        var runtimeManager = new RuntimeManager();
        LoadRuntimeLibraries(envConfig.Libraries, runtimeManager, pathResolver, log);

        // Phase 4: IR Generation
        var irGen = new IrGenerator(diagnostics, analyzer.Symbols, runtimeManager);
        var irModule = irGen.Generate(ast);

        if (diagnostics.HasErrors) return Fail(diagnostics, log);

        if (options.DumpIr)
        {
            dumpOut.WriteLine(irModule);
        }

        // ROM を使わない版 (env の norom、ソースの CONST __NOROM = 1): コード生成の前に X の中身を X__NOROM に差し替える
        if (envConfig.NoRom)
        {
            if (irModule.NoRomInvalid)
            {
                diagnostics.Error("CONST __NOROM must be 0 or 1", irModule.NoRomSpan);
                return Fail(diagnostics, log);
            }
            if (irModule.NoRom)
                runtimeManager.ApplyVariant("__NOROM");
        }

        // 0000H〜3FFFH を RAM に (env の lowram、ソースの CONST __LOWRAM = ページ): __NOROM の後で X__LOWRAM に差し替える。
        // 正しくない値はいつもエラー。0 か書いていなければ何もしない
        if (envConfig.LowRam)
        {
            string? lowRamError = null;
            if (irModule.LowRamInvalid)
                lowRamError = "CONST __LOWRAM must be 0 or a page from 18 to 222";
            else if (irModule.LowRam > 0 && !irModule.NoRom)
                lowRamError = "CONST __LOWRAM needs CONST __NOROM = 1";
            if (lowRamError != null)
            {
                diagnostics.Error(lowRamError, irModule.LowRamSpan);
                return Fail(diagnostics, log);
            }
            if (irModule.LowRam > 0)
                runtimeManager.ApplyVariant("__LOWRAM");
        }

        // #MODULE $addr PAGE n (zxn): コンパイルのときに分かる決まり (ページ・窓・__LOWRAM・窓が同じモジュールどうしの呼び出し)
        var pageErrors = ModulePageChecker.Check(irModule, envConfig.ModulePages,
            envConfig.LowRam && irModule.LowRam is > 0 ? irModule.LowRam.Value : 0);
        if (pageErrors.Count > 0)
        {
            foreach (var (msg, span) in pageErrors) diagnostics.Error(msg, span);
            return Fail(diagnostics, log);
        }

        // env がいつも当てる差し替え (zxs の variants: [__NOROM, __ZXS])。書いた順に
        foreach (var variant in envConfig.Variants)
            runtimeManager.ApplyVariant(variant);

        // Phase 5: Code Generation

        if (!irModule.OrgAddress.HasValue && envConfig.DefaultOrg > 0)
            irModule.OrgAddress = envConfig.DefaultOrg;
        if (!irModule.WorkAddress.HasValue && envConfig.DefaultWork > 0)
            irModule.WorkAddress = envConfig.DefaultWork;

        var codeGen = new CodeGenerator(irModule, runtimeManager, envConfig, diagnostics);
        var (mainAsm, overlays) = codeGen.GenerateAll();

        // 使う部品 (差し替えの後、依存とインラインの SLANGINIT も含む) の @requires が、env の features にあるか (features のある env だけ)
        if (envConfig.Features != null && codeGen.RuntimePlan is { } plan)
        {
            var used = new HashSet<string>(plan.MainResidentFunctions, StringComparer.OrdinalIgnoreCase);
            used.UnionWith(plan.MainInlineFunctions);
            foreach (var set in plan.OverlayLocalFunctions.Values) used.UnionWith(set);
            var missing = new List<string>();
            foreach (var name in used.OrderBy(n => n, StringComparer.OrdinalIgnoreCase))
            {
                if (!runtimeManager.Functions.TryGetValue(name, out var func)) continue;
                foreach (var need in func.Requires)
                    if (!envConfig.Features.Contains(need, StringComparer.OrdinalIgnoreCase))
                        missing.Add($"slangc: error: {func.Name} needs {FeatureName(need)} (env {envConfig.Name})");
            }
            if (missing.Count > 0)
            {
                diagnostics.WriteTo(log);
                foreach (var m in missing) log.WriteLine(m);
                return SlangCompileResult.Failed;
            }
        }

        if (diagnostics.HasErrors) return Fail(diagnostics, log);

        // PAGE のあるモジュールの表 (slangbuild が組んだ後に、窓の大きさと本体との重なりを調べる)
        var modules = irModule.Overlays.Any(o => o.Page != null)
            ? irModule.Overlays.Where(o => o.Page != null).Select(o => $"{o.Index} ${o.OrgAddress:X4} {o.Page}").ToList()
            : null;

        return new SlangCompileResult
        {
            Ok = true,
            MainAsm = mainAsm,
            Overlays = overlays,
            ModulesLines = modules,
            SharedSymbolsInc = overlays.Count > 0 ? SharedSymbolsInc(irModule, codeGen.RuntimePlan, runtimeManager) : null,
        };
    }

    private static SlangCompileResult Fail(DiagnosticBag diagnostics, TextWriter log)
    {
        diagnostics.WriteTo(log);
        return SlangCompileResult.Failed;
    }

    /// <summary>@requires の機能の名前を、エラーの文の言い方にする</summary>
    static string FeatureName(string feature) =>
        feature.Equals("next", StringComparison.OrdinalIgnoreCase) ? "ZX Spectrum Next" : feature;

    /// <summary>
    /// 解決済み EnvironmentConfig が指定するランタイムライブラリ (.asm) をロード。
    /// 見つからないものは `; Warning: Runtime not found: X` を log に書いて続ける
    /// </summary>
    static void LoadRuntimeLibraries(IEnumerable<string> libraries, RuntimeManager manager, PathResolver paths, TextWriter log)
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
                log.WriteLine($"; Warning: Runtime not found: {asmLib}");
        }
    }
    /// <summary>
    /// overlay があるときの共有の記号 (`.inc`) の中身。改行は Environment.NewLine (CLI が StreamWriter で書いていたのと同じ)
    /// </summary>
    public static string SharedSymbolsInc(IrModule module, RuntimePlan? plan = null, RuntimeManager? runtime = null)
    {
        using var writer = new StringWriter();
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
        return writer.ToString();
    }
}
