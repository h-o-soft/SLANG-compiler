using SLANGCompiler.HuBasic.Parsing;

namespace SLANGCompiler.HuBasic.Analysis;

/// <summary>
/// 構文木を検査して <see cref="HuProgram"/> を作る。
/// - 行の並べ替え・重複・飛び先の検査
/// - 型 (整数 / 文字列 / 浮動小数) の決定と検査 (浮動小数の変数は段階 F2 までエラー)、'/' の型の決定
/// - REPEAT/UNTIL・WHILE/WEND の字面の対応、FOR が回らないときの飛び先の走査
/// - 配列・文字列リテラル・DATA の表、GOSUB の戻り先 ID、静的データの合計
/// </summary>
public sealed class HuAnalyzer
{
    public const int DefaultDataBudget = 24 * 1024;
    public const int MaxArrayBytes = 16 * 1024;
    public const int MaxArrayIndex = MaxArrayBytes / 2 - 1; // 8191
    public const int MaxDataBytes = 32767;
    public const int MaxNameLength = 28; // SLANG の名前の上限 32 から接頭辞の分を引く

    private readonly HuDiagnosticBag _diag;
    private readonly int _dataBudget;

    private readonly HashSet<char> _intLetters = new();
    private readonly SortedSet<string> _intVars = new(StringComparer.Ordinal);
    private readonly SortedSet<string> _strVars = new(StringComparer.Ordinal);
    private readonly Dictionary<string, int> _arrays = new(StringComparer.Ordinal);
    private readonly List<string> _literals = new();
    private readonly Dictionary<HuFor, int> _forIds = new(ReferenceEqualityComparer.Instance);
    private readonly Dictionary<HuFor, (HuNext Next, int Part)> _forExits = new(ReferenceEqualityComparer.Instance);
    private readonly Dictionary<HuStmt, int> _gosubIds = new(ReferenceEqualityComparer.Instance);
    private readonly Dictionary<HuStmt, HuStmt> _loopPairs = new(ReferenceEqualityComparer.Instance);
    private readonly Dictionary<HuStmt, int> _loopIds = new(ReferenceEqualityComparer.Instance);
    private HashSet<int> _lineNumbers = new();

    private HuAnalyzer(HuDiagnosticBag diag, int dataBudget)
    {
        _diag = diag;
        _dataBudget = dataBudget;
    }

    public static HuProgram Analyze(HuAstProgram ast, HuDiagnosticBag diag, int dataBudget = DefaultDataBudget)
        => new HuAnalyzer(diag, dataBudget).Run(ast);

    private HuProgram Run(HuAstProgram ast)
    {
        var lines = SortLines(ast.Lines);
        _lineNumbers = lines.Select(l => l.Number).ToHashSet();

        // DEFINT は位置に関係なくプログラム全体に効くものとして先に集める
        foreach (var s in AllStatements(lines))
            if (s is HuDefint d)
                foreach (var (from, to) in d.Ranges)
                    for (char c = from; c <= to; c++) _intLetters.Add(c);

        CollectDims(lines);
        foreach (var line in lines)
            foreach (var s in line.Statements)
                CheckStatement(s);

        MatchLoops(lines);
        var (dataBytes, dataLines) = BuildData(lines);

        var program = new HuProgram
        {
            Lines = lines,
            IntVars = _intVars.ToList(),
            StrVars = _strVars.ToList(),
            Arrays = new Dictionary<string, int>(_arrays),
            Literals = _literals,
            DataBytes = dataBytes,
            DataLines = dataLines,
            ForIds = _forIds,
            ForExits = _forExits,
            GosubIds = _gosubIds,
            LoopPairs = _loopPairs,
            LoopIds = _loopIds,
            StaticDataBytes = ComputeStaticBytes(dataBytes.Length, dataLines.Count),
        };
        if (program.StaticDataBytes > _dataBudget)
        {
            _diag.Error("HB0309",
                $"静的なデータの合計 {program.StaticDataBytes} バイトが上限 {_dataBudget} バイトを超えています " +
                $"(整数変数 {_intVars.Count}、配列 {_arrays.Count}、文字列変数 {_strVars.Count}、" +
                $"文字列リテラル {_literals.Count}、DATA {dataBytes.Length} バイト)",
                HuPos.None);
        }
        return program;
    }

    // ------------------------------------------------------------ 行

    private List<HuLine> SortLines(IReadOnlyList<HuLine> lines)
    {
        for (int i = 1; i < lines.Count; i++)
        {
            if (lines[i].Number < lines[i - 1].Number)
            {
                _diag.Warning("HB0501", "行番号が昇順に並んでいないので並べ替えます", lines[i].Pos);
                break;
            }
        }
        var seen = new Dictionary<int, HuLine>();
        foreach (var l in lines)
        {
            if (seen.ContainsKey(l.Number))
                _diag.Error("HB0502", $"行番号 {l.Number} が 2 回以上あります", l.Pos);
            else
                seen[l.Number] = l;
        }
        return seen.Values.OrderBy(l => l.Number).ToList();
    }

    /// <summary>プログラム順 (IF の THEN 節、ELSE 節の順) にすべての文を並べる</summary>
    public static IEnumerable<HuStmt> AllStatements(IEnumerable<HuLine> lines)
    {
        foreach (var l in lines)
            foreach (var s in Flatten(l.Statements))
                yield return s;
    }

    private static IEnumerable<HuStmt> Flatten(IEnumerable<HuStmt> stmts)
    {
        foreach (var s in stmts)
        {
            yield return s;
            if (s is HuIf f)
            {
                foreach (var t in Flatten(f.Then)) yield return t;
                if (f.Else != null)
                    foreach (var e in Flatten(f.Else)) yield return e;
            }
        }
    }

    private void CheckTarget(int line, HuPos pos)
    {
        if (!_lineNumbers.Contains(line))
            _diag.Error("HB0503", $"飛び先の行 {line} がありません", pos);
    }

    // ------------------------------------------------------------ DIM と配列

    private void CollectDims(List<HuLine> lines)
    {
        foreach (var s in AllStatements(lines))
        {
            if (s is not HuDim dim) continue;
            foreach (var (name, boundExpr) in dim.Arrays)
            {
                if (name.EndsWith('$'))
                {
                    _diag.Error("HB0306", $"文字列の配列 {name} は未対応です", dim.Pos);
                    continue;
                }
                CheckVarName(name, dim.Pos);
                if (ConstExprOnly(boundExpr) == false)
                {
                    _diag.Error("HB0304", $"DIM {name} の上限は定数で書いてください (変数を含む上限は未対応)", dim.Pos);
                    continue;
                }
                var bound = ConstValue(boundExpr);
                // 上限の添字そのものを直接比べる (掛け算で大きさを計算して比べるとあふれるため)
                if (bound == null || bound < 0 || bound > MaxArrayIndex)
                {
                    _diag.Error("HB0305", $"DIM {name} の上限が範囲外です (0〜{MaxArrayIndex})", dim.Pos);
                    continue;
                }
                var storage = Math.Max((int)bound.Value, 10);
                _arrays[name] = _arrays.TryGetValue(name, out var prev) ? Math.Max(prev, storage) : storage;
            }
        }
    }

    /// <summary>
    /// 定数だけの整数式なら値 (16 ビットで回り込ませない生の値。64 ビットで計算し、
    /// 途中で ±2^40 を超えたら null として扱う)。それ以外は null。
    /// </summary>
    public static long? ConstValue(HuExpr e)
    {
        const long Limit = 1L << 40;
        long? v = e switch
        {
            HuNum n => n.Value,
            // 浮動小数の定数は四捨五入した値 (DIM の上限などで、32768 以上の 10 進の数も範囲の検査に乗せる)
            HuFloat f when Math.Abs(f.Value) <= Limit => (long)Math.Round(f.Value, MidpointRounding.AwayFromZero),
            HuUnary { Op: "-" } u => ConstValue(u.Operand) is long x ? -x : null,
            HuUnary { Op: "+" } u => ConstValue(u.Operand),
            HuBinary { Op: "+" } b => ConstValue(b.Left) is long l && ConstValue(b.Right) is long r ? l + r : null,
            HuBinary { Op: "-" } b => ConstValue(b.Left) is long l && ConstValue(b.Right) is long r ? l - r : null,
            HuBinary { Op: "*" } b => ConstValue(b.Left) is long l && ConstValue(b.Right) is long r ? MulOrNull(l, r) : null,
            _ => null,
        };
        return v is long w && (w > Limit || w < -Limit) ? null : v;

        static long? MulOrNull(long l, long r)
            => l != 0 && Math.Abs(r) > Limit / Math.Abs(l) ? null : l * r;
    }

    /// <summary>数値と + - * と単項 - + だけでできた式か (値の大きさは問わない)</summary>
    private static bool ConstExprOnly(HuExpr e) => e switch
    {
        HuNum => true,
        HuFloat => true,
        HuUnary { Op: "-" or "+" } u => ConstExprOnly(u.Operand),
        HuBinary { Op: "+" or "-" or "*" } b => ConstExprOnly(b.Left) && ConstExprOnly(b.Right),
        _ => false,
    };

    private void UseArray(string name, HuPos pos)
    {
        if (name.EndsWith('$'))
        {
            _diag.Error("HB0306", $"文字列の配列 {name} は未対応です", pos);
            return;
        }
        CheckVarName(name, pos);
        if (!_arrays.ContainsKey(name)) _arrays[name] = 10; // DIM なし → 上限 10
    }

    // ------------------------------------------------------------ 変数と型

    private void CheckVarName(string name, HuPos pos)
    {
        var bare = name.TrimEnd('$', '%', '!', '#');
        if (bare.Length > MaxNameLength)
            _diag.Error("HB0310", $"変数名 {name} が長すぎます ({MaxNameLength} 文字まで)", pos);
        if (name.EndsWith('!') || name.EndsWith('#'))
            _diag.Error("HB0403", $"浮動小数の変数 {name} は未対応です (整数と文字列のみ)", pos);
        else if (!name.EndsWith('$') && !name.EndsWith('%') && !_intLetters.Contains(name[0]))
            _diag.Error("HB0403",
                $"変数 {name} は浮動小数になります (DEFINT で整数にするか、% を付けてください)", pos);
    }

    private static HuType VarType(string name) => name.EndsWith('$') ? HuType.Str : HuType.Int;

    private void UseVar(string name, HuPos pos)
    {
        CheckVarName(name, pos);
        if (name.EndsWith('$')) _strVars.Add(name);
        else _intVars.Add(name);
    }

    private void AddLiteral(string text)
    {
        if (!_literals.Contains(text)) _literals.Add(text);
    }

    /// <summary>
    /// 式を検査して型を付ける。intConv は「この式の値がその場で整数に変換される位置」か
    /// ('/' はこの位置の最上位でだけ許す)。
    /// </summary>
    private HuType CheckExpr(HuExpr e, bool intConv)
    {
        HuType t;
        switch (e)
        {
            case HuNum:
                t = HuType.Int;
                break;
            case HuFloat:
                t = HuType.Float;
                break;
            case HuStr s:
                AddLiteral(s.Text);
                t = HuType.Str;
                break;
            case HuVar v:
                UseVar(v.Name, v.Pos);
                t = VarType(v.Name);
                break;
            case HuIndex ix:
                UseArray(ix.Name, ix.Pos);
                ExpectNum(ix.Index, intConv: true);
                t = HuType.Int;
                break;
            case HuUnary u:
            {
                var ot = ExpectNum(u.Operand, intConv: u.Op == "NOT");
                // 符号は中身の型のまま、NOT は整数 (浮動小数は丸めてからビットを反転する)
                t = u.Op == "NOT" ? HuType.Int : ot;
                break;
            }
            case HuBinary b:
                t = CheckBinary(b, intConv);
                break;
            case HuCall c:
                t = CheckCall(c);
                break;
            default:
                t = HuType.Int;
                break;
        }
        e.Type = t;
        return t;
    }

    /// <summary>
    /// 数の式 (整数か浮動小数) を求める。浮動小数は、整数が要る位置では出力のときに四捨五入で整数にする。
    /// 返り値は式の型 (文字列ならエラーにして Int を返す)。
    /// </summary>
    private HuType ExpectNum(HuExpr e, bool intConv)
    {
        var t = CheckExpr(e, intConv);
        if (t == HuType.Str)
        {
            _diag.Error("HB0404", "数の式が必要です (文字列が書かれています)", e.Pos);
            return HuType.Int;
        }
        return t;
    }

    private static bool IsNum(HuType t) => t != HuType.Str;

    /// <summary>数の演算の結果の型: どちらかが浮動小数なら浮動小数</summary>
    private static HuType NumResult(HuType l, HuType r) => l == HuType.Float || r == HuType.Float ? HuType.Float : HuType.Int;

    private void ExpectStr(HuExpr e)
    {
        if (CheckExpr(e, false) != HuType.Str)
            _diag.Error("HB0404", "文字列の式が必要です (整数が書かれています)", e.Pos);
    }

    private HuType CheckBinary(HuBinary b, bool intConv)
    {
        switch (b.Op)
        {
            case "+":
            {
                var lt = CheckExpr(b.Left, false);
                var rt = CheckExpr(b.Right, false);
                if (IsNum(lt) != IsNum(rt))
                {
                    _diag.Error("HB0404", "文字列と数を + でつなげません", b.Pos);
                    return lt;
                }
                return lt == HuType.Str ? HuType.Str : NumResult(lt, rt);
            }
            case "=":
            case "<>":
            case "<":
            case ">":
            case "<=":
            case ">=":
            {
                var lt = CheckExpr(b.Left, false);
                var rt = CheckExpr(b.Right, false);
                if (IsNum(lt) != IsNum(rt))
                    _diag.Error("HB0404", "文字列と数は比べられません", b.Pos);
                return HuType.Int;
            }
            case "AND":
            case "OR":
            case "XOR":
                ExpectNum(b.Left, intConv: true);
                ExpectNum(b.Right, intConv: true);
                return HuType.Int;
            case "/":
            {
                // Hu-BASIC の / は浮動小数の割り算。ただし左右とも整数で、結果がその場で整数に丸められる
                // 位置なら、整数の割り算を四捨五入する BAS_IDIVR で同じ値になる (型を Int にして目印にする)
                var lt = ExpectNum(b.Left, intConv: false);
                var rt = ExpectNum(b.Right, intConv: false);
                return intConv && lt == HuType.Int && rt == HuType.Int ? HuType.Int : HuType.Float;
            }
            case "-":
            case "*":
                return NumResult(ExpectNum(b.Left, intConv: false), ExpectNum(b.Right, intConv: false));
            default: // \ MOD (左右は整数に丸めてから計算する)
                ExpectNum(b.Left, intConv: true);
                ExpectNum(b.Right, intConv: true);
                return HuType.Int;
        }
    }

    private HuType CheckCall(HuCall c)
    {
        var a = c.Args;
        switch (c.Name)
        {
            case "INP":
            case "STICK":
            case "STRIG":
                ExpectNum(a[0], intConv: true);
                return HuType.Int;
            case "ASC":
            case "VAL":
                ExpectStr(a[0]);
                return HuType.Int;
            case "CHR$":
            case "CGPAT$":
                ExpectNum(a[0], intConv: true);
                return HuType.Str;
            case "HEXCHR$":
                ExpectStr(a[0]);
                return HuType.Str;
            case "MID$":
            case "RIGHT$":
                ExpectStr(a[0]);
                for (int i = 1; i < a.Count; i++) ExpectNum(a[i], intConv: true);
                return HuType.Str;
            case "INKEY$":
                return HuType.Str;
            case "RND":
                // 引数の意味は記録にないので、読んで捨てる
                if (a.Count > 0) ExpectNum(a[0], intConv: false);
                return HuType.Float;
            case "INT":
                ExpectNum(a[0], intConv: false);
                return HuType.Float;
            default:
                return HuType.Int;
        }
    }

    // ------------------------------------------------------------ 文

    private int _gosubCount;
    private int _forCount;

    private void CheckStatement(HuStmt s)
    {
        switch (s)
        {
            case HuAssign asg:
            {
                var tt = CheckExpr(asg.Target, false);
                var vt = CheckExpr(asg.Value, intConv: tt == HuType.Int);
                if (IsNum(tt) != IsNum(vt))
                    _diag.Error("HB0404", "代入の左右の型が違います", asg.Pos);
                break;
            }
            case HuGoto g:
                CheckTarget(g.Line, g.Pos);
                break;
            case HuGosub g:
                CheckTarget(g.Line, g.Pos);
                _gosubIds[g] = _gosubCount++;
                break;
            case HuReturn r:
                if (r.Line is int rl) CheckTarget(rl, r.Pos);
                break;
            case HuOnJump on:
                ExpectNum(on.Selector, intConv: false);
                foreach (var l in on.Lines) CheckTarget(l, on.Pos);
                if (on.Kind == "GOSUB") _gosubIds[on] = _gosubCount++;
                break;
            case HuIf f:
                ExpectNum(f.Cond, intConv: false);
                foreach (var t in f.Then) CheckStatement(t);
                if (f.Else != null)
                    foreach (var e in f.Else) CheckStatement(e);
                break;
            case HuFor fr:
                UseVar(fr.Var, fr.Pos);
                if (VarType(fr.Var) != HuType.Int)
                    _diag.Error("HB0404", "FOR の変数は整数にしてください", fr.Pos);
                ExpectNum(fr.From, intConv: true);
                ExpectNum(fr.To, intConv: false);
                if (fr.Step != null) ExpectNum(fr.Step, intConv: false);
                _forIds[fr] = _forCount++;
                break;
            case HuNext nx:
                foreach (var v in nx.Vars)
                {
                    UseVar(v, nx.Pos);
                    if (VarType(v) != HuType.Int)
                        _diag.Error("HB0404", $"NEXT の変数 {v} は整数にしてください", nx.Pos);
                }
                break;
            case HuUntil u:
                ExpectNum(u.Cond, intConv: false);
                break;
            case HuWhile w:
                ExpectNum(w.Cond, intConv: false);
                break;
            case HuRead rd:
                foreach (var t in rd.Targets) CheckExpr(t, false);
                break;
            case HuRestore rs:
                if (rs.Line is int line) CheckTarget(line, rs.Pos);
                break;
            case HuOut o:
                ExpectNum(o.Port, intConv: true);
                ExpectNum(o.Value, intConv: true);
                break;
            case HuPrint p:
                if (p.Using != null) ExpectStr(p.Using);
                foreach (var item in p.Items)
                {
                    if (item is HuPrintExpr pe)
                    {
                        var t = CheckExpr(pe.Expr, false);
                        if (p.Using != null && t == HuType.Str)
                            _diag.Error("HB0406", "PRINT USING は数値の書式だけに対応しています", pe.Expr.Pos);
                        else if (p.Using != null && t == HuType.Float)
                            _diag.Error("HB0407", "PRINT USING の浮動小数は未対応です (段階 F2 で対応)", pe.Expr.Pos);
                    }
                    else if (item is HuPrintSpc sp)
                        ExpectNum(sp.Count, intConv: true);
                }
                break;
            case HuCommand cmd:
                CheckCommand(cmd);
                break;
            case HuLineText lt:
                ExpectNum(lt.X1, true);
                ExpectNum(lt.Y1, true);
                ExpectNum(lt.X2, true);
                ExpectNum(lt.Y2, true);
                ExpectStr(lt.Char);
                break;
            case HuDefChr dc:
                ExpectNum(dc.Code, true);
                ExpectStr(dc.Pattern);
                break;
            case HuDim:
            case HuDefint:
            case HuData:
            case HuRem:
            case HuRepeat:
            case HuWend:
            case HuEnd:
            case HuInit:
                break;
        }
    }

    private void CheckCommand(HuCommand cmd)
    {
        if (cmd.Name == "PLAY")
        {
            ExpectStr(cmd.Args[0]!);
            return;
        }
        foreach (var a in cmd.Args)
            if (a != null) ExpectNum(a, intConv: true);
        if (cmd.Name == "WIDTH" && cmd.Args[0] is HuNum w && w.Value != 40 && w.Value != 80)
            _diag.Error("HB0311", "WIDTH は 40 か 80 です", cmd.Pos);
    }

    // ------------------------------------------------------------ ループの対応

    private void MatchLoops(List<HuLine> lines)
    {
        var all = AllStatements(lines).ToList();
        var stack = new Stack<HuStmt>();
        int loopId = 0;
        foreach (var s in all)
        {
            switch (s)
            {
                case HuRepeat:
                case HuWhile:
                    _loopIds[s] = loopId++;
                    stack.Push(s);
                    break;
                case HuUntil u:
                    if (stack.Count > 0 && stack.Peek() is HuRepeat rp)
                    {
                        stack.Pop();
                        _loopPairs[rp] = u;
                        _loopPairs[u] = rp;
                    }
                    else
                        _diag.Error("HB0602", "UNTIL に対応する REPEAT がありません", u.Pos);
                    break;
                case HuWend w:
                    if (stack.Count > 0 && stack.Peek() is HuWhile wh)
                    {
                        stack.Pop();
                        _loopPairs[wh] = w;
                        _loopPairs[w] = wh;
                    }
                    else
                        _diag.Error("HB0602", "WEND に対応する WHILE がありません", w.Pos);
                    break;
            }
        }
        foreach (var open in stack)
            _diag.Error("HB0603", open is HuRepeat ? "REPEAT が UNTIL で閉じられていません" : "WHILE が WEND で閉じられていません", open.Pos);

        // FOR が 1 回も回らないときの飛び先: 後ろを字面の順に走査し、間の FOR と NEXT の入れ子を
        // 数えながら「同じ変数を名指しする NEXT の部分」か「同じ深さの変数なしの NEXT」を探す
        for (int i = 0; i < all.Count; i++)
        {
            if (all[i] is not HuFor fr) continue;
            int depth = 0;
            bool found = false;
            for (int j = i + 1; j < all.Count && !found; j++)
            {
                if (all[j] is HuFor) { depth++; continue; }
                if (all[j] is not HuNext nx) continue;
                int parts = Math.Max(1, nx.Vars.Count);
                for (int k = 0; k < parts; k++)
                {
                    bool named = nx.Vars.Count > 0;
                    // 同じ変数を名指しする NEXT は深さに関係なく一致 (Hu-BASIC はその NEXT で
                    // 内側の FOR 枠を捨てる)。それ以外は内側のループを閉じたものとして深さを減らし、
                    // 深さ 0 の変数なしの NEXT は一致
                    if ((named && nx.Vars[k] == fr.Var) || (!named && depth == 0))
                    {
                        _forExits[fr] = (nx, k);
                        found = true;
                        break;
                    }
                    if (depth > 0) depth--;
                }
            }
            if (!found)
                _diag.Error("HB0601", $"FOR {fr.Var} に対応する NEXT が見つかりません", fr.Pos);
        }
    }

    // ------------------------------------------------------------ DATA

    private (byte[], List<(int, int)>) BuildData(List<HuLine> lines)
    {
        var bytes = new List<byte>();
        var dataLines = new List<(int, int)>();
        foreach (var line in lines)
        {
            bool first = true;
            foreach (var s in Flatten(line.Statements))
            {
                if (s is not HuData d) continue;
                if (first)
                {
                    dataLines.Add((line.Number, bytes.Count));
                    first = false;
                }
                foreach (var item in d.Items)
                {
                    var len = Math.Min(item.Length, 255);
                    bytes.Add((byte)len);
                    for (int i = 0; i < len; i++) bytes.Add((byte)item[i]);
                }
            }
        }
        if (bytes.Count > MaxDataBytes)
            _diag.Error("HB0107", $"DATA の合計 {bytes.Count} バイトが {MaxDataBytes} バイトを超えています", HuPos.None);
        return (bytes.ToArray(), dataLines);
    }

    private int ComputeStaticBytes(int dataBytes, int dataLineCount)
    {
        long total = _intVars.Count * 2L + _strVars.Count * 256L;
        foreach (var bound in _arrays.Values) total += (bound + 1L) * 2 + 4;
        foreach (var lit in _literals) total += lit.Length + 1L;
        total += dataBytes + (dataLineCount + 1L) * 4;
        return (int)Math.Min(total, int.MaxValue);
    }
}
