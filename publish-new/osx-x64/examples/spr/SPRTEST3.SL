///
/// SPRTEST3.SL — SPRLIB 複数スプライト + パーティクルテスト
///
/// スプライト 0: プレイヤー (白ダイヤ, カーソルキー 移動)
/// スプライト 1-7: パーティクル (P キーで発射)
///

#include SPRLIB.LIB

// === プレイヤー: 白ダイヤモンド 16x16 ===
ARRAY BYTE PLAYER[] = {
	// B
	$01,$80, $03,$C0, $07,$E0, $0F,$F0,
	$1F,$F8, $3F,$FC, $7F,$FE, $FF,$FF,
	$FF,$FF, $7F,$FE, $3F,$FC, $1F,$F8,
	$0F,$F0, $07,$E0, $03,$C0, $01,$80,
	// R
	$01,$80, $03,$C0, $07,$E0, $0F,$F0,
	$1F,$F8, $3F,$FC, $7F,$FE, $FF,$FF,
	$FF,$FF, $7F,$FE, $3F,$FC, $1F,$F8,
	$0F,$F0, $07,$E0, $03,$C0, $01,$80,
	// G
	$01,$80, $03,$C0, $07,$E0, $0F,$F0,
	$1F,$F8, $3F,$FC, $7F,$FE, $FF,$FF,
	$FF,$FF, $7F,$FE, $3F,$FC, $1F,$F8,
	$0F,$F0, $07,$E0, $03,$C0, $01,$80
};

// === パーティクル: シアン小弾 8x8 中央 ===
// (16x16 のうち中央 8x8 だけ描画、周囲は透明)
ARRAY BYTE BULLET[] = {
	// B
	$00,$00, $00,$00, $00,$00, $00,$00,
	$3C,$00, $7E,$00, $7E,$00, $3C,$00,
	$3C,$00, $7E,$00, $7E,$00, $3C,$00,
	$00,$00, $00,$00, $00,$00, $00,$00,
	// R (なし)
	$00,$00, $00,$00, $00,$00, $00,$00,
	$00,$00, $00,$00, $00,$00, $00,$00,
	$00,$00, $00,$00, $00,$00, $00,$00,
	$00,$00, $00,$00, $00,$00, $00,$00,
	// G
	$00,$00, $00,$00, $00,$00, $00,$00,
	$3C,$00, $7E,$00, $7E,$00, $3C,$00,
	$3C,$00, $7E,$00, $7E,$00, $3C,$00,
	$00,$00, $00,$00, $00,$00, $00,$00
};

// === パーティクル: 赤小弾 ===
ARRAY BYTE BULLET_R[] = {
	// B (なし)
	$00,$00, $00,$00, $00,$00, $00,$00,
	$00,$00, $00,$00, $00,$00, $00,$00,
	$00,$00, $00,$00, $00,$00, $00,$00,
	$00,$00, $00,$00, $00,$00, $00,$00,
	// R
	$00,$00, $00,$00, $00,$00, $00,$00,
	$3C,$00, $7E,$00, $7E,$00, $3C,$00,
	$3C,$00, $7E,$00, $7E,$00, $3C,$00,
	$00,$00, $00,$00, $00,$00, $00,$00,
	// G (なし)
	$00,$00, $00,$00, $00,$00, $00,$00,
	$00,$00, $00,$00, $00,$00, $00,$00,
	$00,$00, $00,$00, $00,$00, $00,$00,
	$00,$00, $00,$00, $00,$00, $00,$00
};

// === パーティクル: 黄小弾 ===
ARRAY BYTE BULLET_Y[] = {
	// B (なし)
	$00,$00, $00,$00, $00,$00, $00,$00,
	$00,$00, $00,$00, $00,$00, $00,$00,
	$00,$00, $00,$00, $00,$00, $00,$00,
	$00,$00, $00,$00, $00,$00, $00,$00,
	// R
	$00,$00, $00,$00, $00,$00, $00,$00,
	$3C,$00, $7E,$00, $7E,$00, $3C,$00,
	$3C,$00, $7E,$00, $7E,$00, $3C,$00,
	$00,$00, $00,$00, $00,$00, $00,$00,
	// G
	$00,$00, $00,$00, $00,$00, $00,$00,
	$3C,$00, $7E,$00, $7E,$00, $3C,$00,
	$3C,$00, $7E,$00, $7E,$00, $3C,$00,
	$00,$00, $00,$00, $00,$00, $00,$00
};

CONST PRT_MAX = 7;	// スプライト 1-7 がパーティクル
VAR I;
VAR PX, PY;

// パーティクル管理 (BYTE 配列)
ARRAY BYTE PRT_X[PRT_MAX-1];
ARRAY BYTE PRT_Y[PRT_MAX-1];
ARRAY BYTE PRT_VX[PRT_MAX-1];
ARRAY BYTE PRT_VY[PRT_MAX-1];
ARRAY BYTE PRT_LIFE[PRT_MAX-1];

VSYNC_PROC()
{
}

PARTICLE_INIT()
{
	FOR I=0 TO PRT_MAX-1
	{
		PRT_LIFE[I] = 0;
	}
}

PARTICLE_ADD(X, Y, VX, VY)
VAR NUM;
{
	// 空きスロット検索 (fadeout 完了も確認)
	FOR NUM=0 TO PRT_MAX-1
	{
		IF PRT_LIFE[NUM] == 0 AND SPR_IS_ACTIVE(NUM + 1) == 0
		{
			PRT_X[NUM] = X;
			PRT_Y[NUM] = Y;
			PRT_VX[NUM] = VX;
			PRT_VY[NUM] = VY;
			PRT_LIFE[NUM] = 20 + RND(20);

			// 色をランダム選択
			IF RND(3) == 0 THEN
				SPR_SET_PATTERN(NUM + 1, BULLET_R);
			ELIF RND(2) == 0 THEN
				SPR_SET_PATTERN(NUM + 1, BULLET_Y);
			ELSE
				SPR_SET_PATTERN(NUM + 1, BULLET);

			SPR_SET_POS(NUM + 1, X, Y);
			SPR_SET_SHOW(NUM + 1, 1);
			RETURN;
		}
	}
}

PARTICLE_UPDATE()
VAR NX, NY;
{
	FOR I=0 TO PRT_MAX-1
	{
		IF PRT_LIFE[I] != 0
		{
			PRT_LIFE[I]--;
			IF PRT_LIFE[I] == 0
			{
				SPR_SET_SHOW(I + 1, 0);
			}
			ELSE
			{
				// 移動先を計算 (BYTE 演算で符号保持)
				NX = (PRT_X[I] + PRT_VX[I]) AND $FF;
				NY = (PRT_Y[I] + PRT_VY[I]) AND $FF;
				IF NX > 76 OR NY > 46
				{
					PRT_LIFE[I] = 0;
					SPR_SET_SHOW(I + 1, 0);
				}
				ELSE
				{
					PRT_X[I] = NX;
					PRT_Y[I] = NY;
					SPR_SET_POS(I + 1, NX, NY);
				}
			}
		}
	}
}

MAIN()
VAR K, PCOOL;
{
	WIDTH(40);
	GRPSETUP();
	GRDISP(1);
	SPR_INIT();

	PX = 37;	// 画面中央付近
	PY = 23;

	SPR_SET_PATTERN(0, PLAYER);
	SPR_SET_POS(0, PX, PY);
	SPR_SET_SHOW(0, 1);

	PARTICLE_INIT();
	PCOOL = 0;

	LOOP
	{
		K = INKEY(0);

		// カーソルキー: プレイヤー移動
		IF K == 29 AND PX > 0 THEN PX--;
		ELIF K == 28 AND PX < 75 THEN PX++;
		IF K == 30 AND PY > 0 THEN PY--;
		ELIF K == 31 AND PY < 46 THEN PY++;

		SPR_SET_POS(0, PX, PY);

		// P: パーティクル発射 (4方向 + 斜め)
		IF K == 'P' AND PCOOL == 0 THEN
		{
			PARTICLE_ADD(PX, PY, 1, 0);
			PARTICLE_ADD(PX, PY, -1, 0);
			PARTICLE_ADD(PX, PY, 0, 1);
			PARTICLE_ADD(PX, PY, 0, -1);
			PCOOL = 5;
		}

		// SPACE: ランダム方向パーティクル 1 発
		IF K == ' ' THEN
		{
			PARTICLE_ADD(PX, PY,
				-1 + RND(3),
				-1 + RND(3)
			);
		}

		IF PCOOL > 0 THEN PCOOL--;
		PARTICLE_UPDATE();

		VSYNC(2);
		SPR_UPDATE();
	}
}
