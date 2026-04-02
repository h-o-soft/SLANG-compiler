; Converted from lib/libdef/libmsxrom_input.yml
; SLANG Runtime Library (new format)

; @name STICK
; @calls MSXCALLS
LD A,L
CALL GTSTCK
LD L,A
LD H,0
RET


; @name STICK2
; @calls MSXCALLS
LD A,L
CALL GTSTCK
LD HL,STICK_TBL
LD E,A
LD D,0
ADD HL,DE
LD A,(HL)
LD L,A
LD H,0
RET
STICK_TBL:
DB 00H
DB 01H
DB 03H
DB 02H
DB 06H
DB 04H
DB 0CH
DB 08H
DB 09H


