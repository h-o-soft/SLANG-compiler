; libx1native_print.asm
; SLANG x1native runtime — text print (OS 非依存、 X1 text VRAM 直書き)
;
; Strategy: sPRINT (= 1 char emit) のみ native 実装、 上位 routine (PRT / PSTR /
; PCRONE / PCR / PCR1 / PCHR / PHEX / PRT / P10 / PMSG / PMSX / MPRNT / PSIGN) は
; libsos_print.asm から copy 流用 (= 全部 PRT 経由、 PRT は sPRINT を JP)。
; WIDTH / PRMODE / SCREEN / LOCATE は S-OS BIOS 依存のため本 file scope 外
; (= MVP sample で使わない、 必要なら後続段階で native 実装)。
;
; Text VRAM layout (= X1):
;   $3000-$37FF : text VRAM (80 col × 25 row = 2000 byte)
;   $2000-$27FF : attribute VRAM (boot ROM 初期値そのまま、 本 file は text のみ書込)
;
; Cursor: sXYADR (L=X 0-79, H=Y 0-24)、 LSX 同名 work area を __WORK__ 内で保持
;
; Adapted from:
;   - libsos_print.asm (SLANG-compiler, MIT) — PRT 系上位 routine
;   - X1_compatible_rom (Meister, CC0) — IPL_PUTCHAR / IPLPRN_1 周辺 (VRAM 書込 pattern)
;     (https://github.com/meister68k/X1_compatible_rom L1247-1340)


; @name sPRINT
; @resident shared
; @param_count 0
; @calls sWORK
; X1 native: A = char code を text VRAM に書き込み + cursor 進行
; - $0D (CR): 行頭に戻す → 次行
; - $0A (LF): 無視 (CR と LF は LSX 流儀で CR=改行、 LF は noop)
; - $08 (BS): 後退 (本 MVP は未対応、 通常文字扱い)
; - 他: VRAM 書込 + cursor X 進行、 X=80 で自動改行、 Y=25 で scroll up
PUSH BC
PUSH DE
PUSH HL
LD (sp_char_buf), A   ; char を退避 (= POP DE で E が破壊されるため、 別途保存)
LD E, A
CP $0D
JR Z, .sp_cr
CP $0A
JR Z, .sp_end
; 通常文字
LD HL, (sXYADR)   ; L=X, H=Y
LD A, L
CP 80
JR C, .sp_putc
; 行末超え: auto CR
PUSH DE
CALL .sp_do_cr
POP DE
LD HL, (sXYADR)
.sp_putc:
; VRAM addr = $3000 + Y*80 + X
PUSH HL
LD A, H
LD H, 0
LD D, H
LD B, A           ; B = Y count
OR A
JR Z, .sp_no_y
LD BC, 80
LD H, 0
LD L, 0
.sp_y_loop:
ADD HL, BC
DEC A
JR NZ, .sp_y_loop
JR .sp_addy
.sp_no_y:
LD H, 0
LD L, 0
.sp_addy:
POP DE            ; D=元Y, E=元X
LD A, E
LD B, 0
LD C, A
ADD HL, BC        ; HL = Y*80 + X
LD BC, $3000
ADD HL, BC        ; HL = $3000 + Y*80 + X
LD A, (sp_char_buf)
LD (HL), A
; cursor X 進行
LD HL, sXYADR
INC (HL)
JR .sp_end
.sp_cr:
CALL .sp_do_cr
JR .sp_end
.sp_end:
POP HL
POP DE
POP BC
RET

; --- internal helpers (= label private to file) ---
sp_char_buf: DB 0   ; char 一時保存用 (= sPRINT 内の POP DE で E が破壊されるため必要)

; CR 処理: X=0, Y++、 Y=25 なら scroll up
.sp_do_cr:
PUSH BC
PUSH DE
PUSH HL
XOR A
LD (sXYADR), A     ; X = 0
LD HL, sXYADR+1
INC (HL)           ; Y++
LD A, (HL)
CP 25
JR C, .sp_cr_done
; Y=25: scroll up = text VRAM を 80 byte 上に移動、 最終行 clear
LD HL, $3000+80    ; src = 2 行目
LD DE, $3000       ; dst = 1 行目
LD BC, 80*24       ; 24 行分
LDIR
; 最終行 clear (= space で埋める、 $20)
LD HL, $3000+80*24
LD A, $20
LD B, 80
.sp_clr_last:
LD (HL), A
INC HL
DJNZ .sp_clr_last
; Y を 24 に戻す
LD A, 24
LD (sXYADR+1), A
.sp_cr_done:
POP HL
POP DE
POP BC
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
