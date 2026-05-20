// Commodore 64 HVSC SID BGM demo (oscar64 backend、env c_bindings: 経由)
//
// disk から PSID v2 .sid file を読み込み → header parse + payload を
// loadAddress に memcpy → init/play address を bridge 内 static 保存 →
// VSYNC ループで毎フレーム play 呼出、というBGM再生フローの最小 sample。
//
// 重要 (= memory layout の制約):
//   SLANG コード側で `ARRAY BYTE BUF[4096]` 等を declare すると、oscar64 が
//   生成する BSS が $0F78-$1F91 等の領域を占有し、HVSC tune (= loadAddress
//   $1000 系が多数) の payload を memcpy する際に **SLANG runtime の global
//   変数を破壊** する (= self-destruction)。これを回避するため、本 sample は
//   `KIO_READ_ADDR` で $C000 (= C64 伝統的 free RAM、BASIC / KERNAL いずれも
//   未使用) に disk 直 read し、`SID_LOAD_FROM_BUF_ADDR` で raw addr を bridge
//   に渡す形を取る。loadAddress > $2000 系 tune だけ扱うなら通常の ARRAY BYTE
//   経由でも動くが、$1000 系も扱えるよう本 sample は raw addr 流儀を採用。
//
// 用意するもの (= user 側の準備、SLANG repo には .sid 同梱しない):
//   - HVSC (= https://www.hvsc.c64.org/) 等から PSID v2 .sid file を 1 つ
//     手元に取得 (= 4KB 以内、playAddress != 0)
//     ※ HVSC の tune はすべて copyright 残存 (= FAQ Q29、配布物に埋め込まず
//        user 環境で個別に持ち込む形態)
//   - c1541 で空の D64 image に .sid を PRG として書き込む:
//       c1541 -format "bgm,01" d64 /tmp/sidbgm.d64 \
//             -write examples/c64/SIDBGM.prg sidbgm \
//             -write your_tune.sid music
//   - VICE 起動:
//       x64sc -8 /tmp/sidbgm.d64
//       LOAD"sidbgm",8
//       RUN
//
// 操作:
//   起動 = song 0 で再生開始
//   1..9 = song 切替 (= multi-song の .sid のみ意味あり)
//
// Build (= checkout 状態 / repo root から実行):
//   slangbuild -E c64 -I include examples/c64/SIDBGM.SL -o examples/c64/SIDBGM

#INCLUDE "C64_KIO.LIB"
#INCLUDE "C64_SID.LIB"

CONST FILE_NUM  = 2;
CONST BUF_ADDR  = $C000;         // 伝統的 free RAM area (= 4KB)、SLANG BSS と衝突しない
CONST BUF_SIZE  = 4096;

MAIN()
{
    VAR OK, N, K, SONG;

    // 1. disk から "music" PRG を $C000 領域に直 read (= SLANG ARRAY 経由せず、
    //    SLANG runtime の BSS と衝突しない free area を使う)
    OK = KIO_OPEN_NAMED(FILE_NUM, DEV_DISK, CH_READ, "music,p,r");
    IF OK == 0 THEN
    {
        PRINT("OPEN FAIL ST=");
        PRINT(KIO_STATUS());
        PRINT(/);
        KIO_CLOSE(FILE_NUM);
        RETURN;
    }

    IF KIO_CHKIN(FILE_NUM) == 0 THEN
    {
        PRINT("CHKIN FAIL ST=");
        PRINT(KIO_STATUS());
        PRINT(/);
        KIO_CLOSE(FILE_NUM);
        RETURN;
    }

    N = KIO_READ_ADDR(FILE_NUM, BUF_ADDR, BUF_SIZE);
    KIO_CLRCHN();
    KIO_CLOSE(FILE_NUM);

    IF N AND $8000
    {
        PRINT("READ ERR ST=");
        PRINT(KIO_STATUS());
        PRINT(/);
        RETURN;
    }

    // 2. PSID v2 header parse + payload を loadAddress に memcpy + init/play addr 保存
    OK = SID_LOAD_FROM_BUF_ADDR(BUF_ADDR, N);
    IF OK == 0 THEN
    {
        PRINT("SID PARSE FAIL");
        PRINT(/);
        PRINT("(PSID V2 + PLAYADDR!=0 REQUIRED)");
        PRINT(/);
        RETURN;
    }

    // 3. SID 全 register 0 にして安全状態 + master volume max + player init (song 0)
    SID_INIT_QUIET();
    SID_VOLUME(15);
    SONG = 0;
    SID_PLAYER_INIT(SONG);

    PRINT("PLAYING (1-9 SWITCH SONG)");
    PRINT(/);

    // 4. VSYNC loop で player の play() を毎フレーム呼出 (= 50Hz tick)
    LOOP
    {
        VIC_WAIT();
        SID_PLAYER_PLAY();

        // 1..9 キーで song 切替 (= multi-song の .sid 用)
        K = INKEY(0);
        IF K >= 49 THEN
        {
            IF K <= 57 THEN
            {
                SONG = K - 49;        // '1'..'9' → 0..8
                SID_INIT_QUIET();
                SID_VOLUME(15);
                SID_PLAYER_INIT(SONG);
            }
        }
    }
}
