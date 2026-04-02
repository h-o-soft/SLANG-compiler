; MAGIC X1

#LIB CALLMAGIC
	 	LD HL,ENT
	 	JP NAME_SPACE_DEFAULT.RCALL
#ENDLIB

#LIB MAGICLIB
B_ENT:
		ld	ix, (POINTER)
MAGICENTRY:
ENT:
		jr	MAIN
LINE_SIDE:
		db    0
		db  80h
		db 0C0h
		db 0E0h
		db 0F0h
		db 0F8h
		db 0FCh
		db 0FEh
DOT_TABLE:
		db  80h
		db  40h
		db  20h
		db  10h
		db    8
		db    4
		db    2
		db    1

MAIN:
		ld	a, (PLANE)
		rlca	
		rlca	
		and	3
		jr	nz, sB020
		inc	a

sB020:
		rrca	
		rrca	
		ld	(PLANE), a
		ld	a, (ix+0)
		and	0Fh
		inc	ix
		ld	hl, MAIN
		push	hl
		add	a, a
		ld	l, a
		ld	h, 0
		ld	de, JMPTBL
		add	hl, de
		ld	a, (hl)
		inc	hl
		ld	h, (hl)
		ld	l, a
		jp	(hl)

JMPTBL:		dw _LINE
		dw _SPLINE
		dw _BOX
		dw _TRIANGLE
		dw _BOXFULL
		dw _CIRCLE
		dw _WINDOW
		dw _MODE
		dw _POINT
		dw _CLS
		dw _PALETTE
		dw _PALA
		dw _DATA
		dw _TRANS
		dw _DISP
		dw _DONE

_LINE:
		ld	a, (ix+0)
		inc	ix
		push	af
		call	sB1C9
		pop	af
		dec	a
		jp	z, PSET

sB06B:
		push	af
		call	sB1C9
		ld	hl, (X1)
		push	hl
		ld	hl, (Y1)
		push	hl
		call	CLIPLINE
		pop	hl
		ld	(Y1), hl
		pop	hl
		ld	(X1), hl
		pop	af
		dec	a
		jr	nz, sB06B
		ret	

_SPLINE:
		call	sB1C9
		call	sB1C9
		call	sB1C9
		jp	SPLINE

_BOX:
		push	ix
		pop	hl
		ld	de, WORK
		ld	bc, 8
		ldir	
		push	hl
		pop	ix
		ld	hl, (WORK)
		ld	(X1), hl
		ld	(X2), hl
		ld	hl, (WORK+2)
		ld	(Y1), hl
		ld	hl, (WORK+6)
		ld	(Y2), hl
		call	CLIPLINE
		ld	hl, (WORK+4)
		ld	(X1), hl
		ld	(X2), hl
		ld	hl, (WORK+2)
		ld	(Y1), hl
		ld	hl, (WORK+6)
		ld	(Y2), hl
		call	CLIPLINE
		ld	hl, (WORK+2)
		ld	(Y1), hl
		ld	(Y2), hl
		ld	hl, (WORK)
		ld	(X1), hl
		ld	hl, (WORK+4)
		ld	(X2), hl
		call	CLIPLINE
		ld	hl, (WORK+6)
		ld	(Y1), hl
		ld	(Y2), hl
		ld	hl, (WORK)
		ld	(X1), hl
		ld	hl, (WORK+4)
		ld	(X2), hl
		jp	CLIPLINE

_TRIANGLE:
		call	sB1F3
		call	sB1C9
		call	sB1C9
		call	sB1C9
		jp	TRIANGLE

_BOXFULL:
		call	sB1F3
		call	sB1C9
		call	sB1C9
		jp	BOX_FULL

_CIRCLE:
		call	sB1F3
		call	sB1C9
		ld	l, (ix+0)
		ld	h, (ix+1)
		inc	ix
		inc	ix
		ld	(X2), hl
		jp	CIRCLE_FULL

_MODE:
		ld	a, (ix+0)
		inc	ix
		ld	(XMODE), a
		ld	a, (ix+0)
		inc	ix
		rrca	
		rrca	
		add	a, 40h
		ld	(PLANE), a
		ret	

_POINT:
		call	sB1C9
		ld	de, (X1)
		ld	bc, (Y1)
		call	POINT
		ld	(PDATA), a
		ret	

_WINDOW:
		push	ix
		pop	hl
		ld	de, MINX
		ld	bc, 8
		ldir	
		push	hl
		pop	ix
		ret	

_CLS:
		jp	CLS

_PALETTE:
		push	ix
		pop	hl
		call	PALSET
		push	hl
		pop	ix
		ret	

_PALA:
		ld	a, (ix+0)
		and	0Fh
		add	a, a
		ld	l, a
		ld	h, 0
		ld	de, CX
		add	hl, de
		ex	de, hl
		push	ix
		pop	hl
		inc	hl
		ldi	
		ldi	
		push	hl
		pop	ix
		ret	

_DATA:
		ld	a, (ix+0)
		inc	ix
		ld	(PCT), a
		push	ix
		pop	hl
		ld	de, OBJ_BUF

sB19D:
		ldi	
		ldi	
		ldi	
		ldi	
		ldi	
		ldi	
		dec	a
		jp	nz, sB19D
		ld	a, (hl)
		inc	hl
		ld	(LCT), a
		ld	de, LINE_BUF

sB1B5:
		ldi	
		ldi	
		dec	a
		jp	nz, sB1B5
		push	hl
		pop	ix
		ret	

_TRANS:
		jp	PERSPECTIV

_DISP:
		jp	DISP_FLAME

_DONE:
		pop	hl
		ret	

sB1C9:
		ld	hl, (X2)
		ld	(X3), hl
		ld	hl, (Y2)
		ld	(Y3), hl
		ld	hl, (X1)
		ld	(X2), hl
		ld	hl, (Y1)
		ld	(Y2), hl
		push	ix
		pop	hl
		ld	de, X1
		ldi	
		ldi	
		ldi	
		ldi	
		push	hl
		pop	ix
		ret	

sB1F3:
		push	ix
		pop	hl
		ld	de, MASKS
		ld	bc, 4
		ldir	
		push	hl
		pop	ix
		ret	

CLS:
		ld	hl, (MINX)
		ld	de, (MAXX)
		ld	a, l
		and	0F8h
		or	h
		rrca	
		rrca	
		rrca	
		ld	h, a
		ld	a, e
		and	0F8h
		or	d
		rrca	
		rrca	
		rrca	
		sub	h
		srl	a
		srl	a
		inc	a
		ld	(sB244+1), a
		ld	e, h
		ld	hl, (MINY)
		ld	a, (MAXY)
		sub	l
		ld	d, a
		inc	d
		ld	c, l
		ld	a, l
		and	0F8h
		ld	l, a
		ld	a, c
		ld	h, 0
		add	hl, hl
		ld	b, h
		ld	c, l
		and	7
		add	a, a
		add	a, h
		ld	h, a
		add	hl, hl
		add	hl, hl
		add	hl, bc
		ld	c, e
		ld	a, (PLANE)
		ld	b, a
		add	hl, bc

sB244:
		ld	e, 0
		xor	a
		ld	b, h
		ld	c, l

sB249:
		out	(c), a
		inc	c
		out	(c), a
		inc	c
		out	(c), a
		inc	c
		out	(c), a
		inc	bc
		dec	e
		jp	nz, sB249
		ld	a, 8
		add	a, h
		ld	h, a
		and	38h
		jr	nz, sB265
		ld	bc, 0C050h
		add	hl, bc
sB265:
		dec	d
		jp	nz, sB244
		ret	

PALSET:
		ld	b, 8
		ld	de, 0
		ld	c, d

sB270:
		ld	a, (hl)
		inc	hl
		rrca	
		rr	c
		rrca	
		rr	d
		rrca	
		rr	e
		djnz	sB270
		ld	a, c
		ld	bc, 1000h
		out	(c), a
		inc	b
		out	(c), d
		inc	b
		out	(c), e
		ret	
		ex	(sp), hl
		ld	de, MINX
		ld	bc, 8
		ldir	
		ex	(sp), hl
		ret	
		ld	a, h
		or	l
		ret	z
		ld	a, 1
		bit	7, h
		ret	z
NEG_HL:
		ld	a, h
		cpl	
		ld	h, a
		ld	a, l
		cpl	
		ld	l, a
		inc	hl
		ld	a, 0FFh
		ret	

XDIV:
		ld	a, h
		or	l
		ret	z
		ld	a, d
		or	e
		jr	nz, sB2B6
		bit	7, h
		ld	hl, 7FFFh
		ret	z
		inc	hl
		ret	

sB2B6:
		ld	a, h
		xor	d
		ex	af, af'
		bit	7, h
		jr	z, sB2C4
		ld	a, h
		cpl	
		ld	h, a
		ld	a, l
		cpl	
		ld	l, a
		inc	hl

sB2C4:
		bit	7, d
		jr	z, sB2CF
		ld	a, d
		cpl	
		ld	d, a
		ld	a, e
		cpl	
		ld	e, a
		inc	de

sB2CF:
		ld	a, d
		cpl	
		ld	b, a
		ld	a, e
		cpl	
		ld	c, a
		inc	bc
		xor	a
		add	hl, bc
		jr	c, sB2DC
		add	hl, de
		or	a

sB2DC:
		rla	
		add	hl, hl
		add	hl, bc
		jr	c, sB2E3
		add	hl, de
		or	a

sB2E3:
		rla	
		add	hl, hl
		add	hl, bc
		jr	c, sB2EA
		add	hl, de
		or	a

sB2EA:
		rla	
		add	hl, hl
		add	hl, bc
		jr	c, sB2F1
		add	hl, de
		or	a

sB2F1:
		rla	
		add	hl, hl
		add	hl, bc
		jr	c, sB2F8
		add	hl, de
		or	a

sB2F8:
		rla	
		add	hl, hl
		add	hl, bc
		jr	c, sB2FF
		add	hl, de
		or	a

sB2FF:
		rla	
		add	hl, hl
		add	hl, bc
		jr	c, sB306
		add	hl, de
		or	a

sB306:
		rla	
		add	hl, hl
		add	hl, bc
		jr	c, sB30D
		add	hl, de
		or	a

sB30D:
		rla	
		ld	h, 0
		ld	l, a
		ex	af, af'
		add	a, a
		ret	nc
		ld	a, h
		cpl	
		ld	h, a
		ld	a, l
		cpl	
		ld	l, a
		inc	hl
		ret	

		ld	a, h
		or	l
		ret	z
		ld	a, d
		or	e
		jr	nz, sB32F
		bit	7, h
		jr	nz, sB32B
		ld	hl, 7FFFh
		ret	

sB32B:
		ld	hl, 8000h
		ret	

sB32F:
		ld	a, h
		xor	d
		ex	af, af'
		bit	7, h
		jr	z, sB33D
		ld	a, h
		cpl	
		ld	h, a
		ld	a, l
		cpl	
		ld	l, a
		inc	hl

sB33D:
		bit	7, d
		jr	z, sB348
		ld	a, d
		cpl	
		ld	d, a
		ld	a, e
		cpl	
		ld	e, a
		inc	de

sB348:
		ld	a, d
		cpl	
		ld	b, a
		ld	a, e
		cpl	
		ld	c, a
		inc	bc
		xor	a
		add	hl, bc
		jr	c, sB355
		add	hl, de
		or	a

sB355:
		rla	
		add	hl, hl
		add	hl, bc
		jr	c, sB35C
		add	hl, de
		or	a

sB35C:
		rla	
		add	hl, hl
		add	hl, bc
		jr	c, sB363
		add	hl, de
		or	a

sB363:
		rla	
		add	hl, hl
		add	hl, bc
		jr	c, sB36A
		add	hl, de
		or	a

sB36A:
		rla	
		add	hl, hl
		add	hl, bc
		jr	c, sB371
		add	hl, de
		or	a

sB371:
		rla	
		add	hl, hl
		add	hl, bc
		jr	c, sB378
		add	hl, de
		or	a

sB378:
		rla	
		add	hl, hl
		add	hl, bc
		jr	c, sB37F
		add	hl, de
		or	a

sB37F:
		rla	
		add	hl, hl
		push	af
		add	hl, bc
		jr	c, sB387
		add	hl, de
		or	a

sB387:
		rla	
		add	hl, hl
		add	hl, bc
		jr	c, sB38E
		add	hl, de
		or	a

sB38E:
		rla	
		add	hl, hl
		add	hl, bc
		jr	c, sB395
		add	hl, de
		or	a

sB395:
		rla	
		add	hl, hl
		add	hl, bc
		jr	c, sB39C
		add	hl, de
		or	a

sB39C:
		rla	
		add	hl, hl
		add	hl, bc
		jr	c, sB3A3
		add	hl, de
		or	a

sB3A3:
		rla	
		add	hl, hl
		add	hl, bc
		jr	c, sB3AA
		add	hl, de
		or	a

sB3AA:
		rla	
		add	hl, hl
		add	hl, bc
		jr	c, sB3B1
		add	hl, de
		or	a

sB3B1:
		rla	
		add	hl, hl
		add	hl, bc
		jr	c, sB3B8
		add	hl, de
		or	a

sB3B8:
		rla	
		pop	hl
		ld	l, a
		ex	af, af'
		add	a, a
		ret	nc
		ld	a, h
		cpl	
		ld	h, a
		ld	a, l
		cpl	
		ld	l, a
		inc	hl
		ret	

MUL:
		ld	a, h
		or	l
		jr	z, sB3CE
		ld	a, d
		or	e
		jr	nz, sB3D3

sB3CE:
		ld	h, a
		ld	l, a
		ld	d, a
		ld	e, a
		ret	

sB3D3:
		ld	a, h
		xor	d
		ex	af, af'
		bit	7, d
		jr	z, sB3E1
		ld	a, d
		cpl	
		ld	d, a
		ld	a, e
		cpl	
		ld	e, a
		inc	de

sB3E1:
		bit	7, h
		jr	z, sB3EC
		ld	a, h
		cpl	
		ld	h, a
		ld	a, l
		cpl	
		ld	l, a
		inc	hl

sB3EC:
		ld	a, h
		ld	c, l
		ld	hl, 0
		srl	a
		rr	c
		jr	nc, sB3F9
		ld	h, d
		ld	l, e

sB3F9:
		srl	h
		rr	l
		rra	
		rr	c
		jr	nc, sB403
		add	hl, de

sB403:
		srl	h
		rr	l
		rra	
		rr	c
		jr	nc, sB40D
		add	hl, de

sB40D:
		srl	h
		rr	l
		rra	
		rr	c
		jr	nc, sB417
		add	hl, de

sB417:
		srl	h
		rr	l
		rra	
		rr	c
		jr	nc, sB421
		add	hl, de

sB421:
		srl	h
		rr	l
		rra	
		rr	c
		jr	nc, sB42B
		add	hl, de

sB42B:
		srl	h
		rr	l
		rra	
		rr	c
		jr	nc, sB435
		add	hl, de

sB435:
		srl	h
		rr	l
		rra	
		rr	c
		jr	nc, sB43F
		add	hl, de

sB43F:
		srl	h
		rr	l
		rra	
		rr	c
		jr	nc, sB449
		add	hl, de

sB449:
		srl	h
		rr	l
		rra	
		rr	c
		jr	nc, sB453
		add	hl, de

sB453:
		srl	h
		rr	l
		rra	
		rr	c
		jr	nc, sB45D
		add	hl, de

sB45D:
		srl	h
		rr	l
		rra	
		rr	c
		jr	nc, sB467
		add	hl, de

sB467:
		srl	h
		rr	l
		rra	
		rr	c
		jr	nc, sB471
		add	hl, de

sB471:
		srl	h
		rr	l
		rra	
		rr	c
		jr	nc, sB47B
		add	hl, de

sB47B:
		srl	h
		rr	l
		rra	
		rr	c
		jr	nc, sB485
		add	hl, de

sB485:
		ld	d, a
		ld	e, c
		ex	af, af'
		add	a, a
		ret	nc
		ld	a, h
		cpl	
		ld	h, a
		ld	a, l
		cpl	
		ld	l, a
		ld	a, d
		cpl	
		ld	d, a
		ld	a, e
		cpl	
		ld	e, a
		inc	de
		ld	a, d
		or	e
		ret	nz
		inc	hl
		ret	

COSHL:
		ld	de, 5Ah
		add	hl, de

SINHL:
		ld	de, 168h

sB4A3:
		or	a
		sbc	hl, de
		jp	p, sB4A3

sB4A9:
		add	hl, de
		bit	7, h
		jr	nz, sB4A9
		ld	c, 0
		ld	de, 0B4h
		or	a
		sbc	hl, de
		jp	p, sB4BB
		inc	c
		add	hl, de

sB4BB:
		ld	de, 5Ah
		or	a
		sbc	hl, de
		add	hl, de
		jp	m, sB4CC
		ex	de, hl
		ld	hl, 0B4h
		or	a
		sbc	hl, de

sB4CC:
		add	hl, hl
		ld	de, SIN_TABLE
		add	hl, de
		ld	a, (hl)
		inc	hl
		ld	h, (hl)
		ld	l, a
		dec	c
		jp	nz, NEG_HL
		ret	

PERSPECTIV:
		ld	a, (PCT)
		or	a
		ret	z
		ld	hl, (MINX)
		ld	de, (MAXX)
		add	hl, de
		srl	h
		rr	l
		ld	(sB6CE+1), hl
		ld	hl, (MINY)
		ld	h, 0
		ld	de, (MAXY)
		ld	d, h
		add	hl, de
		srl	h
		rr	l
		ld	(sB6DF+1), hl
		ld	hl, (HEAD)
		call	COSHL
		ld	(COSH),	hl
		ld	hl, (HEAD)
		call	SINHL
		ld	(SINH),	hl
		ld	hl, (PITCH)
		call	COSHL
		ld	(COSP),	hl
		ld	hl, (PITCH)
		call	SINHL
		ld	(SINP),	hl
		ld	hl, (BANK)
		call	COSHL
		ld	(COSB),	hl
		ld	hl, (BANK)
		call	SINHL
		ld	(SINB),	hl
		ld	hl, (COSH)
		ld	de, (COSB)
		call	MUL
		push	hl
		ld	hl, (SINH)
		ld	de, (SINP)
		call	MUL
		ld	de, (SINB)
		call	MUL
		pop	de
		add	hl, de
		ld	(sB648+1), hl
		ld	hl, (COSH)
		ld	de, (SINB)
		call	MUL
		push	hl
		ld	hl, (SINH)
		ld	de, (SINP)
		call	MUL
		ld	de, (COSB)
		call	MUL
		pop	de
		or	a
		sbc	hl, de
		ld	(sB670+1), hl
		ld	hl, (SINH)
		ld	de, (COSP)
		call	MUL
		ld	(sB698+1), hl
		ld	hl, (COSP)
		ld	de, (SINB)
		call	MUL
		ld	(sB652+1),	hl
		ld	hl, (COSP)
		ld	de, (COSB)
		call	MUL
		ld	(sB67A+1), hl
		ld	hl, (SINP)
		call	NEG_HL
		ld	(sB6A2+1), hl
		ld	hl, (SINH)
		ld	de, (COSB)
		call	MUL
		push	hl
		ld	hl, (COSH)
		ld	de, (SINP)
		call	MUL
		ld	de, (SINB)
		call	MUL
		pop	de
		or	a
		sbc	hl, de
		ld	(sB65C+1), hl
		ld	hl, (SINH)
		ld	de, (SINB)
		call	MUL
		push	hl
		ld	hl, (COSH)
		ld	de, (SINP)
		call	MUL
		ld	de, (COSB)
		call	MUL
		pop	de
		add	hl, de
		ld	(sB684+1), hl
		ld	hl, (COSH)
		ld	de, (COSP)
		call	MUL
		ld	(sB6AC+1), hl
		ld	hl, (CX)
		ld	(sB666+1), hl
		ld	hl, (CY)
		ld	(sB68E+1), hl
		ld	hl, (CZ)
		ld	(sB6B6+1), hl
		ld	iy, DISP_BUF
		ld	de, OBJ_BUF
		ld	a, (PCT)

sB617:
		ld	(sB6EF+1), a
		ld	hl, (DX)
		ld	a, (de)
		inc	de
		sub	l
		ld	l, a
		ld	a, (de)
		inc	de
		sbc	a, h
		ld	h, a
		ld	(X1), hl
		ld	hl, (DY)
		ld	a, (de)
		inc	de
		sub	l
		ld	l, a
		ld	a, (de)
		inc	de
		sbc	a, h
		ld	h, a
		ld	(Y1), hl
		ld	hl, (DZ)
		ld	a, (de)
		inc	de
		sub	l
		ld	l, a
		ld	a, (de)
		inc	de
		sbc	a, h
		ld	h, a
		ld	(Z1), hl
		push	de
		ld	hl, (X1)

sB648:
		ld	de, 0
		call	MUL
		push	hl
		ld	hl, (Y1)

sB652:
		ld	de, 0
		call	MUL
		push	hl
		ld	hl, (Z1)

sB65C:
		ld	de, 0
		call	MUL
		pop	de
		add	hl, de
		pop	de
		add	hl, de

sB666:
		ld	de, 0
		add	hl, de
		ld	(X2), hl
		ld	hl, (X1)

sB670:
		ld	de, 0
		call	MUL
		push	hl
		ld	hl, (Y1)

sB67A:
		ld	de, 0
		call	MUL
		push	hl
		ld	hl, (Z1)

sB684:
		ld	de, 0
		call	MUL
		pop	de
		add	hl, de
		pop	de
		add	hl, de

sB68E:
		ld	de, 0
		add	hl, de
		ld	(Y2), hl
		ld	hl, (X1)

sB698:
		ld	de, 0
		call	MUL
		push	hl
		ld	hl, (Y1)

sB6A2:
		ld	de, 0
		call	MUL
		push	hl
		ld	hl, (Z1)

sB6AC:
		ld	de, 0
		call	MUL
		pop	de
		add	hl, de
		pop	de
		add	hl, de

sB6B6:
		ld	de, 0
		add	hl, de
		bit	7, h
		jr	z, sB6C1
		ld	hl, 0

sB6C1:
		ld	bc, 32h
		add	hl, bc
		push	hl
		ex	de, hl
		ld	hl, (X2)
		call	XDIV
		add	hl, hl

sB6CE:
		ld	de, 0
		add	hl, de
		ld	(iy+0),	l
		ld	(iy+1),	h
		pop	de
		ld	hl, (Y2)
		call	XDIV

sB6DF:
		ld	de, 0
		add	hl, de
		ld	(iy+2),	l
		ld	(iy+3),	h
		ld	de, 4
		add	iy, de
		pop	de

sB6EF:
		ld	a, 0
		dec	a
		jp	nz, sB617
		ret	


CLIPLINE:
		ld	hl, (X1)
		ld	de, (X2)
		ld	bc, 0
		ld	a, e
		sub	l
		ld	a, d
		sbc	a, h
		jp	p, sB709
		ex	de, hl
		inc	b

sB709:
		inc	de
		ld	a, (MINX)
		sub	e
		ld	a, (MINX+1)
		sbc	a, d
		ret	p
		dec	de
		ld	a, (MAXX)
		sub	l
		ld	a, (MAXX+1)
		sbc	a, h
		ret	m
		ld	(X1), hl
		ld	(X2), de
		ld	hl, (Y1)
		ld	de, (Y2)
		ld	a, e
		sub	l
		ld	a, d
		sbc	a, h
		jp	p, sB734
		ex	de, hl
		inc	c
sB734:
		inc	de
		ld	a, (MINY)
		sub	e
		ld	a, 0
		sbc	a, d
		ret	p
		dec	de
		ld	a, (MAXY)
		sub	l
		ld	a, 0
		sbc	a, h
		ret	m
		ld	a, b
		xor	c
		jr	z, sB74B
		ex	de, hl
sB74B:
		ld	(Y1), hl
		ld	(Y2), de
		ld	hl, (X1)
		ld	bc, (MINX)
		or	a
		sbc	hl, bc
		jp	p, sB76A
		call	sB831
		ld	(X1), bc
		ld	(Y1), de

sB76A:
		ld	hl, (X2)
		ld	bc, (MAXX)
		inc	bc
		or	a
		sbc	hl, bc
		jp	m, sB784
		dec	bc
		call	sB831
		ld	(X2), bc
		ld	(Y2), de

sB784:
		ld	hl, (Y1)
		ld	de, (Y2)
		ld	a, e
		sub	l
		ld	a, d
		sbc	a, h
		jp	p, sB7AE
		ld	(Y2), hl
		ld	(Y1), de
		ld	hl, (X1)
		ld	de, (X2)
		ld	(X2), hl
		ld	(X1), de
		ld	hl, (Y1)
		ld	de, (Y2)
sB7AE:
		ld	bc, (MAXY)
		inc	bc
		or	a
		sbc	hl, bc
		ret	p
		ex	de, hl
		ld	bc, (MINY)
		or	a
		sbc	hl, bc
		ret	m
		ld	hl, (X1)
		ld	de, (Y1)
		ld	(Y1), hl
		ld	(X1), de
		ld	hl, (X2)
		ld	de, (Y2)
		ld	(Y2), hl
		ld	(X2), de
		ld	hl, (X1)
		ld	a, (MINY)
		ld	c, a
		ld	b, 0
		or	a
		sbc	hl, bc
		jp	p, sB7F6
		call	sB831
		ld	(X1), bc
		ld	(Y1), de
sB7F6:
		ld	hl, (X2)
		ld	a, (MAXY)
		ld	c, a
		ld	b, 0
		inc	c
		or	a
		sbc	hl, bc
		jp	m, sB812
		dec	c
		call	sB831
		ld	(X2), bc
		ld	(Y2), de
sB812:
		ld	hl, (X1)
		ld	de, (Y1)
		ld	(Y1), hl
		ld	(X1), de
		ld	hl, (X2)
		ld	de, (Y2)
		ld	(Y2), hl
		ld	(X2), de
		jp	LINE

sB831:
		ld	hl, (X1)
		ld	(XA1), hl
		ld	hl, (X2)
		ld	(XA2), hl
		ld	hl, (Y1)
		ld	(YA1), hl
		ld	hl, (Y2)
		ld	(YA2), hl
sB849:
		ld	hl, (YA1)
		ld	de, (YA2)
		add	hl, de
		sra	h
		rr	l
		push	hl
		ld	hl, (XA1)
		ld	de, (XA2)
		add	hl, de
		sra	h
		rr	l
		pop	de
		push	hl
		or	a
		sbc	hl, bc
		ld	(sB87E+1), hl
		pop	hl
		jp	p, sB877
		ld	(XA1), hl
		ld	(YA1), de
		jr	sB87E
sB877:
		ld	(XA2), hl
		ld	(YA2), de
sB87E:
		ld	hl, 0
		bit	7, h
		jr	z, sB88C
		ld	a, h
		cpl	
		ld	h, a
		ld	a, l
		cpl	
		ld	l, a
		inc	hl

sB88C:
		inc	h
		dec	h
		jp	nz, sB849
		ld	a, l
		cp	2
		jp	nc, sB849
		ret	

PSET:
		ld	hl, (X1)
		ld	(X2), hl
		ld	hl, (Y1)
		ld	(Y2), hl

LINE:
		ld	a, (XMODE)
		and	3
		add	a, a
		add	a, a
		add	a, a
		ld	bc, 382Fh
		jr	z, sB8B4
		ld	bc, 3000h
sB8B4:
		add	a, 0A5h
		ld	(sB966), a
		ld	(sB99D), a
		ld	a, c
		ld	(sB949), a
		ld	a, b
		ld	(sB96B), a
		ld	(sB9C0), a
		ld	a, (Y1)
		ld	b, a
		ld	a, (Y2)
		sub	b
		ld	d, a
		jp	nc, sB8EE
		neg	
		ld	d, a
		ld	hl, (X1)
		push	hl
		ld	hl, (X2)
		ld	(X1), hl
		pop	hl
		ld	(X2), hl
		ld	a, (Y2)
		ld	(Y1), a
		ld	a, b
		ld	(Y2), a
sB8EE:
		ld	hl, (X2)
		ld	bc, (X1)
		or	a
		sbc	hl, bc
		ld	bc, 30Dh
		jp	nc, sB908
		ld	a, h
		cpl	
		ld	h, a
		ld	a, l
		cpl	
		ld	l, a
		inc	hl
		ld	bc, 0B05h
sB908:
		ld	a, c
		ld	(sB96B-1),	a
		ld	(sB9C0-1),	a
		ld	a, b
		ld	(sB96D), a
		ld	(sB9C2), a
		push	hl
		ld	a, (Y1)
		ld	e, a
		and	0F8h
		ld	l, a
		ld	h, 0
		add	hl, hl
		ld	b, h
		ld	c, l
		ld	a, e
		and	7
		add	a, a
		add	a, h
		ld	h, a
		add	hl, hl
		add	hl, hl
		add	hl, bc
		ld	bc, (X1)
		ld	a, c
		and	0F8h
		or	b
		rrca	
		rrca	
		rrca	
		add	a, l
		ld	c, a
		ld	a, (PLANE)
		adc	a, h
		ld	b, a
		ld	a, (X1)
		and	7
		ld	hl, DOT_TABLE
		add	a, l
		ld	l, a
		ld	a, (hl)
sB949:
		nop	
		ld	l, a
		ld	a, d
		exx	
		pop	de
		inc	d
		dec	d
		jr	nz, sB956
		cp	e
		jp	nc, sB992
sB956:
		ld	c, a
		ld	b, 0
		ld	h, d
		ld	l, e
		srl	h
		rr	l
		push	de
		exx	
		pop	de
		inc	d
		inc	e
sB964:
		in	a, (c)
sB966:
		or	l
		out	(c), a
		rlc	b
sB96B:
		jr	nc, sB96E
sB96D:
		inc	bc
sB96E:
		exx	
		or	a
		sbc	hl, bc
		exx	
		jp	nc, sB989
		exx	
		add	hl, de
		exx	
		ld	a, 8
		add	a, b
		ld	b, a
		and	38h
		jr	nz, sB989
		ld	a, 50h
		add	a, c
		ld	c, a
		ld	a, 0C0h
		adc	a, b
		ld	b, a
sB989:
		dec	e
		jp	nz, sB964
		dec	d
		jp	nz, sB964
		ret	

sB992:
		ld	c, a
		ld	b, a
		srl	a
		ld	l, a
		ld	h, 0
		inc	b
sB99A:
		exx	
		in	a, (c)
sB99D:
		or	l
		out	(c), a
		ld	a, 8
		add	a, b
		ld	b, a
		and	38h
		jr	nz, sB9B0
		ld	a, 50h
		add	a, c
		ld	c, a
		ld	a, 0C0h
		adc	a, b
		ld	b, a
sB9B0:
		exx	
		or	a
		sbc	hl, de
		jp	nc, sB9C4
		ld	a, c
		add	a, l
		ld	l, a
		jr	nc, sB9BD
		inc	h

sB9BD:
		exx	
		rlc	b
sB9C0:
		jr	nc, sB9C3
sB9C2:
		inc	bc
sB9C3:
		exx	
sB9C4:
		djnz	sB99A
		ret	

SIN_TABLE:	dw 0
		dw 11Dh
		dw 23Bh
		dw 359h
		dw 476h
		dw 593h
		dw 6B0h
		dw 7CCh
		dw 8E8h
		dw 0A03h
		dw 0B1Dh
		dw 0C36h
		dw 0D4Eh
		dw 0E65h
		dw 0F7Bh
		dw 1090h
		dw 11A4h
		dw 12B6h
		dw 13C6h
		dw 14D6h
		dw 15E3h
		dw 16EFh
		dw 17F9h
		dw 1901h
		dw 1A07h
		dw 1B0Ch
		dw 1C0Eh
		dw 1D0Eh
		dw 1E0Bh
		dw 1F07h
		dw 2000h
		dw 20F6h
		dw 21EAh
		dw 22DBh
		dw 23C9h
		dw 24B5h
		dw 259Eh
		dw 2684h
		dw 2766h
		dw 2846h
		dw 2923h
		dw 29FCh
		dw 2AD3h
		dw 2BA5h
		dw 2C75h
		dw 2D41h
		dw 2E09h
		dw 2ECEh
		dw 2F8Fh
		dw 304Dh
		dw 3106h
		dw 31BCh
		dw 326Eh
		dw 331Ch
		dw 33C6h
		dw 346Ch
		dw 350Eh
		dw 35ACh
		dw 3646h
		dw 36DBh
		dw 376Ch
		dw 37F9h
		dw 3882h
		dw 3906h
		dw 3985h
		dw 3A00h
		dw 3A77h
		dw 3AE9h
		dw 3B56h
		dw 3BBFh
		dw 3C23h
		dw 3C83h
		dw 3CDEh
		dw 3D34h
		dw 3D85h
		dw 3DD1h
		dw 3E19h
		dw 3E5Ch
		dw 3E99h
		dw 3ED2h
		dw 3F07h
		dw 3F36h
		dw 3F60h
		dw 3F85h
		dw 3FA6h
		dw 3FC1h
		dw 3FD8h
		dw 3FE9h
		dw 3FF6h
		dw 3FFDh
		dw 4000h

EX_RASTER:
		ld	a, l
		sub	e
		ld	a, h
		sbc	a, d
		jp	m, RASTER
		ex	de, hl

RASTER:
		inc	b
		dec	b
		ret	nz
		ld	a, (MAXY)
		cp	c
		ret	c
		inc	c
		ret	z
		ld	a, (MINY)
		cp	c
		ret	nc
		dec	c
		ld	a, c
		ex	af, af'
		ld	bc, (MINX)
		dec	bc
		ld	a, c
		sub	e
		ld	a, b
		sbc	a, d
		ret	p
		inc	bc
		ld	a, c
		sub	l
		ld	a, b
		sbc	a, h
		jp	m, sBAAB
		ld	h, b
		ld	l, c
sBAAB:
		ld	bc, (MAXX)
		ld	a, c
		sub	l
		ld	a, b
		sbc	a, h
		ret	m
		ld	a, c
		sub	e
		ld	a, b
		sbc	a, d
		jp	p, sBABD
		ld	d, b
		ld	e, c
sBABD:
		ld	bc, 879h
		inc	de
		ld	bc, 0F807h
		ld	a, l
		and	c
		ld	(sBB09+1), a
		ld	a, e
		and	c
		ld	(sBC26+1), a
		ld	a, l
		and	b
		or	h
		rrca	
		rrca	
		rrca	
		ld	h, a
		ld	a, e
		and	b
		or	d
		rrca	
		rrca	
		rrca	
		sub	h
		ld	d, a
		ld	a, h
		ex	af, af'
		ld	e, a
		and	b
		ld	l, a
		ld	h, 0
		add	hl, hl
		ld	b, h
		ld	c, l
		ld	a, e
		and	7
		add	a, a
		add	a, h
		ld	h, a
		add	hl, hl
		add	hl, hl
		add	hl, bc
		ex	af, af'
		add	a, l
		ld	c, a
		ld	a, (PLANE)
		adc	a, h
		ld	b, a
		ld	hl, MASKS
		ld	a, e
		and	3
		add	a, l
		ld	l, a
		ld	a, (hl)
		ld	(MASK),	a
		inc	d
		dec	d
		jp	z, sBC3D
sBB09:
		ld	a, 0
		ld	hl, LINE_SIDE
		add	a, l
		ld	l, a
		ld	a, (hl)
		ld	l, a
		cpl	
		ld	h, a
		in	a, (c)
		and	l
		ld	l, a
		ld	a, (MASK)
		and	h
		or	l
		out	(c), a
		inc	bc
		dec	d
		jp	z, sBC26
		ld	a, 50h
		sub	d
		ld	h, a
		add	a, a
		add	a, h
		ld	hl, sBB36
		add	a, l
		ld	l, a
		jr	nc, sBB32
		inc	h
sBB32:
		ld	a, (MASK)
		jp	(hl)
sBB36:
		out	(c), a
		inc	bc
		out	(c), a
		inc	bc
		out	(c), a
		inc	bc
		out	(c), a
		inc	bc
		out	(c), a
		inc	bc
		out	(c), a
		inc	bc
		out	(c), a
		inc	bc
		out	(c), a
		inc	bc
		out	(c), a
		inc	bc
		out	(c), a
		inc	bc
		out	(c), a
		inc	bc
		out	(c), a
		inc	bc
		out	(c), a
		inc	bc
		out	(c), a
		inc	bc
		out	(c), a
		inc	bc
		out	(c), a
		inc	bc
		out	(c), a
		inc	bc
		out	(c), a
		inc	bc
		out	(c), a
		inc	bc
		out	(c), a
		inc	bc
		out	(c), a
		inc	bc
		out	(c), a
		inc	bc
		out	(c), a
		inc	bc
		out	(c), a
		inc	bc
		out	(c), a
		inc	bc
		out	(c), a
		inc	bc
		out	(c), a
		inc	bc
		out	(c), a
		inc	bc
		out	(c), a
		inc	bc
		out	(c), a
		inc	bc
		out	(c), a
		inc	bc
		out	(c), a
		inc	bc
		out	(c), a
		inc	bc
		out	(c), a
		inc	bc
		out	(c), a
		inc	bc
		out	(c), a
		inc	bc
		out	(c), a
		inc	bc
		out	(c), a
		inc	bc
		out	(c), a
		inc	bc
		out	(c), a
		inc	bc
		out	(c), a
		inc	bc
		out	(c), a
		inc	bc
		out	(c), a
		inc	bc
		out	(c), a
		inc	bc
		out	(c), a
		inc	bc
		out	(c), a
		inc	bc
		out	(c), a
		inc	bc
		out	(c), a
		inc	bc
		out	(c), a
		inc	bc
		out	(c), a
		inc	bc
		out	(c), a
		inc	bc
		out	(c), a
		inc	bc
		out	(c), a
		inc	bc
		out	(c), a
		inc	bc
		out	(c), a
		inc	bc
		out	(c), a
		inc	bc
		out	(c), a
		inc	bc
		out	(c), a
		inc	bc
		out	(c), a
		inc	bc
		out	(c), a
		inc	bc
		out	(c), a
		inc	bc
		out	(c), a
		inc	bc
		out	(c), a
		inc	bc
		out	(c), a
		inc	bc
		out	(c), a
		inc	bc
		out	(c), a
		inc	bc
		out	(c), a
		inc	bc
		out	(c), a
		inc	bc
		out	(c), a
		inc	bc
		out	(c), a
		inc	bc
		out	(c), a
		inc	bc
		out	(c), a
		inc	bc
		out	(c), a
		inc	bc
		out	(c), a
		inc	bc
		out	(c), a
		inc	bc
		out	(c), a
		inc	bc
		out	(c), a
		inc	bc
		out	(c), a
		inc	bc
		out	(c), a
		inc	bc
		out	(c), a
		inc	bc
sBC26:
		ld	a, 0
		ld	hl, LINE_SIDE
		add	a, l
		ld	l, a
		ld	a, (hl)
		ld	h, a
		cpl	
		ld	l, a
sBC31:
		in	a, (c)
		and	l
		ld	l, a
		ld	a, (MASK)
		and	h
		or	l
		out	(c), a
		ret	

sBC3D:
		ld	a, (sBB09+1)
		ld	hl, LINE_SIDE
		ld	d, l
		add	a, l
		ld	l, a
		ld	a, (sBC26+1)
		ld	e, (hl)
		add	a, d
		ld	l, a
		ld	a, e
		cpl	
		and	(hl)
		ld	h, a
		cpl	
		ld	l, a
		jr	sBC31

POINT:
		inc	b
		dec	b
		jr	nz, sBCB3
		ld	a, 0C7h
		cp	c
		jr	c, sBCB3
		ld	a, 7Fh
		sub	e
		ld	a, 2
		sbc	a, d
		jr	c, sBCB3
		push	bc
		push	de
		push	hl
		ld	a, c
		and	0F8h
		ld	l, a
		ld	h, 0
		add	hl, hl
		ld	a, c
		ld	b, h
		ld	c, l
		and	7
		add	a, a
		add	a, h
		ld	h, a
		add	hl, hl
		add	hl, hl
		add	hl, bc
		ld	a, e
		and	0F8h
		or	d
		rrca	
		rrca	
		rrca	
		add	a, l
		ld	c, a
		ld	a, 40h
		adc	a, h
		ld	b, a
		ld	a, e
		and	7
		ld	hl, DOT_TABLE
		add	a, l
		ld	l, a
		ld	a, (hl)
		ld	l, a
		ld	d, 0
		in	a, (c)
		and	l
		jr	z, sBC9A
		ld	d, 1
sBC9A:
		set	7, b
		res	6, b
		in	a, (c)
		and	l
		jr	z, sBCA5
		set	1, d
sBCA5:
		set	6, b
		in	a, (c)
		and	l
		jr	z, sBCAE
		set	2, d
sBCAE:
		ld	a, d
		pop	hl
		pop	de
		pop	bc
		ret	
sBCB3:
		ld	a, 0FFh
		ret	
TRIANGLE:
		ld	a, (X1+1)
		ld	b, a
		ld	a, (X2+1)
		ld	c, a
		ld	a, (X3+1)
		and	b
		and	c
		and	0FCh
		ret	nz
		ld	a, (Y1+1)
		ld	hl, (Y3)
		and	h
		ld	de, (Y2)
		and	d
		add	a, a
		ret	c
		sbc	hl, de
		jp	p, sBCF1
		ld	hl, (Y3)
		ld	(Y3), de
		ld	(Y2), hl
		ld	hl, (X2)
		ld	de, (X3)
		ld	(X2), de
		ld	(X3), hl
sBCF1:
		ld	hl, (Y3)
		ld	de, (Y1)
		or	a
		sbc	hl, de
		jp	p, sBD26
		ld	hl, (Y2)
		ld	bc, (Y3)
		ld	(Y3), de
		ld	(Y1), hl
		ld	(Y2), bc
		ld	hl, (X1)
		ld	de, (X2)
		ld	bc, (X3)
		ld	(X3), hl
		ld	(X1), de
		ld	(X2), bc
sBD26:
		ld	hl, (Y2)
		ld	de, (Y1)
		or	a
		sbc	hl, de
		jp	p, sBD4B
		ld	hl, (Y2)
		ld	(Y2), de
		ld	(Y1), hl
		ld	hl, (X1)
		ld	de, (X2)
		ld	(X1), de
		ld	(X2), hl
sBD4B:
		ld	hl, (X1)
		ld	de, (X2)
		ld	(SX), hl
		ld	(_EX_), hl
		ex	de, hl
		ld	a, 3
		or	a
		sbc	hl, de
		jr	z, sBD6E-1
		jp	p, sBD6E
		ld	a, h
		cpl	
		ld	h, a
		ld	a, l
		cpl	
		ld	l, a
		inc	hl
		ld	a, 0Bh
		cp	0AFh
sBD6E:
		ld	(sBE54), a
		ld	(sBE43+1), hl
		ld	hl, (X3)
		ld	a, 3
		or	a
		sbc	hl, de
		jr	z, sBD8C-1
		jp	p, sBD8C
		ld	a, h
		cpl	
		ld	h, a
		ld	a, l
		cpl	
		ld	l, a
		inc	hl
		ld	a, 0Bh
		cp	0AFh
sBD8C:
		ld	(sBE73), a
		ld	(sBE62+1), hl
		ld	hl, (Y1)
		ld	(PY), hl
		ex	de, hl
		ld	hl, (Y3)
		or	a
		sbc	hl, de
		ld	(sBE6B+1), hl
		ld	hl, (Y3)
		ld	de, (Y2)
		or	a
		sbc	hl, de
		ld	a, h
		or	l
		push	hl
		ld	hl, (Y1)
		ex	de, hl
		sbc	hl, de
		push	hl
		exx	
		pop	de
		pop	hl
		exx	
		ld	(sBE4C+1),	hl
		jr	nz, sBDE4
		ld	hl, (X2)
		ld	(SX), hl
		or	a
		jr	nz, sBDFD
		ld	hl, (X1)
		ld	de, (X2)
		ld	bc, (Y1)
		call	EX_RASTER
		ld	hl, (X2)
		ld	de, (X3)
		ld	bc, (Y1)
		jp	EX_RASTER

sBDE4:
		sra	h
		rr	l
		ld	(R1), hl
		ld	hl, (sBE6B+1)
		sra	h
		rr	l
		ld	(R2), hl
		call	sBE2D
		exx	
		ld	a, h
		or	l
		exx	
		ret	z

sBDFD:
		exx	
		ld	(sBE4C+1),	hl
		ld	d, h
		ld	e, l
		sra	h
		rr	l
		ld	(R1), hl
		exx	
		ld	hl, (X3)
		ld	de, (X2)
		ld	a, 3
		or	a
		sbc	hl, de
		jr	z, sBE27-1
		jp	p, sBE27
		ld	a, h
		cpl	
		ld	h, a
		ld	a, l
		cpl	
		ld	l, a
		inc	hl
		ld	a, 0Bh
		cp	0AFh
sBE27:
		ld	(sBE54), a
		ld	(sBE43+1), hl
sBE2D:
		ld	hl, (PY)
		ld	b, h
		ld	c, l
		inc	hl
		ld	(PY), hl
		ld	hl, (SX)
		ld	de, (_EX_)
		call	EX_RASTER
		ld	hl, (R1)
sBE43:
		ld	de, 0
		or	a
		sbc	hl, de
		jp	p, sBE5C
sBE4C:
		ld	de, 0
		ld	bc, (SX)
sBE53:
		add	hl, de
sBE54:
		nop	
		jp	nc, sBE53
		ld	(SX), bc
sBE5C:
		ld	(R1), hl
		ld	hl, (R2)
sBE62:
		ld	de, 0
		or	a
		sbc	hl, de
		jp	p, sBE7B
sBE6B:
		ld	de, 0
		ld	bc, (_EX_)
sBE72:
		add	hl, de
sBE73:
		nop	
		jp	nc, sBE72
		ld	(_EX_), bc
sBE7B:
		ld	(R2), hl
		exx	
		dec	de
		ld	a, d
		or	e
		exx	
		jp	nz, sBE2D
		ret	

BOX_FULL:
		ld	hl, (X1)
		ld	de, (X2)
		push	hl
		or	a
		sbc	hl, de
		pop	hl
		jp	m, sBE97
		ex	de, hl
sBE97:
		bit	7, d
		ret	nz
		bit	7, h
		jr	z, sBEA1
		ld	hl, 0
sBEA1:
		ld	a, 7Fh
		sub	l
		ld	a, 2
		sbc	a, h
		ret	c
		ld	a, 7Fh
		sub	e
		ld	a, 2
		sbc	a, d
		jr	nc, sBEB3
		ld	de, 27Fh
sBEB3:
		ld	(sBEF0+1), hl
		ld	(sBEF0+4),	de
		ld	hl, (Y2)
		ld	de, (Y1)
		ld	a, h
		bit	7, h
		jr	z, sBEC9
		ld	hl, 0
sBEC9:
		and	d
		bit	7, d
		jr	z, sBED1
		ld	de, 0
sBED1:
		rlca	
		ret	c
		or	a
		sbc	hl, de
		jr	nc, sBEDD
		add	hl, de
		ex	de, hl
		or	a
		sbc	hl, de
sBEDD:
		ld	a, 0C7h
		sub	e
		ret	c
		inc	h
		dec	h
		jr	nz, sBEE8
		cp	l
		jr	nc, sBEE9
sBEE8:
		ld	l, a
sBEE9:
		ld	b, l
		inc	b
sBEEB:
		ld	a, e
		exx	
		ld	c, a
		ld	b, 0
sBEF0:
		ld	hl, 0
		ld	de, 0
		call	RASTER
		exx	
		inc	e
		djnz	sBEEB
		ret	

CIRCLE_FULL:
		xor	a
		exx	
		ld	e, a
		ld	d, a
		ld	(FLG), a
		ld	hl, (X2)
		ld	(_R_), hl
		ld	b, h
		ld	c, l
		ld	a, b
		or	c
		exx	
		jp	z, DOT
sBF13:
		exx	
		ld	h, d
		ld	l, e
		dec	hl
		or	a
		sbc	hl, bc
		exx	
		ret	p
		jr	z, sBF43
		call	sBFA5
		ld	a, (FLG)
		or	a
		jr	nz, sBF43
		ld	hl, (SX)
		ld	de, (_EX_)
		ld	bc, (YA)
		call	RASTER
		ld	hl, (SX)
		ld	de, (_EX_)
		ld	bc, (YB)
		call	RASTER
sBF43:
		call	sBFA5
		exx	
		ld	h, d
		ld	l, e
		dec	hl
		or	a
		sbc	hl, bc
		exx	
		jr	z, sBF76
		ld	hl, (SX)
		ld	de, (_EX_)
		ld	bc, (YA)
		call	RASTER
		ld	hl, (YA)
		ld	de, (YB)
		or	a
		sbc	hl, de
		jr	z, sBF76
		ld	b, d
		ld	c, e
		ld	hl, (SX)
		ld	de, (_EX_)
		call	RASTER
sBF76:
		ld	a, 1
		ld	(FLG), a
		exx	
		inc	de
		push	de
		exx	
		pop	hl
		add	hl, hl
		dec	hl
		ld	b, h
		ld	c, l
		ld	hl, (_R_)
		or	a
		sbc	hl, bc
		ld	(_R_), hl
		jp	nc, sBF13
		xor	a
		ld	(FLG), a
		exx	
		dec	bc
		push	bc
		exx	
		pop	hl
		add	hl, hl
		ld	bc, (_R_)
		add	hl, bc
		ld	(_R_), hl
		jp	sBF13

sBFA5:
		exx	
		ld	hl, (X1)
		or	a
		sbc	hl, de
		ld	(SX), hl
		ld	hl, (X1)
		add	hl, de
		ld	(_EX_), hl
		push	bc
		sra	b
		rr	c
		ld	hl, (Y1)
		or	a
		sbc	hl, bc
		ld	(YA), hl
		ld	hl, (Y1)
		add	hl, bc
		ld	(YB), hl
		ld	b, d
		ld	c, e
		pop	de
		exx	
		ret	

DOT:
		ld	hl, (X1)
		ld	de, (X1)
		ld	bc, (Y1)
		jp	RASTER

DISP_FLAME:
		ld	a, (PCT)
		or	a
		ret	z
		ld	a, (LCT)
		or	a
		ret	z
		ld	hl, LINE_BUF

sBFEB:
		ld	(sC01F+1), a
		ld	c, (hl)
		inc	hl
		ld	a, (hl)
		inc	hl
		push	hl
		ld	l, c
		ld	h, 0
		add	hl, hl
		add	hl, hl
		ld	de, DISP_BUF
		add	hl, de
		ld	de, X1
		ldi	
		ldi	
		ldi	
		ldi	
		ld	l, a
		ld	h, 0
		add	hl, hl
		add	hl, hl
		ld	de, DISP_BUF
		add	hl, de
		ld	de, X2
		ldi	
		ldi	
		ldi	
		ldi	
		call	CLIPLINE
		pop	hl
sC01F:
		ld	a, 0
		dec	a
		jp	nz, sBFEB
		ret	

SPLINE:
		ld	hl, (X1)
		ld	de, (X3)
		add	hl, de
		sra	h
		rr	l
		ex	de, hl
		ld	hl, (X2)
		add	hl, hl
		or	a
		sbc	hl, de
		ld	(X2), hl
		ld	hl, (Y1)
		ld	de, (Y3)
		add	hl, de
		sra	h
		rr	l
		ex	de, hl
		ld	hl, (Y2)
		add	hl, hl
		or	a
		sbc	hl, de
		ld	(Y2), hl
		xor	a
		ld	hl, (X1)
		ld	(XA1+1), hl
		ld	(XA1), a
		ld	hl, (Y1)
		ld	(YA1+1), hl
		ld	(YA1), a
		ld	hl, (X2)
		ld	de, (X1)
		xor	a
		sbc	hl, de
		sra	h
		rr	l
		rra	
		sra	h
		rr	l
		rra	
		sra	h
		rr	l
		rra	
		ld	(XA), a
		ld	(XA+1), hl
		ld	hl, (Y2)
		ld	de, (Y1)
		xor	a
		sbc	hl, de
		sra	h
		rr	l
		rra	
		sra	h
		rr	l
		rra	
		sra	h
		rr	l
		rra	
		ld	(YA), a
		ld	(YA+1), hl
		ld	hl, (X2)
		add	hl, hl
		ex	de, hl
		ld	hl, (X1)
		or	a
		sbc	hl, de
		ld	de, (X3)
		add	hl, de
		ld	a, h
		add	a, a
		sbc	a, a
		ld	(XB+2), a
		ld	(XB), hl
		add	hl, hl
		adc	a, a
		ld	(XC+2), a
		ld	(XC), hl
		ld	hl, (Y2)
		add	hl, hl
		ex	de, hl
		ld	hl, (Y1)
		or	a
		sbc	hl, de
		ld	de, (Y3)
		add	hl, de
		ld	a, h
		add	a, a
		sbc	a, a
		ld	(YB+2), a
		ld	(YB), hl
		add	hl, hl
		adc	a, a
		ld	(YC+2), a
		ld	(YC), hl
		xor	a
		ld	h, a
		ld	l, a
		ld	(XA2), hl
		ld	(XA2+2), a
		ld	(YA2), hl
		ld	(YA2+2), a
		ld	(XA3), hl
		ld	(XA3+2), a
		ld	(YA3), hl
		ld	(YA3+2), a
		ld	c, 11h
		ld	iy, WORK
sC108:
		push	bc
		ld	hl, (XA1)
		ld	a, (XA1+2)
		ld	b, a
		ld	de, (XA2)
		ld	a, (XA2+2)
		add	hl, de
		adc	a, b
		ld	b, a
		ld	de, (XA3)
		ld	a, (XA3+2)
		add	hl, de
		adc	a, b
		ld	(iy+0),	h
		ld	(iy+1),	a
		ld	hl, (YA1)
		ld	a, (YA1+2)
		ld	b, a
		ld	de, (YA2)
		ld	a, (YA2+2)
		add	hl, de
		adc	a, b
		ld	b, a
		ld	de, (YA3)
		ld	a, (YA3+2)
		add	hl, de
		adc	a, b
		ld	(iy+2),	h
		ld	(iy+3),	a
		ld	hl, (XA2)
		ld	a, (XA2+2)
		ld	b, a
		ld	de, (XA)
		ld	a, (XA+2)
		add	hl, de
		adc	a, b
		ld	(XA2), hl
		ld	(XA2+2), a
		ld	hl, (YA2)
		ld	a, (YA2+2)
		ld	b, a
		ld	de, (YA)
		ld	a, (YA+2)
		add	hl, de
		adc	a, b
		ld	(YA2), hl
		ld	(YA2+2), a
		ld	hl, (XA3)
		ld	a, (XA3+2)
		ld	b, a
		ld	de, (XB)
		ld	a, (XB+2)
		ld	c, a
		add	hl, de
		adc	a, b
		ld	(XA3), hl
		ld	(XA3+2), a
		ld	hl, (XC)
		ex	de, hl
		ld	a, (XC+2)
		add	hl, de
		adc	a, c
		ld	(XB), hl
		ld	(XB+2), a
		ld	hl, (YA3)
		ld	a, (YA3+2)
		ld	b, a
		ld	de, (YB)
		ld	a, (YB+2)
		ld	c, a
		add	hl, de
		adc	a, b
		ld	(YA3), hl
		ld	(YA3+2), a
		ld	hl, (YC)
		ex	de, hl
		ld	a, (YC+2)
		add	hl, de
		adc	a, c
		ld	(YB), hl
		ld	(YB+2), a
		ld	bc, 4
		add	iy, bc
		pop	bc
		dec	c
		jp	nz, sC108
		ld	hl, WORK
		ld	b, 10h
sC1D0:
		push	bc
		ld	de, X1
		ldi	
		ldi	
		ldi	
		ldi	
		push	hl
		ldi	
		ldi	
		ldi	
		ldi	
		call	CLIPLINE
		pop	hl
		pop	bc
		djnz	sC1D0
		ret	

		; org	(START+01200h)

POINTER:	dw 0
PDATA:		db 0
CX:		dw 0
CY:		dw 0
CZ:		dw 0
DX:		dw 0
DY:		dw 0
DZ:		dw 0
HEAD:		dw 0
PITCH:		dw 0
BANK:		dw 0
MINX:		dw 0
MINY:		dw 0
MAXX:		dw 0
MAXY:		dw 0
COL:		db    0
PALET:		ds    8
PICS:		dw 0
X1:		dw 0
Y1:		dw 0
X2:		dw 0
Y2:		dw 0
X3:		dw 0
Y3:		dw 0
Z1:		dw 0
Z2:		dw 0
Z3:		dw 0
COSH:		dw 0
SINH:		dw 0
COSP:		dw 0
SINP:		dw 0
COSB:		dw 0
SINB:		dw 0
_R_:		dw 0
SX:		dw 0
_EX_:		dw 0
_EY_:		dw 0
WK1:		dw 0
WK2:		dw 0
WK3:		dw 0
WK4:		dw 0
PLANE:		db 0
XMODE:		db 0
FLG:		db 0
MASK:		db 0
MASKS:		ds 4
_py_:		dw 0
XA1:		ds 3
YA1:		ds 3
XA2:		ds 3
YA2:		ds 3
XA3:		ds 3
YA3:		ds 3
XA:		ds 3
YA:		ds 3
XB:		ds 3
YB:		ds 3
XC:		ds 3
YC:		ds 3
PY:		dw 0
R1:		dw 0
R2:		dw 0
STACK:		dw 0
WORK:		ds 40

DISP_BUF:	ds 1024
PCT:		ds 1
LCT:		ds 1
OBJ_BUF:	ds 1536
LINE_BUF:	ds 512

MAGIC_END	EQU	(LINE_BUF+512)

#ENDLIB
