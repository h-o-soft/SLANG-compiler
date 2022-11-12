using System;
using System.Collections.Generic;
using System.IO;

namespace SLANGCompiler.SLANG
{
    /// <summary>
    /// アセンブラコードを蓄積し、最終的なアセンブラコードを出力するためのクラス
    /// </summary>
    public class CodeRepository
    {
        private List<Code> codeList;
        public List<string> CodeList { get; private set; }

        private int lineNumber;
        private Dictionary<int, int> labelToLineDictionary;

        IErrorReporter errorReporter;

        public CodeRepository(IErrorReporter errorReporter)
        {
            this.errorReporter = errorReporter;
            this.CodeList = new List<string>();
            Initialize();
        }

        /// <summary>
        /// 初期化を行いコード蓄積の準備を行う
        /// </summary>
        public void Initialize()
        {
            codeList = new List<Code>();
            lineNumber = 1;
            labelToLineDictionary = new Dictionary<int, int>();
        }

        /// <summary>
        /// 文字列によるアセンブラコードを追加する
        /// </summary>
        public void AddCode(string codeString)
        {
            var code = new Code(
                CodeType.String,
                lineNumber,
                codeString,
                0,
                ConditionalCode.None);
            codeList.Add(code);
            if(codeString.Contains('\n'))
            {
                lineNumber++;
            }
        }

        /// <summary>
        /// ジャンプ命令を追加する
        /// </summary>
        public void AddJump(int labelNumber, ConditionalCode conditional = ConditionalCode.None, bool isNear = false)
        {
            var code = new Code(
                isNear ? CodeType.JumpNear : CodeType.Jump,
                lineNumber,
                null,
                labelNumber,
                conditional);
            codeList.Add(code);
            lineNumber++;
        }

        /// <summary>
        /// ラベル追加する
        /// </summary>
        public void AddLabel(int labelNumber)
        {
            labelToLineDictionary[labelNumber] = lineNumber;
            var code = new Code(
                CodeType.Label,
                lineNumber,
                null,
                labelNumber,
                ConditionalCode.None);
            codeList.Add(code);
        }

        /// <summary>
        /// 指定番号のラベルがコードに存在しているかを確認
        /// </summary>
        public bool IsLabelExists(int labelNumber)
        {
            return labelToLineDictionary.ContainsKey(labelNumber);
        }

        /// <summary>
        /// ラベルアドレスを追加する
        /// </summary>
        public void AddLabelAddress(int labelNumber)
        {
            var code = new Code(
                CodeType.LabelAddress,
                lineNumber,
                null,
                labelNumber,
                ConditionalCode.None);
            codeList.Add(code);
        }

        // ラベル番号をラベル文字列に変換する
        public string GetLabelString(int labelNum)
        {
            return "__L" + labelNum;
        }

        // ラベルが存在する行番号を返す。複数ラベルが同一行に並ぶ場合は同じ値を返す。
        protected int getLabelTargetLine(int labelNum)
        {
            if(!labelToLineDictionary.ContainsKey(labelNum))
            {
                errorReporter.Error($"could not found label : {labelNum}");
                return -1;
            }
            return labelToLineDictionary[labelNum];
        }

        /// <summary>
        /// ジャンプのコード文字列を得る
        /// </summary>
        public string GetJumpString(Code code)
        {
            string condStr;
            string jumpCode = code.CodeType == CodeType.Jump ? "JP" : "JR";
            if(code.ConditionalCode != ConditionalCode.None)
            {
                condStr = code.ConditionalCode.GetCode() + ",";
            } else {
                condStr = "";
            }
            int targetLine = getLabelTargetLine(code.LabelNumber);
            return $" {jumpCode} " + condStr + GetLabelString(targetLine);
        }

        bool connectNext = false;
        private void WriteCode(string codeStr)
        {
            // 末尾に改行が無い場合は前の行とつなげる
            if(!codeStr.EndsWith('\n'))
            {
                connectNext = true;
            }
            var codes = codeStr.TrimEnd('\n').Split("\n");
            foreach(var code in codes)
            {
                if(connectNext)
                {
                    CodeList[CodeList.Count - 1] += code;
                    connectNext = false;
                    continue;
                }
                CodeList.Add(code);
            }
        }

        /// <summary>
        /// コードリポジトリに蓄積されたアセンブラコードを出力する。
        /// </summary>
        public List<string> GenerateCodeList(int orgValue, int offsetValue)
        {
            int prevLabel = -1;

            CodeList.Clear();

           WriteCode($"\n\tORG\t${orgValue:X}\n");

           if(offsetValue >= 0)
           {
            // OFFSETは未対応
            WriteCode($";\tOFFSET\t${offsetValue:X}\n");
           }

            string condStr = "";
            foreach(var code in codeList)
            {
                switch(code.CodeType)
                {
                    case CodeType.String:
                        WriteCode(code.CodeString);
                        break;
                    case CodeType.LabelAddress:
                        {
                            int targetLine = getLabelTargetLine(code.LabelNumber);
                            var labelName = GetLabelString(targetLine);
                            WriteCode($" DW {labelName}\n");
                            break;
                        }
                    case CodeType.Jump:
                    case CodeType.JumpNear:
                        {
                            int targetLine = getLabelTargetLine(code.LabelNumber);
                            if(targetLine == code.LineNumber + 1)
                            {
                                // 次の行へのジャンプで無意味なため削除
                            } else {
                                var jumpString = GetJumpString(code);
                                WriteCode(jumpString + "\n");
                            }
                            break;
                        }
                    case CodeType.Label:
                        if(prevLabel != code.LineNumber)
                        {
                            WriteCode(GetLabelString(code.LineNumber) + ":\n");
                            prevLabel = code.LineNumber;
                        }
                        break;
                }
            }
            return CodeList;
        }
    }
}
