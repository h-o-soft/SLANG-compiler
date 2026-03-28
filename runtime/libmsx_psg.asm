; Converted from /home/user/SLANG-compiler/lib/libdef/libmsx_psg.yml
; SLANG Runtime Library (new format)

; @name PSG_BASE
; @lib PSGLIB
RDVRM:        EQU $004A	; BIOS RDVRM
WRTVRM:       EQU $004D	; BIOS WRTVRM
FILVRM:       EQU	$0056	; BIOS VRAM指定領域同一データ転送
LDIRVM:       EQU	$005C	; BIOS VRAMブロック転送
CHGMOD:       EQU $005F   ; BIOS スクリーンモード変更
GICINI:	      EQU $0090	; PSGの初期化アドレス
WRTPSG:	      EQU $0093   ; PSGレジスタへのデータ書込アドレス
ERAFNC:       EQU $00CC   ; BIOS ファンクションキー非表示
GTSTCK:       EQU $00D5   ; BIOS ジョイスティックの状態取得
GTTRIG:       EQU $00D8   ; BIOS トリガボタンの状態取得
SNSMAT:       EQU $0141   ; BIOS キーマトリクススキャン
KILBUF:       EQU $0156   ; BIOS キーバッファクリア
LINL32:       EQU $F3AF   ; WIDTH値
CLIKSW:       EQU $F3DB   ; キークリックスイッチ(0:OFF,0以外:ON)
REG0SAV:      EQU $F3DF   ; VDPコントロールレジスタ0
REG1SAV:      EQU $F3E0   ; VDPコントロールレジスタ1
FORCLR:       EQU $F3E9   ; 前景色
BAKCLR:       EQU $F3EA   ; 背景色
BDRCLR:       EQU $F3EB   ; 周辺色
INTCNT:       EQU $FCA2   ; システムで1/60秒でインクリメントするワークエリア
H_TIMI:       EQU $FD9F   ; 垂直帰線割り込みフック

; @extlib psg/psgdriver.asm:PSG_COMMON

; @name PSG_INIT
; @calls PSG_BASE
; @lib PSGLIB
; @extlib psg/psgdriver.asm:PSG_INIT

; @name PSG_PLAY
; @calls PSG_BASE
; @lib PSGLIB
; @extlib psg/psgdriver.asm:PSG_PLAY

; @name PSG_SFX
; @calls PSG_BASE
; @lib PSGLIB
; @extlib psg/psgdriver.asm:PSG_SFX

; @name PSG_STOP
; @calls PSG_BASE
; @lib PSGLIB
; @extlib psg/psgdriver.asm:PSG_STOP

; @name PSG_PAUSE
; @calls PSG_BASE
; @lib PSGLIB
; @extlib psg/psgdriver.asm:PSG_PAUSE

; @name PSG_RESUME
; @calls PSG_BASE
; @lib PSGLIB
; @extlib psg/psgdriver.asm:PSG_RESUME

; @name PSG_PROC
; @calls PSG_BASE
; @lib PSGLIB
; @extlib psg/psgdriver.asm:PSG_PROC

; @name PSG_END
; @calls PSG_BASE,PSG_STOP
; @lib PSGLIB
; @extlib psg/psgdriver.asm:PSG_END

; @name VSYNC
; VSYNC(MSX) / not implemented
RET


