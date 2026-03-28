using SLANGCompiler.Lexer;
using SLANGCompiler.Parser.Ast;

namespace SLANGCompiler.Parser;

/// <summary>
/// SLANG再帰下降パーサー。
/// SLANGTEST.SLの全構文をカバーする。
/// </summary>
public class Parser
{
    private readonly List<Token> _tokens;
    private readonly DiagnosticBag _diagnostics;
    private int _pos;
    private int _argListDepth;

    public Parser(List<Token> tokens, DiagnosticBag diagnostics)
    {
        _tokens = tokens;
        _diagnostics = diagnostics;
    }

    // ==== Token access ====

    private Token Current => _pos < _tokens.Count ? _tokens[_pos] : new Token(TokenKind.EOF, "", SourceSpan.Unknown);
    private Token Peek(int offset = 0)
    {
        int i = _pos + offset;
        return i < _tokens.Count ? _tokens[i] : new Token(TokenKind.EOF, "", SourceSpan.Unknown);
    }
    private Token Advance() { var t = Current; _pos++; return t; }
    private bool Check(TokenKind k) => Current.Kind == k;
    private bool CheckAny(params TokenKind[] ks) => ks.Contains(Current.Kind);
    private bool Match(TokenKind k) { if (Check(k)) { Advance(); return true; } return false; }
    private Token Expect(TokenKind k, string? msg = null)
    {
        if (Check(k)) return Advance();
        Error(msg ?? $"Expected {k}, got {Current.Kind}");
        return Current;
    }
    private void Error(string msg) => _diagnostics.Error(msg, Current.Span);

    // ==== Top-level ====

    public CompilationUnit ParseCompilationUnit()
    {
        var defs = new List<AstNode>();
        while (!Check(TokenKind.EOF))
        {
            if (Match(TokenKind.Semicolon)) continue;
            int before = _pos;
            var def = ParseTopLevel();
            if (def != null) defs.Add(def);
            // Safety: if parser didn't advance, skip token to avoid infinite loop
            if (_pos == before) Advance();
        }
        return new CompilationUnit(defs, SourceSpan.Unknown);
    }

    private AstNode? ParseTopLevel()
    {
        switch (Current.Kind)
        {
            case TokenKind.Org: return ParseOrg();
            case TokenKind.Work: return ParseWork();
            case TokenKind.Offset: return ParseOffset();
            case TokenKind.Plain: return new PlainAsm(Advance().StringValue, Current.Span);
            case TokenKind.PreprocIf: return ParsePreprocIf();
            case TokenKind.PreprocElse: Advance(); return null;
            case TokenKind.PreprocEnd: Advance(); return null;
            case TokenKind.PreprocInclude: Advance(); return null; // preprocessorが処理済み
            case TokenKind.Const: return ParseConstDecl();
            case TokenKind.Var: return ParseVarDeclList();
            case TokenKind.Array: return ParseArrayDeclList();
            case TokenKind.Machine: return ParseMachineDeclList();
            case TokenKind.Byte:
            case TokenKind.Word:
            case TokenKind.Float:
            case TokenKind.Exclamation:
                return ParseVarDeclList();
            case TokenKind.Identifier:
                // Function definition: IDENT '(' ...
                if (IsFuncDefStart())
                    return ParseFuncDef();
                Error($"Unexpected identifier at top level: {Current.Text}");
                return null;
            case TokenKind.Module:
                // Skip #MODULE for now
                Advance();
                return null;
            default:
                Error($"Unexpected token at top level: {Current.Kind} '{Current.Text}'");
                return null;
        }
    }

    private bool IsFuncDefStart()
    {
        // identifier [: expr] '('
        int i = 1;
        if (Peek(i).Kind == TokenKind.Colon) i += 2; // skip : expr (simplified)
        return Peek(i).Kind == TokenKind.LParen;
    }

    // ==== Preprocessor ====

    private AstNode? ParsePreprocIf()
    {
        var token = Advance(); // #IF
        var expr = token.StringValue;
        // Simple: if the expression is "FALSE" or "0", skip until #END/#ELSE
        // For now, just skip the conditional block entirely
        if (expr.Equals("FALSE", StringComparison.OrdinalIgnoreCase) || expr == "0")
        {
            SkipPreprocBlock();
            return null;
        }
        // TRUE: parse normally until #END
        return null;
    }

    private void SkipPreprocBlock()
    {
        int depth = 1;
        while (!Check(TokenKind.EOF) && depth > 0)
        {
            if (Check(TokenKind.PreprocIf)) { depth++; Advance(); continue; }
            if (Check(TokenKind.PreprocEnd)) { depth--; Advance(); continue; }
            if (Check(TokenKind.PreprocElse) && depth == 1) { Advance(); return; }
            Advance();
        }
    }

    // ==== Directives ====

    private OrgDirective ParseOrg()
    {
        var s = Advance().Span;
        return new OrgDirective(ParseNcExpr(), s);
    }
    private WorkDirective ParseWork()
    {
        var s = Advance().Span;
        return new WorkDirective(ParseNcExpr(), s);
    }
    private OffsetDirective ParseOffset()
    {
        var s = Advance().Span;
        return new OffsetDirective(ParseNcExpr(), s);
    }

    // ==== CONST declaration ====

    private AstNode ParseConstDecl()
    {
        var start = Advance().Span; // CONST
        var decls = new List<AstNode>();
        do
        {
            bool isAsm = Match(TokenKind.Asm); // CONST ASM → EQUとして定義
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
                value = ParseNcExpr();
            }
            decls.Add(new ConstDecl(name, value, isAsm, start));
        } while (Match(TokenKind.Comma));
        Match(TokenKind.Semicolon);

        return decls.Count == 1 ? decls[0] : new Block(decls, start);
    }

    // ==== VAR declaration (supports comma-separated, mixed types) ====

    private AstNode ParseVarDeclList()
    {
        var start = Current.Span;
        bool hadVarKeyword = Match(TokenKind.Var);
        var decls = new List<AstNode>();

        do
        {
            decls.Add(ParseSingleVarOrArray(hadVarKeyword));
        } while (Match(TokenKind.Comma));

        Match(TokenKind.Semicolon);
        return decls.Count == 1 ? decls[0] : new Block(decls, start);
    }

    private AstNode ParseSingleVarOrArray(bool afterVarKeyword)
    {
        var start = Current.Span;
        DataSize size = ParseOptionalDataSize();
        var name = Expect(TokenKind.Identifier, "Expected variable name").StringValue;
        Expression? address = null;

        // Array dimensions
        var dims = new List<Expression?>();
        while (Check(TokenKind.ArrayBracketOpen))
        {
            Advance();
            if (Check(TokenKind.RBracket))
                dims.Add(null); // indirect / unsized
            else
                dims.Add(ParseNcExpr());
            Expect(TokenKind.RBracket, "Expected ']'");
        }

        // :address
        if (Match(TokenKind.Colon))
            address = ParseNcExpr();

        // = initializer
        Expression? initValue = null;
        List<Expression>? initCode = null;
        if (Match(TokenKind.Eq))
        {
            if (IsBlockOpen())
                initCode = ParseCodeBlock();
            else
                initValue = ParseNcExpr();
        }

        if (dims.Count > 0)
            return new ArrayDecl(name, size, address, dims, initValue, initCode, start);
        return new VarDecl(name, size, address, initValue, initCode, start);
    }

    // ==== ARRAY declaration ====

    private AstNode ParseArrayDeclList()
    {
        var start = Advance().Span; // ARRAY
        var decls = new List<AstNode>();

        do
        {
            var s = Current.Span;
            DataSize size = ParseOptionalDataSize();
            var name = Expect(TokenKind.Identifier, "Expected array name").StringValue;

            var dims = new List<Expression?>();
            while (Check(TokenKind.ArrayBracketOpen))
            {
                Advance();
                if (Check(TokenKind.RBracket))
                    dims.Add(null);
                else
                    dims.Add(ParseNcExpr());
                Expect(TokenKind.RBracket, "Expected ']'");
            }

            Expression? address = null;
            if (Match(TokenKind.Colon))
                address = ParseNcExpr();

            Expression? initValue = null;
            List<Expression>? initCode = null;
            if (Match(TokenKind.Eq))
            {
                if (IsBlockOpen())
                    initCode = ParseCodeBlock();
                else
                    initValue = ParseNcExpr();
            }

            decls.Add(new ArrayDecl(name, size, address, dims, initValue, initCode, s));
        } while (Match(TokenKind.Comma));

        Match(TokenKind.Semicolon);
        return decls.Count == 1 ? decls[0] : new Block(decls, start);
    }

    // ==== MACHINE declaration ====

    private AstNode ParseMachineDeclList()
    {
        var start = Advance().Span; // MACHINE
        var decls = new List<AstNode>();

        do
        {
            var name = Expect(TokenKind.Identifier, "Expected function name").StringValue;
            Expression? address = null;
            int? paramCount = null;

            if (Match(TokenKind.Colon))
                address = ParseNcExpr();

            if (Match(TokenKind.LParen))
            {
                if (Check(TokenKind.IntegerLiteral))
                {
                    paramCount = Current.IntValue;
                    Advance();
                }
                Expect(TokenKind.RParen, "Expected ')'");
            }

            decls.Add(new MachineDecl(name, address, paramCount, start));
        } while (Match(TokenKind.Comma));

        Match(TokenKind.Semicolon);
        return decls.Count == 1 ? decls[0] : new Block(decls, start);
    }

    // ==== Function definition ====

    private FuncDef ParseFuncDef()
    {
        var start = Current.Span;
        var name = Advance().StringValue; // identifier
        Expression? address = null;

        if (Match(TokenKind.Colon))
            address = ParseNcExpr();

        Expect(TokenKind.LParen, "Expected '('");
        var parms = new List<ParamDecl>();
        if (!Check(TokenKind.RParen))
        {
            do
            {
                var ps = Current.Span;
                DataSize psz = ParseOptionalDataSize();
                var pn = Expect(TokenKind.Identifier, "Expected parameter name").StringValue;
                bool isArr = false;
                if (Check(TokenKind.ArrayBracketOpen)) { Advance(); Expect(TokenKind.RBracket); isArr = true; }
                parms.Add(new ParamDecl(pn, psz, isArr, ps));
            } while (Match(TokenKind.Comma));
        }
        Expect(TokenKind.RParen, "Expected ')'");

        // Static declarations (before BEGIN)
        var staticDecls = new List<AstNode>();
        while (IsDeclStart() && !IsBlockOpen())
        {
            staticDecls.Add(ParseLocalDecl());
        }

        // BEGIN
        ExpectBlockOpen("Expected BEGIN/[/{/( for function body");

        // Local declarations (after BEGIN, before statements)
        var localDecls = new List<AstNode>();
        while (IsDeclStart())
        {
            localDecls.Add(ParseLocalDecl());
        }

        // Statements
        var stmts = ParseStmtList();
        var body = new Block(stmts, start);

        // END with optional return value
        Expression? retVal = null;
        ExpectBlockClose("Expected END/]");

        if (Match(TokenKind.LParen))
        {
            retVal = ParseExpr();
            Expect(TokenKind.RParen, "Expected ')'");
        }
        Match(TokenKind.Semicolon);

        return new FuncDef(name, address, parms, staticDecls, localDecls, body, retVal, start);
    }

    private AstNode ParseLocalDecl()
    {
        if (Check(TokenKind.Array)) return ParseArrayDeclList();
        return ParseVarDeclList(); // handles VAR, BYTE, WORD, FLOAT
    }

    // ==== Statements ====

    private List<AstNode> ParseStmtList()
    {
        var stmts = new List<AstNode>();
        while (!IsBlockClose() && !Check(TokenKind.EOF) && !Check(TokenKind.Until)
               && !Check(TokenKind.Wend))
        {
            if (Match(TokenKind.Semicolon)) continue;
            int before = _pos;
            var stmt = ParseStmt();
            if (stmt != null) stmts.Add(stmt);
            if (_pos == before) Advance(); // safety
        }
        return stmts;
    }

    private AstNode? ParseStmt()
    {
        switch (Current.Kind)
        {
            case TokenKind.If: return ParseIf();
            case TokenKind.While: return ParseWhile();
            case TokenKind.Repeat: return ParseRepeat();
            case TokenKind.Loop: return ParseLoop();
            case TokenKind.For: return ParseFor();
            case TokenKind.Case: return ParseCase();
            case TokenKind.Exit: return ParseExit();
            case TokenKind.Continue: Advance(); Match(TokenKind.Semicolon); return new ContinueStmt(Current.Span);
            case TokenKind.Return: return ParseReturn();
            case TokenKind.Goto: return ParseGoto();
            case TokenKind.Print: return ParsePrint();
            case TokenKind.Plain: return new PlainAsm(Advance().StringValue, Current.Span);
            case TokenKind.Var:
            case TokenKind.Array:
            case TokenKind.Byte:
            case TokenKind.Word:
            case TokenKind.Float:
                return ParseLocalDecl();
            case TokenKind.PreprocIf: return ParsePreprocIf();
            case TokenKind.PreprocEnd: Advance(); return null;
            default:
                if (IsBlockOpen())
                    return ParseCompound();
                // Label: IDENT ':'
                if (Check(TokenKind.Identifier) && Peek(1).Kind == TokenKind.Colon)
                {
                    var lbl = Advance().StringValue;
                    Advance(); // :
                    return new LabelStmt(lbl, Current.Span);
                }
                return ParseExprStmt();
        }
    }

    private Block ParseCompound()
    {
        var s = Current.Span;
        ExpectBlockOpen();
        var stmts = ParseStmtList();
        ExpectBlockClose();
        return new Block(stmts, s);
    }

    private ExpressionStmt ParseExprStmt()
    {
        var s = Current.Span;
        var expr = ParseExpr();
        Match(TokenKind.Semicolon);
        return new ExpressionStmt(expr, s);
    }

    // ---- IF ----
    private IfStmt ParseIf()
    {
        var s = Advance().Span; // IF
        var branches = new List<(Expression, AstNode)>();

        var cond = ParseExpr();
        Match(TokenKind.Then);
        var body = ParseIfBody();
        branches.Add((cond, body));

        while (Check(TokenKind.Elif) || Check(TokenKind.Ef) || (Check(TokenKind.Else) && Peek(1).Kind == TokenKind.If))
        {
            if (Match(TokenKind.Elif) || Match(TokenKind.Ef)) { }
            else { Advance(); Advance(); } // ELSE IF
            cond = ParseExpr();
            Match(TokenKind.Then);
            body = ParseIfBody();
            branches.Add((cond, body));
        }

        AstNode? elsePart = null;
        if (Match(TokenKind.Else))
            elsePart = ParseIfBody();

        Match(TokenKind.EndIf);
        return new IfStmt(branches, elsePart, s);
    }

    private AstNode ParseIfBody()
    {
        if (IsBlockOpen()) return ParseCompound();
        var stmt = ParseStmt();
        Match(TokenKind.Semicolon);
        return stmt ?? new Block(new List<AstNode>(), Current.Span);
    }

    // ---- WHILE ----
    private WhileStmt ParseWhile()
    {
        var s = Advance().Span;
        // WHILE expr or WHILE(expr)
        var cond = ParseExpr();

        AstNode body;
        if (Match(TokenKind.Do) || IsBlockOpen())
        {
            if (IsBlockOpen())
            {
                body = ParseCompound();
            }
            else
            {
                var stmts = ParseStmtList();
                if (Match(TokenKind.Wend)) { }
                else ExpectBlockClose();
                body = new Block(stmts, s);
            }
        }
        else
        {
            body = ParseStmt() ?? new Block(new List<AstNode>(), s);
        }
        return new WhileStmt(cond, body, s);
    }

    // ---- REPEAT ----
    private RepeatStmt ParseRepeat()
    {
        var s = Advance().Span;
        AstNode body;
        if (IsBlockOpen())
            body = ParseCompound();
        else
            body = ParseStmt() ?? new Block(new List<AstNode>(), s);
        Expect(TokenKind.Until, "Expected UNTIL");
        var cond = ParseExpr();
        Match(TokenKind.Semicolon);
        return new RepeatStmt(body, cond, s);
    }

    // ---- LOOP ----
    private LoopStmt ParseLoop()
    {
        var s = Advance().Span;
        var body = IsBlockOpen() ? (AstNode)ParseCompound() : ParseStmt()!;
        return new LoopStmt(body, s);
    }

    // ---- FOR ----
    private ForStmt ParseFor()
    {
        var s = Advance().Span;
        var v = Expect(TokenKind.Identifier).StringValue;
        Expect(TokenKind.Eq, "Expected '='");
        var from = ParseNcExpr();
        bool down = Match(TokenKind.DownTo);
        if (!down) Expect(TokenKind.To, "Expected TO or DOWNTO");
        var to = ParseNcExpr();

        AstNode body;
        if (Match(TokenKind.Do) || IsBlockOpen())
        {
            body = ParseCompound();
            Match(TokenKind.Next); Match(TokenKind.Semicolon);
        }
        else
        {
            body = ParseStmt() ?? new Block(new List<AstNode>(), s);
            Match(TokenKind.Next); Match(TokenKind.Semicolon);
        }
        return new ForStmt(v, from, to, down, body, s);
    }

    // ---- CASE ----
    private CaseStmt ParseCase()
    {
        var s = Advance().Span;
        var expr = ParseExpr();
        Match(TokenKind.Of);
        ExpectBlockOpen();

        var branches = new List<CaseBranch>();
        while (!IsBlockClose() && !Check(TokenKind.EOF))
        {
            if (Match(TokenKind.Semicolon)) continue;
            if (Check(TokenKind.Others))
            {
                Advance();
                Match(TokenKind.Colon);
                var body = ParseStmt()!;
                branches.Add(new CaseBranch(null, null, body));
            }
            else
            {
                // value [, value]* [TO value] ':' stmt
                var val = ParseNcExpr();
                Expression? rangeEnd = null;

                // Handle comma-separated case values: 6,7,8:
                while (Match(TokenKind.Comma))
                {
                    // For now, treat comma-separated as individual branches
                    // The last one gets the body
                    Match(TokenKind.Colon);
                    if (!Check(TokenKind.IntegerLiteral) && !Check(TokenKind.Identifier))
                    {
                        var body = ParseStmt()!;
                        branches.Add(new CaseBranch(val, rangeEnd, body));
                        goto nextBranch;
                    }
                    branches.Add(new CaseBranch(val, null, new Block(new List<AstNode>(), s)));
                    val = ParseNcExpr();
                }

                if (Match(TokenKind.To))
                    rangeEnd = ParseNcExpr();
                Match(TokenKind.Colon);
                {
                    var body = ParseStmt()!;
                    branches.Add(new CaseBranch(val, rangeEnd, body));
                }
                nextBranch:;
            }
        }
        ExpectBlockClose();
        return new CaseStmt(expr, branches, s);
    }

    // ---- EXIT ----
    private ExitStmt ParseExit()
    {
        var s = Advance().Span;
        Expression? level = null;
        string? label = null;

        if (Match(TokenKind.To))
            label = Expect(TokenKind.Identifier).StringValue;
        else if (Match(TokenKind.LParen))
        {
            level = ParseExpr();
            Expect(TokenKind.RParen);
        }
        Match(TokenKind.Semicolon);
        return new ExitStmt(level, label, s);
    }

    // ---- RETURN ----
    private ReturnStmt ParseReturn()
    {
        var s = Advance().Span;
        Expression? val = null;
        if (Match(TokenKind.LParen))
        {
            val = ParseExpr();
            Expect(TokenKind.RParen);
        }
        else if (!Check(TokenKind.Semicolon) && !IsBlockClose() && !Check(TokenKind.EOF))
        {
            val = ParseExpr();
        }
        Match(TokenKind.Semicolon);
        return new ReturnStmt(val, s);
    }

    // ---- GOTO ----
    private GotoStmt ParseGoto()
    {
        var s = Advance().Span;
        var lbl = Expect(TokenKind.Identifier).StringValue;
        Match(TokenKind.Semicolon);
        return new GotoStmt(lbl, s);
    }

    // ---- PRINT ----
    private PrintStmt ParsePrint()
    {
        var s = Advance().Span;
        Expect(TokenKind.LParen, "Expected '('");
        var args = new List<Expression>();
        _argListDepth++;
        while (!Check(TokenKind.RParen) && !Check(TokenKind.EOF))
        {
            args.Add(ParsePrintArg());
            if (!Match(TokenKind.Comma)) break;
        }
        _argListDepth--;
        Expect(TokenKind.RParen, "Expected ')'");
        Match(TokenKind.Semicolon);
        return new PrintStmt(args, s);
    }

    private Expression ParsePrintArg()
    {
        var s = Current.Span;
        // / = newline
        if (Check(TokenKind.Slash))
        {
            Advance();
            return new StringFuncExpr("/", new List<Expression>(), s);
        }
        // String functions
        if (Check(TokenKind.StringFunc))
        {
            var fn = Advance().StringValue;
            Expect(TokenKind.LParen);
            var args = ParseArgList();
            Expect(TokenKind.RParen);
            return new StringFuncExpr(fn, args, s);
        }
        if (Check(TokenKind.Exclamation) && Peek(1).Kind == TokenKind.LParen)
        {
            Advance();
            Expect(TokenKind.LParen);
            var args = ParseArgList();
            Expect(TokenKind.RParen);
            return new StringFuncExpr("!", args, s);
        }
        if (Check(TokenKind.Percent) && Peek(1).Kind == TokenKind.LParen)
        {
            Advance();
            Expect(TokenKind.LParen);
            var args = ParseArgList();
            Expect(TokenKind.RParen);
            return new StringFuncExpr("%", args, s);
        }
        return ParseNcExpr();
    }

    // ==== Expression parsing (precedence climbing) ====

    /// <summary>ParseExpr: full expression including comma operator (outside arg lists)</summary>
    public Expression ParseExpr()
    {
        var e = ParseAssign();
        while (_argListDepth == 0 && Match(TokenKind.Comma))
        {
            var r = ParseAssign();
            e = new CommaExpr(e, r, e.Span);
        }
        return e;
    }

    /// <summary>ParseNcExpr: no-comma expression (used in most places)</summary>
    private Expression ParseNcExpr() => ParseAssign();

    private Expression ParseAssign()
    {
        var e = ParseConditional();
        if (Match(TokenKind.Eq))
        {
            var v = ParseAssign();
            return new AssignExpr(e, v, e.Span);
        }
        if (CheckAny(TokenKind.PlusEq, TokenKind.MinusEq, TokenKind.StarEq, TokenKind.SlashEq))
        {
            var op = Current.Kind switch
            {
                TokenKind.PlusEq => CompoundAssignOp.AddAssign,
                TokenKind.MinusEq => CompoundAssignOp.SubAssign,
                TokenKind.StarEq => CompoundAssignOp.MulAssign,
                _ => CompoundAssignOp.DivAssign,
            };
            Advance();
            return new CompoundAssignExpr(op, e, ParseAssign(), e.Span);
        }
        return e;
    }

    private Expression ParseConditional()
    {
        var e = ParseLogOr();
        if (Match(TokenKind.Question))
        {
            var t = ParseNcExpr();
            Expect(TokenKind.Colon, "Expected ':' in ?:");
            var f = ParseConditional();
            return new ConditionalExpr(e, t, f, e.Span);
        }
        return e;
    }

    private Expression ParseLogOr()
    {
        var e = ParseLogAnd();
        while (Match(TokenKind.LogOr))
        {
            e = new BinaryExpr(BinaryOp.LogOr, e, ParseLogAnd(), e.Span);
        }
        return e;
    }

    private Expression ParseLogAnd()
    {
        var e = ParseBitOps();
        while (Match(TokenKind.LogAnd))
        {
            e = new BinaryExpr(BinaryOp.LogAnd, e, ParseBitOps(), e.Span);
        }
        return e;
    }

    // 仕様: AND OR XOR は同一優先度、関係演算子より低い
    private Expression ParseBitOps()
    {
        var e = ParseEquality();
        while (true)
        {
            if (Match(TokenKind.And) || Match(TokenKind.Ampersand))
                e = new BinaryExpr(BinaryOp.And, e, ParseEquality(), e.Span);
            else if (Match(TokenKind.Or) || Match(TokenKind.Pipe))
                e = new BinaryExpr(BinaryOp.Or, e, ParseEquality(), e.Span);
            else if (Match(TokenKind.Xor))
                e = new BinaryExpr(BinaryOp.Xor, e, ParseEquality(), e.Span);
            else break;
        }
        return e;
    }

    private Expression ParseEquality()
    {
        var e = ParseComparison();
        while (CheckAny(TokenKind.EqEq, TokenKind.NotEq))
        {
            var op = Current.Kind == TokenKind.EqEq ? BinaryOp.Eq : BinaryOp.Neq;
            Advance();
            e = new BinaryExpr(op, e, ParseComparison(), e.Span);
        }
        return e;
    }

    private Expression ParseComparison()
    {
        var e = ParseAdd();
        while (true)
        {
            BinaryOp op;
            if (Check(TokenKind.Lt)) op = BinaryOp.Lt;
            else if (Check(TokenKind.Gt)) op = BinaryOp.Gt;
            else if (Check(TokenKind.Le)) op = BinaryOp.Le;
            else if (Check(TokenKind.Ge)) op = BinaryOp.Ge;
            else if (Check(TokenKind.SignedLt)) op = BinaryOp.SLt;
            else if (Check(TokenKind.SignedGt)) op = BinaryOp.SGt;
            else if (Check(TokenKind.SignedLe)) op = BinaryOp.SLe;
            else if (Check(TokenKind.SignedGe)) op = BinaryOp.SGe;
            else break;
            Advance();
            e = new BinaryExpr(op, e, ParseAdd(), e.Span);
        }
        return e;
    }

    private Expression ParseAdd()
    {
        var e = ParseMul();
        while (CheckAny(TokenKind.Plus, TokenKind.Minus))
        {
            var op = Current.Kind == TokenKind.Plus ? BinaryOp.Add : BinaryOp.Sub;
            Advance();
            e = new BinaryExpr(op, e, ParseMul(), e.Span);
        }
        return e;
    }

    // 仕様: * / MOD << >> .*. ./. .MOD. .<<. .>>. は同一優先順位
    private Expression ParseMul()
    {
        var e = ParseUnary();
        while (true)
        {
            BinaryOp op;
            if (Check(TokenKind.Star)) op = BinaryOp.Mul;
            else if (Check(TokenKind.Slash)) op = BinaryOp.Div;
            else if (Check(TokenKind.Mod)) op = BinaryOp.Mod;
            else if (Check(TokenKind.Shl)) op = BinaryOp.Shl;
            else if (Check(TokenKind.Shr)) op = BinaryOp.Shr;
            else if (Check(TokenKind.SignedMul)) op = BinaryOp.SMul;
            else if (Check(TokenKind.SignedDiv)) op = BinaryOp.SDiv;
            else if (Check(TokenKind.SignedMod)) op = BinaryOp.SMod;
            else if (Check(TokenKind.SignedShl)) op = BinaryOp.SShl;
            else if (Check(TokenKind.SignedShr)) op = BinaryOp.SShr;
            else break;
            Advance();
            e = new BinaryExpr(op, e, ParseUnary(), e.Span);
        }
        return e;
    }

    private Expression ParseUnary()
    {
        var s = Current.Span;

        if (CheckAny(TokenKind.PlusPlus, TokenKind.MinusMinus))
        {
            bool inc = Current.Kind == TokenKind.PlusPlus;
            Advance();
            return new IncrementExpr(ParseUnary(), inc, true, s);
        }
        if (Match(TokenKind.Minus))
            return new UnaryExpr(UnaryOp.Negate, ParseUnary(), s);
        if (Match(TokenKind.Plus))
            return new UnaryExpr(UnaryOp.Plus, ParseUnary(), s);
        if (Match(TokenKind.Not))
            return new UnaryExpr(UnaryOp.Not, ParseUnary(), s);
        if (Match(TokenKind.Cpl))
            return new UnaryExpr(UnaryOp.Cpl, ParseUnary(), s);
        if (Match(TokenKind.Ampersand))
            return new AddressOfExpr(ParseUnary(), s);
        if (Check(TokenKind.High)) { Advance(); return new HighLowExpr(true, ParseUnary(), s); }
        if (Check(TokenKind.Low)) { Advance(); return new HighLowExpr(false, ParseUnary(), s); }
        if (Match(TokenKind.Percent))
            return new CastExpr(DataSize.Word, ParseUnary(), s);
        if (Check(TokenKind.Code))
        {
            Advance();
            Expect(TokenKind.LParen);
            var codes = ParseCodeExprList();
            Expect(TokenKind.RParen);
            return new CodeExpr(codes, s);
        }

        return ParsePostfix();
    }

    private Expression ParsePostfix()
    {
        var e = ParsePrimary();
        while (true)
        {
            if (CheckAny(TokenKind.PlusPlus, TokenKind.MinusMinus))
            {
                bool inc = Current.Kind == TokenKind.PlusPlus;
                Advance();
                e = new IncrementExpr(e, inc, false, e.Span);
            }
            else if (Check(TokenKind.ArrayBracketOpen))
            {
                var indices = new List<Expression>();
                while (Check(TokenKind.ArrayBracketOpen))
                {
                    Advance();
                    indices.Add(ParseNcExpr());
                    Expect(TokenKind.RBracket, "Expected ']'");
                }
                e = new ArrayAccessExpr(e, indices, e.Span);
            }
            else if (Check(TokenKind.LParen))
            {
                Advance();
                var args = new List<Expression>();
                if (!Check(TokenKind.RParen))
                {
                    _argListDepth++;
                    args = ParseArgList();
                    _argListDepth--;
                }
                Expect(TokenKind.RParen, "Expected ')'");
                e = new CallExpr(e, args, e.Span);
            }
            else break;
        }
        return e;
    }

    private Expression ParsePrimary()
    {
        var s = Current.Span;

        if (Check(TokenKind.IntegerLiteral) || Check(TokenKind.CharLiteral))
        {
            var t = Advance();
            return new IntegerLiteral(t.IntValue, t.Span);
        }
        if (Check(TokenKind.FloatLiteral))
        {
            var t = Advance();
            return new FloatLiteral(t.FloatValue, t.Span);
        }
        if (Check(TokenKind.StringLiteral))
        {
            var t = Advance();
            return new StringLiteral(t.StringValue, t.Span);
        }
        if (Check(TokenKind.Identifier))
        {
            var t = Advance();
            // Handle TRUE/FALSE as constants
            if (t.Text.Equals("TRUE", StringComparison.OrdinalIgnoreCase))
                return new IntegerLiteral(1, t.Span);
            if (t.Text.Equals("FALSE", StringComparison.OrdinalIgnoreCase))
                return new IntegerLiteral(0, t.Span);
            return new IdentifierExpr(t.StringValue, t.Span);
        }
        if (Match(TokenKind.LParen))
        {
            var e = ParseExpr();
            Expect(TokenKind.RParen, "Expected ')'");
            return e;
        }

        Error($"Expected expression, got {Current.Kind} '{Current.Text}'");
        return new IntegerLiteral(0, s);
    }

    // ==== Helpers ====

    private List<Expression> ParseArgList()
    {
        var list = new List<Expression>();
        do { list.Add(ParseNcExpr()); } while (Match(TokenKind.Comma));
        return list;
    }

    private List<Expression> ParseCodeExprList()
    {
        var list = new List<Expression>();
        do { list.Add(ParseNcExpr()); } while (Match(TokenKind.Comma));
        return list;
    }

    private List<Expression> ParseCodeBlock()
    {
        ExpectBlockOpen();
        var list = ParseCodeExprList();
        ExpectBlockClose();
        return list;
    }

    private DataSize ParseOptionalDataSize()
    {
        if (Match(TokenKind.Byte) || Match(TokenKind.Exclamation)) return DataSize.Byte;
        if (Match(TokenKind.Word)) return DataSize.Word;
        if (Match(TokenKind.Float)) return DataSize.Float;
        return DataSize.Word;
    }

    private bool IsBlockOpen() => CheckAny(TokenKind.Begin, TokenKind.LBracket, TokenKind.LBrace, TokenKind.LAngleBracket);
    private bool IsBlockClose() => CheckAny(TokenKind.End, TokenKind.RBracket, TokenKind.RBrace, TokenKind.RAngleBracket);

    private void ExpectBlockOpen(string? msg = null)
    {
        if (IsBlockOpen()) { Advance(); return; }
        Error(msg ?? "Expected block open");
    }
    private void ExpectBlockClose(string? msg = null)
    {
        if (IsBlockClose()) { Advance(); return; }
        Error(msg ?? "Expected block close");
    }

    private bool IsDeclStart() => CheckAny(TokenKind.Var, TokenKind.Array, TokenKind.Const, TokenKind.Machine,
                                            TokenKind.Byte, TokenKind.Word);

    /// <summary>Match an identifier used as keyword (like AND, OR)</summary>
    private bool MatchIdent(string name)
    {
        if (Check(TokenKind.Identifier) && Current.Text.Equals(name, StringComparison.OrdinalIgnoreCase))
        {
            Advance();
            return true;
        }
        return false;
    }
}
