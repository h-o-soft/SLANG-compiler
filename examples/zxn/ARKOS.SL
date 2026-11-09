// Arkos Tracker (AKM) の曲と効果音を ZX Spectrum Next の AY で鳴らす見本。
// 置き場 (ARKOS.cfg で NEX に入れる): 本体 $8000〜, 曲 BGM.AKM $B000, 効果音 SE.AKX $C000, 再生部 PSGAKM.bin $E000
// 操作: BGM が鳴る → SPACE で効果音 → ENTER で止める
#INCLUDE "ARKOSZXN.SL"

CONST BGM_ADDR = $B000;
CONST SFX_ADDR = $C000;

VAR K;

MAIN()
BEGIN
	// 画面を 50Hz にする (NextReg $05 の 2 ビット目を 0 に。ほかのビットは残す)。曲は 1 フレームに 1 回進める
	ZXN_WRITE_REG($05, ZXN_READ_REG($05) AND $FB);
	// フレームの割り込み (ULA) だけを許す。ZXN_VSYNC の HALT が 1 フレームに 1 回だけ解けるように
	ZXN_WRITE_REG($C4, $01);
	ZXN_WRITE_REG($C5, 0);
	ZXN_WRITE_REG($C6, 0);

	// 画面を消す (ビットマップを 0、色を白の字・黒の地に)
	FOR K = $4000 TO $57FF { MEM[K] = 0; }
	FOR K = $5800 TO $5AFF { MEM[K] = $47; }
	LOCATE(0, 0);
	PRINT("ARKOS TRACKER AKM SAMPLE", /);
	PRINT("SPACE = SFX, ENTER = STOP", /);

	ARKOS_INIT();
	ARKOS_SFX_INIT(SFX_ADDR);
	ARKOS_BGM_PLAY(BGM_ADDR);

	K = 0;
	WHILE K != $0D DO BEGIN
		ZXN_VSYNC();
		ARKOS_PSG_PROC();
		K = INKEY(0);
		IF K == $20 THEN ARKOS_SFX_PLAY(1, 0);
	END;
	ARKOS_PSG_END();
	PRINT("STOPPED", /);
END;
