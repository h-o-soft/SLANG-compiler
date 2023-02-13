;---------------------------------------------------------------;
;	Copyright (c) 2019 clear_buff.asm
;	This software is released under the MIT License.
;	http://opensource.org/licenses/mit-license.php
;---------------------------------------------------------------; 

; Clear Buffer.

init_clear_char_work:
	ld	a,(flip_w)
	jp	setup_clear_char_work


; 消去バッファ数を求める。
calc_del_char_num:
	ld	a, ( flip_render_w )
	or	del_char_num_w & 0ffh
	ld	l,a
	ld	h, del_char_num_w >> 8

	; 消去バッファの下位8bitが書込みアドレスを表しているので、
	; それを4で割った値が個数となる。
	ld	a, ( del_char_write_w+1 )
	and	07ch
	RRCA
	RRCA

	ld	(hl),a

	ret


; 消去バッファに登録しているVRAMをチェックしてクリアする。
; BitLineバージョン
update_clear_buff_w:
	; 前回の削除キャラバッファ数を算出する。
	ld	a, ( flip_render_w )
	or	del_char_num_w & 0ffh
	ld	l,a
	ld	h, del_char_num_w >> 8

	; 一つも無ければ何もしない。
	ld	a,(hl)
	or	a
	ret	z

	ld ixl,a	; 個数はIXLregへ。

	; 同時アクセスモードへ変更
	di
	ld bc, 01a03h
	ld de, 00b0ah	; PortC5 を 1→0にする。
	out (c),d
	out (c),e

	; 消去キャラワーク: 自己書換え
del_char_read_w:
	ld	hl, 0000h

ucbw_5:
	; 消去VRAMアドレス
	ld	c,(hl)
	inc l

	ld	a,(hl)
	and	03fh	; 同時アクセスモード(RGB)のVRAMアドレス(0000h～03fffh)へ。
	ld	b,a
	inc l

	; 描画タイプ (PosY,SizeY込みのデータ)
	ld	a,(hl)	; 
	inc	l

	ex	de,hl

	ld		l,a
	ld		h, clear_chara_jump_tbl >> 8

	; ジャンプテーブルが足りなくなったため、
	; 描画タイプを00-1ffまで拡張する。

	ld		a,(hl)
	inc		h
	ld		h,(hl)
	ld		l,a
	ld		( ucbw_1+1 ),hl

	ld	a,(de)		; Xサイズ (キャラ単位)
	inc	e

	; 消去用データ(次段加算用/VRAMに書込む値)
	ld hl,0800h

	push	de

ucbw_4:
	ex	af,af'

	push	bc
ucbw_1:
	; 消去処理(自己書換え)
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

	; 同時アクセスモードを解除。
	ld a,040h
	in a,(c)		; 040**hからin

	ei

	ret

;----
;	END
