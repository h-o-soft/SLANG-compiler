; cpm 環境向け file ライブラリ (CP/M 2.2 互換)
;
; liblsx_file.asm のコピー + 以下の差分:
;   - FREAD/FWRITE: CP/M 3+ の _RDBLK ($27) / _WRBLK ($26) を CP/M 2.2 互換の
;                   _RDREC ($14) / _WRREC ($15) に置換 (= 1 record/128 byte 固定)
;   - FGETC/FPUTC: スタブ ($FF return) — scope B で 128 byte 内部バッファ実装予定
;   - FREADWRITE: 廃止 (set record size 1 は CP/M 3+ 機能)
;
; 同名関数 (FOPEN/FREAD 等) と work 変数 (LSXFCB/LSXFMODE/LSXFCBS) は liblsx_file
; と完全に同じ。env では cpm.env のみがこのファイルを参照、liblsx_file は他 env
; が継続使用するので衝突しない。
;
; scope A 制約 (本 PR 範囲):
;   FREAD: 1 record (128 byte) 固定 read、size パラメータは無視
;   FWRITE: 1 record 固定 write
;   FGETC/FPUTC: 未実装 ($FF を return)
;
; scope B (follow-up PR):
;   FREAD/FWRITE を size に応じて multi-record loop 化、FGETC/FPUTC を 128 byte
;   buffer + offset 管理で実装

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
; @calls LSXCALLS,FWORK,LSXFILE,NEGHL
; HL=fnum DE=offset BC=mode(0=head, 1=current, 2=tail)
CALL LSXFCHECKNUM
JP C,.fseek1
; return $FF
LD HL,255
RET

.fseek1
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
; @calls LSXCALLS,FWORK,LSXFILE
; HL=fnum
;
; scope A スタブ: 常に $FF (失敗) を返す。
; scope B で 128 byte 内部バッファ + offset 管理で実装予定。
; (CP/M 2.2 は record size = 1 byte の連続 read をサポートしないため、
;  buffered read に切り替える必要がある。)

LD HL,255
RET


; @name FPUTC
; @resident shared
; @calls LSXCALLS,FWORK,LSXFILE
; HL=fnum DE=chr
;
; scope A スタブ: 常に $FF (失敗) を返す。
; scope B で 128 byte 内部バッファ + flush で実装予定。

LD HL,255
RET


; @name FCLOSE
; @resident shared
; @calls LSXCALLS,FWORK,LSXFILE
; HL=fnum
CALL LSXCALCFCB
EX DE,HL
LD C,$10  ; _FCLOSE
PUSH IY
CALL BDOS
POP IY
LD L,A
LD H,0
RET


; @name FREAD
; @resident shared
; @calls LSXCALLS,FWORK,LSXFILE
; HL=fnum DE=address BC=size
;
; CP/M 2.2 (RunCPM) 制約: 1 record (128 byte) 固定 read。size パラメータは
; 現状無視 — overlay <= 128B 用途で十分。scope B で multi-record loop に
; 拡張予定 (BC を 128 ずつ消費するループ + EOF 処理)。

CALL LSXFCHECKNUM
JP C,.fread1
; return $FF (bad fnum)
LD HL,255
RET

.fread1
; LSXFCB=fnum*37+LSXFCBS
CALL LSXCALCFCB
LD (LSXFCB),HL

; SET DTA = address (DE)
LD C,$1A      ; _SETDTA
PUSH IY
CALL BDOS
POP IY

; _RDREC (sequential read, CP/M 2.2 互換)
LD DE,(LSXFCB)
LD C,$14      ; _RDREC
PUSH IY
CALL BDOS
POP IY

; A=0 success / A=1 EOF (partial) / A>=$10 read error
; scope A は EOF も成功扱い (overlay は EOF 寸前まで読み切れば OK)。
; ADD A,1 で 0→1, 1→2, $FF→0 となるが、$FF は CP/M 2.2 では返らないので
; 「CY=1 == 真の error」のみ判定する素直な実装に変える:
OR A
JR Z,.fread_ok
CP 2
JR NC,.fread_err
.fread_ok
LD HL,0
RET

.fread_err
LD HL,$FFFF
SCF
RET


; @name FWRITE
; @resident shared
; @calls LSXCALLS,FWORK,LSXFILE
; HL=fnum DE=address BC=size
;
; CP/M 2.2 (RunCPM) 制約: 1 record (128 byte) 固定 write。size パラメータは
; 現状無視 — scope B で multi-record loop に拡張予定。

CALL LSXFCHECKNUM
JP C,.fwrite1
; return $FF (bad fnum)
LD HL,255
RET

.fwrite1
; LSXFCB=fnum*37+LSXFCBS
CALL LSXCALCFCB
LD (LSXFCB),HL

; SET DTA = address (DE)
LD C,$1A      ; _SETDTA
PUSH IY
CALL BDOS
POP IY

; _WRREC (sequential write, CP/M 2.2 互換)
LD DE,(LSXFCB)
LD C,$15      ; _WRREC
PUSH IY
CALL BDOS
POP IY

LD L,A
LD H,0
RET


; @name FWORK
; @resident shared
; @works LSXFCBS:296,LSXFCB:2,LSXFMODE:2
;


