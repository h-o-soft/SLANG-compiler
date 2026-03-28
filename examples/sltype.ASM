	ORG	$0100

; === Entry Point ===
	LD	IY,__IYWORK
	CALL	MAIN
	RET

main:
; function main
	PUSH	IY
	LD	BC,$0002
	ADD	IY,BC
	LD	HL,$0000
	LD	(IY+$70),L
	LD	(IY+$71),H
	LD	HL,$0081
	LD	(IY+$72),L
	LD	(IY+$73),H
	LD	HL,$0000
	LD	(IY+$74),L
	LD	(IY+$75),H
	CALL	fopen
	PUSH	HL
	LD	HL,$0000
	POP	DE
	EX	DE,HL
	OR	A
	SBC	HL,DE
	LD	HL,$0000
	JR	NZ,$+3
	INC	HL
	LD	A,H
	OR	L
	JP	Z,_L0
	JP	_main_EXIT
_L0:
_L1:
	LD	HL,$0000
	LD	(IY+$70),L
	LD	(IY+$71),H
	CALL	fgetc
	LD	(IY+$6E),L
	LD	(IY+$6F),H
	LD	L,(IY+$6E)
	LD	H,(IY+$6F)
	LD	DE,$00FF
	EX	DE,HL
	OR	A
	SBC	HL,DE
	LD	HL,$0000
	JR	C,$+3
	INC	HL
	LD	L,(IY+$6E)
	LD	H,(IY+$6F)
	LD	DE,$001A
	OR	A
	SBC	HL,DE
	LD	HL,$0000
	JR	Z,$+3
	INC	HL
	POP	DE
	LD	A,D
	OR	H
	LD	H,A
	LD	A,E
	OR	L
	LD	L,A
; if t14 == 0 goto _L3
	LD	A,H
	OR	L
	JP	Z,_L3
	JP	_L2
_L3:
	LD	L,(IY+$6E)
	LD	H,(IY+$6F)
	LD	(^DE),HL
	LD	HL,$0002
	LD	(^BC),HL
	LD	HL,$0005
	LD	(IY+$70),L
	LD	(IY+$71),H
	CALL	CALL
	JP	_L1
_L2:
	LD	HL,$0000
	LD	(IY+$70),L
	LD	(IY+$71),H
	CALL	fclose
_main_EXIT:
	POP	IY
	RET


; === Runtime Functions ===
GETREG:
PUSH HL
LD (_IY), IY
LD (_IX), IX
LD (_HL), HL
LD (_DE), DE
LD (_BC), BC
PUSH AF
POP HL
LD (_AF), HL
LD HL, 0000
JR NC, .getreg1
INC HL
.getreg1
LD (_CARRY), HL
LD HL, 0000
JR NZ, .getreg2
INC HL
.getreg2
LD (_ZERO), HL
LD HL, 0004
ADD HL, SP
LD (_SP), HL
POP HL
RET

CALL:
PUSH IY
LD DE,.call1
PUSH DE
PUSH HL
LD A,(___AF+1)
LD BC,(___BC)
LD DE,(___DE)
LD HL,(___HL)
LD IX,(___IX)
LD IY,(___IY)
RET
.call1
PUSH HL
CALL GETREG
LD HL,0006
ADD HL,SP
POP HL
POP IY
RET


; === IY Work Area (256 bytes) ===
__IYWORK:
	DS	256

SLANG_PROG_END: