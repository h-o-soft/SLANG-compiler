using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using YamlDotNet.RepresentationModel;
using YamlDotNet.Serialization;
using YamlDotNet.Serialization.NamingConventions;

namespace SLANGCompiler.SLANG
{
    public enum ConstInfoType
    {
        Value,
        Code,
    }

    public class ConstInfo
    {
        public ConstInfoType ConstInfoType { get; set; }
        public int Value { get; set; }
        public string SymbolString { get; set; }

        public ConstInfo(int value)
        {
            this.ConstInfoType = ConstInfoType.Value;
            this.Value = value;
            this.SymbolString = null;
        }

        public ConstInfo(string symbolStr)
        {
            this.ConstInfoType = ConstInfoType.Code;
            this.Value = 0;
            this.SymbolString = symbolStr;
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
