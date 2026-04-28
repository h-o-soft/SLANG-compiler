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

        // disk セクション (slangbuild --emit disk 用)
        if (raw.Disk != null)
        {
            // template path は env file dir 基準の相対パスを絶対化して保存
            // (= caller 側で再計算しなくて良いように)
            var envDir = Path.GetDirectoryName(Path.GetFullPath(envFilePath))!;
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
    }
}
