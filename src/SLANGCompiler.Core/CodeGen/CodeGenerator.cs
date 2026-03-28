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
    private readonly Runtime.RuntimeManager? _runtimeManager;
    private readonly Z80Emitter _e;
    private string _currentFuncExitLabel = "_EXIT";
    private int _currentFuncLocalSize;
    private readonly HashSet<string> _calledFunctions = new(StringComparer.OrdinalIgnoreCase);

    public CodeGenerator(IrModule module, Runtime.RuntimeManager? runtimeManager = null)
    {
        _module = module;
        _runtimeManager = runtimeManager;
        _e = new Z80Emitter();
    }

    public string Generate()
    {
        // ORG宣言
        if (_module.OrgAddress.HasValue)
        {
            _e.Instruction("ORG", $"${_module.OrgAddress.Value:X4}");
            _e.Blank();
        }

        // エントリポイント: IYワーク設定 → MAIN呼び出し → STOP
        _e.Comment("=== Entry Point ===");
        _e.Instruction("LD", "IY,__IYWORK");
        _e.Instruction("CALL", "MAIN");
        // STOP相当(S-OS: JP 0 or RET depending on environment)
        _e.Instruction("RET");
        _e.Blank();

        // Functions
        foreach (var func in _module.Functions)
        {
            EmitFunction(func);
            _e.Blank();
        }

        // String table
        if (_module.StringTable.Count > 0)
        {
            _e.Blank();
            _e.Comment("=== String Table ===");
            foreach (var (label, text) in _module.StringTable)
            {
                _e.Label(label);
                EmitStringData(text);
            }
        }

        // Global variable work area
        if (_module.GlobalVars.Count > 0)
        {
            _e.Blank();
            _e.Comment("=== Global Variables (Work Area) ===");

            // アドレス固定変数: EQUで定義
            foreach (var gv in _module.GlobalVars.Where(v => v.FixedAddress.HasValue))
            {
                _e.Raw($"{gv.AsmLabel}\tEQU\t${gv.FixedAddress!.Value:X4}");
            }

            // 通常変数: DS(Define Storage)で領域確保
            foreach (var gv in _module.GlobalVars.Where(v => !v.FixedAddress.HasValue))
            {
                if (gv.InitialData != null)
                {
                    // 初期値付き → コード領域に埋め込み
                    _e.Label(gv.AsmLabel);
                    var bytes = string.Join(",", gv.InitialData.Select(b => $"${b:X2}"));
                    _e.Raw($"\tDB\t{bytes}");
                }
                else
                {
                    // 初期値なし → ワーク領域にDS
                    _e.Label(gv.AsmLabel);
                    _e.Raw($"\tDS\t{gv.ByteSize}");
                }
            }
        }

        // WORK宣言がある場合
        if (_module.WorkAddress.HasValue)
        {
            _e.Blank();
            _e.Comment($"=== WORK at ${_module.WorkAddress.Value:X4} ===");
        }

        // ランタイム関数の結合（使用されたもののみ）
        if (_runtimeManager != null)
        {
            // ユーザー定義関数名を収集（ランタイムとの区別用）
            var userFuncs = new HashSet<string>(_module.Functions.Select(f => f.Name), StringComparer.OrdinalIgnoreCase);

            foreach (var name in _calledFunctions)
            {
                if (!userFuncs.Contains(name))
                    _runtimeManager.MarkUsed(name);
            }

            var usedRuntime = _runtimeManager.GetUsedFunctions().ToList();
            if (usedRuntime.Count > 0)
            {
                _e.Blank();
                _e.Comment("=== Runtime Functions ===");
                foreach (var func in usedRuntime)
                {
                    _e.Label(func.Name);
                    // ランタイムのコードをそのまま出力
                    foreach (var line in func.Code.Split('\n'))
                    {
                        if (!string.IsNullOrWhiteSpace(line))
                            _e.Raw(line);
                    }
                    _e.Blank();
                }
            }
        }

        // IYワーク領域 (256バイト)
        _e.Blank();
        _e.Comment("=== IY Work Area (256 bytes) ===");
        _e.Label("__IYWORK");
        _e.Raw("\tDS\t256");

        // プログラム末尾マーカー
        _e.Blank();
        _e.Label("SLANG_PROG_END");

        return _e.ToAssembly();
    }

    private void EmitStringData(string text)
    {
        // 文字列をDBバイト列として出力（末尾$00付き）
        var bytes = new List<string>();
        foreach (var ch in text)
        {
            bytes.Add($"${(int)ch:X2}");
        }
        bytes.Add("$00");
        _e.Raw($"\tDB\t{string.Join(",", bytes)}");
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
        _currentFunction = func;
        _e.Label(func.Name);

        foreach (var inst in func.Instructions)
        {
            EmitInstruction(inst);
        }
        _currentFunction = null;
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
            case IrOp.LoadLocal:
                EmitLoadLocal(inst);
                break;
            case IrOp.StoreLocal:
                EmitStoreLocal(inst);
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
            case IrOp.MemLoad: EmitMemLoad(inst); break;
            case IrOp.MemStore: EmitMemStore(inst); break;
            case IrOp.IndirLoad: EmitIndirLoad(inst); break;
            case IrOp.IndirStore: EmitIndirStore(inst); break;
            case IrOp.PortIn: EmitPortIn(inst); break;
            case IrOp.PortOut: EmitPortOut(inst); break;

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
                    _e.Comment($"return value in HL");
                }
                _e.Instruction("JP", _currentFuncExitLabel);
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
        var funcName = inst.Dest.Name ?? "UNKNOWN";
        _currentFuncExitLabel = $"_{funcName}_EXIT";

        // ローカル変数のサイズを事前計算するため、後続のStoreLocal命令を走査
        _currentFuncLocalSize = ComputeLocalSize(inst);

        _e.Comment($"function {funcName}");
        if (_currentFuncLocalSize > 0)
        {
            // 動的変数あり → IY退避＆調整
            _e.Instruction("PUSH", "IY");
            _e.Instruction("LD", $"BC,${_currentFuncLocalSize:X4}");
            _e.Instruction("ADD", "IY,BC");
        }
        else
        {
            // 動的変数なし → 引数だけならIYはそのまま
            _e.Instruction("PUSH", "IY");
        }
    }

    private void EmitFuncEnd()
    {
        _e.Label(_currentFuncExitLabel);
        _e.Instruction("POP", "IY");
        _e.Instruction("RET");
    }

    /// <summary>
    /// 関数内のローカル変数合計サイズを計算（StoreLocal命令のオフセットから推定）
    /// </summary>
    private int ComputeLocalSize(IrInstruction funcBeginInst)
    {
        // 現在の関数のIR命令を走査して、最小のIYオフセット（$70未満）を見つける
        if (_currentFunction == null) return 0;

        int minOffset = 0x70;
        foreach (var inst in _currentFunction.Instructions)
        {
            if (inst.Op == IrOp.StoreLocal || inst.Op == IrOp.LoadLocal)
            {
                var offset = inst.Op == IrOp.StoreLocal
                    ? (int)inst.Dest.ImmediateValue
                    : (int)inst.Src1.ImmediateValue;
                if (offset < 0x70 && offset < minOffset)
                    minOffset = offset;
            }
        }
        return 0x70 - minOffset;
    }

    // 現在処理中の関数IR
    private IrFunction? _currentFunction;

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

    private void EmitLoadLocal(IrInstruction inst)
    {
        int offset = (int)inst.Src1.ImmediateValue;
        if (inst.DataSize == 1)
        {
            // BYTE: (IY+offset) → L, H=0
            _e.Instruction("LD", $"L,(IY+${offset:X2})");
            _e.Instruction("LD", "H,$00");
        }
        else
        {
            // WORD: (IY+offset) → L, (IY+offset+1) → H
            _e.Instruction("LD", $"L,(IY+${offset:X2})");
            _e.Instruction("LD", $"H,(IY+${offset + 1:X2})");
        }
    }

    private void EmitStoreLocal(IrInstruction inst)
    {
        int offset = (int)inst.Dest.ImmediateValue;
        if (inst.DataSize == 1)
        {
            // BYTE: L → (IY+offset)
            _e.Instruction("LD", $"(IY+${offset:X2}),L");
        }
        else
        {
            // WORD: L → (IY+offset), H → (IY+offset+1)
            _e.Instruction("LD", $"(IY+${offset:X2}),L");
            _e.Instruction("LD", $"(IY+${offset + 1:X2}),H");
        }
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

    // ==== Array Access Z80 Code Generation ====
    //
    // 元実装に準拠したZ80パターン:
    //   WORD配列ロード: base + index*2 → LD E,(HL) / INC HL / LD D,(HL) / EX DE,HL
    //   BYTE配列ロード: base + index*1 → LD L,(HL) / LD H,0
    //   スケーリング: ×1=nop, ×2=ADD HL,HL, ×4=ADD HL,HL×2

    private void EmitArrayLoad(IrInstruction inst)
    {
        // Src1=base(スタック上), Src2=index(HL), DataSize=要素サイズ
        bool isByte = inst.DataSize == 1;

        _e.Comment($"array load [{(isByte ? "BYTE" : "WORD")}]");
        // 現在: HL=index, スタック上にbase
        // index * scale
        if (!isByte)
            _e.Instruction("ADD", "HL,HL"); // ×2 for WORD

        // base + scaled_index
        _e.Instruction("POP", "DE");  // DE = base address
        _e.Instruction("ADD", "HL,DE"); // HL = final address

        // Dereference
        if (isByte)
        {
            _e.Instruction("LD", "L,(HL)");
            _e.Instruction("LD", "H,$00");
        }
        else
        {
            _e.Instruction("LD", "E,(HL)");
            _e.Instruction("INC", "HL");
            _e.Instruction("LD", "D,(HL)");
            _e.Instruction("EX", "DE,HL");
        }
    }

    private void EmitArrayStore(IrInstruction inst)
    {
        // Dest=base, Src1=value(HL), Src2=index
        // 呼ばれる時点: HL=index (最後に評価された値)
        // その前にvalue, baseがスタック上にある
        bool isByte = inst.DataSize == 1;

        _e.Comment($"array store [{(isByte ? "BYTE" : "WORD")}]");

        // HL=index → scale
        if (!isByte)
            _e.Instruction("ADD", "HL,HL");

        // base + scaled_index
        _e.Instruction("POP", "DE"); // DE = base
        _e.Instruction("ADD", "HL,DE"); // HL = address

        // value → store
        _e.Instruction("POP", "DE"); // DE = value
        if (isByte)
        {
            _e.Instruction("LD", "(HL),E");
        }
        else
        {
            _e.Instruction("LD", "(HL),E");
            _e.Instruction("INC", "HL");
            _e.Instruction("LD", "(HL),D");
        }
    }

    // ==== MEM/MEMW Direct Memory Access ====

    private void EmitMemLoad(IrInstruction inst)
    {
        // HL = address, DataSize = 1(MEM) or 2(MEMW)
        bool isByte = inst.DataSize == 1;

        if (isByte)
        {
            // MEM[addr]: LD L,(HL) / LD H,0
            _e.Instruction("LD", "L,(HL)");
            _e.Instruction("LD", "H,$00");
        }
        else
        {
            // MEMW[addr]: LD E,(HL) / INC HL / LD D,(HL) / EX DE,HL
            _e.Instruction("LD", "E,(HL)");
            _e.Instruction("INC", "HL");
            _e.Instruction("LD", "D,(HL)");
            _e.Instruction("EX", "DE,HL");
        }
    }

    private void EmitMemStore(IrInstruction inst)
    {
        // Dest=addr(was in HL, now stack), Src1=value(HL)
        bool isByte = inst.DataSize == 1;

        _e.Instruction("POP", "DE"); // DE = addr (pushed before value)
        _e.Instruction("EX", "DE,HL"); // HL = addr, DE = value

        // 実際にはaddr→HL, value→DEの順序は呼び出しパターンに依存
        // TODO: 正確なスタック順序を確認

        if (isByte)
        {
            _e.Instruction("LD", "(HL),E");
        }
        else
        {
            _e.Instruction("LD", "(HL),E");
            _e.Instruction("INC", "HL");
            _e.Instruction("LD", "(HL),D");
        }
    }

    // ==== Indirect Variable Dereference ====

    private void EmitIndirLoad(IrInstruction inst)
    {
        // HL = *HL
        bool isByte = inst.DataSize == 1;
        if (isByte)
        {
            _e.Instruction("LD", "L,(HL)");
            _e.Instruction("LD", "H,$00");
        }
        else
        {
            _e.Instruction("LD", "E,(HL)");
            _e.Instruction("INC", "HL");
            _e.Instruction("LD", "D,(HL)");
            _e.Instruction("EX", "DE,HL");
        }
    }

    private void EmitIndirStore(IrInstruction inst)
    {
        // *HL = DE
        bool isByte = inst.DataSize == 1;
        _e.Instruction("POP", "DE");
        _e.Instruction("EX", "DE,HL");
        if (isByte)
        {
            _e.Instruction("LD", "(HL),E");
        }
        else
        {
            _e.Instruction("LD", "(HL),E");
            _e.Instruction("INC", "HL");
            _e.Instruction("LD", "(HL),D");
        }
    }

    // ==== PORT I/O ====

    private void EmitPortIn(IrInstruction inst)
    {
        // HL = port address → read from port
        bool isByte = inst.DataSize == 1;
        _e.Instruction("LD", "B,H");
        _e.Instruction("LD", "C,L");
        if (isByte)
        {
            _e.Instruction("IN", "L,(C)");
            _e.Instruction("LD", "H,$00");
        }
        else
        {
            _e.Instruction("IN", "L,(C)");
            _e.Instruction("INC", "BC");
            _e.Instruction("IN", "H,(C)");
        }
    }

    private void EmitPortOut(IrInstruction inst)
    {
        // HL = port address, DE = value
        bool isByte = inst.DataSize == 1;
        _e.Instruction("POP", "DE"); // value
        _e.Instruction("EX", "DE,HL");
        _e.Instruction("LD", "B,H");
        _e.Instruction("LD", "C,L");
        _e.Instruction("EX", "DE,HL");
        if (isByte)
        {
            _e.Instruction("OUT", "(C),L");
        }
        else
        {
            _e.Instruction("OUT", "(C),L");
            _e.Instruction("INC", "BC");
            _e.Instruction("OUT", "(C),H");
        }
    }

    private void EmitCall(IrInstruction inst)
    {
        var funcName = inst.Src1.Name ?? inst.Src1.ToString();
        _calledFunctions.Add(funcName);
        int machineArgs = (int)inst.Src2.ImmediateValue;

        if (machineArgs > 0)
        {
            // MACHINE関数: スタック上の引数をレジスタに移す
            // 引数はPushArgで逆順にスタックに積まれている
            // 仕様: 1個→HL, 2個→HL,DE, 3個→HL,DE,BC
            switch (machineArgs)
            {
                case 1:
                    // HLに既に入っている（最後のPushArgの値）
                    _e.Instruction("POP", "HL");
                    break;
                case 2:
                    _e.Instruction("POP", "DE");
                    _e.Instruction("POP", "HL");
                    break;
                case 3:
                    _e.Instruction("POP", "BC");
                    _e.Instruction("POP", "DE");
                    _e.Instruction("POP", "HL");
                    break;
                default:
                    // 4個以上: スタック渡し（そのまま）
                    break;
            }
        }

        _e.Instruction("CALL", funcName);
    }
}
