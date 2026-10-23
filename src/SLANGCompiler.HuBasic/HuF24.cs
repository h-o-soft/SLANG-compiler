namespace SLANGCompiler.HuBasic;

/// <summary>浮動小数 (SLANG の FLOAT、f24) と整数の定数の扱いで、解析と出力の両方が使うもの</summary>
public static class HuF24
{
    /// <summary>
    /// double を、SLANG が FLOAT の定数を f24 に直したときの値にする (SLANG の LabelUtils.ConvertToF24 と同じ丸め:
    /// 仮数 16 ビットで四捨五入、2^-62 未満は 0、2^64 以上は無限大)。
    /// </summary>
    public static double Quantize(double v)
    {
        if (v == 0 || double.IsNaN(v) || double.IsInfinity(v)) return v;
        double sign = v < 0 ? -1 : 1;
        double m = Math.Abs(v);
        int exp = 0;
        while (m < 1) { exp--; m += m; }
        while (m >= 2) { exp++; m /= 2; }
        if (exp > 63) return sign * double.PositiveInfinity;
        if (exp < -62) return 0;
        double q = Math.Floor((m - 1.0) * 65536 + 0.5);
        if (q >= 65536)
        {
            // 丸めで仮数が繰り上がる (SLANG も指数を 1 上げる。指数が上限を超えれば無限大)
            q = 0;
            exp++;
            if (exp > 63) return sign * double.PositiveInfinity;
        }
        return sign * (1.0 + q / 65536) * Math.Pow(2, exp);
    }

    /// <summary>
    /// 10 進の数の綴り (符号なし。数字・小数点・指数 E/D、後ろの ! # は無視) を、ランタイムの BAS_SVALF と同じ読み方で
    /// f24 の値にする: 有効数字は 9 桁まで (それより下の桁は捨てる。整数部の捨てた桁は 10 倍に数える)、
    /// その 10 進の値から f24 へ 1 回だけ四捨五入する。定数と INPUT で読んだ値が必ず同じになる。
    /// 読めなければ null。
    /// </summary>
    public static double? ParseDecimal(string text)
    {
        var t = text.TrimEnd('!', '#');
        int i = 0, nd = 0;
        long x = 0;
        var m = System.Numerics.BigInteger.Zero;
        bool any = false;
        for (; i < t.Length && char.IsAsciiDigit(t[i]); i++)
        {
            any = true;
            if (nd < 9) { m = m * 10 + (t[i] - '0'); if (!m.IsZero) nd++; }
            else x++;
        }
        if (i < t.Length && t[i] == '.')
        {
            for (i++; i < t.Length && char.IsAsciiDigit(t[i]); i++)
            {
                any = true;
                if (nd < 9) { m = m * 10 + (t[i] - '0'); x--; if (!m.IsZero) nd++; }
            }
        }
        if (!any) return null;
        if (i < t.Length && "EeDd".Contains(t[i]))
        {
            i++;
            bool neg = false;
            if (i < t.Length && (t[i] == '+' || t[i] == '-')) { neg = t[i] == '-'; i++; }
            long e = 0;
            for (; i < t.Length && char.IsAsciiDigit(t[i]); i++) e = Math.Min(e * 10 + (t[i] - '0'), 999);
            x += neg ? -e : e;
        }
        if (m.IsZero) return 0;
        // 値 = m × 10^x を num / den の有理数で
        var num = x >= 0 ? m * System.Numerics.BigInteger.Pow(10, (int)x) : m;
        var den = x >= 0 ? System.Numerics.BigInteger.One : System.Numerics.BigInteger.Pow(10, (int)-x);
        // 2^e <= 値 < 2^(e+1) となる e
        int exp = (int)(num.GetBitLength() - den.GetBitLength());
        if (Compare(num, den, exp) < 0) exp--;
        if (Compare(num, den, exp + 1) >= 0) exp++;
        // 仮数 = floor((値 / 2^e - 1) × 65536 + 1/2) = floor(値 × 2^(17-e) / 2 + 1/2) の形で整数だけで計算する
        var scaledNum = num * 2;
        var scaledDen = den;
        int sh = 16 - exp;
        if (sh >= 0) scaledNum <<= sh; else scaledDen <<= -sh;
        var q = (scaledNum + scaledDen) / (scaledDen * 2);   // floor(値 × 2^(16-e) + 1/2)
        var mant = q - 65536;
        if (mant >= 65536) { mant = 0; exp++; }
        if (exp > 63) return double.PositiveInfinity;
        if (exp < -62) return 0;
        return (1.0 + (double)mant / 65536) * Math.Pow(2, exp);
    }

    /// <summary>num / den と 2^e を比べる</summary>
    private static int Compare(System.Numerics.BigInteger num, System.Numerics.BigInteger den, int e)
        => e >= 0 ? num.CompareTo(den << e) : (num << -e).CompareTo(den);

    /// <summary>f24 で表せる最大の大きさ (2^63 × (1 + 65535/65536)、約 1.8E+19)</summary>
    public static readonly double MaxFinite = (1.0 + 65535.0 / 65536) * Math.Pow(2, 63);

    /// <summary>f24 に直すと無限大になるか (表せる範囲を超えるか)</summary>
    public static bool Overflows(double v) => double.IsInfinity(Quantize(v));

    /// <summary>整数の定数の値 (構文木では 0〜65535 のビットの並び) を、16 ビットの符号付きの値にする</summary>
    public static int SignedInt(int bits) => unchecked((short)bits);
}
