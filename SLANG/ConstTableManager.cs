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
        Value,
        /// <summary>CODEアドレス参照型</summary>
        Code,
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
        /// <summary>CONST値(CODEを参照する場合のシンボル名)</summary>
        public string SymbolString { get; set; }

        /// <summary>値を持つCONST値のコンストラクタ</summary>
        public ConstInfo(int value)
        {
            this.ConstInfoType = ConstInfoType.Value;
            this.Value = value;
            this.SymbolString = null;
        }

        /// <summary>CODEシンボル名を持つCONST値のコンストラクタ</summary>
        public ConstInfo(string symbolStr)
        {
            this.ConstInfoType = ConstInfoType.Code;
            this.Value = 0;
            this.SymbolString = symbolStr;
        }

        /// <summary>CONST値の複製</summary>
        public ConstInfo Clone()
        {
            if(ConstInfoType == ConstInfoType.Value)
            {
                return new ConstInfo(Value);
            } else {
                return new ConstInfo(SymbolString);
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
                return constSymbol.LabelName;
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

        /// <summary>
        /// CONST定義されたシンボルと値を追加する
        /// </summary>
        public void Add(string name, int value)
        {
            constTableDictionary[name] = new ConstInfo(value);
        }

        /// <summary>
        /// CONST定義されたシンボルと値を追加する
        /// </summary>
        public void AddCode(string name, string codeName)
        {
            constTableDictionary[name] = new ConstInfo(codeName);
        }

        /// <summary>
        /// CONST定義された値が存在すればvalueに返し、戻り値がtrueになる。存在しない場合はfalseになる。
        /// </summary>
        public bool TryGetValue(string name, out ConstInfo info)
        {
            if(constTableDictionary.TryGetValue(name, out info))
            {
                return true;
            }
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
