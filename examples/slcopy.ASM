; global: LoadConst t0 #256
; ORG t0
; ARRAY buf[SLANGCompiler.Parser.Ast.IntegerLiteral]

main:
; function main
	PUSH	IY
	LD	HL,$0081
	LD	(n),HL
_L0:
	LD	HL,(MEM)
	LD	HL,(n)
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
	LD	HL,$0020
	PUSH	HL
	POP	DE
	EX	DE,HL
	OR	A
	SBC	HL,DE
	LD	HL,$0000
	JR	Z,$+3
	INC	HL
; if t6 == 0 goto _L1
	LD	A,H
	OR	L
	JP	Z,_L1
	LD	HL,(n)
	LD	HL,$0001
	PUSH	HL
	POP	DE
	EX	DE,HL
	ADD	HL,DE
	LD	(n),HL
	JP	_L0
_L1:
	LD	HL,$0000
	PUSH	HL
	LD	HL,(n)
	PUSH	HL
	LD	HL,$0000
	PUSH	HL
	CALL	fopen
	LD	HL,$0000
	PUSH	HL
	POP	DE
	EX	DE,HL
	OR	A
	SBC	HL,DE
	LD	HL,$0000
	JR	NZ,$+3
	INC	HL
; if t15 == 0 goto _L2
	LD	A,H
	OR	L
	JP	Z,_L2
	JP	_EXIT
_L2:
_L3:
	LD	HL,(MEM)
	LD	HL,(n)
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
	LD	HL,$0020
	PUSH	HL
	POP	DE
	EX	DE,HL
	EX	DE,HL
	OR	A
	SBC	HL,DE
	LD	HL,$0000
	JR	C,$+3
	INC	HL
; if t21 == 0 goto _L4
	LD	A,H
	OR	L
	JP	Z,_L4
	LD	HL,(n)
	LD	HL,$0001
	PUSH	HL
	POP	DE
	EX	DE,HL
	ADD	HL,DE
	LD	(n),HL
	JP	_L3
_L4:
	LD	HL,$0001
	PUSH	HL
	LD	HL,(n)
	PUSH	HL
	LD	HL,$0003
	PUSH	HL
	CALL	fopen
	LD	HL,$0000
	PUSH	HL
	POP	DE
	EX	DE,HL
	OR	A
	SBC	HL,DE
	LD	HL,$0000
	JR	NZ,$+3
	INC	HL
; if t30 == 0 goto _L5
	LD	A,H
	OR	L
	JP	Z,_L5
	JP	_EXIT
_L5:
_L6:
	LD	HL,$0001
; if t31 == 0 goto _L7
	LD	A,H
	OR	L
	JP	Z,_L7
	LD	HL,$0000
	PUSH	HL
	LD	HL,(buf)
	PUSH	HL
	LD	HL,$8000
	PUSH	HL
	CALL	fread
	LD	(n),HL
	LD	HL,(n)
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
	LD	HL,(n)
	LD	HL,$0000
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
	OR	L
	OR	D
	OR	E
	LD	HL,$0000
	JR	Z,$+3
	INC	HL
; if t43 == 0 goto _L8
	LD	A,H
	OR	L
	JP	Z,_L8
	JP	_L7
_L8:
	LD	HL,$0001
	PUSH	HL
	LD	HL,(buf)
	PUSH	HL
	LD	HL,(n)
	PUSH	HL
	CALL	fwrite
	LD	HL,$0000
	PUSH	HL
	POP	DE
	EX	DE,HL
	OR	A
	SBC	HL,DE
	LD	HL,$0000
	JR	NZ,$+3
	INC	HL
; if t49 == 0 goto _L9
	LD	A,H
	OR	L
	JP	Z,_L9
; load string "Write error"
	LD	HL,0 ; string placeholder
	CALL	__print_str
	CALL	__strfunc_/
	CALL	__print_newline
	JP	_L7
_L9:
	JP	_L6
_L7:
	LD	HL,$0001
	PUSH	HL
	CALL	fclose
	LD	HL,$0000
	PUSH	HL
	CALL	fclose
_EXIT:
	POP	IY
	RET
