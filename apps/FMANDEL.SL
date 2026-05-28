//
// 
//
CONST WD=80;

VAR X,Y,I,VAL;
VAR FLOAT CA, FLOAT CB, FLOAT FX, FLOAT FY;
VAR FLOAT A, FLOAT B, FLOAT T;

MAIN()
BEGIN



    WIDTH(80);
    GRDISP(0);
    GRCLS();
    GRDISP(1);

    MAGLOAD("MINIPOS.MAG",32,8);
    
    FOPEN(0,"GWPE.M8A",4);
    FREAD(0, WORKEND, 32768);
    FCLOSE(0);
    M8ALOAD(WORKEND);
    INPUT();

    GRCLS();

    WIDTH(40);
    GRDISP(1);

    MAGLOAD("MINIPOS.MAG",32,8);
    //MAGLOAD("MINIPOS.MAG",0,0);
    // MAGLOAD("UZU640D.MAG",0,0);
    INPUT();

    // GRCLS();
    // WIDTH(80);
    MAGLOAD("MINIPOS.MAG",0,20);
    // INPUT();

    INPUT();
    GRDISP(0);
    INPUT();
    GRDISP(1);
    INPUT();
    GRDISP(0);
    GRCLS();

    WIDTH(80);

    // FOR Y=0 TO 24
    // {
    //     FOR X=0 TO WD-2
    //     {
    //         FX = X - (WD/2-1);
    //         FY = Y - 12;
    //         CA = FX * 0.0458;
    //         CB = FY * 0.08333;
    //         A = CA;
    //         B = CB;
    //         FOR I=0 TO 15
    //         {
    //             T = A*A - B*B + CA;
    //             B = 2*A*B + CB;
    //             A=T;
    //             IF (A*A+B*B)>4.0 THEN EXIT;
    //         }
    //         IF I<=15 {
    //             IF I>9 THEN I=I+7;
    //             PRINT(CHR$(48+I));
    //         } ELSE {
    //             PRINT(" ");
    //         }
    //     }
    //     PRINT(/);
    // }
END;

