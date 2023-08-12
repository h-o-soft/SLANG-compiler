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
        int locVarSize;
        int exitLabel;
        int localOffset;

        SymbolTable currentFunction;

        int funcNumber;

        /// <summary>
        /// 関数開始時の初期化
        /// </summary>
        public void initFunc()
        {
            // 関数内ラベルを消す
            labelManager.Clear();
        }

        /// <summary>
        /// 関数の開始コードを出力する
        /// </summary>
        private void funchead()
        {
            // モジュールモードではない関数定義の前に初期化コードを置く(うーん……)
            if(!isInitCodeGenerated && !isCurrentModuleMode)
            {
                // ここでORGを書く
                var moduleCount = ((SLANGScanner)this.Scanner).moduleCount;
                if(moduleCount > 0)
                {
                    gencode($"\n\tORG\t${orgValue:X},${moduleCount * 0x10000:X}\n");
                } else {
                    gencode($"\n\tORG\t${orgValue:X}\n");
                }
                genInitCode();
                isInitCodeGenerated = true;
            }
            locVarSize = computeOffset();
            gencode($"; Function : {currentFunction.Name}\n");
            genfunclabel(currentFunction);
            if(locVarSize > 0)
            {
                gencode(" PUSH IY\n");
                gencode($" LD BC,{locVarSize}\n");
                gencode(" ADD IY,BC\n");
            }

            exitLabel = genNewLabel();
        }

        /// <summary>
        /// 関数の終了コードを出力する
        /// </summary>
        public void funcend(Expr expr)
        {
            // 戻り値あり
            if(expr != null)
            {
                genexptop(expr);
            }

            genlabel(exitLabel);

            if(locVarSize > 0)
            {
                gencode(" POP IY\n");
            }
            gencode(" RET\n");
            gencode(";\n");
        }

        /// <summary>
        /// 関数の終了処理を行う
        /// </summary>
        public void endFunc()
        {
            // 未定義ラベルがあるかどうか調べる(ある場合はエラーが出力される)
            labelManager.CheckNotDefinedLabel();
        }

        // 関数を定義する
        private void funcDef(Tree tree)
        {
            SymbolTable symbol;

            // 関数をシンボルテーブルに登録
            symbol = symbolDecl(tree, SymbolClass.Global, DeclNode.Func);
            if(symbol != null)
            {
                // MAIN関数だけは何があってもそのまま使う
                bool? useOriginalSymbol = null;
                if(symbol.Name.ToUpper() == "MAIN")
                {
                    useOriginalSymbol = true;
                }
                symbol = symbolTableManager.Add(symbol, useOriginalSymbol);
            }

            if(symbol == null)
            {
                return;
            }
            if(!symbol.TypeInfo.IsFunction())
            {
                Error($"{symbol.Name} is not declared as function");
                return;
            }
            symbol.SymbolClass = SymbolClass.Global;

            currentFunction = symbol;

            // ローカル変数定義
            functionSymbolTableManagerList.Add(localSymbolTableManager);
            localSymbolTableManager = new SymbolTableManager(this);
            localSymbolTableManager.UseOriginalSymbol = symbolTableManager.UseOriginalSymbol;
            localSymbolTableManager.CaseSensitive = symbolTableManager.CaseSensitive;
            localSymbolTableManager.OutputOriginalSymbol = symbolTableManager.OutputOriginalSymbol;
            funcNumber++;

            // 関数のパラメータがくっついているTreeを探す
            Tree p;
            for(p = tree; ; p = p.First)
            {
                if(p.Node == DeclNode.Func && p.First.Node == DeclNode.Id)
                {
                    break;
                }
            }

            int paramCount = 0;
            for(p = p.Second; p != null; p = p.Second)
            {
                var s = paramDecl(p.First.First.TypeInfo, p.First.First);
                if(s != null)
                {
                    localSymbolTableManager.Add(s);
                }
                paramCount++;
            }
            // 関数の引数の数を設定
            symbol.Size = paramCount;

        }

        // 関数のパラメータを定義する
        private SymbolTable paramDecl(TypeInfo typeInfo, Tree tree)
        {
            TypeInfo firstTypeInfo = null;
            ConstInfo address = null;

            while(true)
            {
                switch(tree.Node)
                {
                    case DeclNode.Id:
                        {
                            if(tree.Address != null)
                            {
                                address = tree.Address;
                            }
                            int dataSize = firstTypeInfo.DataSize.GetDataSize();
                            SymbolTable s = new SymbolTable()
                            {
                                Name = tree.IdentifierName,
                                SymbolClass = SymbolClass.Param,
                                TypeInfo =  typeInfo,
                                Address = address,
                                Size = dataSize,
                                InitialValueList = null
                            };
                            return s;
                        }
                    case DeclNode.Ptr:
                        {
                            // パラメータでは配列は指定出来ない(はず)
                            tree = tree.First;
                            break;
                        }
                    case DeclNode.Func:
                        {
                            // Console.WriteLine("Func regLoc function");
                            tree = tree.First;
                            break;
                        }
                    case DeclNode.Array:
                        {
                            // パラメータの場合は間接変数になるはずなので、そうする
                            var baseType = typeInfo;
                            var tp = new TypeInfo(TypeInfoClass.Indirect, 0, TypeDataSize.Word, typeInfo );
                            typeInfo = tp;

                            tree = tree.First;
                            break;
                        }
                    case DeclNode.TypeInfo:
                        {
                            typeInfo = tree.TypeInfo.Clone();
                            firstTypeInfo = tree.TypeInfo.Clone();
                            tree = tree.First;
                            if(tree.Address != null)
                            {
                                address = tree.Address;
                            }
                            break;
                        }
                    default:
                        bug("paramdecl");
                        break;
                }
            }
        }

        /// <summary>
        /// 関数自身のラベルを出力する
        /// </summary>
        private void genfunclabel(SymbolTable symbol)
        {
            var funcLabel = symbol.LabelName;
            if(symbolTableManager.OutputOriginalSymbol && symbol.OriginalName != funcLabel)
            {
                gencode($"_{symbol.NormalizeOriginalName}_ EQU {funcLabel}\n");
            }
            gencode($"{funcLabel}:\n");

            // 一応処理中ですよ、という事で標準出力にも出してみる
            Console.WriteLine($";  function: {symbol.Name}");
        }

        // 関数のパラメータとローカル変数のオフセットを計算する
        private int computeOffset()
        {
            int offset = 0;
            foreach(var p in localSymbolTableManager.SymbolTableList)
            {
                if(p.SymbolClass == SymbolClass.Global)
                {
                    // p.LabelHeader = "F" + funcNumber;
                    p.LabelHeader = currentFunction.NormalizeOriginalName;
                    continue;
                }
                p.Address = new ConstInfo(offset);
                if(p.TypeInfo.IsArray())
                {
                    offset += p.Size;
                } else if(p.TypeInfo.GetDataSize() == TypeDataSize.Float)
                {
                    // FLOATは3バイト必要
                    offset++;
                }
                offset += 2;
            }

            localOffset = 0x70 - offset;

            // ローカル変数の位置を調整する
            foreach(var p in localSymbolTableManager.SymbolTableList)
            {
                if(p.SymbolClass == SymbolClass.Global)
                {
                    continue;
                }
                p.Address.Value += localOffset;
            }
            return offset;
        }
    }
}
