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
        // グローバルシンボルテーブル
        private SymbolTableManager symbolTableManager;
        // ローカルシンボルテーブル
        private SymbolTableManager localSymbolTableManager;
        // 関数用のシンボルテーブル
        private List<SymbolTableManager> functionSymbolTableManagerList;

        // CONST値管理マネージャ
        private ConstTableManager constTableManager;
        public ConstTableManager ConstTableManager => constTableManager;

        // シンボルテーブル群を初期化する。言語標準の変数もあわせて定義する。
        private void initSymbolTable()
        {
            symbolTableManager = new SymbolTableManager(this);
            localSymbolTableManager = new SymbolTableManager(this);
            functionSymbolTableManagerList = new List<SymbolTableManager>();
            constTableManager = new ConstTableManager();

            funcNumber = 0;

            constTableManager.Add("TRUE", 1);
            constTableManager.Add("FALSE", 0);

            var port = new SymbolTable()
            {
                Name = "PORT",
                SymbolClass = SymbolClass.Global,
                TypeInfo = TypeInfo.PortByte,
                Size = 1,
            };
            symbolTableManager.Add(port);
            var portw = new SymbolTable()
            {
                Name = "PORTW",
                SymbolClass = SymbolClass.Global,
                TypeInfo = TypeInfo.PortWord,
                Size = 1,
            };
            symbolTableManager.Add(portw);

            var mem = new SymbolTable()
            {
                Name = "MEM",
                SymbolClass = SymbolClass.Global,
                TypeInfo = TypeInfo.MemoryByte,
                Address = new ConstInfo(0),
                Size = 65536
            };
            symbolTableManager.Add(mem);

            var memw = new SymbolTable()
            {
                Name = "MEMW",
                SymbolClass = SymbolClass.Global,
                TypeInfo = TypeInfo.MemoryWord,
                Address = new ConstInfo(0),
                Size = 65536
            };
            symbolTableManager.Add(memw);

            var memf = new SymbolTable()
            {
                Name = "MEMF",
                SymbolClass = SymbolClass.Global,
                TypeInfo = TypeInfo.MemoryFloat,
                Address = new ConstInfo(0),
                Size = 65536
            };
            symbolTableManager.Add(memf);

            symbolTableManager.AddSymbol("^BC", TypeInfo.WordTypeInfo, true);
            symbolTableManager.AddSymbol("^DE", TypeInfo.WordTypeInfo, true);
            symbolTableManager.AddSymbol("^HL", TypeInfo.WordTypeInfo, true);
            symbolTableManager.AddSymbol("^IX", TypeInfo.WordTypeInfo, true);
            symbolTableManager.AddSymbol("^IY", TypeInfo.WordTypeInfo, true);
            symbolTableManager.AddSymbol("^AF", TypeInfo.WordTypeInfo, true);
            symbolTableManager.AddSymbol("^A", TypeInfo.WordTypeInfo, true);      // AについてはAFの2バイト目を指すように改竄されるので注意。その関係でワークは1バイト無駄が出る。
            symbolTableManager.AddSymbol("^CARRY", TypeInfo.WordTypeInfo, true).AddAliasName("^CY");
            symbolTableManager.AddSymbol("^ZERO", TypeInfo.WordTypeInfo, true);
            symbolTableManager.AddSymbol("^SP", TypeInfo.WordTypeInfo, true);

            // 仮定義のワーク末尾シンボル。コード生成時に削除され、ワークの末尾に移動する。
            var workEndArray = new SymbolTable()
            {
                Name = "WORKEND",
                SymbolClass = SymbolClass.Global,
                TypeInfo = new TypeInfo(TypeInfoClass.Array, 0, TypeDataSize.Byte, TypeInfo.ByteTypeInfo),
                Size = 0
            };
            symbolTableManager.Add(workEndArray, true);
        }

        /// <summary>
        /// シンボルテーブルについてソースコードで使われた変数名、関数名をそのまま使う場合はtrue、内部番号に置換する場合はfalseを指定する
        /// </summary>
        public void SetOriginalSymbolUse(bool originalUse)
        {
            symbolTableManager.UseOriginalSymbol = originalUse;
            localSymbolTableManager.UseOriginalSymbol = originalUse;
        }

        /// <summary>
        /// シンボルテーブルについてソースコードで使われた変数名、関数名をデバッグ用に定義する( VAL という変数が _VAL_ としてシンボル定義される)
        /// </summary>
        public void SetOutputDebugSymbol(bool outputOriginal)
        {
            symbolTableManager.OutputOriginalSymbol = outputOriginal;
            localSymbolTableManager.OutputOriginalSymbol = outputOriginal;
        }

        /// <summary>
        /// シンボルテーブルとCONSTテーブルについて大文字小文字の区別をする場合はtrue、しない場合はfalseを指定する
        /// </summary>
        public void SetCaseSensitiveSymbol(bool caseSensitive)
        {
            symbolTableManager.CaseSensitive = caseSensitive;
            localSymbolTableManager.CaseSensitive = caseSensitive;
            constTableManager.CaseSensitive = caseSensitive;
        }

        /// <summary>
        /// シンボルテーブルの状態を表示する(デバッグ用)
        /// </summary>
        public void dispSymbolTable()
        {
            Console.WriteLine("===== GLOBAL =====");
            symbolTableManager?.DebugDisp();
            Console.WriteLine("===== LOCAL =====");
            localSymbolTableManager?.DebugDisp();
        }

        // Treeの情報を元にシンボルを定義する
        private SymbolTable symbolDecl(Tree tree, SymbolClass symbolClass, DeclNode baseNode)
        {
            bool isArray = (baseNode == DeclNode.Array);
            bool isMachine = (baseNode == DeclNode.Machine);
            SymbolTable s = null;
            TypeInfo typeInfo = null;
            TypeInfo firstTypeInfo = null;
            bool isIndirect = false;
            Tree arrayTree = null;
            int arraySize = 1;
            ConstInfo address = null;
            int arrayCount = 0;
            int paramCount = 0;
            FunctionType functionType = isMachine ? FunctionType.Machine : FunctionType.Normal;
            Tree initialValueCodeTree = null;

            List<int> initialValueList = null;
            while(true)
            {
                if(tree == null)
                {
                    return null;
                }
                switch(tree.Node)
                {
                    case DeclNode.TypeInfo:
                        {
                            typeInfo = tree.TypeInfo.Clone();
                            firstTypeInfo = typeInfo.Clone();
                            tree = tree.First;
                            if(tree.Address != null)
                            {
                                address = tree.Address;
                            }
                            break;
                        }
                    case DeclNode.Id:
                        {
                            if(tree.Address != null)
                            {
                                address = tree.Address;
                            }
                            int dataSize;
                            if(isIndirect)
                            {
                                // 間接変数はポインタなので必ずデータサイズは2バイト
                                // 間接配列変数の場合は配列ぶんを掛けておく
                                dataSize = 2;
                            } else {
                                dataSize = firstTypeInfo.DataSize.GetDataSize();
                            }
                            if(arrayTree != null)
                            {
                                dataSize *= arraySize;
                            }
                            Tree initialValueCode = null;
                            if(isArray)
                            {
                                initialValueList = null;
                                initialValueCode = initialValueCodeTree;
                            } else if(initialValueList == null)
                            {
                                initialValueList = tree.InitialValues;
                            }
                            s = new SymbolTable()
                            {
                                Name = tree.IdentifierName,
                                SymbolClass = symbolClass,
                                TypeInfo =  typeInfo,
                                Address = address,
                                Size = isMachine ? paramCount : dataSize,
                                InitialValueList = initialValueList,
                                InitialValueCode = initialValueCode,
                                FunctionType = functionType,
                                Used = true
                            };
                            return s;
                        }
                    case DeclNode.Ptr:
                    {
                        // 配列宣言の場合は間接ではなく、サイズなしの配列として扱う
                        if(isArray)
                        {
                            arrayTree = tree;
                            typeInfo = new TypeInfo(TypeInfoClass.Array, -1, typeInfo.DataSize, null);
                        } else {
                            isIndirect = true;
                        }
                        tree = tree.First;
                        break;
                    }
                    case DeclNode.Array:
                    {
                        arrayCount++;
                        if(arrayCount > 2)
                        {
                            Error("invalid array dimension");
                            return null;
                        }
                        arrayTree = tree;
                        var baseType = typeInfo;
                        var tp = new TypeInfo((isArray || arrayCount > 1) ? TypeInfoClass.Array : TypeInfoClass.Indirect, tree.ArraySize + 1, typeInfo.GetDataSize(), typeInfo);
                        typeInfo = tp;
                        arraySize *= (tree.ArraySize+1);
                        // 間接変数宣言である
                        if(tp.InfoClass == TypeInfoClass.Indirect)
                        {
                            isIndirect = true;
                        }
                        if(tree.Address != null)
                        {
                            address = tree.Address;
                        }
                        if(tree.initialValueCodeTree != null)
                        {
                            initialValueCodeTree = tree.initialValueCodeTree;
                        } else if(tree.InitialValues != null)
                        {
                            // 主に間接変数のアドレス初期値代入
                            initialValueList = tree.InitialValues;
                        }
                        tree = tree.First;
                        break;
                    }
                    case DeclNode.Func:
                    {
                        typeInfo = new TypeInfo(TypeInfoClass.Function, 0, TypeDataSize.Word, typeInfo);
                        if(isMachine)
                        {
                            paramCount = tree.ArraySize;
                            if(tree.Address != null)
                            {
                                address = tree.Address;
                            }
                        }

                        tree = tree.First;
                        break;
                    }
                    case DeclNode.Dummy:
                    {
                        tree = tree.First;
                        break;
                    }
                    default:
                    {
                        return null;
                    }
                }
            }
        }

        // ローカル変数の定義
        private void localDataDecl(Tree tree, bool isLocal)
        {
            for(Tree p = tree.First; p != null ; p = p.Second)
            {
                var symbol = symbolDecl(p, isLocal ? SymbolClass.Local : SymbolClass.Global, tree.Node );
                if(symbol != null)
                {
                    localSymbolTableManager.Add(symbol);
                }
            }
        }

        // グローバル変数の定義
        public void globalDataDecl(Tree tree)
        {
            if(tree == null)
            {
                return;
            }
            if(tree.Node == DeclNode.Const)
            {
                // CONSTは都度定義しているのでここでは定義しない
                return;
            }

            SymbolTable symbol;
            for(Tree p = tree.First; p != null; p = p.Second)
            {
                symbol = symbolDecl(p, SymbolClass.Global, tree.Node);
                if(symbol != null)
                {
                    symbolTableManager.Add(symbol);
                }
            }
        }



        /// <summary>
        /// CONST定義状態を表示する(デバッグ用)
        /// </summary>
        public void DispConstTable()
        {
            constTableManager.DebugDisp();
        }

        // シンボルテーブルのアセンブラコードを出力する
        private void genSymbolTable(StreamWriter outputStreamWriter)
        {
            if(workAddressValue >= 0)
            {
                outputStreamWriter.WriteLine($"\n\tORG\t${workAddressValue:X}\n");
            }

            outputStreamWriter.WriteLine("; Variables (works)");
            var workLabel = "__WORK__";
            outputStreamWriter.WriteLine(workLabel + ":");

            outputStreamWriter.Flush();
            int workOffset = 0;

            // 関数の静的宣言の出力(初期値なし)
            foreach(var manager in functionSymbolTableManagerList)
            {
                workOffset = manager.GenerateCode(outputStreamWriter, workLabel, workOffset);
            }

            outputStreamWriter.WriteLine("");

            // グローバル変数の出力(初期値なし)
            workOffset = symbolTableManager.GenerateCode(outputStreamWriter, workLabel, workOffset);

            outputStreamWriter.WriteLine($"\n__WORKEND__ EQU (__WORK__ + {workOffset})");
            outputStreamWriter.Flush();
        }
    }
}
