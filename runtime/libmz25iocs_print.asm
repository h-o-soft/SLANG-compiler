; SLANG Runtime Library for MZ-2500 IOCS text output
;
; This library is based on libsos_print.asm, but removes S-OS calls.
; It is intended for BASIC-system / IOCS based targets where S-OS #PRINT
; must not be used.

; @name WIDTH
; @resident shared
; @param_count 1
; Not implemented for IOCS version.
RET


; @name PRMODE
; @resident shared
; @param_count 1
; Printer output mode is not implemented for IOCS version.
RET


; @name SCREEN
; @resident shared
; @param_count 2
; Not implemented for IOCS version.
LD HL,0
RET


; @name LOCATE
; @resident shared
; @param_count 2
; Text cursor positioning is not implemented yet.
RET


; @name PTAB
; @resident shared
; @param_count 1
; @calls PCR1
LD E,$1C
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
LD A,H
CALL PRT
LD A,L
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
; MZ-2500 IOCS CRT1C: output one character in A.
RST 18H
DB  03H
RET

; VTOS writes five decimal digits and a trailing zero here.
; Five digits are enough for a 16-bit value; PSIGN prints '-' separately.
WORK10:
DS  10


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
