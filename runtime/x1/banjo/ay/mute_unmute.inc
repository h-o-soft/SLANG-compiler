
; banjo sound driver
; Joe Kennedy 2024

; ix: channel
; iy: music_state
banjo_mute_channel_ay:

    ; X1: 該当 ch の volume reg (8/9/10) に 0 を書いて消音
    ld a, (ix + channel.subchannel)
    add a, 8
    ld h, a                 ; H = volume register
    ld l, 0                 ; L = 0
    call psg_write

    ret

; ix: channel
; iy: music state
banjo_unmute_channel_ay:

    ; reset muted bit
    res CHAN_FLAG_BIT_MUTED, (ix + channel.flags)
    
    ret

; mute all ay channels
banjo_mute_all_ay:

    ; X1: volume reg 8/9/10 に 0 を書く。 ループは H=reg を 8→10 で回す。
    ; psg_write は BC 非破壊なので B をループカウンタに使える (D は不要)。
    ld h, 8                 ; H = first volume register
    ld b, 3

    music_mute_all_ay_loop:

        ld l, 0             ; L = 0 (silence)
        call psg_write      ; H/L で書込 (H 保持)
        inc h               ; next volume register
        djnz music_mute_all_ay_loop

    ret