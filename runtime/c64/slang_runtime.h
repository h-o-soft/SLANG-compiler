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

void slang_print_str(const char *s);
void slang_print_int(unsigned int n);
void slang_print_sint(int n);
void slang_print_hex_b(unsigned char n);
void slang_print_hex_w(unsigned int n);
void slang_print_float(float f);
void slang_print_char(unsigned char c);
void slang_println(void);
void slang_print_tab(unsigned char col);

/* === INPUT === */

int  slang_input_int(void);
void slang_input_str(char *buf, unsigned int max);

/* === String helpers (SLANG static-buffer semantics) === */

const char *slang_chr(unsigned char n);
const char *slang_deci(int n);

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
