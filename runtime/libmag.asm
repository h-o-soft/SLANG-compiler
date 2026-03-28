; Converted from /home/user/SLANG-compiler/lib/libdef/libmag.yml
; SLANG Runtime Library (new format)

; @name MAGBASE
; @calls MAGLOAD,X1WORK
; @lib MAGLIB
; @extlib mag/MAG.ASM:MAGBASE

; @name GRDISP
; @lib MAGLIB
; @extlib mag/MAG.ASM:GRDISP

; @name GRCLS
; @calls MAGBASE
; @lib MAGLIB
; @extlib mag/MAG.ASM:GRCLS

; @name MAGLOAD
; @calls MAGBASE,X1WORK,GRCLS,MULHLDE,FOPEN,FGETC,FREAD,FSEEK,FCLOSE
; @lib MAGLIB
; @extlib mag/MAG.ASM:MAGLOAD

