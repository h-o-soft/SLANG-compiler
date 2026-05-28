using System.Buffers.Binary;

namespace SLANGCompiler.SlfsPack;

/// <summary>
/// D88 disk image binary を構築する writer。
/// 入力 = geometry + 各 sector の data byte array (= logical sector index 順)。
/// 出力 = D88 image byte[]。
/// </summary>
public sealed class D88Writer
{
    private readonly D88Format.Geometry _geom;
    private readonly byte[][] _sectors;
    private string _diskName = "";

    public D88Writer(D88Format.Geometry geometry)
    {
        _geom = geometry ?? throw new ArgumentNullException(nameof(geometry));
        _sectors = new byte[_geom.LogicalSectorCount][];
        // default 全 sector 0 fill (= 未書込 sector は 0)
        for (int i = 0; i < _sectors.Length; i++)
            _sectors[i] = new byte[_geom.SectorSize];
    }

    public D88Writer SetDiskName(string name) { _diskName = name ?? ""; return this; }

    /// <summary>logical sector index を data で埋める (= data は SectorSize byte 必須)</summary>
    public D88Writer WriteSector(int lsec, byte[] data)
    {
        if (data == null) throw new ArgumentNullException(nameof(data));
        if (data.Length != _geom.SectorSize)
            throw new ArgumentException($"sector data size mismatch (= expected {_geom.SectorSize}, got {data.Length})");
        if (lsec < 0 || lsec >= _sectors.Length)
            throw new ArgumentOutOfRangeException(nameof(lsec));
        _sectors[lsec] = (byte[])data.Clone();
        return this;
    }

    /// <summary>D88 image binary を構築して返す</summary>
    public byte[] Build()
    {
        int trackBlockSize = _geom.SectorsPerTrack * (D88Format.SectorHeaderSize + _geom.SectorSize);
        int totalSize = D88Format.DataAreaOffset + _geom.TotalTrackEntries * trackBlockSize;
        var img = new byte[totalSize];

        // D88 header
        // disk name (16 byte ASCII null-padded)
        var nameBytes = System.Text.Encoding.ASCII.GetBytes(_diskName);
        int nameCopyLen = Math.Min(nameBytes.Length, 16);
        Array.Copy(nameBytes, 0, img, 0, nameCopyLen);
        // +1A write_protect = 0 (write OK)
        // +1B media_type
        img[0x1B] = _geom.MediaType;
        // +1C disk_size (4 byte LE)
        BinaryPrimitives.WriteUInt32LittleEndian(img.AsSpan(0x1C, 4), (uint)totalSize);

        // track offset table + track data
        int curOffset = D88Format.DataAreaOffset;
        byte sizeCode = D88Format.CodeFromSectorSize(_geom.SectorSize);

        for (int cyl = 0; cyl < _geom.TotalCylinders; cyl++)
        {
            for (int head = 0; head < _geom.Sides; head++)
            {
                int trackIdx = _geom.TrackTableIndex(cyl, head);
                int tableOffset = D88Format.TrackOffsetTableOffset + trackIdx * 4;
                BinaryPrimitives.WriteUInt32LittleEndian(img.AsSpan(tableOffset, 4), (uint)curOffset);

                for (int sec = 1; sec <= _geom.SectorsPerTrack; sec++)
                {
                    int lsec = cyl * _geom.Sides * _geom.SectorsPerTrack
                             + head * _geom.SectorsPerTrack
                             + (sec - 1);
                    var data = _sectors[lsec];

                    // sector header (16 byte)
                    img[curOffset + 0] = (byte)cyl;
                    img[curOffset + 1] = (byte)head;
                    img[curOffset + 2] = (byte)sec;
                    img[curOffset + 3] = sizeCode;
                    BinaryPrimitives.WriteUInt16LittleEndian(img.AsSpan(curOffset + 4, 2),
                                                              (ushort)_geom.SectorsPerTrack);
                    // +06 density = 0 (DD)
                    // +07 DDM = 0 (normal)
                    // +08 status = 0 (OK)
                    // +09..+0D reserved = 0
                    BinaryPrimitives.WriteUInt16LittleEndian(img.AsSpan(curOffset + 0x0E, 2),
                                                              (ushort)_geom.SectorSize);

                    // sector data
                    Array.Copy(data, 0, img, curOffset + D88Format.SectorHeaderSize, _geom.SectorSize);

                    curOffset += D88Format.SectorHeaderSize + _geom.SectorSize;
                }
            }
        }

        return img;
    }
}
