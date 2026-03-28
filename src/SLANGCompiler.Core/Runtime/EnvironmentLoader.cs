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

        [YamlMember(Alias = "optimize")]
        public string? Optimize { get; set; }
    }
}
