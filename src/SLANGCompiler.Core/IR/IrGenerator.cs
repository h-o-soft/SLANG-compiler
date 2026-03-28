using SLANGCompiler.Parser.Ast;
using SLANGCompiler.Semantics;

namespace SLANGCompiler.IR;

/// <summary>
/// AST → IR変換（骨格）
/// </summary>
public class IrGenerator : IAstVisitor<IrOperand>
{
    private readonly DiagnosticBag _diagnostics;
    private readonly IrModule _module = new();
    private IrFunction? _currentFunction;

    public IrGenerator(DiagnosticBag diagnostics)
    {
        _diagnostics = diagnostics;
    }

    public IrModule Generate(CompilationUnit unit)
    {
        unit.Accept(this);
        return _module;
    }

    private int AllocTemp() => _currentFunction?.AllocTemp() ?? 0;

    private void Emit(IrOp op, IrOperand dest = default, IrOperand src1 = default, IrOperand src2 = default)
    {
        var inst = new IrInstruction(op, dest, src1, src2);
        if (_currentFunction != null)
            _currentFunction.Instructions.Add(inst);
        else
            _module.GlobalData.Add(inst);
    }

    // ---- Visitor implementations ----

    public IrOperand VisitCompilationUnit(CompilationUnit node)
    {
        foreach (var def in node.Definitions)
            def.Accept(this);
        return IrOperand.None;
    }

    public IrOperand VisitFuncDef(FuncDef node)
    {
        _currentFunction = new IrFunction { Name = node.Name };
        Emit(IrOp.FuncBegin, IrOperand.Sym(node.Name));

        node.Body.Accept(this);

        if (node.ReturnValue != null)
        {
            var retVal = node.ReturnValue.Accept(this);
            Emit(IrOp.Return, retVal);
        }
        else
        {
            Emit(IrOp.FuncEnd);
        }

        _module.Functions.Add(_currentFunction);
        _currentFunction = null;
        return IrOperand.None;
    }

    public IrOperand VisitBlock(Block node)
    {
        foreach (var stmt in node.Statements)
            stmt.Accept(this);
        return IrOperand.None;
    }

    public IrOperand VisitExpressionStmt(ExpressionStmt node)
    {
        node.Expr.Accept(this);
        return IrOperand.None;
    }

    public IrOperand VisitIntegerLiteral(IntegerLiteral node)
    {
        var temp = IrOperand.Temp(AllocTemp());
        Emit(IrOp.LoadConst, temp, IrOperand.Imm(node.Value));
        return temp;
    }

    public IrOperand VisitFloatLiteral(FloatLiteral node)
    {
        var temp = IrOperand.Temp(AllocTemp());
        Emit(IrOp.LoadConst, temp, IrOperand.Imm((long)BitConverter.DoubleToInt64Bits(node.Value)));
        return temp;
    }

    public IrOperand VisitStringLiteral(StringLiteral node)
    {
        var temp = IrOperand.Temp(AllocTemp());
        // TODO: register string in string table and load address
        Emit(IrOp.LoadConst, temp, IrOperand.Imm(0));
        return temp;
    }

    public IrOperand VisitIdentifier(IdentifierExpr node)
    {
        var temp = IrOperand.Temp(AllocTemp());
        Emit(IrOp.LoadVar, temp, IrOperand.Sym(node.Name));
        return temp;
    }

    public IrOperand VisitBinaryExpr(BinaryExpr node)
    {
        var left = node.Left.Accept(this);
        var right = node.Right.Accept(this);
        var dest = IrOperand.Temp(AllocTemp());

        var op = node.Op switch
        {
            BinaryOp.Add => IrOp.Add,
            BinaryOp.Sub => IrOp.Sub,
            BinaryOp.Mul => IrOp.Mul,
            BinaryOp.Div => IrOp.Div,
            BinaryOp.Mod => IrOp.Mod,
            BinaryOp.SMul => IrOp.SMul,
            BinaryOp.SDiv => IrOp.SDiv,
            BinaryOp.SMod => IrOp.SMod,
            BinaryOp.And => IrOp.And,
            BinaryOp.Or => IrOp.Or,
            BinaryOp.Xor => IrOp.Xor,
            BinaryOp.Shl => IrOp.Shl,
            BinaryOp.Shr => IrOp.Shr,
            BinaryOp.SShl => IrOp.SShl,
            BinaryOp.SShr => IrOp.SShr,
            BinaryOp.Eq => IrOp.CmpEq,
            BinaryOp.Neq => IrOp.CmpNeq,
            BinaryOp.Lt => IrOp.CmpLt,
            BinaryOp.Gt => IrOp.CmpGt,
            BinaryOp.Le => IrOp.CmpLe,
            BinaryOp.Ge => IrOp.CmpGe,
            BinaryOp.SLt => IrOp.CmpSLt,
            BinaryOp.SGt => IrOp.CmpSGt,
            BinaryOp.SLe => IrOp.CmpSLe,
            BinaryOp.SGe => IrOp.CmpSGe,
            BinaryOp.LogAnd => IrOp.LogAnd,
            BinaryOp.LogOr => IrOp.LogOr,
            _ => IrOp.Nop,
        };

        Emit(op, dest, left, right);
        return dest;
    }

    public IrOperand VisitUnaryExpr(UnaryExpr node)
    {
        var operand = node.Operand.Accept(this);
        var dest = IrOperand.Temp(AllocTemp());

        var op = node.Op switch
        {
            UnaryOp.Negate => IrOp.Neg,
            UnaryOp.Not => IrOp.LogNot,
            UnaryOp.Cpl => IrOp.Not,
            UnaryOp.Plus => IrOp.Nop,
            _ => IrOp.Nop,
        };

        if (op == IrOp.Nop) return operand;
        Emit(op, dest, operand);
        return dest;
    }

    public IrOperand VisitAssignExpr(AssignExpr node)
    {
        var value = node.Value.Accept(this);
        if (node.Target is IdentifierExpr id)
        {
            Emit(IrOp.StoreVar, IrOperand.Sym(id.Name), value);
        }
        else if (node.Target is ArrayAccessExpr arr)
        {
            // TODO: proper array store
            var baseAddr = arr.Array.Accept(this);
            Emit(IrOp.ArrayStore, baseAddr, value);
        }
        return value;
    }

    public IrOperand VisitCallExpr(CallExpr node)
    {
        // Push arguments
        foreach (var arg in node.Arguments)
        {
            var argVal = arg.Accept(this);
            Emit(IrOp.PushArg, argVal);
        }

        // Call
        var dest = IrOperand.Temp(AllocTemp());
        if (node.Function is IdentifierExpr func)
        {
            Emit(IrOp.Call, dest, IrOperand.Sym(func.Name));
        }
        return dest;
    }

    public IrOperand VisitArrayAccessExpr(ArrayAccessExpr node)
    {
        var baseAddr = node.Array.Accept(this);
        var dest = IrOperand.Temp(AllocTemp());

        // For each dimension, compute scaled offset
        // TODO: proper multi-dimensional array offset calculation
        foreach (var index in node.Indices)
        {
            var idx = index.Accept(this);
            Emit(IrOp.ArrayLoad, dest, baseAddr, idx);
            baseAddr = dest;
        }

        return dest;
    }

    // ---- Stub implementations for remaining visitors ----

    public IrOperand VisitOrgDirective(OrgDirective node) { return IrOperand.None; }
    public IrOperand VisitWorkDirective(WorkDirective node) { return IrOperand.None; }
    public IrOperand VisitOffsetDirective(OffsetDirective node) { return IrOperand.None; }
    public IrOperand VisitModuleBlock(ModuleBlock node) { return IrOperand.None; }
    public IrOperand VisitPlainAsm(PlainAsm node)
    {
        Emit(IrOp.InlineAsm, IrOperand.Asm(node.AsmText));
        return IrOperand.None;
    }

    public IrOperand VisitVarDecl(VarDecl node) { return IrOperand.None; }
    public IrOperand VisitArrayDecl(ArrayDecl node) { return IrOperand.None; }
    public IrOperand VisitConstDecl(ConstDecl node) { return IrOperand.None; }
    public IrOperand VisitMachineDecl(MachineDecl node) { return IrOperand.None; }
    public IrOperand VisitParamDecl(ParamDecl node) { return IrOperand.None; }

    public IrOperand VisitIfStmt(IfStmt node)
    {
        // TODO: implement control flow
        return IrOperand.None;
    }

    public IrOperand VisitWhileStmt(WhileStmt node) { return IrOperand.None; }
    public IrOperand VisitRepeatStmt(RepeatStmt node) { return IrOperand.None; }
    public IrOperand VisitLoopStmt(LoopStmt node) { return IrOperand.None; }
    public IrOperand VisitForStmt(ForStmt node) { return IrOperand.None; }
    public IrOperand VisitCaseStmt(CaseStmt node) { return IrOperand.None; }
    public IrOperand VisitExitStmt(ExitStmt node) { return IrOperand.None; }
    public IrOperand VisitContinueStmt(ContinueStmt node) { return IrOperand.None; }

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

    public IrOperand VisitPrintStmt(PrintStmt node) { return IrOperand.None; }

    public IrOperand VisitCompoundAssignExpr(CompoundAssignExpr node)
    {
        var value = node.Value.Accept(this);
        var target = node.Target.Accept(this);
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
        if (node.Target is IdentifierExpr id)
        {
            Emit(IrOp.StoreVar, IrOperand.Sym(id.Name), dest);
        }
        return dest;
    }

    public IrOperand VisitIncrementExpr(IncrementExpr node) { return IrOperand.None; }
    public IrOperand VisitConditionalExpr(ConditionalExpr node) { return IrOperand.None; }
    public IrOperand VisitCommaExpr(CommaExpr node) { return IrOperand.None; }
    public IrOperand VisitAddressOfExpr(AddressOfExpr node) { return IrOperand.None; }
    public IrOperand VisitHighLowExpr(HighLowExpr node) { return IrOperand.None; }
    public IrOperand VisitCodeExpr(CodeExpr node) { return IrOperand.None; }
    public IrOperand VisitCastExpr(CastExpr node) { return IrOperand.None; }
    public IrOperand VisitStringFuncExpr(StringFuncExpr node) { return IrOperand.None; }
}
