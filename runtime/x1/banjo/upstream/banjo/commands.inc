
; banjo sound driver
; Joe Kennedy 2023

; bc : jump table address (preserve this!)
; de : instruction pointer (preserve this!)
mpnl_instrument_change:

	; check if the channel is muted and skip command if it is
	bit CHAN_FLAG_BIT_MUTED, (ix + channel.flags)
	jp nz, mpnl_skip_2_byte_command

		; don't do anything if the instrument number is the same
		ld a, (de)
		cp a, (ix + channel.instrument_num)
		call nz, music_instrument_change

		jp mpnl_command_done

; bc : jump table address (preserve this!)
; de : instruction pointer (preserve this!)
mpnl_volume_change:

	; get new volume
	ld a, (de)
	and a, 0xf

	; don't do anything if the volume is the same
	cp a, (ix + channel.volume)
	jp z, mpnl_command_done

		; store new channel volume
		ld (ix + channel.volume), a

		; set bit to indicate a channel volume change event
		set CHAN_EVENT_BIT_VOLUME_CHANGE, (ix + channel.events)

		jp mpnl_command_done
	

.ifndef BANJO_MINIMAL

; bc : jump table address (preserve this!)
; de : instruction pointer (preserve this!)
mpnl_pitch_slide_up:
	
	; set slide_type = 1
	ld (ix + channel.effect1), SLIDE_TYPE_UP

	jr mpnl_slide_common

; bc : jump table address (preserve this!)
; de : instruction pointer (preserve this!)
mpnl_pitch_slide_down:
	
	; set slide_type = 2
	ld (ix + channel.effect1), SLIDE_TYPE_DOWN

	jr mpnl_slide_common

; bc : jump table address (preserve this!)
; de : instruction pointer (preserve this!)
mpnl_portamento:

	; set slide_type = 4
	ld (ix + channel.effect1), SLIDE_TYPE_PORTA

mpnl_slide_common:

	; load slide amount
	ld a, (de)
	ld (ix + channel.effect2), a

	; set pitch slide flag
	ld a, (ix + channel.flags)
	and a, ~(CHAN_FLAG_ARPEGGIO | CHAN_FLAG_VIBRATO | CHAN_FLAG_SLIDE)
	or a, CHAN_FLAG_SLIDE
	ld (ix + channel.flags), a
	
	; set "pitch always changing" flag
	set CHAN_EVENT_BIT_PITCH_CHANGE_ALWAYS, (ix + channel.events)

	jp mpnl_command_done


mpnl_slide_off:

	; clear slide
	ld (ix + channel.effect1), 0

	; clear slide flag
	ld a, (ix + channel.flags)
	and a, ~(CHAN_FLAG_ARPEGGIO | CHAN_FLAG_VIBRATO | CHAN_FLAG_SLIDE)
	ld (ix + channel.flags), a

	; mark pitch as requiring update
	set CHAN_EVENT_BIT_PITCH_CHANGED, (ix + channel.events)

	; clear the "pitch always changing" flag
	res CHAN_EVENT_BIT_PITCH_CHANGE_ALWAYS, (ix + channel.events)

	jp mpnl_command_done_one_byte_command


; bc : jump table address (preserve this!)
; de : instruction pointer (preserve this!)
mpnl_arpeggio:

	.ifndef BANJO_DISABLE_ARPEGGIO

	; keep arp values
	ld a, (de)
	ld l, a

	; load arpeggio offsets
	rrca
	rrca
	rrca
	rrca
	and a, 0x0f
	ld (ix + channel.effect2), a

	ld a, l
	and a, 0x0f
	ld (ix + channel.effect3), a

	; set arpeggio position to 2
	ld (ix + channel.effect1), 2

	; set arpeggio flag
	ld a, (ix + channel.flags)
	and a, ~(CHAN_FLAG_ARPEGGIO | CHAN_FLAG_VIBRATO | CHAN_FLAG_SLIDE)
	or a, CHAN_FLAG_ARPEGGIO
	ld (ix + channel.flags), a
	
	; set "pitch always changing" flag
	set CHAN_EVENT_BIT_PITCH_CHANGE_ALWAYS, (ix + channel.events)

	.endif

	jp mpnl_command_done

; bc : jump table address (preserve this!)
; de : instruction pointer (preserve this!)
mpnl_arpeggio_off:

	.ifndef BANJO_DISABLE_ARPEGGIO

	; clear arpeggio flag
	ld a, (ix + channel.flags)
	and a, ~(CHAN_FLAG_ARPEGGIO | CHAN_FLAG_VIBRATO | CHAN_FLAG_SLIDE)
	ld (ix + channel.flags), a

	; mark pitch as requiring update
	set CHAN_EVENT_BIT_PITCH_CHANGED, (ix + channel.events)
	
	; clear the "pitch always changing" flag
	res CHAN_EVENT_BIT_PITCH_CHANGE_ALWAYS, (ix + channel.events)

	.endif

	jp mpnl_command_done_one_byte_command


; bc : jump table address (preserve this!)
; de : instruction pointer (preserve this!)
mpnl_vibrato:

	.ifndef BANJO_DISABLE_VIBRATO
	
	; initialise vibrato counter
	ld (ix + channel.effect1), 0

	; get vibrato params
	ld a, (de)
	ld l, a

	; add amount
	and a, 0xf
	ld (ix + channel.effect2), a
	
	; vibrato amplitude
	ld a, l
	and a, 0xf0
	ld (ix + channel.effect3), a

	; set vibrato flag
	ld a, (ix + channel.flags)
	and a, ~(CHAN_FLAG_ARPEGGIO | CHAN_FLAG_VIBRATO | CHAN_FLAG_SLIDE)
	or a, CHAN_FLAG_VIBRATO
	ld (ix + channel.flags), a
	
	; set "pitch always changing" flag
	set CHAN_EVENT_BIT_PITCH_CHANGE_ALWAYS, (ix + channel.events)

	.endif

	jp mpnl_command_done

; bc : jump table address (preserve this!)
; de : instruction pointer (preserve this!)
mpnl_vibrato_off:

	.ifndef BANJO_DISABLE_VIBRATO

	; clear vibrato flag
	ld a, (ix + channel.flags)
	and a, ~(CHAN_FLAG_ARPEGGIO | CHAN_FLAG_VIBRATO | CHAN_FLAG_SLIDE)
	ld (ix + channel.flags), a

	; mark pitch as requiring update
	set CHAN_EVENT_BIT_PITCH_CHANGED, (ix + channel.events)

	; clear the "pitch always changing" flag
	res CHAN_EVENT_BIT_PITCH_CHANGE_ALWAYS, (ix + channel.events)

	.endif

	jp mpnl_command_done_one_byte_command

; bc : jump table address (preserve this!)
; de : instruction pointer (preserve this!)
mpnl_legato_on:

	; switch legato flag on
	set CHAN_EVENT_BIT_LEGATO, (ix + channel.events)

	jp mpnl_command_done_one_byte_command

; bc : jump table address (preserve this!)
; de : instruction pointer (preserve this!)
mpnl_legato_off:

	; switch legato flag off
	res CHAN_EVENT_BIT_LEGATO, (ix + channel.events)

	jp mpnl_command_done_one_byte_command

; bc : jump table address (preserve this!)
; de : instruction pointer (preserve this!)
mpnl_order_jump:

	; get order to jump to and store it
	ld a, (de)
	ld (iy + music_state.order_jump), a
	set STATE_FLAG_BIT_ORDER_JUMP, (iy + music_state.flags)
	
	jp mpnl_command_done

; bc : jump table address (preserve this!)
; de : instruction pointer (preserve this!)
mpnl_order_next:

	; get order to jump to by incrementing to the current order
	ld a, (iy + music_state.order)
    inc a

    ; check it's a valid order
    cp a, (iy + music_state.orders_length)
    jr c, mpnlon_order_ok

        xor a, a

    mpnlon_order_ok:
	ld (iy + music_state.order_jump), a
	set STATE_FLAG_BIT_ORDER_JUMP, (iy + music_state.flags)

	jp mpnl_command_done_one_byte_command


; bc : jump table address (preserve this!)
; de : instruction pointer (preserve this!)
mpnl_set_speed_1:

	; get new speed
	ld a, (de)
	ld (iy + music_state.speed_1), a

	jp mpnl_command_done


; bc : jump table address (preserve this!)
; de : instruction pointer (preserve this!)
mpnl_set_speed_2:

	; get new speed
	ld a, (de)
	ld (iy + music_state.speed_2), a

	jp mpnl_command_done


; bc : jump table address (preserve this!)
; de : instruction pointer (preserve this!)
mpnl_note_delay:

	; get new note delay
	ld a, (de)
	ld (ix + channel.wait), a

	; set tic wait flag
	set CHAN_EVENT_BIT_TIC_WAIT, (ix + channel.events)

	; store the pointer to the next command and return
	; this will be picked up when tic_wait hits zero
	inc de
	ld (ix + channel.pattern_ptr), e
	ld (ix + channel.pattern_ptr + 1), d

	ret

; bc : jump table address (preserve this!)
; de : instruction pointer (preserve this!)
mpnl_note_release:

	set CHAN_EVENT_BIT_MACRO_RELEASE, (ix + channel.events)

	jp mpnl_command_done_one_byte_command

.endif

.ifdef BANJO_MINIMAL
mpnl_arpeggio_off:
mpnl_slide_off:
mpnl_vibrato_off:
mpnl_legato_on:
mpnl_legato_off:
mpnl_order_next:
.endif

; skip commands of various sizes
; used for channel types which don't support a command
mpnl_skip_1_byte_command:

	jp mpnl_command_done_one_byte_command


.ifdef BANJO_MINIMAL
mpnl_pitch_slide_up:
mpnl_pitch_slide_down:
mpnl_portamento:
mpnl_arpeggio:
mpnl_order_jump:
mpnl_set_speed_1:
mpnl_set_speed_2:
mpnl_note_delay:
.endif

mpnl_skip_2_byte_command:

	jp mpnl_command_done


.ifdef BANJO_MINIMAL
mpnl_vibrato:
.endif

mpnl_skip_3_byte_command:

	inc de

	jp mpnl_command_done

