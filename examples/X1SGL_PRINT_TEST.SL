// X1 SGL text 表示問題の切り分け試行 sample
// 既存 X1SGL.SL の SGL_PRINT2 (= 2 page 両方描画) を SGL_PRINT (= 1 page only、
// flip_render_w 経由で back buffer のみ描画) に置換。
//
// 動作確認目的:
//  - X1turbo で text/PCG 表示されるか?
//    - 出る → SGL_PRINT2 (= 2 page 書込) 固有の問題、 fix は render_text_2page
//    - 出ない → SGL の page flip 全体問題、 fix は flip_screen / SGL_VSYNC 周辺
//  - X1 normal は X1SGL.SL 同等で動作するはず
//
// 既存 X1SGL.SL からの差分:
//  - SGL_TFILL 内 SGL_PRINT2 → SGL_PRINT
//  - MAIN 内 SGL_PRINT2 → SGL_PRINT (X1 SGL DEMO 文字列)
//  - sprite + PSG + PCG 動作は全て keep

CONST WD=40;

CONST BALL1 = ball_p0_c1;
CONST BALL3 = ball3_p0_c3;
CONST MUSADR = SOUNDDATA;

VAR X,Y,Y2,SPR1,SPR2,SPR3;
ARRAY BYTE SPRDAT[16-1];

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

// テキストアトリビュートについて両ページとも指定の値で埋める
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

// テキストを 1 page only (= flip_render_w 経由 back buffer のみ) で埋める
// (= 元 X1SGL.SL は SGL_PRINT2 で 2 page 両方描画)
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
          // 切り分け: SGL_PRINT2 → SGL_PRINT (= 1 page only)
          SGL_PRINT(I,J,MOJI);
        }
}

VSYNC_PROC()
{
  PSG_PROC();
}

MAIN()
{
  WIDTH(40);
  SGL_INIT();
  SGL_FPSMODE(2);
  PCGDEF(42,BLOCKPCG);
  TATR_FILL(0,0,40,25,$27);

  SGL_TFILL(0,0,40,25,'*');
  SGL_TFILL(1,1,39,24,' ');
  FOR Y=0 TO 5
  {
    SGL_TFILL(1,3 + Y*4,39,3 + Y*4,'*');
  }
  TATR_FILL(2,1,2+11,1,$07);
  // 切り分け: SGL_PRINT2 → SGL_PRINT (= 1 page only)
  SGL_PRINT(2,1,"X1 SGL DEMO");

  SGL_DEFPAT(1, BALL1);
  SGL_DEFPAT(2, BALL3);
  SPR1 = SGL_SPRCREATE(2, 2);
  SPR2 = SGL_SPRCREATE(1, 2);
  SPR3 = SGL_SPRCREATE(1, 2);

  X = 160 << 7;
  Y = 90 << 8;
  SGL_SPRMOVE(SPR2,X,Y);
  SGL_SPRMOVE(SPR3,X,110 << 8);

  Y2 = 150 << 8;
  SPRDAT[0] = 1;
  SPRDAT[1] = 1*2;
  MEMW[SPRDAT + 3] = 100 << 7;
  MEMW[SPRDAT + 6] = Y2;
  SGL_SPRSET(SPR2,SPRDAT);

  PSG_INIT(0);
  PSG_PLAY(MUSADR);

  LOOP {
    SGL_SPRMOVE(SPR1,X,Y);
    SGL_SPRMOVE(SPR2,100 << 7, Y2);
    SGL_SPRPAT(SPR1,1 + ((X >> 10) AND 1));
    SGL_SPRDISP(SPR3,((X >> 11) AND 1));
    X += 1 << 7;
    IF X >= (320 << 7) THEN X = 0;
    Y2 += 3 << 8;
    IF Y2 >= (200 << 8) THEN Y2 = 0;

    SGL_VSYNC();
  }
}


#ASM
; X1 SGL のパターンデータテーブル (= X1SGL.SL と同一)
ball_p0_c1:
  db  0fch
  db  0f8h
  db  8
  dw  ball_p0_c1_0
  dw  ball_p0_c1_1
  dw  ball_p0_c1_2
  dw  ball_p0_c1_3
  dw  ball_p0_c1_4
  dw  ball_p0_c1_5
  dw  ball_p0_c1_6
  dw  ball_p0_c1_7

ball_p0_c1_0:
  db 183
  db 000h
  db 39
  db 63
  db 2
  db 32
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
  db 183
  db 000h
  db 38
  db 62
  db 3
  db 32
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
  db 183, 000h, 38, 62, 3, 32
  db 0feh, 001h, 0f8h, 007h, 0f0h, 00fh, 0e0h, 01ch, 0e0h, 018h
  db 0c0h, 039h, 0c0h, 033h, 0c0h, 021h, 0c0h, 021h, 0c0h, 033h
  db 0c0h, 039h, 0e0h, 018h, 0e0h, 01ch, 0f0h, 00fh, 0f8h, 007h, 0feh, 001h
  db 007h, 0f8h, 001h, 09eh, 000h, 00fh, 000h, 003h, 000h, 091h
  db 000h, 0f9h, 000h, 0fch, 000h, 0f8h, 000h, 0f8h, 000h, 0fch
  db 000h, 0f9h, 000h, 091h, 000h, 003h, 000h, 00fh, 001h, 09eh, 007h, 0f8h
  db 0ffh, 000h, 0ffh, 000h, 0ffh, 000h, 07fh, 080h, 07fh, 080h
  db 03fh, 0c0h, 03fh, 0c0h, 03fh, 040h, 03fh, 040h, 03fh, 0c0h
  db 03fh, 0c0h, 07fh, 080h, 07fh, 080h, 0ffh, 000h, 0ffh, 000h, 0ffh, 000h

ball_p0_c1_3:
  db 183, 000h, 38, 62, 3, 32
  db 0ffh, 000h, 0fch, 003h, 0f8h, 007h, 0f0h, 00eh, 0f0h, 00ch
  db 0e0h, 01ch, 0e0h, 019h, 0e0h, 010h, 0e0h, 010h, 0e0h, 019h
  db 0e0h, 01ch, 0f0h, 00ch, 0f0h, 00eh, 0f8h, 007h, 0fch, 003h, 0ffh, 000h
  db 003h, 0fch, 000h, 0cfh, 000h, 087h, 000h, 001h, 000h, 048h
  db 000h, 0fch, 000h, 0feh, 000h, 0fch, 000h, 0fch, 000h, 0feh
  db 000h, 0fch, 000h, 048h, 000h, 001h, 000h, 087h, 000h, 0cfh, 003h, 0fch
  db 0ffh, 000h, 0ffh, 000h, 07fh, 080h, 03fh, 0c0h, 03fh, 0c0h
  db 01fh, 0e0h, 01fh, 060h, 01fh, 020h, 01fh, 020h, 01fh, 060h
  db 01fh, 0e0h, 03fh, 0c0h, 03fh, 0c0h, 07fh, 080h, 0ffh, 000h, 0ffh, 000h

ball_p0_c1_4:
  db 183, 000h, 38, 62, 3, 32
  db 0ffh, 000h, 0feh, 001h, 0fch, 003h, 0f8h, 007h, 0f8h, 006h
  db 0f0h, 00eh, 0f0h, 00ch, 0f0h, 008h, 0f0h, 008h, 0f0h, 00ch
  db 0f0h, 00eh, 0f8h, 006h, 0f8h, 007h, 0fch, 003h, 0feh, 001h, 0ffh, 000h
  db 081h, 07eh, 000h, 0e7h, 000h, 0c3h, 000h, 000h, 000h, 024h
  db 000h, 07eh, 000h, 0ffh, 000h, 07eh, 000h, 07eh, 000h, 0ffh
  db 000h, 07eh, 000h, 024h, 000h, 000h, 000h, 0c3h, 000h, 0e7h, 081h, 07eh
  db 0ffh, 000h, 07fh, 080h, 03fh, 0c0h, 01fh, 0e0h, 01fh, 060h
  db 00fh, 070h, 00fh, 030h, 00fh, 010h, 00fh, 010h, 00fh, 030h
  db 00fh, 070h, 01fh, 060h, 01fh, 0e0h, 03fh, 0c0h, 07fh, 080h, 0ffh, 000h

ball_p0_c1_5:
  db 183, 000h, 38, 62, 3, 32
  db 0ffh, 000h, 0ffh, 000h, 0feh, 001h, 0fch, 003h, 0fch, 003h
  db 0f8h, 007h, 0f8h, 006h, 0f8h, 004h, 0f8h, 004h, 0f8h, 006h
  db 0f8h, 007h, 0fch, 003h, 0fch, 003h, 0feh, 001h, 0ffh, 000h, 0ffh, 000h
  db 0c0h, 03fh, 000h, 0f3h, 000h, 0e1h, 000h, 080h, 000h, 012h
  db 000h, 03fh, 000h, 07fh, 000h, 03fh, 000h, 03fh, 000h, 07fh
  db 000h, 03fh, 000h, 012h, 000h, 080h, 000h, 0e1h, 000h, 0f3h, 0c0h, 03fh
  db 0ffh, 000h, 03fh, 0c0h, 01fh, 0e0h, 00fh, 070h, 00fh, 030h
  db 007h, 038h, 007h, 098h, 007h, 008h, 007h, 008h, 007h, 098h
  db 007h, 038h, 00fh, 030h, 00fh, 070h, 01fh, 0e0h, 03fh, 0c0h, 0ffh, 000h

ball_p0_c1_6:
  db 183, 000h, 38, 62, 3, 32
  db 0ffh, 000h, 0ffh, 000h, 0ffh, 000h, 0feh, 001h, 0feh, 001h
  db 0fch, 003h, 0fch, 003h, 0fch, 002h, 0fch, 002h, 0fch, 003h
  db 0fch, 003h, 0feh, 001h, 0feh, 001h, 0ffh, 000h, 0ffh, 000h, 0ffh, 000h
  db 0e0h, 01fh, 080h, 079h, 000h, 0f0h, 000h, 0c0h, 000h, 089h
  db 000h, 09fh, 000h, 03fh, 000h, 01fh, 000h, 01fh, 000h, 03fh
  db 000h, 09fh, 000h, 089h, 000h, 0c0h, 000h, 0f0h, 080h, 079h, 0e0h, 01fh
  db 07fh, 080h, 01fh, 0e0h, 00fh, 0f0h, 007h, 038h, 007h, 018h
  db 003h, 09ch, 003h, 0cch, 003h, 084h, 003h, 084h, 003h, 0cch
  db 003h, 09ch, 007h, 018h, 007h, 038h, 00fh, 0f0h, 01fh, 0e0h, 07fh, 080h

ball_p0_c1_7:
  db 183, 000h, 38, 62, 3, 32
  db 0ffh, 000h, 0ffh, 000h, 0ffh, 000h, 0ffh, 000h, 0ffh, 000h
  db 0feh, 001h, 0feh, 001h, 0feh, 001h, 0feh, 001h, 0feh, 001h
  db 0feh, 001h, 0ffh, 000h, 0ffh, 000h, 0ffh, 000h, 0ffh, 000h, 0ffh, 000h
  db 0f0h, 00fh, 0c0h, 03ch, 080h, 078h, 000h, 0e0h, 000h, 0c4h
  db 000h, 0cfh, 000h, 09fh, 000h, 00fh, 000h, 00fh, 000h, 09fh
  db 000h, 0cfh, 000h, 0c4h, 000h, 0e0h, 080h, 078h, 0c0h, 03ch, 0f0h, 00fh
  db 03fh, 0c0h, 00fh, 0f0h, 007h, 078h, 003h, 01ch, 003h, 08ch
  db 001h, 0ceh, 001h, 0e6h, 001h, 0c2h, 001h, 0c2h, 001h, 0e6h
  db 001h, 0ceh, 003h, 08ch, 003h, 01ch, 007h, 078h, 00fh, 0f0h, 03fh, 0c0h

ball3_p0_c3:
  db  0fch, 0f8h, 8
  dw  ball3_p0_c3_0, ball3_p0_c3_1, ball3_p0_c3_2, ball3_p0_c3_3
  dw  ball3_p0_c3_4, ball3_p0_c3_5, ball3_p0_c3_6, ball3_p0_c3_7

ball3_p0_c3_0:
  db 183, 010h, 39, 63, 2, 64
  db 0f8h, 007h, 007h, 007h, 0e0h, 01fh, 01fh, 01fh, 0c0h, 03fh, 03fh, 03fh
  db 080h, 07fh, 07fh, 07fh, 080h, 077h, 07fh, 070h, 000h, 0e3h, 0ffh, 0e0h
  db 000h, 0c1h, 0ffh, 0c0h, 000h, 080h, 0ffh, 080h, 000h, 0f1h, 0f0h, 0ffh
  db 000h, 0f3h, 0f0h, 0ffh, 000h, 0f7h, 0f0h, 0ffh, 080h, 07fh, 070h, 07fh
  db 080h, 07fh, 078h, 07fh, 0c0h, 03fh, 03ch, 03fh, 0e0h, 01fh, 01eh, 01fh
  db 0f8h, 007h, 007h, 007h
  db 01fh, 0e0h, 0e0h, 0e0h, 007h, 0f8h, 078h, 0f8h, 003h, 0fch, 03ch, 0fch
  db 001h, 0feh, 01eh, 0feh, 001h, 0feh, 00eh, 0feh, 000h, 0efh, 00fh, 0ffh
  db 000h, 0cfh, 00fh, 0ffh, 000h, 08fh, 00fh, 0ffh, 000h, 001h, 0ffh, 001h
  db 000h, 083h, 0ffh, 003h, 000h, 0c7h, 0ffh, 007h, 001h, 0eeh, 0feh, 00eh
  db 001h, 0feh, 0feh, 0feh, 003h, 0fch, 0fch, 0fch, 007h, 0f8h, 0f8h, 0f8h
  db 01fh, 0e0h, 0e0h, 0e0h

ball3_p0_c3_1:
  db 183, 010h, 38, 62, 3, 64
  db 0fch, 003h, 003h, 003h, 0f0h, 00fh, 00fh, 00fh, 0e0h, 01fh, 01fh, 01fh
  db 0c0h, 03fh, 03fh, 03fh, 0c0h, 03bh, 03fh, 038h, 080h, 071h, 07fh, 070h
  db 080h, 060h, 07fh, 060h, 080h, 040h, 07fh, 040h, 080h, 078h, 078h, 07fh
  db 080h, 079h, 078h, 07fh, 080h, 07bh, 078h, 07fh, 0c0h, 03fh, 038h, 03fh
  db 0c0h, 03fh, 03ch, 03fh, 0e0h, 01fh, 01eh, 01fh, 0f0h, 00fh, 00fh, 00fh
  db 0fch, 003h, 003h, 003h
  db 00fh, 0f0h, 0f0h, 0f0h, 003h, 0fch, 0bch, 0fch, 001h, 0feh, 09eh, 0feh
  db 000h, 0ffh, 08fh, 0ffh, 000h, 0ffh, 087h, 07fh, 000h, 0f7h, 087h, 07fh
  db 000h, 0e7h, 087h, 07fh, 000h, 047h, 087h, 07fh, 000h, 080h, 07fh, 080h
  db 000h, 0c1h, 07fh, 081h, 000h, 0e3h, 07fh, 083h, 000h, 0f7h, 07fh, 087h
  db 000h, 0ffh, 07fh, 0ffh, 001h, 0feh, 07eh, 0feh, 003h, 0fch, 07ch, 0fch
  db 00fh, 0f0h, 0f0h, 0f0h
  db 0ffh, 000h, 000h, 000h, 0ffh, 000h, 000h, 000h, 0ffh, 000h, 000h, 000h
  db 0ffh, 000h, 000h, 000h, 0ffh, 000h, 000h, 000h, 07fh, 080h, 080h, 080h
  db 07fh, 080h, 080h, 080h, 07fh, 080h, 080h, 080h, 07fh, 080h, 080h, 080h
  db 07fh, 080h, 080h, 080h, 07fh, 080h, 080h, 080h, 0ffh, 000h, 000h, 000h
  db 0ffh, 000h, 000h, 000h, 0ffh, 000h, 000h, 000h, 0ffh, 000h, 000h, 000h
  db 0ffh, 000h, 000h, 000h

ball3_p0_c3_2:
  db 183, 010h, 38, 62, 3, 64
  db 0feh, 001h, 001h, 001h, 0f8h, 007h, 007h, 007h, 0f0h, 00fh, 00fh, 00fh
  db 0e0h, 01fh, 01fh, 01fh, 0e0h, 01dh, 01fh, 01ch, 0c0h, 038h, 03fh, 038h
  db 0c0h, 030h, 03fh, 030h, 0c0h, 020h, 03fh, 020h, 0c0h, 03ch, 03ch, 03fh
  db 0c0h, 03ch, 03ch, 03fh, 0c0h, 03dh, 03ch, 03fh, 0e0h, 01fh, 01ch, 01fh
  db 0e0h, 01fh, 01eh, 01fh, 0f0h, 00fh, 00fh, 00fh, 0f8h, 007h, 007h, 007h
  db 0feh, 001h, 001h, 001h
  db 007h, 0f8h, 0f8h, 0f8h, 001h, 0feh, 0deh, 0feh, 000h, 0ffh, 0cfh, 0ffh
  db 000h, 0ffh, 0c7h, 0ffh, 000h, 0ffh, 0c3h, 03fh, 000h, 0fbh, 0c3h, 03fh
  db 000h, 073h, 0c3h, 03fh, 000h, 023h, 0c3h, 03fh, 000h, 040h, 03fh, 0c0h
  db 000h, 0e0h, 03fh, 0c0h, 000h, 0f1h, 03fh, 0c1h, 000h, 0fbh, 03fh, 0c3h
  db 000h, 0ffh, 03fh, 0ffh, 000h, 0ffh, 03fh, 0ffh, 001h, 0feh, 0beh, 0feh
  db 007h, 0f8h, 0f8h, 0f8h
  db 0ffh, 000h, 000h, 000h, 0ffh, 000h, 000h, 000h, 0ffh, 000h, 000h, 000h
  db 07fh, 080h, 080h, 080h, 07fh, 080h, 080h, 080h, 03fh, 0c0h, 0c0h, 0c0h
  db 03fh, 0c0h, 0c0h, 0c0h, 03fh, 0c0h, 0c0h, 0c0h, 03fh, 040h, 0c0h, 040h
  db 03fh, 0c0h, 0c0h, 0c0h, 03fh, 0c0h, 0c0h, 0c0h, 07fh, 080h, 080h, 080h
  db 07fh, 080h, 080h, 080h, 0ffh, 000h, 000h, 000h, 0ffh, 000h, 000h, 000h
  db 0ffh, 000h, 000h, 000h

ball3_p0_c3_3:
  db 183, 010h, 38, 62, 3, 64
  db 0ffh, 000h, 000h, 000h, 0fch, 003h, 003h, 003h, 0f8h, 007h, 007h, 007h
  db 0f0h, 00fh, 00fh, 00fh, 0f0h, 00eh, 00fh, 00eh, 0e0h, 01ch, 01fh, 01ch
  db 0e0h, 018h, 01fh, 018h, 0e0h, 010h, 01fh, 010h, 0e0h, 01eh, 01eh, 01fh
  db 0e0h, 01eh, 01eh, 01fh, 0e0h, 01eh, 01eh, 01fh, 0f0h, 00fh, 00eh, 00fh
  db 0f0h, 00fh, 00fh, 00fh, 0f8h, 007h, 007h, 007h, 0fch, 003h, 003h, 003h
  db 0ffh, 000h, 000h, 000h
  db 003h, 0fch, 0fch, 0fch, 000h, 0ffh, 0efh, 0ffh, 000h, 0ffh, 0e7h, 0ffh
  db 000h, 0ffh, 0e3h, 0ffh, 000h, 0ffh, 0e1h, 01fh, 000h, 07dh, 0e1h, 01fh
  db 000h, 039h, 0e1h, 01fh, 000h, 011h, 0e1h, 01fh, 000h, 020h, 01fh, 0e0h
  db 000h, 070h, 01fh, 0e0h, 000h, 0f8h, 01fh, 0e0h, 000h, 0fdh, 01fh, 0e1h
  db 000h, 0ffh, 01fh, 0ffh, 000h, 0ffh, 09fh, 0ffh, 000h, 0ffh, 0dfh, 0ffh
  db 003h, 0fch, 0fch, 0fch
  db 0ffh, 000h, 000h, 000h, 0ffh, 000h, 000h, 000h, 07fh, 080h, 080h, 080h
  db 03fh, 0c0h, 0c0h, 0c0h, 03fh, 0c0h, 0c0h, 0c0h, 01fh, 0e0h, 0e0h, 0e0h
  db 01fh, 0e0h, 0e0h, 0e0h, 01fh, 0e0h, 0e0h, 0e0h, 01fh, 020h, 0e0h, 020h
  db 01fh, 060h, 0e0h, 060h, 01fh, 0e0h, 0e0h, 0e0h, 03fh, 0c0h, 0c0h, 0c0h
  db 03fh, 0c0h, 0c0h, 0c0h, 07fh, 080h, 080h, 080h, 0ffh, 000h, 000h, 000h
  db 0ffh, 000h, 000h, 000h

ball3_p0_c3_4:
  db 183, 010h, 38, 62, 3, 64
  db 0ffh, 000h, 000h, 000h, 0feh, 001h, 001h, 001h, 0fch, 003h, 003h, 003h
  db 0f8h, 007h, 007h, 007h, 0f8h, 007h, 007h, 007h, 0f0h, 00eh, 00fh, 00eh
  db 0f0h, 00ch, 00fh, 00ch, 0f0h, 008h, 00fh, 008h, 0f0h, 00fh, 00fh, 00fh
  db 0f0h, 00fh, 00fh, 00fh, 0f0h, 00fh, 00fh, 00fh, 0f8h, 007h, 007h, 007h
  db 0f8h, 007h, 007h, 007h, 0fch, 003h, 003h, 003h, 0feh, 001h, 001h, 001h
  db 0ffh, 000h, 000h, 000h
  db 081h, 07eh, 07eh, 07eh, 000h, 0ffh, 0f7h, 0ffh, 000h, 0ffh, 0f3h, 0ffh
  db 000h, 0ffh, 0f1h, 0ffh, 000h, 07fh, 0f0h, 00fh, 000h, 03eh, 0f0h, 00fh
  db 000h, 01ch, 0f0h, 00fh, 000h, 008h, 0f0h, 00fh, 000h, 010h, 00fh, 0f0h
  db 000h, 038h, 00fh, 0f0h, 000h, 07ch, 00fh, 0f0h, 000h, 0feh, 00fh, 0f0h
  db 000h, 0ffh, 08fh, 0ffh, 000h, 0ffh, 0cfh, 0ffh, 000h, 0ffh, 0efh, 0ffh
  db 081h, 07eh, 07eh, 07eh
  db 0ffh, 000h, 000h, 000h, 07fh, 080h, 080h, 080h, 03fh, 0c0h, 0c0h, 0c0h
  db 01fh, 0e0h, 0e0h, 0e0h, 01fh, 0e0h, 0e0h, 0e0h, 00fh, 0f0h, 0f0h, 0f0h
  db 00fh, 0f0h, 0f0h, 0f0h, 00fh, 0f0h, 0f0h, 0f0h, 00fh, 010h, 0f0h, 010h
  db 00fh, 030h, 0f0h, 030h, 00fh, 070h, 0f0h, 070h, 01fh, 0e0h, 0e0h, 0e0h
  db 01fh, 0e0h, 0e0h, 0e0h, 03fh, 0c0h, 0c0h, 0c0h, 07fh, 080h, 080h, 080h
  db 0ffh, 000h, 000h, 000h

ball3_p0_c3_5:
  db 183, 010h, 38, 62, 3, 64
  db 0ffh, 000h, 000h, 000h, 0ffh, 000h, 000h, 000h, 0feh, 001h, 001h, 001h
  db 0fch, 003h, 003h, 003h, 0fch, 003h, 003h, 003h, 0f8h, 007h, 007h, 007h
  db 0f8h, 006h, 007h, 006h, 0f8h, 004h, 007h, 004h, 0f8h, 007h, 007h, 007h
  db 0f8h, 007h, 007h, 007h, 0f8h, 007h, 007h, 007h, 0fch, 003h, 003h, 003h
  db 0fch, 003h, 003h, 003h, 0feh, 001h, 001h, 001h, 0ffh, 000h, 000h, 000h
  db 0ffh, 000h, 000h, 000h
  db 0c0h, 03fh, 03fh, 03fh, 000h, 0ffh, 0fbh, 0ffh, 000h, 0ffh, 0f9h, 0ffh
  db 000h, 0ffh, 0f8h, 0ffh, 000h, 0bfh, 0f8h, 087h, 000h, 01fh, 0f8h, 007h
  db 000h, 00eh, 0f8h, 007h, 000h, 004h, 0f8h, 007h, 000h, 088h, 087h, 0f8h
  db 000h, 09ch, 087h, 0f8h, 000h, 0beh, 087h, 0f8h, 000h, 0ffh, 087h, 0f8h
  db 000h, 0ffh, 0c7h, 0ffh, 000h, 0ffh, 0e7h, 0ffh, 000h, 0ffh, 0f7h, 0ffh
  db 0c0h, 03fh, 03fh, 03fh
  db 0ffh, 000h, 000h, 000h, 03fh, 0c0h, 0c0h, 0c0h, 01fh, 0e0h, 0e0h, 0e0h
  db 00fh, 0f0h, 0f0h, 0f0h, 00fh, 0f0h, 070h, 0f0h, 007h, 078h, 078h, 0f8h
  db 007h, 078h, 078h, 0f8h, 007h, 078h, 078h, 0f8h, 007h, 008h, 0f8h, 008h
  db 007h, 018h, 0f8h, 018h, 007h, 038h, 0f8h, 038h, 00fh, 070h, 0f0h, 070h
  db 00fh, 0f0h, 0f0h, 0f0h, 01fh, 0e0h, 0e0h, 0e0h, 03fh, 0c0h, 0c0h, 0c0h
  db 0ffh, 000h, 000h, 000h

ball3_p0_c3_6:
  db 183, 010h, 38, 62, 3, 64
  db 0ffh, 000h, 000h, 000h, 0ffh, 000h, 000h, 000h, 0ffh, 000h, 000h, 000h
  db 0feh, 001h, 001h, 001h, 0feh, 001h, 001h, 001h, 0fch, 003h, 003h, 003h
  db 0fch, 003h, 003h, 003h, 0fch, 002h, 003h, 002h, 0fch, 003h, 003h, 003h
  db 0fch, 003h, 003h, 003h, 0fch, 003h, 003h, 003h, 0feh, 001h, 001h, 001h
  db 0feh, 001h, 001h, 001h, 0ffh, 000h, 000h, 000h, 0ffh, 000h, 000h, 000h
  db 0ffh, 000h, 000h, 000h
  db 0e0h, 01fh, 01fh, 01fh, 080h, 07fh, 07dh, 07fh, 000h, 0ffh, 0fch, 0ffh
  db 000h, 0ffh, 0fch, 0ffh, 000h, 0dfh, 0fch, 0c3h, 000h, 08fh, 0fch, 083h
  db 000h, 007h, 0fch, 003h, 000h, 002h, 0fch, 003h, 000h, 0c4h, 0c3h, 0fch
  db 000h, 0ceh, 0c3h, 0fch, 000h, 0dfh, 0c3h, 0fch, 000h, 0ffh, 0c3h, 0fch
  db 000h, 0ffh, 0e3h, 0ffh, 000h, 0ffh, 0f3h, 0ffh, 080h, 07fh, 07bh, 07fh
  db 0e0h, 01fh, 01fh, 01fh
  db 07fh, 080h, 080h, 080h, 01fh, 0e0h, 0e0h, 0e0h, 00fh, 0f0h, 0f0h, 0f0h
  db 007h, 0f8h, 078h, 0f8h, 007h, 0f8h, 038h, 0f8h, 003h, 0bch, 03ch, 0fch
  db 003h, 03ch, 03ch, 0fch, 003h, 03ch, 03ch, 0fch, 003h, 004h, 0fch, 004h
  db 003h, 00ch, 0fch, 00ch, 003h, 01ch, 0fch, 01ch, 007h, 0b8h, 0f8h, 038h
  db 007h, 0f8h, 0f8h, 0f8h, 00fh, 0f0h, 0f0h, 0f0h, 01fh, 0e0h, 0e0h, 0e0h
  db 07fh, 080h, 080h, 080h

ball3_p0_c3_7:
  db 183, 010h, 38, 62, 3, 64
  db 0ffh, 000h, 000h, 000h, 0ffh, 000h, 000h, 000h, 0ffh, 000h, 000h, 000h
  db 0ffh, 000h, 000h, 000h, 0ffh, 000h, 000h, 000h, 0feh, 001h, 001h, 001h
  db 0feh, 001h, 001h, 001h, 0feh, 001h, 001h, 001h, 0feh, 001h, 001h, 001h
  db 0feh, 001h, 001h, 001h, 0feh, 001h, 001h, 001h, 0ffh, 000h, 000h, 000h
  db 0ffh, 000h, 000h, 000h, 0ffh, 000h, 000h, 000h, 0ffh, 000h, 000h, 000h
  db 0ffh, 000h, 000h, 000h
  db 0f0h, 00fh, 00fh, 00fh, 0c0h, 03fh, 03eh, 03fh, 080h, 07fh, 07eh, 07fh
  db 000h, 0ffh, 0feh, 0ffh, 000h, 0efh, 0feh, 0e1h, 000h, 0c7h, 0feh, 0c1h
  db 000h, 083h, 0feh, 081h, 000h, 001h, 0feh, 001h, 000h, 0e2h, 0e1h, 0feh
  db 000h, 0e7h, 0e1h, 0feh, 000h, 0efh, 0e1h, 0feh, 000h, 0ffh, 0e1h, 0feh
  db 000h, 0ffh, 0f1h, 0ffh, 080h, 07fh, 079h, 07fh, 0c0h, 03fh, 03dh, 03fh
  db 0f0h, 00fh, 00fh, 00fh
  db 03fh, 0c0h, 0c0h, 0c0h, 00fh, 0f0h, 0f0h, 0f0h, 007h, 0f8h, 078h, 0f8h
  db 003h, 0fch, 03ch, 0fch, 003h, 0fch, 01ch, 0fch, 001h, 0deh, 01eh, 0feh
  db 001h, 09eh, 01eh, 0feh, 001h, 01eh, 01eh, 0feh, 001h, 002h, 0feh, 002h
  db 001h, 006h, 0feh, 006h, 001h, 08eh, 0feh, 00eh, 003h, 0dch, 0fch, 01ch
  db 003h, 0fch, 0fch, 0fch, 007h, 0f8h, 0f8h, 0f8h, 00fh, 0f0h, 0f0h, 0f0h
  db 03fh, 0c0h, 0c0h, 0c0h

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
    DB  14, 200, 216, 40, 0, 14
    DB  254
#END
