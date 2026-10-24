namespace SLANGCompiler.HuBasic.Lexing;

public enum HuTokenKind
{
    Number,      // 整数 (10 進 / &H / &O / &B)。Value に値 (0-65535)
    Float,       // 浮動小数の数値 (小数点・指数・! # を含むもの、32768 以上の 10 進)。Text に綴り
    String,      // "..." (Text は中身、引用符なし)
    Ident,       // 変数名 (大文字化済み、型の接尾辞 $ % を含む)
    Keyword,     // 予約語 (Text は正規化した綴り)
    Symbol,      // 演算子・区切り (Text は記号)
    Data,        // DATA の生の項目列 (DataItems)
    Comment,     // ' または REM 以降 (Text は行末までの本文)
    End,         // 行末
}

public sealed record HuToken(HuTokenKind Kind, string Text, int Value, int Column)
{
    public IReadOnlyList<string> DataItems { get; init; } = Array.Empty<string>();

    public bool IsKeyword(string kw) => Kind == HuTokenKind.Keyword && Text == kw;
    public bool IsSymbol(string s) => Kind == HuTokenKind.Symbol && Text == s;

    public override string ToString() => Kind switch
    {
        HuTokenKind.Number => Value.ToString(),
        HuTokenKind.String => $"\"{Text}\"",
        HuTokenKind.End => "<行末>",
        _ => Text,
    };
}
