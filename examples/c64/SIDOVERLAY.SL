// Commodore 64 SIDFX priority SFX overlay demo (oscar64 backend、 v3b-C)
//
// SLANG コードで voice 0 (= triangle melody) + voice 2 (= sidfx laser SFX) の
// 同時再生 demo。oscar64 audio/sidfx の priority-based SFX overlay を SLANG
// から扱う最小 sample。
//
// 構成:
//   voice 0 = SLANG コード自前の melody (= SID_SFX で 4 note ドミソド loop)
//   voice 1 = 未使用 (= BGM 拡張用に空けてある)
//   voice 2 = SIDFX_PLAY で laser SFX (= joystick fire で発射)
//   SIDFX_LOOP_2() で voice 2 のみ tick (= voice 0/1 を温存して BGM 干渉なし)
//
// 操作:
//   起動 = 4 note ドミソド loop (voice 0、 約 0.6 秒 / 音)
//   joystick port 2 fire = 押下 edge で voice 2 laser SFX 発射 (= sweep down saw)
//     注意: oscar64 sidfx_play は priority 比較が `<=` (= 同 priority も上書き)
//     のため、 fire 押しっぱなしで毎フレーム SIDFX_PLAY を呼ぶと SFX 冒頭で
//     reset され続けて完走しない。main loop 内で前フレーム fire 状態を保持
//     する edge detection (= 押下瞬間 1 回発射) で回避している。
//
// SIDFX struct (= oscar64 audio/sidfx.h、 14 byte fixed):
//   ARRAY BYTE で WORD/BYTE 混在 (= v3b-D の `%` prefix) で組み立てる
//   layout 詳細は C64_SIDFX.LIB のコメント参照
//
// Build (= checkout 状態 / repo root から実行):
//   slangbuild -E c64 -I include examples/c64/SIDOVERLAY.SL -o examples/c64/SIDOVERLAY
//   x64sc -autostart examples/c64/SIDOVERLAY.prg
// 配布物 install 後 (= SLANG_HOME 設定済) は -I include 省略可。
//
// HVSC BGM 併用について:
//   本 sample は voice 0 を SLANG 自前 melody に使っているが、 voice 0/1 を
//   HVSC tune (v3b-B SID_PLAYER_PLAY) に渡して voice 2 SFX と並列稼働させる
//   ことも理論上可能。ただし HVSC tune の多くは 3 voice 全部使うため、
//   voice 2 を skip させる tune-specific hack が必要 (= GoatTracker player の
//   sfx hook 設定等)。一般化困難なので v3b-C scope 外、 user が tune 個別に
//   対応する想定。

#INCLUDE "C64_VIC.LIB"
#INCLUDE "C64_JOY.LIB"
#INCLUDE "C64_SID.LIB"
#INCLUDE "C64_SIDFX.LIB"

CONST NOTE_DURATION = 30;     // frame 数 / 音 (= 30 frame = 約 0.6 sec @ PAL 50Hz)
CONST MELODY_LEN    = 4;      // 4 note loop

// SIDFX struct (= 14 byte) を 1 phase 構成で laser SFX として定義:
//   高音 saw 開始 → 32 tick かけて -64/tick で sweep down → release
ARRAY BYTE FX_LASER[] = {
    %$1500,                                     // freq 開始
    %$0000,                                     // pwm (saw 時は不要、 0 でOK)
    SID_CTRL_GATE OR SID_WF_SAW,                // ctrl: gate on + saw
    SID_ATK_8 OR SID_DKY_72,                    // attdec
    SID_SUS_8 OR SID_DKY_300,                   // susrel
    %$FFC0,                                     // dfreq: -64/tick sweep down (= signed)
    %$0000,                                     // dpwm: 0
    $20,                                        // time1 = 32 tick phase 1 duration
    $00,                                        // time0 = 0
    SIDFX_PRIO_MID                              // priority
};

MAIN()
{
    VAR DIR, NOTE_IDX, NOTE_TIMER, MELODY_FREQ, PREV_FIRE;

    // 初期化
    SID_INIT_QUIET();
    SID_VOLUME(15);
    SIDFX_INIT();
    NOTE_IDX = 0;
    NOTE_TIMER = 0;
    PREV_FIRE = 0;

    PRINT("SIDFX OVERLAY: V0=MELODY V2=SFX");
    PRINT(/);
    PRINT("FIRE=LASER (PORT 2)");
    PRINT(/);

    LOOP
    {
        VIC_WAIT();
        SIDFX_LOOP_2();           // voice 2 sidfx progression (= 毎フレーム必須)

        // voice 0 melody progression: NOTE_DURATION frame ごとに次音
        IF NOTE_TIMER == 0
        {
            // 4 note (C4 / E4 / G4 / C5) を IF chain で選択 (= ARRAY WORD が
            // 未対応な v3b-D 制約下、 CONST 直書きで分岐)
            IF NOTE_IDX == 0 THEN MELODY_FREQ = NOTE_C4;
            IF NOTE_IDX == 1 THEN MELODY_FREQ = NOTE_E4;
            IF NOTE_IDX == 2 THEN MELODY_FREQ = NOTE_G4;
            IF NOTE_IDX == 3 THEN MELODY_FREQ = NOTE_C5;
            SID_SFX(SID_VOICE_1, MELODY_FREQ,
                    SID_ATK_8 OR SID_DKY_240,
                    SID_SUS_8 OR SID_DKY_300,
                    SID_WF_TRI);
            NOTE_IDX = NOTE_IDX + 1;
            IF NOTE_IDX >= MELODY_LEN THEN NOTE_IDX = 0;
        }
        NOTE_TIMER = NOTE_TIMER + 1;
        IF NOTE_TIMER >= NOTE_DURATION THEN NOTE_TIMER = 0;

        // joystick fire で voice 2 SFX 発射 (= 押下 edge detection、 fire 押した
        // 瞬間 1 回だけ発射する debouncing。fire 押しっぱなしで SIDFX_PLAY を毎
        // フレーム呼ぶと oscar64 sidfx の priority 比較が `<=` (= 同 priority も
        // 上書き) のため SFX 冒頭で reset され続けて先に進まない)。
        JOY_POLL(JOY_PORT2);
        DIR = JOY_DIR(JOY_PORT2);
        IF DIR AND JOY_FIRE
        {
            IF PREV_FIRE == 0 THEN SIDFX_PLAY(SIDFX_VOICE_3, FX_LASER, 1);
            PREV_FIRE = 1;
        }
        ELSE
        {
            PREV_FIRE = 0;
        }
    }
}
