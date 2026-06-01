
; banjo sound driver
; Joe Kennedy 2024

; ix: channel
; iy: music state
banjo_init_ay:

    ld b, 3

    music_init_ay_loop:

        ; get subchannel number
        ld a, 3
        sub a, b
        ld (ix + channel.subchannel), a

        ; X1: silence the chip channel (volume reg 8/9/10 = 0)。 B=ループカウンタ保持。
        ; psg_write は BC 非破壊なので B はそのまま使える。
        add a, 8
        ld h, a                 ; H = volume register (8/9/10)
        ld l, 0                 ; L = 0 (silence)
        call psg_write

        ; set default channel volume
        ld (ix + channel.volume), 0x0f

        ; move to next channel
        ld de, _sizeof_channel
        add ix, de

        djnz music_init_ay_loop

    ; return to music_play function
    ret


; ix: channel
; iy: music state
banjo_init_sfx_channel_ay:

    ; subchannel
    ld a, (iy + music_state.sfx_subchannel)
    ld (ix + channel.subchannel), a

    ; set default channel volume
    ld (ix + channel.volume), 0x0f

    ; return to music_play function
    ret