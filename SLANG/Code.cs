namespace SLANGCompiler.SLANG
{
    /// <summary>
    /// アセンブラコードの種別
    /// </summary>
    public enum CodeType
    {
        /// <summary>通常の文字列によるコード</summary>
        String,
        /// <summary>ラベル</summary>
        Label,
        /// <summary>ジャンプ(JP)</summary>
        Jump,
        /// <summary>ジャンプ(JR)</summary>
        JumpNear,
        /// <summary>ラベルのアドレス値(DW _LABEL 的なコード)</summary>
        LabelAddress,
    }

    /// <summary>
    /// フラグ状態を示すenum値
    /// </summary>
    public enum ConditionalCode
    {
        None,

        Zero,
        NonZero,
        NonCarry,
        Carry,
    }

    public static partial class ConditionalCodeExtend {
            private static readonly string[] condStrings = new string[]
            {
                null,

                "Z",
                "NZ",
                "NC",
                "C",
            };
            /// <summary>
            /// フラグ状態を示すenum値をフラグ値文字列として返す
            /// </summary>
            public static string GetCode(this ConditionalCode param)
            {
                return condStrings[(int)param];
            }
    }

    /// <summary>
    /// アセンブラコードクラス
    /// </summary>
    public class Code
    {
        /// <summary>コードの種類</summary>
        public CodeType CodeType { get; private set; }
        /// <summary>このコードの行番号</summary>
        public int LineNumber { get; private set; }
        /// <summary>コード文字列</summary>
        public string CodeString { get; private set; }
        /// <summary>条件つきジャンプのための条件判断情報</summary>
        public ConditionalCode ConditionalCode { get; private set; }
        /// <summary>ラベル番号</summary>
        public int LabelNumber { get; private set; }

        public Code(CodeType codeType, int lineNumber, string codeString, int labelNumber, ConditionalCode conditionalCode)
        {
            this.CodeType = codeType;
            this.LineNumber = lineNumber;
            this.CodeString = codeString;
            this.LabelNumber = labelNumber;
            this.ConditionalCode = conditionalCode;
        }
    }
}
