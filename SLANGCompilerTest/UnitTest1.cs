using System;
using Xunit;
using Xunit.Abstractions;
using SLANGCompiler.SLANG;
using System.Collections.Generic;
using System.Net.Http.Headers;
using System.Text;
using System.Reflection;

namespace SLANGCompilerTest
{
    public class CodeTestHelper
    {
        private List<string> codeList;
        private int line;
        ITestOutputHelper output;

        public CodeTestHelper(ITestOutputHelper output)
        {
            this.output = output;
        }

        public void Start(string code)
        {
            codeList = new List<string>(code.Split('\n'));
            line = 0;
        }

        public bool Seek(string str)
        {
            for(int i = 0; i < codeList.Count; i++)
            {
                if (codeList[i].Contains(str))
                {
                    line = i + 1;
                    return true;
                }
            }
            output.WriteLine($"Could not found {str}");
            return false;
        }

        public bool Check(string str)
        {
            if (codeList[line].Contains(str))
            {
                return true;
            }
            output.WriteLine($"Could not found {str} : {codeList[line]}");
            return false;
        }

    }
    public class UnitTest1
    {
        private readonly ITestOutputHelper output;

        SLANGCompiler.Program prog = new SLANGCompiler.Program();
        CodeTestHelper helper;
        System.IO.MemoryStream errorStream;

        public UnitTest1(ITestOutputHelper output)
        {
            this.output = output;
            this.helper = new CodeTestHelper(output);
            errorStream = new System.IO.MemoryStream();
        }

        protected bool CodeCheck(string code, string searchStr)
        {
            var resultCode = prog.ParseString(code, errorStream);
            helper.Start(resultCode);
            output.WriteLine(Encoding.UTF8.GetString(errorStream.ToArray()));

            string[] serachs = searchStr.Split('\n');

            foreach(var search in serachs)
            {
                if (!helper.Seek(search))
                {
                    output.WriteLine($"OutputCode: {resultCode}");
                    return false;
                }
            
            }
            return true;
        }

        [Fact(DisplayName = "CONST値が正しく反映される")]
        public void ConstTest1()
        {
            // CONST単体
            Assert.True(CodeCheck(
                "CONST TEST=123;\nVAR TVAR=TEST;",
                "__TVAR:\n DW 123"
                ));

            // CONST計算
            Assert.True(CodeCheck(
                "CONST TEST=123+15;\nVAR TVAR=TEST;",
                "__TVAR:\n DW 138"
                ));

            // CONS同士の計算、同一行で定義したCONST値の参照
            Assert.True(CodeCheck(
                "CONST TEST=123,TEST2=456,TEST3=TEST+TEST2+5;\nVAR TVAR=TEST3;",
                "__TVAR:\n DW 584"
                ));
        }

        [Fact(DisplayName = "単純変数の定義")]
        public void SimpleVarTest()
        {
            Assert.True(CodeCheck(
                "VAR VAL;",
                "__VAL EQU (__WORK__"
                ));
        }

        [Fact(DisplayName = "初期値あり単純変数の定義")]
        public void SimpleVarTest2()
        {
            Assert.True(CodeCheck(
                "VAR VAL=1234;",
                "__VAL:\n DW 1234"
                ));
        }

        [Fact(DisplayName = "単純変数を使った演算")]
        public void SimpleVarExpr1()
        {
            output.WriteLine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile));

            // CONSTの加算
            Assert.True(CodeCheck(
                "VAR VAL;\nMAIN()\nBEGIN\n VAL=100; VAL=VAL+123;\nEND;",
                " LD HL,100\n LD (__VAL),HL\n LD HL,(__VAL)\n LD DE,123\n ADD HL,DE\n LD (__VAL),HL"
                ));
            // 単純変数の加算
            Assert.True(CodeCheck(
                "VAR VAL;\nMAIN()\nBEGIN\n VAL=100; VAL=VAL+VAL;\nEND;",
                " LD HL,100\n LD (__VAL),HL\n LD HL,(__VAL)\n LD DE,(__VAL)\n ADD HL,DE\n LD (__VAL),HL"
                ));
            // CONSTの減算
            Assert.True(CodeCheck(
                "VAR VAL;\nMAIN()\nBEGIN\n VAL=100; VAL=VAL-123;\nEND;",
                " LD HL,100\n LD (__VAL),HL\n LD HL,(__VAL)\n OR A\n SBC HL,123\n LD (__VAL),HL"
                ));
            // 単純変数の減算
            Assert.True(CodeCheck(
                "VAR VAL;\nMAIN()\nBEGIN\n VAL=100; VAL=VAL-VAL;\nEND;",
                " LD HL,100\n LD (__VAL),HL\n LD HL,(__VAL)\n LD DE,(__VAL)\n OR A\n SBC HL,DE\n LD (__VAL),HL"
                ));
            // CONSTの乗算(4倍)
            Assert.True(CodeCheck(
                "VAR VAL;\nMAIN()\nBEGIN\n VAL=100; VAL=VAL*4;\nEND;",
                " LD HL,100\n LD (__VAL),HL\n LD HL,(__VAL)\n ADD HL,HL\n ADD HL,HL\n LD (__VAL),HL"
                ));
            // CONSTの乗算(5倍)
            Assert.True(CodeCheck(
                "VAR VAL;\nMAIN()\nBEGIN\n VAL=100; VAL=VAL*5;\nEND;",
                " LD HL,100\n LD (__VAL),HL\n LD HL,(__VAL)\n LD DE,5\n CALL MULHLDE\n LD (__VAL),HL"
                ));
        }


    }
}
