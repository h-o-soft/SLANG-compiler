namespace SLANGCompiler.SLANG
{
    /// <summary>
    /// コンパイラのエラーレポートを行うインターフェイス
    /// </summary>
    public interface IErrorReporter
    {
        /// <summary>
        /// エラーを発生させる。エラー出力にソースファイル名と行番号を追加した状態で文字列が表示される。
        /// </summary>
        public void Error(string error, bool noDispLine = false);
    }
}
