using SLANGCompiler.Runtime;

namespace SLANGCompiler.Build;

/// <summary>
/// PR-B2 prelink Pass 2 で構築する「全 target が export 宣言した関数」のアドレス
/// 集約 table。
///
/// 設計の核心 (Codex 指摘):
///   全 .sym を union するのではなく、各 target の `; === Exported User Functions ===`
///   セクションに列挙された関数名だけを採用する。runtime 関数 (MPRNT, DIVHLDE8.div81
///   等) は target ごとに複製されて .sym に乗るが、export set には入らないので
///   衝突しない。local label / private symbol も自動的に除外される。
/// </summary>
public class ExportedFunctionTable
{
    /// <summary>関数名 (case-insensitive) → 絶対アドレス</summary>
    public Dictionary<string, int> Symbols { get; } =
        new(StringComparer.OrdinalIgnoreCase);

    /// <summary>関数名 → 定義元の説明 (例: "main", "overlay 0")</summary>
    public Dictionary<string, string> SourceByName { get; } =
        new(StringComparer.OrdinalIgnoreCase);

    /// <summary>
    /// 1 target 分の export 関数を Pass 1 sym から登録する。同名重複は防御エラー。
    /// </summary>
    /// <param name="sourceLabel">"main" or "overlay 0" 等、エラー報告用ラベル</param>
    /// <param name="exportNames">この target が `; FUNC <name>` で宣言した関数名集合</param>
    /// <param name="pass1Symbols">この target の Pass 1 出力 .sym (label → addr)</param>
    public void Add(string sourceLabel, IEnumerable<string> exportNames,
                    IReadOnlyDictionary<string, int> pass1Symbols)
    {
        foreach (var name in exportNames)
        {
            if (!pass1Symbols.TryGetValue(name, out var addr))
            {
                // export 宣言があるのに pass 1 sym に対応ラベルが無い = compiler のバグ
                // または ASM の手書き不整合。スキップして警告 (driver 上位で詳細表示)
                continue;
            }
            if (Symbols.TryGetValue(name, out var existingAddr))
            {
                if (existingAddr != addr)
                {
                    throw new InvalidOperationException(
                        $"ExportedFunctionTable: duplicate export '{name}' with different "
                        + $"addresses ({SourceByName[name]}: ${existingAddr:X4}, "
                        + $"{sourceLabel}: ${addr:X4}). "
                        + "SLANG semantic should have prevented same-named exports across targets; "
                        + "this is a defensive check.");
                }
                // 同名同アドレスは無視 (= alias 等で偶然同じになるケース、本来あり得ない)
                continue;
            }
            Symbols[name] = addr;
            SourceByName[name] = sourceLabel;
        }
    }

    /// <summary>関数名を解決。未登録なら null。</summary>
    public int? Resolve(string name)
        => Symbols.TryGetValue(name, out var addr) ? addr : null;
}
