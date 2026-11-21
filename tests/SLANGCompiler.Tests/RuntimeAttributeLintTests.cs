using System.Text.RegularExpressions;
using Xunit;

namespace SLANGCompiler.Tests;

/// <summary>
/// ランタイムの `; @key value` の行の形を確かめる。
/// slangc は知らない key を黙って飛ばし、`@works` の `label:size` でない項目も黙って捨てる。
/// ランタイムを別の道具で読むときに困らないよう、属性に見える行はすべて正しい形にしておく。
/// </summary>
public class RuntimeAttributeLintTests
{
    // RuntimeParser.Parse が扱う key
    private static readonly HashSet<string> KnownKeys = new(StringComparer.Ordinal)
    {
        "name", "param_count", "calls", "lib", "works", "works_align", "requires",
        "init_code", "end_init", "function_type", "stack_cleanup", "alias",
        "param_types", "result_type", "resident",
    };

    private static readonly Regex WorksItem = new(@"^[A-Za-z_][A-Za-z0-9_]*:[0-9]+$");
    private static readonly Regex Number = new(@"^[0-9]+$");
    private static readonly Regex Name = new(@"^[A-Za-z_][A-Za-z0-9_]*$");

    /// <summary>1 つのファイルの中身を調べ、形の悪い行を「ファイル:行: 内容」の形で返す</summary>
    internal static List<string> Lint(string text, string path)
    {
        var problems = new List<string>();
        var lines = text.Split('\n');
        bool sawName = false;
        string? openInit = null;           // 閉じていない @init_code の行
        int? paramCount = null;            // いまの関数の @param_count
        (int Count, string Where)? paramTypes = null; // いまの関数の @param_types の数と行

        void EndFunction()
        {
            if (openInit != null)
                problems.Add($"{openInit} (@init_code が @end_init で閉じていない)");
            if (paramCount is int pc && paramTypes is var (n, w) && pc != n)
                problems.Add($"{w} (@param_types の数 {n} が @param_count {pc} と違う)");
            openInit = null;
            paramCount = null;
            paramTypes = null;
        }

        for (int i = 0; i < lines.Length; i++)
        {
            var line = lines[i].TrimEnd('\r');
            var t = line.TrimStart();
            if (!t.StartsWith("; @", StringComparison.Ordinal))
                continue;
            var meta = t[3..];
            var sp = meta.IndexOf(' ');
            var key = sp >= 0 ? meta[..sp].Trim() : meta.Trim();
            var value = sp >= 0 ? meta[(sp + 1)..].Trim() : "";
            string where = $"{path}:{i + 1}: {t}";

            if (!KnownKeys.Contains(key))
            {
                problems.Add($"{where} (知らない key '{key}')");
                continue;
            }
            if (key == "name")
            {
                EndFunction();
                sawName = true;
            }
            else if (!sawName)
            {
                // slangc は最初の @name より前の属性を黙って飛ばす
                problems.Add($"{where} (@name より前にある)");
                continue;
            }
            switch (key)
            {
                case "name":
                case "lib":
                case "alias":
                case "function_type":
                    if (value.Length == 0)
                        problems.Add($"{where} (@{key} が空)");
                    else if (!Name.IsMatch(value))
                        problems.Add($"{where} (@{key} '{value}' が 1 つの名前でない)");
                    break;
                case "param_types":
                    var types = value.Split(',', StringSplitOptions.TrimEntries);
                    if (value.Length == 0)
                        problems.Add($"{where} (@param_types が空)");
                    else if (types.Any(ty => ty != "int" && ty != "float"))
                    {
                        foreach (var ty in types.Where(ty => ty != "int" && ty != "float"))
                            problems.Add($"{where} (@param_types の項目 '{ty}' は int か float)");
                    }
                    else if (types.Contains("float") && !(types is ["float"] or ["float", "float"]))
                        // FLOAT (3 バイト) を引数に渡せるのは、1 つだけのときか float,float のときだけ
                        problems.Add($"{where} (FLOAT を含む引数の型は float か float,float だけ)");
                    else
                        paramTypes = (types.Length, where);
                    break;
                case "result_type":
                    if (value != "int" && value != "float")
                        problems.Add($"{where} (@result_type は int か float)");
                    break;
                case "init_code":
                    if (value.Length != 0)
                        problems.Add($"{where} (@init_code に値は書かない)");
                    if (openInit != null)
                        problems.Add($"{where} (@init_code の中に @init_code がある)");
                    else
                        openInit = where;
                    break;
                case "end_init":
                    if (value.Length != 0)
                        problems.Add($"{where} (@end_init に値は書かない)");
                    if (openInit == null)
                        problems.Add($"{where} (対になる @init_code がない)");
                    openInit = null;
                    break;
                case "stack_cleanup":
                    if (value != "callee")
                        problems.Add($"{where} (@stack_cleanup は callee だけ)");
                    break;
                case "calls":
                case "requires":
                    if (value.Length == 0)
                        problems.Add($"{where} (@{key} が空)");
                    else
                        foreach (var n in value.Split(',', StringSplitOptions.TrimEntries))
                        {
                            if (!Name.IsMatch(n))
                                problems.Add($"{where} (@{key} の項目 '{n}' がランタイムの名前の形でない)");
                        }
                    break;
                case "works":
                    if (value.Length == 0)
                    {
                        problems.Add($"{where} (@works が空)");
                        break;
                    }
                    foreach (var item in value.Split(',', StringSplitOptions.TrimEntries))
                    {
                        if (!WorksItem.IsMatch(item))
                            problems.Add($"{where} (@works の項目 '{item}' が label:size の形でない)");
                        else if (!int.TryParse(item[(item.IndexOf(':') + 1)..], out _))
                            problems.Add($"{where} (@works の項目 '{item}' の大きさが大きすぎる)");
                    }
                    break;
                case "param_count":
                    if (!Number.IsMatch(value) || !int.TryParse(value, out var count))
                        problems.Add($"{where} (@param_count が 10 進の数でない)");
                    else
                        paramCount = count;
                    break;
                case "works_align":
                    if (!Number.IsMatch(value) || !int.TryParse(value, out var align))
                        problems.Add($"{where} (@works_align が 10 進の数でない)");
                    else if (align < 1 || (align & (align - 1)) != 0)
                        problems.Add($"{where} (@works_align は 2 の冪)");
                    break;
                case "resident":
                    if (value != "shared" && value != "local")
                        problems.Add($"{where} (@resident は shared か local)");
                    break;
            }
        }
        EndFunction();
        return problems;
    }

    [Fact]
    public void AllRuntimeFiles_HaveWellFormedAttributes()
    {
        var dir = Path.Combine(TestTools.RepoRoot(), "runtime");
        var files = Directory.GetFiles(dir, "*.asm", SearchOption.TopDirectoryOnly);
        Assert.NotEmpty(files);
        var problems = new List<string>();
        foreach (var f in files.OrderBy(x => x, StringComparer.Ordinal))
            problems.AddRange(Lint(File.ReadAllText(f), Path.GetFileName(f)));
        Assert.True(problems.Count == 0, string.Join("\n", problems));
    }

    [Fact]
    public void Lint_AcceptsWellFormedLines()
    {
        const string text = "; @name FOO\n; @param_count 2\n; @calls BAR, BAZ\n; @works FOOW:16,FOOX:2\n"
            + "; @works_align 256\n; @resident local\n; @lib FOOLIB\n; @stack_cleanup callee\n"
            + "; @alias FOO2\n; @param_types float,float\n; @result_type float\n; @function_type Machine\n; @init_code\n; @end_init\nRET\n; ふつうの注釈 @works ではない\n";
        Assert.Empty(Lint(text, "t.asm"));
    }

    [Theory]
    [InlineData("; @works 経由で確保する", "label:size")]
    [InlineData("; @works FOOW", "label:size")]
    [InlineData("; @works FOOW:$10", "label:size")]
    [InlineData("; @works", "空")]
    [InlineData("; @nmae FOO", "知らない key")]
    [InlineData("; @Name FOO", "知らない key")]
    [InlineData("; @param_count x", "10 進")]
    [InlineData("; @resident both", "shared か local")]
    [InlineData("; @name", "@name が空")]
    [InlineData("; @name FOO BAR", "1 つの名前でない")]
    [InlineData("; @lib", "@lib が空")]
    [InlineData("; @lib FOO LIB", "1 つの名前でない")]
    [InlineData("; @alias FOO-2", "1 つの名前でない")]
    [InlineData("; @param_types flaot,float", "int か float")]
    [InlineData("; @param_types float,", "int か float")]
    [InlineData("; @param_types", "@param_types が空")]
    [InlineData("; @result_type flaot", "int か float")]
    [InlineData("; @calls X1WORK で依存を満たす", "名前の形でない")]
    [InlineData("; @calls FOO BAR", "名前の形でない")]
    [InlineData("; @calls FOO,,BAR", "名前の形でない")]
    [InlineData("; @calls", "@calls が空")]
    [InlineData("; @requires FOO BAR", "名前の形でない")]
    [InlineData("; @stack_cleanup calle", "callee だけ")]
    [InlineData("; @works BUF:2147483648", "大きすぎる")]
    [InlineData("; @param_count 2147483648", "10 進")]
    [InlineData("; @works_align 2147483648", "10 進")]
    [InlineData("; @works_align 257", "2 の冪")]
    [InlineData("; @works_align 0", "2 の冪")]
    [InlineData("; @works BUF:16,,NEXT:2", "label:size")]
    [InlineData("; @works BUF:16,", "label:size")]
    [InlineData("; @param_types float,int", "float か float,float だけ")]
    [InlineData("; @param_types int,float", "float か float,float だけ")]
    [InlineData("; @param_types float,float,float", "float か float,float だけ")]
    [InlineData("; @end_init", "対になる @init_code がない")]
    public void Lint_ReportsMalformedLines(string line, string expected)
    {
        var problems = Lint("; @name FOO\n" + line + "\nRET\n", "t.asm");
        var p = Assert.Single(problems);
        Assert.Contains("t.asm:2:", p);
        Assert.Contains(expected, p);
    }

    [Theory]
    [InlineData("; @name A\n; @init_code\nLD A,1\n; @name B\nRET\n", "t.asm:2:", "閉じていない")]
    [InlineData("; @name A\n; @init_code\nLD A,1\nRET\n", "t.asm:2:", "閉じていない")]
    [InlineData("; @name A\n; @init_code\n; @init_code\n; @end_init\n", "t.asm:3:", "@init_code の中に")]
    [InlineData("; @init_code FOO\n; @name A\n", "t.asm:1:", "@name より前")]
    [InlineData("; @calls FOO\n; @name A\nRET\n", "t.asm:1:", "@name より前")]
    [InlineData("; @name A\n; @param_count 1\n; @param_types float,float\nRET\n", "t.asm:3:", "@param_count 1 と違う")]
    public void Lint_ReportsMalformedFunctions(string text, string at, string expected)
    {
        var p = Assert.Single(Lint(text, "t.asm"));
        Assert.Contains(at, p);
        Assert.Contains(expected, p);
    }

    [Fact]
    public void Lint_ReportsValueOnInitMarker()
    {
        var p = Assert.Single(Lint("; @name A\n; @init_code FOO\nLD A,1\n; @end_init\nRET\n", "t.asm"));
        Assert.Contains("値は書かない", p);
    }
}
