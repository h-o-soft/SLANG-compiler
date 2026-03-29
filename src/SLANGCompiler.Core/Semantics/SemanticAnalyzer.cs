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
    private bool _inStaticDecl;
    private string? _currentFuncName;
    private FuncInfo? _currentFunc;

    public SymbolTable Symbols => _symbols;

    public SemanticAnalyzer(DiagnosticBag diagnostics, bool caseSensitive = false)
    {
        _diagnostics = diagnostics;
        _symbols = new SymbolTable(caseSensitive);
        _constEval = new ConstEvaluator(_symbols);
        RegisterBuiltins();
    }

    public void Analyze(CompilationUnit unit)
    {
        unit.Accept(this);
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
            var sym = _symbols.Define(name, SymbolKind.Function, funcType);
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
        var type = DataSizeToType(node.Size);
        var sym = _symbols.Define(node.Name, SymbolKind.Variable, type);

        if (_currentFunc != null && _symbols.IsGlobalScope == false && !_inStaticDecl)
        {
            // ローカル変数(動的): BYTE/WORDとも2バイト、FLOATは3バイト
            sym.IsGlobal = false;
            sym.Offset = _currentFunc.AllocLocal(type.ByteSize, isFloat: node.Size == DataSize.Float);
            sym.AsmLabel = null; // IYオフセットでアクセス
        }
        else
        {
            // グローバル変数 or 関数内静的宣言: __WORK__に配置
            sym.IsGlobal = true;
            sym.AsmLabel = (_inStaticDecl && _currentFuncName != null)
                ? $"__{_currentFuncName}_{node.Name}"
                : $"__{node.Name}";
            if (node.Address != null)
            {
                // アドレス固定
                // TODO: 定数式を評価してAddress設定
            }
        }

        if (node.InitialValue != null)
            node.InitialValue.Accept(this);

        return null;
    }

    public object? VisitArrayDecl(ArrayDecl node)
    {
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
                var val = _constEval.Evaluate(dim);
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

        var sym = _symbols.Define(node.Name, SymbolKind.Variable, type);

        if (_currentFunc != null && !_symbols.IsGlobalScope && !_inStaticDecl)
        {
            sym.IsGlobal = false;
            sym.Offset = _currentFunc.AllocLocal(type.ByteSize, isArray: true);
            sym.AsmLabel = null;
        }
        else
        {
            sym.IsGlobal = true;
            sym.AsmLabel = (_inStaticDecl && _currentFuncName != null)
                ? $"__{_currentFuncName}_{node.Name}"
                : $"__{node.Name}";
        }

        return null;
    }

    public object? VisitConstDecl(ConstDecl node)
    {
        var sym = _symbols.Define(node.Name, SymbolKind.Constant, SlangType.Word);
        // 定数式を評価（他のCONST参照も解決可能）
        var val = _constEval.Evaluate(node.Value);
        if (val.HasValue)
            sym.ConstValue = val.Value;
        return null;
    }

    public object? VisitMachineDecl(MachineDecl node)
    {
        var paramTypes = new List<SlangType>();
        if (node.ParamCount.HasValue)
            paramTypes = Enumerable.Repeat(SlangType.Word, node.ParamCount.Value).ToList();
        var funcType = new FunctionType(SlangType.Word, paramTypes);
        var sym = _symbols.Define(node.Name, SymbolKind.MachineFunction, funcType);
        sym.IsGlobal = true;
        sym.AsmLabel = $"_{node.Name}";
        return null;
    }

    public object? VisitFuncDef(FuncDef node)
    {
        // 関数自体をグローバルに登録
        var paramTypes = node.Parameters.Select(p => DataSizeToType(p.Size)).ToList();
        var funcType = new FunctionType(SlangType.Word, paramTypes);
        var funcSym = _symbols.Define(node.Name, SymbolKind.Function, funcType);
        funcSym.IsGlobal = true;
        funcSym.AsmLabel = node.Name;

        // 関数スコープ開始
        _symbols.PushScope(node.Name);
        var prevFunc = _currentFunc;
        _currentFunc = new FuncInfo(node.Name);

        // 仮引数を動的変数として登録
        // 仕様: 引数は(IY+$70)から順に配置
        int argOffset = 0x70;
        foreach (var param in node.Parameters)
        {
            var pType = DataSizeToType(param.Size);
            var pSym = _symbols.Define(param.Name, SymbolKind.Parameter, pType);
            pSym.IsGlobal = false;
            pSym.Offset = argOffset;
            argOffset += 2; // 引数は常にWORD
        }

        // 静的宣言（__WORK__に配置、AsmLabelに関数名プレフィックス）
        _currentFuncName = node.Name;
        _inStaticDecl = true;
        foreach (var decl in node.StaticDeclarations)
            decl.Accept(this);
        _inStaticDecl = false;

        // 局所宣言（IYフレームに配置）
        foreach (var decl in node.LocalDeclarations)
            decl.Accept(this);

        // 本体
        node.Body.Accept(this);

        // 関数情報を保存
        funcSym.ConstValue = _currentFunc; // FuncInfoを紐付け

        _currentFunc = prevFunc;
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
            b.Body.Accept(this);
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
    public object? VisitAssignExpr(AssignExpr node) { node.Target.Accept(this); node.Value.Accept(this); return null; }
    public object? VisitCompoundAssignExpr(CompoundAssignExpr node) { node.Target.Accept(this); node.Value.Accept(this); return null; }
    public object? VisitIncrementExpr(IncrementExpr node) { node.Operand.Accept(this); return null; }
    public object? VisitCallExpr(CallExpr node)
    {
        node.Function.Accept(this);
        foreach (var arg in node.Arguments) arg.Accept(this);
        return null;
    }
    public object? VisitArrayAccessExpr(ArrayAccessExpr node)
    {
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
    public object? VisitAddressOfExpr(AddressOfExpr node) { node.Operand.Accept(this); return null; }
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
        foreach (var def in node.Definitions) def.Accept(this);
        return null;
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
}

/// <summary>
/// 関数のローカル変数情報
/// </summary>
public class FuncInfo
{
    public string Name { get; }
    public int LocalSize { get; private set; }

    public FuncInfo(string name) { Name = name; }

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
