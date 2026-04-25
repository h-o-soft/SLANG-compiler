using System.Diagnostics;
using SLANGCompiler.Runtime;

namespace SLANGCompiler.Build;

/// <summary>
/// slangbuild の orchestration エントリ。
///
/// フロー:
///   1) slangc spawn → main.ASM + overlay._mN.ASM 群 + main.inc
///   2) AILZ80ASM main.ASM → main.bin + main.sym
///   3) 各 overlay について OverlayImportsBuilder で imports.asm を作り、
///      AILZ80ASM imports.asm overlay._mN.ASM → overlay._mN.bin
///   4) cleanup (--keep-asm 時は残す)
/// </summary>
public class Driver
{
    public class Options
    {
        public string InputPath { get; set; } = "";
        public string? OutputPrefix { get; set; }
        public string Environment { get; set; } = "lsx";
        public string? AsmPath { get; set; }
        public string? SlangcPath { get; set; }
        public bool KeepAsm { get; set; }
        public bool Verbose { get; set; }
        /// <summary>slangc に pass-through する `-I &lt;path&gt;` の値リスト</summary>
        public List<string> IncludePaths { get; } = new();
        /// <summary>slangc に pass-through する `-L &lt;path&gt;` の値リスト</summary>
        public List<string> LibraryPaths { get; } = new();
    }

    private readonly Options _opts;
    private readonly ToolResolver _resolver;

    public Driver(Options opts, ToolResolver? resolver = null)
    {
        _opts = opts;
        _resolver = resolver ?? new ToolResolver();
    }

    /// <summary>main flow を実行。終了コード (0 = success) を返す。</summary>
    public int Run()
    {
        if (string.IsNullOrEmpty(_opts.InputPath))
        {
            Console.Error.WriteLine("slangbuild: missing input file");
            return 1;
        }
        if (!File.Exists(_opts.InputPath))
        {
            Console.Error.WriteLine($"slangbuild: input file not found: {_opts.InputPath}");
            return 1;
        }

        // 出力ベースパス決定:
        //   -o 指定あり → 絶対パスならそのまま、相対パスなら cwd 基準で resolve
        //                  (Path.GetFullPath は相対なら cwd を補う)。これにより
        //                  Makefile.dist が `-o examples/PROG` を書いた場合は
        //                  cwd (= リポジトリ root) 配下の `examples/PROG.bin` に出る
        //   -o 省略    → 入力 SL と同じディレクトリ + 入力ファイル名から派生
        string outputBase = _opts.OutputPrefix != null
            ? Path.GetFullPath(_opts.OutputPrefix)
            : Path.Combine(
                Path.GetDirectoryName(Path.GetFullPath(_opts.InputPath))!,
                DerivePrefix(_opts.InputPath));

        // 中間ファイル / 最終ファイルは outputBase と同じディレクトリに出す
        var outputDir = Path.GetDirectoryName(outputBase)!;
        var prefix = Path.GetFileName(outputBase);
        var mainAsm = outputBase + ".ASM";
        var mainBin = outputBase + ".bin";
        var mainSym = outputBase + ".sym";

        var intermediates = new List<string>();
        bool succeeded = false;

        try
        {
            // === Step 1: slangc spawn ===
            var slangc = _resolver.ResolveSlangc(_opts.SlangcPath);
            if (_opts.Verbose) Console.Error.WriteLine($"slangbuild: using slangc: {slangc}");

            var slangcResult = SpawnSlangc(slangc, _opts.InputPath, mainAsm, _opts.Environment);
            if (slangcResult != 0)
            {
                Console.Error.WriteLine($"slangbuild: slangc failed (exit {slangcResult})");
                return slangcResult;
            }
            if (!File.Exists(mainAsm))
            {
                Console.Error.WriteLine($"slangbuild: slangc did not produce expected output: {mainAsm}");
                return 1;
            }
            intermediates.Add(mainAsm);
            // .inc も出力されている可能性
            var incPath = outputBase + ".inc";
            if (File.Exists(incPath)) intermediates.Add(incPath);

            // overlay ASM の検出 (`<prefix>._mN.ASM`、outputDir 内)
            // case-insensitive な FS (macOS APFS / Windows) では `_m*.ASM` パターンが
            // 旧 `--keep-asm` 残骸の `.dummy.imports.asm` / `.imports.asm` まで拾い、
            // 次回以降のビルドで filename チェーン爆発を起こす。
            // `_m<digits>.ASM` 厳密一致の regex でフィルタする。
            var overlayPattern = new System.Text.RegularExpressions.Regex(
                $"^{System.Text.RegularExpressions.Regex.Escape(prefix)}\\._m\\d+\\.ASM$",
                System.Text.RegularExpressions.RegexOptions.IgnoreCase);
            var overlayAsms = Directory.GetFiles(outputDir, prefix + "._m*.ASM")
                                       .Where(p => overlayPattern.IsMatch(Path.GetFileName(p)))
                                       .OrderBy(p => p, StringComparer.OrdinalIgnoreCase)
                                       .ToList();
            foreach (var p in overlayAsms) intermediates.Add(p);

            // === Step 2: PrelinkPlan 構築 (cross-ref 検出) ===
            var asm = _resolver.ResolveAilz80Asm(_opts.AsmPath);
            if (_opts.Verbose) Console.Error.WriteLine($"slangbuild: using AILZ80ASM: {asm.Path}");
            var runner = new AssemblerRunner(asm.Path, _opts.Verbose);

            var planTargets = new List<(string Label, string AsmPath)>
            {
                ("main", mainAsm),
            };
            for (int i = 0; i < overlayAsms.Count; i++)
                planTargets.Add(($"overlay {i}", overlayAsms[i]));
            var plan = PrelinkPlan.Build(planTargets);

            if (plan.IsTrivial)
            {
                // === 単段モード (PR-B 既存パス) ===
                int rc = AssembleSingleStage(runner, plan, mainAsm, mainBin, mainSym,
                                             overlayAsms, outputDir, intermediates);
                if (rc != 0) return rc;
            }
            else
            {
                // === prelink モード (PR-B2): Pass 1 → Pass 2 → Pass 3 ===
                int rc = AssemblePrelink(runner, plan, mainBin, mainSym,
                                         outputDir, intermediates);
                if (rc != 0) return rc;
            }

            if (_opts.Verbose)
            {
                Console.Error.WriteLine($"slangbuild: success — {prefix}.bin"
                    + (overlayAsms.Count > 0 ? $" + {overlayAsms.Count} overlay(s)" : ""));
            }
            succeeded = true;
            return 0;
        }
        finally
        {
            // 中間ファイル cleanup は「成功時 + --keep-asm 未指定」のときだけ。
            // 失敗時は AILZ80ASM のエラー行 (xxx.ASM:NNN ...) をユーザーが追えるよう
            // 必ず残す。
            if (succeeded && !_opts.KeepAsm)
            {
                foreach (var p in intermediates)
                {
                    try { if (File.Exists(p)) File.Delete(p); } catch { /* best effort */ }
                }
            }
            else if (!succeeded && !_opts.KeepAsm)
            {
                Console.Error.WriteLine(
                    "slangbuild: build failed — keeping intermediate files for inspection.");
            }
        }
    }

    private static string DerivePrefix(string inputPath)
    {
        var name = Path.GetFileName(inputPath);
        var dot = name.LastIndexOf('.');
        return dot >= 0 ? name[..dot] : name;
    }

    /// <summary>
    /// 単段モード (PR-B 既存パス): main を直接アセンブル → 各 overlay について
    /// `OverlayImportsBuilder` で main.sym から filtered EQU を生成 → overlay を
    /// アセンブル。cross-ref が無いケースで使う。
    /// </summary>
    private int AssembleSingleStage(AssemblerRunner runner, PrelinkPlan plan,
        string mainAsm, string mainBin, string mainSym,
        List<string> overlayAsms, string outputDir, List<string> intermediates)
    {
        var mainLst = Path.ChangeExtension(mainBin, ".LST");
        var mainResult = runner.AssembleMain(mainAsm, mainBin, mainSym, lstPath: mainLst);
        if (!mainResult.Success)
        {
            Console.Error.Write(mainResult.Stderr);
            Console.Error.WriteLine($"slangbuild: main assembly failed (exit {mainResult.ExitCode})");
            return mainResult.ExitCode;
        }
        intermediates.Add(mainSym);

        foreach (var overlayAsm in overlayAsms)
        {
            var overlayBase = Path.GetFileNameWithoutExtension(overlayAsm);
            var importsAsm = Path.Combine(outputDir, overlayBase + ".imports.asm");
            var overlayBin = Path.Combine(outputDir, overlayBase + ".bin");
            var overlaySym = Path.Combine(outputDir, overlayBase + ".sym");
            var overlayLst = Path.Combine(outputDir, overlayBase + ".LST");

            var (_, unresolved) = OverlayImportsBuilder.Build(mainSym, overlayAsm, importsAsm);
            intermediates.Add(importsAsm);

            if (unresolved.Count > 0 && _opts.Verbose)
            {
                Console.Error.WriteLine(
                    $"slangbuild: {overlayBase}: {unresolved.Count} unresolved EXTERN(s) "
                    + "(main.sym lacks: " + string.Join(", ", unresolved) + ")");
            }

            var ovResult = runner.AssembleOverlay(importsAsm, overlayAsm, overlayBin, overlaySym,
                                                  lstPath: overlayLst);
            if (!ovResult.Success)
            {
                Console.Error.Write(ovResult.Stderr);
                Console.Error.WriteLine($"slangbuild: overlay assembly failed for {overlayBase} (exit {ovResult.ExitCode})");
                return ovResult.ExitCode;
            }
            intermediates.Add(overlaySym);
        }
        return 0;
    }

    /// <summary>
    /// prelink モード (PR-B2 新規): cross-ref があるケース。
    ///   Pass 1: 各 target に dummy imports ($0000 EQU) を渡してアセンブル → pass1.sym 取得
    ///   Pass 2: 全 target の Exports + pass1.sym から ExportedFunctionTable 構築
    ///   Pass 3: combined real imports を生成して再アセンブル → 本番 bin/sym
    /// `-nsa` 付与で命令長を固定し、Pass 1 と Pass 3 で同じ target 内のラベル
    /// アドレスが一致することを保証する。
    /// </summary>
    private int AssemblePrelink(AssemblerRunner runner, PrelinkPlan plan,
        string mainBin, string mainSym, string outputDir, List<string> intermediates)
    {
        if (_opts.Verbose) Console.Error.WriteLine($"slangbuild: prelink mode (cross-references found)");

        // Pass 1: 各 target を dummy imports でアセンブル → pass1 sym 取得
        var pass1Symbols = new Dictionary<string, Dictionary<string, int>>(); // target.Label → sym dict
        foreach (var t in plan.Targets)
        {
            var baseName = Path.GetFileNameWithoutExtension(t.AsmPath);
            var dummyImportsPath = Path.Combine(outputDir, baseName + ".dummy.imports.asm");
            var pass1BinPath = Path.Combine(outputDir, baseName + ".pass1.bin");
            var pass1SymPath = Path.Combine(outputDir, baseName + ".pass1.sym");

            PrelinkPlan.WriteDummyImports(t, dummyImportsPath);
            intermediates.Add(dummyImportsPath);

            var result = runner.AssembleOverlay(dummyImportsPath, t.AsmPath,
                                                pass1BinPath, pass1SymPath,
                                                superAssemble: false);
            if (!result.Success)
            {
                Console.Error.Write(result.Stderr);
                Console.Error.WriteLine(
                    $"slangbuild: prelink Pass 1 failed for {t.Label} (exit {result.ExitCode})");
                return result.ExitCode;
            }
            // Pass 1 の bin はテンポラリ即削除 (sym だけ Pass 2 で使う)
            try { File.Delete(pass1BinPath); } catch { }
            intermediates.Add(pass1SymPath);
            pass1Symbols[t.Label] = SymFileReader.ReadFile(pass1SymPath);
        }

        // Pass 2: ExportedFunctionTable を構築
        var exportedTable = new ExportedFunctionTable();
        foreach (var t in plan.Targets)
            exportedTable.Add(t.Label, t.Exports, pass1Symbols[t.Label]);

        // Pass 3: combined real imports を生成 → 本番アセンブル
        var mainPass1Sym = pass1Symbols.GetValueOrDefault("main", new Dictionary<string, int>());
        foreach (var t in plan.Targets)
        {
            var baseName = Path.GetFileNameWithoutExtension(t.AsmPath);
            var importsAsm = Path.Combine(outputDir, baseName + ".imports.asm");
            var (_, unresolved) = PrelinkPlan.WriteRealImports(
                t, exportedTable, mainPass1Sym, importsAsm);
            intermediates.Add(importsAsm);

            if (unresolved.Count > 0 && _opts.Verbose)
            {
                Console.Error.WriteLine(
                    $"slangbuild: {t.Label}: {unresolved.Count} unresolved EXTERN(s) "
                    + "(none found in any target sym: " + string.Join(", ", unresolved) + ")");
            }

            // 本番出力ファイル名: main は <prefix>.bin、overlay は <prefix>._mN.bin
            string outBin, outSym, outLst;
            if (t.Label == "main")
            {
                outBin = mainBin;
                outSym = mainSym;
                outLst = Path.ChangeExtension(mainBin, ".LST");
            }
            else
            {
                outBin = Path.Combine(outputDir, baseName + ".bin");
                outSym = Path.Combine(outputDir, baseName + ".sym");
                outLst = Path.Combine(outputDir, baseName + ".LST");
            }

            var result = runner.AssembleOverlay(importsAsm, t.AsmPath, outBin, outSym,
                                                superAssemble: false, lstPath: outLst);
            if (!result.Success)
            {
                Console.Error.Write(result.Stderr);
                Console.Error.WriteLine(
                    $"slangbuild: prelink Pass 3 failed for {t.Label} (exit {result.ExitCode})");
                return result.ExitCode;
            }
            if (t.Label != "main") intermediates.Add(outSym);
        }
        intermediates.Add(mainSym);
        return 0;
    }

    private int SpawnSlangc(ResolvedTool slangc, string inputPath, string outAsmPath, string env)
    {
        var psi = new ProcessStartInfo
        {
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
        };

        if (slangc.Kind == ResolutionKind.DotnetRun)
        {
            // dotnet run --project <csproj> -c Release -- -E <env> -o <out> <input>
            psi.FileName = slangc.Path; // dotnet
            psi.ArgumentList.Add("run");
            psi.ArgumentList.Add("--project");
            psi.ArgumentList.Add(slangc.ProjectPath!);
            psi.ArgumentList.Add("-c");
            psi.ArgumentList.Add("Release");
            psi.ArgumentList.Add("--");
        }
        else
        {
            psi.FileName = slangc.Path;
        }
        psi.ArgumentList.Add("-E");
        psi.ArgumentList.Add(env);
        foreach (var p in _opts.IncludePaths)
        {
            psi.ArgumentList.Add("-I");
            psi.ArgumentList.Add(p);
        }
        foreach (var p in _opts.LibraryPaths)
        {
            psi.ArgumentList.Add("-L");
            psi.ArgumentList.Add(p);
        }
        psi.ArgumentList.Add("-o");
        psi.ArgumentList.Add(outAsmPath);
        psi.ArgumentList.Add(inputPath);

        if (_opts.Verbose)
        {
            var argLine = string.Join(" ", psi.ArgumentList);
            Console.Error.WriteLine($"+ {psi.FileName} {argLine}");
        }

        using var proc = Process.Start(psi)!;
        var stdout = proc.StandardOutput.ReadToEnd();
        var stderr = proc.StandardError.ReadToEnd();
        proc.WaitForExit(60_000);

        if (_opts.Verbose)
        {
            if (!string.IsNullOrEmpty(stdout)) Console.Out.Write(stdout);
            if (!string.IsNullOrEmpty(stderr)) Console.Error.Write(stderr);
        }
        else if (proc.ExitCode != 0)
        {
            // 失敗時は stderr を必ず出す (verbose でなくても)
            if (!string.IsNullOrEmpty(stderr)) Console.Error.Write(stderr);
        }

        return proc.ExitCode;
    }
}
