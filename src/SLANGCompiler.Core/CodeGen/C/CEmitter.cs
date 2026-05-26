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
    // current 関数の戻り型 size (= FuncDef 進入中のみ意味あり、`RETURN;` の暗黙
    // default 値を type に応じて補うために保持)。VisitFuncDef enter/leave で set/clear。
    private DataSize _currentFuncRetSize = DataSize.Word;

    // current 関数名 (= FuncDef 進入中なら名前、関数外 / global なら null)。
    // SLANG の関数内 static var (= BEGIN 前の VarDecl) の C 側 ident 衝突を
    // 避けるために funcName_varName で suffix 付ける。
    private string? _currentFuncName;
    // current FuncDef の static decl (BEGIN 前 VAR/ARRAY) 名集合。
    // body 内で同名識別子を見つけたとき StaticVarIdent (= V_funcName_name) で
    // 引けるようにする。FuncDef enter/leave で push/pop。
    private readonly HashSet<string> _currentStaticDecls = new(StringComparer.OrdinalIgnoreCase);

    // 添字省略 ARRAY + InitialCode で **固定配列化** された symbol 名集合
    // (= PR #189 oscar_c CEmitter の独自拡張、 SLANG 仕様の純 PointerType 解釈と
    // 異なる)。これら symbol への直接代入は CEmitter が `static unsigned char V_A[N] = {...}`
    // で emit 済のため `V_A = ...` の無効 C を生む。VisitAssignExpr で defensive reject 用。
    private readonly HashSet<string> _unsizedArraysWithInit = new(StringComparer.OrdinalIgnoreCase);

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

        // (v3b-E (5) 調査結果: `ARRAY ...:address = { ... }` (fixed addr + InitialCode)
        //  は SLANG parser 自体が文法 reject する (= "Expected expression, got LBrace '{'"、
        //  `:address` の後に `;` か別の syntax が来るのが文法)、 CEmitter には到達しない
        //  ため defensive guard は dead code として削除。 ArrayInitOscarCTests に
        //  parser reject を pin する test を追加。)

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

        // isIndirect (= 添字省略 `ARRAY BYTE A[]`) + InitialCode は SLANG 仕様で
        // 「添字省略時は要素数チェックなし」= 初期値 byte 列の長さで実配列サイズが
        // 決まる。pointer 化せず固定配列として emit する。
        if (isIndirect && node.InitialCode != null)
        {
            // 多次元 unsized + InitialCode: v3b-E (4) で対応。 C99 の auto dim 推論
            // (= `static T A[][M+1][N+1]... = {flat init};`) に乗せ、 第 1 次元のみ
            // `[]` で C compiler に推論させる。 ARRAY BYTE / WORD のみ対応 (= ARRAY
            // FLOAT multi-dim は scope 外 reject)、 第 2 次元以降の省略は C99 違反
            // のため reject。
            bool isMultiDim = node.Dimensions.Count != 1;
            if (isMultiDim)
            {
                if (slangType is PrimitiveType { Kind: PrimitiveKind.Float })
                {
                    Error("multi-dimensional ARRAY FLOAT with omitted size + initializer is not supported by oscar_c backend (= scope 外、 ARRAY BYTE / WORD のみ対応)", node.Span);
                    return new("", null);
                }
                // 第 1 次元のみ省略 OK、 第 2 次元以降に null があれば C99 違反で reject
                for (int i = 1; i < node.Dimensions.Count; i++)
                {
                    if (node.Dimensions[i] == null)
                    {
                        Error("only the first dimension can be omitted in multi-dim ARRAY initializer (= C99 仕様で第 2 次元以降の `[]` は不可)", node.Span);
                        return new("", null);
                    }
                }
            }
            var emit = BuildArrayInitFromCode(node.InitialCode, slangType, node.Span);
            if (isMultiDim)
            {
                // 第 1 次元 = `[]` (C 自動推論)、 第 2 次元以降は SLANG dim 値 + 1
                var mdimsSb = new StringBuilder("[]");
                var mdimList = new List<int> { 0 };
                foreach (var dim in node.Dimensions.Skip(1))
                {
                    var ev = _constEval.Evaluate(dim!);
                    if (ev == null) { Error("ARRAY dimension must be a compile-time constant expression", dim!.Span); ev = 0; }
                    int allocSize = ev.Value + 1;
                    mdimsSb.Append('[').Append(allocSize).Append(']');
                    mdimList.Add(allocSize);
                }
                var declLineM = _scope.ScopeDepth == 0
                    ? Line($"static {cType} {ident}{mdimsSb} = {emit.InitText};")
                    : Line($"{cType} {ident}{mdimsSb} = {emit.InitText};");
                if (_scope.ScopeDepth > 0)
                    _scope.DeclareLocal(node.Name, new ArrayType(slangType, mdimList));
                _unsizedArraysWithInit.Add(node.Name);
                return new(declLineM, null);
            }
            // C 配列長は CElementCount (= WORD なら byte 数 /2)、 byte 数を直接使うと
            // WORD 配列で 2 倍になる事故が起きる (Issue #194 / Codex review 指摘)。
            var declLine0 = _scope.ScopeDepth == 0
                ? Line($"static {cType} {ident}[{emit.CElementCount}] = {emit.InitText};")
                : Line($"{cType} {ident}[{emit.CElementCount}] = {emit.InitText};");
            if (_scope.ScopeDepth > 0)
                _scope.DeclareLocal(node.Name, new ArrayType(slangType, new List<int> { emit.CElementCount }));
            // 固定配列化された symbol を記録 (= VisitAssignExpr で reject 用)。
            // SLANG 仕様レベルでは PointerType だが、 oscar_c は固定配列で emit して
            // いるため代入は無効 C を生む。
            _unsizedArraysWithInit.Add(node.Name);
            return new(declLine0, null);
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
        int totalSize = 1;
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
            totalSize *= allocSize;
        }

        string init = "";
        if (node.InitialValue != null)
        {
            init = " = " + Expr(node.InitialValue);
        }
        else if (node.InitialCode != null)
        {
            // SLANG `ARRAY BYTE/WORD NAME[N] = { 値, %値, ... }` 形式の初期化。
            // 各要素は default BYTE (= 1 byte)、CastExpr で wrap されてれば
            // TargetSize に従う (= `%` prefix で WORD → 2 byte LE 展開)。
            // 容量超過 / 非定数 BYTE / FLOAT array トップレベル cast 等の SLANG 仕様
            // 違反は SemanticAnalyzer + ArrayInitialCodeSizer 側で先に reject 済 (Issue
            // #190)。ここでは oscar_c emit のみ。 ARRAY WORD は v3b-E (Issue #194)
            // で対応、 残 4 gap (FLOAT array / FLOAT prefix / 非定数 / multi-dim 添字
            // 省略 / fixed-addr) は次 PR 以降。 固定配列は C array 長 = dims から計算済
            // のためここでは InitText のみ使い、 C の implicit zero fill に容量埋めを委譲。
            var emit = BuildArrayInitFromCode(node.InitialCode, slangType, node.Span);
            // StringLiteral 単独 path は C string literal が NUL を含んで初期化するため、
            // 固定配列の容量 (= dims から決まる N+1 byte) が NUL 含む長さに満たないと
            // C 上は valid (= NUL 落ち) だが SLANG 期待の終端保証が壊れる。
            // semantic は SJIS K byte 単位で容量比較するため検知できないので backend で reject。
            if (emit.IsCStringLiteral && emit.CElementCount > totalSize)
            {
                Error($"固定長 ARRAY BYTE NAME[N] = {{\"...\"}} で C string literal の NUL 終端が容量に入りません (= 容量 {totalSize} byte < NUL 含 {emit.CElementCount} byte)。容量を 1 byte 大きく、もしくは StringLiteral を短くしてください", node.Span);
            }
            init = " = " + emit.InitText;
        }

        var declLine = _scope.ScopeDepth == 0
            ? Line($"static {cType} {ident}{dimsSb}{init};")
            : Line($"{cType} {ident}{dimsSb}{init};");
        if (_scope.ScopeDepth > 0)
            _scope.DeclareLocal(node.Name, new ArrayType(slangType, dimList));
        return new(declLine, null);
    }

    /// <summary>
    /// BuildArrayInitFromCode の戻り値。 byte 単位の入力 / emit 量 / C 要素数を分離して
    /// caller の事故 (= unsized 経路で WORD だと C 配列長が 2 倍になる類い) を防ぐ。
    /// </summary>
    /// <param name="InitText">C の "{0xNN, ...}" / "{0xNNNN, ...}" 文字列</param>
    /// <param name="SourceByteCount">入力 CODE byte stream 長 (= padding 前)</param>
    /// <param name="EmittedByteCount">実際 emit した byte 総数 (= WORD で奇数 byte の場合 +1 padding 後)</param>
    /// <param name="CElementCount">C 配列の要素数 (BYTE: EmittedByteCount / WORD: EmittedByteCount/2)</param>
    private sealed record ArrayInitEmitResult(
        string InitText,
        int SourceByteCount,
        int EmittedByteCount,
        int CElementCount,
        bool IsCStringLiteral = false);

    /// <summary>
    /// SLANG `ARRAY BYTE/WORD NAME[N] = { 値, %値, ... }` の InitialCode を C array
    /// init 文字列に変換する。SLANG 仕様の「CODE byte stream」解釈に従い、 まず
    /// byte 単位で展開してから elementType に応じて出力形を切り替える:
    /// <list type="bullet">
    /// <item><description>BYTE: byte stream を `{0xNN, 0xNN, ...}` で出力</description></item>
    /// <item><description>WORD: byte stream を 2 byte ずつ grouping (= little-endian) して
    /// `{0xNNNN, ...}` で出力、 奇数 byte なら最後を 0 padding</description></item>
    /// </list>
    /// 容量までの 0 fill は helper では行わず C の implicit zero fill に委譲する
    /// (= 固定配列 `static unsigned int V[capacity] = {...};` で C が残りを 0 埋め)。
    /// FLOAT 配列 / FLOAT prefix / 非定数 element / StringLiteral / CodeLabelRef は
    /// Issue #194 配下の次 PR で実装、 ここでは defensive reject。
    /// </summary>
    private ArrayInitEmitResult BuildArrayInitFromCode(
        System.Collections.Generic.List<Expression> code,
        SlangType elementType, SourceSpan span)
    {
        bool isByteArray = elementType is PrimitiveType { Kind: PrimitiveKind.Byte };
        bool isWordArray = elementType is PrimitiveType { Kind: PrimitiveKind.Word };
        bool isFloatArray = elementType is PrimitiveType { Kind: PrimitiveKind.Float };
        if (!isByteArray && !isWordArray && !isFloatArray)
        {
            Error("`= { ... }` initializer is supported only for ARRAY BYTE / WORD / FLOAT in oscar_c backend", span);
            return new ArrayInitEmitResult("{0}", 0, 0, 0);
        }

        // v3b-E (1b): ARRAY FLOAT FA[N] = { 1.0, 2.0, ... } 専用 path
        // oscar64 native float32 を使い `static float V_FA[N+1] = {1.0, 2.0};` で emit。
        // SLANG semantic は f24 (3 byte/elem) 基準で容量計算するが、 oscar_c では
        // float32 (4 byte/elem) 基準で C 配列確保 = element 数ベースで偶然整合する
        // (= 容量 N+1 elements 以内に init element が収まれば semantic / C 両方 pass)。
        // ユーザー指示: oscar 側 float のみ考慮、 SLANG f24 byte stream layout は無視。
        if (isFloatArray)
        {
            var floatLiterals = new System.Collections.Generic.List<string>(code.Count);
            bool sawFloatError = false;
            foreach (var expr in code)
            {
                // トップレベル CastExpr は ArrayInitialCodeSizer で先 reject 済 = defensive
                if (expr is CastExpr)
                {
                    Error("Cast expression not allowed in FLOAT array initializer (= ArrayInitialCodeSizer で先 reject 済、 defensive)", expr.Span);
                    sawFloatError = true;
                    continue;
                }
                // ConstEvaluator.EvaluateFloat で値解決 (= FloatLiteral / IntegerLiteral
                // promote / 定数式 1.0 + 2.0 等を C float literal に展開)。
                // oscar64 は exponent notation (= `1E-05`) を float literal として
                // 受理しないため、 共通 helper で固定小数点 + `.0` 補完に整形する。
                var fv = _constEval.EvaluateFloat(expr);
                if (fv.HasValue)
                {
                    floatLiterals.Add(FormatFloatForOscar64Literal(fv.Value));
                    continue;
                }
                // 非定数 (= 識別子参照等) は oscar64 static initializer 制約で reject
                // (= 既存 (3b) 知見、 error 3008 Constant initializer expected と同じ理由)
                Error("ARRAY FLOAT initializer element must be a compile-time constant in oscar_c backend (= 非定数 expression の static init は oscar64 で error 3008、 MAIN 冒頭等で runtime 初期化に書き換え推奨)", expr.Span);
                sawFloatError = true;
            }
            if (sawFloatError) return new ArrayInitEmitResult("{0}", 0, 0, 0);
            string fInitText = "{" + string.Join(", ", floatLiterals) + "}";
            // SourceByteCount = SLANG semantic 基準 (= f24 3 byte/elem)、
            // EmittedByteCount = oscar64 float32 基準 (= 4 byte/elem)、
            // CElementCount = element 数 (= caller の C 配列長算出 / NUL fit check に使う)
            return new ArrayInitEmitResult(fInitText, code.Count * 3, code.Count * 4, code.Count);
        }

        // 単独 StringLiteral path (= `ARRAY BYTE S[] = {"hello"}` / `ARRAY BYTE S[N] = {"hi"}`)
        // は C string literal をそのまま emit して oscar64 -psci で PETSCII 化に任せる
        // (= hex literal 列で出すと -psci 変換 が効かない、 PETSCII 自動変換は string
        //  literal のみ対象)。 BYTE 配列限定 (WORD/FLOAT は意味曖昧 = scope 外)、 mixed
        //  (= StringLiteral + 他要素) も loop 側で reject。
        if (code.Count == 1 && code[0] is StringLiteral slit)
        {
            if (!isByteArray)
            {
                Error("StringLiteral element is supported only for ARRAY BYTE in oscar_c backend (= ARRAY WORD への StringLiteral は意味曖昧、 scope 外)", span);
                return new ArrayInitEmitResult("\"\"", 0, 0, 0);
            }
            // 非 ASCII / 表示不可制御文字 / NUL は v3b-E (3a) scope 外 で reject:
            //   1. SLANG raw .Length と SJIS byte 数が一致しないと C 配列長 (= raw.Length + 1)
            //      が SJIS byte 数より小さくなる (= "あ" U+3042 は SJIS 2 byte / .Length=1)
            //   2. CStringEncoder の `\xNN` escape は C 仕様で後続 hex digit を食う
            //      (例 "\x01A" → C `\x01A` = 0x1A) ため制御文字 + 続く hex char が壊れる
            //   3. NUL (= U+0000) は CStringEncoder が `\0` (C octal escape の短縮形) で
            //      出すため、直後が 0..7 だと C 側で octal escape として連結 (例 SLANG
            //      `"\x007"` → C `"\07"` = [0x07] と誤解釈) → これも reject
            // SLANG lexer 解釈に注意: `"\n"` 経由は CR (0x0D)、 `"\xNN"` 経由は raw byte、
            // SLANG `"\r"` `"\t"` `"\0"` 自体は別の char (0x1C / 't' / '0') になるため
            // ここの check は **char 値ベース** で判定する (= SLANG escape 構文ベースではない)。
            // 安全策として **ASCII printable (0x20-0x7E) + CR (0x0D、 SLANG `\n` 経由) のみ** 許可。
            foreach (var ch in slit.Value)
            {
                bool isAsciiPrintable = ch >= 0x20 && ch <= 0x7E;
                bool isAllowedNewline = ch == '\r'; // SLANG `\n` (lexer で CR=0x0D 解釈)
                if (!isAsciiPrintable && !isAllowedNewline)
                {
                    Error("StringLiteral with non-ASCII / non-printable / NUL character is not supported in oscar_c ARRAY BYTE initializer (v3b-E (3a) scope は ASCII printable 0x20-0x7E + 改行 CR (0x0D、 SLANG `\\n` 経由) のみ、 NUL / 高位 byte / その他制御文字は別 PR / v3b-E (3a-ext) 候補)", slit.Span);
                    return new ArrayInitEmitResult("\"\"", 0, 0, 0, IsCStringLiteral: true);
                }
            }
            // SLANG 仕様 byte stream 長は SJIS bytes (= ArrayInitialCodeSizer の容量
            // check と整合、 ASCII printable 限定なら SJIS == .Length)。 C 配列は NUL
            // 含めて確保 (= C string literal 規約)。
            var sjisBytes = StringEncoder.ToShiftJisBytes(slit.Value, _diagnostics);
            int strSourceBytes = sjisBytes.Length;
            int strCElementCount = slit.Value.Length + 1; // C source の char 数 + NUL
            string strInitText = CStringEncoder.Encode(slit.Value);
            return new ArrayInitEmitResult(strInitText, strSourceBytes, strCElementCount, strCElementCount, IsCStringLiteral: true);
        }

        // v3b-E (3b) 調査結果: ARRAY WORD W[] = { %FUNC, %ARRAY } 等の address reloc
        // を oscar_c で emit する MVP を一度試したが、 oscar64 が **static integer
        // initializer で address-to-integer cast (= `(unsigned int)F_FUNC` /
        // `(unsigned int)V_BUF`) を constant initializer と認めない** (= error 3008
        // Constant initializer expected) ため、 `void (*fp[])() = { foo }` 系の
        // pointer-typed initializer なら通るが SLANG `ARRAY WORD` の C 型は unsigned int[]
        // で意味論的に変えられない。 runtime 初期化 / 内部 `void *[]` 表現の特別扱いは
        // (3b) MVP の範囲を超える別 issue。
        // → **permanent backend gap**: oscar_c では `%FUNC` / `%ARRAY` の address ref
        //   を static init に書けない、 SLANG 側で runtime 初期化 (= ARRAY 宣言後
        //   `W[0] = %FUNC; W[1] = %ARRAY;` を main 等の冒頭で書く) で workaround。
        // 既存 generic non-const reject (loop 内 L515 付近) で error 出るが、 message
        // を oscar64 制約由来として明示するため下の path で個別 reject 経路を残す。

        var bytes = new System.Collections.Generic.List<byte>();
        foreach (var expr in code)
        {
            var itemExpr = expr;
            int itemSize = 1; // default BYTE
            if (itemExpr is CastExpr cast)
            {
                itemExpr = cast.Operand;
                if (cast.TargetSize == DataSize.Float)
                {
                    // FLOAT prefix (= `%%1.5`) は SLANG 仕様で f24 (= 3 byte) を byte
                    // stream に流す機能だが、 oscar_c は oscar64 native float32 mapped で
                    // f24 byte 表現を持たないため意味的に **permanent backend gap**。
                    // ユーザーが float 値を ARRAY に詰めたい場合は ARRAY FLOAT を使う。
                    Error("FLOAT prefix (`%%`) in ARRAY BYTE / WORD initializer is not supported by oscar_c backend (= oscar64 は f24 byte stream 表現を持たない、 ARRAY FLOAT FA[N] = { 1.0, ... } を使ってください、 #194 配下 v3b-E (2) permanent backend gap)", expr.Span);
                    continue;
                }
                itemSize = cast.TargetSize == DataSize.Byte ? 1 : 2;
            }

            // StringLiteral が mixed で来た (= 単独 short-circuit に入らなかった) は
            // 別 PR scope。 単独 path だけ先に対応済 (= v3b-E (3a) first PR)。
            if (itemExpr is StringLiteral)
            {
                Error("StringLiteral mixed with other items in ARRAY initializer is not supported by oscar_c backend (= 別 PR / v3b-E 候補、 単独 StringLiteral 要素なら対応済)", expr.Span);
                continue;
            }

            var constVal = _constEval.Evaluate(itemExpr);
            if (itemExpr is IntegerLiteral ilit)
                constVal = (int)ilit.Value;
            if (!constVal.HasValue)
            {
                // v3b-E (3b) 調査: 非定数 IdentifierExpr (= `%FUNC` / `%ARRAY` 等の
                //   address ref) は oscar64 が static unsigned int initializer に
                //   address-to-integer cast (= `(unsigned int)F_FUNC`) を許容しない
                //   (= error 3008 Constant initializer expected)、 permanent backend gap。
                // workaround は SLANG 側で runtime 初期化 (= MAIN() の冒頭等で
                //   `JT[0] = %FUNC; JT[1] = %ARRAY;`)、 Z80 backend は `DW LABEL` で
                //   linker reloc 解決可能なため backend gap として明示する。
                if (itemExpr is IdentifierExpr id)
                {
                    var sym = _globals?.GlobalScope.Resolve(id.Name);
                    if (sym?.Kind == SymbolKind.Function
                        || sym?.Kind == SymbolKind.MachineFunction
                        || (sym?.IsArrayDecl == true && sym.IsGlobal))
                    {
                        Error($"ARRAY initializer の非定数 identifier `{id.Name}` の address 参照は oscar_c では未対応 (= oscar64 が static integer initializer で `(unsigned int)F_xxx` / `(unsigned int)V_xxx` を constant initializer と認めない、 error 3008)。 SLANG 側で runtime 初期化に書き換えてください (例: `ARRAY WORD <ARRAY 名>[N];` のように **固定サイズ宣言** にしてから MAIN 冒頭で `<ARRAY 名>[i] = %{id.Name};` で代入。 添字省略 `[]` のまま InitialCode を削ると oscar_c で pointer 宣言になるので注意)。 Z80 backend は `DW LABEL` で linker reloc 解決", expr.Span);
                        continue;
                    }
                }
                Error("non-FLOAT ARRAY initializer element must be a compile-time constant in oscar_c backend (= 非定数 expression の static init は oscar64 で error 3008、 runtime 初期化に書き換え推奨)", expr.Span);
                continue;
            }

            int v = constVal.Value;
            bytes.Add((byte)(v & 0xFF));
            if (itemSize == 2)
                bytes.Add((byte)((v >> 8) & 0xFF));
            // (itemSize == 3 = FLOAT prefix は既に上で reject 済)
        }

        int sourceByteCount = bytes.Count;
        string initText;
        int emittedByteCount;
        int cElementCount;
        if (isByteArray)
        {
            var literals = new System.Collections.Generic.List<string>(bytes.Count);
            foreach (var b in bytes)
                literals.Add($"0x{b:X2}");
            initText = "{" + string.Join(", ", literals) + "}";
            emittedByteCount = sourceByteCount;
            cElementCount = sourceByteCount;
        }
        else // WORD
        {
            // 奇数 byte なら最後を 0 padding して 2 byte ずつ WORD literal 化
            if ((bytes.Count & 1) != 0) bytes.Add(0);
            var literals = new System.Collections.Generic.List<string>(bytes.Count / 2);
            for (int i = 0; i < bytes.Count; i += 2)
            {
                int w = bytes[i] | (bytes[i + 1] << 8);
                literals.Add($"0x{w:X4}");
            }
            initText = "{" + string.Join(", ", literals) + "}";
            emittedByteCount = bytes.Count;
            cElementCount = bytes.Count / 2;
        }

        return new ArrayInitEmitResult(initText, sourceByteCount, emittedByteCount, cElementCount);
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
        _currentFuncRetSize = node.ReturnSize;
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
            bool isIndirect = ad.Dimensions.Any(d => d == null);

            // 添字省略 (= ARRAY BYTE A[]) + InitialCode は SLANG 仕様で
            // 「添字省略時は要素数チェックなし」、 初期値 byte 列長で配列サイズを決定。
            // v3b-E (4): multi-dim 添字省略は C99 auto dim 推論 (`static T A[][M+1]...`)
            // に乗せる、 ARRAY BYTE / WORD のみ対応 (= ARRAY FLOAT multi-dim は scope 外)。
            if (isIndirect && ad.InitialCode != null)
            {
                bool isMultiDimL = ad.Dimensions.Count != 1;
                if (isMultiDimL)
                {
                    if (slangType is PrimitiveType { Kind: PrimitiveKind.Float })
                    {
                        Error("multi-dimensional ARRAY FLOAT with omitted size + initializer is not supported by oscar_c backend (= scope 外、 ARRAY BYTE / WORD のみ対応)", ad.Span);
                        return "";
                    }
                    for (int i = 1; i < ad.Dimensions.Count; i++)
                    {
                        if (ad.Dimensions[i] == null)
                        {
                            Error("only the first dimension can be omitted in multi-dim ARRAY initializer (= C99 仕様で第 2 次元以降の `[]` は不可)", ad.Span);
                            return "";
                        }
                    }
                }
                var emit0 = BuildArrayInitFromCode(ad.InitialCode, slangType, ad.Span);
                if (isMultiDimL)
                {
                    var mdimsSbL = new StringBuilder("[]");
                    var mdimListL = new List<int> { 0 };
                    foreach (var dim in ad.Dimensions.Skip(1))
                    {
                        var ev = _constEval.Evaluate(dim!);
                        if (ev == null) { Error("ARRAY dimension must be a compile-time constant expression", dim!.Span); ev = 0; }
                        int allocSize = ev.Value + 1;
                        mdimsSbL.Append('[').Append(allocSize).Append(']');
                        mdimListL.Add(allocSize);
                    }
                    _scope.DeclareLocal(ad.Name, new ArrayType(slangType, mdimListL));
                    return Line($"static {cType} {ident}{mdimsSbL} = {emit0.InitText};");
                }
                _scope.DeclareLocal(ad.Name, new ArrayType(slangType, new List<int> { emit0.CElementCount }));
                // (関数内 static は _scope に ArrayType 登録済 = VisitAssignExpr の
                //  _scope.Resolve 経由で reject されるため、 _unsizedArraysWithInit
                //  への登録は不要。 そちらは global 由来の同名 symbol 限定。)
                return Line($"static {cType} {ident}[{emit0.CElementCount}] = {emit0.InitText};");
            }

            // 間接配列 (VAR BYTE T[];) — 関数内でもポインタとして扱う。
            // INDTEST.SL の TEST1 で T = ADR; とアドレスを後から代入するパターン。
            // local 間接配列は本来 static にする必要はないが、SLANG の static decl
            // (BEGIN 前) に書かれている場合は static にする。
            if (isIndirect)
            {
                _scope.DeclareLocal(ad.Name, new PointerType(slangType));
                return Line($"static {cType} *{ident};");
            }

            // 固定サイズ配列。SLANG 仕様: `ARRAY A[N]` は要素数 N+1 (index 0..N 有効)。
            var dimsSb = new StringBuilder();
            var dimList = new List<int>();
            int totalSize = 1;
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
                totalSize *= allocSize;
            }
            _scope.DeclareLocal(ad.Name, new ArrayType(slangType, dimList));
            string init = "";
            if (ad.InitialValue != null)
                init = " = " + Expr(ad.InitialValue);
            else if (ad.InitialCode != null)
            {
                // 容量超過 check は SemanticAnalyzer に移行済 (Issue #190)、 ここでは
                // emit のみ。 ARRAY WORD は v3b-E (Issue #194) 対応済、 残 gap は次 PR。
                // 容量埋めは C implicit zero fill に委譲 (= dims から決まる固定配列長
                // を使い InitText だけ流す)。
                var emit = BuildArrayInitFromCode(ad.InitialCode, slangType, ad.Span);
                // StringLiteral 単独 path は C string literal が NUL を含むため、固定
                // 配列の容量が NUL 含む長さに満たないと NUL 終端保証が壊れる (= global
                // 経路と同じ理由)。
                if (emit.IsCStringLiteral && emit.CElementCount > totalSize)
                {
                    Error($"関数内 static ARRAY BYTE NAME[N] = {{\"...\"}} で C string literal の NUL 終端が容量に入りません (= 容量 {totalSize} byte < NUL 含 {emit.CElementCount} byte)。容量を 1 byte 大きく、もしくは StringLiteral を短くしてください", ad.Span);
                }
                init = " = " + emit.InitText;
            }
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
        if (node.Value == null)
        {
            // SLANG `RETURN;` (引数なし)。CTranspiler は全関数を非 void 戻り型で
            // emit する (= MAIN も unsigned int、終端で暗黙 0 return 補完) ため、
            // 中途 `RETURN;` も型に応じた default 値を返す必要がある。
            var defaultRet = _currentFuncRetSize == DataSize.Float ? "0.0" : "0";
            return new(Line($"return {defaultRet};"), null);
        }
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
        return new(FormatFloatForOscar64Literal(node.Value), SlangType.Float);
    }

    /// <summary>
    /// double 値を oscar64 が受理する C float literal 形式に整形する。 oscar64 は
    /// (1) `f` suffix 不可 (= ANSI C と差異)、 (2) **exponent notation (`1E-05` 等)
    /// を float literal として受理しない** (= Codex review #199 で実機確認) ため、
    /// `R` (短い round-trip) を優先しつつ、 exponent が含まれた場合のみ F17 で
    /// 固定小数点 fallback して trailing zeros を除去する (= `0.00001` の精度悪化
    /// を避けつつ `3.14` の精度誤差表記 (`3.14000000000000012`) も避ける)。 整数値
    /// (`1`) は `.0` 補完で float literal 確定。
    /// </summary>
    internal static string FormatFloatForOscar64Literal(double v)
    {
        string s = v.ToString("R", CultureInfo.InvariantCulture);
        // exponent (= 1E-05 / 1e+30) が出たら F17 で固定小数点 fallback
        if (s.IndexOfAny(new[] { 'e', 'E' }) >= 0)
        {
            s = v.ToString("F17", CultureInfo.InvariantCulture);
            if (s.Contains('.'))
            {
                s = s.TrimEnd('0');
                if (s.EndsWith('.')) s += '0';
            }
        }
        else if (!s.Contains('.'))
        {
            s += ".0";
        }
        return s;
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
        // Issue #190: ARRAY 実体への代入は SemanticAnalyzer.CheckNotArrayAssignment
        // で先に reject される。ここは defensive guard (= backend 到達した場合の
        // 無効 C emit 防止)、 条件は semantic 側と完全一致させる:
        // `IsArrayDecl=true && Type is ArrayType` のみ reject、 添字省略
        // (= IsArrayDecl=true && Type is PointerType) は SLANG 仕様で「間接配列 =
        // ポインタ」扱いのため通過する。
        if (node.Target is IdentifierExpr tid)
        {
            var sym = _globals?.GlobalScope.Resolve(tid.Name);
            bool isArraySymbol = sym?.IsArrayDecl == true && sym.Type is ArrayType;
            if (!isArraySymbol)
            {
                // global 未登録 (= 関数内 static or local) を _scope で ArrayType 確認。
                // PointerType (= VAR BYTE T[] や ARRAY BYTE P[] の添字省略) は通過。
                var localType = _scope.Resolve(tid.Name);
                if (localType is ArrayType)
                    isArraySymbol = true;
            }
            // PR #189 oscar_c 拡張: 添字省略 + InitialCode は固定配列で emit してる
            // ため、 semantic 上 PointerType 扱いの symbol でも oscar_c では代入不可。
            // ただし関数内 local (= VAR BYTE A[] や ARRAY BYTE A[]) で shadowing
            // されている場合は global 由来の判定を skip (= local が PointerType なら
            // 代入 OK)。
            if (!isArraySymbol && !_scope.IsLocal(tid.Name)
                && _unsizedArraysWithInit.Contains(tid.Name))
                isArraySymbol = true;
            if (isArraySymbol)
            {
                Error($"cannot assign to ARRAY symbol `{tid.Name}`; ARRAY 宣言は配列実体で再代入不可 (`VAR BYTE T[];` / 初期値なし `ARRAY BYTE P[];` のポインタ宣言ならOK)", node.Span);
                return new("/* invalid ARRAY assignment */0", SlangType.Word);
            }
        }
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

        // 解決順:
        //   1. RuntimeBinding (static builtin: WIDTH / LOCATE / HEX2$ 等)
        //   2. SymbolTable (SLANG CFUNC 宣言 / user Function) — SLANG ソース起源を優先
        //   3. CBindingRegistry (= env c_bindings) — SemanticAnalyzer が builtin として
        //      MachineFunction 登録する SLANG 仕様関数 (INPUT / LOCATE 等) を env binding で
        //      override する。MachineFunction error の前に env binding を試すことで、
        //      Z80 backend と同じ SLANG 仕様関数名を c64 で使えるようにする
        //   4. MachineFunction → error (env binding がない場合のみ)
        //   5. undeclared → error
        var binding = RuntimeBinding.Lookup(funcName);
        string cName;
        SlangType retType;
        FunctionType? cfuncSig = null;   // CFUNC / env binding で param 型キャストするとき参照

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
                // SLANG CFUNC 宣言: SymbolKind.CFunction として登録された関数。
                // c_name は Symbol.CName に case preserve で保存されている。
                cName = sym.CName ?? IdentifierMap.Sanitize(funcName);
                cfuncSig = sym.Type as FunctionType;
                retType = cfuncSig?.ReturnType ?? SlangType.Word;
            }
            else if (sym?.Kind == SymbolKind.Function)
            {
                // user 定義関数 (= SLANG ソース内 FuncDef)
                cName = FuncIdent(funcName);
                retType = sym.Type is FunctionType uft ? uft.ReturnType : SlangType.Word;
            }
            else
            {
                // MachineFunction or undeclared 両方とも env c_bindings: で
                // override する余地を与える。env binding hit なら使用、なければ
                // MachineFunction error or undeclared error。
                var envBinding = _cBindings.Lookup(funcName);
                if (envBinding != null)
                {
                    cName = envBinding.CName;
                    var paramTypes = envBinding.Params.Select(CBindingRegistry.MapType).ToList();
                    cfuncSig = new FunctionType(CBindingRegistry.MapType(envBinding.Return), paramTypes);
                    retType = cfuncSig.ReturnType;
                }
                else if (sym?.Kind == SymbolKind.MachineFunction)
                {
                    Error($"MACHINE function `{funcName}` is not supported by oscar_c backend; gate the call with `#IF ENV_TYPE==7` or `#IF BACKEND==1`, or provide it via env c_bindings: / CFUNC", node.Span);
                    return new("/* unsupported MACHINE call */0", SlangType.Word);
                }
                else
                {
                    Error($"undeclared function `{funcName}` cannot be called from oscar_c backend; declare it as a function, CFUNC, or env c_bindings entry", node.Span);
                    return new($"/* undeclared {funcName} */0", SlangType.Word);
                }
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
        if (srcType == null || srcType.Equals(targetType))
        {
            // PointerType target は明示 cast を残す。理由: SLANG StringLiteral は
            // PointerType(Byte) を返すが、C 側では `const u8[]` (string literal の型)
            // で表現される。CFUNC / c_bindings の `unsigned char *` 引数に渡すには
            // 明示 cast が必要 (= oscar64 は const から non-const への暗黙変換を拒否)。
            if (targetType is PointerType)
            {
                return $"(({CTypeMapper.MapDeclType(targetType)})({expr.Text}))";
            }
            return expr.Text;
        }

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
        // SLANG 起源 (CFUNC / user Function) が同名なら Phase 1 で先に emit 済 → skip。
        // SemanticAnalyzer が自動登録する builtin (= SymbolKind.MachineFunction、
        // INPUT / LOCATE / INKEY 等) は env binding が override する想定なので skip しない
        // (= env binding の extern を必ず emit、VisitCallExpr の resolve も env を優先)。
        foreach (var b in _cBindings.All)
        {
            var existing = _globals?.GlobalScope.ResolveLocal(b.Name);
            if (existing?.Kind == SymbolKind.CFunction || existing?.Kind == SymbolKind.Function)
                continue;

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
