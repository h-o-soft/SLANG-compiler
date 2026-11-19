// SCREEN.SL - 文字・色・グラフィック・PCG (X1 の x1 env)
// X1Pen の SLANG タブで、本物の slangc と同じ ASM・bin になるかを比べるための見本

// PCG の字の形 (8 ドット × 8 行)。PCGDEF は 1 行ごとに 青・赤・緑 の 3 バイトを読む (青と赤でマゼンタのハート)
ARRAY BYTE HEART[] = {
    $66,$66,$00,
    $FF,$FF,$00,
    $FF,$FF,$00,
    $FF,$FF,$00,
    $7E,$7E,$00,
    $3C,$3C,$00,
    $18,$18,$00,
    $00,$00,$00
};

MAIN()
VAR I;
{
    WIDTH(40);
    GRPSETUP();
    GRDISP(1);
    GRCLS();

    // 文字と色 (TATTR は今のカーソルの升の属性。下の 3 ビットが色)
    FOR I=1 TO 7
    {
        LOCATE(2, I);
        TATTR(I);
        PRINT("COLOR ", I);
    }

    // PCG の字を 1 つ作って並べる
    PCGDEF($80, HEART);
    LOCATE(2, 9);
    FOR I=0 TO 9
    {
        TATTR($27);
        PRINT(STR$($80, 1));    // 字のコード $80 を 1 つ (!() は番地からの文字列なので使わない)
    }

    // 線と四角
    FOR I=0 TO 7
        LINE(160 + I * 20, 20, 300 - I * 10, 180, I);
    BFILL(20, 120, 120, 180, 2);
}
