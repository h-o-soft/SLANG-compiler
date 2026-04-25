; Converted from lib/libdef/libpc80mk2xbios_base.yml
; SLANG Runtime Library (new format)

; @name PC80CALLS
; @resident shared
; @works AT_WIDTH:1
;-----------------------------------------------------------------------
; 定数定義
;

;; ROM内ルーチン
;BIOS:
;.FUNC_COLOR	equ	$08F7		; Function Key On/Offとカラーモノクロ指定
;.WIDTH		equ	$093A		; CRT 画面表示文字数の設定
;.CURSOR_OFF	equ	$0BD2		; カーソル消去
;.MONITOR	equ	$5C66		; モニタに戻る
;.PUTCRT1  equ $0257   ; CRTへの1バイト出力
;.LOCATE equ $03A9     ; カーソルの移動
XBIOS:
.INPUT   equ $0003   ; キー入力
.WIDCH  equ $004D   ; CRT 画面表示文字数の設定
.PRINTS equ $000B   ; DEのアドレスの文字列を表示
.PUTC   equ $0013   ; CRTへの1バイト出力
.LOCATE equ $005f   ; カーソルの移動(H=Y,L=X)
.SETATR equ $0062   ; テキストアトリビュートの設定
.MONITOR  equ $0008 ; RSTJOB
.LPTON  equ $0065
.LPTOF  equ $0068
.LPRNT  equ $006B
.PRINT  equ $006E
.SCRN   equ $0071
.GETKY  equ $0074
.FLGET  equ $0077
.INKEY  equ $007A
.CSR    equ $007D
.HLHEX  equ $0080
.KBFAD  equ $0083
.GETL   equ $0085
.PORT30 equ $FF00
.PORT31 equ $FF01
.PORT40 equ $FF02

;N80WORK:
;.PORT31 equ  $E6C6    ; 出力ポート31H番地への出力データ
; bit 7: BG Color G(attr mode)
; bit 6: BG Color R(attr mode)
; bit 5: BG Color B(attr mode)
; bit 4: 0=640x200, 1=320x200
; bit 3: Graphics Screen 1=On/0=Off
; bit 2: 640x200: 0=attribute mode / 1=mono mode
;        320x200: 0=4 color 0 / 1=4 color 1
; bit 1: $0000-$7FFF : 0=ROM MODE / 1=RAM MODE
; bit 0: 4thROM 0=enable / 1=disable

; テキスト
CRTC:
.Digits		equ	80		; テキストの横サイズ
.Lines		equ	25		; テキストの行数
.LineSize	equ	120		; 1行のサイズ

; 方向フラグ
JOY:
.Right		equ	%00001000
.Left		equ	%00000100
.Down		equ	%00000010
.Up		equ	%00000001

; キーボード入力
KEYP00		equ	$00
KEYP01		equ	$01
KEYP08		equ	$08
KEYP09		equ	$09

; キーマップ
KEY00:
.NUM_6		equ	%01000000
.NUM_4		equ	%00010000
.NUM_2		equ	%00000100

KEY01:
.NUM_8		equ	%00000001

KEY09:
.ESC		equ	%10000000
.SPACE		equ	%01000000
.F5		equ	%00100000
.F4		equ	%00010000
.F3		equ	%00001000
.F2		equ	%00000100
.F1		equ	%00000010
.STOP		equ	%00000001

; アドレス
ADRS:
.VText		equ	$f300				; テキストVRAMアドレス
.VTextSize	equ	CRTC.LineSize * CRTC.Lines	; テキストサイズ
.VTextEnd	equ	ADRS.VText + ADRS.VTextSize	; テキストVRAM終端アドレス

; アトリビュート
ATRB:
.Max        	equ 20      	; テキスト1行のアトリビュート最大変化数
.MaxByte   	equ (ATRB.Max * 2)  ; テキスト1行のアトリビュートバイト数

; mode = 1
.Black      	equ %00001000   ; 黒
.Blue       	equ %00101000   ; 青
.Red        	equ %01001000   ; 赤
.Magenta    	equ %01101000   ; 紫
.Green      	equ %10001000   ; 緑
.Cyan       	equ %10101000   ; 水
.Yellow     	equ %11001000   ; 黄
.White      	equ %11101000   ; 白
.SemiGrph   	equ %00011000   ; セミグラフィック

; mode = 0
.Underline  	equ %00100000   ; 下線
.Overline   	equ %00010000   ; 上線
.Reverse    	equ %00000100   ; 反転
.Blink      	equ %00000010   ; 点滅
.Secret     	equ %00000001   ; シークレット


; @name SLANGINIT
; @resident local
; @calls PC80CALLS,PC80WORK
INIT:

; WORK ZERO CLEAR
XOR A
LD HL,__WORK__
LD DE,__WORK__+1
LD BC,__WORKEND__-__WORK__-1
LD (HL),A
LDIR

; ROM / 4th rom enable(use SD card routine)
LD  A,0
OUT (31H),A
; Read ROM / Write RAM
LD  A,10H
OUT  (E2H),A

; LOAD'XBIOS.CMT' to 0000H
#IF exists PC8001_SD
; for SD
LD HL,XBIOSNAME
CALL  600FH  ; ROPEN
CALL  6009H  ; RREAD
#ELSE
; for CMT
NOP
NOP
NOP
CALL 0BF3H   ; CMT Read Start
CALL 5F3AH ; LOAD CMT Machine binary
#ENDIF

; to All RAM
LD  A,11H
OUT  (E2H),A

<<CALLINITIALIZER>>

LD IY,__IYWORK

; CALL XBIOS COLD ENTRY
CALL  0000H

; C02CH
CALL MAIN
jp	XBIOS.MONITOR
INFLOOP:
JP INFLOOP

XBIOSNAME:
DB 'XBIOS.CMT',0


; @name MEMMODE
; @resident shared
; @calls PC80CALLS
; HL = READ: 0=ROM / 1=RAM
; DE = WRITE: 0=ROM / 1=RAM

; backup HL
LD D,L

; 31H to ROM mode
LD HL,XBIOS.PORT31
LD A,(HL)
; ROM MODE
AND $FC
OUT (31H),A
LD (HL),A

LD BC,$E2

; RAMWRITE(bit4)
SLA E
SLA E
SLA E
SLA E
; RAMREAD(bit0)
LD A,D
OR E
OUT (C),A

RET


; @name CMDSCREEN
; @resident shared
; HL = GRAPHIC MODE(0=640x200 MONO、1=640x200Attribute Color、2=320x200、4 Color 1,3=320x200、4 Color 1)
; DE = 0 = GRAPHIC OFF / 1 = GRAPHIC ON
; BC = COLOR CODE

; Color Code Backup
LD B,C

; Graphics Mode Backup
LD A,L
AND 1
LD H,A

LD A,(XBIOS.PORT31)

; AND $71
; 4th ROM許可(いいのか？よくわからない……)
AND $00
SRA L
SLA L
SLA L
SLA L
SLA L
OR L  ; bit 4 0=640x200, 1=320x200 を設定
SLA E
SLA E
SLA E
OR E  ; bit 3 Graphics Screen 1=On/0=Off
SLA H
SLA H
OR H

; COLOR CODEは後で考える(ここではない)
; SLA B
; SLA B
; SLA B
; SLA B
; SLA B
; OR B

OUT (31H),A
LD (XBIOS.PORT31),A

RET


; @name LOADCMT
; @resident shared
; @calls SD_UTIL
CALL SDROM_ENABLE
CALL $BF3   ; CMT Read Start
CALL $5F3A  ; LOAD CMT Machine binary
JP SDROM_DISABLE


; @name SD_UTIL
; @resident shared
SDROM_ENABLE:
PUSH AF
LD A,(XBIOS.PORT31)
AND $FE
OUT (31H),A
LD (XBIOS.PORT31),A
LD A,$10
OUT ($E2),A
POP AF
RET

SDROM_DISABLE:
PUSH AF
LD A,(XBIOS.PORT31)
OR $01
OUT (31H),A
LD (XBIOS.PORT31),A
LD A,$11
OUT ($E2),A
POP AF
RET


; @name SD_ROPEN
; @resident shared
; @calls SD_UTIL
CALL SDROM_ENABLE
CALL $600f
JP SDROM_DISABLE


; @name SD_FGET
; @resident shared
; @calls SD_UTIL
CALL SDROM_ENABLE
CALL $6006
CALL SDROM_DISABLE
LD L,A
LD H,0
RET


; @name SD_RREAD
; @resident shared
; @calls SD_UTIL
CALL SDROM_ENABLE
CALL $6009
JP SDROM_DISABLE


; @name SD_WAOPEN
; @resident shared
; @calls SD_UTIL
CALL SDROM_ENABLE
CALL $6012
JP SDROM_DISABLE


; @name SD_WNOPEN
; @resident shared
; @calls SD_UTIL
CALL SDROM_ENABLE
CALL $601B
JP SDROM_DISABLE


; @name SD_FPUT
; @resident shared
; @calls SD_UTIL
LD A,L
CALL SDROM_ENABLE
CALL $6018
JP SDROM_DISABLE


; @name SD_FWRITE
; @resident shared
; @calls SD_UTIL
CALL SDROM_ENABLE
CALL $6015
JP SDROM_DISABLE


; @name SD_WCLOSE
; @resident shared
; @calls SD_UTIL
CALL SDROM_ENABLE
CALL $601E
JP SDROM_DISABLE


; @name SETGVRAM
; @resident shared
; HL = 0=Main Memory / 1=GVRAM
push bc

; 0x8000～の領域をどちらにするかの選択
bit 0,l
jr z,.mainmemory
ld c,$5c
jr .setmode

.mainmemory
ld c,$5f

.setmode
out (c),a

pop bc
ret


; @name PC80WORK
; @resident shared
; @param_count 0
; @works WORKDUMMY:2
;


; @name KANJILOCATE
; @resident shared
; @lib PC80KANJI
	; HL = X
	; DE = Y
	ld	a,l
	ld	(KanjiX),a
	ld	a,e
	ld	(KanjiY),a
	ret

; @name KANJIPUT
; @resident shared
; @lib PC80KANJI

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


; @name PCGDEF2
; @resident shared
; @calls PCGDEF
; (256 chr mode)
; HL = chr code(0x00-0x7f) DE = address
EX DE,HL
LD A,0
SLA E
RLA ; x2
SLA E
RLA ; x4
SLA E
RLA ; x8
OR $04
LD D,A

; for PCG8200
LD A,19
OUT (3),A

JP PCGDEF.defmain


; @name PCGDEF
; @resident shared
; (128 chr mode)
; HL = chr code(0x00-0x7f) DE = address
EX DE,HL
LD A,0
SLA E
RLA ; x2
SLA E
RLA ; x4
SLA E
RLA ; x8
LD D,A

; for PCG8200
LD A,8
OUT (3),A

.defmain
; 8bytes transfer
LD C,0

OUTI
LD A,E
OUT (1),A
LD A,D
OR $10
OUT (2),A
AND $EF
OUT (2),A
INC E

OUTI
LD A,E
OUT (1),A
LD A,D
OR $10
OUT (2),A
AND $EF
OUT (2),A
INC E

OUTI
LD A,E
OUT (1),A
LD A,D
OR $10
OUT (2),A
AND $EF
OUT (2),A
INC E

OUTI
LD A,E
OUT (1),A
LD A,D
OR $10
OUT (2),A
AND $EF
OUT (2),A
INC E

OUTI
LD A,E
OUT (1),A
LD A,D
OR $10
OUT (2),A
AND $EF
OUT (2),A
INC E

OUTI
LD A,E
OUT (1),A
LD A,D
OR $10
OUT (2),A
AND $EF
OUT (2),A
INC E

OUTI
LD A,E
OUT (1),A
LD A,D
OR $10
OUT (2),A
AND $EF
OUT (2),A
INC E

OUTI
LD A,E
OUT (1),A
LD A,D
OR $10
OUT (2),A
AND $EF
OUT (2),A

RET


; @name STICK2
; @resident shared
; TENKEY INPUT
; result:
;   bit0 up
;   bit1 right
;   bit2 down
;   bit3 left

; 0～7
IN A,(0)
LD B,A
LD A,0
RRC B ; 0
RRC B ; 1
JR C,.nohit1
OR 0b1100
; JR .endhit
.nohit1
RRC B  ; 2
JR C,.nohit2
OR 0b0100
; JR .endhit
.nohit2
RRC B  ; 3
JR C,.nohit3
OR 0b0110
; JR .endhit
.nohit3
RRC B  ; 4
JR C,.nohit4
OR 0b1000
; JR .endhit
.nohit4
RRC B  ; 5
.nohit5
RRC B  ; 6
JR C,.nohit6
OR 0b0010
; JR .endhit
.nohit6
RRC B  ; 7
JR C,.nohit7
OR 0b1001
; JR .endhit
.nohit7

; 8、0
LD C,A
IN A,(1)
LD B,A
LD A,C

RRC B  ; 8
JR C,.nohit8
OR 0b0001
; JR .endhit
.nohit8
RRC B  ; 9
JR C,.endhit
OR 0b0011
.endhit

LD L,A
LD H,0
RET


; @name STRIG
; @resident shared
LD HL,0
IN A,(KEYP09)
AND KEY09.SPACE
JR NZ,.nohit
LD L,1
.nohit
RET


; @name VSYNC
; @resident shared
IN	A, (KEYP08)
RLCA
RET	NC
.front
IN	A, ($40)
AND	%00100000
JR	NZ, .front
.blank
IN	A, ($40)
AND	%00100000
JR	Z, .blank
RET


; @name KEYCHK
; @resident shared
LD C,L
IN L,(C)
LD H,0
RET


; @name BEEP
; @resident shared
LD A,L
AND 1
RLA
RLA
RLA
RLA
RLA ; $20
OUT ($40),A
RET


; @name GET_PORT31
; @resident shared
LD A,(XBIOS.PORT31)
LD L,A
LD H,0
RET


; @name SET_PORT31
; @resident shared
LD  A,L
OUT (31H),A
LD (XBIOS.PORT31),A
RET


