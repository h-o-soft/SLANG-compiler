;
; Kanji
;
; JIS�R�[�h��Ŋi�[����Ă��� JIS X 0208(1978�N����)
; JIS83/JIS90�̉���͊܂܂Ȃ�
; NEC�O��(98�O��)�͊܂܂Ȃ�
; http://www.asahi-net.or.jp/~ax2s-kmtn/ref/jisx0208.html
;
; ���p���ɂ� 0x80-0xA0,0xE0-0xFF �ɔ��p�Ђ炪�ȃt�H���g�������Ă���
; ����� Shift-JIS �� 1�o�C�g�ڂƔ��̈�ɂ���
;
; mk2�ȍ~�͑�ꐅ���W������
; MR,FH,MH,FA,MA,MA2,FE,FE2,MC �͑�񐅏��W������
;
;$E8(R/W) ��ꐅ�� (R=�ǂݏo��/W=���ʃA�h���X�w��) �ǂݏo���͑S�p�����̉E��,���p1/4�p�����̋������C��
;$E9(R/W) ��ꐅ�� (R=�ǂݏo��/W=��ʃA�h���X�w��) �ǂݏo���͑S�p�����̍���,���p1/4�p�����̊���C��
;$EA(W)   �ǂݏo���J�n�T�C�� �������ޒl�͉��ł��ǂ��B�������񂾌� 8clk �E�F�C�g���K�v
;$EB(W)   �ǂݏo���I���T�C�� �������ޒl�͉��ł��ǂ�
;$EC(R/W) ��񐅏� (R=�ǂݏo��/W=���ʃA�h���X�w��) �ǂݏo���͑S�p�����̉E��
;$ED(R/W) ��񐅏� (R=�ǂݏo��/W=��ʃA�h���X�w��) �ǂݏo���͑S�p�����̍���
;
;$EA,$EB �̏������݂���уE�F�C�g�� FR �ȍ~�ł͕s�v
;�m����ROM�A�h���X�w��ł���悤�ɂ��邽�߁A����� $EB �ɒl���o�͂��Ă�������
;
;����ROM�A�h���X�w��͉��ʁE��ʂ𖈉񏑂����ޕK�v�͖����B
;�ŏ��̈�x������ʂ������āA���ʃA�h���X��������+1����������ł����Ηǂ��B
;
;
;�������R�[�h����A�h���X�ւ̕ϊ�
;�@���p(0020 - 00FF)
;�@   FEDCBA98 76543210
;�@   00000000 nnnnnnnn
;�@-> 00000nnn nnnnn000
;�@
;�@1/4�p(0100 - 01FF)
;�@   FEDCBA98 76543210
;�@   00000001 nnnnnnnn
;�@-> 000010nn nnnnnn00
;�@
;�@�񊿎�(2120 - 27FF)
;�@   FEDCBA98 76543210
;�@   00100aaa 0bbccccc
;�@-> 00bbaaac cccc0000
;�@
;�@��ꐅ��(3020-4F5F)
;�@   FEDCBA98 76543210
;�@   000aaaaa 0bbccccc
;�@-> bbaaaaac cccc0000
;�@
;�@��񐅏�(5020-6F7F)
;�@   FEDCBA98 76543210
;�@   00a0bbbb 0ccddddd
;�@-> ccabbbbd dddd0000
;�@
;�@��񐅏�(7020-705F)
;�@   FEDCBA98 76543210
;�@   01110aaa 0bbccccc
;�@-> 00bbaaac cccc0000
;
;http://www.maroon.dti.ne.jp/youkan/pc88/index.html
;->����ROM ��ϕ�����₷��
;


#LIB KANJIPUT

; 0�ɂ����640x200���[�h�A1�`3�ɂ����320x200���[�h�Ŏw�肵���F�ŕ`�悳��܂�(�e�L�g�[)
KANJICOLOR	equ	0
; 0=�ʏ�`��A1=OR�`��A2=1�s��΂��`��(�ǂ߂Ȃ�)
; ��320x200�ł͒ʏ�`��ȊO�͓��삵�܂���
KANJIMODE	equ	0

PutKanji:
	ld			a,(hl)
	inc			hl
	or			a
	ret			z						;�I�[=0
	cp			$0D
	jr			z,.crlf

	ld			d,0
	ld			e,a
	xor			$20						;ShiftJIS 1�o�C�g�ڂ� 0x81 �` 0x9F �܂��� 0xE0 �` 0xFC
	sub			$A1
	cp			$3C						; if ((c ^ 0x20) - 0xA1 < 0x3C)
	jr			nc,.half

	ld			d,e
	ld			e,(hl)
	inc			hl
	push		hl

	call		KanjiXY2VRAM
	call		SJIS2JIS
	call		JIS2ADR
	;call	Zenkaku3flip
#if KANJIMODE == 2
	call	Zenkaku2E
#elif KANJIMODE == 1
	call	Zenkaku2OR
#else
	call	Zenkaku
#endif

	ld			hl,KanjiX
	inc			(hl)
	inc			(hl)
#if KANJICOLOR != 0
	inc			(hl)
	inc			(hl)
#endif
;	ld			a,(hl)
;	; 80���傫����Ή��s
;	cp			80
;	jr			c,.next
;	pop			hl
;	jp			.crlf
;.next:
	pop			hl
	jp			PutKanji

.crlf
	xor			a
	ld			(KanjiX),a
	ld			a,(KanjiY)
	inc			a
#if KANJIMODE == 0
	inc		a
#endif
	ld			(KanjiY),a
	jp			PutKanji

.half
	ld			a,e						;1�o�C�g��=1 �̎��� 1/4�p�Ƃ���
	dec			a
	jr			z,.quarter
	push		hl
	ex			de,hl					;0000-00FF ���p
	add			hl,hl
	add			hl,hl
	add			hl,hl
	ex			de,hl

	call		KanjiXY2VRAM
	; call	Hankaku3flip
#if KANJIMODE == 2
	call	Hankaku2E
#elif KANJIMODE == 1
	call	Hankaku2OR
#else
	call	Hankaku
#endif

	ld			hl,KanjiX
	inc			(hl)
#if KANJICOLOR != 0
	inc			(hl)
#endif
	pop			hl
	jp			PutKanji

.quarter								;0100-01FF 1/4�p
	ld			d,2
	ld			e,(hl)
	inc			hl
	push		hl
	ex			de,hl
	add			hl,hl
	add			hl,hl
	ex			de,hl

	call		KanjiXY2VRAM
	;call	Quarter3flip
#if KANJIMODE == 2
	call	Quarter2E
#elif KANJIMODE == 1
	call	Quarter2OR
#else
	call	Quarter
#endif

	ld			hl,KanjiX
	inc			(hl)
#if KANJICOLOR != 0
	inc			(hl)
#endif
	pop			hl
	jp			PutKanji

;--------------------------------------------------------------------------------------------------�\��
;���[�v�W�Jetc.
Zenkaku:
	ld			hl,(KanjiVRAM)
	out			(c),d					;$E9 ��ʃA�h���X���������ނ̂͏��񂾂��ŗǂ�
	dec			c
	ld			a,e
	ld			de,78
	ld			b,16*3
.loop
	out			(c),a					;$E8 ���ʃA�h���X
	out			($EA),a					;����ROM�ǂݏo���T�C�� FR/MR �ȍ~�̓E�F�C�g�܂ߕs�v
	inc			a
	inc			c						;8clk wait

#if KANJICOLOR != 0
	push af
	push bc
	in			a,(c)
	; a��8�r�b�g����bc�ɓW�J���ĕԂ�
	call	Wide2byte

	ld	(hl),b
	inc	hl
	ld	(hl),c
	inc	hl

	ld	c,$e8
	in	a,(c)
	; a��8�r�b�g����bc�ɓW�J���ĕԂ�
	call	Wide2byte

	ld	(hl),b
	inc	hl
	ld	(hl),c
	dec	hl

	add	hl,de

	pop bc
	dec	c
	pop af

	dec b
	dec b
#else
	ini									;$E9
	dec			c
	ini									;$E8
	add			hl,de
#endif

	out			($EB),a					;�ǂݏo���I���T�C�� FR/MR �ȍ~�͕s�v
	djnz		.loop
	ret

Hankaku:
	ld			hl,(KanjiVRAM)
	ld			c,$E9
	out			(c),d					;$E9 ��ʃA�h���X���������ނ̂͏��񂾂��ŗǂ�
	dec			c
	ld			a,e
	ld			de,79
	ld			b,8*3
.loop
	out			(c),a					;$E8 ���ʃA�h���X
	out			($EA),a					;����ROM�ǂݏo���T�C�� FR/MR �ȍ~�̓E�F�C�g�܂ߕs�v
	inc			a
	inc			c						;8clk wait

#if KANJICOLOR != 0
	push af
	push bc
	in			a,(c)
	; a��8�r�b�g����bc�ɓW�J���ĕԂ�
	call	Wide2byte

	ld	(hl),b
	inc	hl
	ld	(hl),c

	add	hl,de

	ld	c,$e8
	in	a,(c)
	; a��8�r�b�g����bc�ɓW�J���ĕԂ�
	call	Wide2byte

	ld	(hl),b
	inc	hl
	ld	(hl),c

	add	hl,de

	pop bc
	dec	c
	pop af

	dec b
	dec b
#else
	ini									;$E9
	add			hl,de
	dec			c
	ini									;$E8
	add			hl,de
#endif

	out			($EB),a					;�ǂݏo���I���T�C�� FR/MR �ȍ~�͕s�v
	djnz		.loop
	ret


#if KANJICOLOR != 0
; a�̒l��bc�ɓW�J���ĕԂ�
Wide2byte:
	push de

	VramColor:		; VramColor+1��0�`3�ŏ���������(�f�t�H���g��3)
	ld	d,a
	ld	e,KANJICOLOR
	ld	c,1
.halftop
	ld	a,0

	; 7bit
	rlc	d
	jr	nc,.nodot1
	or	e
.nodot1
	sla	a
	sla	a
	; 6bit
	rlc	d
	jr	nc,.nodot2
	or	e
.nodot2
	sla	a
	sla	a
	; 5bit
	rlc	d
	jr	nc,.nodot3
	or	e
.nodot3
	sla	a
	sla	a
	; 4bit
	rlc	d
	jr	nc,.nodot4
	or	e
.nodot4
	; �����d�̏��4�r�b�g��8�r�b�g�ɓW�J�����a�ɓ����Ă���
	bit	0,c
	jr	z,.lower4bit
	ld	b,a
	ld	c,0
	jr	.halftop
.lower4bit
	ld	c,a

	pop de
	ret
#endif


Quarter:
	ld			hl,(KanjiVRAM)
	ld			c,$E9
	out			(c),d					;$E9 ��ʃA�h���X���������ނ̂͏��񂾂��ŗǂ�
	dec			c
	ld			a,e
	ld			de,79
	ld			b,4*3
	jp			Hankaku.loop


;--------------------------------------------------------------------------------------------------�\�� �o���G�[�V����
;1���C����΂��A�/�������C���̂ݍ���8�h�b�g�ŕ`���Ă݂�
Zenkaku2E:
	ld			hl,(KanjiVRAM)
	out			(c),d					;$E9 ��ʃA�h���X���������ނ̂͏��񂾂��ŗǂ�
	dec			c
	ld			a,e
	nop							;inc a �ŋ���
	ld			de,78
	ld			b,8*3
.loop
	out			(c),a					;$E8 ���ʃA�h���X
	out			($EA),a					;����ROM�ǂݏo���T�C�� FR/MR �ȍ~�̓E�F�C�g�܂ߕs�v
	add			a,2
	inc			c						;7+8clk wait

	ini									;$E9
	dec			c
	ini									;$E8
	add			hl,de

	out			($EB),a					;�ǂݏo���I���T�C�� FR/MR �ȍ~�͕s�v
	djnz		.loop
	ret

Hankaku2E:
	ld			hl,(KanjiVRAM)
	ld			c,$E9
	out			(c),d					;$E9 ��ʃA�h���X���������ނ̂͏��񂾂��ŗǂ�
	dec			c
	ld			a,e
	ld			de,80
	ld			b,8*2
.loop
	out			(c),a					;$E8 ���ʃA�h���X
	out			($EA),a					;����ROM�ǂݏo���T�C�� FR/MR �ȍ~�̓E�F�C�g�܂ߕs�v
	inc			a
	inc			c						;7+8clk wait

	ini									;$E9 <- $E8 �Ŋ���C��
	dec			hl
	dec			c
	add			hl,de

	out			($EB),a					;�ǂݏo���I���T�C�� FR/MR �ȍ~�͕s�v
	djnz		.loop
	ret

Quarter2E:
	ld			hl,(KanjiVRAM)
	ld			c,$E9
	out			(c),d					;$E9 ��ʃA�h���X���������ނ̂͏��񂾂��ŗǂ�
	dec			c
	ld			a,e
	ld			de,80
	ld			b,4*2
	jp			Hankaku2E.loop




;�������C���Ɗ���C�����������č���8�h�b�g�ŕ`���Ă݂�
Zenkaku2OR:
	ld			hl,(KanjiVRAM)
	out			(c),d					;$E9 ��ʃA�h���X���������ނ̂͏��񂾂��ŗǂ�
	dec			c
	ld			b,8
.loop
	out			(c),e					;$E8 ���ʃA�h���X
	out			($EA),a					;����ROM�ǂݏo���T�C�� FR/MR �ȍ~�̓E�F�C�g�܂ߕs�v
	inc			e
	inc			c						;8clk wait

	in			d,(c)					;$E9
	dec			c
	in			a,(c)					;$E8

	out			($EB),a					;�ǂݏo���I���T�C�� FR/MR �ȍ~�͕s�v

	out			(c),e					;$E8 ���ʃA�h���X
	out			($EA),a					;����ROM�ǂݏo���T�C�� FR/MR �ȍ~�̓E�F�C�g�܂ߕs�v
	inc			e
	inc			c						;8clk wait

	ex			af,af'
	in			a,(c)					;$E9
	or			d
	ld			(hl),a
	inc			hl
	dec			c
	ex			af,af'
	in			d,(c)					;$E8
	or			d
	ld			(hl),a

	ld			a,79
	add			a,l
	ld			l,a
	adc			a,h
	sub			l
	ld			h,a

	out			($EB),a					;�ǂݏo���I���T�C�� FR/MR �ȍ~�͕s�v
	djnz		.loop
	ret


Hankaku2OR:
	ld			hl,(KanjiVRAM)
	ld			a,d
	out			($E9),a					;$E9 ��ʃA�h���X���������ނ̂͏��񂾂��ŗǂ�
	ld			c,e
	ld			de,80
	ld			b,8
.loop
	ld			a,c
	out			($E8),a					;$E8 ���ʃA�h���X
	out			($EA),a					;����ROM�ǂݏo���T�C�� FR/MR �ȍ~�̓E�F�C�g�܂ߕs�v
	inc			c
	nop									;8clk wait

	in			a,($E9)					;$E9
	ld			(hl),a
	in			a,($E8)					;$E8
	or			(hl)
	ld			(hl),a
	add			hl,de

	out			($EB),a					;�ǂݏo���I���T�C�� FR/MR �ȍ~�͕s�v
	djnz		.loop
	ret

Quarter2OR:
	ld			hl,(KanjiVRAM)
	ld			a,d
	out			($E9),a					;$E9 ��ʃA�h���X���������ނ̂͏��񂾂��ŗǂ�
	ld			c,e
	ld			de,80
	ld			b,4
	jp			Hankaku2OR.loop


;�������C���Ɗ���C����ʁX�� VRAM �v���[���ɕ`���āA���݂ɕ\�����Ă݂�
;�v���[��2���g�킸�ɁAvsync���Ƀo�b�t�@������݂ɓ]������Ƃ�������B
Zenkaku3Flip:
	ld			hl,(KanjiVRAM)
	out			(c),d					;$E9 ��ʃA�h���X���������ނ̂͏��񂾂��ŗǂ�
	ld			a,e
	dec			c
	ld			de,78
	ld			b,8*5
.loop
	out			(c),a					;$E8 ���ʃA�h���X
	out			($EA),a					;����ROM�ǂݏo���T�C�� FR/MR �ȍ~�̓E�F�C�g�܂ߕs�v
	inc			a
	inc			c						;8clk wait

	out			($5D),a					;VRAM.RED
	ini									;$E9
	dec			c
	ini									;$E8
	dec			hl
	dec			hl

	out			($EB),a					;�ǂݏo���I���T�C�� FR/MR �ȍ~�͕s�v

	out			(c),a					;$E8 ���ʃA�h���X
	out			($EA),a					;����ROM�ǂݏo���T�C�� FR/MR �ȍ~�̓E�F�C�g�܂ߕs�v
	inc			a						;4+4clk wait
	inc			c

	out			($5E),a					;VRAM.GREEN
	ini									;$E9
	dec			c
	ini									;$E8

	add			hl,de
	out			($EB),a					;�ǂݏo���I���T�C�� FR/MR �ȍ~�͕s�v
	djnz		.loop
	ret

Hankaku3Flip:
	ld			hl,(KanjiVRAM)
	ld			a,d
	out			($E9),a					;$E9 ��ʃA�h���X���������ނ̂͏��񂾂��ŗǂ�
	ld			c,e
	ld			de,80
	ld			b,8
.loop
	ld			a,c
	out			($E8),a					;$E8 ���ʃA�h���X
	out			($EA),a					;����ROM�ǂݏo���T�C�� FR/MR �ȍ~�̓E�F�C�g�܂ߕs�v
	inc			c						;8+12clk wait
	out			($5D),a					;VRAM.RED

	in			a,($E9)					;$E9
	ld			(hl),a
	out			($5E),a					;VRAM.GREEN
	in			a,($E8)					;$E8
	ld			(hl),a
	add			hl,de

	out			($EB),a					;�ǂݏo���I���T�C�� FR/MR �ȍ~�͕s�v
	djnz		.loop
	ret

Quarter3Flip:
	ld			hl,(KanjiVRAM)
	ld			a,d
	out			($E9),a					;$E9 ��ʃA�h���X���������ނ̂͏��񂾂��ŗǂ�
	ld			c,e
	ld			de,80
	ld			b,4
	jp			Hankaku3Flip.loop

;--------------------------------------------------------------------------------------------------�ϊ�
;JIS�R�[�h������ROM�̃A�h���X��
;in: de
;out: de,c
JIS2ADR:
	ld			a,d
	cp			$70
	jr			nc,.part22				;7020-705F ��񐅏�
	cp			$50
	jr			nc,.part21				;5020-6F7F ��񐅏�
	cp			$30
	jr			nc,.part1				;3020-4F5F ��ꐅ��
;	cp			$21
;	jr			nc,.nokanji				;2120-277F �񊿎�

.nokanji
	ld			a,d
	and			%00000111
	ld			c,a
	ld			a,e
	and			%01100000
	rrca
	rrca								;000bb000
	or			c						;000bbaaa
	ld			d,a
	ld			a,e
	add			a,a
	add			a,a
	add			a,a
	add			a,a
	ld			e,a						;cccc0000
	rl			d						;00bbaaac
	ld			c,$E9
	ret
.part1
	ld			a,e
	and			%01100000
	add			a,a
	ld			c,a						;bb000000
	ld			a,e
	add			a,a
	add			a,a
	add			a,a
	add			a,a
	ld			e,a						;cccc0000
	rl			d
	ld			a,d
	and			%00111111
	or			c
	ld			d,a						;bbaaaaac
	ld			c,$E9
	ret
.part21
	ld			a,e
	and			%01100000
	add			a,a
	or			d
	and			%11100000				;cca00000
	ld			c,a
	ld			a,e
	add			a,a
	add			a,a
	add			a,a
	add			a,a
	ld			e,a						;dddd0000
	rl			d
	ld			a,d
	and			%00011111
	or			c
	ld			d,a						;ccabbbbd
	ld			c,$ED
	ret
.part22
	ld			a,e
	and			%01100000
	rrca
	ld			c,a
	ld			a,e
	add			a,a
	add			a,a
	add			a,a
	add			a,a
	ld			e,a						;cccc0000
	rl			d
	ld			a,d
	and			%00001111
	or			c
	ld			d,a						;00bbaaac
	ld			c,$ED
	ret

KanjiXY2VRAM:
	push		hl
	ld			a,(KanjiY)
	add			a,a
	add			a,.table & $FF
	ld			l,a
	adc			a,.table >> 8
	sub			l
	ld			h,a

	ld			a,(KanjiX)
	add			a,(hl)
	inc			hl
	ld			h,(hl)
	ld			l,a
	adc			a,h
	sub			l
	ld			h,a

	ld			(KanjiVRAM),hl
	pop			hl
	ret

.table
	dw			$8000+80*8* 0, $8000+80*8* 1, $8000+80*8* 2, $8000+80*8* 3, $8000+80*8* 4, $8000+80*8* 5, $8000+80*8* 6, $8000+80*8* 7
	dw			$8000+80*8* 8, $8000+80*8* 9, $8000+80*8*10, $8000+80*8*11, $8000+80*8*12, $8000+80*8*13, $8000+80*8*14, $8000+80*8*15
	dw			$8000+80*8*16, $8000+80*8*17, $8000+80*8*18, $8000+80*8*19, $8000+80*8*20, $8000+80*8*21, $8000+80*8*22, $8000+80*8*23
	dw			$8000+80*8*24


;Shift-JIS �� JIS �R�[�h�ɕϊ�
;in: de
;out: de
SJIS2JIS:
	ld			a,d
	cp			$A0						;���o�C�g(H)�� 0x9F �ȉ��Ȃ� H-=0x71 �łȂ���΁AH-=0xB1
	jr			nc,.skip1
	add			a,$B1-$71
.skip1
	sub			$B1

	add			a,a
	inc			a
	ld			d,a						;H=(H<<1)+1

	ld			a,e
	cp			$7F						;���o�C�g(L)�� 0x7F�ȏ�Ȃ� L--
	jr			c,.skip2
	dec			a
.skip2

	cp			$9E						;���o�C�g(L)�� 0x9E �ȏ�Ȃ� L-=0x7D,H++ �łȂ���� L-=0x1F
	jr			c,.skip3
	sub			$7D-$1F
	inc			d
.skip3
	sub			$1F
	ld			e,a
	ret

KanjiX:		db	0						;0-79
KanjiY:		db	1						;0-24
KanjiVRAM:	dw	$8000

#ENDLIB

#LIB KANJILOCATE
	; HL = X
	; DE = Y
	ld	a,l
	ld	(KanjiX),a
	ld	a,e
	ld	(KanjiY),a
	ret
#ENDLIB
