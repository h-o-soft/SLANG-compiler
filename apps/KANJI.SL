// 漢字表示のテストです
CONST WD=40;

ARRAY BYTE FDAT[24-1];

MACHINE SJIS2OFS(1):_SJIS2OFS;

//VAR MOJIOFS;

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

VAR KX,KY;

KANJIPUT(S)
VAR I,ADR,C,J,GADR;
{
    FOPEN(0, "KANJI.BIN", 4);
    ADR = S;
    C = MEM[ADR];
    WHILE C!=0 {
        C = C << 8;
        C = C OR MEM[++ADR];

        // ShiftJIS to file offset(KUTEN)
        C = SJIS2OFS(C);
        // 漢字画像データオフセットに移動
        FSEEK(0,C,0);
        FREAD(0,FDAT,8);
        // PCG定義版
        // FOR I=0 TO 7
        // {
        //     J = (7-I)*3;
        //     FDAT[J++] = FDAT[7-I];
        //     FDAT[J++] = FDAT[7-I];
        //     FDAT[J] = FDAT[7-I];
        // }
        // PCGDEF(MOJIOFS, FDAT);
        // PRINT(CHR$(MOJIOFS));
        GADR = 0x4000 + KY * 40 + KX;
        FOR I=0 TO 7
        {
            PORT[GADR] = FDAT[I];
            GADR += 0x4000;
            PORT[GADR] = FDAT[I];
            GADR += 0x4000;
            PORT[GADR] = FDAT[I];
            GADR = GADR + (1 << 11) - 0x8000;
        }
        KX++;
        IF KX >= 40 {
            KX = KX - 40;
            KY++;
        }
        //MOJIOFS++;
        C = MEM[++ADR];
    }
    FCLOSE(0);
}

MAIN()
{
    WIDTH(WD);
    //MOJIOFS = 0x80;

    KX=0;
    KY=0;
    GRCLS();
    GRDISP(1);

//    PORT[0x1FD0] = 0;
    INKEY(1);

    KANJIPUT("Ｘ１で漢字を表示してみる実験。ＨＤＤからだと実用的な速度で表示出来そう？");
    KX=0;
    KY=KY+1;
    KANJIPUT("複雑な文字は読みづらい。薔薇。繊維。絨毯。");
    KX=0;
    KY=KY+1;
    KANJIPUT("グラフィック画面に描画してるので何文字でも問題なく表示されます。");
    KX=0;
    KY=KY+1;
    KANJIPUT("現状半角文字には未対応。そのうちどうにか……。");
    KX=0;
    KY=KY+1;
    KANJIPUT("☆★○■※などなど記号も出ます。バンザーイ。");
    INPUT();
}

#ASM
_SJIS2OFS:
    ; 区番号-1を得る
    LD A,H
    CP $e0
    JR C,S2K_1
    ; 0xe0以上
    SUB $e0
    SLA A
    ADD A,62
    JR S2K_2
S2K_1:
    ; 0x81～
    SUB $81
    SLA A
S2K_2:
    LD E,A
    LD D,0

    ; 点番号-1を得る
    LD A,L
    CP $80
    JR C,S2K_3
    ; 0x80～
    SUB $80-63
    JR S2K_4
S2K_3:
    ; 0x40～
    SUB $40
S2K_4:

    CP 94
    JR C,S2K_5
    INC E
    SUB 94
S2K_5:
    LD C,A
    LD B,0
    PUSH BC

    ; 区 = DEを94倍する
    LD L,94
    LD H,0
    CALL MULHLDE
    POP DE

    ; 点を足す
    ADD HL,DE

    ; 8倍するとファイルオフセットになる
    ADD HL,HL
    ADD HL,HL
    ADD HL,HL

    RET

#END
