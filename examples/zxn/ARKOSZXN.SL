// Arkos Tracker (AKM) の再生部 PSGAKM.bin (runtime/zxn/playerAkm_zxn_wrapper.asm を RASM で $E000 に組んだもの) を呼ぶ宣言。
// PSGAKM.bin を $E000 に置いてから使う。引数は 1 つ目 HL、2 つ目 DE。
// 呼ぶ側の決まり:
//   - ARKOS_PSG_PROC は 1 フレームに 1 回呼ぶ (曲の速さは呼ぶ回数で決まる。Arkos の曲は、ふつう 50Hz)
//   - 効果音は BGM が動いている間だけ進む
//   - 使っている間、ほかの処理が AY の選択 (ポート $FFFD) を変えない
MACHINE ARKOS_INIT(0):$E000;          // TurboSound を有効にして AY 0 を選び、音を止める
MACHINE ARKOS_BGM_PLAY(1):$E003;      // 曲 (.akm) の番地
MACHINE ARKOS_BGM_STOP(0):$E006;
MACHINE ARKOS_BGM_PAUSE(0):$E009;
MACHINE ARKOS_BGM_RESUME(0):$E00C;
MACHINE ARKOS_SFX_INIT(1):$E00F;      // 効果音 (.akx) の番地
MACHINE ARKOS_SFX_PLAY(2):$E012;      // 効果音の番号 (1 から)、チャンネル (0〜2)
MACHINE ARKOS_SFX_STOP(1):$E015;      // チャンネル (0〜2)
MACHINE ARKOS_PSG_PROC(0):$E018;      // 1 フレームに 1 回
MACHINE ARKOS_PSG_END(0):$E01B;       // BGM_STOP と同じ (この後 PSG_PROC を呼んでも鳴らない)
