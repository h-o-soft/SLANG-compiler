
; banjo sound driver
; Joe Kennedy 2025

; ix: channel
; iy: music state
banjo_init_opm:

    ld b, 8
    ld de, _sizeof_channel

    music_init_opm_loop:

        ; default volume
        ld (ix + channel.volume), 0x7f

        ; subchannel number
        ld a, 8
        sub a, b
        ld (ix + channel.subchannel), a

        ; move to next channel
        add ix, de

        djnz music_init_opm_loop

    ; init pms/ams depth to max
    ld h, 0x19
    ld l, 0x7f
    call banjo_opm_write

    ld h, 0x19
    ld l, 0xff
    call banjo_opm_write

    ; clear noise register
    ld h, 0x0f
    ld l, 0x00
    call banjo_opm_write

    ret
