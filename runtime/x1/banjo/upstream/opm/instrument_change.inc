
; banjo sound driver
; Joe Kennedy 2025

; bc : jump table address (preserve this!)
; de : instruction pointer (preserve this!)
mpnl_opm_instrument_change:

	; check if the channel is muted and skip command if it is
	bit CHAN_FLAG_BIT_MUTED, (ix + channel.flags)
	jp nz, mpnl_skip_2_byte_command

	; don't do anything if the instrument number is the same
	ld a, (de)
	cp a, (ix + channel.instrument_num)
	jp z, mpnl_skip_2_byte_command

        ; preserve bc and de
        push bc
        push de

    	; update the instrument
		call music_instrument_change

        ; coming out of music_instrument_change, hl should be pointing at the instrument data
        ; at the start of the fm information

		; get address of patch data into hl
		inc hl
		ld a, (hl)
		inc hl
		ld h, (hl)
		ld l, a

        ; patch pointed into de
        ex de, hl

        ; 0x20 - fb/connect
        ; select register for this channel
        ld a, (ix + channel.subchannel)
        and a, 0x7
        add a, 0x20

        ld h, a
        ld a, (de)
        ld l, a
        call banjo_opm_write

        ; move patch pointer on
        inc de

        ; 0x38 - pms/ams
        ; move register to correct number
        ld a, h
        add a, 0x18
        ld h, a

        ld a, (de)
        ld l, a
        call banjo_opm_write

        ; move patch pointer on
        inc de

        ; operators loop
        ld b, 6 * 4

        mpnm_opm_ic_ops_loop:

            ; move register to next operator slot
            ld a, h
            add a, 0x8
            ld h, a

            ld a, (de)
            ld l, a
            call banjo_opm_write

            ; move patch pointer on
            inc de

            djnz mpnm_opm_ic_ops_loop

        ; patch pointer in hl
        ex de, hl

        ; operator enable value for key-on
        ; combine with subchannel
        ld a, (ix + channel.subchannel)
        and a, 0x7
        or a, (hl)
        ld (ix + channel.subchannel), a

        inc hl

        ; store panning/alg/fb register
        ld a, (hl)
        ld (ix + channel.gp1), a

        inc hl

        ; store pointer to rest of patch data
		; used for volume changes
		ld (ix + channel.gp2), l
		ld (ix + channel.gp3), h

        ; restore bc and de
        pop de
        pop bc

	mpnl_opl_2op_instrument_change_done:

	jp mpnl_command_done
