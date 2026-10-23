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
    string SourceName = "");

public sealed record HuTranspileResult(string? Output, HuDiagnosticBag Diagnostics, HuProgram? Program, HuFlat? Flat);

/// <summary>前段 (字句・構文・解析) → 共通の展開 → 後段 (SLANG) をつなぐ入口。</summary>
public static class HuTranspiler
{
    public static HuTranspileResult Transpile(string text, HuTranspileOptions? options = null)
    {
        var diag = new HuDiagnosticBag();
        return Run(HuSource.FromText(text, diag), diag, options ?? new HuTranspileOptions());
    }

    public static HuTranspileResult TranspileFile(string path, string encoding, HuTranspileOptions options)
    {
        var diag = new HuDiagnosticBag();
        return Run(HuSource.FromFile(path, encoding, diag), diag, options);
    }

    /// <summary>前段だけ (解析済みのプログラムまで)。テストと別の後段から使う。</summary>
    public static HuProgram? Analyze(string text, HuDiagnosticBag diag, int dataBudget = HuAnalyzer.DefaultDataBudget)
    {
        var src = HuSource.FromText(text, diag);
        var ast = HuParser.Parse(src, diag);
        if (diag.HasErrors) return null;
        var prog = HuAnalyzer.Analyze(ast, diag, dataBudget);
        return diag.HasErrors ? null : prog;
    }

    private static HuTranspileResult Run(HuSource src, HuDiagnosticBag diag, HuTranspileOptions options)
    {
        var ast = HuParser.Parse(src, diag);
        if (diag.HasErrors) return new HuTranspileResult(null, diag, null, null);
        var prog = HuAnalyzer.Analyze(ast, diag, options.DataBudget);
        if (diag.HasErrors) return new HuTranspileResult(null, diag, prog, null);
        var flat = HuLowerer.Lower(prog, new HuLowerOptions(options.Wait, options.WaitArg, options.LineComments));
        IHuBackend backend = new SlangBackend();
        var output = backend.Emit(prog, flat, new HuEmitOptions(options.SourceName, options.LineComments));
        // 後段が足した静的な領域 (浮動小数の定数の表と一時変数) も、静的なデータの上限に数える
        long total = (long)prog.StaticDataBytes + backend.ExtraStaticBytes;
        if (total > options.DataBudget)
        {
            diag.Error("HB0309",
                $"静的なデータの合計 {total} バイトが上限 {options.DataBudget} バイトを超えています " +
                $"(そのうち浮動小数の定数と一時変数 {backend.ExtraStaticBytes} バイト)", HuPos.None);
            return new HuTranspileResult(null, diag, prog, flat);
        }
        return new HuTranspileResult(output, diag, prog, flat);
    }
}
