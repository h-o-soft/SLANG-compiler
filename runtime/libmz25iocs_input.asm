; SLANG Runtime Library for MZ-2500 IOCS keyboard input
;
; Minimal input library for IOCS based targets.
; Only INKEY is implemented at this stage.

; @name INKEY
; @resident shared
; @param_count 1
; @calls MZ25_IOCS_INKEY
; MZ-2500 IOCS INKEY.
;   n = 0 : one IOCS call, return immediately if IOCS returns 0.
;   n = 1 : wait for one key. Cursor blink is not emulated.
;   n > 1 : wait for one key.
LD A,L
CP 1
JR NC,.inkey_wait
CALL MZ25_IOCS_INKEY
JR .inkey_end

.inkey_wait
CALL MZ25_IOCS_INKEY
OR A
JR Z,.inkey_wait

.inkey_end
LD L,A
LD H,0
RET


; @name LINPUT
; @resident shared
; Not implemented. Return $FFFF (= ESC cancelled) like libsos_input LINPUT.
LD HL,$FFFF
RET


; @name GETL
; @resident shared
; Not implemented. Return $FFFF (= ESC cancelled) like libsos_input GETL.
LD HL,$FFFF
RET


; @name GETLIN
; @resident shared
; Not implemented. Return $FFFF (= ESC cancelled) like libsos_input GETLIN.
LD HL,$FFFF
RET


; @name INPUT
; @resident shared
; Not implemented. Set CARRY = 1 and return 0 like libsos_input INPUT failure path.
LD BC,1
LD (_CARRY),BC
LD HL,0
RET


; @name MZ25_IOCS_INKEY
; @resident shared
RST 18H
DB  0DH
RET
