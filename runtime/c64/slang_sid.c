/*
 * SLANG SID bridge for C64 (oscar64).
 * See slang_sid.h for API contract.
 *
 * oscar64 <c64/sid.h> の struct SID memory map (= 0xd400) を直接操作する。
 * 本 bridge の実装はすべて SLANG 側オリジナルで、oscar64 の実装コード
 * (audio/sidfx.c 等) は転載しない運用方針。公開 header の API 参照に限定。
 */

#include "slang_sid.h"
#include <c64/sid.h>

void slang_sid_init_quiet(void)
{
    /* SID register 25 個 (0xD400-0xD418) を全 0 でクリア。
     * 既存の音を止めて安全状態に。 */
    volatile unsigned char *p = (volatile unsigned char *)0xD400;
    for (unsigned char i = 0; i < 25; i++) p[i] = 0;
}

void slang_sid_volume(unsigned char vol)
{
    /* fmodevol register: 下位 4bit = volume、上位 4bit = filter mode。
     * 上位 4bit を保持して下位だけ更新。 */
    sid.fmodevol = (sid.fmodevol & 0xF0) | (vol & 0x0F);
}

void slang_sid_freq(unsigned char voice, unsigned int freq)
{
    if (voice < 3) sid.voices[voice].freq = freq;
}

void slang_sid_pwm(unsigned char voice, unsigned int pwm)
{
    /* pwm register (12-bit、上位 4 bit は HW 側で無視)。 */
    if (voice < 3) sid.voices[voice].pwm = pwm;
}

void slang_sid_adsr(unsigned char voice, unsigned char ad, unsigned char sr)
{
    if (voice < 3) {
        sid.voices[voice].attdec = ad;
        sid.voices[voice].susrel = sr;
    }
}

void slang_sid_ctrl(unsigned char voice, unsigned char ctrl)
{
    if (voice < 3) sid.voices[voice].ctrl = ctrl;
}

void slang_sid_gate_on(unsigned char voice)
{
    if (voice < 3) sid.voices[voice].ctrl |= SID_CTRL_GATE;
}

void slang_sid_gate_off(unsigned char voice)
{
    if (voice < 3) sid.voices[voice].ctrl &= (unsigned char)~SID_CTRL_GATE;
}

void slang_sid_sfx(unsigned char voice, unsigned int freq,
                   unsigned char ad, unsigned char sr, unsigned char waveform)
{
    if (voice < 3) {
        sid.voices[voice].freq = freq;
        sid.voices[voice].attdec = ad;
        sid.voices[voice].susrel = sr;
        /* SID envelope は GATE bit の 0→1 transition で attack を re-trigger
         * するため、既に GATE が立っている voice に再呼出されても確実に
         * attack をやり直せるよう、まず GATE off (= ctrl に waveform のみ) を
         * 書いてから GATE on (= waveform | GATE) を書く 2 段書き込み。 */
        sid.voices[voice].ctrl = (unsigned char)waveform;
        sid.voices[voice].ctrl = (unsigned char)(waveform | SID_CTRL_GATE);
    }
}
