
; banjo sound driver
; Joe Kennedy 2023

; bc: jump table to handle commands (e.g. music_command_jump_table_sn)
; ix: channels
; iy: state
music_process_new_line:

    ; note delay?
    bit CHAN_EVENT_BIT_TIC_WAIT, (ix + channel.events)
    jr z, mpnl_no_tic_wait

        ; decrement tic wait and return if not zero
        dec (ix + channel.wait)
        ret nz

			; tic wait is now zero
			; reset tic wait flag and continue
			res CHAN_EVENT_BIT_TIC_WAIT, (ix + channel.events)
			jr music_process_new_line_continue

    mpnl_no_tic_wait:

    ; process new line if necessary
	bit STATE_FLAG_BIT_PROCESS_NEW_LINE, (iy + music_state.flags)
    ret z

		; is there a line wait?
		ld a, (ix + channel.wait)
		or a, a
		jr z, music_process_new_line_continue

			; decrease line wait and return if not zero
			dec (ix + channel.wait)
			ret

    music_process_new_line_continue:

	; get current pattern pointer for this channel
	ld e, (ix + channel.pattern_ptr)
	ld d, (ix + channel.pattern_ptr + 1)

	mpnl_loop:
	
		; get command
		ld a, (de)
		
		; if it's < 32 it's a jump table command
		cp a, 32
		jr c, mpnl_use_jump_table
		
		; is this a note on? (bit 7 set)
		bit 7, a
		jp z, mpnl_not_note_on
			
			; offset jump table address to note-on command
			ld l, c
			ld h, b

			; jump to jump table address
			jp (hl)
			
		; is the end of this line for this channel? (bit 6 set)
		; if not, use jump table
		mpnl_not_note_on:
		bit 6, a
		jp z, mpnl_not_eol

			; end of this line reached
			; get line_wait amount and set it in channel
			and a, 0x3f
			ld (ix + channel.wait), a

			; store the pointer to the next command and return
			inc de
			ld (ix + channel.pattern_ptr), e
			ld (ix + channel.pattern_ptr + 1), d

			ret
			
		; must be a volume change (bit 5 set)
		mpnl_not_eol:
			
			; offset jump table address to volume change command
			ld hl, 9
			add hl, bc
			
			; jump to jump table address
			jp (hl)

		; if not, process the command
		mpnl_use_jump_table:

			; address in table is command num * 3
			; use it to offset into the jump table
			; hl = (a * 3) + bc
			ld l, a
			add a, a
			add a, l
			add a, c
			ld l, a
			adc a, b
			sub a, l
			ld h, a

			; move pattern pointer on
			inc de

			; jump to jump table address
			jp (hl)

		mpnl_command_done:
		
			inc de

		mpnl_command_done_one_byte_command:
			
			jr mpnl_loop
