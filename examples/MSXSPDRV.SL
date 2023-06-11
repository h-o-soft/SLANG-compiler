
#INCLUDE MSXGRP.LIB

CONST CHARS=_chars, COLORS=_colors;
CONST PLAYERPAT=_playerpat;

ARRAY BYTE SPATR[3];

VAR PADR;
VAR ANIM;
VAR X,Y,FLP;
VAR S;


MAIN()
VAR ADDIDX,I;
{
    MSX_GRPINIT();
    MSX_SET_COLOR(15,0,0);
  
    SET_SPRITE_MODE(SPRITE_LARGE);
    SET_CLICK(0);

    MSX_VWRITE("MSX SPDRV DEMO (SLANG)",(VRAM_START + VRAM_WIDTH * (1) + 2) , 22);

    // 定義は別途考える
    SPR_DEF(0, PLAYERPAT, 6, 0);
    // 左右反転
    SPR_DEF(6, PLAYERPAT, 6, 1);

    SPATR[0]=100;   // Y
    SPATR[1]=50;    // X
    SPATR[2]=0;
    SPATR[3]=15;

    SPDRV_INITIALIZE();

    FOR I=0 TO 4 {
        SPDRV_SET(I,SPATR);
        SPDRV_MOVE(I,I*30,50 + I*2);
    }

    FLP = 0;
    X=10;
    Y=50;

    LOOP
    {
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

        SPATR[0] = Y;
        SPATR[1] = X;
        // color 1
        SPATR[2] = ADDIDX;
        SPATR[3] = 1;
        SPDRV_SET(0, SPATR);
        // color 9
        SPATR[2] += 4;
        SPATR[3] = 9;
        SPDRV_SET(1,SPATR);
        // color 15
        SPATR[2] += 4;
        SPATR[3] = 15;
        SPDRV_SET(2,SPATR);

        // update sprite
        SPDRV_UPDATE();
        WAIT_VSYNC(2);
        SPDRV_FLIP();
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
