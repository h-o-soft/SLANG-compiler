
; banjo sound driver
; Joe Kennedy 2023


banjo_jp_hl:
    jp (hl)

; update the song if one is playing
banjo_update_song:

	; check if a song is playing
	; return if it's not
	ld a, (song_playing)
	or a, a
	ret z

		; if it is, run the updates
		push ix
		push iy

		ld ix, song_channels
		ld iy, song_state
		call music_update

		pop iy
		pop ix

		ret

; update function to be called every frame
; ix: channels
; iy: music state
music_update:

	; move to next tic
	dec (iy + music_state.tic)
	
	; check if we need to move to the next line
	jr nz, music_update_tics_done

		; signal that we need to process the next line
		set STATE_FLAG_BIT_PROCESS_NEW_LINE, (iy + music_state.flags)

		; check if we need to jump to a new order
		bit STATE_FLAG_BIT_ORDER_JUMP, (iy + music_state.flags)
		jr z, music_update_no_jump

			call music_jump_channels_order_and_pattern
		
			jr music_update_tics_done

		music_update_no_jump:

		; move to next line
		dec (iy + music_state.line)

		; line count is 0 means a new pattern
		jr z, music_update_pattern_done

			; on to next line in pattern
			ld a, (iy + music_state.line)
			and a, 0x1
			jr nz, music_update_new_line_odd_speed

				ld a, (iy + music_state.speed_1)
				jr music_update_new_line_speed_done

			music_update_new_line_odd_speed:

				ld a, (iy + music_state.speed_2)

			music_update_new_line_speed_done:

			; update tic count with speed
			ld (iy + music_state.tic), a

			; done for now
			jr music_update_tics_done

		music_update_pattern_done:

			ld a, (iy + music_state.order)
			inc a

			; check if we've reached the last order
			cp a, (iy + music_state.orders_length)
			jr z, mu_last_order
			
				; not the last order
				; move on to next pattern
				call music_jump_channels_order_and_pattern
				jr music_update_tics_done
			
			; we've reached the last order
			mu_last_order:

				; check if this music is looping or not
				bit STATE_FLAG_BIT_LOOP, (iy + music_state.flags)
				jr z, mu_no_loop

					; the music does loop - so go back to first patterns
					xor a, a
					call music_jump_channels_order_and_pattern
					jr music_update_tics_done
				
				mu_no_loop:

					; get pointer to song stop call
					ld l, (iy + music_state.song_stop)
					ld h, (iy + music_state.song_stop + 1)

					; run call
					jp (hl)
			
	music_update_tics_done:

		.ifndef BANJO_MINIMAL

		; check if there's a fade occurring
		ld a, (iy + music_state.master_volume_fade)
		or a, a
		call nz, banjo_update_fade

		.endif

		; get pointer to channel update call & jump to it
		ld l, (iy + music_state.channel_update_call)
		ld h, (iy + music_state.channel_update_call + 1)
		call banjo_jp_hl

		; clear process new line flag
		; clear master volume change flag
		ld a, (iy + music_state.flags)
		and a, ~(STATE_FLAG_PROCESS_NEW_LINE | STATE_FLAG_MASTER_VOLUME_CHANGE)
		ld (iy + music_state.flags), a

		ret