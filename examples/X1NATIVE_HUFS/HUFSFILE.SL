/*
  x1native_hufs (Hu-BASIC と同じ形のディスク) のファイルを、名前で読み書きする例 (docs/HUFS.md)

    slangbuild -E x1native_hufs --emit disk examples/X1NATIVE_HUFS/HUFSFILE.SL -o HUFSFILE \
      --hufs-add GREET.TXT=examples/X1NATIVE_HUFS/files/GREET.TXT,text

  1. ディスクの一覧を出す (FFIRST / FNEXT)
  2. GREET.TXT (Hu-BASIC の順ファイルの形: 行の終わりは CR、終わりに $1A) を 1 バイトずつ読んで出す
  3. MEMO.TXT を作って書き (FOPEN mode 3)、後ろに足し (mode 2)、読み戻す
  4. MEMO.TXT の名前を MEMO2.TXT に変えてから消し、空きのクラスタの数が戻ることを見る

  期待する表示 (空きの数と頭のクラスタは、組み立てたものによる):
    FREE=76 clusters
    HUFSMAIN     .Sys $41 start=2
    GREET        .TXT $04 start=3
    [HELLO] [WORLD] []            (最後の行の CR の後ろは $1A なので、空の [] が出る)
    MEMO: ONE TWO (7 bytes)
    REN=0000 DEL=0000 FREE=76 clusters
    END
*/
ARRAY BYTE ENT[31];
ARRAY BYTE BUF[63];
VAR R, C, N, I, FREE0;

// FFIRST / FNEXT の 32 バイト (ディレクトリの項目) から、名前・拡張子・種類・頭のクラスタを出す
SHOWENT()
VAR J;
BEGIN
    FOR J = 1 TO 13 { PRINT(CHR$(ENT[J])); }
    PRINT(".");
    FOR J = 14 TO 16 { PRINT(CHR$(ENT[J])); }
    PRINT(" $", HEX2$(ENT[0]), " start=", ENT[30], /);
END;

MAIN()
BEGIN
    FREE0 = FFREE();
    PRINT("FREE=", FREE0, " clusters", /);
    R = FFIRST(ENT);
    WHILE R == 0 DO BEGIN
        SHOWENT();
        R = FNEXT(ENT);
    END;

    // 順ファイルを読む: $1A で終わり (ファイル API は $1A を見ないので、読む側で止める)
    R = FOPEN(0, "GREET.TXT", 0);
    IF R <> 0 THEN BEGIN PRINT("OPEN ERROR ", HEX4$(R), /); RETURN; END;
    PRINT("[");
    WHILE 1 DO BEGIN
        C = FGETC(0);
        IF C == $1A OR C == $FFFF THEN EXIT;
        IF C == 13 THEN PRINT("] [") ELSE PRINT(CHR$(C));
    END;
    PRINT("]", /);
    R = FCLOSE(0);

    // 作って書く → 閉じる → 後ろに足す → 読み戻す (種類 Bin で作ると、大きさの欄に長さが入る)
    R = FOPEN(0, "MEMO.TXT", 3 + 4);
    R = FWRITE(0, "ONE ", 4);
    R = FCLOSE(0);
    R = FOPEN(0, "MEMO.TXT", 2);
    R = FWRITE(0, "TWO", 3);
    R = FCLOSE(0);
    R = FOPEN(0, "MEMO.TXT", 0);
    N = FREAD(0, BUF, 64);
    PRINT("MEMO: ");
    FOR I = 0 TO N - 1 { PRINT(CHR$(BUF[I])); }
    PRINT(" (", N, " bytes)", /);
    R = FCLOSE(0);

    // 名前を変えて、消す
    PRINT("REN=", HEX4$(FRENAME("MEMO.TXT", "MEMO2.TXT")));
    PRINT(" DEL=", HEX4$(FDELETE("MEMO2.TXT")));
    PRINT(" FREE=", FFREE(), " clusters", /);
    PRINT("END", /);
END;
