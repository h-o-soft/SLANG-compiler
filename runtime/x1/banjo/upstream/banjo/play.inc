
; banjo sound driver
; Joe Kennedy 2023

; hl: pointer to music data
banjo_play_song:

	; try to ensure we're playing valid data
	ld a, (hl)
	cp a, BANJO_MAGIC_BYTE
	ret nz

	; get channnel count
	; abort if this song uses too many channels
	inc hl
	inc hl
	ld a, (banjo_max_channels)
	cp a, (hl)
	ret c

	; move pointer back
	dec hl
	dec hl

	; mark song as playing
	ld a, 1
	ld (song_playing), a

	; loop mode into b
	ld b, d

	push iy
	push ix

	ld ix, song_channels
	ld iy, song_state
	call music_play

	pop ix
	pop iy

	ret


; b: loop mode
; ix: channels
; iy: state
; hl: pointer to music data
music_play:

	; put music state header address into de
	push iy
	pop de

	; copy over song header
	ld bc, _sizeof_music_state
	ldir

	; init song position variables
	; check if we need to adjust the playback speeds from ntsc -> pal
	ld a, (music_framerate)
	cp a, 60
	jr z, music_play_same_speed
		
		; subtract 1 from speed 1 and speed 2 to roughly match playback
		; playing back on pal rather than ntsc
		ld a, (iy + music_state.speed_1)
		dec a
		ld (iy + music_state.speed_1), a
		
		ld a, (iy + music_state.speed_2)
		dec a
		ld (iy + music_state.speed_2), a

		
	music_play_same_speed:
	
	; preserve channel data pointer
	push ix 

	; point hl at the order_ptrs in the music_state
	push iy
	pop hl
	ld bc, music_state.order_ptrs
	add hl, bc

	; point hl at the first channel's order pointer
	ld a, (hl)
	inc hl
	ld h, (hl)
	ld l, a

	; init channel data
	ld b, (iy + music_state.channel_count)
		
	music_play_channel_data:

		; the following variables are initialised to 0
		xor a, a

		; clear all flags and events bits
		ld (ix + channel.flags), a
		ld (ix + channel.events), a
		
		; default waits to 0
		ld (ix + channel.wait), a

		; default ex_macro_type
		ld (ix + channel.ex_macro_type), a
		
		; the following variables are initialised to 0xff
		dec a

		; default instrument to no instrument (0xff)
		ld (ix + channel.instrument_num), a

		; initialise freq to 0xffff
		ld (ix + channel.freq), a
		ld (ix + channel.freq + 1), a
		
		; move to next channel
		ld de, _sizeof_channel
		add ix, de

		djnz music_play_channel_data
	
	; restore channel data pointer
	pop ix

	; set up pattern
	xor a, a
	call music_jump_channels_order_and_pattern

	;
	; init channel type data
	;

	; preserve channel data pointer
	push ix

	; get pointer to channel init call
	ld l, (iy + music_state.channel_init_call)
	ld h, (iy + music_state.channel_init_call + 1)

	call banjo_jp_hl
		
	; restore channel data pointer
	pop ix

	; set process new line flag
	set STATE_FLAG_BIT_PROCESS_NEW_LINE, (iy + music_state.flags)

	ret

banjo_song_rewind:

	; check the song state to see if the first byte is the magic byte
	ld a, (song_state + music_state.magic_byte)
	cp a, BANJO_MAGIC_BYTE
	ret nz

		push ix
		push iy

		ld ix, song_channels
		ld iy, song_state
		
		xor a, a
		call music_jump_channels_order_and_pattern

		ld b, (iy + music_state.channel_count)
		ld de, _sizeof_channel

		bsrw_channel_loop:

			ld (ix + channel.flags), a
			ld (ix + channel.wait), a

			add ix, de
			djnz bsrw_channel_loop

		pop iy
		pop ix

		ret

; if there's a valid banjo song loaded, resume playback of it
banjo_song_resume:

	; check the song state to see if the first byte is the magic byte
	ld a, (song_state + music_state.magic_byte)
	cp a, BANJO_MAGIC_BYTE
	ret nz

		; resume playback
		ld a, 1
		ld (song_playing), a
	
		ret

; a: loop mode
; 	   0 for loop off
; 	>= 1 for loop on
banjo_set_song_loop_mode:

	ld hl, song_state + music_state.flags

	; check loop mode
	or a, a
	jr nz, bsslm_loop_on

		; looping off
		res STATE_FLAG_BIT_LOOP, (hl)
		ret

	bsslm_loop_on:

		; looping on
		set STATE_FLAG_BIT_LOOP, (hl)
		ret