namespace SLANGCompiler.HuBasic;

/// <summary>
/// x1native / x1native_slfs のプログラムの置き場と、組み立て後の大きさの検査。CLI の --check-bin (と --emit) と、
/// ブラウザから呼ぶ口が同じ判定と文を使う。
/// </summary>
internal static class HuLayout
{
    /// <summary>プログラムの配置先</summary>
    public const int LoadAddress = 0x1000;
    /// <summary>割り込み用に予約された領域の先頭</summary>
    public const int ReservedStart = 0xFFE0;

    /// <summary>
    /// bin の大きさと作業領域の終わり (__WORKEND__) から、配置先から予約領域の手前に収まるかを見る。
    /// 文は CLI がそのまま出す 1 行 (収まれば stdout、収まらなければ stderr)
    /// </summary>
    public static (bool Fits, string Message) Check(long binLength, long workEnd)
    {
        long binEnd = LoadAddress + binLength;
        long end = Math.Max(binEnd, workEnd);
        if (end > ReservedStart)
            return (false,
                $"hubasic2sl: error HB0312: プログラムの終わり ${end:X4} (bin ${binEnd:X4}、作業領域 ${workEnd:X4}) が " +
                $"予約領域 ${ReservedStart:X4} を越えています ({end - ReservedStart} バイト超過)");
        return (true, $"hubasic2sl: ${LoadAddress:X4}-${end - 1:X4} に収まります " +
                      $"(bin ${binEnd:X4}、作業領域 ${workEnd:X4}、残り {ReservedStart - end} バイト)");
    }
}
