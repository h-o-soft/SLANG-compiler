using System.Globalization;
using System.Text;
using SLANGCompiler;
using SLANGCompiler.Lexer;
using SLANGCompiler.Parser.Ast;
using SLANGCompiler.Semantics;

namespace SLANGCompiler.CodeGen.C;

/// <summary>
/// AST visitor が返す統一型。
/// statement: <c>Text</c> は indent + trailing newline 込みの完成行群、<c>Type</c> は null。
/// expression: <c>Text</c> は親が必要に応じて括弧で囲める形 (CEmitter は厚めに <c>()</c> を付与済み)、
///             <c>Type</c> は SLANG 推論型 (PRINT dispatch や算術 wrap に使う)。
/// </summary>
public readonly record struct EmitResult(string Text, SlangType? Type);

/// <summary>
/// SLANG AST → oscar64 入力 C ソース へ変換する AST visitor。
///
/// 設計原則:
///   - 既存 <see cref="IR.IrGenerator"/> / <see cref="CodeGenerator"/> (Z80) は触らない
///   - oscar64 (16-bit int, 32-bit float) を前提に、SLANG WORD は <c>unsigned int</c>、
///     FLOAT は <c>float</c> にマップ
///   - WORD 算術は各演算ごとに <c>(unsigned int)</c> ラップして 16-bit wrap を保つ
///   - precedence 括弧は厚めに付与 (= 可読性より正確性優先、oscar64 parser 任せ)
///   - Z80 固有 (inline <c>#ASM</c> / <c>MACHINE</c> / <c>PortArrayType</c>) は診断 error
/// </summary>
public class CEmitter : IAstVisitor<EmitResult>
{
    // === CaseStmt range 展開の閾値 ===
    // (hi - lo + 1) <= この値なら `case v0: case v1: ...` を直接並べ、
    // それ以上は `if (e >= lo && e <= hi) { ... }` chain に落とす。
    // 0 TO 65535 のような巨大 range で .c ソースが爆発しないようにする保険。
    private const int CaseRangeExpansionThreshold = 16;

    private readonly SymbolTable? _globals;
    private readonly CScopeTracker _scope;
    private readonly DiagnosticBag _diagnostics;
    private readonly ConstEvaluator _constEval;
    private readonly CBindingRegistry _cBindings;
    private int _indent;
    // current 関数名 (= FuncDef 進入中なら名前、関数外 / global なら null)。
    // SLANG の関数内 static var (= BEGIN 前の VarDecl) の C 側 ident 衝突を
    // 避けるために funcName_varName で suffix 付ける。
    private string? _currentFuncName;
    // current FuncDef の static decl (BEGIN 前 VAR/ARRAY) 名集合。
    // body 内で同名識別子を見つけたとき StaticVarIdent (= V_funcName_name) で
    // 引けるようにする。FuncDef enter/leave で push/pop。
    private readonly HashSet<string> _currentStaticDecls = new(StringComparer.OrdinalIgnoreCase);

    public CEmitter(SymbolTable? globals, CScopeTracker scope, DiagnosticBag diagnostics,
                    CBindingRegistry? cBindings = null)
    {
        _globals = globals;
        _scope = scope;
        _diagnostics = diagnostics;
        // ARRAY 次元 / `#define` 値の compile-time 評価。CONST 識別子 + 算術式の解決
        // をしてくれる (= TryEvalIntLiteral の上位互換)。
        _constEval = new ConstEvaluator(globals);
        // env file `c_bindings:` 経由の binding lookup layer。null なら空 registry。
        _cBindings = cBindings ?? new CBindingRegistry();
    }

    // === ヘルパ ===

    private string Indent() => new(' ', _indent * 4);

    /// <summary>1 行 = indent + body + "\n"</summary>
    private string Line(string body) => Indent() + body + "\n";

    /// <summary>式の visit 結果を文字列に取り出す (Type 情報は捨てる)</summary>
    private string Expr(Expression e) => e.Accept(this).Text;

    /// <summary>式の visit 結果を tuple で取り出す</summary>
    private EmitResult ExprFull(Expression e) => e.Accept(this);

    /// <summary>SLANG DataSize → SlangType</summary>
    private static SlangType TypeOfDataSize(DataSize ds) => ds switch
    {
        DataSize.Byte => SlangType.Byte,
        DataSize.Word => SlangType.Word,
        DataSize.Float => SlangType.Float,
        _ => SlangType.Word,
    };

    /// <summary>FLOAT が絡むなら結果も FLOAT、Byte+Word は Word</summary>
    private static SlangType PromoteArith(SlangType? a, SlangType? b)
    {
        if (a is PrimitiveType { Kind: PrimitiveKind.Float }
            || b is PrimitiveType { Kind: PrimitiveKind.Float })
            return SlangType.Float;
        if (a is PrimitiveType { Kind: PrimitiveKind.Word }
            || b is PrimitiveType { Kind: PrimitiveKind.Word })
            return SlangType.Word;
        if (a is PrimitiveType { Kind: PrimitiveKind.Byte }
            && b is PrimitiveType { Kind: PrimitiveKind.Byte })
            return SlangType.Byte;
        return SlangType.Word;
    }

    /// <summary>
    /// WORD 算術の wrap キャスト (16-bit 演算 wrap 保証)。
    /// oscar64 の usual arithmetic conversion で 32-bit に逃げないように、
    /// Byte / Word 演算結果を毎回 cast し直す。Float は対象外。
    /// </summary>
    private static string WrapArith(string text, SlangType resultType)
    {
        var cast = CTypeMapper.WrapCastFor(resultType);
        return cast != null ? $"(({cast}){text})" : text;
    }

    private void Error(string message, SourceSpan span) => _diagnostics.Error(message, span);

    // === Top-level ===

    public EmitResult VisitCompilationUnit(CompilationUnit node)
    {
        var sb = new StringBuilder();

        // ヘッダ: slang_runtime.h を include (oscar64 -i= で path 解決される前提)
        sb.Append("#include \"slang_runtime.h\"\n\n");

        // 第 0 pass: CFUNC 宣言由来 (SymbolKind.CFunction) を extern 集約。
        // SLANG ソース全体を走査してから C 関数 prototype を出すため、
        // SymbolTable.GlobalScope の Resolve 結果を活用する。
        // 同じ c_name の重複は signature 一致なら 1 個に集約、不一致は error。
        var externs = BuildCFuncExterns();
        if (externs.Length > 0)
        {
            sb.Append("/* CFUNC declarations (slangc-generated) */\n");
            sb.Append(externs);
            sb.Append('\n');
        }

        // Parser は複数宣言 (`VAR X, Y;`) を Block にラップして返すため、
        // 全 pass で flatten してから種別判定する。
        var flatDefs = FlattenDecls(node.Definitions).ToList();

        // 第 1 pass: ConstDecl (= #define で先頭に集める)
        foreach (var def in flatDefs)
        {
            if (def is ConstDecl)
            {
                sb.Append(def.Accept(this).Text);
            }
        }

        // 第 2 pass: グローバル VarDecl / ArrayDecl
        bool anyGlobalVar = false;
        foreach (var def in flatDefs)
        {
            if (def is VarDecl || def is ArrayDecl)
            {
                sb.Append(def.Accept(this).Text);
                anyGlobalVar = true;
            }
        }
        if (anyGlobalVar) sb.Append('\n');

        // 第 3 pass: 関数 prototype を全て先に出す (前方参照対応、SLANG 仕様)
        bool anyFunc = false;
        foreach (var def in flatDefs)
        {
            if (def is FuncDef fd)
            {
                sb.Append(EmitFuncPrototype(fd));
                anyFunc = true;
            }
        }
        if (anyFunc) sb.Append('\n');

        // 第 4 pass: 関数本体
        FuncDef? mainFunc = null;
        foreach (var def in flatDefs)
        {
            if (def is FuncDef fd)
            {
                sb.Append(fd.Accept(this).Text);
                sb.Append('\n');
                if (fd.Name.Equals("MAIN", StringComparison.OrdinalIgnoreCase))
                    mainFunc = fd;
            }
            else if (def is MachineDecl md)
            {
                md.Accept(this);  // diagnostics に error を積む
            }
            else if (def is PlainAsm pa)
            {
                pa.Accept(this);
            }
            else if (def is ModuleBlock mb)
            {
                mb.Accept(this);
            }
            else if (def is OrgDirective or WorkDirective or OffsetDirective)
            {
                // v1 では no-op (warn なし、必要なら --verbose で note 追加)
            }
        }

        // C エントリーポイント: int main(void) { F_MAIN(); return 0; }
        if (mainFunc != null)
        {
            sb.Append("int main(void)\n{\n");
            sb.Append($"    F_{IdentifierMap.Sanitize(mainFunc.Name)}();\n");
            sb.Append("    return 0;\n");
            sb.Append("}\n");
        }
        else
        {
            // MAIN なしは error (Z80 backend と同じ規約)
            Error("MAIN function is required", node.Span);
        }

        return new EmitResult(sb.ToString(), null);
    }

    public EmitResult VisitOrgDirective(OrgDirective node) => new("", null);
    public EmitResult VisitWorkDirective(WorkDirective node) => new("", null);
    public EmitResult VisitOffsetDirective(OffsetDirective node) => new("", null);

    public EmitResult VisitModuleBlock(ModuleBlock node)
    {
        Error("`#MODULE` (overlay) is not supported by oscar_c backend in v1", node.Span);
        return new("", null);
    }

    public EmitResult VisitPlainAsm(PlainAsm node)
    {
        Error("inline `#ASM` block is not supported by oscar_c backend; gate it with `#IF ENV_TYPE==7` or `#IF BACKEND==1`", node.Span);
        return new("", null);
    }

    // === Declarations ===

    public EmitResult VisitVarDecl(VarDecl node)
    {
        var slangType = TypeOfDataSize(node.Size);
        var cType = CTypeMapper.MapDeclType(slangType);
        var ident = VarIdent(node.Name);

        if (node.InitialCode != null)
        {
            Error("`= { code... }` initializer is not supported by oscar_c backend (uses Z80-specific MACHINE code)", node.Span);
            return new("", null);
        }

        string init;
        if (node.InitialValue != null)
        {
            init = " = " + CastTo(ExprFull(node.InitialValue), slangType);
        }
        else if (node.Address != null)
        {
            // VAR x:address → ポインタとして扱う (= MEM 直アクセス)。
            // ただし SLANG では VAR x:$3000 は固定アドレスに変数を置く意味だが、
            // oscar64 では link 配置を強制できないので **同等の意味の固定アドレス
            // ポインタ** に変換: static T* V_x = (T*)0x3000; ではなく、
            // #define で expr 化する方が安全。
            // v1 では明示 error (= 利用例少なめ、SLANG ENV_TYPE で gate 想定)。
            Error($"VAR `{node.Name}:address` (固定アドレス変数) は oscar_c backend では未サポート (v1)。MEM[$addr] 経由でアクセスしてください", node.Span);
            return new("", null);
        }
        else
        {
            init = " = " + CTypeMapper.ZeroInitializer(slangType);
        }

        if (_scope.ScopeDepth == 0)
        {
            // global var
            return new(Line($"static {cType} {ident}{init};"), null);
        }
        else
        {
            // local var. CScopeTracker にも登録。
            _scope.DeclareLocal(node.Name, slangType);
            return new(Line($"{cType} {ident}{init};"), null);
        }
    }

    public EmitResult VisitArrayDecl(ArrayDecl node)
    {
        var slangType = TypeOfDataSize(node.Size);
        var cType = CTypeMapper.MapDeclType(slangType);
        var ident = VarIdent(node.Name);

        // 次元サイズ: 全て静的整数 const literal 想定 (SLANG semantic で保証)。
        // 1 つでも null = 間接配列 (= ポインタ)、宣言は static T *A_name;
        bool isIndirect = node.Dimensions.Any(d => d == null);

        if (node.InitialCode != null)
        {
            Error("`= { code... }` initializer is not supported by oscar_c backend (uses Z80-specific MACHINE code)", node.Span);
            return new("", null);
        }

        // ARRAY x:address → static T *A_x = (T*)addr;
        if (node.Address != null)
        {
            var addrText = Expr(node.Address);
            var line = _scope.ScopeDepth == 0
                ? Line($"static {cType} *{ident} = ({cType} *){addrText};")
                : Line($"{cType} *{ident} = ({cType} *){addrText};");
            if (_scope.ScopeDepth > 0)
                _scope.DeclareLocal(node.Name, new PointerType(slangType));
            return new(line, null);
        }

        if (isIndirect)
        {
            var line = _scope.ScopeDepth == 0
                ? Line($"static {cType} *{ident};")
                : Line($"{cType} *{ident};");
            if (_scope.ScopeDepth > 0)
                _scope.DeclareLocal(node.Name, new PointerType(slangType));
            return new(line, null);
        }

        // 固定サイズ配列。各 dim は static const literal 想定 (SLANG semantic で保証)。
        // SLANG 仕様: `ARRAY A[N]` は要素数 **N+1** (index 0..N 有効、C と異なる)。
        // SemanticAnalyzer.cs:194 / IrGenerator.cs:397/404 と同じく `+1` する。
        var dimsSb = new StringBuilder();
        var dimList = new List<int>();
        foreach (var dim in node.Dimensions)
        {
            // null は isIndirect で先に処理済み
            var evaluated = _constEval.Evaluate(dim!);
            if (evaluated == null)
            {
                Error("ARRAY dimension must be a compile-time constant expression", dim!.Span);
                evaluated = 0;
            }
            int allocSize = evaluated.Value + 1;
            dimsSb.Append('[').Append(allocSize).Append(']');
            dimList.Add(allocSize);
        }

        string init = "";
        if (node.InitialValue != null)
        {
            init = " = " + Expr(node.InitialValue);
        }

        var declLine = _scope.ScopeDepth == 0
            ? Line($"static {cType} {ident}{dimsSb}{init};")
            : Line($"{cType} {ident}{dimsSb}{init};");
        if (_scope.ScopeDepth > 0)
            _scope.DeclareLocal(node.Name, new ArrayType(slangType, dimList));
        return new(declLine, null);
    }

    public EmitResult VisitConstDecl(ConstDecl node)
    {
        if (node.IsAsmEqu)
        {
            Error("`CONST ASM` is not supported by oscar_c backend in v1", node.Span);
            return new("", null);
        }
        var ident = ConstIdent(node.Name);
        var valText = Expr(node.Value);
        // #define で出す (oscar64 の constexpr 評価が確実)
        return new(Line($"#define {ident} ({valText})"), null);
    }

    public EmitResult VisitCFuncDecl(CFuncDecl node)
    {
        // CFUNC 宣言自体は emit なし。extern 宣言の集約 emit は Commit 2 で
        // VisitCompilationUnit に BuildCFuncExterns pass を追加するときに行う。
        // 呼び出しは VisitCallExpr の SymbolKind.CFunction 経路で resolve (Commit 2 実装)。
        return new("", null);
    }

    public EmitResult VisitMachineDecl(MachineDecl node)
    {
        Error($"MACHINE declaration `{node.Name}` is not supported by oscar_c backend; gate it with `#IF ENV_TYPE==7` or `#IF BACKEND==1`", node.Span);
        return new("", null);
    }

    public EmitResult VisitFuncDef(FuncDef node)
    {
        var retType = CTypeMapper.MapDeclType(TypeOfDataSize(node.ReturnSize));
        var funcIdent = FuncIdent(node.Name);
        var paramSig = BuildParamSignature(node.Parameters);

        _currentFuncName = node.Name;
        _scope.EnterFunction();
        _currentStaticDecls.Clear();

        // params を CScopeTracker に登録
        foreach (var p in node.Parameters)
        {
            var paramType = p.IsArray
                ? (SlangType)new PointerType(TypeOfDataSize(p.Size))
                : TypeOfDataSize(p.Size);
            _scope.DeclareLocal(p.Name, paramType);
        }

        // parser が `VAR X, Y;` を Block にラップするため flatten してから処理。
        var flatStatic = FlattenDecls(node.StaticDeclarations).ToList();
        var flatLocal = FlattenDecls(node.LocalDeclarations).ToList();

        // static decl の名前を先に集約 (= body 内識別子参照で StaticVarIdent を引くために)
        foreach (var stat in flatStatic)
        {
            string? name = stat switch
            {
                VarDecl v => v.Name,
                ArrayDecl a => a.Name,
                _ => null,
            };
            if (name != null) _currentStaticDecls.Add(name);
        }

        var sb = new StringBuilder();
        sb.Append(Line($"static {retType} {funcIdent}({paramSig})"));
        sb.Append(Line("{"));
        _indent++;

        // static decl (BEGIN 前) → C の関数内 static 変数
        foreach (var stat in flatStatic)
        {
            sb.Append(EmitStaticDecl(stat));
        }
        // local decl (BEGIN 後) → 関数冒頭の通常変数
        foreach (var local in flatLocal)
        {
            sb.Append(local.Accept(this).Text);
        }

        // body
        // Block は visit すると `{` / `}` を出してしまうので、Block.Statements を直に処理
        if (node.Body is Block body)
        {
            foreach (var stmt in body.Statements)
            {
                sb.Append(stmt.Accept(this).Text);
            }
        }
        else
        {
            sb.Append(node.Body.Accept(this).Text);
        }

        if (node.ReturnValue != null)
        {
            var retTypeSlang = TypeOfDataSize(node.ReturnSize);
            sb.Append(Line($"return {CastTo(ExprFull(node.ReturnValue), retTypeSlang)};"));
        }
        else
        {
            // Void 以外は暗黙 0 return を補う (SLANG の慣行 + oscar64 warning 抑制)。
            // MAIN も F_MAIN 自体は unsigned int 戻り型なので return を入れる。
            // (= 外側 int main(void) wrapper が return 0; 出すのとは別)。
            // oscar64 は float リテラル `0.0f` を受け付けないため `0.0` を使う。
            if (node.ReturnSize == DataSize.Float)
                sb.Append(Line("return 0.0;"));
            else
                sb.Append(Line("return 0;"));
        }

        _indent--;
        sb.Append(Line("}"));

        _scope.LeaveFunction();
        _currentFuncName = null;
        _currentStaticDecls.Clear();

        return new(sb.ToString(), null);
    }

    public EmitResult VisitParamDecl(ParamDecl node)
    {
        // ParamDecl 単独の visit は使わない (FuncDef 側で BuildParamSignature 経由)
        return new("", null);
    }

    private string EmitFuncPrototype(FuncDef fd)
    {
        var retType = CTypeMapper.MapDeclType(TypeOfDataSize(fd.ReturnSize));
        var funcIdent = FuncIdent(fd.Name);
        var sig = BuildParamSignature(fd.Parameters);
        return $"static {retType} {funcIdent}({sig});\n";
    }

    private string BuildParamSignature(List<ParamDecl> parameters)
    {
        if (parameters.Count == 0) return "void";
        var parts = new List<string>();
        foreach (var p in parameters)
        {
            var ptype = TypeOfDataSize(p.Size);
            var cType = CTypeMapper.MapDeclType(ptype);
            if (p.IsArray) cType += " *";
            parts.Add($"{cType} {VarIdent(p.Name)}");
        }
        return string.Join(", ", parts);
    }

    private string EmitStaticDecl(AstNode decl)
    {
        // FuncDef の StaticDeclarations: BEGIN 前の VAR/ARRAY/CONST。
        // C の関数内 `static` 変数として出す。識別子は funcName_varName で衝突回避。
        if (decl is VarDecl vd)
        {
            var slangType = TypeOfDataSize(vd.Size);
            var cType = CTypeMapper.MapDeclType(slangType);
            var ident = StaticVarIdent(_currentFuncName!, vd.Name);
            _scope.DeclareLocal(vd.Name, slangType);

            string init;
            if (vd.InitialValue != null)
                init = " = " + CastTo(ExprFull(vd.InitialValue), slangType);
            else
                init = " = " + CTypeMapper.ZeroInitializer(slangType);

            return Line($"static {cType} {ident}{init};");
        }
        if (decl is ArrayDecl ad)
        {
            // 関数内 static / 関数内 local の VAR/ARRAY 宣言。
            var slangType = TypeOfDataSize(ad.Size);
            var cType = CTypeMapper.MapDeclType(slangType);
            var ident = StaticVarIdent(_currentFuncName!, ad.Name);

            // 間接配列 (VAR BYTE T[];) — 関数内でもポインタとして扱う。
            // INDTEST.SL の TEST1 で T = ADR; とアドレスを後から代入するパターン。
            // local 間接配列は本来 static にする必要はないが、SLANG の static decl
            // (BEGIN 前) に書かれている場合は static にする。
            if (ad.Dimensions.Any(d => d == null))
            {
                _scope.DeclareLocal(ad.Name, new PointerType(slangType));
                return Line($"static {cType} *{ident};");
            }

            // 固定サイズ配列。SLANG 仕様: `ARRAY A[N]` は要素数 N+1 (index 0..N 有効)。
            var dimsSb = new StringBuilder();
            var dimList = new List<int>();
            foreach (var dim in ad.Dimensions)
            {
                var evaluated = _constEval.Evaluate(dim!);
                if (evaluated == null)
                {
                    Error("ARRAY dimension must be a compile-time constant expression", dim!.Span);
                    evaluated = 0;
                }
                int allocSize = evaluated.Value + 1;
                dimsSb.Append('[').Append(allocSize).Append(']');
                dimList.Add(allocSize);
            }
            _scope.DeclareLocal(ad.Name, new ArrayType(slangType, dimList));
            string init = ad.InitialValue != null ? " = " + Expr(ad.InitialValue) : "";
            return Line($"static {cType} {ident}{dimsSb}{init};");
        }
        if (decl is ConstDecl cd)
        {
            return decl.Accept(this).Text;  // VisitConstDecl と同じ
        }
        Error($"unsupported static declaration in function body: {decl.GetType().Name}", decl.Span);
        return "";
    }

    // === Statements ===

    public EmitResult VisitBlock(Block node)
    {
        var sb = new StringBuilder();
        sb.Append(Line("{"));
        _indent++;
        foreach (var stmt in node.Statements)
        {
            sb.Append(stmt.Accept(this).Text);
        }
        _indent--;
        sb.Append(Line("}"));
        return new(sb.ToString(), null);
    }

    public EmitResult VisitExpressionStmt(ExpressionStmt node)
    {
        var e = Expr(node.Expr);
        return new(Line($"{e};"), null);
    }

    public EmitResult VisitIfStmt(IfStmt node)
    {
        var sb = new StringBuilder();
        for (int i = 0; i < node.Branches.Count; i++)
        {
            var (cond, body) = node.Branches[i];
            var prefix = i == 0 ? "if" : "else if";
            sb.Append(Line($"{prefix} ({Expr(cond)})"));
            sb.Append(EmitStmtAsBlock(body));
        }
        if (node.ElseBody != null)
        {
            sb.Append(Line("else"));
            sb.Append(EmitStmtAsBlock(node.ElseBody));
        }
        return new(sb.ToString(), null);
    }

    public EmitResult VisitWhileStmt(WhileStmt node)
    {
        var sb = new StringBuilder();
        sb.Append(Line($"while ({Expr(node.Condition)})"));
        sb.Append(EmitStmtAsBlock(node.Body));
        return new(sb.ToString(), null);
    }

    public EmitResult VisitRepeatStmt(RepeatStmt node)
    {
        // REPEAT...UNTIL c → do {} while (!(c));
        var sb = new StringBuilder();
        sb.Append(Line("do"));
        sb.Append(EmitStmtAsBlock(node.Body));
        sb.Append(Line($"while (!({Expr(node.Condition)}));"));
        return new(sb.ToString(), null);
    }

    public EmitResult VisitLoopStmt(LoopStmt node)
    {
        var sb = new StringBuilder();
        sb.Append(Line("for (;;)"));
        sb.Append(EmitStmtAsBlock(node.Body));
        return new(sb.ToString(), null);
    }

    public EmitResult VisitForStmt(ForStmt node)
    {
        // SLANG FOR semantics:
        //   1. body 開始前に I を比較 (TO なら I<=end、DOWNTO なら I>=end)、満たさなければ skip
        //   2. body 実行 (body 内で I を書き換えても OK)
        //   3. body 後に I を step (++/--)
        //   4. 1 へ戻る
        //   - 自然終了 (EXIT 無し) で I=end+step (= `FOR I=0 TO 15` → I=16)、
        //     post-loop の `IF I<=15` で natural / EXIT を区別できる
        //   - body 内で I++ している場合は実質 step=2 で正しく end まで進む
        //   - from > to (downto なら from < to) なら body 0 回 (skip)
        //
        // 生成 form (= 標準的 while ループ):
        //   {
        //       T _for_end_0 = (to);
        //       loopVar = (from);
        //       while (loopVar {<=,>=} _for_end_0) {
        //           body
        //           {step};
        //           // wrap 防御: TO で 0xFFFF→0、DOWNTO で 0→0xFFFF した瞬間に break
        //           if (loopVar == {wrap_sentinel}) break;
        //       }
        //   }
        // wrap_sentinel: TO は 0、DOWNTO は 0xFFFF。SLANG WORD 範囲全体を覆う FOR で
        // 標準 C `for` が無限ループになる端ケースを救う (= `FOR I=0 TO 0xFFFF` 等)。
        // `==` 比較 + step ジャンプ越えの旧 form は manual `I++` 等の body 内 step
        // 変化に弱かったため、`<=` 比較ベースに揃える。
        var varType = _scope.Resolve(node.Variable) ?? SlangType.Word;
        var loopVar = VarIdent(node.Variable);
        var fromText = CastTo(ExprFull(node.From), varType);
        var toText = CastTo(ExprFull(node.To), varType);

        var sb = new StringBuilder();
        var endIdent = NewTempIdent("_for_end");
        var cType = CTypeMapper.MapDeclType(varType);
        var stepOp = node.IsDownTo ? "--" : "++";
        var cmpOp = node.IsDownTo ? ">=" : "<=";
        // wrap sentinel: BYTE は 0xFF / 0、WORD は 0xFFFF / 0
        string wrapSentinel;
        if (varType is PrimitiveType { Kind: PrimitiveKind.Byte })
            wrapSentinel = node.IsDownTo ? "0xFFu" : "0u";
        else
            wrapSentinel = node.IsDownTo ? "0xFFFFu" : "0u";

        sb.Append(Line("{"));
        _indent++;
        sb.Append(Line($"{cType} {endIdent} = {toText};"));
        sb.Append(Line($"{loopVar} = {fromText};"));
        sb.Append(Line($"while ({loopVar} {cmpOp} {endIdent}) {{"));
        _indent++;
        sb.Append(EmitStmtInner(node.Body));
        sb.Append(Line($"{stepOp}{loopVar};"));
        sb.Append(Line($"if ({loopVar} == {wrapSentinel}) break;"));
        _indent--;
        sb.Append(Line("}"));
        _indent--;
        sb.Append(Line("}"));
        return new(sb.ToString(), null);
    }

    public EmitResult VisitCaseStmt(CaseStmt node)
    {
        // SLANG CASE expr OF v1: body; v2 TO v3: body; OTHERS: body; ENDCASE
        // → C switch (expr) { case v1: body; break; case v2: case v3: body; break; default: body; }
        // range は閾値 16 以下なら case 展開、それ以上なら if chain (ただし switch 内では使えないので
        // 大きい range が混じる場合は全体を if chain に切り替える)。
        var exprText = Expr(node.Expr);

        // 全 branch を解析: 各 branch が小 range / 大 range / 単値 / others のどれか判定。
        bool anyLargeRange = false;
        foreach (var b in node.Branches)
        {
            if (b.RangeEnd != null)
            {
                if (TryEvalIntLiteral(b.Value, out var lo) && TryEvalIntLiteral(b.RangeEnd, out var hi))
                {
                    if (hi - lo + 1 > CaseRangeExpansionThreshold) { anyLargeRange = true; break; }
                }
                else
                {
                    // 範囲端を const literal で評価できなければ if chain 化
                    anyLargeRange = true;
                    break;
                }
            }
        }

        var sb = new StringBuilder();
        if (anyLargeRange)
        {
            sb.Append(EmitCaseAsIfChain(node, exprText));
        }
        else
        {
            sb.Append(EmitCaseAsSwitch(node, exprText));
        }
        return new(sb.ToString(), null);
    }

    private string EmitCaseAsSwitch(CaseStmt node, string exprText)
    {
        var sb = new StringBuilder();
        sb.Append(Line($"switch ({exprText})"));
        sb.Append(Line("{"));
        _indent++;
        // SLANG の CaseBranch は body == null だと「次の body 付き branch にフォールスルー」
        // 例: 1, 2, 3: body  → case 1: case 2: case 3: body; break;
        // accumulator で値を貯めて、body が出てきたら一気に出す。
        var pendingValues = new List<string>();
        foreach (var b in node.Branches)
        {
            if (b.Value == null)
            {
                // OTHERS
                if (b.Body != null)
                {
                    sb.Append(Line("default:"));
                    _indent++;
                    sb.Append(EmitStmtInner(b.Body));
                    sb.Append(Line("break;"));
                    _indent--;
                }
                continue;
            }
            // case 値: 単値 or range
            if (b.RangeEnd != null)
            {
                if (TryEvalIntLiteral(b.Value, out var lo) && TryEvalIntLiteral(b.RangeEnd, out var hi))
                {
                    for (long v = lo; v <= hi; v++)
                        pendingValues.Add($"((unsigned int){v}u)");
                }
                else
                {
                    Error("CASE range bounds must be integer literals for case expansion", b.Value!.Span);
                }
            }
            else
            {
                pendingValues.Add(Expr(b.Value));
            }

            if (b.Body != null)
            {
                foreach (var v in pendingValues)
                {
                    sb.Append(Line($"case {v}:"));
                }
                pendingValues.Clear();
                _indent++;
                sb.Append(EmitStmtInner(b.Body));
                sb.Append(Line("break;"));
                _indent--;
            }
        }
        _indent--;
        sb.Append(Line("}"));
        return sb.ToString();
    }

    private string EmitCaseAsIfChain(CaseStmt node, string exprText)
    {
        // 大 range / 非 literal range が混じる場合: if (e == v) / if (e >= lo && e <= hi) chain
        // 値の累積 fallthrough は OR で連結。
        var sb = new StringBuilder();
        var pendingConds = new List<string>();
        bool firstBranch = true;
        AstNode? othersBody = null;
        foreach (var b in node.Branches)
        {
            if (b.Value == null)
            {
                if (b.Body != null) othersBody = b.Body;
                continue;
            }
            string cond;
            if (b.RangeEnd != null)
            {
                var lo = Expr(b.Value);
                var hi = Expr(b.RangeEnd);
                cond = $"(({exprText}) >= ({lo}) && ({exprText}) <= ({hi}))";
            }
            else
            {
                cond = $"(({exprText}) == ({Expr(b.Value)}))";
            }
            pendingConds.Add(cond);

            if (b.Body != null)
            {
                var combined = string.Join(" || ", pendingConds);
                pendingConds.Clear();
                var head = firstBranch ? "if" : "else if";
                firstBranch = false;
                sb.Append(Line($"{head} ({combined})"));
                sb.Append(EmitStmtAsBlock(b.Body));
            }
        }
        if (othersBody != null)
        {
            sb.Append(Line(firstBranch ? "if (1)" : "else"));
            sb.Append(EmitStmtAsBlock(othersBody));
        }
        return sb.ToString();
    }

    public EmitResult VisitExitStmt(ExitStmt node)
    {
        if (node.TargetLabel != null)
        {
            return new(Line($"goto {LabelIdent(node.TargetLabel)};"), null);
        }
        // Level: null or 1 = break、それ以上は error (v1)
        if (node.Level == null) return new(Line("break;"), null);
        if (TryEvalIntLiteral(node.Level, out var lv) && lv == 1)
            return new(Line("break;"), null);
        Error("multi-level EXIT (level >= 2) is not supported by oscar_c backend in v1; use a labeled loop and EXIT TO label", node.Span);
        return new(Line("break;  /* unsupported EXIT level */"), null);
    }

    public EmitResult VisitContinueStmt(ContinueStmt node) => new(Line("continue;"), null);

    public EmitResult VisitReturnStmt(ReturnStmt node)
    {
        if (node.Value == null) return new(Line("return;"), null);
        return new(Line($"return ({Expr(node.Value)});"), null);
    }

    public EmitResult VisitGotoStmt(GotoStmt node) => new(Line($"goto {LabelIdent(node.Label)};"), null);
    public EmitResult VisitLabelStmt(LabelStmt node) => new(Line($"{LabelIdent(node.Label)}:;"), null);

    public EmitResult VisitPrintStmt(PrintStmt node)
    {
        var sb = new StringBuilder();
        foreach (var arg in node.Arguments)
        {
            sb.Append(EmitPrintArg(arg));
        }
        return new(sb.ToString(), null);
    }

    private string EmitPrintArg(Expression arg)
    {
        // SLANG PRINT 仕様 (docs/SLANG-spec.md より):
        //   "..."        → 文字列出力 (literal は slang_print_str)
        //   /            → 改行
        //   値           → 10進左詰め出力 (= unsigned int + 改行なし)
        //   !(s) / MSX$(s) → NUL-terminated 文字列をそのまま出力
        //   %(v) / PN$(v)  → 符号付き10進左詰め出力
        //   FORM$(v,n)   → 10進 n 桁右詰め
        //   DECI$(v)     → 10進 5 桁右詰め
        //   HEX2$(v)     → 16進 2 桁
        //   HEX4$(v)     → 16進 4 桁
        //   MSG$(addr)   → CR-terminated 文字列出力
        //   STR$(c,n)    → 1 文字 c を n 回出力
        //   CHR$(n)      → 上位・下位 byte の順に ASCII 出力 (2 byte)
        //   SPC$(n)      → 空白を n 個
        //   CR$(n)       → 改行を n 個
        //   TAB$(n)      → カーソルを n 回右移動 (相対)
        //
        // 副作用専用構文 (戻り値なし) は print 系 C 関数に直接 dispatch する。
        // 戻り値あり (char* return) で式コンテキストでも使われるものは
        // <c>slang_print_str(slang_xxx(...))</c> パターンで wrap する。
        if (arg is StringFuncExpr sfx)
        {
            switch (sfx.FuncName)
            {
                case "/":
                    return Line("slang_println();");

                case "!":
                case "MSX$":
                    // NUL-terminated 文字列: 引数は const char* (StringLiteral / address)
                    if (sfx.Arguments.Count == 1)
                    {
                        var s = Expr(sfx.Arguments[0]);
                        return Line($"slang_print_str((const char *)({s}));");
                    }
                    break;

                case "%":
                case "PN$":
                    // 符号付き10進。WORD は signed 化して slang_print_sint。
                    if (sfx.Arguments.Count == 1)
                    {
                        var v = Expr(sfx.Arguments[0]);
                        return Line($"slang_print_sint((int)(short)({v}));");
                    }
                    break;

                case "FORM$":
                    // PRINT FORM$(v, n) → 10進 n 桁右詰め
                    if (sfx.Arguments.Count == 2)
                    {
                        var v = Expr(sfx.Arguments[0]);
                        var w = Expr(sfx.Arguments[1]);
                        return Line($"slang_print_form((int)(short)({v}), (unsigned char)({w}));");
                    }
                    break;

                case "DECI$":
                    // 5 桁右詰め (Z80 backend 仕様)。slang_print_deci に wrap。
                    if (sfx.Arguments.Count == 1)
                    {
                        var v = Expr(sfx.Arguments[0]);
                        return Line($"slang_print_deci((int)(short)({v}));");
                    }
                    break;

                case "HEX2$":
                    if (sfx.Arguments.Count == 1)
                        return Line($"slang_print_hex_b((unsigned char)({Expr(sfx.Arguments[0])}));");
                    break;
                case "HEX4$":
                    if (sfx.Arguments.Count == 1)
                        return Line($"slang_print_hex_w((unsigned int)({Expr(sfx.Arguments[0])}));");
                    break;

                case "MSG$":
                    // CR-terminated 文字列出力
                    if (sfx.Arguments.Count == 1)
                        return Line($"slang_print_msg((const char *)({Expr(sfx.Arguments[0])}));");
                    break;

                case "STR$":
                    // 1 文字 c を n 回
                    if (sfx.Arguments.Count == 2)
                        return Line($"slang_print_str_n((unsigned char)({Expr(sfx.Arguments[0])}), (unsigned int)({Expr(sfx.Arguments[1])}));");
                    break;

                case "CHR$":
                    // 上位・下位 byte の順に出力 (= 2 byte) ← Z80 仕様
                    if (sfx.Arguments.Count == 1)
                        return Line($"slang_print_chr_w((unsigned int)({Expr(sfx.Arguments[0])}));");
                    break;

                case "SPC$":
                    if (sfx.Arguments.Count == 1)
                        return Line($"slang_print_spc((unsigned int)({Expr(sfx.Arguments[0])}));");
                    break;
                case "CR$":
                    if (sfx.Arguments.Count == 1)
                        return Line($"slang_print_cr((unsigned int)({Expr(sfx.Arguments[0])}));");
                    break;
                case "TAB$":
                    if (sfx.Arguments.Count == 1)
                        return Line($"slang_print_tab_n((unsigned int)({Expr(sfx.Arguments[0])}));");
                    break;
                case "FL$":
                    if (sfx.Arguments.Count == 1)
                        return Line($"slang_print_float((float)({Expr(sfx.Arguments[0])}));");
                    break;
            }
            // 上記 case に該当しないか引数数不一致 → fall through (= 通常 expr 経路)
        }

        var r = ExprFull(arg);
        if (r.Type is PrimitiveType { Kind: PrimitiveKind.Float })
            return Line($"slang_print_float({r.Text});");
        if (r.Type is PointerType || arg is StringLiteral)
            return Line($"slang_print_str({r.Text});");
        // 既定: WORD として 10 進 unsigned 出力 (= SLANG `PRINT(値)` の挙動)
        return Line($"slang_print_int((unsigned int)({r.Text}));");
    }

    // === Expressions ===

    public EmitResult VisitIntegerLiteral(IntegerLiteral node)
    {
        var v = node.Value;
        if (v < 0)
        {
            // 負値は UnaryExpr(Negate, IntegerLiteral) として来るのが SLANG の常、
            // 念のためここでも対応。
            return new($"((int)({v.ToString(CultureInfo.InvariantCulture)}))", SlangType.Word);
        }
        if (v <= 0xFFFF)
        {
            // 16-bit 範囲: unsigned int リテラル + キャスト
            return new($"((unsigned int)0x{v:X4}u)", SlangType.Word);
        }
        // 16-bit 範囲外: 警告して unsigned long 化 (= 厳密には WORD ではないので
        // 後段で usual arithmetic conversion による 32-bit 拡張が起きる可能性あり)
        _diagnostics.Warning($"integer literal {v} exceeds 16-bit range; oscar_c backend will widen to unsigned long", node.Span);
        return new($"((unsigned long){v}UL)", SlangType.Word);
    }

    public EmitResult VisitFloatLiteral(FloatLiteral node)
    {
        // oscar64 は float リテラルの `f` suffix を受け付けない (ANSI C と差異)。
        // 整数表記 (`1`) は int になるため、必ず `.` を含める形にする。
        var s = node.Value.ToString("R", CultureInfo.InvariantCulture);
        if (!s.Contains('.') && !s.Contains('e') && !s.Contains('E')) s += ".0";
        return new(s, SlangType.Float);
    }

    public EmitResult VisitStringLiteral(StringLiteral node)
    {
        var encoded = CStringEncoder.Encode(node.Value);
        return new(encoded, new PointerType(SlangType.Byte));
    }

    public EmitResult VisitIdentifier(IdentifierExpr node)
    {
        var resolvedType = _scope.Resolve(node.Name) ?? SlangType.Word;
        // 解決順:
        //   1. current FuncDef の static decl (BEGIN 前) → V_funcName_name
        //   2. global SymbolTable の Constant / Function / MachineFunction → 専用 prefix
        //   3. それ以外 (local var / param / global var) → V_name
        string ident;
        if (_currentFuncName != null && _currentStaticDecls.Contains(node.Name))
        {
            ident = StaticVarIdent(_currentFuncName, node.Name);
            return new(ident, resolvedType);
        }
        var sym = _globals?.GlobalScope.Resolve(node.Name);
        if (sym?.Kind == SymbolKind.Constant)
        {
            ident = ConstIdent(node.Name);
        }
        else if (sym?.Kind == SymbolKind.Function || sym?.Kind == SymbolKind.MachineFunction)
        {
            ident = FuncIdent(node.Name);
        }
        else
        {
            ident = VarIdent(node.Name);
        }
        return new(ident, resolvedType);
    }

    public EmitResult VisitBinaryExpr(BinaryExpr node)
    {
        var left = ExprFull(node.Left);
        var right = ExprFull(node.Right);
        var lType = left.Type ?? SlangType.Word;
        var rType = right.Type ?? SlangType.Word;

        // 比較系 (Eq/Neq/Lt/...) は結果が「真偽値 (int)」だが、SLANG では WORD 扱い
        // が便利なので Type = Word とする。
        switch (node.Op)
        {
            case BinaryOp.Eq:
            case BinaryOp.Neq:
            case BinaryOp.Lt:
            case BinaryOp.Gt:
            case BinaryOp.Le:
            case BinaryOp.Ge:
                {
                    var cop = node.Op switch
                    {
                        BinaryOp.Eq => "==",
                        BinaryOp.Neq => "!=",
                        BinaryOp.Lt => "<",
                        BinaryOp.Gt => ">",
                        BinaryOp.Le => "<=",
                        BinaryOp.Ge => ">=",
                        _ => "??"
                    };
                    return new($"(({left.Text}) {cop} ({right.Text}))", SlangType.Word);
                }
            case BinaryOp.SLt:
            case BinaryOp.SGt:
            case BinaryOp.SLe:
            case BinaryOp.SGe:
                {
                    var cop = node.Op switch
                    {
                        BinaryOp.SLt => "<",
                        BinaryOp.SGt => ">",
                        BinaryOp.SLe => "<=",
                        BinaryOp.SGe => ">=",
                        _ => "??"
                    };
                    return new($"((short)({left.Text}) {cop} (short)({right.Text}))", SlangType.Word);
                }
            case BinaryOp.LogAnd:
                return new($"(({left.Text}) && ({right.Text}))", SlangType.Word);
            case BinaryOp.LogOr:
                return new($"(({left.Text}) || ({right.Text}))", SlangType.Word);
        }

        // 算術/ビット系
        var result = PromoteArith(lType, rType);

        // FLOAT 混在の場合、整数側を signed cast 経由で float に promote する
        // (Z80 backend の i16tof24 と同じセマンティクス)。
        // この変換をしないと unsigned int の値が wrap した状態 (例: -39 → 0xFFD9)
        // で float に上がってしまい、計算結果が壊れる。
        string leftText = left.Text;
        string rightText = right.Text;
        if (result is PrimitiveType { Kind: PrimitiveKind.Float })
        {
            leftText = PromoteToFloatSigned(leftText, lType);
            rightText = PromoteToFloatSigned(rightText, rType);
        }

        string text;
        switch (node.Op)
        {
            case BinaryOp.Add: text = $"({leftText}) + ({rightText})"; break;
            case BinaryOp.Sub: text = $"({leftText}) - ({rightText})"; break;
            case BinaryOp.Mul: text = $"({leftText}) * ({rightText})"; break;
            case BinaryOp.Div: text = $"({leftText}) / ({rightText})"; break;
            case BinaryOp.Mod: text = $"({leftText}) % ({rightText})"; break;
            case BinaryOp.SMul:
                text = $"(short)({left.Text}) * (short)({right.Text})";
                break;
            case BinaryOp.SDiv:
                text = $"(short)({left.Text}) / (short)({right.Text})";
                break;
            case BinaryOp.SMod:
                text = $"(short)({left.Text}) % (short)({right.Text})";
                break;
            case BinaryOp.And: text = $"({left.Text}) & ({right.Text})"; break;
            case BinaryOp.Or:  text = $"({left.Text}) | ({right.Text})"; break;
            case BinaryOp.Xor: text = $"({left.Text}) ^ ({right.Text})"; break;
            case BinaryOp.Shl: text = $"({left.Text}) << ({right.Text})"; break;
            case BinaryOp.Shr: text = $"(unsigned int)({left.Text}) >> ({right.Text})"; break;
            case BinaryOp.SShl: text = $"(short)({left.Text}) << ({right.Text})"; break;
            case BinaryOp.SShr: text = $"(short)({left.Text}) >> ({right.Text})"; break;
            default:
                Error($"unsupported binary operator {node.Op}", node.Span);
                text = $"0 /* unsupported {node.Op} */";
                break;
        }
        return new(WrapArith($"({text})", result), result);
    }

    public EmitResult VisitUnaryExpr(UnaryExpr node)
    {
        var inner = ExprFull(node.Operand);
        var t = inner.Type ?? SlangType.Word;
        return node.Op switch
        {
            UnaryOp.Negate => new(WrapArith($"(-({inner.Text}))", t), t),
            UnaryOp.Plus => new($"(+({inner.Text}))", t),
            UnaryOp.Not => new($"(!({inner.Text}))", SlangType.Word),
            UnaryOp.Cpl => new(WrapArith($"(~({inner.Text}))", t), t),
            _ => new($"0 /* unsupported unary */", t),
        };
    }

    public EmitResult VisitAssignExpr(AssignExpr node)
    {
        var target = ExprFull(node.Target);
        var value = ExprFull(node.Value);
        var targetType = target.Type ?? SlangType.Word;
        var castedValue = CastTo(value, targetType);
        return new($"({target.Text} = {castedValue})", targetType);
    }

    public EmitResult VisitCompoundAssignExpr(CompoundAssignExpr node)
    {
        var target = ExprFull(node.Target);
        var value = ExprFull(node.Value);
        var targetType = target.Type ?? SlangType.Word;
        var cop = node.Op switch
        {
            CompoundAssignOp.AddAssign => "+=",
            CompoundAssignOp.SubAssign => "-=",
            CompoundAssignOp.MulAssign => "*=",
            CompoundAssignOp.DivAssign => "/=",
            _ => "??="
        };
        return new($"({target.Text} {cop} {value.Text})", targetType);
    }

    public EmitResult VisitIncrementExpr(IncrementExpr node)
    {
        var inner = ExprFull(node.Operand);
        var t = inner.Type ?? SlangType.Word;
        var op = node.IsIncrement ? "++" : "--";
        var text = node.IsPrefix ? $"({op}{inner.Text})" : $"({inner.Text}{op})";
        return new(text, t);
    }

    public EmitResult VisitCallExpr(CallExpr node)
    {
        var funcName = (node.Function as IdentifierExpr)?.Name;
        if (funcName == null)
        {
            Error("indirect function calls are not supported by oscar_c backend in v1", node.Span);
            return new("/* unsupported indirect call */0", SlangType.Word);
        }

        // 解決順 (= plan の 4 階層):
        //   1. RuntimeBinding (builtin: WIDTH / LOCATE / HEX2$ 等)
        //   2. SymbolTable (SLANG 宣言: Function / CFunction / MachineFunction)
        //   3. CBindingRegistry (= env c_bindings)  ← Commit 4 で追加
        //   4. undeclared → error
        var binding = RuntimeBinding.Lookup(funcName);
        string cName;
        SlangType retType;
        FunctionType? cfuncSig = null;   // CFUNC で param 型キャストするとき参照

        if (binding != null)
        {
            cName = binding.CName;
            retType = binding.ReturnType;
        }
        else
        {
            var sym = _globals?.GlobalScope.Resolve(funcName);
            if (sym?.Kind == SymbolKind.CFunction)
            {
                // CFUNC: SLANG 宣言で SymbolKind.CFunction として登録された関数。
                // c_name は Symbol.CName に case preserve で保存されている。
                // 引数は CFuncDecl の Parameters 型に合わせて CastTo する。
                cName = sym.CName ?? IdentifierMap.Sanitize(funcName);
                cfuncSig = sym.Type as FunctionType;
                retType = cfuncSig?.ReturnType ?? SlangType.Word;
            }
            else if (sym?.Kind == SymbolKind.MachineFunction)
            {
                Error($"MACHINE function `{funcName}` is not supported by oscar_c backend; gate the call with `#IF ENV_TYPE==7` or `#IF BACKEND==1`", node.Span);
                return new("/* unsupported MACHINE call */0", SlangType.Word);
            }
            else if (sym == null)
            {
                // 未宣言関数: env c_bindings: で提供された binding を試す。
                // SLANG 側 CFUNC 宣言と同名なら #2 で先に hit するので、ここに来るのは
                // env binding 限定 (= ユーザー override の規律に合う)。
                var envBinding = _cBindings.Lookup(funcName);
                if (envBinding != null)
                {
                    cName = envBinding.CName;
                    var paramTypes = envBinding.Params.Select(CBindingRegistry.MapType).ToList();
                    cfuncSig = new FunctionType(CBindingRegistry.MapType(envBinding.Return), paramTypes);
                    retType = cfuncSig.ReturnType;
                }
                else
                {
                    // SLANG では Z80 backend で「未宣言関数 = address に JSR する MACHINE 風」
                    // 扱いを許す慣行があるが、oscar_c backend ではどの関数を呼ぶか決定不能なので
                    // 診断 error にする (v1)。
                    Error($"undeclared function `{funcName}` cannot be called from oscar_c backend (v1); declare it as a function, CFUNC, or env c_bindings entry, or gate with `#IF ENV_TYPE==7`", node.Span);
                    return new($"/* undeclared {funcName} */0", SlangType.Word);
                }
            }
            else
            {
                cName = FuncIdent(funcName);
                retType = sym.Type is FunctionType ft ? ft.ReturnType : SlangType.Word;
            }
        }

        // CFUNC は param 型に合わせて CastTo (= 型あり CFUNC の WORD vs BYTE 差を吸収)。
        // それ以外は ExprFull で型情報を捨てる従来挙動。
        IEnumerable<string> args;
        if (cfuncSig != null)
        {
            var castedArgs = new List<string>();
            for (int i = 0; i < node.Arguments.Count; i++)
            {
                var r = ExprFull(node.Arguments[i]);
                var target = i < cfuncSig.ParameterTypes.Count
                    ? cfuncSig.ParameterTypes[i]
                    : SlangType.Word;
                castedArgs.Add(CastTo(r, target));
            }
            args = castedArgs;
        }
        else
        {
            args = node.Arguments.Select(a => Expr(a));
        }
        return new($"({cName}({string.Join(", ", args)}))", retType);
    }

    public EmitResult VisitArrayAccessExpr(ArrayAccessExpr node)
    {
        // memory-mapped 配列 (MEM[]/MEMW[]) は SLANG_MEM/SLANG_MEMW マクロ展開。
        // SemanticAnalyzer.DefineSystemArray が MEM/MEMW を MemoryArrayType として
        // global 登録する (SemanticAnalyzer.cs:113)。CEmitter は通常の VisitIdentifier
        // で V_MEM/V_MEMW という存在しない C ident を出してしまうので、ここで先に
        // 識別子由来の MemoryArrayType を判定して SLANG_MEM / SLANG_MEMW へ。
        if (node.Array is IdentifierExpr memId)
        {
            var memSym = _globals?.GlobalScope.Resolve(memId.Name);
            if (memSym?.Type is MemoryArrayType mat)
            {
                if (node.Indices.Count != 1)
                {
                    Error($"`{memId.Name}` takes a single index (= absolute address)", node.Span);
                    return new("0", mat.ElementType);
                }
                var addr = Expr(node.Indices[0]);
                var macro = mat.ElementType is PrimitiveType { Kind: PrimitiveKind.Word }
                    ? "SLANG_MEMW" : "SLANG_MEM";
                return new($"{macro}({addr})", mat.ElementType);
            }
        }

        var arr = ExprFull(node.Array);
        var arrType = arr.Type;
        var idxParts = new StringBuilder();
        foreach (var idx in node.Indices)
        {
            idxParts.Append('[').Append(Expr(idx)).Append(']');
        }
        var elemType = arrType switch
        {
            ArrayType at => at.ElementType,
            PointerType pt => pt.ElementType,
            _ => SlangType.Word,
        };
        return new($"({arr.Text}{idxParts})", elemType);
    }

    public EmitResult VisitConditionalExpr(ConditionalExpr node)
    {
        var c = Expr(node.Condition);
        var t = ExprFull(node.TrueExpr);
        var f = ExprFull(node.FalseExpr);
        var result = PromoteArith(t.Type, f.Type);
        return new($"(({c}) ? ({t.Text}) : ({f.Text}))", result);
    }

    public EmitResult VisitCommaExpr(CommaExpr node)
    {
        var l = Expr(node.Left);
        var r = ExprFull(node.Right);
        return new($"(({l}), ({r.Text}))", r.Type);
    }

    public EmitResult VisitAddressOfExpr(AddressOfExpr node)
    {
        var inner = ExprFull(node.Operand);
        return new($"(&({inner.Text}))", new PointerType(inner.Type ?? SlangType.Byte));
    }

    public EmitResult VisitHighLowExpr(HighLowExpr node)
    {
        var inner = ExprFull(node.Operand);
        if (node.IsHigh)
            return new($"((unsigned char)((({inner.Text}) >> 8) & 0xFF))", SlangType.Byte);
        return new($"((unsigned char)(({inner.Text}) & 0xFF))", SlangType.Byte);
    }

    public EmitResult VisitCodeExpr(CodeExpr node)
    {
        Error("CODE expressions are not supported by oscar_c backend in v1 (Z80-specific)", node.Span);
        return new("0", SlangType.Word);
    }

    public EmitResult VisitCastExpr(CastExpr node)
    {
        var inner = ExprFull(node.Operand);
        var targetType = TypeOfDataSize(node.TargetSize);
        return new($"(({CTypeMapper.MapDeclType(targetType)})({inner.Text}))", targetType);
    }

    public EmitResult VisitStringFuncExpr(StringFuncExpr node)
    {
        // PRINT 文の中で出てくる場合は VisitPrintStmt 側で個別 dispatch する。
        // ここに来るのは式コンテキスト (= 値が必要な使われ方) のみ。
        var binding = RuntimeBinding.Lookup(node.FuncName);
        if (binding != null)
        {
            var args = node.Arguments.Select(a => Expr(a));
            return new($"({binding.CName}({string.Join(", ", args)}))", binding.ReturnType);
        }
        Error($"unsupported string/builtin function `{node.FuncName}` in expression context", node.Span);
        return new("0", SlangType.Word);
    }

    // === Helpers (ident / cast / util) ===

    /// <summary>変数の C ident: <c>V_</c> + sanitize</summary>
    private string VarIdent(string slangName) => "V_" + IdentifierMap.Sanitize(slangName);

    /// <summary>関数の C ident: <c>F_</c> + sanitize</summary>
    private string FuncIdent(string slangName) => "F_" + IdentifierMap.Sanitize(slangName);

    /// <summary>定数の C ident: <c>C_</c> + sanitize</summary>
    private string ConstIdent(string slangName) => "C_" + IdentifierMap.Sanitize(slangName);

    /// <summary>関数内 static 変数の C ident: <c>V_funcName_varName</c></summary>
    private string StaticVarIdent(string funcName, string varName)
        => "V_" + IdentifierMap.Sanitize(funcName) + "_" + IdentifierMap.Sanitize(varName);

    /// <summary>GOTO/LABEL の C ident: <c>L_</c> + sanitize</summary>
    private string LabelIdent(string slangLabel) => "L_" + IdentifierMap.Sanitize(slangLabel);

    private int _tempCounter;
    private string NewTempIdent(string prefix) => $"{prefix}_{_tempCounter++}";

    /// <summary>
    /// 文を波括弧ブロックで包む。Body が既に Block なら visit がそのまま <c>{...}</c> を出すので素通し、
    /// 単文なら <c>{ stmt; }</c> で包む。
    /// </summary>
    private string EmitStmtAsBlock(AstNode body)
    {
        if (body is Block)
        {
            return body.Accept(this).Text;
        }
        var sb = new StringBuilder();
        sb.Append(Line("{"));
        _indent++;
        sb.Append(body.Accept(this).Text);
        _indent--;
        sb.Append(Line("}"));
        return sb.ToString();
    }

    /// <summary>
    /// Block の中身を波括弧無しで出す (= 親側で既に <c>{}</c> を出している場合用)。
    /// 単文ならそのまま 1 行返す。
    /// </summary>
    private string EmitStmtInner(AstNode body)
    {
        if (body is Block bb)
        {
            var sb = new StringBuilder();
            foreach (var s in bb.Statements) sb.Append(s.Accept(this).Text);
            return sb.ToString();
        }
        return body.Accept(this).Text;
    }

    /// <summary>SLANG 式を target 型にキャスト (代入や PRINT で使う)</summary>
    private string CastTo(EmitResult expr, SlangType targetType)
    {
        var srcType = expr.Type;
        if (srcType == null || srcType.Equals(targetType)) return expr.Text;

        // 整数 → Float 変換は signed 解釈 (Z80 backend の i16tof24 と同じセマンティクス)。
        // SLANG WORD は unsigned int としてマップしているが、FLOAT に変換するときは
        // ビット列を signed 16-bit として解釈する慣行 (= `Y - 39` が 0xFFD9 ではなく -39
        // として浮動小数演算に入る)。
        if (targetType is PrimitiveType { Kind: PrimitiveKind.Float })
        {
            if (srcType is PrimitiveType { Kind: PrimitiveKind.Word })
                return $"((float)(short)({expr.Text}))";
            if (srcType is PrimitiveType { Kind: PrimitiveKind.Byte })
                return $"((float)(signed char)({expr.Text}))";
        }

        var cTarget = CTypeMapper.MapDeclType(targetType);
        return $"(({cTarget})({expr.Text}))";
    }

    /// <summary>
    /// 整数式を signed cast 経由で float キャスト (= Z80 backend の i16tof24 相当)。
    /// VisitBinaryExpr / VisitCastExpr 等から float promote の際に呼ぶ。
    /// 既に float / 非数値型ならそのまま返す。
    /// </summary>
    private static string PromoteToFloatSigned(string text, SlangType? type) => type switch
    {
        PrimitiveType { Kind: PrimitiveKind.Word } => $"((float)(short)({text}))",
        PrimitiveType { Kind: PrimitiveKind.Byte } => $"((float)(signed char)({text}))",
        _ => text,
    };

    /// <summary>
    /// 宣言リストを flatten。SLANG parser は `VAR X, Y, Z;` を <c>Block(VarDecl X, VarDecl Y, VarDecl Z)</c>
    /// にラップして返すため、宣言処理側で 1 段解体する必要がある (= 中身の VarDecl/ArrayDecl/ConstDecl 等を
    /// そのまま yield)。Block は再帰的に解体 (= ネスト Block 防御)。
    /// </summary>
    /// <summary>
    /// SymbolTable から CFunction を全列挙して C 側 extern 宣言文字列を作る。
    /// 同じ c_name の重複は signature 一致なら 1 個に集約 (alias)、不一致は error。
    /// 略式 CFUNC (= Parameters null) は SemanticAnalyzer.VisitCFuncDecl で全 WORD として
    /// SymbolTable に登録済 (= 同じ FunctionType を辿る)。
    /// </summary>
    private string BuildCFuncExterns()
    {
        var sb = new StringBuilder();
        // c_name → 既に emit した extern の signature (= FunctionType)
        // signature 一致なら skip、不一致なら error
        var emitted = new Dictionary<string, FunctionType>(StringComparer.Ordinal);

        // === Phase 1: SymbolTable.CFunction (SLANG ソース内 CFUNC 宣言) ===
        if (_globals != null)
        {
            foreach (var s in _globals.GlobalScope.Symbols.Values)
            {
                if (s.Kind != SymbolKind.CFunction) continue;
                var cName = s.CName ?? IdentifierMap.Sanitize(s.Name);
                var ft = s.Type as FunctionType;
                if (ft == null) continue;
                AppendOrCheckExtern(sb, emitted, cName, ft, s.Name);
            }
        }

        // === Phase 2: CBindingRegistry (env file c_bindings:) ===
        // SLANG 側で同名 CFUNC 宣言があれば override により Phase 1 で先に emit
        // 済み (= signature 一致なら skip、不一致なら error)。
        foreach (var b in _cBindings.All)
        {
            // SLANG 側 CFUNC が既に同名 Symbol を登録している場合は skip
            // (= info diagnostics は CTranspiler 側で出す)
            if (_globals?.GlobalScope.ResolveLocal(b.Name) != null) continue;

            var paramTypes = b.Params.Select(CBindingRegistry.MapType).ToList();
            var ft = new FunctionType(CBindingRegistry.MapType(b.Return), paramTypes);
            AppendOrCheckExtern(sb, emitted, b.CName, ft, b.Name);
        }
        return sb.ToString();
    }

    private void AppendOrCheckExtern(StringBuilder sb, Dictionary<string, FunctionType> emitted,
                                      string cName, FunctionType ft, string slangName)
    {
        if (emitted.TryGetValue(cName, out var prevSig))
        {
            if (!SignatureEqual(prevSig, ft))
            {
                Error(
                    $"CFUNC `{slangName}` aliases C function `{cName}` with a signature different from a previous binding (= C prototype 衝突)",
                    new Lexer.SourceSpan());
            }
            return;
        }
        emitted[cName] = ft;
        var retC = CTypeMapper.MapDeclType(ft.ReturnType);
        string paramsC = ft.ParameterTypes.Count == 0
            ? "void"
            : string.Join(", ", ft.ParameterTypes.Select(t => CTypeMapper.MapDeclType(t)));
        sb.Append($"extern {retC} {cName}({paramsC});\n");
    }

    /// <summary>FunctionType の signature 同一性 (ReturnType + ParameterTypes 全て一致)</summary>
    private static bool SignatureEqual(FunctionType a, FunctionType b)
    {
        if (!a.ReturnType.Equals(b.ReturnType)) return false;
        if (a.ParameterTypes.Count != b.ParameterTypes.Count) return false;
        for (int i = 0; i < a.ParameterTypes.Count; i++)
            if (!a.ParameterTypes[i].Equals(b.ParameterTypes[i])) return false;
        return true;
    }

    private static IEnumerable<AstNode> FlattenDecls(IEnumerable<AstNode> nodes)
    {
        foreach (var n in nodes)
        {
            if (n is Block b)
            {
                foreach (var inner in FlattenDecls(b.Statements))
                    yield return inner;
            }
            else
            {
                yield return n;
            }
        }
    }

    private static bool TryEvalIntLiteral(Expression? e, out long value)
    {
        value = 0;
        if (e == null) return false;
        if (e is IntegerLiteral il) { value = il.Value; return true; }
        if (e is UnaryExpr ue && ue.Op == UnaryOp.Negate
            && ue.Operand is IntegerLiteral il2)
        {
            value = -il2.Value;
            return true;
        }
        return false;
    }
}
