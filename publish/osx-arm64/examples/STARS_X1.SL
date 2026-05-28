
// PCG定義をする複数星(テキスト)表示のテスト
// ORG $3000 (等)をつけるだけでS-OSでも動きます

CONST NUM=100;
CONST WD=40;

CONST STARPCG = [
    $00, $00, $00,
    $10, $10, $10,
    $20, $38, $38,
    $80, $FE, $FE,
    $00, $38, $38,
    $00, $28, $28,
    $00, $44, $44,
    $00, $00, $00
    ];

VAR CNT;
VAR I,J,IDX;
VAR KEY;
ARRAY STARS[NUM*4-1];

TATR_FILL(X1,Y1,X2,Y2,ATR)
VAR I,J;
BEGIN
    X2--;
    Y2--;

    FOR J=Y1 TO Y2
        FOR I=X1 TO X2
        {
            PORT[$2800+I+J*WD]=ATR;
        }
END;

MAIN()
BEGIN
    WIDTH(WD);

    // SET * PCG
    PCGDEF(42,STARPCG);
    // SET PCG & COLOR 7(ALL)
    TATR_FILL(0,0,80,24,$27);

    INITSTARS();

    CNT=1;
    KEY=0;
    WHILE(KEY!=27)
    {
        KEY=INKEY(0);
        IF KEY=='A' && CNT>1 THEN CNT--;
        IF KEY=='D' && CNT<NUM-1 THEN CNT++;
        LOCATE(0,0);
        PRINT(%(CNT));

        FOR I=0 TO CNT-1
        {
            IDX=I*4;
            LOCATE(HIGH STARS[IDX],HIGH STARS[IDX+1]);
            PRINT(!(" "));
            STARS[IDX] += STARS[IDX+2];
            STARS[IDX+1] += STARS[IDX+3];
            LOCATE(HIGH STARS[IDX],HIGH STARS[IDX+1]);
            PRINT(!("*"));
            IF (HIGH STARS[IDX] <= 0) || (HIGH STARS[IDX] >= WD-1) THEN {
                STARS[IDX+2] = -STARS[IDX+2];
            }
            IF (HIGH STARS[IDX+1] <= 0) || (HIGH STARS[IDX+1] >= 23) THEN {
                STARS[IDX+3] = -STARS[IDX+3];
            }
        }
    }
END;


// 星の位置と速度を初期化する
INITSTARS()
BEGIN
    FOR I=0 TO NUM-1
    {
        IDX=I*4;
        STARS[IDX]     = (1+RND(WD-3)) << 8;
        STARS[IDX+1]   = (1+RND(21)) << 8;
        J = 128 + RND(128);
        IF RND(1)==0 THEN J=-J;
        STARS[IDX+2] = J;
        J = 128 + RND(128);
        IF RND(1)==0 THEN J=-J;
        STARS[IDX+3]   = J;
    }
END;

