
namespace SLANGCompiler.SLANG
{
    /// <summary>
    /// ラベルの生成を行うインターフェイス
    /// </summary>
    public interface ILabelCreator
    {
        /// <summary>
        /// 新規にラベル(番号)を作成する
        /// </summary>
        public int CreateLabel();

        /// <summary>
        /// ラベル番号を元にラベル定義を生成する
        /// </summary>
        public void GenerateLabel(int labelNum);
    }
}
