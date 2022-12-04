//
// 
//

VAR X,Y,I,VAL;
VAR FLOAT CA, FLOAT CB, FLOAT FX, FLOAT FY;
VAR FLOAT A, FLOAT B, FLOAT T;

MAIN()
BEGIN
    FOR Y=0 TO 24
    {
        FOR X=0 TO 78
        {
            FX = X - 39;
            FY = Y - 12;
            CA = FX * 0.0458;
            CB = FY * 0.08333;
            A = CA;
            B = CB;
            FOR I=0 TO 15
            {
                T = A*A - B*B + CA;
                B = 2*A*B + CB;
                A=T;
                IF (A*A+B*B)>4.0 THEN EXIT;
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

