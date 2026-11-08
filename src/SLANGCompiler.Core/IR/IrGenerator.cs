using SLANGCompiler.Lexer;
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
    private readonly Runtime.RuntimeManager? _runtimeManager;
    private readonly IrModule _module = new();
    private IrFunction? _currentFunction;
    private int _labelCount;
    private bool _inStaticDecl;
    private string? _currentFuncName;
    // 関数の中の CONST (名前 → 記号)。宣言の順に入れる。関数の外では null
    private Dictionary<string, Symbol>? _localConsts;
    private FuncInfo? _currentFuncInfo;
    // 関数の頭の局所宣言の初期値 (引数のオフセットが決まってから出す)。関数の頭の局所宣言を訪ねる間だけ null でない
    private List<Action>? _deferredLocalInits;

    private void EmitLocalInit(Action emit)
    {
        if (_deferredLocalInits == null)
        {
            emit();
            return;
        }
        // 後で出すときも、名前は宣言の所で見えていたものだけで引く (後ろで宣言する局所変数は隠す。
        // VAR X = BASE; VAR BASE = 9; の BASE は、大域の BASE)。引数は初めから見えている (オフセットは決まった後のもの)
        var visible = new HashSet<string>(_localVars!.Keys, _localVars.Comparer);
        _deferredLocalInits.Add(() =>
        {
            var later = _localVars!.Where(kv => !visible.Contains(kv.Key)).ToList();
            foreach (var kv in later) _localVars.Remove(kv.Key);
            try { emit(); }
            finally { foreach (var kv in later) _localVars[kv.Key] = kv.Value; }
        });
    }

    // 次に訪ねる式の値を使わない (式の文・カンマ式の左など)。受け取る式 (TakesDiscard) だけが読んで消す
    private bool _valueDiscarded;

    /// <summary>値を使わない印を受け取る式 (代入・増減・カンマ式と、値をそのまま返す単項の +・キャスト)</summary>
    private static bool TakesDiscard(Expression e) => e is IncrementExpr or CommaExpr or AssignExpr or CompoundAssignExpr
        or UnaryExpr { Op: UnaryOp.Plus } or CastExpr;

    private bool TakeDiscard()
    {
        bool d = _valueDiscarded;
        _valueDiscarded = false;
        return d;
    }

    /// <summary>
    /// 書いた値を式の値として使うとき: 書き込みの後で、値を HL (FLOAT は AHL) に置き直す。メモリへの書き込みの後は
    /// HL が番地なので、コード生成が EX DE,HL を出す。結果は 1 つの新しい値なので、PRINT・関数の引数など、
    /// どこで読んでも積むかどうかの判断がふつうに働く
    /// </summary>
    private IrOperand ValueAfterStore(IrOperand stored)
    {
        if (stored.Kind != IrOperandKind.Temp) return stored;
        // StoreVar・StoreLocal は値を取り出さず HL を壊さないので、書いた値をそのまま使う (積む位置もいままでどおり)。
        // 値を取り出して書く (メモリ・PORT への書き込み) ときだけ置き直す
        var list = _emitToGlobalData || _currentFunction == null ? _module.GlobalData : _currentFunction.Instructions;
        if (list.Count == 0 || list[^1].Op is not (IrOp.IndirStore or IrOp.ArrayStore or IrOp.MemStore or IrOp.PortOut))
            return stored;
        int ds = _tempDataSize.TryGetValue(stored.TempIndex, out int d) ? d : 2;
        var t = IrOperand.Temp(AllocTemp());
        Emit(IrOp.StoredValue, t, stored, dataSize: ds);
        if (ds == 3) _tempDataSize[t.TempIndex] = 3;
        return t;
    }

    /// <summary>関数の中の CONST も引く ConstEvaluator</summary>
    private ConstEvaluator NewConstEvaluator() => new(_globalSymbols, _localConsts);

    // 式に関数の中の CONST の名前があるか (大域の名前と同じ名前は意味解析で拒否している)
    private bool UsesLocalConst(Expression expr)
    {
        if (_localConsts == null) return false;
        bool uses = false;
        SemanticAnalyzer.ForEachName(expr, (name, _) => { if (_localConsts.ContainsKey(name)) uses = true; });
        return uses;
    }

    /// <summary>名前の記号: 関数の中の CONST を先に、なければ大域</summary>
    private Symbol? ResolveConstOrGlobal(string name)
        => _localConsts != null && _localConsts.TryGetValue(name, out var c) ? c : _globalSymbols?.Resolve(name);
    private bool _emitToGlobalData;
    private int _currentReturnDataSize = 2;  // 現在処理中の関数の戻り値型 (RETURN/END(expr) で参照)

    // 関数内ローカルシンボル（IrGenerator自身が管理）
    private Dictionary<string, LocalVarInfo>? _localVars;
    // 関数内静的変数のAsmLabelマップ（ソース名→__FUNC_VAR形式）
    private Dictionary<string, string>? _staticVarLabels;
    // 関数内静的変数のdataSizeマップ（BYTE変数のFOR/代入対応）
    private Dictionary<string, int>? _staticVarSizes;
    // 関数内静的配列の要素サイズマップ（BYTE配列のインデックスアクセス用）
    private Dictionary<string, int>? _staticElemSizes;
    // 関数内静的変数の種別マップ（Scalar/Array/Pointer）
    private Dictionary<string, VarKind>? _staticVarKinds;

    // #MODULE 内で IR を生成中のオーバーレイモジュール (null = メイン部)。
    // モジュール private な VAR/ARRAY は ここの LocalVars に振り分けられ、
    // ResolveAsmLabel / ResolveVarInfo は main global より先にここを引く。
    private OverlayModule? _currentOverlay;
    // overlay.LocalVars の name → info 逆引き（ResolveVarInfo の O(1) 化）
    private Dictionary<string, GlobalVarInfo>? _currentOverlayLookup;

    public IrGenerator(DiagnosticBag diagnostics, SymbolTable? symbols = null,
                       Runtime.RuntimeManager? runtimeManager = null)
    {
        _diagnostics = diagnostics;
        _globalSymbols = symbols;
        _runtimeManager = runtimeManager;
    }

    /// <summary>シンボル名からASMラベルを解決。関数内静的変数→overlay private→グローバルシンボル→デフォルトの順。</summary>
    private string ResolveAsmLabel(string name)
    {
        // 関数内静的変数（__FUNC_VAR形式）
        if (_staticVarLabels != null && _staticVarLabels.TryGetValue(name, out var staticLabel))
            return staticLabel;
        // オーバーレイ private 変数 (`_V_M<n>_<name>`)
        if (_currentOverlayLookup != null && _currentOverlayLookup.TryGetValue(name, out var ovInfo))
            return ovInfo.AsmLabel;
        // グローバルシンボルテーブル
        var sym = _globalSymbols?.Resolve(name);
        return sym?.AsmLabel ?? LabelUtils.UserVarLabel(name);
    }

    /// <summary>変数名から統合情報を解決。優先順: local → static → overlay private → global。</summary>
    private VarInfo ResolveVarInfo(string name)
    {
        // 1. ローカル変数
        if (_localVars?.TryGetValue(name, out var li) == true)
        {
            int varDs = li.ByteSize;
            int elemSz = li.Kind == VarKind.Scalar ? varDs : li.ElemSize;
            return new VarInfo { IsResolved = true, Kind = li.Kind, ElemSize = elemSz, VarDataSize = varDs, Local = li };
        }
        // 2. 関数内static変数
        if (_staticVarSizes?.ContainsKey(name) == true)
        {
            var kind = (_staticVarKinds?.TryGetValue(name, out var k) == true) ? k : VarKind.Scalar;
            int varDs = _staticVarSizes[name];
            int elemSz = kind == VarKind.Scalar ? varDs
                       : (_staticElemSizes?.TryGetValue(name, out int e) == true ? e : 2);
            return new VarInfo { IsResolved = true, Kind = kind, ElemSize = elemSz, VarDataSize = varDs };
        }
        // 3. overlay private 変数 (#MODULE 内のプライベート VAR/ARRAY)
        if (_currentOverlayLookup != null && _currentOverlayLookup.TryGetValue(name, out var ov))
        {
            var kind = ov.VarKind switch
            {
                GlobalVarKind.Pointer => VarKind.Pointer,
                GlobalVarKind.Array   => VarKind.Array,
                _                     => VarKind.Scalar,
            };
            int varDs = ov.VarKind == GlobalVarKind.Scalar ? ov.ByteSize : 2;
            int elemSz = kind == VarKind.Scalar ? varDs : ov.ElemSize;
            return new VarInfo { IsResolved = true, Kind = kind, ElemSize = elemSz, VarDataSize = varDs };
        }
        // 4. グローバルシンボル
        var sym = _globalSymbols?.Resolve(name);
        if (sym != null)
        {
            VarKind kind = sym.Type is PointerType && !sym.IsArrayDecl ? VarKind.Pointer
                         : sym.Type is ArrayType || sym.IsArrayDecl     ? VarKind.Array
                         :                                                 VarKind.Scalar;
            int varDs = sym.Type?.ByteSize ?? 2;
            int elemSz;
            if (kind == VarKind.Scalar)
                elemSz = varDs;
            else if (sym.Type is ArrayType at)
                elemSz = at.ElementType.ByteSize;  // BYTE=1 / WORD=2 / FLOAT=3
            else if (sym.Type is PointerType pt)
                elemSz = pt.ElementType.ByteSize;  // BYTE=1 / WORD=2 / FLOAT=3
            else
                elemSz = 2;
            return new VarInfo { IsResolved = true, Kind = kind, ElemSize = elemSz, VarDataSize = varDs, GlobalSym = sym };
        }
        // 5. 未解決
        return new VarInfo { IsResolved = false, Kind = VarKind.Scalar, ElemSize = 2, VarDataSize = 2 };
    }

    /// <summary>未定義変数チェック付きResolveVarInfo。未定義ならエラーを出しつつVarInfoを返す。</summary>
    private VarInfo CheckDefined(string name, SourceSpan span)
    {
        var vi = ResolveVarInfo(name);
        if (!vi.IsResolved)
            _diagnostics?.Error($"Undefined variable: {name}", span);
        return vi;
    }

    private enum VarKind { Scalar, Array, Pointer }

    private record LocalVarInfo(int Offset, int ByteSize, VarKind Kind = VarKind.Scalar, bool IsByte = false, List<int>? Dims = null, int ElemSize = 2)
    {
        public bool IsArray => Kind == VarKind.Array;
        public bool IsPointer => Kind == VarKind.Pointer;
    }

    /// <summary>変数/配列の統合情報（3つのソースを優先順に解決）。</summary>
    private readonly record struct VarInfo
    {
        public bool IsResolved { get; init; }
        public VarKind Kind { get; init; }
        /// <summary>Array/Pointerでは要素幅(1, 2 or 3)、ScalarではVarDataSizeと同値。</summary>
        public int ElemSize { get; init; }
        /// <summary>変数自体のサイズ (1/2/3)。</summary>
        public int VarDataSize { get; init; }
        public LocalVarInfo? Local { get; init; }
        public Symbol? GlobalSym { get; init; }
    }

    private int _localOffset;

    // 各tempのデータサイズを追跡（FLOAT判定用）
    private readonly Dictionary<int, int> _tempDataSize = new();

    /// <summary>idx を要素サイズ倍に拡大した temp を返す (間接変数経路の共通化)。
    /// elemSize=1 はそのまま、=2 は ADD HL,HL、それ以外は LoadConst+Mul。</summary>
    private IrOperand ScaleIndexByElemSize(IrOperand idx, int elemSize)
    {
        if (elemSize == 1) return idx;
        var scaled = IrOperand.Temp(AllocTemp());
        if (elemSize == 2)
        {
            Emit(IrOp.Add, scaled, idx, idx);
        }
        else
        {
            var c = IrOperand.Temp(AllocTemp());
            Emit(IrOp.LoadConst, c, IrOperand.Imm(elemSize));
            Emit(IrOp.Mul, scaled, idx, c);
        }
        return scaled;
    }

    public IrModule Generate(CompilationUnit unit)
    {
        CheckFunctionLabelCollisions();
        unit.Accept(this);
        return _module;
    }

    /// <summary>
    /// 予約語と同じ名前の関数は、ラベルに <c>_F_</c> を付ける (<see cref="LabelUtils.FunctionLabel"/>)。
    /// その付け替えたラベルが、ほかの関数・MACHINE のラベルや CONST ASM の名前と同じならエラーにする
    /// (アセンブラのラベルの重複になるため。Z80 の出力だけの話なので、IR を作る所で見る)
    /// </summary>
    private void CheckFunctionLabelCollisions()
    {
        if (_globalSymbols == null) return;
        var symbols = _globalSymbols.GlobalScope.Symbols.Values;
        foreach (var f in symbols)
        {
            if (f.Kind != SymbolKind.Function || f.AsmLabel == null) continue;
            if (f.AsmLabel == LabelUtils.SanitizeLabel(f.Name)) continue;   // 付け替えていない
            foreach (var o in symbols)
            {
                if (ReferenceEquals(o, f)) continue;
                // MACHINE はラベルを出すのは CODE 本体のあるものだけ (アドレスだけのものは $xxxx を呼ぶ)
                string? other = o.Kind switch
                {
                    SymbolKind.Function => o.AsmLabel,
                    SymbolKind.MachineFunction when o.HasCodeBody => o.AsmLabel,
                    SymbolKind.Constant when o.IsAsmEqu => o.Name,
                    _ => null,
                };
                if (other != null && string.Equals(other, f.AsmLabel, StringComparison.OrdinalIgnoreCase))
                    _diagnostics.Error(
                        $"function '{f.Name}' becomes label '{f.AsmLabel}' (the name is reserved by the assembler), "
                        + $"which is already used by '{o.Name}'", default);
            }
        }
    }

    private int AllocTemp() => _currentFunction?.AllocTemp() ?? 0;
    private string NewLabel() => $"_L{_labelCount++}";

    private void Emit(IrOp op, IrOperand dest = default, IrOperand src1 = default, IrOperand src2 = default, int dataSize = 2)
    {
        var inst = new IrInstruction(op, dest, src1, src2) { DataSize = dataSize };
        CheckFloatOperands(inst);
        if (_emitToGlobalData || _currentFunction == null)
            _module.GlobalData.Add(inst);
        else
            _currentFunction.Instructions.Add(inst);
    }

    // いま作っている文の場所 (Emit の FLOAT の検査のエラーの場所)
    private SourceSpan _stmtSpan;

    /// <summary>
    /// 命令が読む値の型を見る。値として読むオペランドは、命令の大きさが 3 (FLOAT) かと、値が FLOAT かが同じであること
    /// (違えば変換が抜けている)。番地・添字として読むオペランドは整数であること。FLOAT を扱えない命令 (MOD・ビット演算・
    /// シフト・符号付きの比較・条件など) は FLOAT の値を読まないこと。書き込みの番地は Dest にある
    /// </summary>
    private void CheckFloatOperands(IrInstruction inst)
    {
        bool fl = inst.DataSize == 3;
        bool bad = false;
        void Value(IrOperand o) { if (o.Kind == IrOperandKind.Temp && (TempDataSize(o) == 3) != fl) bad = true; }
        void Int(IrOperand o) { if (o.Kind == IrOperandKind.Temp && TempDataSize(o) == 3) bad = true; }
        switch (inst.Op)
        {
            case IrOp.StoreVar or IrOp.StoreLocal: Value(inst.Src1); break;
            case IrOp.ArrayStore: Value(inst.Src1); Int(inst.Dest); Int(inst.Src2); break;
            case IrOp.IndirStore or IrOp.MemStore or IrOp.PortOut: Value(inst.Src1); Int(inst.Dest); break;
            case IrOp.ArrayLoad: Int(inst.Src1); Int(inst.Src2); break;
            case IrOp.IndirLoad or IrOp.MemLoad or IrOp.PortIn or IrOp.LoadIndirect: Int(inst.Src1); break;
            case IrOp.Add or IrOp.Sub or IrOp.Mul or IrOp.Div or IrOp.SMul or IrOp.SDiv or IrOp.Neg
                or IrOp.CmpEq or IrOp.CmpNeq or IrOp.CmpLt or IrOp.CmpGt or IrOp.CmpLe or IrOp.CmpGe
                or IrOp.JoinValue or IrOp.StoredValue:
                Value(inst.Src1); Value(inst.Src2); break;
            case IrOp.Mod or IrOp.SMod or IrOp.And or IrOp.Or or IrOp.Xor or IrOp.Not or IrOp.Shl or IrOp.Shr
                or IrOp.SShl or IrOp.SShr or IrOp.CmpSLt or IrOp.CmpSGt or IrOp.CmpSLe or IrOp.CmpSGe
                or IrOp.High or IrOp.Low or IrOp.LogAnd or IrOp.LogOr or IrOp.LogNot
                or IrOp.JumpIfZero or IrOp.JumpIfNonZero:
                bad = fl;
                Int(inst.Src1); Int(inst.Src2);
                break;
        }
        if (!bad) return;
        _diagnostics.Error(inst.Op is IrOp.JumpIfZero or IrOp.JumpIfNonZero or IrOp.LogAnd or IrOp.LogOr or IrOp.LogNot
            ? "A FLOAT value cannot be used as a condition (compare it, e.g. F <> 0.0)"
            : "A FLOAT value cannot be used in this operation, index or address (use FTOI to make it an integer)", _stmtSpan);
    }

    /// <summary>HL の値 (input) を変える呼び出し (i16tof24・FTOI)。読む値を ImplicitSrc に覚える (コード生成が「後で読むか」を判断するため)</summary>
    private void EmitConversionCall(IrOperand conv, string func, IrOperand input, int dataSize = 2)
    {
        Emit(IrOp.Call, conv, IrOperand.Sym(func), IrOperand.Imm(0), dataSize: dataSize);
        var list = _emitToGlobalData || _currentFunction == null ? _module.GlobalData : _currentFunction.Instructions;
        list[^1].ImplicitSrc = input;
    }

    /// <summary>
    /// MPRNT方式のインライン文字列出力: CALL MPRNT + DB "text",0
    /// 旧コンパイラ互換（LD HL,_Sx不要で3バイト節約/呼び出し）
    /// </summary>
    private void EmitInlinePrint(string text)
    {
        var dbArgs = StringEncoder.ToAsmDbArgs(text, _diagnostics);
        Emit(IrOp.InlineAsm, IrOperand.Asm($"\tCALL\tMPRNT\n\tDB\t{dbArgs},0"));
    }

    /// <summary>
    /// 条件式がビットAND/ORチェーンの比較式か判定。
    /// 短絡ジャンプ最適化が適用可能な場合にtrue。
    /// </summary>
    private static bool CanShortCircuit(Expression expr)
    {
        if (expr is BinaryExpr bin)
        {
            if (bin.Op is BinaryOp.And or BinaryOp.LogAnd or BinaryOp.LogOr)
                return IsComparisonOrChain(bin.Left) && IsComparisonOrChain(bin.Right);
        }
        return false;
    }

    private static bool IsComparisonOrChain(Expression expr)
    {
        if (expr is BinaryExpr bin)
        {
            if (bin.Op is BinaryOp.Eq or BinaryOp.Neq
                or BinaryOp.Lt or BinaryOp.Gt or BinaryOp.Le or BinaryOp.Ge
                or BinaryOp.SLt or BinaryOp.SGt or BinaryOp.SLe or BinaryOp.SGe)
                return true;
            if (bin.Op is BinaryOp.And or BinaryOp.LogAnd or BinaryOp.LogOr)
                return IsComparisonOrChain(bin.Left) && IsComparisonOrChain(bin.Right);
        }
        return false;
    }

    /// <summary>
    /// AND/LogAnd/LogOrチェーンを短絡ジャンプに変換。
    /// 条件がfalseならfalseLabelへジャンプ。
    /// </summary>
    private void EmitShortCircuitJump(Expression expr, string falseLabel)
    {
        if (expr is BinaryExpr bin && bin.Op is BinaryOp.And or BinaryOp.LogAnd)
        {
            // AND: 左false→即falseLabel、右false→即falseLabel
            EmitShortCircuitJump(bin.Left, falseLabel);
            EmitShortCircuitJump(bin.Right, falseLabel);
        }
        else if (expr is BinaryExpr bin2 && bin2.Op == BinaryOp.LogOr)
        {
            // OR: 左true→skip(body側)、左false→右を評価
            var trueLabel = NewLabel();
            EmitShortCircuitJumpIfTrue(bin2.Left, trueLabel);
            EmitShortCircuitJump(bin2.Right, falseLabel);
            Emit(IrOp.Label, IrOperand.Lbl(trueLabel));
        }
        else
        {
            var val = expr.Accept(this);
            Emit(IrOp.JumpIfZero, IrOperand.Lbl(falseLabel), val);
        }
    }

    /// <summary>
    /// 条件がtrueならtrueLabelへジャンプ（LogOr用、再帰対称）。
    /// </summary>
    private void EmitShortCircuitJumpIfTrue(Expression expr, string trueLabel)
    {
        if (expr is BinaryExpr bin && bin.Op == BinaryOp.LogOr)
        {
            // OR: 左true→即trueLabel、右true→即trueLabel
            EmitShortCircuitJumpIfTrue(bin.Left, trueLabel);
            EmitShortCircuitJumpIfTrue(bin.Right, trueLabel);
        }
        else if (expr is BinaryExpr bin2 && bin2.Op is BinaryOp.And or BinaryOp.LogAnd)
        {
            // AND: 左false→skip、右true→trueLabel
            var skipLabel = NewLabel();
            EmitShortCircuitJump(bin2.Left, skipLabel);
            EmitShortCircuitJumpIfTrue(bin2.Right, trueLabel);
            Emit(IrOp.Label, IrOperand.Lbl(skipLabel));
        }
        else
        {
            var val = expr.Accept(this);
            Emit(IrOp.JumpIfNonZero, IrOperand.Lbl(trueLabel), val);
        }
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
        var prevSpan = _stmtSpan;
        foreach (var stmt in node.Statements)
        {
            _stmtSpan = stmt.Span;
            stmt.Accept(this);
        }
        _stmtSpan = prevSpan;
        return IrOperand.None;
    }

    // ==== Declarations ====

    public IrOperand VisitVarDecl(VarDecl node)
    {
        int ds = node.Size == DataSize.Byte ? 1 : (node.Size == DataSize.Float ? 3 : 2);

        // オーバーレイ モジュール直下の VAR → overlay.LocalVars に登録 (main __WORK__ を汚さない)
        if (_currentOverlay != null && _currentFunction == null && !_inStaticDecl)
        {
            var ovLabel = LabelUtils.OverlayVarLabel(_currentOverlay.Index, node.Name);
            var info = new GlobalVarInfo
            {
                Name = node.Name,
                AsmLabel = ovLabel,
                ByteSize = ds,
                ElemSize = ds,
                VarKind = GlobalVarKind.Scalar,
            };
            _currentOverlay.LocalVars.Add(info);
            _currentOverlayLookup![node.Name] = info;
            return IrOperand.None;
        }

        // グローバルスコープ or 関数内静的宣言 → __WORK__に配置
        if (_currentFunction == null || _inStaticDecl)
        {
            int? fixedAddr = null;
            string? fixedAddrLabel = null;
            if (node.Address is IntegerLiteral addrLit)
                fixedAddr = (int)addrLit.Value;
            else if (node.Address != null && UsesLocalConst(node.Address)
                     && NewConstEvaluator().Evaluate(node.Address) is int localAddr)
            {
                // 関数の中の CONST を使ったアドレスの式 (VAR BYTE REG:BASE)
                fixedAddr = localAddr & 0xFFFF;
            }
            else if (node.Address != null)
            {
                // その場で値が決まる式はその値、そうでなければアセンブラの式 (ラベル。ARRAY と同じ)。
                // いままでは整数のリテラル以外を黙って __WORK__ に置いていた。番地は整数なので FLOAT の式はエラー
                // (途中の小数を整数で畳み込んで、黙って違う番地にしない)
                if (ContainsFloat(node.Address, namesAreLabels: true) || NamesConstWithFloat(node.Address))
                    _diagnostics.Error($"Address of VAR '{node.Name}' must be an integer expression, not FLOAT", node.Address.Span);
                else if (NewConstEvaluator().Evaluate(node.Address) is int constAddr)
                    fixedAddr = constAddr & 0xFFFF;
                else if (CheckVarAddressNames(node))
                {
                    int errorsBefore = _diagnostics.ErrorCount;
                    var asmResult = LabelUtils.ExprToAsmString(node.Address, _globalSymbols, _diagnostics, _localConsts);
                    if (asmResult.HasValue)
                    {
                        fixedAddrLabel = asmResult.Value.Expr;
                        foreach (var dep in asmResult.Value.Deps)
                            _module.AddressSymbolDeps.Add(dep);
                    }
                    else if (_diagnostics.ErrorCount == errorsBefore)
                        _diagnostics.Error($"Address of VAR '{node.Name}' must be a constant or an assembler label expression",
                            node.Address.Span);
                }
            }

            // ラベル: 関数内静的は __{FuncName}_{VarName}、トップレベルは __{VarName}
            var label = (_inStaticDecl && _currentFuncName != null)
                ? LabelUtils.StaticVarLabel(_currentFuncName!, node.Name)
                : LabelUtils.UserVarLabel(node.Name);

            // 関数内静的変数のラベルとサイズを追跡
            if (_inStaticDecl && _currentFuncName != null)
            {
                _staticVarLabels![node.Name] = label;
                _staticVarSizes![node.Name] = ds;
                _staticVarKinds![node.Name] = VarKind.Scalar;
            }

            _module.GlobalVars.Add(new GlobalVarInfo
            {
                Name = node.Name,
                AsmLabel = label,
                ByteSize = ds,
                ElemSize = ds,
                FixedAddress = fixedAddr,
                FixedAddressLabel = fixedAddrLabel,
                VarKind = GlobalVarKind.Scalar,
            });

            // 初期値付き: _emitToGlobalDataでGlobalDataに積む（起動時1回だけ初期化）
            if (node.InitialValue != null)
            {
                _emitToGlobalData = true;
                _tempDataSize.Clear();   // 関数の外の一時値は番号がいつも 0。前の初期値の FLOAT の記録を残さない
                _stmtSpan = node.Span;
                try
                {
                    // 起動時の代入は LoadConst と StoreVar の組だけを書く (EmitGlobalInit)。整数の変数の初期値は、その場で
                    // 決まる整数の定数だけ (?: なども値にしてから積む)。FLOAT を使う式・定数にならない式・FLOAT の変数の
                    // 初期値は、黙って書かれなかったのでエラー (FLOAT は整数で畳み込むと違う値になりうる)
                    int? cv = ds <= 2 && !ContainsFloat(node.InitialValue) ? NewConstEvaluator().Evaluate(node.InitialValue) : null;
                    if (cv == null)
                        _diagnostics.Error(ds > 2
                            ? $"FLOAT variable '{node.Name}' cannot have an initializer here yet (assign it at the start of MAIN)"
                            : $"The initializer of global/static variable '{node.Name}' must be an integer constant "
                              + "(FLOAT and non-constant expressions are not written at startup)", node.InitialValue.Span);
                    var val = IrOperand.Temp(AllocTemp());
                    Emit(IrOp.LoadConst, val, IrOperand.Imm((cv ?? 0) & (ds == 1 ? 0xFF : 0xFFFF)));
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
                var name = node.Name;
                // 代入と同じく、変数の型に変えて、割り当てた大きさ (BYTE・WORD は 2) で書く。FLOAT の変数の値だけ、
                // 代入の文の値と同じく FLOAT の定数の演算を正しい値で畳み込む (整数の変数の値はいままでどおり)
                if (ds == 3) MarkFloatFold(node.InitialValue, true);
                EmitLocalInit(() =>
                {
                    var val = node.InitialValue.Accept(this);
                    var info = _localVars![name];
                    val = EmitTypeConversion(val, info.ByteSize);
                    Emit(IrOp.StoreLocal, IrOperand.Imm(info.Offset), val, dataSize: info.ByteSize);
                });
            }
        }
        return IrOperand.None;
    }

    public IrOperand VisitArrayDecl(ArrayDecl node)
    {
        int elemSize = node.Size switch
        {
            DataSize.Byte => 1,
            DataSize.Float => 3,
            _ => 2,
        };
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
                var constEval = _globalSymbols != null ? NewConstEvaluator() : null;
                var val = constEval?.Evaluate(dim);
                dimSize = val.HasValue ? val.Value + 1 : 1;
            }
            dims.Add(dimSize);
            if (dimSize > 0) totalSize *= dimSize;
        }

        // オーバーレイ モジュール直下の ARRAY → overlay.LocalVars (InitialValue / 固定アドレスは
        // Semantic 段階でエラー判定済みなので、ここでは BSS の純粋配列/ポインタのみ扱う)
        if (_currentOverlay != null && _currentFunction == null && !_inStaticDecl)
        {
            bool isPtr = dims.All(d => d == 0);
            int byteSize = isPtr ? 2 : totalSize;
            var ovLabel = LabelUtils.OverlayVarLabel(_currentOverlay.Index, node.Name);
            var info = new GlobalVarInfo
            {
                Name = node.Name,
                AsmLabel = ovLabel,
                ByteSize = byteSize,
                ElemSize = elemSize,
                IsArray = true,
                VarKind = isPtr ? GlobalVarKind.Pointer : GlobalVarKind.Array,
                StorageKind = VarStorageKind.Bss,
            };
            _currentOverlay.LocalVars.Add(info);
            _currentOverlayLookup![node.Name] = info;
            return IrOperand.None;
        }

        if (_currentFunction == null || _inStaticDecl)
        {
            // グローバル or 静的配列（totalSizeは上で計算済み）
            int? fixedAddr = null;
            string? fixedAddrLabel = null;
            if (node.Address is IntegerLiteral addrLit)
                fixedAddr = (int)addrLit.Value;
            else if (node.Address != null)
            {
                // ラベルベースのアドレス (例: ARRAY X[]:LABEL)
                var asmResult = LabelUtils.ExprToAsmString(node.Address, _globalSymbols, _diagnostics, _localConsts);
                if (asmResult.HasValue)
                {
                    fixedAddrLabel = asmResult.Value.Expr;
                    foreach (var dep in asmResult.Value.Deps)
                        _module.AddressSymbolDeps.Add(dep);
                }
            }

            // 初期値付き配列: CODEリストをInitialItemsに変換
            List<InitItem>? initItems = null;
            if (node.InitialCode != null)
            {
                initItems = new List<InitItem>();

                if (node.Size == DataSize.Float)
                {
                    // FLOAT配列専用パス: 全要素3バイトf24で展開
                    var constEval = NewConstEvaluator();
                    foreach (var expr in node.InitialCode)
                    {
                        // トップレベル要素として CastExpr (%X 等) を置くのは禁止。
                        // BYTE/WORD 要素混在の意図を防ぐためで、式の内部に含まれる
                        // CastExpr は EvaluateFloat が再帰的に評価するため許容される。
                        if (expr is CastExpr)
                        {
                            _diagnostics?.Error("Cast expression not allowed in FLOAT array initializer", expr.Span);
                            continue;
                        }
                        double? val = constEval.EvaluateFloat(expr);
                        if (!val.HasValue)
                        {
                            _diagnostics?.Error("FLOAT array initializer must be a constant expression", expr.Span);
                            continue;
                        }
                        var bytes = LabelUtils.ConvertToF24(val.Value);
                        initItems.Add(InitItem.Byte(bytes[0]));
                        initItems.Add(InitItem.Byte(bytes[1]));
                        initItems.Add(InitItem.Byte(bytes[2]));
                    }
                }
                else
                {
                    foreach (var expr in node.InitialCode)
                    {
                        var initExpr = expr;
                        int itemSize = 1; // 配列初期値はデフォルトBYTE（%付きでWORD）
                        if (initExpr is CastExpr cast)
                        {
                            initExpr = cast.Operand;
                            itemSize = cast.TargetSize == DataSize.Byte ? 1 : cast.TargetSize == DataSize.Float ? 3 : 2;
                        }

                        // 定数式の評価を試みる（BinaryExpr, CONST参照なども含む）
                        var constEval = _globalSymbols != null ? NewConstEvaluator() : null;
                        var constVal = constEval?.Evaluate(initExpr);
                        if (initExpr is IntegerLiteral ilit)
                            constVal = (int)ilit.Value; // IntegerLiteralは直接使用

                        if (constVal.HasValue)
                        {
                            int v = constVal.Value;
                            if (itemSize == 1)
                                initItems.Add(InitItem.Byte((byte)(v & 0xFF)));
                            else if (itemSize == 3)
                            {
                                initItems.Add(InitItem.Byte((byte)(v & 0xFF)));
                                initItems.Add(InitItem.Byte((byte)((v >> 8) & 0xFF)));
                                initItems.Add(InitItem.Byte((byte)((v >> 16) & 0xFF)));
                            }
                            else
                            {
                                initItems.Add(InitItem.Byte((byte)(v & 0xFF)));
                                initItems.Add(InitItem.Byte((byte)((v >> 8) & 0xFF)));
                            }
                        }
                        else if (initExpr is StringLiteral slit)
                        {
                            var sjisBytes = StringEncoder.ToShiftJisBytes(slit.Value, _diagnostics);
                            foreach (var b in sjisBytes)
                                initItems.Add(InitItem.Byte(b));
                        }
                        else
                        {
                            // 非定数式: ExprToAsmStringでアセンブラ式に変換
                            var asmResult = LabelUtils.ExprToAsmString(initExpr, _globalSymbols, _diagnostics, _localConsts);
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
            {
                _staticVarLabels![node.Name] = label;
                bool isPointerType = dims.All(d => d == 0);
                _staticVarSizes![node.Name] = 2; // 配列/ポインタ変数は常にWORD
                _staticElemSizes![node.Name] = elemSize;
                _staticVarKinds![node.Name] = isPointerType ? VarKind.Pointer : VarKind.Array;
            }

            // PointerType(間接変数)はアドレス格納なのでWORD(2byte)確保
            bool isPointerGlobal = dims.All(d => d == 0);
            int globalByteSize = isPointerGlobal ? 2 : totalSize;

            _module.GlobalVars.Add(new GlobalVarInfo
            {
                Name = node.Name,
                AsmLabel = label,
                ByteSize = globalByteSize,
                FixedAddress = fixedAddr,
                FixedAddressLabel = fixedAddrLabel,
                IsArray = true,
                InitialItems = initItems,
                StorageKind = initItems != null ? VarStorageKind.InitArray : VarStorageKind.Bss,
            });

            // 間接変数の初期値 (VAR P[] = $C000): 単純変数の初期値と同じく、起動時に 1 回だけ変数そのものに書く
            if (node.InitialValue != null && CheckPointerInitializer(node, isPointerGlobal))
            {
                _emitToGlobalData = true;
                try
                {
                    Emit(IrOp.StoreVar, IrOperand.Sym(label), PointerInitialValue(node.InitialValue), dataSize: 2);
                }
                finally
                {
                    _emitToGlobalData = false;
                }
            }
        }
        else
        {
            // ローカル配列/間接変数: IYオフセットに動的確保
            bool isPointerVar = dims.All(d => d == 0);
            int allocSize = isPointerVar ? 2 : totalSize; // ポインタ変数はアドレス格納なのでWORD(2)
            _localOffset += allocSize;
            int offset = 0x70 - _localOffset;
            var kind = isPointerVar ? VarKind.Pointer : VarKind.Array;
            _localVars![node.Name] = new LocalVarInfo(offset, allocSize, Kind: kind, IsByte: isByte, Dims: dims, ElemSize: elemSize);

            // 間接変数の初期値: 単純変数の局所の初期値と同じく、宣言の所で書く
            if (node.InitialValue != null && CheckPointerInitializer(node, isPointerVar))
                EmitLocalInit(() => Emit(IrOp.StoreLocal, IrOperand.Imm(offset), PointerInitialValue(node.InitialValue), dataSize: 2));
        }
        return IrOperand.None;
    }

    /// <summary>
    /// VAR の番地のアセンブラの式に使える名前か。番地だけの MACHINE (ラベルを出さない) と、ランタイムの名前 (別名・
    /// 名前空間の解決をしない) はエラーにする。使えるのは #ASM のラベルと、SLANG の大域の名前
    /// </summary>
    private bool CheckVarAddressNames(VarDecl node)
    {
        bool ok = true;
        var visited = new HashSet<Symbol>();
        void Check(Expression? e)
        {
            SemanticAnalyzer.ForEachName(e, (name, span) =>
            {
                var sym = _globalSymbols?.Resolve(name);
                if (sym?.Kind == SymbolKind.MachineFunction && !sym.HasCodeBody)
                {
                    _diagnostics.Error($"Address of VAR '{node.Name}' cannot use MACHINE '{name}' without a CODE body (write its address)", span);
                    ok = false;
                }
                else if (sym == null && _runtimeManager?.Functions.ContainsKey(name) == true)
                {
                    _diagnostics.Error($"Address of VAR '{node.Name}' cannot use the runtime name '{name}'", span);
                    ok = false;
                }
                // CONST の式 (アセンブラの式として中身が展開される) の中の名前も見る
                else if (sym?.Kind == SymbolKind.Constant && sym.ConstAst != null && visited.Add(sym))
                    Check(sym.ConstAst);
            });
        }
        Check(node.Address);
        return ok;
    }

    /// <summary>
    /// 間接変数の初期値 (番地なので 2 バイトの整数)。その場で決まる整数の定数 (リテラル・CONST・その式) だけを受け付ける。
    /// FLOAT・変数 (局所変数が隠す CONST の名前も)・ラベルはエラー (通す形を狭くする)
    /// </summary>
    private IrOperand PointerInitialValue(Expression init)
    {
        bool usesVariable = false;
        SemanticAnalyzer.ForEachName(init, (n, _) =>
        {
            if (_localVars?.ContainsKey(n) == true || _staticVarSizes?.ContainsKey(n) == true) usesVariable = true;
        });
        int? v = _globalSymbols != null && !usesVariable && !ContainsFloat(init) ? NewConstEvaluator().Evaluate(init) : null;
        if (v == null)
            _diagnostics.Error("The initializer of an indirect variable must be an integer constant "
                + "(a literal, a CONST, or an expression of them); FLOAT, variables and labels are not allowed", init.Span);
        var t = IrOperand.Temp(AllocTemp());
        Emit(IrOp.LoadConst, t, IrOperand.Imm((v ?? 0) & 0xFFFF));
        return t;
    }

    /// <summary>式の中の CONST の名前の式 (後ろで決まる・ラベルを含む CONST の、まだ値でない式) に FLOAT があるか (たどる)</summary>
    private bool NamesConstWithFloat(Expression e, HashSet<Symbol>? visited = null)
    {
        visited ??= new HashSet<Symbol>();
        bool found = false;
        SemanticAnalyzer.ForEachName(e, (n, _) =>
        {
            if (_globalSymbols?.Resolve(n) is { Kind: SymbolKind.Constant, ConstAst: not null } sym && visited.Add(sym)
                && (ContainsFloat(sym.ConstAst, namesAreLabels: true) || NamesConstWithFloat(sym.ConstAst, visited)))
                found = true;
        });
        return found;
    }

    /// <summary>
    /// 値の式に FLOAT (FLOAT のリテラル・FLOAT の名前 (局所変数が隠すものは見ない)・FLOAT へのキャスト) があるか。
    /// 比較・条件の中の FLOAT も見る (その場で決める値は整数で畳み込むので、FLOAT の比較を誤る)
    /// </summary>
    /// namesAreLabels: 番地の式 (VAR X:式) では、変数の名前はその番地 (整数のラベル) なので、FLOAT の変数でも FLOAT と見ない
    /// (FLOAT の CONST は値なので見る)
    private bool ContainsFloat(Expression? e, bool namesAreLabels = false) => e switch
    {
        FloatLiteral => true,
        IdentifierExpr id => namesAreLabels
            ? _globalSymbols?.Resolve(id.Name) is { Kind: SymbolKind.Constant, ConstFloatValue: not null }
            : IdentifierIsFloat(id),
        CastExpr c => c.TargetSize == DataSize.Float || ContainsFloat(c.Operand, namesAreLabels),
        BinaryExpr b => ContainsFloat(b.Left, namesAreLabels) || ContainsFloat(b.Right, namesAreLabels),
        UnaryExpr u => ContainsFloat(u.Operand, namesAreLabels),
        HighLowExpr h => ContainsFloat(h.Operand, namesAreLabels),
        ConditionalExpr c => ContainsFloat(c.Condition, namesAreLabels) || ContainsFloat(c.TrueExpr, namesAreLabels)
            || ContainsFloat(c.FalseExpr, namesAreLabels),
        CommaExpr c => ContainsFloat(c.Right, namesAreLabels),
        IntegerLiteral => false,
        null => false,
        _ => !namesAreLabels,   // ほかの形 (関数の呼び出し・配列の要素・代入など) は FLOAT かもしれない
    };

    /// <summary>
    /// = 式 (CODE の並びでない初期値) は間接変数 (VAR P[]) だけ。大きさのある配列・ARRAY はエラー
    /// (いままでは黙って捨てていた)
    /// </summary>
    private bool CheckPointerInitializer(ArrayDecl node, bool isPointer)
    {
        // VAR P[] = 番地 だけ。ARRAY A[] は配列 (A の番地そのもの) なので、= 式 を受け付けない
        if (isPointer && !node.IsArrayKeyword) return true;
        _diagnostics.Error(isPointer
                ? $"ARRAY '{node.Name}' cannot have a single-value initializer (use {{...}}, or VAR {node.Name}[] = address for an indirect variable)"
                : $"Array '{node.Name}' with a size cannot have a single-value initializer (use {{...}})",
            node.InitialValue!.Span);
        return false;
    }

    public IrOperand VisitConstDecl(ConstDecl node)
    {
        // 関数の中の CONST: 意味解析で値が決まっている。宣言の順に見えるようにする
        if (_localConsts != null && _currentFuncInfo != null)
        {
            if (_currentFuncInfo.LocalConstants.TryGetValue(node.Name, out var local))
                _localConsts[node.Name] = local;
            Emit(IrOp.Comment, IrOperand.Asm($"CONST {node.Name}"));
            return IrOperand.None;
        }
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
        else if (node.IsAsmEqu)
        {
            // CONST ASM NAME = expr; → アセンブラの EQU として出力
            // 値は SemanticAnalyzer で評価済み
            var sym = _globalSymbols?.Resolve(node.Name);
            if (LabelUtils.IsAsmReserved(LabelUtils.SanitizeLabel(node.Name)))
            {
                // 名前がそのまま EQU の名前になるので、関数のように付け替えられない
                _diagnostics.Error($"CONST ASM name '{node.Name}' is reserved by the assembler", node.Span);
            }
            else if (sym != null && sym.Kind == SymbolKind.Constant && sym.ConstValue is int constVal)
            {
                Emit(IrOp.InlineAsm, IrOperand.Asm($"{node.Name} EQU ${constVal:X4}"));
            }
            else if (sym != null && sym.Kind == SymbolKind.Constant && sym.ConstAst != null)
            {
                // アセンブラ式定数 (シンボル参照含む)
                var exprStr = LabelUtils.ExprToAsmString(sym.ConstAst, _globalSymbols, _diagnostics);
                Emit(IrOp.InlineAsm, IrOperand.Asm($"{node.Name} EQU {exprStr}"));
            }
            else
            {
                _diagnostics.Error($"CONST ASM {node.Name} requires compile-time evaluable value", node.Span);
            }
        }
        else
        {
            Emit(IrOp.Comment, IrOperand.Asm($"CONST {node.Name}"));
        }
        return IrOperand.None;
    }

    public IrOperand VisitCFuncDecl(CFuncDecl node)
    {
        // CFUNC は C backend 専用宣言。Z80 backend で出てきたら診断 error。
        // SLANG コードが両 backend 兼用なら `#IF BACKEND==1` で gate する想定。
        _diagnostics.Error(
            $"CFUNC declaration `{node.Name}` is not supported by Z80 backend; "
            + "gate it with `#IF BACKEND==1` or `#IF ENV_TYPE==7`",
            node.Span);
        return IrOperand.None;
    }

    public IrOperand VisitMachineDecl(MachineDecl node)
    {
        Emit(IrOp.Comment, IrOperand.Asm($"MACHINE {node.Name}"));

        // 静的宣言を関数スコープで処理
        if (node.StaticDeclarations.Count > 0)
        {
            var prevStaticLabels = _staticVarLabels;
            var prevStaticSizes = _staticVarSizes;
            var prevStaticElemSizes = _staticElemSizes;
            var prevStaticVarKinds = _staticVarKinds;
            _staticVarLabels = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            _staticVarSizes = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
            _staticElemSizes = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
            _staticVarKinds = new Dictionary<string, VarKind>(StringComparer.OrdinalIgnoreCase);
            _currentFuncName = LabelUtils.SanitizeLabel(node.Name);
            _inStaticDecl = true;
            foreach (var d in node.StaticDeclarations) d.Accept(this);
            _inStaticDecl = false;
            _currentFuncName = null;
            _staticVarLabels = prevStaticLabels;
            _staticVarSizes = prevStaticSizes;
            _staticElemSizes = prevStaticElemSizes;
            _staticVarKinds = prevStaticVarKinds;
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
            // 定数式の評価を試みる（BinaryExpr, CONST参照なども含む）
            var constEval2 = _globalSymbols != null ? NewConstEvaluator() : null;
            var constVal2 = constEval2?.Evaluate(initExpr);
            if (initExpr is IntegerLiteral ilit2)
                constVal2 = (int)ilit2.Value;

            if (constVal2.HasValue)
            {
                int v = constVal2.Value;
                if (itemSize == 1)
                    initItems.Add(InitItem.Byte((byte)(v & 0xFF)));
                else
                {
                    initItems.Add(InitItem.Byte((byte)(v & 0xFF)));
                    initItems.Add(InitItem.Byte((byte)((v >> 8) & 0xFF)));
                }
            }
            else if (initExpr is StringLiteral slit)
            {
                foreach (var ch in slit.Value)
                    initItems.Add(InitItem.Byte((byte)ch));
            }
            else
            {
                // 非定数式: アセンブラ式に変換（ラベル参照等）
                var asmResult = LabelUtils.ExprToAsmString(initExpr, _globalSymbols, _diagnostics, _localConsts);
                if (asmResult.HasValue)
                {
                    // ラベル参照は常にWORD（アドレス）
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
        _currentFunction = new IrFunction { Name = LabelUtils.FunctionLabel(node.Name) };
        _tempDataSize.Clear();
        var prevLocalConsts = _localConsts;
        var prevFuncInfo = _currentFuncInfo;
        _currentFuncInfo = _globalSymbols?.Resolve(node.Name)?.ConstValue as FuncInfo;
        _localConsts = _currentFuncInfo?.LocalConstants.Count > 0
            ? new Dictionary<string, Symbol>(_currentFuncInfo.LocalConstants.Comparer) : null;

        // 関数の戻り値型を保存 (ネスト関数定義に備えて prev/restore 形式)
        int prevReturnDs = _currentReturnDataSize;
        _currentReturnDataSize = node.ReturnSize == DataSize.Float ? 3 : 2;

        // ローカルシンボルテーブルを構築
        var prevLocalVars = _localVars;
        var prevOffset = _localOffset;
        var prevStaticLabels = _staticVarLabels;
        var prevStaticSizes = _staticVarSizes;
        var prevStaticElemSizes = _staticElemSizes;
        var prevStaticVarKinds = _staticVarKinds;
        _localVars = new Dictionary<string, LocalVarInfo>(StringComparer.OrdinalIgnoreCase);
        _staticVarLabels = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        _staticVarSizes = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
        _staticElemSizes = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
        _staticVarKinds = new Dictionary<string, VarKind>(StringComparer.OrdinalIgnoreCase);
        _localOffset = 0;

        // 仮引数を仮登録（オフセットはLocalDeclarations走査後に確定）
        // 各引数のバイトサイズを ParamDecl.Size から取得 (FLOAT=3, BYTE/WORD=2、配列引数は常に 2 byte ポインタ)
        var paramNames = new List<string>();
        var paramSizes = new List<int>();
        foreach (var p in node.Parameters)
        {
            var info = MakeParamLocalVarInfo(p, 0); // 仮オフセット
            _localVars[p.Name] = info;
            paramNames.Add(p.Name);
            paramSizes.Add(info.ByteSize);
        }

        Emit(IrOp.FuncBegin, IrOperand.Sym(LabelUtils.FunctionLabel(node.Name)));

        // Static declarations → グローバルメモリ(__WORK__)、Local declarations → 動的(IY)
        _currentFuncName = LabelUtils.SanitizeLabel(node.Name);
        _inStaticDecl = true;
        foreach (var d in node.StaticDeclarations) d.Accept(this);
        _inStaticDecl = false;
        // 局所の初期値は、引数のオフセットが決まってから出す (初期値の式が引数を読むことがある)
        _deferredLocalInits = new List<Action>();
        foreach (var d in node.LocalDeclarations) d.Accept(this);
        var localInits = _deferredLocalInits;
        _deferredLocalInits = null;

        // ローカル変数確定後、引数のオフセットを計算
        // ADD IY, (localOffset + paramTotalBytes) でIYがずれるため:
        // 引数は 0x70 - localOffset - paramTotalBytes から配置
        // ローカル変数は 0x70 - localOffset から 0x70 - 1 まで
        {
            int paramTotalBytes = paramSizes.Sum();
            int totalFrameSize = _localOffset + paramTotalBytes;
            int argOff = 0x70 - totalFrameSize;
            for (int i = 0; i < node.Parameters.Count; i++)
            {
                _localVars[paramNames[i]] = MakeParamLocalVarInfo(node.Parameters[i], argOff);
                argOff += paramSizes[i];
            }
            // localOffsetに引数分も加算（ADD IY,BCのフレームサイズ）
            _localOffset = totalFrameSize;
        }
        foreach (var init in localInits) init();

        // Body
        node.Body.Accept(this);

        // Return value from END(expr)
        if (node.ReturnValue != null)
        {
            _stmtSpan = node.ReturnValue.Span;
            if (_currentReturnDataSize == 3) MarkFloatFold(node.ReturnValue, true);
            var retVal = node.ReturnValue.Accept(this);
            retVal = ConvertReturnValue(retVal, node.ReturnValue.Span);
            Emit(IrOp.Return, retVal, dataSize: _currentReturnDataSize);
        }

        Emit(IrOp.FuncEnd);
        _currentFunction.LocalSize = _localOffset;
        _module.Functions.Add(_currentFunction);
        _currentFunction = null;
        _localVars = prevLocalVars;
        _staticVarLabels = prevStaticLabels;
        _staticVarSizes = prevStaticSizes;
        _staticElemSizes = prevStaticElemSizes;
        _staticVarKinds = prevStaticVarKinds;
        _localOffset = prevOffset;
        _currentReturnDataSize = prevReturnDs;
        _localConsts = prevLocalConsts;
        _currentFuncInfo = prevFuncInfo;
        _tempDataSize.Clear();   // 関数の外の一時値は番号がいつも 0 なので、関数の中の FLOAT の記録を残さない
        return IrOperand.None;
    }

    /// <summary>ローカル変数のオフセットを割り当て</summary>
    /// <summary>
    /// 仮引数 (ParamDecl) を関数スコープの LocalVarInfo に変換する。
    /// 配列引数 (BYTE T[] など) は常に 2 byte ポインタとしてスタックに積まれるが、
    /// 添字アクセス時に必要な要素サイズ (BYTE=1 / WORD=2 / FLOAT=3) と byte 配列フラグも反映する。
    /// </summary>
    private static LocalVarInfo MakeParamLocalVarInfo(ParamDecl p, int offset)
    {
        if (p.IsArray)
        {
            int elemSize = p.Size switch
            {
                DataSize.Byte => 1,
                DataSize.Float => 3,
                _ => 2,
            };
            bool isByte = p.Size == DataSize.Byte;
            return new LocalVarInfo(offset, 2, Kind: VarKind.Pointer, IsByte: isByte, ElemSize: elemSize);
        }
        // 非配列: BYTE/WORD はスタックに 2 byte で積む (BYTE は upper byte 不定)、FLOAT は 3 byte
        int sz = p.Size == DataSize.Float ? 3 : 2;
        return new LocalVarInfo(offset, sz);
    }

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
        // 代入の文の値に、FLOAT の定数の四則演算を正しい値で畳み込む印を付ける (式の中の代入式には付けない。
        // 条件の中の (C=1.0/2.0) などで、FLOAT の値を条件に持ち込まないため)。FLOAT の値を作ってよいのは、
        // 代入先がすべて FLOAT の変数・FLOAT の配列の要素のときだけ (それ以外は整数の値を正すだけ)
        if (node.Expr is AssignExpr assign) MarkFloatFold(assign.Value, IsFloatTarget(assign.Target));
        _valueDiscarded = TakesDiscard(node.Expr);
        node.Expr.Accept(this);
        return IrOperand.None;
    }

    public IrOperand VisitIfStmt(IfStmt node)
    {
        var endLabel = NewLabel();
        var constEval = _globalSymbols != null ? NewConstEvaluator() : null;

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

            // ビットAND/ORチェーンの短絡評価最適化
            // IF (cmp1 AND cmp2 AND ...) → 各比較で即座にジャンプ（旧コンパイラ互換）
            if (CanShortCircuit(cond))
                EmitShortCircuitJump(cond, nextLabel);
            else
            {
                var condVal = cond.Accept(this);
                Emit(IrOp.JumpIfZero, IrOperand.Lbl(nextLabel), condVal);
            }

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
        var constEval = _globalSymbols != null ? NewConstEvaluator() : null;
        var constCond = constEval?.Evaluate(node.Condition);

        Emit(IrOp.Label, IrOperand.Lbl(startLabel));

        if (constCond.HasValue && constCond.Value != 0)
        {
            // WHILE(TRUE) or WHILE(非ゼロ定数): 条件チェック不要
        }
        else
        {
            if (CanShortCircuit(node.Condition))
                EmitShortCircuitJump(node.Condition, endLabel);
            else
            {
                var condVal = node.Condition.Accept(this);
                Emit(IrOp.JumpIfZero, IrOperand.Lbl(endLabel), condVal);
            }
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

        // CONTINUE は UNTIL の判定へ行く (C の do-while と同じ)。ラベルは CONTINUE があるときだけ作る
        var loop = PushLoop(null, endLabel);

        Emit(IrOp.Label, IrOperand.Lbl(startLabel));
        node.Body.Accept(this);
        if (loop.ContinueLabel != null)
            Emit(IrOp.Label, IrOperand.Lbl(loop.ContinueLabel));

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

        // FOR変数のアクセス方法を決定（未定義チェック付き）
        var forVi = CheckDefined(node.Variable, node.Span);
        var forVarInfo = forVi.Local;
        bool forVarIsLocal = forVarInfo != null;
        int forVarDs = forVi.VarDataSize;
        if (forVarDs == 3 && forVi.Kind == VarKind.Scalar)
        {
            _diagnostics.Error("The FOR variable must not be FLOAT (use an integer variable)", node.Span);
            return IrOperand.None;
        }

        // Initialize: var = from
        var fromVal = node.From.Accept(this);
        if (TempDataSize(fromVal) == 3)
            _diagnostics.Error("The start of FOR must not be FLOAT (use FTOI)", node.From.Span);
        if (forVarIsLocal)
            Emit(IrOp.StoreLocal, IrOperand.Imm(forVarInfo!.Offset), fromVal, dataSize: forVarDs);
        else
            Emit(IrOp.StoreVar, IrOperand.Sym(ResolveAsmLabel(node.Variable)), fromVal, dataSize: forVarDs);

        PushLoop(contLabel, endLabel);

        Emit(IrOp.Label, IrOperand.Lbl(startLabel));

        // Body
        node.Body.Accept(this);

        // Continue label (increment/decrement)
        Emit(IrOp.Label, IrOperand.Lbl(contLabel));

        // Increment/decrement
        var curVal = IrOperand.Temp(AllocTemp());
        if (forVarIsLocal)
            Emit(IrOp.LoadLocal, curVal, IrOperand.Imm(forVarInfo!.Offset), dataSize: forVarDs);
        else
            Emit(IrOp.LoadVar, curVal, IrOperand.Sym(ResolveAsmLabel(node.Variable)), dataSize: forVarDs);
        var one = IrOperand.Temp(AllocTemp());
        Emit(IrOp.LoadConst, one, IrOperand.Imm(1));
        var newVal = IrOperand.Temp(AllocTemp());
        Emit(node.IsDownTo ? IrOp.Sub : IrOp.Add, newVal, curVal, one);
        if (forVarIsLocal)
            Emit(IrOp.StoreLocal, IrOperand.Imm(forVarInfo!.Offset), newVal, dataSize: forVarDs);
        else
            Emit(IrOp.StoreVar, IrOperand.Sym(ResolveAsmLabel(node.Variable)), newVal, dataSize: forVarDs);

        // Compare with limit
        // FOR TO: unsigned比較（旧コンパイラ互換、コンパクト）
        // FOR DOWNTO: 符号付き比較（0を跨ぐオーバーフロー対策）
        var limit = node.To.Accept(this);
        if (TempDataSize(limit) == 3)
            _diagnostics.Error("The end of FOR must not be FLOAT (use FTOI)", node.To.Span);
        var cmp = IrOperand.Temp(AllocTemp());
        Emit(node.IsDownTo ? IrOp.CmpSGe : IrOp.CmpLe, cmp, newVal, limit);
        Emit(IrOp.JumpIfNonZero, IrOperand.Lbl(startLabel), cmp);

        Emit(IrOp.Label, IrOperand.Lbl(endLabel));
        PopLoop();
        return IrOperand.None;
    }

    public IrOperand VisitCaseStmt(CaseStmt node)
    {
        var endLabel = NewLabel();

        PushLoop(endLabel, endLabel, isCase: true); // EXIT・CONTINUE は CASE を数えずに外のループへ

        // Phase 1: 各ブランチの bodyLabel を前計算
        // body==null のブランチ（カンマ先行値）は次の body!=null ブランチの bodyLabel を共有
        var bodyLabels = new string?[node.Branches.Count];
        string? othersLabel = null;

        // 後ろから走査して bodyLabel を伝播
        string? currentBodyLabel = null;
        for (int i = node.Branches.Count - 1; i >= 0; i--)
        {
            var branch = node.Branches[i];
            if (branch.Body != null)
            {
                currentBodyLabel = NewLabel();
                bodyLabels[i] = currentBodyLabel;
                if (branch.Value == null)
                    othersLabel = currentBodyLabel;
            }
            else
            {
                // カンマ先行値: 次の body 付きブランチの bodyLabel を共有
                bodyLabels[i] = currentBodyLabel;
            }
        }

        // Phase 2: 比較コード出力
        for (int i = 0; i < node.Branches.Count; i++)
        {
            var branch = node.Branches[i];
            if (branch.Value == null)
            {
                // OTHERS — Phase 2 では何も出力しない（Phase 3 で body を出力）
                continue;
            }

            var nextLabel = NewLabel();
            var branchVal = branch.Value.Accept(this);

            if (branch.RangeEnd != null)
            {
                // Range: value TO rangeEnd — 短絡ジャンプ化
                var rangeEnd = branch.RangeEnd.Accept(this);
                // CmpGe: exprVal >= branchVal（exprVal再評価: fusedSBCでHL破壊されるため）
                var reloadedExpr1 = node.Expr.Accept(this);
                var cmpLo = IrOperand.Temp(AllocTemp());
                Emit(IrOp.CmpGe, cmpLo, reloadedExpr1, branchVal);
                Emit(IrOp.JumpIfZero, IrOperand.Lbl(nextLabel), cmpLo);
                // CmpLe: exprVal <= rangeEnd（同様に再評価）
                var reloadedExpr2 = node.Expr.Accept(this);
                var cmpHi = IrOperand.Temp(AllocTemp());
                Emit(IrOp.CmpLe, cmpHi, reloadedExpr2, rangeEnd);
                Emit(IrOp.JumpIfZero, IrOperand.Lbl(nextLabel), cmpHi);
            }
            else
            {
                // exprValを再ロード（fusedSBCでHL破壊されるため）
                var reloadedExpr = node.Expr.Accept(this);
                var cmp = IrOperand.Temp(AllocTemp());
                Emit(IrOp.CmpEq, cmp, reloadedExpr, branchVal);
                Emit(IrOp.JumpIfZero, IrOperand.Lbl(nextLabel), cmp);
            }

            // マッチ時: 対応する bodyLabel へジャンプ
            Emit(IrOp.Jump, IrOperand.Lbl(bodyLabels[i]!));
            Emit(IrOp.Label, IrOperand.Lbl(nextLabel));
        }

        // 全比較不一致時: OTHERS があればそこへ、なければ endLabel へ
        Emit(IrOp.Jump, IrOperand.Lbl(othersLabel ?? endLabel));

        // Phase 3: body コード出力（body!=null のブランチだけ）
        for (int i = 0; i < node.Branches.Count; i++)
        {
            var branch = node.Branches[i];
            if (branch.Body == null)
                continue;

            Emit(IrOp.Label, IrOperand.Lbl(bodyLabels[i]!));
            branch.Body.Accept(this);
            Emit(IrOp.Jump, IrOperand.Lbl(endLabel));
        }

        Emit(IrOp.Label, IrOperand.Lbl(endLabel));
        PopLoop();
        return IrOperand.None;
    }

    public IrOperand VisitExitStmt(ExitStmt node)
    {
        if (node.TargetLabel != null)
        {
            // GOTO と同じラベル (_LBL_)
            Emit(IrOp.Jump, IrOperand.Lbl(LabelUtils.UserLabel(node.TargetLabel)));
            return IrOperand.None;
        }

        // EXIT(n): n はその場で決まる定数。CASE を数えずに、内側から n 番目のループを抜ける
        int level = 1;
        if (node.Level != null)
        {
            // 引数・局所変数・静的変数は、同じ名前の大域の CONST を隠す (値は実行時なので定数ではない)。
            // 後ろで決まる大域の CONST (CONST A = B; CONST B = 1;) はたどる
            bool usesVariable = false;
            SemanticAnalyzer.ForEachName(node.Level, (name, _) => { if (IsShadowingVariable(name)) usesVariable = true; });
            var n = _globalSymbols != null && !usesVariable
                ? new ConstEvaluator(_globalSymbols, _localConsts, followConstAst: true).Evaluate(node.Level) : null;
            if (n is not int lv || lv < 1)
            {
                _diagnostics.Error("EXIT level must be a constant of 1 or more", node.Level.Span);
                return IrOperand.None;
            }
            level = lv;
        }
        var loop = FindLoop(level);
        if (loop != null)
            Emit(IrOp.Jump, IrOperand.Lbl(loop.BreakLabel));
        else if (FindLoop(1) == null)
            _diagnostics.Error("EXIT outside loop", node.Span);
        else
            _diagnostics.Error($"EXIT({level}) exceeds the loop nesting", node.Span);
        return IrOperand.None;
    }

    public IrOperand VisitContinueStmt(ContinueStmt node)
    {
        // CASE を数えずに、いちばん内側のループの続ける所へ
        var loop = FindLoop(1);
        if (loop != null)
        {
            loop.ContinueLabel ??= NewLabel();
            Emit(IrOp.Jump, IrOperand.Lbl(loop.ContinueLabel));
        }
        else
            _diagnostics.Error("CONTINUE outside loop", node.Span);
        return IrOperand.None;
    }

    public IrOperand VisitReturnStmt(ReturnStmt node)
    {
        if (node.Value != null)
        {
            if (_currentReturnDataSize == 3) MarkFloatFold(node.Value, true);   // FLOAT の関数の戻り値は FLOAT の値が要る所
            var val = node.Value.Accept(this);
            val = ConvertReturnValue(val, node.Value.Span);
            Emit(IrOp.Return, val, dataSize: _currentReturnDataSize);
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
                        if (sf.Arguments.Count > 0) PrintValue(sf.Arguments[0]);
                        Emit(IrOp.Call, IrOperand.None, IrOperand.Sym("PHEX2"));
                        break;
                    case "HEX4$":
                        if (sf.Arguments.Count > 0) PrintValue(sf.Arguments[0]);
                        Emit(IrOp.Call, IrOperand.None, IrOperand.Sym("PHEX4"));
                        break;
                    case "FORM$":
                        // FORM$(value, n): HL=value, DE=n桁 → MACHINE(2)として呼ぶ
                        if (sf.Arguments.Count >= 2)
                        {
                            PrintValue(sf.Arguments[0]);
                            Emit(IrOp.PushArg, IrOperand.None);
                            PrintValue(sf.Arguments[1]);
                            Emit(IrOp.PushArg, IrOperand.None);
                        }
                        Emit(IrOp.Call, IrOperand.None, IrOperand.Sym("P10toN"), IrOperand.Imm(2));
                        break;
                    case "DECI$":
                        if (sf.Arguments.Count > 0) PrintValue(sf.Arguments[0]);
                        Emit(IrOp.Call, IrOperand.None, IrOperand.Sym("P10to5"));
                        break;
                    case "%" or "PN$":
                        if (sf.Arguments.Count > 0) PrintValue(sf.Arguments[0]);
                        Emit(IrOp.Call, IrOperand.None, IrOperand.Sym("PSIGN"));
                        break;
                    case "MSG$":
                        if (sf.Arguments.Count > 0) PrintValue(sf.Arguments[0]);
                        Emit(IrOp.Call, IrOperand.None, IrOperand.Sym("PMSG"));
                        break;
                    case "!" or "MSX$":
                        if (sf.Arguments.Count > 0) PrintValue(sf.Arguments[0]);
                        Emit(IrOp.Call, IrOperand.None, IrOperand.Sym("PMSX"));
                        break;
                    case "STR$":
                        // STR$(char, n): HL=char, DE=n → MACHINE(2)として呼ぶ
                        if (sf.Arguments.Count >= 2)
                        {
                            PrintValue(sf.Arguments[0]);
                            Emit(IrOp.PushArg, IrOperand.None);
                            PrintValue(sf.Arguments[1]);
                            Emit(IrOp.PushArg, IrOperand.None);
                        }
                        Emit(IrOp.Call, IrOperand.None, IrOperand.Sym("PSTR"), IrOperand.Imm(2));
                        break;
                    case "CHR$":
                        if (sf.Arguments.Count > 0) PrintValue(sf.Arguments[0]);
                        Emit(IrOp.Call, IrOperand.None, IrOperand.Sym("PCHR"));
                        break;
                    case "SPC$":
                        if (sf.Arguments.Count > 0) PrintValue(sf.Arguments[0]);
                        Emit(IrOp.Call, IrOperand.None, IrOperand.Sym("PSPC"));
                        break;
                    case "CR$":
                        if (sf.Arguments.Count > 0) PrintValue(sf.Arguments[0]);
                        Emit(IrOp.Call, IrOperand.None, IrOperand.Sym("PCR"));
                        break;
                    case "TAB$":
                        if (sf.Arguments.Count > 0) PrintValue(sf.Arguments[0]);
                        Emit(IrOp.Call, IrOperand.None, IrOperand.Sym("PTAB"));
                        break;
                    case "FL$":
                        // 値は FLOAT の値が要る所 (整数は FLOAT に変える)
                        if (sf.Arguments.Count > 0)
                        {
                            MarkFloatFold(sf.Arguments[0], true);
                            EmitTypeConversionExpand(sf.Arguments[0].Accept(this), 3);
                        }
                        Emit(IrOp.Call, IrOperand.None, IrOperand.Sym("PFLOAT"));
                        break;
                    default:
                        foreach (var a in sf.Arguments) PrintValue(a);
                        Emit(IrOp.Call, IrOperand.None, IrOperand.Sym($"PRINT_{sf.FuncName}"));
                        break;
                }
            }
            else if (arg is StringLiteral strLit)
            {
                // MPRNT方式: CALL MPRNT → DB "string" → DB 0
                // 旧コンパイラ互換のインライン文字列出力（LD HL不要で3バイト節約）
                EmitInlinePrint(strLit.Value);
            }
            else
            {
                PrintValue(arg);
                // P10: HL=数値を10進文字列に変換して出力
                Emit(IrOp.Call, IrOperand.None, IrOperand.Sym("P10"));
            }
        }
        return IrOperand.None;
    }

    /// <summary>PRINT の項の値 (FL$ のほか)。FLOAT の値はそのまま出せない</summary>
    private void PrintValue(Expression e)
    {
        if (TempDataSize(e.Accept(this)) == 3)
            _diagnostics.Error("A FLOAT value cannot be printed directly (use FL$(x))", e.Span);
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
        return EmitLoadFloatConst(node.Value);
    }

    /// <summary>FLOAT即値をLoadFloatConstとしてemit（VisitFloatLiteralとConstFloatValue共通）</summary>
    private IrOperand EmitLoadFloatConst(double value)
    {
        var f24 = LabelUtils.ConvertToF24(value);
        int hlVal = f24[0] | (f24[1] << 8);  // mantissa
        int aVal = f24[2];                     // exponent
        var t = IrOperand.Temp(AllocTemp());
        Emit(IrOp.LoadFloatConst, t, IrOperand.Imm(hlVal), IrOperand.Imm(aVal), dataSize: 3);
        _tempDataSize[t.TempIndex] = 3;
        return t;
    }

    public IrOperand VisitStringLiteral(StringLiteral node)
    {
        // 文字列テーブルに登録し、ラベルアドレスをロード（重複排除）
        string? label = null;
        foreach (var (k, v) in _module.StringTable)
        {
            if (v == node.Value) { label = k; break; }
        }
        if (label == null)
        {
            label = $"_S{_module.StringTable.Count}";
            _module.StringTable[label] = node.Value;
        }

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
            if (localInfo.Kind == VarKind.Array)
            {
                // サイズ確定ローカル配列: アドレスをロード（IY+offsetの実効アドレス）
                Emit(IrOp.InlineAsm, t, IrOperand.Asm($"\tPUSH\tIY\n\tPOP\tHL\n\tLD\tDE,${localInfo.Offset:X4}\n\tADD\tHL,DE"));
                return t;
            }
            int localDs = localInfo.Kind == VarKind.Pointer ? 2 : localInfo.ByteSize;
            Emit(IrOp.LoadLocal, t, IrOperand.Imm(localInfo.Offset), dataSize: localDs);
            _tempDataSize[t.TempIndex] = localDs;
            return t;
        }

        // 2. 関数の中の CONST、なければグローバルシンボルテーブルを検索
        var sym = ResolveConstOrGlobal(node.Name);
        if (sym != null && sym.Kind == SymbolKind.Constant && sym.ConstValue is int constVal)
        {
            Emit(IrOp.LoadConst, t, IrOperand.Imm(constVal));
        }
        else if (sym != null && sym.Kind == SymbolKind.Constant && sym.ConstFloatValue.HasValue)
        {
            // FLOAT定数: 即値ロード
            return EmitLoadFloatConst(sym.ConstFloatValue.Value);
        }
        else if (sym != null && sym.Kind == SymbolKind.Constant && sym.ConstAst != null)
        {
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
            Emit(IrOp.LoadAddr, t, IrOperand.Sym(ResolveAsmLabel(node.Name)));
        }
        else
        {
            // ResolveVarInfoで種別を判定（未定義チェック付き）
            var vi = CheckDefined(node.Name, node.Span);
            if (vi.Kind == VarKind.Array)
            {
                Emit(IrOp.LoadAddr, t, IrOperand.Sym(ResolveAsmLabel(node.Name)));
            }
            else
            {
                Emit(IrOp.LoadVar, t, IrOperand.Sym(ResolveAsmLabel(node.Name)), dataSize: vi.VarDataSize);
                _tempDataSize[t.TempIndex] = vi.VarDataSize;
            }
        }
        return t;
    }

    public IrOperand VisitBinaryExpr(BinaryExpr node)
    {
        // 定数畳み込み: 両辺が定数ならコンパイル時に計算
        if (_globalSymbols != null)
        {
            var constEval = NewConstEvaluator();
            // 代入する値の中の FLOAT が関わる四則演算は、整数としてではなく正しい値で畳み込む (A=5.0/2.0 は FLOAT の 2.5)。
            // f24 で正確に表せないものは畳み込まず実行時に計算する。代入の値の外 (条件・添字・引数など) は今までどおり
            bool marked = _floatFoldNodes.Contains(node);
            var floatFold = marked || _intFoldNodes.Contains(node) ? constEval.FoldFloatArithmetic(node) : null;
            // 代入先が FLOAT でないときは、整数の値を正すだけ (FLOAT の値・実行時の計算は作らず、今までどおり)
            if (!marked && floatFold is { Kind: not ConstEvaluator.FloatFoldKind.Int }) floatFold = null;
            if (floatFold is { Kind: ConstEvaluator.FloatFoldKind.Float } ff) return EmitLoadFloatConst(ff.FloatValue);
            // FLOAT が要る位置 (実行時に計算する FLOAT の演算の辺) では、整数の結果も FLOAT の定数にする。
            // FLOAT の値が要る所で、整数の結果を符号付きで FLOAT に戻すと違う値になるとき (20000.0*2.0 の 40000 は -25536) も
            if (floatFold is { Kind: ConstEvaluator.FloatFoldKind.Int } fr
                && (_floatRequiredNodes.Contains(node) || marked && (short)fr.IntValue != fr.FloatValue))
                return EmitLoadFloatConst(fr.FloatValue);
            // 実行時に計算するときは、辺の FLOAT の定数の演算を FLOAT のまま作る (整数に畳み込むと親が整数の演算になる)
            if (floatFold is { Kind: ConstEvaluator.FloatFoldKind.Runtime })
            {
                RequireFloat(node.Left);
                RequireFloat(node.Right);
            }
            var constResult = floatFold is { } fi
                ? (fi.Kind == ConstEvaluator.FloatFoldKind.Int ? fi.IntValue : (int?)null)
                : constEval.Evaluate(node);
            if (constResult.HasValue)
            {
                var t = IrOperand.Temp(AllocTemp());
                Emit(IrOp.LoadConst, t, IrOperand.Imm(constResult.Value));
                return t;
            }

            // no-op算術のIR正規化: CodeGeneratorのスタック整合バグ回避
            // Add(x,0)/Sub(x,0) → x, Mul(x,1)/Div(x,1) → x
            // 実行時に計算する定数の演算 (Runtime) では、FLOAT が関わる辺を整数の評価で 0・1 と見ない
            // (1.0+1.0/3.0 の 1.0/3.0 を 0 と見て消さない)。FLOAT の値が要る所でも、FLOAT が関わる辺は正しい値で見る
            // (F=G+(1.0/2.0) の右を 0 と見て消さない)。それ以外は今までどおり
            bool runtimeFold = floatFold is { Kind: ConstEvaluator.FloatFoldKind.Runtime };
            if (node.Op is BinaryOp.Add or BinaryOp.Sub)
            {
                var rc = NormalizationConst(constEval, node.Right, runtimeFold, marked);
                if (rc.HasValue && rc.Value == 0) return node.Left.Accept(this);
                if (node.Op == BinaryOp.Add)
                {
                    var lc = NormalizationConst(constEval, node.Left, runtimeFold, marked);
                    if (lc.HasValue && lc.Value == 0) return node.Right.Accept(this);
                }
            }
            if (node.Op is BinaryOp.Mul or BinaryOp.SMul or BinaryOp.Div or BinaryOp.SDiv)
            {
                var rc = NormalizationConst(constEval, node.Right, runtimeFold, marked);
                if (rc.HasValue && rc.Value == 1) return node.Left.Accept(this);
                if (node.Op is BinaryOp.Mul or BinaryOp.SMul)
                {
                    var lc = NormalizationConst(constEval, node.Left, runtimeFold, marked);
                    if (lc.HasValue && lc.Value == 1) return node.Right.Accept(this);
                }
            }
        }

        // 論理AND/OR: 短絡評価（旧コンパイラ互換）
        if (node.Op == BinaryOp.LogAnd)
        {
            var falseLabel = NewLabel();
            var endLabel = NewLabel();
            var result = IrOperand.Temp(AllocTemp());
            var lhsAnd = node.Left.Accept(this);
            Emit(IrOp.JumpIfZero, IrOperand.Lbl(falseLabel), lhsAnd);
            var rhsAnd = node.Right.Accept(this);
            Emit(IrOp.JumpIfZero, IrOperand.Lbl(falseLabel), rhsAnd);
            Emit(IrOp.LoadConst, result, IrOperand.Imm(1));
            Emit(IrOp.Jump, IrOperand.Lbl(endLabel));
            Emit(IrOp.Label, IrOperand.Lbl(falseLabel));
            Emit(IrOp.LoadConst, result, IrOperand.Imm(0));
            Emit(IrOp.Label, IrOperand.Lbl(endLabel));
            return result;
        }
        if (node.Op == BinaryOp.LogOr)
        {
            var trueLabel = NewLabel();
            var endLabel = NewLabel();
            var result = IrOperand.Temp(AllocTemp());
            var lhsOr = node.Left.Accept(this);
            Emit(IrOp.JumpIfNonZero, IrOperand.Lbl(trueLabel), lhsOr);
            var rhsOr = node.Right.Accept(this);
            Emit(IrOp.JumpIfNonZero, IrOperand.Lbl(trueLabel), rhsOr);
            Emit(IrOp.LoadConst, result, IrOperand.Imm(0));
            Emit(IrOp.Jump, IrOperand.Lbl(endLabel));
            Emit(IrOp.Label, IrOperand.Lbl(trueLabel));
            Emit(IrOp.LoadConst, result, IrOperand.Imm(1));
            Emit(IrOp.Label, IrOperand.Lbl(endLabel));
            return result;
        }

        // FLOAT専用パターン: A*A → f24sqr, 2*A → f24mul2
        // ASTレベルで早期検出（i16tof24変換前に拾う必要がある）
        // 現段階ではglobal FLOAT identifierのみ対象（ローカルFLOATやCastExprは取りこぼし）。
        // 同じ名前の局所変数があれば局所が優先 (VisitIdentifier と同じ)、配列はアドレスなので対象外
        if (node.Op == BinaryOp.Mul)
        {
            bool leftIsFloat = node.Left is IdentifierExpr li2 && IsGlobalFloatScalar(li2);
            bool rightIsFloat = node.Right is IdentifierExpr ri2 && IsGlobalFloatScalar(ri2);

            // f24sqr: X*X（同一FLOAT変数）
            if (leftIsFloat && rightIsFloat
                && node.Left is IdentifierExpr sqrL && node.Right is IdentifierExpr sqrR
                && sqrL.Name == sqrR.Name)
            {
                node.Left.Accept(this); // AHL = X
                var sqrDest = IrOperand.Temp(AllocTemp());
                Emit(IrOp.Call, sqrDest, IrOperand.Sym("f24sqr"), IrOperand.Imm(0), dataSize: 3);
                _tempDataSize[sqrDest.TempIndex] = 3;
                return sqrDest;
            }

            // f24mul2: 2*X or X*2（一方が整数リテラル2、他方がglobal FLOAT）
            Expression? floatSide = null;
            if (node.Left is IntegerLiteral il2a && il2a.Value == 2 && rightIsFloat)
                floatSide = node.Right;
            else if (node.Right is IntegerLiteral il2b && il2b.Value == 2 && leftIsFloat)
                floatSide = node.Left;

            if (floatSide != null)
            {
                floatSide.Accept(this); // AHL = X
                var mul2Dest = IrOperand.Temp(AllocTemp());
                Emit(IrOp.Call, mul2Dest, IrOperand.Sym("f24mul2"), IrOperand.Imm(0), dataSize: 3);
                _tempDataSize[mul2Dest.TempIndex] = 3;
                return mul2Dest;
            }
        }

        // 左を作り終えた位置を覚える。右が FLOAT なら、左の変換を右を作った後でこの位置に差し込む (左の値を積む前に
        // 変える。?: の真の側と同じ)
        var left = node.Left.Accept(this);
        int leftDs = left.Kind == IrOperandKind.Temp && _tempDataSize.TryGetValue(left.TempIndex, out int lds) ? lds : 2;
        var list = CurrentInstructionList();
        int leftEnd = list.Count;

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
                var conv = IrOperand.Temp(AllocTemp());
                list.Insert(leftEnd, new IrInstruction(IrOp.Call, conv, IrOperand.Sym("i16tof24"), IrOperand.Imm(0))
                    { DataSize = 3, ImplicitSrc = left });
                _tempDataSize[conv.TempIndex] = 3;
                left = conv;
            }
            if (rightDs != 3)
            {
                var conv = IrOperand.Temp(AllocTemp());
                EmitConversionCall(conv, "i16tof24", right, dataSize: 3);
                _tempDataSize[conv.TempIndex] = 3;
                right = conv;
            }
        }

        Emit(op, dest, left, right, dataSize: resultDs);
        // 比較の命令の大きさは辺の大きさ (FLOAT の比較)、値は 0/1 の整数
        _tempDataSize[dest.TempIndex] = IsComparisonOp(node.Op) ? 2 : resultDs;
        return dest;
    }

    /// <summary>局所変数に隠されていない、大域の FLOAT のスカラー変数か (A*A → f24sqr などの特殊処理の条件)</summary>
    private bool IsGlobalFloatScalar(IdentifierExpr id)
        => !(_localVars?.ContainsKey(id.Name) ?? false) && ResolveConstOrGlobal(id.Name)?.Type?.ByteSize == 3
           && IdentifierIsFloat(id);

    /// <summary>VisitIdentifier の結果が FLOAT になるか (同じ順に解決する: 局所変数 → 定数 → コードブロック → 変数)</summary>
    private bool IdentifierIsFloat(IdentifierExpr id)
    {
        if (_localVars != null && _localVars.TryGetValue(id.Name, out var li))
            return li.Kind == VarKind.Scalar && li.ByteSize == 3;   // 配列はアドレス、ポインタは 2 バイト
        var sym = ResolveConstOrGlobal(id.Name);
        if (sym is { Kind: SymbolKind.Constant }) return sym.ConstValue is not int && sym.ConstFloatValue.HasValue;
        if (sym is { IsCodeBlock: true }) return false;
        var vi = ResolveVarInfo(id.Name);
        return vi.Kind != VarKind.Array && vi.VarDataSize == 3;
    }

    /// <summary>
    /// FLOAT の定数の四則演算を正しい値で畳み込んでよい二項演算 (代入する値の中で、値がそのまま代入に届くもの)。
    /// ここにない二項演算 (条件・添字・関数の引数・ビット演算の中など) は、今までどおり整数として畳み込む
    /// </summary>
    private readonly HashSet<BinaryExpr> _floatFoldNodes = new(ReferenceEqualityComparer.Instance);

    /// <summary>
    /// 代入先が FLOAT でない代入の値の中の二項演算 (FoldFloatArithmetic は整数の結果だけを使う。FLOAT の値は作らない)
    /// </summary>
    private readonly HashSet<BinaryExpr> _intFoldNodes = new(ReferenceEqualityComparer.Instance);

    /// <summary>
    /// 代入する値の式をたどり、値がそのまま代入に届く二項演算に印を付ける。たどるのは、子の値をそのまま使う四則演算の
    /// 両辺・単項の +/-・キャスト・カンマ式の右・代入式の値・CodeEvalExpr の中身だけ。
    /// floatTarget は、値が届く代入先がすべて FLOAT の変数・FLOAT の配列の要素か (真なら _floatFoldNodes、偽なら _intFoldNodes)。
    /// MEM・PORT への代入は FLOAT からの変換がなく、WORD の変数への代入は FTOI で値を変えてから外側の代入に渡すので、
    /// FLOAT の値を届けてはいけない
    /// </summary>
    private void MarkFloatFold(Expression e, bool floatTarget)
    {
        switch (e)
        {
            case BinaryExpr b when b.Op is BinaryOp.Add or BinaryOp.Sub or BinaryOp.Mul or BinaryOp.Div
                                         or BinaryOp.SMul or BinaryOp.SDiv:
                (floatTarget ? _floatFoldNodes : _intFoldNodes).Add(b);
                MarkFloatFold(b.Left, floatTarget);
                MarkFloatFold(b.Right, floatTarget);
                break;
            case UnaryExpr { Op: UnaryOp.Negate or UnaryOp.Plus } u: MarkFloatFold(u.Operand, floatTarget); break;
            case CastExpr c: MarkFloatFold(c.Operand, floatTarget); break;
            case CommaExpr c: MarkFloatFold(c.Right, floatTarget); break;
            case AssignExpr a: MarkFloatFold(a.Value, floatTarget && IsFloatTarget(a.Target)); break;
            case CodeEvalExpr ce: MarkFloatFold(ce.Inner, floatTarget); break;
            case ConditionalExpr c: MarkFloatFold(c.TrueExpr, floatTarget); MarkFloatFold(c.FalseExpr, floatTarget); break;   // ?: の両側の値 (条件は除く)
        }
    }

    /// <summary>代入先が FLOAT の変数 (スカラー) か、FLOAT の配列・ポインタの要素か (EmitStore が 3 バイトで書く所)</summary>
    private bool IsFloatTarget(Expression target)
    {
        if (target is IdentifierExpr id)
        {
            var vi = ResolveVarInfo(id.Name);
            return vi.IsResolved && vi.Kind == VarKind.Scalar && vi.VarDataSize == 3;
        }
        if (target is ArrayAccessExpr { Array: IdentifierExpr an })
        {
            if (an.Name.Equals("PORT", StringComparison.OrdinalIgnoreCase) || an.Name.Equals("PORTW", StringComparison.OrdinalIgnoreCase))
                return false;
            var vi = ResolveVarInfo(an.Name);
            if (vi.GlobalSym?.Type is MemoryArrayType) return false;
            return vi.IsResolved && vi.Kind is VarKind.Array or VarKind.Pointer && vi.ElemSize == 3;
        }
        return false;
    }

    /// <summary>
    /// 実行時に計算する FLOAT の演算の辺で、FLOAT の値が要る二項演算 (整数の結果も FLOAT の定数にする)。
    /// 子の値をそのまま使う辺 (単項の +/-・キャスト・カンマ式の右・CodeEvalExpr) をたどって入れる
    /// </summary>
    private readonly HashSet<BinaryExpr> _floatRequiredNodes = new(ReferenceEqualityComparer.Instance);

    private void RequireFloat(Expression e)
    {
        switch (e)
        {
            case BinaryExpr b: _floatRequiredNodes.Add(b); break;
            case UnaryExpr { Op: UnaryOp.Negate or UnaryOp.Plus } u: RequireFloat(u.Operand); break;
            case CastExpr c: RequireFloat(c.Operand); break;
            case CommaExpr c: RequireFloat(c.Right); break;
            case ConditionalExpr c: RequireFloat(c.TrueExpr); RequireFloat(c.FalseExpr); break;
            case CodeEvalExpr ce: RequireFloat(ce.Inner); break;
        }
    }

    /// <summary>
    /// 何もしない演算の正規化で見る辺の整数の値。実行時に計算する定数の演算で FLOAT が関わる辺は見ない (null)。
    /// FLOAT の値が要る所 (floatValue) で FLOAT が関わる辺は、整数として正しく、符号付きで FLOAT に戻しても同じ値のときだけ見る
    /// (小数の値・f24 で正確に表せない値 (実行時に計算する) の辺は見ない)
    /// </summary>
    private static int? NormalizationConst(ConstEvaluator ce, Expression side, bool runtimeFold, bool floatValue)
    {
        if (!ce.InvolvesFloat(side)) return ce.Evaluate(side);
        if (runtimeFold) return null;
        if (!floatValue) return ce.Evaluate(side);
        if (ce.FoldFloatArithmetic(side) is not { } fold) return ce.Evaluate(side);   // 四則でない辺 (比較など) は今までどおり
        return fold.Kind == ConstEvaluator.FloatFoldKind.Int && (short)fold.IntValue == fold.FloatValue ? fold.IntValue : null;
    }

    public IrOperand VisitUnaryExpr(UnaryExpr node)
    {
        bool discarded = TakeDiscard();   // 単項の + は値をそのまま返すので、値を使わない印を中へ渡す
        // 定数畳み込み: リテラルに対する単項演算をコンパイル時に計算
        if (node.Operand is IntegerLiteral numLit)
        {
            int v = (int)numLit.Value;
            int? folded = node.Op switch
            {
                UnaryOp.Plus => v,
                UnaryOp.Negate => (-v) & 0xFFFF,
                UnaryOp.Not => (v != 0) ? 0 : 1,
                UnaryOp.Cpl => (~v) & 0xFFFF,
                _ => null,
            };
            if (folded.HasValue)
            {
                var dest = IrOperand.Temp(AllocTemp());
                Emit(IrOp.LoadConst, dest, IrOperand.Imm(folded.Value));
                return dest;
            }
        }
        // FLOAT即値の単項マイナスは符号反転して定数化
        if (node.Op == UnaryOp.Negate && node.Operand is FloatLiteral fLit)
        {
            return EmitLoadFloatConst(-fLit.Value);
        }

        _valueDiscarded = discarded && node.Op == UnaryOp.Plus && TakesDiscard(node.Operand);
        var operand = node.Operand.Accept(this);
        if (node.Op == UnaryOp.Plus) return operand;

        int operandDs = operand.Kind == IrOperandKind.Temp
            && _tempDataSize.TryGetValue(operand.TempIndex, out int ods) ? ods : 2;

        var dest2 = IrOperand.Temp(AllocTemp());
        var op = node.Op switch
        {
            UnaryOp.Negate => IrOp.Neg,
            UnaryOp.Not => IrOp.LogNot,
            UnaryOp.Cpl => IrOp.Not,
            _ => IrOp.Nop,
        };
        // FLOATの単項マイナスは f24 符号ビットを反転 (DataSize=3 で識別)
        int destDs = (op == IrOp.Neg && operandDs == 3) ? 3 : 2;
        Emit(op, dest2, operand, dataSize: destDs);
        if (destDs == 3) _tempDataSize[dest2.TempIndex] = 3;
        return dest2;
    }

    public IrOperand VisitAssignExpr(AssignExpr node)
    {
        bool discarded = TakeDiscard();
        var value = node.Value.Accept(this);
        // 代入式の値は、書いた値 (代入先の型に変えたもの。BYTE は上位を切る前の値)
        var stored = EmitStore(node.Target, value);
        return discarded ? stored : ValueAfterStore(stored);
    }

    public IrOperand VisitCompoundAssignExpr(CompoundAssignExpr node)
    {
        bool discarded = TakeDiscard();
        CheckTargetEvaluatedTwice(node.Target, node.Value, node.Span);
        // FLOAT の代入先: F = F op 値 と同じ式 (FLOAT の四則演算) で作る
        // (整数の演算で足して、その結果を整数から FLOAT に変えていた)
        if (IsFloatTarget(node.Target) && node.Op is CompoundAssignOp.AddAssign or CompoundAssignOp.SubAssign
                or CompoundAssignOp.MulAssign or CompoundAssignOp.DivAssign)
        {
            var binOp = node.Op switch
            {
                CompoundAssignOp.AddAssign => BinaryOp.Add,
                CompoundAssignOp.SubAssign => BinaryOp.Sub,
                CompoundAssignOp.MulAssign => BinaryOp.Mul,
                _ => BinaryOp.Div,
            };
            var floatValue = new BinaryExpr(binOp, node.Target, node.Value, node.Span);
            MarkFloatFold(floatValue, true);   // 代入の文と同じく、FLOAT の定数の四則演算を正しい値で畳み込む
            _valueDiscarded = discarded;
            return VisitAssignExpr(new AssignExpr(node.Target, floatValue, node.Span));
        }

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
        var stored = EmitStore(node.Target, dest);
        return discarded ? stored : ValueAfterStore(stored);
    }

    public IrOperand VisitIncrementExpr(IncrementExpr node)
    {
        // 値を使わない (式の文・カンマ式の左) か。中の式 (T[I++]++ の I++) には引き継がない
        bool valueUnused = TakeDiscard();
        CheckTargetEvaluatedTwice(node.Operand, null, node.Span);

        // FLOAT: F = F + 1 と同じ式 (FLOAT の足し算) で作る。後置の値は、新しい値から 1.0 を引いても
        // 古い値に戻らないことがあるので、使えない
        if (IsFloatTarget(node.Operand))
        {
            if (!node.IsPrefix && !valueUnused)
                _diagnostics.Error("The value of postfix ++/-- on FLOAT cannot be used (use prefix ++/--, or a separate statement)",
                    node.Span);
            _valueDiscarded = valueUnused;
            return VisitAssignExpr(new AssignExpr(node.Operand,
                new BinaryExpr(node.IsIncrement ? BinaryOp.Add : BinaryOp.Sub, node.Operand,
                    new IntegerLiteral(1, node.Span), node.Span), node.Span));
        }

        var val = node.Operand.Accept(this);
        var one = IrOperand.Temp(AllocTemp());
        Emit(IrOp.LoadConst, one, IrOperand.Imm(1));
        var result = IrOperand.Temp(AllocTemp());
        Emit(node.IsIncrement ? IrOp.Add : IrOp.Sub, result, val, one);
        var stored = EmitStore(node.Operand, result);

        if (valueUnused) return node.IsPrefix ? stored : val;   // 値を使わない: いままでどおり
        var newValue = ValueAfterStore(stored);
        if (node.IsPrefix) return newValue;

        // 後置で値を使う: 古い値 (val) は、足し引きのときに HL の上で書き換わって残らないので、書いた後に
        // 新しい値から 1 を引いて (-- なら足して) 作り直す。16 ビットで回るので必ず古い値に戻る
        // (BYTE も、新しい値は上位を切る前の HL のまま)
        var one2 = IrOperand.Temp(AllocTemp());
        Emit(IrOp.LoadConst, one2, IrOperand.Imm(1));
        var old = IrOperand.Temp(AllocTemp());
        Emit(node.IsIncrement ? IrOp.Sub : IrOp.Add, old, newValue, one2);
        return old;
    }

    public IrOperand VisitCallExpr(CallExpr node)
    {
        var funcName = (node.Function as IdentifierExpr)?.Name;
        var funcSym = funcName != null ? _globalSymbols?.Resolve(funcName) : null;

        // CFUNC 呼出は Z80 backend では未対応 (C backend 専用)。
        // `#IF BACKEND==1` で gate していない場合に発見できるよう診断 error。
        if (funcSym?.Kind == SymbolKind.CFunction)
        {
            _diagnostics.Error(
                $"call to CFUNC `{funcName}` is not supported by Z80 backend; "
                + "gate the call with `#IF BACKEND==1` or `#IF ENV_TYPE==7`",
                node.Span);
            return IrOperand.Imm(0);
        }

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
            // ランタイムの関数そのものへの呼び出し (記号表にない名前) だけ、ランタイムの注釈 (引数の型・戻り値の型) を使う。
            // 同じ名前の MACHINE を宣言していれば、その宣言の取り決めのまま
            Runtime.RuntimeFunction? rtFunc = funcSym == null && funcName != null
                && _runtimeManager?.Functions.TryGetValue(funcName, out var rf) == true ? rf : null;
            var paramTypes = rtFunc?.ParamTypes;
            if (paramTypes != null && paramTypes.Count != node.Arguments.Count)
                _diagnostics.Error($"Function '{funcName}' expects {paramTypes.Count} arguments, got {node.Arguments.Count}",
                    node.Span);
            // FLOAT の引数 2 つ (FPOW・FLOGY): x を AHL、y を CDE で渡す。どちらも 3 バイトで積む
            bool twoFloats = paramTypes is ["float", "float"] && node.Arguments.Count == 2;

            // MACHINE関数: レジスタ渡し (0:CALL, 1:HL, 2:HL+DE, 3:HL+DE+BC)
            // 各引数を評価した直後にPushArgしてスタックに退避
            for (int i = 0; i < node.Arguments.Count; i++)
            {
                string? paramType = paramTypes != null && i < paramTypes.Count ? paramTypes[i] : null;
                if (paramType == "float") MarkFloatFold(node.Arguments[i], true);
                var argVal = node.Arguments[i].Accept(this);
                if (paramType == "float")
                    argVal = EmitTypeConversionExpand(argVal, 3);
                else if (paramType != null && TempDataSize(argVal) == 3)
                    _diagnostics.Error("Cannot pass FLOAT value to non-FLOAT parameter (use FTOI for explicit conversion)",
                        node.Arguments[i].Span);
                // 注釈のない MACHINE・ランタイムの関数に FLOAT を渡せるのは、ただ 1 つの引数 (AHL のまま渡る) のときだけ
                // (2 つ目からは A が壊れる。積む数と取り出す数 (宣言の引数の数) がどちらも 1 のとき)
                else if (paramType == null && TempDataSize(argVal) == 3
                         && (node.Arguments.Count != 1 || machineParamCount.Value != 1))
                    _diagnostics.Error($"A FLOAT value can be passed to MACHINE '{funcName}' only as its single argument "
                        + "(use FTOI to make it an integer)", node.Arguments[i].Span);
                Emit(IrOp.PushArg, argVal, IrOperand.Imm(i), dataSize: twoFloats ? 3 : 2);
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
                asmName = funcSym.AddressExpr ?? funcSym.AsmLabel ?? LabelUtils.FunctionLabel(funcName!);
                if (funcSym.AddressExprDeps != null)
                    foreach (var dep in funcSym.AddressExprDeps)
                        _module.AddressSymbolDeps.Add(dep);
            }
            else
            {
                asmName = funcSym?.AsmLabel ?? LabelUtils.FunctionLabel(funcName!);
            }
            // ランタイム関数のFLOAT戻り値: DataSizeを3に設定（CodeGeneratorがPUSH AF+PUSH HLを生成するため）
            int resultDs = rtFunc?.ResultType == "float" ? 3 : 2;
            Emit(IrOp.Call, dest, IrOperand.Sym(asmName), IrOperand.Imm(machineParamCount.Value), dataSize: resultDs);
            if (twoFloats) CurrentInstructionList()[^1].ArgSizes = [3, 3];
            if (resultDs == 3)
                _tempDataSize[dest.TempIndex] = 3;
            return dest;
        }
        else
        {
            // ユーザー関数: PushArgでスタックに退避してからCALL
            // 引数評価中に別の関数呼び出しがIYワークを壊す問題の防止
            // （MACHINE関数と同じPushArg方式、codegenでPOP→IY格納に展開）
            var funcType = funcSym?.Type as FunctionType;
            var paramTypes = funcType?.ParameterTypes;
            var returnType = funcType?.ReturnType;
            int returnDs = (returnType is PrimitiveType pt && pt.Kind == PrimitiveKind.Float) ? 3 : 2;

            // 引数個数チェック (型情報がある関数のみ)
            if (paramTypes != null && paramTypes.Count != node.Arguments.Count)
            {
                _diagnostics.Error(
                    $"Function '{funcName}' expects {paramTypes.Count} arguments, got {node.Arguments.Count}",
                    node.Span);
            }

            var argSizes = new int[node.Arguments.Count];
            for (int i = 0; i < node.Arguments.Count; i++)
            {
                // パラメータ型から targetDs を決定 (FLOAT=3, それ以外=2)
                int targetDs = 2;
                if (paramTypes != null && i < paramTypes.Count
                    && paramTypes[i] is PrimitiveType ppt && ppt.Kind == PrimitiveKind.Float)
                    targetDs = 3;

                // FLOAT の引数の値は、代入の値と同じく FLOAT の定数の演算を正しい値で畳み込む
                if (targetDs == 3) MarkFloatFold(node.Arguments[i], true);
                var argVal = node.Arguments[i].Accept(this);
                int valueDs = argVal.Kind == IrOperandKind.Temp
                    && _tempDataSize.TryGetValue(argVal.TempIndex, out int vds) ? vds : 2;

                // FLOAT → WORD (縮小) はエラー
                if (valueDs == 3 && targetDs != 3)
                {
                    _diagnostics.Error(
                        "Cannot pass FLOAT value to non-FLOAT parameter (use FTOI for explicit conversion)",
                        node.Arguments[i].Span);
                }
                // 拡大方向 (WORD→FLOAT) のみ自動変換
                argVal = EmitTypeConversionExpand(argVal, targetDs);
                Emit(IrOp.PushArg, argVal, IrOperand.Imm(i), dataSize: targetDs);
                argSizes[i] = targetDs;
            }

            var dest = IrOperand.Temp(AllocTemp());
            var asmName = funcSym?.AsmLabel ?? LabelUtils.FunctionLabel(funcName ?? "__indirect_call");
            // 負の引数数 = ユーザー関数のIY渡し（0は引数なしで共通）
            // ArgSizes はユーザー関数 (callArgMode<0) と、MACHINE の FLOAT 2 つの呼び出しに設定
            var callInst = new IrInstruction(IrOp.Call, dest, IrOperand.Sym(asmName),
                IrOperand.Imm(node.Arguments.Count > 0 ? -node.Arguments.Count : 0))
            {
                DataSize = returnDs,
                ArgSizes = node.Arguments.Count > 0 ? argSizes : null,
            };
            _currentFunction!.Instructions.Add(callInst);
            if (returnDs == 3)
                _tempDataSize[dest.TempIndex] = 3;
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

        // ResolveVarInfoで種別・要素サイズを統合判定
        var arrVi = arrayName != null ? CheckDefined(arrayName, node.Span) : default;
        bool isIndirect = arrVi.Kind == VarKind.Pointer;
        bool isIndirectByte = isIndirect && arrVi.ElemSize == 1;
        bool isArrayByte = arrVi.Kind == VarKind.Array && arrVi.ElemSize == 1;

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
            int elemSize = arrVi.ElemSize;
            IrOperand scaledIdx = ScaleIndexByElemSize(idx, elemSize);
            var addr = IrOperand.Temp(AllocTemp());
            Emit(IrOp.Add, addr, baseAddr, scaledIdx);

            // アドレスから値を読む
            var dest = IrOperand.Temp(AllocTemp());
            Emit(IrOp.IndirLoad, dest, addr, dataSize: elemSize);
            if (elemSize == 3) _tempDataSize[dest.TempIndex] = 3;
            return dest;
        }
        else
        {
            // 通常配列: アドレス計算して値をロード
            return ComputeArrayAccess(node, loadValue: true);
        }
    }

    /// <summary>
    /// 配列要素のアドレス計算（共通helper）。
    /// loadValue=true: アドレスから値をロードして返す（通常の配列アクセス）
    /// loadValue=false: アドレスだけ返す（&amp;array[idx]用）
    /// </summary>
    private IrOperand ComputeArrayAccess(ArrayAccessExpr node, bool loadValue)
    {
        var arrayName = (node.Array as IdentifierExpr)?.Name;
        // 呼び出し元(VisitArrayAccessExpr/VisitAddressOfExpr)でCheckDefined済み
        var vi = arrayName != null ? ResolveVarInfo(arrayName) : default;
        var arraySym = vi.GlobalSym;
        var arrInfo = vi.Local;
        bool isArrayByte = vi.Kind != VarKind.Scalar && vi.ElemSize == 1;

        // 各次元のストライドを計算
        List<int> strides;
        bool isLocalArray = arrInfo != null && arrInfo.Kind == VarKind.Array && arrInfo.Dims != null;
        if (isLocalArray)
        {
            strides = ComputeStridesFromDims(arrInfo!.Dims!, arrInfo.ElemSize, node.Indices.Count);
            isArrayByte = arrInfo.ElemSize == 1;
        }
        else
        {
            strides = ComputeStrides(arraySym, node.Indices.Count, arrayName);
        }

        int elemSize = vi.ElemSize;

        // 部分配列参照: 指定インデックス数 < 配列の次元数 → アドレスを返す
        int arrayRank = 0;
        if (isLocalArray && arrInfo?.Dims != null)
            arrayRank = arrInfo.Dims.Count;
        else if (arraySym?.Type is ArrayType atRank)
            arrayRank = atRank.Rank;
        if (arrayRank > 0 && node.Indices.Count < arrayRank)
            loadValue = false;

        // ローカル配列の定数インデックス最適化
        if (isLocalArray)
        {
            var directOffset = TryComputeLocalArrayOffset(arrInfo!, node.Indices, strides, elemSize);
            if (directOffset.HasValue)
            {
                var result = IrOperand.Temp(AllocTemp());
                if (loadValue)
                {
                    Emit(IrOp.LoadLocal, result, IrOperand.Imm(directOffset.Value), dataSize: elemSize);
                    if (elemSize == 3) _tempDataSize[result.TempIndex] = 3;
                }
                else
                {
                    // アドレスのみ: IY+offset のアドレスを生成
                    Emit(IrOp.InlineAsm, result, IrOperand.Asm(
                        $"\tPUSH\tIY\n\tPOP\tHL\n\tLD\tDE,${directOffset.Value:X4}\n\tADD\tHL,DE"));
                }
                return result;
            }
        }

        // グローバル/static配列の定数インデックス最適化
        if (!isLocalArray && arrayName != null && vi.Kind == VarKind.Array)
        {
            var globalOffset = TryComputeConstArrayOffset(node.Indices, strides);
            if (globalOffset.HasValue)
            {
                var result = IrOperand.Temp(AllocTemp());
                string label = ResolveAsmLabel(arrayName);
                string sym = globalOffset.Value == 0 ? label : $"{label}+{globalOffset.Value}";
                if (loadValue)
                {
                    Emit(IrOp.LoadVar, result, IrOperand.Sym(sym), dataSize: elemSize);
                    if (elemSize == 3) _tempDataSize[result.TempIndex] = 3;
                }
                else
                    Emit(IrOp.LoadAddr, result, IrOperand.Sym(sym));
                return result;
            }
        }

        // 配列のベースアドレスをロード
        IrOperand baseAddr;
        if (arrayName != null)
        {
            baseAddr = IrOperand.Temp(AllocTemp());
            if (isLocalArray)
            {
                Emit(IrOp.Comment, IrOperand.Asm($"local array {arrayName} addr"));
                Emit(IrOp.InlineAsm, baseAddr, IrOperand.Asm($"\tPUSH\tIY\n\tPOP\tHL\n\tLD\tDE,${arrInfo!.Offset:X4}\n\tADD\tHL,DE"));
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

        // 各次元のインデックス×ストライドを加算
        var addr = baseAddr;
        for (int i = 0; i < node.Indices.Count; i++)
        {
            var idx = node.Indices[i].Accept(this);
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

        if (loadValue)
        {
            var result = IrOperand.Temp(AllocTemp());
            Emit(IrOp.IndirLoad, result, addr, dataSize: elemSize);
            if (elemSize == 3) _tempDataSize[result.TempIndex] = 3;
            return result;
        }
        return addr; // アドレスのみ
    }

    public IrOperand VisitConditionalExpr(ConditionalExpr node)
    {
        // どちらの側も値を作るだけで、値は HL (FLOAT は AHL) に残る。合流のラベルの後の JoinValue で
        // 「結果は HL にある」とし、積むかどうかはその 1 か所で決める (側ごとに決めると食い違う)。
        // 結果の大きさは、両側の値の実際の大きさで決める (どちらかが FLOAT なら FLOAT。もう片方を i16tof24 で広げる。
        // 真の側の変換は、偽の側を作った後で、真の側の終わり (合流へ飛ぶ前) に入れる)
        var falseLabel = NewLabel();
        var endLabel = NewLabel();

        var cond = node.Condition.Accept(this);
        Emit(IrOp.JumpIfZero, IrOperand.Lbl(falseLabel), cond);

        var trueVal = node.TrueExpr.Accept(this);
        var list = CurrentInstructionList();
        int trueEnd = list.Count;
        Emit(IrOp.Jump, IrOperand.Lbl(endLabel));

        Emit(IrOp.Label, IrOperand.Lbl(falseLabel));
        var falseVal = node.FalseExpr.Accept(this);

        int ds = TempDataSize(trueVal) == 3 || TempDataSize(falseVal) == 3 ? 3 : 2;
        if (ds == 3 && TempDataSize(falseVal) != 3)
        {
            var conv = IrOperand.Temp(AllocTemp());
            EmitConversionCall(conv, "i16tof24", falseVal, dataSize: 3);
            _tempDataSize[conv.TempIndex] = 3;
            falseVal = conv;
        }
        if (ds == 3 && TempDataSize(trueVal) != 3)
        {
            var conv = IrOperand.Temp(AllocTemp());
            list.Insert(trueEnd, new IrInstruction(IrOp.Call, conv, IrOperand.Sym("i16tof24"), IrOperand.Imm(0))
                { DataSize = 3, ImplicitSrc = trueVal });
            _tempDataSize[conv.TempIndex] = 3;
            trueVal = conv;
        }

        Emit(IrOp.Label, IrOperand.Lbl(endLabel));
        var result = IrOperand.Temp(AllocTemp());
        // src に両側の値を置く (値を HL に残すことを、コード生成の「ほかで使うか」の判断に伝える)
        Emit(IrOp.JoinValue, result, trueVal, falseVal, dataSize: ds);
        if (ds == 3) _tempDataSize[result.TempIndex] = 3;
        return result;
    }

    private static bool IsComparisonOp(BinaryOp op) => op is BinaryOp.Eq or BinaryOp.Neq or BinaryOp.Lt or BinaryOp.Gt
        or BinaryOp.Le or BinaryOp.Ge or BinaryOp.SLt or BinaryOp.SGt or BinaryOp.SLe or BinaryOp.SGe;

    private int TempDataSize(IrOperand v)
        => v.Kind == IrOperandKind.Temp && _tempDataSize.TryGetValue(v.TempIndex, out int d) ? d : 2;

    /// <summary>いま命令を積んでいる列 (Emit と同じ)</summary>
    private List<IrInstruction> CurrentInstructionList()
        => _emitToGlobalData || _currentFunction == null ? _module.GlobalData : _currentFunction.Instructions;

    public IrOperand VisitCommaExpr(CommaExpr node)
    {
        // 左の値は使わない。右の値は、カンマ式そのものの値を使わないとき (式の文など) だけ使わない
        bool discarded = TakeDiscard();
        _valueDiscarded = TakesDiscard(node.Left);
        node.Left.Accept(this);
        _valueDiscarded = discarded && TakesDiscard(node.Right);
        return node.Right.Accept(this);
    }

    /// <summary>&MEM[x]・&PORT[x] の x (命令を作らずにそのまま番地になるので、ここで FLOAT を止める)</summary>
    private IrOperand IntegerAddress(Expression e)
    {
        var v = e.Accept(this);
        if (TempDataSize(v) == 3)
            _diagnostics.Error("A FLOAT value cannot be used as an address (use FTOI to make it an integer)", e.Span);
        return v;
    }

    public IrOperand VisitAddressOfExpr(AddressOfExpr node)
    {
        if (node.Operand is IdentifierExpr id)
        {
            var t = IrOperand.Temp(AllocTemp());
            // ローカル変数/パラメータ: IY+offsetのアドレスを計算
            if (_localVars != null && _localVars.TryGetValue(id.Name, out var localInfo))
            {
                Emit(IrOp.InlineAsm, t, IrOperand.Asm(
                    $"\tPUSH\tIY\n\tPOP\tHL\n\tLD\tDE,${localInfo.Offset:X4}\n\tADD\tHL,DE"));
                return t;
            }
            CheckDefined(id.Name, id.Span);
            Emit(IrOp.LoadAddr, t, IrOperand.Sym(ResolveAsmLabel(id.Name)));
            return t;
        }
        if (node.Operand is ArrayAccessExpr arr)
        {
            var arrName = (arr.Array as IdentifierExpr)?.Name;
            var arrSym = arrName != null ? _globalSymbols?.Resolve(arrName) : null;

            // &MEM[x] / &SOS[x] → アドレスxそのもの
            if (arrSym?.Type is MemoryArrayType)
                return IntegerAddress(arr.Indices[0]);

            // &PORT[x] → 意味がないがアドレスxを返す
            if (arrName != null && (arrName.Equals("PORT", StringComparison.OrdinalIgnoreCase)
                || arrName.Equals("PORTW", StringComparison.OrdinalIgnoreCase)))
                return IntegerAddress(arr.Indices[0]);

            // 間接変数: &p[i] → p + i*elemSize のアドレス
            var addrVi = arrName != null ? CheckDefined(arrName, arr.Span) : default;
            if (addrVi.Kind == VarKind.Pointer)
            {
                var baseAddr = arr.Array.Accept(this); // ポインタ値
                var idx = arr.Indices[0].Accept(this);
                IrOperand scaledIdx = ScaleIndexByElemSize(idx, addrVi.ElemSize);
                var addr = IrOperand.Temp(AllocTemp());
                Emit(IrOp.Add, addr, baseAddr, scaledIdx);
                return addr; // アドレスのみ（IndirLoadなし）
            }

            // 通常配列: アドレスのみ返す
            return ComputeArrayAccess(arr, loadValue: false);
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

    /// <summary>CODE の中で実行時に作る値。FLOAT は扱えない (CODE の値は HL の整数として使われる)</summary>
    private void CodeValue(Expression e)
    {
        if (TempDataSize(e.Accept(this)) == 3)
            _diagnostics.Error("A FLOAT value cannot be used in CODE (use FTOI to make it an integer)", e.Span);
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
                CodeValue(eval.Inner);
            }
            else if (v is CodeLabelRef labelRef)
            {
                // <ラベル> → ラベルアドレスを2バイトで埋め込み
                Emit(IrOp.DefWord, IrOperand.Lbl(labelRef.Label));
            }
            else if (v is CastExpr cast)
            {
                // 型,定数式 → BYTE: 1バイト, WORD: 2バイト
                var constVal = _globalSymbols != null ? NewConstEvaluator().Evaluate(cast.Operand) : null;
                if (constVal.HasValue)
                {
                    if (cast.TargetSize == DataSize.Byte)
                        Emit(IrOp.DefByte, IrOperand.Imm(constVal.Value & 0xFF));
                    else
                        Emit(IrOp.DefWord, IrOperand.Imm(constVal.Value & 0xFFFF));
                }
                else if (cast.TargetSize == DataSize.Word)
                {
                    // 非定数WORD: ラベル式としてDWで埋め込みを試みる
                    var asmResult = LabelUtils.ExprToAsmString(cast.Operand, _globalSymbols, _diagnostics, _localConsts);
                    if (asmResult.HasValue)
                    {
                        Emit(IrOp.DefWord, IrOperand.Lbl(asmResult.Value.Expr));
                        foreach (var dep in asmResult.Value.Deps)
                            _module.AddressSymbolDeps.Add(dep);
                    }
                    else
                    {
                        CodeValue(cast.Operand); // フォールバック: 実行時コード
                    }
                }
                else
                {
                    CodeValue(cast.Operand); // 非定数BYTE→実行時コード
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
                CodeValue(v);
            }
        }

        // CODE関数の値 = 実行後のHLの値
        var dest = IrOperand.Temp(AllocTemp());
        return dest;
    }

    public IrOperand VisitCastExpr(CastExpr node)
    {
        _valueDiscarded = TakeDiscard() && TakesDiscard(node.Operand);   // 値をそのまま返すので、値を使わない印を中へ渡す
        var v = node.Operand.Accept(this);
        if (TempDataSize(v) == 3)   // キャストは値を変えない (FLOAT の値を整数にしない)
            _diagnostics.Error("A type cast does not convert a FLOAT value (use FTOI or an assignment)", node.Span);
        return v;
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
        int? addr = EvaluateAddress(node.Value);
        if (addr.HasValue) _module.OrgAddress = addr.Value;
        return IrOperand.None;
    }

    public IrOperand VisitWorkDirective(WorkDirective node)
    {
        int? addr = EvaluateAddress(node.Value);
        if (!addr.HasValue) return IrOperand.None;

        if (_currentOverlay != null)
            _currentOverlay.WorkAddress = addr.Value;
        else
            _module.WorkAddress = addr.Value;
        return IrOperand.None;
    }

    /// <summary>ORG/WORK/OFFSET 用の定数式評価 (IntegerLiteral の高速パス付き)。</summary>
    private int? EvaluateAddress(Expression expr)
    {
        if (expr is IntegerLiteral lit) return (int)lit.Value;
        if (_globalSymbols == null) return null;
        var ce = NewConstEvaluator();
        return ce.Evaluate(expr);
    }

    public IrOperand VisitOffsetDirective(OffsetDirective node)
    {
        int? addr = EvaluateAddress(node.Value);
        if (addr.HasValue) _module.OffsetAddress = addr.Value;
        return IrOperand.None;
    }

    public IrOperand VisitModuleBlock(ModuleBlock node)
    {
        // オーバーレイモジュール: 別のコンテキストに切り替えてIR生成
        // シンボルテーブルは共有のまま（メイン部と相互参照可能）

        int orgAddr = EvaluateAddress(node.Name) ?? 0;

        // ヘッダ位置のランタイムポリシーを IR enum にミラー
        var policy = node.RuntimePolicy switch
        {
            Parser.Ast.OverlayRuntimePolicy.Local       => OverlayRuntimePolicy.Local,
            Parser.Ast.OverlayRuntimePolicy.Resident    => OverlayRuntimePolicy.Resident,
            Parser.Ast.OverlayRuntimePolicy.SelfContain => OverlayRuntimePolicy.SelfContain,
            Parser.Ast.OverlayRuntimePolicy.Auto        => OverlayRuntimePolicy.Auto,
            _ => OverlayRuntimePolicy.Local,
        };

        // 未実装ポリシーは現時点ではコンパイルエラー (enum は予約のみ)
        if (policy == OverlayRuntimePolicy.SelfContain || policy == OverlayRuntimePolicy.Auto)
        {
            _diagnostics?.Error(
                $"#MODULE policy '{node.RuntimePolicy}' is not implemented yet (use RESIDENT or omit)",
                node.Span);
            policy = OverlayRuntimePolicy.Local; // フォールバック (回復のため)
        }

        var overlay = new OverlayModule
        {
            Index = _module.Overlays.Count,
            OrgAddress = orgAddr,
            RuntimePolicy = policy,
        };

        // VAR/ARRAY/WORK の振り分け先を切り替え
        var prevOverlay = _currentOverlay;
        var prevOverlayLookup = _currentOverlayLookup;
        _currentOverlay = overlay;
        _currentOverlayLookup = new Dictionary<string, GlobalVarInfo>(StringComparer.OrdinalIgnoreCase);

        try
        {
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
                    overlay.Functions.Add(func);
                }
            }
        }
        finally
        {
            _currentOverlay = prevOverlay;
            _currentOverlayLookup = prevOverlayLookup;
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

    /// <summary>代入先の型に合わせてFLOAT変換を挿入 (双方向: WORD↔FLOAT)。
    /// 代入文での暗黙縮小 (FLOAT→WORD) は FTOI を自動挿入する。</summary>
    private IrOperand EmitTypeConversion(IrOperand value, int targetDs)
    {
        int valueDs = value.Kind == IrOperandKind.Temp && _tempDataSize.TryGetValue(value.TempIndex, out int vds) ? vds : 2;
        if (targetDs == 3 && valueDs != 3)
        {
            // Word→Float変換
            var conv = IrOperand.Temp(AllocTemp());
            EmitConversionCall(conv, "i16tof24", value, dataSize: 3);
            _tempDataSize[conv.TempIndex] = 3;
            return conv;
        }
        else if (targetDs != 3 && valueDs == 3)
        {
            // Float→Word変換
            var conv = IrOperand.Temp(AllocTemp());
            EmitConversionCall(conv, "FTOI", value);
            _tempDataSize[conv.TempIndex] = 2;
            return conv;
        }
        return value;
    }

    /// <summary>関数引数/戻り値用: 拡大方向 (WORD→FLOAT) のみ自動変換。
    /// 縮小方向 (FLOAT→WORD) はそのまま返す (呼び出し側で別途エラー判定)。</summary>
    private IrOperand EmitTypeConversionExpand(IrOperand value, int targetDs)
    {
        int valueDs = value.Kind == IrOperandKind.Temp && _tempDataSize.TryGetValue(value.TempIndex, out int vds) ? vds : 2;
        if (targetDs == 3 && valueDs != 3)
        {
            // Word → Float: i16tof24 を挿入
            var conv = IrOperand.Temp(AllocTemp());
            EmitConversionCall(conv, "i16tof24", value, dataSize: 3);
            _tempDataSize[conv.TempIndex] = 3;
            return conv;
        }
        // 同サイズ or 縮小方向はそのまま (縮小は呼び出し側でエラー)
        return value;
    }

    /// <summary>関数戻り値の型変換 (拡大のみ、縮小はエラー)。
    /// _currentReturnDataSize に従って RETURN/END(expr) の値を変換する。</summary>
    private IrOperand ConvertReturnValue(IrOperand val, SourceSpan errSpan)
    {
        int returnDs = _currentReturnDataSize;
        int valueDs = val.Kind == IrOperandKind.Temp
            && _tempDataSize.TryGetValue(val.TempIndex, out int vds) ? vds : 2;

        // FLOAT → WORD はエラー (FTOI を明示要求)
        if (valueDs == 3 && returnDs != 3)
        {
            _diagnostics.Error(
                "Cannot return FLOAT from non-FLOAT function (use FTOI for explicit conversion)",
                errSpan);
            return val;
        }
        // 拡大方向 (WORD→FLOAT) のみ自動変換
        return EmitTypeConversionExpand(val, returnDs);
    }

    /// <summary>
    /// ++ -- と複合代入は、代入先 (配列の添字・番地の式) を、読むときと書くときの 2 回計算する。次のときは、2 回で
    /// 違う所になるか副作用が 2 回起きるので、エラーにする:
    /// 添字・配列の式に ++ -- ・代入・関数の呼び出し・PORT の読み取りがある。右辺 (複合代入の値) が、添字・配列の式の
    /// 変数に代入する (T[I] += (I = I + 1))。関数の呼び出しの中で変わるものは見ない (式の中の順は決めない、仕様書)
    /// </summary>
    private void CheckTargetEvaluatedTwice(Expression target, Expression? value, SourceSpan span)
    {
        if (target is not ArrayAccessExpr arr) return;
        bool sideEffect = false;
        var names = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        void Walk(Expression? e)
        {
            switch (e)
            {
                case IdentifierExpr id: names.Add(id.Name); break;
                case IncrementExpr or AssignExpr or CompoundAssignExpr or CallExpr or CodeExpr or StringFuncExpr:
                    sideEffect = true; break;
                case ArrayAccessExpr { Array: IdentifierExpr port } when port.Name.Equals("PORT", StringComparison.OrdinalIgnoreCase)
                        || port.Name.Equals("PORTW", StringComparison.OrdinalIgnoreCase):
                    sideEffect = true; break;
                case BinaryExpr b: Walk(b.Left); Walk(b.Right); break;
                case UnaryExpr u: Walk(u.Operand); break;
                case CastExpr c: Walk(c.Operand); break;
                case HighLowExpr h: Walk(h.Operand); break;
                case AddressOfExpr a: Walk(a.Operand); break;
                case ConditionalExpr c: Walk(c.Condition); Walk(c.TrueExpr); Walk(c.FalseExpr); break;
                case CommaExpr c: Walk(c.Left); Walk(c.Right); break;
                case ArrayAccessExpr a: Walk(a.Array); foreach (var x in a.Indices) Walk(x); break;
            }
        }
        // 配列の名前そのもの (大きさのある配列・MEM) は番地が変わらないので、変数を読むとは見ない (間接変数は見る)
        if (arr.Array is not IdentifierExpr baseId || ResolveVarInfo(baseId.Name).Kind == VarKind.Pointer) Walk(arr.Array);
        foreach (var x in arr.Indices) Walk(x);
        if (sideEffect)
        {
            _diagnostics.Error("Side effects (++, --, assignments, function calls, PORT reads) are not allowed in the index of "
                + "the target of ++/--/+=/-=/*=//= (the target is evaluated twice, for reading and for writing)", span);
            return;
        }

        // 代入先が変数を読むとき、右辺に代入・++ -- があればエラー (どの変数を変えるかは見ない: MEMW[&I] = 1 のように
        // メモリを通して変えることもある。通す形を狭くする)
        if (names.Count == 0) return;
        bool writes = false;
        void WalkValue(Expression? e)
        {
            switch (e)
            {
                case AssignExpr or CompoundAssignExpr or IncrementExpr: writes = true; break;
                case BinaryExpr b: WalkValue(b.Left); WalkValue(b.Right); break;
                case UnaryExpr u: WalkValue(u.Operand); break;
                case CastExpr c: WalkValue(c.Operand); break;
                case HighLowExpr h: WalkValue(h.Operand); break;
                case AddressOfExpr a: WalkValue(a.Operand); break;
                case ConditionalExpr c: WalkValue(c.Condition); WalkValue(c.TrueExpr); WalkValue(c.FalseExpr); break;
                case CommaExpr c: WalkValue(c.Left); WalkValue(c.Right); break;
                case ArrayAccessExpr a: WalkValue(a.Array); foreach (var x in a.Indices) WalkValue(x); break;
                case CallExpr call: foreach (var x in call.Arguments) WalkValue(x); break;
                case CodeExpr code: foreach (var x in code.Values) WalkValue(x); break;
                case CodeEvalExpr ce: WalkValue(ce.Inner); break;
            }
        }
        WalkValue(value);
        if (writes)
            _diagnostics.Error("The right side of +=/-=/*=//= must not contain assignments or ++/-- when the index of the target "
                + "uses a variable (the target is evaluated twice, for reading and for writing)", span);
    }

    /// <summary>代入先に値を書く。書いた値 (代入先の型に変えたもの) を返す (代入式の値)</summary>
    private IrOperand EmitStore(Expression target, IrOperand value)
    {
        if (target is IdentifierExpr id)
        {
            var vi = CheckDefined(id.Name, id.Span);
            int storeDs = vi.VarDataSize;
            value = EmitTypeConversion(value, storeDs);
            if (vi.Local != null)
                Emit(IrOp.StoreLocal, IrOperand.Imm(vi.Local.Offset), value, dataSize: storeDs);
            else
                Emit(IrOp.StoreVar, IrOperand.Sym(ResolveAsmLabel(id.Name)), value, dataSize: storeDs);
        }
        else if (target is ArrayAccessExpr arr)
        {
            var arrayName = (arr.Array as IdentifierExpr)?.Name;
            var stVi = arrayName != null ? CheckDefined(arrayName, arr.Span) : default;
            var arraySym = stVi.GlobalSym;
            bool isMemArray = arraySym?.Type is MemoryArrayType;
            bool isByteAccess = arraySym?.Type is MemoryArrayType mt && mt.ElementType == SlangType.Byte;
            bool isIndirect = stVi.Kind == VarKind.Pointer;
            bool isIndirectByte = isIndirect && stVi.ElemSize == 1;

            // PORT/PORTW判定
            bool isPortArray = arrayName != null &&
                (arrayName.Equals("PORT", StringComparison.OrdinalIgnoreCase) ||
                 arrayName.Equals("PORTW", StringComparison.OrdinalIgnoreCase));
            bool isPortByte = arrayName != null && arrayName.Equals("PORT", StringComparison.OrdinalIgnoreCase);

            if (isPortArray)
            {
                // PORT[addr] = value (FLOAT は整数に変える。変えないと AHL のまま積まれて、DE だけ取り出される)
                value = EmitTypeConversion(value, isPortByte ? 1 : 2);
                var addr = arr.Indices[0].Accept(this);
                Emit(IrOp.PortOut, addr, value, dataSize: isPortByte ? 1 : 2);
            }
            else if (isMemArray)
            {
                value = EmitTypeConversion(value, isByteAccess ? 1 : 2);   // FLOAT は整数に変える (PORT と同じ)
                var addr = arr.Indices[0].Accept(this);
                Emit(IrOp.MemStore, addr, value, dataSize: isByteAccess ? 1 : 2);
            }
            else if (isIndirect)
            {
                // 間接変数ストア: *(base + idx * elemSize) = value
                int elemSize = stVi.ElemSize;
                value = EmitTypeConversion(value, elemSize);

                var baseAddr = IrOperand.Temp(AllocTemp());
                if (stVi.Local != null)
                    Emit(IrOp.LoadLocal, baseAddr, IrOperand.Imm(stVi.Local.Offset));
                else
                    Emit(IrOp.LoadVar, baseAddr, IrOperand.Sym(ResolveAsmLabel(arrayName!)));

                var idx = arr.Indices[0].Accept(this);
                IrOperand scaledIdx = ScaleIndexByElemSize(idx, elemSize);
                var addr = IrOperand.Temp(AllocTemp());
                Emit(IrOp.Add, addr, baseAddr, scaledIdx);
                Emit(IrOp.IndirStore, addr, value, dataSize: elemSize);
            }
            else
            {
                // 通常配列のストア（多次元対応）
                // 要素サイズに応じた型変換 (int→float の i16tof24 等を挟む)
                value = EmitTypeConversion(value, stVi.ElemSize);
                bool storeIsByte = stVi.Kind == VarKind.Array && stVi.ElemSize == 1;

                // 多次元ストライド計算（定数最適化でも使用）
                List<int> strides;
                var stArrLi = stVi.Local;
                bool isLocalStoreArray = stArrLi != null && stArrLi.Kind == VarKind.Array && stArrLi.Dims != null;
                if (isLocalStoreArray)
                {
                    strides = ComputeStridesFromDims(stArrLi!.Dims!, stArrLi.ElemSize, arr.Indices.Count);
                    storeIsByte = stArrLi.ElemSize == 1;
                }
                else
                {
                    strides = ComputeStrides(arraySym, arr.Indices.Count, arrayName);
                }

                // ローカル配列の定数インデックス最適化: (IY+d) 直接ストア
                bool localStoreHandled = false;
                if (isLocalStoreArray)
                {
                    int elemSize = stArrLi!.ElemSize;
                    var directOffset = TryComputeLocalArrayOffset(stArrLi, arr.Indices, strides, elemSize);
                    if (directOffset.HasValue)
                    {
                        Emit(IrOp.StoreLocal, IrOperand.Imm(directOffset.Value), value, dataSize: elemSize);
                        localStoreHandled = true;
                    }
                }

                // グローバル/static配列の定数インデックス最適化: label+offset 直接ストア
                if (!localStoreHandled && !isLocalStoreArray && arrayName != null && stVi.Kind == VarKind.Array)
                {
                    int gElemSize = stVi.ElemSize;
                    var globalOffset = TryComputeConstArrayOffset(arr.Indices, strides);
                    if (globalOffset.HasValue)
                    {
                        string label = ResolveAsmLabel(arrayName);
                        string sym = globalOffset.Value == 0 ? label : $"{label}+{globalOffset.Value}";
                        Emit(IrOp.StoreVar, IrOperand.Sym(sym), value, dataSize: gElemSize);
                        localStoreHandled = true;
                    }
                }

                if (localStoreHandled) { /* 定数インデックスで処理済み */ }
                else
                {

                IrOperand baseAddr;
                if (arrayName != null)
                {
                    baseAddr = IrOperand.Temp(AllocTemp());
                    // ローカル配列: IY+offsetのアドレスを計算
                    if (isLocalStoreArray)
                    {
                        Emit(IrOp.InlineAsm, baseAddr, IrOperand.Asm($"\tPUSH\tIY\n\tPOP\tHL\n\tLD\tDE,${stArrLi!.Offset:X4}\n\tADD\tHL,DE"));
                    }
                    else if (stVi.Local != null)
                        Emit(IrOp.LoadLocal, baseAddr, IrOperand.Imm(stVi.Local.Offset));
                    else if (stVi.Kind == VarKind.Pointer)
                        Emit(IrOp.LoadVar, baseAddr, IrOperand.Sym(ResolveAsmLabel(arrayName)));
                    else
                        Emit(IrOp.LoadAddr, baseAddr, IrOperand.Sym(ResolveAsmLabel(arrayName)));
                }
                else
                {
                    baseAddr = arr.Array.Accept(this);
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
                Emit(IrOp.IndirStore, addr, value, dataSize: stVi.ElemSize);

                } // end else (non-const index path)
            }
        }
        else
        {
            // Indirect store
            var addr = target.Accept(this);
            Emit(IrOp.StoreIndirect, addr, value);
        }
        return value;
    }

    // ==== Loop stack for EXIT/CONTINUE ====

    /// <summary>
    /// 配列の各次元のストライド(バイト数)を計算する。
    /// 例: ARRAY WORD AR2[5][10] → dims=[6,11], elemSize=2
    ///   stride[0] = 11 * 2 = 22
    ///   stride[1] = 2
    /// </summary>
    private List<int> ComputeStrides(Symbol? arraySym, int indexCount, string? arrayName = null)
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
            // arraySym不明: ResolveVarInfoでelemSizeを判定
            int elemSize = arrayName != null ? ResolveVarInfo(arrayName).ElemSize : 2;
            for (int i = 0; i < indexCount; i++)
                strides.Add(elemSize);
        }
        return strides;
    }

    /// <summary>次元リストとelemSizeからストライドを計算</summary>
    /// <summary>
    /// ローカル配列の定数インデックスオフセットを計算。
    /// 全インデックスが定数で、最終オフセットが(IY+d)範囲内なら値を返す。
    /// strides計算は既存ロジック(ComputeStridesFromDims/ComputeStrides)をそのまま使用。
    /// </summary>
    private int? TryComputeLocalArrayOffset(
        LocalVarInfo localInfo, List<Expression> indices, List<int> strides, int elemSize)
    {
        var constEval = _globalSymbols != null ? NewConstEvaluator() : null;
        if (constEval == null) return null;

        int totalOffset = localInfo.Offset;
        for (int i = 0; i < indices.Count && i < strides.Count; i++)
        {
            var constIdx = constEval.Evaluate(indices[i]);
            if (constIdx == null) return null;
            totalOffset += constIdx.Value * strides[i];
        }

        // (IY+d)範囲チェック: BYTE=0..127, WORD=0..126
        int maxOffset = 127 - (elemSize - 1);
        if (totalOffset < 0 || totalOffset > maxOffset) return null;
        return totalOffset;
    }

    /// <summary>
    /// グローバル/static配列の定数インデックスオフセットを計算。
    /// 全インデックスが定数なら合計オフセットを返す。
    /// elemSizeはstride計算に既に織り込まれているため引数不要。
    /// </summary>
    private int? TryComputeConstArrayOffset(
        List<Expression> indices, List<int> strides)
    {
        var constEval = _globalSymbols != null ? NewConstEvaluator() : null;
        if (constEval == null) return null;

        int totalOffset = 0;
        for (int i = 0; i < indices.Count && i < strides.Count; i++)
        {
            var constIdx = constEval.Evaluate(indices[i]);
            if (constIdx == null) return null;
            totalOffset += constIdx.Value * strides[i];
        }

        if (totalOffset < 0) return null;
        return totalOffset;
    }

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

    /// <summary>EXIT・CONTINUE の行き先。CASE も積む (EXIT・CONTINUE は CASE を数えずに外のループへ行く)</summary>
    private sealed class LoopTarget(string? continueLabel, string breakLabel, bool isCase)
    {
        // null なら、CONTINUE が来たときに作る (REPEAT の UNTIL の前。CONTINUE のない REPEAT のラベルの番号を変えない)
        public string? ContinueLabel = continueLabel;
        public string BreakLabel { get; } = breakLabel;
        public bool IsCase { get; } = isCase;
    }

    private readonly Stack<LoopTarget> _loopStack = new();

    private LoopTarget PushLoop(string? cont, string brk, bool isCase = false)
    {
        var t = new LoopTarget(cont, brk, isCase);
        _loopStack.Push(t);
        return t;
    }
    private void PopLoop() => _loopStack.Pop();

    /// <summary>大域の名前を隠す変数 (引数・局所変数・関数内の静的変数・オーバーレイの中の変数) か</summary>
    private bool IsShadowingVariable(string name)
        => _localVars?.ContainsKey(name) == true || _staticVarSizes?.ContainsKey(name) == true
           || _currentOverlayLookup?.ContainsKey(name) == true;

    /// <summary>内側から level 番目のループ (CASE は数えない)。なければ null</summary>
    private LoopTarget? FindLoop(int level)
    {
        foreach (var t in _loopStack)
            if (!t.IsCase && --level == 0) return t;
        return null;
    }
}
