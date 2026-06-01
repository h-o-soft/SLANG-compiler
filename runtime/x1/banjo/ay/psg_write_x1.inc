; ====================================================================================================
; psg_write_x1.inc — X1 PSG (AY-3-8910) レジスタ read/write (= runtime WRTPSG 同等 + read)
;
; X1 PSG: アドレスポート $1C00 / データポート $1B00 (16bit I/O、 非連番)。 X1 機能 I/O 表より
; $1B**=PSG データ (R/W)、 $1C**=PSG レジスタ指定 (R/W) なので read も可能。
;
; ★ABI は H=reg, L=val 形式 (= OPM の banjo_opm_write と同じ)。 A/E 形式にしないこと:
;   banjo の AY command handler は DE = instruction pointer (曲データ読取位置、 "preserve this!")
;   なので、 ld e,val すると DE 下位を壊して曲が暴走する。 HL は command handler の scratch。
;
; psg_write: H=register number, L=value。
;   ★DE / BC / IX / IY 非破壊 (内部は A と BC のみ使用、 BC は push/pop)。 H/L は入力で使う。
; psg_read : H=register number → A=value。
;   ★HL / DE / BC / IX / IY 全て非破壊 (内部は A と BC のみ、 BC は push/pop)。 結果は A のみ。
;     → read-modify-write で「H=reg 保持のまま read→A 加工→L=A→psg_write」が綺麗に書ける。
; ====================================================================================================

; H=reg, L=val を書く。 A/BC のみ使用 (BC は保存)、 DE/HL/IX/IY 非破壊。
psg_write:
    push bc
    ld a, h
    ld bc, $1c00
    out (c), a          ; アドレスラッチ ($1C00) = reg
    dec b               ; bc = $1b00 (データポート)
    ld a, l
    out (c), a          ; データ書込 ($1B00) = val
    pop bc
    ret

; H=reg を選び、 そのレジスタ値を A に読む。 A/BC のみ使用 (BC は保存)、 HL/DE/IX/IY 非破壊。
psg_read:
    push bc
    ld a, h
    ld bc, $1c00
    out (c), a          ; アドレスラッチ ($1C00) = reg
    dec b               ; bc = $1b00 (データポート)
    in a, (c)           ; データ読出 ($1B00) → A
    pop bc
    ret
