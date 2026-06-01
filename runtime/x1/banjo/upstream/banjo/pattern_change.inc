
; banjo sound driver
; Joe Kennedy 2024

; jump to a given order/pattern
; used for the 0x0b effect
; a: order number to jump to
; ix: channels
; iy: state
music_jump_channels_order_and_pattern:

	; update order index
	ld (iy + music_state.order), a

	; point de at first channel's channel.pattern_ptr 
	; first get it into hl
	push ix
	pop hl
	
	; move along to pattern_ptr and swap into de
	ld de, channel.pattern_ptr
	add hl, de
	ex de, hl

	; double order number in hl
	ld h, 0
	ld l, a
	add hl, hl

	; get pointer to orders list in bc
	ld c, (iy + music_state.order_ptrs)
	ld b, (iy + music_state.order_ptrs + 1)

	; add them together
	; address of this order for the first channel is now pointed at by hl
	add hl, bc

	; get pointer to order in hl
	ld a, (hl)
	inc hl
	ld h, (hl)
	ld l, a

	; loop through all channels
	ld b, (iy + music_state.channel_count)
	ld c, 0xff

	mjcoap_loop:

		; update pattern ptr
		ldi
		ldi

		; set line_wait = 0 for channel
		xor a, a
		ld (de), a

		; move along to next channel
		ld a, _sizeof_channel - 2
		add a, e
		ld e, a
		adc a, d
		sub a, e
		ld d, a

		djnz mjcoap_loop

	; reset order_jump flag to indicate we don't need to jump
	res STATE_FLAG_BIT_ORDER_JUMP, (iy + music_state.flags)

	; reset line and tic
	ld a, (iy + music_state.pattern_length)
	ld (iy + music_state.line), a

	ld a, (iy + music_state.speed_1)
	ld (iy + music_state.tic), a

	ret
	