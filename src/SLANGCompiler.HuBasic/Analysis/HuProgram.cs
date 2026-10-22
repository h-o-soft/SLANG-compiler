using SLANGCompiler.HuBasic.Parsing;

namespace SLANGCompiler.HuBasic.Analysis;

/// <summary>
/// 解析済みのプログラム。前段 (字句・構文・解析) の出力で、後段に渡す型。
/// 出力の形 (SLANG など) には依存しない。
/// 構文木のノードを鍵にする表は、参照の同一性で引く。
/// </summary>
public sealed class HuProgram
{
    /// <summary>行番号の昇順に並べた行</summary>
    public required IReadOnlyList<HuLine> Lines { get; init; }

    /// <summary>整数の単純変数 (名前は接尾辞込み: "A", "A%")</summary>
    public required IReadOnlyList<string> IntVars { get; init; }

    /// <summary>文字列の単純変数 (名前は "A$")</summary>
    public required IReadOnlyList<string> StrVars { get; init; }

    /// <summary>整数の配列: 名前 → 記憶域の上限の添字 (DIM の上限の最大値と 10 の大きい方)</summary>
    public required IReadOnlyDictionary<string, int> Arrays { get; init; }

    /// <summary>文字列リテラル (重複をまとめたもの、X1 文字コード)。添字が ID</summary>
    public required IReadOnlyList<string> Literals { get; init; }

    /// <summary>DATA の本体 ([長さ][本体] の並び)</summary>
    public required byte[] DataBytes { get; init; }

    /// <summary>DATA のある行: (行番号, DataBytes 内の位置)。行番号の昇順</summary>
    public required IReadOnlyList<(int Line, int Offset)> DataLines { get; init; }

    /// <summary>FOR の出現番号 (0 から)</summary>
    public required IReadOnlyDictionary<HuFor, int> ForIds { get; init; }

    /// <summary>FOR が 1 回も回らないときの飛び先: (NEXT 文, その中の変数の部分の位置)</summary>
    public required IReadOnlyDictionary<HuFor, (HuNext Next, int Part)> ForExits { get; init; }

    /// <summary>GOSUB (と ON … GOSUB) の戻り先 ID (0 から)</summary>
    public required IReadOnlyDictionary<HuStmt, int> GosubIds { get; init; }

    /// <summary>REPEAT↔UNTIL、WHILE↔WEND の対応 (両方向)</summary>
    public required IReadOnlyDictionary<HuStmt, HuStmt> LoopPairs { get; init; }

    /// <summary>REPEAT・WHILE の出現番号 (ラベル名に使う)</summary>
    public required IReadOnlyDictionary<HuStmt, int> LoopIds { get; init; }

    /// <summary>静的に確保するデータの合計 (バイト)</summary>
    public int StaticDataBytes { get; init; }

    public int LiteralId(string text)
    {
        for (int i = 0; i < Literals.Count; i++)
            if (Literals[i] == text) return i;
        throw new KeyNotFoundException(text);
    }
}
