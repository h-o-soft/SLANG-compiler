


#LIB SETATR
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
.loop:
	cp			(hl)						;�����J�n�ʒu�̂��̂��������獷���ւ���
	jr			z,.found					;�擪���猟������̂ŁA�ŏ��̃A�g���r���[�g�� x=0 �łȂ��ꍇ�ł���v����Ώ㏑�����Ă��܂��B
	inc			hl							;���̏ꍇ�A�V���ɐݒ肵���A�g���r���[�g�̊J�n�ʒu�͋����I�� x=0 �Ɖ��߂���Ă��܂��B
	inc			hl							;�Ȃ̂ŁA����ɏ���������Ă��邱�Ƃ��O��ƂȂ�B
	djnz		.loop

	ld			b,20
.sort:
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

.skip1:
	inc			hl							;�������J�n�ʒu������������A���̌��ɑ}������
	inc			hl
	pop			af
.sortlp:
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

.next:
	djnz		.sort						;�Ō�܂Ō������ċ󂾂炯 or ��ԍŏ��̊J�n�ʒu�� a ��肤���낾�����火

	pop			af
	inc			c							;x=0 �̏ꍇ�͂��̂܂ܐ擪�̃A�g���r���[�g�ɏ㏑���B�����o���̓i�V�B
	dec			c
	jr			z,.skip2
	inc			hl							;x!=0�̏ꍇ�A�擪�ɏ������ނƈÖق�x=0�ɂ���Ă��܂��̂ŁA����炵�ď������ށB
	inc			hl							;���̏ꍇ�A�Ō���̃A�g���r���[�g�͒ǂ��o����Ė����ƂȂ�
	ld			b,1
	jr			.sortlp
.skip2:										;x=0 �̏ꍇ�͂��̂܂ܐ擪�̃A�g���r���[�g�ɏ㏑���B�����o���̓i�V�B
	ld			(hl),c
	inc			hl
	ld			(hl),a
	jr			.exit

.found:										;�����J�n�ʒu�̂��̂���������A�����ɏ㏑������
	pop			af							;���̏ꍇ�A�擪�� 0 �ł� 80 �ȏ�ł��Ȃ������ȊJ�n�ʒu���������܂�Ă����͂��Ȃ̂ŏ㏑���Ŗ��Ȃ�
	inc			hl
	ld			(hl),a
	jr			.exit

.clear:
	pop			af
	ld			b,20
.clearlp:
	ld			(hl),$80					;�S�N���A�� BASIC �ɕ���� $80,$E8 �Ƃ���B
	inc			hl
	ld			(hl),$E8
	inc			hl
	djnz		.clearlp
.exit:
	; pop			af
	; out			($32),a
	ret

.sub:										;b=y c=x �� hl=tatr�@�ɂ���
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

#ENDLIB

