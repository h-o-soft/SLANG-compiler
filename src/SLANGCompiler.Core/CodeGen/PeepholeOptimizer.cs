namespace SLANGCompiler.CodeGen;

/// <summary>
/// ピープホール最適化: 生成済みのアセンブリ行を走査して
/// 無駄なコードパターンを除去・簡略化する。
/// </summary>
public class PeepholeOptimizer
{
    /// <summary>
    /// アセンブリ行リストを最適化して返す。
    /// 複数パスで適用（変更がなくなるまで繰り返す）。
    /// </summary>
    public List<string> Optimize(List<string> lines, int maxPasses = 10)
    {
        var result = new List<string>(lines);
        for (int pass = 0; pass < maxPasses; pass++)
        {
            int changes = 0;
            result = ApplyRules(result, ref changes);
            if (changes == 0) break;
        }
        return result;
    }

    private List<string> ApplyRules(List<string> lines, ref int changes)
    {
        var result = new List<string>();

        for (int i = 0; i < lines.Count; i++)
        {
            var line = lines[i].Trim();
            var next = i + 1 < lines.Count ? lines[i + 1].Trim() : "";

            // Rule 1: PUSH HL / POP HL → (削除)
            if (line == "PUSH\tHL" && next == "POP\tHL")
            {
                i++; changes++; continue;
            }
            // Rule 2: PUSH HL / POP DE → EX DE,HL (not always valid, but common pattern)
            // Actually this is the common spill pattern. Keep as-is for now since
            // it's semantically different.

            // Rule 3: PUSH DE / POP DE → (削除)
            if (line == "PUSH\tDE" && next == "POP\tDE")
            {
                i++; changes++; continue;
            }

            // Rule 4: LD HL,xxxx / PUSH HL / POP DE → LD DE,xxxx
            if (line.StartsWith("LD\tHL,") && next == "PUSH\tHL"
                && i + 2 < lines.Count && lines[i + 2].Trim() == "POP\tDE")
            {
                var operand = line[6..]; // after "LD\tHL,"
                result.Add($"\tLD\tDE,{operand}");
                i += 2; changes++; continue;
            }

            // Rule 5: EX DE,HL / EX DE,HL → (削除、二重交換は無操作)
            if (line == "EX\tDE,HL" && next == "EX\tDE,HL")
            {
                i++; changes++; continue;
            }

            // Rule 6: JP label / label: → label: (直後へのジャンプは不要)
            if (line.StartsWith("JP\t") && !line.Contains(","))
            {
                var target = line[3..].Trim();
                if (next == $"{target}:")
                {
                    changes++; continue; // JPを削除、ラベルは残す
                }
            }

            // Rule 7: LD DE,xxxx / PUSH DE / POP HL → LD HL,xxxx
            if (line.StartsWith("LD\tDE,") && next == "PUSH\tDE"
                && i + 2 < lines.Count && lines[i + 2].Trim() == "POP\tHL")
            {
                var operand = line[6..];
                result.Add($"\tLD\tHL,{operand}");
                i += 2; changes++; continue;
            }

            // Rule 8: PUSH HL / POP DE / EX DE,HL / ADD HL,DE → ADD HL,HL
            // (×2パターン: HL値を自身に加算)
            if (line == "PUSH\tHL" && next == "POP\tDE"
                && i + 2 < lines.Count && lines[i + 2].Trim() == "EX\tDE,HL"
                && i + 3 < lines.Count && lines[i + 3].Trim() == "ADD\tHL,DE")
            {
                result.Add("\tADD\tHL,HL");
                i += 3; changes++; continue;
            }

            // Rule 9: PUSH HL / POP DE / EX DE,HL → (削除、連続なら無操作)
            if (line == "PUSH\tHL" && next == "POP\tDE"
                && i + 2 < lines.Count && lines[i + 2].Trim() == "EX\tDE,HL")
            {
                i += 2; changes++; continue;
            }

            // Rule 10: POP DE / EX DE,HL / ADD HL,DE → POP DE / ADD HL,DE
            // (POP後のEXは、HL=src1,DE=src2のセットアップだが、ADD HL,DEは可換なので不要)
            if (line == "POP\tDE" && next == "EX\tDE,HL"
                && i + 2 < lines.Count && lines[i + 2].Trim() == "ADD\tHL,DE")
            {
                result.Add(lines[i]); // POP DE
                result.Add($"\tADD\tHL,DE"); // ADD directly
                i += 2; changes++; continue;
            }

            // Rule 11: LD (var),HL / LD HL,(var) → LD (var),HL (不要な再ロード除去)
            if (line.StartsWith("LD\t(") && line.EndsWith(",HL"))
            {
                var varName = line[3..^3]; // "(var)" 部分
                if (next == $"LD\tHL,{varName}")
                {
                    result.Add(lines[i]); // LD (var),HL を残す
                    i++; changes++; continue; // LD HL,(var) をスキップ
                }
            }

            // Rule 12: LD (var),HL / LD HL,(var) 同一変数の再ロード（BYTE変数対応）
            // LD A,L / LD (var),A / LD A,(var) / LD L,A → LD A,L / LD (var),A
            // (BYTEストア後の再ロードを除去 - HLのLにまだ値がある)

            result.Add(lines[i]);
        }

        return result;
    }
}
