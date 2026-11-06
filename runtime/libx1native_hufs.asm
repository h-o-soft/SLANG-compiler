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
;   - 同じシリンダならシークしない。失敗したら 1 回だけシークし直してやり直す (FDC_RWREC_HUFS)
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
; 戻り値 NZ = エラー (時間切れも NZ)。DE・HL は壊さない

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
;
; シリンダが FDC のトラックのレジスタ (最後にシークしたシリンダ) と同じなら、シークしない
; (verify つきのシークは、ヘッドを下ろす待ちのあいだに次のセクタを逃がして、ほぼ 1 周待つため)。
; 失敗したら 1 回だけ、ヘッドを 0 トラックに戻して (restore。verify つき) からシークし直して、やり直す
; (Not Ready・時間切れと書き込み禁止はやり直さない)。

; @name FDC_RWREC_HUFS
; @resident shared
; @param_count 0
; @calls FDC_CMD_HUFS,FDC_SEEK_HUFS,FDC_XFER_HUFS,WAIT_HUFS1
; @works HUFS_SEC:1
PUSH    HL                      ; 緩衝
PUSH    DE                      ; レコード番号
OR      A                       ; 退避する F の Cy を、やり直しの印に使う (0 = 1 回目)
PUSH    AF                      ; 命令
.frh_try:
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
LD      D, A                    ; D = シリンダ
LD      BC, $0FF9
IN      A, (C)                  ; トラックのレジスタ
CP      D
JR      Z, .frh_xfer            ; 同じシリンダ: シークしない
LD      A, D
CALL    FDC_SEEK_HUFS
JR      NZ, .frh_err
.frh_xfer:
POP     AF
PUSH    AF
LD      E, A                    ; E = 命令
LD      A, (HUFS_SEC)
CALL    FDC_XFER_HUFS
LD      B, A                    ; 状態
POP     AF
PUSH    AF
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
JR      Z, .frh_err             ; A = 0: やり直す
LD      A, 1                    ; 書き込み禁止
JR      .frh_fail
.frh_rchk:
AND     9Ch                     ; 読む: NR・RNF・CRC・lost
JR      Z, .frh_ok
.frh_err:                       ; A = 状態
LD      B, A
POP     AF                      ; 命令。Cy = 1 ならやり直しも失敗した
JR      C, .frh_err2
BIT     7, B
JR      NZ, .frh_err2           ; Not Ready・時間切れ
POP     DE                      ; レコード番号
POP     HL                      ; 緩衝
PUSH    HL
PUSH    DE
SCF                             ; やり直しの印
PUSH    AF
LD      A, 0Eh                  ; restore + verify + head load (トラックのレジスタを 0 に合わせ直す。失敗は後で分かる)
CALL    FDC_CMD_HUFS
JR      .frh_try
.frh_err2:
PUSH    AF
LD      A, 2
.frh_fail:
SCF
JR      .frh_end
.frh_ok:
XOR     A                       ; Cy = 0
.frh_end:
POP     DE                      ; 退避した命令・レコード番号・緩衝を捨てる
POP     DE
POP     DE
PUSH    AF
XOR     A                       ; モーター OFF
LD      BC, $0FFC
OUT     (C), A
POP     AF
RET


; ========================================================================
; FRAWREAD / FRAWWRITE: レコードを 1 つ、ファイルの仕組みを通らずに読む / 書く
; ========================================================================
; SLANG の公開の関数 (2 引数):
;   arg1 HL = レコード番号 (0〜1279)
;   arg2 DE = 緩衝 (256 バイト)
;   戻り値 HL = 0 成功、$FF09 FDC の失敗・時間切れ・ディスクなし、$FF0B 書き込み禁止、$FF0E 範囲外、
;              $FF04 (FRAWWRITE) 開いているファイルがあるときにレコード 0〜31 へ書こうとした
; 割り込みの状態は、入口のものに戻して返る
; ファイルの仕組みの FAT・ディレクトリの写しは、開いているファイルがないときの FOPEN などで読み直すので、
; 開いているものがないときの FRAWWRITE のあとも、写しが古いままにはならない

; @name FRAWREAD
; @resident shared
; @param_count 2
; @calls FDC_RWREC_HUFS,HUFS_RAWIO
LD      A, 80h
JP      HUFS_RAWIO


; @name FRAWWRITE
; @resident shared
; @param_count 2
; @calls FDC_RWREC_HUFS,HUFS_RAWIO,HUFS_OPENW
; 開いているファイルがあるときに、レコード 0〜31 (IPL・FAT・ディレクトリ) へ書くのはエラー ($FF04)
LD      A, (HUFS_OPENCNT)
OR      A
JR      Z, .frw_ok
LD      A, H
OR      A
JR      NZ, .frw_ok
LD      A, L
CP      32
JR      NC, .frw_ok
LD      HL, $FF04
RET
.frw_ok:
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


; ========================================================================
; ファイル API (段階 1a): FOPEN / FCLOSE / FREAD / FWRITE / FGETC / FPUTC / FSEEK ほか (docs/HUFS.md)
; ========================================================================
; 呼び方 (引数 HL・DE・BC、戻り値 HL) は、ほかの環境 (lsx・cpm など) の FOPEN 系とそろえる。
; 戻り値と細かい意味は、この環境のもの (docs/HUFS.md)。
;
; 中の約束:
;   - IX = ファイル 1 本の表 (FCB)。公開の関数は IX を退避して戻す (IY は SLANG の物なので使わない)
;   - 中の関数のエラー: Cy = 1、A = エラーの番号の下のバイト ($01〜$0F)。成功は Cy = 0
;   - 公開の関数のエラー: HL = $FF00 + 番号
;     $01 ない、$02 ディスクがいっぱい、$03 番号が悪い、$04 開いている、$05 開いていない、$06 モードが合わない、
;     $07 名前が悪い、$08 ディレクトリがいっぱい、$09 FDC の失敗、$0A 同じ名前がある、$0B 書き込み禁止、
;     $0C 扱えない項目、$0D 壊れた FAT、$0E 範囲外
;   - FAT の写し (0〜79 番の 80 バイト) は 1 つで、すべての番号から使う。開いているファイルがないときの FOPEN などで、
;     ディスクから読み直す
;   - FAT・ディレクトリの書き込みに失敗したら、以後このディスクには書かない (HUFS_WLOCK。書く操作は $FF09)
;
; FCB (1 本 272 バイト):
;   +0  状態 (0 = 閉じている。開いているときは $80 + mode の下 2 ビット)
;   +1  ディレクトリの項目の番号 (0〜127)
;   +2  頭のクラスタ
;   +3  種類 (項目の +0)
;   +4  今の位置 (3 バイト: バイト・レコードの下・レコードの上。24 ビットのバイト位置)
;   +7  論理の長さ (3 バイト。同じ形)
;   +10 確保したレコード数 (2 バイト)
;   +12 緩衝に入っているレコード (ファイルの頭から数える。$FFFF = なし)
;   +14 緩衝の変更あり
;   +15 最後のエラー (FERR)
;   +16 緩衝 (256 バイト)
; ファイル番号 0・1 の FCB はライブラリが持つ (HUFS_FCBS)。2〜15 は FSETBUF で渡された場所を使う。


; 開いているファイルの数 (FRAWWRITE も見るので、ほかの作業と分ける)

; @name HUFS_OPENW
; @resident shared
; @param_count 0
; @works HUFS_OPENCNT:1
;


; @name HUFS_CORE
; @resident shared
; @param_count 0
; @calls HUFS_RAWIO,HUFS_OPENW
; @works HUFS_FAT:80,HUFS_DIRBUF:256,HUFS_DIRREC:1,HUFS_WLOCK:1,HUFS_NAME:16,HUFS_FREEIDX:1,HUFS_OMODE:1,HUFS_FCBTAB:28,HUFS_FCBS:544

; ---- HUFS_RD / HUFS_WR: レコード 1 つを読む / 書く ----
; 入口 DE = レコード番号、HL = 緩衝。戻り値 Cy・A。AF・BC・DE・HL を壊す
HUFS_RD:
LD      A, 80h
JR      HUFS_RW1
HUFS_WR:
#if exists HUFS_STOPAFTER
; 試験のためだけの仕掛け: HUFS_STOPAFTER 回書いたら、次の書き込みの前で止まる (途中で止まったディスクを作る)
PUSH    HL
LD      HL, .stopcnt
INC     (HL)
LD      A, (HL)
POP     HL
CP      HUFS_STOPAFTER+1
JR      C, .go
DI
HALT
.stopcnt:
DB      0
.go:
#endif
LD      A, (HUFS_WLOCK)
OR      A
LD      A, 9
SCF
RET     NZ
LD      A, 0A0h
HUFS_RW1:
EX      DE, HL
CALL    HUFS_RAWIO
LD      A, H
OR      A
RET     Z
LD      A, L
SCF
RET

; ---- FAT・ディレクトリを読む / 書く ----
; FAT の写しは、2D で使う 0〜79 番だけを持つ。書くときは、レコード 14 をディレクトリの緩衝に読み直して
; 0〜79 番だけを入れ替えて書く (80〜255 番はディスクにあるまま残す。ディレクトリの写しはそのとき捨てる)。
; 書き込みに失敗したら、以後このディスクには書かない (HUFS_WLOCK)
HUFS_FATRD:
CALL    HUFS_FAT14
RET     C
LD      HL, HUFS_DIRBUF
LD      DE, HUFS_FAT
LD      BC, 80
LDIR
RET
HUFS_FATWR:
CALL    HUFS_FAT14
RET     C
LD      HL, HUFS_FAT
LD      DE, HUFS_DIRBUF
LD      BC, 80
LDIR
LD      HL, HUFS_DIRBUF
LD      DE, 14
JR      HUFS_METAW
HUFS_FAT14:
XOR     A
LD      (HUFS_DIRREC), A
LD      HL, HUFS_DIRBUF
LD      DE, 14
JP      HUFS_RD
HUFS_DIRWR:
LD      HL, HUFS_DIRREC
LD      E, (HL)
LD      D, 0
LD      HL, HUFS_DIRBUF
HUFS_METAW:
CALL    HUFS_WR
RET     NC
LD      HL, HUFS_WLOCK
LD      (HL), 1
LD      HL, HUFS_DIRREC
LD      (HL), 0
RET

; ---- HUFS_ENTP: ディレクトリの項目の場所 ----
; 入口 A = 項目の番号 (0〜127)。戻り値 HL = HUFS_DIRBUF の中の項目 (そのレコードを読んである)
HUFS_ENTP:
LD      C, A
RRCA
RRCA
RRCA
AND     0Fh
ADD     A, 16                   ; レコード番号
LD      HL, HUFS_DIRREC
CP      (HL)
JR      Z, .hit
LD      (HL), 0
PUSH    BC
PUSH    AF
LD      E, A
LD      D, 0
LD      HL, HUFS_DIRBUF
CALL    HUFS_RD
POP     DE                      ; D = レコード番号
POP     BC
RET     C
LD      A, D
LD      (HUFS_DIRREC), A
.hit:
LD      A, C
AND     7
RRCA
RRCA
RRCA                            ; × 32
LD      L, A
LD      H, 0
LD      DE, HUFS_DIRBUF
ADD     HL, DE
RET

; ---- HUFS_FATP: A = クラスタ → HL = FAT の写しの中の場所 (A を壊す) ----
HUFS_FATP:
LD      HL, HUFS_FAT
ADD     A, L
LD      L, A
RET     NC
INC     H
RET

; ---- HUFS_ALLOC: 空いているクラスタ (2〜79) のうち、番号のいちばん小さいもの → A (なければ Cy・A = 2) ----
; 0・1 はシステムの領域 (IPL・FAT・ディレクトリ) なので、FAT の中身によらず取らない
HUFS_ALLOC:
LD      HL, HUFS_FAT+2
LD      BC, 78*256+2
.l:
LD      A, (HL)
OR      A
JR      Z, .f
INC     HL
INC     C
DJNZ    .l
LD      A, 2
SCF
RET
.f:
LD      A, C
RET

; ---- HUFS_CHAIN: 鎖をたどって確かめる ----
; 入口 A = 頭のクラスタ。戻り値 BC = レコードの数、A = 最後のクラスタ。壊れていたら Cy・A = $0D
HUFS_CHAIN:
LD      D, 81
LD      BC, 0
.l:
LD      E, A
CP      2
JR      C, .bad                 ; 0・1 はシステムの領域
CP      80
JR      NC, .bad
DEC     D
JR      Z, .bad                 ; 輪になっている
CALL    HUFS_FATP
LD      A, (HL)
OR      A
JR      Z, .bad                 ; 空きを指している
CP      80h
JR      NC, .last
LD      HL, 16
ADD     HL, BC
LD      B, H
LD      C, L
JR      .l
.last:
CP      90h
JR      NC, .bad
AND     0Fh
INC     A
ADD     A, C
LD      C, A
LD      A, 0
ADC     A, B
LD      B, A
LD      A, E
OR      A
RET
.bad:
LD      A, 0Dh
SCF
RET

; ---- HUFS_FREECH: A = クラスタ から後ろの鎖を、写しの上で空きにする ----
HUFS_FREECH:
LD      B, 80
.l:
CP      80
RET     NC                      ; 終わり ($80〜) か範囲外
CP      2
RET     C                       ; 0・1 (システムの領域) は空きにしない
CALL    HUFS_FATP
LD      A, (HL)
LD      (HL), 0
DJNZ    .l
RET

; ---- HUFS_SYNC: 開いているファイルがなければ、FAT を読み直し、ディレクトリの写しを捨てる ----
HUFS_SYNC:
LD      A, (HUFS_OPENCNT)
OR      A
RET     NZ
LD      (HUFS_DIRREC), A
JP      HUFS_FATRD

; ---- HUFS_PARSE: DE = 名前 (0 で終わる) → HUFS_NAME (名前 13 + 拡張子 3、空白で埋める) ----
; 頭の "0:" は飛ばす。最後の '.' で分ける。名前 1〜13、拡張子 0〜3。$00〜$1F・'"'・':' は使えない (Cy・A = 7)
HUFS_PARSE:
LD      A, (DE)
CP      '0'
JR      NZ, .p1
INC     DE
LD      A, (DE)
DEC     DE
CP      ':'
JR      NZ, .p1
INC     DE
INC     DE
.p1:
LD      HL, HUFS_NAME
LD      B, 16
.fill:
LD      (HL), ' '
INC     HL
DJNZ    .fill
LD      H, D
LD      L, E
LD      BC, 0                   ; 最後の '.' の場所
PUSH    DE
.scan:
LD      A, (HL)
OR      A
JR      Z, .end
CP      20h
JR      C, .bad
CP      '"'
JR      Z, .bad
CP      ':'
JR      Z, .bad
CP      '.'
JR      NZ, .s2
LD      B, H
LD      C, L
.s2:
INC     HL
JR      .scan
.end:
LD      A, B
OR      C
JR      NZ, .s3
LD      B, H                    ; '.' がない: 名前は終わりまで
LD      C, L
.s3:
POP     DE                      ; DE = 名前の頭
PUSH    HL                      ; 終わり
LD      H, B
LD      L, C
OR      A
SBC     HL, DE                  ; 名前の長さ
LD      A, H
OR      A
JR      NZ, .bad
LD      A, L
OR      A
JR      Z, .bad
CP      14
JR      NC, .bad
LD      C, A
LD      B, 0
EX      DE, HL
LD      DE, HUFS_NAME
LDIR                            ; HL = '.' か終わり
POP     DE                      ; DE = 終わり
LD      A, (HL)
CP      '.'
JR      NZ, .ok
INC     HL
EX      DE, HL
OR      A
SBC     HL, DE                  ; 拡張子の長さ
LD      A, H
OR      A
JR      NZ, .bad1
LD      A, L
CP      4
JR      NC, .bad1
OR      A
RET     Z
LD      C, A
LD      B, 0
EX      DE, HL
LD      DE, HUFS_NAME+13
LDIR
.ok:
OR      A
RET
.bad:
POP     DE
.bad1:
LD      A, 7
SCF
RET

; ---- HUFS_FIND: HUFS_NAME をディレクトリから探す ----
; 戻り値 Z: あった (A = 番号、HL = 項目)。NZ: ない。Cy: 読めなかった
; HUFS_FREEIDX に、最初の空き (消したもの $00 か終わり $FF) の番号を残す ($FF = 空きがない)
HUFS_FIND:
LD      A, 0FFh
LD      (HUFS_FREEIDX), A
XOR     A
.l:
PUSH    AF
CALL    HUFS_ENTP
JR      C, .err
LD      A, (HL)
OR      A
JR      Z, .free
INC     A
JR      Z, .endm
PUSH    HL
INC     HL
LD      DE, HUFS_NAME
LD      B, 16
.c:
LD      A, (DE)
CP      (HL)
JR      NZ, .cn
INC     HL
INC     DE
DJNZ    .c
POP     HL
POP     AF
CP      A                       ; Z、Cy = 0
RET
.cn:
POP     HL
.next:
POP     AF
INC     A
CP      128
JR      NZ, .l
JR      .nf
.free:
LD      A, (HUFS_FREEIDX)
INC     A
JR      NZ, .next
POP     AF
PUSH    AF
LD      (HUFS_FREEIDX), A
JR      .next
.endm:
LD      A, (HUFS_FREEIDX)
INC     A
JR      NZ, .e2
POP     AF
PUSH    AF
LD      (HUFS_FREEIDX), A
.e2:
POP     AF
.nf:
LD      A, 1
OR      A                       ; NZ、Cy = 0
RET
.err:
POP     BC
RET

; ---- HUFS_FCB: HL = ファイル番号 → IX = FCB (番号が悪い・緩衝がないなら Cy・A = 3) ----
; HUFS_FCBO: さらに、開いていなければ Cy・A = 5
; A・HL を壊す。BC・DE は壊さない
HUFS_FCB:
LD      A, H
OR      A
JR      NZ, .bad
LD      A, L
CP      16
JR      NC, .bad
LD      IX, HUFS_FCBS
OR      A
RET     Z
CP      2
JR      NC, .tab
LD      HL, 272
EX      DE, HL
ADD     IX, DE
EX      DE, HL
RET
.tab:
ADD     A, A
LD      L, A
LD      H, 0
PUSH    DE
LD      DE, HUFS_FCBTAB-4
ADD     HL, DE
POP     DE
LD      A, (HL)
INC     HL
LD      H, (HL)
LD      L, A
OR      H
JR      Z, .bad
PUSH    HL
POP     IX
RET
.bad:
LD      A, 3
SCF
RET
HUFS_FCBO:
CALL    HUFS_FCB
RET     C
LD      A, (IX+0)
OR      A
RET     NZ
LD      A, 5
SCF
RET

; ---- HUFS_LOCATE: HL = ファイルの頭から数えたレコード → DE = ディスクのレコード、B = クラスタ ----
; 鎖が足りない (終わりの印や範囲外に行き当たる) ときは Cy・A = $0D
HUFS_LOCATE:
LD      A, L
AND     0Fh
LD      C, A
ADD     HL, HL
ADD     HL, HL
ADD     HL, HL
ADD     HL, HL
LD      B, H                    ; 鎖をたどる回数
INC     B
LD      A, (IX+2)
JR      .w2
.w:
CALL    HUFS_FATP
LD      A, (HL)
CP      2
JR      C, .bad
CP      80
JR      NC, .bad
.w2:
DJNZ    .w
LD      B, A
LD      L, A
LD      H, 0
ADD     HL, HL
ADD     HL, HL
ADD     HL, HL
ADD     HL, HL
LD      A, L
OR      C
LD      E, A
LD      D, H
RET
.bad:
LD      A, 0Dh
SCF
RET

; ---- HUFS_EXT1: 確保したレコードを 1 つ増やす (最後のクラスタが埋まっていたら、クラスタを 1 つ取って継ぐ) ----
HUFS_EXT1:
LD      L, (IX+10)
LD      H, (IX+11)
DEC     HL
CALL    HUFS_LOCATE
RET     C
LD      A, B
CALL    HUFS_FATP               ; HL = 最後のクラスタの FAT
LD      A, E
AND     0Fh
CP      0Fh
JR      Z, .new
INC     (HL)                    ; $80+n → $80+n+1
JR      .inc
.new:
PUSH    HL
CALL    HUFS_ALLOC
POP     HL
RET     C
LD      (HL), A
CALL    HUFS_FATP
LD      (HL), 80h
.inc:
INC     (IX+10)
JR      NZ, .r
INC     (IX+11)
.r:
OR      A
RET

; ---- HUFS_ATEOF: Cy = 1 なら 位置 < 長さ (まだ読める) ----
HUFS_ATEOF:
LD      A, (IX+6)
CP      (IX+9)
RET     NZ
LD      A, (IX+5)
CP      (IX+8)
RET     NZ
LD      A, (IX+4)
CP      (IX+7)
RET

; ---- 緩衝 ----
; HUFS_BUFHL: HL = 緩衝の頭。HUFS_BUFP: HL = 緩衝の中の今の位置
HUFS_BUFP:
CALL    HUFS_BUFHL
LD      C, (IX+4)
LD      B, 0
ADD     HL, BC
RET
HUFS_BUFHL:
PUSH    IX
POP     HL
LD      BC, 16
ADD     HL, BC
RET

; HUFS_FLUSH: 緩衝に変更があれば、ディスクに書く
HUFS_FLUSH:
LD      A, (IX+14)
OR      A
RET     Z
LD      L, (IX+12)
LD      H, (IX+13)
CALL    HUFS_LOCATE
RET     C
CALL    HUFS_BUFHL
CALL    HUFS_WR
RET     C
LD      (IX+14), 0
RET

; HUFS_LOADBUF: 今の位置のレコードを緩衝に入れる (前のものに変更があれば先に書く)
HUFS_LOADBUF:
LD      L, (IX+5)
LD      H, (IX+6)
LD      A, L
CP      (IX+12)
JR      NZ, .ld
LD      A, H
CP      (IX+13)
RET     Z
.ld:
PUSH    HL
CALL    HUFS_FLUSH
POP     HL
RET     C
LD      (IX+12), L
LD      (IX+13), H
CALL    HUFS_LOCATE
JR      C, .fail
CALL    HUFS_BUFHL
CALL    HUFS_RD
RET     NC
.fail:
LD      (IX+12), 0FFh
LD      (IX+13), 0FFh
RET

HUFS_ADVPOS:
INC     (IX+4)
RET     NZ
INC     (IX+5)
RET     NZ
INC     (IX+6)
RET

; ---- HUFS_GETC: 1 バイト読む → HL = 0〜255、$FFFF (終わり)、エラーは Cy・HL = $FF00+番号 ----
HUFS_GETC:
CALL    HUFS_ATEOF
JR      C, .ok
LD      HL, 0FFFFh
RET
.ok:
CALL    HUFS_LOADBUF
JR      C, HUFS_ERRHL
CALL    HUFS_BUFP
LD      L, (HL)
LD      H, 0
JR      HUFS_ADVPOS
HUFS_ERRHL:
LD      L, A
LD      H, 0FFh
SCF
RET

; ---- HUFS_PUTC: A = 1 バイト書く (Cy・A = エラー) ----
HUFS_PUTC:
LD      E, A
LD      A, (IX+0)
AND     3
LD      A, 6
SCF
RET     Z                       ; 読むだけで開いている
LD      A, (HUFS_WLOCK)
OR      A
LD      A, 9
SCF
RET     NZ                      ; FAT・ディレクトリの書き込みに失敗したあと (緩衝にも入れない)
LD      A, (IX+3)
AND     7
CP      1
JR      NZ, .ext
LD      A, (IX+6)               ; Bin は 65535 バイトまで (大きさの欄が 2 バイト)
OR      A
JR      NZ, .full
LD      A, (IX+5)
AND     (IX+4)
INC     A
JR      Z, .full
.ext:
LD      L, (IX+5)
LD      H, (IX+6)
LD      C, (IX+10)
LD      B, (IX+11)
OR      A
SBC     HL, BC
JR      C, .have
PUSH    DE
CALL    HUFS_EXT1
POP     DE
JR      NC, .ext
RET
.have:
PUSH    DE
CALL    HUFS_LOADBUF
POP     DE
RET     C
CALL    HUFS_BUFP
LD      (HL), E
LD      (IX+14), 1
CALL    HUFS_ADVPOS
CALL    HUFS_ATEOF
JR      C, .ok                  ; 位置 < 長さ: 長さはそのまま
LD      A, (IX+4)
LD      (IX+7), A
LD      A, (IX+5)
LD      (IX+8), A
LD      A, (IX+6)
LD      (IX+9), A
.ok:
OR      A
RET
.full:
LD      A, 2
SCF
RET

; ---- HUFS_INUSE: 項目 A を開いているものと重なるか (B = 0 でなければ、新しく開くものが書く) ----
; 重なれば Cy・A = 4。IX は壊さない
HUFS_INUSE:
PUSH    IX
LD      C, A
LD      HL, 16
.l:
DEC     L
PUSH    HL
CALL    HUFS_FCB
POP     HL
JR      C, .n
LD      A, (IX+0)
OR      A
JR      Z, .n
LD      A, (IX+1)
CP      C
JR      NZ, .n
LD      A, (IX+0)
AND     3
OR      B
JR      NZ, .conf               ; どちらかが書く
.n:
LD      A, L
OR      A
JR      NZ, .l
POP     IX
RET
.conf:
POP     IX
LD      A, 4
SCF
RET

; ---- HUFS_CLOSE1: IX のファイルを閉じる (緩衝 → FAT → 項目の順に書く。エラーでも閉じた扱いにする) ----
HUFS_CLOSE1:
LD      A, (IX+0)
AND     3
JR      Z, .done                ; 読むだけ
CALL    HUFS_FLUSH
JR      C, .fail
CALL    HUFS_FATWR
JR      C, .fail
LD      A, (IX+3)
AND     7
DEC     A
JR      NZ, .done               ; 大きさの欄を書くのは Bin だけ (ほかは 0 のまま。Hu-BASIC と同じ)
LD      A, (IX+1)
CALL    HUFS_ENTP
JR      C, .fail
LD      BC, 12h
ADD     HL, BC
LD      A, (IX+7)
LD      (HL), A
INC     HL
LD      A, (IX+8)
LD      (HL), A
CALL    HUFS_DIRWR
JR      C, .fail
.done:
XOR     A
.fail:
LD      (IX+0), 0
LD      HL, HUFS_OPENCNT
DEC     (HL)
RET

; ---- 公開の関数の終わり (入口で PUSH IX してあること) ----
HUFS_RETCY:
JR      C, HUFS_RETERR
HUFS_RET0:
LD      HL, 0
POP     IX
RET
HUFS_RETERR:
LD      L, A
LD      H, 0FFh
POP     IX
RET


; ========================================================================
; FOPEN(fn, name, mode): ファイルを開く
; ========================================================================
; mode の下 2 ビット: 0 読む、1 読み書き (なければ作る)、2 後ろに足す (なければ作る)、3 作る (あれば中を捨てる)
; mode の bit 2: 作るときの種類を Bin にする (既定は Asc)
; 戻り値 0 / エラー

; @name FOPEN
; @resident shared
; @param_count 3
; @calls HUFS_CORE
PUSH    IX
CALL    HUFS_FCB
JP      C, HUFS_RETERR
LD      A, (IX+0)
OR      A
LD      A, 4
JP      NZ, HUFS_RETERR         ; その番号で開いている
LD      A, C
LD      (HUFS_OMODE), A
CALL    HUFS_PARSE
JP      C, HUFS_RETERR
CALL    HUFS_SYNC
JP      C, HUFS_RETERR
CALL    HUFS_FIND
JP      C, HUFS_RETERR
JP      NZ, .create
; ---- ある ----
LD      (IX+1), A
LD      A, (HL)
LD      (IX+3), A
AND     7                       ; 種類は Bin・Bas・Asc のどれか 1 つ
JP      Z, .bad
LD      B, A
DEC     A
AND     B
JP      NZ, .bad
PUSH    HL
LD      BC, 11h
ADD     HL, BC
LD      A, (HL)                 ; パスワードなし ($20)
SUB     20h
LD      C, 1Dh-11h
ADD     HL, BC
OR      (HL)                    ; +1D と +1F は 0
INC     HL
LD      E, (HL)                 ; +1E 頭のクラスタ
INC     HL
OR      (HL)
POP     HL
JR      NZ, .bad
LD      (IX+2), E
PUSH    HL
LD      BC, 12h
ADD     HL, BC
LD      E, (HL)
INC     HL
LD      D, (HL)                 ; DE = 大きさの欄
POP     HL
LD      A, (HUFS_OMODE)
AND     3
LD      B, A
JR      Z, .ro
BIT     6, (HL)
LD      A, 0Bh
JP      NZ, HUFS_RETERR         ; 書き込み禁止の印
.ro:
LD      A, (IX+1)
PUSH    DE
CALL    HUFS_INUSE
POP     DE
JP      C, HUFS_RETERR
LD      A, (IX+2)
PUSH    DE
CALL    HUFS_CHAIN
POP     DE
JP      C, HUFS_RETERR
LD      (IX+10), C
LD      (IX+11), B
LD      A, (HUFS_OMODE)
AND     3
CP      3
JR      Z, .trunc
LD      A, (IX+3)               ; 長さ: Bin は大きさの欄、ほかは確保したレコード数 × 256
AND     7
DEC     A
JR      NZ, .asc
; Bin の大きさの欄が鎖より大きい (中を捨てる途中で止まったディスクなど) ときは、鎖の大きさまでにする
LD      A, B
OR      A
JR      NZ, .binok              ; 鎖が 64 KB 以上
LD      A, C
CP      D
JR      C, .clamp               ; 鎖のレコード数 < 大きさの上のバイト
JR      NZ, .binok
LD      A, E
OR      A
JR      Z, .binok
.clamp:
LD      E, 0
LD      D, C
.binok:
XOR     A
LD      (IX+7), E
LD      (IX+8), D
LD      (IX+9), A
JP      .setup
.asc:
LD      (IX+7), 0
LD      (IX+8), C
LD      (IX+9), B
JP      .setup
.bad:
LD      A, 0Ch
JP      HUFS_RETERR
; ---- 中を捨てる: 頭のクラスタだけ残す → FAT → 項目の大きさの欄を 0 に ----
.trunc:
LD      A, (IX+2)
CALL    HUFS_FATP
LD      A, (HL)
LD      (HL), 80h
CALL    HUFS_FREECH
CALL    HUFS_FATWR
JP      C, HUFS_RETERR
LD      A, (IX+1)
CALL    HUFS_ENTP
JP      C, HUFS_RETERR
LD      BC, 12h
ADD     HL, BC
LD      (HL), 0
INC     HL
LD      (HL), 0
CALL    HUFS_DIRWR
JP      C, HUFS_RETERR
JP      .empty
; ---- ない: 作る (クラスタを取る → FAT → 項目。成功を返す前に、項目まで押さえる) ----
.create:
LD      A, (HUFS_OMODE)
AND     3
LD      A, 1
JP      Z, HUFS_RETERR          ; 読むだけ: ない
LD      A, (HUFS_FREEIDX)
LD      (IX+1), A
INC     A
LD      A, 8
JP      Z, HUFS_RETERR          ; ディレクトリがいっぱい
CALL    HUFS_ALLOC
JP      C, HUFS_RETERR
LD      (IX+2), A
CALL    HUFS_FATP
LD      (HL), 80h
CALL    HUFS_FATWR
JR      NC, .c2
PUSH    AF
LD      A, (IX+2)
CALL    HUFS_FATP
LD      (HL), 0                 ; 写しを戻す
POP     AF
JP      HUFS_RETERR
.c2:
LD      A, (IX+1)
CALL    HUFS_ENTP
JP      C, HUFS_RETERR
; 終わりの印の所に作るときは、次の項目を終わりの印にする (終わりの後ろに残った古い項目が見えないように)。
; 同じレコードなら新しい項目と一緒に書く。次のレコードなら、新しい項目より先に書く
LD      A, (HL)
INC     A
JR      NZ, .c3
LD      A, (IX+1)
AND     7
CP      7
JR      Z, .cnext
PUSH    HL
LD      BC, 32
ADD     HL, BC
LD      (HL), 0FFh
POP     HL
JR      .c3
.cnext:
LD      A, (IX+1)
INC     A
JP      M, .c3                  ; 128 番目 (最後の項目) なら次はない
CALL    HUFS_ENTP
JP      C, HUFS_RETERR
LD      A, (HL)
INC     A
JR      Z, .cagain              ; もう終わりの印
LD      (HL), 0FFh
CALL    HUFS_DIRWR
JP      C, HUFS_RETERR
.cagain:
LD      A, (IX+1)
CALL    HUFS_ENTP
JP      C, HUFS_RETERR
.c3:
LD      A, (HUFS_OMODE)
AND     4
LD      A, 4                    ; Asc
JR      Z, .c4
LD      A, 1                    ; Bin
.c4:
LD      (HL), A
LD      (IX+3), A
INC     HL
EX      DE, HL
LD      HL, HUFS_NAME
LD      BC, 16
LDIR
EX      DE, HL
LD      (HL), 20h               ; パスワードなし
INC     HL
LD      B, 12                   ; 大きさ・読む番地・実行番地・日付・+1D は 0
.c5:
LD      (HL), 0
INC     HL
DJNZ    .c5
LD      A, (IX+2)
LD      (HL), A
INC     HL
LD      (HL), 0
CALL    HUFS_DIRWR
JP      C, HUFS_RETERR
.empty:
LD      (IX+10), 1
LD      (IX+11), 0
XOR     A
LD      (IX+7), A
LD      (IX+8), A
LD      (IX+9), A
; ---- 位置 (mode 2 は終わり、ほかは頭) と、残りの欄 ----
.setup:
LD      A, (HUFS_OMODE)
AND     3
CP      2
JR      Z, .app
XOR     A
LD      (IX+4), A
LD      (IX+5), A
LD      (IX+6), A
JR      .s2
.app:
LD      A, (IX+7)
LD      (IX+4), A
LD      A, (IX+8)
LD      (IX+5), A
LD      A, (IX+9)
LD      (IX+6), A
.s2:
LD      A, (HUFS_OMODE)
AND     3
OR      80h
LD      (IX+0), A
LD      (IX+12), 0FFh
LD      (IX+13), 0FFh
LD      (IX+14), 0
LD      (IX+15), 0
LD      HL, HUFS_OPENCNT
INC     (HL)
JP      HUFS_RET0


; ========================================================================
; FCLOSE(fn): 閉じる。戻り値 0 / エラー (エラーでも閉じた扱いにする)
; ========================================================================

; @name FCLOSE
; @resident shared
; @param_count 1
; @calls HUFS_CORE
PUSH    IX
CALL    HUFS_FCBO
JP      C, HUFS_RETERR
CALL    HUFS_CLOSE1
JP      HUFS_RETCY


; ========================================================================
; FREAD(fn, addr, size) / FWRITE(fn, addr, size)
; ========================================================================
; 戻り値: 読んだ / 書いたバイト数 (size は 0〜$FEFF)。途中でエラーになったら、そこまでの数を返し、エラーは FERR で取る

; @name FREAD
; @resident shared
; @param_count 3
; @calls HUFS_CORE
PUSH    IX
CALL    HUFS_FCBO
JP      C, HUFS_RETERR
LD      HL, 0
.l:
LD      A, B
OR      C
JR      Z, .done
PUSH    HL
PUSH    DE
PUSH    BC
CALL    HUFS_GETC
POP     BC
POP     DE
JR      C, .err
INC     H                       ; $FFFF (終わり) なら 0 になる
JR      Z, .eof
LD      A, L
LD      (DE), A
INC     DE
DEC     BC
POP     HL
INC     HL
JR      .l
.err:
LD      (IX+15), L
.eof:
POP     HL
.done:
POP     IX
RET


; @name FWRITE
; @resident shared
; @param_count 3
; @calls HUFS_CORE
PUSH    IX
CALL    HUFS_FCBO
JP      C, HUFS_RETERR
LD      HL, 0
.l:
LD      A, B
OR      C
JR      Z, .done
PUSH    HL
PUSH    DE
PUSH    BC
LD      A, (DE)
CALL    HUFS_PUTC
POP     BC
POP     DE
JR      C, .err
INC     DE
DEC     BC
POP     HL
INC     HL
JR      .l
.err:
LD      (IX+15), A
POP     HL
.done:
POP     IX
RET


; ========================================================================
; FGETC(fn): 戻り値 0〜255 / $FFFF (終わり) / エラー。FPUTC(fn, c): 戻り値 0 / エラー
; ========================================================================

; @name FGETC
; @resident shared
; @param_count 1
; @calls HUFS_CORE
PUSH    IX
CALL    HUFS_FCBO
JP      C, HUFS_RETERR
CALL    HUFS_GETC
POP     IX
RET


; @name FPUTC
; @resident shared
; @param_count 2
; @calls HUFS_CORE
PUSH    IX
CALL    HUFS_FCBO
JP      C, HUFS_RETERR
LD      A, E
CALL    HUFS_PUTC
JP      HUFS_RETCY


; ========================================================================
; FSEEK(fn, off, whence): whence 0 頭から (off は符号なし)、1 今から・2 終わりから (off は符号付き)
; ========================================================================
; 頭より前・ディスクの大きさ (1280 レコード) より後ろは $FF0E。終わりより後ろはよい (書いたときに、あいだのレコードを取る)

; @name FSEEK
; @resident shared
; @param_count 3
; @calls HUFS_CORE
PUSH    IX
CALL    HUFS_FCBO
JP      C, HUFS_RETERR
LD      A, C
OR      A
JR      NZ, .w1
EX      DE, HL                  ; 頭から
JR      .set
.w1:
DEC     A
JR      NZ, .w2
LD      L, (IX+4)               ; 今から
LD      H, (IX+5)
LD      A, (IX+6)
JR      .add
.w2:
LD      L, (IX+7)               ; 終わりから
LD      H, (IX+8)
LD      A, (IX+9)
.add:
ADD     HL, DE
ADC     A, 0
BIT     7, D
JR      Z, .set
DEC     A                       ; off が負
.set:
CP      5                       ; 1280 レコード = $50000 バイト
JR      NC, .range
LD      (IX+4), L
LD      (IX+5), H
LD      (IX+6), A
JP      HUFS_RET0
.range:
LD      A, 0Eh
JP      HUFS_RETERR


; ========================================================================
; FSEEKREC(fn, rec): 位置をレコード rec (0〜1279) の頭に。64 KB を超える位置はこちらで
; FTELLREC(fn): 今のレコード番号。FSIZEREC(fn): 確保したレコード数。FEOF(fn): 1 / 0
; ========================================================================

; @name FSEEKREC
; @resident shared
; @param_count 2
; @calls HUFS_CORE
PUSH    IX
CALL    HUFS_FCBO
JP      C, HUFS_RETERR
LD      HL, 1279
OR      A
SBC     HL, DE
LD      A, 0Eh
JP      C, HUFS_RETERR
LD      (IX+4), 0
LD      (IX+5), E
LD      (IX+6), D
JP      HUFS_RET0


; @name FTELLREC
; @resident shared
; @param_count 1
; @calls HUFS_CORE
PUSH    IX
CALL    HUFS_FCBO
JP      C, HUFS_RETERR
LD      L, (IX+5)
LD      H, (IX+6)
POP     IX
RET


; @name FSIZEREC
; @resident shared
; @param_count 1
; @calls HUFS_CORE
PUSH    IX
CALL    HUFS_FCBO
JP      C, HUFS_RETERR
LD      L, (IX+10)
LD      H, (IX+11)
POP     IX
RET


; @name FEOF
; @resident shared
; @param_count 1
; @calls HUFS_CORE
PUSH    IX
CALL    HUFS_FCBO
JP      C, HUFS_RETERR
CALL    HUFS_ATEOF
LD      HL, 0
JR      C, .r
INC     L
.r:
POP     IX
RET


; ========================================================================
; FERR(fn): 最後のエラー (0 = なし)。読むと 0 に戻る
; ========================================================================

; @name FERR
; @resident shared
; @param_count 1
; @calls HUFS_CORE
PUSH    IX
CALL    HUFS_FCB
JP      C, HUFS_RETERR
LD      A, (IX+15)
LD      (IX+15), 0
LD      HL, 0
OR      A
JR      Z, .r
LD      L, A
DEC     H
.r:
POP     IX
RET


; ========================================================================
; FDELETE(name): 消す。開いている・書き込み禁止はエラー
; ========================================================================
; 項目の種類を $00 にして書く → FAT の写しから鎖を空きにして FAT を書く (間で止まるとクラスタが漏れるだけ)

; @name FDELETE
; @resident shared
; @param_count 1
; @calls HUFS_CORE
PUSH    IX
EX      DE, HL
CALL    HUFS_PARSE
JP      C, HUFS_RETERR
CALL    HUFS_SYNC
JP      C, HUFS_RETERR
CALL    HUFS_FIND
JP      C, HUFS_RETERR
JR      Z, .f
LD      A, 1
JP      HUFS_RETERR
.f:
LD      (HUFS_FREEIDX), A       ; 項目の番号 (一時に借りる)
BIT     6, (HL)
LD      A, 0Bh
JP      NZ, HUFS_RETERR
LD      BC, 1Eh
ADD     HL, BC
LD      A, (HL)
LD      (HUFS_OMODE), A         ; 頭のクラスタ (一時に借りる)
CALL    HUFS_CHAIN
JP      C, HUFS_RETERR
LD      A, (HUFS_FREEIDX)
LD      B, 1
CALL    HUFS_INUSE
JP      C, HUFS_RETERR
LD      A, (HUFS_FREEIDX)
CALL    HUFS_ENTP
JP      C, HUFS_RETERR
LD      (HL), 0
CALL    HUFS_DIRWR
JP      C, HUFS_RETERR
LD      A, (HUFS_OMODE)
CALL    HUFS_FREECH
CALL    HUFS_FATWR
JP      HUFS_RETCY


; ========================================================================
; FRENAME(old, new): 名前を変える。書き込み禁止・new がある、はエラー
; ========================================================================
; 開いているファイルの名前も変えてよい (Hu-BASIC の NAME と同じ。開いているものは項目の番号で持つので、名前が変わっても困らない)

; @name FRENAME
; @resident shared
; @param_count 2
; @calls HUFS_CORE
; @works HUFS_NAME2:16
PUSH    IX
PUSH    HL
CALL    HUFS_PARSE              ; new
LD      HL, HUFS_NAME
LD      DE, HUFS_NAME2
LD      BC, 16
LDIR                            ; Cy は変わらない
POP     DE
JP      C, HUFS_RETERR
CALL    HUFS_PARSE              ; old
JP      C, HUFS_RETERR
CALL    HUFS_SYNC
JP      C, HUFS_RETERR
CALL    HUFS_FIND
JP      C, HUFS_RETERR
JR      Z, .f
LD      A, 1
JP      HUFS_RETERR
.f:
LD      (HUFS_FREEIDX), A
BIT     6, (HL)
LD      A, 0Bh
JP      NZ, HUFS_RETERR
LD      HL, HUFS_NAME2
LD      DE, HUFS_NAME
LD      BC, 16
LDIR
LD      A, (HUFS_FREEIDX)
PUSH    AF
CALL    HUFS_FIND               ; new がもうあるか
POP     BC                      ; B = old の項目の番号
JP      C, HUFS_RETERR
LD      A, 0Ah
JP      Z, HUFS_RETERR
LD      A, B
CALL    HUFS_ENTP
JP      C, HUFS_RETERR
INC     HL
EX      DE, HL
LD      HL, HUFS_NAME
LD      BC, 16
LDIR
CALL    HUFS_DIRWR
JP      HUFS_RETCY


; ========================================================================
; FFIRST(buf) / FNEXT(buf): ディレクトリの項目 (消したものは飛ばす) を 32 バイトのまま buf に写す
; ========================================================================
; 戻り値 0 = あった / $FFFF = もうない / エラー

; @name FFIRST
; @resident shared
; @param_count 1
; @calls HUFS_CORE,FNEXT
PUSH    HL
CALL    HUFS_SYNC
POP     HL
JR      C, .err
XOR     A
LD      (HUFS_FFIDX), A
JP      FNEXT
.err:
LD      L, A
LD      H, 0FFh
RET


; @name FNEXT
; @resident shared
; @param_count 1
; @calls HUFS_CORE
; @works HUFS_FFIDX:1
EX      DE, HL
.l:
LD      A, (HUFS_FFIDX)
CP      128
JR      NC, .none
PUSH    DE
PUSH    AF
CALL    HUFS_ENTP
POP     BC                      ; B = 番号
POP     DE
JR      C, .err
LD      A, B
INC     A
LD      (HUFS_FFIDX), A
LD      A, (HL)
OR      A
JR      Z, .l                   ; 消したもの
INC     A
JR      Z, .end                 ; 終わりの印
LD      BC, 32
LDIR
LD      HL, 0
RET
.end:
LD      A, 128
LD      (HUFS_FFIDX), A
.none:
LD      HL, 0FFFFh
RET
.err:
LD      L, A
LD      H, 0FFh
RET


; ========================================================================
; FFREE(): 空いているクラスタの数 (1 クラスタ = 4 KB) / エラー
; ========================================================================

; @name FFREE
; @resident shared
; @param_count 0
; @calls HUFS_CORE
CALL    HUFS_SYNC
JR      C, .err
LD      HL, HUFS_FAT+2          ; 0・1 はシステムの領域
LD      B, 78
LD      DE, 0
.l:
LD      A, (HL)
OR      A
JR      NZ, .n
INC     E
.n:
INC     HL
DJNZ    .l
EX      DE, HL
RET
.err:
LD      L, A
LD      H, 0FFh
RET


; ========================================================================
; FSETBUF(fn, addr): ファイル番号 2〜15 の FCB の場所 (272 バイト) を渡す
; ========================================================================
; 戻り値 0 / $FF03 (番号が悪い・addr が 0) / $FF04 (その番号で開いている)

; @name FSETBUF
; @resident shared
; @param_count 2
; @calls HUFS_CORE
LD      A, D
OR      E
JR      Z, .bad
LD      A, H
OR      A
JR      NZ, .bad
LD      A, L
SUB     2
JR      C, .bad
CP      14
JR      NC, .bad
ADD     A, A
LD      C, A
LD      B, 0
LD      HL, HUFS_FCBTAB
ADD     HL, BC
PUSH    HL
LD      A, (HL)
INC     HL
LD      H, (HL)
LD      L, A
OR      H
JR      Z, .set
LD      A, (HL)                 ; 前の場所の状態
OR      A
.set:
POP     HL
JR      NZ, .busy
LD      (HL), E
INC     HL
LD      (HL), D
XOR     A
LD      (DE), A                 ; 閉じている印
LD      H, A
LD      L, A
RET
.bad:
LD      HL, $FF03
RET
.busy:
LD      HL, $FF04
RET
