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
}
