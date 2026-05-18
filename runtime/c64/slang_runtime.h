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

/* === INPUT === */

int  slang_input_int(void);
void slang_input_str(char *buf, unsigned int max);

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
