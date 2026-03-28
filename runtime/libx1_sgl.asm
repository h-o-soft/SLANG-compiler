; Converted from /home/user/SLANG-compiler/lib/libdef/libx1_sgl.yml
; SLANG Runtime Library (new format)

; @name X1SGLINCLUDE
; @lib x1sgl
; @include sgl/macro_define.asm
; @include sgl/value_define.asm
; @include sgl/render_util.asm
; @include sgl/text_render.asm
; @include sgl/mem_util.asm
; @include sgl/chara_manager.asm
; @include sgl/chara_data_manager.asm
; @include sgl/bitline.asm
; @include sgl/crtc.asm
; @include sgl/chara_render.asm
; @include sgl/clear_buff.asm
; @include sgl/render.asm
; @include sgl/render_r.asm
; @include sgl/render_g.asm
; @include sgl/render_brg16.asm
; @include sgl/render_br16.asm
; @include sgl/render_br.asm
; @include sgl/render_b16.asm
; @include sgl/clear_16.asm
; @include sgl/data_work.asm

; @name X1SGLBASE
; @lib x1sgl
; @extlib sgl/x1sgl.asm:SGLBASE

; @name SGL_INIT
; @calls X1SGLINCLUDE,X1SGLBASE
; @lib x1sgl
; @extlib sgl/x1sgl.asm:SGL_INIT

; @name SGL_DEFPAT
; @calls X1SGLINCLUDE,X1SGLBASE
; @lib x1sgl
; @extlib sgl/x1sgl.asm:SGL_DEFPAT

; @name SGL_SPRCREATE
; @calls X1SGLINCLUDE,X1SGLBASE
; @lib x1sgl
; @extlib sgl/x1sgl.asm:SGL_SPRCREATE

; @name SGL_SPRDESTROY
; @calls X1SGLINCLUDE,X1SGLBASE
; @lib x1sgl
; @extlib sgl/x1sgl.asm:SGL_SPRDESTROY

; @name SGL_SPRSET
; @calls X1SGLINCLUDE,X1SGLBASE
; @lib x1sgl
; @extlib sgl/x1sgl.asm:SGL_SPRSET

; @name SGL_SPRPAT
; @calls X1SGLINCLUDE,X1SGLBASE
; @lib x1sgl
; @extlib sgl/x1sgl.asm:SGL_SPRPAT

; @name SGL_SPRMOVE
; @calls X1SGLINCLUDE,X1SGLBASE
; @lib x1sgl
; @extlib sgl/x1sgl.asm:SGL_SPRMOVE

; @name SGL_SPRDISP
; @calls X1SGLINCLUDE,X1SGLBASE
; @lib x1sgl
; @extlib sgl/x1sgl.asm:SGL_SPRDISP

; @name SGL_FPSMODE
; @calls X1SGLINCLUDE,X1SGLBASE
; @lib x1sgl
; @extlib sgl/x1sgl.asm:SGL_FPSMODE

; @name SGL_VSYNC
; @calls X1SGLINCLUDE,X1SGLBASE
; @lib x1sgl
; @extlib sgl/x1sgl.asm:SGL_VSYNC

; @name SGL_PRINT
; @calls X1SGLINCLUDE,X1SGLBASE,AT_VRCALC
; @lib x1sgl
; @extlib sgl/x1sgl.asm:SGL_PRINT

; @name SGL_PRINT2
; @calls X1SGLINCLUDE,X1SGLBASE,AT_VRCALC
; @lib x1sgl
; @extlib sgl/x1sgl.asm:SGL_PRINT2

