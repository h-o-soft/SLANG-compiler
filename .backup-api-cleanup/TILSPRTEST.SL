///
/// TILSPRTEST.SL — TILELIB + SPRLIB 統合デモ
///
/// PCG タイル背景 (8dot スクロール + アニメ) + スプライト (プレイヤー + パーティクル)
///

#include "../tile/TILELIB.SL"
#include "../spr/SPRLIB.SL"

#ASM

; ============================================================
; PCG タイル (BRG インターリーブ)
; ============================================================
Tile0:
	REPT 8
	DB $00,$00,$00
	ENDM
Tile1:
	DB $00,$FF,$00, $00,$FF,$00, $00,$FE,$00, $00,$00,$00
	DB $00,$FF,$00, $00,$FF,$00, $00,$EF,$00, $00,$00,$00
Tile2:
	DB $00,$00,$55, $00,$00,$AA, $00,$00,$55, $00,$00,$AA
	DB $00,$00,$77, $00,$00,$DD, $00,$00,$77, $00,$00,$DD
Tile3:
	DB $11,$00,$00, $22,$00,$00, $44,$00,$00, $88,$00,$00
	DB $11,$00,$00, $22,$00,$00, $44,$00,$00, $88,$00,$00
Tile4:
	DB $FF,$FF,$FF, $FF,$FF,$FF, $FF,$FF,$FF, $FF,$FF,$FF
	DB $FF,$FF,$FF, $FF,$FF,$FF, $FF,$FF,$FF, $FF,$FF,$FF
Tile5:
	DB $00,$FF,$FF, $00,$FF,$FF, $00,$FF,$FF, $00,$FF,$FF
	DB $00,$FF,$FF, $00,$FF,$FF, $00,$FF,$FF, $00,$FF,$FF
Tile6:
	DB $FF,$00,$FF, $FF,$00,$FF, $FF,$00,$FF, $FF,$00,$FF
	DB $FF,$00,$FF, $FF,$00,$FF, $FF,$00,$FF, $FF,$00,$FF
Tile7:
	DB $FF,$FF,$00, $FF,$FF,$00, $FF,$FF,$00, $FF,$FF,$00
	DB $FF,$FF,$00, $FF,$FF,$00, $FF,$FF,$00, $FF,$FF,$00

TileIdx:
	DB 1, 1, 1, 1
	DB 2, 2, 2, 2
	DB 3, 3, 3, 3
	DB 1, 2, 2, 1
	DB 3, 1, 1, 3
	DB 4, 4, 4, 4	; アニメ

TileMap:
	REPT 8
	DB 0, 1, 2, 1, 5, 3, 1, 2, 4, 1, 2, 1, 0, 3, 5, 2, 0, 1, 2, 1, 5, 3, 1, 2, 4, 1, 2, 1, 0, 3, 5, 2
	DB 1, 5, 1, 2, 3, 1, 4, 1, 1, 0, 1, 2, 3, 5, 0, 1, 1, 5, 1, 2, 3, 1, 4, 1, 1, 0, 1, 2, 3, 5, 0, 1
	DB 2, 1, 0, 3, 1, 2, 1, 5, 2, 1, 4, 3, 1, 2, 1, 0, 2, 1, 0, 3, 1, 2, 1, 5, 2, 1, 4, 3, 1, 2, 1, 0
	DB 3, 2, 5, 0, 1, 4, 2, 3, 3, 2, 1, 0, 5, 0, 2, 3, 3, 2, 5, 0, 1, 4, 2, 3, 3, 2, 1, 0, 5, 0, 2, 3
	ENDM

; ============================================================
; スプライト
; ============================================================
; 16x16 ダイヤモンド (7色ストライプ)
; 行 0,1: 色1 青 (B のみ)
; 行 2,3: 色2 赤 (R のみ)
; 行 4,5: 色3 紫→黒 (B+R, パレット変更で黒表示)
; 行 6,7: 色4 緑 (G のみ)
; 行 8,9: 色5 シアン (B+G)
; 行 10,11: 色6 黄 (R+G)
; 行 12-15: 色7 白 (B+R+G)
SprPlayer:
	; B プレーン
	DB $01,$80, $03,$C0		; r0,1 青
	DB $00,$00, $00,$00		; r2,3 赤 (Bなし)
	DB $1F,$F8, $3F,$FC		; r4,5 紫→黒
	DB $00,$00, $00,$00		; r6,7 緑 (Bなし)
	DB $FF,$FF, $7F,$FE		; r8,9 シアン
	DB $00,$00, $00,$00		; r10,11 黄 (Bなし)
	DB $0F,$F0, $07,$E0		; r12,13 白
	DB $03,$C0, $01,$80		; r14,15 白
	; R プレーン
	DB $00,$00, $00,$00		; r0,1 青 (Rなし)
	DB $07,$E0, $0F,$F0		; r2,3 赤
	DB $1F,$F8, $3F,$FC		; r4,5 紫→黒
	DB $00,$00, $00,$00		; r6,7 緑 (Rなし)
	DB $00,$00, $00,$00		; r8,9 シアン (Rなし)
	DB $3F,$FC, $1F,$F8		; r10,11 黄
	DB $0F,$F0, $07,$E0		; r12,13 白
	DB $03,$C0, $01,$80		; r14,15 白
	; G プレーン
	DB $00,$00, $00,$00		; r0,1 青 (Gなし)
	DB $00,$00, $00,$00		; r2,3 赤 (Gなし)
	DB $00,$00, $00,$00		; r4,5 紫→黒 (Gなし)
	DB $7F,$FE, $FF,$FF		; r6,7 緑
	DB $FF,$FF, $7F,$FE		; r8,9 シアン
	DB $3F,$FC, $1F,$F8		; r10,11 黄
	DB $0F,$F0, $07,$E0		; r12,13 白
	DB $03,$C0, $01,$80		; r14,15 白

SprBulletC:
	DB $00,$00, $00,$00, $00,$00, $00,$00
	DB $3C,$00, $7E,$00, $7E,$00, $3C,$00
	DB $3C,$00, $7E,$00, $7E,$00, $3C,$00
	DB $00,$00, $00,$00, $00,$00, $00,$00
	REPT 4
	DB $00,$00, $00,$00, $00,$00, $00,$00
	ENDM
	DB $00,$00, $00,$00, $00,$00, $00,$00
	DB $3C,$00, $7E,$00, $7E,$00, $3C,$00
	DB $3C,$00, $7E,$00, $7E,$00, $3C,$00
	DB $00,$00, $00,$00, $00,$00, $00,$00

SprBulletR:
	REPT 4
	DB $00,$00, $00,$00, $00,$00, $00,$00
	ENDM
	DB $00,$00, $00,$00, $00,$00, $00,$00
	DB $3C,$00, $7E,$00, $7E,$00, $3C,$00
	DB $3C,$00, $7E,$00, $7E,$00, $3C,$00
	DB $00,$00, $00,$00, $00,$00, $00,$00
	REPT 4
	DB $00,$00, $00,$00, $00,$00, $00,$00
	ENDM

SprBulletY:
	REPT 4
	DB $00,$00, $00,$00, $00,$00, $00,$00
	ENDM
	DB $00,$00, $00,$00, $00,$00, $00,$00
	DB $3C,$00, $7E,$00, $7E,$00, $3C,$00
	DB $3C,$00, $7E,$00, $7E,$00, $3C,$00
	DB $00,$00, $00,$00, $00,$00, $00,$00
	DB $00,$00, $00,$00, $00,$00, $00,$00
	DB $3C,$00, $7E,$00, $7E,$00, $3C,$00
	DB $3C,$00, $7E,$00, $7E,$00, $3C,$00
	DB $00,$00, $00,$00, $00,$00, $00,$00

#END

CONST C_TILE0 = Tile0;
CONST C_TILE1 = Tile1;
CONST C_TILE2 = Tile2;
CONST C_TILE3 = Tile3;
CONST C_TILE4 = Tile4;
CONST C_TILE5 = Tile5;
CONST C_TILE6 = Tile6;
CONST C_TILE7 = Tile7;
CONST C_TILE_IDX = TileIdx;
CONST C_TILE_MAP = TileMap;
CONST C_PLAYER   = SprPlayer;
CONST C_BULLET_C = SprBulletC;
CONST C_BULLET_R = SprBulletR;
CONST C_BULLET_Y = SprBulletY;

// CONST SCREEN_W = 40;
// CONST SCREEN_H = 25;
CONST SCREEN_OFS_X = 1;
CONST SCREEN_OFS_Y = 1;
CONST SCREEN_W = 26;
CONST SCREEN_H = 22;
CONST MAP_CW = 64;
CONST MAP_CH = 64;
CONST SCROLL_MARGIN = 8;
CONST PRT_MAX = 7;

VAR I;
VAR PX, PY, SCX, SCY;

ARRAY BYTE PRT_X[PRT_MAX-1];
ARRAY BYTE PRT_Y[PRT_MAX-1];
ARRAY BYTE PRT_VX[PRT_MAX-1];
ARRAY BYTE PRT_VY[PRT_MAX-1];
ARRAY BYTE PRT_LIFE[PRT_MAX-1];

#ASM
_TILE_ANIMCNT:	DB 0
#END

VSYNC_PROC()
{
	#ASM
	ld hl,_TILE_ANIMCNT
	inc (hl)
	ld a,(hl)
	and $07
	jr nz,.no_anim
	ld hl,_TILE_ANIMIDX
	inc (hl)
	ld a,(hl)
	and 3
	ld (hl),a
.no_anim:
	#END
}

PARTICLE_INIT()
{
	FOR I=0 TO PRT_MAX-1
		PRT_LIFE[I] = 0;
}

PARTICLE_ADD(X, Y, VX, VY)
VAR NUM;
{
	FOR NUM=0 TO PRT_MAX-1
	{
		IF PRT_LIFE[NUM] == 0 AND SPR_IS_ACTIVE(NUM + 1) == 0
		{
			PRT_X[NUM] = X;
			PRT_Y[NUM] = Y;
			PRT_VX[NUM] = VX;
			PRT_VY[NUM] = VY;
			PRT_LIFE[NUM] = 20 + RND(20);
			IF RND(3) == 0 THEN
				SPR_SET_PATTERN(NUM + 1, C_BULLET_R);
			ELIF RND(2) == 0 THEN
				SPR_SET_PATTERN(NUM + 1, C_BULLET_Y);
			ELSE
				SPR_SET_PATTERN(NUM + 1, C_BULLET_C);
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
			IF PRT_LIFE[I] == 0 THEN
				SPR_SET_SHOW(I + 1, 0);
			ELSE
			{
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
VAR K, SPX, SPY;
{
	WIDTH(40);
	SPR_INIT();
	GRPSETUP();
	GRDISP(1);

	// パレット変更: 色3 (紫) を黒にする
	// デフォルト: B=$AA R=$CC G=$F0 (各ビット n = 色 n の成分)
	// 色3 の B/R ビットをクリア: B=$A2, R=$C4 (G は元から0)
	#ASM
	ld bc,$1000
	ld a,$A2		; B パレット
	out (c),a
	ld bc,$1100
	ld a,$C4		; R パレット
	out (c),a
	ld bc,$1200
	ld a,$F0		; G パレット (変更なし, 念のため固定)
	out (c),a
	#END

	PCGDEF(0, C_TILE0);
	PCGDEF(1, C_TILE1);
	PCGDEF(2, C_TILE2);
	PCGDEF(3, C_TILE3);
	PCGDEF(4, C_TILE4);
	PCGDEF(5, C_TILE5);
	PCGDEF(6, C_TILE6);
	PCGDEF(7, C_TILE7);

	TILE_INIT();
	TILE_SET_MAP(C_TILE_MAP, C_TILE_IDX, 0);
	TILE_SET_MAPSIZE(32, 32);
	TILE_SET_ANIM(4);
	TILE_SET_OFS(SCREEN_OFS_X, SCREEN_OFS_Y);
	TILE_SET_SIZE(SCREEN_W, SCREEN_H)

	PX = 20;
	PY = 20;
	SCX = 0;
	SCY = 0;

	SPR_SET_PATTERN(0, C_PLAYER);
	SPR_SET_SHOW(0, 1);
	PARTICLE_INIT();

	// 初回: 両ページに描画
	TILE_SET_PAGE(0);
	TILE_FILL_MAP();
	TILE_SET_PAGE(4);
	TILE_FILL_MAP();

	LOOP
	{
		K = INKEY(0);

		IF K == 'A' AND PX > SCREEN_OFS_X THEN PX-=2;
		ELIF K == 'D' AND PX < MAP_CW * 2 - 4 THEN PX+=2;
		IF K == 'W' AND PY > SCREEN_OFS_Y THEN PY-=2;
		ELIF K == 'S' AND PY < MAP_CH * 2 - 4 THEN PY+=2;
		//IF K == 'A' AND PX > SCREEN_OFS_X THEN PX--;
		//ELIF K == 'D' AND PX < MAP_CW * 2 - 4 THEN PX++;
		//IF K == 'W' AND PY > SCREEN_OFS_Y THEN PY--;
		//ELIF K == 'S' AND PY < MAP_CH * 2 - 4 THEN PY++;

		IF PX/2 > SCX + SCREEN_W - SCROLL_MARGIN THEN SCX = PX/2 - (SCREEN_W - SCROLL_MARGIN);
		IF PX/2 < SCX + SCROLL_MARGIN AND SCX > 0 THEN SCX = PX/2 - SCROLL_MARGIN;
		IF PY/2 > SCY + SCREEN_H - SCROLL_MARGIN THEN SCY = PY/2 - (SCREEN_H - SCROLL_MARGIN);
		IF PY/2 < SCY + SCROLL_MARGIN AND SCY > 0 THEN SCY = PY/2 - SCROLL_MARGIN;
		IF SCX >= $8000 THEN SCX = 0;
		IF SCY >= $8000 THEN SCY = 0;
		IF SCX > MAP_CW - SCREEN_W THEN SCX = MAP_CW - SCREEN_W;
		IF SCY > MAP_CH - SCREEN_H THEN SCY = MAP_CH - SCREEN_H;

		SPX = PX - SCX * 2;
		SPY = PY - SCY * 2;
		SPR_SET_POS(0, SPX, SPY);

		IF K == 'P' THEN
		{
			PARTICLE_ADD(SPX, SPY, 1, 0);
			PARTICLE_ADD(SPX, SPY, -1, 0);
			PARTICLE_ADD(SPX, SPY, 0, 1);
			PARTICLE_ADD(SPX, SPY, 0, -1);
		}
		IF K == ' ' THEN
			PARTICLE_ADD(SPX, SPY, -1 + RND(3), -1 + RND(3));

		PARTICLE_UPDATE();

		// 両ページにタイル描画 (FILL×2 ≈ 6ms, COPY_PAGE の半分)
		TILE_SET_SCROLL(SCX, SCY);
		TILE_SET_PAGE(0);
		TILE_FILL_MAP();
		TILE_SET_PAGE(4);
		TILE_FILL_MAP();

		VSYNC(2);
		SPR_UPDATE();
	}
}
