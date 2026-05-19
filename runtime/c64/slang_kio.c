/*
 * SLANG KERNAL file I/O bridge for C64 (oscar64).
 * See slang_kio.h for API contract.
 */

#include "slang_kio.h"
#include <c64/kernalio.h>

/* ============================================================
 * 通常 API
 * ============================================================ */

void slang_kio_setnam(unsigned char *name)
{
    krnio_setnam((const char *)name);
}

unsigned int slang_kio_open(unsigned char fnum, unsigned char dev, unsigned char ch)
{
    return (unsigned int)(krnio_open(fnum, dev, ch) ? 1 : 0);
}

unsigned int slang_kio_open_named(unsigned char fnum, unsigned char dev,
                                   unsigned char ch, unsigned char *name)
{
    krnio_setnam((const char *)name);
    return (unsigned int)(krnio_open(fnum, dev, ch) ? 1 : 0);
}

void slang_kio_close(unsigned char fnum)
{
    krnio_close(fnum);
}

unsigned int slang_kio_chkin(unsigned char fnum)
{
    return (unsigned int)(krnio_chkin(fnum) ? 1 : 0);
}

unsigned int slang_kio_chkout(unsigned char fnum)
{
    return (unsigned int)(krnio_chkout(fnum) ? 1 : 0);
}

void slang_kio_clrchn(void)
{
    krnio_clrchn();
}

unsigned int slang_kio_chrin(void)
{
    /* krnio_chrin は char (unsigned 0..255) を返す。 */
    return (unsigned int)(unsigned char)krnio_chrin();
}

unsigned int slang_kio_chrout(unsigned int ch)
{
    return (unsigned int)(krnio_chrout((char)ch) ? 1 : 0);
}

unsigned int slang_kio_getch(unsigned char fnum)
{
    /* krnio_getch は int を返す: 下位 8 bit = byte、bit 8 = EOF、負値 = error。
     * SLANG WORD では負値が $FFFx で届く (sign extension で OK)。 */
    return (unsigned int)(int)krnio_getch(fnum);
}

unsigned int slang_kio_putch(unsigned char fnum, unsigned int ch)
{
    return (unsigned int)(int)krnio_putch(fnum, (char)ch);
}

unsigned int slang_kio_read(unsigned char fnum, unsigned char *buf, unsigned int n)
{
    /* oscar64 krnio_read(fnum, char *data, int num) → int (= 実読み数 or 負値) */
    return (unsigned int)(int)krnio_read(fnum, (char *)buf, (int)n);
}

unsigned int slang_kio_write(unsigned char fnum, unsigned char *buf, unsigned int n)
{
    return (unsigned int)(int)krnio_write(fnum, (const char *)buf, (int)n);
}

unsigned int slang_kio_puts(unsigned char fnum, unsigned char *str)
{
    return (unsigned int)(int)krnio_puts(fnum, (const char *)str);
}

unsigned int slang_kio_gets(unsigned char fnum, unsigned char *buf, unsigned int n)
{
    return (unsigned int)(int)krnio_gets(fnum, (char *)buf, (int)n);
}

unsigned int slang_kio_status(void)
{
    return (unsigned int)krnio_status();
}

/* ============================================================
 * raw address 用 API
 * ============================================================ */

void slang_kio_setnam_addr(unsigned int name_addr)
{
    krnio_setnam((const char *)name_addr);
}

unsigned int slang_kio_open_named_addr(unsigned char fnum, unsigned char dev,
                                        unsigned char ch, unsigned int name_addr)
{
    krnio_setnam((const char *)name_addr);
    return (unsigned int)(krnio_open(fnum, dev, ch) ? 1 : 0);
}

unsigned int slang_kio_read_addr(unsigned char fnum, unsigned int buf_addr, unsigned int n)
{
    return (unsigned int)(int)krnio_read(fnum, (char *)buf_addr, (int)n);
}

unsigned int slang_kio_write_addr(unsigned char fnum, unsigned int buf_addr, unsigned int n)
{
    return (unsigned int)(int)krnio_write(fnum, (const char *)buf_addr, (int)n);
}
