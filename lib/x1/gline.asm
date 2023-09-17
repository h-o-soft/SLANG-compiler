;###################
;	G-LINE
;###################

#LIB LINEALL
	; return address
	POP HL
	LD (LRETADR),HL

	; color
	POP HL
	LD A,L
	LD (X1GLINE._COLOR),A

	; Y1(1) C
	; X1(2) BE
	; Y2(1) D
	; X2(2) HL

	; Y2
	POP HL
	LD A,L
	; X2
	POP HL
	LD D,H
	LD E,L
	; Y1
	POP HL
	LD B,L

	LD HL,X1GLINE._LINEDATA
	LD (HL),B ; Y1
	INC HL
	POP BC  ; X1
	LD (HL),C
	INC HL
	LD (HL),B
	INC HL
	LD (HL),A ; Y2
	INC HL
	LD (HL),E ; X2
	INC HL
	LD (HL),D

	PUSH IY
	PUSH IX

	; OR
	LD A,$28    ; JR Z,...
	LD (.zeroornonzero),A
	CALL X1GLINE.SETOR
	CALL .drawlines

	; DELETE
	LD A,$20    ; JR NZ,...
	LD (.zeroornonzero),A
	CALL X1GLINE.SETDEL
	CALL .drawlines

	POP IX
	POP IY

	LD HL,(LRETADR)
	PUSH HL
	RET

.drawlines
	LD A,(X1GLINE._COLOR)
	LD B,3
	LD  HL,X1GLINE.LINECOMMON+22  ;SETB、6を足すとSETR、12を足すとSETGになる
.drawloop
	LD (.setjmpadr+1),HL
	BIT 0,A
.zeroornonzero
	JR  Z,.nocolor
	PUSH BC
	PUSH HL
	PUSH AF
.setjmpadr
	CALL $0000
	CALL X1GLINE.MEMCOM
	POP AF
	POP HL
	POP BC
.nocolor
	RRCA
	LD DE,6
	ADD HL,DE
	DJNZ .drawloop
	RET

LRETADR:
	DW 2

#ENDLIB


#LIB X1GLINE
	;ORG	$B000
	LD	IX,($C200)
	JP	DAMY
	JP	LINE
	JP	SETOR
	JP	SETXOR
	JP	SET320
	JP	SET640
	JP	SETB
	JP	SETB1
	JP	SETR
	JP	SETR1
	JP	SETG
	JP	SETG1
	JP	MEMCOM
	JP	SETDEL		; 追加したナニカ。線を消す。
	DS	2
_CMDATA:
	DW	_LINEDATA
SETOR:
	LD	A,$F6
	LD	(_ORXOR),A
	LD	A,0
	CALL	UPDATEREV
	LD	(_REVFLG),A
	RET
SETXOR:
	LD	A,$EE
	LD	(_ORXOR),A
	LD	A,0
	CALL	UPDATEREV
	LD	(_REVFLG),A
	RET
SET320:
	XOR	A
	LD	(NOP320),A
	LD	A,$28
	JR	SET50OR28
SET640:
	LD	A,(BRG+1)
	AND	$F0
	LD	(BRG+1),A
	LD	A,$29
	LD	(NOP320),A
	LD	A,$50
SET50OR28:
	LD	(YODOWNB7-6),A
	LD	(YODOWNB6-6),A
	LD	(YODOWNB5-6),A
	LD	(YODOWNB4-6),A
	LD	(YODOWNB3-6),A
	LD	(YODOWNB2-6),A
	LD	(YODOWNB1-6),A
	LD	(YODW28OR50+1),A
	LD	(TADWE7-6),A
	LD	(TADWE6-6),A
	LD	(TADWE5-6),A
	LD	(TADWE4-6),A
	LD	(TADWE3-6),A
	LD	(TADWE2-6),A
	LD	(TADWE1-6),A
	LD	(TADWE0-6),A
	RET
LINE:
	CALL	CPY1Y2
	EX	AF,AF'
	OUT	(C),A
BASYODATA:
	LD	HL,$0000
	LD	(HL),$19
	INC	L
	LD	(HL),$D2
	INC	L
MOTODATA:
	LD	(HL),$00
	RET
CPY1Y2:
	LD	A,C
	CP	B
	JR	NC,YYSET
EX1AND2:
	LD	C,B
	LD	B,A
	EX	HL,DE
YYSET:
	LD	A,C
	LD	(_Y2),A
	SUB	B
	LD	(_YY),A
	LD	(_X2),HL
CPX1X2:
	LD	A,D
	CP	H
	JR	Z,SAMEDANDH
	JP	NC,HIDARI
MIGI:
	LD	A,E
	AND	$07
	LD	(_STABIT),A
	LD	A,L
	AND	$07
	LD	(_ENDBIT),A
	PUSH	DE
	XOR	A
	SBC	HL,DE
	LD	(_XX),HL
	LD	(_SAYU),A
	JP	GETADDRESS
SAMEDANDH:
	LD	A,E
	CP	L
	JR	NC,HIDARI
	JR	MIGI
HIDARI:
	LD	A,E
	AND	$07
	XOR	$07
	LD	(_STABIT),A
	LD	A,L
	AND	$07
	XOR	$07
	LD	(_ENDBIT),A
	PUSH	DE
	EX	HL,DE
	SBC	HL,DE
	LD	(_XX),HL
	LD	A,1
	LD	(_SAYU),A
GETADDRESS:
	POP	DE
	LD	L,B
	CALL	GETADDHL
	LD	(_STAADD),HL
CPXXYY:
	LD	DE,(_XX)
	LD	HL,(_YY)
	SBC	HL,DE
	JR	NC,TATE
YOKO:
	LD	B,D
	LD	C,E
	LD	HL,(_YY)
	CALL	DIVHLDE
	JP	LINEYOKO
TATE:
	LD	BC,(_YY)
	EX	HL,DE
	CALL	DIVHLDE
	JP	LINETATE
GETADDHL:
	SRL	D
	RR	E
	SRL	D
	RR	E
	SRL	E
	LD	H,D
BRG:
	LD	D,$C0
	PUSH	DE
	PUSH	HL
	LD	A,L
	AND	$07
	LD	L,H
	LD	H,A
	ADD	HL,HL
	ADD	HL,HL
	ADD	HL,HL
	LD	B,H
	LD	C,L
	POP	HL
	LD	A,L
	AND	$F8
	LD	L,A
	LD	E,A
	LD	D,H
	ADD	HL,HL
	ADD	HL,HL
	ADD	HL,DE
NOP320:
	ADD	HL,HL
	ADD	HL,BC
	pop	DE
	ADD	HL,DE
	RET
DIVHLDE:
	LD	DE,$0000
	LD	A,$10
DIV1:
	SLA	E
	RL	D
	ADC	HL,HL
	SBC	HL,BC
	JR	C,DIV2
	INC	E
	DEC	A
	JP	NZ,DIV1
	RET
DIV2:
	ADD	HL,BC
	DEC	A
	JP	NZ,DIV1
	RET
LINEYOKO:
	LD	HL,(_XX)
	SRL	H
	RR	L
	SRL	H
	RR	L
	SRL	L
	EX	AF,AF'
	LD	A,L
	INC	A
	EX	AF,AF'
	LD	A,(_SAYU)
	OR	A
	JP	NZ,YOHIDARISET
YOMIGISET:
	LD	HL,(_ORXOR)
	LD	H,$80
	LD	(YOKO7),HL
	LD	H,$40
	LD	(YOKO6),HL
	LD	H,$20
	LD	(YOKO5),HL
	LD	H,$10
	LD	(YOKO4),HL
	LD	H,$08
	LD	(YOKO3),HL
	LD	H,$04
	LD	(YOKO2),HL
	LD	H,$02
	LD	(YOKO1),HL
	LD	H,$01
	LD	(YOKO0),HL
	LD	A,$03
	LD	(YOINCADRS),A
	JP	YOKOSET
YOHIDARISET:
	LD	HL,(_ORXOR)
	LD	H,$01
	LD	(YOKO7),HL
	LD	H,$02
	LD	(YOKO6),HL
	LD	H,$04
	LD	(YOKO5),HL
	LD	H,$08
	LD	(YOKO4),HL
	LD	H,$10
	LD	(YOKO3),HL
	LD	H,$20
	LD	(YOKO2),HL
	LD	H,$40
	LD	(YOKO1),HL
	LD	H,$80
	LD	(YOKO0),HL
	LD	A,$0B
	LD	(YOINCADRS),A
YOKOSET:
	LD	HL,(_ENDBIT)
	LD	BC,_YOENDDATA
	ADD	HL,BC
	LD	L,(HL)
	LD	(BASYODATA+1),HL
	LD	(HL),$C3
	INC	L
	LD	(HL),LOW Check
	INC	L
	LD	A,(HL)
	LD	(MOTODATA+1),A
	LD	(HL),HIGH Check
	INC	L
	INC	L
	LD	(CheckRET+1),HL
	LD	HL,(_ENDBIT)
	LD	BC,_YOSTADATA+1
	ADD	HL,BC
	LD	L,(HL)
	LD	H,HIGH Check
	LD	(CheckRET-2),HL
	LD	HL,(_STABIT)
	DEC	BC
	ADD	HL,BC
	LD	L,(HL)
	PUSH	HL
	LD	BC,(_STAADD)
	LD	HL,$8000
	LD	IY,YOINCADRS
	IN	A,(C)
	RET
	ALIGN	256
_YOSTADATA:
	DB	YOKO7
	DB	YOKO6
	DB	YOKO5
	DB	YOKO4
	DB	YOKO3
	DB	YOKO2
	DB	YOKO1
	DB	YOKO0
	DB	YOINCADRS
_YOENDDATA:
	DB	YOKO7+2
	DB	YOKO6+2
	DB	YOKO5+2
	DB	YOKO4+2
	DB	YOKO3+2
	DB	YOKO2+2
	DB	YOKO1+2
	DB	YOKO0+4
YOINCADRS:
	INC	BC
	IN	A,(C)
YOKO7:
	OR	$80
	ADD	HL,DE
	JP	NC,YOKO6
YODOWN7:
	OUT	(C),A
	LD	A,B
	ADD	A,$08
	LD	B,A
	AND	$38
	JP	NZ,YODOWNB7
	LD	A,C
	ADD	A,$50
	LD	C,A
	LD	A,B
	ADC	A,$C0
	LD	B,A
YODOWNB7:
	IN	A,(C)
YOKO6:
	OR	$40
	ADD	HL,DE
	JP	NC,YOKO5
YODOWN6:
	OUT	(C),A
	LD	A,B
	ADD	A,$08
	LD	B,A
	AND	$38
	JP	NZ,YODOWNB6
	LD	A,C
	ADD	A,$50
	LD	C,A
	LD	A,B
	ADC	A,$C0
	LD	B,A
YODOWNB6:
	IN	A,(C)
YOKO5:
	OR	$20
	ADD	HL,DE
	JP	NC,YOKO4
YODOWN5:
	OUT	(C),A
	LD	A,B
	ADD	A,$08
	LD	B,A
	AND	$38
	JP	NZ,YODOWNB5
	LD	A,C
	ADD	A,$50
	LD	C,A
	LD	A,B
	ADC	A,$C0
	LD	B,A
YODOWNB5:
	IN	A,(C)
YOKO4:
	OR	$10
	ADD	HL,DE
	JP	NC,YOKO3
YODOWN4:
	OUT	(C),A
	LD	A,B
	ADD	A,$08
	LD	B,A
	AND	$38
	JP	NZ,YODOWNB4
	LD	A,C
	ADD	A,$50
	LD	C,A
	LD	A,B
	ADC	A,$C0
	LD	B,A
YODOWNB4:
	IN	A,(C)
YOKO3:
	OR	$08
	ADD	HL,DE
	JP	NC,YOKO2
YODOWN3:
	OUT	(C),A
	LD	A,B
	ADD	A,$08
	LD	B,A
	AND	$38
	JP	NZ,YODOWNB3
	LD	A,C
	ADD	A,$50
	LD	C,A
	LD	A,B
	ADC	A,$C0
	LD	B,A
YODOWNB3:
	IN	A,(C)
YOKO2:
	OR	$04
	ADD	HL,DE
	JP	NC,YOKO1
YODOWN2:
	OUT	(C),A
	LD	A,B
	ADD	A,$08
	LD	B,A
	AND	$38
	JP	NZ,YODOWNB2
	LD	A,C
	ADD	A,$50
	LD	C,A
	LD	A,B
	ADC	A,$C0
	LD	B,A
YODOWNB2:
	IN	A,(C)
YOKO1:
	OR	$02
	ADD	HL,DE
	JP	NC,YOKO0
YODOWN1:
	OUT	(C),A
	LD	A,B
	ADD	A,$08
	LD	B,A
	AND	$38
	JP	NZ,YODOWNB1
	LD	A,C
	ADD	A,$50
	LD	C,A
	LD	A,B
	ADC	A,$C0
	LD	B,A
YODOWNB1:
	IN	A,(C)
YOKO0:
	OR	$01
	OUT	(C),A
	ADD	HL,DE
	JP	NC,YOINCADRS
YODOWN0:
	LD	A,B
	ADD	A,$08
	LD	B,A
	AND	$38
	JP	NZ,YOINCADRS
	LD	A,C
YODW28OR50:
	ADD	A,$50
	LD	C,A
	LD	A,B
	ADC	A,$C0
	LD	B,A
	JP	(IY)
Check:
	EX	AF,AF'
	DEC	A
	RET	Z
	EX	AF,AF'
	ADD	HL,DE
	JP	NC,$0000
CheckRET:
	JP	$0000

	ALIGN	256
_TATEDATA:
	DB	TATE7
	DB	TATE6
	DB	TATE5
	DB	TATE4
	DB	TATE3
	DB	TATE2
	DB	TATE1
	DB	TATE0
	DW	0
TATE7:
	IN	A,(C)
	OR	$80
	OUT	(C),A
	LD	A,B
	ADD	A,$08
	LD	B,A
	AND	$38
	JP	NZ,TADWE7
	LD	A,C
	ADD	A,$50
	LD	C,A
	LD	A,B
	ADC	A,$C0
	LD	B,A
TADWE7:
	ADD	HL,DE
	JR	NC,TATE7
TATE6:
	IN	A,(C)
	OR	$40
	OUT	(C),A
	LD	A,B
	ADD	A,$08
	LD	B,A
	AND	$38
	JP	NZ,TADWE6
	LD	A,C
	ADD	A,$50
	LD	C,A
	LD	A,B
	ADC	A,$C0
	LD	B,A
TADWE6:
	ADD	HL,DE
	JR	NC,TATE6
TATE5:
	IN	A,(C)
	OR	$20
	OUT	(C),A
	LD	A,B
	ADD	A,$08
	LD	B,A
	AND	$38
	JP	NZ,TADWE5
	LD	A,C
	ADD	A,$50
	LD	C,A
	LD	A,B
	ADC	A,$C0
	LD	B,A
TADWE5:
	ADD	HL,DE
	JR	NC,TATE5
TATE4:
	IN	A,(C)
	OR	$10
	OUT	(C),A
	LD	A,B
	ADD	A,$08
	LD	B,A
	AND	$38
	JP	NZ,TADWE4
	LD	A,C
	ADD	A,$50
	LD	C,A
	LD	A,B
	ADC	A,$C0
	LD	B,A
TADWE4:
	ADD	HL,DE
	JR	NC,TATE4
TATE3:
	IN	A,(C)
	OR	$08
	OUT	(C),A
	LD	A,B
	ADD	A,$08
	LD	B,A
	AND	$38
	JP	NZ,TADWE3
	LD	A,C
	ADD	A,$50
	LD	C,A
	LD	A,B
	ADC	A,$C0
	LD	B,A
TADWE3:
	ADD	HL,DE
	JR	NC,TATE3
TATE2:
	IN	A,(C)
	OR	$04
	OUT	(C),A
	LD	A,B
	ADD	A,$08
	LD	B,A
	AND	$38
	JP	NZ,TADWE2
	LD	A,C
	ADD	A,$50
	LD	C,A
	LD	A,B
	ADC	A,$C0
	LD	B,A
TADWE2:
	ADD	HL,DE
	JR	NC,TATE2
TATE1:
	IN	A,(C)
	OR	$02
	OUT	(C),A
	LD	A,B
	ADD	A,$08
	LD	B,A
	AND	$38
	JP	NZ,TADWE1
	LD	A,C
	ADD	A,$50
	LD	C,A
	LD	A,B
	ADC	A,$C0
	LD	B,A
TADWE1:
	ADD	HL,DE
	JR	NC,TATE1
TATE0:
	IN	A,(C)
	OR	$01
	OUT	(C),A
	LD	A,B
	ADD	A,$08
	LD	B,A
	AND	$38
	JP	NZ,TADWE0
	LD	A,C
	ADD	A,$50
	LD	C,A
	LD	A,B
	ADC	A,$C0
	LD	B,A
TADWE0:
	ADD	HL,DE
	JR	NC,TATE0
TAINCADRS:
	INC	BC
	JP	(IY)
Check2:
	OUT	(C),A
	EX	AF,AF'
	CP	C
	JR	Z,CheckR
	EX	AF,AF'
	LD	A,B
	RET
CheckR:
	EX	AF,AF'
	LD	A,(_ENDB)
	CP	B
	JR	Z,TATERET
	LD	A,B
	RET
TATERET:
	LD	HL,$0000
	LD	(HL),$ED
	INC	L
	LD	(HL),$79
	INC	L
	LD	(HL),$78
	POP	HL
	RET
LINETATE:
	PUSH	DE
	LD	DE,(_X2)
	LD	A,(_Y2)
	LD	L,A
	CALL	GETADDHL
	LD	(_ENDB-1),HL
	EX	AF,AF'
	LD	A,L
	EX	AF,AF'
	POP	DE
	POP	HL
	LD	A,(_SAYU)
	OR	A
	JP	NZ,TAHIDARISET
TAMIGISET:
	LD	HL,(_ORXOR)
	LD	H,$80
	LD	(TATE7+2),HL
	LD	H,$40
	LD	(TATE6+2),HL
	LD	H,$20
	LD	(TATE5+2),HL
	LD	H,$10
	LD	(TATE4+2),HL
	LD	H,$08
	LD	(TATE3+2),HL
	LD	H,$04
	LD	(TATE2+2),HL
	LD	H,$02
	LD	(TATE1+2),HL
	LD	H,$01
	LD	(TATE0+2),HL
	LD	A,$03
	LD	(TAINCADRS),A
	JP	TATESET
TAHIDARISET:
	LD	HL,(_ORXOR)
	LD	H,$01
	LD	(TATE7+2),HL
	LD	H,$02
	LD	(TATE6+2),HL
	LD	H,$04
	LD	(TATE5+2),HL
	LD	H,$08
	LD	(TATE4+2),HL
	LD	H,$10
	LD	(TATE3+2),HL
	LD	H,$20
	LD	(TATE2+2),HL
	LD	H,$40
	LD	(TATE1+2),HL
	LD	H,$80
	LD	(TATE0+2),HL
	LD	A,$0B
	LD	(TAINCADRS),A
TATESET:
	LD	HL,(_ENDBIT)
	LD	BC,_TATEDATA
	ADD	HL,BC
	LD	A,(HL)
	ADD	A,4
	LD	L,A
	LD	(TATERET+1),HL
	LD	(HL),$CD
	INC	L
	LD	(HL),LOW Check2
	INC	L
	LD	(HL),HIGH Check2
	LD	HL,(_STABIT)
	ADD	HL,BC
	LD	L,(HL)
	PUSH	HL
	LD	BC,(_STAADD)
	LD	HL,$8000
	LD	IY,TATE7
	RET
SETB:
	LD	A,$40
	LD	(BRG+1),A
	RET
SETB1:
	LD	A,$44
	LD	(BRG+1),A
	RET
SETR:
	LD	A,$80
	LD	(BRG+1),A
	RET
SETR1:
	LD	A,$84
	LD	(BRG+1),A
	RET
SETG:
	LD	A,$C0
	LD	(BRG+1),A
	RET
SETG1:
	LD	A,$C4
	LD	(BRG+1),A
	RET
MEMCOM:
	LD	HL,(_CMDATA)
	LD	B,(HL)
	INC	HL
	LD	E,(HL)
	INC	HL
	LD	D,(HL)
	INC	HL
	LD	C,(HL)
	INC	HL
	LD	A,(HL)
	INC	HL
	LD	H,(HL)
	LD	L,A
	JP	LINE

SETDEL:
	LD	A,$E6		; AND(線を消す)
	LD	(_ORXOR),A
	LD	A,1
	CALL	UPDATEREV
	LD	(_REVFLG),A
	RET

UPDATEREV:
	PUSH	AF
	PUSH	BC
	PUSH	DE
	PUSH	HL

	; _REVFLGと異なっていた場合はXORして反転させる
	LD	HL,_REVFLG
	CP	(HL)
	JR	Z,.noupdate		; 異なっていないので何もしない

	; 全ての値を反転させる(その上でANDすると該当部分の色が消える)
	LD	DE, 5
	LD	HL,YOMIGISET+4
	CALL	XORPROC
	LD	HL,YOHIDARISET+4
	CALL	XORPROC
	LD	HL,TAMIGISET+4
	CALL	XORPROC
	LD	HL,TAHIDARISET+4
	CALL	XORPROC

.noupdate
	POP	HL
	POP	DE
	POP	BC
	POP	AF
	RET

XORPROC:
	LD	B,8
.xorloop
	LD	A,(HL)
	CPL
	LD	(HL),A
	ADD	HL,DE
	DJNZ	.xorloop
	RET




_ENDADD:
	DB	$00
	DB	$00
_ENDB:
	DB	0
_STAADD:
	DW	0
_ENDBIT:
	DW	0
_STABIT:
	DW	0
_XX:
	DW	0
_YY:
	DW	0
_SAYU:
	DB	0
_ORXOR:
	DB	$F6
_X2:
	DW	0
_Y2:
	DW	0
_REVFLG:
	DB	0
_COLOR:
	DB	0
_LINEDATA:
	DS	6
DAMY:
;

#ENDLIB
