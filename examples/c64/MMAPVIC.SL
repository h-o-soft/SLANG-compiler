// C64 v0.25.0 動作確認 sample (= 最低限ゲーム作成 API: mmap / vic_setmode / memcpy)
// build: slangbuild -E c64 -I include examples/c64/MMAPVIC.SL -o MMAPVIC
// 実行: VICE x64sc で MMAPVIC.prg load + RUN
//
// 期待:
//   1) 画面 clear → 中央に「自前 顔風 char」 3 個 → キー
//   2) ROM CHARSET 戻し (= screen 触らず font 切替だけ) → 同位置が ROM char 0 = '@' 3 個 → キー
//   3) 終了
//
// 動作 (= 自前 font 切替時の典型 pattern):
//   1) mmap_trampoline + mmap_set で BASIC disable + I/O visible 確保
//   2) 自前 font area ($3000) の char 32 (= space) と char 0 (= 顔) を SLANG ARRAY から MEMCPY で定義
//      (= 他の char index は未定義 garbage、 ただし screen 全 space で埋めるので見えない)
//   3) vic_setmode で自前 font 切替
//   4) MEMSET で screen RAM 全 1000 byte を char 32 (= space) で fill (= 起動時 BASIC 残骸 clear)
//   5) MEMSET で screen 中央に char 0 (= 顔) を 3 個書込
//   6) キー → vic_setmode で font を ROM CHARSET ($1000) に戻す (= screen 触らず)
//      → screen の char index 0 が ROM の '@' 3 個に変化 (= visual 確認)
//   7) キー → 終了
//
// font area $3000 の場所:
//   VIC bank 0 ($0000-$3FFF) 内、 BASIC ($0800-$0FFF) でも CHAR ROM shadow
//   ($1000-$1FFF) でもない自由領域 ($2000-$3FFF) の中盤。

#INCLUDE "C64_MEMMAP.LIB"
#INCLUDE "C64_VIC.LIB"

// 空白 char (= 8 byte 全 0 = 黒い square、 char index 32 に書込)
ARRAY BYTE BLANK_CHAR[] = { 0, 0, 0, 0, 0, 0, 0, 0 };

// 顔風 char (= 8x8 pixel = 8 byte、 char index 0 に書込)
//   ████████  $FF
//   █......█  $81
//   █.█..█.█  $A5
//   █......█  $81
//   █.████.█  $BD
//   █..██..█  $99
//   █......█  $81
//   ████████  $FF
ARRAY BYTE FACE_CHAR[] = {
    $FF, $81, $A5, $81, $BD, $99, $81, $FF
};

CONST FONT_AREA = $3000;     // VIC bank 0 内 自由領域 (= $2000-$3FFF)
CONST SCR_SIZE  = 1000;      // 40 col × 25 row = 1000 byte
CONST SCR_ROW   = 12;        // 表示 行
CONST SCR_COL   = 19;        // 表示 開始 col
CONST SCR_COUNT = 3;         // 並べる char 数

VAR K;
VAR OLD_MAP;     // mmap restore 用 (= oscar64 exit path が default mmap 想定)

MAIN()
BEGIN
    // 1. trampoline install (= IRQ/NMI が mmap 切替中も動作維持)
    MMAP_TRAMPOLINE();

    // 2. BASIC ROM disable (= I/O visible に維持、 旧 mmap 値を保存)
    OLD_MAP = MMAP_SET(MMAP_NO_BASIC);

    // 3. 自前 char data を font area に書込 (= SLANG ARRAY → RAM、 src/dst 別で安全)
    MEMCPY(FONT_AREA + 32 * 8, BLANK_CHAR, 8);    // char 32 (= space) = 黒
    MEMCPY(FONT_AREA + 0 * 8,  FACE_CHAR,  8);    // char 0 = 顔

    // 4. VIC font 切替 (= screen = $0400、 font = $3000)
    VIC_SETMODE(VICM_TEXT, VIC_SCREEN_DEFAULT, FONT_AREA);

    // 5. screen 全体を 32 (= 自前 space = 黒) で fill = 起動時 BASIC 残骸 clear
    MEMSET(VIC_SCREEN_DEFAULT, 32, SCR_SIZE);

    // 6. 中央 3 char に char 0 (= 顔) を fill
    MEMSET(VIC_SCREEN_DEFAULT + SCR_ROW * 40 + SCR_COL, 0, SCR_COUNT);

    // 7. 任意キー待ち (= c64 backend INKEY は常時 non-blocking、 自前 loop 必要)
    K = 0;
    WHILE K == 0 { K = INKEY(0); }

    // 8. ROM CHARSET に戻す (= screen content 触らず、 font 切替だけ)
    //    → screen の char index 0 (= 中央 3 個) が ROM の char 0 = '@' に変化
    //    (= 「自前 font → ROM font」 visual 確認、 char index 32 は ROM でも space で透明)
    VIC_SETMODE(VICM_TEXT, VIC_SCREEN_DEFAULT, VIC_FONT_ROM);

    // 9. 再 wait (= '@' 3 個表示確認後 終了)
    K = 0;
    WHILE K == 0 { K = INKEY(0); }

    // 10. mmap restore (= oscar64 exit path / BASIC return が default mmap 想定、
    //     MMAP_NO_BASIC のまま return すると runtime 副作用 risk あり、 Codex 指摘 fix)
    MMAP_SET(OLD_MAP);
END;
