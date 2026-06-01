
; banjo sound driver
; Joe Kennedy 2023

; ix: channel
; iy: state
; a: new instrument number
music_instrument_change:

	; preserve bc and de
	push bc
	push de

	; store the new instrument number in the channel
	ld (ix + channel.instrument_num), a
	
    .assert _sizeof_instrument == 12   ; ensure that an instrument is the size we think it is!

	; get instrument pointer by doing instrument_num * 12
	ld l, a
	ld h, 0
	
	; * 4
	add hl, hl
	add hl, hl

	; keep * 4 for later
	ld b, h
	ld c, l
	
	; * 8
	add hl, hl
	
	; * 12
	add hl, bc
	
	; offset into instrument data
	ld c, (iy + music_state.instrument_ptrs)
	ld b, (iy + music_state.instrument_ptrs + 1)
	add hl, bc

	; get channel struct ptr ix into de
	push ix
	pop de

	; set macro flags
	; get channel.flags (should be first byte of channel struct)
	; then unset the macro flags and combine with flags from first byte of instrument 
	ld a, (de)
	and a, ~(CHAN_FLAG_VOLUME_MACRO | CHAN_FLAG_ARP_MACRO | CHAN_FLAG_EX_MACRO)
	or a, (hl)
	ld (de), a

	; move along to volume_macro_vol
	ld a, channel.volume_macro_vol
	add a, e
	ld e, a
	adc a, d
	sub a, e
	ld d, a

	; move to volume macro info in instrument
	inc hl

	; set volume_macro_vol to 0
	xor a, a
	ld (de), a

	; move to volume macro ptr
	inc de
	inc de

	; volume macro
	; copy over macro info
	; ptr
	ldi
	ldi

	; arp macro
	; move past arp_macro_val and arp_macro_pos to arp_macro_ptr
	inc de
	inc de

	; copy over macro info
	; ptr
	ldi
	ldi

	; ex macro
	; move past ex_macro_val and ex_macro_pos to arp_macro_type
	inc de
	inc de

	; copy over macro info
	; ex_macro_type
	ldi

	; ex_macro_ptr
	ldi
	ldi

	; restore de and bc
	pop de
	pop bc
	
	ret
