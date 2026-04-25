; Converted from lib/libdef/libp88_print.yml
; SLANG Runtime Library (new format)

; @name P88PCOMMON
; @resident shared
VRMTOP EQU $F3C8
LOCX: DB 0
LOCY: DB 0


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
; @calls GETLOCADR
PUSH HL
CP $0D
JR Z,.NEXTLINE

CALL GETLOCADR


; TO TEXT VRAM
PUSH BC
LD B,A

; LD A,($31)
; AND $FD ; ROM/RAM
; LD ($31),A

IN A,($32)
LD C,A
AND $AF
OUT ($32),A
LD A,B

LD (HL),A

; TO MAIN RAM
; LD A,($31)
; OR $02  ; 64k RAM
; LD ($31),A

LD A,C
OUT ($32),A
POP BC

LD A,(LOCX)
INC A
LD (LOCX),A
CP 80
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

WORK10:
DB  "12345",0
DS  4


; @name GETLOCADR
; @resident shared
; @calls P88PCOMMON
PUSH	BC
LD	BC,(LOCX)
VADRS1:
PUSH	AF
PUSH	DE

LD	L,B
LD	H,0

; HLを10倍にする
LD D,H
LD E,L
ADD HL, HL  ; HL = 2 * original value
ADD HL, HL  ; HL = 4 * original value
ADD HL, DE  ; HL = 5 * original value
ADD HL, HL  ; HL = 10 * original value

; DEに10倍の値を保存
; DE = 10 * original value
LD D,H
LD E,L

; DEを12倍にする（10倍 + 2倍）
ADD HL, DE  ; HL = 20 * original value
ADD HL, DE  ; HL = 30 * original value
ADD HL, DE  ; HL = 40 * original value
ADD HL, DE  ; HL = 50 * original value
ADD HL, DE  ; HL = 60 * original value

; 最後にHLを2倍して120倍にする
ADD HL, HL  ; 最終的にHL = 120 * original value

LD	E,C
LD	D,0
ADD	HL,DE
LD	DE,VRMTOP
ADD	HL,DE
POP	DE
POP	AF
POP	BC
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


