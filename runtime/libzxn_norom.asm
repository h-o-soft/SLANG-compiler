; SLANG Runtime Library (new format)
; zxn の ROM に頼らない部品。ソースの大域の CONST __NOROM = 1; のとき、slangc が X の中身を X__NOROM のものに差し替える
; (名前と出力の順は X のまま。env の norom)。CONST が無ければ、ここの断片はどこからも呼ばれず、出力に入らない。
; 字の形 (ZXN_FONT) は LSX-Dodgers Next の移植で作った字 (H.O SOFT。LICENSE・THIRD_PARTY_NOTICES.md)。


; @name PRT__NOROM
; @resident shared
; @param_count 1
; @calls ZXNWORK,ZXN_FONT
; PRT と同じ動き。画面の番地は素の Z80 で、字の形は ZXN_FONT から (ROM の 3D00H と Z80N の PIXELAD を使わない)
PUSH HL
; PSTR は回数を DE に持って呼ぶので、DE・BC も残す
PUSH DE
PUSH BC
CP $0D
JR Z,.NEXTLINE

; 字の形の番地 DE = ZXN_FONT + (字 - 32) * 8
SUB 32
LD L,A
LD H,0
ADD HL,HL
ADD HL,HL
ADD HL,HL
LD DE,ZXN_FONT
ADD HL,DE
EX DE,HL
; 画面の番地 HL: H = 40H | (行 AND 18H)、L = ((行 AND 7) << 5) | 桁
LD A,(LOCY)
LD C,A
AND $18
OR $40
LD H,A
LD A,C
AND $07
RRCA
RRCA
RRCA
LD C,A
LD A,(LOCX)
OR C
LD L,A
LD B,8
.drawfont
LD A,(DE)
LD (HL),A
INC DE
INC H
DJNZ .drawfont

LD A,(LOCX)
INC A
LD (LOCX),A
CP $20
JR NZ,.NONEXT
LD A,0
LD (LOCX),A
LD A,(LOCY)
INC A
LD (LOCY),A
.NONEXT
POP BC
POP DE
POP HL
RET

.NEXTLINE
LD A,(LOCY)
INC A
LD (LOCY),A
XOR A
LD (LOCX),A
POP BC
POP DE
POP HL
RET


; @name GETC__NOROM
; @resident shared
; @calls ZXN_KEYMAP
; キーの半行のポート (FEFEH〜7FFEH) を読み、押されているシフトでないキーのうち最初の 1 つを ZXN_KEYMAP で字にする。
; A = L = 字 (0 = 押していない)、H = 0。値は ROM の L モード・CAPS LOCK が切れているときと同じ (同時押しは最初の 1 つ、両シフトは SYM の表)
LD HL,ZXN_KEYMAP
LD BC,$7FFE
IN A,(C)
BIT 1,A             ; SYMBOL SHIFT (0 = 押している)
LD DE,80
JR Z,.table
LD B,$FE
IN A,(C)
BIT 0,A             ; CAPS SHIFT
LD DE,40
JR Z,.table
LD E,0
.table
ADD HL,DE
LD B,$FE
.row
IN A,(C)
CPL
AND $1F
LD D,A
LD A,B
CP $FE
JR NZ,.notcaps
RES 0,D             ; CAPS SHIFT のビットは見ない
.notcaps
CP $7F
JR NZ,.notsym
RES 1,D             ; SYMBOL SHIFT のビットは見ない
.notsym
LD A,D
LD D,5
.bit
RRCA
JR C,.found
INC HL
DEC D
JR NZ,.bit
RLC B
JR C,.row           ; 8 本の半行 (7FFEH の次で 0 のビットが 1 周する)
XOR A
JR .ret
.found
LD A,(HL)
.ret
LD L,A
LD H,0
RET


; @name SLANGINIT__NOROM
; @requires next
; @resident local
; @calls ZXNWORK,ZXN_NOROM_ISR
; @works ZXN_IM2TBL:32
; @works_align 32
; SLANGINIT と同じこと (DI、SP、WORK を 0、初期化、IY、EI、MAIN) に加えて、Next のハードウェアの IM 2 にする (ROM の 0038H を使わない)。
; 表は WORK の中の 32 バイト (32 の境目)。ULA の割り込み (11 番) だけを許す
di

; SP の初め: __STACKTOP (env の stack_top、ソースの CONST __STACKTOP で変えられる。既定 $E000)
LD SP,__STACKTOP

; WORK ZERO CLEAR
XOR A
LD HL,__WORK__
LD DE,__WORK__+1
LD BC,__WORKEND__-__WORK__-1
LD (HL),A
LDIR

<<CALLINITIALIZER>>

LD IY,__IYWORK

; 表の ULA の所 (11 番 × 2) に処理の番地。NextReg C0H = (表の下 8 ビット AND E0H) OR 01H、I = 表の上 8 ビット
LD HL,ZXN_NOROM_ISR
LD (ZXN_IM2TBL+22),HL
LD A,(ZXN_IM2TBL & $E0) | $01
DB $ED,$92,$C0
; 割り込み元は ULA だけ (C4H: ULA、C5H: CTC、C6H: UART)
DB $ED,$91,$C4,$01
DB $ED,$91,$C5,$00
DB $ED,$91,$C6,$00
LD A,ZXN_IM2TBL >> 8
LD I,A
IM 2

ei
call MAIN

di
halt

INFLOOP:
JP INFLOOP


; @name ZXN_NOROM_ISR
; @requires next
; @resident shared
; SLANGINIT__NOROM の割り込みの処理 (何もしない。HALT を解くだけ)
EI
RETI


; @name SLANGINIT__LOWRAM
; @requires next
; @resident local
; @calls ZXNWORK,ZXN_NOROM_ISR,ZXN_LOWRAM_RST8
; @works ZXN_IM2TBL:32
; @works_align 32
; SLANGINIT__NOROM と同じことに加えて、0000H〜3FFFH を RAM (ページ __LOWRAM・__LOWRAM+1) にする (ソースの CONST __LOWRAM = ページ;)。
; 使えるのは 0010H〜3FFFH (番地を指定した変数・配列)。RAM の 0008H に JP ZXN_LOWRAM_RST8 を置き、ファイルの関数の RST $08 を包みへ回す
di

; SP の初め: __STACKTOP (env の stack_top、ソースの CONST __STACKTOP で変えられる。既定 $E000)
LD SP,__STACKTOP

; NextReg B9H = 0: 0000H〜0038H の divMMC の入口を「ROM 3 が見えているときだけ」に (RAM の間は付かない。NextZXOS 2.10 と同じ値)
DB $ED,$91,$B9,$00
; MMU0・1 = ページ __LOWRAM・__LOWRAM+1
DB $ED,$91,$50,__LOWRAM
DB $ED,$91,$51,__LOWRAM+1
; RAM の 0008H に JP ZXN_LOWRAM_RST8
LD A,$C3
LD ($0008),A
LD HL,ZXN_LOWRAM_RST8
LD ($0009),HL

; WORK ZERO CLEAR
XOR A
LD HL,__WORK__
LD DE,__WORK__+1
LD BC,__WORKEND__-__WORK__-1
LD (HL),A
LDIR

<<CALLINITIALIZER>>

LD IY,__IYWORK

; 表の ULA の所 (11 番 × 2) に処理の番地。NextReg C0H = (表の下 8 ビット AND E0H) OR 01H、I = 表の上 8 ビット
LD HL,ZXN_NOROM_ISR
LD (ZXN_IM2TBL+22),HL
LD A,(ZXN_IM2TBL & $E0) | $01
DB $ED,$92,$C0
; 割り込み元は ULA だけ (C4H: ULA、C5H: CTC、C6H: UART)
DB $ED,$91,$C4,$01
DB $ED,$91,$C5,$00
DB $ED,$91,$C6,$00
LD A,ZXN_IM2TBL >> 8
LD I,A
IM 2

ei
call MAIN

di
halt

INFLOOP:
JP INFLOOP


; @name ZXN_LOWRAM_RST8
; @requires next
; @resident shared
; SLANGINIT__LOWRAM の包み。RAM の 0008H の JP から来る (呼んだ所の次の 1 バイトが esxDOS の関数の番号)。
; 呼ぶ前の MMU0・1 を NextReg から読んで控え、呼ぶ間だけ ROM (FFH) にして本当の RST $08 を呼び、戻ったら控えた値に戻す
; (ページを決め打ちしないので、Next の SLANG IDE の始まりから来ても同じに使える)。A・BC・DE・HL・IX とフラグは、そのまま渡して返す
; (割り込みの処理からファイルの関数を呼ばないこと: 関数の番号と控えの置き場は 1 つ。
;  割り込みの処理からポート 243BH・253BH で NextReg を読み書きしないこと: 下で 50H・51H を選んでから読むまでの間に選び直されると、ほかのレジスタの値を控える)
EX (SP),HL
PUSH AF
LD A,(HL)
INC HL
LD (.fn),A
POP AF
EX (SP),HL
PUSH AF
PUSH BC
LD BC,$243B         ; NextReg 50H・51H を読み、下の戻す NEXTREG の値に書く
LD A,$50
OUT (C),A
INC B
IN A,(C)
LD (.mmu0),A
DEC B
LD A,$51
OUT (C),A
INC B
IN A,(C)
LD (.mmu1),A
POP BC
LD A,$FF
DB $ED,$92,$50
DB $ED,$92,$51
POP AF
RST $08
.fn
DB 0
PUSH AF
DB $ED,$91,$50
.mmu0
DB 0
DB $ED,$91,$51
.mmu1
DB 0
POP AF
RET


; @name ZXN_FONT
; @resident shared
; 32〜127 の 96 字 × 8 バイト。1 字は上の行から 8 バイト、1 バイト目は 0、2〜8 バイト目が 5x7 の字 (ビット 6〜2)。
; LSX-Dodgers Next の移植で作った字 (zxslang の tools/font.py と同じ表)。7FH は ROM と同じ (c)
DB $00,$00,$00,$00,$00,$00,$00,$00  ; 20H
DB $00,$10,$10,$10,$10,$10,$00,$10  ; !
DB $00,$28,$28,$28,$00,$00,$00,$00  ; "
DB $00,$28,$7C,$28,$28,$7C,$28,$00  ; #
DB $00,$10,$3C,$50,$38,$14,$78,$10  ; $
DB $00,$64,$64,$08,$10,$20,$4C,$4C  ; %
DB $00,$30,$48,$50,$20,$54,$48,$34  ; &
DB $00,$10,$10,$20,$00,$00,$00,$00  ; '
DB $00,$08,$10,$20,$20,$20,$10,$08  ; (
DB $00,$20,$10,$08,$08,$08,$10,$20  ; )
DB $00,$00,$54,$38,$7C,$38,$54,$00  ; *
DB $00,$00,$10,$10,$7C,$10,$10,$00  ; +
DB $00,$00,$00,$00,$00,$00,$10,$20  ; ,
DB $00,$00,$00,$00,$7C,$00,$00,$00  ; -
DB $00,$00,$00,$00,$00,$00,$00,$10  ; .
DB $00,$04,$08,$08,$10,$20,$20,$40  ; /
DB $00,$38,$44,$4C,$54,$64,$44,$38  ; 0
DB $00,$10,$30,$10,$10,$10,$10,$38  ; 1
DB $00,$38,$44,$04,$08,$10,$20,$7C  ; 2
DB $00,$78,$04,$04,$38,$04,$04,$78  ; 3
DB $00,$08,$18,$28,$48,$7C,$08,$08  ; 4
DB $00,$7C,$40,$40,$78,$04,$04,$78  ; 5
DB $00,$18,$20,$40,$78,$44,$44,$38  ; 6
DB $00,$7C,$04,$08,$10,$20,$20,$20  ; 7
DB $00,$38,$44,$44,$38,$44,$44,$38  ; 8
DB $00,$38,$44,$44,$3C,$04,$08,$30  ; 9
DB $00,$00,$10,$00,$00,$10,$00,$00  ; :
DB $00,$00,$10,$00,$00,$10,$10,$20  ; 3BH
DB $00,$04,$08,$10,$20,$10,$08,$04  ; <
DB $00,$00,$00,$7C,$00,$7C,$00,$00  ; =
DB $00,$40,$20,$10,$08,$10,$20,$40  ; >
DB $00,$38,$44,$04,$08,$10,$00,$10  ; ?
DB $00,$38,$44,$5C,$54,$5C,$40,$38  ; @
DB $00,$38,$44,$44,$7C,$44,$44,$44  ; A
DB $00,$78,$44,$44,$78,$44,$44,$78  ; B
DB $00,$38,$44,$40,$40,$40,$44,$38  ; C
DB $00,$78,$44,$44,$44,$44,$44,$78  ; D
DB $00,$7C,$40,$40,$78,$40,$40,$7C  ; E
DB $00,$7C,$40,$40,$78,$40,$40,$40  ; F
DB $00,$38,$44,$40,$5C,$44,$44,$3C  ; G
DB $00,$44,$44,$44,$7C,$44,$44,$44  ; H
DB $00,$38,$10,$10,$10,$10,$10,$38  ; I
DB $00,$1C,$08,$08,$08,$08,$48,$30  ; J
DB $00,$44,$48,$50,$60,$50,$48,$44  ; K
DB $00,$40,$40,$40,$40,$40,$40,$7C  ; L
DB $00,$44,$6C,$54,$54,$44,$44,$44  ; M
DB $00,$44,$64,$54,$4C,$44,$44,$44  ; N
DB $00,$38,$44,$44,$44,$44,$44,$38  ; O
DB $00,$78,$44,$44,$78,$40,$40,$40  ; P
DB $00,$38,$44,$44,$44,$54,$48,$34  ; Q
DB $00,$78,$44,$44,$78,$50,$48,$44  ; R
DB $00,$3C,$40,$40,$38,$04,$04,$78  ; S
DB $00,$7C,$10,$10,$10,$10,$10,$10  ; T
DB $00,$44,$44,$44,$44,$44,$44,$38  ; U
DB $00,$44,$44,$44,$44,$44,$28,$10  ; V
DB $00,$44,$44,$44,$54,$54,$54,$28  ; W
DB $00,$44,$44,$28,$10,$28,$44,$44  ; X
DB $00,$44,$44,$28,$10,$10,$10,$10  ; Y
DB $00,$7C,$04,$08,$10,$20,$40,$7C  ; Z
DB $00,$38,$20,$20,$20,$20,$20,$38  ; [
DB $00,$40,$20,$20,$10,$08,$08,$04  ; 5CH
DB $00,$38,$08,$08,$08,$08,$08,$38  ; ]
DB $00,$10,$28,$44,$00,$00,$00,$00  ; ^
DB $00,$00,$00,$00,$00,$00,$00,$7C  ; _
DB $00,$20,$10,$08,$00,$00,$00,$00  ; `
DB $00,$00,$00,$38,$04,$3C,$44,$3C  ; a
DB $00,$40,$40,$58,$64,$44,$44,$78  ; b
DB $00,$00,$00,$38,$44,$40,$44,$38  ; c
DB $00,$04,$04,$34,$4C,$44,$44,$3C  ; d
DB $00,$00,$00,$38,$44,$7C,$40,$38  ; e
DB $00,$18,$24,$20,$70,$20,$20,$20  ; f
DB $00,$00,$00,$3C,$44,$3C,$04,$38  ; g
DB $00,$40,$40,$58,$64,$44,$44,$44  ; h
DB $00,$10,$00,$30,$10,$10,$10,$38  ; i
DB $00,$08,$00,$18,$08,$08,$48,$30  ; j
DB $00,$40,$40,$48,$50,$60,$50,$48  ; k
DB $00,$30,$10,$10,$10,$10,$10,$38  ; l
DB $00,$00,$00,$68,$54,$54,$44,$44  ; m
DB $00,$00,$00,$58,$64,$44,$44,$44  ; n
DB $00,$00,$00,$38,$44,$44,$44,$38  ; o
DB $00,$00,$00,$78,$44,$78,$40,$40  ; p
DB $00,$00,$00,$3C,$44,$3C,$04,$04  ; q
DB $00,$00,$00,$58,$64,$40,$40,$40  ; r
DB $00,$00,$00,$3C,$40,$38,$04,$78  ; s
DB $00,$20,$20,$70,$20,$20,$24,$18  ; t
DB $00,$00,$00,$44,$44,$44,$4C,$34  ; u
DB $00,$00,$00,$44,$44,$44,$28,$10  ; v
DB $00,$00,$00,$44,$44,$54,$54,$28  ; w
DB $00,$00,$00,$44,$28,$10,$28,$44  ; x
DB $00,$00,$00,$44,$44,$3C,$04,$38  ; y
DB $00,$00,$00,$7C,$08,$10,$20,$7C  ; z
DB $00,$0C,$10,$10,$60,$10,$10,$0C  ; {
DB $00,$10,$10,$10,$10,$10,$10,$10  ; |
DB $00,$60,$10,$10,$0C,$10,$10,$60  ; }
DB $00,$00,$00,$20,$54,$08,$00,$00  ; ~
DB $00,$38,$44,$5C,$54,$5C,$44,$38  ; 7FH


; @name ZXN_KEYMAP
; @resident shared
; GETC__NOROM のキーの表。40 キー × 3 (シフトなし・CAPS SHIFT・SYMBOL SHIFT)。番号は 半行 × 5 + ビット。
; 値は ROM の L モード・CAPS LOCK が切れているときの KEY_CODE と同じ (ROM の GETC に 114 の組み合わせを押して取ったもの)
; シフトなし (半行 FEFEH〜7FFEH の順に 5 キーずつ。CAPS・SYM の場所は 0)
DB $00,$7A,$78,$63,$76
DB $61,$73,$64,$66,$67
DB $71,$77,$65,$72,$74
DB $31,$32,$33,$34,$35
DB $30,$39,$38,$37,$36
DB $70,$6F,$69,$75,$79
DB $0D,$6C,$6B,$6A,$68
DB $20,$00,$6D,$6E,$62
; CAPS SHIFT (半行 FEFEH〜7FFEH の順に 5 キーずつ。CAPS・SYM の場所は 0)
DB $00,$5A,$58,$43,$56
DB $41,$53,$44,$46,$47
DB $51,$57,$45,$52,$54
DB $07,$06,$04,$05,$08
DB $0C,$0F,$09,$0B,$0A
DB $50,$4F,$49,$55,$59
DB $0D,$4C,$4B,$4A,$48
DB $20,$00,$4D,$4E,$42
; SYMBOL SHIFT (半行 FEFEH〜7FFEH の順に 5 キーずつ。CAPS・SYM の場所は 0)
DB $00,$3A,$60,$3F,$2F
DB $E2,$C3,$CD,$CC,$CB
DB $C7,$C9,$C8,$3C,$3E
DB $21,$40,$23,$24,$25
DB $5F,$29,$28,$27,$26
DB $22,$3B,$AC,$C5,$C6
DB $0D,$3D,$2B,$2D,$5E
DB $20,$00,$2E,$2C,$2A
