CONST _THREE=1,_PLOTSW=0,_FLOAT=0,_SINCOS=1,_TILESW=0,_GHIN=$00;
#INCLUDE GRAPHF.LIB

MAIN()
VAR I,V;
{
    FOR I=0 TO 360
    {
        V = @ICOS(I, 1000);
        PRINT(%(I), ":", %(V), /);
	I=I+29;
    }
}
