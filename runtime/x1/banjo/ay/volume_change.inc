
; banjo sound driver
; Joe Kennedy 2024

; ix: current channel
; iy: music state
music_volume_change_ay:

    ; calculate volume w/ volume macros and master volume
    ld a, (iy + music_state.master_volume)
    call music_calculate_volume_0x0f

    ; combine new volume with envelope flag
    ld c, a
    ld a, (ix + channel.volume)
    and a, 0xf0
    or a, c                     ; A = combined volume value

    ; X1: H=reg, L=val で psg_write (port/inc c は不要)
    ld l, a                     ; L = volume value
    ld a, (ix + channel.subchannel)
    add a, 8
    ld h, a                     ; H = volume register (8/9/10)
    call psg_write

	jp music_volume_change_ay_done