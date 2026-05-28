// SLFS Phase 3 動作確認 sample (= generated header で asset id を CONST 化)
// build: slangbuild --emit disk -E x1native_slfs examples/X1NATIVE_SLFS/SLFSDEMO.SL -o SLFSDEMO --slfs-add examples/X1NATIVE_SLFS/assets/
// 実行: emulator (xmil 等) で SLFSDEMO.d88 を drive 1 mount → boot
//
// 注意: buffer は sector round-up 分の容量必要 (= GREETING.TXT 17 byte でも 256 byte 確保)
//
// 注意 (Phase 3): slangbuild が `--slfs-add` 指定済の場合、 compile 前に
// `<input SL stem>.assets.inc` を outputDir に自動生成 + slangc に `-I outputDir`
// を渡すため、 下記 `#INCLUDE` で取り込める。 中身は `CONST FILE_<NAME> = id, ...;`
// (= packer sort 順の id を SLANG 識別子化、 sort 順変更も自動追従、 typo は
//  compile error)。

#INCLUDE SLFSDEMO.assets.inc

VAR SIZE;

MAIN()
BEGIN
    PRINT("SLFS DEMO");
    PRINT(/);
    SIZE = FS_READ_BY_ID(FILE_GREETING_TXT, $4000);
    IF SIZE == 0 THEN PRINT("READ 0 FAILED",/);
    IF SIZE != 0 THEN PRINT("READ 0 OK SIZE=", SIZE, /);
    PRINT("PRESS ANY KEY");
    PRINT(/);
    INKEY(1);
END;
