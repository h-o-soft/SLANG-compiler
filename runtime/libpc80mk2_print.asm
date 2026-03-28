; Converted from /home/user/SLANG-compiler/lib/libdef/libpc80mk2_print.yml
; SLANG Runtime Library (new format)

; @name WIDTH
; HL=80 or 40
ld  b,l
ld  c,25
jp	BIOS.WIDTH


; @name WIDTH2
; HL=80 or 40
; DE=25 or 20
ld  b,l
ld  c,e
jp	BIOS.WIDTH


; @name TEXTMODE
; L = 0でファンクションキー非表示、1で表示
; E = 0で白黒、1でカラー
XOR A
INC L
DEC L
JR Z,.nofunc
LD A,$FF
.nofunc
LD B,A

XOR A
INC E
DEC E
JR Z,.mono
LD A,$FF
.mono
LD C,A

jp	BIOS.FUNC_COLOR 	; ファンクションキーを消してカラーモードにする


; @name LOCATE
; HL=x DE=y
LD H,L
LD L,E

LD A,H
BIT 7,A
JR Z,.nowidthminus
LD H,0
.nowidthminus

LD A,L
BIT 7,A
JR Z,.noheightminus
LD L,0
.noheightminus

INC H
INC L

LD A,H
CP 81
JP C,.widthok
LD H,80
.widthok

LD A,L
CP 26
JP C,.heightok
LD L,25
.heightok

jp BIOS.LOCATE


; @name PRT
; @calls PC80CALLS
CALL BIOS.PUTCRT1
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


; @name CTRL0D
; @calls CSR,AT_VRCALC
CALL _POS
LD L,0
INC H

PUSH	BC
CALL AT_VRCALC
LD (_TXADR),HL
POP	BC

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


; @name SETATR
; @lib PC80ASM
; @extlib pc8001/setatr.asm:SETATR

