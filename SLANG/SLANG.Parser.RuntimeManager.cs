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
        /// ランタイム情報(YAMLで読まれる)
        /// </summary>
        public class RuntimeCode
        {
            public string[] calls = null;
            public string insideName = null;
            public string code = null;
            public FunctionType functionType = FunctionType.Machine;
            public int paramCount = 0;
            public string address = null;
            public Dictionary<string, int> works = null;
            public  OperatorType resultType = OperatorType.Word;
            public string initializeCode = null;
            public string libName = null;
            public string extlib = null;
        }

        /// <summary>
        ///  ランタイムのコードを管理し、使われたもののみを出力するクラス
        /// </summary>
        public class RuntimeManager
        {
            // 関連するランタイムと利用状況を管理するクラス
            private class RuntimeInfo
            {
                public string Name { get; private set; }
                public string InsideName { get; private set; }
                public bool Used { get; private set; }
                public string Code { get; set; }
                public string InitializeCode { get; set; }
                public string[] InsideCalls { get; private set; }

                public Dictionary<string, int> WorkDictionary { get; private set; }

                public string NamespaceName { get; set; }

                public RuntimeInfo(string name, string insideName, bool used, string code, string initializeCode, string[] insideCalls, Dictionary<string, int> workDictionary, string namespaceName)
                {
                    Name = name;
                    InsideName = insideName;
                    Used = used;
                    Code = code;
                    InitializeCode = initializeCode;
                    InsideCalls = insideCalls;
                    WorkDictionary = workDictionary;
                    NamespaceName = namespaceName;
                }

                public void Use()
                {
                    Used = true;
                }

                public void Unuse()
                {
                    Used = false;
                }
            }

            // ランタイム一覧。この順番でRuntimeが出力される
            private List<RuntimeInfo> runtimeInfoList = new List<RuntimeInfo>();

            // SymbolTableManagerと連携して処理する
            private SymbolTableManager symbolTableManager;

            private string runtimePath;

            private static readonly string InitialNamespace = "NAME_SPACE_DEFAULT";
            private string currentNamespace = InitialNamespace;
            private Stack<string> namespaceStack = new Stack<string>();


            public RuntimeManager(SymbolTableManager symbolTableManager)
            {
                this.symbolTableManager = symbolTableManager;
            }

            /// <summary>
            /// ランタイムファイル(YAML)を読み込む
            /// </summary>
            public void LoadRuntime(string fileName)
            {
                if(!File.Exists(fileName))
                {
                    throw new FileNotFoundException($"could not found runtime file. {fileName}");
                }
                // ランタイムのパスを保存
                runtimePath = System.IO.Path.GetDirectoryName(System.IO.Path.GetFullPath(fileName));

                StreamReader sr = new StreamReader(fileName, Encoding.GetEncoding("UTF-8"));
                var deserializer = new DeserializerBuilder()
                    .WithNamingConvention(UnderscoredNamingConvention.Instance)
                    .Build();
                var yamlObj = deserializer.Deserialize<Dictionary<string,RuntimeCode>>(sr);
                sr.Close();

                foreach(var data in yamlObj)
                {
                    AddRuntime(data.Key, data.Value);
                }
            }

            private string LoadExtLib(string libPath, string labelName)
            {
                var libraryPath = Path.Combine(runtimePath, "extlib", libPath);
                StringBuilder codeStr = new StringBuilder();
                if(File.Exists(libraryPath))
                {
                    var stream = new FileStream(libraryPath, FileMode.Open);
                    var charsetDetectedResult = UtfUnknown.CharsetDetector.DetectFromStream(stream);
                    stream.Position = 0;
                    stream.Close();
                    
                    bool found = false;
                    foreach (string line in System.IO.File.ReadLines(libraryPath, charsetDetectedResult.Detected.Encoding))
                    {
                        if(found && line.Trim().ToUpper().StartsWith("#ENDLIB"))
                        {
                            break;
                        }
                        if(found)
                        {
                            codeStr.Append(line + "\n");
                        }
                        if(line.Trim().ToUpper().StartsWith("#LIB"))
                        {
                            var checkLibName = line.Trim().Split(' ')[1];
                            if(checkLibName == labelName)
                            {
                                found = true;
                            }
                        }
                    }  
                }
                return codeStr.ToString();
            }

            /// <summary>
            ///  ランタイムを追加する
            /// </summary>
            private void AddRuntime(string label, RuntimeCode runtimeCode)
            {
                var calls = runtimeCode.calls;
                var code = runtimeCode.code;
                var libName = runtimeCode.libName;
                var extlib = runtimeCode.extlib;
                var initializeCode = runtimeCode.initializeCode;

                var info = new RuntimeInfo(label, runtimeCode.insideName, false, null, null, runtimeCode.calls, runtimeCode.works, libName);

                if(string.IsNullOrEmpty(code) && !string.IsNullOrEmpty(extlib))
                {
                    // codeにextlibの中から特定コードを読み込む
                    var libPath    = extlib.Split(':')[0];
                    var labelName  = extlib.Split(':')[1];
                    code = LoadExtLib(libPath, labelName);
                }

                var sb = new StringBuilder();
                var codes = code.Split("\n");
                foreach(var str in codes)
                {
                    // ラベル以外を字下げする
                    if(!(str.Length > 0 && (str[0] == '.' || str.IndexOf(":") > 0)))
                    {
                        sb.Append(" ");
                    }
                    sb.Append(str);
                    sb.Append("\n");
                }
                info.Code = sb.ToString();

                if(initializeCode != null)
                {
                    sb.Clear();
                    codes = initializeCode.Split("\n");
                    foreach(var str in codes)
                    {
                        // ラベル以外を字下げする
                        if(!(str.Length > 0 && (str[0] == '.' || str.IndexOf(":") > 0)))
                        {
                            sb.Append(" ");
                        }
                        sb.Append(str);
                        sb.Append("\n");
                    }
                    info.InitializeCode = sb.ToString();
                }

                runtimeInfoList.Add(info);

                // 関数アドレス指定が入っている場合は16進数、2進数での指定を考慮し、変換してから渡す
                // 文字列の場合は文字列CONSTとして渡す
                int address = -1;
                string addressStr = null;
                if(!string.IsNullOrEmpty(runtimeCode.address))
                {
                    var adrStr = runtimeCode.address;
                    if(adrStr[0] == '$')
                    {
                        // 16進数
                        address = Convert.ToInt32(adrStr.Substring(1), 16);
                    } else if(char.ToLower(adrStr[adrStr.Length - 1]) == 'h')
                    {
                        // 16進数
                        address = Convert.ToInt32(adrStr.Substring(0, adrStr.Length - 1), 16);
                    } else if(char.ToLower(adrStr[adrStr.Length - 1]) == 'b')
                    {
                        // 2進数
                        address = Convert.ToInt32(adrStr.Substring(0, adrStr.Length - 1), 2);
                    } else {
                        if(!int.TryParse(adrStr, out address))
                        {
                            address = -1;
                            addressStr = runtimeCode.address;
                        }
                    }
                }
                ConstInfo addressInfo = null;
                if(address == -1)
                {
                    if(addressStr != null)
                    {
                        addressInfo = new ConstInfo(addressStr, true);
                    }
                } else {
                    addressInfo = new ConstInfo(address);
                }

                // シンボルテーブル側に反映させておく
                string runtimeName = "";
                if(!string.IsNullOrEmpty(info.NamespaceName))
                {
                    runtimeName = info.NamespaceName + ".";
                }
                if(!string.IsNullOrEmpty(info.InsideName))
                {
                    runtimeName += info.InsideName;
                } else {
                    runtimeName += label;
                }
                symbolTableManager.AddFunction(runtimeCode.functionType, label, runtimeName, runtimeCode.paramCount, addressInfo, true, true, runtimeCode.resultType);
            }

            /// <summary>
            /// 関連する全てのランタイムに使用マークをつける
            /// </summary>
            public bool Use(string label)
            {
                foreach(var info in runtimeInfoList)
                {
                    if(info.Name == label)
                    {
                        if(info.Used)
                        {
                            return false;
                        }
                        info.Use();
                        if(info.InsideCalls != null)
                        {
                            foreach(var call in info.InsideCalls)
                            {
                                Use(call);
                            }
                        }
                        return false;
                    }
                }
                return true;
            }

            public string GetRuntimeCode(string runtimeName)
            {
                if(Use(runtimeName))
                {
                    return null;
                }

                foreach(var info in runtimeInfoList)
                {
                    if(info.Name == runtimeName)
                    {
                        // 最終的にコード出力しない事にする
                        info.Unuse();
                        return info.Code;
                    }
                }
                return null;
            }

            private void PushNamespace(string currentName)
            {
                namespaceStack.Push(currentName);
            }

            private string PopNamespace()
            {
                return namespaceStack.Pop();
            }

            private void ChangeNamespace(string namespaceName, StreamWriter writer)
            {
                var runtimeNamespace = namespaceName;
                if(string.IsNullOrEmpty(runtimeNamespace))
                {
                    runtimeNamespace = InitialNamespace;
                }

                if(runtimeNamespace != currentNamespace)
                {
                    //PushNamespace(currentNamespace);
                    currentNamespace = runtimeNamespace;
                    if(string.IsNullOrEmpty(currentNamespace))
                    {
                        currentNamespace = InitialNamespace;
                    }
                    writer.Write($"[{currentNamespace}]\n");
                }
            }

            /// <summary>
            /// StreamWriterに対してランタイムコードを出力する
            /// </summary>
            public void Generate(StreamWriter writer)
            {
                foreach(var info in runtimeInfoList)
                {
                    if(info.Used)
                    {
                        var name = info.InsideName;
                        if(string.IsNullOrEmpty(name))
                        {
                            name = info.Name;
                        }

                        ChangeNamespace(info.NamespaceName, writer);
                        writer.Write($"{name}:\n");
                        if(info.Name == "CALL")
                        {
                            // ランタイム関数「CALL」については最適化のため差し替えを行う(無駄なレジスタ代入を避ける)
                            genCall(writer);
                        } else {
                            writer.Write(info.Code);
                        }
                        writer.WriteLine("");

                        if(info.InitializeCode != null)
                        {
                            writer.Write($"{name}_INITIALIZE:\n");
                            writer.Write(info.InitializeCode);
                        }
                    }
                }
                // 最後は初期Namespaceに戻す
                ChangeNamespace(InitialNamespace, writer);
            }

            /// <summary>
            /// ランタイム初期化コードを呼び出す
            /// </summary>
            public void GenerateInitializeCode(StreamWriter writer)
            {
                writer.WriteLine("RUNTIME_INIT:");
                foreach(var info in runtimeInfoList)
                {
                    if(info.Used && info.InitializeCode != null)
                    {
                        var name = info.InsideName;
                        if(string.IsNullOrEmpty(name))
                        {
                            name = info.Name;
                        }
                        writer.WriteLine($" CALL {name}_INITIALIZE");
                    }
                }
                writer.WriteLine("RET");
            }

            public void AddWorkSymbol()
            {
                foreach(var info in runtimeInfoList)
                {
                    if(info.Used && info.WorkDictionary != null)
                    {
                        foreach(var pair in info.WorkDictionary)
                        {
                            var symbolName = pair.Key;
                            var dataSize = pair.Value;

                            if(dataSize == 1)
                            {
                                // BYTE変数
                                symbolTableManager.AddSymbol(symbolName, TypeInfo.ByteTypeInfo, true, info.NamespaceName);
                            } else if(dataSize == 2)
                            {
                                // WORD変数
                                symbolTableManager.AddSymbol(symbolName, TypeInfo.WordTypeInfo, true, info.NamespaceName );
                            } else {
                                // 配列変数
                                var arrayTypeInfo = new TypeInfo(TypeInfoClass.Array, dataSize, TypeDataSize.Byte, TypeInfo.ByteTypeInfo);
                                var symbol = symbolTableManager.AddSymbol(symbolName, arrayTypeInfo, true, info.NamespaceName );
                                symbol.Size = dataSize;
                            }
                        }
                    }
                }
            }

            // ランタイム「CALL」については、使われたレジスタのみを代入するコードを生成する
            private void genCall(StreamWriter writer)
            {
                string[] registers = new string[]{
                    "BC", "DE", "HL", "IX", "IY"
                };
                writer.Write(" PUSH IY\n");
                writer.Write(" LD DE,.call1\n");
                writer.Write(" PUSH DE\n");
                writer.Write(" PUSH HL\n");
                writer.Write(" LD A,(_AF+1)\n");
                // レジスタ変数未使用の場合は0を入れ、使用済みの場合は普通に代入する
                for(int i = 0; i < registers.Length; i++)
                {
                    var reg = registers[i];
                    var symbol = symbolTableManager.SearchSymbol("^" + reg);
                    if(symbol.Used)
                    {
                        writer.Write($" LD {reg},(_{reg})\n");
                    } else {
                        writer.Write($" LD {reg},0\n");
                    }
                }
                writer.Write(" RET\n");
                writer.Write(".call1\n");
                writer.Write(" PUSH HL\n");
                writer.Write(" CALL GETREG\n");
                writer.Write(" LD HL,6\n");
                writer.Write(" ADD HL,SP\n");
                writer.Write(" POP HL\n");
                writer.Write(" POP IY\n");
                writer.Write(" RET\n");
            }
        }
    }
}
