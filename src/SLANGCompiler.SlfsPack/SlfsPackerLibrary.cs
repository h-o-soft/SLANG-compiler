namespace SLANGCompiler.SlfsPack;

/// <summary>
/// SLFS packer の本体 (= shared library)。 slangbuild DiskImageBuilder から直接呼出。
///
/// layout:
///   sector 0           = HuBASIC IPL header (= SlfsBootHeader 32 byte) + 残り 0 fill
///   sector 1           = SLFS superblock (= "SLFS" magic + fields)
///   sector 2..N        = directory entries (= 16 byte × asset count、 16 entry / sector)
///   sector M..K        = main program (= packer 計算で配置、 disk_offset = M * 256)
///   sector K+1..       = asset data 連続配置 (= sorted by filename)
///   sector 後半        = save area (= Phase 2 以降、 free area)
/// </summary>
public sealed class SlfsPackerLibrary
{
    public sealed class AssetEntry
    {
        public string Name { get; set; } = "";   // up to 11 char
        public byte[] Data { get; set; } = Array.Empty<byte>();
        public byte Type { get; set; } = 0;
    }

    public sealed class Options
    {
        public byte[] MainBinary { get; set; } = Array.Empty<byte>();
        public ushort MainLoadAddress { get; set; } = 0x1000;
        public ushort MainExecuteAddress { get; set; } = 0x1000;
        public string MainFileName { get; set; } = "SLFSMAIN";
        public string VolumeName { get; set; } = "GAMEDISK";
        public D88Format.Geometry Geometry { get; set; } = D88Format.Geometry.Standard2D;
        public List<AssetEntry> Assets { get; set; } = new();

        // save area (= Phase 2 で使用、 Phase 1 では trailing area として確保のみ)
        public int SaveSectorCount { get; set; } = 64;  // default 16 KB
    }

    public const int MaxAssetCount = 256;          // ID 0..255 (= L 8-bit)
    public const int MaxAssetByteSize = 0xFFFF;    // 65535 byte

    private readonly Options _opts;

    public SlfsPackerLibrary(Options opts)
    {
        _opts = opts ?? throw new ArgumentNullException(nameof(opts));
        if (_opts.MainBinary.Length == 0)
            throw new ArgumentException("MainBinary is empty");
        if (_opts.MainBinary.Length > 0xFFFF)
            throw new ArgumentException($"MainBinary too large ({_opts.MainBinary.Length} > 65535)");
        if (_opts.Assets.Count > MaxAssetCount)
            throw new ArgumentException($"too many assets ({_opts.Assets.Count} > {MaxAssetCount}, Phase 1 ID 0..255)");
        foreach (var a in _opts.Assets)
        {
            if (a.Data.Length == 0)
                throw new ArgumentException($"asset '{a.Name}' is empty (= byte_size = 0 invalid)");
            if (a.Data.Length > MaxAssetByteSize)
                throw new ArgumentException($"asset '{a.Name}' too large ({a.Data.Length} > {MaxAssetByteSize})");
        }
    }

    /// <summary>asset list を normalized filename で sort + collision check (= public、 test 用)</summary>
    public List<AssetEntry> SortedAssets()
    {
        var sorted = _opts.Assets
            .Select(a => (Asset: a, NormName: SlfsDirEntry.NormalizeFileName(a.Name)))
            .OrderBy(x => x.NormName, Comparer<byte[]>.Create(SlfsDirEntry.CompareNormalizedFileName))
            .ToList();
        // collision check
        for (int i = 1; i < sorted.Count; i++)
        {
            if (SlfsDirEntry.CompareNormalizedFileName(sorted[i - 1].NormName, sorted[i].NormName) == 0)
                throw new InvalidOperationException(
                    $"asset filename collision (= 11 byte normalized): '{sorted[i - 1].Asset.Name}' and '{sorted[i].Asset.Name}'");
        }
        return sorted.Select(x => x.Asset).ToList();
    }

    /// <summary>D88 image bytes を構築して返す</summary>
    public byte[] Build()
    {
        int sectorSize = _opts.Geometry.SectorSize;
        var sortedAssets = SortedAssets();
        int assetCount = sortedAssets.Count;

        // layout 計算
        //   sector 0: boot header
        //   sector 1: superblock
        //   sector 2..(2 + dirSectorCount - 1): directory
        //   sector (mainStart)..(mainStart + mainSectorCount - 1): main bin
        //   sector (assetStart)..: 各 asset 連続配置
        //   sector (saveStart)..: save area (= 末尾)

        int dirSectorCount = (assetCount + 15) / 16;  // 16 entry / sector
        if (dirSectorCount == 0) dirSectorCount = 1;  // 空 dir も 1 sector 確保

        int dirStartSector = 2;
        int mainStartSector = dirStartSector + dirSectorCount;
        int mainSectorCount = (_opts.MainBinary.Length + sectorSize - 1) / sectorSize;
        int assetStartSector = mainStartSector + mainSectorCount;

        // 各 asset の配置 + dir entry 構築
        var dirEntries = new List<SlfsDirEntry>();
        int curAssetSector = assetStartSector;
        foreach (var a in sortedAssets)
        {
            int sCount = (a.Data.Length + sectorSize - 1) / sectorSize;
            dirEntries.Add(new SlfsDirEntry
            {
                FileName = a.Name,
                Type = a.Type,
                StartSector = (ushort)curAssetSector,
                ByteSize = (ushort)a.Data.Length,
            });
            curAssetSector += sCount;
        }

        int saveStartSector = curAssetSector;
        int totalLogicalSectors = _opts.Geometry.LogicalSectorCount;
        int saveSectorCount = Math.Min(_opts.SaveSectorCount, totalLogicalSectors - saveStartSector);
        if (saveSectorCount < 0) saveSectorCount = 0;

        // 配置 sanity check
        if (saveStartSector + saveSectorCount > totalLogicalSectors)
            throw new InvalidOperationException(
                $"disk image overflow (= main + assets + save = {saveStartSector + saveSectorCount} > {totalLogicalSectors})");

        // D88 image 構築
        var writer = new D88Writer(_opts.Geometry);
        writer.SetDiskName(_opts.VolumeName);

        // sector 0: SlfsBootHeader
        var header = new SlfsBootHeader
        {
            FileName = _opts.MainFileName,
            DataSize = (ushort)_opts.MainBinary.Length,
            LoadAddress = _opts.MainLoadAddress,
            ExecuteAddress = _opts.MainExecuteAddress,
            DiskOffset = mainStartSector * sectorSize,
        };
        var sector0 = new byte[sectorSize];
        header.ToBytes().CopyTo(sector0, 0);
        writer.WriteSector(0, sector0);

        // sector 1: superblock
        var sb = new SlfsSuperblock
        {
            Version = 1,
            Sides = (byte)_opts.Geometry.Sides,
            Tracks = (byte)_opts.Geometry.Tracks,
            SectorsPerTrack = (byte)_opts.Geometry.SectorsPerTrack,
            DirStartSector = (ushort)dirStartSector,
            DirEntryCount = (ushort)assetCount,
            DataAreaStartSector = (ushort)assetStartSector,
            SaveAreaStartSector = (ushort)saveStartSector,
            SaveSectorCount = (ushort)saveSectorCount,
            VolumeName = _opts.VolumeName,
        };
        var sector1 = new byte[sectorSize];
        sb.ToBytes().CopyTo(sector1, 0);
        writer.WriteSector(1, sector1);

        // sector 2..N: directory entries
        for (int dsec = 0; dsec < dirSectorCount; dsec++)
        {
            var dirBuf = new byte[sectorSize];
            for (int i = 0; i < 16; i++)
            {
                int idx = dsec * 16 + i;
                if (idx >= dirEntries.Count) break;
                dirEntries[idx].ToBytes().CopyTo(dirBuf, i * SlfsDirEntry.Size);
            }
            writer.WriteSector(dirStartSector + dsec, dirBuf);
        }

        // main bin 配置
        WriteContinuous(writer, mainStartSector, _opts.MainBinary, sectorSize);

        // asset data 配置
        int aSec = assetStartSector;
        foreach (var a in sortedAssets)
        {
            int sCount = (a.Data.Length + sectorSize - 1) / sectorSize;
            WriteContinuous(writer, aSec, a.Data, sectorSize);
            aSec += sCount;
        }

        return writer.Build();
    }

    private static void WriteContinuous(D88Writer writer, int startSector, byte[] data, int sectorSize)
    {
        int sCount = (data.Length + sectorSize - 1) / sectorSize;
        for (int i = 0; i < sCount; i++)
        {
            var buf = new byte[sectorSize];
            int srcOffset = i * sectorSize;
            int copyLen = Math.Min(sectorSize, data.Length - srcOffset);
            Array.Copy(data, srcOffset, buf, 0, copyLen);
            writer.WriteSector(startSector + i, buf);
        }
    }
}
