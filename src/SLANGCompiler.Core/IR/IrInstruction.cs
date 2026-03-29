namespace SLANGCompiler.IR;

/// <summary>
/// IR（中間表現）のオペコード。
/// Z80の詳細に依存しない抽象的な操作を定義。
/// </summary>
public enum IrOp
{
    // -- Data movement --
    LoadConst,          // dest = immediate value
    LoadVar,            // dest = variable (by symbol, global)
    StoreVar,           // variable = src (global)
    LoadLocal,          // dest = (IY+offset), offset in Src1.ImmediateValue
    StoreLocal,         // (IY+offset) = src, offset in Dest.ImmediateValue
    LoadAddr,           // dest = address of variable
    LoadIndirect,       // dest = mem[src]
    StoreIndirect,      // mem[dest] = src

    // -- Arithmetic --
    Add,
    Sub,
    Mul,
    Div,
    Mod,
    SMul,               // signed multiply
    SDiv,               // signed divide
    SMod,               // signed modulo
    Neg,                // unary negate

    // -- Bitwise --
    And,
    Or,
    Xor,
    Not,                // bitwise complement
    Shl,
    Shr,
    SShl,               // signed/arithmetic shift left
    SShr,               // signed/arithmetic shift right

    // -- Comparison (result: 0 or 1) --
    CmpEq,
    CmpNeq,
    CmpLt,
    CmpGt,
    CmpLe,
    CmpGe,
    CmpSLt,            // signed
    CmpSGt,
    CmpSLe,
    CmpSGe,

    // -- Logical --
    LogAnd,
    LogOr,
    LogNot,

    // -- High/Low byte --
    High,
    Low,

    // -- Array access --
    // ArrayLoad: dest = *(base + index * stride)
    //   Dest=result, Src1=base, Src2=index, DataSize=element size, stride in ImmediateValue of Src2 if needed
    ArrayLoad,
    // ArrayStore: *(base + index * stride) = value
    //   Dest=base, Src1=value, Src2=index
    ArrayStore,

    // -- Memory direct access (MEM/MEMW) --
    MemLoad,            // dest = MEM[addr] or MEMW[addr]
    MemStore,           // MEM[addr] = value or MEMW[addr] = value

    // -- Indirect variable access --
    IndirLoad,          // dest = *ptr (dereference pointer)
    IndirStore,         // *ptr = value

    // -- Port I/O --
    PortIn,
    PortOut,

    // -- Control flow --
    Label,
    Jump,
    JumpIfZero,         // conditional jump (if src == 0)
    JumpIfNonZero,      // conditional jump (if src != 0)
    Call,
    Return,

    // -- Function --
    FuncBegin,
    FuncEnd,
    PushArg,            // push argument for function call
    PopResult,          // pop return value

    // -- Stack --
    Push,
    Pop,

    // -- Inline assembly --
    InlineAsm,

    // -- Special --
    Nop,
    Comment,

    // -- Data definition --
    DefByte,            // DB
    DefWord,            // DW
    DefString,          // DB "string", 0
}

/// <summary>
/// IRオペランドの種類
/// </summary>
public enum IrOperandKind
{
    None,
    Immediate,          // 即値
    Symbol,             // シンボル参照
    Temp,               // 一時変数 (t0, t1, ...)
    Label,              // ラベル参照
    AsmString,          // インラインアセンブリ文字列
}

/// <summary>
/// IRオペランド
/// </summary>
public record struct IrOperand(IrOperandKind Kind, long ImmediateValue = 0, string? Name = null, int TempIndex = -1)
{
    public static readonly IrOperand None = new(IrOperandKind.None);
    public static IrOperand Imm(long value) => new(IrOperandKind.Immediate, ImmediateValue: value);
    public static IrOperand Sym(string name) => new(IrOperandKind.Symbol, Name: name);
    public static IrOperand Temp(int index) => new(IrOperandKind.Temp, TempIndex: index);
    public static IrOperand Lbl(string name) => new(IrOperandKind.Label, Name: name);
    public static IrOperand Asm(string text) => new(IrOperandKind.AsmString, Name: text);

    public override string ToString() => Kind switch
    {
        IrOperandKind.None => "_",
        IrOperandKind.Immediate => $"#{ImmediateValue}",
        IrOperandKind.Symbol => $"@{Name}",
        IrOperandKind.Temp => $"t{TempIndex}",
        IrOperandKind.Label => $"L{Name}",
        IrOperandKind.AsmString => $"asm\"{Name}\"",
        _ => "?",
    };
}

/// <summary>
/// IR命令
/// </summary>
public class IrInstruction
{
    public IrOp Op { get; set; }
    public IrOperand Dest { get; set; }
    public IrOperand Src1 { get; set; }
    public IrOperand Src2 { get; set; }
    public int DataSize { get; set; } = 2;   // 1=byte, 2=word, 4=float

    public IrInstruction(IrOp op, IrOperand dest = default, IrOperand src1 = default, IrOperand src2 = default)
    {
        Op = op;
        Dest = dest;
        Src1 = src1;
        Src2 = src2;
    }

    public override string ToString()
    {
        var parts = new List<string> { Op.ToString() };
        if (Dest.Kind != IrOperandKind.None) parts.Add(Dest.ToString());
        if (Src1.Kind != IrOperandKind.None) parts.Add(Src1.ToString());
        if (Src2.Kind != IrOperandKind.None) parts.Add(Src2.ToString());
        return string.Join(" ", parts);
    }
}

/// <summary>
/// IR関数単位
/// </summary>
public class IrFunction
{
    public string Name { get; set; } = "";
    public List<IrInstruction> Instructions { get; } = new();
    public int TempCount { get; set; }
    public int LocalSize { get; set; }

    public int AllocTemp() => TempCount++;

    public void Emit(IrOp op, IrOperand dest = default, IrOperand src1 = default, IrOperand src2 = default)
    {
        Instructions.Add(new IrInstruction(op, dest, src1, src2));
    }

    public override string ToString()
    {
        var lines = Instructions.Select((inst, i) => $"  {i:D4}: {inst}");
        return $"func {Name}:\n{string.Join("\n", lines)}";
    }
}

/// <summary>
/// IRモジュール（コンパイル単位全体）
/// </summary>
public class IrModule
{
    public List<IrFunction> Functions { get; } = new();
    public List<IrInstruction> GlobalData { get; } = new();
    public Dictionary<string, string> StringTable { get; } = new();
    public List<GlobalVarInfo> GlobalVars { get; } = new();
    public int? OrgAddress { get; set; }
    public int? WorkAddress { get; set; }
    public int? OffsetAddress { get; set; }
    public List<OverlayModule> Overlays { get; } = new();
    public HashSet<string> AddressSymbolDeps { get; } = new(StringComparer.OrdinalIgnoreCase);

    public override string ToString()
    {
        var parts = new List<string>();
        if (GlobalData.Count > 0)
        {
            parts.Add(".data:");
            parts.AddRange(GlobalData.Select(d => $"  {d}"));
        }
        parts.AddRange(Functions.Select(f => f.ToString()));
        return string.Join("\n", parts);
    }
}

/// <summary>
/// グローバル変数情報（ワークエリア生成用）
/// </summary>
public enum VarStorageKind { Bss, InitArray, CodeConst }

/// <summary>初期化データの1要素: バイト値 or アセンブラ式DW</summary>
public record struct InitItem(byte? ByteValue, string? AsmExpr)
{
    public static InitItem Byte(byte v) => new(v, null);
    public static InitItem Word(string expr) => new(null, expr);
    public int ByteSize => AsmExpr != null ? 2 : 1;
}

public class GlobalVarInfo
{
    public string Name { get; set; } = "";
    public string AsmLabel { get; set; } = "";
    public int ByteSize { get; set; } = 2;
    public int? FixedAddress { get; set; }
    public List<InitItem>? InitialItems { get; set; }
    public bool HasInitializer => InitialItems != null && InitialItems.Count > 0;
    public bool IsArray { get; set; }
    public VarStorageKind StorageKind { get; set; } = VarStorageKind.Bss;
}

/// <summary>
/// オーバーレイモジュール: 別アドレス空間に配置されるコードブロック。
/// メイン部とシンボルテーブルを共有するが、別ASMファイルとして出力される。
/// </summary>
public class OverlayModule
{
    public int Index { get; set; }
    public int OrgAddress { get; set; }
    public List<IrFunction> Functions { get; } = new();
    public List<GlobalVarInfo> LocalVars { get; } = new();
    public Dictionary<string, string> StringTable { get; } = new();
}
