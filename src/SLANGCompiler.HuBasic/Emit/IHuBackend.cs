using SLANGCompiler.HuBasic.Analysis;
using SLANGCompiler.HuBasic.Lowering;

namespace SLANGCompiler.HuBasic.Emit;

/// <summary>
/// 後段。前段の解析結果 (<see cref="HuProgram"/>) と、共通の展開 (<see cref="HuFlat"/>) から出力を作る。
/// 今は SLANG のソースを出す後段だけがある。
/// </summary>
public interface IHuBackend
{
    string Emit(HuProgram program, HuFlat flat, HuEmitOptions options);

    /// <summary>
    /// 直前の Emit で、後段が自分で足した静的な領域のバイト数 (前段の <see cref="HuProgram.StaticDataBytes"/> に
    /// 含まれないもの。SLANG の後段では浮動小数の定数の表と一時変数)。静的なデータの上限の検査に足す。
    /// </summary>
    int ExtraStaticBytes { get; }
}

public sealed record HuEmitOptions(string SourceName = "", bool LineComments = false);
