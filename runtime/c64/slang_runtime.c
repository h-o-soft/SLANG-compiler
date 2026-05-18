/*
 * SLANG runtime for C64 (oscar64) - v1 implementation.
 *
 * Uses oscar64 standard headers (stdio / stdlib / conio).
 * PETSCII encoding for string literals is handled by oscar64 -psci.
 */

#include "slang_runtime.h"
#include <stdio.h>
#include <stdlib.h>
#include <conio.h>
#include <c64/keyboard.h>

/* ============================================================
 * Static buffers for string-returning helpers.
 * SLANG semantics: every call overwrites the previous buffer
 * (= caller should consume immediately).
 * ============================================================ */

static char slang_chr_buf[3];   /* CHR$ は 1〜2 byte (上位/下位 byte) + NUL */
static char slang_num_buf[8];   /* DECI$/PN$/HEX2$/HEX4$/FL$ 共用 */

/* ============================================================
 * Internal helpers
 * ============================================================ */

static void put_str(const char *s)
{
    while (*s) putch(*s++);
}

static const char hex_digits[] = "0123456789ABCDEF";

/* ============================================================
 * PRINT
 * ============================================================ */

void slang_print_str(const char *s)
{
    put_str(s);
}

void slang_print_msg(const char *s)
{
    /* CR (0x0D) terminated 文字列。CR の手前まで出力。 */
    while (*s && *s != 0x0D) putch(*s++);
}

void slang_print_int(unsigned int n)
{
    utoa(n, slang_num_buf, 10);
    put_str(slang_num_buf);
}

void slang_print_sint(int n)
{
    itoa(n, slang_num_buf, 10);
    put_str(slang_num_buf);
}

void slang_print_hex_b(unsigned char n)
{
    putch(hex_digits[(n >> 4) & 0x0F]);
    putch(hex_digits[n & 0x0F]);
}

void slang_print_hex_w(unsigned int n)
{
    slang_print_hex_b((unsigned char)((n >> 8) & 0xFF));
    slang_print_hex_b((unsigned char)(n & 0xFF));
}

void slang_print_float(float f)
{
    ftoa(f, slang_num_buf);
    put_str(slang_num_buf);
}

void slang_print_char(unsigned char c)
{
    putch(c);
}

void slang_print_chr_w(unsigned int n)
{
    /* SLANG CHR$(n): n が 8-bit なら 1 文字、16-bit なら上位・下位の順に 2 文字 */
    unsigned char hi = (unsigned char)((n >> 8) & 0xFF);
    unsigned char lo = (unsigned char)(n & 0xFF);
    if (hi != 0) putch(hi);
    putch(lo);
}

void slang_println(void)
{
    putch(13);  /* CR */
}

/* DECI$(v): 10進 5 桁右詰め (Z80 backend 仕様)。Negative 値は先頭に '-' */
void slang_print_deci(int n)
{
    char tmp[8];
    int len, i;
    itoa(n, tmp, 10);
    len = 0;
    while (tmp[len]) ++len;
    for (i = len; i < 5; ++i) putch(' ');
    put_str(tmp);
}

/* FORM$(v, w): 10進 w 桁右詰め */
void slang_print_form(int n, unsigned char w)
{
    char tmp[8];
    int len, i;
    itoa(n, tmp, 10);
    len = 0;
    while (tmp[len]) ++len;
    for (i = len; i < (int)w; ++i) putch(' ');
    put_str(tmp);
}

/* STR$(c, n): char c を n 回 */
void slang_print_str_n(unsigned char c, unsigned int n)
{
    while (n--) putch(c);
}

/* SPC$(n) = STR$(' ', n) */
void slang_print_spc(unsigned int n)
{
    while (n--) putch(' ');
}

/* CR$(n) = 改行 n 回 */
void slang_print_cr(unsigned int n)
{
    while (n--) putch(13);
}

/* PTAB(col): カーソルを絶対 col 位置に水平移動 (= MZ-2500 互換) */
void slang_print_tab(unsigned char col)
{
    gotoxy(col, wherey());
}

/* TAB$(n): カーソルを n 回相対右移動 (= スペースで埋める実装)。oscar64
 * conio に「カーソルだけ動かす」API は無いので、cursor を gotoxy で
 * 計算して直接移動する。画面端を超えた場合の動作は putch(' ') と異なる
 * (= overwrite せずカーソルだけ進める) ため、行頭折り返しは発生しない。 */
void slang_print_tab_n(unsigned int n)
{
    unsigned char x = wherex();
    unsigned char y = wherey();
    unsigned int nx = (unsigned int)x + n;
    while (nx >= 40)
    {
        nx -= 40;
        ++y;
    }
    gotoxy((unsigned char)nx, y);
}

/* ============================================================
 * 端末制御
 * ============================================================ */

void slang_width(unsigned int w)
{
    /* C64 は常に 40 桁。引数は受け流す (= SLANG コードが WIDTH(80) と
     * 書いてもエラーにせず、ただし実画面幅は変わらない)。 */
    (void)w;
}

void slang_locate(unsigned int x, unsigned int y)
{
    gotoxy((unsigned char)x, (unsigned char)y);
}

unsigned int slang_inkey(unsigned int mode)
{
    /* SLANG INKEY(mode): mode 値で blocking 度を切り替える既存 env 仕様
     * (lsx: 0=sGETKY, 1=sFLGET, 2+=sINKEY)。
     *
     * C backend は「押下中ならそのキーの PETSCII を返す、離したら即座に 0」
     * という即時状態取得セマンティクスにする (= STARS.SL のようなゲーム向け
     * use case で必須)。
     *
     * 実装: C64 KERNAL が IRQ (50Hz/60Hz) で zero page $C5 (= 現在押下中の
     * キーの matrix scan code、64 = no key) をメンテしているのでこれを直接
     * 読む。oscar64 の keyb_poll() を呼ぶ方法もあるが、それは KERNAL の
     * 通常 IRQ scan と競合する可能性 + VICE の symbolic キーマッピングで
     * matrix が pulse でしか押されないケースに弱い。$C5 を読む方法は
     * KERNAL の scan 結果を借りるので VICE のマッピングモードに依存しない。
     *
     * SHIFT 状態は $028D bit0 (= 左右どちらかの SHIFT)。scan code に
     * 0x40 を OR して keyb_codes[] の後半 64 entry (= shift 押下時の
     * PETSCII) を参照する。
     *
     * mode 引数は受け流す。 */
    (void)mode;
    unsigned char k = *(volatile unsigned char *)0x00C5;
    if (k >= 64) return 0;  /* 64 = no key currently pressed */
    if ((*(volatile unsigned char *)0x028D) & 0x01) k |= 0x40;  /* SHIFT 修飾 */
    return (unsigned int)(unsigned char)keyb_codes[k];
}

unsigned int slang_screen(unsigned int x, unsigned int y)
{
    /* C64 screen RAM ($0400) から直接読む。VIC-II screen-code を返す。
     * 1000 = 25 row * 40 col の範囲外は 0 を返す (silent)。 */
    unsigned int idx = y * 40 + x;
    if (idx >= 1000) return 0;
    return (unsigned int)(*(volatile unsigned char *)(0x0400 + idx));
}

void slang_prmode(unsigned int m)
{
    /* C64 では PRINT の出力先切替先がないので no-op。 */
    (void)m;
}

/* ============================================================
 * INPUT
 * ============================================================ */

int slang_input_int(void)
{
    char buf[8];
    slang_input_str(buf, sizeof(buf));
    return atoi(buf);
}

void slang_input_str(char *buf, unsigned int max)
{
    /* Naive line input: read until CR, up to max-1 chars. */
    unsigned int n = 0;
    char c;
    while (n + 1 < max)
    {
        c = getch();
        if (c == 13) break;
        if (c == 20)  /* DEL */
        {
            if (n > 0) { --n; putch(20); }
            continue;
        }
        buf[n++] = c;
        putch(c);
    }
    buf[n] = 0;
    putch(13);
}

/* ============================================================
 * String helpers (static buffer, 連続呼び出しで上書き)
 * ============================================================ */

const char *slang_chr(unsigned char n)
{
    slang_chr_buf[0] = (char)n;
    slang_chr_buf[1] = 0;
    return slang_chr_buf;
}

const char *slang_deci(int n)
{
    /* 5 桁右詰め: tmp に変換 → slang_num_buf に padding 込みで埋める */
    char tmp[8];
    int len, i, j;
    itoa(n, tmp, 10);
    len = 0;
    while (tmp[len]) ++len;
    j = 0;
    for (i = len; i < 5; ++i) slang_num_buf[j++] = ' ';
    for (i = 0; i < len && j < (int)sizeof(slang_num_buf) - 1; ++i)
        slang_num_buf[j++] = tmp[i];
    slang_num_buf[j] = 0;
    return slang_num_buf;
}

const char *slang_hex2(unsigned char n)
{
    slang_num_buf[0] = hex_digits[(n >> 4) & 0x0F];
    slang_num_buf[1] = hex_digits[n & 0x0F];
    slang_num_buf[2] = 0;
    return slang_num_buf;
}

const char *slang_hex4(unsigned int n)
{
    slang_num_buf[0] = hex_digits[(n >> 12) & 0x0F];
    slang_num_buf[1] = hex_digits[(n >> 8) & 0x0F];
    slang_num_buf[2] = hex_digits[(n >> 4) & 0x0F];
    slang_num_buf[3] = hex_digits[n & 0x0F];
    slang_num_buf[4] = 0;
    return slang_num_buf;
}

const char *slang_pn(int n)
{
    itoa(n, slang_num_buf, 10);
    return slang_num_buf;
}

const char *slang_fl(float f)
{
    ftoa(f, slang_num_buf);
    return slang_num_buf;
}

const char *slang_msx(const char *s)
{
    /* MSX$(addr): 既に NUL-terminated なのでそのまま返す */
    return s;
}

const char *slang_msg(const char *s)
{
    /* MSG$(addr): CR-terminated を NUL-terminated に変換して static buffer
     * に複製。buffer サイズ制約があるので長すぎる場合は truncate。 */
    static char msg_buf[64];
    unsigned int i = 0;
    while (s[i] && s[i] != 0x0D && i < sizeof(msg_buf) - 1)
    {
        msg_buf[i] = s[i];
        ++i;
    }
    msg_buf[i] = 0;
    return msg_buf;
}

/* ============================================================
 * Random
 * ============================================================ */

unsigned int slang_rnd(unsigned int max)
{
    if (max == 0) return 0;
    return ((unsigned int)rand()) % max;
}

void slang_srnd(unsigned int seed)
{
    srand(seed);
}

/* ============================================================
 * Bit ops
 * ============================================================ */

unsigned int slang_bit(unsigned int v, unsigned char b)
{
    return (v >> b) & 1u;
}

void slang_set(unsigned char *p, unsigned char b)
{
    *p |= (unsigned char)(1u << b);
}

void slang_reset(unsigned char *p, unsigned char b)
{
    *p &= (unsigned char)~(1u << b);
}
