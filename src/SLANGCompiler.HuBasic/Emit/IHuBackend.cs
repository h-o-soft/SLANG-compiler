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
}

public sealed record HuEmitOptions(string SourceName = "", bool LineComments = false);
