; M8A(MASDX 8COLOR TYPE-A) GRAPHIC LOADER by hex125(293)

include "LDXEQU.ASM"

	ORG	$0100

	JP	START		; プログラム開始

ERR:
	LD	DE, ERRMES01	; 失敗時は終了
ERR2:
	LD	C, _STROUT	; 表示
	JP	SYSTEM

FILECLOSE:
	LD	DE, FCB1
	LD	C, _FCLOSE
	JP	SYSTEM

START:
	LD	DE, TITLE	; タイトル表示
	LD	C, _STROUT
	CALL	SYSTEM

	LD	A, (DTA1)	; 引数あるか？
	OR	A
	JR	NZ, START2

	LD	DE, USAGE	; 引数なければ使用方法表示
	JR	ERR2

START2:
	LD	DE, FCB1	; ファイルオープン
	LD	C, _FOPEN
	CALL	SYSTEM
	OR	A		
	JP	Z, READ

	CALL	FILECLOSE
	JR	ERR

READ:
	LD	HL, 1		; レコードサイズを1にする
	LD	(FCB1+14), HL

	DEC	HL		; ランダムレコード初期化
	LD	(FCB1+33), HL
	LD	(FCB1+35), HL

	LD	DE, BUFAD	; 読み出し先
	LD	C, _SETDTA	; DTAの設定
	CALL	SYSTEM

READLOOP01:
	LD	DE, FCB1
	LD	HL, (FCB1+16)	; 読み出すサイズ

	PUSH	HL
	LD	HL, ($0006)	; フリーエリアをチェック
	LD	DE, BUFAD
	OR	A
	SBC	HL, DE
	LD	D, H
	LD	E, L
	POP	HL

SIZECHK:
	PUSH	HL		; CP HL, DE
	OR	A
	SBC	HL, DE
	POP	HL

	JR	C, READSTART

ERR3:
	CALL	FILECLOSE	; Too large file.
	LD	DE, ERRMES02	; 終了
	JP	ERR2
	

READSTART:
	LD	DE, FCB1
	LD	C, _RDBLK
	CALL	SYSTEM

	CP	$ff
	JR	Z, READEXIT02

	PUSH	AF
	LD	DE, BUFAD

READLOOP02:
	LD	A, H
	OR	L
	JR	Z, READEXIT01

	LD	A, (DE)
	INC	DE

	DEC	HL
	JR	READLOOP02

READEXIT01:
	POP	AF
	OR	A
	JR	Z, READLOOP01

READEXIT02:
	CALL	FILECLOSE	; ファイルクローズ

PALETON:
	LD	BC, $10AA
	OUT	(C), C
	LD	BC, $11CC
	OUT	(C), C
	LD	BC, $12F0
	OUT	(C), C
	INC	B
	DB	$ED, $71	; OUT (C),0

SET_CRTC:
;	LD	A, (REWRITE00+1)
;	JP	DRAW_BG		; 描画



;------------------------------------------------------------------------------
#LIB M8ALOAD
DRAW_BG:
;背景画像を描画する(320x200固定)
; input: HL=データ開始位置
;        L =表示開始位置（縦）
;
; DB nnGRBGRB
;    | |  |
;    | |  dot1
;    | dot2
;    n=0 : next byte is repeat length
;    n!=0: repeat length (1～3)

; HL .... DATA POINTER
; B ..... WIDTH COUNTER(DEC)
; C ..... HEIGHT COUNTER(DEC)

; BC' ... VRAM address
; D' .... BLUE
; E' .... RED
; H' .... GREEN
; L' .... TEMP.

; IXH ... color code
; IXL ... rep counter	

;------------------------------------------------------------------------------
;;;;	LD	HL, BUFAD

PALETON:
	LD	BC, $10AA
	OUT	(C), C
	LD	BC, $11CC
	OUT	(C), C
	LD	BC, $12F0
	OUT	(C), C
	INC	B
	DB	$ED, $71	; OUT (C),0

	LD	A,0
	LD	BC,$1FD0
	OUT	(C),A

	LD	A, (HL)		; 幅（横方向繰り返し数）
	INC	HL
	LD	(REWRITE00+1), A


	CP	41
	JR	NC, WIDTH80
WIDTH40:
	LD	A, 40
	JR	WIDTH
WIDTH80:
	LD	A, 80
WIDTH:
	LD	(REWRITE02+1), A


	LD	A, (HL)		; 高さ（縦方向繰り返し数）
	INC	HL
	LD	(REWRITE01+1), A

;------------------------------------------------------------------------------
	CALL	SET_BGDAT			;	取得
	LD	B, 0				;	縦にY回
;------------------------------------------------------------------------------
DRAW_BG00:
	LD	A, B				;	A=縦座標
REWRITE00:
	LD	C, $00				;	C=横にX回

	EXX
		; 左端座標の計算
		;(Y AND 7)*2^11 + (Y \ 8)*B3	
		LD	C, A
		AND	7
		ADD	A, A
		ADD	A, A
		ADD	A, A
		LD	IYL, A

		LD	A, C
;		SRL	A		;
;		SRL	A		;
;		SRL	A		;24
		RRCA			;
		RRCA			;
		RRCA			;12
		AND	00011111B	;19


		LD	B, A
REWRITE02:
		LD	A, 0

	; A×B = HL
AXBHL:
	LD	HL, 0			; 21 00 00	結果をクリア
	LD	D, H			; 54		
	LD	E, B			; 58		DE=B
	LD	B, 8			; 06 08		8bitぶん繰り返す(counter)
AXBHL00:
	RRCA				; 0f		最下位bitがCyに入る
	JR	NC, AXBHL01		; 30 01		
	ADD	HL, DE			; 19		Cy=1ならDEを加える
AXBHL01:
	SLA	E			; cb 23
	RL	D			; cb 12		DEをシフト
	DJNZ	AXBHL00			; 10 f6


	; H+A
	LD	A, IYL
	ADD	A, H
	LD	B, A
	LD	C, L
	SET	6, B
	EXX

;------------------------------------------------------------------------------
DRAW_BG01:
	EXX
		LD	A, IXH		; A=色コード

BITSET07:
		RRA				; 1f		4	blue
		RL	D			; cb 12		8/12
		RRA				; 1f		4/16	red
		RL	E			; cb 13		8/24
		RRA				; 1f		4/28	green
		RL	H			; cb 12		8/40
BITSET06:
		RRA				; 1f		4/48	blue
		RL	D			; cb 12		8/56
		RRA				; 1f		4/60	red
		RL	E			; cb 13		8/68
		RRA				; 1f		4/72	green
		RL	H			; cb 12		8/84

		DEC	IXL			; dd 2d		8
		CALL	Z, SET_BGDAT2		; cc xx xx		カウンタが0になったら再度データを取得

		LD	A, IXH			; dd 7c		8	A=color code(HI)

BITSET05:
		RRA				; 1f		4	blue
		RL	D			; cb 12		8/12
		RRA				; 1f		4/16	red
		RL	E			; cb 13		8/24
		RRA				; 1f		4/28	green
		RL	H			; cb 12		8/40
BITSET04:
		RRA				; 1f		4/48	blue
		RL	D			; cb 12		8/56
		RRA				; 1f		4/60	red
		RL	E			; cb 13		8/68
		RRA				; 1f		4/72	green
		RL	H			; cb 12		8/84

		DEC	IXL			; dd 2d		8
		CALL	Z, SET_BGDAT2		; cc xx xx		カウンタが0になったら再度データを取得

		LD	A, IXH			; dd 7c		8	A=color code(HI)

BITSET03:
		RRA				; 1f		4	blue
		RL	D			; cb 12		8/12
		RRA				; 1f		4/16	red
		RL	E			; cb 13		8/24
		RRA				; 1f		4/28	green
		RL	H			; cb 12		8/40
BITSET02:
		RRA				; 1f		4/48	blue
		RL	D			; cb 12		8/56
		RRA				; 1f		4/60	red
		RL	E			; cb 13		8/68
		RRA				; 1f		4/72	green
		RL	H			; cb 12		8/84

		DEC	IXL			; dd 2d		8
		CALL	Z, SET_BGDAT2		; cc xx xx		カウンタが0になったら再度データを取得

		LD	A, IXH			; dd 7c		8	A=color code(HI)

BITSET01:
		RRA				; 1f		4	blue
		RL	D			; cb 12		8/12
		RRA				; 1f		4/16	red
		RL	E			; cb 13		8/24
		RRA				; 1f		4/28	green
		RL	H			; cb 12		8/40
BITSET00:
		RRA				; 1f		4/48	blue
		RL	D			; cb 12		8/56
		RRA				; 1f		4/60	red
		RL	E			; cb 13		8/68
		RRA				; 1f		4/72	green
		RL	H			; cb 12		8/84

		DEC	IXL			; dd 2d		8
		CALL	Z, SET_BGDAT2		; cc xx xx		カウンタが0になったら再度データを取得
;		LD	A, IXH			; dd 7c		8	A=color code(HI)

;------------------------------------------------------------------------------
; GVRAMに転送
TRANS2GVRAM:
		LD	L, B

		OUT	(C), D			; ed 51		12/23	BLUE out

		LD	A, $40			; 3e 40		 7/30	B->R面
		ADD	A, B			; 80		 4/34
		LD	B, A			; 47		 4/38
		OUT	(C), E			; ed 59		12/50	RED out

		SET	6, B			;			R->G面
		OUT	(C), H			; ed 79		12/	GREEN out

		LD	B, L

;------------------------------------------------------------------------------
		; 表示位置を横方向に--
		INC	BC

	EXX

	DEC	C				; 横方向
	JR	NZ, DRAW_BG01

;------------------------------------------------------------------------------
	;表示位置を縦方向に--
	INC	B
REWRITE01:
	LD	A, 200				; 縦にY回
	CP	B
	JP	NZ, DRAW_BG00
	RET

;------------------------------------------------------------------------------
; 圧縮データから設定を取得(裏)
SET_BGDAT2:
	EXX			;表へ
	CALL	SET_BGDAT
	EXX
	RET
;------------------------------------------------------------------------------
; 圧縮データから設定を取得
;in:  HL =read address
;out: IXH=color code
;     IXL=rep counter
SET_BGDAT:
	LD	A, (HL)			; 色コードを取得
	INC	HL
	LD	IXH, A			; 色コードを設定

	AND	11000000B
	JR	Z, SET_BGDAT02		; 上位2bitが0の場合は、次のバイトが繰り返し回数-1

	; 上位2bitが00でない場合
	LD	A,IXH			; 色コードをもどす（繰り返し数を取得）
	RLCA
	RLCA
	AND	00000011B		; A=繰り返し回数
	JR	SET_BGDAT03

SET_BGDAT02:				; 上位2bitが00の場合
	LD	A, (HL)			; 繰り返し回数を取得
	INC	HL
	INC	A			; +1する
SET_BGDAT03:
	LD	IXL, A			; 繰り返し回数を設定

	RET


;------------------------------------------------------------------------------
TITLE:
	DB	"M8A Graphic Loader v0.01 by hex125(293)",$0a,$0d,"$"
USAGE:
	DB	"Usage: M8A ﾌｧｲﾙﾈｰﾑ$"
ERRMES01:
	DB	"File not found.$"
ERRMES02:
	DB	"Too large file. Cannot show.$"

BUFAD:

#ENDLIB

