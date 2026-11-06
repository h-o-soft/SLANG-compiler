; libx1native_hufs.asm
; SLANG x1native runtime: HUFS (Hu-BASIC と同じ形の 2D ディスク) の読み書き
;
; x1native_hufs 環境のライブラリ (docs/HUFS.md)。ディスクの形は Hu-BASIC (CZ-8FB01) と同じ
; (レコード 14 が FAT、16〜31 がディレクトリ、1 クラスタ = 16 レコード)。
;
; FDC の下回りは libx1native_slfs.asm (X1 互換 IPL ROM の fork) を写して、名前を _HUFS にしたもの。
; slfs の側には手を入れないため写している。片方を直したら、もう片方も見ること。
; 写したものとの違い:
;   - 命令を出す前の Busy / Not Ready の待ちに上限を付けた (ディスクがないときに戻らなくならないように)。
;     上限を越えたら FDC に強制割り込み ($D0) を出して、エラーの状態 ($80 = Not Ready) を返す。
;     転送中の DRQ の待ちは元のままの短い輪にする (数えると 1 バイトの間に合わず Lost Data になる)。
;     転送の命令は FDC が自分で終わらせる (Not Ready なら始めない、セクタがなければ RNF) ので、そこには上限が要らない
;   - 書き込み禁止 (状態の bit 6) を、ほかの書き込みエラーと分けて返す
;
; 2D 固定。ドライブ 0 固定 (試験のために、SLANG の CONST ASM HUFS_DRIVE でドライブを変えられる)。
;
; FDC port:
;   $0FF8 = コマンド / 状態、$0FF9 = トラック、$0FFA = セクタ、$0FFB = データ
;   $0FFC = ドライブ番号 / 面 / モーター ON


; ========================================================================
; WAIT_HUFS1: 36.5µs 待つ (FDC に読み書きの命令を出したあと、DRQ が立つまで)
; ========================================================================

; @name WAIT_HUFS1
; @resident shared
; @param_count 0
LD      A, 7
.wait_hufs1_1:
DEC     A
JR      NZ, .wait_hufs1_1
RET


; ========================================================================
; WAIT_FDC_BUSY_HUFS: Busy と Not Ready が落ちるのを待つ (上限つき)
; ========================================================================
; 入口: BC = $0FF8
; 戻り値: Cy = 0 なら落ちた。Cy = 1 なら時間切れ (約 13 秒。FDC に強制割り込みを出してある)
; DE・HL は壊さない

; @name WAIT_FDC_BUSY_HUFS
; @resident shared
; @param_count 0
PUSH    DE
PUSH    HL
LD      L, 16                   ; 16 × 65536 回 (1 回 52T、4MHz で約 13 秒)
LD      DE, 0
.wfbh_1:
IN      A, (C)
AND     81h                     ; bit 0 BUSY、bit 7 NOT READY
JR      Z, .wfbh_ok
DEC     DE
LD      A, D
OR      E
JR      NZ, .wfbh_1
DEC     L
JR      NZ, .wfbh_1
LD      A, 0D0h                 ; 強制割り込み (命令を打ち切る)
OUT     (C), A
POP     HL
POP     DE
SCF
RET
.wfbh_ok:
POP     HL
POP     DE
RET                             ; AND の結果で Cy = 0


; ========================================================================
; FDC_CMD_HUFS: FDC に命令を出して、終わるのを待つ
; ========================================================================
; パラメータ A = 命令
; 戻り値 Cy = 1 なら時間切れ

; @name FDC_CMD_HUFS
; @resident shared
; @param_count 0
; @calls WAIT_FDC_BUSY_HUFS
LD      BC, $0FF8
OUT     (C), A
JP      WAIT_FDC_BUSY_HUFS


; ========================================================================
; FDC_SEEK_HUFS: トラックへシーク
; ========================================================================
; パラメータ A = トラック番号 (シリンダ)
; 戻り値 NZ = エラー (時間切れも NZ)

; @name FDC_SEEK_HUFS
; @resident shared
; @param_count 0
; @calls FDC_CMD_HUFS
LD      BC, $0FFB
OUT     (C), A
LD      A, 1Eh                  ; seek + verify + head load + 6ms step
CALL    FDC_CMD_HUFS
JR      C, .fsh_tmo
IN      A, (C)
AND     99h                     ; bit 0 BUSY、bit 3 CRC、bit 4 RNF、bit 7 NR
RET
.fsh_tmo:
LD      A, 80h
OR      A                       ; NZ
RET


; ========================================================================
; FDC_XFER_HUFS: 1 セクタを読む / 書く (トラックへの移動は済んでいる前提)
; ========================================================================
; パラメータ A = セクタ番号 (0 から)、HL = 緩衝、E = 命令 (80h なら読む、0A0h なら書く)
; 戻り値 A = 状態 (時間切れは 80h)

; @name FDC_XFER_HUFS
; @resident shared
; @param_count 0
; @calls WAIT_FDC_BUSY_HUFS,WAIT_HUFS1
LD      BC, $0FFA
INC     A                       ; 1 から数える番号に
OUT     (C), A
LD      C, LOW($0FF8)
CALL    WAIT_FDC_BUSY_HUFS
JR      C, .fxh_tmo

LD      A, E
LD      D, LOW($0FF8)
LD      E, LOW($0FFB)
LD      BC, $0FF8
OUT     (C), A
CP      80h
JR      NZ, .fxh_write
CALL    WAIT_HUFS1
.fxh_r1:
IN      A, (C)
RRCA
JR      NC, .fxh_done
RRCA
JR      NC, .fxh_r1
LD      C, E
IN      A, (C)
LD      (HL), A
INC     HL
LD      C, D
JR      .fxh_r1

.fxh_write:
CALL    WAIT_HUFS1
.fxh_w1:
IN      A, (C)
RRCA
JR      NC, .fxh_done
RRCA
JR      NC, .fxh_w1
LD      C, E
LD      A, (HL)
OUT     (C), A
INC     HL
LD      C, D
JR      .fxh_w1

.fxh_done:
RLCA                            ; 状態の bit を戻す
RET

.fxh_tmo:
LD      A, 80h                  ; Not Ready としてエラーにする
RET


; ========================================================================
; FDC_RWREC_HUFS: レコード 1 つを読む / 書く (モーター・面・シークを含む)
; ========================================================================
; パラメータ:
;   DE = レコード番号 (0〜1279。bit 3-0 セクタ、bit 4 面、bit 15-5 シリンダ。slfs の logical sector と同じ)
;   HL = 緩衝
;   A  = 80h なら読む、0A0h なら書く
; 戻り値:
;   Cy = 0 成功
;   Cy = 1 失敗。A = 1 書き込み禁止、A = 2 そのほか (FDC のエラー・時間切れ・ディスクなし)
; 割り込みは、呼ぶ側で止めておくこと

; @name FDC_RWREC_HUFS
; @resident shared
; @param_count 0
; @calls FDC_SEEK_HUFS,FDC_XFER_HUFS,WAIT_HUFS1
; @works HUFS_SEC:1
PUSH    AF                      ; 命令
LD      A, E
AND     0Fh
LD      (HUFS_SEC), A           ; セクタ (0 から)
LD      A, E
AND     10h                     ; 面
OR      80h                     ; モーター ON
#if exists HUFS_DRIVE
OR      HUFS_DRIVE
#endif
LD      BC, $0FFC
OUT     (C), A
; シリンダ = レコード番号 >> 5
LD      A, E
SRL     D
RRA
SRL     D
RRA
SRL     D
RRA
SRL     D
RRA
SRL     D
RRA
PUSH    HL
CALL    FDC_SEEK_HUFS
POP     HL
JR      NZ, .frh_err_pop
POP     AF
LD      E, A                    ; E = 命令
LD      A, (HUFS_SEC)
PUSH    DE
CALL    FDC_XFER_HUFS
POP     DE
LD      B, A                    ; 状態
LD      A, E
CP      80h
LD      A, B
JR      Z, .frh_rchk
AND     0FCh                    ; 書く: 書き込み禁止・書き込み失敗・RNF・CRC・lost・NR
JR      Z, .frh_ok
; 書き込み禁止か: 強制割り込みを出して、Type I の状態の bit 6 (ドライブの書き込み禁止) を見る
; (書き込みの命令の状態では、書き込み禁止を Write Fault ($20) で返すエミュレータがあるため)
LD      BC, $0FF8
LD      A, 0D0h
OUT     (C), A
CALL    WAIT_HUFS1
IN      A, (C)
AND     40h
JR      Z, .frh_err
LD      A, 1                    ; 書き込み禁止
JR      .frh_fail
.frh_rchk:
AND     9Ch                     ; 読む: NR・RNF・CRC・lost
JR      Z, .frh_ok
JR      .frh_err
.frh_err_pop:
POP     AF
.frh_err:
LD      A, 2
.frh_fail:
PUSH    AF
CALL    .frh_motor_off
POP     AF
SCF
RET
.frh_ok:
CALL    .frh_motor_off
OR      A                       ; Cy = 0
RET
.frh_motor_off:
LD      A, 0
LD      BC, $0FFC
OUT     (C), A
RET


; ========================================================================
; FRAWREAD / FRAWWRITE: レコードを 1 つ、ファイルの仕組みを通らずに読む / 書く
; ========================================================================
; SLANG の公開の関数 (2 引数):
;   arg1 HL = レコード番号 (0〜1279)
;   arg2 DE = 緩衝 (256 バイト)
;   戻り値 HL = 0 成功、$FF09 FDC の失敗・時間切れ・ディスクなし、$FF0B 書き込み禁止、$FF0E 範囲外
; 割り込みの状態は、入口のものに戻して返る

; @name FRAWREAD
; @resident shared
; @param_count 2
; @calls FDC_RWREC_HUFS,HUFS_RAWIO
LD      A, 80h
JP      HUFS_RAWIO


; @name FRAWWRITE
; @resident shared
; @param_count 2
; @calls FDC_RWREC_HUFS,HUFS_RAWIO
LD      A, 0A0h
JP      HUFS_RAWIO


; FRAWREAD / FRAWWRITE の共通: A = 命令、HL = レコード番号、DE = 緩衝

; @name HUFS_RAWIO
; @resident shared
; @param_count 0
; @calls FDC_RWREC_HUFS
PUSH    AF
PUSH    DE
LD      DE, 1280
OR      A
SBC     HL, DE
ADD     HL, DE                  ; HL を戻す。Cy = 1 なら HL < 1280
POP     DE
JR      C, .hri_ok
POP     AF
LD      HL, $FF0E
RET
.hri_ok:
EX      DE, HL                  ; DE = レコード番号、HL = 緩衝
POP     AF
LD      B, A                    ; 命令
LD      A, I                    ; 割り込みの状態 (P/V = IFF2)
DI
PUSH    AF
LD      A, B
CALL    FDC_RWREC_HUFS
JR      NC, .hri_success
CP      1
LD      HL, $FF0B
JR      Z, .hri_ret
LD      HL, $FF09
JR      .hri_ret
.hri_success:
LD      HL, 0
.hri_ret:
POP     AF
RET     PO                      ; 入口で割り込みが止まっていた
EI
RET
