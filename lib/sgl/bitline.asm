;---------------------------------------------------------------;
;	Copyright (c) 2019 bitline.asm
;	This software is released under the MIT License.
;	http://opensource.org/licenses/mit-license.php
;---------------------------------------------------------------; 

; BitLine関連 (2017/02/18)
; BitLineはTimeStampの変形バージョンとして考案した。
; 8x8の各ラインを1bitで表現している。Line0⇔Bit0に対応する。
; 各ラインを描画時対応するBitを立てる。
; このバッファを使う事で、ライン単位のブレンド判定が可能になる。

init_bitline:
	; BitLineBufferをクリア
	ld	hl, BITLINE_BUFFER0_ADRS
	ld	bc, BITLINE_BUFFER_SIZE
	call	clear_mem

	ld	hl, BITLINE_BUFFER1_ADRS
	ld	bc, BITLINE_BUFFER_SIZE
	call	clear_mem

	ret


; ----
;	END

