///
/// SPRTEST.SL — SPRLIB テスト
///
/// 16x16 白ダイヤモンドスプライトを カーソルキー で 4 ドット単位移動
///

#include SPRLIB.LIB

// 16x16 ダイヤモンド (白, 96 bytes: B32+R32+G32)
ARRAY BYTE SPRITE[] = {
	// B プレーン
	$01,$80, $03,$C0, $07,$E0, $0F,$F0,
	$1F,$F8, $3F,$FC, $7F,$FE, $FF,$FF,
	$FF,$FF, $7F,$FE, $3F,$FC, $1F,$F8,
	$0F,$F0, $07,$E0, $03,$C0, $01,$80,
	// R プレーン
	$01,$80, $03,$C0, $07,$E0, $0F,$F0,
	$1F,$F8, $3F,$FC, $7F,$FE, $FF,$FF,
	$FF,$FF, $7F,$FE, $3F,$FC, $1F,$F8,
	$0F,$F0, $07,$E0, $03,$C0, $01,$80,
	// G プレーン
	$01,$80, $03,$C0, $07,$E0, $0F,$F0,
	$1F,$F8, $3F,$FC, $7F,$FE, $FF,$FF,
	$FF,$FF, $7F,$FE, $3F,$FC, $1F,$F8,
	$0F,$F0, $07,$E0, $03,$C0, $01,$80
};

VAR PX, PY;

VSYNC_PROC()
{
}

MAIN()
VAR K;
{
	WIDTH(40);
	GRPSETUP();
	GRDISP(1);
	SPR_INIT();

	PX = 20;	// 4 ドット単位 X (= 80 ピクセル)
	PY = 12;	// 4 ドット単位 Y (= 48 ピクセル)

	SPR_SET_PATTERN(0, SPRITE);
	SPR_SET_POS(0, PX, PY);
	SPR_SET_SHOW(0, 1);

	LOOP
	{
		K = INKEY(0);

		// カーソルキー: 4 ドット単位移動
		IF K == 29 AND PX > 0 THEN PX--;
		ELIF K == 28 AND PX < 75 THEN PX++;
		IF K == 30 AND PY > 0 THEN PY--;
		ELIF K == 31 AND PY < 46 THEN PY++;

		SPR_SET_POS(0, PX, PY);
		VSYNC(1);
		SPR_UPDATE();
	}
}
