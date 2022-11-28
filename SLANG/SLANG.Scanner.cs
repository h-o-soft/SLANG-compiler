using System;
using System.Collections.Generic;
using System.Text;

namespace SLANGCompiler.SLANG
{
    internal partial class SLANGScanner
    {
        CodeRepository codeRepository;
        ConstTableManager constTableManager;
        SLANGParser constParser = new SLANGParser();

        private bool isSourceComment = false;

        public void SetCodeRepository(CodeRepository codeRepository)
        {
            this.codeRepository = codeRepository;
        }

        public void SetConstTableManager(ConstTableManager constTableManager)
        {
            this.constTableManager = constTableManager;
        }

        public bool CheckConst(string constStr)
        {
            constParser.ParseConstExpr("#CCHK" + constStr, constTableManager);
            if(constParser.LastConstExpr.IsConst())
            {
                // Console.WriteLine("Check:" + constParser.LastConstExpr.Value);
                return constParser.LastConstExpr.ConstValue.Value != 0;
            } else {
                error("expr must be const. : " + constStr);
            }
            return false;
        }


        void GetChar(string charStr)
        {
            if(charStr.Length != 1)
            {
                yyerror("invalid char format");
                return;
            }
            var constValue = new ConstInfo((int)charStr[0]);
            yylval.constValue = constValue;
        }
        void GetNumber()
        {
            var number = SLANGCommonUtility.GetValue(yytext);
            var constValue = new ConstInfo(number);
            yylval.constValue = constValue;
        }

        private Dictionary<string, Token> keywordDictionary = new Dictionary<string, Token>()
        {
            {"VAR", Token.VAR},
            {"BYTE", Token.BYTE},
            {"WORD", Token.WORD},
            {"ARRAY", Token.ARRAY},
            {"CONST", Token.CONST},
            {"IF", Token.IF},
            {"THEN", Token.THEN},
            {"ELSE", Token.ELSE},
            {"ELIF", Token.ELIF},
            {"ELSEIF", Token.ELIF},
            {"EF", Token.ELIF},
            {"ENDIF", Token.ENDIF},
            {"WHILE", Token.WHILE},
            {"DO", Token.DO},
            {"WEND", Token.WEND},
            {"EXIT", Token.EXIT},
            {"REPEAT", Token.REPEAT},
            {"UNTIL", Token.UNTIL},
            {"FOR", Token.FOR},
            {"TO", Token.TO},
            {"DOWNTO", Token.DOWNTO},
            {"NEXT", Token.NEXT},
            {"CONTINUE", Token.CONTINUE},
            {"BEGIN", Token.BEGIN},
            {"END", Token.END},
            {"AND", Token.OP_AND},
            {"OR", Token.OP_OR},
            {"XOR", Token.OP_XOR},
            {"MOD", Token.OP_MOD},
            {"HIGH", Token.HIGH},
            {"LOW", Token.LOW},
            {"CPL", Token.CPL},
            {"NOT", Token.NOT},
            {"GOTO", Token.GOTO},
            {"ORG", Token.ORG},
            {"WORK", Token.WORK},
            {"OFFSET", Token.OFFSET},
            {"MACHINE", Token.MACHINE},
            {"RETURN", Token.RETURN},
            {"PRINT", Token.PRINT},
            {"CODE", Token.CODE},
            {"CASE", Token.CASE},
            {"OTHERS", Token.OTHERS},
            {"OF", Token.OF},
            {"LOOP", Token.LOOP},
        };

        Token GetIdentifier()
        {
            // Console.WriteLine($"GetIdentifier:{yytext}");
            Token token;
            if(keywordDictionary.TryGetValue(yytext.ToUpper(), out token))
            {
                return token;
            }
            ConstInfo info;
            if(constTableManager.TryGetValue(yytext, out info))
            {
                yylval.constValue = info;
                return Token.CONSTANT;
            }

            yylval.symbol = yytext;
            return Token.IDENTIFIER;
        }

        public Token GetAddop()
        {
            var op = yytext.ToLower();
            switch(op)
            {
                case "+":
                    yylval.op = "+"; // Token.OP_ADD;
                break;
                case "-":
                    yylval.op = "-"; // Token.OP_SUB;
                break;
            }
            return Token.addop;
        }

        public Token GetEqop()
        {
            // opが==以外の場合はNeqとして処理される(テキトー)
            yylval.op = yytext.ToLower();
            return Token.eqop;
        }

        public Token GetCompop()
        {
            yylval.op = yytext.ToLower();
            return Token.compop;
        }

        public Token GetScompop()
        {
            yylval.op = yytext;
            return Token.scompop;
        }

        public Token GetIncDecOp()
        {
            yylval.op = yytext;
            return Token.incdecop;
        }

        public Token GetShiftop()
        {
            var op = yytext.ToLower();
            switch(op)
            {
                case "<<":
                    yylval.op = "<<"; // Token.OP_LSHIFT;
                break;
                case ">>":
                    yylval.op = ">>"; // Token.OP_RSHIFT;
                break;
            }
            return Token.shiftop;
        }

        public Token GetSshiftop()
        {
            var op = yytext.ToLower();
            switch(op)
            {
                case ".<<.":
                    yylval.op = ".<<."; // Token.OP_LSHIFT;
                break;
                case ".>>.":
                    yylval.op = ".>>."; // Token.OP_RSHIFT;
                break;
            }
            return Token.sshiftop;
        }

        public void ProcIf(bool flag)
        {
            if(flag)
            {
                BEGIN(INITIAL);
            } else {
                BEGIN(PREIFSKIP);
            }
        }

        // どうするかな……。
        public string currentFileName { get; set; }

        public int ErrorCount{ get; private set; }

        public System.IO.TextWriter ErrorTextWriter { get; set; }

        public void error(string error)
        {
            if(ErrorTextWriter != null)
            {
                ErrorTextWriter.WriteLine(error);
            } else {
                Console.Error.WriteLine(error);
            }
            ErrorCount++;
        }

		public override void yyerror(string format, params object[] args)
		{
            format = $"{currentFileName}:{yylloc.EndLine+1} " + format;
			base.yyerror(format, args);
            error(string.Format(format, args));
            //Console.WriteLine($"yylloc StartLine:{yylloc.StartLine} EndLine:{yylloc.EndLine} StartColumn:{yylloc.StartColumn} EndColumn:{yylloc.EndColumn}");
		}


        private void genSourceComment(string source)
        {
            if(isSourceComment)
            {
                var comments = source.Replace("\r","").Split("\n");
                foreach(var comment in comments)
                {
                    if(!string.IsNullOrEmpty(comment))
                    {
                        codeRepository.AddComment(comment);
                    }
                }
            }
        }

        /// <summary>
        /// SLANGのソースコードをアセンブラソースにコメントとして含めるかどうかを設定する
        /// </summary>
        public void SetSourceComment(bool isSourceComment)
        {
            this.isSourceComment = isSourceComment;
        }
    }
}
