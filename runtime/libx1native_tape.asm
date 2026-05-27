; libx1native_tape.asm
; SLANG x1native runtime — CMT (cassette tape) 多段 load 対応
;
; Adapted from:
;   - X1_compatible_rom (Meister, CC0 1.0 Universal,
;     https://github.com/meister68k/X1_compatible_rom)
;     参考実装: X1_compatible_rom.z80 の以下 routine 群
;       - tape bit-stream read: L1639-1819 (MT_EDGE / MT_RDBIT / MT_RDBIT_D /
;         MT_SKIP1111 / MT_SKIP0 / MT_SKIP1 / MT_RDBYTE)
;       - block load: L1440-1516 (CMT_LOADIFB / CMT_LOADFILE)
;       - sub CPU deck control: L1521-1574 (MT_CTRL_PLAY / MT_CTRL_STOP / MT_CTRL)
;       - sub CPU helpers: L1209-1233 (WAIT_80C49_WR / READ_80C49)
;     CC0 → MIT 互換、 attribution として本 header に明記。
;
; 機能:
;   MTREAD(load_addr): 次 info+data block を読込、 戻り HL = data size (or $FFFF)
;     - 引数 HL を data load 先 addr として優先 (= info block 内 LoadAddress 無視)
;     - 内部で deck PLAY → info block read → data block read → deck STOP
;   MTREADJP(load_addr, jp_addr): 上 + jump (= raw stage 用、 overlay 経由なら
;     MTREAD + 関数呼出 推奨)
;
; CMT port:
;   $1A01 (8255 port B) bit 1: CMT data line polling
;   $1900: sub CPU command port ($E9 = cassette deck control)
;
; 前提: X1 tape は 1 段目読込完了で auto stop、 2 段目は MT_CTRL_PLAY で
;       再生再開必須。 tape read は bit-level timing critical (~185µs/bit)、
;       routine 内 DI で割り込み禁止 + IFF2 復元 guard で entry 時状態維持。
;
; X1 info block byte format (= X1InfoBlock.cs と整合):
;   byte 0x00: BootFlag、 0x01-0x0D: FileName (13 char)、
;   byte 0x0E-0x10: Extension (3 char)、 byte 0x11: Password、
;   byte 0x12-0x13: DataSize (16-bit LE)、
;   byte 0x14-0x15: LoadAddress (= 無視、 引数 HL 優先)、
;   byte 0x16-0x17: ExecuteAddress、 ...


; @name MTREAD
; @resident shared
; @param_count 1
; @calls sWORK, sWORK_TAPE, MT_CTRL_PLAY, MT_CTRL_STOP
; HL = load_addr (= 引数優先、 info block の LoadAddress は無視)
; 戻り: HL = data size (or $FFFF = error)
;
; IFF2 復元 guard (= Codex 指摘の DI 先順):
;   LD A, I → IFF2 を P/V flag に転写
;   DI      → 直後 DI (flags 壊さない)
;   PUSH AF → A + flags 保存
;   ... critical section (= DI 確定 状態) ...
;   POP AF
;   JP PO, skip → P/V = 0 (= 元 DI) なら EI skip
;   EI
;
; ただし MTREAD critical section 内の MT_CTRL_PLAY / MT_CTRL_STOP 呼出時、
; 内部 MT_CTRL routine が **sub CPU 通信のため一時 EI** する (= X1_compatible_rom
; 元 source 由来、 sub CPU $1900 への deck command 送出時の必要 sync)。 この間
; (= 数十 cycle、 OUT (C), $E9 + WAIT_80C49_WR 1 回程度) は IRQ 入り得る。
; 本 routine 全体としての設計は「MTREAD 中は DI で bit-level read 保護」 だが、
; deck control 部分 (= bit-stream read ではない) は短い EI window あり = PSG IRQ
; 等が間に入る可能性。 bit-stream read (= _mtload_block 内) は完全 DI 維持。
LD A, I
DI
PUSH AF
PUSH HL                  ; load_addr 保存
CALL MT_CTRL_PLAY        ; deck 再生再開 (= 1 段目 auto stop からの再開)

; info block (32 byte + checksum 2 byte) を MT_INFOBUF に read
LD BC, $1A01
LD HL, MT_INFOBUF
CALL .cli_entry          ; CMT info block load (= local routine)
JR C, .mtr_fail

; info block byte 0x12-0x13 (= DataSize LE) を DE に
LD HL, MT_INFOBUF + $12
LD E, (HL)
INC HL
LD D, (HL)               ; DE = data size

; data block を caller 指定 load_addr に read (= 引数 HL 優先)
POP HL                   ; HL = load_addr (= 引数復元)
PUSH HL                  ; stack 整合 (= 後の POP DE で discard)
LD BC, $1A01
CALL .clf_entry          ; CMT data file load (= local routine)
JR C, .mtr_fail

EX DE, HL                ; HL = data size (= 成功時戻り値)
JR .mtr_done

.mtr_fail:
LD HL, $FFFF             ; error 時 $FFFF
.mtr_done:
PUSH HL                  ; size or $FFFF 保存
CALL MT_CTRL_STOP        ; deck 停止
POP HL                   ; size or $FFFF 復元
POP DE                   ; load_addr discard (= stack 整合)
POP AF                   ; IFF2 復元
JP PO, .mtr_skip_ei      ; P/V = 0 (= 元 DI) なら EI skip
EI
.mtr_skip_ei:
RET

; --- 以下、 MTREAD 内部 ローカル helper (= 同 @name block 内、 linker 落とし回避) ---

; CMT info block read (= 32 byte + checksum 2 byte)
; X1_compatible_rom L1440-1467 fork、 引数: BC=$1A01, HL=info buffer, 戻り CY=error
.cli_entry:
CALL .mt_skip1111
JR C, .cli_exit
LD D, 40 - 1
CALL .mt_skip0
JR C, .cli_exit
LD D, 41
CALL .mt_skip1
JR C, .cli_exit
LD E, 34
.cli_1:
CALL .mt_rdbyte
LD (HL), D
INC HL
JR C, .cli_exit
DEC E
JR NZ, .cli_1
.cli_exit:
RET

; CMT data block read (= N byte + checksum 2 byte)
; X1_compatible_rom L1482-1516 fork、 引数: BC=$1A01, DE=size, HL=load addr (= 引数優先)
.clf_entry:
PUSH DE
CALL .mt_skip1111
JR C, .clf_exit
LD D, 20 - 1
CALL .mt_skip0
JR C, .clf_exit
LD D, 21
CALL .mt_skip1
JR C, .clf_exit
.clf_1:
CALL .mt_rdbyte
LD (HL), D
INC HL
JR C, .clf_exit
POP DE
DEC DE
PUSH DE
LD A, D
OR E
JR NZ, .clf_1
.clf_exit:
POP DE
RET

; CMT edge wait (= 立ち上がり)、 X1_compatible_rom L1639-1659 fork
.mt_edge:
IN A, (C)
RRCA
JR NC, .me_2
RRCA
JR C, .mt_edge
.me_1:
IN A, (C)
RRCA
JR NC, .me_2
RRCA
JR NC, .me_1
.me_2:
CCF
RET

; CMT 1 bit read (= edge + ~185µs window + read)、 X1_compatible_rom L1671-1687 fork
.mt_rdbit:
CALL .mt_edge
JR C, .mrb_exit
LD A, 35
.mrb_1:
DEC A
JR NZ, .mrb_1
IN A, (C)
AND $02
.mrb_exit:
RET

; CMT 1 bit read → D reg (= left shift + add bit)、 X1_compatible_rom L1698-1706 fork
.mt_rdbit_d:
RLC D
CALL .mt_rdbit
JR C, .mrbd_exit
JR Z, .mrbd_exit
INC D
.mrbd_exit:
RET

; CMT skip 255+ 1s + 0 1 個、 X1_compatible_rom L1719-1741 fork
.mt_skip1111:
PUSH DE
.ms1111_1:
LD D, $FF
.ms1111_2:
CALL .mt_rdbit
JR C, .ms1111_exit
JR Z, .ms1111_1
DEC D
JR NZ, .ms1111_2
.ms1111_3:
CALL .mt_rdbit
JR C, .ms1111_exit
JR NZ, .ms1111_3
.ms1111_exit:
POP DE
RET

; CMT skip D 個 0、 X1_compatible_rom L1753-1764 fork
.mt_skip0:
CALL .mt_rdbit
JR C, .ms0_exit
SCF
JR NZ, .ms0_exit
DEC D
JR NZ, .mt_skip0
OR A
.ms0_exit:
RET

; CMT skip D 個 1、 X1_compatible_rom L1776-1787 fork
.mt_skip1:
CALL .mt_rdbit
JR C, .ms1_exit
SCF
JR Z, .ms1_exit
DEC D
JR NZ, .mt_skip1
OR A
.ms1_exit:
RET

; CMT 1 byte read (= start bit + 8 data MSB)、 X1_compatible_rom L1801-1819 fork
.mt_rdbyte:
CALL .mt_rdbit
JR C, .mrby_exit
SCF
JR Z, .mrby_exit
LD D, 0
CALL .mt_rdbit_d
CALL .mt_rdbit_d
CALL .mt_rdbit_d
CALL .mt_rdbit_d
CALL .mt_rdbit_d
CALL .mt_rdbit_d
CALL .mt_rdbit_d
CALL .mt_rdbit_d
.mrby_exit:
RET


; @name MTREADJP
; @resident shared
; @param_count 2
; @calls sWORK, MTREAD
; HL = load_addr (= arg1)、 DE = jp_addr (= arg2、 SLANG MACHINE 規約)
; 成功: jp_addr に JP (= no-return)、 失敗: HL = $FFFF + RET
; raw stage 用 (= overlay 経由なら MTREAD + 関数呼出 推奨)
PUSH DE                  ; jp_addr 保存
CALL MTREAD              ; HL = data size or $FFFF
POP DE                   ; jp_addr 復元
; HL = $FFFF check (= 明示比較、 Codex 指摘で AND/INC 系の誤判定回避)
LD A, H
CP $FF
JR NZ, .mrj_ok
LD A, L
CP $FF
JR Z, .mrj_fail
.mrj_ok:
EX DE, HL                ; HL = jp_addr
JP (HL)                  ; jump (= no-return)
.mrj_fail:
LD HL, $FFFF
RET


; @name MT_CTRL_PLAY
; @resident shared
; @calls sWORK, MT_CTRL
; cassette deck 再生開始 (= sub CPU $1900 経由 command 2)
; X1_compatible_rom L1521-1526 fork
LD BC, $1A01
LD A, 2                  ; 2 = 再生 (READ) command
JP MT_CTRL


; @name MT_CTRL_STOP
; @resident shared
; @calls sWORK, MT_CTRL
; cassette deck 停止 (= sub CPU $1900 経由 command 1)
; X1_compatible_rom L1531-1537 fork、 EX AF,AF' で副 register 経由 flag 保存
EX AF, AF'
LD A, 1                  ; 1 = 停止 (STOP) command
CALL MT_CTRL
EX AF, AF'
RET


; @name MT_CTRL
; @resident shared
; @calls sWORK
; sub CPU $1900 経由 cassette deck command sender (= 汎用 deck control)
; 引数: A = command
;   0 = EJECT, 1 = STOP, 2 = READ (= 再生), 3 = FF, 4 = REW,
;   5 = APSS+1, 6 = APSS-1, A = WRITE
; X1_compatible_rom L1555-1574 fork
; ローカル helper: .wait_80c49_wr (= sub CPU write wait)
PUSH BC
PUSH AF
EI                       ; sub CPU 通信中 一時 EI (= 元 source comment「必要？」)
CALL .wait_80c49_wr
LD BC, $1900
LD A, $E9                ; $E9 = cassette deck control command
OUT (C), A
CALL .wait_80c49_wr
DI                       ; sub CPU 通信完了後 DI で interrupt 抑制復帰
CALL .wait_80c49_wr
LD BC, $1900
POP AF
OUT (C), A
POP BC
RET

; sub CPU 80C49 write wait (= 8255 port B bit 6 polling)
; X1_compatible_rom L1209-1216 fork
.wait_80c49_wr:
LD BC, $1A01
.w80_1:
IN A, (C)
AND $40
JR NZ, .w80_1
RET


; @name sWORK_TAPE
; @resident shared
; @works MT_INFOBUF:32
; MTREAD 内部用 info block 一時 buffer (= 32 byte BSS、 sWORK 統合)
; 注: 既存 sWORK の @works listing は libx1native_base.asm 内 sWORK で集約、
;     本 routine の @works は selective link 経由で __WORK__ に append される
;     (= SLANG runtime planner が同名重複を dedupe)。 別 @works 用 @name を
;     立てることで MTREAD link 時に MT_INFOBUF も link 対象になる。
RET
