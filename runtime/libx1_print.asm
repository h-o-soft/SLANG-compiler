; Converted from lib/libdef/libx1_print.yml
; SLANG Runtime Library (new format)

; @name WIDTH
; @param_count 1
; @calls sWORK,X1WORK,CTRL0C
PUSH	BC
PUSH	DE
PUSH	HL

LD A,L
LD	BC,$01FF0
CP	41
JR	C,.WIDTH40
IN	A,(C)
RRCA
LD	HL,_C8025L
JR	C,.SETCRTC
LD	HL,_C8025H
JR	.SETCRTC
.WIDTH40
IN	A,(C)
RRCA
LD	HL,_C4025L
JR	C,.SETCRTC
LD	HL,_C4025H
.SETCRTC
LD	DE,_CRTCD
LD	BC,16
LDIR

LD	HL,_CRTCD
XOR	A
.SETCRT1
LD	BC,01800H
OUT	(C),A
INC	C
INC	B
OUTI
INC	A
CP	12
JR	NZ,.SETCRT1
INC	HL
INC	HL
LD	BC,01A03H+00100H
OUTI
LD	BC,01FD0H+00100H
OUTI

CALL	CTRL0C

POP	HL
POP	DE
POP	BC

; LSX-Dodgers の CRTCD 領域 (LSX_base + $B0, 16 byte) を同期する。
; これにより LSX 内部の _WIDTH ($+$B1), _PAGE_MINUS ($+$BA),
; _WIDTH_MINUS ($+$BC), WK1FD0 ($+$BF) が一括更新され、プログラム終了後
; に LSX がプロンプト再描画で CRTC を再設定しても画面が崩れない
; (refs/MODE.ASM X1 パスと同じ考え方)。
; LSX 基底は WBOOT の JP 先アドレス ($0001 のワード) から動的に取得する。
; WBOOTBK は SLANGINIT の直後に WORK ゼロクリアされて使えないため
; 直接 ($0001) を読む必要がある。
; ※ 本実装は LSX_HEIGHT を 25 に固定して運用する (X1 CRTC が 25 行表示
;   なので物理画面を使い切れる形)。LSX デフォルト (24) から 25 に書き換え
;   られるので、WIDTH 呼出し以降 LSX 内部の HEIGHT も 25 になる点に注意。
LD	HL,($0001)		; HL = WBOOT の JP 先 (= LSX 基底 + 微小オフセット)
LD	L,$B0			; HL = LSX_base + $B0 (CRTCD 先頭)
EX	DE,HL			; DE = dest (LSX CRTCD)
LD	HL,_CRTCD		; HL = source (ローカル 16 byte)
LD	BC,16
LDIR

; LSX の _HEIGHT ($+$97) を 25 に書き込む。
; テンプレート側の _PAGE_MINUS = -WIDTH*25 と整合が取れ、LSX が 25 行目も
; 使い切るようになる。
LD	HL,($0001)
LD	L,$97
LD	(HL),25

LD	A,(WK1FD0)
LD	(_WK1FD0),A
LD	A,(_CRTCD+1)
LD	(AT_WIDTH),A
AND	A
RET


; @name CTRL0B
; @calls sWORK,X1WORK
LD	HL,0
LD	(_TXADR),HL
RET


; @name CTRL0C
; @calls CTRL0B,sWORK,X1WORK
CALL	CTRL0B
CTRL06:
LD	BC,(_TXADR)
.C1AX1
LD	A,B
OR	038H
LD	B,A
DB	0EDH,071H	;OUT (C),0	Z80未定義命令	kanji
RES	3,B
LD	A,020H
OUT	(C),A		;Text
RES	4,B
LD	A,(AT_COLORF)
OUT	(C),A		;Color
INC	BC
RES	5,B
LD	HL,(_CRTCD+10)
ADD	HL,BC
JR	NC,.C1AX1
RET


; @name PRMODE
; @param_count 1
; PRMODE not supported
RET


; @name SCREEN
; @param_count 2
; @calls sSCRN
LD H,E
CALL sSCRN
LD L,A
LD H,0
RET


; @name LOCATE
; @param_count 2
; @calls AT_VRCALC,sWORK,X1WORK
LD H,E
PUSH	BC
PUSH	HL
CALL	AT_VRCALC
LD	(_TXADR),HL
POP	HL
POP	BC
RET


; @name AT_VRCALC
; @param_count 1
; @calls sWORK,X1WORK
PUSH	DE
LD C,L
LD B,8
LD E,H
LD D,0
LD HL,(_CRTCD)
LD L,D
.LOC2
ADD HL,HL
JR NC,.LOC3
ADD HL,DE
.LOC3
DJNZ .LOC2
ADD HL,BC
POP DE
RET


; @name PTAB
; @param_count 1
; @calls PCR1
LD E,$09
JR PCR1


; @name PSPC
; @param_count 1
; @calls PCR1
LD E,' '
JR PCR1


; @name PCRONE
; @param_count 0
; @calls PCR
LD HL,1


; @name PCR
; @param_count 1
; @calls PSTR2
EX DE,HL
LD HL,$0D0A
JR PSTR2


; @name PCR1
; @param_count 1
; @calls PSTR
EX DE,HL


; @name PSTR
; @param_count 2
; @calls PRT
.pstr1
LD A,D
OR E
RET Z
LD A,L
CALL PRT
DEC DE
JR .pstr1


; @name PSTR2
; @param_count 2
; @calls PCHR
.pstr1
LD A,D
OR E
RET Z
CALL PCHR
DEC DE
JR .pstr1


; @name PCHR
; @calls PRT
LD A, H
OR A
CALL NZ,PRT
LD A, L
OR A
JR NZ,PRT


; @name CRDISP
; @calls PRT
LD A,$0D
JR PRT


; @name PHEX4
; @param_count 1
; @calls PHEX2
LD A,H
CALL PHEX


; @name PHEX2
; @param_count 1
; @calls PHEX
LD A,L


; @name PHEX
; @param_count 1
; @calls SASC,PRT
PUSH AF
RRCA
RRCA
RRCA
RRCA
CALL SASC
CALL PRT

POP AF
CALL SASC


; @name PRT
; @param_count 1
; @calls X1WORK,CTRL0D
PUSH DE
PUSH BC
PUSH HL

CP 00EH
JR C,PRT_CTRL

; PRINT ANK
LD	E,A
LD	BC,(_TXADR)
LD	A,B
OR	038H
LD	B,A
DB	0EDH,071H	;OUT (C),0
RES	3,B
OUT	(C),E		;Text
LD A,B
AND 007H
LD B,A
INC BC
LD	(_TXADR),BC
LD	A,E

PRT_END:
POP HL
POP BC
POP DE
AND	A
RET

PRT_CTRL:
CP 13
JP NZ,CTRL_NO13
CALL CTRL0D
JR PRT_END

CTRL_NO13:
JR PRT_END

WORK10:
DB  "12345",0
DS  4


; @name CTRL0D
; @calls LSXCALLS
; BDOSにCR+LFを送りLSX-Dodgersに改行・スクロールを委譲
; _TXADR($EE8E)はLSX-Dodgersと共有しているため自動的に同期される
PUSH DE
LD E,$0D
LD C,$06
CALL $0005
LD E,$0A
LD C,$06
CALL $0005
POP DE

RET


; @name CSR
_POS:
LD	HL,(_TXADR)
PUSH	BC
LD	BC,(_CRTCD+12)
XOR	A
POS1:
ADD	HL,BC
INC	A
JR	C,POS1
SBC	HL,BC
DEC	A
LD	H,A
POP	BC
AND	A
RET


; @name PSIGN
; @param_count 1
; @calls PRT,NEGHL,P10
BIT 7, H
JR Z,.psign1
LD A, $2D
CALL PRT
CALL NEGHL
.psign1


; @name P10
; @param_count 1
; @function_type Machine
; @calls P10to5,P10toN
LD DE, -1
JR P10toN


; @name P10to5
; @param_count 1
; @function_type Machine
; @calls P10toN
LD DE, 0005


; @name P10toN
; @param_count 2
; @function_type Machine
; @calls PRT,VTOS,PMSX
PUSH DE
LD DE, WORK10
CALL VTOS
EX DE, HL
POP DE
LD A, E
CP $05
JR NC, .p10ton1
LD A, $05
SUB E
.p10ton2
INC HL
DEC A
JR NZ, .p10ton2
JR PMSX
.p10ton1
LD A, E
CP $FF
JR NZ, PMSX
.p10ton4
LD A, (HL)
CP $20
JR NZ, PMSX
INC HL
JR .p10ton4


; @name PMSX
; @calls PMSX1
LD B, 00


; @name PMSX1
; @calls PRT,PMSG
LD A, (HL)
CP B
RET Z
CALL PRT
INC HL
JR PMSX1


; @name PMSG
; @calls PMSX1
LD B, $0D
JR PMSX1


; @name MPRNT
; @calls PRT
EX (SP),HL
.mprnt2
LD A, (HL)
INC HL
OR A
JR Z, .mprnt1
CALL PRT
JR .mprnt2
.mprnt1
EX (SP),HL
RET


; @name VTOS
; @param_count 2
; @function_type Machine
; @calls DIVHLDE8
PUSH HL
EXX
POP HL
EXX
LD HL, $0005
ADD HL, DE
LD (HL), $00
LD B, $05
.vtos1
EXX
LD E, $0A
CALL DIVHLDE8
LD A, E
ADD A, $30
EXX
DEC HL
LD (HL), A
DJNZ .vtos1
LD B, $04
.vtos3
LD A, (HL)
CP $30
JR NZ, .vtos2
LD (HL), $20
INC HL
DJNZ .vtos3
.vtos2
RET


; @name X1WORK
; LSX-Dodgers 1.62c ワーク共有
_TXADR		EQU	$EE8E	; テキストカーソルVRAMアドレス
; CRTCD 関連 (_WIDTH / _PAGE_MINUS / _WIDTH_MINUS / WK1FD0) は WIDTH 内で
; LSX_base + $B0 へ 16 byte LDIR することで同期するため、個別 EQU は不要。

AT_COLORF:
DB	7
AT_WIDTH:
DB	80
_WK1FD0:DB  0

; X1 の CRTC は 25 行表示。LSX-Dodgers のデフォルト _HEIGHT は 24 だが、
; WIDTH 関数内で LSX の _HEIGHT を 25 に書き換えて整合を取り、25 行
; すべてを使えるようにする (下記 WIDTH 末尾の _HEIGHT 書き込みを参照)。
CRTC_LINE EQU 25
_CRTCD:
DB	06FH,050H,059H,038H,01FH,002H,019H,01CH
DB	000H,007H
DW	0-80*CRTC_LINE,0-80
DB	00CH
WK1FD0:	DB	0A0H
_C8025L:
DB	06FH,050H,059H,038H,01FH,002H,019H,01CH
DB	000H,007H
DW	0-80*CRTC_LINE,0-80
DB	00CH
DB	0A0H
_C8025H:
DB	06BH,050H,059H,088H,01BH,001H,019H,01AH
DB	000H,00FH
DW	0-80*CRTC_LINE,0-80
DB	00CH
DB	0A3H
_C4025L:
DB	037H,028H,02DH,034H,01FH,002H,019H,01CH
DB	000H,007H
DW	0-40*CRTC_LINE,0-40
DB	00DH
DB	0A0H
_C4025H:
DB	035H,028H,02DH,084H,01BH,001H,019H,01AH
DB	000H,00FH
DW	0-40*CRTC_LINE,0-40
DB	00DH
DB	0A3H


