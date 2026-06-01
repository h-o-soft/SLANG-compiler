; ====================================================================================================
; banjo_ay_x1.asm — banjo の AY (PSG) driver を X1 向けに取り込む自前 SECTION (refs/banjo 不改変)
;
; refs/banjo/music_driver/ay/banjo_ay.asm は MSX 連番 8bit ポート ($a0/$a1) 前提なので使わず、
; refs/banjo_x1/ay で検証した X1 PSG ($1C00/$1B00) 用 .inc 群を runtime/x1/banjo/ay/ に
; vendoring して取り込む。
; 各 .inc の out(c)/in(c) は全て psg_write/psg_read (psg_write_x1.inc) 経由に置換済。
;
; ピッチ: X1 PSG は 2MHz 駆動。 banjo 同梱テーブルは 3.57MHz (MSX/SMS) / 4MHz (PC88) のみで 2MHz
; が無いので、 3.57MHz 版を ×(2.0/3.579545) 換算した自前 fnums_2mhz_x1.inc (X1 専用) を使う。
; テーブル構造は 3.57 版と同じ A 始まりなので note_on の sub-9 補正が要る → build_driver.sh が
; AY/both build で -D BANJO_3_57MHZ を渡す (= 名前は 3_57 だが「A 始まりテーブル + sub-9」のスイッチとして使うだけ、
; BANJO_MSX とは別物なので MSX 依存は発火しない)。
; ※経緯: OPM は X1=4MHz だったが PSG は別チップで 2MHz。 4MHz 版は +2 半音、 3.57 版もまだ高く、
;   2MHz 換算で一致することを実機確認済み。
; ====================================================================================================

.SECTION "BANJO_AY" FREE
    .include "ay/fnums_2mhz_x1.inc"
    .include "ay/command_jump_table.inc"
    .include "ay/commands.inc"
    .include "ay/commands_envelopes.inc"
    .include "ay/init.inc"
    .include "ay/mute_unmute.inc"
    .include "ay/note_on_off.inc"
    .include "ay/update.inc"
    .include "ay/update_pitch_registers.inc"
    .include "ay/volume_change.inc"
    .include "ay/psg_write_x1.inc"
.ENDS
