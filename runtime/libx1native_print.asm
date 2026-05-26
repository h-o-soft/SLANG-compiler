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
; Strategy: sPRINT (= 1 char emit) のみ native 実装、 上位 routine (PRT / PSTR /
; PCRONE / 等) は libsos_print.asm から copy 流用 (= 全部 PRT 経由、 PRT = JP sPRINT)。
; WIDTH / PRMODE / SCREEN / LOCATE は scope 外 (= MVP sample で使わない)。
;
; X1 text VRAM layout:
;   port-mapped、 BC = $30xx (text) / $20xx (attribute) / $10xx (kanji)
;   80 col × 25 row = 2000 byte (= offset $0000-$07CF)
;
; Cursor: sXYADR (L=X 0-79, H=Y 0-24)、 LSX 同名 work area を __WORK__ 内で保持。
; scroll は port-mapped VRAM だと LDIR 不可 (= 1920 cell × IN+OUT loop) で重い、
; MVP は Y=25 到達で stop (= 最終行に居続け、 後続 char は同じ位置を上書き)。
;
; Adapted from:
;   - libx1_print.asm (SLANG-compiler, MIT) — PRT 内 VRAM port-mapped OUT sequence
;   - libsos_print.asm (SLANG-compiler, MIT) — PRT 系上位 routine
;   - X1_compatible_rom (Meister, CC0) — IPL_PUTCHAR 実装 (BIT_ATTR_TEXT 反転)
;     (https://github.com/meister68k/X1_compatible_rom L1262-1280)


; @name sPRINT
; @resident shared
; @param_count 0
; @calls sWORK
; A = char code
; - $0D (CR): X=0, Y++ (= sp_do_cr)
; - $0A (LF): no-op
; - 他: text VRAM port-mapped 書込 + cursor X 進行、 X=80 で auto CR
PUSH AF
PUSH BC
PUSH DE
PUSH HL
LD E, A
CP $0D
JR Z, .sp_cr
CP $0A
JR Z, .sp_end
; 通常文字: X=80 なら auto CR
LD HL, (sXYADR)
LD A, L
CP 80
JR C, .sp_putc
PUSH DE
CALL sp_do_cr
POP DE
LD HL, (sXYADR)
.sp_putc:
; HL = Y*80 + X を計算 (= text 領域内 offset 0-$07CF)
PUSH DE
LD A, H
LD D, 0
LD E, A       ; DE = Y (= 0-24)
LD H, 0
LD L, 0       ; HL = 0
LD BC, 80
OR A
JR Z, .sp_no_y_loop
.sp_y_loop:
ADD HL, BC    ; HL += 80
DEC A
JR NZ, .sp_y_loop
.sp_no_y_loop:
POP DE        ; DE 復元 (E=char、 D=?)
; HL += X (= sXYADR low byte)
LD A, (sXYADR)
LD B, 0
LD C, A
ADD HL, BC    ; HL = Y*80 + X (= 0-$07CF)
; port-mapped OUT (libx1_print.asm PRT sequence)
LD B, H
LD C, L
LD A, B
OR $38        ; text VRAM region (= bit set $30-$37 範囲)
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
.sp_end:
POP HL
POP DE
POP BC
POP AF
RET


; CR 処理: X=0, Y++、 Y=25 到達で stop (= MVP、 scroll は port-mapped 重いので後回し)
sp_do_cr:
PUSH AF
PUSH HL
XOR A
LD (sXYADR), A     ; X = 0
LD HL, sXYADR+1
INC (HL)           ; Y++
LD A, (HL)
CP 25
JR C, .spcr_done
; Y=25 到達: 最終行に固定 (= scroll なし、 上書きされ続ける、 後続 PR で port-mapped scroll 実装)
LD A, 24
LD (sXYADR+1), A
.spcr_done:
POP HL
POP AF
RET


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
LD A, H
CALL PRT
LD A, L
JR PRT


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
