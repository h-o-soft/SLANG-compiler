/*
 * SLANG runtime for C64 (oscar64) - v1.
 *
 * v1 scope: I/O + integer arithmetic + FLOAT (mapped to float32).
 * Graphics / sound / disk / CMT / overlay are deferred to v2+.
 *
 * String handling expects oscar64 -psci: unprefixed string literals are
 * encoded as PETSCII. v1 supports ASCII printable characters only;
 * Japanese / SJIS / kana are out of scope.
 */
#ifndef SLANG_RUNTIME_H
#define SLANG_RUNTIME_H

/* sprite bridge API (= env c_bindings: で公開、SLANG 側 CFUNC 宣言不要)。
 * 同一 header chain で生成 C 側 extern と bridge 実装の signature drift を防ぐ。
 * sprite を使わない SLANG プログラムでも include コスト微小。 */
#include "slang_sprite.h"

/* === PRINT === */

void slang_print_str(const char *s);          /* NUL-terminated 文字列 */
void slang_print_msg(const char *s);          /* CR (0x0D) terminated 文字列 */
void slang_print_int(unsigned int n);         /* 10進 unsigned */
void slang_print_sint(int n);                 /* 10進 signed (= PRINT %(v) / PN$) */
void slang_print_hex_b(unsigned char n);      /* 16進 2 桁 */
void slang_print_hex_w(unsigned int n);       /* 16進 4 桁 */
void slang_print_float(float f);
void slang_print_char(unsigned char c);
void slang_print_chr_w(unsigned int n);       /* SLANG CHR$(n): 上位・下位 byte の順 */
void slang_println(void);                     /* 改行 1 個 */
void slang_print_deci(int n);                 /* DECI$(v): 10進 5 桁右詰め */
void slang_print_form(int n, unsigned char w); /* FORM$(v, n): 10進 n 桁右詰め */
void slang_print_str_n(unsigned char c, unsigned int n);  /* STR$(c, n): 同じ文字を n 回 */
void slang_print_spc(unsigned int n);         /* SPC$(n): 空白 n 個 */
void slang_print_cr(unsigned int n);          /* CR$(n): 改行 n 個 */
void slang_print_tab(unsigned char col);      /* PTAB(col): 絶対カーソル位置に水平移動 */
void slang_print_tab_n(unsigned int n);       /* TAB$(n): カーソル相対右移動 n 回 */

/* === 端末制御 (= MACHINE 関数の C backend 実装) === */

void slang_width(unsigned int w);             /* WIDTH(w): C64 は 40 桁固定 = no-op */
void slang_locate(unsigned int x, unsigned int y);  /* LOCATE(x, y): 0-indexed */
unsigned int slang_inkey(unsigned int mode);  /* INKEY(mode): non-blocking key read */
unsigned int slang_screen(unsigned int x, unsigned int y);  /* SCREEN(x, y): 画面文字読み */
void slang_prmode(unsigned int m);            /* PRMODE(m): C64 では切替先なし = no-op */

/* === INPUT (SLANG 互換 bridge、env c_bindings: で SLANG → C 関数として公開) === */

/* GETLIN(buf_addr, x): カーソルを x 列に移動してから 1 行入力。
 * 戻り値: 入力文字数 (NUL terminate 除く)、ESC キーで 0xFFFF。
 * buf_addr は SLANG WORD で受け取り bridge 内で (unsigned char *) 化。 */
unsigned int slang_getlin(unsigned int buf_addr, unsigned int x);

/* GETL(buf_addr) = GETLIN(buf_addr, 0) */
unsigned int slang_getl(unsigned int buf_addr);

/* LINPUT(buf_addr, x) = GETLIN と同等 (Z80 backend 互換、内部実装は同じ)。
 * Z80 では sCSR でカーソル位置を保存する違いがあるが、C backend では
 * 単純化して GETLIN と同じ挙動。 */
unsigned int slang_linput(unsigned int buf_addr, unsigned int x);

/* INPUT(): 1 行入力 → 数値 parse (10進、または $hex)。
 * 戻り値: 数値、ESC キーで 0xFFFF。Z80 backend が使う `_CARRY` 機構は v1
 * では未対応のため、SLANG コード側で戻り値 $FFFF を ESC として判定する。 */
unsigned int slang_input(void);

/* === String helpers (SLANG static-buffer semantics; 連続呼び出しで上書き) === */

const char *slang_chr(unsigned char n);       /* CHR$(n): 1 byte char as string */
const char *slang_deci(int n);                /* DECI$(v): 5 桁右詰め string */
const char *slang_hex2(unsigned char n);      /* HEX2$(v): 2 桁 hex string */
const char *slang_hex4(unsigned int n);       /* HEX4$(v): 4 桁 hex string */
const char *slang_pn(int n);                  /* PN$(v): signed 10進 string */
const char *slang_fl(float f);                /* FL$(f): float → string */
const char *slang_msx(const char *s);         /* MSX$(addr): そのまま (= NUL-term) */
const char *slang_msg(const char *s);         /* MSG$(addr): CR-term → NUL-term 変換 */

/* === Random === */

unsigned int slang_rnd(unsigned int max);
void slang_srnd(unsigned int seed);

/* === Bit ops === */

unsigned int slang_bit(unsigned int v, unsigned char b);
void slang_set(unsigned char *p, unsigned char b);
void slang_reset(unsigned char *p, unsigned char b);

/* === Direct memory access (used by CEmitter for SLANG MEM[] / MEMW[]) === */

#define SLANG_MEM(addr)   (*(volatile unsigned char *)(addr))
#define SLANG_MEMW(addr)  (*(volatile unsigned int  *)(addr))

#endif /* SLANG_RUNTIME_H */
