
; banjo sound driver
; Joe Kennedy 2025

; a : midi note
; bc : jump table address (preserve this!)
; de : instruction pointer (preserve this!)
; ix : pointer to current channel
; iy : pointer to music state
mpnl_note_on_opm:

    ; check if the channel is muted
	bit CHAN_FLAG_BIT_MUTED, (ix + channel.flags)
	jr nz, mnon_opm_dont_write_chip

		; send note off if there's a note currently playing and we're not playing legato
		; and the channel isn't muted
		bit CHAN_FLAG_BIT_NOTE_ON, (ix + channel.flags)
		jr z, mnon_opm_legato_done

			; check for legato
			bit CHAN_EVENT_BIT_LEGATO, (ix + channel.events)
			jr nz, mnon_opm_legato_done
		
				; send note off
				ld h, 0x8
				ld a, (ix + channel.subchannel)
				and a, 0x7
				ld l, a
				call banjo_opm_write

	mnon_opm_legato_done:

    ; send note on
    ld h, 0x8
    ld l, (ix + channel.subchannel)
    call banjo_opm_write

	; standard note-on stuff
	ld a, (de)
	call music_note_on

	mnon_opm_dont_write_chip:

	jp mpnl_command_done


; bc : jump table address (preserve this!)
; de : instruction pointer (preserve this!)
; ix : pointer to current channel
; iy : pointer to music state
mpnl_note_off_opm:

	; clear note-on bit
    res CHAN_FLAG_BIT_NOTE_ON, (ix + channel.flags)

    ; check if the channel is muted
	bit CHAN_FLAG_BIT_MUTED, (ix + channel.flags)
	jr nz, mnoff_opm_dont_write_chip

        ; send note off
        ld h, 0x8
		ld a, (ix + channel.subchannel)
		and a, 0x7
		ld l, a
        call banjo_opm_write

	mnoff_opm_dont_write_chip:

	jp mpnl_command_done_one_byte_command
	