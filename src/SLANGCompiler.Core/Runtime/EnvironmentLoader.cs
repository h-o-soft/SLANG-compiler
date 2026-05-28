using YamlDotNet.Serialization;
using YamlDotNet.Serialization.NamingConventions;

namespace SLANGCompiler.Runtime;

/// <summary>
/// .envファイル（YAML）からプラットフォーム環境設定を読み込む
/// </summary>
public class EnvironmentLoader
{
    /// <summary>
    /// .envファイルを読み込んでEnvironmentConfigを返す
    /// </summary>
    public static EnvironmentConfig Load(string envFilePath)
    {
        var text = File.ReadAllText(envFilePath);
        var deserializer = new DeserializerBuilder()
            .WithNamingConvention(UnderscoredNamingConvention.Instance)
            .IgnoreUnmatchedProperties()
            .Build();
        var raw = deserializer.Deserialize<EnvFileData>(text);

        var config = new EnvironmentConfig
        {
            Name = Path.GetFileNameWithoutExtension(envFilePath),
            EnvType = raw.EnvType,
            OsType = raw.OsType,
        };

        // ORGアドレス
        if (!string.IsNullOrEmpty(raw.DefaultOrg))
            config.DefaultOrg = ParseAddress(raw.DefaultOrg);

        // WORKアドレス
        if (!string.IsNullOrEmpty(raw.DefaultWork))
            config.DefaultWork = ParseAddress(raw.DefaultWork);

        // コード領域の読取専用フラグ（ROM環境）
        config.CodeReadonly = raw.CodeReadonly;

        // backend: コンパイラ backend 種別 (= 出力経路の選択)。
        // 未指定 / "z80" = BackendKind.Z80 (既存挙動)、"oscar_c" = BackendKind.OscarC。
        // typo は明示 reject。
        var rawBackend = raw.Backend?.Trim();
        if (string.IsNullOrEmpty(rawBackend) || rawBackend.Equals("z80", StringComparison.OrdinalIgnoreCase))
        {
            config.Backend = BackendKind.Z80;
        }
        else if (rawBackend.Equals("oscar_c", StringComparison.OrdinalIgnoreCase))
        {
            config.Backend = BackendKind.OscarC;
        }
        else
        {
            throw new InvalidDataException(
                $"Invalid `backend:` value '{rawBackend}' in {envFilePath}. "
                + "Allowed: z80 (default) / oscar_c.");
        }

        // output: 出力 format。allowlist は backend で異なる:
        //   Z80     : null/bin (default) / cmt
        //   OscarC  : c_source 必須
        // null/空 = null、"bin" は null 正規化、それ以外は allowlist 確認。
        var rawOutput = raw.Output?.Trim();
        if (string.IsNullOrEmpty(rawOutput))
        {
            config.OutputFormat = null;
        }
        else
        {
            var normalized = rawOutput.ToLowerInvariant();
            if (normalized != "bin" && normalized != "cmt" && normalized != "c_source")
            {
                throw new InvalidDataException(
                    $"Invalid `output:` value '{rawOutput}' in {envFilePath}. "
                    + "Allowed: bin (default) / cmt / c_source.");
            }
            config.OutputFormat = (normalized == "bin") ? null : normalized;
        }

        // backend / output の整合 check
        if (config.Backend == BackendKind.OscarC && config.OutputFormat != "c_source")
        {
            throw new InvalidDataException(
                $"`backend: oscar_c` requires `output: c_source` in {envFilePath}.");
        }
        if (config.Backend == BackendKind.Z80 && config.OutputFormat == "c_source")
        {
            throw new InvalidDataException(
                $"`output: c_source` requires `backend: oscar_c` in {envFilePath}.");
        }

        // ライブラリリスト（.yml → .asm に変換）
        if (raw.Libraries != null)
        {
            foreach (var lib in raw.Libraries)
            {
                // runtime.yml → runtime.asm, liblsx_print.yml → liblsx_print.asm
                var asmName = Path.ChangeExtension(lib, ".asm");
                config.Libraries.Add(asmName);
            }
        }

        // === CMT 系新フィールドの整合 check ===
        // cmt_concat (pc80mk2x 経路) / cmt_assets (pc80mk2xsd 経路) /
        // overlay_name (overlay rename template) / overlay_output_format
        // (overlay 専用 format) は全て output: cmt 専用 (= bin / 未指定 env で
        // 指定すると壊れたファイル生成 silent wrong になるため reject)。
        bool hasCmtConcat = raw.CmtConcat != null && raw.CmtConcat.Count > 0;
        bool hasCmtAssets = raw.CmtAssets != null && raw.CmtAssets.Count > 0;
        bool hasOverlayName = !string.IsNullOrEmpty(raw.OverlayName);
        bool hasOverlayOutputFormat = !string.IsNullOrEmpty(raw.OverlayOutputFormat);
        bool hasAnyCmtField = hasCmtConcat || hasCmtAssets || hasOverlayName
                              || hasOverlayOutputFormat;

        if (hasAnyCmtField && config.OutputFormat != "cmt")
        {
            throw new InvalidDataException(
                $"`cmt_concat` / `cmt_assets` / `overlay_name` / `overlay_output_format` "
                + $"require `output: cmt` in {envFilePath}.");
        }

        // cmt_concat と cmt_assets は build flow が排他 (= 同 env で両方指定
        // すると結合と個別配置が衝突)。両方 non-empty で reject。
        if (hasCmtConcat && hasCmtAssets)
        {
            throw new InvalidDataException(
                $"`cmt_concat` and `cmt_assets` are mutually exclusive in {envFilePath}.");
        }

        var envDir = Path.GetDirectoryName(Path.GetFullPath(envFilePath))!;

        if (hasCmtConcat)
        {
            config.CmtConcat = raw.CmtConcat!
                .Select(p => Path.GetFullPath(Path.Combine(envDir, p)))
                .ToList();
        }
        if (hasCmtAssets)
        {
            config.CmtAssets = raw.CmtAssets!
                .Select(p => Path.GetFullPath(Path.Combine(envDir, p)))
                .ToList();
        }

        // overlay_output_format allowlist (= bin / cmt のみ、output: と同じ)
        if (hasOverlayOutputFormat)
        {
            var ofnorm = raw.OverlayOutputFormat!.Trim().ToLowerInvariant();
            if (ofnorm != "bin" && ofnorm != "cmt")
            {
                throw new InvalidDataException(
                    $"Invalid `overlay_output_format:` value '{raw.OverlayOutputFormat}' "
                    + $"in {envFilePath}. Allowed: bin / cmt.");
            }
            config.OverlayOutputFormat = ofnorm;
        }

        // defines: env が自動定義する名前→値 map (integer 限定)。slangc 側で
        // Preprocessor.DefineConst() 経由で SL の #IF NAME==VAL に、slangbuild
        // 側で AILZ80ASM `-dl NAME=VAL` 引数で参照される (= ASM 側 #IF exists
        // NAME も活きる)。例: pc80mk2xsd で PC8001_SD: 1 を定義すれば、SL に
        // CONST ASM 行を書かずに SD 経路が自動的に有効化される。
        // 名前 validate: ^[A-Za-z_][A-Za-z0-9_]*$ (= C/asm 識別子規則と同等)。
        if (raw.Defines != null && raw.Defines.Count > 0)
        {
            var nameRe = new System.Text.RegularExpressions.Regex(
                @"^[A-Za-z_][A-Za-z0-9_]*$");
            var validated = new Dictionary<string, int>();
            foreach (var (name, value) in raw.Defines)
            {
                if (!nameRe.IsMatch(name))
                {
                    throw new InvalidDataException(
                        $"Invalid `defines:` name '{name}' in {envFilePath}. "
                        + "Must match ^[A-Za-z_][A-Za-z0-9_]*$.");
                }
                validated[name] = value;
            }
            config.Defines = validated;
        }

        // bin_pad_size / overlay_pad_align: bin 出力 (= output: cmt 以外)
        // 専用。cmt 環境で指定すると header 込み bin に padding する形に
        // なり意味不明なので reject。null / 0 / 負 = padding なし
        // (= 設定漏れ寛容、明示的 reject はしない、Loader 上で null 相当に
        // 正規化)。
        bool hasBinPadSize = raw.BinPadSize.HasValue && raw.BinPadSize.Value > 0;
        bool hasOverlayPadAlign =
            raw.OverlayPadAlign.HasValue && raw.OverlayPadAlign.Value > 0;
        if ((hasBinPadSize || hasOverlayPadAlign) && config.OutputFormat == "cmt")
        {
            throw new InvalidDataException(
                $"`bin_pad_size` / `overlay_pad_align` are not allowed with "
                + $"`output: cmt` in {envFilePath}.");
        }
        if (hasBinPadSize) config.BinPadSize = raw.BinPadSize!.Value;
        if (hasOverlayPadAlign) config.OverlayPadAlign = raw.OverlayPadAlign!.Value;

        // overlay_name validate (path 安全性 + {index} 必須):
        // - {index} placeholder 必須 (= 無いと overlay 複数個で全部同じ path
        //   に上書き silent wrong)
        // - absolute path 拒否、separator/.. 禁止 (= output dir 外書き防御)
        if (hasOverlayName)
        {
            var name = raw.OverlayName!;
            if (!name.Contains("{index}"))
            {
                throw new InvalidDataException(
                    $"`overlay_name:` must contain `{{index}}` placeholder "
                    + $"in {envFilePath} (got: '{name}').");
            }
            if (Path.IsPathRooted(name) || Path.GetFileName(name) != name)
            {
                throw new InvalidDataException(
                    $"`overlay_name:` must be a single file name without "
                    + $"directory separator or `..` in {envFilePath} (got: '{name}').");
            }
            config.OverlayName = name;
        }

        // === oscar64 backend 系フィールドの parse + 排他検証 ===
        // OscarC でのみ使えるフィールドは: oscar_path / oscar_machine / oscar_format /
        //   oscar_optimize / oscar_petscii / c_runtime_files / c_runtime_includes
        // Z80 backend でこれらが指定されたら typo 早期検出のため reject。
        bool hasOscarPath = !string.IsNullOrWhiteSpace(raw.OscarPath);
        bool hasOscarMachine = !string.IsNullOrWhiteSpace(raw.OscarMachine);
        bool hasOscarFormat = !string.IsNullOrWhiteSpace(raw.OscarFormat);
        bool hasOscarOptimize = !string.IsNullOrWhiteSpace(raw.OscarOptimize);
        bool hasOscarPetscii = raw.OscarPetscii.HasValue;
        bool hasCRuntimeFiles = raw.CRuntimeFiles != null && raw.CRuntimeFiles.Count > 0;
        bool hasCRuntimeIncludes = raw.CRuntimeIncludes != null && raw.CRuntimeIncludes.Count > 0;
        bool hasCBindings = raw.CBindings != null && raw.CBindings.Count > 0;
        bool hasAnyOscarField = hasOscarPath || hasOscarMachine || hasOscarFormat
                              || hasOscarOptimize || hasOscarPetscii
                              || hasCRuntimeFiles || hasCRuntimeIncludes
                              || hasCBindings;

        if (config.Backend == BackendKind.Z80 && hasAnyOscarField)
        {
            throw new InvalidDataException(
                $"`oscar_*` / `c_runtime_*` / `c_bindings` fields require `backend: oscar_c` in {envFilePath}.");
        }

        if (config.Backend == BackendKind.OscarC)
        {
            // Z80 専用フィールドの混入を reject (typo 早期検出)。
            if (config.Libraries.Count > 0)
                throw new InvalidDataException(
                    $"`libraries:` is not allowed with `backend: oscar_c` in {envFilePath}.");
            if (raw.Disk != null)
                throw new InvalidDataException(
                    $"`disk:` is not allowed with `backend: oscar_c` in {envFilePath}.");
            if (hasBinPadSize || hasOverlayPadAlign)
                throw new InvalidDataException(
                    $"`bin_pad_size` / `overlay_pad_align` are not allowed with `backend: oscar_c` in {envFilePath}.");
            if (hasCmtConcat || hasCmtAssets || hasOverlayName || hasOverlayOutputFormat)
                throw new InvalidDataException(
                    $"`cmt_concat` / `cmt_assets` / `overlay_name` / `overlay_output_format` "
                    + $"are not allowed with `backend: oscar_c` in {envFilePath}.");

            // c_runtime_files は最低 1 件必須 (slang_runtime.c 等)。
            if (!hasCRuntimeFiles)
                throw new InvalidDataException(
                    $"`c_runtime_files:` is required with `backend: oscar_c` in {envFilePath}.");

            // oscar_* フィールドを config に流し込み (絶対化必要なものは絶対化)。
            if (hasOscarPath) config.OscarPath = raw.OscarPath!.Trim();
            if (hasOscarMachine) config.OscarMachine = raw.OscarMachine!.Trim();
            if (hasOscarFormat) config.OscarFormat = raw.OscarFormat!.Trim();
            if (hasOscarOptimize) config.OscarOptimize = raw.OscarOptimize!.Trim();
            if (hasOscarPetscii) config.OscarPetscii = raw.OscarPetscii!.Value;

            // c_runtime_files / c_runtime_includes は env file dir 起点で絶対化
            // (既存 Disk.Template / SystemFiles と同じ pattern)。installed 環境でも
            // path が解決できるようにする。
            config.CRuntimeFiles = raw.CRuntimeFiles!
                .Select(p => Path.GetFullPath(Path.Combine(envDir, p)))
                .ToList();
            if (hasCRuntimeIncludes)
            {
                config.CRuntimeIncludes = raw.CRuntimeIncludes!
                    .Select(p => Path.GetFullPath(Path.Combine(envDir, p)))
                    .ToList();
            }

            // c_bindings: env file が提供する C 関数 binding 表 (OscarC 専用)。
            // SLANG ソース側 CFUNC 宣言と同じ意味を YAML 経由で env が用意できる。
            if (raw.CBindings != null && raw.CBindings.Count > 0)
            {
                config.CBindings = ParseCBindings(raw.CBindings, envFilePath);
            }
        }

        // disk セクション (slangbuild --emit disk 用)
        if (raw.Disk != null)
        {
            // template path は env file dir 基準の相対パスを絶対化して保存
            // (= caller 側で再計算しなくて良いように)
            var templateAbs = string.IsNullOrEmpty(raw.Disk.Template)
                ? ""
                : Path.GetFullPath(Path.Combine(envDir, raw.Disk.Template));

            config.Disk = new DiskConfig
            {
                Format = raw.Disk.Format ?? "",
                Template = templateAbs,
                Tool = raw.Disk.Tool ?? "",
                MainName = raw.Disk.MainName ?? "",
                OverlayName = raw.Disk.OverlayName ?? "",
            };
            // HuDisk の -r / -g 用 (lsx/x1 では未指定 = null のまま)
            if (!string.IsNullOrEmpty(raw.Disk.MainLoad))
                config.Disk.MainLoad = ParseAddress(raw.Disk.MainLoad);
            if (!string.IsNullOrEmpty(raw.Disk.MainExec))
                config.Disk.MainExec = ParseAddress(raw.Disk.MainExec);
            if (!string.IsNullOrEmpty(raw.Disk.OverlayLoad))
                config.Disk.OverlayLoad = ParseAddress(raw.Disk.OverlayLoad);

            // udostool の system_files (= ipl/subsys/iosys 等)。
            // env file dir 基準の相対 path を絶対化して保持 (= 既存 disk.template
            // と同じ pattern、installed 環境でも path が解決される)。
            if (raw.Disk.SystemFiles != null && raw.Disk.SystemFiles.Count > 0)
            {
                config.Disk.SystemFiles = raw.Disk.SystemFiles
                    .Select(sf => new DiskSystemFile
                    {
                        Path = string.IsNullOrEmpty(sf.Path)
                            ? ""
                            : Path.GetFullPath(Path.Combine(envDir, sf.Path)),
                        Flag = sf.Flag ?? "",
                    })
                    .ToList();
            }

            // mzd88 の title (= --title 引数、optional)
            if (!string.IsNullOrEmpty(raw.Disk.Title))
                config.Disk.Title = raw.Disk.Title;

            // slfs-pack の volume (= D88 disk volume name、 16 byte ASCII)
            if (!string.IsNullOrEmpty(raw.Disk.Volume))
                config.Disk.Volume = raw.Disk.Volume;

            // mzd88 の extra_files (= 起動用 BASIC ローダ等の追加ファイル)。
            // system_files と同じく env file dir 基準で絶対化。
            if (raw.Disk.ExtraFiles != null && raw.Disk.ExtraFiles.Count > 0)
            {
                config.Disk.ExtraFiles = raw.Disk.ExtraFiles
                    .Select(ef => string.IsNullOrEmpty(ef.Path)
                        ? ""
                        : Path.GetFullPath(Path.Combine(envDir, ef.Path)))
                    .ToList();
            }
        }

        // tape セクション (Phase B、 slangbuild --emit tape 用)
        if (raw.Tape != null)
        {
            config.Tape = new TapeConfig
            {
                Name = raw.Tape.Name,
                WavSampleRate = raw.Tape.WavSampleRate,
                WavBits = raw.Tape.WavBits,
            };
            // load/exec は "$XXXX" / "0xXXXX" 文字列 → int parse (= default_org と同流儀)
            if (!string.IsNullOrEmpty(raw.Tape.Load))
                config.Tape.Load = ParseAddress(raw.Tape.Load);
            if (!string.IsNullOrEmpty(raw.Tape.Exec))
                config.Tape.Exec = ParseAddress(raw.Tape.Exec);
        }

        return config;
    }

    /// <summary>
    /// env file の c_bindings: YAML 配列を <see cref="CBindingDef"/> リストに変換 + validate。
    /// 失敗 case はすべて InvalidDataException で reject (silent wrong 防止)。
    /// </summary>
    private static List<CBindingDef> ParseCBindings(
        List<EnvFileCBinding> raw, string envFilePath)
    {
        var slangIdentRe = new System.Text.RegularExpressions.Regex(@"^[A-Za-z_][A-Za-z0-9_]*$");
        var seenNames = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var seenCNamesSig = new Dictionary<string, CBindingDef>(StringComparer.Ordinal);
        var result = new List<CBindingDef>();

        foreach (var entry in raw)
        {
            if (string.IsNullOrWhiteSpace(entry.Name))
                throw new InvalidDataException(
                    $"`c_bindings:` entry missing `name:` in {envFilePath}.");
            if (string.IsNullOrWhiteSpace(entry.CName))
                throw new InvalidDataException(
                    $"`c_bindings:` entry `{entry.Name}` missing `c_name:` in {envFilePath}.");

            var name = entry.Name.Trim();
            var cName = entry.CName.Trim();

            if (!slangIdentRe.IsMatch(name))
                throw new InvalidDataException(
                    $"`c_bindings:` invalid `name:` '{name}' in {envFilePath}; must match ^[A-Za-z_][A-Za-z0-9_]*$.");
            if (!slangIdentRe.IsMatch(cName))
                throw new InvalidDataException(
                    $"`c_bindings:` invalid `c_name:` '{cName}' in {envFilePath}; must match ^[A-Za-z_][A-Za-z0-9_]*$.");

            // name 重複は case-insensitive で reject (= "Spr_Set" と "spr_set" が
            // registry overwrite で silent に消えるのを防ぐ、レビュー L 反映)
            if (!seenNames.Add(name))
                throw new InvalidDataException(
                    $"`c_bindings:` duplicate `name:` '{name}' (case-insensitive) in {envFilePath}.");

            var paramTypes = new List<CBindingType>();
            if (entry.Params != null)
            {
                foreach (var t in entry.Params)
                {
                    var parsed = ParseCBindingType(t, envFilePath, $"`c_bindings:` entry `{name}` params");
                    if (parsed == CBindingType.Void)
                        throw new InvalidDataException(
                            $"`c_bindings:` entry `{name}` params cannot include `void` in {envFilePath}.");
                    paramTypes.Add(parsed);
                }
            }
            var retType = string.IsNullOrWhiteSpace(entry.Return)
                ? CBindingType.Word    // 省略 = WORD 仮定 (= 略式 CFUNC と揃える)
                : ParseCBindingType(entry.Return!, envFilePath, $"`c_bindings:` entry `{name}` return");

            var def = new CBindingDef
            {
                Name = name,
                CName = cName,
                Params = paramTypes,
                Return = retType,
            };

            // c_name 重複: signature 一致なら alias OK、不一致 (= 同じ C 関数を
            // 異なる prototype で呼び出す) は error (= 後段で C extern が衝突する)
            if (seenCNamesSig.TryGetValue(cName, out var prev))
            {
                if (!CBindingSignatureEqual(prev, def))
                    throw new InvalidDataException(
                        $"`c_bindings:` entry `{name}` aliases C function `{cName}` with a different signature than previous binding `{prev.Name}` in {envFilePath}.");
            }
            else
            {
                seenCNamesSig[cName] = def;
            }
            result.Add(def);
        }
        return result;
    }

    private static CBindingType ParseCBindingType(string token, string envFilePath, string contextMsg)
    {
        return token.Trim().ToLowerInvariant() switch
        {
            "byte"      => CBindingType.Byte,
            "word"      => CBindingType.Word,
            "float"     => CBindingType.Float,
            "byte_ptr"  => CBindingType.BytePtr,
            "word_ptr"  => CBindingType.WordPtr,
            "float_ptr" => CBindingType.FloatPtr,
            "void"      => CBindingType.Void,
            _ => throw new InvalidDataException(
                $"{contextMsg}: invalid type token '{token}' in {envFilePath}. "
                + "Allowed: byte / word / float / byte_ptr / word_ptr / float_ptr / void (return only)."),
        };
    }

    private static bool CBindingSignatureEqual(CBindingDef a, CBindingDef b)
    {
        if (a.Return != b.Return) return false;
        if (a.Params.Count != b.Params.Count) return false;
        for (int i = 0; i < a.Params.Count; i++)
            if (a.Params[i] != b.Params[i]) return false;
        return true;
    }

    private static int ParseAddress(string s)
    {
        s = s.Trim().Trim('"');
        if (s.StartsWith("$"))
            return Convert.ToInt32(s[1..], 16);
        if (s.StartsWith("0x", StringComparison.OrdinalIgnoreCase))
            return Convert.ToInt32(s[2..], 16);
        return int.Parse(s);
    }

    // YAMLデシリアライズ用の内部クラス
    private class EnvFileData
    {
        [YamlMember(Alias = "env_type")]
        public int EnvType { get; set; }

        [YamlMember(Alias = "os_type")]
        public int OsType { get; set; }

        [YamlMember(Alias = "default_org")]
        public string? DefaultOrg { get; set; }

        [YamlMember(Alias = "default_work")]
        public string? DefaultWork { get; set; }

        [YamlMember(Alias = "libraries")]
        public List<string>? Libraries { get; set; }

        [YamlMember(Alias = "code_readonly")]
        public bool CodeReadonly { get; set; }

        [YamlMember(Alias = "optimize")]
        public string? Optimize { get; set; }

        [YamlMember(Alias = "output")]
        public string? Output { get; set; }

        [YamlMember(Alias = "cmt_concat")]
        public List<string>? CmtConcat { get; set; }

        [YamlMember(Alias = "cmt_assets")]
        public List<string>? CmtAssets { get; set; }

        [YamlMember(Alias = "overlay_name")]
        public string? OverlayName { get; set; }

        [YamlMember(Alias = "overlay_output_format")]
        public string? OverlayOutputFormat { get; set; }

        [YamlMember(Alias = "defines")]
        public Dictionary<string, int>? Defines { get; set; }

        [YamlMember(Alias = "bin_pad_size")]
        public int? BinPadSize { get; set; }

        [YamlMember(Alias = "overlay_pad_align")]
        public int? OverlayPadAlign { get; set; }

        [YamlMember(Alias = "disk")]
        public EnvFileDiskData? Disk { get; set; }

        [YamlMember(Alias = "tape")]
        public EnvFileTapeData? Tape { get; set; }

        // --- C backend (oscar64) 系 ---
        [YamlMember(Alias = "backend")]
        public string? Backend { get; set; }

        [YamlMember(Alias = "oscar_path")]
        public string? OscarPath { get; set; }

        [YamlMember(Alias = "oscar_machine")]
        public string? OscarMachine { get; set; }

        [YamlMember(Alias = "oscar_format")]
        public string? OscarFormat { get; set; }

        [YamlMember(Alias = "oscar_optimize")]
        public string? OscarOptimize { get; set; }

        [YamlMember(Alias = "oscar_petscii")]
        public bool? OscarPetscii { get; set; }

        [YamlMember(Alias = "c_runtime_files")]
        public List<string>? CRuntimeFiles { get; set; }

        [YamlMember(Alias = "c_runtime_includes")]
        public List<string>? CRuntimeIncludes { get; set; }

        [YamlMember(Alias = "c_bindings")]
        public List<EnvFileCBinding>? CBindings { get; set; }
    }

    /// <summary>
    /// c_bindings: 1 entry の YAML 構造。
    ///   - name: SLANG 側名前
    ///   - c_name: C 側 ident (case preserve)
    ///   - params: 型 token のリスト (byte/word/float/byte_ptr/word_ptr/float_ptr)
    ///   - return: 戻り型 token (上記 + void)
    /// </summary>
    private class EnvFileCBinding
    {
        [YamlMember(Alias = "name")]
        public string? Name { get; set; }
        [YamlMember(Alias = "c_name")]
        public string? CName { get; set; }
        [YamlMember(Alias = "params")]
        public List<string>? Params { get; set; }
        [YamlMember(Alias = "return")]
        public string? Return { get; set; }
    }

    private class EnvFileDiskData
    {
        [YamlMember(Alias = "format")]
        public string? Format { get; set; }

        [YamlMember(Alias = "template")]
        public string? Template { get; set; }

        [YamlMember(Alias = "tool")]
        public string? Tool { get; set; }

        [YamlMember(Alias = "main_name")]
        public string? MainName { get; set; }

        [YamlMember(Alias = "overlay_name")]
        public string? OverlayName { get; set; }

        // HuDisk -r / -g 用 (string で受けて ParseAddress で int 化)
        [YamlMember(Alias = "main_load")]
        public string? MainLoad { get; set; }

        [YamlMember(Alias = "main_exec")]
        public string? MainExec { get; set; }

        [YamlMember(Alias = "overlay_load")]
        public string? OverlayLoad { get; set; }

        [YamlMember(Alias = "system_files")]
        public List<EnvFileSystemFile>? SystemFiles { get; set; }

        // mzd88 driver 用: --title 引数 (= MZ-2500 D88 image label、optional)
        [YamlMember(Alias = "title")]
        public string? Title { get; set; }

        // slfs-pack driver 用: D88 disk volume name (= 16 byte ASCII)
        [YamlMember(Alias = "volume")]
        public string? Volume { get; set; }

        // mzd88 driver 用: main 書込後に -add する追加ファイル群
        // (= 起動用 BASIC ローダ等)。env file dir 基準の相対 path を絶対化。
        [YamlMember(Alias = "extra_files")]
        public List<EnvFileExtraFile>? ExtraFiles { get; set; }
    }

    private class EnvFileExtraFile
    {
        [YamlMember(Alias = "path")]
        public string? Path { get; set; }
    }

    /// <summary>
    /// `tape:` セクションの YAML 受け側 (Phase B、 X1 .tap / .wav 出力 default 値)。
    /// load/exec は "$XXXX" / "0xXXXX" 等の文字列で受けて ParseAddress で int 化。
    /// </summary>
    private class EnvFileTapeData
    {
        [YamlMember(Alias = "name")]
        public string? Name { get; set; }

        [YamlMember(Alias = "load")]
        public string? Load { get; set; }

        [YamlMember(Alias = "exec")]
        public string? Exec { get; set; }

        [YamlMember(Alias = "wav_sample_rate")]
        public int? WavSampleRate { get; set; }

        [YamlMember(Alias = "wav_bits")]
        public int? WavBits { get; set; }
    }

    private class EnvFileSystemFile
    {
        [YamlMember(Alias = "path")]
        public string? Path { get; set; }

        [YamlMember(Alias = "flag")]
        public string? Flag { get; set; }
    }
}
