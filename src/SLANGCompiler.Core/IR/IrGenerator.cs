using SLANGCompiler.Parser.Ast;
using SLANGCompiler.Semantics;

namespace SLANGCompiler.IR;

/// <summary>
/// AST → IR変換
/// </summary>
public class IrGenerator : IAstVisitor<IrOperand>
{
    private readonly DiagnosticBag _diagnostics;
    private readonly SymbolTable? _globalSymbols;
    private readonly IrModule _module = new();
    private IrFunction? _currentFunction;
    private int _labelCount;
    private bool _inStaticDecl;
    private string? _currentFuncName;
    private bool _emitToGlobalData;

    // 関数内ローカルシンボル（IrGenerator自身が管理）
    private Dictionary<string, LocalVarInfo>? _localVars;
    // 関数内静的変数のAsmLabelマップ（ソース名→__FUNC_VAR形式）
    private Dictionary<string, string>? _staticVarLabels;

    public IrGenerator(DiagnosticBag diagnostics, SymbolTable? symbols = null)
    {
        _diagnostics = diagnostics;
        _globalSymbols = symbols;
    }

    /// <summary>シンボル名からASMラベルを解決。関数内静的変数→グローバルシンボル→デフォルトの順。</summary>
    private string ResolveAsmLabel(string name)
    {
        // 関数内静的変数（__FUNC_VAR形式）
        if (_staticVarLabels != null && _staticVarLabels.TryGetValue(name, out var staticLabel))
            return staticLabel;
        // グローバルシンボルテーブル
        var sym = _globalSymbols?.Resolve(name);
        return sym?.AsmLabel ?? LabelUtils.UserVarLabel(name);
    }

    private record LocalVarInfo(int Offset, int ByteSize, bool IsArray = false, bool IsByte = false, List<int>? Dims = null);
    private int _localOffset;

    // 各tempのデータサイズを追跡（FLOAT判定用）
    private readonly Dictionary<int, int> _tempDataSize = new();

    public IrModule Generate(CompilationUnit unit)
    {
        unit.Accept(this);
        return _module;
    }

    private int AllocTemp() => _currentFunction?.AllocTemp() ?? 0;
    private string NewLabel() => $"_L{_labelCount++}";

    private void Emit(IrOp op, IrOperand dest = default, IrOperand src1 = default, IrOperand src2 = default, int dataSize = 2)
    {
        var inst = new IrInstruction(op, dest, src1, src2) { DataSize = dataSize };
        if (_emitToGlobalData || _currentFunction == null)
            _module.GlobalData.Add(inst);
        else
            _currentFunction.Instructions.Add(inst);
    }

    // ==== Top-level ====

    public IrOperand VisitCompilationUnit(CompilationUnit node)
    {
        foreach (var def in node.Definitions)
            def.Accept(this);
        return IrOperand.None;
    }

    public IrOperand VisitBlock(Block node)
    {
        foreach (var stmt in node.Statements)
            stmt.Accept(this);
        return IrOperand.None;
    }

    // ==== Declarations ====

    public IrOperand VisitVarDecl(VarDecl node)
    {
        int ds = node.Size == DataSize.Byte ? 1 : (node.Size == DataSize.Float ? 3 : 2);

        // グローバルスコープ or 関数内静的宣言 → __WORK__に配置
        if (_currentFunction == null || _inStaticDecl)
        {
            int? fixedAddr = null;
            if (node.Address is IntegerLiteral addrLit)
                fixedAddr = (int)addrLit.Value;

            // ラベル: 関数内静的は __{FuncName}_{VarName}、トップレベルは __{VarName}
            var label = (_inStaticDecl && _currentFuncName != null)
                ? LabelUtils.StaticVarLabel(_currentFuncName!, node.Name)
                : LabelUtils.UserVarLabel(node.Name);

            // 関数内静的変数のラベルを追跡（ResolveAsmLabel用）
            if (_inStaticDecl && _currentFuncName != null)
                _staticVarLabels![node.Name] = label;

            _module.GlobalVars.Add(new GlobalVarInfo
            {
                Name = node.Name,
                AsmLabel = label,
                ByteSize = ds,
                FixedAddress = fixedAddr,
            });

            // 初期値付き: _emitToGlobalDataでGlobalDataに積む（起動時1回だけ初期化）
            if (node.InitialValue != null)
            {
                _emitToGlobalData = true;
                try
                {
                    var val = node.InitialValue.Accept(this);
                    Emit(IrOp.StoreVar, IrOperand.Sym(label), val, dataSize: ds);
                }
                finally
                {
                    _emitToGlobalData = false;
                }
            }
        }
        else
        {
            // ローカル変数: IYオフセット割り当て
            AllocLocalVar(node.Name, ds);

            if (node.InitialValue != null)
            {
                var val = node.InitialValue.Accept(this);
                var info = _localVars![node.Name];
                Emit(IrOp.StoreLocal, IrOperand.Imm(info.Offset), val, dataSize: ds);
            }
        }
        return IrOperand.None;
    }

    public IrOperand VisitArrayDecl(ArrayDecl node)
    {
        int elemSize = node.Size == DataSize.Byte ? 1 : 2;
        bool isByte = node.Size == DataSize.Byte;

        // 次元情報を計算
        var dims = new List<int>();
        int totalSize = elemSize;
        foreach (var dim in node.Dimensions)
        {
            int dimSize;
            if (dim is IntegerLiteral lit)
                dimSize = (int)lit.Value + 1; // 仕様: +1個分
            else if (dim == null)
                dimSize = 0; // 間接配列
            else
            {
                var constEval = _globalSymbols != null ? new ConstEvaluator(_globalSymbols) : null;
                var val = constEval?.Evaluate(dim);
                dimSize = val.HasValue ? val.Value + 1 : 1;
            }
            dims.Add(dimSize);
            if (dimSize > 0) totalSize *= dimSize;
        }

        if (_currentFunction == null || _inStaticDecl)
        {
            // グローバル or 静的配列（totalSizeは上で計算済み）
            int? fixedAddr = null;
            if (node.Address is IntegerLiteral addrLit)
                fixedAddr = (int)addrLit.Value;

            // 初期値付き配列: CODEリストをInitialItemsに変換
            List<InitItem>? initItems = null;
            if (node.InitialCode != null)
            {
                initItems = new List<InitItem>();
                foreach (var expr in node.InitialCode)
                {
                    var initExpr = expr;
                    int itemSize = elemSize;
                    if (initExpr is CastExpr cast)
                    {
                        initExpr = cast.Operand;
                        itemSize = cast.TargetSize == DataSize.Byte ? 1 : cast.TargetSize == DataSize.Float ? 3 : 2;
                    }

                    if (initExpr is IntegerLiteral ilit)
                    {
                        if (itemSize == 1)
                            initItems.Add(InitItem.Byte((byte)(ilit.Value & 0xFF)));
                        else if (itemSize == 3)
                        {
                            initItems.Add(InitItem.Byte((byte)(ilit.Value & 0xFF)));
                            initItems.Add(InitItem.Byte((byte)((ilit.Value >> 8) & 0xFF)));
                            initItems.Add(InitItem.Byte((byte)((ilit.Value >> 16) & 0xFF)));
                        }
                        else
                        {
                            initItems.Add(InitItem.Byte((byte)(ilit.Value & 0xFF)));
                            initItems.Add(InitItem.Byte((byte)((ilit.Value >> 8) & 0xFF)));
                        }
                    }
                    else if (initExpr is StringLiteral slit)
                    {
                        foreach (var ch in slit.Value)
                            initItems.Add(InitItem.Byte((byte)ch));
                    }
                    else
                    {
                        // 非定数式: ExprToAsmStringでアセンブラ式に変換
                        var asmResult = LabelUtils.ExprToAsmString(initExpr, _globalSymbols, _diagnostics);
                        if (asmResult.HasValue && itemSize == 2)
                        {
                            initItems.Add(InitItem.Word(asmResult.Value.Expr));
                            foreach (var dep in asmResult.Value.Deps)
                                _module.AddressSymbolDeps.Add(dep);
                        }
                        else if (itemSize == 1)
                        {
                            _diagnostics?.Error("Non-constant BYTE expression in CODE block not supported",
                                initExpr.Span);
                        }
                    }
                }
                // totalSizeに満たない場合は0で埋める
                int currentSize = initItems.Sum(i => i.ByteSize);
                while (currentSize < totalSize)
                {
                    initItems.Add(InitItem.Byte(0));
                    currentSize++;
                }
            }

            var label = (_inStaticDecl && _currentFuncName != null)
                ? LabelUtils.StaticVarLabel(_currentFuncName!, node.Name)
                : LabelUtils.UserVarLabel(node.Name);

            if (_inStaticDecl && _currentFuncName != null)
                _staticVarLabels![node.Name] = label;

            _module.GlobalVars.Add(new GlobalVarInfo
            {
                Name = node.Name,
                AsmLabel = label,
                ByteSize = totalSize,
                FixedAddress = fixedAddr,
                IsArray = true,
                InitialItems = initItems,
                StorageKind = initItems != null ? VarStorageKind.InitArray : VarStorageKind.Bss,
            });
        }
        else
        {
            // ローカル配列: IYオフセットに動的確保
            _localOffset += totalSize;
            int offset = 0x70 - _localOffset;
            _localVars![node.Name] = new LocalVarInfo(offset, totalSize, IsArray: true, IsByte: isByte, Dims: dims);
        }
        return IrOperand.None;
    }

    public IrOperand VisitConstDecl(ConstDecl node)
    {
        // CODEブロック型CONST: CONST X = [...] → ラベル+DBデータとしてコード領域に配置
        if (node.Value is CodeExpr codeExpr)
        {
            var label = (_inStaticDecl && _currentFuncName != null)
                ? LabelUtils.StaticVarLabel(_currentFuncName!, node.Name)
                : LabelUtils.UserVarLabel(node.Name);

            if (_inStaticDecl && _currentFuncName != null)
                _staticVarLabels![node.Name] = label;

            var initItems = BuildCodeBlockItems(codeExpr);

            _module.GlobalVars.Add(new GlobalVarInfo
            {
                Name = node.Name,
                AsmLabel = label,
                ByteSize = initItems.Sum(i => i.ByteSize),
                InitialItems = initItems,
                StorageKind = VarStorageKind.CodeConst,
            });
        }
        else
        {
            Emit(IrOp.Comment, IrOperand.Asm($"CONST {node.Name}"));
        }
        return IrOperand.None;
    }

    public IrOperand VisitMachineDecl(MachineDecl node)
    {
        Emit(IrOp.Comment, IrOperand.Asm($"MACHINE {node.Name}"));

        // 静的宣言を関数スコープで処理
        if (node.StaticDeclarations.Count > 0)
        {
            var prevStaticLabels = _staticVarLabels;
            _staticVarLabels = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            _currentFuncName = LabelUtils.SanitizeLabel(node.Name);
            _inStaticDecl = true;
            foreach (var d in node.StaticDeclarations) d.Accept(this);
            _inStaticDecl = false;
            _currentFuncName = null;
            _staticVarLabels = prevStaticLabels;
        }

        if (node.CodeBody != null)
        {
            var sym = _globalSymbols?.Resolve(node.Name);
            var label = sym?.AsmLabel ?? $"_{LabelUtils.SanitizeLabel(node.Name)}";
            var initItems = BuildCodeBlockItems(node.CodeBody);
            // 旧コンパイラ互換: funcend()相当のRETを末尾に追加
            initItems.Add(InitItem.Byte(0xC9)); // RET

            _module.GlobalVars.Add(new GlobalVarInfo
            {
                Name = node.Name,
                AsmLabel = label,
                ByteSize = initItems.Sum(i => i.ByteSize),
                InitialItems = initItems,
                StorageKind = VarStorageKind.CodeConst,
            });
        }
        return IrOperand.None;
    }

    private List<InitItem> BuildCodeBlockItems(CodeExpr codeExpr)
    {
        var initItems = new List<InitItem>();
        foreach (var expr in codeExpr.Values)
        {
            var initExpr = expr;
            int itemSize = 1; // CODEブロックのデフォルトはBYTE
            if (initExpr is CastExpr cast)
            {
                initExpr = cast.Operand;
                itemSize = cast.TargetSize == DataSize.Byte ? 1 : cast.TargetSize == DataSize.Float ? 3 : 2;
            }
            if (initExpr is IntegerLiteral ilit)
            {
                if (itemSize == 1)
                    initItems.Add(InitItem.Byte((byte)(ilit.Value & 0xFF)));
                else
                {
                    initItems.Add(InitItem.Byte((byte)(ilit.Value & 0xFF)));
                    initItems.Add(InitItem.Byte((byte)((ilit.Value >> 8) & 0xFF)));
                }
            }
            else if (initExpr is StringLiteral slit)
            {
                foreach (var ch in slit.Value)
                    initItems.Add(InitItem.Byte((byte)ch));
            }
            else
            {
                // 非定数式: アセンブラ式に変換
                var asmResult = LabelUtils.ExprToAsmString(initExpr, _globalSymbols, _diagnostics);
                if (asmResult.HasValue && itemSize == 2)
                {
                    initItems.Add(InitItem.Word(asmResult.Value.Expr));
                    foreach (var dep in asmResult.Value.Deps)
                        _module.AddressSymbolDeps.Add(dep);
                }
                else if (itemSize == 1)
                {
                    _diagnostics?.Error("Non-constant BYTE expression in CODE block not supported",
                        initExpr.Span);
                }
            }
        }
        return initItems;
    }

    public IrOperand VisitParamDecl(ParamDecl node) => IrOperand.None;

    // ==== Function ====

    public IrOperand VisitFuncDef(FuncDef node)
    {
        _currentFunction = new IrFunction { Name = LabelUtils.SanitizeLabel(node.Name) };

        // ローカルシンボルテーブルを構築
        var prevLocalVars = _localVars;
        var prevOffset = _localOffset;
        var prevStaticLabels = _staticVarLabels;
        _localVars = new Dictionary<string, LocalVarInfo>(StringComparer.OrdinalIgnoreCase);
        _staticVarLabels = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        _localOffset = 0;

        // 仮引数を仮登録（オフセットはLocalDeclarations走査後に確定）
        var paramNames = new List<string>();
        foreach (var p in node.Parameters)
        {
            _localVars[p.Name] = new LocalVarInfo(0, 2); // 仮オフセット
            paramNames.Add(p.Name);
        }

        Emit(IrOp.FuncBegin, IrOperand.Sym(LabelUtils.SanitizeLabel(node.Name)));

        // Static declarations → グローバルメモリ(__WORK__)、Local declarations → 動的(IY)
        _currentFuncName = LabelUtils.SanitizeLabel(node.Name);
        _inStaticDecl = true;
        foreach (var d in node.StaticDeclarations) d.Accept(this);
        _inStaticDecl = false;
        foreach (var d in node.LocalDeclarations) d.Accept(this);

        // ローカル変数確定後、引数のオフセットを計算
        // ADD IY, (localOffset + paramCount*2) でIYがずれるため:
        // 引数は 0x70 - localOffset - paramCount*2 から配置
        // ローカル変数は 0x70 - localOffset から 0x70 - 1 まで
        {
            int totalFrameSize = _localOffset + paramNames.Count * 2;
            int argOff = 0x70 - totalFrameSize;
            foreach (var pn in paramNames)
            {
                _localVars[pn] = new LocalVarInfo(argOff, 2);
                argOff += 2;
            }
            // localOffsetに引数分も加算（ADD IY,BCのフレームサイズ）
            _localOffset = totalFrameSize;
        }

        // Body
        node.Body.Accept(this);

        // Return value from END(expr)
        if (node.ReturnValue != null)
        {
            var retVal = node.ReturnValue.Accept(this);
            Emit(IrOp.Return, retVal);
        }

        Emit(IrOp.FuncEnd);
        _currentFunction.LocalSize = _localOffset;
        _module.Functions.Add(_currentFunction);
        _currentFunction = null;
        _localVars = prevLocalVars;
        _staticVarLabels = prevStaticLabels;
        _localOffset = prevOffset;
        return IrOperand.None;
    }

    /// <summary>ローカル変数のオフセットを割り当て</summary>
    private int AllocLocalVar(string name, int byteSize)
    {
        // BYTE/WORDとも2バイト確保（仕様準拠）
        int allocSize = byteSize <= 2 ? 2 : byteSize;
        _localOffset += allocSize;
        int offset = 0x70 - _localOffset;
        _localVars![name] = new LocalVarInfo(offset, allocSize);
        return offset;
    }

    // ==== Statements ====

    public IrOperand VisitExpressionStmt(ExpressionStmt node)
    {
        node.Expr.Accept(this);
        return IrOperand.None;
    }

    public IrOperand VisitIfStmt(IfStmt node)
    {
        var endLabel = NewLabel();
        var constEval = _globalSymbols != null ? new ConstEvaluator(_globalSymbols) : null;

        for (int i = 0; i < node.Branches.Count; i++)
        {
            var (cond, body) = node.Branches[i];
            var nextLabel = (i < node.Branches.Count - 1 || node.ElseBody != null) ? NewLabel() : endLabel;

            // 定数条件の最適化
            var constCond = constEval?.Evaluate(cond);
            if (constCond.HasValue && constCond.Value != 0)
            {
                // 常にTRUE: 条件チェック不要、bodyを出力して残りのブランチ/elseは省略
                body.Accept(this);
                Emit(IrOp.Label, IrOperand.Lbl(endLabel));
                return IrOperand.None;
            }
            else if (constCond.HasValue && constCond.Value == 0)
            {
                // 常にFALSE: このブランチを完全にスキップ
                if (nextLabel != endLabel)
                    Emit(IrOp.Label, IrOperand.Lbl(nextLabel));
                continue;
            }

            var condVal = cond.Accept(this);
            Emit(IrOp.JumpIfZero, IrOperand.Lbl(nextLabel), condVal);

            body.Accept(this);
            if (nextLabel != endLabel)
                Emit(IrOp.Jump, IrOperand.Lbl(endLabel));

            if (nextLabel != endLabel)
                Emit(IrOp.Label, IrOperand.Lbl(nextLabel));
        }

        if (node.ElseBody != null)
        {
            node.ElseBody.Accept(this);
        }

        Emit(IrOp.Label, IrOperand.Lbl(endLabel));
        return IrOperand.None;
    }

    public IrOperand VisitWhileStmt(WhileStmt node)
    {
        var startLabel = NewLabel();
        var endLabel = NewLabel();

        PushLoop(startLabel, endLabel);

        // 定数TRUE条件: 条件チェック省略（無限ループ = LOOP相当）
        var constEval = _globalSymbols != null ? new ConstEvaluator(_globalSymbols) : null;
        var constCond = constEval?.Evaluate(node.Condition);

        Emit(IrOp.Label, IrOperand.Lbl(startLabel));

        if (constCond.HasValue && constCond.Value != 0)
        {
            // WHILE(TRUE) or WHILE(非ゼロ定数): 条件チェック不要
        }
        else
        {
            var condVal = node.Condition.Accept(this);
            Emit(IrOp.JumpIfZero, IrOperand.Lbl(endLabel), condVal);
        }

        node.Body.Accept(this);
        Emit(IrOp.Jump, IrOperand.Lbl(startLabel));

        Emit(IrOp.Label, IrOperand.Lbl(endLabel));
        PopLoop();
        return IrOperand.None;
    }

    public IrOperand VisitRepeatStmt(RepeatStmt node)
    {
        var startLabel = NewLabel();
        var endLabel = NewLabel();

        PushLoop(startLabel, endLabel);

        Emit(IrOp.Label, IrOperand.Lbl(startLabel));
        node.Body.Accept(this);

        var condVal = node.Condition.Accept(this);
        Emit(IrOp.JumpIfZero, IrOperand.Lbl(startLabel), condVal);

        Emit(IrOp.Label, IrOperand.Lbl(endLabel));
        PopLoop();
        return IrOperand.None;
    }

    public IrOperand VisitLoopStmt(LoopStmt node)
    {
        var startLabel = NewLabel();
        var endLabel = NewLabel();

        PushLoop(startLabel, endLabel);

        Emit(IrOp.Label, IrOperand.Lbl(startLabel));
        node.Body.Accept(this);
        Emit(IrOp.Jump, IrOperand.Lbl(startLabel));

        Emit(IrOp.Label, IrOperand.Lbl(endLabel));
        PopLoop();
        return IrOperand.None;
    }

    public IrOperand VisitForStmt(ForStmt node)
    {
        var startLabel = NewLabel();
        var contLabel = NewLabel();
        var endLabel = NewLabel();

        // Initialize: var = from
        var fromVal = node.From.Accept(this);
        Emit(IrOp.StoreVar, IrOperand.Sym(ResolveAsmLabel(node.Variable)), fromVal);

        PushLoop(contLabel, endLabel);

        Emit(IrOp.Label, IrOperand.Lbl(startLabel));

        // Body
        node.Body.Accept(this);

        // Continue label (increment/decrement)
        Emit(IrOp.Label, IrOperand.Lbl(contLabel));

        // Increment/decrement
        var curVal = IrOperand.Temp(AllocTemp());
        Emit(IrOp.LoadVar, curVal, IrOperand.Sym(ResolveAsmLabel(node.Variable)));
        var one = IrOperand.Temp(AllocTemp());
        Emit(IrOp.LoadConst, one, IrOperand.Imm(1));
        var newVal = IrOperand.Temp(AllocTemp());
        Emit(node.IsDownTo ? IrOp.Sub : IrOp.Add, newVal, curVal, one);
        Emit(IrOp.StoreVar, IrOperand.Sym(ResolveAsmLabel(node.Variable)), newVal);

        // Compare with limit（符号付き比較: 0を跨ぐオーバーフローで終了）
        var limit = node.To.Accept(this);
        var cmp = IrOperand.Temp(AllocTemp());
        Emit(node.IsDownTo ? IrOp.CmpSGe : IrOp.CmpSLe, cmp, newVal, limit);
        Emit(IrOp.JumpIfNonZero, IrOperand.Lbl(startLabel), cmp);

        Emit(IrOp.Label, IrOperand.Lbl(endLabel));
        PopLoop();
        return IrOperand.None;
    }

    public IrOperand VisitCaseStmt(CaseStmt node)
    {
        var endLabel = NewLabel();
        var exprVal = node.Expr.Accept(this);

        PushLoop(endLabel, endLabel); // EXIT in CASE goes to end

        foreach (var branch in node.Branches)
        {
            if (branch.Value == null)
            {
                // OTHERS
                branch.Body.Accept(this);
                Emit(IrOp.Jump, IrOperand.Lbl(endLabel));
            }
            else
            {
                var nextLabel = NewLabel();
                var branchVal = branch.Value.Accept(this);

                if (branch.RangeEnd != null)
                {
                    // Range: value TO rangeEnd
                    var rangeEnd = branch.RangeEnd.Accept(this);
                    var cmpLo = IrOperand.Temp(AllocTemp());
                    var cmpHi = IrOperand.Temp(AllocTemp());
                    Emit(IrOp.CmpGe, cmpLo, exprVal, branchVal);
                    Emit(IrOp.CmpLe, cmpHi, exprVal, rangeEnd);
                    var both = IrOperand.Temp(AllocTemp());
                    Emit(IrOp.LogAnd, both, cmpLo, cmpHi);
                    Emit(IrOp.JumpIfZero, IrOperand.Lbl(nextLabel), both);
                }
                else
                {
                    var cmp = IrOperand.Temp(AllocTemp());
                    Emit(IrOp.CmpEq, cmp, exprVal, branchVal);
                    Emit(IrOp.JumpIfZero, IrOperand.Lbl(nextLabel), cmp);
                }

                branch.Body.Accept(this);
                Emit(IrOp.Jump, IrOperand.Lbl(endLabel));
                Emit(IrOp.Label, IrOperand.Lbl(nextLabel));
            }
        }

        Emit(IrOp.Label, IrOperand.Lbl(endLabel));
        PopLoop();
        return IrOperand.None;
    }

    public IrOperand VisitExitStmt(ExitStmt node)
    {
        if (node.TargetLabel != null)
        {
            Emit(IrOp.Jump, IrOperand.Lbl(node.TargetLabel));
        }
        else
        {
            var breakLabel = GetBreakLabel();
            if (breakLabel != null)
                Emit(IrOp.Jump, IrOperand.Lbl(breakLabel));
            else
                _diagnostics.Error("EXIT outside loop", node.Span);
        }
        return IrOperand.None;
    }

    public IrOperand VisitContinueStmt(ContinueStmt node)
    {
        var contLabel = GetContinueLabel();
        if (contLabel != null)
            Emit(IrOp.Jump, IrOperand.Lbl(contLabel));
        else
            _diagnostics.Error("CONTINUE outside loop", node.Span);
        return IrOperand.None;
    }

    public IrOperand VisitReturnStmt(ReturnStmt node)
    {
        if (node.Value != null)
        {
            var val = node.Value.Accept(this);
            Emit(IrOp.Return, val);
        }
        else
        {
            Emit(IrOp.Return);
        }
        return IrOperand.None;
    }

    public IrOperand VisitGotoStmt(GotoStmt node)
    {
        Emit(IrOp.Jump, IrOperand.Lbl(LabelUtils.UserLabel(node.Label)));
        return IrOperand.None;
    }

    public IrOperand VisitLabelStmt(LabelStmt node)
    {
        Emit(IrOp.Label, IrOperand.Lbl(LabelUtils.UserLabel(node.Label)));
        return IrOperand.None;
    }

    public IrOperand VisitPrintStmt(PrintStmt node)
    {
        // 仕様準拠のランタイム関数名:
        //   PSTR: 文字列出力(HL=文字列アドレス)
        //   PRT:  数値出力(HL=値、10進左詰め)
        //   PCRONE: 改行出力
        //   PHEX2/PHEX4: 16進出力
        //   P10toN: n桁右詰め出力(HL=値, DE=桁数)
        //   PSIGN/PSPC/PMSX/PMSG 等

        foreach (var arg in node.Arguments)
        {
            if (arg is StringFuncExpr sf)
            {
                switch (sf.FuncName.ToUpperInvariant())
                {
                    case "/":
                        Emit(IrOp.Call, IrOperand.None, IrOperand.Sym("PCRONE"));
                        break;
                    case "HEX2$":
                        if (sf.Arguments.Count > 0) { var v = sf.Arguments[0].Accept(this); }
                        Emit(IrOp.Call, IrOperand.None, IrOperand.Sym("PHEX2"));
                        break;
                    case "HEX4$":
                        if (sf.Arguments.Count > 0) { var v = sf.Arguments[0].Accept(this); }
                        Emit(IrOp.Call, IrOperand.None, IrOperand.Sym("PHEX4"));
                        break;
                    case "FORM$":
                        // FORM$(value, n): HL=value, DE=n桁
                        if (sf.Arguments.Count >= 2)
                        {
                            var v = sf.Arguments[0].Accept(this);
                            Emit(IrOp.PushArg, v);
                            var n = sf.Arguments[1].Accept(this);
                            // DE=n, HL=value
                        }
                        Emit(IrOp.Call, IrOperand.None, IrOperand.Sym("P10toN"));
                        break;
                    case "DECI$":
                        if (sf.Arguments.Count > 0) { var v = sf.Arguments[0].Accept(this); }
                        Emit(IrOp.Call, IrOperand.None, IrOperand.Sym("P10to5"));
                        break;
                    case "%" or "PN$":
                        if (sf.Arguments.Count > 0) { var v = sf.Arguments[0].Accept(this); }
                        Emit(IrOp.Call, IrOperand.None, IrOperand.Sym("PSIGN"));
                        break;
                    case "MSG$":
                        if (sf.Arguments.Count > 0) { var v = sf.Arguments[0].Accept(this); }
                        Emit(IrOp.Call, IrOperand.None, IrOperand.Sym("PMSG"));
                        break;
                    case "!" or "MSX$":
                        if (sf.Arguments.Count > 0) { var v = sf.Arguments[0].Accept(this); }
                        Emit(IrOp.Call, IrOperand.None, IrOperand.Sym("PMSX"));
                        break;
                    case "STR$":
                        // STR$(char, n)
                        if (sf.Arguments.Count >= 2)
                        {
                            sf.Arguments[0].Accept(this);
                            Emit(IrOp.PushArg, IrOperand.None);
                            sf.Arguments[1].Accept(this);
                        }
                        Emit(IrOp.Call, IrOperand.None, IrOperand.Sym("PSTR2"));
                        break;
                    case "CHR$":
                        if (sf.Arguments.Count > 0) { var v = sf.Arguments[0].Accept(this); }
                        Emit(IrOp.Call, IrOperand.None, IrOperand.Sym("PCHR"));
                        break;
                    case "SPC$":
                        if (sf.Arguments.Count > 0) { var v = sf.Arguments[0].Accept(this); }
                        Emit(IrOp.Call, IrOperand.None, IrOperand.Sym("PSPC"));
                        break;
                    case "CR$":
                        if (sf.Arguments.Count > 0) { var v = sf.Arguments[0].Accept(this); }
                        Emit(IrOp.Call, IrOperand.None, IrOperand.Sym("PCR"));
                        break;
                    case "TAB$":
                        if (sf.Arguments.Count > 0) { var v = sf.Arguments[0].Accept(this); }
                        Emit(IrOp.Call, IrOperand.None, IrOperand.Sym("PTAB"));
                        break;
                    default:
                        foreach (var a in sf.Arguments) a.Accept(this);
                        Emit(IrOp.Call, IrOperand.None, IrOperand.Sym($"PRINT_{sf.FuncName}"));
                        break;
                }
            }
            else
            {
                var val = arg.Accept(this);
                if (arg is StringLiteral)
                {
                    // PMSX: HL=null終端文字列アドレスから出力
                    Emit(IrOp.Call, IrOperand.None, IrOperand.Sym("PMSX"));
                }
                else
                {
                    // P10: HL=数値を10進文字列に変換して出力
                    Emit(IrOp.Call, IrOperand.None, IrOperand.Sym("P10"));
                }
            }
        }
        return IrOperand.None;
    }

    // ==== Expressions ====

    public IrOperand VisitIntegerLiteral(IntegerLiteral node)
    {
        var t = IrOperand.Temp(AllocTemp());
        Emit(IrOp.LoadConst, t, IrOperand.Imm(node.Value));
        return t;
    }

    private int _floatConstCount;

    public IrOperand VisitFloatLiteral(FloatLiteral node)
    {
        // FLOAT定数をconstant poolに格納し、LoadVar(dataSize:3)で読む
        var f24 = LabelUtils.ConvertToF24(node.Value);
        var label = $"_FC{_floatConstCount++}";
        _module.GlobalVars.Add(new GlobalVarInfo
        {
            Name = label, AsmLabel = label, ByteSize = 3,
            InitialItems = new List<InitItem> { InitItem.Byte(f24[0]), InitItem.Byte(f24[1]), InitItem.Byte(f24[2]) },
            StorageKind = VarStorageKind.CodeConst,
        });
        var t = IrOperand.Temp(AllocTemp());
        Emit(IrOp.LoadVar, t, IrOperand.Sym(label), dataSize: 3);
        _tempDataSize[t.TempIndex] = 3;
        return t;
    }

    public IrOperand VisitStringLiteral(StringLiteral node)
    {
        // 文字列テーブルに登録し、ラベルアドレスをロード
        var label = $"_S{_module.StringTable.Count}";
        _module.StringTable[label] = node.Value;

        var t = IrOperand.Temp(AllocTemp());
        Emit(IrOp.LoadAddr, t, IrOperand.Lbl(label));
        return t;
    }

    public IrOperand VisitIdentifier(IdentifierExpr node)
    {
        var t = IrOperand.Temp(AllocTemp());

        // 1. ローカル変数テーブルをまず検索
        if (_localVars != null && _localVars.TryGetValue(node.Name, out var localInfo))
        {
            if (localInfo.IsArray)
            {
                // ローカル配列: アドレスをロード（IY+offsetの実効アドレス）
                Emit(IrOp.InlineAsm, t, IrOperand.Asm($"\tPUSH\tIY\n\tPOP\tHL\n\tLD\tDE,${localInfo.Offset:X4}\n\tADD\tHL,DE"));
                return t;
            }
            Emit(IrOp.LoadLocal, t, IrOperand.Imm(localInfo.Offset), dataSize: localInfo.ByteSize);
            _tempDataSize[t.TempIndex] = localInfo.ByteSize;
            return t;
        }

        // 2. グローバルシンボルテーブルを検索
        var sym = _globalSymbols?.Resolve(node.Name);
        if (sym != null && sym.Kind == SymbolKind.Constant && sym.ConstValue is int constVal)
        {
            Emit(IrOp.LoadConst, t, IrOperand.Imm(constVal));
        }
        else if (sym != null && sym.Kind == SymbolKind.Constant && sym.ConstAst != null)
        {
            // アセンブラ式定数: CONST X=SOROBAN, CONST X=LABEL+$14
            // 初回解決時にキャッシュ（失敗時も再試行しない）
            if (!sym.ConstAsmResolved)
            {
                sym.ConstAsmResolved = true;
                var result = LabelUtils.ExprToAsmString(sym.ConstAst, _globalSymbols, _diagnostics);
                if (result.HasValue)
                {
                    sym.ConstAsmExpr = result.Value.Expr;
                    sym.ConstAsmDeps = result.Value.Deps;
                }
            }
            if (sym.ConstAsmExpr != null)
            {
                Emit(IrOp.LoadAddr, t, IrOperand.Sym(sym.ConstAsmExpr));
                if (sym.ConstAsmDeps != null)
                    foreach (var dep in sym.ConstAsmDeps)
                        _module.AddressSymbolDeps.Add(dep);
            }
        }
        else if (sym != null && sym.IsCodeBlock)
        {
            // CODEブロック定数: アドレスをロード（LD HL,label）
            Emit(IrOp.LoadAddr, t, IrOperand.Sym(ResolveAsmLabel(node.Name)));
        }
        else if (sym != null && sym.Type is ArrayType)
        {
            // 配列変数: アドレスをロード（LD HL,label）
            Emit(IrOp.LoadAddr, t, IrOperand.Sym(ResolveAsmLabel(node.Name)));
        }
        else
        {
            int ds = sym?.Type?.ByteSize ?? 2;
            Emit(IrOp.LoadVar, t, IrOperand.Sym(ResolveAsmLabel(node.Name)), dataSize: ds);
            _tempDataSize[t.TempIndex] = ds;
        }
        return t;
    }

    public IrOperand VisitBinaryExpr(BinaryExpr node)
    {
        // 定数畳み込み: 両辺が定数ならコンパイル時に計算
        if (_globalSymbols != null)
        {
            var constEval = new ConstEvaluator(_globalSymbols);
            var constResult = constEval.Evaluate(node);
            if (constResult.HasValue)
            {
                var t = IrOperand.Temp(AllocTemp());
                Emit(IrOp.LoadConst, t, IrOperand.Imm(constResult.Value));
                return t;
            }
        }

        // left/rightの型を先にAcceptし、FLOATの場合はleft変換→right Acceptの順にする
        var left = node.Left.Accept(this);
        int leftDs = left.Kind == IrOperandKind.Temp && _tempDataSize.TryGetValue(left.TempIndex, out int lds) ? lds : 2;

        // rightがFLOATかどうかを事前推定（AcceptしないとtempDataSizeは不明だが、
        // FloatLiteralやFLOAT変数は先に型が分かる）
        bool rightMightBeFloat = node.Right is FloatLiteral
            || (node.Right is IdentifierExpr rid && _globalSymbols?.Resolve(rid.Name)?.Type?.ByteSize == 3);

        // leftが整数でrightがFLOATの場合、leftの変換をright Accept前に行う
        if (leftDs != 3 && rightMightBeFloat)
        {
            var conv = IrOperand.Temp(AllocTemp());
            Emit(IrOp.Call, conv, IrOperand.Sym("i16tof24"), IrOperand.Imm(0), dataSize: 3);
            _tempDataSize[conv.TempIndex] = 3;
            left = conv;
            leftDs = 3;
        }

        var right = node.Right.Accept(this);
        var dest = IrOperand.Temp(AllocTemp());

        var op = node.Op switch
        {
            BinaryOp.Add => IrOp.Add, BinaryOp.Sub => IrOp.Sub,
            BinaryOp.Mul => IrOp.Mul, BinaryOp.Div => IrOp.Div, BinaryOp.Mod => IrOp.Mod,
            BinaryOp.SMul => IrOp.SMul, BinaryOp.SDiv => IrOp.SDiv, BinaryOp.SMod => IrOp.SMod,
            BinaryOp.And => IrOp.And, BinaryOp.Or => IrOp.Or, BinaryOp.Xor => IrOp.Xor,
            BinaryOp.Shl => IrOp.Shl, BinaryOp.Shr => IrOp.Shr,
            BinaryOp.SShl => IrOp.SShl, BinaryOp.SShr => IrOp.SShr,
            BinaryOp.Eq => IrOp.CmpEq, BinaryOp.Neq => IrOp.CmpNeq,
            BinaryOp.Lt => IrOp.CmpLt, BinaryOp.Gt => IrOp.CmpGt,
            BinaryOp.Le => IrOp.CmpLe, BinaryOp.Ge => IrOp.CmpGe,
            BinaryOp.SLt => IrOp.CmpSLt, BinaryOp.SGt => IrOp.CmpSGt,
            BinaryOp.SLe => IrOp.CmpSLe, BinaryOp.SGe => IrOp.CmpSGe,
            BinaryOp.LogAnd => IrOp.LogAnd, BinaryOp.LogOr => IrOp.LogOr,
            _ => IrOp.Nop,
        };

        int rightDs = right.Kind == IrOperandKind.Temp && _tempDataSize.TryGetValue(right.TempIndex, out int rds) ? rds : 2;
        int resultDs = (leftDs == 3 || rightDs == 3) ? 3 : 2;

        // Word→Float型変換（rightがFLOATでleftがまだ整数の場合、またはその逆）
        if (resultDs == 3)
        {
            if (leftDs != 3)
            {
                // leftの変換: このケースはrightMightBeFloat判定で漏れた場合のフォールバック
                var conv = IrOperand.Temp(AllocTemp());
                Emit(IrOp.Call, conv, IrOperand.Sym("i16tof24"), IrOperand.Imm(0), dataSize: 3);
                _tempDataSize[conv.TempIndex] = 3;
                left = conv;
            }
            if (rightDs != 3)
            {
                var conv = IrOperand.Temp(AllocTemp());
                Emit(IrOp.Call, conv, IrOperand.Sym("i16tof24"), IrOperand.Imm(0), dataSize: 3);
                _tempDataSize[conv.TempIndex] = 3;
                right = conv;
            }
        }

        Emit(op, dest, left, right, dataSize: resultDs);
        _tempDataSize[dest.TempIndex] = resultDs;
        return dest;
    }

    public IrOperand VisitUnaryExpr(UnaryExpr node)
    {
        var operand = node.Operand.Accept(this);
        if (node.Op == UnaryOp.Plus) return operand;

        var dest = IrOperand.Temp(AllocTemp());
        var op = node.Op switch
        {
            UnaryOp.Negate => IrOp.Neg,
            UnaryOp.Not => IrOp.LogNot,
            UnaryOp.Cpl => IrOp.Not,
            _ => IrOp.Nop,
        };
        Emit(op, dest, operand);
        return dest;
    }

    public IrOperand VisitAssignExpr(AssignExpr node)
    {
        var value = node.Value.Accept(this);
        EmitStore(node.Target, value);
        return value;
    }

    public IrOperand VisitCompoundAssignExpr(CompoundAssignExpr node)
    {
        var target = node.Target.Accept(this);
        var value = node.Value.Accept(this);
        var dest = IrOperand.Temp(AllocTemp());

        var op = node.Op switch
        {
            CompoundAssignOp.AddAssign => IrOp.Add,
            CompoundAssignOp.SubAssign => IrOp.Sub,
            CompoundAssignOp.MulAssign => IrOp.Mul,
            CompoundAssignOp.DivAssign => IrOp.Div,
            _ => IrOp.Nop,
        };

        Emit(op, dest, target, value);
        EmitStore(node.Target, dest);
        return dest;
    }

    public IrOperand VisitIncrementExpr(IncrementExpr node)
    {
        var val = node.Operand.Accept(this);
        var one = IrOperand.Temp(AllocTemp());
        Emit(IrOp.LoadConst, one, IrOperand.Imm(1));
        var result = IrOperand.Temp(AllocTemp());
        Emit(node.IsIncrement ? IrOp.Add : IrOp.Sub, result, val, one);
        EmitStore(node.Operand, result);

        return node.IsPrefix ? result : val;
    }

    public IrOperand VisitCallExpr(CallExpr node)
    {
        var funcName = (node.Function as IdentifierExpr)?.Name;
        var funcSym = funcName != null ? _globalSymbols?.Resolve(funcName) : null;

        // MACHINE関数判定:
        // - SymbolKind.MachineFunction（MACHINE宣言、ビルトイン関数）
        // - シンボル未登録（ランタイム関数）→ 引数個数は呼び出し側の引数数を使用
        // - SymbolKind.Function（ユーザー定義関数）→ IYオフセット渡し
        bool isUserFunc = funcSym?.Kind == SymbolKind.Function;
        bool isMachine = !isUserFunc;
        int? machineParamCount = null;
        if (isMachine)
        {
            if (funcSym?.Type is FunctionType ft)
                machineParamCount = ft.ParameterTypes.Count;
            else
                machineParamCount = node.Arguments.Count; // ランタイム関数: 呼び出し側の引数数
        }

        if (isMachine && machineParamCount.HasValue)
        {
            // MACHINE関数: レジスタ渡し (0:CALL, 1:HL, 2:HL+DE, 3:HL+DE+BC)
            // 各引数を評価した直後にPushArgしてスタックに退避
            for (int i = 0; i < node.Arguments.Count; i++)
            {
                var argVal = node.Arguments[i].Accept(this);
                Emit(IrOp.PushArg, argVal, IrOperand.Imm(i));
            }

            var dest = IrOperand.Temp(AllocTemp());
            // MACHINE:式のアドレスを優先（初回解決時にキャッシュ）
            string asmName;
            if (funcSym?.AddressAst != null)
            {
                if (!funcSym.AddressExprResolved)
                {
                    funcSym.AddressExprResolved = true;
                    var result = LabelUtils.ExprToAsmString(funcSym.AddressAst, _globalSymbols, _diagnostics);
                    if (result.HasValue)
                    {
                        funcSym.AddressExpr = result.Value.Expr;
                        funcSym.AddressExprDeps = result.Value.Deps;
                    }
                }
                asmName = funcSym.AddressExpr ?? funcSym.AsmLabel ?? LabelUtils.SanitizeLabel(funcName!);
                if (funcSym.AddressExprDeps != null)
                    foreach (var dep in funcSym.AddressExprDeps)
                        _module.AddressSymbolDeps.Add(dep);
            }
            else
            {
                asmName = funcSym?.AsmLabel ?? LabelUtils.SanitizeLabel(funcName!);
            }
            Emit(IrOp.Call, dest, IrOperand.Sym(asmName), IrOperand.Imm(machineParamCount.Value));
            return dest;
        }
        else
        {
            // ユーザー関数: (IY+$70)～に引数を格納してCALL
            int argOffset = 0x70;
            foreach (var arg in node.Arguments)
            {
                var argVal = arg.Accept(this);
                // (IY+argOffset) に書き込み
                Emit(IrOp.StoreLocal, IrOperand.Imm(argOffset), argVal);
                argOffset += 2;
            }

            var dest = IrOperand.Temp(AllocTemp());
            var asmName = funcSym?.AsmLabel ?? LabelUtils.SanitizeLabel(funcName ?? "__indirect_call");
            Emit(IrOp.Call, dest, IrOperand.Sym(asmName));
            return dest;
        }
    }

    public IrOperand VisitArrayAccessExpr(ArrayAccessExpr node)
    {
        var arrayName = (node.Array as IdentifierExpr)?.Name;
        var arraySym = arrayName != null ? _globalSymbols?.Resolve(arrayName) : null;

        // システム配列判定 (MEM, MEMW, PORT, PORTW, SOS, SOSW)
        bool isMemArray = arraySym?.Type is MemoryArrayType;
        bool isByteAccess = arraySym?.Type is MemoryArrayType mat && mat.ElementType == SlangType.Byte;

        // 間接変数判定 (VAR x[])
        bool isIndirect = arraySym?.Type is PointerType;
        // 間接変数/配列のBYTE判定
        bool isIndirectByte = arraySym?.Type is PointerType pt && pt.ElementType == SlangType.Byte;
        bool isArrayByte = arraySym?.Type is ArrayType aty && aty.ElementType == SlangType.Byte;

        // PORT/PORTW判定
        bool isPortArray = arrayName != null &&
            (arrayName.Equals("PORT", StringComparison.OrdinalIgnoreCase) ||
             arrayName.Equals("PORTW", StringComparison.OrdinalIgnoreCase));
        bool isPortByte = arrayName != null && arrayName.Equals("PORT", StringComparison.OrdinalIgnoreCase);

        // SOS/SOSW判定
        bool isSosArray = arrayName != null &&
            (arrayName.Equals("SOS", StringComparison.OrdinalIgnoreCase) ||
             arrayName.Equals("SOSW", StringComparison.OrdinalIgnoreCase));

        if (isPortArray)
        {
            // PORT[addr] / PORTW[addr]: I/Oポートアクセス
            var addr = node.Indices[0].Accept(this);
            var dest = IrOperand.Temp(AllocTemp());
            Emit(IrOp.PortIn, dest, addr, dataSize: isPortByte ? 1 : 2);
            return dest;
        }
        else if (isMemArray || isSosArray)
        {
            // MEM[addr] / MEMW[addr] / SOS[addr] / SOSW[addr]: 直接メモリアクセス
            var addr = node.Indices[0].Accept(this);
            var dest = IrOperand.Temp(AllocTemp());
            Emit(IrOp.MemLoad, dest, addr, dataSize: isByteAccess ? 1 : 2);
            return dest;
        }
        else if (isIndirect)
        {
            // 間接変数: IVAL[i] → *(IVAL + i * elemSize)
            var baseAddr = node.Array.Accept(this); // IVALの値(アドレス)をロード
            var idx = node.Indices[0].Accept(this);

            // base + idx * elemSize のアドレスを計算
            int elemSize = isIndirectByte ? 1 : 2;
            IrOperand scaledIdx;
            if (elemSize == 1)
            {
                scaledIdx = idx;
            }
            else
            {
                scaledIdx = IrOperand.Temp(AllocTemp());
                Emit(IrOp.Add, scaledIdx, idx, idx); // ×2
            }
            var addr = IrOperand.Temp(AllocTemp());
            Emit(IrOp.Add, addr, baseAddr, scaledIdx);

            // アドレスから値を読む
            var dest = IrOperand.Temp(AllocTemp());
            Emit(IrOp.IndirLoad, dest, addr, dataSize: elemSize);
            return dest;
        }
        else
        {
            // 通常配列: base_label + sum(index[i] * stride[i])
            //
            // 例: ARRAY WORD AR2[5][10] → 6行×11列のWORD配列
            //   AR2[i][j] → base + (i * 11 * 2) + (j * 2)
            //   stride[0] = dim[1] * elemSize = 11 * 2 = 22
            //   stride[1] = elemSize = 2
            //
            // 例: ARRAY BYTE ARB2[10][30] → 11行×31列のBYTE配列
            //   ARB2[i][j] → base + (i * 31) + j
            //   stride[0] = dim[1] = 31
            //   stride[1] = 1

            // 配列のベースアドレスをロード
            IrOperand baseAddr;
            if (arrayName != null)
            {
                baseAddr = IrOperand.Temp(AllocTemp());
                // ローカル配列: IY+offsetのアドレスを計算
                if (_localVars != null && _localVars.TryGetValue(arrayName, out var localArrInfo) && localArrInfo.IsArray)
                {
                    // ローカル配列のベースアドレス計算: HL = IY + offset
                    Emit(IrOp.Comment, IrOperand.Asm($"local array {arrayName} addr"));
                    Emit(IrOp.InlineAsm, baseAddr, IrOperand.Asm($"\tPUSH\tIY\n\tPOP\tHL\n\tLD\tDE,${localArrInfo.Offset:X4}\n\tADD\tHL,DE"));
                    isArrayByte = localArrInfo.IsByte;
                }
                else if (_localVars != null && _localVars.TryGetValue(arrayName, out var localInfo))
                {
                    Emit(IrOp.LoadLocal, baseAddr, IrOperand.Imm(localInfo.Offset));
                }
                else
                {
                    Emit(IrOp.LoadAddr, baseAddr, IrOperand.Sym(ResolveAsmLabel(arrayName)));
                }
            }
            else
            {
                baseAddr = node.Array.Accept(this);
            }

            // 各次元のストライドを計算
            List<int> strides;
            if (_localVars != null && arrayName != null
                && _localVars.TryGetValue(arrayName, out var arrInfo) && arrInfo.Dims != null)
            {
                // ローカル配列: LocalVarInfoのDimsからストライド計算
                strides = ComputeStridesFromDims(arrInfo.Dims, arrInfo.IsByte ? 1 : 2, node.Indices.Count);
            }
            else
            {
                strides = ComputeStrides(arraySym, node.Indices.Count);
            }

            // 各次元のインデックス×ストライドを加算
            // base + idx0*stride0 + idx1*stride1 + ...
            var addr = baseAddr;
            for (int i = 0; i < node.Indices.Count; i++)
            {
                var idx = node.Indices[i].Accept(this);
                int stride = strides[i];

                // idx * stride をアドレスに加算
                var scaledIdx = IrOperand.Temp(AllocTemp());
                if (stride == 1)
                {
                    scaledIdx = idx;
                }
                else if (stride == 2)
                {
                    // ×2は ADD HL,HL で効率的に生成される
                    Emit(IrOp.Add, scaledIdx, idx, idx);
                }
                else
                {
                    // stride定数を乗算
                    var strideOp = IrOperand.Temp(AllocTemp());
                    Emit(IrOp.LoadConst, strideOp, IrOperand.Imm(stride));
                    Emit(IrOp.Mul, scaledIdx, idx, strideOp);
                }

                var newAddr = IrOperand.Temp(AllocTemp());
                Emit(IrOp.Add, newAddr, addr, scaledIdx);
                addr = newAddr;
            }

            // 最終アドレスから値をロード
            var result = IrOperand.Temp(AllocTemp());
            bool isByte = isArrayByte || (arraySym?.Type is ArrayType at && at.ElementType == SlangType.Byte);
            Emit(IrOp.IndirLoad, result, addr, dataSize: isByte ? 1 : 2);
            return result;
        }
    }

    public IrOperand VisitConditionalExpr(ConditionalExpr node)
    {
        var falseLabel = NewLabel();
        var endLabel = NewLabel();
        var result = IrOperand.Temp(AllocTemp());

        var cond = node.Condition.Accept(this);
        Emit(IrOp.JumpIfZero, IrOperand.Lbl(falseLabel), cond);

        var trueVal = node.TrueExpr.Accept(this);
        // Copy to result temp
        Emit(IrOp.Add, result, trueVal, IrOperand.Imm(0)); // pseudo-copy
        Emit(IrOp.Jump, IrOperand.Lbl(endLabel));

        Emit(IrOp.Label, IrOperand.Lbl(falseLabel));
        var falseVal = node.FalseExpr.Accept(this);
        Emit(IrOp.Add, result, falseVal, IrOperand.Imm(0));

        Emit(IrOp.Label, IrOperand.Lbl(endLabel));
        return result;
    }

    public IrOperand VisitCommaExpr(CommaExpr node)
    {
        node.Left.Accept(this);
        return node.Right.Accept(this);
    }

    public IrOperand VisitAddressOfExpr(AddressOfExpr node)
    {
        if (node.Operand is IdentifierExpr id)
        {
            var t = IrOperand.Temp(AllocTemp());
            Emit(IrOp.LoadAddr, t, IrOperand.Sym(ResolveAsmLabel(id.Name)));
            return t;
        }
        return node.Operand.Accept(this);
    }

    public IrOperand VisitHighLowExpr(HighLowExpr node)
    {
        var val = node.Operand.Accept(this);
        var dest = IrOperand.Temp(AllocTemp());
        Emit(node.IsHigh ? IrOp.High : IrOp.Low, dest, val);
        return dest;
    }

    public IrOperand VisitCodeExpr(CodeExpr node)
    {
        // CODE関数: データを直接オブジェクトに埋め込む。
        // 式中で使用した場合、実行後のHLの値が関数の値。
        foreach (var v in node.Values)
        {
            if (v is StringLiteral str)
            {
                // "文字列" → そのままバイト列（$00なし）
                Emit(IrOp.DefString, IrOperand.Asm(str.Value));
            }
            else if (v is CodeEvalExpr eval)
            {
                // [式] → 式を評価してHLに代入するコードを埋め込み
                eval.Inner.Accept(this);
            }
            else if (v is CodeLabelRef labelRef)
            {
                // <ラベル> → ラベルアドレスを2バイトで埋め込み
                Emit(IrOp.DefWord, IrOperand.Lbl(labelRef.Label));
            }
            else if (v is CastExpr cast)
            {
                // 型,定数式 → BYTE: 1バイト, WORD: 2バイト
                var constVal = _globalSymbols != null ? new ConstEvaluator(_globalSymbols).Evaluate(cast.Operand) : null;
                if (constVal.HasValue)
                {
                    if (cast.TargetSize == DataSize.Byte)
                        Emit(IrOp.DefByte, IrOperand.Imm(constVal.Value & 0xFF));
                    else
                        Emit(IrOp.DefWord, IrOperand.Imm(constVal.Value & 0xFFFF));
                }
                else
                {
                    cast.Operand.Accept(this); // 非定数→実行時コード
                }
            }
            else if (v is IntegerLiteral ilit)
            {
                // 定数(型指定なし) → デフォルト1バイト
                Emit(IrOp.DefByte, IrOperand.Imm(ilit.Value & 0xFF));
            }
            else
            {
                // その他の式 → 実行時コード
                v.Accept(this);
            }
        }

        // CODE関数の値 = 実行後のHLの値
        var dest = IrOperand.Temp(AllocTemp());
        return dest;
    }

    public IrOperand VisitCastExpr(CastExpr node)
    {
        return node.Operand.Accept(this);
    }

    public IrOperand VisitStringFuncExpr(StringFuncExpr node)
    {
        // For PRINT context, evaluate args
        foreach (var arg in node.Arguments)
            arg.Accept(this);
        var t = IrOperand.Temp(AllocTemp());
        Emit(IrOp.Call, t, IrOperand.Sym($"_SF_{LabelUtils.SanitizeLabel(node.FuncName)}"));
        return t;
    }

    // ==== Directives ====

    public IrOperand VisitOrgDirective(OrgDirective node)
    {
        if (node.Value is IntegerLiteral lit)
            _module.OrgAddress = (int)lit.Value;
        return IrOperand.None;
    }

    public IrOperand VisitWorkDirective(WorkDirective node)
    {
        if (node.Value is IntegerLiteral lit)
            _module.WorkAddress = (int)lit.Value;
        return IrOperand.None;
    }

    public IrOperand VisitOffsetDirective(OffsetDirective node)
    {
        if (node.Value is IntegerLiteral lit)
            _module.OffsetAddress = (int)lit.Value;
        return IrOperand.None;
    }

    public IrOperand VisitModuleBlock(ModuleBlock node)
    {
        // オーバーレイモジュール: 別のコンテキストに切り替えてIR生成
        // シンボルテーブルは共有のまま（メイン部と相互参照可能）

        int orgAddr = 0;
        if (node.Name is IntegerLiteral lit)
            orgAddr = (int)lit.Value;
        else
        {
            var constEval = _globalSymbols != null ? new ConstEvaluator(_globalSymbols) : null;
            var val = constEval?.Evaluate(node.Name);
            if (val.HasValue) orgAddr = val.Value;
        }

        var overlay = new OverlayModule
        {
            Index = _module.Overlays.Count,
            OrgAddress = orgAddr,
        };

        // メイン部のFunctionsリストを退避して、オーバーレイ用に切り替え
        var savedFunctions = _module.Functions;
        var overlayFunctions = overlay.Functions;

        // モジュール内の定義をIR化（関数はoverlayのリストに追加される）
        foreach (var def in node.Definitions)
        {
            var prevCount = _module.Functions.Count;
            def.Accept(this);
            // 新たに追加された関数をoverlayに移動
            while (_module.Functions.Count > prevCount)
            {
                var func = _module.Functions[^1];
                _module.Functions.RemoveAt(_module.Functions.Count - 1);
                overlayFunctions.Add(func);
            }
        }

        _module.Overlays.Add(overlay);
        return IrOperand.None;
    }

    public IrOperand VisitPlainAsm(PlainAsm node)
    {
        Emit(IrOp.InlineAsm, IrOperand.Asm(node.AsmText));
        return IrOperand.None;
    }

    // ==== Helpers: Store to lvalue ====

    /// <summary>代入先の型に合わせてFLOAT変換を挿入</summary>
    private IrOperand EmitTypeConversion(IrOperand value, int targetDs)
    {
        int valueDs = value.Kind == IrOperandKind.Temp && _tempDataSize.TryGetValue(value.TempIndex, out int vds) ? vds : 2;
        if (targetDs == 3 && valueDs != 3)
        {
            // Word→Float変換
            var conv = IrOperand.Temp(AllocTemp());
            Emit(IrOp.Call, conv, IrOperand.Sym("i16tof24"), IrOperand.Imm(0), dataSize: 3);
            _tempDataSize[conv.TempIndex] = 3;
            return conv;
        }
        else if (targetDs != 3 && valueDs == 3)
        {
            // Float→Word変換
            var conv = IrOperand.Temp(AllocTemp());
            Emit(IrOp.Call, conv, IrOperand.Sym("FTOI"), IrOperand.Imm(0));
            _tempDataSize[conv.TempIndex] = 2;
            return conv;
        }
        return value;
    }

    private void EmitStore(Expression target, IrOperand value)
    {
        if (target is IdentifierExpr id)
        {
            // ローカル変数優先
            if (_localVars != null && _localVars.TryGetValue(id.Name, out var localInfo))
            {
                value = EmitTypeConversion(value, localInfo.ByteSize);
                Emit(IrOp.StoreLocal, IrOperand.Imm(localInfo.Offset), value, dataSize: localInfo.ByteSize);
            }
            else
            {
                var sym = _globalSymbols?.Resolve(id.Name);
                int ds = sym?.Type?.ByteSize ?? 2;
                value = EmitTypeConversion(value, ds);
                Emit(IrOp.StoreVar, IrOperand.Sym(ResolveAsmLabel(id.Name)), value, dataSize: ds);
            }
        }
        else if (target is ArrayAccessExpr arr)
        {
            var arrayName = (arr.Array as IdentifierExpr)?.Name;
            var arraySym = arrayName != null ? _globalSymbols?.Resolve(arrayName) : null;
            bool isMemArray = arraySym?.Type is MemoryArrayType;
            bool isByteAccess = arraySym?.Type is MemoryArrayType mt && mt.ElementType == SlangType.Byte;
            bool isIndirect = arraySym?.Type is PointerType;
            bool isIndirectByte = arraySym?.Type is PointerType pt2 && pt2.ElementType == SlangType.Byte;

            // PORT/PORTW判定
            bool isPortArray = arrayName != null &&
                (arrayName.Equals("PORT", StringComparison.OrdinalIgnoreCase) ||
                 arrayName.Equals("PORTW", StringComparison.OrdinalIgnoreCase));
            bool isPortByte = arrayName != null && arrayName.Equals("PORT", StringComparison.OrdinalIgnoreCase);

            if (isPortArray)
            {
                // PORT[addr] = value
                var addr = arr.Indices[0].Accept(this);
                Emit(IrOp.PortOut, addr, value, dataSize: isPortByte ? 1 : 2);
            }
            else if (isMemArray)
            {
                var addr = arr.Indices[0].Accept(this);
                Emit(IrOp.MemStore, addr, value, dataSize: isByteAccess ? 1 : 2);
            }
            else if (isIndirect)
            {
                // 間接変数ストア: *(base + idx * elemSize) = value
                var baseAddr = IrOperand.Temp(AllocTemp());
                if (_localVars != null && _localVars.TryGetValue(arrayName!, out var li))
                    Emit(IrOp.LoadLocal, baseAddr, IrOperand.Imm(li.Offset));
                else
                    Emit(IrOp.LoadVar, baseAddr, IrOperand.Sym(ResolveAsmLabel(arrayName!)));

                var idx = arr.Indices[0].Accept(this);
                int elemSize = isIndirectByte ? 1 : 2;

                IrOperand scaledIdx;
                if (elemSize == 1)
                    scaledIdx = idx;
                else
                {
                    scaledIdx = IrOperand.Temp(AllocTemp());
                    Emit(IrOp.Add, scaledIdx, idx, idx);
                }
                var addr = IrOperand.Temp(AllocTemp());
                Emit(IrOp.Add, addr, baseAddr, scaledIdx);
                Emit(IrOp.IndirStore, addr, value, dataSize: elemSize);
            }
            else
            {
                // 通常配列のストア（多次元対応）
                bool storeIsByte = arraySym?.Type is ArrayType stAt && stAt.ElementType == SlangType.Byte;
                IrOperand baseAddr;
                if (arrayName != null)
                {
                    baseAddr = IrOperand.Temp(AllocTemp());
                    // ローカル配列: IY+offsetのアドレスを計算
                    if (_localVars != null && _localVars.TryGetValue(arrayName, out var li) && li.IsArray)
                    {
                        Emit(IrOp.InlineAsm, baseAddr, IrOperand.Asm($"\tPUSH\tIY\n\tPOP\tHL\n\tLD\tDE,${li.Offset:X4}\n\tADD\tHL,DE"));
                        storeIsByte = li.IsByte;
                    }
                    else if (_localVars != null && _localVars.TryGetValue(arrayName, out var li2))
                        Emit(IrOp.LoadLocal, baseAddr, IrOperand.Imm(li2.Offset));
                    else if (arraySym?.Type is PointerType)
                        Emit(IrOp.LoadVar, baseAddr, IrOperand.Sym(ResolveAsmLabel(arrayName)));
                    else
                        Emit(IrOp.LoadAddr, baseAddr, IrOperand.Sym(ResolveAsmLabel(arrayName)));
                }
                else
                {
                    baseAddr = arr.Array.Accept(this);
                }

                // 多次元ストライド計算
                List<int> strides;
                if (_localVars != null && arrayName != null
                    && _localVars.TryGetValue(arrayName, out var stArrInfo) && stArrInfo.Dims != null)
                {
                    strides = ComputeStridesFromDims(stArrInfo.Dims, stArrInfo.IsByte ? 1 : 2, arr.Indices.Count);
                }
                else
                {
                    strides = ComputeStrides(arraySym, arr.Indices.Count);
                }
                var addr = baseAddr;
                for (int i = 0; i < arr.Indices.Count; i++)
                {
                    var idx = arr.Indices[i].Accept(this);
                    int stride = strides[i];

                    var scaledIdx = IrOperand.Temp(AllocTemp());
                    if (stride == 1)
                        scaledIdx = idx;
                    else if (stride == 2)
                        Emit(IrOp.Add, scaledIdx, idx, idx);
                    else
                    {
                        var strideOp = IrOperand.Temp(AllocTemp());
                        Emit(IrOp.LoadConst, strideOp, IrOperand.Imm(stride));
                        Emit(IrOp.Mul, scaledIdx, idx, strideOp);
                    }

                    var newAddr = IrOperand.Temp(AllocTemp());
                    Emit(IrOp.Add, newAddr, addr, scaledIdx);
                    addr = newAddr;
                }

                // 最終アドレスに値を書き込み
                Emit(IrOp.IndirStore, addr, value, dataSize: storeIsByte ? 1 : 2);
            }
        }
        else
        {
            // Indirect store
            var addr = target.Accept(this);
            Emit(IrOp.StoreIndirect, addr, value);
        }
    }

    // ==== Loop stack for EXIT/CONTINUE ====

    /// <summary>
    /// 配列の各次元のストライド(バイト数)を計算する。
    /// 例: ARRAY WORD AR2[5][10] → dims=[6,11], elemSize=2
    ///   stride[0] = 11 * 2 = 22
    ///   stride[1] = 2
    /// </summary>
    private List<int> ComputeStrides(Symbol? arraySym, int indexCount)
    {
        var strides = new List<int>();
        if (arraySym?.Type is ArrayType at)
        {
            for (int i = 0; i < indexCount; i++)
            {
                strides.Add(at.GetStride(i));
            }
        }
        else
        {
            // 型情報なし: WORD(2バイト)デフォルト
            for (int i = 0; i < indexCount; i++)
                strides.Add(2);
        }
        return strides;
    }

    /// <summary>次元リストとelemSizeからストライドを計算</summary>
    private List<int> ComputeStridesFromDims(List<int> dims, int elemSize, int indexCount)
    {
        var strides = new List<int>();
        for (int i = 0; i < indexCount && i < dims.Count; i++)
        {
            int stride = elemSize;
            for (int j = dims.Count - 1; j > i; j--)
                stride *= dims[j];
            strides.Add(stride);
        }
        return strides;
    }

    private readonly Stack<(string ContinueLabel, string BreakLabel)> _loopStack = new();

    private void PushLoop(string cont, string brk) => _loopStack.Push((cont, brk));
    private void PopLoop() => _loopStack.Pop();
    private string? GetBreakLabel() => _loopStack.Count > 0 ? _loopStack.Peek().BreakLabel : null;
    private string? GetContinueLabel() => _loopStack.Count > 0 ? _loopStack.Peek().ContinueLabel : null;
}
