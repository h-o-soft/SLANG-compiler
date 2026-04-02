

#LIB MSXGRPBASE

msxbios:
	push iy
	ld	iy,($FCC0)
	call	CALSLT
	pop iy
	ei
	ret
#ENDLIB

#LIB MSXCALLBIOS
	push hl
	pop ix
	jp msxbios
#ENDLIB

#LIB MSX_SCREEN
	ld	a,l
	ld	hl, 005Fh	; CHGMOD
	push	hl
	pop	ix
	jp msxbios
#ENDLIB

#LIB MSXSETCOLOR
	; HL = foreground
	; DE = background
	; BC = border
	ld	a,e		;border
	ld	(BDRCLR),a
	ld	a,l		;foreground
	and	$0f
	ld	(FORCLR),a
	rlca
	rlca
	rlca
	rlca
	and	$f0
	ld	l,a
	ld	a,c		;background
	and	$0f
	ld	(BAKCLR),a
	or	l
	ld	(VDP_ATTR),a
	ld	a,(0FCAFh)	;SCRMOD
	ld	ix,CHGCLR
	call	msxbios
	ret
#ENDLIB

#LIB MSX_VWRITE
	; hl = source, de = dest, bc = count
	ld	ix,LDIRVM
	jp	msxbios
#ENDLIB

#LIB MSX_VWRITE_DIRECT
	; hl = source, de = dest, bc = count
	push ix
	ex de,hl

	ld	ix,SETWRT
	call	msxbios
	ld	l,c	; count - bc is preserved by bios
	ld	h,b

	ld	bc,VDP_DATA
wrtloop:
	ld	a,(de)
	out	(c),a

	inc	de
	dec	hl
	ld	a,h
	or	l
	jr	nz,wrtloop
	pop	ix
	ret
#ENDLIB

#LIB MSX_VFILL
	; hl = addr, value = de, count = bc
	ld a, e		; value

	ld ix,FILVRM
	jp	msxbios
#ENDLIB


#LIB GET_VDP_REG
	ld de,RG0SAV
	add hl,de

	ld	l,(hl)
	ld	h,0

	ret
#ENDLIB

#LIB SET_VDP_REG
	push	ix
	ld	c,l
	ld	b,e
	ld	ix,WRTVDP
	call	msxbios
	pop	ix
	ret
#ENDLIB

#LIB SET_SPRITE_16HFLIP
	; hl = pattern index, de = data
SP_PATTERNS EQU $3800

_ubox_set_sprite_pat16_flip:
	; add pattern(hl = pattern index)
	add hl, hl
	add hl, hl
	add hl, hl
	add hl, hl
	add hl, hl
	ld bc,SP_PATTERNS
	add hl, bc

	push de
	ld bc, 16
	ex de, hl
	add hl, bc
	ex de, hl
	call flip

	pop de
	call flip

	ret

flip:
	ld b, 16
flip0:
	call flip_and_copy
	inc hl
	inc de
	djnz flip0
	ret

flip_and_copy:
	ld a, (de)
	ld c, a
  	rlca
  	rlca
  	xor c
 	and $aa
	xor c
	ld c, a
	rlca
	rlca
	rlca
	rrc c
	xor c
	and $66
	xor c

	ld ix,WRTVRM
	jp MSXLIB.msxbios
	;jp WRTVRM
#ENDLIB