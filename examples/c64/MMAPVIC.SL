// C64 v0.25.0 動作確認 sample (= 最低限ゲーム作成 API: mmap / vic_setmode / memcpy)
// build: slangbuild -E c64 -I include examples/c64/MMAPVIC.SL -o MMAPVIC
// 実行: VICE x64sc で MMAPVIC.prg load + RUN
//
// 期待: 画面左上に "MMAPVIC DEMO" 12 char 表示

#INCLUDE "C64_MEMMAP.LIB"
#INCLUDE "C64_VIC.LIB"

// MEMCPY 確認用 source data (= SLANG コード内 ARRAY BYTE bake-in、
// SLANG string initializer 構文 = `ARRAY BYTE X[] = { "..." };`)
ARRAY BYTE GREETING[] = { "MMAPVIC DEMO" };   // 12 char + NUL terminator

MAIN()
BEGIN
    // 1. trampoline install (= IRQ/NMI が mmap 切替中も動作維持)
    MMAP_TRAMPOLINE();

    // 2. BASIC ROM disable (= I/O は visible に維持、 VIC_SETMODE が I/O register 叩ける)
    //    Note: MMAP_RAM だと I/O 不可視で VIC_SETMODE 動かない、 NO_BASIC 推奨
    MMAP_SET(MMAP_NO_BASIC);

    // 3. text mode (= standard、 charset 触らず VIC bank default の CHAR ROM 使用)
    VIC_SETMODE(VICM_TEXT, VIC_SCREEN_DEFAULT, VIC_FONT_ROM);

    // 4. MEMCPY で SLANG ARRAY → screen RAM 直接書込み (= 通常 memory copy 動作確認)
    MEMCPY(VIC_SCREEN_DEFAULT, GREETING, 12);   // 画面左上 12 char (NUL 除く)

    INKEY(1);
END;
