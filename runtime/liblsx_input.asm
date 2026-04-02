; Converted from lib/libdef/liblsx_input.yml
; SLANG Runtime Library (new format)

; @name INKEY
; @param_count 1
; @calls sGETKY,sFLGET,sINKEY
LD A,L
CP 1
JR NC,.inkey1
CALL sGETKY
JR .inkey_end
.inkey1
JR NZ,.inkey2
CALL sFLGET
JR .inkey_end
.inkey2
CALL sINKEY
.inkey_end
LD L,A
LD H,0
RET


; @name LINPUT
; @param_count 2
; @calls sCSR,GETL
PUSH HL
CALL sCSR
LD D,L
POP HL
JR GETLPROC


; @name GETL
; @param_count 1
; @calls GETLIN
LD E,0


; @name GETLIN
; @param_count 2
; @calls sGETL,sWORK
LD D,0
GETLPROC:
PUSH DE
LD DE,sKBFAD
CALL sGETL
POP BC
LD A,(DE)
CP $1B
JR NZ, .getlin1
LD (HL),A
LD HL,$FFFF
RET
.getlin1
INC B
DEC B
JR Z,.getlin2
LD A,(DE)
OR A
JR Z,.getlin2
INC DE
DEC B
JR .getlin1
.getlin2
LD B,0
.getlin4
LD A,(DE)
INC DE
OR A
JR Z,.getlin3
LD (HL),A
INC HL
INC B
DEC C
JR NZ,.getlin4
.getlin3
LD (HL),0
LD L,B
LD H,0
RET


; @name INPUT
; @param_count 0
; @calls sWORK,LINPUT,sHLHEX,ADECI
LD BC,0
LD (_CARRY),BC
LD HL,sKBFAD
LD DE,0
CALL LINPUT
LD DE,sKBFAD
.linput2
LD A,(DE)
CP $20
JR NZ, .input1
INC DE
JR .linput2
.input1
LD A,(DE)
CP $24
JR NZ,.input3
INC DE
CALL sHLHEX
JR C,.input4
RET
.input3
LD HL,0
LD A,(DE)
CALL ADECI
JR C,.input4
.input5
ADD HL,HL
LD B,H
LD C,L
ADD HL,HL
ADD HL,HL
ADD HL,BC
LD B,0
LD C,A
ADD HL,BC
INC DE
LD A, (DE)
CALL ADECI
JR NC, .input5
RET
.input4
LD BC,1
LD (_CARRY),BC
LD HL,0
RET


; @name ADECI
; @param_count 0
SUB $30
RET C
CP $0A
CCF
RET


