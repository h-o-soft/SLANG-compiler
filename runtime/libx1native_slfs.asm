; libx1native_slfs.asm
; SLANG x1native runtime: SLFS (SLANG File System) disk I/O
;
; ゲーム専用 minimal file I/O、 D88 disk image から asset 読込。
; HuBASIC IPL 互換 boot sector + SLFS 独自 dir 構造、 read-only main + small
; save area (= Phase 2 以降)。 Phase 1 では FS_READ_BY_ID のみ提供。
;
; FDC routine は X1 互換 IPL ROM (= https://github.com/meister68k/X1_compatible_rom、
; Meister CC0) の LOADFILE / FDC_SEEK / FDC_READ 等を fork、 SLANG runtime style
; (= @resident shared + @works + IFF2 guard + @calls 明示) に適応。
;
; X1 IPL ROM が起動時に sector 0 read + main program load + jp まで行うため
; boot loader Z80 code は不要 (= sector 0 = HuBASIC IPL header 32 byte のみで起動)。
;
; Phase 1 は 2D 固定 (= 2 sides × 40 tracks × 16 sectors × 256 bytes = 320 KB)、
; geometry は runtime hardcoded、 packer 側で 2D 以外を作らない前提。


; ========================================================================
; FDC port (= X1 hardware spec、 SLANG runtime spec で EQU が selective link で
; 落ちるため hardcoded で書く):
;   $0FF8 = FDC コマンド / ステータス
;   $0FF9 = FDC トラックレジスタ
;   $0FFA = FDC セクタレジスタ
;   $0FFB = FDC データレジスタ
;   $0FFC = ドライブ no / サイド / モーター ON
;   drive 0 固定 (= Phase 1)
; ========================================================================


; ========================================================================
; X1FDC_WORK: SLFS work area provider
; ========================================================================
; FS_SECTOR_BUF (256 byte): superblock / dir / data 一時 load
; FS_SB_CACHE_FLAG (1 byte): superblock cache 済 flag (0=未init、非0=init済)
; FS_SB_DIR_START (2 byte): superblock の directory_start_sector
; FS_SB_ENTRY_COUNT (2 byte): superblock の directory_entry_count
; FS_SAVED_ID (1 byte): FS_READ_BY_ID call 間の id 保持
; FS_SAVED_BUFFER (2 byte): FS_READ_BY_ID call 間の buffer addr 保持

; @name X1FDC_WORK
; @resident shared
; @param_count 0
; @works FS_SECTOR_BUF:256,FS_SB_CACHE_FLAG:1,FS_SB_DIR_START:2,FS_SB_ENTRY_COUNT:2,FS_SAVED_ID:1,FS_SAVED_BUFFER:2
RET


; ========================================================================
; WAIT_SLFS1: 36.5µs delay (= FDC read cmd 後 DRQ ready 待ち)
; ========================================================================
; X1_compatible_rom WAIT1 fork。 146 clock = 36.5µs at 4 MHz。

; @name WAIT_SLFS1
; @resident shared
; @param_count 0
LD      A, 7
.wait_slfs1_1:
DEC     A
JR      NZ, .wait_slfs1_1
RET


; ========================================================================
; WAIT_FDC_BUSY_SLFS: FDC Busy 待ち
; ========================================================================
; entry 時 BC = $0FF8 でなければならない。
; X1_compatible_rom WAIT_FDC_BUSY fork。

; @name WAIT_FDC_BUSY_SLFS
; @resident shared
; @param_count 0
.wait_fdc_busy_slfs_1:
IN      A, (C)
AND     81h
JR      NZ, .wait_fdc_busy_slfs_1
RET


; ========================================================================
; FDC_CMD_SLFS: FDC command レジスタに命令を与えて完了を待つ
; ========================================================================
; パラメータ A = command 番号

; @name FDC_CMD_SLFS
; @resident shared
; @param_count 0
; @calls WAIT_FDC_BUSY_SLFS
LD      BC, $0FF8
OUT     (C), A
JP      WAIT_FDC_BUSY_SLFS


; ========================================================================
; FDC_RESTORE_SLFS: FDC restore (= head を track 0 に移動)
; ========================================================================

; @name FDC_RESTORE_SLFS
; @resident shared
; @param_count 0
; @calls FDC_CMD_SLFS
LD      A, 2
JP      FDC_CMD_SLFS


; ========================================================================
; FDC_SEEK_SLFS: FDC seek to track
; ========================================================================
; パラメータ A = track 番号
; 戻り値 NZ = エラー

; @name FDC_SEEK_SLFS
; @resident shared
; @param_count 0
; @calls FDC_CMD_SLFS
LD      BC, $0FFB
OUT     (C), A
LD      A, 1Eh                  ; seek + verify + head load + 6ms step
CALL    FDC_CMD_SLFS
IN      A, (C)
AND     99h                     ; bit 0 BUSY, bit 3 CRC, bit 4 RNF, bit 7 NR
RET


; ========================================================================
; FDC_READ_SLFS: 1 sector 読込み (= track 移動済前提)
; ========================================================================
; パラメータ A = sector 番号 (0-origin)、 HL = 読込み buffer addr
; 戻り値 A = status reg

; @name FDC_READ_SLFS
; @resident shared
; @param_count 0
; @calls WAIT_FDC_BUSY_SLFS,WAIT_SLFS1
LD      BC, $0FFA
INC     A                       ; 1-origin に変換
OUT     (C), A
LD      C, LOW($0FF8)
CALL    WAIT_FDC_BUSY_SLFS

LD      D, LOW($0FF8)
LD      E, LOW($0FFB)
LD      BC, $0FF8

LD      A, 80h                  ; read sector
OUT     (C), A
CALL    WAIT_SLFS1
.fdc_read_slfs_1:
IN      A, (C)
RRCA
JR      NC, .fdc_read_slfs_2
RRCA
JR      NC, .fdc_read_slfs_1
LD      C, E
IN      A, (C)
LD      (HL), A
INC     HL
LD      C, D
JR      .fdc_read_slfs_1
.fdc_read_slfs_2:
RLCA
RET


; ========================================================================
; FDC_LOAD_SECTORS_SLFS: 連続 sector 読込み (= LOADFILE fork)
; ========================================================================
; パラメータ:
;   A  = sector 数 (1..255、 0 入力時は 256 として処理)
;   DE = レコード番号 = (track << 9) | (side << 8) | sector_0origin
;        bit 15-9: track (0..127)、 bit 8: side、 bit 3-0: sector (0..15)
;   HL = 読込み buffer addr
; 戻り値:
;   Cy = エラー時 1、 成功時 0

; @name FDC_LOAD_SECTORS_SLFS
; @resident shared
; @param_count 0
; @calls FDC_SEEK_SLFS,FDC_READ_SLFS
OR      A                       ; clear Cy
EX      AF, AF'                 ; sector count を A' に退避

LD      A, E
RLCA
RL      D
RLCA
RL      D
RLCA
RL      D
RLCA
RL      D
LD      A, E
AND     0Fh
LD      E, A

.load_sectors_slfs_1:
LD      A, 1
AND     D                       ; side
LD      A, 0
JR      Z, .load_sectors_slfs_2
OR      10h
.load_sectors_slfs_2:
OR      80h                     ; motor ON
LD      BC, $0FFC
OUT     (C), A

LD      A, D
SRL     A
CALL    FDC_SEEK_SLFS
JR      NZ, .load_sectors_slfs_err

.load_sectors_slfs_3:
PUSH    DE
LD      A, E
CALL    FDC_READ_SLFS
POP     DE
AND     9Ch                     ; error bits: RNF / CRC / lost / write protect
JR      NZ, .load_sectors_slfs_err

EX      AF, AF'
DEC     A
JR      Z, .load_sectors_slfs_done
EX      AF, AF'

LD      A, E
INC     A
AND     0Fh
LD      E, A
JR      NZ, .load_sectors_slfs_3

LD      A, D
INC     A
LD      D, A
JR      .load_sectors_slfs_1

.load_sectors_slfs_done:
LD      A, 0
LD      BC, $0FFC
OUT     (C), A
OR      A                       ; Cy = 0
RET

.load_sectors_slfs_err:
LD      A, 0
LD      BC, $0FFC
OUT     (C), A
SCF
RET


; ========================================================================
; FS_READ_BY_ID: SLFS public API (= asset 読込み)
; ========================================================================
; SLANG public ABI:
;   @param_count 2
;   arg1 HL = id (L 使用、 H 無視、 範囲 0..255)
;   arg2 DE = load buffer (= sector round-up 分の容量必要)
;   戻り値 HL = 実 byte_size (1..65535)、 失敗時 HL = 0
;   成功時 CY = 0、 失敗時 CY = 1

; @name FS_READ_BY_ID
; @resident shared
; @param_count 2
; @calls X1FDC_WORK,FDC_LOAD_SECTORS_SLFS
; --- IFF2 guard entry (= 設計順 LD A,I; DI; PUSH AF) ---
LD      A, I
DI
PUSH    AF

; --- save id + buffer to work area ---
LD      A, L
LD      (FS_SAVED_ID), A
LD      (FS_SAVED_BUFFER), DE

; --- superblock cache ensure ---
LD      A, (FS_SB_CACHE_FLAG)
OR      A
JR      NZ, .fsrb_cache_ok
; load superblock (= logical sector 1 = cyl 0 head 0 sector 2)
; logical sector index = FDC_LOAD_SECTORS_SLFS の DE 入力と同じ encoding
; (= 2D 限定で lsec.bit 3-0 = sector_0origin、 bit 4 = side、 bit 15-5 = cyl)
LD      DE, 1
LD      HL, FS_SECTOR_BUF
LD      A, 1
CALL    FDC_LOAD_SECTORS_SLFS
JR      C, .fsrb_fail
; cache essential fields: +08 dir_start (2), +0A entry_count (2)
LD      HL, (FS_SECTOR_BUF + 08h)
LD      (FS_SB_DIR_START), HL
LD      HL, (FS_SECTOR_BUF + 0Ah)
LD      (FS_SB_ENTRY_COUNT), HL
LD      A, 1
LD      (FS_SB_CACHE_FLAG), A
.fsrb_cache_ok:

; --- id 範囲 check ---
LD      A, (FS_SAVED_ID)
LD      H, 0
LD      L, A
LD      DE, (FS_SB_ENTRY_COUNT)
OR      A                       ; clear Cy
SBC     HL, DE                  ; HL = id - entry_count
JR      NC, .fsrb_fail          ; id >= entry_count = 範囲外

; --- dir sector を load ---
; dir sector # = FS_SB_DIR_START + (id >> 4)
LD      A, (FS_SAVED_ID)
SRL     A
SRL     A
SRL     A
SRL     A                       ; A = id >> 4
LD      H, 0
LD      L, A
LD      DE, (FS_SB_DIR_START)
ADD     HL, DE                  ; HL = dir sector logical index
EX      DE, HL                  ; DE = lsec (= FDC_LOAD_SECTORS_SLFS 入力)
LD      HL, FS_SECTOR_BUF
LD      A, 1
CALL    FDC_LOAD_SECTORS_SLFS
JR      C, .fsrb_fail

; --- entry offset = (id & 15) * 16 ---
LD      A, (FS_SAVED_ID)
AND     0Fh
SLA     A
SLA     A
SLA     A
SLA     A                       ; A = entry offset 内 byte 位置
LD      H, 0
LD      L, A
LD      DE, FS_SECTOR_BUF
ADD     HL, DE                  ; HL → entry 先頭

; --- entry parse: +0C start_sector (2 LE), +0E byte_size (2 LE) ---
LD      DE, 0Ch
ADD     HL, DE                  ; HL → +0C
LD      E, (HL)
INC     HL
LD      D, (HL)
INC     HL                      ; DE = start_sector (= logical sector index)
LD      C, (HL)
INC     HL
LD      B, (HL)                 ; BC = byte_size

; byte_size 0 check (= packer reject 済だが念のため)
LD      A, B
OR      C
JR      Z, .fsrb_fail

; --- sector count = ceil(byte_size / 256) = (byte_size + 255) >> 8 ---
; 計算: DEC BC; INC B; A = B  (= ceil(BC / 256)、 BC=0 で破綻、 上で 0 check 済)
PUSH    BC                      ; byte_size 保存
DEC     BC
INC     B
LD      A, B                    ; A = sector count (= 1..256、 256 は 0 入力扱い)

; --- data sector 連続 read ---
; DE = start_sector (= logical sector index、 そのまま FDC_LOAD_SECTORS_SLFS に渡す)
LD      HL, (FS_SAVED_BUFFER)
CALL    FDC_LOAD_SECTORS_SLFS
POP     BC                      ; byte_size 復元
JR      C, .fsrb_fail2

; --- success: HL = byte_size ---
LD      H, B
LD      L, C
POP     AF
JP      PO, .fsrb_skip_ei
EI
.fsrb_skip_ei:
OR      A                       ; Cy = 0
RET

; --- fail with byte_size 退避済 stack 状態 ---
.fsrb_fail2:
                                ; BC 既に pop 済、 stack 状態 = saved IFF2 のみ
.fsrb_fail:
POP     AF                      ; IFF2 restore
JP      PO, .fsrb_skip_ei_fail
EI
.fsrb_skip_ei_fail:
LD      HL, 0
SCF
RET
