;
; Kanji
;
; JISコード基準で格納されている JIS X 0208(1978年制定)
; JIS83/JIS90の改定は含まない
; NEC外字(98外字)は含まない
; http://www.asahi-net.or.jp/~ax2s-kmtn/ref/jisx0208.html
;
; 半角部には 0x80-0xA0,0xE0-0xFF に半角ひらがなフォントが入っている
; これは Shift-JIS の 1バイト目と被る領域にある
;
; mk2以降は第一水準標準装備
; MR,FH,MH,FA,MA,MA2,FE,FE2,MC は第二水準標準装備
;
;$E8(R/W) 第一水準 (R=読み出し/W=下位アドレス指定) 読み出しは全角漢字の右側,半角1/4角文字の偶数ライン
;$E9(R/W) 第一水準 (R=読み出し/W=上位アドレス指定) 読み出しは全角漢字の左側,半角1/4角文字の奇数ライン
;$EA(W)   読み出し開始サイン 書き込む値は何でも良い。書き込んだ後 8clk ウェイトが必要
;$EB(W)   読み出し終了サイン 書き込む値は何でも良い
;$EC(R/W) 第二水準 (R=読み出し/W=下位アドレス指定) 読み出しは全角漢字の右側
;$ED(R/W) 第二水準 (R=読み出し/W=上位アドレス指定) 読み出しは全角漢字の左側
;
;$EA,$EB の書き込みおよびウェイトは FR 以降では不要
;確実にROMアドレス指定できるようにするため、初回に $EB に値を出力しておくこと
;
;漢字ROMアドレス指定は下位・上位を毎回書き込む必要は無い。
;最初の一度だけ上位を書いて、下位アドレスだけ毎回+1ずつ書き込んでいけば良い。
;
;
;※文字コードからアドレスへの変換
;　半角(0020 - 00FF)
;　   FEDCBA98 76543210
;　   00000000 nnnnnnnn
;　-> 00000nnn nnnnn000
;　
;　1/4角(0100 - 01FF)
;　   FEDCBA98 76543210
;　   00000001 nnnnnnnn
;　-> 000010nn nnnnnn00
;　
;　非漢字(2120 - 27FF)
;　   FEDCBA98 76543210
;　   00100aaa 0bbccccc
;　-> 00bbaaac cccc0000
;　
;　第一水準(3020-4F5F)
;　   FEDCBA98 76543210
;　   000aaaaa 0bbccccc
;　-> bbaaaaac cccc0000
;　
;　第二水準(5020-6F7F)
;　   FEDCBA98 76543210
;　   00a0bbbb 0ccddddd
;　-> ccabbbbd dddd0000
;　
;　第二水準(7020-705F)
;　   FEDCBA98 76543210
;　   01110aaa 0bbccccc
;　-> 00bbaaac cccc0000
;
;http://www.maroon.dti.ne.jp/youkan/pc88/index.html
;->漢字ROM 大変分かりやすい
;


#LIB KANJIPUT

; 0にすると640x200モード、1～3にすると320x200モードで指定した色で描画されます(テキトー)
KANJICOLOR	equ	0
; 0=通常描画、1=OR描画、2=1行飛ばし描画(読めない)
; ※320x200では通常描画以外は動作しません
KANJIMODE	equ	0

PutKanji:
	ld			a,(hl)
	inc			hl
	or			a
	ret			z						;終端=0
	cp			$0D
	jr			z,.crlf

	ld			d,0
	ld			e,a
	xor			$20						;ShiftJIS 1バイト目は 0x81 ～ 0x9F または 0xE0 ～ 0xFC
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
;	; 80より大きければ改行
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
	ld			a,e						;1バイト目=1 の時は 1/4角とする
	dec			a
	jr			z,.quarter
	push		hl
	ex			de,hl					;0000-00FF 半角
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

.quarter								;0100-01FF 1/4角
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

;--------------------------------------------------------------------------------------------------表示
;ループ展開etc.
Zenkaku:
	ld			hl,(KanjiVRAM)
	out			(c),d					;$E9 上位アドレスを書き込むのは初回だけで良い
	dec			c
	ld			a,e
	ld			de,78
	ld			b,16*3
.loop
	out			(c),a					;$E8 下位アドレス
	out			($EA),a					;漢字ROM読み出しサイン FR/MR 以降はウェイト含め不要
	inc			a
	inc			c						;8clk wait

#if KANJICOLOR != 0
	push af
	push bc
	in			a,(c)
	; aの8ビットををbcに展開して返す
	call	Wide2byte

	ld	(hl),b
	inc	hl
	ld	(hl),c
	inc	hl

	ld	c,$e8
	in	a,(c)
	; aの8ビットををbcに展開して返す
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

	out			($EB),a					;読み出し終了サイン FR/MR 以降は不要
	djnz		.loop
	ret

Hankaku:
	ld			hl,(KanjiVRAM)
	ld			c,$E9
	out			(c),d					;$E9 上位アドレスを書き込むのは初回だけで良い
	dec			c
	ld			a,e
	ld			de,79
	ld			b,8*3
.loop
	out			(c),a					;$E8 下位アドレス
	out			($EA),a					;漢字ROM読み出しサイン FR/MR 以降はウェイト含め不要
	inc			a
	inc			c						;8clk wait

#if KANJICOLOR != 0
	push af
	push bc
	in			a,(c)
	; aの8ビットををbcに展開して返す
	call	Wide2byte

	ld	(hl),b
	inc	hl
	ld	(hl),c

	add	hl,de

	ld	c,$e8
	in	a,(c)
	; aの8ビットををbcに展開して返す
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

	out			($EB),a					;読み出し終了サイン FR/MR 以降は不要
	djnz		.loop
	ret


#if KANJICOLOR != 0
; aの値をbcに展開して返す
Wide2byte:
	push de

	VramColor:		; VramColor+1を0～3で書き換える(デフォルトは3)
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
	; これでdの上位4ビットが8ビットに展開されてaに入っている
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
	out			(c),d					;$E9 上位アドレスを書き込むのは初回だけで良い
	dec			c
	ld			a,e
	ld			de,79
	ld			b,4*3
	jp			Hankaku.loop


;--------------------------------------------------------------------------------------------------表示 バリエーション
;1ライン飛ばし、奇数/偶数ラインのみ高さ8ドットで描いてみる
Zenkaku2E:
	ld			hl,(KanjiVRAM)
	out			(c),d					;$E9 上位アドレスを書き込むのは初回だけで良い
	dec			c
	ld			a,e
	nop							;inc a で偶数
	ld			de,78
	ld			b,8*3
.loop
	out			(c),a					;$E8 下位アドレス
	out			($EA),a					;漢字ROM読み出しサイン FR/MR 以降はウェイト含め不要
	add			a,2
	inc			c						;7+8clk wait

	ini									;$E9
	dec			c
	ini									;$E8
	add			hl,de

	out			($EB),a					;読み出し終了サイン FR/MR 以降は不要
	djnz		.loop
	ret

Hankaku2E:
	ld			hl,(KanjiVRAM)
	ld			c,$E9
	out			(c),d					;$E9 上位アドレスを書き込むのは初回だけで良い
	dec			c
	ld			a,e
	ld			de,80
	ld			b,8*2
.loop
	out			(c),a					;$E8 下位アドレス
	out			($EA),a					;漢字ROM読み出しサイン FR/MR 以降はウェイト含め不要
	inc			a
	inc			c						;7+8clk wait

	ini									;$E9 <- $E8 で奇数ライン
	dec			hl
	dec			c
	add			hl,de

	out			($EB),a					;読み出し終了サイン FR/MR 以降は不要
	djnz		.loop
	ret

Quarter2E:
	ld			hl,(KanjiVRAM)
	ld			c,$E9
	out			(c),d					;$E9 上位アドレスを書き込むのは初回だけで良い
	dec			c
	ld			a,e
	ld			de,80
	ld			b,4*2
	jp			Hankaku2E.loop




;偶数ラインと奇数ラインを合成して高さ8ドットで描いてみる
Zenkaku2OR:
	ld			hl,(KanjiVRAM)
	out			(c),d					;$E9 上位アドレスを書き込むのは初回だけで良い
	dec			c
	ld			b,8
.loop
	out			(c),e					;$E8 下位アドレス
	out			($EA),a					;漢字ROM読み出しサイン FR/MR 以降はウェイト含め不要
	inc			e
	inc			c						;8clk wait

	in			d,(c)					;$E9
	dec			c
	in			a,(c)					;$E8

	out			($EB),a					;読み出し終了サイン FR/MR 以降は不要

	out			(c),e					;$E8 下位アドレス
	out			($EA),a					;漢字ROM読み出しサイン FR/MR 以降はウェイト含め不要
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

	out			($EB),a					;読み出し終了サイン FR/MR 以降は不要
	djnz		.loop
	ret


Hankaku2OR:
	ld			hl,(KanjiVRAM)
	ld			a,d
	out			($E9),a					;$E9 上位アドレスを書き込むのは初回だけで良い
	ld			c,e
	ld			de,80
	ld			b,8
.loop
	ld			a,c
	out			($E8),a					;$E8 下位アドレス
	out			($EA),a					;漢字ROM読み出しサイン FR/MR 以降はウェイト含め不要
	inc			c
	nop									;8clk wait

	in			a,($E9)					;$E9
	ld			(hl),a
	in			a,($E8)					;$E8
	or			(hl)
	ld			(hl),a
	add			hl,de

	out			($EB),a					;読み出し終了サイン FR/MR 以降は不要
	djnz		.loop
	ret

Quarter2OR:
	ld			hl,(KanjiVRAM)
	ld			a,d
	out			($E9),a					;$E9 上位アドレスを書き込むのは初回だけで良い
	ld			c,e
	ld			de,80
	ld			b,4
	jp			Hankaku2OR.loop


;偶数ラインと奇数ラインを別々の VRAM プレーンに描いて、交互に表示してみる
;プレーン2枚使わずに、vsync毎にバッファから交互に転送するという手も。
Zenkaku3Flip:
	ld			hl,(KanjiVRAM)
	out			(c),d					;$E9 上位アドレスを書き込むのは初回だけで良い
	ld			a,e
	dec			c
	ld			de,78
	ld			b,8*5
.loop
	out			(c),a					;$E8 下位アドレス
	out			($EA),a					;漢字ROM読み出しサイン FR/MR 以降はウェイト含め不要
	inc			a
	inc			c						;8clk wait

	out			($5D),a					;VRAM.RED
	ini									;$E9
	dec			c
	ini									;$E8
	dec			hl
	dec			hl

	out			($EB),a					;読み出し終了サイン FR/MR 以降は不要

	out			(c),a					;$E8 下位アドレス
	out			($EA),a					;漢字ROM読み出しサイン FR/MR 以降はウェイト含め不要
	inc			a						;4+4clk wait
	inc			c

	out			($5E),a					;VRAM.GREEN
	ini									;$E9
	dec			c
	ini									;$E8

	add			hl,de
	out			($EB),a					;読み出し終了サイン FR/MR 以降は不要
	djnz		.loop
	ret

Hankaku3Flip:
	ld			hl,(KanjiVRAM)
	ld			a,d
	out			($E9),a					;$E9 上位アドレスを書き込むのは初回だけで良い
	ld			c,e
	ld			de,80
	ld			b,8
.loop
	ld			a,c
	out			($E8),a					;$E8 下位アドレス
	out			($EA),a					;漢字ROM読み出しサイン FR/MR 以降はウェイト含め不要
	inc			c						;8+12clk wait
	out			($5D),a					;VRAM.RED

	in			a,($E9)					;$E9
	ld			(hl),a
	out			($5E),a					;VRAM.GREEN
	in			a,($E8)					;$E8
	ld			(hl),a
	add			hl,de

	out			($EB),a					;読み出し終了サイン FR/MR 以降は不要
	djnz		.loop
	ret

Quarter3Flip:
	ld			hl,(KanjiVRAM)
	ld			a,d
	out			($E9),a					;$E9 上位アドレスを書き込むのは初回だけで良い
	ld			c,e
	ld			de,80
	ld			b,4
	jp			Hankaku3Flip.loop

;--------------------------------------------------------------------------------------------------変換
;JISコードを漢字ROMのアドレスに
;in: de
;out: de,c
JIS2ADR:
	ld			a,d
	cp			$70
	jr			nc,.part22				;7020-705F 第二水準
	cp			$50
	jr			nc,.part21				;5020-6F7F 第二水準
	cp			$30
	jr			nc,.part1				;3020-4F5F 第一水準
;	cp			$21
;	jr			nc,.nokanji				;2120-277F 非漢字

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


;Shift-JIS を JIS コードに変換
;in: de
;out: de
SJIS2JIS:
	ld			a,d
	cp			$A0						;第一バイト(H)が 0x9F 以下なら H-=0x71 でなければ、H-=0xB1
	jr			nc,.skip1
	add			a,$B1-$71
.skip1
	sub			$B1

	add			a,a
	inc			a
	ld			d,a						;H=(H<<1)+1

	ld			a,e
	cp			$7F						;第二バイト(L)が 0x7F以上なら L--
	jr			c,.skip2
	dec			a
.skip2

	cp			$9E						;第二バイト(L)が 0x9E 以上なら L-=0x7D,H++ でなければ L-=0x1F
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
