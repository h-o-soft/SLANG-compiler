using SLANGCompiler.HuBasic.Analysis;
using SLANGCompiler.HuBasic.Emit;
using SLANGCompiler.HuBasic.Emit.Slang;
using SLANGCompiler.HuBasic.Lowering;
using SLANGCompiler.HuBasic.Parsing;
using SLANGCompiler.HuBasic.Text;

namespace SLANGCompiler.HuBasic;

public sealed record HuTranspileOptions(
    HuWaitMode Wait = HuWaitMode.None,
    int WaitArg = 1,
    bool LineComments = false,
    int DataBudget = HuAnalyzer.DefaultDataBudget,
    string SourceName = "",
    bool AssumeDefint = false,
    bool LineNumbers = true,
    int StringLen = HuAnalyzer.MaxStringLen,
    string? Env = null);

/// <param name="RequiredEnv">組み立てに要る環境: ファイルを使うなら x1native_hufs (使わなければ null。どの環境でもよい)</param>
public sealed record HuTranspileResult(string? Output, HuDiagnosticBag Diagnostics, HuProgram? Program, HuFlat? Flat)
{
    public string? RequiredEnv { get; init; }
}

/// <summary>前段 (字句・構文・解析) → 共通の展開 → 後段 (SLANG) をつなぐ入口。</summary>
public static class HuTranspiler
{
    public static HuTranspileResult Transpile(string text, HuTranspileOptions? options = null)
    {
        var diag = new HuDiagnosticBag();
        return Run(HuSource.FromText(text, diag), diag, options ?? new HuTranspileOptions());
    }

    public static HuTranspileResult TranspileFile(string path, string encoding, HuTranspileOptions options)
        => TranspileBytes(File.ReadAllBytes(path), encoding, options);

    /// <summary>ファイルの中身のバイト列から変換する (ブラウザから呼ぶ口)。encoding は <see cref="TranspileFile"/> と同じ</summary>
    public static HuTranspileResult TranspileBytes(byte[] bytes, string encoding, HuTranspileOptions options)
    {
        var diag = new HuDiagnosticBag();
        return Run(HuSource.FromBytes(bytes, encoding, diag), diag, options);
    }

    /// <summary>前段だけ (解析済みのプログラムまで)。テストと別の後段から使う。</summary>
    public static HuProgram? Analyze(string text, HuDiagnosticBag diag, int dataBudget = HuAnalyzer.DefaultDataBudget,
                                     bool assumeDefint = false)
    {
        var src = HuSource.FromText(text, diag);
        var ast = HuParser.Parse(src, diag);
        if (diag.HasErrors) return null;
        var prog = HuAnalyzer.Analyze(ast, diag, dataBudget, assumeDefint);
        return diag.HasErrors ? null : prog;
    }

    private static HuTranspileResult Run(HuSource src, HuDiagnosticBag diag, HuTranspileOptions options)
    {
        var ast = HuParser.Parse(src, diag);
        if (diag.HasErrors) return new HuTranspileResult(null, diag, null, null);
        var prog = HuAnalyzer.Analyze(ast, diag, options.DataBudget, options.AssumeDefint, options.StringLen);
        if (diag.HasErrors) return new HuTranspileResult(null, diag, prog, null);
        // ファイルの命令は x1native_hufs (Hu-BASIC と同じ形のディスク) だけ。PRINT#0 は数えない
        var required = prog.UsesFiles ? HufsEnv : null;
        if (prog.UsesFiles && options.Env is not (null or HufsEnv))
        {
            diag.Error("HB0315",
                $"ファイルの命令 (OPEN・PRINT#・INPUT# など) は {HufsEnv} の環境だけで使えます (いまは {options.Env})。" +
                $"--emit disk なら --env を書かないか --env {HufsEnv} にしてください (テープでは使えません)", prog.FirstFilePos);
            return new HuTranspileResult(null, diag, prog, null) { RequiredEnv = required };
        }
        bool hufs = prog.UsesFiles || options.Env == HufsEnv;
        var flat = HuLowerer.Lower(prog, new HuLowerOptions(options.Wait, options.WaitArg, options.LineComments, options.LineNumbers, hufs));
        IHuBackend backend = new SlangBackend();
        var output = backend.Emit(prog, flat,
            new HuEmitOptions(options.SourceName, options.LineComments, DiskMainName(options.SourceName, diag), hufs));
        // 後段が足した静的な領域 (浮動小数の定数の表と一時変数) も、静的なデータの上限に数える
        long total = (long)prog.StaticDataBytes + backend.ExtraStaticBytes;
        if (total > options.DataBudget)
        {
            diag.Error("HB0309",
                $"静的なデータの合計 {total} バイトが上限 {options.DataBudget} バイトを超えています " +
                $"(そのうち浮動小数の定数と一時変数 {backend.ExtraStaticBytes} バイト)" +
                HuAnalyzer.StringLenHint(total, options.DataBudget, prog.StringSlots, prog.StringLen, afterBackend: true), HuPos.None);
            return new HuTranspileResult(null, diag, prog, flat) { RequiredEnv = required };
        }
        return new HuTranspileResult(output, diag, prog, flat) { RequiredEnv = required };
    }

    /// <summary>Hu-BASIC と同じ形のディスクの環境 (ファイルの命令を使うプログラムの組み立て先)</summary>
    public const string HufsEnv = "x1native_hufs";

    /// <summary>
    /// ディスクイメージ (x1native_slfs) の IPL の起動名にする名前: 入力のファイル名から拡張子と前後の空白を除いたもの (大文字小文字はそのまま)。
    /// slangbuild の決まり (ASCII の 0x20〜0x7E、1〜13 文字) に合わせ、13 文字を超えたら切り、ASCII 以外の字があれば使わない
    /// (どちらも warning HB0112)。使わないときは空 (slangbuild は env の名前、なければ SLFSMAIN を使う)。
    /// </summary>
    internal static string DiskMainName(string sourceName, HuDiagnosticBag diag)
    {
        var name = Path.GetFileNameWithoutExtension(sourceName).Trim();
        if (name.Length == 0) return "";
        if (name.Any(ch => ch < 0x20 || ch > 0x7E))
        {
            diag.Warning("HB0112", $"ファイル名 \"{name}\" に ASCII 以外の字があるので、ディスクイメージの IPL の起動名にしません " +
                                   "(env の名前、なければ SLFSMAIN。slangbuild の --disk-main-name で指定できます)", HuPos.None);
            return "";
        }
        if (name.Length > 13)
        {
            var cut = name[..13].TrimEnd();
            diag.Warning("HB0112", $"ファイル名 \"{name}\" は 13 文字を超えるので、ディスクイメージの IPL の起動名は \"{cut}\" にします " +
                                   "(slangbuild の --disk-main-name で変えられます)", HuPos.None);
            name = cut;
        }
        return name;
    }
}
