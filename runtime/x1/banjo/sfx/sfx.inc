
; banjo sound driver
; Joe Kennedy 2024

banjo_sfx_init:

    ; no sfx should be playing initially
    ld a, 0
    ld (sfx_playing), a
    ld (sfx_priority), a
    
    ; clear first byte which has the magic byte
    ld (sfx_state), a

    ret

; hl: pointer to music data
banjo_play_sfx:

    ; try to ensure we're playing valid data
    ld a, (hl)
    cp a, BANJO_MAGIC_BYTE
    ret nz

        ; mark sfx as playing
        ld a, 1
        ld (sfx_playing), a
        
        push ix
        push iy

        ld ix, sfx_channel
        ld iy, sfx_state
        call music_play

        ; for sfx we just need to mute a channel
        ld a, (iy + music_state.sfx_channel)
        call banjo_mute_song_channel_from_sfx

        pop iy
        pop ix

        ret

; stop the currently playing sfx, muting its channel
; if a song is playing, unmute the corresponding channel
banjo_sfx_stop:

    ; do nothing if an sfx isn't playing
    ld a, (sfx_playing)
    or a, a
    jr z, banjo_sfx_stop_no_sfx

        push iy
        push ix

        ld ix, sfx_channel
        ld iy, sfx_state

        ; mute sfx channel to stop any hanging notes

        ; get address of mute function
        ld l, (iy + music_state.channel_mute_calls)
        ld h, (iy + music_state.channel_mute_calls + 1)
        ld a, (hl)
        inc hl
        ld h, (hl)
        ld l, a

        ; call mute function
        call banjo_jp_hl

        ; check if a song is playing
        ld a, (song_playing)
        or a, a
        jr z, banjo_sfx_stop_done
            
            ; get the sfx channel
            ld a, (iy + music_state.sfx_channel)

            ; unmute the channel in the song
            call banjo_unmute_song_channel

        banjo_sfx_stop_done:

        pop ix
        pop iy

    banjo_sfx_stop_no_sfx:

    ld a, 0
    ld (sfx_playing), a
    ld (sfx_priority), a
    
    ret

; update the sfx if one is playing
banjo_update_sfx:

    ; check if a sfx is playing
    ; return if it's not
    ld a, (sfx_playing)
    or a, a
    ret z

        ; if it is, run the updates
        push ix
        push iy

        ld ix, sfx_channel
        ld iy, sfx_state
        call music_update

        pop iy
        pop ix

        ret

; a: loop mode
; 	   0 for loop off
; 	>= 1 for loop on
banjo_set_sfx_loop_mode:

	ld hl, sfx_state + music_state.flags

	; check loop mode
	or a, a
	jr nz, bssfxlm_loop_on

		; looping off
		res STATE_FLAG_BIT_LOOP, (hl)
		ret

	bssfxlm_loop_on:

		; looping on
		set STATE_FLAG_BIT_LOOP, (hl)
		ret

; set new master volume for the sfx
; values >= 0x80 are full volume
; lower values are attenuated
; a: new master volume
banjo_set_sfx_master_volume:

    ; flag that the master volume has been changed
    ld hl, sfx_state + music_state.flags
    set STATE_FLAG_BIT_MASTER_VOLUME_CHANGE, (hl)

    ; store the new volume
    ld hl, sfx_state + music_state.master_volume
    ld (hl), a

    ret


; separate call which preserves iy = sfx_state
; jumpt into banjo_mute_song_channel
; a: channel to mute
banjo_mute_song_channel_from_sfx:

    ; preserve channel number
    ld l, a

    ; check if the song is playing 
    ; do nothing if it isn't
    ld a, (song_playing)
    or a, a
    ret z

    push ix
    push iy

    jp bmsc_sfx_jump