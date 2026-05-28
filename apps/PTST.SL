VAR X,Y,VX;

VSYNC_PROC()
{
}

MAIN()
{
    X=1;
    Y=10;
    VX=1;
    LOOP
    {
        LOCATE(X,Y);
        PRINT(" Hello, SLANG! ",/);
        X=X+VX;
        IF X==60 THEN {
            VX=-1;
        } ELIF X==0 THEN {
            VX=1;
        }
        VSYNC(1);
    }
}

