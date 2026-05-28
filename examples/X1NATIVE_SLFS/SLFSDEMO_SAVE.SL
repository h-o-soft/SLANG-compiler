// SLFS Phase 2 動作確認 sample (= save area read / write)
// build: slangbuild --emit disk -E x1native_slfs examples/X1NATIVE_SLFS/SLFSDEMO_SAVE.SL -o SLFSDEMO_SAVE --slfs-add examples/X1NATIVE_SLFS/assets/
// 実行: emulator (xmil 等) で SLFSDEMO_SAVE.d88 を drive 1 mount → boot
//
// 動作:
//   1) save slot 0 (= save area offset 0, 1 sector = 256 byte) に test pattern を write
//   2) save slot 0 を別 buffer に read
//   3) 一致確認 (= 全 byte sum 比較) + 表示

VAR R;
VAR W_SUM;
VAR R_SUM;
ARRAY BYTE WBUF[255];   // write buffer (= 256 byte = 1 sector、 SLANG ARRAY[N] = N+1 要素)
ARRAY BYTE RBUF[255];   // read buffer

MAIN()
BEGIN
    PRINT("SLFS DEMO (SAVE/LOAD)");
    PRINT(/);

    // fill write buffer with test pattern (= 各 byte = 0..255)
    FOR R = 0 TO 255 { WBUF[R] = R; }

    // save slot 0 (= save area offset 0, 1 sector) に write
    R = FS_SAVE_W(0, 1, WBUF);
    IF R == 0 THEN PRINT("WRITE FAILED",/);
    IF R != 0 THEN PRINT("WRITE OK SIZE=", R, /);

    // 同 slot を read
    R = FS_SAVE_R(0, 1, RBUF);
    IF R == 0 THEN PRINT("READ FAILED",/);
    IF R != 0 THEN PRINT("READ OK SIZE=", R, /);

    // 一致確認 (= 全 byte sum 比較)
    W_SUM = 0;
    R_SUM = 0;
    FOR R = 0 TO 255 {
        W_SUM = W_SUM + WBUF[R];
        R_SUM = R_SUM + RBUF[R];
    }
    IF W_SUM == R_SUM THEN PRINT("MATCH OK SUM=", W_SUM, /);
    IF W_SUM != R_SUM THEN PRINT("MISMATCH W=", W_SUM, " R=", R_SUM, /);

    PRINT("PRESS ANY KEY");
    PRINT(/);
    INKEY(1);
END;
