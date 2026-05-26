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

; --- X1 hardware 初期化 (= X1_compatible_rom IPLBOT 参考、 CC0) ---
; INIT_8255: port B のみ入力 mode (= 8255 CWR $1A03 に $82)
LD BC, $1A03
LD A, $82
OUT (C), A

; CLR_VRAM_ALL: text VRAM $3000-$37FF を space + attribute $2000-$27FF を白で fill
; (= X1_compatible_rom CLR_VRAM_ALL + CLR_VRAM、 256 byte × 8 block = $800 byte loop)
LD A, 8           ; HIGH(TXTSIZ=$800) = 8 block (= 256 byte × 8)
LD HL, $2007      ; H = $20 (space) / L = $07 (white attribute) = TEXT_STD
LD BC, $3000      ; BC = IOTEXT (text VRAM base port)
.clrv_text:
OUT (C), H        ; text: space を 256 byte
INC C
JR NZ, .clrv_text
RES 4, B          ; bit 4 clear: text ($30) → attribute ($20)
.clrv_attr:
OUT (C), L        ; attribute: white を 256 byte
INC C
JR NZ, .clrv_attr
SET 4, B          ; bit 4 set: attribute ($20) → text ($30)
INC B             ; 次 256 byte block ($3000 → $3100 → ... → $3700)
DEC A
JR NZ, .clrv_text

; cursor 初期値 (= __WORK__ 内 sXYADR を 0,0 に)。 __WORK__ clear で既に 0 だが
; 明示的に書く。 LSX 同期 ($FF80 = TXTCUR) は意図的に行わない (= native は独立)。
XOR A
LD (sXYADR), A
LD (sXYADR+1), A

; CRTC 初期化は本 file scope 外 (= IPL / emulator boot ROM が WIDTH 80 mode で
;  設定済の前提、 X1_compatible_rom は WIDTH 40 / 80 別パラメタ table 必要)。
;  WIDTH 切替が必要なら後続 PR で別 routine 追加。

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
