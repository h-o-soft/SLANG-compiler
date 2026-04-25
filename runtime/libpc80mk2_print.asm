; Converted from lib/libdef/libpc80mk2_print.yml
; SLANG Runtime Library (new format)

; @name WIDTH
; @resident shared
; HL=80 or 40
ld  b,l
ld  c,25
jp	BIOS.WIDTH


; @name WIDTH2
; @resident shared
; HL=80 or 40
; DE=25 or 20
ld  b,l
ld  c,e
jp	BIOS.WIDTH


; @name TEXTMODE
; @resident shared
; L = 0でファンクションキー非表示、1で表示
; E = 0で白黒、1でカラー
XOR A
INC L
DEC L
JR Z,.nofunc
LD A,$FF
.nofunc
LD B,A

XOR A
INC E
DEC E
JR Z,.mono
LD A,$FF
.mono
LD C,A

jp	BIOS.FUNC_COLOR 	; ファンクションキーを消してカラーモードにする


; @name LOCATE
; @resident shared
; HL=x DE=y
LD H,L
LD L,E

LD A,H
BIT 7,A
JR Z,.nowidthminus
LD H,0
.nowidthminus

LD A,L
BIT 7,A
JR Z,.noheightminus
LD L,0
.noheightminus

INC H
INC L

LD A,H
CP 81
JP C,.widthok
LD H,80
.widthok

LD A,L
CP 26
JP C,.heightok
LD L,25
.heightok

jp BIOS.LOCATE


; @name PRT
; @resident shared
; @calls PC80CALLS
; @works WORK10:10
CALL BIOS.PUTCRT1
RET


; @name PTAB
; @resident shared
; @param_count 1
; @calls PCR1
LD E,$09
JR PCR1


; @name PSPC
; @resident shared
; @param_count 1
; @calls PCR1
LD E,' '
JR PCR1


; @name PCRONE
; @resident shared
; @param_count 0
; @calls PCR
LD HL,1


; @name PCR
; @resident shared
; @param_count 1
; @calls PSTR2
EX DE,HL
LD HL,$0D0A
JR PSTR2


; @name PCR1
; @resident shared
; @param_count 1
; @calls PSTR
EX DE,HL


; @name PSTR
; @resident shared
; @param_count 2
; @calls PRT
.pstr1
LD A,D
OR E
RET Z
LD A,L
CALL PRT
DEC DE
JR .pstr1


; @name PSTR2
; @resident shared
; @param_count 2
; @calls PCHR
.pstr1
LD A,D
OR E
RET Z
CALL PCHR
DEC DE
JR .pstr1


; @name PCHR
; @resident shared
; @calls PRT
LD A, H
OR A
CALL NZ,PRT
LD A, L
OR A
JR NZ,PRT


; @name CRDISP
; @resident shared
; @calls PRT
LD A,$0D
JR PRT


; @name PHEX4
; @resident shared
; @param_count 1
; @calls PHEX2
LD A,H
CALL PHEX


; @name PHEX2
; @resident shared
; @param_count 1
; @calls PHEX
LD A,L


; @name PHEX
; @resident shared
; @param_count 1
; @calls SASC,PRT
PUSH AF
RRCA
RRCA
RRCA
RRCA
CALL SASC
CALL PRT

POP AF
CALL SASC


; @name CTRL0D
; @resident shared
; @calls CSR,AT_VRCALC
CALL _POS
LD L,0
INC H

PUSH	BC
CALL AT_VRCALC
LD (_TXADR),HL
POP	BC

RET


; @name PSIGN
; @resident shared
; @param_count 1
; @calls PRT,NEGHL,P10
BIT 7, H
JR Z,.psign1
LD A, $2D
CALL PRT
CALL NEGHL
.psign1


; @name P10
; @resident shared
; @param_count 1
; @function_type Machine
; @calls P10to5,P10toN
LD DE, -1
JR P10toN


; @name P10to5
; @resident shared
; @param_count 1
; @function_type Machine
; @calls P10toN
LD DE, 0005


; @name P10toN
; @resident shared
; @param_count 2
; @function_type Machine
; @calls PRT,VTOS,PMSX
PUSH DE
LD DE, WORK10
CALL VTOS
EX DE, HL
POP DE
LD A, E
CP $05
JR NC, .p10ton1
LD A, $05
SUB E
.p10ton2
INC HL
DEC A
JR NZ, .p10ton2
JR PMSX
.p10ton1
LD A, E
CP $FF
JR NZ, PMSX
.p10ton4
LD A, (HL)
CP $20
JR NZ, PMSX
INC HL
JR .p10ton4


; @name PMSX
; @resident shared
; @calls PMSX1
LD B, 00


; @name PMSX1
; @resident shared
; @calls PRT,PMSG
LD A, (HL)
CP B
RET Z
CALL PRT
INC HL
JR PMSX1


; @name PMSG
; @resident shared
; @calls PMSX1
LD B, $0D
JR PMSX1


; @name MPRNT
; @resident shared
; @calls PRT
EX (SP),HL
.mprnt2
LD A, (HL)
INC HL
OR A
JR Z, .mprnt1
CALL PRT
JR .mprnt2
.mprnt1
EX (SP),HL
RET


; @name VTOS
; @resident shared
; @param_count 2
; @function_type Machine
; @calls DIVHLDE8
PUSH HL
EXX
POP HL
EXX
LD HL, $0005
ADD HL, DE
LD (HL), $00
LD B, $05
.vtos1
EXX
LD E, $0A
CALL DIVHLDE8
LD A, E
ADD A, $30
EXX
DEC HL
LD (HL), A
DJNZ .vtos1
LD B, $04
.vtos3
LD A, (HL)
CP $30
JR NZ, .vtos2
LD (HL), $20
INC HL
DJNZ .vtos3
.vtos2
RET


; @name SETATR
; @resident shared
; @lib PC80ASM
; HL = �s
; DE = �J�n�ʒuX
; BC = �V�K�K�p����A�g���r���[�g

	; �V�K�K�p����A�g���r���[�g
	ld a,c

	; �J�n�ʒuX
	ld c,e

	; �J�n�s
	ld b,l

;--------------------------------------------------------------------------------------------------
;tab4 sjis
;b=�sy(0-24) c=�J�n�ʒux(0-79) a=�V�K�K�p����A�g���r���[�g de,hl �g�p
; �A�g���r���[�g�̓K�p�����l���Ȃ��ėǂ��w���p�[���[�`��
; �J�n�ʒu�� $80 �ɂ��ČĂяo���ƊY���s��S�ăN���A($80,$E8)����B
;
; �����J�n�ʒu���̂�����ꍇ �� �㏑������BPC80/88 �A�g���r���[�g�̎d�l�ɂ��A����J�n�ʒu�ł̃A�g���r���[�g�͕s��
; ����������20�𒴂����ꍇ �� �Ō���̃A�g���r���[�g���ǂ��o�����B
; ���̃��[�`�����g�킸�ɒ��ڃA�g���r���[�g��M�镹�p�͍l�����Ă��Ȃ��̂ŕK�����̃��[�`�����ĂԂ��ƁB
; �ŏ��̃A�g���r���[�g�J�n�ʒu�� 0 �� $80 �ŏ���������Ă���A�Ȍ�̃A�g���r���[�g���\�[�g����Ă���O��Ŏg�p����B
; �ŏ��̃A�g���r���[�g�͈Öق� x=0 �ɂȂ��Ă��܂��̂ŁA�����I�� x=0 �ȊO�ł͎g�p���Ȃ��B
;
; �e�L�X�gVRAM ���J���[���[�h�ɂȂ��Ă��邱�ƁB�łȂ��ƐF�ݒ�Ȃǂ��ł��Ȃ��B

TVRAM			equ		$F300 ; $F3C8
;ATRC �� ATRD �͓����ɂ͎g���Ȃ�
;�Ⴄ�O���[�v�͓����Ɏw��ł���(�u�����N�{�A���_�[���C���Ȃǁj
ATRD_DECOLAT	equ		%00000000
ATRC_COLOR		equ		%00001000

ATRC_BLACK		equ		%00001000
ATRC_BLUE		equ		%00101000
ATRC_RED		equ		%01001000
ATRC_PURPLE		equ		%01101000
ATRC_GREEN		equ		%10001000
ATRC_CYAN		equ		%10101000
ATRC_YELLOW		equ		%11001000
ATRC_WHITE		equ		%11101000

ATRC_SEMIG		equ		%00011000
ATRC_CHR		equ		%00001000

ATRD_DLINE		equ		%00100000
ATRD_ULINE		equ		%00100000

ATRD_REVSECa	equ		%00000111				;101�Ɠ����B�w�肵�����������ŉB���
ATRD_REVBLK		equ		%00000110
ATRD_REVSEC		equ		%00000101
ATRD_REV		equ		%00000100
ATRD_SECa		equ		%00000011				;001�Ɠ����B�w�肵�����������ŉB���
ATRD_BLK		equ		%00000010
ATRD_SEC		equ		%00000001
ATRD_NOR		equ		%00000000

;sample
;	ld			a,ATRC_RED
;	ld			bc,(0 << 8) | 10				;x=10 y=0 ���� �����F��Ԃɂ���
;	call		SetTextAtr

SetTextAtr:
	ld			h,a
	; in			a,($32)
	; push		af
	; res			4,a
	; out			($32),a

	push		hl							;push af �̑���B�A�g���r���[�g���ꎞ�ۑ�

	call		.sub						;b=y c=x �� hl=tatr �ɂ���
	bit			7,c
	jr			nz,.clear					;�J�n�ʒu=$80�Ȃ炻�̍s�͏���������

	ld			a,c							;x
	ld			b,20
.loop
	cp			(hl)						;�����J�n�ʒu�̂��̂��������獷���ւ���
	jr			z,.found					;�擪���猟������̂ŁA�ŏ��̃A�g���r���[�g�� x=0 �łȂ��ꍇ�ł���v����Ώ㏑�����Ă��܂��B
	inc			hl							;���̏ꍇ�A�V���ɐݒ肵���A�g���r���[�g�̊J�n�ʒu�͋����I�� x=0 �Ɖ��߂���Ă��܂��B
	inc			hl							;�Ȃ̂ŁA����ɏ���������Ă��邱�Ƃ��O��ƂȂ�B
	djnz		.loop

	ld			b,20
.sort
	dec			hl
	dec			hl
	ld			a,(hl)
	or			a
	jr			z,.next						;0 �܂��� 80 �ȏ�͋󔒂ƌ��Ȃ��Ĕ�΂�
	cp			80
	jr			nc,.next
	ld			a,c
	cp			(hl)
	jr			c,.next						;�������J�n�ʒu�̑傫�����͔̂�΂��ď��������̂�T��

	ld			a,b
	cp			20							;�����Ȃ菉��Ō�����������ɉ����o�����ɍŌ����������������
	jr			nz,.skip1
	dec			hl
	dec			hl
	dec			b

.skip1
	inc			hl							;�������J�n�ʒu������������A���̌��ɑ}������
	inc			hl
	pop			af
.sortlp
	ld			e,(hl)						;�Â��l�� de �ɕۑ�
	ld			(hl),c						;�V�����l ac ������
	inc			hl
	ld			d,(hl)
	ld			(hl),a
	inc			hl

	ld			a,b
	inc			b
	cp			19							;19���ŏI�ʒu
	ld			c,e
	ld			a,d
	jr			nz,.sortlp					;�V����������}���������ʁA�Ō���̑����͒ǂ��o�����
	jr			.exit

.next
	djnz		.sort						;�Ō�܂Ō������ċ󂾂炯 or ��ԍŏ��̊J�n�ʒu�� a ��肤���낾�����火

	pop			af
	inc			c							;x=0 �̏ꍇ�͂��̂܂ܐ擪�̃A�g���r���[�g�ɏ㏑���B�����o���̓i�V�B
	dec			c
	jr			z,.skip2
	inc			hl							;x!=0�̏ꍇ�A�擪�ɏ������ނƈÖق�x=0�ɂ���Ă��܂��̂ŁA����炵�ď������ށB
	inc			hl							;���̏ꍇ�A�Ō���̃A�g���r���[�g�͒ǂ��o����Ė����ƂȂ�
	ld			b,1
	jr			.sortlp
.skip2										;x=0 �̏ꍇ�͂��̂܂ܐ擪�̃A�g���r���[�g�ɏ㏑���B�����o���̓i�V�B
	ld			(hl),c
	inc			hl
	ld			(hl),a
	jr			.exit

.found										;�����J�n�ʒu�̂��̂���������A�����ɏ㏑������
	pop			af							;���̏ꍇ�A�擪�� 0 �ł� 80 �ȏ�ł��Ȃ������ȊJ�n�ʒu���������܂�Ă����͂��Ȃ̂ŏ㏑���Ŗ��Ȃ�
	inc			hl
	ld			(hl),a
	jr			.exit

.clear
	pop			af
	ld			b,20
.clearlp
	ld			(hl),$80					;�S�N���A�� BASIC �ɕ���� $80,$E8 �Ƃ���B
	inc			hl
	ld			(hl),$E8
	inc			hl
	djnz		.clearlp
.exit
	; pop			af
	; out			($32),a
	ret

.sub										;b=y c=x �� hl=tatr�@�ɂ���
	push		bc
	ld			a,b
	ld			h,b
	ld			l,0
	ld			b,l
	srl			h
	rr			l							;x128
	add			a,a
	add			a,a
	add			a,a
	ld			c,a							;x8
	sbc			hl,bc
	ld			bc,TVRAM+80
	add			hl,bc						;hl=TATR+120*y
	pop			bc
	ret


