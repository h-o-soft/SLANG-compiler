///
/// TILELIB.SL — X1 PCG タイルマップライブラリ
///
/// 背景をテキスト VRAM + PCG で 40x24 文字描画する。
/// スクロール: 1 文字 (8 ピクセル) 単位。
/// ダブルバッファ: TILE_SET_PAGE で裏ページに描画。
///
/// 構造:
///   マップ: 圧縮スーパーチップ番号 (1 byte = 1 SC)
///   タイルインデックス: SC ごとに 4 バイト (TL, TR, BL, BR)
///   PCG: タイル番号 = PCG キャラクタ番号
///

// --- API ---
MACHINE TILE_INIT(0): TileInit;
MACHINE TILE_FILL_MAP(0): TileFillMap;
MACHINE TILE_SET_MAP(3): TileSetMap;
MACHINE TILE_SET_MAPSIZE(2): TileSetMapSize;
MACHINE TILE_SET_SCROLL(2): TileSetScroll;
MACHINE TILE_SET_ANIM(1): TileSetAnim;
MACHINE TILE_SET_PAGE(1): TileSetPage;
MACHINE TILE_SET_OFS(2): TileSetOfs;
MACHINE TILE_SET_SIZE(2): TileSetSize;
MACHINE TILE_INVALIDATE(0): TileInvalidate;

// ============================================================
// ブロック 1: 実装
// ============================================================
#ASM

; --- 定数 ---
TILE_TVRAM	EQU	$3000	; テキスト VRAM (I/O)
TILE_ATTR	EQU	$2800	; アトリビュート VRAM (I/O)
TILE_ATTR_PCG7	EQU	$27	; PCG 有効 + カラー 7
TILE_WIDTH	EQU	40	; 画面横文字数
TILE_HEIGHT	EQU	25	; 画面縦文字数
TILE_CHARROWS	EQU	24	; 描画行数 (上 24 行)
TILE_MAPWIDTH	EQU	20	; 横スーパーチップ数 (20×2=40 文字)

; ============================================================
; TileInit — テキスト VRAM/アトリビュート初期化 (両ページ)
; ============================================================
TileInit:
	; アトリビュート $2800-$2FFF を $27 で埋める (両ページ分)
	ld bc,TILE_ATTR
	ld d,TILE_ATTR_PCG7
	ld hl,$0800
.attr_loop:
	ld a,d
	out (c),a
	inc bc
	dec hl
	ld a,h
	or l
	jr nz,.attr_loop

	; テキスト VRAM $3000-$37FF を 0 で埋める (両ページ分)
	ld bc,TILE_TVRAM
	ld hl,$0800
.text_loop:
	xor a
	out (c),a
	inc bc
	dec hl
	ld a,h
	or l
	jr nz,.text_loop

	; 状態クリア
	xor a
	ld (_TILE_SCROLLX),a
	ld (_TILE_SCROLLX+1),a
	ld (_TILE_SCROLLY),a
	ld (_TILE_SCROLLY+1),a
	ld (_TILE_PAGEOFS),a
	; dirty=2 で初回描画を強制
	ld a,2
	ld (_TILE_DIRTY),a
	ret

; ============================================================
; TileFillMap — 現在の描画ページに 40×24 文字描画
;   ペア処理: 2 文字単位でスーパーチップ参照を共有
;   sub_x 奇数時は端 1 文字を別処理
; ============================================================
TileFillMap:
	; アニメ変化検出: ANIMIDX != ANIMSNAP なら dirty = 2 にする
	ld a,(_TILE_ANIMIDX)
	ld hl,_TF_ANIMSNAP
	cp (hl)
	jr z,.no_anim_change
	ld a,2
	ld (_TILE_DIRTY),a
.no_anim_change:
	; dirty == 0 なら何もしない
	ld a,(_TILE_DIRTY)
	or a
	ret z
	dec a
	ld (_TILE_DIRTY),a

	push ix

	; アニメインデックスをスナップショット (割り込み対策)
	ld a,(_TILE_ANIMIDX)
	ld (_TF_ANIMSNAP),a

	; sub_x = SCROLLX & 1 (行内ループで参照)
	ld a,(_TILE_SCROLLX)
	and 1
	ld (_TF_SUBX),a

	; absY = SCROLLY
	ld hl,(_TILE_SCROLLY)
	ld (_TF_ABSY),hl

	; DE = テキスト VRAM ベース + OFS + ページオフセット
	ld hl,(_TILE_DE_BASE)	; = OFS_Y*40 + OFS_X
	ld bc,TILE_TVRAM
	add hl,bc
	ex de,hl		; DE = $3000 + OFS
	ld a,(_TILE_PAGEOFS)
	add a,d
	ld d,a			; + page offset

	; 行ループカウンタ (shadow C')
	exx
	ld a,(_TILE_SIZE_H)
	ld c,a
	exx

.rowLoop:
	; sub_y = absY & 1, タイルオフセット = sub_y * 2 (0 or 2)
	ld hl,(_TF_ABSY)
	ld a,l
	and 1
	add a,a			; 0 or 2

	; 行用 IDXBASE = TileIdx + tile_offset を事前計算
	push de
	ld c,a
	ld b,0
	ld hl,(_TILE_IDXADR)
	add hl,bc
	ld (_TF_IDXBASE),hl

	; HL = absY >> 1 (マップ行番号)
	ld hl,(_TF_ABSY)
	srl h
	rr l

	; HL = HL * MAPW
	ld a,(_TILE_MAPW)
	ld e,a
	ld d,0
	call _TileMulHLDE
	; HL = (absY/2) * MAPW

	; HL += SCROLLX/2 (行内オフセット)
	ld de,(_TILE_SCROLLX)
	srl d
	rr e
	add hl,de

	; IX = MAPCHIPADR + HL (描画開始 SC のアドレス)
	ex de,hl
.ref_mapchipadr:
	ld ix,0			; パッチされる
	add ix,de
	pop de			; DE 復元 (テキスト VRAM ポインタ)

	; sub_x で分岐
	ld a,(_TF_SUBX)
	or a
	jr nz,.oddRow

	; --- 偶数 X: HALFW ペア (SIZE_W 文字) ---
	exx
	ld a,(_TILE_HALFW)
	ld b,a
	exx
	jr .pairLoop

.oddRow:
	; --- 奇数 X: 右半分1文字 + (HALFW-1)ペア + 左半分1文字 ---
	call _TileDrawRight
	inc ix
	exx
	ld a,(_TILE_HALFW)
	dec a
	ld b,a
	exx

.pairLoop:
	; SC を読んで左右 2 タイル出力
	ld a,(ix)
	and $3f
	add a,a
	add a,a			; A = SC * 4
	ld c,a
	ld b,0
	ld hl,(_TF_IDXBASE)
	add hl,bc		; HL = &tile[SC*4 + offset]

	; 左タイル
	ld a,(hl)
	call _TileAnimCheck
	ld b,d
	ld c,e
	out (c),a
	inc de

	; 右タイル
	inc hl
	ld a,(hl)
	call _TileAnimCheck
	ld b,d
	ld c,e
	out (c),a
	inc de

	inc ix

	exx
	dec b
	exx
	jp nz,.pairLoop

	; 奇数開始時: 末尾 SC の左半分 1 文字
	ld a,(_TF_SUBX)
	or a
	call nz,_TileDrawLeft

	; DE を次の行頭へ (DE += 40 - SIZE_W)
	ld hl,(_TILE_DE_ROWSTEP)
	add hl,de
	ex de,hl

	; absY++
	ld hl,(_TF_ABSY)
	inc hl
	ld (_TF_ABSY),hl

	exx
	dec c
	exx
	jp nz,.rowLoop

	pop ix
	ret

; ============================================================
; ヘルパー: 端1文字描画 (奇数 X 開始時)
; ============================================================

; IX のスーパーチップ右半分 (offset+1) を 1 文字出力
_TileDrawRight:
	ld a,(ix)
	and $3f
	add a,a
	add a,a
	ld c,a
	ld b,0
	ld hl,(_TF_IDXBASE)
	add hl,bc
	inc hl			; +1 = 右列
	ld a,(hl)
	call _TileAnimCheck
	ld b,d
	ld c,e
	out (c),a
	inc de
	ret

; IX のスーパーチップ左半分 (offset+0) を 1 文字出力
_TileDrawLeft:
	ld a,(ix)
	and $3f
	add a,a
	add a,a
	ld c,a
	ld b,0
	ld hl,(_TF_IDXBASE)
	add hl,bc
	ld a,(hl)
	call _TileAnimCheck
	ld b,d
	ld c,e
	out (c),a
	inc de
	ret

; ============================================================
; アニメチップ判定
;   閾値以上のチップは下位 2 ビットを ANIMSNAP で差し替える
; ============================================================
_TileAnimCheck:
.ref_animstart:
	cp 255			; 閾値 (TileSetAnim でパッチ, 初期値=無効)
	ret c			; 閾値未満 → そのまま
	and $fc
	push hl
	ld hl,_TF_ANIMSNAP
	or (hl)
	pop hl
	ret

; ============================================================
; HL = HL * DE (16bit unsigned)
; ============================================================
_TileMulHLDE:
	ld b,h
	ld c,l
	ld hl,0
.mulLoop:
	srl d
	rr e
	jr nc,.mulNoadd
	add hl,bc
.mulNoadd:
	sla c
	rl b
	ld a,d
	or e
	jr nz,.mulLoop
	ret

; ============================================================
; 設定 API
; ============================================================

; TileSetMap — HL = マップチップ, DE = タイルインデックス, BC = (未使用)
TileSetMap:
	ld (TileFillMap.ref_mapchipadr+2),hl
	ld (_TILE_IDXADR),de
	ld a,2
	ld (_TILE_DIRTY),a
	ret

; TileSetMapSize — HL = 幅, DE = 高さ (スーパーチップ単位)
TileSetMapSize:
	ld a,l
	ld (_TILE_MAPW),a
	ld a,e
	ld (_TILE_MAPH),a
	ld a,2
	ld (_TILE_DIRTY),a
	ret

; TileSetScroll — HL = X, DE = Y (キャラクタ単位)
;   X か Y が変化していたら dirty=2 (両ページ再描画)
TileSetScroll:
	; 新値を保存しつつ旧値と比較
	push hl
	push de
	; X 比較
	ld bc,(_TILE_SCROLLX)
	or a
	sbc hl,bc
	jr nz,.changed
	; Y 比較
	ex de,hl	; HL = 新 Y
	ld bc,(_TILE_SCROLLY)
	or a
	sbc hl,bc
	jr nz,.changed
	; 変化なし
	pop de
	pop hl
	ret
.changed:
	pop de
	pop hl
	ld (_TILE_SCROLLX),hl
	ld (_TILE_SCROLLY),de
	ld a,2
	ld (_TILE_DIRTY),a
	ret

; TileSetAnim — HL = アニメ閾値 (この番号以上が対象)
TileSetAnim:
	ld a,l
	ld (_TileAnimCheck.ref_animstart+1),a
	ret

; TileSetPage — HL = ページオフセット (0 or 4)
TileSetPage:
	ld a,l
	ld (_TILE_PAGEOFS),a
	ret

; TileSetOfs — HL = X, DE = Y (キャラクタ単位)
;   描画開始位置をテキスト VRAM 内でずらす
TileSetOfs:
	ld a,l
	ld (_TILE_OFS_X),a
	ld a,e
	ld (_TILE_OFS_Y),a
	; DE_BASE = Y * 40 + X
	push hl
	ex de,hl		; HL = Y
	ld de,40
	call _TileMulHLDE	; HL = Y * 40
	pop de			; DE = X
	add hl,de
	ld (_TILE_DE_BASE),hl
	ld a,2
	ld (_TILE_DIRTY),a
	ret

; TileSetSize — HL = W, DE = H (キャラクタ単位, W は偶数推奨)
TileSetSize:
	ld a,l
	ld (_TILE_SIZE_W),a
	; HALFW = W / 2
	srl a
	ld (_TILE_HALFW),a
	; DE_ROWSTEP = 40 - W
	ld a,40
	sub l
	ld (_TILE_DE_ROWSTEP),a
	xor a
	ld (_TILE_DE_ROWSTEP+1),a
	ld a,e
	ld (_TILE_SIZE_H),a
	ld a,2
	ld (_TILE_DIRTY),a
	ret

; TileInvalidate — 強制再描画フラグ (両ページ)
TileInvalidate:
	ld a,2
	ld (_TILE_DIRTY),a
	ret

#END

// ============================================================
// ブロック 2: ワークエリア
// ============================================================
#ASM

_TILE_ANIMIDX:	DB 0	; アニメフレーム (0-3, VSYNC_PROC で更新)
_TILE_SCROLLX:	DW 0
_TILE_SCROLLY:	DW 0
_TILE_MAPW:	DB 32
_TILE_MAPH:	DB 32
_TILE_PAGEOFS:	DB 0	; 描画ページオフセット (0 or 4)
_TILE_IDXADR:	DW 0	; タイルインデックステーブルアドレス
_TF_ANIMSNAP:	DB 0	; アニメインデックス スナップショット
_TF_ABSY:	DW 0	; 描画中の絶対 Y 座標
_TF_SUBX:	DB 0	; sub_x (0 or 1, 行間で固定)
_TF_IDXBASE:	DW 0	; 行ごと: TileIdx + tile_offset
_TILE_DIRTY:	DB 2	; 残り再描画ページ数 (0=不要, 1-2=必要)
_TILE_OFS_X:	DB 0	; 描画開始 X (キャラクタ単位)
_TILE_OFS_Y:	DB 0	; 描画開始 Y (キャラクタ単位)
_TILE_SIZE_W:	DB 40	; 描画幅 (キャラクタ単位)
_TILE_SIZE_H:	DB 24	; 描画高さ (キャラクタ単位)
_TILE_HALFW:	DB 20	; SIZE_W / 2 (ペア数)
_TILE_DE_BASE:	DW 0	; OFS_Y * 40 + OFS_X
_TILE_DE_ROWSTEP: DW 0	; 40 - SIZE_W (行末→次行頭の加算量)

#END
