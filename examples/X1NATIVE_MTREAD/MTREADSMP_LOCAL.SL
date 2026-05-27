// X1 多段 tape ロード sample (= #MODULE Local mode、 RESIDENT 指定なし)
//
// build: slangbuild -E x1native -I include examples/X1NATIVE_MTREAD/MTREADSMP_LOCAL.SL \
//        -o /tmp/MTREADSMP_LOCAL --emit tape
//
// Local mode (= #MODULE $4000、 RESIDENT 省略) では overlay 内に runtime
// routine が **local copy** される。 runtime work sym (sXYADR / AT_WIDTH 等)
// は main 側 sWORK を EXTERN 参照 (= main work area 共有前提、 Issue #209 fix
// で対応)。 overlay binary は RESIDENT mode より大きい (= runtime routine 全
// コピー) が、 通常の overlay 運用で動作する。
//
// 注: overlay 単独動作 (= main work area 完全上書き、 self-contained mode)
//     は本 PR scope 外 (= 別 Issue 対応予定)。

VAR OVERLAY_RESULT;

#MODULE $4000

// overlay 内 関数 (= Local mode で local copy された runtime 経由)。
// PRINT 等 SLANG runtime call も使える (= main 側 sWORK BSS sym を EXTERN
// 参照、 Issue #209 fix で動作可能になった)。
overlay_entry()
BEGIN
    PRINT("STAGE2 OVERLAY LOADED (LOCAL MODE)!");
    PRINT(/);
    OVERLAY_RESULT = 42;
END;

#END

MAIN()
BEGIN
    PRINT("STAGE1: LOADING OVERLAY (LOCAL MODE)...");
    PRINT(/);
    OVERLAY_RESULT = 0;
    MTREAD($4000);
    overlay_entry();    // prelink が CALL 解決 (= 既存 #MODULE 機構)
    PRINT("STAGE2 RESULT = ");
    PRINT(%(OVERLAY_RESULT));
    PRINT(/);
    PRINT("PRESS ANY KEY...");
    INKEY(1);
END;
