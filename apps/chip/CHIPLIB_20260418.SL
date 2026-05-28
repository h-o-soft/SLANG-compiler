/// 
/// CHIPLIB.SL
/// 

VAR MAPX:MAPX;
VAR MAPY:MAPY;

MACHINE SPR_START(0): SpriteStart;
MACHINE SPR_POS(2): SpritePos;
MACHINE SPR_ADD(3): SpriteAdd;
MACHINE SPR_FLASH(1): SetSpriteFlash;

MACHINE CHIP_DRAW(3): ChipDraw;
MACHINE CHIP_MASK_DRAW(3): ChipMaskDraw;
MACHINE CHIP_SET_MAP(3): SetMapData;

MACHINE Init_VVRAM(0):InitVVRAM;
MACHINE Draw_VVRAM(0):DrawVVRAM;
MACHINE Fill_Map(0):FillMap;

MACHINE GRP_OFS(2):GrpSetOfs;
MACHINE GRP_SIZE(2):GrpSetSize;
MACHINE GRP_ANIM(1):GrpSetAnim;

#ASM
; --- X1 版定数定義 ---
; バッファ確保用の最大値 (画面40x25 + スクロールマージン。MAPHEIGHT*2 以上必要)
VVRAMWIDTH_MAX	EQU	42
VVRAMHEIGHT_MAX	EQU	28
; 初期値 (CHIP_SET_VVRAMSIZE で動的変更可)
VVRAMWIDTH	EQU	42
VVRAMHEIGHT	EQU	27
MAPWIDTH	EQU	21	; 全画面: (40+1)/2+1
MAPHEIGHT	EQU	14	; 全画面: (25+1)/2+1
DRAWX		EQU	0
DRAWY		EQU	0
DRAWWCNT	EQU	40	; 全画面 320/8
DRAWHCNT	EQU	25	; 全画面 200/8

; テスト用: MAPGRPADR をテストデータに設定 (本番は外部から指定)
MAPGRPADR	EQU	TestMapGrp
MAPIDXADR	EQU	$8700
MAPCHIPADR	EQU	$6900
MERGEBUFFER	EQU	$B000

ANIMIDX:	DB	0

; テスト用チップ画像データ (CHIP_SIZE=24 bytes × 7 chips)
TestMapGrp:
include "map0_8x8.grp",b

;; chip 0: 黒
;	DS 24
;; chip 1: 白
;	DB $FF,$FF,$FF,$FF,$FF,$FF,$FF,$FF
;	DB $FF,$FF,$FF,$FF,$FF,$FF,$FF,$FF
;	DB $FF,$FF,$FF,$FF,$FF,$FF,$FF,$FF
;; chip 2: チェッカー (シアン/黄)
;	DB $AA,$55,$AA,$55,$AA,$55,$AA,$55
;	DB $55,$AA,$55,$AA,$55,$AA,$55,$AA
;	DB $FF,$FF,$FF,$FF,$FF,$FF,$FF,$FF
;; chip 3: 縦縞 (赤/緑)
;	DB $00,$00,$00,$00,$00,$00,$00,$00
;	DB $F0,$F0,$F0,$F0,$F0,$F0,$F0,$F0
;	DB $0F,$0F,$0F,$0F,$0F,$0F,$0F,$0F
;; chip 4: 赤
;	DB $00,$00,$00,$00,$00,$00,$00,$00
;	DB $FF,$FF,$FF,$FF,$FF,$FF,$FF,$FF
;	DB $00,$00,$00,$00,$00,$00,$00,$00
;; chip 5: 緑
;	DB $00,$00,$00,$00,$00,$00,$00,$00
;	DB $00,$00,$00,$00,$00,$00,$00,$00
;	DB $FF,$FF,$FF,$FF,$FF,$FF,$FF,$FF
;; chip 6: 青
;	DB $FF,$FF,$FF,$FF,$FF,$FF,$FF,$FF
;	DB $00,$00,$00,$00,$00,$00,$00,$00
;	DB $00,$00,$00,$00,$00,$00,$00,$00

;
;  InitVVRAM
;    仮想VRAMとその関連データの初期化
; 
InitVVRAM:
	; 仮想VRAM (RAM ダブルバッファ) をクリア
	ld hl,VVRAM_PAGE0
	ld bc,VVRAM_SIZE
	xor a
	call CLEARRAM
	ld hl,VVRAM_PAGE1
	ld bc,VVRAM_SIZE
	xor a
	call CLEARRAM
	; Primary をページ 0 にしておく
	xor a
	ld (PRIMARY_VVRAM),a
	ret

; RAM を A の値でクリア (HL=先頭, BC=サイズ)
CLEARRAM:
	ld (hl),a
	ld d,h
	ld e,l
	inc de
	dec bc
	ldir
	ret

; 仮想 VRAM / チップ定数
VVRAM_SIZE	EQU	VVRAMWIDTH_MAX * VVRAMHEIGHT_MAX	; 最大サイズでクリア
CHIP_SIZE	EQU	24	; 通常チップ: B8+R8+G8
MASK_CHIP_SIZE	EQU	32	; マスク付き: mask8+B8+R8+G8
MERGE_UNIT	EQU	32	; 合成バッファ 1 個

;;; ============================================================
;;; X1 版 Phase 0: 最小描画プリミティブ (320x200, 8x8 チップ)
;;; ============================================================

;
; GETVRAMADR_X1
;   X1 の非線形 VRAM I/O アドレスを計算する (320x200, WIDTH=40)
;   Y%8==0 前提で簡略化: (Y&7)<<11 の項は常に 0
;
;   入力: HL = X (ピクセル単位), DE = Y (ピクセル単位、8の倍数のみ)
;   出力: HL = B プレーン ($4000 ベース) の I/O アドレス
;   破壊: AF, BC, DE
;
;   計算: $4000 + X + Y * 40 (入力はチップ単位 = 8ドット単位)
;
GETVRAMADR_X1:
	push hl			; X を保存
	; Y * 40 = Y*8 + Y*32
	ex de,hl		; HL = Y
	ld d,h
	ld e,l			; DE = Y
	add hl,hl
	add hl,hl
	add hl,hl		; HL = Y*8
	ld b,h
	ld c,l			; BC = Y*8
	add hl,hl
	add hl,hl		; HL = Y*32
	add hl,bc		; HL = Y*40
	ex de,hl		; DE = Y*40
	pop hl			; HL = X
	; 合算
	add hl,de		; HL = X + Y*40
	ld de,$4000
	add hl,de		; HL = $4000 + X + Y*40
	ret

;
; SetMapData
;   マップデータのアドレスを設定 (自己書き換え)
;   HL = MAPCHIPADR, DE = MAPIDXADR, BC = MAPGRPADR
;
SetMapData:
	ld (FillMap.ref_mapchipadr+2),hl	; ld ix,imm の即値は +2
	ld (FillMap.ref_mapidxadr+1),de
	ld (VramDrawAddress.ref_mapgrpadr1+1),bc
	ld (SpriteAdd.ref_mapgrpadr2+1),bc
	ret

;
; ChipDraw
;   指定座標に 1 チップ (8x8) を描画
;   HL = X (チップ単位), DE = Y (チップ単位)
;   BC = チップ画像データアドレス (24 bytes: B8+R8+G8)
;
ChipDraw:
	push bc			; 画像データアドレスを保存
	call GETVRAMADR_X1	; HL = VRAM I/O アドレス
	call SetDrawAddress_X1	; 自己書き換え
	pop hl			; HL = 画像データ
	call ChipOneDraw_X1
	ret

;
; SetDrawAddress_X1
;   ChipOneDraw_X1 内の ld bc 即値を自己書き換えして描画先を設定
;
;   入力: HL = B プレーンの VRAM I/O アドレス (GETVRAMADR_X1 の戻り値)
;   破壊: なし
;
SetDrawAddress_X1:
	ld (ChipOneDraw_X1.bc_init+1),hl
	ret

;
; chipdraw_x1_inner
;   8x8 チップ 1 プレーンを VRAM に描画 (アンロール展開)
;
;   前提: BC = VRAM I/O アドレス、SP = 画像データ先頭
;   動作: POP で 2 行分 (2 bytes) を一括読み → OUT (C),A でライン描画
;         各行で B += $08 (X1 インターリーブの次ライン)
;         8 行完了時 B は +$40 されており、次プレーンの先頭に自動遷移
;   破壊: AF, HL, BC (B += $40)
;
chipdraw_x1_inner:
	ld (.stacksave+1),sp
	ld sp,hl		; SP = 画像データ
	; ---- line 0, 1 ----
	pop hl
	ld a,l
	out (c),a
	ld a,b
	add a,$08
	ld b,a
	ld a,h
	out (c),a
	ld a,b
	add a,$08
	ld b,a
	; ---- line 2, 3 ----
	pop hl
	ld a,l
	out (c),a
	ld a,b
	add a,$08
	ld b,a
	ld a,h
	out (c),a
	ld a,b
	add a,$08
	ld b,a
	; ---- line 4, 5 ----
	pop hl
	ld a,l
	out (c),a
	ld a,b
	add a,$08
	ld b,a
	ld a,h
	out (c),a
	ld a,b
	add a,$08
	ld b,a
	; ---- line 6, 7 ----
	pop hl
	ld a,l
	out (c),a
	ld a,b
	add a,$08
	ld b,a
	ld a,h
	out (c),a
	ld a,b
	add a,$08
	ld b,a		; 計 B += $08*8 = $40 → 次プレーンへ
	; SP 現在値 (= 次の画像データ位置) を HL に保存
	ld hl,0
	add hl,sp
.stacksave:
	ld sp,0		; SP 復帰
	ret

;
; ChipOneDraw_X1
;   8x8 チップを 3 プレーン (B/R/G) 描画する
;
;   前提: SetDrawAddress_X1 で描画先が設定済み
;   入力: HL = 画像データ先頭 (24 bytes: B[8] + R[8] + G[8])
;   動作: chipdraw_x1_inner を 3 回呼ぶ (内部で SP 保存/復帰)
;         chipdraw_x1_inner 内で B += $40 されるため、
;         B→R→G のプレーン切替はオーバーヘッドゼロ
;   破壊: AF, HL, BC
;
ChipOneDraw_X1:
	di
.bc_init:
	ld bc,$0000		; ← SetDrawAddress_X1 で自己書き換え
	call chipdraw_x1_inner	; B プレーン ($4000+offset)
	call chipdraw_x1_inner	; R プレーン (自動: B += $40 → $8000+offset)
	call chipdraw_x1_inner	; G プレーン (自動: B += $40 → $C000+offset)
	ei
	ret

;;; ============================================================
;;; ↓ここから下はPC-8801mkIISR前提 (移植中)
;;; ============================================================

;
; FillMap
;   マップデータを元に現在書き込み有効な仮想VRAMに転送
; 
FillMap:
	; 入力
	; (Work) MAPX  描画左上座標(16x8を1単位とした座標
	; (Work) MAPY  描画左上座標
	; (Work) PRIMARY_VVRAM 描画対象の仮想VRAM番号とMAPX、MAPYのオフセット位置(各1ビット)
	push iy

	; 引数なし
	; MAPX, MAPYの位置から描画する
	; MAPX, MAPYは横16ドット、縦8ドット単位の座標である(つまり描画チップ1つぶん)
	; 640ドット8画面ぶんで16ドット単位で動くので、0〜320。1動くと16ドット動く。
	; 192ドット8画面ぶんで8ドット単位で動くので0〜192。1動くと8ドット動く。
	; いずれにしてBYTEにはおさまらないのでWORDにしておく

	; MAPXが奇数の場合横は21パーツ描画する事になる(左端右端は半分のみ描画)
	; 同様にMAPYが奇数の場合は縦に13パーツ描画する事になる(上端下端は半分のみ描画)
	; MAPX,MAPYについては下記のイメージ(二進数)
	; %PPPP PPPP PPPP PPPC
	; Cが対象チップのオフセット位置(MAPIDXADRのオフセット。0か1)
	; PがMAPCHIPADRの位置
	; 
	

	; あれだ。描画については上記のPPPPが代わったら描画しなおし。
	; 描画についてはズラさずにそのまま素朴に描画
	; ただし右端 下端は1チップずつ余分に描画する
	; そして仮想VRAMから描画する時に、上記Cのオフセットを考慮して描画してやる
	; つまり横16、縦8ドット単位のスクロール時は再描画は行われず、オフセット位置の考慮のみがされる
	; なるほど頭いいな……

	call SETVRAM_INDIVIDUAL

	ld iy,MAPY
.ref_mapchipadr:
	ld ix,MAPCHIPADR

	; deにPrimary(描画先VVRAM)のアドレスを入れる (RAM ダブルバッファ)
	ld a,(PRIMARY_VVRAM)
	and 1
	jr nz,.usepage1
	ld de,VVRAM_PAGE0
	jr .pagesel_done
.usepage1:
	ld de,VVRAM_PAGE1
.pagesel_done:

	; IY = MAPY(IY+2がMAPX)
	; IX = MAPCHIPADR(圧縮マップ)
	; DE = 描画先VVRAMアドレス(Primary/Secondary考慮済)


	push de
	; backup sp
	ld (.tileMapSp+1),sp

	;Calculate Y-offset
	ld a,(IY+1)	; High MAPY
	srl a
	ld h,a
	ld a,(IY+0)	; Low MAPY
	rr a
	ld l,a
	ld e,(IY+4)	; MAPW。 可変サイズなので……
	ld d,0
	call MULHLDE	; 素朴に掛け算する
	ex de,hl
	add ix,de

	ld a,(IY+3)	; High MAPX
	srl a
	ld d,a
	ld a,(IY+2)	; Low MAPX
	rr a
	ld e,a
	add ix,de
	; ここでixに描画元のMAPCHIPADR先頭が入る(ここから最大20x12を拾って描画する)

	pop de
	push de
	; deには描画先仮想VRAM先頭アドレスが入っている


	exx
	; 縦のループ数
	ld c,MAPHEIGHT
	; ↑このへんのbとcはMAPX、MAPYの最下位ビットの状態によって変化するが、今のところは固定で描いてみる
.tileTransferY
	exx
	; 仮想VRAMアドレス先頭を保存しておく
	ld (.DeRestore+1),de
	exx
	; 圧縮マップの横のループ数
	; これはMAPXの最下位ビットが1の場合は18にして左右を半分ずつ描画する必要があるので注意(今はとりあえずズラさずに描く)
	ld b,MAPWIDTH
.tileTransferX
	exx

	ld a,(ix)	; 圧縮マップのタイル番号
	and $3f		; 上位2ビットは無視
	inc ix		; 右に移動

	; 4倍してやる(2x2でマップチップが4バイト構成なので)
	ld l,a
	ld h,0
	add hl,hl
	add hl,hl
	ld c,l
	ld b,h
	; ld c,a
	; xor a
	; sla c
	; rla
	; sla c
	; rla
	; ld b,a
.ref_mapidxadr:
	ld hl,MAPIDXADR
	add hl,bc

	; ここまでで
	; iy = マップ座標の先頭
	; ix = 描画元のMAPCHIPADR(これはしばらく使わない)
	; hl = マップチップ先頭(2x2の先頭)
	; de = 描画先仮想VRAM先頭
	; が、入っている。

	di
	ld sp,hl
	; マップチップから実VVRAMチップ番号を得るためにspに入れる(たかだか4バイトなのでsp使わないロジックでもいいかもしれない)
	
	ex de,hl
	; 入れ替えてこうなる
	; hl = 描画先仮想VRAM先頭
	; de = マップチップ先頭(2x2の先頭)

	; (X1: バンク切替不要、VVRAM は RAM 上)

	; bcには仮想VRAM改行幅を入れる
.ref_vvw_m1:
	ld bc,VVRAMWIDTH-1

	; 2x2を描画する(クリッピングを考慮していない)
	; VVRAMチップ番号を2つ得る
	pop de
	ld (hl),e
	inc hl
	ld (hl),d
	add hl,bc
	pop de
	ld (hl),e
	inc hl
	ld (hl),d
	xor a
	sbc hl,bc	; これで右に移動している

	; ここまでで2x2が描画出来ている

	ex de,hl
	; hl = (pop deで壊れているので使われない)
	; de = 仮想VRAM先頭

	exx
	; 事前にbに横幅入れておく(おおむね20)
	; cには縦幅入れておく(おおむね12)
	; ただしクリッピングする場合は2ずつ減り、描画先アドレスがズレるので注意(めんどい)
	dec b
	jr nz,.tileTransferX
	exx

	; ここでDE(描画先)を戻す
.DeRestore
	ld de,0

.ref_vvw_x2:
	ld hl,VVRAMWIDTH*2	; 横一行の2行ぶん足す
	add hl,de
	ex de,hl
	; これでdeに描画先VVRAMアドレスが入る

	; TODO 高速化
	ld a,(MAPW)
	ld c,a
	ld b,0
	; 本来は圧縮マップの横描画数なので-20あるいは-18になるはず(IXが進んだ数を引いてやって、次の行の先頭に移動させる)
	add ix,bc		;Move to the next row of the Supertilemap
	ld bc,-MAPWIDTH
	add ix,bc		;Move to the next row of the Supertilemap

	exx
	dec c
	jr nz,.tileTransferY
	exx

.tileMapSp
	ld	sp,0

	pop hl	; DEに入れておいた描画先VVRAMアドレス

	; これでマップデータから仮想VRAMに情報が書かれているはず(ホントか？)

	; (X1: メインメモリ復帰不要、VVRAM は RAM 上)

	pop IY
	ei
	ret


; マップチップ描画の左上座標を指定
; X1 版: X, Y ともにチップ単位 (8x8 = 1)
GrpSetOfs:
	; hl = x (チップ単位)
	; de = y (チップ単位)
	call GETVRAMADR_X1
	call SetDrawAddress_X1
	ld (VramDrawAddress+1),hl	; DrawVVRAM 裏レジスタ初期値も設定
	ret

; マップチップ描画の縦横の個数を指定 (X1 8x8 版)
GrpSetSize:
	; hl = 横個数 (8x8 チップ数)
	; de = 縦個数

	; VVRAM マージン = VVRAMWIDTH - 横個数
.ref_vvw:
	ld a,VVRAMWIDTH
	sub l
	ld (VramDrawAddress.vvrammargin+1),a

	; 縦サイズを入れておく
	ld a,e
	ld (VramDrawAddress.drawhcnt1+1),a

	; 横サイズを入れておく
	ld a,l
	ld (VramDrawAddress.drawwcnt0+1),a
	ld (VramDrawAddress.drawwcnt1+1),a
	ld (VramDrawAddress.drawwcnt2+1),a
	ld (VramDrawAddress.drawwcnt3+1),a

	; 行間 VRAM アドレスジャンプ = 40 - 横個数 (X1 WIDTH=40, 8x8=1byte/chip)
	ld a,40
	sub l
	ld l,a
	ld h,0
	ld (VramDrawAddress+4),hl

	ret

GrpSetAnim:
	; hl = anim parts start index
	ld a,l
	ld (VramDrawAddress.animstart+1),a
	ret
	

; DrawVVRAM
;  仮想VRAMのキャラクターコードを元に描画を行う
;  ダブルバッファのSWAPも実行する
;
DrawVVRAM:
	; 裏レジスタに実 VRAM I/O アドレスを入れておく (X1: B プレーンベース)
	exx
VramDrawAddress:
	ld hl,$4000		; ← GrpSetOfs で自己書き換え (GETVRAMADR_X1 の戻り値)
	ld de,40-DRAWWCNT	; ← GrpSetSize で自己書き換え (行間マージン)
.drawwcnt0
	ld c,DRAWWCNT		; ← GrpSetSize で自己書き換え
	exx

	; 横描画数
.drawwcnt1
	ld b,DRAWWCNT		; ← GrpSetSize
	; 縦描画数
.drawhcnt1
	ld c,DRAWHCNT		; ← GrpSetSize

	; VVRAM のオフセット計算 (X/Y のサブチップオフセット)
	ld a,(MAPY)
	and 1
	sla a
	sla a
	ld e,a
	ld a,(MAPX)
	and 1
	sla a
	or e

	; 新データ (FillMap/SpriteAdd が書いた側 = PRIMARY ページ)
	push af
	ld a,(PRIMARY_VVRAM)
	and 1
	jr nz,.draw_new_page1
	ld hl,VVRAM_PAGE0	; PRIMARY=0 → 新データ=PAGE0
	jr .draw_new_done
.draw_new_page1:
	ld hl,VVRAM_PAGE1	; PRIMARY=1 → 新データ=PAGE1
.draw_new_done:
	pop af
	call setvadrofs
	push hl

	; 旧データ (前回描画 = PRIMARY の逆ページ)
	; ★ A は PRIMARY_VVRAM を使う (bit 2-1 に前回のオフセット情報が入っている)
	ld a,(PRIMARY_VVRAM)
	push af			; PRIMARY_VVRAM を保存
	and 1			; bit 0 でページ選択
	jr z,.draw_old_page1
	ld hl,VVRAM_PAGE0	; PRIMARY=1 → 旧データ=PAGE0
	jr .draw_old_done
.draw_old_page1:
	ld hl,VVRAM_PAGE1	; PRIMARY=0 → 旧データ=PAGE1
.draw_old_done:
	pop af			; PRIMARY_VVRAM 復帰 (前回のオフセット)
	call setvadrofs
	pop de
	; DE = 新データ (PRIMARY), HL = 旧データ (前回)

.drawvvram_loop
	; (X1: バンク切替不要、VVRAM は RAM 上)

	ld a,(de)	; DE が secondary 側
	; 128以上の場合(合成チップの場合)は必ず描画する
.animstart
	cp 80
	jr nc,.combdraw
	; 128未満の場合は前の画面と比較する
	cp (hl)

	; 一致している場合は描画しない
	jr z,.nodraw
.mapdraw
	; マップ描画
	push de
	push hl
	; aにチップ番号があるので24倍してからMAPGRPADRを足す (8x8: CHIP_SIZE=24)
	ld l,a
	ld h,0
.mapanimdraw
	add hl,hl
	add hl,hl
	add hl,hl	; ×8
	ld e,l
	ld d,h
	add hl,hl
	add hl,de	; ×24
.ref_mapgrpadr1:
	ld de,MAPGRPADR
	add hl,de
	ex de,hl
	; deがチップアドレスになっている
	exx
	push hl
	exx
	pop hl
	jr .chipdraw
.animdraw
	; マップ描画だがチップアドレス下位2ビットを差し替えてやる
	push de
	push hl
	and $fc
	ld l,a
	ld a,(ANIMIDX)
	or l
	ld l,a
	ld h,0
	jr .mapanimdraw
.combdraw
	cp $80
	jr c,.animdraw

	; 合成チップ描画
	push de
	push hl

	ex de,hl
	; 合成バッファ位置を算出 (8x8: MERGE_UNIT=32)
	and $7f
	ld l,a
	ld h,0
	add hl,hl
	add hl,hl
	add hl,hl
	add hl,hl
	add hl,hl	; 32倍
	ld a,HIGH MERGEBUFFER
	add a,h
	ld h,a
	ex de,hl

	exx
	push hl
	exx
	pop hl
	;jr .chipdraw
.chipdraw
	; この時点で HL=実VRAM I/Oアドレス、DE=画像アドレス になっている
	push bc
	call SetDrawAddress_X1	; HL を ChipOneDraw_X1.bc_init に書き換え
	ex de,hl		; HL = 画像データ (ChipOneDraw_X1 は HL で受ける)
	call ChipOneDraw_X1

	pop bc
	pop hl
	pop de
.nodraw
	inc hl
	inc de

	; 裏レジスタ側で実VRAM I/Oアドレスを計算
	exx
	dec c
	; 1行ぶんは単に横移動
	jr	nz,.yoko2add
	; 右端に到達したら次の char row (+40) に進む
	add hl,de
.drawwcnt2
	ld c,DRAWWCNT		; ← GrpSetSize
.yoko2add
	; 横 8 ドットおきなので 1 バイト (8x8)
	inc hl
	exx

	djnz .drawvvram_loop

.drawwcnt3
	ld b,DRAWWCNT		; ← GrpSetSize

	; hl と de を VVRAMWIDTH - DRAWWCNT 足して次の行先頭に
	push bc
.vvrammargin
	ld bc,VVRAMWIDTH-DRAWWCNT	; ← GrpSetSize で自己書き換え
	add hl,bc
	ex de,hl
	add hl,bc
	ex de,hl
	pop bc

	dec c
	jp nz,.drawvvram_loop

	; 今回のオフセットを保存しておく
	ld a,(MAPY)
	and 1
	sla a
	sla a
	ld b,a
	ld a,(MAPX)
	and 1
	sla a
	or b
	ld b,a

	; 描画対象をSWAPしておく
	ld a,(PRIMARY_VVRAM)
	xor 1
	and 1
	; 前回オフセットを保存
	or b
	ld (PRIMARY_VVRAM),a

	ret

; MAPX, MAPYのオフセットを考慮して仮想VRAMの参照先頭位置をズラす
; X1版: HL は呼び出し元で VVRAM_PAGE0 or VVRAM_PAGE1 を設定済み
setvadrofs:
	push de
	bit 2,a
	jr z,.noyofs
	; オフセットされている場合は一行ぶん足す
.ref_vvw:
	ld de,VVRAMWIDTH
	add hl,de
.noyofs
	; 横位置オフセットの場合は2バイトズラして開始する
	bit 1,a
	jr  z,.offsetx1
	inc hl
.offsetx1
	pop de
	ret

; SETVRAM_INDIVIDUAL / SETVRAM_EXTENDED
;   X1 には独立/拡張アクセスモードの概念がないため no-op
SETVRAM_INDIVIDUAL:
SETVRAM_EXTENDED:
	ret

; (PC-88 版 SetDrawAddress / ChipOneDraw / chipdraw は削除)
; X1 版は Phase 0 で実装済み: SetDrawAddress_X1 / ChipOneDraw_X1 / chipdraw_x1_inner


; ChipMaskDraw
;   マスクデータつきの16x8画像を合成バッファに書き込む
;   点滅描画を行う場合はSetSpriteFlashで点滅状態設定をしてから呼ぶ事
ChipMaskDraw:
	; hl = source, de = dest
	; sourceの32バイト(8バイトマスク、24バイトRGB)をdeに描画 (X1 8x8版)
	; 先頭のマスクでマスクしてからRGBをorして格納する

	push hl
	ld bc,8		; mask 8 bytes (8x8)
	add hl,bc
	ld c,l
	ld b,h
	pop hl

	; hl  = mask address
	; bc  = color address
	; de = or描画対象dest

	; call後、hlは変化なく、bcとdeは16バイト進んでいる
	; なので連続して呼び出すとマスクされた上でRGBが正しく描画される
	; スタックいじるので割り込み禁止
.noflash
	push hl
FlashOrDraw1:
	call MaskDrawPlane
	; call MaskFlashPlane
	pop hl
	push hl
FlashOrDraw2:
	call MaskDrawPlane
	; call MaskFlashPlane
	pop hl
FlashOrDraw3:
	call MaskDrawPlane
	; call MaskFlashPlane
	ret

MaskDrawPlane:
	; 8x8版: 8 bytes/plane (4 POP × 2 bytes)
	; SP = mask, HL = color, DE = dest (合成バッファ、RAM)
	di
	ld (.stackadr+1),sp
	ld sp,hl
	ld l,c
	ld h,b

	; --- 4 POP (8 bytes) ---
	pop bc
	ld a,(de)
	and c
	or (hl)
	ld (de),a
	inc hl
	inc de
	ld a,(de)
	and b
	or (hl)
	ld (de),a
	pop bc
	inc hl
	inc de
	ld a,(de)
	and c
	or (hl)
	ld (de),a
	inc hl
	inc de
	ld a,(de)
	and b
	or (hl)
	ld (de),a
	pop bc
	inc hl
	inc de
	ld a,(de)
	and c
	or (hl)
	ld (de),a
	inc hl
	inc de
	ld a,(de)
	and b
	or (hl)
	ld (de),a
	pop bc
	inc hl
	inc de
	ld a,(de)
	and c
	or (hl)
	ld (de),a
	inc hl
	inc de
	ld a,(de)
	and b
	or (hl)
	ld (de),a
	inc hl
	inc de

	; hlはcolorの次の色になっている
	ld c,l
	ld b,h

	; hlを8バイト戻す(マスクの先頭) (8x8: mask=8 bytes)
	ld hl,-8
	add hl,sp
.stackadr
	ld sp,0
	ei
	ret

MaskFlashPlane:
	di
	push bc

	ld (.fstackadr+1),sp
	ld sp,hl

	; 8x8版: 4 回ループ (2 bytes × 4 = 8 bytes/plane)
	ld b,4
.flashloop
	pop hl
	ld a,h
	cpl
	ld h,a
	ld a,l
	cpl
	ld l,a
	ld a,(de)
	or l
	ld (de),a
	inc de
	ld a,(de)
	or h
	ld (de),a
	inc de

	djnz .flashloop
.fstackadr
	ld sp,0

	; deは維持する
	; hlはそのまま
	; bcは8バイト進める (8x8: 8 bytes/plane)
	pop hl

	ld bc,8
	add hl,bc
	ld c,l
	ld b,h

	ei
	ret


; SetSpriteFlash
; 次に ChipMaskDraw した時の点滅状態を設定する
; L :
;     bit0 = Rプレーンを点滅させるか
;     bit1 = Gプレーンを点滅させるか
;     bit2 = Bプレーンを点滅させるか
; 
; プレーンについては0だと点滅せず、7だと真っ白になる
SetSpriteFlash:
	ld a,l
	; bit 7,a
	; jr z,.noflash

	ld hl,MaskDrawPlane
	ld de,MaskFlashPlane

	srl a
	jr nc,.normaldraw1
	ex de,hl
.normaldraw1
	ld (FlashOrDraw1+1),hl

	ld hl,MaskDrawPlane
	ld de,MaskFlashPlane

	srl a
	jr nc,.normaldraw2
	ex de,hl
.normaldraw2
	ld (FlashOrDraw2+1),hl

	ld hl,MaskDrawPlane
	ld de,MaskFlashPlane

	srl a
	jr nc,.normaldraw3
	ex de,hl
.normaldraw3
	ld (FlashOrDraw3+1),hl
.noflash
	ret

; 合成パーツ番号を初期化する
SpriteStart:
	ld a,128
	ld (EXPARTSIDX),a
	ret

; スプライトの書き込み開始位置を設定(VVRAMの座標)
SpritePos:
	; hl = X
	; de = Y
	ld a,l
	ld (SPRX),a
	ld a,e
	ld (SPRY),a
	ret

;	ld a,(MAPX)
;	and 1
;	add a,l
;	ld (SPRX),a
;	ld a,(MAPY)
;	and 1
;	add a,e
;	ld (SPRY),a
;	ret

SpriteAdd:
	; hl = image address with mask
	; de = sw
	; bc = sh

	; 保存しておく
	ld a,e
	ld (SWBAK),a

	ld b,e
	; b = SW
	; c = SH
	push bc

	; 画像アドレスは保存しておく(後で使う)
	ld (SIMGADR),hl

	; ; VADRについては描画開始位置計算を後で行う
	; ld hl,0
	; ld (VADR),hl

	;-----------------------------

	; スプライトのYが縦方向上にハミ出しているか？
	ld a,(MAPY)
	and $fe
	ld b,a
	ld a,(SPRY)
	cp b
	jr nc,.sy_ge_MAPY	; はみ出していない
	; はみ出しているので描画が開始されるところまで下に下げていく

	; SW * 32を計算して入れてやる(マスクあり画像横ぶんスキップ: 8x8版は32bytes/chip)
	; deにはまだSWが入っているはず
	ex de,hl	; deをhlに移す
	add hl,hl
	add hl,hl
	add hl,hl
	add hl,hl
	add hl,hl	; 32倍
	ld (.sw_mul_64 + 1),hl

.yclip_loop
	; ADR = ADR + TMP1
	ld hl,(SIMGADR)
.sw_mul_64
	ld de,0
	add hl,de
	ld (SIMGADR),hl
	;; VADR = VADR + VVRAMWIDTH
	;ld hl,(VADR)
	;ld de,VVRAMWIDTH
	;add hl,de
	;ld (VADR),hl
	; SPRY++
	ld a,(SPRY)
	inc a
	ld (SPRY),a
	; SH--
	pop bc
	; pushしたものの下位がSH
	dec c
	jp z,.spradd_end2	; IF SH==0 GOTO .spradd_end
				; 描画するものがなければここで終了
	push bc			; SW, SHを保存しなおす
	; UNTIL (SY >= MAPY)
	; aにはSPRYが入ったままのはず
	; ld a,(SPRY)
	; このへん冗長なのでどうにかしたい
	ld c,a
	ld a,(MAPY)
	and $fe
	ld b,a
	ld a,c
	cp b
	jr c,.yclip_loop	; IF SY < MAPY goto .ycliploop
.sy_ge_MAPY

	; 描画可能な状況でVVRAMの先頭アドレスを計算してやる
	ld a,(MAPY)
	and $fe
	ld b,a
	ld a,(SPRY)
	sub b
	ld l,a
	ld h,0
.ref_vvw1:
	ld de,VVRAMWIDTH
	call MULHLDE	; HL = Y * VVRAMWIDTH
	ex de,hl

	; SPRXがMAPXより小さい場合は負値になるが計算上問題ないはず
	ld a,(SPRX)
	ld l,a
	ld h,0
	ld a,(MAPX)
	and $fe
	ld c,a
	ld b,0
	or a
	sbc hl,bc
	add hl,de		; 縦位置を足す
	; VVRAM先頭アドレス (RAM ダブルバッファ)
	push af
	ld a,(PRIMARY_VVRAM)
	and 1
	jr nz,.spr_usepage1
	ld de,VVRAM_PAGE0
	jr .spr_pagesel_done
.spr_usepage1:
	ld de,VVRAM_PAGE1
.spr_pagesel_done:
	pop af
	add hl,de
	; VVRAMの開始アドレスを保存しておく
	ld (VADR),hl

	;-----------------------------------------------
	; ループ内での計算は無駄なので事前計算しておく
	; が、単純な計算なのでその場でやってもいいのかも
	;-----------------------------------------------
	; MAPY_MAPH2 = MAPY + VVRAMHEIGHT
	ld a,(MAPY)
	and $fe
.ref_vvh:
	add a,VVRAMHEIGHT
; 	ld b, a
; 	ld a,(MAPH)
; 	add a,a
; 	cp b
; 	jr c,.inside_maph
; 	ld a, b
; .inside_maph
	ld (.mapy_maph2 + 1),a

	; MAPX_MAPW2 = MAPX + MAPWIDTH*2;
	ld a,(MAPX)
	and $fe
	add a,MAPWIDTH*2
; 	; MAPWIDTH*2より大きければMAPWにする
; 	ld b, a
; 	ld a,(MAPW)
; 	add a,a
; 	dec a
; 	cp b
; 	jr c,.inside_mapw
; 	; MAPWにおさまっているのでbに保存した右端の値を戻す
; 	ld a,b
; .inside_mapw
	ld (.mapx_mapw2 + 1),a

	; (X1: VVRAMバンク切替不要、RAM上)
	;-----------------------------------------------

.yloop
	; 画面下にハミ出したらもう描画する事はないので終了
	;IF (SY >= MAPY_MAPH2) GOTO .spradd_end
	ld a,(SPRY)
.mapy_maph2
	cp 0
	jp nc,.spradd_end
	jp z,.spradd_end

.xloop
;	if (SPRX < MAPX) OR (SPRX >= MAPX_MAPW2)
;	{
;		GOTO SPRADD_HSKIP;
;	}
	ld a,(MAPX)
	and $fe
	ld b,a
	ld a,(SPRX)
	cp b
	jr c,.spradd_hskip
.mapx_mapw2
	cp 0
	jr nc,.spradd_hskip
	jr z,.spradd_hskip

	; (X1: VVRAM バンク切替不要)
	; VC = MEM[VADR];
	ld hl,(VADR)
	ld a,(hl)
	cp $80
	jr nc,.extraparts
	; 通常チップ

	; マップチップ番号をbに入れておく
	ld b,a
	; 合成チップ番号を新規発行して書き込む
	; MEM[VADR] = EXPARTSIDX;
	; EXPARTSIDX++;
	ld a,(EXPARTSIDX)
	ld (hl),a
	inc a
	ld (EXPARTSIDX),a

	; // 合成バッファのアドレス
	; CADR = $A000 + EXPARTSIDX * 64;
	dec a
	and $7f
	call CALC_MERGEADR
	push hl
	ld a,HIGH MERGEBUFFER
	add a,h
	ld h,a
	; hlに合成バッファアドレス

	; // BG画像を合成バッファにコピー
	; MEMCPY(CADR, MAPGRPADR + VC * 48, 48);
	ex de,hl
	pop hl
	; 合成チップアドレスCADRを保存
	push de
	; MAPGRPADR + VC * 24を計算する (8x8: CHIP_SIZE=24)
	ld l,b
	ld h,0
	add hl,hl
	add hl,hl
	add hl,hl	; ×8
	ld c,l
	ld b,h
	add hl,hl
	add hl,bc	; ×24
.ref_mapgrpadr2:
	ld bc,MAPGRPADR
	add hl,bc

	ld bc,CHIP_SIZE
	ldir		; マップチップ画像を合成バッファにコピー
	pop de
	; deに合成チップアドレス(CADR)が入っている
	jr .execmerge
.extraparts
	; 合成チップ
	; CADR = $A000 + (VC AND $7F) * 64;
	and $7f
	call CALC_MERGEADR
	ld a,HIGH MERGEBUFFER
	add a,h
	ld h,a
	ex de,hl
.execmerge
	; (X1: メインメモリ復帰不要、MERGEBUFFER は RAM 上)
	; hl = source, de = dest
	ld hl,(SIMGADR)
	call ChipMaskDraw

.spradd_hskip
;	VADR++;
	ld hl,(VADR)
	inc hl
	ld (VADR),hl
;	ADR = ADR + MASK_CHIP_SIZE (32 for 8x8);
	ld hl,(SIMGADR)
	ld de,MASK_CHIP_SIZE
	add hl,de
	ld (SIMGADR),hl
;	SX++;
	ld a,(SPRX)
	inc a
	ld (SPRX),a
;	SW--;
	pop bc
	dec b
	push bc
	jr nz,.xloop
.spradd_vskip
	pop bc
;	SH--;
	dec c
	jr z,.spradd_end2
;	SW = SWBAK;
	ld a,(SWBAK)
	ld b,a
	push bc
	; SPRXを戻す(クリッピングに必要)
	ld a,(SPRX)
	sub a,b
	ld (SPRX),a
;	VADR = VADR + VVRAMWIDTH - SWBAK;
.ref_vvw2:
	ld a,VVRAMWIDTH
	sub a,b
	ld l,a
	ld h,0
	ex de,hl
	ld hl,(VADR)
	add hl,de
	ld (VADR),hl
;	SY++;
	ld a,(SPRY)
	inc a
	ld (SPRY),a
	jp .yloop

.spradd_end
	pop bc
.spradd_end2

	ret

CALC_MERGEADR:
	ld l,a
	ld h,0
	add hl,hl
	add hl,hl
	add hl,hl
	add hl,hl
	add hl,hl	; 32倍 (8x8: MERGE_UNIT=32)
	ret
#END

/// GAME WORK

#ASM


; 0 = 0 or 1(primary or secondary)
; 1 = prev x
; 2 = prev y
PRIMARY_VVRAM:
	DB	0

; 仮の点滅状態ワーク(最終的には削除する)
FLASHFLG:
	db 0
; 仮のアニメーションインデックスワーク(最終的には削除する)

SWBAK:	DS	1
SIMGADR:
	DS	2
VADR:	DS	2

SPRX:	ds	1
SPRY:	ds	1

EXPARTSIDX:
	db	128

#END

#ASM
	; for(int i = 0; i < enemy_count; i++)
	; {
	;     inGameData.dungeon_monster_ids[i] = get_monster(appear_type);
	; }
	; inGameData.dungeon_monster_count = enemy_count;

; 指定の値が範囲内におさまっているかチェックする
; input:
;	c = 調べる値
;	hl = 開始値のアドレス(hl+1が終了値のアドレス)
;	※開始値、終了値と調べる値が一致していてもおさまっている事とする( start <= a <= end)
; output:
;	cf = 立っているとおさまっている
;	hl = 終了値の次のアドレスになっている
; clobbers:
;	
RangeCheck:
    ld a, c
    cp (hl)                 ; start と比較 (A=c)
    jr c, .out_of_range     ; c < start なら範囲外
    inc hl                  ; HL = end のアドレス

    ld a, c
    cp (hl)                 ; end と比較 (A=c)
    jr c, .in_range         ; c < end なら範囲内
    jr z, .in_range         ; c == end なら範囲内

    ; c > end なので範囲外
.out_of_range2:
    inc hl                  ; HL = end+1
    and a                   ; CF=0 にする（Aは変わらない）
    ret

.out_of_range:
    inc hl                  ; HL = end
    jr .out_of_range2       ; HL を end+1 にして CF=0 で返す

.in_range:
    inc hl                  ; HL = end+1
    scf                     ; CF=1
    ret

; 画面内に入っているかどうか調べる
; input:
;	e = x
;	d = y
; output:
;	cf立ってる = 入っていない / cf寝てる = 入っている
IsScreenInside:
	; 二倍にしてMAPXとの単位をあわせる
	sla e
	sla d

	ld	hl, MAPY
	ld	a, d
	cp	(hl)
	jr	c, .screen_out		; MAPYより上なので画面外
	ld	a, (hl)			; MAPY
	add	a, DRAWHCNT		; MAPY + DRAWHCNT
	cp	d			; Y
	jr	c, .screen_out		; MAPY + DRAWHCNTより下なので画面外

	inc	hl
	inc	hl			; MAPX

	ld	a, e
	cp	(hl)
	jr	c, .screen_out		; MAPXより左なので画面外
	ld	a, (hl)			; MAPX
	add	a, DRAWWCNT		; MAPX + DRAWWCNT
	cp	e			; X
	jr	c, .screen_out		; MAPX + DRAWWCNTより右なので画面外
.screen_out
	ret

GETVRAMADR:
	; HL = X, DE = Y
	PUSH BC
	
	; HLについて8で割る(右シフト3)
	; AにはXを8で割った余りを入れてやる
	LD A,L
	AND %00000111
	
	; Xを8で割る
	SRL H      ; Hレジスタを右シフト。上位ビットからキャリーフラグへ
	RR L       ; キャリーフラグをEレジスタの上位ビットに、Eレジスタを右シフト
	
	SRL H      ; 2回目のシフト
	RR L
	
	SRL H      ; 3回目のシフト
	RR L
	; HL = X/8
	
	; 上位は空いてるのでAを保存
	LD H,A
	
	; _AにはVRAMアドレスの余りが入っている(10の場合は2、みたいな感じ。 %10000000 をその値で右シフトすると描画位置ビットが得られる)
	; ↓これをやっておくかは悩む
	; LD (_A),A
	
	; Y * 80 + X
	; 400バイトくらい空きがあればテーブルを使いたいが……もったいないね
	EX DE,HL
	ADD HL,HL
	ADD HL,HL
	ADD HL,HL
	ADD HL,HL
	; 16
	LD C,L
	LD B,H
	ADD HL,HL
	ADD HL,HL
	; 64 + 16
	ADD HL,BC
	; ここでYが80倍された値がHLに入っている
	
	; $C000とX/8を足してアドレスにしてやる
	LD A,$C0
	OR H
	LD H,A
	LD A,D
	LD D,0
	ADD HL,DE
	
	; HLにはVRAMのアドレスが入っている
	POP BC
	
	RET

#END



#END

// 共有系ワーク
#ASM

; マップの描画位置(横16ドット、縦8ドット単位の最小単位を1とする位置。この位置が画面の左上になるよう描画される。右端 下端がハミ出した場合の挙動は不定)
MAPY:   DW      0
MAPX:   DW      0

; マップの縦横サイズ(可変)
MAPW:   DB      50
MAPH:   DB      40

; --- 仮想 VRAM ダブルバッファ (RAM 上、最大値で確保) ---
VVRAM_PAGE0:	DS	VVRAMWIDTH_MAX * VVRAMHEIGHT_MAX
VVRAM_PAGE1:	DS	VVRAMWIDTH_MAX * VVRAMHEIGHT_MAX

#END

MACHINE CHIP_SET_MAPSIZE(2): SetMapSize;
MACHINE CHIP_SET_SCROLL(2): SetScroll;
MACHINE CHIP_SET_VVRAMSIZE(2): SetVVRAMSize;

CONST SPR_WHITE = TestSprite_White;
CONST VVPAGE0 = VVRAM_PAGE0;
CONST CHIP_IMG_BLUE = TestMapGrp + 6 * 24;
CONST MAP_CHIP = TestMapChip;
CONST MAP_IDX = TestMapIdx;
CONST MAP_GRP = TestMapGrp;


VAR PX,PY;
VAR DX,DY;
VSYNC_PROC() { }

// === テスト用 MAIN (本番では削除) ===
MAIN()
VAR I,K;
{
	WIDTH(40);
	GRPSETUP();
	GRDISP(1);
	GRCLS();

	Init_VVRAM();
	CHIP_SET_VVRAMSIZE(42, 28);
	// GRP_OFS(0, 0);   // チップ単位
	// GRP_SIZE(40, 25);
	GRP_OFS(1, 1);     // 1チップ分マージン (旧ピクセル値 8,8 に相当)
	GRP_SIZE(38, 23);

	// マップデータを設定
	CHIP_SET_MAP(MAP_CHIP, MAP_IDX, MAP_GRP);
	CHIP_SET_MAPSIZE(64, 64);

	// 初回: マップを描画
	Fill_Map();

	// デバッグ: VVRAM 行 0 と 行 1 のチップ番号をダンプ
	PRINT("Row0: ");
	FOR I = 0 TO 9
		PRINT(MEM[VVPAGE0 + I], " ");
	PRINT(/);
	PRINT("Row1: ");
	FOR I = 0 TO 9
		PRINT(MEM[VVPAGE0 + 42 + I], " ");
	PRINT(/);
	INKEY(2);

	Draw_VVRAM();

	PX = 5;
	PY = 3;

	// 全画面スクロールテスト (斜め移動 + スプライト)
	LOOP
	{
	//FOR I = 0 TO 80
	//{
		K = INKEY(0);
		IF K=='A' THEN PX--;
		ELIF K=='D' THEN PX++;
		IF K=='W' THEN PY--;
		ELIF K=='S' THEN PY++;
		DX = PX - 5;
		DY = PY - 3;
		CHIP_SET_SCROLL(DX, DY);
		//CHIP_SET_SCROLL(I, I / 2);
		Fill_Map();

		SPR_START();
		// SPR_POS(I / 2 + 5, I / 4 + 3);
		// SPR_POS(I + PX, I/2 + PY);
		SPR_POS(PX, PY);
		SPR_ADD(SPR_WHITE, 2, 2);
		SPR_POS(PX+2, PY);
		SPR_ADD(SPR_WHITE, 2, 2);
		SPR_POS(PX, PY+2);
		SPR_ADD(SPR_WHITE, 2, 2);
		SPR_POS(PX+1, PY+1);
		SPR_ADD(SPR_WHITE, 2, 2);

		Draw_VVRAM();
		VSYNC(2);
	//}
	}

	INKEY(2);
}

#ASM
; SetVVRAMSize — 仮想 VRAM サイズを動的に設定 (全参照箇所を自己書き換え)
; HL = width, DE = height
SetVVRAMSize:
	; width 関連の自己書き換え
	; FillMap: ld bc,width-1
	push hl
	dec l
	ld h,0
	ld (FillMap.ref_vvw_m1+1),hl
	pop hl
	; FillMap: ld hl,width*2
	push hl
	ld h,0
	add hl,hl
	ld (FillMap.ref_vvw_x2+1),hl
	pop hl
	; GrpSetSize: ld a,width
	ld a,l
	ld (GrpSetSize.ref_vvw+1),a
	; setvadrofs: ld de,width
	push hl
	ld h,0
	ld (setvadrofs.ref_vvw+1),hl
	pop hl
	; SpriteAdd: ld de,width (MULHLDE用)
	push hl
	ld h,0
	ld (SpriteAdd.ref_vvw1+1),hl
	pop hl
	; SpriteAdd: ld a,width
	ld a,l
	ld (SpriteAdd.ref_vvw2+1),a

	; height 関連の自己書き換え
	; SpriteAdd: add a,height
	ld a,e
	ld (SpriteAdd.ref_vvh+1),a

	ret

; SetScroll — スクロール位置を設定
; HL = MAPX, DE = MAPY
SetScroll:
	ld (MAPX),hl
	ld (MAPY),de
	ret

; SetMapSize — MAPW/MAPH を動的に設定
; HL = width, DE = height
SetMapSize:
	ld a,l
	ld (MAPW),a
	ld a,e
	ld (MAPH),a
	ret

; テスト用マップインデックス (2x2 スーパーチップ定義)
; 各 4 bytes: 左上/右上/左下/右下 のチップ番号
; ※8px オフセットスクロールを可視化するため、左右で異なるチップにする
TestMapIdx:
include "mapidx0.bin",B
;	; スーパーチップ 0: 白/赤
;	DB 0, 1, 8, 9
;	; スーパーチップ 1: チェッカー/緑
;	DB 7, 7, 7, 7
;	; スーパーチップ 2: 赤/青
;	DB 4, 6, 4, 6
;	; スーパーチップ 3: 縦縞/白
;	DB 3, 1, 3, 1

; テスト用圧縮マップ (64x64 スーパーチップ)
TestMapChip:
	REPT 4
	DB 21,21,21,21,24,4,4,4,4,25,21,21,21,21,21,24
	DB 21,21,21,21,24,4,4,4,4,25,21,21,21,21,21,24
	DB 21,21,21,21,24,4,4,4,4,25,21,21,21,21,21,24
	DB 21,21,21,21,24,4,4,4,4,25,21,21,21,21,21,24

	DB 21,21,21,21,24,4,4,4,4,25,21,21,21,21,21,24
	DB 21,21,21,21,24,4,4,4,4,25,21,21,21,21,21,24
	DB 21,21,21,21,24,4,4,4,4,25,21,21,21,21,21,24
	DB 21,21,21,21,24,4,4,4,4,25,21,21,21,21,21,24

	DB 26,22,22,27,24,4,4,4,4,26,22,22,22,22,27,24
	DB 26,22,22,27,24,4,4,4,4,26,22,22,22,22,27,24
	DB 26,22,22,27,24,4,4,4,4,26,22,22,22,22,27,24
	DB 26,22,22,27,24,4,4,4,4,26,22,22,22,22,27,24

	DB 21,21,21,21,21,21,21,21,21,21,21,21,21,21,21,21
	DB 21,21,21,21,21,21,21,21,21,21,21,21,21,21,21,21
	DB 21,21,21,21,21,21,21,21,21,21,21,21,21,21,21,21
	DB 21,21,21,21,21,21,21,21,21,21,21,21,21,21,21,21

	;DB 0, 8, 21, 23, 33, 21, 22, 23, 0, 21, 22, 23, 0, 1, 2, 3
	;DB 0, 9, 21, 23, 33, 21, 22, 23, 0, 21, 22, 23, 0, 1, 2, 3
	;DB 0, 10, 21, 23, 33, 21, 22, 23, 0, 21, 22, 23, 0, 1, 2, 3
	;DB 0, 11, 21, 23, 33, 21, 22, 23, 0, 21, 22, 23, 0, 1, 2, 3
	ENDM

; テスト用: PRIMARY_VVRAM に応じたページにチップ 4 (赤) を敷き詰める
FillBG_Entry:
	ld a,(PRIMARY_VVRAM)
	and 1
	jr nz,.fillbg_page1
	ld hl,VVRAM_PAGE0
	jr .fillbg_go
.fillbg_page1:
	ld hl,VVRAM_PAGE1
.fillbg_go:
	ld a,10		; 10行
.fillbg_outer:
	push af
	ld b,20		; 20チップ/行
.fillbg_inner:
	ld (hl),4	; チップ 4 = 赤
	inc hl
	djnz .fillbg_inner
	ld de,VVRAMWIDTH-20
	add hl,de
	pop af
	dec a
	jr nz,.fillbg_outer
	ret

; テスト用マスク付きスプライト (MASK_CHIP_SIZE=32 bytes per chip)
; 2x2 チップ = 4 チップ = 128 bytes
; 各チップ: mask[8] + B[8] + R[8] + G[8]

; ダイヤ型マスク付きスプライト (2x2=16x16 の中央にダイヤ、シアン色)
; mask: 0=描画、FF=透過
TestSprite_White:
; chip (0,0) — 左上: ダイヤの右下部分
	DB $FF,$FF,$FF,$c0,$C0,$80,$00,$00	; mask
	DB $00,$00,$00,$1F,$3F,$7F,$FF,$FF	; B (シアン=B+G)
	DB $00,$00,$00,$00,$00,$00,$00,$00	; R
	DB $00,$00,$00,$1F,$3F,$7F,$FF,$FF	; G
; chip (1,0) — 右上: ダイヤの左下部分
	DB $FF,$FF,$FF,$07,$03,$01,$00,$00	; mask
	DB $00,$00,$00,$F8,$FC,$FE,$FF,$FF	; B
	DB $00,$00,$00,$00,$00,$00,$00,$00	; R
	DB $00,$00,$00,$F8,$FC,$FE,$FF,$FF	; G
; chip (0,1) — 左下: ダイヤの右上部分
	DB $00,$00,$80,$C0,$E0,$FF,$FF,$FF	; mask
	DB $FF,$FF,$7F,$3F,$1F,$00,$00,$00	; B
	DB $00,$00,$00,$00,$00,$00,$00,$00	; R
	DB $FF,$FF,$7F,$3F,$1F,$00,$00,$00	; G
; chip (1,1) — 右下: ダイヤの左上部分
	DB $00,$00,$01,$03,$07,$FF,$FF,$FF	; mask
	DB $FF,$FF,$FE,$FC,$F8,$00,$00,$00	; B
	DB $00,$00,$00,$00,$00,$00,$00,$00	; R
	DB $FF,$FF,$FE,$FC,$F8,$00,$00,$00	; G
#END
