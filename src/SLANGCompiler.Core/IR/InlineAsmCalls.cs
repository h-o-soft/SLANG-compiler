namespace SLANGCompiler.IR;

/// <summary>
/// #ASM の中の `CALL 名前` の行を読む。トップレベルの #ASM から、呼ぶランタイムを選ぶのに使う
/// </summary>
public static class InlineAsmCalls
{
    private const string DefaultNamespace = "NAME_SPACE_DEFAULT.";

    // 行の頭のラベル (`名前:`・`.名前:`・`名前::`)。ラベルと同じ行の命令を読むために外す
    private static readonly System.Text.RegularExpressions.Regex LeadingLabel =
        new(@"^\.?[A-Za-z_][A-Za-z0-9_]*::?\s*");

    /// <summary>
    /// `CALL 名前`・`CALL Z,名前` (条件つきも) の名前。大文字・小文字は問わない。`;` から後は注釈。
    /// 行の頭のラベルは飛ばす (`L1: CALL 名前` も読む)。`NAME_SPACE_DEFAULT.名前` は `名前` にする (同じものなので)
    /// </summary>
    public static IEnumerable<string> Names(string asm)
    {
        foreach (var line in asm.Split('\n'))
        {
            var t = LeadingLabel.Replace(line.Trim(), "");
            if (t.Length < 5 || !t.StartsWith("CALL", StringComparison.OrdinalIgnoreCase) || (t[4] != ' ' && t[4] != '\t')) continue;
            var operand = t[5..].Split(';')[0].Trim();
            var name = operand[(operand.LastIndexOf(',') + 1)..].Trim();
            if (name.StartsWith(DefaultNamespace, StringComparison.OrdinalIgnoreCase))
                name = name[DefaultNamespace.Length..];
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

    /// <summary>トップレベル (関数の外) の #ASM の中身。#ASM ... #END の 1 つが 1 つ</summary>
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
