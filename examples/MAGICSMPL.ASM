; CONST _PLOTSW
; CONST _THREE
; CONST _FLOAT
; CONST _SINCOS
; CONST _TILESW
; CONST _GHIN
; CONST _KPRINTF
; CONST _COLOR
; CONST _HEAD
; CONST _PITCH
; CONST _BANK
; ARRAY _CO[SLANGCompiler.Parser.Ast.IntegerLiteral]
; ARRAY _ZAHYO[SLANGCompiler.Parser.Ast.IntegerLiteral,SLANGCompiler.Parser.Ast.IntegerLiteral]
; ARRAY _WIRE[SLANGCompiler.Parser.Ast.IntegerLiteral,SLANGCompiler.Parser.Ast.IntegerLiteral]
; ARRAY _LPCT[SLANGCompiler.Parser.Ast.IntegerLiteral]
; ARRAY _PAL[SLANGCompiler.Parser.Ast.IntegerLiteral]
; ARRAY _DISP[SLANGCompiler.Parser.Ast.IntegerLiteral,SLANGCompiler.Parser.Ast.IntegerLiteral]
; ARRAY _PAR[SLANGCompiler.Parser.Ast.IntegerLiteral]
; ARRAY _BUFPAR[SLANGCompiler.Parser.Ast.IntegerLiteral]
; ARRAY _ROADY[SLANGCompiler.Parser.Ast.IntegerLiteral,SLANGCompiler.Parser.Ast.IntegerLiteral]
;   = {initial code}
; ARRAY _GRDTN[SLANGCompiler.Parser.Ast.IntegerLiteral]
;   = {initial code}
; CONST FLDIV
; ARRAY _STABLE[SLANGCompiler.Parser.Ast.IntegerLiteral]
;   = {initial code}
; ARRAY _ATNTBL[SLANGCompiler.Parser.Ast.IntegerLiteral]
;   = {initial code}
; global: LoadConst t0 #65535
; global init: _TILE1 = t0
; global: LoadConst t0 #65535
; global init: _TILE2 = t0
; global: LoadConst t0 #0
; global init: _MSCREEN = t0
; global: LoadConst t0 #0
; global init: _SPITCH = t0
; global: LoadConst t0 #0
; global init: _SHEAD = t0
; global: LoadConst t0 #0
; global init: _SBANK = t0
; global: LoadConst t0 #0
; global init: _OFSX = t0
; global: LoadConst t0 #0
; global init: _OFSY = t0
; global: LoadConst t0 #0
; global init: _OFSZ = t0
; global: LoadConst t0 #2
; global init: _DEMODE = t0
; global: LoadConst t0 #15
; global init: _KSTEP = t0
; global: LoadConst t0 #7
; global init: _SBLUE = t0
; global: LoadConst t0 #4
; global init: _SRED = t0
; global: LoadConst t0 #4
; global init: _SGREEN = t0
; global: LoadConst t0 #30
; global init: _LX = t0
; global: LoadConst t0 #10
; global init: _LY = t0
; global: LoadConst t0 #10
; global init: _LZ = t0
; global: LoadConst t0 #7
; global init: _GMASK = t0

@LINE:
; function @LINE
	PUSH	IY
	LD	HL,$0000
	LD	HL,(_CO)
	LD	HL,$0000
; array store: t1[t2] = t0
	PUSH	HL
	POP	DE
	LD	(HL),E
	INC	HL
	LD	(HL),D
	LD	HL,$0002
	LD	HL,(_CO)
	LD	HL,$0001
; array store: t4[t5] = t3
	PUSH	HL
	POP	DE
	LD	(HL),E
	INC	HL
	LD	(HL),D
	LD	HL,(X1)
	LD	HL,(MEMW)
	LD	HL,_CO
	LD	HL,$0002
	PUSH	HL
	POP	DE
	EX	DE,HL
	ADD	HL,DE
; array store: t7[t10] = t6
	PUSH	HL
	POP	DE
	LD	(HL),E
	INC	HL
	LD	(HL),D
	LD	HL,(Y1)
	LD	HL,(MEMW)
	LD	HL,_CO
	LD	HL,$0004
	PUSH	HL
	POP	DE
	EX	DE,HL
	ADD	HL,DE
; array store: t12[t15] = t11
	PUSH	HL
	POP	DE
	LD	(HL),E
	INC	HL
	LD	(HL),D
	LD	HL,(X2)
	LD	HL,(MEMW)
	LD	HL,_CO
	LD	HL,$0006
	PUSH	HL
	POP	DE
	EX	DE,HL
	ADD	HL,DE
; array store: t17[t20] = t16
	PUSH	HL
	POP	DE
	LD	(HL),E
	INC	HL
	LD	(HL),D
	LD	HL,(Y2)
	LD	HL,(MEMW)
	LD	HL,_CO
	LD	HL,$0008
	PUSH	HL
	POP	DE
	EX	DE,HL
	ADD	HL,DE
; array store: t22[t25] = t21
	PUSH	HL
	POP	DE
	LD	(HL),E
	INC	HL
	LD	(HL),D
	LD	HL,$000F
	LD	HL,(_CO)
	LD	HL,$000A
; array store: t27[t28] = t26
	PUSH	HL
	POP	DE
	LD	(HL),E
	INC	HL
	LD	(HL),D
	LD	HL,_CO
	LD	(^IX),HL
	CALL	CALLMAGIC
_EXIT:
	POP	IY
	RET

@LINEC:
; function @LINEC
	PUSH	IY
	LD	HL,$0007
	LD	HL,(_CO)
	LD	HL,$0000
; array store: t1[t2] = t0
	PUSH	HL
	POP	DE
	LD	(HL),E
	INC	HL
	LD	(HL),D
	LD	HL,$0000
	LD	HL,(_CO)
	LD	HL,$0001
; array store: t4[t5] = t3
	PUSH	HL
	POP	DE
	LD	(HL),E
	INC	HL
	LD	(HL),D
	LD	HL,$0000
	LD	HL,(_CO)
	LD	HL,$0002
; array store: t7[t8] = t6
	PUSH	HL
	POP	DE
	LD	(HL),E
	INC	HL
	LD	(HL),D
	LD	HL,$0000
	LD	HL,(_CO)
	LD	HL,$0003
; array store: t10[t11] = t9
	PUSH	HL
	POP	DE
	LD	(HL),E
	INC	HL
	LD	(HL),D
	LD	HL,$0002
	LD	HL,(_CO)
	LD	HL,$0004
; array store: t13[t14] = t12
	PUSH	HL
	POP	DE
	LD	(HL),E
	INC	HL
	LD	(HL),D
	LD	HL,(X1)
	LD	HL,(MEMW)
	LD	HL,_CO
	LD	HL,$0005
	PUSH	HL
	POP	DE
	EX	DE,HL
	ADD	HL,DE
; array store: t16[t19] = t15
	PUSH	HL
	POP	DE
	LD	(HL),E
	INC	HL
	LD	(HL),D
	LD	HL,(Y1)
	LD	HL,(MEMW)
	LD	HL,_CO
	LD	HL,$0007
	PUSH	HL
	POP	DE
	EX	DE,HL
	ADD	HL,DE
; array store: t21[t24] = t20
	PUSH	HL
	POP	DE
	LD	(HL),E
	INC	HL
	LD	(HL),D
	LD	HL,(X2)
	LD	HL,(MEMW)
	LD	HL,_CO
	LD	HL,$0009
	PUSH	HL
	POP	DE
	EX	DE,HL
	ADD	HL,DE
; array store: t26[t29] = t25
	PUSH	HL
	POP	DE
	LD	(HL),E
	INC	HL
	LD	(HL),D
	LD	HL,(Y2)
	LD	HL,(MEMW)
	LD	HL,_CO
	LD	HL,$000B
	PUSH	HL
	POP	DE
	EX	DE,HL
	ADD	HL,DE
; array store: t31[t34] = t30
	PUSH	HL
	POP	DE
	LD	(HL),E
	INC	HL
	LD	(HL),D
	LD	HL,$000F
	LD	HL,(_CO)
	LD	HL,$000D
; array store: t36[t37] = t35
	PUSH	HL
	POP	DE
	LD	(HL),E
	INC	HL
	LD	(HL),D
	LD	HL,(COL)
	PUSH	HL
	CALL	@COLDRAW
_EXIT:
	POP	IY
	RET

@SPLINE:
; function @SPLINE
	PUSH	IY
	LD	HL,$0001
	LD	HL,(_CO)
	LD	HL,$0000
; array store: t1[t2] = t0
	PUSH	HL
	POP	DE
	LD	(HL),E
	INC	HL
	LD	(HL),D
	LD	HL,(X1)
	LD	HL,(MEMW)
	LD	HL,_CO
	LD	HL,$0001
	PUSH	HL
	POP	DE
	EX	DE,HL
	ADD	HL,DE
; array store: t4[t7] = t3
	PUSH	HL
	POP	DE
	LD	(HL),E
	INC	HL
	LD	(HL),D
	LD	HL,(Y1)
	LD	HL,(MEMW)
	LD	HL,_CO
	LD	HL,$0003
	PUSH	HL
	POP	DE
	EX	DE,HL
	ADD	HL,DE
; array store: t9[t12] = t8
	PUSH	HL
	POP	DE
	LD	(HL),E
	INC	HL
	LD	(HL),D
	LD	HL,(X2)
	LD	HL,(MEMW)
	LD	HL,_CO
	LD	HL,$0005
	PUSH	HL
	POP	DE
	EX	DE,HL
	ADD	HL,DE
; array store: t14[t17] = t13
	PUSH	HL
	POP	DE
	LD	(HL),E
	INC	HL
	LD	(HL),D
	LD	HL,(Y2)
	LD	HL,(MEMW)
	LD	HL,_CO
	LD	HL,$0007
	PUSH	HL
	POP	DE
	EX	DE,HL
	ADD	HL,DE
; array store: t19[t22] = t18
	PUSH	HL
	POP	DE
	LD	(HL),E
	INC	HL
	LD	(HL),D
	LD	HL,(X3)
	LD	HL,(MEMW)
	LD	HL,_CO
	LD	HL,$0009
	PUSH	HL
	POP	DE
	EX	DE,HL
	ADD	HL,DE
; array store: t24[t27] = t23
	PUSH	HL
	POP	DE
	LD	(HL),E
	INC	HL
	LD	(HL),D
	LD	HL,(Y3)
	LD	HL,(MEMW)
	LD	HL,_CO
	LD	HL,$000B
	PUSH	HL
	POP	DE
	EX	DE,HL
	ADD	HL,DE
; array store: t29[t32] = t28
	PUSH	HL
	POP	DE
	LD	(HL),E
	INC	HL
	LD	(HL),D
	LD	HL,$000F
	LD	HL,(_CO)
	LD	HL,$000D
; array store: t34[t35] = t33
	PUSH	HL
	POP	DE
	LD	(HL),E
	INC	HL
	LD	(HL),D
	LD	HL,_CO
	LD	(^IX),HL
	CALL	CALLMAGIC
_EXIT:
	POP	IY
	RET

@SPLINEC:
; function @SPLINEC
	PUSH	IY
	LD	HL,$0007
	LD	HL,(_CO)
	LD	HL,$0000
; array store: t1[t2] = t0
	PUSH	HL
	POP	DE
	LD	(HL),E
	INC	HL
	LD	(HL),D
	LD	HL,$0000
	LD	HL,(_CO)
	LD	HL,$0001
; array store: t4[t5] = t3
	PUSH	HL
	POP	DE
	LD	(HL),E
	INC	HL
	LD	(HL),D
	LD	HL,$0000
	LD	HL,(_CO)
	LD	HL,$0002
; array store: t7[t8] = t6
	PUSH	HL
	POP	DE
	LD	(HL),E
	INC	HL
	LD	(HL),D
	LD	HL,$0001
	LD	HL,(_CO)
	LD	HL,$0003
; array store: t10[t11] = t9
	PUSH	HL
	POP	DE
	LD	(HL),E
	INC	HL
	LD	(HL),D
	LD	HL,(X1)
	LD	HL,(MEMW)
	LD	HL,_CO
	LD	HL,$0004
	PUSH	HL
	POP	DE
	EX	DE,HL
	ADD	HL,DE
; array store: t13[t16] = t12
	PUSH	HL
	POP	DE
	LD	(HL),E
	INC	HL
	LD	(HL),D
	LD	HL,(Y1)
	LD	HL,(MEMW)
	LD	HL,_CO
	LD	HL,$0006
	PUSH	HL
	POP	DE
	EX	DE,HL
	ADD	HL,DE
; array store: t18[t21] = t17
	PUSH	HL
	POP	DE
	LD	(HL),E
	INC	HL
	LD	(HL),D
	LD	HL,(X2)
	LD	HL,(MEMW)
	LD	HL,_CO
	LD	HL,$0008
	PUSH	HL
	POP	DE
	EX	DE,HL
	ADD	HL,DE
; array store: t23[t26] = t22
	PUSH	HL
	POP	DE
	LD	(HL),E
	INC	HL
	LD	(HL),D
	LD	HL,(Y2)
	LD	HL,(MEMW)
	LD	HL,_CO
	LD	HL,$000A
	PUSH	HL
	POP	DE
	EX	DE,HL
	ADD	HL,DE
; array store: t28[t31] = t27
	PUSH	HL
	POP	DE
	LD	(HL),E
	INC	HL
	LD	(HL),D
	LD	HL,(X3)
	LD	HL,(MEMW)
	LD	HL,_CO
	LD	HL,$000C
	PUSH	HL
	POP	DE
	EX	DE,HL
	ADD	HL,DE
; array store: t33[t36] = t32
	PUSH	HL
	POP	DE
	LD	(HL),E
	INC	HL
	LD	(HL),D
	LD	HL,(Y3)
	LD	HL,(MEMW)
	LD	HL,_CO
	LD	HL,$000E
	PUSH	HL
	POP	DE
	EX	DE,HL
	ADD	HL,DE
; array store: t38[t41] = t37
	PUSH	HL
	POP	DE
	LD	(HL),E
	INC	HL
	LD	(HL),D
	LD	HL,$000F
	LD	HL,(_CO)
	LD	HL,$0010
; array store: t43[t44] = t42
	PUSH	HL
	POP	DE
	LD	(HL),E
	INC	HL
	LD	(HL),D
	LD	HL,(COL)
	PUSH	HL
	CALL	@COLDRAW
_EXIT:
	POP	IY
	RET

@BOX:
; function @BOX
	PUSH	IY
	LD	HL,$0002
	LD	HL,(_CO)
	LD	HL,$0000
; array store: t1[t2] = t0
	PUSH	HL
	POP	DE
	LD	(HL),E
	INC	HL
	LD	(HL),D
	LD	HL,(X1)
	LD	HL,(MEMW)
	LD	HL,_CO
	LD	HL,$0001
	PUSH	HL
	POP	DE
	EX	DE,HL
	ADD	HL,DE
; array store: t4[t7] = t3
	PUSH	HL
	POP	DE
	LD	(HL),E
	INC	HL
	LD	(HL),D
	LD	HL,(Y1)
	LD	HL,(MEMW)
	LD	HL,_CO
	LD	HL,$0003
	PUSH	HL
	POP	DE
	EX	DE,HL
	ADD	HL,DE
; array store: t9[t12] = t8
	PUSH	HL
	POP	DE
	LD	(HL),E
	INC	HL
	LD	(HL),D
	LD	HL,(X2)
	LD	HL,(MEMW)
	LD	HL,_CO
	LD	HL,$0005
	PUSH	HL
	POP	DE
	EX	DE,HL
	ADD	HL,DE
; array store: t14[t17] = t13
	PUSH	HL
	POP	DE
	LD	(HL),E
	INC	HL
	LD	(HL),D
	LD	HL,(Y2)
	LD	HL,(MEMW)
	LD	HL,_CO
	LD	HL,$0007
	PUSH	HL
	POP	DE
	EX	DE,HL
	ADD	HL,DE
; array store: t19[t22] = t18
	PUSH	HL
	POP	DE
	LD	(HL),E
	INC	HL
	LD	(HL),D
	LD	HL,$000F
	LD	HL,(_CO)
	LD	HL,$0009
; array store: t24[t25] = t23
	PUSH	HL
	POP	DE
	LD	(HL),E
	INC	HL
	LD	(HL),D
	LD	HL,_CO
	LD	(^IX),HL
	CALL	CALLMAGIC
_EXIT:
	POP	IY
	RET

@BOXC:
; function @BOXC
	PUSH	IY
	LD	HL,$0007
	LD	HL,(_CO)
	LD	HL,$0000
; array store: t1[t2] = t0
	PUSH	HL
	POP	DE
	LD	(HL),E
	INC	HL
	LD	(HL),D
	LD	HL,$0000
	LD	HL,(_CO)
	LD	HL,$0001
; array store: t4[t5] = t3
	PUSH	HL
	POP	DE
	LD	(HL),E
	INC	HL
	LD	(HL),D
	LD	HL,$0000
	LD	HL,(_CO)
	LD	HL,$0002
; array store: t7[t8] = t6
	PUSH	HL
	POP	DE
	LD	(HL),E
	INC	HL
	LD	(HL),D
	LD	HL,$0002
	LD	HL,(_CO)
	LD	HL,$0003
; array store: t10[t11] = t9
	PUSH	HL
	POP	DE
	LD	(HL),E
	INC	HL
	LD	(HL),D
	LD	HL,(X1)
	LD	HL,(MEMW)
	LD	HL,_CO
	LD	HL,$0004
	PUSH	HL
	POP	DE
	EX	DE,HL
	ADD	HL,DE
; array store: t13[t16] = t12
	PUSH	HL
	POP	DE
	LD	(HL),E
	INC	HL
	LD	(HL),D
	LD	HL,(Y1)
	LD	HL,(MEMW)
	LD	HL,_CO
	LD	HL,$0006
	PUSH	HL
	POP	DE
	EX	DE,HL
	ADD	HL,DE
; array store: t18[t21] = t17
	PUSH	HL
	POP	DE
	LD	(HL),E
	INC	HL
	LD	(HL),D
	LD	HL,(X2)
	LD	HL,(MEMW)
	LD	HL,_CO
	LD	HL,$0008
	PUSH	HL
	POP	DE
	EX	DE,HL
	ADD	HL,DE
; array store: t23[t26] = t22
	PUSH	HL
	POP	DE
	LD	(HL),E
	INC	HL
	LD	(HL),D
	LD	HL,(Y2)
	LD	HL,(MEMW)
	LD	HL,_CO
	LD	HL,$000A
	PUSH	HL
	POP	DE
	EX	DE,HL
	ADD	HL,DE
; array store: t28[t31] = t27
	PUSH	HL
	POP	DE
	LD	(HL),E
	INC	HL
	LD	(HL),D
	LD	HL,$000F
	LD	HL,(_CO)
	LD	HL,$000C
; array store: t33[t34] = t32
	PUSH	HL
	POP	DE
	LD	(HL),E
	INC	HL
	LD	(HL),D
	LD	HL,(COL)
	PUSH	HL
	CALL	@COLDRAW
_EXIT:
	POP	IY
	RET

@COLDRAW:
; function @COLDRAW
	PUSH	IY
	LD	HL,$0000
	LD	(I),HL
_L0:
	LD	HL,(COL)
	PUSH	HL
	LD	HL,(I)
	PUSH	HL
	CALL	BIT
; if t3 == 0 goto _L4
	LD	A,H
	OR	L
	JP	Z,_L4
	LD	HL,$0002
	LD	HL,(_CO)
	LD	HL,$0001
; array store: t5[t6] = t4
	PUSH	HL
	POP	DE
	LD	(HL),E
	INC	HL
	LD	(HL),D
	JP	_L3
_L4:
	LD	HL,$0000
	LD	HL,(_CO)
	LD	HL,$0001
; array store: t8[t9] = t7
	PUSH	HL
	POP	DE
	LD	(HL),E
	INC	HL
	LD	(HL),D
_L3:
	LD	HL,_CO
	LD	(^IX),HL
	CALL	CALLMAGIC
	LD	HL,(I)
	LD	HL,$0001
	PUSH	HL
	POP	DE
	EX	DE,HL
	ADD	HL,DE
	LD	HL,(_CO)
	LD	HL,$0002
; array store: t15[t16] = t14
	PUSH	HL
	POP	DE
	LD	(HL),E
	INC	HL
	LD	(HL),D
_L1:
	LD	HL,(I)
	LD	HL,$0001
	PUSH	HL
	POP	DE
	EX	DE,HL
	ADD	HL,DE
	LD	(I),HL
	LD	HL,$0002
	PUSH	HL
	POP	DE
	EX	DE,HL
	EX	DE,HL
	OR	A
	SBC	HL,DE
	LD	HL,$0000
	JR	C,$+3
	INC	HL
	LD	A,L
	XOR	$01
	LD	L,A
; if t21 != 0 goto _L0
	LD	A,H
	OR	L
	JP	NZ,_L0
_L2:
_EXIT:
	POP	IY
	RET

@COLDRAWT:
; function @COLDRAWT
	PUSH	IY
	LD	HL,$0000
	LD	(I),HL
_L5:
	LD	HL,(COL)
	PUSH	HL
	LD	HL,(I)
	PUSH	HL
	CALL	BIT
; if t3 == 0 goto _L9
	LD	A,H
	OR	L
	JP	Z,_L9
	LD	HL,$0002
	LD	HL,(_CO)
	LD	HL,$0001
; array store: t5[t6] = t4
	PUSH	HL
	POP	DE
	LD	(HL),E
	INC	HL
	LD	(HL),D
	LD	HL,(_TILE1)
	LD	HL,(MEMW)
	LD	HL,_CO
	LD	HL,$0004
	PUSH	HL
	POP	DE
	EX	DE,HL
	ADD	HL,DE
; array store: t8[t11] = t7
	PUSH	HL
	POP	DE
	LD	(HL),E
	INC	HL
	LD	(HL),D
	LD	HL,(_TILE2)
	LD	HL,(MEMW)
	LD	HL,_CO
	LD	HL,$0006
	PUSH	HL
	POP	DE
	EX	DE,HL
	ADD	HL,DE
; array store: t13[t16] = t12
	PUSH	HL
	POP	DE
	LD	(HL),E
	INC	HL
	LD	(HL),D
	JP	_L8
_L9:
	LD	HL,$0000
	LD	HL,(_CO)
	LD	HL,$0001
; array store: t18[t19] = t17
	PUSH	HL
	POP	DE
	LD	(HL),E
	INC	HL
	LD	(HL),D
	LD	HL,$0000
	LD	HL,(MEMW)
	LD	HL,_CO
	LD	HL,$0004
	PUSH	HL
	POP	DE
	EX	DE,HL
	ADD	HL,DE
; array store: t21[t24] = t20
	PUSH	HL
	POP	DE
	LD	(HL),E
	INC	HL
	LD	(HL),D
	LD	HL,$0000
	LD	HL,(MEMW)
	LD	HL,_CO
	LD	HL,$0006
	PUSH	HL
	POP	DE
	EX	DE,HL
	ADD	HL,DE
; array store: t26[t29] = t25
	PUSH	HL
	POP	DE
	LD	(HL),E
	INC	HL
	LD	(HL),D
_L8:
	LD	HL,_CO
	LD	(^IX),HL
	CALL	CALLMAGIC
	LD	HL,(I)
	LD	HL,$0001
	PUSH	HL
	POP	DE
	EX	DE,HL
	ADD	HL,DE
	LD	HL,(_CO)
	LD	HL,$0002
; array store: t35[t36] = t34
	PUSH	HL
	POP	DE
	LD	(HL),E
	INC	HL
	LD	(HL),D
_L6:
	LD	HL,(I)
	LD	HL,$0001
	PUSH	HL
	POP	DE
	EX	DE,HL
	ADD	HL,DE
	LD	(I),HL
	LD	HL,$0002
	PUSH	HL
	POP	DE
	EX	DE,HL
	EX	DE,HL
	OR	A
	SBC	HL,DE
	LD	HL,$0000
	JR	C,$+3
	INC	HL
	LD	A,L
	XOR	$01
	LD	L,A
; if t41 != 0 goto _L5
	LD	A,H
	OR	L
	JP	NZ,_L5
_L7:
_EXIT:
	POP	IY
	RET

@TRIANGLE:
; function @TRIANGLE
	PUSH	IY
	LD	HL,$0003
	LD	HL,(_CO)
	LD	HL,$0000
; array store: t1[t2] = t0
	PUSH	HL
	POP	DE
	LD	(HL),E
	INC	HL
	LD	(HL),D
	LD	HL,(_TILE1)
	LD	HL,(MEMW)
	LD	HL,_CO
	LD	HL,$0001
	PUSH	HL
	POP	DE
	EX	DE,HL
	ADD	HL,DE
; array store: t4[t7] = t3
	PUSH	HL
	POP	DE
	LD	(HL),E
	INC	HL
	LD	(HL),D
	LD	HL,(_TILE2)
	LD	HL,(MEMW)
	LD	HL,_CO
	LD	HL,$0003
	PUSH	HL
	POP	DE
	EX	DE,HL
	ADD	HL,DE
; array store: t9[t12] = t8
	PUSH	HL
	POP	DE
	LD	(HL),E
	INC	HL
	LD	(HL),D
	LD	HL,(X1)
	LD	HL,(MEMW)
	LD	HL,_CO
	LD	HL,$0005
	PUSH	HL
	POP	DE
	EX	DE,HL
	ADD	HL,DE
; array store: t14[t17] = t13
	PUSH	HL
	POP	DE
	LD	(HL),E
	INC	HL
	LD	(HL),D
	LD	HL,(Y1)
	LD	HL,(MEMW)
	LD	HL,_CO
	LD	HL,$0007
	PUSH	HL
	POP	DE
	EX	DE,HL
	ADD	HL,DE
; array store: t19[t22] = t18
	PUSH	HL
	POP	DE
	LD	(HL),E
	INC	HL
	LD	(HL),D
	LD	HL,(X2)
	LD	HL,(MEMW)
	LD	HL,_CO
	LD	HL,$0009
	PUSH	HL
	POP	DE
	EX	DE,HL
	ADD	HL,DE
; array store: t24[t27] = t23
	PUSH	HL
	POP	DE
	LD	(HL),E
	INC	HL
	LD	(HL),D
	LD	HL,(Y2)
	LD	HL,(MEMW)
	LD	HL,_CO
	LD	HL,$000B
	PUSH	HL
	POP	DE
	EX	DE,HL
	ADD	HL,DE
; array store: t29[t32] = t28
	PUSH	HL
	POP	DE
	LD	(HL),E
	INC	HL
	LD	(HL),D
	LD	HL,(X3)
	LD	HL,(MEMW)
	LD	HL,_CO
	LD	HL,$000D
	PUSH	HL
	POP	DE
	EX	DE,HL
	ADD	HL,DE
; array store: t34[t37] = t33
	PUSH	HL
	POP	DE
	LD	(HL),E
	INC	HL
	LD	(HL),D
	LD	HL,(Y3)
	LD	HL,(MEMW)
	LD	HL,_CO
	LD	HL,$000F
	PUSH	HL
	POP	DE
	EX	DE,HL
	ADD	HL,DE
; array store: t39[t42] = t38
	PUSH	HL
	POP	DE
	LD	(HL),E
	INC	HL
	LD	(HL),D
	LD	HL,$000F
	LD	HL,(_CO)
	LD	HL,$0011
; array store: t44[t45] = t43
	PUSH	HL
	POP	DE
	LD	(HL),E
	INC	HL
	LD	(HL),D
	LD	HL,_CO
	LD	(^IX),HL
	CALL	CALLMAGIC
_EXIT:
	POP	IY
	RET

@TRIANGLEC:
; function @TRIANGLEC
	PUSH	IY
	LD	HL,$0007
	LD	HL,(_CO)
	LD	HL,$0000
; array store: t1[t2] = t0
	PUSH	HL
	POP	DE
	LD	(HL),E
	INC	HL
	LD	(HL),D
	LD	HL,$0000
	LD	HL,(_CO)
	LD	HL,$0001
; array store: t4[t5] = t3
	PUSH	HL
	POP	DE
	LD	(HL),E
	INC	HL
	LD	(HL),D
	LD	HL,$0000
	LD	HL,(_CO)
	LD	HL,$0002
; array store: t7[t8] = t6
	PUSH	HL
	POP	DE
	LD	(HL),E
	INC	HL
	LD	(HL),D
	LD	HL,$0003
	LD	HL,(_CO)
	LD	HL,$0003
; array store: t10[t11] = t9
	PUSH	HL
	POP	DE
	LD	(HL),E
	INC	HL
	LD	(HL),D
	LD	HL,(X1)
	LD	HL,(MEMW)
	LD	HL,_CO
	LD	HL,$0008
	PUSH	HL
	POP	DE
	EX	DE,HL
	ADD	HL,DE
; array store: t13[t16] = t12
	PUSH	HL
	POP	DE
	LD	(HL),E
	INC	HL
	LD	(HL),D
	LD	HL,(Y1)
	LD	HL,(MEMW)
	LD	HL,_CO
	LD	HL,$000A
	PUSH	HL
	POP	DE
	EX	DE,HL
	ADD	HL,DE
; array store: t18[t21] = t17
	PUSH	HL
	POP	DE
	LD	(HL),E
	INC	HL
	LD	(HL),D
	LD	HL,(X2)
	LD	HL,(MEMW)
	LD	HL,_CO
	LD	HL,$000C
	PUSH	HL
	POP	DE
	EX	DE,HL
	ADD	HL,DE
; array store: t23[t26] = t22
	PUSH	HL
	POP	DE
	LD	(HL),E
	INC	HL
	LD	(HL),D
	LD	HL,(Y2)
	LD	HL,(MEMW)
	LD	HL,_CO
	LD	HL,$000E
	PUSH	HL
	POP	DE
	EX	DE,HL
	ADD	HL,DE
; array store: t28[t31] = t27
	PUSH	HL
	POP	DE
	LD	(HL),E
	INC	HL
	LD	(HL),D
	LD	HL,(X3)
	LD	HL,(MEMW)
	LD	HL,_CO
	LD	HL,$0010
	PUSH	HL
	POP	DE
	EX	DE,HL
	ADD	HL,DE
; array store: t33[t36] = t32
	PUSH	HL
	POP	DE
	LD	(HL),E
	INC	HL
	LD	(HL),D
	LD	HL,(Y3)
	LD	HL,(MEMW)
	LD	HL,_CO
	LD	HL,$0012
	PUSH	HL
	POP	DE
	EX	DE,HL
	ADD	HL,DE
; array store: t38[t41] = t37
	PUSH	HL
	POP	DE
	LD	(HL),E
	INC	HL
	LD	(HL),D
	LD	HL,$000F
	LD	HL,(_CO)
	LD	HL,$0014
; array store: t43[t44] = t42
	PUSH	HL
	POP	DE
	LD	(HL),E
	INC	HL
	LD	(HL),D
	LD	HL,(COL)
	PUSH	HL
	CALL	@COLDRAWT
_EXIT:
	POP	IY
	RET

@FULL:
; function @FULL
	PUSH	IY
	LD	HL,$0004
	LD	HL,(_CO)
	LD	HL,$0000
; array store: t1[t2] = t0
	PUSH	HL
	POP	DE
	LD	(HL),E
	INC	HL
	LD	(HL),D
	LD	HL,(_TILE1)
	LD	HL,(MEMW)
	LD	HL,_CO
	LD	HL,$0001
	PUSH	HL
	POP	DE
	EX	DE,HL
	ADD	HL,DE
; array store: t4[t7] = t3
	PUSH	HL
	POP	DE
	LD	(HL),E
	INC	HL
	LD	(HL),D
	LD	HL,(_TILE2)
	LD	HL,(MEMW)
	LD	HL,_CO
	LD	HL,$0003
	PUSH	HL
	POP	DE
	EX	DE,HL
	ADD	HL,DE
; array store: t9[t12] = t8
	PUSH	HL
	POP	DE
	LD	(HL),E
	INC	HL
	LD	(HL),D
	LD	HL,(X1)
	LD	HL,(MEMW)
	LD	HL,_CO
	LD	HL,$0005
	PUSH	HL
	POP	DE
	EX	DE,HL
	ADD	HL,DE
; array store: t14[t17] = t13
	PUSH	HL
	POP	DE
	LD	(HL),E
	INC	HL
	LD	(HL),D
	LD	HL,(Y1)
	LD	HL,(MEMW)
	LD	HL,_CO
	LD	HL,$0007
	PUSH	HL
	POP	DE
	EX	DE,HL
	ADD	HL,DE
; array store: t19[t22] = t18
	PUSH	HL
	POP	DE
	LD	(HL),E
	INC	HL
	LD	(HL),D
	LD	HL,(X2)
	LD	HL,(MEMW)
	LD	HL,_CO
	LD	HL,$0009
	PUSH	HL
	POP	DE
	EX	DE,HL
	ADD	HL,DE
; array store: t24[t27] = t23
	PUSH	HL
	POP	DE
	LD	(HL),E
	INC	HL
	LD	(HL),D
	LD	HL,(Y2)
	LD	HL,(MEMW)
	LD	HL,_CO
	LD	HL,$000B
	PUSH	HL
	POP	DE
	EX	DE,HL
	ADD	HL,DE
; array store: t29[t32] = t28
	PUSH	HL
	POP	DE
	LD	(HL),E
	INC	HL
	LD	(HL),D
	LD	HL,$000F
	LD	HL,(_CO)
	LD	HL,$000D
; array store: t34[t35] = t33
	PUSH	HL
	POP	DE
	LD	(HL),E
	INC	HL
	LD	(HL),D
	LD	HL,_CO
	LD	(^IX),HL
	CALL	CALLMAGIC
_EXIT:
	POP	IY
	RET

@FULLC:
; function @FULLC
	PUSH	IY
	LD	HL,$0007
	LD	HL,(_CO)
	LD	HL,$0000
; array store: t1[t2] = t0
	PUSH	HL
	POP	DE
	LD	(HL),E
	INC	HL
	LD	(HL),D
	LD	HL,$0000
	LD	HL,(_CO)
	LD	HL,$0001
; array store: t4[t5] = t3
	PUSH	HL
	POP	DE
	LD	(HL),E
	INC	HL
	LD	(HL),D
	LD	HL,$0000
	LD	HL,(_CO)
	LD	HL,$0002
; array store: t7[t8] = t6
	PUSH	HL
	POP	DE
	LD	(HL),E
	INC	HL
	LD	(HL),D
	LD	HL,$0004
	LD	HL,(_CO)
	LD	HL,$0003
; array store: t10[t11] = t9
	PUSH	HL
	POP	DE
	LD	(HL),E
	INC	HL
	LD	(HL),D
	LD	HL,(X1)
	LD	HL,(MEMW)
	LD	HL,_CO
	LD	HL,$0008
	PUSH	HL
	POP	DE
	EX	DE,HL
	ADD	HL,DE
; array store: t13[t16] = t12
	PUSH	HL
	POP	DE
	LD	(HL),E
	INC	HL
	LD	(HL),D
	LD	HL,(Y1)
	LD	HL,(MEMW)
	LD	HL,_CO
	LD	HL,$000A
	PUSH	HL
	POP	DE
	EX	DE,HL
	ADD	HL,DE
; array store: t18[t21] = t17
	PUSH	HL
	POP	DE
	LD	(HL),E
	INC	HL
	LD	(HL),D
	LD	HL,(X2)
	LD	HL,(MEMW)
	LD	HL,_CO
	LD	HL,$000C
	PUSH	HL
	POP	DE
	EX	DE,HL
	ADD	HL,DE
; array store: t23[t26] = t22
	PUSH	HL
	POP	DE
	LD	(HL),E
	INC	HL
	LD	(HL),D
	LD	HL,(Y2)
	LD	HL,(MEMW)
	LD	HL,_CO
	LD	HL,$000E
	PUSH	HL
	POP	DE
	EX	DE,HL
	ADD	HL,DE
; array store: t28[t31] = t27
	PUSH	HL
	POP	DE
	LD	(HL),E
	INC	HL
	LD	(HL),D
	LD	HL,$000F
	LD	HL,(_CO)
	LD	HL,$0010
; array store: t33[t34] = t32
	PUSH	HL
	POP	DE
	LD	(HL),E
	INC	HL
	LD	(HL),D
	LD	HL,(COL)
	PUSH	HL
	CALL	@COLDRAWT
_EXIT:
	POP	IY
	RET

@CIRCLE:
; function @CIRCLE
	PUSH	IY
	LD	HL,$0005
	LD	HL,(_CO)
	LD	HL,$0000
; array store: t1[t2] = t0
	PUSH	HL
	POP	DE
	LD	(HL),E
	INC	HL
	LD	(HL),D
	LD	HL,(_TILE1)
	LD	HL,(MEMW)
	LD	HL,_CO
	LD	HL,$0001
	PUSH	HL
	POP	DE
	EX	DE,HL
	ADD	HL,DE
; array store: t4[t7] = t3
	PUSH	HL
	POP	DE
	LD	(HL),E
	INC	HL
	LD	(HL),D
	LD	HL,(_TILE2)
	LD	HL,(MEMW)
	LD	HL,_CO
	LD	HL,$0003
	PUSH	HL
	POP	DE
	EX	DE,HL
	ADD	HL,DE
; array store: t9[t12] = t8
	PUSH	HL
	POP	DE
	LD	(HL),E
	INC	HL
	LD	(HL),D
	LD	HL,(X1)
	LD	HL,(MEMW)
	LD	HL,_CO
	LD	HL,$0005
	PUSH	HL
	POP	DE
	EX	DE,HL
	ADD	HL,DE
; array store: t14[t17] = t13
	PUSH	HL
	POP	DE
	LD	(HL),E
	INC	HL
	LD	(HL),D
	LD	HL,(Y1)
	LD	HL,(MEMW)
	LD	HL,_CO
	LD	HL,$0007
	PUSH	HL
	POP	DE
	EX	DE,HL
	ADD	HL,DE
; array store: t19[t22] = t18
	PUSH	HL
	POP	DE
	LD	(HL),E
	INC	HL
	LD	(HL),D
	LD	HL,(R1)
	LD	HL,(MEMW)
	LD	HL,_CO
	LD	HL,$0009
	PUSH	HL
	POP	DE
	EX	DE,HL
	ADD	HL,DE
; array store: t24[t27] = t23
	PUSH	HL
	POP	DE
	LD	(HL),E
	INC	HL
	LD	(HL),D
	LD	HL,$000F
	LD	HL,(_CO)
	LD	HL,$000B
; array store: t29[t30] = t28
	PUSH	HL
	POP	DE
	LD	(HL),E
	INC	HL
	LD	(HL),D
	LD	HL,_CO
	LD	(^IX),HL
	CALL	CALLMAGIC
_EXIT:
	POP	IY
	RET

@CIRCLEC:
; function @CIRCLEC
	PUSH	IY
	LD	HL,$0007
	LD	HL,(_CO)
	LD	HL,$0000
; array store: t1[t2] = t0
	PUSH	HL
	POP	DE
	LD	(HL),E
	INC	HL
	LD	(HL),D
	LD	HL,$0000
	LD	HL,(_CO)
	LD	HL,$0001
; array store: t4[t5] = t3
	PUSH	HL
	POP	DE
	LD	(HL),E
	INC	HL
	LD	(HL),D
	LD	HL,$0000
	LD	HL,(_CO)
	LD	HL,$0002
; array store: t7[t8] = t6
	PUSH	HL
	POP	DE
	LD	(HL),E
	INC	HL
	LD	(HL),D
	LD	HL,$0005
	LD	HL,(_CO)
	LD	HL,$0003
; array store: t10[t11] = t9
	PUSH	HL
	POP	DE
	LD	(HL),E
	INC	HL
	LD	(HL),D
	LD	HL,(X1)
	LD	HL,(MEMW)
	LD	HL,_CO
	LD	HL,$0008
	PUSH	HL
	POP	DE
	EX	DE,HL
	ADD	HL,DE
; array store: t13[t16] = t12
	PUSH	HL
	POP	DE
	LD	(HL),E
	INC	HL
	LD	(HL),D
	LD	HL,(Y1)
	LD	HL,(MEMW)
	LD	HL,_CO
	LD	HL,$000A
	PUSH	HL
	POP	DE
	EX	DE,HL
	ADD	HL,DE
; array store: t18[t21] = t17
	PUSH	HL
	POP	DE
	LD	(HL),E
	INC	HL
	LD	(HL),D
	LD	HL,(R1)
	LD	HL,(MEMW)
	LD	HL,_CO
	LD	HL,$000C
	PUSH	HL
	POP	DE
	EX	DE,HL
	ADD	HL,DE
; array store: t23[t26] = t22
	PUSH	HL
	POP	DE
	LD	(HL),E
	INC	HL
	LD	(HL),D
	LD	HL,$000F
	LD	HL,(_CO)
	LD	HL,$000E
; array store: t28[t29] = t27
	PUSH	HL
	POP	DE
	LD	(HL),E
	INC	HL
	LD	(HL),D
	LD	HL,(COL)
	PUSH	HL
	CALL	@COLDRAWT
_EXIT:
	POP	IY
	RET

@WINDOW:
; function @WINDOW
	PUSH	IY
	LD	HL,$0006
	LD	HL,(_CO)
	LD	HL,$0000
; array store: t1[t2] = t0
	PUSH	HL
	POP	DE
	LD	(HL),E
	INC	HL
	LD	(HL),D
	LD	HL,(X1)
	LD	HL,(MEMW)
	LD	HL,_CO
	LD	HL,$0001
	PUSH	HL
	POP	DE
	EX	DE,HL
	ADD	HL,DE
; array store: t4[t7] = t3
	PUSH	HL
	POP	DE
	LD	(HL),E
	INC	HL
	LD	(HL),D
	LD	HL,(Y1)
	LD	HL,(MEMW)
	LD	HL,_CO
	LD	HL,$0003
	PUSH	HL
	POP	DE
	EX	DE,HL
	ADD	HL,DE
; array store: t9[t12] = t8
	PUSH	HL
	POP	DE
	LD	(HL),E
	INC	HL
	LD	(HL),D
	LD	HL,(X2)
	LD	HL,(MEMW)
	LD	HL,_CO
	LD	HL,$0005
	PUSH	HL
	POP	DE
	EX	DE,HL
	ADD	HL,DE
; array store: t14[t17] = t13
	PUSH	HL
	POP	DE
	LD	(HL),E
	INC	HL
	LD	(HL),D
	LD	HL,(Y2)
	LD	HL,(MEMW)
	LD	HL,_CO
	LD	HL,$0007
	PUSH	HL
	POP	DE
	EX	DE,HL
	ADD	HL,DE
; array store: t19[t22] = t18
	PUSH	HL
	POP	DE
	LD	(HL),E
	INC	HL
	LD	(HL),D
	LD	HL,$000F
	LD	HL,(_CO)
	LD	HL,$0009
; array store: t24[t25] = t23
	PUSH	HL
	POP	DE
	LD	(HL),E
	INC	HL
	LD	(HL),D
	LD	HL,_CO
	LD	(^IX),HL
	CALL	CALLMAGIC
_EXIT:
	POP	IY
	RET

@MODE:
; function @MODE
	PUSH	IY
	LD	HL,$0007
	LD	HL,(_CO)
	LD	HL,$0000
; array store: t1[t2] = t0
	PUSH	HL
	POP	DE
	LD	(HL),E
	INC	HL
	LD	(HL),D
	LD	HL,(MODE)
	LD	HL,(_CO)
	LD	HL,$0001
; array store: t4[t5] = t3
	PUSH	HL
	POP	DE
	LD	(HL),E
	INC	HL
	LD	(HL),D
	LD	HL,(PLANE)
	LD	HL,(_CO)
	LD	HL,$0002
; array store: t7[t8] = t6
	PUSH	HL
	POP	DE
	LD	(HL),E
	INC	HL
	LD	(HL),D
	LD	HL,$000F
	LD	HL,(_CO)
	LD	HL,$0003
; array store: t10[t11] = t9
	PUSH	HL
	POP	DE
	LD	(HL),E
	INC	HL
	LD	(HL),D
	LD	HL,_CO
	LD	(^IX),HL
	CALL	CALLMAGIC
_EXIT:
	POP	IY
	RET

@CLS:
; function @CLS
	PUSH	IY
	LD	HL,$0009
	LD	HL,(_CO)
	LD	HL,$0000
; array store: t1[t2] = t0
	PUSH	HL
	POP	DE
	LD	(HL),E
	INC	HL
	LD	(HL),D
	LD	HL,$000F
	LD	HL,(_CO)
	LD	HL,$0001
; array store: t4[t5] = t3
	PUSH	HL
	POP	DE
	LD	(HL),E
	INC	HL
	LD	(HL),D
	LD	HL,_CO
	LD	(^IX),HL
	CALL	CALLMAGIC
_EXIT:
	POP	IY
	RET

@PALET:
; function @PALET
	PUSH	IY
	LD	HL,$000A
	LD	HL,(_CO)
	LD	HL,$0000
; array store: t1[t2] = t0
	PUSH	HL
	POP	DE
	LD	(HL),E
	INC	HL
	LD	(HL),D
	LD	HL,(A0)
	LD	HL,(_PAL)
	LD	HL,$0000
; array store: t4[t5] = t3
	PUSH	HL
	POP	DE
	LD	(HL),E
	INC	HL
	LD	(HL),D
	LD	HL,(A0)
	LD	HL,(_CO)
	LD	HL,$0001
; array store: t7[t8] = t6
	PUSH	HL
	POP	DE
	LD	(HL),E
	INC	HL
	LD	(HL),D
	LD	HL,(A1)
	LD	HL,(_PAL)
	LD	HL,$0001
; array store: t10[t11] = t9
	PUSH	HL
	POP	DE
	LD	(HL),E
	INC	HL
	LD	(HL),D
	LD	HL,(A1)
	LD	HL,(_CO)
	LD	HL,$0002
; array store: t13[t14] = t12
	PUSH	HL
	POP	DE
	LD	(HL),E
	INC	HL
	LD	(HL),D
	LD	HL,(A2)
	LD	HL,(_PAL)
	LD	HL,$0002
; array store: t16[t17] = t15
	PUSH	HL
	POP	DE
	LD	(HL),E
	INC	HL
	LD	(HL),D
	LD	HL,(A2)
	LD	HL,(_CO)
	LD	HL,$0003
; array store: t19[t20] = t18
	PUSH	HL
	POP	DE
	LD	(HL),E
	INC	HL
	LD	(HL),D
	LD	HL,(A3)
	LD	HL,(_PAL)
	LD	HL,$0003
; array store: t22[t23] = t21
	PUSH	HL
	POP	DE
	LD	(HL),E
	INC	HL
	LD	(HL),D
	LD	HL,(A3)
	LD	HL,(_CO)
	LD	HL,$0004
; array store: t25[t26] = t24
	PUSH	HL
	POP	DE
	LD	(HL),E
	INC	HL
	LD	(HL),D
	LD	HL,(A4)
	LD	HL,(_PAL)
	LD	HL,$0004
; array store: t28[t29] = t27
	PUSH	HL
	POP	DE
	LD	(HL),E
	INC	HL
	LD	(HL),D
	LD	HL,(A4)
	LD	HL,(_CO)
	LD	HL,$0005
; array store: t31[t32] = t30
	PUSH	HL
	POP	DE
	LD	(HL),E
	INC	HL
	LD	(HL),D
	LD	HL,(A5)
	LD	HL,(_PAL)
	LD	HL,$0005
; array store: t34[t35] = t33
	PUSH	HL
	POP	DE
	LD	(HL),E
	INC	HL
	LD	(HL),D
	LD	HL,(A5)
	LD	HL,(_CO)
	LD	HL,$0006
; array store: t37[t38] = t36
	PUSH	HL
	POP	DE
	LD	(HL),E
	INC	HL
	LD	(HL),D
	LD	HL,(A6)
	LD	HL,(_PAL)
	LD	HL,$0006
; array store: t40[t41] = t39
	PUSH	HL
	POP	DE
	LD	(HL),E
	INC	HL
	LD	(HL),D
	LD	HL,(A6)
	LD	HL,(_CO)
	LD	HL,$0007
; array store: t43[t44] = t42
	PUSH	HL
	POP	DE
	LD	(HL),E
	INC	HL
	LD	(HL),D
	LD	HL,(A7)
	LD	HL,(_PAL)
	LD	HL,$0007
; array store: t46[t47] = t45
	PUSH	HL
	POP	DE
	LD	(HL),E
	INC	HL
	LD	(HL),D
	LD	HL,(A7)
	LD	HL,(_CO)
	LD	HL,$0008
; array store: t49[t50] = t48
	PUSH	HL
	POP	DE
	LD	(HL),E
	INC	HL
	LD	(HL),D
	LD	HL,$000F
	LD	HL,(_CO)
	LD	HL,$0009
; array store: t52[t53] = t51
	PUSH	HL
	POP	DE
	LD	(HL),E
	INC	HL
	LD	(HL),D
	LD	HL,_CO
	LD	(^IX),HL
	CALL	CALLMAGIC
_EXIT:
	POP	IY
	RET

@INIT:
; function @INIT
	PUSH	IY
	LD	HL,$0000
	PUSH	HL
	LD	HL,$0000
	PUSH	HL
	LD	HL,$027F
	PUSH	HL
	LD	HL,$00C7
	PUSH	HL
	CALL	@WINDOW
	LD	HL,$0002
	PUSH	HL
	LD	HL,$0002
	PUSH	HL
	CALL	@MODE
	CALL	@CLS
	LD	HL,$0002
	PUSH	HL
	LD	HL,$0001
	PUSH	HL
	CALL	@MODE
	CALL	@CLS
	LD	HL,$0002
	PUSH	HL
	LD	HL,$0000
	PUSH	HL
	CALL	@MODE
	CALL	@CLS
	LD	HL,$0007
	PUSH	HL
	CALL	@GRAD
	LD	HL,$0001
	LD	(_WSEL),HL
	LD	HL,$0000
	LD	(_MSCREEN),HL
	LD	HL,$0000
	LD	(_BSWITCH),HL
	LD	HL,$0002
	LD	(_DEMODE),HL
	LD	HL,$0000
	PUSH	HL
	LD	HL,$0001
	PUSH	HL
	LD	HL,$0002
	PUSH	HL
	LD	HL,$0003
	PUSH	HL
	LD	HL,$0004
	PUSH	HL
	LD	HL,$0005
	PUSH	HL
	LD	HL,$0006
	PUSH	HL
	LD	HL,$0007
	PUSH	HL
	CALL	@PALET
; load string ""
	LD	HL,0 ; string placeholder
	CALL	__print_str
_EXIT:
	POP	IY
	RET

@SETOD:
; function @SETOD
	PUSH	IY
	LD	HL,$0000
	LD	(I),HL
_L10:
	LD	HL,$0000
	LD	(K),HL
_L13:
	LD	HL,(MEMW)
	LD	HL,(POSIT)
	LD	HL,(I)
	LD	HL,$0006
	PUSH	HL
	POP	DE
	EX	DE,HL
	CALL	MUL16
	PUSH	HL
	POP	DE
	EX	DE,HL
	ADD	HL,DE
	LD	HL,(K)
	LD	HL,$0002
	PUSH	HL
	POP	DE
	EX	DE,HL
	CALL	MUL16
	PUSH	HL
	POP	DE
	EX	DE,HL
	ADD	HL,DE
; array load: t13 = t2[t12]
	PUSH	HL
	POP	DE
	EX	DE,HL
	ADD	HL,HL
	ADD	HL,DE
	LD	E,(HL)
	INC	HL
	LD	D,(HL)
	EX	DE,HL
	LD	HL,(_ZAHYO)
	LD	HL,(I)
; array load: t16 = t14[t15]
	PUSH	HL
	POP	DE
	EX	DE,HL
	ADD	HL,HL
	ADD	HL,DE
	LD	E,(HL)
	INC	HL
	LD	D,(HL)
	EX	DE,HL
	LD	HL,(K)
; array store: t16[t17] = t13
	PUSH	HL
	POP	DE
	LD	(HL),E
	INC	HL
	LD	(HL),D
_L14:
	LD	HL,(K)
	LD	HL,$0001
	PUSH	HL
	POP	DE
	EX	DE,HL
	ADD	HL,DE
	LD	(K),HL
	LD	HL,$0002
	PUSH	HL
	POP	DE
	EX	DE,HL
	EX	DE,HL
	OR	A
	SBC	HL,DE
	LD	HL,$0000
	JR	C,$+3
	INC	HL
	LD	A,L
	XOR	$01
	LD	L,A
; if t22 != 0 goto _L13
	LD	A,H
	OR	L
	JP	NZ,_L13
_L15:
_L11:
	LD	HL,(I)
	LD	HL,$0001
	PUSH	HL
	POP	DE
	EX	DE,HL
	ADD	HL,DE
	LD	(I),HL
	LD	HL,(LENGTH)
	PUSH	HL
	POP	DE
	EX	DE,HL
	EX	DE,HL
	OR	A
	SBC	HL,DE
	LD	HL,$0000
	JR	C,$+3
	INC	HL
	LD	A,L
	XOR	$01
	LD	L,A
; if t27 != 0 goto _L10
	LD	A,H
	OR	L
	JP	NZ,_L10
_L12:
	LD	HL,(LENGTH)
	LD	HL,$0001
	PUSH	HL
	POP	DE
	EX	DE,HL
	ADD	HL,DE
	LD	HL,(_LPCT)
	LD	HL,$0000
; array store: t31[t32] = t30
	PUSH	HL
	POP	DE
	LD	(HL),E
	INC	HL
	LD	(HL),D
_EXIT:
	POP	IY
	RET

@SETWD:
; function @SETWD
	PUSH	IY
	LD	HL,$0000
	LD	(I),HL
_L16:
	LD	HL,$0000
	LD	(K),HL
_L19:
	LD	HL,(MEM)
	LD	HL,(POSIT)
	LD	HL,(I)
	LD	HL,$0002
	PUSH	HL
	POP	DE
	EX	DE,HL
	CALL	MUL16
	PUSH	HL
	POP	DE
	EX	DE,HL
	ADD	HL,DE
	LD	HL,(K)
	PUSH	HL
	POP	DE
	EX	DE,HL
	ADD	HL,DE
; array load: t11 = t2[t10]
	PUSH	HL
	POP	DE
	EX	DE,HL
	ADD	HL,HL
	ADD	HL,DE
	LD	E,(HL)
	INC	HL
	LD	D,(HL)
	EX	DE,HL
	LD	HL,(_WIRE)
	LD	HL,(I)
; array load: t14 = t12[t13]
	PUSH	HL
	POP	DE
	EX	DE,HL
	ADD	HL,HL
	ADD	HL,DE
	LD	E,(HL)
	INC	HL
	LD	D,(HL)
	EX	DE,HL
	LD	HL,(K)
; array store: t14[t15] = t11
	PUSH	HL
	POP	DE
	LD	(HL),E
	INC	HL
	LD	(HL),D
_L20:
	LD	HL,(K)
	LD	HL,$0001
	PUSH	HL
	POP	DE
	EX	DE,HL
	ADD	HL,DE
	LD	(K),HL
	LD	HL,$0001
	PUSH	HL
	POP	DE
	EX	DE,HL
	EX	DE,HL
	OR	A
	SBC	HL,DE
	LD	HL,$0000
	JR	C,$+3
	INC	HL
	LD	A,L
	XOR	$01
	LD	L,A
; if t20 != 0 goto _L19
	LD	A,H
	OR	L
	JP	NZ,_L19
_L21:
_L17:
	LD	HL,(I)
	LD	HL,$0001
	PUSH	HL
	POP	DE
	EX	DE,HL
	ADD	HL,DE
	LD	(I),HL
	LD	HL,(LENGTH)
	PUSH	HL
	POP	DE
	EX	DE,HL
	EX	DE,HL
	OR	A
	SBC	HL,DE
	LD	HL,$0000
	JR	C,$+3
	INC	HL
	LD	A,L
	XOR	$01
	LD	L,A
; if t25 != 0 goto _L16
	LD	A,H
	OR	L
	JP	NZ,_L16
_L18:
	LD	HL,(LENGTH)
	LD	HL,$0001
	PUSH	HL
	POP	DE
	EX	DE,HL
	ADD	HL,DE
	LD	HL,(_LPCT)
	LD	HL,$0001
; array store: t29[t30] = t28
	PUSH	HL
	POP	DE
	LD	(HL),E
	INC	HL
	LD	(HL),D
_EXIT:
	POP	IY
	RET

@MAGIC:
; function @MAGIC
	PUSH	IY
	LD	HL,$000D
	LD	HL,(COM)
	PUSH	HL
	POP	DE
	EX	DE,HL
	ADD	HL,DE
	LD	HL,(_CO)
	LD	HL,$0000
; array store: t3[t4] = t2
	PUSH	HL
	POP	DE
	LD	(HL),E
	INC	HL
	LD	(HL),D
	LD	HL,$000F
	LD	HL,(_CO)
	LD	HL,$0001
; array store: t6[t7] = t5
	PUSH	HL
	POP	DE
	LD	(HL),E
	INC	HL
	LD	(HL),D
	LD	HL,_CO
	LD	(^IX),HL
	CALL	CALLMAGIC
_EXIT:
	POP	IY
	RET

@CRTKN:
; function @CRTKN
	PUSH	IY
	LD	HL,(_DEMODE)
	PUSH	HL
	LD	HL,(_MSCREEN)
	PUSH	HL
	CALL	@MODE
	LD	HL,(_MSCREEN)
	LD	HL,$0000
	PUSH	HL
	POP	DE
	EX	DE,HL
	OR	A
	SBC	HL,DE
	LD	HL,$0000
	JR	Z,$+3
	INC	HL
; if t5 == 0 goto _L22
	LD	A,H
	OR	L
	JP	Z,_L22
	LD	HL,$0000
	LD	(A),HL
	LD	HL,(COLOR)
	LD	(B),HL
	LD	HL,(GR)
	LD	(C),HL
	LD	HL,(COLOR)
	LD	(D),HL
_L22:
	LD	HL,(_MSCREEN)
	LD	HL,$0001
	PUSH	HL
	POP	DE
	EX	DE,HL
	OR	A
	SBC	HL,DE
	LD	HL,$0000
	JR	Z,$+3
	INC	HL
; if t12 == 0 goto _L23
	LD	A,H
	OR	L
	JP	Z,_L23
	LD	HL,(COLOR)
	LD	(A),HL
	LD	HL,$0000
	LD	(B),HL
	LD	HL,(COLOR)
	LD	(C),HL
	LD	HL,(GR)
	LD	(D),HL
_L23:
	LD	HL,(_MSCREEN)
	LD	HL,$0001
	PUSH	HL
	POP	DE
	EX	DE,HL
	LD	A,H
	XOR	D
	LD	H,A
	LD	A,L
	XOR	E
	LD	L,A
	LD	(_MSCREEN),HL
	LD	HL,(PRW)
	LD	HL,$0000
	PUSH	HL
	POP	DE
	EX	DE,HL
	OR	A
	SBC	HL,DE
	LD	HL,$0000
	JR	Z,$+3
	INC	HL
; if t22 == 0 goto _L24
	LD	A,H
	OR	L
	JP	Z,_L24
	LD	HL,$0000
	PUSH	HL
	LD	HL,(A)
	PUSH	HL
	LD	HL,(B)
	PUSH	HL
	LD	HL,(A)
	LD	HL,(B)
	PUSH	HL
	POP	DE
	EX	DE,HL
	ADD	HL,DE
	PUSH	HL
	LD	HL,(GR)
	PUSH	HL
	LD	HL,(C)
	PUSH	HL
	LD	HL,(D)
	PUSH	HL
	LD	HL,(A)
	LD	HL,(B)
	PUSH	HL
	POP	DE
	EX	DE,HL
	ADD	HL,DE
	PUSH	HL
	CALL	@PALET
_L24:
	LD	HL,(PRW)
	LD	HL,$0001
	PUSH	HL
	POP	DE
	EX	DE,HL
	OR	A
	SBC	HL,DE
	LD	HL,$0000
	JR	Z,$+3
	INC	HL
; if t38 == 0 goto _L25
	LD	A,H
	OR	L
	JP	Z,_L25
	LD	HL,$0000
	PUSH	HL
	LD	HL,(A)
	PUSH	HL
	LD	HL,(B)
	PUSH	HL
	LD	HL,(A)
	LD	HL,(B)
	PUSH	HL
	POP	DE
	EX	DE,HL
	ADD	HL,DE
	PUSH	HL
	LD	HL,(GR)
	PUSH	HL
	LD	HL,(GR)
	PUSH	HL
	LD	HL,(GR)
	PUSH	HL
	LD	HL,(GR)
	PUSH	HL
	CALL	@PALET
_L25:
_EXIT:
	POP	IY
	RET

@GRAD:
; function @GRAD
	PUSH	IY
	LD	HL,(GR)
	LD	HL,$0002
	PUSH	HL
	POP	DE
	EX	DE,HL
	CALL	MUL16
	LD	(I),HL
	LD	HL,(_GRDTN)
	LD	HL,(I)
; array load: t6 = t3[t5]
	PUSH	HL
	POP	DE
	EX	DE,HL
	ADD	HL,HL
	ADD	HL,DE
	LD	E,(HL)
	INC	HL
	LD	D,(HL)
	EX	DE,HL
	LD	(_TILE1),HL
	LD	HL,(_GRDTN)
	LD	HL,(I)
	LD	HL,$0001
	PUSH	HL
	POP	DE
	EX	DE,HL
	ADD	HL,DE
; array load: t12 = t7[t11]
	PUSH	HL
	POP	DE
	EX	DE,HL
	ADD	HL,HL
	ADD	HL,DE
	LD	E,(HL)
	INC	HL
	LD	D,(HL)
	EX	DE,HL
	LD	(_TILE2),HL
_EXIT:
	POP	IY
	RET

@ROAD:
; function @ROAD
	PUSH	IY
	LD	HL,(AA)
	LD	HL,$0007
	PUSH	HL
	POP	DE
	EX	DE,HL
	LD	A,H
	AND	D
	LD	H,A
	LD	A,L
	AND	E
	LD	L,A
	LD	(AA),HL
	LD	HL,$0007
	LD	HL,(AA)
	PUSH	HL
	POP	DE
	EX	DE,HL
	OR	A
	SBC	HL,DE
	PUSH	HL
	CALL	@GRAD
	LD	HL,(_ROADY)
	LD	HL,(AA)
; array load: t10 = t7[t9]
	PUSH	HL
	POP	DE
	EX	DE,HL
	ADD	HL,HL
	ADD	HL,DE
	LD	E,(HL)
	INC	HL
	LD	D,(HL)
	EX	DE,HL
	LD	HL,$0000
; array load: t12 = t10[t11]
	PUSH	HL
	POP	DE
	EX	DE,HL
	ADD	HL,HL
	ADD	HL,DE
	LD	E,(HL)
	INC	HL
	LD	D,(HL)
	EX	DE,HL
	LD	(I),HL
	LD	HL,(_ROADY)
	LD	HL,(AA)
; array load: t16 = t13[t15]
	PUSH	HL
	POP	DE
	EX	DE,HL
	ADD	HL,HL
	ADD	HL,DE
	LD	E,(HL)
	INC	HL
	LD	D,(HL)
	EX	DE,HL
	LD	HL,$0001
; array load: t18 = t16[t17]
	PUSH	HL
	POP	DE
	EX	DE,HL
	ADD	HL,HL
	ADD	HL,DE
	LD	E,(HL)
	INC	HL
	LD	D,(HL)
	EX	DE,HL
	LD	(J),HL
	LD	HL,$0000
	PUSH	HL
	LD	HL,(I)
	PUSH	HL
	LD	HL,$027F
	PUSH	HL
	LD	HL,(J)
	PUSH	HL
	CALL	@FULL
	LD	HL,$0000
	PUSH	HL
	LD	HL,$00C7
	LD	HL,(I)
	PUSH	HL
	POP	DE
	EX	DE,HL
	OR	A
	SBC	HL,DE
	PUSH	HL
	LD	HL,$027F
	PUSH	HL
	LD	HL,$00C7
	LD	HL,(J)
	PUSH	HL
	POP	DE
	EX	DE,HL
	OR	A
	SBC	HL,DE
	PUSH	HL
	CALL	@FULL
_EXIT:
	POP	IY
	RET

@FLASH:
; function @FLASH
	PUSH	IY
	LD	HL,$000A
	LD	HL,(_CO)
	LD	HL,$0000
; array store: t1[t2] = t0
	PUSH	HL
	POP	DE
	LD	(HL),E
	INC	HL
	LD	(HL),D
	LD	HL,(_PAL)
	LD	HL,$0000
; array load: t6 = t3[t5]
	PUSH	HL
	POP	DE
	EX	DE,HL
	ADD	HL,HL
	ADD	HL,DE
	LD	E,(HL)
	INC	HL
	LD	D,(HL)
	EX	DE,HL
	LD	HL,(_CO)
	LD	HL,$0001
; array store: t7[t8] = t6
	PUSH	HL
	POP	DE
	LD	(HL),E
	INC	HL
	LD	(HL),D
	LD	HL,(_PAL)
	LD	HL,$0001
; array load: t12 = t9[t11]
	PUSH	HL
	POP	DE
	EX	DE,HL
	ADD	HL,HL
	ADD	HL,DE
	LD	E,(HL)
	INC	HL
	LD	D,(HL)
	EX	DE,HL
	LD	HL,(_CO)
	LD	HL,$0002
; array store: t13[t14] = t12
	PUSH	HL
	POP	DE
	LD	(HL),E
	INC	HL
	LD	(HL),D
	LD	HL,(_PAL)
	LD	HL,$0002
; array load: t18 = t15[t17]
	PUSH	HL
	POP	DE
	EX	DE,HL
	ADD	HL,HL
	ADD	HL,DE
	LD	E,(HL)
	INC	HL
	LD	D,(HL)
	EX	DE,HL
	LD	HL,(_CO)
	LD	HL,$0003
; array store: t19[t20] = t18
	PUSH	HL
	POP	DE
	LD	(HL),E
	INC	HL
	LD	(HL),D
	LD	HL,(_PAL)
	LD	HL,$0003
; array load: t24 = t21[t23]
	PUSH	HL
	POP	DE
	EX	DE,HL
	ADD	HL,HL
	ADD	HL,DE
	LD	E,(HL)
	INC	HL
	LD	D,(HL)
	EX	DE,HL
	LD	HL,(_CO)
	LD	HL,$0004
; array store: t25[t26] = t24
	PUSH	HL
	POP	DE
	LD	(HL),E
	INC	HL
	LD	(HL),D
	LD	HL,(_PAL)
	LD	HL,$0004
; array load: t30 = t27[t29]
	PUSH	HL
	POP	DE
	EX	DE,HL
	ADD	HL,HL
	ADD	HL,DE
	LD	E,(HL)
	INC	HL
	LD	D,(HL)
	EX	DE,HL
	LD	HL,(_CO)
	LD	HL,$0005
; array store: t31[t32] = t30
	PUSH	HL
	POP	DE
	LD	(HL),E
	INC	HL
	LD	(HL),D
	LD	HL,(_PAL)
	LD	HL,$0005
; array load: t36 = t33[t35]
	PUSH	HL
	POP	DE
	EX	DE,HL
	ADD	HL,HL
	ADD	HL,DE
	LD	E,(HL)
	INC	HL
	LD	D,(HL)
	EX	DE,HL
	LD	HL,(_CO)
	LD	HL,$0006
; array store: t37[t38] = t36
	PUSH	HL
	POP	DE
	LD	(HL),E
	INC	HL
	LD	(HL),D
	LD	HL,(_PAL)
	LD	HL,$0006
; array load: t42 = t39[t41]
	PUSH	HL
	POP	DE
	EX	DE,HL
	ADD	HL,HL
	ADD	HL,DE
	LD	E,(HL)
	INC	HL
	LD	D,(HL)
	EX	DE,HL
	LD	HL,(_CO)
	LD	HL,$0007
; array store: t43[t44] = t42
	PUSH	HL
	POP	DE
	LD	(HL),E
	INC	HL
	LD	(HL),D
	LD	HL,(_PAL)
	LD	HL,$0007
; array load: t48 = t45[t47]
	PUSH	HL
	POP	DE
	EX	DE,HL
	ADD	HL,HL
	ADD	HL,DE
	LD	E,(HL)
	INC	HL
	LD	D,(HL)
	EX	DE,HL
	LD	HL,(_CO)
	LD	HL,$0008
; array store: t49[t50] = t48
	PUSH	HL
	POP	DE
	LD	(HL),E
	INC	HL
	LD	(HL),D
	LD	HL,(MEM)
	LD	HL,C2
; array load: t54 = t51[t53]
	PUSH	HL
	POP	DE
	EX	DE,HL
	ADD	HL,HL
	ADD	HL,DE
	LD	E,(HL)
	INC	HL
	LD	D,(HL)
	EX	DE,HL
	LD	HL,(_CO)
	LD	HL,(C1)
	LD	HL,$0001
	PUSH	HL
	POP	DE
	EX	DE,HL
	ADD	HL,DE
; array store: t55[t58] = t54
	PUSH	HL
	POP	DE
	LD	(HL),E
	INC	HL
	LD	(HL),D
	LD	HL,$000F
	LD	HL,(_CO)
	LD	HL,$0009
; array store: t60[t61] = t59
	PUSH	HL
	POP	DE
	LD	(HL),E
	INC	HL
	LD	(HL),D
	LD	HL,(C2)
	LD	HL,(_PAL)
	LD	HL,(C1)
; array store: t63[t64] = t62
	PUSH	HL
	POP	DE
	LD	(HL),E
	INC	HL
	LD	(HL),D
	LD	HL,_CO
	LD	(^IX),HL
	CALL	CALLMAGIC
_EXIT:
	POP	IY
	RET

@PLINIT:
; function @PLINIT
	PUSH	IY
	LD	HL,$0002
	PUSH	HL
	CALL	PRMODE
; load string "H"
	LD	HL,0 ; string placeholder
	CALL	__print_str
; load string "N"
	LD	HL,0 ; string placeholder
	CALL	__print_str
; load string "J1"
	LD	HL,0 ; string placeholder
	CALL	__print_str
; load string "N"
	LD	HL,0 ; string placeholder
	CALL	__print_str
	LD	HL,$0000
	PUSH	HL
	CALL	PRMODE
_EXIT:
	POP	IY
	RET

@PLINE:
; function @PLINE
	PUSH	IY
	LD	HL,$0002
	PUSH	HL
	CALL	PRMODE
; load string "M "
	LD	HL,0 ; string placeholder
	CALL	__print_str
	LD	HL,(X1)
	LD	HL,$013F
	PUSH	HL
	POP	DE
	EX	DE,HL
	OR	A
	SBC	HL,DE
	LD	HL,$0005
	PUSH	HL
	POP	DE
	EX	DE,HL
	CALL	MUL16
	LD	HL,$07D0
	PUSH	HL
	POP	DE
	EX	DE,HL
	ADD	HL,DE
	CALL	__print_num
; load string ","
	LD	HL,0 ; string placeholder
	CALL	__print_str
	LD	HL,$03E8
	LD	HL,(Y1)
	LD	HL,$0063
	PUSH	HL
	POP	DE
	EX	DE,HL
	OR	A
	SBC	HL,DE
	LD	HL,$000A
	PUSH	HL
	POP	DE
	EX	DE,HL
	CALL	MUL16
	PUSH	HL
	POP	DE
	EX	DE,HL
	OR	A
	SBC	HL,DE
	CALL	__print_num
; load string "N"
	LD	HL,0 ; string placeholder
	CALL	__print_str
; load string "D "
	LD	HL,0 ; string placeholder
	CALL	__print_str
	LD	HL,(X2)
	LD	HL,$013F
	PUSH	HL
	POP	DE
	EX	DE,HL
	OR	A
	SBC	HL,DE
	LD	HL,$0005
	PUSH	HL
	POP	DE
	EX	DE,HL
	CALL	MUL16
	LD	HL,$07D0
	PUSH	HL
	POP	DE
	EX	DE,HL
	ADD	HL,DE
	CALL	__print_num
; load string ","
	LD	HL,0 ; string placeholder
	CALL	__print_str
	LD	HL,$03E8
	LD	HL,(Y2)
	LD	HL,$0063
	PUSH	HL
	POP	DE
	EX	DE,HL
	OR	A
	SBC	HL,DE
	LD	HL,$000A
	PUSH	HL
	POP	DE
	EX	DE,HL
	CALL	MUL16
	PUSH	HL
	POP	DE
	EX	DE,HL
	OR	A
	SBC	HL,DE
	CALL	__print_num
; load string "N"
	LD	HL,0 ; string placeholder
	CALL	__print_str
	LD	HL,$0000
	PUSH	HL
	CALL	PRMODE
_EXIT:
	POP	IY
	RET

@PLOT:
; function @PLOT
	PUSH	IY
	CALL	@PLINIT
	LD	HL,$0000
	LD	(I),HL
_L26:
	LD	HL,(_WIRE)
	LD	HL,(I)
; array load: t5 = t2[t4]
	PUSH	HL
	POP	DE
	EX	DE,HL
	ADD	HL,HL
	ADD	HL,DE
	LD	E,(HL)
	INC	HL
	LD	D,(HL)
	EX	DE,HL
	LD	HL,$0000
; array load: t7 = t5[t6]
	PUSH	HL
	POP	DE
	EX	DE,HL
	ADD	HL,HL
	ADD	HL,DE
	LD	E,(HL)
	INC	HL
	LD	D,(HL)
	EX	DE,HL
	LD	(J),HL
	LD	HL,(_WIRE)
	LD	HL,(I)
; array load: t11 = t8[t10]
	PUSH	HL
	POP	DE
	EX	DE,HL
	ADD	HL,HL
	ADD	HL,DE
	LD	E,(HL)
	INC	HL
	LD	D,(HL)
	EX	DE,HL
	LD	HL,$0001
; array load: t13 = t11[t12]
	PUSH	HL
	POP	DE
	EX	DE,HL
	ADD	HL,HL
	ADD	HL,DE
	LD	E,(HL)
	INC	HL
	LD	D,(HL)
	EX	DE,HL
	LD	(K),HL
	LD	HL,(_DISP)
	LD	HL,(J)
; array load: t17 = t14[t16]
	PUSH	HL
	POP	DE
	EX	DE,HL
	ADD	HL,HL
	ADD	HL,DE
	LD	E,(HL)
	INC	HL
	LD	D,(HL)
	EX	DE,HL
	LD	HL,$0000
; array load: t19 = t17[t18]
	PUSH	HL
	POP	DE
	EX	DE,HL
	ADD	HL,HL
	ADD	HL,DE
	LD	E,(HL)
	INC	HL
	LD	D,(HL)
	EX	DE,HL
	PUSH	HL
	LD	HL,(_DISP)
	LD	HL,(J)
; array load: t23 = t20[t22]
	PUSH	HL
	POP	DE
	EX	DE,HL
	ADD	HL,HL
	ADD	HL,DE
	LD	E,(HL)
	INC	HL
	LD	D,(HL)
	EX	DE,HL
	LD	HL,$0001
; array load: t25 = t23[t24]
	PUSH	HL
	POP	DE
	EX	DE,HL
	ADD	HL,HL
	ADD	HL,DE
	LD	E,(HL)
	INC	HL
	LD	D,(HL)
	EX	DE,HL
	PUSH	HL
	LD	HL,(_DISP)
	LD	HL,(K)
; array load: t29 = t26[t28]
	PUSH	HL
	POP	DE
	EX	DE,HL
	ADD	HL,HL
	ADD	HL,DE
	LD	E,(HL)
	INC	HL
	LD	D,(HL)
	EX	DE,HL
	LD	HL,$0000
; array load: t31 = t29[t30]
	PUSH	HL
	POP	DE
	EX	DE,HL
	ADD	HL,HL
	ADD	HL,DE
	LD	E,(HL)
	INC	HL
	LD	D,(HL)
	EX	DE,HL
	PUSH	HL
	LD	HL,(_DISP)
	LD	HL,(K)
; array load: t35 = t32[t34]
	PUSH	HL
	POP	DE
	EX	DE,HL
	ADD	HL,HL
	ADD	HL,DE
	LD	E,(HL)
	INC	HL
	LD	D,(HL)
	EX	DE,HL
	LD	HL,$0001
; array load: t37 = t35[t36]
	PUSH	HL
	POP	DE
	EX	DE,HL
	ADD	HL,HL
	ADD	HL,DE
	LD	E,(HL)
	INC	HL
	LD	D,(HL)
	EX	DE,HL
	PUSH	HL
	CALL	@PLINE
_L27:
	LD	HL,(I)
	LD	HL,$0001
	PUSH	HL
	POP	DE
	EX	DE,HL
	ADD	HL,DE
	LD	(I),HL
	LD	HL,(_LPCT)
	LD	HL,$0001
; array load: t45 = t42[t44]
	PUSH	HL
	POP	DE
	EX	DE,HL
	ADD	HL,HL
	ADD	HL,DE
	LD	E,(HL)
	INC	HL
	LD	D,(HL)
	EX	DE,HL
	LD	HL,$0001
	PUSH	HL
	POP	DE
	EX	DE,HL
	OR	A
	SBC	HL,DE
	PUSH	HL
	POP	DE
	EX	DE,HL
	EX	DE,HL
	OR	A
	SBC	HL,DE
	LD	HL,$0000
	JR	C,$+3
	INC	HL
	LD	A,L
	XOR	$01
	LD	L,A
; if t48 != 0 goto _L26
	LD	A,H
	OR	L
	JP	NZ,_L26
_L28:
_EXIT:
	POP	IY
	RET

@TLINE:
; function @TLINE
	PUSH	IY
	LD	HL,$0000
	LD	(I),HL
_L29:
	LD	HL,(_PAR)
	LD	HL,(I)
; array load: t4 = t1[t3]
	PUSH	HL
	POP	DE
	EX	DE,HL
	ADD	HL,HL
	ADD	HL,DE
	LD	E,(HL)
	INC	HL
	LD	D,(HL)
	EX	DE,HL
	LD	HL,(_BUFPAR)
	LD	HL,(I)
; array store: t5[t6] = t4
	PUSH	HL
	POP	DE
	LD	(HL),E
	INC	HL
	LD	(HL),D
_L30:
	LD	HL,(I)
	LD	HL,$0001
	PUSH	HL
	POP	DE
	EX	DE,HL
	ADD	HL,DE
	LD	(I),HL
	LD	HL,$0008
	PUSH	HL
	POP	DE
	EX	DE,HL
	EX	DE,HL
	OR	A
	SBC	HL,DE
	LD	HL,$0000
	JR	C,$+3
	INC	HL
	LD	A,L
	XOR	$01
	LD	L,A
; if t11 != 0 goto _L29
	LD	A,H
	OR	L
	JP	NZ,_L29
_L31:
	LD	HL,(XS)
	LD	HL,(MEMW)
	LD	HL,_ZAHYO
; array store: t13[t14] = t12
	PUSH	HL
	POP	DE
	LD	(HL),E
	INC	HL
	LD	(HL),D
	LD	HL,(YS)
	LD	HL,(MEMW)
	LD	HL,_ZAHYO
	LD	HL,$0002
	PUSH	HL
	POP	DE
	EX	DE,HL
	ADD	HL,DE
; array store: t16[t19] = t15
	PUSH	HL
	POP	DE
	LD	(HL),E
	INC	HL
	LD	(HL),D
	LD	HL,(ZS)
	LD	HL,(MEMW)
	LD	HL,_ZAHYO
	LD	HL,$0004
	PUSH	HL
	POP	DE
	EX	DE,HL
	ADD	HL,DE
; array store: t21[t24] = t20
	PUSH	HL
	POP	DE
	LD	(HL),E
	INC	HL
	LD	(HL),D
	LD	HL,(XE)
	LD	HL,(MEMW)
	LD	HL,_ZAHYO
	LD	HL,$0006
	PUSH	HL
	POP	DE
	EX	DE,HL
	ADD	HL,DE
; array store: t26[t29] = t25
	PUSH	HL
	POP	DE
	LD	(HL),E
	INC	HL
	LD	(HL),D
	LD	HL,(YE)
	LD	HL,(MEMW)
	LD	HL,_ZAHYO
	LD	HL,$0008
	PUSH	HL
	POP	DE
	EX	DE,HL
	ADD	HL,DE
; array store: t31[t34] = t30
	PUSH	HL
	POP	DE
	LD	(HL),E
	INC	HL
	LD	(HL),D
	LD	HL,(ZE)
	LD	HL,(MEMW)
	LD	HL,_ZAHYO
	LD	HL,$000A
	PUSH	HL
	POP	DE
	EX	DE,HL
	ADD	HL,DE
; array store: t36[t39] = t35
	PUSH	HL
	POP	DE
	LD	(HL),E
	INC	HL
	LD	(HL),D
	LD	HL,$0000
	LD	HL,(_WIRE)
	LD	HL,$0000
; array load: t43 = t41[t42]
	PUSH	HL
	POP	DE
	EX	DE,HL
	ADD	HL,HL
	ADD	HL,DE
	LD	E,(HL)
	INC	HL
	LD	D,(HL)
	EX	DE,HL
	LD	HL,$0000
; array store: t43[t44] = t40
	PUSH	HL
	POP	DE
	LD	(HL),E
	INC	HL
	LD	(HL),D
	LD	HL,$0001
	LD	HL,(_WIRE)
	LD	HL,$0000
; array load: t48 = t46[t47]
	PUSH	HL
	POP	DE
	EX	DE,HL
	ADD	HL,HL
	ADD	HL,DE
	LD	E,(HL)
	INC	HL
	LD	D,(HL)
	EX	DE,HL
	LD	HL,$0001
; array store: t48[t49] = t45
	PUSH	HL
	POP	DE
	LD	(HL),E
	INC	HL
	LD	(HL),D
	LD	HL,$0002
	LD	HL,(_LPCT)
	LD	HL,$0000
; array store: t51[t52] = t50
	PUSH	HL
	POP	DE
	LD	(HL),E
	INC	HL
	LD	(HL),D
	LD	HL,$0001
	LD	HL,(_LPCT)
	LD	HL,$0001
; array store: t54[t55] = t53
	PUSH	HL
	POP	DE
	LD	(HL),E
	INC	HL
	LD	(HL),D
	LD	HL,(_OFSX)
	LD	HL,(_PAR)
	LD	HL,$0000
; array store: t57[t58] = t56
	PUSH	HL
	POP	DE
	LD	(HL),E
	INC	HL
	LD	(HL),D
	LD	HL,(_OFSY)
	LD	HL,(_PAR)
	LD	HL,$0001
; array store: t60[t61] = t59
	PUSH	HL
	POP	DE
	LD	(HL),E
	INC	HL
	LD	(HL),D
	LD	HL,(_OFSZ)
	LD	HL,(_PAR)
	LD	HL,$0002
; array store: t63[t64] = t62
	PUSH	HL
	POP	DE
	LD	(HL),E
	INC	HL
	LD	(HL),D
	LD	HL,$0000
	LD	HL,(_PAR)
	LD	HL,$0003
; array store: t66[t67] = t65
	PUSH	HL
	POP	DE
	LD	(HL),E
	INC	HL
	LD	(HL),D
	LD	HL,$0000
	LD	HL,(_PAR)
	LD	HL,$0004
; array store: t69[t70] = t68
	PUSH	HL
	POP	DE
	LD	(HL),E
	INC	HL
	LD	(HL),D
	LD	HL,$0000
	LD	HL,(_PAR)
	LD	HL,$0005
; array store: t72[t73] = t71
	PUSH	HL
	POP	DE
	LD	(HL),E
	INC	HL
	LD	(HL),D
	LD	HL,(_SHEAD)
	LD	HL,(_PAR)
	LD	HL,(_HEAD)
; array store: t75[t76] = t74
	PUSH	HL
	POP	DE
	LD	(HL),E
	INC	HL
	LD	(HL),D
	LD	HL,(_SPITCH)
	LD	HL,(_PAR)
	LD	HL,(_PITCH)
; array store: t78[t79] = t77
	PUSH	HL
	POP	DE
	LD	(HL),E
	INC	HL
	LD	(HL),D
	LD	HL,(_SBANK)
	LD	HL,(_PAR)
	LD	HL,(_BANK)
; array store: t81[t82] = t80
	PUSH	HL
	POP	DE
	LD	(HL),E
	INC	HL
	LD	(HL),D
	LD	HL,$0000
	PUSH	HL
	CALL	@MAGIC
	LD	HL,$0001
	PUSH	HL
	CALL	@MAGIC
	LD	HL,(_BSWITCH)
	LD	HL,$0001
	PUSH	HL
	POP	DE
	EX	DE,HL
	OR	A
	SBC	HL,DE
	LD	HL,$0000
	JR	Z,$+3
	INC	HL
; if t89 == 0 goto _L32
	LD	A,H
	OR	L
	JP	Z,_L32
	CALL	@FUKUGEN
_L32:
	LD	HL,$0000
	LD	(I),HL
_L33:
	LD	HL,(_BUFPAR)
	LD	HL,(I)
; array load: t95 = t92[t94]
	PUSH	HL
	POP	DE
	EX	DE,HL
	ADD	HL,HL
	ADD	HL,DE
	LD	E,(HL)
	INC	HL
	LD	D,(HL)
	EX	DE,HL
	LD	HL,(_PAR)
	LD	HL,(I)
; array store: t96[t97] = t95
	PUSH	HL
	POP	DE
	LD	(HL),E
	INC	HL
	LD	(HL),D
_L34:
	LD	HL,(I)
	LD	HL,$0001
	PUSH	HL
	POP	DE
	EX	DE,HL
	ADD	HL,DE
	LD	(I),HL
	LD	HL,$0008
	PUSH	HL
	POP	DE
	EX	DE,HL
	EX	DE,HL
	OR	A
	SBC	HL,DE
	LD	HL,$0000
	JR	C,$+3
	INC	HL
	LD	A,L
	XOR	$01
	LD	L,A
; if t102 != 0 goto _L33
	LD	A,H
	OR	L
	JP	NZ,_L33
_L35:
_EXIT:
	POP	IY
	RET

@FUKUGEN:
; function @FUKUGEN
	PUSH	IY
	LD	HL,(_OBFAD)
	PUSH	HL
	LD	HL,$0001
	PUSH	HL
	CALL	@SETOD
	LD	HL,(_WBFAD)
	PUSH	HL
	LD	HL,$0000
	PUSH	HL
	CALL	@SETWD
	LD	HL,(_OBUFF)
	LD	HL,(_LPCT)
	LD	HL,$0000
; array store: t7[t8] = t6
	PUSH	HL
	POP	DE
	LD	(HL),E
	INC	HL
	LD	(HL),D
	LD	HL,(_WBUFF)
	LD	HL,(_LPCT)
	LD	HL,$0001
; array store: t10[t11] = t9
	PUSH	HL
	POP	DE
	LD	(HL),E
	INC	HL
	LD	(HL),D
	LD	HL,(_BSWITCH)
	LD	HL,$0001
	PUSH	HL
	POP	DE
	EX	DE,HL
	OR	A
	SBC	HL,DE
	LD	HL,$0000
	JR	Z,$+3
	INC	HL
; if t14 == 0 goto _L36
	LD	A,H
	OR	L
	JP	Z,_L36
	LD	HL,$0000
	LD	(I),HL
_L37:
	LD	HL,(_BUFPAR)
	LD	HL,(I)
; array load: t19 = t16[t18]
	PUSH	HL
	POP	DE
	EX	DE,HL
	ADD	HL,HL
	ADD	HL,DE
	LD	E,(HL)
	INC	HL
	LD	D,(HL)
	EX	DE,HL
	LD	HL,(_PAR)
	LD	HL,(I)
; array store: t20[t21] = t19
	PUSH	HL
	POP	DE
	LD	(HL),E
	INC	HL
	LD	(HL),D
_L38:
	LD	HL,(I)
	LD	HL,$0001
	PUSH	HL
	POP	DE
	EX	DE,HL
	ADD	HL,DE
	LD	(I),HL
	LD	HL,$0008
	PUSH	HL
	POP	DE
	EX	DE,HL
	EX	DE,HL
	OR	A
	SBC	HL,DE
	LD	HL,$0000
	JR	C,$+3
	INC	HL
	LD	A,L
	XOR	$01
	LD	L,A
; if t26 != 0 goto _L37
	LD	A,H
	OR	L
	JP	NZ,_L37
_L39:
_L36:
_EXIT:
	POP	IY
	RET

@CLINE:
; function @CLINE
	PUSH	IY
	LD	HL,(C)
	LD	HL,$0001
	PUSH	HL
	POP	DE
	EX	DE,HL
	LD	A,H
	AND	D
	LD	H,A
	LD	A,L
	AND	E
	LD	L,A
	LD	HL,$0001
	PUSH	HL
	POP	DE
	EX	DE,HL
	OR	A
	SBC	HL,DE
	LD	HL,$0000
	JR	Z,$+3
	INC	HL
; if t4 == 0 goto _L41
	LD	A,H
	OR	L
	JP	Z,_L41
	LD	HL,(_DEMODE)
	PUSH	HL
	LD	HL,$0000
	PUSH	HL
	CALL	@MODE
	LD	HL,(X1)
	PUSH	HL
	LD	HL,(Y1)
	PUSH	HL
	LD	HL,(X2)
	PUSH	HL
	LD	HL,(Y2)
	PUSH	HL
	CALL	@LINE
	JP	_L40
_L41:
	LD	HL,$0002
	LD	HL,(_DEMODE)
	PUSH	HL
	POP	DE
	EX	DE,HL
	OR	A
	SBC	HL,DE
	PUSH	HL
	LD	HL,$0000
	PUSH	HL
	CALL	@MODE
	LD	HL,(X1)
	PUSH	HL
	LD	HL,(Y1)
	PUSH	HL
	LD	HL,(X2)
	PUSH	HL
	LD	HL,(Y2)
	PUSH	HL
	CALL	@LINE
_L40:
	LD	HL,(C)
	LD	HL,$0002
	PUSH	HL
	POP	DE
	EX	DE,HL
	LD	A,H
	AND	D
	LD	H,A
	LD	A,L
	AND	E
	LD	L,A
	LD	HL,$0002
	PUSH	HL
	POP	DE
	EX	DE,HL
	OR	A
	SBC	HL,DE
	LD	HL,$0000
	JR	Z,$+3
	INC	HL
; if t27 == 0 goto _L43
	LD	A,H
	OR	L
	JP	Z,_L43
	LD	HL,(_DEMODE)
	PUSH	HL
	LD	HL,$0001
	PUSH	HL
	CALL	@MODE
	LD	HL,(X1)
	PUSH	HL
	LD	HL,(Y1)
	PUSH	HL
	LD	HL,(X2)
	PUSH	HL
	LD	HL,(Y2)
	PUSH	HL
	CALL	@LINE
	JP	_L42
_L43:
	LD	HL,$0002
	LD	HL,(_DEMODE)
	PUSH	HL
	POP	DE
	EX	DE,HL
	OR	A
	SBC	HL,DE
	PUSH	HL
	LD	HL,$0001
	PUSH	HL
	CALL	@MODE
	LD	HL,(X1)
	PUSH	HL
	LD	HL,(Y1)
	PUSH	HL
	LD	HL,(X2)
	PUSH	HL
	LD	HL,(Y2)
	PUSH	HL
	CALL	@LINE
_L42:
	LD	HL,(C)
	LD	HL,$0004
	PUSH	HL
	POP	DE
	EX	DE,HL
	LD	A,H
	AND	D
	LD	H,A
	LD	A,L
	AND	E
	LD	L,A
	LD	HL,$0004
	PUSH	HL
	POP	DE
	EX	DE,HL
	OR	A
	SBC	HL,DE
	LD	HL,$0000
	JR	Z,$+3
	INC	HL
; if t50 == 0 goto _L45
	LD	A,H
	OR	L
	JP	Z,_L45
	LD	HL,(_DEMODE)
	PUSH	HL
	LD	HL,$0002
	PUSH	HL
	CALL	@MODE
	LD	HL,(X1)
	PUSH	HL
	LD	HL,(Y1)
	PUSH	HL
	LD	HL,(X2)
	PUSH	HL
	LD	HL,(Y2)
	PUSH	HL
	CALL	@LINE
	JP	_L44
_L45:
	LD	HL,$0002
	LD	HL,(_DEMODE)
	PUSH	HL
	POP	DE
	EX	DE,HL
	OR	A
	SBC	HL,DE
	PUSH	HL
	LD	HL,$0002
	PUSH	HL
	CALL	@MODE
	LD	HL,(X1)
	PUSH	HL
	LD	HL,(Y1)
	PUSH	HL
	LD	HL,(X2)
	PUSH	HL
	LD	HL,(Y2)
	PUSH	HL
	CALL	@LINE
_L44:
_EXIT:
	POP	IY
	RET

@CBOX:
; function @CBOX
	PUSH	IY
	LD	HL,(C)
	LD	HL,$0001
	PUSH	HL
	POP	DE
	EX	DE,HL
	LD	A,H
	AND	D
	LD	H,A
	LD	A,L
	AND	E
	LD	L,A
	LD	HL,$0001
	PUSH	HL
	POP	DE
	EX	DE,HL
	OR	A
	SBC	HL,DE
	LD	HL,$0000
	JR	Z,$+3
	INC	HL
; if t4 == 0 goto _L47
	LD	A,H
	OR	L
	JP	Z,_L47
	LD	HL,(_DEMODE)
	PUSH	HL
	LD	HL,$0000
	PUSH	HL
	CALL	@MODE
	LD	HL,(X1)
	PUSH	HL
	LD	HL,(Y1)
	PUSH	HL
	LD	HL,(X2)
	PUSH	HL
	LD	HL,(Y2)
	PUSH	HL
	CALL	@BOX
	JP	_L46
_L47:
	LD	HL,$0002
	LD	HL,(_DEMODE)
	PUSH	HL
	POP	DE
	EX	DE,HL
	OR	A
	SBC	HL,DE
	PUSH	HL
	LD	HL,$0000
	PUSH	HL
	CALL	@MODE
	LD	HL,(X1)
	PUSH	HL
	LD	HL,(Y1)
	PUSH	HL
	LD	HL,(X2)
	PUSH	HL
	LD	HL,(Y2)
	PUSH	HL
	CALL	@BOX
_L46:
	LD	HL,(C)
	LD	HL,$0002
	PUSH	HL
	POP	DE
	EX	DE,HL
	LD	A,H
	AND	D
	LD	H,A
	LD	A,L
	AND	E
	LD	L,A
	LD	HL,$0002
	PUSH	HL
	POP	DE
	EX	DE,HL
	OR	A
	SBC	HL,DE
	LD	HL,$0000
	JR	Z,$+3
	INC	HL
; if t27 == 0 goto _L49
	LD	A,H
	OR	L
	JP	Z,_L49
	LD	HL,(_DEMODE)
	PUSH	HL
	LD	HL,$0001
	PUSH	HL
	CALL	@MODE
	LD	HL,(X1)
	PUSH	HL
	LD	HL,(Y1)
	PUSH	HL
	LD	HL,(X2)
	PUSH	HL
	LD	HL,(Y2)
	PUSH	HL
	CALL	@BOX
	JP	_L48
_L49:
	LD	HL,$0002
	LD	HL,(_DEMODE)
	PUSH	HL
	POP	DE
	EX	DE,HL
	OR	A
	SBC	HL,DE
	PUSH	HL
	LD	HL,$0001
	PUSH	HL
	CALL	@MODE
	LD	HL,(X1)
	PUSH	HL
	LD	HL,(Y1)
	PUSH	HL
	LD	HL,(X2)
	PUSH	HL
	LD	HL,(Y2)
	PUSH	HL
	CALL	@BOX
_L48:
	LD	HL,(C)
	LD	HL,$0004
	PUSH	HL
	POP	DE
	EX	DE,HL
	LD	A,H
	AND	D
	LD	H,A
	LD	A,L
	AND	E
	LD	L,A
	LD	HL,$0004
	PUSH	HL
	POP	DE
	EX	DE,HL
	OR	A
	SBC	HL,DE
	LD	HL,$0000
	JR	Z,$+3
	INC	HL
; if t50 == 0 goto _L51
	LD	A,H
	OR	L
	JP	Z,_L51
	LD	HL,(_DEMODE)
	PUSH	HL
	LD	HL,$0002
	PUSH	HL
	CALL	@MODE
	LD	HL,(X1)
	PUSH	HL
	LD	HL,(Y1)
	PUSH	HL
	LD	HL,(X2)
	PUSH	HL
	LD	HL,(Y2)
	PUSH	HL
	CALL	@BOX
	JP	_L50
_L51:
	LD	HL,$0002
	LD	HL,(_DEMODE)
	PUSH	HL
	POP	DE
	EX	DE,HL
	OR	A
	SBC	HL,DE
	PUSH	HL
	LD	HL,$0002
	PUSH	HL
	CALL	@MODE
	LD	HL,(X1)
	PUSH	HL
	LD	HL,(Y1)
	PUSH	HL
	LD	HL,(X2)
	PUSH	HL
	LD	HL,(Y2)
	PUSH	HL
	CALL	@BOX
_L50:
_EXIT:
	POP	IY
	RET

@CFULL:
; function @CFULL
	PUSH	IY
	LD	HL,(_GMASK)
	LD	HL,$0001
	PUSH	HL
	POP	DE
	EX	DE,HL
	LD	A,H
	AND	D
	LD	H,A
	LD	A,L
	AND	E
	LD	L,A
	LD	HL,$0001
	PUSH	HL
	POP	DE
	EX	DE,HL
	OR	A
	SBC	HL,DE
	LD	HL,$0000
	JR	Z,$+3
	INC	HL
; if t4 == 0 goto _L52
	LD	A,H
	OR	L
	JP	Z,_L52
	LD	HL,$0002
	PUSH	HL
	LD	HL,$0000
	PUSH	HL
	CALL	@MODE
	LD	HL,(C1)
	PUSH	HL
	CALL	@GRAD
	LD	HL,(X1)
	PUSH	HL
	LD	HL,(Y1)
	PUSH	HL
	LD	HL,(X2)
	PUSH	HL
	LD	HL,(Y2)
	PUSH	HL
	CALL	@FULL
_L52:
	LD	HL,(_GMASK)
	LD	HL,$0002
	PUSH	HL
	POP	DE
	EX	DE,HL
	LD	A,H
	AND	D
	LD	H,A
	LD	A,L
	AND	E
	LD	L,A
	LD	HL,$0002
	PUSH	HL
	POP	DE
	EX	DE,HL
	OR	A
	SBC	HL,DE
	LD	HL,$0000
	JR	Z,$+3
	INC	HL
; if t19 == 0 goto _L53
	LD	A,H
	OR	L
	JP	Z,_L53
	LD	HL,$0002
	PUSH	HL
	LD	HL,$0001
	PUSH	HL
	CALL	@MODE
	LD	HL,(C2)
	PUSH	HL
	CALL	@GRAD
	LD	HL,(X1)
	PUSH	HL
	LD	HL,(Y1)
	PUSH	HL
	LD	HL,(X2)
	PUSH	HL
	LD	HL,(Y2)
	PUSH	HL
	CALL	@FULL
_L53:
	LD	HL,(_GMASK)
	LD	HL,$0004
	PUSH	HL
	POP	DE
	EX	DE,HL
	LD	A,H
	AND	D
	LD	H,A
	LD	A,L
	AND	E
	LD	L,A
	LD	HL,$0004
	PUSH	HL
	POP	DE
	EX	DE,HL
	OR	A
	SBC	HL,DE
	LD	HL,$0000
	JR	Z,$+3
	INC	HL
; if t34 == 0 goto _L54
	LD	A,H
	OR	L
	JP	Z,_L54
	LD	HL,$0002
	PUSH	HL
	LD	HL,$0002
	PUSH	HL
	CALL	@MODE
	LD	HL,(C3)
	PUSH	HL
	CALL	@GRAD
	LD	HL,(X1)
	PUSH	HL
	LD	HL,(Y1)
	PUSH	HL
	LD	HL,(X2)
	PUSH	HL
	LD	HL,(Y2)
	PUSH	HL
	CALL	@FULL
_L54:
_EXIT:
	POP	IY
	RET

@CPOLY:
; function @CPOLY
	PUSH	IY
; ARRAY JO[SLANGCompiler.Parser.Ast.IntegerLiteral,SLANGCompiler.Parser.Ast.IntegerLiteral]
;   = {initial code}
	LD	HL,$0000
	LD	(I),HL
_L55:
	LD	HL,(_PAR)
	LD	HL,(I)
; array load: t4 = t1[t3]
	PUSH	HL
	POP	DE
	EX	DE,HL
	ADD	HL,HL
	ADD	HL,DE
	LD	E,(HL)
	INC	HL
	LD	D,(HL)
	EX	DE,HL
	LD	HL,(_BUFPAR)
	LD	HL,(I)
; array store: t5[t6] = t4
	PUSH	HL
	POP	DE
	LD	(HL),E
	INC	HL
	LD	(HL),D
_L56:
	LD	HL,(I)
	LD	HL,$0001
	PUSH	HL
	POP	DE
	EX	DE,HL
	ADD	HL,DE
	LD	(I),HL
	LD	HL,$0008
	PUSH	HL
	POP	DE
	EX	DE,HL
	EX	DE,HL
	OR	A
	SBC	HL,DE
	LD	HL,$0000
	JR	C,$+3
	INC	HL
	LD	A,L
	XOR	$01
	LD	L,A
; if t11 != 0 goto _L55
	LD	A,H
	OR	L
	JP	NZ,_L55
_L57:
	LD	HL,(_OFSX)
	LD	HL,(_PAR)
	LD	HL,$0000
; array store: t13[t14] = t12
	PUSH	HL
	POP	DE
	LD	(HL),E
	INC	HL
	LD	(HL),D
	LD	HL,(_OFSY)
	LD	HL,(_PAR)
	LD	HL,$0001
; array store: t16[t17] = t15
	PUSH	HL
	POP	DE
	LD	(HL),E
	INC	HL
	LD	(HL),D
	LD	HL,(_OFSZ)
	LD	HL,(_PAR)
	LD	HL,$0002
; array store: t19[t20] = t18
	PUSH	HL
	POP	DE
	LD	(HL),E
	INC	HL
	LD	(HL),D
	LD	HL,$0000
	LD	HL,(_PAR)
	LD	HL,$0003
; array store: t22[t23] = t21
	PUSH	HL
	POP	DE
	LD	(HL),E
	INC	HL
	LD	(HL),D
	LD	HL,$0000
	LD	HL,(_PAR)
	LD	HL,$0004
; array store: t25[t26] = t24
	PUSH	HL
	POP	DE
	LD	(HL),E
	INC	HL
	LD	(HL),D
	LD	HL,$0000
	LD	HL,(_PAR)
	LD	HL,$0005
; array store: t28[t29] = t27
	PUSH	HL
	POP	DE
	LD	(HL),E
	INC	HL
	LD	(HL),D
	LD	HL,(_SHEAD)
	LD	HL,(_PAR)
	LD	HL,(_HEAD)
; array store: t31[t32] = t30
	PUSH	HL
	POP	DE
	LD	(HL),E
	INC	HL
	LD	(HL),D
	LD	HL,(_SPITCH)
	LD	HL,(_PAR)
	LD	HL,(_PITCH)
; array store: t34[t35] = t33
	PUSH	HL
	POP	DE
	LD	(HL),E
	INC	HL
	LD	(HL),D
	LD	HL,(_SBANK)
	LD	HL,(_PAR)
	LD	HL,(_BANK)
; array store: t37[t38] = t36
	PUSH	HL
	POP	DE
	LD	(HL),E
	INC	HL
	LD	(HL),D
	LD	HL,$0000
	LD	(I),HL
_L58:
	LD	HL,(XV)
	LD	HL,(I)
; array load: t43 = t40[t42]
	PUSH	HL
	POP	DE
	EX	DE,HL
	ADD	HL,HL
	ADD	HL,DE
	LD	E,(HL)
	INC	HL
	LD	D,(HL)
	EX	DE,HL
	LD	HL,(_ZAHYO)
	LD	HL,(I)
; array load: t46 = t44[t45]
	PUSH	HL
	POP	DE
	EX	DE,HL
	ADD	HL,HL
	ADD	HL,DE
	LD	E,(HL)
	INC	HL
	LD	D,(HL)
	EX	DE,HL
	LD	HL,$0000
; array store: t46[t47] = t43
	PUSH	HL
	POP	DE
	LD	(HL),E
	INC	HL
	LD	(HL),D
	LD	HL,(YV)
	LD	HL,(I)
; array load: t51 = t48[t50]
	PUSH	HL
	POP	DE
	EX	DE,HL
	ADD	HL,HL
	ADD	HL,DE
	LD	E,(HL)
	INC	HL
	LD	D,(HL)
	EX	DE,HL
	LD	HL,(_ZAHYO)
	LD	HL,(I)
; array load: t54 = t52[t53]
	PUSH	HL
	POP	DE
	EX	DE,HL
	ADD	HL,HL
	ADD	HL,DE
	LD	E,(HL)
	INC	HL
	LD	D,(HL)
	EX	DE,HL
	LD	HL,$0001
; array store: t54[t55] = t51
	PUSH	HL
	POP	DE
	LD	(HL),E
	INC	HL
	LD	(HL),D
	LD	HL,(ZV)
	LD	HL,(I)
; array load: t59 = t56[t58]
	PUSH	HL
	POP	DE
	EX	DE,HL
	ADD	HL,HL
	ADD	HL,DE
	LD	E,(HL)
	INC	HL
	LD	D,(HL)
	EX	DE,HL
	LD	HL,(_ZAHYO)
	LD	HL,(I)
; array load: t62 = t60[t61]
	PUSH	HL
	POP	DE
	EX	DE,HL
	ADD	HL,HL
	ADD	HL,DE
	LD	E,(HL)
	INC	HL
	LD	D,(HL)
	EX	DE,HL
	LD	HL,$0002
; array store: t62[t63] = t59
	PUSH	HL
	POP	DE
	LD	(HL),E
	INC	HL
	LD	(HL),D
_L59:
	LD	HL,(I)
	LD	HL,$0001
	PUSH	HL
	POP	DE
	EX	DE,HL
	ADD	HL,DE
	LD	(I),HL
	LD	HL,$0003
	PUSH	HL
	POP	DE
	EX	DE,HL
	EX	DE,HL
	OR	A
	SBC	HL,DE
	LD	HL,$0000
	JR	C,$+3
	INC	HL
	LD	A,L
	XOR	$01
	LD	L,A
; if t68 != 0 goto _L58
	LD	A,H
	OR	L
	JP	NZ,_L58
_L60:
	LD	HL,$0004
	LD	HL,(_LPCT)
	LD	HL,$0000
; array store: t70[t71] = t69
	PUSH	HL
	POP	DE
	LD	(HL),E
	INC	HL
	LD	(HL),D
	LD	HL,JO
	PUSH	HL
	LD	HL,$0003
	PUSH	HL
	CALL	@SETWD
	LD	HL,$0000
	PUSH	HL
	CALL	@MAGIC
	LD	HL,(_DISP)
	LD	HL,$0000
; array load: t80 = t77[t79]
	PUSH	HL
	POP	DE
	EX	DE,HL
	ADD	HL,HL
	ADD	HL,DE
	LD	E,(HL)
	INC	HL
	LD	D,(HL)
	EX	DE,HL
	LD	HL,$0000
; array load: t82 = t80[t81]
	PUSH	HL
	POP	DE
	EX	DE,HL
	ADD	HL,HL
	ADD	HL,DE
	LD	E,(HL)
	INC	HL
	LD	D,(HL)
	EX	DE,HL
	LD	(PX0),HL
	LD	HL,(_DISP)
	LD	HL,$0001
; array load: t86 = t83[t85]
	PUSH	HL
	POP	DE
	EX	DE,HL
	ADD	HL,HL
	ADD	HL,DE
	LD	E,(HL)
	INC	HL
	LD	D,(HL)
	EX	DE,HL
	LD	HL,$0000
; array load: t88 = t86[t87]
	PUSH	HL
	POP	DE
	EX	DE,HL
	ADD	HL,HL
	ADD	HL,DE
	LD	E,(HL)
	INC	HL
	LD	D,(HL)
	EX	DE,HL
	LD	(PX1),HL
	LD	HL,(_DISP)
	LD	HL,$0002
; array load: t92 = t89[t91]
	PUSH	HL
	POP	DE
	EX	DE,HL
	ADD	HL,HL
	ADD	HL,DE
	LD	E,(HL)
	INC	HL
	LD	D,(HL)
	EX	DE,HL
	LD	HL,$0000
; array load: t94 = t92[t93]
	PUSH	HL
	POP	DE
	EX	DE,HL
	ADD	HL,HL
	ADD	HL,DE
	LD	E,(HL)
	INC	HL
	LD	D,(HL)
	EX	DE,HL
	LD	(PX2),HL
	LD	HL,(_DISP)
	LD	HL,$0003
; array load: t98 = t95[t97]
	PUSH	HL
	POP	DE
	EX	DE,HL
	ADD	HL,HL
	ADD	HL,DE
	LD	E,(HL)
	INC	HL
	LD	D,(HL)
	EX	DE,HL
	LD	HL,$0000
; array load: t100 = t98[t99]
	PUSH	HL
	POP	DE
	EX	DE,HL
	ADD	HL,HL
	ADD	HL,DE
	LD	E,(HL)
	INC	HL
	LD	D,(HL)
	EX	DE,HL
	LD	(PX3),HL
	LD	HL,(_DISP)
	LD	HL,$0000
; array load: t104 = t101[t103]
	PUSH	HL
	POP	DE
	EX	DE,HL
	ADD	HL,HL
	ADD	HL,DE
	LD	E,(HL)
	INC	HL
	LD	D,(HL)
	EX	DE,HL
	LD	HL,$0001
; array load: t106 = t104[t105]
	PUSH	HL
	POP	DE
	EX	DE,HL
	ADD	HL,HL
	ADD	HL,DE
	LD	E,(HL)
	INC	HL
	LD	D,(HL)
	EX	DE,HL
	LD	(PY0),HL
	LD	HL,(_DISP)
	LD	HL,$0001
; array load: t110 = t107[t109]
	PUSH	HL
	POP	DE
	EX	DE,HL
	ADD	HL,HL
	ADD	HL,DE
	LD	E,(HL)
	INC	HL
	LD	D,(HL)
	EX	DE,HL
	LD	HL,$0001
; array load: t112 = t110[t111]
	PUSH	HL
	POP	DE
	EX	DE,HL
	ADD	HL,HL
	ADD	HL,DE
	LD	E,(HL)
	INC	HL
	LD	D,(HL)
	EX	DE,HL
	LD	(PY1),HL
	LD	HL,(_DISP)
	LD	HL,$0002
; array load: t116 = t113[t115]
	PUSH	HL
	POP	DE
	EX	DE,HL
	ADD	HL,HL
	ADD	HL,DE
	LD	E,(HL)
	INC	HL
	LD	D,(HL)
	EX	DE,HL
	LD	HL,$0001
; array load: t118 = t116[t117]
	PUSH	HL
	POP	DE
	EX	DE,HL
	ADD	HL,HL
	ADD	HL,DE
	LD	E,(HL)
	INC	HL
	LD	D,(HL)
	EX	DE,HL
	LD	(PY2),HL
	LD	HL,(_DISP)
	LD	HL,$0003
; array load: t122 = t119[t121]
	PUSH	HL
	POP	DE
	EX	DE,HL
	ADD	HL,HL
	ADD	HL,DE
	LD	E,(HL)
	INC	HL
	LD	D,(HL)
	EX	DE,HL
	LD	HL,$0001
; array load: t124 = t122[t123]
	PUSH	HL
	POP	DE
	EX	DE,HL
	ADD	HL,HL
	ADD	HL,DE
	LD	E,(HL)
	INC	HL
	LD	D,(HL)
	EX	DE,HL
	LD	(PY3),HL
	LD	HL,(PX1)
	LD	HL,(PX0)
	PUSH	HL
	POP	DE
	EX	DE,HL
	OR	A
	SBC	HL,DE
	LD	HL,(PY2)
	LD	HL,(PY1)
	PUSH	HL
	POP	DE
	EX	DE,HL
	OR	A
	SBC	HL,DE
	PUSH	HL
	POP	DE
	EX	DE,HL
	CALL	MUL16
	LD	HL,(PY1)
	LD	HL,(PY0)
	PUSH	HL
	POP	DE
	EX	DE,HL
	OR	A
	SBC	HL,DE
	LD	HL,(PX2)
	LD	HL,(PX1)
	PUSH	HL
	POP	DE
	EX	DE,HL
	OR	A
	SBC	HL,DE
	PUSH	HL
	POP	DE
	EX	DE,HL
	CALL	MUL16
	PUSH	HL
	POP	DE
	EX	DE,HL
	OR	A
	SBC	HL,DE
	LD	(NZ),HL
	LD	HL,(_WSEL)
	LD	HL,$0001
	PUSH	HL
	POP	DE
	EX	DE,HL
	LD	A,H
	AND	D
	LD	H,A
	LD	A,L
	AND	E
	LD	L,A
	LD	HL,$0001
	PUSH	HL
	POP	DE
	EX	DE,HL
	OR	A
	SBC	HL,DE
	LD	HL,$0000
	JR	Z,$+3
	INC	HL
; if t144 == 0 goto _L61
	LD	A,H
	OR	L
	JP	Z,_L61
	LD	HL,$0001
	PUSH	HL
	CALL	@MAGIC
_L61:
	LD	HL,(_WSEL)
	LD	HL,$0002
	PUSH	HL
	POP	DE
	EX	DE,HL
	LD	A,H
	AND	D
	LD	H,A
	LD	A,L
	AND	E
	LD	L,A
	LD	HL,$0002
	PUSH	HL
	POP	DE
	EX	DE,HL
	OR	A
	SBC	HL,DE
	LD	HL,$0000
	JR	Z,$+3
	INC	HL
; if t151 == 0 goto _L62
	LD	A,H
	OR	L
	JP	Z,_L62
	LD	HL,$0000
	LD	(I),HL
_L63:
	LD	HL,(_BUFPAR)
	LD	HL,(I)
; array load: t156 = t153[t155]
	PUSH	HL
	POP	DE
	EX	DE,HL
	ADD	HL,HL
	ADD	HL,DE
	LD	E,(HL)
	INC	HL
	LD	D,(HL)
	EX	DE,HL
	LD	HL,(_PAR)
	LD	HL,(I)
; array store: t157[t158] = t156
	PUSH	HL
	POP	DE
	LD	(HL),E
	INC	HL
	LD	(HL),D
_L64:
	LD	HL,(I)
	LD	HL,$0001
	PUSH	HL
	POP	DE
	EX	DE,HL
	ADD	HL,DE
	LD	(I),HL
	LD	HL,$0008
	PUSH	HL
	POP	DE
	EX	DE,HL
	EX	DE,HL
	OR	A
	SBC	HL,DE
	LD	HL,$0000
	JR	C,$+3
	INC	HL
	LD	A,L
	XOR	$01
	LD	L,A
; if t163 != 0 goto _L63
	LD	A,H
	OR	L
	JP	NZ,_L63
_L65:
	JP	_EXIT
_L62:
	LD	HL,(NZ)
	LD	HL,$0004
	LD	A,H
	CPL
	LD	H,A
	LD	A,L
	CPL
	LD	L,A
	INC	HL
	PUSH	HL
	POP	DE
	EX	DE,HL
	CALL	SCMP_LT
; if t167 == 0 goto _L66
	LD	A,H
	OR	L
	JP	Z,_L66
	LD	HL,$0000
	LD	(I),HL
_L67:
	LD	HL,(_BUFPAR)
	LD	HL,(I)
; array load: t172 = t169[t171]
	PUSH	HL
	POP	DE
	EX	DE,HL
	ADD	HL,HL
	ADD	HL,DE
	LD	E,(HL)
	INC	HL
	LD	D,(HL)
	EX	DE,HL
	LD	HL,(_PAR)
	LD	HL,(I)
; array store: t173[t174] = t172
	PUSH	HL
	POP	DE
	LD	(HL),E
	INC	HL
	LD	(HL),D
_L68:
	LD	HL,(I)
	LD	HL,$0001
	PUSH	HL
	POP	DE
	EX	DE,HL
	ADD	HL,DE
	LD	(I),HL
	LD	HL,$0008
	PUSH	HL
	POP	DE
	EX	DE,HL
	EX	DE,HL
	OR	A
	SBC	HL,DE
	LD	HL,$0000
	JR	C,$+3
	INC	HL
	LD	A,L
	XOR	$01
	LD	L,A
; if t179 != 0 goto _L67
	LD	A,H
	OR	L
	JP	NZ,_L67
_L69:
	JP	_EXIT
_L66:
	LD	HL,(_WSEL)
	LD	HL,$0004
	PUSH	HL
	POP	DE
	EX	DE,HL
	LD	A,H
	AND	D
	LD	H,A
	LD	A,L
	AND	E
	LD	L,A
	LD	HL,$0004
	PUSH	HL
	POP	DE
	EX	DE,HL
	OR	A
	SBC	HL,DE
	LD	HL,$0000
	JR	Z,$+3
	INC	HL
; if t184 == 0 goto _L70
	LD	A,H
	OR	L
	JP	Z,_L70
	LD	HL,$0001
	PUSH	HL
	CALL	@MAGIC
	LD	HL,$0000
	LD	(I),HL
_L71:
	LD	HL,(_BUFPAR)
	LD	HL,(I)
; array load: t191 = t188[t190]
	PUSH	HL
	POP	DE
	EX	DE,HL
	ADD	HL,HL
	ADD	HL,DE
	LD	E,(HL)
	INC	HL
	LD	D,(HL)
	EX	DE,HL
	LD	HL,(_PAR)
	LD	HL,(I)
; array store: t192[t193] = t191
	PUSH	HL
	POP	DE
	LD	(HL),E
	INC	HL
	LD	(HL),D
_L72:
	LD	HL,(I)
	LD	HL,$0001
	PUSH	HL
	POP	DE
	EX	DE,HL
	ADD	HL,DE
	LD	(I),HL
	LD	HL,$0008
	PUSH	HL
	POP	DE
	EX	DE,HL
	EX	DE,HL
	OR	A
	SBC	HL,DE
	LD	HL,$0000
	JR	C,$+3
	INC	HL
	LD	A,L
	XOR	$01
	LD	L,A
; if t198 != 0 goto _L71
	LD	A,H
	OR	L
	JP	NZ,_L71
_L73:
	JP	_EXIT
_L70:
	LD	HL,(YV)
	LD	HL,$0001
; array load: t202 = t199[t201]
	PUSH	HL
	POP	DE
	EX	DE,HL
	ADD	HL,HL
	ADD	HL,DE
	LD	E,(HL)
	INC	HL
	LD	D,(HL)
	EX	DE,HL
	LD	HL,(YV)
	LD	HL,$0000
; array load: t206 = t203[t205]
	PUSH	HL
	POP	DE
	EX	DE,HL
	ADD	HL,HL
	ADD	HL,DE
	LD	E,(HL)
	INC	HL
	LD	D,(HL)
	EX	DE,HL
	PUSH	HL
	POP	DE
	EX	DE,HL
	OR	A
	SBC	HL,DE
	LD	HL,(ZV)
	LD	HL,$0002
; array load: t211 = t208[t210]
	PUSH	HL
	POP	DE
	EX	DE,HL
	ADD	HL,HL
	ADD	HL,DE
	LD	E,(HL)
	INC	HL
	LD	D,(HL)
	EX	DE,HL
	LD	HL,(ZV)
	LD	HL,$0001
; array load: t215 = t212[t214]
	PUSH	HL
	POP	DE
	EX	DE,HL
	ADD	HL,HL
	ADD	HL,DE
	LD	E,(HL)
	INC	HL
	LD	D,(HL)
	EX	DE,HL
	PUSH	HL
	POP	DE
	EX	DE,HL
	OR	A
	SBC	HL,DE
	PUSH	HL
	POP	DE
	EX	DE,HL
	CALL	MUL16
	LD	HL,(ZV)
	LD	HL,$0001
; array load: t221 = t218[t220]
	PUSH	HL
	POP	DE
	EX	DE,HL
	ADD	HL,HL
	ADD	HL,DE
	LD	E,(HL)
	INC	HL
	LD	D,(HL)
	EX	DE,HL
	LD	HL,(ZV)
	LD	HL,$0000
; array load: t225 = t222[t224]
	PUSH	HL
	POP	DE
	EX	DE,HL
	ADD	HL,HL
	ADD	HL,DE
	LD	E,(HL)
	INC	HL
	LD	D,(HL)
	EX	DE,HL
	PUSH	HL
	POP	DE
	EX	DE,HL
	OR	A
	SBC	HL,DE
	LD	HL,(YV)
	LD	HL,$0002
; array load: t230 = t227[t229]
	PUSH	HL
	POP	DE
	EX	DE,HL
	ADD	HL,HL
	ADD	HL,DE
	LD	E,(HL)
	INC	HL
	LD	D,(HL)
	EX	DE,HL
	LD	HL,(YV)
	LD	HL,$0001
; array load: t234 = t231[t233]
	PUSH	HL
	POP	DE
	EX	DE,HL
	ADD	HL,HL
	ADD	HL,DE
	LD	E,(HL)
	INC	HL
	LD	D,(HL)
	EX	DE,HL
	PUSH	HL
	POP	DE
	EX	DE,HL
	OR	A
	SBC	HL,DE
	PUSH	HL
	POP	DE
	EX	DE,HL
	CALL	MUL16
	PUSH	HL
	POP	DE
	EX	DE,HL
	OR	A
	SBC	HL,DE
	LD	(NX),HL
	LD	HL,(ZV)
	LD	HL,$0001
; array load: t241 = t238[t240]
	PUSH	HL
	POP	DE
	EX	DE,HL
	ADD	HL,HL
	ADD	HL,DE
	LD	E,(HL)
	INC	HL
	LD	D,(HL)
	EX	DE,HL
	LD	HL,(ZV)
	LD	HL,$0000
; array load: t245 = t242[t244]
	PUSH	HL
	POP	DE
	EX	DE,HL
	ADD	HL,HL
	ADD	HL,DE
	LD	E,(HL)
	INC	HL
	LD	D,(HL)
	EX	DE,HL
	PUSH	HL
	POP	DE
	EX	DE,HL
	OR	A
	SBC	HL,DE
	LD	HL,(XV)
	LD	HL,$0002
; array load: t250 = t247[t249]
	PUSH	HL
	POP	DE
	EX	DE,HL
	ADD	HL,HL
	ADD	HL,DE
	LD	E,(HL)
	INC	HL
	LD	D,(HL)
	EX	DE,HL
	LD	HL,(XV)
	LD	HL,$0001
; array load: t254 = t251[t253]
	PUSH	HL
	POP	DE
	EX	DE,HL
	ADD	HL,HL
	ADD	HL,DE
	LD	E,(HL)
	INC	HL
	LD	D,(HL)
	EX	DE,HL
	PUSH	HL
	POP	DE
	EX	DE,HL
	OR	A
	SBC	HL,DE
	PUSH	HL
	POP	DE
	EX	DE,HL
	CALL	MUL16
	LD	HL,(XV)
	LD	HL,$0001
; array load: t260 = t257[t259]
	PUSH	HL
	POP	DE
	EX	DE,HL
	ADD	HL,HL
	ADD	HL,DE
	LD	E,(HL)
	INC	HL
	LD	D,(HL)
	EX	DE,HL
	LD	HL,(XV)
	LD	HL,$0000
; array load: t264 = t261[t263]
	PUSH	HL
	POP	DE
	EX	DE,HL
	ADD	HL,HL
	ADD	HL,DE
	LD	E,(HL)
	INC	HL
	LD	D,(HL)
	EX	DE,HL
	PUSH	HL
	POP	DE
	EX	DE,HL
	OR	A
	SBC	HL,DE
	LD	HL,(ZV)
	LD	HL,$0002
; array load: t269 = t266[t268]
	PUSH	HL
	POP	DE
	EX	DE,HL
	ADD	HL,HL
	ADD	HL,DE
	LD	E,(HL)
	INC	HL
	LD	D,(HL)
	EX	DE,HL
	LD	HL,(ZV)
	LD	HL,$0001
; array load: t273 = t270[t272]
	PUSH	HL
	POP	DE
	EX	DE,HL
	ADD	HL,HL
	ADD	HL,DE
	LD	E,(HL)
	INC	HL
	LD	D,(HL)
	EX	DE,HL
	PUSH	HL
	POP	DE
	EX	DE,HL
	OR	A
	SBC	HL,DE
	PUSH	HL
	POP	DE
	EX	DE,HL
	CALL	MUL16
	PUSH	HL
	POP	DE
	EX	DE,HL
	OR	A
	SBC	HL,DE
	LD	(NY),HL
	LD	HL,(XV)
	LD	HL,$0001
; array load: t280 = t277[t279]
	PUSH	HL
	POP	DE
	EX	DE,HL
	ADD	HL,HL
	ADD	HL,DE
	LD	E,(HL)
	INC	HL
	LD	D,(HL)
	EX	DE,HL
	LD	HL,(XV)
	LD	HL,$0000
; array load: t284 = t281[t283]
	PUSH	HL
	POP	DE
	EX	DE,HL
	ADD	HL,HL
	ADD	HL,DE
	LD	E,(HL)
	INC	HL
	LD	D,(HL)
	EX	DE,HL
	PUSH	HL
	POP	DE
	EX	DE,HL
	OR	A
	SBC	HL,DE
	LD	HL,(YV)
	LD	HL,$0002
; array load: t289 = t286[t288]
	PUSH	HL
	POP	DE
	EX	DE,HL
	ADD	HL,HL
	ADD	HL,DE
	LD	E,(HL)
	INC	HL
	LD	D,(HL)
	EX	DE,HL
	LD	HL,(YV)
	LD	HL,$0001
; array load: t293 = t290[t292]
	PUSH	HL
	POP	DE
	EX	DE,HL
	ADD	HL,HL
	ADD	HL,DE
	LD	E,(HL)
	INC	HL
	LD	D,(HL)
	EX	DE,HL
	PUSH	HL
	POP	DE
	EX	DE,HL
	OR	A
	SBC	HL,DE
	PUSH	HL
	POP	DE
	EX	DE,HL
	CALL	MUL16
	LD	HL,(YV)
	LD	HL,$0001
; array load: t299 = t296[t298]
	PUSH	HL
	POP	DE
	EX	DE,HL
	ADD	HL,HL
	ADD	HL,DE
	LD	E,(HL)
	INC	HL
	LD	D,(HL)
	EX	DE,HL
	LD	HL,(YV)
	LD	HL,$0000
; array load: t303 = t300[t302]
	PUSH	HL
	POP	DE
	EX	DE,HL
	ADD	HL,HL
	ADD	HL,DE
	LD	E,(HL)
	INC	HL
	LD	D,(HL)
	EX	DE,HL
	PUSH	HL
	POP	DE
	EX	DE,HL
	OR	A
	SBC	HL,DE
	LD	HL,(XV)
	LD	HL,$0002
; array load: t308 = t305[t307]
	PUSH	HL
	POP	DE
	EX	DE,HL
	ADD	HL,HL
	ADD	HL,DE
	LD	E,(HL)
	INC	HL
	LD	D,(HL)
	EX	DE,HL
	LD	HL,(XV)
	LD	HL,$0001
; array load: t312 = t309[t311]
	PUSH	HL
	POP	DE
	EX	DE,HL
	ADD	HL,HL
	ADD	HL,DE
	LD	E,(HL)
	INC	HL
	LD	D,(HL)
	EX	DE,HL
	PUSH	HL
	POP	DE
	EX	DE,HL
	OR	A
	SBC	HL,DE
	PUSH	HL
	POP	DE
	EX	DE,HL
	CALL	MUL16
	PUSH	HL
	POP	DE
	EX	DE,HL
	OR	A
	SBC	HL,DE
	LD	(NZ),HL
	LD	HL,(_LX)
	LD	(plx),HL
	LD	HL,(_LY)
	LD	(ply),HL
	LD	HL,(_LZ)
	LD	(plz),HL
	LD	HL,(plx)
	LD	(lg),HL
	LD	HL,(lg)
	LD	HL,(FLDIV)
	PUSH	HL
	POP	DE
	EX	DE,HL
	CALL	DIV16
	LD	(lg),HL
	LD	HL,(ply)
	LD	HL,(FLDIV)
	PUSH	HL
	POP	DE
	EX	DE,HL
	CALL	DIV16
	LD	(ply),HL
	LD	HL,(plz)
	LD	HL,(FLDIV)
	PUSH	HL
	POP	DE
	EX	DE,HL
	CALL	DIV16
	LD	(plz),HL
	LD	HL,(lg)
	LD	HL,(lg)
	PUSH	HL
	POP	DE
	EX	DE,HL
	CALL	MUL16
	LD	HL,(ply)
	LD	HL,(ply)
	PUSH	HL
	POP	DE
	EX	DE,HL
	CALL	MUL16
	PUSH	HL
	POP	DE
	EX	DE,HL
	ADD	HL,DE
	LD	HL,(plz)
	LD	HL,(plz)
	PUSH	HL
	POP	DE
	EX	DE,HL
	CALL	MUL16
	PUSH	HL
	POP	DE
	EX	DE,HL
	ADD	HL,DE
	PUSH	HL
	CALL	FSQRT
	LD	(lg),HL
	LD	HL,(lg)
	LD	HL,(FLDIV)
	PUSH	HL
	POP	DE
	EX	DE,HL
	CALL	MUL16
	LD	(lg),HL
	LD	HL,(_LY)
	LD	(ply),HL
	LD	HL,(_LZ)
	LD	(plz),HL
	LD	HL,(plx)
	LD	HL,(lg)
	PUSH	HL
	POP	DE
	EX	DE,HL
	CALL	DIV16
	LD	(plx),HL
	LD	HL,(ply)
	LD	HL,(lg)
	PUSH	HL
	POP	DE
	EX	DE,HL
	CALL	DIV16
	LD	(ply),HL
	LD	HL,(plz)
	LD	HL,(lg)
	PUSH	HL
	POP	DE
	EX	DE,HL
	CALL	DIV16
	LD	(plz),HL
	LD	HL,(NX)
	LD	(ax),HL
	LD	HL,(NY)
	LD	(ay),HL
	LD	HL,(NZ)
	LD	(az),HL
	LD	HL,(ax)
	LD	HL,(FLDIV)
	PUSH	HL
	POP	DE
	EX	DE,HL
	CALL	DIV16
	LD	(ax),HL
	LD	HL,(ay)
	LD	HL,(FLDIV)
	PUSH	HL
	POP	DE
	EX	DE,HL
	CALL	DIV16
	LD	(ay),HL
	LD	HL,(az)
	LD	HL,(FLDIV)
	PUSH	HL
	POP	DE
	EX	DE,HL
	CALL	DIV16
	LD	(az),HL
	LD	HL,(ax)
	LD	HL,(ax)
	PUSH	HL
	POP	DE
	EX	DE,HL
	CALL	MUL16
	LD	HL,(ay)
	LD	HL,(ay)
	PUSH	HL
	POP	DE
	EX	DE,HL
	CALL	MUL16
	PUSH	HL
	POP	DE
	EX	DE,HL
	ADD	HL,DE
	LD	HL,(az)
	LD	HL,(az)
	PUSH	HL
	POP	DE
	EX	DE,HL
	CALL	MUL16
	PUSH	HL
	POP	DE
	EX	DE,HL
	ADD	HL,DE
	PUSH	HL
	CALL	FSQRT
	LD	(lg),HL
	LD	HL,(lg)
	LD	HL,(FLDIV)
	PUSH	HL
	POP	DE
	EX	DE,HL
	CALL	MUL16
	LD	(lg),HL
	LD	HL,(NX)
	LD	HL,(plx)
	PUSH	HL
	POP	DE
	EX	DE,HL
	CALL	MUL16
	LD	(ax),HL
	LD	HL,(NY)
	LD	HL,(ply)
	PUSH	HL
	POP	DE
	EX	DE,HL
	CALL	MUL16
	LD	(ay),HL
	LD	HL,(NZ)
	LD	HL,(plz)
	PUSH	HL
	POP	DE
	EX	DE,HL
	CALL	MUL16
	LD	(az),HL
	LD	HL,(ax)
	LD	HL,(ay)
	PUSH	HL
	POP	DE
	EX	DE,HL
	ADD	HL,DE
	LD	HL,(az)
	PUSH	HL
	POP	DE
	EX	DE,HL
	ADD	HL,DE
	LD	(yg),HL
	LD	HL,(yg)
	LD	HL,(lg)
	PUSH	HL
	POP	DE
	EX	DE,HL
	CALL	DIV16
	LD	(yg),HL
	LD	HL,(yg)
	LD	HL,$0000
	PUSH	HL
	POP	DE
	EX	DE,HL
	CALL	MUL16
	LD	(yg),HL
	LD	HL,(yg)
	PUSH	HL
	CALL	FTOI
	LD	HL,$0007
	PUSH	HL
	POP	DE
	EX	DE,HL
	ADD	HL,DE
	LD	HL,$0002
	PUSH	HL
	POP	DE
	EX	DE,HL
	CALL	SDIV16
	LD	(LV),HL
	LD	HL,(LV)
	LD	HL,$0000
	PUSH	HL
	POP	DE
	EX	DE,HL
	CALL	SCMP_LT
; if t410 == 0 goto _L74
	LD	A,H
	OR	L
	JP	Z,_L74
	LD	HL,$0000
	LD	(LV),HL
_L74:
	LD	HL,(_GMASK)
	LD	HL,$0001
	PUSH	HL
	POP	DE
	EX	DE,HL
	LD	A,H
	AND	D
	LD	H,A
	LD	A,L
	AND	E
	LD	L,A
	LD	HL,$0001
	PUSH	HL
	POP	DE
	EX	DE,HL
	OR	A
	SBC	HL,DE
	LD	HL,$0000
	JR	Z,$+3
	INC	HL
; if t416 == 0 goto _L75
	LD	A,H
	OR	L
	JP	Z,_L75
	LD	HL,$0002
	PUSH	HL
	LD	HL,$0000
	PUSH	HL
	CALL	@MODE
	LD	HL,(LV)
	LD	HL,(_SBLUE)
	PUSH	HL
	POP	DE
	EX	DE,HL
	CALL	MUL16
	LD	HL,$0007
	PUSH	HL
	POP	DE
	EX	DE,HL
	CALL	DIV16
	PUSH	HL
	CALL	@GRAD
	LD	HL,(PX0)
	PUSH	HL
	LD	HL,(PY0)
	PUSH	HL
	LD	HL,(PX1)
	PUSH	HL
	LD	HL,(PY1)
	PUSH	HL
	LD	HL,(PX2)
	PUSH	HL
	LD	HL,(PY2)
	PUSH	HL
	CALL	@TRIANGLE
	LD	HL,(PX2)
	PUSH	HL
	LD	HL,(PY2)
	PUSH	HL
	LD	HL,(PX3)
	PUSH	HL
	LD	HL,(PY3)
	PUSH	HL
	LD	HL,(PX0)
	PUSH	HL
	LD	HL,(PY0)
	PUSH	HL
	CALL	@TRIANGLE
_L75:
	LD	HL,(_GMASK)
	LD	HL,$0002
	PUSH	HL
	POP	DE
	EX	DE,HL
	LD	A,H
	AND	D
	LD	H,A
	LD	A,L
	AND	E
	LD	L,A
	LD	HL,$0002
	PUSH	HL
	POP	DE
	EX	DE,HL
	OR	A
	SBC	HL,DE
	LD	HL,$0000
	JR	Z,$+3
	INC	HL
; if t444 == 0 goto _L76
	LD	A,H
	OR	L
	JP	Z,_L76
	LD	HL,$0002
	PUSH	HL
	LD	HL,$0001
	PUSH	HL
	CALL	@MODE
	LD	HL,(LV)
	LD	HL,(_SRED)
	PUSH	HL
	POP	DE
	EX	DE,HL
	CALL	MUL16
	LD	HL,$0007
	PUSH	HL
	POP	DE
	EX	DE,HL
	CALL	DIV16
	PUSH	HL
	CALL	@GRAD
	LD	HL,(PX0)
	PUSH	HL
	LD	HL,(PY0)
	PUSH	HL
	LD	HL,(PX1)
	PUSH	HL
	LD	HL,(PY1)
	PUSH	HL
	LD	HL,(PX2)
	PUSH	HL
	LD	HL,(PY2)
	PUSH	HL
	CALL	@TRIANGLE
	LD	HL,(PX2)
	PUSH	HL
	LD	HL,(PY2)
	PUSH	HL
	LD	HL,(PX3)
	PUSH	HL
	LD	HL,(PY3)
	PUSH	HL
	LD	HL,(PX0)
	PUSH	HL
	LD	HL,(PY0)
	PUSH	HL
	CALL	@TRIANGLE
_L76:
	LD	HL,(_GMASK)
	LD	HL,$0004
	PUSH	HL
	POP	DE
	EX	DE,HL
	LD	A,H
	AND	D
	LD	H,A
	LD	A,L
	AND	E
	LD	L,A
	LD	HL,$0004
	PUSH	HL
	POP	DE
	EX	DE,HL
	OR	A
	SBC	HL,DE
	LD	HL,$0000
	JR	Z,$+3
	INC	HL
; if t472 == 0 goto _L77
	LD	A,H
	OR	L
	JP	Z,_L77
	LD	HL,$0002
	PUSH	HL
	LD	HL,$0002
	PUSH	HL
	CALL	@MODE
	LD	HL,(LV)
	LD	HL,(_SGREEN)
	PUSH	HL
	POP	DE
	EX	DE,HL
	CALL	MUL16
	LD	HL,$0007
	PUSH	HL
	POP	DE
	EX	DE,HL
	CALL	DIV16
	PUSH	HL
	CALL	@GRAD
	LD	HL,(PX0)
	PUSH	HL
	LD	HL,(PY0)
	PUSH	HL
	LD	HL,(PX1)
	PUSH	HL
	LD	HL,(PY1)
	PUSH	HL
	LD	HL,(PX2)
	PUSH	HL
	LD	HL,(PY2)
	PUSH	HL
	CALL	@TRIANGLE
	LD	HL,(PX2)
	PUSH	HL
	LD	HL,(PY2)
	PUSH	HL
	LD	HL,(PX3)
	PUSH	HL
	LD	HL,(PY3)
	PUSH	HL
	LD	HL,(PX0)
	PUSH	HL
	LD	HL,(PY0)
	PUSH	HL
	CALL	@TRIANGLE
_L77:
	LD	HL,(_WSEL)
	LD	HL,$0008
	PUSH	HL
	POP	DE
	EX	DE,HL
	LD	A,H
	AND	D
	LD	H,A
	LD	A,L
	AND	E
	LD	L,A
	LD	HL,$0008
	PUSH	HL
	POP	DE
	EX	DE,HL
	OR	A
	SBC	HL,DE
	LD	HL,$0000
	JR	Z,$+3
	INC	HL
; if t500 == 0 goto _L78
	LD	A,H
	OR	L
	JP	Z,_L78
	LD	HL,$0001
	PUSH	HL
	CALL	@MAGIC
_L78:
	LD	HL,$0000
	LD	(I),HL
_L79:
	LD	HL,(_BUFPAR)
	LD	HL,(I)
; array load: t507 = t504[t506]
	PUSH	HL
	POP	DE
	EX	DE,HL
	ADD	HL,HL
	ADD	HL,DE
	LD	E,(HL)
	INC	HL
	LD	D,(HL)
	EX	DE,HL
	LD	HL,(_PAR)
	LD	HL,(I)
; array store: t508[t509] = t507
	PUSH	HL
	POP	DE
	LD	(HL),E
	INC	HL
	LD	(HL),D
_L80:
	LD	HL,(I)
	LD	HL,$0001
	PUSH	HL
	POP	DE
	EX	DE,HL
	ADD	HL,DE
	LD	(I),HL
	LD	HL,$0008
	PUSH	HL
	POP	DE
	EX	DE,HL
	EX	DE,HL
	OR	A
	SBC	HL,DE
	LD	HL,$0000
	JR	C,$+3
	INC	HL
	LD	A,L
	XOR	$01
	LD	L,A
; if t514 != 0 goto _L79
	LD	A,H
	OR	L
	JP	NZ,_L79
_L81:
_EXIT:
	POP	IY
	RET

@KYU:
; function @KYU
	PUSH	IY
; ARRAY KYUX[SLANGCompiler.Parser.Ast.IntegerLiteral]
; ARRAY KYUY[SLANGCompiler.Parser.Ast.IntegerLiteral]
; ARRAY KYUZ[SLANGCompiler.Parser.Ast.IntegerLiteral]
	LD	HL,$0000
	LD	(I),HL
_L82:
	LD	HL,(I)
	LD	HL,$00B4
	PUSH	HL
	POP	DE
	EX	DE,HL
	OR	A
	SBC	HL,DE
	LD	HL,$0000
	JR	C,$+3
	INC	HL
; if t3 == 0 goto _L83
	LD	A,H
	OR	L
	JP	Z,_L83
	LD	HL,$0000
	LD	(J),HL
	LD	HL,(I)
	PUSH	HL
	LD	HL,(HNK)
	PUSH	HL
	CALL	@ISIN
	LD	(K),HL
	LD	HL,(I)
	LD	HL,(_KSTEP)
	PUSH	HL
	POP	DE
	EX	DE,HL
	ADD	HL,DE
	PUSH	HL
	LD	HL,(HNK)
	PUSH	HL
	CALL	@ISIN
	LD	(L),HL
	LD	HL,(I)
	PUSH	HL
	LD	HL,(HNK)
	PUSH	HL
	CALL	@ICOS
	LD	HL,(MDY)
	PUSH	HL
	POP	DE
	EX	DE,HL
	ADD	HL,DE
	LD	HL,(KYUY)
	LD	HL,$0000
; array store: t18[t19] = t17
	PUSH	HL
	POP	DE
	LD	(HL),E
	INC	HL
	LD	(HL),D
	LD	HL,(KYUY)
	LD	HL,$0000
; array load: t23 = t20[t22]
	PUSH	HL
	POP	DE
	EX	DE,HL
	ADD	HL,HL
	ADD	HL,DE
	LD	E,(HL)
	INC	HL
	LD	D,(HL)
	EX	DE,HL
	LD	HL,(KYUY)
	LD	HL,$0001
; array store: t24[t25] = t23
	PUSH	HL
	POP	DE
	LD	(HL),E
	INC	HL
	LD	(HL),D
	LD	HL,(I)
	LD	HL,(_KSTEP)
	PUSH	HL
	POP	DE
	EX	DE,HL
	ADD	HL,DE
	PUSH	HL
	LD	HL,(HNK)
	PUSH	HL
	CALL	@ICOS
	LD	HL,(MDY)
	PUSH	HL
	POP	DE
	EX	DE,HL
	ADD	HL,DE
	LD	HL,(KYUY)
	LD	HL,$0002
; array store: t33[t34] = t32
	PUSH	HL
	POP	DE
	LD	(HL),E
	INC	HL
	LD	(HL),D
	LD	HL,(KYUY)
	LD	HL,$0002
; array load: t38 = t35[t37]
	PUSH	HL
	POP	DE
	EX	DE,HL
	ADD	HL,HL
	ADD	HL,DE
	LD	E,(HL)
	INC	HL
	LD	D,(HL)
	EX	DE,HL
	LD	HL,(KYUY)
	LD	HL,$0003
; array store: t39[t40] = t38
	PUSH	HL
	POP	DE
	LD	(HL),E
	INC	HL
	LD	(HL),D
_L84:
	LD	HL,(J)
	LD	HL,$0168
	PUSH	HL
	POP	DE
	EX	DE,HL
	OR	A
	SBC	HL,DE
	LD	HL,$0000
	JR	C,$+3
	INC	HL
; if t43 == 0 goto _L85
	LD	A,H
	OR	L
	JP	Z,_L85
	LD	HL,(MDX)
	LD	HL,(J)
	PUSH	HL
	LD	HL,(K)
	PUSH	HL
	CALL	@ISIN
	PUSH	HL
	POP	DE
	EX	DE,HL
	ADD	HL,DE
	LD	HL,(KYUX)
	LD	HL,$0000
; array store: t49[t50] = t48
	PUSH	HL
	POP	DE
	LD	(HL),E
	INC	HL
	LD	(HL),D
	LD	HL,(MDX)
	LD	HL,(J)
	LD	HL,(_KSTEP)
	PUSH	HL
	POP	DE
	EX	DE,HL
	ADD	HL,DE
	PUSH	HL
	LD	HL,(K)
	PUSH	HL
	CALL	@ISIN
	PUSH	HL
	POP	DE
	EX	DE,HL
	ADD	HL,DE
	LD	HL,(KYUX)
	LD	HL,$0001
; array store: t58[t59] = t57
	PUSH	HL
	POP	DE
	LD	(HL),E
	INC	HL
	LD	(HL),D
	LD	HL,(MDX)
	LD	HL,(J)
	LD	HL,(_KSTEP)
	PUSH	HL
	POP	DE
	EX	DE,HL
	ADD	HL,DE
	PUSH	HL
	LD	HL,(L)
	PUSH	HL
	CALL	@ISIN
	PUSH	HL
	POP	DE
	EX	DE,HL
	ADD	HL,DE
	LD	HL,(KYUX)
	LD	HL,$0002
; array store: t67[t68] = t66
	PUSH	HL
	POP	DE
	LD	(HL),E
	INC	HL
	LD	(HL),D
	LD	HL,(MDX)
	LD	HL,(J)
	PUSH	HL
	LD	HL,(L)
	PUSH	HL
	CALL	@ISIN
	PUSH	HL
	POP	DE
	EX	DE,HL
	ADD	HL,DE
	LD	HL,(KYUX)
	LD	HL,$0003
; array store: t74[t75] = t73
	PUSH	HL
	POP	DE
	LD	(HL),E
	INC	HL
	LD	(HL),D
	LD	HL,(MDZ)
	LD	HL,(J)
	PUSH	HL
	LD	HL,(K)
	PUSH	HL
	CALL	@ICOS
	PUSH	HL
	POP	DE
	EX	DE,HL
	ADD	HL,DE
	LD	HL,(KYUZ)
	LD	HL,$0000
; array store: t81[t82] = t80
	PUSH	HL
	POP	DE
	LD	(HL),E
	INC	HL
	LD	(HL),D
	LD	HL,(MDZ)
	LD	HL,(J)
	LD	HL,(_KSTEP)
	PUSH	HL
	POP	DE
	EX	DE,HL
	ADD	HL,DE
	PUSH	HL
	LD	HL,(K)
	PUSH	HL
	CALL	@ICOS
	PUSH	HL
	POP	DE
	EX	DE,HL
	ADD	HL,DE
	LD	HL,(KYUZ)
	LD	HL,$0001
; array store: t90[t91] = t89
	PUSH	HL
	POP	DE
	LD	(HL),E
	INC	HL
	LD	(HL),D
	LD	HL,(MDZ)
	LD	HL,(J)
	LD	HL,(_KSTEP)
	PUSH	HL
	POP	DE
	EX	DE,HL
	ADD	HL,DE
	PUSH	HL
	LD	HL,(L)
	PUSH	HL
	CALL	@ICOS
	PUSH	HL
	POP	DE
	EX	DE,HL
	ADD	HL,DE
	LD	HL,(KYUZ)
	LD	HL,$0002
; array store: t99[t100] = t98
	PUSH	HL
	POP	DE
	LD	(HL),E
	INC	HL
	LD	(HL),D
	LD	HL,(MDZ)
	LD	HL,(J)
	PUSH	HL
	LD	HL,(L)
	PUSH	HL
	CALL	@ICOS
	PUSH	HL
	POP	DE
	EX	DE,HL
	ADD	HL,DE
	LD	HL,(KYUZ)
	LD	HL,$0003
; array store: t106[t107] = t105
	PUSH	HL
	POP	DE
	LD	(HL),E
	INC	HL
	LD	(HL),D
	LD	HL,(KYUX)
	PUSH	HL
	LD	HL,(KYUY)
	PUSH	HL
	LD	HL,(KYUZ)
	PUSH	HL
	CALL	@CPOLY
	LD	HL,(J)
	LD	HL,(_KSTEP)
	PUSH	HL
	POP	DE
	EX	DE,HL
	ADD	HL,DE
	LD	(J),HL
	JP	_L84
_L85:
	LD	HL,(I)
	LD	HL,(_KSTEP)
	PUSH	HL
	POP	DE
	EX	DE,HL
	ADD	HL,DE
	LD	(I),HL
	JP	_L82
_L83:
_EXIT:
	POP	IY
	RET

@ISIN:
; function @ISIN
	PUSH	IY
_L86:
	LD	HL,(KAKU)
	LD	HL,$0000
	PUSH	HL
	POP	DE
	EX	DE,HL
	CALL	SCMP_LT
; if t2 == 0 goto _L87
	LD	A,H
	OR	L
	JP	Z,_L87
	LD	HL,(KAKU)
	LD	HL,$0168
	PUSH	HL
	POP	DE
	EX	DE,HL
	ADD	HL,DE
	LD	(KAKU),HL
	JP	_L86
_L87:
	LD	HL,(KAKU)
	LD	HL,$0168
	PUSH	HL
	POP	DE
	EX	DE,HL
	CALL	MOD16
	LD	(KAKU),HL
	LD	HL,(KAKU)
	LD	HL,$005A
	PUSH	HL
	POP	DE
	EX	DE,HL
	CALL	MOD16
	LD	(J),HL
	LD	HL,(KAKU)
	LD	HL,$0000
	LD	HL,$0059
	PUSH	HL
	POP	DE
	EX	DE,HL
	OR	A
	SBC	HL,DE
	LD	HL,$0000
	JR	NC,$+3
	INC	HL
	PUSH	HL
	POP	DE
	EX	DE,HL
	EX	DE,HL
	OR	A
	SBC	HL,DE
	LD	HL,$0000
	JR	C,$+3
	INC	HL
	LD	A,L
	XOR	$01
	LD	L,A
	PUSH	HL
	POP	DE
	EX	DE,HL
	LD	A,H
	OR	L
	LD	H,$00
	LD	L,$00
	JR	Z,$+7
	LD	A,D
	OR	E
	JR	Z,$+3
	INC	HL
; if t17 == 0 goto _L89
	LD	A,H
	OR	L
	JP	Z,_L89
	LD	HL,(_STABLE)
	LD	HL,(J)
; array load: t21 = t18[t20]
	PUSH	HL
	POP	DE
	EX	DE,HL
	ADD	HL,HL
	ADD	HL,DE
	LD	E,(HL)
	INC	HL
	LD	D,(HL)
	EX	DE,HL
	LD	(I),HL
	JP	_L88
_L89:
	LD	HL,$005A
	LD	HL,$00B3
	PUSH	HL
	POP	DE
	EX	DE,HL
	OR	A
	SBC	HL,DE
	LD	HL,$0000
	JR	NC,$+3
	INC	HL
	PUSH	HL
	POP	DE
	EX	DE,HL
	EX	DE,HL
	OR	A
	SBC	HL,DE
	LD	HL,$0000
	JR	C,$+3
	INC	HL
	LD	A,L
	XOR	$01
	LD	L,A
	PUSH	HL
	POP	DE
	EX	DE,HL
	LD	A,H
	OR	L
	LD	H,$00
	LD	L,$00
	JR	Z,$+7
	LD	A,D
	OR	E
	JR	Z,$+3
	INC	HL
; if t26 == 0 goto _L90
	LD	A,H
	OR	L
	JP	Z,_L90
	LD	HL,(_STABLE)
	LD	HL,$0059
	LD	HL,(J)
	PUSH	HL
	POP	DE
	EX	DE,HL
	OR	A
	SBC	HL,DE
; array load: t32 = t27[t31]
	PUSH	HL
	POP	DE
	EX	DE,HL
	ADD	HL,HL
	ADD	HL,DE
	LD	E,(HL)
	INC	HL
	LD	D,(HL)
	EX	DE,HL
	LD	(I),HL
	JP	_L88
_L90:
	LD	HL,$00B4
	LD	HL,$010D
	PUSH	HL
	POP	DE
	EX	DE,HL
	OR	A
	SBC	HL,DE
	LD	HL,$0000
	JR	NC,$+3
	INC	HL
	PUSH	HL
	POP	DE
	EX	DE,HL
	EX	DE,HL
	OR	A
	SBC	HL,DE
	LD	HL,$0000
	JR	C,$+3
	INC	HL
	LD	A,L
	XOR	$01
	LD	L,A
	PUSH	HL
	POP	DE
	EX	DE,HL
	LD	A,H
	OR	L
	LD	H,$00
	LD	L,$00
	JR	Z,$+7
	LD	A,D
	OR	E
	JR	Z,$+3
	INC	HL
; if t37 == 0 goto _L91
	LD	A,H
	OR	L
	JP	Z,_L91
	LD	HL,(_STABLE)
	LD	HL,(J)
; array load: t41 = t38[t40]
	PUSH	HL
	POP	DE
	EX	DE,HL
	ADD	HL,HL
	ADD	HL,DE
	LD	E,(HL)
	INC	HL
	LD	D,(HL)
	EX	DE,HL
	LD	A,H
	CPL
	LD	H,A
	LD	A,L
	CPL
	LD	L,A
	INC	HL
	LD	(I),HL
	JP	_L88
_L91:
	LD	HL,$010E
	LD	HL,$0167
	PUSH	HL
	POP	DE
	EX	DE,HL
	OR	A
	SBC	HL,DE
	LD	HL,$0000
	JR	NC,$+3
	INC	HL
	PUSH	HL
	POP	DE
	EX	DE,HL
	EX	DE,HL
	OR	A
	SBC	HL,DE
	LD	HL,$0000
	JR	C,$+3
	INC	HL
	LD	A,L
	XOR	$01
	LD	L,A
	PUSH	HL
	POP	DE
	EX	DE,HL
	LD	A,H
	OR	L
	LD	H,$00
	LD	L,$00
	JR	Z,$+7
	LD	A,D
	OR	E
	JR	Z,$+3
	INC	HL
; if t47 == 0 goto _L92
	LD	A,H
	OR	L
	JP	Z,_L92
	LD	HL,(_STABLE)
	LD	HL,$0059
	LD	HL,(J)
	PUSH	HL
	POP	DE
	EX	DE,HL
	OR	A
	SBC	HL,DE
; array load: t53 = t48[t52]
	PUSH	HL
	POP	DE
	EX	DE,HL
	ADD	HL,HL
	ADD	HL,DE
	LD	E,(HL)
	INC	HL
	LD	D,(HL)
	EX	DE,HL
	LD	A,H
	CPL
	LD	H,A
	LD	A,L
	CPL
	LD	L,A
	INC	HL
	LD	(I),HL
	JP	_L88
_L92:
_L88:
	LD	HL,(I)
	LD	HL,$7FFF
	LD	HL,(HAN)
	PUSH	HL
	POP	DE
	EX	DE,HL
	CALL	SDIV16
	PUSH	HL
	POP	DE
	EX	DE,HL
	CALL	SDIV16
	LD	(I),HL
	LD	HL,(I)
; return t60
	JP	_EXIT
_EXIT:
	POP	IY
	RET

@ICOS:
; function @ICOS
	PUSH	IY
	LD	HL,(KAKU)
	LD	HL,$005A
	PUSH	HL
	POP	DE
	EX	DE,HL
	ADD	HL,DE
	PUSH	HL
	LD	HL,(HAN)
	PUSH	HL
	CALL	@ISIN
	LD	(KAKU),HL
	LD	HL,(KAKU)
; return t5
	JP	_EXIT
_EXIT:
	POP	IY
	RET

@IATN:
; function @IATN
	PUSH	IY
	LD	HL,(XX)
	PUSH	HL
	CALL	ABS
	LD	(X),HL
	LD	HL,(YY)
	PUSH	HL
	CALL	ABS
	LD	(Y),HL
	LD	HL,(X)
	LD	HL,(Y)
	PUSH	HL
	POP	DE
	EX	DE,HL
	EX	DE,HL
	OR	A
	SBC	HL,DE
	LD	HL,$0000
	JR	C,$+3
	INC	HL
; if t6 == 0 goto _L94
	LD	A,H
	OR	L
	JP	Z,_L94
	LD	HL,(_ATNTBL)
	LD	HL,(Y)
	LD	HL,$0040
	PUSH	HL
	POP	DE
	EX	DE,HL
	CALL	MUL16
	LD	HL,(X)
	PUSH	HL
	POP	DE
	EX	DE,HL
	CALL	DIV16
; array load: t14 = t7[t13]
	PUSH	HL
	POP	DE
	EX	DE,HL
	ADD	HL,HL
	ADD	HL,DE
	LD	E,(HL)
	INC	HL
	LD	D,(HL)
	EX	DE,HL
	LD	(DATA),HL
	JP	_L93
_L94:
	LD	HL,$005A
	LD	HL,(_ATNTBL)
	LD	HL,(X)
	LD	HL,$0040
	PUSH	HL
	POP	DE
	EX	DE,HL
	CALL	MUL16
	LD	HL,(Y)
	PUSH	HL
	POP	DE
	EX	DE,HL
	CALL	DIV16
; array load: t23 = t16[t22]
	PUSH	HL
	POP	DE
	EX	DE,HL
	ADD	HL,HL
	ADD	HL,DE
	LD	E,(HL)
	INC	HL
	LD	D,(HL)
	EX	DE,HL
	PUSH	HL
	POP	DE
	EX	DE,HL
	OR	A
	SBC	HL,DE
	LD	(DATA),HL
_L93:
	LD	HL,(YY)
	LD	HL,$0000
	PUSH	HL
	POP	DE
	EX	DE,HL
	CALL	SCMP_LT
; if t27 == 0 goto _L96
	LD	A,H
	OR	L
	JP	Z,_L96
	LD	HL,(XX)
	LD	HL,$0000
	PUSH	HL
	POP	DE
	EX	DE,HL
	CALL	SCMP_LT
; if t30 == 0 goto _L98
	LD	A,H
	OR	L
	JP	Z,_L98
	LD	HL,$00B4
	LD	HL,(DATA)
	PUSH	HL
	POP	DE
	EX	DE,HL
	ADD	HL,DE
; return t33
	JP	_EXIT
	JP	_L97
_L98:
	LD	HL,$0168
	LD	HL,(DATA)
	PUSH	HL
	POP	DE
	EX	DE,HL
	OR	A
	SBC	HL,DE
; return t36
	JP	_EXIT
_L97:
	JP	_L95
_L96:
	LD	HL,(XX)
	LD	HL,$0000
	PUSH	HL
	POP	DE
	EX	DE,HL
	CALL	SCMP_LT
; if t39 == 0 goto _L100
	LD	A,H
	OR	L
	JP	Z,_L100
	LD	HL,$00B4
	LD	HL,(DATA)
	PUSH	HL
	POP	DE
	EX	DE,HL
	OR	A
	SBC	HL,DE
; return t42
	JP	_EXIT
	JP	_L99
_L100:
	LD	HL,(DATA)
; return t43
	JP	_EXIT
_L99:
_L95:
_EXIT:
	POP	IY
	RET

@SCTL:
; function @SCTL
	PUSH	IY
; ARRAY PX[SLANGCompiler.Parser.Ast.IntegerLiteral]
; ARRAY PY[SLANGCompiler.Parser.Ast.IntegerLiteral]
; ARRAY PZ[SLANGCompiler.Parser.Ast.IntegerLiteral]
	LD	HL,(I)
	LD	HL,(_KSTEP)
	PUSH	HL
	POP	DE
	EX	DE,HL
	OR	A
	SBC	HL,DE
	LD	HL,(PX)
	LD	HL,$0003
; array store: t3[t4] = t2
	PUSH	HL
	POP	DE
	LD	(HL),E
	INC	HL
	LD	(HL),D
	LD	HL,(PX)
	LD	HL,$0003
; array load: t8 = t5[t7]
	PUSH	HL
	POP	DE
	EX	DE,HL
	ADD	HL,HL
	ADD	HL,DE
	LD	E,(HL)
	INC	HL
	LD	D,(HL)
	EX	DE,HL
	LD	HL,(PX)
	LD	HL,$0002
; array store: t9[t10] = t8
	PUSH	HL
	POP	DE
	LD	(HL),E
	INC	HL
	LD	(HL),D
	LD	HL,(I)
	LD	HL,(PX)
	LD	HL,$0001
; array store: t12[t13] = t11
	PUSH	HL
	POP	DE
	LD	(HL),E
	INC	HL
	LD	(HL),D
	LD	HL,(PX)
	LD	HL,$0001
; array load: t17 = t14[t16]
	PUSH	HL
	POP	DE
	EX	DE,HL
	ADD	HL,HL
	ADD	HL,DE
	LD	E,(HL)
	INC	HL
	LD	D,(HL)
	EX	DE,HL
	LD	HL,(PX)
	LD	HL,$0000
; array store: t18[t19] = t17
	PUSH	HL
	POP	DE
	LD	(HL),E
	INC	HL
	LD	(HL),D
	LD	HL,(J)
	LD	HL,(PZ)
	LD	HL,$0003
; array store: t21[t22] = t20
	PUSH	HL
	POP	DE
	LD	(HL),E
	INC	HL
	LD	(HL),D
	LD	HL,(J)
	LD	HL,(_KSTEP)
	PUSH	HL
	POP	DE
	EX	DE,HL
	OR	A
	SBC	HL,DE
	LD	HL,(PZ)
	LD	HL,$0002
; array store: t26[t27] = t25
	PUSH	HL
	POP	DE
	LD	(HL),E
	INC	HL
	LD	(HL),D
	LD	HL,(PZ)
	LD	HL,$0002
; array load: t31 = t28[t30]
	PUSH	HL
	POP	DE
	EX	DE,HL
	ADD	HL,HL
	ADD	HL,DE
	LD	E,(HL)
	INC	HL
	LD	D,(HL)
	EX	DE,HL
	LD	HL,(PZ)
	LD	HL,$0001
; array store: t32[t33] = t31
	PUSH	HL
	POP	DE
	LD	(HL),E
	INC	HL
	LD	(HL),D
	LD	HL,(PZ)
	LD	HL,$0003
; array load: t37 = t34[t36]
	PUSH	HL
	POP	DE
	EX	DE,HL
	ADD	HL,HL
	ADD	HL,DE
	LD	E,(HL)
	INC	HL
	LD	D,(HL)
	EX	DE,HL
	LD	HL,(PZ)
	LD	HL,$0000
; array store: t38[t39] = t37
	PUSH	HL
	POP	DE
	LD	(HL),E
	INC	HL
	LD	(HL),D
	LD	HL,(I)
	PUSH	HL
	LD	HL,(SNP)
	PUSH	HL
	CALL	@ISIN
	LD	(K),HL
	LD	HL,(J)
	PUSH	HL
	LD	HL,(K)
	PUSH	HL
	CALL	@ISIN
	LD	HL,(POSY)
	PUSH	HL
	POP	DE
	EX	DE,HL
	ADD	HL,DE
	LD	HL,(PY)
	LD	HL,$0000
; array store: t48[t49] = t47
	PUSH	HL
	POP	DE
	LD	(HL),E
	INC	HL
	LD	(HL),D
	LD	HL,(J)
	LD	HL,(_KSTEP)
	PUSH	HL
	POP	DE
	EX	DE,HL
	OR	A
	SBC	HL,DE
	PUSH	HL
	LD	HL,(K)
	PUSH	HL
	CALL	@ISIN
	LD	HL,(POSY)
	PUSH	HL
	POP	DE
	EX	DE,HL
	ADD	HL,DE
	LD	HL,(PY)
	LD	HL,$0001
; array store: t57[t58] = t56
	PUSH	HL
	POP	DE
	LD	(HL),E
	INC	HL
	LD	(HL),D
	LD	HL,(I)
	LD	HL,(_KSTEP)
	PUSH	HL
	POP	DE
	EX	DE,HL
	OR	A
	SBC	HL,DE
	PUSH	HL
	LD	HL,(SNP)
	PUSH	HL
	CALL	@ISIN
	LD	(K),HL
	LD	HL,(J)
	PUSH	HL
	LD	HL,(K)
	PUSH	HL
	CALL	@ISIN
	LD	HL,(POSY)
	PUSH	HL
	POP	DE
	EX	DE,HL
	ADD	HL,DE
	LD	HL,(PY)
	LD	HL,$0003
; array store: t69[t70] = t68
	PUSH	HL
	POP	DE
	LD	(HL),E
	INC	HL
	LD	(HL),D
	LD	HL,(J)
	LD	HL,(_KSTEP)
	PUSH	HL
	POP	DE
	EX	DE,HL
	OR	A
	SBC	HL,DE
	PUSH	HL
	LD	HL,(K)
	PUSH	HL
	CALL	@ISIN
	LD	HL,(POSY)
	PUSH	HL
	POP	DE
	EX	DE,HL
	ADD	HL,DE
	LD	HL,(PY)
	LD	HL,$0002
; array store: t78[t79] = t77
	PUSH	HL
	POP	DE
	LD	(HL),E
	INC	HL
	LD	(HL),D
	LD	HL,(PX)
	PUSH	HL
	LD	HL,(PY)
	PUSH	HL
	LD	HL,(PZ)
	PUSH	HL
	CALL	@CPOLY
_EXIT:
	POP	IY
	RET

@WAVE:
; function @WAVE
	PUSH	IY
	LD	HL,(XSIZE)
	LD	A,H
	CPL
	LD	H,A
	LD	A,L
	CPL
	LD	L,A
	INC	HL
	LD	(I),HL
_L101:
	LD	HL,(I)
	LD	HL,(_KSTEP)
	PUSH	HL
	POP	DE
	EX	DE,HL
	CALL	SCMP_LT
; if t4 == 0 goto _L102
	LD	A,H
	OR	L
	JP	Z,_L102
	LD	HL,(ZSIZE)
	LD	(J),HL
_L103:
	LD	HL,(J)
	LD	HL,$0000
	PUSH	HL
	POP	DE
	EX	DE,HL
	CALL	SCMP_GT
; if t8 == 0 goto _L104
	LD	A,H
	OR	L
	JP	Z,_L104
	LD	HL,(I)
	PUSH	HL
	LD	HL,(J)
	PUSH	HL
	LD	HL,(POSY)
	PUSH	HL
	LD	HL,(SNP)
	PUSH	HL
	CALL	@SCTL
	LD	HL,(J)
	LD	HL,(_KSTEP)
	PUSH	HL
	POP	DE
	EX	DE,HL
	OR	A
	SBC	HL,DE
	LD	(J),HL
	JP	_L103
_L104:
	LD	HL,(I)
	LD	HL,(_KSTEP)
	PUSH	HL
	POP	DE
	EX	DE,HL
	ADD	HL,DE
	LD	(I),HL
	JP	_L101
_L102:
	LD	HL,(XSIZE)
	LD	(I),HL
_L105:
	LD	HL,(I)
	LD	HL,(_KSTEP)
	LD	A,H
	CPL
	LD	H,A
	LD	A,L
	CPL
	LD	L,A
	INC	HL
	PUSH	HL
	POP	DE
	EX	DE,HL
	CALL	SCMP_GT
; if t24 == 0 goto _L106
	LD	A,H
	OR	L
	JP	Z,_L106
	LD	HL,(ZSIZE)
	LD	(J),HL
_L107:
	LD	HL,(J)
	LD	HL,$0000
	PUSH	HL
	POP	DE
	EX	DE,HL
	CALL	SCMP_GT
; if t28 == 0 goto _L108
	LD	A,H
	OR	L
	JP	Z,_L108
	LD	HL,(I)
	PUSH	HL
	LD	HL,(J)
	PUSH	HL
	LD	HL,(POSY)
	PUSH	HL
	LD	HL,(SNP)
	PUSH	HL
	CALL	@SCTL
	LD	HL,(J)
	LD	HL,(_KSTEP)
	PUSH	HL
	POP	DE
	EX	DE,HL
	OR	A
	SBC	HL,DE
	LD	(J),HL
	JP	_L107
_L108:
	LD	HL,(I)
	LD	HL,(_KSTEP)
	PUSH	HL
	POP	DE
	EX	DE,HL
	OR	A
	SBC	HL,DE
	LD	(I),HL
	JP	_L105
_L106:
_EXIT:
	POP	IY
	RET

MAIN:
; function MAIN
	PUSH	IY
	CALL	@INIT
	LD	HL,$0000
	LD	(I),HL
_L109:
	LD	HL,(I)
	LD	HL,$0008
	PUSH	HL
	POP	DE
	EX	DE,HL
	CALL	MOD16
	LD	(COL),HL
	LD	HL,(I)
	LD	HL,$000A
	PUSH	HL
	POP	DE
	EX	DE,HL
	CALL	MUL16
	PUSH	HL
	LD	HL,(I)
	LD	HL,$0004
	PUSH	HL
	POP	DE
	EX	DE,HL
	CALL	MUL16
	PUSH	HL
	LD	HL,$027F
	LD	HL,(I)
	LD	HL,$000A
	PUSH	HL
	POP	DE
	EX	DE,HL
	CALL	MUL16
	PUSH	HL
	POP	DE
	EX	DE,HL
	OR	A
	SBC	HL,DE
	PUSH	HL
	LD	HL,$00C7
	LD	HL,(I)
	LD	HL,$0004
	PUSH	HL
	POP	DE
	EX	DE,HL
	CALL	MUL16
	PUSH	HL
	POP	DE
	EX	DE,HL
	OR	A
	SBC	HL,DE
	PUSH	HL
	LD	HL,(COL)
	PUSH	HL
	CALL	@BOXC
_L110:
	LD	HL,(I)
	LD	HL,$0001
	PUSH	HL
	POP	DE
	EX	DE,HL
	ADD	HL,DE
	LD	(I),HL
	LD	HL,$0010
	PUSH	HL
	POP	DE
	EX	DE,HL
	EX	DE,HL
	OR	A
	SBC	HL,DE
	LD	HL,$0000
	JR	C,$+3
	INC	HL
	LD	A,L
	XOR	$01
	LD	L,A
; if t27 != 0 goto _L109
	LD	A,H
	OR	L
	JP	NZ,_L109
_L111:
	CALL	INPUT
	CALL	@INIT
	LD	HL,$0007
	PUSH	HL
	CALL	@GRAD
	LD	HL,$0000
	LD	(I),HL
_L112:
	LD	HL,(I)
	LD	HL,$0008
	PUSH	HL
	POP	DE
	EX	DE,HL
	CALL	MOD16
	LD	(COL),HL
	LD	HL,(I)
	LD	HL,$000A
	PUSH	HL
	POP	DE
	EX	DE,HL
	CALL	MUL16
	PUSH	HL
	LD	HL,(I)
	LD	HL,$0004
	PUSH	HL
	POP	DE
	EX	DE,HL
	CALL	MUL16
	PUSH	HL
	LD	HL,$027F
	LD	HL,(I)
	LD	HL,$000A
	PUSH	HL
	POP	DE
	EX	DE,HL
	CALL	MUL16
	PUSH	HL
	POP	DE
	EX	DE,HL
	OR	A
	SBC	HL,DE
	PUSH	HL
	LD	HL,$00C7
	LD	HL,(I)
	LD	HL,$0004
	PUSH	HL
	POP	DE
	EX	DE,HL
	CALL	MUL16
	PUSH	HL
	POP	DE
	EX	DE,HL
	OR	A
	SBC	HL,DE
	PUSH	HL
	LD	HL,(COL)
	PUSH	HL
	CALL	@FULLC
_L113:
	LD	HL,(I)
	LD	HL,$0001
	PUSH	HL
	POP	DE
	EX	DE,HL
	ADD	HL,DE
	LD	(I),HL
	LD	HL,$0010
	PUSH	HL
	POP	DE
	EX	DE,HL
	EX	DE,HL
	OR	A
	SBC	HL,DE
	LD	HL,$0000
	JR	C,$+3
	INC	HL
	LD	A,L
	XOR	$01
	LD	L,A
; if t58 != 0 goto _L112
	LD	A,H
	OR	L
	JP	NZ,_L112
_L114:
	CALL	INPUT
	LD	HL,$0000
	LD	(I),HL
_L115:
	LD	HL,$0258
	PUSH	HL
	CALL	RND
	PUSH	HL
	LD	HL,$00C8
	PUSH	HL
	CALL	RND
	PUSH	HL
	LD	HL,$0258
	PUSH	HL
	CALL	RND
	PUSH	HL
	LD	HL,$00C8
	PUSH	HL
	CALL	RND
	PUSH	HL
	LD	HL,$0258
	PUSH	HL
	CALL	RND
	PUSH	HL
	LD	HL,$00C8
	PUSH	HL
	CALL	RND
	PUSH	HL
	LD	HL,$0008
	PUSH	HL
	CALL	RND
	PUSH	HL
	CALL	@TRIANGLEC
_L116:
	LD	HL,(I)
	LD	HL,$0001
	PUSH	HL
	POP	DE
	EX	DE,HL
	ADD	HL,DE
	LD	(I),HL
	LD	HL,$001E
	PUSH	HL
	POP	DE
	EX	DE,HL
	EX	DE,HL
	OR	A
	SBC	HL,DE
	LD	HL,$0000
	JR	C,$+3
	INC	HL
	LD	A,L
	XOR	$01
	LD	L,A
; if t80 != 0 goto _L115
	LD	A,H
	OR	L
	JP	NZ,_L115
_L117:
	CALL	INPUT
	LD	HL,$0000
	LD	(I),HL
_L118:
	LD	HL,$0258
	PUSH	HL
	CALL	RND
	PUSH	HL
	LD	HL,$00C8
	PUSH	HL
	CALL	RND
	PUSH	HL
	LD	HL,$0258
	PUSH	HL
	CALL	RND
	PUSH	HL
	LD	HL,$00C8
	PUSH	HL
	CALL	RND
	PUSH	HL
	LD	HL,$0258
	PUSH	HL
	CALL	RND
	PUSH	HL
	LD	HL,$00C8
	PUSH	HL
	CALL	RND
	PUSH	HL
	LD	HL,$0008
	PUSH	HL
	CALL	RND
	PUSH	HL
	CALL	@SPLINEC
_L119:
	LD	HL,(I)
	LD	HL,$0001
	PUSH	HL
	POP	DE
	EX	DE,HL
	ADD	HL,DE
	LD	(I),HL
	LD	HL,$001E
	PUSH	HL
	POP	DE
	EX	DE,HL
	EX	DE,HL
	OR	A
	SBC	HL,DE
	LD	HL,$0000
	JR	C,$+3
	INC	HL
	LD	A,L
	XOR	$01
	LD	L,A
; if t102 != 0 goto _L118
	LD	A,H
	OR	L
	JP	NZ,_L118
_L120:
	CALL	INPUT
	LD	HL,$0000
	LD	(I),HL
_L121:
	LD	HL,$0258
	PUSH	HL
	CALL	RND
	PUSH	HL
	LD	HL,$00C8
	PUSH	HL
	CALL	RND
	PUSH	HL
	LD	HL,$000A
	LD	HL,$0064
	PUSH	HL
	CALL	RND
	PUSH	HL
	POP	DE
	EX	DE,HL
	ADD	HL,DE
	PUSH	HL
	LD	HL,$0008
	PUSH	HL
	CALL	RND
	PUSH	HL
	CALL	@CIRCLEC
_L122:
	LD	HL,(I)
	LD	HL,$0001
	PUSH	HL
	POP	DE
	EX	DE,HL
	ADD	HL,DE
	LD	(I),HL
	LD	HL,$001E
	PUSH	HL
	POP	DE
	EX	DE,HL
	EX	DE,HL
	OR	A
	SBC	HL,DE
	LD	HL,$0000
	JR	C,$+3
	INC	HL
	LD	A,L
	XOR	$01
	LD	L,A
; if t120 != 0 goto _L121
	LD	A,H
	OR	L
	JP	NZ,_L121
_L123:
	CALL	INPUT
	LD	HL,$0000
	LD	(I),HL
_L124:
	LD	HL,$0280
	PUSH	HL
	CALL	RND
	PUSH	HL
	LD	HL,$00C8
	PUSH	HL
	CALL	RND
	PUSH	HL
	LD	HL,$0280
	PUSH	HL
	CALL	RND
	PUSH	HL
	LD	HL,$00C8
	PUSH	HL
	CALL	RND
	PUSH	HL
	LD	HL,$0008
	PUSH	HL
	CALL	RND
	PUSH	HL
	CALL	@LINEC
	LD	HL,$0000
	PUSH	HL
	CALL	INKEY
	LD	HL,$0020
	PUSH	HL
	POP	DE
	EX	DE,HL
	OR	A
	SBC	HL,DE
	LD	HL,$0000
	JR	Z,$+3
	INC	HL
; if t137 == 0 goto _L127
	LD	A,H
	OR	L
	JP	Z,_L127
	JP	_L126
_L127:
_L125:
	LD	HL,(I)
	LD	HL,$0001
	PUSH	HL
	POP	DE
	EX	DE,HL
	ADD	HL,DE
	LD	(I),HL
	LD	HL,$03E8
	PUSH	HL
	POP	DE
	EX	DE,HL
	EX	DE,HL
	OR	A
	SBC	HL,DE
	LD	HL,$0000
	JR	C,$+3
	INC	HL
	LD	A,L
	XOR	$01
	LD	L,A
; if t142 != 0 goto _L124
	LD	A,H
	OR	L
	JP	NZ,_L124
_L126:
	CALL	INPUT
	CALL	@INIT
_EXIT:
	POP	IY
	RET
