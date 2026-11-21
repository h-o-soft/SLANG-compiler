; Converted from lib/libdef/liblsx_file.yml
; SLANG Runtime Library (new format)

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

CALL LSXFCHECKNUM
JP C,.fgetc1
; return $FF
LD HL,255
RET

.fgetc1
; LSXFCB=fnum*37+LSXFCBS
CALL LSXCALCFCB
LD (LSXFCB),HL

; SET DTA
PUSH HL
LD  DE,.fgetbuf
LD  C,$1A ; _SETDTA
CALL  BDOS
POP HL

; FCB head to DE
LD E,L
LD D,H

; SET RECORD SIZE(FCB+14)
LD BC,14
ADD HL,BC
LD (HL),1
INC HL
LD (HL),0

LD HL,1     ; read record size

LD C,$27    ; _RDBLK
PUSH IY
CALL BDOS
POP IY
CP 0
JR NZ,.fgeterr

; ok
LD A,(.fgetbuf)
LD L,A
LD H,0
LD A,0
RET

.fgeterr
LD H,$FF
LD L,1    ; error code (tekito-)
LD A,L
SCF
RET

.fgetbuf
DS  1


; @name FPUTC
; @resident shared
; @calls LSXCALLS,FWORK,LSXFILE
; HL=fnum DE=chr

CALL LSXFCHECKNUM
JP C,.fgetc1
; return $FF
LD HL,255
RET

.fgetc1
; LSXFCB=fnum*37+LSXFCBS
CALL LSXCALCFCB
LD (LSXFCB),HL


; SET DTA
PUSH HL

LD HL,.fputbuf
LD (HL),E

LD  DE,.fputbuf
LD  C,$1A ; _SETDTA
CALL  BDOS
POP HL

; FCB head to DE
LD E,L
LD D,H

; SET RECORD SIZE(FCB+14)
LD BC,14
ADD HL,BC
LD (HL),1
INC HL
LD (HL),0

LD HL,1     ; write record size

LD C,$26    ; _WRBLK
PUSH IY
CALL BDOS
POP IY
CP 0
JR NZ,.fputerr

; ok
LD A,(.fputbuf)
LD L,A
LD H,0
LD A,0
RET

.fputerr
LD H,$FF
LD L,1    ; error code (tekito-)
LD A,L
SCF
RET

.fputbuf
DS  1


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


; @name FREADWRITE
; @resident shared
; SET DTA (DE)
PUSH BC
LD  C,$1A ; _SETDTA
PUSH IY
CALL  BDOS
POP IY

; SET RECORD SIZE(FCB+14)
LD HL,(LSXFCB)
LD BC,14
ADD HL,BC
LD (HL),1
INC HL
LD (HL),0

POP BC

LD HL,(LSXFCB)  ; DE->FCB
EX DE,HL

LD L,C          ; HL->read record size
LD H,B

RET


; @name FREAD
; @resident shared
; @calls LSXCALLS,FWORK,LSXFILE,FREADWRITE
; HL=fnum DE=address BC=size

CALL LSXFCHECKNUM
JP C,.fread1
; return $FF
LD HL,255
RET

.fread1
; LSXFCB=fnum*37+LSXFCBS
CALL LSXCALCFCB
LD (LSXFCB),HL

CALL FREADWRITE

LD C,$27
PUSH IY
CALL BDOS
POP IY
ADD A,1
SBC A,A
RET NC
LD L,A
LD H,A  ;HL=$FFFF CF=1
RET


; @name FWRITE
; @resident shared
; @calls LSXCALLS,FWORK,LSXFILE,FREADWRITE
; HL=fnum DE=address BC=size

CALL LSXFCHECKNUM
JP C,.fread1
; return $FF
LD HL,255
RET

.fread1
; LSXFCB=fnum*37+LSXFCBS
CALL LSXCALCFCB
LD (LSXFCB),HL

CALL FREADWRITE

LD C,$26
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




; @name FDELETE
; @resident shared
; @param_count 1
; @calls LSXCALLS,FOPEN,FWORKX
; HL=name。消せたら 0、無い・消せないときは 255。
; 名前に * か ? があれば、何も消さずに 255 を返す (まとめて消さない)
PUSH HL
CALL LSXXCLR
POP DE
LD HL,LSXTFCB
CALL LSXXNAME
JP C,LSXXERR
LD DE,LSXTFCB
LD C,$13  ; _FDEL
CALL LSXXBDOS
LD L,A
LD H,0
RET


; @name FRENAME
; @resident shared
; @param_count 2
; @calls LSXCALLS,FOPEN,FWORKX
; HL=old DE=new。変えられたら 0、できないときは 255。
; どちらかの名前に * か ? があれば 255
PUSH DE
PUSH HL
CALL LSXXCLR
POP DE
LD HL,LSXTFCB
CALL LSXXNAME
POP DE
JP C,LSXXERR
LD HL,LSXTFCB+16  ; 新しい名前は FCB+10H (ドライブ) と +11H (名前)
CALL LSXXNAME
JP C,LSXXERR
LD DE,LSXTFCB
LD C,$17  ; _FREN
CALL LSXXBDOS
LD L,A
LD H,0
RET


; @name FFIRST
; @resident shared
; @param_count 1
; @calls LSXCALLS,FWORKX
; HL=buf (32 バイト)。今のドライブの今のフォルダの最初の項目を buf に写して 0、無ければ $FFFF
PUSH HL
CALL LSXXCLR
LD HL,LSXTFCB+1
LD B,11
LD A,'?'
.wild
LD (HL),A
INC HL
DJNZ .wild
LD C,$11  ; _SFIRST
JP LSXXFIND


; @name FNEXT
; @resident shared
; @param_count 1
; @calls LSXCALLS,FWORKX
; HL=buf (32 バイト)。FFIRST の続きの項目を buf に写して 0、もう無ければ $FFFF
PUSH HL
LD C,$12  ; _SNEXT
JP LSXXFIND


; @name FWORKX
; @resident shared
; @calls LSXCALLS
; @works LSXTFCB:37,LSXDTA:33
; FDELETE・FRENAME・FFIRST・FNEXT の作業の FCB と DTA、共通の下回り

; 作業の FCB を 0 で埋める
LSXXCLR:
LD HL,LSXTFCB
LD B,37
XOR A
.clr
LD (HL),A
INC HL
DJNZ .clr
RET

; HL=書く先 (FCB+0 か FCB+10H)、DE=名前。ドライブと 8+3 の名前にする。
; ワイルドカード (* か ?) が入っていれば Cy=1
LSXXNAME:
PUSH HL
CALL PPATH
POP HL
INC HL
LD B,11
.chk
LD A,(HL)
CP '?'
SCF
RET Z
INC HL
DJNZ .chk
OR A
RET

LSXXERR:
LD HL,255
RET

LSXXBDOS:
PUSH IY
CALL BDOS
POP IY
RET

; C=11H か 12H、スタックの上に buf (呼んだ側から JP で来る)。
; 見つかれば項目 32 バイトを buf に写して HL=0、無ければ HL=$FFFF。
; どちらでも、戻る前に DTA を既定の 0080H に戻す (今の DTA を読む呼び出しが無いので、元の値には戻せない)
LSXXFIND:
PUSH BC
LD DE,LSXDTA
LD C,$1A  ; _SETDTA
CALL LSXXBDOS
POP BC
LD DE,LSXTFCB
CALL LSXXBDOS
PUSH AF
LD DE,$0080
LD C,$1A  ; _SETDTA
CALL LSXXBDOS
POP AF
POP DE
OR A
JR NZ,.none
LD HL,LSXDTA+1
LD BC,32
LDIR
LD HL,0
RET
.none
LD HL,$FFFF
RET
