; Converted from /home/user/SLANG-compiler/lib/libdef/libsos_print.yml
; SLANG Runtime Library (new format)

; @name WIDTH
; @param_count 1
; @calls SOSCALLS
LD A,L
LD (AT_WIDTH),A
CALL sWIDCH
RET


; @name PRMODE
; @param_count 1
; @calls SOSCALLS,PRT
LD A,L
CP 1
LD HL,sPRINT
JR NC,.prmode1
CALL sLPTOF
JR .prmode2
.prmode1
JR NZ, .prmode3
CALL sLPTON
JR .prmode2
.prmode3
LD HL,sLPRNT
.prmode2
LD (PRT+1), HL
RET


; @name SCREEN
; @param_count 2
; @calls SOSCALLS
LD H,E
CALL sSCRN
LD L,A
LD H,0
RET


; @name LOCATE
; @param_count 2
; @calls SOSCALLS
LD H,E
JP sLOC


; @name PTAB
; @param_count 1
; @calls PCR1
LD E,$1C
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
; @calls PCR1
LD E,$0D


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


; @name PCHR
; @calls PRT
LD A, H
CALL PRT
LD A, L
JR PRT


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
; @calls SOSCALLS
JP sPRINT

WORK10:
DB  "12345",0
DS  4


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


