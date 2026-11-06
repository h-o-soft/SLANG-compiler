/*
  x1native_hufs (Hu-BASIC と同じ形のディスク) のレコードを、ファイルの仕組みを通らずに読み書きする例 (docs/HUFS.md)

    slangbuild -E x1native_hufs --emit disk examples/X1NATIVE_HUFS/HUFSRAW.SL -o HUFSRAW \
      --hufs-add GREET.TXT=examples/X1NATIVE_HUFS/files/GREET.TXT,text

  レコード 0 (IPL) を読み、最後のレコード (1279) に書いて読み戻す。範囲外 (1280) はエラー ($FF0E) になる。
  期待する表示: R0=0000 01 / W=0000 / R=0000 FF00 / X=FF0E / END
*/
ARRAY BYTE BUF[255];
VAR R, I;
MAIN()
BEGIN
    R = FRAWREAD(0, BUF);
    PRINT("R0=", HEX4$(R), " ", HEX2$(BUF[0]), /);
    FOR I = 0 TO 255 { BUF[I] = 255 - I; }
    R = FRAWWRITE(1279, BUF);
    PRINT("W=", HEX4$(R), /);
    FOR I = 0 TO 255 { BUF[I] = 0; }
    R = FRAWREAD(1279, BUF);
    PRINT("R=", HEX4$(R), " ", HEX2$(BUF[0]), HEX2$(BUF[255]), /);
    R = FRAWREAD(1280, BUF);
    PRINT("X=", HEX4$(R), /);
    PRINT("END", /);
END;
