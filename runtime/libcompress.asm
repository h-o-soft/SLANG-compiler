; Converted from lib/libdef/libcompress.yml
; SLANG Runtime Library (new format)

; @name LZE_DECODE
; @lib lze
dlze:
		ld	a,080h
dlze_lp1:
		ldi
dlze_lp2:
		call	getbit ; GET_BIT
		jr	c,dlze_lp1

		call	getbit ; GET_BIT
		jr	c,dlze_far
		ld	c,0

		call	getbit ; GET_BIT
		rl	c
		call	getbit ; GET_BIT
		rl	c

		push	hl
		ld	l,(hl)
		ld	h,-1

dlze_copy:
		ld	b,0
		inc	c
		add	hl,de
	; IFNDEF	ALLOW_LDIR_UNROLLING
		inc	bc
		ldir
	; ELSE
	; 	ldir
	; 	ldi
	; ENDIF
		pop	hl
		inc	hl
		jr	dlze_lp2

dlze_far:
		ex      af, af';'

		ld	a,(hl)
		inc	hl

		ld	c,(hl)
		rra
		rr	c
		rra
		rr	c
		rra
		rr	c
		or	0e0h
		ld	b,a

		ld	a,(hl)
		and	7
		jr	nz,dlze_skip

		inc	hl
		or	(hl)
		ret	z
		dec	a

dlze_skip:
		push	hl
		ld	l,c
		ld	h,b
		ld	c,a
		ex      af, af';'
		jr	dlze_copy

getbit:
	; IFNDEF	ALLOW_INLINE_GETBIT
		add	a,a
		ret	nz
	; ENDIF
		ld	a,(hl)
		inc	hl
		adc	a,a
		ret


; @name LZEE_DECODE
; @lib lzee
dlze:
		ld	a,080h
dlze_lp1:
		ldi
dlze_lp2:
	 	call	getbit	; GET_BIT
		jr	c,dlze_lp1

	 	call	getbit	; GET_BIT
		jr	c,dlze_far

		ld	c,0
	 	call	getbit	; GET_BIT
		rl	c
	 	call	getbit	; GET_BIT
		rl	c

		push	hl
		ld	l,(hl)
		ld	h,-1

dlze_copy:
		ld	b,0
		inc	c
		add	hl,de
	; IFNDEF	ALLOW_LDIR_UNROLLING
		inc	bc

		ldir
	; ELSE
	; 	ldir
	; 	ldi
	; ENDIF
		pop	hl
		inc	hl
		jr	dlze_lp2

dlze_far:
		ex      af, af';'

		ld	a,(hl)
		or	7
		rrca
		rrca
		rrca
		ld	b,a

		ld	a,(hl)
		inc	hl
		ld	c,(hl)

		and	7
		jr	nz,dlze_skip

		inc	hl
		or	(hl)
		ret	z
		dec	a

dlze_skip:
		push	hl
		ld	l,c
		ld	h,b
		ld	c,a
		ex      af, af';'
		jr	dlze_copy

getbit:
	;IFNDEF	ALLOW_INLINE_GETBIT
		add	a,a
		ret	nz
	;ENDIF
		ld	a,(hl)
		inc	hl
		adc	a,a
		ret

; @name LZEEE_DECODE
; @lib lzeee
dlzeee:
		ldi
		scf

getbit1:
		ld	a,(hl)
		inc	hl
		adc	a,a
		jr	c,dlze_lp1n

	;	DUP	2
	;	ldi
	;	add	a
	;	jr	c,dlze_lp1n
	;	EDUP

dlze_lp1:
		ldi
dlze_lp2:
		add	a,a
		jr	nc,dlze_lp1
		jr	z,getbit1
dlze_lp1n:
		add	a,a
		jr	c,dlze_far
dlze_near:
		ld	bc,0

		add	a,a
		call	z,getbit
		rl	c

		add	a,a
		call	z,getbit
		rl	c

		push	hl
		ld	l,(hl)
		ld	h,-1

dlze_copy:
		inc	c
		add	hl,de
		ldir
		ldi
		pop	hl
		inc	hl
		jr	dlze_lp2

getbit2:
		ld	a,(hl)
		inc	hl
		adc	a,a
		jr	nc,dlze_near

dlze_far:
		jr	z,getbit2
		ex      af, af';'
		ld	a,(hl)
		inc	hl
		push	hl
		ld	l,(hl)
		ld	c,a
		or	7
		rrca
		rrca
		rrca
		ld	h,a
		ld	a,c
		and	7
		jr	nz,dlze_skip

		pop	bc
		inc	bc
		ld	a,(bc)
	; IFNDEF	OBSOLETED_F4
		or	a
		ret	z
	; ELSE
	; 	sub	1
	; 	ret	c
	; ENDIF
		push	bc

dlze_skip:
		ld	b,0
		ld	c,a
		ex      af, af';'
		jr	dlze_copy

getbit:
		ld	a,(hl)
		inc	hl
		adc	a,a
		ret

; @name ZX0_DECODE
; @lib zx0
dzx0_standard:
        ld      bc, $ffff               ; preserve default offset 1
        push    bc
        inc     bc
        ld      a, $80
dzx0s_literals:
        call    dzx0s_elias             ; obtain length
        ldir                            ; copy literals
        add     a, a                    ; copy from last offset or new offset?
        jr      c, dzx0s_new_offset
        call    dzx0s_elias             ; obtain length
dzx0s_copy:
        ex      (sp), hl                ; preserve source, restore offset
        push    hl                      ; preserve offset
        add     hl, de                  ; calculate destination - offset
        ldir                            ; copy from offset
        pop     hl                      ; restore offset
        ex      (sp), hl                ; preserve offset, restore source
        add     a, a                    ; copy from literals or new offset?
        jr      nc, dzx0s_literals
dzx0s_new_offset:
        pop     bc                      ; discard last offset
        ld      c, $fe                  ; prepare negative offset
        call    dzx0s_elias_loop        ; obtain offset MSB
        inc     c
        ret     z                       ; check end marker
        ld      b, c
        ld      c, (hl)                 ; obtain offset LSB
        inc     hl
        rr      b                       ; last offset bit becomes first length bit
        rr      c
        push    bc                      ; preserve new offset
        ld      bc, 1                   ; obtain length
        call    nc, dzx0s_elias_backtrack
        inc     bc
        jr      dzx0s_copy
dzx0s_elias:
        inc     c                       ; interlaced Elias gamma coding
dzx0s_elias_loop:
        add     a, a
        jr      nz, dzx0s_elias_skip
        ld      a, (hl)                 ; load another group of 8 bits
        inc     hl
        rla
dzx0s_elias_skip:
        ret     c
dzx0s_elias_backtrack:
        add     a, a
        rl      c
        rl      b
        jr      dzx0s_elias_loop
; -----------------------------------------------------------------------------

