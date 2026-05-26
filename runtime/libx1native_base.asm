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
; Phase A scope:
;   - SLANGINIT: SP set + __WORK__ clear + cursor init + IY + MAIN call → STOP
;   - STOP: DI + HALT loop
;   - CRTC / VRAM clear / SEARCHCTC は本 file scope 外 (= emulator boot ROM の画面
;     初期化を信頼、 native binary は load + PC jump で起動)。 後続 Phase で追加。

; @name SLANGINIT
; @resident local
; @calls sWORK
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

; cursor 初期値 (= __WORK__ 内 sXYADR を 0,0 に)。 __WORK__ clear で既に 0 だが
; 明示的に書く。 LSX 同期 ($FF80 = TXTCUR) は意図的に行わない (= native は独立)。
XOR A
LD (sXYADR), A
LD (sXYADR+1), A

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
; @works sXYADR:2,sKBFAD:128,sKBFAD0:1,sKBFAD1:1,sKBFADX:81,sPRBF:80,sSUBPS:2,sSUBBF:256,_CTCVEC:2,_CTC:2
; LSX 同名 work area を踏襲 (= sPRINT / sGETL 等の互換性確保)。 LSX 固定 addr
; ($EE8C / $EE8E / $EE92 等) は使わない、 全て __WORK__ 内 BSS。
