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
        return sign * (1.0 + q / 65536) * Math.Pow(2, exp);
    }

    /// <summary>整数の定数の値 (構文木では 0〜65535 のビットの並び) を、16 ビットの符号付きの値にする</summary>
    public static int SignedInt(int bits) => unchecked((short)bits);
}
