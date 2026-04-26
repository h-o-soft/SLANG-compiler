; cpm 環境向け file ライブラリ (CP/M 2.2 互換)
;
; liblsx_file.asm のコピー + 以下の差分:
;   - FREAD/FWRITE: CP/M 3+ の _RDBLK ($27) / _WRBLK ($26) を CP/M 2.2 互換の
;                   random access (_RDRND $21 / _WRRND $22) に置換
;                   (= record-aligned multi-record loop、FCB+33..36 の random
;                   record を honor)
;   - FCBRECINC: 内部 helper、FREAD/FWRITE 成功時に FCB+33..36 を +1 して
;                sequential semantics を維持 (_RDRND/_WRRND は auto-increment しない)
;   - FGETC/FPUTC: 単一 active fnum の 128 byte 内部バッファ方式
;   - ACTBUF_FLUSH: 内部 helper、dirty バッファを書き戻す + 0 クリア
;   - FREADWRITE: 廃止 (set record size 1 は CP/M 3+ 機能)
;
; 同名関数 (FOPEN/FREAD 等) と work 変数 (LSXFCB/LSXFMODE/LSXFCBS) は liblsx_file
; と完全に同じ。env では cpm.env のみがこのファイルを参照、liblsx_file は他 env
; が継続使用するので衝突しない。
;
; ★ FREAD/FWRITE は record-aligned (128 byte 単位)。返り値は実際に読み/書き
;   できた bytes (= records × 128)。size の「128 未満の端数」は切り捨て、
;   sub-record 精度が必要なら caller は FGETC/FPUTC を使う。EOF は HL=0、
;   error は HL=$FFFF。
;
; ★ FGETC/FPUTC の制約:
;   - 単一アクティブバッファ方式。同 fnum + 同 mode (連続 FGETC または連続
;     FPUTC) は OK。別 fnum へ切替時は自動 (read=reload, write=auto flush)
;   - **同一 fnum で read/write mode 切替は未サポート** — FCLOSE → FOPEN で
;     reopen 必須。サイレント誤動作を防ぐため未サポート組合せは $FF を返す
;   - FPUTC は write mode 入口時 / flush 後に ACTBUF を 0 で初期化
;     (= partial record でも残り部分は 0 で埋まる、stale data 漏れなし)
;
; ★ 同一 open file で FGETC/FPUTC と FREAD/FWRITE/FSEEK を混在させるのは
;   未サポート。FREAD/FWRITE/FSEEK 呼び出し時に active buffer は invalidate
;   される。混在したい場合は FCLOSE → FOPEN で reopen する。
;
; ★ lsx 完全互換ではない。lsx の FREAD は CP/M 3+ の variable record size を
;   使うので任意 byte 数を正確に R/W できるが、cpm は record-aligned + active
;   buffered byte I/O の組み合わせ。128B 境界整数倍ファイル + 単一 active
;   fnum 用途で実用的。

; @name LSXFILE
; @resident shared
; @calls MULHLDE
; fnum to FCB address
LSXCALCFCB:
PUSH BC
PUSH DE
LD DE,37
CALL MULHLDE
LD DE,LSXFCBS
ADD HL,DE
POP DE
POP BC
RET

; HL >= 8 ?
LSXFCHECKNUM:
PUSH HL
PUSH DE

; HL >= 8
LD DE,8
OR A
SBC HL,DE
; non carry (HL >= 8)

POP DE
POP HL
RET


; @name FOPEN
; @resident shared
; @calls LSXCALLS,FWORK,LSXFILE
; HL=fnum DE=fname addr BC=mode
LD (LSXFCB),HL
LD A,C
AND 3
LD C,A
LD (LSXFMODE),BC

CALL LSXFCHECKNUM
JP C,.fopen1
; return $FF
LD HL,255
RET

.fopen1
; LSXFCB=fnum*37+LSXFCBS
LD HL,(LSXFCB)
CALL LSXCALCFCB
LD (LSXFCB),HL

; LD C,$29  ; _PPATH
; CALL BDOS
CALL PPATH

; FCB+12(セクタインデックス)と+32(カレントレコード)をクリア
; _FOPENはFCB+12をセクタインデックスとして使用するため、0にしておく必要がある
PUSH HL
LD HL,(LSXFCB)
LD BC,12
ADD HL,BC
LD (HL),0
LD HL,(LSXFCB)
LD BC,32
ADD HL,BC
LD (HL),0
POP HL

LD HL,(LSXFMODE)
; mode >= 3
LD DE,3
OR A
SBC HL,DE
JR C,.fopen2
LD C,$16  ; _FMAKE
JR .fopen3
.fopen2
LD C,$0F  ; _FOPEN
.fopen3
LD DE,(LSXFCB)
PUSH IY
CALL BDOS
POP IY

; SET RANDOM RECORD to 0(4bytes)
LD HL,(LSXFCB)
LD DE,33
ADD HL,DE
LD (HL),0
INC HL
LD (HL),0
INC HL
LD (HL),0
INC HL
LD (HL),0

LD L,A
LD H,0
RET

PPATH:
CALL	MGETDV	;文字列からデバイス名を取り出します
SUB	'A'-1
LD	(HL),A
INC	HL

CALL	CLRWFG
LD	B,8	;プライマリ名
FNML11:
CALL	GTFLTR
LD	(HL),A
INC	HL
DJNZ	FNML11
CALL	SKPPRD
CALL	CLRWFG
LD	B,3	;拡張子
FNML12:
CALL	GTFLTR
LD	(HL),A
INC	HL
DJNZ	FNML12
CALL	SKPPRD

XOR	A	;ＣＹ←０
RET

MGETDV:
CALL	SPSKIP	;文字列の空白を読み飛ばします
LD	A,(DE)
OR	A
JR	Z,MGTDV1	;文字列の終わりに達していました
INC	DE
LD	A,(DE)
DEC	DE
CP	':'
JR	NZ,MGTDV1	;デバイスが指定されていません

;	デバイスが指定されています
LD	A,(DE)
CALL	TOUPR
INC	DE
INC	DE
OR	A	;ＣＹ←０
RET

;	デバイスが指定されていません
MGTDV1:
; カレントドライブ(0)
LD  A,'A'-1
SCF		;ＣＹ←１
RET

SPSLP:	INC	DE

SPSKIP:
LD	A,(DE)
CP	' '
JR	Z,SPSLP
CP	09H	;ＴＡＢ
JR	Z,SPSLP

RET

TOUPR:
CP	'a'
RET	C
CP	'z'+1
RET	NC
SUB	20H
RET

CLRWFG:
XOR	A
LD	(WASFLG),A
RET

WASFLG:	DS	1	;＝FFHで「*」フェイズ中
GFLLP:	INC	DE

GTFLTR:
;	「*」フェイズかチェックします
LD	A,(WASFLG)
OR	A
JR	NZ,GFLWLD

LD	A,(DE)	;Ａｃｃ←１文字

;	区切りに達したか調べます
OR	A	;ＮＵＬ
JR	Z,GFLESC
CP	0DH	;ＲＥＴ
JR	Z,GFLESC
CP	':'
JR	Z,GFLESC
CP	'.'
JR	Z,GFLESC

;	コントロール･コードとスペースをスキップします
LD	A,(DE)
CP	7FH	;ＤＥＬ
JR	Z,GFLLP
CP	21H	;00H～20H
JR	C,GFLLP

;	「*」かチェックします
CP	'*'
JR	Z,GFLAST

;	通常終了
INC	DE	;ＤＥのカウント･アップ
JP	TOUPR

;	「*」を発見しました
GFLAST:	LD	A,0FFH
LD	(WASFLG),A
INC	DE

;	「*」フェイズ
GFLWLD:	LD	A,'?'
RET

;	ピリオドかファイル名の最後に達しました
GFLESC:	LD	A,' '
RET
SKPLP:	INC	DE

SKPPRD:
LD	A,(DE)	;Ａｃｃ←１文字

;	区切りに達したか調べます
OR	A	;ＮＵＬ
RET	Z
CP	0DH	;ＲＥＴ
RET	Z
CP	':'
RET	Z

;	ピリオドに達したか調べます
CP	'.'
JR	NZ,SKPLP

;	ピリオドをスキップ
INC	DE
RET


; @name FSEEK
; @resident shared
; @calls LSXCALLS,FWORK,LSXFILE,NEGHL,ACTBUF_INVALIDATE
; HL=fnum DE=offset BC=mode(0=head, 1=current, 2=tail)
;
; 同 fnum が active buffer なら invalidate (= seek 後の論理位置ズレ防止)。
CALL LSXFCHECKNUM
JP C,.fseek1
; return $FF
LD HL,255
RET

.fseek1
; active buffer 整合: 同 fnum ならフラッシュ + 状態クリア
PUSH HL
PUSH DE
PUSH BC
LD A,(ACTBUFMODE)
OR A
JR Z,.fseek_skip_inv
LD A,(ACTBUFFNUM)
CP L
JR NZ,.fseek_skip_inv
CALL ACTBUF_INVALIDATE
OR A
JR NZ,.fseek_inv_err          ; flush 失敗 → error
.fseek_skip_inv
POP BC
POP DE
POP HL
JR .fseek2

.fseek_inv_err
POP BC
POP DE
POP HL
LD HL,255
RET

.fseek2

; LSXFCB=fnum*37+LSXFCBS
CALL LSXCALCFCB
LD (LSXFCB),HL

LD A,C
CP 1
JP Z,.fseek_current
JP C,.fseek_head

; fseek_tail
PUSH DE
PUSH HL
LD BC,33
ADD HL,BC
EX DE,HL    ; DE=(FCB)Random record
POP HL
LD BC,16
ADD HL,BC   ; HL=(FCB)File size
EX (SP),HL
CALL NEGHL
EX (SP),HL
POP BC      ; BC=-offset

LD A,(HL)
INC HL
SUB C
LD (DE),A
INC DE

LD A,(HL)
INC HL
SBC A,B
LD (DE),A
INC DE

LD A,(HL)
INC HL
SBC A,0
LD (DE),A
INC DE

LD A,(HL)
SBC A,0
LD (DE),A
JP .fseek_end

.fseek_head
LD BC,33
ADD HL,BC
LD (HL),E ; FCB+33
INC HL
LD (HL),D ; FCB+34
INC HL
LD (HL),0 ; FCB+35
INC HL
LD (HL),0 ; FCB=36

JP .fseek_end

.fseek_current
LD BC,33
ADD HL,BC

; FCB+33-36 += DE
LD A,E
ADD A,(HL)
LD (HL),A
LD A,D
INC HL
ADC A,(HL)
LD (HL),A
INC HL
LD A,0
ADC A,(HL)
LD (HL),A
INC HL
LD A,0
ADC A,(HL)
LD (HL),A

.fseek_end
LD HL,0
RET


; @name FGETC
; @resident shared
; @calls LSXCALLS,FWORK,LSXFILE,FCBRECINC,ACTBUF_FLUSH
; HL=fnum
;
; 単一アクティブバッファ方式 (read mode)。同 fnum + read mode なら ACTBUF
; から続きを返す、必要なら _RDRND で次 record を ACTBUF へ補充。
; 異 fnum なら自動切替 (write 時は前 fnum の dirty を flush)。
; 同 fnum + 異 mode (write→read) は未サポート → $FF01 を返す。

CALL LSXFCHECKNUM
JP C,.fgetc1
LD HL,$FF01            ; bad fnum
SCF
RET

.fgetc1
; mode 切替判定
LD A,(ACTBUFMODE)
OR A
JR Z,.fgetc_init       ; mode==0 → 初期化
; mode != 0
LD A,(ACTBUFFNUM)
CP L
JR NZ,.fgetc_switch    ; 別 fnum → 切替
; 同 fnum
LD A,(ACTBUFMODE)
CP 1
JR Z,.fgetc_serve      ; 同 fnum + read → 続行
; 同 fnum + write → 未サポート
LD HL,$FF01
SCF
RET

.fgetc_switch
; 別 fnum: 既存 active を flush (write の場合)
; ACTBUF_FLUSH は HL/DE/BC を破壊するので fnum (HL) を保護
PUSH HL
CALL ACTBUF_FLUSH
POP HL
OR A
JR NZ,.fgetc_eof_or_err   ; flush 失敗 → error 伝播
.fgetc_init
; ACTBUFFNUM = L (fnum)
LD A,L
LD (ACTBUFFNUM),A
LD A,1                 ; read mode
LD (ACTBUFMODE),A
LD A,128               ; force reload sentinel
LD (ACTBUFOFS),A
XOR A
LD (ACTBUFDIRTY),A
; LSXFCB を当該 fnum に設定 (reload で必要)
CALL LSXCALCFCB
LD (LSXFCB),HL

.fgetc_serve
; ACTBUFOFS == 128 なら reload
LD A,(ACTBUFOFS)
CP 128
JR NZ,.fgetc_return
; reload: ACTBUFFNUM の current random record から ACTBUF へ
LD A,(ACTBUFFNUM)
LD L,A
LD H,0
CALL LSXCALCFCB
LD (LSXFCB),HL
; SETDTA = ACTBUF
LD DE,ACTBUF
LD C,$1A
PUSH IY
CALL BDOS
POP IY
; _RDRND
LD DE,(LSXFCB)
LD C,$21
PUSH IY
CALL BDOS
POP IY
OR A
JR NZ,.fgetc_eof_or_err
; success: advance random record, reset offset
CALL FCBRECINC
XOR A
LD (ACTBUFOFS),A

.fgetc_return
; return ACTBUF[ACTBUFOFS], ACTBUFOFS++
LD A,(ACTBUFOFS)
LD E,A
LD D,0
LD HL,ACTBUF
ADD HL,DE
LD A,(HL)              ; A = byte
; ACTBUFOFS++
LD HL,ACTBUFOFS
INC (HL)
LD L,A                 ; HL = 0..$FF
LD H,0
RET

.fgetc_eof_or_err
LD HL,$FF01            ; EOF / error
SCF
RET


; @name FPUTC
; @resident shared
; @calls LSXCALLS,FWORK,LSXFILE,FCBRECINC,ACTBUF_FLUSH
; HL=fnum DE=chr
;
; 単一アクティブバッファ方式 (write mode)。同 fnum + write mode なら
; ACTBUF[ACTBUFOFS]=chr、ACTBUFOFS == 128 で auto-flush。
; 異 fnum なら自動切替、同 fnum + 異 mode (read→write) は未サポート → $FF。

CALL LSXFCHECKNUM
JP C,.fputc1
LD HL,255
RET

.fputc1
; mode 切替判定
LD A,(ACTBUFMODE)
OR A
JR Z,.fputc_init       ; mode==0 → 初期化
LD A,(ACTBUFFNUM)
CP L
JR NZ,.fputc_switch    ; 別 fnum → 切替
LD A,(ACTBUFMODE)
CP 2
JR Z,.fputc_serve      ; 同 fnum + write → 続行
; 同 fnum + read → 未サポート
LD HL,255
RET

.fputc_switch
; 別 fnum: 既存 active を flush (write の場合)
; ACTBUF_FLUSH は HL/DE/BC を破壊するので fnum (HL) と chr (DE) を保護
PUSH HL
PUSH DE
CALL ACTBUF_FLUSH
POP DE
POP HL
OR A
JR NZ,.fputc_err          ; flush 失敗 → error 伝播
.fputc_init
; chr (DE) を保存 (LDIR で DE が破壊されるため)
PUSH DE
; ACTBUFFNUM = L (fnum)
LD A,L
LD (ACTBUFFNUM),A
LD A,2                 ; write mode
LD (ACTBUFMODE),A
XOR A
LD (ACTBUFOFS),A
LD (ACTBUFDIRTY),A
; LSXFCB を当該 fnum に設定
CALL LSXCALCFCB
LD (LSXFCB),HL
; ACTBUF を 0 で 128 byte クリア (= partial record の tail を 0 padding)
LD HL,ACTBUF
LD DE,ACTBUF+1
LD BC,127
LD (HL),0
LDIR
; chr 復元
POP DE

.fputc_serve
; 前回 FPUTC の flush 失敗で OFS=128 に詰まっている場合は retry
LD A,(ACTBUFOFS)
CP 128
JR NZ,.fputc_doStore
PUSH DE
CALL ACTBUF_FLUSH
POP DE
OR A
JR NZ,.fputc_err

.fputc_doStore
; ACTBUF[ACTBUFOFS] = chr (E)
LD A,(ACTBUFOFS)
LD L,A
LD H,0
LD BC,ACTBUF
ADD HL,BC
LD (HL),E              ; store chr
; ACTBUFOFS++
LD HL,ACTBUFOFS
INC (HL)
; ACTBUFDIRTY = 1
LD A,1
LD (ACTBUFDIRTY),A
; if ACTBUFOFS == 128 then flush
LD A,(ACTBUFOFS)
CP 128
JR NZ,.fputc_done
; chr (DE) を保護してから flush 試行
PUSH DE
CALL ACTBUF_FLUSH
POP DE
OR A
JR NZ,.fputc_err           ; flush 失敗 → error
.fputc_done
LD HL,0
RET
.fputc_err
LD HL,255
RET


; @name FCLOSE
; @resident shared
; @calls LSXCALLS,FWORK,LSXFILE,ACTBUF_INVALIDATE
; HL=fnum
;
; もし当該 fnum が active buffer (FGETC/FPUTC) と一致するなら、
; close 前に flush + invalidate して書き残しを保存する。

; B = invalidate 結果保存 (0 = OK)
LD B,0
PUSH HL
LD A,(ACTBUFMODE)
OR A
JR Z,.fclose_skip_inv
LD A,(ACTBUFFNUM)
CP L
JR NZ,.fclose_skip_inv
CALL ACTBUF_INVALIDATE
LD B,A             ; 保存
.fclose_skip_inv
POP HL

CALL LSXCALCFCB
EX DE,HL
PUSH BC            ; B (invalidate result) を保護
LD C,$10  ; _FCLOSE
PUSH IY
CALL BDOS
POP IY
POP BC
; A = _FCLOSE 結果, B = invalidate 結果。
; invalidate が失敗していたら $FF を返す (= flush 失敗を優先)。
LD L,A
LD H,0
LD A,B
OR A
RET Z              ; invalidate OK → _FCLOSE 結果を返す
LD HL,255
RET


; @name FREAD
; @resident shared
; @calls LSXCALLS,FWORK,LSXFILE,FCBRECINC,ACTBUF_INVALIDATE
; HL=fnum DE=address BC=size
;
; record-aligned multi-record read。返り値 HL = bytes_read = records × 128。
; size を 128 で割って floor(size/128) records を読む試み。
; - 全 record 成功: HL = floor(size/128) * 128
; - EOF mid-loop: HL = 既読 records × 128 (= 0 含む)
; - error: HL = $FFFF, CY=1
; - size < 128: 0 records, HL = 0 (= caller は FGETC を使うべき)
; size パラメータの 128 未満端数は切り捨て (sub-record 精度なし)。
;
; 同一 fnum が active buffer (FGETC/FPUTC) なら invalidate (= 整合仕様)。

CALL LSXFCHECKNUM
JP C,.fread1
LD HL,255             ; bad fnum
RET

.fread1
; active buffer 整合: 同 fnum なら invalidate
PUSH HL
PUSH DE
PUSH BC
LD A,(ACTBUFMODE)
OR A
JR Z,.fread_skip_inv
LD A,(ACTBUFFNUM)
CP L
JR NZ,.fread_skip_inv
CALL ACTBUF_INVALIDATE
OR A
JR NZ,.fread_inv_err          ; flush 失敗 → error
.fread_skip_inv
POP BC
POP DE
POP HL
JR .fread2

.fread_inv_err
POP BC
POP DE
POP HL
LD HL,$FFFF
SCF
RET

.fread2
; LSXFCB setup
CALL LSXCALCFCB
LD (LSXFCB),HL

; save original size (BC) for total computation at end
PUSH BC

.fread_loop
; if BC < 128: done (= caller's remaining is sub-record)
LD A,B
OR A
JR NZ,.fread_chunk    ; B>0 → BC >= 256 >= 128
LD A,C
CP 128
JR C,.fread_done      ; B==0 && C<128 → stop
.fread_chunk

; SETDTA = DE (preserve BC, DE)
PUSH BC
PUSH DE
LD C,$1A
PUSH IY
CALL BDOS
POP IY
; _RDRND
LD DE,(LSXFCB)
LD C,$21
PUSH IY
CALL BDOS
POP IY                ; A = result
POP DE                ; restore address
POP BC                ; restore BC

; A=0 success, A=1 EOF, A>=2 error
OR A
JR Z,.fread_advance
CP 2
JR NC,.fread_err
; A=1 EOF: stop with current total
JR .fread_done

.fread_advance
CALL FCBRECINC        ; advance random record
; advance DE += 128
PUSH HL               ; HL is dirty, save (used for FCB calc by FCBRECINC)
LD HL,128
ADD HL,DE
EX DE,HL              ; DE += 128
POP HL
; BC -= 128
LD A,C
SUB 128
LD C,A
LD A,B
SBC A,0
LD B,A
JP .fread_loop

.fread_done
; total_read = original_size - BC_remaining
POP HL                ; HL = saved original size
OR A
SBC HL,BC
RET

.fread_err
POP HL                ; discard saved
LD HL,$FFFF
SCF
RET


; @name FWRITE
; @resident shared
; @calls LSXCALLS,FWORK,LSXFILE,FCBRECINC,ACTBUF_INVALIDATE
; HL=fnum DE=address BC=size
;
; record-aligned multi-record write。返り値 HL = bytes_written = records × 128。
; FREAD と対称、size の 128 未満端数は切り捨て。
; - 全 record 成功: HL = floor(size/128) * 128
; - error mid-loop: 部分書込み後 HL=$FFFF, CY=1

CALL LSXFCHECKNUM
JP C,.fwrite1
LD HL,255
RET

.fwrite1
; active buffer 整合: 同 fnum なら invalidate
PUSH HL
PUSH DE
PUSH BC
LD A,(ACTBUFMODE)
OR A
JR Z,.fwrite_skip_inv
LD A,(ACTBUFFNUM)
CP L
JR NZ,.fwrite_skip_inv
CALL ACTBUF_INVALIDATE
OR A
JR NZ,.fwrite_inv_err          ; flush 失敗 → error
.fwrite_skip_inv
POP BC
POP DE
POP HL
JR .fwrite2

.fwrite_inv_err
POP BC
POP DE
POP HL
LD HL,$FFFF
SCF
RET

.fwrite2
CALL LSXCALCFCB
LD (LSXFCB),HL

PUSH BC               ; save original size

.fwrite_loop
LD A,B
OR A
JR NZ,.fwrite_chunk
LD A,C
CP 128
JR C,.fwrite_done
.fwrite_chunk

; SETDTA = DE
PUSH BC
PUSH DE
LD C,$1A
PUSH IY
CALL BDOS
POP IY
; _WRRND
LD DE,(LSXFCB)
LD C,$22
PUSH IY
CALL BDOS
POP IY                ; A = result
POP DE
POP BC

OR A
JR NZ,.fwrite_err     ; A != 0 = error (CP/M write には EOF concept なし)

CALL FCBRECINC
PUSH HL
LD HL,128
ADD HL,DE
EX DE,HL
POP HL
LD A,C
SUB 128
LD C,A
LD A,B
SBC A,0
LD B,A
JP .fwrite_loop

.fwrite_done
POP HL
OR A
SBC HL,BC
RET

.fwrite_err
POP HL
LD HL,$FFFF
SCF
RET


; @name ACTBUF_FLUSH
; @resident shared
; @calls FWORK,FCBRECINC,LSXFILE
;
; ACTBUFDIRTY が立っていれば、ACTBUF を ACTBUFFNUM の current random record
; に書き、advance、ACTBUF を 0 で埋め直し、ACTBUFOFS=0, ACTBUFDIRTY=0。
; ACTBUFMODE / ACTBUFFNUM は維持 (= active 状態継続)。
;
; 戻り値: A = BDOS _WRRND の結果。0 = 成功、!= 0 = error (disk full / etc)
;        error 時は dirty を保持 (= ACTBUFOFS / ACTBUFDIRTY / ACTBUF 不変)
;        するので caller は再試行可能。FCBRECINC も実行されない。

LD A,(ACTBUFDIRTY)
OR A
RET Z              ; not dirty: A=0 で OK

; FCB アドレス計算
LD A,(ACTBUFFNUM)
LD L,A
LD H,0
CALL LSXCALCFCB
LD (LSXFCB),HL

; SETDTA = ACTBUF
LD DE,ACTBUF
LD C,$1A
PUSH IY
CALL BDOS
POP IY

; _WRRND
LD DE,(LSXFCB)
LD C,$22
PUSH IY
CALL BDOS
POP IY

; A = _WRRND result。0=success、それ以外は error
OR A
RET NZ             ; error: dirty 保持、caller に伝播

; success path
CALL FCBRECINC

; ACTBUFOFS=0, ACTBUFDIRTY=0
XOR A
LD (ACTBUFOFS),A
LD (ACTBUFDIRTY),A

; zero ACTBUF (128 byte)
LD HL,ACTBUF
LD DE,ACTBUF+1
LD BC,127
LD (HL),0
LDIR
XOR A              ; success: return A=0
RET


; @name ACTBUF_INVALIDATE
; @resident shared
; @calls FWORK,ACTBUF_FLUSH
;
; active buffer を完全クリア。dirty なら ACTBUF_FLUSH してから ACTBUFMODE=0。
; FREAD/FWRITE/FSEEK 入口で同 fnum の active を強制 invalidate するのに使う。
;
; 戻り値: A = ACTBUF_FLUSH の結果。0 = 成功、!= 0 = flush error (= dirty 保持、
;        ACTBUFMODE もそのまま)。caller は再試行 / abort を判断できる。

CALL ACTBUF_FLUSH
OR A
RET NZ             ; flush 失敗: dirty 保持、MODE もそのまま、error 伝播
LD (ACTBUFMODE),A  ; A=0
RET


; @name FCBRECINC
; @resident shared
; FCB+33..36 (random record) を 4 byte で +1 する内部 helper。
; FREAD/FWRITE が _RDRND/_WRRND 成功後に呼び、sequential semantics を維持。
; レジスタ: HL のみ破壊
LD HL,(LSXFCB)
PUSH BC
LD BC,33
ADD HL,BC
POP BC
INC (HL)
RET NZ
INC HL
INC (HL)
RET NZ
INC HL
INC (HL)
RET NZ
INC HL
INC (HL)
RET


; @name FWORK
; @resident shared
; @works LSXFCBS:296,LSXFCB:2,LSXFMODE:2,ACTBUF:128,ACTBUFFNUM:1,ACTBUFOFS:1,ACTBUFMODE:1,ACTBUFDIRTY:1
;
; ACTBUF: FGETC/FPUTC 用 1 record バッファ (128 byte)
; ACTBUFFNUM: アクティブ fnum (0..7)、$FF = none
; ACTBUFOFS: ACTBUF 内の current offset (0..127、128 = sentinel for forced reload)
; ACTBUFMODE: 0=none, 1=read (FGETC), 2=write (FPUTC)
; ACTBUFDIRTY: 1 = write 内容が flush されていない、0 = clean


