; libx1native_print.asm
; SLANG x1native runtime — text print (OS 非依存、 X1 port-mapped text VRAM 書込)
;
; **重要**: X1 の text VRAM は **port-mapped I/O** (= `OUT (C), A`、 BC = VRAM addr)、
; 単純な `LD (HL), A` では書けない (= main RAM に書いてしまう)。 既存 libx1_print.asm
; PRT (LSX 上で動作実績あり) と X1_compatible_rom IPL_PUTCHAR の sequence を採用:
;   1. BC に VRAM addr (= $0000-$07CF が text 領域内 offset)
;   2. LD A,B; OR $38; LD B,A   ← 上位 nibble に $38 = text VRAM region 選択
;   3. DB $ED,$71  (OUT (C),0 = Z80 未定義命令)  ← kanji = 0 (= ANK 文字指定)
;   4. RES 3, B    ← bit 3 clear ($38→$30) で text 領域に切替
;   5. OUT (C), A  ← text VRAM 書込 (= 文字コード)
;   6. attribute は触らず (= boot ROM 初期色のまま、 IPL_PUTCHAR と同様の MVP 戦略)
;
; Strategy: sPRINT (= 1 char emit) + WIDTH / LOCATE / SCREEN / PRMODE + scroll up
; + HOME / CLEAR 制御文字 を native 実装、 上位 routine (PRT / PSTR / PCRONE / 等)
; は libsos_print.asm から copy 流用 (= 全部 PRT 経由、 PRT = JP sPRINT)。
;
; X1 text VRAM layout:
;   port-mapped、 BC = $30xx (text) / $20xx (attribute) / $38xx (kanji selector、
;   = text region + bit 3 set、 OUT 0 用 Z80 未定義命令 DB $ED, $71 で kanji=0
;   書込)。 kanji は memory map 上 $1000-$17FF / $3800-$3FFF にあるが、 port
;   I/O 経由では $38xx で書込する (= 既存 libx1_print CTRL0C / libx1_sgl
;   KANJI_VRAM_ADRS=$3800 と同戦略、 $10xx は port-mapped では別 region)。
;   80 col × 25 row = 2000 byte (= offset $0000-$07CF)
;
; Cursor: sXYADR (L=X 0-(width-1), H=Y 0-24)、 LSX 同名 work area を __WORK__ 内
; で保持。 width は AT_WIDTH (= libx1native_base.asm の sWORK BSS、 40 or 80) で
; 動的化、 wrap / scroll / VRAM offset 計算 (= AT_VRCALC) は全て AT_WIDTH 経由。
;
; scroll: port-mapped VRAM は LDIR 不可 (= 1 cell ずつ IN/OUT loop)、 width*24
; cell × 2 plane (= text + attribute) を 1 行上に shift + 最終行 fill。 kanji
; plane は SLANGINIT の clear_screen で 0 fill 済み、 sPRINT は毎 char kanji=0
; を上書きするため scroll では触らない (= 0 維持される)。
;
; Adapted from:
;   - libx1_print.asm (SLANG-compiler, MIT) — PRT 内 VRAM port-mapped OUT sequence
;   - libsos_print.asm (SLANG-compiler, MIT) — PRT 系上位 routine
;   - X1_compatible_rom (Meister, CC0) — IPL_PUTCHAR 実装 (BIT_ATTR_TEXT 反転)
;     (https://github.com/meister68k/X1_compatible_rom L1262-1280)


; @name sPRINT
; @resident shared
; @param_count 0
; @calls sWORK, AT_VRCALC, clear_screen
; A = char code
; - $0D (CR): sp_do_cr (= X=0, Y++、 Y=25 で scroll_up)
; - $0B (HOME): cursor を 0,0 へ (画面 clear なし)
; - $0C (CLEAR): clear_screen (= 3 plane 全 cell 初期化 + cursor 0,0)
; - $0A (LF): no-op
; - 他: text + attribute VRAM port-mapped 書込 + cursor X 進行
;       X >= AT_WIDTH なら auto CR (= 書込前に sp_do_cr で折返し)
PUSH AF
PUSH BC
PUSH DE
PUSH HL
LD E, A
CP $0D
JR Z, .sp_cr
CP $0B
JR Z, .sp_home
CP $0C
JR Z, .sp_clear
CP $0A
JR Z, .sp_end
; 通常文字: 折返し判定 (= AT_WIDTH を A、 現在 X を B に置いて CP、
; X >= AT_WIDTH なら sp_do_cr で次行へ。 Codex review off-by-one fix:
; JR Z (X == width) + JR C (X > width) で >= width を確実に wrap)
LD HL, (sXYADR)
LD A, L                ; A = X
LD B, A                ; B = X 退避
LD A, (AT_WIDTH)       ; A = width
CP B                   ; A - B = width - X
JR Z, .sp_wrap         ; X == width: wrap
JR C, .sp_wrap         ; X > width (= 念のため): wrap
JR .sp_putc
.sp_wrap:
CALL sp_do_cr
LD HL, (sXYADR)
.sp_putc:
; HL = (sXYADR.H << 8) | sXYADR.L = (Y, X)、 AT_VRCALC で Y*width+X (VRAM offset)
CALL AT_VRCALC
; port-mapped OUT (= libx1_print.asm PRT sequence と同じく、 attribute は触らない)
; attribute を書込みすると PCG 用に設定された attribute (= PCG flag 含む) を
; 消してしまうため、 LSX / S-OS 慣例通り sPRINT では text + kanji=0 のみ書込。
; 初期 attribute ($07 = 白) は SLANGINIT 内 clear_screen で全 cell に fill 済、
; scroll_up の最終行 fill / CLEAR ($0C) でも attribute $07 で塗り直されるため
; 通常 text 表示には影響なし。
LD B, H
LD C, L
LD A, B
OR $38        ; text VRAM region (= bit 5,4,3 set = $38-$3F 範囲)
LD B, A
DB $ED, $71   ; OUT (C), 0 = kanji area に 0 (= ANK 文字、 Z80 未定義命令)
RES 3, B      ; bit 3 clear ($38→$30、 text region)
OUT (C), E    ; OUT text (= 文字コード)
; cursor X 進行
LD HL, sXYADR
INC (HL)
JR .sp_end
.sp_cr:
CALL sp_do_cr
JR .sp_end
.sp_home:
CALL sCTRL_home
JR .sp_end
.sp_clear:
CALL sCTRL_clear
.sp_end:
POP HL
POP DE
POP BC
POP AF
RET


; CR 処理: X=0, Y++、 Y=25 到達で scroll_up + Y=24 cap
; PUSH AF/BC/DE/HL: scroll_up 内で全 reg 破壊するため上位呼出側に影響しない
sp_do_cr:
PUSH AF
PUSH BC
PUSH DE
PUSH HL
XOR A
LD (sXYADR), A     ; X = 0
LD HL, sXYADR+1
INC (HL)           ; Y++
LD A, (HL)
CP 25
JR C, .spcr_done
CALL scroll_up
LD A, 24
LD (sXYADR+1), A
.spcr_done:
POP HL
POP DE
POP BC
POP AF
RET


; 1 行 scroll up: text + attribute 2 plane を 1 行 (= width cell) 上に memmove
; + 最終行 (= row 24) を space ($20) / 白 ($07) で fill。
; src = offset width (= 2 行目先頭) → dst = offset 0、 count = width * 24 cell。
; port-mapped: BC = $30xx (text) or $20xx (attribute)、 IN read → OUT write。
; kanji plane は触らない (= clear_screen で 0 fill 済 + sPRINT で 0 維持)。
scroll_up:
PUSH AF
PUSH BC
PUSH DE
PUSH HL
; count = AT_WIDTH * 24 (= width * 8 * 3)
LD A, (AT_WIDTH)
LD H, 0
LD L, A
ADD HL, HL          ; ×2
ADD HL, HL          ; ×4
ADD HL, HL          ; ×8 (= width * 8)
LD D, H
LD E, L             ; DE = width * 8
ADD HL, DE
ADD HL, DE          ; HL = width * 24
LD (sc_count), HL

; text plane scroll: $30(src) → $30(dst)
LD A, (AT_WIDTH)
LD H, 0
LD L, A
LD (sc_src), HL    ; src offset = AT_WIDTH (= 2 行目先頭)
LD HL, 0
LD (sc_dst), HL    ; dst offset = 0
.sc_text_loop:
LD HL, (sc_src)
LD B, H
LD C, L
LD A, B
OR $30
LD B, A            ; BC = $30xx + src offset (text region port)
IN A, (C)
LD E, A            ; E = read char
LD HL, (sc_dst)
LD B, H
LD C, L
LD A, B
OR $30
LD B, A
OUT (C), E         ; dst へ書込
LD HL, (sc_src)
INC HL
LD (sc_src), HL
LD HL, (sc_dst)
INC HL
LD (sc_dst), HL
LD HL, (sc_count)
DEC HL
LD (sc_count), HL
LD A, H
OR L
JR NZ, .sc_text_loop

; attribute plane scroll: $20(src) → $20(dst)、 count 再計算
LD A, (AT_WIDTH)
LD H, 0
LD L, A
ADD HL, HL
ADD HL, HL
ADD HL, HL
LD D, H
LD E, L
ADD HL, DE
ADD HL, DE          ; HL = width * 24
LD (sc_count), HL
LD A, (AT_WIDTH)
LD H, 0
LD L, A
LD (sc_src), HL
LD HL, 0
LD (sc_dst), HL
.sc_attr_loop:
LD HL, (sc_src)
LD B, H
LD C, L
LD A, B
OR $20
LD B, A            ; BC = $20xx + src offset (attribute region port)
IN A, (C)
LD E, A
LD HL, (sc_dst)
LD B, H
LD C, L
LD A, B
OR $20
LD B, A
OUT (C), E
LD HL, (sc_src)
INC HL
LD (sc_src), HL
LD HL, (sc_dst)
INC HL
LD (sc_dst), HL
LD HL, (sc_count)
DEC HL
LD (sc_count), HL
LD A, H
OR L
JR NZ, .sc_attr_loop

; 最終行 (= row 24、 offset width*24 から width cell) を text=$20 + attr=$07 で fill
LD A, (AT_WIDTH)
LD H, 0
LD L, A
ADD HL, HL
ADD HL, HL
ADD HL, HL
LD D, H
LD E, L
ADD HL, DE
ADD HL, DE          ; HL = width * 24 (= 最終行 先頭 offset)
LD (sc_dst), HL
LD A, (AT_WIDTH)
LD (sc_count), A    ; count low byte (= width <= 80、 8-bit で足りる)
.sc_fill_loop:
LD HL, (sc_dst)
LD B, H
LD C, L
LD A, B
OR $30
LD B, A            ; text region $30xx
LD A, $20
OUT (C), A         ; text = space
RES 4, B           ; → attribute region $20xx
LD A, $07
OUT (C), A         ; attribute = 白
LD HL, (sc_dst)
INC HL
LD (sc_dst), HL
LD A, (sc_count)
DEC A
LD (sc_count), A
JR NZ, .sc_fill_loop

POP HL
POP DE
POP BC
POP AF
RET


; sCTRL_home ($0B): cursor を 0,0 に reset (= 画面 clear なし)
sCTRL_home:
XOR A
LD (sXYADR), A
LD (sXYADR+1), A
RET

; sCTRL_clear ($0C): 全画面 clear (= clear_screen 内で sXYADR reset も走る)
sCTRL_clear:
JP clear_screen     ; tail call、 helper 1 本化

; scroll_up 内 work area (= count / src / dst の 3 word、 resident asm 領域内 RAM)
sc_count: DW 0
sc_src:   DW 0
sc_dst:   DW 0


; --- 以下 libsos_print.asm からの copy (= PRT 経由で sPRINT を呼ぶ流儀) ---

; @name PCRONE
; @resident shared
; @param_count 0
; @calls PCR
LD HL,1


; @name PCR
; @resident shared
; @param_count 1
; @calls PCR1
LD E,$0D


; @name PCR1
; @resident shared
; @param_count 1
; @calls PSTR
EX DE,HL


; @name PSTR
; @resident shared
; @param_count 2
; @calls PRT
.pstr1
LD A,D
OR E
RET Z
LD A,L
CALL PRT
DEC DE
JR .pstr1


; @name PCHR
; @resident shared
; @calls PRT
; 既存 libx1_print.asm L243-251 と同じく、 H / L が 0 なら sPRINT skip
; (= NUL 文字を VRAM に書かない、 cursor 進めない、 Codex review Medium 指摘修正)
LD A, H
OR A
CALL NZ, PRT
LD A, L
OR A
JR NZ, PRT
RET


; @name PRT
; @resident shared
; @param_count 1
; @calls sPRINT
JP sPRINT


; @name PHEX4
; @resident shared
; @param_count 1
; @calls PHEX2
LD A,H
CALL PHEX


; @name PHEX2
; @resident shared
; @param_count 1
; @calls PHEX
LD A,L


; @name PHEX
; @resident shared
; @param_count 1
; @calls SASC,PRT
PUSH AF
RRCA
RRCA
RRCA
RRCA
CALL SASC
CALL PRT

POP AF
CALL SASC


; @name PMSG
; @resident shared
; @calls PMSX1
LD B, $0D
JR PMSX1


; @name PMSX
; @resident shared
; @calls PMSX1
LD B, 00


; @name PMSX1
; @resident shared
; @calls PRT,PMSG
LD A, (HL)
CP B
RET Z
CALL PRT
INC HL
JR PMSX1


; @name MPRNT
; @resident shared
; @calls PRT
EX (SP),HL
.mprnt2
LD A, (HL)
INC HL
OR A
JR Z, .mprnt1
CALL PRT
JR .mprnt2
.mprnt1
EX (SP),HL
RET


; @name PSIGN
; @resident shared
; @param_count 1
; @calls PRT,NEGHL,P10
BIT 7, H
JR Z,.psign1
LD A, $2D
CALL PRT
CALL NEGHL
.psign1


; @name P10
; @resident shared
; @param_count 1
; @function_type Machine
; @calls P10to5,P10toN
LD DE, -1
JR P10toN


; @name P10to5
; @resident shared
; @param_count 1
; @function_type Machine
; @calls P10toN
LD DE, 0005


; @name P10toN
; @resident shared
; @param_count 2
; @function_type Machine
; @calls PRT,VTOS,PMSX
PUSH DE
LD DE, WORK10
CALL VTOS
EX DE, HL
POP DE
LD A, E
CP $05
JR NC, .p10ton1
LD A, $05
SUB E
.p10ton2
INC HL
DEC A
JR NZ, .p10ton2
JR PMSX
.p10ton1
LD A, E
CP $FF
JR NZ, PMSX
.p10ton4
LD A, (HL)
CP $20
JR NZ, PMSX
INC HL
JR .p10ton4

WORK10:
DB  "12345",0
DS  4


; --- 公開 API: LOCATE / SCREEN / PRMODE / WIDTH (= 既存 LSX libx1_print と同名規約) ---

; @name LOCATE
; @resident shared
; @param_count 2
; @calls sWORK
; arg1 = HL (L = X、 H 未使用)、 arg2 = DE (E = Y)、 戻りなし
; 既存 libx1_print L141-152 と同じ規約: LD H, E で H = Y にしてから (sXYADR) に書く
LD H, E
LD (sXYADR), HL
RET


; @name SCREEN
; @resident shared
; @param_count 2
; @calls sWORK, AT_VRCALC
; arg1 = HL (L = X)、 arg2 = DE (E = Y)、 戻り HL = char code
; (= 内部で port-mapped IN で A に読み、 L=A, H=0 で HL に展開 = SLANG MACHINE 戻り規約)
LD H, E             ; H = Y、 L = X (= sXYADR と同 packing で AT_VRCALC へ)
CALL AT_VRCALC      ; HL = Y * width + X (= text VRAM 内 offset)
LD B, H
LD C, L
LD A, B
OR $38              ; text VRAM region ($38-$3F、 sPRINT 同戦略)
LD B, A
RES 3, B            ; bit 3 clear ($38→$30、 text region)
IN A, (C)           ; A = char code
LD L, A
LD H, 0
RET


; @name PRMODE
; @resident shared
; @param_count 1
; stub (= printer なし、 既存 LSX 系 libx1_print も同様の RET-only)
RET


; @name WIDTH
; @resident shared
; @param_count 1
; @calls sWORK, INIT_CRTC, clear_screen, _C8025L, _C4025L
; L = column count (= 40 or 80)、 41 未満 → 40 mode / それ以上 → 80 mode。
; PARM 選択 + INIT_CRTC + AT_WIDTH 更新 + 画面 clear (= 既存 LSX 系も WIDTH 末尾で
; CTRL0C 呼出、 STARS 冒頭 `WIDTH(WD)` 等で画面初期化を期待する慣習に合わせる)。
LD A, L
CP 41
JR C, .w40
LD HL, _C8025L
LD A, 80
JR .w_set
.w40:
LD HL, _C4025L
LD A, 40
.w_set:
LD (AT_WIDTH), A
CALL INIT_CRTC
JP clear_screen     ; tail call (= clear_screen 内の RET でそのまま戻る)
