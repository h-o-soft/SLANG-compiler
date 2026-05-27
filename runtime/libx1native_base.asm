; libx1native_base.asm
; SLANG x1native runtime — OS 非依存 X1 hardware 直接 access
;
; Adapted from:
;   - liblsx_base.asm (SLANG-compiler, MIT) — SLANGINIT 構造、 __WORK__ clear pattern
;   - libsosx1_base.asm (SLANG-compiler, MIT) — SEARCHCTC / X1turbo 割り込みパッチ (将来追加用、 本 file 未実装)
;   - libx1_base.asm (SLANG-compiler, MIT) — VSYNC / SETUPCTC は別 lib (libx1_base) でそのまま reuse
;   - X1_compatible_rom (Meister, CC0 1.0 Universal、 https://github.com/meister68k/X1_compatible_rom)
;     参考: X1 memory layout 定数 (text VRAM=$3000, attribute VRAM=$2000, TXTCUR=$FF80)
;
; 機能:
;   - SLANGINIT: SP set + __WORK__ clear + 8255 init + CRTC 80 mode init +
;     AT_WIDTH=80 set + clear_screen call + IY + MAIN call → HALT
;   - STOP: DI + HALT loop
;   - INIT_CRTC: PARM table を _CRTCD に copy + CRTC R0-R11 を $1800/$1801 経由
;     OUT + 8255 port C + WK1FD0 sync (= libx1_print.asm WIDTH の SETCRT1 loop fork)
;   - AT_VRCALC: H=Y / L=X → HL=Y*width+X (= _CRTCD の R1 から動的に width 取得、
;     Russian peasant 乗算、 libx1_print fork)
;   - clear_screen: text + attribute + kanji 3 plane を全 cell 初期化 + sXYADR reset
;     (= boot ROM 残骸 clear、 SLANGINIT / WIDTH 切替 / CLEAR ($0C) で共通利用)
;   - _CRTCD / _C8025L / _C4025L: CRTC PARM table (= libx1_print fork、
;     turbo 系 _C8025H / _C4025H は scope 外 = MVP は標準 X1 80/40 のみ)

; @name SLANGINIT
; @resident local
; @calls sWORK, INIT_CRTC, clear_screen, _C8025L
DI
; SP は default_org ($1000) 直前に置く。 emulator load 時に boot ROM 経由 SP が
; 既に有効でも、 安全のため明示設定 (= スタック overflow しても user code に
; 喰い込まない範囲)。
LD SP, $0FFE

; WORK ZERO CLEAR
XOR A
LD HL, __WORK__
LD DE, __WORK__+1
LD BC, __WORKEND__-__WORK__-1
LD (HL), A
LDIR

<<CALLINITIALIZER>>

; --- X1 hardware 初期化 (= X1_compatible_rom IPLBOT 参考、 CC0) ---
; INIT_8255: port B のみ入力 mode (= 8255 CWR $1A03 に $82)
LD BC, $1A03
LD A, $82
OUT (C), A

; CRTC 80 mode 初期化 + AT_WIDTH = 80 (= standalone binary 化、 emulator/IPL
; の WIDTH 80 mode 設定に依存しない)。 _C8025L は標準 X1 80 col Lo-res
; PARM table、 turbo 系 (_C8025H) は scope 外。
LD A, 80
LD (AT_WIDTH), A
LD HL, _C8025L
CALL INIT_CRTC

; text + attribute + kanji 3 plane を全 cell 初期化 + sXYADR reset
; (= 起動直後の boot ROM VRAM 残骸 clear、 helper 1 本化 = WIDTH 切替 /
;  CLEAR ($0C) 制御文字 からも同じ routine 利用)。
; LSX 同期 ($FF80 = TXTCUR) は行わない (= native は独立)。
CALL clear_screen

LD IY, __IYWORK

CALL MAIN

; MAIN return 後の暴走防止 (= OS なしで戻り先がないため inline HALT loop)。
; STOP() を SLANG コードから明示的に呼出された場合は別 routine (@resident shared) を使う。
DI
.slang_halt:
HALT
JR .slang_halt


; @name STOP
; @resident shared
; @param_count 0
; SLANG コード `STOP()` 明示呼出時の停止。 OS なしで戻り先なし、 DI + HALT loop。
; LSX BDOS exit `JP 0` の代替。
DI
.stop_halt:
HALT
JR .stop_halt


; @name sWORK
; @resident shared
; @param_count 0
; @works sXYADR:2,sKBFAD:128,sKBFAD0:1,sKBFAD1:1,sKBFADX:81,sPRBF:80,sSUBPS:2,sSUBBF:256,_CTCVEC:2,_CTC:2,AT_WIDTH:1
; LSX 同名 work area を踏襲 (= sPRINT / sGETL 等の互換性確保)。 LSX 固定 addr
; ($EE8C / $EE8E / $EE92 等) は使わない、 全て __WORK__ 内 BSS。
; AT_WIDTH (1 byte) は現在の column 数 (40 or 80)、 SLANGINIT で 80 に init、
; WIDTH() で更新、 AT_VRCALC は _CRTCD の R1 から読むが symbol 単独参照用に
; 別 BSS 1 byte 確保 (= 既存 X1 系 libx1_print.asm と同名で graphics / pcg
; native 化時に流用可)。


; @name INIT_CRTC
; @resident shared
; @calls _CRTCD
; CRTC 80 mode / 40 mode 切替 + 画面 mode 設定。
; HL = source PARM table (= _C8025L / _C4025L 等、 16 byte)、 _CRTCD work area
; に LDIR copy してから CRTC R0-R11 を $1800/$1801 経由 OUT + 8255 port C
; ($1A03) + WK1FD0 ($1FD0) sync。
; libx1_print.asm WIDTH (SETCRT1 loop L33-49) を fork、 LSX 内部 sync は削除。
LD DE, _CRTCD
LD BC, 16
LDIR

LD HL, _CRTCD
XOR A
.icrtc_loop:
LD BC, $1800
OUT (C), A         ; reg # を $1800 (CRTC address port) へ
INC C              ; → $1801 (CRTC data port)
INC B              ; → $1901 (X1 では (B<<8)|C を 16-bit I/O port として扱う)
OUTI               ; (HL++) → port、 B-- (但し loop は CP 12 で抜けるので B 破壊 OK)
INC A
CP 12
JR NZ, .icrtc_loop
INC HL             ; byte 12-13 (LSX 内部 cache 用 2 byte) を skip
INC HL
LD BC, $1A03 + $0100
OUTI               ; byte 14 を 8255 port C へ OUT
LD BC, $1FD0 + $0100
OUTI               ; byte 15 を WK1FD0 ($1FD0) へ OUT
RET


; @name AT_VRCALC
; @resident shared
; @calls _CRTCD
; H = Y、 L = X 入力 → HL = Y * width + X 出力 (= text VRAM 内 offset)。
; width は _CRTCD の R1 (byte 1) から動的取得 (= WIDTH() 切替に追従)。
; Russian peasant 乗算 (8 回 ADD HL,HL + carry 時 ADD DE)、 libx1_print fork。
PUSH DE
LD C, L            ; C = X
LD B, 8            ; loop count
LD E, H            ; E = Y
LD D, 0
LD HL, (_CRTCD)    ; HL = (R1 << 8) | R0 (little-endian)
LD L, D            ; HL = R1 << 8 = width * 256 (= 加算累計の初期値、 H=R1=width)
.atvr_loop:
ADD HL, HL         ; 1 bit 左シフト
JR NC, .atvr_skip
ADD HL, DE         ; carry あれば Y 加算
.atvr_skip:
DJNZ .atvr_loop
ADD HL, BC         ; +X (= 最終 VRAM offset)
POP DE
RET


; @name clear_screen
; @resident shared
; @calls sWORK
; text + attribute + kanji 3 plane を全 cell 初期化 + sXYADR reset (= 0,0)。
; 80 col × 25 row = 2000 cell、 256 byte × 8 block = 2048 byte 走査 (= 余 48
; byte は VRAM 領域内 hidden cell、 残骸あっても表示無影響)。
;
; X1 の VRAM plane 選択は port BC の上位 byte の bit pattern:
;   bit 5 = 1 (= $20 set): kanji or text/attribute 領域選択 (実際は常に 1)
;   bit 4 = 1 (text/kanji) / 0 (attribute)
;   bit 3 = 1 (kanji selector、 次 OUT は kanji plane) / 0 (text/attribute)
; sPRINT 同手順 (OR $38; DB $ED,$71; RES 3; text; RES 4; attribute) を 1 cell
; ずつ実行。 「kanji plane = $10xx」 は誤解で、 既存 libx1_print CTRL0C / libx1_sgl
; KANJI_VRAM_ADRS=$3800 と同じく kanji も $38xx 経由 (= text region の bit 3
; set 状態で OUT 0 を出す Z80 未定義命令 DB $ED,$71 で実現)。
PUSH AF
PUSH BC
PUSH DE
PUSH HL
XOR A
LD (sXYADR), A
LD (sXYADR+1), A
LD H, 8            ; 8 block × 256 byte (= outer counter、 inner で A 破壊するため H 退避)
LD BC, $3000       ; B 走査 = $30 / $31 / ... / $37
.cls_outer:
.cls_inner:
LD D, B            ; D = 現在 block (= $30-$37) を保存
LD A, B
OR $38             ; bit 3 set → $38xx (kanji selector)
LD B, A
DB $ED, $71        ; OUT (C), 0 = kanji = 0 (= ANK 文字、 Z80 未定義命令)
RES 3, B           ; bit 3 clear → text region ($30xx)
LD E, $20
OUT (C), E         ; text = space
RES 4, B           ; bit 4 clear → attribute region ($20xx)
LD E, $07
OUT (C), E         ; attribute = 白
LD B, D            ; B 復元 ($30xx-$37xx 範囲、 次 cell 用)
INC C
JR NZ, .cls_inner
INC B              ; 次 256 byte block ($30 → $31 → ...)
DEC H              ; outer counter (= A は inner で破壊されるので使えない)
JR NZ, .cls_outer
POP HL
POP DE
POP BC
POP AF
RET


; @name _CRTCD
; @resident shared
; CRTC 設定 work area (= 16 byte、 INIT_CRTC で current PARM table を LDIR copy)。
; layout: byte 0-11 = R0-R11 (CRTC reg)、 byte 12-13 = LSX 内部 cache 用 (OUT
; しない)、 byte 14 = 8255 port C 値、 byte 15 = WK1FD0 値。
; AT_VRCALC は byte 0-1 を WORD 読みして R1 (= width = byte 1) を取得する。
; DS 16 で uninit、 SLANGINIT 内 INIT_CRTC で _C8025L から最初の copy が走る。
DS 16


; @name _C8025L
; @resident shared
; CRTC PARM: 標準 X1 80 col Lo-res、 25 行。 libx1_print.asm _C8025L fork。
; turbo 系 (_C8025H) は scope 外 (= MVP では標準 X1 のみ)。
DB	$6F, $50, $59, $38, $1F, $02, $19, $1C      ; R0-R7
DB	$00, $07                                     ; R8-R9
DW	0 - 80 * 25, 0 - 80                          ; R10/R11 + LSX cache (-80*25 = $F830, -80 = $FFB0)
DB	$0C                                          ; 8255 port C
DB	$A0                                          ; WK1FD0


; @name _C4025L
; @resident shared
; CRTC PARM: 標準 X1 40 col Lo-res、 25 行。 libx1_print.asm _C4025L fork。
DB	$37, $28, $2D, $34, $1F, $02, $19, $1C      ; R0-R7
DB	$00, $07                                     ; R8-R9
DW	0 - 40 * 25, 0 - 40                          ; R10/R11 + LSX cache
DB	$0D                                          ; 8255 port C
DB	$A0                                          ; WK1FD0
