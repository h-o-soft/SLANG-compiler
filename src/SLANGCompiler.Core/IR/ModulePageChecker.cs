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
        CheckModuleMap(module, errors);
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

            if (o.OrgAddress < 0 || o.OrgAddress > 0xFFFF || slot == 3 || slot == 7)
                errors.Add(($"{head}: the window must be 0000H-5FFFH or 8000H-DFFFH", o.Span));
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

    private static bool CallsModuleMap(IrFunction f) => f.Instructions.Any(i =>
        i.Op == IrOp.Call && i.Src1.Kind == IrOperandKind.Symbol && string.Equals(i.Src1.Name, ModuleMapName, StringComparison.OrdinalIgnoreCase));

    /// <summary>
    /// ZXN_MODULE(n) を使うなら、全部のモジュールに PAGE が要る (表を作るため)。ZXN_MODULE は本体からだけ呼べる:
    /// モジュールの関数から静的にたどって届くもの (本体の関数を通るものも) はエラー (自分の窓を付け替えると、戻る先が消えるため)
    /// </summary>
    private static void CheckModuleMap(IrModule module, List<(string, SourceSpan)> errors)
    {
        var all = module.Functions.Concat(module.Overlays.SelectMany(o => o.Functions)).ToList();
        if (!all.Any(CallsModuleMap)) return;
        foreach (var o in module.Overlays.Where(o => o.Page == null))
            errors.Add(($"ZXN_MODULE needs PAGE on every #MODULE (#MODULE {o.Index} has none)", o.Span));

        var funcs = all.ToDictionary(f => f.Name, StringComparer.OrdinalIgnoreCase);
        foreach (var o in module.Overlays)
        {
            var seen = new HashSet<string>(o.Functions.Select(f => f.Name), StringComparer.OrdinalIgnoreCase);
            var queue = new Queue<IrFunction>(o.Functions);
            while (queue.Count > 0)
            {
                var f = queue.Dequeue();
                if (CallsModuleMap(f))
                {
                    errors.Add(($"ZXN_MODULE is reached from #MODULE {o.Index} (through {f.Name}); call it from the main program only", o.Span));
                    break;
                }
                foreach (var i in f.Instructions)
                    if (i.Op == IrOp.Call && i.Src1.Kind == IrOperandKind.Symbol && i.Src1.Name != null
                        && funcs.TryGetValue(i.Src1.Name, out var callee) && seen.Add(callee.Name))
                        queue.Enqueue(callee);
            }
        }
    }

    /// <summary>窓が同じ PAGE のモジュールどうしの呼び出しを、呼び出しの表からたどる (直接の CALL だけ。CALL(番地)・#ASM・関数の番地はたどらない)</summary>
    private static void CheckCalls(IrModule module, List<OverlayModule> paged, List<(string, SourceSpan)> errors)
    {
        var owner = new Dictionary<string, OverlayModule?>(StringComparer.OrdinalIgnoreCase);
        var funcs = new Dictionary<string, IrFunction>(StringComparer.OrdinalIgnoreCase);
        foreach (var f in module.Functions) { owner[f.Name] = null; funcs[f.Name] = f; }
        foreach (var o in module.Overlays)
            foreach (var f in o.Functions) { owner[f.Name] = o; funcs[f.Name] = f; }

        IEnumerable<string> Callees(IrFunction f) => f.Instructions
            .Where(i => i.Op == IrOp.Call && i.Src1.Kind == IrOperandKind.Symbol && i.Src1.Name != null && funcs.ContainsKey(i.Src1.Name))
            .Select(i => i.Src1.Name!);

        foreach (var a in paged)
        {
            int slot = a.OrgAddress >> 13;
            var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            var queue = new Queue<string>(a.Functions.Select(f => f.Name));
            foreach (var n in queue) seen.Add(n);
            OverlayModule? hit = null;
            while (queue.Count > 0 && hit == null)
            {
                foreach (var callee in Callees(funcs[queue.Dequeue()]))
                {
                    if (!seen.Add(callee)) continue;
                    var b = owner[callee];
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
