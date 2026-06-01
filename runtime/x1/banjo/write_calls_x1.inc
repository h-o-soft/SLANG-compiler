; ====================================================================================================
; write_calls_x1.inc — banjo OPM driver の X1 向け I/O 層 (refs/banjo は不改変、 これで差し替え)
;
; banjo の opm/write_calls.inc は MSX slot write 前提なので使わず、 本 file の banjo_opm_write を
; banjo_opm_x1.asm から include する。 banjo の OPM .inc 群は全て `call banjo_opm_write` 経由で
; chip を叩くので、 ここだけ X1 仕様にすれば OPM 全機能が X1 で動く。
;
; X1 FM 音源ボード (CZ-8BS1 等) の YM2151 (OPM):
;   $0700 = アドレスポート (W)
;   $0701 = データ (W) / ステータス (R)
; X1 は 16bit I/O なので必ず BC 経由 (in/out a,(n) は n<=255 のみで $0701 は表現不可)。
; 連続 write の取りこぼし回避に、 各 write 前に status bit7 (busy) clear を待つ。
; ====================================================================================================

; banjo_opm_write: H = register number, L = value
banjo_opm_write:
    push bc
.wait_reg:
    ld bc, $0701
    in a,(c)            ; read status
    rlca                ; bit7 (busy) -> carry
    jr c,.wait_reg
    ld bc, $0700
    ld a,h
    out (c),a           ; latch register number
.wait_dat:
    ld bc, $0701
    in a,(c)
    rlca
    jr c,.wait_dat
    ld a,l
    out (c),a           ; write data
    pop bc
    ret
