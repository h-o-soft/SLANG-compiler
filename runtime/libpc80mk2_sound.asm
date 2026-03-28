; Converted from /home/user/SLANG-compiler/lib/libdef/libpc80mk2_sound.yml
; SLANG Runtime Library (new format)

; @name SND_COMMON
; @lib PC80SND
; @extlib pc8001/soundv2.z80:SND_COMMON

; @name SND_STOP
; @calls SND_COMMON
; @lib PC80SND
; @extlib pc8001/soundv2.z80:SND_STOP

; @name SND_INIT
; @calls SND_COMMON
; @lib PC80SND
; @extlib pc8001/soundv2.z80:SND_INIT

; @name SND_PLAY
; @calls SND_COMMON
; @lib PC80SND
; @extlib pc8001/soundv2.z80:SND_PLAY

; @name SND_SEPLAY
; @calls SND_COMMON
; @lib PC80SND
; @extlib pc8001/soundv2.z80:SND_SEPLAY

; @name SND_SYNC
; @calls SND_COMMON,SND_PROC
; @lib PC80SND
; @extlib pc8001/soundv2.z80:SND_SYNC

; @name SND_PROC
; @calls SND_COMMON
; @lib PC80SND
; @extlib pc8001/soundv2.z80:SND_PROC

; @name SND_ISPLAYING
; @calls SND_COMMON
; @lib PC80SND
; @extlib pc8001/soundv2.z80:SND_ISPLAYING

