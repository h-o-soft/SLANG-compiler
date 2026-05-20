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

/* === HVSC .sid BGM 再生 (v3b-B) === */

/* bridge 内 static に保存する .sid player の init / play address。
 * slang_sid_load_from_buf 成功時にセット、SLANG_PLAYER_INIT/PLAY で参照。 */
static unsigned int g_sid_init_addr = 0;
static unsigned int g_sid_play_addr = 0;

/* PSID v2 magic check (= "PSID" の ASCII bytes、PETSCII 変換と独立)。
 * oscar64 `-psci` 配下では char literal 'P' 等が PETSCII 大文字 (= 0xD0 等の
 * shifted 領域) に変換される挙動があるため、SLANG リテラル形式での比較は
 * 使えない。バイナリ disk file の magic は ASCII で書かれているので、ここは
 * 16 進値直書きで比較する。 */
static unsigned char is_psid_magic(const unsigned char *p)
{
    /* "PSID" = 0x50 0x53 0x49 0x44 (ASCII)、buf 先頭の disk byte と直接比較。 */
    return (unsigned char)(p[0] == 0x50 && p[1] == 0x53
                        && p[2] == 0x49 && p[3] == 0x44);
}

unsigned int slang_sid_load_from_buf(unsigned char *buf, unsigned int len)
{
    /* 最低 header 0x7C + payload 先頭 (loadAddr 0 の場合) 2 byte は欲しい。 */
    if (len < 0x7E) return 0;

    /* magic: PSID v2 のみ accept、RSID は IRQ driven の通常 program 形式で
     * v3b-B の bridge 経由再生と動作が違うため非対応。 */
    if (!is_psid_magic(buf)) return 0;

    /* version は BE WORD で 1 or 2 を許容。v3 以降は未検証なので reject。 */
    unsigned int version = ((unsigned int)buf[0x04] << 8) | buf[0x05];
    if (version < 1 || version > 2) return 0;

    unsigned int data_offset = ((unsigned int)buf[0x06] << 8) | buf[0x07];
    unsigned int load_addr   = ((unsigned int)buf[0x08] << 8) | buf[0x09];
    unsigned int init_addr   = ((unsigned int)buf[0x0A] << 8) | buf[0x0B];
    unsigned int play_addr   = ((unsigned int)buf[0x0C] << 8) | buf[0x0D];

    /* playAddress = 0 は IRQ-driven (RSID 様)、v3b-B 非対応。 */
    if (play_addr == 0) return 0;

    if (data_offset >= len) return 0;

    unsigned char *payload = buf + data_offset;
    unsigned int payload_len = len - data_offset;

    if (load_addr == 0) {
        /* PSID 仕様: header loadAddress = 0 のとき、payload 先頭 2 byte (LE WORD)
         * が実 loadAddress、残りが実 payload。 */
        if (payload_len < 2) return 0;
        load_addr = (unsigned int)payload[0] | ((unsigned int)payload[1] << 8);
        payload += 2;
        payload_len -= 2;
    }

    /* payload を load_addr に memcpy (= C64 64KB address space に直接書込)。 */
    volatile unsigned char *dst = (volatile unsigned char *)load_addr;
    for (unsigned int i = 0; i < payload_len; i++) {
        dst[i] = payload[i];
    }

    g_sid_init_addr = init_addr;
    g_sid_play_addr = play_addr;
    return 1;
}

/* JMP indirect 用 zero page vector slot。
 * $FB-$FE は KERNAL でも BASIC でも未使用の "free" zp として伝統的に使われる
 * (C64 PRG 開発者向けの公式 reference でも safe 領域として案内)。oscar64 user
 * code は zp $20-$7F を使うので competition なし。 */
#define SLANG_SID_INIT_VEC 0x00FB
#define SLANG_SID_PLAY_VEC 0x00FD

void slang_sid_player_init(unsigned char song)
{
    /* oscar64 の C 関数ポインタ呼出 (= ((void(*)(byte))addr)(song)) は
     * 内部 bytecode interpreter (bcexec) へ JSR されてしまい raw 6502 code には
     * 飛ばないため、6502 indirect jump (JMP ($xxxx)) で実装する。
     * .sid load 未成功時は no-op。 */
    if (g_sid_init_addr == 0) return;

    /* zp $FB/$FC に init address (LE) を書き込んでから JMP indirect。
     * SID player init code は RTS で帰る (= 既存 stack の caller return addr へ)。 */
    *((volatile unsigned char *)(SLANG_SID_INIT_VEC + 0)) =
        (unsigned char)(g_sid_init_addr & 0xFF);
    *((volatile unsigned char *)(SLANG_SID_INIT_VEC + 1)) =
        (unsigned char)((g_sid_init_addr >> 8) & 0xFF);

    __asm {
        lda song
        jmp ($00fb)
    }
}

void slang_sid_player_play(void)
{
    if (g_sid_play_addr == 0) return;

    *((volatile unsigned char *)(SLANG_SID_PLAY_VEC + 0)) =
        (unsigned char)(g_sid_play_addr & 0xFF);
    *((volatile unsigned char *)(SLANG_SID_PLAY_VEC + 1)) =
        (unsigned char)((g_sid_play_addr >> 8) & 0xFF);

    __asm {
        jmp ($00fd)
    }
}

unsigned int slang_sid_player_ready(void)
{
    return (g_sid_init_addr != 0 && g_sid_play_addr != 0) ? 1 : 0;
}

unsigned int slang_sid_load_from_buf_addr(unsigned int buf_addr, unsigned int len)
{
    return slang_sid_load_from_buf((unsigned char *)buf_addr, len);
}
