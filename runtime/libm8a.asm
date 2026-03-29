; Converted from lib/libdef/libm8a.yml
; SLANG Runtime Library (new format)

; @name M8ALOAD
; @lib M8ALIB

WIDTH		EQU	40

DRAWSPEED	EQU	0	;0=�ȃT�C�Y�ᑬ/1=����/1�ȏ�=���[�v�W�J����
CALCSPEED	EQU	0	;0=�v�Z�擾�ᑬ/0�ȊO=TABLE�擾����

DRAWM8A:
; input: HL=�f�[�^�J�n�ʒu
;        DE=�������`��J�n�ʒu(X)
;        BC=�c�����`��J�n�ʒu(Y)
	LD D,E
	LD E,C

#IF CALCSPEED == 0
	; ���݂�WIDTH�l������(CALCSPEED��0�̎��̂ݓ��I��WIDTH�؂�ւ����\�ƂȂ�)
	LD A,(NAME_SPACE_DEFAULT.AT_WIDTH)
	LD (REWRITE0Y+1),A
#ENDIF

;M8A�摜��`�悷��
; input: HL=�f�[�^�J�n�ʒu
;        D=�������`��J�n�ʒu(X)
;        E=�c�����`��J�n�ʒu(Y)
;
; �j��: AF,BC,DE,HL,BC',DE',HL',IX
;
; (data header)
;	DB "M8A"	;magic 'M8A'
;	DB $00		;�\��
;	DB (��-1)\8
;	DB ����
;
; (data format)
;	DB nnGRBGRB
;	   | |  |
;	   | |  dot1
;	   | dot2
;	   nn=0 : next byte is repeat length(1�`256)
;	   nn!=0: repeat length (1�`3)
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
	LD	A, E		; ���炵���c
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

	INC	HL		;�\��

	LD	A, (HL)		; ���i�������J��Ԃ����j
	INC	HL
	LD	(REWRITE0X+1), A

	LD	A, (HL)		; �����i�c�����J��Ԃ����j
	INC	HL
	LD	(REWRITE0Z+1), A


;------------------------------------------------------------------------------
	CALL	SET_GDAT		; �F�R�[�h���J��Ԃ��񐔂��擾
	LD	B, 0			; �c��Y��
;------------------------------------------------------------------------------
DRAWM8A00:
REWRITE00:
	LD	A, 0			; A=�c�����Y������
	ADD	A, B			; A=�c���W

REWRITE0X:
	LD	C, $00			; C=����X��
	EXX				; ����

	; ���[���W�v�Z
#IF CALCSPEED == 0
		LD	C, A		; ���[���W�̌v�Z...(Y AND 7)*2^11 + (Y \ 8)*WIDTH
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
AXBHL:					; A�~B = HL
		LD	HL, 0		; ���ʂ��N���A
		LD	D, H		; D=0
		LD	E, B		; DE=B
		LD	B, 8		; 8bit�Ԃ�J��Ԃ�(counter)
AXBHL00:
		RRCA			; �ŉ���bit��Cy�ɓ���
		JR	NC, AXBHL01
		ADD	HL, DE		; Cy=1�Ȃ�DE��������
AXBHL01:
		SLA	E
		RL	D		; DE���V�t�g
		DJNZ	AXBHL00

REWRITEA:
		LD	A, 0		; ���ȏ�������
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
		LD	A, 0		; A=�������Y������
		ADD	A, C
		LD	C, A
		JR	NC, HOGE
		INC	B
HOGE:
	EXX				; �\��
;------------------------------------------------------------------------------
DRAWM8A01:
#IF DRAWSPEED == 0
	; �`�揈���i�ᑬ�j
	EXX				; ����
		LD	L, 4		; 4��J��Ԃ�
BITSET:
		LD	A, IXH		; A=�F�R�[�h
		CALL	SETBITSUB
		CALL	SETBITSUB
		DEC	IXL		; rep counter--
		CALL	Z, SET_GDAT2	; �J�E���^��0�ɂȂ�����ēx�f�[�^���擾
		DEC	L
		JR	NZ, BITSET
#ENDIF
#IF DRAWSPEED == 1
	; �`�揈���i�����j
	EXX				; ����
		LD	L, 4		; 4��J��Ԃ�
BITSET:
		LD	A, IXH		; A=�F�R�[�h
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
		CALL	Z, SET_GDAT2	; �J�E���^��0�ɂȂ�����ēx�f�[�^���擾
		DEC	L
		JR	NZ, BITSET
#ENDIF
#IF DRAWSPEED > 1
	; �`�揈���i�����j
	EXX				; ����
		LD	A, IXH		; A=�F�R�[�h
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
		CALL	Z, SET_GDAT2	; �J�E���^��0�ɂȂ�����ēx�f�[�^���擾
		LD	A, IXH		; A=�F�R�[�h
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
		CALL	Z, SET_GDAT2	; �J�E���^��0�ɂȂ�����ēx�f�[�^���擾
		LD	A, IXH		; A=�F�R�[�h
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
		CALL	Z, SET_GDAT2	; �J�E���^��0�ɂȂ�����ēx�f�[�^���擾
		LD	A, IXH		; A=�F�R�[�h
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
		CALL	Z, SET_GDAT2	; �J�E���^��0�ɂȂ�����ēx�f�[�^���擾
#ENDIF

; GVRAM�ɓ]��
TRANS2GVRAM:
		LD	L, B		; �ꎞ�ۑ�
		OUT	(C), D		; BLUE out
		LD	A, $40		; B->R��
		ADD	A, B
		LD	B, A
		OUT	(C), E		; RED out
		SET	6, B		; R->G��
		OUT	(C), H		; GREEN out
		LD	B, L		; BLUE�ɖ߂�
		INC	BC		; �\���ʒu����������++
	EXX				; �\��
	DEC	C			; �������J�E���^--
	JR	NZ, DRAWM8A01
	INC	B			; �c�����J�E���^++
REWRITE0Z:
	LD	A, 200			; �c��Y��
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
; ���k�f�[�^����ݒ���擾(��)
SET_GDAT2:
	EXX			;�\��
	CALL	SET_GDAT
	EXX			;����
	RET
;------------------------------------------------------------------------------
; ���k�f�[�^����ݒ���擾
;in:  HL =read address
;out: IXH=color code
;     IXL=rep counter
SET_GDAT:
	LD	A, (HL)			; �F�R�[�h���擾
	INC	HL
	LD	IXH, A			; �F�R�[�h��ݒ�
	AND	11000000B
	JR	Z, SET_GDAT02		; ���2bit��00�̏ꍇ�́A���̃o�C�g���J��Ԃ���-1

	LD	A, IXH			; ���2bit��00�łȂ��ꍇ�͐F�R�[�h��߂��i�J��Ԃ������擾�j
	RLCA
	RLCA
	AND	00000011B		; A=�J��Ԃ���
	LD	IXL, A			; �J��Ԃ��񐔂�ݒ�
	RET

SET_GDAT02:				; ���2bit��00�̏ꍇ
	LD	A, (HL)			; �J��Ԃ��񐔂��擾
	INC	HL
	INC	A			; +1����
	LD	IXL, A			; �J��Ԃ��񐔂�ݒ�
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



