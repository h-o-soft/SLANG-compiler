;---------------------------------------------------------------;
;	Copyright (c) 2019 chara_render.asm
;	This software is released under the MIT License.
;	http://opensource.org/licenses/mit-license.php
;---------------------------------------------------------------; 

;---------------------------------------------------------------;
;	消去キャラバッファ (128x2)
;	各並びは
;		+0 タイムスタンプバッファ(下位)
;		+1 タイムスタンプバッファ(上位)
;		+2 Yサイズ (ピクセル単位)
;		+3 Xサイズ (キャラ単位)
;---------------------------------------------------------------;
align 256

; キャラ消去ワーク (Page0用)
clear_char_work0:
	ds	128

; キャラ消去ワーク (Page1用)
clear_char_work1:
	ds	128

;---------------------------------------------------------------;
;	キャラクタ描画処理テーブル (32種類)
;	アドレスをL,Hで分離。
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
; レンダリングテーブル 32種類
; ここは上位のみ。
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
;	キャラクタ消去処理テーブル (32種類)
;	アドレスをL,Hで分ける。
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
; 消去テーブル 32種類
; ここは上位のみ。
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
; キャラクタ描画 (任意サイズ,クリッピング付き)
; 引数:
;	DEreg: posx
;	Areg: posy
;	HLreg: image data
;		+0 クリップY情報 (200-sizey-1)
;		+1 描画タイプ (0: RGB/SizeY:12 010h: B /SizeY:12 )
;		+2 クリップ右情報 (40-sizex+1)
;		+3 クリップ左情報 (64-sizex+1)
;		+4 サイズX (byte単位)
;		+5 サイズY (ピクセル単位)
;---------------------------------------------------------------;
render_chara_image_w:



; VRAMアドレスを求める。
	; Ypos クリップ情報
	cp	(hl)
	ret	nc

	inc	hl

	ex	de,hl

	push hl			; XposをPush.

	; Y座標からVRAMアドレス(Blue)を求める。
	; 同時に描画ページ (00 or 04h) を ORする。
	ld	l,a
	ld	h,VRAM_ADRS_TBL_H
	ld	b,(hl)
	inc h
	ld	c,(hl)

	; Y座標に合わせた描画処理を自己書換えでセットする。
	and	07h
;;	add	a,a
	ld	l,a

	ld	a,(de)	; 描画タイプを or。
	inc	de
	or	l
	; キャラ削除用に描画タイプを自己書換え位置に書き込む。
	ld	( draw_type_buff+1 ),a

	; 描画処理先を求めて自己書換えする。
	ld		l,a
	ld		h, render_chara_jump_tbl >> 8

	ld		a,(hl)
	inc		h
	ld		h,(hl)
	ld		l,a

	ld	(image_jump+1),hl

	; フリップページをVRAMアドレスに反映する。
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

	; HLreg ← VRAM Adrs.(X方向をもう足している)

	ex	de,hl

	cp	(hl)	; 右クリップ判定
	inc	hl
	jp	c, rciw_2

	cp	40
	jp	c, rciw_3

	cp	(hl)	; 左クリップ判定
	inc	hl
	ret	c

; 左クリップ処理

	; 左端のクリップなので BCregにはx=0のVRAMアドレスが入っている。

	xor	03fh	; 左にクリップアウトした幅 (64-xpos)
	inc	a
	ld	d,a

	ld	a,(hl)	; Xサイズ - クリップアウト幅
	inc	hl
	sub	d

	; Areg: 画面内にはみ出た幅(=描画幅)を裏レジスタへ。
	ex af,af'

	ld	e,(hl)	; ピッチYをデータに足してスキップする。
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
; 右クリップ処理
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
; 画面内なのでクリップ処理が不要な時
	ld	b,d
	ld	c,e

	inc	hl		; 左クリップ値はスキップ

	ld a,(hl)	; 横サイズ(8ドット単位)
	inc hl

	inc hl		; 縦サイズはスキップ

	; Areg: 横サイズのループ

rciw_1:
	; 削除キャラバッファへの書き込み
	ex	de,hl
del_char_write_w:
	ld	hl,0000

	ld	(hl),c
	inc	l
	ld	(hl),b
	inc	l

draw_type_buff:
	ld	(hl), 00h	; 自己書換えで描画タイプを書き込み
	inc	l

	ld	(hl),a		; Xサイズ(キャラ単位)
	inc	l

	; 自己書換えで書込みアドレスを更新する。
	ld	( del_char_write_w+1 ),hl
	ex	de,hl

rciw_5:
	ex af,af'

	push bc

	; 自己書き換えで描画処理へジャンプする。
image_jump:
	call 0000h

	pop bc
	inc bc		; X方向に +8

	ex af,af'
	dec a
	jp nz, rciw_5

	ret



;---------------------------------------------------------------; 
;	各キャラクタの描画
;---------------------------------------------------------------; 
draw_chara_manager:

	di

	; 消去バッファを使って前回書いた所を消す。
	call	update_clear_buff_w

	ld		iy, chara_work
	ld		b, CHARA_NUM
cmd_1:
	ld		l,(iy+CHR_PATTERN)

	; キャラパターンが0かどうか判定
	; →最下位ビットを表示/非表示フラグにする(0で表示、1で非表示)
	bit		0,l
	; inc		l
	; dec		l
	jp		nz,cmd_2
;
	; キャラデータをDEregに取得する。

	push	bc

	inc		l

	ld		h, chara_pivot_table >> 8

	; Ypos+PivotYを計算してA'regに保持。
	ld		a, (iy+CHR_POSYH)
	add		a, (hl)
	ex		af,af'

	dec		l

	; X座標は上位9bitが整数部,下位7bitが小数部となっている。
	; 1bitシフトして整数部を求める。

	; Xpos(上位8bit)とPivotXを足す。(ゆえにPivotXは2の倍数単位)
	ld		a, (iy+CHR_POSXH)
	add		a, (hl)
	ld		c,a

;;	dec		l			; Indexを戻す。

	ld		a, (iy+CHR_POSXL)
	rlca	; 7bit目をCyに入れる。
	rl		c
	ld		b,00h	; 7
	rl		b		; 8

	; BCreg: Xpos

	; X方向のオフセット(0-7)とキャラパターンに対応したデータアドレスを取得する。
	ld		a, 07h
	and		c
	add		a, chara_data_table >> 8
	ld		h,a

	ld		e,(hl)
	inc		l
	ld		d,(hl)

	; DEreg: キャラクタデータ
	ex		de,hl

	ld		e,c
	ld		d,b

	; Ypos+PivotYを復帰。
	ex		af,af'

	call	render_chara_image_w

	call	check_vsync_state

cmd_3:
	pop		bc

cmd_2:
	ld		de, CHR_SIZE
	add		iy,de

	djnz	cmd_1

	; 今回の削除バッファ数を求める。
	call	calc_del_char_num

	ei

	ret


;----
;	END
