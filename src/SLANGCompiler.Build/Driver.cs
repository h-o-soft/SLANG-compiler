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
        public string? NdcPath { get; set; }
        public string? HudiskPath { get; set; }
        public string? UdostoolPath { get; set; }
        public bool KeepAsm { get; set; }
        public bool Verbose { get; set; }
        /// <summary>slangc に pass-through する `-I &lt;path&gt;` の値リスト</summary>
        public List<string> IncludePaths { get; } = new();
        /// <summary>slangc に pass-through する `-L &lt;path&gt;` の値リスト</summary>
        public List<string> LibraryPaths { get; } = new();

        /// <summary>"bin" (default) or "disk"。"disk" は env の `disk:` セクション必須。</summary>
        public string EmitMode { get; set; } = "bin";
        /// <summary>`--disk-image &lt;path&gt;`。EmitMode == "disk" 時のみ意味を持つ。
        /// null の場合は &lt;output_prefix&gt;.d88 を使う。</summary>
        public string? DiskImagePath { get; set; }
        /// <summary>`--disk-template &lt;path&gt;`。env file の disk.template を CLI で
        /// override する。EmitMode == "disk" 時のみ意味を持つ。null/空なら env 値を使う。</summary>
        public string? DiskTemplatePath { get; set; }
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

        // === env 解決 (前倒し) ===
        // env file を 1 度だけ解決し、出力 format / disk image 組み立てに共有する。
        // env 未解決は即 error (= slangc 側でも fail するので早期失敗で十分)。
        // BuildDiskImage 側で再解決する旧フローは廃止 (= 二重解決排除)。
        var pathResolver = new PathResolver(_opts.IncludePaths, _opts.LibraryPaths);
        var searchPaths = pathResolver.GetRuntimePaths()
                                       .Concat(pathResolver.GetLibPaths())
                                       .ToList();
        var resolved = EnvironmentResolver.Resolve(_opts.Environment, searchPaths);
        if (resolved == null)
        {
            Console.Error.WriteLine($"slangbuild: env not found: {_opts.Environment}");
            return 1;
        }
        var (envConfig, envPath) = resolved.Value;

        // --emit disk + disk: セクション無しは早期 reject (= 無駄な compile/asm 回避)
        if (_opts.EmitMode == "disk" && envConfig.Disk == null)
        {
            Console.Error.WriteLine(
                $"slangbuild: --emit disk requires `disk:` section in env: {envPath}");
            return 1;
        }

        // === 出力 format / 拡張子 / AILZ80ASM 出力 flag / extra args 決定 ===
        // env file `output: cmt` 指定で `.bin` → `.cmt` 切替 + AILZ80ASM の
        // shortcut option を `-bin` → `-cmt` に置換 + `-gap 0` を追加 pass。
        // `-bin` と `-cmt` を同時に渡すと両 format の file が出るので、format
        // ごとに 1 つに切替える設計 (= AssemblerRunner 側 outputFlag 引数)。
        // null/未指定 (= bin default) なら従来通り `-bin` のみ。
        // outputFlag / extraArgs は prelink Pass 1/3 でアドレス整合上 同 target
        // で同じものを渡す必要 (= 各 helper method で参照)。
        var binExt = (envConfig.OutputFormat == "cmt") ? ".cmt" : ".bin";
        var asmOutputFlag = (envConfig.OutputFormat == "cmt") ? "-cmt" : "-bin";
        var asmExtraArgs = BuildAsmExtras(envConfig.OutputFormat, envConfig.Defines);

        // overlay 用は env.OverlayOutputFormat (= 未指定なら main に追従)
        // で別計算。pc80mk2xsd では main = cmt + overlay = bin (= raw binary、
        // SD で SD_RREAD 用、CMT header / gap 不要) のような env-specific
        // 設計を許容する。defines は main / overlay 共通で pass される
        // (= ASM 側 #IF exists NAME 判定が main / overlay どちらでも活きる)。
        var overlayFormat = envConfig.OverlayOutputFormat ?? envConfig.OutputFormat;
        var overlayBinExt = (overlayFormat == "cmt") ? ".cmt" : ".bin";
        var asmOverlayOutputFlag = (overlayFormat == "cmt") ? "-cmt" : "-bin";
        var asmOverlayExtraArgs = BuildAsmExtras(overlayFormat, envConfig.Defines);

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
        var mainBin = outputBase + binExt;
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
                                             overlayAsms, outputDir, intermediates,
                                             binExt, asmOutputFlag, asmExtraArgs,
                                             overlayBinExt, asmOverlayOutputFlag,
                                             asmOverlayExtraArgs);
                if (rc != 0) return rc;
            }
            else
            {
                // === prelink モード (PR-B2): Pass 1 → Pass 2 → Pass 3 ===
                int rc = AssemblePrelink(runner, plan, mainBin, mainSym,
                                         outputDir, intermediates,
                                         binExt, asmOutputFlag, asmExtraArgs,
                                         overlayBinExt, asmOverlayOutputFlag,
                                         asmOverlayExtraArgs);
                if (rc != 0) return rc;
            }

            // === Step 3.5a: overlay rename (env.OverlayName 指定時) ===
            // 旧 path: <output dir>/<prefix>._m{N}{overlayBinExt}
            // 新 path: <output dir>/<env.OverlayName で {index} 展開>
            // pc80mk2xsd で M0.BIN 命名にするため。pc80mk2x も rename して
            // 同 path を後続 cmt_concat step で使う (= path 計算 1 本化)。
            var renamedOverlayBins = new List<string>();
            var outputDirFull = Path.GetFullPath(outputDir);
            if (!string.IsNullOrEmpty(envConfig.OverlayName))
            {
                for (int i = 0; i < overlayAsms.Count; i++)
                {
                    var oldPath = Path.Combine(outputDir,
                        Path.GetFileNameWithoutExtension(overlayAsms[i]) + overlayBinExt);
                    var newName = envConfig.OverlayName!.Replace("{index}", i.ToString());
                    var newPath = Path.Combine(outputDir, newName);

                    // rename 直前の二重 check: 最終 path が outputDir 直下に
                    // 居ることを確認 (= Loader で `{index}` 必須 + separator/..
                    // 禁止を vetting 済みだが、placeholder 値や OS 差のある
                    // separator 経由のパス攻撃の最終防御)
                    var newPathFull = Path.GetFullPath(newPath);
                    if (Path.GetDirectoryName(newPathFull) != outputDirFull)
                    {
                        Console.Error.WriteLine(
                            $"slangbuild: overlay_name produced out-of-output-dir path: {newPath}");
                        return 1;
                    }

                    if (File.Exists(oldPath))
                    {
                        File.Move(oldPath, newPath, overwrite: true);
                    }
                    renamedOverlayBins.Add(newPath);
                }
            }
            else
            {
                renamedOverlayBins = overlayAsms
                    .Select(a => Path.Combine(outputDir,
                        Path.GetFileNameWithoutExtension(a) + overlayBinExt))
                    .ToList();
            }

            // === Step 3.5a-pad: bin padding (env.BinPadSize / OverlayPadAlign 指定時) ===
            // VGS-Zero 等で固定サイズ ROM 出力 (= main を 16384 byte 固定 +
            // 各 overlay を 8192 倍数に切り上げ padding)。renamedOverlayBins
            // 経由で rename 後 path に対して padding するので、pc80mk2xsd の
            // `_m{N}` 命名でも VGS-Zero の `M{index}.BIN` 命名でも対応可能。
            // CMT 出力 (= output: cmt) との排他は Loader で保証済。
            if (envConfig.BinPadSize.HasValue && envConfig.BinPadSize.Value > 0)
            {
                int padRc = PadBinToFixedSize(mainBin, envConfig.BinPadSize.Value);
                if (padRc != 0) return padRc;
            }
            if (envConfig.OverlayPadAlign.HasValue && envConfig.OverlayPadAlign.Value > 0)
            {
                foreach (var ov in renamedOverlayBins)
                {
                    // renamedOverlayBins に登録済の overlay は生成されている
                    // べき成果物。欠落は internal error として silent wrong
                    // 防止のため明示エラー (cmt_concat の missing file 対応と
                    // 揃える方針)。
                    if (!File.Exists(ov))
                    {
                        Console.Error.WriteLine(
                            $"slangbuild: overlay_pad_align: overlay file not found: {ov}");
                        return 1;
                    }
                    int rc = PadBinToAlignment(ov, envConfig.OverlayPadAlign.Value);
                    if (rc != 0) return rc;
                }
            }

            // === Step 3.5b: cmt_assets コピー (env.CmtAssets 指定時) ===
            // pc80mk2xsd で XBIOS.CMT 等を output dir にコピー。ユーザーは
            // output dir 全体を SD カードに移すだけで揃う運用。
            if (envConfig.CmtAssets != null && envConfig.CmtAssets.Count > 0)
            {
                foreach (var srcPath in envConfig.CmtAssets)
                {
                    if (!File.Exists(srcPath))
                    {
                        Console.Error.WriteLine(
                            $"slangbuild: cmt_assets: file not found: {srcPath}");
                        return 1;
                    }
                    var dstPath = Path.Combine(outputDir, Path.GetFileName(srcPath));
                    File.Copy(srcPath, dstPath, overwrite: true);
                    if (_opts.Verbose)
                        Console.Error.WriteLine(
                            $"slangbuild: cmt asset: {Path.GetFileName(srcPath)} → {dstPath}");
                }
            }

            // === Step 3.5c: CMT 結合 (env.CmtConcat 指定時のみ) ===
            // pc80mk2x で main.cmt + XBIOS.CMT + overlay._mN.cmt を 1 本に
            // 結合。結合先 = main.cmt 上書き (= ユーザーは結合済 1 本だけ
            // 使う運用)。Loader で cmt_concat と cmt_assets が排他保証済。
            if (envConfig.CmtConcat != null && envConfig.CmtConcat.Count > 0)
            {
                int concatRc = ConcatCmt(mainBin, envConfig.CmtConcat,
                                          renamedOverlayBins, intermediates);
                if (concatRc != 0) return concatRc;
            }

            // === Step 4: --emit disk → disk image 組み立て ===
            if (_opts.EmitMode == "disk")
            {
                // overlay は env.OverlayName で rename 済なら renamedOverlayBins、
                // 未指定なら overlayBinExt 拡張子の既定 path
                int diskRc = BuildDiskImage(envConfig, envPath, mainBin,
                                            renamedOverlayBins, outputBase);
                if (diskRc != 0) return diskRc;
            }

            if (_opts.Verbose)
            {
                Console.Error.WriteLine($"slangbuild: success — {prefix}{binExt}"
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

    /// <summary>
    /// AILZ80ASM の extra args を組み立てる。format == "cmt" なら <c>-gap 0</c>、
    /// env file `defines:` 指定があれば各 entry を <c>-dl NAME=VAL</c> として
    /// append する。両者なしなら null (= 引数追加なし)。
    /// main / overlay 両方で共通利用 (= prelink Pass 1/3 と本番 assemble で
    /// 同じ args を全 target に pass する設計、target 内整合のため)。
    /// </summary>
    private static string[]? BuildAsmExtras(string? format, Dictionary<string, int>? defines)
    {
        var list = new List<string>();
        if (format == "cmt")
        {
            list.Add("-gap");
            list.Add("0");
        }
        if (defines != null && defines.Count > 0)
        {
            foreach (var (name, value) in defines)
            {
                list.Add("-dl");
                list.Add($"{name}={value}");
            }
        }
        return list.Count > 0 ? list.ToArray() : null;
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
        List<string> overlayAsms, string outputDir, List<string> intermediates,
        string binExt, string asmOutputFlag, string[]? asmExtraArgs,
        string overlayBinExt, string asmOverlayOutputFlag,
        string[]? asmOverlayExtraArgs)
    {
        var mainLst = Path.ChangeExtension(mainBin, ".LST");
        var mainResult = runner.AssembleMain(mainAsm, mainBin, mainSym,
                                             lstPath: mainLst,
                                             outputFlag: asmOutputFlag,
                                             extraArgs: asmExtraArgs);
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
            var overlayBin = Path.Combine(outputDir, overlayBase + overlayBinExt);
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
                                                  lstPath: overlayLst,
                                                  outputFlag: asmOverlayOutputFlag,
                                                  extraArgs: asmOverlayExtraArgs);
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
        string mainBin, string mainSym, string outputDir, List<string> intermediates,
        string binExt, string asmOutputFlag, string[]? asmExtraArgs,
        string overlayBinExt, string asmOverlayOutputFlag,
        string[]? asmOverlayExtraArgs)
    {
        if (_opts.Verbose) Console.Error.WriteLine($"slangbuild: prelink mode (cross-references found)");

        // Pass 1: 各 target を dummy imports でアセンブル → pass1 sym 取得
        var pass1Symbols = new Dictionary<string, Dictionary<string, int>>(); // target.Label → sym dict
        foreach (var t in plan.Targets)
        {
            // target ごとに main / overlay の outputFlag を切替 (= pc80mk2xsd で
            // main = cmt + overlay = bin の組合せに対応)。各 target 内では
            // Pass 1 と Pass 3 で同じ outputFlag を使う必要 (= prelink アドレス整合)。
            bool isMain = (t.Label == "main");
            var tBinExt = isMain ? binExt : overlayBinExt;
            var tOutputFlag = isMain ? asmOutputFlag : asmOverlayOutputFlag;
            var tExtraArgs = isMain ? asmExtraArgs : asmOverlayExtraArgs;

            var baseName = Path.GetFileNameWithoutExtension(t.AsmPath);
            var dummyImportsPath = Path.Combine(outputDir, baseName + ".dummy.imports.asm");
            // Pass 1 の bin は即削除 intermediate なので拡張子は固定で良いが、
            // AILZ80ASM 出力 format (= -cmt) を main / Pass 3 と揃える都合上、
            // ファイル extension も binExt にしておく (= 一貫性、debug 時の混乱防止)。
            var pass1BinPath = Path.Combine(outputDir, baseName + ".pass1" + tBinExt);
            var pass1SymPath = Path.Combine(outputDir, baseName + ".pass1.sym");

            PrelinkPlan.WriteDummyImports(t, dummyImportsPath);
            intermediates.Add(dummyImportsPath);

            var result = runner.AssembleOverlay(dummyImportsPath, t.AsmPath,
                                                pass1BinPath, pass1SymPath,
                                                superAssemble: false,
                                                outputFlag: tOutputFlag,
                                                extraArgs: tExtraArgs);
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

            // target ごとに main / overlay の outputFlag を切替 (Pass 1 と一致)
            bool isMain = (t.Label == "main");
            var tBinExt = isMain ? binExt : overlayBinExt;
            var tOutputFlag = isMain ? asmOutputFlag : asmOverlayOutputFlag;
            var tExtraArgs = isMain ? asmExtraArgs : asmOverlayExtraArgs;

            // 本番出力ファイル名: main は <prefix>{binExt}、overlay は <prefix>._mN{overlayBinExt}
            string outBin, outSym, outLst;
            if (isMain)
            {
                outBin = mainBin;
                outSym = mainSym;
                outLst = Path.ChangeExtension(mainBin, ".LST");
            }
            else
            {
                outBin = Path.Combine(outputDir, baseName + tBinExt);
                outSym = Path.Combine(outputDir, baseName + ".sym");
                outLst = Path.Combine(outputDir, baseName + ".LST");
            }

            var result = runner.AssembleOverlay(importsAsm, t.AsmPath, outBin, outSym,
                                                superAssemble: false, lstPath: outLst,
                                                outputFlag: tOutputFlag,
                                                extraArgs: tExtraArgs);
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

    /// <summary>
    /// CMT 結合 phase (env file `cmt_concat:` 指定時)。main.cmt の直後に
    /// 追加 .cmt path 群と各 overlay._mN.cmt を 1 本に結合し、結合先 = main.cmt
    /// 上書き (= ユーザーは結合済 1 本だけ使う)。
    /// 結合は同 dir tmp file 経由 + <c>File.Move(.., overwrite: true)</c> で
    /// delete-then-move の中間状態を回避 (= main.cmt 破壊を避ける)。
    /// 結合元欠落時は明示エラー (= silent wrong 防止)。結合に消費された
    /// overlay は intermediate cleanup 対象に追加 (= --keep-asm 時は残る)。
    /// </summary>
    private int ConcatCmt(string mainBin, List<string> concatFiles,
                          List<string> overlayBins, List<string> intermediates)
    {
        // 結合元の存在確認 (= concat file が無いと silent wrong になる)
        foreach (var f in concatFiles)
        {
            if (!File.Exists(f))
            {
                Console.Error.WriteLine(
                    $"slangbuild: cmt_concat: file not found: {f}");
                return 1;
            }
        }

        // tmp file は main と同じ dir に置いて File.Move を同 FS 内 rename
        // に限定。OS / API / FS で挙動が多少違うので「atomic」とは言い切ら
        // ないが、cross-FS 失敗 / delete-then-move の中間状態は避けられる。
        var mainDir = Path.GetDirectoryName(mainBin)!;
        var mainName = Path.GetFileName(mainBin);
        var tmpPath = Path.Combine(mainDir, "." + mainName + ".concat.tmp");

        try
        {
            using (var tmp = File.Create(tmpPath))
            {
                CopyToStream(mainBin, tmp);
                foreach (var f in concatFiles) CopyToStream(f, tmp);
                foreach (var ov in overlayBins)
                {
                    if (File.Exists(ov)) CopyToStream(ov, tmp);
                }
            }
            // overwrite: true で「delete-then-move」を避けつつ置換
            // (= 旧版の File.Delete + File.Move 2 段は中間状態で main を失う
            // 可能性、Codex Medium 指摘)
            File.Move(tmpPath, mainBin, overwrite: true);

            if (_opts.Verbose)
            {
                var inputs = string.Join(" + ",
                    new[] { Path.GetFileName(mainBin) }
                        .Concat(concatFiles.Select(Path.GetFileName))
                        .Concat(overlayBins.Where(File.Exists)
                                            .Select(Path.GetFileName)));
                Console.Error.WriteLine(
                    $"slangbuild: cmt concat: {inputs} → {Path.GetFileName(mainBin)}");
            }
        }
        catch
        {
            try { if (File.Exists(tmpPath)) File.Delete(tmpPath); } catch { }
            throw;
        }

        // overlay bin は結合に消費されたので intermediate cleanup 対象に追加
        // (= --keep-asm 指定時は残る、既存 cleanup ロジック)
        foreach (var ov in overlayBins)
        {
            if (File.Exists(ov) && !intermediates.Contains(ov))
                intermediates.Add(ov);
        }
        return 0;
    }

    private static void CopyToStream(string srcPath, FileStream dst)
    {
        using var src = File.OpenRead(srcPath);
        src.CopyTo(dst);
    }

    /// <summary>
    /// bin を指定 byte size まで末尾 0 で padding。既存サイズが指定サイズを
    /// 超えていた場合は silent truncation を避けるため error 終了。
    /// VGS-Zero 等の固定サイズ ROM 出力用 (= 16384 byte 固定 ROM 等)。
    /// </summary>
    private int PadBinToFixedSize(string binPath, int targetSize)
    {
        var currentSize = new FileInfo(binPath).Length;
        if (currentSize > targetSize)
        {
            Console.Error.WriteLine(
                $"slangbuild: bin_pad_size: bin size {currentSize} byte exceeds "
                + $"target {targetSize} byte ({binPath}). reduce code size or "
                + $"increase bin_pad_size in env file.");
            return 1;
        }
        if (currentSize == targetSize) return 0;
        WriteZeroPadding(binPath, currentSize, targetSize);
        if (_opts.Verbose)
            Console.Error.WriteLine(
                $"slangbuild: bin pad: {Path.GetFileName(binPath)} "
                + $"{currentSize} → {targetSize} byte (zero-filled)");
        return 0;
    }

    /// <summary>
    /// bin を alignment の倍数 (= ((size + align - 1) / align) * align) に
    /// 末尾 0 で padding。すでに倍数なら no-op。empty (= 0 byte) でも no-op。
    /// 上限なし。VGS-Zero の 8KB bank switching 等で各 overlay を bank 単位
    /// に揃えるため。
    /// </summary>
    private int PadBinToAlignment(string binPath, int align)
    {
        var currentSize = new FileInfo(binPath).Length;
        if (currentSize == 0) return 0; // empty overlay は no-op
        long rounded = ((currentSize + align - 1) / align) * align;
        if (currentSize == rounded) return 0;
        WriteZeroPadding(binPath, currentSize, rounded);
        if (_opts.Verbose)
            Console.Error.WriteLine(
                $"slangbuild: overlay pad: {Path.GetFileName(binPath)} "
                + $"{currentSize} → {rounded} byte (align {align}, zero-filled)");
        return 0;
    }

    /// <summary>
    /// bin の末尾 (= currentSize の位置) から targetSize まで 0 を明示的に
    /// 書き込む。<c>FileStream.SetLength</c> の拡張領域は Windows で 0 fill
    /// が仕様上未定義 (Microsoft Learn 記載) なので、portable に保証するため
    /// `dd if=/dev/zero conv=notrunc` と等価な明示書き込みで実装する。
    /// 8KB chunk で write することで巨大 padding でもメモリ消費を抑える。
    /// </summary>
    private static void WriteZeroPadding(string binPath, long currentSize, long targetSize)
    {
        long padBytes = targetSize - currentSize;
        if (padBytes <= 0) return;
        const int ChunkSize = 8192;
        var zeros = new byte[(int)Math.Min(padBytes, ChunkSize)];
        using var fs = new FileStream(binPath, FileMode.Open, FileAccess.Write);
        fs.Seek(0, SeekOrigin.End);
        long written = 0;
        while (written < padBytes)
        {
            int chunk = (int)Math.Min(zeros.Length, padBytes - written);
            fs.Write(zeros, 0, chunk);
            written += chunk;
        }
    }

    /// <summary>
    /// `--emit disk` 用の disk image 組み立て phase。
    /// envConfig は Run() 冒頭で <see cref="EnvironmentResolver"/> 経由で解決済み
    /// (= 二重解決排除、Disk null チェックも Run() 側で early reject 済)。
    /// Phase 1 は format=d88 / tool=ndc のみサポート。
    /// </summary>
    private int BuildDiskImage(EnvironmentConfig envConfig, string envPath,
                               string mainBin, IList<string> overlayBins, string outputBase)
    {
        if (_opts.Verbose)
            Console.Error.WriteLine($"slangbuild: disk: env loaded from {envPath}");

        // Run() 冒頭で early reject 済みだが、防御的に再 check (= 内部不整合検出)
        if (envConfig.Disk == null)
        {
            Console.Error.WriteLine(
                $"slangbuild: --emit disk requires `disk:` section in env: {envPath}");
            return 1;
        }

        // 制約 check (Phase 2: format=d88 のみ、tool=ndc/hudisk 許容)
        if (envConfig.Disk.Format != "d88")
        {
            Console.Error.WriteLine(
                $"slangbuild: only disk.format: d88 supported (got: {envConfig.Disk.Format})");
            return 1;
        }
        if (envConfig.Disk.Tool != "ndc"
            && envConfig.Disk.Tool != "hudisk"
            && envConfig.Disk.Tool != "udostool")
        {
            Console.Error.WriteLine(
                $"slangbuild: only disk.tool: ndc / hudisk / udostool supported "
                + $"(got: {envConfig.Disk.Tool})");
            return 1;
        }

        // 出力 disk image path: --disk-image 指定があればそれ、無ければ <output_prefix>.d88
        var diskOut = !string.IsNullOrEmpty(_opts.DiskImagePath)
            ? Path.GetFullPath(_opts.DiskImagePath)
            : outputBase + ".d88";

        // tool ごとに必要な実行ファイルだけ resolve (= 不要な resolver で fail させない)
        ResolvedTool? ndc = null;
        ResolvedTool? hudisk = null;
        ResolvedTool? udostool = null;
        if (envConfig.Disk.Tool == "ndc")
        {
            ndc = _resolver.ResolveNdc(_opts.NdcPath);
            if (_opts.Verbose) Console.Error.WriteLine($"slangbuild: using ndc: {ndc.Path}");
        }
        else if (envConfig.Disk.Tool == "hudisk")
        {
            hudisk = _resolver.ResolveHudisk(_opts.HudiskPath);
            if (_opts.Verbose)
            {
                var via = hudisk.Kind == ResolutionKind.MonoRun
                    ? $"mono {hudisk.ProjectPath}"
                    : hudisk.Path;
                Console.Error.WriteLine($"slangbuild: using HuDisk: {via}");
            }
        }
        else if (envConfig.Disk.Tool == "udostool")
        {
            udostool = _resolver.ResolveUdostool(_opts.UdostoolPath);
            if (_opts.Verbose)
            {
                var via = udostool.Kind == ResolutionKind.MonoRun
                    ? $"mono {udostool.ProjectPath}"
                    : udostool.Path;
                Console.Error.WriteLine($"slangbuild: using udostool: {via}");
            }
        }

        // --disk-template が指定されていれば env の disk.template を override
        var templateOverride = !string.IsNullOrEmpty(_opts.DiskTemplatePath)
            ? Path.GetFullPath(_opts.DiskTemplatePath)
            : null;
        if (_opts.Verbose && templateOverride != null)
            Console.Error.WriteLine($"slangbuild: disk template override: {templateOverride}");

        var builder = new DiskImageBuilder(envConfig.Disk, ndc, hudisk, udostool,
                                           _opts.Verbose, templateOverride);
        return builder.Build(mainBin, overlayBins, diskOut);
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
