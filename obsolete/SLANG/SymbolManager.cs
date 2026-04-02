using System;
using System.Collections.Generic;
using System.IO;
using System.Text;

namespace SLANGCompiler.SLANG
{

    /// <summary>
    /// <para>シンボルテーブルマネージャー</para>
    /// <para>シンボルテーブルを管理し、最終的なシンボルテーブル関連のコード生成までを行う</para>
    /// </summary>
    public class SymbolTableManager
    {
        protected List<SymbolTable> symbolTableList = new List<SymbolTable>();
        /// <summary>
        /// シンボルテーブルマネージャーが管理するシンボルテーブル一覧
        /// </summary>
        public List<SymbolTable> SymbolTableList => symbolTableList;

        private TypeInfo funcWordTypeInfo;
        private TypeInfo funcFloatTypeInfo;

        private IErrorReporter errorReporter;

        public bool UseOriginalSymbol { get; set; }

        public bool CaseSensitive { get; set; }

        public bool OutputOriginalSymbol { get; set; }

        public SymbolTableManager(IErrorReporter errorReporter)
        {
            this.errorReporter = errorReporter;
            Initialize();
            UseOriginalSymbol = false;
            CaseSensitive = false;
            OutputOriginalSymbol = false;
        }

        /// <summary>
        /// シンボルテーブルマネージャーを初期化する
        /// </summary>
        public void Initialize()
        {
            symbolTableList.Clear();
            funcWordTypeInfo = TypeInfo.CreateTypeInfo(TypeInfoClass.Function, TypeInfo.WordTypeInfo.Clone(), 2, TypeDataSize.Word);
            funcFloatTypeInfo = TypeInfo.CreateTypeInfo(TypeInfoClass.Function, TypeInfo.FloatTypeInfo.Clone(), 3, TypeDataSize.Float);
        }

        /// <summary>
        /// 関数を追加する
        /// </summary>
        public void AddFunction(FunctionType functionType, string name, string insideName, int paramCount, ConstInfo Address = null, bool? useOriginalSymbol = null, bool isRuntime = false, OperatorType operatorType = OperatorType.Word)
        {
            if(useOriginalSymbol == null)
            {
                useOriginalSymbol = UseOriginalSymbol;
            }
            var symbol = new SymbolTable()
            {
                Name = name,
                InsideName = insideName,
                FunctionType = functionType,
                SymbolClass = SymbolClass.Global,
                TypeInfo = operatorType == OperatorType.Float ? funcFloatTypeInfo : funcWordTypeInfo,
                Size = paramCount,
                Address = Address,
                UseOriginalSymbol = (bool)useOriginalSymbol,
                IsRuntime = isRuntime
            };
            Add(symbol);
            symbol.Id = symbolTableList.IndexOf(symbol);
        }

        /// <summary>
        /// シンボルを追加する
        /// </summary>
        public SymbolTable Add(SymbolTable table, bool? useOriginalSymbol = null)
        {
            if(useOriginalSymbol == null)
            {
                useOriginalSymbol = UseOriginalSymbol;
            }
            foreach(var s in symbolTableList)
            {
                if(s.Name == table.Name)
                {
                    // 一時定義関数を実関数宣言に入れ替えられるか？
                    if(!s.TypeInfo.IsFunction() || !table.TypeInfo.IsFunction())
                    {
                        errorReporter.Error($"symbol \"{s.Name}\" already defined.");
                        return null;
                    }
                    // 実宣言により一時定義関数を置き換える
                    if(s.TypeInfo.InfoClass == TypeInfoClass.TempFunc && table.TypeInfo.InfoClass == TypeInfoClass.Function)
                    {
                        s.TypeInfo = table.TypeInfo;
                        s.Size = table.Size;
                        s.Address = table.Address;
                        s.UseOriginalSymbol = (bool)useOriginalSymbol;
                        table.Id = s.Id;
                        return s;
                    } else if(s.FunctionType != FunctionType.Machine)
                    {
                        // 引数の数が違う場合はエラーとなる(MACHINE宣言された場合はパラメータの違いは無視する(というか、tableで届いたものは捨てる))
                        if(s.Size != table.Size)
                        {
                            errorReporter.Error("invalid function parameter count : " + s.Name);
                        }
                    }
                    return s;
                }
            }
            table.UseOriginalSymbol = (bool)useOriginalSymbol;
            symbolTableList.Add(table);
            table.Id = symbolTableList.IndexOf(table);
            return table;
        }

        /// <summary>
        /// シンボル名を元にシンボルを探す
        /// </summary>
        public SymbolTable SearchSymbol(string name)
        {
            var symName = CaseSensitive ? name : name.ToUpper();
            foreach(var table in symbolTableList)
            {
                var tableName = CaseSensitive ? table.Name : table.Name.ToUpper();
                // シンボル名を探す
                if(symName == tableName)
                {
                    return table;
                }

                // 別名も探す
                if(table.AliasNameList != null)
                {
                    foreach(var aliasName in table.AliasNameList)
                    {
                        var aName = CaseSensitive ? aliasName : aliasName.ToUpper();
                        if(symName == aName)
                        {
                            return table;
                        }
                    }
                }
            }
            return null;
        }

        /// <summary>
        /// 型情報と共にシンボルを追加する
        /// </summary>
        public SymbolTable AddSymbol(string name, TypeInfo typeInfo, bool? useOriginalSymbol, string namespaceName = null)
        {
            if(useOriginalSymbol == null)
            {
                useOriginalSymbol = UseOriginalSymbol;
            }
            var table = new SymbolTable()
            {
                Name = name,
                TypeInfo = typeInfo,
                Address = null,
                UseOriginalSymbol = (bool)useOriginalSymbol,
                NamespaceName = namespaceName
            };
            symbolTableList.Add(table);
            table.Id = symbolTableList.IndexOf(table);

            return table;
        }

        public void Remove(string name)
        {
            var table = SearchSymbol(name);
            if(table != null)
            {
                symbolTableList.Remove(table);
            }
        }

        private readonly static string DefaultNamespaceName = "NAME_SPACE_DEFAULT";

        // 初期値なしのシンボルテーブルコードを生成する(EQUにより定義される)
        private int generateCodeWorks(StreamWriter outputStreamWriter, string workLabel, int workOffset)
        {
            string currentNamespace = DefaultNamespaceName;
            bool isNotDefaultNamespace = false;
            // Global Classの変数のコードを出力する
            foreach(var symbol in symbolTableList)
            {
                var namespaceName = "";
                if(string.IsNullOrEmpty(symbol.NamespaceName))
                {
                    namespaceName = DefaultNamespaceName;
                } else {
                    namespaceName = symbol.NamespaceName;
                }
                if(currentNamespace != namespaceName)
                {
                    outputStreamWriter.Write($"[{namespaceName}]\n");
                    currentNamespace = namespaceName;
                    isNotDefaultNamespace = currentNamespace != DefaultNamespaceName;
                }

                if(symbol.SymbolClass != SymbolClass.Global)
                {
                    continue;
                }
                // PORT配列、関数は出力しない
                var infoClass = symbol.TypeInfo.InfoClass;
                if(infoClass == TypeInfoClass.PortArray || infoClass == TypeInfoClass.MemoryArray || infoClass == TypeInfoClass.Function)
                {
                    continue;
                }
                // アドレス指定がしてある場合も出力しない
                if(symbol.Address != null)
                {
                    continue;
                }
                if(symbol.InitialValueList != null || symbol.InitialValueCode != null)
                {
                    continue;
                }

                var labelName = symbol.LabelName;

                // 強引。^Aは^AFの2バイト目を指すようにする
                if(labelName == "_A")
                {
                    outputStreamWriter.WriteLine($"{labelName} EQU (_AF + 1)");
                } else {
                    var prefix = isNotDefaultNamespace?DefaultNamespaceName+".":"";
                    outputStreamWriter.WriteLine($"{labelName} EQU ({prefix}__WORK__ + {workOffset})");

                    if(OutputOriginalSymbol && symbol.OriginalName != labelName)
                    {
                        // デバッグ用シンボルとして本来の名前もEQUで設定しておく(大丈夫？)
                        outputStreamWriter.WriteLine($"_{symbol.NormalizeOriginalName}_ EQU {labelName}");
                    }
                }
                if(symbol.TypeInfo.IsIndirectType())
                {
                    // 間接変数なので格納領域は必ず2(ポインタ)
                    workOffset += 2;
                } else if(symbol.TypeInfo.IsArray())
                {
                    workOffset += symbol.Size;
                } else {
                    workOffset += symbol.TypeInfo.GetDataSize().GetDataSize();
                }
            }

            if(currentNamespace != DefaultNamespaceName)
            {
                    outputStreamWriter.Write($"[{DefaultNamespaceName}]\n");
            }

            return workOffset;
        }

        /// <summary>
        /// 一時定義関数が残っているかどうかを確認し、残っていた場合はtrue、残っていない場合はfalseを返す
        /// </summary>
        public bool CheckTempFunc()
        {
            bool hasError = false;
            foreach(var table in symbolTableList)
            {
                if(table.IsTempFunction())
                {
                    hasError = true;
                    errorReporter.Error($"Could not found identifier : {table.Name}", true);
                }
            }
            return hasError;
        }

        public void GenerateInitialValueSymbol(CodeRepository codeRepository, ICodeStatementGenerator codeStatementGenerator)
        {
            bool isFirst = true;
            // Global Classの変数のコードを出力する
            foreach(var symbol in symbolTableList)
            {
                if(symbol.SymbolClass != SymbolClass.Global)
                {
                    continue;
                }
                // PORT配列、メモリ配列、関数は出力しない
                var infoClass = symbol.TypeInfo.InfoClass;
                if(infoClass == TypeInfoClass.PortArray || infoClass == TypeInfoClass.MemoryArray || infoClass == TypeInfoClass.Function)
                {
                    continue;
                }
                // アドレス指定がしてある場合も出力しない
                if(symbol.Address != null)
                {
                    continue;
                }
                if(symbol.InitialValueList == null && symbol.InitialValueCode == null)
                {
                    continue;
                }

                if(isFirst)
                {
                    codeRepository.AddCode("\n");
                    codeRepository.AddCode("; Variables (has initial values)\n");
                    isFirst = false;
                }

                var labelName = symbol.LabelName;

                if(OutputOriginalSymbol && symbol.OriginalName != labelName)
                {
                    // デバッグ用シンボルとして本来の名前もEQUで設定しておく(大丈夫？)
                    // _(変数名)_ という名前で定義される(うーむ)
                    codeRepository.AddCode($"_{symbol.NormalizeOriginalName}_ EQU ({labelName})\n");
                }
                codeRepository.AddCode($"{labelName}:\n");

                var dataSize = symbol.Size;

                var isFloat = symbol.TypeInfo.GetDataSize() == TypeDataSize.Float;
                var isByte = symbol.TypeInfo.GetDataSize() == TypeDataSize.Byte;
                isByte = isByte && !symbol.TypeInfo.IsIndirect();
                string dataDefine = isByte ? "DB" : "DW";

                if(symbol.TypeInfo.IsIndirectType())
                {
                    // 間接変数なので初期値がある場合は必ず2バイトの値のはず
                    if(symbol.InitialValueList != null)
                    {
                        codeRepository.AddCode($" DW {symbol.InitialValueList[0]}\n");
                    }

                } else if(symbol.TypeInfo.IsArray())
                {
                    var arraySize = symbol.Size;
                    var oneDataSize = (symbol.TypeInfo.GetDataSize() == TypeDataSize.Byte ? 1 : 2);
                    var arrayCount = arraySize / oneDataSize;
                    int initByteSize = 0;

                    if(symbol.InitialValueCode != null)
                    {
                        initByteSize = codeStatementGenerator.GenerateCodeStmt(symbol.InitialValueCode);
                    } else {
                        errorReporter.Error("Could not be found initial array value : " + labelName);
                    }
                    if(initByteSize < arrayCount)
                    {
                        var restSize = arraySize - initByteSize;
                        codeRepository.AddCode($" DS {restSize}\n");
                    }
                } else {
                    if(symbol.InitialValueList == null)
                    {
                        if(isFloat)
                        {
                            codeRepository.AddCode($" DS 3\n");
                        } else if(isByte)
                        {
                            codeRepository.AddCode($" DS 1\n");
                        } else {
                            codeRepository.AddCode($" DS 2\n");
                        }
                    } else {
                        if(isFloat)
                        {
                            errorReporter.Error("not support (float initial value)");
                        } else {
                            codeRepository.AddCode($" {dataDefine} {symbol.InitialValueList[0]}\n");
                        }
                    }
                }
            }
            if(!isFirst)
            {
                codeRepository.AddCode("\n");
            }
        }

        /// <summary>
        /// シンボルテーブルのコードを出力する
        /// </summary>
        public int GenerateCode(StreamWriter outputStreamWriter, string workLabel, int workOffset = 0)
        {
            if(workLabel != null)
            {
                workOffset = generateCodeWorks(outputStreamWriter, workLabel, workOffset);
            }

            outputStreamWriter.Flush();

            return workOffset;
        }

        /// <summary>
        /// シンボルテーブルの中身をテキトーに表示する(デバッグ用)
        /// </summary>
        public void DebugDisp()
        {
            Console.WriteLine("■SYMBOL");
            foreach(var symbol in symbolTableList)
            {
                Console.WriteLine($"Symbol: {symbol.Name} : VAR{symbolTableList.IndexOf(symbol)} ");
                Console.WriteLine($"  SymbolClass: {symbol.SymbolClass} ");
                if(symbol.TypeInfo.IsFunction())
                {
                    Console.WriteLine($"  FunctionType:{symbol.FunctionType}");
                }
                Console.WriteLine($"  Size: {symbol.Size} ");
                Console.WriteLine($"  TypeInfo: ");
                TypeInfo lastTypeInfo = null;
                Console.WriteLine(symbol.TypeInfo.ToString());
                for(TypeInfo t = symbol.TypeInfo; t != null ; t = t.Parent)
                {
                    lastTypeInfo = t;
                    //Console.WriteLine(t.ToString());
                }
                Console.WriteLine($"  One Data Size: {lastTypeInfo.DataSize}");
                Console.WriteLine($"  Offset: {symbol.Address} ");
                Console.WriteLine($"  InitialValues:");
                if(symbol.InitialValueList != null)
                {
                    foreach(var value in symbol.InitialValueList)
                    {
                        Console.Write($"{value},");
                    }
                    Console.WriteLine("");
                }
            }
        }
    }
}
