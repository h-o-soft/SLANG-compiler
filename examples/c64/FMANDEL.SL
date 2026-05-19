// Commodore 64 (40 桁画面) 用テキストマンデルブロ
// 元の examples/FMANDEL.SL (80 桁版、MZ-2500 / MSX 等向け) を 40 桁に縮めた版。
// X 軸方向の解像度を半分にして倍率を 2 倍 (0.0458 → 0.0916) にすることで
// 同じ範囲のマンデルブロを 40 桁画面で描く。
//
// Build (= checkout 状態 / repo root から実行):
//   slangbuild -E c64 -I include examples/c64/FMANDEL.SL -o examples/c64/FMANDEL
//   x64sc -autostart examples/c64/FMANDEL.prg
// 配布物 install 後 (= SLANG_HOME 設定済) は -I include 省略可。

VAR X,Y,I,VAL;
VAR FLOAT CA, FLOAT CB, FLOAT FX, FLOAT FY;
VAR FLOAT A, FLOAT B, FLOAT T;

MAIN()
BEGIN
    FOR Y=0 TO 24
    {
        FOR X=0 TO 38
        {
            FX = X - 19;        // 中心を 40 桁の半分に
            FY = Y - 12;
            CA = FX * 0.0916;   // 80 桁版 0.0458 の 2 倍 (横解像度半減を補償)
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
