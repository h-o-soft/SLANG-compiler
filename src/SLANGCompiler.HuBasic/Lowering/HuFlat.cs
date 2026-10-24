using SLANGCompiler.HuBasic.Parsing;

namespace SLANGCompiler.HuBasic.Lowering;

// ============================================================================
// 制御を平らに展開した列。ラベル・飛び先・代入・ランタイム呼び出しだけで、
// 出力の形 (SLANG など) には依存しない。式は解析済みの構文木 (HuExpr) のまま持つ。
// ============================================================================

public abstract record FlatOp;

/// <summary>ラベル</summary>
public sealed record FLabel(string Name) : FlatOp;

/// <summary>無条件の飛び先</summary>
public sealed record FGoto(string Label) : FlatOp;

/// <summary>整数の条件 Cond が JumpIfTrue のとき Label へ飛ぶ (Cond は 0 以外が真)</summary>
public sealed record FIf(HuExpr Cond, bool JumpIfTrue, string Label) : FlatOp;

/// <summary>隠し変数 Hidden と定数の符号付き比較 (Op: "==" "<" ">" ">=") が成り立てば Label へ</summary>
public sealed record FIfHidden(string Hidden, string Op, int Value, string Label) : FlatOp;

/// <summary>整数の変数・配列要素への代入</summary>
public sealed record FAssign(HuExpr Target, HuExpr Value) : FlatOp;

/// <summary>隠し変数への代入</summary>
public sealed record FAssignHidden(string Hidden, HuExpr Value) : FlatOp;

/// <summary>ランタイム呼び出し。Result が null でなければ戻り値をその隠し変数に入れる</summary>
public sealed record FCall(string Function, IReadOnlyList<FArg> Args, string? Result = null) : FlatOp;

/// <summary>隠し変数の値で分岐する (一致しなければ次へ)</summary>
public sealed record FDispatch(string Hidden, IReadOnlyList<(int Value, string Label)> Cases) : FlatOp;

/// <summary>I/O ポートへの 1 バイトの書き込み</summary>
public sealed record FPortOut(HuExpr Port, HuExpr Value) : FlatOp;

/// <summary>POKE・POKE@ の 1 バイト: 隠し変数 PK の番地 (ポート) + Offset に Value を書く (Io が真なら I/O 空間)</summary>
public sealed record FPoke(int Offset, HuExpr Value, bool Io) : FlatOp;

/// <summary>SWAP a, b (A・B は HuVar / HuIndex。添字は A・B の順に調べる)</summary>
public sealed record FSwap(HuExpr A, HuExpr B) : FlatOp;

/// <summary>元の行の目印 (出力でコメントにする)</summary>
public sealed record FLineMark(int Line, string Source) : FlatOp;

/// <summary>ランタイム呼び出しの引数</summary>
public abstract record FArg;
public sealed record FExprArg(HuExpr Expr) : FArg;          // 式の値 (文字列なら文字列のポインタ、浮動小数は整数に丸める)
public sealed record FFloatArg(HuExpr Expr) : FArg;         // 浮動小数のまま渡す値 (整数の式なら浮動小数にする)
public sealed record FIntArg(int Value) : FArg;             // 定数
public sealed record FAddrArg(HuExpr Var) : FArg;           // 数の単純変数のアドレス
public sealed record FFloatAddrArg(HuExpr Expr) : FArg;     // 浮動小数の値の番地 (式なら一時変数に入れて、その番地)
public sealed record FHiddenAddrArg(string Hidden) : FArg;  // 隠し変数のアドレス
public sealed record FDataTableArg(bool LineTable) : FArg;  // DATA の本体 (false) / 行の表 (true) の先頭

/// <summary>DATA を読むための疑似関数 (READ の展開で使う)</summary>
public static class FlatPseudo
{
    public const string ReadNum = "#READN";     // DATA の次の項目を数として (浮動小数)
    public const string ReadStr = "#READS";
    public const string InputNum = "#INPUTN";   // INPUT の次の項目を数として (浮動小数)
    public const string InputStr = "#INPUTS";   // INPUT の次の項目を文字列として
}

public sealed record HuFlat(IReadOnlyList<FlatOp> Ops, IReadOnlyList<string> HiddenVars);
