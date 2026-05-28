
CONST WD=40;

CONST BALL1 = ball_p0_c1;
CONST BALL3 = ball3_p0_c3;
CONST MUSADR = SOUNDDATA;

VAR X,Y,SPR1,SPR2;

CONST BLOCKPCG = [
    $00, $00, $00,
    $00, $F7, $00,
    $00, $F7, $00,
    $00, $F7, $00,
    $00, $00, $00,
    $00, $7F, $00,
    $00, $7F, $00,
    $00, $7F, $00
    ];

TATR_FILL(X1,Y1,X2,Y2,ATR)
VAR I,J;
BEGIN
    X2--;
    Y2--;

    FOR J=Y1 TO Y2
        FOR I=X1 TO X2
        {
            PORT[$2800+I+J*WD]=ATR;
            PORT[$2800+I+J*WD + $400]=ATR;
        }
END;

SGL_TFILL(X1,Y1,X2,Y2,PAT)
VAR I,J;
ARRAY BYTE MOJI[2-1];
{
    X2--;
    Y2--;
    MOJI[0] = PAT;
    MOJI[1] = 0;

    FOR J=Y1 TO Y2
        FOR I=X1 TO X2
        {
          SGL_PRINT2(I,J,MOJI);
        }
}

MAIN()
{
//  PSG_INIT(1);
//  PSG_PLAY(MUSADR);

  // 内部的にWIDTH 40にしておく必要がある
  WIDTH(40);

  // SGL初期化 
	SGL_INIT();

  // FPSMODE 0 = 60fps / 2 = 30fps / 4 = 20fps
  SGL_FPSMODE(0);

  // SET * PCG
  PCGDEF(42,BLOCKPCG);
  // SET PCG & COLOR 7(ALL)
  TATR_FILL(0,0,40,25,$27);
  SGL_TFILL(0,0,40,25,'*');
  SGL_TFILL(1,1,39,24,' ');
  FOR Y=0 TO 5
  {
    SGL_TFILL(1,3 + Y*4,39,3 + Y*4,'*');
  }

  TATR_FILL(2,1,2+11,1,$07);
  SGL_PRINT2(2,1,"X1 SGL DEMO");

	SGL_DEFPAT(1, BALL1);
	SGL_DEFPAT(2, BALL3);
	SPR1 = SGL_SPRCREATE(2, 2);
	SPR2 = SGL_SPRCREATE(1, 2);

	X = 160 << 7;
	Y = 90 << 8;
	SGL_SPRMOVE(SPR2,X,Y);
	LOOP {
		SGL_SPRMOVE(SPR1,X,Y);
		X += 1 << 7;
		IF X >= (320 << 7) THEN X = 0;
		SGL_VSYNC();
	}
}


#ASM

; データテーブル
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





SOUNDDATA:
; _05:
;     DB  0
;     DW  _05_TRK1
;     DW  _05_TRK2
;     DW  _05_TRK3
; _05_TRK1:
;     DB  217, %10, 212, 12, 5, 210, 12, 5, 212, 24, 5, 210, 24, 5, 212, 12
;     DB  5, 210, 12, 5, 212, 24, 5, 210, 24, 5, 212, 12, 5, 210, 12, 5
;     DB  212, 24, 5, 210, 24, 5, 212, 12, 5, 210, 12, 5, 212, 24, 5, 210
;     DB  24, 5, 212, 12, 5, 210, 12, 5, 212, 24, 5, 210, 24, 5, 212, 12
;     DB  5, 210, 12, 5, 212, 24, 5, 210, 24, 5, 212, 12, 5, 210, 12, 5
;     DB  212, 24, 5, 210, 24, 5, 212, 12, 5, 210, 12, 5, 212, 24, 5, 210
;     DB  24, 5, 212, 12, 5, 210, 12, 5, 212, 24, 5, 210, 24, 5, 212, 12
;     DB  5, 210, 12, 5, 212, 24, 5, 210, 24, 5, 212, 12, 5, 210, 12, 5
;     DB  212, 24, 5, 210, 24, 5, 212, 12, 5, 210, 12, 5, 212, 24, 5, 210
;     DB  24, 5, 212, 12, 5, 210, 12, 5, 212, 24, 5, 210, 24, 5, 212, 12
;     DB  5, 210, 12, 5, 212, 24, 5, 210, 24, 5, 212, 12, 5, 210, 12, 5
;     DB  212, 24, 5, 210, 24, 5, 212, 12, 5, 210, 12, 5, 212, 24, 5, 210
;     DB  24, 5, 253, 212, 12, 5, 210, 12, 5, 212, 24, 5, 210, 24, 5, 212
;     DB  12, 5, 210, 12, 5, 212, 24, 5, 210, 24, 5, 212, 12, 5, 210, 12
;     DB  5, 212, 24, 5, 210, 24, 5, 212, 12, 5, 210, 12, 5, 212, 24, 5
;     DB  210, 24, 5, 212, 12, 5, 210, 12, 5, 212, 24, 5, 210, 24, 5, 212
;     DB  12, 5, 210, 12, 5, 212, 24, 5, 210, 24, 5, 212, 12, 5, 210, 12
;     DB  5, 212, 24, 5, 210, 24, 5, 212, 12, 5, 210, 12, 5, 212, 24, 5
;     DB  210, 24, 5, 212, 12, 5, 210, 12, 5, 212, 24, 5, 210, 24, 5, 212
;     DB  12, 5, 210, 12, 5, 212, 24, 5, 210, 24, 5, 212, 12, 5, 210, 12
;     DB  5, 212, 24, 5, 210, 24, 5, 212, 12, 5, 210, 12, 5, 212, 24, 5
;     DB  210, 24, 5, 212, 12, 5, 210, 12, 5, 212, 24, 5, 210, 24, 5, 212
;     DB  12, 5, 210, 12, 5, 212, 24, 5, 210, 24, 5, 212, 12, 5, 210, 12
;     DB  5, 212, 24, 5, 210, 24, 5, 212, 12, 5, 210, 12, 5, 212, 24, 5
;     DB  210, 24, 5, 212, 19, 5, 210, 19, 5, 212, 31, 5, 210, 31, 5, 212
;     DB  19, 5, 210, 19, 5, 212, 31, 5, 210, 31, 5, 212, 19, 5, 210, 19
;     DB  5, 212, 31, 5, 210, 31, 5, 212, 19, 5, 210, 19, 5, 212, 31, 5
;     DB  210, 31, 5, 212, 19, 5, 210, 19, 5, 212, 31, 5, 210, 31, 5, 212
;     DB  19, 5, 210, 19, 5, 212, 31, 5, 210, 31, 5, 212, 19, 5, 210, 19
;     DB  5, 212, 31, 5, 210, 31, 5, 212, 19, 5, 210, 19, 5, 212, 31, 5
;     DB  210, 31, 5, 212, 17, 5, 210, 17, 5, 212, 29, 5, 210, 29, 5, 212
;     DB  17, 5, 210, 17, 5, 212, 29, 5, 210, 29, 5, 212, 17, 5, 210, 17
;     DB  5, 212, 29, 5, 210, 29, 5, 212, 17, 5, 210, 17, 5, 212, 29, 5
;     DB  210, 29, 5, 212, 17, 5, 210, 17, 5, 212, 29, 5, 210, 29, 5, 212
;     DB  17, 5, 210, 17, 5, 212, 29, 5, 210, 29, 5, 212, 17, 5, 210, 17
;     DB  5, 212, 29, 5, 210, 29, 5, 212, 17, 5, 210, 17, 5, 212, 29, 5
;     DB  210, 29, 5, 212, 12, 5, 208, 12, 5, 212, 12, 5, 208, 12, 5, 212
;     DB  12, 5, 208, 12, 5, 212, 24, 5, 210, 24, 5, 212, 12, 5, 208, 12
;     DB  5, 212, 12, 5, 208, 12, 5, 212, 12, 5, 208, 12, 5, 212, 24, 5
;     DB  210, 24, 5, 212, 12, 5, 208, 12, 5, 212, 12, 5, 208, 12, 5, 212
;     DB  12, 5, 208, 12, 5, 212, 24, 5, 210, 24, 5, 212, 12, 5, 208, 12
;     DB  5, 212, 12, 5, 208, 12, 5, 212, 12, 5, 208, 12, 5, 212, 24, 5
;     DB  210, 24, 5, 212, 12, 5, 208, 12, 5, 212, 12, 5, 208, 12, 5, 212
;     DB  12, 5, 208, 12, 5, 212, 24, 5, 210, 24, 5, 212, 12, 5, 208, 12
;     DB  5, 212, 12, 5, 208, 12, 5, 212, 12, 5, 208, 12, 5, 212, 24, 5
;     DB  210, 24, 5, 212, 12, 5, 208, 12, 5, 212, 12, 5, 208, 12, 5, 212
;     DB  12, 5, 208, 12, 5, 212, 24, 5, 210, 24, 5, 212, 12, 5, 208, 12
;     DB  5, 212, 12, 5, 208, 12, 5, 212, 12, 5, 208, 12, 5, 212, 24, 5
;     DB  210, 24, 5, 212, 19, 5, 210, 19, 5, 212, 31, 5, 210, 31, 5, 212
;     DB  19, 5, 210, 19, 5, 212, 31, 5, 210, 31, 5, 212, 19, 5, 210, 19
;     DB  5, 212, 31, 5, 210, 31, 5, 212, 19, 5, 210, 19, 5, 212, 31, 5
;     DB  210, 31, 5, 212, 19, 5, 210, 19, 5, 212, 31, 5, 210, 31, 5, 212
;     DB  19, 5, 210, 19, 5, 212, 31, 5, 210, 31, 5, 212, 19, 5, 210, 19
;     DB  5, 212, 31, 5, 210, 31, 5, 212, 19, 5, 210, 19, 5, 212, 31, 5
;     DB  210, 31, 5, 212, 17, 5, 210, 17, 5, 212, 29, 5, 210, 29, 5, 212
;     DB  17, 5, 210, 17, 5, 212, 29, 5, 210, 29, 5, 212, 17, 5, 210, 17
;     DB  5, 212, 29, 5, 210, 29, 5, 212, 17, 5, 210, 17, 5, 212, 29, 5
;     DB  210, 29, 5, 212, 17, 5, 210, 17, 5, 212, 29, 5, 210, 29, 5, 212
;     DB  17, 5, 210, 17, 5, 212, 29, 5, 210, 29, 5, 212, 14, 5, 210, 14
;     DB  5, 212, 26, 5, 210, 26, 5, 212, 14, 5, 210, 14, 5, 212, 26, 5
;     DB  210, 26, 5
;     DB  254
; _05_TRK2:
;     DB  200, 0, 10, 217, %10, 212, 63, 5, 208, 63, 5, 200, 0, 10, 212, 63
;     DB  5, 208, 63, 5, 200, 0, 10, 212, 60, 5, 208, 60, 5, 200, 0, 10
;     DB  212, 63, 5, 208, 63, 5, 200, 0, 70, 212, 63, 5, 208, 63, 5, 200
;     DB  0, 10, 212, 63, 5, 208, 63, 5, 200, 0, 10, 212, 63, 5, 208, 63
;     DB  5, 200, 0, 10, 212, 60, 5, 208, 60, 5, 200, 0, 10, 212, 63, 5
;     DB  208, 63, 5, 200, 0, 80, 253, 212, 36, 20, 210, 36, 5, 208, 36, 5
;     DB  212, 38, 5, 39, 5, 41, 20, 46, 20, 210, 43, 5, 212, 44, 15, 41
;     DB  20, 36, 20, 39, 20, 41, 55, 210, 41, 5, 208, 41, 5, 210, 39, 5
;     DB  212, 41, 5, 43, 5, 44, 20, 41, 20, 44, 20, 46, 20, 48, 30, 210
;     DB  48, 5, 208, 48, 5, 210, 55, 5, 212, 56, 5, 55, 10, 53, 10, 51
;     DB  10, 53, 20, 51, 10, 53, 20, 51, 10, 53, 10, 55, 10, 60, 5, 58
;     DB  5, 60, 70, 210, 60, 10, 208, 60, 10, 200, 0, 60, 212, 48, 55, 210
;     DB  48, 5, 208, 48, 5, 212, 46, 5, 48, 5, 50, 5, 46, 35, 210, 46
;     DB  5, 208, 46, 5, 200, 0, 35, 212, 44, 55, 210, 44, 5, 208, 44, 5
;     DB  212, 41, 5, 44, 5, 46, 5, 41, 30, 210, 41, 5, 208, 41, 5, 200
;     DB  0, 40, 212, 29, 5, 34, 5, 36, 25, 210, 36, 5, 212, 34, 5, 38
;     DB  5, 41, 25, 210, 41, 5, 212, 38, 5, 43, 5, 46, 5, 210, 46, 5
;     DB  212, 48, 10, 53, 5, 210, 53, 5, 200, 0, 10, 212, 50, 5, 210, 50
;     DB  5, 212, 48, 10, 46, 5, 210, 46, 5, 212, 48, 5, 46, 5, 48, 70
;     DB  210, 48, 20, 208, 48, 20, 212, 46, 30, 210, 46, 10
;     DB  254
; _05_TRK3:
;     DB  217, %01, 212, 216, 28, 9, 5, 200, 216, 40, 0, 5, 217, %10, 212, 67
;     DB  5, 208, 67, 5, 217, %01, 212, 216, 4, 72, 5, 206, 72, 5, 217, %10
;     DB  212, 68, 5, 208, 68, 5, 217, %01, 212, 216, 28, 9, 5, 200, 216, 40
;     DB  0, 5, 217, %10, 212, 65, 5, 208, 65, 5, 217, %01, 212, 216, 4, 72
;     DB  5, 206, 72, 5, 217, %10, 212, 67, 5, 208, 67, 5, 217, %01, 212, 216
;     DB  28, 9, 5, 200, 216, 40, 0, 15, 212, 216, 4, 72, 5, 206, 72, 5
;     DB  200, 216, 40, 0, 10, 212, 216, 28, 9, 5, 200, 216, 40, 0, 15, 212
;     DB  216, 4, 72, 5, 206, 72, 5, 217, %10, 212, 67, 5, 210, 67, 5, 217
;     DB  %01, 212, 216, 28, 9, 5, 200, 216, 40, 0, 5, 217, %10, 212, 67, 5
;     DB  208, 67, 5, 217, %01, 212, 216, 4, 72, 5, 206, 72, 5, 217, %10, 212
;     DB  68, 5, 208, 68, 5, 217, %01, 212, 216, 28, 9, 5, 200, 216, 40, 0
;     DB  5, 217, %10, 212, 65, 5, 208, 65, 5, 217, %01, 212, 216, 4, 72, 5
;     DB  206, 72, 5, 217, %10, 212, 67, 5, 208, 67, 5, 217, %01, 212, 216, 28
;     DB  9, 5, 200, 216, 40, 0, 15, 212, 216, 4, 72, 5, 206, 72, 5, 200
;     DB  216, 40, 0, 10, 212, 216, 4, 72, 5, 206, 72, 5, 212, 72, 5, 206
;     DB  72, 5, 212, 72, 5, 206, 72, 5, 212, 72, 5, 206, 72, 5, 253, 212
;     DB  216, 28, 9, 5, 200, 216, 40, 0, 15, 212, 216, 4, 72, 5, 206, 72
;     DB  5, 200, 216, 40, 0, 10, 212, 216, 28, 9, 5, 200, 216, 40, 0, 15
;     DB  212, 216, 4, 72, 5, 206, 72, 5, 200, 216, 40, 0, 10, 212, 216, 28
;     DB  9, 5, 200, 216, 40, 0, 15, 212, 216, 4, 72, 5, 206, 72, 5, 200
;     DB  216, 40, 0, 10, 212, 216, 28, 9, 5, 200, 216, 40, 0, 5, 212, 216
;     DB  28, 9, 5, 200, 216, 40, 0, 5, 212, 216, 4, 72, 5, 206, 72, 5
;     DB  200, 216, 40, 0, 10, 212, 216, 28, 9, 5, 200, 216, 40, 0, 15, 212
;     DB  216, 4, 72, 5, 206, 72, 5, 200, 216, 40, 0, 10, 212, 216, 28, 9
;     DB  5, 200, 216, 40, 0, 15, 212, 216, 4, 72, 5, 206, 72, 5, 200, 216
;     DB  40, 0, 10, 212, 216, 28, 9, 5, 200, 216, 40, 0, 15, 212, 216, 4
;     DB  72, 5, 206, 72, 5, 200, 216, 40, 0, 10, 212, 216, 28, 9, 5, 200
;     DB  216, 40, 0, 5, 212, 216, 28, 9, 5, 200, 216, 40, 0, 5, 212, 216
;     DB  4, 72, 5, 206, 72, 5, 200, 216, 40, 0, 10, 212, 216, 28, 9, 5
;     DB  200, 216, 40, 0, 15, 212, 216, 4, 72, 5, 206, 72, 5, 200, 216, 40
;     DB  0, 10, 212, 216, 28, 9, 5, 200, 216, 40, 0, 15, 212, 216, 4, 72
;     DB  5, 206, 72, 5, 200, 216, 40, 0, 10, 212, 216, 28, 9, 5, 200, 216
;     DB  40, 0, 15, 212, 216, 4, 72, 5, 206, 72, 5, 200, 216, 40, 0, 10
;     DB  212, 216, 28, 9, 5, 200, 216, 40, 0, 5, 212, 216, 28, 9, 5, 200
;     DB  216, 40, 0, 5, 212, 216, 4, 72, 5, 206, 72, 5, 200, 216, 40, 0
;     DB  10, 212, 216, 28, 9, 5, 200, 216, 40, 0, 15, 212, 216, 4, 72, 5
;     DB  206, 72, 5, 200, 216, 40, 0, 10, 212, 216, 28, 9, 5, 200, 216, 40
;     DB  0, 15, 212, 216, 4, 72, 5, 206, 72, 5, 200, 216, 40, 0, 10, 212
;     DB  216, 28, 9, 5, 200, 216, 40, 0, 15, 212, 216, 4, 72, 5, 206, 72
;     DB  5, 200, 216, 40, 0, 10, 212, 216, 4, 72, 5, 206, 72, 5, 212, 72
;     DB  5, 206, 72, 5, 212, 72, 5, 206, 72, 5, 212, 72, 5, 206, 72, 5
;     DB  212, 216, 28, 9, 5, 200, 216, 40, 0, 15, 212, 216, 4, 72, 5, 206
;     DB  72, 5, 200, 216, 40, 0, 10, 212, 216, 28, 9, 5, 200, 216, 40, 0
;     DB  15, 212, 216, 4, 72, 5, 206, 72, 5, 200, 216, 40, 0, 10, 212, 216
;     DB  28, 9, 5, 200, 216, 40, 0, 15, 212, 216, 4, 72, 5, 206, 72, 5
;     DB  200, 216, 40, 0, 10, 212, 216, 28, 9, 5, 200, 216, 40, 0, 5, 212
;     DB  216, 28, 9, 5, 200, 216, 40, 0, 5, 212, 216, 4, 72, 5, 206, 72
;     DB  5, 200, 216, 40, 0, 10, 212, 216, 28, 9, 5, 200, 216, 40, 0, 15
;     DB  212, 216, 4, 72, 5, 206, 72, 5, 200, 216, 40, 0, 10, 212, 216, 28
;     DB  9, 5, 200, 216, 40, 0, 15, 212, 216, 4, 72, 5, 206, 72, 5, 200
;     DB  216, 40, 0, 10, 212, 216, 28, 9, 5, 200, 216, 40, 0, 15, 212, 216
;     DB  4, 72, 5, 206, 72, 5, 200, 216, 40, 0, 10, 212, 216, 28, 9, 5
;     DB  200, 216, 40, 0, 5, 212, 216, 28, 9, 5, 200, 216, 40, 0, 5, 212
;     DB  216, 4, 72, 5, 206, 72, 5, 200, 216, 40, 0, 10, 212, 216, 28, 9
;     DB  5, 200, 216, 40, 0, 15, 212, 216, 4, 72, 5, 206, 72, 5, 200, 216
;     DB  40, 0, 10, 212, 216, 28, 9, 5, 200, 216, 40, 0, 15, 212, 216, 4
;     DB  72, 5, 206, 72, 5, 200, 216, 40, 0, 10, 212, 216, 28, 9, 5, 200
;     DB  216, 40, 0, 15, 212, 216, 4, 72, 5, 206, 72, 5, 200, 216, 40, 0
;     DB  10, 212, 216, 28, 9, 5, 200, 216, 40, 0, 5, 212, 216, 28, 9, 5
;     DB  200, 216, 40, 0, 5, 212, 216, 4, 72, 5, 206, 72, 5, 200, 216, 40
;     DB  0, 10, 212, 216, 28, 9, 5, 200, 216, 40, 0, 15, 212, 216, 4, 72
;     DB  5, 206, 72, 5, 200, 216, 40, 0, 10, 212, 216, 28, 9, 5, 200, 216
;     DB  40, 0, 15, 212, 216, 4, 72, 5, 206, 72, 5, 200, 216, 40, 0, 10
;     DB  212, 216, 28, 9, 5, 200, 216, 40, 0, 15, 212, 216, 4, 72, 5, 206
;     DB  72, 5, 200, 216, 40, 0, 10, 212, 216, 4, 72, 5, 206, 72, 5, 212
;     DB  72, 5, 206, 72, 5, 212, 72, 5, 206, 72, 5, 212, 72, 5, 206, 72
;     DB  5
;     DB  254

#END