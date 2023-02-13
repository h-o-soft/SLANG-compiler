;---------------------------------------------------------------;
;	Copyright (c) 2019 render_r.asm
;	This software is released under the MIT License.
;	http://opensource.org/licenses/mit-license.php
;---------------------------------------------------------------; 

;---------------------------------------------------------------;
; プレーン R 1ライン描画
; 引数
;	HLreg: キャラデータ
;	BCreg: 描画VRAMアドレス
;	Ereg: ビットラインデータ
;---------------------------------------------------------------;
rc_image_r1:
	ld		d,b		; DregにBregをバッファ。

	ld		a,b
	or		BITLINE_MASK
	ld		b,a

	ld		a,(bc)
	and		e
	jp		nz, rc_blend_r1

	ld		a,(bc)
	or		e
	ld		(bc),a

	ld		a, 040h+1
	add		a,d
	ld		b,a

	OUT_B_HL
	ADD_B_40	; 整合性のため R→G

	ret

rc_blend_r1:
	ld		a,(bc)
	or		e
	ld		(bc),a

	ld		b,d		; Bregに復帰。
	ld		d, 040h

	jp		rc_blend_r1_line


;---------------------------------------------------------------;
; プレーン R 2ライン描画
; 引数
;	HLreg: キャラデータ
;	BCreg: 描画VRAMアドレス
;	Ereg: ビットラインデータ
;---------------------------------------------------------------;
rc_image_r2:
	ld		d,b		; DregにBregをバッファ。

	ld		a,b
	or		BITLINE_MASK
	ld		b,a

	ld		a,(bc)
	and		e
	jp		nz, rc_blend_r2
;
	ld		a,(bc)
	or		e
	ld		(bc),a

	; B→Rプレーンにする。更に OUTI用に+1して Aregにも残す。
	ld		a,d
	ld		de, 04008h		; Dreg: プレーン増加用, Ereg: ライン増加用
	inc		a
	add		a,d
	ld		b,a

	jp		rc_write_r2_line

rc_blend_r2:
	ld		a,(bc)
	or		e
	ld		(bc),a

	ld		b,d		; Bregに復帰。
	ld		d, 40h

	jp		rc_blend_r2_line


;---------------------------------------------------------------;
; プレーン R 3ライン描画
; 引数
;	HLreg: キャラデータ
;	BCreg: 描画VRAMアドレス
;	Ereg: ビットラインデータ
;---------------------------------------------------------------;
rc_image_r3:
	ld		d,b		; DregにBregをバッファ。

	ld		a,b
	or		BITLINE_MASK
	ld		b,a

	ld		a,(bc)
	and		e
	jp		nz, rc_blend_r3

	ld		a,(bc)
	or		e
	ld		(bc),a

	; B→Rプレーンにする。更に OUTI用に+1して Aregにも残す。
	ld		a,d
	ld		de, 04008h		; Dreg: プレーン増加用, Ereg: ライン増加用
	inc		a
	add		a,d
	ld		b,a

	jp		rc_write_r3_line

rc_blend_r3:
	ld		a,(bc)
	or		e
	ld		(bc),a

	ld		b,d		; Bregに復帰。
	ld		d, 40h

	jp		rc_blend_r3_line

;---------------------------------------------------------------;
; プレーン R 4ライン描画
; 引数
;	HLreg: キャラデータ
;	BCreg: 描画VRAMアドレス
;	Ereg: ビットラインデータ
;---------------------------------------------------------------;
rc_image_r4:
	ld		d,b		; DregにBregをバッファ。

	ld		a,b
	or		BITLINE_MASK
	ld		b,a

	ld		a,(bc)
	and		e
	jp		nz, rc_blend_r4

	ld		a,(bc)
	or		e
	ld		(bc),a

	; B→Rプレーンにする。更に OUTI用に+1して Aregにも残す。
	ld		a,d
	ld		de, 04008h		; Dreg: プレーン増加用, Ereg: ライン増加用
	inc		a
	add		a,d
	ld		b,a

	jp		rc_write_r4_line

rc_blend_r4:
	ld		a,(bc)
	or		e
	ld		(bc),a

	ld		b,d		; Bregに復帰。
	ld		d, 40h

	jp		rc_blend_r4_line

;---------------------------------------------------------------;
; プレーン R 5ライン描画
; 引数
;	HLreg: キャラデータ
;	BCreg: 描画VRAMアドレス
;	Ereg: ビットラインデータ
;---------------------------------------------------------------;
rc_image_r5:
	ld		d,b		; DregにBregをバッファ。

	ld		a,b
	or		BITLINE_MASK
	ld		b,a

	ld		a,(bc)
	and		e
	jp		nz, rc_blend_r5

	ld		a,(bc)
	or		e
	ld		(bc),a

	; B→Rプレーンにする。更に OUTI用に+1して Aregにも残す。
	ld		a,d
	ld		de, 04008h		; Dreg: プレーン増加用, Ereg: ライン増加用
	inc		a
	add		a,d
	ld		b,a

	jp		rc_write_r5_line

rc_blend_r5:
	ld		a,(bc)
	or		e
	ld		(bc),a

	ld		b,d		; Bregに復帰。
	ld		d, 40h

	jp		rc_blend_r5_line

;---------------------------------------------------------------;
; プレーン R 6ライン描画
; 引数
;	HLreg: キャラデータ
;	BCreg: 描画VRAMアドレス
;	Ereg: ビットラインデータ
;---------------------------------------------------------------;
rc_image_r6:
	ld		d,b		; DregにBregをバッファ。

	ld		a,b
	or		BITLINE_MASK
	ld		b,a

	ld		a,(bc)
	and		e
	jp		nz, rc_blend_r6

	ld		a,(bc)
	or		e
	ld		(bc),a

	; B→Rプレーンにする。更に OUTI用に+1して Aregにも残す。
	ld		a,d
	ld		de, 04008h		; Dreg: プレーン増加用, Ereg: ライン増加用
	inc		a
	add		a,d
	ld		b,a

	jp		rc_write_r6_line

rc_blend_r6:
	ld		a,(bc)
	or		e
	ld		(bc),a

	ld		b,d		; Bregに復帰。
	ld		d, 40h

	jp		rc_blend_r6_line

;---------------------------------------------------------------;
; プレーン R 7ライン描画
; 引数
;	HLreg: キャラデータ
;	BCreg: 描画VRAMアドレス
;	Ereg: ビットラインデータ
;---------------------------------------------------------------;
rc_image_r7:
	ld		d,b		; DregにBregをバッファ。

	ld		a,b
	or		BITLINE_MASK
	ld		b,a

	ld		a,(bc)
	and		e
	jp		nz, rc_blend_r7

	ld		a,(bc)
	or		e
	ld		(bc),a

	; B→Rプレーンにする。更に OUTI用に+1して Aregにも残す。
	ld		a,d
	ld		de, 04008h		; Dreg: プレーン増加用, Ereg: ライン増加用
	inc		a
	add		a,d
	ld		b,a

	jp		rc_write_r7_line

rc_blend_r7:
	ld		a,(bc)
	or		e
	ld		(bc),a

	ld		b,d		; Bregに復帰。
	ld		d, 40h

	jp		rc_blend_r7_line

;---------------------------------------------------------------;
; プレーン: R 8ライン描画
; 引数
;	HLreg: キャラデータ
;	BCreg: 描画VRAMアドレス
;---------------------------------------------------------------;
rc_image_r8:
	; VRAM Adrs(BCreg)からBitLineBuffを求める。
	; BitLineBuffは 0f8xxにあるので、f800を ORすると求まる。
	; Ereg: ビットラインデータ

	ld		d,b		; DregにBregをバッファ。

	ld		a,b
	or		BITLINE_MASK
	ld		b,a

	ld		a,(bc)
	or		a
	jp		nz, rc_blend_r8

	ld		a,0ffh
	ld		(bc),a

	; Dregを復帰して、Blue→Redプレーンへ。
	; その際に OUTI用に+1してAregにも残す。
	ld		a,d
	ld		de, 04008h	; Dreg: プレーン増加用 Ereg: ライン増加用(08h)
	inc		a
	add		a,d
	ld		b,a

	OUT_R_HL_ADD_E
rc_write_r7_line:
	OUT_R_HL_ADD_E
rc_write_r6_line:
	OUT_R_HL_ADD_E
rc_write_r5_line:
	OUT_R_HL_ADD_E
rc_write_r4_line:
	OUT_R_HL_ADD_E
rc_write_r3_line:
	OUT_R_HL_ADD_E
rc_write_r2_line:
	OUT_R_HL_ADD_E
	OUT_R_HL

	; 整合性のため R→G
	ld		a,b
	add		a,d
	ld		b,a

	ret

rc_blend_r8:
	ld		a,0ffh
	ld		(bc),a

	ld		b,d		; Bregに復帰。
	ld		d, 40h

	; 3プレーンとブレンドするのでBプレーンでOK.

	BLEND_R_HL_ADD_B_D
	ADD_B_88

rc_blend_r7_line:
	BLEND_R_HL_ADD_B_D
	ADD_B_88

rc_blend_r6_line:
	BLEND_R_HL_ADD_B_D
	ADD_B_88

rc_blend_r5_line:
	BLEND_R_HL_ADD_B_D
	ADD_B_88

rc_blend_r4_line:
	BLEND_R_HL_ADD_B_D
	ADD_B_88

rc_blend_r3_line:
	BLEND_R_HL_ADD_B_D
	ADD_B_88

rc_blend_r2_line:
	BLEND_R_HL_ADD_B_D
	ADD_B_88

rc_blend_r1_line:
	BLEND_R_HL_ADD_B_D

	ret


;----
;	END
