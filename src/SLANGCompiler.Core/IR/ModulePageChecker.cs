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

    /// <summary>
    /// 関数が呼ぶ名前: IR の呼び出しと、#ASM の中の `CALL 名前` の行 (コード生成がランタイムを選ぶのと同じ読み方)。
    /// `CALL(番地)`・関数の番地を使う呼び出しは入らない
    /// </summary>
    private static IEnumerable<string> CalledNames(IrFunction f)
    {
        foreach (var i in f.Instructions)
        {
            if (i.Op == IrOp.Call && i.Src1.Kind == IrOperandKind.Symbol && i.Src1.Name != null)
                yield return i.Src1.Name;
            else if (i.Op == IrOp.InlineAsm)
            {
                var asm = i.Dest.Kind == IrOperandKind.Temp ? i.Src1.Name : i.Dest.Name;
                if (asm == null) continue;
                foreach (var line in asm.Split('\n'))
                {
                    // `CALL 名前`・`CALL Z,名前` (条件つきも)。大文字・小文字は問わない
                    var t = line.Trim();
                    if (t.Length < 5 || !t.StartsWith("CALL", StringComparison.OrdinalIgnoreCase) || (t[4] != ' ' && t[4] != '\t')) continue;
                    var operand = t[5..].Split(';')[0].Trim();
                    var name = operand[(operand.LastIndexOf(',') + 1)..].Trim();
                    if (name.Length > 0) yield return name;
                }
            }
        }
    }

    private static bool CallsModuleMap(IrFunction f) =>
        CalledNames(f).Any(n => string.Equals(n, ModuleMapName, StringComparison.OrdinalIgnoreCase));

    /// <summary>
    /// (module_pages のある env で、ランタイムの ZXN_MODULE のとき)
    /// ZXN_MODULE(n) を使うなら、全部のモジュールに PAGE が要る (表を作るため)。ZXN_MODULE は本体からだけ呼べる:
    /// モジュールの関数から静的にたどって届くもの (本体の関数を通るものも) はエラー (自分の窓を付け替えると、戻る先が消えるため)
    /// </summary>
    private static void CheckModuleMap(IrModule module, List<(string, SourceSpan)> errors)
    {
        var all = module.Functions.Concat(module.Overlays.SelectMany(o => o.Functions)).ToList();
        // 利用者が ZXN_MODULE という名前の関数を書いたなら、それはランタイムの ZXN_MODULE ではない
        if (all.Any(f => string.Equals(f.Name, ModuleMapName, StringComparison.OrdinalIgnoreCase))) return;
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
                foreach (var n in CalledNames(f))
                    if (funcs.TryGetValue(n, out var callee) && seen.Add(callee.Name))
                        queue.Enqueue(callee);
            }
        }
    }

    /// <summary>窓が同じ PAGE のモジュールどうしの呼び出しを、呼び出しの表からたどる (IR の呼び出しと #ASM の CALL の行。CALL(番地)・関数の番地はたどらない)</summary>
    private static void CheckCalls(IrModule module, List<OverlayModule> paged, List<(string, SourceSpan)> errors)
    {
        var owner = new Dictionary<string, OverlayModule?>(StringComparer.OrdinalIgnoreCase);
        var funcs = new Dictionary<string, IrFunction>(StringComparer.OrdinalIgnoreCase);
        foreach (var f in module.Functions) { owner[f.Name] = null; funcs[f.Name] = f; }
        foreach (var o in module.Overlays)
            foreach (var f in o.Functions) { owner[f.Name] = o; funcs[f.Name] = f; }

        IEnumerable<string> Callees(IrFunction f) => CalledNames(f).Where(funcs.ContainsKey);

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
