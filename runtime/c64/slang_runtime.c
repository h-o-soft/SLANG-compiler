/*
 * SLANG runtime for C64 (oscar64) - v1 implementation.
 *
 * Uses oscar64 standard headers (stdio / stdlib / conio / string).
 * PETSCII encoding for string literals is handled by oscar64 -psci.
 */

#include "slang_runtime.h"
#include <stdio.h>
#include <stdlib.h>
#include <string.h>
#include <conio.h>

/* === PRINT === */

void slang_print_str(const char *s)
{
    /* oscar64 has printf with %s for PETSCII strings. */
    printf("%s", s);
}

void slang_print_int(unsigned int n)
{
    char buf[6];
    utoa(n, buf, 10);
    printf("%s", buf);
}

void slang_print_sint(int n)
{
    char buf[7];
    itoa(n, buf, 10);
    printf("%s", buf);
}

void slang_print_hex_b(unsigned char n)
{
    static const char hex[] = "0123456789ABCDEF";
    char buf[3];
    buf[0] = hex[(n >> 4) & 0x0F];
    buf[1] = hex[n & 0x0F];
    buf[2] = 0;
    printf("%s", buf);
}

void slang_print_hex_w(unsigned int n)
{
    slang_print_hex_b((unsigned char)((n >> 8) & 0xFF));
    slang_print_hex_b((unsigned char)(n & 0xFF));
}

void slang_print_float(float f)
{
    char buf[16];
    ftoa(f, buf);
    printf("%s", buf);
}

void slang_print_char(unsigned char c)
{
    putch(c);
}

void slang_println(void)
{
    putch(13);
}

void slang_print_tab(unsigned char col)
{
    gotoxy(col, wherey());
}

/* === INPUT === */

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

/* === String helpers (static buffer; overwritten on each call) === */

static char slang_chr_buf[2];
const char *slang_chr(unsigned char n)
{
    slang_chr_buf[0] = n;
    slang_chr_buf[1] = 0;
    return slang_chr_buf;
}

static char slang_deci_buf[7];
const char *slang_deci(int n)
{
    itoa(n, slang_deci_buf, 10);
    return slang_deci_buf;
}

/* === Random === */

unsigned int slang_rnd(unsigned int max)
{
    if (max == 0) return 0;
    return ((unsigned int)rand()) % max;
}

void slang_srnd(unsigned int seed)
{
    srand(seed);
}

/* === Bit ops === */

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
