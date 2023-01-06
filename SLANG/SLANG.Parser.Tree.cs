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
        ///   <para>CONST定義1つを処理し、ツリーをつなげる</para>
        ///   <para>一行の中で定義したものを同一行で使う事があるため、宣言時に1つずつ定義している。</para>
        /// </summary>
        public Tree DefineConst(Tree symbolTree, Expr value)
        {
            if(value.IsIntValueConst())
            {
                // 普通の数値
                constTableManager.Add(symbolTree.IdentifierName, value.Value);
            } else if(value.IsFloatValueConst())
            {
                // 普通の数値(Float)
                constTableManager.Add(symbolTree.IdentifierName, value.GetConstFloatValue());
            } else if(value.Opcode == Opcode.Adr && value.Symbol.FunctionType == FunctionType.Machine){
                // シンボル(関数ラベル)
                // ランタイムにある場合は利用フラグを立てる
                runtimeManager.Use(value.Symbol.Name);
                constTableManager.AddCode(symbolTree.IdentifierName, value.Symbol.Name);
            } else if(value.Opcode == Opcode.Adr && value.TypeInfo.Parent != null && value.TypeInfo.Parent.InfoClass == TypeInfoClass.TempFunc)
            {
                // この場合はラベルの別名定義と思われるのでランタイム名を文字列として設定する
                // (同時に、TempFuncではありえないので、シンボルテーブルから外す)
                symbolTableManager.Remove(value.Symbol.Name);
                var str = value.Symbol.RuntimeName;
                constTableManager.AddString(symbolTree.IdentifierName, str);
            } else {
                // この場合は文字列定数として扱う
                var str = createExprString(value);
                constTableManager.AddString(symbolTree.IdentifierName, str);
            }
            return Tree.CreateTree1(DeclNode.Dummy);
        }

        public Tree DefineConst(Tree symbolTree, Tree codeTree)
        {
            var name = symbolTree.IdentifierName;
            // codeTreeの内容を初期値に持つConstと同名の配列を定義する
            var tpInfo = new TypeInfo(TypeInfoClass.Array, 1, TypeDataSize.Byte, TypeInfo.WordTypeInfo.Clone());
            var symbol = new SymbolTable()
            {
                Name = name,
                SymbolClass = SymbolClass.Global,
                TypeInfo = tpInfo,
                Address =null,
                Size = 1,
                InitialValueCode = codeTree,
                InitialValueList = null,
                FunctionType = FunctionType.Machine,
                Used = true
            };
            symbolTableManager.Add(symbol);

            // 配列の名称をCONST値として設定する
            constTableManager.AddCode(symbolTree.IdentifierName, symbolTree.IdentifierName);

            return Tree.CreateTree1(DeclNode.Dummy);
        }
    }
}
