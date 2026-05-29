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
        public string? Mzd88Path { get; set; }
        /// <summary>`--oscar-path &lt;path&gt;`。BackendKind.OscarC env のとき
        /// oscar64 binary を上書き指定する。null なら env file の
        /// <c>oscar_path:</c> → <c>$OSCAR64</c> → PATH の順で探索。</summary>
        public string? OscarPath { get; set; }

        /// <summary>`--c-source &lt;path&gt;` repeatable。BackendKind.OscarC 専用、
        /// oscar64 invoke の source list 末尾 (env.CRuntimeFiles の後) に
        /// ユーザー C ファイルを追加する。CFUNC 宣言で参照する関数の実体や、
        /// SLANG では書けない C コード片を混ぜるために使う。Z80 backend で
        /// 非空なら early reject。</summary>
        public List<string> CSourceFiles { get; } = new();
        public bool KeepAsm { get; set; }
        public bool Verbose { get; set; }
        /// <summary>slangc に pass-through する `-I &lt;path&gt;` の値リスト</summary>
        public List<string> IncludePaths { get; } = new();
        /// <summary>slangc に pass-through する `-L &lt;path&gt;` の値リスト</summary>
        public List<string> LibraryPaths { get; } = new();

        /// <summary>"bin" (default) or "disk" or "tape"。"disk" は env の `disk:`
        /// セクション必須、 "tape" は raw bin output env (= Z80 backend + OutputFormat
        /// null/bin) + env の `tape:` セクション or CLI tape option 必須 (Phase B)。</summary>
        public string EmitMode { get; set; } = "bin";
        /// <summary>`--disk-image &lt;path&gt;`。EmitMode == "disk" 時のみ意味を持つ。
        /// null の場合は &lt;output_prefix&gt;.d88 を使う。</summary>
        public string? DiskImagePath { get; set; }
        /// <summary>`--disk-template &lt;path&gt;`。env file の disk.template を CLI で
        /// override する。EmitMode == "disk" 時のみ意味を持つ。null/空なら env 値を使う。</summary>
        public string? DiskTemplatePath { get; set; }

        // === Phase B: --emit tape 関連 ===
        /// <summary>`--wav`。EmitMode == "tape" 時に .wav も同時生成。</summary>
        public bool EmitWav { get; set; }
        /// <summary>`--tape-name &lt;name&gt;`。env.Tape.Name を CLI override。</summary>
        public string? TapeName { get; set; }
        /// <summary>`--tape-load &lt;addr&gt;`。env.Tape.Load (or env.DefaultOrg) を CLI override。
        /// 例: `--tape-load '$1000'` or `--tape-load 0x1000`。</summary>
        public int? TapeLoad { get; set; }
        /// <summary>`--tape-exec &lt;addr&gt;`。env.Tape.Exec (or load) を CLI override。</summary>
        public int? TapeExec { get; set; }

        /// <summary>`--tape-add <file[@load[:exec]]>` (repeatable)。EmitMode == "tape" 時、
        /// 生バイナリ (= Arkos driver / BGM / SFX 等) を main の後続 tape stage として連結。
        /// load/exec は省略可 (= MTREAD は呼出側 addr 引数優先で info block の load を無視するため、
        /// 省略時 0)。 stage 順 = tape 物理順 = SLANG の MTREAD 呼び出し順。</summary>
        public List<string> TapeAddSpecs { get; } = new();

        // === Phase B+: SLFS (= --emit disk + disk.tool: slfs-pack) 関連 ===
        /// <summary>`--slfs-add <name>:<path>` or `--slfs-add <dir>` (repeatable)。
        /// disk.tool == "slfs-pack" 時の asset list (= SLFS dir entry)。
        /// 各要素: (name, path)。 dir 指定なら BuildDisk 側で展開。</summary>
        public List<string> SlfsAddSpecs { get; } = new();
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

        // === Backend dispatch ===
        // OscarC backend は AILZ80ASM 前提の組み立てを通らない。Z80 とは
        // 出力 format / overlay / disk が全く別物なので、Run() 冒頭で早期に
        // 別 method に逃がす設計 (= レビュー指摘の RunOscarC 案)。
        if (envConfig.Backend == BackendKind.OscarC)
        {
            // OscarC backend で disk 系オプション (--emit disk / --disk-image /
            // --disk-template) を指定したら early reject。silent ignore で
            // .prg だけ生成され disk image は作られない silent wrong 事故を防ぐ
            // (codex review 反映)。
            if (_opts.EmitMode != "bin")
            {
                Console.Error.WriteLine(
                    $"slangbuild: --emit {_opts.EmitMode} is not supported by `backend: oscar_c` env "
                    + "(only `--emit bin` / default for OscarC).");
                return 1;
            }
            if (!string.IsNullOrEmpty(_opts.DiskImagePath))
            {
                Console.Error.WriteLine(
                    "slangbuild: --disk-image is not supported by `backend: oscar_c` env.");
                return 1;
            }
            if (!string.IsNullOrEmpty(_opts.DiskTemplatePath))
            {
                Console.Error.WriteLine(
                    "slangbuild: --disk-template is not supported by `backend: oscar_c` env.");
                return 1;
            }
            return RunOscarC(envConfig, envPath);
        }

        // Z80 backend で --c-source 指定 → early reject
        // (env file の oscar_*/c_runtime_* と同じ排他検証の規律)
        if (_opts.CSourceFiles.Count > 0)
        {
            Console.Error.WriteLine(
                "slangbuild: --c-source requires `backend: oscar_c` env (current env is Z80)");
            return 1;
        }

        // --emit disk + disk: セクション無しは早期 reject (= 無駄な compile/asm 回避)
        if (_opts.EmitMode == "disk" && envConfig.Disk == null)
        {
            Console.Error.WriteLine(
                $"slangbuild: --emit disk requires `disk:` section in env: {envPath}");
            return 1;
        }

        // --emit tape は raw bin output env 限定 (= Phase B)。
        // Z80 backend + OutputFormat null/bin のみ許容、 cmt / c_source は reject。
        // Driver.RunOscarC は別 path で Z80 dispatch 前に分岐するため、 ここでは
        // Z80 backend 想定の OutputFormat check のみ。
        if (_opts.EmitMode == "tape")
        {
            if (envConfig.OutputFormat != null)
            {
                Console.Error.WriteLine(
                    $"slangbuild: --emit tape requires raw bin output env " +
                    $"(got `output: {envConfig.OutputFormat}`) in env: {envPath}");
                return 1;
            }
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
            // === Step 0.5: SLFS Phase 3 - compile 前 generated header 出力 ===
            // EmitMode == disk && disk.tool == slfs-pack && --slfs-add 指定済 の case で
            // asset list → SLANG CONST 形式 .inc 生成。 SLANG sample が
            // `#INCLUDE <input SL stem>.assets.inc` で取り込んで FILE_XXX identifier
            // で asset id を書ける。 `-o` basename と decoupling (= header 名は input
            // SL stem 基準で固定、 -o /tmp/OTHER 等で source stem と異なっても OK)。
            List<string>? slfsExtraIncludes = null;
            if (_opts.EmitMode == "disk"
                && envConfig.Disk?.Tool == "slfs-pack"
                && _opts.SlfsAddSpecs.Count > 0)
            {
                var slStem = Path.GetFileNameWithoutExtension(_opts.InputPath);
                var slfsIncPath = Path.Combine(outputDir, slStem + ".assets.inc");
                var slfsAssets = SlfsAssetResolver.Resolve(_opts.SlfsAddSpecs);
                var hdr = SlfsPack.SlfsHeaderBuilder.Build(slfsAssets);
                if (!string.IsNullOrEmpty(hdr))
                {
                    if (!Directory.Exists(outputDir)) Directory.CreateDirectory(outputDir);
                    File.WriteAllText(slfsIncPath, hdr);
                    intermediates.Add(slfsIncPath);
                    slfsExtraIncludes = new List<string> { outputDir };
                    if (_opts.Verbose)
                        Console.Error.WriteLine($"slangbuild: slfs: wrote generated header {slfsIncPath} ({hdr.Length} byte, {slfsAssets.Count} asset(s))");
                }
            }

            // === Step 1: slangc spawn ===
            var slangc = _resolver.ResolveSlangc(_opts.SlangcPath);
            if (_opts.Verbose) Console.Error.WriteLine($"slangbuild: using slangc: {slangc}");

            var slangcResult = SpawnSlangc(slangc, _opts.InputPath, mainAsm, _opts.Environment, slfsExtraIncludes);
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
                $"^{System.Text.RegularExpressions.Regex.Escape(prefix)}\\._m(\\d+)\\.ASM$",
                System.Text.RegularExpressions.RegexOptions.IgnoreCase);
            // overlay 順序は **数値順** (= _m2 < _m10、 lexicographic だと _m10 < _m2 で
            //  10+ overlay 時に多段 tape stage 順が壊れる、 Codex review 指摘)。
            // regex group 1 (= _m の後の digit 列) を int parse して OrderBy。
            var overlayAsms = Directory.GetFiles(outputDir, prefix + "._m*.ASM")
                                       .Select(p => new { Path = p, Match = overlayPattern.Match(Path.GetFileName(p)) })
                                       .Where(x => x.Match.Success)
                                       .OrderBy(x => int.Parse(x.Match.Groups[1].Value))
                                       .Select(x => x.Path)
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

            // === Step 4 (Phase B+): --emit tape → X1 .tap (+ optional .wav) 組み立て ===
            //  + 多段 tape (= #MODULE overlay の自動連結) 対応
            if (_opts.EmitMode == "tape")
            {
                var binBytes = File.ReadAllBytes(mainBin);
                var mainCfg = TapeImageBuilder.MergeTapeConfig(envConfig, _opts, outputBase);

                // overlay 検出時: 各 overlay を tape stage として収集
                // (= overlay ASM 冒頭 `ORG $XXXX` (CodeGenerator.cs L143 出力) を
                //  regex parse して overlay load addr 取得、 各 overlay bin を
                //  tape stages list 化 → TapeImageBuilder で連結 .tap 生成)
                List<(byte[] bin, TapeImageBuilder.ResolvedTapeConfig cfg)>? additionalStages = null;
                if (renamedOverlayBins.Count > 0)
                {
                    additionalStages = new();
                    var orgRegex = new System.Text.RegularExpressions.Regex(
                        @"^\s*ORG\s+\$([0-9A-Fa-f]+)\b",
                        System.Text.RegularExpressions.RegexOptions.Multiline);
                    for (int i = 0; i < renamedOverlayBins.Count; i++)
                    {
                        var overlayAsmText = File.ReadAllText(overlayAsms[i]);
                        var orgMatch = orgRegex.Match(overlayAsmText);
                        if (!orgMatch.Success)
                        {
                            Console.Error.WriteLine(
                                $"slangbuild: overlay {i} ASM の冒頭 `ORG $XXXX` parse 失敗 " +
                                $"(file: {overlayAsms[i]})、 多段 tape 連結不能");
                            return 1;
                        }
                        var orgAddr = Convert.ToInt32(orgMatch.Groups[1].Value, 16);
                        var bytes = File.ReadAllBytes(renamedOverlayBins[i]);
                        // overlay stage の exec addr は header 上 formality (= load と同値書込)、
                        // MTREAD は次 block の data を読むだけで exec は使わない (= docs 明記)。
                        var overlayCfg = new TapeImageBuilder.ResolvedTapeConfig(
                            Name: $"M{i}",
                            Load: orgAddr,
                            Exec: orgAddr,
                            WavSampleRate: mainCfg.WavSampleRate,
                            WavBits: mainCfg.WavBits);
                        additionalStages.Add((bytes, overlayCfg));
                    }
                }

                // --tape-add: 生バイナリ (= Arkos driver / BGM / SFX 等) を後続 tape stage に連結。
                // spec = file[@load[:exec]]。 load/exec は省略可 (= MTREAD は呼出側 addr 引数優先で
                // info block load を無視、 省略時 0)。 overlay stage の後ろに append (= stage 順 =
                // overlay → tape-add = tape 物理順 = MTREAD 呼び出し順)。
                if (_opts.TapeAddSpecs.Count > 0)
                {
                    additionalStages ??= new();
                    foreach (var spec in _opts.TapeAddSpecs)
                    {
                        if (!ParseTapeAddSpec(spec, out var filePath, out var load, out var exec))
                        {
                            Console.Error.WriteLine(
                                $"slangbuild: invalid --tape-add spec: {spec} (= file[@load[:exec]])");
                            return 1;
                        }
                        if (!File.Exists(filePath))
                        {
                            Console.Error.WriteLine($"slangbuild: --tape-add file not found: {filePath}");
                            return 1;
                        }
                        var bytes = File.ReadAllBytes(filePath);
                        var stageCfg = new TapeImageBuilder.ResolvedTapeConfig(
                            Name: Path.GetFileNameWithoutExtension(filePath),
                            Load: load,                 // 省略時 0
                            Exec: exec ?? load,         // exec 省略時 load と同値
                            WavSampleRate: mainCfg.WavSampleRate,
                            WavBits: mainCfg.WavBits);
                        additionalStages.Add((bytes, stageCfg));
                    }
                }

                int tapeRc = new TapeImageBuilder().Build(
                    binBytes, mainCfg, additionalStages, outputBase, _opts.EmitWav, _opts.Verbose);
                if (tapeRc != 0) return tapeRc;
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
    /// BackendKind.OscarC 用のフロー (= 完全別経路、AILZ80ASM / overlay /
    /// disk / cmt の組み立てロジックを通らない)。
    ///
    /// Flow:
    ///   1) slangc -E &lt;env&gt; -o &lt;prefix&gt;.c &lt;input.SL&gt;
    ///   2) oscar64 -tm=&lt;machine&gt; -tf=&lt;format&gt; [-psci] {-i=...}
    ///      &lt;prefix&gt;.c {runtime/c64/slang_runtime.c} -o=&lt;prefix&gt;.prg
    ///   3) cleanup (--keep-asm 指定時は .c も残す、それ以外は削除)
    ///
    /// envConfig は Run() 冒頭で解決済み (envConfig.Backend == OscarC が確定)。
    /// EmitMode は使わない (`--emit disk` は OscarC backend で意味を持たない、
    /// 必要なら別途エラー化を検討)。
    /// </summary>
    private int RunOscarC(EnvironmentConfig envConfig, string envPath)
    {
        // 出力 path: -o は prefix としても完全 path としても受ける。slangbuild は
        // prefix 運用 (= <prefix>.c と <prefix>.prg を生成)。
        // slangc 単体は -o を完全 path として扱うので、ここでは .c を明示付与。
        string outputBase = _opts.OutputPrefix != null
            ? Path.GetFullPath(_opts.OutputPrefix)
            : Path.Combine(
                Path.GetDirectoryName(Path.GetFullPath(_opts.InputPath))!,
                DerivePrefix(_opts.InputPath));
        var cPath = outputBase + ".c";
        var prgPath = outputBase + ".prg";

        var intermediates = new List<string>();

        // === Step 1: slangc spawn → <prefix>.c ===
        var slangc = _resolver.ResolveSlangc(_opts.SlangcPath);
        if (_opts.Verbose) Console.Error.WriteLine($"slangbuild: using slangc: {slangc.Path}");
        var slangcResult = SpawnSlangc(slangc, _opts.InputPath, cPath, _opts.Environment);
        if (slangcResult != 0)
        {
            Console.Error.WriteLine($"slangbuild: slangc failed (exit {slangcResult})");
            return slangcResult;
        }
        if (!File.Exists(cPath))
        {
            Console.Error.WriteLine($"slangbuild: slangc did not produce expected output: {cPath}");
            return 1;
        }
        intermediates.Add(cPath);

        // === Step 2: oscar64 invoke ===
        var oscarBin = OscarInvoker.FindOscarBinary(_opts.OscarPath, envConfig);
        if (oscarBin == null)
        {
            Console.Error.WriteLine(
                "slangbuild: oscar64 binary not found. Install oscar64 and either "
                + "(1) put it on PATH, (2) set $OSCAR64, "
                + "(3) set `oscar_path:` in env file, or "
                + "(4) pass --oscar-path <path>.");
            return 1;
        }
        if (_opts.Verbose) Console.Error.WriteLine($"slangbuild: using oscar64: {oscarBin}");

        // CRuntimeFiles の存在 check (= 後段で oscar64 が file not found を出すよりも
        // 早く分かりやすいメッセージを出す)
        if (envConfig.CRuntimeFiles != null)
        {
            foreach (var rt in envConfig.CRuntimeFiles)
            {
                if (!File.Exists(rt))
                {
                    Console.Error.WriteLine($"slangbuild: c_runtime_files not found: {rt}");
                    return 1;
                }
            }
        }

        // --c-source で指定されたユーザー C ファイルを検証 + 絶対化。
        // cwd 起点で絶対化 (= 既存 InputPath と同じ流儀)。
        var extraCSources = new List<string>();
        foreach (var src in _opts.CSourceFiles)
        {
            var abs = Path.GetFullPath(src);
            if (!File.Exists(abs))
            {
                Console.Error.WriteLine($"slangbuild: --c-source file not found: {abs}");
                return 1;
            }
            extraCSources.Add(abs);
            if (_opts.Verbose) Console.Error.WriteLine($"slangbuild: extra C source: {abs}");
        }

        var invoker = new OscarInvoker(oscarBin, _opts.Verbose);
        var result = invoker.Compile(cPath, prgPath, envConfig, extraCSources);
        if (!result.Success)
        {
            if (!string.IsNullOrEmpty(result.Stdout)) Console.Error.Write(result.Stdout);
            if (!string.IsNullOrEmpty(result.Stderr)) Console.Error.Write(result.Stderr);
            Console.Error.WriteLine($"slangbuild: oscar64 failed (exit {result.ExitCode})");
            return result.ExitCode == 0 ? 1 : result.ExitCode;
        }
        if (_opts.Verbose && !string.IsNullOrEmpty(result.Stdout))
            Console.Error.Write(result.Stdout);

        if (!File.Exists(prgPath))
        {
            Console.Error.WriteLine($"slangbuild: oscar64 did not produce expected output: {prgPath}");
            return 1;
        }

        // === Step 3: cleanup ===
        // oscar64 は副産物として .asm / .map / .int / .lbl / .dbj を吐く。
        // --keep-asm 指定時は全て残す。デフォルトは intermediates (= .c) のみ削除し、
        // oscar64 副産物は user の debug 用に残す (= 既存 AILZ80ASM 経路は .ASM を
        // intermediates として管理するのと同じ慣行で .c は管理する)。
        if (!_opts.KeepAsm)
        {
            foreach (var p in intermediates)
            {
                try { File.Delete(p); } catch { }
            }
        }

        return 0;
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
            && envConfig.Disk.Tool != "udostool"
            && envConfig.Disk.Tool != "mzd88"
            && envConfig.Disk.Tool != "slfs-pack")
        {
            Console.Error.WriteLine(
                $"slangbuild: only disk.tool: ndc / hudisk / udostool / mzd88 / slfs-pack supported "
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
        ResolvedTool? mzd88 = null;
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
        else if (envConfig.Disk.Tool == "mzd88")
        {
            mzd88 = _resolver.ResolveMzd88(_opts.Mzd88Path);
            if (_opts.Verbose) Console.Error.WriteLine($"slangbuild: using mzd88: {mzd88.Path}");
        }

        // --disk-template が指定されていれば env の disk.template を override
        var templateOverride = !string.IsNullOrEmpty(_opts.DiskTemplatePath)
            ? Path.GetFullPath(_opts.DiskTemplatePath)
            : null;
        if (_opts.Verbose && templateOverride != null)
            Console.Error.WriteLine($"slangbuild: disk template override: {templateOverride}");

        var builder = new DiskImageBuilder(envConfig.Disk, ndc, hudisk, udostool, mzd88,
                                           _opts.Verbose, templateOverride, _opts.SlfsAddSpecs);
        return builder.Build(mainBin, overlayBins, diskOut);
    }

    private int SpawnSlangc(ResolvedTool slangc, string inputPath, string outAsmPath, string env,
                            IList<string>? extraIncludePaths = null)
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
        // extraIncludePaths: caller 指定の追加 -I (= 例 SLFS Phase 3 で
        // generated header 配置 outputDir、 常時 add 副作用回避のため明示渡し)
        if (extraIncludePaths != null)
        {
            foreach (var p in extraIncludePaths)
            {
                psi.ArgumentList.Add("-I");
                psi.ArgumentList.Add(p);
            }
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

    /// <summary>
    /// `--tape-add` の spec を parse: `file[@load[:exec]]`。
    /// load 省略時 0、 exec 省略時 null (= 呼出側で load にフォールバック)。
    /// addr は `$XXXX` / `0xXXXX` / decimal。 file path 内の `:` (= Windows drive) は
    /// `@` より前なので影響しない。
    /// internal: X1NativeTapeTests から直接 parse 検証するため。
    /// </summary>
    internal static bool ParseTapeAddSpec(string spec, out string filePath, out int load, out int? exec)
    {
        filePath = spec;
        load = 0;
        exec = null;
        if (string.IsNullOrWhiteSpace(spec)) return false;

        int at = spec.IndexOf('@');
        if (at < 0)
        {
            filePath = spec;   // file 名のみ (= load/exec なし)
            return filePath.Length > 0;
        }

        filePath = spec.Substring(0, at);
        if (filePath.Length == 0) return false;

        var addrPart = spec.Substring(at + 1);
        int colon = addrPart.IndexOf(':');
        string loadStr;
        string? execStr = null;
        if (colon < 0)
        {
            loadStr = addrPart;
        }
        else
        {
            loadStr = addrPart.Substring(0, colon);
            execStr = addrPart.Substring(colon + 1);
        }
        if (!TryParseAddr(loadStr, out load)) return false;
        if (execStr != null)
        {
            if (!TryParseAddr(execStr, out int e)) return false;
            exec = e;
        }
        return true;
    }

    /// <summary>
    /// address 文字列を int に parse (= `$XXXX` / `0xXXXX` / decimal)。
    /// Program.TryParseAddress と同等 (= Driver から使うための再実装、 小さいので複製)。
    /// </summary>
    private static bool TryParseAddr(string s, out int value)
    {
        value = 0;
        if (string.IsNullOrWhiteSpace(s)) return false;
        s = s.Trim();
        if (s.StartsWith("$"))
            return int.TryParse(s.Substring(1), System.Globalization.NumberStyles.HexNumber,
                System.Globalization.CultureInfo.InvariantCulture, out value);
        if (s.StartsWith("0x") || s.StartsWith("0X"))
            return int.TryParse(s.Substring(2), System.Globalization.NumberStyles.HexNumber,
                System.Globalization.CultureInfo.InvariantCulture, out value);
        return int.TryParse(s, System.Globalization.NumberStyles.Integer,
            System.Globalization.CultureInfo.InvariantCulture, out value);
    }
}
