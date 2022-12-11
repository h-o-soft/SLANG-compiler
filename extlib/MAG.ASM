#LIB MAGBASE
;
;	MAG  by Gaku
;
THOLD	EQU	11
MAG_FP1 EQU 0

FLAGBF	EQU	((NAME_SPACE_DEFAULT.__WORKEND__ + 255) & 0xFF00)
LINEBF	EQU	FLAGBF+256

KWABF1:	EQU	LINEBF+16*512
KWABF2:	EQU	KWABF1+2048

BUFSIZ	EQU	4*1024
DTBUF	EQU	KWABF2+2048
DTBUFE	EQU	DTBUF+BUFSIZ

PDSIZE	EQU	8*1024
PDBUF	EQU	DTBUFE
PDBUFE	EQU	PDBUF+PDSIZE

HEADBF	EQU	PDBUFE
PALBF	EQU	HEADBF+32
#ENDLIB


#LIB GRDISP
    LD A,H
    OR L
    JR Z,GR_NODISP

    ; Z 互換(8色)モード
    LD	BC,$1FB0
    DB	$ED,$71

    ; $10xxに$AA(BLUE)
    LD	BC,$10AA
    OUT	(C),C
    ; $11xxに$CC(RED)
    LD	BC,$11CC
    OUT	(C),C
    ; $12xxにF0(GREEN)
    LD	BC,$12F0
    OUT	(C),C

    ; ; $13xxに0(描画優先度設定)→とりあえず触らない
    ; INC	B
    ; DB	$ED,$71
    RET

GR_NODISP:
    ; パレット設定を全て0にして非表示にする
    LD	B,$10
    DB	$ED,$71
    INC	B
    DB	$ED,$71
    INC	B
    DB	$ED,$71
    INC	B
    DB	$ED,$71
    RET
#ENDLIB

#LIB GRCLS
    ;SMC16:	LD	A,($ECBF)
    LD A,(MAGWK1FD0)
    LD A,0
    RES	4,A
    CALL	CLS0
    ;SMC17:	LD	A,($ECBF)
    LD A,(MAGWK1FD0)
    LD A,0
    SET	4,A
CLS0:
    LD	BC,$1FD0
    OUT	(C),A
    ;SMC18:	LD	($ECBF),A
    LD (MAGWK1FD0),A
    LD A,0
    XOR	A
    LD	C,A
    LD	HL,$4003
CLS1:
    LD	B,H
CLS2:
    DB	$ED,$71
    INC	B
    JR	NZ,CLS2
    ;
    ADD	A,L
    LD	C,A
    JR	NZ,CLS1
    RET
#ENDLIB

#LIB MAGLOAD
    ; HL = FILENAME, DE = X, BC = Y
    ; X,YからVRAM位置を算出する
    ; 200line専用
    ; LD	BC,X/8 + ((Y & 7) << 11) + ((Y/8)*80)
    PUSH HL
    PUSH DE
    PUSH BC
    LD A,C
    AND 7
    SLA A
    SLA A
    SLA A
    LD B,A
    LD C,0
    POP HL
    PUSH BC
    ; HL = HL / 8
    SRA H
    RR L
    SRA H
    RR L
    SRA H
    RR L
    LD DE,80
    CALL NAME_SPACE_DEFAULT.MULHLDE
    POP BC
    ADD HL,BC
    POP DE
    ; DE = DE / 8
    SRA D
    RR E
    SRA D
    RR E
    SRA D
    RR E
    ADD HL,DE
    LD (MAG_STARTPOS+1),HL
    POP HL

    ; 機種チェックはとりあえずしない
    ; LD	HL,(OSFLG)
    ; LD	A,H
    ; CP	$1D
    ; RET	NZ
    ; LD	A,L
    ; AND	$7F
    ; RET	NZ

    EX HL,DE

    PUSH DE
    LD HL,MAG_FP1
    ; DE = FILENAME
    LD BC,4
    CALL NAME_SPACE_DEFAULT.FOPEN
    OR	A
    JP	NZ,END
    POP DE

    LD	(MAG_SPBK),SP

MAG_LOAD:
    ; CALL	MAGLOAD_GRAPH1
MAGLOAD_WILD:
    LD	HL,FLAGBF
    LD	DE,FLAGBF+1
    LD	BC,16*512+256+4096-1
    LD	(HL),0
    LDIR

    LD	A,1
    LD	(FLAGAX+1),A
    LD	HL,0
    LD	(ZURI+1),HL

    LD	HL,LINEBF
    LD	(LINEX+1),HL

    LD	HL,$FFFF
    LD	DE,$FFFF
MAG_STARTPOS: LD	BC,0
    EXX

    LD HL,0
    LD (MAGOFS_F1),HL

    LD	HL,HEADBF
    LD	BC,8
    CALL	READ
    LD	HL,HEADBF
    LD	DE,MAGID
    LD	B,8
    CALL	CPSTR
    JP	NZ,NEXT

    LD	BC,$1800
    LD	A,12
    OUT	(C),A
    INC	C
    DB	$ED,$71
    DEC	C
    INC	A
    OUT	(C),A
    INC	C
    DB	$ED,$71
    LD	HL,8
COMMENT:
    INC	HL
    CALL	MAG_FGETC
    CP	$1A
    JR	Z,HEADER
    JR	COMMENT
HEADER:
    LD	(ADDHD+1),HL
    CALL	MAG_FGETC
    OR	A
    JP	NZ,NEXT
    LD	HL,HEADBF
    LD	(HL),A
    INC	HL
    LD	BC,31+48	;ヘッダ パレット
    CALL	READ
    PUSH	HL

    LD	HL,PALBF
    LD	DE,DEGI8
    LD	B,48
    CALL	CPSTR
    LD	A,(HEADBF+3)	;スクリーンモード
    JR	NZ,NOTD
    OR	6
    LD	(HEADBF+3),A
NOTD:
    BIT	7,A		;256色
    JP	NZ,NEXT
    AND	1		;200ライン
    LD	(L200+1),A

    LD	HL,(HEADBF+16)
    LD	BC,(HEADBF+12)
    AND	A
    SBC	HL,BC
    LD	B,H
    LD	C,L
    POP	HL
    CALL	READ		;フラグA
    			;フラグB

    LD	A,(HEADBF+26)
    LD	HL,(HEADBF+24)
ADDHD:	LD	DE,0
    ADD	HL,DE
    ADC	A,0

    LD (MAGOFS_F2),HL

    LD	HL,(HEADBF+8)
    LD	BC,(HEADBF+4)
    AND	A
    SBC	HL,BC
    LD	BC,8
    ADD	HL,BC
    ADD	HL,HL
    ADD	HL,HL
    ADD	HL,HL
    ADD	HL,HL
    ADD	HL,HL
    LD	A,H
    LD	(XSIZE+1),A

    LD	HL,(HEADBF+10)
    LD	BC,(HEADBF+6)
    AND	A
    SBC	HL,BC
    INC	HL
    LD	(YSIZE+1),HL

RGBSW:	LD	B,$00
    LD	A,(HEADBF+3)
    OR	B
    LD	(HEADBF+3),A

    LD	HL,KUWANO
    LD	DE,KUWANO

;SMC01: LD	A,($ECBF)
    LD A,(MAGWK1FD0)
    RRCA
    JR	NC,LOW
            ;High Reso
    LD	A,(HEADBF+3)	;スクリーンモード
    LD	B,0
    AND	6
    CP	6
    JR	NZ,HIGH3
    			;デジタル 8 色
    LD	A,(HEADBF+3)	;スクリーンモード
    LD	DE,RGB

    ADD	A,A
    AND	2
    JR	Z,HIGH2

    LD	DE,NUL
HIGH2:
    LD	HL,RGB
HIGH3:
    LD	B,A
;SMC02:	LD	A,($ECBF)
    LD A,(MAGWK1FD0)
    LD A,0
    AND	$FD
    OR	B
;SMC03:	LD	($ECBF),A
    LD (MAGWK1FD0),A
    LD	BC,$1FD0
    OUT	(C),A

    JR	FA

LOW:
    LD	HL,KUWANO2
    LD	DE,NUL

    LD	A,(HEADBF+3)	;スクリーンモード
    AND	7
    CP	7
    JR	NZ,FA

    LD	HL,RGB
FA:
    LD	(MODE1+1),HL
    LD	(MODE2+1),DE
    ;
    			;パレット 8bit->4bit
    LD	B,48
    LD	HL,PALBF
PALI:
    LD	A,(HL)
    RRCA
    RRCA
    RRCA
    RRCA
    AND	$0F
    SUB	4
    JR	NC,PLUS
    XOR	A
PLUS:
    LD	(HL),A
    INC	HL
    DJNZ	PALI

    LD	IX,(HEADBF+12)	;フラグA
    LD	DE,HEADBF
    ADD	IX,DE
    LD	IY,(HEADBF+16)	;フラグB
    ADD	IY,DE

    LD	B,25
LOOP1:
    PUSH	BC

    ; LD	E,$FF
    ; LD	C,6
    ; CALL	SYSTEM
    ; CP	$1B
    ; JP	Z,NEXT
    ; CP	3
    ; JP	Z,END
    ; CP	$13
    ; CALL	Z,WAIT1

ZURI:	LD	HL,0
    LD	A,H
    OR	L
    JR	Z,ZURI1

    LD	BC,$1800
    LD	A,12
    OUT	(C),A
    INC	C
    OUT	(C),H
    DEC	C
    INC	A
    OUT	(C),A
    INC	C
    OUT	(C),L
    DEC	C
    LD	A,6
    OUT	(C),A
    INC	C
    LD	A,24
    OUT	(C),A
    ; LD	BC,MAG_WIDTH
    LD A,(NAME_SPACE_DEFAULT.AT_WIDTH)
    LD C,A
    LD B,0
    ADD	HL,BC
    LD	(ZURI+1),HL
ZURI1:
    LD	B,8
LOOP2:
    PUSH	BC
    EXX
    PUSH	BC
    PUSH	BC
    EXX

;SMC04:	LD	A,($ECBF)
    LD A,(MAGWK1FD0)
    LD A,0
    AND	$EF
;SMC05:	LD	($ECBF),A
    LD (MAGWK1FD0),A
    LD	BC,$1FD0
    OUT	(C),A

    CALL	MAG
    LD	HL,(LINEX+1)
    PUSH	HL
L200:	LD	A,$00
    OR	A
    CALL	Z,MAG
    LD	HL,(LINEX+1)
    EX	(SP),HL
MODE1:	CALL	KUWANO

;SMC06:	LD	A,($ECBF)
    LD A,(MAGWK1FD0)
    LD A,0
    OR	$10
;SMC07:	LD	($ECBF),A
    LD (MAGWK1FD0),A
    LD	BC,$1FD0
    OUT	(C),A

    POP	HL
    EXX
    POP	BC
    EXX
MODE2:	CALL	KUWANO

    EXX
    POP	BC
    LD	A,B
    ADD	A,8
    LD	B,A
    EXX

    POP	BC
    DEC	B
    JP	NZ,LOOP2

    LD	HL,(YSIZE+1)
    LD	A,H
    OR	L
    JR	Z,WAIT

    EXX
    PUSH	HL
    ; LD	HL,MAG_WIDTH
    LD A,(NAME_SPACE_DEFAULT.AT_WIDTH)
    LD L,A
    LD H,0
    ADD	HL,BC
    LD	A,H
    AND	7
    LD	B,A
    LD	C,L
    POP	HL
    EXX
    POP	BC
    DEC	B
    JP	NZ,LOOP1

    CALL	WAIT1
    LD	HL,(ZURI+1)
    ; LD	BC,MAG_WIDTH
    LD A,(NAME_SPACE_DEFAULT.AT_WIDTH)
    LD C,A
    LD B,0
    ADD	HL,BC
    LD	(ZURI+1),HL

    LD	BC,$2000
    LD	HL,$800
CLRAT:
    DB	$ED,$71
    INC	BC
    DEC	HL
    LD	A,H
    OR	L
    JR	NZ,CLRAT

    LD	B,25
    JP	LOOP1

WAIT:
    DB	$3E
NEXT:
    XOR	A
    OR	A
    EX	AF,AF'

END:
    LD HL,MAG_FP1
    CALL NAME_SPACE_DEFAULT.FCLOSE

    ; 突如ENDに飛んでくる事があるので強引にSPを戻して戻ってやる
    LD	HL,(MAG_SPBK)
    LD SP,HL

    LD L,0
    LD H,L
    RET

WAIT1:
    LD	BC,$1800
    LD	A,6
    OUT	(C),A
    INC	C
    LD	A,25
    OUT	(C),A

    RET


RGB:
    ; LD	C,MAG_WIDTH
    ; LD A,(AT_WIDTH)
    LD A,(XSIZE+1)
    LD C,A
DIS1:
    LD	DE,$4004
    CALL	DISS		;G
    EXX
    SET	7,B
    SET	6,B
    OUT	(C),A
    EXX
    LD	DE,$2002
    CALL	DISS		;R
    EXX
    RES	6,B
    OUT	(C),A
    EXX
    LD	DE,$1001
    CALL	DISS		;B
    EXX
    RES	7,B
    SET	6,B
    OUT	(C),A
    LD	A,B
    INC	BC
    XOR	B
    AND	8
    JR	Z,INCXX
    LD	A,B
    SUB	8
    LD	B,A
INCXX:
    EXX
    INC	HL
    INC	HL
    INC	HL
    INC	HL
    DEC	C
    JR	NZ,DIS1

DISS:
    PUSH	BC
    PUSH	HL
    LD	BC,$0400
DISS1:
    LD	A,(HL)
    AND	D
    JR	Z,DISS2
    SCF
DISS2:
    RL	C
    LD	A,(HL)
    AND	E
    JR	Z,DISS3
    SCF
DISS3:
    RL	C

    INC	HL
    DJNZ	DISS1

    LD	A,C
    POP	HL
    POP	BC
    RET

KUWANO:
    PUSH	IX
    PUSH	IY

KLINE1:	LD	IX,KWABF1+4
KLINE2:	LD	IY,KWABF2+4

    LD	B,80
KUWA1:
    PUSH	BC

    LD	B,4
KUWA2:
    PUSH	BC

    LD	A,(HL)
    INC	HL
    PUSH	HL

    PUSH	AF
    RRCA
    RRCA
    RRCA
    RRCA
    CALL	PALET1
    POP	AF
    CALL	PALET1

    POP	HL
    POP	BC
    DJNZ	KUWA2

    EXX
    SET	7,B
    SET	6,B
    LD	A,(MAGLOAD_GREEN)
    OUT	(C),A
    RES	6,B
    LD	A,(MAGLOAD_RED)
    OUT	(C),A
    RES	7,B
    SET	6,B
    LD	A,(MAGLOAD_BLUE)
    OUT	(C),A
    LD	A,B
    INC	BC
    XOR	B
    AND	8
    JR	Z,INCX
    LD	A,B
    SUB	8
    LD	B,A
INCX:
    EXX
    POP	BC
    DJNZ	KUWA1
KUWAE:
    LD	HL,(KLINE1+2)
    LD	IX,(KLINE2+2)
    LD	(KLINE2+2),HL
    LD	(KLINE1+2),IX

    POP	IY
    POP	IX
    RET

PALET1:
    ;C=GREEN D=RED E=BLUE
    AND	$0F
    LD	E,A
    ADD	A,A
    ADD	A,E
    LD	E,A
    LD	D,0
    LD	HL,PALBF
    ADD	HL,DE
    LD	A,(HL)
    INC	HL
    LD	D,(HL)
    INC	HL
    LD	E,(HL)
PALETX:
    LD	HL,MAGLOAD_GREEN
    CALL	KUWANO1
    LD	A,D
    LD	HL,MAGLOAD_RED		;RED
    CALL	KUWANO1
    LD	A,E
    LD	HL,MAGLOAD_BLUE		;BLUE
KUWANO1:
    ADD	A,(IX+0)		;桑野
    LD	(IX+0),0
    ADD	A,-THOLD
    JR	C,PSET
    SUB	-THOLD
PSET:
    RL	(HL)

    LD	B,A
    SRL	B
    SUB	B
    LD	C,B		;C=A/2
    SRL	B
    SUB	B
    PUSH	BC		;B=A/4
    SRL	B
    SUB	B
    PUSH	BC		;B=A/8

    ADD	A,(IY+0)		;Y+1
    LD	(IY+0),A

    POP	AF		;A=A/8
    ADD	A,(IY+3*1)	;Y+1 X+1
    LD	(IY+3*1),A

    POP	AF		;A=A/4
    ADD	A,(IY-3*1)	;Y+1 X-1
    LD	(IY-3*1),A

    LD	A,C		;A=A/2
    ADD	A,(IX+3*1)	;X+1
    LD	(IX+3*1),A

    INC	IX
    INC	IY
NUL:
    RET

KUWANO2:
    POP	BC
    POP	DE
    PUSH	DE
    PUSH	BC

    PUSH	IX
    PUSH	IY

    LD	IX,(KLINE1+2)
    LD	IY,(KLINE2+2)

    LD	B,80
LKUWA1:
    PUSH	BC

    LD	B,4
LKUWA2:
    PUSH	BC

    LD	A,(DE)
    INC	DE
    LD	B,(HL)
    INC	HL
    PUSH	DE
    PUSH	HL

    PUSH	BC
    PUSH	AF
    LD	A,B
    RRCA
    RRCA
    RRCA
    RRCA
    LD	B,A
    POP	AF
    PUSH	AF
    RRCA
    RRCA
    RRCA
    RRCA
    CALL	PALET2
    POP	AF
    POP	BC
    CALL	PALET2

    POP	HL
    POP	DE
    POP	BC
    DJNZ	LKUWA2

    EXX
    SET	7,B
    SET	6,B
    LD	A,(MAGLOAD_GREEN)
    OUT	(C),A
    RES	6,B
    LD	A,(MAGLOAD_RED)
    OUT	(C),A
    RES	7,B
    SET	6,B
    LD	A,(MAGLOAD_BLUE)
    OUT	(C),A
    LD	A,B
    INC	BC
    XOR	B
    AND	8
    JR	Z,LINCX
    LD	A,B
    SUB	8
    LD	B,A
LINCX:
    EXX
    POP	BC
    DJNZ	LKUWA1
    JP	KUWAE

PALET2:
    ;C=GREEN D=RED E=BLUE
    AND	$0F
    LD	E,A
    ADD	A,A
    ADD	A,E
    LD	E,A
    LD	D,0
    LD	HL,PALBF
    ADD	HL,DE
    LD	C,(HL)
    INC	HL
    LD	D,(HL)
    INC	HL
    LD	E,(HL)

    LD	A,B
    AND	$0F
    PUSH	DE
    LD	E,A
    ADD	A,A
    ADD	A,E
    LD	E,A
    LD	D,0
    LD	HL,PALBF
    ADD	HL,DE
    POP	DE

    LD	A,(HL)
    INC	HL
    ADD	A,C
    RRA
    LD	C,A

    LD	A,(HL)
    INC	HL
    ADD	A,D
    RRA
    LD	D,A

    LD	A,(HL)
    ADD	A,E
    RRA
    LD	E,A

    LD	A,C
    JP	PALETX

    ;				フラグ&ピクセル 展開
MAG:
    LD	A,(LINEX+2)
    SUB	LINEBF/256-2
    AND	$1F
    ADD	A,LINEBF/256
    LD	(LINEX+2),A

LINEX:	LD	DE,LINEBF
XSIZE:	LD	B,80
YSIZE:	LD	HL,0

    LD	A,H
    OR	L
    JR	Z,CLEAR
    DEC	HL
    LD	(YSIZE+1),HL

    LD	HL,FLAGBF
FLAGA1:
    RLC	(IX+0)
    JR	C,FLAGA3
    XOR	A
    JR	FLAGA4
FLAGA3:
    CALL	MAG_FGETC
FLAGA4:
    XOR	(HL)
    LD	(HL),A
    CP	$11
    JR	NZ,FLAGA5
    PUSH	BC
    PUSH	HL
    LD	H,D
    LD	L,E
    DEC	HL
    DEC	HL
    LDI
    LDI
    LDI
    LDI
    POP	HL
    POP	BC
    JR	FLAGA6
FLAGA5:
    RRCA
    RRCA
    RRCA
    CALL	PIC
    LD	A,(HL)
    ADD	A,A
    CALL	PIC
FLAGA6:
    INC	HL
FLAGAX:	LD	A,$01
    RLCA
    LD	(FLAGAX+1),A
    JR	NC,FLAGA2
    INC	IX
FLAGA2:
    DJNZ	FLAGA1
    RET

CLEAR:
    LD	C,4
CLEAR1:
    LD	(DE),A
    INC	DE
    DJNZ	CLEAR1
    DEC	C
    JR	NZ,CLEAR1
    RET

PIC:
    AND	$1E
    JR	NZ,PICT

    CALL	FGETP
    LD	(DE),A
    INC	DE
    CALL	FGETP
    LD	(DE),A
    INC	DE
    RET
PICT:
    PUSH	BC
    PUSH	HL
    LD	C,A
    LD	B,0
    LD	HL,MAGLOAD_TABLE
    ADD	HL,BC

    LD	A,E
    SUB	(HL)		;1
    INC	HL
    LD	H,(HL)
    LD	L,A
    LD	A,D
    SBC	A,LINEBF/256	;2
    SUB	H
    AND	$1F
    ADD	A,LINEBF/256
    LD	H,A

    LDI
    LDI
    POP	HL
    POP	BC
    RET

READ:
    PUSH	HL
    ADD	HL,BC
    JP	C,END

    ; メモリオーバーチェックはしない
    ; LD	DE,(SYSTEM+1)
    ; DEC	D
    ; SBC	HL,DE
    ; JP	NC,NEXT

    POP	HL
READ1:
    LD	A,B
    OR	C
    RET	Z
    CALL	MAG_FGETC
    LD	(HL),A
    INC	HL
    DEC	BC
    JR	READ1

MAG_FGETC:
    EXX
    LD	A,H
    CP	DTBUFE/$100
    JR	NC,MAG_FGETC2
MAG_FGETC1:
    LD	A,(HL)
    INC	HL
    EXX
    RET
MAG_FGETC2:
    PUSH	BC
    PUSH	DE

    PUSH IX
    PUSH IY

    LD HL,(MAGOFS_F1)
    EX DE,HL
    LD HL,MAG_FP1
    LD BC,0  ; HEAD
    CALL NAME_SPACE_DEFAULT.FSEEK

    LD HL,MAG_FP1
    LD DE,DTBUF
    LD BC,BUFSIZ
    CALL NAME_SPACE_DEFAULT.FREAD

    POP IY
    POP IX

    ; OUT OF FILE
    CP $11
    JP Z,FGETCOK

    OR A
    JP NZ,NEXT

FGETCOK:

    LD HL,(MAGOFS_F1)
    LD DE,BUFSIZ
    ADD HL,DE
    LD (MAGOFS_F1),HL

    LD	HL,DTBUF
    POP	DE
    POP	BC
    JR	MAG_FGETC1

FGETP:
    EXX
    LD	A,D
    CP	PDBUFE/$100
    JR	NC,FGETP2
FGETP1:
    LD	A,(DE)
    INC	DE
    EXX
    RET
FGETP2:
    PUSH	BC
    PUSH	HL

    PUSH IX
    PUSH IY

    LD HL,(MAGOFS_F2)
    EX DE,HL
    LD HL,MAG_FP1
    LD BC,0  ; HEAD
    CALL NAME_SPACE_DEFAULT.FSEEK

    LD HL,MAG_FP1
    LD DE,PDBUF
    LD BC,PDSIZE
    CALL NAME_SPACE_DEFAULT.FREAD

    POP IY
    POP IX

    ; OUT OF FILE
    CP $11
    JP Z,FGETPOK

    OR A
    JP NZ,NEXT

FGETPOK:

    LD HL,(MAGOFS_F2)
    LD DE,PDSIZE
    ADD HL,DE
    LD (MAGOFS_F2),HL

    LD	DE,PDBUF
    POP	HL
    POP	BC
    JR	FGETP1

CPSTR:
    LD	A,(DE)
    INC	DE
    CP	(HL)
    INC	HL
    RET	NZ
    DJNZ	CPSTR
    RET

MAGLOAD_TABLE:
    DB	0  ,0		; 0
    DB	1*2,0		; 1
    DB	2*2,0		; 2
    DB	4*2,0		; 3
    DB	0  ,1*2		; 4
    DB	1*2,1*2		; 5
    DB	0  ,2*2		; 6
    DB	1*2,2*2		; 7
    DB	2*2,2*2		; 8
    DB	0  ,4*2		; 9
    DB	1*2,4*2		;10
    DB	2*2,4*2		;11
    DB	0  ,8*2		;12
    DB	1*2,8*2		;13
    DB	2*2,8*2		;14
    DB	0  ,0		;15

MAGWK1FD0:
    DB $00

MAGOFS_F1:
        DW 0
MAGOFS_F2:
        DW 0

MAGLOAD_GREEN:	DB	0
MAGLOAD_RED:	DB	0
MAGLOAD_BLUE:	DB	0

MAGID:
    DB	"MAKI02  "
    DB	" X1  "
DEGI8:
    DB	$00,$00,$00
    DB	$00,$00,$FF
    DB	$00,$FF,$00
    DB	$00,$FF,$FF
    DB	$FF,$00,$00
    DB	$FF,$00,$FF
    DB	$FF,$FF,$00
    DB	$FF,$FF,$FF
    DB	$00,$00,$00
    DB	$00,$00,$FF
    DB	$00,$FF,$00
    DB	$00,$FF,$FF
    DB	$FF,$00,$00
    DB	$FF,$00,$FF
    DB	$FF,$FF,$00
    DB	$FF,$FF,$FF
MAG_SPBK:
    DW 0
#ENDLIB
