; hufsboot.asm: x1native_hufs のディスクの小さなローダ (docs/HUFS.md の「ローダの形のディスク」)
;
; IPL ROM が、レコード 0 の印 (起動名.Sys) に従ってこれを 0100H に読み込み、0100H へ飛ぶ。
; ローダはディレクトリから「起動名.Bin」を探し、FAT の鎖をたどって本体を読む番地に読み込み、実行番地へ飛ぶ。
; 本体のクラスタは続いていなくてよい (IPL ROM は続いたレコードしか読めないため、このローダを挟む)。
;
; 組み立て: AILZ80ASM -i hufsboot.asm -bin hufsboot.bin
; 組み立てたバイト列は src/SLANGCompiler.SlfsPack/HuLoader.cs に置く (試験で一致を確かめる)。
;
; メモリ:
;   0100H〜03FFH  このローダ (768 バイト以下)
;   0400H〜04FFH  緩衝 (ディレクトリ・最後のレコード)
;   0500H〜05FFH  FAT (レコード 14)
;   0600H〜061FH  作業
;   0FFEH から下  スタック
; 本体は 1000H 以上にしか読まない (ローダのコード・緩衝・スタックに重ならないように)。
;
; 読む前に調べること (外れたら、どこにも書かずに LOAD ERROR):
;   - 読む番地 L が 1000H 以上、大きさ S が 0 でない、L + S が 10000H 以下
;   - 先頭のクラスタと、次を指す FAT の値が $02〜$4F
;   - 次があるクラスタでは、残りのレコード数が 16 より多い
;   - 最後のクラスタの印 $80+n では、n + 1 が残りのレコード数とちょうど同じ
;   - クラスタは 78 個まで (輪になっていても止まる)
;   - セクタはちょうど 256 バイト (違えば読み損ないとして扱い、256 バイトより後ろは書かない)
;   - 読む命令を出した後の待ちにも上限 (約 10 秒) を付ける
; 最後のレコードは緩衝に読み、残りのバイトだけを写す (L + S より先には書かない)。
;
; FDC の下回りは libx1native_hufs.asm の読む所を写したもの (待ちの上限・同じシリンダならシークしない・
; 失敗したら 1 回だけヘッドを戻してやり直す、も同じ)。ドライブ 0、2D、読むだけ。
; 使うのは FDC (0FF8H〜0FFCH) とテキストの VRAM だけで、X1 と X1turbo のどちらでも動く。

            ORG     0100H

BUF         EQU     0400H       ; ディレクトリ・最後のレコード
FATBUF      EQU     0500H       ; FAT
NAME        EQU     0600H       ; 起動名 (13 バイト)
SECNO       EQU     0610H       ; セクタ (0 から)
RETRY       EQU     0611H       ; やり直したか
RRREC       EQU     0612H       ; 読むレコード番号 (2)
RRBUF       EQU     0614H       ; 読む先 (2)
DEST        EQU     0616H       ; 本体を次に書く番地 (2)
EXEC        EQU     0618H       ; 実行番地 (2)
REMAIN      EQU     061AH       ; 残りのレコード数
LASTBYTES   EQU     061BH       ; 最後のレコードのバイト数 (0 なら 256)
CLUSTER     EQU     061CH       ; 今のクラスタ
NEXTCL      EQU     061DH       ; FAT の値
COUNT       EQU     061EH       ; 残りのクラスタの数の上限
ISLAST      EQU     061FH       ; 1 なら最後のクラスタを読んでいる
STACKTOP    EQU     0FFEH

MINLOAD     EQU     1000H       ; 本体を読める一番下の番地
MAXCLUSTERS EQU     78          ; データのクラスタの数 ($02〜$4F)

START:
            DI
            LD      SP, STACKTOP

            ; レコード 0 (IPL の印) から起動名を取る
            LD      DE, 0
            LD      HL, BUF
            CALL    READREC
            JP      C, ERROR
            LD      HL, BUF+1
            LD      DE, NAME
            LD      BC, 13
            LDIR

            ; FAT
            LD      DE, 14
            LD      HL, FATBUF
            CALL    READREC
            JP      C, ERROR

            ; ディレクトリ (レコード 16〜31) で「起動名.Bin」(種類の bit 0 が立った項目) を探す
            LD      DE, 16
DIRREC:
            PUSH    DE                  ; レコード番号
            LD      HL, BUF
            CALL    READREC
            JP      C, ERROR
            LD      HL, BUF
            LD      B, 8                ; 1 レコードに 8 項目
DIRENT:
            LD      A, (HL)
            CP      0FFH
            JP      Z, ERROR            ; ここから後ろは使っていない: 見つからない
            RRCA
            JR      NC, NEXTENT         ; Bin でない (消したもの $00 も)
            PUSH    HL
            PUSH    BC
            INC     HL
            LD      DE, NAME
            LD      B, 13
            CALL    CMPB
            JR      NZ, NOMATCH
            LD      DE, EXTBIN
            LD      B, 3
            CALL    CMPB
            JR      Z, FOUND
NOMATCH:
            POP     BC
            POP     HL
NEXTENT:
            LD      DE, 32
            ADD     HL, DE
            DJNZ    DIRENT
            POP     DE
            INC     DE
            LD      A, E
            CP      32
            JR      NZ, DIRREC
            JP      ERROR

; HL と DE から B バイトを比べる。同じなら Z
CMPB:
            LD      A, (DE)
            CP      (HL)
            RET     NZ
            INC     HL
            INC     DE
            DJNZ    CMPB
            RET

FOUND:
            POP     BC
            POP     IX                  ; 項目
            POP     DE                  ; レコード番号は要らない
            LD      C, (IX+12H)
            LD      B, (IX+13H)         ; BC = 大きさ S
            LD      A, B
            OR      C
            JP      Z, ERROR            ; S = 0
            LD      L, (IX+14H)
            LD      H, (IX+15H)         ; HL = 読む番地 L
            LD      (DEST), HL
            LD      A, H
            CP      HIGH(MINLOAD)
            JP      C, ERROR            ; L < 1000H
            ADD     HL, BC
            JR      NC, SIZEOK
            LD      A, H
            OR      L
            JP      NZ, ERROR           ; L + S > 10000H
SIZEOK:
            LD      L, (IX+16H)
            LD      H, (IX+17H)
            LD      (EXEC), HL
            LD      A, C
            LD      (LASTBYTES), A
            LD      HL, 255
            ADD     HL, BC              ; S は 61440 以下なので桁あふれしない
            LD      A, H
            LD      (REMAIN), A         ; 要るレコード数 = ceil(S / 256)
            LD      A, (IX+1EH)
            LD      (CLUSTER), A
            LD      A, MAXCLUSTERS
            LD      (COUNT), A

CLLOOP:
            LD      A, (CLUSTER)
            CALL    CHKCL
            JP      C, ERROR
            LD      E, A
            LD      D, 0
            LD      HL, FATBUF
            ADD     HL, DE
            LD      A, (HL)             ; FAT の値
            LD      (NEXTCL), A
            CALL    CHKCL
            JR      NC, MIDDLE          ; $02〜$4F: 次がある
            CP      80H
            JP      C, ERROR
            CP      90H
            JP      NC, ERROR
            ; 最後のクラスタ ($80+n): n + 1 が残りのレコード数とちょうど同じこと
            SUB     7FH
            LD      B, A
            LD      A, (REMAIN)
            CP      B
            JP      NZ, ERROR
            LD      A, 1
            CALL    READCL
            JP      C, ERROR
            LD      HL, (EXEC)
            JP      (HL)

MIDDLE:
            LD      A, (REMAIN)
            CP      17
            JP      C, ERROR            ; 次があるのに、残りが 16 レコード以下
            SUB     16
            LD      (REMAIN), A
            LD      B, 16
            XOR     A
            CALL    READCL
            JP      C, ERROR
            LD      A, (NEXTCL)
            LD      (CLUSTER), A
            LD      HL, COUNT
            DEC     (HL)
            JP      Z, ERROR
            JR      CLLOOP

; A がデータのクラスタ ($02〜$4F) なら Cy = 0
CHKCL:
            CP      2
            RET     C
            CP      50H
            CCF
            RET

; (CLUSTER) の頭から B レコードを、(DEST) へ順に読む。
; A = 1 なら最後のクラスタで、最後のレコードは緩衝に読んで (LASTBYTES) バイトだけを写す (0 なら 256)
; Cy = 1 なら失敗
READCL:
            LD      (ISLAST), A
            LD      A, (CLUSTER)
            LD      L, A
            LD      H, 0
            ADD     HL, HL
            ADD     HL, HL
            ADD     HL, HL
            ADD     HL, HL
            EX      DE, HL              ; DE = レコード番号
RCLOOP:
            PUSH    BC
            PUSH    DE
            LD      A, B
            DEC     A
            JR      NZ, RCFULL
            LD      A, (ISLAST)
            OR      A
            JR      Z, RCFULL
            ; 本体の最後のレコード
            LD      HL, BUF
            CALL    READREC
            JR      C, RCERR
            LD      A, (LASTBYTES)
            LD      C, A
            LD      B, 0
            OR      A
            JR      NZ, RCPART
            INC     B                   ; 0 なら 256 バイト
RCPART:
            LD      HL, BUF
            LD      DE, (DEST)
            LDIR
            JR      RCNEXT
RCFULL:
            LD      HL, (DEST)
            CALL    READREC
            JR      C, RCERR
            LD      HL, (DEST)
            INC     H
            LD      (DEST), HL
RCNEXT:
            POP     DE
            POP     BC
            INC     DE
            DJNZ    RCLOOP
            OR      A
            RET
RCERR:
            POP     DE
            POP     BC
            SCF
            RET

; 読めなかった: テキストの画面の 1 行目 (40 桁) を LOAD ERROR にして止まる
ERROR:
            DI
            LD      HL, ERRMSG
            LD      BC, 3000H           ; テキストの VRAM
            LD      E, ERRMSGEND-ERRMSG
ERRL:
            LD      A, (HL)
            OUT     (C), A
            LD      A, B
            SUB     10H                 ; アトリビュート (2000H〜)
            LD      B, A
            LD      A, 07H
            OUT     (C), A
            LD      A, B
            ADD     A, 10H
            LD      B, A
            INC     HL
            INC     C
            DEC     E
            JR      NZ, ERRL
ERRHALT:
            HALT
            JR      ERRHALT

ERRMSG:     DB      "LOAD ERROR"
            DS      30, ' '             ; IPL ROM の「Loading …」を消す (40 桁)
ERRMSGEND:
EXTBIN:     DB      "Bin"

; ------------------------------------------------------------------------
; FDC の下回り (libx1native_hufs.asm の読む所を写したもの)
; FDC port: 0FF8H コマンド / 状態、0FF9H トラック、0FFAH セクタ、0FFBH データ、0FFCH ドライブ / 面 / モーター
; ------------------------------------------------------------------------

; レコードを 1 つ読む。DE = レコード番号 (bit 3-0 セクタ、bit 4 面、bit 15-5 シリンダ)、HL = 緩衝 (256 バイト)
; Cy = 1 なら失敗
READREC:
            LD      (RRREC), DE
            LD      (RRBUF), HL
            XOR     A
            LD      (RETRY), A
RRTRY:
            LD      DE, (RRREC)
            LD      A, E
            AND     0FH
            LD      (SECNO), A
            LD      A, E
            AND     10H                 ; 面
            OR      80H                 ; モーター ON、ドライブ 0
            LD      BC, 0FFCH
            OUT     (C), A
            LD      A, E                ; シリンダ = レコード番号 >> 5
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
            LD      D, A
            LD      BC, 0FF9H
            IN      A, (C)              ; トラックのレジスタ
            CP      D
            JR      Z, RRXFER           ; 同じシリンダ: シークしない
            LD      A, D
            CALL    FDCSEEK
            JR      NZ, RRERR
RRXFER:
            LD      HL, (RRBUF)
            LD      A, (SECNO)
            CALL    FDCREAD
            AND     9CH                 ; NR・RNF・CRC・lost
            JR      Z, RROK
RRERR:                                  ; A = 状態
            LD      B, A
            LD      A, (RETRY)
            OR      A
            JR      NZ, RRFAIL
            BIT     7, B
            JR      NZ, RRFAIL          ; Not Ready・時間切れはやり直さない
            INC     A
            LD      (RETRY), A
            LD      A, 0EH              ; restore + verify + head load
            CALL    FDCCMD
            JR      RRTRY
RRFAIL:
            CALL    MOTOROFF
            SCF
            RET
RROK:
MOTOROFF:
            XOR     A                   ; Cy = 0
            LD      BC, 0FFCH
            OUT     (C), A
            RET

; 1 セクタを読む。A = セクタ (0 から)、HL = 緩衝。戻り値 A = 状態 (時間切れは 80H)
; 緩衝に書くのは 256 バイトまで。256 バイトに届かずに終わった (0 バイトを含む) ら RNF ($10) を足して返し、
; 257 バイト目が来たら、それを読んだところで命令を打ち切って RNF を返す。
; 命令を出した後の待ちにも上限を付ける (約 10 秒。尽きたら FDC に強制割り込みを出して時間切れにする)。
; 2D は 1 バイトが 32µs (4MHz で 128T) ごとに来るので、DRQ が立ってからデータを読むまでを、
; どの道を通っても 128T より短くする (数えるのは毎回 DE、尽きたら状態をもう一度見てから C)
FDCREAD:
            LD      BC, 0FFAH
            INC     A                   ; 1 から数える番号に
            OUT     (C), A
            LD      C, 0F8H
            CALL    WAITBUSY
            JR      C, FRTMO
            LD      A, 80H              ; 読む
            OUT     (C), A
            CALL    WAIT1
            LD      B, 0                ; 残り 256 バイト
            LD      C, 10               ; 待ちの数えの上の桁 (1 で約 1 秒)
            LD      DE, 0
FRLOOP:
            LD      A, 0FH
            IN      A, (0F8H)
            RRCA
            JR      NC, FRSHORT         ; 256 バイトの前に終わった
            RRCA
            JR      C, FRDATA           ; DRQ
            DEC     DE
            LD      A, D
            OR      E
            JP      NZ, FRLOOP
            LD      A, 0FH              ; DE が尽きた: もう一度見てから C を減らす
            IN      A, (0F8H)
            RRCA
            JR      NC, FRSHORT
            RRCA
            JR      C, FRDATA
            DEC     C
            JP      NZ, FRLOOP
            JR      FRSTALL
FRDATA:
            LD      A, 0FH
            IN      A, (0FBH)
            LD      (HL), A
            INC     HL
            DJNZ    FRLOOP
FRDRAIN:                                ; 256 バイト読んだ: 終わるのを待つ
            LD      A, 0FH
            IN      A, (0F8H)
            RRCA
            JR      NC, FRDONE
            RRCA
            JR      C, FRLONG
            DEC     DE
            LD      A, D
            OR      E
            JP      NZ, FRDRAIN
            DEC     C
            JP      NZ, FRDRAIN
            JR      FRSTALL
FRLONG:                                 ; 257 バイト目: 読み捨てて命令を打ち切る (緩衝には書かない)
            LD      A, 0FH
            IN      A, (0FBH)
            LD      BC, 0FF8H
            LD      A, 0D0H
            OUT     (C), A
            LD      A, 10H
            RET
FRDONE:
            RLCA                        ; 状態の bit を戻す
            RET
FRSHORT:
            RLCA
            OR      10H
            RET
FRSTALL:                                ; 命令が終わらない: 打ち切って時間切れ
            LD      BC, 0FF8H
            LD      A, 0D0H
            OUT     (C), A
FRTMO:
            LD      A, 80H
            RET

; シーク。A = シリンダ。NZ ならエラー
FDCSEEK:
            LD      BC, 0FFBH
            OUT     (C), A
            LD      A, 1EH              ; seek + verify + head load + 6ms step
            CALL    FDCCMD
            JR      C, FSTMO
            IN      A, (C)
            AND     99H                 ; BUSY・CRC・RNF・NR
            RET
FSTMO:
            LD      A, 80H
            OR      A
            RET

; 命令を出して終わるのを待つ。A = 命令。Cy = 1 なら時間切れ
FDCCMD:
            LD      BC, 0FF8H
            OUT     (C), A
            ; そのまま WAITBUSY へ

; Busy と Not Ready が落ちるのを待つ (約 13 秒まで)。BC = 0FF8H
; Cy = 1 なら時間切れ (FDC に強制割り込みを出してある)。DE・HL は壊さない
WAITBUSY:
            PUSH    DE
            PUSH    HL
            LD      L, 16
            LD      DE, 0
WBLOOP:
            IN      A, (C)
            AND     81H                 ; BUSY・NOT READY
            JR      Z, WBOK
            DEC     DE
            LD      A, D
            OR      E
            JR      NZ, WBLOOP
            DEC     L
            JR      NZ, WBLOOP
            LD      A, 0D0H             ; 強制割り込み
            OUT     (C), A
            POP     HL
            POP     DE
            SCF
            RET
WBOK:
            POP     HL
            POP     DE
            RET

; 36.5µs 待つ (読む命令を出したあと、DRQ が立つまで)
WAIT1:
            LD      A, 7
W1LOOP:
            DEC     A
            JR      NZ, W1LOOP
            RET

LOADEREND:
