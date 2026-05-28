// SLFS Phase 1 動作確認 sample (= 数値 ID 直書き)
// build: slangbuild --emit disk -E x1native_slfs examples/X1NATIVE_SLFS/SLFSDEMO.SL -o SLFSDEMO --slfs-add examples/X1NATIVE_SLFS/assets/
// 実行: emulator (xmil 等) で SLFSDEMO.d88 を drive 1 mount → boot
//
// 注意: buffer は sector round-up 分の容量必要 (= GREETING.TXT 17 byte でも 256 byte 確保)

VAR SIZE;

MAIN()
BEGIN
    PRINT("SLFS DEMO");
    PRINT(/);
    SIZE = FS_READ_BY_ID(0, $4000);   // id 0 = sorted dir entry 0
    IF SIZE == 0 THEN PRINT("READ 0 FAILED",/);
    IF SIZE != 0 THEN PRINT("READ 0 OK SIZE=", SIZE, /);
    PRINT("PRESS ANY KEY");
    PRINT(/);
    INKEY(1);
END;
