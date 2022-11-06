using System.Collections.Generic;

namespace SLANGCompiler.SLANG
{
    /// <summary>
    /// (関数内で定義する)ラベル管理クラス
    /// </summary>
    public class LabelManager
    {
        private ILabelCreator labelCreator;
        private IErrorReporter errorReporter;

        private Dictionary<string, int> labelDictionary;
        private Dictionary<string, bool> labelIsGenerated;


        /// <summary>
        /// コンストラクタ。ラベル生成インターフェイスとエラー出力インターフェイスを渡す。
        /// </summary>
        public LabelManager(ILabelCreator labelCreator, IErrorReporter errorReporter)
        {
            this.errorReporter = errorReporter;
            this.labelCreator = labelCreator;
            labelDictionary = new Dictionary<string, int>();
            labelIsGenerated = new Dictionary<string, bool>();
        }

        /// <summary>
        /// 未定義ラベルがあるかどうかを調査する。ある場合はエラーを出力する。
        /// </summary>
        public bool CheckNotDefinedLabel()
        {
            bool hasError = false;
            System.Text.StringBuilder errorSb = null;
            foreach(var label in labelDictionary.Keys)
            {
                bool generated;
                if(!labelIsGenerated.TryGetValue(label, out generated))
                {
                    generated = false;
                }
                if(!generated)
                {
                    if(errorSb == null)
                    {
                        errorSb = new System.Text.StringBuilder();
                        errorSb.Append("not defined label(s) : ");
                    } else {
                        errorSb.Append(",");
                    }
                    errorSb.Append(label);
                    hasError = true;
                }
            }
            if(hasError)
            {
                errorReporter.Error(errorSb.ToString());
            }
            return hasError;
        }

        /// <summary>
        /// GOTOなどでラベルを利用する時に呼ぶ。定義されていない場合はこの場で仮定義する。
        /// </summary>
        public int ReferenceLabel(string name)
        {
            int labelNum;
            if(labelDictionary.TryGetValue(name, out labelNum))
            {
                // already defined
                return labelNum;
            }
            labelNum = labelCreator.CreateLabel();
            labelDictionary.Add(name, labelNum);
            return labelNum;
        }

        /// <summary>
        /// 指定した名前のラベルが存在するかチェックする
        /// </summary>
        public bool IsExists(string name)
        {
            return labelDictionary.ContainsKey(name);
        }

        /// <summary>
        /// ラベルを定義する。これを呼ぶ事でラベルの位置が確定する。
        /// </summary>
        public int DefineLabel(string name)
        {
            // 存在しない場合はこのタイミングで作る
            if(!labelDictionary.ContainsKey(name))
            {
                ReferenceLabel(name);
            }
            labelIsGenerated[name] = true;
            return labelDictionary[name];
        }

        /// <summary>
        /// ラベル定義情報をクリアする。関数の頭で呼び出す事。
        /// </summary>
        public void Clear()
        {
            labelDictionary.Clear();
        }
    }
}
