using SLANGCompiler.IR;

namespace SLANGCompiler.Runtime;

/// <summary>
/// CodeGenerator が runtime 関数の出力先 (main resident / overlay local / overlay extern)
/// を決定するための plan オブジェクト。
///
/// 設計の核心:
///   - 集合演算は **正規名 (RuntimeFunction.Name)** ベースで行う。alias 名は入力時に
///     正規化されるため、`MainResidentFunctions` には同じ関数が 2 度入らない (= main
///     実体は必ず 1 個)。
///   - SLANGINIT のような main inline 関数は `MainInlineFunctions` で別管理し、
///     `MainResidentFunctions` からは除外する (inline 展開 + 通常 runtime 出力から exclude
///     という既存挙動を仕様化)。
///   - shared promoted な overlay 呼び出し関数の依存閉包は再帰的に `MainResidentFunctions`
///     に積まれる (依存漏れ防止)。
/// </summary>
public class RuntimePlan
{
    private readonly RuntimeManager _runtime;

    /// <summary>main ASM に通常出力する関数集合 (依存込み, 正規名)。inline は除外済。</summary>
    public HashSet<string> MainResidentFunctions { get; }

    /// <summary>main ASM 内に inline 展開し、通常 runtime 出力からは除外する関数集合
    /// (= 既存 GetAndExclude 相当)。SLANGINIT が代表例。</summary>
    public HashSet<string> MainInlineFunctions { get; }

    /// <summary>overlay i に local 出力する関数集合 (依存込み, 正規名)</summary>
    public Dictionary<int, HashSet<string>> OverlayLocalFunctions { get; }

    /// <summary>overlay i から EXTERN で main を呼ぶ関数集合 (正規名)</summary>
    public Dictionary<int, HashSet<string>> OverlayExternFunctions { get; }

    internal RuntimePlan(
        RuntimeManager runtime,
        HashSet<string> mainResident,
        HashSet<string> mainInline,
        Dictionary<int, HashSet<string>> overlayLocal,
        Dictionary<int, HashSet<string>> overlayExtern)
    {
        _runtime = runtime;
        MainResidentFunctions = mainResident;
        MainInlineFunctions = mainInline;
        OverlayLocalFunctions = overlayLocal;
        OverlayExternFunctions = overlayExtern;
    }

    /// <summary>main 用: 通常出力すべき関数 (LoadOrder 順, MainInlineFunctions は除外済)</summary>
    public IEnumerable<RuntimeFunction> GetMainOutputFunctions()
    {
        return MainResidentFunctions
            .Select(n => _runtime.Functions.TryGetValue(n, out var f) ? f : null)
            .Where(f => f != null)
            .Distinct()
            .OrderBy(f => f!.LoadOrder)!;
    }

    /// <summary>main 用: inline 展開する関数を取得 (= 既存 GetAndExclude の plan 版)。
    /// `MainInlineFunctions` に登録されていれば関数本体を返す。
    /// 副作用なし (plan からは除去しない) — exclude 効果は GetMainOutputFunctions が
    /// MainInlineFunctions を返さないことで担保している。</summary>
    public RuntimeFunction? GetInlineFunction(string name)
    {
        var canonical = Normalize(name, _runtime);
        if (MainInlineFunctions.Contains(canonical)
            && _runtime.Functions.TryGetValue(canonical, out var func))
        {
            return func;
        }
        return null;
    }

    /// <summary>main 用: InitCode を持つ関数 (RUNTIME_INIT 用)。
    /// MainResident + MainInline 両方を対象とする (inline 関数自身が @init_code を
    /// 持つ場合の保険)。</summary>
    public IEnumerable<RuntimeFunction> GetMainInitFunctions()
    {
        return MainResidentFunctions.Concat(MainInlineFunctions)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .Select(n => _runtime.Functions.TryGetValue(n, out var f) ? f : null)
            .Where(f => f != null && !string.IsNullOrEmpty(f!.InitCode))
            .Distinct()
            .OrderBy(f => f!.LoadOrder)!;
    }

    /// <summary>main 用: @works を持つ関数 (EmitWorkArea 用)。
    /// MainResident + MainInline 両方を対象 (= shared 関数の works が main __WORK__
    /// に集約される)。</summary>
    public IEnumerable<RuntimeFunction> GetMainWorksFunctions()
    {
        return MainResidentFunctions.Concat(MainInlineFunctions)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .Select(n => _runtime.Functions.TryGetValue(n, out var f) ? f : null)
            .Where(f => f != null && f!.Works != null && f.Works.Count > 0)
            .Distinct()
            .OrderBy(f => f!.LoadOrder)!;
    }

    /// <summary>overlay i 用: local 出力すべき関数 (LoadOrder 順)</summary>
    public IEnumerable<RuntimeFunction> GetOverlayOutputFunctions(int overlayIndex)
    {
        if (!OverlayLocalFunctions.TryGetValue(overlayIndex, out var set))
            return Enumerable.Empty<RuntimeFunction>();
        return set
            .Select(n => _runtime.Functions.TryGetValue(n, out var f) ? f : null)
            .Where(f => f != null)
            .Distinct()
            .OrderBy(f => f!.LoadOrder)!;
    }

    /// <summary>overlay i 用: EXTERN として参照する main resident な関数名 (正規名, 名前順)。
    /// overlay ASM 末尾の "Shared Runtime References" コメントに使う。</summary>
    public IEnumerable<string> GetOverlayExternNames(int overlayIndex)
    {
        if (!OverlayExternFunctions.TryGetValue(overlayIndex, out var set))
            return Enumerable.Empty<string>();
        return set.OrderBy(n => n, StringComparer.OrdinalIgnoreCase);
    }

    /// <summary>overlay i 用: local copy 出力される関数のうち @works を持つもの (LoadOrder 順)。
    /// 各 function の Works property を呼出側で集めて overlay ASM の 「Shared Work
    /// Labels」 EXTERN listing 構築に使う (= Issue #209、 main 側 sWORK BSS sym を
    /// EXTERN 参照する形に変換)。</summary>
    public IEnumerable<RuntimeFunction> GetOverlayWorksFunctions(int overlayIndex)
    {
        if (!OverlayLocalFunctions.TryGetValue(overlayIndex, out var set))
            return Enumerable.Empty<RuntimeFunction>();
        return set
            .Select(n => _runtime.Functions.TryGetValue(n, out var f) ? f : null)
            .Where(f => f != null && f!.Works != null && f.Works.Count > 0)
            .Distinct()
            .OrderBy(f => f!.LoadOrder)!;
    }

    /// <summary>main __WORK__ に allocate すべき @works を持つ関数 (= MainResident +
    /// MainInline + 全 OverlayLocalFunctions の union、 LoadOrder 順)。
    /// EmitWorkArea から呼ばれ、 overlay local copy runtime function の Works sym も
    /// main __WORK__ に確保される (= Issue #209、 overlay からは EXTERN で main 側参照)。
    /// main が当該 runtime call を使わなくても overlay のみ使う場合に main.sym へ EQU 出力
    /// される保証。</summary>
    public IEnumerable<RuntimeFunction> GetMainAllocatedWorksFunctions()
    {
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var result = new List<RuntimeFunction>();
        foreach (var f in GetMainWorksFunctions())
            if (seen.Add(f.Name)) result.Add(f);
        foreach (var kv in OverlayLocalFunctions)
            foreach (var f in GetOverlayWorksFunctions(kv.Key))
                if (seen.Add(f.Name)) result.Add(f);
        return result.OrderBy(f => f.LoadOrder);
    }

    internal static string Normalize(string name, RuntimeManager runtime)
    {
        // alias 名 → 正規名へ。RuntimeManager.Functions には alias 経由でも引けるよう
        // 同じ RuntimeFunction が複数キーで登録されている。func.Name が正規名。
        if (runtime.Functions.TryGetValue(name, out var func))
            return func.Name;
        return name; // 未知 (ユーザー関数等) は素通り
    }
}

/// <summary>
/// runtime 関数の集約決定エンジン (純粋ロジック)。
///
/// 入出力は集合だけで、CodeGenerator / RuntimeManager の状態を変更しない (副作用なし)。
/// </summary>
public static class RuntimePlanner
{
    /// <summary>
    /// 集約 plan を構築する。
    /// </summary>
    /// <param name="mainCalled">main の関数本体から呼ばれた関数名集合 (alias 名でも可)</param>
    /// <param name="overlays">全オーバーレイ</param>
    /// <param name="overlayCalled">overlay インデックス → そのオーバーレイから呼ばれた関数名集合</param>
    /// <param name="runtime">RuntimeManager (alias / 依存解決用)</param>
    /// <param name="mainInlineNames">main に inline 展開する関数名 (例: ["SLANGINIT"])。
    ///   依存閉包は MainResident に積まれる。inline 関数自身は MainInline に保持される。</param>
    public static RuntimePlan Build(
        IEnumerable<string> mainCalled,
        IReadOnlyList<OverlayModule> overlays,
        IReadOnlyDictionary<int, HashSet<string>> overlayCalled,
        RuntimeManager runtime,
        IEnumerable<string> mainInlineNames)
    {
        var inlineSet = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var residentSet = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        // Phase 1: inline 関数自身は MainInline、依存先は MainResident
        foreach (var raw in mainInlineNames)
        {
            var name = RuntimePlan.Normalize(raw, runtime);
            if (!runtime.Functions.TryGetValue(name, out var func)) continue;
            inlineSet.Add(name);
            foreach (var dep in func.Dependencies)
                CollectClosure(dep, residentSet, runtime);
        }

        // Phase 2: main called → MainResident
        foreach (var raw in mainCalled)
            CollectClosure(raw, residentSet, runtime);

        // Phase 3: overlay の shared promotion を MainResident に追加
        foreach (var overlay in overlays)
        {
            if (overlay.RuntimePolicy != OverlayRuntimePolicy.Resident) continue;
            if (!overlayCalled.TryGetValue(overlay.Index, out var called)) continue;

            foreach (var raw in called)
            {
                var name = RuntimePlan.Normalize(raw, runtime);
                if (!runtime.Functions.TryGetValue(name, out var func)) continue;
                if (func.Residency == RuntimeResidency.Shared)
                    CollectClosure(name, residentSet, runtime); // 依存閉包込みで main 行き
            }
        }

        // Phase 4: overlay local の確定 (依存閉包) + main 集合との重複は extern に分離
        var overlayLocal = new Dictionary<int, HashSet<string>>();
        var overlayExtern = new Dictionary<int, HashSet<string>>();
        foreach (var overlay in overlays)
        {
            var localSet = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            var externSet = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            if (overlayCalled.TryGetValue(overlay.Index, out var called))
            {
                foreach (var raw in called)
                {
                    var name = RuntimePlan.Normalize(raw, runtime);
                    if (!runtime.Functions.TryGetValue(name, out var func)) continue;

                    bool sharedToMain = overlay.RuntimePolicy == OverlayRuntimePolicy.Resident
                                      && func.Residency == RuntimeResidency.Shared;
                    if (sharedToMain)
                    {
                        // shared promoted: 自分は extern 参照、依存閉包は Phase 3 で main 入り済み
                        externSet.Add(name);
                    }
                    else
                    {
                        // local: 依存閉包込みで overlay へ (= @resident local が module
                        //   RESIDENT を override する経路もここを通る)
                        CollectClosure(name, localSet, runtime);
                    }
                }
            }

            // localSet 内に main (resident or inline) と重複するものがあれば extern に分離。
            // ただし Local モード (= 現状互換の self-contained) では分離せず、各 overlay
            // 独立で local copy を持つ (関数が偶然 main にもあっても overlay は自分の copy
            // を呼ぶ、現状の挙動を維持)。Resident モードでのみメモリ節約のため main に
            // 集約された関数は overlay 側で extern 参照に切替える。
            if (overlay.RuntimePolicy == OverlayRuntimePolicy.Resident)
            {
                var toExtern = localSet
                    .Where(n => residentSet.Contains(n) || inlineSet.Contains(n))
                    .ToList();
                foreach (var n in toExtern)
                {
                    localSet.Remove(n);
                    externSet.Add(n);
                }
            }

            overlayLocal[overlay.Index] = localSet;
            overlayExtern[overlay.Index] = externSet;
        }

        // Phase 5: MainInline ∩ MainResident = ∅ を保証 (inline 関数自身が他経路で
        // resident に入った場合も inline 側を優先して resident から除外)
        residentSet.ExceptWith(inlineSet);

        return new RuntimePlan(runtime, residentSet, inlineSet, overlayLocal, overlayExtern);
    }

    private static void CollectClosure(string rawName, HashSet<string> result, RuntimeManager runtime)
    {
        var name = RuntimePlan.Normalize(rawName, runtime);
        if (!result.Add(name)) return;
        if (runtime.Functions.TryGetValue(name, out var func))
        {
            foreach (var dep in func.Dependencies)
                CollectClosure(dep, result, runtime);
        }
    }
}
