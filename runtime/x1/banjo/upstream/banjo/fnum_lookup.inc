
; hl: fnum lookup table
; de: note (whole note in upper byte, note fraction in lower byte)
music_fnum_lookup:

	; get fraction of semitone
	; fnum lookup table has 16 fnums per note, 2 bytes per fnum
	; shift and mask it to use the top 4 bits as note fraction offset into fnum table
	; bits go like ffffffff -> 000ffff0
	ld a, e
	rrca
	rrca
	rrca
	and a, 0x1e
	ld b, a

	; get pointer to divmod of this note
	ld a, d
	add a, <music_divmod12
	ld e, a
	adc a, >music_divmod12
	sub a, e
	ld d, a

	; get divmod12 and preserve in c
	ld a, (de)
	ld c, a

	; de = (((note % 12) * 32) | note fraction)
	; mod 12 is in the upper nibble, so it's already * 16
	; multiply by 2 and combine with note fraction offset in b
	; all together into de
	ld d, 0
	and a, 0xf0
	add a, a
	rl d
	or a, b
	ld e, a

	; offset into fnum lookup table
	add hl, de

	; get fnum and return in hl
	ld a, (hl)
	inc hl
	ld h, (hl)
	ld l, a

	; restore divmod
	; return octave (note / 12) in b
	ld a, c
	and a, 0xf
	ld b, a

	ret

; b: octave
; hl: fnum for octave 0
music_fnum_shift_octave:

	; don't do anything when octave == 0
	ld a, b
	or a, a
	ret z

		; lower byte into a, so we can use rra
		ld a, l

		; divide the fnum by 2 for each octave
		shift_octave_loop:

			srl h
			rra

			djnz shift_octave_loop

		; restore lower byte into l
		ld l, a

		ret
