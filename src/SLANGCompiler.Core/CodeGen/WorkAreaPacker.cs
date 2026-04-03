namespace SLANGCompiler.CodeGen;

/// <summary>
/// ワークメモリのアライン対応ブロック配置（メモリテトリス）。
///
/// ランタイムの @works は1行が連続配置を前提としたブロック単位。
/// @works_align N でブロック先頭を N バイト境界に配置。
///
/// アルゴリズム:
///   1. align付きブロックをアライン値の降順 → サイズ降順でソート
///   2. align付きブロックを順に境界配置。末尾〜次の境界までの隙間を記録
///   3. alignなしブロックをサイズ降順でソート
///   4. 隙間にフィットするものを詰め込む (First Fit Decreasing)
///   5. 残りは末尾に積む
/// </summary>
public class WorkAreaPacker
{
    /// <summary>配置されたブロック1つ</summary>
    public class PlacedBlock
    {
        public List<(string Label, int Size)> Items { get; set; } = new();
        public string? LibName { get; set; }
        public int Alignment { get; set; }
        public int TotalSize => Items.Sum(i => i.Size);
        public int Offset { get; set; }   // __WORK__からの相対オフセット
    }

    /// <summary>
    /// ブロック群を配置して、各ラベルのオフセットを確定する。
    /// 戻り値: (配置済みブロックリスト, 合計使用サイズ)
    /// </summary>
    public (List<PlacedBlock> Placed, int TotalSize) Pack(List<PlacedBlock> blocks, int baseOffset = 0)
    {
        if (blocks.Count == 0)
            return (new List<PlacedBlock>(), baseOffset);

        // align付きとなしに分離
        var aligned = blocks.Where(b => b.Alignment > 1).OrderByDescending(b => b.Alignment).ThenByDescending(b => b.TotalSize).ToList();
        var unaligned = blocks.Where(b => b.Alignment <= 1).OrderByDescending(b => b.TotalSize).ToList();

        // 配置結果
        var placed = new List<PlacedBlock>();
        // 隙間リスト: (offset, size)
        var gaps = new List<(int Offset, int Size)>();

        int cursor = baseOffset;

        // Phase 1: align付きブロックを境界に配置
        foreach (var block in aligned)
        {
            int alignedOffset = AlignUp(cursor, block.Alignment);

            // cursor〜alignedOffsetの間に隙間が生じる
            if (alignedOffset > cursor)
            {
                gaps.Add((cursor, alignedOffset - cursor));
            }

            block.Offset = alignedOffset;
            placed.Add(block);
            cursor = alignedOffset + block.TotalSize;

            // ブロック末尾〜次の境界までの隙間も記録
            int nextBoundary = AlignUp(cursor, block.Alignment);
            if (nextBoundary > cursor)
            {
                gaps.Add((cursor, nextBoundary - cursor));
                cursor = nextBoundary;
            }
        }

        // Phase 2: 隙間にalignなしブロックを詰め込む (First Fit Decreasing)
        var remaining = new List<PlacedBlock>();
        // 隙間をオフセット順にソート
        gaps.Sort((a, b) => a.Offset.CompareTo(b.Offset));

        foreach (var block in unaligned)
        {
            bool fitted = false;
            for (int i = 0; i < gaps.Count; i++)
            {
                var (gapOffset, gapSize) = gaps[i];
                if (block.TotalSize <= gapSize)
                {
                    // フィット！
                    block.Offset = gapOffset;
                    placed.Add(block);

                    // 隙間を縮小
                    int newGapOffset = gapOffset + block.TotalSize;
                    int newGapSize = gapSize - block.TotalSize;
                    if (newGapSize > 0)
                        gaps[i] = (newGapOffset, newGapSize);
                    else
                        gaps.RemoveAt(i);

                    fitted = true;
                    break;
                }
            }
            if (!fitted)
                remaining.Add(block);
        }

        // Phase 3: 残りを末尾に積む
        foreach (var block in remaining)
        {
            block.Offset = cursor;
            placed.Add(block);
            cursor += block.TotalSize;
        }

        // オフセット順にソート（出力時の見やすさ）
        placed.Sort((a, b) => a.Offset.CompareTo(b.Offset));

        return (placed, cursor);
    }

    private static int AlignUp(int value, int alignment)
    {
        if (alignment <= 1) return value;
        return (value + alignment - 1) / alignment * alignment;
    }
}
