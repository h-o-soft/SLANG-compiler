; Converted from /home/user/SLANG-compiler/lib/libdef/libmsx_spdrv.yml
; SLANG Runtime Library (new format)

; @name SPDRV_INCLUDE
; @lib MSXSPDRV
; @include spdrv/msx_constant.asm

; @name SPDRV_WORK
; @calls SPDRV_INCLUDE
; @lib MSXSPDRV
; @extlib spdrv/sprite_driver_work.asm:SPDRV_DUMMY

; @name SPDRV_INITIALIZE
; @calls SPDRV_WORK
; @lib MSXSPDRV
; @extlib spdrv/sprite_driver.asm:SPDRV_INITIALIZE

; @name SPDRV_FLIP
; @calls SPDRV_WORK
; @lib MSXSPDRV
; @extlib spdrv/sprite_driver.asm:SPDRV_FLIP

; @name SPDRV_SET
; @calls SPDRV_WORK
; @lib MSXSPDRV
; @extlib spdrv/sprite_driver.asm:SPDRV_SET

; @name SPDRV_MOVE
; @calls SPDRV_WORK
; @lib MSXSPDRV
; @extlib spdrv/sprite_driver.asm:SPDRV_MOVE

; @name SPDRV_UPDATE
; @calls SPDRV_WORK
; @lib MSXSPDRV
; @extlib spdrv/sprite_driver.asm:SPDRV_UPDATE

; @name SPDRV2_WORK
; @calls SPDRV_INCLUDE
; @lib MSXSPDRV
; @extlib spdrv2/sprite_driver_work.asm:SPDRV2_DUMMY

; @name SPDRV2_INITIALIZE
; @calls SPDRV2_WORK
; @lib MSXSPDRV
; @extlib spdrv2/sprite_driver.asm:SPDRV2_INITIALIZE

; @name SPDRV2_FLIP
; @calls SPDRV2_WORK
; @lib MSXSPDRV
; @extlib spdrv2/sprite_driver.asm:SPDRV2_FLIP

; @name SPDRV2_SET
; @calls SPDRV2_WORK
; @lib MSXSPDRV
; @extlib spdrv2/sprite_driver.asm:SPDRV2_SET

; @name SPDRV2_MOVE
; @calls SPDRV2_WORK
; @lib MSXSPDRV
; @extlib spdrv2/sprite_driver.asm:SPDRV2_MOVE

; @name SPDRV2_UPDATE
; @calls SPDRV2_WORK
; @lib MSXSPDRV
; @extlib spdrv2/sprite_driver.asm:SPDRV2_UPDATE

