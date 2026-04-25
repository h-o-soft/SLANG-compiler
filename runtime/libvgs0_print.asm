; Converted from lib/libdef/libvgs0_print.yml
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
LD A, H
CALL PRT
LD A, L
JR PRT


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
; @calls VGSWORK,GETLOCADR
PUSH HL
; HL -> VRAM ADDR
CP $0D
JR Z,.NEXTLINE
CP $00
JR Z,.NONEXT

CALL GETLOCADR
LD (HL),A

; update attribute
LD A,H
OR $04
LD H,A
LD A,(TXTATR)
LD (HL),A


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
POP HL
RET

.NEXTLINE
LD A,(LOCY)
INC A
LD (LOCY),A
XOR A
LD (LOCX),A
POP HL
RET


; @name GETLOCADR
; @resident shared
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
; @calls PRT,VTOS,PMSX,VGSWORK
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
; @calls VGSWORK
LD A,(TXTATR)
AND $F0
OR L
LD (TXTATR),A
RET


; @name TEXTPLANE
; @resident shared
; @calls VGSWORK
; HL = 0 -> BG / 1 -> FG
LD A,$80
BIT 0,L
JR Z,.TXTBG
OR $08
.TXTBG
LD (TXTPLANE),A
RET


