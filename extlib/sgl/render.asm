;---------------------------------------------------------------;
;	Copyright (c) 2019 render.asm
;	This software is released under the MIT License.
;	http://opensource.org/licenses/mit-license.php
;---------------------------------------------------------------; 

;---------------------------------------------------------------;
; BRG 1ライン描画
; 引数
;	HLreg: キャラデータ
;	BCreg: 描画VRAMアドレス
;	Ereg: ビットラインデータ
;---------------------------------------------------------------;
rc_image_01:
	ld	d,b		; DregにBregをバッファ。

	ld	a,b
	or	BITLINE_MASK
	ld	b,a

	ld	a,(bc)
	and	e
	jp	nz, rc_blend_01

	ld	a,(bc)
	or	e
	ld	(bc),a

	ld	b,d		; Bregに復帰。

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

	ld	b,d		; Bregに復帰。
	ld	d, 40h

	jp	brg_blend_01

;---------------------------------------------------------------;
; BRG 2ライン描画
; 引数
;	HLreg: キャラデータ
;	BCreg: 描画VRAMアドレス
;	Ereg: ビットラインデータ
;---------------------------------------------------------------;
rc_image_02:
	ld	d,b		; DregにBregをバッファ。

	ld	a,b
	or	BITLINE_MASK
	ld	b,a

	ld	a,(bc)
	and	e
	jp	nz, rc_blend_02

	ld	a,(bc)
	or	e
	ld	(bc),a

	ld	b,d		; Bregに復帰。

; wirte
	ld de, 04088h

	inc b
	ld a,b

	jp	brg_write_02

rc_blend_02:
	ld	a,(bc)
	or	e
	ld	(bc),a

	ld	b,d		; Bregに復帰。
	ld	d, 40h

	jp	brg_blend_02


;---------------------------------------------------------------;
; 3ライン描画
; 引数
;	HLreg: キャラデータ
;	BCreg: 描画VRAMアドレス
;	Ereg: ビットラインデータ
;---------------------------------------------------------------;
rc_image_03:

	ld	d,b		; DregにBregをバッファ。

	ld	a,b
	or	BITLINE_MASK
	ld	b,a

	ld	a,(bc)
	and	e
	jp	nz, rc_blend_03

	ld	a,(bc)
	or	e
	ld	(bc),a

	ld	b,d		; Bregに復帰。

; wirte
	ld de, 04088h

	inc b
	ld a,b

	jp	brg_write_03

rc_blend_03:
	ld	a,(bc)
	or	e
	ld	(bc),a

	ld	b,d		; Bregに復帰。
	ld	d, 40h

	jp	brg_blend_03


;---------------------------------------------------------------;
; 4ライン描画
; 引数
;	HLreg: キャラデータ
;	BCreg: 描画VRAMアドレス
;	Ereg: ビットラインデータ
;---------------------------------------------------------------;
rc_image_04:
	ld	d,b		; DregにBregをバッファ。

	ld	a,b
	or	BITLINE_MASK
	ld	b,a

	ld	a,(bc)
	and	e
	jp	nz, rc_blend_04

	ld	a,(bc)
	or	e
	ld	(bc),a

	ld	b,d		; Bregに復帰。

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

	ld	b,d		; Bregに復帰。
	ld	d, 40h

	jp	brg_blend_04


;---------------------------------------------------------------;
; 5ライン描画
; 引数
;	HLreg: キャラデータ
;	BCreg: 描画VRAMアドレス
;	Ereg: ビットラインデータ
;---------------------------------------------------------------;
rc_image_05:
	ld	d,b		; DregにBregをバッファ。

	ld	a,b
	or	BITLINE_MASK
	ld	b,a

	ld	a,(bc)
	and	e
	jp	nz, rc_blend_05

	ld	a,(bc)
	or	e
	ld	(bc),a

	ld	b,d		; Bregに復帰。

; wirte
	ld de, 04088h

	inc b
	ld a,b

	jp	brg_write_05


rc_blend_05:
	ld	a,(bc)
	or	e
	ld	(bc),a

	ld	b,d		; Bregに復帰。
	ld	d, 40h

	jp	brg_blend_05

;---------------------------------------------------------------;
; 6ライン描画
; 引数
;	HLreg: キャラデータ
;	BCreg: 描画VRAMアドレス
;	Ereg: ビットラインデータ
;---------------------------------------------------------------;
rc_image_06:
	ld	d,b		; DregにBregをバッファ。

	ld	a,b
	or	BITLINE_MASK
	ld	b,a

	ld	a,(bc)
	and	e
	jp	nz, rc_blend_06

	ld	a,(bc)
	or	e
	ld	(bc),a

	ld	b,d		; Bregに復帰。

; wirte
	ld de, 04088h

	inc b
	ld a,b

	jp	brg_write_06

rc_blend_06:
	ld	a,(bc)
	or	e
	ld	(bc),a

	ld	b,d		; Bregに復帰。
	ld	d, 40h

	jp	brg_blend_06


;---------------------------------------------------------------;
; 7ライン描画
; 引数
;	HLreg: キャラデータ
;	BCreg: 描画VRAMアドレス
;	Ereg: ビットラインデータ
;---------------------------------------------------------------;
rc_image_07:
	ld	d,b		; DregにBregをバッファ。

	ld	a,b
	or	BITLINE_MASK
	ld	b,a

	ld	a,(bc)
	and	e
	jp	nz, rc_blend_07

	ld	a,(bc)
	or	e
	ld	(bc),a

	ld	b,d		; Bregに復帰。

; wirte
	ld de, 04088h

	inc b
	ld a,b

	jp	brg_write_07

rc_blend_07:
	ld	a,(bc)
	or	e
	ld	(bc),a

	ld	b,d		; Bregに復帰。
	ld	d, 40h

	jp	brg_blend_07


;---------------------------------------------------------------;
; 8ライン描画
; 引数
;	HLreg: キャラデータ
;	BCreg: 描画VRAMアドレス
;---------------------------------------------------------------;
rc_image_08:
	; VRAM Adrs(BCreg)からBitLineBuffを求める。
	; BitLineBuffは 0f8xxにあるので、f800を ORすると求まる。

; Ereg: ビットラインデータ

	ld	d,b		; DregにBregをバッファ。

	ld	a,b
	or	BITLINE_MASK
	ld	b,a

	ld	a,(bc)
	or	a
	jp	nz, rc_blend_08

	ld	a,0ffh
	ld	(bc),a

	ld	b,d		; Bregに復帰。


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

	ld	b,d		; Bregに復帰。

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
; プレーン B 1ライン描画
; 引数
;	HLreg: キャラデータ
;	BCreg: 描画VRAMアドレス
;	Ereg: ビットラインデータ
;---------------------------------------------------------------;
rc_image_b1:
	ld	d,b		; DregにBregをバッファ。

	ld	a,b
	or	BITLINE_MASK
	ld	b,a

	ld	a,(bc)
	and	e
	jp	nz, rc_blend_b1

	ld	a,(bc)
	or	e
	ld	(bc),a

	ld	b,d		; Bregに復帰。

; wirte
	inc b
;;	ld a,b

	OUT_B_HL
	ADD_B_80	; 整合性のため B→G

	ret

rc_blend_b1:
	ld	a,(bc)
	or	e
	ld	(bc),a

	ld	b,d		; Bregに復帰。
	ld	d, 040h

	BLEND_B_HL_ADD_B_D

	ret

;---------------------------------------------------------------;
; プレーン B 2ライン描画
; 引数
;	HLreg: キャラデータ
;	BCreg: 描画VRAMアドレス
;	Ereg: ビットラインデータ
;---------------------------------------------------------------;
rc_image_b2:
	ld	d,b		; DregにBregをバッファ。

	ld	a,b
	or	BITLINE_MASK
	ld	b,a

	ld	a,(bc)
	and	e
	jp	nz, rc_blend_b2

	ld	a,(bc)
	or	e
	ld	(bc),a

	ld	b,d		; Bregに復帰。

; wirte
	ld	e, 008h

	inc b
	ld a,b

	OUT_B_HL_ADD_E
	OUT_B_HL

	ADD_B_80	; 整合性のため B→G

	ret

rc_blend_b2:
	ld	a,(bc)
	or	e
	ld	(bc),a

	ld	b,d		; Bregに復帰。
	ld	d, 40h

	jp	b_blend_02

;---------------------------------------------------------------;
; プレーン B 3ライン描画
; 引数
;	HLreg: キャラデータ
;	BCreg: 描画VRAMアドレス
;	Ereg: ビットラインデータ
;---------------------------------------------------------------;
rc_image_b3:
	ld	d,b		; DregにBregをバッファ。

	ld	a,b
	or	BITLINE_MASK
	ld	b,a

	ld	a,(bc)
	and	e
	jp	nz, rc_blend_b3

	ld	a,(bc)
	or	e
	ld	(bc),a

	ld	b,d		; Bregに復帰。

; wirte
	ld	e, 008h

	inc b
	ld a,b

	OUT_B_HL_ADD_E
	OUT_B_HL_ADD_E
	OUT_B_HL

	ADD_B_80	; 整合性のため B→G

	ret

rc_blend_b3:
	ld	a,(bc)
	or	e
	ld	(bc),a

	ld	b,d		; Bregに復帰。
	ld	d, 40h

	jp	b_blend_03


;---------------------------------------------------------------;
; プレーン B 4ライン描画
; 引数
;	HLreg: キャラデータ
;	BCreg: 描画VRAMアドレス
;	Ereg: ビットラインデータ
;---------------------------------------------------------------;
rc_image_b4:
	ld	d,b		; DregにBregをバッファ。

	ld	a,b
	or	BITLINE_MASK
	ld	b,a

	ld	a,(bc)
	and	e
	jp	nz, rc_blend_b4

	ld	a,(bc)
	or	e
	ld	(bc),a

	ld	b,d		; Bregに復帰。

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

	ADD_B_80	; 整合性のため B→G

	ret

rc_blend_b4:
	ld	a,(bc)
	or	e
	ld	(bc),a

	ld	b,d		; Bregに復帰。
	ld	d, 40h

	jp	b_blend_04

;---------------------------------------------------------------;
; プレーン B 5ライン描画
; 引数
;	HLreg: キャラデータ
;	BCreg: 描画VRAMアドレス
;	Ereg: ビットラインデータ
;---------------------------------------------------------------;
rc_image_b5:
	ld	d,b		; DregにBregをバッファ。

	ld	a,b
	or	BITLINE_MASK
	ld	b,a

	ld	a,(bc)
	and	e
	jp	nz, rc_blend_b5

	ld	a,(bc)
	or	e
	ld	(bc),a

	ld	b,d		; Bregに復帰。

; wirte
	ld	e, 008h

	inc b
	ld a,b

	OUT_B_HL_ADD_E
	OUT_B_HL_ADD_E
	OUT_B_HL_ADD_E
	OUT_B_HL_ADD_E
	OUT_B_HL

	ADD_B_80	; 整合性のため B→G

	ret

rc_blend_b5:
	ld	a,(bc)
	or	e
	ld	(bc),a

	ld	b,d		; Bregに復帰。
	ld	d, 40h

	jp	b_blend_05

;---------------------------------------------------------------;
; プレーン B 6ライン描画
; 引数
;	HLreg: キャラデータ
;	BCreg: 描画VRAMアドレス
;	Ereg: ビットラインデータ
;---------------------------------------------------------------;
rc_image_b6:
	ld	d,b		; DregにBregをバッファ。

	ld	a,b
	or	BITLINE_MASK
	ld	b,a

	ld	a,(bc)
	and	e
	jp	nz, rc_blend_b6

	ld	a,(bc)
	or	e
	ld	(bc),a

	ld	b,d		; Bregに復帰。

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

	ADD_B_80	; 整合性のため B→G

	ret

rc_blend_b6:
	ld	a,(bc)
	or	e
	ld	(bc),a

	ld	b,d		; Bregに復帰。
	ld	d, 40h

	jp	b_blend_06

;---------------------------------------------------------------;
; プレーン B 7ライン描画
; 引数
;	HLreg: キャラデータ
;	BCreg: 描画VRAMアドレス
;	Ereg: ビットラインデータ
;---------------------------------------------------------------;
rc_image_b7:
	ld	d,b		; DregにBregをバッファ。

	ld	a,b
	or	BITLINE_MASK
	ld	b,a

	ld	a,(bc)
	and	e
	jp	nz, rc_blend_b7

	ld	a,(bc)
	or	e
	ld	(bc),a

	ld	b,d		; Bregに復帰。

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

	ADD_B_80	; 整合性のため B→G

	ret

rc_blend_b7:
	ld	a,(bc)
	or	e
	ld	(bc),a

	ld	b,d		; Bregに復帰。
	ld	d, 40h

	jp	b_blend_07


;---------------------------------------------------------------;
; プレーン: B 8ライン描画
; 引数
;	HLreg: キャラデータ
;	BCreg: 描画VRAMアドレス
;---------------------------------------------------------------;
rc_image_b8:
	; VRAM Adrs(BCreg)からBitLineBuffを求める。
	; BitLineBuffは 0f8xxにあるので、f800を ORすると求まる。

; Ereg: ビットラインデータ

	ld	d,b		; DregにBregをバッファ。

	ld	a,b
	or	BITLINE_MASK
	ld	b,a

	ld	a,(bc)
	or	a
	jp	nz,	rc_blend_b8

	ld	a,0ffh
	ld	(bc),a

	ld	b,d		; Bregに復帰。

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

	ADD_B_80	; 整合性のため B→G

	ret

rc_blend_b8:
	ld	a,0ffh
	ld	(bc),a

	ld	b,d		; Bregに復帰。
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
; 1ライン消去
; 引数
;	BCreg: 描画VRAMアドレス
;	Ereg: BitLineデータ リセット用に 指定ビットを反転したもの。
;	Hreg: 次段加算用 08h
;	Lreg: VRAMへの出力値 00h
;---------------------------------------------------------------;
clear_image_01:
	; VRAM Adrs(BCreg)からBitLineBuffを求める。
	; BitLineBuffは 0f8xxにあるので、f800を ORすると求まる。

; Ereg: ビットラインデータ

	ld	d,b		; DregにBregをバッファ。

	; 消去する必要があるかどうかBitLineバッファをチェック。
	ld	a,b
	or	BITLINE_MASK
	ld	b,a

	ld	a,(bc)
	and	e
	ld	(bc),a

	ld	b,d		; Bregに復帰。

	out	(c),l		; 1

	ret

;---------------------------------------------------------------;
; 2ライン消去
; 引数
;	BCreg: 描画VRAMアドレス
;	Ereg: BitLineデータ リセット用に 指定ビットを反転したもの。
;	Hreg: 次段加算用 08h
;	Lreg: VRAMへの出力値 00h
;---------------------------------------------------------------;
clear_image_02:
	; VRAM Adrs(BCreg)からBitLineBuffを求める。
	; BitLineBuffは 0f8xxにあるので、f800を ORすると求まる。

; Ereg: ビットラインデータ

	ld	d,b		; DregにBregをバッファ。

	; 消去する必要があるかどうかBitLineバッファをチェック。
	ld	a,b
	or	BITLINE_MASK
	ld	b,a

	; BitLineバッファにマスク部分の0を書き込む。
	ld	a,(bc)
	and	e
	ld	(bc),a

	ld	b,d		; Bregに復帰。
	ld	a,b		; VRAM計算用にAregにもBregを入れておく。

	; 加算用に設定
	ld	h,08h

	OUT_L_ADD_H		; 0
	out	(c),l		; 1

	ret

;---------------------------------------------------------------;
; 3ライン消去
; 引数
;	BCreg: 描画VRAMアドレス
;	Ereg: BitLineデータ リセット用に 指定ビットを反転したもの。
;	Hreg: 次段加算用 08h
;	Lreg: VRAMへの出力値 00h
;---------------------------------------------------------------;
clear_image_03:
	; VRAM Adrs(BCreg)からBitLineBuffを求める。
	; BitLineBuffは 0f8xxにあるので、f800を ORすると求まる。

; Ereg: ビットラインデータ

	ld	d,b		; DregにBregをバッファ。

	; 消去する必要があるかどうかBitLineバッファをチェック。
	ld	a,b
	or	BITLINE_MASK
	ld	b,a

	; BitLineバッファにマスク部分の0を書き込む。
	ld	a,(bc)
	and	e
	ld	(bc),a

	ld	b,d		; Bregに復帰。
	ld	a,b		; VRAM計算用にAregにもBregを入れておく。

	; 加算用に設定
	ld	h,08h

	OUT_L_ADD_H		; 0
	OUT_L_ADD_H		; 1
	out	(c),l		; 2

	ret

;---------------------------------------------------------------;
; 4ライン消去
; 引数
;	BCreg: 描画VRAMアドレス
;	Dreg: BitLineデータ
;	Ereg: BitLineマスクデータ (Dreg を反転したもの)
;	Hreg: 次段加算用 08h
;	Lreg: VRAMへの出力値 00h
; Hregを破壊する場合がある。
;---------------------------------------------------------------;
clear_image_04:
	; VRAM Adrs(BCreg)からBitLineBuffを求める。
	; BitLineBuffは 0f8xxにあるので、f800を ORすると求まる。

; Ereg: ビットラインデータ

	ld	h,b		; HregにBregをバッファ。

	; 消去する必要があるかどうかBitLineバッファをチェック。
	ld	a,b
	or	BITLINE_MASK
	ld	b,a

	ld	a,(bc)
	and	d
	ret	z

	; BitLineバッファにマスク部分の0を書き込む。
	ld	a,(bc)
	and	e
	ld	(bc),a

	ld	b,h		; Bregに復帰。
	ld	a,b		; VRAM計算用にAregにもBregを入れておく。

	; 加算用に設定
	ld	h,08h

	OUT_L_ADD_H		; 0
	OUT_L_ADD_H		; 1
	OUT_L_ADD_H		; 2
	out	(c),l		; 3

	ret

;---------------------------------------------------------------;
; 5ライン消去
; 引数
;	BCreg: 描画VRAMアドレス
;	Dreg: BitLineデータ
;	Ereg: BitLineマスクデータ (Dreg を反転したもの)
;	Hreg: 次段加算用 08h
;	Lreg: VRAMへの出力値 00h
; Hregを破壊する場合がある。
;---------------------------------------------------------------;
clear_image_05:
	; VRAM Adrs(BCreg)からBitLineBuffを求める。
	; BitLineBuffは 0f8xxにあるので、f800を ORすると求まる。

; Ereg: ビットラインデータ

	ld	h,b		; HregにBregをバッファ。

	; 消去する必要があるかどうかBitLineバッファをチェック。
	ld	a,b
	or	BITLINE_MASK
	ld	b,a

	ld	a,(bc)
	and	d
	ret	z

	; BitLineバッファにマスク部分の0を書き込む。
	ld	a,(bc)
	and	e
	ld	(bc),a

	ld	b,h		; Bregに復帰。
	ld	a,b		; VRAM計算用にAregにもBregを入れておく。

	; 加算用に設定
	ld	h,08h

	OUT_L_ADD_H		; 0
	OUT_L_ADD_H		; 1
	OUT_L_ADD_H		; 2
	OUT_L_ADD_H		; 3
	out	(c),l		; 4

	ret

;---------------------------------------------------------------;
; 6ライン消去
; 引数
;	BCreg: 描画VRAMアドレス
;	Dreg: BitLineデータ
;	Ereg: BitLineマスクデータ (Dreg を反転したもの)
;	Hreg: 次段加算用 08h
;	Lreg: VRAMへの出力値 00h
; Hregを破壊する場合がある。
;---------------------------------------------------------------;
clear_image_06:
	; VRAM Adrs(BCreg)からBitLineBuffを求める。
	; BitLineBuffは 0f8xxにあるので、f800を ORすると求まる。

; Ereg: ビットラインデータ

	ld	h,b		; HregにBregをバッファ。

	; 消去する必要があるかどうかBitLineバッファをチェック。
	ld	a,b
	or	BITLINE_MASK
	ld	b,a

	ld	a,(bc)
	and	d
	ret	z

	; BitLineバッファにマスク部分の0を書き込む。
	ld	a,(bc)
	and	e
	ld	(bc),a

	ld	b,h		; Bregに復帰。
	ld	a,b		; VRAM計算用にAregにもBregを入れておく。

	; 加算用に設定
	ld	h,08h

	OUT_L_ADD_H		; 0
	OUT_L_ADD_H		; 1
	OUT_L_ADD_H		; 2
	OUT_L_ADD_H		; 3
	OUT_L_ADD_H		; 4
	out	(c),l		; 5

	ret


;---------------------------------------------------------------;
; 7ライン消去
; 引数
;	BCreg: 描画VRAMアドレス
;	Dreg: BitLineデータ
;	Ereg: BitLineマスクデータ (Dreg を反転したもの)
;	Hreg: 次段加算用 08h
;	Lreg: VRAMへの出力値 00h
; Hregを破壊する場合がある。
;---------------------------------------------------------------;
clear_image_07:
	; VRAM Adrs(BCreg)からBitLineBuffを求める。
	; BitLineBuffは 0f8xxにあるので、f800を ORすると求まる。

; Ereg: ビットラインデータ

	ld	h,b		; HregにBregをバッファ。

	; 消去する必要があるかどうかBitLineバッファをチェック。
	ld	a,b
	or	BITLINE_MASK
	ld	b,a

	ld	a,(bc)
	and	d
	ret	z

	; BitLineバッファにマスク部分の0を書き込む。
	ld	a,(bc)
	and	e
	ld	(bc),a

	ld	b,h		; Bregに復帰。
	ld	a,b		; VRAM計算用にAregにもBregを入れておく。

	; 加算用に設定
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
; 8ライン消去
; 引数
;	BCreg: 描画VRAMアドレス
;	Hreg: 次段加算用 08h
;	Lreg: VRAMへの出力値 00h
;---------------------------------------------------------------;
clear_image_08:
	; VRAM Adrs(BCreg)からBitLineBuffを求める。
	; BitLineBuffは 0f8xxにあるので、f800を ORすると求まる。

; Ereg: ビットラインデータ

	ld	d,b		; DregにBregをバッファ。

	; 消去する必要があるかどうかBitLineバッファをチェック。
	ld	a,b
	or	BITLINE_MASK
	ld	b,a

	ld	a,(bc)
	or	a
	ret	z

	; BitLineバッファに消去済みの0を書き込む。
	xor	a
	ld	(bc),a

	ld	b,d		; Bregに復帰。
	ld	a,b		; VRAM計算用にAregにもBregを入れておく。

	; 加算用に設定
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
; RG 1ライン描画
; 引数
;	HLreg: キャラデータ
;	BCreg: 描画VRAMアドレス
;	Ereg: ビットラインデータ
;---------------------------------------------------------------;
rc_image_rg_01:
	ld	d,b		; DregにBregをバッファ。

	ld	a,b
	or	BITLINE_MASK
	ld	b,a

	ld	a,(bc)
	and	e
	jp	nz, rc_blend_rg_01

	ld	a,(bc)	; BitLineにフラグを立てる。
	or	e
	ld	(bc),a

	ld	b,d		; Bregに復帰。

; wirte
	ld	de, 040c8h	; Eregは-40+8の値。

	; OUTI用に+1しておく。
	inc b

	; Blueプレーンのアドレスなので足してRedにする。
	; Aregにも値を残しておく。
	ld	a,b
	add	a,d
	ld	b,a

	OUT_RG_HL_ADD_D		; 0

	ret

rc_blend_rg_01:
	ld	a,(bc)
	or	e
	ld	(bc),a

	ld	b,d		; Bregに復帰。
	ld	d, 40h

	BLEND_RG_HL_ADD_B_D	; 0

	ret

;---------------------------------------------------------------;
; RG 2ライン描画
; 引数
;	HLreg: キャラデータ
;	BCreg: 描画VRAMアドレス
;	Ereg: ビットラインデータ
;---------------------------------------------------------------;
rc_image_rg_02:
	ld	d,b		; DregにBregをバッファ。

	ld	a,b
	or	BITLINE_MASK
	ld	b,a

	ld	a,(bc)
	and	e
	jp	nz, rc_blend_rg_02

	ld	a,(bc)	; BitLineにフラグを立てる。
	or	e
	ld	(bc),a

	ld	b,d		; Bregに復帰。

; wirte
	ld	de, 040c8h	; Eregは-40+8の値。

	; OUTI用に+1しておく。
	inc b

	; Blueプレーンのアドレスなので足してRedにする。
	; Aregにも値を残しておく。
	ld	a,b
	add	a,d
	ld	b,a

	jp	rg_write_02

rc_blend_rg_02:
	ld	a,(bc)
	or	e
	ld	(bc),a

	ld	b,d		; Bregに復帰。
	ld	d, 40h

	jp	rg_blend_02

;---------------------------------------------------------------;
; RG 3ライン描画
; 引数
;	HLreg: キャラデータ
;	BCreg: 描画VRAMアドレス
;	Ereg: ビットラインデータ
;---------------------------------------------------------------;
rc_image_rg_03:
	ld	d,b		; DregにBregをバッファ。

	ld	a,b
	or	BITLINE_MASK
	ld	b,a

	ld	a,(bc)
	and	e
	jp	nz, rc_blend_rg_03

	ld	a,(bc)	; BitLineにフラグを立てる。
	or	e
	ld	(bc),a

	ld	b,d		; Bregに復帰。

; wirte
	ld	de, 040c8h	; Eregは-40+8の値。

	; OUTI用に+1しておく。
	inc b

	; Blueプレーンのアドレスなので足してRedにする。
	; Aregにも値を残しておく。
	ld	a,b
	add	a,d
	ld	b,a

	jp	rg_write_03

rc_blend_rg_03:
	ld	a,(bc)
	or	e
	ld	(bc),a

	ld	b,d		; Bregに復帰。
	ld	d, 40h

	jp	rg_blend_03

;---------------------------------------------------------------;
; RG 4ライン描画
; 引数
;	HLreg: キャラデータ
;	BCreg: 描画VRAMアドレス
;	Ereg: ビットラインデータ
;---------------------------------------------------------------;
rc_image_rg_04:
	ld	d,b		; DregにBregをバッファ。

	ld	a,b
	or	BITLINE_MASK
	ld	b,a

	ld	a,(bc)
	and	e
	jp	nz, rc_blend_rg_04

	ld	a,(bc)	; BitLineにフラグを立てる。
	or	e
	ld	(bc),a

	ld	b,d		; Bregに復帰。

; wirte
	ld	de, 040c8h	; Eregは-40+8の値。

	; OUTI用に+1しておく。
	inc b

	; Blueプレーンのアドレスなので足してRedにする。
	; Aregにも値を残しておく。
	ld	a,b
	add	a,d
	ld	b,a

	jp	rg_write_04

rc_blend_rg_04:
	ld	a,(bc)
	or	e
	ld	(bc),a

	ld	b,d		; Bregに復帰。
	ld	d, 40h

	jp	rg_blend_04

;---------------------------------------------------------------;
; RG 5ライン描画
; 引数
;	HLreg: キャラデータ
;	BCreg: 描画VRAMアドレス
;	Ereg: ビットラインデータ
;---------------------------------------------------------------;
rc_image_rg_05:
	ld	d,b		; DregにBregをバッファ。

	ld	a,b
	or	BITLINE_MASK
	ld	b,a

	ld	a,(bc)
	and	e
	jp	nz, rc_blend_rg_05

	ld	a,(bc)	; BitLineにフラグを立てる。
	or	e
	ld	(bc),a

	ld	b,d		; Bregに復帰。

; wirte
	ld	de, 040c8h	; Eregは-40+8の値。

	; OUTI用に+1しておく。
	inc b

	; Blueプレーンのアドレスなので足してRedにする。
	; Aregにも値を残しておく。
	ld	a,b
	add	a,d
	ld	b,a

	jp	rg_write_05

rc_blend_rg_05:
	ld	a,(bc)
	or	e
	ld	(bc),a

	ld	b,d		; Bregに復帰。
	ld	d, 40h

	jp	rg_blend_05

;---------------------------------------------------------------;
; RG 6ライン描画
; 引数
;	HLreg: キャラデータ
;	BCreg: 描画VRAMアドレス
;	Ereg: ビットラインデータ
;---------------------------------------------------------------;
rc_image_rg_06:
	ld	d,b		; DregにBregをバッファ。

	ld	a,b
	or	BITLINE_MASK
	ld	b,a

	ld	a,(bc)
	and	e
	jp	nz, rc_blend_rg_06

	ld	a,(bc)	; BitLineにフラグを立てる。
	or	e
	ld	(bc),a

	ld	b,d		; Bregに復帰。

; wirte
	ld	de, 040c8h	; Eregは-40+8の値。

	; OUTI用に+1しておく。
	inc b

	; Blueプレーンのアドレスなので足してRedにする。
	; Aregにも値を残しておく。
	ld	a,b
	add	a,d
	ld	b,a

	jp	rg_write_06

rc_blend_rg_06:
	ld	a,(bc)
	or	e
	ld	(bc),a

	ld	b,d		; Bregに復帰。
	ld	d, 40h

	jp	rg_blend_06

;---------------------------------------------------------------;
; RG 7ライン描画
; 引数
;	HLreg: キャラデータ
;	BCreg: 描画VRAMアドレス
;	Ereg: ビットラインデータ
;---------------------------------------------------------------;
rc_image_rg_07:
	ld	d,b		; DregにBregをバッファ。

	ld	a,b
	or	BITLINE_MASK
	ld	b,a

	ld	a,(bc)
	and	e
	jp	nz, rc_blend_rg_07

	ld	a,(bc)	; BitLineにフラグを立てる。
	or	e
	ld	(bc),a

	ld	b,d		; Bregに復帰。

; wirte
	ld	de, 040c8h	; Eregは-40+8の値。

	; OUTI用に+1しておく。
	inc b

	; Blueプレーンのアドレスなので足してRedにする。
	; Aregにも値を残しておく。
	ld	a,b
	add	a,d
	ld	b,a

	jp	rg_write_07

rc_blend_rg_07:
	ld	a,(bc)
	or	e
	ld	(bc),a

	ld	b,d		; Bregに復帰。
	ld	d, 40h

	jp	rg_blend_07

;---------------------------------------------------------------;
; RG 8ライン描画
; 引数
;	HLreg: キャラデータ
;	BCreg: 描画VRAMアドレス
;---------------------------------------------------------------;
rc_image_rg_08:
	; VRAM Adrs(BCreg)からBitLineBuffを求める。
	; BitLineBuffは 0f8xxにあるので、f800を ORすると求まる。

; Ereg: ビットラインデータ

	ld	d,b		; DregにBregをバッファ。

	ld	a,b
	or	BITLINE_MASK
	ld	b,a

	ld	a,(bc)
	or	a
	jp	nz, rc_blend_rg_08

	ld	a,0ffh
	ld	(bc),a

	ld	b,d		; Bregに復帰。

; wirte
	ld	de, 040c8h	; Eregは-40+8の値。

	; OUTI用に+1しておく。
	inc b

	; Blueプレーンのアドレスなので足してRedにする。
	; Aregにも値を残しておく。
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
	ld	a,0ffh	; Bitラインに書き込み。
	ld	(bc),a

	ld	b,d		; Bregに復帰。
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
; Blue Green 1ライン描画
; 引数
;	HLreg: キャラデータ
;	BCreg: 描画VRAMアドレス (Blueプレーンアドレス)
;	Ereg: ビットラインデータ
;---------------------------------------------------------------;
rc_image_bg_01:
	ld	d,b		; DregにBregをバッファ。

	ld	a,b
	or	BITLINE_MASK
	ld	b,a

	ld	a,(bc)
	and	e
	jp	nz, rc_blend_bg_01

	ld	a,(bc)	; BitLineにフラグを立てる。
	or	e
	ld	(bc),a

	ld	b,d		; Bregに復帰。

; wirte
	ld	de, 08088h	; DregはBlue→Green, Eregは-80+8の値。

	; OUTI用に+1しておく。
	inc b

	; Blueプレーンアドレス(H)をAregにも残す。
	ld	a,b

	OUT_BG_HL_ADD_D		; 0

	ret

rc_blend_bg_01:
	; BitLineにフラグを立てる。
	ld	a,(bc)
	or	e
	ld	(bc),a

	ld	b,d		; Bregに復帰。
	ld	d, 40h	; 次プレーン算出用 (RGBプレーンに書込むため 040h)

	BLEND_BG_HL_ADD_B_D	; 0

	ret

;---------------------------------------------------------------;
; Blue Green 2ライン描画
; 引数
;	HLreg: キャラデータ
;	BCreg: 描画VRAMアドレス
;	Ereg: ビットラインデータ
;---------------------------------------------------------------;
rc_image_bg_02:
	ld	d,b		; DregにBregをバッファ。

	ld	a,b
	or	BITLINE_MASK
	ld	b,a

	ld	a,(bc)
	and	e
	jp	nz, rc_blend_bg_02

	ld	a,(bc)	; BitLineにフラグを立てる。
	or	e
	ld	(bc),a

	ld	b,d		; Bregに復帰。

; wirte
	ld	de, 08088h	; DregはBlue→Green, Eregは-80+8の値。

	; OUTI用に+1しておく。
	inc b

	; Blueプレーンアドレス(H)をAregにも残す。
	ld	a,b

	jp		bg_write_02


rc_blend_bg_02:
	ld	a,(bc)	; BitLineにフラグを立てる。
	or	e
	ld	(bc),a

	ld	b,d		; Bregに復帰。
	ld	d, 40h	; 次プレーン算出用 (RGBプレーンに書込むため 040h)

	jp	bg_blend_02

;---------------------------------------------------------------;
; Blue Green 3ライン描画
; 引数
;	HLreg: キャラデータ
;	BCreg: 描画VRAMアドレス
;	Ereg: ビットラインデータ
;---------------------------------------------------------------;
rc_image_bg_03:
	ld	d,b		; DregにBregをバッファ。

	ld	a,b
	or	BITLINE_MASK
	ld	b,a

	ld	a,(bc)
	and	e
	jp	nz, rc_blend_bg_03

	ld	a,(bc)	; BitLineにフラグを立てる。
	or	e
	ld	(bc),a

	ld	b,d		; Bregに復帰。

; wirte
	ld	de, 08088h	; DregはBlue→Green, Eregは-80+8の値。

	; OUTI用に+1しておく。
	inc b

	; Blueプレーンアドレス(H)をAregにも残す。
	ld	a,b

	jp		bg_write_03

rc_blend_bg_03:
	ld	a,(bc)	; BitLineにフラグを立てる。
	or	e
	ld	(bc),a

	ld	b,d		; Bregに復帰。
	ld	d, 40h	; 次プレーン算出用 (RGBプレーンに書込むため 040h)

	jp	bg_blend_03

;---------------------------------------------------------------;
; Blue Green 4ライン描画
; 引数
;	HLreg: キャラデータ
;	BCreg: 描画VRAMアドレス
;	Ereg: ビットラインデータ
;---------------------------------------------------------------;
rc_image_bg_04:
	ld	d,b		; DregにBregをバッファ。

	ld	a,b
	or	BITLINE_MASK
	ld	b,a

	ld	a,(bc)
	and	e
	jp	nz, rc_blend_bg_04

	ld	a,(bc)	; BitLineにフラグを立てる。
	or	e
	ld	(bc),a

	ld	b,d		; Bregに復帰。

; wirte
	ld	de, 08088h	; DregはBlue→Green, Eregは-80+8の値。

	; OUTI用に+1しておく。
	inc b

	; Blueプレーンアドレス(H)をAregにも残す。
	ld	a,b

	jp		bg_write_04

rc_blend_bg_04:
	ld	a,(bc)	; BitLineにフラグを立てる。
	or	e
	ld	(bc),a

	ld	b,d		; Bregに復帰。
	ld	d, 40h	; 次プレーン算出用 (RGBプレーンに書込むため 040h)

	jp	bg_blend_04

;---------------------------------------------------------------;
; Blue Green 5ライン描画
; 引数
;	HLreg: キャラデータ
;	BCreg: 描画VRAMアドレス
;	Ereg: ビットラインデータ
;---------------------------------------------------------------;
rc_image_bg_05:
	ld	d,b		; DregにBregをバッファ。

	ld	a,b
	or	BITLINE_MASK
	ld	b,a

	ld	a,(bc)
	and	e
	jp	nz, rc_blend_bg_05

	ld	a,(bc)	; BitLineにフラグを立てる。
	or	e
	ld	(bc),a

	ld	b,d		; Bregに復帰。

; wirte
	ld	de, 08088h	; DregはBlue→Green, Eregは-80+8の値。

	; OUTI用に+1しておく。
	inc b

	; Blueプレーンアドレス(H)をAregにも残す。
	ld	a,b

	jp		bg_write_05


rc_blend_bg_05:
	ld	a,(bc)	; BitLineにフラグを立てる。
	or	e
	ld	(bc),a

	ld	b,d		; Bregに復帰。
	ld	d, 40h	; 次プレーン算出用 (RGBプレーンに書込むため 040h)

	jp	bg_blend_05

;---------------------------------------------------------------;
; Blue Green 6ライン描画
; 引数
;	HLreg: キャラデータ
;	BCreg: 描画VRAMアドレス
;	Ereg: ビットラインデータ
;---------------------------------------------------------------;
rc_image_bg_06:
	ld	d,b		; DregにBregをバッファ。

	ld	a,b
	or	BITLINE_MASK
	ld	b,a

	ld	a,(bc)
	and	e
	jp	nz, rc_blend_bg_06

	ld	a,(bc)	; BitLineにフラグを立てる。
	or	e
	ld	(bc),a

	ld	b,d		; Bregに復帰。

; wirte
	ld	de, 08088h	; DregはBlue→Green, Eregは-80+8の値。

	; OUTI用に+1しておく。
	inc b

	; Blueプレーンアドレス(H)をAregにも残す。
	ld	a,b

	jp		bg_write_06

rc_blend_bg_06:
	ld	a,(bc)	; BitLineにフラグを立てる。
	or	e
	ld	(bc),a

	ld	b,d		; Bregに復帰。
	ld	d, 40h	; 次プレーン算出用 (RGBプレーンに書込むため 040h)

	jp	bg_blend_06

;---------------------------------------------------------------;
; Blue Green 7ライン描画
; 引数
;	HLreg: キャラデータ
;	BCreg: 描画VRAMアドレス
;	Ereg: ビットラインデータ
;---------------------------------------------------------------;
rc_image_bg_07:
	ld	d,b		; DregにBregをバッファ。

	ld	a,b
	or	BITLINE_MASK
	ld	b,a

	ld	a,(bc)
	and	e
	jp	nz, rc_blend_bg_07

	ld	a,(bc)	; BitLineにフラグを立てる。
	or	e
	ld	(bc),a

	ld	b,d		; Bregに復帰。

; wirte
	ld	de, 08088h	; DregはBlue→Green, Eregは-80+8の値。

	; OUTI用に+1しておく。
	inc b

	; Blueプレーンアドレス(H)をAregにも残す。
	ld	a,b

	jp		bg_write_07

rc_blend_bg_07:
	ld	a,(bc)	; BitLineにフラグを立てる。
	or	e
	ld	(bc),a

	ld	b,d		; Bregに復帰。
	ld	d, 40h	; 次プレーン算出用 (RGBプレーンに書込むため 040h)

	jp	bg_blend_07

;---------------------------------------------------------------;
; Blue Green 8ライン描画
; 引数
;	HLreg: キャラデータ
;	BCreg: 描画VRAMアドレス
;	Ereg: ビットラインデータ
;---------------------------------------------------------------;
rc_image_bg_08:
	ld	d,b		; DregにBregをバッファ。

	ld	a,b
	or	BITLINE_MASK
	ld	b,a

	ld	a,(bc)
	and	e
	jp	nz, rc_blend_bg_08

	ld	a, 0ffh; BitLineにフラグを立てる。
	ld	(bc),a

	ld	b,d		; Bregに復帰。

; wirte
	ld	de, 08088h	; DregはBlue→Green, Eregは-80+8の値。

	; OUTI用に+1しておく。
	inc b

	; Blueプレーンアドレス(H)をAregにも残す。
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
	ld	a, 0ffh		; BitLineにフラグを立てる。
	ld	(bc),a

	ld	b,d		; Bregに復帰。
	ld	d, 40h	; 次プレーン算出用 (RGBプレーンに書込むため 040h)

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

