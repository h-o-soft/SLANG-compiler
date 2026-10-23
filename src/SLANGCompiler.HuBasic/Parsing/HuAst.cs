namespace SLANGCompiler.HuBasic.Parsing;

// ============================================================================
// Hu-BASIC の構文木。出力の形 (SLANG など) には依存しない。
// ============================================================================

/// <summary>式の型。Float は SLANG の FLOAT (24 ビットの浮動小数) で持つ</summary>
public enum HuType { Int, Str, Float }

/// <summary>変数の型の種類 (変数を見分ける鍵の一部)。Single と Double はどちらも FLOAT で持つ</summary>
public enum HuVarKind { Int, Single, Double, Str }

// ---------------------------------------------------------------- 式

public abstract record HuExpr(HuPos Pos)
{
    /// <summary>解析で決まる型 (構文解析の直後は未設定)</summary>
    public HuType Type { get; set; } = HuType.Int;
}

public sealed record HuNum(int Value, HuPos Pos) : HuExpr(Pos);

/// <summary>浮動小数の定数 (小数点・指数・! # を含む数、または 32768 以上の 10 進の数)</summary>
public sealed record HuFloat(double Value, HuPos Pos) : HuExpr(Pos);

/// <summary>文字列リテラル。Text は X1 文字コードの string。</summary>
public sealed record HuStr(string Text, HuPos Pos) : HuExpr(Pos);

/// <summary>単純変数 (Name は接尾辞込み: A, A%, A$)</summary>
public sealed record HuVar(string Name, HuPos Pos) : HuExpr(Pos);

/// <summary>配列の要素 (1 次元)</summary>
public sealed record HuIndex(string Name, HuExpr Index, HuPos Pos) : HuExpr(Pos);

public sealed record HuUnary(string Op, HuExpr Operand, HuPos Pos) : HuExpr(Pos);   // "-", "+", "NOT"

public sealed record HuBinary(string Op, HuExpr Left, HuExpr Right, HuPos Pos) : HuExpr(Pos);
// Op: "+","-","*","/","\\","MOD","=","<>","<",">","<=",">=","AND","OR","XOR"

/// <summary>組み込み関数 (INP, MID$, LEFT$, RIGHT$, CHR$, ASC, VAL, HEXCHR$, CGPAT$, INKEY$, STICK, STRIG, RND, INT,
/// STRING$, CHARACTER$, INPUT$, STR$)</summary>
public sealed record HuCall(string Name, IReadOnlyList<HuExpr> Args, HuPos Pos) : HuExpr(Pos);

// ---------------------------------------------------------------- 文

public abstract record HuStmt(HuPos Pos);

public sealed record HuAssign(HuExpr Target, HuExpr Value, HuPos Pos) : HuStmt(Pos);   // Target は HuVar / HuIndex
public sealed record HuGoto(int Line, HuPos Pos) : HuStmt(Pos);
public sealed record HuGosub(int Line, HuPos Pos) : HuStmt(Pos);
public sealed record HuReturn(int? Line, HuPos Pos) : HuStmt(Pos);
public sealed record HuOnJump(HuExpr Selector, string Kind, IReadOnlyList<int> Lines, HuPos Pos) : HuStmt(Pos); // Kind: GOTO/GOSUB/RESTORE
public sealed record HuIf(HuExpr Cond, IReadOnlyList<HuStmt> Then, IReadOnlyList<HuStmt>? Else, HuPos Pos) : HuStmt(Pos);
public sealed record HuFor(string Var, HuExpr From, HuExpr To, HuExpr? Step, HuPos Pos) : HuStmt(Pos);
public sealed record HuNext(IReadOnlyList<string> Vars, HuPos Pos) : HuStmt(Pos);    // 空 = 変数の指定なし
public sealed record HuRepeat(HuPos Pos) : HuStmt(Pos);
public sealed record HuUntil(HuExpr Cond, HuPos Pos) : HuStmt(Pos);
public sealed record HuWhile(HuExpr Cond, HuPos Pos) : HuStmt(Pos);
public sealed record HuWend(HuPos Pos) : HuStmt(Pos);
public sealed record HuEnd(HuPos Pos) : HuStmt(Pos);
public sealed record HuInit(HuPos Pos) : HuStmt(Pos);
public sealed record HuDim(IReadOnlyList<(string Name, HuExpr Bound)> Arrays, HuPos Pos) : HuStmt(Pos);
/// <summary>DEFINT / DEFSNG / DEFDBL / DEFSTR (Kind はその型の種類)</summary>
public sealed record HuDefType(HuVarKind Kind, IReadOnlyList<(char From, char To)> Ranges, HuPos Pos) : HuStmt(Pos);
public sealed record HuData(IReadOnlyList<string> Items, HuPos Pos) : HuStmt(Pos);
public sealed record HuRead(IReadOnlyList<HuExpr> Targets, HuPos Pos) : HuStmt(Pos);
public sealed record HuRestore(int? Line, HuPos Pos) : HuStmt(Pos);
public sealed record HuOut(HuExpr Port, HuExpr Value, HuPos Pos) : HuStmt(Pos);

/// <summary>PRINT の項目: 式 / 区切り (";" ",") / SPC(n)</summary>
public abstract record HuPrintItem;
public sealed record HuPrintExpr(HuExpr Expr) : HuPrintItem;
public sealed record HuPrintSep(char Sep) : HuPrintItem;
public sealed record HuPrintSpc(HuExpr Count) : HuPrintItem;

public sealed record HuPrint(int? Channel, HuExpr? Using, IReadOnlyList<HuPrintItem> Items, HuPos Pos) : HuStmt(Pos);

/// <summary>引数を取る画面・音などの命令 (LOCATE, COLOR, CLS, WIDTH, CFLASH, CSIZE, CGEN,
/// PLAY, TEMPO, BEEP, CLICK, PAUSE)。省略された引数は null。</summary>
public sealed record HuCommand(string Name, IReadOnlyList<HuExpr?> Args, HuPos Pos) : HuStmt(Pos);

/// <summary>テキスト版 LINE (x1,y1)-(x2,y2),"c"[,B|BF]。Fill は BF (箱の中も塗る)</summary>
public sealed record HuLineText(HuExpr X1, HuExpr Y1, HuExpr X2, HuExpr Y2, HuExpr Char, bool Box, HuPos Pos) : HuStmt(Pos)
{
    public bool Fill { get; init; }
}

/// <summary>INPUT ["案内" ; または ,] 変数, … (Question は案内の後に「? 」を出すか。案内なしなら出す)</summary>
public sealed record HuInput(string? Prompt, bool Question, IReadOnlyList<HuExpr> Targets, HuPos Pos) : HuStmt(Pos);

/// <summary>MID$ の文: MID$(変数, 位置[, 長さ]) = 文字列 (変数の一部を置き換える。長さは変えない)</summary>
public sealed record HuMidSet(string Var, HuExpr Start, HuExpr? Length, HuExpr Value, HuPos Pos) : HuStmt(Pos);

public sealed record HuDefChr(HuExpr Code, HuExpr Pattern, HuPos Pos) : HuStmt(Pos);

/// <summary>コメント。'#VSYNC n の指示コメントは Directive に入る。</summary>
public sealed record HuRem(string? Directive, int DirectiveArg, HuPos Pos) : HuStmt(Pos);

// ---------------------------------------------------------------- 行とプログラム

public sealed record HuLine(int Number, IReadOnlyList<HuStmt> Statements, HuPos Pos, string SourceText);

public sealed record HuAstProgram(IReadOnlyList<HuLine> Lines);
