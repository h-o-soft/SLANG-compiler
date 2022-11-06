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
    /// CONST定義されたシンボルとその値を管理するマネージャ
    /// </summary>
    public class ConstTableManager
    {
        private Dictionary<string, int> constTableDictionary = new Dictionary<string, int>();

        /// <summary>
        /// CONST定義されたシンボルと値を追加する
        /// </summary>
        public void Add(string name, int value)
        {
            constTableDictionary[name] = value;
        }

        /// <summary>
        /// CONST定義された値が存在すればvalueに返し、戻り値がtrueになる。存在しない場合はfalseになる。
        /// </summary>
        public bool TryGetValue(string name, out int value)
        {
            if(constTableDictionary.TryGetValue(name, out value))
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
                Console.WriteLine($"{pair.Key} : {pair.Value}");
            }
        }
    }
}
