%namespace SLANGCompiler.SLANG
%scannertype SLANGScanner
%visibility internal
%tokentype Token

%option stack, minimize, parser, verbose, persistbuffer, noembedbuffers 

%option unicode, codepage:UTF-8

%x COMMENT
%x LINECOMMENT 
%x STRING
%x CHARCONST
%x INCL
%x PREIFSKIP
%x SKIPEOL
%x ASM

Eol             (\r\n?|\n)
dotchr          [^\r\n]
NotWh           [^ \t\r\n]
Space           [ \t]
Number          ([0-9]+|$[0-9a-fA-F]+|[0-9a-fA-F]+[Hh]|[01]+[Bb])
IdentSymbol     [_@\^]
Identifier      ({IdentSymbol}|[a-zA-Z\u3041-\u3096\u30A1-\u30FA々〇〻\u3400-\u9FFF\uF900-\uFAFF\uD840-\uD87F\uDC00-\uDFFF])({IdentSymbol}|[0-9a-zA-Z\u3041-\u3096\u30A1-\u30FA々〇〻\u3400-\u9FFF\uF900-\uFAFF\uD840-\uD87F\uDC00-\uDFFF])*


PREIF   #[Ii][Ff]
PREELSE #[Ee][Ll][Ss][Ee]
PREEND  #[Ee][Nn][Dd]|#[Ee][Nn][Dd][Ii][Ff]
ASM     #[Aa][Ss][Mm]

CMTSTART	"/\*"|"(*"
CMTEND		"*\/"|"*)"

hex (x|X)[0-9a-fA-F]{1,2}
oct [0-7]{1,3}

ELIF        [Ee][Ll][Ss][Ee]
EF          [Ee][Ff]
OP_ADD      \+
OP_SUB      -
OP_MUL      \*
OP_DIV      /
OP_EQ       =
OP_LSHIFT   \<\<
OP_RSHIFT   \>\>
P_OPEN      \(
P_CLOSE     \)
B_OPEN      \[
B_CLOSE     \]
BR_OPEN     \{
BR_CLOSE    \}
F_OPEN      \｢
F_CLOSE     \｣
COMMA       ,
COLON       :
QUESTION    \?
SC          ;

CP_EQ       ==
CP_NE       <>

EXC   !

FORMD [Ff][Oo][Rr][Mm]\$
DECID [Dd][Ee][Cc][Ii]\$
PER   \%
PND   [Pp][Nn]\$
HEX2D [Hh][Ee][Xx]2\$
HEX4D [Hh][Ee][Xx]4\$
MSGD  [Mm][Ss][Gg]\$
MSXD  [Mm][Ss][Xx]\$
STRD  [Ss][Tt][Rr]\$
CHRD  [Cc][Hh][Rr]\$
SPCD  [Ss][Pp][Cc]\$
CRD   [Cc][Rr]\$
TABD  [Tt][Aa][Bb]\$
STRFUNC {FORMD}|{DECID}|{PND}|{HEX2D}|{HEX4D}|{MSGD}|{MSXD}|{STRD}|{CHRD}|{SPCD}|{CRD}|{TABD}

%{
    StringBuilder lexStrBuffer = null;
    bool nextBraceIsArray = false;

    private class LocationInfo
    {
        private string fileName;
        public string FileName => fileName;

        private QUT.Gppg.LexLocation location;
        public QUT.Gppg.LexLocation Location => location;

        public LocationInfo(string fileName, QUT.Gppg.LexLocation location)
        {
            this.fileName = fileName;
            this.location = location;
        }
    }

    Stack<LocationInfo> locationStack = new Stack<LocationInfo>();
    
    private class ContextInfo
    {
        public bool useLocation;
        public BufferContext context;
        public ContextInfo(BufferContext context, bool useLocation)
        {
            this.context = context;
            this.useLocation = useLocation;
        }
    }

    Stack<ContextInfo> buffStack = new Stack<ContextInfo>();
    string Indent() { return new string(' ', buffStack.Count * 4); }
%}

%%

/* Scanner body */

{Number}		{ GetNumber(); return (int)Token.CONSTANT; }

"#CCHK#IF"         { return (int)Token.PRECONST; }
\&\&			{ return (int)Token.logand; }
\|\|			{ return (int)Token.logor; }
{PER}           { return (int)Token.PER; }
{CP_EQ}			{ return (int)GetEqop(); }
{CP_NE}			{ return (int)GetEqop(); }
"!="			{ return (int)GetEqop(); }
\>			    { return (int)GetCompop(); }
\>\=			{ return (int)GetCompop(); }
\<\=			{ return (int)GetCompop(); }
\<			    { return (int)GetCompop(); }
{OP_ADD}		{ return (int)GetAddop(); }
{OP_SUB}		{ return (int)GetAddop(); }
{OP_MUL}		{ return (int)Token.OP_MUL; }
{OP_DIV}		{ return (int)Token.OP_DIV; }
".*."			{ return (int)Token.OP_SMUL; }
"./."			{ return (int)Token.OP_SDIV; }
".MOD."			{ return (int)Token.OP_SMOD; }
".<<."			{ return (int)GetSshiftop(); }
".>>."			{ return (int)GetSshiftop(); }
".>."			{ return (int)GetScompop(); }
".<."			{ return (int)GetScompop(); }
".>=."			{ return (int)GetScompop(); }
".<=."			{ return (int)GetScompop(); }
{P_OPEN}		{ return (int)Token.P_OPEN; }
{P_CLOSE}		{ nextBraceIsArray = false; return (int)Token.P_CLOSE; }
{B_OPEN}		{
                    int result;
                    result = nextBraceIsArray ? (int)Token.AB_OPEN : (int)Token.B_OPEN;
                    nextBraceIsArray = false;
                    return result;
                }
{B_CLOSE}		{ nextBraceIsArray = true; return (int)Token.B_CLOSE; }
{BR_OPEN}		{ return (int)Token.BR_OPEN; }
{BR_CLOSE}		{ return (int)Token.BR_CLOSE; }
{F_OPEN}		{ return (int)Token.F_OPEN; }
{F_CLOSE}		{ return (int)Token.F_CLOSE; }
{OP_EQ}			{ nextBraceIsArray = false; return (int)Token.OP_EQ; }
{COMMA}			{ return (int)Token.COMMA; }
{COLON}			{ nextBraceIsArray = false; return (int)Token.COLON; }
{QUESTION}		{ return (int)Token.QUESTION; }
{SC}			{ nextBraceIsArray = false; return (int)Token.SC; }
{OP_LSHIFT}		{ return (int)GetShiftop(); }
{OP_RSHIFT}		{ return (int)GetShiftop(); }
\&			    { return (int)Token.OP_AMP; }
"++"			{ return (int)GetIncDecOp(); }
"--"			{ return (int)GetIncDecOp(); }
"+="|"-="|"*="|"/="	{ yylval.op = yytext; return (int)Token.assignop; }

{Identifier}	{ nextBraceIsArray = true; return (int)GetIdentifier(); }

{Space}+		{ nextBraceIsArray = false; }

{STRFUNC}       { yylval.symbol = yytext; return (int)Token.STRFUNC; }
{EXC}           { yylval.symbol = "!"; return (int)Token.EXC; }

{Eol}           { LocationNextLine(); nextBraceIsArray = false; }


/* #INCLUDE */

^#[Ii][Nn][Cc][Ll][Uu][Dd][Ee]                  BEGIN(INCL);

<INCL>{Eol}                  BEGIN(INITIAL); TryInclude(null);
<INCL>[ \t]                  /* skip whitespace */
<INCL>[^ \t]{dotchr}*        BEGIN(INITIAL); TryInclude(yytext);      

/* #IF #ELSE #ENDIF */
{PREIF}[ \t]+{dotchr}*       {
        if(CheckConst(yytext)){
            BEGIN(INITIAL);
        } else {
            BEGIN(PREIFSKIP);
        };
        }

{PREEND}                    { BEGIN(SKIPEOL); }
{PREELSE}                   { BEGIN(PREIFSKIP); }

<PREIFSKIP>[^\n]            {}
<PREIFSKIP>\n               { LocationNextLine(); }
<PREIFSKIP>{PREELSE}        BEGIN(SKIPEOL);
<PREIFSKIP>{PREEND}         BEGIN(SKIPEOL);

<SKIPEOL>[^\n]      {}
<SKIPEOL>\n         { BEGIN(INITIAL); LocationNextLine(); }

/* #ASM */
{ASM}{Space}*{Eol}  {
                      lexStrBuffer = new StringBuilder();
                      BEGIN(ASM);
                    }
<ASM>[^\n]          {
                       lexStrBuffer.Append(yytext);
                    }
<ASM>\n             { lexStrBuffer.Append("\n"); LocationNextLine(); }
<ASM><<EOF>>        yyerror("EOF in #ASM");
<ASM>{PREEND}       {
                        BEGIN(INITIAL);
                        yylval.str = lexStrBuffer.ToString();
                        return (int)Token.PLAIN;
                    }

/* Comment */
{CMTSTART}          BEGIN(COMMENT);
<COMMENT>[^\n]      {}
<COMMENT>\n         { LocationNextLine(); }
<COMMENT><<EOF>>    yyerror("EOF in comment");
<COMMENT>{CMTEND}   BEGIN(INITIAL);

/* Line Comment */
"//"                BEGIN(LINECOMMENT);
<LINECOMMENT>[^\n]  {}
<LINECOMMENT>\n     { BEGIN(INITIAL); LocationNextLine(); }

/* Char(CONST) */
"\'"                {
                        lexStrBuffer = new StringBuilder();
                        BEGIN(CHARCONST);
                    }
<CHARCONST>\n       {
                        yyerror("Unterminated char");       
                        lexStrBuffer.Clear();
                        BEGIN(INITIAL);
                        LocationNextLine();
                    }
<CHARCONST><<EOF>>  {
                        yyerror("EOF in string");       
                        lexStrBuffer.Clear();
                        BEGIN(INITIAL);
                    }
<CHARCONST>[^\\\n'] {
                        lexStrBuffer.Append(yytext);
                    }
<CHARCONST>"\'"     {
                        BEGIN(INITIAL);
                        GetChar(lexStrBuffer.ToString());
                        return (int)Token.CONSTANT;
                    }

/* String */
"\""                { 
                        lexStrBuffer = new StringBuilder();
                        BEGIN(STRING);
                    }
<STRING>\n          {
                        yyerror("Unterminated string");       
                        lexStrBuffer.Clear();
                        BEGIN(INITIAL);
                        LocationNextLine();
                    }
<STRING><<EOF>>     {
                        yyerror("EOF in string");       
                        lexStrBuffer.Clear();
                        BEGIN(INITIAL);
                    }
<STRING>[^\\\n"]    {
                        lexStrBuffer.Append(yytext);
                    }
<STRING>\\\n        {}
<STRING>\\{hex}     {
                        {
                        int value = Convert.ToInt32(yytext.Substring(2), 16);
                        lexStrBuffer.Append((char)value);
                        }
                    }
<STRING>\\{oct}     {
                        {
                        int value = Convert.ToInt32(yytext.Substring(2), 8);
                        lexStrBuffer.Append((char)value);
                        }
                    }
<STRING>\\[^\n]|<CHARCONST>\\[^\n]   {
                    switch(yytext[yyleng-1]){
                    case 'n' :
                    case '/' : lexStrBuffer.Append((char)0x0d);
                                // TODO LSX-Dodgersではこれを有効にしないと駄目
                               //lexStrBuffer.Append((char)0x0a);
                               break;

                    case 'C' :
                    case 'c' : lexStrBuffer.Append((char)0x0c);  
                               break;

                    case 'R' :
                    case 'r' :
                               lexStrBuffer.Append((char)0x1c);
                               //lexStrBuffer.Append('\r');
                               break;

                    case 'L' :
                    case 'l' : lexStrBuffer.Append((char)0x1d);
                               break;

                    case 'U' :
                    case 'u' : lexStrBuffer.Append((char)0x1e);
                               break;

                    case 'D' :
                    case 'd' : lexStrBuffer.Append((char)0x1f);
                               break;
                    default  : lexStrBuffer.Append(yytext[yyleng-1]);
                    break;
                    }
                  }
<STRING>"\""        {
                        BEGIN(INITIAL);
                        yylval.str = lexStrBuffer.ToString();
                        return (int)Token.STRING;
                    }
%%
    void LocationAddColumn(int cnt)
    {
        yylloc = new QUT.Gppg.LexLocation(yylloc.StartLine, yylloc.StartColumn, yylloc.EndLine, yylloc.EndColumn + cnt);
    }

    void LocationNextLine()
    {
        yylloc = new QUT.Gppg.LexLocation(yylloc.StartLine, yylloc.StartColumn, yylloc.EndLine + 1, 0);
    }

    void LocationNext()
    {
    yylloc = new QUT.Gppg.LexLocation(yylloc.EndLine, yylloc.EndColumn + 1, yylloc.EndLine, yylloc.EndColumn );
    }

    void PushLocation()
    {
    locationStack.Push(new LocationInfo(currentFileName, yylloc));
    }

    void LocationInit()
    {
        yylloc = new QUT.Gppg.LexLocation(0, 0, 0, 0);
    }

    void PopLocation()
    {
        var info = locationStack.Pop();
        currentFileName = info.FileName;
        yylloc = info.Location;
    }

    private void StartPreIf(string exprStr)
    {
        if (string.IsNullOrEmpty(exprStr))
        {
            Console.Error.WriteLine("#IF, no CONST");
        }
        else 
        {
            BufferContext savedCtx = MkBuffCtx();

            byte[] inputBuffer = System.Text.Encoding.UTF8.GetBytes(exprStr + ";");
            MemoryStream stream = new MemoryStream(inputBuffer);
            var charsetDetectedResult = UtfUnknown.CharsetDetector.DetectFromStream(stream);
            stream.Position = 0;
            SetSource(stream, CodePageHandling.GetCodePage(charsetDetectedResult.Detected.EncodingName));
            buffStack.Push(new ContextInfo(savedCtx, false));
        }
    }

    private void TryInclude(string fName)
    {
        if (string.IsNullOrEmpty(fName))
            Console.Error.WriteLine("#INCLUDE, no filename");
        else 
            try {
                if(fName[0] == '"')
                {
                  fName = fName.Trim('"');
                }
                BufferContext savedCtx = MkBuffCtx();
                var stream = new FileStream(fName, FileMode.Open);
                var charsetDetectedResult = UtfUnknown.CharsetDetector.DetectFromStream(stream);
                stream.Position = 0;
                SetSource(stream, CodePageHandling.GetCodePage(charsetDetectedResult.Detected.EncodingName));
                Console.WriteLine("; include {0}", fName);
                buffStack.Push(new ContextInfo(savedCtx, true)); // Don't push until file open succeeds!
                PushLocation();
                LocationInit();
                currentFileName = fName;
            }
            catch
            {
                Console.Error.WriteLine("#include, could not open file \"{0}\"", fName);
            }
    }

    protected override bool yywrap()
    {
        if (buffStack.Count == 0) return true;
        var info = buffStack.Pop();
        RestoreBuffCtx(info.context);
        if(info.useLocation)
        {
          PopLocation();
        }
        return false;
    }
