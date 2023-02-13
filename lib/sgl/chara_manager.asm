;---------------------------------------------------------------;
;	Copyright (c) 2019 chara_manager.asm
;	This software is released under the MIT License.
;	http://opensource.org/licenses/mit-license.php
;---------------------------------------------------------------; 
;---------------------------------------------------------------;
; キャラクタワークの初期化
;---------------------------------------------------------------;
init_chara_manager:
	ld hl, chara_work
	ld bc, CHR_SIZE * CHARA_NUM
	call clear_mem

	ret

;---------------------------------------------------------------;
; キャラワークを確保
; 戻り値:
;	Zflag: ワークが確保できた
;	 IXreg: 確保できたワークアドレス
;	NonZflag: ワークの確保に失敗
; 保持: HLregは壊さない。
;---------------------------------------------------------------;
find_chara_work:
	ld	c, CHARA_NUM

	ld	ix, chara_work
	ld	de, CHR_SIZE
few_1:
	ld	a, (ix+CHR_KIND)
	or	a
	ret	z

	add	ix,de
	dec	c
	jp	nz, few_1

	; Zflagを下げる。
	dec	c
	ret

; ;---------------------------------------------------------------;
; ;---------------------------------------------------------------;
; create_ball_b:
; 	call	find_chara_work_iy
; 	ret		nz
; ;
; 	call	init_ball_b
; 
; 	xor		a
; 	ret
; 
; ;---------------------------------------------------------------;
; ;---------------------------------------------------------------;
; create_ball_br:
; 	call	find_chara_work_iy
; 	ret		nz
; ;
; 	call	init_ball_br
; 
; 	xor		a
; 	ret
; 
; ;---------------------------------------------------------------;
; ;---------------------------------------------------------------;
; create_ball_brg:
; 	call	find_chara_work_iy
; 	ret		nz
; ;
; 	call	init_ball_brg
; 
; 	xor		a
; 	ret

;---------------------------------------------------------------;
; 敵キャラワークを確保 (IYreg)
; 戻り値:
;	Zflag: ワークが確保できた
;	 IYreg: 確保できたワークアドレス
;	NonZflag: ワークの確保に失敗
;	注意: HLregを破壊しない事。
;---------------------------------------------------------------;
find_chara_work_iy:
	ld	iy, chara_work
	ld	c, CHARA_NUM
	ld	de, CHR_SIZE
fewi_1:
	ld	a, (iy+CHR_KIND)
	or	a
	ret	z

	add	iy,de
	dec	c
	jp	nz, fewi_1

	; Zflagを下げる。
	dec	c
	ret

remove_chara:
	ld		iy, chara_work + (CHARA_NUM-1) * CHR_SIZE
	ld		c, CHARA_NUM
	ld		de, -CHR_SIZE
rech_2:
	ld		a,(iy+CHR_KIND)
	or		a
	jr		nz, rech_1
;
	add		iy,de

	dec		c
	jp		nz, rech_2
;
	ret

rech_1:
	ld		(iy+CHR_KIND),00h
	ld		(iy+CHR_PATTERN),00h

	ret

;---------------------------------------------------------------;
; 敵キャラワークを確保 (IXreg)
; 戻り値:
;	Zflag: ワークが確保できた
;	 IXreg: 確保できたワークアドレス
;	NonZflag: ワークの確保に失敗
;	注意: HLregを破壊しない事。
;---------------------------------------------------------------;
find_chara_work_ix:
	ld	ix, chara_work
	ld	c, CHARA_NUM
	ld	de, CHR_SIZE
fcwi_1:
	ld	a, (ix+CHR_KIND)
	or	a
	ret	z

	add	ix,de
	dec	c
	jp	nz, fcwi_1

	; Zflagを下げる。
	dec	c
	ret


;---------------------------------------------------------------;
;	キャラクタ初期化処理
;	Areg: キャラクタKind
;	IYreg: キャラクタワーク
kind_init_jump:
	ld	(iy+CHR_KIND),a

	; 自己書換えによるテーブルジャンプ
	ld	(kij_1+1),a	; 13
kij_1:
	jr	kij_1		; 12

	jp	jump_none			; 0
;	jp	init_ball_b			; 1*3
;	jp	init_ball_br		; 2*3
;	jp	init_ball_brg		; 3*3

;---------------------------------------------------------------;
;	キャラクタ更新処理
kind_jump:
	; 自己書換えによるテーブルジャンプ
	ld	(kj_1+1),a	; 13
kj_1:
	jr	kj_1		; 12

	jp	jump_none			; 0
;	jp	ball_b				; 1*3
;	jp	ball_br				; 2*3
;	jp	ball_brg			; 3*3


;---------------------------------------------------------------; 
;	各キャラクタの更新
;---------------------------------------------------------------; 
update_chara_manager:
	; ボールやFPS制御
	call	update_function

	; キャラクタを更新
	ld		iy, chara_work
	ld		b,CHARA_NUM
cmu_1:
	push	bc

	ld		a,(iy+CHR_KIND)
	or		a
	call	nz, kind_jump

	ld		de, CHR_SIZE
	add		iy,de

	pop		bc

	djnz	cmu_1

jump_none:
	ret

;---------------------------------------------------------------;
;	ボールやFPSの制御
;---------------------------------------------------------------;
update_function:
; 	ld		a, (trg_w)
; 	BIT_A_0_KEY_UP
; 	jp		z, dup_1
; ;
; 	; ボール(B)を一つ追加。
; 	call	create_ball_b
; 	ret		nz
; ;
; 	call	inc_chara_num
; 	call	render_chara_num
; 
; 	ret
; 
; dup_1:
; 	BIT_A_1_KEY_DOWN
; 	jp		z, dup_2
; ;
; 	; ボール(BR)を一つ追加。
; 	call	create_ball_br
; 	ret		nz
; ;
; 	call	inc_chara_num
; 	call	render_chara_num
; 
; 	ret
; 
; dup_2:
; 	BIT_A_2_KEY_LEFT
; 	jp		z, dup_3
; ;
; 	; ボール(BRG)を一つ追加。
; 	call	create_ball_brg
; 	ret		nz
; ;
; 	call	inc_chara_num
; 	call	render_chara_num
; 
; 	ret
; 
; dup_3:
; 	BIT_A_3_KEY_RIGHT
; 	jp		z, dup_4
; ;
; 	; ボールを削除
; 	call	remove_chara
; 	jp		z, dup_4
; ;
; 	call	dec_chara_num
; 	call	render_chara_num
; 
; 	ret
; 
; dup_4:
; 	BIT_A_5_KEY_TRG1
; 	jp		z, dup_5
; ;
; 	ld		a,(fps_mode)
; 	add		a,02h
; 	cp		06h
; 	jr		c, dup_6
; ;
; 	xor		a
; dup_6:
; 	ld		(fps_mode),a
; 
; 	call	render_fps_mode
; 	ret
; 
; dup_5:
	ret

;;----------------------
;	END

