; libx1native_input.asm
; SLANG x1native runtime — keyboard input (OS 非依存)
;
; X1 keyboard input は sub CPU (= 80C49 @ port $1900) 経由:
;   1. 8255 port B ($1A01) の bit40h (write 可能 flag) 待ち
;   2. $1900 に command 0xE6 (= keyboard 入力) 送信
;   3. 再度 write 可能待ち
;   4. 8255B bit20h (read 可能 flag) 待ち → $1900 から function key (= 1 byte 読み捨て)
;   5. 同様 → $1900 から ASCII (= 本命 1 byte)
;
; Adapted from:
;   - liblsx_input.asm (SLANG-compiler, MIT) — INKEY / LINPUT / GETL / GETLIN 構造
;   - X1_compatible_rom (Meister, CC0) — KBHIT / WAIT_80C49_WR / READ_80C49 シーケンス
;     (https://github.com/meister68k/X1_compatible_rom L1164-1232)
;
; Phase A scope: sGETKY / sFLGET / sINKEY / INKEY のみ。 LINPUT / GETL / GETLIN /
; INPUT は LSX 既存 path (= sGETL = BDOS 経路) に依存するため、 sample で使わなければ
; dead code 除去で問題なし。 完全対応は後続段階。

; --- I/O ports ---
; IO8255B  EQU $1A01   ; 8255 port B (status flag for 80C49 sub CPU)
; IO80C49  EQU $1900   ; sub CPU 80C49 data port
; (EQU は asm 内で局所使用、 LSX 同名 symbol との衝突回避のため inline 即値で書く)


; @name sGETKY
; @resident shared
; @param_count 0
; X1 keyboard 1 byte 取得 (= no wait、 押されてなければ A=0)
; X1_compatible_rom KBHIT 相当
PUSH BC
EI
; WAIT_80C49_WR: 8255B bit40h が 0 になるまで wait (= sub CPU 書込み可能)
LD BC, $1A01
.gk_w1:
IN A, (C)
AND $40
JR NZ, .gk_w1
; command 送信
LD BC, $1900
LD A, $E6
OUT (C), A
; 再度 wait
LD BC, $1A01
.gk_w2:
IN A, (C)
AND $40
JR NZ, .gk_w2
DI
; READ_80C49 ×1: function key (読み捨て)
LD BC, $1A01
.gk_r1:
IN A, (C)
AND $20
JR NZ, .gk_r1
LD BC, $1900
IN A, (C)        ; function key (使わない)
; READ_80C49 ×2: ASCII (本命)
LD BC, $1A01
.gk_r2:
IN A, (C)
AND $20
JR NZ, .gk_r2
LD BC, $1900
IN A, (C)
POP BC
OR A             ; flag 更新
RET


; @name sFLGET
; @resident shared
; @param_count 0
; @calls sGETKY
; キーが押されるまで wait (= LSX BDOS DIRIN 相当)
.fl_loop:
CALL sGETKY
OR A
JR Z, .fl_loop
RET


; @name sINKEY
; @resident shared
; @param_count 0
; @calls sGETKY
; LSX 既存と同 semantic: sGETKY ループで wait
.ik_loop:
CALL sGETKY
OR A
JR Z, .ik_loop
RET


; @name INKEY
; @resident shared
; @param_count 1
; @calls sGETKY,sFLGET,sINKEY
; SLANG `INKEY(n)`: n=0 → sGETKY (no wait), n=1 → sFLGET (wait once),
; その他 → sINKEY (loop wait)
; liblsx_input.asm INKEY と同 logic
LD A,L
CP 1
JR NC,.inkey1
CALL sGETKY
JR .inkey_end
.inkey1
JR NZ,.inkey2
CALL sFLGET
JR .inkey_end
.inkey2
CALL sINKEY
.inkey_end
LD L,A
LD H,0
RET
