using SLANGCompiler.HuBasic.Analysis;
using SLANGCompiler.HuBasic.Parsing;

namespace SLANGCompiler.HuBasic.Lowering;

public enum HuWaitMode { None, Line, Frame }

public sealed record HuLowerOptions(HuWaitMode Wait = HuWaitMode.None, int WaitArg = 1, bool LineMarks = false);

/// <summary>
/// <see cref="HuProgram"/> を、ラベルと飛び先とランタイム呼び出しの列に展開する。
/// - GOSUB は制御スタックに戻り先 ID を積んで飛び、RETURN は共通の戻り処理 (H_RET) で分岐する
/// - FOR / NEXT は制御スタックの FOR 枠で実行時に対応付け、NEXT の折り返しは共通の H_FOR で分岐する
/// - IF・REPEAT・WHILE はすべてラベルと条件付きの飛び先にする
/// 隠し変数の名前は抽象名 ("T", "DIM:名前", "UB:名前") で、後段が実際の名前に写す。
/// </summary>
public sealed class HuLowerer
{
    public const string Temp = "T";
    /// <summary>実行中の行番号 (実行時エラーの「in 行番号」の表示に使う)</summary>
    public const string CurrentLine = "LINE";
    public static string DimState(string array) => "DIM:" + array;
    public static string UpperBound(string array) => "UB:" + array;

    public const string ReturnLabel = "H_RET";
    public const string ForLabel = "H_FOR";

    private readonly HuProgram _prog;
    private readonly HuLowerOptions _opt;
    /// <summary>いま変換している行の番号 (GOSUB から戻った後に実行中の行番号を戻すのに使う)</summary>
    private int _curLine;
    private readonly List<FlatOp> _ops = new();
    private int _labelSeq;

    // FOR の出口 (NEXT の部分) → その出口を使う FOR の番号
    private readonly Dictionary<(HuNext, int), List<int>> _exitsAt = new(new NextPartComparer());

    private HuLowerer(HuProgram prog, HuLowerOptions opt)
    {
        _prog = prog;
        _opt = opt;
        foreach (var (fr, exit) in prog.ForExits)
        {
            var key = (exit.Next, exit.Part);
            if (!_exitsAt.TryGetValue(key, out var list)) _exitsAt[key] = list = new List<int>();
            list.Add(prog.ForIds[fr]);
        }
    }

    public static HuFlat Lower(HuProgram prog, HuLowerOptions? opt = null)
        => new HuLowerer(prog, opt ?? new HuLowerOptions()).Run();

    public static string LineLabel(int line) => "L" + line;
    public static string ReturnPoint(int id) => "R" + id;
    public static string ForTop(int id) => "QFT" + id;
    public static string ForExit(int id) => "QFX" + id;

    private string NewLabel() => "Q" + _labelSeq++;

    private void Emit(FlatOp op) => _ops.Add(op);
    private void Call(string fn, params FArg[] args) => Emit(new FCall(fn, args));
    private static FArg E(HuExpr e) => new FExprArg(e);
    private static FArg I(int v) => new FIntArg(v);

    private HuFlat Run()
    {
        Call("BAS_INIT");
        Call("BAS_LNINIT", new FHiddenAddrArg(CurrentLine));
        if (_prog.DataBytes.Length > 0) Call("BAS_DATAINIT", new FDataTableArg(false), new FDataTableArg(true));
        foreach (var name in _prog.Arrays.Keys)
        {
            Emit(new FAssignHidden(DimState(name), new HuNum(0, HuPos.None)));
            Emit(new FAssignHidden(UpperBound(name), new HuNum(_prog.Arrays[name], HuPos.None)));
        }

        foreach (var line in _prog.Lines)
        {
            _curLine = line.Number;
            Emit(new FLabel(LineLabel(line.Number)));
            Emit(new FAssignHidden(CurrentLine, new HuNum(line.Number, line.Pos)));
            if (_opt.LineMarks) Emit(new FLineMark(line.Number, line.SourceText));
            if (_opt.Wait == HuWaitMode.Line) Call("BAS_WLINE", I(_opt.WaitArg));
            LowerStatements(line.Statements);
        }
        Call("BAS_END");

        // 共通の戻り処理: 制御スタックの GOSUB 枠を外して戻り先へ
        Emit(new FLabel(ReturnLabel));
        Emit(new FCall("BAS_GPOP", Array.Empty<FArg>(), Temp));
        var returns = _prog.GosubIds.Values.OrderBy(v => v).Select(id => (id, ReturnPoint(id))).ToList();
        if (returns.Count > 0) Emit(new FDispatch(Temp, returns));
        Call("BAS_END");

        // FOR の共通の折り返し: NEXT が返した FOR の番号の先頭へ
        Emit(new FLabel(ForLabel));
        var fors = _prog.ForIds.Values.OrderBy(v => v).Select(id => (id, ForTop(id))).ToList();
        if (fors.Count > 0) Emit(new FDispatch(Temp, fors));
        Call("BAS_END");

        var hidden = new List<string> { Temp, CurrentLine };
        foreach (var name in _prog.Arrays.Keys)
        {
            hidden.Add(DimState(name));
            hidden.Add(UpperBound(name));
        }
        return new HuFlat(_ops, hidden);
    }

    private void LowerStatements(IEnumerable<HuStmt> stmts)
    {
        foreach (var s in stmts) LowerStatement(s);
    }

    private void LowerStatement(HuStmt s)
    {
        if (NeedsTempReset(s)) Call("BAS_STMP");
        switch (s)
        {
            case HuAssign a:
                if (a.Target.Type == HuType.Str) Call("BAS_SSET", E(a.Target), E(a.Value));
                else Emit(new FAssign(a.Target, a.Value));
                break;
            case HuGoto g:
                Emit(new FGoto(LineLabel(g.Line)));
                break;
            case HuGosub g:
            {
                var id = _prog.GosubIds[g];
                Call("BAS_GPUSH", I(id));
                Emit(new FGoto(LineLabel(g.Line)));
                Emit(new FLabel(ReturnPoint(id)));
                RestoreCurrentLine(g.Pos);
                break;
            }
            case HuReturn r:
                if (r.Line is int rl)
                {
                    Call("BAS_GPOP");
                    Emit(new FGoto(LineLabel(rl)));
                }
                else
                    Emit(new FGoto(ReturnLabel));
                break;
            case HuOnJump on:
                LowerOn(on);
                break;
            case HuIf f:
                LowerIf(f);
                break;
            case HuFor fr:
            {
                var id = _prog.ForIds[fr];
                var v = new HuVar(fr.Var, fr.Pos) { Type = HuType.Int };
                Emit(new FAssign(v, fr.From));
                var step = fr.Step ?? new HuNum(1, fr.Pos);
                Emit(new FCall("BAS_FORPUSH", new[] { new FAddrArg(v), E(fr.To), E(step), I(id) }, Temp));
                if (_prog.ForExits.ContainsKey(fr))
                    Emit(new FIfHidden(Temp, "==", 0, ForExit(id)));
                Emit(new FLabel(ForTop(id)));
                break;
            }
            case HuNext nx:
            {
                int parts = Math.Max(1, nx.Vars.Count);
                for (int k = 0; k < parts; k++)
                {
                    FArg addr = nx.Vars.Count == 0
                        ? I(0)
                        : new FAddrArg(new HuVar(nx.Vars[k], nx.Pos) { Type = HuType.Int });
                    Emit(new FCall("BAS_FORNEXT", new[] { addr }, Temp));
                    Emit(new FIfHidden(Temp, ">=", 0, ForLabel));
                    if (_exitsAt.TryGetValue((nx, k), out var ids))
                        foreach (var id in ids) Emit(new FLabel(ForExit(id)));
                }
                break;
            }
            case HuRepeat rp:
                Emit(new FLabel(LoopTop(rp)));
                break;
            case HuUntil u:
                if (_prog.LoopPairs.TryGetValue(u, out var rep))
                    Emit(new FIf(u.Cond, false, LoopTop(rep)));
                break;
            case HuWhile w:
                // 条件は毎周評価されるので、一時領域を空にするのはループの先頭ラベルの後ろ
                Emit(new FLabel(LoopTop(w)));
                if (MakesTemps(w.Cond)) Call("BAS_STMP");
                Emit(new FIf(w.Cond, false, LoopEnd(w)));
                break;
            case HuWend we:
                if (_prog.LoopPairs.TryGetValue(we, out var wh))
                {
                    Emit(new FGoto(LoopTop(wh)));
                    Emit(new FLabel(LoopEnd(wh)));
                }
                break;
            case HuEnd:
                Call("BAS_END");
                break;
            case HuInit:
                Call("BAS_INIT2");
                break;
            case HuDim d:
                foreach (var (name, bound) in d.Arrays)
                {
                    if (!_prog.Arrays.ContainsKey(name)) continue;
                    var n = (int)(HuAnalyzer.ConstValue(bound) ?? 0);
                    Call("BAS_DIM", new FHiddenAddrArg(DimState(name)), new FHiddenAddrArg(UpperBound(name)), I(n));
                }
                break;
            case HuRead rd:
                foreach (var t in rd.Targets)
                {
                    if (t.Type == HuType.Str)
                    {
                        Call("BAS_SSET", E(t), E(new HuCall(FlatPseudo.ReadStr, Array.Empty<HuExpr>(), rd.Pos) { Type = HuType.Str }));
                        Call("BAS_STMP");
                    }
                    else
                    {
                        // 数値の READ も内部で一時文字列を使う (BAS_READN は項目を文字列で読んでから解釈する)
                        Emit(new FAssign(t, new HuCall(FlatPseudo.ReadNum, Array.Empty<HuExpr>(), rd.Pos) { Type = HuType.Int }));
                        Call("BAS_STMP");
                    }
                }
                break;
            case HuRestore rs:
                Call("BAS_RESTORE", I(rs.Line ?? 0));
                break;
            case HuOut o:
                Emit(new FPortOut(o.Port, o.Value));
                break;
            case HuPrint p:
                LowerPrint(p);
                break;
            case HuCommand c:
                LowerCommand(c);
                break;
            case HuLineText lt:
                Call("BAS_TLINE", E(lt.X1), E(lt.Y1), E(lt.X2), E(lt.Y2), E(lt.Char), I(lt.Box ? 1 : 0));
                break;
            case HuDefChr dc:
                Call("BAS_DEFCHR", E(dc.Code), E(dc.Pattern));
                break;
            case HuRem rem:
                if (rem.Directive == "VSYNC" && _opt.Wait == HuWaitMode.Frame)
                    Call("BAS_VSYNC", I(rem.DirectiveArg > 0 ? rem.DirectiveArg : _opt.WaitArg));
                break;
            case HuDefint:
            case HuData:
                break;
        }
    }

    private string LoopTop(HuStmt loop) => "QL" + _prog.LoopIds[loop];
    private string LoopEnd(HuStmt loop) => "QLE" + _prog.LoopIds[loop];

    private void LowerOn(HuOnJump on)
    {
        Emit(new FAssignHidden(Temp, on.Selector));
        switch (on.Kind)
        {
            case "GOTO":
                Emit(new FDispatch(Temp, on.Lines.Select((l, i) => (i + 1, LineLabel(l))).ToList()));
                break;
            case "GOSUB":
            {
                var id = _prog.GosubIds[on];
                var ret = ReturnPoint(id);
                Emit(new FIfHidden(Temp, "<", 1, ret));
                Emit(new FIfHidden(Temp, ">", on.Lines.Count, ret));
                Call("BAS_GPUSH", I(id));
                Emit(new FDispatch(Temp, on.Lines.Select((l, i) => (i + 1, LineLabel(l))).ToList()));
                Emit(new FLabel(ret));
                RestoreCurrentLine(on.Pos);
                break;
            }
            case "RESTORE":
            {
                var end = NewLabel();
                var labels = on.Lines.Select(_ => NewLabel()).ToList();
                Emit(new FDispatch(Temp, labels.Select((l, i) => (i + 1, l)).ToList()));
                Emit(new FGoto(end));
                for (int i = 0; i < labels.Count; i++)
                {
                    Emit(new FLabel(labels[i]));
                    Call("BAS_RESTORE", I(on.Lines[i]));
                    Emit(new FGoto(end));
                }
                Emit(new FLabel(end));
                break;
            }
        }
    }

    private void LowerIf(HuIf f)
    {
        var end = NewLabel();
        if (f.Else == null)
        {
            Emit(new FIf(f.Cond, false, end));
            LowerStatements(f.Then);
        }
        else
        {
            var els = NewLabel();
            Emit(new FIf(f.Cond, false, els));
            LowerStatements(f.Then);
            Emit(new FGoto(end));
            Emit(new FLabel(els));
            LowerStatements(f.Else);
        }
        Emit(new FLabel(end));
    }

    /// <summary>GOSUB から戻った所で、実行中の行番号を呼び出した行に戻す (実行時エラーの「in 行番号」のため)</summary>
    private void RestoreCurrentLine(HuPos pos)
        => Emit(new FAssignHidden(CurrentLine, new HuNum(_curLine, pos)));

    private void LowerPrint(HuPrint p)
    {
        if (p.Channel == 0) Call("BAS_PRAW", I(1));
        if (p.Using != null)
        {
            Call("BAS_PUFMT", E(p.Using));
            Call("BAS_STMP");
        }
        HuPrintItem? last = null;
        foreach (var item in p.Items)
        {
            switch (item)
            {
                case HuPrintExpr pe when p.Using != null:
                    Call("BAS_PUSING", E(pe.Expr));
                    if (MakesTemps(pe.Expr)) Call("BAS_STMP");
                    break;
                case HuPrintExpr pe when pe.Expr.Type == HuType.Str:
                    Call("BAS_PSTR", E(pe.Expr));
                    if (MakesTemps(pe.Expr)) Call("BAS_STMP");
                    break;
                case HuPrintExpr pe when pe.Expr.Type == HuType.Float:
                    Emit(new FCall("BAS_PFLT", new FArg[] { new FFloatArg(pe.Expr) }));
                    if (MakesTemps(pe.Expr)) Call("BAS_STMP");
                    break;
                case HuPrintExpr pe:
                    Call("BAS_PNUM", E(pe.Expr));
                    if (MakesTemps(pe.Expr)) Call("BAS_STMP");
                    break;
                case HuPrintSep { Sep: ',' } when p.Using == null:
                    Call("BAS_PZONE");
                    break;
                case HuPrintSpc sp:
                    Call("BAS_PSPC", E(sp.Count));
                    if (MakesTemps(sp.Count)) Call("BAS_STMP");
                    break;
            }
            last = item;
        }
        if (last is not HuPrintSep) Call("BAS_PNL");
        if (p.Channel == 0) Call("BAS_PRAW", I(0));
    }

    private void LowerCommand(HuCommand c)
    {
        HuExpr? Arg(int i) => i < c.Args.Count ? c.Args[i] : null;
        FArg ArgOr(int i, int dflt) => Arg(i) is HuExpr e ? E(e) : I(dflt);
        switch (c.Name)
        {
            case "LOCATE": Call("BAS_LOCATE", E(Arg(0)!), E(Arg(1)!)); break;
            case "COLOR": Call("BAS_COLOR", ArgOr(0, -1), ArgOr(1, -1)); break;
            case "CLS": Call("BAS_CLS", ArgOr(0, -1)); break; // 省略 = テキストだけ (-1)、0-3 = グラフィック、4 = 全部
            case "WIDTH": Call("BAS_WIDTH", E(Arg(0)!)); break;
            case "CFLASH": Call("BAS_CFLASH", ArgOr(0, 0)); break;
            case "CSIZE": Call("BAS_CSIZE", ArgOr(0, 0)); break;
            case "CGEN": Call("BAS_CGEN", E(Arg(0)!)); break;
            case "PLAY": Call("BAS_PLAY", E(Arg(0)!)); break;
            case "TEMPO": Call("BAS_TEMPO", E(Arg(0)!)); break;
            case "BEEP": Call("BAS_BEEP"); break;
            case "CLICK": Call("BAS_CLICK", E(Arg(0)!)); break;
            case "PAUSE": Call("BAS_PAUSE", E(Arg(0)!)); break;
        }
    }

    // ------------------------------------------------------------ 一時文字列

    /// <summary>式の評価で一時文字列を作るか (文字列を返す関数・連結・DATA の読み出し)</summary>
    public static bool MakesTemps(HuExpr e) => e switch
    {
        HuCall c => c.Type == HuType.Str || c.Args.Any(MakesTemps),
        HuBinary b => (b.Op == "+" && b.Type == HuType.Str) || MakesTemps(b.Left) || MakesTemps(b.Right),
        HuUnary u => MakesTemps(u.Operand),
        HuIndex ix => MakesTemps(ix.Index),
        _ => false,
    };

    /// <summary>
    /// 文の頭で一時領域を空に戻すか。文が持つ式 (代入先の添字・FOR の式なども含む) のどれかが一時文字列を作るなら戻す。
    /// IF の THEN 節・ELSE 節の文は、それぞれの文の頭で判定する。WHILE の条件はループの先頭ラベルの後ろで戻す (別扱い)。
    /// PRINT は項目ごとに戻すので常に戻す。READ は項目を一時文字列で読むので、前の文の一時文字列が残らないよう常に戻す。
    /// </summary>
    internal static bool NeedsTempReset(HuStmt s)
        => s is HuPrint or HuRead || (s is not HuWhile && StatementExprs(s).Any(MakesTemps));

    /// <summary>文が直接持つ式をすべて列挙する (IF の THEN 節・ELSE 節の中の文は含まない)</summary>
    internal static IEnumerable<HuExpr> StatementExprs(HuStmt s)
    {
        switch (s)
        {
            case HuAssign a: yield return a.Target; yield return a.Value; break;
            case HuOnJump on: yield return on.Selector; break;
            case HuIf f: yield return f.Cond; break;
            case HuFor fr:
                yield return fr.From;
                yield return fr.To;
                if (fr.Step != null) yield return fr.Step;
                break;
            case HuUntil u: yield return u.Cond; break;
            case HuWhile w: yield return w.Cond; break;
            case HuDim d: foreach (var (_, b) in d.Arrays) yield return b; break;
            case HuRead r: foreach (var t in r.Targets) yield return t; break;
            case HuOut o: yield return o.Port; yield return o.Value; break;
            case HuPrint p:
                if (p.Using != null) yield return p.Using;
                foreach (var item in p.Items)
                {
                    if (item is HuPrintExpr pe) yield return pe.Expr;
                    else if (item is HuPrintSpc sp) yield return sp.Count;
                }
                break;
            case HuCommand c: foreach (var a in c.Args) if (a != null) yield return a; break;
            case HuLineText lt:
                yield return lt.X1; yield return lt.Y1; yield return lt.X2; yield return lt.Y2; yield return lt.Char;
                break;
            case HuDefChr dc: yield return dc.Code; yield return dc.Pattern; break;
        }
    }

    private sealed class NextPartComparer : IEqualityComparer<(HuNext, int)>
    {
        public bool Equals((HuNext, int) x, (HuNext, int) y) => ReferenceEquals(x.Item1, y.Item1) && x.Item2 == y.Item2;
        public int GetHashCode((HuNext, int) obj) => HashCode.Combine(System.Runtime.CompilerServices.RuntimeHelpers.GetHashCode(obj.Item1), obj.Item2);
    }
}
