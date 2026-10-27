using SLANGCompiler.HuBasic.Parsing;

namespace SLANGCompiler.HuBasic.Analysis;

/// <summary>
/// 構文木を検査して <see cref="HuProgram"/> を作る。
/// - 行の並べ替え・重複・飛び先の検査
/// - 変数の鍵 (名前と型の種類) と型 (整数 / 文字列 / 浮動小数) の決定と検査、'/' の型の決定

/// - REPEAT/UNTIL・WHILE/WEND の字面の対応、FOR が回らないときの飛び先の走査
/// - 配列・文字列リテラル・DATA の表、GOSUB の戻り先 ID、静的データの合計
/// </summary>
public sealed class HuAnalyzer
{
    public const int DefaultDataBudget = 24 * 1024;
    public const int MaxArrayBytes = 16 * 1024;
    public const int MaxArrayIndex = MaxArrayBytes / 2 - 1; // 8191
    public const int MaxFloatArrayIndex = MaxArrayBytes / 3 - 1; // 5460 (1 要素 3 バイト)
    public const int MaxStringLen = 255; // 文字列 1 本の長さの上限 (HuBASIC と同じ。--string-len の既定)
    public const int MaxDataBytes = 32767;
    public const int MaxNameLength = 28; // SLANG の名前の上限 32 から接頭辞の分を引く

    private readonly HuDiagnosticBag _diag;
    private readonly int _dataBudget;
    /// <summary>文字列 1 本の長さの上限 (--string-len)。文字列の変数と配列の要素のバッファは これ + 1 バイト</summary>
    private readonly int _stringLen;

    private readonly HuVarKind _defaultKind;
    /// <summary>DEFxxx で決めた頭文字の型の種類と、その頭文字の最初の DEFxxx の行</summary>
    private readonly Dictionary<char, HuVarKind> _letterKinds = new();
    private readonly Dictionary<char, int> _defLines = new();
    /// <summary>DEFxxx より前の行で使った接尾辞のない変数: 頭文字 → (変数名, 位置) (warning をまとめて出す)</summary>
    private readonly SortedDictionary<char, (string Name, HuPos Pos)> _earlyUses = new();
    private readonly HashSet<string> _warnedDouble = new(StringComparer.Ordinal);
    /// <summary>いま検査している行の番号 (DEFxxx より前の行で使ったかの判定に使う)</summary>
    private int _curLine;
    private readonly SortedSet<string> _intVars = new(StringComparer.Ordinal);
    private readonly SortedSet<string> _floatVars = new(StringComparer.Ordinal);
    private readonly SortedSet<string> _strVars = new(StringComparer.Ordinal);
    private readonly Dictionary<string, int> _arrays = new(StringComparer.Ordinal);
    private readonly List<string> _literals = new();
    private readonly Dictionary<HuFor, int> _forIds = new(ReferenceEqualityComparer.Instance);
    private readonly Dictionary<HuFor, (HuNext Next, int Part)> _forExits = new(ReferenceEqualityComparer.Instance);
    private readonly Dictionary<HuStmt, int> _gosubIds = new(ReferenceEqualityComparer.Instance);
    private readonly Dictionary<HuStmt, HuStmt> _loopPairs = new(ReferenceEqualityComparer.Instance);
    private readonly Dictionary<HuStmt, int> _loopIds = new(ReferenceEqualityComparer.Instance);
    private HashSet<int> _lineNumbers = new();
    /// <summary>LABEL の名前 → 番号 (同じ名前は前の方)、LABEL の文 → 番号</summary>
    private readonly Dictionary<string, int> _labelTargets = new(StringComparer.Ordinal);
    private readonly Dictionary<HuLabel, int> _labelIds = new(ReferenceEqualityComparer.Instance);

    private HuAnalyzer(HuDiagnosticBag diag, int dataBudget, bool assumeDefint, int stringLen)
    {
        _diag = diag;
        _dataBudget = dataBudget;
        _stringLen = stringLen;
        _defaultKind = assumeDefint ? HuVarKind.Int : HuVarKind.Single;
    }

    /// <param name="assumeDefint">接尾辞も DEFxxx もない頭文字の既定の型を整数にする (--assume-defint)。
    /// ソースの DEFxxx と接尾辞のほうが優先で、DEFxxx の衝突の検査などには数えない</param>
    /// <param name="stringLen">文字列 1 本の長さの上限 (--string-len。1〜255)</param>
    public static HuProgram Analyze(HuAstProgram ast, HuDiagnosticBag diag, int dataBudget = DefaultDataBudget,
                                    bool assumeDefint = false, int stringLen = MaxStringLen)
        => new HuAnalyzer(diag, dataBudget, assumeDefint, stringLen).Run(ast);

    private HuProgram Run(HuAstProgram ast)
    {
        var lines = SortLines(ast.Lines);
        _lineNumbers = lines.Select(l => l.Number).ToHashSet();
        CollectLabels(lines);

        CollectDefTypes(lines);
        CollectDims(lines);
        foreach (var line in lines)
        {
            _curLine = line.Number;
            foreach (var s in line.Statements)
                CheckStatement(s);
        }
        WarnEarlyUses();

        MatchLoops(lines);
        var (dataBytes, dataLines) = BuildData(lines);

        var program = new HuProgram
        {
            Lines = lines,
            LetterKinds = new Dictionary<char, HuVarKind>(_letterKinds),
            DefaultKind = _defaultKind,
            IntVars = _intVars.ToList(),
            FloatVars = _floatVars.ToList(),
            StrVars = _strVars.ToList(),
            Arrays = new Dictionary<string, int>(_arrays),
            StringLen = _stringLen,
            Literals = _literals,
            DataBytes = dataBytes,
            DataLines = dataLines,
            ForIds = _forIds,
            ForExits = _forExits,
            GosubIds = _gosubIds,
            LabelIds = _labelIds,
            LabelTargets = _labelTargets,
            LoopPairs = _loopPairs,
            LoopIds = _loopIds,
            StaticDataBytes = ComputeStaticBytes(dataBytes.Length, dataLines.Count),
        };
        if (program.StaticDataBytes > _dataBudget)
        {
            _diag.Error("HB0309",
                $"静的なデータの合計 {program.StaticDataBytes} バイトが上限 {_dataBudget} バイトを超えています " +
                $"(整数変数 {_intVars.Count}、浮動小数の変数 {_floatVars.Count}、配列 {_arrays.Count}、文字列変数 {_strVars.Count}、" +
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

    /// <summary>飛び先が名前ならその LABEL があるか、行番号ならその行があるか</summary>
    private void CheckTarget(int line, string? label, HuPos pos)
    {
        if (label == null) CheckTarget(line, pos);
        else if (!_labelTargets.ContainsKey(label))
            _diag.Error("HB0504", $"飛び先の名前 \"{label}\" の LABEL がありません", pos);
    }

    /// <summary>LABEL の表を作る (プログラム順。同じ名前は本物と同じく前の方を使い、後ろの方に warning)</summary>
    private void CollectLabels(List<HuLine> lines)
    {
        foreach (var s in AllStatements(lines))
        {
            if (s is not HuLabel lb) continue;
            var id = _labelIds.Count;
            _labelIds[lb] = id;
            if (_labelTargets.ContainsKey(lb.Name))
                _diag.Warning("HB0505", $"LABEL \"{lb.Name}\" が 2 つ以上あります。前の方に飛びます", lb.Pos);
            else
                _labelTargets[lb.Name] = id;
        }
    }

    // ------------------------------------------------------------ DIM と配列

    private void CollectDims(List<HuLine> lines)
    {
        foreach (var line in lines)
        foreach (var s in Flatten(line.Statements))
        {
            if (s is not HuDim dim) continue;
            _curLine = line.Number;
            foreach (var (name, boundExpr) in dim.Arrays)
            {
                var key = CheckVarName(name, dim.Pos);
                var kind = HuProgram.KindOfKey(key);
                if (ConstExprOnly(boundExpr) == false)
                {
                    _diag.Error("HB0304", $"DIM {name} の上限は定数で書いてください (変数を含む上限は未対応)", dim.Pos);
                    continue;
                }
                if (ContainsFloat(boundExpr) && !IsSignedFloatConst(boundExpr))
                {
                    _diag.Error("HB0313", $"DIM {name} の上限に浮動小数の計算は未対応です (浮動小数の定数 1 つなら書けます)", dim.Pos);
                    continue;
                }
                var bound = ConstValue(boundExpr);
                var max = MaxIndexOf(kind);
                // 上限の添字そのものを直接比べる (掛け算で大きさを計算して比べるとあふれるため)
                if (bound == null || bound < 0 || bound > max)
                {
                    _diag.Error("HB0305", $"DIM {name} の上限が範囲外です (0〜{max})", dim.Pos);
                    continue;
                }
                // 置き場は DIM の上限の分だけ (DIM を通る前に使うと、実行時に上限 min(10, 置き場の上限) で確定する: BAS_AIDX)
                var storage = (int)bound.Value;
                _arrays[key] = _arrays.TryGetValue(key, out var prev) ? Math.Max(prev, storage) : storage;
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
        if (e is HuBinary bin && (ContainsFloat(bin.Left) || ContainsFloat(bin.Right))) return null;
        long? v = e switch
        {
            HuNum n => n.Value,
            // 浮動小数の定数 1 つは、f24 に直した値を四捨五入した値 (DIM の上限などで、32768 以上の 10 進の数も
            // 範囲の検査に乗せる)。浮動小数を含む計算は、途中で丸めると値が変わるので定数として扱わない (下の二項演算)
            HuFloat f when Math.Abs(f.Value) <= Limit => (long)Math.Round(HuF24.Quantize(f.Value), MidpointRounding.AwayFromZero),
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

    /// <summary>数の定数 (整数か浮動小数) 1 つに符号を付けただけの式か</summary>
    private static bool IsSignedNumConst(HuExpr e) => e switch
    {
        HuNum or HuFloat => true,
        HuUnary { Op: "-" or "+" } u => IsSignedNumConst(u.Operand),
        _ => false,
    };

    /// <summary>浮動小数の定数 1 つに符号を付けただけの式か</summary>
    private static bool IsSignedFloatConst(HuExpr e) => e switch
    {
        HuFloat => true,
        HuUnary { Op: "-" or "+" } u => IsSignedFloatConst(u.Operand),
        _ => false,
    };

    /// <summary>浮動小数の定数を含む式か</summary>
    public static bool ContainsFloat(HuExpr e) => e switch
    {
        HuFloat => true,
        HuUnary u => ContainsFloat(u.Operand),
        HuBinary b => ContainsFloat(b.Left) || ContainsFloat(b.Right),
        _ => false,
    };

    /// <summary>数値と + - * と単項 - + だけでできた式か (値の大きさは問わない)</summary>
    private static bool ConstExprOnly(HuExpr e) => e switch
    {
        HuNum => true,
        HuFloat => true,
        HuUnary { Op: "-" or "+" } u => ConstExprOnly(u.Operand),
        HuBinary { Op: "+" or "-" or "*" } b => ConstExprOnly(b.Left) && ConstExprOnly(b.Right),
        _ => false,
    };

    /// <summary>配列の上限の添字 (配列の大きさの上限 16 KiB を、1 要素の大きさで割ったもの)</summary>
    private int MaxIndexOf(HuVarKind kind) => kind switch
    {
        HuVarKind.Int => MaxArrayIndex,
        HuVarKind.Str => MaxArrayBytes / (_stringLen + 1) - 1,
        _ => MaxFloatArrayIndex,
    };

    /// <summary>配列の要素を使う。要素の型を返す</summary>
    private HuType UseArray(string name, HuPos pos)
    {
        var key = CheckVarName(name, pos);
        var kind = HuProgram.KindOfKey(key);
        if (!_arrays.ContainsKey(key)) _arrays[key] = 10; // DIM なし → 上限 10
        return HuProgram.TypeOfKind(kind);
    }

    // ------------------------------------------------------------ 変数と型

    private static string DefKeyword(HuVarKind kind) => kind switch
    {
        HuVarKind.Int => "DEFINT",
        HuVarKind.Single => "DEFSNG",
        HuVarKind.Double => "DEFDBL",
        _ => "DEFSTR",
    };

    /// <summary>
    /// DEFxxx は位置に関係なくプログラム全体に効くものとして先に集める (HuBASIC は実行した時点から効く)。
    /// 同じ頭文字に違う DEFxxx があれば、どちらに決めてよいか分からないのでエラー。IF の中の DEFxxx も同じく数える。
    /// </summary>
    private void CollectDefTypes(List<HuLine> lines)
    {
        foreach (var line in lines)
        foreach (var s in Flatten(line.Statements))
        {
            if (s is not HuDefType d) continue;
            foreach (var (from, to) in d.Ranges)
            for (char c = from; c <= to; c++)
            {
                if (_letterKinds.TryGetValue(c, out var prev) && prev != d.Kind)
                {
                    _diag.Error("HB0410", $"頭文字 {c} に {DefKeyword(prev)} と {DefKeyword(d.Kind)} の両方があります", d.Pos);
                    continue;
                }
                _letterKinds[c] = d.Kind;
                _defLines.TryAdd(c, line.Number);
            }
        }
    }

    private string Key(string name) => HuProgram.KeyOf(name, _letterKinds, _defaultKind);

    /// <summary>変数名を検査して、変数の鍵を返す</summary>
    private string CheckVarName(string name, HuPos pos)
    {
        var bare = name.TrimEnd('$', '%', '!', '#');
        if (bare.Length > MaxNameLength)
            _diag.Error("HB0310", $"変数名 {name} が長すぎます ({MaxNameLength} 文字まで)", pos);
        var key = Key(name);
        // 接尾辞のない変数を、その頭文字の DEFxxx より前の行で使っている (HuBASIC ではそこは既定の型)
        if (key.Length != name.Length && _defLines.TryGetValue(name[0], out var defLine) && _curLine < defLine)
            _earlyUses.TryAdd(name[0], (name, pos));
        if (HuProgram.KindOfKey(key) == HuVarKind.Double && _warnedDouble.Add(key))
            _diag.Warning("HB0412", $"倍精度の変数 {name} は、単精度と同じ浮動小数 (有効数字 約 5 桁) で扱います", pos);
        return key;
    }

    /// <summary>DEFxxx より前の行で使った変数の warning を、DEFxxx の行ごとに 1 つにまとめて出す</summary>
    private void WarnEarlyUses()
    {
        foreach (var group in _earlyUses.GroupBy(u => (Line: _defLines[u.Key], Kind: _letterKinds[u.Key])))
        {
            var names = group.Select(u => u.Value.Name).ToList();
            var list = string.Join(", ", names.Take(8)) + (names.Count > 8 ? $" ほか {names.Count - 8} 個" : "");
            var kw = DefKeyword(group.Key.Kind);
            _diag.Warning("HB0411",
                $"{group.Key.Line} 行の {kw} より前の行で、変数 {list} を使っています " +
                $"(HuBASIC ではその行を実行するまで既定の型です。hubasic2sl はプログラム全体で {kw} の型にします)",
                group.Select(u => u.Value.Pos).OrderBy(p => p.SourceLine).ThenBy(p => p.Column).First());
        }
    }

    private HuType VarType(string name) => HuProgram.TypeOfKind(HuProgram.KindOfKey(Key(name)));

    /// <summary>代入先 (変数か配列の要素) の型の種類</summary>
    private HuVarKind KindOfTarget(HuExpr target) => target switch
    {
        HuVar v => HuProgram.KindOfKey(Key(v.Name)),
        HuIndex ix => HuProgram.KindOfKey(Key(ix.Name)),
        _ => HuVarKind.Int,
    };

    private void UseVar(string name, HuPos pos)
    {
        var key = CheckVarName(name, pos);
        switch (HuProgram.KindOfKey(key))
        {
            case HuVarKind.Str: _strVars.Add(key); break;
            case HuVarKind.Int: _intVars.Add(key); break;
            default: _floatVars.Add(key); break;
        }
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
                t = UseArray(ix.Name, ix.Pos);
                ExpectNum(ix.Index, intConv: true);
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
                // HuBASIC の / は浮動小数の割り算。ただし左右とも整数で、結果がその場で整数に丸められる
                // 位置なら、整数の割り算を四捨五入する BAS_IDIVR で同じ値になる (型を Int にして目印にする)
                var lt = ExpectNum(b.Left, intConv: false);
                var rt = ExpectNum(b.Right, intConv: false);
                return intConv && lt == HuType.Int && rt == HuType.Int ? HuType.Int : HuType.Float;
            }
            case "-":
            case "*":
                return NumResult(ExpectNum(b.Left, intConv: false), ExpectNum(b.Right, intConv: false));
            case "^":
                // べき乗はいつも浮動小数 (2^3 も)
                ExpectNum(b.Left, intConv: false);
                ExpectNum(b.Right, intConv: false);
                return HuType.Float;
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
            case "PEEK":
            case "PEEK@":
                ExpectNum(a[0], intConv: true);
                return HuType.Int;
            case "TIME":
                return HuType.Float;
            case "ASC":
                ExpectStr(a[0]);
                return HuType.Int;
            case "VAL":
                // HuBASIC の VAL は浮動小数を返す (VAL("1.6") は 1.6。整数に入れるときに四捨五入)
                ExpectStr(a[0]);
                return HuType.Float;
            case "STR$":
                ExpectNum(a[0], intConv: false);
                return HuType.Str;
            case "CHR$":
                // CHR$(a, b, …) は、それぞれの文字をつなげる
                foreach (var x in a) ExpectNum(x, intConv: true);
                return HuType.Str;
            case "CGPAT$":
                ExpectNum(a[0], intConv: true);
                return HuType.Str;
            case "CHARACTER$":
                ExpectNum(a[0], intConv: true);
                ExpectNum(a[1], intConv: true);
                return HuType.Str;
            case "SCRN$":
                ExpectNum(a[0], intConv: true);
                ExpectNum(a[1], intConv: true);
                ExpectNum(a[2], intConv: true);
                return HuType.Str;
            case "STRING$":
                ExpectNum(a[0], intConv: true);
                // 2 つ目は文字列 (それを繰り返す) か文字コード
                CheckExpr(a[1], intConv: true);
                return HuType.Str;
            case "HEXCHR$":
                ExpectStr(a[0]);
                return HuType.Str;
            case "MID$":
            case "LEFT$":
            case "RIGHT$":
                ExpectStr(a[0]);
                for (int i = 1; i < a.Count; i++) ExpectNum(a[i], intConv: true);
                return HuType.Str;
            case "INKEY$":
                // INKEY$(0) だけ (INKEY$(1)・(2) は別の意味なので未対応)
                if (a.Count > 0 && !(a[0] is HuNum { Value: 0 }))
                    _diag.Error("HB0302", "INKEY$ の引数は 0 だけに対応しています", c.Pos);
                return HuType.Str;
            case "INPUT$":
                ExpectNum(a[0], intConv: true);
                return HuType.Str;
            case "RND":
                // 引数の意味は記録にないので、数の定数だけを受け付けて捨てる (式を書くと評価を省けないため)
                if (a.Count > 0)
                {
                    ExpectNum(a[0], intConv: false);
                    if (!IsSignedNumConst(a[0]))
                        _diag.Error("HB0302", "RND の引数は数の定数だけに対応しています", c.Pos);
                }
                return HuType.Float;
            case "INT":
                ExpectNum(a[0], intConv: false);
                return HuType.Float;
            // 本物は整数どうしの計算でも 16 ビットに収めないので、返す型は外から見えない。値が整数で済む関数は、
            // 速さのため Int を返す (ABS・FIX は引数が Int のときだけ)
            case "ABS":
            case "FIX":
                return ExpectNum(a[0], intConv: false);
            case "SGN":
                ExpectNum(a[0], intConv: false);
                return HuType.Int;
            case "CINT":
                // 整数に入れる位置と同じ丸め
                ExpectNum(a[0], intConv: true);
                return HuType.Int;
            case "LEN":
                ExpectStr(a[0]);
                return HuType.Int;
            case "INSTR":
                // INSTR([n,]a,b)。n は整数に入れる位置と同じに丸める
                if (a.Count > 2) ExpectNum(a[0], intConv: true);
                ExpectStr(a[^2]);
                ExpectStr(a[^1]);
                return HuType.Int;
            case "HEX$":
            case "SPACE$":
                ExpectNum(a[0], intConv: true);
                return HuType.Str;
            case "SQR":
            case "SIN":
            case "COS":
            case "TAN":
            case "ATN":
            case "LOG":
            case "EXP":
            case "FRAC":
            case "CSNG":
            case "CDBL":
            case "PAI":
            case "RAD":
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
                CheckTarget(g.Line, g.Label, g.Pos);
                break;
            case HuGosub g:
                CheckTarget(g.Line, g.Label, g.Pos);
                _gosubIds[g] = _gosubCount++;
                break;
            case HuReturn r:
                if (r.Line is int rl) CheckTarget(rl, r.Pos);
                break;
            case HuOnJump on:
                ExpectNum(on.Selector, intConv: false);
                for (int i = 0; i < on.Lines.Count; i++) CheckTarget(on.Lines[i], on.Labels?[i], on.Pos);
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
                CheckForVar(fr.Var, "FOR", fr.Pos);
                // 整数の FOR の初めの値は、変数に入れる位置 (整数に丸める)
                ExpectNum(fr.From, intConv: VarType(fr.Var) == HuType.Int);
                ExpectNum(fr.To, intConv: false);
                if (fr.Step != null) ExpectNum(fr.Step, intConv: false);
                _forIds[fr] = _forCount++;
                break;
            case HuNext nx:
                foreach (var v in nx.Vars)
                {
                    UseVar(v, nx.Pos);
                    CheckForVar(v, "NEXT", nx.Pos);
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
            case HuSwap sw:
            {
                CheckExpr(sw.A, false);
                CheckExpr(sw.B, false);
                // 型の種類 (単精度と倍精度も別) が違えば、本物は Type mismatch
                if (KindOfTarget(sw.A) != KindOfTarget(sw.B))
                    _diag.Error("HB0404", "SWAP の 2 つの変数の型が違います", sw.Pos);
                break;
            }
            case HuPrint p:
                if (p.Using != null) ExpectStr(p.Using);
                foreach (var item in p.Items)
                {
                    if (item is HuPrintExpr pe)
                    {
                        var t = CheckExpr(pe.Expr, false);
                        if (p.Using != null && t == HuType.Str)
                            _diag.Error("HB0406", "PRINT USING は数値の書式だけに対応しています", pe.Expr.Pos);
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
            case HuLineGraphic lg:
                ExpectNum(lg.X1, true);
                ExpectNum(lg.Y1, true);
                ExpectNum(lg.X2, true);
                ExpectNum(lg.Y2, true);
                if (lg.Color != null) ExpectNum(lg.Color, true);
                break;
            case HuPaint pt:
                ExpectNum(pt.X, true);
                ExpectNum(pt.Y, true);
                // 色の位置の文字列は本物ではタイルパターン
                if (CheckExpr(pt.Color, true) == HuType.Str)
                    _diag.Error("HB0301", "PAINT のタイルパターンは未対応です", pt.Color.Pos);
                foreach (var b in pt.Borders) ExpectNum(b, true);
                break;
            case HuPoke pk:
                ExpectNum(pk.Addr, true);
                foreach (var v in pk.Values) ExpectNum(v, true);
                break;
            case HuDefChr dc:
                ExpectNum(dc.Code, true);
                ExpectStr(dc.Pattern);
                break;
            case HuInput inp:
                if (inp.Prompt != null) AddLiteral(inp.Prompt);
                foreach (var t in inp.Targets) CheckExpr(t, false);
                break;
            case HuMidSet ms:
                UseVar(ms.Var, ms.Pos);
                if (VarType(ms.Var) != HuType.Str)
                    _diag.Error("HB0404", $"MID$ の文の変数 {ms.Var} は文字列の変数にしてください", ms.Pos);
                ExpectNum(ms.Start, intConv: true);
                if (ms.Length != null) ExpectNum(ms.Length, intConv: true);
                ExpectStr(ms.Value);
                break;
            case HuDim:
            case HuDefType:
            case HuData:
            case HuRem:
            case HuRepeat:
            case HuWend:
            case HuEnd:
            case HuInit:
                break;
        }
    }

    private void CheckForVar(string name, string stmt, HuPos pos)
    {
        if (VarType(name) == HuType.Str)
            _diag.Error("HB0404", $"{stmt} の変数 {name} に文字列の変数は使えません", pos);
    }

    private void CheckCommand(HuCommand cmd)
    {
        if (cmd.Name == "PLAY")
        {
            // 文字列なら MML、数ならテンポ (TEMPO と同じ。展開で見分ける)
            CheckExpr(cmd.Args[0]!, false);
            return;
        }
        if (cmd.Name == "KEY")
        {
            ExpectNum(cmd.Args[0]!, intConv: true);
            ExpectStr(cmd.Args[1]!);
            _diag.Warning("HB0704", "KEY は何もしない命令として扱います (ファンクションキーの文字列は定義しません。番号の範囲だけを調べます)", cmd.Pos);
            return;
        }
        if (cmd.Name == "RANDOMIZE")
        {
            // 本物は範囲を調べない (40000 もエラーにならない)
            if (cmd.Args.Count > 0) ExpectNum(cmd.Args[0]!, intConv: false);
            return;
        }
        if (cmd.Name == "TIME")
        {
            // 小数は切り捨てる (展開で INT をかける) ので、整数にする位置ではない
            ExpectNum(cmd.Args[0]!, intConv: false);
            return;
        }
        if (cmd.Name == "KBUF")
        {
            _diag.Warning("HB0701", "KBUF は何もしない命令として扱います", cmd.Pos);
            return;
        }
        if (cmd.Name == "SCREEN")
        {
            foreach (var a in cmd.Args)
            {
                if (a == null) continue;
                ExpectNum(a, intConv: true);
                // 何もしない命令なので、引数は評価を省いてよい数の定数だけを受け付ける
                if (!IsSignedNumConst(a))
                    _diag.Error("HB0703", "SCREEN の引数は数の定数だけに対応しています", cmd.Pos);
            }
            // 0,0 (と省略) は INIT や WIDTH の後と同じ状態なので黙って何もしない。それ以外は見え方が本物と違う
            bool defaultPages = cmd.Args.Take(2).All(a => a == null || a is HuNum { Value: 0 });
            if (!defaultPages || cmd.Args.Count == 0)
                _diag.Warning("HB0702", "SCREEN は何もしない命令として扱います (テキストの頁の切り替えは未対応。" +
                                        "表示も書き込みも 0 頁のまま)", cmd.Pos);
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
                    // 同じ変数を名指しする NEXT は深さに関係なく一致 (HuBASIC はその NEXT で
                    // 内側の FOR 枠を捨てる)。それ以外は内側のループを閉じたものとして深さを減らし、
                    // 深さ 0 の変数なしの NEXT は一致
                    if ((named && Key(nx.Vars[k]) == Key(fr.Var)) || (!named && depth == 0))
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

    /// <summary>配列の 1 要素の大きさ (文字列の配列の要素は、文字列の変数と同じ 長さの上限 + 1 バイトのバッファ)</summary>
    private int ElementBytes(HuVarKind kind) => kind switch
    {
        HuVarKind.Int => 2,
        HuVarKind.Str => _stringLen + 1,
        _ => 3,
    };

    private int ComputeStaticBytes(int dataBytes, int dataLineCount)
    {
        long total = _intVars.Count * 2L + _floatVars.Count * 3L + _strVars.Count * (_stringLen + 1L);
        foreach (var (key, bound) in _arrays)
            total += (bound + 1L) * ElementBytes(HuProgram.KindOfKey(key)) + 4;
        foreach (var lit in _literals) total += lit.Length + 1L;
        total += dataBytes + (dataLineCount + 1L) * 4;
        return (int)Math.Min(total, int.MaxValue);
    }
}
