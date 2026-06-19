; Converted from lib/libdef/libx1_pcg.yml
; SLANG Runtime Library (new format)


; @name PCGCOMMON
; @resident shared
; @param_count 0

; no-display span (GCG_SPAN cells) へ 3 プレーンを敷く
; attribute=$2800+ / kanji=$3800+ / text=$3000+ (PCGSET0 の base + kanji plane 追加)

SETPCGCELLS:
    LD BC,$1FD0
    XOR A
    OUT (C),A
    ; attribute VRAM ($2800+offset) ← .GCG_ATTR ($20=PCG / $07=ANK CGROM)
    LD A,(.GCG_SPAN)
    LD D,A
    LD BC,(.GCG_NODISPADR)
    LD HL,$2800
    LD A,(.GCG_ATTR)
    CALL .GCG_FILL
    ; kanji plane ($3800+offset) ← 0 (ANK 選択。 これが無いと漢字側状態に依存)
    LD A,(.GCG_SPAN)
    LD D,A
    LD BC,(.GCG_NODISPADR)
    LD HL,$3800
    XOR A
    CALL .GCG_FILL
    ; text VRAM ($3000+offset) ← CODE
    LD A,(.GCG_SPAN)
    LD D,A
    LD BC,(.GCG_NODISPADR)
    LD HL,$3000
    LD A,(.GCG_CODE)
    CALL .GCG_FILL
    RET

; HL=base, BC=offset, A=value, D=count → port (base|offset) から D cell に A を OUT
.GCG_FILL
    ADD HL,BC
    LD B,H
    LD C,L
.GCG_FILL2
    OUT (C),A
    INC BC
    DEC D
    JR NZ,.GCG_FILL2
    RET

.GCG_CODE
    DB 0
.GCG_SPAN
    DB 0
.GCG_NODISPADR
    DW 0
.GCG_ATTR
    DB 0

; @name PCGDEFS
; @resident shared
; @param_count 3
; @calls PCGDEF
; HL = STARTIDX (ascii code), DE = ADDR (24 bytes/tile), BC = COUNT
; ADDR から 24 バイト × COUNT バイトの連続 PCG パターンを STARTIDX から順に登録。
; 各タイル定義毎に CRTC vblank 待ちが入るため COUNT 個の処理に約 COUNT/60 秒かかる。
.pcgdefs_loop
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
; @calls PCGCOMMON,sWORK,X1WORK
; HL = ascii code DE = address
    PUSH DE
    LD E,L
    LD A,E
    LD (SETPCGCELLS.GCG_CODE),A        ; ANK コード保存

    LD A,(AT_WIDTH)  ; 40 or 80
    CP 40
    JR Z,.PCGDEF40

    ; WIDTH 80
    LD HL,$07D0
    LD A,48
    JR .PCG_SETNODISP

.PCGDEF40
    ; WIDTH 40(screen 0)
    LD HL,$03E8
    LD A,24

.PCG_SETNODISP
    LD (SETPCGCELLS.GCG_NODISPADR),HL
    LD (SETPCGCELLS.GCG_SPAN),A
    LD A,$20
    LD (SETPCGCELLS.GCG_ATTR),A

    CALL SETPCGCELLS
    POP HL
    CALL SETPCG
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
.PCGVDSP0
    IN A,(C)
    JP P,.PCGVDSP0
.PCGVDSP1
    IN A,(C)
    JP M,.PCGVDSP1
    ;
    EXX
    EX AF,AF'
.PCGSETP
    OUTI
    LD B,D
    OUTI
    LD B,E
    OUTI
    ;
    LD B,PCGBLUE
    EX AF,AF'
    LD A,0BH
.PCGDLY
    DEC A
    JP NZ,.PCGDLY
    EX AF,AF'
    ;
    INC C
    DEC A
    JP NZ,.PCGSETP
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
; @calls sWORK,X1WORK,PCGCOMMON
; HL = CODE (ANK; 実際は L のみ使用、H は無視), DE = ADR (格納先、 8 バイト書込)
; CGROM(ANK) フォントを 1 文字分 (8 ライン × 1 バイト) ADR へ読み出す。
; X1 の CG/PCG は「画面走査中セルの I/O」という間接アクセスのため、 no-display
; 領域に対象 CODE を非 PCG (attr=$07) + kanji=0 で span 敷きし、 ラスタ同期して
; $14xx (CGROM read port) を 8 ライン読む。 span 全 cell を同一 CODE で埋めるので
; どの cell が走査されても同じ字形が返り、 read タイミングに寛容になる (PCGDEF 同様)。
; AT_WIDTH provider は @calls sWORK,X1WORK でリンク時に解決 (実行時 WIDTH 不要)。
; 設計: CG ROM read は 1FD0.bit5=0 の「互換モード」を使う (高速モードは turbo 以上限定)。
; 互換モードは CRTC が今スキャン中の CG コード+ラスタ位置が返る = ライン番号は raster で
; 決まり、 ソフトで timing 同期が要る。 PCG の 1 プレーン定義 250T ループを
; read に反転し、 固定 $1400 read の 25T ブロック + dummy 8T + delay 217T で
; 1 ライン 250T に合わせて読む。
GCG_RDDLYVAL    EQU     $0E     ; 250T loop 用 delay 初期値
GCG_P14         EQU     $14     ; CGROM read port 上位 ($14xx)

    LD A,L
    LD (SETPCGCELLS.GCG_CODE),A        ; ANK コード保存 (L のみ使用)
    EX DE,HL
    LD (.GCG_DEST),HL       ; 格納先 ADR を退避 (cell fill で DE/HL を潰すため)

; 幅依存の no-display 範囲を決定 (PCGDEF と同一: 40桁=$03E8/24cell, 80桁=$07D0/48cell)
    LD A,(AT_WIDTH)        ; 40 or 80 (provider は @calls sWORK,X1WORK でリンク)
    CP 40
    JR Z,.GCG_W40
    LD HL,$07D0
    LD A,48
    JR .GCG_SETSPAN
.GCG_W40
    LD HL,$03E8
    LD A,24
.GCG_SETSPAN
    LD (SETPCGCELLS.GCG_NODISPADR),HL
    LD (SETPCGCELLS.GCG_SPAN),A
    LD A,$07
    LD (SETPCGCELLS.GCG_ATTR),A

    CALL SETPCGCELLS      ; no-display span に attr=$07 / kanji=0 / text=CODE を敷く

; --- 互換モード read (1 plane): raster sync を 1 回 + 250T ループで 8 ライン読む ---
    LD HL,(.GCG_DEST)       ; HL = ADR
    LD B,GCG_P14           ; BC = CGROM read port ($1400)
    LD C,0
    LD E,8                 ; line counter
    EXX                    ; read regs を alternate に退避し、 sync 用 BC を使えるようにする
    DI
    LD BC,$1A01           ; raster status sync (= SETPCG の VDSP と同一)
.GCG_VDSP0
    IN A,(C)
    JP P,.GCG_VDSP0
.GCG_VDSP1
    IN A,(C)
    JP M,.GCG_VDSP1
    EXX                    ; HL=dest, BC=$1400, E=8
.GCG_RDLOOP
    IN A,(C)              ; 12, GET DATA
    LD (HL),A             ; 7, STORE 1 BYTE
    INC HL                ; 6, INC POINTER
    NOP                   ; 4, DUMMY
    NOP                   ; 4, DUMMY
    LD A,GCG_RDDLYVAL     ; 7
    ; 12+7+6+4+4+7=40
.GCG_RDDLY
    DEC A                 ; 4
    JP NZ,.GCG_RDDLY       ; 10
    ; (4+10)*14=196
    DEC E                 ; 4
    JP NZ,.GCG_RDLOOP      ; 10
    ; 40+196+14 = 250
    EI
    RET


.GCG_DEST
    DW 0
