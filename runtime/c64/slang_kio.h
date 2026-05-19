/*
 * SLANG KERNAL file I/O bridge API for C64 (oscar64).
 *
 * oscar64 <c64/kernalio.h> の薄い wrapper。SLANG WORD + byte_ptr 経由で
 * SLANG コードから扱えるよう型整合と signed-error の sign extension を
 * bridge 側で吸収する。
 *
 * 文字列形式: NUL terminated PETSCII 固定 (= oscar64 `-psci` で SLANG
 * リテラル "HISCORE,S,R" がそのまま PETSCII 化されて KERNAL に渡る)。
 *
 * 使い方 (SLANG 側、典型):
 *   #INCLUDE "C64_KIO.LIB"      ; KRNIO_OK / DEV_DISK / CH_READ 等
 *   ARRAY BYTE BUF[80];
 *   KIO_OPEN_NAMED(2, DEV_DISK, CH_READ, "HISCORE,S,R");
 *   IF KIO_STATUS() == KRNIO_OK THEN {
 *       KIO_CHKIN(2);
 *       SCORE = KIO_CHRIN();
 *       KIO_CLRCHN();
 *   }
 *   KIO_CLOSE(2);
 *
 * 戻り値の慣行:
 *   - bool 系 (open / chkin / chkout / chrout) は 0 = fail / 1 = success
 *   - byte 読み系 (chrin) は 0..255
 *   - getch / read / write / gets / puts は oscar64 で signed int を返し、
 *     エラーは負値。SLANG WORD では負値を $FFFx (= sign extension) として
 *     返すので、エラー判定は `IF (result & $8000) THEN ...` または特定
 *     値 `IF result == $FFFF THEN ...` で行う。
 *   - status は krnioerr enum 値 (= 0 = KRNIO_OK、それ以外はエラー bit mask)
 */
#ifndef SLANG_KIO_H
#define SLANG_KIO_H

/* ============================================================
 * 通常 API (byte_ptr 中心、SLANG StringLiteral / ARRAY BYTE がそのまま渡せる)
 * ============================================================ */

/* 文字列ポインタは non-const で受ける (= CTranspiler が env byte_ptr binding
 * の extern を `unsigned char *` で emit するため、const 付きにすると C 規格
 * 上の conflicting types で oscar64 が rejecting する)。
 * 中身は read-only として扱う (= 内部で krnio_setnam(const char *) に cast 渡し)。 */
void slang_kio_setnam(unsigned char *name);

/* 複合 helper: setnam + open を 1 呼びにまとめる。typical usage で頻出。 */
unsigned int slang_kio_open_named(unsigned char fnum, unsigned char dev,
                                   unsigned char ch, unsigned char *name);

unsigned int slang_kio_open(unsigned char fnum, unsigned char dev, unsigned char ch);
void         slang_kio_close(unsigned char fnum);
unsigned int slang_kio_chkin(unsigned char fnum);
unsigned int slang_kio_chkout(unsigned char fnum);
void         slang_kio_clrchn(void);

unsigned int slang_kio_chrin(void);
unsigned int slang_kio_chrout(unsigned int ch);

/* getch / putch は fnum を毎回指定する形 (= chkin/chkout 不要)。
 * getch の戻り値: 下位 8 bit が読み込み byte、bit 8 が EOF flag。負値はエラー。
 * SLANG WORD では負値は $FFFx として届く。 */
unsigned int slang_kio_getch(unsigned char fnum);
unsigned int slang_kio_putch(unsigned char fnum, unsigned int ch);

/* read / write はバイト列まとめ I/O。戻り値 = 実 byte 数、エラーは
 * SLANG WORD で $FFFx。oscar64 内部では signed int (= 16-bit) なので
 * 30000 byte 程度までは正値で返る (C64 RAM 制約で実用 OK)。 */
unsigned int slang_kio_read(unsigned char fnum, unsigned char *buf, unsigned int n);
unsigned int slang_kio_write(unsigned char fnum, unsigned char *buf, unsigned int n);

/* puts / gets は文字列向け wrapper。
 * puts: NUL terminated 文字列を 1 回で書く。
 * gets: CR/LF terminated 行を読み、buf に NUL terminate して書く。戻り値 =
 *       実読み byte 数 (NUL 除く)。 */
unsigned int slang_kio_puts(unsigned char fnum, unsigned char *str);
unsigned int slang_kio_gets(unsigned char fnum, unsigned char *buf, unsigned int n);

/* 最後の I/O 操作の status を取得 (= krnioerr enum 値、KRNIO_OK = 0)。 */
unsigned int slang_kio_status(void);

/* ============================================================
 * raw address 用 API (MEM[$xxxx] / 固定アドレス buffer から呼びたい場合の補助)
 * SLANG WORD で受けて bridge 内で (const unsigned char *)addr cast。
 * 通常 API (byte_ptr 版) で済むなら不要、SLANG コードで明示的に絶対
 * アドレスを使う高度なケース向け。
 * ============================================================ */

void         slang_kio_setnam_addr(unsigned int name_addr);
unsigned int slang_kio_open_named_addr(unsigned char fnum, unsigned char dev,
                                        unsigned char ch, unsigned int name_addr);
unsigned int slang_kio_read_addr(unsigned char fnum, unsigned int buf_addr, unsigned int n);
unsigned int slang_kio_write_addr(unsigned char fnum, unsigned int buf_addr, unsigned int n);

#endif /* SLANG_KIO_H */
