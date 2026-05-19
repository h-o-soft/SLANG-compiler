/*
 * SLANG joystick bridge API for C64 (oscar64).
 *
 * oscar64 <c64/joystick.h> の薄い wrapper。SLANG WORD 経由で扱えるよう
 * signed -1 を unsigned 0xFFFF として返す + bitmask 形式の JOY_DIR を追加。
 *
 * 使い方 (SLANG 側):
 *   #INCLUDE "C64_JOY.LIB"        ; JOY_UP / DOWN / LEFT / RIGHT / FIRE / PORT2 等
 *   VAR D;
 *   LOOP {
 *       JOY_POLL(JOY_PORT2);      ; 1 frame 1 回呼ぶ
 *       D = JOY_DIR(JOY_PORT2);
 *       IF D & JOY_LEFT THEN ...
 *       IF JOY_B(JOY_PORT2) THEN ...
 *   }
 *
 * SLANG コード上で JOY_X / JOY_Y を直接見たい場合は -1 = $FFFF として
 * 扱う:
 *   IF JOY_X(JOY_PORT2) == $FFFF THEN ...   ; 左
 *   IF JOY_X(JOY_PORT2) == 1     THEN ...   ; 右
 *   IF JOY_X(JOY_PORT2) == 0     THEN ...   ; 中立
 * 一般的には JOY_DIR bitmask を主 API として使うほうが直感的。
 */
#ifndef SLANG_JOYSTICK_H
#define SLANG_JOYSTICK_H

/* JOY_POLL(port): port (0 or 1) のジョイスティックをスキャンして
 * 後段 JOY_X/Y/B/DIR で読める状態にする。1 frame 1 回呼ぶ想定。 */
void slang_joy_poll(unsigned char port);

/* JOY_X(port) / JOY_Y(port):
 *   左/上 = -1 (= SLANG WORD では $FFFF)
 *   中立  = 0
 *   右/下 = 1
 * oscar64 内部の signed byte を unsigned int に sign extension。 */
unsigned int slang_joy_x(unsigned char port);
unsigned int slang_joy_y(unsigned char port);

/* JOY_B(port): fire ボタン押下中 = 1、離 = 0 */
unsigned int slang_joy_b(unsigned char port);

/* JOY_DIR(port): 5 bit の bitmask (= include/C64_JOY.LIB の CONST と整合)。
 *   bit 0 (= 1)  = UP
 *   bit 1 (= 2)  = DOWN
 *   bit 2 (= 4)  = LEFT
 *   bit 3 (= 8)  = RIGHT
 *   bit 4 (= 16) = FIRE
 * 斜め入力では UP|RIGHT のように複数 bit が立つ。 */
unsigned int slang_joy_dir(unsigned char port);

#endif /* SLANG_JOYSTICK_H */
