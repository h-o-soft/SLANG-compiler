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
; @param_count 2
; @calls GETLIN
; LINPUT(格納アドレス, 長さ)。ほかの機械では画面のカーソルの桁から後ろを読む。Next には画面エディタが無いので GETLIN と同じ
JP GETLIN


; @name GETL
; @resident shared
; @param_count 1
; @calls ZXN_GETLINE
; GETL(格納アドレス): キーボードから 1 行 (255 字まで) を読んで格納アドレスに置き、行の長さを返す。行の終わりは 0。
; EDIT (CAPS SHIFT + 1) で取り消すと、格納アドレスに 1BH を置いて -1 を返す
LD C,0
JP ZXN_GETLINE


; @name GETLIN
; @resident shared
; @param_count 2
; @calls ZXN_GETLINE
; GETLIN(格納アドレス, 長さ): 長さの字までのほかは GETL と同じ。それより多くは受けない。長さが 0 か 255 より大きければ 255
LD A,D
OR A
JR Z,.n
LD E,0              ; 255 より大きい: ZXN_GETLINE の 0 (255 字)
.n
LD C,E
JP ZXN_GETLINE


; @name ZXN_GETLINE
; @resident shared
; @calls GETC,PRT,ZXN_SCROLL,ZXNWORK
; HL = 置き場、C = 最大の字数 (0 なら 255)。カーソルの所から 1 行を読む。HL = 長さ (取り消しなら -1)。
; 打った字は PRT で出す。カーソルの印は _。キーは離すのを待ってから次を読む (GETC を回して見る。HALT は使わず、割り込みの状態も変えない)。
; 20H〜7EH を受ける。DELETE (0CH) で 1 字戻す。ENTER (0DH) で終わり、EDIT (07H) で取り消す。どちらも改行を出す
LD A,C
OR A
JR NZ,.max
DEC C
.max
EX DE,HL            ; DE = 次に置く所
LD B,0              ; B = 字の数
.loop
CALL ZXN_SCROLL     ; 一番下を越えていたら、ここで流す (印を描くことで流れないように)
LD A,'_'
CALL .mark
.release
CALL .getc
JR NZ,.release
.press
CALL .getc
JR Z,.press
CP $0D
JR Z,.enter
CP $0C
JR Z,.delete
CP $07
JR Z,.cancel
CP $20
JR C,.loop
CP $7F
JR NC,.loop
LD L,A
LD A,B
CP C
JR NC,.loop         ; もう置けない
LD A,L
LD (DE),A
INC DE
INC B
CALL PRT            ; 印の上に字を書いて進む
JR .loop
.delete
LD A,B
OR A
JR Z,.loop
DEC B
DEC DE
LD A,' '
CALL .mark
LD A,(LOCX)         ; 1 桁戻る (0 なら前の行の 31 桁)
OR A
JR NZ,.back
LD A,(LOCY)
DEC A
LD (LOCY),A
LD A,32
.back
DEC A
LD (LOCX),A
JR .loop
.enter
LD A,' '
CALL .mark
XOR A
LD (DE),A
LD A,$0D
CALL PRT
LD L,B
LD H,0
RET
.cancel
LD A,' '
CALL .mark
LD A,E              ; 置き場の頭 = DE - B
SUB B
LD L,A
LD A,D
SBC A,0
LD H,A
LD (HL),$1B
INC HL
LD (HL),0
LD A,$0D
CALL PRT
LD HL,$FFFF
RET
; A をカーソルの所に描き、カーソルは動かさない
.mark
LD HL,(LOCX)
PUSH HL
CALL PRT
POP HL
LD (LOCX),HL
RET
; GETC (BC・DE を残す)。A = 字、0 なら Z
.getc
PUSH BC
PUSH DE
CALL GETC
POP DE
POP BC
OR A
RET


; @name INPUT
; @resident shared
; @param_count 0
; @calls ZXN_GETLINE
; @works ZXN_INBUF:17
; キーボードから 15 字までの 1 行を読んで数にする。前の空白を飛ばし、先頭の - で負、$ の後は 16 進。
; 数になれば ^CARRY = 0、ならなければ (取り消しも) ^CARRY = 1 で 0 を返す。数の後ろの字は見ない
LD HL,0
LD (_CARRY),HL
LD HL,ZXN_INBUF
LD C,15
CALL ZXN_GETLINE
LD DE,ZXN_INBUF
.skip
LD A,(DE)
CP ' '
JR NZ,.sign
INC DE
JR .skip
.sign
LD HL,ZXN_INBUF+16  ; 負の印 (置き場の 17 バイト目)
LD (HL),0
CP '-'
JR NZ,.number
LD (HL),1
INC DE
LD A,(DE)
.number
LD HL,0
CP '$'
JR Z,.hex
CALL .digit
JR C,.bad
.dec
ADD HL,HL
LD B,H
LD C,L
ADD HL,HL
ADD HL,HL
ADD HL,BC
LD C,A
LD B,0
ADD HL,BC
INC DE
LD A,(DE)
CALL .digit
JR NC,.dec
JR .end
.hex
INC DE
LD A,(DE)
CALL .hexdigit
JR C,.bad
.hexnext
ADD HL,HL
ADD HL,HL
ADD HL,HL
ADD HL,HL
OR L
LD L,A
INC DE
LD A,(DE)
CALL .hexdigit
JR NC,.hexnext
.end
LD A,(ZXN_INBUF+16)
OR A
RET Z
XOR A
SUB L
LD L,A
SBC A,A
SUB H
LD H,A
RET
.bad
LD HL,1
LD (_CARRY),HL
DEC HL
RET
; A = 字 → 0〜9 (キャリーが立てば数字でない)
.digit
SUB '0'
RET C
CP 10
CCF
RET
; A = 字 → 0〜15 (キャリーが立てば 16 進の字でない。小文字も)
.hexdigit
CP 'a'
JR C,.upper
SUB $20
.upper
SUB '0'
RET C
CP 10
CCF
RET NC
SUB 7
CP 10
RET C
CP 16
CCF
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
; C は KEY_SCAN が入れた FEH のまま (KEY_CODE の DEC C で負になり、K/L/C の道へ)
; KEY_CODE は字のキーで BIT 3,(IY+$30) (ROM の FLAGS2 の CAPS LOCK) を見る。SLANG の IY は局所の変数の場所なので、
; 呼ぶ間だけ 0 のバイトを指させる (いつも CAPS LOCK が切れているときの値。GETC__NOROM と同じ)
push iy
ld iy, .caps_off-$30
call KEY_CODE ; routine K-DECODE
pop iy

ld l,a
ret

.EMPTY_INKEY
xor a
ld l,a
ret

.caps_off
db 0


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

