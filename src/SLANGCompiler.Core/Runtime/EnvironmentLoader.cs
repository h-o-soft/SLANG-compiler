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

        // output: AILZ80ASM 出力 format (default = bin、"cmt" で CMT 出力)。
        // 空白/null は null、"bin" も null に正規化 (= 内部表現統一)、
        // それ以外は明示エラーで typo を早期検出。
        var rawOutput = raw.Output?.Trim();
        if (string.IsNullOrEmpty(rawOutput))
        {
            config.OutputFormat = null;
        }
        else
        {
            var normalized = rawOutput.ToLowerInvariant();
            if (normalized != "bin" && normalized != "cmt")
            {
                throw new InvalidDataException(
                    $"Invalid `output:` value '{rawOutput}' in {envFilePath}. "
                    + "Allowed: bin (default) / cmt.");
            }
            config.OutputFormat = (normalized == "bin") ? null : normalized;
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
        }

        return config;
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

        [YamlMember(Alias = "disk")]
        public EnvFileDiskData? Disk { get; set; }
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
    }

    private class EnvFileSystemFile
    {
        [YamlMember(Alias = "path")]
        public string? Path { get; set; }

        [YamlMember(Alias = "flag")]
        public string? Flag { get; set; }
    }
}
