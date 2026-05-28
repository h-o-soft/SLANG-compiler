using System.Buffers.Binary;

namespace SLANGCompiler.SlfsPack;

/// <summary>
/// D88 disk image を読出す test helper / verify 用 reader。
/// ReadSector(cyl, head, sector) で 256 byte sector data を返す。
/// </summary>
public sealed class D88Reader
{
    private readonly byte[] _image;

    public D88Reader(byte[] image)
    {
        _image = image ?? throw new ArgumentNullException(nameof(image));
        if (_image.Length < D88Format.DataAreaOffset)
            throw new ArgumentException("D88 image too short (= header + track table 未満)", nameof(image));
    }

    public static D88Reader FromFile(string path) => new(File.ReadAllBytes(path));

    /// <summary>track table index = 2*cyl + head の disk image 内 offset (= 0 なら track 未使用)</summary>
    public int GetTrackOffset(int trackTableIndex)
    {
        if (trackTableIndex < 0 || trackTableIndex >= 164)
            throw new ArgumentOutOfRangeException(nameof(trackTableIndex));
        int offsetTablePos = D88Format.TrackOffsetTableOffset + trackTableIndex * 4;
        return (int)BinaryPrimitives.ReadUInt32LittleEndian(_image.AsSpan(offsetTablePos, 4));
    }

    /// <summary>指定 sector の 16 byte sector header + data を返す</summary>
    public byte[] ReadSector(int cyl, int head, int sector)
    {
        int trackIdx = 2 * cyl + head;
        int trackOffset = GetTrackOffset(trackIdx);
        if (trackOffset == 0)
            throw new InvalidOperationException($"track {trackIdx} (cyl {cyl} head {head}) is unused (offset = 0)");

        // track 内を sector header walk で目的 sector を探す (= 1-origin)
        int pos = trackOffset;
        while (pos < _image.Length)
        {
            byte hdrC = _image[pos + 0];
            byte hdrH = _image[pos + 1];
            byte hdrR = _image[pos + 2];
            byte hdrN = _image[pos + 3];
            int dataSize = BinaryPrimitives.ReadUInt16LittleEndian(_image.AsSpan(pos + 0x0E, 2));
            if (hdrC == cyl && hdrH == head && hdrR == sector)
            {
                int dataStart = pos + D88Format.SectorHeaderSize;
                var result = new byte[dataSize];
                Array.Copy(_image, dataStart, result, 0, dataSize);
                return result;
            }
            pos += D88Format.SectorHeaderSize + dataSize;
            // 次 track 領域に入る前に sector_count_in_track で打ち切ることもできるが、
            // simplest に walk して見つからなければ throw
        }
        throw new InvalidOperationException($"sector not found: cyl={cyl}, head={head}, sector={sector}");
    }

    /// <summary>D88 header の media_type 取得</summary>
    public byte MediaType => _image[0x1B];

    /// <summary>D88 header の disk_name (16 byte ASCII null-terminated)</summary>
    public string DiskName
    {
        get
        {
            int len = 0;
            while (len < 16 && _image[len] != 0) len++;
            return System.Text.Encoding.ASCII.GetString(_image, 0, len);
        }
    }
}
