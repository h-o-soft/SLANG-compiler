


#LIB SETATR
; HL = 行
; DE = 開始位置X
; BC = 新規適用するアトリビュート

	; 新規適用するアトリビュート
	ld a,c

	; 開始位置X
	ld c,e

	; 開始行
	ld b,l

;--------------------------------------------------------------------------------------------------
;tab4 sjis
;b=行y(0-24) c=開始位置x(0-79) a=新規適用するアトリビュート de,hl 使用
; アトリビュートの適用順を考えなくて良いヘルパールーチン
; 開始位置を $80 にして呼び出すと該当行を全てクリア($80,$E8)する。
;
; 同じ開始位置ものがある場合 → 上書きする。PC80/88 アトリビュートの仕様により、同一開始位置でのアトリビュートは不可
; 属性総数が20個を超えた場合 → 最後尾のアトリビュートが追い出される。
; このルーチンを使わずに直接アトリビュートを弄る併用は考慮していないので必ずこのルーチンを呼ぶこと。
; 最初のアトリビュート開始位置は 0 か $80 で初期化されており、以後のアトリビュートもソートされている前提で使用する。
; 最初のアトリビュートは暗黙の x=0 になってしまうので、明示的な x=0 以外では使用しない。
;
; テキストVRAM がカラーモードになっていること。でないと色設定などができない。

TVRAM			equ		$F300 ; $F3C8
;ATRC と ATRD は同時には使えない
;違うグループは同時に指定できる(ブリンク＋アンダーラインなど）
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

ATRD_REVSECa	equ		%00000111				;101と同じ。指定した部分が■で隠れる
ATRD_REVBLK		equ		%00000110
ATRD_REVSEC		equ		%00000101
ATRD_REV		equ		%00000100
ATRD_SECa		equ		%00000011				;001と同じ。指定した部分が黒で隠れる
ATRD_BLK		equ		%00000010
ATRD_SEC		equ		%00000001
ATRD_NOR		equ		%00000000

;sample
;	ld			a,ATRC_RED
;	ld			bc,(0 << 8) | 10				;x=10 y=0 から 文字色を赤にする
;	call		SetTextAtr

SetTextAtr:
	ld			h,a
	; in			a,($32)
	; push		af
	; res			4,a
	; out			($32),a

	push		hl							;push af の代わり。アトリビュートを一時保存

	call		.sub						;b=y c=x を hl=tatr にする
	bit			7,c
	jr			nz,.clear					;開始位置=$80ならその行は初期化する

	ld			a,c							;x
	ld			b,20
.loop:
	cp			(hl)						;同じ開始位置のものがあったら差し替える
	jr			z,.found					;先頭から検索するので、最初のアトリビュートが x=0 でない場合でも一致すれば上書きしてしまう。
	inc			hl							;その場合、新たに設定したアトリビュートの開始位置は強制的に x=0 と解釈されてしまう。
	inc			hl							;なので、正常に初期化されていることが前提となる。
	djnz		.loop

	ld			b,20
.sort:
	dec			hl
	dec			hl
	ld			a,(hl)
	or			a
	jr			z,.next						;0 または 80 以上は空白と見なして飛ばす
	cp			80
	jr			nc,.next
	ld			a,c
	cp			(hl)
	jr			c,.next						;自分より開始位置の大きいものは飛ばして小さいものを探す

	ld			a,b
	cp			20							;いきなり初回で見つかったら後ろに押し出さずに最後尾だけを書き換え
	jr			nz,.skip1
	dec			hl
	dec			hl
	dec			b

.skip1:
	inc			hl							;小さい開始位置が見つかったら、その後ろに挿入する
	inc			hl
	pop			af
.sortlp:
	ld			e,(hl)						;古い値を de に保存
	ld			(hl),c						;新しい値 ac を入れる
	inc			hl
	ld			d,(hl)
	ld			(hl),a
	inc			hl

	ld			a,b
	inc			b
	cp			19							;19が最終位置
	ld			c,e
	ld			a,d
	jr			nz,.sortlp					;新しい属性を挿入した結果、最後尾の属性は追い出される
	jr			.exit

.next:
	djnz		.sort						;最後まで検索して空だらけ or 一番最初の開始位置が a よりうしろだったら↓

	pop			af
	inc			c							;x=0 の場合はそのまま先頭のアトリビュートに上書き。押し出しはナシ。
	dec			c
	jr			z,.skip2
	inc			hl							;x!=0の場合、先頭に書き込むと暗黙のx=0にされてしまうので、一つずらして書き込む。
	inc			hl							;この場合、最後尾のアトリビュートは追い出されて無効となる
	ld			b,1
	jr			.sortlp
.skip2:										;x=0 の場合はそのまま先頭のアトリビュートに上書き。押し出しはナシ。
	ld			(hl),c
	inc			hl
	ld			(hl),a
	jr			.exit

.found:										;同じ開始位置のものがあったら、そこに上書きする
	pop			af							;この場合、先頭に 0 でも 80 以上でもない無効な開始位置が書き込まれていたはずなので上書きで問題ない
	inc			hl
	ld			(hl),a
	jr			.exit

.clear:
	pop			af
	ld			b,20
.clearlp:
	ld			(hl),$80					;全クリアは BASIC に倣って $80,$E8 とする。
	inc			hl
	ld			(hl),$E8
	inc			hl
	djnz		.clearlp
.exit:
	; pop			af
	; out			($32),a
	ret

.sub:										;b=y c=x を hl=tatr　にする
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

