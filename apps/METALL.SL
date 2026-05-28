/*
**      MM   MM EEEEEEE TTTTTTT    A    L           /~~~~   _ /
**      M M M M E          T     AA AA  L          /     /~~//
**      M  M  M EEEEEE     T    A     A L         /~~~. / _//__ __/_
**      M     M E          T    AAAAAAA L        /   /  ~~//~ /  /~
**      M     M EEEEEEE    T    A     A LLLLLLL /   /  __//  /  /
**                                                              ~~
*/

//ORG     $3000;
//OFFSET  $6000;
//WORK    $D000;

// LSX-Dodgers
//ORG $100;

//CONST   _THREE=1,_PLOTSW=0,_FLOAT=0,_SINCOS=1,_TILESW=1,_GHIN=$49;
//CONST _KPRINTF=0;
//CONST _COLOR=0;

CONST   _THREE=1,_PLOTSW=0,_FLOAT=0,_SINCOS=1,_TILESW=1,_GHIN=$49;
CONST _KPRINTF=0;
CONST _COLOR=0;

// X1/LSX-Dodgersの場合は_LSXを1にしてやる
#IF ENV_TYPE<=1
CONST _LSX=1;
#ELSE
CONST _LSX=0;
#ENDIF


ARRAY
MYCH[63][8]=[
//      0      ........      ........      ........      ........
              "｡---｡ | ",0, "|   O  |",0, "ﾍ   |  |",0, "ﾍ---@=o|",0,
              " |  |  |",0, " +---｡| ",0, "|====ﾍ  ",0, " ----   ",0,
//      3      ........      ........      ........      ........
              "｡----｡-ﾍ",0, "|    | |",0, "|    | |",0, "|====| |",0,
              "|    | |",0, "-----+_/",0, "|-|==ﾍ  ",0, "------  ",0,
//      2      ........      ........      ........      ........
              " | ｡---｡",0, "|  O   |",0, "|  |/~~/",0, "|o=| __/",0,
              "|  |ﾍ---",0, " |｡----+",0, "  /====|",0, "   ---- ",0,
//      1      ........      ........      ........      ........
              "/~~|---｡",0, "|  |OO |",0, "|  |  /-",0, "|  |--|@",0,
              "|  |  ﾍ-",0, "ﾍ__|---+",0, "  /#/== ",0, "   -----",0,
//      4      ........      ........      ........      ........
              "｡---｡   ",0, "|   O   ",0, "ﾍ ｡---｡ ",0, "ﾍ-|   | ",0,
              " ||   | ",0, " +|   | ",0, "|=|   | ",0, " -ﾟ---ﾟ ",0,
//      7      ........      ........      ........      ........
              "｡----｡  ",0, "|    |  ",0, "|  ｡---｡",0, "|==|   |",0,
              "|  |   |",0, "---|   |",0, "|-||   |",0, "---ﾟ---ﾟ",0,
//      6      ........      ........      ........      ........
              "   ｡---｡",0, "   O   |",0, " ｡-|/--/",0, " | || -/",0,
              " | |ﾍ-- ",0, " | ---+ ",0, " |/====|",0, " ﾟ ---- ",0,
//      5      ........      ........      ........      ........
              "   ｡---｡",0, "   |OO |",0, " ｡-|  /-",0, " | |--|@",0,
              " | |  ﾍ-",0, " | |---+",0, " |/#/== ",0, " ｡ -----",0],

ZAKOPT[26][6]=[ "･=･","\L\L\L\D",
                "|ﾛ|","\L\L\L\D",
                "･=･",0,"   "   ,

                " _ ","\L\L\L\D",
                "|P|","\L\L\L\D",
                " ~ ",0,"   "   ,

                "/ ﾍ","\L\L\L\D",
                " X ","\L\L\L\D",
                "ﾍ /",0,"   "   ,

                "｡-｡","\L\L\L\D",
                "===","\L\L\L\D",
                "|}|",0,"   "   ,

                " | ","\L\L\L\D",
                "-+-","\L\L\L\D",
                " | ",0,"   "   ,

                "ﾍ|/","\L\L\L\D",
                "=*=","\L\L\L\D",
                "/|ﾍ",0,"   "   ,

                " _ ","\L\L\L\D",
                "| |","\L\L\L\D",
                " ~ ",0,"   "   ,

                "   ","\L\L\L\D",
                "   ","\L\L\L\D",
                "   ",0,"   "   ,

                "//ﾍ","\L\L\L\D",
                "%&)","\L\L\L\D",
                "ﾍ%/",0,"   "  ],


              METAOBJ[111][2]=[ %  -16 ,%  -56 ,%  -24  ,
                                %  -16 ,%  -56 ,%    0  ,
                                %  -16 ,%  -48 ,%    8  ,
                                %  -16 ,%  -32 ,%    8  ,
                                %  -16 ,%  -32 ,%  -24  ,
                                %   16 ,%  -56 ,%  -24  ,
                                %   16 ,%  -56 ,%    0  ,
                                %   16 ,%  -48 ,%    8  ,
                                %   16 ,%  -32 ,%    8  ,
                                %   16 ,%  -32 ,%  -24  ,       /* HEAD */

                                %  -32 ,%  -32 ,%  -24  ,
                                %  -32 ,%  -32 ,%    8  ,
                                %  -32 ,%  -24 ,%   24  ,
                                %  -32 ,%    0 ,%   24  ,
                                %  -32 ,%   16 ,%    8  ,
                                %  -32 ,%   16 ,%  -24  ,
                                %  -32 ,%    8 ,%  -32  ,
                                %  -32 ,%  -24 ,%  -32  ,
                                %   32 ,%  -32 ,%  -24  ,
                                %   32 ,%  -32 ,%    8  ,
                                %   32 ,%  -24 ,%   24  ,
                                %   32 ,%    0 ,%   24  ,
                                %   32 ,%   16 ,%    8  ,
                                %   32 ,%   16 ,%  -24  ,
                                %   32 ,%    8 ,%  -32  ,
                                %   32 ,%  -24 ,%  -32  ,       /* BODY */

                                %   36 ,%  -24 ,%    0  ,
                                %   52 ,%  -24 ,%    0  ,
                                %   52 ,%  -16 ,%  -16  ,
                                %   36 ,%  -16 ,%  -16  ,
                                %   36 ,%   -8 ,%    8  ,
                                %   52 ,%   -8 ,%    8  ,
                                %   52 ,%    0 ,%   -8  ,
                                %   36 ,%    0 ,%   -8  ,
                                %   36 ,%   -8 ,%   12  ,
                                %   52 ,%   -8 ,%   12  ,
                                %   52 ,%    8 ,%   12  ,
                                %   36 ,%    8 ,%   12  ,
                                %   36 ,%   -8 ,%   32  ,
                                %   52 ,%   -8 ,%   32  ,
                                %   52 ,%    8 ,%   32  ,
                                %   36 ,%    8 ,%   32  ,
                                %   36 ,%   -8 ,%   40  ,
                                %   52 ,%   -8 ,%   40  ,
                                %   52 ,%    8 ,%   40  ,
                                %   36 ,%    8 ,%   40  ,
                                %   44 ,%    0 ,%   52  ,       /* R.HAND */

                                %  -48 ,%  -16 ,%  -16  ,
                                %  -40 ,%  -16 ,%  -16  ,
                                %  -40 ,%    8 ,%  -16  ,
                                %  -48 ,%    8 ,%  -16  ,
                                %  -48 ,%  -16 ,%    8  ,
                                %  -40 ,%  -16 ,%    8  ,
                                %  -48 ,%   -8 ,%   16  ,
                                %  -40 ,%   -8 ,%   16  ,
                                %  -40 ,%    8 ,%   16  ,
                                %  -48 ,%    8 ,%   16  ,
                                %  -42 ,%   -4 ,%   16  ,
                                %  -46 ,%   -4 ,%   16  ,
                                %  -46 ,%    4 ,%   16  ,
                                %  -42 ,%    4 ,%   16  ,
                                %  -42 ,%   -4 ,%   48  ,
                                %  -46 ,%   -4 ,%   48  ,
                                %  -46 ,%    4 ,%   48  ,
                                %  -42 ,%    4 ,%   48  ,       /* BLUSTER */

                                %   72 ,%  -56 ,%   48  ,
                                %   72 ,%  -32 ,%   64  ,
                                %   72 ,%   16 ,%   64  ,
                                %   72 ,%   16 ,%   56  ,
                                %   72 ,%  -32 ,%   56  ,
                                %   16 ,%  -56 ,%   48  ,
                                %   16 ,%  -32 ,%   64  ,
                                %   16 ,%   16 ,%   64  ,
                                %   16 ,%   16 ,%   56  ,
                                %   16 ,%  -32 ,%   56  ,
                                %   44 ,%   32 ,%   48  ,       /* SHIELD */

                                %   32 ,%   16 ,%  -24  ,
                                %   32 ,%   16 ,%    8  ,
                                %   32 ,%   24 ,%   32  ,
                                %   32 ,%   40 ,%   40  ,
                                %   32 ,%   40 ,%  -40  ,
                                %   32 ,%   32 ,%  -40  ,
                                %  -32 ,%   16 ,%  -24  ,
                                %  -32 ,%   16 ,%    8  ,
                                %  -32 ,%   24 ,%   32  ,
                                %  -32 ,%   40 ,%   40  ,
                                %  -32 ,%   40 ,%  -40  ,
                                %  -32 ,%   32 ,%  -40  ,       /* FOOT 1 */

                                %  -24 ,%   24 ,%  -32  ,
                                %  -24 ,%   24 ,%  -24  ,
                                %  -24 ,%   32 ,%   16  ,
                                %  -24 ,%   48 ,%   24  ,
                                %  -24 ,%   48 ,%   16  ,
                                %  -24 ,%   32 ,%  -24  ,

                                %   -8 ,%   24 ,%  -32  ,
                                %   -8 ,%   24 ,%  -24  ,
                                %   -8 ,%   32 ,%   16  ,
                                %   -8 ,%   48 ,%   24  ,
                                %   -8 ,%   48 ,%   16  ,
                                %   -8 ,%   32 ,%  -24  ,

                                %    8 ,%   24 ,%  -32  ,
                                %    8 ,%   24 ,%  -24  ,
                                %    8 ,%   32 ,%   16  ,
                                %    8 ,%   48 ,%   24  ,
                                %    8 ,%   48 ,%   16  ,
                                %    8 ,%   32 ,%  -24  ,

                                %   24 ,%   24 ,%  -32  ,
                                %   24 ,%   24 ,%  -24  ,
                                %   24 ,%   32 ,%   16  ,
                                %   24 ,%   48 ,%   24  ,
                                %   24 ,%   48 ,%   16  ,
                                %   24 ,%   32 ,%  -24  ],

        BYTE  METAWIR[170][1]=[

        0,1,    1,2,    2,3,    3,4,    4,0,
        5,6,    6,7,    7,8,    8,9,    9,5,
        0,5,    1,6,    2,7,    3,8,    4,9,    /* HEAD */

        10,11,  11,12,  12,13,  13,14,  14,15,  15,16,  16,17,  17,10,
        18,19,  19,20,  20,21,  21,22,  22,23,  23,24,  24,25,  25,18,
        10,18,  11,19,  12,20,  13,21,  14,22,  15,23,  16,24,  17,25,

                                                /* BODY */

        26,27,  27,28,  28,29,  29,26,
        30,31,  31,32,  32,33,  33,30,
        26,30,  27,31,  28,32,  29,33,

        34,35,  35,36,  36,37,  37,34,
        38,39,  39,40,  40,41,  41,38,
        34,38,  35,39,  36,40,  37,41,

        42,43,  43,44,  44,45,  45,42,
        42,46,  43,46,  44,46,  45,46,          /* R.HAND */

        48,49,  49,55,  55,54,  54,52,  52,48,
        47,50,  50,56,  56,53,  53,51,  51,47,
        47,48,  50,49,  56,55,  53,54,  51,52,

        57,58,  58,59,  59,60,  60,57,
        61,62,  62,63,  63,64,  64,61,
        57,61,  58,62,  59,63,  60,64,          /* BLUSTER */

        65,66,  66,67,  67,75,  75,68,  68,69,  69,65,
        70,71,  71,72,  72,75,  75,73,  73,74,  74,70,
        65,70,  66,71,  67,68,  72,73,  69,74,  67,72,  68,73,

                                                /* SHIELD */

        76,77,  77,78,  78,79,  79,80,  80,81,  81,76,
        82,83,  83,84,  84,85,  85,86,  86,87,  87,82,
        76,82,  77,83,  78,84,  79,85,  80,86,  81,87,

                                                /* FOOT 1 */

        88,89,  89,90,  90,91,  91,92,  92,93,  93,88,
        94,95,  95,96,  96,97,  97,98,  98,99,  99,94,
        88,94,  89,95,  90,96,  91,97,  92,98,  93,99,

        100,101, 101,102, 102,103, 103,104, 104,105, 105,100,
        106,107, 107,108, 108,109, 109,110, 110,111, 111,106,
        100,106, 101,107, 102,108, 103,109, 104,110, 105,111],

        DOCKOBJ[35][2]=[        % -24 , % -64 , %-100 ,
                                %  88 , % -64 , %-100 ,
                                %  88 , %  40 , %-100 ,
                                % -88 , %  40 , %-100 ,
                                % -88 , % -40 , %-100 ,
                                % -24 , % -40 , %-100 ,
                                % -24 , % -64 , % 100 ,
                                %  88 , % -64 , % 100 ,
                                %  88 , %  40 , % 100 ,
                                % -88 , %  40 , % 100 ,
                                % -88 , % -40 , % 100 ,
                                % -24 , % -40 , % 100 ,

                                % -64 , % -32 , % -16 ,
                                % -48 , % -32 , % -16 ,
                                % -48 , % -24 , % -16 ,
                                % -56 , % -24 , % -16 ,
                                % -56 , %  16 , % -16 ,
                                % -48 , %  16 , % -16 ,
                                % -48 , %  24 , % -16 ,
                                % -64 , %  24 , % -16 ,
                                % -64 , %   0 , % -16 ,
                                % -88 , %   0 , % -16 ,
                                % -88 , %  -8 , % -16 ,
                                % -64 , %  -8 , % -16 ,

                                % -64 , % -32 , %   8 ,
                                % -48 , % -32 , %   8 ,
                                % -48 , % -24 , %   8 ,
                                % -56 , % -24 , %   8 ,
                                % -56 , %  16 , %   8 ,
                                % -48 , %  16 , %   8 ,
                                % -48 , %  24 , %   8 ,
                                % -64 , %  24 , %   8 ,
                                % -64 , %   0 , %   8 ,
                                % -88 , %   0 , %   8 ,
                                % -88 , %  -8 , %   8 ,
                                % -64 , %  -8 , %   8 ],

        BYTE    DOCKWIR[53][1]=[
                0,1,    1,2,    2,3,    3,4,    4,5,    5,0,
                6,7,    7,8,    8,9,    9,10,   10,11,  11,6,
                0,6,    1,7,    2,8,    3,9,    4,10,   5,11,

                12,13,  13,14,  14,15,  15,16,  16,17,  17,18,
                18,19,  19,20,  20,21,  21,22,  22,23,  23,12,

                24,25,  25,26,  26,27,  27,28,  28,29,  29,30,
                30,31,  31,32,  32,33,  33,34,  34,35,  35,24,

                12,24,  13,19,  14,20,  15,21,  16,22,  17,23,
                18,30,  19,31,  20,32,  21,33,  22,34,  23,35];


#INCLUDE GRAPHF.LIB

VAR     DA,MX,MY,JF,PT,RL,SC,TX,TY,TF,ECT,EW1,EST,TSX,TSY,WX,WY,TWL,TWH,
        LAND,ROUND,MEGY,EEGY,CLR,EW2,EW3,EW4,EW5,EW6,EW7,TGX,TGY,XS,CHF,
        MP,TCF,SHARP_X1,CBYOU,BYOU,FUN,TOGGLE,NANDO,
        LCBYOU,LBYOU,LFUN,RLC;
        //LCBYOU:$9FF0,LBYOU:$9FF2,LFUN:$9FF4,RLC;

ARRAY   MBF[7   ],MBX[7   ],MBY[7   ],MBRL[7   ],
        TBF[30  ],TBX[30  ],TBY[30  ],TBRL[30  ],TBUD[30  ],TDF[30  ],
        ZKF[19  ],ZKX[19  ],ZKY[19  ],ZKW1[19  ],ZKW2[19  ],ZKN[19  ],
        ZKT[8   ]=[%1,%0,%1,%1,%0,%0,%0,%0,%1];

ARRAY   BYTE    SCC[70]=
["ﾛﾛﾛ----ﾛﾛﾛ---ﾛ=ﾛﾛ----ﾛﾛﾛﾛ----ﾛﾛﾛ--=--ﾛﾛﾛﾛﾛ-----ﾛﾛﾛﾛﾛ---=-ﾛﾛﾛﾛ-----ﾛﾛﾛﾛ",0],

        BYTE    JOYRT[23]=
[ $06,$1C,$3E,$07,$ED,$79,$05,$ED,$78,$E6,$3F,$ED,$79,$04,$3E,$0E,$ED,$79,
  $05,$ED,$78,$C9 ],

        BYTE    COMS[26]=["#S LAP TIME:9FF0:9FFF:1FFA",0],
                COML[11]=["#L LAP TIME",0];

MAIN()
    VAR I;
    VAR CNT;
    [
        SHARP_X1=0;
        // SHARP_X1=CODE( $CD,$F7,$1F )/4096;
        SHARP_X1=2;
        IF (SHARP_X1==2) BEEP(); BEEP(); BEEP();
        LCBYOU=9; LBYOU=59; LFUN=15;
        COMMAND(&COML);
MLOOP:
        FOR I=0 TO  7 [ MBF[I]=0; ]
        FOR I=0 TO 30 [ TBF[I]=0; ]
        NANDO=1;
        TITLE(); ROUND=1;
        SCR_INIT();
        MX=0; MY=14; JF=0; DA=0; RL=0; SC=1; TX=70; LAND=1;
        TY=15; TF=0; ECT=0; EST=1; MEGY=100; TOGGLE=0;
        METAPRT(0);
        FOR ROUND=ROUND TO 5
        [
            FOR I=0 TO 19 [ ZKF[I]=0; ]
            CLR=0; ECT=0; EST=1; CHF=0; TCF=0; RLC=0; EEGY=0;
            LOCATE(0,22); PRINT(MSX$(SCC));
            PARPRT1();
            MLOOP1:
            [
                PARPRT();
                METAMOV();
                ENESEL();
                METASHOT();
                TSHOT_M();
                SCROL();
                IF (MEGY.<.0) MEGY=0;
                IF (MEGY==0)
                [
                    FOR I=0 TO 50
                    [
                        ENESEL();
                        DUM();
                        @MODE(2,0);
                        @GRAD(RND(7)+1);
                        @CIRCLE(MX*8+RND(64),MY*8+RND(64),RND(60));
                    ]
                    GOTO MLOOP;
                ]
                //IF (INKEY(0)=='E') [ @PALET(0,0,0,0,0,0,0,0); CALL($1FFA); ]
            ]
            IF (CLR<2) GOTO MLOOP1;
        ]
        GOTO MLOOP;
    ]

//LOADMAGIC()
//    [
//#ASM
//    ; SET MAGIC PATH
//    LD  DE,MAGICPATH
//	LD	HL,$005C
//	LD	C,$29       ; _CHKFN
//	CALL	$0005   ; BDOS
//
//    ; File Open
//	LD	DE,$005C
//	LD	C,$0F   ; FOPEN
//	CALL	$0005   ; BDOS
//
//    ; Get Filesize
//	LD	DE,$005C
//	LD	C,$23       ; GETFSZ
//	CALL	$0005   ; BDOS
//
//	; Set DTA
//	LD	C,$1A       ; SETDTA
//	LD	DE,$B000
//	CALL	$0005   ;BDOS
//
//    ; All Read
//	LD	IX,$005C
//	LD	(IX+14),1
//	LD	(IX+15),0
//	LD	(IX+33),0
//	LD	(IX+34),0
//	LD	(IX+35),0
//	LD	(IX+36),0
//	LD	HL,($005C+16)
//	LD	DE,$005C
//	LD	C,$27		; Random Block Read
//	CALL	$0005   ;BDOS
//
//    ; FCLOSE
//	LD	DE,$005C
//	LD	C,$10		; FCLOSE
//	CALL	$0005   ; BDOS
//    RET
//MAGICPATH:
//    DB "MAGIC.BIN",0
//#END
//    ]

ENESEL()
    [
        CASE ROUND OF
        [
          1 ENE_MOVE1();
          2 ENE_MOVE2();
          3 ENE_MOVE4();
          4 ENE_MOVE3();
          5 ENE_MOVE5();
        ]
    ]

SCR_INIT()
    [
        @INIT();
        @WINDOW(0,0,559,199);
    ]

COMMAND(POSIT)
    [
        ^DE=POSIT;
        //CALL($211B);
    ]

ZAKOPRT(I)
    [
        PRINT(MSX$(&ZAKOPT+I*21));
    ]

METAPRT(P)
    VAR I,J;
    [
        I=(DA*32+RL*16+P*8)*9;
        FOR J=0 TO 7
        [
          LOCATE(MX,MY+J); PRINT(MSX$(&MYCH+I+J*9));
        ]
    ]

METAMOV()
    VAR KEY,F,T,JOY;
    [
        F=0; T=0; KEY=0;
        IF (SHARP_X1==2)
        [
            JOY=STICK(); IF (JOY==255) GOTO METAKEY;
            IF  ((JOY AND 64)==0) AND (RLC==0) [ T=1; RL=1-RL; RLC=2; ]
            IF  ((JOY AND  4)==0) LEFT();
            IF  ((JOY AND  8)==0) RIGHT();
            IF  ((JOY AND  1)==0) AND (LAND==1) [ JF=7; LAND=0; ]
            IF  ((JOY AND  2)==0) JF=0;
            GOTO METAEND;
        ]
METAKEY:
        KEY=INKEY(0);
        CASE KEY OF
        [
            '4' LEFT();
            '6' RIGHT();
            '8' IF (JF==0) AND (LAND==1) [ JF=7; LAND=0; ]
            '3' IF (RLC==0) AND (RL==1) [ T=1; RL=0; RLC=2; ]
            '1' IF (RLC==0) AND (RL==0) [ T=1; RL=1; RLC=2; ]
            '2' JF=0;
            ' ' TOGGLE=32;
            '5' TOGGLE=0;
        ]
METAEND:
        IF (RLC>0) RLC--;
        IF (KEY=='/') PAUSE();
        IF (JF>0) [ JF--; UP(); ]
        IF (JF==0) AND (MY<13) DOWN();
        IF (MY==14) LAND=1;
        METAPRT(T);
    ]

STICK()
    VAR JOY;
    [
        CALL(&JOYRT);
        JOY=^A;
        RETURN(JOY);
   ]

PAUSE()
    [
        BEEP(); BEEP();
        WHILE(INKEY(0)==0) []
        BEEP(); BEEP();
    ]

TERASE(ZX,ZY)
    [
        IF (ZX.>.-1) AND (ZX.<.68) AND
           (ZY.>.-1) AND (ZY.<.22)
        [
            LOCATE(ZX,ZY);
            PRINT("   \L\L\L\D   \L\L\L\D   ");
        ]
    ]

TPRINT(ZX,ZY,ZN)
    [
        IF (ZX.>.-1) AND (ZX.<.68) AND
           (ZY.>.-1) AND (ZY.<.22)
        [
            LOCATE(ZX,ZY); ZAKOPRT(ZN);
        ]
    ]

ZAKOMOV()
    VAR I,ZX,ZY,BX,BY,L,F,G;
    [
        F=0;
        IF (ROUND==3) OR (NANDO>1) G=1; ELSE G=0;
        FOR I=0 TO 19
        [
            IF (ZKF[I]>0)
            [
              ZX=ZKX[I]; ZY=ZKY[I];
              TERASE(ZX,ZY);
              CASE ZKN[I] OF
              [
                  0 [ ZKW1[I]=((ZKW1[I]+MP) MOD 360);
                      ZX=@ICOS(ZKW1[I],ZKW2[I]).*.2+TSX-2;
                      ZY=@ISIN(ZKW1[I],ZKW2[I])    +TSY-1;
                                                                 ]
                  1 [ ZY=ZY+SGN(19-ZY);
                      ZX=ZX-SC;
                      IF (ZX.<.MX+7) AND (ZX+2.>.MX) AND
                         (ZY.<.MY+7) AND (ZY+2.>.MY)
                      [
                        IF (ROUND==5) AND (NANDO>1)
                        [ MEGY=MEGY-8; ]
                        MEGY=MEGY+15; ZKF[I]=0; BEEP();
                      ]                                          ]
                  3 [
                      IF (ABS(ZKW1[I])<20) ZKW1[I]=ZKW1[I]+SGN(TGX-ZX);
                      IF (ABS(ZKW2[I])<10) ZKW2[I]=ZKW2[I]+SGN(TGY-ZY);
                      IF (RND(100)>75)
                      [
                        BX=TSX; BY=TSY;
                        TSX=ZX; TSY=ZY;
                        CASE NANDO OF
                        [
                           0 TO 1       TSHOT_I(ZKW1[I]*60,ZKW2[I]*65);
                                2       HAPPOU(1);
                           3 TO 4       HAPPOU(2);
                        ]
                        TSX=BX; TSY=BY;
                      ]
                      TERASE(ZX,ZY);
                      ZX=ZX+ZKW1[I]./.6;
                      ZY=ZY+ZKW2[I]./.3;
                      IF (ZY.>.23) [ ZY=23; ZKW2[I]=-RND(4);     ]
                      IF (ZY.<. 0) [ ZY= 0; ZKW2[I]= RND(4);     ]
                      IF (CHF<2)
                      [
                          IF (ZX.<. 0) [ ZX= 0; ZKW1[I]= RND(4);     ]
                          IF (ZX.>.69) [ ZX=69; ZKW1[I]=-RND(4);     ]
                      ]
                                                                 ]
               4 TO 6 ZKN[I]++;
                  7 [ ZKF[I]=0;
                      FOR L=10 TO 19
                      [
                          IF (ZKF[L]==0) AND (G==1) AND (F==0)
                          [
                              ZKX[L]=ZKX[I]; ZKY[L]=ZKY[I];
                              ZKF[L]=1; ZKN[L]=1; F=1;
                          ]
                      ]                                         ]
                  8 [
                     CASE I OF
                      [
                        9      [ ZKW1[9]=ZX; ZKW2[9]=ZY;
                                 ZX=ZKX[0]-ZKW1[0]./.5;
                                 ZY=ZKY[0]-ZKW2[0]./.3;         ]
                        1 TO 8 [ ZKW1[I]=ZX; ZKW2[I]=ZY;
                                 ZX=ZKW1[I+1]; ZY=ZKW2[I+1];    ]

                      ]
                    ]
              ]
              IF (CHF==0) AND (ZKN[I]<>1) AND
              (ZKT[ZKN[I]]==1) CRUSH(ZX,ZY,ZKN[I],I);
              TPRINT(ZX,ZY,ZKN[I]);
              ZKX[I]=ZX; ZKY[I]=ZY;
            ]
        ]
    ]

CRUSH(ZX,ZY,N,I)
    [
        IF (ZX.<.MX+7) AND (ZX+2.>.MX) AND
           (ZY.<.MY+7) AND (ZY+2.>.MY)
        [
            @FLASH(0,2);
            MEGY--; UP(); UP(); UP(); JF=0; LAND=1;
            IF (N==8)
            [
                IF (ZKW1[I].<.0) [ RIGHT(); RIGHT(); ]
                            ELSE [ LEFT();  LEFT();  ]
            ]
        ]
    ]

METASHOT()
    VAR I,J,K,L,F,GX,GY,HX,HY,JOY,KEY;
    [
        F=0; DA=0;
        FOR I=0 TO 4
        [
          JOY=0; IF (SHARP_X1==2) JOY=(32-(STICK() AND 32));
          IF (JOY==0) KEY=TOGGLE;
          IF (MBF[I]==0) AND (F==0) AND (JOY+KEY==' ')
          [
            MBF[I]=1;    MBRL[I]=(1-RL)*128-64; DA=1;
            MBX[I]=MX*8; MBY[I]=(MY+3)*8+3; F=1;
          ]
          IF (MBF[I]==1)
          [
            @MODE(0,2);
            @LINE(MBX[I],MBY[I],MBX[I]+64,MBY[I]);
            MBX[I]=MBX[I]+MBRL[I];
            GX=MBX[I]/8; GY=MBY[I]/8;
            IF (ABS(TX-GX+4+WX).<.XS) AND (TCF==0) AND
               (GY<TY+WY+TWL) AND (GY>TY+WY-TWH)
            [
                @PALET(0,2,2,2,1,2,2,1); MBX[I]=10000;
                EEGY--;
                IF (EEGY.<.0) [ EEGY=0; CLR=1; ]
            ]
            FOR J=0 TO 9
            [
                IF (ABS(ZKX[J]-GX)<8) AND (ZKF[J]<>0) AND
                   (ABS(ZKY[J]-GY)<3) AND (ZKT[ZKN[J]]==1) AND
                   (CHF==0)
                [
                    @PALET(0,1,RND(8),RND(8),RND(8),RND(8),RND(8),RND(8));
                    K=MBX[I]; MBX[I]=10000;
                    IF (ZKF[J].>.0) AND (ZKT[ZKN[J]]==1)
                    [
                        ZKF[J]--;
                        IF (ZKF[J]==0)
                        [
                            ZKF[J]=1; ZKN[J]=4; EEGY--;
                            IF (EEGY.<.0) [ EEGY=0; CLR=1; ]
                        ]
                    ]
                    IF (ZKF[J]==-1) [ MBRL[I]=-MBRL[I];
                                      MBX[I]=K; J=9;
                                      IF (NANDO==4) TSHOT_I(MBRL[I]*15,0); ]

                ]
            ]
            IF (MBX[I].<.-64) OR (MBX[I].>.560) MBF[I]=0;
            ELSE
              @MODE(2,2); @LINE(MBX[I],MBY[I],MBX[I]+64,MBY[I]);
          ]
        ]
        @MODE(2,1-_MSCREEN);
    ]

LEFT()
    VAR I;
    [
        FOR I=0 TO 7 [ LOCATE(MX+6,MY+I); PRINT("  "); ]
        MX--; MX--;
        IF (MX.<.0) MX=0;
    ]

RIGHT()
    VAR I;
    [
        FOR I=0 TO 7 [ LOCATE(MX  ,MY+I); PRINT("  "); ]
        MX++; MX++;
        IF (MX.>.62) MX=62;
    ]

UP()
    [
        IF (MY<2) RETURN;
        LOCATE(MX,MY+6); PRINT("        ");
        LOCATE(MX,MY+7); PRINT("        ");
        MY--; MY--;
    ]

DOWN()
    [
        IF (MY>12) RETURN;
        LOCATE(MX,MY  ); PRINT("        ");
        LOCATE(MX,MY+1); PRINT("        ");
        MY++; MY++;
    ]

SCROL()
    VAR I,J;
    [
        IF (SC==0) RETURN;
        FOR J=0 TO SC
        [
            I=SCC[0];
            CODE($01,%69,$21,%SCC+1,$11,%SCC,$ED,$B0);
            SCC[69]=I;

        ]
        LOCATE(0,22); PRINT(MSX$(SCC));
    ]

ENE_PRT1()
    VAR EX,EY;
    [
        @MODE(2,1-_MSCREEN);
        EX=TX*8; EY=TY*8;
        @LINE(EX,EY+56,EX+80,EY);
        @LINE(EX+80,EY,EX+192,EY);
        @SPLINE(EX+192,EY,EX+224,EY+24,EX+240,EY+56);
        @LINE(EX+240,EY+56,EX+192,EY+56);
        @LINE(EX+192,EY+56,EX+192,EY+40);
        @LINE(EX+192,EY+40,EX+64,EY+40);
        @LINE(EX+64,EY+40,EX,EY+56);
        @GRAD(6);
        @FULL(EX+96,EY+16,EX+176,EY+24);
        @CIRCLE(EX+80,EY+48+TF,16);
        @CIRCLE(EX+128,EY+48+TF,16);
        @CIRCLE(EX+176,EY+48+TF,16);
        TF=4-TF;
        @CRTKN(5,1,0);
        @GRAD(0);
        @FULL(EX-10,EY,EX+264,EY+70);
        IF (TX<MX+6) AND (TX+27>MX) AND (TY<MY+4) AND (CHF==0)
        [
            @FLASH(0,2); MEGY=MEGY-5;
            IF (TX+15>MX+4) LEFT(); ELSE RIGHT();
        ]
        IF (TX<MX+6) AND (TX+27>MX) AND (TY<MY+8)
        [
            IF (TY<MY+8) UP();
            IF (JF==0) LAND=1;
        ]
        ELSE IF (MY<14) THEN LAND=0;
    ]

ENE_PRT2()
  [
        @PALET(0,0,0,0,0,0,0,0);
        @CFULL(0,0,559,168,2,1,0);
        @MODE(2,0);
        @GRAD(4);
        @TRIANGLE(0,100,40,120,0,120);
        @TRIANGLE(559,100,519,120,559,120);
        @FULL(0,120,559,160);
        @GRAD(0);
        @CIRCLE(200,80,140);
        @CIRCLE(360,80,140);
  ]

ENE_PRT3(R)
  VAR   X1,Y1,X2,Y2,X3,Y3,I,J,K;
  [
        TSX=TX; TSY=TY; I=360-EW1;
        @GRAD(4);
        J=TX*8; K=TY*8;
        X1=@ICOS(EW1,EW2*2)+J;
        X2=@ICOS(EW1+120,EW2*2)+J;
        X3=@ICOS(EW1+240,EW2*2)+J;
        Y1=@ISIN(EW1,EW2  )+K;
        Y2=@ISIN(EW1+120,EW2  )+K;
        Y3=@ISIN(EW1+240,EW2  )+K;
        CASE NANDO OF
        [
         0 TO 2         @TRIANGLE(X1,Y1,X2,Y2,X3,Y3);
         3 TO 4 [       @LINE(X1,Y1,X2,Y2);
                        @LINE(X2,Y2,X3,Y3);
                        @LINE(X3,Y3,X1,Y1);             ]
        ]
        @GRAD(7);
        X1=@ICOS(  I,EW2*2)+J;
        X2=@ICOS(  I+120,EW2*2)+J;
        X3=@ICOS(  I+240,EW2*2)+J;
        Y1=@ISIN(  I,EW2  )+K;
        Y2=@ISIN(  I+120,EW2  )+K;
        Y3=@ISIN(  I+240,EW2  )+K;
        CASE NANDO OF
        [
         0 TO 2         @TRIANGLE(X1,Y1,X2,Y2,X3,Y3);
         3 TO 4 [       @LINE(X1,Y1,X2,Y2);
                        @LINE(X2,Y2,X3,Y3);
                        @LINE(X3,Y3,X1,Y1);             ]
        ]
        CASE NANDO OF
        [
         0 TO 2 @CRTKN(7,4,0);
         3 TO 4 @CRTKN(RND(3)+5,4,0);
        ]
        EW1=((EW1+R) MOD 360);
        @GRAD(0);
        I=(TX-EW3)*8; J=(TY-EW4)*8;
        @FULL(I-EW5*2-10,J-EW5-10,I+EW5*2+10,J+EW5+10);
        TWL=EW2/8+1; TWH=TWL;
        IF (MX+4>TX-8) AND (MX+4<TX+8) AND (MY+4>TY-5) AND
           (MY+4<TY+5) AND (CHF==0)
        [
            @FLASH(0,2); MEGY--;
        ]
  ]

ENE_PRT4()
    [
        TPRINT(TX-1,TY-1,2);
        CRUSH(TX,TY,3,10);
        @CRTKN(1,RND(7)+1,0);
    ]

ENE_PRT5()
    [
        @PALET(0,0,0,0,0,0,0,0);
        @MODE(2,0);
        @GRAD(1); @TRIANGLE(0,160,400,0,400,160);
        @GRAD(2); @CIRCLE(432,64,192);
        @GRAD(5); @FULL(160,0,560,32);
                  @FULL(496,0,560,160);
                  @FULL(0,152,560,160);
                  @TRIANGLE(224,152,496,136,496,152);
                  @TRIANGLE(0,0,160,0,160,32);
        @GRAD(2); @CIRCLE(432,64,128);
        @GRAD(0); @CIRCLE(432,64, 32);
    ]

ENE_MOVE1()
    VAR I;
    [
        TIME();
        TSX = TX + 12;
        TSY = TY +  2;
        CASE EST
        [
            1 [
                EEGY = 40 + NANDO * 10;
                WX   =  9;    WY   =  2;
                XS   =  6;    TWL  =  2;
                TWH  =  2;    EST++;
              ]

            2 [
                TX--;
                IF (TX == 43)   EST++;
              ]

            3 [
                ECT++;
                IF (ECT > 30)
                [
                  ECT = 0;
                  EST++;
                ]
                TSHOT_I(-800 , 0);
              ]

            4 [
                TX--;
                IF (TX<  25) EST++;
                TSHOT_I(400 , 0);
              ]

            5 [
                TX = TX - 3;
                IF (TX < 5)  EST++;
                TSHOT_I(800 , -200);
                TSHOT_I(800 ,    0);
                TSHOT_I(800 ,  200);
              ]

            6 [
                TX++;
                IF (TX > 38)    EST = 3;
                IF (NANDO > 2)  HOMING(NANDO + 1 , 1);
                TSHOT_I(-800 , -200);
                TSHOT_I(-800 ,    0);
                TSHOT_I(-800 ,  200);
                TSHOT_I( 200 ,    0);
              ]
        ]
        ENE_PRT1();
        IF (CLR == 1)
        [
            FOR I = 0 TO 60
            [
                @PALET(0 , 2 , 2 , 2 , 2 , 2 , 2 , 2);
                @GRAD(RND(7) + 1);
                @CIRCLE(TX * 8 + RND(200) , TY * 8 + RND(70) , 40 + RND(30));
                DUM();
            ]
            CLS3();
            TX = 100;
            FOR I = 0 TO 100 [ DUM(); ]
            CLR++;
        ]
    ]

DUM()
    [
        SCROL();
        METASHOT();
        ZAKOMOV();
        TSHOT_M();
        METAMOV();
    ]


ENE_MOVE2()
    VAR I,J;
    [
        TIME();
        @PALET(0 , 1 , 2 , 3 , 5 , 5 , 2 , 5);
        CASE EST
        [
            1 [
                ENE_PRT2();
                @MODE(2 , 0);
                TY   =  10;                 TSX  = 100;
                SC   =   0;                 WY   =   0;
                TWL  =   5;                 TWH  =   9;
                TX   =  25;                 EST  =   2;
                WX   =   0;                 XS   =  10;
                EEGY = 125 + NANDO * 25;
              ]

            2 [
                ECT++;
                @GRAD(ECT);
                @MODE(2 , 0);
                @CIRCLE(TX * 8 , 80 , 120);
                @MODE(2 , 2);
                IF (ECT > 5)
                [
                  ECT = 0;
                  EST = 3;
                ]
              ]

            3 [
                WA();
                ECT++;
                IF (ECT > 40)
                [
                  ECT = 0;
                  EST = 4;
                ]
              ]

            4 [
                CASE NANDO + 1 OF
                [
                    1 TO 2   TSHOT_I(@ICOS(ECT*30,600) , @ISIN(ECT*30,400));
                    3 TO 5   HAPPOU(NANDO);
                ]
                ECT++;
                IF (ECT > 12)
                [
                  ECT = 0;
                  EST = 5;
                ]
              ]

            5 [
                ECT++;
                @GRAD(6 - ECT);
                @MODE(2 , 0);
                @CIRCLE(TX * 8  ,  80 , 120);
                @MODE(2 , 2);
                IF (ECT > 5)
                [
                  ECT = 0;
                  EST = 6;
                  TY = 100;
                ]
              ]

            6 [
                ECT++;
                IF (ECT > 30)
                [
                  ECT = 0;
                  EST = 7;
                ]
              ]

            7 [
                IF (TX == 25)  TX = 45;   ELSE   TX = 25;
                TY  = 10;
                EST =  2;
              ]
        ]
        IF (NANDO == 4) HOMING(2 , 1);
        IF (  CLR == 1)
        [
            SC = 1;
            FOR I = 0 TO 10 [ DUM(); ]
            CLS3();
            TX = 100;
            FOR I = 0 TO 110 [ DUM(); ]
            CLR++;
        ]
    ]

WA()
    VAR I,J,K;
    [
        J = SGN(MX - TX);
        K = SGN(MY - TY);
        IF (MY < 11) AND (MY > 6)  K = 0;
        EW1 = 31;

        FOR I = 0 TO 30 [ EW1 = EW1 - TBF[I]; ]

        IF (EW1 > 17)
        [
            FOR I = 0 TO 17
            [
                TSX = @ICOS(I * 20 , 14) + TX;
                TSY = @ISIN(I * 20 , 7 ) + TY;
                TSHOT_I(J .*. 600  ,  K .*. 200);
            ]
        ]
        TSX = TX;  TSY = TY;
    ]

ENE_MOVE3()
    VAR I , J , K;
    [
        I = TX; J = TY;
        CASE EST OF
        [
            1 [
                TX  =  40;                 TY  =  10;
                EW1 =   0;                 EW2 = 332;
                SC  =   1;                 EW6 =   0;
                EW7 =   0;                 WX  =   0;
                WY  =   0;                 XS  =   5;
                EEGY = 160 + NANDO * 10;   EST++;
                ECT=0;                     @MODE(2 , 0);
              ]

            2 [
                ENE_PRT3(5);
                ECT++;
                EW2 = EW2 - 4;
                IF (ECT > 75) [ ECT = 0;  EST++; ]
              ]

            3 [
                ENE_PRT3(5);
                ECT++;
                HAPPOU(3);
                ECT++;
                TIME();
                IF (ECT>30) [ ECT=0; EST++; ]
              ]

            4 [
                EW6 = EW6 + SGN(MX - TX + 4);
                EW7 = EW7 + SGN(MY - TY + 4);
                IF (NANDO == 0)
                [
                    IF (EW6 .<. -3)  EW6 = -3;
                    IF (EW6 .>.  3)  EW6 =  3;
                    IF (EW7 .<. -3)  EW7 = -3;
                    IF (EW7 .>.  3)  EW7 =  3;
                ]
                ENE_PRT3(6);
                TX = TX + EW6;
                TY = TY + EW7;
                TIME();
                IF (TX .<.  0) [ TX =  0;  EW6 =    -EW6; ]
                IF (TX .>. 69) [ TX = 69;  EW6 =    -EW6; ]
                IF (TY .<.  0) [ TY =  0;  EW7 =  RND(3); ]
                IF (TY .>. 23) [ TY = 23;  EW7 = -RND(3); ]
                ECT++;
                TSHOT_I(EW6 .*. 200  ,  EW7 .*. 200);
                IF (ECT > 30) [ ECT = 0; EST++; ]
              ]

            5 [
                ENE_PRT3(6);
                EST++;
                TIME();
                CASE NANDO OF
                [
                    0   [
                          ZKF[0] =  1;
                          ZKN[0] =  1;
                          ZKX[0] = TX;
                          ZKY[0] = TY;
                        ]
                1 TO 2    HAPPOU(3);
                3 TO 4    WA();
                ]
                EW6 = RND(10) - 5;
                EW7 = RND( 8) - 4;
              ]

            6 [
                ENE_PRT3(6);
                ECT++;
                HOMING(2 , 1);
                TIME();
                IF (ECT > 20)
                [
                  ECT = 0;
                  EST = 4;
                  EW6 = 0;
                  EW7 = 0;
                ]
              ]
        ]
        ZAKOMOV();
        IF (CLR == 1)
        [
            SC = 0;
            FOR TY = TY TO 40
            [
                FOR K = 0 TO 3
                [
                    I = TX;
                    J = TY;
                    ENE_PRT3(10);
                    EW2++;
                    DUM();
                    EW3 = TX - I;
                    EW4 = TY - J;
                    EW5 = EW2;
                 ]
            ]
            CLS3();
            TX = 100;
            SC = 1;
            FOR K = 0 TO 100
            [
              DUM(); HOMING(3 , 2);
             ]
            CLR++;
        ]
        EW3 = TX - I;
        EW4 = TY - J;
        EW5 = EW2;
    ]

ENE_MOVE4()
    VAR I , J;
    [
        TIME();
        TSX = TX;
        TSY = TY;
        CASE EST OF
        [
            1 [
                FOR I = 0 TO 9
                [
                    ZKW1[ I] = (I AND 1) * 180;
                    ZKW2[ I] = 36 + (I AND 14);
                    ZKN[  I] =   0;
                    ZKF[  I] =  -1;
                    ZKX[  I] = 100;
                    IF (NANDO==0) ZKF[I]=3;
                ]
                EEGY = 70;
                WX   =  0;
                WY   =  0;
                XS   =  4;
                MP   = 10;
                TX   = 70;
                TY   = 14;
                TWL  =  3;
                TWH  =  3;
                ECT  =  0;
                EST++;
              ]

            2 [
                ECT++;
                FOR I = 0 TO 9 [ ZKW2[I]--; ]
                TERASE(TX - 1  ,  TY - 1);
                TX--;
                ENE_PRT4();
                ZAKOMOV();
                IF (ECT > 30) [ ECT = 0;  EST++; ]
              ]

            3 [
                ENE_PRT4();
                ZAKOMOV();
                HAPPOU(1);
                HOMING(3 , 1);
                EST++;
              ]

            4 [
                ENE_PRT4();
                ZAKOMOV();
                HOMING(3 , 1);
                ECT++;
                IF (ECT > 50) [ ECT = 0;  EST++; ]
              ]

            5 [
                ENE_PRT4();
                ZAKOMOV();
                HOMING(3 , 1);
                FOR I = 0 TO 9 [ ZKW2[I]++; ]
                TERASE(TX - 1  ,  TY - 1);
                TX--;
                ENE_PRT4();
                ZAKOMOV();
                ECT++;
                IF (ECT > 30) [ ECT = 0;  EST++; ]
              ]

            6 [
                ENE_PRT4();
                ZAKOMOV();
                HOMING(1 , 2);
                TSHOT_I(RND(7) - 3  ,  RND(7) - 3);
                TERASE(TX - 1  ,  TY - 1);
                TX++;
                ENE_PRT4();
                ZAKOMOV();
                ECT++;
                IF (ECT > 61) [ ECT = 0;  EST = 2; ]
              ]
        ]
        IF (CLR == 1)
        [
            FOR I = 0 TO 9 [ ZKN[I] = 1; ]
            TGX = TX;
            TGY = TY;
            FOR TY = TY TO 30
            [
                FOR J = 0 TO 5
                [
                    ENE_PRT4();  DUM();  TERASE(TX - 1  ,  TY - 1);
                ]
            ]
            FOR I = 0 TO 9 [ ZKF[I] = 0; ]
            CLR++;
        ]
    ]

ENE_MOVE5()
    VAR I , J;
    [
        TIME();
        @PALET(0 , 1 , 1 , 1 , 6 , 1 , 1 , 7);
        TSX = TX;
        TSY = TY;
        CASE EST OF
        [

            1 [
                TX    = 54;   TY  = 9;   TWL = 4;   TWH = 7;
                WX    =  0;   WY  = 0;   XS = 13;   SC  = 0;
                MP    =  1;   TCF = 1;

                FOR I = 0 TO 9  [ ZKF[I] = 0; ]
                EEGY = 50; EST++;
                ENE_PRT5();
                PARPRT1();
              ]

            2 [
                TGX  =  TX;
                TGY  =  TY;
                HOSEI();
                IF (EEGY < 25)  AND  (NANDO > 0)
                [
                  ECT=0;
                  EST=4;
                ]
                ZAKOOUT(20 , 9);
                ZAKOMOV();
                ZKF[0] = -1;
                ECT++;
                IF (ECT>30) [ ECT=0; EST++; ]
              ]

            3 [
                TGX = MX;
                TGY = MY;
                HOSEI();
                IF (EEGY < 25)  AND  (NANDO > 0)
                [
                  ECT = 0; EST++;
                ]
                ZAKOOUT(20 , 9);
                ZAKOMOV();
                ZKF[0] = -1;
                ECT++;
                IF (ECT > 30) [ ECT = 0;  EST = 2; ]
              ]

            4 [
                @MODE(2 , 1);
                @GRAD(ECT);
                @CIRCLE(432 , 64 , 112);
                ZAKOOUT(20 , 9);
                ZAKOMOV();
                ECT++;
                IF (ECT > 7) [ EST++;  ECT = 0; ]
              ]

            5 [
                WA();
                ZAKOOUT(20 , 9);
                ZAKOMOV();
                EST++;
              ]

            6 [
                HAPPOU(2);
                ZAKOOUT(20 , 9);
                ZAKOMOV();
                EST++;
              ]

            7 [
                TGX = TX;
                TGY = TY;
                ZAKOOUT(20 , 9);
                ZAKOMOV();
                HOMING(3 , 2);
                ECT++;
                IF (ECT > 70) [ ECT= 0 ;  EST++; ]
              ]

            8 [
                TGX = MX;
                TGY = MY;
                ZAKOOUT(20 , 9);
                ZAKOMOV();
                HOMING(3 , 2);
                ECT++;
                IF (ECT > 60) [ ECT = 0;  EST++; ]
              ]

            9 [
                ZAKOOUT(20 , 9);
                ZAKOMOV();
                HOMING(2 , 2);
                WA();
                ECT++;
                IF (ECT > 100) [ ECT = 0;  EST = 5; ]
              ]
        ]
        IF (CLR == 1)
        [
            @MODE(2 , 0);
            FOR I = 6 DOWNTO 0
            [
                @MODE(2 , 0);  @GRAD(I);  @CIRCLE(432 , 64 , 112);
                @MODE(2 , 1);             @CIRCLE(432 , 64 , 112);
            ]
            _DEMODE = 2; CHF = 1;
            FOR I = 0 TO 99

            [
                ZAKOOUT(30 , 9);
                DUM();
                @PALET(0 , RND(7) + 1 ,  2   , 3  ,  4  ,  5  ,  6  ,  7);
                @CLINE(0 ,            I * 2 , 560 ,            I * 2 , 0);
                @CLINE(0 , (99 - I) * 2 + 1 , 560 , (99 - I) * 2 + 1 , 0);
            ]

            SC     = 3;
            ZKN[9] = 4;
            ZKF[9] = 1;
            FOR I = 0 TO 100
            [
                DUM();
                FOR J = 8 DOWNTO 1
                [
                    IF (ZKF[J + 1] == 0) [ ZKF[J] = 1;  ZKN[J] = 4; ]
                ]
            ]
            ENDING();
            CLR++;
        ]
    ]

HOSEI()
    VAR I;
    [
        FOR I = 0 TO 30
        [
            IF (TBF[I] > 0)
            [
                IF (ABS(TBRL[I]) < 300)  TBRL[I] = SGN(MX - TBX[I]) * 300;
                IF (ABS(TBUD[I]) < 300)  TBUD[I] = SGN(MY - TBY[I]) * 300;
            ]
        ]
    ]

ZAKOOUT(POW , MAX)
    VAR I;
    [
        FOR I = 0 TO MAX
        [
            IF (ZKF[I] == 0)
            [
                ZKF[I]  =  POW;
                ZKX[I]  =  TX-1;    ZKY[I]   =  TY - 1;
                ZKW1[I] =  0;       ZKW2[I]  =       0;
                IF (I > 0)
                [
                  ZKN[I]  =  8;
                  ZKW1[I] = -1;
                  @PALET(0 , 6 , 5 , 7 , 1 , 1 , 1 , 1);
                ]
                ELSE
                [
                  ZKN[I] = 3;
                ]
            ]
        ]
    ]

HAPPOU(R)
    VAR I,J;
    [
        J = 31;

        FOR I = 0 TO 30 [ J = J - TBF[I]; ]
        IF (J > 8)
        [
            I=200*R;
            TSHOT_I( 0      , -I);    TSHOT_I( I * 2  ,  -I);
            TSHOT_I( I * 2  ,  0);    TSHOT_I( I * 2  ,   I);
            TSHOT_I(     0  ,  I);    TSHOT_I(-I * 2  ,   I);
            TSHOT_I(-I * 2  ,  0);    TSHOT_I(-I * 2  ,  -I);
        ]
    ]

HOMING(R , S)
    VAR I;
    [
        FOR I = 0 TO 30
        [
            CASE S OF
            [
            1   IF (TBF[I] == 1)
                [
                    TBRL[I] = TBRL[I] + SGN(MX - TBX[I] + 3) .*. 100 .*. R;
                    TBUD[I] = TBUD[I] + SGN(MY - TBY[I] + 3) .*.  50 .*. R;
                ]
            2   IF (TBF[I] == 1) AND (TDF[I] == 0)
                [
                    TBRL[I] = TBRL[I] + SGN(MX - TBX[I]) .*. 100 .*. R;
                    TBUD[I] = TBUD[I] + SGN(MY - TBY[I]) .*.  50 .*. R;
                ]
            ]
        ]
    ]

TSHOT_I(GX , GY)
    VAR I , F;
    [
        F = 0;
        FOR I = 0 TO 30
        [
            IF (TBF[I] == 0) AND (F == 0)
                [
                TBX[I]  = TSX;  TBY[I]  = TSY;
                TBRL[I] =  GX;  TBUD[I] =  GY;
                TDF[I]  =   0;  TBF[I]  =   1;
                F       =   1;
                ]
        ]
    ]

TSHOT_M()
    VAR I , GX , GY , GRL , GLR , SCHR;
    ARRAY BYTE SF[3] = ["<>^v"];
    [
        FOR I = 0 TO 30
        [
            GRL = 0;
            CASE RL OF
            [
              0 [ IF (TBRL[I] .<. 0)  GRL = 0;  ELSE  GRL = 1; ]
              1 [ IF (TBRL[I] .>. 0)  GRL = 0;  ELSE  GRL = 1; ]
            ]
            GX = TBX[I]; GY = TBY[I];
            IF (TBF[I] == 1)
            [
                LOCATE(GX , GY); PRINT(" ");
                GX = GX + (TBRL[I] ./. 200);
                GY = GY + (TBUD[I] ./. 200);
                IF (GX .>. MX - 1)  AND  (GX .<. MX + 8)  AND
                   (GY .>. MY - 1)  AND  (GY .<. MY + 8)
                [
                    IF (DA == 0) AND (GRL == 0)
                    [
                        TBUD[I] = RND(900) - 400;
                        TBRL[I] = 0 - TBRL[I];
                        TDF[I]  = 1;
                        GX = MX + 3 - RL * 10;
                        GX = GX + (TBRL[I] ./. 100);
                        GY = GY + (TBUD[I] ./. 100);
                    ]
                    ELSEIF (CHF == 0)
                    [
                        @FLASH(0 , 2); GX = 10000;
                        MEGY--;
                        IF (SGN(TBRL[I]) == 1) RIGHT(); ELSE LEFT();
                    ]
                ]
                IF (GX .<. 0)  OR  (GX .>. 69)  OR
                   (GY .<. 0)  OR  (GY .>. 20)
                [
                    TBF[I] = 0;
                ]
                ELSE
                [
                    IF (ABS(TBRL[I]) >  ABS(TBUD[I]) * 2)
                        IF (TBRL[I] .<. 0) SCHR = 0; ELSE SCHR = 1;
                    ELSE
                        IF (TBUD[I] .<. 0) SCHR = 2; ELSE SCHR = 3;
                    LOCATE(GX , GY); PRINT(STR$( SF[SCHR] , 1));
                ]
            ]
        TBX[I] = GX; TBY[I] = GY;
        ]
    ]

TITLE()
    VAR I,KEY;
    [
        @INIT();

        CBYOU=0; BYOU=0; FUN=0;
        FOR I = 0 TO 8 [ _PAR[I] = 0; ]

//        LOOP{
//            KEY=INKEY(0);
//            PRINT(KEY,/);
//        }

        @SETOD(&METAOBJ , 111);
        @SETWD(&METAWIR , 170);
        _PAR[_HEAD] =  180;
        _PAR[    2] =  100;
        _PAR[    1] = - 10;
        @MODE(2 , 2);
        LOCATE(34 , 10); PRINT("METAL FIGHT");

        @MAGIC(0); @MAGIC(1);
        @SETOD(&DOCKOBJ , 35);
        @SETWD(&DOCKWIR , 53);
        @MODE(2 , 0);


        //WHILE (TRUE)
        //[
        KEY=0;
        WHILE (KEY == 0)
        [
            @MAGIC(0);
            @MAGIC(1);
            @CRTKN(1 , 2 , 1);
            @CLS();
            KEY = PUSH_KEY();
            //TSHOT_M();
            //HOMING(3,2);
        ]

        //TSX=20;
        //TSY=10;
        //ECT=0;
        //FOR I=0 TO 11 [
        //    TSHOT_I(@ICOS(ECT*30,600) , @ISIN(ECT*30,400));
        //    ECT++;
        //]
        //]





        KEY = INKEY(0);
        CASE KEY OF
        [
            'K' NANDO = 0;
            'A' NANDO = 2;
            'B' NANDO = 3;
            'C' NANDO = 4;
        ]
#IF (_LSX!=0)
        _LSXCLS();
#ELSE
        PRINT("\C");
#ENDIF
        FOR I = 0 TO 50
        [
            _PAR[  2] = _PAR[  2] + I;
            @MAGIC(0);
            @MAGIC(1);
            @CRTKN(1 , 6 , 1);
            @CLS();
            IF (PUSH_KEY() > 0) RETURN;
        ]
        @MODE(2 , 0);      @CLS();
        @MODE(2 , 1);      @CLS();
        _PAR[2] = 100;
        @SETOD(&METAOBJ , 111);
        @SETWD(&METAWIR , 170);
        @MODE(2 , 0);      @MAGIC(0);         @MAGIC(1);
        @MODE(2 , 1);      @MAGIC(1);
        @MODE(2 , 2);      @CLS();
        @CRTKN(6 , 6 , 1);
        @CLS();
        FOR I = 0 TO 8
        [
            _PAR[_HEAD] = _PAR[_HEAD] + 20;
            _PAR[    0] = _PAR[    0] + 10;
            _PAR[    1] = _PAR[    1] + 6;
            @MAGIC(0);
            @MAGIC(1);
            @CRTKN(6 , 6 , 1);
            @CLS();
            IF (PUSH_KEY() > 0) RETURN;
        ]
        FOR I = 0 TO 20
        [
            _PAR[2] = _PAR[2] + 50;
            _PAR[0] = _PAR[0] - 4;
            @MAGIC(0);
            @MAGIC(1);
            @CRTKN(6 , 6 , 1);
            @CLS();
            IF (PUSH_KEY() > 0) RETURN;
        ]
    ]

PUSH_KEY()
    VAR JOY;
    [
        JOY = 0;
        IF (SHARP_X1 == 2) JOY = 96 - (STICK() AND 96);
        JOY = JOY + INKEY(0);
        RETURN(JOY);
    ]

PARPRT1()
    [

#IF (_LSX!=0)
        _LSXCLS();
#ELSE
        PRINT("\C");
#ENDIF
        LOCATE(70 , 1);  PRINT("ENERGY");
        LOCATE(70 , 4);  PRINT("ENEMY");
        LOCATE(70 , 6);  PRINT("ROUND:" , ROUND);
        LOCATE(70 , 8);  PRINT("TIME");
        LOCATE(70 , 12); PRINT("LAP TIME");
        LOCATE(71 , 14); PRINT(LFUN , ":" , LBYOU , ":" , LCBYOU , " ");
    ]

TIME()
    [
        CBYOU++;
        IF (CBYOU > 9)
        [
            CBYOU= 0 ; BYOU++;
            IF (BYOU > 59)
            [
                BYOU = 0; FUN++;
            ]
        ]
    ]

PARPRT()
    [
        LOCATE(71 , 2);    PRINT(MEGY , "  ");
        LOCATE(71 , 5);    PRINT(EEGY , "  ");
        LOCATE(71 , 10);   PRINT(FUN  ,  ":" , BYOU , ":" , CBYOU , " ");
    ]

CLS3()
    [
        @MODE(2 , 0);   @CLS();
        @MODE(2 , 1);   @CLS();
        @MODE(2 , 2);   @CLS();
    ]

ENDING()
    VAR I,J,K,L;
    [
        SC = 0;
        FOR I = 0 TO 8 [ _PAR[I]=0; ]
        @INIT();

        IF (NANDO == 0) RETURN;
        @SETOD(&METAOBJ , 111);
        @SETWD(&METAWIR , 170);
        _PAR[_HEAD] = 180;
        _PAR[    2] = 600;

        FOR I = 0 TO 50
        [
            @MAGIC(0);
            @MAGIC(1);
            @CRTKN(6 , RND(7) + 1 , 0);
            @CLS();
            _PAR[2] = _PAR[2] - 10;
            @MODE(2 , 2);
            @GRAD(RND(7) + 1);
            @CIRCLE(220 + RND(200) , 50+RND(60) , RND(40) + 10);
            @MODE(2 , 1 - _MSCREEN);
        ]
        @MODE(2 , 2);
        @WINDOW(0 , 0 , 639 , 110);
        FOR I = 1 TO 10
        [
            FOR J = 7 DOWNTO 1
            [
                @GRAD(8 - J);
                @CIRCLE(319  ,  199  ,  I * J * 10);
            ]
        ]

        FOR I=0 TO 600 [ @PALET(0,RND(8),RND(8),RND(8),RND(8),0,0,0); ]
        @INIT();
        FOR I = 0 TO 9 [ ZKF[I] = 0; ]
        CHF   = 1;
        SC    = 3;

        ZAKOOUT(25 , 9);
        FOR I = 1 TO 6
        [
            FOR J = 0 TO 100
            [
                ZAKOMOV();  TGX = RND(69);  TGY = RND(21);
                TSHOT_M();  SCROL();

                FOR K = 0 TO 30 [ TBRL[I] = TBRL[I] - 5; ]
            ]
            CASE I OF
            [
                1 [ LOCATE(25,13); PRINT("S-OS ﾂｳｼﾝ GAME ｼﾘｰｽﾞ ﾀﾞｲ 2ﾀﾞﾝ");
                    LOCATE(34,15); PRINT("METAL FIGHT");                    ]
                2 [ LOCATE(25,13); PRINT("Program & Graphic & Character");
                    LOCATE(32,15); PRINT("JUNICHI KUROKI");                 ]
                3 [ LOCATE(33,13); PRINT("Special Thanks");
                    LOCATE(33,15); PRINT("TAKASHI FUKAYA");                 ]
                4 [ LOCATE(30,13); PRINT("Kenasitekureta Yatu");
                    LOCATE(34,15); PRINT("TAKAMITU MATSUMURA");             ]
                5 [ LOCATE(33,13); PRINT("SEE YOU AGAIN");
                    LOCATE(35,15); PRINT("NEXT GAME");                      ]
                6 [ LOCATE(36,13); PRINT("The End"); TGX=1000; CHF=2;       ]
            ]
            FOR J = 0 TO 60000 []
            FOR J = 0 TO 60000 []
            FOR J = 0 TO 60000 []

            LOCATE(0 , 13);  PRINT(STR$(32 , 79));
            LOCATE(0 , 15);  PRINT(STR$(32 , 79));
        ]
        FOR I=0 TO 100 [ ZAKOMOV();   SCROL();   TSHOT_M(); ]

        I =  CBYOU  +   BYOU * 10  +   FUN * 600;
        J = LCBYOU  +  LBYOU * 10  +  LFUN * 600;
        IF (J>I)
        [
            LFUN      =       FUN;
            LBYOU     =       BYOU;
            LCBYOU    =       CBYOU;

            LOCATE(36 , 10);  PRINT("BEST LAP!");
            LOCATE(36 , 15);  PRINT(LFUN , ":" , LBYOU , ":" , LCBYOU , " ");
            COMMAND(&COMS);

            FOR J = 0 TO 60000 []
            FOR J = 0 TO 60000 []
        ]
    ]

/***************************************************************************ﾍ
****                                                                     ****
**   Action game 'METAL FIGHT' Program & Character & Graphic K.JUNICHI     **
**                                                                         **
**                          1992,2,20 15:41:47                             **
****                                                                     ****
ﾍ***************************************************************************/
 
#ASM
#END

