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
    /// CONST値の種類
    /// </summary>
    public enum ConstType
    {
        /// <summary>BYTE定数(使われていない)</summary>
        Byte,
        /// <summary>WORD定数</summary>
        Word,
        /// <summary>CODE定数(CODEのアドレスを指す)/summary>
        Code,
    }

    /// <summary>
    /// CONST値クラス(パースされた直後にこれに格納される)
    /// </summary>
    public struct Const
    {
        public ConstType constType;
        public int value;
        public string symbolValue;
    }
}
