; Converted from lib/libdef/libpc80mk2_sound.yml
; SLANG Runtime Library (new format)
;
; PCG-8100 後期 / PCG-8200 / PCG-8800 系互換ボード (PSA3.0 等) が搭載する
; Intel 8253 PIT で 3 ch 矩形波サウンドを扱うドライバ。
; 原典: 内藤 時浩 (Tokihiro Naito) 氏作「8253 簡易サウンドドライバ V2」
;       (2020/11/27、obsolete/lib/pc8001/soundv2.z80 同梱)
; 許諾: 著者より PD (Public Domain) 扱いでの利用許可を取得 (X 経由、2023/8/17)
;       詳細は THIRD_PARTY_NOTICES.md 参照
; 改変: SLANG ランタイム形式への移植 + KEYON 動的 mask + SNDOutput shadow
;       最適化 + 音長カウンタ修正 + 休符 (TONE.REST=0x7F) サポート +
;       SND_ISPLAYING 判定修正 等

; @name SND_COMMON
; @resident shared
; @lib PC80SND


ALIGN 256
;
; �����J�E���^�l�e�[�u�� 256���E�ۏ�
;
FRQTBL:
	dw	0xEE80, 0xE11D, 0xD47B, 0xC88D, 0xBD4C, 0xB2AC
	dw	0xA8A5, 0x9F2E, 0x963F, 0x8DD0, 0x85DA, 0x7E57
	dw	0x7740, 0x708F, 0x6A3D, 0x6447, 0x5EA6, 0x5956
	dw	0x5453, 0x4F97, 0x4B1F, 0x46E8, 0x42ED, 0x3F2C
	dw	0x3BA0, 0x3847, 0x351F, 0x3223, 0x2F53, 0x2CAB
	dw	0x2A29, 0x27CB, 0x2590, 0x2374, 0x2177, 0x1F96
	dw	0x1DD0, 0x1C24, 0x1A8F, 0x1912, 0x17AA, 0x1656
	dw	0x1515, 0x13E6, 0x12C8, 0x11BA, 0x10BB, 0x0FCB
	dw	0x0EE8, 0x0E12, 0x0D48, 0x0C89, 0x0BD5, 0x0B2B
	dw	0x0A8A, 0x09F3, 0x0964, 0x08DD, 0x085E, 0x07E5
	dw	0x0774, 0x0709, 0x06A4, 0x0644, 0x05EA, 0x0595
	dw	0x0545, 0x04F9, 0x04B2, 0x046F, 0x042F, 0x03F3

;-----------------------------------------------------------------------
; �J�E���^�l���o�͂���
; B  = �J�E���^�l�ݒ�J�n�r�b�g
; C  = SNDCH �o�̓|�[�g
; HL = ���ۂɏo�͂���J�E���^�l
;
SNDOutput:
	ld	a, b
	out	(0x0F), a
	nop
	out	(c), l
	nop
	out	(c), h
	ret

;-----------------------------------------------------------------------
; �J�E���^�l���ύX�����Ƃ��̂ݏo�͂���
; B  = �J�E���^�l�ݒ�J�n�r�b�g
; C  = SNDCH �o�̓|�[�g
; DE = LAST_Fx �A�h���X
; HL = ���ۂɏo�͂���J�E���^�l
;
; 8253 mode 3 is reloaded by control/counter writes, so keep the currently
; emitted counter in shadow RAM and skip redundant 60Hz writes.
SNDOutputChanged:
	ld	a, (de)
	cp	l
	jr	nz, .write
	inc	de
	ld	a, (de)
	cp	h
	ret	z
	dec	de
.write	ld	a, l
	ld	(de), a
	inc	de
	ld	a, h
	ld	(de), a
	jp	SNDOutput

;-----------------------------------------------------------------------
; �`�����l���ʍX�V�����i�h���C�o�����Ăяo����p�j
; HL = ���[�N�擪�A�h���X�i + MML.ADDRESS�j
;
SNDPlayer:
	ld	(.retads), sp
	ld	sp, hl			; SP = +2 MML.ADDRES

	; �A�h���X���Ȃ���ΏI��
	pop	de			; SP = +4 MML.LENGTH
	ld	a, d			; DE MML�f�[�^�|�C���^
	or	e
	jr	z, .exit		; �f�[�^��������ΏI������

	; �����p���m�F
	pop	hl			; H = ��������, L = ������
	dec	l			; ���݂̉��� -1
	jp	m, .cmd			; �}�C�i�X�Ȃ�R�}���h���
	push	hl			; ������ۑ�
	jr	nz, .exit		; �����Ō�Ŗ�����ΏI��
	push	de			; dummy
	ld	h, l			; HL = 0
	push	hl			; MML.FREQ ���[����

	; SNDTimer �ɖ߂�
.retads	equ	$ + 1
.exit	ld	sp, 0			; SP �����ɖ߂���
	ret				; �I������

	; �I��
.stop	ld	hl, 0
	push	hl			; MML.LENGTH = 0
	push	hl			; ADDRESS = 0
	jr	.exit			; �I������

	; ����
.length	neg				; �������]
	dec	a			; -1 to compensate for trailing silent tick
	ld	h, a			; �����������X�V

	; MML�R�}���h���
.cmd	ld	a, (de)			; Acc �R�}���h
	inc	de			; DE ���̃A�h���X��
	cp	0x80			; �I���R�[�h�Ŕ�r
	jr	z, .stop		; ZF=1 �Ȃ�I��
	jp	nc, .length		; �}�C�i�X�l�Ȃ特��
	cp	0x7F			; rest (silence)
	jr	z, .rest

	; ���K
	ld	l, h			; ������������
	push	hl			; ������ۑ�
	push	de			; MML�|�C���^��ۑ�
	add	a, a			; �J�E���^�A�Ԃ�2�{����
	add	a, FRQTBL % 256		; �e�[�u������8bit���Z
	ld	l, a
	ld	h, FRQTBL / 256		; HL �J�E���^�l�̈ʒu
	ld	a, (hl)
	inc	l
	ld	h, (hl)
	ld	l, a			; HL �J�E���^�l
	push	hl			; MML.FREQ �ɕۑ�
	jr	.exit

	; rest (silence): FREQ=0, length proceeds normally
.rest	ld	l, h			; L = LENDATA
	push	hl			; MML.LENGTH = LENDATA
	push	de			; MML.ADDRESS = next
	ld	hl, 0			; FREQ = 0
	push	hl			; MML.FREQ = 0
	jr	.exit


;-----------------------------------------------------------------------
; ���K��`
;
TONE:
.C	equ	0
.CP	equ	1
.D	equ	2
.DP	equ	3
.E	equ	4
.F	equ	5
.FP	equ	6
.G	equ	7
.GP	equ	8
.A	equ	9
.AP	equ	10
.B	equ	11

.O1	equ	(0 * 12)
.O2	equ	(1 * 12)
.O3	equ	(2 * 12)
.O4	equ	(3 * 12)
.O5	equ	(4 * 12)
.O6	equ	(5 * 12)

.REST	equ	0x7F		; rest (silence)

;-----------------------------------------------------------------------
; ��������t���O
;
KEYON:	; �L�[�I���t���O out(0x02) �ɏo�͂���
.@1	equ	%00001000	; ch.1
.@2	equ	%01000000	; ch.2
.@3	equ	%10000000	; ch.3
.All	equ	(.@1 | .@2 | .@3)

SNDCH:	; �`�����l�����̃J�E���g�l�ݒ�|�[�g�ԍ�
.@1	equ	%00001100	; ch.1
.@2	equ	%00001101	; ch.2
.@3	equ	%00001110	; ch.3

SNDWRT:	; �J�E���^�l�o�͊J�n�t���O ���ʁ^���
.@1	equ	%00110110	; ch.1
.@2	equ	%01110110	; ch.2
.@3	equ	%10110110	; ch.3

;-----------------------------------------------------------------------
; ���[�N�G���A
;
MML:
.FREQ		equ	0		; 2 �������g��
.ADDRESS	equ	2		; 2 MML�f�[�^�A�h���X
.LENGTH		equ	4		; 1 ���݂̉����J�E���^
.LENDATA	equ	5		; 1 ���������l
.REPEAT		equ	6		; 2 MML�f�[�^�����A�h���X
.SIZE		equ	8		; �`�����l���f�[�^�T�C�Y

	org     ($ + 1) / 2 * 2		; �����A���C�����g
SND:
.Counter	ds	1		; �ėp�J�E���^
.Repeat		ds	1		; BGM���s�[�g�Đ��t���O
.CH1		ds	MML.SIZE	; 1ch
.CH2		ds	MML.SIZE	; 2ch
.CH3		ds	MML.SIZE	; 3ch
.CHEnd		equ	$		; CH�Ō��
.SE		ds	MML.SIZE	; SE
.BLANKFLG	ds	1		; �����A���ʒu�t���O

; shadow registers: last FREQ actually written via SNDOutput per physical channel
; (CH3 slot tracks SE-or-CH3 multiplexed value). Used to skip redundant
; SNDWRT writes that would re-trigger 8253 mode-3 reload mid-cycle.
.LAST_F1	ds	2		; CH1 last sent freq
.LAST_F2	ds	2		; CH2 last sent freq
.LAST_F3	ds	2		; CH3/SE last sent freq


; @name SND_STOP
; @resident shared
; @calls SND_COMMON
; @lib PC80SND
SNDStop:

; @name SND_INIT
; @resident shared
; @calls SND_COMMON
; @lib PC80SND
;-----------------------------------------------------------------------
; �����~�߂�
;
SNDInitialize:
	xor	a
	out	(0x02), a	; �L�[�I���t���O��S��~����
	ld	hl, 0
	ld	(.retads), sp
	ld	sp, SND.LAST_F3 + 2	; force first SNDOutput per ch
	push	hl
	push	hl
	push	hl
	ld	sp, SND.CH1 + MML.ADDRESS + 2
	push	hl
	ld	sp, SND.CH2 + MML.ADDRESS + 2
	push	hl
	ld	sp, SND.CH3 + MML.ADDRESS + 2
	push	hl
	ld	sp, SND.SE  + MML.ADDRESS + 2
	push	hl
.retads	equ	$ + 1
	ld	sp, 0		; SP �����ɖ߂���
	ret			; �I������

; @name SND_PLAY
; @resident shared
; @calls SND_COMMON
; @lib PC80SND
;-----------------------------------------------------------------------
; ���y��炷
; HL = BGM�f�[�^�A�h���X
;
SNDMusicStart:
	ld	(.retads), sp
	ld	sp, SND.CHEnd
	ld	de, 0x0800	; ���������l 8

	; CH3
	ld	c, (hl)
	inc	hl
	ld	b, (hl)		; BC CH3 MML �A�h���X
	inc	hl
	push	bc		; CH3 + MML.REPEAT
	push	de		; CH3 + MML.LENGTH
	push	bc		; CH3 + MML.ADDRESS
	push	de		; CH2 + MML.FREQ (dummy)

	; CH2
	ld	c, (hl)
	inc	hl
	ld	b, (hl)		; BC CH2 MML �A�h���X
	inc	hl
	push	bc		; CH2 + MML.REPEAT
	push	de		; CH2 + MML.LENGTH
	push	bc		; CH2 + MML.ADDRESS
	push	de		; CH2 + MML.FREQ (dummy)

	; CH1
	ld	c, (hl)
	inc	hl
	ld	b, (hl)		; BC CH1 MML �A�h���X
	push	bc		; CH1 + MML.REPEAT
	push	de		; CH1 + MML.LENGTH
	push	bc		; CH1 + MML.ADDRESS

.retads	equ	$ + 1
	ld	sp, 0		; SP �����ɖ߂���
	ret			; �I������

; @name SND_SEPLAY
; @resident shared
; @calls SND_COMMON
; @lib PC80SND
;-----------------------------------------------------------------------
; ���ʉ���炷
; HL = ���ʉ��f�[�^�A�h���X
;
SNDEffectStart:
	ld	(SND.SE + MML.ADDRESS), hl
	ld	hl, 0x0100	; ���������l 1
	ld	(SND.SE + MML.LENGTH), hl
	ld	h, l
	ld	(SND.SE + MML.FREQ), hl
	ret

; @name SND_SYNC
; @resident shared
; @calls SND_COMMON,SND_PROC
; @lib PC80SND
;-----------------------------------------------------------------------
; �����A�������ɓ������特��炷
;
SNDDiver:
	push	af
	ld	a, (SND.BLANKFLG)
	or	a
	jr	nz, .chkF

	; �����A�������ɂ���
	in	a, (0x40)
	and	%00100000
	ld	(SND.BLANKFLG), a
	pop	af
	ret

	; �����A�����\�ɂ���
.chkF	in	a, (0x40)
	and	%00100000
	ld	(SND.BLANKFLG), a
	call	z, SNDTimer	; ������\�ɕς�����̂ŌĂяo��
	pop	af
	ret

; @name SND_PROC
; @resident shared
; @calls SND_COMMON
; @lib PC80SND
;-----------------------------------------------------------------------
; �T�E���h�h���C�o�G���g��
;
SNDTimer:
	; �S�Ă̎g�p���W�X�^��ۑ����ĕ��A����
	push	hl
	push	de
	push	bc
	push	af
	call	.exec
	pop	af
	pop	bc
	pop	de
	pop	hl
	ret

	; �ėp�^�C�}�[�X�V
.exec	ld	hl, SND.Counter
	inc	(hl)

	; �`�����l���ʍX�V
	ld	hl, SND.CH1 + MML.ADDRESS
	call	SNDPlayer
	ld	hl, SND.CH2 + MML.ADDRESS
	call	SNDPlayer
	ld	hl, SND.CH3 + MML.ADDRESS
	call	SNDPlayer
	ld	hl, SND.SE + MML.ADDRESS
	call	SNDPlayer

	; output: skip SNDOutput when FREQ matches shadow (avoid 60Hz mode-3 reload)

	; CH1
	ld	hl, (SND.CH1 + MML.FREQ)
	ld	de, SND.LAST_F1
	ld	bc, SNDWRT.@1 * 256 + SNDCH.@1
	call	SNDOutputChanged

	; CH2
	ld	hl, (SND.CH2 + MML.FREQ)
	ld	de, SND.LAST_F2
	ld	bc, SNDWRT.@2 * 256 + SNDCH.@2
	call	SNDOutputChanged

	; CH3 / SE multiplex (SE wins when non-zero)
	ld	hl, (SND.SE + MML.FREQ)
	ld	a, h
	or	l
	jr	nz, .pick3
	ld	hl, (SND.CH3 + MML.FREQ)
.pick3	ld	de, SND.LAST_F3
	ld	bc, SNDWRT.@3 * 256 + SNDCH.@3
	call	SNDOutputChanged

	; KEYON mask: only channels with FREQ != 0 (silent ch stays gated off)
	ld	c, 0
	ld	hl, (SND.CH1 + MML.FREQ)
	ld	a, h
	or	l
	jr	z, .nk1
	set	3, c		; KEYON.@1 (bit 3)
.nk1	ld	hl, (SND.CH2 + MML.FREQ)
	ld	a, h
	or	l
	jr	z, .nk2
	set	6, c		; KEYON.@2 (bit 6)
.nk2	ld	hl, (SND.SE + MML.FREQ)
	ld	a, h
	or	l
	jr	nz, .key3
	ld	hl, (SND.CH3 + MML.FREQ)
	ld	a, h
	or	l
	jr	z, .nk3
.key3	set	7, c		; KEYON.@3 (bit 7)
.nk3	ld	a, c
	out	(0x02), a	; key on active channels only
	ret

; @name SND_ISPLAYING
; @resident shared
; @calls SND_COMMON
; @lib PC80SND
;-----------------------------------------------------------------------
; ���y�����t����Ă��邩
; out: ZF=1 ��~���Ă���
;
SNDIsPlaying:
	ld	hl, (SND.CH1 + MML.ADDRESS)
	ld	a, h
	or	l
	ret	nz
	ld	hl, (SND.CH2 + MML.ADDRESS)
	ld	a, h
	or	l
	ret	nz
	ld	hl, (SND.CH3 + MML.ADDRESS)
	ld	a, h
	or	l
	ret
