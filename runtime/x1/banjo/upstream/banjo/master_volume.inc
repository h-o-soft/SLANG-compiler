
; banjo sound driver
; Joe Kennedy 2024

; set new master volume for the song
; values >= 0x80 are full volume
; lower values are attenuated
; a: new master volume
banjo_set_song_master_volume:

    ; flag that the master volume has been changed
    ld hl, song_state + music_state.flags
    set STATE_FLAG_BIT_MASTER_VOLUME_CHANGE, (hl)

    ; turn the volume into an attenuation
    cp a, 0x80
    jr c, bssmv_not_full

        ; no attenuation
        xor a, a
        jr bssmv_set_value

    bssmv_not_full:

        ; att = 0x7f - vol
        neg
        add a, 0x7f

    bssmv_set_value:

    ; store the new volume
    ld hl, song_state + music_state.master_volume
    ld (hl), a

    ret


; a: fade amount to subtract from master volume attenuation per frame
banjo_song_fade_in:

    neg

; a: fade amount to add to master volume attenuation per frame
banjo_song_fade_out:

    ld hl, song_state + music_state.master_volume_fade
    ld (hl), a

    ret

; update master volume fade
banjo_update_fade:

    push bc

    ; get the fade amount
    ; negative values will fade in (reduce the attenuation)
    ld a, (iy + music_state.master_volume_fade)
    bit 7, a
    jr nz, buf_fade_in

        ; fading out by increasing the attenuation
        add a, (iy + music_state.master_volume)
        
        ; if is this >= 0x80 we're done
        ; otherwise just update the volume
        bit 7, a
        jr z, buf_done

            ; set fade value to 0 and master volume attenuation to 0x7f
            ld (iy + music_state.master_volume_fade), 0
            ld a, 0x7f

            jr buf_done

    buf_fade_in:

        ; fading in by decreasing the attenuation
        add a, (iy + music_state.master_volume)

        ; if the attenuation has gone negative we're done
        ; otherwise just update the volume
        bit 7, a
        jr z, buf_done

            ; set fade value to 0 and master volume attenuation to 0
            xor a, a
            ld (iy + music_state.master_volume_fade), a

    buf_done:

        ; update master volume attenuation
        ; flag that the master volume has been changed
        ld (iy + music_state.master_volume), a
        set STATE_FLAG_BIT_MASTER_VOLUME_CHANGE, (iy + music_state.flags)

        pop bc

        ret