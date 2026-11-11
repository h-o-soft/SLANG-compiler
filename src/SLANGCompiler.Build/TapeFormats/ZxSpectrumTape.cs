using System.Text;

namespace SLANGCompiler.Build.TapeFormats;

/// <summary>
/// 素の ZX Spectrum のテープ (TAP と TZX)。BASIC の読み込み口と CODE の 4 ブロック:
///   1. BASIC のヘッダ (型 0、自動で走る行 10)
///   2. BASIC の本体: <c>10 CLEAR VAL "org-1":LOAD ""CODE :RANDOMIZE USR VAL "org"</c>
///   3. CODE のヘッダ (型 3、読み込む番地 org)
///   4. CODE の本体 (bin)
/// 各ブロックは flag (ヘッダ 00H・本体 FFH) + 中身 + XOR のチェックサム。
/// TAP は各ブロックの頭に 2 バイトの長さ。TZX は頭 "ZXTape!" 1AH 1.20 の後、各ブロックを ID 10H (標準の速さ) で包む。
/// </summary>
public static class ZxSpectrumTape
{
    /// <summary>TZX の各ブロックの後の休み (ms)</summary>
    private const int TzxPauseMs = 1000;

    /// <summary>テープの名前 (10 文字まで、大文字)。ASCII の印字できる字の外はエラー</summary>
    public static string NormalizeName(string name)
    {
        var upper = name.ToUpperInvariant();
        if (upper.Length == 0 || upper.Any(c => c < 0x20 || c > 0x7E))
            throw new ArgumentException($"tape name must be printable ASCII: '{name}'");
        return upper.Length > 10 ? upper[..10] : upper;
    }

    public static List<byte[]> Blocks(byte[] code, int org, string name)
    {
        if (org < 0x4000 || org + code.Length > 0x10000)
            throw new ArgumentException($"code ${org:X4}+{code.Length} does not fit 4000H-FFFFH");
        var basic = BasicLoader(org);
        return new List<byte[]>
        {
            Header(0, name, basic.Length, 10, basic.Length),
            Block(0xFF, basic),
            Header(3, name, code.Length, org, 32768),
            Block(0xFF, code),
        };
    }

    /// <summary>10 CLEAR VAL "org-1":LOAD ""CODE :RANDOMIZE USR VAL "org" (数は VAL "…" で、5 バイトの数の形を使わない)</summary>
    public static byte[] BasicLoader(int org)
    {
        var body = new List<byte> { 0xFD, 0xB0 };                       // CLEAR VAL
        body.AddRange(Encoding.ASCII.GetBytes($"\"{org - 1}\":"));
        body.AddRange(new byte[] { 0xEF, 0x22, 0x22, 0xAF, 0x3A, 0xF9, 0xC0, 0xB0 });  // LOAD ""CODE :RANDOMIZE USR VAL
        body.AddRange(Encoding.ASCII.GetBytes($"\"{org}\""));
        body.Add(0x0D);
        var line = new List<byte> { 0x00, 10, (byte)body.Count, (byte)(body.Count >> 8) };   // 行番号は上位が先
        line.AddRange(body);
        return line.ToArray();
    }

    private static byte[] Header(int type, string name, int length, int param1, int param2)
    {
        var h = new List<byte> { (byte)type };
        h.AddRange(Encoding.ASCII.GetBytes(name.PadRight(10)));
        foreach (var v in new[] { length, param1, param2 })
        {
            h.Add((byte)v);
            h.Add((byte)(v >> 8));
        }
        return Block(0x00, h.ToArray());
    }

    private static byte[] Block(byte flag, byte[] data)
    {
        var b = new byte[data.Length + 2];
        b[0] = flag;
        data.CopyTo(b, 1);
        byte x = 0;
        for (int i = 0; i < b.Length - 1; i++) x ^= b[i];
        b[^1] = x;
        return b;
    }

    public static byte[] Tap(List<byte[]> blocks)
    {
        var ms = new MemoryStream();
        foreach (var b in blocks)
        {
            ms.WriteByte((byte)b.Length);
            ms.WriteByte((byte)(b.Length >> 8));
            ms.Write(b);
        }
        return ms.ToArray();
    }

    public static byte[] Tzx(List<byte[]> blocks)
    {
        var ms = new MemoryStream();
        ms.Write(Encoding.ASCII.GetBytes("ZXTape!"));
        ms.Write(new byte[] { 0x1A, 1, 20 });
        foreach (var b in blocks)
        {
            ms.WriteByte(0x10);
            ms.WriteByte(TzxPauseMs & 0xFF);
            ms.WriteByte(TzxPauseMs >> 8);
            ms.WriteByte((byte)b.Length);
            ms.WriteByte((byte)(b.Length >> 8));
            ms.Write(b);
        }
        return ms.ToArray();
    }
}
