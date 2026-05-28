CONST NUM=100;
CONST WD=80;

VAR CNT;
VAR I,J,IDX;
VAR KEY;
ARRAY STARS[NUM*4-1];

MAIN()
BEGIN
    WIDTH(WD);
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

