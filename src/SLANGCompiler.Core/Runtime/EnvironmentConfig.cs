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
    public bool CodeReadonly { get; set; }

    /// <summary>
    /// disk image 出力設定 (slangbuild --emit disk 用)。env file に
    /// `disk:` セクションが無ければ null。
    /// </summary>
    public DiskConfig? Disk { get; set; }
}

/// <summary>
/// `disk:` セクション (slangbuild --emit disk 用)。
/// Phase 1 では format=d88 + tool=ndc のみサポート。
/// </summary>
public class DiskConfig
{
    /// <summary>"d88" 等。Phase 1 は "d88" のみ</summary>
    public string Format { get; set; } = "";

    /// <summary>
    /// template disk image の絶対パス。EnvironmentLoader 側で env file
    /// dir 基準の相対パスから絶対化済み。
    /// </summary>
    public string Template { get; set; } = "";

    /// <summary>"ndc" 等。Phase 1 は "ndc" のみ</summary>
    public string Tool { get; set; } = "";

    /// <summary>main bin の disk 内ファイル名 (例: "PROG.COM")</summary>
    public string MainName { get; set; } = "";

    /// <summary>
    /// overlay 名テンプレート (例: "M{index}.BIN")。
    /// `{index}` placeholder を 0..N に展開して使う。
    /// </summary>
    public string OverlayName { get; set; } = "";

    /// <summary>HuDisk の <c>-r &lt;load&gt;</c> 引数。null なら付けない (= ndc 等の
    /// load address 概念無しツール)。</summary>
    public int? MainLoad { get; set; }

    /// <summary>HuDisk の <c>-g &lt;exec&gt;</c> 引数。null なら付けない。</summary>
    public int? MainExec { get; set; }

    /// <summary>overlay の load address。null なら -r を付けない。
    /// (overlay には exec を付けない sos の慣習)</summary>
    public int? OverlayLoad { get; set; }
}
