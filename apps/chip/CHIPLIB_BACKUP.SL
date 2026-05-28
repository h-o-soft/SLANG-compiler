/// 
/// CHIPLIB.SL
/// 

VAR MAPX:MAPX;
VAR MAPY:MAPY;

MACHINE SPR_START(0): SpriteStart;
MACHINE SPR_POS(2): SpritePos;
MACHINE SPR_ADD(3): SpriteAdd;
MACHINE SPR_FLASH(1): SetSpriteFlash;

MACHINE Chip_Draw(3): ChipDraw;
MACHINE Chip_MaskDraw(3): ChipMaskDraw;

MACHINE Init_VVRAM(0):InitVVRAM;
MACHINE Draw_VVRAM(0):DrawVVRAM;
MACHINE Fill_Map(0):FillMap;

MACHINE GRP_OFS(2):GrpSetOfs;
MACHINE GRP_SIZE(2):GrpSetSize;
MACHINE GRP_ANIM(1):GrpSetAnim;

#ASM
; 
;  InitVVRAM
;    仮想VRAMとその関連データの初期化
; 
InitVVRAM:
	; 仮想VRAM(VVRAM)を消す
	ld bc,$4000+$E80	; Bの余分な部分に書く(ここでいいのか?)
	xor a
	call CLEARVVRAMBANK
	ld bc,$8000+$E80	; Rの余分な部分に書く
	ld a,$ff
	call CLEARVVRAMBANK

	; Primaryをページ0にしておく
	xor a
	ld (PRIMARY_VVRAM),a
	ret

; 1024bytes消す
CLEARVVRAMBANK:
	ld d,a
	ld hl,1024
.loop
	ld a,d
	out (c),a
	inc bc
	dec hl
	ld a,h
	or l
	jr nz,.loop
	ld a,d
	ret

;;;; <注意>
;;;; ↑ここまでSHARP X1用になっていて、 ↓ここから下はPC-8801mkIISR前提になっている

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
	ld ix,MAPCHIPADR

	; deにPrimary(描画済の表VVRAM)のアドレスを入れる
	ld de,VVRAMTOPADR

	ld a,(PRIMARY_VVRAM)
	and 1
	ld a,$5c
	jr z,.noswap1
	inc a
.noswap1
	ld (.setvbank+1),a

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

.setvbank:
	out ($5c),a

	; bcには仮想VRAM改行幅を入れる
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

	; メインメモリに戻しておく
	out	($5f), a

	pop IY
	ei
	ret


; マップチップ描画の左上座標を指定
GrpSetOfs:
	; hl = x(8の倍数にする事)
	; de = y
	call GETVRAMADR
	ld (VramDrawAddress+1),hl
	ret

; マップチップ描画の縦横の個数を指定
GrpSetSize:
	; hl = 横個数
	; de = 縦個数
	ld c,e
	ld b,d
	
	ld a,l
	cp 25
	jr c,.notfulldraw
	neg
	add a,40
	jr .setmargin
.notfulldraw
	ld a,8
.setmargin
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

	; 80*8 - (横個数*2) を計算しておく
	; hlを2倍
	sla l
	rl h
	ex de,hl
	; 80*8 - de
	or a
	ld hl,80*8
	sbc hl,de
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
	; 裏レジスタにVRAMアドレスを入れておく
	exx
VramDrawAddress:
	ld hl,$C000+80*DRAWY+DRAWX	; 1文字ぶん右下から初める
	ld de,80*8-(DRAWWCNT*2)
.drawwcnt0
	ld c,DRAWWCNT
	exx

	; 横描画数
.drawwcnt1
	ld b,DRAWWCNT
	; 縦描画数
.drawhcnt1
	ld c,DRAWHCNT

	ld a,(MAPY)
	and 1
	sla a
	sla a
	ld e,a
	ld a,(MAPX)
	and 1
	sla a
	or e
	; hlをオフセット情報を元にズラしてやる
	call setvadrofs
	push hl

	ld a,(PRIMARY_VVRAM)
	; 前回のオフセットにより比較元の位置をズラしておく
	call setvadrofs
	pop de
	; deがprimary、hlがsecondary

	; ※↑でaは変化しないのでこれでいい
	and 1
	ld a,$5c
	jr z,.noswap1
	inc a
.noswap1
	ld (.setvbank0+1),a
	xor 1
	ld (.setvbank1+1),a

	; ; 独立アクセスモード→いらんかな
	; LD A,%00011001
	; OUT ($32),A
.drawvvram_loop
	; 毎回やるのもどうかと思うが……
.setvbank0
	out ($5c),a

	ld a,(de)	; deがPrimary側
	; 128以上の場合(合成チップの場合)は必ず描画する
.animstart
	cp 80
	jr nc,.combdraw
	; 128未満の場合は前の画面と比較する
.setvbank1
	out ($5d),a
	cp (hl)

	; 一致している場合は描画しない
	jr z,.nodraw
.mapdraw
	; マップ描画
	push de
	push hl
	; aにチップ番号があるので48倍してからMAPGRPADRを足すと良い
	ld l,a
	ld h,0
.mapanimdraw
	add hl,hl
	add hl,hl
	add hl,hl
	add hl,hl
	ld e,l
	ld d,h
	add hl,hl
	add hl,de
	; 48倍
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
	; 合成バッファ位置を算出
	and $7f
	ld l,a
	ld h,0
	add hl,hl
	add hl,hl
	add hl,hl
	add hl,hl
	add hl,hl
	add hl,hl	; 64倍
	ld a,HIGH MERGEBUFFER
	add a,h
	ld h,a
	ex de,hl

	exx
	push hl
	exx
	pop hl
	;jr .chipdraw
	; まだここには来ないはず(合成処理を作っていないので)
.chipdraw
	; この時点で HL=VRAMアドレス、DE=画像アドレス になっている
	push bc
	call SetDrawAddress
	call ChipOneDraw

	pop bc
	; pop de
	; pop hl
	pop hl
	pop de
.nodraw
	inc hl
	inc de

	; 裏レジスタ側でVRAMアドレスを計算してやる
	exx
	dec c
	; 1ラインぶん(40キャラぶん)は単に横移動させる
	jr	nz,.yoko2add
	; 右端に到達していたら縦8ドットぶん足す
	add hl,de
.drawwcnt2
	ld c,DRAWWCNT
.yoko2add
	; 横16ドットおきなので2バイト
	inc hl
	inc hl
	exx

	djnz .drawvvram_loop

.drawwcnt3
	ld b,DRAWWCNT

	; hlとdeをVVRAMWIDTH-DRAWWCNT足してやる必要がある

	; こっちの方が速いかな？
	push bc
.vvrammargin
	ld bc,8
	add hl,bc
	ex de,hl
	add hl,bc
	ex de,hl
	pop bc

	; bcを使わない版
	; ld a,VVRAMWIDTH-DRAWWCNT
	; add a,l
	; ld l,a
	; ld a,h
	; adc a,0
	; ld h,a
	; ld a,VVRAMWIDTH-DRAWWCNT
	; add a,e
	; ld e,a
	; ld a,d
	; adc a,0
	; ld d,a

	; ld de,40-DRAWWCNT
	; add hl,de

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

	; C000 - FFFFをメインメモリに切り替える
	out ($5f),a

	ret

; MAPX, MAPYのオフセットを考慮して仮想VRAMの参照先頭位置をズラす
setvadrofs:
	ld hl,VVRAMTOPADR
	push de
	bit 2,a
	jr z,.noyofs
	; オフセットされている場合は一行ぶん足す
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

; SETVRAM_INDIVIDUAL
;   VRAMを独立アクセウモードに切り替える
SETVRAM_INDIVIDUAL:
	di
	; 独立アクセスモードに切り替え
;	ld a,(NAME_SPACE_DEFAULT.IO32H)
	in a,($32)
	and %10111111
	out ($32),a
;	ld (NAME_SPACE_DEFAULT.IO32H),a
	ei
	ret

; SETVRAM_EXTENDED
;   VRAMを拡張アクセウモードに切り替える
SETVRAM_EXTENDED:
	di
	; 拡張アクセスモードに切り替え
	; ld a,(NAME_SPACE_DEFAULT.IO32H)
	in a,($32)
	or %01000000
	out ($32),a
	; ld (NAME_SPACE_DEFAULT.IO32H),a
	ei
	ret

; SetDrawAddress
;    VRAM描画のためにアドレス自己書き換えを行う(16x8を描画するために16x8の各行先頭アドレスを設定する)
;    hl = 
SetDrawAddress:
	; アドレス計算
	ld bc,80
	ld (stor00 + 1), hl
	add hl,bc
	ld (stor01 + 1), hl
	add hl,bc
	ld (stor02 + 1), hl
	add hl,bc
	ld (stor03 + 1), hl
	add hl,bc
	ld (stor04 + 1), hl
	add hl,bc
	ld (stor05 + 1), hl
	add hl,bc
	ld (stor06 + 1), hl
	add hl,bc
	ld (stor07 + 1), hl
	ret

; 16x8を描画する
; hl = VRAMアドレス先頭, de = 画像アドレス
ChipOneDraw:
	ex de,hl

	di
	out ($5c),a
	call chipdraw
	out ($5d),a
	call chipdraw
	out ($5e),a
	call chipdraw
	out ($5f),a
	ei

	ret

chipdraw:
	ld (stackadr+1),sp
	ld sp,hl
	pop hl
stor00:	ld ($0000),hl
	pop hl
stor01:	ld ($0001),hl
	pop hl
stor02:	ld ($0002),hl
	pop hl
stor03:	ld ($0003),hl
	pop hl
stor04:	ld ($0004),hl
	pop hl
stor05:	ld ($0005),hl
	pop hl
stor06:	ld ($0006),hl
	pop hl
stor07:	ld ($0007),hl
	ld hl,0
	add hl,sp
stackadr:
	ld sp,0
	ret


; ChipMaskDraw
;   マスクデータつきの16x8画像を合成バッファに書き込む
;   点滅描画を行う場合はSetSpriteFlashで点滅状態設定をしてから呼ぶ事
ChipMaskDraw:
	; hl = source, de = dest
	; sourceの64バイト(16バイトマスク、48バイトRGB)をdeに描画
	; 先頭のマスクでマスクしてからRGBをorして格納する

	push hl
	ld bc,16
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
	; 描画先アドレスをixに移しておく
	di
	ld (.stackadr+1),sp
	ld sp,hl
	ld l,c
	ld h,b

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

	; hlを16バイト戻す(マスクの先頭)
	ld hl,-16
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

	; コード量削減のためにループ展開しないパターン
	ld b,8
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
	; bcは16バイト進める
	pop hl

	ld bc,16
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

	; SW * 64を計算して入れてやる(マスクあり画像横ぶんスキップ)
	; deにはまだSWが入っているはず
	ex de,hl	; deをhlに移す
	add hl,hl
	add hl,hl
	add hl,hl
	add hl,hl
	add hl,hl
	add hl,hl	; 64倍
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
	add hl,hl
	add hl,hl
	add hl,hl
	add hl,hl
	add hl,hl	; VVRAMWIDTH倍(32倍)
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
	ld de,VVRAMTOPADR	; VVRAM先頭アドレス
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

	; VVRAMのバンクを設定してやる
	ld a,(PRIMARY_VVRAM)
	and 1
	add a,$5c
	ld (.primary_bank + 1),a
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

.primary_bank
	out ($5c),a	; 事前に書き換えている
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
	; MAPGRPADR + VC * 48を計算する
	ld l,b
	ld h,0
	add hl,hl
	add hl,hl
	add hl,hl
	add hl,hl
	ld c,l
	ld b,h
	add hl,hl
	add hl,bc	; 48倍
	ld bc,MAPGRPADR
	add hl,bc

	ld bc,48
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
	; $C000〜をメインメモリに戻す
	out ($5f),a
	; hl = source, de = dest
	ld hl,(SIMGADR)
	call ChipMaskDraw

.spradd_hskip
;	VADR++;
	ld hl,(VADR)
	inc hl
	ld (VADR),hl
;	ADR = ADR + 64;
	ld hl,(SIMGADR)
	ld de,64
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
	add hl,hl
	add hl,hl	; 64倍
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

#END
