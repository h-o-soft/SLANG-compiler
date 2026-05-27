// X1 多段 tape ロード sample (= #MODULE overlay 経由、 RESIDENT mode)
//
// build: slangbuild -E x1native -I include examples/X1NATIVE_MTREAD/MTREADSMP.SL \
//        -o /tmp/MTREADSMP --emit tape
// → /tmp/MTREADSMP.tap (= main + overlay._m0 自動連結、 各段 標準 sync 仕様)
//
// 実行: emulator (xmil / X-millenium) で MTREADSMP.tap mount → IPL boot
//   1) 1 段目 (main) auto load + exec → "STAGE1: LOADING OVERLAY..." 表示
//   2) MTREAD($4000) で 2 段目 (overlay._m0) を $4000 に load
//      (= 内部で MT_CTRL_PLAY で deck 再生再開、 info+data block read、 MT_CTRL_STOP)
//   3) overlay_entry() call (= prelink が overlay 内 関数を addr 解決)
//      → "STAGE2 OVERLAY LOADED!" 表示 + OVERLAY_RESULT に 42 を書込
//   4) main で "STAGE2 RESULT = 42" 表示 + INKEY(1) で stop 解除
//
// 注: `#MODULE $4000 RESIDENT` で **RESIDENT mode** 指定 = overlay 内 runtime
//     call (PRINT 等) は main 側 shared runtime への EXTERN 参照に変換され、
//     overlay binary は大幅縮小 (= 例: 248 byte → 36 byte、 -85%)。
//     RESIDENT 指定なし (= Local default) だと overlay 内に runtime routine が
//     複製され、 さらに sWORK 内 BSS sym (sXYADR / AT_WIDTH) の EXTERN 宣言
//     不足で assemble fail する。

VAR OVERLAY_RESULT;

#MODULE $4000 RESIDENT

// overlay 内 関数 (= prelink で main から call 可能、 overlay binary は $4000 に
//  load される前提、 関数の実 addr は SLANG runtime planner が解決)。
// RESIDENT mode のため PRINT 等 runtime call OK (= shared promotion 経由)。
overlay_entry()
BEGIN
    PRINT("STAGE2 OVERLAY LOADED!");
    PRINT(/);
    OVERLAY_RESULT = 42;
END;

#END

MAIN()
BEGIN
    PRINT("STAGE1: LOADING OVERLAY...");
    PRINT(/);
    OVERLAY_RESULT = 0;
    // overlay binary (= overlay._m0.bin) を $4000 に load
    // 注: overlay 先頭が必ず overlay_entry とは限らない。
    //     overlay 経由なら MTREAD + 関数呼出で安全に呼び出す
    //     (= MTREADJP($4000, $4000) は raw stage 用)。
    MTREAD($4000);
    overlay_entry();    // prelink が CALL 解決 (= 既存 #MODULE 機構)
    PRINT("STAGE2 RESULT = ");
    PRINT(%(OVERLAY_RESULT));
    PRINT(/);
    PRINT("PRESS ANY KEY...");
    INKEY(1);
END;
