/*
 GRAPH.LIB( MAGIC ) SAMPLE
 */

CONST   _PLOTSW=0,_THREE=0,_FLOAT=0,_SINCOS=1,_TILESW=1,_GHIN=$FFFF,_KPRINTF=0,_COLOR=1;

#INCLUDE GRAPHF.LIB

MAIN()
VAR I,COL;
{
    @INIT();
    FOR I=0 TO 16
    {
        COL = I MOD 8;
        @BOXC(I*10,I*4,639-I*10,199-I*4,COL);
    }
    INPUT();

    @INIT();
    @GRAD(7);
    FOR I=0 TO 16
    {
        COL = I MOD 8;
        @FULLC(I*10,I*4,639-I*10,199-I*4,COL);
    }
    INPUT();

    FOR I=0 TO 30
    {
        @TRIANGLEC(RND(600),RND(200),RND(600),RND(200),RND(600),RND(200),RND(8));
    }
    INPUT();

    FOR I=0 TO 30
    {
        @SPLINEC(RND(600),RND(200),RND(600),RND(200),RND(600),RND(200),RND(8));
    }
    INPUT();

    FOR I=0 TO 30
    {
        @CIRCLEC(RND(600),RND(200),10 + RND(100),RND(8));
    }
    INPUT();

    FOR I=0 TO 1000
    {
        @LINEC(RND(640),RND(200),RND(640),RND(200),RND(8));
        IF (INKEY(0)==' ') EXIT;
    }
    INPUT();

    @INIT();
}
