using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using YamlDotNet.RepresentationModel;
using YamlDotNet.Serialization;
using YamlDotNet.Serialization.NamingConventions;

namespace SLANGCompiler.SLANG
{
    internal partial class SLANGParser
    {
        /// <summary>
        /// プログラム内の文字列を管理するマネージャクラス
        /// </summary>
        public class StringDataManager
        {
            private List<string> stringList = new List<string>();

            /// <summary>
            /// 文字列番号を元に文字列を得る
            /// </summary>
            public string GetString(int num)
            {
                if(num > stringList.Count - 1)
                {
                    return null;
                }
                return stringList[num];
            }

            /// <summary>
            /// 文字列番号を元に文字列のラベルを返す
            /// </summary>
            public string GetLabel(int num)
            {
                return "_STR" + num;

            }

            /// <summary>
            /// 文字列を追加し、文字列番号を返す
            /// </summary>
            public int AddString(string str)
            {
                stringList.Add(str);
                return stringList.Count - 1;
            }

            /// <summary>
            /// 文字列を未使用状態にする(最終的に出力されなくなる)
            /// </summary>
            public void Remove(int num)
            {
                if(num > stringList.Count - 1)
                {
                    return;
                }
                stringList[num] = null;
            }

            /// <summary>
            /// 文字列を全てクリアする
            /// </summary>
            public void Clear()
            {
                stringList.Clear();
            }

            /// <summary>
            /// 文字列のアセンブラ定義用コードを得る
            /// </summary>
            public string GetStringCode(string str, bool requireZero)
            {
                var codeList = new List<string>();

                int crCount = 0;
                bool insideString = false;
                bool requireComma = false;
                StringBuilder sb = new StringBuilder();
                foreach(var ch in str)
                {
                    // 表示出来ない文字はバイナリとして出力する
                    if((int)ch < 0x20)
                    {
                        // 文字列の途中の場合は閉じる
                        if(insideString)
                        {
                            sb.Append('"');
                            insideString = false;
                        }
                        if(requireComma)
                        {
                            sb.Append(',');
                        }
                        sb.Append($"${(int)ch:X2}");
                        requireComma = true;
                        crCount += 3;
                    } else {
                        if(!insideString)
                        {
                            if(requireComma)
                            {
                                sb.Append(',');
                                requireComma = false;
                            }
                            sb.Append('"');
                            insideString = true;
                            requireComma = true;
                        }
                        sb.Append(ch);
                        crCount++;
                    }
                }
                if(insideString)
                {
                    sb.Append('"');
                    insideString = false;
                }
                if(requireZero)
                {
                    if(requireComma)
                    {
                        sb.Append(',');
                        requireComma = false;
                    }
                    sb.Append("0\n");
                } else {
                    sb.Append("\n");
                }

                return sb.ToString();
            }

            /// <summary>
            /// 管理している文字列群をアセンブラのコードとして出力する
            /// </summary>
            public void GenerateCode(StreamWriter writer)
            {
                int idx = 0;
                foreach(var str in stringList)
                {
                    if(str != null)
                    {
                        writer.Write(GetLabel(idx));
                        writer.Write(":\n");

                        writer.Write(" DB ");

                        var code = GetStringCode(str, true);

                        writer.Write(code);
                    }
                    idx++;
                }
                writer.Flush();
            }
        }
    }
}