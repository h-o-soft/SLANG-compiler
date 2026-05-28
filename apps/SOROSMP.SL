
#INCLUDE SOROBAN.LIB

VAR VAL;

MAIN()
    ARRAY
        P2[@DBL],X[@DBL],CONS1[@DBL],
        N2[@DBL],N[@DBL],CONS6[@DBL];
    BEGIN
        //// LOAD SOROBAN
        //VAL = FOPEN(0,"SOROBAN.BIN", 4);
        //FREAD(0, $9F00, 3871);
        //FCLOSE(0);

        @DOUBLE();

        ^CY = 1;

        PRINT("\C");
        @CVUTF(X,1);

        @PAI(X,X);
        PRINT(
            MSX$(@CVFTS(X))
        )

        @CVUTF(P2,0);
        @CVUTF(N,1);
        @MOVE(CONS1,N);
        @CVSTF(CONS6,"6");

        REPEAT [
            LOCATE(0,1);
            @MUL(N2,N,N);
            @DIV(X,CONS6,N2);
            @ADD(P2,P2,X);
            @SQR(X,P2);
            PRINT(
                MSX$(@CVFTS(X))
            );
            @ADD(N,N,CONS1);
        ] UNTIL INKEY(0)==$1B;
        PRINT(/);
    END;

