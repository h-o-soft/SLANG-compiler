//
// 
//

#INCLUDE SOROBAN.LIB

VAR X,Y,I,VAL;
ARRAY BYTE CA[@DBL], BYTE CB[@DBL], BYTE FX[@DBL], BYTE FY[@DBL];
ARRAY BYTE A[@DBL], BYTE B[@DBL], BYTE T[@DBL];
ARRAY BYTE CAK[@DBL], BYTE CBK[@DBL], BYTE FV2[@DBL], BYTE FV4[@DBL];
ARRAY BYTE TMPF[@DBL],TMPF2[@DBL];

MAIN()
BEGIN
    @CVSTF(CAK,"0.0458");
    @CVSTF(CBK,"0.08333");
    @CVSTF(FV2,"2");
    @CVSTF(FV4,"4");
    FOR Y=0 TO 24
    {
        FOR X=0 TO 78
        {
            @CVITF(FX,X-39);
            @CVITF(FY,Y-12);
            @MUL(CA,FX,CAK);    // CA=X*0.0458
            @MUL(CB,FY,CBK);    // CB=Y*0.08333
            @MOVE(A,CA);        // A=CA
            @MOVE(B,CB);        // B=CB
            FOR I=0 TO 15
            {
                // T=A*A-B*B+CA
                @MUL(T,A,A);        // T=A*A
                @MUL(TMPF,B,B);     // TMPF=B*B
                @SUB(T,T,TMPF);     // T=T-TMPF
                @ADD(T,T,CA);       // T=T+CA

                // B=2*A*B+CB
                @MUL(TMPF,FV2,A);
                @MUL(TMPF,TMPF,B);
                @ADD(B,TMPF,CB);

                // A=T
                @MOVE(A,T);

                // IF (A*A+B*B)>4 THEN GOTO 200
                @MUL(TMPF,A,A);
                @MUL(TMPF2,B,B);
                @ADD(TMPF,TMPF,TMPF2);
                IF @CMP(TMPF,FV4)==1 THEN EXIT;
            }
            IF I<=15 {
                IF I>9 THEN I=I+7;
                PRINT(CHR$(48+I));
            } ELSE {
                PRINT(" ");
            }
        }
        PRINT(/);
    }
END;

