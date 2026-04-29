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
    /// AILZ80ASM の出力 format。null/未指定 = bin (= `.bin` 拡張子、追加
    /// オプションなし)。"cmt" = cassette tape image (= `.cmt` 拡張子 +
    /// AILZ80ASM に `-cmt -gap 0` を pass)。
    /// 将来 "rom" / "sna" 等の format 追加可能 (= EnvironmentLoader 側で
    /// allowlist を拡張)。
    /// </summary>
    public string? OutputFormat { get; set; }

    /// <summary>
    /// disk image 出力設定 (slangbuild --emit disk 用)。env file に
    /// `disk:` セクションが無ければ null。
    /// </summary>
    public DiskConfig? Disk { get; set; }

    /// <summary>
    /// AILZ80ASM 出力後に main bin の直後に concat する追加 .cmt path のリスト
    /// (env file dir 基準の相対 path → 絶対化済み)。pc80mk2x の XBIOS.CMT 結合用。
    /// null/empty = 結合なし。<see cref="OutputFormat"/> == "cmt" 必須
    /// (= 不一致は <c>EnvironmentLoader</c> で reject)。
    /// 結合順序: main.cmt + cmt_concat[0..] + overlay._mN.cmt (overlay 最後)
    /// </summary>
    public List<string>? CmtConcat { get; set; }
}

/// <summary>
/// `disk:` セクション (slangbuild --emit disk 用)。
/// 現状サポートは format=d88、tool=ndc / hudisk / udostool。
/// </summary>
public class DiskConfig
{
    /// <summary>"d88" のみ</summary>
    public string Format { get; set; } = "";

    /// <summary>
    /// template disk image の絶対パス。EnvironmentLoader 側で env file
    /// dir 基準の相対パスから絶対化済み。
    /// </summary>
    public string Template { get; set; } = "";

    /// <summary>"ndc" / "hudisk" / "udostool"</summary>
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

    /// <summary>
    /// udostool 専用: IPL / SUB / SYS など disk 構築の前段で書き込む系統ファイル。
    /// 他 tool では null (= 未指定)。EnvironmentLoader 側で env file dir 基準の
    /// 相対 path を絶対化済み。順序保証 = YAML の system_files リスト順。
    /// </summary>
    public List<DiskSystemFile>? SystemFiles { get; set; }
}

/// <summary>
/// udostool 経路で template に書き込む系統ファイル 1 entry
/// (= IPL / SUB / SYS のいずれか)。
/// </summary>
public class DiskSystemFile
{
    /// <summary>system file の絶対 path (= EnvironmentLoader が env file dir 基準で絶対化済)</summary>
    public string Path { get; set; } = "";

    /// <summary>udostool の flag。"-IPL" / "-SUB" / "-SYS" のいずれか (= 大文字固定)</summary>
    public string Flag { get; set; } = "";
}
