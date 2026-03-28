; CONST WD
; CONST BALL1
; CONST BALL3
; CONST MUSADR
; ARRAY SPRDAT[SLANGCompiler.Parser.Ast.BinaryExpr]
; CONST BLOCKPCG
; global: InlineAsm asm"; X1 SGLのパターンデータテーブル
ball_p0_c1:
  db  0fch  ; Pivot(x) -4
  db  0f8h  ; Pivot(y) -8
  db  8  ; データ数
  dw  ball_p0_c1_0  ; 0
  dw  ball_p0_c1_1  ; 1
  dw  ball_p0_c1_2  ; 2
  dw  ball_p0_c1_3  ; 3
  dw  ball_p0_c1_4  ; 4
  dw  ball_p0_c1_5  ; 5
  dw  ball_p0_c1_6  ; 6
  dw  ball_p0_c1_7  ; 7

ball_p0_c1_0:
; Xoffset: 0
; Mask: [B]
; OutSizeX: 16 OutSizeY: 16
; DataOrder: Vertical

  db 183  ; clipy(byte) 200-sizey-1
  db 000h  ; DrawType (Plane: B SizeY: 16)
  db 39  ; clipx(right) 40-sizex+1
  db 63  ; clipx(left) 64-sizex+1
  db 2  ; sizex(byte)
  db 32  ; sizey(pitch)

; 0(x)
  db 0f8h, 007h
  db 0e0h, 01eh
  db 0c0h, 03ch
  db 080h, 070h
  db 080h, 062h
  db 000h, 0e7h
  db 000h, 0cfh
  db 000h, 087h
  db 000h, 087h
  db 000h, 0cfh
  db 000h, 0e7h
  db 080h, 062h
  db 080h, 070h
  db 0c0h, 03ch
  db 0e0h, 01eh
  db 0f8h, 007h
; 1(x)
  db 01fh, 0e0h
  db 007h, 078h
  db 003h, 03ch
  db 001h, 00eh
  db 001h, 046h
  db 000h, 0e7h
  db 000h, 0f3h
  db 000h, 0e1h
  db 000h, 0e1h
  db 000h, 0f3h
  db 000h, 0e7h
  db 001h, 046h
  db 001h, 00eh
  db 003h, 03ch
  db 007h, 078h
  db 01fh, 0e0h

ball_p0_c1_1:
; Xoffset: 1
; Mask: [B]
; OutSizeX: 24 OutSizeY: 16
; DataOrder: Vertical

  db 183  ; clipy(byte) 200-sizey-1
  db 000h  ; DrawType (Plane: B SizeY: 16)
  db 38  ; clipx(right) 40-sizex+1
  db 62  ; clipx(left) 64-sizex+1
  db 3  ; sizex(byte)
  db 32  ; sizey(pitch)

; 0(x)
  db 0fch, 003h
  db 0f0h, 00fh
  db 0e0h, 01eh
  db 0c0h, 038h
  db 0c0h, 031h
  db 080h, 073h
  db 080h, 067h
  db 080h, 043h
  db 080h, 043h
  db 080h, 067h
  db 080h, 073h
  db 0c0h, 031h
  db 0c0h, 038h
  db 0e0h, 01eh
  db 0f0h, 00fh
  db 0fch, 003h
; 1(x)
  db 00fh, 0f0h
  db 003h, 03ch
  db 001h, 01eh
  db 000h, 007h
  db 000h, 023h
  db 000h, 0f3h
  db 000h, 0f9h
  db 000h, 0f0h
  db 000h, 0f0h
  db 000h, 0f9h
  db 000h, 0f3h
  db 000h, 023h
  db 000h, 007h
  db 001h, 01eh
  db 003h, 03ch
  db 00fh, 0f0h
; 2(x)
  db 0ffh, 000h
  db 0ffh, 000h
  db 0ffh, 000h
  db 0ffh, 000h
  db 0ffh, 000h
  db 07fh, 080h
  db 07fh, 080h
  db 07fh, 080h
  db 07fh, 080h
  db 07fh, 080h
  db 07fh, 080h
  db 0ffh, 000h
  db 0ffh, 000h
  db 0ffh, 000h
  db 0ffh, 000h
  db 0ffh, 000h

ball_p0_c1_2:
; Xoffset: 2
; Mask: [B]
; OutSizeX: 24 OutSizeY: 16
; DataOrder: Vertical

  db 183  ; clipy(byte) 200-sizey-1
  db 000h  ; DrawType (Plane: B SizeY: 16)
  db 38  ; clipx(right) 40-sizex+1
  db 62  ; clipx(left) 64-sizex+1
  db 3  ; sizex(byte)
  db 32  ; sizey(pitch)

; 0(x)
  db 0feh, 001h
  db 0f8h, 007h
  db 0f0h, 00fh
  db 0e0h, 01ch
  db 0e0h, 018h
  db 0c0h, 039h
  db 0c0h, 033h
  db 0c0h, 021h
  db 0c0h, 021h
  db 0c0h, 033h
  db 0c0h, 039h
  db 0e0h, 018h
  db 0e0h, 01ch
  db 0f0h, 00fh
  db 0f8h, 007h
  db 0feh, 001h
; 1(x)
  db 007h, 0f8h
  db 001h, 09eh
  db 000h, 00fh
  db 000h, 003h
  db 000h, 091h
  db 000h, 0f9h
  db 000h, 0fch
  db 000h, 0f8h
  db 000h, 0f8h
  db 000h, 0fch
  db 000h, 0f9h
  db 000h, 091h
  db 000h, 003h
  db 000h, 00fh
  db 001h, 09eh
  db 007h, 0f8h
; 2(x)
  db 0ffh, 000h
  db 0ffh, 000h
  db 0ffh, 000h
  db 07fh, 080h
  db 07fh, 080h
  db 03fh, 0c0h
  db 03fh, 0c0h
  db 03fh, 040h
  db 03fh, 040h
  db 03fh, 0c0h
  db 03fh, 0c0h
  db 07fh, 080h
  db 07fh, 080h
  db 0ffh, 000h
  db 0ffh, 000h
  db 0ffh, 000h

ball_p0_c1_3:
; Xoffset: 3
; Mask: [B]
; OutSizeX: 24 OutSizeY: 16
; DataOrder: Vertical

  db 183  ; clipy(byte) 200-sizey-1
  db 000h  ; DrawType (Plane: B SizeY: 16)
  db 38  ; clipx(right) 40-sizex+1
  db 62  ; clipx(left) 64-sizex+1
  db 3  ; sizex(byte)
  db 32  ; sizey(pitch)

; 0(x)
  db 0ffh, 000h
  db 0fch, 003h
  db 0f8h, 007h
  db 0f0h, 00eh
  db 0f0h, 00ch
  db 0e0h, 01ch
  db 0e0h, 019h
  db 0e0h, 010h
  db 0e0h, 010h
  db 0e0h, 019h
  db 0e0h, 01ch
  db 0f0h, 00ch
  db 0f0h, 00eh
  db 0f8h, 007h
  db 0fch, 003h
  db 0ffh, 000h
; 1(x)
  db 003h, 0fch
  db 000h, 0cfh
  db 000h, 087h
  db 000h, 001h
  db 000h, 048h
  db 000h, 0fch
  db 000h, 0feh
  db 000h, 0fch
  db 000h, 0fch
  db 000h, 0feh
  db 000h, 0fch
  db 000h, 048h
  db 000h, 001h
  db 000h, 087h
  db 000h, 0cfh
  db 003h, 0fch
; 2(x)
  db 0ffh, 000h
  db 0ffh, 000h
  db 07fh, 080h
  db 03fh, 0c0h
  db 03fh, 0c0h
  db 01fh, 0e0h
  db 01fh, 060h
  db 01fh, 020h
  db 01fh, 020h
  db 01fh, 060h
  db 01fh, 0e0h
  db 03fh, 0c0h
  db 03fh, 0c0h
  db 07fh, 080h
  db 0ffh, 000h
  db 0ffh, 000h

ball_p0_c1_4:
; Xoffset: 4
; Mask: [B]
; OutSizeX: 24 OutSizeY: 16
; DataOrder: Vertical

  db 183  ; clipy(byte) 200-sizey-1
  db 000h  ; DrawType (Plane: B SizeY: 16)
  db 38  ; clipx(right) 40-sizex+1
  db 62  ; clipx(left) 64-sizex+1
  db 3  ; sizex(byte)
  db 32  ; sizey(pitch)

; 0(x)
  db 0ffh, 000h
  db 0feh, 001h
  db 0fch, 003h
  db 0f8h, 007h
  db 0f8h, 006h
  db 0f0h, 00eh
  db 0f0h, 00ch
  db 0f0h, 008h
  db 0f0h, 008h
  db 0f0h, 00ch
  db 0f0h, 00eh
  db 0f8h, 006h
  db 0f8h, 007h
  db 0fch, 003h
  db 0feh, 001h
  db 0ffh, 000h
; 1(x)
  db 081h, 07eh
  db 000h, 0e7h
  db 000h, 0c3h
  db 000h, 000h
  db 000h, 024h
  db 000h, 07eh
  db 000h, 0ffh
  db 000h, 07eh
  db 000h, 07eh
  db 000h, 0ffh
  db 000h, 07eh
  db 000h, 024h
  db 000h, 000h
  db 000h, 0c3h
  db 000h, 0e7h
  db 081h, 07eh
; 2(x)
  db 0ffh, 000h
  db 07fh, 080h
  db 03fh, 0c0h
  db 01fh, 0e0h
  db 01fh, 060h
  db 00fh, 070h
  db 00fh, 030h
  db 00fh, 010h
  db 00fh, 010h
  db 00fh, 030h
  db 00fh, 070h
  db 01fh, 060h
  db 01fh, 0e0h
  db 03fh, 0c0h
  db 07fh, 080h
  db 0ffh, 000h

ball_p0_c1_5:
; Xoffset: 5
; Mask: [B]
; OutSizeX: 24 OutSizeY: 16
; DataOrder: Vertical

  db 183  ; clipy(byte) 200-sizey-1
  db 000h  ; DrawType (Plane: B SizeY: 16)
  db 38  ; clipx(right) 40-sizex+1
  db 62  ; clipx(left) 64-sizex+1
  db 3  ; sizex(byte)
  db 32  ; sizey(pitch)

; 0(x)
  db 0ffh, 000h
  db 0ffh, 000h
  db 0feh, 001h
  db 0fch, 003h
  db 0fch, 003h
  db 0f8h, 007h
  db 0f8h, 006h
  db 0f8h, 004h
  db 0f8h, 004h
  db 0f8h, 006h
  db 0f8h, 007h
  db 0fch, 003h
  db 0fch, 003h
  db 0feh, 001h
  db 0ffh, 000h
  db 0ffh, 000h
; 1(x)
  db 0c0h, 03fh
  db 000h, 0f3h
  db 000h, 0e1h
  db 000h, 080h
  db 000h, 012h
  db 000h, 03fh
  db 000h, 07fh
  db 000h, 03fh
  db 000h, 03fh
  db 000h, 07fh
  db 000h, 03fh
  db 000h, 012h
  db 000h, 080h
  db 000h, 0e1h
  db 000h, 0f3h
  db 0c0h, 03fh
; 2(x)
  db 0ffh, 000h
  db 03fh, 0c0h
  db 01fh, 0e0h
  db 00fh, 070h
  db 00fh, 030h
  db 007h, 038h
  db 007h, 098h
  db 007h, 008h
  db 007h, 008h
  db 007h, 098h
  db 007h, 038h
  db 00fh, 030h
  db 00fh, 070h
  db 01fh, 0e0h
  db 03fh, 0c0h
  db 0ffh, 000h

ball_p0_c1_6:
; Xoffset: 6
; Mask: [B]
; OutSizeX: 24 OutSizeY: 16
; DataOrder: Vertical

  db 183  ; clipy(byte) 200-sizey-1
  db 000h  ; DrawType (Plane: B SizeY: 16)
  db 38  ; clipx(right) 40-sizex+1
  db 62  ; clipx(left) 64-sizex+1
  db 3  ; sizex(byte)
  db 32  ; sizey(pitch)

; 0(x)
  db 0ffh, 000h
  db 0ffh, 000h
  db 0ffh, 000h
  db 0feh, 001h
  db 0feh, 001h
  db 0fch, 003h
  db 0fch, 003h
  db 0fch, 002h
  db 0fch, 002h
  db 0fch, 003h
  db 0fch, 003h
  db 0feh, 001h
  db 0feh, 001h
  db 0ffh, 000h
  db 0ffh, 000h
  db 0ffh, 000h
; 1(x)
  db 0e0h, 01fh
  db 080h, 079h
  db 000h, 0f0h
  db 000h, 0c0h
  db 000h, 089h
  db 000h, 09fh
  db 000h, 03fh
  db 000h, 01fh
  db 000h, 01fh
  db 000h, 03fh
  db 000h, 09fh
  db 000h, 089h
  db 000h, 0c0h
  db 000h, 0f0h
  db 080h, 079h
  db 0e0h, 01fh
; 2(x)
  db 07fh, 080h
  db 01fh, 0e0h
  db 00fh, 0f0h
  db 007h, 038h
  db 007h, 018h
  db 003h, 09ch
  db 003h, 0cch
  db 003h, 084h
  db 003h, 084h
  db 003h, 0cch
  db 003h, 09ch
  db 007h, 018h
  db 007h, 038h
  db 00fh, 0f0h
  db 01fh, 0e0h
  db 07fh, 080h

ball_p0_c1_7:
; Xoffset: 7
; Mask: [B]
; OutSizeX: 24 OutSizeY: 16
; DataOrder: Vertical

  db 183  ; clipy(byte) 200-sizey-1
  db 000h  ; DrawType (Plane: B SizeY: 16)
  db 38  ; clipx(right) 40-sizex+1
  db 62  ; clipx(left) 64-sizex+1
  db 3  ; sizex(byte)
  db 32  ; sizey(pitch)

; 0(x)
  db 0ffh, 000h
  db 0ffh, 000h
  db 0ffh, 000h
  db 0ffh, 000h
  db 0ffh, 000h
  db 0feh, 001h
  db 0feh, 001h
  db 0feh, 001h
  db 0feh, 001h
  db 0feh, 001h
  db 0feh, 001h
  db 0ffh, 000h
  db 0ffh, 000h
  db 0ffh, 000h
  db 0ffh, 000h
  db 0ffh, 000h
; 1(x)
  db 0f0h, 00fh
  db 0c0h, 03ch
  db 080h, 078h
  db 000h, 0e0h
  db 000h, 0c4h
  db 000h, 0cfh
  db 000h, 09fh
  db 000h, 00fh
  db 000h, 00fh
  db 000h, 09fh
  db 000h, 0cfh
  db 000h, 0c4h
  db 000h, 0e0h
  db 080h, 078h
  db 0c0h, 03ch
  db 0f0h, 00fh
; 2(x)
  db 03fh, 0c0h
  db 00fh, 0f0h
  db 007h, 078h
  db 003h, 01ch
  db 003h, 08ch
  db 001h, 0ceh
  db 001h, 0e6h
  db 001h, 0c2h
  db 001h, 0c2h
  db 001h, 0e6h
  db 001h, 0ceh
  db 003h, 08ch
  db 003h, 01ch
  db 007h, 078h
  db 00fh, 0f0h
  db 03fh, 0c0h

ball3_p0_c3:
  db  0fch  ; Pivot(x) -4
  db  0f8h  ; Pivot(y) -8
  db  8  ; データ数
  dw  ball3_p0_c3_0  ; 0
  dw  ball3_p0_c3_1  ; 1
  dw  ball3_p0_c3_2  ; 2
  dw  ball3_p0_c3_3  ; 3
  dw  ball3_p0_c3_4  ; 4
  dw  ball3_p0_c3_5  ; 5
  dw  ball3_p0_c3_6  ; 6
  dw  ball3_p0_c3_7  ; 7

ball3_p0_c3_0:
; Xoffset: 0
; Mask: [B R G]
; OutSizeX: 16 OutSizeY: 16
; DataOrder: Vertical

  db 183  ; clipy(byte) 200-sizey-1
  db 010h  ; DrawType (Plane: BRG SizeY: 16)
  db 39  ; clipx(right) 40-sizex+1
  db 63  ; clipx(left) 64-sizex+1
  db 2  ; sizex(byte)
  db 64  ; sizey(pitch)

; 0(x)
  db 0f8h, 007h, 007h, 007h
  db 0e0h, 01fh, 01fh, 01fh
  db 0c0h, 03fh, 03fh, 03fh
  db 080h, 07fh, 07fh, 07fh
  db 080h, 077h, 07fh, 070h
  db 000h, 0e3h, 0ffh, 0e0h
  db 000h, 0c1h, 0ffh, 0c0h
  db 000h, 080h, 0ffh, 080h
  db 000h, 0f1h, 0f0h, 0ffh
  db 000h, 0f3h, 0f0h, 0ffh
  db 000h, 0f7h, 0f0h, 0ffh
  db 080h, 07fh, 070h, 07fh
  db 080h, 07fh, 078h, 07fh
  db 0c0h, 03fh, 03ch, 03fh
  db 0e0h, 01fh, 01eh, 01fh
  db 0f8h, 007h, 007h, 007h
; 1(x)
  db 01fh, 0e0h, 0e0h, 0e0h
  db 007h, 0f8h, 078h, 0f8h
  db 003h, 0fch, 03ch, 0fch
  db 001h, 0feh, 01eh, 0feh
  db 001h, 0feh, 00eh, 0feh
  db 000h, 0efh, 00fh, 0ffh
  db 000h, 0cfh, 00fh, 0ffh
  db 000h, 08fh, 00fh, 0ffh
  db 000h, 001h, 0ffh, 001h
  db 000h, 083h, 0ffh, 003h
  db 000h, 0c7h, 0ffh, 007h
  db 001h, 0eeh, 0feh, 00eh
  db 001h, 0feh, 0feh, 0feh
  db 003h, 0fch, 0fch, 0fch
  db 007h, 0f8h, 0f8h, 0f8h
  db 01fh, 0e0h, 0e0h, 0e0h

ball3_p0_c3_1:
; Xoffset: 1
; Mask: [B R G]
; OutSizeX: 24 OutSizeY: 16
; DataOrder: Vertical

  db 183  ; clipy(byte) 200-sizey-1
  db 010h  ; DrawType (Plane: BRG SizeY: 16)
  db 38  ; clipx(right) 40-sizex+1
  db 62  ; clipx(left) 64-sizex+1
  db 3  ; sizex(byte)
  db 64  ; sizey(pitch)

; 0(x)
  db 0fch, 003h, 003h, 003h
  db 0f0h, 00fh, 00fh, 00fh
  db 0e0h, 01fh, 01fh, 01fh
  db 0c0h, 03fh, 03fh, 03fh
  db 0c0h, 03bh, 03fh, 038h
  db 080h, 071h, 07fh, 070h
  db 080h, 060h, 07fh, 060h
  db 080h, 040h, 07fh, 040h
  db 080h, 078h, 078h, 07fh
  db 080h, 079h, 078h, 07fh
  db 080h, 07bh, 078h, 07fh
  db 0c0h, 03fh, 038h, 03fh
  db 0c0h, 03fh, 03ch, 03fh
  db 0e0h, 01fh, 01eh, 01fh
  db 0f0h, 00fh, 00fh, 00fh
  db 0fch, 003h, 003h, 003h
; 1(x)
  db 00fh, 0f0h, 0f0h, 0f0h
  db 003h, 0fch, 0bch, 0fch
  db 001h, 0feh, 09eh, 0feh
  db 000h, 0ffh, 08fh, 0ffh
  db 000h, 0ffh, 087h, 07fh
  db 000h, 0f7h, 087h, 07fh
  db 000h, 0e7h, 087h, 07fh
  db 000h, 047h, 087h, 07fh
  db 000h, 080h, 07fh, 080h
  db 000h, 0c1h, 07fh, 081h
  db 000h, 0e3h, 07fh, 083h
  db 000h, 0f7h, 07fh, 087h
  db 000h, 0ffh, 07fh, 0ffh
  db 001h, 0feh, 07eh, 0feh
  db 003h, 0fch, 07ch, 0fch
  db 00fh, 0f0h, 0f0h, 0f0h
; 2(x)
  db 0ffh, 000h, 000h, 000h
  db 0ffh, 000h, 000h, 000h
  db 0ffh, 000h, 000h, 000h
  db 0ffh, 000h, 000h, 000h
  db 0ffh, 000h, 000h, 000h
  db 07fh, 080h, 080h, 080h
  db 07fh, 080h, 080h, 080h
  db 07fh, 080h, 080h, 080h
  db 07fh, 080h, 080h, 080h
  db 07fh, 080h, 080h, 080h
  db 07fh, 080h, 080h, 080h
  db 0ffh, 000h, 000h, 000h
  db 0ffh, 000h, 000h, 000h
  db 0ffh, 000h, 000h, 000h
  db 0ffh, 000h, 000h, 000h
  db 0ffh, 000h, 000h, 000h

ball3_p0_c3_2:
; Xoffset: 2
; Mask: [B R G]
; OutSizeX: 24 OutSizeY: 16
; DataOrder: Vertical

  db 183  ; clipy(byte) 200-sizey-1
  db 010h  ; DrawType (Plane: BRG SizeY: 16)
  db 38  ; clipx(right) 40-sizex+1
  db 62  ; clipx(left) 64-sizex+1
  db 3  ; sizex(byte)
  db 64  ; sizey(pitch)

; 0(x)
  db 0feh, 001h, 001h, 001h
  db 0f8h, 007h, 007h, 007h
  db 0f0h, 00fh, 00fh, 00fh
  db 0e0h, 01fh, 01fh, 01fh
  db 0e0h, 01dh, 01fh, 01ch
  db 0c0h, 038h, 03fh, 038h
  db 0c0h, 030h, 03fh, 030h
  db 0c0h, 020h, 03fh, 020h
  db 0c0h, 03ch, 03ch, 03fh
  db 0c0h, 03ch, 03ch, 03fh
  db 0c0h, 03dh, 03ch, 03fh
  db 0e0h, 01fh, 01ch, 01fh
  db 0e0h, 01fh, 01eh, 01fh
  db 0f0h, 00fh, 00fh, 00fh
  db 0f8h, 007h, 007h, 007h
  db 0feh, 001h, 001h, 001h
; 1(x)
  db 007h, 0f8h, 0f8h, 0f8h
  db 001h, 0feh, 0deh, 0feh
  db 000h, 0ffh, 0cfh, 0ffh
  db 000h, 0ffh, 0c7h, 0ffh
  db 000h, 0ffh, 0c3h, 03fh
  db 000h, 0fbh, 0c3h, 03fh
  db 000h, 073h, 0c3h, 03fh
  db 000h, 023h, 0c3h, 03fh
  db 000h, 040h, 03fh, 0c0h
  db 000h, 0e0h, 03fh, 0c0h
  db 000h, 0f1h, 03fh, 0c1h
  db 000h, 0fbh, 03fh, 0c3h
  db 000h, 0ffh, 03fh, 0ffh
  db 000h, 0ffh, 03fh, 0ffh
  db 001h, 0feh, 0beh, 0feh
  db 007h, 0f8h, 0f8h, 0f8h
; 2(x)
  db 0ffh, 000h, 000h, 000h
  db 0ffh, 000h, 000h, 000h
  db 0ffh, 000h, 000h, 000h
  db 07fh, 080h, 080h, 080h
  db 07fh, 080h, 080h, 080h
  db 03fh, 0c0h, 0c0h, 0c0h
  db 03fh, 0c0h, 0c0h, 0c0h
  db 03fh, 0c0h, 0c0h, 0c0h
  db 03fh, 040h, 0c0h, 040h
  db 03fh, 0c0h, 0c0h, 0c0h
  db 03fh, 0c0h, 0c0h, 0c0h
  db 07fh, 080h, 080h, 080h
  db 07fh, 080h, 080h, 080h
  db 0ffh, 000h, 000h, 000h
  db 0ffh, 000h, 000h, 000h
  db 0ffh, 000h, 000h, 000h

ball3_p0_c3_3:
; Xoffset: 3
; Mask: [B R G]
; OutSizeX: 24 OutSizeY: 16
; DataOrder: Vertical

  db 183  ; clipy(byte) 200-sizey-1
  db 010h  ; DrawType (Plane: BRG SizeY: 16)
  db 38  ; clipx(right) 40-sizex+1
  db 62  ; clipx(left) 64-sizex+1
  db 3  ; sizex(byte)
  db 64  ; sizey(pitch)

; 0(x)
  db 0ffh, 000h, 000h, 000h
  db 0fch, 003h, 003h, 003h
  db 0f8h, 007h, 007h, 007h
  db 0f0h, 00fh, 00fh, 00fh
  db 0f0h, 00eh, 00fh, 00eh
  db 0e0h, 01ch, 01fh, 01ch
  db 0e0h, 018h, 01fh, 018h
  db 0e0h, 010h, 01fh, 010h
  db 0e0h, 01eh, 01eh, 01fh
  db 0e0h, 01eh, 01eh, 01fh
  db 0e0h, 01eh, 01eh, 01fh
  db 0f0h, 00fh, 00eh, 00fh
  db 0f0h, 00fh, 00fh, 00fh
  db 0f8h, 007h, 007h, 007h
  db 0fch, 003h, 003h, 003h
  db 0ffh, 000h, 000h, 000h
; 1(x)
  db 003h, 0fch, 0fch, 0fch
  db 000h, 0ffh, 0efh, 0ffh
  db 000h, 0ffh, 0e7h, 0ffh
  db 000h, 0ffh, 0e3h, 0ffh
  db 000h, 0ffh, 0e1h, 01fh
  db 000h, 07dh, 0e1h, 01fh
  db 000h, 039h, 0e1h, 01fh
  db 000h, 011h, 0e1h, 01fh
  db 000h, 020h, 01fh, 0e0h
  db 000h, 070h, 01fh, 0e0h
  db 000h, 0f8h, 01fh, 0e0h
  db 000h, 0fdh, 01fh, 0e1h
  db 000h, 0ffh, 01fh, 0ffh
  db 000h, 0ffh, 09fh, 0ffh
  db 000h, 0ffh, 0dfh, 0ffh
  db 003h, 0fch, 0fch, 0fch
; 2(x)
  db 0ffh, 000h, 000h, 000h
  db 0ffh, 000h, 000h, 000h
  db 07fh, 080h, 080h, 080h
  db 03fh, 0c0h, 0c0h, 0c0h
  db 03fh, 0c0h, 0c0h, 0c0h
  db 01fh, 0e0h, 0e0h, 0e0h
  db 01fh, 0e0h, 0e0h, 0e0h
  db 01fh, 0e0h, 0e0h, 0e0h
  db 01fh, 020h, 0e0h, 020h
  db 01fh, 060h, 0e0h, 060h
  db 01fh, 0e0h, 0e0h, 0e0h
  db 03fh, 0c0h, 0c0h, 0c0h
  db 03fh, 0c0h, 0c0h, 0c0h
  db 07fh, 080h, 080h, 080h
  db 0ffh, 000h, 000h, 000h
  db 0ffh, 000h, 000h, 000h

ball3_p0_c3_4:
; Xoffset: 4
; Mask: [B R G]
; OutSizeX: 24 OutSizeY: 16
; DataOrder: Vertical

  db 183  ; clipy(byte) 200-sizey-1
  db 010h  ; DrawType (Plane: BRG SizeY: 16)
  db 38  ; clipx(right) 40-sizex+1
  db 62  ; clipx(left) 64-sizex+1
  db 3  ; sizex(byte)
  db 64  ; sizey(pitch)

; 0(x)
  db 0ffh, 000h, 000h, 000h
  db 0feh, 001h, 001h, 001h
  db 0fch, 003h, 003h, 003h
  db 0f8h, 007h, 007h, 007h
  db 0f8h, 007h, 007h, 007h
  db 0f0h, 00eh, 00fh, 00eh
  db 0f0h, 00ch, 00fh, 00ch
  db 0f0h, 008h, 00fh, 008h
  db 0f0h, 00fh, 00fh, 00fh
  db 0f0h, 00fh, 00fh, 00fh
  db 0f0h, 00fh, 00fh, 00fh
  db 0f8h, 007h, 007h, 007h
  db 0f8h, 007h, 007h, 007h
  db 0fch, 003h, 003h, 003h
  db 0feh, 001h, 001h, 001h
  db 0ffh, 000h, 000h, 000h
; 1(x)
  db 081h, 07eh, 07eh, 07eh
  db 000h, 0ffh, 0f7h, 0ffh
  db 000h, 0ffh, 0f3h, 0ffh
  db 000h, 0ffh, 0f1h, 0ffh
  db 000h, 07fh, 0f0h, 00fh
  db 000h, 03eh, 0f0h, 00fh
  db 000h, 01ch, 0f0h, 00fh
  db 000h, 008h, 0f0h, 00fh
  db 000h, 010h, 00fh, 0f0h
  db 000h, 038h, 00fh, 0f0h
  db 000h, 07ch, 00fh, 0f0h
  db 000h, 0feh, 00fh, 0f0h
  db 000h, 0ffh, 08fh, 0ffh
  db 000h, 0ffh, 0cfh, 0ffh
  db 000h, 0ffh, 0efh, 0ffh
  db 081h, 07eh, 07eh, 07eh
; 2(x)
  db 0ffh, 000h, 000h, 000h
  db 07fh, 080h, 080h, 080h
  db 03fh, 0c0h, 0c0h, 0c0h
  db 01fh, 0e0h, 0e0h, 0e0h
  db 01fh, 0e0h, 0e0h, 0e0h
  db 00fh, 0f0h, 0f0h, 0f0h
  db 00fh, 0f0h, 0f0h, 0f0h
  db 00fh, 0f0h, 0f0h, 0f0h
  db 00fh, 010h, 0f0h, 010h
  db 00fh, 030h, 0f0h, 030h
  db 00fh, 070h, 0f0h, 070h
  db 01fh, 0e0h, 0e0h, 0e0h
  db 01fh, 0e0h, 0e0h, 0e0h
  db 03fh, 0c0h, 0c0h, 0c0h
  db 07fh, 080h, 080h, 080h
  db 0ffh, 000h, 000h, 000h

ball3_p0_c3_5:
; Xoffset: 5
; Mask: [B R G]
; OutSizeX: 24 OutSizeY: 16
; DataOrder: Vertical

  db 183  ; clipy(byte) 200-sizey-1
  db 010h  ; DrawType (Plane: BRG SizeY: 16)
  db 38  ; clipx(right) 40-sizex+1
  db 62  ; clipx(left) 64-sizex+1
  db 3  ; sizex(byte)
  db 64  ; sizey(pitch)

; 0(x)
  db 0ffh, 000h, 000h, 000h
  db 0ffh, 000h, 000h, 000h
  db 0feh, 001h, 001h, 001h
  db 0fch, 003h, 003h, 003h
  db 0fch, 003h, 003h, 003h
  db 0f8h, 007h, 007h, 007h
  db 0f8h, 006h, 007h, 006h
  db 0f8h, 004h, 007h, 004h
  db 0f8h, 007h, 007h, 007h
  db 0f8h, 007h, 007h, 007h
  db 0f8h, 007h, 007h, 007h
  db 0fch, 003h, 003h, 003h
  db 0fch, 003h, 003h, 003h
  db 0feh, 001h, 001h, 001h
  db 0ffh, 000h, 000h, 000h
  db 0ffh, 000h, 000h, 000h
; 1(x)
  db 0c0h, 03fh, 03fh, 03fh
  db 000h, 0ffh, 0fbh, 0ffh
  db 000h, 0ffh, 0f9h, 0ffh
  db 000h, 0ffh, 0f8h, 0ffh
  db 000h, 0bfh, 0f8h, 087h
  db 000h, 01fh, 0f8h, 007h
  db 000h, 00eh, 0f8h, 007h
  db 000h, 004h, 0f8h, 007h
  db 000h, 088h, 087h, 0f8h
  db 000h, 09ch, 087h, 0f8h
  db 000h, 0beh, 087h, 0f8h
  db 000h, 0ffh, 087h, 0f8h
  db 000h, 0ffh, 0c7h, 0ffh
  db 000h, 0ffh, 0e7h, 0ffh
  db 000h, 0ffh, 0f7h, 0ffh
  db 0c0h, 03fh, 03fh, 03fh
; 2(x)
  db 0ffh, 000h, 000h, 000h
  db 03fh, 0c0h, 0c0h, 0c0h
  db 01fh, 0e0h, 0e0h, 0e0h
  db 00fh, 0f0h, 0f0h, 0f0h
  db 00fh, 0f0h, 070h, 0f0h
  db 007h, 078h, 078h, 0f8h
  db 007h, 078h, 078h, 0f8h
  db 007h, 078h, 078h, 0f8h
  db 007h, 008h, 0f8h, 008h
  db 007h, 018h, 0f8h, 018h
  db 007h, 038h, 0f8h, 038h
  db 00fh, 070h, 0f0h, 070h
  db 00fh, 0f0h, 0f0h, 0f0h
  db 01fh, 0e0h, 0e0h, 0e0h
  db 03fh, 0c0h, 0c0h, 0c0h
  db 0ffh, 000h, 000h, 000h

ball3_p0_c3_6:
; Xoffset: 6
; Mask: [B R G]
; OutSizeX: 24 OutSizeY: 16
; DataOrder: Vertical

  db 183  ; clipy(byte) 200-sizey-1
  db 010h  ; DrawType (Plane: BRG SizeY: 16)
  db 38  ; clipx(right) 40-sizex+1
  db 62  ; clipx(left) 64-sizex+1
  db 3  ; sizex(byte)
  db 64  ; sizey(pitch)

; 0(x)
  db 0ffh, 000h, 000h, 000h
  db 0ffh, 000h, 000h, 000h
  db 0ffh, 000h, 000h, 000h
  db 0feh, 001h, 001h, 001h
  db 0feh, 001h, 001h, 001h
  db 0fch, 003h, 003h, 003h
  db 0fch, 003h, 003h, 003h
  db 0fch, 002h, 003h, 002h
  db 0fch, 003h, 003h, 003h
  db 0fch, 003h, 003h, 003h
  db 0fch, 003h, 003h, 003h
  db 0feh, 001h, 001h, 001h
  db 0feh, 001h, 001h, 001h
  db 0ffh, 000h, 000h, 000h
  db 0ffh, 000h, 000h, 000h
  db 0ffh, 000h, 000h, 000h
; 1(x)
  db 0e0h, 01fh, 01fh, 01fh
  db 080h, 07fh, 07dh, 07fh
  db 000h, 0ffh, 0fch, 0ffh
  db 000h, 0ffh, 0fch, 0ffh
  db 000h, 0dfh, 0fch, 0c3h
  db 000h, 08fh, 0fch, 083h
  db 000h, 007h, 0fch, 003h
  db 000h, 002h, 0fch, 003h
  db 000h, 0c4h, 0c3h, 0fch
  db 000h, 0ceh, 0c3h, 0fch
  db 000h, 0dfh, 0c3h, 0fch
  db 000h, 0ffh, 0c3h, 0fch
  db 000h, 0ffh, 0e3h, 0ffh
  db 000h, 0ffh, 0f3h, 0ffh
  db 080h, 07fh, 07bh, 07fh
  db 0e0h, 01fh, 01fh, 01fh
; 2(x)
  db 07fh, 080h, 080h, 080h
  db 01fh, 0e0h, 0e0h, 0e0h
  db 00fh, 0f0h, 0f0h, 0f0h
  db 007h, 0f8h, 078h, 0f8h
  db 007h, 0f8h, 038h, 0f8h
  db 003h, 0bch, 03ch, 0fch
  db 003h, 03ch, 03ch, 0fch
  db 003h, 03ch, 03ch, 0fch
  db 003h, 004h, 0fch, 004h
  db 003h, 00ch, 0fch, 00ch
  db 003h, 01ch, 0fch, 01ch
  db 007h, 0b8h, 0f8h, 038h
  db 007h, 0f8h, 0f8h, 0f8h
  db 00fh, 0f0h, 0f0h, 0f0h
  db 01fh, 0e0h, 0e0h, 0e0h
  db 07fh, 080h, 080h, 080h

ball3_p0_c3_7:
; Xoffset: 7
; Mask: [B R G]
; OutSizeX: 24 OutSizeY: 16
; DataOrder: Vertical

  db 183  ; clipy(byte) 200-sizey-1
  db 010h  ; DrawType (Plane: BRG SizeY: 16)
  db 38  ; clipx(right) 40-sizex+1
  db 62  ; clipx(left) 64-sizex+1
  db 3  ; sizex(byte)
  db 64  ; sizey(pitch)

; 0(x)
  db 0ffh, 000h, 000h, 000h
  db 0ffh, 000h, 000h, 000h
  db 0ffh, 000h, 000h, 000h
  db 0ffh, 000h, 000h, 000h
  db 0ffh, 000h, 000h, 000h
  db 0feh, 001h, 001h, 001h
  db 0feh, 001h, 001h, 001h
  db 0feh, 001h, 001h, 001h
  db 0feh, 001h, 001h, 001h
  db 0feh, 001h, 001h, 001h
  db 0feh, 001h, 001h, 001h
  db 0ffh, 000h, 000h, 000h
  db 0ffh, 000h, 000h, 000h
  db 0ffh, 000h, 000h, 000h
  db 0ffh, 000h, 000h, 000h
  db 0ffh, 000h, 000h, 000h
; 1(x)
  db 0f0h, 00fh, 00fh, 00fh
  db 0c0h, 03fh, 03eh, 03fh
  db 080h, 07fh, 07eh, 07fh
  db 000h, 0ffh, 0feh, 0ffh
  db 000h, 0efh, 0feh, 0e1h
  db 000h, 0c7h, 0feh, 0c1h
  db 000h, 083h, 0feh, 081h
  db 000h, 001h, 0feh, 001h
  db 000h, 0e2h, 0e1h, 0feh
  db 000h, 0e7h, 0e1h, 0feh
  db 000h, 0efh, 0e1h, 0feh
  db 000h, 0ffh, 0e1h, 0feh
  db 000h, 0ffh, 0f1h, 0ffh
  db 080h, 07fh, 079h, 07fh
  db 0c0h, 03fh, 03dh, 03fh
  db 0f0h, 00fh, 00fh, 00fh
; 2(x)
  db 03fh, 0c0h, 0c0h, 0c0h
  db 00fh, 0f0h, 0f0h, 0f0h
  db 007h, 0f8h, 078h, 0f8h
  db 003h, 0fch, 03ch, 0fch
  db 003h, 0fch, 01ch, 0fch
  db 001h, 0deh, 01eh, 0feh
  db 001h, 09eh, 01eh, 0feh
  db 001h, 01eh, 01eh, 0feh
  db 001h, 002h, 0feh, 002h
  db 001h, 006h, 0feh, 006h
  db 001h, 08eh, 0feh, 00eh
  db 003h, 0dch, 0fch, 01ch
  db 003h, 0fch, 0fch, 0fch
  db 007h, 0f8h, 0f8h, 0f8h
  db 00fh, 0f0h, 0f0h, 0f0h
  db 03fh, 0c0h, 0c0h, 0c0h

; PSGのサウンドデータ(ヒドさがウリの曲)
SOUNDDATA:
_26:
    DB  0
    DW  _26_TRK1
    DW  _26_TRK2
    DW  _26_TRK3
_26_TRK1:
    DB  217, %10, 209, 24, 14, 200, 0, 14, 209, 24, 28, 28, 14, 200, 0, 14
    DB  209, 28, 28, 24, 14, 200, 0, 14, 209, 24, 28, 19, 14, 200, 0, 14
    DB  209, 19, 28, 24, 14, 200, 0, 14, 209, 24, 14, 210, 24, 14, 209, 28
    DB  14, 200, 0, 14, 209, 28, 28, 31, 14, 200, 0, 14, 209, 31, 84
    DB  254
_26_TRK2:
    DB  217, %10, 212, 36, 14, 38, 14, 40, 14, 43, 14, 40, 28, 36, 56, 40
    DB  28, 36, 42, 200, 0, 14, 212, 36, 14, 38, 14, 40, 14, 43, 14, 46
    DB  28, 48, 28, 46, 28, 43, 70, 200, 0, 14
    DB  254
_26_TRK3:
    DB  217, %01, 205, 216, 8, 60, 14, 200, 216, 40, 0, 14, 205, 216, 8, 60
    DB  14, 200, 216, 40, 0, 14, 205, 216, 8, 60, 14, 200, 216, 40, 0, 14
    DB  205, 216, 8, 60, 14, 200, 216, 40, 0, 14, 205, 216, 8, 60, 14, 200
    DB  216, 40, 0, 14, 205, 216, 8, 60, 14, 200, 216, 40, 0, 14, 205, 216
    DB  8, 60, 14, 200, 216, 40, 0, 14, 205, 216, 8, 60, 14, 200, 216, 40
    DB  0, 14, 205, 216, 8, 60, 14, 200, 216, 40, 0, 14, 205, 216, 8, 60
    DB  14, 200, 216, 40, 0, 14, 205, 216, 8, 60, 14, 200, 216, 40, 0, 14
    DB  205, 216, 8, 60, 14, 200, 216, 40, 0, 14, 205, 216, 8, 60, 14, 200
    DB  216, 40, 0, 14, 205, 216, 8, 60, 14, 200, 216, 40, 0, 14, 205, 216
    DB  8, 60, 14, 200, 216, 40, 0, 14, 205, 216, 8, 60, 14, 200, 216, 40
    DB  0, 14
    DB  254
"

TATR_FILL:
; function TATR_FILL
	PUSH	IY
	LD	HL,(X2)
	LD	HL,$0001
	PUSH	HL
	POP	DE
	EX	DE,HL
	OR	A
	SBC	HL,DE
	LD	(X2),HL
	LD	HL,(Y2)
	LD	HL,$0001
	PUSH	HL
	POP	DE
	EX	DE,HL
	OR	A
	SBC	HL,DE
	LD	(Y2),HL
	LD	HL,(Y1)
	LD	(J),HL
_L0:
	LD	HL,(X1)
	LD	(I),HL
_L3:
	LD	HL,(ATR)
	LD	HL,(PORT)
	LD	HL,$2800
	LD	HL,(I)
	PUSH	HL
	POP	DE
	EX	DE,HL
	ADD	HL,DE
	LD	HL,(J)
	LD	HL,(WD)
	PUSH	HL
	POP	DE
	EX	DE,HL
	CALL	MUL16
	PUSH	HL
	POP	DE
	EX	DE,HL
	ADD	HL,DE
; array store: t9[t16] = t8
	PUSH	HL
	POP	DE
	LD	(HL),E
	INC	HL
	LD	(HL),D
	LD	HL,(ATR)
	LD	HL,(PORT)
	LD	HL,$2800
	LD	HL,(I)
	PUSH	HL
	POP	DE
	EX	DE,HL
	ADD	HL,DE
	LD	HL,(J)
	LD	HL,(WD)
	PUSH	HL
	POP	DE
	EX	DE,HL
	CALL	MUL16
	PUSH	HL
	POP	DE
	EX	DE,HL
	ADD	HL,DE
	LD	HL,$0400
	PUSH	HL
	POP	DE
	EX	DE,HL
	ADD	HL,DE
; array store: t18[t27] = t17
	PUSH	HL
	POP	DE
	LD	(HL),E
	INC	HL
	LD	(HL),D
_L4:
	LD	HL,(I)
	LD	HL,$0001
	PUSH	HL
	POP	DE
	EX	DE,HL
	ADD	HL,DE
	LD	(I),HL
	LD	HL,(X2)
	PUSH	HL
	POP	DE
	EX	DE,HL
	EX	DE,HL
	OR	A
	SBC	HL,DE
	LD	HL,$0000
	JR	C,$+3
	INC	HL
	LD	A,L
	XOR	$01
	LD	L,A
; if t32 != 0 goto _L3
	LD	A,H
	OR	L
	JP	NZ,_L3
_L5:
_L1:
	LD	HL,(J)
	LD	HL,$0001
	PUSH	HL
	POP	DE
	EX	DE,HL
	ADD	HL,DE
	LD	(J),HL
	LD	HL,(Y2)
	PUSH	HL
	POP	DE
	EX	DE,HL
	EX	DE,HL
	OR	A
	SBC	HL,DE
	LD	HL,$0000
	JR	C,$+3
	INC	HL
	LD	A,L
	XOR	$01
	LD	L,A
; if t37 != 0 goto _L0
	LD	A,H
	OR	L
	JP	NZ,_L0
_L2:
_EXIT:
	POP	IY
	RET

SGL_TFILL:
; function SGL_TFILL
	PUSH	IY
; ARRAY MOJI[SLANGCompiler.Parser.Ast.BinaryExpr]
	LD	HL,(X2)
	LD	HL,$0001
	PUSH	HL
	POP	DE
	EX	DE,HL
	OR	A
	SBC	HL,DE
	LD	(X2),HL
	LD	HL,(Y2)
	LD	HL,$0001
	PUSH	HL
	POP	DE
	EX	DE,HL
	OR	A
	SBC	HL,DE
	LD	(Y2),HL
	LD	HL,(PAT)
	LD	HL,(MOJI)
	LD	HL,$0000
; array store: t7[t8] = t6
	PUSH	HL
	POP	DE
	LD	(HL),E
	INC	HL
	LD	(HL),D
	LD	HL,$0000
	LD	HL,(MOJI)
	LD	HL,$0001
; array store: t10[t11] = t9
	PUSH	HL
	POP	DE
	LD	(HL),E
	INC	HL
	LD	(HL),D
	LD	HL,(Y1)
	LD	(J),HL
_L6:
	LD	HL,(X1)
	LD	(I),HL
_L9:
	LD	HL,(I)
	PUSH	HL
	LD	HL,(J)
	PUSH	HL
	LD	HL,(MOJI)
	PUSH	HL
	CALL	SGL_PRINT2
_L10:
	LD	HL,(I)
	LD	HL,$0001
	PUSH	HL
	POP	DE
	EX	DE,HL
	ADD	HL,DE
	LD	(I),HL
	LD	HL,(X2)
	PUSH	HL
	POP	DE
	EX	DE,HL
	EX	DE,HL
	OR	A
	SBC	HL,DE
	LD	HL,$0000
	JR	C,$+3
	INC	HL
	LD	A,L
	XOR	$01
	LD	L,A
; if t22 != 0 goto _L9
	LD	A,H
	OR	L
	JP	NZ,_L9
_L11:
_L7:
	LD	HL,(J)
	LD	HL,$0001
	PUSH	HL
	POP	DE
	EX	DE,HL
	ADD	HL,DE
	LD	(J),HL
	LD	HL,(Y2)
	PUSH	HL
	POP	DE
	EX	DE,HL
	EX	DE,HL
	OR	A
	SBC	HL,DE
	LD	HL,$0000
	JR	C,$+3
	INC	HL
	LD	A,L
	XOR	$01
	LD	L,A
; if t27 != 0 goto _L6
	LD	A,H
	OR	L
	JP	NZ,_L6
_L8:
_EXIT:
	POP	IY
	RET

VSYNC_PROC:
; function VSYNC_PROC
	PUSH	IY
	CALL	PSG_PROC
_EXIT:
	POP	IY
	RET

MAIN:
; function MAIN
	PUSH	IY
	LD	HL,$0028
	PUSH	HL
	CALL	WIDTH
	CALL	SGL_INIT
	LD	HL,$0002
	PUSH	HL
	CALL	SGL_FPSMODE
	LD	HL,$002A
	PUSH	HL
	LD	HL,(BLOCKPCG)
	PUSH	HL
	CALL	PCGDEF
	LD	HL,$0000
	PUSH	HL
	LD	HL,$0000
	PUSH	HL
	LD	HL,$0028
	PUSH	HL
	LD	HL,$0019
	PUSH	HL
	LD	HL,$0027
	PUSH	HL
	CALL	TATR_FILL
	LD	HL,$0000
	PUSH	HL
	LD	HL,$0000
	PUSH	HL
	LD	HL,$0028
	PUSH	HL
	LD	HL,$0019
	PUSH	HL
	LD	HL,$002A
	PUSH	HL
	CALL	SGL_TFILL
	LD	HL,$0001
	PUSH	HL
	LD	HL,$0001
	PUSH	HL
	LD	HL,$0027
	PUSH	HL
	LD	HL,$0018
	PUSH	HL
	LD	HL,$0020
	PUSH	HL
	CALL	SGL_TFILL
	LD	HL,$0000
	LD	(Y),HL
_L12:
	LD	HL,$0001
	PUSH	HL
	LD	HL,$0003
	LD	HL,(Y)
	LD	HL,$0004
	PUSH	HL
	POP	DE
	EX	DE,HL
	CALL	MUL16
	PUSH	HL
	POP	DE
	EX	DE,HL
	ADD	HL,DE
	PUSH	HL
	LD	HL,$0027
	PUSH	HL
	LD	HL,$0003
	LD	HL,(Y)
	LD	HL,$0004
	PUSH	HL
	POP	DE
	EX	DE,HL
	CALL	MUL16
	PUSH	HL
	POP	DE
	EX	DE,HL
	ADD	HL,DE
	PUSH	HL
	LD	HL,$002A
	PUSH	HL
	CALL	SGL_TFILL
_L13:
	LD	HL,(Y)
	LD	HL,$0001
	PUSH	HL
	POP	DE
	EX	DE,HL
	ADD	HL,DE
	LD	(Y),HL
	LD	HL,$0005
	PUSH	HL
	POP	DE
	EX	DE,HL
	EX	DE,HL
	OR	A
	SBC	HL,DE
	LD	HL,$0000
	JR	C,$+3
	INC	HL
	LD	A,L
	XOR	$01
	LD	L,A
; if t45 != 0 goto _L12
	LD	A,H
	OR	L
	JP	NZ,_L12
_L14:
	LD	HL,$0002
	PUSH	HL
	LD	HL,$0001
	PUSH	HL
	LD	HL,$0002
	LD	HL,$000B
	PUSH	HL
	POP	DE
	EX	DE,HL
	ADD	HL,DE
	PUSH	HL
	LD	HL,$0001
	PUSH	HL
	LD	HL,$0007
	PUSH	HL
	CALL	TATR_FILL
	LD	HL,$0002
	PUSH	HL
	LD	HL,$0001
	PUSH	HL
; load string "X1 SGL DEMO"
	LD	HL,0 ; string placeholder
	PUSH	HL
	CALL	SGL_PRINT2
	LD	HL,$0001
	PUSH	HL
	LD	HL,(BALL1)
	PUSH	HL
	CALL	SGL_DEFPAT
	LD	HL,$0002
	PUSH	HL
	LD	HL,(BALL3)
	PUSH	HL
	CALL	SGL_DEFPAT
	LD	HL,$0002
	PUSH	HL
	LD	HL,$0002
	PUSH	HL
	CALL	SGL_SPRCREATE
	LD	(SPR1),HL
	LD	HL,$0001
	PUSH	HL
	LD	HL,$0002
	PUSH	HL
	CALL	SGL_SPRCREATE
	LD	(SPR2),HL
	LD	HL,$0001
	PUSH	HL
	LD	HL,$0002
	PUSH	HL
	CALL	SGL_SPRCREATE
	LD	(SPR3),HL
	LD	HL,$00A0
	LD	HL,$0007
	PUSH	HL
	POP	DE
	EX	DE,HL
	CALL	SHL16
	LD	(X),HL
	LD	HL,$005A
	LD	HL,$0008
	PUSH	HL
	POP	DE
	EX	DE,HL
	CALL	SHL16
	LD	(Y),HL
	LD	HL,(SPR2)
	PUSH	HL
	LD	HL,(X)
	PUSH	HL
	LD	HL,(Y)
	PUSH	HL
	CALL	SGL_SPRMOVE
	LD	HL,(SPR3)
	PUSH	HL
	LD	HL,(X)
	PUSH	HL
	LD	HL,$006E
	LD	HL,$0008
	PUSH	HL
	POP	DE
	EX	DE,HL
	CALL	SHL16
	PUSH	HL
	CALL	SGL_SPRMOVE
	LD	HL,$0096
	LD	HL,$0008
	PUSH	HL
	POP	DE
	EX	DE,HL
	CALL	SHL16
	LD	(Y2),HL
	LD	HL,$0001
	LD	HL,(SPRDAT)
	LD	HL,$0000
; array store: t93[t94] = t92
	PUSH	HL
	POP	DE
	LD	(HL),E
	INC	HL
	LD	(HL),D
	LD	HL,$0001
	LD	HL,$0002
	PUSH	HL
	POP	DE
	EX	DE,HL
	CALL	MUL16
	LD	HL,(SPRDAT)
	LD	HL,$0001
; array store: t98[t99] = t97
	PUSH	HL
	POP	DE
	LD	(HL),E
	INC	HL
	LD	(HL),D
	LD	HL,$0064
	LD	HL,$0007
	PUSH	HL
	POP	DE
	EX	DE,HL
	CALL	SHL16
	LD	HL,(MEMW)
	LD	HL,(SPRDAT)
	LD	HL,$0003
	PUSH	HL
	POP	DE
	EX	DE,HL
	ADD	HL,DE
; array store: t103[t106] = t102
	PUSH	HL
	POP	DE
	LD	(HL),E
	INC	HL
	LD	(HL),D
	LD	HL,(Y2)
	LD	HL,(MEMW)
	LD	HL,(SPRDAT)
	LD	HL,$0006
	PUSH	HL
	POP	DE
	EX	DE,HL
	ADD	HL,DE
; array store: t108[t111] = t107
	PUSH	HL
	POP	DE
	LD	(HL),E
	INC	HL
	LD	(HL),D
	LD	HL,(SPR2)
	PUSH	HL
	LD	HL,(SPRDAT)
	PUSH	HL
	CALL	SGL_SPRSET
	LD	HL,$0000
	PUSH	HL
	CALL	PSG_INIT
	LD	HL,(MUSADR)
	PUSH	HL
	CALL	PSG_PLAY
_L15:
	LD	HL,(SPR1)
	PUSH	HL
	LD	HL,(X)
	PUSH	HL
	LD	HL,(Y)
	PUSH	HL
	CALL	SGL_SPRMOVE
	LD	HL,(SPR2)
	PUSH	HL
	LD	HL,$0064
	LD	HL,$0007
	PUSH	HL
	POP	DE
	EX	DE,HL
	CALL	SHL16
	PUSH	HL
	LD	HL,(Y2)
	PUSH	HL
	CALL	SGL_SPRMOVE
	LD	HL,(SPR1)
	PUSH	HL
	LD	HL,$0001
	LD	HL,(X)
	LD	HL,$000A
	PUSH	HL
	POP	DE
	EX	DE,HL
	CALL	SHR16
	LD	HL,$0001
	PUSH	HL
	POP	DE
	EX	DE,HL
	LD	A,H
	AND	D
	LD	H,A
	LD	A,L
	AND	E
	LD	L,A
	PUSH	HL
	POP	DE
	EX	DE,HL
	ADD	HL,DE
	PUSH	HL
	CALL	SGL_SPRPAT
	LD	HL,(SPR3)
	PUSH	HL
	LD	HL,(X)
	LD	HL,$000B
	PUSH	HL
	POP	DE
	EX	DE,HL
	CALL	SHR16
	LD	HL,$0001
	PUSH	HL
	POP	DE
	EX	DE,HL
	LD	A,H
	AND	D
	LD	H,A
	LD	A,L
	AND	E
	LD	L,A
	PUSH	HL
	CALL	SGL_SPRDISP
	LD	HL,(X)
	LD	HL,$0001
	LD	HL,$0007
	PUSH	HL
	POP	DE
	EX	DE,HL
	CALL	SHL16
	PUSH	HL
	POP	DE
	EX	DE,HL
	ADD	HL,DE
	LD	(X),HL
	LD	HL,(X)
	LD	HL,$0140
	LD	HL,$0007
	PUSH	HL
	POP	DE
	EX	DE,HL
	CALL	SHL16
	PUSH	HL
	POP	DE
	EX	DE,HL
	OR	A
	SBC	HL,DE
	LD	HL,$0000
	JR	NC,$+3
	INC	HL
; if t154 == 0 goto _L17
	LD	A,H
	OR	L
	JP	Z,_L17
	LD	HL,$0000
	LD	(X),HL
_L17:
	LD	HL,(Y2)
	LD	HL,$0003
	LD	HL,$0008
	PUSH	HL
	POP	DE
	EX	DE,HL
	CALL	SHL16
	PUSH	HL
	POP	DE
	EX	DE,HL
	ADD	HL,DE
	LD	(Y2),HL
	LD	HL,(Y2)
	LD	HL,$00C8
	LD	HL,$0008
	PUSH	HL
	POP	DE
	EX	DE,HL
	CALL	SHL16
	PUSH	HL
	POP	DE
	EX	DE,HL
	OR	A
	SBC	HL,DE
	LD	HL,$0000
	JR	NC,$+3
	INC	HL
; if t165 == 0 goto _L18
	LD	A,H
	OR	L
	JP	Z,_L18
	LD	HL,$0000
	LD	(Y2),HL
_L18:
	CALL	SGL_VSYNC
	JP	_L15
_L16:
_EXIT:
	POP	IY
	RET
