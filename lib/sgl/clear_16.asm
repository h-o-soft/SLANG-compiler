;---------------------------------------------------------------;
;	Copyright (c) 2019 clear_16.asm
;	This software is released under the MIT License.
;	http://opensource.org/licenses/mit-license.php
;---------------------------------------------------------------; 

;---------------------------------------------------------------;
; Ypos=0 縦16pixelの消去
clear_size16_y0:
	; 8pixel 消去。
	call	clear_image_08

	; VRAMを次の段へ (and 07を行う事でy:0-7でも次の段へ)
	ADD_BC_0028_AND_C7

	; 8pixel 消去
	jp	clear_image_08

;---------------------------------------------------------------;
; Ypos=1 縦16pixelの消去
clear_size16_y1:
	ld	de, 0fe01h
	call clear_image_07

	; VRAMを次の段へ (and 07を行う事でy:0-7でも次の段へ)
	ADD_BC_0028_AND_C7

	; 8pixel 消去
	call clear_image_08

	; VRAMを次の段へ (and 07を行う事でy:0-7でも次の段へ)
	ADD_BC_0028_AND_C7

	ld	e, 0feh
	jp	clear_image_01

;---------------------------------------------------------------;
; Ypos=2 縦16pixelの消去
clear_size16_y2:
	ld	de,0fc03h
	call clear_image_06

	; VRAMを次の段へ (and 07を行う事でy:0-7でも次の段へ)
	ADD_BC_0028_AND_C7

	; 8pixel 消去
	call clear_image_08

	; VRAMを次の段へ (and 07を行う事でy:0-7でも次の段へ)
	ADD_BC_0028_AND_C7

	ld	e,0fch
	jp	clear_image_02

;---------------------------------------------------------------;
; Ypos=3 縦16pixelの消去
clear_size16_y3:
	ld	de,0f807h
	call clear_image_05

	; VRAMを次の段へ (and 07を行う事でy:0-7でも次の段へ)
	ADD_BC_0028_AND_C7

	; 8pixel 消去
	call clear_image_08

	; VRAMを次の段へ (and 07を行う事でy:0-7でも次の段へ)
	ADD_BC_0028_AND_C7

	ld	e,0f8h
	jp	clear_image_03

;---------------------------------------------------------------;
; Ypos=4 縦16pixelの消去
clear_size16_y4:
	ld	de,0f00fh
	call clear_image_04

	; VRAMを次の段へ (and 07を行う事でy:0-7でも次の段へ)
	ADD_BC_0028_AND_C7

	; 8pixel 消去
	call clear_image_08

	; VRAMを次の段へ (and 07を行う事でy:0-7でも次の段へ)
	ADD_BC_0028_AND_C7

	ld	de,00ff0h
	jp	clear_image_04

;---------------------------------------------------------------;
; Ypos=5 縦16pixelの消去
clear_size16_y5:
	ld	de,0e01fh
	call clear_image_03

	; VRAMを次の段へ (and 07を行う事でy:0-7でも次の段へ)
	ADD_BC_0028_AND_C7

	call clear_image_08

	; VRAMを次の段へ (and 07を行う事でy:0-7でも次の段へ)
	ADD_BC_0028_AND_C7

	ld	de, 01fe0h
	jp	clear_image_05

;---------------------------------------------------------------;
; Ypos=6 縦16pixelの消去
clear_size16_y6:
	ld	e,03fh
	call clear_image_02

	; VRAMを次の段へ (and 07を行う事でy:0-7でも次の段へ)
	ADD_BC_0028_AND_C7

	call clear_image_08

	; VRAMを次の段へ (and 07を行う事でy:0-7でも次の段へ)
	ADD_BC_0028_AND_C7

	ld	de, 03fc0h
	jp	clear_image_06

;---------------------------------------------------------------;
; Ypos=7 縦16pixelの消去
clear_size16_y7:
	ld	e,07fh
	call clear_image_01

	; VRAMを次の段へ (and 07を行う事でy:0-7でも次の段へ)
	ADD_BC_0028_AND_C7

	call clear_image_08

	; VRAMを次の段へ (and 07を行う事でy:0-7でも次の段へ)
	ADD_BC_0028_AND_C7

	ld	de,07f80h
	jp  clear_image_07

;----
;	END

