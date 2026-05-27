// X1 多段 tape ロード sample (= #MODULE overlay 経由)
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
//      → OVERLAY_RESULT に 42 を書込
//   4) main で "STAGE2 RESULT = 42" 表示 + INKEY(1) で stop 解除
//
// 注: overlay 内では PRINT 等 SLANG runtime call は使わない (= 既存 #MODULE +
//      x1native の組合せで overlay 内から sXYADR / AT_WIDTH (= sWORK 内 BSS)
//      参照が EXTERN 宣言されない既存問題のため別 issue 対応予定)。
//      overlay は値計算 / VAR 書込のみ、 表示は main 側で。

VAR OVERLAY_RESULT;

#MODULE $4000

// overlay 内 関数 (= prelink で main から call 可能、 overlay binary は $4000 に
//  load される前提、 関数の実 addr は SLANG runtime planner が解決)。
// 注: PRINT 等 runtime call は本 sample では使用しない (= 上記既存問題)。
overlay_entry()
BEGIN
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
