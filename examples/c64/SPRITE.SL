// Commodore 64 sprite bounce demo (oscar64 backend, env c_bindings: 経由)
//
// Build:
//   slangbuild -E c64 examples/c64/SPRITE.SL -o examples/c64/SPRITE
//
// 期待動作: 白い四角スプライト 1 個が画面内を斜め移動して端でバウンス。

#INCLUDE "C64_VIC.LIB"

// sprite block 192 = $3000 (= 192 * 64)。SPR_SET の image 引数で
// この block number を渡せば bridge 内 pointer table 反映で
// SLANG 側 MEM[$07F8] 手書き不要。
CONST SPRITE_IMG_BLOCK = 192;
CONST SPRITE_IMG_ADDR  = $3000;

VAR X, Y, DX, DY;
ARRAY BYTE SPRDATA[63];   // SLANG 仕様 N+1 = 64 byte (sprite block 全体)

MAIN()
{
    VAR I;

    // 1. sprite データを SPRDATA に作る (= 単色四角)。
    //    sprite 表示データは先頭 63 byte だが、64 byte block 全体を埋めて
    //    未初期化バイト由来の表示揺れを防ぐ。
    FOR I = 0 TO 63 { SPRDATA[I] = $FF; }

    // 2. SPRDATA を $3000 に手動コピー (= sprite block の実体配置)
    FOR I = 0 TO 63 { MEM[SPRITE_IMG_ADDR + I] = SPRDATA[I]; }

    // 3. env binding 経由で sprite 0 を有効化、配置、色設定。
    //    SPR_INIT は screen RAM 先頭 ($0400) を bridge に渡す。
    //    bridge 内で pointer table base (= $07F8) を保持し、後段 SPR_SET の
    //    image 引数で自動反映。
    SPR_INIT($0400);
    SPR_SET(0, 1, 100, 100, SPRITE_IMG_BLOCK, VCOL_WHITE, 0, 0, 0);

    // 4. 移動ループ。LOOP は無限、画面端 (= sprite hardware の表示範囲) で
    //    DX/DY 反転。EXIT 条件は今は無し (= STOP キーで break、または別途
    //    キー処理を入れる)。
    X = 100; Y = 100; DX = 2; DY = 1;
    LOOP
    {
        VIC_WAIT();            // VSYNC 待ち (= 1 フレーム同期、tearing 防止)
        X = X + DX;
        Y = Y + DY;
        IF X < 24 || X > 320 THEN DX = 0 - DX;
        IF Y < 50 || Y > 220 THEN DY = 0 - DY;
        SPR_MOVE(0, X, Y);
    }
}
