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
            constTableManager.Add(symbolTree.IdentifierName, value.Value);
            return Tree.CreateTree1(DeclNode.Dummy);
        }
    }
}
