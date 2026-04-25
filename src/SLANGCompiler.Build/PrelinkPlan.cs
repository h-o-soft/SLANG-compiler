using System.Text;
using SLANGCompiler.Runtime;

namespace SLANGCompiler.Build;

/// <summary>
/// PR-B2 prelink モード: 各 ASM target (main + 各 overlay) の Exports / Imports
/// セクションを集計し、Pass 1 用 dummy imports + Pass 3 用 combined real imports を
/// 生成する。
///
/// cross-ref が無いプロジェクト (= overlay 無し or ユーザー関数 cross-ref 無し)
/// では `IsTrivial = true` で driver は単段フロー (PR-B 既存パス) に分岐する。
/// </summary>
public class PrelinkPlan
{
    /// <summary>1 つの ASM target (main または overlay) の cross-ref 情報</summary>
    public class TargetInfo
    {
        public string Label { get; set; } = "";   // "main", "overlay 0", ...
        public string AsmPath { get; set; } = "";
        public List<string> Exports { get; set; } = new(); // `; FUNC <name>` から
        public List<string> UserFunctionImports { get; set; } = new(); // PR-B2 新セクション
        public List<string> SharedImports { get; set; } = new();       // PR-B 既存 3 セクションの合算
    }

    public List<TargetInfo> Targets { get; } = new();

    /// <summary>cross-ref が無い (= 全 target で UserFunctionImports が空) なら true。
    /// この場合 driver は PR-B 既存の単段フローを使う。</summary>
    public bool IsTrivial =>
        Targets.All(t => t.UserFunctionImports.Count == 0);

    // PR-A 出力の固定セクションヘッダ (本 PR-B2 で driver が拾う対象)
    private static readonly string[] ExportsSectionHeaders =
    {
        "; === Exported User Functions ===",
    };
    private static readonly string[] UserFunctionImportsSectionHeaders =
    {
        "; === User Function References ===",
    };
    private static readonly string[] SharedImportsSectionHeaders =
    {
        "; === Shared Runtime References (resolved via two-stage assembly) ===",
        "; === Shared Symbols (from main) ===",
        "; === String references (from main) ===",
    };

    /// <summary>
    /// 与えられた ASM ファイル群から PrelinkPlan を構築する。
    /// </summary>
    /// <param name="targets">(label, asmPath) のリスト。例: [("main", "test.ASM"), ("overlay 0", "test._m0.ASM"), ...]</param>
    public static PrelinkPlan Build(IEnumerable<(string Label, string AsmPath)> targets)
    {
        var plan = new PrelinkPlan();
        foreach (var (label, asmPath) in targets)
        {
            var asmText = File.ReadAllText(asmPath);
            plan.Targets.Add(new TargetInfo
            {
                Label = label,
                AsmPath = asmPath,
                Exports = AsmSectionParser.ExtractFuncNames(asmText, ExportsSectionHeaders),
                UserFunctionImports = AsmSectionParser.ExtractExternNames(asmText, UserFunctionImportsSectionHeaders),
                SharedImports = AsmSectionParser.ExtractExternNames(asmText, SharedImportsSectionHeaders),
            });
        }
        return plan;
    }

    /// <summary>
    /// Pass 1 用: 全 extern を $0000 EQU で埋めた dummy imports.asm を書き出す。
    /// 命令長確定のため target の AILZ80ASM Pass 1 でアセンブルできる状態にする。
    /// </summary>
    public static void WriteDummyImports(TargetInfo target, string outputPath)
    {
        var sb = new StringBuilder();
        sb.AppendLine("; SLANG slangbuild — Pass 1 dummy imports (all $0000 for length determination)");
        sb.AppendLine($"; source target: {Path.GetFileName(target.AsmPath)}");
        sb.AppendLine();
        var allExterns = target.UserFunctionImports.Concat(target.SharedImports)
                              .Distinct(StringComparer.OrdinalIgnoreCase)
                              .OrderBy(n => n, StringComparer.OrdinalIgnoreCase);
        foreach (var name in allExterns)
            sb.AppendLine($"{name} equ $0000");
        File.WriteAllText(outputPath, sb.ToString());
    }

    /// <summary>
    /// Pass 3 用: combined real imports.asm を書き出す。
    ///   - User Function References → ExportedFunctionTable から実アドレス
    ///   - Shared Imports → mainPass1Symbols (= main resident な実アドレス) から
    ///
    /// 戻り値: (出力 path, 未解決ラベル名のリスト)。未解決は warning 扱いで素通し
    /// (AILZ80ASM 側で「未定義シンボル」エラーとして報告される設計)。
    /// </summary>
    public static (string OutputPath, List<string> Unresolved) WriteRealImports(
        TargetInfo target,
        ExportedFunctionTable exportedTable,
        IReadOnlyDictionary<string, int> mainPass1Symbols,
        string outputPath)
    {
        var unresolved = new List<string>();
        var sb = new StringBuilder();
        sb.AppendLine("; SLANG slangbuild — Pass 3 combined real imports");
        sb.AppendLine($"; source target: {Path.GetFileName(target.AsmPath)}");
        sb.AppendLine();

        // 1) User Function References: target が呼ぶ他 target の関数を ExportedFunctionTable で解決
        if (target.UserFunctionImports.Count > 0)
        {
            sb.AppendLine("; --- user function cross-references ---");
            foreach (var name in target.UserFunctionImports)
            {
                var addr = exportedTable.Resolve(name);
                if (addr.HasValue)
                    sb.AppendLine($"{name} equ ${addr.Value:X4}");
                else
                    unresolved.Add(name);
            }
            sb.AppendLine();
        }

        // 2) Shared Imports: PR-B 経路 (main resident な runtime / global / string)
        if (target.SharedImports.Count > 0)
        {
            sb.AppendLine("; --- shared runtime / globals / strings (from main) ---");
            foreach (var name in target.SharedImports)
            {
                if (mainPass1Symbols.TryGetValue(name, out var addr))
                    sb.AppendLine($"{name} equ ${addr:X4}");
                else
                    unresolved.Add(name);
            }
        }

        File.WriteAllText(outputPath, sb.ToString());
        return (outputPath, unresolved);
    }
}
