%namespace SLANGCompiler.SLANG
%partial
%parsertype SLANGParser
%visibility internal
%tokentype Token

%union { 
       public ConstInfo constValue;
       public Expr expr;
       public string symbol;
       public Tree tree;
       public TypeInfo typeInfo;

       // Constant
       public string op;
       public int value; 
       public string str; 

       // if
       public int label;

       // for
       public ForInfo forInfo;
}

%start file

%token <symbol>      IDENTIFIER STRFUNC
%token <constValue>  CONSTANT
%token <str>         STRING PLAIN
%token <symbol> EXC
%token               VAR BYTE WORD FLOAT ARRAY CONST PER
%token IF THEN ELSE ELIF ENDIF
%token WHILE DO WEND REPEAT UNTIL CASE OTHERS OF LOOP
%token FOR TO DOWNTO NEXT
%token EXIT CONTINUE RETURN
%token GOTO
%token ORG WORK OFFSET MACHINE
%token PRINT CODE

%token BEGIN END
%token B_OPEN B_CLOSE F_OPEN F_CLOSE BR_OPEN BR_CLOSE AB_OPEN
%token COLON SC
%token PRECONST

%token <op> addop shiftop sshiftop compop scompop eqop
%token <op> incdecop assignop
%token OP_AMP

%left COMMA
%right OP_EQ assignop
%right QUESTION COLON       // conditional ? :
%left logor
%left logand
%left OP_OR
%left OP_XOR
%left OP_AND
%left eqop
%left compop scompop
%left shiftop sshiftop
%left addop HIGH LOW CPL NOT
%left OP_MUL OP_DIV OP_MOD OP_SMUL OP_SDIV OP_SMOD
%left P_OPEN P_CLOSE
%right unop
%nonassoc incdecop

%type <tree> declaration
%type <tree> declarator_list declarator declarator2 func_declarator func_declarator_list func_head_decl
%type <tree> const_list const
%type <tree> param_list param_decl
%type <tree> expr_list str_expr_list code_expr_list
%type <expr> primary nc_expr expr func_body func_end nc_str_expr nc_code_expr case_stmt_head
%type <label> then_part then_head then_part_list while_head repeat_head loop_head case_head
%type <forInfo> for_head
%type <op> for_to_or_downto

%%

file
       : def
       | file def
       | PRECONST { SetConstMode(true); } nc_expr { setConstExpr($3); SetConstMode(false); }
       ;
def
       : func_def
       | declaration { globalDataDecl($1); }
       | ORG nc_expr { SetOrg($2); }
       | WORK nc_expr { SetWork($2); }
       | OFFSET nc_expr { SetOffset($2); }
       | PLAIN { procPlainString($1); }
       | sc
       ;

func_def
       : func_head_decl
              {
                     funcDef($1);
                     initFunc();
                     contLabel = 0;
                     breakLabel = 0;
              }
          func_body
              {
                     funcend($3);
                     endFunc();
              }
       ;
func_body
       : static_declaration_list func_begin declaration_list  { funchead(); } stmt_list func_end { $$ = $6; }
       ;

declaration_list
       : /* empty */
       | declaration_list declaration { yyerrok(); localDataDecl($2, true); }
       ;

static_declaration_list
       : /* empty */
       | static_declaration_list declaration { yyerrok(); localDataDecl($2, false); }
       ;

func_begin
       : begin
       ;

func_end
       : end sc { $$ = null; }
       | end { $$ = null; }
       | end P_OPEN expr P_CLOSE sc { $$ = $3; }
       ;

stmt_list
       : /*empty */
       | stmt_list stmt
       ;

stmt
       : compound_stmt
       | PLAIN { procPlainString($1); }
       | expr sc  { expstmt($1); resetHeap(); }
       | IF {
              int label = genNewLabel(); pushIfLabel(label);
            }
              then_part_list
              else_part
              endif
              {
                     int label = popIfLabel();
                     genlabel(label);
                     var elseLabel = popElseLabel();
                     if(elseLabel >= 0)genlabel(elseLabel);
              }
       | loop_head stmt
              {
                     genjump($1);
                     genlabel($1 + 1);
                     popLabels();
              }
       | while_head compound_while_stmt
       {
              genjump($1);
              genlabel($1 + 1);
              popLabels();
       }
       | repeat_head until_stmt UNTIL expr sc
              {
                     genbool(enBool($4), 0, $1);
                     genlabel($1 + 1);
                     popLabels();
              }
       | EXIT sc
              {
                     if (breakLabel == 0)
                     {
                            Error("break outside loop");
                     }
                     genjump(breakLabel);
              }
       | EXIT P_OPEN expr P_CLOSE sc
              {
                     if(!$3.IsConst())
                     {
                            Error("exit param must be const.");
                     }
                     int brkLabel = peekBreakLabel($3.ConstValue.Value - 1);
                     Console.WriteLine("brkLabel:" + brkLabel);
                     if(brkLabel >= 0)
                     {
                            genjump(brkLabel);
                     } else {
                            Error("could not exit outside loop");
                     }
              }
       | CONTINUE sc
              {
                     if (contLabel == 0)
                     {
                            Error("continue outside loop");
                     }
                     genjump(contLabel);
              }
       | IDENTIFIER COLON
              {
                     genStringLabel($1);
              }
       | GOTO IDENTIFIER
              {
                     genGoto($2);
              }
       | EXIT TO IDENTIFIER
              {
                     genGoto($3);
              }
       | RETURN sc
              {
                     genjump(exitLabel);
              }
       | RETURN expr sc
              {
                     genexp(coerce($2, OperatorType.Word));
                     genjump(exitLabel);
              }
       | PRINT P_OPEN str_expr_list P_CLOSE { genPrint($3); }
       | case_head begin case_stmt_list end {
              doCaseEnd();
              genlabel($1 + 1);
              popLabels();
       }
       | for_head for_stmt
              {
                     var forIdentifier = $1.Expr;
                     var forLabel = $1.Label;
                     var forOp = $1.Op;
                     var forExpr = $1.CheckExpr;

                     genlabel($1.Label + 1);
                     // 1足す
                     Expr one = makeNode1(Opcode.Const, OperatorType.Constant, TypeInfo.WordTypeInfo, null);
                     one.ConstValue = new ConstInfo(1);
                     genexp(expIncdec(forOp == "TO" ? Opcode.PostInc : Opcode.PostDec, forIdentifier));

                     genForloop(forOp, forIdentifier, forExpr, $1.Label);

                     genlabel($1.Label + 2);
                     popLabels();
              }
       | sc
       ;

case_head
       : CASE expr of {
              int label;
              $$ = label = genNewLabel(); genNewLabel();
              pushLabels();
              breakLabel = label + 1;
              contLabel = label;
              genlabel(label);

              doCaseHead($2);
       }
       ;

of
       : OF
       | /* empty */
       ;

case_stmt_list
       :
       | case_stmt_list case_stmt
       ;

case_stmt
       : case_stmt_head COLON { doCase($1); } stmt 
       | OTHERS COLON { doCase(null); } stmt 
       | case_stmt_head { doCase($1); } stmt 
       | OTHERS { doCase(null); } stmt 
       | sc
       ;

case_stmt_head
       : expr { $$ = $1; }
       | nc_expr TO nc_expr { $$ = makeNode2(Opcode.Range, OperatorType.Word, TypeInfo.WordTypeInfo, $1, $3);  }
       ;

repeat_head
       : REPEAT {
              int label;
              $$ = label = genNewLabel(); genNewLabel();
              pushLabels();
              breakLabel = label + 1;
              contLabel = label;
              genlabel(label);
       }
       ;

until_stmt
       : begin stmt_list end
       | {} stmt
       ;

for_stmt
       : for_begin stmt_list for_end
       | stmt NEXT sc
       | stmt
       ;

for_head
       : FOR IDENTIFIER OP_EQ nc_expr for_to_or_downto nc_expr
       {
              $$ = new ForInfo();
              int label;

              $$.Label = label = genNewLabel(); genNewLabel(); genNewLabel();
              $$.Expr = expIdent($2);
              $$.CheckExpr = $6;
              $$.Op = $5;

              pushLabels();
              breakLabel = label + 2;
              contLabel  = label + 1;
              genexp(expAssign(expIdent($2), $4));
              genlabel(label);
              resetHeap();
       }
       ;

for_to_or_downto
       : TO  { $$ = "TO"; }
       | DOWNTO { $$ = "DOWNTO"; }
       ;

loop_head
       : LOOP {
              int label;
              $$ = label = genNewLabel() ; genNewLabel();
              pushLabels();
              breakLabel = label + 1; contLabel = label;
              genlabel(label);
              }
       ;

while_head
       : WHILE expr
       {
              int label;
              $$ = label = genNewLabel(); genNewLabel();
              pushLabels();
              breakLabel = label + 1; contLabel = label;
              genlabel(label);
              if($2.IsValueConst() && $2.ConstValue.Value != 0)
              {
                     // expr no check
              } else {
                     genbool(enBool($2), 0, label + 1);
              }
              resetHeap();
       }
       ;

then_part_list
       : then_part { genjump(peekIfLabel()); pushElseLabel($1);  $$ = $1; }
       | then_part_list elif then_part { genjump(peekIfLabel()); pushElseLabel($3); $$ = $3; }
       ;

then_part
       : then_head stmt sc { $$ = $1; }
       | then_head stmt { $$ = $1; }
       ;

then_head
       : expr then
              {
                     var elseLabel = popElseLabel();
                     if(elseLabel >= 0)
                     {
                            genlabel(elseLabel);
                     }
                     int label;
                     $$ = label = genNewLabel();
                     genbool(enBool($1), 0, label);
                     resetHeap();
              }
       ;

else_part
       :
       | ELSE { var elseLabel = popElseLabel(); if(elseLabel >= 0)genlabel(elseLabel); } stmt
       ;

elif
       : ELSE IF
       | ELIF
       ;

endif
       :
       | ENDIF
       ;

compound_stmt
       : begin { yyerrok(); }
         stmt_list
         end {}
       ;

begin
       : BEGIN
       | B_OPEN
       | P_OPEN
       | F_OPEN
       | BR_OPEN
       ;

end
       : END
       | B_CLOSE
       | P_CLOSE
       | F_CLOSE
       | BR_CLOSE
       ;


compound_while_stmt
       : wbegin { yyerrok(); }
         stmt_list
         wend
       | stmt
       ;

wbegin
       : DO
       | begin
       ;

wend
       : WEND
       | end
       ;

for_begin
       : DO
       | begin
       ;

for_end
       : end NEXT sc
       | end
       | NEXT sc
       ;

then
       : 
       | THEN
       ;

declaration
       : VAR declarator_list sc { $$ = Tree.CreateDecl($2); }
       | ARRAY declarator_list sc { $$ = Tree.CreateTree2(DeclNode.Array, $2); }
       | CONST const_list sc { $$ = Tree.CreateTree2(DeclNode.Const, $2); }
       | MACHINE func_declarator_list sc { $$ = Tree.CreateTree2(DeclNode.Machine, $2); }
       ;

const_list
       : const
       | const_list COMMA const
       ;

// CONSTは1つ1つ定義する(一行の中で定義したものを使用可能にするため……)
const
       : IDENTIFIER OP_EQ expr { $$ = DefineConst(Tree.CreateDeclIdentifier(DeclNode.Id, $1), $3); }
       | IDENTIFIER OP_EQ begin code_expr_list end { $$ = DefineConst(Tree.CreateDeclIdentifier(DeclNode.Id, $1), $4); }
       | IDENTIFIER error { Error("CONST required initial value"); }
       ;

declarator_list
       : declarator { $$ = $1; }
       | declarator_list COMMA declarator { $$ = $1.Append($3); }
       ;

func_declarator_list
       : func_declarator { $$ = Tree.CreateIdentifierTypeTree(TypeDataSize.Word, $1); }
       | func_declarator_list COMMA func_declarator { $$ = $1.Append(Tree.CreateIdentifierTypeTree(TypeDataSize.Word, $3)); }
       ;

declarator
       : byte_spec declarator2 { $$ = Tree.CreateIdentifierTypeTree(TypeDataSize.Byte, $2); }
       | byte_spec declarator2 OP_EQ begin code_expr_list end { $$ = Tree.CreateIdentifierTypeTree(TypeDataSize.Byte, $2.SetInitialValueCode($5)); }
       | byte_spec declarator2 OP_EQ expr { $$ = Tree.CreateIdentifierTypeTree(TypeDataSize.Byte, $2.UpdateIdentifier( null, $4)); }

       | word_spec declarator2 OP_EQ begin code_expr_list end { $$ = Tree.CreateIdentifierTypeTree(TypeDataSize.Word, $2.SetInitialValueCode($5)); }
       | word_spec declarator2 { $$ = Tree.CreateIdentifierTypeTree(TypeDataSize.Word, $2); }
       | word_spec declarator2 OP_EQ expr { $$ = Tree.CreateIdentifierTypeTree(TypeDataSize.Word, $2.UpdateIdentifier(null, $4)); }

       | float_spec declarator2 OP_EQ begin code_expr_list end { $$ = Tree.CreateIdentifierTypeTree(TypeDataSize.Float, $2.SetInitialValueCode($5)); }
       | float_spec declarator2 { $$ = Tree.CreateIdentifierTypeTree(TypeDataSize.Float, $2); }
       | float_spec declarator2 OP_EQ expr { $$ = Tree.CreateIdentifierTypeTree(TypeDataSize.Float, $2.UpdateIdentifier(null, $4)); }
       ;

// 宣言のWORDは省略可能
word_spec
       :
       | WORD
       ;

byte_spec
       : BYTE
       | EXC
       ;

float_spec
       : FLOAT
       ;

declarator2
       : IDENTIFIER { $$ = Tree.CreateDeclIdentifier(DeclNode.Id, $1); }
       | declarator2 COLON expr { $$ = $1.UpdateIdentifier(createExprString($3)); } // Address
       | declarator2 AB_OPEN B_CLOSE { $$ = Tree.CreateArray($1, null);  } // Array or Indirect
       | declarator2 AB_OPEN expr B_CLOSE { $$ = Tree.CreateArray($1, $3);  } // Array(with size)
       ;

func_head_decl
       : declarator2 P_OPEN P_CLOSE { $$ = Tree.CreateIdentifierTypeTree(TypeDataSize.Word, Tree.CreateTree3(DeclNode.Func, $1, null )); } // Function
       | declarator2 P_OPEN param_list P_CLOSE { $$ = Tree.CreateIdentifierTypeTree(TypeDataSize.Word, Tree.CreateTree3(DeclNode.Func, $1, $3 )); } // Function(with param)
       | declarator2 P_OPEN CONSTANT P_CLOSE { $$ = Tree.CreateIdentifierTypeTree(TypeDataSize.Word, Tree.CreateTreeExpr(DeclNode.Func, $1, expConst($3,TypeDataSize.Byte))); } // Machine Function(with param count)
       ;

func_declarator
       : IDENTIFIER { $$ = Tree.CreateDeclIdentifier(DeclNode.Id, $1); }
       | func_declarator COLON expr { $$ = $1.UpdateIdentifier(createExprString($3)); }
       | func_declarator P_OPEN P_CLOSE { $$ = Tree.CreateFuncDecl($1, null ); }
       | func_declarator P_OPEN expr P_CLOSE { $$ = Tree.CreateFuncDecl($1, $3 ); }
       ;

param_list
       : param_decl { $$ = Tree.CreateTree3(DeclNode.Dummy, $1, null); }
       | param_list COMMA param_decl { $$ = $1.Append(Tree.CreateTree3(DeclNode.Dummy, $3, null)); }
       ;

param_decl
       : declarator { $$ = Tree.CreateTree3(DeclNode.Dummy, $1, null); }
       ;

nc_expr : primary    { $$ = $1; }
       | PER nc_expr { $$ = coerce($2, OperatorType.Word); }
       | CODE P_OPEN code_expr_list P_CLOSE { $$ = expCode($3); }
       | OP_AMP nc_expr %prec unop { $$ = expAddrof($2); }
       | HIGH nc_expr %prec unop { $$ = expHighlow( Opcode.High, $2); }
       | LOW nc_expr %prec unop { $$ = expHighlow( Opcode.Low, $2); }
       | NOT nc_expr %prec unop { $$ = expUnary( Opcode.Not, $2); }
       | CPL nc_expr %prec unop { $$ = expUnary( Opcode.Cpl, $2); }
       | incdecop nc_expr { $$ = expIncdec($1 =="++" ? Opcode.PreInc : Opcode.PreDec, $2); }
       | nc_expr incdecop { $$ = expIncdec($2 =="++" ? Opcode.PostInc : Opcode.PostDec, $1); }
       | addop nc_expr %prec unop { $$ = expUnary( $1 == "+" ? Opcode.Plus : Opcode.Minus, $2 ); }
           | nc_expr addop nc_expr		{ $$ = expAddsub( $2 == "+" ? Opcode.Add : Opcode.Sub, $1, $3 ); }
           | nc_expr shiftop nc_expr	{ $$ = expShiftop( $2 == "<<" ? Opcode.Shl : Opcode.Shr, $1, $3 ); }
           | nc_expr sshiftop nc_expr	{ $$ = expShiftop( $2 == ".<<." ? Opcode.SShl : Opcode.SShr, $1, $3 ); }
           | nc_expr OP_MUL nc_expr		{ $$ = expBinary( Opcode.Mul, $1, $3 ); }
           | nc_expr OP_DIV nc_expr		{ $$ = expBinary( Opcode.Div, $1, $3 ); }
           | nc_expr OP_MOD nc_expr		{ $$ = expBinary( Opcode.Mod, $1, $3 ); }
           | nc_expr OP_SMUL nc_expr		{ $$ = expSBinary( Opcode.SMul, $1, $3 ); }
           | nc_expr OP_SDIV nc_expr		{ $$ = expSBinary( Opcode.SDiv, $1, $3 ); }
           | nc_expr OP_SMOD nc_expr		{ $$ = expSBinary( Opcode.SMod, $1, $3 ); }
           | nc_expr OP_AND nc_expr		{ $$ = expBinary( Opcode.And, $1, $3 ); }
           | nc_expr OP_OR nc_expr		       { $$ = expBinary( Opcode.Or, $1, $3 ); }
           | nc_expr OP_XOR nc_expr		{ $$ = expBinary( Opcode.Xor, $1, $3 ); }
           | nc_expr OP_EQ nc_expr               { $$ = expAssign($1, $3); }
           | nc_expr assignop nc_expr            { $$ = expAssignOp($2, $1, $3); }
           | nc_expr eqop nc_expr                { $$ = expCompare($2 == "==" ? Opcode.Eq : Opcode.Neq, $1, $3); }
           | nc_expr compop nc_expr              { 
              Opcode op = Opcode.Eq;
              switch($2)
              {
                     case "==": op = Opcode.Eq; break;
                     case "<>": op = Opcode.Neq; break;
                     case "!=": op = Opcode.Neq; break;
                     case ">": op = Opcode.Gt; break;
                     case ">=": op = Opcode.Ge; break;
                     case "<": op = Opcode.Lt; break;
                     case "<=": op = Opcode.Le; break;
              }
              $$ = expCompare(op, $1, $3);
            }
           | nc_expr scompop nc_expr              { 
              Opcode op = Opcode.SGt;
              switch($2)
              {
                     case ".>.": op = Opcode.SGt; break;
                     case ".>=.": op = Opcode.SGe; break;
                     case ".<.": op = Opcode.SLt; break;
                     case ".<=.": op = Opcode.SLe; break;
              }
              $$ = expCompare(op, $1, $3);
            }
           | nc_expr logand nc_expr              { $$ = expLogop(Opcode.Land, $1, $3); }
           | nc_expr logor nc_expr              { $$ = expLogop(Opcode.Lor, $1, $3); }
           | nc_expr QUESTION nc_expr COLON nc_expr { $$ = expConditional($1, $3, $5); }
           ;
expr
       : nc_expr { $$ = $1; }
       | expr COMMA nc_expr { $$ = expComma($1, $3); }
       ;

primary : 
         IDENTIFIER                                             { $$ = expIdent($1); }
       | CONSTANT                                               { $$ = expConst($1,TypeDataSize.Byte); }
       | STRING                                                 { $$ = expString($1); }
       | primary AB_OPEN expr B_CLOSE { $$ = expArray($1, $3); }
       | P_OPEN nc_expr P_CLOSE           { $$ = $2; }
       | primary P_OPEN expr_list P_CLOSE { $$ = expFuncall($1, $3); }
       | primary P_OPEN P_CLOSE { $$ = expFuncall($1, null); }
       ;

expr_list
       : nc_expr { $$ = Tree.CreateTreeExpr(DeclNode.Dummy, null, $1 ); }
       | expr_list COMMA nc_expr { yyerrok(); $$ = Tree.CreateTreeExpr(DeclNode.Dummy, $1, $3 ); }
       ;

nc_str_expr
       : OP_DIV %prec unop {
              $$ = expStrFuncall("/", null);
        } 
       | STRFUNC P_OPEN expr_list P_CLOSE { $$ = expStrFuncall($1, $3);}
       | EXC P_OPEN expr_list P_CLOSE { $$ = expStrFuncall($1, $3);}
       | PER P_OPEN expr_list P_CLOSE { $$ = expStrFuncall("%", $3);}
       | nc_expr
       ;

str_expr_list
       : nc_str_expr { $$ = Tree.CreateTreeExpr(DeclNode.Dummy, null, $1 ); }
       | str_expr_list COMMA nc_str_expr { yyerrok(); $$ = Tree.CreateTreeExpr(DeclNode.Dummy, $1, $3 ); }
       ;

nc_code_expr
       : nc_expr
       | B_OPEN nc_expr B_CLOSE { $$ = makeNode1(Opcode.CodeExpr, OperatorType.Word, TypeInfo.WordTypeInfo, $2); }
       | STRING { $$ = expString($1); }
       | compop IDENTIFIER compop { if($1 == "<" && $3 == ">") { $$ = expLabel($2); } }
       ;

code_expr_list
       : nc_code_expr { $$ = Tree.CreateTreeExpr(DeclNode.Dummy, null, $1 ); }
       | code_expr_list COMMA nc_code_expr { yyerrok(); $$ = Tree.CreateTreeExpr(DeclNode.Dummy, $1, $3 ); }
       ;
sc
       : SC { yyerrok(); }
       ;
%%