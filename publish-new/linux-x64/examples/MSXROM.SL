
#INCLUDE MSXGRP.LIB

CONST CHARS=_chars, COLORS=_colors;
CONST PLAYERPAT=_playerpat;

ARRAY BYTE SPRDAT[4*2-1];
VAR ANIM;
VAR X,Y,FLP;
VAR S;

FILL_PCG(X1,Y1,X2,Y2,PAT)
VAR I,CNT;
{
    CNT = X2-X1+1;
  
    FOR I=Y1 TO Y2
    {
        MSX_VFILL(VRAM_START + VRAM_WIDTH * I + X1, PAT, CNT);
    }

}

MAIN()
VAR ADDIDX,I;
{
    MSX_GRPINIT();
    MSX_SET_COLOR(15,0,0);
  
    SET_SPRITE_MODE(SPRITE_LARGE);
    SET_CLICK(0);
    MSX_VFILL(VRAM_START, VRAM_NONE, VRAM_WIDTH * VRAM_HEIGHT);
  
    MSX_VWRITE(CHARS, 0x0000, 16);
    MSX_VWRITE(CHARS, 0x0800, 16);
    MSX_VWRITE(CHARS, 0x1000, 16);
  
    MSX_VWRITE(COLORS, 0x2000, 16);
    MSX_VWRITE(COLORS, 0x2800, 16);
    MSX_VWRITE(COLORS, 0x3000, 16);
  
    FILL_PCG(0,0,31,23,1);
    FILL_PCG(1,1,30,22,0);
  
    FOR I=0 TO 7
    {
        FILL_PCG(1,I*3,31,I*3,1);
    }
  
    SPMAN_INIT();
  
    // 通常パターン定義
    SPMAN_ALLOC_PAT(0, PLAYERPAT, 6, 0);
    // 左右反転
    SPMAN_ALLOC_PAT(6, PLAYERPAT, 6, 1);
  
    // 文字を描画
    MSX_VWRITE("MSX PCG & SPRITE DEMO",(VRAM_START + VRAM_WIDTH * (1) + 2) , 21);

    X = 128;
    Y = 192/2;
  
    SPRDAT[0] = 192/2;
    SPRDAT[1] = 150;
    SPRDAT[2] = 0;
    SPRDAT[3] = 1;
    FLP = 0;
  
    // SPRDAT[4] = 192/2 - 4;
    // SPRDAT[5] = 256/2;
    SPRDAT[6] = 8;
    SPRDAT[7] = 15;
  
    LOOP {
        // 30fps
        WAIT_VSYNC(2);
        // スプライトを転送
        SPMAN_UPDATE();

        // キーボード or ジョイスティック1で移動
        S = STICK2(0) OR STICK2(1);
        IF S AND 8 THEN {
            X--;
            FLP = 0;
        } ELSE IF S AND 2 THEN {
            X++;
            FLP = 24;
        }
        IF S AND 1 THEN {
            Y--;
        } ELSE IF S AND 4 THEN {
            Y++;
        }

        ADDIDX = FLP + (((ANIM >> 4) AND 1) ? 12 : 0);
        ANIM++;
        
        SPRDAT[0] = Y;
        SPRDAT[1] = X;
        // color 1
        SPRDAT[2] = ADDIDX;
        SPRDAT[3] = 1;
        SPMAN_ALLOC_FIXED_SPRITE(SPRDAT);
        // color 9
        SPRDAT[2] += 4;
        SPRDAT[3] = 9;
        SPMAN_ALLOC_FIXED_SPRITE(SPRDAT);
        // color 15
        SPRDAT[2] += 4;
        SPRDAT[3] = 15;
        SPMAN_ALLOC_FIXED_SPRITE(SPRDAT);
  
        // 固定じゃないスプライト
        // (↑のfixedスプライトと同一ラインに並ぶと順番に描画される(点滅))
        SPRDAT[4] = 192/2 - 4;
        SPRDAT[5] = 256/2;
        SPMAN_ALLOC_SPRITE(&SPRDAT[4]);
        SPRDAT[4] += 4;
        SPRDAT[5] += 12;
        SPMAN_ALLOC_SPRITE(&SPRDAT[4]);
        SPRDAT[4] += 4;
        SPRDAT[5] += 12;
        SPMAN_ALLOC_SPRITE(&SPRDAT[4]);
    }
}

#ASM

_playerpat:
; 0,0 COL 1 : (0,0,0)
    DB $07,$08,$17,$48,$bc,$8a,$8a,$44
    DB $3b,$15,$24,$24,$1e,$09,$04,$03
    DB $e0,$10,$e8,$12,$3d,$91,$91,$31
    DB $f2,$2c,$56,$a1,$32,$cc,$e4,$b8
; 0,0 COL 9 : (255,137,125)
    DB $00,$00,$00,$00,$03,$05,$05,$03
    DB $00,$00,$00,$18,$00,$06,$03,$00
    DB $00,$00,$00,$00,$c0,$60,$60,$c0
    DB $00,$00,$08,$00,$00,$30,$18,$00
; 0,0 COL 15 : (255,255,255)
    DB $00,$07,$08,$07,$40,$70,$70,$38
    DB $04,$0a,$1b,$03,$01,$00,$00,$00
    DB $00,$e0,$10,$e0,$02,$0e,$0e,$0e
    DB $0c,$d0,$a0,$5e,$cc,$00,$00,$00
; 1,0 COL 1 : (0,0,0)
    DB $07,$08,$17,$08,$07,$0c,$0a,$0a
    DB $1c,$2b,$55,$44,$9c,$ae,$45,$03
    DB $e0,$10,$e8,$10,$f0,$30,$90,$98
    DB $36,$f9,$25,$59,$a9,$3b,$e4,$98
; 1,0 COL 9 : (255,137,125)
    DB $00,$00,$00,$00,$00,$03,$05,$05
    DB $03,$00,$00,$18,$00,$00,$02,$00
    DB $00,$00,$00,$00,$00,$c0,$60,$60
    DB $c0,$00,$08,$00,$00,$00,$18,$00
; 1,0 COL 15 : (255,255,255)
    DB $00,$07,$08,$07,$00,$00,$00,$00
    DB $00,$14,$2a,$23,$63,$41,$00,$00
    DB $00,$e0,$10,$e0,$00,$00,$00,$00
    DB $08,$06,$d2,$a6,$56,$c4,$00,$00

_chars:
    DB 0, 0, 0, 0, 0, 0, 0, 0
    DB 0, 247, 247, 247, 0, 127, 127, 127

_colors:
    DB $f0, $f0, $f0, $f0, $f0, $f0, $f0, $f0
    DB $60, $60, $60, $60, $60, $60, $60, $60

#END
