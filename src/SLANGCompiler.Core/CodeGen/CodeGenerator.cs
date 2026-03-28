using SLANGCompiler.IR;
using SLANGCompiler.Parser.Ast;
using SLANGCompiler.Semantics;

namespace SLANGCompiler.CodeGen;

/// <summary>
/// IR → Z80 アセンブリのコード生成器（骨格）
/// </summary>
public class CodeGenerator
{
    private readonly IrModule _module;
    private readonly Z80Emitter _emitter;

    public CodeGenerator(IrModule module)
    {
        _module = module;
        _emitter = new Z80Emitter();
    }

    public string Generate()
    {
        // Global data
        foreach (var data in _module.GlobalData)
        {
            EmitInstruction(data);
        }

        // Functions
        foreach (var func in _module.Functions)
        {
            EmitFunction(func);
        }

        return _emitter.ToAssembly();
    }

    private void EmitFunction(IrFunction func)
    {
        _emitter.Label(func.Name);

        foreach (var inst in func.Instructions)
        {
            EmitInstruction(inst);
        }
    }

    private void EmitInstruction(IrInstruction inst)
    {
        switch (inst.Op)
        {
            case IrOp.Label:
                _emitter.Label(inst.Dest.Name ?? "");
                break;

            case IrOp.LoadConst:
                EmitLoadConst(inst);
                break;

            case IrOp.Add:
                EmitBinaryOp(inst, "ADD");
                break;

            case IrOp.Sub:
                EmitBinaryOp(inst, "SUB");
                break;

            case IrOp.Jump:
                _emitter.Instruction("JP", inst.Dest.Name);
                break;

            case IrOp.JumpIfZero:
                _emitter.Instruction("JP", $"Z,{inst.Dest.Name}");
                break;

            case IrOp.JumpIfNonZero:
                _emitter.Instruction("JP", $"NZ,{inst.Dest.Name}");
                break;

            case IrOp.Call:
                _emitter.Instruction("CALL", inst.Dest.Name);
                break;

            case IrOp.Return:
                _emitter.Instruction("RET");
                break;

            case IrOp.InlineAsm:
                if (inst.Dest.Name != null)
                    _emitter.Raw(inst.Dest.Name);
                break;

            case IrOp.Comment:
                _emitter.Comment(inst.Dest.Name ?? "");
                break;

            case IrOp.Nop:
                break;

            default:
                _emitter.Comment($"TODO: {inst}");
                break;
        }
    }

    private void EmitLoadConst(IrInstruction inst)
    {
        var value = inst.Src1.ImmediateValue;
        if (inst.DataSize == 1)
        {
            _emitter.Instruction("LD", $"A,${value & 0xFF:X2}");
        }
        else
        {
            _emitter.Instruction("LD", $"HL,${value & 0xFFFF:X4}");
        }
    }

    private void EmitBinaryOp(IrInstruction inst, string mnemonic)
    {
        // Placeholder: will need register allocation
        _emitter.Comment($"{mnemonic} t{inst.Dest.TempIndex} = t{inst.Src1.TempIndex} op t{inst.Src2.TempIndex}");
    }
}
