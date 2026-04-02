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
    /// Z80のレジスタEnum
    /// </summary>
    public enum Register
    {
        HL,
        DE,
        BC,
        A,
        C,

        Invalid
    }
    public static partial class RegisterExtend {
            private static readonly string[] registerString = new string[]
            {
                "HL",
                "DE",
                "BC",
                "A",
                "C",
                "(Invalid)"
            };
            /// <summary>
            /// レジスタを示すenum値をZ80ニーモニックとして返す
            /// </summary>
            public static string GetCode(this Register param)
            {
                return registerString[(int)param];
            }
    }
}
