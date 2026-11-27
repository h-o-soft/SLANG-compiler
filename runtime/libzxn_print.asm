; Converted from lib/libdef/libzxn_print.yml
; SLANG Runtime Library (new format)

; @name WIDTH
; @resident shared
; @param_count 1
RET


; @name PRMODE
; @resident shared
; @param_count 1
RET


; @name SCREEN
; @resident shared
; @param_count 2
RET


; @name LOCATE
; @resident shared
; @param_count 2
; @calls ZXNWORK
LD H,E
LD (LOCX),HL
RET


; @name PTAB
; @resident shared
; @param_count 1
; @calls PCR1
; TAB -> Space
LD E,$20
JR PCR1


; @name PSPC
; @resident shared
; @param_count 1
; @calls PCR1
LD E,' '
JR PCR1


; @name PCRONE
; @resident shared
; @param_count 0
; @calls PCR
LD HL,1


; @name PCR
; @resident shared
; @param_count 1
; @calls PCR1
LD E,$0D


; @name PCR1
; @resident shared
; @param_count 1
; @calls PSTR
EX DE,HL


; @name PSTR
; @resident shared
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


; @name PCHR
; @resident shared
; @calls PRT
; 16-bit 値 HL のうち 0 バイトは出力しない (liblsx_print と同じ)。
; PRT は 0 も字として描いて桁を進めるので、CHR$(48+I) が 2 字になっていた。
LD A, H
OR A
CALL NZ,PRT
LD A, L
OR A
JR NZ,PRT
RET


; @name CRDISP
; @resident shared
; @calls PRT
LD A,$0D
JR PRT


; @name PHEX4
; @resident shared
; @param_count 1
; @calls PHEX2
LD A,H
CALL PHEX


; @name PHEX2
; @resident shared
; @param_count 1
; @calls PHEX
LD A,L


; @name PHEX
; @resident shared
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
; @resident shared
; @param_count 1
; @calls ZXNWORK,GETLOCADR,ZXN_SCROLL,ZXN_PUTATR
PUSH HL
; PSTR は回数を DE に持って呼ぶので、DE・BC も残す
PUSH DE
PUSH BC
; 前の字で一番下の行 (23) を越えていたら、ここで画面を 1 行上げる (遅れて流す)
CALL ZXN_SCROLL
; HL -> VRAM ADDR
CP $0D
JR Z,.NEXTLINE

; Load Cursor Position
LD DE,(LOCX)
SLA E
SLA E
SLA E
SLA D
SLA D
SLA D
; PIXELAD
DB $ED,$94

; generate FONT Address to HL
SUB 32
LD E,A
LD D,0
; multiply 8
SLA E
RL D
SLA E
RL D
SLA E
RL D
LD A,$3D
ADD A,D
LD D,A
LD B,8
.drawfont
LD A,(DE)
LD (HL),A
INC DE
INC H
DJNZ .drawfont

; COLOR の色 (0 なら書かない)
CALL ZXN_PUTATR

LD A,(LOCX)
INC A
LD (LOCX),A
CP $20
JR NZ,.NONEXT
LD A,0
LD (LOCX),A
LD A,(LOCY)
INC A
LD (LOCY),A
.NONEXT
POP BC
POP DE
POP HL
RET

.NEXTLINE
LD A,(LOCY)
INC A
LD (LOCY),A
XOR A
LD (LOCX),A
POP BC
POP DE
POP HL
RET


; @name GETLOCADR
; @resident shared
; @calls ZXNWORK
PUSH AF
PUSH DE
LD A,(LOCY)
AND $1F
LD L,A
LD H,0
; HL*32
ADD HL,HL ; 2
ADD HL,HL ; 4
ADD HL,HL ; 8
ADD HL,HL ; 16
ADD HL,HL ; 32
LD A,(LOCX)
LD E,A
LD A,(TXTPLANE)
LD D,A
ADD HL,DE
POP DE
POP AF
RET


; @name ZXN_SCROLL
; @resident shared
; @calls ZXNWORK
; LOCY が 24 以上なら、文字の画面を 1 行上げて LOCY = 23 にする。どのレジスタも変えない。
; 点は文字の行 1〜23 を 0〜22 へ、一番下の行は 0。属性 (5800H〜) も 1 行上げ、一番下の行は
; TXTATR (COLOR) が 0 でなければそれで埋め、0 なら前の 23 行目のまま。ROM と PIXELAD は使わない (PRT__NOROM からも呼ぶ)
PUSH AF
LD A,(LOCY)
CP 24
JR C,.done
PUSH BC
PUSH DE
PUSH HL
LD C,0
.row
LD A,C
CALL .rowadr
EX DE,HL
LD A,C
INC A
CALL .rowadr
LD B,8
.line
PUSH BC
PUSH HL
PUSH DE
LD BC,32
LDIR
POP DE
POP HL
POP BC
INC H
INC D
DJNZ .line
INC C
LD A,C
CP 23
JR NZ,.row
CALL .rowadr
LD B,8
.clear
PUSH BC
PUSH HL
LD D,H
LD E,L
INC DE
LD (HL),0
LD BC,31
LDIR
POP HL
POP BC
INC H
DJNZ .clear
LD HL,$5820
LD DE,$5800
LD BC,736
LDIR
LD A,(TXTATR)
OR A
JR Z,.attrkept
LD H,D
LD L,E
LD (HL),A
INC DE
LD BC,31
LDIR
.attrkept
LD A,23
LD (LOCY),A
POP HL
POP DE
POP BC
.done
POP AF
RET
; A = 文字の行 → HL = その行の点の 1 本目の番地 (40H | (行 AND 18H)、((行 AND 7) << 5))
.rowadr
LD L,A
AND $18
OR $40
LD H,A
LD A,L
AND $07
RRCA
RRCA
RRCA
LD L,A
RET


; @name ZXN_PUTATR
; @resident shared
; @calls ZXNWORK
; TXTATR (COLOR) が 0 でなければ、カーソル (LOCX, LOCY) の桁の属性 (5800H + 行 × 32 + 桁) に書く。A・DE・HL が変わる
LD A,(TXTATR)
OR A
RET Z
LD E,A
LD A,(LOCY)
RRCA
RRCA
RRCA
LD L,A
AND $03
OR $58
LD H,A
LD A,L
AND $E0
LD L,A
LD A,(LOCX)
OR L
LD L,A
LD (HL),E
RET


; @name PSIGN
; @resident shared
; @param_count 1
; @calls PRT,NEGHL,P10
BIT 7, H
JR Z,.psign1
LD A, $2D
CALL PRT
CALL NEGHL
.psign1


; @name P10
; @resident shared
; @param_count 1
; @function_type Machine
; @calls P10to5,P10toN
LD DE, -1
JR P10toN


; @name P10to5
; @resident shared
; @param_count 1
; @function_type Machine
; @calls P10toN
LD DE, 0005


; @name P10toN
; @resident shared
; @param_count 2
; @function_type Machine
; @calls PRT,VTOS,PMSX,ZXNWORK
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
; @resident shared
; @calls PMSX1
LD B, 00


; @name PMSX1
; @resident shared
; @calls PRT,PMSG
LD A, (HL)
CP B
RET Z
CALL PRT
INC HL
JR PMSX1


; @name PMSG
; @resident shared
; @calls PMSX1
LD B, $0D
JR PMSX1


; @name MPRNT
; @resident shared
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


; @name COLOR
; @resident shared
; @calls ZXNWORK
; この後の PRINT の字の色 (ZX の属性のバイト: ビット 0〜2 インク、3〜5 紙、6 明るさ、7 点滅)。
; 0 なら色を書かない (始まりも 0。そのときは画面の色のまま)
LD A,L
LD (TXTATR),A
RET


