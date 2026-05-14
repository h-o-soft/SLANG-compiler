; SLANG Runtime Library for MZ-2500 IOCS keyboard input
;
; Input library for IOCS based targets.
; INKEY uses SVC_INKEY. GETL/GETLIN/LINPUT use SVC_GETL, which reads
; a line into the IOCS internal buffer and returns that buffer address in DE.
; SVC_GETL returns the whole text line from column 0. LINPUT and INPUT skip
; the cursor X position ($05E2), while GETL/GETLIN keep the column-0 behavior.


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
; @param_count 2
; @calls GETLIN
; LINPUT(buffer, length). Read one line from the current cursor position.
; IOCS GETL returns from column 0, so D keeps the pre-call cursor X skip count.
LD A,($05E2)
LD D,A
JR GETLPROC


; @name GETL
; @resident shared
; @param_count 1
; @calls GETLIN
; GETL(buffer). Read one line from column 0. Return length.
LD E,0


; @name GETLIN
; @resident shared
; @param_count 2
; @calls MZ25_IOCS_GETL
; GETLIN(buffer, length). Read one line from column 0 and copy at most length bytes.
; Return $FFFF on SHIFT+BREAK/Cy or ESC.
LD D,0
GETLPROC:
PUSH HL             ; destination buffer
PUSH DE             ; D=skip count, E=max length (0 means compatible 256-byte wrap)
CALL MZ25_IOCS_GETL ; DE = IOCS internal input buffer, Cy = SHIFT+BREAK
POP BC              ; B=skip count, C=max length
POP HL              ; destination buffer
JR C,.getl_cancel
LD A,(DE)
CP $1B
JR NZ,.getlin1
LD (HL),A
.getl_cancel
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
; @resident shared
; @param_count 0
; @calls LINPUT,ADECI,MZ25_HLHEX
; Read a line, then parse a decimal number or a $-prefixed hexadecimal number.
; On success, return the value and set _CARRY=0. On cancel/error, return 0 and
; set _CARRY=1.
LD BC,0
LD (_CARRY),BC
LD HL,MZ25_INPUT_BUF
LD DE,0
CALL LINPUT
LD DE,$FFFF
OR A
SBC HL,DE
JR Z,.input4
LD DE,MZ25_INPUT_BUF
.linput2
LD A,(DE)
CP $20
JR NZ,.input1
INC DE
JR .linput2
.input1
LD A,(DE)
CP $24
JR NZ,.input3
INC DE
CALL MZ25_HLHEX
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
LD A,(DE)
CALL ADECI
JR NC,.input5
RET
.input4
LD BC,1
LD (_CARRY),BC
LD HL,0
RET

MZ25_INPUT_BUF:
DS 256

; @name MZ25_HLHEX
; @resident shared
; @param_count 0
; @calls MZ25_HEXVAL
; Parse a $-prefixed input body. DE points to the first hexadecimal digit.
; Returns HL=value with Cy clear. Cy is set if no hexadecimal digit exists.
LD HL,0
LD B,0
.mz25_hlhex_loop
LD A,(DE)
CALL MZ25_HEXVAL
JR C,.mz25_hlhex_done
ADD HL,HL
ADD HL,HL
ADD HL,HL
ADD HL,HL
LD C,A
LD A,L
OR C
LD L,A
INC DE
INC B
JR .mz25_hlhex_loop
.mz25_hlhex_done
LD A,B
OR A
JR NZ,.mz25_hlhex_ok
SCF
RET
.mz25_hlhex_ok
OR A
RET


; @name MZ25_HEXVAL
; @resident shared
; @param_count 0
; Convert A from ASCII hexadecimal to 0..15. Cy is set on non-hex input.
CP $30
RET C
CP $3A
JR C,.mz25_hex_digit
CP $41
JR C,.mz25_hex_lower_check
CP $47
JR C,.mz25_hex_upper
.mz25_hex_lower_check
CP $61
JR C,.mz25_hex_invalid
CP $67
JR C,.mz25_hex_lower
.mz25_hex_invalid
SCF
RET
.mz25_hex_digit
SUB $30
OR A
RET
.mz25_hex_upper
SUB $37
OR A
RET
.mz25_hex_lower
SUB $57
OR A
RET

; @name ADECI
; @resident shared
; @param_count 0
SUB $30
RET C
CP $0A
CCF
RET


; @name MZ25_IOCS_INKEY
; @resident shared
RST 18H
DB  0DH
RET


; @name MZ25_IOCS_GETL
; @resident shared
RST 18H
DB  0CH
RET
