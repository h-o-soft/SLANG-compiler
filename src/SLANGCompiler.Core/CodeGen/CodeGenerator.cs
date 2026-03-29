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
    private readonly Runtime.EnvironmentConfig? _envConfig;
    private Z80Emitter _e;  // メイン or オーバーレイのエミッタ（切替可能）
    private readonly Z80Emitter _mainEmitter;
    private string _currentFuncExitLabel = "_EXIT";
    private int _currentFuncLocalSize;
    private readonly HashSet<string> _calledFunctions = new(StringComparer.OrdinalIgnoreCase);

    public CodeGenerator(IrModule module, Runtime.RuntimeManager? runtimeManager = null,
        Runtime.EnvironmentConfig? envConfig = null)
    {
        _module = module;
        _runtimeManager = runtimeManager;
        _envConfig = envConfig;
        _mainEmitter = new Z80Emitter();
        _e = _mainEmitter;
    }

    /// <summary>
    /// オーバーレイモジュール付きの場合、(メインASM, [(モジュール名, ASM)]...) を返す
    /// </summary>
    public (string MainAsm, List<(string Name, string Asm)> Overlays) GenerateAll()
    {
        var mainAsm = Generate();
        var overlays = new List<(string, string)>();

        foreach (var overlay in _module.Overlays)
        {
            overlays.Add(($"_m{overlay.Index}", GenerateOverlay(overlay)));
        }

        return (mainAsm, overlays);
    }

    private string GenerateOverlay(OverlayModule overlay)
    {
        // エミッタをオーバーレイ用に切り替え
        var savedEmitter = _e;
        var savedCalled = new HashSet<string>(_calledFunctions);
        _calledFunctions.Clear();
        _e = new Z80Emitter();

        _e.Comment($"=== Overlay Module {overlay.Index} ===");
        _e.Instruction("ORG", $"${overlay.OrgAddress:X4}");
        _e.Blank();

        // モジュール内の関数（メイン部と同じEmitFunctionロジックでフル生成）
        foreach (var func in overlay.Functions)
        {
            EmitFunction(func);
            _e.Blank();
        }

        // モジュール内で使われた文字列（メイン部StringTableから該当分を抽出）
        // 現在は全文字列がメイン部に入るので、オーバーレイからはメイン部の文字列を参照
        // TODO: オーバーレイ固有の文字列テーブル分離

        // ランタイム関数の結合（このモジュール固有の呼び出しのみ解決）
        if (_runtimeManager != null)
        {
            var userFuncs = new HashSet<string>(overlay.Functions.Select(f => f.Name), StringComparer.OrdinalIgnoreCase);
            // メイン部のユーザー関数も除外対象に加える
            foreach (var f in _module.Functions)
                userFuncs.Add(f.Name);

            var overlayRuntime = _runtimeManager.ResolveForNames(_calledFunctions, userFuncs).ToList();
            if (overlayRuntime.Count > 0)
            {
                _e.Blank();
                _e.Comment("=== Runtime Functions ===");
                foreach (var func in overlayRuntime)
                {
                    _e.Label(func.Name);
                    foreach (var line in func.Code.Split('\n'))
                    {
                        if (!string.IsNullOrWhiteSpace(line))
                            _e.Raw(line);
                    }
                    _e.Blank();
                }
            }
        }

        // 共有シンボル: メイン部のグローバル変数をEQUまたはEXTERN宣言
        _e.Blank();
        _e.Comment("=== Shared Symbols (from main) ===");
        foreach (var gv in _module.GlobalVars)
        {
            if (gv.FixedAddress.HasValue)
                _e.Raw($"{gv.AsmLabel}\tEQU\t${gv.FixedAddress.Value:X4}");
            else
                _e.Raw($"; EXTERN {gv.AsmLabel}  ; address resolved at link time");
        }

        // 文字列テーブル参照（メイン部の文字列ラベルをEXTERN）
        if (_module.StringTable.Count > 0)
        {
            _e.Comment("=== String references (from main) ===");
            foreach (var label in _module.StringTable.Keys)
                _e.Raw($"; EXTERN {label}");
        }

        var result = _e.ToAssembly();

        // エミッタを復元
        _e = savedEmitter;
        _calledFunctions.Clear();
        foreach (var name in savedCalled) _calledFunctions.Add(name);

        return result;
    }

    public string Generate()
    {
        // === Phase 1: 関数本体の生成（_calledFunctions収集） ===
        var funcEmitter = new Z80Emitter();
        var savedEmitter = _e;
        _e = funcEmitter;

        foreach (var func in _module.Functions)
        {
            EmitFunction(func);
            _e.Blank();
        }

        _e = savedEmitter;

        // === Phase 2: ランタイム使用関数の確定 ===
        if (_runtimeManager != null)
        {
            var userFuncs = new HashSet<string>(_module.Functions.Select(f => f.Name), StringComparer.OrdinalIgnoreCase);
            foreach (var name in _calledFunctions)
            {
                if (!userFuncs.Contains(name))
                    _runtimeManager.MarkUsed(name);
            }
        }

        // === Phase 3: ORG + ENV_TYPE/OS_TYPE ===
        if (_module.OrgAddress.HasValue)
        {
            _e.Instruction("ORG", $"${_module.OrgAddress.Value:X4}");
        }

        if (_envConfig != null)
        {
            _e.Raw($"ENV_TYPE EQU {_envConfig.EnvType} ");
            _e.Raw($"OS_TYPE EQU {_envConfig.OsType}");
        }

        // === Phase 4: エントリポイント生成 ===
        if (_runtimeManager?.Functions.ContainsKey("SLANGINIT") == true)
        {
            // SLANGINITをインライン展開し、通常出力から除外
            var code = _runtimeManager.GetAndExclude("SLANGINIT");
            if (code != null)
            {
                code = code.Replace("<<CALLINITIALIZER>>", " CALL RUNTIME_INIT");
                // 行単位でSLANGINITを処理し、CALL MAIN直前にグローバル初期化を挿入
                foreach (var line in code.Split('\n'))
                {
                    var tokens = line.Trim().Split(new[] { ' ', '\t' }, StringSplitOptions.RemoveEmptyEntries);
                    if (tokens.Length >= 2
                        && tokens[0].Equals("CALL", StringComparison.OrdinalIgnoreCase)
                        && tokens[1].Equals("MAIN", StringComparison.OrdinalIgnoreCase))
                    {
                        EmitGlobalInit();
                    }
                    if (!string.IsNullOrWhiteSpace(line))
                        _e.Raw(line);
                }
            }
        }
        else
        {
            // フォールバック: SLANGINITなし環境
            _e.Comment("=== Entry Point ===");
            _e.Instruction("LD", "IY,__IYWORK");
            if (HasRuntimeInitializers())
                _e.Instruction("CALL", "RUNTIME_INIT");
            EmitGlobalInit();
            _e.Instruction("CALL", "MAIN");
            _e.Instruction("RET");
        }
        _e.Blank();

        // === Phase 5: 関数本体を挿入 ===
        _e.AppendFrom(funcEmitter);

        // === Phase 6: 文字列テーブル ===
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

        // === Phase 7: 初期値付きグローバル変数（コード領域に配置） ===
        // アドレス固定変数: EQUで定義
        foreach (var gv in _module.GlobalVars.Where(v => v.FixedAddress.HasValue))
        {
            _e.Raw($"{gv.AsmLabel}\tEQU\t${gv.FixedAddress!.Value:X4}");
        }

        // 初期値付き変数: コード領域にDBで配置
        foreach (var gv in _module.GlobalVars.Where(v => !v.FixedAddress.HasValue && v.InitialData != null))
        {
            _e.Label(gv.AsmLabel);
            var bytes = string.Join(",", gv.InitialData!.Select(b => $"${b:X2}"));
            _e.Raw($"\tDB\t{bytes}");
        }

        // === Phase 8: ランタイム関数出力 + RUNTIME_INIT ===
        if (_runtimeManager != null)
        {
            // RUNTIME_INIT（常に出力）
            EmitRuntimeInit();

            var outputFuncs = _runtimeManager.GetOutputFunctions().ToList();
            if (outputFuncs.Count > 0)
            {
                _e.Blank();
                _e.Comment("=== Runtime Functions ===");
                foreach (var func in outputFuncs)
                {
                    _e.Label(func.Name);
                    foreach (var line in func.Code.Split('\n'))
                    {
                        if (!string.IsNullOrWhiteSpace(line))
                            _e.Raw(line);
                    }
                    _e.Blank();
                }
            }
        }

        // プログラム末尾マーカー
        _e.Label("SLANG_PROG_END");

        // === Phase 9: __WORK__集約レイアウト ===
        EmitWorkArea();

        // ピープホール最適化
        _e.OptimizeWith(new PeepholeOptimizer());

        return _e.ToAssembly();
    }

    /// <summary>
    /// コンパイラ生成グローバル変数初期化コード (VAR X=42等)
    /// </summary>
    private void EmitGlobalInit()
    {
        int? pendingConstVal = null;
        foreach (var inst in _module.GlobalData)
        {
            if (inst.Op == IrOp.LoadConst && inst.Src1.Kind == IrOperandKind.Immediate)
            {
                pendingConstVal = (int)(inst.Src1.ImmediateValue & 0xFFFF);
            }
            else if (inst.Op == IrOp.StoreVar && pendingConstVal.HasValue)
            {
                _e.Instruction("LD", $"HL,${pendingConstVal.Value:X4}");
                _e.Instruction("LD", $"({AsmLabel(inst.Dest.Name!)}),HL");
                pendingConstVal = null;
            }
            else
            {
                pendingConstVal = null;
            }
        }
    }

    /// <summary>
    /// RUNTIME_INIT関数の出力（常に出力、初期化対象0件でもRETのみ）
    /// </summary>
    private void EmitRuntimeInit()
    {
        _e.Blank();
        _e.Label("RUNTIME_INIT");
        if (_runtimeManager != null)
        {
            foreach (var func in _runtimeManager.GetUsedFunctions())
            {
                if (!string.IsNullOrEmpty(func.InitCode))
                    _e.Instruction("CALL", $"{func.Name}_INITIALIZE");
            }
        }
        _e.Instruction("RET");

        // 各initializer本体
        if (_runtimeManager != null)
        {
            foreach (var func in _runtimeManager.GetUsedFunctions())
            {
                if (!string.IsNullOrEmpty(func.InitCode))
                {
                    _e.Blank();
                    _e.Label($"{func.Name}_INITIALIZE");
                    foreach (var line in func.InitCode.Split('\n'))
                    {
                        if (!string.IsNullOrWhiteSpace(line))
                            _e.Raw(line);
                    }
                }
            }
        }
    }

    /// <summary>
    /// RUNTIME_INITが必要かどうか
    /// </summary>
    private bool HasRuntimeInitializers()
    {
        if (_runtimeManager == null) return false;
        return _runtimeManager.GetUsedFunctions().Any(f => !string.IsNullOrEmpty(f.InitCode));
    }

    // システムレジスタワーク変数の定義（旧実装準拠の順序）
    private static readonly (string Label, int Size)[] SystemRegisterWorks =
    {
        ("_BC", 2), ("_DE", 2), ("_HL", 2), ("_IX", 2), ("_IY", 2),
        ("_AF", 2), ("_CARRY", 2), ("_ZERO", 2), ("_SP", 2),
    };

    /// <summary>
    /// __WORK__集約レイアウトの出力
    /// </summary>
    private void EmitWorkArea()
    {
        _e.Blank();
        _e.Comment("; Variables (works)");

        // WORK指定時は別ORGを出力
        if (_module.WorkAddress.HasValue)
        {
            _e.Instruction("ORG", $"${_module.WorkAddress.Value:X4}");
            _e.Blank();
        }

        _e.Label("__WORK__");
        int workOffset = 0;

        // 1. ユーザーグローバル変数（固定アドレスなし、初期値なし）→ EQU
        foreach (var gv in _module.GlobalVars.Where(v => !v.FixedAddress.HasValue && v.InitialData == null))
        {
            _e.Raw($"{gv.AsmLabel} EQU (__WORK__ + {workOffset})");
            workOffset += gv.ByteSize;
        }

        // 3. システムレジスタワーク（_BC, _DE, _HL, _IX, _IY, _AF, _CARRY, _ZERO, _SP）
        int afOffset = 0;
        foreach (var (label, size) in SystemRegisterWorks)
        {
            if (label == "_AF") afOffset = workOffset;
            _e.Raw($"{label} EQU (__WORK__ + {workOffset})");
            workOffset += size;
        }
        // _A = _AF + 1 (エイリアス、領域を消費しない)
        _e.Raw($"_A EQU (_AF + 1)");

        // 4. ランタイムworks変数
        if (_runtimeManager != null)
        {
            foreach (var (label, size) in _runtimeManager.GetUsedWorkVariables())
            {
                _e.Raw($"{label} EQU (__WORK__ + {workOffset})");
                workOffset += size;
            }
        }

        // 5. __IYWORK (256バイト)
        _e.Raw($"__IYWORK EQU (__WORK__ + {workOffset})");
        _e.Raw($"WORKEND EQU (__WORK__ + {workOffset + 256})");
        _e.Blank();
        _e.Raw($"__WORKEND__ EQU (__WORK__ + {workOffset + 256})");
    }

    private void EmitStringData(string text)
    {
        // 印刷可能ASCII文字のみならDB "text",0形式、それ以外は混在形式
        if (text.All(ch => ch >= 0x20 && ch < 0x7F && ch != '"'))
        {
            _e.Raw($"\tDB\t\"{text}\",0");
        }
        else
        {
            // 制御文字を含む場合: 印刷可能部分は文字列、それ以外は$xx
            var parts = new List<string>();
            var strBuf = new System.Text.StringBuilder();
            foreach (var ch in text)
            {
                if (ch >= 0x20 && ch < 0x7F && ch != '"')
                {
                    strBuf.Append(ch);
                }
                else
                {
                    if (strBuf.Length > 0)
                    {
                        parts.Add($"\"{strBuf}\"");
                        strBuf.Clear();
                    }
                    parts.Add($"${(int)ch:X2}");
                }
            }
            if (strBuf.Length > 0)
                parts.Add($"\"{strBuf}\"");
            parts.Add("0");
            _e.Raw($"\tDB\t{string.Join(",", parts)}");
        }
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

        // レジスタ直接ロード最適化付きスタックマシン:
        //
        // 二項演算 BinOp(t_dest, t_src1, t_src2) で、
        // t_src1 と t_src2 の生成命令が両方とも「単純ロード」(LoadVar/LoadConst/LoadLocal/LoadAddr)
        // の場合、PUSH/POP を省いて直接 HL/DE にロードする。
        //
        // 例: Z = X + Y
        //   最適化前: LD HL,(X) → PUSH HL → LD HL,(Y) → POP DE → EX → ADD
        //   最適化後: LD HL,(X) → LD DE,(Y) → ADD HL,DE
        //
        // 複雑な部分式はスタック経由のまま。

        var insts = func.Instructions;

        // Pass 1: 各tempの生成命令マップと、直接ロード最適化対象の特定
        var tempDef = new Dictionary<int, int>();
        for (int i = 0; i < insts.Count; i++)
        {
            if (insts[i].Dest.Kind == IrOperandKind.Temp)
                tempDef[insts[i].Dest.TempIndex] = i;
        }

        // 直接ロード最適化: 二項演算の両src が単純ロードなら、
        // 元のLoad命令をスキップし、演算時にHL/DEに直接ロードする
        var skipEmit = new HashSet<int>();
        var directBinaryOps = new HashSet<int>(); // 両src単純ロード
        var halfDirectOps = new HashSet<int>();  // src2のみ単純ロード

        for (int i = 0; i < insts.Count; i++)
        {
            var inst = insts[i];
            if (IsBinaryOp(inst.Op)
                && inst.Src1.Kind == IrOperandKind.Temp
                && inst.Src2.Kind == IrOperandKind.Temp
                && tempDef.TryGetValue(inst.Src1.TempIndex, out int s1)
                && tempDef.TryGetValue(inst.Src2.TempIndex, out int s2))
            {
                if (IsSimpleLoad(insts[s1]) && IsSimpleLoad(insts[s2]))
                {
                    // 両方単純ロード → HL/DE直接ロード
                    skipEmit.Add(s1);
                    skipEmit.Add(s2);
                    directBinaryOps.Add(i);
                }
                else if (IsSimpleLoad(insts[s2]) && !IsSimpleLoad(insts[s1]))
                {
                    // src2のみ単純ロード → src1はHLに残っている、src2をDE直接ロード
                    skipEmit.Add(s2);
                    halfDirectOps.Add(i);
                }
            }
        }

        // 比較+JumpIfZero融合: CmpXx → JumpIfZero を直接条件分岐に変換
        // CmpEq t5 t3 t4 → JumpIfZero label t5 を
        // SBC HL,DE → JP NZ,label に融合（0/1変換不要）
        var fusedCompareJumps = new Dictionary<int, int>(); // compareIdx → jumpIdx
        for (int i = 0; i < insts.Count - 1; i++)
        {
            if (IsCompareOp(insts[i].Op) && insts[i].Dest.Kind == IrOperandKind.Temp)
            {
                int cmpTemp = insts[i].Dest.TempIndex;
                // 次の命令(skipを飛ばして)がJumpIfZeroでこのtempを参照するか
                for (int j = i + 1; j < insts.Count; j++)
                {
                    if (skipEmit.Contains(j)) continue;
                    if (insts[j].Op == IrOp.JumpIfZero
                        && insts[j].Src1.Kind == IrOperandKind.Temp
                        && insts[j].Src1.TempIndex == cmpTemp)
                    {
                        fusedCompareJumps[i] = j;
                        skipEmit.Add(j); // JumpIfZeroは融合先で処理
                        break;
                    }
                    // このtempが他で使われるなら融合不可
                    if (UsesTemp(insts[j], cmpTemp)) break;
                }
            }
        }

        // NeedsPushAfterで参照するためフィールドにセット
        _currentDirectBinaryOps = directBinaryOps;
        _currentHalfDirectOps = halfDirectOps;
        _currentSkipEmit = skipEmit;

        // Pass 2: 出力
        for (int i = 0; i < insts.Count; i++)
        {
            if (skipEmit.Contains(i)) continue;

            var inst = insts[i];

            if (directBinaryOps.Contains(i))
            {
                var s1 = insts[tempDef[inst.Src1.TempIndex]];
                var s2 = insts[tempDef[inst.Src2.TempIndex]];

                // 融合比較+ジャンプ: 比較してフラグから直接分岐
                if (fusedCompareJumps.TryGetValue(i, out int jumpIdx))
                {
                    var jumpInst = insts[jumpIdx];
                    var label = jumpInst.Dest.Name!;
                    EmitInstruction(s1);
                    EmitLoadToDE(s2);
                    EmitFusedCompareJump(inst, label);
                    continue;
                }

                // INC/DEC最適化: x+1 → INC HL, x-1 → DEC HL
                if ((inst.Op == IrOp.Add || inst.Op == IrOp.Sub)
                    && s2.Op == IrOp.LoadConst && s2.Src1.Kind == IrOperandKind.Immediate)
                {
                    int constVal = (int)(s2.Src1.ImmediateValue & 0xFFFF);
                    if (constVal == 1)
                    {
                        EmitInstruction(s1); // src1 → HL
                        _e.Instruction(inst.Op == IrOp.Add ? "INC" : "DEC", "HL");
                        continue;
                    }
                    if (constVal == 2)
                    {
                        EmitInstruction(s1);
                        _e.Instruction(inst.Op == IrOp.Add ? "INC" : "DEC", "HL");
                        _e.Instruction(inst.Op == IrOp.Add ? "INC" : "DEC", "HL");
                        continue;
                    }
                }

                // 通常の直接ロード最適化
                EmitInstruction(s1);
                EmitLoadToDE(s2);
                EmitBinaryDirect(inst);
                continue;
            }

            // src2のみ単純ロード: src1の結果がHL、src2をDE直接ロード
            if (halfDirectOps.Contains(i))
            {
                var s2Inst = insts[tempDef[inst.Src2.TempIndex]];

                if (fusedCompareJumps.TryGetValue(i, out int hJumpIdx))
                {
                    var jumpInst = insts[hJumpIdx];
                    EmitLoadToDE(s2Inst);
                    EmitFusedCompareJump(inst, jumpInst.Dest.Name!);
                    continue;
                }

                // INC/DEC最適化
                if ((inst.Op == IrOp.Add || inst.Op == IrOp.Sub)
                    && s2Inst.Op == IrOp.LoadConst && s2Inst.Src1.Kind == IrOperandKind.Immediate)
                {
                    int cv = (int)(s2Inst.Src1.ImmediateValue & 0xFFFF);
                    if (cv == 1) { _e.Instruction(inst.Op == IrOp.Add ? "INC" : "DEC", "HL"); continue; }
                    if (cv == 2) { _e.Instruction(inst.Op == IrOp.Add ? "INC" : "DEC", "HL"); _e.Instruction(inst.Op == IrOp.Add ? "INC" : "DEC", "HL"); continue; }
                }

                EmitLoadToDE(s2Inst);
                EmitBinaryDirect(inst);
                continue;
            }

            // 非直接ロードだが融合比較ジャンプの場合
            if (fusedCompareJumps.TryGetValue(i, out int jumpIdx2))
            {
                var jumpInst = insts[jumpIdx2];
                var label = jumpInst.Dest.Name!;
                // 比較のsrc1/src2はスタック経由
                EmitInstruction(inst); // 比較(0/1生成)は不要だが... フォールバック
                // TODO: スタック経由の融合比較
                _e.Instruction("LD", "A,H");
                _e.Instruction("OR", "L");
                _e.Instruction("JP", $"Z,{label}");
                continue;
            }

            EmitInstruction(inst);

            // PUSH挿入判定
            if (inst.Dest.Kind == IrOperandKind.Temp && !skipEmit.Contains(i))
            {
                if (NeedsPushAfter(insts, i, inst.Dest.TempIndex))
                    _e.Instruction("PUSH", "HL");
            }
        }
        _currentFunction = null;
    }

    /// <summary>単純ロード命令かどうか</summary>
    private static bool IsSimpleLoad(IrInstruction inst) => inst.Op is
        IrOp.LoadVar or IrOp.LoadConst or IrOp.LoadLocal or IrOp.LoadAddr;

    /// <summary>ロード命令をDEレジスタ版で出力</summary>
    private void EmitLoadToDE(IrInstruction inst)
    {
        switch (inst.Op)
        {
            case IrOp.LoadConst:
                if (inst.Src1.Kind == IrOperandKind.AsmString)
                    _e.Instruction("LD", "DE,0 ; string placeholder");
                else
                {
                    int val = (int)(inst.Src1.ImmediateValue & 0xFFFF);
                    _e.Instruction("LD", $"DE,${val:X4}");
                }
                break;
            case IrOp.LoadVar:
                _e.Instruction("LD", $"DE,({AsmLabel(inst.Src1.Name!)})");
                break;
            case IrOp.LoadLocal:
                int offset = (int)inst.Src1.ImmediateValue;
                _e.Instruction("LD", $"E,(IY+${offset:X2})");
                _e.Instruction("LD", $"D,(IY+${offset + 1:X2})");
                break;
            case IrOp.LoadAddr:
            {
                var addrName = inst.Src1.Kind == IrOperandKind.Symbol ? AsmLabel(inst.Src1.Name!) : inst.Src1.Name!;
                _e.Instruction("LD", $"DE,{addrName}");
                break;
            }
        }
    }

    /// <summary>
    /// 比較+分岐の融合: 比較結果を0/1にせず、フラグから直接JP。
    /// HL=src1, DE=src2 がセット済み。JumpIfZero→条件が偽なら分岐。
    /// </summary>
    private void EmitFusedCompareJump(IrInstruction cmpInst, string label)
    {
        // CmpEq + JumpIfZero = 「等しくなければジャンプ」→ JP NZ
        // CmpNeq + JumpIfZero = 「等しければジャンプ」→ JP Z
        // CmpLt + JumpIfZero = 「小さくなければジャンプ」→ JP NC
        // CmpGe + JumpIfZero = 「以上でなければジャンプ」→ JP C
        // CmpGt + JumpIfZero = src2-src1してCで判定

        switch (cmpInst.Op)
        {
            case IrOp.CmpEq:
                _e.Instruction("OR", "A");
                _e.Instruction("SBC", "HL,DE");
                _e.Instruction("JP", $"NZ,{label}");
                break;
            case IrOp.CmpNeq:
                _e.Instruction("OR", "A");
                _e.Instruction("SBC", "HL,DE");
                _e.Instruction("JP", $"Z,{label}");
                break;
            case IrOp.CmpLt:
                _e.Instruction("OR", "A");
                _e.Instruction("SBC", "HL,DE");
                _e.Instruction("JP", $"NC,{label}");
                break;
            case IrOp.CmpGe:
                _e.Instruction("OR", "A");
                _e.Instruction("SBC", "HL,DE");
                _e.Instruction("JP", $"C,{label}");
                break;
            case IrOp.CmpGt:
                // src1 > src2 → src2 - src1 で C判定
                _e.Instruction("EX", "DE,HL");
                _e.Instruction("OR", "A");
                _e.Instruction("SBC", "HL,DE");
                _e.Instruction("JP", $"NC,{label}");
                break;
            case IrOp.CmpLe:
                // src1 <= src2 → !(src1 > src2) → src2-src1でC判定
                _e.Instruction("EX", "DE,HL");
                _e.Instruction("OR", "A");
                _e.Instruction("SBC", "HL,DE");
                _e.Instruction("JP", $"C,{label}");
                break;
            default:
                // フォールバック: 0/1生成 + JP Z
                EmitBinaryDirect(cmpInst);
                _e.Instruction("LD", "A,H");
                _e.Instruction("OR", "L");
                _e.Instruction("JP", $"Z,{label}");
                break;
        }
    }

    /// <summary>
    /// 二項演算をHL/DE直接で出力（POP不要）。
    /// 呼び出し時点で HL=src1, DE=src2 がセット済み。
    /// </summary>
    private void EmitBinaryDirect(IrInstruction inst)
    {
        // FLOAT判定: DataSize==3 の場合はf24ランタイムを使用
        bool isFloat = inst.DataSize == 3;

        switch (inst.Op)
        {
            // 算術
            case IrOp.Add:
                if (isFloat) _e.Instruction("CALL", "f24add");
                else _e.Instruction("ADD", "HL,DE");
                break;
            case IrOp.Sub:
                if (isFloat) _e.Instruction("CALL", "f24sub");
                else { _e.Instruction("OR", "A"); _e.Instruction("SBC", "HL,DE"); }
                break;
            case IrOp.Mul or IrOp.SMul:
                if (isFloat) _e.Instruction("CALL", "f24mul");
                else _e.Instruction("CALL", "MUL16");
                break;
            case IrOp.Div or IrOp.SDiv:
                if (isFloat) _e.Instruction("CALL", "f24div");
                else _e.Instruction("CALL", inst.Op == IrOp.SDiv ? "SDIV16" : "DIV16");
                break;
            case IrOp.Mod: _e.Instruction("CALL", "MOD16"); break;
            case IrOp.SMod: _e.Instruction("CALL", "SMOD16"); break;

            // ビット演算
            case IrOp.And:
                _e.Instruction("LD", "A,H"); _e.Instruction("AND", "D"); _e.Instruction("LD", "H,A");
                _e.Instruction("LD", "A,L"); _e.Instruction("AND", "E"); _e.Instruction("LD", "L,A");
                break;
            case IrOp.Or:
                _e.Instruction("LD", "A,H"); _e.Instruction("OR", "D"); _e.Instruction("LD", "H,A");
                _e.Instruction("LD", "A,L"); _e.Instruction("OR", "E"); _e.Instruction("LD", "L,A");
                break;
            case IrOp.Xor:
                _e.Instruction("LD", "A,H"); _e.Instruction("XOR", "D"); _e.Instruction("LD", "H,A");
                _e.Instruction("LD", "A,L"); _e.Instruction("XOR", "E"); _e.Instruction("LD", "L,A");
                break;

            // シフト
            case IrOp.Shl: _e.Instruction("CALL", "SHL16"); break;
            case IrOp.Shr: _e.Instruction("CALL", "SHR16"); break;
            case IrOp.SShl: _e.Instruction("CALL", "SHL16"); break;
            case IrOp.SShr: _e.Instruction("CALL", "SSHR16"); break;

            // 比較
            // FLOAT: f24cmpを呼んでフラグで判定 (Z=等、C=小)
            case IrOp.CmpEq:
                if (isFloat) { _e.Instruction("CALL", "f24cmp"); }
                else { _e.Instruction("OR", "A"); _e.Instruction("SBC", "HL,DE"); }
                _e.Instruction("OR", "A"); _e.Instruction("SBC", "HL,DE");
                _e.Instruction("LD", "HL,$0000"); _e.Instruction("JR", "Z,$+3"); _e.Instruction("INC", "HL");
                break;
            case IrOp.CmpNeq:
                _e.Instruction("OR", "A"); _e.Instruction("SBC", "HL,DE");
                _e.Instruction("LD", "HL,$0000"); _e.Instruction("JR", "NZ,$+3"); _e.Instruction("INC", "HL");
                break;
            case IrOp.CmpLt:
                _e.Instruction("OR", "A"); _e.Instruction("SBC", "HL,DE");
                _e.Instruction("LD", "HL,$0000"); _e.Instruction("JR", "C,$+3"); _e.Instruction("INC", "HL");
                break;
            case IrOp.CmpGe:
                _e.Instruction("OR", "A"); _e.Instruction("SBC", "HL,DE");
                _e.Instruction("LD", "HL,$0000"); _e.Instruction("JR", "NC,$+3"); _e.Instruction("INC", "HL");
                break;
            case IrOp.CmpGt:
                // src1 > src2 → src2 - src1 で C
                _e.Instruction("EX", "DE,HL"); _e.Instruction("OR", "A"); _e.Instruction("SBC", "HL,DE");
                _e.Instruction("LD", "HL,$0000"); _e.Instruction("JR", "C,$+3"); _e.Instruction("INC", "HL");
                break;
            case IrOp.CmpLe:
                // src1 <= src2 → !(src1 > src2)
                _e.Instruction("EX", "DE,HL"); _e.Instruction("OR", "A"); _e.Instruction("SBC", "HL,DE");
                _e.Instruction("LD", "HL,$0001"); _e.Instruction("JR", "C,$+3"); _e.Instruction("DEC", "HL");
                break;

            // 符号付き比較
            case IrOp.CmpSLt: _e.Instruction("CALL", "SCMP_LT"); break;
            case IrOp.CmpSGt: _e.Instruction("CALL", "SCMP_GT"); break;
            case IrOp.CmpSLe: _e.Instruction("CALL", "SCMP_LE"); break;
            case IrOp.CmpSGe: _e.Instruction("CALL", "SCMP_GE"); break;

            // 論理
            case IrOp.LogAnd:
                _e.Instruction("LD", "A,H"); _e.Instruction("OR", "L");
                _e.Instruction("LD", "HL,$0000"); _e.Instruction("JR", "Z,$+7");
                _e.Instruction("LD", "A,D"); _e.Instruction("OR", "E");
                _e.Instruction("JR", "Z,$+3"); _e.Instruction("INC", "HL");
                break;
            case IrOp.LogOr:
                _e.Instruction("LD", "A,H"); _e.Instruction("OR", "L");
                _e.Instruction("OR", "D"); _e.Instruction("OR", "E");
                _e.Instruction("LD", "HL,$0000"); _e.Instruction("JR", "Z,$+3"); _e.Instruction("INC", "HL");
                break;

            default:
                _e.Comment($"unsupported direct: {inst.Op}");
                break;
        }
    }

    /// <summary>
    /// destTempが後続の二項演算等のsrc1として使われ、
    /// その演算のsrc2が別のtemp（=HLを使う）場合にPUSHが必要。
    /// </summary>
    // 最適化パスの結果（NeedsPushAfterで参照）
    private HashSet<int> _currentDirectBinaryOps = new();
    private HashSet<int> _currentHalfDirectOps = new();
    private HashSet<int> _currentSkipEmit = new();

    private bool NeedsPushAfter(List<IrInstruction> insts, int currentIdx, int destTemp)
    {
        for (int j = currentIdx + 1; j < insts.Count; j++)
        {
            if (_currentSkipEmit.Contains(j)) continue; // スキップされる命令は無視

            var next = insts[j];

            // この temp が src1 で、src2 が別の temp → PUSH 必要
            if (next.Src1.Kind == IrOperandKind.Temp && next.Src1.TempIndex == destTemp)
            {
                // directBinaryOps/halfDirectOps → src2は直接DEロードされるのでPUSH不要
                if (_currentDirectBinaryOps.Contains(j) || _currentHalfDirectOps.Contains(j))
                    return false;

                // 二項演算でsrc2もtempなら
                if (IsBinaryOp(next.Op) && next.Src2.Kind == IrOperandKind.Temp)
                    return true;

                // IndirStore/MemStore/PortOut: Src1=value, Dest=addr
                // addrが別tempならvalue退避が必要（addr計算でHLが上書きされるため）
                if (next.Op is IrOp.IndirStore or IrOp.MemStore or IrOp.PortOut
                    && next.Dest.Kind == IrOperandKind.Temp && next.Dest.TempIndex != destTemp)
                    return true;

                // ArrayStore: Dest=base, Src1=value, Src2=index → 全部tempの場合PUSH要
                if (next.Op == IrOp.ArrayStore && next.Src2.Kind == IrOperandKind.Temp)
                    return true;

                return false; // 使われるが、PUSH不要
            }

            // この temp が src2 として使われる → 直前のsrc1がPUSHされるべき → ここではPUSH不要
            if (next.Src2.Kind == IrOperandKind.Temp && next.Src2.TempIndex == destTemp)
                return false;

            // Dest として使われる（上書き）→ もう使われない
            if (next.Dest.Kind == IrOperandKind.Temp && next.Dest.TempIndex == destTemp)
                return false;
        }
        return false;
    }

    private static bool IsBinaryOp(IrOp op) => op switch
    {
        IrOp.Add or IrOp.Sub or IrOp.Mul or IrOp.Div or IrOp.Mod
        or IrOp.SMul or IrOp.SDiv or IrOp.SMod
        or IrOp.And or IrOp.Or or IrOp.Xor
        or IrOp.Shl or IrOp.Shr or IrOp.SShl or IrOp.SShr
        or IrOp.CmpEq or IrOp.CmpNeq or IrOp.CmpLt or IrOp.CmpGt or IrOp.CmpLe or IrOp.CmpGe
        or IrOp.CmpSLt or IrOp.CmpSGt or IrOp.CmpSLe or IrOp.CmpSGe
        or IrOp.LogAnd or IrOp.LogOr => true,
        _ => false,
    };

    private static bool IsCompareOp(IrOp op) => op is
        IrOp.CmpEq or IrOp.CmpNeq or IrOp.CmpLt or IrOp.CmpGt or IrOp.CmpLe or IrOp.CmpGe
        or IrOp.CmpSLt or IrOp.CmpSGt or IrOp.CmpSLe or IrOp.CmpSGe;

    private static bool UsesTemp(IrInstruction inst, int tempIdx)
    {
        return (inst.Src1.Kind == IrOperandKind.Temp && inst.Src1.TempIndex == tempIdx)
            || (inst.Src2.Kind == IrOperandKind.Temp && inst.Src2.TempIndex == tempIdx)
            || (inst.Dest.Kind == IrOperandKind.Temp && inst.Dest.TempIndex == tempIdx);
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

            case IrOp.DefByte:
                _e.Raw($"\tDB\t${inst.Dest.ImmediateValue & 0xFF:X2}");
                break;
            case IrOp.DefWord:
                if (inst.Dest.Kind == IrOperandKind.Label)
                    _e.Raw($"\tDW\t{inst.Dest.Name}");
                else
                    _e.Raw($"\tDW\t${inst.Dest.ImmediateValue & 0xFFFF:X4}");
                break;
            case IrOp.DefString:
                if (inst.Dest.Name != null)
                {
                    var bytes = inst.Dest.Name.Select(ch => $"${(int)ch:X2}");
                    _e.Raw($"\tDB\t{string.Join(",", bytes)}");
                }
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

        // ローカル変数のサイズ: IR生成時に計算済みの値を優先、フォールバックでStoreLocal走査
        _currentFuncLocalSize = (_currentFunction?.LocalSize > 0)
            ? _currentFunction.LocalSize
            : ComputeLocalSize(inst);

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

    /// <summary>シンボル名→ASMラベル名。Z80レジスタ名との衝突を回避。</summary>
    private static string AsmLabel(string name) => $"_{name}";

    private void EmitLoadVar(IrInstruction inst)
    {
        var label = AsmLabel(inst.Src1.Name!);
        _e.Instruction("LD", $"HL,({label})");
        if (inst.DataSize == 3)
            _e.Instruction("LD", $"A,({label}+2)");
    }

    private void EmitStoreVar(IrInstruction inst)
    {
        var label = AsmLabel(inst.Dest.Name!);
        _e.Instruction("LD", $"({label}),HL");
        if (inst.DataSize == 3)
            _e.Instruction("LD", $"({label}+2),A");
    }

    private void EmitLoadLocal(IrInstruction inst)
    {
        int offset = (int)inst.Src1.ImmediateValue;
        if (inst.DataSize == 1)
        {
            _e.Instruction("LD", $"L,(IY+${offset:X2})");
            _e.Instruction("LD", "H,$00");
        }
        else
        {
            _e.Instruction("LD", $"L,(IY+${offset:X2})");
            _e.Instruction("LD", $"H,(IY+${offset + 1:X2})");
            if (inst.DataSize == 3) // FLOAT: 3バイト目
                _e.Instruction("LD", $"A,(IY+${offset + 2:X2})");
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
            _e.Instruction("LD", $"(IY+${offset:X2}),L");
            _e.Instruction("LD", $"(IY+${offset + 1:X2}),H");
            if (inst.DataSize == 3) // FLOAT: 3バイト目
                _e.Instruction("LD", $"(IY+${offset + 2:X2}),A");
        }
    }

    private void EmitLoadAddr(IrInstruction inst)
    {
        var name = inst.Src1.Name!;
        // Symbol(変数名)はプレフィックス付き、Label(文字列等)はそのまま
        var label = inst.Src1.Kind == IrOperandKind.Symbol ? AsmLabel(name) : name;
        _e.Instruction("LD", $"HL,{label}");
    }

    // 二項演算: スタック上にsrc1(PUSH済み)、HLにsrc2
    // → POP DE(=src1) → EX DE,HL → HL=src1, DE=src2 → 演算

    private void EmitArith(IrInstruction inst, string op)
    {
        _e.Instruction("POP", "DE");
        _e.Instruction("EX", "DE,HL");
        if (op == "ADD")
            _e.Instruction("ADD", "HL,DE");
        else
        {
            _e.Instruction("OR", "A");
            _e.Instruction("SBC", "HL,DE");
        }
    }

    private void EmitMul(IrInstruction inst)
    {
        _e.Instruction("POP", "DE");
        _e.Instruction("EX", "DE,HL");
        _e.Instruction("CALL", "MUL16");
    }

    private void EmitDiv(IrInstruction inst, bool signed)
    {
        _e.Instruction("POP", "DE");
        _e.Instruction("EX", "DE,HL");
        _e.Instruction("CALL", signed ? "SDIV16" : "DIV16");
    }

    private void EmitMod(IrInstruction inst, bool signed)
    {
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
        // src1(stack) op src2(HL)
        _e.Instruction("POP", "DE");
        _e.Instruction("LD", "A,D");
        _e.Instruction(op, "H");
        _e.Instruction("LD", "H,A");
        _e.Instruction("LD", "A,E");
        _e.Instruction(op, "L");
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
        // src1(stack)をHLに, src2(HL)をDEに → CALL SHL/SHR
        _e.Instruction("POP", "DE");
        _e.Instruction("EX", "DE,HL"); // HL=src1(被シフト値), DE=src2(シフト量)
        _e.Instruction("CALL", left ? "SHL16" : "SHR16");
    }

    private void EmitCompare(IrInstruction inst, string cond)
    {
        // src1(stack) cmp src2(HL) → 0 or 1
        _e.Instruction("POP", "DE");     // DE = src1
        _e.Instruction("EX", "DE,HL");   // HL = src1, DE = src2
        _e.Instruction("OR", "A");
        _e.Instruction("SBC", "HL,DE");  // src1 - src2
        _e.Instruction("LD", "HL,$0000");
        _e.Instruction("JR", $"{cond},$+3");
        _e.Instruction("INC", "HL");
    }

    private void EmitCompareGt(IrInstruction inst)
    {
        // src1 > src2 → swap and use C: src2 - src1 で C=1 なら src1 > src2
        _e.Instruction("POP", "DE");     // DE = src1
        // HL = src2, DE = src1 → src2 - src1
        _e.Instruction("OR", "A");
        _e.Instruction("SBC", "HL,DE");  // src2 - src1
        _e.Instruction("LD", "HL,$0000");
        _e.Instruction("JR", "C,$+3");   // C=1 → src1 > src2
        _e.Instruction("INC", "HL");
    }

    private void EmitCompareLe(IrInstruction inst)
    {
        // src1 <= src2 → !(src1 > src2)
        EmitCompareGt(inst);
        _e.Instruction("LD", "A,L");
        _e.Instruction("XOR", "$01");
        _e.Instruction("LD", "L,A");
    }

    private void EmitSignedCompare(IrInstruction inst, string kind)
    {
        _e.Instruction("POP", "DE");
        _e.Instruction("EX", "DE,HL");
        _e.Instruction("CALL", $"SCMP_{kind}");
    }

    private void EmitLogAnd(IrInstruction inst)
    {
        // (src1 != 0) && (src2 != 0)
        // DE=src1(stack), HL=src2
        _e.Instruction("POP", "DE");
        // src1をチェック
        _e.Instruction("LD", "A,D");
        _e.Instruction("OR", "E");
        _e.Instruction("LD", "D,H");  // save src2
        _e.Instruction("LD", "E,L");
        _e.Instruction("LD", "HL,$0000");
        _e.Instruction("JR", "Z,$+7"); // src1==0 → false
        // src2をチェック
        _e.Instruction("LD", "A,D");
        _e.Instruction("OR", "E");
        _e.Instruction("JR", "Z,$+3"); // src2==0 → false
        _e.Instruction("INC", "HL");
    }

    private void EmitLogOr(IrInstruction inst)
    {
        // (src1 != 0) || (src2 != 0)
        _e.Instruction("POP", "DE");
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
        // HL=addr (最後に評価された値), スタック上にvalue
        // POP DE(=value) → *(HL) = DE
        bool isByte = inst.DataSize == 1;
        _e.Instruction("POP", "DE");
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
