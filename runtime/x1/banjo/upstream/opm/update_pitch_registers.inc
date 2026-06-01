
; banjo sound driver
; Joe Kennedy 2024

; iy: music_state
; ix: channel we're updating
music_update_pitch_registers_opm:

    push bc

    ; get whole note in d, fraction in e
    call music_calc_fnum

    ; key fraction register in c
    ld a, (ix + channel.subchannel)
    and a, 0x7
    add a, 0x30
    ld c, a

    ; put into h
    ld h, a

    ; get key fraction in l
    ld l, e

    ; write to register
    call banjo_opm_write

    ; get keycode
    ; note number (0-14) in lower nibble and octave in upper nibble
    ; using divmod table
    ld a, d
    and a, 0x7f
    add a, <music_divmod12_opm
    ld l, a
    adc a, >music_divmod12_opm
    sub a, l
    ld h, a
    ld l, (hl)

    ; get key code register in h
    ld a, c
    sub a, 8
    ld h, a

    ; write to register
    call banjo_opm_write

    pop bc

    jp music_update_pitch_registers_opm_done
    