
;		ORG	$9F00


#LIB SOROBANLIB
SORO_NOPATCH	EQU	0
SORO_KATA_VALUE	EQU	8

SORO_BYTE	EQU	8

SORO_KATA	DW	SORO_KATA_VALUE
		jp	SORO_MOVE
		jp	SORO_SWAP
		jp	SORO_CVDBL
		jp	SORO_CVSNG
		jp	SORO_CVSTF
		jp	SORO_CVUTF
		jp	SORO_CVITF
		jp	SORO_CVFTS
		jp	SORO_CVFTU
		jp	SORO_CVFTI
		jp	SORO_ADD
		jp	SORO_SUB
		jp	SORO_MUL
		jp	SORO_DIV
		jp	SORO_IDIV
		jp	SORO_MOD
		jp	SORO_CMP
		jp	SORO_NEG
		jp	SORO_INT
		jp	SORO_FIX
		jp	SORO_FRAC
		jp	SORO_CINT
		jp	SORO_SQR
		jp	SORO_SIN
		jp	SORO_COS
		jp	SORO_TAN
		jp	SORO_ATN
		jp	SORO_EXP
		jp	SORO_LOG
		jp	SORO_POW
		jp	SORO_PAI
		jp	SORO_RAD
		jp	SORO_ABS
		jp	SORO_SGN

		; jp	$
		nop
		nop
		nop
		; jp	$
		nop
		nop
		nop
		; jp	$
		nop
		nop
		nop

SORO_RCF_SHTR:
		or	a
SORO_SHTR:
#IF SORO_KATA_VALUE == 5
		jr	SORO_SHTR5	; $22,$07
#ELSE
		rr	(hl)		; $CB
		inc	hl		; $1E
#ENDIF

		rr	(hl)
		inc	hl
		rr	(hl)
		inc	hl
SORO_SHTR5:
		rr	(hl)
		inc	hl
		rr	(hl)
		inc	hl
		rr	(hl)
		inc	hl
		rr	(hl)
		inc	hl
		rr	(hl)
		ret	
SORO_RCF_SHTL:
		or	a
SORO_SHTL:
#IF SORO_KATA_VALUE == 5
		jr	SORO_SHTL5	; $18,$16
#ELSE
		rl	(hl)		; $cb
		dec	hl		; $16
#ENDIF
		rl	(hl)
		dec	hl
		rl	(hl)
		dec	hl
		rl	(hl)
		dec	hl
		rl	(hl)
		dec	hl
		rl	(hl)
		dec	hl
		rl	(hl)
		dec	hl
		rl	(hl)
		ret	
SORO_SHTL5:
		dec	hl
		dec	hl
		dec	hl
		rl	(hl)
		dec	hl
		rl	(hl)
		dec	hl
		rl	(hl)
		dec	hl
		rl	(hl)
		dec	hl
		rl	(hl)
		ret	
SORO_BSHTR:
#IF SORO_KATA_VALUE == 8
		ld	bc, 7
#ELSE
		ld	bc, 4
#ENDIF
		add	hl, bc
		ld	d, h
		ld	e, l
		dec	hl
		lddr	
		xor	a
		ld	(de), a
		ret	
SORO_TOBUFF:
		push	de
		call	SORO_TOBUFF1
		pop	hl
		ld	de, SORO_SHISU2
		ld	bc, (SORO_KATA)
		ldir	
		xor	a
		ld	(de), a
		ld	hl, SORO_KARISU2+0
		ld	a, 0
		bit	7, (hl)
		jr	z, .tobuff1
		ld	a, 1
.tobuff1
		ld	(SORO_FUGOU2), a
		ld	a, (SORO_SHISU2)
		or	a
		jr	z, .tobuff2
		set	7, (hl)
.tobuff2
		ret	

SORO_TOBUFF1:

		ld	bc, (SORO_KATA)
#IF SORO_NOPATCH == 0
		ld	a, c
		cp	8
		jr	nz, .tobuff1_1
		ld	de, 1ECBh ; RR (HL)
		jr	.tobuff1_2
.tobuff1_1
		ld	de, 718h  ; JR SORO_SHTR5
.tobuff1_2
		dec	a
		ld	(SORO_BSHTR+1), a
		ld	(SORO_SHTR),	de
		ld	a, e
		ld	(SORO_SHTL),	a
#ENDIF
		ld	de, SORO_SHISU1
		ldir	
		xor	a
		ld	(de), a
		ld	hl, SORO_KARISU1+0
		ld	a, 0
		bit	7, (hl)
		jr	z, .tobuff1_3
		ld	a, 1
.tobuff1_3
		ld	(SORO_FUGOU1), a
		ld	a, (SORO_SHISU1)
		or	a
		jr	z, .tobuff1_4
		set	7, (hl)
.tobuff1_4
		ret	

SORO_SEIKIKA:
		ld	a, (SORO_SHISU1)
		or	a
		ret	z
		ld	hl, SORO_KARISU1+0
		ld	a, (SORO_SHISU1)
		ld	b, a
		ld	a, (SORO_KATA)
		ld	c, a
		ld	de, 8
.seikika1
		ld	a, (hl)
		or	a
		jr	nz, .seikika2
		inc	d
		inc	hl
		ld	a, b
		sub	e
		ld	b, a
		jr	z, SORO_ZEROVALUE
		jr	c, SORO_ZEROVALUE
		dec	c
		jr	z, SORO_ZEROVALUE
		jr	.seikika1
.seikika2
		ld	a, b
		ld	(SORO_SHISU1), a
		ld	a, d
		or	a
		jr	z, .seikika3
		ld	de, SORO_KARISU1+0
		ld	b, 0
		ldir	
		ld	b, a
.seikika4
		xor	a
		ld	(de), a
		inc	de
		djnz	.seikika4
.seikika3
		ld	de, SORO_KARISU1+0
		ld	a, (SORO_SHISU1)
		ld	b, a
.seikika5
		ld	a, (de)
		bit	7, a
		jr	nz, .seikika6
		ld	hl, SORO_KARISU1+8-1
		call	SORO_RCF_SHTL
		dec	b
		jr	z, SORO_ZEROVALUE
		jr	.seikika5
.seikika6
		ld	a, b
		ld	(SORO_SHISU1), a
		ld	a, (SORO_KATA)
		ld	hl, SORO_KARISU1+4
		cp	8
		jr	nz, .seikika7 
		ld	hl, SORO_KARISU1+7
.seikika7
		bit	7, (hl)
		jr	z, .seikika8
		dec	a
		ld	b, a
		scf	
.seikika9
		dec	hl
		ld	a, (hl)
		adc	a, 0
		ld	(hl), a
		djnz	.seikika9
		call	c, SORO_BITAFURE
.seikika8
		ld	hl, SORO_KARISU1+0
		ld	a, (SORO_FUGOU1)
		cp	0
		jr	nz, .seikika10
		res	7, (hl)
.seikika10
		ret	

SORO_ZEROVALUE:
		ld	a, 0
		ld	(SORO_FUGOU1), a
		xor	a
		ld	(SORO_SHISU1), a
		ld	hl, SORO_ATAI0	; OK?
		ld	de, SORO_KARISU1
		jp	SORO_MOVE
SORO_MAXVALUE:
		ld	hl, SORO_SHISU1
		ld	a, (SORO_KATA)
.maxvalue1
		ld	(hl), 0FFh
		inc	hl
		dec	a
		jr	nz, .maxvalue1
		ld	(hl), 0
		ret	

SORO_KARISUGENZAN:
		ld	a, (SORO_KATA)
		cp	5
		jr	nz, .karigen1
		ld	de, SORO_KARISU1+4 ;  29Ah
		ld	hl, SORO_KARISU2+4 ; 2A4h
		jr	.karigen2
.karigen1
		ld	de, SORO_KARISU1+7 ; 29Dh
		ld	hl, SORO_KARISU2+7 ; 2A7h
		ld	a, (de)
		sub	(hl)
		ld	(de), a
		dec	de
		dec	hl
		ld	a, (de)
		sbc	a, (hl)
		ld	(de), a
		dec	de
		dec	hl
		ld	a, (de)
		sbc	a, (hl)
		ld	(de), a
		dec	de
		dec	hl
.karigen2
		ld	a, (de)
		sbc	a, (hl)
		ld	(de), a
		dec	de
		dec	hl
		ld	a, (de)
		sbc	a, (hl)
		ld	(de), a
		dec	de
		dec	hl
		ld	a, (de)
		sbc	a, (hl)
		ld	(de), a
		dec	de
		dec	hl
		ld	a, (de)
		sbc	a, (hl)
		ld	(de), a
		dec	de
		dec	hl
		ld	a, (de)
		sbc	a, (hl)
		ld	(de), a
		ret	

SORO_KARISUKASAN:
		ld	a, (SORO_KATA)
		cp	5
		jr	nz, .karisukasan1
		ld	de, SORO_KARISU1+4
		ld	hl, SORO_KARISU2+4
		jr	.karisukasan2
.karisukasan1
		ld	de, SORO_KARISU1+7
		ld	hl, SORO_KARISU2+7
		ld	a, (de)
		add	a, (hl)
		ld	(de), a
		dec	de
		dec	hl
		ld	a, (de)
		adc	a, (hl)
		ld	(de), a
		dec	de
		dec	hl
		ld	a, (de)
		adc	a, (hl)
		ld	(de), a
		dec	de
		dec	hl
.karisukasan2
		ld	a, (de)
		adc	a, (hl)
		ld	(de), a
		dec	de
		dec	hl
		ld	a, (de)
		adc	a, (hl)
		ld	(de), a
		dec	de
		dec	hl
		ld	a, (de)
		adc	a, (hl)
		ld	(de), a
		dec	de
		dec	hl
		ld	a, (de)
		adc	a, (hl)
		ld	(de), a
		dec	de
		dec	hl
		ld	a, (de)
		adc	a, (hl)
		ld	(de), a
		ret	nc

SORO_BITAFURE:
		ld	hl, SORO_KARISU1
		call	SORO_SHTR
		ld	hl, SORO_SHISU1
		inc	(hl)
		call	z, SORO_MAXVALUE
		scf	
		ret	

SORO_KARISUHIKAKU:
		ld	de, SORO_KARISU1
		ld	hl, SORO_KARISU2
		ld	a, (SORO_KATA)
		cp	8
		jr	nz, .karisuhikaku1 
		ld	a, (de)
		cp	(hl)
		ret	nz
		inc	de
		inc	hl
		ld	a, (de)
		cp	(hl)
		ret	nz
		inc	de
		inc	hl
		ld	a, (de)
		cp	(hl)
		ret	nz
		inc	de
		inc	hl
.karisuhikaku1
		ld	a, (de)
		cp	(hl)
		ret	nz
		inc	de
		inc	hl
		ld	a, (de)
		cp	(hl)
		ret	nz
		inc	de
		inc	hl
		ld	a, (de)
		cp	(hl)
		ret	nz
		inc	de
		inc	hl
		ld	a, (de)
		cp	(hl)
		ret	nz
		inc	de
		inc	hl
		ld	a, (de)
		cp	(hl)
		ret	
SORO_KOUKAN:
		ld	hl, SORO_FUGOU1
		ld	de, SORO_FUGOU2
		ld	a, (SORO_KATA)
		ld	b, a
		inc	b
		inc	b
SORO_KOUKANSHORI:
		ld	c, (hl)
		ld	a, (de)
		ld	(hl), a
		ld	a, c
		ld	(de), a
		inc	hl
		inc	de
		djnz	SORO_KOUKANSHORI
		ret	

; WORK
; SORO_FUGOU1:	db 0
; SORO_SHISU1:	db 0
; SORO_KARISU1:	ds SORO_BYTE
; SORO_FUGOU2:	db 0
; SORO_SHISU2:	db 0
; SORO_KARISU2:	ds SORO_BYTE
; SORO_VAR_X:	ds SORO_BYTE
; SORO_VAR_XX:	ds SORO_BYTE
; SORO_VAR_Y:	ds SORO_BYTE

SORO_MOVE:
SORO_LDIR:
		push	bc
		push	de
		push	hl
		ld	bc, (SORO_KATA)
		ldir	
		pop	hl
		pop	de
		pop	bc
		ret	

SORO_SWAP:
		push	bc
		push	de
		push	hl
		ld	a, (SORO_KATA)
		ld	b, a
		call	SORO_KOUKANSHORI
		pop	hl
		pop	de
		pop	bc
		ret	

SORO_SUB:
		push	bc
		push	de
		push	hl
		call	SORO_TOBUFF
		ld	hl, SORO_FUGOU2
		ld	a, (hl)
		xor	1
		ld	(hl), a
		jr	SORO_ADDSHORI

SORO_ADD:
		push	bc
		push	de
		push	hl
		call	SORO_TOBUFF
SORO_ADDSHORI:
		ld	a, (SORO_SHISU1)
		ld	b, a
		ld	a, (SORO_SHISU2)
		ld	c, a
		ld	a, b
		cp	c
		jr	nc, .soroadd1
		push	bc
		call	SORO_KOUKAN
		pop	bc
		ld	a, b
		ld	b, c
		ld	c, a
.soroadd1
		inc	c
		dec	c
		jp	z, SORO_ENZANSHURYO
		ld	d, 8*5
		ld	a, (SORO_KATA)
		cp	8
		jr	nz, .soroadd2
		ld	d, 8*8
.soroadd2
		ld	a, b
		sub	c
		ld	b, a
		cp	d
		jp	nc, SORO_ENZANSHURYO
		ld	c, 8
.soroadd3
		ld	a, b
		sub	c
		jr	c, .soroadd4
		ld	b, a
		push	bc
		ld	hl, SORO_KARISU2
		call	SORO_BSHTR
		pop	bc
		jr	.soroadd3
.soroadd4
		inc	b
		dec	b
		jr	z, .soroadd5
.soroadd6
		ld	hl, SORO_KARISU2
		call	SORO_RCF_SHTR
		djnz	.soroadd6
.soroadd5
		ld	a, (SORO_FUGOU1)
		ld	b, a
		ld	a, (SORO_FUGOU2)
		cp	b
		jr	nz, .soroadd7
		call	SORO_KARISUKASAN
		jr	.soroadd8
.soroadd7
		ld	a, (SORO_SHISU1)
		ld	b, a
		ld	a, (SORO_SHISU2)
		cp	b
		jr	nz, .soroadd9
		call	SORO_KARISUHIKAKU
		call	c, SORO_KOUKAN
.soroadd9
		call	SORO_KARISUGENZAN
.soroadd8
		jp	SORO_ENZANSHURYO

SORO_MUL:
		push	bc
		push	de
		push	hl

		call	SORO_TOBUFF

		ld	a, (SORO_SHISU1)
		or	a
		jp	z, SORO_ENZANSHURYO
		ld	a, (SORO_SHISU2)
		or	a
		jp	z, SORO_ENZANSYURYOZERO
		ld	a, (SORO_FUGOU2)
		ld	c, a
		ld	a, (SORO_FUGOU1)
		xor	c
		ld	(SORO_FUGOU1), a

		ld	h, 0
		ld	a, (SORO_SHISU1)
		ld	l, a
		ld	d, h
		ld	a, (SORO_SHISU2)
		ld	e, a
		add	hl, de
		ld	de, 0FF7Fh	; -BIAS
		add	hl, de
		ld	a, h
		cp	0FFh

		jp	z, SORO_ENZANSYURYOZERO
		cp	1
		jp	z, SORO_ENZANSYURYOMAX
		ld	a, l
		ld	(SORO_SHISU1), a
		ld	hl, SORO_KARISU1
		ld	de, SORO_JOUSU
		call	SORO_MOVE
		ex	de, hl
		ld	hl, SORO_ATAI0
		call	SORO_MOVE
		ld	hl, SORO_JOUSU
		ld	a, (SORO_KATA)
		dec	a
		ld	b, a
.soromul1
		ld	c, (hl)
		inc	hl
		push	bc
		push	hl
		inc	c
		dec	c
		jr	nz, .soromul2
		ld	hl, SORO_KARISU2
		call	SORO_BSHTR
		jr	.soromul3
.soromul2
		ld	b, 8
.soromul4
		sla	c
		jr	nc, .soromul5
		call	SORO_KARISUKASAN
		jr	nc, .soromul5
		ld	hl, SORO_KARISU2
		call	SORO_RCF_SHTR
.soromul5
		ld	hl, SORO_KARISU2
		call	SORO_RCF_SHTR
		djnz	.soromul4
.soromul3
		pop	hl
		pop	bc
		djnz	.soromul1
		jp	SORO_ENZANSHURYO

; WORK
; SORO_JOUSU:	ds SORO_BYTE

SORO_DIV:
		push	bc
		push	de
		push	hl
		call	SORO_TOBUFF
		ld	a, (SORO_SHISU1)
		or	a
		jp	z, SORO_ENZANSHURYO
		ld	a, (SORO_SHISU2)
		or	a
		jp	z, SORO_ENZANSYURYOZERO
		ld	a, (SORO_FUGOU2)
		ld	c, a
		ld	a, (SORO_FUGOU1)
		xor	c
		ld	(SORO_FUGOU1), a
		ld	h, 0
		ld	a, (SORO_SHISU1)
		ld	l, a
		ld	d, h
		ld	a, (SORO_SHISU2)
		ld	e, a
		or	a
		sbc	hl, de
		ld	e, 81h ; BIAS
		add	hl, de
		ld	a, h
		cp	0FFh
		jp	z, SORO_ENZANSYURYOZERO
		cp	1
		jp	z, SORO_ENZANSYURYOMAX
		ld	a, l
		ld	(SORO_SHISU1), a
		ld	hl, SORO_KARISU1
		call	SORO_RCF_SHTR
		ld	hl, SORO_KARISU2
		call	SORO_RCF_SHTR
		ld	hl, SORO_SHOU
		ld	a, (SORO_KATA)
		dec	a
		ld	b, a
.sorodiv1
		push	bc
		push	hl
		ld	bc, 800h
.sorodiv2
		call	SORO_KARISUHIKAKU
		jr	c, .sorodiv3
		call	SORO_KARISUGENZAN
		scf	
		jr	.sorodiv4
.sorodiv3
		or	a
.sorodiv4
		rl	c
		ld	hl, SORO_KARISU1+8-1
		call	SORO_SHTL
		djnz	.sorodiv2
		pop	hl
		ld	(hl), c
		inc	hl
		pop	bc
		djnz	.sorodiv1
		push	hl
		call	SORO_KARISUHIKAKU
		ld	a, 0FFh
		adc	a, 0
		pop	hl
		ld	(hl), a
		ld	hl, SORO_SHOU
		ld	de, SORO_KARISU1
		call	SORO_MOVE
SORO_ENZANSHURYO:
		call	SORO_SEIKIKA
		pop	de
		push	de
		ld	hl, SORO_SHISU1
		ld	bc, (SORO_KATA)
		ldir	
		pop	hl
		pop	de
		pop	bc
		ret	

SORO_ENZANSYURYOZERO:
		call	SORO_ZEROVALUE
		jr	SORO_ENZANSHURYO

SORO_ENZANSYURYOMAX:
		call	SORO_MAXVALUE
		jr	SORO_ENZANSHURYO

; WORK
; SORO_SHOU:	ds SORO_BYTE

SORO_MOD:
		push	de
		push	hl
		push	de
		ld	de, SORO_MOD_X
		call	SORO_MOVE
		ex	de, hl
		pop	de
		call	SORO_IDIV
		call	SORO_MUL
		ex	de, hl
		pop	hl
		call	SORO_SUB
		pop	de
		ret	

; WORK
; SORO_MOD_X:	ds SORO_BYTE

SORO_CMP:
		push	bc
		push	de
		push	hl
		call	SORO_TOBUFF
		ld	c, 1
		ld	a, (SORO_FUGOU2)
		ld	b, a
		ld	a, (SORO_FUGOU1)
		cp	b
		jr	z, .sorocmp1
		cp	1
		jr	nz, .sorocmp2
		ld	c, 0FFh
.sorocmp2
		jr	.sorocmp3
.sorocmp1
		ld	a, (SORO_SHISU2)
		ld	b, a
		ld	a, (SORO_SHISU1)
		cp	b
		jr	z, .sorocmp4
		jr	nc, .sorocmp5
		ld	c, 0FFh
.sorocmp5
		jr	.sorocmp6
.sorocmp4
		call	SORO_KARISUHIKAKU
		jr	nz, .sorocmp7
		ld	c, 0
		jr	.sorocmp6
.sorocmp7
		jr	nc, .sorocmp6
		ld	c, 0FFh
.sorocmp6
		ld	a, (SORO_FUGOU1)
		cp	1
		jr	nz, .sorocmp3
		ld	a, c
		neg	
		ld	c, a
.sorocmp3
		ld	a, c
		inc	a
		sub	1
		pop	hl
		pop	de
		pop	bc
		ret	

SORO_ADDA:
		push	de
		push	hl
		ld	d, 0
		ld	e, a
		ld	hl, SORO_ADDDEC_X
		call	SORO_CVUTF
		ex	de, hl
		pop	hl
		call	SORO_ADD
		pop	de
		ret	

; WORK
; SORO_ADDDEC_X:	ds	SORO_BYTE

SORO_MUL10:
		push	de
		ld	de, SORO_ATAI10
		call	SORO_MUL
		pop	de
		ret	

SORO_DIV10:
		push	de
		ld	de, SORO_ATAI10
		call	SORO_DIV
		pop	de
		ret	

SORO_SQR:
		push	bc
		push	de
		push	hl
		call	SORO_TOBUFF1
		ld	a, 0
		ld	(SORO_FUGOU1), a
		ld	a, (SORO_SHISU1)
		or	a
		jp	z, SORO_ENZANSHURYO
		sub	81h ; BIAS
		rra	
		jr	c, .sorosqr1
		ld	hl, SORO_KARISU1
		call	SORO_SHTR
.sorosqr1
		add	a, 81h ; BIAS
		ld	(SORO_SHISU1), a
		ld	hl, SORO_KARISU1
		ld	de, SORO_VAR_X
		call	SORO_MOVE
		ld	hl, SORO_ATAI0
		ld	de, SORO_KARISU1
		call	SORO_MOVE
		ld	de, SORO_KARISU2
		call	SORO_MOVE
		ld	de, SORO_HEIHOUKON
		call	SORO_MOVE
		ld	c, 8*4+1	; 21h
		ld	a, (SORO_KATA)
		cp	8
		jr	nz, .sorosqr2
		ld	c, 8*7+1 ;  39h
.sorosqr2
		ld	hl, SORO_HEIHOUKON+8-1
		call	SORO_RCF_SHTL
		ld	hl, SORO_VAR_X+8-1
		call	SORO_RCF_SHTL
		ld	hl, SORO_KARISU1+8-1
		call	SORO_SHTL
		ld	hl, SORO_VAR_X+8-1
		call	SORO_RCF_SHTL
		ld	hl, SORO_KARISU1+8-1
		call	SORO_SHTL
		scf	
		ld	hl, SORO_KARISU2+8-1
		call	SORO_SHTL
		call	SORO_KARISUHIKAKU
		jr	nc, .sorosqr3
		ld	hl, SORO_KARISU2+4
		ld	a, (SORO_KATA)
		cp	8
		jr	nz, .sorosqr4
		ld	hl, SORO_KARISU2+7
.sorosqr4
		dec	(hl)
		jr	.sorosqr5
.sorosqr3
		call	SORO_KARISUGENZAN
		ld	a, (SORO_KATA)
		ld	b, a
		ld	hl, SORO_HEIHOUKON+4
		ld	de, SORO_KARISU2+4
		cp	8
		jr	nz, .sorosqr6
		ld	hl, SORO_HEIHOUKON+7
		ld	de, SORO_KARISU2+7
.sorosqr6
		set	7, (hl)
		scf	
.sorosqr7
		ld	a, (de)
		adc	a, 0
		ld	(de), a
		dec	de
		djnz	.sorosqr7
.sorosqr5
		dec	c
		jr	nz, .sorosqr2
		ld	hl, SORO_HEIHOUKON
		ld	de, SORO_KARISU1
		call	SORO_MOVE
		jp	SORO_ENZANSHURYO

SORO_HEIHOUKON	EQU	SORO_VAR_Y

SORO_IDIV:
		call	SORO_DIV
SORO_FIX:
		push	bc
		push	de
		push	hl
		ld	b, 8*4-1 ; 1Fh
		ld	de, 4
		ld	a, (SORO_KATA)
		cp	8
		jr	nz, .sorofix1
		ld	b, 8*7-1 ; 37h
		ld	de, 7
.sorofix1
		ld	a, (hl)
		sub	81h ; BIAS
		ld	c, a
		jr	nc, .sorofix2
		ld	de, SORO_ATAI0
		ex	de, hl
		call	SORO_MOVE
		jr	.sorofix3
.sorofix2
		cp	b
		jr	nc, .sorofix3
		add	hl, de
		ld	a, b
		sub	c
.sorofix4
		cp	9
		jr	c, .sorofix5
		ld	(hl), 0
		dec	hl
		sub	8
		jr	.sorofix4
.sorofix5
		ld	c, 0FFh
.sorofix6
		sla	c
		dec	a
		jr	nz, .sorofix6
		ld	a, (hl)
		and	c
		ld	(hl), a
.sorofix3
		pop	hl
		pop	de
		pop	bc
		ret	

SORO_FRAC:
		push	de
		ld	de, SORO_FRAC_X
		call	SORO_MOVE
		ex	de, hl
		call	SORO_FIX
		ex	de, hl
		call	SORO_SUB
		pop	de
		ret	

; WORK
; SORO_FRAC_X:	ds SORO_BYTE

SORO_CINT:
		push	de
		ld	de, SORO_CINT_X
		call	SORO_MOVE
		ex	de, hl
		call	SORO_FRAC
		ld	a, (hl)
		cp	80h ; BIAS-1
		push	af
		ld	hl, SORO_FRAC_X
		call	SORO_MOVE
		ex	de, hl
		pop	af
		pop	de
		ret	nz
		ld	a, (SORO_FUGOU1)
		cp	1
		jr	z, SORO_DEC
SORO_INC:
		push	de
		ld	de, SORO_ATAI1
		call	SORO_ADD
		pop	de
		ret	

; WORK
; SORO_CINT_X:	ds SORO_BYTE

SORO_INT:
		call	SORO_FIX
		inc	hl
		bit	7, (hl)
		dec	hl
		ret	z

SORO_DEC:
		push	de
		ld	de, SORO_ATAI1
		call	SORO_SUB
		pop	de
		ret	

SORO_CVDBL:
		push	hl
		inc	hl
		inc	hl
		inc	hl
		inc	hl
		inc	hl
		xor	a
		ld	(hl), a
		inc	hl
		ld	(hl), a
		inc	hl
		ld	(hl), a
		pop	hl
		ret	

SORO_CVSNG:
		push	bc
		push	de
		push	hl
		ld	a, (SORO_KATA)
		push	af
		ld	a, 8
		ld	(SORO_KATA), a
		call	SORO_TOBUFF1
		ld	a, 5
		ld	(SORO_KATA), a
		call	SORO_SEIKIKA
		pop	af
		ld	(SORO_KATA), a
		pop	de
		push	de
		ld	hl, SORO_SHISU1
		ld	bc, 5
		ldir	
		xor	a
		ld	(de), a
		inc	de
		ld	(de), a
		inc	de
		ld	(de), a
		pop	hl
		pop	de
		pop	bc
		ret	

SORO_CVUTF:
		push	bc
		push	de
		push	hl
		push	de
		ld	hl,SORO_ATAI0
		call	SORO_TOBUFF1
		ld	a, 90h ; BIAS+16-1
		ld	(SORO_SHISU1), a
		pop	de
		ld	l, d
		ld	h, e
		ld	(SORO_KARISU1+0), hl
		jp	SORO_ENZANSHURYO

SORO_CVITF:
		bit	7, d
		jr	z, SORO_CVUTF
		push	de
		call	SORO_NEG_DE
		call	SORO_CVUTF
		pop	de
SORO_NEG:
		inc	(hl)
		dec	(hl)
		ret	z
		inc	hl
		ld	a, (hl)
		xor	80h
		ld	(hl), a
		dec	hl
		ret	

SORO_CVFTU:
		push	hl
		ld	a, (hl)
		inc	hl
		ld	d, (hl)
		inc	hl
		ld	e, (hl)
		inc	hl
		ld	h, (hl)
		bit	7, d
		push	af
		or	a
		jr	z, .cvftu1
		set	7, d
.cvftu2
		cp	90h ; BIAS+15
		jr	nc, .cvftu3
		srl	d
		rr	e
		rr	h
		inc	a
		jr	.cvftu2
.cvftu3
		bit	7, h
		jr	z, .cvftu1
		inc	de
.cvftu1
		pop	af
		pop	hl
		ret	

SORO_CVFTI:
		call	SORO_CVFTU
		ret	z

SORO_NEG_DE:
		ld	a, d
		cpl	
		ld	d, a
		ld	a, e
		cpl	
		ld	e, a
		inc	de
		ret	

SORO_CVSTF:
		push	bc
		ld	(SORO_STF_HL), hl
.cvstf1
		ld	a, (de)
		cp	20h ; ' '
		jr	nz, .cvstf2
		inc	de
		jr	.cvstf1
.cvstf2
		call	SORO_STFFUGO
		push	af
		push	de
		ld	de, SORO_ATAI0
		ex	de, hl
		call	SORO_MOVE
		ex	de, hl
		pop	de
		xor	a
		ld	(SORO_CNT), a
		ld	c, 0
.cvstf3
		inc	c
		dec	c
		jr	nz, .cvstf4
		ld	a, (de)
		cp	2Eh ; '.'
		jr	nz, .cvstf4
		inc	de
		inc	c
.cvstf4
		ld	a, (de)
		cp	30h ; '0'
		jr	c, .cvstf5
		cp	3Ah ; '9'+1
		jr	nc, .cvstf5
		call	SORO_MUL10
		ld	a, (de)
		sub	30h ; '0'
		call	SORO_ADDA
		inc	de
		ld	a, (SORO_CNT)
		sub	c
		ld	(SORO_CNT), a
		jr	.cvstf3
.cvstf5
		ld	a, (de)
		cp	45h ; 'E'
		jr	z, .cvstf6
		cp	65h ; 'e'
		jr	nz, .cvstf7
.cvstf6
		inc	de
		call	SORO_STFFUGO
		push	af
		ld	hl, 0
.cvstf8
		ld	a, (de)
		sub	30h ; '0'
		jr	c, .cvstf9
		cp	0Ah
		jr	nc, .cvstf9
		add	hl, hl
		ld	b, h
		ld	c, l
		add	hl, hl
		add	hl, hl
		add	hl, bc
		ld	c, a
		ld	b, 0
		add	hl, bc
		inc	de
		jr	.cvstf8
.cvstf9
		pop	af
		cp	1
		jr	nz, .cvstf10
		ld	a, l
		neg	
		ld	l, a
.cvstf10
		ld	a, (SORO_CNT)
		add	a, l
		ld	(SORO_CNT), a
.cvstf7
		ld	hl, (SORO_STF_HL)
		ld	a, (SORO_CNT)
		or	a
		jr	z, .cvstf11
		cp	80h ; '€'
		jr	nc, .cvstf12
		ld	b, a
.cvstf13
		call	SORO_MUL10
		djnz	.cvstf13
		jr	.cvstf11
.cvstf12
		neg	
		ld	b, a
.cvstf14
		call	SORO_DIV10
		djnz	.cvstf14
.cvstf11
		pop	af
		cp	1
		call	z, SORO_NEG
		pop	bc
		ret	

SORO_STFFUGO:
		ld	a, (de)
		cp	2Dh ; '-'
		jr	nz, .stffugo1
		inc	de
		ld	a, 1
		ret	
.stffugo1
		cp	2Bh ; '+'
		jr	nz, .stffugo2
		inc	de
.stffugo2
		ld	a, 0
		ret	

; WORK
; SORO_CNT:	db 0
; SORO_STF_HL:	dw 0

SORO_CVFTS:
		push	bc
		push	de
		push	hl
		push	de
		ld	de, SORO_VAR_X
		call	SORO_MOVE
		ex	de, hl
		pop	de
		ld	a, (hl)
		or	a
		jr	nz, .cvfts1
		ld	a, 20h ; ' '
		ld	(de), a
		inc	de
		ld	a, 30h ; '0'
		ld	(de), a
		inc	de
		jp	SORO_CVFTSEND
.cvfts1
		inc	hl
		bit	7, (hl)
		res	7, (hl)
		ld	a, 20h ; ' '
		jr	z, .cvfts2
		ld	a, 2Dh ; '-'
.cvfts2
		ld	(de), a
		inc	de
		push	de
		ld	hl, SORO_VAR_X
		ld	de, SORO_ATAIE8
		ld	c, 8
		ld	a, (SORO_KATA)
		cp	8
		jr	nz, .cvfts3
		ld	de, SORO_ATAIE16
		ld	c, 10h
.cvfts3
		push	de
		ld	b, 0
		ld	a, (hl)
		cp	81h ; BIAS+0
		jr	c, .cvfts4
.cvfts5
		call	SORO_CMP
		jr	c, .cvfts6
		call	SORO_DIV
		ld	a, b
		add	a, c
		ld	b, a
		jr	.cvfts5
.cvfts6
		jr	.cvfts7
.cvfts4
		call	SORO_MUL
		ld	a, b
		sub	c
		ld	b, a
		ld	a, (hl)
		cp	81h ; BIAS+0
		jr	c, .cvfts4
.cvfts7
		ld	a, b
		ld	(SORO_TENSHISUU), a
		ld	bc, SORO_ATAIE7
		ld	a, (SORO_KATA)
		cp	8
		jr	nz, .cvfts8
		ld	bc, SORO_ATAIE15
.cvfts8
		ld	de, SORO_ATAI10
.cvfts9
		call	SORO_CMP
		jr	c, .cvfts10
		ld	a, (SORO_TENSHISUU)
		inc	a
		ld	(SORO_TENSHISUU), a
		ld	a, e
		sub	8
		ld	e, a
		jr	nc, .cvfts11
		dec	d
.cvfts11
		ld	a, c
		add	a, 8
		ld	c, a
		jr	nc, .cvfts12
		inc	b
.cvfts12
		jr	.cvfts9
.cvfts10
		ld	d, b
		ld	e, c
		call	SORO_MUL
		call	SORO_CINT
		pop	de
		call	SORO_CMP
		jr	c, .cvfts13
		call	SORO_DIV10
		ld	a, (SORO_TENSHISUU)
		inc	a
		ld	(SORO_TENSHISUU), a
.cvfts13
		pop	de
		ld	a, 20h ; ' '
		ld	(de), a
		inc	de
		push	de
		ld	b, 0F8h
		ld	a, (SORO_KATA)
		cp	8
		jr	nz, .cvfts14
		ld	b, 0F0h
.cvfts14
		ld	a, (SORO_TENSHISUU)
		cp	b
		jr	c, .cvfts15
		neg	
		dec	de
		ld	b, a
.cvfts16
		ld	a, 30h ; '0'
		ld	(de), a
		inc	de
		djnz	.cvfts16
.cvfts15
		ld	a, (SORO_KATA)
		cp	8
		jr	nz, .cvfts17
		ld	bc, SORO_ATAIE12
		call	SORO_FTS4
		ld	bc, SORO_ATAIE8
		call	SORO_FTS4
.cvfts17
		ld	bc, SORO_ATAIE4
		call	SORO_FTS4
		ld	hl, SORO_VAR_X
		call	SORO_FTS_PUT4
		pop	hl
		ld	b, 8
		ld	a, (SORO_KATA)
		cp	8
		jr	nz, .cvfts18
		ld	b, 10h
.cvfts18
		ld	a, (SORO_TENSHISUU)
		ld	c, a
		cp	b
		jr	nc, .cvfts19
		push	de
		ld	d, h
		ld	e, l
		dec	de
		ld	b, 0
		inc	bc
		ldir	
		ex	de, hl
		pop	de
		jr	.cvfts20
.cvfts19
		ld	b, (hl)
		dec	hl
		ld	a, (hl)
		cp	30h ; '0'
		jr	nz, .cvfts21
		ld	c, 0
		jr	.cvfts20
.cvfts21
		ld	(hl), b
		inc	hl
.cvfts20
		ld	a, c
		ld	(SORO_TENSHISUU), a
		ld	(hl), 2Eh ; '.'
.cvfts22
		dec	de
		ld	a, (de)
		cp	30h ; '0'
		jr	z, .cvfts22
		cp	2Eh ; '.'
		jr	z, .cvfts23
		inc	de
.cvfts23
		ld	a, (SORO_TENSHISUU)
		ld	l, a
		or	a
		jr	z, SORO_CVFTSEND
		ld	a, 45h ; 'E'
		ld	(de), a
		inc	de
		ld	a, l
		cp	80h
		jr	nc, .cvfts24
		ld	a, 2Bh ; '+'
		jr	.cvfts25
.cvfts24
		neg	
		ld	l, a
		ld	a, 2Dh ; '-'
.cvfts25
		ld	(de), a
		inc	de
		call	SORO_FTS_PUT2

SORO_CVFTSEND:
		xor	a
		ld	(de), a
		pop	hl
		pop	de
		pop	bc
		ret	

SORO_FTS4:
		push	de
		ld	hl, SORO_VAR_X
		ld	de, SORO_VAR_Y
		call	SORO_MOVE
		ex	de, hl
		ld	d, b
		ld	e, c
		call	SORO_IDIV
		ld	de, SORO_VAR_XX
		call	SORO_MOVE
		ex	de, hl
		ld	d, b
		ld	e, c
		call	SORO_MUL
		ex	de, hl
		ld	hl, SORO_VAR_X
		call	SORO_SUB
		pop	de
		ld	hl, SORO_VAR_Y
SORO_FTS_PUT4:
		push	de
		call	SORO_CVFTU
		ex	de, hl
		pop	de
		ld	bc, 1000
		ld	a, 2Fh ; '0'-1
		or	a
.sorofts41
		inc	a
		sbc	hl, bc
		jr	nc, .sorofts41
		add	hl, bc
		ld	(de), a
		inc	de
		ld	bc, 100
		ld	a, 2Fh ; '0'-1
		or	a
.sorofts42
		inc	a
		sbc	hl, bc
		jr	nc, .sorofts42
		add	hl, bc
		ld	(de), a
		inc	de
SORO_FTS_PUT2:
		ld	b, 10
		ld	a, l
		ld	c, 2Fh ; '0'-1
.soroftsput21
		inc	c
		sub	b
		jr	nc, .soroftsput21
		add	a, b
		ld	l, a
		ld	a, c
		ld	(de), a
		inc	de
		ld	a, l
		add	a, 30h ; '0'
		ld	(de), a
		inc	de
		ret	


; WORK
; SORO_TENSHISUU:	ds 1

; CONST TABLE

SORO_ATAIE16:
		db 0B6h
		db  0Eh
		db  1Bh
		db 0C9h
		db 0BFh
		db    4
		db    0
		db    0
SORO_ATAIE15:
		db 0B2h
		db  63h
		db  5Fh
		db 0A9h
		db  31h
		db 0A0h
		db    0
		db    0
SORO_ATAIE14:
		db 0AFh
		db  35h
		db 0E6h
		db  20h
		db 0F4h
		db  80h
		db    0
		db    0
SORO_ATAIE13:
		db 0ACh
		db  11h
		db  84h
		db 0E7h
		db  2Ah
		db    0
		db    0
		db    0
SORO_ATAIE12:
		db 0A8h
		db  68h
		db 0D4h
		db 0A5h
		db  10h
		db    0
		db    0
		db    0
SORO_ATAIE11:
		db 0A5h
		db  3Ah
		db  43h
		db 0B7h
		db  40h
		db    0
		db    0
		db    0
SORO_ATAIE10:
		db 0A2h
		db  15h
		db    2
		db 0F9h
		db    0
		db    0
		db    0
		db    0
SORO_ATAIE9:
		db  9Eh
		db  6Eh
		db  6Bh
		db  28h
		db    0
		db    0
		db    0
		db    0
SORO_ATAIE8:
		db  9Bh
		db  3Eh
		db 0BCh
		db  20h
		db    0
		db    0
		db    0
		db    0
SORO_ATAIE7:
		db  98h
		db  18h
		db  96h
		db  80h
		db    0
		db    0
		db    0
		db    0
SORO_ATAIE6:
		db  94h
		db  74h
		db  24h
		db    0
		db    0
		db    0
		db    0
		db    0
SORO_ATAIE5:
		db  91h
		db  43h
		db  50h
		db    0
		db    0
		db    0
		db    0
		db    0
SORO_ATAIE4:
		db  8Eh
		db  1Ch
		db  40h
		db    0
		db    0
		db    0
		db    0
		db    0
SORO_ATAIE3:
		db  8Ah
		db  7Ah
		db    0
		db    0
		db    0
		db    0
		db    0
		db    0
SORO_ATAIE2:
		db  87h
		db  48h
		db    0
		db    0
		db    0
		db    0
		db    0
		db    0
SORO_ATAI10:
		db  84h
		db  20h
		db    0
		db    0
		db    0
		db    0
		db    0
		db    0
SORO_ATAI1:
		db  81h
		db    0
		db    0
		db    0
		db    0
		db    0
		db    0
		db    0
SORO_ATAI0:
		db    0
		db    0
		db    0
		db    0
		db    0
		db    0
		db    0
		db    0

SORO_TAN:
		push	de
		ld	de, SORO_TAN_X
		call	SORO_MOVE
		call	SORO_SIN
		ex	de, hl
		call	SORO_COS
		ex	de, hl
		call	SORO_DIV
		pop	de
		ret	

; WORK
; SORO_TAN_X:	ds SORO_BYTE

SORO_COS:
		push	bc
		push	de
		push	hl
		ld	de, SORO_ATAIPAI2
		call	SORO_ADD
		jr	SORO_SIN_PROC

SORO_SIN:
		push	bc
		push	de
		push	hl
SORO_SIN_PROC:
		ld	c, 0
		ld	de, SORO_VAR_X
		call	SORO_MOVE
		ex	de, hl
		ld	de, SORO_ATAI2PAI
		call	SORO_MOD
		inc	hl
		bit	7, (hl)
		jr	z, .sorosin1
		res	7, (hl)
		inc	c
.sorosin1
		dec	hl
		ld	de, SORO_ATAIPAI
		call	SORO_CMP
		jr	c, .sorosin2
		call	SORO_SUB
		inc	c
.sorosin2
		ld	de, SORO_ATAIPAI2
		call	SORO_CMP
		jr	c, .sorosin3
		ld	de, SORO_ATAIPAI
		call	SORO_SUB
		inc	c
.sorosin3
		bit	0, c
		call	nz, SORO_NEG
		ld	a, (hl)
		cp	4Dh
		jr	nc, .sorosin4
		pop	de
		ld	hl, SORO_ATAI0
		call	SORO_MOVE
		ex	de, hl
		jr	.sorosin5
.sorosin4
		ld	de, SORO_VAR_XX
		call	SORO_MOVE
		ex	de, hl
		call	SORO_MUL
		pop	de
		ld	hl, SORO_SIN_TBL
		call	SORO_MOVE
		ex	de, hl
		ld	b, 0Ah
		ld	a, (SORO_KATA)
		cp	5
		jr	nz, .sorosin6
		ld	b, 8
		ld	de, SORO_SIN_TBL+SORO_BYTE*2
.sorosin6
		push	de
		ld	de, SORO_VAR_XX
		call	SORO_MUL
		pop	de
		ld	a, e
		add	a, 8
		ld	e, a
		jr	nc, .sorosin7
		inc	d
.sorosin7
		call	SORO_ADD
		djnz	.sorosin6
		ld	de, SORO_VAR_X
		call	SORO_MUL
.sorosin5
		pop	de
		pop	bc
		ret	

SORO_ATAIPAI2:	db  81h
		db  49h
		db  0Fh
		db 0DAh
		db 0A2h
		db  21h
		db  68h
		db 0C2h
SORO_ATAIPAI:	db  82h
		db  49h
		db  0Fh
		db 0DAh
		db 0A2h
		db  21h
		db  68h
		db 0C2h
SORO_ATAI2PAI:	db  83h
		db  49h
		db  0Fh
		db 0DAh
		db 0A2h
		db  21h
		db  68h
		db 0C2h
SORO_SIN_TBL:	db  3Fh
		db  38h
		db 0DCh
		db  77h
		db 0B6h
		db 0E7h
		db 0ABh
		db  8Ch
		db  48h
		db  97h
		db 0A4h
		db 0DAh
		db  34h
		db  0Ah
		db  0Ah
		db 0B9h
		db  50h
		db  4Ah
		db  96h
		db  3Bh
		db  81h
		db  85h
		db  6Ah
		db  53h
		db  58h
		db 0D7h
		db  3Fh
		db  9Fh
		db  39h
		db  9Dh
		db 0C0h
		db 0F9h
		db  60h
		db  30h
		db  92h
		db  30h
		db  9Dh
		db  43h
		db  68h
		db  4Ch
		db  67h
		db 0D7h
		db  32h
		db  2Bh
		db  3Fh
		db 0AAh
		db  27h
		db  1Ch
		db  6Eh
		db  38h
		db 0EFh
		db  1Dh
		db  2Ah
		db 0B6h
		db  39h
		db  9Ch
		db  74h
		db 0D0h
		db  0Dh
		db    0
		db 0D0h
		db  0Dh
		db    0
		db 0D0h
		db  7Ah
		db    8
		db  88h
		db  88h
		db  88h
		db  88h
		db  88h
		db  89h
		db  7Eh
		db 0AAh
		db 0AAh
		db 0AAh
		db 0AAh
		db 0AAh
		db 0AAh
		db 0ABh
		db  81h
		db    0
		db    0
		db    0
		db    0
		db    0
		db    0
		db    0

SORO_ATN:
		push	bc
		push	de
		inc	hl
		bit	7, (hl)
		res	7, (hl)
		dec	hl
		push	af
		ld	de, SORO_ATAI1
		call	SORO_CMP
		jr	nc, .soroatn1
		call	SORO_ATNFUNC
		jr	.soroatn2
.soroatn1
		jr	nz, .soroatn3
		ld	de, SORO_ATAIPAI2
		ex	de, hl
		call	SORO_MOVE
		ex	de, hl
		dec	(hl)
		jr	.soroatn2
.soroatn3
		ld	de, SORO_VAR_X
		call	SORO_MOVE
		ld	de, SORO_ATAI1
		ex	de, hl
		call	SORO_MOVE
		ex	de, hl
		ld	de, SORO_VAR_X
		call	SORO_DIV
		call	SORO_ATNFUNC
		ld	de, SORO_ATAIPAI2
		call	SORO_SUB
		call	SORO_NEG
.soroatn2
		pop	af
		call	nz, SORO_NEG
		pop	de
		pop	bc
		ret	

SORO_ATNFUNC:
		ld	de, SORO_ATAI414
		call	SORO_CMP
		push	af
		jr	c, .soroatnfunc1
		ld	de, SORO_VAR_X
		call	SORO_MOVE
		ld	de, SORO_ATAI414
		call	SORO_SUB
		push	hl
		ld	hl, SORO_VAR_X
		call	SORO_MUL
		call	SORO_INC
		ex	de, hl
		pop	hl
		call	SORO_DIV
.soroatnfunc1
		ld	de, SORO_VAR_X
		call	SORO_MOVE
		ld	de, SORO_VAR_XX
		call	SORO_MOVE
		ex	de, hl
		call	SORO_MUL
		ld	hl, SORO_ATN_TBL
		call	SORO_MOVE
		ex	de, hl
		ld	b, 12h
		ld	a, (SORO_KATA)
		cp	5
		jr	nz, .soroatnfunc2
		ld	b, 0Ah
		ld	de, SORO_ATN_TBL+SORO_BYTE*8
.soroatnfunc2
		push	de
		ld	de, SORO_VAR_XX
		call	SORO_MUL
		pop	de
		ld	a, e
		add	a, 8
		ld	e, a
		jr	nc, .soroatnfunc3
		inc	d
.soroatnfunc3
		call	SORO_ADD
		djnz	.soroatnfunc2
		ld	de, SORO_VAR_X
		call	SORO_MUL
		pop	af
		jr	c, .soroatnfunc4
		ld	de, SORO_ATAIATN414
		call	SORO_ADD
.soroatnfunc4
		ret	

SORO_ATAI414:	db  7Fh
		db  54h
		db  12h
		db    5
		db 0BCh
		db    1
		db 0A3h
		db  6Eh
SORO_ATAIATN414:
		db  7Fh
		db  49h
		db  0Eh
		db  56h
		db  32h
		db 0C5h
		db  5Bh
		db  5Ch
SORO_ATN_TBL:	db  7Bh
		db  5Dh
		db  67h
		db 0C8h
		db 0A6h
		db  0Dh
		db 0D6h
		db  7Dh
		db  7Bh
		db 0EAh
		db  0Eh
		db 0A0h
		db 0EAh
		db  0Eh
		db 0A0h
		db 0EAh
		db  7Bh
		db  78h
		db  3Eh
		db  0Fh
		db  83h
		db 0E0h
		db 0F8h
		db  3Eh
		db  7Ch
		db  84h
		db  21h
		db    8
		db  42h
		db  10h
		db  84h
		db  21h
		db  7Ch
		db  0Dh
		db  3Dh
		db 0CBh
		db    8
		db 0D3h
		db 0DCh
		db 0B1h
		db  7Ch
		db  97h
		db 0B4h
		db  25h
		db 0EDh
		db    9
		db  7Bh
		db  42h
		db  7Ch
		db  23h
		db 0D7h
		db  0Ah
		db  3Dh
		db  70h
		db 0A3h
		db 0D7h
		db  7Ch
		db 0B2h
		db  16h
		db  42h
		db 0C8h
		db  59h
		db  0Bh
		db  21h
		db  7Ch
		db  43h
		db  0Ch
		db  30h
		db 0C3h
		db  0Ch
		db  30h
		db 0C3h
		db  7Ch
		db 0D7h
		db  94h
		db  35h
		db 0E5h
		db  0Dh
		db  79h
		db  43h
		db  7Ch
		db  70h
		db 0F0h
		db 0F0h
		db 0F0h
		db 0F0h
		db 0F0h
		db 0F1h
		db  7Dh
		db  88h
		db  88h
		db  88h
		db  88h
		db  88h
		db  88h
		db  89h
		db  7Dh
		db  1Dh
		db  89h
		db 0D8h
		db  9Dh
		db  89h
		db 0D8h
		db  9Eh
		db  7Dh
		db 0BAh
		db  2Eh
		db  8Bh
		db 0A2h
		db 0E8h
		db 0BAh
		db  2Fh
		db  7Dh
		db  63h
		db  8Eh
		db  38h
		db 0E3h
		db  8Eh
		db  38h
		db 0E4h
		db  7Eh
		db  92h
		db  49h
		db  24h
		db  92h
		db  49h
		db  24h
		db  92h
		db  7Eh
		db  4Ch
		db 0CCh
		db 0CCh
		db 0CCh
		db 0CCh
		db 0CCh
		db 0CDh
		db  7Fh
		db 0AAh
		db 0AAh
		db 0AAh
		db 0AAh
		db 0AAh
		db 0AAh
		db 0ABh
		db  81h
		db    0
		db    0
		db    0
		db    0
		db    0
		db    0
		db    0

SORO_POW:
		push	de
		push	hl
		inc	hl
		bit	7, (hl)
		res	7, (hl)
		ld	a, 0
		jr	z, .soropow1
		ld	a, 1
.soropow1
		ld	(SORO_POW_FUGO), a
		ld	hl, SORO_VAR_X
		ex	de, hl
		call	SORO_MOVE
		ex	de, hl
		call	SORO_FRAC
		ld	a, (hl)
		pop	hl
		pop	de
		or	a
		jr	nz, .soropow2
		ld	a, (de)
		cp	91h ; BIAS+15
		jr	c, .soropow3
.soropow2
		ld	a, (SORO_POW_FUGO)
		cp	1
		ret	z
		jr	SORO_POW_EXLOG
.soropow3
		push	de
		push	bc
		push	hl
		ex	de, hl
		call	SORO_CVFTU
		ld	b, d
		ld	c, e
		ex	de, hl
		pop	hl
		push	bc
		ld	a, c
		sub	32h
		ld	a, b
		sbc	a, 0
		jr	c, .soropow4
		call	SORO_POW_EXLOG
		jr	.soropow5
.soropow4
		inc	de
		ld	b, 0
		ld	a, (de)
		bit	7, a
		jr	z, .soropow6
		ld	b, 1
.soropow6
		ld	de, SORO_VAR_X
		call	SORO_MOVE
		ld	de, SORO_ATAI1
		ex	de, hl
		call	SORO_MOVE
		ex	de, hl
		ld	de, SORO_VAR_X
.soropow7
		inc	c
		dec	c
		jr	z, .soropow5
		ld	a, b
		cp	0
		jr	nz, .soropow8
		call	SORO_MUL
		jr	.soropow9
.soropow8
		call	SORO_DIV
.soropow9
		dec	c
		jr	.soropow7
.soropow5
		pop	bc
		ld	a, (SORO_POW_FUGO)
		cp	1
		jr	nz, .soropow10
		bit	0, c
		call	nz, SORO_NEG
.soropow10
		pop	bc
		pop	de
		ret	

; WORK
; SORO_POW_FUGO:	ds 1

SORO_POW_EXLOG:
		call	SORO_LOG
		call	SORO_MUL
SORO_EXP:
		push	bc
		push	de
		push	hl
		ld	de, SORO_VAR_X
		call	SORO_MOVE
		ex	de, hl
		ld	de, SORO_ATAILe2
		call	SORO_DIV
		call	SORO_FRAC
		ld	de, SORO_ATAILe2
		call	SORO_MUL
		pop	de
		ld	hl, SORO_EXP_TBL
		call	SORO_MOVE
		ex	de, hl
		ld	b, 10h
		ld	a, (SORO_KATA)
		cp	5
		jr	nz, .soroexp1
		ld	b, 0Ch
		ld	de, SORO_EXP_TBL+SORO_BYTE*4
.soroexp1
		push	de
		ld	de, SORO_VAR_X
		call	SORO_MUL
		pop	de
		ld	a, e
		add	a, 8
		ld	e, a
		jr	nc, .soroexp2
		inc	d
.soroexp2
		call	SORO_ADD
		djnz	.soroexp1
		push	hl
		ld	hl, SORO_FRAC_X
		call	SORO_CVFTI
		pop	hl
		ld	a, (hl)
		add	a, e
		ld	(hl), a
		pop	de
		pop	bc
		ret	

SORO_ATAILe2:
		db  80h
		db  31h
		db  72h
		db  17h
		db 0F7h
		db 0D1h
		db 0CFh
		db  7Ah
SORO_EXP_TBL:	db  54h
		db  57h
		db  3Fh
		db  9Fh
		db  39h
		db  9Dh
		db 0C0h
		db 0F9h
		db  58h
		db  57h
		db  3Fh
		db  9Fh
		db  39h
		db  9Dh
		db 0C0h
		db 0F9h
		db  5Ch
		db  49h
		db 0CBh
		db 0A5h
		db  46h
		db    3
		db 0E4h
		db 0E9h
		db  60h
		db  30h
		db  92h
		db  30h
		db  9Dh
		db  43h
		db  68h
		db  4Ch
		db  64h
		db  0Fh
		db  76h
		db 0C7h
		db  7Fh
		db 0C6h
		db 0C4h
		db 0BEh
		db  67h
		db  57h
		db  32h
		db  2Bh
		db  3Fh
		db 0AAh
		db  27h
		db  1Ch
		db  6Bh
		db  13h
		db 0F2h
		db  7Dh
		db 0BBh
		db 0C4h
		db 0FAh
		db 0E4h
		db  6Eh
		db  38h
		db 0EFh
		db  1Dh
		db  2Ah
		db 0B6h
		db  39h
		db  9Ch
		db  71h
		db  50h
		db  0Dh
		db    0
		db 0D0h
		db  0Dh
		db    0
		db 0D0h
		db  74h
		db  50h
		db  0Dh
		db    0
		db 0D0h
		db  0Dh
		db    0
		db 0D0h
		db  77h
		db  36h
		db  0Bh
		db  60h
		db 0B6h
		db  0Bh
		db  60h
		db 0B6h
		db  7Ah
		db    8
		db  88h
		db  88h
		db  88h
		db  88h
		db  88h
		db  89h
		db  7Ch
		db  2Ah
		db 0AAh
		db 0AAh
		db 0AAh
		db 0AAh
		db 0AAh
		db 0ABh
		db  7Eh
		db  2Ah
		db 0AAh
		db 0AAh
		db 0AAh
		db 0AAh
		db 0AAh
		db 0ABh
		db  80h
		db    0
		db    0
		db    0
		db    0
		db    0
		db    0
		db    0
		db  81h
		db    0
		db    0
		db    0
		db    0
		db    0
		db    0
		db    0
		db  81h
		db    0
		db    0
		db    0
		db    0
		db    0
		db    0
		db    0

SORO_LOG:
		push	bc
		push	de
		push	hl
		ld	de, SORO_VAR_X
		call	SORO_MOVE
		ex	de, hl
		ld	a, (hl)
		ld	(SORO_LOG_SHISU), a
		ld	(hl), 81h ; BIAS+0
		ld	de, SORO_VAR_Y
		call	SORO_MOVE
		call	SORO_DEC
		ex	de, hl
		call	SORO_INC
		ex	de, hl
		call	SORO_DIV
		ld	de, SORO_VAR_XX
		call	SORO_MOVE
		ex	de, hl
		call	SORO_MUL
		pop	de
		ld	hl, SORO_LOG_TBL
		call	SORO_MOVE
		ex	de, hl
		ld	b, 0Eh
		ld	a, (SORO_KATA)
		cp	5
		jr	nz, .sorolog1
		ld	b, 8
		ld	de, SORO_LOG_TBL+SORO_BYTE*6	; OK?
.sorolog1
		push	de
		ld	de, SORO_VAR_XX
		call	SORO_MUL
		pop	de
		ld	a, e
		add	a, 8
		ld	e, a
		jr	nc, .sorolog2
		inc	d
.sorolog2
		call	SORO_ADD
		djnz	.sorolog1
		ld	de, SORO_VAR_X
		call	SORO_MUL
		inc	(hl)
		push	hl
		ld	a, (SORO_LOG_SHISU)
		sub	81h ; BIAS
		ld	e, a
		ld	a, d
		sbc	a, d
		ld	d, a
		ld	hl, SORO_VAR_Y
		call	SORO_CVITF
		ld	de, SORO_ATAILe2
		call	SORO_MUL
		ex	de, hl
		pop	hl
		call	SORO_ADD
		pop	de
		pop	bc
		ret	

; WORK
; SORO_LOG_SHISU:	ds 1

SORO_LOG_TBL:	db  7Ch
		db  0Dh
		db  3Dh
		db 0CBh
		db    8
		db 0D3h
		db 0DCh
		db 0B1h
		db  7Ch
		db  17h
		db 0B4h
		db  25h
		db 0EDh
		db    9
		db  7Bh
		db  42h
		db  7Ch
		db  23h
		db 0D7h
		db  0Ah
		db  3Dh
		db  70h
		db 0A3h
		db 0D7h
		db  7Ch
		db  32h
		db  16h
		db  42h
		db 0C8h
		db  59h
		db  0Bh
		db  21h
		db  7Ch
		db  43h
		db  0Ch
		db  30h
		db 0C3h
		db  0Ch
		db  30h
		db 0C3h
		db  7Ch
		db  57h
		db  94h
		db  35h
		db 0E5h
		db  0Dh
		db  79h
		db  43h
		db  7Ch
		db  70h
		db 0F0h
		db 0F0h
		db 0F0h
		db 0F0h
		db 0F0h
		db 0F1h
		db  7Dh
		db    8
		db  88h
		db  88h
		db  88h
		db  88h
		db  88h
		db  89h
		db  7Dh
		db  1Dh
		db  89h
		db 0D8h
		db  9Dh
		db  89h
		db 0D8h
		db  9Eh
		db  7Dh
		db  3Ah
		db  2Eh
		db  8Bh
		db 0A2h
		db 0E8h
		db 0BAh
		db  2Fh
		db  7Dh
		db  63h
		db  8Eh
		db  38h
		db 0E3h
		db  8Eh
		db  38h
		db 0E4h
		db  7Eh
		db  12h
		db  49h
		db  24h
		db  92h
		db  49h
		db  24h
		db  92h
		db  7Eh
		db  4Ch
		db 0CCh
		db 0CCh
		db 0CCh
		db 0CCh
		db 0CCh
		db 0CDh
		db  7Fh
		db  2Ah
		db 0AAh
		db 0AAh
		db 0AAh
		db 0AAh
		db 0AAh
		db 0ABh
		db  81h
		db    0
		db    0
		db    0
		db    0
		db    0
		db    0
		db    0

SORO_ABS:
		inc	hl
		res	7, (hl)
		dec	hl
		ret	
SORO_SGN:
		inc	(hl)
		dec	(hl)
		ret	z
		push	de
		inc	hl
		bit	7, (hl)
		dec	hl
		push	af
		ld	de, SORO_ATAI1
		ex	de, hl
		call	SORO_MOVE
		ex	de, hl
		pop	af
		call	nz, SORO_NEG
		pop	de
		ret	

SORO_PAI:
		push	de
		jr	SORO_RAD_1
SORO_RAD:
		push	de
		ld	de, SORO_ATAI180
		call	SORO_DIV
SORO_RAD_1:
		ld	de, SORO_ATAIPAI
		call	SORO_MUL
		pop	de
		ret	

SORO_ATAI180:	db  88h
		db  34h
		db    0
		db    0
		db    0
		db    0
		db    0
		db    0
#ENDLIB
