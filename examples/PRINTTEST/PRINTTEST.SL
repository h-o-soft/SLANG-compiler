// ====================================================================================================
// PRINTTEST.SL — PRINT 系メソッド表示確認サンプル
//
// PRINT と各書式関数を一通り出力する。 全 X1 系 env (x1 / sosx1 / x1native /
// x1native_slfs) で同じ表示になることを確認する (= Issue #221 の HEX2$/HEX4$
// 並び順バグ + SPC$/TAB$ 欠落の回帰確認も兼ねる)。 各行の右に期待値コメント付き。
//
// 網羅する print 系関数 (= 内部の fall-through chain 別):
//   P10           : PRINT(数値)
//   MPRNT         : PRINT("文字列リテラル")
//   PCRONE→PCR→PCR1→PSTR→PRT : / (改行)
//   PHEX4→PHEX2→PHEX : HEX4$ / HEX2$        ← #221 で崩れる報告
//   P10toN        : FORM$(v, n)
//   P10to5→P10toN : DECI$(v)
//   PSIGN→P10     : PN$(v)
//   PCHR→PRT      : CHR$(v)
//   PSTR→PRT      : STR$(char, n)
//   PSPC          : SPC$(n)    ← 旧 x1native では未定義、 #221 修正で追加
//   PTAB          : TAB$(n)    ← 同上
//
// 注: FL$ (PFLOAT) / MSG$ / MSX$ は別途 (float / 文字列アドレス要)。
// ====================================================================================================

VAR K;

MAIN()
{
    PRINT("=== PRINT TEST (#221) ===", /);

    // 期待値は右コメント (= x1 / sosx1 基準)
    PRINT("DEC : ", 12345, /);                       // DEC : 12345
    PRINT("PN- : ", PN$(0 - 123), /);                // PN- : -123
    PRINT("PN+ : ", PN$(123), /);                    // PN+ : 123
    PRINT("HEX2: ", HEX2$($AB), /);                  // HEX2: AB
    PRINT("HEX4: ", HEX4$($1FA2), /);                // HEX4: 1FA2   ← 報告された崩れ
    PRINT("HEX4: ", HEX4$($00FF), /);                // HEX4: 00FF
    PRINT("HEX4: ", HEX4$($EEC0), /);                // HEX4: EEC0
    PRINT("FORM: [", FORM$(42, 6), "]", /);          // FORM: [    42]
    PRINT("DECI: [", DECI$(42), "]", /);             // DECI: [   42]
    PRINT("CHR : ", CHR$(65), CHR$(66), CHR$(67), /);// CHR : ABC
    PRINT("STR : ", STR$($2A, 10), /);               // STR : **********
    PRINT("SPC : [", SPC$(5), "]", /);               // SPC : [     ]  (5 空白)
    PRINT("TAB : [", TAB$(3), "]", /);               // TAB : [<tab×3>]

    PRINT("=== END ===", /);

    // キー待ち (= x1native は OS に戻らないので画面保持。 x1/sosx1 でも待つ)
    K = 0;
    WHILE K == 0 { K = INKEY(0); }
}
