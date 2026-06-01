
; banjo sound driver
; Joe Kennedy 2024

; bc : jump table address (preserve this!)
; de : instruction pointer (preserve this!)
; ix : channel pointer
; iy : pointer to music state
mpnl_note_on_ay:

    .ifdef BANJO_3_57MHZ

		; sub 9 from the note number as the 3.57mhz pitch table starts at the note A
		sub a, 9
		jr nc, mpl_no_ay_nc

			xor a, a

		mpl_no_ay_nc:

    .endif

	; standard note-on stuff
	call music_note_on

	jp mpnl_command_done

; bc : jump table address (preserve this!)
; de : instruction pointer (preserve this!)
; ix : pointer to current channel
; iy : pointer to music state
mpnl_note_off_ay:

	; clear note-on bit
    res CHAN_FLAG_BIT_NOTE_ON, (ix + channel.flags)

    ; check if the channel is muted
	bit CHAN_FLAG_BIT_MUTED, (ix + channel.flags)
	jr nz, mnoff_sn_dont_write_chip

		; X1: 該当 ch の volume reg (8/9/10) に 0 を書いて消音 + envelope off。
		; command handler なので DE (instruction ptr) は触らない。 psg_write は H/L のみ使用。
		ld a, (ix + channel.subchannel)
		add a, 8
		ld h, a                 ; H = volume register
		ld l, 0                 ; L = 0 (volume 0 + env off)
		call psg_write

	mnoff_sn_dont_write_chip:
	
	jp mpnl_command_done_one_byte_command
