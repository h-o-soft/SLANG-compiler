; === Entry Point ===
	LD	IY,__IYWORK
	CALL	MAIN
	RET

MAIN:
; function MAIN
	PUSH	IY
	LD	HL,$0000
	LD	(_Y),HL
_L0:
	LD	HL,$0000
	LD	(_X),HL
_L3:
	LD	HL,(_X)
	LD	DE,$0027
	OR	A
	SBC	HL,DE
	LD	(_FX),HL
	LD	(_FX+2),A
	LD	HL,(_Y)
	LD	DE,$000C
	OR	A
	SBC	HL,DE
	LD	(_FY),HL
	LD	(_FY+2),A
	LD	HL,(_FX)
	LD	A,(_FX+2)
	PUSH	HL
	LD	HL,$4817
	CALL	i16tof24
	POP	DE
	EX	DE,HL
	CALL	MUL16
	LD	(_CA),HL
	LD	(_CA+2),A
	LD	HL,(_FY)
	LD	A,(_FY+2)
	PUSH	HL
	LD	HL,$92F7
	CALL	i16tof24
	POP	DE
	EX	DE,HL
	CALL	MUL16
	LD	(_CB),HL
	LD	(_CB+2),A
	LD	HL,(_CA)
	LD	A,(_CA+2)
	LD	(_A),HL
	LD	(_A+2),A
	LD	HL,(_CB)
	LD	A,(_CB+2)
	LD	(_B),HL
	LD	(_B+2),A
	LD	HL,$0000
	LD	(_I),HL
_L6:
	LD	HL,(_A)
	LD	A,(_A+2)
	LD	DE,(_A)
	CALL	f24mul
	LD	HL,(_B)
	LD	A,(_B+2)
	LD	DE,(_B)
	CALL	f24mul
	POP	DE
	EX	DE,HL
	OR	A
	SBC	HL,DE
	PUSH	HL
	LD	HL,(_CA)
	LD	A,(_CA+2)
	POP	DE
	EX	DE,HL
	ADD	HL,DE
	LD	(_T),HL
	LD	(_T+2),A
	LD	HL,$0002
	LD	HL,(_A)
	LD	A,(_A+2)
	CALL	i16tof24
	PUSH	HL
	POP	DE
	EX	DE,HL
	CALL	MUL16
	PUSH	HL
	LD	HL,(_B)
	LD	A,(_B+2)
	POP	DE
	EX	DE,HL
	CALL	MUL16
	PUSH	HL
	LD	HL,(_CB)
	LD	A,(_CB+2)
	POP	DE
	EX	DE,HL
	ADD	HL,DE
	LD	(_B),HL
	LD	(_B+2),A
	LD	HL,(_T)
	LD	A,(_T+2)
	LD	(_A),HL
	LD	(_A+2),A
	LD	HL,(_A)
	LD	A,(_A+2)
	LD	DE,(_A)
	CALL	f24mul
	LD	HL,(_B)
	LD	A,(_B+2)
	LD	DE,(_B)
	CALL	f24mul
	POP	DE
	EX	DE,HL
	ADD	HL,DE
	PUSH	HL
	LD	HL,$0000
	CALL	i16tof24
	POP	DE
	OR	A
	SBC	HL,DE
	LD	HL,$0000
	JR	C,$+3
	INC	HL
	LD	A,H
	OR	L
	JP	Z,_L9
	JP	_L8
_L9:
_L7:
	LD	HL,(_I)
	INC	HL
	LD	(_I),HL
	LD	HL,$000F
	POP	DE
	OR	A
	SBC	HL,DE
	LD	HL,$0000
	JR	C,$+3
	INC	HL
	LD	A,L
	XOR	$01
	LD	L,A
; if t51 != 0 goto _L6
	LD	A,H
	OR	L
	JP	NZ,_L6
_L8:
	LD	HL,(_I)
	LD	DE,$000F
	EX	DE,HL
	OR	A
	SBC	HL,DE
	JP	C,_L11
	LD	HL,(_I)
	LD	DE,$0009
	EX	DE,HL
	OR	A
	SBC	HL,DE
	JP	NC,_L12
	LD	HL,(_I)
	LD	DE,$0007
	ADD	HL,DE
	LD	(_I),HL
_L12:
	LD	HL,$0030
	LD	DE,(_I)
	ADD	HL,DE
	CALL	PCHR
	JP	_L10
_L11:
	LD	HL,_S0
	CALL	PSTR
_L10:
_L4:
	LD	HL,(_X)
	INC	HL
	LD	(_X),HL
	LD	HL,$004E
	POP	DE
	OR	A
	SBC	HL,DE
	LD	HL,$0000
	JR	C,$+3
	INC	HL
	LD	A,L
	XOR	$01
	LD	L,A
; if t69 != 0 goto _L3
	LD	A,H
	OR	L
	JP	NZ,_L3
_L5:
	CALL	PCRONE
_L1:
	LD	HL,(_Y)
	INC	HL
	LD	(_Y),HL
	LD	HL,$0018
	POP	DE
	OR	A
	SBC	HL,DE
	LD	HL,$0000
	JR	C,$+3
	INC	HL
	LD	A,L
	XOR	$01
	LD	L,A
; if t74 != 0 goto _L0
	LD	A,H
	OR	L
	JP	NZ,_L0
_L2:
_MAIN_EXIT:
	POP	IY
	RET


; === String Table ===
_S0:
	DB	$20,$00

; === Global Variables (Work Area) ===
_X:
	DS	2
_Y:
	DS	2
_I:
	DS	2
_VAL:
	DS	2
_CA:
	DS	3
_CB:
	DS	3
_FX:
	DS	3
_FY:
	DS	3
_A:
	DS	3
_B:
	DS	3
_T:
	DS	3

; === Runtime Functions ===
u16tof24:
i16tof24_pos:
ld b,$3F+16    ;Initial exponent and sign
;Check if HL is 0, if so return AHL == 0x000000
ld a,h
or l
ret z
; HL is non-zero
; shift HL left until there is an overflow (the implicit bit)
; meanwhile, decrement B, the exponent each iteration
dec b
add hl,hl
jr nc,$-2
ld a,b
ret

i16tof24:
;Inputs:
;   HL holds a 16-bit signed integer, (-32768 to 32767)
;Outputs:
;   Converts to an f24 float in AHL
;   returns z flag set if zero, nz otherwise :)
bit 7,h
jr z,i16tof24_pos
xor a
sub l
ld l,a
sbc a,a
sub h
ld h,a
ld b,$BF+16
db $11     ;start of `ld de,**`, eats the next two bytes

PRT:
PUSH HL
PUSH DE
PUSH BC
PUSH IY
LD E,A
LD C,$06
CALL $0005
POP IY
POP BC
POP DE
POP HL
RET
WORK10:
DB  "12345",0
DS  4

PCHR:
LD A, H
CALL PRT
LD A, L
JR PRT

PSTR:
.pstr1
LD A,D
OR E
RET Z
LD A,L
CALL PRT
DEC DE
JR .pstr1

PSTR2:
.pstr1
LD A,D
OR E
RET Z
CALL PCHR
DEC DE
JR .pstr1

PCR:
EX DE,HL
LD HL,$0D0A
JR PSTR2

PCRONE:
LD HL,1


; === IY Work Area (256 bytes) ===
__IYWORK:
	DS	256

SLANG_PROG_END: