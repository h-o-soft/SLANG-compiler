;---------------------------------------------------------------;
;	Copyright (c) 2019 macro_define.asm
;	This software is released under the MIT License.
;	http://opensource.org/licenses/mit-license.php
;---------------------------------------------------------------; 

;---------------------------------------------------------------; 
;	マクロ
;---------------------------------------------------------------; 
OUT_L_ADD_H	MACRO
	out	(c),l
	add	a,h
	ld	b,a
ENDM

OUT_B_HL_ADD_E MACRO
	; Areg: 出力予定の Breg+1の値が入っている。
	; Ereg: １ライン下用 08h
	inc hl		; MASK部分をスキップ

	outi
	add a,e
	ld	b,a
ENDM

OUT_R_HL_ADD_E MACRO
	; Areg: 出力予定の Breg+1の値が入っている。
	; Ereg: １ライン下用 08h
	inc hl		; MASK部分をスキップ

	outi

	add a,e
	ld	b,a
ENDM

OUT_G_HL_ADD_E MACRO
	; Areg: 出力予定の Breg+1の値が入っている。
	; Ereg: １ライン下用 08h
	inc hl		; MASK部分をスキップ

	outi

	add a,e
	ld	b,a
ENDM

OUT_B_HL MACRO
	; BRG - 0
	inc hl		; MASK部分をスキップ

	outi
ENDM

OUT_R_HL MACRO
	; BRG - 0
	inc hl		; MASK部分をスキップ

	outi
ENDM

OUT_G_HL MACRO
	; BRG - 0
	inc hl		; MASK部分をスキップ

	outi
ENDM

; RGプレーン出力用
OUT_RG_HL_ADD_D_E	MACRO
	inc hl		; MASK部分をスキップ

	outi		; Redプレーン出力

	add a,d
	ld b,a
;
	outi		; Greenプレーン出力

	add a,e		; その後 Redプレーンに戻す。
	ld b,a
;
ENDM

; RGプレーン出力用
; Areg: VRAM(H)+1, Dreg: 040h
OUT_RG_HL_ADD_D	MACRO
	inc hl		; MASK部分をスキップ

	outi		; Redプレーン出力
	add a,d
	ld b,a
;
	outi		; Greenプレーン出力
;
ENDM


; BGプレーン出力用
; Areg: VRAM(H)+1, Dreg: 080h
OUT_BG_HL_ADD_D	MACRO
	inc hl		; MASK部分をスキップ

	outi		; Blueプレーン出力

	add	a,d		; Redプレーンスキップ
	ld	b,a

	outi		; Greenプレーン出力

ENDM

; BRプレーン出力用
; Areg: VRAM(H)+1, Dreg: 040h
OUT_BR_HL_ADD_D	MACRO
	inc hl		; MASK部分をスキップ

	outi		; Blueプレーン出力

	add	a,d		; Redプレーンへ。
	ld	b,a

	outi		; Redプレーン出力

ENDM

; BRプレーン出力用 (マスク無)
; Areg: VRAM(H)+1, Dreg: 040h
OUT_BR_HL_ADD_D_N	MACRO
	outi		; Blueプレーン出力

	add	a,d		; Redプレーンへ。
	ld	b,a

	outi		; Redプレーン出力

ENDM

; 1プレーン出力用 (マスク無)
; Areg: VRAM(H)+1
OUT_1_HL	MACRO
	outi		; 1プレーン出力

ENDM

; BRGプレーン出力用 (マスク有り)
; Areg: VRAM(H)+1, Dreg: 040h
OUT_BRG_HL_ADD_D	MACRO
	inc		hl	; マスクスキップ

	outi		; Blueプレーン出力
	add	a,d		; Redプレーンへ。
	ld	b,a

	outi		; Redプレーン出力
	add	a,d		; Greenプレーンへ。
	ld	b,a

	outi		; Greenプレーン出力。
ENDM



; out (c),l / add a,h ld b,h
OUT_L8_ADD_H MACRO
	ld hl,008ffh

	; VRAM Clear

	; 0
	out (c),l
	add a,h
	ld b,a

	; 1
	out (c),l
	add a,h
	ld b,a

	; 2
	out (c),l
	add a,h
	ld b,a

	; 3
	out (c),l
	add a,h
	ld b,a

	; 4
	out (c),l
	add a,h
	ld b,a

	; 5
	out (c),l
	add a,h
	ld b,a

	; 6
	out (c),l
	add a,h
	ld b,a

	; 7
	out (c),l

ENDM

; (IO:bc)→(IO:bc') を8byte分行う。
OUT_BC_BCEX8	MACRO
	in	a,(c)
	inc	bc
	exx
	out	(c),a
	inc	bc
	exx

	in	a,(c)
	inc	bc
	exx
	out	(c),a
	inc	bc
	exx

	in	a,(c)
	inc	bc
	exx
	out	(c),a
	inc	bc
	exx

	in	a,(c)
	inc	bc
	exx
	out	(c),a
	inc	bc
	exx

	in	a,(c)
	inc	bc
	exx
	out	(c),a
	inc	bc
	exx

	in	a,(c)
	inc	bc
	exx
	out	(c),a
	inc	bc
	exx

	in	a,(c)
	inc	bc
	exx
	out	(c),a
	inc	bc
	exx

	in	a,(c)
	inc	bc
	exx
	out	(c),a
	inc	bc
	exx

ENDM

; (hl)→(IO:bc) を8byte分行う。
OUT_HL_BC8 MACRO
	; 0
	inc	b
	outi
	inc	bc

	inc	b
	outi
	inc	bc

	inc	b
	outi
	inc	bc

	inc	b
	outi
	inc	bc

	; 4
	inc	b
	outi
	inc	bc

	inc	b
	outi
	inc	bc

	inc	b
	outi
	inc	bc

	inc	b
	outi
	inc	bc
ENDM

; ADD_BC_04828 BCregに 4828hを足す
; BCregがGreenプレーンの7ライン目を指しているとして、
; 04828hを足す事で1ライン下のBlueプレーンに移動する。

ADD_BC_4828 MACRO
	; VRAMを次の段へ。
	ld a, 028h		; 7
	add a,c			; 4
	ld c,a			; 4
	ld a, 048h		; 7
	adc a,b			; 4
	ld b,a			; 4
ENDM

ADD_BC_C828 MACRO
	; VRAMを次の段へ。
	ld a, 028h		; 7
	add a,c			; 4
	ld c,a			; 4
	ld a, 0C8h		; 7
	adc a,b			; 4
	ld b,a			; 4
ENDM

ADD_BC_0028_AND_C7 MACRO
	; VRAMを次の段へ。
	ld a, 028h		; 7
	add a,c			; 4
	ld c,a			; 4

	ld a, 00h		; 7
	adc a,b			; 4
	and	07h
	ld b,a			; 4
ENDM

BLEND_RGB_HL_ADD_B_D MACRO
	ld e,(hl)	; mask
	inc hl

	; Blue.
	in a,(c)
	and e
	or (hl)
	out (c),a
	inc hl

	ld a,d
	add a,b
	ld b,a

	; Red.
	in a,(c)
	and e
	or (hl)
	out (c),a
	inc hl

	ld a,d
	add a,b
	ld b,a

	; Green.
	in a,(c)
	and e
	or (hl)
	out (c),a
	inc hl

ENDM

; BのみBLENDして、R,GはANDのみ。
; Dregは次のプレーンへの 40hが入っている。
; Ereg: マスク
BLEND_B_HL_ADD_B_D MACRO
	ld e,(hl)	; mask
	inc hl

	; Blue.
	in a,(c)
	and e
	or (hl)
	out (c),a
	inc hl

	ld a,d
	add a,b
	ld b,a

	; Redは andで穴をあけるのみ
	in a,(c)
	and e
	out (c),a

	ld a,d
	add a,b
	ld b,a

	; Greenは andで穴をあけるのみ
	in a,(c)
	and e
	out (c),a

ENDM


; RのみBLENDして、B,GはANDのみ。
; Dregは次のプレーンへの 40hが入っている。
; Ereg: マスク
BLEND_R_HL_ADD_B_D MACRO
	ld e,(hl)	; mask
	inc hl

	; Blueは andで穴をあけるのみ
	in a,(c)
	and e
	out (c),a

	; Blue→Red
	ld a,d
	add a,b
	ld b,a

	; Red.
	in a,(c)
	and e
	or		(hl)
	out (c),a
	inc hl

	; Red→Green
	ld a,d
	add a,b
	ld b,a

	; Greenは andで穴をあけるのみ
	in a,(c)
	and e
	out (c),a

ENDM

; GのみBLENDして、B,RはANDのみ。
; Dregは次のプレーンへの 40hが入っている。
; Ereg: マスク
BLEND_G_HL_ADD_B_D MACRO
	ld e,(hl)	; mask
	inc hl

	; Blueは andで穴をあけるのみ
	in a,(c)
	and e
	out (c),a

	; Blue→Red
	ld a,d
	add a,b
	ld b,a

	; Redは andで穴をあけるのみ。
	in a,(c)
	and e
	out (c),a

	; Red→Green
	ld a,d
	add a,b
	ld b,a

	; Green blend.
	in a,(c)
	and e
	or		(hl)
	out (c),a
	inc hl
ENDM

; B,RをBLENDして、GはANDのみ。
; Dreg: 次プレーン算出用に 040h。
BLEND_BR_HL_ADD_B_D MACRO
	ld e,(hl)	; mask
	inc hl

	; Blue: Blend
	in	a,(c)
	and	e
	or	(hl)
	out	(c),a
	inc	hl

	ld	a,d	; Redプレーンへ
	add	a,b
	ld	b,a

	; Red: Blend
	in	a,(c)
	and	e
	or	(hl)
	out	(c),a
	inc	hl

	ld a,d	; Greenプレーンへ。
	add a,b
	ld b,a

	; Greenは andで穴を開けるのみ。
	in	a,(c)
	and	e
	out	(c),a

ENDM


; B,GをBLENDして、RはANDのみ。
; Dreg: 次プレーン算出用に 040h。
BLEND_BG_HL_ADD_B_D MACRO
	ld e,(hl)	; mask
	inc hl

	; Blue: Blend
	in	a,(c)
	and	e
	or	(hl)
	out	(c),a
	inc	hl

	ld	a,d	; Redプレーンへ
	add	a,b
	ld	b,a

	; Redは andで穴を開けるのみ。
	in	a,(c)
	and	e
	out	(c),a

	ld a,d	; Greenプレーンへ。
	add a,b
	ld b,a

	; Green: Blend
	in a,(c)
	and e
	or (hl)
	out (c),a
	inc hl

ENDM

BLEND_RG_HL_ADD_B_D MACRO
	ld e,(hl)	; mask
	inc hl

;	; Blue は andで穴を空けるのみ。
	in a,(c)
	and e
	out (c),a

	ld a,d	; Redプレーンへ。
	add a,b
	ld b,a

	; Red.
	in a,(c)
	and e
	or (hl)
	out (c),a
	inc hl

	ld a,d	; Greenプレーンへ。
	add a,b
	ld b,a

	; Green.
	in a,(c)
	and e
	or (hl)
	out (c),a
	inc hl

ENDM


D_BLEND_RGB_HL_ADD_B_D MACRO
	ld e,(hl)	; mask
	inc hl

	; Blue.
	in a,(c)
	and e

	ld	a,0ffh
	and	e

;	or (hl)
	out (c),a
	inc hl

	ld a,d
	add a,b
	ld b,a

	; Red.
	in a,(c)
	and e
;	or (hl)
	out (c),a
	inc hl

	ld a,d
	add a,b
	ld b,a

	; Green.
	in a,(c)
	and e
;	or (hl)
	out (c),a
	inc hl

ENDM

; G→Bに戻す時に使用
ADD_B_80	MACRO
	ld	a,080h
	add a,b
	ld b,a
ENDM

; R→Gに使用
ADD_B_40	MACRO
	ld	a,040h
	add a,b
	ld b,a
ENDM

; BRG→Bに戻して更に1ライン下。
ADD_B_88	MACRO
	ld	a,088h
	add a,b
	ld b,a
ENDM

; B を更に1ライン下。(Dreg に 08hが入っている)
ADD_B_D		MACRO
	ld	a,d
	add	a,b
	ld	b,a
ENDM


; Breg プレーンを足す。
; AregにBregと同じ値が入っていて、Dregに040hが入っている。
ADD_A_D_B	MACRO
	add	a,d
	ld	b,a
ENDM

; 共通: Bプレーンに戻して更に1ライン下。
; Areg: VRAM(Gプレーン)+1, Ereg: 088h
ADD_B_E MACRO
	add	a,e
	ld	b,a
ENDM

; Aregの4bitを上下入れ替える。
RRCA4	MACRO
	rrca
	rrca
	rrca
	rrca
ENDM

; ジョイパッド (上)
BIT_A_0_KEY_UP MACRO
	bit	0,a
ENDM

; ジョイパッド (下)
BIT_A_1_KEY_DOWN MACRO
	bit	1,a
ENDM

; ジョイパッド (左)
BIT_A_2_KEY_LEFT MACRO
	bit	2,a
ENDM

; ジョイパッド (右)
BIT_A_3_KEY_RIGHT MACRO
	bit	3,a
ENDM

; ジョイパッド (トリガ1)
BIT_A_5_KEY_TRG1 MACRO
	bit	5,a
ENDM

; ジョイパッド (トリガ2)
BIT_A_6_KEY_TRG2 MACRO
	bit	6,a
ENDM

; アナログパレットデータ設定
; PALET_DATA_CDE [パレット番号(0-4095)], [GRB] (各4bit)
; CDEreg にセットする。

PALET_DATA_CDE	MACRO
	ld		c, %1>>4
	ld		de,  ( ( ((%1 & 0fh ) << 4) | (%2 & 0fh) ) << 8) | (%2 >> 4)
ENDM

;END

