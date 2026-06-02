// ====================================================================================================
// banjo 複数楽曲・複数SFX サンプル (X1、 AY/PSG)
//
// 複数の曲/SFX を 1 つの bundle (banjodat.bin) にまとめて実行時ロードし、 生成された
// アドレス定数で再生する。 アドレスは Makefile + banjo_pack_assets.py が manifest
// (assets/banjo_assets.txt) から自動算出し BANJOMULTI.assets.addr.inc に出力する:
//   bgm1.fur -> MUSIC_BGM1、 bgm2.fur -> MUSIC_BGM2、 beep.fur -> SFX_BEEP
// (manifest を変えたら下の参照定数名も合わせること)。
//
// ビルド: cd examples/X1_BANJO_MULTI && make x1 / sosx1 / x1native / x1native_slfs (CHIP=ay)
//   ※ assets/ に bgm1.fur / bgm2.fur (AY 曲) と beep.fur (AY、 ch2 に SFX) を用意する。
//
// 操作: 1=曲1 / 2=曲2 / SPACE=SFX / ENTER=終了。
// ====================================================================================================

// Makefile が生成 (アドレス/サイズ/有効フラグ + MUSIC_*/SFX_* 定数)。
#INCLUDE BANJOMULTI.assets.addr.inc
// Makefile が生成 (CONST ASM BANJO_CHIP_FLAG)。
#INCLUDE BANJOMULTI.config.inc

// SLFS の generated header (FILE_* asset id)。 SLFS build でのみ存在。
#IF X1NATIVE
  #IF SLFS
    #INCLUDE BANJOMULTI.assets.inc
  #END
#END

CONST DRV_ADDR = $8000;             // driver (driver.bin)
CONST ASM BANJO_BASE = $8000;       // 中間層 JP 先 (= DRV_ADDR)
CONST ASM BANJO_LOOP_ON = 1;        // ループ再生

VAR K, CTC_PORT;

// CTC mode 時は driver 側 (banjo_x1_update) が ctc_port を見て no-op にするので無条件で可。
VSYNC_PROC()
{
    BANJO_UPDATE();
}

MAIN()
{
    // --- driver + bundle (全曲/SFX) を実行時ロード (env 別) ---
#IF X1NATIVE
  #IF SLFS
    FS_READ_BY_ID(FILE_BANJODRV_BIN, DRV_ADDR);
    FS_READ_BY_ID(FILE_BANJODAT_BIN, BANJODAT_BASE);
  #ELSE
    MTREAD(DRV_ADDR);
    MTREAD(BANJODAT_BASE);          // tape 順 = Makefile --tape-add 順 (driver -> bundle)
  #END
#ELSE
    FOPEN(0, "BANJODRV.BIN", 0);
    FREAD(0, DRV_ADDR, DRV_SIZE);
    FCLOSE(0);
    FOPEN(0, "BANJODAT.BIN", 0);
    FREAD(0, BANJODAT_BASE, BANJODAT_SIZE);
    FCLOSE(0);
#END

    // --- 再生開始 ---
    CTC_PORT = X1_CTC_PORT();
    IF CTC_PORT != 0 THEN BANJO_SET_CTC_PORT(CTC_PORT, X1_CTC_VEC());

    BANJO_INIT(BANJO_CHIP_FLAG);
    BANJO_PLAY(MUSIC_BGM1, BANJO_LOOP_ON);   // 生成されたアドレス定数で再生

    PRINT("banjo multi-asset player", /);
#IF BANJO_BUNDLE_HAS_SFX
    PRINT("1/2: song  SPACE: sfx  ENTER: quit", /);
#ELSE
    PRINT("1/2: song  ENTER: quit", /);
#END
    LOOP
    {
        VSYNC(1);
        K = INKEY(0);
        IF K == $31 THEN BANJO_PLAY(MUSIC_BGM1, BANJO_LOOP_ON);   // '1'
        IF K == $32 THEN BANJO_PLAY(MUSIC_BGM2, BANJO_LOOP_ON);   // '2'
#IF BANJO_BUNDLE_HAS_SFX
        IF K == $20 THEN BANJO_SFX_PLAY(SFX_BEEP);                // SPACE
#END
        IF K == 13 THEN EXIT;
    }
    BANJO_END();
}
