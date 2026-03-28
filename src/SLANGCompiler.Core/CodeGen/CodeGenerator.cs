using SLANGCompiler.IR;

namespace SLANGCompiler.CodeGen;

/// <summary>
/// IR → Z80 アセンブリのコード生成器。
/// レジスタ使用規約:
///   HL = 主演算レジスタ / 戻り値
///   DE = 第2演算レジスタ / 引数
///   BC = 第3レジスタ / ループカウンタ
///   SP = スタックポインタ
///   IX = ローカル変数ベースポインタ
///
/// 一時変数はスタックにspillする簡易方式。
/// </summary>
public class CodeGenerator
{
    private readonly IrModule _module;
    private readonly Z80Emitter _e;

    public CodeGenerator(IrModule module)
    {
        _module = module;
        _e = new Z80Emitter();
    }

    public string Generate()
    {
        // Global data
        foreach (var inst in _module.GlobalData)
        {
            EmitGlobalData(inst);
        }

        _e.Blank();

        // Functions
        foreach (var func in _module.Functions)
        {
            EmitFunction(func);
            _e.Blank();
        }

        return _e.ToAssembly();
    }

    private void EmitGlobalData(IrInstruction inst)
    {
        switch (inst.Op)
        {
            case IrOp.Comment:
                _e.Comment(inst.Dest.Name ?? "");
                break;
            case IrOp.StoreVar:
                // Global variable initialization - we'll handle this differently
                // For now, emit as a comment
                _e.Comment($"global init: {inst.Dest.Name} = {inst.Src1}");
                break;
            default:
                _e.Comment($"global: {inst}");
                break;
        }
    }

    private void EmitFunction(IrFunction func)
    {
        _e.Label(func.Name);

        foreach (var inst in func.Instructions)
        {
            EmitInstruction(inst);
        }
    }

    private void EmitInstruction(IrInstruction inst)
    {
        switch (inst.Op)
        {
            case IrOp.FuncBegin:
                EmitFuncBegin(inst);
                break;
            case IrOp.FuncEnd:
                EmitFuncEnd();
                break;

            case IrOp.LoadConst:
                EmitLoadConst(inst);
                break;
            case IrOp.LoadVar:
                EmitLoadVar(inst);
                break;
            case IrOp.StoreVar:
                EmitStoreVar(inst);
                break;
            case IrOp.LoadAddr:
                EmitLoadAddr(inst);
                break;

            case IrOp.Add: EmitArith(inst, "ADD"); break;
            case IrOp.Sub: EmitArith(inst, "SUB"); break;
            case IrOp.Mul: EmitMul(inst); break;
            case IrOp.Div: EmitDiv(inst, signed: false); break;
            case IrOp.Mod: EmitMod(inst, signed: false); break;
            case IrOp.SMul: EmitMul(inst); break;
            case IrOp.SDiv: EmitDiv(inst, signed: true); break;
            case IrOp.SMod: EmitMod(inst, signed: true); break;
            case IrOp.Neg: EmitNeg(inst); break;

            case IrOp.And: EmitBitwise(inst, "AND"); break;
            case IrOp.Or: EmitBitwise(inst, "OR"); break;
            case IrOp.Xor: EmitBitwise(inst, "XOR"); break;
            case IrOp.Not: EmitCpl(inst); break;
            case IrOp.Shl: EmitShift(inst, left: true); break;
            case IrOp.Shr: EmitShift(inst, left: false); break;

            case IrOp.CmpEq: EmitCompare(inst, "Z"); break;
            case IrOp.CmpNeq: EmitCompare(inst, "NZ"); break;
            case IrOp.CmpLt: EmitCompare(inst, "C"); break;
            case IrOp.CmpGe: EmitCompare(inst, "NC"); break;
            case IrOp.CmpGt: EmitCompareGt(inst); break;
            case IrOp.CmpLe: EmitCompareLe(inst); break;
            case IrOp.CmpSLt: EmitSignedCompare(inst, "LT"); break;
            case IrOp.CmpSGt: EmitSignedCompare(inst, "GT"); break;
            case IrOp.CmpSLe: EmitSignedCompare(inst, "LE"); break;
            case IrOp.CmpSGe: EmitSignedCompare(inst, "GE"); break;

            case IrOp.LogAnd: EmitLogAnd(inst); break;
            case IrOp.LogOr: EmitLogOr(inst); break;
            case IrOp.LogNot: EmitLogNot(inst); break;

            case IrOp.High: EmitHighLow(inst, high: true); break;
            case IrOp.Low: EmitHighLow(inst, high: false); break;

            case IrOp.ArrayLoad: EmitArrayLoad(inst); break;
            case IrOp.ArrayStore: EmitArrayStore(inst); break;

            case IrOp.Label:
                _e.Label(inst.Dest.Name ?? "");
                break;
            case IrOp.Jump:
                _e.Instruction("JP", inst.Dest.Name);
                break;
            case IrOp.JumpIfZero:
                // src1 has the condition value (in HL after eval)
                _e.Comment($"if {inst.Src1} == 0 goto {inst.Dest.Name}");
                _e.Instruction("LD", "A,H");
                _e.Instruction("OR", "L");
                _e.Instruction("JP", $"Z,{inst.Dest.Name}");
                break;
            case IrOp.JumpIfNonZero:
                _e.Comment($"if {inst.Src1} != 0 goto {inst.Dest.Name}");
                _e.Instruction("LD", "A,H");
                _e.Instruction("OR", "L");
                _e.Instruction("JP", $"NZ,{inst.Dest.Name}");
                break;

            case IrOp.Call:
                EmitCall(inst);
                break;
            case IrOp.Return:
                if (inst.Dest.Kind != IrOperandKind.None)
                {
                    _e.Comment($"return {inst.Dest}");
                    // value should be in HL already
                }
                _e.Instruction("JP", "_EXIT");
                break;

            case IrOp.PushArg:
                _e.Instruction("PUSH", "HL");
                break;

            case IrOp.InlineAsm:
                if (inst.Dest.Name != null)
                    _e.Raw(inst.Dest.Name);
                break;

            case IrOp.Comment:
                _e.Comment(inst.Dest.Name ?? "");
                break;

            case IrOp.Nop:
                break;

            default:
                _e.Comment($"TODO: {inst}");
                break;
        }
    }

    // ==== Instruction emission ====

    private void EmitFuncBegin(IrInstruction inst)
    {
        // 仕様: IYレジスタでローカル変数を管理
        // 動的変数: (IY+$00)～(IY+$6F) 最大240バイト
        // 引数:     (IY+$70)～(IY+$7F) 最大8個
        _e.Comment($"function {inst.Dest.Name}");
        _e.Instruction("PUSH", "IY");
        // TODO: 動的変数サイズに応じてIYを調整
        // LD BC, n ; ADD IY, BC
    }

    private void EmitFuncEnd()
    {
        _e.Label("_EXIT");
        _e.Instruction("POP", "IY");
        _e.Instruction("RET");
    }

    private void EmitLoadConst(IrInstruction inst)
    {
        if (inst.Src1.Kind == IrOperandKind.AsmString)
        {
            // String constant - load address of string data
            _e.Comment($"load string {inst.Src1.Name}");
            // TODO: proper string table lookup
            _e.Instruction("LD", $"HL,0 ; string placeholder");
        }
        else
        {
            int val = (int)(inst.Src1.ImmediateValue & 0xFFFF);
            if (inst.DataSize == 1)
            {
                _e.Instruction("LD", $"A,${val & 0xFF:X2}");
            }
            else
            {
                _e.Instruction("LD", $"HL,${val:X4}");
            }
        }
    }

    private void EmitLoadVar(IrInstruction inst)
    {
        var name = inst.Src1.Name!;
        _e.Instruction("LD", $"HL,({name})");
    }

    private void EmitStoreVar(IrInstruction inst)
    {
        var name = inst.Dest.Name!;
        _e.Instruction("LD", $"({name}),HL");
    }

    private void EmitLoadAddr(IrInstruction inst)
    {
        var name = inst.Src1.Name!;
        _e.Instruction("LD", $"HL,{name}");
    }

    private void EmitArith(IrInstruction inst, string op)
    {
        // HL = src1 op src2
        // src1 should be in HL, src2 in DE
        _e.Instruction("PUSH", "HL"); // save src1
        // src2 is evaluated and in HL
        _e.Instruction("POP", "DE"); // DE = src1
        _e.Instruction("EX", "DE,HL"); // HL = src1, DE = src2
        if (op == "ADD")
        {
            _e.Instruction("ADD", "HL,DE");
        }
        else // SUB
        {
            _e.Instruction("OR", "A"); // clear carry
            _e.Instruction("SBC", "HL,DE");
        }
    }

    private void EmitMul(IrInstruction inst)
    {
        _e.Instruction("PUSH", "HL");
        _e.Instruction("POP", "DE");
        _e.Instruction("EX", "DE,HL");
        _e.Instruction("CALL", "MUL16");
    }

    private void EmitDiv(IrInstruction inst, bool signed)
    {
        _e.Instruction("PUSH", "HL");
        _e.Instruction("POP", "DE");
        _e.Instruction("EX", "DE,HL");
        _e.Instruction("CALL", signed ? "SDIV16" : "DIV16");
    }

    private void EmitMod(IrInstruction inst, bool signed)
    {
        _e.Instruction("PUSH", "HL");
        _e.Instruction("POP", "DE");
        _e.Instruction("EX", "DE,HL");
        _e.Instruction("CALL", signed ? "SMOD16" : "MOD16");
    }

    private void EmitNeg(IrInstruction inst)
    {
        // HL = -HL
        _e.Instruction("LD", "A,H");
        _e.Instruction("CPL");
        _e.Instruction("LD", "H,A");
        _e.Instruction("LD", "A,L");
        _e.Instruction("CPL");
        _e.Instruction("LD", "L,A");
        _e.Instruction("INC", "HL");
    }

    private void EmitBitwise(IrInstruction inst, string op)
    {
        // HL = HL op DE (byte-by-byte)
        _e.Instruction("PUSH", "HL");
        _e.Instruction("POP", "DE");
        _e.Instruction("EX", "DE,HL");
        _e.Instruction("LD", "A,H");
        _e.Instruction(op, "D");
        _e.Instruction("LD", "H,A");
        _e.Instruction("LD", "A,L");
        _e.Instruction(op, "E");
        _e.Instruction("LD", "L,A");
    }

    private void EmitCpl(IrInstruction inst)
    {
        _e.Instruction("LD", "A,H");
        _e.Instruction("CPL");
        _e.Instruction("LD", "H,A");
        _e.Instruction("LD", "A,L");
        _e.Instruction("CPL");
        _e.Instruction("LD", "L,A");
    }

    private void EmitShift(IrInstruction inst, bool left)
    {
        // Shift HL by DE amount
        _e.Instruction("PUSH", "HL");
        _e.Instruction("POP", "DE");
        _e.Instruction("EX", "DE,HL");
        _e.Instruction("CALL", left ? "SHL16" : "SHR16");
    }

    private void EmitCompare(IrInstruction inst, string cond)
    {
        // Compare: HL op DE → result in HL (0 or 1)
        _e.Instruction("PUSH", "HL");
        _e.Instruction("POP", "DE");
        _e.Instruction("EX", "DE,HL");
        _e.Instruction("OR", "A");
        _e.Instruction("SBC", "HL,DE");
        _e.Instruction("LD", "HL,$0000");
        _e.Instruction($"JR", $"{cond},$+3");
        _e.Instruction("INC", "HL");
    }

    private void EmitCompareGt(IrInstruction inst)
    {
        // HL > DE : swap and use C flag
        _e.Instruction("PUSH", "HL");
        _e.Instruction("POP", "DE");
        _e.Instruction("EX", "DE,HL");
        _e.Instruction("EX", "DE,HL"); // DE = left, HL = right
        _e.Instruction("OR", "A");
        _e.Instruction("SBC", "HL,DE"); // right - left
        _e.Instruction("LD", "HL,$0000");
        _e.Instruction("JR", "C,$+3");
        _e.Instruction("INC", "HL");
    }

    private void EmitCompareLe(IrInstruction inst)
    {
        // HL <= DE : !(HL > DE)
        EmitCompareGt(inst);
        // Invert
        _e.Instruction("LD", "A,L");
        _e.Instruction("XOR", "$01");
        _e.Instruction("LD", "L,A");
    }

    private void EmitSignedCompare(IrInstruction inst, string kind)
    {
        // Signed comparison using runtime helper
        _e.Instruction("PUSH", "HL");
        _e.Instruction("POP", "DE");
        _e.Instruction("EX", "DE,HL");
        _e.Instruction("CALL", $"SCMP_{kind}");
    }

    private void EmitLogAnd(IrInstruction inst)
    {
        // HL = (src1 != 0) && (src2 != 0)
        _e.Instruction("PUSH", "HL");
        _e.Instruction("POP", "DE");
        _e.Instruction("EX", "DE,HL");
        _e.Instruction("LD", "A,H");
        _e.Instruction("OR", "L");
        _e.Instruction("LD", "H,$00");
        _e.Instruction("LD", "L,$00");
        _e.Instruction("JR", "Z,$+7");
        _e.Instruction("LD", "A,D");
        _e.Instruction("OR", "E");
        _e.Instruction("JR", "Z,$+3");
        _e.Instruction("INC", "HL");
    }

    private void EmitLogOr(IrInstruction inst)
    {
        _e.Instruction("PUSH", "HL");
        _e.Instruction("POP", "DE");
        _e.Instruction("EX", "DE,HL");
        _e.Instruction("LD", "A,H");
        _e.Instruction("OR", "L");
        _e.Instruction("OR", "D");
        _e.Instruction("OR", "E");
        _e.Instruction("LD", "HL,$0000");
        _e.Instruction("JR", "Z,$+3");
        _e.Instruction("INC", "HL");
    }

    private void EmitLogNot(IrInstruction inst)
    {
        _e.Instruction("LD", "A,H");
        _e.Instruction("OR", "L");
        _e.Instruction("LD", "HL,$0001");
        _e.Instruction("JR", "Z,$+3");
        _e.Instruction("DEC", "HL");
    }

    private void EmitHighLow(IrInstruction inst, bool high)
    {
        if (high)
        {
            _e.Instruction("LD", "L,H");
            _e.Instruction("LD", "H,$00");
        }
        else
        {
            _e.Instruction("LD", "H,$00");
        }
    }

    private void EmitArrayLoad(IrInstruction inst)
    {
        // HL = base[index]  (word array)
        _e.Comment($"array load: {inst.Dest} = {inst.Src1}[{inst.Src2}]");
        // index in HL, base address in DE
        _e.Instruction("PUSH", "HL"); // save index
        _e.Instruction("POP", "DE");
        _e.Instruction("EX", "DE,HL"); // HL = index, DE = base
        // Scale index * 2 for word array
        _e.Instruction("ADD", "HL,HL");
        _e.Instruction("ADD", "HL,DE");
        // Load value
        _e.Instruction("LD", "E,(HL)");
        _e.Instruction("INC", "HL");
        _e.Instruction("LD", "D,(HL)");
        _e.Instruction("EX", "DE,HL");
    }

    private void EmitArrayStore(IrInstruction inst)
    {
        // base[index] = value
        _e.Comment($"array store: {inst.Dest}[{inst.Src2}] = {inst.Src1}");
        // This is complex - need base, index, and value
        // Simplified: value in HL, push it, compute address, store
        _e.Instruction("PUSH", "HL"); // save value
        // TODO: proper address computation
        _e.Instruction("POP", "DE"); // DE = value
        _e.Instruction("LD", "(HL),E");
        _e.Instruction("INC", "HL");
        _e.Instruction("LD", "(HL),D");
    }

    private void EmitCall(IrInstruction inst)
    {
        var funcName = inst.Src1.Name ?? inst.Src1.ToString();
        _e.Instruction("CALL", funcName);
    }
}
