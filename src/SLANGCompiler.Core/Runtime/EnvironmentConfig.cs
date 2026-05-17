namespace SLANGCompiler.Runtime;

/// <summary>
/// コンパイラの backend 種別。env file の <c>backend:</c> field で切替。
/// 既定 = <see cref="Z80"/> (= 既存の Z80 ASM 出力経路)。
/// </summary>
public enum BackendKind
{
    /// <summary>Z80 アセンブリ出力 (= AILZ80ASM を slangbuild が呼ぶ)</summary>
    Z80 = 0,

    /// <summary>
    /// C ソース出力 (oscar64 経由で 6502 / Commodore 64 等を target にする)。
    /// slangc は <c>.c</c> ファイルまで、slangbuild が oscar64 を invoke して <c>.prg</c> 等を生成。
    /// </summary>
    OscarC = 1,
}

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
    /// この env が使う backend。既定は <see cref="BackendKind.Z80"/>。
    /// <see cref="BackendKind.OscarC"/> 指定時:
    ///   - <see cref="OutputFormat"/> は <c>"c_source"</c> 必須
    ///   - <see cref="Libraries"/> / <see cref="Disk"/> / <see cref="BinPadSize"/> /
    ///     <see cref="OverlayPadAlign"/> / <see cref="CmtConcat"/> / <see cref="CmtAssets"/> /
    ///     <see cref="OverlayName"/> / <see cref="OverlayOutputFormat"/> 指定不可 (Loader で reject)
    ///   - <see cref="CRuntimeFiles"/> 最低 1 件必須
    /// <see cref="BackendKind.Z80"/> 指定時:
    ///   - <see cref="OscarPath"/> / <see cref="OscarMachine"/> / <see cref="OscarFormat"/> /
    ///     <see cref="OscarOptimize"/> / <see cref="CRuntimeFiles"/> / <see cref="CRuntimeIncludes"/>
    ///     指定不可 (typo 早期検出のため reject)
    /// </summary>
    public BackendKind Backend { get; set; } = BackendKind.Z80;

    /// <summary>
    /// oscar64 binary の path。null / 未指定 = 環境変数 <c>$OSCAR64</c> → PATH 上の
    /// <c>oscar64</c> の順で探索 (slangbuild が解決、slangc 側は使わない)。
    /// <see cref="Backend"/> == <see cref="BackendKind.OscarC"/> でのみ有効。
    /// </summary>
    public string? OscarPath { get; set; }

    /// <summary>
    /// oscar64 の target machine (oscar64 <c>-tm=&lt;v&gt;</c> 引数)。
    /// null / 未指定 = <c>"c64"</c>。oscar64 は <c>c64</c>/<c>c128</c>/<c>vic20</c>/
    /// <c>plus4</c>/<c>pet</c>/<c>nes</c>/<c>atari</c>/<c>x16</c>/<c>mega65</c> を受ける。
    /// </summary>
    public string? OscarMachine { get; set; }

    /// <summary>
    /// oscar64 の output format (oscar64 <c>-tf=&lt;v&gt;</c> 引数)。
    /// null / 未指定 = <c>"prg"</c>。<c>prg</c>/<c>crt</c>/<c>bin</c> を受ける。
    /// </summary>
    public string? OscarFormat { get; set; }

    /// <summary>
    /// oscar64 の optimization level (例: <c>"O3"</c> / <c>"Os"</c>)。
    /// null = oscar64 既定 (<c>-O</c> 相当) に任せる。指定時は <c>-&lt;v&gt;</c> 形式で pass。
    /// </summary>
    public string? OscarOptimize { get; set; }

    /// <summary>
    /// oscar64 の <c>-psci</c> (= unprefixed string literal を PETSCII で encode) を
    /// 付与するか。既定 = true (C64 backend の前提)。
    /// </summary>
    public bool OscarPetscii { get; set; } = true;

    /// <summary>
    /// oscar64 が input として positional に受け取る C runtime source の path リスト
    /// (= env file dir 起点の相対 path → 絶対化済み)。<see cref="BackendKind.OscarC"/>
    /// で最低 1 件必須。例: <c>["c64/slang_runtime.c"]</c>。
    /// </summary>
    public List<string>? CRuntimeFiles { get; set; }

    /// <summary>
    /// oscar64 の include path (<c>-i=&lt;path&gt;</c>、複数可)。env file dir 起点の
    /// 相対 path → 絶対化済み。例: <c>["c64"]</c>。
    /// </summary>
    public List<string>? CRuntimeIncludes { get; set; }

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

    /// <summary>
    /// AILZ80ASM 出力後に output dir に copy する static asset の path リスト
    /// (env file dir 基準の相対 path → 絶対化済み)。pc80mk2xsd の XBIOS.CMT
    /// SD カード配置用 (= ユーザーは output dir 全体を SD に移すだけで揃う)。
    /// null/empty = コピーなし。<see cref="OutputFormat"/> == "cmt" 必須。
    /// <see cref="CmtConcat"/> と同 env で両方指定すると Loader で reject
    /// (= build flow が排他、結合経路と個別配置経路の使い分け)。
    /// コピー先 file 名は asset path の basename。
    /// </summary>
    public List<string>? CmtAssets { get; set; }

    /// <summary>
    /// overlay 出力 file 名 template (例: <c>"M{index}.BIN"</c>)。
    /// `{index}` placeholder を 0..N に展開して overlay の最終 path を決定。
    /// null = template なし (= 既存挙動 <c>&lt;prefix&gt;._m{index}.{overlayBinExt}</c>)。
    /// pc80mk2xsd で旧慣例の <c>M0.BIN</c> 命名に揃えるため。
    /// <see cref="OutputFormat"/> == "cmt" 必須、`{index}` 必須、output dir 外
    /// 書き禁止 (= absolute path / separator / `..` を Loader で validate)。
    /// </summary>
    public string? OverlayName { get; set; }

    /// <summary>
    /// overlay の AILZ80ASM 出力 format (<c>"bin"</c> / <c>"cmt"</c> / null)。
    /// null = main の <see cref="OutputFormat"/> に追従 (= 既存挙動互換)。
    /// pc80mk2xsd では <c>"bin"</c> 指定 (= main は CMT 形式 header 込みだが
    /// overlay は raw binary、SD カードから SD_RREAD で読むため header 不要)。
    /// <see cref="OutputFormat"/> == "cmt" 必須。
    /// </summary>
    public string? OverlayOutputFormat { get; set; }

    /// <summary>
    /// env が自動的に define する名前→値 map (= integer 値のみ)。
    /// slangc 側では <c>Preprocessor.DefineConst()</c> 経由で SL の
    /// <c>#IF NAME==VAL</c> 判定に参照される。
    /// slangbuild 側では AILZ80ASM 起動時に <c>-dl NAME=VAL</c> 引数として
    /// 全 assemble 呼出 (main / overlay / prelink Pass 1/3) に pass される
    /// (= ASM 側の <c>#IF exists NAME</c> も活きる)。
    /// 例: pc80mk2xsd で <c>PC8001_SD: 1</c> を定義することで、ユーザーが
    /// SL に <c>CONST ASM PC8001_SD = 1;</c> を書かなくても SD 経路が
    /// 自動的に有効化される。
    /// 名前は <c>^[A-Za-z_][A-Za-z0-9_]*$</c> regex で validate。
    /// </summary>
    public Dictionary<string, int>? Defines { get; set; }

    /// <summary>
    /// main bin の **固定サイズ** (byte 単位)。AILZ80ASM 出力後に末尾を 0
    /// で埋めて指定サイズに揃える。null / 0 / 負 = padding なし (既存挙動)。
    /// VGS-Zero (vgs0) で 16384 byte 固定 ROM 出力に使う。
    /// <see cref="OutputFormat"/> == "cmt" 環境では指定不可 (Loader で reject)。
    /// 既存 bin が指定サイズを超えていた場合 Driver が error で終了
    /// (= silent truncation 防止)。
    /// </summary>
    public int? BinPadSize { get; set; }

    /// <summary>
    /// overlay (`#MODULE`) bin の **alignment** (byte 単位)。各 overlay の
    /// サイズを指定値の倍数に切り上げて末尾を 0 で埋める。null / 0 / 負 =
    /// padding なし (既存挙動)。VGS-Zero の 8KB bank switching に対応する
    /// ため <c>8192</c> 指定で各 overlay を 8KB 単位に揃える。
    /// <see cref="OutputFormat"/> == "cmt" 環境では指定不可 (Loader で reject)。
    /// 上限なし (= overlay サイズに応じて切り上げ、empty overlay は no-op)。
    /// </summary>
    public int? OverlayPadAlign { get; set; }
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

    /// <summary>
    /// mzd88 専用: <c>--title</c> 引数 (= MZ-2500 D88 image label、optional)。
    /// 他 tool では null/空。
    /// </summary>
    public string? Title { get; set; }

    /// <summary>
    /// mzd88 専用: main 書込後に <c>-add</c> で追加格納するファイル群
    /// (= MZ-2500 起動用 BASIC ローダ等、load/exec 指定なしで disk に格納)。
    /// EnvironmentLoader 側で env file dir 基準の相対 path を絶対化済み。
    /// 順序保証 = YAML リスト順。他 tool では null。
    /// </summary>
    public List<string>? ExtraFiles { get; set; }
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
