


VAR FLOAT X, FLOAT Y, FLOAT R;
VAR I;
var %%RATE;

MAIN()
BEGIN

    RATE=360.0;
    FOR I=0 TO 359*3
    {
        R = I * 2 * 3.1415926 / 360.0;
        X = FSIN(R)*39.0*RATE/360.0;
        Y = FCOS(R)*12.0*RATE/360.0;
	RATE = RATE - 0.3333;

        LOCATE(40+FTOI(X),12+FTOI(Y));
        PRINT("*");
    }

END;