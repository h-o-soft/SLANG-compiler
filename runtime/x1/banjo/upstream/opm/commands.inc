
; banjo sound driver
; Joe Kennedy 2025

; bc : jump table address (preserve this!)
; de : instruction pointer (preserve this!)
mpnl_opm_volume_change:

	; check if the channel is muted and skip command if it is
	bit CHAN_FLAG_BIT_MUTED, (ix + channel.flags)
	jp nz, mpnl_skip_2_byte_command

		; get new volume
		inc de
		ld a, (de)
		and a, 0x7f

		; don't do anything if the volume is the same
		cp a, (ix + channel.volume)
		jp z, mpnl_command_done

			; store new channel volume
			ld (ix + channel.volume), a

			; set bit to indicate a channel volume change event
			set CHAN_EVENT_BIT_VOLUME_CHANGE, (ix + channel.events)

			jp mpnl_command_done
			
; bc : jump table address (preserve this!)
; de : instruction pointer (preserve this!)
mpnl_opm_pan:

	; check if the channel is muted and skip command if it is
	bit CHAN_FLAG_BIT_MUTED, (ix + channel.flags)
	jp nz, mpnl_skip_2_byte_command

		; get pan value
		ld a, (de)
		ld l, a

		; get alg/fb register value
		ld a, (ix + channel.gp1)
		and a, 0x3f
		or a, l
		ld l, a

		ld a, (ix + channel.subchannel)
		and a, 0x7
		add a, 0x20
		ld h, a
		
		call banjo_opm_write

		jp mpnl_command_done

; bc : jump table address (preserve this!)
; de : instruction pointer (preserve this!)
mpnl_opm_noise:

	; check if the channel is muted and skip command if it is
	bit CHAN_FLAG_BIT_MUTED, (ix + channel.flags)
	jp nz, mpnl_skip_2_byte_command

		; get noise value and write it
		ld a, (de)
		ld l, a
		ld h, 0x0f
		call banjo_opm_write

		jp mpnl_command_done


; bc : jump table address (preserve this!)
; de : instruction pointer (preserve this!)
mpnl_opm_lfo_rate:

	; check if the channel is muted and skip command if it is
	bit CHAN_FLAG_BIT_MUTED, (ix + channel.flags)
	jp nz, mpnl_skip_2_byte_command

		; get lfo value
		ld a, (de)
		ld l, a
		ld h, 0x18
		call banjo_opm_write

		jp mpnl_command_done


; bc : jump table address (preserve this!)
; de : instruction pointer (preserve this!)
mpnl_opm_lfo_wave:

	; check if the channel is muted and skip command if it is
	bit CHAN_FLAG_BIT_MUTED, (ix + channel.flags)
	jp nz, mpnl_skip_2_byte_command
		
		; get wave value
		ld a, (de)
		ld l, a
		ld h, 0x1b
		call banjo_opm_write

		jp mpnl_command_done
