; Converted from /home/user/SLANG-compiler/lib/libdef/libx1_grp.yml
; SLANG Runtime Library (new format)

; @name MSINIT
; @calls SETUPCTC
; @lib X1MOUSE
; @extlib x1/mouse.asm:MSINIT

; @name MSGET
; @lib X1MOUSE
; @extlib x1/mouse.asm:MSGET

; @name PAINT1
; @lib X1PAINT
; @extlib x1/gpaint.asm:PAINT

; @name PAINT
; @calls PAINT1
; @lib X1PAINT
JP  X1PAINT.PAINTAUTO

; @name PAINT2
; @calls PAINT1
PUSH BC
PUSH DE
PUSH HL
EX DE,HL
LD A,L
JP X1PAINT.GPAINT_TOP


; @name SET_PAINTBUF
; @calls PAINTSLOW
; @lib X1PAINT
; @extlib x1/gpaint.asm:SETPAINTBUF

; @name BFILL
; @calls PAINT1
; @lib X1PAINT
; @extlib x1/gpaint.asm:BFILL

; @name LINECOMMON
; @lib X1GLINE
; @extlib x1/gline.asm:X1GLINE

; @name LINE
; @calls LINECOMMON,X1WORK
; @extlib x1/gline.asm:LINEALL

; @name XLINE
; @calls LINECOMMON,LINE,X1WORK
; @extlib x1/gline.asm:XORLINE

; @name GRPSETUP
; @calls PAINT1,LINE
; LINE SETUP
; set to 640 or 320
LD A,(AT_WIDTH)   ; 40 or 80
CP 40
JR Z,.line320
CALL X1GLINE.SET640
JR .skip
.line320
CALL X1GLINE.SET320
.skip

; PAINT SETUP
JP	X1PAINT.WIDTHPATCH


