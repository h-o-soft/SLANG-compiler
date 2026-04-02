; Converted from lib/libdef/liblsx_base.yml
; SLANG Runtime Library (new format)

; @name SLANGINIT
; @calls sWORK,LSXCALLS
LD HL,($0001)
LD (WBOOTBK),HL

; WORK ZERO CLEAR
XOR A
LD HL,__WORK__
LD DE,__WORK__+1
LD BC,__WORKEND__-__WORK__-1
LD (HL),A
LDIR

<<CALLINITIALIZER>>

LD HL,F0C0H
LD (_CTCVEC),HL

LD IY,__IYWORK

CALL MAIN
JP 0


; @name LSXCALLS
BDOS EQU $0005
PRNOUT EQU $05
INPOUT EQU $06
DIRIO EQU $06
DIRIN	EQU	$07
GETS EQU $0A
CONST	EQU	$0B
lSCRN EQU $EFD0
lLOC EQU $EFD6
FCB1	EQU	005CH
FCB2	EQU	006CH
DTA1	EQU	0080H


; @name STOP
; @param_count 0
LD HL,(WBOOTBK)
LD ($00001),HL
JP 0


; @name sSYSTEM
; @param_count 0
; @calls LSXCALLS
EXX
PUSH	BC
PUSH	DE
PUSH	HL
PUSH	IX
PUSH	IY
EXX
CALL	$0005
EXX
POP	IY
POP	IX
POP	HL
POP	DE
POP	BC
EXX
RET


; @name sMSG
; @param_count 0
; @calls LSXCALLS,sMSX
PUSH	HL
LD	H,$0D
JR	sMSG1


; @name sMSX
; @param_count 0
; @calls LSXCALLS,sPRINT
PUSH	HL
LD	H,0
sMSG1:
PUSH	AF
PUSH	DE
sMSX1:
LD	A,(DE)
CP	H
JR	Z,sMSX2
CALL	sPRINT
INC	DE
JR	MSX1
sMSX2:
POP	DE
POP	AF
POP	HL
RET


; @name sLPTOF
; @param_count 0
RET


; @name sLPTON
; @param_count 0
RET


; @name sWIDCH
; @param_count 0
RET


; @name sMPRNT
; @param_count 0
; @calls sPRINT
EX	(SP),HL
.mprnt1
LD	A,(HL)
INC	HL
OR	A
CALL	NZ,sPRINT
OR	A
JR	NZ,.mprnt1
EX	(SP),HL
RET


; @name sNL
; @param_count 0
; @calls sZPRINT,sWORK
PUSH	AF
LD	A,(sXYADR)
OR	A
JR	Z,.nlx
CALL	sZPRINT
DB	$0D,$0A,0
CALL	PCLR
.nlx
POP	AF
RET


; @name sPRINTS
; @param_count 0
; @calls sPRINT
PUSH	AF
LD	A,$20
LTNL1:
CALL	sPRINT
POP	AF
RET


; @name sLTNL
; @param_count 0
; @calls sPRINTS
PUSH	AF
LD	A,$0D
JR	LTNL1


; @name sCSR
; @param_count 0
; @calls sWORK
LD	HL,(sXYADR)
RET


; @name sSCRN
; @param_count 0
; @calls lLOC1
; for LSX-Dodgers
PUSH BC
CALL lLOC1
LD	C,L
LD	A,H
OR	030H
LD	B,A
CALL lSCRN
POP BC
;LD	A,$20
RET


; @name lLOC1
; @param_count 0
; @calls sWORK
PUSH	DE
LD	C,L
LD	B,8
LD	E,H
LD	D,0
LD	HL,(sCRTCD)
LD	L,D
.LOC2
ADD	HL,HL
JR	NC,.LOC3
ADD	HL,DE
.LOC3
DJNZ	.LOC2
ADD	HL,BC
POP	DE
RET


; @name sLOC
; @param_count 0
; @calls sWORK,sPCLR,sZPRINT
CALL	sPCLR
LD	A,H
ADD	A,$20
LD	(sLOCY),A
LD	A,L
ADD	A,$20
LD	(sLOCX),A
CALL	sZPRINT
DB	$1B,"Y"
sLOCY:	DB	$20
sLOCX:	DB	$20
DB	0
AND	A		;CF=0
RET


; @name sPAUSE
; @param_count 0
; @calls sBRKEY,sGETKY,sINKEY,sBRKEY
CALL	sBRKEY
JR	Z,.pause2
CP	$20
JR	NZ,.pause1
.null
CALL	sGETKY
OR	A
JR	NZ,NULL
CALL	sINKEY
CALL	BRKEY1
JR	Z,.pause2

.pause1
EX	(SP),HL
INC	HL
INC	HL
EX	(SP),HL
RET

.pause2
EX	(SP),HL
LD	A,(HL)
INC	HL
LD	H,(HL)
LD	L,A
EX	(SP),HL
RET


; @name sBRKEY
; @param_count 0
; @calls sGETKY
CALL	sGETKY
BRKEY1:
  CP	$1B
  RET	Z
  CP	3
  RET


; @name sHEX
; @param_count 0
; @calls sCAP
CALL	sCAP
SUB	$30
RET	C
CP	$0A
CCF
RET	NC
CP	$11
RET	C
SUB	7
CP	$10
CCF
RET


; @name sCAP
; @param_count 0
CP	"a"
RET	C
CP	"z"+1
RET	NC
SUB	$20
RET


; @name sZPRINT
; @param_count 0
; @calls sSYSTEM
EX	(SP),HL
.zprnt1
LD	A,(HL)
INC	HL
OR	A
JR	Z,.zprnt2
CALL	.zprnt3
JR	.zprnt1
.zprnt2
EX	(SP),HL
RET

.zprnt3
PUSH	BC
PUSH	DE
PUSH	HL
LD	E,A
LD	C,INPOUT
CALL sSYSTEM
POP	HL
POP	DE
POP	BC
RET


; @name BEEP
; @param_count 0
; @calls sPRNT0
PUSH	AF
LD	A,7
JR	sPRNT01


; @name sPRNT0
; @param_count 0
; @calls sPRINT
PUSH	AF
CP	$20
JR	NC,sPRNT01
LD	A,$20
sPRNT01:
CALL	sPRINT
POP	AF
RET


; @name sGETL
; @param_count 0
; @calls sPRINT,sWORK,sFGETL,sKYBFC
PUSH	BC
PUSH	HL
PUSH	DE

;----------------
;Breakを抑制
LD	HL,($0001)
LD	(WBOOTBK),HL
LD	HL,.zgetlbrk
LD	(00001H),HL
LD	(sSPBK),SP
;----------------

CALL	sFGETL
JR	NC,.zgetl2
CALL	sKYBFC

LD	DE,sKBFAD0
LD	A,80
LD	(DE),A
LD	C,GETS
CALL	sSYSTEM
.zgetl2
JR	.zgetnobrk

;----------------
;Break押されて飛んできたのでSPを戻す
.zgetlbrk
LD	HL,(sSPBK)
LD	SP,HL
LD	A,01BH
LD	(sKBFADX),A	; ESC(break)
;----------------
.zgetnobrk

; WBOOTアドレスを戻す
LD	HL,(WBOOTBK)
LD	(00001H),HL

POP	DE
PUSH	DE
LD	B,80
LD	A,(sKBFADX)	;break
CP	$0D
JR	Z,.getp0
CP	9
JR	Z,.getp0
CP	$20
JR	NC,.getp0
LD	A,$1B
LD	(DE),A
INC	DE
DEC	B
JR	.getl2
.getp0
LD	A,(sXYADR)
OR	A
JR	Z,.getl0
LD	C,A
LD	HL,sPRBF
.getp1
LD	A,(HL)
INC	HL
LD	(DE),A
INC	DE
DEC	B
JR	Z,.getl3
DEC	C
JR	NZ,.getp1
.getl0
LD	HL,sKBFADX
.getl1
LD	A,(HL)
INC	HL
CP	9
JR	NZ,.getlt
LD	A,$20
JR	.getlt2
.getlt
CP	$20
JR	C,.getl2
.getlt2
LD	(DE),A
INC	DE
DJNZ	.getl1
JR	.getl3
.getl2
XOR	A
LD	(DE),A
INC	DE
DJNZ	.getl2
.getl3
POP	DE
POP	HL
POP	BC
LD	A,$0D
JP	sPRINT


; @name sFGETL
; @param_count 0
; @calls sWORK,sBRKEY,sINKBF
CALL	.fgetl1
RET	C

LD	HL,$0D00
LD	(sKBFAD1),HL
RET
.fgetl1
CALL	sBRKEY
SCF
RET	Z
CALL	sINKBF
;
CP	$0D
RET	Z
CP	$0A
JR	Z,.fgetl1
;
OR	A
SCF
RET	Z
CP	$1A
SCF
RET	Z
;
CP	$09
JR	NZ,.fgetl2
LD	A,$20
.fgetl2
CALL	sPRINT
JR	.fgetl1


; @name sINKBF
; @param_count 0
; @calls sWORK
PUSH	HL
LD	HL,(sSUBPS)
LD	A,H
XOR	L
JR	Z,.inkbf1
LD	A,H
INC	A
LD	(sSUBPS+1),A
LD	L,A
LD	H,sSUBBF/256
LD	A,(HL)
SCF
.inkbf1
POP	HL
RET


; @name sASC
; @param_count 0
AND	$0F
OR	$30
CP	$3A
RET	C
ADD	A,7
RET


; @name sGETKY
; @param_count 0
; @calls LSXCALLS,sFLGET,GETKY_DOINIT
GETKY_INIT:
JP	GETKY_DOINIT	; 初回のみ実行。実行後NOP×3に自己書き換え
GETKY_MAIN:
PUSH	BC
PUSH	DE
PUSH	HL
LD	E,$FF
LD	C,INPOUT
JR	GETKY1


; @name sFLGET
; @param_count 0
; @calls sSYSTEM
PUSH	BC
PUSH	DE
PUSH	HL
LD	C,DIRIN
GETKY1:
CALL	sSYSTEM
POP	HL
POP	DE
POP	BC
CP	4
JR	NZ,.fl1
LD	A,$1C
RET
.fl1
CP	5
JR	NZ,.fl2
LD	A,$1E
RET
.fl2
CP	$13
JR	NZ,.fl3
LD	A,$1D
RET
.fl3
CP	$18
JR	NZ,.fl4
LD	A,$1F
RET
.fl4
OR	A
JR	NZ,GETKY_FL5
; LSX-Dodgers 1.62c: キーデータ直読み（バージョン不一致時はNOP×3に書き換え済み）
GETKY_PATCH:
LD	A,($EE92)
GETKY_FL5:
AND	A
RET


; @name GETKY_DOINIT
; @param_count 0
; @calls LSXCALLS
; sGETKY初回: LSX-Dodgersバージョン判定＋自己書き換え
PUSH	BC
PUSH	DE
PUSH	HL
; _DOSVER (C=$6F) でバージョン取得
LD	C,$6F
CALL	$0005
; DE = LSX-Dodgersバージョン (1.62c = $0162)
LD	A,D
CP	$01
JR	NZ,.getky_nopatch
LD	A,E
CP	$62
JR	Z,.getky_patchdone
.getky_nopatch:
; バージョン不一致: LD A,(nn) → NOP×3 に書き換え
XOR	A
LD	(GETKY_PATCH),A		; $3A(LD A,(nn)) → $00(NOP)
LD	(GETKY_PATCH+1),A	; → NOP
LD	(GETKY_PATCH+2),A	; → NOP
.getky_patchdone:
; 初期化コード自体を無効化: JP → NOP×3
XOR	A
LD	(GETKY_INIT),A
LD	(GETKY_INIT+1),A
LD	(GETKY_INIT+2),A
POP	HL
POP	DE
POP	BC
JR	GETKY_MAIN


; @name sINKEY
; @param_count 0
; @calls sGETKY
CALL	sGETKY
OR	A
JR	Z,sINKEY
RET


; @name sLPRNT
; @param_count 0
; @calls sSYSTEM
PUSH	BC
PUSH	DE
PUSH	HL
LD	E,A
LD	C,$05		;PRNOUT
CALL	sSYSTEM
POP	HL
POP	DE
POP	BC
ADD	A,$01
RET


; @name sKYBFC
; @param_count 0
; @calls sSYSTEM,sWORK
PUSH	AF
PUSH	BC
PUSH	DE
PUSH	HL
LD	HL,0
LD	(sSUBPS),HL
.keybc1
LD	E,$FF
LD	C,$06
CALL	sSYSTEM
OR	A
JR	NZ,.keybc1
POP	HL
POP	DE
POP	BC
POP	AF
RET


; @name sPRINT
; @param_count 0
; @calls sWORK,sCTRL,sSYSTEM
PUSH	BC
PUSH	DE
PUSH	HL
PUSH	AF
LD	E,A
CP	$1B
JR	C,sCTRL
.print1
LD	HL,(sXYADR)
LD	H,0
LD	A,L
SUB	80
JR	C,.pr1
LD	L,0
.pr1
INC	L
LD	(sXYADR),HL
DEC	L
LD	BC,sPRBF
ADD	HL,BC
LD	(HL),E
sPRINT2:
LD	C,$06		;INPOUT
CALL	sSYSTEM
sPRINT3:
POP	AF
POP	HL
POP	DE
POP	BC
AND	A
RET


; @name sPCLR
; @param_count 0
; @calls sWORK
XOR	A
LD	(sXYADR),A
RET


; @name sCTRL
; @param_count 0
; @calls sWORK,sPCLR,sSYSTEM,sPRINT,sZPRINT
CP	$0D
JR	Z,.CTRL_M
CP	7
JR	Z,sPRINT2
CP	$0C
JR	Z,.CTRL_L
CP	$1A
JR	Z,.CTRL_Z
CP	$0B
JR	Z,.CTRL_K
CP	9
JR	Z,.CTRL_I
JR	sPRINT3
.CTRL_M
CALL	sPCLR
LD	C,$06		;INPOUT
CALL	sSYSTEM
LD	E,$0A		;CRLF
JR	sPRINT2
.CTRL_L
CALL	sPCLR
CALL	sZPRINT
.CTLX1
DB	$1B,"E",0
JR	sPRINT3
.CTRL_Z
CALL	sZPRINT
.CTZX1
DB	$1B,"J",0
JR	sPRINT3
.CTRL_K
CALL	sPCLR
CALL	sZPRINT
.CTKX1
DB	$1B,"H",0
JR	sPRINT3
.CTRL_I
LD	A,(sXYADR)
AND	7
LD	B,A
LD	A,8
SUB	B
LD	B,A
.CTRL_I1
LD	A,$20
CALL	sPRINT
DJNZ	.CTRL_I1
JR	sPRINT3


; @name sBOOT
; @param_count 0
LD	HL,0
LD	($0001),HL
JP	0


; @name sHLHEX
; @param_count 0
; @calls sHEX
PUSH	HL
PUSH	DE
CALL	HLHEX5
JR	C,HLHEX3
LD	L,A
LD	H,0
HLHEX1:
CALL	HLHEX6
JR	C,HLHEX2
ADD	HL,HL
ADD	HL,HL
ADD	HL,HL
ADD	HL,HL
ADD	A,L
LD	L,A
LD	A,$00
ADC	A,H
LD	H,A
JR	HLHEX1
HLHEX2:
POP	AF
POP	AF
XOR	A
RET
HLHEX3:
POP	DE
POP	HL
SCF
RET
HLHEX4:
INC	DE
HLHEX5:
LD	A,(DE)
CP	$20
JR	Z,HLHEX4
HLHEX6:
LD	A,(DE)
CALL	sHEX
RET	C
INC	DE
RET


; @name sWORK
; @param_count 0
; @works sCRTCD:1,sXYADR:2,sKBFAD:128,sKBFAD0:1,sKBFAD1:1,sKBFADX:81,sPRBF:80,sSUBPS:2,sSUBBF:256,sSPBK:2,WBOOTBK:2,_CTCVEC:2

