; ====================================================================================================
; banjo_sfx_x1.asm — banjo SFX エンジンを X1 向けに取り込む自前 SECTION (refs/banjo 不改変)
;
; refs/banjo/music_driver/sfx/banjo_sfx.asm は自前で defines を include し RAMSECTION/SECTION を
; 完結させる単独モジュール構造なので使わず、 sfx.inc 本体だけを runtime/x1/banjo/sfx/ に vendoring し、
; X1 build の include 順 (defines は master が既に include 済) とガード方針に合わせて本ラッパで包む
; (= banjo_ay_x1.asm と同思想)。
;
; SFX は AY (PSG) 専用 (ユーザ選択)。 本 file は banjo_driver_x1.asm の `.ifdef BANJO_USE_AY`
; ブロック内 (= banjo_ay_x1.asm の後、 banjo Core "banjo.asm" の include より後) で取り込む。
;
; sfx.inc が直接参照する外部 symbol は全て Core (banjo.asm) 側に存在:
;   music_play / music_update / banjo_jp_hl / song_playing / banjo_unmute_song_channel /
;   bmsc_sfx_jump (banjo_mute_song_channel 内)。 define (BANJO_MAGIC_BYTE / STATE_FLAG_BIT_LOOP /
;   STATE_FLAG_BIT_MASTER_VOLUME_CHANGE) と struct (music_state / channel) も defines で定義済。
;
; SFX 状態 RAM は banjo_init が 0 化しない (banjo_init は song_playing のみ初期化) ので、
; wrapper の banjo_x1_init が CTC 有効化前に banjo_sfx_init を呼んで初期化する (banjo_x1_wrapper.asm)。
; ====================================================================================================

; SFX 状態/ch データ。 Core の "BANJO_RAM" (song_state を持つ) に APPENDTO で連結 → linkfile の
; [ramsections] が "BANJO_RAM" を SLOT1 (= BANJO_RAM_BASE) に固定するのでそのまま乗る。
.RAMSECTION "BANJO_RAM_SFX" APPENDTO "BANJO_RAM" FREE
    sfx_playing:  db
    sfx_priority: db
    sfx_state    INSTANCEOF music_state
    sfx_channel  INSTANCEOF channel
.ENDS

.SECTION "BANJO_SFX" FREE
    .include "sfx/sfx.inc"
.ENDS
