; Converted from /home/user/SLANG-compiler/lib/libdef/libzxn_base.yml
; SLANG Runtime Library (new format)

; @name ZXNCALLS
DUMMY   EQU $0000
KEY_SCAN	EQU $028E
KEY_TEST	EQU $031E
KEY_CODE	EQU $0333
DOS_OPEN EQU $0106
DOS_CLOSE EQU $0109
DOS_GET_EOF EQU $0139


; @name SLANGINIT
; @calls ZXNWORK,ZXNCALLS
; im 1
di

; test
LD SP,$E000

; WORK ZERO CLEAR
XOR A
LD HL,__WORK__
LD DE,__WORK__+1
LD BC,__WORKEND__-__WORK__-1
LD (HL),A
LDIR

<<CALLINITIALIZER>>

LD IY,__IYWORK

; di
ei
call MAIN

di
halt

INFLOOP:
JP INFLOOP


; @name STOP
; @param_count 0
JP INFLOOP


; @name ZXN_READ_REG
; @param_count 1
; L = NEXTREG register
; LD H,0
LD A,L
LD BC,$243B
OUT (C),A

INC B
IN A,(C)
LD L,A
RET


; @name ZXN_WRITE_REG
; @param_count 2
LD A,L
LD BC,$243B
OUT (C),A

LD A,E
INC B
OUT (C),A

; ; 自己書き換え出来る場合はこちら
; ; HL = NEXTREG register, DE = Value
; ; register
; LD A,L
; LD (.nextreg_port),A

; ; value
; LD A,E

; ; NEXTREG r,A
; DB $ED,$92
; .nextreg_port
; DB 0
RET


; @name ZXN_SET_BANK_8K
; HL = mmu(0〜7)
; DE = page
LD A,$50
ADD A,L
LD BC,$243B
OUT (C),A

INC B
IN A,(C)
LD L,A

OUT (C),E

; RESULT HL -> old bank
RET


; @name ZXN_SET_BANK_16K
LD A,$50
ADD A,L
LD D,A    ; register number
LD BC,$243B
OUT (C),A

INC B
IN A,(C)
LD L,A

; first 8k bank write(MMU(n) = page)
SLA E
LD A,E
OUT (C),A

; next 8k bank write(MMU(n + 1) = page + 1)
DEC B
LD A,D  ; register number
INC A
OUT (C),A

INC B
INC E
LD A,E
LD BC,$253B
OUT (C),A

RET


; @name ZXN_BANK_SET_ESX
; 最初の16kをROMにリセット
; NEXTREG $50,$FF
DB $ED,$91,$50,$FF
; NEXTREG $51,$FF
DB $ED,$91,$51,$FF
RET


; @name SET_CPU_SPEED
; @calls ZXN_WRITE_REG
; HL = 0 = 3.5MHz / 1 = 7MHz / 2 = 14MHz / 3 = 28MHz
EX DE,HL
LD HL,$07
JP ZXN_WRITE_REG


; @name ZXN_ULA_SET_SHADOW
; HL = 0 -> not shadow(bank 5) / 1 -> shadow(bank 7)
SLA L
SLA L
SLA L
LD A,L
LD BC,$7FFD
OUT (C),A
RET


; @name ULA_VISIBLE
; HL 0 = invisible / 1 = visible
; 0 <-> 1
LD A,L
XOR 1
LD L,A
LD A,(ULA_CTRL)
AND $7F
RRC L
OR L
LD (ULA_CTRL),A

DB $ED,$92,$68
RET


; @name SET_PAL
; HL = Selects palette for read or write($43)
; DE = IDX
; BC = COLOR
LD A,(EULA_CTRL)
AND $8F
SLA L
SLA L
SLA L
SLA L
OR L
LD (EULA_CTRL),A
; NEXTREG $43,A
DB $ED,$92,$43
LD A,E
; NEXTREG $40,A
DB $ED,$92,$40
LD A,C
; NEXTREG $41,A
DB $ED,$92,$41

RET


; @name SET_PALALL
; HL = Selects palette for read or write($43)
; DE = COLOR Address
LD A,(EULA_CTRL)
AND $8F
SLA L
SLA L
SLA L
SLA L
OR L
LD (EULA_CTRL),A
; NEXTREG $43,A
DB $ED,$92,$43
; NEXTREG $40,0
DB $ED,$91,$40,$00
EX DE,HL
LD B,255
.copy8bitpal
LD A,(HL)
INC HL
; NEXTREG $41,A
DB $ED,$92,$41
DJNZ .copy8bitpal

RET


; @name SET_PAL9
; HL = Selects palette for read or write($43)
; DE = IDX
; BC = COLOR(9bits)
LD A,(EULA_CTRL)
AND $0F
SLA L
SLA L
SLA L
SLA L
OR L
LD (EULA_CTRL),A
; NEXTREG $43,A
DB $ED,$92,$43
LD A,E
; NEXTREG $40,A
DB $ED,$92,$40
LD A,C
; NEXTREG $44,A
DB $ED,$92,$44
LD A,B
; NEXTREG $44,A
DB $ED,$92,$44

RET


; @name SET_PAL9ALL
; HL = Selects palette for read or write($43)
; DE = COLOR Address
LD A,(EULA_CTRL)
AND $0F
SLA L
SLA L
SLA L
SLA L
OR L
LD (EULA_CTRL),A
; NEXTREG $43,A
DB $ED,$92,$43
; NEXTREG $40,0
DB $ED,$91,$40,$00
EX DE,HL
LD B,255
.copy9bitpal
LD A,(HL)
INC HL
; NEXTREG $44,A
DB $ED,$92,$44
LD A,(HL)
INC HL
; NEXTREG $44,A
DB $ED,$92,$44
DJNZ .copy9bitpal

RET


; @name L2_SCREEN
; HL 0 = 256x192 / 1 = 320x256 / 2 = 640x256(4bpp)
SLA L
SLA L
SLA L
SLA L
LD A,L
DB $ED,$92,$70
RET


; @name L2_VISIBLE
; @calls ZXNWORK
; HL 0 = invisible / 1 = visible
LD A,(L2_ACCESS)
AND $FD
SLA L ; layer 2 visible?
OR L
LD (L2_ACCESS),A
LD BC,$123B
OUT (C),A
RET


; @name L2_SETRAM
LD A,L
DB $ED,$92,$12
RET


; @name L2_SETRAMSHADOW
LD A,L
DB $ED,$92,$13
RET


; @name L2_TRANSPARENCY
LD A,L
DB $ED,$92,$14
RET


; @name L2_OFFSET
; HL = X Offset
; DE = Y Offset
LD A,L
DB $ED,$92,$16

; 320x256 or 640x256 mode only
LD A,H
DB $ED,$92,$71

; Y Offset
LD A,E
DB $ED,$92,$17
RET


; @name L2_CLIPWINDOW
; @calls ZXNWORK
; スタックには近い順に(RETADR),Y2,Y1,X2,X1が積まれている
LD (SPTMP),SP
LD IX,(SPTMP)

; LにX1を入れる
LD L,(IX+8)
; HにX2を入れる
LD H,(IX+6)
; DにY1を入れる
LD D,(IX+4)
; EにY2を入れる
LD E,(IX+2)

; reset Layer 2 clip-window register index
DB $ED,$91,$1C,$01

; X1
LD A,L
DB $ED,$92,$18
; X2
LD A,H
DB $ED,$92,$18
; Y1
LD A,D
DB $ED,$92,$18
; Y2
LD A,E
DB $ED,$92,$18

; 戻りアドレスを戻す
LD SP,IX
RET


; @name TILE_INIT
; HL 0 = 40x32 / 1 = 80x32
; DE 0 = 2byte / 1 = 1byte
LD A,(TILE_CTRL)
AND $1F
OR $80
RRC L
RRC L
RRC E
RRC E
RRC E
OR L
OR E
LD (TILE_CTRL),A
; NEXTREG $6B,A
DB $ED,$92,$6B
RET


; @name TILE_VISIBLE
; HL 0 = Invisible / 1 = Visible
LD A,(TILE_CTRL)
AND $7F
RRC L
OR L
LD (TILE_CTRL),A
; NEXTREG $6B,A
DB $ED,$92,$6B
RET


; @name TILE_GLOBALATR
; HL = Default Tilemap Attribute
LD A,L
DB $ED,$92,$6C
RET


; @name TILE_SETADR
; HL = tilemap address
; DE = tile address

; Set Tilemap offset
LD A,L
DB $ED,$92,$6E

; Set Tile offset
LD A,E
DB $ED,$92,$6F

RET


; @name TILE_CLIP
; @calls ZXNWORK
; スタックには近い順に(RETADR),Y2,Y1,X2,X1が積まれている
LD (SPTMP),SP
LD IX,(SPTMP)

; LにX1を入れる
LD L,(IX+8)
; HにX2を入れる
LD H,(IX+6)
; DにY1を入れる
LD D,(IX+4)
; EにY2を入れる
LD E,(IX+2)

; reset Tilemap clip-window register index
DB $ED,$91,$1C,$8

; X1
LD A,L
DB $ED,$92,$1B
; X2
LD A,H
DB $ED,$92,$1B
; Y1
LD A,D
DB $ED,$92,$1B
; Y2
LD A,E
DB $ED,$92,$1B

; 戻りアドレスを戻す
LD SP,IX
RET


; @name TILE_OFFSET
; HL = X Offset
; DE = Y Offset

; X Offset LSB
LD A,L
DB $ED,$92,$30

; X Offset MSB
LD A,H
DB $ED,$92,$2F

; Y Offset
LD A,E
DB $ED,$92,$31
RET


; @name TILE_DEFS
; HL = Index
; DE = Tile Address
; BC = Tile Count
; Read Tile Definitions Base Address
PUSH BC

; HL = HL * 32
SLA L
RL H
SLA L
RL H
SLA L
RL H
SLA L
RL H
SLA L
RL H

LD A,$6F
LD BC,$243B
OUT (C),A

INC B
IN A,(C)
ADD A,$40
ADD A,H
LD H,A

EX DE,HL

POP BC
; BC = BC * 32
SLA C
RL B
SLA C
RL B
SLA C
RL B
SLA C
RL B
SLA C
RL B

LDIR

RET


; @name TILE_DEF
; HL Tile Index
; DE Tile Data Address

; HL = HL * 32
SLA L
RL H
SLA L
RL H
SLA L
RL H
SLA L
RL H
SLA L
RL H

; Read Tile Definitions Base Address
LD A,$6F
LD BC,$243B
OUT (C),A

INC B
IN A,(C)
ADD A,$40
ADD A,H
LD H,A

EX DE,HL
; HL = Tile Data Address
; DE = tile def address
LD BC,32
LDIR

RET


; @name TILE_SETMAP
; @calls MULHLDE
; HL = X
; DE = Y
; BC = tile value
PUSH BC
PUSH HL

LD A,(TILE_CTRL)

; bit 6 : 0=40x32 / 1 = 80x32
BIT 6,A
JR Z,.t40
; 80
LD HL,80
JR .mulproc
.t40
LD HL,40
.mulproc
CALL MULHLDE
; HL = Y * (40 or 80)
POP DE
ADD HL,DE
; HL = X + Y * (40 or 80)

LD A,(TILE_CTRL)
BIT 5,A
JR NZ,.tile1
SLA L
RL H
.tile1
PUSH AF

; Read Tilemap Base Address
LD A,$6E
LD BC,$243B
OUT (C),A

INC B
IN A,(C)
ADD A,$40
ADD A,H
LD H,A

POP AF
POP DE

LD (HL),E
BIT 5,A
JR NZ,.tilew1
INC HL
LD (HL),D
.tilew1

RET


; @name LAYER_PRIORITY
; HL
; 0 S L U
; 1 L S U
; 2 S U L
; 3 L U S
; 4 U S L
; 5 U L S
; 6 (U|T)S(T|U)(B+L)
; 7 (U|T)S(T|U)(B+L-5)
LD A,(SPL_SYS)
AND $E3
SLA L
SLA L
OR L
LD (SPL_SYS),A

DB $ED,$92,$15
RET


; @name SPR_LOAD
; HL = index
; DE = address
; BC = size
LD (.dmaSource),DE
LD (.dmaLength),BC
LD A,L
LD BC,$303B
OUT (C),A
LD HL,.dmaProgram
LD B,.dmaProgramLength
LD C,$6B
OTIR
RET
.dmaProgram
DB %10000011
DB %01111101
.dmaSource
DW 0
.dmaLength
DW 0
DB %00010100
DB %00101000
DB %10101101
DW $005B
DB %10000010
DB %11001111
DB %10000111
.dmaProgramLength EQU $ - .dmaProgram


; @name SPR_VISIBLE
; @calls ZXNWORK
LD A,(SPL_SYS)
AND $FE
OR L
LD (SPL_SYS),A
DB $ED,$92,$15
RET


; @name SPR_SETID
; HL = Sprite Id
LD A,L
; NEXTREG $34,A
DB $ED,$92,$34
RET


; @name SPR_SET
;
; スタックには近い順に(RETADR)PAT,Y,X,IDXが積まれている
; PatはAttribute 2が上位、Attribute 3が下位に来た値であるが、特例としてX座標の最上位ビットはX側が使われる
LD (SPTMP),SP
LD IX,(SPTMP)

; Sprite Index
LD A,(IX+8)
; NEXTREG $34,A
DB $ED,$92,$34

; X
LD A,(IX+6)
DB $ED,$92,$35
; Y
LD A,(IX+4)
DB $ED,$92,$36

; Attribute 2
LD A,(IX+7) ; X 9bit
AND 1
LD L,(IX+3)
DB $ED,$92,$37

; Attribute 3
LD A,(IX+2)
OR $80      ; Sprite Visible
DB $ED,$92,$38

; 戻りアドレスを戻す
LD SP,IX
RET


; @name SPR_MOVE
; HL = (low)spr num / (high)Attribute 2(7-4 pal offset, 3 flip X, 2 flip Y, 1 rotate)
; DE = X
; BC = Y
LD A,L
DB $ED,$92,$34

; Low X
LD A,E
DB $ED,$92,$35

; Y
LD A,C
DB $ED,$92,$36

; High X
LD A,D
AND 1
OR H
DB $ED,$92,$37

RET


; @name SPR_STARTANCHOR
; SPR_SETの直後に呼び出すとSPR_SETしたスプライトが親スプライトになる
; SPR_SETの第四引数には$4000をORする事
LD A,$20
OR L
DB $ED,$92,$79
RET


; @name SPR_SETREL
; HL = X
; DE = Y
; C  = PAT
; B  = Attribute 3
; X
LD A,L
DB $ED,$92,$35

; Y
LD A,E
DB $ED,$92,$36

; Attribute 2
LD A,H  ; X 9bit
AND 1
LD L,B
DB $ED,$92,$37

; Attribute 3
LD A,C
OR $C0      ; Sprite Visible / use attribute 4
DB $ED,$92,$38

; Relative Sprite / ID Increment
; NEXTREG $79,$40
DB $ED,$91,$79,$40

RET


; @name SPR_SCALE
; HL = (low)spr num  (high)7-6 H+N6(H->4bit sprite)
; DE = X scale factor(1x,2x,4x,8x)
; BC = Y scale factor(1x,2x,4x,8x)
LD A,L
DB $ED,$92,$34

; Attribute
SLA E
SLA E
SLA E
SLA C
XOR A
LD A,E
OR C
OR H
DB $ED,$92,$39

RET


; @name SPR_CLIP
; @calls ZXNWORK
; スタックには近い順に(RETADR),Y2,Y1,X2,X1が積まれている
LD (SPTMP),SP
LD IX,(SPTMP)

; LにX1を入れる
LD L,(IX+8)
; HにX2を入れる
LD H,(IX+6)
; DにY1を入れる
LD D,(IX+4)
; EにY2を入れる
LD E,(IX+2)

; reset Tilemap clip-window register index
DB $ED,$91,$1C,$2

; X1
LD A,L
DB $ED,$92,$19
; X2
LD A,H
DB $ED,$92,$19
; Y1
LD A,D
DB $ED,$92,$19
; Y2
LD A,E
DB $ED,$92,$19

; 戻りアドレスを戻す
LD SP,IX
RET


; @name SPR_HIDE
; HL Sprite Num
LD A,L
DB $ED,$92,$34

; Attribute 3
XOR A
DB $ED,$92,$38


; @name STICK
; HL = 0 (JOYSTICK 1) or 1 (JOYSTICK2)
LD A,L
CP 1
JR .joy1
LD A,$37-$1F
.joy1
ADD A,$1F
LD C,A
LD B,0
IN A,(C)
LD L,A
LD H,0
RET


; @name COPPER_SET
; HL = Copper Instructions Address
; DE = Copper Size

; NEXTREG $61,0
DB $ED, $91, $61, $00
; NEXTREG $62,0
DB $ED, $91, $62, $00

LD B,E
.nextByte
LD A,(HL)
; NEXTREG $63,A
DB $ED,$92,$63
INC HL
DJNZ .nextByte

; NEXTREG $61,0
DB $ED, $91, $61, $00
; NEXTREG $62,$C0
DB $ED, $91, $62, $C0

RET


; @name ZXN_VSYNC
HALT
RET


; @name ZXN_SETIM2
; @calls VSYNC_JP
; HL = InterrultHandler
; DE = ($C5)($C4)   ; (CTC channel interrupts)(INT and ULA)
;  C = ($C6)        ; UART interrupts
DI
; NEXTREG $C0, (L & %11100000) | %00000001
LD A,L
AND %11100000
OR %00000001
DB $ED,$92,$C0
LD A,E
DB $ED,$92,$C4
LD A,D
DB $ED,$92,$C5
LD A,C
DB $ED,$92,$C6

LD A,H
LD I,A

IM 2
EI
RET


; @name VSYNC_JP
JP !VSYNC_PROC


; @name ZXNWORK
; @param_count 0
;


