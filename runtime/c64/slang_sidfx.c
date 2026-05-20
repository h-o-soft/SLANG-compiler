/*
 * SLANG SFX overlay bridge for C64 (oscar64 audio/sidfx).
 * See slang_sidfx.h for API contract.
 *
 * oscar64 <audio/sidfx.h> の declaration を `#include` で参照、 実体
 * (audio/sidfx.c、 GPL-3.0) は user の oscar64 install からリンク時に解決。
 * SLANG MIT runtime には GPL コードを borrow しない方針 (= sprite/joystick/kio
 * と同じ規律)。
 */

#include "slang_sidfx.h"
#include <audio/sidfx.h>

void slang_sidfx_init(void)
{
    sidfx_init();
}

void slang_sidfx_play(unsigned char chn, unsigned char *fx, unsigned char cnt)
{
    if (chn < 3)
        sidfx_play(chn, (const struct SIDFX *)fx, cnt);
}

void slang_sidfx_stop(unsigned char chn)
{
    if (chn < 3)
        sidfx_stop(chn);
}

unsigned int slang_sidfx_idle(unsigned char chn)
{
    if (chn >= 3) return 0;
    return sidfx_idle(chn) ? 1 : 0;
}

unsigned int slang_sidfx_cnt(unsigned char chn)
{
    if (chn >= 3) return 0;
    return (unsigned int)(unsigned char)sidfx_cnt(chn);
}

void slang_sidfx_loop(void)
{
    sidfx_loop();
}

void slang_sidfx_loop_2(void)
{
    sidfx_loop_2();
}
