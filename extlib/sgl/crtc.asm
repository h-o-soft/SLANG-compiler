;---------------------------------------------------------------;
;	Copyright (c) 2019 crtc.asm
;	This software is released under the MIT License.
;	http://opensource.org/licenses/mit-license.php
;---------------------------------------------------------------;

;//---------------------------------------------------------------; 
;//	CRTC設定
;//		in: HLreg:	CRTCデータ
;//---------------------------------------------------------------; 
set_crtc80:
	ld	hl, crtc80_H
	jr		set_crtc

set_crtc40:
	ld	hl, crtc40_L
set_crtc:
	ld	de,000eh
	ld	bc,01800h
sc_1:
	out	(c),d
	inc	bc

	ld	a,(hl)
	inc	hl

	out	(c),a
	dec	bc

	inc	d
	dec	e
	jr	nz,sc_1
;
	ld	a,(hl)			; 40/80桁の切り替え
	inc hl
	ld	bc,01a03h
	out	(c),a

; IF 0
; 	; 画面管理ポート: 低解像度/25ライン
; 	ld a,(hl)
; 	ld	bc,01fd0h
; 	out	(c),a
; ENDIF

	ret

;//---------------------------------------------------------------; 
;//		CRTC設定データ 40桁/80桁
;//---------------------------------------------------------------; 
crtc40_L:
	db	37h,28h,2dh,34h,1fh,02h,19h,1ch,00h,07h,00h,00h,00h,00h,0dh
	; 01fd0 - 互換モード
	db	CRTC_1FD0_L

crtc40_H:
	db	35h,28h,2dh,84h,1bh,00h,19h,1ah,00h,0fh,00h,00h,00h,00h,0dh
	; 01fd0 - PCG高速アクセスモード On
	db	CRTC_1FD0

crtc80_L:
	db	6bh,50h,59h,38h,1fh,02h,19h,1ch,00h,07h,00h,00h,00h,00h,0ch
	db	00h

crtc80_H:
	db	6bh,50h,59h,88h,1bh,00h,19h,1ah,00h,0fh,00h,00h,00h,00h,0ch
	db	03h

;//---------------------------------------------------------------; 
;//	切替初期化
;//---------------------------------------------------------------; 
init_screen:
	; 削除キャラバッファを初期化
	; 初期は Page1用
	ld	hl, clear_char_work1
	ld	( del_char_write_w+1 ),hl

	ret

;//---------------------------------------------------------------; 
;//---------------------------------------------------------------; 
init_flip:
	xor		a
	ld		( flip_w ),a
	ld		( flip_delchr_w ),a
	ld		( vsync_state), a
	ld		( vsync_w ),a

	ld		a, 04h
	ld		( flip_render_w ),a

	ret


;//---------------------------------------------------------------; 
;//	Screen0,Screen1を切り替える。
;//---------------------------------------------------------------; 
flip_screen:
	ld	bc, 01800h
	ld	a, 0ch		; CRTC 12Reg.
	out	(c),a

	; flip_w(表示ページ)を xor 04h で反転する。
	; flip_render_w は、その反転なので、単にコピーするだけでオケー。

	ld	a,(flip_w)	; 0
	ld	(flip_render_w),a	; 0
	xor	FLIP_ADRS
	ld	(flip_w),a	; 4

	inc	c
	out	(c),a

setup_clear_char_work:
	; flip_w = 0 時、消去キャラワーク clear_char_work1
	; flip_w = 4 時、消去キャラワーク clear_char_work0
	ld	hl, clear_char_work1
	ld	de, clear_char_work0
	or	a
	jp	z, fsc_1

	ex	de,hl
fsc_1:
	ld	( del_char_write_w+1 ),hl
	ld	( del_char_read_w+1 ),hl

	ret


align 8
del_char_num_w:
	db	000h	; Page0の削除キャラバッファ数

;
; 高速化のため削除キャラバッファ数を配置を4byte alignにしている。
; スキマがもったいないのでそこに flip_w関係のワークを埋めておく。

flip_w:
	; FlipWork
	; 表示ページを格納する。
	; 000hの時 描画ページ1,表示ページ0
	; 004hの時 描画ページ0,表示ページ1
	db	000h

flip_render_w:
	; 描画ページを格納する。flip_w とは反対の状態。
	; 000hの時 描画ページ0
	; 004hの時 描画ページ1
	db	004h

flip_delchr_w:
	; 削除キャラバッファページを格納する。
	; 000h の時 削除キャラバッファ書込み 0,削除キャラバッファ読出し 1
	; 001h の時 削除キャラバッファ書込み 1,削除キャラバッファ読出し 0
	db	000h

del_char_num_w_page1:
	db	000h	; Page1の削除キャラバッファ数

	; フレームごとのカウンタ
frame_cnt:
	ds	1

align 2
vsync_w:
	db	000h

vsync_state:
	db	000h

; FPS
; 00: 60fps 02: 30fps 04: 20fps
fps_mode:
	db	000h

frame_dropout:
	db	000h


;//---------------------------------------------------------------;
;//---------------------------------------------------------------;
wait_vsync_fps:
	ld		a,(fps_mode)
	sra		a
	jr		z, wait_vsync60_state	; 60fps Vsync待ち
;
	dec		a
	jr		nz, wait_vsync20_state	; 20fps Vsync待ち
;
	jp		wait_vsync30_state		; 30fps Vsync待ち

;//---------------------------------------------------------------;
;//---------------------------------------------------------------;
; VSync(60fps)待ち
wait_vsync60_state:
	ld		a, (vsync_state )
	cp		02h
	jr		c, wvs60_3

;
	ld		a, 60
	ld		(frame_dropout),a
wvs60_3:

	; State0: Vsync開始待ち
	ld		bc, 1a01h
wvs60_1:
	in		a,(c)
	jp		p,wvs60_1
;
	; State1: Vsync開始
wvs60_2:
	in		a,(c)
	jp		m,wvs60_2
	
	push	af
	call	!VSYNC_PROC
	pop	af

	and		080h
	ld		(vsync_w),a

	xor		a
	ld		(vsync_state),a

	ret

; VSync(30fps)待ち
wait_vsync30_state:
	ld		a, (vsync_state )
	cp		04h
	jr		c, wvs_5
;
	ld		a, 30
	ld		(frame_dropout),a
wvs_5:

	ld		bc, 1a01h

	ld		a, (vsync_state)
	or		a
	jp		z, wvs_1
;
	dec		a
	jp		z, wvs_2
;
	dec		a
	jp		z, wvs_3
;
	jp		wvs_4

	; State0: Vsync開始待ち
wvs_1:
	in	a,(c)
	jp	p,wvs_1

;
	; State1: Vsync終了待ち
wvs_2:
	in	a,(c)
	jp	m, wvs_2

	push bc
	call	!VSYNC_PROC
	pop bc
;
	; State2: Vsync開始待ち
wvs_3:
	in	a,(c)
	jp	p, wvs_3

wvs_4:
	in	a,(c)
	jp	m, wvs_4

	push af
	call	!VSYNC_PROC
	pop af

	; 開始したらState0に戻す。

	and		080h
	ld		(vsync_w),a

	xor		a
	ld		(vsync_state),a

	ret


; VSync(20fps)待ち
wait_vsync20_state:
	ld		a, (vsync_state )
	cp		06h
	jr		c, wvs20_7
;
	ld		a, 20
	ld		(frame_dropout),a
wvs20_7:

	ld	bc, 1a01h

	ld	a, (vsync_state)
	or	a
	jp	z, wvs20_1

	dec	a
	jp	z, wvs20_2

	dec	a
	jp	z, wvs20_3

	dec	a
	jp	z, wvs20_4

	dec	a
	jp	z, wvs20_5

	jp	wvs20_6

	; State0: Vsync開始待ち (1フレーム目)
wvs20_1:
	in	a,(c)
	jp	p,wvs20_1

;
	; State1: Vsync終了待ち
wvs20_2:
	in	a,(c)
	jp	m, wvs20_2

	push 	bc
	call	!VSYNC_PROC
	pop	bc
;
	; State2: Vsync開始待ち (2フレーム目)
wvs20_3:
	in	a,(c)
	jp	p, wvs20_3

	; State3: Vsync終了待ち
wvs20_4:
	in	a,(c)
	jp	m, wvs20_4

	push 	bc
	call	!VSYNC_PROC
	pop	bc
	; State4: Vsync開始待ち (3フレーム目)
wvs20_5:
	in	a,(c)
	jp	p, wvs20_5

	; State5: Vsync終了待ち
wvs20_6:
	in	a,(c)
	jp	m, wvs20_6

	push af
	call	!VSYNC_PROC
	pop af

	; 開始したらState0に戻す。
	and		080h
	ld		(vsync_w),a

	xor		a
	ld		(vsync_state),a

	ret

; VsyncをチェックしてStateを変更する。
; VBlankのエッジ毎に Stateを+1する。
check_vsync_state:
	ld	hl, vsync_w
	ld	bc, 1a01h
	in	a,(c)
	and	080h
	cp	(hl)
	ret	z		; 前回のVSync状態と比較。
;
	ld	(hl),a
	inc	l

	; 異なっていれば vsync_stateを+1する。
	; 0フレーム目(Vsync前) → 0
	; 0フレーム目(Vsync中) → 1
	; 1フレーム目(Vsync前) → 2
	; 1フレーム目(Vsync中) → 3
	; 2フレーム目(Vsync前) → 4
	; 2フレーム目(Vsync中) → 5

	inc	(hl)	; Stateを+1する。

	CP	080h
	ret	z
	jp !VSYNC_PROC


;//---------------------------------------------------------------; 
;//	VSync(垂直帰線期間)のエッジを待つ。
;//---------------------------------------------------------------; 
wait_vsync:
	ld hl,0000h
	ld de,0000h

	ld bc, 1a01h
edge_1:
	inc hl

	in a,(c)
	jp p,edge_1

edge_2:
	inc de

	in a,(c)
	jp m,edge_2

	ret

;//---------------------------------------------------------------; 
;//	VSyncの開始を待つ。
;//---------------------------------------------------------------; 
wait_vsync0:
	ld bc, 1a01h
ill_1:
	in a,(c)
	jp m,ill_1

	ret


; タイマー。200h回ループで 約 3.499msec。
wait_time:
	ld	hl, 0200h
wt_1:
	dec	hl
	ld	a,h
	or	l
	jr	nz, wt_1

	ret


;---------------------------------------------------------------; 
; アクセス(R/W)VRAMバンクを VRAM1に設定
;---------------------------------------------------------------; 
set_vram1:
	ld		a, CRTC_1FD0_L | 0x10
	ld		bc, 01fd0h
	out		(c),a
	ret

;---------------------------------------------------------------; 
; アクセス(R/W)VRAMバンクを VRAM0に設定
;---------------------------------------------------------------; 
set_vram0:
	ld		a, CRTC_1FD0_L
	ld		bc, 01fd0h
	out		(c),a
	ret


;---------------------------------------------------------------; 
;	END

