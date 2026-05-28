using System.Buffers.Binary;
using System.Text;

namespace SLANGCompiler.SlfsPack;

/// <summary>
/// SLFS D88 disk image の read 系 helper (= list / extract / info の共通基盤)。
///
/// D88Reader (= 低レベル sector read) + SLFS layout 解釈 (= boot header / superblock /
/// directory entry parse)。 packer の inverse 操作。
/// </summary>
public sealed class SlfsReader
{
    public sealed class ParsedBootHeader
    {
        public byte BootFlag { get; init; }
        public string FileName { get; init; } = "";
        public string Extension { get; init; } = "";
        public ushort DataSize { get; init; }
        public ushort LoadAddress { get; init; }
        public ushort ExecuteAddress { get; init; }
        public int DiskOffset { get; init; }
    }

    public sealed class ParsedSuperblock
    {
        public bool MagicValid { get; init; }
        public byte Version { get; init; }
        public byte Sides { get; init; }
        public byte Tracks { get; init; }
        public byte SectorsPerTrack { get; init; }
        public ushort DirStartSector { get; init; }
        public ushort DirEntryCount { get; init; }
        public ushort DataAreaStartSector { get; init; }
        public ushort SaveAreaStartSector { get; init; }
        public ushort SaveSectorCount { get; init; }
        public string VolumeName { get; init; } = "";
    }

    public sealed class ParsedDirEntry
    {
        public int Id { get; init; }
        public string FileName { get; init; } = "";
        public byte Type { get; init; }
        public ushort StartSector { get; init; }
        public ushort ByteSize { get; init; }
    }

    private readonly D88Reader _d88;
    private readonly D88Format.Geometry _geom;

    public SlfsReader(byte[] image, D88Format.Geometry? geom = null)
    {
        _d88 = new D88Reader(image);
        _geom = geom ?? D88Format.Geometry.Standard2D;
    }

    public static SlfsReader FromFile(string path) => new(File.ReadAllBytes(path));

    /// <summary>boot sector (= logical 0) を parse</summary>
    public ParsedBootHeader ReadBootHeader()
    {
        var s = ReadLogicalSector(0);
        return new ParsedBootHeader
        {
            BootFlag = s[0],
            FileName = Encoding.ASCII.GetString(s, 1, 13).TrimEnd(' ', '\0'),
            Extension = Encoding.ASCII.GetString(s, 0x0E, 3).TrimEnd(' ', '\0'),
            DataSize = BinaryPrimitives.ReadUInt16LittleEndian(s.AsSpan(0x12, 2)),
            LoadAddress = BinaryPrimitives.ReadUInt16LittleEndian(s.AsSpan(0x14, 2)),
            ExecuteAddress = BinaryPrimitives.ReadUInt16LittleEndian(s.AsSpan(0x16, 2)),
            DiskOffset = s[0x1D] | (s[0x1E] << 8) | (s[0x1F] << 16),
        };
    }

    /// <summary>superblock (= logical 1) を parse</summary>
    public ParsedSuperblock ReadSuperblock()
    {
        var s = ReadLogicalSector(1);
        bool magicValid = s[0] == 'S' && s[1] == 'L' && s[2] == 'F' && s[3] == 'S';
        return new ParsedSuperblock
        {
            MagicValid = magicValid,
            Version = s[0x04],
            Sides = s[0x05],
            Tracks = s[0x06],
            SectorsPerTrack = s[0x07],
            DirStartSector = BinaryPrimitives.ReadUInt16LittleEndian(s.AsSpan(0x08, 2)),
            DirEntryCount = BinaryPrimitives.ReadUInt16LittleEndian(s.AsSpan(0x0A, 2)),
            DataAreaStartSector = BinaryPrimitives.ReadUInt16LittleEndian(s.AsSpan(0x0C, 2)),
            SaveAreaStartSector = BinaryPrimitives.ReadUInt16LittleEndian(s.AsSpan(0x0E, 2)),
            SaveSectorCount = BinaryPrimitives.ReadUInt16LittleEndian(s.AsSpan(0x10, 2)),
            VolumeName = Encoding.ASCII.GetString(s, 0x12, 16).TrimEnd(' ', '\0'),
        };
    }

    /// <summary>directory entry 全件を parse (= ID 順 = sorted filename 順)</summary>
    public List<ParsedDirEntry> ReadDirectory()
    {
        var sb = ReadSuperblock();
        if (!sb.MagicValid)
            throw new InvalidOperationException("not a SLFS disk image (= superblock magic mismatch)");

        var result = new List<ParsedDirEntry>(sb.DirEntryCount);
        int dirSectorCount = (sb.DirEntryCount + 15) / 16;
        if (sb.DirEntryCount == 0) return result;
        if (dirSectorCount == 0) dirSectorCount = 1;

        for (int dsec = 0; dsec < dirSectorCount; dsec++)
        {
            var sectorData = ReadLogicalSector(sb.DirStartSector + dsec);
            for (int i = 0; i < 16; i++)
            {
                int id = dsec * 16 + i;
                if (id >= sb.DirEntryCount) break;
                int off = i * SlfsDirEntry.Size;
                result.Add(new ParsedDirEntry
                {
                    Id = id,
                    FileName = Encoding.ASCII.GetString(sectorData, off, 11).TrimEnd(' ', '\0'),
                    Type = sectorData[off + 0x0B],
                    StartSector = BinaryPrimitives.ReadUInt16LittleEndian(sectorData.AsSpan(off + 0x0C, 2)),
                    ByteSize = BinaryPrimitives.ReadUInt16LittleEndian(sectorData.AsSpan(off + 0x0E, 2)),
                });
            }
        }
        return result;
    }

    /// <summary>指定 ID の asset 内容を抽出 (= byte_size に切詰め)</summary>
    public byte[] ExtractAsset(int id)
    {
        var dir = ReadDirectory();
        if (id < 0 || id >= dir.Count)
            throw new ArgumentOutOfRangeException(nameof(id), $"id {id} out of range (0..{dir.Count - 1})");
        return ExtractAssetEntry(dir[id]);
    }

    /// <summary>指定 filename の asset 内容を抽出 (= 11 byte normalized 比較)</summary>
    public byte[] ExtractAssetByName(string name)
    {
        var dir = ReadDirectory();
        var target = SlfsDirEntry.NormalizeFileName(name);
        foreach (var e in dir)
        {
            var n = SlfsDirEntry.NormalizeFileName(e.FileName);
            if (SlfsDirEntry.CompareNormalizedFileName(n, target) == 0)
                return ExtractAssetEntry(e);
        }
        throw new InvalidOperationException($"asset not found: {name}");
    }

    private byte[] ExtractAssetEntry(ParsedDirEntry entry)
    {
        int sectorCount = (entry.ByteSize + _geom.SectorSize - 1) / _geom.SectorSize;
        var result = new byte[entry.ByteSize];
        int written = 0;
        for (int i = 0; i < sectorCount; i++)
        {
            var s = ReadLogicalSector(entry.StartSector + i);
            int copyLen = Math.Min(_geom.SectorSize, entry.ByteSize - written);
            Array.Copy(s, 0, result, written, copyLen);
            written += copyLen;
        }
        return result;
    }

    /// <summary>boot header の disk_offset から main program 本体を抽出 (= byte_size に切詰め)</summary>
    public byte[] ExtractMain()
    {
        var header = ReadBootHeader();
        if (header.BootFlag != 0x01)
            throw new InvalidOperationException($"not a bootable disk (= boot_flag = ${header.BootFlag:X2})");
        int startLsec = header.DiskOffset / _geom.SectorSize;
        int sectorCount = (header.DataSize + _geom.SectorSize - 1) / _geom.SectorSize;
        var result = new byte[header.DataSize];
        int written = 0;
        for (int i = 0; i < sectorCount; i++)
        {
            var s = ReadLogicalSector(startLsec + i);
            int copyLen = Math.Min(_geom.SectorSize, header.DataSize - written);
            Array.Copy(s, 0, result, written, copyLen);
            written += copyLen;
        }
        return result;
    }

    /// <summary>save area の raw dump を抽出 (= sector 単位 offset / count 指定可)</summary>
    public byte[] ExtractSave(int offsetSectors = 0, int? countSectors = null)
    {
        var sb = ReadSuperblock();
        if (!sb.MagicValid)
            throw new InvalidOperationException("not a SLFS disk image");
        int count = countSectors ?? sb.SaveSectorCount;
        if (offsetSectors < 0 || count < 0 || offsetSectors + count > sb.SaveSectorCount)
            throw new ArgumentOutOfRangeException(
                $"save range out of bounds (offset={offsetSectors}, count={count}, available={sb.SaveSectorCount})");
        var result = new byte[count * _geom.SectorSize];
        for (int i = 0; i < count; i++)
        {
            var s = ReadLogicalSector(sb.SaveAreaStartSector + offsetSectors + i);
            Array.Copy(s, 0, result, i * _geom.SectorSize, _geom.SectorSize);
        }
        return result;
    }

    /// <summary>logical sector index → D88Reader.ReadSector</summary>
    private byte[] ReadLogicalSector(int lsec)
    {
        var (cyl, head, sector) = _geom.LogicalToChs(lsec);
        return _d88.ReadSector(cyl, head, sector);
    }
}
