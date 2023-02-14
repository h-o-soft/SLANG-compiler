;---------------------------------------------------------------;
;	Copyright (c) 2019 chara_data_manager.asm
;	This software is released under the MIT License.
;	http://opensource.org/licenses/mit-license.php
;---------------------------------------------------------------; 
;----

; キャラクタパターン (2,4…)のデータアドレステーブル (0は使用しない)
; パターン種類は128種類
; データオフセット(Xoffset)は0～7 の8種類


; X方向 Offset 0
;　2,3　キャラクタパターン1　データアドレス(L,H) … 127パターン分
; X方向 Offset 1～7
;　2,3　キャラクタパターン1　データアドレス(L,H) … 127パターン分

; PCGデータ領域(6KB)は転送後は使えるので共用する。
; 高速化のために256byte アライメントにある。
;;chara_data_table	equ		pcg_data

align 256

chara_data_table:
	ds X_OFS_NUM*256

; 各パターンごとのPivotテーブル (並びは高速化のため PivotX, PivotYの順)
; X方向(整数部として。つまり2の倍数): -80～+7f Y方向: -80～+7f
; 高速化のために256byte アライメントにある。
chara_pivot_table:
	ds	256

; 各パターンごとの格納メモリバンクテーブル
; 2byteごとに使用されているが第0byteのみ使用している。
; 高速化のために256byte アライメントにある。
chara_bank_table:
	ds	256

init_chara_data_manager:
	ld hl, chara_data_table
	ld bc, X_OFS_NUM*256
	call clear_mem

	ld hl, chara_pivot_table
	ld bc, 256
	call clear_mem

	ld	hl, chara_bank_table
	ld	bc, 256
	ld	a, BANK_MAIN
	call	fill_mem


	; SGL TODO
	; このへんは汎用化する

;	; キャラパターン Ball(B) 00
;	ld	c, PAT_BALL_B00
;	ld	hl, ball_p0_c1
;	call cdm_set_data8_bank_main
;
;	; キャラパターン Ball(B) 01
;	ld	c, PAT_BALL_B01
;	ld	hl, ball_p1_c1
;	call cdm_set_data8_bank_main
;
;	; キャラパターン Ball(BR) 00
;	ld	c, PAT_BALL2_BR00
;	ld	hl, ball2_p0_c2
;	call cdm_set_data8_bank_main
;
;	; キャラパターン Ball(BR) 01
;	ld	c, PAT_BALL2_BR01
;	ld	hl, ball2_p1_c2
;	call cdm_set_data8_bank_main
;
;	; キャラパターン Ball(BRG) 00
;	ld	c, PAT_BALL3_BRG00
;	ld	hl, ball3_p0_c3
;	call cdm_set_data8_bank_main
;
;	; キャラパターン Ball(BRG) 01
;	ld	c, PAT_BALL3_BRG01
;	ld	hl, ball3_p1_c3
;	call cdm_set_data8_bank_main

	ret


; PatternとXOffsetに対応したキャラクタデータを返す。
;	Lreg: Pattern番号 (2,4…254)
;	Areg: Xpos Offset(0-7)
; 戻り値: DEreg: data adrs.
;
; 破壊しないReg: BCreg
cdm_get_data:
;( pat*8 + (x&7) )*2
	and 07h
	ld h,a

	ld de,chara_data_table
	add hl,de

	ld e,(hl)
	inc hl
	ld d,(hl)

	ret


; キャラデータテーブルを与えて、各パターンのキャラデータを設定する。
; 合わせてキャラクタのPivotテーブルも設定する。
; Pattern , XOffset
;	Creg: Pattern(2,4…254)
;	HLreg: キャラデータテーブル

; Bank Main用
cdm_set_data8_bank_main:
	ld	de, 0000h
csdbm_1:
	ld	( cdm_adrs + 1 ), de

cdm_set_data8:
	; 格納バンクメモリ設定
	ld	e,c
	ld	d, chara_bank_table >> 8
	ld	(de),a

	di

	ld	a,e	; キャラパターン番号をAregに保持

	ld	d, chara_pivot_table >> 8

	; ldi命令で BCregはデクリメントされる。
	ldi		; PivotX (DE++)←(HL++)
	ldi		; PivotY (DE++)←(HL++)

	ld	b, (hl)	; データ数
	inc	hl

	ld	c,a	; 再び Cregにキャラパターン値を復帰。

	xor	a
csd8_1:
	ld	e,(hl)
	inc	hl
	ld	d,(hl)
	inc	hl

	push	hl

	; 自己書換え
cdm_adrs:
	ld	hl, 0000h

	add	hl,de
	ex	de,hl

	; X Offset位置に対応したキャラデータを設定する。
	;	Creg: Pattern(2,4…254)
	;	Areg: X Offset(0-7)
	;	DEreg: キャラデータ

	ld	l,c
	and 07h
	ld h,a

	push de
	ld de,chara_data_table
	add hl,de
	pop de

	ld (hl),e
	inc hl
	ld (hl),d

	pop hl

	inc	a

	djnz	csd8_1

	ei

	ret


; キャラクタデータテーブルを返す。
;	Lreg: Pattern(2,4…254)
;	Areg: X Offset(0-7)
;
; 戻り値: HLreg - テーブルアドレスIndex.
cdm_calc_pattern_adrs:
	and 07h
	ld h,a
	ld de,chara_data_table
	add hl,de

	ret


;---------------------------------------------------------------; 

;	END

