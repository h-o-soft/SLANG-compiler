; ====================================================================================================
; banjo_driver_x1.asm — banjo driver (X1) の master build file
;
; banjo Core (SYS=0、 X1 local mode) + chip driver + X1 jump table wrapper を 1 つの固定 ORG bin に
; まとめる。 SLANG からは「実行時にこの bin を BANJO_BASE にロードし、 先頭 jump table を叩く」。
; 曲データは別ビルド (driver の label を EQU 注入して曲単独 assemble、 banjo_extract_syms.py)。
;
; build パラメータ (wla-z80 -D で上書き可):
;   BANJO_ORG       driver の配置先 (既定 $8000)
;   BANJO_RAM_BASE  banjo work RAM の配置先 (既定 $C000、 user program/data 不可の予約領域)
;   BANJO_MAX_CHANNELS  song_channels 確保数 (既定 8 = OPM。 wrapper 参照)
;   BANJO_USE_AY / BANJO_USE_OPM  chip 選択 (既定は driver master が include する chip による)
;
; build は runtime/x1/banjo/ を cwd に wla-z80 を実行する前提 (= upstream/... 相対 include 解決)。
; ====================================================================================================

; banjo Core は X1 local mode で統一 (SYS=1 SMS / 2 MSX / 3 PC88 の check_hardware・init を含めない)。
; SYS=0 では banjo_init が呼ぶ banjo_init_system_call を wrapper が自前定義する。
.ifndef BANJO_SYS
.define BANJO_SYS 0
.endif
.ifndef BANJO_ORG
.define BANJO_ORG $8000
.endif
.ifndef BANJO_RAM_BASE
.define BANJO_RAM_BASE $C000
.endif
.ifndef BANJO_MAX_CHANNELS
.define BANJO_MAX_CHANNELS 8        ; song_channels 確保数 (OPM=8 / AY=3 / AY+OPM=11)。 wrapper も参照
.endif

; struct channel / music_state / BANJO_HAS_* (guard 付きなので Core 側の再 include と二重でも安全)
.include "upstream/banjo_defines_wladx.inc"

.MEMORYMAP
DEFAULTSLOT 0
SLOTSIZE $4000
SLOT 0 BANJO_ORG
SLOT 1 BANJO_RAM_BASE
.ENDME
.ROMBANKMAP
BANKSTOTAL 1
BANKSIZE $4000
BANKS 1
.ENDRO

; banjo work RAM (song_channels)。 banjo.asm 内 .RAMSECTION "BANJO_RAM" も linkfile の
; [ramsections] で SLOT 1 (= BANJO_RAM_BASE) に固定する。
.RAMSECTION "BANJO_X1_RAM" BANK 0 SLOT 1
song_channels: INSTANCEOF channel BANJO_MAX_CHANNELS
.ENDS

; --- driver bin 先頭 (= BANJO_BASE) に jump table を置く ---
; .BANK/.ORG で SLOT 0 (ROM) 帰属を明示しないと、 linkfile [ramsections] の RAM 再配置が効かず
; RAMSECTION が ROM 側に落ちて自壊する (検証済 experiment と同構造に揃える)。
.BANK 0 SLOT 0
.ORG $0000      ; == memory BANJO_ORG (SLOT 0)
.include "banjo_x1_wrapper.asm"

; --- banjo Core (SYS=0)。 banjo.asm は自分の dir 基準 include なので incdir 切替 ---
.incdir "upstream/banjo"
.include "banjo.asm"
.incdir "."

; --- chip driver (build 時に選択) ---
.ifdef BANJO_USE_OPM
.include "banjo_opm_x1.asm"
.endif
.ifdef BANJO_USE_AY
.include "banjo_ay_x1.asm"
.include "banjo_sfx_x1.asm"     ; AY 専用 SFX エンジン (banjo_ay_x1.asm / banjo.asm の後)
.endif

; driver 末尾 marker (= trim/サイズ算出の参考、 trim_to_end は最大使用アドレス基準)
.SECTION "BANJO_DRIVER_END" FREE
banjo_driver_end:
.ENDS
