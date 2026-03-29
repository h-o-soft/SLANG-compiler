; Converted from lib/libdef/libmsx_grp.yml
; SLANG Runtime Library (new format)

; @name MSXGRPBASE
; @calls MSXGRPSYS,MSXGRPWORK
; @lib MSXLIB
CONSOLE_COLUMNS equ 32
CONSOLE_ROWS equ 24
CALSLT EQU $001C
WRTVRM EQU $004D
SETWRT EQU $0053
FILVRM equ $0056
LDIRVM equ $005C
INIGRP equ $0072
INIMLT equ $0075
CHGCLR equ $0062
INIT32 equ $006F
INITXT EQU $006C
WRTVDP EQU $0047

BDRCLR equ $F3EB
FORCLR equ $F3E9
BAKCLR equ $F3EA

RG0SAV EQU $F3DF
RG1SAV EQU $F3E0
RG2SAV EQU $F3E1
RG3SAV EQU $F3E2
RG4SAV EQU $F3E3
RG5SAV EQU $F3E4
RG6SAV EQU $F3E5
RG7SAV EQU $F3E6

VDP_DATA EQU $98
VDP_DATAIN EQU $98
VDP_CMD EQU $99
VDP_STATUS EQU $99


; @name MSX_CALLBIOS
; @lib MSXLIB
	push hl
	pop ix
	jp msxbios

; @name MSX_SCREEN
; @lib MSXLIB
	ld	a,l
	ld	hl, 005Fh	; CHGMOD
	push	hl
	pop	ix
	jp msxbios

; @name MSXGRPSYS
; @lib MSXLIB

msxbios:
	push iy
	ld	iy,($FCC0)
	call	CALSLT
	pop iy
	ei
	ret

; @name MSX_SET_COLOR
; @calls MSXGRPBASE,MSXGRPWORK,MSXGRPSYS
; @lib MSXLIB
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

; @name MSX_VWRITE
; @calls MSXGRPBASE,MSXGRPWORK,MSXGRPSYS
; @lib MSXLIB
	; hl = source, de = dest, bc = count
	ld	ix,LDIRVM
	jp	msxbios

; @name MSX_VWRITE_DIRECT
; @calls MSXGRPBASE,MSXGRPWORK,MSXGRPSYS
; @lib MSXLIB
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

; @name MSX_VFILL
; @calls MSXGRPBASE,MSXGRPWORK,MSXGRPSYS
; @lib MSXLIB
	; hl = addr, value = de, count = bc
	ld a, e		; value

	ld ix,FILVRM
	jp	msxbios

; @name GET_VDP_REG
; @calls MSXGRPBASE,MSXGRPWORK,MSXGRPSYS
; @lib MSXLIB
	ld de,RG0SAV
	add hl,de

	ld	l,(hl)
	ld	h,0

	ret

; @name SET_VDP_REG
; @calls MSXGRPBASE,MSXGRPWORK,MSXGRPSYS
; @lib MSXLIB
	push	ix
	ld	c,l
	ld	b,e
	ld	ix,WRTVDP
	call	msxbios
	pop	ix
	ret

; @name SET_SPRITE_16HFLIP
; @calls MSXGRPBASE,MSXGRPWORK,MSXGRPSYS
; @lib MSXLIB
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

; @name MSXGRPWORK
; @lib MSXLIB
; @works VDP_ATTR:1
;


; @name MSXCALLS
CHPUT   EQU $00A2
EXPTBL  EQU $FCC1
ENASLT  EQU $0024
INIT32  EQU $006F
RSLREG  EQU $0138
CHGMOD  EQU $005F
LINL40  EQU $F3AE
POSIT   EQU $00C6
GTSTCK  EQU $00D5


; @name STICK2
; @calls MSXCALLS
LD A,L
LD IX,GTSTCK
CALL MSXLIB.msxbios
; CALL GTSTCK
LD HL,STICK_TBL
LD E,A
LD D,0
ADD HL,DE
LD A,(HL)
LD L,A
LD H,0
RET
STICK_TBL:
DB 00H
DB 01H
DB 03H
DB 02H
DB 06H
DB 04H
DB 0CH
DB 08H
DB 09H


