using Xunit;
using SLANGCompiler.IR;
using SLANGCompiler.Runtime;

namespace SLANGCompiler.Tests;

/// <summary>
/// RuntimePlanner の集合演算ロジックを in-memory で検証する単体テスト。
///
/// 既存ランタイムを触らずに、テスト用 RuntimeFunction を直接 RuntimeManager に
/// 注入することで、shared / local / 依存閉包 / alias 正規化 / SLANGINIT inline
/// の各仕様を網羅する。
/// </summary>
public class RuntimePlannerTests
{
    /// <summary>
    /// テスト用 RuntimeManager を組み立てるヘルパー。
    /// asm ソースを与える代わりに RuntimeFunction を直接登録するため、Reflection で
    /// private dict (`_functions`) に流し込む。
    /// </summary>
    private static RuntimeManager BuildRuntime(params (string Name, RuntimeResidency Residency,
        string[] Deps, string[] Aliases, string? InitCode, (string, int)[]? Works)[] funcs)
    {
        var rm = new RuntimeManager();
        var fldFuncs = typeof(RuntimeManager).GetField("_functions",
            System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)!;
        var dict = (Dictionary<string, RuntimeFunction>)fldFuncs.GetValue(rm)!;
        var fldOrder = typeof(RuntimeManager).GetField("_loadOrderCounter",
            System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)!;
        int order = 0;
        foreach (var f in funcs)
        {
            var rf = new RuntimeFunction
            {
                Name = f.Name,
                Code = $"; body of {f.Name}\n RET",
                Dependencies = new List<string>(f.Deps),
                Aliases = new List<string>(f.Aliases),
                Residency = f.Residency,
                InitCode = f.InitCode,
                Works = f.Works?.Select(w => w).ToList(),
                LoadOrder = order++,
            };
            dict[rf.Name] = rf;
            foreach (var alias in rf.Aliases) dict[alias] = rf;
        }
        fldOrder.SetValue(rm, order);
        return rm;
    }

    private static OverlayModule MakeOverlay(int idx, OverlayRuntimePolicy policy)
        => new() { Index = idx, OrgAddress = 0x8000 + idx * 0x1000, RuntimePolicy = policy };

    private static IReadOnlyDictionary<int, HashSet<string>> CalledMap(
        params (int Idx, string[] Names)[] entries)
    {
        var d = new Dictionary<int, HashSet<string>>();
        foreach (var (idx, names) in entries)
            d[idx] = new HashSet<string>(names, StringComparer.OrdinalIgnoreCase);
        return d;
    }

    [Fact]
    public void Test1_LocalPolicy_AllFunctionsGoToOverlayLocal()
    {
        // Local モード (default) では関数の @resident に関わらず overlay 内 local
        var rt = BuildRuntime(
            ("MPRNT", RuntimeResidency.Local, new string[0], new string[0], null, null),
            ("VTOS",  RuntimeResidency.Shared, new string[0], new string[0], null, null));
        var ov = MakeOverlay(0, OverlayRuntimePolicy.Local);
        var plan = RuntimePlanner.Build(
            new HashSet<string>(),
            new[] { ov },
            CalledMap((0, new[] { "MPRNT", "VTOS" })),
            rt, Array.Empty<string>());

        Assert.Empty(plan.MainResidentFunctions);
        Assert.Equal(2, plan.OverlayLocalFunctions[0].Count);
        Assert.Empty(plan.OverlayExternFunctions[0]);
        Assert.Contains("MPRNT", plan.OverlayLocalFunctions[0]);
        Assert.Contains("VTOS", plan.OverlayLocalFunctions[0]);
    }

    [Fact]
    public void Test2_ResidentPolicy_DefaultFunctionStaysLocal()
    {
        // RESIDENT モードでも関数 default (= Local) は overlay local 残留
        var rt = BuildRuntime(
            ("MPRNT", RuntimeResidency.Local, new string[0], new string[0], null, null));
        var ov = MakeOverlay(0, OverlayRuntimePolicy.Resident);
        var plan = RuntimePlanner.Build(
            new HashSet<string>(),
            new[] { ov },
            CalledMap((0, new[] { "MPRNT" })),
            rt, Array.Empty<string>());

        Assert.Empty(plan.MainResidentFunctions);
        Assert.Contains("MPRNT", plan.OverlayLocalFunctions[0]);
        Assert.Empty(plan.OverlayExternFunctions[0]);
    }

    [Fact]
    public void Test3_ResidentPolicy_SharedFunctionPromotedToMain()
    {
        // RESIDENT モード × @resident shared → main 集約 + overlay extern
        var rt = BuildRuntime(
            ("MPRNT", RuntimeResidency.Shared, new string[0], new string[0], null, null));
        var ov = MakeOverlay(0, OverlayRuntimePolicy.Resident);
        var plan = RuntimePlanner.Build(
            new HashSet<string>(),
            new[] { ov },
            CalledMap((0, new[] { "MPRNT" })),
            rt, Array.Empty<string>());

        Assert.Contains("MPRNT", plan.MainResidentFunctions);
        Assert.Empty(plan.OverlayLocalFunctions[0]);
        Assert.Contains("MPRNT", plan.OverlayExternFunctions[0]);
    }

    [Fact]
    public void Test4_LocalOverride_BeatsModuleResidentPolicy()
    {
        // RESIDENT モード × @resident local (明示) → local 強制が勝つ
        var rt = BuildRuntime(
            ("FRAGILE", RuntimeResidency.Local, new string[0], new string[0], null, null));
        var ov = MakeOverlay(0, OverlayRuntimePolicy.Resident);
        var plan = RuntimePlanner.Build(
            new HashSet<string>(),
            new[] { ov },
            CalledMap((0, new[] { "FRAGILE" })),
            rt, Array.Empty<string>());

        Assert.Empty(plan.MainResidentFunctions);
        Assert.Contains("FRAGILE", plan.OverlayLocalFunctions[0]);
    }

    [Fact]
    public void Test5_MainAndOverlayShareSameFunction_OnlyOneCopyInMain()
    {
        // main も overlay も同じ shared 関数を呼ぶ → main に 1 個、overlay は extern
        var rt = BuildRuntime(
            ("MPRNT", RuntimeResidency.Shared, new string[0], new string[0], null, null));
        var ov = MakeOverlay(0, OverlayRuntimePolicy.Resident);
        var plan = RuntimePlanner.Build(
            new HashSet<string> { "MPRNT" },
            new[] { ov },
            CalledMap((0, new[] { "MPRNT" })),
            rt, Array.Empty<string>());

        Assert.Single(plan.MainResidentFunctions, n => n.Equals("MPRNT", StringComparison.OrdinalIgnoreCase));
        Assert.Empty(plan.OverlayLocalFunctions[0]);
        Assert.Contains("MPRNT", plan.OverlayExternFunctions[0]);
    }

    [Fact]
    public void Test6_DependencyClosure_PromotedFunctionPullsItsDependencies()
    {
        // A → B (B も runtime) で A だけ promoted → B も main へ移動 (依存閉包)
        var rt = BuildRuntime(
            ("A", RuntimeResidency.Shared, new[] { "B" }, new string[0], null, null),
            ("B", RuntimeResidency.Local,  new string[0], new string[0], null, null));
        var ov = MakeOverlay(0, OverlayRuntimePolicy.Resident);
        var plan = RuntimePlanner.Build(
            new HashSet<string>(),
            new[] { ov },
            CalledMap((0, new[] { "A" })),
            rt, Array.Empty<string>());

        Assert.Contains("A", plan.MainResidentFunctions);
        Assert.Contains("B", plan.MainResidentFunctions); // 依存先も main 行き
        Assert.Empty(plan.OverlayLocalFunctions[0]);
        Assert.Contains("A", plan.OverlayExternFunctions[0]);
        // overlay は B を直接呼んでいないので extern には B は出ない
    }

    [Fact]
    public void Test7_MixedPolicies_SharedToMainAndLocalToOverlay()
    {
        // overlay 0 = RESIDENT, overlay 1 = Local で同じ shared 関数を呼ぶ
        // → main に 1 個 + overlay 0 は extern + overlay 1 にも local copy
        var rt = BuildRuntime(
            ("F", RuntimeResidency.Shared, new string[0], new string[0], null, null));
        var ov0 = MakeOverlay(0, OverlayRuntimePolicy.Resident);
        var ov1 = MakeOverlay(1, OverlayRuntimePolicy.Local);
        var plan = RuntimePlanner.Build(
            new HashSet<string>(),
            new[] { ov0, ov1 },
            CalledMap((0, new[] { "F" }), (1, new[] { "F" })),
            rt, Array.Empty<string>());

        Assert.Contains("F", plan.MainResidentFunctions);
        Assert.Contains("F", plan.OverlayExternFunctions[0]);
        Assert.Empty(plan.OverlayLocalFunctions[0]);
        // overlay 1 は Local モードなので local copy
        Assert.Empty(plan.OverlayExternFunctions[1]);
        Assert.Contains("F", plan.OverlayLocalFunctions[1]);
    }

    [Fact]
    public void Test8_AliasNormalization_SameFunctionCalledByMultipleNames_OnlyOneEntry()
    {
        // alias で呼ばれても plan の集合は正規名 1 つ (= main 実体は 1 個)
        var rt = BuildRuntime(
            ("RBIT", RuntimeResidency.Shared, new string[0], new[] { "BIT" }, null, null));
        var ov = MakeOverlay(0, OverlayRuntimePolicy.Resident);
        var plan = RuntimePlanner.Build(
            new HashSet<string> { "BIT" },        // main は alias 名で呼ぶ
            new[] { ov },
            CalledMap((0, new[] { "RBIT" })),     // overlay は正規名で呼ぶ
            rt, Array.Empty<string>());

        Assert.Single(plan.MainResidentFunctions);
        Assert.Contains("RBIT", plan.MainResidentFunctions);
        Assert.DoesNotContain("BIT", plan.MainResidentFunctions); // 正規化済み
    }

    [Fact]
    public void Test9_SlanginitInline_FunctionItselfInInline_DepsInResident()
    {
        // SLANGINIT は MainInline、その依存先は MainResident
        var rt = BuildRuntime(
            ("SLANGINIT", RuntimeResidency.Local, new[] { "SETUP" }, new string[0], null, null),
            ("SETUP",     RuntimeResidency.Local, new string[0], new string[0], null, null));
        var plan = RuntimePlanner.Build(
            new HashSet<string>(),
            new List<OverlayModule>(),
            new Dictionary<int, HashSet<string>>(),
            rt, new[] { "SLANGINIT" });

        Assert.Contains("SLANGINIT", plan.MainInlineFunctions);
        Assert.DoesNotContain("SLANGINIT", plan.MainResidentFunctions); // 通常出力からは除外
        Assert.Contains("SETUP", plan.MainResidentFunctions); // 依存先は通常出力
        // Inline ∩ Resident = ∅
        Assert.Empty(plan.MainInlineFunctions.Intersect(plan.MainResidentFunctions,
            StringComparer.OrdinalIgnoreCase));
    }

    [Fact]
    public void Test10_GetMainInitFunctions_IncludesSharedAndInline()
    {
        // shared 関数 + inline 関数の両方の InitCode が main RUNTIME_INIT 用に取れる
        var rt = BuildRuntime(
            ("SHARED_INIT", RuntimeResidency.Shared, new string[0], new string[0],
                "; init for shared", null),
            ("INLINE_INIT", RuntimeResidency.Local, new string[0], new string[0],
                "; init for inline", null));
        var ov = MakeOverlay(0, OverlayRuntimePolicy.Resident);
        var plan = RuntimePlanner.Build(
            new HashSet<string>(),
            new[] { ov },
            CalledMap((0, new[] { "SHARED_INIT" })),
            rt, new[] { "INLINE_INIT" });

        var initNames = plan.GetMainInitFunctions().Select(f => f.Name).ToList();
        Assert.Contains("SHARED_INIT", initNames);
        Assert.Contains("INLINE_INIT", initNames);
    }

    [Fact]
    public void Test11_GetMainWorksFunctions_IncludesSharedFunctionWorks()
    {
        // shared promoted 関数の @works が main __WORK__ に集約される (Codex 指摘の置換漏れ検証)
        var rt = BuildRuntime(
            ("SHARED_W", RuntimeResidency.Shared, new string[0], new string[0],
                null, new (string, int)[] { ("WORK_LABEL", 16) }));
        var ov = MakeOverlay(0, OverlayRuntimePolicy.Resident);
        var plan = RuntimePlanner.Build(
            new HashSet<string>(),
            new[] { ov },
            CalledMap((0, new[] { "SHARED_W" })),
            rt, Array.Empty<string>());

        var worksFuncs = plan.GetMainWorksFunctions().Select(f => f.Name).ToList();
        Assert.Contains("SHARED_W", worksFuncs); // shared promoted 関数の works が main に
    }

    [Fact]
    public void Test12_GetAndConsumeInline_OnlyReturnsMainInlineFunctions()
    {
        var rt = BuildRuntime(
            ("SLANGINIT", RuntimeResidency.Local, new string[0], new string[0], null, null),
            ("MPRNT",     RuntimeResidency.Local, new string[0], new string[0], null, null));
        var plan = RuntimePlanner.Build(
            new HashSet<string> { "MPRNT" },
            new List<OverlayModule>(),
            new Dictionary<int, HashSet<string>>(),
            rt, new[] { "SLANGINIT" });

        Assert.NotNull(plan.GetAndConsumeInline("SLANGINIT"));
        Assert.Null(plan.GetAndConsumeInline("MPRNT")); // resident 側は対象外
    }
}
