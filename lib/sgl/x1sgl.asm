

#LIB SGLBASE
	; SGLBASE

SGL_VRCALC:
    ; HL = X, DE = Y
    LD H,E

    PUSH DE
    LD C,L
    LD B,8
    LD E,H
    LD D,0
    LD H,40	; WIDTH 40専用
    LD L,D
    .LOC2
    ADD HL,HL
    JR NC,.LOC3
    ADD HL,DE
    .LOC3
    DJNZ .LOC2
    ADD HL,BC
    POP DE

    ; to text vram address
    LD C,L
    LD B,H
    LD	A,B
    OR	038H
    LD	B,A

    RET
#ENDLIB

#LIB SGL_INIT

	call	fill_text_vram
	call	fill_attr_vram

	call	set_crtc40

	call	vram_priority
	call	vram_palette_init

	call	clear_graphic_vram_b
	call	clear_graphic_vram_r
	call	clear_graphic_vram_g

	; VRAMアドレステーブルを作成。
	call	create_vram_adrs_tbl

	call	init_screen

	call	init_bitline

	call	init_chara_manager

	; キャラクタ消去ワーク初期化
	call	init_clear_char_work

;	call	init_input

;	call	init_test_title
;	call	render_chara_num
;	call	render_fps_mode

	ret



#ENDLIB

#LIB SGL_DEFPAT
	; hl = pat num , de = address
	ex de,hl
	sla e
	ld c,e
	jp cdm_set_data8_bank_main
#ENDLIB

#LIB SGL_SPRCREATE
	; hl = pattern num, de = kind
	push iy
	push de
	; 空きワークを探す
 	call	find_chara_work_iy
	jp	nz,sgl_error

	pop de

	; パターン番号は2倍しないと駄目
	sla l

	; ザッと初期化
	ld	(iy+CHR_KIND), e
	ld	(iy+CHR_PATTERN), l

	ld	(iy+CHR_POSXL),0
	ld	(iy+CHR_POSYL),0

	ld	(iy+CHR_POSXH),0
	ld	(iy+CHR_POSYH),0

	ld	(iy+CHR_WORK0),0
	ld	(iy+CHR_WORK1),0
	ld	(iy+CHR_WORK2),0

	; iyのアドレスがスプライトハンドルになる
	push iy
	pop hl

	pop iy
	ret

sgl_error:
	pop de
	pop iy

	; 0だとエラー
	ld hl,0
	ret
#ENDLIB

#LIB SGL_SPRDESTROY
	; hl = sprite handle
	; KIND & PATTERNを0にする
	xor a
	ld	(hl), a
	inc	hl
	ld	(hl), a
	ret
#ENDLIB

#LIB SGL_SPRSET
	; HL = sprite handle, DE = data address
	ex de,hl
	ld bc,CHR_SIZE
	ldir
	ret
#ENDLIB

#LIB SGL_SPRPAT
	; HL = sprite handle, DE = pattern number
	; HLに入っているワークのCHR_PATTERNを書き換える
	sla e
	inc hl
	ld (hl),e
	ret
#ENDLIB

#LIB SGL_SPRMOVE
	; HLに入っているワークのX,Yを書き換える
	INC HL
	INC HL
	INC HL
	LD (HL),E
	INC HL
	LD (HL),D
	INC HL
	INC HL
	LD (HL),C
	INC HL
	LD (HL),B
	RET
#ENDLIB

#LIB SGL_SPRDISP
    ; 表示/非表示の設定
    ; HL = sprite handle, DE = 0 = nodisp 1 = disp
    inc hl
    ld a,(hl)
    and $fe
    or e
    ; 表示を0、非表示を1にするため、反転させる
    xor 1
    ld (hl),a
    ret
#ENDLIB

#LIB SGL_FPSMODE
	ld a,l
	ld (fps_mode),a
	ret
#ENDLIB

#LIB SGL_VSYNC
	; キャラクタ処理
	; call	update_chara_manager
	push iy

	; キャラクタ描画
	call	draw_chara_manager

	call	wait_vsync_fps

	; call	disp_frame_dropout

	call	flip_screen

	pop iy
	ret
#ENDLIB

#LIB SGL_PRINT
	; HL = x, DE = y, BC = STRING ADDRESS
	PUSH BC
	CALL SGL_VRCALC

	; 描画ページに描画する
	LD A,(flip_render_w)
	OR B
	LD B,A

	POP HL
	; HL = string address , BC = vram address
	jp render_text
#ENDLIB



#LIB SGL_PRINT2
	; HL = x, DE = y, BC = STRING ADDRESS
	PUSH BC
	CALL SGL_VRCALC
	POP HL
	; HL = string address , BC = vram address
	jp render_text_2page
#ENDLIB

