; Converted from lib/libdef/libzxn_input.yml
; SLANG Runtime Library (new format)

; @name INKEY
; @resident shared
; @calls GETC
; 　入力されたキーの値を返す。
; 　　　n=0のときS-OSの#GETKYと同じ → リアルタイム入力。入力されたものをA。されてなければ0。
; 　　　n=1のときS-OSの#FLGETと同じ → カーソル位置でカーソル点滅1文字入力。Aに返す。オートリピートかかる。
; 　　　その他のときS-OSの#INKEYと同じ → 何かを押すまでキー待ち。押された文字がAに。
LD A,L
CP 1
JR NC,.inkey1
CALL GETC
JR .inkey_end

.inkey1
JR NZ,.inkey2
CALL .ZXFLGET
JR .inkey_end

.inkey2
CALL .ZXINKEY

.inkey_end
LD L,A
LD H,0
RET

; カーソル出すの面倒なのでn>=1の場合は同じ処理(キー入力待ち)
.ZXINKEY
.ZXFLGET

.waitkey
CALL GETC
CP 0
JR Z,.waitkey
RET


; @name LINPUT
; @resident shared
; 未実装
; LINPUT(格納アドレス, 長さ)
; コールし た時点のカーソル以降を続み込むほかはGETLlN関数と同じ。
RET


; @name GETL
; @resident shared
; 未実装
; HL 格納アドレス
; キーボードから1行入力し，格納アドレスに格納し，行の長さを返す。
; BREAKキーが押された場合は-1を返す。行の最後は0となる。
RET


; @name GETLIN
; @resident shared
; 未実装
; GETLlN (格納アドレス, 長さ)
; 1行の最大長を指定できるほかは，GETL関数と同じ。オーバーした分は無視される。
RET


; @name INPUT
; @resident shared
; 未実装
; キーボードから入力された数値を返す。先頭に$を付けると，16進数とみなす。
; コールした時点のカーソル以降を読み込み，正常な入力が行われた場合は^CARRY=0，
; BREAKキーが押されたり誤入力があった場合は^CARRY=1となる。
RET


; @name GETC
; @resident shared
; @calls ZXNWORK,ZXNCALLS
call KEY_SCAN
jp nz, .EMPTY_INKEY

call KEY_TEST
jp nc, .EMPTY_INKEY

dec d	; D is expected to be FLAGS so set bit 3 $FF
; 'L' Mode so no keywords.
ld e, a	; main key to A
; C is MODE 0 'KLC' from above still.
call KEY_CODE ; routine K-DECODE

ld l,a
ret

.EMPTY_INKEY
xor a
ld l,a
ret


; @name GETKEY
; @resident shared
LD A,L
CP 8
JR NC,.extendkey

; normal keymap
LD E,L
LD D,0
LD HL,.keyport
ADD HL,DE
LD B,(HL)
LD C,$FE
IN A,(C)

LD H,0
LD L,A
RET

.extendkey
ADD A,$B0-8
LD BC,$243B
OUT (C),A

INC B
IN A,(C)
CPL

LD H,0
LD L,A
RET

.keyport
DB $7F, $BF, $DF, $EF, $F7, $FB, $FD, $FE

