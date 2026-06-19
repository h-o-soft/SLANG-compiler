// CGROMから文字フォントを読み出し、太字にしてPCGに定義するサンプル

CONST WD = 40;
ARRAY BYTE BUF[24-1];

TATR_FILL(X1,Y1,X2,Y2,ATR)
VAR I,J;
{
    X2--;
    Y2--;

    FOR J=Y1 TO Y2
        FOR I=X1 TO X2
        {
            PORT[$2800+I+J*WD]=ATR;
        }
}

DEFFONT(FROM_CODE, TO_CODE)
VAR I,J,ADR
VAR BYTE DAT;
{
    FOR I=FROM_CODE TO TO_CODE
    {
        // ROMからフォントを取得
        GETCGROM(I, BUF);
        // 太字にする(左シフトしたものをORする)
        FOR J=0 TO 7
        {
            DAT = BUF[J];
            BUF[J] = DAT OR (DAT << 1);
        }
        // PCG定義用に3倍化(末尾から埋める)
        FOR J=0 TO 7
        {
            ADR = 23 - J*3;
            DAT = BUF[7-J];
            BUF[ADR] = DAT;
            ADR--;
            BUF[ADR] = DAT; 
            ADR--;
            BUF[ADR] = DAT;
        }
        PCGDEF(I,BUF);
    }
}


MAIN()
VAR I;
BEGIN
    WIDTH(WD);
    DEFFONT($20, $7A);
    // 4行はPCG
    TATR_FILL(0,0,40,4,$27);
    // 5行目からは非PCG
    TATR_FILL(0,5,40,15,$7);

    PRINT("Hello, SLANG!",/);

    // PCGでの表示
    FOR I=$21 TO $7A
    {
        PRINT(CHR$(I));
    }

    // CGROMでの表示
    LOCATE(0,5);
    FOR I=$21 TO $7A
    {
        PRINT(CHR$(I));
    }

    LOOP{}
END;
