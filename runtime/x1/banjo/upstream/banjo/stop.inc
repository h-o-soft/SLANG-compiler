
; banjo sound driver
; Joe Kennedy 2023

; stop the currently playing song and mute all channels
banjo_song_stop:

    ; do nothing if a song isn't playing
    ld a, (song_playing)
    or a, a
    ret z

        push ix
        push iy

        ld ix, song_channels
        ld iy, song_state

		; get pointer to channel init call
		ld l, (iy + music_state.song_mute)
		ld h, (iy + music_state.song_mute + 1)

		; run call
        call banjo_jp_hl

        pop iy
        pop ix        

        xor a, a
        ld (song_playing), a

        ret