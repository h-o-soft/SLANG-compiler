///
/// CHIPTEST.SL — CHIPLIB サンプル (外部バイナリ不要)
///

#include "CHIPLIB.SL"

#ASM

; ============================================================
; サンプル用チップ画像 (8x8, CHIP_SIZE=24 bytes: B8+R8+G8)
; ============================================================
SampleMapGrp:
; chip 0: 黒
	DS 24
; chip 1: 白
	DB $FF,$FF,$FF,$FF,$FF,$FF,$FF,$FF
	DB $FF,$FF,$FF,$FF,$FF,$FF,$FF,$FF
	DB $FF,$FF,$FF,$FF,$FF,$FF,$FF,$FF
; chip 2: チェッカー (シアン/黄)
	DB $AA,$55,$AA,$55,$AA,$55,$AA,$55
	DB $55,$AA,$55,$AA,$55,$AA,$55,$AA
	DB $FF,$FF,$FF,$FF,$FF,$FF,$FF,$FF
; chip 3: 縦縞 (赤/緑)
	DB $00,$00,$00,$00,$00,$00,$00,$00
	DB $F0,$F0,$F0,$F0,$F0,$F0,$F0,$F0
	DB $0F,$0F,$0F,$0F,$0F,$0F,$0F,$0F
; chip 4: 赤
	DB $00,$00,$00,$00,$00,$00,$00,$00
	DB $FF,$FF,$FF,$FF,$FF,$FF,$FF,$FF
	DB $00,$00,$00,$00,$00,$00,$00,$00
; chip 5: 緑
	DB $00,$00,$00,$00,$00,$00,$00,$00
	DB $00,$00,$00,$00,$00,$00,$00,$00
	DB $FF,$FF,$FF,$FF,$FF,$FF,$FF,$FF
; chip 6: 青
	DB $FF,$FF,$FF,$FF,$FF,$FF,$FF,$FF
	DB $00,$00,$00,$00,$00,$00,$00,$00
	DB $00,$00,$00,$00,$00,$00,$00,$00
; chip 7: シアン
	DB $FF,$FF,$FF,$FF,$FF,$FF,$FF,$FF
	DB $00,$00,$00,$00,$00,$00,$00,$00
	DB $FF,$FF,$FF,$FF,$FF,$FF,$FF,$FF
; chip 8-11: アニメ用 (8=赤, 9=緑, 10=青, 11=シアン → 4パターン循環)
; chip 8: 赤 (=chip4)
	DB $00,$00,$00,$00,$00,$00,$00,$00
	DB $FF,$FF,$FF,$FF,$FF,$FF,$FF,$FF
	DB $00,$00,$00,$00,$00,$00,$00,$00
; chip 9: 緑 (=chip5)
	DB $00,$00,$00,$00,$00,$00,$00,$00
	DB $00,$00,$00,$00,$00,$00,$00,$00
	DB $FF,$FF,$FF,$FF,$FF,$FF,$FF,$FF
; chip 10: 青 (=chip6)
	DB $FF,$FF,$FF,$FF,$FF,$FF,$FF,$FF
	DB $00,$00,$00,$00,$00,$00,$00,$00
	DB $00,$00,$00,$00,$00,$00,$00,$00
; chip 11: シアン (=chip7)
	DB $FF,$FF,$FF,$FF,$FF,$FF,$FF,$FF
	DB $00,$00,$00,$00,$00,$00,$00,$00
	DB $FF,$FF,$FF,$FF,$FF,$FF,$FF,$FF

; ============================================================
; サンプル用マップインデックス (2x2 スーパーチップ)
; 各 4 bytes: 左上, 右上, 左下, 右下
; ============================================================
SampleMapIdx:
	DB 1, 4, 1, 4	; 0: 白/赤
	DB 2, 5, 2, 5	; 1: チェッカー/緑
	DB 4, 6, 4, 6	; 2: 赤/青
	DB 3, 1, 3, 1	; 3: 縦縞/白
	DB 5, 2, 5, 2	; 4: 緑/チェッカー
	DB 6, 3, 6, 3	; 5: 青/縦縞
	DB 5, 5, 5, 5	; 6: 緑ベタ (差分描画デモ用)
	DB 4, 4, 4, 4	; 7: 赤ベタ
	DB 8, 8, 8, 8	; 8: アニメチップ (チップ8-11が循環 = 赤→緑→青→シアン)
	DB 6, 6, 6, 6	; 8: 青ベタ

; ============================================================
; サンプル用圧縮マップ (32x32 スーパーチップ)
; ============================================================
SampleMapChip:
	REPT 8
	DB 8, 1, 2, 3, 8, 5, 0, 1, 8, 3, 4, 5, 8, 1, 2, 3, 8, 5, 0, 1, 8, 3, 4, 5, 8, 1, 2, 3, 8, 5, 0, 1
	DB 1, 8, 3, 4, 5, 8, 1, 2, 3, 8, 5, 0, 1, 8, 3, 4, 5, 8, 1, 2, 3, 8, 5, 0, 1, 8, 3, 4, 5, 8, 1, 2
	DB 2, 3, 8, 5, 0, 1, 2, 3, 4, 5, 0, 1, 2, 3, 4, 5, 0, 1, 2, 3, 4, 5, 0, 1, 2, 3, 4, 5, 0, 1, 2, 3
	DB 3, 4, 5, 8, 1, 2, 3, 4, 5, 0, 1, 2, 3, 4, 5, 0, 1, 2, 3, 4, 5, 0, 1, 2, 3, 4, 5, 0, 1, 2, 3, 4
	ENDM

; ============================================================
; サンプル用スプライト (2x2 ダイヤ型シアン)
; ============================================================
SampleSprite:
	DB $FF,$FF,$FF,$E0,$C0,$80,$00,$00
	DB $00,$00,$00,$1F,$3F,$7F,$FF,$FF
	DB $00,$00,$00,$00,$00,$00,$00,$00
	DB $00,$00,$00,$1F,$3F,$7F,$FF,$FF
	DB $FF,$FF,$FF,$07,$03,$01,$00,$00
	DB $00,$00,$00,$F8,$FC,$FE,$FF,$FF
	DB $00,$00,$00,$00,$00,$00,$00,$00
	DB $00,$00,$00,$F8,$FC,$FE,$FF,$FF
	DB $00,$00,$80,$C0,$E0,$FF,$FF,$FF
	DB $FF,$FF,$7F,$3F,$1F,$00,$00,$00
	DB $00,$00,$00,$00,$00,$00,$00,$00
	DB $FF,$FF,$7F,$3F,$1F,$00,$00,$00
	DB $00,$00,$01,$03,$07,$FF,$FF,$FF
	DB $FF,$FF,$FE,$FC,$F8,$00,$00,$00
	DB $00,$00,$00,$00,$00,$00,$00,$00
	DB $FF,$FF,$FE,$FC,$F8,$00,$00,$00

; 合成バッファ (256 バイトアライン)
	ALIGN 256
SampleMergeBuf:
	DS 4096

#END

CONST C_SPRITE    = SampleSprite;
CONST C_MAP_CHIP  = SampleMapChip;
CONST C_MAP_IDX   = SampleMapIdx;
CONST C_MAP_GRP   = SampleMapGrp;
CONST C_MERGEBUF  = SampleMergeBuf;

CONST SCREEN_W = 40;
CONST SCREEN_H = 25;
CONST MAP_CW = 64;	// マップ幅 (チップ単位 = MAPSIZE_W * 2)
CONST MAP_CH = 64;	// マップ高
CONST SCROLL_MARGIN = 8;

VAR I;
VAR PX, PY, SCX, SCY;
VAR BYTE MAP_PTR[];

CONST PARTICLE_MAX = 10;
ARRAY BYTE PRT_X[PARTICLE_MAX-1];
ARRAY BYTE PRT_Y[PARTICLE_MAX-1];
ARRAY BYTE PRT_VX[PARTICLE_MAX-1];
ARRAY BYTE PRT_VY[PARTICLE_MAX-1];
ARRAY BYTE PRT_PTN[PARTICLE_MAX-1];
ARRAY BYTE PRT_CNT[PARTICLE_MAX-1];


#ASM
_CHIP_ANIMCNT:	DB 0	; アニメフレームカウンタ (CHIPLIB 外ワーク)
#END

VSYNC_PROC()
{
	// アニメカウンタ更新 (8 フレームごとにパターン切替)
	#ASM
	ld hl,_CHIP_ANIMCNT
	inc (hl)
	ld a,(hl)
	and $07
	jr nz,.no_anim
	ld hl,_CHIP_ANIMIDX
	inc (hl)
	ld a,(hl)
	and 3
	ld (hl),a
.no_anim:
	#END
}

PARTICLE_INIT()
{
	FOR I=0 TO PARTICLE_MAX-1
	{
		PRT_PTN[I]=0;
	}
}

PARTICLE_FETCH()
{
	FOR I=0 TO PARTICLE_MAX-1
	{
		IF PRT_PTN[I]==0 {
			RETURN I;
		}
	}
	RETURN $FFFF;
}

PARTICLE_ADD(X,Y,VX,VY)
VAR NUM;
{
	NUM = PARTICLE_FETCH();
	IF NUM & $8000 THEN RETURN;

	PRT_X[NUM] = X;
	PRT_Y[NUM] = Y;
	PRT_VX[NUM] = VX;
	PRT_VY[NUM] = VY;
	PRT_PTN[NUM] = 1;	// 仮
	PRT_CNT[NUM] = 10;
}

PARTICLE_PROC()
{
	FOR I=0 TO PARTICLE_MAX-1
	{
		IF PRT_PTN[I]!=0 {
			IF PRT_CNT[I]==0 {
				PRT_PTN[I] = 0;
			}
			PRT_X[I]+=PRT_VX[I];
			PRT_Y[I]+=PRT_VY[I];
			PRT_CNT[I]--;
		}
	}
}

PARTICLE_DRAW()
{
	FOR I=0 TO PARTICLE_MAX-1
	{
		IF PRT_PTN[I]!=0 {
			CHIP_SPR_POS(PRT_X[I], PRT_Y[I]);
			CHIP_SPR_ADD(C_SPRITE, 2, 2);
		}
	}
}

MAIN()
VAR I, K;
{
	WIDTH(40);
	GRPSETUP();
	GRDISP(1);
	GRCLS();

	CHIP_INIT();
	CHIP_SET_VVRAMSIZE(42, 28);
	CHIP_SET_OFS(0, 0);
	CHIP_SET_SIZE(SCREEN_W, SCREEN_H);

	CHIP_SET_MAP(C_MAP_CHIP, C_MAP_IDX, C_MAP_GRP);
	CHIP_SET_BUFFER(C_MERGEBUF);
	CHIP_SET_MAPSIZE(32, 32);
	CHIP_SET_ANIM(8);	// チップ 8 以上がアニメ対象 (8→9→10→11 で循環)

	MAP_PTR = C_MAP_CHIP;

	PX = 10;
	PY = 10;
	SCX = 0;
	SCY = 0;

	// 初回描画
	CHIP_FILL_MAP();
	CHIP_DRAW_VVRAM();

	LOOP
	{
		K = INKEY(0);

		// WASD: プレイヤー移動 (マップ範囲内に制限)
		IF K == 'A' AND PX > 0 THEN PX--;
		ELIF K == 'D' AND PX < MAP_CW - 1 THEN PX++;
		IF K == 'W' AND PY > 0 THEN PY--;
		ELIF K == 'S' AND PY < MAP_CH - 1 THEN PY++;

		// スクロール追従 (画面端 MARGIN チップに近づいたらスクロール)
		IF PX > SCX + SCREEN_W - SCROLL_MARGIN THEN SCX = PX - (SCREEN_W - SCROLL_MARGIN);
		IF PX < SCX + SCROLL_MARGIN AND SCX > 0 THEN SCX = PX - SCROLL_MARGIN;
		IF PY > SCY + SCREEN_H - SCROLL_MARGIN THEN SCY = PY - (SCREEN_H - SCROLL_MARGIN);
		IF PY < SCY + SCROLL_MARGIN AND SCY > 0 THEN SCY = PY - SCROLL_MARGIN;

		// スクロール範囲制限
		IF SCX >= $8000 THEN SCX = 0;
		IF SCY >= $8000 THEN SCY = 0;
		IF SCX > MAP_CW - SCREEN_W THEN SCX = MAP_CW - SCREEN_W;
		IF SCY > MAP_CH - SCREEN_H THEN SCY = MAP_CH - SCREEN_H;

		// SPACE: マップを緑ベタで埋める (差分描画デモ)
		IF K == ' ' THEN
		{
			FOR I = 0 TO 32*32-1
				MAP_PTR[I] = 6;
		}

		// R: ランダムに 10 箇所書き換え
		IF K == 'R' THEN
		{
			FOR I = 0 TO 9
				MAP_PTR[RND(32*32)] = RND(9);	// 0-8 (8=アニメ含む)
		}

		// 1: 斜めパターンに復帰
		IF K == '1' THEN
		{
			FOR I = 0 TO 32*32-1
				MAP_PTR[I] = (I + I/32) MOD 6;
		}

		IF K=='P' THEN
		{
			PARTICLE_ADD(
				PX, PY,
				-1 + RND(2)*2,
				-1 + RND(2)*2
			);
		}

		PARTICLE_PROC();

		CHIP_SET_SCROLL(SCX, SCY);
		CHIP_FILL_MAP();

		CHIP_SPR_START();
		CHIP_SPR_POS(PX, PY);
		CHIP_SPR_ADD(C_SPRITE, 2, 2);
		PARTICLE_DRAW()

		CHIP_DRAW_VVRAM();
		VSYNC(2);
	}
}
