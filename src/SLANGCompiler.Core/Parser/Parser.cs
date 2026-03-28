using SLANGCompiler.Lexer;
using SLANGCompiler.Parser.Ast;

namespace SLANGCompiler.Parser;

/// <summary>
/// SLANG再帰下降パーサー。
/// トークン列からASTを構築する。
/// </summary>
public class Parser
{
    private readonly List<Token> _tokens;
    private readonly DiagnosticBag _diagnostics;
    private int _pos;

    public Parser(List<Token> tokens, DiagnosticBag diagnostics)
    {
        _tokens = tokens;
        _diagnostics = diagnostics;
        _pos = 0;
    }

    // ---- Token access ----

    private Token Current => _pos < _tokens.Count ? _tokens[_pos] : new Token(TokenKind.EOF, "", SourceSpan.Unknown);
    private Token Peek(int offset = 0)
    {
        int idx = _pos + offset;
        return idx < _tokens.Count ? _tokens[idx] : new Token(TokenKind.EOF, "", SourceSpan.Unknown);
    }

    private Token Advance()
    {
        var token = Current;
        _pos++;
        return token;
    }

    private bool Check(TokenKind kind) => Current.Kind == kind;
    private bool CheckAny(params TokenKind[] kinds) => kinds.Contains(Current.Kind);

    private bool Match(TokenKind kind)
    {
        if (Current.Kind == kind) { Advance(); return true; }
        return false;
    }

    private Token Expect(TokenKind kind, string? message = null)
    {
        if (Current.Kind == kind) return Advance();
        var msg = message ?? $"Expected {kind}, got {Current.Kind}";
        _diagnostics.Error(msg, Current.Span);
        return Current; // error recovery: don't advance
    }

    private void ExpectSemicolon() => Expect(TokenKind.Semicolon, "Expected ';'");

    // ---- Top-level ----

    public CompilationUnit ParseCompilationUnit()
    {
        var start = Current.Span;
        var definitions = new List<AstNode>();

        while (!Check(TokenKind.EOF))
        {
            // Skip stray semicolons
            if (Match(TokenKind.Semicolon)) continue;

            var def = ParseDefinition();
            if (def != null)
                definitions.Add(def);
            else
                Advance(); // error recovery
        }

        return new CompilationUnit(definitions, start);
    }

    private AstNode? ParseDefinition()
    {
        switch (Current.Kind)
        {
            case TokenKind.Org:
                return ParseOrgDirective();
            case TokenKind.Work:
                return ParseWorkDirective();
            case TokenKind.Offset:
                return ParseOffsetDirective();
            case TokenKind.Module:
                return ParseModuleBlock();
            case TokenKind.Plain:
                return ParsePlainAsm();
            case TokenKind.Var:
            case TokenKind.Byte:
            case TokenKind.Word:
            case TokenKind.Float:
                return ParseVarDeclaration();
            case TokenKind.Array:
                return ParseArrayDeclaration();
            case TokenKind.Const:
                return ParseConstDeclaration();
            case TokenKind.Machine:
                return ParseMachineDeclaration();
            default:
                // Could be a function definition or expression
                if (Current.Kind == TokenKind.Identifier)
                {
                    return ParseFuncDefOrDecl();
                }
                _diagnostics.Error($"Unexpected token: {Current.Kind}", Current.Span);
                return null;
        }
    }

    // ---- Directives ----

    private OrgDirective ParseOrgDirective()
    {
        var start = Advance().Span; // ORG
        var value = ParseExpression();
        return new OrgDirective(value, start);
    }

    private WorkDirective ParseWorkDirective()
    {
        var start = Advance().Span; // WORK
        var value = ParseExpression();
        return new WorkDirective(value, start);
    }

    private OffsetDirective ParseOffsetDirective()
    {
        var start = Advance().Span; // OFFSET
        var value = ParseExpression();
        return new OffsetDirective(value, start);
    }

    private ModuleBlock ParseModuleBlock()
    {
        var start = Advance().Span; // MODULE
        var name = ParseExpression();
        var defs = new List<AstNode>();
        // Module ends at MODULEEND (which the lexer produces from #MODULE...#END)
        // For now, treat as simple directive
        return new ModuleBlock(name, defs, start);
    }

    private PlainAsm ParsePlainAsm()
    {
        var token = Advance();
        return new PlainAsm(token.StringValue, token.Span);
    }

    // ---- Declarations ----

    private AstNode ParseVarDeclaration()
    {
        var start = Current.Span;
        DataSize size = DataSize.Word;

        // Optional type prefix (VAR keyword is also optional before BYTE/WORD/FLOAT)
        if (Match(TokenKind.Var))
        {
            // Check for type specifier after VAR
            size = ParseOptionalDataSize();
        }
        else
        {
            size = ParseDataSize();
        }

        var name = Expect(TokenKind.Identifier, "Expected variable name").StringValue;
        Expression? address = null;
        Expression? initValue = null;
        List<Expression>? initCode = null;

        // :address
        if (Match(TokenKind.Colon))
        {
            address = ParseExpression();
        }

        // Check for array dimensions
        var dims = new List<Expression?>();
        while (Check(TokenKind.ArrayBracketOpen))
        {
            Advance();
            if (Check(TokenKind.RBracket))
            {
                dims.Add(null); // unsized
            }
            else
            {
                dims.Add(ParseExpression());
            }
            Expect(TokenKind.RBracket, "Expected ']'");
        }

        // = initializer
        if (Match(TokenKind.Eq))
        {
            if (IsBlockOpen())
            {
                initCode = ParseCodeBlock();
            }
            else
            {
                initValue = ParseExpression();
            }
        }

        ExpectSemicolon();

        if (dims.Count > 0)
        {
            return new ArrayDecl(name, size, address, dims, initValue, initCode, start);
        }
        return new VarDecl(name, size, address, initValue, initCode, start);
    }

    private AstNode ParseArrayDeclaration()
    {
        var start = Advance().Span; // ARRAY
        DataSize size = ParseOptionalDataSize();

        var name = Expect(TokenKind.Identifier, "Expected array name").StringValue;
        Expression? address = null;

        // :address
        if (Match(TokenKind.Colon))
        {
            address = ParseExpression();
        }

        // Dimensions
        var dims = new List<Expression?>();
        while (Check(TokenKind.ArrayBracketOpen))
        {
            Advance();
            if (Check(TokenKind.RBracket))
            {
                dims.Add(null);
            }
            else
            {
                dims.Add(ParseExpression());
            }
            Expect(TokenKind.RBracket, "Expected ']'");
        }

        Expression? initValue = null;
        List<Expression>? initCode = null;

        if (Match(TokenKind.Eq))
        {
            if (IsBlockOpen())
            {
                initCode = ParseCodeBlock();
            }
            else
            {
                initValue = ParseExpression();
            }
        }

        ExpectSemicolon();
        return new ArrayDecl(name, size, address, dims, initValue, initCode, start);
    }

    private AstNode ParseConstDeclaration()
    {
        var start = Advance().Span; // CONST
        bool isAsm = Match(TokenKind.Machine); // ASM CONST → uses EQU
        // Actually, check for ASM keyword before identifier
        // In original: ASM IDENTIFIER = expr

        var name = Expect(TokenKind.Identifier, "Expected constant name").StringValue;
        Expect(TokenKind.Eq, "Expected '='");

        Expression value;
        if (IsBlockOpen())
        {
            var codes = ParseCodeBlock();
            value = new CodeExpr(codes, start);
        }
        else
        {
            value = ParseExpression();
        }

        ExpectSemicolon();
        return new ConstDecl(name, value, isAsm, start);
    }

    private AstNode ParseMachineDeclaration()
    {
        var start = Advance().Span; // MACHINE
        var name = Expect(TokenKind.Identifier, "Expected function name").StringValue;
        Expression? address = null;
        int? paramCount = null;

        if (Match(TokenKind.Colon))
        {
            address = ParseExpression();
        }

        if (Match(TokenKind.LParen))
        {
            if (Check(TokenKind.IntegerLiteral))
            {
                paramCount = Current.IntValue;
                Advance();
            }
            Expect(TokenKind.RParen, "Expected ')'");
        }

        ExpectSemicolon();
        return new MachineDecl(name, address, paramCount, start);
    }

    // ---- Function definitions ----

    private AstNode ParseFuncDefOrDecl()
    {
        // Look ahead to determine if this is a function definition
        // func_head: identifier ['[' ... ']'] '(' ... ')'
        // If followed by a block, it's a function definition
        var start = Current.Span;
        var name = Expect(TokenKind.Identifier).StringValue;
        Expression? address = null;

        if (Match(TokenKind.Colon))
        {
            address = ParseExpression();
        }

        if (!Check(TokenKind.LParen))
        {
            // Not a function - treat as expression statement
            // Rewind and parse as expression
            // For now, simple error
            _diagnostics.Error($"Unexpected identifier at top level: {name}", start);
            return new PlainAsm("", start);
        }

        Advance(); // (
        var parameters = new List<ParamDecl>();
        if (!Check(TokenKind.RParen))
        {
            parameters = ParseParameterList();
        }
        Expect(TokenKind.RParen, "Expected ')'");

        // Static declarations (before BEGIN)
        var staticDecls = new List<AstNode>();
        while (IsDeclarationStart())
        {
            var decl = ParseLocalDeclaration();
            if (decl != null) staticDecls.Add(decl);
        }

        // BEGIN
        ExpectBlockOpen();

        // Local declarations (after BEGIN)
        var localDecls = new List<AstNode>();
        while (IsDeclarationStart())
        {
            var decl = ParseLocalDeclaration();
            if (decl != null) localDecls.Add(decl);
        }

        // Statement list
        var stmts = ParseStatementList();
        var body = new Block(stmts, start);

        // END with optional return value
        Expression? returnValue = null;
        ExpectBlockClose();

        if (Match(TokenKind.LParen))
        {
            returnValue = ParseExpression();
            Expect(TokenKind.RParen, "Expected ')'");
        }

        Match(TokenKind.Semicolon);

        return new FuncDef(name, address, parameters, staticDecls, localDecls, body, returnValue, start);
    }

    private List<ParamDecl> ParseParameterList()
    {
        var parms = new List<ParamDecl>();
        do
        {
            var parm = ParseParamDecl();
            parms.Add(parm);
        } while (Match(TokenKind.Comma));
        return parms;
    }

    private ParamDecl ParseParamDecl()
    {
        var start = Current.Span;
        DataSize size = ParseOptionalDataSize();
        var name = Expect(TokenKind.Identifier, "Expected parameter name").StringValue;
        bool isArray = false;

        // Check for [] indicating array/pointer parameter
        if (Check(TokenKind.ArrayBracketOpen))
        {
            Advance();
            Expect(TokenKind.RBracket, "Expected ']'");
            isArray = true;
        }

        return new ParamDecl(name, size, isArray, start);
    }

    // ---- Statements ----

    private List<AstNode> ParseStatementList()
    {
        var stmts = new List<AstNode>();
        while (!IsBlockClose() && !Check(TokenKind.EOF))
        {
            if (Match(TokenKind.Semicolon)) continue;

            var stmt = ParseStatement();
            if (stmt != null)
                stmts.Add(stmt);
            else
                Advance(); // error recovery
        }
        return stmts;
    }

    private AstNode? ParseStatement()
    {
        switch (Current.Kind)
        {
            case TokenKind.If: return ParseIfStatement();
            case TokenKind.While: return ParseWhileStatement();
            case TokenKind.Repeat: return ParseRepeatStatement();
            case TokenKind.Loop: return ParseLoopStatement();
            case TokenKind.For: return ParseForStatement();
            case TokenKind.Case: return ParseCaseStatement();
            case TokenKind.Exit: return ParseExitStatement();
            case TokenKind.Continue: return ParseContinueStatement();
            case TokenKind.Return: return ParseReturnStatement();
            case TokenKind.Goto: return ParseGotoStatement();
            case TokenKind.Print: return ParsePrintStatement();
            case TokenKind.Plain: return ParsePlainAsm();
            case var k when IsBlockOpen():
                return ParseCompoundStatement();
            default:
                // Label or expression
                if (Current.Kind == TokenKind.Identifier && Peek(1).Kind == TokenKind.Colon)
                {
                    var label = Advance().StringValue;
                    Advance(); // :
                    return new LabelStmt(label, Current.Span);
                }
                return ParseExpressionStatement();
        }
    }

    private Block ParseCompoundStatement()
    {
        var start = Current.Span;
        ExpectBlockOpen();
        var stmts = ParseStatementList();
        ExpectBlockClose();
        return new Block(stmts, start);
    }

    private IfStmt ParseIfStatement()
    {
        var start = Advance().Span; // IF
        var branches = new List<(Expression Condition, AstNode Body)>();

        // First branch
        var cond = ParseExpression();
        Match(TokenKind.Then);
        var body = ParseStatementOrBlock();
        Match(TokenKind.Semicolon);
        branches.Add((cond, body));

        // ELIF branches
        while (Check(TokenKind.Elif) || (Check(TokenKind.Else) && Peek(1).Kind == TokenKind.If))
        {
            if (Match(TokenKind.Elif))
            {
                // ELIF
            }
            else
            {
                Advance(); // ELSE
                Advance(); // IF
            }
            cond = ParseExpression();
            Match(TokenKind.Then);
            body = ParseStatementOrBlock();
            Match(TokenKind.Semicolon);
            branches.Add((cond, body));
        }

        // ELSE
        AstNode? elseBody = null;
        if (Match(TokenKind.Else))
        {
            elseBody = ParseStatementOrBlock();
        }

        Match(TokenKind.EndIf);

        return new IfStmt(branches, elseBody, start);
    }

    private WhileStmt ParseWhileStatement()
    {
        var start = Advance().Span; // WHILE
        var cond = ParseExpression();
        var body = ParseWhileBody();
        return new WhileStmt(cond, body, start);
    }

    private AstNode ParseWhileBody()
    {
        if (Match(TokenKind.Do) || IsBlockOpen())
        {
            if (Current.Kind != TokenKind.Do) { /* block open already consumed in IsBlockOpen check */ }
            var stmts = ParseStatementList();
            if (Check(TokenKind.Wend))
                Advance();
            else
                ExpectBlockClose();
            return new Block(stmts, Current.Span);
        }
        return ParseStatementOrBlock();
    }

    private RepeatStmt ParseRepeatStatement()
    {
        var start = Advance().Span; // REPEAT
        var body = ParseStatementOrBlock();
        Expect(TokenKind.Until, "Expected 'UNTIL'");
        var cond = ParseExpression();
        ExpectSemicolon();
        return new RepeatStmt(body, cond, start);
    }

    private LoopStmt ParseLoopStatement()
    {
        var start = Advance().Span; // LOOP
        var body = ParseStatementOrBlock();
        return new LoopStmt(body, start);
    }

    private ForStmt ParseForStatement()
    {
        var start = Advance().Span; // FOR
        var varName = Expect(TokenKind.Identifier, "Expected variable").StringValue;
        Expect(TokenKind.Eq, "Expected '='");
        var from = ParseExpression();
        bool isDownTo = false;
        if (Match(TokenKind.DownTo))
            isDownTo = true;
        else
            Expect(TokenKind.To, "Expected 'TO' or 'DOWNTO'");
        var to = ParseExpression();
        var body = ParseForBody();
        return new ForStmt(varName, from, to, isDownTo, body, start);
    }

    private AstNode ParseForBody()
    {
        if (Match(TokenKind.Do) || IsBlockOpen())
        {
            var stmts = ParseStatementList();
            ExpectBlockClose();
            Match(TokenKind.Next);
            Match(TokenKind.Semicolon);
            return new Block(stmts, Current.Span);
        }
        var stmt = ParseStatementOrBlock();
        Match(TokenKind.Next);
        Match(TokenKind.Semicolon);
        return stmt;
    }

    private CaseStmt ParseCaseStatement()
    {
        var start = Advance().Span; // CASE
        var expr = ParseExpression();
        Match(TokenKind.Of);
        ExpectBlockOpen();

        var branches = new List<CaseBranch>();
        while (!IsBlockClose() && !Check(TokenKind.EOF))
        {
            if (Match(TokenKind.Semicolon)) continue;

            if (Match(TokenKind.Others))
            {
                Match(TokenKind.Colon);
                var body = ParseStatementOrBlock();
                branches.Add(new CaseBranch(null, null, body));
            }
            else
            {
                var val = ParseExpression();
                Expression? rangeEnd = null;
                if (Match(TokenKind.To))
                {
                    rangeEnd = ParseExpression();
                }
                Match(TokenKind.Colon);
                var body = ParseStatementOrBlock();
                branches.Add(new CaseBranch(val, rangeEnd, body));
            }
        }

        ExpectBlockClose();
        return new CaseStmt(expr, branches, start);
    }

    private ExitStmt ParseExitStatement()
    {
        var start = Advance().Span; // EXIT
        Expression? level = null;
        string? targetLabel = null;

        if (Match(TokenKind.To))
        {
            targetLabel = Expect(TokenKind.Identifier, "Expected label").StringValue;
        }
        else if (Match(TokenKind.LParen))
        {
            level = ParseExpression();
            Expect(TokenKind.RParen, "Expected ')'");
        }

        ExpectSemicolon();
        return new ExitStmt(level, targetLabel, start);
    }

    private ContinueStmt ParseContinueStatement()
    {
        var start = Advance().Span; // CONTINUE
        ExpectSemicolon();
        return new ContinueStmt(start);
    }

    private ReturnStmt ParseReturnStatement()
    {
        var start = Advance().Span; // RETURN
        Expression? value = null;
        if (!Check(TokenKind.Semicolon) && !IsBlockClose())
        {
            value = ParseExpression();
        }
        ExpectSemicolon();
        return new ReturnStmt(value, start);
    }

    private GotoStmt ParseGotoStatement()
    {
        var start = Advance().Span; // GOTO
        var label = Expect(TokenKind.Identifier, "Expected label").StringValue;
        return new GotoStmt(label, start);
    }

    private PrintStmt ParsePrintStatement()
    {
        var start = Advance().Span; // PRINT
        Expect(TokenKind.LParen, "Expected '('");
        var args = new List<Expression>();
        if (!Check(TokenKind.RParen))
        {
            do
            {
                args.Add(ParsePrintArgument());
            } while (Match(TokenKind.Comma));
        }
        Expect(TokenKind.RParen, "Expected ')'");
        return new PrintStmt(args, start);
    }

    private Expression ParsePrintArgument()
    {
        // String functions like /, !, FORM$(...), etc.
        if (Current.Kind == TokenKind.Slash)
        {
            var span = Advance().Span;
            return new StringFuncExpr("/", new List<Expression>(), span);
        }
        if (Current.Kind == TokenKind.StringFunc)
        {
            var token = Advance();
            Expect(TokenKind.LParen, "Expected '('");
            var args = ParseExpressionList();
            Expect(TokenKind.RParen, "Expected ')'");
            return new StringFuncExpr(token.StringValue, args, token.Span);
        }
        if (Current.Kind == TokenKind.Exclamation)
        {
            var token = Advance();
            Expect(TokenKind.LParen, "Expected '('");
            var args = ParseExpressionList();
            Expect(TokenKind.RParen, "Expected ')'");
            return new StringFuncExpr("!", args, token.Span);
        }
        if (Current.Kind == TokenKind.Percent)
        {
            var token = Advance();
            Expect(TokenKind.LParen, "Expected '('");
            var args = ParseExpressionList();
            Expect(TokenKind.RParen, "Expected ')'");
            return new StringFuncExpr("%", args, token.Span);
        }
        return ParseExpression();
    }

    private ExpressionStmt ParseExpressionStatement()
    {
        var start = Current.Span;
        var expr = ParseExpression();
        ExpectSemicolon();
        return new ExpressionStmt(expr, start);
    }

    // ---- Expressions (Pratt parser / precedence climbing) ----

    public Expression ParseExpression()
    {
        var expr = ParseAssignment();

        // Comma expression
        if (Check(TokenKind.Comma) && !IsInArgList())
        {
            while (Match(TokenKind.Comma))
            {
                var right = ParseAssignment();
                expr = new CommaExpr(expr, right, expr.Span);
            }
        }

        return expr;
    }

    // Track whether we're inside an argument list (to avoid comma as operator)
    private int _argListDepth;
    private bool IsInArgList() => _argListDepth > 0;

    private Expression ParseAssignment()
    {
        var expr = ParseConditional();

        if (Check(TokenKind.Eq))
        {
            Advance();
            var value = ParseAssignment();
            return new AssignExpr(expr, value, expr.Span);
        }

        if (CheckAny(TokenKind.PlusEq, TokenKind.MinusEq, TokenKind.StarEq, TokenKind.SlashEq))
        {
            var op = Current.Kind switch
            {
                TokenKind.PlusEq => CompoundAssignOp.AddAssign,
                TokenKind.MinusEq => CompoundAssignOp.SubAssign,
                TokenKind.StarEq => CompoundAssignOp.MulAssign,
                TokenKind.SlashEq => CompoundAssignOp.DivAssign,
                _ => CompoundAssignOp.AddAssign,
            };
            Advance();
            var value = ParseAssignment();
            return new CompoundAssignExpr(op, expr, value, expr.Span);
        }

        return expr;
    }

    private Expression ParseConditional()
    {
        var expr = ParseLogOr();

        if (Match(TokenKind.Question))
        {
            var trueExpr = ParseExpression();
            Expect(TokenKind.Colon, "Expected ':' in conditional expression");
            var falseExpr = ParseConditional();
            return new ConditionalExpr(expr, trueExpr, falseExpr, expr.Span);
        }

        return expr;
    }

    private Expression ParseLogOr()
    {
        var left = ParseLogAnd();
        while (Match(TokenKind.LogOr))
        {
            var right = ParseLogAnd();
            left = new BinaryExpr(BinaryOp.LogOr, left, right, left.Span);
        }
        return left;
    }

    private Expression ParseLogAnd()
    {
        var left = ParseBitOr();
        while (Match(TokenKind.LogAnd))
        {
            var right = ParseBitOr();
            left = new BinaryExpr(BinaryOp.LogAnd, left, right, left.Span);
        }
        return left;
    }

    private Expression ParseBitOr()
    {
        var left = ParseBitXor();
        while (Match(TokenKind.Pipe))
        {
            var right = ParseBitXor();
            left = new BinaryExpr(BinaryOp.Or, left, right, left.Span);
        }
        return left;
    }

    private Expression ParseBitXor()
    {
        // XOR is not in current SLANG? Skip for now, fall through
        return ParseBitAnd();
    }

    private Expression ParseBitAnd()
    {
        var left = ParseEquality();
        while (Match(TokenKind.Ampersand))
        {
            var right = ParseEquality();
            left = new BinaryExpr(BinaryOp.And, left, right, left.Span);
        }
        return left;
    }

    private Expression ParseEquality()
    {
        var left = ParseComparison();
        while (CheckAny(TokenKind.EqEq, TokenKind.NotEq))
        {
            var op = Current.Kind == TokenKind.EqEq ? BinaryOp.Eq : BinaryOp.Neq;
            Advance();
            var right = ParseComparison();
            left = new BinaryExpr(op, left, right, left.Span);
        }
        return left;
    }

    private Expression ParseComparison()
    {
        var left = ParseShift();
        while (CheckAny(TokenKind.Lt, TokenKind.Gt, TokenKind.Le, TokenKind.Ge,
                         TokenKind.SignedLt, TokenKind.SignedGt, TokenKind.SignedLe, TokenKind.SignedGe))
        {
            var op = Current.Kind switch
            {
                TokenKind.Lt => BinaryOp.Lt,
                TokenKind.Gt => BinaryOp.Gt,
                TokenKind.Le => BinaryOp.Le,
                TokenKind.Ge => BinaryOp.Ge,
                TokenKind.SignedLt => BinaryOp.SLt,
                TokenKind.SignedGt => BinaryOp.SGt,
                TokenKind.SignedLe => BinaryOp.SLe,
                TokenKind.SignedGe => BinaryOp.SGe,
                _ => BinaryOp.Lt,
            };
            Advance();
            var right = ParseShift();
            left = new BinaryExpr(op, left, right, left.Span);
        }
        return left;
    }

    private Expression ParseShift()
    {
        var left = ParseAddSub();
        while (CheckAny(TokenKind.Shl, TokenKind.Shr, TokenKind.SignedShl, TokenKind.SignedShr))
        {
            var op = Current.Kind switch
            {
                TokenKind.Shl => BinaryOp.Shl,
                TokenKind.Shr => BinaryOp.Shr,
                TokenKind.SignedShl => BinaryOp.SShl,
                TokenKind.SignedShr => BinaryOp.SShr,
                _ => BinaryOp.Shl,
            };
            Advance();
            var right = ParseAddSub();
            left = new BinaryExpr(op, left, right, left.Span);
        }
        return left;
    }

    private Expression ParseAddSub()
    {
        var left = ParseMulDiv();
        while (CheckAny(TokenKind.Plus, TokenKind.Minus))
        {
            var op = Current.Kind == TokenKind.Plus ? BinaryOp.Add : BinaryOp.Sub;
            Advance();
            var right = ParseMulDiv();
            left = new BinaryExpr(op, left, right, left.Span);
        }
        return left;
    }

    private Expression ParseMulDiv()
    {
        var left = ParseUnary();
        while (CheckAny(TokenKind.Star, TokenKind.Slash,
                         TokenKind.SignedMul, TokenKind.SignedDiv, TokenKind.SignedMod))
        {
            var op = Current.Kind switch
            {
                TokenKind.Star => BinaryOp.Mul,
                TokenKind.Slash => BinaryOp.Div,
                TokenKind.SignedMul => BinaryOp.SMul,
                TokenKind.SignedDiv => BinaryOp.SDiv,
                TokenKind.SignedMod => BinaryOp.SMod,
                _ => BinaryOp.Mul,
            };
            Advance();
            var right = ParseUnary();
            left = new BinaryExpr(op, left, right, left.Span);
        }
        return left;
    }

    private Expression ParseUnary()
    {
        var start = Current.Span;

        // Prefix increment/decrement
        if (CheckAny(TokenKind.PlusPlus, TokenKind.MinusMinus))
        {
            bool isInc = Current.Kind == TokenKind.PlusPlus;
            Advance();
            var operand = ParseUnary();
            return new IncrementExpr(operand, isInc, isPrefix: true, start);
        }

        // Unary operators
        if (Match(TokenKind.Minus))
            return new UnaryExpr(UnaryOp.Negate, ParseUnary(), start);
        if (Match(TokenKind.Plus))
            return new UnaryExpr(UnaryOp.Plus, ParseUnary(), start);
        if (Match(TokenKind.Not))
            return new UnaryExpr(UnaryOp.Not, ParseUnary(), start);
        if (Match(TokenKind.Cpl))
            return new UnaryExpr(UnaryOp.Cpl, ParseUnary(), start);

        // Address-of
        if (Match(TokenKind.Ampersand))
            return new AddressOfExpr(ParseUnary(), start);

        // HIGH / LOW
        if (Check(TokenKind.High) || Check(TokenKind.Low))
        {
            bool isHigh = Current.Kind == TokenKind.High;
            Advance();
            return new HighLowExpr(isHigh, ParseUnary(), start);
        }

        // % (word cast)
        if (Match(TokenKind.Percent))
            return new CastExpr(DataSize.Word, ParseUnary(), start);

        // CODE expression
        if (Check(TokenKind.Code))
        {
            Advance();
            Expect(TokenKind.LParen, "Expected '('");
            var codes = ParseCodeExprList();
            Expect(TokenKind.RParen, "Expected ')'");
            return new CodeExpr(codes, start);
        }

        return ParsePostfix();
    }

    private Expression ParsePostfix()
    {
        var expr = ParsePrimary();

        while (true)
        {
            if (CheckAny(TokenKind.PlusPlus, TokenKind.MinusMinus))
            {
                bool isInc = Current.Kind == TokenKind.PlusPlus;
                Advance();
                expr = new IncrementExpr(expr, isInc, isPrefix: false, expr.Span);
            }
            else if (Check(TokenKind.ArrayBracketOpen))
            {
                // Array access - collect all dimensions
                var indices = new List<Expression>();
                while (Check(TokenKind.ArrayBracketOpen))
                {
                    Advance();
                    indices.Add(ParseExpression());
                    Expect(TokenKind.RBracket, "Expected ']'");
                }
                expr = new ArrayAccessExpr(expr, indices, expr.Span);
            }
            else if (Check(TokenKind.LParen))
            {
                // Function call
                Advance();
                var args = new List<Expression>();
                if (!Check(TokenKind.RParen))
                {
                    _argListDepth++;
                    args = ParseExpressionList();
                    _argListDepth--;
                }
                Expect(TokenKind.RParen, "Expected ')'");
                expr = new CallExpr(expr, args, expr.Span);
            }
            else
            {
                break;
            }
        }

        return expr;
    }

    private Expression ParsePrimary()
    {
        var start = Current.Span;

        switch (Current.Kind)
        {
            case TokenKind.IntegerLiteral:
            case TokenKind.CharLiteral:
            {
                var token = Advance();
                return new IntegerLiteral(token.IntValue, token.Span);
            }
            case TokenKind.FloatLiteral:
            {
                var token = Advance();
                return new FloatLiteral(token.FloatValue, token.Span);
            }
            case TokenKind.StringLiteral:
            {
                var token = Advance();
                return new StringLiteral(token.StringValue, token.Span);
            }
            case TokenKind.Identifier:
            {
                var token = Advance();
                return new IdentifierExpr(token.StringValue, token.Span);
            }
            case TokenKind.LParen:
            {
                Advance();
                var expr = ParseExpression();
                Expect(TokenKind.RParen, "Expected ')'");
                return expr;
            }
            default:
                _diagnostics.Error($"Expected expression, got {Current.Kind}", Current.Span);
                return new IntegerLiteral(0, start); // error recovery
        }
    }

    private List<Expression> ParseExpressionList()
    {
        var list = new List<Expression>();
        do
        {
            list.Add(ParseAssignment()); // single expression, not comma
        } while (Match(TokenKind.Comma));
        return list;
    }

    private List<Expression> ParseCodeExprList()
    {
        var list = new List<Expression>();
        do
        {
            list.Add(ParseAssignment());
        } while (Match(TokenKind.Comma));
        return list;
    }

    // ---- Helpers ----

    private DataSize ParseDataSize()
    {
        if (Match(TokenKind.Byte) || Match(TokenKind.Exclamation)) return DataSize.Byte;
        if (Match(TokenKind.Word)) return DataSize.Word;
        if (Match(TokenKind.Float)) return DataSize.Float;
        return DataSize.Word; // default
    }

    private DataSize ParseOptionalDataSize()
    {
        if (Check(TokenKind.Byte) || Check(TokenKind.Exclamation)) { Advance(); return DataSize.Byte; }
        if (Check(TokenKind.Word)) { Advance(); return DataSize.Word; }
        if (Check(TokenKind.Float)) { Advance(); return DataSize.Float; }
        return DataSize.Word;
    }

    private bool IsBlockOpen() =>
        CheckAny(TokenKind.Begin, TokenKind.LBracket, TokenKind.LBrace,
                 TokenKind.LAngleBracket, TokenKind.LParen);

    private bool IsBlockClose() =>
        CheckAny(TokenKind.End, TokenKind.RBracket, TokenKind.RBrace,
                 TokenKind.RAngleBracket, TokenKind.RParen, TokenKind.Wend);

    private void ExpectBlockOpen()
    {
        if (IsBlockOpen())
            Advance();
        else
            _diagnostics.Error("Expected block open (BEGIN, [, {, (, ｢)", Current.Span);
    }

    private void ExpectBlockClose()
    {
        if (IsBlockClose())
            Advance();
        else
            _diagnostics.Error("Expected block close (END, ], }, ), ｣)", Current.Span);
    }

    private bool IsDeclarationStart() =>
        CheckAny(TokenKind.Var, TokenKind.Array, TokenKind.Const, TokenKind.Machine);

    private AstNode? ParseLocalDeclaration()
    {
        switch (Current.Kind)
        {
            case TokenKind.Var: return ParseVarDeclaration();
            case TokenKind.Array: return ParseArrayDeclaration();
            case TokenKind.Const: return ParseConstDeclaration();
            case TokenKind.Machine: return ParseMachineDeclaration();
            default: return null;
        }
    }

    private AstNode ParseStatementOrBlock()
    {
        if (IsBlockOpen())
            return ParseCompoundStatement();
        var stmt = ParseStatement();
        return stmt ?? new Block(new List<AstNode>(), Current.Span);
    }

    private List<Expression> ParseCodeBlock()
    {
        ExpectBlockOpen();
        var list = ParseCodeExprList();
        ExpectBlockClose();
        return list;
    }
}
