; CONST CONV
; CONST CONV2
; global: LoadConst t0 #28672
; VAR AVAL : t0
; ARRAY IVAL[]
; ARRAY AIVAL[]
; ARRAY AR[SLANGCompiler.Parser.Ast.IntegerLiteral]
; ARRAY ARB[SLANGCompiler.Parser.Ast.IntegerLiteral]
; ARRAY AR2[SLANGCompiler.Parser.Ast.IntegerLiteral,SLANGCompiler.Parser.Ast.IntegerLiteral]
; ARRAY ARB2[SLANGCompiler.Parser.Ast.IntegerLiteral,SLANGCompiler.Parser.Ast.IntegerLiteral]
; ARRAY AAR[SLANGCompiler.Parser.Ast.IntegerLiteral]
; ARRAY AARB[SLANGCompiler.Parser.Ast.IntegerLiteral]
; ARRAY A2[SLANGCompiler.Parser.Ast.IntegerLiteral,SLANGCompiler.Parser.Ast.IntegerLiteral]
; ARRAY ARI[SLANGCompiler.Parser.Ast.IntegerLiteral]
;   = {initial code}
; global: LoadConst t0 #14
; global init: IVV = t0

MAIN:
; function MAIN
	PUSH	IY
; ARRAY LAR[SLANGCompiler.Parser.Ast.IntegerLiteral,SLANGCompiler.Parser.Ast.IntegerLiteral]
; ARRAY LARB[SLANGCompiler.Parser.Ast.IntegerLiteral,SLANGCompiler.Parser.Ast.IntegerLiteral]
	LD	HL,$0001
	LD	(TNUM),HL
; load string "CONST TEST"
	LD	HL,0 ; string placeholder
	CALL	__print_str
	CALL	__strfunc_/
	CALL	__print_newline
	LD	HL,(CONV)
	LD	HL,$007B
	PUSH	HL
	POP	DE
	EX	DE,HL
	OR	A
	SBC	HL,DE
	LD	HL,$0000
	JR	Z,$+3
	INC	HL
; if t5 == 0 goto _L1
	LD	A,H
	OR	L
	JP	Z,_L1
	LD	HL,(TNUM)
	CALL	__print_num
; load string "OK"
	LD	HL,0 ; string placeholder
	CALL	__print_str
	CALL	__strfunc_/
	CALL	__print_newline
	JP	_L0
_L1:
	LD	HL,(TNUM)
	CALL	__print_num
; load string "NG"
	LD	HL,0 ; string placeholder
	CALL	__print_str
	CALL	__strfunc_/
	CALL	__print_newline
_L0:
	LD	HL,(TNUM)
	LD	HL,$0001
	PUSH	HL
	POP	DE
	EX	DE,HL
	ADD	HL,DE
	LD	(TNUM),HL
; load string "VAR TEST"
	LD	HL,0 ; string placeholder
	CALL	__print_str
	CALL	__strfunc_/
	CALL	__print_newline
	LD	HL,(CONV)
	LD	(VAL),HL
	LD	HL,(VAL)
	LD	HL,$007B
	PUSH	HL
	POP	DE
	EX	DE,HL
	OR	A
	SBC	HL,DE
	LD	HL,$0000
	JR	Z,$+3
	INC	HL
; if t20 == 0 goto _L3
	LD	A,H
	OR	L
	JP	Z,_L3
	LD	HL,(TNUM)
	CALL	__print_num
; load string "OK"
	LD	HL,0 ; string placeholder
	CALL	__print_str
	CALL	__strfunc_/
	CALL	__print_newline
	JP	_L2
_L3:
	LD	HL,(TNUM)
	CALL	__print_num
; load string "NG"
	LD	HL,0 ; string placeholder
	CALL	__print_str
	CALL	__strfunc_/
	CALL	__print_newline
_L2:
	LD	HL,(TNUM)
	LD	HL,$0001
	PUSH	HL
	POP	DE
	EX	DE,HL
	ADD	HL,DE
	LD	(TNUM),HL
	LD	HL,(VAL)
	LD	(AVAL),HL
	LD	HL,(AVAL)
	LD	HL,$007B
	PUSH	HL
	POP	DE
	EX	DE,HL
	OR	A
	SBC	HL,DE
	LD	HL,$0000
	JR	Z,$+3
	INC	HL
; if t33 == 0 goto _L5
	LD	A,H
	OR	L
	JP	Z,_L5
	LD	HL,(TNUM)
	CALL	__print_num
; load string "OK"
	LD	HL,0 ; string placeholder
	CALL	__print_str
	CALL	__strfunc_/
	CALL	__print_newline
	JP	_L4
_L5:
	LD	HL,(TNUM)
	CALL	__print_num
; load string "NG"
	LD	HL,0 ; string placeholder
	CALL	__print_str
	CALL	__strfunc_/
	CALL	__print_newline
_L4:
	LD	HL,(TNUM)
	LD	HL,$0001
	PUSH	HL
	POP	DE
	EX	DE,HL
	ADD	HL,DE
	LD	(TNUM),HL
; load string "INDIRECT VAR TEST"
	LD	HL,0 ; string placeholder
	CALL	__print_str
	CALL	__strfunc_/
	CALL	__print_newline
	LD	HL,$7000
	LD	(IVAL),HL
	LD	HL,(IVAL)
	LD	HL,$0000
; array load: t49 = t46[t48]
	PUSH	HL
	POP	DE
	EX	DE,HL
	ADD	HL,HL
	ADD	HL,DE
	LD	E,(HL)
	INC	HL
	LD	D,(HL)
	EX	DE,HL
	LD	HL,$007B
	PUSH	HL
	POP	DE
	EX	DE,HL
	OR	A
	SBC	HL,DE
	LD	HL,$0000
	JR	Z,$+3
	INC	HL
; if t51 == 0 goto _L7
	LD	A,H
	OR	L
	JP	Z,_L7
	LD	HL,(TNUM)
	CALL	__print_num
; load string "OK"
	LD	HL,0 ; string placeholder
	CALL	__print_str
	CALL	__strfunc_/
	CALL	__print_newline
	JP	_L6
_L7:
	LD	HL,(TNUM)
	CALL	__print_num
; load string "NG"
	LD	HL,0 ; string placeholder
	CALL	__print_str
	CALL	__strfunc_/
	CALL	__print_newline
_L6:
	LD	HL,(TNUM)
	LD	HL,$0001
	PUSH	HL
	POP	DE
	EX	DE,HL
	ADD	HL,DE
	LD	(TNUM),HL
	LD	HL,$01C8
	LD	HL,(IVAL)
	LD	HL,$0000
; array store: t62[t63] = t61
	PUSH	HL
	POP	DE
	LD	(HL),E
	INC	HL
	LD	(HL),D
	LD	HL,(AVAL)
	LD	HL,$01C8
	PUSH	HL
	POP	DE
	EX	DE,HL
	OR	A
	SBC	HL,DE
	LD	HL,$0000
	JR	Z,$+3
	INC	HL
; if t66 == 0 goto _L9
	LD	A,H
	OR	L
	JP	Z,_L9
	LD	HL,(TNUM)
	CALL	__print_num
; load string "OK"
	LD	HL,0 ; string placeholder
	CALL	__print_str
	CALL	__strfunc_/
	CALL	__print_newline
	JP	_L8
_L9:
	LD	HL,(TNUM)
	CALL	__print_num
; load string "NG"
	LD	HL,0 ; string placeholder
	CALL	__print_str
	CALL	__strfunc_/
	CALL	__print_newline
_L8:
	LD	HL,(TNUM)
	LD	HL,$0001
	PUSH	HL
	POP	DE
	EX	DE,HL
	ADD	HL,DE
	LD	(TNUM),HL
	LD	HL,$7000
	LD	(AIVAL),HL
	LD	HL,(AIVAL)
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
	LD	HL,$01C8
	PUSH	HL
	POP	DE
	EX	DE,HL
	OR	A
	SBC	HL,DE
	LD	HL,$0000
	JR	Z,$+3
	INC	HL
; if t82 == 0 goto _L11
	LD	A,H
	OR	L
	JP	Z,_L11
	LD	HL,(TNUM)
	CALL	__print_num
; load string "OK"
	LD	HL,0 ; string placeholder
	CALL	__print_str
	CALL	__strfunc_/
	CALL	__print_newline
	JP	_L10
_L11:
	LD	HL,(TNUM)
	CALL	__print_num
; load string "NG"
	LD	HL,0 ; string placeholder
	CALL	__print_str
	CALL	__strfunc_/
	CALL	__print_newline
_L10:
	LD	HL,(TNUM)
	LD	HL,$0001
	PUSH	HL
	POP	DE
	EX	DE,HL
	ADD	HL,DE
	LD	(TNUM),HL
	LD	HL,$000A
	LD	HL,(AR)
	LD	HL,$0003
; array store: t93[t94] = t92
	PUSH	HL
	POP	DE
	LD	(HL),E
	INC	HL
	LD	(HL),D
	LD	HL,$0014
	LD	HL,(AR)
	LD	HL,$0004
; array store: t96[t97] = t95
	PUSH	HL
	POP	DE
	LD	(HL),E
	INC	HL
	LD	(HL),D
	LD	HL,(AR)
	LD	HL,$0003
; array load: t101 = t98[t100]
	PUSH	HL
	POP	DE
	EX	DE,HL
	ADD	HL,HL
	ADD	HL,DE
	LD	E,(HL)
	INC	HL
	LD	D,(HL)
	EX	DE,HL
	LD	HL,$000A
	PUSH	HL
	POP	DE
	EX	DE,HL
	OR	A
	SBC	HL,DE
	LD	HL,$0000
	JR	Z,$+3
	INC	HL
	LD	HL,(AR)
	LD	HL,$0004
; array load: t107 = t104[t106]
	PUSH	HL
	POP	DE
	EX	DE,HL
	ADD	HL,HL
	ADD	HL,DE
	LD	E,(HL)
	INC	HL
	LD	D,(HL)
	EX	DE,HL
	LD	HL,$0014
	PUSH	HL
	POP	DE
	EX	DE,HL
	OR	A
	SBC	HL,DE
	LD	HL,$0000
	JR	Z,$+3
	INC	HL
	PUSH	HL
	POP	DE
	EX	DE,HL
	LD	A,H
	AND	D
	LD	H,A
	LD	A,L
	AND	E
	LD	L,A
; if t110 == 0 goto _L13
	LD	A,H
	OR	L
	JP	Z,_L13
	LD	HL,(TNUM)
	CALL	__print_num
; load string "OK"
	LD	HL,0 ; string placeholder
	CALL	__print_str
	CALL	__strfunc_/
	CALL	__print_newline
	JP	_L12
_L13:
	LD	HL,(TNUM)
	CALL	__print_num
; load string "NG"
	LD	HL,0 ; string placeholder
	CALL	__print_str
	CALL	__strfunc_/
	CALL	__print_newline
_L12:
	LD	HL,(TNUM)
	LD	HL,$0001
	PUSH	HL
	POP	DE
	EX	DE,HL
	ADD	HL,DE
	LD	(TNUM),HL
; load string "Expr TEST"
	LD	HL,0 ; string placeholder
	CALL	__print_str
	CALL	__strfunc_/
	CALL	__print_newline
	LD	HL,$0064
	LD	(NUM),HL
	LD	HL,(NUM)
	LD	HL,$0003
	PUSH	HL
	POP	DE
	EX	DE,HL
	CALL	MUL16
	LD	HL,(NUM)
	LD	HL,(NUM)
	PUSH	HL
	POP	DE
	EX	DE,HL
	CALL	MUL16
	LD	HL,$000A
	PUSH	HL
	POP	DE
	EX	DE,HL
	CALL	DIV16
	PUSH	HL
	POP	DE
	EX	DE,HL
	ADD	HL,DE
	LD	(VAL),HL
	LD	HL,(VAL)
	LD	HL,$0514
	PUSH	HL
	POP	DE
	EX	DE,HL
	OR	A
	SBC	HL,DE
	LD	HL,$0000
	JR	Z,$+3
	INC	HL
; if t134 == 0 goto _L15
	LD	A,H
	OR	L
	JP	Z,_L15
	LD	HL,(TNUM)
	CALL	__print_num
; load string "OK"
	LD	HL,0 ; string placeholder
	CALL	__print_str
	CALL	__strfunc_/
	CALL	__print_newline
	JP	_L14
_L15:
	LD	HL,(TNUM)
	CALL	__print_num
; load string "NG"
	LD	HL,0 ; string placeholder
	CALL	__print_str
	LD	HL,(VAL)
	CALL	__print_num
	CALL	__strfunc_/
	CALL	__print_newline
_L14:
	LD	HL,(TNUM)
	LD	HL,$0001
	PUSH	HL
	POP	DE
	EX	DE,HL
	ADD	HL,DE
	LD	(TNUM),HL
	LD	HL,$0001
	LD	(NUM),HL
	LD	HL,$0064
	LD	HL,(IVAL)
	LD	HL,(NUM)
; array store: t147[t148] = t146
	PUSH	HL
	POP	DE
	LD	(HL),E
	INC	HL
	LD	(HL),D
	LD	HL,(IVAL)
	LD	HL,(NUM)
; array load: t152 = t149[t151]
	PUSH	HL
	POP	DE
	EX	DE,HL
	ADD	HL,HL
	ADD	HL,DE
	LD	E,(HL)
	INC	HL
	LD	D,(HL)
	EX	DE,HL
	LD	HL,$0003
	PUSH	HL
	POP	DE
	EX	DE,HL
	CALL	MUL16
	LD	HL,(IVAL)
	LD	HL,(NUM)
; array load: t158 = t155[t157]
	PUSH	HL
	POP	DE
	EX	DE,HL
	ADD	HL,HL
	ADD	HL,DE
	LD	E,(HL)
	INC	HL
	LD	D,(HL)
	EX	DE,HL
	LD	HL,(IVAL)
	LD	HL,(NUM)
; array load: t162 = t159[t161]
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
	CALL	MUL16
	LD	HL,$000A
	PUSH	HL
	POP	DE
	EX	DE,HL
	CALL	DIV16
	PUSH	HL
	POP	DE
	EX	DE,HL
	ADD	HL,DE
	LD	(VAL),HL
	LD	HL,(VAL)
	LD	HL,$0514
	PUSH	HL
	POP	DE
	EX	DE,HL
	OR	A
	SBC	HL,DE
	LD	HL,$0000
	JR	Z,$+3
	INC	HL
; if t169 == 0 goto _L17
	LD	A,H
	OR	L
	JP	Z,_L17
	LD	HL,(TNUM)
	CALL	__print_num
; load string "OK"
	LD	HL,0 ; string placeholder
	CALL	__print_str
	CALL	__strfunc_/
	CALL	__print_newline
	JP	_L16
_L17:
	LD	HL,(TNUM)
	CALL	__print_num
; load string "NG"
	LD	HL,0 ; string placeholder
	CALL	__print_str
	LD	HL,(VAL)
	CALL	__print_num
	CALL	__strfunc_/
	CALL	__print_newline
_L16:
	LD	HL,(TNUM)
	LD	HL,$0001
	PUSH	HL
	POP	DE
	EX	DE,HL
	ADD	HL,DE
	LD	(TNUM),HL
	LD	HL,$0000
	LD	(NUM),HL
	LD	HL,$0064
	LD	HL,(IVAL)
	LD	HL,(NUM)
	LD	HL,$0001
	PUSH	HL
	POP	DE
	EX	DE,HL
	ADD	HL,DE
; array store: t182[t185] = t181
	PUSH	HL
	POP	DE
	LD	(HL),E
	INC	HL
	LD	(HL),D
	LD	HL,(IVAL)
	LD	HL,(NUM)
	LD	HL,$0001
	PUSH	HL
	POP	DE
	EX	DE,HL
	ADD	HL,DE
; array load: t191 = t186[t190]
	PUSH	HL
	POP	DE
	EX	DE,HL
	ADD	HL,HL
	ADD	HL,DE
	LD	E,(HL)
	INC	HL
	LD	D,(HL)
	EX	DE,HL
	LD	HL,$0003
	PUSH	HL
	POP	DE
	EX	DE,HL
	CALL	MUL16
	LD	HL,(IVAL)
	LD	HL,(NUM)
	LD	HL,$0001
	PUSH	HL
	POP	DE
	EX	DE,HL
	ADD	HL,DE
; array load: t199 = t194[t198]
	PUSH	HL
	POP	DE
	EX	DE,HL
	ADD	HL,HL
	ADD	HL,DE
	LD	E,(HL)
	INC	HL
	LD	D,(HL)
	EX	DE,HL
	LD	HL,(IVAL)
	LD	HL,(NUM)
	LD	HL,$0001
	PUSH	HL
	POP	DE
	EX	DE,HL
	ADD	HL,DE
; array load: t205 = t200[t204]
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
	CALL	MUL16
	LD	HL,$000A
	PUSH	HL
	POP	DE
	EX	DE,HL
	CALL	DIV16
	PUSH	HL
	POP	DE
	EX	DE,HL
	ADD	HL,DE
	LD	(VAL),HL
	LD	HL,(VAL)
	LD	HL,$0514
	PUSH	HL
	POP	DE
	EX	DE,HL
	OR	A
	SBC	HL,DE
	LD	HL,$0000
	JR	Z,$+3
	INC	HL
; if t212 == 0 goto _L19
	LD	A,H
	OR	L
	JP	Z,_L19
	LD	HL,(TNUM)
	CALL	__print_num
; load string "OK"
	LD	HL,0 ; string placeholder
	CALL	__print_str
	CALL	__strfunc_/
	CALL	__print_newline
	JP	_L18
_L19:
	LD	HL,(TNUM)
	CALL	__print_num
; load string "NG"
	LD	HL,0 ; string placeholder
	CALL	__print_str
	LD	HL,(VAL)
	CALL	__print_num
	CALL	__strfunc_/
	CALL	__print_newline
_L18:
	LD	HL,(TNUM)
	LD	HL,$0001
	PUSH	HL
	POP	DE
	EX	DE,HL
	ADD	HL,DE
	LD	(TNUM),HL
	LD	HL,$0064
	LD	A,H
	CPL
	LD	H,A
	LD	A,L
	CPL
	LD	L,A
	INC	HL
	LD	(NUM),HL
	LD	HL,(NUM)
	LD	HL,$0002
	PUSH	HL
	POP	DE
	EX	DE,HL
	CALL	SDIV16
	LD	(VAL),HL
	LD	HL,(VAL)
	LD	HL,$0032
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
	OR	A
	SBC	HL,DE
	LD	HL,$0000
	JR	Z,$+3
	INC	HL
; if t231 == 0 goto _L21
	LD	A,H
	OR	L
	JP	Z,_L21
	LD	HL,(TNUM)
	CALL	__print_num
; load string "OK"
	LD	HL,0 ; string placeholder
	CALL	__print_str
	CALL	__strfunc_/
	CALL	__print_newline
	JP	_L20
_L21:
	LD	HL,(TNUM)
	CALL	__print_num
; load string "NG"
	LD	HL,0 ; string placeholder
	CALL	__print_str
	LD	HL,(VAL)
	CALL	__print_num
	CALL	__strfunc_/
	CALL	__print_newline
_L20:
	LD	HL,(TNUM)
	LD	HL,$0001
	PUSH	HL
	POP	DE
	EX	DE,HL
	ADD	HL,DE
	LD	(TNUM),HL
	LD	HL,(VAL)
	LD	HL,$000A
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
	LD	HL,(VAL)
	LD	HL,$0033
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
	PUSH	HL
	POP	DE
	EX	DE,HL
	LD	A,H
	AND	D
	LD	H,A
	LD	A,L
	AND	E
	LD	L,A
	LD	HL,(VAL)
	LD	HL,$000A
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
	CALL	SCMP_LE
	PUSH	HL
	POP	DE
	EX	DE,HL
	LD	A,H
	AND	D
	LD	H,A
	LD	A,L
	AND	E
	LD	L,A
	LD	HL,(VAL)
	LD	HL,$0033
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
	CALL	SCMP_GE
	PUSH	HL
	POP	DE
	EX	DE,HL
	LD	A,H
	AND	D
	LD	H,A
	LD	A,L
	AND	E
	LD	L,A
; if t260 == 0 goto _L23
	LD	A,H
	OR	L
	JP	Z,_L23
	LD	HL,(TNUM)
	CALL	__print_num
; load string "OK"
	LD	HL,0 ; string placeholder
	CALL	__print_str
	CALL	__strfunc_/
	CALL	__print_newline
	JP	_L22
_L23:
	LD	HL,(TNUM)
	CALL	__print_num
; load string "NG"
	LD	HL,0 ; string placeholder
	CALL	__print_str
	LD	HL,(VAL)
	CALL	__print_num
	CALL	__strfunc_/
	CALL	__print_newline
_L22:
	LD	HL,(TNUM)
	LD	HL,$0001
	PUSH	HL
	POP	DE
	EX	DE,HL
	ADD	HL,DE
	LD	(TNUM),HL
	LD	HL,$0032
	LD	(VAL),HL
	LD	HL,(VAL)
	LD	HL,$000A
	PUSH	HL
	POP	DE
	EX	DE,HL
	EX	DE,HL
	OR	A
	SBC	HL,DE
	LD	HL,$0000
	JR	C,$+3
	INC	HL
	LD	HL,(VAL)
	LD	HL,$0033
	PUSH	HL
	POP	DE
	EX	DE,HL
	OR	A
	SBC	HL,DE
	LD	HL,$0000
	JR	C,$+3
	INC	HL
	PUSH	HL
	POP	DE
	EX	DE,HL
	LD	A,H
	AND	D
	LD	H,A
	LD	A,L
	AND	E
	LD	L,A
	LD	HL,(VAL)
	LD	HL,$0033
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
	AND	D
	LD	H,A
	LD	A,L
	AND	E
	LD	L,A
	LD	HL,(VAL)
	LD	HL,$0032
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
	LD	A,H
	AND	D
	LD	H,A
	LD	A,L
	AND	E
	LD	L,A
; if t286 == 0 goto _L25
	LD	A,H
	OR	L
	JP	Z,_L25
	LD	HL,(TNUM)
	CALL	__print_num
; load string "OK"
	LD	HL,0 ; string placeholder
	CALL	__print_str
	CALL	__strfunc_/
	CALL	__print_newline
	JP	_L24
_L25:
	LD	HL,(TNUM)
	CALL	__print_num
; load string "NG"
	LD	HL,0 ; string placeholder
	CALL	__print_str
	LD	HL,(VAL)
	CALL	__print_num
	CALL	__strfunc_/
	CALL	__print_newline
_L24:
	LD	HL,(TNUM)
	LD	HL,$0001
	PUSH	HL
	POP	DE
	EX	DE,HL
	ADD	HL,DE
	LD	(TNUM),HL
; load string "IF TEST"
	LD	HL,0 ; string placeholder
	CALL	__print_str
	CALL	__strfunc_/
	CALL	__print_newline
	LD	HL,$0032
	LD	(VAL),HL
	LD	HL,(VAL)
	LD	HL,$0032
	PUSH	HL
	POP	DE
	EX	DE,HL
	EX	DE,HL
	OR	A
	SBC	HL,DE
	LD	HL,$0000
	JR	C,$+3
	INC	HL
; if t302 == 0 goto _L27
	LD	A,H
	OR	L
	JP	Z,_L27
	LD	HL,(TNUM)
	CALL	__print_num
; load string "NG"
	LD	HL,0 ; string placeholder
	CALL	__print_str
	CALL	__strfunc_/
	CALL	__print_newline
	JP	_L26
_L27:
	LD	HL,(VAL)
	LD	HL,$0031
	PUSH	HL
	POP	DE
	EX	DE,HL
	EX	DE,HL
	OR	A
	SBC	HL,DE
	LD	HL,$0000
	JR	C,$+3
	INC	HL
; if t308 == 0 goto _L26
	LD	A,H
	OR	L
	JP	Z,_L26
	LD	HL,(TNUM)
	CALL	__print_num
; load string "OK"
	LD	HL,0 ; string placeholder
	CALL	__print_str
	CALL	__strfunc_/
	CALL	__print_newline
_L26:
	LD	HL,(TNUM)
	LD	HL,$0001
	PUSH	HL
	POP	DE
	EX	DE,HL
	ADD	HL,DE
	LD	(TNUM),HL
	LD	HL,$0032
	LD	(VAL),HL
	LD	HL,(VAL)
	LD	HL,$0032
	PUSH	HL
	POP	DE
	EX	DE,HL
	EX	DE,HL
	OR	A
	SBC	HL,DE
	LD	HL,$0000
	JR	C,$+3
	INC	HL
; if t318 == 0 goto _L29
	LD	A,H
	OR	L
	JP	Z,_L29
	LD	HL,(TNUM)
	CALL	__print_num
; load string "NG"
	LD	HL,0 ; string placeholder
	CALL	__print_str
	CALL	__strfunc_/
	CALL	__print_newline
	JP	_L28
_L29:
	LD	HL,(VAL)
	LD	HL,$0033
	PUSH	HL
	POP	DE
	EX	DE,HL
	EX	DE,HL
	OR	A
	SBC	HL,DE
	LD	HL,$0000
	JR	C,$+3
	INC	HL
; if t324 == 0 goto _L30
	LD	A,H
	OR	L
	JP	Z,_L30
	LD	HL,(TNUM)
	CALL	__print_num
; load string "NG"
	LD	HL,0 ; string placeholder
	CALL	__print_str
	CALL	__strfunc_/
	CALL	__print_newline
	JP	_L28
_L30:
	LD	HL,(TNUM)
	CALL	__print_num
; load string "OK"
	LD	HL,0 ; string placeholder
	CALL	__print_str
	CALL	__strfunc_/
	CALL	__print_newline
_L28:
	LD	HL,(VAL)
	LD	HL,$0031
	PUSH	HL
	POP	DE
	EX	DE,HL
	EX	DE,HL
	OR	A
	SBC	HL,DE
	LD	HL,$0000
	JR	C,$+3
	INC	HL
; if t333 == 0 goto _L32
	LD	A,H
	OR	L
	JP	Z,_L32
	LD	HL,(TNUM)
	CALL	__print_num
; load string "OK"
	LD	HL,0 ; string placeholder
	CALL	__print_str
	CALL	__strfunc_/
	CALL	__print_newline
	JP	_L31
_L32:
	LD	HL,(VAL)
	LD	HL,$0033
	PUSH	HL
	POP	DE
	EX	DE,HL
	EX	DE,HL
	OR	A
	SBC	HL,DE
	LD	HL,$0000
	JR	C,$+3
	INC	HL
; if t339 == 0 goto _L33
	LD	A,H
	OR	L
	JP	Z,_L33
	LD	HL,(TNUM)
	CALL	__print_num
; load string "NG"
	LD	HL,0 ; string placeholder
	CALL	__print_str
	CALL	__strfunc_/
	CALL	__print_newline
	JP	_L31
_L33:
	LD	HL,(TNUM)
	CALL	__print_num
; load string "NG"
	LD	HL,0 ; string placeholder
	CALL	__print_str
	CALL	__strfunc_/
	CALL	__print_newline
_L31:
	LD	HL,(VAL)
	LD	HL,$0032
	PUSH	HL
	POP	DE
	EX	DE,HL
	EX	DE,HL
	OR	A
	SBC	HL,DE
	LD	HL,$0000
	JR	C,$+3
	INC	HL
; if t348 == 0 goto _L35
	LD	A,H
	OR	L
	JP	Z,_L35
	LD	HL,(TNUM)
	CALL	__print_num
; load string "NG"
	LD	HL,0 ; string placeholder
	CALL	__print_str
	CALL	__strfunc_/
	CALL	__print_newline
	JP	_L34
_L35:
	LD	HL,(VAL)
	LD	HL,$0033
	PUSH	HL
	POP	DE
	EX	DE,HL
	OR	A
	SBC	HL,DE
	LD	HL,$0000
	JR	C,$+3
	INC	HL
; if t354 == 0 goto _L36
	LD	A,H
	OR	L
	JP	Z,_L36
	LD	HL,(TNUM)
	CALL	__print_num
; load string "OK"
	LD	HL,0 ; string placeholder
	CALL	__print_str
	CALL	__strfunc_/
	CALL	__print_newline
	JP	_L34
_L36:
	LD	HL,(TNUM)
	CALL	__print_num
; load string "NG"
	LD	HL,0 ; string placeholder
	CALL	__print_str
	CALL	__strfunc_/
	CALL	__print_newline
_L34:
	LD	HL,(TNUM)
	LD	HL,$0001
	PUSH	HL
	POP	DE
	EX	DE,HL
	ADD	HL,DE
	LD	(TNUM),HL
	LD	HL,(VAL)
	LD	HL,$0032
	PUSH	HL
	POP	DE
	EX	DE,HL
	OR	A
	SBC	HL,DE
	LD	HL,$0000
	JR	Z,$+3
	INC	HL
; if t366 == 0 goto _L38
	LD	A,H
	OR	L
	JP	Z,_L38
	LD	HL,(VAL)
	LD	HL,$0032
	PUSH	HL
	POP	DE
	EX	DE,HL
	EX	DE,HL
	OR	A
	SBC	HL,DE
	LD	HL,$0000
	JR	C,$+3
	INC	HL
; if t369 == 0 goto _L40
	LD	A,H
	OR	L
	JP	Z,_L40
	LD	HL,(TNUM)
	CALL	__print_num
; load string "NG"
	LD	HL,0 ; string placeholder
	CALL	__print_str
	CALL	__strfunc_/
	CALL	__print_newline
	JP	_L39
_L40:
	LD	HL,(TNUM)
	CALL	__print_num
; load string "OK"
	LD	HL,0 ; string placeholder
	CALL	__print_str
	CALL	__strfunc_/
	CALL	__print_newline
_L39:
; load string " -> OK"
	LD	HL,0 ; string placeholder
	CALL	__print_str
	CALL	__strfunc_/
	CALL	__print_newline
	JP	_L37
_L38:
	LD	HL,(TNUM)
	CALL	__print_num
; load string "NG2"
	LD	HL,0 ; string placeholder
	CALL	__print_str
	CALL	__strfunc_/
	CALL	__print_newline
_L37:
	LD	HL,(TNUM)
	LD	HL,$0001
	PUSH	HL
	POP	DE
	EX	DE,HL
	ADD	HL,DE
	LD	(TNUM),HL
; load string "WHILE TEST"
	LD	HL,0 ; string placeholder
	CALL	__print_str
	CALL	__strfunc_/
	CALL	__print_newline
	LD	HL,$0014
	LD	(VAL),HL
; load string "COUNT "
	LD	HL,0 ; string placeholder
	CALL	__print_str
_L41:
	LD	HL,(VAL)
	LD	HL,$000A
	PUSH	HL
	POP	DE
	EX	DE,HL
	EX	DE,HL
	OR	A
	SBC	HL,DE
	LD	HL,$0000
	JR	C,$+3
	INC	HL
; if t390 == 0 goto _L42
	LD	A,H
	OR	L
	JP	Z,_L42
	LD	HL,(VAL)
	CALL	__print_num
; load string " "
	LD	HL,0 ; string placeholder
	CALL	__print_str
	LD	HL,(VAL)
	LD	HL,$0001
	PUSH	HL
	POP	DE
	EX	DE,HL
	OR	A
	SBC	HL,DE
	LD	(VAL),HL
	JP	_L41
_L42:
	CALL	__strfunc_/
	CALL	__print_newline
	LD	HL,(VAL)
	LD	HL,$000A
	PUSH	HL
	POP	DE
	EX	DE,HL
	OR	A
	SBC	HL,DE
	LD	HL,$0000
	JR	Z,$+3
	INC	HL
; if t399 == 0 goto _L44
	LD	A,H
	OR	L
	JP	Z,_L44
	LD	HL,(TNUM)
	CALL	__print_num
; load string "OK"
	LD	HL,0 ; string placeholder
	CALL	__print_str
	CALL	__strfunc_/
	CALL	__print_newline
	JP	_L43
_L44:
	LD	HL,(TNUM)
	CALL	__print_num
; load string "NG"
	LD	HL,0 ; string placeholder
	CALL	__print_str
	CALL	__strfunc_/
	CALL	__print_newline
_L43:
	LD	HL,(TNUM)
	LD	HL,$0001
	PUSH	HL
	POP	DE
	EX	DE,HL
	ADD	HL,DE
	LD	(TNUM),HL
	LD	HL,$000A
	LD	(VAL),HL
_L45:
	LD	HL,(VAL)
	LD	HL,$0000
	PUSH	HL
	POP	DE
	EX	DE,HL
	OR	A
	SBC	HL,DE
	LD	HL,$0000
	JR	NC,$+3
	INC	HL
; if t412 == 0 goto _L46
	LD	A,H
	OR	L
	JP	Z,_L46
	LD	HL,(VAL)
	LD	HL,$0005
	PUSH	HL
	POP	DE
	EX	DE,HL
	OR	A
	SBC	HL,DE
	LD	HL,$0000
	JR	Z,$+3
	INC	HL
; if t415 == 0 goto _L47
	LD	A,H
	OR	L
	JP	Z,_L47
	JP	_L46
_L47:
	LD	HL,(VAL)
	LD	HL,$0001
	PUSH	HL
	POP	DE
	EX	DE,HL
	OR	A
	SBC	HL,DE
	LD	(VAL),HL
	JP	_L45
_L46:
	LD	HL,(VAL)
	LD	HL,$0005
	PUSH	HL
	POP	DE
	EX	DE,HL
	OR	A
	SBC	HL,DE
	LD	HL,$0000
	JR	Z,$+3
	INC	HL
; if t421 == 0 goto _L49
	LD	A,H
	OR	L
	JP	Z,_L49
	LD	HL,(TNUM)
	CALL	__print_num
; load string "OK"
	LD	HL,0 ; string placeholder
	CALL	__print_str
	CALL	__strfunc_/
	CALL	__print_newline
	JP	_L48
_L49:
	LD	HL,(TNUM)
	CALL	__print_num
; load string "NG"
	LD	HL,0 ; string placeholder
	CALL	__print_str
	CALL	__strfunc_/
	CALL	__print_newline
_L48:
	LD	HL,(TNUM)
	LD	HL,$0001
	PUSH	HL
	POP	DE
	EX	DE,HL
	ADD	HL,DE
	LD	(TNUM),HL
	LD	HL,$000A
	LD	(VAL),HL
; load string "WHILE "
	LD	HL,0 ; string placeholder
	CALL	__print_str
_L50:
	LD	HL,(VAL)
	LD	HL,$0000
	PUSH	HL
	POP	DE
	EX	DE,HL
	EX	DE,HL
	OR	A
	SBC	HL,DE
	LD	HL,$0000
	JR	C,$+3
	INC	HL
; if t435 == 0 goto _L51
	LD	A,H
	OR	L
	JP	Z,_L51
_L52:
	LD	HL,(VAL)
	LD	HL,$0005
	PUSH	HL
	POP	DE
	EX	DE,HL
	EX	DE,HL
	OR	A
	SBC	HL,DE
	LD	HL,$0000
	JR	C,$+3
	INC	HL
; if t438 == 0 goto _L53
	LD	A,H
	OR	L
	JP	Z,_L53
	LD	HL,(VAL)
	LD	HL,$0007
	PUSH	HL
	POP	DE
	EX	DE,HL
	OR	A
	SBC	HL,DE
	LD	HL,$0000
	JR	Z,$+3
	INC	HL
; if t441 == 0 goto _L54
	LD	A,H
	OR	L
	JP	Z,_L54
	JP	_L53
_L54:
; load string "["
	LD	HL,0 ; string placeholder
	CALL	__print_str
	LD	HL,(VAL)
	CALL	__print_num
; load string "] "
	LD	HL,0 ; string placeholder
	CALL	__print_str
	LD	HL,(VAL)
	LD	HL,$0001
	PUSH	HL
	POP	DE
	EX	DE,HL
	OR	A
	SBC	HL,DE
	LD	(VAL),HL
	JP	_L52
_L53:
	LD	HL,(VAL)
	CALL	__print_num
; load string " "
	LD	HL,0 ; string placeholder
	CALL	__print_str
	LD	HL,(VAL)
	LD	HL,$0001
	PUSH	HL
	POP	DE
	EX	DE,HL
	OR	A
	SBC	HL,DE
	LD	(VAL),HL
	JP	_L50
_L51:
	CALL	__strfunc_/
	CALL	__print_newline
	LD	HL,(VAL)
	LD	HL,$0000
	PUSH	HL
	POP	DE
	EX	DE,HL
	OR	A
	SBC	HL,DE
	LD	HL,$0000
	JR	Z,$+3
	INC	HL
; if t456 == 0 goto _L55
	LD	A,H
	OR	L
	JP	Z,_L55
	LD	HL,(TNUM)
	CALL	__print_num
; load string "OK"
	LD	HL,0 ; string placeholder
	CALL	__print_str
	CALL	__strfunc_/
	CALL	__print_newline
_L55:
	LD	HL,(TNUM)
	LD	HL,$0001
	PUSH	HL
	POP	DE
	EX	DE,HL
	ADD	HL,DE
	LD	(TNUM),HL
; load string "REPEAT UNTIL TEST"
	LD	HL,0 ; string placeholder
	CALL	__print_str
	LD	HL,$0000
	LD	(VAL),HL
_L56:
	LD	HL,(VAL)
	CALL	__print_num
; load string " "
	LD	HL,0 ; string placeholder
	CALL	__print_str
	LD	HL,(VAL)
	LD	HL,$0001
	PUSH	HL
	POP	DE
	EX	DE,HL
	ADD	HL,DE
	LD	(VAL),HL
	LD	HL,(VAL)
	LD	HL,$000A
	PUSH	HL
	POP	DE
	EX	DE,HL
	OR	A
	SBC	HL,DE
	LD	HL,$0000
	JR	Z,$+3
	INC	HL
; if t472 == 0 goto _L56
	LD	A,H
	OR	L
	JP	Z,_L56
_L57:
	LD	HL,(VAL)
	CALL	__print_num
	CALL	__strfunc_/
	CALL	__print_newline
	LD	HL,(VAL)
	LD	HL,$000A
	PUSH	HL
	POP	DE
	EX	DE,HL
	OR	A
	SBC	HL,DE
	LD	HL,$0000
	JR	Z,$+3
	INC	HL
; if t477 == 0 goto _L59
	LD	A,H
	OR	L
	JP	Z,_L59
	LD	HL,(TNUM)
	CALL	__print_num
; load string "OK"
	LD	HL,0 ; string placeholder
	CALL	__print_str
	CALL	__strfunc_/
	CALL	__print_newline
	JP	_L58
_L59:
	LD	HL,(TNUM)
	CALL	__print_num
; load string "NG"
	LD	HL,0 ; string placeholder
	CALL	__print_str
	CALL	__strfunc_/
	CALL	__print_newline
_L58:
	LD	HL,(TNUM)
	LD	HL,$0001
	PUSH	HL
	POP	DE
	EX	DE,HL
	ADD	HL,DE
	LD	(TNUM),HL
; load string "FOR TEST"
	LD	HL,0 ; string placeholder
	CALL	__print_str
	LD	HL,$0000
	LD	(VAL),HL
_L60:
	LD	HL,(VAL)
	CALL	__print_num
; load string " "
	LD	HL,0 ; string placeholder
	CALL	__print_str
_L61:
	LD	HL,(VAL)
	LD	HL,$0001
	PUSH	HL
	POP	DE
	EX	DE,HL
	ADD	HL,DE
	LD	(VAL),HL
	LD	HL,$000A
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
; if t495 != 0 goto _L60
	LD	A,H
	OR	L
	JP	NZ,_L60
_L62:
	CALL	__strfunc_/
	CALL	__print_newline
	LD	HL,(VAL)
	LD	HL,$000B
	PUSH	HL
	POP	DE
	EX	DE,HL
	OR	A
	SBC	HL,DE
	LD	HL,$0000
	JR	Z,$+3
	INC	HL
; if t499 == 0 goto _L64
	LD	A,H
	OR	L
	JP	Z,_L64
	LD	HL,(TNUM)
	CALL	__print_num
; load string "OK"
	LD	HL,0 ; string placeholder
	CALL	__print_str
	CALL	__strfunc_/
	CALL	__print_newline
	JP	_L63
_L64:
	LD	HL,(TNUM)
	CALL	__print_num
; load string "NG"
	LD	HL,0 ; string placeholder
	CALL	__print_str
	CALL	__strfunc_/
	CALL	__print_newline
_L63:
	LD	HL,(TNUM)
	LD	HL,$0001
	PUSH	HL
	POP	DE
	EX	DE,HL
	ADD	HL,DE
	LD	(TNUM),HL
	LD	HL,$000A
	LD	(VAL),HL
_L65:
	LD	HL,(VAL)
	CALL	__print_num
; load string " "
	LD	HL,0 ; string placeholder
	CALL	__print_str
_L66:
	LD	HL,(VAL)
	LD	HL,$0001
	PUSH	HL
	POP	DE
	EX	DE,HL
	OR	A
	SBC	HL,DE
	LD	(VAL),HL
	LD	HL,$0000
	PUSH	HL
	POP	DE
	EX	DE,HL
	OR	A
	SBC	HL,DE
	LD	HL,$0000
	JR	NC,$+3
	INC	HL
; if t516 != 0 goto _L65
	LD	A,H
	OR	L
	JP	NZ,_L65
_L67:
	CALL	__strfunc_/
	CALL	__print_newline
	LD	HL,(VAL)
	LD	HL,$0001
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
	OR	A
	SBC	HL,DE
	LD	HL,$0000
	JR	Z,$+3
	INC	HL
; if t521 == 0 goto _L69
	LD	A,H
	OR	L
	JP	Z,_L69
	LD	HL,(TNUM)
	CALL	__print_num
; load string "OK"
	LD	HL,0 ; string placeholder
	CALL	__print_str
	CALL	__strfunc_/
	CALL	__print_newline
	JP	_L68
_L69:
	LD	HL,(TNUM)
	CALL	__print_num
; load string "NG"
	LD	HL,0 ; string placeholder
	CALL	__print_str
	CALL	__strfunc_/
	CALL	__print_newline
_L68:
	LD	HL,(TNUM)
	LD	HL,$0001
	PUSH	HL
	POP	DE
	EX	DE,HL
	ADD	HL,DE
	LD	(TNUM),HL
; load string "LABEL TEST"
	LD	HL,0 ; string placeholder
	CALL	__print_str
	JP	LABEL1
	LD	HL,(TNUM)
	CALL	__print_num
; load string "NG"
	LD	HL,0 ; string placeholder
	CALL	__print_str
	CALL	__strfunc_/
	CALL	__print_newline
LABEL1:
	LD	HL,(TNUM)
	CALL	__print_num
; load string "OK"
	LD	HL,0 ; string placeholder
	CALL	__print_str
	CALL	__strfunc_/
	CALL	__print_newline
	LD	HL,(TNUM)
	LD	HL,$0001
	PUSH	HL
	POP	DE
	EX	DE,HL
	ADD	HL,DE
	LD	(TNUM),HL
; load string "CASE TEST"
	LD	HL,0 ; string placeholder
	CALL	__print_str
	LD	HL,$0005
	LD	(VAL),HL
	LD	HL,(VAL)
	LD	HL,$0000
	LD	HL,$0004
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
; if t548 == 0 goto _L71
	LD	A,H
	OR	L
	JP	Z,_L71
	LD	HL,(TNUM)
	CALL	__print_num
; load string "NG"
	LD	HL,0 ; string placeholder
	CALL	__print_str
	CALL	__strfunc_/
	CALL	__print_newline
	JP	_L70
_L71:
	LD	HL,$0005
	PUSH	HL
	POP	DE
	EX	DE,HL
	OR	A
	SBC	HL,DE
	LD	HL,$0000
	JR	Z,$+3
	INC	HL
; if t553 == 0 goto _L72
	LD	A,H
	OR	L
	JP	Z,_L72
	LD	HL,(TNUM)
	CALL	__print_num
; load string "OK"
	LD	HL,0 ; string placeholder
	CALL	__print_str
	CALL	__strfunc_/
	CALL	__print_newline
	JP	_L70
_L72:
	LD	HL,(TNUM)
	CALL	__print_num
; load string "NG"
	LD	HL,0 ; string placeholder
	CALL	__print_str
	CALL	__strfunc_/
	CALL	__print_newline
	JP	_L70
_L70:
	LD	HL,(TNUM)
	LD	HL,$0001
	PUSH	HL
	POP	DE
	EX	DE,HL
	ADD	HL,DE
	LD	(TNUM),HL
	LD	HL,$0005
	LD	(VAL),HL
	LD	HL,(VAL)
	LD	HL,$0000
	PUSH	HL
	POP	DE
	EX	DE,HL
	OR	A
	SBC	HL,DE
	LD	HL,$0000
	JR	Z,$+3
	INC	HL
; if t566 == 0 goto _L74
	LD	A,H
	OR	L
	JP	Z,_L74
	LD	HL,(TNUM)
	CALL	__print_num
; load string "NG"
	LD	HL,0 ; string placeholder
	CALL	__print_str
	CALL	__strfunc_/
	CALL	__print_newline
	JP	_L73
_L74:
	LD	HL,$0001
	LD	HL,$0005
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
; if t574 == 0 goto _L75
	LD	A,H
	OR	L
	JP	Z,_L75
	LD	HL,(TNUM)
	CALL	__print_num
; load string "OK"
	LD	HL,0 ; string placeholder
	CALL	__print_str
	CALL	__strfunc_/
	CALL	__print_newline
	JP	_L73
_L75:
	LD	HL,(TNUM)
	CALL	__print_num
; load string "NG"
	LD	HL,0 ; string placeholder
	CALL	__print_str
	CALL	__strfunc_/
	CALL	__print_newline
	JP	_L73
_L73:
	LD	HL,(TNUM)
	LD	HL,$0001
	PUSH	HL
	POP	DE
	EX	DE,HL
	ADD	HL,DE
	LD	(TNUM),HL
	LD	HL,$0006
	LD	(VAL),HL
	LD	HL,(VAL)
	LD	HL,$0000
	PUSH	HL
	POP	DE
	EX	DE,HL
	OR	A
	SBC	HL,DE
	LD	HL,$0000
	JR	Z,$+3
	INC	HL
; if t587 == 0 goto _L77
	LD	A,H
	OR	L
	JP	Z,_L77
	LD	HL,(TNUM)
	CALL	__print_num
; load string "NG"
	LD	HL,0 ; string placeholder
	CALL	__print_str
	CALL	__strfunc_/
	CALL	__print_newline
	JP	_L76
_L77:
	LD	HL,$0001
	LD	HL,$0005
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
; if t595 == 0 goto _L78
	LD	A,H
	OR	L
	JP	Z,_L78
	LD	HL,(TNUM)
	CALL	__print_num
; load string "NG"
	LD	HL,0 ; string placeholder
	CALL	__print_str
	CALL	__strfunc_/
	CALL	__print_newline
	JP	_L76
_L78:
	LD	HL,(TNUM)
	CALL	__print_num
; load string "OK"
	LD	HL,0 ; string placeholder
	CALL	__print_str
	CALL	__strfunc_/
	CALL	__print_newline
	JP	_L76
_L76:
	LD	HL,(TNUM)
	LD	HL,$0001
	PUSH	HL
	POP	DE
	EX	DE,HL
	ADD	HL,DE
	LD	(TNUM),HL
	LD	HL,$0008
	LD	(VAL),HL
	LD	HL,(VAL)
	LD	HL,$0000
	PUSH	HL
	POP	DE
	EX	DE,HL
	OR	A
	SBC	HL,DE
	LD	HL,$0000
	JR	Z,$+3
	INC	HL
; if t608 == 0 goto _L80
	LD	A,H
	OR	L
	JP	Z,_L80
	LD	HL,(TNUM)
	CALL	__print_num
; load string "NG"
	LD	HL,0 ; string placeholder
	CALL	__print_str
	CALL	__strfunc_/
	CALL	__print_newline
	JP	_L79
_L80:
	LD	HL,$0001
	LD	HL,$0005
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
; if t616 == 0 goto _L81
	LD	A,H
	OR	L
	JP	Z,_L81
	LD	HL,(TNUM)
	CALL	__print_num
; load string "NG"
	LD	HL,0 ; string placeholder
	CALL	__print_str
	CALL	__strfunc_/
	CALL	__print_newline
	JP	_L79
_L81:
	LD	HL,$0006
	PUSH	HL
	POP	DE
	EX	DE,HL
	OR	A
	SBC	HL,DE
	LD	HL,$0000
	JR	Z,$+3
	INC	HL
; if t621 == 0 goto _L82
	LD	A,H
	OR	L
	JP	Z,_L82
	JP	_L79
_L82:
	LD	HL,$0007
	PUSH	HL
	POP	DE
	EX	DE,HL
	OR	A
	SBC	HL,DE
	LD	HL,$0000
	JR	Z,$+3
	INC	HL
; if t623 == 0 goto _L83
	LD	A,H
	OR	L
	JP	Z,_L83
	JP	_L79
_L83:
	LD	HL,$0008
	PUSH	HL
	POP	DE
	EX	DE,HL
	OR	A
	SBC	HL,DE
	LD	HL,$0000
	JR	Z,$+3
	INC	HL
; if t625 == 0 goto _L84
	LD	A,H
	OR	L
	JP	Z,_L84
	LD	HL,(TNUM)
	CALL	__print_num
; load string "OK"
	LD	HL,0 ; string placeholder
	CALL	__print_str
	CALL	__strfunc_/
	CALL	__print_newline
	JP	_L79
_L84:
	LD	HL,(TNUM)
	CALL	__print_num
; load string "NG"
	LD	HL,0 ; string placeholder
	CALL	__print_str
	CALL	__strfunc_/
	CALL	__print_newline
	JP	_L79
_L79:
	LD	HL,(TNUM)
	LD	HL,$0001
	PUSH	HL
	POP	DE
	EX	DE,HL
	ADD	HL,DE
	LD	(TNUM),HL
; load string "FUNC TEST"
	LD	HL,0 ; string placeholder
	CALL	__print_str
	LD	HL,$0005
	LD	(VAL),HL
	LD	HL,(VAL)
	PUSH	HL
	LD	HL,$000A
	PUSH	HL
	CALL	FUNC1
	LD	(NUM),HL
; load string "MAIN VAL:"
	LD	HL,0 ; string placeholder
	CALL	__print_str
	LD	HL,(VAL)
	CALL	__print_num
	CALL	__strfunc_/
	CALL	__print_newline
	LD	HL,(VAL)
	LD	HL,$0005
	PUSH	HL
	POP	DE
	EX	DE,HL
	OR	A
	SBC	HL,DE
	LD	HL,$0000
	JR	Z,$+3
	INC	HL
; if t645 == 0 goto _L86
	LD	A,H
	OR	L
	JP	Z,_L86
	LD	HL,(TNUM)
	CALL	__print_num
; load string "OK"
	LD	HL,0 ; string placeholder
	CALL	__print_str
	CALL	__strfunc_/
	CALL	__print_newline
	JP	_L85
_L86:
	LD	HL,(TNUM)
	CALL	__print_num
; load string "NG"
	LD	HL,0 ; string placeholder
	CALL	__print_str
	CALL	__strfunc_/
	CALL	__print_newline
_L85:
	LD	HL,(TNUM)
	LD	HL,$0001
	PUSH	HL
	POP	DE
	EX	DE,HL
	ADD	HL,DE
	LD	(TNUM),HL
	LD	HL,(NUM)
	LD	HL,$00E4
	PUSH	HL
	POP	DE
	EX	DE,HL
	OR	A
	SBC	HL,DE
	LD	HL,$0000
	JR	Z,$+3
	INC	HL
; if t657 == 0 goto _L88
	LD	A,H
	OR	L
	JP	Z,_L88
	LD	HL,(TNUM)
	CALL	__print_num
; load string "OK"
	LD	HL,0 ; string placeholder
	CALL	__print_str
	CALL	__strfunc_/
	CALL	__print_newline
	JP	_L87
_L88:
	LD	HL,(TNUM)
	CALL	__print_num
; load string "NG"
	LD	HL,0 ; string placeholder
	CALL	__print_str
	CALL	__strfunc_/
	CALL	__print_newline
_L87:
	LD	HL,(TNUM)
	LD	HL,$0001
	PUSH	HL
	POP	DE
	EX	DE,HL
	ADD	HL,DE
	LD	(TNUM),HL
; load string "FUNC RETURN TEST"
	LD	HL,0 ; string placeholder
	CALL	__print_str
	LD	HL,$000A
	PUSH	HL
	CALL	FUNC2
	LD	(NUM),HL
	LD	HL,(NUM)
	LD	HL,$0001
	PUSH	HL
	POP	DE
	EX	DE,HL
	OR	A
	SBC	HL,DE
	LD	HL,$0000
	JR	Z,$+3
	INC	HL
; if t672 == 0 goto _L90
	LD	A,H
	OR	L
	JP	Z,_L90
	LD	HL,(TNUM)
	CALL	__print_num
; load string "OK"
	LD	HL,0 ; string placeholder
	CALL	__print_str
	CALL	__strfunc_/
	CALL	__print_newline
	JP	_L89
_L90:
	LD	HL,(TNUM)
	CALL	__print_num
; load string "NG"
	LD	HL,0 ; string placeholder
	CALL	__print_str
	CALL	__strfunc_/
	CALL	__print_newline
_L89:
	LD	HL,(TNUM)
	LD	HL,$0001
	PUSH	HL
	POP	DE
	EX	DE,HL
	ADD	HL,DE
	LD	(TNUM),HL
	LD	HL,$0064
	PUSH	HL
	CALL	FUNC2
	LD	(NUM),HL
	LD	HL,(NUM)
	LD	HL,$000A
	PUSH	HL
	POP	DE
	EX	DE,HL
	OR	A
	SBC	HL,DE
	LD	HL,$0000
	JR	Z,$+3
	INC	HL
; if t686 == 0 goto _L92
	LD	A,H
	OR	L
	JP	Z,_L92
	LD	HL,(TNUM)
	CALL	__print_num
; load string "OK"
	LD	HL,0 ; string placeholder
	CALL	__print_str
	CALL	__strfunc_/
	CALL	__print_newline
	JP	_L91
_L92:
	LD	HL,(TNUM)
	CALL	__print_num
; load string "NG"
	LD	HL,0 ; string placeholder
	CALL	__print_str
	CALL	__strfunc_/
	CALL	__print_newline
_L91:
	LD	HL,(TNUM)
	LD	HL,$0001
	PUSH	HL
	POP	DE
	EX	DE,HL
	ADD	HL,DE
	LD	(TNUM),HL
; load string "MEM[] MEMW[] TEST"
	LD	HL,0 ; string placeholder
	CALL	__print_str
	LD	HL,$04D2
	LD	(AVAL),HL
	LD	HL,(MEMW)
	LD	HL,$7000
; array load: t701 = t698[t700]
	PUSH	HL
	POP	DE
	EX	DE,HL
	ADD	HL,HL
	ADD	HL,DE
	LD	E,(HL)
	INC	HL
	LD	D,(HL)
	EX	DE,HL
	LD	HL,$04D2
	PUSH	HL
	POP	DE
	EX	DE,HL
	OR	A
	SBC	HL,DE
	LD	HL,$0000
	JR	Z,$+3
	INC	HL
; if t703 == 0 goto _L94
	LD	A,H
	OR	L
	JP	Z,_L94
	LD	HL,(TNUM)
	CALL	__print_num
; load string "OK "
	LD	HL,0 ; string placeholder
	CALL	__print_str
	JP	_L93
_L94:
	LD	HL,(TNUM)
	CALL	__print_num
; load string "NG"
	LD	HL,0 ; string placeholder
	CALL	__print_str
	CALL	__strfunc_/
	CALL	__print_newline
_L93:
	LD	HL,(TNUM)
	LD	HL,$0001
	PUSH	HL
	POP	DE
	EX	DE,HL
	ADD	HL,DE
	LD	(TNUM),HL
	LD	HL,(MEM)
	LD	HL,$7000
; array load: t715 = t712[t714]
	PUSH	HL
	POP	DE
	EX	DE,HL
	ADD	HL,HL
	ADD	HL,DE
	LD	E,(HL)
	INC	HL
	LD	D,(HL)
	EX	DE,HL
	LD	HL,$04D2
	LD	H,$00
	PUSH	HL
	POP	DE
	EX	DE,HL
	OR	A
	SBC	HL,DE
	LD	HL,$0000
	JR	Z,$+3
	INC	HL
	LD	HL,(MEM)
	LD	HL,$7001
; array load: t722 = t719[t721]
	PUSH	HL
	POP	DE
	EX	DE,HL
	ADD	HL,HL
	ADD	HL,DE
	LD	E,(HL)
	INC	HL
	LD	D,(HL)
	EX	DE,HL
	LD	HL,$04D2
	LD	L,H
	LD	H,$00
	PUSH	HL
	POP	DE
	EX	DE,HL
	OR	A
	SBC	HL,DE
	LD	HL,$0000
	JR	Z,$+3
	INC	HL
	PUSH	HL
	POP	DE
	EX	DE,HL
	LD	A,H
	AND	D
	LD	H,A
	LD	A,L
	AND	E
	LD	L,A
; if t726 == 0 goto _L96
	LD	A,H
	OR	L
	JP	Z,_L96
	LD	HL,(TNUM)
	CALL	__print_num
; load string "OK"
	LD	HL,0 ; string placeholder
	CALL	__print_str
	CALL	__strfunc_/
	CALL	__print_newline
	JP	_L95
_L96:
	LD	HL,(TNUM)
	CALL	__print_num
; load string "NG"
	LD	HL,0 ; string placeholder
	CALL	__print_str
	CALL	__strfunc_/
	CALL	__print_newline
_L95:
	LD	HL,(TNUM)
	LD	HL,$0001
	PUSH	HL
	POP	DE
	EX	DE,HL
	ADD	HL,DE
	LD	(TNUM),HL
	LD	HL,$8000
	LD	HL,(MEMW)
	LD	HL,$8000
; array store: t737[t738] = t736
	PUSH	HL
	POP	DE
	LD	(HL),E
	INC	HL
	LD	(HL),D
	LD	HL,(MEMW)
	LD	HL,$8000
; array load: t742 = t739[t741]
	PUSH	HL
	POP	DE
	EX	DE,HL
	ADD	HL,HL
	ADD	HL,DE
	LD	E,(HL)
	INC	HL
	LD	D,(HL)
	EX	DE,HL
	LD	(VAL),HL
	LD	HL,(VAL)
	LD	HL,$8000
	PUSH	HL
	POP	DE
	EX	DE,HL
	OR	A
	SBC	HL,DE
	LD	HL,$0000
	JR	Z,$+3
	INC	HL
; if t745 == 0 goto _L98
	LD	A,H
	OR	L
	JP	Z,_L98
	LD	HL,(TNUM)
	CALL	__print_num
; load string "OK "
	LD	HL,0 ; string placeholder
	CALL	__print_str
	JP	_L97
_L98:
	LD	HL,(TNUM)
	CALL	__print_num
; load string "NG"
	LD	HL,0 ; string placeholder
	CALL	__print_str
	CALL	__strfunc_/
	CALL	__print_newline
_L97:
	LD	HL,(TNUM)
	LD	HL,$0001
	PUSH	HL
	POP	DE
	EX	DE,HL
	ADD	HL,DE
	LD	(TNUM),HL
	LD	HL,$0002
	LD	HL,(MEMW)
	LD	HL,(VAL)
; array store: t755[t756] = t754
	PUSH	HL
	POP	DE
	LD	(HL),E
	INC	HL
	LD	(HL),D
	LD	HL,(MEMW)
	LD	HL,(VAL)
; array load: t760 = t757[t759]
	PUSH	HL
	POP	DE
	EX	DE,HL
	ADD	HL,HL
	ADD	HL,DE
	LD	E,(HL)
	INC	HL
	LD	D,(HL)
	EX	DE,HL
	LD	(NUM),HL
	LD	HL,(NUM)
	LD	HL,$0002
	PUSH	HL
	POP	DE
	EX	DE,HL
	OR	A
	SBC	HL,DE
	LD	HL,$0000
	JR	Z,$+3
	INC	HL
; if t763 == 0 goto _L100
	LD	A,H
	OR	L
	JP	Z,_L100
	LD	HL,(TNUM)
	CALL	__print_num
; load string "OK "
	LD	HL,0 ; string placeholder
	CALL	__print_str
	JP	_L99
_L100:
	LD	HL,(TNUM)
	CALL	__print_num
; load string "NG"
	LD	HL,0 ; string placeholder
	CALL	__print_str
	CALL	__strfunc_/
	CALL	__print_newline
_L99:
	LD	HL,(TNUM)
	LD	HL,$0001
	PUSH	HL
	POP	DE
	EX	DE,HL
	ADD	HL,DE
	LD	(TNUM),HL
	LD	HL,$0064
	LD	HL,(MEMW)
	LD	HL,(VAL)
	LD	HL,(NUM)
	PUSH	HL
	POP	DE
	EX	DE,HL
	ADD	HL,DE
; array store: t773[t776] = t772
	PUSH	HL
	POP	DE
	LD	(HL),E
	INC	HL
	LD	(HL),D
	LD	HL,$0017
	LD	HL,(MEMW)
	LD	HL,(VAL)
	LD	HL,(NUM)
	PUSH	HL
	POP	DE
	EX	DE,HL
	ADD	HL,DE
	LD	HL,$0002
	PUSH	HL
	POP	DE
	EX	DE,HL
	ADD	HL,DE
; array store: t778[t783] = t777
	PUSH	HL
	POP	DE
	LD	(HL),E
	INC	HL
	LD	(HL),D
	LD	HL,(MEMW)
	LD	HL,(VAL)
	LD	HL,(NUM)
	PUSH	HL
	POP	DE
	EX	DE,HL
	ADD	HL,DE
; array load: t789 = t784[t788]
	PUSH	HL
	POP	DE
	EX	DE,HL
	ADD	HL,HL
	ADD	HL,DE
	LD	E,(HL)
	INC	HL
	LD	D,(HL)
	EX	DE,HL
	LD	HL,(MEMW)
	LD	HL,(VAL)
	LD	HL,(NUM)
	PUSH	HL
	POP	DE
	EX	DE,HL
	ADD	HL,DE
	LD	HL,$0002
	PUSH	HL
	POP	DE
	EX	DE,HL
	ADD	HL,DE
; array load: t797 = t790[t796]
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
	ADD	HL,DE
	LD	(VAL),HL
	LD	HL,(VAL)
	LD	HL,$007B
	PUSH	HL
	POP	DE
	EX	DE,HL
	OR	A
	SBC	HL,DE
	LD	HL,$0000
	JR	Z,$+3
	INC	HL
; if t801 == 0 goto _L102
	LD	A,H
	OR	L
	JP	Z,_L102
	LD	HL,(TNUM)
	CALL	__print_num
; load string "OK "
	LD	HL,0 ; string placeholder
	CALL	__print_str
	JP	_L101
_L102:
	LD	HL,(TNUM)
	CALL	__print_num
; load string "NG"
	LD	HL,0 ; string placeholder
	CALL	__print_str
	CALL	__strfunc_/
	CALL	__print_newline
_L101:
	LD	HL,(TNUM)
	LD	HL,$0001
	PUSH	HL
	POP	DE
	EX	DE,HL
	ADD	HL,DE
	LD	(TNUM),HL
	LD	HL,$0001
	LD	HL,(MEM)
	LD	HL,$8000
; array store: t811[t812] = t810
	PUSH	HL
	POP	DE
	LD	(HL),E
	INC	HL
	LD	(HL),D
	LD	HL,$0064
	LD	HL,(MEM)
	LD	HL,$8001
; array store: t814[t815] = t813
	PUSH	HL
	POP	DE
	LD	(HL),E
	INC	HL
	LD	(HL),D
	LD	HL,(MEM)
	LD	HL,$8000
; array load: t819 = t816[t818]
	PUSH	HL
	POP	DE
	EX	DE,HL
	ADD	HL,HL
	ADD	HL,DE
	LD	E,(HL)
	INC	HL
	LD	D,(HL)
	EX	DE,HL
	LD	(VAL),HL
	LD	HL,(VAL)
	LD	HL,$0001
	PUSH	HL
	POP	DE
	EX	DE,HL
	OR	A
	SBC	HL,DE
	LD	HL,$0000
	JR	Z,$+3
	INC	HL
; if t822 == 0 goto _L104
	LD	A,H
	OR	L
	JP	Z,_L104
	LD	HL,(TNUM)
	CALL	__print_num
; load string "OK "
	LD	HL,0 ; string placeholder
	CALL	__print_str
	JP	_L103
_L104:
	LD	HL,(TNUM)
	CALL	__print_num
; load string "NG"
	LD	HL,0 ; string placeholder
	CALL	__print_str
	CALL	__strfunc_/
	CALL	__print_newline
_L103:
	LD	HL,(TNUM)
	LD	HL,$0001
	PUSH	HL
	POP	DE
	EX	DE,HL
	ADD	HL,DE
	LD	(TNUM),HL
	LD	HL,$8000
	LD	(VAL),HL
	LD	HL,(MEM)
	LD	HL,(VAL)
; array load: t835 = t832[t834]
	PUSH	HL
	POP	DE
	EX	DE,HL
	ADD	HL,HL
	ADD	HL,DE
	LD	E,(HL)
	INC	HL
	LD	D,(HL)
	EX	DE,HL
	LD	(NUM),HL
	LD	HL,(NUM)
	LD	HL,$0001
	PUSH	HL
	POP	DE
	EX	DE,HL
	OR	A
	SBC	HL,DE
	LD	HL,$0000
	JR	Z,$+3
	INC	HL
; if t838 == 0 goto _L106
	LD	A,H
	OR	L
	JP	Z,_L106
	LD	HL,(TNUM)
	CALL	__print_num
; load string "OK "
	LD	HL,0 ; string placeholder
	CALL	__print_str
	JP	_L105
_L106:
	LD	HL,(TNUM)
	CALL	__print_num
; load string "NG"
	LD	HL,0 ; string placeholder
	CALL	__print_str
	CALL	__strfunc_/
	CALL	__print_newline
_L105:
	LD	HL,(TNUM)
	LD	HL,$0001
	PUSH	HL
	POP	DE
	EX	DE,HL
	ADD	HL,DE
	LD	(TNUM),HL
	LD	HL,$007B
	LD	HL,(MEM)
	LD	HL,(VAL)
; array store: t848[t849] = t847
	PUSH	HL
	POP	DE
	LD	(HL),E
	INC	HL
	LD	(HL),D
	LD	HL,(MEM)
	LD	HL,(VAL)
; array load: t853 = t850[t852]
	PUSH	HL
	POP	DE
	EX	DE,HL
	ADD	HL,HL
	ADD	HL,DE
	LD	E,(HL)
	INC	HL
	LD	D,(HL)
	EX	DE,HL
	LD	HL,$007B
	PUSH	HL
	POP	DE
	EX	DE,HL
	OR	A
	SBC	HL,DE
	LD	HL,$0000
	JR	Z,$+3
	INC	HL
; if t855 == 0 goto _L108
	LD	A,H
	OR	L
	JP	Z,_L108
	LD	HL,(TNUM)
	CALL	__print_num
; load string "OK "
	LD	HL,0 ; string placeholder
	CALL	__print_str
	JP	_L107
_L108:
	LD	HL,(TNUM)
	CALL	__print_num
; load string "NG"
	LD	HL,0 ; string placeholder
	CALL	__print_str
	CALL	__strfunc_/
	CALL	__print_newline
_L107:
	LD	HL,(TNUM)
	LD	HL,$0001
	PUSH	HL
	POP	DE
	EX	DE,HL
	ADD	HL,DE
	LD	(TNUM),HL
	LD	HL,(MEM)
	LD	HL,(VAL)
; array load: t867 = t864[t866]
	PUSH	HL
	POP	DE
	EX	DE,HL
	ADD	HL,HL
	ADD	HL,DE
	LD	E,(HL)
	INC	HL
	LD	D,(HL)
	EX	DE,HL
	LD	(NUM),HL
	LD	HL,(NUM)
	LD	HL,$007B
	PUSH	HL
	POP	DE
	EX	DE,HL
	OR	A
	SBC	HL,DE
	LD	HL,$0000
	JR	Z,$+3
	INC	HL
; if t870 == 0 goto _L110
	LD	A,H
	OR	L
	JP	Z,_L110
	LD	HL,(TNUM)
	CALL	__print_num
; load string "OK "
	LD	HL,0 ; string placeholder
	CALL	__print_str
	JP	_L109
_L110:
	LD	HL,(TNUM)
	CALL	__print_num
; load string "NG"
	LD	HL,0 ; string placeholder
	CALL	__print_str
	CALL	__strfunc_/
	CALL	__print_newline
_L109:
	LD	HL,(TNUM)
	LD	HL,$0001
	PUSH	HL
	POP	DE
	EX	DE,HL
	ADD	HL,DE
	LD	(TNUM),HL
	LD	HL,$8000
	LD	(VAL),HL
	LD	HL,$0001
	LD	(NUM),HL
	LD	HL,$001E
	LD	HL,(MEM)
	LD	HL,$8001
; array store: t882[t883] = t881
	PUSH	HL
	POP	DE
	LD	(HL),E
	INC	HL
	LD	(HL),D
	LD	HL,$0019
	LD	HL,(MEM)
	LD	HL,$8003
; array store: t885[t886] = t884
	PUSH	HL
	POP	DE
	LD	(HL),E
	INC	HL
	LD	(HL),D
	LD	HL,(MEM)
	LD	HL,(VAL)
	LD	HL,(NUM)
	PUSH	HL
	POP	DE
	EX	DE,HL
	ADD	HL,DE
; array load: t892 = t887[t891]
	PUSH	HL
	POP	DE
	EX	DE,HL
	ADD	HL,HL
	ADD	HL,DE
	LD	E,(HL)
	INC	HL
	LD	D,(HL)
	EX	DE,HL
	LD	HL,(MEM)
	LD	HL,(VAL)
	LD	HL,(NUM)
	PUSH	HL
	POP	DE
	EX	DE,HL
	ADD	HL,DE
	LD	HL,$0002
	PUSH	HL
	POP	DE
	EX	DE,HL
	ADD	HL,DE
; array load: t900 = t893[t899]
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
	ADD	HL,DE
	LD	(VAL),HL
	LD	HL,(VAL)
	LD	HL,$0037
	PUSH	HL
	POP	DE
	EX	DE,HL
	OR	A
	SBC	HL,DE
	LD	HL,$0000
	JR	Z,$+3
	INC	HL
; if t904 == 0 goto _L112
	LD	A,H
	OR	L
	JP	Z,_L112
	LD	HL,(TNUM)
	CALL	__print_num
; load string "OK"
	LD	HL,0 ; string placeholder
	CALL	__print_str
	CALL	__strfunc_/
	CALL	__print_newline
	JP	_L111
_L112:
	LD	HL,(TNUM)
	CALL	__print_num
; load string "NG"
	LD	HL,0 ; string placeholder
	CALL	__print_str
	CALL	__strfunc_/
	CALL	__print_newline
_L111:
	LD	HL,(TNUM)
	LD	HL,$0001
	PUSH	HL
	POP	DE
	EX	DE,HL
	ADD	HL,DE
	LD	(TNUM),HL
; load string "ARRAY2 TEST"
	LD	HL,0 ; string placeholder
	CALL	__print_str
	CALL	__strfunc_/
	CALL	__print_newline
	LD	HL,$0001
	LD	(VAL),HL
	LD	HL,$0005
	LD	(NUM),HL
	LD	HL,$000A
	LD	HL,(AR2)
	LD	HL,$0001
; array load: t921 = t919[t920]
	PUSH	HL
	POP	DE
	EX	DE,HL
	ADD	HL,HL
	ADD	HL,DE
	LD	E,(HL)
	INC	HL
	LD	D,(HL)
	EX	DE,HL
	LD	HL,$0005
; array store: t921[t922] = t918
	PUSH	HL
	POP	DE
	LD	(HL),E
	INC	HL
	LD	(HL),D
	LD	HL,(AR2)
	LD	HL,$0001
; array load: t926 = t923[t925]
	PUSH	HL
	POP	DE
	EX	DE,HL
	ADD	HL,HL
	ADD	HL,DE
	LD	E,(HL)
	INC	HL
	LD	D,(HL)
	EX	DE,HL
	LD	HL,$0005
; array load: t928 = t926[t927]
	PUSH	HL
	POP	DE
	EX	DE,HL
	ADD	HL,HL
	ADD	HL,DE
	LD	E,(HL)
	INC	HL
	LD	D,(HL)
	EX	DE,HL
	LD	HL,$000A
	PUSH	HL
	POP	DE
	EX	DE,HL
	OR	A
	SBC	HL,DE
	LD	HL,$0000
	JR	Z,$+3
	INC	HL
; if t930 == 0 goto _L114
	LD	A,H
	OR	L
	JP	Z,_L114
	LD	HL,(TNUM)
	CALL	__print_num
; load string "OK"
	LD	HL,0 ; string placeholder
	CALL	__print_str
	CALL	__strfunc_/
	CALL	__print_newline
	JP	_L113
_L114:
	LD	HL,(TNUM)
	CALL	__print_num
; load string "NG"
	LD	HL,0 ; string placeholder
	CALL	__print_str
	CALL	__strfunc_/
	CALL	__print_newline
_L113:
	LD	HL,(TNUM)
	LD	HL,$0001
	PUSH	HL
	POP	DE
	EX	DE,HL
	ADD	HL,DE
	LD	(TNUM),HL
	LD	HL,$000A
	LD	HL,(ARB2)
	LD	HL,$0001
; array load: t943 = t941[t942]
	PUSH	HL
	POP	DE
	EX	DE,HL
	ADD	HL,HL
	ADD	HL,DE
	LD	E,(HL)
	INC	HL
	LD	D,(HL)
	EX	DE,HL
	LD	HL,$0005
; array store: t943[t944] = t940
	PUSH	HL
	POP	DE
	LD	(HL),E
	INC	HL
	LD	(HL),D
	LD	HL,(ARB2)
	LD	HL,$0001
; array load: t948 = t945[t947]
	PUSH	HL
	POP	DE
	EX	DE,HL
	ADD	HL,HL
	ADD	HL,DE
	LD	E,(HL)
	INC	HL
	LD	D,(HL)
	EX	DE,HL
	LD	HL,$0005
; array load: t950 = t948[t949]
	PUSH	HL
	POP	DE
	EX	DE,HL
	ADD	HL,HL
	ADD	HL,DE
	LD	E,(HL)
	INC	HL
	LD	D,(HL)
	EX	DE,HL
	LD	HL,$000A
	PUSH	HL
	POP	DE
	EX	DE,HL
	OR	A
	SBC	HL,DE
	LD	HL,$0000
	JR	Z,$+3
	INC	HL
; if t952 == 0 goto _L116
	LD	A,H
	OR	L
	JP	Z,_L116
	LD	HL,(TNUM)
	CALL	__print_num
; load string "OK"
	LD	HL,0 ; string placeholder
	CALL	__print_str
	CALL	__strfunc_/
	CALL	__print_newline
	JP	_L115
_L116:
	LD	HL,(TNUM)
	CALL	__print_num
; load string "NG"
	LD	HL,0 ; string placeholder
	CALL	__print_str
	CALL	__strfunc_/
	CALL	__print_newline
_L115:
	LD	HL,(TNUM)
	LD	HL,$0001
	PUSH	HL
	POP	DE
	EX	DE,HL
	ADD	HL,DE
	LD	(TNUM),HL
	LD	HL,(AR2)
	LD	HL,(VAL)
; array load: t965 = t962[t964]
	PUSH	HL
	POP	DE
	EX	DE,HL
	ADD	HL,HL
	ADD	HL,DE
	LD	E,(HL)
	INC	HL
	LD	D,(HL)
	EX	DE,HL
	LD	HL,(NUM)
; array load: t967 = t965[t966]
	PUSH	HL
	POP	DE
	EX	DE,HL
	ADD	HL,HL
	ADD	HL,DE
	LD	E,(HL)
	INC	HL
	LD	D,(HL)
	EX	DE,HL
	LD	HL,$000A
	PUSH	HL
	POP	DE
	EX	DE,HL
	OR	A
	SBC	HL,DE
	LD	HL,$0000
	JR	Z,$+3
	INC	HL
; if t969 == 0 goto _L118
	LD	A,H
	OR	L
	JP	Z,_L118
	LD	HL,(TNUM)
	CALL	__print_num
; load string "OK"
	LD	HL,0 ; string placeholder
	CALL	__print_str
	CALL	__strfunc_/
	CALL	__print_newline
	JP	_L117
_L118:
	LD	HL,(TNUM)
	CALL	__print_num
; load string "NG"
	LD	HL,0 ; string placeholder
	CALL	__print_str
	CALL	__strfunc_/
	CALL	__print_newline
_L117:
	LD	HL,(TNUM)
	LD	HL,$0001
	PUSH	HL
	POP	DE
	EX	DE,HL
	ADD	HL,DE
	LD	(TNUM),HL
	LD	HL,(ARB2)
	LD	HL,(VAL)
; array load: t982 = t979[t981]
	PUSH	HL
	POP	DE
	EX	DE,HL
	ADD	HL,HL
	ADD	HL,DE
	LD	E,(HL)
	INC	HL
	LD	D,(HL)
	EX	DE,HL
	LD	HL,(NUM)
; array load: t984 = t982[t983]
	PUSH	HL
	POP	DE
	EX	DE,HL
	ADD	HL,HL
	ADD	HL,DE
	LD	E,(HL)
	INC	HL
	LD	D,(HL)
	EX	DE,HL
	LD	HL,$000A
	PUSH	HL
	POP	DE
	EX	DE,HL
	OR	A
	SBC	HL,DE
	LD	HL,$0000
	JR	Z,$+3
	INC	HL
; if t986 == 0 goto _L120
	LD	A,H
	OR	L
	JP	Z,_L120
	LD	HL,(TNUM)
	CALL	__print_num
; load string "OK"
	LD	HL,0 ; string placeholder
	CALL	__print_str
	CALL	__strfunc_/
	CALL	__print_newline
	JP	_L119
_L120:
	LD	HL,(TNUM)
	CALL	__print_num
; load string "NG"
	LD	HL,0 ; string placeholder
	CALL	__print_str
	CALL	__strfunc_/
	CALL	__print_newline
_L119:
	LD	HL,(TNUM)
	LD	HL,$0001
	PUSH	HL
	POP	DE
	EX	DE,HL
	ADD	HL,DE
	LD	(TNUM),HL
	LD	HL,$0002
	LD	(VAL),HL
	LD	HL,$0004
	LD	(NUM),HL
	LD	HL,(AR2)
	LD	HL,(VAL)
	LD	HL,$0001
	PUSH	HL
	POP	DE
	EX	DE,HL
	OR	A
	SBC	HL,DE
; array load: t1003 = t998[t1002]
	PUSH	HL
	POP	DE
	EX	DE,HL
	ADD	HL,HL
	ADD	HL,DE
	LD	E,(HL)
	INC	HL
	LD	D,(HL)
	EX	DE,HL
	LD	HL,(NUM)
	LD	HL,(VAL)
	PUSH	HL
	POP	DE
	EX	DE,HL
	ADD	HL,DE
	LD	HL,$0001
	PUSH	HL
	POP	DE
	EX	DE,HL
	OR	A
	SBC	HL,DE
; array load: t1009 = t1003[t1008]
	PUSH	HL
	POP	DE
	EX	DE,HL
	ADD	HL,HL
	ADD	HL,DE
	LD	E,(HL)
	INC	HL
	LD	D,(HL)
	EX	DE,HL
	LD	HL,$000A
	PUSH	HL
	POP	DE
	EX	DE,HL
	OR	A
	SBC	HL,DE
	LD	HL,$0000
	JR	Z,$+3
	INC	HL
; if t1011 == 0 goto _L122
	LD	A,H
	OR	L
	JP	Z,_L122
	LD	HL,(TNUM)
	CALL	__print_num
; load string "OK"
	LD	HL,0 ; string placeholder
	CALL	__print_str
	CALL	__strfunc_/
	CALL	__print_newline
	JP	_L121
_L122:
	LD	HL,(TNUM)
	CALL	__print_num
; load string "NG"
	LD	HL,0 ; string placeholder
	CALL	__print_str
	CALL	__strfunc_/
	CALL	__print_newline
_L121:
	LD	HL,(TNUM)
	LD	HL,$0001
	PUSH	HL
	POP	DE
	EX	DE,HL
	ADD	HL,DE
	LD	(TNUM),HL
	LD	HL,(ARB2)
	LD	HL,(VAL)
	LD	HL,$0001
	PUSH	HL
	POP	DE
	EX	DE,HL
	OR	A
	SBC	HL,DE
; array load: t1026 = t1021[t1025]
	PUSH	HL
	POP	DE
	EX	DE,HL
	ADD	HL,HL
	ADD	HL,DE
	LD	E,(HL)
	INC	HL
	LD	D,(HL)
	EX	DE,HL
	LD	HL,(NUM)
	LD	HL,(VAL)
	PUSH	HL
	POP	DE
	EX	DE,HL
	ADD	HL,DE
	LD	HL,$0001
	PUSH	HL
	POP	DE
	EX	DE,HL
	OR	A
	SBC	HL,DE
; array load: t1032 = t1026[t1031]
	PUSH	HL
	POP	DE
	EX	DE,HL
	ADD	HL,HL
	ADD	HL,DE
	LD	E,(HL)
	INC	HL
	LD	D,(HL)
	EX	DE,HL
	LD	HL,$000A
	PUSH	HL
	POP	DE
	EX	DE,HL
	OR	A
	SBC	HL,DE
	LD	HL,$0000
	JR	Z,$+3
	INC	HL
; if t1034 == 0 goto _L124
	LD	A,H
	OR	L
	JP	Z,_L124
	LD	HL,(TNUM)
	CALL	__print_num
; load string "OK"
	LD	HL,0 ; string placeholder
	CALL	__print_str
	CALL	__strfunc_/
	CALL	__print_newline
	JP	_L123
_L124:
	LD	HL,(TNUM)
	CALL	__print_num
; load string "NG"
	LD	HL,0 ; string placeholder
	CALL	__print_str
	CALL	__strfunc_/
	CALL	__print_newline
_L123:
	LD	HL,(TNUM)
	LD	HL,$0001
	PUSH	HL
	POP	DE
	EX	DE,HL
	ADD	HL,DE
	LD	(TNUM),HL
	LD	HL,$0001
	LD	(VAL),HL
	LD	HL,$0003
	LD	(NUM),HL
	LD	HL,$000A
	LD	HL,(LAR)
	LD	HL,$0001
; array load: t1049 = t1047[t1048]
	PUSH	HL
	POP	DE
	EX	DE,HL
	ADD	HL,HL
	ADD	HL,DE
	LD	E,(HL)
	INC	HL
	LD	D,(HL)
	EX	DE,HL
	LD	HL,$0003
; array store: t1049[t1050] = t1046
	PUSH	HL
	POP	DE
	LD	(HL),E
	INC	HL
	LD	(HL),D
	LD	HL,$000A
	LD	HL,(LARB)
	LD	HL,$0001
; array load: t1054 = t1052[t1053]
	PUSH	HL
	POP	DE
	EX	DE,HL
	ADD	HL,HL
	ADD	HL,DE
	LD	E,(HL)
	INC	HL
	LD	D,(HL)
	EX	DE,HL
	LD	HL,$0003
; array store: t1054[t1055] = t1051
	PUSH	HL
	POP	DE
	LD	(HL),E
	INC	HL
	LD	(HL),D
	LD	HL,(LAR)
	LD	HL,$0001
; array load: t1059 = t1056[t1058]
	PUSH	HL
	POP	DE
	EX	DE,HL
	ADD	HL,HL
	ADD	HL,DE
	LD	E,(HL)
	INC	HL
	LD	D,(HL)
	EX	DE,HL
	LD	HL,$0003
; array load: t1061 = t1059[t1060]
	PUSH	HL
	POP	DE
	EX	DE,HL
	ADD	HL,HL
	ADD	HL,DE
	LD	E,(HL)
	INC	HL
	LD	D,(HL)
	EX	DE,HL
	LD	HL,$000A
	PUSH	HL
	POP	DE
	EX	DE,HL
	OR	A
	SBC	HL,DE
	LD	HL,$0000
	JR	Z,$+3
	INC	HL
; if t1063 == 0 goto _L126
	LD	A,H
	OR	L
	JP	Z,_L126
	LD	HL,(TNUM)
	CALL	__print_num
; load string "OK"
	LD	HL,0 ; string placeholder
	CALL	__print_str
	CALL	__strfunc_/
	CALL	__print_newline
	JP	_L125
_L126:
	LD	HL,(TNUM)
	CALL	__print_num
; load string "NG"
	LD	HL,0 ; string placeholder
	CALL	__print_str
	CALL	__strfunc_/
	CALL	__print_newline
_L125:
	LD	HL,(TNUM)
	LD	HL,$0001
	PUSH	HL
	POP	DE
	EX	DE,HL
	ADD	HL,DE
	LD	(TNUM),HL
	LD	HL,(LARB)
	LD	HL,$0001
; array load: t1076 = t1073[t1075]
	PUSH	HL
	POP	DE
	EX	DE,HL
	ADD	HL,HL
	ADD	HL,DE
	LD	E,(HL)
	INC	HL
	LD	D,(HL)
	EX	DE,HL
	LD	HL,$0003
; array load: t1078 = t1076[t1077]
	PUSH	HL
	POP	DE
	EX	DE,HL
	ADD	HL,HL
	ADD	HL,DE
	LD	E,(HL)
	INC	HL
	LD	D,(HL)
	EX	DE,HL
	LD	HL,$000A
	PUSH	HL
	POP	DE
	EX	DE,HL
	OR	A
	SBC	HL,DE
	LD	HL,$0000
	JR	Z,$+3
	INC	HL
; if t1080 == 0 goto _L128
	LD	A,H
	OR	L
	JP	Z,_L128
	LD	HL,(TNUM)
	CALL	__print_num
; load string "OK"
	LD	HL,0 ; string placeholder
	CALL	__print_str
	CALL	__strfunc_/
	CALL	__print_newline
	JP	_L127
_L128:
	LD	HL,(TNUM)
	CALL	__print_num
; load string "NG"
	LD	HL,0 ; string placeholder
	CALL	__print_str
	CALL	__strfunc_/
	CALL	__print_newline
_L127:
	LD	HL,(TNUM)
	LD	HL,$0001
	PUSH	HL
	POP	DE
	EX	DE,HL
	ADD	HL,DE
	LD	(TNUM),HL
	LD	HL,(LAR)
	LD	HL,(VAL)
; array load: t1093 = t1090[t1092]
	PUSH	HL
	POP	DE
	EX	DE,HL
	ADD	HL,HL
	ADD	HL,DE
	LD	E,(HL)
	INC	HL
	LD	D,(HL)
	EX	DE,HL
	LD	HL,(NUM)
; array load: t1095 = t1093[t1094]
	PUSH	HL
	POP	DE
	EX	DE,HL
	ADD	HL,HL
	ADD	HL,DE
	LD	E,(HL)
	INC	HL
	LD	D,(HL)
	EX	DE,HL
	LD	HL,$000A
	PUSH	HL
	POP	DE
	EX	DE,HL
	OR	A
	SBC	HL,DE
	LD	HL,$0000
	JR	Z,$+3
	INC	HL
; if t1097 == 0 goto _L130
	LD	A,H
	OR	L
	JP	Z,_L130
	LD	HL,(TNUM)
	CALL	__print_num
; load string "OK"
	LD	HL,0 ; string placeholder
	CALL	__print_str
	CALL	__strfunc_/
	CALL	__print_newline
	JP	_L129
_L130:
	LD	HL,(TNUM)
	CALL	__print_num
; load string "NG"
	LD	HL,0 ; string placeholder
	CALL	__print_str
	CALL	__strfunc_/
	CALL	__print_newline
_L129:
	LD	HL,(TNUM)
	LD	HL,$0001
	PUSH	HL
	POP	DE
	EX	DE,HL
	ADD	HL,DE
	LD	(TNUM),HL
	LD	HL,(LARB)
	LD	HL,(VAL)
; array load: t1110 = t1107[t1109]
	PUSH	HL
	POP	DE
	EX	DE,HL
	ADD	HL,HL
	ADD	HL,DE
	LD	E,(HL)
	INC	HL
	LD	D,(HL)
	EX	DE,HL
	LD	HL,(NUM)
; array load: t1112 = t1110[t1111]
	PUSH	HL
	POP	DE
	EX	DE,HL
	ADD	HL,HL
	ADD	HL,DE
	LD	E,(HL)
	INC	HL
	LD	D,(HL)
	EX	DE,HL
	LD	HL,$000A
	PUSH	HL
	POP	DE
	EX	DE,HL
	OR	A
	SBC	HL,DE
	LD	HL,$0000
	JR	Z,$+3
	INC	HL
; if t1114 == 0 goto _L132
	LD	A,H
	OR	L
	JP	Z,_L132
	LD	HL,(TNUM)
	CALL	__print_num
; load string "OK"
	LD	HL,0 ; string placeholder
	CALL	__print_str
	CALL	__strfunc_/
	CALL	__print_newline
	JP	_L131
_L132:
	LD	HL,(TNUM)
	CALL	__print_num
; load string "NG"
	LD	HL,0 ; string placeholder
	CALL	__print_str
	CALL	__strfunc_/
	CALL	__print_newline
_L131:
	LD	HL,(TNUM)
	LD	HL,$0001
	PUSH	HL
	POP	DE
	EX	DE,HL
	ADD	HL,DE
	LD	(TNUM),HL
	LD	HL,$0002
	LD	(VAL),HL
	LD	HL,$0002
	LD	(NUM),HL
	LD	HL,(LAR)
	LD	HL,(VAL)
	LD	HL,$0001
	PUSH	HL
	POP	DE
	EX	DE,HL
	OR	A
	SBC	HL,DE
; array load: t1131 = t1126[t1130]
	PUSH	HL
	POP	DE
	EX	DE,HL
	ADD	HL,HL
	ADD	HL,DE
	LD	E,(HL)
	INC	HL
	LD	D,(HL)
	EX	DE,HL
	LD	HL,(NUM)
	LD	HL,(VAL)
	PUSH	HL
	POP	DE
	EX	DE,HL
	ADD	HL,DE
	LD	HL,$0001
	PUSH	HL
	POP	DE
	EX	DE,HL
	OR	A
	SBC	HL,DE
; array load: t1137 = t1131[t1136]
	PUSH	HL
	POP	DE
	EX	DE,HL
	ADD	HL,HL
	ADD	HL,DE
	LD	E,(HL)
	INC	HL
	LD	D,(HL)
	EX	DE,HL
	LD	HL,$000A
	PUSH	HL
	POP	DE
	EX	DE,HL
	OR	A
	SBC	HL,DE
	LD	HL,$0000
	JR	Z,$+3
	INC	HL
; if t1139 == 0 goto _L134
	LD	A,H
	OR	L
	JP	Z,_L134
	LD	HL,(TNUM)
	CALL	__print_num
; load string "OK"
	LD	HL,0 ; string placeholder
	CALL	__print_str
	CALL	__strfunc_/
	CALL	__print_newline
	JP	_L133
_L134:
	LD	HL,(TNUM)
	CALL	__print_num
; load string "NG"
	LD	HL,0 ; string placeholder
	CALL	__print_str
	CALL	__strfunc_/
	CALL	__print_newline
_L133:
	LD	HL,(TNUM)
	LD	HL,$0001
	PUSH	HL
	POP	DE
	EX	DE,HL
	ADD	HL,DE
	LD	(TNUM),HL
	LD	HL,(LARB)
	LD	HL,(VAL)
	LD	HL,$0001
	PUSH	HL
	POP	DE
	EX	DE,HL
	OR	A
	SBC	HL,DE
; array load: t1154 = t1149[t1153]
	PUSH	HL
	POP	DE
	EX	DE,HL
	ADD	HL,HL
	ADD	HL,DE
	LD	E,(HL)
	INC	HL
	LD	D,(HL)
	EX	DE,HL
	LD	HL,(NUM)
	LD	HL,(VAL)
	PUSH	HL
	POP	DE
	EX	DE,HL
	ADD	HL,DE
	LD	HL,$0001
	PUSH	HL
	POP	DE
	EX	DE,HL
	OR	A
	SBC	HL,DE
; array load: t1160 = t1154[t1159]
	PUSH	HL
	POP	DE
	EX	DE,HL
	ADD	HL,HL
	ADD	HL,DE
	LD	E,(HL)
	INC	HL
	LD	D,(HL)
	EX	DE,HL
	LD	HL,$000A
	PUSH	HL
	POP	DE
	EX	DE,HL
	OR	A
	SBC	HL,DE
	LD	HL,$0000
	JR	Z,$+3
	INC	HL
; if t1162 == 0 goto _L136
	LD	A,H
	OR	L
	JP	Z,_L136
	LD	HL,(TNUM)
	CALL	__print_num
; load string "OK"
	LD	HL,0 ; string placeholder
	CALL	__print_str
	CALL	__strfunc_/
	CALL	__print_newline
	JP	_L135
_L136:
	LD	HL,(TNUM)
	CALL	__print_num
; load string "NG"
	LD	HL,0 ; string placeholder
	CALL	__print_str
	CALL	__strfunc_/
	CALL	__print_newline
_L135:
	LD	HL,(TNUM)
	LD	HL,$0001
	PUSH	HL
	POP	DE
	EX	DE,HL
	ADD	HL,DE
	LD	(TNUM),HL
; load string "INPUT 'START'"
	LD	HL,0 ; string placeholder
	CALL	__print_str
	LD	HL,(AARB)
	PUSH	HL
	CALL	GETL
	LD	HL,(AARB)
	PUSH	HL
; load string "START"
	LD	HL,0 ; string placeholder
	PUSH	HL
	CALL	STRCMP
	LD	HL,$0000
	PUSH	HL
	POP	DE
	EX	DE,HL
	OR	A
	SBC	HL,DE
	LD	HL,$0000
	JR	Z,$+3
	INC	HL
; if t1179 == 0 goto _L138
	LD	A,H
	OR	L
	JP	Z,_L138
	LD	HL,(TNUM)
	CALL	__print_num
; load string "OK"
	LD	HL,0 ; string placeholder
	CALL	__print_str
	CALL	__strfunc_/
	CALL	__print_newline
	JP	_L137
_L138:
	LD	HL,(TNUM)
	CALL	__print_num
; load string "NG"
	LD	HL,0 ; string placeholder
	CALL	__print_str
	CALL	__strfunc_/
	CALL	__print_newline
_L137:
_EXIT:
	POP	IY
	RET

STRCMP:
; function STRCMP
	PUSH	IY
	LD	HL,(A)
	LD	(I),HL
	LD	HL,(B)
	LD	(J),HL
_L139:
	LD	HL,$0001
; if t2 == 0 goto _L140
	LD	A,H
	OR	L
	JP	Z,_L140
	LD	HL,(MEM)
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
	LD	HL,(MEM)
	LD	HL,(J)
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
	PUSH	HL
	POP	DE
	EX	DE,HL
	OR	A
	SBC	HL,DE
	LD	(D),HL
	LD	HL,(D)
	LD	HL,$0000
	PUSH	HL
	POP	DE
	EX	DE,HL
	OR	A
	SBC	HL,DE
	LD	HL,$0000
	JR	NZ,$+3
	INC	HL
; if t14 == 0 goto _L141
	LD	A,H
	OR	L
	JP	Z,_L141
	LD	HL,(D)
; return t15
	JP	_EXIT
_L141:
	LD	HL,(MEM)
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
	LD	HL,$0000
	PUSH	HL
	POP	DE
	EX	DE,HL
	OR	A
	SBC	HL,DE
	LD	HL,$0000
	JR	Z,$+3
	INC	HL
; if t21 == 0 goto _L142
	LD	A,H
	OR	L
	JP	Z,_L142
	LD	HL,$0000
; return t22
	JP	_EXIT
_L142:
	LD	HL,(I)
	LD	HL,$0001
	PUSH	HL
	POP	DE
	EX	DE,HL
	ADD	HL,DE
	LD	(I),HL
	LD	HL,(J)
	LD	HL,$0001
	PUSH	HL
	POP	DE
	EX	DE,HL
	ADD	HL,DE
	LD	(J),HL
	JP	_L139
_L140:
_EXIT:
	POP	IY
	RET

FUNC1:
; function FUNC1
	PUSH	IY
	LD	HL,$007B
	LD	(LOCAL),HL
	LD	HL,(I)
	CALL	__print_num
; load string ","
	LD	HL,0 ; string placeholder
	CALL	__print_str
	LD	HL,(J)
	CALL	__print_num
	CALL	__strfunc_/
	CALL	__print_newline
	LD	HL,$0064
	LD	(VAL),HL
; load string "FUNC VAL:"
	LD	HL,0 ; string placeholder
	CALL	__print_str
	LD	HL,(VAL)
	CALL	__print_num
	CALL	__strfunc_/
	CALL	__print_newline
; load string "I:"
	LD	HL,0 ; string placeholder
	CALL	__print_str
	LD	HL,(I)
	CALL	__print_num
	CALL	__strfunc_/
	CALL	__print_newline
	LD	HL,(I)
	LD	HL,(LOCAL)
	PUSH	HL
	POP	DE
	EX	DE,HL
	ADD	HL,DE
	LD	HL,(VAL)
	PUSH	HL
	POP	DE
	EX	DE,HL
	ADD	HL,DE
	LD	(I),HL
	LD	HL,(I)
	LD	HL,$00E4
	PUSH	HL
	POP	DE
	EX	DE,HL
	OR	A
	SBC	HL,DE
	LD	HL,$0000
	JR	Z,$+3
	INC	HL
; if t19 == 0 goto _L144
	LD	A,H
	OR	L
	JP	Z,_L144
	LD	HL,(TNUM)
	CALL	__print_num
; load string "OK"
	LD	HL,0 ; string placeholder
	CALL	__print_str
	CALL	__strfunc_/
	CALL	__print_newline
	JP	_L143
_L144:
	LD	HL,(TNUM)
	CALL	__print_num
; load string "NG"
	LD	HL,0 ; string placeholder
	CALL	__print_str
	LD	HL,(I)
	CALL	__print_num
	CALL	__strfunc_/
	CALL	__print_newline
; load string "LOCAL:"
	LD	HL,0 ; string placeholder
	CALL	__print_str
	LD	HL,(LOCAL)
	CALL	__print_num
	CALL	__strfunc_/
	CALL	__print_newline
; load string "VAL:"
	LD	HL,0 ; string placeholder
	CALL	__print_str
	LD	HL,(VAL)
	CALL	__print_num
	CALL	__strfunc_/
	CALL	__print_newline
_L143:
	LD	HL,(TNUM)
	LD	HL,$0001
	PUSH	HL
	POP	DE
	EX	DE,HL
	ADD	HL,DE
	LD	(TNUM),HL
	LD	HL,(I)
; return t36
	JP	_EXIT
_EXIT:
	POP	IY
	RET

FUNC2:
; function FUNC2
	PUSH	IY
	LD	HL,(I)
	LD	HL,$0064
	PUSH	HL
	POP	DE
	EX	DE,HL
	OR	A
	SBC	HL,DE
	LD	HL,$0000
	JR	C,$+3
	INC	HL
; if t2 == 0 goto _L145
	LD	A,H
	OR	L
	JP	Z,_L145
	LD	HL,$0001
; return t3
	JP	_EXIT
_L145:
	LD	HL,$000A
; return t4
	JP	_EXIT
_EXIT:
	POP	IY
	RET
