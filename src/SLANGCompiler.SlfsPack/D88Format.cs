namespace SLANGCompiler.SlfsPack;

/// <summary>
/// D88 disk image format 定数 + sector header layout。
///
/// layout 概要:
///   +00..+1F (32 byte): D88 header (disk_name 16 + reserved 9 + write_protect 1 + media_type 1 + disk_size 4 LE)
///   +20..+2AF (656 byte): track offset table (164 track × 4 byte LE、 未使用 track は 0)
///   各 track: sector 列 (= 各 sector = 16 byte header + data)
///   sector header:
///     +00 C (cylinder) / +01 H (head) / +02 R (record/sector、 1-origin) / +03 N (size code)
///     +04 sector_count_in_track (2 byte LE) / +06 density (0=DD) / +07 DDM (0=normal)
///     +08 status (0=OK) / +09 reserved 5 byte / +0E sector_data_size (2 byte LE)
/// </summary>
public static class D88Format
{
    public const int D88HeaderSize = 32;
    public const int TrackOffsetTableSize = 164 * 4;       // 656 byte
    public const int TrackOffsetTableOffset = D88HeaderSize; // 0x20
    public const int DataAreaOffset = D88HeaderSize + TrackOffsetTableSize; // 0x2B0
    public const int SectorHeaderSize = 16;
    public const int DefaultSectorSize = 256;

    public const byte MediaType2D = 0x00;
    public const byte MediaType2DD = 0x10;
    public const byte MediaType2HD = 0x20;

    /// <summary>N (sector size code): 0=128, 1=256, 2=512, 3=1024</summary>
    public static int SectorSizeFromCode(byte n) => 128 << n;
    public static byte CodeFromSectorSize(int size) => size switch
    {
        128 => 0,
        256 => 1,
        512 => 2,
        1024 => 3,
        _ => throw new ArgumentException($"unsupported sector size: {size}", nameof(size))
    };

    /// <summary>2D 標準 geometry (= 2 sides × 40 tracks × 16 sectors × 256 bytes = 320 KB)</summary>
    public sealed class Geometry
    {
        public int Sides { get; init; }
        public int Tracks { get; init; }
        public int SectorsPerTrack { get; init; }
        public int SectorSize { get; init; } = DefaultSectorSize;
        public byte MediaType { get; init; }

        public static readonly Geometry Standard2D = new()
        {
            Sides = 2,
            Tracks = 40,
            SectorsPerTrack = 16,
            SectorSize = 256,
            MediaType = MediaType2D
        };

        public int TotalCylinders => Tracks;
        public int TotalTrackEntries => Tracks * Sides; // D88 track table index 数
        public int LogicalSectorCount => Tracks * Sides * SectorsPerTrack;

        /// <summary>logical sector index (= disk 先頭からの 0-origin 連番) → (cyl, head, sector 1-origin)</summary>
        public (int Cyl, int Head, int Sector) LogicalToChs(int lsec)
        {
            if (lsec < 0 || lsec >= LogicalSectorCount)
                throw new ArgumentOutOfRangeException(nameof(lsec));
            int sectorsPerCyl = Sides * SectorsPerTrack;
            int cyl = lsec / sectorsPerCyl;
            int withinCyl = lsec % sectorsPerCyl;
            int head = withinCyl / SectorsPerTrack;
            int sector = (withinCyl % SectorsPerTrack) + 1;
            return (cyl, head, sector);
        }

        /// <summary>D88 track table index (= 2 * cyl + head)</summary>
        public int TrackTableIndex(int cyl, int head) => 2 * cyl + head;
    }
}
