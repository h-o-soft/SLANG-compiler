
; banjo sound driver
; Joe Kennedy 2024

; ix: channel
; iy: music_state
music_update_pitch_registers_ay:

    push bc

    ; calculate note number
    call music_calc_fnum

    ; turn that into an fnum and octave
    ld hl, ay_tone_lookup
    call music_fnum_lookup

    ; shift fnum to the correct octave
    call music_fnum_shift_octave

    ; X1: HL = fnum (上記後)。 本 routine は command handler ではない (= music_update から呼ばれる)
    ; ので DE は instruction pointer ではなく、 fnum 退避に使ってよい (★command 系では DE 退避厳禁)。
    ld d, h                     ; D = coarse value (fnum high)
    ld e, l                     ; E = fine value  (fnum low)

    ; fine pitch: reg = subchannel*2 (0/2/4)、 val = fine
    ld a, (ix + channel.subchannel)
    add a, a
    ld h, a                     ; H = fine pitch register
    ld l, e                     ; L = fine value
    call psg_write

    ; coarse pitch: reg+1 (1/3/5)、 val = coarse
    inc h                       ; H = coarse pitch register
    ld l, d                     ; L = coarse value
    call psg_write

    pop bc

    jp music_update_pitch_registers_ay_done
