; ====================================================================================================
; banjo_opm_x1.asm — banjo OPM (YM2151/FM) chip driver を X1 向けに取り込む SECTION
;
; upstream (upstream/opm/) は MSX 依存の write_calls.inc を内包する banjo_opm.asm を持つが、 それは
; 使わず、 ここで OPM の system 非依存 .inc を個別 include + I/O 層だけ X1 仕様 (write_calls_x1.inc、
; $0700/$0701 直叩き) に差し替える。 ピッチ KC テーブルも X1 FM クロック (4MHz) 補正版
; (divmod_12_opm_x1.inc、 2 半音下げ) を使う。
;
; banjo_driver_x1.asm から `.incdir "<runtime/x1/banjo>"` の状態で include される前提。
; ====================================================================================================

.SECTION "BANJO_OPM" FREE
    .include "upstream/opm/command_jump_table.inc"
    .include "upstream/opm/commands.inc"
    ; KC テーブルは upstream の代わりに X1 4MHz 補正版 (= 2 半音下げ)。
    .include "divmod_12_opm_x1.inc"
    .include "upstream/opm/init.inc"
    .include "upstream/opm/instrument_change.inc"
    .include "upstream/opm/mute_unmute.inc"
    .include "upstream/opm/note_on_off.inc"
    .include "upstream/opm/update_pitch_registers.inc"
    .include "upstream/opm/update.inc"
    .include "upstream/opm/volume_change.inc"
    ; OPM I/O 層: X1 FM ボード $0700/$0701 直叩き (= banjo_opm_write を供給)。
    .include "write_calls_x1.inc"
.ENDS
