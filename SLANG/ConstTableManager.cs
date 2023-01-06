using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using YamlDotNet.RepresentationModel;
using YamlDotNet.Serialization;
using YamlDotNet.Serialization.NamingConventions;

namespace SLANGCompiler.SLANG
{

    /// <summary>
    /// Const値の型
    /// </summary>
    public enum ConstInfoType
    {
        /// <summary>数値型</summary>
        IntValue,
        /// <summary>浮動小数型</summary>
        FloatValue,
        /// <summary>CODEアドレス参照型</summary>
        Code,
        String,
    }

    /// <summary>
    /// Const値の情報を持つクラス
    /// </summary>
    public class ConstInfo
    {
        /// <summary>CONST値の型</summary>
        public ConstInfoType ConstInfoType { get; set; }
        /// <summary>CONST値(値)</summary>
        public int Value { get; set; }
        public float FloatValue { get; set; }
        /// <summary>CONST値(CODEを参照する場合のシンボル名)</summary>
        public string SymbolString { get; set; }

        public bool CaseSensitive { get; set; }

        /// <summary>値を持つCONST値のコンストラクタ</summary>
        public ConstInfo(int value)
        {
            this.ConstInfoType = ConstInfoType.IntValue;
            this.FloatValue = this.Value = value;
            this.SymbolString = null;
        }

        /// <summary>FLOAT値を持つCONST値のコンストラクタ</summary>
        public ConstInfo(float value)
        {
            this.ConstInfoType = ConstInfoType.FloatValue;
            this.FloatValue = value;
            this.Value = (int)value;
            this.SymbolString = null;
        }

        /// <summary>CODEシンボル名または文字列を持つCONST値のコンストラクタ</summary>
        public ConstInfo(string symbolStr, bool isCode)
        {
            this.ConstInfoType = isCode ? ConstInfoType.Code : ConstInfoType.String;
            this.Value = 0;
            this.SymbolString = symbolStr;
        }

        /// <summary>CONST値の複製</summary>
        public ConstInfo Clone()
        {
            if(ConstInfoType == ConstInfoType.IntValue)
            {
                return new ConstInfo(Value);
            } else if(ConstInfoType == ConstInfoType.FloatValue)
            {
                return new ConstInfo(FloatValue);
            } else {
                // code or string
                return new ConstInfo(SymbolString, ConstInfoType == ConstInfoType.Code);
            }
        }

        /// <summary>このConstInfoのCONST情報を文字列で返す</summary>
        public string GetConstStr(SymbolTableManager symbolManager)
        {
            if(ConstInfoType == ConstInfoType.Code)
            {
                var constSymbol = symbolManager.SearchSymbol(SymbolString);
                if(constSymbol == null)
                {
                    return null;
                }
                if(constSymbol.IsRuntime)
                {
                    return constSymbol.RuntimeName;
                } else {
                    return constSymbol.LabelName;
                }
            } else if(ConstInfoType == ConstInfoType.String)
            {
                return SymbolString;
            } else {
                if(Value > 255)
                {
                    return $"${Value:X4}";
                } else {
                    return $"${Value:X2}";
                }
            }
        }
    }
    /// <summary>
    /// CONST定義されたシンボルとその値を管理するマネージャ
    /// </summary>
    public class ConstTableManager
    {

        private Dictionary<string, ConstInfo> constTableDictionary = new Dictionary<string, ConstInfo>();

        public bool CaseSensitive { get; set; }

        private ConstInfo zeroConst = new ConstInfo(0);
        public ConstInfo ZeroConst => zeroConst;

        /// <summary>
        /// CONST定義されたシンボルと値を追加する
        /// </summary>
        public void Add(string name, int value)
        {
            constTableDictionary[name] = new ConstInfo(value);
        }

        /// <summary>
        /// CONST定義されたシンボルと値(float)を追加する
        /// </summary>
        public void Add(string name, float value)
        {
            constTableDictionary[name] = new ConstInfo(value);
        }

        /// <summary>
        /// CONST定義されたシンボルと値を追加する
        /// </summary>
        public void AddCode(string name, string codeName)
        {
            constTableDictionary[name] = new ConstInfo(codeName, true);
        }

        /// <summary>
        /// CONST定義された文字列を追加する
        /// </summary>
        public void AddString(string name, string str)
        {
            constTableDictionary[name] = new ConstInfo(str, false);
        }

        /// <summary>
        /// CONST定義された値が存在すればvalueに返し、戻り値がtrueになる。存在しない場合はfalseになる。
        /// </summary>
        public bool TryGetValue(string name, out ConstInfo info)
        {
            if(CaseSensitive)
            {
                if(constTableDictionary.TryGetValue(name, out info))
                {
                    return true;
                }
            } else {
                foreach(var pair in constTableDictionary)
                {
                    if(pair.Key.ToUpper() == name.ToUpper())
                    {
                        info = pair.Value;
                        return true;
                    }
                }
            }
            info = null;
            return false;
        }

        /// <summary>
        /// CONST定義された情報を表示する(デバッグ用)
        /// </summary>
        public void DebugDisp()
        {
            Console.WriteLine("■CONST");
            foreach(var pair in constTableDictionary)
            {
                Console.WriteLine($"{pair.Key} : {pair.Value.ConstInfoType} : {pair.Value.Value} : {pair.Value.SymbolString}");
            }
        }
    }
}
