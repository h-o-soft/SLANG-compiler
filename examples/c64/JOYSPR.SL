// Commodore 64 sprite + joystick demo (oscar64 backend、env c_bindings: 経由)
//
// SPRITE.SL を改造、自動移動を joystick 入力に置換。
// port 2 (= 一般的な C64 ゲームパッド位置) の 8 方向で sprite を動かし、
// fire ボタンで sprite 色を順に変える。
//
// 主 API は JOY_DIR(port) bitmask (= 短く書ける + 符号問題なし)。
// 補助 API JOY_X(port) は signed -1 を SLANG WORD では $FFFF として返すので、
// 直接使う場合は IF JOY_X(JOY_PORT2) == $FFFF THEN ... の判定パターン。
//
// Build:
//   slangbuild -E c64 examples/c64/JOYSPR.SL -o examples/c64/JOYSPR
//   x64sc -autostart examples/c64/JOYSPR.prg

#INCLUDE "C64_VIC.LIB"
#INCLUDE "C64_JOY.LIB"

// sprite block 192 = $3000 (= 192 * 64)、SPRITE.SL と同じ
CONST SPRITE_IMG_BLOCK = 192;
CONST SPRITE_IMG_ADDR  = $3000;

// 移動速度 (joystick の 1 方向押下で 1 frame に動く dot 数)
CONST SPEED = 2;

VAR X, Y, COLOR, DIR;
ARRAY BYTE SPRDATA[63];

MAIN()
{
    VAR I;

    // sprite データ生成 + $3000 配置 (SPRITE.SL と同じ)
    FOR I = 0 TO 63 { SPRDATA[I] = $FF; }
    FOR I = 0 TO 63 { MEM[SPRITE_IMG_ADDR + I] = SPRDATA[I]; }

    // sprite 0 を有効化
    SPR_INIT($0400);
    SPR_SET(0, 1, 160, 130, SPRITE_IMG_BLOCK, VCOL_WHITE, 0, 0, 0);
    COLOR = VCOL_WHITE;

    X = 160; Y = 130;
    LOOP
    {
        VIC_WAIT();                  // VSYNC で更新間隔を 50/60Hz に固定
        JOY_POLL(JOY_PORT2);         // 1 frame 1 回 poll
        DIR = JOY_DIR(JOY_PORT2);    // 5 bit bitmask

        // 方向移動 (= 斜め時は X/Y 両方更新される)
        IF DIR & JOY_LEFT  THEN X = X - SPEED;
        IF DIR & JOY_RIGHT THEN X = X + SPEED;
        IF DIR & JOY_UP    THEN Y = Y - SPEED;
        IF DIR & JOY_DOWN  THEN Y = Y + SPEED;

        // sprite 表示範囲で clamp (= 画面外に出ても VIC が描画しないだけで弊害は
        // 無いが、HW sprite の visible 範囲に揃えると分かりやすい)
        IF X < 24  THEN X = 24;
        IF X > 320 THEN X = 320;
        IF Y < 50  THEN Y = 50;
        IF Y > 220 THEN Y = 220;
        SPR_MOVE(0, X, Y);

        // fire 押下で色を順に変える (= bitmask check と並列、JOY_B でも JOY_FIRE
        // でも同じ)。bouncing 防止のため、押下中は 1 度だけ更新する。
        IF DIR & JOY_FIRE
        {
            COLOR = COLOR + 1;
            IF COLOR > VCOL_LT_GREY THEN COLOR = VCOL_BLACK + 1;
            SPR_COLOR(0, COLOR);
            // fire 離されるまで待つ (= 連続変色防止、簡易 debouncing)
            WHILE JOY_B(JOY_PORT2)
            {
                VIC_WAIT();
                JOY_POLL(JOY_PORT2);
            }
        }
    }
}
