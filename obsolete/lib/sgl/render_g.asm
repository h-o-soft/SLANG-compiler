;---------------------------------------------------------------;
;	Copyright (c) 2019 render_g.asm
;	This software is released under the MIT License.
;	http://opensource.org/licenses/mit-license.php
;---------------------------------------------------------------; 

;---------------------------------------------------------------;
; プレーン G 1ライン描画
; 引数
;	HLreg: キャラデータ
;	BCreg: 描画VRAMアドレス
;	Ereg: ビットラインデータ
;---------------------------------------------------------------;
rc_image_g1:
	ld		d,b		; DregにBregをバッファ。

	ld		a,b
	or		BITLINE_MASK
	ld		b,a

	ld		a,(bc)
	and		e
	jp		nz, rc_blend_g1

	ld		a,(bc)
	or		e
	ld		(bc),a

	ld		a, 080h+1
	add		a,d
	ld		b,a

	OUT_G_HL

	; Gなので特に修正なし。

	ret

rc_blend_g1:
	ld		a,(bc)
	or		e
	ld		(bc),a

	ld		b,d		; Bregに復帰。
	ld		d, 040h

	jp		rc_blend_g1_line


;---------------------------------------------------------------;
; プレーン G 2ライン描画
; 引数
;	HLreg: キャラデータ
;	BCreg: 描画VRAMアドレス
;	Ereg: ビットラインデータ
;---------------------------------------------------------------;
rc_image_g2:
	ld		d,b		; DregにBregをバッファ。

	ld		a,b
	or		BITLINE_MASK
	ld		b,a

	ld		a,(bc)
	and		e
	jp		nz, rc_blend_g2
;
	ld		a,(bc)
	or		e
	ld		(bc),a

	; B→Gプレーンにする。更に OUTI用に+1して Aregにも残す。
	ld		a,d
	ld		de, 08008h		; Dreg: プレーン増加用, Ereg: ライン増加用
	inc		a
	add		a,d
	ld		b,a

	jp		rc_write_g2_line

rc_blend_g2:
	ld		a,(bc)
	or		e
	ld		(bc),a

	ld		b,d		; Bregに復帰。
	ld		d, 40h

	jp		rc_blend_g2_line


;---------------------------------------------------------------;
; プレーン G 3ライン描画
; 引数
;	HLreg: キャラデータ
;	BCreg: 描画VRAMアドレス
;	Ereg: ビットラインデータ
;---------------------------------------------------------------;
rc_image_g3:
	ld		d,b		; DregにBregをバッファ。

	ld		a,b
	or		BITLINE_MASK
	ld		b,a

	ld		a,(bc)
	and		e
	jp		nz, rc_blend_g3

	ld		a,(bc)
	or		e
	ld		(bc),a

	; B→Gプレーンにする。更に OUTI用に+1して Aregにも残す。
	ld		a,d
	ld		de, 08008h		; Dreg: プレーン増加用, Ereg: ライン増加用
	inc		a
	add		a,d
	ld		b,a

	jp		rc_write_g3_line

rc_blend_g3:
	ld		a,(bc)
	or		e
	ld		(bc),a

	ld		b,d		; Bregに復帰。
	ld		d, 40h

	jp		rc_blend_g3_line

;---------------------------------------------------------------;
; プレーン G 4ライン描画
; 引数
;	HLreg: キャラデータ
;	BCreg: 描画VRAMアドレス
;	Ereg: ビットラインデータ
;---------------------------------------------------------------;
rc_image_g4:
	ld		d,b		; DregにBregをバッファ。

	ld		a,b
	or		BITLINE_MASK
	ld		b,a

	ld		a,(bc)
	and		e
	jp		nz, rc_blend_g4

	ld		a,(bc)
	or		e
	ld		(bc),a

	; B→Gプレーンにする。更に OUTI用に+1して Aregにも残す。
	ld		a,d
	ld		de, 08008h		; Dreg: プレーン増加用, Ereg: ライン増加用
	inc		a
	add		a,d
	ld		b,a

	jp		rc_write_g4_line

rc_blend_g4:
	ld		a,(bc)
	or		e
	ld		(bc),a

	ld		b,d		; Bregに復帰。
	ld		d, 40h

	jp		rc_blend_g4_line

;---------------------------------------------------------------;
; プレーン G 5ライン描画
; 引数
;	HLreg: キャラデータ
;	BCreg: 描画VRAMアドレス
;	Ereg: ビットラインデータ
;---------------------------------------------------------------;
rc_image_g5:
	ld		d,b		; DregにBregをバッファ。

	ld		a,b
	or		BITLINE_MASK
	ld		b,a

	ld		a,(bc)
	and		e
	jp		nz, rc_blend_g5

	ld		a,(bc)
	or		e
	ld		(bc),a

	; B→Gプレーンにする。更に OUTI用に+1して Aregにも残す。
	ld		a,d
	ld		de, 08008h		; Dreg: プレーン増加用, Ereg: ライン増加用
	inc		a
	add		a,d
	ld		b,a

	jp		rc_write_g5_line

rc_blend_g5:
	ld		a,(bc)
	or		e
	ld		(bc),a

	ld		b,d		; Bregに復帰。
	ld		d, 40h

	jp		rc_blend_g5_line

;---------------------------------------------------------------;
; プレーン G 6ライン描画
; 引数
;	HLreg: キャラデータ
;	BCreg: 描画VRAMアドレス
;	Ereg: ビットラインデータ
;---------------------------------------------------------------;
rc_image_g6:
	ld		d,b		; DregにBregをバッファ。

	ld		a,b
	or		BITLINE_MASK
	ld		b,a

	ld		a,(bc)
	and		e
	jp		nz, rc_blend_g6

	ld		a,(bc)
	or		e
	ld		(bc),a

	; B→Gプレーンにする。更に OUTI用に+1して Aregにも残す。
	ld		a,d
	ld		de, 08008h		; Dreg: プレーン増加用, Ereg: ライン増加用
	inc		a
	add		a,d
	ld		b,a

	jp		rc_write_g6_line

rc_blend_g6:
	ld		a,(bc)
	or		e
	ld		(bc),a

	ld		b,d		; Bregに復帰。
	ld		d, 40h

	jp		rc_blend_g6_line

;---------------------------------------------------------------;
; プレーン G 7ライン描画
; 引数
;	HLreg: キャラデータ
;	BCreg: 描画VRAMアドレス
;	Ereg: ビットラインデータ
;---------------------------------------------------------------;
rc_image_g7:
	ld		d,b		; DregにBregをバッファ。

	ld		a,b
	or		BITLINE_MASK
	ld		b,a

	ld		a,(bc)
	and		e
	jp		nz, rc_blend_g7

	ld		a,(bc)
	or		e
	ld		(bc),a

	; B→Gプレーンにする。更に OUTI用に+1して Aregにも残す。
	ld		a,d
	ld		de, 08008h		; Dreg: プレーン増加用, Ereg: ライン増加用
	inc		a
	add		a,d
	ld		b,a

	jp		rc_write_g7_line

rc_blend_g7:
	ld		a,(bc)
	or		e
	ld		(bc),a

	ld		b,d		; Bregに復帰。
	ld		d, 40h

	jp		rc_blend_g7_line

;---------------------------------------------------------------;
; プレーン: R 8ライン描画
; 引数
;	HLreg: キャラデータ
;	BCreg: 描画VRAMアドレス
;---------------------------------------------------------------;
rc_image_g8:
	; VRAM Adrs(BCreg)からBitLineBuffを求める。
	; BitLineBuffは 0f8xxにあるので、f800を ORすると求まる。
	; Ereg: ビットラインデータ

	ld		d,b		; DregにBregをバッファ。

	ld		a,b
	or		BITLINE_MASK
	ld		b,a

	ld		a,(bc)
	or		a
	jp		nz, rc_blend_g8

	ld		a,0ffh
	ld		(bc),a

	; Dregを復帰して、Blue→Greenプレーンへ。
	; その際に OUTI用に+1してAregにも残す。
	ld		a,d
	ld		de, 08008h	; Dreg: プレーン増加用 Ereg: ライン増加用(08h)
	inc		a
	add		a,d
	ld		b,a

	OUT_G_HL_ADD_E
rc_write_g7_line:
	OUT_G_HL_ADD_E
rc_write_g6_line:
	OUT_G_HL_ADD_E
rc_write_g5_line:
	OUT_G_HL_ADD_E
rc_write_g4_line:
	OUT_G_HL_ADD_E
rc_write_g3_line:
	OUT_G_HL_ADD_E
rc_write_g2_line:
	OUT_G_HL_ADD_E
	OUT_G_HL

	; Gプレーンなので特に修正なし。

	ret

rc_blend_g8:
	ld		a,0ffh
	ld		(bc),a

	ld		b,d		; Bregに復帰。
	ld		d, 40h	; プレーン増加用

	; 3プレーンとブレンドするのでBプレーンでOK.

	BLEND_G_HL_ADD_B_D
	ADD_B_88

rc_blend_g7_line:
	BLEND_G_HL_ADD_B_D
	ADD_B_88

rc_blend_g6_line:
	BLEND_G_HL_ADD_B_D
	ADD_B_88

rc_blend_g5_line:
	BLEND_G_HL_ADD_B_D
	ADD_B_88

rc_blend_g4_line:
	BLEND_G_HL_ADD_B_D
	ADD_B_88

rc_blend_g3_line:
	BLEND_G_HL_ADD_B_D
	ADD_B_88

rc_blend_g2_line:
	BLEND_G_HL_ADD_B_D
	ADD_B_88

rc_blend_g1_line:
	BLEND_G_HL_ADD_B_D

	ret


;----
;	END
