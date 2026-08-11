; Converted from lib/libdef/libsos_pcg.yml
; SLANG Runtime Library (new format)

; @name PCGDEFS
; @resident shared
; @param_count 3
; @calls PCGDEF
; HL = STARTIDX (ascii code), DE = ADDR (24 bytes/tile), BC = COUNT
; ADDR から 24 バイト × COUNT バイトの連続 PCG パターンを STARTIDX から順に登録。
; 各タイル定義毎に CRTC vblank 待ちが入るため COUNT 個の処理に約 COUNT/60 秒かかる。
.pcgdefs_loop:
LD A,B
OR C
RET Z

PUSH HL
PUSH DE
PUSH BC
CALL PCGDEF
POP BC
POP DE
POP HL

INC L
PUSH HL
LD HL,24
ADD HL,DE
EX DE,HL
POP HL
DEC BC
JR .pcgdefs_loop

; @name PCGDEF
; @resident shared
; HL = ascii code DE = address
PUSH DE
LD E,L

LD A,(sWIDTH)  ; 40 or 80
CP 40
JR Z,PCGDEF40

; WIDTH 80
LD HL,$07D0
LD D,48
JR PCG_SETNODISP

PCGDEF40:
; WIDTH 40(screen 0)
LD HL,$03E8
LD D,24

PCG_SETNODISP:
LD (PCG_NODISPADR),HL

POP HL
CALL PCGSET0
CALL SETPCG
RET

PCGSET0:
PUSH HL
PUSH DE
LD BC,$1FD0
XOR A
OUT (C),A
;
LD BC,(PCG_NODISPADR)
LD HL,$2800 ; ATTRIBUTE
POP DE
PUSH DE
LD A,20H    ; PCG COLOR 0
CALL PCGSET1
;
LD BC,(PCG_NODISPADR)
LD HL,$3000 ; VRAM
POP DE
LD A,E      ; ASCII CODE
CALL PCGSET1
;
POP HL
RET

PCGSET1:
ADD HL,BC
LD B,H
LD C,L
PCGSET2:
OUT (C),A
INC BC
DEC D
JR NZ,PCGSET2
RET

PCGBLUE EQU $15+1
PCGRED EQU $16+1
PCGGREEN EQU $17+1
SETPCG:
LD B,PCGBLUE
LD C,0
LD D,PCGRED
LD E,PCGGREEN
LD A,$08
EX AF,AF'
EXX
;
DI
LD BC,$1A01
PCGVDSP0:
IN A,(C)
JP P,PCGVDSP0
PCGVDSP1:
IN A,(C)
JP M,PCGVDSP1
;
EXX
EX AF,AF'
PCGSETP:
OUTI
LD B,D
OUTI
LD B,E
OUTI
;
LD B,PCGBLUE
EX AF,AF'
LD A,0BH
PCGDLY:
DEC A
JP NZ,PCGDLY
EX AF,AF'
;
INC C
DEC A
JP NZ,PCGSETP
;
EI
RET

PCG_NODISPADR:
DW 0
; DW $03E8  ; WIDTH 40 SCREEN 0
; DW $07E8  ; WIDTH 40 SCREEN 1
; DW $07D0  ; WIDTH 80


; @name GETCGROM
; @resident shared
; @param_count 2
; @calls SOSCALLS
; HL = CODE (ANK; 実際は L のみ使用、H は無視), DE = ADR (格納先、 8 バイト書込)
; CGROM(ANK) フォントを 1 文字分 (8 ライン × 1 バイト) ADR へ読み出す。
; S-OS 版は既存 PCGDEF と同じく sWIDTH を参照して no-display span を決める。
SCG_RDDLYVAL    EQU     $0E     ; 250T loop 用 delay 初期値
SCG_P14         EQU     $14     ; CGROM read port 上位 ($14xx)

LD A,L
LD (SCG_CODE),A        ; ANK コード保存 (L のみ使用)
EX DE,HL
LD (SCG_DEST),HL       ; 格納先 ADR を退避 (cell fill で DE/HL を潰すため)

; 幅依存の no-display 範囲を決定 (PCGDEF と同一: 40桁=$03E8/24cell, 80桁=$07D0/48cell)
LD A,(sWIDTH)          ; 40 or 80
CP 40
JR Z,SCG_W40
LD HL,$07D0
LD A,48
JR SCG_SETSPAN
SCG_W40:
LD HL,$03E8
LD A,24
SCG_SETSPAN:
LD (SCG_NODISPADR),HL
LD (SCG_SPAN),A

CALL SCG_SETCELLS      ; no-display span に attr=$07 / kanji=0 / text=CODE を敷く

; --- 互換モード read (1 plane): raster sync を 1 回 + 250T ループで 8 ライン読む ---
LD HL,(SCG_DEST)       ; HL = ADR
LD B,SCG_P14           ; BC = CGROM read port ($1400)
LD C,0
LD E,8                 ; line counter
EXX                    ; read regs を alternate に退避し、 sync 用 BC を使えるようにする
DI
LD BC,$1A01            ; raster status sync (= SETPCG の VDSP と同一)
SCG_VDSP0:
IN A,(C)
JP P,SCG_VDSP0
SCG_VDSP1:
IN A,(C)
JP M,SCG_VDSP1
EXX                    ; HL=dest, BC=$1400, E=8
SCG_RDLOOP:
IN A,(C)               ; 12, GET DATA
LD (HL),A              ; 7, STORE 1 BYTE
INC HL                 ; 6, INC POINTER
NOP                    ; 4, DUMMY
NOP                    ; 4, DUMMY
LD A,SCG_RDDLYVAL      ; 7
; 12+7+6+4+4+7=40
SCG_RDDLY:
DEC A                  ; 4
JP NZ,SCG_RDDLY        ; 10
; (4+10)*14=196
DEC E                  ; 4
JP NZ,SCG_RDLOOP       ; 10
; 40+196+14 = 250
EI
RET

SCG_SETCELLS:
LD BC,$1FD0
XOR A
OUT (C),A
; attribute VRAM ($2800+offset) ← $07 (非 PCG)
LD A,(SCG_SPAN)
LD D,A
LD BC,(SCG_NODISPADR)
LD HL,$2800
LD A,$07
CALL SCG_FILL
; kanji plane ($3800+offset) ← 0 (ANK 選択。これが無いと漢字側状態に依存)
LD A,(SCG_SPAN)
LD D,A
LD BC,(SCG_NODISPADR)
LD HL,$3800
XOR A
CALL SCG_FILL
; text VRAM ($3000+offset) ← CODE
LD A,(SCG_SPAN)
LD D,A
LD BC,(SCG_NODISPADR)
LD HL,$3000
LD A,(SCG_CODE)
CALL SCG_FILL
RET

; HL=base, BC=offset, A=value, D=count → port (base|offset) から D cell に A を OUT
SCG_FILL:
ADD HL,BC
LD B,H
LD C,L
SCG_FILL2:
OUT (C),A
INC BC
DEC D
JR NZ,SCG_FILL2
RET

SCG_DEST:
DW 0
SCG_CODE:
DB 0
SCG_SPAN:
DB 0
SCG_NODISPADR:
DW 0


; @name TATTR
; @resident shared
; @param_count 1
; @calls SOSCALLS
; HL = 属性値 ($0000-$00FF のみ有効)、戻り HL=1 成功 / HL=0 検証失敗
; 現在のテキストカーソル位置のセルの attribute だけを 1 バイト書く。
; 文字も漢字セレクタも書かず、カーソルも進めない。状態も持たない。
; bit7=X2 / bit6=Y2 / bit5=PCG / bit4=点滅 / bit3=反転 / bit2-0=BRG 色。
; 全ビットがハードウェア上の意味を持つのでマスクせずそのまま書く。
; 検証失敗時は I/O を一切行わない。DI/EI もしない (単一 OUT なので不要)。
;
; sosx1 はカーソルを SLANG 側で持たず S-OS が所有するので sCSR ($2018) で取得する
; (戻り L=X / H=Y、LOCATE が sLOC に H=Y,L=X を渡すのと同じ packing)。
; 桁数は既存 PCGDEF / GETCGROM と同じく sWIDTH ($1F5C) を読む。AT_WIDTH は
; SLANGINIT が値を入れた直後に __WORK__ をゼロクリアするため起動直後は 0 で使えない。
LD A,H
OR A
JR NZ,TAT_NG
PUSH HL                ; sCSR の破壊レジスタが不明なので属性を stack に退避
CALL sCSR              ; HL = (Y << 8) | X
LD C,L                 ; C = X
LD E,H                 ; E = Y
LD D,0
LD B,8
LD A,(sWIDTH)          ; 40 or 80
LD H,A
LD L,0                 ; HL = width * 256 (= 乗算の初期値)
TAT_MUL:
ADD HL,HL
JR NC,TAT_MUL1
ADD HL,DE
TAT_MUL1:
DJNZ TAT_MUL
LD B,0                 ; DJNZ 終了で B=0、BC = X
ADD HL,BC              ; HL = Y * width + X (= text VRAM 内 offset)
LD A,(sWIDTH)
LD BC,1000             ; 40 桁: 有効 offset 0-999
CP 41
JR C,TAT_LIM
LD BC,2000             ; 80 桁: 有効 offset 0-1999
TAT_LIM:
AND A
SBC HL,BC
JR NC,TAT_NGPOP        ; offset >= limit → 表示範囲外
ADD HL,BC              ; offset を復元
LD B,H
LD C,L
SET 5,B                ; BC = $2000 + offset (canonical attribute port)
POP DE                 ; E = 退避した属性 (PUSH HL の L 側)
LD A,E
OUT (C),A              ; ちょうど 1 バイト OUT
LD HL,1
RET
TAT_NGPOP:
POP DE                 ; 拒否パスでも stack を必ず戻す
TAT_NG:
LD HL,0
RET
