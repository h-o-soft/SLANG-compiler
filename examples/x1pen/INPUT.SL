// INPUT.SL - キーとジョイスティック (X1 の x1 env)
// X1Pen の SLANG タブで、本物の slangc と同じ ASM・bin になるかを比べるための見本
// ESC キーで終わる

// VSYNC を使うプログラムは、フレームごとに呼ばれる VSYNC_PROC() を書く (何もしなくても)
VSYNC_PROC()
{
}

MAIN()
VAR K, J0, J1;
{
    WIDTH(40);
    LOCATE(0, 0);
    PRINT("KEY / STICK(0) / STICK(1)  ESC=END");
    LOOP
    {
        K = INKEY(0);
        IF K == $1B THEN EXIT;
        J0 = STICK(0);
        J1 = STICK(1);
        LOCATE(0, 2);
        PRINT("KEY ", K, "   ");
        LOCATE(0, 3);
        PRINT("STICK0 ", J0, "   ");
        LOCATE(0, 4);
        PRINT("STICK1 ", J1, "   ");
        IF (J0 & $01) == 0 THEN PRINT("UP   ") ELSE PRINT("     ");
        VSYNC(1);
    }
}
