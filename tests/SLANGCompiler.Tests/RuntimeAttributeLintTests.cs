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

    /// <summary>1 つのファイルの中身を調べ、形の悪い行を「ファイル:行: 内容」の形で返す</summary>
    internal static List<string> Lint(string text, string path)
    {
        var problems = new List<string>();
        var lines = text.Split('\n');
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
            switch (key)
            {
                case "name":
                    if (value.Length == 0)
                        problems.Add($"{where} (@name が空)");
                    break;
                case "stack_cleanup":
                    if (value != "callee")
                        problems.Add($"{where} (@stack_cleanup は callee だけ)");
                    break;
                case "works":
                    var items = value.Split(',', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries);
                    if (items.Length == 0)
                        problems.Add($"{where} (@works が空)");
                    foreach (var item in items)
                    {
                        if (!WorksItem.IsMatch(item))
                            problems.Add($"{where} (@works の項目 '{item}' が label:size の形でない)");
                        else if (!int.TryParse(item[(item.IndexOf(':') + 1)..], out _))
                            problems.Add($"{where} (@works の項目 '{item}' の大きさが大きすぎる)");
                    }
                    break;
                case "param_count":
                case "works_align":
                    if (!Number.IsMatch(value) || !int.TryParse(value, out _))
                        problems.Add($"{where} (@{key} が 10 進の数でない)");
                    break;
                case "resident":
                    if (value != "shared" && value != "local")
                        problems.Add($"{where} (@resident は shared か local)");
                    break;
            }
        }
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
            + "; @works_align 256\n; @resident local\n; @lib FOOLIB\n; @stack_cleanup callee\nRET\n; ふつうの注釈 @works ではない\n";
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
    [InlineData("; @stack_cleanup calle", "callee だけ")]
    [InlineData("; @works BUF:2147483648", "大きすぎる")]
    [InlineData("; @param_count 2147483648", "10 進")]
    [InlineData("; @works_align 2147483648", "10 進")]
    public void Lint_ReportsMalformedLines(string line, string expected)
    {
        var problems = Lint("; @name FOO\n" + line + "\nRET\n", "t.asm");
        var p = Assert.Single(problems);
        Assert.Contains("t.asm:2:", p);
        Assert.Contains(expected, p);
    }
}
