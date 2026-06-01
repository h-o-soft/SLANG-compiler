// ====================================================================================================
// banjo (Furnace tracker) driver sample — AY/PSG or OPM/FM song playback (4 env 1 source)
//
// driver / song を本体に embed せず、 各 env のネイティブ手段で実行時ロードする:
//   x1 / sosx1     : FOPEN + FREAD でディスクから読む
//   x1native       : MTREAD で多段 tape の後続ステージを読む
//   x1native_slfs  : FS_READ_BY_ID で SLFS asset を読む
// ロード後は env 非依存の中間層 (BANJO_*) を呼ぶ。
//
// ビルドは Makefile 経由 (= driver bin の生成 + 曲 .fur のコンバート + asset 同梱 + sizes.inc 生成):
//   cd examples/X1_BANJO && make x1 / make sosx1 / make x1native / make x1native_slfs
//   ※ 曲データ assets/song.fur を用意してください (Furnace tracker、 AY/PSG or OPM/FM)。
//
// SFX (効果音) は AY 専用。 CHIP=ay でビルドすると assets/sfx.fur が SFX として同梱され、 SPACE で
// 再生できる (CHIP=opm/both は SFX 無し)。 配置アドレス (SONG_ADDR/SFX_ADDR) と SFX 有効フラグ
// (BANJO_SFX_ENABLED) は Makefile が BANJOMP.config.inc に生成する (chip 別レイアウトと一致)。
//
// 操作: 曲が鳴る、 (CHIP=ay なら) SPACE で SFX、 ENTER で終了。
// CTC が見つかれば CTC tick、見つからなければ VSYNC polling で BANJO_UPDATE()。
// ====================================================================================================

// FREAD 用の file size 定数 (= Makefile が driver/song の実 byte 数から生成)。
// x1native / x1native_slfs は size 不要だが、 全 env 共通で include する
// (= 非 x1 env の FREAD が DRV_SIZE 等を参照するため)。
#INCLUDE BANJOMP.sizes.inc
#INCLUDE BANJOMP.config.inc

// SLFS の generated header (= FILE_* asset id)。 slangbuild が --slfs-add から
// 自動生成するため、 SLFS build でのみ存在する → #IF SLFS 内でのみ include。
#IF X1NATIVE
  #IF SLFS
    #INCLUDE BANJOMP.assets.inc
  #END
#END

// 配置アドレス。 driver は固定 ORG=$8000。 SONG_ADDR / SFX_ADDR は chip 別レイアウトのため
// Makefile が BANJOMP.config.inc に生成する (= prep_song の --load-addr と同一ソース)。
CONST DRV_ADDR = $8000;             // driver (driver.bin)
CONST ASM BANJO_BASE = $8000;       // 中間層が JP 先に使う (= DRV_ADDR と同値)

CONST ASM BANJO_LOOP_ON = 1;        // 末尾でループ再生 (0 = 1 回で停止)

VAR K, CTC_PORT;

// VSYNC ごとに player を tick。 CTC mode 時は driver 側 (banjo_x1_update) が ctc_port を見て
// no-op にするので、 ここは無条件呼出でよい (= SLANG 側の IF 判定を省いて毎フレーム軽くする)。
VSYNC_PROC()
{
    BANJO_UPDATE();
}

MAIN()
{
    // --- driver / song を実行時ロード (env 別の手段) ---
#IF X1NATIVE
  #IF SLFS
    FS_READ_BY_ID(FILE_BANJODRV_BIN, DRV_ADDR);
    FS_READ_BY_ID(FILE_BANJOSNG_BIN, SONG_ADDR);
    #IF BANJO_SFX_ENABLED
    FS_READ_BY_ID(FILE_BANJOSFX_BIN, SFX_ADDR);
    #END
  #ELSE
    MTREAD(DRV_ADDR);
    MTREAD(SONG_ADDR);
    #IF BANJO_SFX_ENABLED
    MTREAD(SFX_ADDR);             // tape 順 = Makefile の --tape-add 順 (driver → song → sfx)
    #END
  #END
#ELSE
    FOPEN(0, "BANJODRV.BIN", 0);
    FREAD(0, DRV_ADDR, DRV_SIZE);
    FCLOSE(0);
    FOPEN(0, "BANJOSNG.BIN", 0);
    FREAD(0, SONG_ADDR, SONG_SIZE);
    FCLOSE(0);
    #IF BANJO_SFX_ENABLED
    FOPEN(0, "BANJOSFX.BIN", 0);
    FREAD(0, SFX_ADDR, SFX_SIZE);
    FCLOSE(0);
    #END
#END

    // --- 再生開始 ---
    CTC_PORT = X1_CTC_PORT();
    IF CTC_PORT != 0 THEN BANJO_SET_CTC_PORT(CTC_PORT, X1_CTC_VEC());

    BANJO_INIT(BANJO_CHIP_FLAG);         // 使用 chip を宣言して driver 初期化
    // ※ SFX エンジンの state 初期化は BANJO_INIT が自動で行う (明示 BANJO_SFX_INIT() 不要)。
    BANJO_PLAY(SONG_ADDR, BANJO_LOOP_ON); // 曲の再生開始

    PRINT("banjo (Furnace) player", /);
#IF BANJO_SFX_ENABLED
    PRINT("SPACE: SFX  ENTER: quit", /);
#ELSE
    PRINT("ENTER: quit", /);
#END
    LOOP
    {
        VSYNC(1);                             // VSYNC_PROC が polling update を tick
        K = INKEY(0);
#IF BANJO_SFX_ENABLED
        IF K == $20 THEN BANJO_SFX_PLAY(SFX_ADDR);   // SPACE で SFX 再生 (AY)
#END
        IF K == 13 THEN EXIT;
    }
    BANJO_END();
}
