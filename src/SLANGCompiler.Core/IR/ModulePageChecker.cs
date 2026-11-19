using SLANGCompiler.Lexer;

namespace SLANGCompiler.IR;

/// <summary>
/// `#MODULE $addr PAGE n` (zxn) の、コンパイルのときに分かる決まりを調べる。
/// 1 つのモジュールは 8K の窓 1 つ (窓 = 番地の上 3 ビット)。組んだ後の大きさと本体との重なりは slangbuild が調べる。
///   - env に module_pages が無ければ PAGE はエラー
///   - ページは 18〜222。モジュールどうしで同じページはエラー
///   - 窓は MMU0〜2・4〜6 (6000H〜7FFFH と E000H〜FFFFH は使えない。Next の SLANG IDE と同じ)
///   - MMU0・1 (0000H〜3FFFH) は CONST __LOWRAM = n が要る。MMU0 は 0010H から、ページは n。MMU1 は n 以外。
///     ほかの窓は n・n+1 を使えない (始まりで n の 0008H〜000AH に JP を書くため)
///   - 窓が同じ PAGE のモジュールどうしの呼び出し (本体の関数を通って届くものも) はエラー
/// </summary>
public static class ModulePageChecker
{
    public const int PageMin = 18;
    public const int PageMax = 222;

    /// <summary>エラーの文と場所を返す (無ければ空)。lowRam は CONST __LOWRAM のページ (0 は無し)</summary>
    public static List<(string Message, SourceSpan Span)> Check(IrModule module, bool modulePages, int lowRam)
    {
        var errors = new List<(string, SourceSpan)>();
        if (modulePages) CheckModuleMap(module, errors);
        var paged = module.Overlays.Where(o => o.Page != null).ToList();
        if (paged.Count == 0) return errors;
        if (!modulePages)
        {
            errors.Add(("#MODULE PAGE needs an env with module pages (zxn)", paged[0].Span));
            return errors;
        }

        var usedPages = new Dictionary<int, int>();
        foreach (var o in paged)
        {
            int page = o.Page!.Value, slot = o.OrgAddress >> 13;
            string head = $"#MODULE {o.Index} (${o.OrgAddress:X4} PAGE {page})";
            if (page < PageMin || page > PageMax)
                errors.Add(($"{head}: PAGE must be {PageMin} to {PageMax}", o.Span));
            else if (usedPages.TryGetValue(page, out var other))
                errors.Add(($"{head}: page {page} is already used by #MODULE {other}", o.Span));
            else
                usedPages[page] = o.Index;

            // 4000H-5FFFH: ULA の画面と NextZXOS の変数。IM 1 の割り込みが毎フレーム 5C00H〜を書くので、付けたページが壊れる
            if (o.OrgAddress < 0 || o.OrgAddress > 0xFFFF || slot == 2 || slot == 3 || slot == 7)
                errors.Add(($"{head}: the window must be 0000H-3FFFH or 8000H-DFFFH", o.Span));
            else if (slot <= 1)
            {
                if (lowRam == 0)
                    errors.Add(($"{head}: a module at 0000H-3FFFH needs CONST __LOWRAM", o.Span));
                else if (slot == 0 && o.OrgAddress < 0x10)
                    errors.Add(($"{head}: a module at 0000H-1FFFH starts at 0010H or later (0000H-000FH is for the runtime)", o.Span));
                else if (slot == 0 && page != lowRam)
                    errors.Add(($"{head}: a module at 0000H-1FFFH must use the __LOWRAM page {lowRam}", o.Span));
                else if (slot == 1 && page == lowRam)
                    errors.Add(($"{head}: the __LOWRAM page {lowRam} is for 0000H-1FFFH", o.Span));
            }
            else if (lowRam != 0 && (page == lowRam || page == lowRam + 1))
                errors.Add(($"{head}: the __LOWRAM pages {lowRam} and {lowRam + 1} are for 0000H-3FFFH", o.Span));
        }

        CheckCalls(module, paged, errors);
        return errors;
    }

    private const string ModuleMapName = "ZXN_MODULE";

    /// <summary>
    /// 関数が呼ぶ名前: IR の呼び出しと、#ASM の中の `CALL 名前` の行 (コード生成がランタイムを選ぶのと同じ読み方)。
    /// `CALL(番地)`・関数の番地を使う呼び出しは入らない
    /// </summary>
    private static IEnumerable<string> CalledNames(IrFunction f) =>
        IrCalledNames(f).Concat(InlineAsmCalls.InFunctions(new[] { f }).SelectMany(InlineAsmCalls.Names));

    private static IEnumerable<string> IrCalledNames(IrFunction f) =>
        f.Instructions.Where(i => i.Op == IrOp.Call && i.Src1.Kind == IrOperandKind.Symbol && i.Src1.Name != null)
                      .Select(i => i.Src1.Name!);

    private static bool IsModuleMap(string name) =>
        string.Equals(name, ModuleMapName, StringComparison.OrdinalIgnoreCase);

    /// <summary>
    /// 呼び出しの表の 1 つ: 持ち主のモジュール (本体は null)、呼ぶ名前、ランタイムの ZXN_MODULE を呼ぶか
    /// (その単位で見える #ASM のラベルに ZXN_MODULE があれば、それはランタイムではない。コード生成と同じ)
    /// </summary>
    private sealed record Node(OverlayModule? Owner, List<string> Calls, bool CallsMap);

    /// <summary>
    /// 呼び出しの表 (名前 → Node)。関数と、本体のトップレベルの #ASM の行の頭のラベル (モジュールの #ASM から CALL できる)。
    /// トップレベルの #ASM は #ASM ... #END の 1 つずつを塊として扱う: ラベルを呼ぶと、そのラベルのある塊の中の CALL の行を全部呼ぶとみなす
    /// (塊の中のラベルの間の切れ目や、どこで戻るかは読まない。多めにたどる)
    /// </summary>
    private static Dictionary<string, Node> CallGraph(IrModule module)
    {
        var mainLabels = InlineAsmCalls.VisibleLabels(module, null);
        // SLANG の呼び出しはいつもランタイム。#ASM の CALL は、見えるラベルに同じ名前が無いときだけ
        bool AsmCallsMap(IEnumerable<string> asmCalls, HashSet<string> labels) =>
            !labels.Contains(ModuleMapName) && asmCalls.Any(IsModuleMap);
        bool FuncCallsMap(IrFunction f, HashSet<string> labels) =>
            IrCalledNames(f).Any(IsModuleMap)
            || AsmCallsMap(InlineAsmCalls.InFunctions(new[] { f }).SelectMany(InlineAsmCalls.Names), labels);

        var g = new Dictionary<string, Node>(StringComparer.OrdinalIgnoreCase);
        foreach (var f in module.Functions)
            g[f.Name] = new Node(null, CalledNames(f).ToList(), FuncCallsMap(f, mainLabels));
        foreach (var o in module.Overlays)
        {
            var labels = InlineAsmCalls.VisibleLabels(module, o);
            foreach (var f in o.Functions)
                g[f.Name] = new Node(o, CalledNames(f).ToList(), FuncCallsMap(f, labels));
        }
        foreach (var block in InlineAsmCalls.TopLevel(module))
        {
            var calls = InlineAsmCalls.Names(block).ToList();
            var node = new Node(null, calls, AsmCallsMap(calls, mainLabels));
            foreach (var label in InlineAsmCalls.Labels(block.Split('\n')))
                g.TryAdd(label, node);
        }
        return g;
    }

    /// <summary>
    /// (module_pages のある env で、ランタイムの ZXN_MODULE のとき)
    /// ZXN_MODULE(n) を使うなら、全部のモジュールに PAGE が要る (表を作るため)。ZXN_MODULE は本体からだけ呼べる:
    /// モジュールの関数から静的にたどって届くもの (本体の関数・トップレベルの #ASM を通るものも) はエラー (自分の窓を付け替えると、戻る先が消えるため)
    /// </summary>
    private static void CheckModuleMap(IrModule module, List<(string, SourceSpan)> errors)
    {
        var all = module.Functions.Concat(module.Overlays.SelectMany(o => o.Functions)).ToList();
        // 利用者が ZXN_MODULE という名前の関数を書いたなら、それはランタイムの ZXN_MODULE ではない
        if (all.Any(f => IsModuleMap(f.Name))) return;
        var g = CallGraph(module);
        // トップレベルの #ASM の CALL でも、ランタイムの ZXN_MODULE が入る (ラベルの無い塊も)
        var mainLabels = InlineAsmCalls.VisibleLabels(module, null);
        if (!g.Values.Any(n => n.CallsMap)
            && (mainLabels.Contains(ModuleMapName) || !InlineAsmCalls.TopLevel(module).SelectMany(InlineAsmCalls.Names).Any(IsModuleMap))) return;
        foreach (var o in module.Overlays.Where(o => o.Page == null))
            errors.Add(($"ZXN_MODULE needs PAGE on every #MODULE (#MODULE {o.Index} has none)", o.Span));

        foreach (var o in module.Overlays)
        {
            var seen = new HashSet<string>(o.Functions.Select(f => f.Name), StringComparer.OrdinalIgnoreCase);
            var queue = new Queue<string>(o.Functions.Select(f => f.Name));
            while (queue.Count > 0)
            {
                var name = queue.Dequeue();
                var node = g[name];
                if (node.CallsMap)
                {
                    errors.Add(($"ZXN_MODULE is reached from #MODULE {o.Index} (through {name}); call it from the main program only", o.Span));
                    break;
                }
                foreach (var n in node.Calls)
                    if (g.ContainsKey(n) && seen.Add(n))
                        queue.Enqueue(n);
            }
        }
    }

    /// <summary>
    /// 窓が同じ PAGE のモジュールどうしの呼び出しを、呼び出しの表からたどる (IR の呼び出しと #ASM の CALL の行。
    /// 本体のトップレベルの #ASM を通るものも。CALL(番地)・関数の番地はたどらない)
    /// </summary>
    private static void CheckCalls(IrModule module, List<OverlayModule> paged, List<(string, SourceSpan)> errors)
    {
        var g = CallGraph(module);
        foreach (var a in paged)
        {
            int slot = a.OrgAddress >> 13;
            var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            var queue = new Queue<string>(a.Functions.Select(f => f.Name));
            foreach (var n in queue) seen.Add(n);
            OverlayModule? hit = null;
            while (queue.Count > 0 && hit == null)
            {
                foreach (var callee in g[queue.Dequeue()].Calls.Where(g.ContainsKey))
                {
                    if (!seen.Add(callee)) continue;
                    var b = g[callee].Owner;
                    if (b != null && b != a && b.Page != null && (b.OrgAddress >> 13) == slot)
                    {
                        hit = b;
                        errors.Add(($"#MODULE {a.Index} (${a.OrgAddress:X4}) reaches {callee} of #MODULE {b.Index} (${b.OrgAddress:X4}) in the same window", a.Span));
                        break;
                    }
                    queue.Enqueue(callee);
                }
            }
        }
    }
}
