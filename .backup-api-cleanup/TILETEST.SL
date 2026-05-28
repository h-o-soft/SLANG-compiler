///
/// TILETEST.SL — TILELIB サンプル (PCG タイルマップ)
///

#include "TILELIB.SL"

#ASM

; ============================================================
; サンプル用 PCG パターン (8x8 × 24 bytes: B8+R8+G8)
; CHIPLIB と同じフォーマット
; ============================================================
; PCG データは BRG インターリーブ (各ラインごとに B,R,G の順)
SampleTile0:	; 黒
	DS 24
SampleTile1:	; レンガ風 (赤)
	DB $00,$FF,$00	; line 0: B,R,G
	DB $00,$FF,$00	; line 1
	DB $00,$FE,$00	; line 2
	DB $00,$00,$00	; line 3
	DB $00,$FF,$00	; line 4
	DB $00,$FF,$00	; line 5
	DB $00,$EF,$00	; line 6
	DB $00,$00,$00	; line 7
SampleTile2:	; 草 (緑)
	DB $00,$00,$55	; line 0
	DB $00,$00,$AA	; line 1
	DB $00,$00,$55	; line 2
	DB $00,$00,$AA	; line 3
	DB $00,$00,$77	; line 4
	DB $00,$00,$DD	; line 5
	DB $00,$00,$77	; line 6
	DB $00,$00,$DD	; line 7
SampleTile3:	; 水 (青)
	DB $11,$00,$00	; line 0
	DB $22,$00,$00	; line 1
	DB $44,$00,$00	; line 2
	DB $88,$00,$00	; line 3
	DB $11,$00,$00	; line 4
	DB $22,$00,$00	; line 5
	DB $44,$00,$00	; line 6
	DB $88,$00,$00	; line 7
SampleTile4:	; 白
	DB $FF,$FF,$FF	; line 0
	DB $FF,$FF,$FF	; line 1
	DB $FF,$FF,$FF	; line 2
	DB $FF,$FF,$FF	; line 3
	DB $FF,$FF,$FF	; line 4
	DB $FF,$FF,$FF	; line 5
	DB $FF,$FF,$FF	; line 6
	DB $FF,$FF,$FF	; line 7

; アニメ用 PCG 4-7 (赤→緑→青→白で循環)
SampleTile5:	; PCG 5: 緑ベタ (アニメフレーム1)
	DB $00,$00,$FF, $00,$00,$FF, $00,$00,$FF, $00,$00,$FF
	DB $00,$00,$FF, $00,$00,$FF, $00,$00,$FF, $00,$00,$FF
SampleTile6:	; PCG 6: 青ベタ (アニメフレーム2)
	DB $FF,$00,$00, $FF,$00,$00, $FF,$00,$00, $FF,$00,$00
	DB $FF,$00,$00, $FF,$00,$00, $FF,$00,$00, $FF,$00,$00
SampleTile7:	; PCG 7: 白ベタ (アニメフレーム3)
	DB $FF,$FF,$FF, $FF,$FF,$FF, $FF,$FF,$FF, $FF,$FF,$FF
	DB $FF,$FF,$FF, $FF,$FF,$FF, $FF,$FF,$FF, $FF,$FF,$FF

; スーパーチップ定義 (2x2、4 bytes each)
; 検証用: SC 0 = タイル 0-3, SC 1 = タイル 4-7 を順番に並べる
SampleTileIdx:
	DB 0, 1, 2, 3	; 0: TL=0, TR=1, BL=2, BR=3
	DB 4, 5, 6, 7	; 1: TL=4, TR=5, BL=6, BR=7
	DB 3, 3, 3, 3	; 2: 水
	DB 4, 4, 4, 4	; 3: 白
	DB 1, 2, 2, 1	; 4: レンガ/草
	DB 4, 4, 4, 4	; 5: アニメ

; 圧縮マップ (32x32 スーパーチップ)
; 左上に SC 0, SC 1 を並べる (タイル 0-7 が表示されるはず)
SampleTileMap:
	DB 0, 1, 0, 1, 0, 1, 0, 1, 0, 1, 0, 1, 0, 1, 0, 1, 0, 1, 0, 1, 0, 1, 0, 1, 0, 1, 0, 1, 0, 1, 0, 1
	REPT 31
	DB 2, 2, 2, 2, 2, 2, 2, 2, 2, 2, 2, 2, 2, 2, 2, 2, 2, 2, 2, 2, 2, 2, 2, 2, 2, 2, 2, 2, 2, 2, 2, 2
	ENDM
#END

CONST C_TILE_MAP = SampleTileMap;
CONST C_TILE_IDX = SampleTileIdx;
CONST C_TILE0 = SampleTile0;
CONST C_TILE1 = SampleTile1;
CONST C_TILE2 = SampleTile2;
CONST C_TILE3 = SampleTile3;
CONST C_TILE4 = SampleTile4;
CONST C_TILE5 = SampleTile5;
CONST C_TILE6 = SampleTile6;
CONST C_TILE7 = SampleTile7;

VAR PX, PY, SCX, SCY;

#ASM
_TILE_ANIMCNT:	DB 0	; アニメフレームカウンタ
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

MAIN()
VAR K;
{
	WIDTH(40);

	// PCG 定義 (1 文字/VSync)
	PCGDEF(0, C_TILE0);
	PCGDEF(1, C_TILE1);
	PCGDEF(2, C_TILE2);
	PCGDEF(3, C_TILE3);
	PCGDEF(4, C_TILE4);
	PCGDEF(5, C_TILE5);
	PCGDEF(6, C_TILE6);
	PCGDEF(7, C_TILE7);

	// タイルマップ初期化
	TILE_INIT();
	TILE_SET_MAP(C_TILE_MAP, C_TILE_IDX, 0);
	TILE_SET_MAPSIZE(32, 32);
	TILE_SET_ANIM(255);	// 検証用: アニメ無効。PCG コードをそのまま確認する

	// 初回描画
	TILE_FILL_MAP();

	PX = 10;
	PY = 10;
	SCX = 0;
	SCY = 0;

	// WASD でスクロール (8 ドット = 1 文字単位)
	LOOP
	{
		K = INKEY(0);
		IF K == 'A' AND PX > 0 THEN PX--;
		ELIF K == 'D' AND PX < 63 THEN PX++;
		IF K == 'W' AND PY > 0 THEN PY--;
		ELIF K == 'S' AND PY < 63 THEN PY++;

		// スクロール追従
		IF PX > SCX + 40 - 8 THEN SCX = PX - (40 - 8);
		IF PX < SCX + 8 AND SCX > 0 THEN SCX = PX - 8;
		IF PY > SCY + 25 - 8 THEN SCY = PY - (25 - 8);
		IF PY < SCY + 8 AND SCY > 0 THEN SCY = PY - 8;
		IF SCX >= $8000 THEN SCX = 0;
		IF SCY >= $8000 THEN SCY = 0;
		IF SCX > 24 THEN SCX = 24;	// 64 - 40
		IF SCY > 40 THEN SCY = 40;	// 64 - 24

		TILE_SET_SCROLL(SCX, SCY);
		TILE_FILL_MAP();
		VSYNC(1);
	}
}
