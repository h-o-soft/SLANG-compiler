///
/// CHIPTEST.SL — CHIPLIB サンプル (差分描画でスムーズスクロール)
///
/// Ys 風のトップビュー RPG イメージ: マップの大半が GROUND (緑の地面) で、
/// 上部数行が SKY (空)、その上にランダムな WALL 矩形が散らばっている。
/// 大半が同じスーパーチップなので、スクロール時の差分は「新しく現れた
/// 端列のうち WALL/WATER がかかった数チップだけ」となり、CHIPLIB の差分
/// 転送機構が活かせて軽快に動く。
///
/// 操作: カーソルキー  = プレイヤー移動 (= 自動スクロール)
///       SPACE = 通常マップ再生成 (Ys 風, 差分機構が活きて軽快)
///       X     = ワーストケースマップ (全セル異なる SC, 差分最大で重い)
///       P     = パーティクル発射
///

#include "CHIPLIB.LIB"

CONST SCREEN_W = 40;
CONST SCREEN_H = 25;
CONST MAP_SCW = 32;	// マップ幅 (スーパーチップ単位)
CONST MAP_SCH = 32;	// マップ高
CONST MAP_CW = MAP_SCW * 2;
CONST MAP_CH = MAP_SCH * 2;
CONST SCROLL_MARGIN = 8;

// 描画負荷の調整値: 値を増やすほど画面が WALL/WATER で埋まり差分が増える
// → スクロールが重くなる。実機での見栄え/速度バランスを見て調整。
CONST SKY_ROWS = 4;	// マップ上部の SKY 行数
CONST N_WALLS = 25;	// ランダム壁矩形の数
CONST WALL_MAX_SIZE = 5;	// 壁矩形の最大幅/高さ (スーパーチップ単位)
CONST N_WATER = 8;	// 水パッチの数 (アニメチップ)

VAR I;
VAR PX, PY, SCX, SCY;

CONST PARTICLE_MAX = 10;
ARRAY BYTE PRT_X[PARTICLE_MAX-1];
ARRAY BYTE PRT_Y[PARTICLE_MAX-1];
ARRAY BYTE PRT_VX[PARTICLE_MAX-1];
ARRAY BYTE PRT_VY[PARTICLE_MAX-1];
ARRAY BYTE PRT_PTN[PARTICLE_MAX-1];
ARRAY BYTE PRT_CNT[PARTICLE_MAX-1];

// ============================================================
// プレイヤースプライト (2x2 ダイヤ型, 黄色 = R+G, マスク+BRG)
// ============================================================
ARRAY BYTE SPRITE[] = {
	// chip 0 (TL): mask, B, R, G
	$FF,$FF,$FF,$E0,$C0,$80,$00,$00,
	$00,$00,$00,$00,$00,$00,$00,$00,
	$00,$00,$00,$1F,$3F,$7F,$FF,$FF,
	$00,$00,$00,$1F,$3F,$7F,$FF,$FF,
	// chip 1 (TR)
	$FF,$FF,$FF,$07,$03,$01,$00,$00,
	$00,$00,$00,$00,$00,$00,$00,$00,
	$00,$00,$00,$F8,$FC,$FE,$FF,$FF,
	$00,$00,$00,$F8,$FC,$FE,$FF,$FF,
	// chip 2 (BL)
	$00,$00,$80,$C0,$E0,$FF,$FF,$FF,
	$00,$00,$00,$00,$00,$00,$00,$00,
	$FF,$FF,$7F,$3F,$1F,$00,$00,$00,
	$FF,$FF,$7F,$3F,$1F,$00,$00,$00,
	// chip 3 (BR)
	$00,$00,$01,$03,$07,$FF,$FF,$FF,
	$00,$00,$00,$00,$00,$00,$00,$00,
	$FF,$FF,$FE,$FC,$F8,$00,$00,$00,
	$FF,$FF,$FE,$FC,$F8,$00,$00,$00
};

// ============================================================
// スーパーチップ table (TL, TR, BL, BR)
//   SC 0: SKY        (4× chip 0)  — 上部数行
//   SC 1: GROUND     (4× chip 1)  — マップの大半
//   SC 2: WALL RED   (4× chip 2)
//   SC 3: WALL STONE (4× chip 3)
//   SC 4: WATER      (4× chip 4 = 4-7 アニメ)
// ============================================================
ARRAY BYTE MAP_IDX[] = {
	0, 0, 0, 0,
	1, 1, 1, 1,
	2, 2, 2, 2,
	3, 3, 3, 3,
	4, 4, 4, 4
};

// ============================================================
// チップ画像 (各 24 バイト = B[8] + R[8] + G[8] planar)
//   chip 0: SKY        — 全面 cyan (B+G)
//   chip 1: GROUND     — green ベース + 草模様 (G プレーン)
//   chip 2: WALL RED   — R プレーンに brick 模様 (赤壁)
//   chip 3: WALL STONE — 全プレーンに stone 模様 (白っぽい石壁)
//   chip 4: WATER 0    ┐
//   chip 5: WATER 1    │ B プレーンの波模様 (4 フレームで循環)
//   chip 6: WATER 2    │
//   chip 7: WATER 3    ┘
// ============================================================
ARRAY BYTE MAP_GRP[] = {
	// chip 0: SKY (cyan = B+G 全面)
	$FF,$FF,$FF,$FF,$FF,$FF,$FF,$FF,
	$00,$00,$00,$00,$00,$00,$00,$00,
	$FF,$FF,$FF,$FF,$FF,$FF,$FF,$FF,

	// chip 1: GROUND (緑地面 + 草の点)
	$00,$00,$00,$00,$00,$00,$00,$00,
	$00,$00,$00,$00,$00,$00,$00,$00,
	$FF,$FF,$EF,$FF,$FF,$FF,$FB,$FF,

	// chip 2: WALL RED (赤レンガ)
	$00,$00,$00,$00,$00,$00,$00,$00,
	$FE,$80,$80,$FE,$EF,$08,$08,$EF,
	$00,$00,$00,$00,$00,$00,$00,$00,

	// chip 3: WALL STONE (白い石壁, 全プレーン同パターン)
	$66,$99,$99,$66,$66,$99,$99,$66,
	$66,$99,$99,$66,$66,$99,$99,$66,
	$66,$99,$99,$66,$66,$99,$99,$66,

	// chip 4: WATER frame 0 (青い波)
	$00,$00,$AA,$55,$00,$00,$AA,$55,
	$00,$00,$00,$00,$00,$00,$00,$00,
	$00,$00,$00,$00,$00,$00,$00,$00,

	// chip 5: WATER frame 1
	$00,$00,$55,$AA,$00,$00,$55,$AA,
	$00,$00,$00,$00,$00,$00,$00,$00,
	$00,$00,$00,$00,$00,$00,$00,$00,

	// chip 6: WATER frame 2
	$AA,$55,$00,$00,$AA,$55,$00,$00,
	$00,$00,$00,$00,$00,$00,$00,$00,
	$00,$00,$00,$00,$00,$00,$00,$00,

	// chip 7: WATER frame 3
	$55,$AA,$00,$00,$55,$AA,$00,$00,
	$00,$00,$00,$00,$00,$00,$00,$00,
	$00,$00,$00,$00,$00,$00,$00,$00
};

// ============================================================
// マップ本体 (スーパーチップ番号配列, 32x32 = 1024 bytes)
// 中身は GENERATE_MAP() で起動時に手続き生成
// ============================================================
ARRAY BYTE MAP_CHIP[MAP_SCW * MAP_SCH - 1];

VSYNC_PROC()
{
	CHIP_ANIM_TICK();
}

// ============================================================
// パーティクル管理
// ============================================================
PARTICLE_INIT()
{
	FOR I=0 TO PARTICLE_MAX-1
		PRT_PTN[I]=0;
}

PARTICLE_ADD(SX, SY, VX, VY)
VAR NUM;
{
	FOR NUM=0 TO PARTICLE_MAX-1
	{
		IF PRT_PTN[NUM] == 0
		{
			PRT_X[NUM] = SX;
			PRT_Y[NUM] = SY;
			PRT_VX[NUM] = VX;
			PRT_VY[NUM] = VY;
			PRT_PTN[NUM] = 1;
			PRT_CNT[NUM] = 20 + RND(20);
			RETURN;
		}
	}
}

PARTICLE_PROC()
{
	FOR I=0 TO PARTICLE_MAX-1
	{
		IF PRT_PTN[I] != 0
		{
			PRT_X[I] = (PRT_X[I] + PRT_VX[I]) AND $FF;
			PRT_Y[I] = (PRT_Y[I] + PRT_VY[I]) AND $FF;
			PRT_CNT[I]--;
			IF PRT_CNT[I] == 0 THEN PRT_PTN[I] = 0;
		}
	}
}

PARTICLE_DRAW()
{
	FOR I=0 TO PARTICLE_MAX-1
	{
		IF PRT_PTN[I] != 0
		{
			// スプライト座標はワールド座標 (CHIPLIB が内部で
			// _CHIP_MAPX/Y と引き合わせて画面外クリップする)
			CHIP_SPR_POS(PRT_X[I], PRT_Y[I]);
			CHIP_SPR_ADD(SPRITE, 1, 1);
		}
	}
}

// ============================================================
// マップ手続き生成 (Ys 風トップビュー)
//   1. 全マップを SC 1 (GROUND, 緑地面) で塗りつぶし
//   2. 上部 SKY_ROWS 行を SC 0 (SKY) に置き換え
//   3. ランダム位置にランダムサイズの壁矩形を N_WALLS 個 (重なり OK)
//      赤レンガ (SC 2) と石壁 (SC 3) を半々で混在
//   4. 水たまり (SC 4 = アニメ) を N_WATER 個ランダム配置
//
// 大半が GROUND/SKY の同じ SC のままで、スクロール時の差分は
// 壁や水がかかった数チップ程度に収まる。
// ============================================================
GENERATE_MAP()
VAR I, X, Y, W, H, RX, RY, SC;
{
	// 1. GROUND で全埋め
	FOR I = 0 TO MAP_SCW * MAP_SCH - 1
		MAP_CHIP[I] = 1;

	// 2. 上部数行を SKY に
	FOR Y = 0 TO SKY_ROWS - 1
		FOR X = 0 TO MAP_SCW - 1
			MAP_CHIP[Y * MAP_SCW + X] = 0;

	// 3. ランダム壁矩形 (重なり OK)
	FOR I = 0 TO N_WALLS - 1
	{
		RX = RND(MAP_SCW - WALL_MAX_SIZE);
		RY = RND(MAP_SCH - WALL_MAX_SIZE);
		W = RND(WALL_MAX_SIZE - 1) + 2;
		H = RND(WALL_MAX_SIZE - 1) + 2;
		SC = 2 + RND(2);	// SC 2 (赤レンガ) or SC 3 (石壁)
		FOR Y = RY TO RY + H - 1
			FOR X = RX TO RX + W - 1
				MAP_CHIP[Y * MAP_SCW + X] = SC;
	}

	// 4. 水たまり (1 マスずつ)
	FOR I = 0 TO N_WATER - 1
	{
		RX = RND(MAP_SCW);
		RY = RND(MAP_SCH);
		MAP_CHIP[RY * MAP_SCW + RX] = 4;
	}
}

// ============================================================
// ワーストケース生成: 全セルを隣と異なる SC で埋める
//   斜めパターンで 5 種の SC をローテーション。スクロール 1 列ぶん動くと
//   全セルが「前と違う SC」になるため差分転送が最大化 = 描画コスト最大。
//   差分機構の限界が見える。
// ============================================================
GENERATE_WORST_MAP()
VAR I, X, Y;
{
	FOR Y = 0 TO MAP_SCH - 1
		FOR X = 0 TO MAP_SCW - 1
			MAP_CHIP[Y * MAP_SCW + X] = (X + Y) MOD 5;
}

MAIN()
VAR K;
{
	WIDTH(40);
	GRPSETUP();
	GRDISP(1);
	GRCLS();

	CHIP_INIT();
	CHIP_SET_OFS(0, 0);
	CHIP_SET_SIZE(SCREEN_W, SCREEN_H);

	CHIP_SET_MAP(MAP_CHIP, MAP_IDX, MAP_GRP);
	CHIP_SET_MAPSIZE(MAP_SCW, MAP_SCH);
	CHIP_SET_ANIM(4);	// チップ 4 以上がアニメ対象 (4→5→6→7 で循環)
	CHIP_SET_ANIM_RATE(8, 4);

	GENERATE_MAP();
	PARTICLE_INIT();

	PX = MAP_CW / 2;
	PY = MAP_CH / 2;

	// 初回スクロール位置をプレイヤーに合わせて中央に配置 (range clamp 込み)
	SCX = PX - SCREEN_W / 2;
	SCY = PY - SCREEN_H / 2;
	IF SCX >= $8000 THEN SCX = 0;
	IF SCY >= $8000 THEN SCY = 0;
	IF SCX > MAP_CW - SCREEN_W THEN SCX = MAP_CW - SCREEN_W;
	IF SCY > MAP_CH - SCREEN_H THEN SCY = MAP_CH - SCREEN_H;

	// 初回描画
	CHIP_SET_SCROLL(SCX, SCY);
	CHIP_FILL_MAP();
	CHIP_DRAW_VVRAM();

	LOOP
	{
		K = INKEY(0);

		// カーソルキー: プレイヤー移動 (チップ単位)
		IF K == 29 AND PX > 0 THEN PX--;
		ELIF K == 28 AND PX < MAP_CW - 1 THEN PX++;
		IF K == 30 AND PY > 0 THEN PY--;
		ELIF K == 31 AND PY < MAP_CH - 1 THEN PY++;

		// スクロール追従
		IF PX > SCX + SCREEN_W - SCROLL_MARGIN THEN SCX = PX - (SCREEN_W - SCROLL_MARGIN);
		IF PX < SCX + SCROLL_MARGIN AND SCX > 0 THEN SCX = PX - SCROLL_MARGIN;
		IF PY > SCY + SCREEN_H - SCROLL_MARGIN THEN SCY = PY - (SCREEN_H - SCROLL_MARGIN);
		IF PY < SCY + SCROLL_MARGIN AND SCY > 0 THEN SCY = PY - SCROLL_MARGIN;

		// スクロール範囲制限
		IF SCX >= $8000 THEN SCX = 0;
		IF SCY >= $8000 THEN SCY = 0;
		IF SCX > MAP_CW - SCREEN_W THEN SCX = MAP_CW - SCREEN_W;
		IF SCY > MAP_CH - SCREEN_H THEN SCY = MAP_CH - SCREEN_H;

		// SPACE: 通常マップ再生成 (Ys 風, 差分高速)
		IF K == ' ' THEN GENERATE_MAP();
		// X: ワーストケースマップ (全セル別 SC, 差分機構の限界デモ)
		IF K == 'X' THEN GENERATE_WORST_MAP();

		// P: パーティクル発射
		IF K == 'P' THEN
			PARTICLE_ADD(PX, PY, -1 + RND(2)*2, -1 + RND(2)*2);

		PARTICLE_PROC();

		CHIP_SET_SCROLL(SCX, SCY);
		CHIP_FILL_MAP();

		CHIP_SPR_START();
		CHIP_SPR_POS(PX, PY);	// ワールド座標 (CHIPLIB がスクロール変換する)
		CHIP_SPR_ADD(SPRITE, 2, 2);
		PARTICLE_DRAW();

		CHIP_DRAW_VVRAM();
		VSYNC(2);
	}
}
