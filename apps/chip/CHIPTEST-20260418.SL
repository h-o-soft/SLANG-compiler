///
/// CHIPTEST.SL - DrawVVRAM 統合テスト (X1 320x200, 8x8 チップ)
///

MACHINE Init_VVRAM(0):InitVVRAM;
MACHINE Draw_VVRAM(0):DrawVVRAM;
MACHINE GRP_OFS(2):GrpSetOfs;
MACHINE GRP_SIZE(2):GrpSetSize;

#ASM

; --- テスト用チップ画像データ (MAPGRPADR に相当) ---
; チップ番号 0: 黒 (全 0)
; チップ番号 1: 白 (全 FF)
; チップ番号 2: チェッカー
; チップ番号 3: 縦縞
; 各チップ CHIP_SIZE = 24 bytes (B8+R8+G8)

TestMapGrp:
; chip 0: 黒
	DS 24
; chip 1: 白
	DB $FF,$FF,$FF,$FF,$FF,$FF,$FF,$FF
	DB $FF,$FF,$FF,$FF,$FF,$FF,$FF,$FF
	DB $FF,$FF,$FF,$FF,$FF,$FF,$FF,$FF
; chip 2: チェッカー (B=市松, R=逆市松, G=全ON → シアン/黄交互)
	DB $AA,$55,$AA,$55,$AA,$55,$AA,$55
	DB $55,$AA,$55,$AA,$55,$AA,$55,$AA
	DB $FF,$FF,$FF,$FF,$FF,$FF,$FF,$FF
; chip 3: 縦縞 (R=$F0, G=$0F → 赤/緑)
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

; 定数 (CHIPLIB.SL 側の定数を再定義)
VVRAMWIDTH	EQU	32
VVRAMHEIGHT	EQU	22
CHIP_SIZE	EQU	24
MASK_CHIP_SIZE	EQU	32
MERGE_UNIT	EQU	32

; MAPGRPADR をテスト用データに設定
MAPGRPADR	EQU	TestMapGrp

#END

MAIN()
{
	WIDTH(40);
	GRPSETUP();
	GRDISP(1);
	GRCLS();

	// 初期化
	Init_VVRAM();
	GRP_OFS(0, 0);
	GRP_SIZE(20, 10);

	// VVRAM (RAM) にチップ番号を直接書き込む
	#ASM
	; VVRAM_PAGE0 にパターンを書く (secondary: primary=0 の逆)
	; DrawVVRAM は primary=0 → secondary=PAGE1 に読みに行く
	; なので PAGE1 に書く
	ld hl,VVRAM_PAGE1

	; 1行目: 白/チェッカー/縦縞/赤/緑/青 の繰り返し
	ld b,20
	ld c,1		; チップ番号 1 から開始
.fill_row1:
	ld (hl),c
	inc hl
	inc c
	ld a,c
	cp 7		; チップ 7 以上になったら 1 に戻す
	jr c,.no_wrap1
	ld c,1
.no_wrap1:
	djnz .fill_row1

	; 残り VVRAMWIDTH-20 をスキップ
	ld de,VVRAMWIDTH-20
	add hl,de

	; 2行目以降も同様に繰り返す (残り 9行)
	ld a,9
.fill_rows:
	push af
	ld b,20
	ld c,1
.fill_row_n:
	ld (hl),c
	inc hl
	inc c
	ld a,c
	cp 7
	jr c,.no_wrap_n
	ld c,1
.no_wrap_n:
	djnz .fill_row_n
	ld de,VVRAMWIDTH-20
	add hl,de
	pop af
	dec a
	jr nz,.fill_rows
	#END

	// DrawVVRAM で描画
	Draw_VVRAM();

	PRINT("DrawVVRAM TEST",/);
	INKEY(2);
}
