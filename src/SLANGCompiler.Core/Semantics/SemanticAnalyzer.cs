using SLANGCompiler.Lexer;
using SLANGCompiler.Parser.Ast;

namespace SLANGCompiler.Semantics;

/// <summary>
/// 意味解析パス: ASTを走査してシンボルテーブルを構築する。
/// - グローバル/ローカル変数の登録
/// - 関数シグネチャの登録
/// - ローカル変数のIYオフセット計算
/// - システム配列・登録済み変数の事前登録
/// </summary>
public class SemanticAnalyzer : IAstVisitor<object?>
{
    private readonly SymbolTable _symbols;
    private readonly DiagnosticBag _diagnostics;
    private readonly ConstEvaluator _constEval;
    private readonly HashSet<string> _usedAsmLabels = new(StringComparer.OrdinalIgnoreCase);
    private bool _inStaticDecl;
    private string? _currentFuncName;
    private FuncInfo? _currentFunc;
    // 関数の中の CONST (大域の名前とのぶつかりを、すべての大域の名前が分かった最後に調べる)
    private readonly List<(string Func, ConstDecl Decl)> _localConstDecls = new();
    // 関数の中で、後ろで宣言する CONST の名前 (配列の大きさで宣言より前に使ったらエラーにする)
    private HashSet<string>? _pendingLocalConsts;
    private bool _inMachineStatic;

    /// <summary>プリプロセッサの #IF の条件に書かれた名前 (<see cref="Lexer.Preprocessor.IdentifiersUsedByIf"/>)</summary>
    public IReadOnlySet<string>? IdentifiersUsedByPreprocessorIf { get; set; }
    private StringComparer NameComparer => _caseSensitive ? StringComparer.Ordinal : StringComparer.OrdinalIgnoreCase;
    // #MODULE 処理中のオーバーレイインデックス。null = メイン側。VAR/ARRAY はこれで
    // overlay scope の private シンボル化 + AsmLabel を `_V_M<idx>_<name>` に切替える。
    private int? _currentOverlayIndex;
    private int _overlayCount;

    public SymbolTable Symbols => _symbols;

    private readonly bool _caseSensitive;

    public SemanticAnalyzer(DiagnosticBag diagnostics, bool caseSensitive = false)
    {
        _diagnostics = diagnostics;
        _caseSensitive = caseSensitive;
        _symbols = new SymbolTable(caseSensitive);
        _constEval = new ConstEvaluator(_symbols);
        RegisterBuiltins();
    }

    public void Analyze(CompilationUnit unit)
    {
        unit.Accept(this);
        CheckLocalConstGlobalNames();
    }

    /// <summary>
    /// 関数の中の CONST が大域の名前と同じならエラー。局所の定数が大域の名前を隠すと、名前を引く所の
    /// すべてで局所を先に見ないと黙って大域を使ってしまうので、隠すこと自体を受け付けない (仕様書の CONST の節)
    /// </summary>
    private void CheckLocalConstGlobalNames()
    {
        foreach (var (func, decl) in _localConstDecls)
        {
            if (_symbols.GlobalScope.ResolveLocal(decl.Name) != null)
                _diagnostics.Error(
                    $"CONST '{decl.Name}' in function '{func}' has the same name as a global name", decl.Span);
        }
    }

    // ==== ビルトイン登録 ====

    private void RegisterBuiltins()
    {
        // 登録済み記号定数
        DefineConst("TRUE", 1);
        DefineConst("FALSE", 0);

        // システム配列
        DefineSystemArray("MEM", SlangType.Byte);
        DefineSystemArray("MEMW", SlangType.Word);
        DefineSystemArray("PORT", SlangType.Byte);
        DefineSystemArray("PORTW", SlangType.Word);
        DefineSystemArray("SOS", SlangType.Byte);
        DefineSystemArray("SOSW", SlangType.Word);

        // 登録済み変数（旧実装準拠: ランタイムが_CARRY, _AF等を参照するため）
        foreach (var name in new[] { "^BC", "^DE", "^HL", "^IX", "^IY", "^AF", "^SP", "^CARRY", "^ZERO" })
        {
            var sym = _symbols.Define(name, SymbolKind.Variable, SlangType.Word);
            sym.IsGlobal = true;
            sym.AsmLabel = $"_{name[1..].ToUpperInvariant()}";
        }
        // ^A = _AF + 1 のエイリアス（__WORK__で領域を消費しない）
        {
            var sym = _symbols.Define("^A", SymbolKind.Variable, SlangType.Word);
            sym.IsGlobal = true;
            sym.AsmLabel = "_A";
        }
        // ^CY = ^CARRY のエイリアス（同一ラベル _CARRY を共有）
        {
            var sym = _symbols.Define("^CY", SymbolKind.Variable, SlangType.Word);
            sym.IsGlobal = true;
            sym.AsmLabel = "_CARRY";
        }
        {
            var sym = _symbols.Define("@KBUFF", SymbolKind.Variable, SlangType.Word);
            sym.IsGlobal = true;
            sym.AsmLabel = "_KBUFF";
        }

        // 登録済み基本関数
        var builtinFuncs = new (string Name, int Params)[]
        {
            ("BEEP", 0), ("STOP", 0), ("LOCATE", 2), ("INKEY", 1),
            ("INPUT", 0), ("GETL", 1), ("GETLIN", 2), ("LINPUT", 2),
            ("WIDTH", 1), ("SCREEN", 2), ("PRMODE", 1),
            ("BIT", 2), ("SET", 2), ("RESET", 2),
            ("ABS", 1), ("SEX", 1), ("SGN", 1), ("RND", 1),
            ("VTOS", 2), ("GETREG", 0), ("CALL", 1),
        };
        foreach (var (name, pc) in builtinFuncs)
        {
            var paramTypes = Enumerable.Repeat(SlangType.Word, pc).ToList();
            var funcType = new FunctionType(SlangType.Word, paramTypes);
            // ビルトイン関数はMACHINE関数（レジスタ渡し: HL, DE, BC）
            var sym = _symbols.Define(name, SymbolKind.MachineFunction, funcType);
            sym.IsGlobal = true;
            sym.AsmLabel = name;
        }
    }

    private void DefineConst(string name, int value)
    {
        var sym = _symbols.Define(name, SymbolKind.Constant, SlangType.Word);
        sym.ConstValue = value;
        sym.IsGlobal = true;
    }

    private void DefineSystemArray(string name, SlangType elementType)
    {
        var arrayType = new MemoryArrayType(elementType);
        var sym = _symbols.Define(name, SymbolKind.Variable, arrayType);
        sym.IsGlobal = true;
        sym.AsmLabel = $"_SYS_{name}";
    }

    // ==== Visitor ====

    public object? VisitCompilationUnit(CompilationUnit node)
    {
        foreach (var def in node.Definitions)
            def.Accept(this);
        return null;
    }

    public object? VisitVarDecl(VarDecl node)
    {
        CheckNotLocalConstName(node.Name, node.Span);
        // 単純変数の {…} の初期値は、どの出力も書かない (Z80 は黙って捨てていた)。エラー
        if (node.InitialCode != null)
            _diagnostics.Error($"VAR '{node.Name}' cannot have a {{...}} initializer (use VAR {node.Name} = value)", node.Span);
        CheckDeclUsesPendingConsts(node.Address, node.InitialValue, node.InitialCode);
        CheckDeclAddress(node.Address, scalarVar: true, node.Span);
        var type = DataSizeToType(node.Size);
        CheckNotTrueFalse(node.Name, node.Span);
        var sym = _symbols.Define(node.Name, SymbolKind.Variable, type);

        if (_currentFunc != null && _symbols.IsGlobalScope == false && !_inStaticDecl)
        {
            // ローカル変数(動的): BYTE/WORDとも2バイト、FLOATは3バイト
            // #MODULE 内関数のローカルも IY オフセットに割り付ける (overlay private 化は
            // "モジュール直下" 宣言だけの話)
            sym.IsGlobal = false;
            sym.Offset = _currentFunc.AllocLocal(type.ByteSize, isFloat: node.Size == DataSize.Float);
            sym.AsmLabel = null; // IYオフセットでアクセス
        }
        else
        {
            // グローバル変数 or 関数内静的宣言 or オーバーレイ モジュール直下: __WORK__ に配置
            sym.IsGlobal = true;
            if (_currentOverlayIndex.HasValue && !_inStaticDecl && _currentFunc == null)
                sym.AsmLabel = LabelUtils.OverlayVarLabel(_currentOverlayIndex.Value, node.Name);
            else if (_inStaticDecl && _currentFuncName != null)
                sym.AsmLabel = LabelUtils.StaticVarLabel(_currentFuncName, node.Name);
            else
                sym.AsmLabel = LabelUtils.UserVarLabel(node.Name);
            if (node.Address != null)
            {
                // アドレス固定
                // TODO: 定数式を評価してAddress設定
            }
        }

        if (node.InitialValue != null)
        {
            node.InitialValue.Accept(this);

            // グローバル/静的変数の初期値は定数でなければならない (FLOAT の変数の初期値は IrGenerator が形を見る)
            if (sym.IsGlobal && node.InitialValue is not IntegerLiteral && node.Size != DataSize.Float)
            {
                var constVal = _constEval.Evaluate(node.InitialValue);
                if (!constVal.HasValue)
                {
                    _diagnostics.Report(DiagnosticSeverity.Warning,
                        $"Non-constant initializer for static/global variable '{node.Name}' may not be initialized at runtime",
                        node.Span);
                }
            }
        }

        return null;
    }

    public object? VisitArrayDecl(ArrayDecl node)
    {
        CheckNotLocalConstName(node.Name, node.Span);
        CheckDeclUsesPendingConsts(node.Address, node.InitialValue, node.InitialCode);
        CheckDeclAddress(node.Address, scalarVar: false, node.Span);
        var elemType = DataSizeToType(node.Size);
        var dims = new List<int>();
        foreach (var dim in node.Dimensions)
        {
            if (dim == null)
            {
                dims.Add(0); // 間接配列
            }
            else
            {
                // 関数の中に CONST があるときは、大きさの式も CONST への書き込みなどを調べる
                if (_currentFunc?.LocalConstants.Count > 0) dim.Accept(this);
                var val = _constEval.Evaluate(dim);
                if (!val.HasValue) CheckConstUsedBeforeDeclaration(dim);
                dims.Add(val.HasValue ? val.Value + 1 : 1); // 仕様: 定数式+1個分確保
            }
        }

        SlangType type;
        if (dims.All(d => d == 0))
        {
            // 間接配列 (VAR x[]) → ポインタ的
            type = new PointerType(elemType);
        }
        else
        {
            type = new ArrayType(elemType, dims);
        }

        CheckNotTrueFalse(node.Name, node.Span);
        var sym = _symbols.Define(node.Name, SymbolKind.Variable, type);
        // ARRAYキーワードで宣言されたもののみフラグ設定
        // VAR X[] はポインタ変数（LoadVar）、ARRAY X[] は配列（LoadAddr）
        if (node.IsArrayKeyword)
            sym.IsArrayDecl = true;

        if (_currentFunc != null && !_symbols.IsGlobalScope && !_inStaticDecl)
        {
            // #MODULE 内関数のローカル ARRAY も通常のローカル扱い (IY フレーム)
            sym.IsGlobal = false;
            sym.Offset = _currentFunc.AllocLocal(type.ByteSize, isArray: true);
            sym.AsmLabel = null;
        }
        else
        {
            sym.IsGlobal = true;
            if (_currentOverlayIndex.HasValue && !_inStaticDecl && _currentFunc == null)
                sym.AsmLabel = LabelUtils.OverlayVarLabel(_currentOverlayIndex.Value, node.Name);
            else if (_inStaticDecl && _currentFuncName != null)
                sym.AsmLabel = LabelUtils.StaticVarLabel(_currentFuncName, node.Name);
            else
                sym.AsmLabel = LabelUtils.UserVarLabel(node.Name);
        }

        // 間接変数の初期値 (VAR P[] = 番地) の式も調べる (代入の禁止など、単純変数の初期値と同じ)
        node.InitialValue?.Accept(this);

        // 固定アドレスの配列の CODE の並びの初期値は、どの出力も書かない (エラーにする。前は構文エラーだった形)
        if (node.Address != null && node.InitialCode != null)
            _diagnostics.Error($"Array '{node.Name}' with a fixed address cannot have an initializer", node.Span);


        // Issue #190: ARRAY initializer 容量超過 check (= 全 backend 共通)。
        // SLANG 仕様: 「添字省略時はチェックしない」 — 全次元 null (= PointerType)
        // と多次元の一部 null (= 例 `ARRAY BYTE A[][3]`) のどちらも容量判定 skip。
        // 固定サイズ ARRAY (= dims に null を含まない) のみ ArrayInitialCodeSizer で
        // 展開後 byte 数を計算し、 type.ByteSize 超過なら error。
        if (node.InitialCode != null
            && type is ArrayType arrType
            && !node.Dimensions.Any(d => d == null))
        {
            int? byteCount = ArrayInitialCodeSizer.CalculateByteCount(
                node.InitialCode, node.Size, _symbols, _diagnostics);
            if (byteCount.HasValue && byteCount.Value > arrType.ByteSize)
            {
                _diagnostics.Error(
                    $"ARRAY `{node.Name}` initializer is {byteCount.Value} bytes but capacity is {arrType.ByteSize} bytes (= SLANG 仕様「多すぎる場合エラー」)",
                    node.Span);
            }
        }

        return null;
    }

    public object? VisitConstDecl(ConstDecl node)
    {
        if (_inMachineStatic)
        {
            _diagnostics.Error("CONST is not allowed in a MACHINE declaration", node.Span);
            return null;
        }
        if (_currentFunc != null)
        {
            DefineLocalConst(node);
            return null;
        }
        if (node.Value is CodeExpr)
        {
            // CODEブロック型CONST: ラベル付きデータブロック（参照時はアドレスが渡される）
            // overlay 配下でも global scope に登録 (main から参照可能に保つ)
            var sym = _currentOverlayIndex.HasValue
                ? _symbols.DefineInGlobal(node.Name, SymbolKind.Variable, SlangType.Word)
                : _symbols.Define(node.Name, SymbolKind.Variable, SlangType.Word);
            sym.IsGlobal = true;
            sym.IsCodeBlock = true;
            sym.AsmLabel = (_inStaticDecl && _currentFuncName != null)
                ? LabelUtils.StaticVarLabel(_currentFuncName, node.Name)
                : LabelUtils.UserVarLabel(node.Name);
        }
        else
        {
            var sym = _currentOverlayIndex.HasValue
                ? _symbols.DefineInGlobal(node.Name, SymbolKind.Constant, SlangType.Word)
                : _symbols.Define(node.Name, SymbolKind.Constant, SlangType.Word);
            sym.IsAsmEqu = node.IsAsmEqu;
            var val = _constEval.Evaluate(node.Value);
            if (val.HasValue)
            {
                sym.ConstValue = val.Value;
            }
            else
            {
                // FLOAT定数式を試行: CONST DEG2RAD = 3.14 / 180.0 等
                var floatVal = _constEval.EvaluateFloat(node.Value);
                if (floatVal.HasValue)
                {
                    sym.ConstFloatValue = floatVal.Value;
                    sym.Type = SlangType.Float;
                }
                else
                {
                    // アセンブラ式定数: CONST X=SOROBAN, CONST X=LABEL+$14等
                    // AST保持のみ。文字列化はIR段階で（前方参照対応）
                    sym.ConstAst = node.Value;
                }
            }
        }
        return null;
    }

    public object? VisitCFuncDecl(CFuncDecl node)
    {
        // SLANG 型 → SlangType list 変換
        var paramTypes = new List<SlangType>();
        if (node.Parameters != null)
        {
            foreach (var p in node.Parameters)
            {
                SlangType t = DataSizeToType(p.Size);
                if (p.IsArray) t = new PointerType(t);
                paramTypes.Add(t);
            }
        }
        else
        {
            // 略式: 全部 WORD 仮定
            for (int i = 0; i < node.ParamCount; i++)
                paramTypes.Add(SlangType.Word);
        }
        SlangType retType = node.IsVoidReturn
            ? SlangType.Void
            : (node.ReturnSize.HasValue ? DataSizeToType(node.ReturnSize.Value) : SlangType.Word);
        var funcType = new FunctionType(retType, paramTypes);

        // 既存 MACHINE 宣言と同じく、overlay 配下でも main から参照可能なよう global に登録。
        var sym = _currentOverlayIndex.HasValue
            ? _symbols.DefineInGlobal(node.Name, SymbolKind.CFunction, funcType)
            : _symbols.Define(node.Name, SymbolKind.CFunction, funcType);
        sym.IsGlobal = true;
        sym.CName = node.CName;
        return null;
    }

    public object? VisitMachineDecl(MachineDecl node)
    {
        if (node.Address != null && node.CodeBody != null)
            _diagnostics.Error("MACHINE declaration cannot have both address and CODE body", node.Span);

        var paramTypes = new List<SlangType>();
        if (node.ParamCount.HasValue)
            paramTypes = Enumerable.Repeat(SlangType.Word, node.ParamCount.Value).ToList();
        var funcType = new FunctionType(SlangType.Word, paramTypes);
        var sym = _currentOverlayIndex.HasValue
            ? _symbols.DefineInGlobal(node.Name, SymbolKind.MachineFunction, funcType)
            : _symbols.Define(node.Name, SymbolKind.MachineFunction, funcType);
        sym.IsGlobal = true;
        sym.AsmLabel = $"_{LabelUtils.SanitizeLabel(node.Name)}";
        sym.HasCodeBody = node.CodeBody != null;
        if (node.Address != null)
            sym.AddressAst = node.Address;  // AST保持、文字列化はIR段階で

        // 静的宣言を関数スコープで処理（AsmLabelに関数名プレフィックス付与）
        if (node.StaticDeclarations.Count > 0)
        {
            _currentFuncName = node.Name;
            _inStaticDecl = true;
            _inMachineStatic = true;
            foreach (var decl in node.StaticDeclarations)
                decl.Accept(this);
            _inMachineStatic = false;
            _inStaticDecl = false;
            _currentFuncName = null;
        }
        return null;
    }

    public object? VisitFuncDef(FuncDef node)
    {
        // 関数自体をグローバルに登録 (overlay 配下でも main と共有する global scope へ)
        var paramTypes = node.Parameters.Select(MakeParamType).ToList();
        var returnType = DataSizeToType(node.ReturnSize);
        var funcType = new FunctionType(returnType, paramTypes);
        var funcSym = _currentOverlayIndex.HasValue
            ? _symbols.DefineInGlobal(node.Name, SymbolKind.Function, funcType)
            : _symbols.Define(node.Name, SymbolKind.Function, funcType);
        funcSym.IsGlobal = true;
        funcSym.AsmLabel = LabelUtils.FunctionLabel(node.Name);

        // 関数スコープ開始
        _symbols.PushScope(node.Name);
        var prevFunc = _currentFunc;
        _currentFunc = new FuncInfo(node.Name, NameComparer);
        var prevPending = _pendingLocalConsts;
        _pendingLocalConsts = CollectConstNames(node.StaticDeclarations.Concat(node.LocalDeclarations));

        // 関数スコープ内で定義された名前を追跡し、重複をエラー化する。
        // SymbolTable.Scope.Define は後勝ち上書きで silent overwrite するため、
        // ここで明示的に重複を検出しないと param と static/local 宣言が同名になっても
        // 警告すら出ず IR と semantic で解決順が逆転する危険な状態になる。
        var nameComparer = _caseSensitive ? StringComparer.Ordinal : StringComparer.OrdinalIgnoreCase;
        var defined = new HashSet<string>(nameComparer);

        // 仮引数を動的変数として登録
        // 仕様: 引数は(IY+$70)から順に配置
        int argOffset = 0x70;
        foreach (var param in node.Parameters)
        {
            if (!defined.Add(param.Name))
            {
                _diagnostics.Error(
                    $"duplicate parameter name '{param.Name}' in function '{node.Name}'",
                    param.Span);
                continue;
            }
            var pType = MakeParamType(param);
            CheckNotTrueFalse(param.Name, param.Span);
            var pSym = _symbols.Define(param.Name, SymbolKind.Parameter, pType);
            pSym.IsGlobal = false;
            pSym.Offset = argOffset;
            argOffset += 2; // 引数は常にWORD
        }

        // 静的宣言（__WORK__に配置、AsmLabelに関数名プレフィックス）
        _currentFuncName = node.Name;
        _inStaticDecl = true;
        foreach (var decl in node.StaticDeclarations)
        {
            CheckDuplicateDeclName(decl, defined, node.Name, "static declaration");
            decl.Accept(this);
        }
        _inStaticDecl = false;

        // 局所宣言（IYフレームに配置）
        foreach (var decl in node.LocalDeclarations)
        {
            CheckDuplicateDeclName(decl, defined, node.Name, "local declaration");
            decl.Accept(this);
        }

        // 本体
        node.Body.Accept(this);
        // END(式) の式も、関数の中の CONST への書き込みなどを調べる (CONST のない関数ではいままでどおり走査しない)
        if (_currentFunc.LocalConstants.Count > 0) node.ReturnValue?.Accept(this);

        // 関数情報を保存
        funcSym.ConstValue = _currentFunc; // FuncInfoを紐付け

        _currentFunc = prevFunc;
        _pendingLocalConsts = prevPending;
        _symbols.PopScope();
        return null;
    }

    public object? VisitParamDecl(ParamDecl node) => null;

    // ==== Statements ====

    public object? VisitBlock(Block node)
    {
        foreach (var stmt in node.Statements)
            stmt.Accept(this);
        return null;
    }

    public object? VisitExpressionStmt(ExpressionStmt node) { node.Expr.Accept(this); return null; }
    public object? VisitIfStmt(IfStmt node)
    {
        foreach (var (cond, body) in node.Branches)
        {
            cond.Accept(this);
            body.Accept(this);
        }
        node.ElseBody?.Accept(this);
        return null;
    }
    public object? VisitWhileStmt(WhileStmt node) { node.Condition.Accept(this); node.Body.Accept(this); return null; }
    public object? VisitRepeatStmt(RepeatStmt node) { node.Body.Accept(this); node.Condition.Accept(this); return null; }
    public object? VisitLoopStmt(LoopStmt node) { node.Body.Accept(this); return null; }
    public object? VisitForStmt(ForStmt node)
    {
        if (IsLocalConst(node.Variable))
            _diagnostics.Error($"cannot use CONST '{node.Variable}' as a FOR variable", node.Span);
        node.From.Accept(this);
        node.To.Accept(this);
        node.Body.Accept(this);
        return null;
    }
    public object? VisitCaseStmt(CaseStmt node)
    {
        node.Expr.Accept(this);
        foreach (var b in node.Branches)
        {
            b.Value?.Accept(this);
            b.RangeEnd?.Accept(this);
            b.Body?.Accept(this);
        }
        return null;
    }
    public object? VisitExitStmt(ExitStmt node) { node.Level?.Accept(this); return null; }
    public object? VisitContinueStmt(ContinueStmt node) => null;
    public object? VisitReturnStmt(ReturnStmt node) { node.Value?.Accept(this); return null; }
    public object? VisitGotoStmt(GotoStmt node) => null;
    public object? VisitLabelStmt(LabelStmt node) => null;
    public object? VisitPrintStmt(PrintStmt node)
    {
        foreach (var arg in node.Arguments) arg.Accept(this);
        return null;
    }

    // ==== Expressions ====

    public object? VisitIntegerLiteral(IntegerLiteral node) => null;
    public object? VisitFloatLiteral(FloatLiteral node) => null;
    public object? VisitStringLiteral(StringLiteral node) => null;
    public object? VisitIdentifier(IdentifierExpr node)
    {
        var sym = _symbols.Resolve(node.Name);
        if (sym == null)
        {
            // 未定義の識別子 → 前方参照の関数かもしれないので警告のみ
            // (SLANGは関数の前方参照を許可)
        }
        return null;
    }
    public object? VisitBinaryExpr(BinaryExpr node) { node.Left.Accept(this); node.Right.Accept(this); return null; }
    public object? VisitUnaryExpr(UnaryExpr node) { node.Operand.Accept(this); return null; }
    public object? VisitAssignExpr(AssignExpr node)
    {
        CheckNotArrayAssignment(node.Target, "assign to", node.Span);
        node.Target.Accept(this); node.Value.Accept(this); return null;
    }
    public object? VisitCompoundAssignExpr(CompoundAssignExpr node)
    {
        CheckNotArrayAssignment(node.Target, "compound-assign", node.Span);
        node.Target.Accept(this); node.Value.Accept(this); return null;
    }
    public object? VisitIncrementExpr(IncrementExpr node)
    {
        CheckNotArrayAssignment(node.Operand, "increment", node.Span);
        node.Operand.Accept(this); return null;
    }

    /// <summary>
    /// Issue #190: <strong>固定サイズ ARRAY</strong> 宣言 symbol への代入系操作
    /// (AssignExpr / CompoundAssignExpr / IncrementExpr) を reject。
    /// 配列実体の置換は SLANG 仕様で意味曖昧で、 backend で無効 code を emit する
    /// 原因になるため semantic で潰す。
    /// <para>許可するもの:</para>
    /// <list type="bullet">
    /// <item><description>ArrayAccessExpr (= <c>A[i] = X</c>) — 要素代入</description></item>
    /// <item><description><c>VAR BYTE T[];</c> (= IsArrayDecl=false、 PointerType) — ポインタ代入</description></item>
    /// <item><description><c>ARRAY BYTE P[];</c> (= 添字省略 ARRAY、 IsArrayDecl=true だが
    /// 型は PointerType = 間接配列) — SLANG 仕様で実質ポインタ扱い、 代入可能</description></item>
    /// </list>
    /// 判定は <c>IsArrayDecl == true</c> かつ <c>Type is ArrayType</c> (= 実体配列の場合のみ) で行う。
    /// </summary>
    private void CheckNotArrayAssignment(Expression target, string opName, SourceSpan span)
    {
        CheckNotLocalConstWrite(target, opName, span);
        if (target is IdentifierExpr id)
        {
            var sym = _symbols.Resolve(id.Name);
            // IsArrayDecl + ArrayType の場合のみ reject (= 添字省略の PointerType は通過)
            if (sym?.IsArrayDecl == true && sym.Type is ArrayType)
            {
                _diagnostics.Error(
                    $"cannot {opName} ARRAY symbol `{id.Name}`; ARRAY 宣言は配列実体で再代入不可 (`VAR BYTE T[];` / `ARRAY BYTE P[];` のポインタ宣言ならOK、 `{id.Name}[i] = ...` の要素代入は許可)",
                    span);
            }
        }
        // ArrayAccessExpr (= `A[i] = X`) は通過 (= 要素代入は許可)
    }
    public object? VisitCallExpr(CallExpr node)
    {
        node.Function.Accept(this);
        foreach (var arg in node.Arguments) arg.Accept(this);
        return null;
    }
    public object? VisitArrayAccessExpr(ArrayAccessExpr node)
    {
        if (node.Array is IdentifierExpr aid && IsLocalConst(aid.Name))
            _diagnostics.Error($"CONST '{aid.Name}' is not an array", node.Span);
        node.Array.Accept(this);
        foreach (var idx in node.Indices) idx.Accept(this);
        return null;
    }
    public object? VisitConditionalExpr(ConditionalExpr node)
    {
        node.Condition.Accept(this); node.TrueExpr.Accept(this); node.FalseExpr.Accept(this);
        return null;
    }
    public object? VisitCommaExpr(CommaExpr node) { node.Left.Accept(this); node.Right.Accept(this); return null; }
    public object? VisitAddressOfExpr(AddressOfExpr node)
    {
        if (node.Operand is IdentifierExpr id && IsLocalConst(id.Name))
            _diagnostics.Error($"cannot take the address of CONST '{id.Name}'", node.Span);
        node.Operand.Accept(this);
        return null;
    }
    public object? VisitHighLowExpr(HighLowExpr node) { node.Operand.Accept(this); return null; }
    public object? VisitCodeExpr(CodeExpr node) { foreach (var v in node.Values) v.Accept(this); return null; }
    public object? VisitCastExpr(CastExpr node) { node.Operand.Accept(this); return null; }
    public object? VisitStringFuncExpr(StringFuncExpr node) { foreach (var a in node.Arguments) a.Accept(this); return null; }

    // ==== Directives ====

    public object? VisitOrgDirective(OrgDirective node) => null;
    public object? VisitWorkDirective(WorkDirective node) => null;
    public object? VisitOffsetDirective(OffsetDirective node) => null;
    public object? VisitModuleBlock(ModuleBlock node)
    {
        // #MODULE 直下での非対応構文を検出 (モジュール内関数本体は走査対象外)
        foreach (var def in node.Definitions)
            ValidateModuleTopLevelDecl(def);

        int idx = _overlayCount++;
        var prevIndex = _currentOverlayIndex;
        _symbols.PushScope($"overlay_{idx}");
        _currentOverlayIndex = idx;
        try
        {
            foreach (var def in node.Definitions) def.Accept(this);
        }
        finally
        {
            _currentOverlayIndex = prevIndex;
            _symbols.PopScope();
        }
        return null;
    }

    private void ValidateModuleTopLevelDecl(AstNode def)
    {
        switch (def)
        {
            case VarDecl vd:
                if (vd.InitialValue != null || vd.InitialCode != null)
                    _diagnostics.Error(
                        $"Module-level VAR '{vd.Name}' cannot have initializer inside #MODULE", vd.Span);
                if (vd.Address != null)
                    _diagnostics.Error(
                        $"Module-level VAR '{vd.Name}' cannot have fixed address inside #MODULE", vd.Span);
                break;
            case ArrayDecl ad:
                if (ad.InitialValue != null || ad.InitialCode != null)
                    _diagnostics.Error(
                        $"Module-level ARRAY '{ad.Name}' cannot have initializer inside #MODULE", ad.Span);
                if (ad.Address != null)
                    _diagnostics.Error(
                        $"Module-level ARRAY '{ad.Name}' cannot have fixed address inside #MODULE", ad.Span);
                break;
            case PlainAsm pa:
                _diagnostics.Error("Top-level #ASM block is not allowed inside #MODULE", pa.Span);
                break;
        }
    }
    public object? VisitPlainAsm(PlainAsm node) => null;

    // ==== Helpers ====

    private static SlangType DataSizeToType(DataSize size) => size switch
    {
        DataSize.Byte => SlangType.Byte,
        DataSize.Word => SlangType.Word,
        DataSize.Float => SlangType.Float,
        _ => SlangType.Word,
    };

    /// <summary>
    /// 関数の中の CONST。宣言したその場で値が決まる整数か FLOAT の定数だけを受け付け、関数のスコープに定義する
    /// (#MODULE の中の関数でも大域には出さない)
    /// </summary>
    private void DefineLocalConst(ConstDecl node)
    {
        var func = _currentFunc!;
        if (node.IsAsmEqu)
        {
            _diagnostics.Error("CONST ASM is only allowed at global scope", node.Span);
            return;
        }
        if (node.Value is CodeExpr)
        {
            _diagnostics.Error("CONST with a code block is not supported in a function", node.Span);
            return;
        }
        // IR の局所変数の表は大文字小文字を区別しないので、ここも区別せずに調べる
        if (_symbols.CurrentScope.Symbols.Keys.Any(k => string.Equals(k, node.Name, StringComparison.OrdinalIgnoreCase)))
        {
            _diagnostics.Error(
                $"duplicate name '{node.Name}' in function '{func.Name}' (CONST conflicts with parameter or other declaration)",
                node.Span);
            return;
        }
        // (関数の中の CONST は大域の名前と同じ名前にできないので、#IF のその名前は、この局所の CONST か未定義のどちらか)
        if (IdentifiersUsedByPreprocessorIf?.Contains(node.Name) == true)
        {
            // #IF はファイル全体に効くので、関数の中の CONST がほかの関数の #IF を変えてしまう
            _diagnostics.Error($"CONST '{node.Name}' in a function cannot be used in #IF (use a global CONST)", node.Span);
            return;
        }
        var val = _constEval.Evaluate(node.Value);
        var floatVal = val.HasValue ? null : _constEval.EvaluateFloat(node.Value);
        if (!val.HasValue && !floatVal.HasValue)
        {
            CheckConstUsedBeforeDeclaration(node.Value);
            _diagnostics.Error(
                $"CONST '{node.Name}' in a function must be a compile-time integer or FLOAT constant", node.Span);
            return;
        }
        var sym = _symbols.Define(node.Name, SymbolKind.Constant, SlangType.Word);
        sym.IsGlobal = false;
        if (val.HasValue)
            sym.ConstValue = val.Value;
        else
        {
            sym.ConstFloatValue = floatVal!.Value;
            sym.Type = SlangType.Float;
        }
        func.LocalConstants[node.Name] = sym;
        _pendingLocalConsts?.Remove(node.Name);
        _localConstDecls.Add((func.Name, node));
    }

    /// <summary>宣言の並び (カンマ並びの Block を含む) の中の CONST の名前</summary>
    private HashSet<string>? CollectConstNames(IEnumerable<AstNode> decls)
    {
        HashSet<string>? names = null;
        void Walk(AstNode d)
        {
            if (d is ConstDecl c) (names ??= new(NameComparer)).Add(c.Name);
            else if (d is Block b) foreach (var x in b.Statements) Walk(x);
        }
        foreach (var d in decls) Walk(d);
        return names;
    }

    /// <summary>関数の中で、同じ名前の CONST がもう定義されていたらエラー (VAR・ARRAY を定義するとき)</summary>
    private void CheckNotLocalConstName(string name, SourceSpan span)
    {
        // IR の局所変数の表は大文字小文字を区別しないので、ここも区別せずに調べる
        if (_currentFunc != null
            && _currentFunc.LocalConstants.Keys.Any(k => string.Equals(k, name, StringComparison.OrdinalIgnoreCase)))
            _diagnostics.Error(
                $"duplicate name '{name}' in function '{_currentFunc.Name}' (declaration conflicts with CONST)", span);
    }

    /// <summary>式の中に、この関数で後ろに宣言する CONST の名前があればエラー (宣言より前では使えない)</summary>
    private void CheckConstUsedBeforeDeclaration(Expression expr)
    {
        if (_pendingLocalConsts == null || _pendingLocalConsts.Count == 0) return;
        ForEachName(expr, (name, span) =>
        {
            if (_pendingLocalConsts.Contains(name))
                _diagnostics.Error($"CONST '{name}' is used before its declaration", span);
        });
    }

    /// <summary>式の中の名前 (識別子と CODE のラベル参照) をすべて渡す。式の入れ物はすべてたどる</summary>
    internal static void ForEachName(Expression? expr, Action<string, SourceSpan> action)
    {
        void Walk(Expression? e)
        {
            switch (e)
            {
                case IdentifierExpr id: action(id.Name, id.Span); break;
                case CodeLabelRef lr: action(lr.Label, lr.Span); break;
                case BinaryExpr b: Walk(b.Left); Walk(b.Right); break;
                case CommaExpr c: Walk(c.Left); Walk(c.Right); break;
                case AssignExpr a: Walk(a.Target); Walk(a.Value); break;
                case CompoundAssignExpr ca: Walk(ca.Target); Walk(ca.Value); break;
                case UnaryExpr u: Walk(u.Operand); break;
                case IncrementExpr inc: Walk(inc.Operand); break;
                case AddressOfExpr ad: Walk(ad.Operand); break;
                case CastExpr c: Walk(c.Operand); break;
                case HighLowExpr h: Walk(h.Operand); break;
                case CodeEvalExpr ev: Walk(ev.Inner); break;
                case ConditionalExpr ce: Walk(ce.Condition); Walk(ce.TrueExpr); Walk(ce.FalseExpr); break;
                case CallExpr call: Walk(call.Function); foreach (var x in call.Arguments) Walk(x); break;
                case ArrayAccessExpr ar: Walk(ar.Array); foreach (var x in ar.Indices) Walk(x); break;
                case CodeExpr code: foreach (var x in code.Values) Walk(x); break;
                case StringFuncExpr sf: foreach (var x in sf.Arguments) Walk(x); break;
            }
        }
        Walk(expr);
    }

    /// <summary>
    /// 関数の中に CONST があるとき、宣言の固定アドレスの式も、CONST への書き込みなどを調べる。
    /// VAR の固定アドレスは整数の値でしか置けないので、局所の CONST を使っていて値が決まらなければエラー
    /// (そのままだと黙って __WORK__ に置かれる)
    /// </summary>
    private void CheckDeclAddress(Expression? address, bool scalarVar, SourceSpan span)
    {
        if (address == null || !(_currentFunc?.LocalConstants.Count > 0)) return;
        int errorsBefore = _diagnostics.ErrorCount;
        address.Accept(this);
        if (!scalarVar || address is IntegerLiteral || _diagnostics.ErrorCount > errorsBefore) return;
        bool usesLocal = false;
        ForEachName(address, (name, _) => { if (IsLocalConst(name)) usesLocal = true; });
        if (usesLocal && _constEval.Evaluate(address) == null)
            _diagnostics.Error("fixed address using CONST in a function must be a compile-time constant", span);
    }

    /// <summary>
    /// 宣言の固定アドレス・初期値に、この関数で後ろに宣言する CONST の名前があればエラー
    /// (そのままだと外のラベルとして扱われ、アセンブルできない ASM になる)
    /// </summary>
    private void CheckDeclUsesPendingConsts(Expression? address, Expression? init, List<Expression>? code)
    {
        if (_pendingLocalConsts == null || _pendingLocalConsts.Count == 0) return;
        if (address != null) CheckConstUsedBeforeDeclaration(address);
        if (init != null) CheckConstUsedBeforeDeclaration(init);
        if (code != null) foreach (var e in code) CheckConstUsedBeforeDeclaration(e);
    }

    /// <summary>関数の中の CONST の記号か (書き込み・アドレス・添字を拒否するため)</summary>
    private bool IsLocalConst(string name)
        => _currentFunc != null && _currentFunc.LocalConstants.TryGetValue(name, out var c)
           && ReferenceEquals(_symbols.Resolve(name), c);

    /// <summary>変数・配列・引数の名前に TRUE・FALSE は使えない (構文解析が式の中の TRUE・FALSE を 1・0 にするので、読めない)</summary>
    private void CheckNotTrueFalse(string name, SourceSpan span)
    {
        if (name.Equals("TRUE", StringComparison.OrdinalIgnoreCase) || name.Equals("FALSE", StringComparison.OrdinalIgnoreCase))
            _diagnostics.Error($"'{name}' cannot be used as a variable or parameter name (it is the constant {(name.Equals("TRUE", StringComparison.OrdinalIgnoreCase) ? 1 : 0)})", span);
    }

    private void CheckNotLocalConstWrite(Expression target, string opName, SourceSpan span)
    {
        if (target is IdentifierExpr id && IsLocalConst(id.Name))
            _diagnostics.Error($"cannot {opName} CONST '{id.Name}'", span);
    }

    /// <summary>
    /// 仮引数 (ParamDecl) を semantic 型に変換する。
    /// 配列引数 (BYTE T[] など) は PointerType wrap、それ以外は基本型そのまま。
    /// IR 側 MakeParamLocalVarInfo と対応する形で IsArray を扱う。
    /// </summary>
    private static SlangType MakeParamType(ParamDecl p)
    {
        var baseType = DataSizeToType(p.Size);
        return p.IsArray ? new PointerType(baseType) : baseType;
    }

    /// <summary>
    /// 関数スコープ内の宣言ノード (VarDecl / ArrayDecl / ConstDecl) の名前が
    /// 既存定義と衝突したらエラーを出す。AST ノードによって名前フィールドが
    /// 異なるので局所的に解決する。defined set にも未定義の名前を追加する。
    /// </summary>
    private void CheckDuplicateDeclName(AstNode decl, HashSet<string> defined, string funcName, string kind)
    {
        string? name = decl switch
        {
            VarDecl v => v.Name,
            ArrayDecl a => a.Name,
            // ConstDecl は DefineLocalConst で調べる (VAR・ARRAY が後ろにあっても見つけるため)
            _ => null,
        };
        if (name == null) return;
        if (!defined.Add(name))
        {
            _diagnostics.Error(
                $"duplicate name '{name}' in function '{funcName}' ({kind} conflicts with parameter or earlier declaration)",
                decl.Span);
        }
    }
}

/// <summary>
/// 関数のローカル変数情報
/// </summary>
public class FuncInfo
{
    public string Name { get; }
    public int LocalSize { get; private set; }

    public FuncInfo(string name, StringComparer? nameComparer = null)
    {
        Name = name;
        LocalConstants = new(nameComparer ?? StringComparer.OrdinalIgnoreCase);
    }

    /// <summary>関数の中の CONST (名前 → 記号)。値はどれも宣言したその場で決まっている。名前の比べ方は記号表と同じ</summary>
    public Dictionary<string, Symbol> LocalConstants { get; }

    /// <summary>
    /// ローカル変数のオフセットを割り当てる。
    ///
    /// 元実装に準拠:
    /// - BYTE/WORD変数: 常に2バイト確保
    /// - FLOAT変数: 3バイト確保 (offset++ + offset+=2)
    /// - 配列: 実サイズ分確保
    ///
    /// 配置方向: (IY+$70)の直前から下向き。
    /// 例: 3個のWORD変数(I, J, L)
    ///   I: (IY+$6A), (IY+$6B)
    ///   J: (IY+$6C), (IY+$6D)
    ///   L: (IY+$6E), (IY+$6F)
    /// </summary>
    public int AllocLocal(int byteSize, bool isFloat = false, bool isArray = false)
    {
        if (isArray)
        {
            // 配列: 実サイズ分
            LocalSize += byteSize;
        }
        else if (isFloat)
        {
            // FLOAT: 3バイト (元実装: offset++ + offset+=2)
            LocalSize += 3;
        }
        else
        {
            // BYTE/WORD: 常に2バイト
            LocalSize += 2;
        }

        if (LocalSize > 240)
        {
            // "Local area overflow" - 動的局所域は240バイト
        }

        // オフセット: $70 - LocalSize
        return 0x70 - LocalSize;
    }
}
