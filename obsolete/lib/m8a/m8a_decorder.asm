;※単体利用する場合は
;  を付加してください

;------------------------------------------------------------------------------
#LIB M8ALOAD

WIDTH		EQU	40

DRAWSPEED	EQU	0	;0=省サイズ低速/1=中速/1以上=ループ展開高速
CALCSPEED	EQU	0	;0=計算取得低速/0以外=TABLE取得高速

DRAWM8A:
; input: HL=データ開始位置
;        DE=横方向描画開始位置(X)
;        BC=縦方向描画開始位置(Y)
	LD D,E
	LD E,C

#IF CALCSPEED == 0
	; 現在のWIDTH値を入れる(CALCSPEEDが0の時のみ動的にWIDTH切り替えが可能となる)
	LD A,(NAME_SPACE_DEFAULT.AT_WIDTH)
	LD (REWRITE0Y+1),A
#ENDIF

;M8A画像を描画する
; input: HL=データ開始位置
;        D=横方向描画開始位置(X)
;        E=縦方向描画開始位置(Y)
;
; 破壊: AF,BC,DE,HL,BC',DE',HL',IX
;
; (data header)
;	DB "M8A"	;magic 'M8A'
;	DB $00		;予約
;	DB (幅-1)\8
;	DB 高さ
;
; (data format)
;	DB nnGRBGRB
;	   | |  |
;	   | |  dot1
;	   | dot2
;	   nn=0 : next byte is repeat length(1～256)
;	   nn!=0: repeat length (1～3)
;
; HL .... DATA POINTER
; B ..... WIDTH COUNTER
; C ..... HEIGHT COUNTER
;
; BC' ... VRAM address
; D' .... BLUE
; E' .... RED
; H' .... GREEN
; L' .... TEMP.
;
; IXH ... color code
; IXL ... repeat counter
;
;------------------------------------------------------------------------------
	LD	A, E		; ずらし幅縦
	LD	(REWRITE00+1), A
	LD	A, D		;
	LD	(REWRITE0A+1), A

	; header read
	LD	BC, $0303	; check magic'M8A'
	LD	DE, MAGICM8A
MGCCHKLP:
	LD	A, (DE)
	INC	DE
	CPI
	JP	NZ, ERREND
	DJNZ	MGCCHKLP

	INC	HL		;予約

	LD	A, (HL)		; 幅（横方向繰り返し数）
	INC	HL
	LD	(REWRITE0X+1), A

	LD	A, (HL)		; 高さ（縦方向繰り返し数）
	INC	HL
	LD	(REWRITE0Z+1), A


;------------------------------------------------------------------------------
	CALL	SET_GDAT		; 色コード＆繰り返し回数を取得
	LD	B, 0			; 縦にY回
;------------------------------------------------------------------------------
DRAWM8A00:
REWRITE00:
	LD	A, 0			; A=縦方向ズラし長
	ADD	A, B			; A=縦座標

REWRITE0X:
	LD	C, $00			; C=横にX回
	EXX				; 裏へ

	; 左端座標計算
#IF CALCSPEED == 0
		LD	C, A		; 左端座標の計算...(Y AND 7)*2^11 + (Y \ 8)*WIDTH
		AND	7		; Y AND 7
		ADD	A, A
		ADD	A, A
		ADD	A, A
		LD	(REWRITEA+1), A		; (Y AND 7)*2^11
		LD	A, C
		RRCA
		RRCA
		RRCA
		AND	00011111B	; (Y \ 8)
REWRITE0Y:
		LD	B, WIDTH
AXBHL:					; A×B = HL
		LD	HL, 0		; 結果をクリア
		LD	D, H		; D=0
		LD	E, B		; DE=B
		LD	B, 8		; 8bitぶん繰り返す(counter)
AXBHL00:
		RRCA			; 最下位bitがCyに入る
		JR	NC, AXBHL01
		ADD	HL, DE		; Cy=1ならDEを加える
AXBHL01:
		SLA	E
		RL	D		; DEをシフト
		DJNZ	AXBHL00

REWRITEA:
		LD	A, 0		; 自己書き換え
		ADD	A, H
		LD	B, A
		LD	C, L
		SET	6, B		;
#ELSE
		LD	H, HIGH GVRAMADRS_LO
		LD	L, A
		LD	C, (HL)
		INC	H
		LD	B, (HL)
		SET	6, B		;
#ENDIF

REWRITE0A:
		LD	A, 0		; A=横方向ズラし長
		ADD	A, C
		LD	C, A
		JR	NC, HOGE
		INC	B
HOGE:
	EXX				; 表へ
;------------------------------------------------------------------------------
DRAWM8A01:
#IF DRAWSPEED == 0
	; 描画処理（低速）
	EXX				; 裏へ
		LD	L, 4		; 4回繰り返す
BITSET:
		LD	A, IXH		; A=色コード
		CALL	SETBITSUB
		CALL	SETBITSUB
		DEC	IXL		; rep counter--
		CALL	Z, SET_GDAT2	; カウンタが0になったら再度データを取得
		DEC	L
		JR	NZ, BITSET
#ENDIF
#IF DRAWSPEED == 1
	; 描画処理（中速）
	EXX				; 裏へ
		LD	L, 4		; 4回繰り返す
BITSET:
		LD	A, IXH		; A=色コード
		RRA			; set blue dot
		RL	D		
		RRA			; set red dot
		RL	E		
		RRA			; set green dot
		RL	H		
		RRA			; set blue dot
		RL	D		
		RRA			; set red dot
		RL	E		
		RRA			; set green dot
		RL	H		
		DEC	IXL		; rep counter--
		CALL	Z, SET_GDAT2	; カウンタが0になったら再度データを取得
		DEC	L
		JR	NZ, BITSET
#ENDIF
#IF DRAWSPEED > 1
	; 描画処理（高速）
	EXX				; 裏へ
		LD	A, IXH		; A=色コード
BITSET07:
		RRA			; set blue dot
		RL	D		
		RRA			; set red dot
		RL	E		
		RRA			; set green dot
		RL	H		
BITSET06:
		RRA			; set blue dot
		RL	D		
		RRA			; set red dot
		RL	E		
		RRA			; set green dot
		RL	H		
		DEC	IXL		; rep counter--
		CALL	Z, SET_GDAT2	; カウンタが0になったら再度データを取得
		LD	A, IXH		; A=色コード
BITSET05:
		RRA			; set blue dot
		RL	D		
		RRA			; set red dot
		RL	E		
		RRA			; set green dot
		RL	H		
BITSET04:
		RRA			; set blue dot
		RL	D		
		RRA			; set red dot
		RL	E		
		RRA			; set green dot
		RL	H		
		DEC	IXL		; rep counter--
		CALL	Z, SET_GDAT2	; カウンタが0になったら再度データを取得
		LD	A, IXH		; A=色コード
BITSET03:
		RRA			; set blue dot
		RL	D		
		RRA			; set red dot
		RL	E		
		RRA			; set green dot
		RL	H		
BITSET02:
		RRA			; set blue dot
		RL	D		
		RRA			; set red dot
		RL	E		
		RRA			; set green dot
		RL	H		
		DEC	IXL		; rep counter--
		CALL	Z, SET_GDAT2	; カウンタが0になったら再度データを取得
		LD	A, IXH		; A=色コード
BITSET01:
		RRA			; set blue dot
		RL	D		
		RRA			; set red dot
		RL	E		
		RRA			; set green dot
		RL	H		
BITSET00:
		RRA			; set blue dot
		RL	D		
		RRA			; set red dot
		RL	E		
		RRA			; set green dot
		RL	H		
		DEC	IXL		; rep counter--
		CALL	Z, SET_GDAT2	; カウンタが0になったら再度データを取得
#ENDIF

; GVRAMに転送
TRANS2GVRAM:
		LD	L, B		; 一時保存
		OUT	(C), D		; BLUE out
		LD	A, $40		; B->R面
		ADD	A, B		
		LD	B, A		
		OUT	(C), E		; RED out
		SET	6, B		; R->G面
		OUT	(C), H		; GREEN out
		LD	B, L		; BLUEに戻す
		INC	BC		; 表示位置を横方向に++
	EXX				; 表へ
	DEC	C			; 横方向カウンタ--
	JR	NZ, DRAWM8A01
	INC	B			; 縦方向カウンタ++
REWRITE0Z:
	LD	A, 200			; 縦にY回
	CP	B
	JP	NZ, DRAWM8A00
	RET

;------------------------------------------------------------------------------
#IF DRAWSPEED == 0
SETBITSUB:
		RRA			; set blue dot
		RL	D		
		RRA			; set red dot
		RL	E		
		RRA			; set green dot
		RL	H		
		RET
#ENDIF

;------------------------------------------------------------------------------
; 圧縮データから設定を取得(裏)
SET_GDAT2:
	EXX			;表へ
	CALL	SET_GDAT
	EXX			;裏へ
	RET
;------------------------------------------------------------------------------
; 圧縮データから設定を取得
;in:  HL =read address
;out: IXH=color code
;     IXL=rep counter
SET_GDAT:
	LD	A, (HL)			; 色コードを取得
	INC	HL
	LD	IXH, A			; 色コードを設定
	AND	11000000B
	JR	Z, SET_GDAT02		; 上位2bitが00の場合は、次のバイトが繰り返し回数-1

	LD	A, IXH			; 上位2bitが00でない場合は色コードを戻す（繰り返し数を取得）
	RLCA
	RLCA
	AND	00000011B		; A=繰り返し回数
	LD	IXL, A			; 繰り返し回数を設定
	RET

SET_GDAT02:				; 上位2bitが00の場合
	LD	A, (HL)			; 繰り返し回数を取得
	INC	HL
	INC	A			; +1する
	LD	IXL, A			; 繰り返し回数を設定
	RET

;------------------------------------------------------------------------------
ERREND:
	SCF
	RET




;------------------------------------------------------------------------------
#IF CALCSPEED != 0 && WIDTH == 40
	ALIGN	256
	; WIDTH40
GVRAMADRS_LO:
	DB	$00,$00,$00,$00,$00,$00,$00,$00		;1	8x 0=  0
	DB	$28,$28,$28,$28,$28,$28,$28,$28		;2	8x 1=  8
	DB	$50,$50,$50,$50,$50,$50,$50,$50		;3	8x 2= 16
	DB	$78,$78,$78,$78,$78,$78,$78,$78		;4	8x 3= 24
	DB	$a0,$a0,$a0,$a0,$a0,$a0,$a0,$a0		;5	8x 4= 32
	DB	$c8,$c8,$c8,$c8,$c8,$c8,$c8,$c8		;6	8x 5= 40
	DB	$f0,$f0,$f0,$f0,$f0,$f0,$f0,$f0		;7	8x 6= 48
	DB	$18,$18,$18,$18,$18,$18,$18,$18		;8	8x 7= 56
	DB	$40,$40,$40,$40,$40,$40,$40,$40		;9	8x 8= 64
	DB	$68,$68,$68,$68,$68,$68,$68,$68		;10	8x 9= 72
	DB	$90,$90,$90,$90,$90,$90,$90,$90		;11	8x10= 80
	DB	$b8,$b8,$b8,$b8,$b8,$b8,$b8,$b8		;12	8x11= 88
	DB	$e0,$e0,$e0,$e0,$e0,$e0,$e0,$e0		;13	8x12= 96
	DB	$08,$08,$08,$08,$08,$08,$08,$08		;14	8x13=104
	DB	$30,$30,$30,$30,$30,$30,$30,$30		;15	8x14=112
	DB	$58,$58,$58,$58,$58,$58,$58,$58		;16	8x15=120
	DB	$80,$80,$80,$80,$80,$80,$80,$80		;17	8x16=128
	DB	$a8,$a8,$a8,$a8,$a8,$a8,$a8,$a8		;18	8x17=136
	DB	$d0,$d0,$d0,$d0,$d0,$d0,$d0,$d0		;19	8x18=144
	DB	$f8,$f8,$f8,$f8,$f8,$f8,$f8,$f8		;20	8x19=152
	DB	$20,$20,$20,$20,$20,$20,$20,$20		;21	8x20=160
	DB	$48,$48,$48,$48,$48,$48,$48,$48		;22	8x21=168
	DB	$70,$70,$70,$70,$70,$70,$70,$70		;23	8x22=176
	DB	$98,$98,$98,$98,$98,$98,$98,$98		;24	8x23=184
	DB	$c0,$c0,$c0,$c0,$c0,$c0,$c0,$c0		;25	8x24=192
	DB	$e8,$e8,$e8,$e8,$e8,$e8,$e8,$e8		;26	8x25=200

	ALIGN	256
GVRAMADRS_HI:
	DB	$40,$48,$50,$58,$60,$68,$70,$78		;1	8x 0=  0
	DB	$40,$48,$50,$58,$60,$68,$70,$78		;2	8x 1=  8
	DB	$40,$48,$50,$58,$60,$68,$70,$78		;3	8x 2= 16
	DB	$40,$48,$50,$58,$60,$68,$70,$78		;4	8x 3= 24
	DB	$40,$48,$50,$58,$60,$68,$70,$78		;5	8x 4= 32
	DB	$40,$48,$50,$58,$60,$68,$70,$78		;6	8x 5= 40
	DB	$40,$48,$50,$58,$60,$68,$70,$78		;7	8x 6= 48
	DB	$41,$49,$51,$59,$61,$69,$71,$79		;8	8x 7= 56
	DB	$41,$49,$51,$59,$61,$69,$71,$79		;9	8x 8= 64
	DB	$41,$49,$51,$59,$61,$69,$71,$79		;10	8x 9= 72
	DB	$41,$49,$51,$59,$61,$69,$71,$79		;11	8x10= 80
	DB	$41,$49,$51,$59,$61,$69,$71,$79		;12	8x11= 88
	DB	$41,$49,$51,$59,$61,$69,$71,$79		;13	8x12= 96
	DB	$42,$4a,$52,$5a,$62,$6a,$72,$7a		;14	8x13=104
	DB	$42,$4a,$52,$5a,$62,$6a,$72,$7a		;15	8x14=112
	DB	$42,$4a,$52,$5a,$62,$6a,$72,$7a		;16	8x15=120
	DB	$42,$4a,$52,$5a,$62,$6a,$72,$7a		;17	8x16=128
	DB	$42,$4a,$52,$5a,$62,$6a,$72,$7a		;18	8x17=136
	DB	$42,$4a,$52,$5a,$62,$6a,$72,$7a		;19	8x18=144
	DB	$42,$4a,$52,$5a,$62,$6a,$72,$7a		;20	8x19=152
	DB	$43,$4b,$53,$5b,$63,$6b,$73,$7b		;21	8x20=160
	DB	$43,$4b,$53,$5b,$63,$6b,$73,$7b		;22	8x21=168
	DB	$43,$4b,$53,$5b,$63,$6b,$73,$7b		;23	8x22=176
	DB	$43,$4b,$53,$5b,$63,$6b,$73,$7b		;24	8x23=184
	DB	$43,$4b,$53,$5b,$63,$6b,$73,$7b		;25	8x24=192
	DB	$43,$4b,$53,$5b,$63,$6b,$73,$7b		;26	8x25=200
#ELIF CALCSPEED != 0 && WIDTH == 80
	; WIDTH80
GVRAMADRS_LO:
	DB	$00,$00,$00,$00,$00,$00,$00,$00		;1	  0-  7
	DB	$50,$50,$50,$50,$50,$50,$50,$50		;2	  8- 15
	DB	$a0,$a0,$a0,$a0,$a0,$a0,$a0,$a0		;3	 16- 23
	DB	$f0,$f0,$f0,$f0,$f0,$f0,$f0,$f0		;4	 24- 31
	DB	$40,$40,$40,$40,$40,$40,$40,$40		;5	 32- 39
	DB	$90,$90,$90,$90,$90,$90,$90,$90		;6	 40- 47
	DB	$e0,$e0,$e0,$e0,$e0,$e0,$e0,$e0		;7	 48- 55
	DB	$30,$30,$30,$30,$30,$30,$30,$30		;8	 56- 63
	DB	$80,$80,$80,$80,$80,$80,$80,$80		;9	 64- 71
	DB	$d0,$d0,$d0,$d0,$d0,$d0,$d0,$d0		;10	 72- 79
	DB	$20,$20,$20,$20,$20,$20,$20,$20		;11	 80- 87
	DB	$70,$70,$70,$70,$70,$70,$70,$70		;12	 88- 95
	DB	$c0,$c0,$c0,$c0,$c0,$c0,$c0,$c0		;13	 96-103
	DB	$10,$10,$10,$10,$10,$10,$10,$10		;14	104-111
	DB	$60,$60,$60,$60,$60,$60,$60,$60		;15	112-119
	DB	$b0,$b0,$b0,$b0,$b0,$b0,$b0,$b0		;16	120-127
	DB	$00,$00,$00,$00,$00,$00,$00,$00		;17	128-135
	DB	$50,$50,$50,$50,$50,$50,$50,$50		;18	136-143
	DB	$a0,$a0,$a0,$a0,$a0,$a0,$a0,$a0		;19	144-151
	DB	$f0,$f0,$f0,$f0,$f0,$f0,$f0,$f0		;20	152-159
	DB	$40,$40,$40,$40,$40,$40,$40,$40		;21	160-167
	DB	$90,$90,$90,$90,$90,$90,$90,$90		;22	168-175
	DB	$e0,$e0,$e0,$e0,$e0,$e0,$e0,$e0		;23	176-183
	DB	$30,$30,$30,$30,$30,$30,$30,$30		;24	184-191
	DB	$80,$80,$80,$80,$80,$80,$80,$80		;25	192-200
	DB	$d0,$d0,$d0,$d0,$d0,$d0,$d0,$d0		;26

	ALIGN 256
GVRAMADRS_HI:
	DB	$00,$08,$10,$18,$20,$28,$30,$38		;1	  0-  7
	DB	$00,$08,$10,$18,$20,$28,$30,$38		;2	  8- 15
	DB	$00,$08,$10,$18,$20,$28,$30,$38		;3	 16- 23
	DB	$00,$08,$10,$18,$20,$28,$30,$38		;4	 24- 31
	DB	$01,$09,$11,$19,$21,$29,$31,$39		;5	 32- 39
	DB	$01,$09,$11,$19,$21,$29,$31,$39		;6	 40- 47
	DB	$01,$09,$11,$19,$21,$29,$31,$39		;7	 48- 55
	DB	$02,$0a,$12,$1a,$22,$2a,$32,$3a		;8	 56- 63
	DB	$02,$0a,$12,$1a,$22,$2a,$32,$3a		;9	 64- 71
	DB	$02,$0a,$12,$1a,$22,$2a,$32,$3a		;10	 72- 79
	DB	$03,$0b,$13,$1b,$23,$2b,$33,$3b		;11	 80- 87
	DB	$03,$0b,$13,$1b,$23,$2b,$33,$3b		;12	 88- 95
	DB	$03,$0b,$13,$1b,$23,$2b,$33,$3b		;13	 96-103
	DB	$04,$0c,$14,$1c,$24,$2c,$34,$3c		;14	104-111
	DB	$04,$0c,$14,$1c,$24,$2c,$34,$3c		;15	112-119
	DB	$04,$0c,$14,$1c,$24,$2c,$34,$3c		;16	120-127
	DB	$05,$0d,$15,$1d,$25,$2d,$35,$3d		;17	128-135
	DB	$05,$0d,$15,$1d,$25,$2d,$35,$3d		;18	136-143
	DB	$05,$0d,$15,$1d,$25,$2d,$35,$3d		;19	144-151
	DB	$05,$0d,$15,$1d,$25,$2d,$35,$3d		;20	152-159
	DB	$06,$0e,$16,$1e,$26,$2e,$36,$3e		;21	160-167
	DB	$06,$0e,$16,$1e,$26,$2e,$36,$3e		;22	168-175
	DB	$06,$0e,$16,$1e,$26,$2e,$36,$3e		;23	176-183
	DB	$07,$0f,$17,$1f,$27,$2f,$37,$3f		;24	184-191
	DB	$07,$0f,$17,$1f,$27,$2f,$37,$3f		;25	192-200
	DB	$07,$0f,$17,$1f,$27,$2f,$37,$3f		;26
#ENDIF

MAGICM8A:
     DB "M8A"


#ENDLIB

