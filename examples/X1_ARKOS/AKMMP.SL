// ====================================================================================================
// Arkos Tracker AKM driver (Minimalist) sample (= BGM 再生 + SFX overlay、 4 env 1 ソース)
//
// AKG sample (ARKOSMP.SL) と中間層 (ARKOS_*) / 配置 / ロード手順は完全に同じ。 違いは
//   - driver bin が PSGAKM (Minimalist player、 AKG より小さい)
//   - BGM データが .akm (minimalist フォーマット)
// だけ。 楽曲フォーマットの差は driver bin 内で吸収されるため、 SLANG 側の呼び出しは
// AKG と一字一句同じ ARKOS_* で済む (= 中間層は AKG/AKM 共通)。
//
// driver / BGM / SFX を本体に embed せず、 各 env のネイティブ手段で実行時ロードする:
//   x1 / sosx1     : FOPEN + FREAD でディスクから読む
//   x1native       : MTREAD で多段 tape の後続ステージを読む
//   x1native_slfs  : FS_READ_BY_ID で SLFS asset を読む
//
// ビルドは Makefile 経由を推奨:
//   cd examples/X1_ARKOS && make akm-x1 / make akm-sosx1 / make akm-x1native / make akm-x1native_slfs
//
// 操作: BGM が鳴る、 SPACE で SFX#1 を channel 0 で再生、 ENTER で終了。
// ====================================================================================================

// FREAD 用の file size 定数 (= Makefile が driver/BGM/SFX の実 byte 数から生成)。
// x1native / x1native_slfs は size 不要だが、 全 env 共通で include する
// (= 非 x1 env の FREAD が DRV_SIZE 等を参照するため)。
#INCLUDE AKMMP.sizes.inc

// SLFS の generated header (= FILE_* asset id)。 slangbuild が --slfs-add から
// 自動生成するため、 SLFS build でのみ存在する → #IF SLFS 内でのみ include。
#IF X1NATIVE
  #IF SLFS
    #INCLUDE AKMMP.assets.inc
  #END
#END

// 配置アドレス (= Arkos データはアドレス固定、 driver は RASM -DDRIVER_ORG で同 ORG ビルド)。
CONST DRV_ADDR = $8000;             // driver (PSGAKM_8000.bin)
CONST BGM_ADDR = $B000;             // BGM.AKM (Arkos export アドレス)
CONST SFX_ADDR = $C000;             // SE.AKX
CONST ASM ARKOS_BASE = $8000;       // 中間層が symbol 参照 (= DRV_ADDR と同値、 AKG と共通)

VAR CTC_PORT, K;

// polling mode (= CTC なし) で VSYNC ごとに player を tick (= CTC mode 時は no-op)
VSYNC_PROC()
{
    IF CTC_PORT == 0 THEN ARKOS_PSG_PROC();
}

MAIN()
{
    PRINT("ARKOS TRACKER AKM SAMPLE", /);
    PRINT("SPACE = SFX, ENTER = QUIT", /);

    // === driver / BGM / SFX を env 別の手段でロード ===
#IF X1NATIVE
  #IF SLFS
    // x1native_slfs: FS_READ_BY_ID(id, addr)。 id は generated header の FILE_* 経由。
    FS_READ_BY_ID(FILE_PSGAKM_BIN, DRV_ADDR);
    FS_READ_BY_ID(FILE_BGM_AKM, BGM_ADDR);
    FS_READ_BY_ID(FILE_SE_AKX, SFX_ADDR);
  #ELSE
    // x1native (tape): MTREAD(addr)。 tape stage 順 = driver → BGM → SFX
    // (= Makefile の --tape-add 順と一致させる)。
    MTREAD(DRV_ADDR);
    MTREAD(BGM_ADDR);
    MTREAD(SFX_ADDR);
  #END
#ELSE
    // x1 / sosx1: FOPEN(fnum, name, mode=0) + FREAD(fnum, addr, size) + FCLOSE。
    // file 名は Makefile の disk 投入名と完全一致 (= 拡張子込み)。
    FOPEN(0, "PSGAKM.BIN", 0); FREAD(0, DRV_ADDR, DRV_SIZE); FCLOSE(0);
    FOPEN(0, "BGM.AKM",    0); FREAD(0, BGM_ADDR, BGM_SIZE); FCLOSE(0);
    FOPEN(0, "SE.AKX",     0); FREAD(0, SFX_ADDR, SFX_SIZE); FCLOSE(0);
#END

    // === 以降 env 非依存 (= AKG/AKM 共通中間層) ===
    // CTC 検出 → 結果次第で mode 切替 (= 固定 port 禁止、 X1 共通 helper 経由)。
    CTC_PORT = X1_CTC_PORT();
    IF CTC_PORT != 0 THEN {
        // CTC mode (= 自動 ISR drive、 x1native は trampoline 経由 RETI)
        ARKOS_SET_CTC_PORT(CTC_PORT, X1_CTC_VEC());
        ARKOS_INIT(1);
    } ELSE {
        // polling mode (= CTC なし、 VSYNC_PROC が tick)
        ARKOS_INIT(0);
    }

    ARKOS_SFX_INIT(SFX_ADDR);
    ARKOS_BGM_PLAY(BGM_ADDR);

    // SPACE で SFX#1 を channel 0 で再生 (= L/H 詰め替え ABI 検証)、 ENTER で終了
    K = 0;
    WHILE K != $0D {
        K = INKEY(0);
        IF K == $20 THEN ARKOS_SFX_PLAY(1, 0);
        VSYNC(1);
    }

    ARKOS_PSG_END();
}
