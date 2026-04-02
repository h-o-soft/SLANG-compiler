using SLANGCompiler.IR;

namespace SLANGCompiler.CodeGen;

/// <summary>
/// Z80レジスタ
/// </summary>
public enum Z80Reg
{
    A, B, C, D, E, H, L,
    AF, BC, DE, HL, SP, IX, IY,
}

/// <summary>
/// Z80アセンブリ出力を構築する
/// </summary>
public class Z80Emitter
{
    private readonly List<string> _lines = new();

    public IReadOnlyList<string> Lines => _lines;

    public void Label(string name)
    {
        _lines.Add($"{name}:");
    }

    public void Instruction(string mnemonic, string? operands = null)
    {
        if (operands != null)
            _lines.Add($"\t{mnemonic}\t{operands}");
        else
            _lines.Add($"\t{mnemonic}");
    }

    public void Comment(string text)
    {
        _lines.Add($"; {text}");
    }

    public void Blank()
    {
        _lines.Add("");
    }

    public void Raw(string line)
    {
        _lines.Add(line);
    }

    public void Org(int address)
    {
        _lines.Add($"\tORG\t${address:X4}");
    }

    public void DefByte(params int[] values)
    {
        _lines.Add($"\tDB\t{string.Join(",", values.Select(v => $"${v:X2}"))}");
    }

    public void DefWord(params int[] values)
    {
        _lines.Add($"\tDW\t{string.Join(",", values.Select(v => $"${v:X4}"))}");
    }

    public void DefString(string text)
    {
        // TODO: proper string encoding
        _lines.Add($"\tDB\t\"{text}\",0");
    }

    public void AppendFrom(Z80Emitter other)
    {
        _lines.AddRange(other._lines);
    }

    public void OptimizeWith(PeepholeOptimizer optimizer)
    {
        var optimized = optimizer.Optimize(_lines);
        _lines.Clear();
        _lines.AddRange(optimized);
    }

    public string ToAssembly()
    {
        return string.Join("\n", _lines);
    }

    public void WriteTo(TextWriter writer)
    {
        foreach (var line in _lines)
        {
            writer.WriteLine(line);
        }
    }
}
