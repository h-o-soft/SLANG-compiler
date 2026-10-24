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

    /// <summary>
    /// 頭文字ごとの型の種類 (DEFINT などで決めたもの)。ここにない頭文字は <see cref="DefaultKind"/>。
    /// 接尾辞のない変数の型は、これで決まる (DEFxxx はプログラム全体に効く)。
    /// </summary>
    public required IReadOnlyDictionary<char, HuVarKind> LetterKinds { get; init; }

    /// <summary>接尾辞もなく DEFxxx もない頭文字の型の種類 (Hu-BASIC の既定は単精度。--assume-defint なら整数)</summary>
    public HuVarKind DefaultKind { get; init; } = HuVarKind.Single;

    /// <summary>整数の単純変数 (変数の鍵: "A%")</summary>
    public required IReadOnlyList<string> IntVars { get; init; }

    /// <summary>浮動小数の単純変数 (変数の鍵: 単精度 "A!"、倍精度 "A#")</summary>
    public required IReadOnlyList<string> FloatVars { get; init; }

    /// <summary>文字列の単純変数 (変数の鍵: "A$")</summary>
    public required IReadOnlyList<string> StrVars { get; init; }

    /// <summary>配列 (整数・浮動小数・文字列): 変数の鍵 → 記憶域の上限の添字 (DIM の上限の最大値と 10 の大きい方)</summary>
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

    /// <summary>
    /// 変数の鍵: 名前に、型の種類を表す接尾辞 (% ! # $) を必ず付けたもの。接尾辞のない名前は頭文字の型の種類で補う
    /// (DEFINT A の下の A と A% は同じ鍵 "A%"、既定の A と A! は同じ鍵 "A!")。
    /// </summary>
    public string Key(string name) => KeyOf(name, LetterKinds, DefaultKind);

    public static string KeyOf(string name, IReadOnlyDictionary<char, HuVarKind> letterKinds, HuVarKind defaultKind)
    {
        if (name.Length > 0 && "%!#$".Contains(name[^1])) return name;
        var kind = name.Length > 0 && letterKinds.TryGetValue(name[0], out var k) ? k : defaultKind;
        return name + Suffix(kind);
    }

    public static char Suffix(HuVarKind kind) => kind switch
    {
        HuVarKind.Int => '%',
        HuVarKind.Single => '!',
        HuVarKind.Double => '#',
        _ => '$',
    };

    /// <summary>鍵 (接尾辞付きの名前) の型の種類</summary>
    public static HuVarKind KindOfKey(string key) => key[^1] switch
    {
        '%' => HuVarKind.Int,
        '!' => HuVarKind.Single,
        '#' => HuVarKind.Double,
        _ => HuVarKind.Str,
    };

    /// <summary>構文木の変数名 (ソースの綴り) の型</summary>
    public HuType VarType(string name) => TypeOfKind(KindOfKey(Key(name)));

    public static HuType TypeOfKind(HuVarKind kind) => kind switch
    {
        HuVarKind.Int => HuType.Int,
        HuVarKind.Str => HuType.Str,
        _ => HuType.Float,
    };

    public int LiteralId(string text)
    {
        for (int i = 0; i < Literals.Count; i++)
            if (Literals[i] == text) return i;
        throw new KeyNotFoundException(text);
    }
}
