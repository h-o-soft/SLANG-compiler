;---------------------------------------------------------------;
;	Copyright (c) 2019 render_br.asm
;	This software is released under the MIT License.
;	http://opensource.org/licenses/mit-license.php
;---------------------------------------------------------------; 

;---------------------------------------------------------------;
; Blue Red 1ライン描画
; 引数
;	HLreg: キャラデータ
;	BCreg: 描画VRAMアドレス (Blueプレーンアドレス)
;	Ereg: ビットラインデータ
;---------------------------------------------------------------;
rc_image_br_01:
	ld	d,b		; DregにBregをバッファ。

	ld	a,b
	or	BITLINE_MASK
	ld	b,a

	ld	a,(bc)
	and	e
	jp	nz, rc_blend_br_01

	ld	a,(bc)	; BitLineにフラグを立てる。
	or	e
	ld	(bc),a

	ld	b,d		; Bregに復帰。

; wirte
	ld	de, 040c8h	; DregはBlue→Red, Eregは-40+8の値。

	; OUTI用に+1しておく。
	inc b

	; Blueプレーンアドレス(H)をAregにも残す。
	ld	a,b

	OUT_BR_HL_ADD_D		; 0

	; 最後にRプレーンからGプレーン,そして OUTI用に+1していたのも減らす。
	add	a, 040h-1
	ld	b,a

	ret

rc_blend_br_01:
	; BitLineにフラグを立てる。
	ld	a,(bc)
	or	e
	ld	(bc),a

	ld	b,d		; Bregに復帰。
	ld	d, 40h	; 次プレーン算出用 (RGBプレーンに書込むため 040h)

	BLEND_BR_HL_ADD_B_D	; 0

	; 終了時はGプレーンの位置を指している。

	ret

;---------------------------------------------------------------;
; Blue Red 2ライン描画
; 引数
;	HLreg: キャラデータ
;	BCreg: 描画VRAMアドレス
;	Ereg: ビットラインデータ
;---------------------------------------------------------------;
rc_image_br_02:
	ld	d,b		; DregにBregをバッファ。

	ld	a,b
	or	BITLINE_MASK
	ld	b,a

	ld	a,(bc)
	and	e
	jp	nz, rc_blend_br_02

	ld	a,(bc)	; BitLineにフラグを立てる。
	or	e
	ld	(bc),a

	ld	b,d		; Bregに復帰。

; wirte
	ld	de, 040c8h	; DregはBlue→Red, Eregは-40+8の値。

	; OUTI用に+1しておく。
	inc b

	; Blueプレーンアドレス(H)をAregにも残す。
	ld	a,b

	jp	br_write_02

rc_blend_br_02:
	ld	a,(bc)	; BitLineにフラグを立てる。
	or	e
	ld	(bc),a

	ld	b,d		; Bregに復帰。
	ld	d, 40h	; 次プレーン算出用 (RGBプレーンに書込むため 040h)

	jp	br_blend_02

;---------------------------------------------------------------;
; Blue Red 3ライン描画
; 引数
;	HLreg: キャラデータ
;	BCreg: 描画VRAMアドレス
;	Ereg: ビットラインデータ
;---------------------------------------------------------------;
rc_image_br_03:
	ld	d,b		; DregにBregをバッファ。

	ld	a,b
	or	BITLINE_MASK
	ld	b,a

	ld	a,(bc)
	and	e
	jp	nz, rc_blend_br_03

	ld	a,(bc)	; BitLineにフラグを立てる。
	or	e
	ld	(bc),a

	ld	b,d		; Bregに復帰。

; wirte
	ld	de, 040c8h	; DregはBlue→Red, Eregは-40+8の値。

	; OUTI用に+1しておく。
	inc b

	; Blueプレーンアドレス(H)をAregにも残す。
	ld	a,b

	jp	br_write_03

rc_blend_br_03:
	ld	a,(bc)	; BitLineにフラグを立てる。
	or	e
	ld	(bc),a

	ld	b,d		; Bregに復帰。
	ld	d, 40h	; 次プレーン算出用 (RGBプレーンに書込むため 040h)

	jp	br_blend_03

;---------------------------------------------------------------;
; Blue Green 4ライン描画
; 引数
;	HLreg: キャラデータ
;	BCreg: 描画VRAMアドレス
;	Ereg: ビットラインデータ
;---------------------------------------------------------------;
rc_image_br_04:
	ld	d,b		; DregにBregをバッファ。

	ld	a,b
	or	BITLINE_MASK
	ld	b,a

	ld	a,(bc)
	and	e
	jp	nz, rc_blend_br_04

	ld	a,(bc)	; BitLineにフラグを立てる。
	or	e
	ld	(bc),a

	ld	b,d		; Bregに復帰。

; wirte
	ld	de, 040c8h	; DregはBlue→Red, Eregは-40+8の値。

	; OUTI用に+1しておく。
	inc b

	; Blueプレーンアドレス(H)をAregにも残す。
	ld	a,b

	jp	br_write_04

rc_blend_br_04:
	ld	a,(bc)	; BitLineにフラグを立てる。
	or	e
	ld	(bc),a

	ld	b,d		; Bregに復帰。
	ld	d, 40h	; 次プレーン算出用 (RGBプレーンに書込むため 040h)

	jp	br_blend_04

;---------------------------------------------------------------;
; Blue Green 5ライン描画
; 引数
;	HLreg: キャラデータ
;	BCreg: 描画VRAMアドレス
;	Ereg: ビットラインデータ
;---------------------------------------------------------------;
rc_image_br_05:
	ld	d,b		; DregにBregをバッファ。

	ld	a,b
	or	BITLINE_MASK
	ld	b,a

	ld	a,(bc)
	and	e
	jp	nz, rc_blend_br_05

	ld	a,(bc)	; BitLineにフラグを立てる。
	or	e
	ld	(bc),a

	ld	b,d		; Bregに復帰。

; wirte
	ld	de, 040c8h	; DregはBlue→Red, Eregは-40+8の値。

	; OUTI用に+1しておく。
	inc b

	; Blueプレーンアドレス(H)をAregにも残す。
	ld	a,b

	jp	br_write_05

rc_blend_br_05:
	ld	a,(bc)	; BitLineにフラグを立てる。
	or	e
	ld	(bc),a

	ld	b,d		; Bregに復帰。
	ld	d, 40h	; 次プレーン算出用 (RGBプレーンに書込むため 040h)

	jp	br_blend_05

;---------------------------------------------------------------;
; Blue Green 6ライン描画
; 引数
;	HLreg: キャラデータ
;	BCreg: 描画VRAMアドレス
;	Ereg: ビットラインデータ
;---------------------------------------------------------------;
rc_image_br_06:
	ld	d,b		; DregにBregをバッファ。

	ld	a,b
	or	BITLINE_MASK
	ld	b,a

	ld	a,(bc)
	and	e
	jp	nz, rc_blend_br_06

	ld	a,(bc)	; BitLineにフラグを立てる。
	or	e
	ld	(bc),a

	ld	b,d		; Bregに復帰。

; wirte
	ld	de, 040c8h	; DregはBlue→Red, Eregは-40+8の値。

	; OUTI用に+1しておく。
	inc b

	; Blueプレーンアドレス(H)をAregにも残す。
	ld	a,b

	jp	br_write_06

rc_blend_br_06:
	ld	a,(bc)	; BitLineにフラグを立てる。
	or	e
	ld	(bc),a

	ld	b,d		; Bregに復帰。
	ld	d, 40h	; 次プレーン算出用 (RGBプレーンに書込むため 040h)

	jp	br_blend_06

;---------------------------------------------------------------;
; Blue Green 7ライン描画
; 引数
;	HLreg: キャラデータ
;	BCreg: 描画VRAMアドレス
;	Ereg: ビットラインデータ
;---------------------------------------------------------------;
rc_image_br_07:
	ld	d,b		; DregにBregをバッファ。

	ld	a,b
	or	BITLINE_MASK
	ld	b,a

	ld	a,(bc)
	and	e
	jp	nz, rc_blend_br_07

	ld	a,(bc)	; BitLineにフラグを立てる。
	or	e
	ld	(bc),a

	ld	b,d		; Bregに復帰。

; wirte
	ld	de, 040c8h	; DregはBlue→Red, Eregは-40+8の値。

	; OUTI用に+1しておく。
	inc b

	; Blueプレーンアドレス(H)をAregにも残す。
	ld	a,b

	jp	br_write_07

rc_blend_br_07:
	ld	a,(bc)	; BitLineにフラグを立てる。
	or	e
	ld	(bc),a

	ld	b,d		; Bregに復帰。
	ld	d, 40h	; 次プレーン算出用 (RGBプレーンに書込むため 040h)

	jp	br_blend_07

;---------------------------------------------------------------;
; Blue Green 8ライン描画
; 引数
;	HLreg: キャラデータ
;	BCreg: 描画VRAMアドレス
;	Ereg: ビットラインデータ
;---------------------------------------------------------------;
rc_image_br_08:
	ld	d,b		; DregにBregをバッファ。

	ld	a,b
	or	BITLINE_MASK
	ld	b,a

	ld	a,(bc)
	and	e
	jp	nz, rc_blend_br_08

	ld	a, 0ffh; BitLineにフラグを立てる。
	ld	(bc),a

	ld	b,d		; Bregに復帰。

; wirte
	ld	de, 040c8h	; DregはBlue→Red, Eregは-40+8の値。

	; OUTI用に+1しておく。
	inc b

	; Blueプレーンアドレス(H)をAregにも残す。
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

	; 最後にRプレーンからGプレーン,そして OUTI用に+1していたのも減らす。
	add	a, 040h-1
	ld	b,a

	ret

rc_blend_br_08:
	ld	a, 0ffh		; BitLineにフラグを立てる。
	ld	(bc),a

	ld	b,d		; Bregに復帰。
	ld	d, 40h	; 次プレーン算出用 (RGBプレーンに書込むため 040h)

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

	; 終了時はGプレーンの位置を指している。

	ret


;----
;	END
