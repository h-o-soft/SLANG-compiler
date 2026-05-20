// Commodore 64 HVSC BGM + SIDFX SE overlay demo (oscar64 backend、 v3b-C)
//
// v3b-B SIDBGM.SL + v3b-C SIDFX_LOOP_2 を併用、 BGM 再生中に voice 2 SFX
// (= laser) を fire 押下で重ね鳴らす「BGM + SE」典型ゲーム構成の最小 sample。
//
// 構成:
//   voice 0/1/2 = HVSC tune player が 3 voice 全部使う
//   voice 2 SFX = SIDFX_PLAY で tune の voice 2 を上書き
//   SID_PLAYER_PLAY()  = BGM 3 voice 進行 (= 毎フレーム)
//   SIDFX_LOOP_2()     = voice 2 のみ sidfx tick (= voice 0/1 BGM 温存)
//
// 同居の振る舞い (= 一般 HVSC tune での観測):
//   - BGM 鳴ってる区間 (= voice 2 が音出してる) → SFX が「プゥーン」と sweep 完走、
//     ただし BGM tune が毎フレーム voice 2 register を update するので SFX 音色は
//     部分的に上書きされ「時々途切れる」感じになる
//   - BGM 無音区間 (= voice 2 が GATE OFF / silence) → SFX 「プッ」と発射時の
//     attack だけ鳴って即消える (= BGM player が voice 2 silence を毎フレーム
//     書き込むため SIDFX 内部 phase は進行しても SID register effect が消える)
//   - SFX 完了後 = tune player が次フレームで voice 2 復帰
//   - tune voice 2 が melody / bass 等の重要パートだと欠落が目立つ
//   - voice 2 が伴奏程度 (or 使わない) の tune の方が SFX overlay 自然
//   - tracker 製 tune (GoatTracker 等) には SFX hook で voice 2 を player 側で
//     skip する build option もある、 そういう tune なら破綻なし overlay 可能
//
// 重要 (= 既知制約、 今後改善予定):
//   ARRAY BYTE FX_LASER (= 14 byte) の oscar64 BSS 配置が HVSC tune の
//   loadAddress (= 多くが $1000 系) と衝突するため、 sample 全体の code/data
//   を極小化して BSS を $0FF0 未満に押し込む形で回避。本来は SLANG / oscar64
//   側で BSS placement を $2000 以降に強制する仕組みが必要 (= 別 issue 課題)。
//   そのため本 sample は機能最小限 (= error 表示 / song 切替 等を削除)。
//
// 操作:
//   起動 = HVSC tune の song 0 で BGM 再生
//   joystick port 2 fire (= 押下 edge) = voice 2 laser SFX 発射
//
// 用意するもの:
//   - HVSC 等 PSID v2 .sid (= 4KB 以内、 playAddress != 0) を 1 つ user 持込
//   - c1541 で D64 化:
//       c1541 -format "bgmfx,01" d64 /tmp/sidbgmsfx.d64 \
//             -write examples/c64/SIDBGM_SFX.prg sidbgmfx \
//             -write your_tune.sid music
//       x64sc -8 /tmp/sidbgmsfx.d64
//       LOAD"sidbgmfx",8
//       RUN
//
// Build:
//   slangbuild -E c64 -I include examples/c64/SIDBGM_SFX.SL -o examples/c64/SIDBGM_SFX

#INCLUDE "C64_KIO.LIB"
#INCLUDE "C64_JOY.LIB"
#INCLUDE "C64_SID.LIB"
#INCLUDE "C64_SIDFX.LIB"

CONST BUF_ADDR = $C000;

// SIDFX struct (= 14 byte、 SIDOVERLAY.SL と同じ laser SFX)
ARRAY BYTE FX_LASER[] = {
    %$1500, %$0000,
    SID_CTRL_GATE OR SID_WF_SAW,
    SID_ATK_8 OR SID_DKY_72,
    SID_SUS_8 OR SID_DKY_300,
    %$FFC0, %$0000,
    $20, $00,
    SIDFX_PRIO_MID
};

MAIN()
{
    VAR N, DIR, PREV_FIRE;

    // disk から "music" PRG を $C000 領域に直 read → SID 起動 → BGM 再生
    KIO_OPEN_NAMED(2, DEV_DISK, CH_READ, "music,p,r");
    KIO_CHKIN(2);
    N = KIO_READ_ADDR(2, BUF_ADDR, 4096);
    KIO_CLRCHN();
    KIO_CLOSE(2);

    SID_LOAD_FROM_BUF_ADDR(BUF_ADDR, N);
    SID_INIT_QUIET();
    SID_VOLUME(15);
    SIDFX_INIT();
    SID_PLAYER_INIT(0);
    PREV_FIRE = 0;

    // main loop: BGM tick + voice 2 SFX tick + fire edge detection
    LOOP
    {
        VIC_WAIT();
        SID_PLAYER_PLAY();
        SIDFX_LOOP_2();

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
