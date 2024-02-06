using System;
using System.IO;
using System.Text;
using System.Linq;
using System.Collections.Generic;
using YamlDotNet.Serialization;
using YamlDotNet.Serialization.NamingConventions;

namespace SLANGCompiler.SLANG
{
    /// <summary>
    /// 最適化ルール情報
    /// </summary>
    public class OptimizeRule
    {
        public string[] Codes{ get; private set; }
        public string[] ReplaceCodes { get; private set; }

        public OptimizeRule(string[] codes, string[] replaceCodes)
        {
            this.Codes = codes;
            this.ReplaceCodes = replaceCodes;
        }
    }


    /// <summary>
    /// 最適化クラス(のぞき穴的最適化)
    /// </summary>
    public class CodeOptimizer
    {
        private class OptimizeRuleOrig
        {
            public string code = null;
            public string replaceCode = null;
        }

        private List<OptimizeRule> optimizeRuleList;

        public CodeOptimizer()
        {
            this.optimizeRuleList = new List<OptimizeRule>();
        }

        /// <summary>文字列がレジスタの場合はtrue、そうでない場合はfalseを返す</summary>
        private bool IsRegister(string str)
        {
            string[] registers = new string[]
            {
                "A","AF","B","C","BC","D","E","DE","H","L","HL","IY","IX","SP","PC","I","R"
            };
            str = str.ToUpper();
            return registers.Contains(str);
        }

        /// <summary>与えられたルールと一致した場合trueを返しreplaceCodeに置き換えコードを出力する、そうでない場合はfalseを返す</summary>
        private bool tryGetReplaceCode(OptimizeRule rule, List<string> codeList, int codeIdx, out string[] replacedCode)
        {
            string[] ruleCode = rule.Codes;

            var stringReplaceDictionary = new Dictionary<int, string>();

            int idx;
            int ruleIdx;
            int captureIdx = 0;
            bool captureMode = false;
            bool captureCheckMode = false;
            int captureNumber = 0;
            char[] delimiters = new char[]{' ', ',', ':', '\t'};
            StringBuilder sb = new StringBuilder();

            replacedCode = null;

            // 1行ずつルールにマッチしているか確認
            bool match = true;
            for(int i = 0; i < ruleCode.Length; i++)
            {
                // コードが尽きたので不一致で戻る
                if(codeIdx >= codeList.Count)
                {
                    return false;
                }
                var codeStr = codeList[codeIdx];
                var ruleStr = ruleCode[i];

                codeStr = codeStr.Trim().TrimEnd();

                ruleIdx = 0;    // ルールコード側文字index
                idx = 0;        // 調べるコード側文字index

                while(match)
                {
                    // @1-@9で指定された文字列を拾う(ただしレジスタの場合は不一致とする)
                    if(captureMode)
                    {
                        if(idx >= codeStr.Length || delimiters.Contains(codeStr[idx]))
                        {
                            if(sb.Length > 0)
                            {
                                captureMode = false;

                                var captureStr = sb.ToString();
                                // レジスタ名の場合は不一致とする(@?をレジスタと一致させるルールは無い)
                                if(IsRegister(captureStr))
                                {
                                    match = false;
                                    break;
                                }
                                stringReplaceDictionary[captureNumber] = sb.ToString();
                                continue;
                            } else {
                                // 1文字も拾えない場合はマッチしなかった事とする
                                match = false;
                                break;
                            }
                        }
                        sb.Append(codeStr[idx]);
                        idx++;
                        continue;
                    }
                    if(captureCheckMode)
                    {
                        var captureStr = stringReplaceDictionary[captureNumber];
                        if(idx >= codeStr.Length && captureIdx >= captureStr.Length)
                        {
                            // 同時に終端に来たので@x に一致した
                            captureCheckMode = false;
                            continue;
                        }
                        if(idx >= codeStr.Length || captureIdx >= captureStr.Length)
                        {
                            // どちらか片方が終わってしまったので一致しなかった
                            match = false;
                            break;
                        }
                        if(codeStr[idx] != captureStr[captureIdx])
                        {
                            match = false;
                            break;
                        }
                        idx++;
                        captureIdx++;
                        continue;
                    }

                    if(idx >= codeStr.Length && ruleIdx >= ruleStr.Length)
                    {
                        // 同時に終端に来たので一致した事とする
                        break;
                    }
                    if(idx >= codeStr.Length || ruleIdx >= ruleStr.Length)
                    {
                        // どちらか片方が終わってしまったので一致しなかった
                        match = false;
                        break;
                    }

                    if(ruleStr[ruleIdx] == '@')
                    {
                        ruleIdx++;
                        captureNumber = (int)(ruleStr[ruleIdx] - '0');
                        ruleIdx++;
                        if(stringReplaceDictionary.ContainsKey(captureNumber))
                        {
                            captureIdx = 0;
                            captureCheckMode = true;
                        } else {
                            captureMode = true;
                        }
                        sb.Clear();
                        continue;
                    } else {
                        if(codeStr[idx] != ruleStr[ruleIdx])
                        {
                            match = false;
                            break;
                        }
                        idx++;
                        ruleIdx++;
                    }
                }
                if(!match)
                {
                    return false;
                }
                codeIdx++;
            }
            // ここまで来たら一致のはず
            if(match)
            {
                // replacedCodeを生成する
                var replaceCodes = rule.ReplaceCodes;
                var resultStrList = new List<string>();
                foreach(var repCode in replaceCodes)
                {
                    var codeStr = repCode;
                    // 置き換え文字を置換する
                    foreach(var pair in stringReplaceDictionary)
                    {
                        codeStr = codeStr.Replace($"@{pair.Key}", pair.Value);
                    }
                    if(codeStr != ";"){
                        resultStrList.Add(codeStr);
                    }
                    replacedCode = resultStrList.ToArray();
                }
            }
            return match;
        }

        /// <summary>覗き穴的最適化ルールとマッチした場合コードの置き換えを行う</summary>
        private bool CheckAllRules(List<string> codeList, int idx)
        {
            var oldCodeList = new List<string>();
            // 全ルールをチェックする
            foreach(var rule in optimizeRuleList)
            {
                // 一致するか？
                if(tryGetReplaceCode(rule, codeList, idx, out string[] replaceCodes))
                {
                    oldCodeList.Clear();
                    for(int i = 0; i < rule.Codes.Length; i++)
                    {
                        oldCodeList.Add(codeList[idx+i]);
                    }
                    codeList.RemoveRange(idx, rule.Codes.Length);

                    // // 置き換え前コード(チェック用)
                    // foreach(var code in oldCodeList)
                    // {
                    //     codeList.Insert(idx, $"; {code}");
                    //     idx++;
                    // }

                    foreach(var code in replaceCodes)
                    {
                        if(code.Contains(':') || code.Contains('.') || code.Contains(';'))
                        {
                            // 字下げなし
                            codeList.Insert(idx, $"{code}");
                        } else {
                            // 字下げあり
                            codeList.Insert(idx, $" {code}");
                        }
                        idx++;
                    }
                    return true;
                }
            }
            return false;
        }

        /// <summary>
        /// のぞき穴的最適化を行う
        /// </summary>
        public List<string> PeepholeOptimize(List<string> codeListOrig, out int matchCount)
        {
            int totalCount = 0;
            int optimizeCount;
            // コメントを削除したリストを作る
            var codeList = new List<string>();
            foreach(var code in codeListOrig)
            {
                if(code.Length == 0 || code[0] != ';')
                {
                    codeList.Add(code);
                }
            }

            do
            {
                optimizeCount = 0;
                for(int i = 0; i < codeList.Count; i++)
                {
                    if(CheckAllRules(codeList, i))
                    {
                        optimizeCount++;
                    }
                }
                totalCount += optimizeCount;
            } while(optimizeCount != 0);

            matchCount = totalCount;

            return codeList;
        }

        private string[] GetCodes(string codeStr)
        {
            // とりあえず改行で区切ってTrimして返すのみ
            var codeList = new List<string>();
            var codes = codeStr.Split('\n');
            foreach(var code in codes)
            {
                codeList.Add(code.Trim());
            }
            return codeList.ToArray();
        }

        /// <summary>
        /// のぞき穴的最適化用の最適化ルールファイルを読み込む
        /// </summary>
        public void LoadOptimizeRule(string filePath)
        {

            StreamReader sr = new StreamReader(filePath, Encoding.GetEncoding("UTF-8"));
            var deserializer = new DeserializerBuilder()
                .WithNamingConvention(UnderscoredNamingConvention.Instance)
                .Build();
            var yamlObj = deserializer.Deserialize<List<OptimizeRuleOrig>>(sr);
            sr.Close();

            foreach(var data in yamlObj)
            {
                string[] codes;
                string[] replaceCodes;

                codes = GetCodes(data.code);
                replaceCodes = GetCodes(data.replaceCode);

                var optRule = new OptimizeRule(codes, replaceCodes);
                AddRule(optRule);
            }
        }

        private void AddRule(OptimizeRule rule)
        {
            optimizeRuleList.Add(rule);
        }
    }
}
