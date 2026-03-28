; Converted from /home/user/SLANG-compiler/lib/libdef/libx1_psg.yml
; SLANG Runtime Library (new format)

; @name PSG_BASE
; @lib PSGLIB
; @extlib psg/psgdriverx1.asm:PSG_COMMON

; @name PSG_INIT
; @calls PSG_BASE,PSG_PROC
; @lib PSGLIB
; @extlib psg/psgdriverx1.asm:PSG_INIT

; @name PSG_PLAY
; @calls PSG_BASE
; @lib PSGLIB
; @extlib psg/psgdriverx1.asm:PSG_PLAY

; @name PSG_SFX
; @calls PSG_BASE
; @lib PSGLIB
; @extlib psg/psgdriverx1.asm:PSG_SFX

; @name PSG_STOP
; @calls PSG_BASE
; @lib PSGLIB
; @extlib psg/psgdriverx1.asm:PSG_STOP

; @name PSG_PAUSE
; @calls PSG_BASE
; @lib PSGLIB
; @extlib psg/psgdriverx1.asm:PSG_PAUSE

; @name PSG_RESUME
; @calls PSG_BASE
; @lib PSGLIB
; @extlib psg/psgdriverx1.asm:PSG_RESUME

; @name PSG_PROC
; @calls PSG_BASE
; @lib PSGLIB
; @extlib psg/psgdriverx1.asm:PSG_PROC

; @name PSG_END
; @calls PSG_BASE,PSG_STOP
; @lib PSGLIB
; @extlib psg/psgdriverx1.asm:PSG_END

