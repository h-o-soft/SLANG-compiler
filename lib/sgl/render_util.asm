;---------------------------------------------------------------;
;	Copyright (c) 2019 render_util.asm
;	This software is released under the MIT License.
;	http://opensource.org/licenses/mit-license.php
;---------------------------------------------------------------; 

;//---------------------------------------------------------------;
;//	VRAMを指定の値で埋める。
;//		BCreg: VRAM アドレス
;//		HLreg: 長さ
;//		Dreg: 埋める値
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
	; 全部PCGアトリビュートにする。
	ld bc, ATTR_VRAM_ADRS
	ld hl, 0800h
	ld d, 07h
;;	ld d, 007h
	jr fill_vram

fill_attr_vram_line:
	; 全部PCGアトリビュートにする。
	ld bc, ATTR_VRAM_ADRS
	ld hl, 0028h
	ld d, 027h
	call	fill_vram

	ld bc, ATTR_VRAM_ADRS+0400h
	ld hl, 0028h
	ld d, 027h
	call	fill_vram

	ret

; デバッグ表示領域初期化
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

; VRAM書込み
; BCreg: 書込み先アドレス
; HLreg: 書込みサイズ
; Dreg: 書き込む内容
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
;// Text(PCG)の上にGRAMを表示する。
;// 0(黒)は下げておかないとTextが見えない。
;// 黒を使うにはどれかの色が犠牲になる。(=GRAM 7色しか見えない)
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
;		処理棒
;---------------------------------------------------------------; 
; 青色
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

; 赤色
task_bar_red:
	push	bc
	push	hl

	ld		bc,01100h
	ld		hl, ( GAME_PALET_R << 8 ) | GAME_PALET_R | 0x01
	jr		tbr_1

; 処理量で色が変わる処理バー。
; 0フレーム目: 青色
; 1フレーム目: 赤色
; 2フレーム目以降: 緑色
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

	; 元に戻す。
	out		(c),h

	ret

;---------------------------------------------------------------;
;	VRAMアドレステーブル
;	f600-f7ffh にY座標(0-199)に対応するVRAMアドレス(4000h～ Blue)テーブルを作成する。
;	f600-f6ff VRAM上位アドレス
;	f700-f7ff VRAM下位アドレス
;---------------------------------------------------------------;
create_vram_adrs_tbl:
	ld c,25

	ld de, B_VRAM_ADRS
	ld l,0	; ライン (0-199)

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
