// Commodore 64 SID 単発 SFX demo (oscar64 backend、env c_bindings: 経由)
//
// JOYSPR.SL を拡張: joystick で sprite 移動 + fire ボタンで voice 0 SFX 発射 +
// 1/2/3 キーで音色 (preset) 切替。SID register direct API + 単発 SFX wrapper
// の v3b-A スコープを動作確認するための sample。
//
// SFX preset (= 3 種類、SLANG コード内で切替):
//   preset 0 = laser:    NOTE_C5 + SAW + 速い attack/decay + 中 sustain (= 押してる間ピーと鳴り続ける)
//   preset 1 = boom:     NOTE_C3 + PULSE + 50% duty pwm + 短 sustain (= 押してる間ボーと低音持続)
//   preset 2 = noise:    NOTE_E4 + NOISE + sustain 0 (= 押した瞬間ザッと鳴って即減衰、押し続けても無音)
//
// SID 注意: PULSE 波形は pulse width register (= SID_PWM) を設定しないと
// duty 0% で無音になる。boom (preset 1) は MAIN 冒頭で SID_PWM(SFX_VOICE,
// SID_PWM_SQR) で 50% duty を設定済 = 押下中ずっと矩形波が鳴る。
// preset 2 (noise) は sustain = 0 設計なので gate on 直後にすぐ無音になる
// 仕様、これは「短い SFX 効果音」用途。長く鳴らしたければ susrel の上位 4bit
// (= SID_SUS_*) を上げる。
//
// 操作:
//   joystick port 2 = 8 方向移動 (JOYSPR.SL と同じ)
//   fire = 現在 preset の SFX 発射 (押下で gate on、離して gate off で release)
//   1 = preset 0 (laser)、2 = preset 1 (boom)、3 = preset 2 (noise)
//
// SID 周波数 / ADSR / waveform 定数は C64_SID.LIB から取得 (= SID register
// 値は PAL clock 基準で事前計算済、NTSC 機では音高が約 4% 高くなる)。
// HVSC .sid 取り込み + BGM 再生は v3b-B、priority SFX overlay は v3b-C で対応予定。
//
// Build (= checkout 状態 / repo root から実行):
//   slangbuild -E c64 -I include examples/c64/SIDSFX.SL -o examples/c64/SIDSFX
//   x64sc -autostart examples/c64/SIDSFX.prg
// 配布物 install 後 (= SLANG_HOME 設定済) は -I include 省略可。

#INCLUDE "C64_VIC.LIB"
#INCLUDE "C64_JOY.LIB"
#INCLUDE "C64_SID.LIB"

// sprite block 192 = $3000 (SPRITE.SL / JOYSPR.SL と同じ慣行)
CONST SPRITE_IMG_BLOCK = 192;
CONST SPRITE_IMG_ADDR  = $3000;
CONST SPEED            = 2;

// SFX voice 番号: voice 0 = SFX 専用 (= v3b-A は単発のみ、voice 1-2 は v3b-B/C で
// BGM 用に予約する想定。先取りで voice 0 を SFX 用に固定しておく)
CONST SFX_VOICE = SID_VOICE_1;

VAR X, Y, COLOR, DIR, K, PRESET, SFX_ACTIVE;
ARRAY BYTE SPRDATA[63];

// SFX 発射 helper: preset 番号に応じた (freq, ADSR, waveform) を SID_SFX に渡す。
// ADSR pack: SID_ATK_* (上位 4bit) OR SID_DKY_* (下位 4bit) で attdec byte、
// SID_SUS_* (上位 4bit) OR SID_DKY_* (下位 4bit) で susrel byte を組み立てる。
FIRE_SFX(P)
{
    IF P == 0 THEN
    {
        // laser: 高音 saw、短 attack + 中 decay、sustain 8/15 + 中 release
        SID_SFX(SFX_VOICE, NOTE_C5,
                SID_ATK_2 OR SID_DKY_168,
                SID_SUS_8 OR SID_DKY_300,
                SID_WF_SAW);
    }
    IF P == 1 THEN
    {
        // boom: 低音 pulse、短 attack + 中 decay、sustain 8/15 (=押下中持続) + 長 release
        // pwm は MAIN 冒頭で SID_PWM_SQR (50% duty) に設定済
        SID_SFX(SFX_VOICE, NOTE_C3,
                SID_ATK_2 OR SID_DKY_300,
                SID_SUS_8 OR SID_DKY_1500,
                SID_WF_PULSE);
    }
    IF P == 2 THEN
    {
        // noise: 中音 noise、瞬時 attack + 短 decay、no sustain + 短 release
        SID_SFX(SFX_VOICE, NOTE_E4,
                SID_ATK_2 OR SID_DKY_72,
                SID_SUS_0 OR SID_DKY_168,
                SID_WF_NOISE);
    }
}

MAIN()
{
    VAR I;

    // sprite データ生成 + $3000 配置 (= SPRITE.SL と同じ全 $FF パターン)
    FOR I = 0 TO 63 { SPRDATA[I] = $FF; }
    FOR I = 0 TO 63 { MEM[SPRITE_IMG_ADDR + I] = SPRDATA[I]; }

    SPR_INIT($0400);
    SPR_SET(0, 1, 160, 130, SPRITE_IMG_BLOCK, VCOL_WHITE, 0, 0, 0);
    COLOR = VCOL_WHITE;

    // SID 初期化 + master volume max。preset 0 (laser) で開始。
    SID_INIT_QUIET();
    SID_VOLUME(15);
    // PULSE wave (= preset 1 boom) 用に pwm を 50% duty に設定 (= 設定しないと無音)。
    // SFX_VOICE 1 つだけで全 preset を回すので、起動時 1 回設定で OK。
    SID_PWM(SFX_VOICE, SID_PWM_SQR);
    PRESET = 0;
    SFX_ACTIVE = 0;

    X = 160; Y = 130;
    LOOP
    {
        VIC_WAIT();
        JOY_POLL(JOY_PORT2);
        DIR = JOY_DIR(JOY_PORT2);

        // 方向移動 (JOYSPR.SL と同じ、斜め時は両軸更新)
        IF DIR AND JOY_LEFT  THEN X = X - SPEED;
        IF DIR AND JOY_RIGHT THEN X = X + SPEED;
        IF DIR AND JOY_UP    THEN Y = Y - SPEED;
        IF DIR AND JOY_DOWN  THEN Y = Y + SPEED;
        IF X < 24  THEN X = 24;
        IF X > 320 THEN X = 320;
        IF Y < 50  THEN Y = 50;
        IF Y > 220 THEN Y = 220;
        SPR_MOVE(0, X, Y);

        // fire = SFX 発射 + 押下中は active flag 立てる、離れたら gate off (release 開始)
        IF DIR AND JOY_FIRE
        {
            IF SFX_ACTIVE == 0
            {
                FIRE_SFX(PRESET);
                SFX_ACTIVE = 1;
                // sprite 色も preset と連動 (= 視覚 feedback)
                IF PRESET == 0 THEN SPR_COLOR(0, VCOL_CYAN);
                IF PRESET == 1 THEN SPR_COLOR(0, VCOL_RED);
                IF PRESET == 2 THEN SPR_COLOR(0, VCOL_YELLOW);
            }
        }
        ELSE
        {
            IF SFX_ACTIVE
            {
                SID_GATE_OFF(SFX_VOICE);
                SFX_ACTIVE = 0;
                SPR_COLOR(0, VCOL_WHITE);
            }
        }

        // 1/2/3 キーで preset 切替 (= INKEY non-blocking 読み)
        K = INKEY(0);
        IF K == 49 THEN PRESET = 0;   // '1'
        IF K == 50 THEN PRESET = 1;   // '2'
        IF K == 51 THEN PRESET = 2;   // '3'
    }
}
