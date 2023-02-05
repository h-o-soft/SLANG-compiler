;---------------------------------------------------------------;
;	Copyright (c) 2019 value_define.asm
;	This software is released under the MIT License.
;	http://opensource.org/licenses/mit-license.php
;---------------------------------------------------------------; 

;---------------------------------------------------------------; 
;---------------------------------------------------------------; 

; メモリバンクの設定
;	メインメモリ 010h
;	バンクメモリ Bank 0: 00h    Bank 1: 01h
BANK_0B00				equ	0b00h

BANK_MAIN				equ 010h
BANK_00					equ	000h
BANK_01					equ 001h

CTC_ADRS				equ	01fa0h

ATTR_VRAM_ADRS			equ	02000h
TEXT_VRAM_ADRS			equ	03000h
KTEXT_VRAM_ADRS			equ	03800h

TEXT_VRAM0_ADRS			equ	03000h
TEXT_VRAM1_ADRS			equ	03400h

TEXT_VRAM19_SIZE		equ	(19*40)
TEXT_VRAM14_SIZE		equ	(14*40)
TEXT_VRAM7_SIZE			equ	(7*40)

KANJI_VRAM_ADRS			equ	03800h

B_VRAM_ADRS				equ	04000h
R_VRAM_ADRS				equ	08000h
G_VRAM_ADRS				equ	0c000h
PLANE_SIZE				equ	04000h		; 1プレーンのサイズ

FLIP_ADRS				equ	04h	; VRAMアドレスフリップ値


BLEND_BUFFER_ADRS		equ	09f00h
BLEND_BUFFER_SIZE		equ	05dc0h

PCG_BLUE				equ	015h
PCG_RED					equ	016h
PCG_GREEN				equ	017h

;;CRTC_1FD0				equ	(023h | 08h)	; PCG高速アクセスモード + 24KHz + 2ラスタ
CRTC_1FD0				equ	023h	; PCG高速アクセスモード + 24KHz + 2ラスタ

CRTC_1FD0_L				equ	020h	; PCG高速アクセスモード


;JUMP_TABLE_SIZE12		equ	0f5h


; メインメモリマップ
; 0000-0f4ff プログラム,データエリア
; 0f500-0f5ff 割込みベクトル,スタック
; 0f600h VRAMアドレステーブル(H)
; 0f700h VRAMアドレステーブル(L)
; 0f800h ビットラインバッファ(Page 0)
; 0fc00h ビットラインバッファ(Page 1)

INT_VECTOR_BUFF			equ	0f500h

INT_VECTOR_KEYBOARD		equ	INT_VECTOR_BUFF + 010h

STACK_BUFF				equ 0f500h+0100h	; スタックポインタ


VRAM_ADRS_TBL_H			equ	0f6h	; VRAMアドレステーブル上位
VRAM_ADRS_TBL_L			equ	0f7h	; VRAMアドレステーブル下位

BITLINE_MASK			equ	0f8h

BITLINE_BUFFER0_ADRS	equ	0f800h
BITLINE_BUFFER0_H		equ	0f8h

BITLINE_BUFFER1_ADRS	equ	0fc00h
BITLINE_BUFFER1_H		equ	0fch

BITLINE_BUFFER_SIZE		equ	1000

;パレットデータ
GAME_PALET_B			equ	0ceh
GAME_PALET_R			equ	0f2h
GAME_PALET_G			equ	066h

; --------------
; キャラクタKIND
; ジャンプテーブルの関係から 3づつ増える。
KIND_NONE				equ	0
KIND_A				equ	1*3
KIND_B				equ	2*3
KIND_C				equ	3*3

; --------------
; キャラクタパターンデータ
X_OFS_0					equ	00h
X_OFS_1					equ	01h
X_OFS_2					equ	02h
X_OFS_3					equ	03h
X_OFS_4					equ	04h
X_OFS_5					equ	05h
X_OFS_6					equ	06h
X_OFS_7					equ	07h
X_OFS_NUM				equ	08h

; --------------
; キャラクタパターン

; 必要に応じて定義する(が、基本はここでは定義しない)
PAT_01					equ	01h*2
PAT_02					equ	02h*2
PAT_03					equ	03h*2

;---------------------------------------------------------------;
;---------------------------------------------------------------;

; 画面外判定定数
; (表示(X 上位8bit) + OFF_SCREEN_X_OFFSET) > OFF_SCREEN_X_RANGE の時画面外
;
; 例:
; 　画面外: -8 ～ 328としてスクリーン値(上位8bit)は 0fch～0a4hになる。
; 　+3で 0a7h～0ffhの範囲であれば画面外。
; 　OFF_SCREEN_X_OFFSET を足して、OFF_SCREEN_X_RANGEより大きければ画面外。
OFF_SCREEN_X_OFFSET		equ	03h
OFF_SCREEN_X_RANGE		equ	167
CLIP_RIGHT_SCREEN_X		equ	164



;END

