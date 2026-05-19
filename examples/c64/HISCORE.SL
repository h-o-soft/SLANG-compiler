// Commodore 64 KERNAL file I/O demo (oscar64 backend、env c_bindings: 経由)
//
// RND で 5 byte のスコア (= HISCORE) を生成し、disk file "score,s,w" に
// save → 同 file を再 open して read → 比較表示まで行う、save/load 往復の
// 最小 sample。
//
// 想定: VICE で D64 image を mount。空の D64 image は VICE 付属の c1541
// で予め作っておく:
//   c1541 -format "hiscore,01" d64 /tmp/hiscore.d64 -write examples/c64/HISCORE.prg hiscore
//   x64sc -8 /tmp/hiscore.d64
//   # BASIC で:
//   LOAD"hiscore",8
//   RUN
// 空 image が無い場合 save で KRNIO_NODEVICE / KRNIO_TIMEOUT 等のエラーが
// status に返る。
//
// 文字列リテラルは PETSCII 流儀で「主部 + type/mode token とも小文字」で
// 書く: oscar64 `-psci` は ASCII 大文字 (0x41-0x5A) を PETSCII shifted
// 領域 (0xC1-0xDA) に、ASCII 小文字 (0x61-0x7A) を PETSCII unshifted
// 大文字 (0x41-0x5A) に変換する。1541 disk drive は `,S,W` 等の type/mode
// token を PETSCII unshifted 大文字 (= 0x53 'S') で expect するため、SLANG
// リテラルでは `"score,s,w"` (全小文字) と書くと oscar64 が 1541 が
// parse できる byte 列に変換する。
//
// 注意: SEQ ファイル主部を `score` にしている (= 起動用 PRG `hiscore` と
// 別主部) のは、1541 emu が同名 PRG と SEQ の共存を拒否する場合があるため
// (= 異なる disk type でも filename 主部が同じだと write 失敗する観測あり)。
// 別 disk image に保存する想定でも、PRG / SEQ で主部を区別しておくのが安全。
//
// 戻り値判定パターン:
//   - KIO_OPEN / KIO_CHKIN / KIO_CHKOUT: 0 = fail (= 直後 KIO_STATUS() で
//     エラー詳細)、1 = success
//   - KIO_READ / KIO_WRITE: 実 byte 数。エラー時は SLANG WORD で $FFFx
//     (= signed 負値の sign extension) で届くので `IF N AND $8000` で判定
//   - KIO_STATUS: krnioerr enum (= KRNIO_OK = 0、KRNIO_EOF = $40 / EOF 時に
//     立つ、KRNIO_NODEVICE = $80 等は C64_KIO.LIB を参照)
//
// Build (= checkout 状態 / repo root から実行):
//   slangbuild -E c64 -I include examples/c64/HISCORE.SL -o examples/c64/HISCORE
// 配布物 install 後 (= SLANG_HOME 設定済) は -I include 省略可。

#INCLUDE "C64_KIO.LIB"

CONST SCORE_LEN = 5;          // BYTE 5 個分のダミー hiscore
CONST FILE_NUM  = 2;          // logical file number (= 任意の 1..254)

ARRAY BYTE SCORE_WR[SCORE_LEN];  // 書き出し用 (= ARRAY[N] は N+1 要素確保される)
ARRAY BYTE SCORE_RD[SCORE_LEN];

MAIN()
{
    VAR I, N, OK;

    // 1. ダミー hiscore 5 byte を RND で生成 (= 0..255)
    SRND(12345);
    FOR I = 0 TO SCORE_LEN - 1
    {
        SCORE_WR[I] = RND(256);
    }

    PRINT("WRITE:");
    FOR I = 0 TO SCORE_LEN - 1
    {
        PRINT(" ");
        PRINT(SCORE_WR[I]);
    }
    PRINT(/);

    // 2. SAVE (= "score,s,w" = sequential file write open、PETSCII 小文字流儀)
    OK = KIO_OPEN_NAMED(FILE_NUM, DEV_DISK, CH_WRITE, "score,s,w");
    IF OK == 0 THEN
    {
        PRINT("OPEN(W) FAIL STATUS=");
        PRINT(KIO_STATUS());
        PRINT(/);
        KIO_CLOSE(FILE_NUM);
        RETURN;
    }

    IF KIO_CHKOUT(FILE_NUM) == 0 THEN
    {
        PRINT("CHKOUT FAIL STATUS=");
        PRINT(KIO_STATUS());
        PRINT(/);
        KIO_CLOSE(FILE_NUM);
        RETURN;
    }

    N = KIO_WRITE(FILE_NUM, SCORE_WR, SCORE_LEN);
    KIO_CLRCHN();
    KIO_CLOSE(FILE_NUM);

    IF N AND $8000
    {
        PRINT("WRITE ERROR STATUS=");
        PRINT(KIO_STATUS());
        PRINT(/);
        RETURN;
    }

    // 3. LOAD (= "score,s,r" = sequential file read open、PETSCII 小文字流儀)
    OK = KIO_OPEN_NAMED(FILE_NUM, DEV_DISK, CH_READ, "score,s,r");
    IF OK == 0 THEN
    {
        PRINT("OPEN(R) FAIL STATUS=");
        PRINT(KIO_STATUS());
        PRINT(/);
        KIO_CLOSE(FILE_NUM);
        RETURN;
    }

    IF KIO_CHKIN(FILE_NUM) == 0 THEN
    {
        PRINT("CHKIN FAIL STATUS=");
        PRINT(KIO_STATUS());
        PRINT(/);
        KIO_CLOSE(FILE_NUM);
        RETURN;
    }

    N = KIO_READ(FILE_NUM, SCORE_RD, SCORE_LEN);
    KIO_CLRCHN();
    KIO_CLOSE(FILE_NUM);

    IF N AND $8000
    {
        PRINT("READ ERROR STATUS=");
        PRINT(KIO_STATUS());
        PRINT(/);
        RETURN;
    }

    PRINT("READ :");
    FOR I = 0 TO SCORE_LEN - 1
    {
        PRINT(" ");
        PRINT(SCORE_RD[I]);
    }
    PRINT(/);

    // 4. 比較
    FOR I = 0 TO SCORE_LEN - 1
    {
        IF SCORE_WR[I] != SCORE_RD[I]
        {
            PRINT("MISMATCH AT ");
            PRINT(I);
            PRINT(/);
            RETURN;
        }
    }
    PRINT("OK: HISCORE ROUNDTRIP");
    PRINT(/);
}
