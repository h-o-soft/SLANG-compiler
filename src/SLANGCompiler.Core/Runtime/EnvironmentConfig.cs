namespace SLANGCompiler.Runtime;

/// <summary>
/// ターゲット環境の設定
/// </summary>
public class EnvironmentConfig
{
    public string Name { get; set; } = "";
    public int EnvType { get; set; }
    public int OsType { get; set; }
    public int DefaultOrg { get; set; }
    public int DefaultWork { get; set; }
    public List<string> Libraries { get; set; } = new();
    public string? OptimizeRules { get; set; }
}
