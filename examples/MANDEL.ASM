; === Entry Point ===
	LD	IY,__IYWORK
	CALL	MAIN
	RET

@SINGLE:
; function @SINGLE
	PUSH	IY
	DB	$3E
	DB	$05
	DB	$32
	LD	HL,(_@@@)
	LD	DE,$0000
	ADD	HL,DE
_@SINGLE_EXIT:
	POP	IY
	RET

@DOUBLE:
; function @DOUBLE
	PUSH	IY
	DB	$3E
	DB	$08
	DB	$32
	LD	HL,(_@@@)
	LD	DE,$0000
	ADD	HL,DE
_@DOUBLE_EXIT:
	POP	IY
	RET

@CVFTU:
; function @CVFTU
	PUSH	IY
	DB	$CD
	LD	HL,(_@@@)
	LD	DE,$001A
	ADD	HL,DE
	DB	$EB
_@CVFTU_EXIT:
	POP	IY
	RET

@CVFTI:
; function @CVFTI
	PUSH	IY
	DB	$CD
	LD	HL,(_@@@)
	LD	DE,$001D
	ADD	HL,DE
	DB	$EB
_@CVFTI_EXIT:
	POP	IY
	RET

@CVFTS:
; function @CVFTS
	PUSH	IY
	DB	$11
	LD	HL,(_BUFF)
	DB	$CD
	LD	HL,(_@@@)
	LD	DE,$0017
	ADD	HL,DE
	DB	$EB
_@CVFTS_EXIT:
	POP	IY
	RET

@CMP:
; function @CMP
	PUSH	IY
	DB	$CD
	LD	HL,(_@@@)
	LD	DE,$0032
	ADD	HL,DE
	DB	$6F
	DB	$38
	DB	$01
	DB	$AF
	DB	$67
_@CMP_EXIT:
	POP	IY
	RET

@NEG:
; function @NEG
	PUSH	IY
	DB	$CD
	LD	HL,(_@@BGN2)
	DB	$CD
	LD	HL,(_@@@)
	LD	DE,$0035
	ADD	HL,DE
_@NEG_EXIT:
	POP	IY
	RET

@INT:
; function @INT
	PUSH	IY
	DB	$CD
	LD	HL,(_@@BGN2)
	DB	$CD
	LD	HL,(_@@@)
	LD	DE,$0038
	ADD	HL,DE
_@INT_EXIT:
	POP	IY
	RET

@FIX:
; function @FIX
	PUSH	IY
	DB	$CD
	LD	HL,(_@@BGN2)
	DB	$CD
	LD	HL,(_@@@)
	LD	DE,$003B
	ADD	HL,DE
_@FIX_EXIT:
	POP	IY
	RET

@FRAC:
; function @FRAC
	PUSH	IY
	DB	$CD
	LD	HL,(_@@BGN2)
	DB	$CD
	LD	HL,(_@@@)
	LD	DE,$003E
	ADD	HL,DE
_@FRAC_EXIT:
	POP	IY
	RET

@CINT:
; function @CINT
	PUSH	IY
	DB	$CD
	LD	HL,(_@@BGN2)
	DB	$CD
	LD	HL,(_@@@)
	LD	DE,$0041
	ADD	HL,DE
_@CINT_EXIT:
	POP	IY
	RET

@SQR:
; function @SQR
	PUSH	IY
	DB	$CD
	LD	HL,(_@@BGN2)
	DB	$CD
	LD	HL,(_@@@)
	LD	DE,$0044
	ADD	HL,DE
_@SQR_EXIT:
	POP	IY
	RET

@SIN:
; function @SIN
	PUSH	IY
	DB	$CD
	LD	HL,(_@@BGN2)
	DB	$CD
	LD	HL,(_@@@)
	LD	DE,$0047
	ADD	HL,DE
_@SIN_EXIT:
	POP	IY
	RET

@COS:
; function @COS
	PUSH	IY
	DB	$CD
	LD	HL,(_@@BGN2)
	DB	$CD
	LD	HL,(_@@@)
	LD	DE,$004A
	ADD	HL,DE
_@COS_EXIT:
	POP	IY
	RET

@TAN:
; function @TAN
	PUSH	IY
	DB	$CD
	LD	HL,(_@@BGN2)
	DB	$CD
	LD	HL,(_@@@)
	LD	DE,$004D
	ADD	HL,DE
_@TAN_EXIT:
	POP	IY
	RET

@ATN:
; function @ATN
	PUSH	IY
	DB	$CD
	LD	HL,(_@@BGN2)
	DB	$CD
	LD	HL,(_@@@)
	LD	DE,$0050
	ADD	HL,DE
_@ATN_EXIT:
	POP	IY
	RET

@EXP:
; function @EXP
	PUSH	IY
	DB	$CD
	LD	HL,(_@@BGN2)
	DB	$CD
	LD	HL,(_@@@)
	LD	DE,$0053
	ADD	HL,DE
_@EXP_EXIT:
	POP	IY
	RET

@LOG:
; function @LOG
	PUSH	IY
	DB	$CD
	LD	HL,(_@@BGN2)
	DB	$CD
	LD	HL,(_@@@)
	LD	DE,$0056
	ADD	HL,DE
_@LOG_EXIT:
	POP	IY
	RET

@PAI:
; function @PAI
	PUSH	IY
	DB	$CD
	LD	HL,(_@@BGN2)
	DB	$CD
	LD	HL,(_@@@)
	LD	DE,$005C
	ADD	HL,DE
_@PAI_EXIT:
	POP	IY
	RET

@RAD:
; function @RAD
	PUSH	IY
	DB	$CD
	LD	HL,(_@@BGN2)
	DB	$CD
	LD	HL,(_@@@)
	LD	DE,$005F
	ADD	HL,DE
_@RAD_EXIT:
	POP	IY
	RET

@ABS:
; function @ABS
	PUSH	IY
	DB	$CD
	LD	HL,(_@@BGN2)
	DB	$CD
	LD	HL,(_@@@)
	LD	DE,$0062
	ADD	HL,DE
_@ABS_EXIT:
	POP	IY
	RET

@SGN:
; function @SGN
	PUSH	IY
	DB	$CD
	LD	HL,(_@@BGN2)
	DB	$CD
	LD	HL,(_@@@)
	LD	DE,$0065
	ADD	HL,DE
_@SGN_EXIT:
	POP	IY
	RET

@CVSTF:
; function @CVSTF
	PUSH	IY
	DB	$CD
	LD	HL,(_@@@)
	LD	DE,$000E
	ADD	HL,DE
	DB	$EB
_@CVSTF_EXIT:
	POP	IY
	RET

@CVDBL:
; function @CVDBL
	PUSH	IY
	DB	$CD
	LD	HL,(_@@BGN2)
	DB	$CD
	LD	HL,(_@@@)
	LD	DE,$0008
	ADD	HL,DE
_@CVDBL_EXIT:
	POP	IY
	RET

@CVSNG:
; function @CVSNG
	PUSH	IY
	DB	$E5
	DB	$E5
	DB	$EB
	DB	$11
	LD	HL,(_@@TEMP)
	DB	$01
	DB	$08
	DB	$00
	DB	$ED
	DB	$B0
	DB	$21
	LD	HL,(_@@TEMP)
	DB	$CD
	LD	HL,(_@@@)
	LD	DE,$000B
	ADD	HL,DE
	DB	$D1
	DB	$06
	DB	$05
	DB	$ED
	DB	$B0
	DB	$E1
_@CVSNG_EXIT:
	POP	IY
	RET

@ADD:
; function @ADD
	PUSH	IY
	DB	$CD
	LD	HL,(_@@BGN3)
	DB	$CD
	LD	HL,(_@@@)
	LD	DE,$0020
	ADD	HL,DE
	DB	$CD
	LD	HL,(_@@END3)
_@ADD_EXIT:
	POP	IY
	RET

@SUB:
; function @SUB
	PUSH	IY
	DB	$CD
	LD	HL,(_@@BGN3)
	DB	$CD
	LD	HL,(_@@@)
	LD	DE,$0023
	ADD	HL,DE
	DB	$CD
	LD	HL,(_@@END3)
_@SUB_EXIT:
	POP	IY
	RET

@MUL:
; function @MUL
	PUSH	IY
	DB	$CD
	LD	HL,(_@@BGN3)
	DB	$CD
	LD	HL,(_@@@)
	LD	DE,$0026
	ADD	HL,DE
	DB	$CD
	LD	HL,(_@@END3)
_@MUL_EXIT:
	POP	IY
	RET

@DIV:
; function @DIV
	PUSH	IY
	DB	$CD
	LD	HL,(_@@BGN3)
	DB	$CD
	LD	HL,(_@@@)
	LD	DE,$0029
	ADD	HL,DE
	DB	$CD
	LD	HL,(_@@END3)
_@DIV_EXIT:
	POP	IY
	RET

@IDIV:
; function @IDIV
	PUSH	IY
	DB	$CD
	LD	HL,(_@@BGN3)
	DB	$CD
	LD	HL,(_@@@)
	LD	DE,$002C
	ADD	HL,DE
	DB	$CD
	LD	HL,(_@@END3)
_@IDIV_EXIT:
	POP	IY
	RET

@MOD:
; function @MOD
	PUSH	IY
	DB	$CD
	LD	HL,(_@@BGN3)
	DB	$CD
	LD	HL,(_@@@)
	LD	DE,$002F
	ADD	HL,DE
	DB	$CD
	LD	HL,(_@@END3)
_@MOD_EXIT:
	POP	IY
	RET

@POW:
; function @POW
	PUSH	IY
	DB	$CD
	LD	HL,(_@@BGN3)
	DB	$CD
	LD	HL,(_@@@)
	LD	DE,$0059
	ADD	HL,DE
	DB	$CD
	LD	HL,(_@@END3)
_@POW_EXIT:
	POP	IY
	RET

MAIN:
; function MAIN
	PUSH	IY
	LD	HL,(_CAK)
	LD	(IY+$70),L
	LD	(IY+$71),H
	LD	HL,_S0
	LD	(IY+$72),L
	LD	(IY+$73),H
	CALL	@CVSTF
	LD	HL,(_CBK)
	LD	(IY+$70),L
	LD	(IY+$71),H
	LD	HL,_S1
	LD	(IY+$72),L
	LD	(IY+$73),H
	CALL	@CVSTF
	LD	HL,(_FV2)
	LD	(IY+$70),L
	LD	(IY+$71),H
	LD	HL,_S2
	LD	(IY+$72),L
	LD	(IY+$73),H
	CALL	@CVSTF
	LD	HL,(_FV4)
	LD	(IY+$70),L
	LD	(IY+$71),H
	LD	HL,_S3
	LD	(IY+$72),L
	LD	(IY+$73),H
	CALL	@CVSTF
	LD	HL,$0000
	LD	(_Y),HL
_L0:
	LD	HL,$0000
	LD	(_X),HL
_L3:
	LD	HL,(_FX)
	LD	HL,(_X)
	LD	DE,$0027
	OR	A
	SBC	HL,DE
	PUSH	HL
	PUSH	HL
	POP	DE
	POP	HL
	CALL	@CVITF
	LD	HL,(_FY)
	LD	HL,(_Y)
	LD	DE,$000C
	OR	A
	SBC	HL,DE
	PUSH	HL
	PUSH	HL
	POP	DE
	POP	HL
	CALL	@CVITF
	LD	HL,(_CA)
	LD	(IY+$70),L
	LD	(IY+$71),H
	LD	HL,(_FX)
	LD	(IY+$72),L
	LD	(IY+$73),H
	LD	HL,(_CAK)
	LD	(IY+$74),L
	LD	(IY+$75),H
	CALL	@MUL
	LD	HL,(_CB)
	LD	(IY+$70),L
	LD	(IY+$71),H
	LD	HL,(_FY)
	LD	(IY+$72),L
	LD	(IY+$73),H
	LD	HL,(_CBK)
	LD	(IY+$74),L
	LD	(IY+$75),H
	CALL	@MUL
	LD	HL,(_A)
	LD	HL,(_CA)
	PUSH	HL
	PUSH	HL
	POP	DE
	POP	HL
	CALL	@MOVE
	LD	HL,(_B)
	LD	HL,(_CB)
	PUSH	HL
	PUSH	HL
	POP	DE
	POP	HL
	CALL	@MOVE
	LD	HL,$0000
	LD	(_I),HL
_L6:
	LD	HL,(_T)
	LD	(IY+$70),L
	LD	(IY+$71),H
	LD	HL,(_A)
	LD	(IY+$72),L
	LD	(IY+$73),H
	LD	HL,(_A)
	LD	(IY+$74),L
	LD	(IY+$75),H
	CALL	@MUL
	LD	HL,(_TMPF)
	LD	(IY+$70),L
	LD	(IY+$71),H
	LD	HL,(_B)
	LD	(IY+$72),L
	LD	(IY+$73),H
	LD	HL,(_B)
	LD	(IY+$74),L
	LD	(IY+$75),H
	CALL	@MUL
	LD	HL,(_T)
	LD	(IY+$70),L
	LD	(IY+$71),H
	LD	HL,(_T)
	LD	(IY+$72),L
	LD	(IY+$73),H
	LD	HL,(_TMPF)
	LD	(IY+$74),L
	LD	(IY+$75),H
	CALL	@SUB
	LD	HL,(_T)
	LD	(IY+$70),L
	LD	(IY+$71),H
	LD	HL,(_T)
	LD	(IY+$72),L
	LD	(IY+$73),H
	LD	HL,(_CA)
	LD	(IY+$74),L
	LD	(IY+$75),H
	CALL	@ADD
	LD	HL,(_TMPF)
	LD	(IY+$70),L
	LD	(IY+$71),H
	LD	HL,(_FV2)
	LD	(IY+$72),L
	LD	(IY+$73),H
	LD	HL,(_A)
	LD	(IY+$74),L
	LD	(IY+$75),H
	CALL	@MUL
	LD	HL,(_TMPF)
	LD	(IY+$70),L
	LD	(IY+$71),H
	LD	HL,(_TMPF)
	LD	(IY+$72),L
	LD	(IY+$73),H
	LD	HL,(_B)
	LD	(IY+$74),L
	LD	(IY+$75),H
	CALL	@MUL
	LD	HL,(_B)
	LD	(IY+$70),L
	LD	(IY+$71),H
	LD	HL,(_TMPF)
	LD	(IY+$72),L
	LD	(IY+$73),H
	LD	HL,(_CB)
	LD	(IY+$74),L
	LD	(IY+$75),H
	CALL	@ADD
	LD	HL,(_A)
	LD	HL,(_T)
	PUSH	HL
	PUSH	HL
	POP	DE
	POP	HL
	CALL	@MOVE
	LD	HL,(_TMPF)
	LD	(IY+$70),L
	LD	(IY+$71),H
	LD	HL,(_A)
	LD	(IY+$72),L
	LD	(IY+$73),H
	LD	HL,(_A)
	LD	(IY+$74),L
	LD	(IY+$75),H
	CALL	@MUL
	LD	HL,(_TMPF2)
	LD	(IY+$70),L
	LD	(IY+$71),H
	LD	HL,(_B)
	LD	(IY+$72),L
	LD	(IY+$73),H
	LD	HL,(_B)
	LD	(IY+$74),L
	LD	(IY+$75),H
	CALL	@MUL
	LD	HL,(_TMPF)
	LD	(IY+$70),L
	LD	(IY+$71),H
	LD	HL,(_TMPF)
	LD	(IY+$72),L
	LD	(IY+$73),H
	LD	HL,(_TMPF2)
	LD	(IY+$74),L
	LD	(IY+$75),H
	CALL	@ADD
	LD	HL,(_TMPF)
	LD	(IY+$70),L
	LD	(IY+$71),H
	LD	HL,(_FV4)
	LD	(IY+$72),L
	LD	(IY+$73),H
	CALL	@CMP
	PUSH	HL
	LD	HL,$0001
	POP	DE
	EX	DE,HL
	OR	A
	SBC	HL,DE
	LD	HL,$0000
	JR	Z,$+3
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
; if t91 != 0 goto _L6
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
	LD	HL,_S4
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
; if t109 != 0 goto _L3
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
; if t114 != 0 goto _L0
	LD	A,H
	OR	L
	JP	NZ,_L0
_L2:
_MAIN_EXIT:
	POP	IY
	RET


; === String Table ===
_S0:
	DB	$30,$2E,$30,$34,$35,$38,$00
_S1:
	DB	$30,$2E,$30,$38,$33,$33,$33,$00
_S2:
	DB	$32,$00
_S3:
	DB	$34,$00
_S4:
	DB	$20,$00

; === Global Variables (Work Area) ===
_@@TEMP:
	DS	9
_X:
	DS	2
_Y:
	DS	2
_I:
	DS	2
_VAL:
	DS	2
_CA:
	DS	1
_CB:
	DS	1
_FX:
	DS	1
_FY:
	DS	1
_A:
	DS	1
_B:
	DS	1
_T:
	DS	1
_CAK:
	DS	1
_CBK:
	DS	1
_FV2:
	DS	1
_FV4:
	DS	1
_TMPF:
	DS	1
_TMPF2:
	DS	2

; === Runtime Functions ===
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