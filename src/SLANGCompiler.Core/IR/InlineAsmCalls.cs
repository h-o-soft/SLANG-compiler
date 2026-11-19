namespace SLANGCompiler.IR;

/// <summary>
/// #ASM の中の `CALL 名前` の行を読む。コード生成がランタイムを選ぶのと、モジュールの呼び出しをたどるのに使う
/// </summary>
public static class InlineAsmCalls
{
    /// <summary>
    /// `CALL 名前`・`CALL Z,名前` (条件つきも) の名前。大文字・小文字は問わない。`;` から後は注釈。
    /// 行の頭にラベルのある行 (`L1: CALL 名前`) は読まない
    /// </summary>
    public static IEnumerable<string> Names(string asm)
    {
        foreach (var line in asm.Split('\n'))
        {
            var t = line.Trim();
            if (t.Length < 5 || !t.StartsWith("CALL", StringComparison.OrdinalIgnoreCase) || (t[4] != ' ' && t[4] != '\t')) continue;
            var operand = t[5..].Split(';')[0].Trim();
            var name = operand[(operand.LastIndexOf(',') + 1)..].Trim();
            if (name.Length > 0) yield return name;
        }
    }

    // 行の頭で決めるラベル (`名前:` と `名前 EQU`。前の空白はよい)。AILZ80ASM の書き方を全部は追わない (拾えない書き方はアセンブルのエラーのまま)
    private static readonly System.Text.RegularExpressions.Regex LineLabel =
        new(@"^\s*([A-Za-z_][A-Za-z0-9_]*)(?::|\s+EQU\b)", System.Text.RegularExpressions.RegexOptions.IgnoreCase);

    /// <summary>#ASM の行の頭で決めるラベル</summary>
    public static IEnumerable<string> Labels(IEnumerable<string> lines) =>
        lines.Select(line => LineLabel.Match(line))
             .Where(m => m.Success)
             .Select(m => m.Groups[1].Value);

    /// <summary>トップレベル (関数の外) の #ASM の中身</summary>
    public static IEnumerable<string> TopLevel(IrModule module) =>
        module.GlobalData
            .Where(i => i.Op == IrOp.InlineAsm && i.Dest.Kind == IrOperandKind.AsmString && i.Dest.Name != null)
            .Select(i => i.Dest.Name!);

    /// <summary>関数の中の #ASM の中身</summary>
    public static IEnumerable<string> InFunctions(IEnumerable<IrFunction> functions) =>
        functions.SelectMany(f => f.Instructions)
            .Where(i => i.Op == IrOp.InlineAsm)
            .Select(i => i.Dest.Kind == IrOperandKind.Temp ? i.Src1.Name : i.Dest.Name)
            .Where(a => a != null)
            .Select(a => a!);
}
