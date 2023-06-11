
#INCLUDE MSXGRP.LIB

CONST SPRPAT=sprite_pattern;

ARRAY BYTE SPATR[8-1];

VAR ANIM;
VAR X,Y;
VAR S;


MAIN()
VAR I;
{
    MSX_SET_COLOR(15,9,6);
    MSX_SCREEN(5);
    SET_SPRITE_MODE(SPRITE_LARGE);

    SPDRV2_INITIALIZE();
  
    SET_CLICK(0);
    MSX_VWRITE(SPRPAT, 0x7800, 32 * 3);

    // 2plane
    SPATR[0]=100;   // Y
    SPATR[1]=50;    // X
    SPATR[2]=0;
    // SPATR[3]=3; // 0=green, 1=blue, 2=red, 3=yellow
    SPATR[4]=2;
    SPATR[5]=4;
    SPATR[6]=4;

    FOR I=0 TO 2 {
        SPATR[3]=RND(3); // 0=green, 1=blue, 2=red, 3=yellow
        SPDRV2_SET(I,SPATR);
        SPDRV2_MOVE(I,I*30,50 + I*2);
    }

    // 1plane
    SPATR[2]=8;
    // SPATR[3]=6; // 5=blue , 6=green
    SPATR[4]=1;

    FOR I=3 TO 5 {
        SPATR[3]=5 + (RND(3) >> 1); // 5=blue , 6=green
        SPDRV2_SET(I,SPATR);
        SPDRV2_MOVE(I,I*30,50 + I*2);
    }

    X=10;
    Y=50;

    SPATR[2] = 0;
    SPATR[4] = 2;
    LOOP
    {
        // キーボード or ジョイスティック1で移動
        S = STICK2(0) OR STICK2(1);
        IF S AND 8 THEN {
            X--;
        } ELSE IF S AND 2 THEN {
            X++;
        }
        IF S AND 1 THEN {
            Y--;
        } ELSE IF S AND 4 THEN {
            Y++;
        }
        ANIM++;

        SPATR[0] = Y;
        SPATR[1] = X;
        SPATR[3] = (ANIM >> 4) AND 3;
        SPDRV2_SET(6, SPATR);

        // update sprite
        SPDRV2_UPDATE();
        WAIT_VSYNC(2);
        SPDRV2_FLIP();
    }


}

#ASM
				; =============================================================
				;		data
				; =============================================================
sprite_pattern:
				db		0b00000000				; #0
				db		0b00000011
				db		0b00001111
				db		0b00011111
				db		0b00111111
				db		0b00111111
				db		0b01111111
				db		0b01111111
				db		0b01111111
				db		0b01111111
				db		0b00111111
				db		0b00111111
				db		0b00011111
				db		0b00001111
				db		0b00000011
				db		0b00000000
				db		0b00000000
				db		0b11000000
				db		0b11110000
				db		0b11111000
				db		0b11111100
				db		0b11111100
				db		0b11111110
				db		0b11111110
				db		0b11111110
				db		0b11111110
				db		0b11111100
				db		0b11111100
				db		0b11111000
				db		0b11110000
				db		0b11000000
				db		0b00000000

				db		0b00000011				; #4
				db		0b00001100
				db		0b00010000
				db		0b00100000
				db		0b01000000
				db		0b01000000
				db		0b10000000
				db		0b10000000
				db		0b10000000
				db		0b10000000
				db		0b01000000
				db		0b01000000
				db		0b00110000
				db		0b00011111
				db		0b00001111
				db		0b00000011
				db		0b11000000
				db		0b00110000
				db		0b00001000
				db		0b00000100
				db		0b00000010
				db		0b00000010
				db		0b00000011
				db		0b00000011
				db		0b00000111
				db		0b00000111
				db		0b00001110
				db		0b00011110
				db		0b01111100
				db		0b11111000
				db		0b11110000
				db		0b11000000

				db		0b00111111				; #8
				db		0b01111111
				db		0b11111111
				db		0b11111111
				db		0b11111111
				db		0b11111111
				db		0b11111111
				db		0b11111111
				db		0b11111111
				db		0b11111111
				db		0b11111111
				db		0b11111111
				db		0b11111111
				db		0b11111111
				db		0b01111111
				db		0b00111111
				db		0b11111100
				db		0b11111110
				db		0b11111111
				db		0b11111111
				db		0b11111111
				db		0b11111111
				db		0b11111111
				db		0b11111111
				db		0b11111111
				db		0b11111111
				db		0b11111111
				db		0b11111111
				db		0b11111111
				db		0b11111111
				db		0b11111110
				db		0b11111100

sprite_color_table:
				db		0x02			; #0
				db		0x02
				db		0x02
				db		0x02
				db		0x02
				db		0x02
				db		0x02
				db		0x02
				db		0x02
				db		0x02
				db		0x02
				db		0x02
				db		0x02
				db		0x02
				db		0x02
				db		0x02

				db		0x04			; #1
				db		0x04
				db		0x04
				db		0x04
				db		0x04
				db		0x04
				db		0x04
				db		0x04
				db		0x04
				db		0x04
				db		0x04
				db		0x04
				db		0x04
				db		0x04
				db		0x04
				db		0x04

				db		0x08			; #2
				db		0x08
				db		0x08
				db		0x08
				db		0x08
				db		0x08
				db		0x08
				db		0x08
				db		0x08
				db		0x08
				db		0x08
				db		0x08
				db		0x08
				db		0x08
				db		0x08
				db		0x08

				db		0x0A			; #3
				db		0x0A
				db		0x0A
				db		0x0A
				db		0x0A
				db		0x0A
				db		0x0A
				db		0x0A
				db		0x0A
				db		0x0A
				db		0x0A
				db		0x0A
				db		0x0A
				db		0x0A
				db		0x0A
				db		0x0A

				db		0x41			; #4
				db		0x41
				db		0x41
				db		0x41
				db		0x41
				db		0x41
				db		0x41
				db		0x41
				db		0x41
				db		0x41
				db		0x41
				db		0x41
				db		0x41
				db		0x41
				db		0x41
				db		0x41

				db		0x04			; #5
				db		0x05
				db		0x07
				db		0x07
				db		0x05
				db		0x05
				db		0x05
				db		0x05
				db		0x04
				db		0x05
				db		0x04
				db		0x04
				db		0x04
				db		0x04
				db		0x04
				db		0x04

				db		0x0C			; #6
				db		0x02
				db		0x03
				db		0x03
				db		0x02
				db		0x02
				db		0x02
				db		0x02
				db		0x0C
				db		0x02
				db		0x0C
				db		0x0C
				db		0x0C
				db		0x0C
				db		0x0C
				db		0x0C
#END
