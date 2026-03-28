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

    // 関数内ローカルシンボル（IrGenerator自身が管理）
    private Dictionary<string, LocalVarInfo>? _localVars;

    public IrGenerator(DiagnosticBag diagnostics, SymbolTable? symbols = null)
    {
        _diagnostics = diagnostics;
        _globalSymbols = symbols;
    }

    private record LocalVarInfo(int Offset, int ByteSize);
    private int _localOffset;

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
        if (_currentFunction != null)
            _currentFunction.Instructions.Add(inst);
        else
            _module.GlobalData.Add(inst);
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

        // グローバルスコープ（関数外）ならGlobalVarsに登録
        if (_currentFunction == null)
        {
            int? fixedAddr = null;
            if (node.Address is IntegerLiteral addrLit)
                fixedAddr = (int)addrLit.Value;

            _module.GlobalVars.Add(new GlobalVarInfo
            {
                Name = node.Name,
                AsmLabel = $"_{node.Name}",
                ByteSize = ds,
                FixedAddress = fixedAddr,
            });

            // 初期値付きグローバル変数
            if (node.InitialValue != null)
            {
                var val = node.InitialValue.Accept(this);
                Emit(IrOp.StoreVar, IrOperand.Sym(node.Name), val, dataSize: ds);
            }
        }
        else
        {
            // ローカル変数: オフセット割り当て
            AllocLocalVar(node.Name, ds);

            if (node.InitialValue != null)
            {
                var val = node.InitialValue.Accept(this);
                // ローカルストア
                var info = _localVars![node.Name];
                Emit(IrOp.StoreLocal, IrOperand.Imm(info.Offset), val, dataSize: ds);
            }
        }
        return IrOperand.None;
    }

    public IrOperand VisitArrayDecl(ArrayDecl node)
    {
        int elemSize = node.Size == DataSize.Byte ? 1 : 2;

        // グローバルスコープなら登録
        if (_currentFunction == null)
        {
            int totalSize = elemSize;
            foreach (var dim in node.Dimensions)
            {
                if (dim is IntegerLiteral lit)
                    totalSize *= ((int)lit.Value + 1); // 仕様: +1個分
                else if (dim == null)
                    totalSize = 2; // 間接配列 = ポインタ(2byte)
            }

            int? fixedAddr = null;
            if (node.Address is IntegerLiteral addrLit)
                fixedAddr = (int)addrLit.Value;

            // 初期値付き配列: CODEリストの定数値をバイト列に変換
            List<byte>? initData = null;
            if (node.InitialCode != null)
            {
                initData = new List<byte>();
                foreach (var expr in node.InitialCode)
                {
                    if (expr is IntegerLiteral ilit)
                    {
                        if (elemSize == 1)
                            initData.Add((byte)(ilit.Value & 0xFF));
                        else
                        {
                            initData.Add((byte)(ilit.Value & 0xFF));
                            initData.Add((byte)((ilit.Value >> 8) & 0xFF));
                        }
                    }
                    else if (expr is StringLiteral slit)
                    {
                        foreach (var ch in slit.Value)
                            initData.Add((byte)ch);
                    }
                    else
                    {
                        // 非定数式: プレースホルダ
                        for (int j = 0; j < elemSize; j++) initData.Add(0);
                    }
                }
                // totalSizeに満たない場合は0で埋める
                while (initData.Count < totalSize)
                    initData.Add(0);
            }

            _module.GlobalVars.Add(new GlobalVarInfo
            {
                Name = node.Name,
                AsmLabel = $"_{node.Name}",
                ByteSize = totalSize,
                FixedAddress = fixedAddr,
                IsArray = true,
                InitialData = initData,
            });
        }
        return IrOperand.None;
    }

    public IrOperand VisitConstDecl(ConstDecl node)
    {
        Emit(IrOp.Comment, IrOperand.Asm($"CONST {node.Name}"));
        return IrOperand.None;
    }

    public IrOperand VisitMachineDecl(MachineDecl node)
    {
        Emit(IrOp.Comment, IrOperand.Asm($"MACHINE {node.Name}"));
        return IrOperand.None;
    }

    public IrOperand VisitParamDecl(ParamDecl node) => IrOperand.None;

    // ==== Function ====

    public IrOperand VisitFuncDef(FuncDef node)
    {
        _currentFunction = new IrFunction { Name = node.Name };

        // ローカルシンボルテーブルを構築
        var prevLocalVars = _localVars;
        var prevOffset = _localOffset;
        _localVars = new Dictionary<string, LocalVarInfo>(StringComparer.OrdinalIgnoreCase);
        _localOffset = 0;

        // 仮引数を登録 (IY+$70から上方向)
        int argOffset = 0x70;
        foreach (var p in node.Parameters)
        {
            _localVars[p.Name] = new LocalVarInfo(argOffset, 2);
            argOffset += 2;
        }

        Emit(IrOp.FuncBegin, IrOperand.Sym(node.Name));

        // Static/local declarations (静的宣言 → グローバルメモリ、局所宣言 → 動的)
        foreach (var d in node.StaticDeclarations) d.Accept(this);
        foreach (var d in node.LocalDeclarations) d.Accept(this);

        // Body
        node.Body.Accept(this);

        // Return value from END(expr)
        if (node.ReturnValue != null)
        {
            var retVal = node.ReturnValue.Accept(this);
            Emit(IrOp.Return, retVal);
        }

        Emit(IrOp.FuncEnd);
        _module.Functions.Add(_currentFunction);
        _currentFunction = null;
        _localVars = prevLocalVars;
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

        for (int i = 0; i < node.Branches.Count; i++)
        {
            var (cond, body) = node.Branches[i];
            var nextLabel = (i < node.Branches.Count - 1 || node.ElseBody != null) ? NewLabel() : endLabel;

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

        Emit(IrOp.Label, IrOperand.Lbl(startLabel));
        var condVal = node.Condition.Accept(this);
        Emit(IrOp.JumpIfZero, IrOperand.Lbl(endLabel), condVal);

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
        Emit(IrOp.StoreVar, IrOperand.Sym(node.Variable), fromVal);

        PushLoop(contLabel, endLabel);

        Emit(IrOp.Label, IrOperand.Lbl(startLabel));

        // Body
        node.Body.Accept(this);

        // Continue label (increment/decrement)
        Emit(IrOp.Label, IrOperand.Lbl(contLabel));

        // Increment/decrement
        var curVal = IrOperand.Temp(AllocTemp());
        Emit(IrOp.LoadVar, curVal, IrOperand.Sym(node.Variable));
        var one = IrOperand.Temp(AllocTemp());
        Emit(IrOp.LoadConst, one, IrOperand.Imm(1));
        var newVal = IrOperand.Temp(AllocTemp());
        Emit(node.IsDownTo ? IrOp.Sub : IrOp.Add, newVal, curVal, one);
        Emit(IrOp.StoreVar, IrOperand.Sym(node.Variable), newVal);

        // Compare with limit
        var limit = node.To.Accept(this);
        var cmp = IrOperand.Temp(AllocTemp());
        Emit(node.IsDownTo ? IrOp.CmpGe : IrOp.CmpLe, cmp, newVal, limit);
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
        Emit(IrOp.Jump, IrOperand.Lbl(node.Label));
        return IrOperand.None;
    }

    public IrOperand VisitLabelStmt(LabelStmt node)
    {
        Emit(IrOp.Label, IrOperand.Lbl(node.Label));
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
                    // PSTR: HL=文字列アドレス
                    Emit(IrOp.Call, IrOperand.None, IrOperand.Sym("PSTR"));
                }
                else
                {
                    // PRT: HL=数値
                    Emit(IrOp.Call, IrOperand.None, IrOperand.Sym("PRT"));
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

    public IrOperand VisitFloatLiteral(FloatLiteral node)
    {
        var t = IrOperand.Temp(AllocTemp());
        Emit(IrOp.LoadConst, t, IrOperand.Imm((long)BitConverter.DoubleToInt64Bits(node.Value)));
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
            Emit(IrOp.LoadLocal, t, IrOperand.Imm(localInfo.Offset), dataSize: localInfo.ByteSize);
            return t;
        }

        // 2. グローバルシンボルテーブルを検索
        var sym = _globalSymbols?.Resolve(node.Name);
        if (sym != null && sym.Kind == SymbolKind.Constant && sym.ConstValue is int constVal)
        {
            // 定数 → 即値ロード
            Emit(IrOp.LoadConst, t, IrOperand.Imm(constVal));
        }
        else
        {
            // グローバル変数 or 未解決 → ラベルアクセス
            Emit(IrOp.LoadVar, t, IrOperand.Sym(node.Name));
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

        var left = node.Left.Accept(this);
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

        Emit(op, dest, left, right);
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

        // MACHINE関数判定
        bool isMachine = funcSym?.Kind == SymbolKind.MachineFunction;
        int? machineParamCount = null;
        if (isMachine && funcSym!.Type is FunctionType ft)
            machineParamCount = ft.ParameterTypes.Count;

        if (isMachine && machineParamCount.HasValue)
        {
            // MACHINE関数: レジスタ渡し (0:CALL, 1:HL, 2:HL+DE, 3:HL+DE+BC, 4+:スタック)
            var args = new List<IrOperand>();
            foreach (var arg in node.Arguments)
                args.Add(arg.Accept(this));

            var dest = IrOperand.Temp(AllocTemp());
            // 引数数に応じた渡し方をIR命令に埋め込む
            // Src2にMACHINEの引数数を渡す
            for (int i = 0; i < args.Count; i++)
                Emit(IrOp.PushArg, args[i], IrOperand.Imm(i));

            Emit(IrOp.Call, dest, IrOperand.Sym(funcName!), IrOperand.Imm(machineParamCount.Value));
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
            Emit(IrOp.Call, dest, IrOperand.Sym(funcName ?? "__indirect_call"));
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

        if (isMemArray)
        {
            // MEM[addr] / MEMW[addr]: インデックスがそのままアドレス
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
            var dest = IrOperand.Temp(AllocTemp());
            int elemSize = isByteAccess ? 1 : 2;
            Emit(IrOp.ArrayLoad, dest, baseAddr, idx, dataSize: elemSize);
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
                // ローカル配列チェック
                if (_localVars != null && _localVars.TryGetValue(arrayName, out var localInfo))
                    Emit(IrOp.LoadLocal, baseAddr, IrOperand.Imm(localInfo.Offset));
                else
                    Emit(IrOp.LoadAddr, baseAddr, IrOperand.Sym(arrayName));
            }
            else
            {
                baseAddr = node.Array.Accept(this);
            }

            // 各次元のストライドを計算
            var strides = ComputeStrides(arraySym, node.Indices.Count);

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
            bool isByte = arraySym?.Type is ArrayType at && at.ElementType == SlangType.Byte;
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
            Emit(IrOp.LoadAddr, t, IrOperand.Sym(id.Name));
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
        // CODE(values...) - emit as data bytes
        var dest = IrOperand.Temp(AllocTemp());
        foreach (var v in node.Values)
            v.Accept(this);
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
        Emit(IrOp.Call, t, IrOperand.Sym($"__strfunc_{node.FuncName}"));
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
        foreach (var def in node.Definitions)
            def.Accept(this);
        return IrOperand.None;
    }

    public IrOperand VisitPlainAsm(PlainAsm node)
    {
        Emit(IrOp.InlineAsm, IrOperand.Asm(node.AsmText));
        return IrOperand.None;
    }

    // ==== Helpers: Store to lvalue ====

    private void EmitStore(Expression target, IrOperand value)
    {
        if (target is IdentifierExpr id)
        {
            // ローカル変数優先
            if (_localVars != null && _localVars.TryGetValue(id.Name, out var localInfo))
            {
                Emit(IrOp.StoreLocal, IrOperand.Imm(localInfo.Offset), value, dataSize: localInfo.ByteSize);
            }
            else
            {
                Emit(IrOp.StoreVar, IrOperand.Sym(id.Name), value);
            }
        }
        else if (target is ArrayAccessExpr arr)
        {
            var arrayName = (arr.Array as IdentifierExpr)?.Name;
            var arraySym = arrayName != null ? _globalSymbols?.Resolve(arrayName) : null;
            bool isMemArray = arraySym?.Type is MemoryArrayType;
            bool isByteAccess = arraySym?.Type is MemoryArrayType mt && mt.ElementType == SlangType.Byte;

            if (isMemArray)
            {
                // MEM[addr] = value / MEMW[addr] = value
                var addr = arr.Indices[0].Accept(this);
                Emit(IrOp.MemStore, addr, value, dataSize: isByteAccess ? 1 : 2);
            }
            else
            {
                // 通常配列/間接変数のストア（多次元対応）
                IrOperand baseAddr;
                if (arrayName != null)
                {
                    baseAddr = IrOperand.Temp(AllocTemp());
                    if (_localVars != null && _localVars.TryGetValue(arrayName, out var li))
                        Emit(IrOp.LoadLocal, baseAddr, IrOperand.Imm(li.Offset));
                    else if (arraySym?.Type is PointerType)
                        Emit(IrOp.LoadVar, baseAddr, IrOperand.Sym(arrayName));
                    else
                        Emit(IrOp.LoadAddr, baseAddr, IrOperand.Sym(arrayName));
                }
                else
                {
                    baseAddr = arr.Array.Accept(this);
                }

                // 多次元ストライド計算してアドレスを算出
                var strides = ComputeStrides(arraySym, arr.Indices.Count);
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
                bool isByte = arraySym?.Type is ArrayType at && at.ElementType == SlangType.Byte;
                Emit(IrOp.IndirStore, addr, value, dataSize: isByte ? 1 : 2);
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

    private readonly Stack<(string ContinueLabel, string BreakLabel)> _loopStack = new();

    private void PushLoop(string cont, string brk) => _loopStack.Push((cont, brk));
    private void PopLoop() => _loopStack.Pop();
    private string? GetBreakLabel() => _loopStack.Count > 0 ? _loopStack.Peek().BreakLabel : null;
    private string? GetContinueLabel() => _loopStack.Count > 0 ? _loopStack.Peek().ContinueLabel : null;
}
