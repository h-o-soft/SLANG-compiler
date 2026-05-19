/*
 * SLANG sprite bridge API for C64 (oscar64).
 *
 * SLANG WORD (= 16-bit unsigned) を経由して oscar64 c64/sprites.h の API を
 * 呼べる wrapper 群。oscar64 関数を SLANG c_bindings: で直接バインドせず、
 * bridge 経由で型整合と pointer table 管理を吸収する。
 *
 * 使い方 (SLANG 側):
 *   #INCLUDE "C64_VIC.LIB"   ; 色定数 (VCOL_*)
 *   MAIN() {
 *       SPR_INIT($0400);     ; screen RAM 先頭 (= 0x0400 + 0x3F8 が pointer table)
 *       SPR_SET(0, 1, 100, 100, 192, VCOL_WHITE, 0, 0, 0);
 *       LOOP { SPR_MOVE(0, X, Y); ... }
 *   }
 *
 * 引数の意味は c64.env の c_bindings: 経由で SLANG から見えるシグネチャに揃う。
 */
#ifndef SLANG_SPRITE_H
#define SLANG_SPRITE_H

/* SPR_INIT(screen_addr): screen RAM 先頭 address を渡す。bridge 内で
 * 後段 SPR_SET / SPR_IMAGE の pointer table 書き込み base に使う。 */
void slang_spr_init(unsigned int screen_addr);

/* SPR_SET(sp, show, x, y, image, color, multi, xex, yex):
 * sprite N (0..7) を有効化 + 位置/イメージ/色設定。show/multi/xex/yex は
 * SLANG BYTE で受けて bridge で != 0 → bool 化。 */
void slang_spr_set(unsigned char sp, unsigned char show,
                   unsigned int x, unsigned int y,
                   unsigned char image, unsigned char color,
                   unsigned char multi, unsigned char xex, unsigned char yex);

/* SPR_MOVE(sp, x, y): sprite N の位置のみ更新 (= 移動 inner loop 用) */
void slang_spr_move(unsigned char sp, unsigned int x, unsigned int y);

/* SPR_SHOW(sp, show): sprite N の有効/無効切替 (= bool show) */
void slang_spr_show(unsigned char sp, unsigned char show);

/* SPR_POSX/Y(sp): 現在位置取得 (sprite 状態同期用) */
unsigned int slang_spr_posx(unsigned char sp);
unsigned int slang_spr_posy(unsigned char sp);

/* SPR_COLOR(sp, color): 色変更のみ */
void slang_spr_color(unsigned char sp, unsigned char color);

/* SPR_IMAGE(sp, image): sprite N の image block (0..255) を切替。
 * bridge 側で pointer table も書き換える (= SLANG 側で MEM[$07F8+sp] 手書き不要) */
void slang_spr_image(unsigned char sp, unsigned char image);

/* VIC_WAIT(): VSYNC 待ち (= oscar64 vic_waitFrame、ラスタが top に来てから
 * bottom に達するまで待つ、完全 1 フレーム同期)。
 * sprite を毎フレーム移動するアニメーションでは update 前か後に呼ぶことで
 * tearing を防ぐ。sprite 専用ではなく VIC-II 全般用なので vic_* 命名。 */
void slang_vic_wait_frame(void);

#endif /* SLANG_SPRITE_H */
