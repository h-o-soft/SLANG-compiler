;******************************************
;***                                    ***
;***    XBIOS FOR NEC PC-8801 SERIES    ***
;***    VERSION 1.00 , REVISION 1.00    ***
;***                                    ***
;***    28-APRIL-1986, (C) TITY SOFT    ***
;***                                    ***
;******************************************

;	.Z80
;	.PHASE	00000H

	ORG	00000H

[XBIOS]
;	PUBLIC	AT_GETL
;	PUBLIC	AT_PRINT
;	PUBLIC	AT_INKEY
;	PUBLIC	AT_BRKEY
;	PUBLIC	AT_ADRC2
;
;	PUBLIC	AT_WRI
;	PUBLIC	AT_WRD
;	PUBLIC	AT_RDI
;	PUBLIC	AT_RDD
;	PUBLIC	AT_WIDTH
;	PUBLIC	AT_DSPXY
;	PUBLIC	AT_KBUF
;	PUBLIC	AT_WIDCH
;	PUBLIC	AT_LPOUT
;	PUBLIC	AT_SNDDT
;	PUBLIC	AT_GETDT
;
;	PUBLIC	DATFUNC
;	PUBLIC	PORT30
;	PUBLIC	CMTWRITE
;	PUBLIC	CMTLOAD
;	PUBLIC	MONVSW
;	PUBLIC	STRPRT
;	PUBLIC	CSRLT

;PC88	EQU	0	;True if to be run under PC-8801 series
;			;,In case of PC-8001 series,make this False

;AT_STACK	EQU	01000H		; BIOS STACK POI
AT_STACK	EQU	0F300H		; BIOS STACK POI
;SYSTEM	EQU	01500H		; SYSTEM ENTRYA
HOT	EQU	1FFAH		; HOT START ADDRESS
PA	EQU	0FCH
PB	EQU	PA+1
PC	EQU	PA+2
CW	EQU	PA+3


; このあたりは常にRAM側にあった方がいいので追い出す
WORK_HEAD	EQU	$FF00
;
PORT30	EQU	WORK_HEAD+0
PORT31	EQU	PORT30+1
PORT40	EQU	PORT31+1


RCF	MACRO
	SCF
	CCF
	ENDM

ONSTOP	MACRO	lb
	IN	A,(9)
	BIT	0,A
	JP	Z,lb
	ENDM
;==========================================
;	BIOS COLD START ENTRY	(0000H)
;==========================================
START:
	JP	BINIT

;==========================================
;	LINE INPUT ENTRY	(0003H)
;==========================================
AT_GETL:
	JP	INPUT
;
	DB	0

;------------------------------------------
;	 NUMBER OF WIDTH-X	(0007H)
;
AT_WIDTH:	DB	80

	JP	RSTJOB		; RST 008H


;==========================================
;	STRING PRINT BY [DE]	(000BH)
;==========================================
;
AT_PRNTS:
	JP	STRPRT

;------------------------------------------
;	 CURSOR LOCATION WORK	(000EH)
AT_DSPXY:
CURX:	DB	0		;(000EH)
CURY:	DB	0		;(000FH)

;
	JP	RSTJOB		; RST	010H

;==========================================
;	PRINT CHARACTER BY ACC.	(0013H)
;==========================================
;
AT_PRINT:
	JP	PRTCHR
;
	DB	0
	DB	0
;
	JP	RSTJOB		;RST 018H

;==========================================
;	INKEY ENTRY		(001BH)
;==========================================
;
AT_INKEY:
	JP	INKEYJ
;
	DB	0
	DB	0
;
	JP	RSTJOB		; RST 020H
;
	JP	NORET
;
	DB      0

;------------------------------------------
;	NULL/CLS CHARACTER	(0027H)
;
NULCHR:	DB	' '
;
	JP	RSTJOB		; RST 028H
;
	JP	NORET
;
	DB	0
	DB	0
;
	JP	RSTJOB		; RST 030H
;
	JP	NORET
;
	DB	0
;
	DB	0
	JP	RSTJOB		; RST 038H

;==========================================
;	CMT R/W			(003BH)
;==========================================
;
AT_WRI:				;(003BH)
	JP	WRI
AT_WRD:				;(003EH)
	JP	WRD
AT_RDI:				;(0041H)
	JP	RDI
AT_RDD:				;(0044H)
	JP	RDD
AT_VFD:				;(0047H)
	JP	VFD

;==========================================
;	STOP KEY CHECK		(004AH)
;==========================================
;
AT_BRKEY:
	JP	STPCHK

;==========================================
;       WIDTH-X SET ENTRY	(004DH)
;==========================================
;
AT_WIDCH:
	JP	WIDTHJ

;-------------------------------------------------

;==========================================
;	VRAM ADDRESS CALC SUB	(0050H)
;==========================================
;
AT_ADRC2:
	JP	VADRBC

;==========================================
;	PRINTER OUTPUT		(0053H)
;==========================================
;
AT_LPOUT:
	JP	LPTOUT

;==========================================
;	SEND COMMAND TO 8031	(0056H)
;==========================================
;
AT_SNDCM:
	JP	SNDCOM

;==========================================
;	SEND DATA TO 8031	(0059H)
;==========================================
;
AT_SNDDT:
	JP	SNDDAT

;==========================================
;	GET DATA FROM 8031	(005CH)
;==========================================
;
AT_GETDT:
	JP	GETDAT

;==========================================
;	LOCATE(S-OS)		(005FH)
;==========================================
;
AT_LOCATE:
	JP	LOC

;==========================================
;	SET TEXT ATR		(0062H)
;==========================================
AT_TEXTATR:
	JP	SetTextAtr

;==========================================
;	LPTON			(0065H)
;==========================================
AT_LPTON:
	JP	LPTON

;==========================================
;	LPTOF			(0068H)
;==========================================
AT_LPTOF:
	JP	LPTOF

;==========================================
;	LPRNT			(006BH)
;==========================================
AT_LPRNT:
	JP	LPRNT

;==========================================
;	PRINT			(006EH)
;==========================================
S_PRINT:
	JP	PRINT

;==========================================
;	SCRN			(0071H)
;==========================================
S_SCRN:
	JP	SCRN

;==========================================
;	GETKY			(0074H)
;==========================================
S_GETKY:
	JP	GETKY

;==========================================
;	FLGET			(0077H)
;==========================================
S_FLGET:
	JP	FLGET

;==========================================
;	INKEY			(007AH)
;==========================================
S_INKEY:
	JP	sINKEY

;==========================================
;	CSR			(007DH)
;==========================================
S_CSR:
	JP	CSR

;==========================================
;	HLHEX			(0080H)
;==========================================
S_HLHEX:
	JP	HLHEX

;==========================================
;	KBFAD			(0083H)
;==========================================
_KBFAD:	DW	XBIOS.AT_KBUF

;==========================================
;	GETL			(0085H)
;==========================================
S_GETL:	JP	GETL

;###############################################

;*****************************
;
;	BIOS INITIALIZE
;
;*****************************
BINIT:
	DI
#IF exists SYSTEM
#ELSE
	EX	(SP),HL
	LD	(STACKSP),HL
#ENDIF

	LD	SP,AT_STACK
	CALL	INICRT

#IF exists SYSTEM
	JP	SYSTEM
#ELSE
	LD HL,(STACKSP)
	PUSH HL
	RET
#ENDIF

;----------------------------
;	GET WIDTH VALUE
;----------------------------
GETWID:
	LD	A,(AT_WIDTH)
	CP	40+1
	LD	A,40
	RET	C
	LD	A,80
	RET

;********************************
;	LINE INPUT ROUTINE
;********************************
INPUT:
	PUSH	HL
	PUSH	BC
	PUSH	DE		; MEMO: 格納アドレス
	LD	L,E		; 格納アドレスをHLにコピー
	LD	H,D
	INC	DE		; 
	LD	BC,255
	LD	(HL),0
	LDIR			; DEのアドレスから256バイトぶんを0で埋める
GETLL:
	CALL	INKEY1
	CP	0DH
	JR	Z,GETSCN
	CP	01BH		;;03H
	JR	Z,GETSCN
	CALL	PRTCHR
	JR	GETLL

#IF NAME_SPACE_DEFAULT.COLOR_MODE==0
GETSCN:
	PUSH	AF		; 最終入力文字を保存
	LD	A,(CURY)	; A=入力行
	LD	HL,CNTTBL
	LD	C,A
	LD	B,0
	ADD	HL,BC		; HL=行接続情報のアドレス
	LD	C,B		; C=0
	LD	B,A		; B=入力行
CNTSC1:	DEC	HL		; 前の行が接続されているか調べるために1行戻す
	DEC	B		; VADRVC用の行番号も1つ戻す
	LD	A,(HL)		; 接続の
	OR	A		; チェック
	JR	NZ,CNTSC1	; つながってたらどんどん前に戻る
	;
	INC	HL		; つながってなかったらつながってない行に戻す。そもそもつながってなかったら入力行アドレスのまま
	PUSH	HL
	INC	B		; Bは入力先頭行
	CALL	VADRBC
				; 入力先頭位置のアドレスを算出(行頭のアドレスになっている)
	EX	(SP),HL		; MEMO: VRAMアドレスをスタックに積み、CNTTBLのアドレスをHLに入れる
CNTSC2:	INC	B		; 行を次に進める
	LD	A,(HL)		; つながっているかチェック
	OR	A
	JR	Z,CNTSC3	; つながっていなければ終了
	INC	HL		; つながっている場合は次行を調べる
	JR	CNTSC2
	;
				; ここでBは入力行の次の行になっている
CNTSC3:	LD	A,B		; Aに入力行の次の行を保存
	DEC	A		; 入力行を戻す
	LD	(CURY),A	; 入力行はそのまま？か？
	POP	DE		; MEMO: VRAMアドレスをDEに入れる
	POP	AF		; MEMO: 最終入力文字
	CP	0DH		; ちゃんと改行で入力したか
	JR	NZ,GETEND	; 中断の場合はここで終わり
	CALL	VADRBC		; 次の行の先頭VRAMアドレスを算出
	CALL	GETWID		; 横幅拾う
	LD	A,(NULCHR)	; 空白文字をAに入れる
	JR	NC,GTED80	; 80文字モードならGTED80に飛ぶ
GTED40:	PUSH	HL
	OR	A
	SBC	HL,DE
	POP	HL
	JR	Z,GTED41
	DEC	HL
	DEC	HL
	CP	(HL)
	JR	Z,GTED40
	INC	HL
	INC	HL
GTED41:	OR	A
	SBC	HL,DE
	SRL	H
	RR	L
	CALL	GETNUM
	EX	DE,HL
	POP	DE
	PUSH	DE
GET40L:	DEC	C
	JR	Z,GETEXT
	LD	A,(HL)
	INC	HL
	INC	HL
	LD	(DE),A
	INC	DE
	JR	GET40L
	;
GTED80:	PUSH	HL		; 入力行の次の行の先頭のVRAMアドレスを保存
	OR	A		; Aには40か80が入っている(この場合は80のはず)
	SBC	HL,DE
	POP	HL
	JR	Z,GTED81
	DEC	HL
	CP	(HL)
	JR	Z,GTED80
	INC	HL
GTED81:	OR	A
	SBC	HL,DE
	CALL	GETNUM
	EX	DE,HL
	POP	DE
	PUSH	DE
	DEC	C
	JR	Z,GETEXT
	LD	B,0
	LDIR
	;
GETEXT:
	LD	A,0DH
	;
GETEND:	PUSH	AF
	LD	A,0DH
	CALL	PRTCHR
	POP	AF
	POP	DE
	POP	BC
	POP	HL
	CP	0DH
	RET	Z
	SCF
	RET
;
GETNUM:	LD	A,H
	OR	A
	LD	C,0
	RET	NZ
	LD	C,L
	INC	C
	RET
#ELSE
GETSCN:
	PUSH	AF		; 最終入力文字を保存
	LD	A,(CURY)	; A=入力行
	LD	B,A
	XOR	A
	LD	C,A
	CALL	VADRBC
	; HL = 転送元アドレス
	; DEにもコピー
	LD	E,L
	LD	D,H
	POP	AF				; MEMO: 最終入力文字
	CP	0DH
	JR	NZ,GETEND
	;CALL	GETWID

	; 行末から空白を削除して入力文字数を得る準備(WIDT40でも80でも)
	LD	C,80
	LD	B,0
	ADD	HL,BC
	DEC	HL

	CALL	GETWID				; 横幅を(フラグで)得る
	LD	A,(NULCHR)
	JR	NC,GTED80
	DEC	HL
GTED40:
	PUSH	HL
	OR	A
	SBC	HL,DE
	POP	HL
	JR	Z,GTED41
	DEC	HL
	DEC	HL
	CP	(HL)
	JR	Z,GTED40
	INC	HL
	INC	HL
GTED41:	OR	A
	SBC	HL,DE
	SRL	H
	RR	L
	CALL	GETNUM
	EX	DE,HL
	POP	DE
	PUSH	DE
GET40L:	DEC	C
	JR	Z,GETEXT
	LD	A,(HL)
	INC	HL
	INC	HL
	LD	(DE),A
	INC	DE
	JR	GET40L
	;
GTED80:	PUSH	HL
	OR	A
	SBC	HL,DE
	POP	HL
	JR	Z,GTED81
	DEC	HL
	CP	(HL)
	JR	Z,GTED80
	INC	HL
GTED81:	OR	A
	SBC	HL,DE
	CALL	GETNUM
	EX	DE,HL
	POP	DE
	PUSH	DE
	DEC	C
	JR	Z,GETEXT
	LD	B,0
	LDIR
	;
GETEXT:
	LD	A,0DH
	;
GETEND:	PUSH	AF
	LD	A,0DH
	CALL	PRTCHR
	POP	AF
	POP	DE
	POP	BC
	POP	HL
	CP	0DH
	RET	Z
	SCF
	RET
;
GETNUM:	LD	A,H
	OR	A
	LD	C,0
	RET	NZ
	LD	C,L
	INC	C
	RET
#ENDIF

;***********************
;
;	WIDTH SET
;
;***********************
WIDTHJ:
	CP	40+1
	LD	A,40
	JR	C,WID48
	LD	A,80
WID48:	LD	(AT_WIDTH),A
	CALL	CRTINIT
	CALL	CTRL_L
	RET

;**************************
;	STRING PRINT
;**************************
STRPRT:
	LD	A,(DE)
	OR	A
	RET	Z
	CALL	PRTCHR
	INC	DE
	JR	STRPRT

;*****************************
;	PRINT CHARACTER
;*****************************
PRTCHR:
	PUSH	AF
	PUSH	BC
	PUSH	DE
	PUSH	HL
	CP	20H
	JR	NC,NOCTRL
	CALL	CTRLSB
	JR	PRINTE
NOCTRL:
	CALL	VADDRS
	LD	(HL),A
	CALL	CSRRT
	LD	A,(CURX)
	OR	A
	JR	NZ,PRINTE
	LD	HL,CNTTBL
	LD	A,(CURY)
	DEC	A
	LD	C,A
	LD	B,0
	ADD	HL,BC
#IF NAME_SPACE_DEFAULT.COLOR_MODE==0
	; カラーモードにおいて行の接続は考慮しない(実装が複雑になるので……そのうちどうにかしたいが……)
	LD	(HL),1
#ENDIF
PRINTE:	POP	HL
	POP	DE
	POP	BC
	POP	AF
	RET
;
;
CTRLSB:
	ADD	A,A
	LD	HL,CTRL_TBL
	LD	E,A
	LD	D,0
	ADD	HL,DE
	LD	A,(HL)
	INC	HL
	LD	H,(HL)
	LD	L,A
	JP	(HL)

;*********************
;
;	PRINTER
;
;******$**************
LPTOUT:
	CP	0DH
	JR	NZ,LPTOT1
	CALL	LPTOT1
	RET	C
	LD	A,0AH
LPTOT1:
	PUSH	AF
LPTLOP:
	ONSTOP	LPTEXT
;+             IN      A,(9)
;+             BIT     0,A
;+             JP      Z,LPTEXT
	IN	A,(040H)
	AND	1
	JR	NZ,LPTLOP
	POP	AF
	OUT	(010H),A
	LD	A,(PORT40)
	AND	0FEH
	OUT	(040H),A
	LD	A,(PORT40)
	OR	1
	OUT	(040H),A
	OR	A
	RET
	;
LPTEXT:
	POP	AF
	SCF
	RET

;----------------------------
;	CURSOR RIGHT
;----------------------------
CSRRT:
	PUSH	BC
	CALL	GETWID
	LD	B,A
	LD	A,(CURX)
	INC	A
	CP	B
	POP	BC
	LD	(CURX),A
	RET	C
	JP	CRLF

;----------------------------
;	CURSOR LEFT
;----------------------------
CSRLT:
	LD	A,(CURX)
	DEC	A
	LD	(CURX),A
	RET	P
	;
	CALL	GETWID
	DEC	A
	LD	(CURX),A
	;			<< JP	CSRUP >>

;----------------------------
;	CURSOR UP
;----------------------------
CSRUP:
	LD	A,(CURY)
	OR	A
	RET	Z
	DEC	A
	LD	(CURY),A
	RET

;----------------------------
;	CURSOR DOWN
;----------------------------
CSRDW:
	LD	A,(CURY)
	CP	24
	JP	NC,SCROLL
	INC	A
	LD	(CURY),A
	RET

;----------------------------
;	CR AND LF
;----------------------------
CRLF:
	XOR	A
	LD	(CURX),A
	JP	CSRDW
;----------------------------
;	SCROLL UP
;----------------------------
SCROLL:
	PUSH	AF
	PUSH	BC
	PUSH	DE
	PUSH	HL
#IF NAME_SPACE_DEFAULT.COLOR_MODE==0
	LD	HL,VRMTOP+80
	LD	DE,VRMTOP
	LD	BC,VRSIZE-80
	LDIR
	LD	A,(NULCHR)
	LD	HL,VRMTOP+2000-80
	LD	DE,VRMTOP+2000-80+1
#ELSE
	LD	HL,VRMTOP+120		; 80 / Color
	LD	DE,VRMTOP
	LD	BC,VRSIZE-120		; 80 / Color
	LDIR
	LD	A,(NULCHR)
	LD	HL,VRMTOP+3000-120	; 2000-80 / Color
	LD	DE,VRMTOP+3000-120+1	; 2000-80 / Color
#ENDIF
	LD	BC,79
	LD	(HL),A
	LDIR
	LD	DE,CNTTBL
	LD	HL,CNTTBL+1
	LD	BC,24
	LDIR
	XOR	A
	LD	(DE),A
	POP	HL
	POP	DE
	POP	BC
	POP	AF
	RET

;======================================
;
;	VRAM-ADDRESS GET
;	(IN  : BC = CURSOR X,Y)
;	EXIT : HL = VRAM-ADDRESS
;
;======================================
VADRBC:
	PUSH	BC
	JR	VADRS1
;
VADDRS:
	PUSH	BC
	LD	BC,(AT_DSPXY)
	;
VADRS1:	PUSH	AF
	PUSH	DE
	LD	L,B
	LD	H,0

#IF NAME_SPACE_DEFAULT.COLOR_MODE==0
	; 85
	; HL = HL * 80
	ADD	HL,HL
	ADD	HL,HL
	ADD	HL,HL
	ADD	HL,HL
	LD	E,L
	LD	D,H
	ADD	HL,HL
	ADD	HL,HL
	ADD	HL,DE
#ELSE
;	; 104 /  HL = HL * 120(64+32+16+8)	Color
;	ADD	HL,HL	; HL=HL*128
;	ADD	HL,HL
;	ADD	HL,HL
;	LD	E,L
;	LD	D,H
;	ADD	HL,HL
;	ADD	HL,HL
;	ADD	HL,HL
;	ADD	HL,HL
;	CCF
;	SBC	HL,DE
;
;	; なんか1足さないと駄目??
;	INC	HL

	; 56 / テーブル使う版の120倍
	LD DE,VRAMYTBL
	ADD HL,HL
	ADD HL,DE
	LD A,(HL)
	INC HL
	LD H,(HL)
	LD L,A
#ENDIF

	CALL	GETWID
	LD	A,C
	JR	NC,ADDRS
	ADD	A,A
	;
ADDRS:	LD	E,A
	LD	D,0
	ADD	HL,DE
	LD	DE,VRMTOP
	ADD	HL,DE
	POP	DE
	POP	AF
	POP	BC
	RET

VRAMYTBL:
	DW	 0*120,  1*120,  2*120,  3*120,  4*120,  5*120,  6*120, 7*120
	DW	 8*120,  9*120, 10*120, 11*120, 12*120, 13*120, 14*120, 15*120
	DW	16*120, 17*120, 18*120, 19*120, 20*120, 21*120, 22*120, 23*120
	DW	24*120, 25*120

;--------------------------------
;	CURSOR SET / RESET
;--------------------------------
CSRON:
	LD	A,081H
	JR	CSRSET
;
CSROFF:
	LD	A,080H
	;
CSRSET:	OUT	(051H),A
	PUSH	HL
	LD	HL,CURX
	CALL	GETWID
	LD	A,(HL)
	JR	NC,CSRS80
	ADD	A,A
CSRS80:	OUT	(050H),A
	INC	HL
	LD	A,(HL)
	OUT	(050H),A
	POP	HL
	RET

;***************************************
;
;       INITIALIZE CRTC AND DMAC
;
;***************************************
INICRT:
	XOR	A
	OUT	(I_PORT),A
	CALL	INIT
	LD	A,80
	CALL	AT_WIDCH
	RET
;
;
CRTINIT:
	DI
	XOR	A
	OUT	(CRTCOM),A
	LD	A,0A0H
	OUT	(DMAMOD),A
	LD	A,LOW VRMTOP
	OUT	(DMAADR),A
	LD	A,HIGH VRMTOP
	OUT	(DMAADR),A
	LD	A,LOW (VRSIZE-1+8000H)
	OUT	(DMACNT),A
	LD	A,HIGH (VRSIZE-1+8000H)
	OUT	(DMACNT),A
	LD	HL,NORMAL
	LD	B,5
	IN	A,(040H)
	AND	00000010B
	JR	NZ,OUTLOP
	LD	HL,HI_RES
OUTLOP:
	LD	C,CRTDAT
	OTIR
	LD	A,043H
	OUT	(CRTCOM),A
	LD	A,0E4H
	OUT	(DMAMOD),A
	LD	A,020H
	OUT	(CRTCOM),A
	CALL	GETWID
	LD	A,(PORT30)
	RES	0,A
	JR	C,CRT40
	SET	0,A
	;
CRT40:	CALL	SET30H

	EI
	RET

;---------------------
;	INIT
;---------------------
INIT:
PATCH1::
	#IF	NAME_SPACE_DEFAULT.PC88==1
	LD	A,00100010B
	#ELSE
	LD	A,00110000B
	#ENDIF
	OUT	(031H),A
	LD	(PORT31),A
#IF NAME_SPACE_DEFAULT.COLOR_MODE==0
	LD	A,00000011B	;;00100011B
#ELSE
	LD	A,00000001B		; <-Color  Mono -> 00000011B	;;00100011B
#ENDIF
	OUT	(030H),A
	LD	(PORT30),A
	LD	A,00000001B
	OUT	(040H),A
	LD	(PORT40),A
	RET

;----------------------------
;	CLEAR SCREEN
;
;	IN : H = Y TOP
;	     L = X TOP
;	     A = Y END
;----------------------------
CLS:
	CALL	CNTCLR
	PUSH	AF
	LD	C,L
	LD	B,H
	CALL	VADRBC
	EX	DE,HL
	POP	BC
	INC	B
	LD	C,0		; X-END
	CALL	VADRBC
	DEC	HL
	OR	A
	SBC	HL,DE
	LD	C,L
	LD	B,H		; BC = SIZE
	LD	L,E
	LD	H,D
	INC	DE
	LD	A,(NULCHR)
	LD	(HL),A
	LDIR

#IF NAME_SPACE_DEFAULT.COLOR_MODE!=0
	; アトリビュート全行初期化
	LD	B,24
.colloop
	PUSH	BC
	LD	C,80H
	CALL	SetTextAtr
	POP	BC
	DEC	B
	JP	P,.colloop
#ENDIF


	RET
;
;
CNTCLR:
	PUSH	AF
	PUSH	BC
	PUSH	DE
	SUB	H
	INC	A
	EX	DE,HL
	LD	HL,CNTTBL
	LD	C,D
	LD	B,0
	ADD	HL,BC
	LD	B,A
CNTCL1:	LD	(HL),0
	INC	HL
	DJNZ	CNTCL1
	EX	DE,HL
	POP	DE
	POP	BC
	POP	AF
	RET

;
;
;
NORET:
	RET

;
; REGIST PRINT ROUTINE
;
RSTJOB:
	DI
.loop
	JR	.loop

;	DI
;	LD	(SAVESP),SP
;	PUSH	IY
;	PUSH	IX
;	PUSH	HL
;	PUSH	DE
;	PUSH	BC
;	PUSH	AF
;	PUSH	AF
;	CALL	MAIN.MPRNT
;	DB	0DH
;	DB	"SZ.H.PNC  AF   BC   DE   HL   IX   IY"
;	DB	0DH,0
;	POP	AF
;
;	CALL	PRFLAG
;
;	LD	B,6
;LOOP8:	POP	HL
;	CALL	MAIN.PRTHL
;	CALL	MAIN.PRNTS
;	DJNZ	LOOP8
;	EXX
;	EX	AF,AF'
;	PUSH	HL
;	PUSH	DE
;	PUSH	BC
;	PUSH	AF
;
;	PUSH	AF
;	CALL	MAIN.MPRNT
;	DB	0DH
;	DB	"SZ.H.PNC  AF\'  BC\'  DE\'  HL\'  SP   PC"
;	DB	0DH,0
;	POP	AF
;
;	CALL	PRFLAG
;	LD	B,4
;LOOP81:	POP	HL
;	CALL	MAIN.PRTHL
;	CALL	MAIN.PRNTS
;	DJNZ	LOOP81
;	LD	HL,(SAVESP)
;	CALL	MAIN.PRTHL
;	CALL	MAIN.PRNTS
;	POP	HL
;	DEC	HL
;	CALL	MAIN.PRTHL
;	CALL	MAIN.LTNL
;	JP	HOT
;
;; PRINT FLAG
;PRFLAG:	PUSH	AF
;	PUSH	BC
;	PUSH	HL
;	PUSH	AF
;	POP	HL	;LD L,F
;	LD	B,8
;BIN1:	RL	L
;	LD	A,'-'
;	JR	NC,BIN2
;	LD	A,'*'
;BIN2:	CALL	PRTCHR
;	DJNZ	BIN1
;	CALL	MAIN.PRNTS
;	CALL	MAIN.PRNTS
;	POP	HL
;	POP	BC
;	POP	AF
;	RET

SAVESP:
;	DS	2
	; Oh!MZのダンプリストがこうなっているのでこうする
	DB	0CDH,0BEH

;
;
CTRL_P:				;HARD COPY OF TEXT SCREEN
	LD	HL,VRMTOP
	LD	C,25
COPY1:	LD	A,(AT_WIDTH)
	LD	B,A
COPY2:	LD	A,(HL)
	CALL	LPTOUT
	RET	C
	INC	HL
	DJNZ	COPY2

	LD	A,0DH
	CALL	LPTOUT
	RET	C

	DEC	C
	JR	NZ,COPY1
	RET

CTRL_B:
	CALL	SUB2
	JR	Z,CTRLB1
	CALL	SLEFT
	RET	C
	JR	CTRL_B
CTRLB1:	CALL	SLEFT
	RET	C
	CALL	SUB2
	JR	Z,CTRLB1
CTRLB2:	CALL	SLEFT
	RET	C
	CALL	SUB2
	JR	NZ,CTRLB2
	CALL	CSRRT
	RET
SUB2:	CALL	VADDRS
	LD	A,(HL)
	AND	A
	RET	Z
	CP	' '
	RET

;	DS	20H	;RESERVED
	; Oh!MZにあわせるためダミーデータを入れる
	DB	$0D,$C2,$CC,$2E,$0E,$03,$D1,$1a
	DB	$B7,$CA,$E1,$2E,$77,$13,$23,$0D
	DB	$C2,$D5,$2E,$22,$FC,$2E,$C9,$45
	DB	$49,$4F,$55,$41,$00,$00,$00,$00

SLEFT:	CALL	CSRLT
	LD	HL,CURX
	LD	A,(HL)
	AND	A
	RET	NZ
	INC	HL
	LD	A,(HL)
	AND	A
	RET	NZ
	SCF		;LOCATION = HOME POSITIOM
	RET

CTRL_E:			; ONE LINE CLEAR
	LD	A,(CURY)
	LD	C,A
	LD	B,0
	LD	HL,CNTTBL
	ADD	HL,BC
CTRLE1:	INC	C
	LD	A,(HL)
	LD	(HL),0		;;
	INC	HL
	OR	A
	JR	NZ,CTRLE1
	LD	B,C
	LD	C,0
	CALL	VADRBC
	PUSH	HL
	CALL	VADDRS		; NOW LOCATION ADDRESS
	EX	DE,HL		;  = DE reg.
	POP	HL		; HL -> END OF LINE
	PUSH	DE
	INC	DE
	OR	A
	SBC	HL,DE
	LD	C,L
	LD	B,H
	POP	HL
	LD	A,(NULCHR)
	LD	(HL),A
	LD	A,C
	OR	B
	RET	Z
	LDIR
	RET
;
CTRL_G:				; BELL
	DI
	LD	A,(PORT40)
	OR	00100000B
	OUT	(040H),A
	PUSH	AF
	LD	HL,04000H
BELLW:	DEC	HL
	LD	A,L
	OR	H
	JR	NZ,BELLW
	POP	AF
	AND	11011111B
	OUT	(040H),A
	EI
	RET
;
CTRL_H:
#IF NAME_SPACE_DEFAULT.COLOR_MODE==0
	LD	A,(CURY)
	LD	C,A
	LD	B,0
	LD	HL,CNTTBL
	ADD	HL,BC
	PUSH	HL
CTRLH1:	INC	C
	LD	A,(HL)
	INC	HL
	OR	A
	JR	NZ,CTRLH1
	LD	B,C
	LD	C,0
	CALL	VADRBC		; MEMO: 行がつながっている最終行の先頭？アドレス
	PUSH	HL
	CALL	VADDRS		; MEMO: 削除する場所のアドレス
	EX	DE,HL
	POP	HL				; HL = 最終行、DE = 削除先頭
	POP	BC
	DEC	HL
	LD	A,(CURX)
	OR	A
	JR	NZ,BACKSP
	DEC	BC
	LD	A,(BC)
	OR	A
	JR	Z,DELETE
	;
BACKSP:	LD	C,E
	LD	B,D
	DEC	BC
	CALL	GETWID
	JR	NC,BKSP1
	DEC	BC
	DEC	HL
BKSP1:	PUSH	HL
	PUSH	BC
	OR	A
	SBC	HL,BC
	LD	C,L
	LD	B,H
	POP	HL
	EX	DE,HL
	LDIR
	CALL	CSRLT
	JR	CTRLH2
	;
DELETE:	LD	C,E
	LD	B,D
	INC	BC
	CALL	GETWID
	JR	NC,DELT1
	INC	BC
	DEC	HL
DELT1:	PUSH	HL
	PUSH	BC
	OR	A
	SBC	HL,DE
	LD	C,L
	LD	B,H
	POP	HL
	LDIR
	;
CTRLH2:	POP	HL
	LD	A,(NULCHR)
	LD	(HL),A
	RET
#ELSE
	; 色つきの1文字削除
	; 大変面倒なので行の接続は考慮しない(手抜き)
	LD	A,(CURX)
	OR	A
	JR	Z,.enddelete		; 行頭での一文字削除は無効
	DEC	A
	LD	(CURX),A
	LD	C,A
	LD	A,(CURY)
	LD	B,A
	CALL	VADRBC
	LD	E,L
	LD	D,H
	INC	HL
	; DE=転送先、HL=転送元
	CALL	GETWID	; 40 or 80チェック(Cで40)
	LD	A,(CURX)
	JR	NC,.wid80
	RLCA		; A=A*2
	INC	HL
	INC	A
.wid80
	LD	C,A
	LD	A,79
	SUB	C
	LD	C,A
	LD	B,0
	; BC->コピーするサイズ
	LDIR
	; この時点でHLは削除したい文字のアドレス
	LD	A,(NULCHR)
	LD	(DE),A
.enddelete
	RET

#ENDIF
CTRL_I:				;H-TARB
	LD	A,(NULCHR)
	CALL	PRTCHR
	LD	A,(CURX)
	AND	7
	JR	NZ,CTRL_I
	RET
;	LD	A,(CURX)
;TABLOP:	INC	A
;	LD	B,A
;	AND	7
;	LD	A,B
;	JR	NZ,TABLOP
;	CALL	GETWID
;	DEC	A
;	CP	B
;	JR	C,TABPAS
;	LD	A,B
;TABPAS:	LD	(CURX),A
;	RET
;


;; 1. (CURX),(CURY)の位置から(末尾-1)(CURY)までを1つ左にズラす
;; 2.もし(CURY)位置のCNTTBLが0以外ならば、次の行の行頭文字を(末尾)(CURY)にコピーする
;; 3. 続いて(0),(CURY+N)(Nは1から始まる)から(末尾-1),(CURY+N)までを1つ左にズラす(1と同じ処理)
;; 4. 以降Nを増やしながらCNTTBLが0以外の行を処理する(最終行は必ず0なはずなので25行オーバーは考えない)
;
;	アセンブラめんどくせえ。CURXとCURYをWORKX、WORKYに移してから処理するか？
;	スタック使えばどうにかなる気もするが……
;
;	LD	A,(CURX)
;	OR	A
;	JR	Z,.gyoutou		; 行頭の場合は次の行の行頭を前の行の末尾にコピーする
;	; 行頭じゃない場合はカーソル位置の左にカーソル位置のデータをコピーする
;	LD	C,A
;	LD	A,(CURY)
;	LD	B,A
;	CALL	VADRBC
;	LD	E,L
;	LD	D,H
;	DEC	DE
;	; HL->コピー元
;	; DE->コピー先
;	LD	A,(CURX)
;	LD	C,A
;	LD	A,(AT_WIDTH)
;	SUB	C
;	LD	B,0
;	; BC->コピーするサイズ
;	LDIR
;
;	; その行の末尾……か？
;	PUSH	DE
;
;	LD	A,(CURY)
;	LD	C,A
;	LD	B,0
;	LD	HL,CNTTBL
;	ADD	HL,BC
;	; 次の行につながっているか
;	LD	A,(HL)
;	OR	A
;	JR	Z,.notconnect
;	; 次の行につながっている場合は次の行の行頭を前の行の末尾にコピーする
;	LD	A,(CURY)
;	INC	A
;	LD	B,A
;	LD	C,0
;	CALL	VADRBC
;	LD	A,(HL)
;	; このHL(さっきPUSHしたDE)がLDIRで動いた末尾文字のアドレスのはず
;	POP	HL
;	LD	(HL),A
;
;.notconnect
;	POP	DE
;
;
;
;
;
;
;DELCHAR:
;	LD	A,(CURY)
;	LD	B,A
;	LD	A,(CURX)
;	LD	C,A
;
;	LD	A,C
;	OR	A
;	JR	Z,.gyoutou		; 行頭なのでこの行を1つ左にズラして行頭文字を一つ上の行の末尾にコピーする
;	CALL	GETWID
;	INC	C
;	CP	C
;	JR	Z,.gyomatsu		; 行末なので次の行を1つ左にズラして行頭文字をこの行の末尾にコピーする
;
;	; 行の途中なので普通にアドレス計算してズラす
;	CALL	DELCHAR1
;
;.gyoutouk
;	; 0行目以外の場合は前の行に戻る
;	LD	A,B
;	OR	A
;	JR	Z,.done
;	INC	C
;	CALL	DELCHAR1
;	; Aにツブされた文字が入っている
;	; 前の行に戻る
;	PUSH	AF
;	DEC	B
;	CALL	GETWID
;	DEC	A
;	LD	C,A
;	; 前行末尾のアドレスを得る
;	CALL	VADRBC
;	POP	AF
;	; そこに次行の先頭文字を入れてやる
;	LD	(HL),A
;
;
;
;
;	LD	E,B
;	LD	D,0
;	LD	HL,CNTTBL
;	ADD	HL,DE
;	LD	A,(HL)
;	OR	A
;	JR	.notconnect1
;
;.done
;	RET
;
;
;
;
;
;; B=Y
;; C=X
;; の位置から末尾までを1つ左にずらす(だけ)
;DELCHAR1:
;	PUSH	BC
;	CALL	VADRBC
;	LD	E,L
;	LD	D,H
;	DEC	HL
;	LD	A,(HL)
;	PUSH	AF			; 消される文字
;	EX	DE,HL
;	; HL->コピー元
;	; DE->コピー先
;	LD	A,(AT_WIDTH)	; GETWIDにしないと駄目かも？
;	SUB	C				; C=X
;	LD	B,0
;	; BC->コピーするサイズ
;	LDIR				; 左にズラす
;	POP	AF
;	; A = 消えた文字
;
;	POP	BC
;	RET
;
;	
;
;
;.notconnect
;	; 終わり
;
;WORKX:
;	DS	1
;WORKY:
;	DS	1




	; 4E8H から動かしてはいけない気もするがとりあえず気にしない(ogino)
;	ORG	04E8H
HOME:
	LD	HL,0
	LD	(AT_DSPXY),HL
	RET

;
CTRL_L:
	CALL	HOME
	JP	CTRL_Z
;
CTRL_N:
	CALL	SUB2
	JR	Z,CTRLN1
	CALL	SRIGHT
	RET	C
	JR	CTRL_N

CTRLN1:	CALL	SRIGHT
	RET	C
	CALL	SUB2
	JR	Z,CTRLN1
	RET

SRIGHT:	LD	HL,CURY
	LD	A,(HL)
	CP	24
	JR	C,SRIT1
	DEC	HL
	LD	A,(AT_WIDTH)
	DEC	A
	CP	(HL)
	JR	NZ,SRIT1
	LD	(HL),0
	SCF
	RET

SRIT1:	CALL	CSRRT
	AND	A	; RCF
	RET

;
CTRL_R:				;ONECHARACTER
	LD	A,(CURY)
	LD	C,A
	LD	B,0
	LD	HL,CNTTBL
	ADD	HL,BC
CTRLR1:	INC	C
	LD	A,(HL)
	INC	HL
	OR	A
	JR	NZ,CTRLR1
	;			; C = NEXT Y LOC

	LD	B,C
	LD	C,0
	PUSH	BC
	;
	DEC	B
	CALL	GETWID
	DEC	A
	LD	C,A
	CALL	VADRBC
	PUSH	HL
	CALL	VADDRS		; NOW LOCATION ADDRESS
	EX	DE,HL		;  = DE reg.
	POP	HL		; HL -> END OF LINE
	LD	A,(NULCHR)
	CP	(HL)
	POP     BC
	JR	Z,CTRLR3	; NO SCROLL
	LD	A,B
	CP	25
	JR	C,DWSCRL	; DOWN SCROLL
	CALL	SCROLL
	CALL	CSRUP
#IF NAME_SPACE_DEFAULT.COLOR_MODE==0
	; カラーモードの場合は行の接続は考慮しない
	LD	A,1
	LD	(CNTTBL+23),A
#ENDIF
	DEC	B
	CALL	VADDRS
	EX	DE,HL
	;
CTRLR2:	CALL	VADRBC
CTRLR3:	PUSH	DE
	PUSH	HL
	OR	A
	SBC	HL,DE
	LD	C,L
	LD	B,H
	POP	HL
	LD	E,L
	LD	D,H
	DEC	HL
	CALL	GETWID
	JR	NC,CTRLR4
	DEC	HL
CTRLR4:	LD	A,C
	OR	B
	JR	Z,CTRLR5
	LDDR
CTRLR5:	POP	DE
	LD	A,(NULCHR)
	LD	(DE),A
	RET
;
DWSCRL:
	PUSH	DE
	PUSH	BC
	CALL	VADRBC
	EX	DE,HL
	LD	HL,0FFFFH - 79
	OR	A
	SBC	HL,DE
	LD	A,L
	OR	H
	JR	Z,DWSCR1
	;
	LD	C,L
	LD	B,H
	LD	DE,0FFFFH
	LD	HL,0FFFFH - 80
	LDDR
	;
	POP	BC
	PUSH	BC
	LD	HL,CNTTBL + 22
	LD	DE,CNTTBL + 23
	LD	A,24
	SUB	B
	LD	C,A
	LD	B,0
	LDDR
	;
DWSCR1:	POP	BC
	PUSH	BC
	CALL	VADRBC
	PUSH	HL
	LD	HL,CNTTBL
	LD	C,B
	LD	B,0
	ADD	HL,BC
	DEC	HL
#IF NAME_SPACE_DEFAULT.COLOR_MODE==0
	; カラーモードの場合は行の接続は考慮しない
	LD	(HL),1
#ENDIF
	POP	HL
	LD	A,(NULCHR)
	LD	B,80
DWSCR2:	LD	(HL),A
	INC	HL
	DJNZ	DWSCR2
	;
	POP	BC
	POP	DE
	JR	CTRLR2
;
CTRL_V:				; MOVE COUSOR LAST OF LINE
	LD	A,(CURY)
	LD	C,A
	LD	B,0
	LD	HL,CNTTBL
	ADD	HL,BC
CTRLV1:	INC	C
	LD	A,(HL)
	INC	HL
	OR	A
	JR	NZ,CTRLV1

	DEC	C
	LD	A,C
	LD	(CURY),A

	LD	A,(AT_WIDTH)
	DEC	A
	LD	(CURX),A
;
CTRLV2:	CALL	SUB2
	JR	NZ,CTRLV3
	CALL	CSRLT
	JR	CTRLV2

CTRLV3:	CALL	CSRRT
	RET

;
CTRL_Z:
	LD	HL,(AT_DSPXY)
	LD	A,24
	CALL	CLS
	RET

;-------------------------------
;	PARAMETER AREA
;-------------------------------
CRTDAT	EQU	050H
CRTCOM	EQU	051H
DMAADR	EQU	064H
DMACNT	EQU	065H
DMAMOD	EQU	068H
I_PORT	EQU	0E4H
#IF NAME_SPACE_DEFAULT.COLOR_MODE==0
VRSIZE	EQU	2000
VRMTOP	EQU	0-VRSIZE
#ELSE
; カラ=の場合はデフォルト位置にあわせる
VRSIZE	EQU	3000
VRMTOP	EQU	0F300H
#ENDIF

;-----------------------
;	DATA AREA
;-----------------------
NORMAL:	DB	11001110B
	DB	11011000B
	DB	01100111B
	DB	11011110B
#IF NAME_SPACE_DEFAULT.COLOR_MODE==0
	DB	00100000B
#ELSE
	DB	01010011B	; attribute 20moji & color
#ENDIF
	;
HI_RES:	DB	11001110B
	DB	10011000B
	DB	01101111B
	DB	01011000B
#IF NAME_SPACE_DEFAULT.COLOR_MODE==0
	DB	00100000B
#ELSE
	DB	01010011B	; attribute 20moji & color
#ENDIF
;
CTRL_TBL:
	DW	NORET		; AT_
	DW	NORET		; A
	DW	CTRL_B		; B
	DW	NORET		; C = STOP KEY
	DW	NORET		; D
	DW	CTRL_E		; E
	DW	NORET		; F
	DW	CTRL_G		; G
	DW	CTRL_H		; H
	DW	CTRL_I		; I
	DW	CSRDW		; J
	DW	HOME		; K
	DW	CTRL_L		; L
	DW	CRLF		; M
	DW	CTRL_N		; N
	DW	NORET		; O
	DW	CTRL_P_PATCH	; P
	DW	NORET		; Q
	DW	CTRL_R		; R
	DW	NORET		; S
	DW	NORET		; T
	DW	NORET		; U
	DW	CTRL_V		; V
	DW	NORET		; W
	DW	NORET		; X
	DW	NORET		; Y
	DW	CTRL_Z		; Z
	DW	NORET		; <ESC>
	DW	CSRRT
	DW	CSRLT
	DW	CSRUP
	DW	CSRDW
;
;

	DB	0
CNTTBL:	DS	25,0


;************************************
;	KEYBOARD INPUT ROUTINE
;************************************
STPCHK:
	CALL	INKEY0
	CP	1BH	;;3
	RET


;
;
INKEYJ:
	OR	A
	JR	Z,INKEY0
	DEC	A
	JR	Z,INKEY1
;
INKEY:
	CALL	KEYIN
	RET
;
INKEY0:
	CALL	KYSCAN
	RET
;
INKEY1:
	CALL	CSRON
INKY11:	CALL	KEYIN
	JR	C,INKY11
	PUSH	AF
	CALL	CSROFF
	POP	AF
	RET
;
KEYIN:	PUSH	HL
	PUSH	BC
	LD	A,(REPTST)
	LD	B,A
KEYIN1:	PUSH	BC
	CALL	KYSCAN
	POP	BC
	JP	C,KEYIN2
	JR	NZ,KEYIN2
	DJNZ	KEYIN1
	PUSH	AF
	LD	A,006H
	LD	(REPTST),A
	POP	AF
KEYIN2:	POP	BC
	POP     HL
	RET
;
KYSCAN:	PUSH	HL
	PUSH	DE
	PUSH	BC
	CALL	GFKEY		;;
	JR	NC,KSCANE1	;;
	CALL	SCAP		;;
	CALL    CHKSFT
	LD	HL,SHFTST
	CP	(HL)
	LD	(HL),A
	JR	Z,KSCAN4
	CP	003H
	JR	Z,KSCAN1
	AND	A
	JR	NZ,KSCAN2
KSCAN1:	XOR	A
	JR	KSCAN4
;
KSCAN2:	CP	001H
	JR	Z,KSCAN3
	CP	004H
	JR	NZ,KSCAN4
KSCAN3:	LD	A,005H
KSCAN4:	CALL	PTSCAN
	JR	C,KSCAN6
	JR	NZ,KSCAN5
	LD	A,(KEYCOD)
	JR	KSCANE
;
KSCAN5:	LD	H,A
	LD	A,040H
	LD	(REPTST),A
	LD	A,H
	CALL	CNVA88
	JR	C,KSCAN6
	LD	(KEYCOD),A
	AND	A
	JR	NZ,KSCANE
	INC	A
	LD	A,000H
	JR	KSCANE
;
KSCAN6:	LD	A,000H
	LD	(KEYCOD),A
	LD	(ONFUNC),A
KSCANE:	CALL	CAPS
KSCANE1:POP	BC
	POP     DE
	POP     HL
	RET
;
CHKSFT:	IN	A,(008H)
	CPL
	AND	0f0H
	RET	Z
	CALL	CKSFT1
	RET	C
	IN	A,(008H)
	CPL
	AND	0F0H
	CP	B
	JR	NZ,CHKSFT
	LD	A,C
	RET
;
CKSFT1:	LD	B,A
	LD	C,000H
	LD	HL,SFTDAT
CKSFT2:	INC	C
	CP	(HL)
	JR	Z,KLOOP
	INC	HL
	LD	D,A
	LD	A,C
	CP	005H
	LD	A,D
	JR	C,CKSFT2
	XOR	B
	DEC	a
	SCF
	RET
;
SFTDAT:	DB	40H,80H,20H,60H,10H

;
KLOOP:	PUSH	BC
	LD	BC,00500H
KLOOP1:	DEC	C
	JR	NZ,KLOOP1
	DJNZ	KLOOP1
	POP	BC
	RET
;
PTSCAN:	LD	C,00AH
	LD	HL,SCNTB2+009H
	LD	A,C
	LD	(KSCNPT),A
	CALL	SCAN3
	JR	NZ,PSCAN3
	LD	A,B
	CP	001H
	SBC	A,A
	JR	NZ,PSCAN0
	LD	(KSCNPT),A
PSCAN0:	LD	HL,SCANTB+10
	JR	PSCAN2
PSCAN1:	CALL	SCAN
	JR	NZ,PSCAN3
	LD	A,B
	CP	001H
	SBC	A,A
	JR	NZ,PSCAN2
	LD	(KSCNPT),A
PSCAN2:	DEC	HL
	DEC	C
	JR	Z,PSCAN1
	JP	P,PSCAN1
	LD	A,(KSCNPT)
	AND	A
	RET	Z
	SCF
	RET
;
PSCAN3:	LD	B,A
	LD	E,000H
	LD	A,001H
PSCAN4:	PUSH	AF
	AND	B
	JR	NZ,PSCAN5
	POP	AF
	INC	E
	RLCA
	JR	PSCAN4
PSCAN5:	POP	AF
	OR	(HL)
	LD	(HL),A
	LD	A,C
	RLCA
	RLCA
	RLCA
	OR	E
	JR	NZ,PSCAN6
	INC	E
PSCAN6:	RET
;
SCAN:	LD	A,008H
	LD	D,00FH
	CP	C
	JR	Z,SCAN1
	LD	D,0FFH
SCAN1:	IN	A,(C)
	CPL
	AND	D
	LD	B,A
	JR	Z,SCAN2
	CALL	KLOOP
	IN	A,(C)
	CPL
	AND	D
	CP	B
	JR	NZ,SCAN1
SCAN2:	CPL
	AND	(HL)
	XOR	(HL)
	LD	(HL),A
	LD	A,B
	XOR	(HL)
	RET
;
SCAN3:	LD	A,00AH
	CP	C
	JR	NZ,SCAN
	IN	A,(00AH)
	AND	080H
	LD	(SCNTB2+00AH),A
	LD	D,07FH
	JR	SCAN1
;
CNVA88:	CP	050H
	JR	C,CNV881
	LD	HL,P0ADAT-050H
	LD	C,A
	LD	B,000H
	ADD	HL,BC
	LD	A,(HL)
	AND	A
	SCF
	RET	Z
	AND	A
	RET
;
P0ADAT:	DB	009H,01FH,01DH,000H
	DB	000H,02DH,02FH,000H
;
CNV881:	CALL	CNVASC
	RET	C
	CP	041H
	JR	C,CNV882
	CP	05BH
	JR	C,CNV883
	CP	061H
	JR	C,CNV882
	CP	07BH
	JR	C,CNV883
CNV882:	AND	A
	RET
;
CNV883:	PUSH	AF
	LD	A,(SCNTB2+00AH)
	AND	A
	JR	NZ,CNV884
	POP	AF
	XOR	020H
	RET
;
CNV884:	POP	AF
	AND	A
	RET
;
CNVASC:	CP	050H
	JR	NC,CNVAS7
	CP	04FH
	JR	Z,CNVAS3
	CP	04EH
	JR	Z,CNVAS4
	CP	048H
	JR	Z,CNVAS5
	LD	HL,KMXDAT
	LD	DE,00013H
	LD	C,000H
CNVAS1:	CP	(HL)
	JR	C,CNVAS2
	LD	C,(HL)
	ADD	HL,DE
	JR	CNVAS1
;
CNVAS2:	PUSH	AF
	PUSH	HL
	LD	HL,SHFTST
	LD	A,(HL)
	RLCA
	JP	C,CNVAS6
	ADD	A,(HL)
	POP	HL
	LD	E,A
	INC	HL
	ADD	HL,DE
	LD	E,(HL)
	INC	HL
	LD	D,(HL)
	INC	HL
	LD	B,(HL)
	POP	AF
	EX	DE,HL
	JP	(HL)
;
CNVAS3:	LD	A,0		;;01BH ESC KEY
	RET
;
CNVAS4:	LD	A,020H
	RET
;
CNVAS5:	LD	A,01BH		;;003H STOP KEY
	RET
CNVAS6:	POP	HL
	POP	AF
CNVAS7:	XOR	A
	SCF
	RET
;
KMXDAT:	DB	00AH
	DW	CNVAS8
	DB	030H
	DW	CNVAS8
	DB	030H
	DW	CNVAS8
	DB	030H
	DW	CNVAS8
	DB	030H
	DW	CNVAS8
	DB	030H
	DW	CNVAS9
	DB	000H
	DB	010H
	DW	CNVAS9
	DB	004H
	DW	CNVAS9
	DB	004H
	DW	CNVAS9
	DB	004H
	Dw	CNVAS9
	DB	004H
	DW	CNVAS9
	DB	004H
	DW	CNVAS9
	DB	002H
	DB	02BH
	DW	CNVASA
	DB	060H
	DW	CNVASB
	DB	040H
	DW	CNVAS8
	DB	000H
	DW	CNVAS9
	DB	006H
	DW	CNVASD
	DB	006H
	DW	CNVAS9
	DB	008H
	DB	030H
	DW	CNVASF
	DB	05BH
	DW	CNVASZ
	DB	07BH
	DW	NONEKY
	DB	000H
	DW	CNVAS9
	DB	00AH
	DW	CNVASG
	DB	00AH
	DW	CNVAS9
	DB	00CH
	DB	03AH
	DW	CNVAS8
	DB	030H
	DW	CNVASX
	DB	020H
	DW	NONEKY
	DB	000H
	DW	CNVAS9
	DB	00EH
	DW	CNVAS9
	DB	010H
	DW	CNVAS9
	DB	012H
	DB	040H
	DW	CNVAS9
	DB	014H
	DW	CNVAS9
	DB	016H
	DW	NONEKY
	DB	000H
	DW	CNVAS9
	DB	018H
	DW	CNVAS9
	DB	01AH
	DW	CNVAS9
	DB	01CH
	DB	049H
	DW	CNVAS9
	DB	01EH
	DW	CNVAS9
	DB	020H
	DW	NONEKY
	DB	000H
	DW	CNVAS9
	DB	01EH
	DW	CNVAS9
	DB	020H
	DW	NONEKY
	DB	000H
	DB	04EH
	DW	NONEKY
	DB	000H
	DW	NONEKY
	DB	005H
	DW	NONEKY
	DB	000H
	DW	NONEKY
	DB	000H
	DW	NONEKY
	DB	005H
	DW	NONEKY
	DB	000H
;
CHTDAT:	DW	A01DAT
	DW	A03DAT
	DW	A02DAT
	DW	A04DAT
	DW	A05DAT
	DW	A06DAT
	DW	A07DAT
	DW	A08DAT
	DW	A09DAT
	DW	A10DAT
	DW	A11DAT
	DW	A12DAT
	DW	A13DAT
	Dw	A14DAT
	Dw	A15DAT
	DW	A16DAT
	DW	A17DAT
;
NONEKY:	SCF
	RET
CNVASX:	CP	C
	JR	NZ,CNVAS8
	LD	A,030H
	RET
;
CNVAS8:	SUB	C
	ADD	A,B
	RET
;
CNVAS9:	SUB	C
	LD	D,000H
	LD	E,B
	LD	HL,CHTDAT
	ADD	HL,DE
	LD	E,(HL)
	INC	HL
	LD	D,(HL)
	EX	DE,HL
	LD	E,A
	LD	D,000H
	ADD	HL,DE
	LD	A,(HL)
	AND	A
	RET	NZ
	SCF
	RET
;
CNVASA:	CALL	CNVAS8
	CP	B
	RET	NZ
	LD	A,040H
	RET
;
CNVASB:	CALL	CNVAS8
	CP	B
	RET	NZ
	LD	A,07EH
	RET

CNVASD:	CP	015H
	JR	Z,CNVASE
	CP	02AH
	JR	NZ,CNVAS9
	LD	A,0AFH
	RET
;
CNVASE:	LD	A,0A8H
	RET
;
CNVASF:	CP	02FH
	JR	NZ,CNVAS8
	LD	A,02DH
	RET
;
CNVASZ:	CP	02FH
	JR	NZ,CNVAS8
	LD	A,03DH
	RET
;
CNVASG:	CP	02BH
	JR	Z,CNVASH
	CP	02DH
	JR	NZ,CNVAS9
	LD	A,0A3H
	RET
;
CNVASH:	LD	A,0A2H
	RET
;
;
A01DAT:	DB	09AH,093H,08FH,092H
	DB	0E1H,0E2H,0E3H,098H
	DB	091H,099H
;
A02DAT:	DB	02AH,02BH,03DH,02CH
	DB	02EH,00DH
;
A03DAT:	DB	095H,0E0H,096H,090H
	DB	09BH,00DH
A04DAT:	DB	0DEH,0C1H,0BAH,0BFH
	DB	0BCH,0B2H,0CAH,0B7H
	DB	0B8H,0C6H,0CFH,0C9H
	DB	0D8H,0D3H,0D0H,0D7H
	DB	0BEH,0C0H,0BDH,0C4H
	DB	0B6H,0C5H,0CBH,0C3H
	DB	0BBH,0DDH,0C2H
A05DAT:	DB	08AH,09EH,084H,082H
	DB	0E6H,0E4H,0E7H,0ECH
	DB	0EDH,0E8H,0EAH,0EBH
	DB	08EH,086H,085h,0E9H
	DB	08DH,09CH,0E5H,09FH
	DB	0EEH,0F0H,083H,09DH
	DB	081H,0EFH,080H
;
A06DAT:	DB	0DFH,0B0H,0D1H,0CDH
	DB	0CEH
;
A07DAT:	DB	000H,0F1H,000H,08BH
	DB	08CH
;
A08DAT:	DB	0DCH,0C7H,0CCH,0B1H
	DB	0B3H,0B4H,0B5H,0D4H
	DB	0D5H,0D6H
;
A09DAT:	DB	0A6H,0C7H,0CCH,0A7H
	DB	0A9H,0AAH,0ABH,0ACH
	DB	0ADH,0AEH
;
A10DAT:	DB	0F7H,000H,000H,000H
	DB	000H,0F2H,0F3H,0F4H
	DB	0F5H,0F6H
;
A11DAT:	DB	03AH,03BH,02CH,02EH
	DB	02FH,000H
;
A12DAT:	DB	02AH,02BH,03CH,03EH
	DB	03FH,05FH
;
A13DAT:	DB	0B9H,0DAH,0C8H,0D9H
	DB	0D2H,0DBH
;
A14DAT:	DB	0B9H,0DAH,0A4H,0A1H
	DB	0A5H,000H
;
A15DAT:	DB	094H,089H,087H,088H
	DB	097H,000H
;
A16DAT:	DB	00CH,01EH,01CH,008H
;
A17DAT:	DB	00BH,01FH,01DH,012H

;------------------------
; FUNCTION KEY SUPPORT
;------------------------
GFKEY:	LD	A,(ONFUNC)
	AND	A
	JR	Z,SCN.FKEY

BACK1:
	LD	C,3
FUNC1:	LD	B,0
	DJNZ	$	;DRLAY
	DEC	C
	JR	NZ,FUNC1
	LD	HL,(ADRSFUNC)
	LD	A,(HL)
	INC	HL
	LD	(ADRSFUNC),HL
	LD	(ONFUNC),A
	AND	A
	RCF
	; SCF
	; CCF
	RET	NZ

; WAIIAT UNTIL ALL F.KEY IS UNACTIVE
LOOP12:	IN	A,(9)
	RRCA
	LD	B,5
LOOP11:	RRCA
	JR	NC,LOOP12
	DJNZ    LOOP11
	SCF
	RET

SCN.FKEY:
	LD	HL,DATFUNC
	IN	A,(8)
	BIT	6,A	; SHIFT IS ACTIVE ?
	JR	NZ,SKIP21
	LD	HL,16*5+DATFUNC
SKIP21:	IN	A,(9)
	RRCA
	LD	DE,16
	LD	B,5
LOOP10:	RRCA
	JR	NC,SKIP20
	ADD	HL,DE
	DJNZ	LOOP10
	SCF
	RET

SKIP20:	LD	(ADRSFUNC),HL
	LD	(ONFUNC),A
	XOR	A
	LD	(KEYCOD),A
	JR	BACK1


;--------------------------
;	INKEY WORK
;--------------------------

KEYCOD:	DB	$45	;DS	1
SHFTST:	DS	1
REPTST:	DS	1
SCANTB:	DS	10
kSCNPT:	DS	1
SCNTB2:	;DS      11
	DS	2
	DB	$FF
	DS	8

ONFUNC:		DS	1
ADRSFUNC:	DS	2

DATFUNC:DB	'FUNCTION KEY 1',0,0
	DB	'FUNCTION KEY 2',0,0
	DB	'FUNCTION KEY 3',0,0
	DB	'FUNCTION KEY 4',0,0
	DB	'FUNCTION KEY 5',0,0
	DB	'FUNCTION KEY 6',0,0
	DB	'FUNCTION KEY 7',0,0
	DB	'FUNCTION KEY 8',0,0
	DB	'FUNCTION KEY 9',0,0
	DB	'FUNCTION KEY 10',0,

;-------------------
;
; ADVANCED ROUTINE
;
;-------------------
SCAP:	LD	A,(PUSHESC)
	AND	A
	JR	NZ,SKIP10
	IN	A,(9)	;PRESS IN CAP ?
	BIT	7,A
	RET	NZ

	LD	(PUSHESC),A
	LD	A,(CAP)
	XOR	1
	LD	(CAP),A
	LD	A,20H
	OUT	(40H),A
	PUSH	BC
	LD	BC,500H
LOOP6:	DEC	BC
	LD	A,B
	OR	C
	JR      NZ,LOOP6
	POP	BC
	XOR	A
	OUT	(40H),A
	RET

SKIP10:	IN	A,(9)
	BIT	7,A
	RET	Z
	XOR	A
	LD	(PUSHESC),A
	RET


; CAPITAL LOCK ROUTINE
CAPS:	PUSH	AF
	LD	A,(CAP)
	AND	A
	JR	NZ,CAPON
	POP	AF
	RET

CAPON:	POP	HL	; H=A,L=F
	LD	A,H
	CP	'a'	
	JR	C,SKIP11
	CP	'z'+1
	JR	NC,SKIP11
	SUB	20H
	JR	SKIP12

SKIP11:	CP	'A'
	JR	C,SKIP12
	CP	'Z'+1
	JR	NC,SKIP12
	ADD	A,20H
SKIP12:	LD	H,A	;H=NEW.A , L=OLD.F
	PUSH	HL
	POP	AF
	RET

PUSHESC:	DB	0
CAP:		DB	0
;************************
;
;	HAND SHAKE
;
;************************

;	SEND TO 80S31
;		[ COMMAND ]
;
SNDCOM:
	PUSH	AF
	LD	A,0FH
	OUT	(CW),A
	DB	3EH
	;
;
;	[ DATA ]
;
SNDDAT:
	PUSH	AF
SDLOP1:	IN	A,(PC)
	AND	2
	JR	Z,SDLOP1
	LD	A,0EH
	OUT	(CW),A
	POP	AF
	PUSH	AF
	OUT	(PB),A
	LD	A,09H
	OUT	(CW),A
SDLOP2:	IN	A,(PC)
	AND	4
	JR	Z,SDLOP2
	LD	A,8
	OUT	(CW),A
SDLOP3:	IN	A,(PC)
	AND	4
	JR	NZ,SDLOP3
	POP	AF
	RET

;
;	GET FROM 80S31
;		[ DATA ]
;
GETDAT:
	LD	A,0BH
	OUT	(CW),A
GTLOP1:	IN	A,(PC)
	RRCA
	JR	NC,GTLOP1
	LD	A,0AH
	OUT	(CW),A
	IN	A,(PA)
	PUSH	AF
	LD	A,0DH
	OUT	(CW),A
GTLOP2:	IN	A,(PC)
	RRCA
	JR	C,GTLOP2
	LD	A,0CH
	OUT	(CW),A
	POP	AF
	RET

;****************************
;
;	CMT SUBROUTINES
;
;****************************
_DVSW	EQU	1F7DH
_IBFAD	EQU	1F74H
_SIZE	EQU	1F72H
_DTADR	EQU	1F70H
_EXADR	EQU	1F6EH

WRI:
WRD:
RDI:
RDD:
VFD:
	LD	A,3
	SCF
	RET

;
; SAVE ROUTINE
;
CMTWRITE:
	LD	(STACKSP),SP
	CALL	CMT_ON
	CALL	RE8251

	LD	A,0CEH
	OUT	(21H),A
	LD	A,011H
	OUT	(21H),A
	IN	A,(20H)
	CALL	WAIT3
	CALL	WAIT3
	CALL	WAIT3
	CALL	MONWTP

	RET	C
	CALL	CMT_OFF
	RET
;
; CMT MOTOR ON
;
CMT_ON:	PUSH	AF
	LD	A,(PORT30)
	AND	13H
	OR	8		; A = 000? 10??
	CALL    SET30H
	POP     AF
	RET
;
; CMT MOTOR OFF
;
CMT_OFF:PUSH	AF
	CALL	WAIT3
	CALL	CMT_OF
	POP	AF
	RET

CMT_OF:	PUSH	AF
	LD	A,(PORT30)
	AND	0F7H
	OR	4		; A = ???? 01??
	CALL	SET30H
	POP	AF
	RET

SET30H:	LD	(PORT30),A
	OUT	(30H),A
	RET

;
; WAIT ROUTINE
;
	PUSH	DE
	PUSH	HL
	LD	E,1
WAIT:	LD	HL,0
WTLOP:	DEC	L
	JR	NZ,WTLOP
	DEC	H
	JR	NZ,WTLOP
	DEC	E
	JR	NZ,WTLOP
	POP	HL
	POP	DE
	RET

WAIT2:	PUSH	DE
	PUSH	HL
	LD	E,6
	JR	WAIT

WAIT3:	PUSH	DE
	PUSH	HL
	LD	E,3
	JR	WAIT



MONWTP:	LD	A,3AH
	CALL	CMT_OUT
	LD	A,H
	CALL	CMT_OUT
	LD	B,A
	LD	A,L
	CALL	CMT_OUT
	ADD	A,B	; 3A-ADRS(HIGH)-ADRS(LOW-SUM
	NEG
	CALL    CMT_OUT
;
; WRITE DATA TO CMT
;
;	INPUT:	HL = START ADRS
;		DE = END   ADRS
;
WR_DATA:LD	A,3AH
	CALL	CMT_OUT
	PUSH	HL
	LD	BC,0FEH
	ADD	HL,BC
	AND	A
	SBC	HL,DE
	POP	HL
	JR	NC,WR_D1
	LD	C,0FFH
	CALL	WR_D2
	JR	WR_DATA
WR_D1:	PUSH	DE
	EX	DE,HL
	AND	A
	SBC	HL,DE
	INC	HL
	LD	C,L
	EX	DE,HL
	POP	DE
	CALL	WR_D2
	LD	A,3AH	;
	CALL	CMT_OUT	;
	XOR	a	; PUT END MARK
	CALL	CMT_OUT	;
	CALL	CMT_OUT	; 3A-00-00
	RET
;
WR_D2:	LD	A,C
	CALL	CMT_OUT	; PUT NUMBER OF DATA
	LD	B,C
WR_D3:	LD	A,(HL)
	CALL	CMT_OUT
	ADD	A,B
	LD	B,A
	INC	HL
	DEC	C
	JR	NZ,WR_D3

	LD	A,B
	NEG		; PUT CHECK SUM
;	CALL	CMT_OUT
;	RET

CMT_OUT:PUSH    AF
CMT_O1:	IN	A,(21H)
	AND	1
	JR	NZ,CMT_O2	; WAIT FOR DTR
	ONSTOP CMTERR
;	IN	A,(9)
;	BIT	0,A
;	JP	Z,CMTERR
	JR	CMT_O1
CMT_O2:	POP	AF
	OUT	(20H),A
	RCF
;	SCF
;	CCF
	RET
;
RE8251:	LD	A,0EH
	OUT	(21H),A
	LD	A,40H
	OUT	(21H),A
	RET
;
; LOAD ROUTINE
CMTLOAD:LD	(STACKSP),SP
	LD	(STACKHL),HL
	LD	A,(PORT30)
	AND	17H
	OR	0CH		;A = 0001 11??
	CALL	SET30H
MONL2:	IN	A,(40H)
	AND	4
	JR	NZ,MONL4
	ONSTOP CMTERR
;	IN	A,(9)
;	BIT	0,A
;	JP	Z,CMTERR
	JR	MONL2

MONL4:	CALL	MONRTP
	RET	C
	CALL	CMT_OFF
;	RCF
	RET

;
; READ FROM CMT
;
MONRTP:	CALL	RE8251
	LD	A,4EH
	OUT	(21H),A
	LD	A,14H
	OUT	(21H),A
	IN	A,(20H)
MONRT1:	IN	A,(21H)
	AND	2
	JR	Z,MONRT1
	IN	A,(21H)
	AND	30H
	JR	NZ,MONRTP
	IN	A,(20H)
	CP	3AH
	JR	NZ,MONRTP
	CALL	CMT_IN
	LD	H,A
	LD	C,A
	CALL	CMT_IN
	LD	L,A
	ADD	A,C
	LD	C,A
	CALL	CMT_IN
	ADD	A,C
	JP	NZ,CMTERR

	LD	A,(_DVSW)
	AND	A		;COMMON FORMAT ?
	JR	NZ,RD_DATA
	LD	HL,(STACKHL)

;
; READ DATUM FROM CMT
;	INPUT : HL = START ADRS
;
RD_DATA:CALL	CMT_IN
	CP	3AH
	JP	NZ,CMTERR
	CALL	CMT_IN
	LD	B,A
	LD	C,A
	AND	A
	RET	Z
RD_D1:	CALL	CMT_IN
	PUSH	AF
	LD	A,(MONVSW)
	AND	A
	JR	Z,RD_D2
	POP	AF
	CP	(HL)
	JP	NZ,CMTERR
	JR	RD_D3
RD_D2:	POP	AF
	LD	(HL),A
RD_D3:	ADD	A,C
	LD	C,A
	INC	HL
	DEC	B
	JR	NZ,RD_D1
	CALL	CMT_IN
	ADD	A,C
	JP	NZ,CMTERR
	JR	RD_DATA

;
; READ 1 BYTE FROM CMHT
;
CMT_IN:	IN	A,(21H)
	AND	2
	JR	NZ,CMT_IN1
	ONSTOP	CMTERR
;	IN	A,(9)
;	BIT	0,A
;	JP	Z,CMTERR
	JR	CMT_IN

CMT_IN1:IN	A,(21H)
	CPL
	AND	30H
	CP	30H
	IN	A,(20H)
	JR	C,CMTERR
	RET

;
CMTERR:	CALL	CMT_OF
	LD	A,1	;Device I/O Error
	SCF
	LD	SP,(STACKSP)
	RET

; LOAD or VERIFY FLAG
MONVSW:	DB	$2D	;DS	1

; FOR SAVE HL Reg.
STACKHL:
	DB	$3E,$B6
	;DS	2



; FOR SAVE SP Reg.
STACKSP:
	DB	$C5
	DS	1

CTRL_P_PATCH:
	LD	A,(AT_WIDTH)
	CP	80
	JP	Z,CTRL_P

	LD	HL,VRMTOP
	LD	C,25
.COPY1
	LD	B,40
.COPY2
	LD	A,(HL)
	CALL	LPTOUT
	RET	C
	INC	HL
	INC	HL
	DJNZ	.COPY2
	LD	A,0DH
	CALL	LPTOUT
	RET	C

	DEC	C
	JR	NZ,.COPY1
	RET

;
;
;
;	END

;------------------------------------------------------

; HL = 行
; DE = 開始位置X
; BC = 新規適用するアトリビュート

	; 新規適用するアトリビュート
	ld a,c

	; 開始位置X
	ld c,e

	; 開始行
	ld b,l

;--------------------------------------------------------------------------------------------------
;tab4 sjis
;b=行y(0-24) c=開始位置x(0-79) a=新規適用するアトリビュート de,hl 使用
; アトリビュートの適用順を考えなくて良いヘルパールーチン
; 開始位置を $80 にして呼び出すと該当行を全てクリア($80,$E8)する。
;
; 同じ開始位置ものがある場合 → 上書きする。PC80/88 アトリビュートの仕様により、同一開始位置でのアトリビュートは不可
; 属性総数が20個を超えた場合 → 最後尾のアトリビュートが追い出される。
; このルーチンを使わずに直接アトリビュートを弄る併用は考慮していないので必ずこのルーチンを呼ぶこと。
; 最初のアトリビュート開始位置は 0 か $80 で初期化されており、以後のアトリビュートもソートされている前提で使用する。
; 最初のアトリビュートは暗黙の x=0 になってしまうので、明示的な x=0 以外では使用しない。
;
; テキストVRAM がカラーモードになっていること。でないと色設定などができない。

TVRAM			equ		VRMTOP	; $F300 ; $F3C8
;ATRC と ATRD は同時には使えない
;違うグループは同時に指定できる(ブリンク＋アンダーラインなど）
ATRD_DECOLAT	equ		%00000000
ATRC_COLOR		equ		%00001000

ATRC_BLACK		equ		%00001000
ATRC_BLUE		equ		%00101000
ATRC_RED		equ		%01001000
ATRC_PURPLE		equ		%01101000
ATRC_GREEN		equ		%10001000
ATRC_CYAN		equ		%10101000
ATRC_YELLOW		equ		%11001000
ATRC_WHITE		equ		%11101000

ATRC_SEMIG		equ		%00011000
ATRC_CHR		equ		%00001000

ATRD_DLINE		equ		%00100000
ATRD_ULINE		equ		%00100000

ATRD_REVSECa	equ		%00000111				;101と同じ。指定した部分が■で隠れる
ATRD_REVBLK		equ		%00000110
ATRD_REVSEC		equ		%00000101
ATRD_REV		equ		%00000100
ATRD_SECa		equ		%00000011				;001と同じ。指定した部分が黒で隠れる
ATRD_BLK		equ		%00000010
ATRD_SEC		equ		%00000001
ATRD_NOR		equ		%00000000

;sample
;	ld			a,ATRC_RED
;	ld			bc,(0 << 8) | 10				;x=10 y=0 から 文字色を赤にする
;	call		SetTextAtr

SetTextAtr:
	ld			h,a
	; in			a,($32)
	; push		af
	; res			4,a
	; out			($32),a

	push		hl							;push af の代わり。アトリビュートを一時保存

	call		.sub						;b=y c=x を hl=tatr にする
	bit			7,c
	jr			nz,.clear					;開始位置=$80ならその行は初期化する

	ld			a,c							;x
	ld			b,20
.loop
	cp			(hl)						;同じ開始位置のものがあったら差し替える
	jr			z,.found					;先頭から検索するので、最初のアトリビュートが x=0 でない場合でも一致すれば上書きしてしまう。
	inc			hl							;その場合、新たに設定したアトリビュートの開始位置は強制的に x=0 と解釈されてしまう。
	inc			hl							;なので、正常に初期化されていることが前提となる。
	djnz		.loop

	ld			b,20
.sort
	dec			hl
	dec			hl
	ld			a,(hl)
	or			a
	jr			z,.next						;0 または 80 以上は空白と見なして飛ばす
	cp			80
	jr			nc,.next
	ld			a,c
	cp			(hl)
	jr			c,.next						;自分より開始位置の大きいものは飛ばして小さいものを探す

	ld			a,b
	cp			20							;いきなり初回で見つかったら後ろに押し出さずに最後尾だけを書き換え
	jr			nz,.skip1
	dec			hl
	dec			hl
	dec			b

.skip1
	inc			hl							;小さい開始位置が見つかったら、その後ろに挿入する
	inc			hl
	pop			af
.sortlp
	ld			e,(hl)						;古い値を de に保存
	ld			(hl),c						;新しい値 ac を入れる
	inc			hl
	ld			d,(hl)
	ld			(hl),a
	inc			hl

	ld			a,b
	inc			b
	cp			19							;19が最終位置
	ld			c,e
	ld			a,d
	jr			nz,.sortlp					;新しい属性を挿入した結果、最後尾の属性は追い出される
	jr			.exit

.next
	djnz		.sort						;最後まで検索して空だらけ or 一番最初の開始位置が a よりうしろだったら↓

	pop			af
	inc			c							;x=0 の場合はそのまま先頭のアトリビュートに上書き。押し出しはナシ。
	dec			c
	jr			z,.skip2
	inc			hl							;x!=0の場合、先頭に書き込むと暗黙のx=0にされてしまうので、一つずらして書き込む。
	inc			hl							;この場合、最後尾のアトリビュートは追い出されて無効となる
	ld			b,1
	jr			.sortlp
.skip2										;x=0 の場合はそのまま先頭のアトリビュートに上書き。押し出しはナシ。
	ld			(hl),c
	inc			hl
	ld			(hl),a
	jr			.exit

.found										;同じ開始位置のものがあったら、そこに上書きする
	pop			af							;この場合、先頭に 0 でも 80 以上でもない無効な開始位置が書き込まれていたはずなので上書きで問題ない
	inc			hl
	ld			(hl),a
	jr			.exit

.clear
	pop			af
	ld			b,20
.clearlp
	ld			(hl),$80					;全クリアは BASIC に倣って $80,$E8 とする。
	inc			hl
	ld			(hl),$E8
	inc			hl
	djnz		.clearlp
.exit
	; pop			af
	; out			($32),a
	ret

.sub										;b=y c=x を hl=tatr　にする
	push		bc
	ld			a,b
	ld			h,b
	ld			l,0
	ld			b,l
	srl			h
	rr			l							;x128
	add			a,a
	add			a,a
	add			a,a
	ld			c,a							;x8
	sbc			hl,bc
	ld			bc,TVRAM+80
	add			hl,bc						;hl=TATR+120*y
	pop			bc
	ret

; ここから下は本来はS-OS側にあったメイン処理だが
; BIOS処理として切り出している

PGETL:
	XOR	A
	LD	(AT_PRCNT),A
	JP	XBIOS.AT_GETL
NAMEBF:
	DS	18

AT_PRCNT:
	DB	0
;----------------------------
PRNTS:
	PUSH	AF
	LD	A," "
	JR	PRINT+1
LTNL:
	PUSH	AF
	LD	A,00DH
	JR	PRINT+1
NL:
	PUSH	AF
	LD	A,(AT_PRCNT)
	OR	A
	JR	NZ,LTNL+1
	POP	AF
	RET

PRINT:
	PUSH	AF
	CALL	XMCNV
	PUSH	AF
	CP	00DH
	LD	A,(AT_PRCNT)
	JR	NZ,AT_00001
	LD	A,0FFH
AT_00001:
	INC	A
	LD	(AT_PRCNT),A
	LD	A,(_LPSW)
	OR	A
	JR	Z,AT_00002
	POP	AF
	PUSH	AF
	CALL	LPRNT
AT_00002:
	POP	AF
	PUSH	AF
	CALL	XBIOS.AT_PRINT
	POP	AF
	POP	AF
	RET


LPRNT:
	PUSH	AF
	;;CALL	AT_LPOUT
	CALL	LPTOUT
	JR	C,LPEND
	POP	AF
	OR	A
	RET
	;
LPEND:
	XOR	A
	LD	(_LPSW),A
	POP	AF
	SCF
	RET
	;
LPTON:
	PUSH	AF
	LD	A,1
	LD	(_LPSW),A
	POP	AF
	RET

LPTOF:
	PUSH	AF
	XOR	A
	LD	(_LPSW),A
	POP	AF
	RET

;-------------------------
GETL:
	CALL	PGETL
	JR	NC,GETL1
	EX	DE,HL
	LD	(HL),01BH
	INC	HL
	LD	(HL),000H
	DEC	HL
	EX	DE,HL
	RET
GETL1:
	PUSH	DE
	LD	A,(DE)
	OR	A
	JR	NZ,AT_00005
	POP	DE
	RET
AT_00005:
	CALL	MXCNV
	LD	(DE),A
	INC	DE
	JR	GETL1+1

GETKY:
	LD	A,0
	CALL	XBIOS.AT_INKEY
	OR	A
	RET	Z
	CP	3
	JP	NZ,MXCNV
	LD	A,01BH
	RET

sINKEY:
	CALL	GETKY
	OR	A
	JR	Z,sINKEY
	RET

FLGET:
	LD	A,1
	JP	XBIOS.AT_INKEY

SCRN:
	PUSH	HL
	CALL	LOCHK
	JR	C,SCRER
	PUSH	BC
	LD	B,H
	LD	C,L
	CALL	XBIOS.AT_ADRC2
	LD	A,(HL)
	CALL	MXCNV
	POP	BC
	CP	20H
	JR	NC,AT_00011
	LD	A,020H
AT_00011:
	OR	A
SCRER:
	POP	HL
	RET
LOCHK:
	PUSH	BC
	LD	B,A
	LD	A,(XBIOS.AT_WIDTH)
	DEC	A
	CP	L
	JR	C,LCERR
	LD	A,(_MXLIN)
	DEC	A
	CP 	H
	JR	C,LCERR
	LD	A,B
	POP	BC
	RET
LCERR:
	LD	A,14
	POP	BC
	RET

CSR:
	LD	HL,(XBIOS.AT_DSPXY)
	RET

LOC:
	CALL	LOCHK
	RET	C
	LD	(XBIOS.AT_DSPXY),HL
	RET

HEX:
	SUB	"0"
	RET	C
	CP	10
	JR	C,HEX1
	CP	17
	RET	C
	SUB	7
	CP	010H
HEX1:
	CCF
	RET
AHEX:
	PUSH	BC
	LD	A,(DE)
	INC	DE
	CALL	HEX
	JR	C,AHEX1
	RRCA
	RRCA
	RRCA
	RRCA
	LD	C,A
	LD	A,(DE)
	INC	DE
	CALL	HEX
	JR	C,AHEX1
	OR	C
AHEX1:	POP	BC
	RET
HLHEX:
	CALL	AHEX
	LD	H,A
	CALL	NC,AHEX
	LD	L,A
	RET

;------------------------
;
;EXCHANEG ONLY 7BH AND 87H
;
MXCNV:
XMCNV:
	CP	7BH
	JR	Z,CNVS1
	CP	87H
	RET	NZ
	LD	A,7BH
	RET

CNVS1:	LD	A,87H
	RET
	;

; WORK
_MXLIN:	DB	25
_LPSW:	DB	0
AT_KBUF:
	DS	256
