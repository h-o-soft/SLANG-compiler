/*
 * SLANG sprite bridge for C64 (oscar64).
 * See slang_sprite.h for API contract.
 */

#include "slang_sprite.h"
#include <c64/sprites.h>
#include <c64/vic.h>

/* bridge 自前の sprite pointer table 計算用 screen base。SPR_INIT 時に
 * SLANG 側が渡す screen RAM 先頭 address を保存する。oscar64 内部の
 * vspriteScreen 等の非公開シンボルには依存しない (= 公開 API のみ依存)。 */
static unsigned char *slang_spr_screen = 0;

void slang_spr_init(unsigned int screen_addr)
{
    slang_spr_screen = (unsigned char *)screen_addr;
    spr_init(slang_spr_screen);
}

/* sprite pointer table への書き込みは bridge 側の責務とする (sample 側で
 * MEM[$07F8 + sp] を手書きせずに済ませる)。SPR_SET と SPR_IMAGE の両方を
 * 同じ規律で揃え、image block 切替の API として直感に合うようにする。
 * oscar64 spr_set / spr_image が内部で書く実装でも重複書き込みは harmless。 */
static void slang_spr_write_ptr(unsigned char sp, unsigned char image)
{
    if (slang_spr_screen != 0 && sp < 8)
        slang_spr_screen[0x3F8 + sp] = image;
}

void slang_spr_set(unsigned char sp, unsigned char show,
                   unsigned int x, unsigned int y,
                   unsigned char image, unsigned char color,
                   unsigned char multi, unsigned char xex, unsigned char yex)
{
    slang_spr_write_ptr(sp, image);
    spr_set(sp, show != 0, (int)x, (int)y, image, color,
            multi != 0, xex != 0, yex != 0);
}

void slang_spr_image(unsigned char sp, unsigned char image)
{
    slang_spr_write_ptr(sp, image);
    spr_image(sp, image);
}

void slang_spr_move(unsigned char sp, unsigned int x, unsigned int y)
{
    spr_move(sp, (int)x, (int)y);
}

void slang_spr_show(unsigned char sp, unsigned char show)
{
    spr_show(sp, show != 0);
}

unsigned int slang_spr_posx(unsigned char sp)
{
    return (unsigned int)spr_posx(sp);
}

unsigned int slang_spr_posy(unsigned char sp)
{
    return (unsigned int)spr_posy(sp);
}

void slang_spr_color(unsigned char sp, unsigned char color)
{
    spr_color(sp, color);
}

void slang_vic_wait_frame(void)
{
    /* oscar64 vic_waitFrame: top + bottom 両方待つ 1 フレーム同期 */
    vic_waitFrame();
}
