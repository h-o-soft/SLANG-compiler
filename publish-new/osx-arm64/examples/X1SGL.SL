
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

// テキストを両ページとも指定文字で埋める
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
          // SGL_PRINT2は2ページ両方の指定位置に文字列を描画する
          SGL_PRINT2(I,J,MOJI);
        }
}

// SGL_VSYNC() を呼び出す場合は必ずVSYNC_PROC()という名前の関数を定義しなければいけない
// 1/62秒？ごとに必ず呼び出されます(PSG_PROCが無い場合でも空っぽの関数を書いてください)
VSYNC_PROC()
{
  PSG_PROC();
}

MAIN()
{
  // 内部的にWIDTH 40にしておく必要がある
  WIDTH(40);

  // SGL初期化 
  SGL_INIT();

  // FPSMODE 0 = 60fps / 2 = 30fps / 4 = 20fps
  SGL_FPSMODE(2);

  // PCGを定義する
  PCGDEF(42,BLOCKPCG);

  // SET PCG & COLOR 7(ALL)
  TATR_FILL(0,0,40,25,$27);

  // とりあえずPCGの画面を構成する
  SGL_TFILL(0,0,40,25,'*');
  SGL_TFILL(1,1,39,24,' ');
  FOR Y=0 TO 5
  {
    SGL_TFILL(1,3 + Y*4,39,3 + Y*4,'*');
  }
  // 文字書く(PCGのアトリビュートをこの部分だけ解除)
  TATR_FILL(2,1,2+11,1,$07);
  SGL_PRINT2(2,1,"X1 SGL DEMO");

  // X1 SGLのパターン定義(1から)
  SGL_DEFPAT(1, BALL1);
  SGL_DEFPAT(2, BALL3);
  // スプライトを作る(戻り値を使ってスプライトを扱う)
  // 第一引数がパターン番号、第二引数は種別だが、現状0以外ならなんでも良い
  SPR1 = SGL_SPRCREATE(2, 2);
  SPR2 = SGL_SPRCREATE(1, 2);
  SPR3 = SGL_SPRCREATE(1, 2);

  X = 160 << 7;
  Y = 90 << 8;
  // スプライトの位置を移動させる(座標変更だけしたい場合はこの関数を使う)
  SGL_SPRMOVE(SPR2,X,Y);
  SGL_SPRMOVE(SPR3,X,110 << 8);

  // スプライト関連の全データを更新する場合はSPRSETを使う
  Y2 = 150 << 8;
  SPRDAT[0] = 1; // KIND
  SPRDAT[1] = 1*2; // PATTERN(パターン番号を2倍する事)
  MEMW[SPRDAT + 3] = 100 << 7; // X
  MEMW[SPRDAT + 6] = Y2; // Y
  // 全情報を設定する
  SGL_SPRSET(SPR2,SPRDAT);

  // 指定スプライトを無効にする
  // SGL_SPRDESTROY(SPR2);

  // PSGの初期化と再生
  // PSG_INIT(0)なので割り込みは使わないモードになる
  // (しかし30fpsや20fpsでもテンポは維持される(はず))
  PSG_INIT(0);
  PSG_PLAY(MUSADR);

  LOOP {
    // 移動
    SGL_SPRMOVE(SPR1,X,Y);
    SGL_SPRMOVE(SPR2,100 << 7, Y2);

    // スプライトパターンのみ切り替える
    SGL_SPRPAT(SPR1,1 + ((X >> 10) AND 1));

    // スプライトの表示、非表示を切り替える(0で非表示、1で表示)
    SGL_SPRDISP(SPR3,((X >> 11) AND 1));
    X += 1 << 7;
    IF X >= (320 << 7) THEN X = 0;
    Y2 += 3 << 8;
    IF Y2 >= (200 << 8) THEN Y2 = 0;

    // X1 SGLの場合はループで必ずこれを呼ぶ必要がある
    SGL_VSYNC();
  }
}


#ASM
; X1 SGLのパターンデータテーブル
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
#END
