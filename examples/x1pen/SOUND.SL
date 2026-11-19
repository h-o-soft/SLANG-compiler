// SOUND.SL - PSG の音 (X1 の x1 env)
// X1Pen の SLANG タブで、本物の slangc と同じ ASM・bin になるかを比べるための見本
// PSG はポート $1C00 でレジスタを選び、$1B00 に値を書く

PSGW(R, V)
{
    PORT[$1C00] = R;
    PORT[$1B00] = V;
}

// 1 つの音 (チャンネル A、周期 P) を、VSYNC で N 回待つ間だけ鳴らす
TONE(P, N)
VAR I;
{
    PSGW(0, P & $FF);
    PSGW(1, P >> 8);
    PSGW(8, 12);
    FOR I=1 TO N VSYNC(1);
    PSGW(8, 0);
}

ARRAY WORD MELODY[] = { 478, 426, 379, 358, 319, 284, 253, 239, 0 };

// VSYNC を使うプログラムは、フレームごとに呼ばれる VSYNC_PROC() を書く (何もしなくても)
VSYNC_PROC()
{
}

MAIN()
VAR I;
{
    PSGW(7, $3E);       // チャンネル A の音だけを出す (ポート A・B は入力のまま)
    I = 0;
    WHILE MELODY[I] != 0
    {
        TONE(MELODY[I], 10);
        I = I + 1;
    }
    BEEP();
}
