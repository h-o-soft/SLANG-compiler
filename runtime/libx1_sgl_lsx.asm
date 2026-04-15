; Converted from lib/libdef/libx1_sgl.yml
; SLANG Runtime Library (new format)

; @name X1SGLINCLUDE
; @lib x1sgl_lsx
;---------------------------------------------------------------;
;	Copyright (c) 2019 macro_define.asm
;	This software is released under the MIT License.
;	http://opensource.org/licenses/mit-license.php
;---------------------------------------------------------------;

;---------------------------------------------------------------;
;	�}�N��
;---------------------------------------------------------------;
OUT_L_ADD_H	MACRO
	out	(c),l
	add	a,h
	ld	b,a
ENDM

OUT_B_HL_ADD_E MACRO
	; Areg: �o�͗\��� Breg+1�̒l�������Ă���B
	; Ereg: �P���C�����p 08h
	inc hl		; MASK�������X�L�b�v

	outi
	add a,e
	ld	b,a
ENDM

OUT_R_HL_ADD_E MACRO
	; Areg: �o�͗\��� Breg+1�̒l�������Ă���B
	; Ereg: �P���C�����p 08h
	inc hl		; MASK�������X�L�b�v

	outi

	add a,e
	ld	b,a
ENDM

OUT_G_HL_ADD_E MACRO
	; Areg: �o�͗\��� Breg+1�̒l�������Ă���B
	; Ereg: �P���C�����p 08h
	inc hl		; MASK�������X�L�b�v

	outi

	add a,e
	ld	b,a
ENDM

OUT_B_HL MACRO
	; BRG - 0
	inc hl		; MASK�������X�L�b�v

	outi
ENDM

OUT_R_HL MACRO
	; BRG - 0
	inc hl		; MASK�������X�L�b�v

	outi
ENDM

OUT_G_HL MACRO
	; BRG - 0
	inc hl		; MASK�������X�L�b�v

	outi
ENDM

; RG�v���[���o�͗p
OUT_RG_HL_ADD_D_E	MACRO
	inc hl		; MASK�������X�L�b�v

	outi		; Red�v���[���o��

	add a,d
	ld b,a
;
	outi		; Green�v���[���o��

	add a,e		; ���̌� Red�v���[���ɖ߂��B
	ld b,a
;
ENDM

; RG�v���[���o�͗p
; Areg: VRAM(H)+1, Dreg: 040h
OUT_RG_HL_ADD_D	MACRO
	inc hl		; MASK�������X�L�b�v

	outi		; Red�v���[���o��
	add a,d
	ld b,a
;
	outi		; Green�v���[���o��
;
ENDM


; BG�v���[���o�͗p
; Areg: VRAM(H)+1, Dreg: 080h
OUT_BG_HL_ADD_D	MACRO
	inc hl		; MASK�������X�L�b�v

	outi		; Blue�v���[���o��

	add	a,d		; Red�v���[���X�L�b�v
	ld	b,a

	outi		; Green�v���[���o��

ENDM

; BR�v���[���o�͗p
; Areg: VRAM(H)+1, Dreg: 040h
OUT_BR_HL_ADD_D	MACRO
	inc hl		; MASK�������X�L�b�v

	outi		; Blue�v���[���o��

	add	a,d		; Red�v���[���ցB
	ld	b,a

	outi		; Red�v���[���o��

ENDM

; BR�v���[���o�͗p (�}�X�N��)
; Areg: VRAM(H)+1, Dreg: 040h
OUT_BR_HL_ADD_D_N	MACRO
	outi		; Blue�v���[���o��

	add	a,d		; Red�v���[���ցB
	ld	b,a

	outi		; Red�v���[���o��

ENDM

; 1�v���[���o�͗p (�}�X�N��)
; Areg: VRAM(H)+1
OUT_1_HL	MACRO
	outi		; 1�v���[���o��

ENDM

; BRG�v���[���o�͗p (�}�X�N�L��)
; Areg: VRAM(H)+1, Dreg: 040h
OUT_BRG_HL_ADD_D	MACRO
	inc		hl	; �}�X�N�X�L�b�v

	outi		; Blue�v���[���o��
	add	a,d		; Red�v���[���ցB
	ld	b,a

	outi		; Red�v���[���o��
	add	a,d		; Green�v���[���ցB
	ld	b,a

	outi		; Green�v���[���o�́B
ENDM



; out (c),l / add a,h ld b,h
OUT_L8_ADD_H MACRO
	ld hl,008ffh

	; VRAM Clear

	; 0
	out (c),l
	add a,h
	ld b,a

	; 1
	out (c),l
	add a,h
	ld b,a

	; 2
	out (c),l
	add a,h
	ld b,a

	; 3
	out (c),l
	add a,h
	ld b,a

	; 4
	out (c),l
	add a,h
	ld b,a

	; 5
	out (c),l
	add a,h
	ld b,a

	; 6
	out (c),l
	add a,h
	ld b,a

	; 7
	out (c),l

ENDM

; (IO:bc)��(IO:bc') ��8byte���s���B
OUT_BC_BCEX8	MACRO
	in	a,(c)
	inc	bc
	exx
	out	(c),a
	inc	bc
	exx

	in	a,(c)
	inc	bc
	exx
	out	(c),a
	inc	bc
	exx

	in	a,(c)
	inc	bc
	exx
	out	(c),a
	inc	bc
	exx

	in	a,(c)
	inc	bc
	exx
	out	(c),a
	inc	bc
	exx

	in	a,(c)
	inc	bc
	exx
	out	(c),a
	inc	bc
	exx

	in	a,(c)
	inc	bc
	exx
	out	(c),a
	inc	bc
	exx

	in	a,(c)
	inc	bc
	exx
	out	(c),a
	inc	bc
	exx

	in	a,(c)
	inc	bc
	exx
	out	(c),a
	inc	bc
	exx

ENDM

; (hl)��(IO:bc) ��8byte���s���B
OUT_HL_BC8 MACRO
	; 0
	inc	b
	outi
	inc	bc

	inc	b
	outi
	inc	bc

	inc	b
	outi
	inc	bc

	inc	b
	outi
	inc	bc

	; 4
	inc	b
	outi
	inc	bc

	inc	b
	outi
	inc	bc

	inc	b
	outi
	inc	bc

	inc	b
	outi
	inc	bc
ENDM

; ADD_BC_04828 BCreg�� 4828h�𑫂�
; BCreg��Green�v���[����7���C���ڂ��w���Ă���Ƃ��āA
; 04828h�𑫂�����1���C������Blue�v���[���Ɉړ�����B

ADD_BC_4828 MACRO
	; VRAM�����̒i�ցB
	ld a, 028h		; 7
	add a,c			; 4
	ld c,a			; 4
	ld a, 048h		; 7
	adc a,b			; 4
	ld b,a			; 4
ENDM

ADD_BC_C828 MACRO
	; VRAM�����̒i�ցB
	ld a, 028h		; 7
	add a,c			; 4
	ld c,a			; 4
	ld a, 0C8h		; 7
	adc a,b			; 4
	ld b,a			; 4
ENDM

ADD_BC_0028_AND_C7 MACRO
	; VRAM�����̒i�ցB
	ld a, 028h		; 7
	add a,c			; 4
	ld c,a			; 4

	ld a, 00h		; 7
	adc a,b			; 4
	and	07h
	ld b,a			; 4
ENDM

BLEND_RGB_HL_ADD_B_D MACRO
	ld e,(hl)	; mask
	inc hl

	; Blue.
	in a,(c)
	and e
	or (hl)
	out (c),a
	inc hl

	ld a,d
	add a,b
	ld b,a

	; Red.
	in a,(c)
	and e
	or (hl)
	out (c),a
	inc hl

	ld a,d
	add a,b
	ld b,a

	; Green.
	in a,(c)
	and e
	or (hl)
	out (c),a
	inc hl

ENDM

; B�̂�BLEND���āAR,G��AND�̂݁B
; Dreg�͎��̃v���[���ւ� 40h�������Ă���B
; Ereg: �}�X�N
BLEND_B_HL_ADD_B_D MACRO
	ld e,(hl)	; mask
	inc hl

	; Blue.
	in a,(c)
	and e
	or (hl)
	out (c),a
	inc hl

	ld a,d
	add a,b
	ld b,a

	; Red�� and�Ō���������̂�
	in a,(c)
	and e
	out (c),a

	ld a,d
	add a,b
	ld b,a

	; Green�� and�Ō���������̂�
	in a,(c)
	and e
	out (c),a

ENDM


; R�̂�BLEND���āAB,G��AND�̂݁B
; Dreg�͎��̃v���[���ւ� 40h�������Ă���B
; Ereg: �}�X�N
BLEND_R_HL_ADD_B_D MACRO
	ld e,(hl)	; mask
	inc hl

	; Blue�� and�Ō���������̂�
	in a,(c)
	and e
	out (c),a

	; Blue��Red
	ld a,d
	add a,b
	ld b,a

	; Red.
	in a,(c)
	and e
	or		(hl)
	out (c),a
	inc hl

	; Red��Green
	ld a,d
	add a,b
	ld b,a

	; Green�� and�Ō���������̂�
	in a,(c)
	and e
	out (c),a

ENDM

; G�̂�BLEND���āAB,R��AND�̂݁B
; Dreg�͎��̃v���[���ւ� 40h�������Ă���B
; Ereg: �}�X�N
BLEND_G_HL_ADD_B_D MACRO
	ld e,(hl)	; mask
	inc hl

	; Blue�� and�Ō���������̂�
	in a,(c)
	and e
	out (c),a

	; Blue��Red
	ld a,d
	add a,b
	ld b,a

	; Red�� and�Ō���������̂݁B
	in a,(c)
	and e
	out (c),a

	; Red��Green
	ld a,d
	add a,b
	ld b,a

	; Green blend.
	in a,(c)
	and e
	or		(hl)
	out (c),a
	inc hl
ENDM

; B,R��BLEND���āAG��AND�̂݁B
; Dreg: ���v���[���Z�o�p�� 040h�B
BLEND_BR_HL_ADD_B_D MACRO
	ld e,(hl)	; mask
	inc hl

	; Blue: Blend
	in	a,(c)
	and	e
	or	(hl)
	out	(c),a
	inc	hl

	ld	a,d	; Red�v���[����
	add	a,b
	ld	b,a

	; Red: Blend
	in	a,(c)
	and	e
	or	(hl)
	out	(c),a
	inc	hl

	ld a,d	; Green�v���[���ցB
	add a,b
	ld b,a

	; Green�� and�Ō����J����̂݁B
	in	a,(c)
	and	e
	out	(c),a

ENDM


; B,G��BLEND���āAR��AND�̂݁B
; Dreg: ���v���[���Z�o�p�� 040h�B
BLEND_BG_HL_ADD_B_D MACRO
	ld e,(hl)	; mask
	inc hl

	; Blue: Blend
	in	a,(c)
	and	e
	or	(hl)
	out	(c),a
	inc	hl

	ld	a,d	; Red�v���[����
	add	a,b
	ld	b,a

	; Red�� and�Ō����J����̂݁B
	in	a,(c)
	and	e
	out	(c),a

	ld a,d	; Green�v���[���ցB
	add a,b
	ld b,a

	; Green: Blend
	in a,(c)
	and e
	or (hl)
	out (c),a
	inc hl

ENDM

BLEND_RG_HL_ADD_B_D MACRO
	ld e,(hl)	; mask
	inc hl

;	; Blue �� and�Ō����󂯂�̂݁B
	in a,(c)
	and e
	out (c),a

	ld a,d	; Red�v���[���ցB
	add a,b
	ld b,a

	; Red.
	in a,(c)
	and e
	or (hl)
	out (c),a
	inc hl

	ld a,d	; Green�v���[���ցB
	add a,b
	ld b,a

	; Green.
	in a,(c)
	and e
	or (hl)
	out (c),a
	inc hl

ENDM


D_BLEND_RGB_HL_ADD_B_D MACRO
	ld e,(hl)	; mask
	inc hl

	; Blue.
	in a,(c)
	and e

	ld	a,0ffh
	and	e

;	or (hl)
	out (c),a
	inc hl

	ld a,d
	add a,b
	ld b,a

	; Red.
	in a,(c)
	and e
;	or (hl)
	out (c),a
	inc hl

	ld a,d
	add a,b
	ld b,a

	; Green.
	in a,(c)
	and e
;	or (hl)
	out (c),a
	inc hl

ENDM

; G��B�ɖ߂����Ɏg�p
ADD_B_80	MACRO
	ld	a,080h
	add a,b
	ld b,a
ENDM

; R��G�Ɏg�p
ADD_B_40	MACRO
	ld	a,040h
	add a,b
	ld b,a
ENDM

; BRG��B�ɖ߂��čX��1���C�����B
ADD_B_88	MACRO
	ld	a,088h
	add a,b
	ld b,a
ENDM

; B ���X��1���C�����B(Dreg �� 08h�������Ă���)
ADD_B_D		MACRO
	ld	a,d
	add	a,b
	ld	b,a
ENDM


; Breg �v���[���𑫂��B
; Areg��Breg�Ɠ����l�������Ă��āADreg��040h�������Ă���B
ADD_A_D_B	MACRO
	add	a,d
	ld	b,a
ENDM

; ����: B�v���[���ɖ߂��čX��1���C�����B
; Areg: VRAM(G�v���[��)+1, Ereg: 088h
ADD_B_E MACRO
	add	a,e
	ld	b,a
ENDM

; Areg��4bit���㉺����ւ���B
RRCA4	MACRO
	rrca
	rrca
	rrca
	rrca
ENDM

; �W���C�p�b�h (��)
BIT_A_0_KEY_UP MACRO
	bit	0,a
ENDM

; �W���C�p�b�h (��)
BIT_A_1_KEY_DOWN MACRO
	bit	1,a
ENDM

; �W���C�p�b�h (��)
BIT_A_2_KEY_LEFT MACRO
	bit	2,a
ENDM

; �W���C�p�b�h (�E)
BIT_A_3_KEY_RIGHT MACRO
	bit	3,a
ENDM

; �W���C�p�b�h (�g���K1)
BIT_A_5_KEY_TRG1 MACRO
	bit	5,a
ENDM

; �W���C�p�b�h (�g���K2)
BIT_A_6_KEY_TRG2 MACRO
	bit	6,a
ENDM

; �A�i���O�p���b�g�f�[�^�ݒ�
; PALET_DATA_CDE [�p���b�g�ԍ�(0-4095)], [GRB] (�e4bit)
; CDEreg �ɃZ�b�g����B

PALET_DATA_CDE	MACRO
	ld		c, %1>>4
	ld		de,  ( ( ((%1 & 0fh ) << 4) | (%2 & 0fh) ) << 8) | (%2 >> 4)
ENDM

;END


;---------------------------------------------------------------;
;	Copyright (c) 2019 value_define.asm
;	This software is released under the MIT License.
;	http://opensource.org/licenses/mit-license.php
;---------------------------------------------------------------;

;---------------------------------------------------------------;
;---------------------------------------------------------------;

; �������o���N�̐ݒ�
;	���C�������� 010h
;	�o���N������ Bank 0: 00h    Bank 1: 01h
BANK_0B00				equ	0b00h

BANK_MAIN				equ 010h
BANK_00					equ	000h
BANK_01					equ 001h

CTC_ADRS				equ	01fa0h

ATTR_VRAM_ADRS			equ	02000h
TEXT_VRAM_ADRS			equ	03000h
KTEXT_VRAM_ADRS			equ	03800h

TEXT_VRAM0_ADRS			equ	03000h
TEXT_VRAM1_ADRS			equ	03400h

TEXT_VRAM19_SIZE		equ	(19*40)
TEXT_VRAM14_SIZE		equ	(14*40)
TEXT_VRAM7_SIZE			equ	(7*40)

KANJI_VRAM_ADRS			equ	03800h

B_VRAM_ADRS				equ	04000h
R_VRAM_ADRS				equ	08000h
G_VRAM_ADRS				equ	0c000h
PLANE_SIZE				equ	04000h		; 1�v���[���̃T�C�Y

FLIP_ADRS				equ	04h	; VRAM�A�h���X�t���b�v�l


BLEND_BUFFER_ADRS		equ	09f00h
BLEND_BUFFER_SIZE		equ	05dc0h

PCG_BLUE				equ	015h
PCG_RED					equ	016h
PCG_GREEN				equ	017h

;;CRTC_1FD0				equ	(023h | 08h)	; PCG�����A�N�Z�X���[�h + 24KHz + 2���X�^
CRTC_1FD0				equ	023h	; PCG�����A�N�Z�X���[�h + 24KHz + 2���X�^

CRTC_1FD0_L				equ	020h	; PCG�����A�N�Z�X���[�h


;JUMP_TABLE_SIZE12		equ	0f5h


; ���C���������}�b�v
; 0000-0f4ff �v���O����,�f�[�^�G���A
; 0f500-0f5ff �����݃x�N�g��,�X�^�b�N
; 0f600h VRAM�A�h���X�e�[�u��(H)
; 0f700h VRAM�A�h���X�e�[�u��(L)
; 0f800h �r�b�g���C���o�b�t�@(Page 0)
; 0fc00h �r�b�g���C���o�b�t�@(Page 1)

;INT_VECTOR_BUFF			equ	0f500h
;
;INT_VECTOR_KEYBOARD		equ	INT_VECTOR_BUFF + 010h
;
;STACK_BUFF				equ 0f500h+0100h	; �X�^�b�N�|�C���^


; LSX-Dodgers版: 固定アドレス依存を排除し、@works ラベルから派生
; VRAM_ADRS_TBL_H_BUF / VRAM_ADRS_TBL_L_BUF / BITLINE_BUFFER0 / BITLINE_BUFFER1 は
; X1SGLBASE の @works で確保される (連続配置)。
VRAM_ADRS_TBL_H			equ	VRAM_ADRS_TBL_H_BUF >> 8
VRAM_ADRS_TBL_L			equ	VRAM_ADRS_TBL_L_BUF >> 8

BITLINE_BUFFER_SIZE		equ	1000	; init_bitline でクリアする使用範囲 (実バッファは1024)

;�p���b�g�f�[�^
GAME_PALET_B			equ	0ceh
GAME_PALET_R			equ	0f2h
GAME_PALET_G			equ	066h

; --------------
; �L�����N�^KIND
; �W�����v�e�[�u���̊֌W���� 3�Â�����B
KIND_NONE				equ	0
KIND_A				equ	1*3
KIND_B				equ	2*3
KIND_C				equ	3*3

; --------------
; �L�����N�^�p�^�[���f�[�^
X_OFS_0					equ	00h
X_OFS_1					equ	01h
X_OFS_2					equ	02h
X_OFS_3					equ	03h
X_OFS_4					equ	04h
X_OFS_5					equ	05h
X_OFS_6					equ	06h
X_OFS_7					equ	07h
X_OFS_NUM				equ	08h

; --------------
; �L�����N�^�p�^�[��

; �K�v�ɉ����Ē�`����(���A��{�͂����ł͒�`���Ȃ�)
PAT_01					equ	01h*2
PAT_02					equ	02h*2
PAT_03					equ	03h*2

;---------------------------------------------------------------;
;---------------------------------------------------------------;

; ��ʊO����萔
; (�\��(X ���8bit) + OFF_SCREEN_X_OFFSET) > OFF_SCREEN_X_RANGE �̎���ʊO
;
; ��:
; �@��ʊO: -8 �` 328�Ƃ��ăX�N���[���l(���8bit)�� 0fch�`0a4h�ɂȂ�B
; �@+3�� 0a7h�`0ffh�͈̔͂ł���Ή�ʊO�B
; �@OFF_SCREEN_X_OFFSET �𑫂��āAOFF_SCREEN_X_RANGE���傫����Ή�ʊO�B
OFF_SCREEN_X_OFFSET		equ	03h
OFF_SCREEN_X_RANGE		equ	167
CLIP_RIGHT_SCREEN_X		equ	164



;END


;---------------------------------------------------------------;
;	Copyright (c) 2019 render_util.asm
;	This software is released under the MIT License.
;	http://opensource.org/licenses/mit-license.php
;---------------------------------------------------------------;

;//---------------------------------------------------------------;
;//	VRAM���w��̒l�Ŗ��߂�B
;//		BCreg: VRAM �A�h���X
;//		HLreg: ����
;//		Dreg: ���߂�l
;//---------------------------------------------------------------;
clear_graphic_vram_b:
	ld bc, B_VRAM_ADRS
	ld hl, 4000h
	ld d, 000h
	jr fill_vram

clear_graphic_vram_r:
	ld bc, R_VRAM_ADRS
	ld hl, 4000h
	ld d, 000h
	jr fill_vram

clear_graphic_vram_g:
	ld bc, G_VRAM_ADRS
	ld hl, 4000h
	ld d, 000h
	jr fill_vram

clear_text_vram:
	ld bc, TEXT_VRAM_ADRS
	ld hl, 1000h
	ld d, 00h
	jr fill_vram

clear_kanji_vram:
	ld bc, KANJI_VRAM_ADRS
	ld hl, 0800h
	ld d, 00h
	jr fill_vram

fill_attr_vram:
	; �S��PCG�A�g���r���[�g�ɂ���B
	ld bc, ATTR_VRAM_ADRS
	ld hl, 0800h
	ld d, 07h
;;	ld d, 007h
	jr fill_vram

fill_attr_vram_line:
	; �S��PCG�A�g���r���[�g�ɂ���B
	ld bc, ATTR_VRAM_ADRS
	ld hl, 0028h
	ld d, 027h
	call	fill_vram

	ld bc, ATTR_VRAM_ADRS+0400h
	ld hl, 0028h
	ld d, 027h
	call	fill_vram

	ret

; �f�o�b�O�\���̈揉����
fill_text_attr_vram_ascii:
	; attr
	ld	bc, ATTR_VRAM_ADRS+40*23
	ld	hl, 40*2
	ld	d,07h
	call	fill_vram

	ld	bc, ATTR_VRAM_ADRS+0400h+40*23
	ld	hl, 40*2
	ld	d,07h
	call	fill_vram

	; text
	ld	bc, TEXT_VRAM_ADRS+40*23
	ld	hl, 40*2
	ld	d,020h
	call	fill_vram

	ld	bc, TEXT_VRAM_ADRS+0400h+40*23
	ld	hl, 40*2
	ld	d,020h
	call	fill_vram

	ret

; VRAM������
; BCreg: �����ݐ�A�h���X
; HLreg: �����݃T�C�Y
; Dreg: �������ޓ��e
fill_vram:
fv_1:
	out (c),d
	inc bc

	dec hl
	ld a,h
	or l
	jr	nz, fv_1

	ret


fill_graphic_vram_r:
	ld bc, R_VRAM_ADRS
	ld hl, 4000h
	ld d, 0f0h
	jr fill_vram

fill_graphic_vram_g:
	ld bc, G_VRAM_ADRS
	ld hl, 4000h
	ld d, 0ffh
	jr fill_vram

fill_text_vram:
	ld	bc, TEXT_VRAM_ADRS
	ld	hl, 0800h
	ld	d, 20h
	jp	fill_vram

fill_gram_colorbar:
	ld		a, 00h
	ld		hl, 0000h

fgcb_1:
	bit		0,a
	ld		bc, B_VRAM_ADRS
	call	nz, fill_gram_vline2

	bit		1,a
	ld		bc, B_VRAM_ADRS + 0400h
	call	nz, fill_gram_vline2

	bit		2,a
	ld		bc, R_VRAM_ADRS
	call	nz, fill_gram_vline2

	bit		3,a
	ld		bc, R_VRAM_ADRS + 0400h
	call	nz, fill_gram_vline2

	bit		4,a
	ld		bc, G_VRAM_ADRS
	call	nz, fill_gram_vline2

	inc		hl
	inc		hl

	inc		a
	cp		16
	jr		nz, fgcb_1
;
;;	ld		a, 10h
	ld		hl, 12*40

fgcb_2:
	bit		0,a
	ld		bc, B_VRAM_ADRS
	call	nz, fill_gram_vline2

	bit		1,a
	ld		bc, B_VRAM_ADRS + 0400h
	call	nz, fill_gram_vline2

	bit		2,a
	ld		bc, R_VRAM_ADRS
	call	nz, fill_gram_vline2

	bit		3,a
	ld		bc, R_VRAM_ADRS + 0400h
	call	nz, fill_gram_vline2

	bit		4,a
	ld		bc, G_VRAM_ADRS
	call	nz, fill_gram_vline2

	inc		hl
	inc		hl

	inc		a
	cp		32
	jr		nz, fgcb_2
;
	ret

fill_gram_vline2:
	push	af
	push	hl

	add		hl,bc
	ld		b,h
	ld		c,l

	push	bc
	call	fill_gram_vline
	pop		bc

	inc		bc

	call	fill_gram_vline
	pop		hl
	pop		af

	ret

fill_gram_vline:
;;	ld		bc, 0000h
	ld		de, 08c8h

	ld		hl, 0ff0ch

	ld		a,b
fgv_1:
	out		(c),h	; 0
	add		a,d
	ld		b,a

	out		(c),h	; 1
	add		a,d
	ld		b,a

	out		(c),h	; 2
	add		a,d
	ld		b,a

	out		(c),h	; 3
	add		a,d
	ld		b,a

	out		(c),h	; 4
	add		a,d
	ld		b,a

	out		(c),h	; 5
	add		a,d
	ld		b,a

	out		(c),h	; 6
	add		a,d
	ld		b,a

	out		(c),h	; 7

	ld		a,028h
	add		a,c
	ld		c,a

	ld		a,b
	adc		a,e
	ld		b,a

	dec		l
	jr		nz, fgv_1

	ret


;//---------------------------------------------------------------;
;// Text(PCG)�̏��GRAM��\������B
;// 0(��)�͉����Ă����Ȃ���Text�������Ȃ��B
;// �����g���ɂ͂ǂꂩ�̐F���]���ɂȂ�B(=GRAM 7�F���������Ȃ�)
;//---------------------------------------------------------------;
vram_priority:
	ld bc, 01300h
	ld a, 0feh
;	ld	a, 00h
	out (c),a

	ret

vram_palette_init:

	ld bc, 1000h

	ld a, GAME_PALET_B
	ld hl, ( GAME_PALET_R << 8 ) | GAME_PALET_G

	out (c),a
	inc b
	out (c),h
	inc b
	out (c),l

	ret

;---------------------------------------------------------------;
;		�����_
;---------------------------------------------------------------;
; �F
task_bar:
	push	bc
	push	hl

	ld		bc,01000h
	ld		hl, ( GAME_PALET_B << 8 ) | GAME_PALET_B | 0x01

tbr_1:
	out		(c),l
	ld		l,8
tb_1:
	dec		l
	jr		nz,tb_1
	out		(c),h

	pop		hl
	pop		bc

	ret

; �ԐF
task_bar_red:
	push	bc
	push	hl

	ld		bc,01100h
	ld		hl, ( GAME_PALET_R << 8 ) | GAME_PALET_R | 0x01
	jr		tbr_1

; �����ʂŐF���ς�鏈���o�[�B
; 0�t���[����: �F
; 1�t���[����: �ԐF
; 2�t���[���ڈȍ~: �ΐF
task_bar_vsync:
	ld	bc,01000h
	ld	a, (vsync_state)
	ld	hl, ( GAME_PALET_B << 8 ) | GAME_PALET_B | 0x01
	cp	01h+1
	jr	c, tv_1
;
	inc	b
	ld	hl, ( GAME_PALET_R << 8 ) | GAME_PALET_R | 0x01
	cp	03h+1
	jr	c, tv_1
;
	inc	b
	ld	hl, ( GAME_PALET_G << 8 ) | GAME_PALET_G | 0x01

tv_1:
	out		(c),l
	ld		l,8
tbv_1:
	dec		l
	jr		nz,tbv_1

	; ���ɖ߂��B
	out		(c),h

	ret

;---------------------------------------------------------------;
;	VRAM�A�h���X�e�[�u��
;	f600-f7ffh ��Y���W(0-199)�ɑΉ�����VRAM�A�h���X(4000h�` Blue)�e�[�u�����쐬����B
;	f600-f6ff VRAM��ʃA�h���X
;	f700-f7ff VRAM���ʃA�h���X
;---------------------------------------------------------------;
create_vram_adrs_tbl:
	ld c,25

	ld de, B_VRAM_ADRS
	ld l,0	; ���C�� (0-199)

cvat_2:
	ld b,8

	push de

cvat_1:
	ld h, VRAM_ADRS_TBL_H
	ld (hl),d
	inc h
	ld (hl),e

	inc l

	push hl
	ld hl, 0800h
	add hl,de
	ex de,hl
	pop hl

	djnz cvat_1

	pop de

	push hl
	ld hl, 0028h
	add hl,de
	ex de,hl
	pop hl

	dec c
	jp nz,cvat_2

	ret

; ----
;	END

;---------------------------------------------------------------;
;	Copyright (c) 2019 text_render.asm
;	This software is released under the MIT License.
;	http://opensource.org/licenses/mit-license.php
;---------------------------------------------------------------;

;---------------------------------------------------------------;
;	�e�L�X�g�`��֘A
;---------------------------------------------------------------;

;---------------------------------------------------------------;
;	�L�����������Z����B
;	Areg: ���Z��
;		BCD�Ȃ̂� 1�����ꍇ�� 01h ���w�肷��B
;---------------------------------------------------------------;
inc_chara_num:
	ld		hl, num_buff

	ld		a,(hl)
	add		a,01h
	daa
	ld		(hl),a

	ret

;---------------------------------------------------------------;
;	�L�����������Z����B
;	Areg: ���Z��
;		BCD�Ȃ̂� 1�����ꍇ�� 01h ���w�肷��B
;---------------------------------------------------------------;
dec_chara_num:
	ld		hl, num_buff

	ld		a,(hl)
	sub		01h
	daa
	ld		(hl),a

	ret

;---------------------------------------------------------------;
;	�L�������`�揈��
;---------------------------------------------------------------;
render_chara_num:
	ld		a,(num_buff)

	ld		bc, TEXT_VRAM_ADRS + 40*1 + 5
	ld		d,a
	RRCA4
	and		0fh
	add		a, 030h
	ld		h,a
	out		(c),a

	inc		bc

	ld		a,d
	and		0fh
	add		a, 030h
	out		(c),a

	ld		b, ( (TEXT_VRAM_ADRS + 40*1 + 5)>>8 ) + FLIP_ADRS
	out		(c),a

	dec		bc
	out		(c),h

	ret

;---------------------------------------------------------------;
;---------------------------------------------------------------;
disp_frame_dropout:
	ld		hl, dropout_cl_str
	ld		bc, TEXT_VRAM_ADRS + 40*5 + 1
	ld		a, (frame_dropout)
	or		a
	jr		z, dfd_1
;
	dec		a
	ld		(frame_dropout),a
	ld		hl, dropout_str
dfd_1:
	jp		render_text_2page

dropout_cl_str:
	db		"       ", 0

dropout_str:
	db		"Dropout", 0

;---------------------------------------------------------------;
;	FPS ���[�h��\��
;---------------------------------------------------------------;
render_fps_mode:
	ld		hl, fps_str_table
	ld		a, (fps_mode)
	rrca
	add		a,l
	ld		l,a
	ld		a,(hl)

	ld		e, '0'

	ld		bc, TEXT_VRAM_ADRS + 40*3 + 5
	out		(c),a
	inc		bc
	out		(c),e

	ld		b, ( (TEXT_VRAM_ADRS + 40*3 + 5)>>8 ) + FLIP_ADRS
	out		(c),e

	dec		bc
	out		(c),a

	ret

align 4
fps_str_table:
	db		"632"

;---------------------------------------------------------------;
;	TEXT VRAM ������ (Page0,Page1 ����)
; BCreg: VRAM�A�h���X
; HLreg: ������f�[�^ (�I�[ 00h)
;---------------------------------------------------------------;
render_text_2page:
	push	hl
	push	bc
	call	render_text
	pop		bc
	ld		a,b
	or		FLIP_ADRS
	ld		b,a
	pop		hl

;---------------------------------------------------------------;
;	TEXT VRAM ������
; BCreg: VRAM�A�h���X
; HLreg: ������f�[�^ (�I�[ 00h)
;---------------------------------------------------------------;
render_text:
rete_1:
	ld		a,(hl)
	or		a
	ret		z

	out		(c),a
	inc		bc
	inc		hl

	jp		rete_1


;----
;	END

;---------------------------------------------------------------;
;	Copyright (c) 2019 mem_util.asm
;	This software is released under the MIT License.
;	http://opensource.org/licenses/mit-license.php
;---------------------------------------------------------------;

;---------------------------------------------------------------;
;	�������ɒl���Z�b�g
;	HLreg: �������擪�A�h���X
;	BCreg: �Z�b�g����T�C�Y
;	Areg: �Z�b�g����l
;---------------------------------------------------------------;
clear_mem:
	xor	a

fill_mem:
	ld d,h
	ld e,l
	inc de

	dec bc

	ld (hl),a
	ldir

	ret

;---------------------------------------------------------------;
;	END

;---------------------------------------------------------------;
;	Copyright (c) 2019 chara_manager.asm
;	This software is released under the MIT License.
;	http://opensource.org/licenses/mit-license.php
;---------------------------------------------------------------;
;---------------------------------------------------------------;
; �L�����N�^���[�N�̏�����
;---------------------------------------------------------------;
init_chara_manager:
	ld hl, chara_work
	ld bc, CHR_SIZE * CHARA_NUM
	call clear_mem

	ret

;---------------------------------------------------------------;
; �L�������[�N���m��
; �߂�l:
;	Zflag: ���[�N���m�ۂł���
;	 IXreg: �m�ۂł������[�N�A�h���X
;	NonZflag: ���[�N�̊m�ۂɎ��s
; �ێ�: HLreg�͉󂳂Ȃ��B
;---------------------------------------------------------------;
find_chara_work:
	ld	c, CHARA_NUM

	ld	ix, chara_work
	ld	de, CHR_SIZE
few_1:
	ld	a, (ix+CHR_KIND)
	or	a
	ret	z

	add	ix,de
	dec	c
	jp	nz, few_1

	; Zflag��������B
	dec	c
	ret

; ;---------------------------------------------------------------;
; ;---------------------------------------------------------------;
; create_ball_b:
; 	call	find_chara_work_iy
; 	ret		nz
; ;
; 	call	init_ball_b
;
; 	xor		a
; 	ret
;
; ;---------------------------------------------------------------;
; ;---------------------------------------------------------------;
; create_ball_br:
; 	call	find_chara_work_iy
; 	ret		nz
; ;
; 	call	init_ball_br
;
; 	xor		a
; 	ret
;
; ;---------------------------------------------------------------;
; ;---------------------------------------------------------------;
; create_ball_brg:
; 	call	find_chara_work_iy
; 	ret		nz
; ;
; 	call	init_ball_brg
;
; 	xor		a
; 	ret

;---------------------------------------------------------------;
; �G�L�������[�N���m�� (IYreg)
; �߂�l:
;	Zflag: ���[�N���m�ۂł���
;	 IYreg: �m�ۂł������[�N�A�h���X
;	NonZflag: ���[�N�̊m�ۂɎ��s
;	����: HLreg��j�󂵂Ȃ����B
;---------------------------------------------------------------;
find_chara_work_iy:
	ld	iy, chara_work
	ld	c, CHARA_NUM
	ld	de, CHR_SIZE
fewi_1:
	ld	a, (iy+CHR_KIND)
	or	a
	ret	z

	add	iy,de
	dec	c
	jp	nz, fewi_1

	; Zflag��������B
	dec	c
	ret

remove_chara:
	ld		iy, chara_work + (CHARA_NUM-1) * CHR_SIZE
	ld		c, CHARA_NUM
	ld		de, -CHR_SIZE
rech_2:
	ld		a,(iy+CHR_KIND)
	or		a
	jr		nz, rech_1
;
	add		iy,de

	dec		c
	jp		nz, rech_2
;
	ret

rech_1:
	ld		(iy+CHR_KIND),00h
	ld		(iy+CHR_PATTERN),00h

	ret

;---------------------------------------------------------------;
; �G�L�������[�N���m�� (IXreg)
; �߂�l:
;	Zflag: ���[�N���m�ۂł���
;	 IXreg: �m�ۂł������[�N�A�h���X
;	NonZflag: ���[�N�̊m�ۂɎ��s
;	����: HLreg��j�󂵂Ȃ����B
;---------------------------------------------------------------;
find_chara_work_ix:
	ld	ix, chara_work
	ld	c, CHARA_NUM
	ld	de, CHR_SIZE
fcwi_1:
	ld	a, (ix+CHR_KIND)
	or	a
	ret	z

	add	ix,de
	dec	c
	jp	nz, fcwi_1

	; Zflag��������B
	dec	c
	ret


;---------------------------------------------------------------;
;	�L�����N�^����������
;	Areg: �L�����N�^Kind
;	IYreg: �L�����N�^���[�N
kind_init_jump:
	ld	(iy+CHR_KIND),a

	; ���ȏ������ɂ��e�[�u���W�����v
	ld	(kij_1+1),a	; 13
kij_1:
	jr	kij_1		; 12

	jp	jump_none			; 0
;	jp	init_ball_b			; 1*3
;	jp	init_ball_br		; 2*3
;	jp	init_ball_brg		; 3*3

;---------------------------------------------------------------;
;	�L�����N�^�X�V����
kind_jump:
	; ���ȏ������ɂ��e�[�u���W�����v
	ld	(kj_1+1),a	; 13
kj_1:
	jr	kj_1		; 12

	jp	jump_none			; 0
;	jp	ball_b				; 1*3
;	jp	ball_br				; 2*3
;	jp	ball_brg			; 3*3


;---------------------------------------------------------------;
;	�e�L�����N�^�̍X�V
;---------------------------------------------------------------;
update_chara_manager:
	; �{�[����FPS����
	call	update_function

	; �L�����N�^���X�V
	ld		iy, chara_work
	ld		b,CHARA_NUM
cmu_1:
	push	bc

	ld		a,(iy+CHR_KIND)
	or		a
	call	nz, kind_jump

	ld		de, CHR_SIZE
	add		iy,de

	pop		bc

	djnz	cmu_1

jump_none:
	ret

;---------------------------------------------------------------;
;	�{�[����FPS�̐���
;---------------------------------------------------------------;
update_function:
; 	ld		a, (trg_w)
; 	BIT_A_0_KEY_UP
; 	jp		z, dup_1
; ;
; 	; �{�[��(B)����ǉ��B
; 	call	create_ball_b
; 	ret		nz
; ;
; 	call	inc_chara_num
; 	call	render_chara_num
;
; 	ret
;
; dup_1:
; 	BIT_A_1_KEY_DOWN
; 	jp		z, dup_2
; ;
; 	; �{�[��(BR)����ǉ��B
; 	call	create_ball_br
; 	ret		nz
; ;
; 	call	inc_chara_num
; 	call	render_chara_num
;
; 	ret
;
; dup_2:
; 	BIT_A_2_KEY_LEFT
; 	jp		z, dup_3
; ;
; 	; �{�[��(BRG)����ǉ��B
; 	call	create_ball_brg
; 	ret		nz
; ;
; 	call	inc_chara_num
; 	call	render_chara_num
;
; 	ret
;
; dup_3:
; 	BIT_A_3_KEY_RIGHT
; 	jp		z, dup_4
; ;
; 	; �{�[�����폜
; 	call	remove_chara
; 	jp		z, dup_4
; ;
; 	call	dec_chara_num
; 	call	render_chara_num
;
; 	ret
;
; dup_4:
; 	BIT_A_5_KEY_TRG1
; 	jp		z, dup_5
; ;
; 	ld		a,(fps_mode)
; 	add		a,02h
; 	cp		06h
; 	jr		c, dup_6
; ;
; 	xor		a
; dup_6:
; 	ld		(fps_mode),a
;
; 	call	render_fps_mode
; 	ret
;
; dup_5:
	ret

;;----------------------
;	END


;---------------------------------------------------------------;
;	Copyright (c) 2019 chara_data_manager.asm
;	This software is released under the MIT License.
;	http://opensource.org/licenses/mit-license.php
;---------------------------------------------------------------;
;----

; �L�����N�^�p�^�[�� (2,4�c)�̃f�[�^�A�h���X�e�[�u�� (0�͎g�p���Ȃ�)
; �p�^�[����ނ�128���
; �f�[�^�I�t�Z�b�g(Xoffset)��0�`7 ��8���


; X���� Offset 0
;�@2,3�@�L�����N�^�p�^�[��1�@�f�[�^�A�h���X(L,H) �c 127�p�^�[����
; X���� Offset 1�`7
;�@2,3�@�L�����N�^�p�^�[��1�@�f�[�^�A�h���X(L,H) �c 127�p�^�[����

; PCG�f�[�^�̈�(6KB)�͓]����͎g����̂ŋ��p����B
; �������̂��߂�256byte �A���C�����g�ɂ���B
;;chara_data_table	equ		pcg_data

align 256

chara_data_table:
	ds X_OFS_NUM*256

; �e�p�^�[�����Ƃ�Pivot�e�[�u�� (���т͍������̂��� PivotX, PivotY�̏�)
; X����(�������Ƃ��āB�܂�2�̔{��): -80�`+7f Y����: -80�`+7f
; �������̂��߂�256byte �A���C�����g�ɂ���B
chara_pivot_table:
	ds	256

; �e�p�^�[�����Ƃ̊i�[�������o���N�e�[�u��
; 2byte���ƂɎg�p����Ă��邪��0byte�̂ݎg�p���Ă���B
; �������̂��߂�256byte �A���C�����g�ɂ���B
chara_bank_table:
	ds	256

init_chara_data_manager:
	ld hl, chara_data_table
	ld bc, X_OFS_NUM*256
	call clear_mem

	ld hl, chara_pivot_table
	ld bc, 256
	call clear_mem

	ld	hl, chara_bank_table
	ld	bc, 256
	ld	a, BANK_MAIN
	call	fill_mem


	; SGL TODO
	; ���̂ւ�͔ėp������

;	; �L�����p�^�[�� Ball(B) 00
;	ld	c, PAT_BALL_B00
;	ld	hl, ball_p0_c1
;	call cdm_set_data8_bank_main
;
;	; �L�����p�^�[�� Ball(B) 01
;	ld	c, PAT_BALL_B01
;	ld	hl, ball_p1_c1
;	call cdm_set_data8_bank_main
;
;	; �L�����p�^�[�� Ball(BR) 00
;	ld	c, PAT_BALL2_BR00
;	ld	hl, ball2_p0_c2
;	call cdm_set_data8_bank_main
;
;	; �L�����p�^�[�� Ball(BR) 01
;	ld	c, PAT_BALL2_BR01
;	ld	hl, ball2_p1_c2
;	call cdm_set_data8_bank_main
;
;	; �L�����p�^�[�� Ball(BRG) 00
;	ld	c, PAT_BALL3_BRG00
;	ld	hl, ball3_p0_c3
;	call cdm_set_data8_bank_main
;
;	; �L�����p�^�[�� Ball(BRG) 01
;	ld	c, PAT_BALL3_BRG01
;	ld	hl, ball3_p1_c3
;	call cdm_set_data8_bank_main

	ret


; Pattern��XOffset�ɑΉ������L�����N�^�f�[�^��Ԃ��B
;	Lreg: Pattern�ԍ� (2,4�c254)
;	Areg: Xpos Offset(0-7)
; �߂�l: DEreg: data adrs.
;
; �j�󂵂Ȃ�Reg: BCreg
cdm_get_data:
;( pat*8 + (x&7) )*2
	and 07h
	ld h,a

	ld de,chara_data_table
	add hl,de

	ld e,(hl)
	inc hl
	ld d,(hl)

	ret


; �L�����f�[�^�e�[�u����^���āA�e�p�^�[���̃L�����f�[�^��ݒ肷��B
; ���킹�ăL�����N�^��Pivot�e�[�u�����ݒ肷��B
; Pattern , XOffset
;	Creg: Pattern(2,4�c254)
;	HLreg: �L�����f�[�^�e�[�u��

; Bank Main�p
cdm_set_data8_bank_main:
	ld	de, 0000h
csdbm_1:
	ld	( cdm_adrs + 1 ), de

cdm_set_data8:
	; �i�[�o���N�������ݒ�
	ld	e,c
	ld	d, chara_bank_table >> 8
	ld	(de),a

	di

	ld	a,e	; �L�����p�^�[���ԍ���Areg�ɕێ�

	ld	d, chara_pivot_table >> 8

	; ldi���߂� BCreg�̓f�N�������g�����B
	ldi		; PivotX (DE++)��(HL++)
	ldi		; PivotY (DE++)��(HL++)

	ld	b, (hl)	; �f�[�^��
	inc	hl

	ld	c,a	; �Ă� Creg�ɃL�����p�^�[���l�𕜋A�B

	xor	a
csd8_1:
	ld	e,(hl)
	inc	hl
	ld	d,(hl)
	inc	hl

	push	hl

	; ���ȏ�����
cdm_adrs:
	ld	hl, 0000h

	add	hl,de
	ex	de,hl

	; X Offset�ʒu�ɑΉ������L�����f�[�^��ݒ肷��B
	;	Creg: Pattern(2,4�c254)
	;	Areg: X Offset(0-7)
	;	DEreg: �L�����f�[�^

	ld	l,c
	and 07h
	ld h,a

	push de
	ld de,chara_data_table
	add hl,de
	pop de

	ld (hl),e
	inc hl
	ld (hl),d

	pop hl

	inc	a

	djnz	csd8_1

	ei

	ret


; �L�����N�^�f�[�^�e�[�u����Ԃ��B
;	Lreg: Pattern(2,4�c254)
;	Areg: X Offset(0-7)
;
; �߂�l: HLreg - �e�[�u���A�h���XIndex.
cdm_calc_pattern_adrs:
	and 07h
	ld h,a
	ld de,chara_data_table
	add hl,de

	ret


;---------------------------------------------------------------;

;	END


;---------------------------------------------------------------;
;	Copyright (c) 2019 bitline.asm
;	This software is released under the MIT License.
;	http://opensource.org/licenses/mit-license.php
;---------------------------------------------------------------;

; BitLine�֘A (2017/02/18)
; BitLine��TimeStamp�̕ό`�o�[�W�����Ƃ��čl�Ă����B
; 8x8�̊e���C����1bit�ŕ\�����Ă���BLine0��Bit0�ɑΉ�����B
; �e���C����`�掞�Ή�����Bit�𗧂Ă�B
; ���̃o�b�t�@���g�����ŁA���C���P�ʂ̃u�����h���肪�\�ɂȂ�B

init_bitline:
	; BitLineBuffer���N���A (LSX-Dodgers版: @works ラベルを直接参照)
	ld	hl, BITLINE_BUFFER0
	ld	bc, BITLINE_BUFFER_SIZE
	call	clear_mem

	ld	hl, BITLINE_BUFFER1
	ld	bc, BITLINE_BUFFER_SIZE
	call	clear_mem

	ret


; ----
;	END


;---------------------------------------------------------------;
;	Copyright (c) 2019 crtc.asm
;	This software is released under the MIT License.
;	http://opensource.org/licenses/mit-license.php
;---------------------------------------------------------------;

;//---------------------------------------------------------------;
;//	CRTC�ݒ�
;//		in: HLreg:	CRTC�f�[�^
;//---------------------------------------------------------------;
set_crtc80:
	ld	hl, crtc80_H
	jr		set_crtc

set_crtc40:
	ld	hl, crtc40_L
set_crtc:
	ld	de,000eh
	ld	bc,01800h
sc_1:
	out	(c),d
	inc	bc

	ld	a,(hl)
	inc	hl

	out	(c),a
	dec	bc

	inc	d
	dec	e
	jr	nz,sc_1
;
	ld	a,(hl)			; 40/80���̐؂�ւ�
	inc hl
	ld	bc,01a03h
	out	(c),a

; IF 0
; 	; ��ʊǗ��|�[�g: ��𑜓x/25���C��
; 	ld a,(hl)
; 	ld	bc,01fd0h
; 	out	(c),a
; ENDIF

	ret

;//---------------------------------------------------------------;
;//		CRTC�ݒ�f�[�^ 40��/80��
;//---------------------------------------------------------------;
crtc40_L:
	db	37h,28h,2dh,34h,1fh,02h,19h,1ch,00h,07h,00h,00h,00h,00h,0dh
	; 01fd0 - �݊����[�h
	db	CRTC_1FD0_L

crtc40_H:
	db	35h,28h,2dh,84h,1bh,00h,19h,1ah,00h,0fh,00h,00h,00h,00h,0dh
	; 01fd0 - PCG�����A�N�Z�X���[�h On
	db	CRTC_1FD0

crtc80_L:
	db	6bh,50h,59h,38h,1fh,02h,19h,1ch,00h,07h,00h,00h,00h,00h,0ch
	db	00h

crtc80_H:
	db	6bh,50h,59h,88h,1bh,00h,19h,1ah,00h,0fh,00h,00h,00h,00h,0ch
	db	03h

;//---------------------------------------------------------------;
;//	�ؑ֏�����
;//---------------------------------------------------------------;
init_screen:
	; �폜�L�����o�b�t�@��������
	; ������ Page1�p
	ld	hl, clear_char_work1
	ld	( del_char_write_w+1 ),hl

	ret

;//---------------------------------------------------------------;
;//---------------------------------------------------------------;
init_flip:
	xor		a
	ld		( flip_w ),a
	ld		( flip_delchr_w ),a
	ld		( vsync_state), a
	ld		( vsync_w ),a

	ld		a, 04h
	ld		( flip_render_w ),a

	ret


;//---------------------------------------------------------------;
;//	Screen0,Screen1��؂�ւ���B
;//---------------------------------------------------------------;
flip_screen:
	ld	bc, 01800h
	ld	a, 0ch		; CRTC 12Reg.
	out	(c),a

	; flip_w(�\���y�[�W)�� xor 04h �Ŕ��]����B
	; flip_render_w �́A���̔��]�Ȃ̂ŁA�P�ɃR�s�[���邾���ŃI�P�[�B

	ld	a,(flip_w)	; 0
	ld	(flip_render_w),a	; 0
	xor	FLIP_ADRS
	ld	(flip_w),a	; 4

	inc	c
	out	(c),a

setup_clear_char_work:
	; flip_w = 0 ���A�����L�������[�N clear_char_work1
	; flip_w = 4 ���A�����L�������[�N clear_char_work0
	ld	hl, clear_char_work1
	ld	de, clear_char_work0
	or	a
	jp	z, fsc_1

	ex	de,hl
fsc_1:
	ld	( del_char_write_w+1 ),hl
	ld	( del_char_read_w+1 ),hl

	ret


align 8
del_char_num_w:
	db	000h	; Page0�̍폜�L�����o�b�t�@��

;
; �������̂��ߍ폜�L�����o�b�t�@����z�u��4byte align�ɂ��Ă���B
; �X�L�}�����������Ȃ��̂ł����� flip_w�֌W�̃��[�N�𖄂߂Ă����B

flip_w:
	; FlipWork
	; �\���y�[�W���i�[����B
	; 000h�̎� �`��y�[�W1,�\���y�[�W0
	; 004h�̎� �`��y�[�W0,�\���y�[�W1
	db	000h

flip_render_w:
	; �`��y�[�W���i�[����Bflip_w �Ƃ͔��΂̏�ԁB
	; 000h�̎� �`��y�[�W0
	; 004h�̎� �`��y�[�W1
	db	004h

flip_delchr_w:
	; �폜�L�����o�b�t�@�y�[�W���i�[����B
	; 000h �̎� �폜�L�����o�b�t�@������ 0,�폜�L�����o�b�t�@�Ǐo�� 1
	; 001h �̎� �폜�L�����o�b�t�@������ 1,�폜�L�����o�b�t�@�Ǐo�� 0
	db	000h

del_char_num_w_page1:
	db	000h	; Page1�̍폜�L�����o�b�t�@��

	; �t���[�����Ƃ̃J�E���^
frame_cnt:
	ds	1

align 2
vsync_w:
	db	000h

vsync_state:
	db	000h

; FPS
; 00: 60fps 02: 30fps 04: 20fps
fps_mode:
	db	000h

frame_dropout:
	db	000h


;//---------------------------------------------------------------;
;//---------------------------------------------------------------;
wait_vsync_fps:
	ld		a,(fps_mode)
	sra		a
	jr		z, wait_vsync60_state	; 60fps Vsync�҂�
;
	dec		a
	jr		nz, wait_vsync20_state	; 20fps Vsync�҂�
;
	jp		wait_vsync30_state		; 30fps Vsync�҂�

;//---------------------------------------------------------------;
;//---------------------------------------------------------------;
; VSync(60fps)�҂�
wait_vsync60_state:
	ld		a, (vsync_state )
	cp		02h
	jr		c, wvs60_3

;
	ld		a, 60
	ld		(frame_dropout),a
wvs60_3:

	; State0: Vsync�J�n�҂�
	ld		bc, 1a01h
wvs60_1:
	in		a,(c)
	jp		p,wvs60_1
;
	; State1: Vsync�J�n
wvs60_2:
	in		a,(c)
	jp		m,wvs60_2

	push	af
	call	!VSYNC_PROC
	pop	af

	and		080h
	ld		(vsync_w),a

	xor		a
	ld		(vsync_state),a

	ret

; VSync(30fps)�҂�
wait_vsync30_state:
	ld		a, (vsync_state )
	cp		04h
	jr		c, wvs_5
;
	ld		a, 30
	ld		(frame_dropout),a
wvs_5:

	ld		bc, 1a01h

	ld		a, (vsync_state)
	or		a
	jp		z, wvs_1
;
	dec		a
	jp		z, wvs_2
;
	dec		a
	jp		z, wvs_3
;
	jp		wvs_4

	; State0: Vsync�J�n�҂�
wvs_1:
	in	a,(c)
	jp	p,wvs_1

;
	; State1: Vsync�I���҂�
wvs_2:
	in	a,(c)
	jp	m, wvs_2

	push bc
	call	!VSYNC_PROC
	pop bc
;
	; State2: Vsync�J�n�҂�
wvs_3:
	in	a,(c)
	jp	p, wvs_3

wvs_4:
	in	a,(c)
	jp	m, wvs_4

	push af
	call	!VSYNC_PROC
	pop af

	; �J�n������State0�ɖ߂��B

	and		080h
	ld		(vsync_w),a

	xor		a
	ld		(vsync_state),a

	ret


; VSync(20fps)�҂�
wait_vsync20_state:
	ld		a, (vsync_state )
	cp		06h
	jr		c, wvs20_7
;
	ld		a, 20
	ld		(frame_dropout),a
wvs20_7:

	ld	bc, 1a01h

	ld	a, (vsync_state)
	or	a
	jp	z, wvs20_1

	dec	a
	jp	z, wvs20_2

	dec	a
	jp	z, wvs20_3

	dec	a
	jp	z, wvs20_4

	dec	a
	jp	z, wvs20_5

	jp	wvs20_6

	; State0: Vsync�J�n�҂� (1�t���[����)
wvs20_1:
	in	a,(c)
	jp	p,wvs20_1

;
	; State1: Vsync�I���҂�
wvs20_2:
	in	a,(c)
	jp	m, wvs20_2

	push 	bc
	call	!VSYNC_PROC
	pop	bc
;
	; State2: Vsync�J�n�҂� (2�t���[����)
wvs20_3:
	in	a,(c)
	jp	p, wvs20_3

	; State3: Vsync�I���҂�
wvs20_4:
	in	a,(c)
	jp	m, wvs20_4

	push 	bc
	call	!VSYNC_PROC
	pop	bc
	; State4: Vsync�J�n�҂� (3�t���[����)
wvs20_5:
	in	a,(c)
	jp	p, wvs20_5

	; State5: Vsync�I���҂�
wvs20_6:
	in	a,(c)
	jp	m, wvs20_6

	push af
	call	!VSYNC_PROC
	pop af

	; �J�n������State0�ɖ߂��B
	and		080h
	ld		(vsync_w),a

	xor		a
	ld		(vsync_state),a

	ret

; Vsync���`�F�b�N����State��ύX����B
; VBlank�̃G�b�W���� State��+1����B
check_vsync_state:
	ld	hl, vsync_w
	ld	bc, 1a01h
	in	a,(c)
	and	080h
	cp	(hl)
	ret	z		; �O���VSync��ԂƔ�r�B
;
	ld	(hl),a
	inc	l

	; �قȂ��Ă���� vsync_state��+1����B
	; 0�t���[����(Vsync�O) �� 0
	; 0�t���[����(Vsync��) �� 1
	; 1�t���[����(Vsync�O) �� 2
	; 1�t���[����(Vsync��) �� 3
	; 2�t���[����(Vsync�O) �� 4
	; 2�t���[����(Vsync��) �� 5

	inc	(hl)	; State��+1����B

	CP	080h
	ret	z
	jp !VSYNC_PROC


;//---------------------------------------------------------------;
;//	VSync(�����A������)�̃G�b�W��҂B
;//---------------------------------------------------------------;
wait_vsync:
	ld hl,0000h
	ld de,0000h

	ld bc, 1a01h
edge_1:
	inc hl

	in a,(c)
	jp p,edge_1

edge_2:
	inc de

	in a,(c)
	jp m,edge_2

	ret

;//---------------------------------------------------------------;
;//	VSync�̊J�n��҂B
;//---------------------------------------------------------------;
wait_vsync0:
	ld bc, 1a01h
ill_1:
	in a,(c)
	jp m,ill_1

	ret


; �^�C�}�[�B200h�񃋁[�v�� �� 3.499msec�B
wait_time:
	ld	hl, 0200h
wt_1:
	dec	hl
	ld	a,h
	or	l
	jr	nz, wt_1

	ret


;---------------------------------------------------------------;
; �A�N�Z�X(R/W)VRAM�o���N�� VRAM1�ɐݒ�
;---------------------------------------------------------------;
set_vram1:
	ld		a, CRTC_1FD0_L | 0x10
	ld		bc, 01fd0h
	out		(c),a
	ret

;---------------------------------------------------------------;
; �A�N�Z�X(R/W)VRAM�o���N�� VRAM0�ɐݒ�
;---------------------------------------------------------------;
set_vram0:
	ld		a, CRTC_1FD0_L
	ld		bc, 01fd0h
	out		(c),a
	ret


;---------------------------------------------------------------;
;	END


;---------------------------------------------------------------;
;	Copyright (c) 2019 chara_render.asm
;	This software is released under the MIT License.
;	http://opensource.org/licenses/mit-license.php
;---------------------------------------------------------------;

;---------------------------------------------------------------;
;	�����L�����o�b�t�@ (128x2)
;	�e���т�
;		+0 �^�C���X�^���v�o�b�t�@(����)
;		+1 �^�C���X�^���v�o�b�t�@(���)
;		+2 Y�T�C�Y (�s�N�Z���P��)
;		+3 X�T�C�Y (�L�����P��)
;---------------------------------------------------------------;
align 256

; �L�����������[�N (Page0�p)
clear_char_work0:
	ds	128

; �L�����������[�N (Page1�p)
clear_char_work1:
	ds	128

;---------------------------------------------------------------;
;	�L�����N�^�`�揈���e�[�u�� (32���)
;	�A�h���X��L,H�ŕ����B
;---------------------------------------------------------------;
align 256
render_chara_jump_tbl:
	; drawtype:00 Plane: B sizey: 16
	db	render_b16_y0	& 0ffh	; 0
	db	render_b16_y1	& 0ffh	; 1
	db	render_b16_y2	& 0ffh	; 2
	db	render_b16_y3	& 0ffh	; 3
	db	render_b16_y4	& 0ffh	; 4
	db	render_b16_y5	& 0ffh	; 5
	db	render_b16_y6	& 0ffh	; 6
	db	render_b16_y7	& 0ffh	; 7

	; drawtype:08 Plane: BR sizey: 16
	db	render_br16_y0	& 0ffh	; 0
	db	render_br16_y1	& 0ffh	; 1
	db	render_br16_y2	& 0ffh	; 2
	db	render_br16_y3	& 0ffh	; 3
	db	render_br16_y4	& 0ffh	; 4
	db	render_br16_y5	& 0ffh	; 5
	db	render_br16_y6	& 0ffh	; 6
	db	render_br16_y7	& 0ffh	; 7

	; drawtype:10 Plane: RGB sizey: 16
	db	render_rgb16_y0	& 0ffh	; 0
	db	render_rgb16_y1	& 0ffh	; 1
	db	render_rgb16_y2	& 0ffh	; 2
	db	render_rgb16_y3	& 0ffh	; 3
	db	render_rgb16_y4	& 0ffh	; 4
	db	render_rgb16_y5	& 0ffh	; 5
	db	render_rgb16_y6	& 0ffh	; 6
	db	render_rgb16_y7	& 0ffh	; 7


;---------------------------------------------------------------;
; �����_�����O�e�[�u�� 32���
; �����͏�ʂ̂݁B
align 256
	; drawtype:00 Plane: B sizey: 16
	db	render_b16_y0	>> 8	; 0
	db	render_b16_y1	>> 8	; 1
	db	render_b16_y2	>> 8	; 2
	db	render_b16_y3	>> 8	; 3
	db	render_b16_y4	>> 8	; 4
	db	render_b16_y5	>> 8	; 5
	db	render_b16_y6	>> 8	; 6
	db	render_b16_y7	>> 8	; 7

	; drawtype:08h Plane: BR sizey: 16
	db	render_br16_y0	>> 8	; 0
	db	render_br16_y1	>> 8	; 1
	db	render_br16_y2	>> 8	; 2
	db	render_br16_y3	>> 8	; 3
	db	render_br16_y4	>> 8	; 4
	db	render_br16_y5	>> 8	; 5
	db	render_br16_y6	>> 8	; 6
	db	render_br16_y7	>> 8	; 7

	; drawtype:10h Plane: RGB sizey: 16
	db	render_rgb16_y0	>> 8	; 0
	db	render_rgb16_y1	>> 8	; 1
	db	render_rgb16_y2	>> 8	; 2
	db	render_rgb16_y3	>> 8	; 3
	db	render_rgb16_y4	>> 8	; 4
	db	render_rgb16_y5	>> 8	; 5
	db	render_rgb16_y6	>> 8	; 6
	db	render_rgb16_y7	>> 8	; 7


;---------------------------------------------------------------;
;	�L�����N�^���������e�[�u�� (32���)
;	�A�h���X��L,H�ŕ�����B
;---------------------------------------------------------------;
align 256
clear_chara_jump_tbl:
	;; DrawType: 00h
	db	clear_size16_y0	& 0ffh	; 0
	db	clear_size16_y1	& 0ffh	; 1
	db	clear_size16_y2	& 0ffh	; 2
	db	clear_size16_y3	& 0ffh	; 3
	db	clear_size16_y4	& 0ffh	; 4
	db	clear_size16_y5	& 0ffh	; 5
	db	clear_size16_y6	& 0ffh	; 6
	db	clear_size16_y7	& 0ffh	; 7

	;; DrawType: 08h
	db	clear_size16_y0	& 0ffh	; 0
	db	clear_size16_y1	& 0ffh	; 1
	db	clear_size16_y2	& 0ffh	; 2
	db	clear_size16_y3	& 0ffh	; 3
	db	clear_size16_y4	& 0ffh	; 4
	db	clear_size16_y5	& 0ffh	; 5
	db	clear_size16_y6	& 0ffh	; 6
	db	clear_size16_y7	& 0ffh	; 7

	;; DrawType: 10h
	db	clear_size16_y0	& 0ffh	; 0
	db	clear_size16_y1	& 0ffh	; 1
	db	clear_size16_y2	& 0ffh	; 2
	db	clear_size16_y3	& 0ffh	; 3
	db	clear_size16_y4	& 0ffh	; 4
	db	clear_size16_y5	& 0ffh	; 5
	db	clear_size16_y6	& 0ffh	; 6
	db	clear_size16_y7	& 0ffh	; 7


;---------------------------------------------------------------;
; �����e�[�u�� 32���
; �����͏�ʂ̂݁B
align 256
clear_chara_jump_tbl_h:
	;; DrawType: 00h
	db	clear_size16_y0	>> 8 ; 0
	db	clear_size16_y1	>> 8 ; 1
	db	clear_size16_y2	>> 8 ; 2
	db	clear_size16_y3	>> 8 ; 3
	db	clear_size16_y4	>> 8 ; 4
	db	clear_size16_y5	>> 8 ; 5
	db	clear_size16_y6	>> 8 ; 6
	db	clear_size16_y7	>> 8 ; 7

	;; DrawType: 08h
	db	clear_size16_y0	>> 8 ; 0
	db	clear_size16_y1	>> 8 ; 1
	db	clear_size16_y2	>> 8 ; 2
	db	clear_size16_y3	>> 8 ; 3
	db	clear_size16_y4	>> 8 ; 4
	db	clear_size16_y5	>> 8 ; 5
	db	clear_size16_y6	>> 8 ; 6
	db	clear_size16_y7	>> 8 ; 7

	;; DrawType: 10h
	db	clear_size16_y0	>> 8 ; 0
	db	clear_size16_y1	>> 8 ; 1
	db	clear_size16_y2	>> 8 ; 2
	db	clear_size16_y3	>> 8 ; 3
	db	clear_size16_y4	>> 8 ; 4
	db	clear_size16_y5	>> 8 ; 5
	db	clear_size16_y6	>> 8 ; 6
	db	clear_size16_y7	>> 8 ; 7


;---------------------------------------------------------------;
; �L�����N�^�`�� (�C�ӃT�C�Y,�N���b�s���O�t��)
; ����:
;	DEreg: posx
;	Areg: posy
;	HLreg: image data
;		+0 �N���b�vY��� (200-sizey-1)
;		+1 �`��^�C�v (0: RGB/SizeY:12 010h: B /SizeY:12 )
;		+2 �N���b�v�E��� (40-sizex+1)
;		+3 �N���b�v����� (64-sizex+1)
;		+4 �T�C�YX (byte�P��)
;		+5 �T�C�YY (�s�N�Z���P��)
;---------------------------------------------------------------;
render_chara_image_w:



; VRAM�A�h���X�����߂�B
	; Ypos �N���b�v���
	cp	(hl)
	ret	nc

	inc	hl

	ex	de,hl

	push hl			; Xpos��Push.

	; Y���W����VRAM�A�h���X(Blue)�����߂�B
	; �����ɕ`��y�[�W (00 or 04h) �� OR����B
	ld	l,a
	ld	h,VRAM_ADRS_TBL_H
	ld	b,(hl)
	inc h
	ld	c,(hl)

	; Y���W�ɍ��킹���`�揈�������ȏ������ŃZ�b�g����B
	and	07h
;;	add	a,a
	ld	l,a

	ld	a,(de)	; �`��^�C�v�� or�B
	inc	de
	or	l
	; �L�����폜�p�ɕ`��^�C�v�����ȏ������ʒu�ɏ������ށB
	ld	( draw_type_buff+1 ),a

	; �`�揈��������߂Ď��ȏ���������B
	ld		l,a
	ld		h, render_chara_jump_tbl >> 8

	ld		a,(hl)
	inc		h
	ld		h,(hl)
	ld		l,a

	ld	(image_jump+1),hl

	; �t���b�v�y�[�W��VRAM�A�h���X�ɔ��f����B
	ld	a,(flip_render_w)
	or	b
	ld	b,a

	pop hl

	; PosX/8
	sra h
	rr	l

	srl l
	srl l

	ld	a,l

	add hl,bc

	; HLreg �� VRAM Adrs.(X���������������Ă���)

	ex	de,hl

	cp	(hl)	; �E�N���b�v����
	inc	hl
	jp	c, rciw_2

	cp	40
	jp	c, rciw_3

	cp	(hl)	; ���N���b�v����
	inc	hl
	ret	c

; ���N���b�v����

	; ���[�̃N���b�v�Ȃ̂� BCreg�ɂ�x=0��VRAM�A�h���X�������Ă���B

	xor	03fh	; ���ɃN���b�v�A�E�g������ (64-xpos)
	inc	a
	ld	d,a

	ld	a,(hl)	; X�T�C�Y - �N���b�v�A�E�g��
	inc	hl
	sub	d

	; Areg: ��ʓ��ɂ͂ݏo����(=�`�敝)�𗠃��W�X�^�ցB
	ex af,af'

	ld	e,(hl)	; �s�b�`Y���f�[�^�ɑ����ăX�L�b�v����B
	inc	hl

	ld	a,d
	ld	d,0
rciw_4:
	add	hl,de
	dec	a
	jp nz, rciw_4

	ex	af,af'

	jp	rciw_1

rciw_3:
; �E�N���b�v����
	ld	b,d
	ld	c,e

	ld	d,a
	ld	a,40
	sub	d

	inc	hl
	inc	hl
	inc	hl

	jp	rciw_1

rciw_2:
; ��ʓ��Ȃ̂ŃN���b�v�������s�v�Ȏ�
	ld	b,d
	ld	c,e

	inc	hl		; ���N���b�v�l�̓X�L�b�v

	ld a,(hl)	; ���T�C�Y(8�h�b�g�P��)
	inc hl

	inc hl		; �c�T�C�Y�̓X�L�b�v

	; Areg: ���T�C�Y�̃��[�v

rciw_1:
	; �폜�L�����o�b�t�@�ւ̏�������
	ex	de,hl
del_char_write_w:
	ld	hl,0000

	ld	(hl),c
	inc	l
	ld	(hl),b
	inc	l

draw_type_buff:
	ld	(hl), 00h	; ���ȏ������ŕ`��^�C�v����������
	inc	l

	ld	(hl),a		; X�T�C�Y(�L�����P��)
	inc	l

	; ���ȏ������ŏ����݃A�h���X���X�V����B
	ld	( del_char_write_w+1 ),hl
	ex	de,hl

rciw_5:
	ex af,af'

	push bc

	; ���ȏ��������ŕ`�揈���փW�����v����B
image_jump:
	call 0000h

	pop bc
	inc bc		; X������ +8

	ex af,af'
	dec a
	jp nz, rciw_5

	ret



;---------------------------------------------------------------;
;	�e�L�����N�^�̕`��
;---------------------------------------------------------------;
draw_chara_manager:

	di

	; �����o�b�t�@���g���đO�񏑂������������B
	call	update_clear_buff_w

	ld		iy, chara_work
	ld		b, CHARA_NUM
cmd_1:
	ld		l,(iy+CHR_PATTERN)

	; �L�����p�^�[����0���ǂ�������
	; ���ŉ��ʃr�b�g��\��/��\���t���O�ɂ���(0�ŕ\���A1�Ŕ�\��)
	bit		0,l
	; inc		l
	; dec		l
	jp		nz,cmd_2
;
	; �L�����f�[�^��DEreg�Ɏ擾����B

	push	bc

	inc		l

	ld		h, chara_pivot_table >> 8

	; Ypos+PivotY���v�Z����A'reg�ɕێ��B
	ld		a, (iy+CHR_POSYH)
	add		a, (hl)
	ex		af,af'

	dec		l

	; X���W�͏��9bit��������,����7bit���������ƂȂ��Ă���B
	; 1bit�V�t�g���Đ����������߂�B

	; Xpos(���8bit)��PivotX�𑫂��B(�䂦��PivotX��2�̔{���P��)
	ld		a, (iy+CHR_POSXH)
	add		a, (hl)
	ld		c,a

;;	dec		l			; Index��߂��B

	ld		a, (iy+CHR_POSXL)
	rlca	; 7bit�ڂ�Cy�ɓ����B
	rl		c
	ld		b,00h	; 7
	rl		b		; 8

	; BCreg: Xpos

	; X�����̃I�t�Z�b�g(0-7)�ƃL�����p�^�[���ɑΉ������f�[�^�A�h���X���擾����B
	ld		a, 07h
	and		c
	add		a, chara_data_table >> 8
	ld		h,a

	ld		e,(hl)
	inc		l
	ld		d,(hl)

	; DEreg: �L�����N�^�f�[�^
	ex		de,hl

	ld		e,c
	ld		d,b

	; Ypos+PivotY�𕜋A�B
	ex		af,af'

	call	render_chara_image_w

	call	check_vsync_state

cmd_3:
	pop		bc

cmd_2:
	ld		de, CHR_SIZE
	add		iy,de

	djnz	cmd_1

	; ����̍폜�o�b�t�@�������߂�B
	call	calc_del_char_num

	ei

	ret


;----
;	END

;---------------------------------------------------------------;
;	Copyright (c) 2019 clear_buff.asm
;	This software is released under the MIT License.
;	http://opensource.org/licenses/mit-license.php
;---------------------------------------------------------------;

; Clear Buffer.

init_clear_char_work:
	ld	a,(flip_w)
	jp	setup_clear_char_work


; �����o�b�t�@�������߂�B
calc_del_char_num:
	ld	a, ( flip_render_w )
	or	del_char_num_w & 0ffh
	ld	l,a
	ld	h, del_char_num_w >> 8

	; �����o�b�t�@�̉���8bit�������݃A�h���X��\���Ă���̂ŁA
	; �����4�Ŋ������l�����ƂȂ�B
	ld	a, ( del_char_write_w+1 )
	and	07ch
	RRCA
	RRCA

	ld	(hl),a

	ret


; �����o�b�t�@�ɓo�^���Ă���VRAM���`�F�b�N���ăN���A����B
; BitLine�o�[�W����
update_clear_buff_w:
	; �O��̍폜�L�����o�b�t�@�����Z�o����B
	ld	a, ( flip_render_w )
	or	del_char_num_w & 0ffh
	ld	l,a
	ld	h, del_char_num_w >> 8

	; ���������Ή������Ȃ��B
	ld	a,(hl)
	or	a
	ret	z

	ld ixl,a	; ����IXLreg�ցB

	; �����A�N�Z�X���[�h�֕ύX
	di
	ld bc, 01a03h
	ld de, 00b0ah	; PortC5 �� 1��0�ɂ���B
	out (c),d
	out (c),e

	; �����L�������[�N: ���ȏ�����
del_char_read_w:
	ld	hl, 0000h

ucbw_5:
	; ����VRAM�A�h���X
	ld	c,(hl)
	inc l

	ld	a,(hl)
	and	03fh	; �����A�N�Z�X���[�h(RGB)��VRAM�A�h���X(0000h�`03fffh)�ցB
	ld	b,a
	inc l

	; �`��^�C�v (PosY,SizeY���݂̃f�[�^)
	ld	a,(hl)	;
	inc	l

	ex	de,hl

	ld		l,a
	ld		h, clear_chara_jump_tbl >> 8

	; �W�����v�e�[�u��������Ȃ��Ȃ������߁A
	; �`��^�C�v��00-1ff�܂Ŋg������B

	ld		a,(hl)
	inc		h
	ld		h,(hl)
	ld		l,a
	ld		( ucbw_1+1 ),hl

	ld	a,(de)		; X�T�C�Y (�L�����P��)
	inc	e

	; �����p�f�[�^(���i���Z�p/VRAM�ɏ����ޒl)
	ld hl,0800h

	push	de

ucbw_4:
	ex	af,af'

	push	bc
ucbw_1:
	; ��������(���ȏ�����)
	call	0000h

	; X+
	pop	bc
	inc	bc

	ex	af,af'
	dec	a
	jp	nz, ucbw_4

	pop	hl

	dec ixl
	jp	nz, ucbw_5

	; �����A�N�Z�X���[�h�������B
	ld a,040h
	in a,(c)		; 040**h����in

	ei

	ret

;----
;	END

;---------------------------------------------------------------;
;	Copyright (c) 2019 render.asm
;	This software is released under the MIT License.
;	http://opensource.org/licenses/mit-license.php
;---------------------------------------------------------------;

;---------------------------------------------------------------;
; BRG 1���C���`��
; ����
;	HLreg: �L�����f�[�^
;	BCreg: �`��VRAM�A�h���X
;	Ereg: �r�b�g���C���f�[�^
;---------------------------------------------------------------;
rc_image_01:
	ld	d,b		; Dreg��Breg���o�b�t�@�B

	ld	a,b
	and	07h
	add	a, BITLINE_BUFFER0 >> 8
	ld	b,a

	ld	a,(bc)
	and	e
	jp	nz, rc_blend_01

	ld	a,(bc)
	or	e
	ld	(bc),a

	ld	b,d		; Breg�ɕ��A�B

; wirte
	ld d, 040h

	inc b
	ld a,b

	OUT_BRG_HL_ADD_D

	ret

rc_blend_01:
	ld	a,(bc)
	or	e
	ld	(bc),a

	ld	b,d		; Breg�ɕ��A�B
	ld	d, 40h

	jp	brg_blend_01

;---------------------------------------------------------------;
; BRG 2���C���`��
; ����
;	HLreg: �L�����f�[�^
;	BCreg: �`��VRAM�A�h���X
;	Ereg: �r�b�g���C���f�[�^
;---------------------------------------------------------------;
rc_image_02:
	ld	d,b		; Dreg��Breg���o�b�t�@�B

	ld	a,b
	and	07h
	add	a, BITLINE_BUFFER0 >> 8
	ld	b,a

	ld	a,(bc)
	and	e
	jp	nz, rc_blend_02

	ld	a,(bc)
	or	e
	ld	(bc),a

	ld	b,d		; Breg�ɕ��A�B

; wirte
	ld de, 04088h

	inc b
	ld a,b

	jp	brg_write_02

rc_blend_02:
	ld	a,(bc)
	or	e
	ld	(bc),a

	ld	b,d		; Breg�ɕ��A�B
	ld	d, 40h

	jp	brg_blend_02


;---------------------------------------------------------------;
; 3���C���`��
; ����
;	HLreg: �L�����f�[�^
;	BCreg: �`��VRAM�A�h���X
;	Ereg: �r�b�g���C���f�[�^
;---------------------------------------------------------------;
rc_image_03:

	ld	d,b		; Dreg��Breg���o�b�t�@�B

	ld	a,b
	and	07h
	add	a, BITLINE_BUFFER0 >> 8
	ld	b,a

	ld	a,(bc)
	and	e
	jp	nz, rc_blend_03

	ld	a,(bc)
	or	e
	ld	(bc),a

	ld	b,d		; Breg�ɕ��A�B

; wirte
	ld de, 04088h

	inc b
	ld a,b

	jp	brg_write_03

rc_blend_03:
	ld	a,(bc)
	or	e
	ld	(bc),a

	ld	b,d		; Breg�ɕ��A�B
	ld	d, 40h

	jp	brg_blend_03


;---------------------------------------------------------------;
; 4���C���`��
; ����
;	HLreg: �L�����f�[�^
;	BCreg: �`��VRAM�A�h���X
;	Ereg: �r�b�g���C���f�[�^
;---------------------------------------------------------------;
rc_image_04:
	ld	d,b		; Dreg��Breg���o�b�t�@�B

	ld	a,b
	and	07h
	add	a, BITLINE_BUFFER0 >> 8
	ld	b,a

	ld	a,(bc)
	and	e
	jp	nz, rc_blend_04

	ld	a,(bc)
	or	e
	ld	(bc),a

	ld	b,d		; Breg�ɕ��A�B

i04_write_1:

; wirte
	ld	de, 04088h
	inc b
	ld a,b

	jp	brg_write_04

rc_blend_04:
	ld	a,(bc)
	or	e
	ld	(bc),a

	ld	b,d		; Breg�ɕ��A�B
	ld	d, 40h

	jp	brg_blend_04


;---------------------------------------------------------------;
; 5���C���`��
; ����
;	HLreg: �L�����f�[�^
;	BCreg: �`��VRAM�A�h���X
;	Ereg: �r�b�g���C���f�[�^
;---------------------------------------------------------------;
rc_image_05:
	ld	d,b		; Dreg��Breg���o�b�t�@�B

	ld	a,b
	and	07h
	add	a, BITLINE_BUFFER0 >> 8
	ld	b,a

	ld	a,(bc)
	and	e
	jp	nz, rc_blend_05

	ld	a,(bc)
	or	e
	ld	(bc),a

	ld	b,d		; Breg�ɕ��A�B

; wirte
	ld de, 04088h

	inc b
	ld a,b

	jp	brg_write_05


rc_blend_05:
	ld	a,(bc)
	or	e
	ld	(bc),a

	ld	b,d		; Breg�ɕ��A�B
	ld	d, 40h

	jp	brg_blend_05

;---------------------------------------------------------------;
; 6���C���`��
; ����
;	HLreg: �L�����f�[�^
;	BCreg: �`��VRAM�A�h���X
;	Ereg: �r�b�g���C���f�[�^
;---------------------------------------------------------------;
rc_image_06:
	ld	d,b		; Dreg��Breg���o�b�t�@�B

	ld	a,b
	and	07h
	add	a, BITLINE_BUFFER0 >> 8
	ld	b,a

	ld	a,(bc)
	and	e
	jp	nz, rc_blend_06

	ld	a,(bc)
	or	e
	ld	(bc),a

	ld	b,d		; Breg�ɕ��A�B

; wirte
	ld de, 04088h

	inc b
	ld a,b

	jp	brg_write_06

rc_blend_06:
	ld	a,(bc)
	or	e
	ld	(bc),a

	ld	b,d		; Breg�ɕ��A�B
	ld	d, 40h

	jp	brg_blend_06


;---------------------------------------------------------------;
; 7���C���`��
; ����
;	HLreg: �L�����f�[�^
;	BCreg: �`��VRAM�A�h���X
;	Ereg: �r�b�g���C���f�[�^
;---------------------------------------------------------------;
rc_image_07:
	ld	d,b		; Dreg��Breg���o�b�t�@�B

	ld	a,b
	and	07h
	add	a, BITLINE_BUFFER0 >> 8
	ld	b,a

	ld	a,(bc)
	and	e
	jp	nz, rc_blend_07

	ld	a,(bc)
	or	e
	ld	(bc),a

	ld	b,d		; Breg�ɕ��A�B

; wirte
	ld de, 04088h

	inc b
	ld a,b

	jp	brg_write_07

rc_blend_07:
	ld	a,(bc)
	or	e
	ld	(bc),a

	ld	b,d		; Breg�ɕ��A�B
	ld	d, 40h

	jp	brg_blend_07


;---------------------------------------------------------------;
; 8���C���`��
; ����
;	HLreg: �L�����f�[�^
;	BCreg: �`��VRAM�A�h���X
;---------------------------------------------------------------;
rc_image_08:
	; VRAM Adrs(BCreg)����BitLineBuff�����߂�B
	; BitLineBuff�� 0f8xx�ɂ���̂ŁAf800�� OR����Ƌ��܂�B

; Ereg: �r�b�g���C���f�[�^

	ld	d,b		; Dreg��Breg���o�b�t�@�B

	ld	a,b
	and	07h
	add	a, BITLINE_BUFFER0 >> 8
	ld	b,a

	ld	a,(bc)
	or	a
	jp	nz, rc_blend_08

	ld	a,0ffh
	ld	(bc),a

	ld	b,d		; Breg�ɕ��A�B


rc_image_08_n:

; wirte

	ld	de, 04088h
	inc b
	ld a,b

	OUT_BRG_HL_ADD_D
	ADD_B_E
brg_write_07:
	OUT_BRG_HL_ADD_D
	ADD_B_E
brg_write_06:
	OUT_BRG_HL_ADD_D
	ADD_B_E
brg_write_05:
	OUT_BRG_HL_ADD_D
	ADD_B_E
brg_write_04:
	OUT_BRG_HL_ADD_D
	ADD_B_E
brg_write_03:
	OUT_BRG_HL_ADD_D
	ADD_B_E
brg_write_02:
	OUT_BRG_HL_ADD_D
	ADD_B_E
brg_write_01:
	OUT_BRG_HL_ADD_D

	ret


rc_blend_08:
	ld	a,0ffh
	ld	(bc),a

	ld	b,d		; Breg�ɕ��A�B

	ld	d, 40h

	BLEND_RGB_HL_ADD_B_D
	ADD_B_88
brg_blend_07:
	BLEND_RGB_HL_ADD_B_D
	ADD_B_88
brg_blend_06:
	BLEND_RGB_HL_ADD_B_D
	ADD_B_88
brg_blend_05:
	BLEND_RGB_HL_ADD_B_D
	ADD_B_88
brg_blend_04:
	BLEND_RGB_HL_ADD_B_D
	ADD_B_88
brg_blend_03:
	BLEND_RGB_HL_ADD_B_D
	ADD_B_88
brg_blend_02:
	BLEND_RGB_HL_ADD_B_D
	ADD_B_88
brg_blend_01:
	BLEND_RGB_HL_ADD_B_D

	ret


;---------------------------------------------------------------;
; �v���[�� B 1���C���`��
; ����
;	HLreg: �L�����f�[�^
;	BCreg: �`��VRAM�A�h���X
;	Ereg: �r�b�g���C���f�[�^
;---------------------------------------------------------------;
rc_image_b1:
	ld	d,b		; Dreg��Breg���o�b�t�@�B

	ld	a,b
	and	07h
	add	a, BITLINE_BUFFER0 >> 8
	ld	b,a

	ld	a,(bc)
	and	e
	jp	nz, rc_blend_b1

	ld	a,(bc)
	or	e
	ld	(bc),a

	ld	b,d		; Breg�ɕ��A�B

; wirte
	inc b
;;	ld a,b

	OUT_B_HL
	ADD_B_80	; �������̂��� B��G

	ret

rc_blend_b1:
	ld	a,(bc)
	or	e
	ld	(bc),a

	ld	b,d		; Breg�ɕ��A�B
	ld	d, 040h

	BLEND_B_HL_ADD_B_D

	ret

;---------------------------------------------------------------;
; �v���[�� B 2���C���`��
; ����
;	HLreg: �L�����f�[�^
;	BCreg: �`��VRAM�A�h���X
;	Ereg: �r�b�g���C���f�[�^
;---------------------------------------------------------------;
rc_image_b2:
	ld	d,b		; Dreg��Breg���o�b�t�@�B

	ld	a,b
	and	07h
	add	a, BITLINE_BUFFER0 >> 8
	ld	b,a

	ld	a,(bc)
	and	e
	jp	nz, rc_blend_b2

	ld	a,(bc)
	or	e
	ld	(bc),a

	ld	b,d		; Breg�ɕ��A�B

; wirte
	ld	e, 008h

	inc b
	ld a,b

	OUT_B_HL_ADD_E
	OUT_B_HL

	ADD_B_80	; �������̂��� B��G

	ret

rc_blend_b2:
	ld	a,(bc)
	or	e
	ld	(bc),a

	ld	b,d		; Breg�ɕ��A�B
	ld	d, 40h

	jp	b_blend_02

;---------------------------------------------------------------;
; �v���[�� B 3���C���`��
; ����
;	HLreg: �L�����f�[�^
;	BCreg: �`��VRAM�A�h���X
;	Ereg: �r�b�g���C���f�[�^
;---------------------------------------------------------------;
rc_image_b3:
	ld	d,b		; Dreg��Breg���o�b�t�@�B

	ld	a,b
	and	07h
	add	a, BITLINE_BUFFER0 >> 8
	ld	b,a

	ld	a,(bc)
	and	e
	jp	nz, rc_blend_b3

	ld	a,(bc)
	or	e
	ld	(bc),a

	ld	b,d		; Breg�ɕ��A�B

; wirte
	ld	e, 008h

	inc b
	ld a,b

	OUT_B_HL_ADD_E
	OUT_B_HL_ADD_E
	OUT_B_HL

	ADD_B_80	; �������̂��� B��G

	ret

rc_blend_b3:
	ld	a,(bc)
	or	e
	ld	(bc),a

	ld	b,d		; Breg�ɕ��A�B
	ld	d, 40h

	jp	b_blend_03


;---------------------------------------------------------------;
; �v���[�� B 4���C���`��
; ����
;	HLreg: �L�����f�[�^
;	BCreg: �`��VRAM�A�h���X
;	Ereg: �r�b�g���C���f�[�^
;---------------------------------------------------------------;
rc_image_b4:
	ld	d,b		; Dreg��Breg���o�b�t�@�B

	ld	a,b
	and	07h
	add	a, BITLINE_BUFFER0 >> 8
	ld	b,a

	ld	a,(bc)
	and	e
	jp	nz, rc_blend_b4

	ld	a,(bc)
	or	e
	ld	(bc),a

	ld	b,d		; Breg�ɕ��A�B

; wirte
	ld	e, 008h

	inc b
	ld a,b

;	dec	b
;	ld	a,08h*3+080h
;	add	a,b
;	ld	b,a
;	ret

	OUT_B_HL_ADD_E
	OUT_B_HL_ADD_E
	OUT_B_HL_ADD_E
	OUT_B_HL

	ADD_B_80	; �������̂��� B��G

	ret

rc_blend_b4:
	ld	a,(bc)
	or	e
	ld	(bc),a

	ld	b,d		; Breg�ɕ��A�B
	ld	d, 40h

	jp	b_blend_04

;---------------------------------------------------------------;
; �v���[�� B 5���C���`��
; ����
;	HLreg: �L�����f�[�^
;	BCreg: �`��VRAM�A�h���X
;	Ereg: �r�b�g���C���f�[�^
;---------------------------------------------------------------;
rc_image_b5:
	ld	d,b		; Dreg��Breg���o�b�t�@�B

	ld	a,b
	and	07h
	add	a, BITLINE_BUFFER0 >> 8
	ld	b,a

	ld	a,(bc)
	and	e
	jp	nz, rc_blend_b5

	ld	a,(bc)
	or	e
	ld	(bc),a

	ld	b,d		; Breg�ɕ��A�B

; wirte
	ld	e, 008h

	inc b
	ld a,b

	OUT_B_HL_ADD_E
	OUT_B_HL_ADD_E
	OUT_B_HL_ADD_E
	OUT_B_HL_ADD_E
	OUT_B_HL

	ADD_B_80	; �������̂��� B��G

	ret

rc_blend_b5:
	ld	a,(bc)
	or	e
	ld	(bc),a

	ld	b,d		; Breg�ɕ��A�B
	ld	d, 40h

	jp	b_blend_05

;---------------------------------------------------------------;
; �v���[�� B 6���C���`��
; ����
;	HLreg: �L�����f�[�^
;	BCreg: �`��VRAM�A�h���X
;	Ereg: �r�b�g���C���f�[�^
;---------------------------------------------------------------;
rc_image_b6:
	ld	d,b		; Dreg��Breg���o�b�t�@�B

	ld	a,b
	and	07h
	add	a, BITLINE_BUFFER0 >> 8
	ld	b,a

	ld	a,(bc)
	and	e
	jp	nz, rc_blend_b6

	ld	a,(bc)
	or	e
	ld	(bc),a

	ld	b,d		; Breg�ɕ��A�B

; wirte
	ld	e, 008h

	inc b
	ld a,b

	OUT_B_HL_ADD_E
	OUT_B_HL_ADD_E
	OUT_B_HL_ADD_E
	OUT_B_HL_ADD_E
	OUT_B_HL_ADD_E
	OUT_B_HL

	ADD_B_80	; �������̂��� B��G

	ret

rc_blend_b6:
	ld	a,(bc)
	or	e
	ld	(bc),a

	ld	b,d		; Breg�ɕ��A�B
	ld	d, 40h

	jp	b_blend_06

;---------------------------------------------------------------;
; �v���[�� B 7���C���`��
; ����
;	HLreg: �L�����f�[�^
;	BCreg: �`��VRAM�A�h���X
;	Ereg: �r�b�g���C���f�[�^
;---------------------------------------------------------------;
rc_image_b7:
	ld	d,b		; Dreg��Breg���o�b�t�@�B

	ld	a,b
	and	07h
	add	a, BITLINE_BUFFER0 >> 8
	ld	b,a

	ld	a,(bc)
	and	e
	jp	nz, rc_blend_b7

	ld	a,(bc)
	or	e
	ld	(bc),a

	ld	b,d		; Breg�ɕ��A�B

; wirte
	ld	e, 008h

	inc b
	ld a,b

	OUT_B_HL_ADD_E
	OUT_B_HL_ADD_E
	OUT_B_HL_ADD_E
	OUT_B_HL_ADD_E
	OUT_B_HL_ADD_E
	OUT_B_HL_ADD_E
	OUT_B_HL

	ADD_B_80	; �������̂��� B��G

	ret

rc_blend_b7:
	ld	a,(bc)
	or	e
	ld	(bc),a

	ld	b,d		; Breg�ɕ��A�B
	ld	d, 40h

	jp	b_blend_07


;---------------------------------------------------------------;
; �v���[��: B 8���C���`��
; ����
;	HLreg: �L�����f�[�^
;	BCreg: �`��VRAM�A�h���X
;---------------------------------------------------------------;
rc_image_b8:
	; VRAM Adrs(BCreg)����BitLineBuff�����߂�B
	; BitLineBuff�� 0f8xx�ɂ���̂ŁAf800�� OR����Ƌ��܂�B

; Ereg: �r�b�g���C���f�[�^

	ld	d,b		; Dreg��Breg���o�b�t�@�B

	ld	a,b
	and	07h
	add	a, BITLINE_BUFFER0 >> 8
	ld	b,a

	ld	a,(bc)
	or	a
	jp	nz,	rc_blend_b8

	ld	a,0ffh
	ld	(bc),a

	ld	b,d		; Breg�ɕ��A�B

rc_image_b08_n:
; wirte
	ld	e, 008h

	inc b
	ld a,b

;	dec	b
;	ld	a,08h*7+80h
;	add	a,b
;	ld	b,a
;	ret


	OUT_B_HL_ADD_E
	OUT_B_HL_ADD_E
	OUT_B_HL_ADD_E
	OUT_B_HL_ADD_E
	OUT_B_HL_ADD_E
	OUT_B_HL_ADD_E
	OUT_B_HL_ADD_E
	OUT_B_HL

	ADD_B_80	; �������̂��� B��G

	ret

rc_blend_b8:
	ld	a,0ffh
	ld	(bc),a

	ld	b,d		; Breg�ɕ��A�B
	ld	d, 40h

b_blend_08:
	BLEND_B_HL_ADD_B_D
	ADD_B_88
b_blend_07:
	BLEND_B_HL_ADD_B_D
	ADD_B_88
b_blend_06:
	BLEND_B_HL_ADD_B_D
	ADD_B_88
b_blend_05:
	BLEND_B_HL_ADD_B_D
	ADD_B_88
b_blend_04:
	BLEND_B_HL_ADD_B_D
	ADD_B_88
b_blend_03:
	BLEND_B_HL_ADD_B_D
	ADD_B_88
b_blend_02:
	BLEND_B_HL_ADD_B_D
	ADD_B_88
b_blend_01:
	BLEND_B_HL_ADD_B_D

	ret



;---------------------------------------------------------------;
; 1���C������
; ����
;	BCreg: �`��VRAM�A�h���X
;	Ereg: BitLine�f�[�^ ���Z�b�g�p�� �w��r�b�g�𔽓]�������́B
;	Hreg: ���i���Z�p 08h
;	Lreg: VRAM�ւ̏o�͒l 00h
;---------------------------------------------------------------;
clear_image_01:
	; VRAM Adrs(BCreg)����BitLineBuff�����߂�B
	; BitLineBuff�� 0f8xx�ɂ���̂ŁAf800�� OR����Ƌ��܂�B

; Ereg: �r�b�g���C���f�[�^

	ld	d,b		; Dreg��Breg���o�b�t�@�B

	; ��������K�v�����邩�ǂ���BitLine�o�b�t�@���`�F�b�N�B
	ld	a,b
	and	07h
	add	a, BITLINE_BUFFER0 >> 8
	ld	b,a

	ld	a,(bc)
	and	e
	ld	(bc),a

	ld	b,d		; Breg�ɕ��A�B

	out	(c),l		; 1

	ret

;---------------------------------------------------------------;
; 2���C������
; ����
;	BCreg: �`��VRAM�A�h���X
;	Ereg: BitLine�f�[�^ ���Z�b�g�p�� �w��r�b�g�𔽓]�������́B
;	Hreg: ���i���Z�p 08h
;	Lreg: VRAM�ւ̏o�͒l 00h
;---------------------------------------------------------------;
clear_image_02:
	; VRAM Adrs(BCreg)����BitLineBuff�����߂�B
	; BitLineBuff�� 0f8xx�ɂ���̂ŁAf800�� OR����Ƌ��܂�B

; Ereg: �r�b�g���C���f�[�^

	ld	d,b		; Dreg��Breg���o�b�t�@�B

	; ��������K�v�����邩�ǂ���BitLine�o�b�t�@���`�F�b�N�B
	ld	a,b
	and	07h
	add	a, BITLINE_BUFFER0 >> 8
	ld	b,a

	; BitLine�o�b�t�@�Ƀ}�X�N������0���������ށB
	ld	a,(bc)
	and	e
	ld	(bc),a

	ld	b,d		; Breg�ɕ��A�B
	ld	a,b		; VRAM�v�Z�p��Areg�ɂ�Breg�����Ă����B

	; ���Z�p�ɐݒ�
	ld	h,08h

	OUT_L_ADD_H		; 0
	out	(c),l		; 1

	ret

;---------------------------------------------------------------;
; 3���C������
; ����
;	BCreg: �`��VRAM�A�h���X
;	Ereg: BitLine�f�[�^ ���Z�b�g�p�� �w��r�b�g�𔽓]�������́B
;	Hreg: ���i���Z�p 08h
;	Lreg: VRAM�ւ̏o�͒l 00h
;---------------------------------------------------------------;
clear_image_03:
	; VRAM Adrs(BCreg)����BitLineBuff�����߂�B
	; BitLineBuff�� 0f8xx�ɂ���̂ŁAf800�� OR����Ƌ��܂�B

; Ereg: �r�b�g���C���f�[�^

	ld	d,b		; Dreg��Breg���o�b�t�@�B

	; ��������K�v�����邩�ǂ���BitLine�o�b�t�@���`�F�b�N�B
	ld	a,b
	and	07h
	add	a, BITLINE_BUFFER0 >> 8
	ld	b,a

	; BitLine�o�b�t�@�Ƀ}�X�N������0���������ށB
	ld	a,(bc)
	and	e
	ld	(bc),a

	ld	b,d		; Breg�ɕ��A�B
	ld	a,b		; VRAM�v�Z�p��Areg�ɂ�Breg�����Ă����B

	; ���Z�p�ɐݒ�
	ld	h,08h

	OUT_L_ADD_H		; 0
	OUT_L_ADD_H		; 1
	out	(c),l		; 2

	ret

;---------------------------------------------------------------;
; 4���C������
; ����
;	BCreg: �`��VRAM�A�h���X
;	Dreg: BitLine�f�[�^
;	Ereg: BitLine�}�X�N�f�[�^ (Dreg �𔽓]��������)
;	Hreg: ���i���Z�p 08h
;	Lreg: VRAM�ւ̏o�͒l 00h
; Hreg��j�󂷂�ꍇ������B
;---------------------------------------------------------------;
clear_image_04:
	; VRAM Adrs(BCreg)����BitLineBuff�����߂�B
	; BitLineBuff�� 0f8xx�ɂ���̂ŁAf800�� OR����Ƌ��܂�B

; Ereg: �r�b�g���C���f�[�^

	ld	h,b		; Hreg��Breg���o�b�t�@�B

	; ��������K�v�����邩�ǂ���BitLine�o�b�t�@���`�F�b�N�B
	ld	a,b
	and	07h
	add	a, BITLINE_BUFFER0 >> 8
	ld	b,a

	ld	a,(bc)
	and	d
	ret	z

	; BitLine�o�b�t�@�Ƀ}�X�N������0���������ށB
	ld	a,(bc)
	and	e
	ld	(bc),a

	ld	b,h		; Breg�ɕ��A�B
	ld	a,b		; VRAM�v�Z�p��Areg�ɂ�Breg�����Ă����B

	; ���Z�p�ɐݒ�
	ld	h,08h

	OUT_L_ADD_H		; 0
	OUT_L_ADD_H		; 1
	OUT_L_ADD_H		; 2
	out	(c),l		; 3

	ret

;---------------------------------------------------------------;
; 5���C������
; ����
;	BCreg: �`��VRAM�A�h���X
;	Dreg: BitLine�f�[�^
;	Ereg: BitLine�}�X�N�f�[�^ (Dreg �𔽓]��������)
;	Hreg: ���i���Z�p 08h
;	Lreg: VRAM�ւ̏o�͒l 00h
; Hreg��j�󂷂�ꍇ������B
;---------------------------------------------------------------;
clear_image_05:
	; VRAM Adrs(BCreg)����BitLineBuff�����߂�B
	; BitLineBuff�� 0f8xx�ɂ���̂ŁAf800�� OR����Ƌ��܂�B

; Ereg: �r�b�g���C���f�[�^

	ld	h,b		; Hreg��Breg���o�b�t�@�B

	; ��������K�v�����邩�ǂ���BitLine�o�b�t�@���`�F�b�N�B
	ld	a,b
	and	07h
	add	a, BITLINE_BUFFER0 >> 8
	ld	b,a

	ld	a,(bc)
	and	d
	ret	z

	; BitLine�o�b�t�@�Ƀ}�X�N������0���������ށB
	ld	a,(bc)
	and	e
	ld	(bc),a

	ld	b,h		; Breg�ɕ��A�B
	ld	a,b		; VRAM�v�Z�p��Areg�ɂ�Breg�����Ă����B

	; ���Z�p�ɐݒ�
	ld	h,08h

	OUT_L_ADD_H		; 0
	OUT_L_ADD_H		; 1
	OUT_L_ADD_H		; 2
	OUT_L_ADD_H		; 3
	out	(c),l		; 4

	ret

;---------------------------------------------------------------;
; 6���C������
; ����
;	BCreg: �`��VRAM�A�h���X
;	Dreg: BitLine�f�[�^
;	Ereg: BitLine�}�X�N�f�[�^ (Dreg �𔽓]��������)
;	Hreg: ���i���Z�p 08h
;	Lreg: VRAM�ւ̏o�͒l 00h
; Hreg��j�󂷂�ꍇ������B
;---------------------------------------------------------------;
clear_image_06:
	; VRAM Adrs(BCreg)����BitLineBuff�����߂�B
	; BitLineBuff�� 0f8xx�ɂ���̂ŁAf800�� OR����Ƌ��܂�B

; Ereg: �r�b�g���C���f�[�^

	ld	h,b		; Hreg��Breg���o�b�t�@�B

	; ��������K�v�����邩�ǂ���BitLine�o�b�t�@���`�F�b�N�B
	ld	a,b
	and	07h
	add	a, BITLINE_BUFFER0 >> 8
	ld	b,a

	ld	a,(bc)
	and	d
	ret	z

	; BitLine�o�b�t�@�Ƀ}�X�N������0���������ށB
	ld	a,(bc)
	and	e
	ld	(bc),a

	ld	b,h		; Breg�ɕ��A�B
	ld	a,b		; VRAM�v�Z�p��Areg�ɂ�Breg�����Ă����B

	; ���Z�p�ɐݒ�
	ld	h,08h

	OUT_L_ADD_H		; 0
	OUT_L_ADD_H		; 1
	OUT_L_ADD_H		; 2
	OUT_L_ADD_H		; 3
	OUT_L_ADD_H		; 4
	out	(c),l		; 5

	ret


;---------------------------------------------------------------;
; 7���C������
; ����
;	BCreg: �`��VRAM�A�h���X
;	Dreg: BitLine�f�[�^
;	Ereg: BitLine�}�X�N�f�[�^ (Dreg �𔽓]��������)
;	Hreg: ���i���Z�p 08h
;	Lreg: VRAM�ւ̏o�͒l 00h
; Hreg��j�󂷂�ꍇ������B
;---------------------------------------------------------------;
clear_image_07:
	; VRAM Adrs(BCreg)����BitLineBuff�����߂�B
	; BitLineBuff�� 0f8xx�ɂ���̂ŁAf800�� OR����Ƌ��܂�B

; Ereg: �r�b�g���C���f�[�^

	ld	h,b		; Hreg��Breg���o�b�t�@�B

	; ��������K�v�����邩�ǂ���BitLine�o�b�t�@���`�F�b�N�B
	ld	a,b
	and	07h
	add	a, BITLINE_BUFFER0 >> 8
	ld	b,a

	ld	a,(bc)
	and	d
	ret	z

	; BitLine�o�b�t�@�Ƀ}�X�N������0���������ށB
	ld	a,(bc)
	and	e
	ld	(bc),a

	ld	b,h		; Breg�ɕ��A�B
	ld	a,b		; VRAM�v�Z�p��Areg�ɂ�Breg�����Ă����B

	; ���Z�p�ɐݒ�
	ld	h,08h

	OUT_L_ADD_H		; 0
	OUT_L_ADD_H		; 1
	OUT_L_ADD_H		; 2
	OUT_L_ADD_H		; 3
	OUT_L_ADD_H		; 4
	OUT_L_ADD_H		; 5
	out	(c),l		; 6

	ret

;---------------------------------------------------------------;
; 8���C������
; ����
;	BCreg: �`��VRAM�A�h���X
;	Hreg: ���i���Z�p 08h
;	Lreg: VRAM�ւ̏o�͒l 00h
;---------------------------------------------------------------;
clear_image_08:
	; VRAM Adrs(BCreg)����BitLineBuff�����߂�B
	; BitLineBuff�� 0f8xx�ɂ���̂ŁAf800�� OR����Ƌ��܂�B

; Ereg: �r�b�g���C���f�[�^

	ld	d,b		; Dreg��Breg���o�b�t�@�B

	; ��������K�v�����邩�ǂ���BitLine�o�b�t�@���`�F�b�N�B
	ld	a,b
	and	07h
	add	a, BITLINE_BUFFER0 >> 8
	ld	b,a

	ld	a,(bc)
	or	a
	ret	z

	; BitLine�o�b�t�@�ɏ����ς݂�0���������ށB
	xor	a
	ld	(bc),a

	ld	b,d		; Breg�ɕ��A�B
	ld	a,b		; VRAM�v�Z�p��Areg�ɂ�Breg�����Ă����B

	; ���Z�p�ɐݒ�
	ld	h,08h

	OUT_L_ADD_H		; 0
	OUT_L_ADD_H		; 1
	OUT_L_ADD_H		; 2
	OUT_L_ADD_H		; 3
	OUT_L_ADD_H		; 4
	OUT_L_ADD_H		; 5
	OUT_L_ADD_H		; 6
	out	(c),l		; 7

	ret



;---------------------------------------------------------------;
; RG 1���C���`��
; ����
;	HLreg: �L�����f�[�^
;	BCreg: �`��VRAM�A�h���X
;	Ereg: �r�b�g���C���f�[�^
;---------------------------------------------------------------;
rc_image_rg_01:
	ld	d,b		; Dreg��Breg���o�b�t�@�B

	ld	a,b
	and	07h
	add	a, BITLINE_BUFFER0 >> 8
	ld	b,a

	ld	a,(bc)
	and	e
	jp	nz, rc_blend_rg_01

	ld	a,(bc)	; BitLine�Ƀt���O�𗧂Ă�B
	or	e
	ld	(bc),a

	ld	b,d		; Breg�ɕ��A�B

; wirte
	ld	de, 040c8h	; Ereg��-40+8�̒l�B

	; OUTI�p��+1���Ă����B
	inc b

	; Blue�v���[���̃A�h���X�Ȃ̂ő�����Red�ɂ���B
	; Areg�ɂ��l���c���Ă����B
	ld	a,b
	add	a,d
	ld	b,a

	OUT_RG_HL_ADD_D		; 0

	ret

rc_blend_rg_01:
	ld	a,(bc)
	or	e
	ld	(bc),a

	ld	b,d		; Breg�ɕ��A�B
	ld	d, 40h

	BLEND_RG_HL_ADD_B_D	; 0

	ret

;---------------------------------------------------------------;
; RG 2���C���`��
; ����
;	HLreg: �L�����f�[�^
;	BCreg: �`��VRAM�A�h���X
;	Ereg: �r�b�g���C���f�[�^
;---------------------------------------------------------------;
rc_image_rg_02:
	ld	d,b		; Dreg��Breg���o�b�t�@�B

	ld	a,b
	and	07h
	add	a, BITLINE_BUFFER0 >> 8
	ld	b,a

	ld	a,(bc)
	and	e
	jp	nz, rc_blend_rg_02

	ld	a,(bc)	; BitLine�Ƀt���O�𗧂Ă�B
	or	e
	ld	(bc),a

	ld	b,d		; Breg�ɕ��A�B

; wirte
	ld	de, 040c8h	; Ereg��-40+8�̒l�B

	; OUTI�p��+1���Ă����B
	inc b

	; Blue�v���[���̃A�h���X�Ȃ̂ő�����Red�ɂ���B
	; Areg�ɂ��l���c���Ă����B
	ld	a,b
	add	a,d
	ld	b,a

	jp	rg_write_02

rc_blend_rg_02:
	ld	a,(bc)
	or	e
	ld	(bc),a

	ld	b,d		; Breg�ɕ��A�B
	ld	d, 40h

	jp	rg_blend_02

;---------------------------------------------------------------;
; RG 3���C���`��
; ����
;	HLreg: �L�����f�[�^
;	BCreg: �`��VRAM�A�h���X
;	Ereg: �r�b�g���C���f�[�^
;---------------------------------------------------------------;
rc_image_rg_03:
	ld	d,b		; Dreg��Breg���o�b�t�@�B

	ld	a,b
	and	07h
	add	a, BITLINE_BUFFER0 >> 8
	ld	b,a

	ld	a,(bc)
	and	e
	jp	nz, rc_blend_rg_03

	ld	a,(bc)	; BitLine�Ƀt���O�𗧂Ă�B
	or	e
	ld	(bc),a

	ld	b,d		; Breg�ɕ��A�B

; wirte
	ld	de, 040c8h	; Ereg��-40+8�̒l�B

	; OUTI�p��+1���Ă����B
	inc b

	; Blue�v���[���̃A�h���X�Ȃ̂ő�����Red�ɂ���B
	; Areg�ɂ��l���c���Ă����B
	ld	a,b
	add	a,d
	ld	b,a

	jp	rg_write_03

rc_blend_rg_03:
	ld	a,(bc)
	or	e
	ld	(bc),a

	ld	b,d		; Breg�ɕ��A�B
	ld	d, 40h

	jp	rg_blend_03

;---------------------------------------------------------------;
; RG 4���C���`��
; ����
;	HLreg: �L�����f�[�^
;	BCreg: �`��VRAM�A�h���X
;	Ereg: �r�b�g���C���f�[�^
;---------------------------------------------------------------;
rc_image_rg_04:
	ld	d,b		; Dreg��Breg���o�b�t�@�B

	ld	a,b
	and	07h
	add	a, BITLINE_BUFFER0 >> 8
	ld	b,a

	ld	a,(bc)
	and	e
	jp	nz, rc_blend_rg_04

	ld	a,(bc)	; BitLine�Ƀt���O�𗧂Ă�B
	or	e
	ld	(bc),a

	ld	b,d		; Breg�ɕ��A�B

; wirte
	ld	de, 040c8h	; Ereg��-40+8�̒l�B

	; OUTI�p��+1���Ă����B
	inc b

	; Blue�v���[���̃A�h���X�Ȃ̂ő�����Red�ɂ���B
	; Areg�ɂ��l���c���Ă����B
	ld	a,b
	add	a,d
	ld	b,a

	jp	rg_write_04

rc_blend_rg_04:
	ld	a,(bc)
	or	e
	ld	(bc),a

	ld	b,d		; Breg�ɕ��A�B
	ld	d, 40h

	jp	rg_blend_04

;---------------------------------------------------------------;
; RG 5���C���`��
; ����
;	HLreg: �L�����f�[�^
;	BCreg: �`��VRAM�A�h���X
;	Ereg: �r�b�g���C���f�[�^
;---------------------------------------------------------------;
rc_image_rg_05:
	ld	d,b		; Dreg��Breg���o�b�t�@�B

	ld	a,b
	and	07h
	add	a, BITLINE_BUFFER0 >> 8
	ld	b,a

	ld	a,(bc)
	and	e
	jp	nz, rc_blend_rg_05

	ld	a,(bc)	; BitLine�Ƀt���O�𗧂Ă�B
	or	e
	ld	(bc),a

	ld	b,d		; Breg�ɕ��A�B

; wirte
	ld	de, 040c8h	; Ereg��-40+8�̒l�B

	; OUTI�p��+1���Ă����B
	inc b

	; Blue�v���[���̃A�h���X�Ȃ̂ő�����Red�ɂ���B
	; Areg�ɂ��l���c���Ă����B
	ld	a,b
	add	a,d
	ld	b,a

	jp	rg_write_05

rc_blend_rg_05:
	ld	a,(bc)
	or	e
	ld	(bc),a

	ld	b,d		; Breg�ɕ��A�B
	ld	d, 40h

	jp	rg_blend_05

;---------------------------------------------------------------;
; RG 6���C���`��
; ����
;	HLreg: �L�����f�[�^
;	BCreg: �`��VRAM�A�h���X
;	Ereg: �r�b�g���C���f�[�^
;---------------------------------------------------------------;
rc_image_rg_06:
	ld	d,b		; Dreg��Breg���o�b�t�@�B

	ld	a,b
	and	07h
	add	a, BITLINE_BUFFER0 >> 8
	ld	b,a

	ld	a,(bc)
	and	e
	jp	nz, rc_blend_rg_06

	ld	a,(bc)	; BitLine�Ƀt���O�𗧂Ă�B
	or	e
	ld	(bc),a

	ld	b,d		; Breg�ɕ��A�B

; wirte
	ld	de, 040c8h	; Ereg��-40+8�̒l�B

	; OUTI�p��+1���Ă����B
	inc b

	; Blue�v���[���̃A�h���X�Ȃ̂ő�����Red�ɂ���B
	; Areg�ɂ��l���c���Ă����B
	ld	a,b
	add	a,d
	ld	b,a

	jp	rg_write_06

rc_blend_rg_06:
	ld	a,(bc)
	or	e
	ld	(bc),a

	ld	b,d		; Breg�ɕ��A�B
	ld	d, 40h

	jp	rg_blend_06

;---------------------------------------------------------------;
; RG 7���C���`��
; ����
;	HLreg: �L�����f�[�^
;	BCreg: �`��VRAM�A�h���X
;	Ereg: �r�b�g���C���f�[�^
;---------------------------------------------------------------;
rc_image_rg_07:
	ld	d,b		; Dreg��Breg���o�b�t�@�B

	ld	a,b
	and	07h
	add	a, BITLINE_BUFFER0 >> 8
	ld	b,a

	ld	a,(bc)
	and	e
	jp	nz, rc_blend_rg_07

	ld	a,(bc)	; BitLine�Ƀt���O�𗧂Ă�B
	or	e
	ld	(bc),a

	ld	b,d		; Breg�ɕ��A�B

; wirte
	ld	de, 040c8h	; Ereg��-40+8�̒l�B

	; OUTI�p��+1���Ă����B
	inc b

	; Blue�v���[���̃A�h���X�Ȃ̂ő�����Red�ɂ���B
	; Areg�ɂ��l���c���Ă����B
	ld	a,b
	add	a,d
	ld	b,a

	jp	rg_write_07

rc_blend_rg_07:
	ld	a,(bc)
	or	e
	ld	(bc),a

	ld	b,d		; Breg�ɕ��A�B
	ld	d, 40h

	jp	rg_blend_07

;---------------------------------------------------------------;
; RG 8���C���`��
; ����
;	HLreg: �L�����f�[�^
;	BCreg: �`��VRAM�A�h���X
;---------------------------------------------------------------;
rc_image_rg_08:
	; VRAM Adrs(BCreg)����BitLineBuff�����߂�B
	; BitLineBuff�� 0f8xx�ɂ���̂ŁAf800�� OR����Ƌ��܂�B

; Ereg: �r�b�g���C���f�[�^

	ld	d,b		; Dreg��Breg���o�b�t�@�B

	ld	a,b
	and	07h
	add	a, BITLINE_BUFFER0 >> 8
	ld	b,a

	ld	a,(bc)
	or	a
	jp	nz, rc_blend_rg_08

	ld	a,0ffh
	ld	(bc),a

	ld	b,d		; Breg�ɕ��A�B

; wirte
	ld	de, 040c8h	; Ereg��-40+8�̒l�B

	; OUTI�p��+1���Ă����B
	inc b

	; Blue�v���[���̃A�h���X�Ȃ̂ő�����Red�ɂ���B
	; Areg�ɂ��l���c���Ă����B
	ld	a,b
	add	a,d
	ld	b,a

	OUT_RG_HL_ADD_D_E	; 0
rg_write_07:
	OUT_RG_HL_ADD_D_E	; 1
rg_write_06:
	OUT_RG_HL_ADD_D_E	; 2
rg_write_05:
	OUT_RG_HL_ADD_D_E	; 3
rg_write_04:
	OUT_RG_HL_ADD_D_E	; 4
rg_write_03:
	OUT_RG_HL_ADD_D_E	; 5
rg_write_02:
	OUT_RG_HL_ADD_D_E	; 6
rg_write_01:
	OUT_RG_HL_ADD_D		; 7

	ret

rc_blend_rg_08:
	ld	a,0ffh	; Bit���C���ɏ������݁B
	ld	(bc),a

	ld	b,d		; Breg�ɕ��A�B
	ld	d, 40h

	BLEND_RG_HL_ADD_B_D	; 0
	ADD_B_88
rg_blend_07:
	BLEND_RG_HL_ADD_B_D	; 1
	ADD_B_88
rg_blend_06:
	BLEND_RG_HL_ADD_B_D	; 2
	ADD_B_88
rg_blend_05:
	BLEND_RG_HL_ADD_B_D	; 3
	ADD_B_88
rg_blend_04:
	BLEND_RG_HL_ADD_B_D	; 4
	ADD_B_88
rg_blend_03:
	BLEND_RG_HL_ADD_B_D	; 5
	ADD_B_88
rg_blend_02:
	BLEND_RG_HL_ADD_B_D	; 6
	ADD_B_88
rg_blend_01:
	BLEND_RG_HL_ADD_B_D	; 7

	ret


;---------------------------------------------------------------;
; Blue Green 1���C���`��
; ����
;	HLreg: �L�����f�[�^
;	BCreg: �`��VRAM�A�h���X (Blue�v���[���A�h���X)
;	Ereg: �r�b�g���C���f�[�^
;---------------------------------------------------------------;
rc_image_bg_01:
	ld	d,b		; Dreg��Breg���o�b�t�@�B

	ld	a,b
	and	07h
	add	a, BITLINE_BUFFER0 >> 8
	ld	b,a

	ld	a,(bc)
	and	e
	jp	nz, rc_blend_bg_01

	ld	a,(bc)	; BitLine�Ƀt���O�𗧂Ă�B
	or	e
	ld	(bc),a

	ld	b,d		; Breg�ɕ��A�B

; wirte
	ld	de, 08088h	; Dreg��Blue��Green, Ereg��-80+8�̒l�B

	; OUTI�p��+1���Ă����B
	inc b

	; Blue�v���[���A�h���X(H)��Areg�ɂ��c���B
	ld	a,b

	OUT_BG_HL_ADD_D		; 0

	ret

rc_blend_bg_01:
	; BitLine�Ƀt���O�𗧂Ă�B
	ld	a,(bc)
	or	e
	ld	(bc),a

	ld	b,d		; Breg�ɕ��A�B
	ld	d, 40h	; ���v���[���Z�o�p (RGB�v���[���ɏ����ނ��� 040h)

	BLEND_BG_HL_ADD_B_D	; 0

	ret

;---------------------------------------------------------------;
; Blue Green 2���C���`��
; ����
;	HLreg: �L�����f�[�^
;	BCreg: �`��VRAM�A�h���X
;	Ereg: �r�b�g���C���f�[�^
;---------------------------------------------------------------;
rc_image_bg_02:
	ld	d,b		; Dreg��Breg���o�b�t�@�B

	ld	a,b
	and	07h
	add	a, BITLINE_BUFFER0 >> 8
	ld	b,a

	ld	a,(bc)
	and	e
	jp	nz, rc_blend_bg_02

	ld	a,(bc)	; BitLine�Ƀt���O�𗧂Ă�B
	or	e
	ld	(bc),a

	ld	b,d		; Breg�ɕ��A�B

; wirte
	ld	de, 08088h	; Dreg��Blue��Green, Ereg��-80+8�̒l�B

	; OUTI�p��+1���Ă����B
	inc b

	; Blue�v���[���A�h���X(H)��Areg�ɂ��c���B
	ld	a,b

	jp		bg_write_02


rc_blend_bg_02:
	ld	a,(bc)	; BitLine�Ƀt���O�𗧂Ă�B
	or	e
	ld	(bc),a

	ld	b,d		; Breg�ɕ��A�B
	ld	d, 40h	; ���v���[���Z�o�p (RGB�v���[���ɏ����ނ��� 040h)

	jp	bg_blend_02

;---------------------------------------------------------------;
; Blue Green 3���C���`��
; ����
;	HLreg: �L�����f�[�^
;	BCreg: �`��VRAM�A�h���X
;	Ereg: �r�b�g���C���f�[�^
;---------------------------------------------------------------;
rc_image_bg_03:
	ld	d,b		; Dreg��Breg���o�b�t�@�B

	ld	a,b
	and	07h
	add	a, BITLINE_BUFFER0 >> 8
	ld	b,a

	ld	a,(bc)
	and	e
	jp	nz, rc_blend_bg_03

	ld	a,(bc)	; BitLine�Ƀt���O�𗧂Ă�B
	or	e
	ld	(bc),a

	ld	b,d		; Breg�ɕ��A�B

; wirte
	ld	de, 08088h	; Dreg��Blue��Green, Ereg��-80+8�̒l�B

	; OUTI�p��+1���Ă����B
	inc b

	; Blue�v���[���A�h���X(H)��Areg�ɂ��c���B
	ld	a,b

	jp		bg_write_03

rc_blend_bg_03:
	ld	a,(bc)	; BitLine�Ƀt���O�𗧂Ă�B
	or	e
	ld	(bc),a

	ld	b,d		; Breg�ɕ��A�B
	ld	d, 40h	; ���v���[���Z�o�p (RGB�v���[���ɏ����ނ��� 040h)

	jp	bg_blend_03

;---------------------------------------------------------------;
; Blue Green 4���C���`��
; ����
;	HLreg: �L�����f�[�^
;	BCreg: �`��VRAM�A�h���X
;	Ereg: �r�b�g���C���f�[�^
;---------------------------------------------------------------;
rc_image_bg_04:
	ld	d,b		; Dreg��Breg���o�b�t�@�B

	ld	a,b
	and	07h
	add	a, BITLINE_BUFFER0 >> 8
	ld	b,a

	ld	a,(bc)
	and	e
	jp	nz, rc_blend_bg_04

	ld	a,(bc)	; BitLine�Ƀt���O�𗧂Ă�B
	or	e
	ld	(bc),a

	ld	b,d		; Breg�ɕ��A�B

; wirte
	ld	de, 08088h	; Dreg��Blue��Green, Ereg��-80+8�̒l�B

	; OUTI�p��+1���Ă����B
	inc b

	; Blue�v���[���A�h���X(H)��Areg�ɂ��c���B
	ld	a,b

	jp		bg_write_04

rc_blend_bg_04:
	ld	a,(bc)	; BitLine�Ƀt���O�𗧂Ă�B
	or	e
	ld	(bc),a

	ld	b,d		; Breg�ɕ��A�B
	ld	d, 40h	; ���v���[���Z�o�p (RGB�v���[���ɏ����ނ��� 040h)

	jp	bg_blend_04

;---------------------------------------------------------------;
; Blue Green 5���C���`��
; ����
;	HLreg: �L�����f�[�^
;	BCreg: �`��VRAM�A�h���X
;	Ereg: �r�b�g���C���f�[�^
;---------------------------------------------------------------;
rc_image_bg_05:
	ld	d,b		; Dreg��Breg���o�b�t�@�B

	ld	a,b
	and	07h
	add	a, BITLINE_BUFFER0 >> 8
	ld	b,a

	ld	a,(bc)
	and	e
	jp	nz, rc_blend_bg_05

	ld	a,(bc)	; BitLine�Ƀt���O�𗧂Ă�B
	or	e
	ld	(bc),a

	ld	b,d		; Breg�ɕ��A�B

; wirte
	ld	de, 08088h	; Dreg��Blue��Green, Ereg��-80+8�̒l�B

	; OUTI�p��+1���Ă����B
	inc b

	; Blue�v���[���A�h���X(H)��Areg�ɂ��c���B
	ld	a,b

	jp		bg_write_05


rc_blend_bg_05:
	ld	a,(bc)	; BitLine�Ƀt���O�𗧂Ă�B
	or	e
	ld	(bc),a

	ld	b,d		; Breg�ɕ��A�B
	ld	d, 40h	; ���v���[���Z�o�p (RGB�v���[���ɏ����ނ��� 040h)

	jp	bg_blend_05

;---------------------------------------------------------------;
; Blue Green 6���C���`��
; ����
;	HLreg: �L�����f�[�^
;	BCreg: �`��VRAM�A�h���X
;	Ereg: �r�b�g���C���f�[�^
;---------------------------------------------------------------;
rc_image_bg_06:
	ld	d,b		; Dreg��Breg���o�b�t�@�B

	ld	a,b
	and	07h
	add	a, BITLINE_BUFFER0 >> 8
	ld	b,a

	ld	a,(bc)
	and	e
	jp	nz, rc_blend_bg_06

	ld	a,(bc)	; BitLine�Ƀt���O�𗧂Ă�B
	or	e
	ld	(bc),a

	ld	b,d		; Breg�ɕ��A�B

; wirte
	ld	de, 08088h	; Dreg��Blue��Green, Ereg��-80+8�̒l�B

	; OUTI�p��+1���Ă����B
	inc b

	; Blue�v���[���A�h���X(H)��Areg�ɂ��c���B
	ld	a,b

	jp		bg_write_06

rc_blend_bg_06:
	ld	a,(bc)	; BitLine�Ƀt���O�𗧂Ă�B
	or	e
	ld	(bc),a

	ld	b,d		; Breg�ɕ��A�B
	ld	d, 40h	; ���v���[���Z�o�p (RGB�v���[���ɏ����ނ��� 040h)

	jp	bg_blend_06

;---------------------------------------------------------------;
; Blue Green 7���C���`��
; ����
;	HLreg: �L�����f�[�^
;	BCreg: �`��VRAM�A�h���X
;	Ereg: �r�b�g���C���f�[�^
;---------------------------------------------------------------;
rc_image_bg_07:
	ld	d,b		; Dreg��Breg���o�b�t�@�B

	ld	a,b
	and	07h
	add	a, BITLINE_BUFFER0 >> 8
	ld	b,a

	ld	a,(bc)
	and	e
	jp	nz, rc_blend_bg_07

	ld	a,(bc)	; BitLine�Ƀt���O�𗧂Ă�B
	or	e
	ld	(bc),a

	ld	b,d		; Breg�ɕ��A�B

; wirte
	ld	de, 08088h	; Dreg��Blue��Green, Ereg��-80+8�̒l�B

	; OUTI�p��+1���Ă����B
	inc b

	; Blue�v���[���A�h���X(H)��Areg�ɂ��c���B
	ld	a,b

	jp		bg_write_07

rc_blend_bg_07:
	ld	a,(bc)	; BitLine�Ƀt���O�𗧂Ă�B
	or	e
	ld	(bc),a

	ld	b,d		; Breg�ɕ��A�B
	ld	d, 40h	; ���v���[���Z�o�p (RGB�v���[���ɏ����ނ��� 040h)

	jp	bg_blend_07

;---------------------------------------------------------------;
; Blue Green 8���C���`��
; ����
;	HLreg: �L�����f�[�^
;	BCreg: �`��VRAM�A�h���X
;	Ereg: �r�b�g���C���f�[�^
;---------------------------------------------------------------;
rc_image_bg_08:
	ld	d,b		; Dreg��Breg���o�b�t�@�B

	ld	a,b
	and	07h
	add	a, BITLINE_BUFFER0 >> 8
	ld	b,a

	ld	a,(bc)
	and	e
	jp	nz, rc_blend_bg_08

	ld	a, 0ffh; BitLine�Ƀt���O�𗧂Ă�B
	ld	(bc),a

	ld	b,d		; Breg�ɕ��A�B

; wirte
	ld	de, 08088h	; Dreg��Blue��Green, Ereg��-80+8�̒l�B

	; OUTI�p��+1���Ă����B
	inc b

	; Blue�v���[���A�h���X(H)��Areg�ɂ��c���B
	ld	a,b

	OUT_BG_HL_ADD_D		; 0
	ADD_B_E
bg_write_07:
	OUT_BG_HL_ADD_D		; 1
	ADD_B_E
bg_write_06:
	OUT_BG_HL_ADD_D		; 2
	ADD_B_E
bg_write_05:
	OUT_BG_HL_ADD_D		; 3
	ADD_B_E
bg_write_04:
	OUT_BG_HL_ADD_D		; 4
	ADD_B_E
bg_write_03:
	OUT_BG_HL_ADD_D		; 5
	ADD_B_E
bg_write_02:
	OUT_BG_HL_ADD_D		; 6
	ADD_B_E
bg_write_01:
	OUT_BG_HL_ADD_D		; 7

	ret

rc_blend_bg_08:
	ld	a, 0ffh		; BitLine�Ƀt���O�𗧂Ă�B
	ld	(bc),a

	ld	b,d		; Breg�ɕ��A�B
	ld	d, 40h	; ���v���[���Z�o�p (RGB�v���[���ɏ����ނ��� 040h)

	BLEND_BG_HL_ADD_B_D	; 0
	ADD_B_88
bg_blend_07:
	BLEND_BG_HL_ADD_B_D	; 1
	ADD_B_88
bg_blend_06:
	BLEND_BG_HL_ADD_B_D	; 2
	ADD_B_88
bg_blend_05:
	BLEND_BG_HL_ADD_B_D	; 3
	ADD_B_88
bg_blend_04:
	BLEND_BG_HL_ADD_B_D	; 4
	ADD_B_88
bg_blend_03:
	BLEND_BG_HL_ADD_B_D	; 5
	ADD_B_88
bg_blend_02:
	BLEND_BG_HL_ADD_B_D	; 6
	ADD_B_88
bg_blend_01:
	BLEND_BG_HL_ADD_B_D	; 7

	ret


;----
;	END


;---------------------------------------------------------------;
;	Copyright (c) 2019 render_r.asm
;	This software is released under the MIT License.
;	http://opensource.org/licenses/mit-license.php
;---------------------------------------------------------------;

;---------------------------------------------------------------;
; �v���[�� R 1���C���`��
; ����
;	HLreg: �L�����f�[�^
;	BCreg: �`��VRAM�A�h���X
;	Ereg: �r�b�g���C���f�[�^
;---------------------------------------------------------------;
rc_image_r1:
	ld		d,b		; Dreg��Breg���o�b�t�@�B

	ld		a,b
	and	07h
	add	a, BITLINE_BUFFER0 >> 8
	ld		b,a

	ld		a,(bc)
	and		e
	jp		nz, rc_blend_r1

	ld		a,(bc)
	or		e
	ld		(bc),a

	ld		a, 040h+1
	add		a,d
	ld		b,a

	OUT_B_HL
	ADD_B_40	; �������̂��� R��G

	ret

rc_blend_r1:
	ld		a,(bc)
	or		e
	ld		(bc),a

	ld		b,d		; Breg�ɕ��A�B
	ld		d, 040h

	jp		rc_blend_r1_line


;---------------------------------------------------------------;
; �v���[�� R 2���C���`��
; ����
;	HLreg: �L�����f�[�^
;	BCreg: �`��VRAM�A�h���X
;	Ereg: �r�b�g���C���f�[�^
;---------------------------------------------------------------;
rc_image_r2:
	ld		d,b		; Dreg��Breg���o�b�t�@�B

	ld		a,b
	and	07h
	add	a, BITLINE_BUFFER0 >> 8
	ld		b,a

	ld		a,(bc)
	and		e
	jp		nz, rc_blend_r2
;
	ld		a,(bc)
	or		e
	ld		(bc),a

	; B��R�v���[���ɂ���B�X�� OUTI�p��+1���� Areg�ɂ��c���B
	ld		a,d
	ld		de, 04008h		; Dreg: �v���[�������p, Ereg: ���C�������p
	inc		a
	add		a,d
	ld		b,a

	jp		rc_write_r2_line

rc_blend_r2:
	ld		a,(bc)
	or		e
	ld		(bc),a

	ld		b,d		; Breg�ɕ��A�B
	ld		d, 40h

	jp		rc_blend_r2_line


;---------------------------------------------------------------;
; �v���[�� R 3���C���`��
; ����
;	HLreg: �L�����f�[�^
;	BCreg: �`��VRAM�A�h���X
;	Ereg: �r�b�g���C���f�[�^
;---------------------------------------------------------------;
rc_image_r3:
	ld		d,b		; Dreg��Breg���o�b�t�@�B

	ld		a,b
	and	07h
	add	a, BITLINE_BUFFER0 >> 8
	ld		b,a

	ld		a,(bc)
	and		e
	jp		nz, rc_blend_r3

	ld		a,(bc)
	or		e
	ld		(bc),a

	; B��R�v���[���ɂ���B�X�� OUTI�p��+1���� Areg�ɂ��c���B
	ld		a,d
	ld		de, 04008h		; Dreg: �v���[�������p, Ereg: ���C�������p
	inc		a
	add		a,d
	ld		b,a

	jp		rc_write_r3_line

rc_blend_r3:
	ld		a,(bc)
	or		e
	ld		(bc),a

	ld		b,d		; Breg�ɕ��A�B
	ld		d, 40h

	jp		rc_blend_r3_line

;---------------------------------------------------------------;
; �v���[�� R 4���C���`��
; ����
;	HLreg: �L�����f�[�^
;	BCreg: �`��VRAM�A�h���X
;	Ereg: �r�b�g���C���f�[�^
;---------------------------------------------------------------;
rc_image_r4:
	ld		d,b		; Dreg��Breg���o�b�t�@�B

	ld		a,b
	and	07h
	add	a, BITLINE_BUFFER0 >> 8
	ld		b,a

	ld		a,(bc)
	and		e
	jp		nz, rc_blend_r4

	ld		a,(bc)
	or		e
	ld		(bc),a

	; B��R�v���[���ɂ���B�X�� OUTI�p��+1���� Areg�ɂ��c���B
	ld		a,d
	ld		de, 04008h		; Dreg: �v���[�������p, Ereg: ���C�������p
	inc		a
	add		a,d
	ld		b,a

	jp		rc_write_r4_line

rc_blend_r4:
	ld		a,(bc)
	or		e
	ld		(bc),a

	ld		b,d		; Breg�ɕ��A�B
	ld		d, 40h

	jp		rc_blend_r4_line

;---------------------------------------------------------------;
; �v���[�� R 5���C���`��
; ����
;	HLreg: �L�����f�[�^
;	BCreg: �`��VRAM�A�h���X
;	Ereg: �r�b�g���C���f�[�^
;---------------------------------------------------------------;
rc_image_r5:
	ld		d,b		; Dreg��Breg���o�b�t�@�B

	ld		a,b
	and	07h
	add	a, BITLINE_BUFFER0 >> 8
	ld		b,a

	ld		a,(bc)
	and		e
	jp		nz, rc_blend_r5

	ld		a,(bc)
	or		e
	ld		(bc),a

	; B��R�v���[���ɂ���B�X�� OUTI�p��+1���� Areg�ɂ��c���B
	ld		a,d
	ld		de, 04008h		; Dreg: �v���[�������p, Ereg: ���C�������p
	inc		a
	add		a,d
	ld		b,a

	jp		rc_write_r5_line

rc_blend_r5:
	ld		a,(bc)
	or		e
	ld		(bc),a

	ld		b,d		; Breg�ɕ��A�B
	ld		d, 40h

	jp		rc_blend_r5_line

;---------------------------------------------------------------;
; �v���[�� R 6���C���`��
; ����
;	HLreg: �L�����f�[�^
;	BCreg: �`��VRAM�A�h���X
;	Ereg: �r�b�g���C���f�[�^
;---------------------------------------------------------------;
rc_image_r6:
	ld		d,b		; Dreg��Breg���o�b�t�@�B

	ld		a,b
	and	07h
	add	a, BITLINE_BUFFER0 >> 8
	ld		b,a

	ld		a,(bc)
	and		e
	jp		nz, rc_blend_r6

	ld		a,(bc)
	or		e
	ld		(bc),a

	; B��R�v���[���ɂ���B�X�� OUTI�p��+1���� Areg�ɂ��c���B
	ld		a,d
	ld		de, 04008h		; Dreg: �v���[�������p, Ereg: ���C�������p
	inc		a
	add		a,d
	ld		b,a

	jp		rc_write_r6_line

rc_blend_r6:
	ld		a,(bc)
	or		e
	ld		(bc),a

	ld		b,d		; Breg�ɕ��A�B
	ld		d, 40h

	jp		rc_blend_r6_line

;---------------------------------------------------------------;
; �v���[�� R 7���C���`��
; ����
;	HLreg: �L�����f�[�^
;	BCreg: �`��VRAM�A�h���X
;	Ereg: �r�b�g���C���f�[�^
;---------------------------------------------------------------;
rc_image_r7:
	ld		d,b		; Dreg��Breg���o�b�t�@�B

	ld		a,b
	and	07h
	add	a, BITLINE_BUFFER0 >> 8
	ld		b,a

	ld		a,(bc)
	and		e
	jp		nz, rc_blend_r7

	ld		a,(bc)
	or		e
	ld		(bc),a

	; B��R�v���[���ɂ���B�X�� OUTI�p��+1���� Areg�ɂ��c���B
	ld		a,d
	ld		de, 04008h		; Dreg: �v���[�������p, Ereg: ���C�������p
	inc		a
	add		a,d
	ld		b,a

	jp		rc_write_r7_line

rc_blend_r7:
	ld		a,(bc)
	or		e
	ld		(bc),a

	ld		b,d		; Breg�ɕ��A�B
	ld		d, 40h

	jp		rc_blend_r7_line

;---------------------------------------------------------------;
; �v���[��: R 8���C���`��
; ����
;	HLreg: �L�����f�[�^
;	BCreg: �`��VRAM�A�h���X
;---------------------------------------------------------------;
rc_image_r8:
	; VRAM Adrs(BCreg)����BitLineBuff�����߂�B
	; BitLineBuff�� 0f8xx�ɂ���̂ŁAf800�� OR����Ƌ��܂�B
	; Ereg: �r�b�g���C���f�[�^

	ld		d,b		; Dreg��Breg���o�b�t�@�B

	ld		a,b
	and	07h
	add	a, BITLINE_BUFFER0 >> 8
	ld		b,a

	ld		a,(bc)
	or		a
	jp		nz, rc_blend_r8

	ld		a,0ffh
	ld		(bc),a

	; Dreg�𕜋A���āABlue��Red�v���[���ցB
	; ���̍ۂ� OUTI�p��+1����Areg�ɂ��c���B
	ld		a,d
	ld		de, 04008h	; Dreg: �v���[�������p Ereg: ���C�������p(08h)
	inc		a
	add		a,d
	ld		b,a

	OUT_R_HL_ADD_E
rc_write_r7_line:
	OUT_R_HL_ADD_E
rc_write_r6_line:
	OUT_R_HL_ADD_E
rc_write_r5_line:
	OUT_R_HL_ADD_E
rc_write_r4_line:
	OUT_R_HL_ADD_E
rc_write_r3_line:
	OUT_R_HL_ADD_E
rc_write_r2_line:
	OUT_R_HL_ADD_E
	OUT_R_HL

	; �������̂��� R��G
	ld		a,b
	add		a,d
	ld		b,a

	ret

rc_blend_r8:
	ld		a,0ffh
	ld		(bc),a

	ld		b,d		; Breg�ɕ��A�B
	ld		d, 40h

	; 3�v���[���ƃu�����h����̂�B�v���[����OK.

	BLEND_R_HL_ADD_B_D
	ADD_B_88

rc_blend_r7_line:
	BLEND_R_HL_ADD_B_D
	ADD_B_88

rc_blend_r6_line:
	BLEND_R_HL_ADD_B_D
	ADD_B_88

rc_blend_r5_line:
	BLEND_R_HL_ADD_B_D
	ADD_B_88

rc_blend_r4_line:
	BLEND_R_HL_ADD_B_D
	ADD_B_88

rc_blend_r3_line:
	BLEND_R_HL_ADD_B_D
	ADD_B_88

rc_blend_r2_line:
	BLEND_R_HL_ADD_B_D
	ADD_B_88

rc_blend_r1_line:
	BLEND_R_HL_ADD_B_D

	ret


;----
;	END

;---------------------------------------------------------------;
;	Copyright (c) 2019 render_g.asm
;	This software is released under the MIT License.
;	http://opensource.org/licenses/mit-license.php
;---------------------------------------------------------------;

;---------------------------------------------------------------;
; �v���[�� G 1���C���`��
; ����
;	HLreg: �L�����f�[�^
;	BCreg: �`��VRAM�A�h���X
;	Ereg: �r�b�g���C���f�[�^
;---------------------------------------------------------------;
rc_image_g1:
	ld		d,b		; Dreg��Breg���o�b�t�@�B

	ld		a,b
	and	07h
	add	a, BITLINE_BUFFER0 >> 8
	ld		b,a

	ld		a,(bc)
	and		e
	jp		nz, rc_blend_g1

	ld		a,(bc)
	or		e
	ld		(bc),a

	ld		a, 080h+1
	add		a,d
	ld		b,a

	OUT_G_HL

	; G�Ȃ̂œ��ɏC���Ȃ��B

	ret

rc_blend_g1:
	ld		a,(bc)
	or		e
	ld		(bc),a

	ld		b,d		; Breg�ɕ��A�B
	ld		d, 040h

	jp		rc_blend_g1_line


;---------------------------------------------------------------;
; �v���[�� G 2���C���`��
; ����
;	HLreg: �L�����f�[�^
;	BCreg: �`��VRAM�A�h���X
;	Ereg: �r�b�g���C���f�[�^
;---------------------------------------------------------------;
rc_image_g2:
	ld		d,b		; Dreg��Breg���o�b�t�@�B

	ld		a,b
	and	07h
	add	a, BITLINE_BUFFER0 >> 8
	ld		b,a

	ld		a,(bc)
	and		e
	jp		nz, rc_blend_g2
;
	ld		a,(bc)
	or		e
	ld		(bc),a

	; B��G�v���[���ɂ���B�X�� OUTI�p��+1���� Areg�ɂ��c���B
	ld		a,d
	ld		de, 08008h		; Dreg: �v���[�������p, Ereg: ���C�������p
	inc		a
	add		a,d
	ld		b,a

	jp		rc_write_g2_line

rc_blend_g2:
	ld		a,(bc)
	or		e
	ld		(bc),a

	ld		b,d		; Breg�ɕ��A�B
	ld		d, 40h

	jp		rc_blend_g2_line


;---------------------------------------------------------------;
; �v���[�� G 3���C���`��
; ����
;	HLreg: �L�����f�[�^
;	BCreg: �`��VRAM�A�h���X
;	Ereg: �r�b�g���C���f�[�^
;---------------------------------------------------------------;
rc_image_g3:
	ld		d,b		; Dreg��Breg���o�b�t�@�B

	ld		a,b
	and	07h
	add	a, BITLINE_BUFFER0 >> 8
	ld		b,a

	ld		a,(bc)
	and		e
	jp		nz, rc_blend_g3

	ld		a,(bc)
	or		e
	ld		(bc),a

	; B��G�v���[���ɂ���B�X�� OUTI�p��+1���� Areg�ɂ��c���B
	ld		a,d
	ld		de, 08008h		; Dreg: �v���[�������p, Ereg: ���C�������p
	inc		a
	add		a,d
	ld		b,a

	jp		rc_write_g3_line

rc_blend_g3:
	ld		a,(bc)
	or		e
	ld		(bc),a

	ld		b,d		; Breg�ɕ��A�B
	ld		d, 40h

	jp		rc_blend_g3_line

;---------------------------------------------------------------;
; �v���[�� G 4���C���`��
; ����
;	HLreg: �L�����f�[�^
;	BCreg: �`��VRAM�A�h���X
;	Ereg: �r�b�g���C���f�[�^
;---------------------------------------------------------------;
rc_image_g4:
	ld		d,b		; Dreg��Breg���o�b�t�@�B

	ld		a,b
	and	07h
	add	a, BITLINE_BUFFER0 >> 8
	ld		b,a

	ld		a,(bc)
	and		e
	jp		nz, rc_blend_g4

	ld		a,(bc)
	or		e
	ld		(bc),a

	; B��G�v���[���ɂ���B�X�� OUTI�p��+1���� Areg�ɂ��c���B
	ld		a,d
	ld		de, 08008h		; Dreg: �v���[�������p, Ereg: ���C�������p
	inc		a
	add		a,d
	ld		b,a

	jp		rc_write_g4_line

rc_blend_g4:
	ld		a,(bc)
	or		e
	ld		(bc),a

	ld		b,d		; Breg�ɕ��A�B
	ld		d, 40h

	jp		rc_blend_g4_line

;---------------------------------------------------------------;
; �v���[�� G 5���C���`��
; ����
;	HLreg: �L�����f�[�^
;	BCreg: �`��VRAM�A�h���X
;	Ereg: �r�b�g���C���f�[�^
;---------------------------------------------------------------;
rc_image_g5:
	ld		d,b		; Dreg��Breg���o�b�t�@�B

	ld		a,b
	and	07h
	add	a, BITLINE_BUFFER0 >> 8
	ld		b,a

	ld		a,(bc)
	and		e
	jp		nz, rc_blend_g5

	ld		a,(bc)
	or		e
	ld		(bc),a

	; B��G�v���[���ɂ���B�X�� OUTI�p��+1���� Areg�ɂ��c���B
	ld		a,d
	ld		de, 08008h		; Dreg: �v���[�������p, Ereg: ���C�������p
	inc		a
	add		a,d
	ld		b,a

	jp		rc_write_g5_line

rc_blend_g5:
	ld		a,(bc)
	or		e
	ld		(bc),a

	ld		b,d		; Breg�ɕ��A�B
	ld		d, 40h

	jp		rc_blend_g5_line

;---------------------------------------------------------------;
; �v���[�� G 6���C���`��
; ����
;	HLreg: �L�����f�[�^
;	BCreg: �`��VRAM�A�h���X
;	Ereg: �r�b�g���C���f�[�^
;---------------------------------------------------------------;
rc_image_g6:
	ld		d,b		; Dreg��Breg���o�b�t�@�B

	ld		a,b
	and	07h
	add	a, BITLINE_BUFFER0 >> 8
	ld		b,a

	ld		a,(bc)
	and		e
	jp		nz, rc_blend_g6

	ld		a,(bc)
	or		e
	ld		(bc),a

	; B��G�v���[���ɂ���B�X�� OUTI�p��+1���� Areg�ɂ��c���B
	ld		a,d
	ld		de, 08008h		; Dreg: �v���[�������p, Ereg: ���C�������p
	inc		a
	add		a,d
	ld		b,a

	jp		rc_write_g6_line

rc_blend_g6:
	ld		a,(bc)
	or		e
	ld		(bc),a

	ld		b,d		; Breg�ɕ��A�B
	ld		d, 40h

	jp		rc_blend_g6_line

;---------------------------------------------------------------;
; �v���[�� G 7���C���`��
; ����
;	HLreg: �L�����f�[�^
;	BCreg: �`��VRAM�A�h���X
;	Ereg: �r�b�g���C���f�[�^
;---------------------------------------------------------------;
rc_image_g7:
	ld		d,b		; Dreg��Breg���o�b�t�@�B

	ld		a,b
	and	07h
	add	a, BITLINE_BUFFER0 >> 8
	ld		b,a

	ld		a,(bc)
	and		e
	jp		nz, rc_blend_g7

	ld		a,(bc)
	or		e
	ld		(bc),a

	; B��G�v���[���ɂ���B�X�� OUTI�p��+1���� Areg�ɂ��c���B
	ld		a,d
	ld		de, 08008h		; Dreg: �v���[�������p, Ereg: ���C�������p
	inc		a
	add		a,d
	ld		b,a

	jp		rc_write_g7_line

rc_blend_g7:
	ld		a,(bc)
	or		e
	ld		(bc),a

	ld		b,d		; Breg�ɕ��A�B
	ld		d, 40h

	jp		rc_blend_g7_line

;---------------------------------------------------------------;
; �v���[��: R 8���C���`��
; ����
;	HLreg: �L�����f�[�^
;	BCreg: �`��VRAM�A�h���X
;---------------------------------------------------------------;
rc_image_g8:
	; VRAM Adrs(BCreg)����BitLineBuff�����߂�B
	; BitLineBuff�� 0f8xx�ɂ���̂ŁAf800�� OR����Ƌ��܂�B
	; Ereg: �r�b�g���C���f�[�^

	ld		d,b		; Dreg��Breg���o�b�t�@�B

	ld		a,b
	and	07h
	add	a, BITLINE_BUFFER0 >> 8
	ld		b,a

	ld		a,(bc)
	or		a
	jp		nz, rc_blend_g8

	ld		a,0ffh
	ld		(bc),a

	; Dreg�𕜋A���āABlue��Green�v���[���ցB
	; ���̍ۂ� OUTI�p��+1����Areg�ɂ��c���B
	ld		a,d
	ld		de, 08008h	; Dreg: �v���[�������p Ereg: ���C�������p(08h)
	inc		a
	add		a,d
	ld		b,a

	OUT_G_HL_ADD_E
rc_write_g7_line:
	OUT_G_HL_ADD_E
rc_write_g6_line:
	OUT_G_HL_ADD_E
rc_write_g5_line:
	OUT_G_HL_ADD_E
rc_write_g4_line:
	OUT_G_HL_ADD_E
rc_write_g3_line:
	OUT_G_HL_ADD_E
rc_write_g2_line:
	OUT_G_HL_ADD_E
	OUT_G_HL

	; G�v���[���Ȃ̂œ��ɏC���Ȃ��B

	ret

rc_blend_g8:
	ld		a,0ffh
	ld		(bc),a

	ld		b,d		; Breg�ɕ��A�B
	ld		d, 40h	; �v���[�������p

	; 3�v���[���ƃu�����h����̂�B�v���[����OK.

	BLEND_G_HL_ADD_B_D
	ADD_B_88

rc_blend_g7_line:
	BLEND_G_HL_ADD_B_D
	ADD_B_88

rc_blend_g6_line:
	BLEND_G_HL_ADD_B_D
	ADD_B_88

rc_blend_g5_line:
	BLEND_G_HL_ADD_B_D
	ADD_B_88

rc_blend_g4_line:
	BLEND_G_HL_ADD_B_D
	ADD_B_88

rc_blend_g3_line:
	BLEND_G_HL_ADD_B_D
	ADD_B_88

rc_blend_g2_line:
	BLEND_G_HL_ADD_B_D
	ADD_B_88

rc_blend_g1_line:
	BLEND_G_HL_ADD_B_D

	ret


;----
;	END

;---------------------------------------------------------------;
;	Copyright (c) 2019 render_brg16.asm
;	This software is released under the MIT License.
;	http://opensource.org/licenses/mit-license.php
;---------------------------------------------------------------;

;---------------------------------------------------------------;
; RGB �c16 pixel y=0:
;---------------------------------------------------------------;
render_rgb16_y0:
	; 8pixel�`�悷��B
	ld		e,0ffh
	call	rc_image_08

	; VRAM�����̒i�ցB
	ADD_BC_4828

	; 8pixel�`�悷��B
	ld		e,0ffh
	jp		rc_image_08

;---------------------------------------------------------------;
; RGB �c16 pixel y=1:
;---------------------------------------------------------------;
render_rgb16_y1:
	ld		e,0feh
	call	rc_image_07

	; VRAM�����̒i�ցB
	ADD_BC_4828

	; 8pixel�`�悷��B
	ld		e,0ffh
	call	rc_image_08

	; VRAM�����̒i�ցB
	ADD_BC_4828

	ld		e, 01h
	jp		rc_image_01

;---------------------------------------------------------------;
; RGB �c16 pixel y=2:
;---------------------------------------------------------------;
render_rgb16_y2:
	ld		e,0fch
	call	rc_image_06

	; VRAM�����̒i�ցB
	ADD_BC_4828

	; 8pixel�`�悷��B
	ld		e,0ffh
	call	rc_image_08

	; VRAM�����̒i�ցB
	ADD_BC_4828

	ld		e,03h
	jp		rc_image_02

;---------------------------------------------------------------;
; RGB �c16 pixel y=3:
;---------------------------------------------------------------;
render_rgb16_y3:
	ld		e,0f8h
	call	rc_image_05

	; VRAM�����̒i�ցB
	ADD_BC_4828

	; 8pixel�`�悷��B
	ld		e,0ffh
	call	rc_image_08

	; VRAM�����̒i�ցB
	ADD_BC_4828

	ld		e,07h
	jp		rc_image_03

;---------------------------------------------------------------;
; RGB �c16 pixel y=4:
;---------------------------------------------------------------;
render_rgb16_y4:
	ld		e,0f0h
	call	rc_image_04

	; VRAM�����̒i�ցB
	ADD_BC_4828

	; 8pixel�`�悷��B
	ld		e,0ffh
	call	rc_image_08

	; VRAM�����̒i�ցB
	ADD_BC_4828

	ld		e,00fh
	jp		rc_image_04

;---------------------------------------------------------------;
; RGB �c16 pixel y=5:
;---------------------------------------------------------------;
render_rgb16_y5:
	ld		e,0e0h
	call	rc_image_03

	; VRAM�����̒i�ցB
	ADD_BC_4828

	ld		e,0ffh
	call	rc_image_08

	; VRAM�����̒i�ցB
	ADD_BC_4828

	ld		e,01fh
	jp		rc_image_05

;---------------------------------------------------------------;
; RGB �c16 pixel y=6:
;---------------------------------------------------------------;
render_rgb16_y6:
	ld		e,0c0h
	call	rc_image_02

	; VRAM�����̒i�ցB
	ADD_BC_4828

	ld		e,0ffh
	call	rc_image_08

	; VRAM�����̒i�ցB
	ADD_BC_4828

	ld		e,03fh
	jp		rc_image_06

;---------------------------------------------------------------;
; RGB �c16 pixel y=7:
;---------------------------------------------------------------;
render_rgb16_y7:
	ld		e,080h
	call	rc_image_01

	; VRAM�����̒i�ցB
	ADD_BC_4828

	ld		e,0ffh
	call	rc_image_08

	; VRAM�����̒i�ցB
	ADD_BC_4828

	ld		e,07fh
	jp		rc_image_07


;----
;	END

;---------------------------------------------------------------;
;	Copyright (c) 2019 render_br16.asm
;	This software is released under the MIT License.
;	http://opensource.org/licenses/mit-license.php
;---------------------------------------------------------------;

;---------------------------------------------------------------;
; BR �c16pixel y=0:
;---------------------------------------------------------------;
render_br16_y0:
	; 8pixel�`�悷��B
	ld	e,0ffh
	call rc_image_br_08

	; VRAM�����̒i�ցB
	ADD_BC_4828

	; 8pixel�`�悷��B
	ld	e,0ffh
	jp	rc_image_br_08

;---------------------------------------------------------------;
; BR �c16pixel y=1:
;---------------------------------------------------------------;
render_br16_y1:
	ld	e,0feh
	call rc_image_br_07

	; VRAM�����̒i�ցB
	ADD_BC_4828

	; 8pixel�`�悷��B
	ld	e,0ffh
	call rc_image_br_08

	; VRAM�����̒i�ցB
	ADD_BC_4828

	ld	e, 01h
	jp	rc_image_br_01

;---------------------------------------------------------------;
; BR �c16pixel y=2:
;---------------------------------------------------------------;
render_br16_y2:
	ld	e,0fch
	call rc_image_br_06

	; VRAM�����̒i�ցB
	ADD_BC_4828

	; 8pixel�`�悷��B
	ld	e,0ffh
	call rc_image_br_08

	; VRAM�����̒i�ցB
	ADD_BC_4828

	ld	e,03h
	jp	rc_image_br_02

;---------------------------------------------------------------;
; BR �c16pixel y=3:
;---------------------------------------------------------------;
render_br16_y3:
	ld	e,0f8h
	call rc_image_br_05

	; VRAM�����̒i�ցB
	ADD_BC_4828

	; 8pixel�`�悷��B
	ld	e,0ffh
	call rc_image_br_08

	; VRAM�����̒i�ցB
	ADD_BC_4828

	ld	e,07h
	jp   rc_image_br_03

;---------------------------------------------------------------;
; BR �c16pixel y=4:
;---------------------------------------------------------------;
render_br16_y4:
	ld	e,0f0h
	call rc_image_br_04

	; VRAM�����̒i�ցB
	ADD_BC_4828

	; 8pixel�`�悷��B
	ld	e,0ffh
	call rc_image_br_08

	; VRAM�����̒i�ցB
	ADD_BC_4828

	ld	e,00fh
	jp   rc_image_br_04

;---------------------------------------------------------------;
; BR �c16pixel y=5:
;---------------------------------------------------------------;
render_br16_y5:
	ld	e,0e0h
	call rc_image_br_03

	; VRAM�����̒i�ցB
	ADD_BC_4828

	ld	e,0ffh
	call rc_image_br_08

	; VRAM�����̒i�ցB
	ADD_BC_4828

	ld	e,01fh
	jp   rc_image_br_05

;---------------------------------------------------------------;
; BR �c16pixel y=6:
;---------------------------------------------------------------;
render_br16_y6:
	ld	e,0c0h
	call rc_image_br_02

	; VRAM�����̒i�ցB
	ADD_BC_4828

	ld	e,0ffh
	call rc_image_br_08

	; VRAM�����̒i�ցB
	ADD_BC_4828

	ld	e,03fh
	jp   rc_image_br_06

;---------------------------------------------------------------;
; BR �c16pixel y=7:
;---------------------------------------------------------------;
render_br16_y7:
	ld	e,080h
	call rc_image_br_01

	; VRAM�����̒i�ցB
	ADD_BC_4828

	ld	e,0ffh
	call rc_image_br_08

	; VRAM�����̒i�ցB
	ADD_BC_4828

	ld	e,07fh
	jp   rc_image_br_07

;----
;	END

;---------------------------------------------------------------;
;	Copyright (c) 2019 render_br.asm
;	This software is released under the MIT License.
;	http://opensource.org/licenses/mit-license.php
;---------------------------------------------------------------;

;---------------------------------------------------------------;
; Blue Red 1���C���`��
; ����
;	HLreg: �L�����f�[�^
;	BCreg: �`��VRAM�A�h���X (Blue�v���[���A�h���X)
;	Ereg: �r�b�g���C���f�[�^
;---------------------------------------------------------------;
rc_image_br_01:
	ld	d,b		; Dreg��Breg���o�b�t�@�B

	ld	a,b
	and	07h
	add	a, BITLINE_BUFFER0 >> 8
	ld	b,a

	ld	a,(bc)
	and	e
	jp	nz, rc_blend_br_01

	ld	a,(bc)	; BitLine�Ƀt���O�𗧂Ă�B
	or	e
	ld	(bc),a

	ld	b,d		; Breg�ɕ��A�B

; wirte
	ld	de, 040c8h	; Dreg��Blue��Red, Ereg��-40+8�̒l�B

	; OUTI�p��+1���Ă����B
	inc b

	; Blue�v���[���A�h���X(H)��Areg�ɂ��c���B
	ld	a,b

	OUT_BR_HL_ADD_D		; 0

	; �Ō��R�v���[������G�v���[��,������ OUTI�p��+1���Ă����̂����炷�B
	add	a, 040h-1
	ld	b,a

	ret

rc_blend_br_01:
	; BitLine�Ƀt���O�𗧂Ă�B
	ld	a,(bc)
	or	e
	ld	(bc),a

	ld	b,d		; Breg�ɕ��A�B
	ld	d, 40h	; ���v���[���Z�o�p (RGB�v���[���ɏ����ނ��� 040h)

	BLEND_BR_HL_ADD_B_D	; 0

	; �I������G�v���[���̈ʒu���w���Ă���B

	ret

;---------------------------------------------------------------;
; Blue Red 2���C���`��
; ����
;	HLreg: �L�����f�[�^
;	BCreg: �`��VRAM�A�h���X
;	Ereg: �r�b�g���C���f�[�^
;---------------------------------------------------------------;
rc_image_br_02:
	ld	d,b		; Dreg��Breg���o�b�t�@�B

	ld	a,b
	and	07h
	add	a, BITLINE_BUFFER0 >> 8
	ld	b,a

	ld	a,(bc)
	and	e
	jp	nz, rc_blend_br_02

	ld	a,(bc)	; BitLine�Ƀt���O�𗧂Ă�B
	or	e
	ld	(bc),a

	ld	b,d		; Breg�ɕ��A�B

; wirte
	ld	de, 040c8h	; Dreg��Blue��Red, Ereg��-40+8�̒l�B

	; OUTI�p��+1���Ă����B
	inc b

	; Blue�v���[���A�h���X(H)��Areg�ɂ��c���B
	ld	a,b

	jp	br_write_02

rc_blend_br_02:
	ld	a,(bc)	; BitLine�Ƀt���O�𗧂Ă�B
	or	e
	ld	(bc),a

	ld	b,d		; Breg�ɕ��A�B
	ld	d, 40h	; ���v���[���Z�o�p (RGB�v���[���ɏ����ނ��� 040h)

	jp	br_blend_02

;---------------------------------------------------------------;
; Blue Red 3���C���`��
; ����
;	HLreg: �L�����f�[�^
;	BCreg: �`��VRAM�A�h���X
;	Ereg: �r�b�g���C���f�[�^
;---------------------------------------------------------------;
rc_image_br_03:
	ld	d,b		; Dreg��Breg���o�b�t�@�B

	ld	a,b
	and	07h
	add	a, BITLINE_BUFFER0 >> 8
	ld	b,a

	ld	a,(bc)
	and	e
	jp	nz, rc_blend_br_03

	ld	a,(bc)	; BitLine�Ƀt���O�𗧂Ă�B
	or	e
	ld	(bc),a

	ld	b,d		; Breg�ɕ��A�B

; wirte
	ld	de, 040c8h	; Dreg��Blue��Red, Ereg��-40+8�̒l�B

	; OUTI�p��+1���Ă����B
	inc b

	; Blue�v���[���A�h���X(H)��Areg�ɂ��c���B
	ld	a,b

	jp	br_write_03

rc_blend_br_03:
	ld	a,(bc)	; BitLine�Ƀt���O�𗧂Ă�B
	or	e
	ld	(bc),a

	ld	b,d		; Breg�ɕ��A�B
	ld	d, 40h	; ���v���[���Z�o�p (RGB�v���[���ɏ����ނ��� 040h)

	jp	br_blend_03

;---------------------------------------------------------------;
; Blue Green 4���C���`��
; ����
;	HLreg: �L�����f�[�^
;	BCreg: �`��VRAM�A�h���X
;	Ereg: �r�b�g���C���f�[�^
;---------------------------------------------------------------;
rc_image_br_04:
	ld	d,b		; Dreg��Breg���o�b�t�@�B

	ld	a,b
	and	07h
	add	a, BITLINE_BUFFER0 >> 8
	ld	b,a

	ld	a,(bc)
	and	e
	jp	nz, rc_blend_br_04

	ld	a,(bc)	; BitLine�Ƀt���O�𗧂Ă�B
	or	e
	ld	(bc),a

	ld	b,d		; Breg�ɕ��A�B

; wirte
	ld	de, 040c8h	; Dreg��Blue��Red, Ereg��-40+8�̒l�B

	; OUTI�p��+1���Ă����B
	inc b

	; Blue�v���[���A�h���X(H)��Areg�ɂ��c���B
	ld	a,b

	jp	br_write_04

rc_blend_br_04:
	ld	a,(bc)	; BitLine�Ƀt���O�𗧂Ă�B
	or	e
	ld	(bc),a

	ld	b,d		; Breg�ɕ��A�B
	ld	d, 40h	; ���v���[���Z�o�p (RGB�v���[���ɏ����ނ��� 040h)

	jp	br_blend_04

;---------------------------------------------------------------;
; Blue Green 5���C���`��
; ����
;	HLreg: �L�����f�[�^
;	BCreg: �`��VRAM�A�h���X
;	Ereg: �r�b�g���C���f�[�^
;---------------------------------------------------------------;
rc_image_br_05:
	ld	d,b		; Dreg��Breg���o�b�t�@�B

	ld	a,b
	and	07h
	add	a, BITLINE_BUFFER0 >> 8
	ld	b,a

	ld	a,(bc)
	and	e
	jp	nz, rc_blend_br_05

	ld	a,(bc)	; BitLine�Ƀt���O�𗧂Ă�B
	or	e
	ld	(bc),a

	ld	b,d		; Breg�ɕ��A�B

; wirte
	ld	de, 040c8h	; Dreg��Blue��Red, Ereg��-40+8�̒l�B

	; OUTI�p��+1���Ă����B
	inc b

	; Blue�v���[���A�h���X(H)��Areg�ɂ��c���B
	ld	a,b

	jp	br_write_05

rc_blend_br_05:
	ld	a,(bc)	; BitLine�Ƀt���O�𗧂Ă�B
	or	e
	ld	(bc),a

	ld	b,d		; Breg�ɕ��A�B
	ld	d, 40h	; ���v���[���Z�o�p (RGB�v���[���ɏ����ނ��� 040h)

	jp	br_blend_05

;---------------------------------------------------------------;
; Blue Green 6���C���`��
; ����
;	HLreg: �L�����f�[�^
;	BCreg: �`��VRAM�A�h���X
;	Ereg: �r�b�g���C���f�[�^
;---------------------------------------------------------------;
rc_image_br_06:
	ld	d,b		; Dreg��Breg���o�b�t�@�B

	ld	a,b
	and	07h
	add	a, BITLINE_BUFFER0 >> 8
	ld	b,a

	ld	a,(bc)
	and	e
	jp	nz, rc_blend_br_06

	ld	a,(bc)	; BitLine�Ƀt���O�𗧂Ă�B
	or	e
	ld	(bc),a

	ld	b,d		; Breg�ɕ��A�B

; wirte
	ld	de, 040c8h	; Dreg��Blue��Red, Ereg��-40+8�̒l�B

	; OUTI�p��+1���Ă����B
	inc b

	; Blue�v���[���A�h���X(H)��Areg�ɂ��c���B
	ld	a,b

	jp	br_write_06

rc_blend_br_06:
	ld	a,(bc)	; BitLine�Ƀt���O�𗧂Ă�B
	or	e
	ld	(bc),a

	ld	b,d		; Breg�ɕ��A�B
	ld	d, 40h	; ���v���[���Z�o�p (RGB�v���[���ɏ����ނ��� 040h)

	jp	br_blend_06

;---------------------------------------------------------------;
; Blue Green 7���C���`��
; ����
;	HLreg: �L�����f�[�^
;	BCreg: �`��VRAM�A�h���X
;	Ereg: �r�b�g���C���f�[�^
;---------------------------------------------------------------;
rc_image_br_07:
	ld	d,b		; Dreg��Breg���o�b�t�@�B

	ld	a,b
	and	07h
	add	a, BITLINE_BUFFER0 >> 8
	ld	b,a

	ld	a,(bc)
	and	e
	jp	nz, rc_blend_br_07

	ld	a,(bc)	; BitLine�Ƀt���O�𗧂Ă�B
	or	e
	ld	(bc),a

	ld	b,d		; Breg�ɕ��A�B

; wirte
	ld	de, 040c8h	; Dreg��Blue��Red, Ereg��-40+8�̒l�B

	; OUTI�p��+1���Ă����B
	inc b

	; Blue�v���[���A�h���X(H)��Areg�ɂ��c���B
	ld	a,b

	jp	br_write_07

rc_blend_br_07:
	ld	a,(bc)	; BitLine�Ƀt���O�𗧂Ă�B
	or	e
	ld	(bc),a

	ld	b,d		; Breg�ɕ��A�B
	ld	d, 40h	; ���v���[���Z�o�p (RGB�v���[���ɏ����ނ��� 040h)

	jp	br_blend_07

;---------------------------------------------------------------;
; Blue Green 8���C���`��
; ����
;	HLreg: �L�����f�[�^
;	BCreg: �`��VRAM�A�h���X
;	Ereg: �r�b�g���C���f�[�^
;---------------------------------------------------------------;
rc_image_br_08:
	ld	d,b		; Dreg��Breg���o�b�t�@�B

	ld	a,b
	and	07h
	add	a, BITLINE_BUFFER0 >> 8
	ld	b,a

	ld	a,(bc)
	and	e
	jp	nz, rc_blend_br_08

	ld	a, 0ffh; BitLine�Ƀt���O�𗧂Ă�B
	ld	(bc),a

	ld	b,d		; Breg�ɕ��A�B

; wirte
	ld	de, 040c8h	; Dreg��Blue��Red, Ereg��-40+8�̒l�B

	; OUTI�p��+1���Ă����B
	inc b

	; Blue�v���[���A�h���X(H)��Areg�ɂ��c���B
	ld	a,b

	OUT_BR_HL_ADD_D		; 0
	ADD_B_E
br_write_07:
	OUT_BR_HL_ADD_D		; 1
	ADD_B_E
br_write_06:
	OUT_BR_HL_ADD_D		; 2
	ADD_B_E
br_write_05:
	OUT_BR_HL_ADD_D		; 3
	ADD_B_E
br_write_04:
	OUT_BR_HL_ADD_D		; 4
	ADD_B_E
br_write_03:
	OUT_BR_HL_ADD_D		; 5
	ADD_B_E
br_write_02:
	OUT_BR_HL_ADD_D		; 6
	ADD_B_E
br_write_01:
	OUT_BR_HL_ADD_D		; 7

	; �Ō��R�v���[������G�v���[��,������ OUTI�p��+1���Ă����̂����炷�B
	add	a, 040h-1
	ld	b,a

	ret

rc_blend_br_08:
	ld	a, 0ffh		; BitLine�Ƀt���O�𗧂Ă�B
	ld	(bc),a

	ld	b,d		; Breg�ɕ��A�B
	ld	d, 40h	; ���v���[���Z�o�p (RGB�v���[���ɏ����ނ��� 040h)

	BLEND_BR_HL_ADD_B_D	; 0
	ADD_B_88
br_blend_07:
	BLEND_BR_HL_ADD_B_D	; 1
	ADD_B_88
br_blend_06:
	BLEND_BR_HL_ADD_B_D	; 2
	ADD_B_88
br_blend_05:
	BLEND_BR_HL_ADD_B_D	; 3
	ADD_B_88
br_blend_04:
	BLEND_BR_HL_ADD_B_D	; 4
	ADD_B_88
br_blend_03:
	BLEND_BR_HL_ADD_B_D	; 5
	ADD_B_88
br_blend_02:
	BLEND_BR_HL_ADD_B_D	; 6
	ADD_B_88
br_blend_01:
	BLEND_BR_HL_ADD_B_D	; 7

	; �I������G�v���[���̈ʒu���w���Ă���B

	ret


;----
;	END

;---------------------------------------------------------------;
;	Copyright (c) 2019 render_b16.asm
;	This software is released under the MIT License.
;	http://opensource.org/licenses/mit-license.php
;---------------------------------------------------------------;

;---------------------------------------------------------------;
; B �c16pixel y=0:
;---------------------------------------------------------------;
render_b16_y0:
	; 8pixel�`�悷��B
	ld		e,0ffh
	call	rc_image_b8

	; VRAM�����̒i�ցB
	ADD_BC_4828

	; 8pixel�`�悷��B
	ld		e,0ffh
	jp		rc_image_b8

;---------------------------------------------------------------;
; B �c16pixel y=1:
;---------------------------------------------------------------;
render_b16_y1:
	ld		e,0feh
	call	rc_image_b7

	; VRAM�����̒i�ցB
	ADD_BC_4828

	ld		e, 0ffh
	call	rc_image_b8

	; VRAM�����̒i�ցB
	ADD_BC_4828

	ld		e, 01h
	jp		rc_image_b1


;---------------------------------------------------------------;
; B �c16pixel y=2:
;---------------------------------------------------------------;
render_b16_y2:
	ld		e,0fch
	call	rc_image_b6

	; VRAM�����̒i�ցB
	ADD_BC_4828

	ld		e,0ffh
	call	rc_image_b8

	; VRAM�����̒i�ցB
	ADD_BC_4828

	ld		e,03h
	jp		rc_image_b2


;---------------------------------------------------------------;
; B �c16pixel y=3:
;---------------------------------------------------------------;
render_b16_y3:
	ld		e,0f8h
	call	rc_image_b5

	; VRAM�����̒i�ցB
	ADD_BC_4828

	ld		e,0ffh
	call	rc_image_b8

	; VRAM�����̒i�ցB
	ADD_BC_4828

	ld		e,07h
	jp		rc_image_b3

;---------------------------------------------------------------;
; B �c16pixel y=4:
;---------------------------------------------------------------;
render_b16_y4:
	ld		e,0f0h
	call	rc_image_b4

	; VRAM�����̒i�ցB
	ADD_BC_4828

	ld		e,0ffh
	call	rc_image_b8

	; VRAM�����̒i�ցB
	ADD_BC_4828

	ld		e,0fh
	jp		rc_image_b4

;---------------------------------------------------------------;
; B �c16pixel y=5:
;---------------------------------------------------------------;
render_b16_y5:
	ld		e,0e0h
	call	rc_image_b3

	; VRAM�����̒i�ցB
	ADD_BC_4828

	ld		e,0ffh
	call	rc_image_b8

	; VRAM�����̒i�ցB
	ADD_BC_4828

	ld		e,01fh
	jp		rc_image_b5


;---------------------------------------------------------------;
; B �c16pixel y=6:
;---------------------------------------------------------------;
render_b16_y6:
	ld		e,0c0h
	call	rc_image_b2

	; VRAM�����̒i�ցB
	ADD_BC_4828

	ld		e,0ffh
	call	rc_image_b8

	; VRAM�����̒i�ցB
	ADD_BC_4828

	ld		e,03fh
	jp		rc_image_b6

;---------------------------------------------------------------;
; B �c16pixel y=7:
;---------------------------------------------------------------;
render_b16_y7:
	ld		e,080h
	call	rc_image_b1

	; VRAM�����̒i�ցB
	ADD_BC_4828

	ld		e,0ffh
	call	rc_image_b8

	; VRAM�����̒i�ցB
	ADD_BC_4828

	ld		e,07fh
	jp		rc_image_b7

;----
;	END

;---------------------------------------------------------------;
;	Copyright (c) 2019 clear_16.asm
;	This software is released under the MIT License.
;	http://opensource.org/licenses/mit-license.php
;---------------------------------------------------------------;

;---------------------------------------------------------------;
; Ypos=0 �c16pixel�̏���
clear_size16_y0:
	; 8pixel �����B
	call	clear_image_08

	; VRAM�����̒i�� (and 07���s������y:0-7�ł����̒i��)
	ADD_BC_0028_AND_C7

	; 8pixel ����
	jp	clear_image_08

;---------------------------------------------------------------;
; Ypos=1 �c16pixel�̏���
clear_size16_y1:
	ld	de, 0fe01h
	call clear_image_07

	; VRAM�����̒i�� (and 07���s������y:0-7�ł����̒i��)
	ADD_BC_0028_AND_C7

	; 8pixel ����
	call clear_image_08

	; VRAM�����̒i�� (and 07���s������y:0-7�ł����̒i��)
	ADD_BC_0028_AND_C7

	ld	e, 0feh
	jp	clear_image_01

;---------------------------------------------------------------;
; Ypos=2 �c16pixel�̏���
clear_size16_y2:
	ld	de,0fc03h
	call clear_image_06

	; VRAM�����̒i�� (and 07���s������y:0-7�ł����̒i��)
	ADD_BC_0028_AND_C7

	; 8pixel ����
	call clear_image_08

	; VRAM�����̒i�� (and 07���s������y:0-7�ł����̒i��)
	ADD_BC_0028_AND_C7

	ld	e,0fch
	jp	clear_image_02

;---------------------------------------------------------------;
; Ypos=3 �c16pixel�̏���
clear_size16_y3:
	ld	de,0f807h
	call clear_image_05

	; VRAM�����̒i�� (and 07���s������y:0-7�ł����̒i��)
	ADD_BC_0028_AND_C7

	; 8pixel ����
	call clear_image_08

	; VRAM�����̒i�� (and 07���s������y:0-7�ł����̒i��)
	ADD_BC_0028_AND_C7

	ld	e,0f8h
	jp	clear_image_03

;---------------------------------------------------------------;
; Ypos=4 �c16pixel�̏���
clear_size16_y4:
	ld	de,0f00fh
	call clear_image_04

	; VRAM�����̒i�� (and 07���s������y:0-7�ł����̒i��)
	ADD_BC_0028_AND_C7

	; 8pixel ����
	call clear_image_08

	; VRAM�����̒i�� (and 07���s������y:0-7�ł����̒i��)
	ADD_BC_0028_AND_C7

	ld	de,00ff0h
	jp	clear_image_04

;---------------------------------------------------------------;
; Ypos=5 �c16pixel�̏���
clear_size16_y5:
	ld	de,0e01fh
	call clear_image_03

	; VRAM�����̒i�� (and 07���s������y:0-7�ł����̒i��)
	ADD_BC_0028_AND_C7

	call clear_image_08

	; VRAM�����̒i�� (and 07���s������y:0-7�ł����̒i��)
	ADD_BC_0028_AND_C7

	ld	de, 01fe0h
	jp	clear_image_05

;---------------------------------------------------------------;
; Ypos=6 �c16pixel�̏���
clear_size16_y6:
	ld	e,03fh
	call clear_image_02

	; VRAM�����̒i�� (and 07���s������y:0-7�ł����̒i��)
	ADD_BC_0028_AND_C7

	call clear_image_08

	; VRAM�����̒i�� (and 07���s������y:0-7�ł����̒i��)
	ADD_BC_0028_AND_C7

	ld	de, 03fc0h
	jp	clear_image_06

;---------------------------------------------------------------;
; Ypos=7 �c16pixel�̏���
clear_size16_y7:
	ld	e,07fh
	call clear_image_01

	; VRAM�����̒i�� (and 07���s������y:0-7�ł����̒i��)
	ADD_BC_0028_AND_C7

	call clear_image_08

	; VRAM�����̒i�� (and 07���s������y:0-7�ł����̒i��)
	ADD_BC_0028_AND_C7

	ld	de,07f80h
	jp  clear_image_07

;----
;	END


;---------------------------------------------------------------;
;	Copyright (c) 2019 data_work.asm
;	This software is released under the MIT License.
;	http://opensource.org/licenses/mit-license.php
;---------------------------------------------------------------;

;---------------------------------------------------------------;
;	���[�N��f�[�^���A���C�����g�̊֌W�ł܂Ƃ߂Ă����t�@�C��
;---------------------------------------------------------------;

;---------------------------------------------------------------;
;	�t���b�v�p�e�L�X�g�`�惏�[�N
;---------------------------------------------------------------;
align 64
flip_text_render_buff:
	ds	4*16

;---------------------------------------------------------------;
;---------------------------------------------------------------;
align 256
dir_table:
	; �ی�0 +dx,+dy
        db      04h, 04h, 04h, 04h, 04h, 04h, 04h, 04h  ; 00
        db      00h, 02h, 03h, 03h, 03h, 03h, 04h, 04h  ; 08
        db      00h, 01h, 02h, 03h, 03h, 03h, 03h, 03h  ; 10
        db      00h, 01h, 01h, 02h, 02h, 03h, 03h, 03h  ; 18
        db      00h, 01h, 01h, 02h, 02h, 02h, 03h, 03h  ; 20
        db      00h, 01h, 01h, 01h, 02h, 02h, 02h, 02h  ; 28
        db      00h, 00h, 01h, 01h, 01h, 02h, 02h, 02h  ; 30
        db      00h, 00h, 01h, 01h, 01h, 02h, 02h, 02h  ; 38

	; �ی�1 +dx,-dy
        db      04h, 04h, 04h, 04h, 04h, 04h, 04h, 04h  ; 00
        db      08h, 06h, 05h, 05h, 05h, 05h, 04h, 04h  ; 08
        db      08h, 07h, 06h, 05h, 05h, 05h, 05h, 05h  ; 10
        db      08h, 07h, 07h, 06h, 06h, 05h, 05h, 05h  ; 18
        db      08h, 07h, 07h, 06h, 06h, 06h, 05h, 05h  ; 20
        db      08h, 07h, 07h, 07h, 06h, 06h, 06h, 06h  ; 28
        db      08h, 08h, 07h, 07h, 07h, 06h, 06h, 06h  ; 30
        db      08h, 08h, 07h, 07h, 07h, 06h, 06h, 06h  ; 38

	; �ی�3 -dx,+dy
        db      0Ch, 0Ch, 0Ch, 0Ch, 0Ch, 0Ch, 0Ch, 0Ch  ; 00
        db      00h, 0Eh, 0Dh, 0Dh, 0Dh, 0Dh, 0Ch, 0Ch  ; 08
        db      00h, 0Fh, 0Eh, 0Dh, 0Dh, 0Dh, 0Dh, 0Dh  ; 10
        db      00h, 0Fh, 0Fh, 0Eh, 0Eh, 0Dh, 0Dh, 0Dh  ; 18
        db      00h, 0Fh, 0Fh, 0Eh, 0Eh, 0Eh, 0Dh, 0Dh  ; 20
        db      00h, 0Fh, 0Fh, 0Fh, 0Eh, 0Eh, 0Eh, 0Eh  ; 28
        db      00h, 00h, 0Fh, 0Fh, 0Fh, 0Eh, 0Eh, 0Eh  ; 30
        db      00h, 00h, 0Fh, 0Fh, 0Fh, 0Eh, 0Eh, 0Eh  ; 38

	; �ی�2 -dx,-dy
        db      0Ch, 0Ch, 0Ch, 0Ch, 0Ch, 0Ch, 0Ch, 0Ch  ; 00
        db      08h, 0Ah, 0Bh, 0Bh, 0Bh, 0Bh, 0Ch, 0Ch  ; 08
        db      08h, 09h, 0Ah, 0Bh, 0Bh, 0Bh, 0Bh, 0Bh  ; 10
        db      08h, 09h, 09h, 0Ah, 0Ah, 0Bh, 0Bh, 0Bh  ; 18
        db      08h, 09h, 09h, 0Ah, 0Ah, 0Ah, 0Bh, 0Bh  ; 20
        db      08h, 09h, 09h, 09h, 0Ah, 0Ah, 0Ah, 0Ah  ; 28
        db      08h, 08h, 09h, 09h, 09h, 0Ah, 0Ah, 0Ah  ; 30
        db      08h, 08h, 09h, 09h, 09h, 0Ah, 0Ah, 0Ah  ; 38

;---------------------------------------------------------------;
;	�L�����N�^�֘A�̃��[�N
;---------------------------------------------------------------;

CHR_KIND	equ	00h
CHR_PATTERN	equ	01h
CHR_STEP	equ	02h

; X���W�͐����� 9bit: ������: 7bit�ōs���Ă݂�B
; �\���͐��������g���A�����蔻��͐������̏��8bit(=2�̔{��)�ō���������B
CHR_POSXL	equ	03h
CHR_POSXH	equ	04h

; ���蔻��p�̃T�C�Y(X), �������̂���CHR_POSXH�̎��ɔz�u
; POSX�������Ɣ�����s�����߁A���T�C�Y�ł͂Q�{�̕��ɂȂ�B
; ��: 8 �� �����W�}16
CHR_SIZEX	equ	05h

; Y���W�͐����� 8bit: ������: 8bit�ōs���B
; �\��,���蔻��͐��������g���B
; POSY�Ɣ�����s�����߁A���T�C�Y�̍����ɂȂ�B
; ��: 8 �� �����W�}8
CHR_POSYL	equ	06h
CHR_POSYH	equ	07h

; ���蔻��p�̃T�C�Y(Y), �������̂���CHR_POSYH�̎��ɔz�u
CHR_SIZEY	equ	08h

; �ėp���[�N
CHR_WORK0	equ	09h
CHR_WORK1	equ	0ah
CHR_WORK2	equ	0bh
CHR_WORK3	equ	0ch

; �X�R�A�^�C�v (���g�p)
SCORE_TYPE	equ	0dh

; �����p�����[�^ (���g�p)
CHR_PARAM	equ	0eh

CHR_SIZE	equ	010h


;---------------------------------------------------------------;
CHARA_NUM	equ	(31)

align 256

chara_work:
	ds	CHR_SIZE * CHARA_NUM

;---------------------------------------------------------------;
;	�e�L�X�g�`��p
;---------------------------------------------------------------;
; �L�����N�^��
num_buff:
	db	00h,00h

;---------------------------------------------------------------;
;---------------------------------------------------------------;

;	END


; @name X1SGLBASE
; @lib x1sgl_lsx
; @works VRAM_ADRS_TBL_H_BUF:256, VRAM_ADRS_TBL_L_BUF:256, BITLINE_BUFFER0:1024, BITLINE_BUFFER1:1024, SGLSPRDISPBUF:32
; @works_align 256
	; SGLBASE

SGL_VRCALC:
    ; HL = X, DE = Y
    LD H,E

    PUSH DE
    LD C,L
    LD B,8
    LD E,H
    LD D,0
    LD H,40	; WIDTH 40専用
    LD L,D
    .LOC2
    ADD HL,HL
    JR NC,.LOC3
    ADD HL,DE
    .LOC3
    DJNZ .LOC2
    ADD HL,BC
    POP DE

    ; to text vram address
    LD C,L
    LD B,H
    LD	A,B
    OR	038H
    LD	B,A

    RET

; @name SGL_INIT
; @calls X1SGLINCLUDE,X1SGLBASE
; @lib x1sgl_lsx

	call	fill_text_vram
	call	fill_attr_vram

	call	set_crtc40

	call	vram_priority
	call	vram_palette_init

	call	clear_graphic_vram_b
	call	clear_graphic_vram_r
	call	clear_graphic_vram_g

	; VRAMアドレステーブルを作成。
	call	create_vram_adrs_tbl

	call	init_screen

	call	init_bitline

	call	init_chara_manager

	; キャラクタ消去ワーク初期化
	call	init_clear_char_work

;	call	init_input

;	call	init_test_title
;	call	render_chara_num
;	call	render_fps_mode

	ret

; LSX-Dodgers版: VRAM_ADRS_TBL/BITLINE_BUFFER は X1SGLBASE の @works で確保

; @name SGL_DEFPAT
; @calls X1SGLINCLUDE,X1SGLBASE
; @lib x1sgl_lsx
	; hl = pat num , de = address
	ex de,hl
	sla e
	ld c,e
	jp cdm_set_data8_bank_main

; @name SGL_SPRCREATE
; @calls X1SGLINCLUDE,X1SGLBASE
; @lib x1sgl_lsx
	; hl = pattern num, de = kind
	push iy
	push de
	; 空きワークを探す
 	call	find_chara_work_iy
	jp	nz,sgl_error

	pop de

	; パターン番号は2倍しないと駄目
	sla l

	; ザッと初期化
	ld	(iy+CHR_KIND), e
	ld	(iy+CHR_PATTERN), l

	ld	(iy+CHR_POSXL),0
	ld	(iy+CHR_POSYL),0

	ld	(iy+CHR_POSXH),0
	ld	(iy+CHR_POSYH),0

	ld	(iy+CHR_WORK0),0
	ld	(iy+CHR_WORK1),0
	ld	(iy+CHR_WORK2),0

	; iyのアドレスがスプライトハンドルになる
	push iy
	pop hl

	pop iy
	ret

sgl_error:
	pop de
	pop iy

	; 0だとエラー
	ld hl,0
	ret

; @name SGL_SPRDESTROY
; @calls X1SGLINCLUDE,X1SGLBASE
; @lib x1sgl_lsx
	; hl = sprite handle
	; KIND & PATTERNを0にする
	xor a
	ld	(hl), a
	inc	hl
	ld	(hl), a
	ret

; @name SGL_SPRSET
; @calls X1SGLINCLUDE,X1SGLBASE
; @lib x1sgl_lsx
	; HL = sprite handle, DE = data address
	ex de,hl
	ld bc,CHR_SIZE
	ldir
	ret

; @name SGL_SPRPAT
; @calls X1SGLINCLUDE,X1SGLBASE
; @lib x1sgl_lsx
	; HL = sprite handle, DE = pattern number
	; HLに入っているワークのCHR_PATTERNを書き換える
	sla e
	inc hl
	ld (hl),e
	ret

; @name SGL_SPRMOVE
; @calls X1SGLINCLUDE,X1SGLBASE
; @lib x1sgl_lsx
	; HLに入っているワークのX,Yを書き換える
	INC HL
	INC HL
	INC HL
	LD (HL),E
	INC HL
	LD (HL),D
	INC HL
	INC HL
	LD (HL),C
	INC HL
	LD (HL),B
	RET

; @name SGL_SPRDISP
; @calls X1SGLINCLUDE,X1SGLBASE
; @lib x1sgl_lsx
    ; 表示/非表示の設定
    ; HL = sprite handle, DE = 0 = nodisp 1 = disp
    inc hl
    ld a,(hl)
    and $fe
    or e
    ; 表示を0、非表示を1にするため、反転させる
    xor 1
    ld (hl),a
    ret

; @name SGL_FPSMODE
; @calls X1SGLINCLUDE,X1SGLBASE
; @lib x1sgl_lsx
	ld a,l
	ld (fps_mode),a
	ret

; @name SGL_VSYNC
; @calls X1SGLINCLUDE,X1SGLBASE
; @lib x1sgl_lsx
	; キャラクタ処理
	; call	update_chara_manager
	push iy

	; キャラクタ描画
	call	draw_chara_manager

	call	wait_vsync_fps

	; call	disp_frame_dropout

	call	flip_screen

	pop iy
	ret

; @name SGL_PRINT
; @calls X1SGLINCLUDE,X1SGLBASE,AT_VRCALC
; @lib x1sgl_lsx
	; HL = x, DE = y, BC = STRING ADDRESS
	PUSH BC
	CALL SGL_VRCALC

	; 描画ページに描画する
	LD A,(flip_render_w)
	OR B
	LD B,A

	POP HL
	; HL = string address , BC = vram address
	jp render_text

; @name SGL_PRINT2
; @calls X1SGLINCLUDE,X1SGLBASE,AT_VRCALC
; @lib x1sgl_lsx
	; HL = x, DE = y, BC = STRING ADDRESS
	PUSH BC
	CALL SGL_VRCALC
	POP HL
	; HL = string address , BC = vram address
	jp render_text_2page

