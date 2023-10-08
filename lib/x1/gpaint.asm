


#LIB PAINT
; HL = X
; DE = Y
; BC = 色(C)と境界色(B)
; 境界色をFFHにすると境界色なし
PAINTSTART:
	PUSH	BC
	PUSH	DE
	PUSH	HL
	LD	(SPWK),SP
	LD	HL,(SPWK)
	LD	D,H
	LD	E,L

	LD	A,B
	CP	0FFH
	JR	Z,.NOBORDER
	XOR	A
.NOBORDER
	; Aは引数の数で、1(色のみ) or 2(色と境界色)になる
	INC	A
	INC	A

; DE = パラメータアドレス
; 形式は下記のとおり
;    DW x座標
;    DW y座標
;    DB 色(4bitずつそれぞれ色を指定可能)
;    DB 境界色1
;    DB 境界色2
;    DB 境界色3...
; A = 色と境界色の数の合計数(1～7)
GPAINT_TOP:
	CALL	WIDTHPATCH
PAINTTOP:
	PUSH	DE
	
	LD	HL, COLCHK0+1
	LD	DE,8
	LD	BC,806h		; 8=回数、6=標準の相対ジャンプ量
.LOOP1
	LD	(HL),C
	ADD	HL,DE
	DJNZ	.LOOP1
	POP	HL

	; X,Yをpush
	LD	B,2
.LOOP2
	LD	E, (HL)
	INC	HL
	LD	D, (HL)
	INC	HL
	PUSH	DE
	DJNZ	.LOOP2

	; A = 残り引数 をBに入れておく(ループ回数)
	LD	B, A
	; 描画色
	LD	A, (HL)
	LD	(PAINTCOL), A
	AND	7

	; 描画色(下位)を境界色に含める
	CALL	ADDBORDER
	LD	A, (HL)
	RLCA	
	RLCA	
	RLCA	
	RLCA	
	LD	C, A
	AND	7
	JR	Z, .CHKBLACK

	; 描画色(上位)を境界色に含める(0以外のみ)
	CALL	ADDBORDER
	JR	.BORDERLP1
.CHKBLACK
	; 上位4ビットが0の場合は上位下位ともに同じ色を入れる(つまり 05Hを55Hにする)
	LD	A, C
	OR	(HL)
	LD	(PAINTCOL), A
	JR	.BORDERLP1

; 全ての境界色を設定
.BORDERLOOP
	LD	A,(HL)
	CALL	ADDBORDER
.BORDERLP1
	INC	HL
	DJNZ	.BORDERLOOP

	; メモリの一部をVRAMの未使用領域と交換
	CALL	EXCHANGE_MEM
	POP	HL
	POP	DE
	; X=DE、Y=HLとしてペイント処理を走らせる
	CALL	PAINTMAIN
	PUSH	HL

	; メモリの一部をVRAMの未使用領域と交換(する事で元の状態に戻す)
	CALL	EXCHANGE_MEM
	POP	HL

	; HLを戻り値にする
	; POP	HL
	POP	DE
	POP	DE
	POP	BC
	RET	

; なんらかのエラーが出た時はここに来る
PAINTERR:
	POP	HL
	POP	DE
	POP	BC
	LD	HL,0ffffh
	SCF
	RET

; WIDTH 40 か 80 により各コードにパッチをあてる
WIDTHPATCH:
	PUSH	AF
	PUSH	BC
	PUSH	DE
	PUSH	HL

	LD	A,1Ah
	IN	A,(2)		; 1A02h
	BIT	6,A		; 0で80、1で40
	LD	HL,PATCH_TBL
	LD	B,9
	LD	C,0FFh
.PATCHLOOP
	; 書き換えアドレスをDEに得る
	LD	E,(HL)
	INC	HL
	LD	D,(HL)
	INC	HL
	JR	NZ,.PATCH40
	INC	HL
	INC	HL
.PATCH40
	; 2バイトパッチをあてる
	LDI	
	LDI	
	JR	Z,.PATCH80
	INC	HL
	INC	HL
.PATCH80
	DJNZ	.PATCHLOOP

	POP	HL
	POP	DE
	POP	BC
	POP	AF
	RET

; 指定した塗り位置の色以外を全て境界色に含めて塗る(一般的なPAINT文)
; INPUT:
;	HL = X
;	DE = Y
;	BC = Color
PAINTAUTO:
	EX	DE,HL
	PUSH	HL	; Y
	LD	HL,PAINTPARAMS
	LD	(HL),E	; X
	INC	HL
	LD	(HL),D
	INC	HL
	POP	DE	
	LD	(HL),E	; Y
	INC	HL
	LD	(HL),D
	INC	HL
	LD	(HL),C	; Color
	INC	HL

	PUSH	HL
	CALL	WIDTHPATCH
	LD	HL,(PAINTPARAMS+2)	; Y
	LD	DE,(PAINTPARAMS)	; X
	CALL	CALCVRAMADR
	; Aに塗り開始位置のピクセル情報を入れて、
	; その色以外の7色を境界色にする
	CALL	GETPIXEL
	POP	HL

	LD	B,7
.ADDBORDER
	CP	B
	JR	Z,.SKIP
	LD	(HL),B
	INC	HL
.SKIP
	DEC	B
	JP	P,.ADDBORDER

	LD	DE,PAINTPARAMS
	LD	A,8

	; 辻褄あわせるためのダミーPUSH
	PUSH	BC
	PUSH	DE
	PUSH	HL
	JP	PAINTTOP

PAINTPARAMS:
	DS	12

; INPUT:
;	HL = VRAM base address
;	B  = target pixel
; OUTPUT:
;	A  = Color(0〜7)
;
; TRASHED:
;	none
GETPIXEL:
	PUSH	BC
	PUSH	DE

	LD	D,B	; target pixel
	LD	E,0	; color

	LD	B,H
	LD	C,L
	SET	6,B	; 4000h
	IN	A,(C)	; BLUE
	AND	D
	JR	Z,.NOBLUE
	LD	E,1
.NOBLUE
	SET	7,B	; C000H
	IN	A,(C)	; GREEN
	AND	D
	JR	Z,.NOGREEN
	SET	2,E
.NOGREEN
	RES	6,B
	IN	A,(C)	; RED
	AND	D
	JR	Z,.NORED
	SET	1,E
.NORED
	LD	A,E

	POP	DE
	POP	BC
	RET




PATCH_TBL:
	DW	RIGHTWIDTH
	CP	40
	CP	80

	DW	CALCVRWIDTH+1
	DW	-320
	DW	-640

	DW	W80DOUBLE
	NOP
	LD	B,H
	ADD	HL,HL
	LD	B,H

	DW	VRUPWIDTH
	SUB	40
	SUB	80

	DW	VRDNWIDTH
	ADD	A,40
	ADD	A,80

	DW	VRDNCMP
	CP	3
	CP	7

	DW	VRDNCMP2
	CP	0E8h
	CP	0D0h

	DW	SETLPCOUNT
	LD	A,2Bh
	LD	A,16h

	DW	LDBUFSZ+1
	DW	03E8h
	DW	07D0h


PAINTMAIN:
	; キュー初期化
	LD	A, 0FFh
	LD	(BUF_HEAD),A
	LD	(BUF_TAIL),A

	; DE,HLでVRAMアドレスを算出(HL、B、裏Aが返る)
	CALL	CALCVRAMADR

; まず、塗り開始位置が境界色かどうか調べる
PAINTLOOP:
	CALL	CHECKCOL
	LD	D,A
	AND	B
	JP	NZ,DEQUEUE
	LD	A,D

; 非境界色の場合は左側境界を探す
SEARCHLEFT:
	OR	A
	JR	Z,LEFT8		; 境界色がないので左に移動
; 境界色を見つけたのでBを左に動かして実際の位置を探す
SEARCHLEFT2:
	LD	D,A
	AND	B
	JR	NZ,FLEFTBORDER
	LD	A,D
	RLC	B
	JR	NC,SEARCHLEFT2
	; 左端までチェックしたら8ドット境界で左に移動
LEFT8:
	; チェックビットを左端に移動させる
	LD	B,80h
	; 左端か？
	EX	AF,AF'
	DEC	A
	; 左端の場合はVRAM位置が左端でビット位置左端(80h)が塗り開始位置となる
	JP	M,LEFTEND
	EX	AF,AF'
	; VRAM8ドット左に移動
	DEC	HL
	; 境界色を得る
	CALL	CHECKCOL
	RLC	B	; 80h→01h
	JR	SEARCHLEFT

; 左端に到達したのでここを境界とする
; 左のボーダーを発見した
FLEFTBORDER:
	; 実際には境界上にサーチ位置があるので
	; ボーダーを右に移動させる
	RRC	B
	JR	NC,LEFTEND
	; 8ドット境界をまたいだので境界を右に移動
	INC	HL
	EX	AF,AF'
	INC	A
	EX	AF,AF'
LEFTEND:
	; 塗り開始位置を保存する
	CALL	SETUPPAINT

	; この位置を開始位置として今度は右側を探す
	CALL	CHECKCOL
	; 開始位置の境界ビットを保存
	LD	D,A
	; Bは大丈夫な位置のはずなのでそのすぐ右から見ていく
SEARCHRIGHT:
	RRC	B
SEARCHRIGHT2:
	JR	C,RIGHT8
	AND	B
	JR	NZ,FRIGHTBORDER	; 右端の境界ドットを発見
	LD	A,D
	JR	SEARCHRIGHT

; 8ドットをまたいだので右に移動
RIGHT8:
	INC	HL
	EX	AF,AF'
	INC	A
	; TODO 自己書き換えでWIDTH数にする
RIGHTWIDTH:
	CP	40
	JR	Z,RIGHTEND0
	EX	AF,AF'
	CALL	CHECKCOL
	; 境界ビットが無い場合はそのまま右に移動
	OR	A
	JR	Z,RIGHT8
	LD	D,A
	JR	SEARCHRIGHT2

RIGHTEND0:
	EX	AF,AF'

FRIGHTBORDER:
; 境界上にあるので左に移動させる
	RLC	B
	JR	NC,RIGHTEND
	DEC	HL
	EX	AF,AF'
	DEC	A
	EX	AF,AF'
RIGHTEND:
	; 右端の情報諸々を保存する
	LD	A,B
	LD	(RIGHTBIT),A	; 右端ビット

	LD	A,(XPOSDIV8)
	LD	B,A
	EX	AF,AF'

	SUB	B
	LD	(PAINTREMAIN),A	; 塗るX/8の数

	LD	HL,(VRAMADDR)	; 左端のVRAMアドレスを得てHLに入れなおし
	CALL	VRAMUP		; 上に移動させ
	CALL	NC,CHKENQUEUE	; キャリーが立っていなかったら上の行をチェックしてキューに入れる
	LD	HL,(VRAMADDR)	; ふたたび左端のVRAMアドレスを得てHLに入れなおし
	CALL	VRAMDOWN	; VRAMアドレスを一行ぶん下に移動させ
	CALL	NC,CHKENQUEUE	; キャリーが立っていなかったら下の行をチェックしてキューに入れる

	LD	A,(RIGHTBIT)	; 右端の位置情報をAに得ておく
	EX	AF,AF'

	; 1ライン塗る処理
	CALL	DRAWLINE

DEQUEUE:
	; キューが尽きたかチェック
	LD	A, (BUF_TAIL)
	LD	D,A
	LD	A,(BUF_HEAD)
	CP	D
	RET	Z			; キューに何も入っていないので終了
	; なんか入ってるのでキュー先頭から拾う
	INC	A
	LD	(BUF_HEAD),A
	JR	NZ,.NOHEAD		; HEADが0以外なら普通にBUF_HEADから拾い、0なら初期化して先頭に戻す
	LD	HL,(BUFADR)		; BACKUP_ADR
	JR	.DEQUEUESTART
.NOHEAD
	LD	HL,(BUFADR_HEAD)
.DEQUEUESTART
	LD	E,(HL)			; VRAMアドレスを取得
	INC	HL
	LD	D,(HL)
	INC	HL
	LD	B,(HL)			; 塗り対象ビット
	INC	HL
	LD	A,(HL)			; X/8とEVENODDを混ぜたもの
	INC	HL
	LD	C,A
	AND	080h			; EVENODDのみ取り出す
	LD	(EVENODD),A
	CPL				; 80h or 00hなので、7fh or ffhになる
	AND	C			; X/8成分のみをAに取り出す
	EX	AF,AF'			; ↑のAを裏Aに入れてやる
	LD	(BUFADR_HEAD),HL	; リングバッファを更新し
	EX	DE,HL			; 取り出したVRAMアドレスをHLに入れて
	JP	PAINTLOOP		; 塗りをループさせる

PAINTEND:
	RET


CHKENQUEUE:
	; この位置から右側にPAINTREMAINぶんだけ走査してキューに入れてやる

	; まずは開始位置と終了位置が同じかどうか見るが
	; その前に下記対応をする
	; 裏(現在)のAを X/8 にする
	; 裏のdを塗り残りバイト数にする(remain)
	; 裏のeも何かに使うはず
	; 表のBを塗り対象ビットにする
	LD	A,(XPOSDIV8)	; 裏AをX/8に
	EX	AF,AF'
	LD	A,(PAINTBIT)
	LD	B,A		; 表Bを塗り開始ビットに
	EXX
	LD	A,(RIGHTBIT)	; 裏Eを終端ビットに
	LD	E,A
	LD	A,(PAINTREMAIN)	; 裏Dを塗り残りバイト数に
	LD	D,A
	EXX
	OR	A
	JR	Z,CHKRIGHT	; 塗り開始ビットと塗り終了ビットが同じなので1バイト以上またいで探す必要なし

	BIT	7,B			; 塗り対象ビットが左端か調べて
	JR	NZ,CHKLEFTLOOP		; 左端の場合は全ビットが境界色の場合があるのでそのチェックをする
	CALL	CHECKCOL		; 左端じゃないの場合は現在のHLのVRAM位置の境界色をAに得て
	JR	CCHKLOOP1		; ここに飛ぶ(通常チェック)
CHKLEFTLOOP:
	CALL	CHECKCOL		; まずは現在位置の境界色状況を確認し
	CP	0FFh			; 全てが境界色だった場合は
	JR	Z,BORDERLEFT		; いちいちチェックする必要がないのでここでX/8単位の右に移動させる処理に飛ぶ

;====================================================================
; 非境界色を発見したらキューに入れる
;====================================================================
CCHKLOOP1:
	LD	C,A
CCHKLOOP:
	AND	B
	JR	Z,FOUNDPOINT		; 非境界色なのでキューに溜める
CBORDER:

;-----------------------------
;現在境界色なので右に移動させる
;-----------------------------
	RRC	B
	LD	A,C
	; バイトまたいだか？
	JR	NC,CCHKLOOP	; またいでなければチェック継続
BORDERLEFT:
	; またいだので右に移動
	EX	AF,AF'
	INC	A		; X/8位置を右に
	EX	AF,AF'
	INC	HL
	EXX
	DEC	D		; 残りバイト数を減らす
	EXX
	JR	Z,CHKRIGHT	; 残りバイト数が尽きたら右側チェック?
	JR	CHKLEFTLOOP	; 尽きてないので境界色が続いているという事なのでループする

;====================================================================
; 非境界色の先頭なのでまずはキューにためる
; 非境界色が尽きるまで右に移動し、
; 境界色を発見したら境界色の中で非境界色を探す処理に遷移する
;====================================================================
FOUNDPOINT:
	CALL	ENQUEUE		; まずは非境界色を塗り対象としてキューに蓄積

NBLOOP:
	LD	A,C		; 境界ビットをAに入れる
	RRC	B		; チェックする位置を右に1ドットずらす
	JR	NC,SBORDER2	; チェックビットが右端をハミ出てなければ境界色探しループを継続
	; ハミだしてたらVRAM位置を右に
	INC	HL
	EX	AF,AF'
	INC	A		; X/8位置を右に
	EX	AF,AF'
	EXX
	DEC	D
	EXX
	JR	Z,CHKRIGHT	; 残りバイト数が尽きたので右チェック
	; 尽きてないので現在の情報を取得し
	CALL	CHECKCOL
	LD	C,A
SBORDER2:
	AND	B		; チェックする
	JR	NZ,CBORDER	; 境界色の中で非境界色を探すところに戻る
	; まだ非境界色なので境界色探しを続ける
	JR	NBLOOP

; 右端終端までチェックを進める
CHKRIGHT:
	CALL	CHECKCOL		; 現在のVRAM位置の境界色を得て
	LD	C,A			; それをCに保存しておく

CHKRIGHTLOOP:
	AND	B			; チェックして
	JR	Z,FOUNDBORDER2		; 非境界色ならここに飛び
CHKRIGHTLOOP2:
	EXX
	LD	A,E			; 右端の塗り終端ビットをAに入れ
	EXX	
	CP	B			; 終端までいったかをチェックして
	RET	Z			; 終端までいったらキューに入れる処理終了
	RRC	B			; いってなかったらチェックビットを右にズラして
	LD	A,C			; VRAM境界情報をAに入れて
	JR	CHKRIGHTLOOP		; 再度チェック

FOUNDBORDER2:
	CALL	ENQUEUE			; 非境界色をキューに入れる
	JR	FOUNDENDCHK

CHKRIGHT2:
	CALL	CHECKCOL
	LD	C,A

NBRIGHTLOOP:
	AND	B			; チェック位置とANDをとって
	JR	NZ,CHKRIGHTLOOP2	; 境界色だったら右端チェックに戻る
FOUNDENDCHK:
	EXX	
	LD	A,E			; 右端終端チェック
	EXX	
	CP	B			; 終端に到達したかを調べ
	RET	Z			; 到達していたら終了
	LD	A,C			; 到達していない場合は境界情報をAに戻し
	RRC	B			; チェック位置を右に1つずらして
	JR	NBRIGHTLOOP		; 境界色チェックに戻る

DRAWLINE:
	; Cは塗り色変数
	LD	A,(PAINTCOL)
	LD	C,A
	; BはVRAM3プレーンぶんのループ
	LD	B,3
	; DはVRAMアドレスに足しこむ値
	LD	D,0
DRAWLOOP:
	RRC	C			; 塗り色を右ローテートさせて
	JR	C,.DRAWCOL		; 該当色が有効ならここに飛び
	LD	HL, 0A300h		; 有効じゃない場合はHLに0A300hを入れ
	ld	E, 0			; Eに0を入れ
	JR	NODRAWCOL		; ここに飛ぶ
.DRAWCOL
	LD	HL,0B32Fh		; 該当色が有効の場合はb32fhをhlに入れて
SETCOL1:
	LD	E,055H			; eにタイル状態を入れ(ここは事前に書き換える事)
NODRAWCOL:
	LD	A,0
	LD	(MASKTILE1),HL		; タイル一色目を塗るかどうか

	BIT	3,C			; タイル二色目が塗りかどうか
	JR	NZ,.DRAWCOL2
	LD	HL, 0A300h		; 有効じゃない場合はHLに0A300hを入れ
	XOR	A
	JR	NODRAWCOL2
.DRAWCOL2
	LD	HL,0B32fh
SETCOL2:
	LD	A,0AAh			; TODO 塗りパターン2を入れる
NODRAWCOL2:
	LD	(MASKTILE2),HL		; タイル2色目を塗るかどうか
	; タイル2色ぶん情報を混ぜてAに入れてやる
	OR	E
	; まとめ塗りの色を入れておく
	LD	(BLOCKDRAW+1),A

	; VRAMアドレス加算(4000h→8000h→C000h)
	LD	A,D
	ADD	A,040H
	LD	D,A

	EXX
	LD	BC,(VRAMADDR)
	OR	B
	LD	B,A

	; BCが描画VRAMアドレスになっているはず
	LD	A,(PAINTREMAIN)		; 塗りチェック残り/8を
	LD	L,A			; Lに入れて
	LD	A,(PAINTBIT)		; 塗り対象ビットを
	LD	D,A			; Dに入れて
	XOR	A			; Aを0にして
	JP	.LEFTMASK		; 左側マスク部分を塗る
.LEFTMASKLOOP
	OR	D			; 塗り開始対象ビットとORして
.LEFTMASK
	RLC	D			; 塗り開始対象ビットを左ローテートさせて
	JP	NC,.LEFTMASKLOOP	; 左がハミでるまでループ

	DEC	L			; 塗りチェック残り/8を1減らして
	INC	L			; 戻して
	JR	Z, PAINTRIGHT		; 塗りチェック残り/8が0だったらここに飛ぶ(つまりブロック単位で塗る必要がなければ右側描画処理に飛ぶ)
	CALL	MASKDRAW		; 左または右側の実描画処理
	INC	BC

	; PAINTREMAINが尽きるまでマスクカラーで塗ってやる
BLOCKDRAW:
	LD	A,0FFh			; ここの0は実際の塗りのパターンで書き換えられている

	JP	BLOCKPAINT1
BLOCKPAINT:
	OUT	(C),A			; Aで塗る
	INC	BC
BLOCKPAINT1:
	DEC	L			; 塗り残し数
	JP	NZ,BLOCKPAINT		; 8ドットぶん、モリモリ塗っていく
	JR	BLOCKEND

	; 右側を塗る(混ぜる)
PAINTRIGHT:
	EX	AF,AF'
	LD	D,A			; 右端ビット情報をdに入れて
	EX	AF,AF'
	JR	RIGHTMASK
BLOCKEND:
	LD	A,(RIGHTBIT)		; 右側終端ビット
	LD	D,A
	XOR	A
	JP	RIGHTMASK
RIGHTMASKLOOP:
	OR	D
RIGHTMASK:
	RRC	D
	JP	NC,RIGHTMASKLOOP
	CALL	MASKDRAW		; 右側の実描画処理

	EXX
	DJNZ	DRAWLOOP
	
	RET

; 1ラインの左端または右端あるいはその両方部分を描画
; INPUT:
;	BC = VRAMアドレス
;	A  = 塗りマスク
MASKDRAW:
	LD	D,A			; 塗りマスクを保存しておき
	IN	E,(C)			; そこのビット情報を拾い
MASKCOL2:
	OR	0AAh			; タイル1を設定
MASKTILE1:
	; 塗る場合
	CPL
	OR	E
;	; 塗らない場合こっちになる
;	NOP
;	AND	E

	LD	E,A			; とりあえず保存し

	LD	A,D			; 塗りマスクを戻し
MASKCOL1:
	OR	055h			; タイル2を設定

MASKTILE2:
	; 塗る場合
	CPL
	OR	E
;	; 塗らない場合こっちになる
;	NOP
;	AND	E

	OUT	(C),A
	RET

;DEBUGPUT:
;	PUSH	AF
;	PUSH	BC
;
;	LD	A,B
;	LD	B,H
;	SET	7,B
;	LD	C,L
;	OUT	(C),A
;	
;	POP	BC
;	POP	AF
;	RET

SETUPPAINT:
	LD	A,(EVENODD)
	XOR	80h
	LD	(EVENODD),A		; 80h <-> 00h な感じで最上位ビット反転

	LD	A,0AAh			; まずタイルパターンとしてAA入れて
	JR	Z,CHEVENODD
	CPL
CHEVENODD:
	LD	(SETCOL1+1),A		; タイル状態を入れる
	LD	(MASKCOL1+1),A		; 〃
	CPL	
	LD	(SETCOL2+1),A		; それを反転させたものを入れる
	LD	(MASKCOL2+1),A		; 〃

	LD	A,B
	LD	(PAINTBIT),A		; 塗り開始位置の塗り対象ビット
	LD	(VRAMADDR),HL		; 塗り開始位置のVRAMアドレス
	EX	AF,AF'
	LD	(XPOSDIV8),A		; 塗り開始位置の左端残り値(X/8)
	EX	AF,AF'

	RET

; VRAMアドレスを計算する
;
; INPUT:
;	HL = Y
;	DE = X
; OUTPUT:
;	HL = VRAM base address
;	B  = bit position
;	(EVENODD) = LINE EVEN or ODD
;	裏A = X/8
; TRASHED:
;	AF,BC,DE
;
; HL = Y、(D)E = Xとして範囲外かどうかを調べつつVRAMアドレスを得る
; 範囲外の場合は容赦なくエラーアドレスに飛ばす
CALCVRAMADR:
	; IF Y >= 200 THEN PAINTERR
	PUSH	HL
	LD	BC,-200
	ADD	HL,BC
	POP	HL
	JP	C,PAINTERR

	; IF X >= WIDTH THEN PAINTERR
	EX	DE,HL
	PUSH	HL
CALCVRWIDTH:		; ここはWIDTH 40/80において-320 / -640で書き換えられる
	LD	BC,-320
	ADD	HL,BC
	POP	HL
	JP	C,PAINTERR

	EX	DE,HL

	; この計算をしよう
	;  (X/8) + ((Y & 7)<<11) + ((Y/8)*WIDTH{40 or 80})

	; まずここ
	; ((Y & 7)<<11)
	LD	A,L	; Yの下位
	AND	7
	LD	B,A	; BCに構築するので上位で、この時点で8bit目に来ている事になる、ので3ビット左にすると11bit目に来る
	AND	1
	RRA
	LD	(EVENODD),A	; 偶数奇数状態をEVENODDに保存
	; Y/8の部分をLに入れておく
	LD	A,L
	AND	$F8
	LD	L,A

	; Y & 7の部分はAに入れておく
	LD	A,B

	; この時点でLは8倍されているので(Y/8)を右シフトしていないので
	; WIDTH40だと5倍、WIDTH80で10倍すればいい
W80DOUBLE:
	; WIDTH 80の時は、ここがADD HL,HLになる
	NOP
	LD	B,H	; x8 or x16の値をBCに入れる
	LD	C,L
	ADD	HL,HL	; x16 or x32
	ADD	HL,HL	; x32 or x64
	ADD	HL,BC	; x40 or x80
	; この値に(Y & 7) << 11を足す……には……
	; 現在AにはY & 7が入っているので、理屈上(Y & 7) << 8の状態
	ADD	A,A	; << 9
	ADD	A,A	; << 10
	ADD	A,A	; << 11	三回足せば11ビットシフト扱いになる
	OR	H	; 上位にもってきて
	LD	H,A	; これでとりあえずYの計算は完了(のはず)

	; これでHLにYの値が入ったので、あとはX/8の値を足してやればいい
	; XはDEに入っているので、DEが右3ビットシフト出来れば良い
	LD	A,E
	AND	7

	; 48 Cyelces right shift
	SRL D
	RR E
	SRL D
	RR E
	SRL D
	RR E

	; 裏レジスタAにX/8を入れておく
	EX	AF,AF'
	LD	A,E
	EX	AF,AF'

	; DEをHLに足す(これでVRAMアドレス算出完了(のはず))
	ADD	HL,DE

	; そしてBに、ビット位置を作る
	LD	B,1
	INC	A	; 1 〜 8
.BITLOOP
	RRC	B	; 初回でBは80Hになり、右に動いていく
	DEC	A
	JR	NZ,.BITLOOP

	RET

; VRAM位置を1ピクセルぶん上に上げる。もう上げられない場合はキャリーを立てて戻る
;
; INPUT:
;	HL = VRAM base address
; OUTPUT:
;	HL = VRAM base address
; TRASHED:
;     A
VRAMUP:
	LD	A,H
	SUB	8		; 800hを引いてみる
	LD	H,A
	RET	NC		; 800h以上の場合は(Y % 8の範囲内を一つ上に移動しただけの場合)成功でそのまま戻る

	; 800h以下の場合は
	ADD	A,40h		; 800h以下の場合 = (Y % 8)が0の場合は、40hを足してやると正の値の正しい位置になる
	LD	H,A		; その値をhに戻し

	LD	A,L		; VRAM値下位をAに入れて
VRUPWIDTH:
	SUB	28h		; WIDTH値を引き(40 or 80)
	LD	L,A		; Lに戻し
	RET	NC		; WIDTH値以上の場合はアドレス計算は正しいので戻り

	DEC	H		; WIDTH値以下の場合は上位を1減らし
	BIT	3,H		; 上端に達したか調べ
	SCF			; Carryを立て
	RET	Z		; 上端に達してなければキャリーを立てて戻り
	CCF
	RET			; 上端に達したらキャリーを寝かせて戻る


; VRAM位置を下に下げる。もう下げられない場合はキャリーを立てて戻る
;
; INPUT:
;	HL = VRAM base address
; OUTPUT:
;	HL = VRAM base address
; TRASHED:
;     A
VRAMDOWN:
	LD	A,H		; VRAMの上位アドレスをAに
	ADD	A,8		; Y % 8の範囲内での移動をまず試し
	LD	H,A
	AND	38H		; Y % 8の範囲内かどうか調べて
	RET	NZ		; 範囲内ならそのまま返る(つまり Y % 8が0〜6だったので、そのまま下に移動するだけですんだ)
				; 範囲外なら = Y % 8が7で、次のブロックの上端に移動させる必要があるので
	RES	6,H		; 第6ビット(40h)を落として正しい値にし
	LD	A,L		; VRAM下位をAに入れ
VRDNWIDTH:
	ADD	A,28h		; WIDTH値を加算して一行下にして
	LD	L,A		; Lに戻し
	JR	NC,.NOADDHL	; FFHをオーバーしていなければH加算の必要はなく
	INC	H		; FFHをオーバーしていたらHを加算してやり
				; ここまででおおむね完成
.NOADDHL
	LD	A,H		; VRAM上位をAに入れて
VRDNCMP:
	CP	3		; その値が7 or 3の場合は(WIDTHにより変えてやる)
	CCF
	RET	NC		; VRAM上位が7以下ならそのまま返り
	RET	NZ		; 7 or 3でも返り
				; ↑VRAM範囲におさまっている
	LD	A,L		; 8以上の場合は下位をAに入れて
VRDNCMP2:
	CP	0E8h		; d0h or e8hと比較して
				; ↑これを超えてたら画面範囲外
	CCF			; キャリーを反転させ(0にして)
	RET			; 戻る


; Aの色を境界色に設定する
; DE,AF破壊
ADDBORDER:
	; Aを8倍してオフセットにあわせる
	RLCA
	RLCA
	RLCA
	LD	DE,COLCHK0+1
	ADD	A,E
	LD	E,A
	JR	NC,.NOADD
	INC	D
.NOADD
	XOR	A
	LD	(DE),A
	RET




; HLで示されるVRAMベースアドレスから境界色のみビットが立った状態にしてAを返す
; BCは保存されるが、DEは破壊される

; INPUT:
;    HL = VRAM base address
; OUTPUT:
;    A = border bits
; TRASHED:
;   DE
CHECKCOL:
	PUSH	BC

	LD	B,H
	LD	C,L
	SET	6,B	; 4000h
	IN	A,(C)	; BLUE
	LD	D,A	; Dに青を
	SET	7,B	; C000H
	IN	A,(C)	; GREEN
	LD	E,A	; Eに緑を
	RES	6,B
	IN	A,(C)	; RED
	LD	C,A

	; この時点で
	; D = BLUE / C = RED / E = GREEN
	; のビット状態が入っている

; 境界色が黒
	LD	B,0
COLCHK0:
	JR	COLCHK1

	LD	A,D
	OR	E
	OR	C
	CPL
	; 一時的にBに入れる
	LD	B,A
	NOP

; 境界色が青
COLCHK1:
	JR	COLCHK2

	LD	A,E
	OR	C
	CPL
	AND	D
	OR	B
	LD	B,A

; 境界色が赤
COLCHK2:
	JR	COLCHK3

	LD	A,E
	OR	D
	CPL
	AND	C
	OR	B
	LD	B,A

; 境界色が紫
COLCHK3:
	JR	COLCHK4

	LD	A,E
	CPL
	AND	C
	AND	D
	OR	B
	LD	B,A

; 境界色が緑
COLCHK4:
	JR	COLCHK5

	LD	A,C
	OR	D
	CPL
	AND	E
	OR	B
	LD	B,A

; 境界色が水色
COLCHK5:
	JR	COLCHK6
	LD	A,C
	CPL
	AND	E
	AND	D
	OR	B
	LD	B,A

; 境界色が黄色
COLCHK6:
	JR	COLCHK7
	LD	A,D
	CPL
	AND	E
	AND	C
	OR	B
	LD	B,A

; 境界色が白
COLCHK7:
	JR	COLCHK8

	LD	A,D
	AND	E
	AND	C
	OR	B
	NOP
	NOP

COLCHK8:
	POP	BC
	RET


; メモリをVRAM未使用領域と交換
; 交換元メインメモリアドレスはBACKUP_ADRに入っているが、プログラムで書き換えられるようにすべき
EXCHANGE_MEM:
		LD	BC, 4000h
		LD	HL, (BUFADR)		; BACKUP_ADR
SETLPCOUNT:
		LD	A, 2Bh

EX_LOOP:
		EX	DE, HL
LDBUFSZ:
		LD	HL, 3E8h
		ADD	HL, BC
		LD	C, L
		LD	B, H
		EX	DE, HL

.LOOP2
		LD	D, (HL)
		IN	E, (C)
		LD	(HL), E
		OUT	(C), D
		INC	HL
		INC	C
		JR	NZ, .LOOP2
		INC	B
		DEC	A
		JR	NZ, EX_LOOP
		RET	

; INPUT:
;	HL = VRAMアドレス
;	B  = チェック対象ビット
;	裏A = X/8位置
;	(EVENODD) = Yの偶数奇数フラグ
ENQUEUE:
	; キューを進める
	LD	A,(BUF_HEAD)
	LD	D,A
	LD	A,(BUF_TAIL)
	INC	A
	CP	D
	; TAILがHEADに追い付いてしまったらバッファが尽きたという事
	JP	Z,PAINTERR
	; バッファ尽きてないのでキューに足せる
	LD	(BUF_TAIL),A
	; 初期状態 or ひとまわりした？
	OR	A

	EX	DE,HL			; VRAMアドレスをDEに移す
	JR	NZ,.NOBUFINIT
	LD	HL,(BUFADR)		; BACKUP_ADR	; TODO バッファ先頭アドレスは動かせるようにする
	JR	.STARTENQUEUE
.NOBUFINIT
	LD	HL,(BUFADR_TAIL)
.STARTENQUEUE
	LD	(HL),E			; VRAMアドレスをキューに入れる
	INC	HL
	LD	(HL),D
	INC	HL
	LD	(HL),B			; 現在位置ビットを保存
	INC	HL
	LD	A,B			; Bを使うので表Aにバックアップしておく
	EX	AF,AF'
	LD	B,A			; X/8の値
	LD	A,(EVENODD)		; 偶数奇数フラグ
	OR	B			; を、混ぜて保存
	LD	(HL),A
	INC	HL
	LD	A,B			; X/8の値を戻す
	EX	AF,AF'
	LD	B,A			; 表Aにバックアップしておいたチェック対象ビットを戻す

	LD	(BUFADR_TAIL),HL
	EX	DE,HL			; DEに入っているVRAMアドレスをHLに戻す
	RET

PAINTCOL:
	DB	0
EVENODD:
	DB	0
VRAMADDR:
	DW	0
PAINTBIT:
	DB	0
XPOSDIV8:
	DB	0
RIGHTBIT:
	DB	0
PAINTREMAIN:
	DB	0
BUF_HEAD:
	DB	0
BUFADR_HEAD:
	DW	0
BUF_TAIL:
	DB	0
BUFADR_TAIL:
	DW	0

; バッファアドレスのデフォルトはSLANGコンパイルのワークの末尾(なので基本的には1kbくらいは空いているはず)
BUFADR:
	DW	NAME_SPACE_DEFAULT.__WORKEND__

SPWK:
	DW	0

#ENDLIB

#LIB SETPAINTBUF
; INPUT:
;	HL = 1kのバッファの先頭アドレス
	LD	(BUFADR),HL
	RET
#ENDLIB


#LIB BFILL
; ペイントルーチンのタイル横ライン描画を流用したBOX FILL
; X2>X1、Y2>Y1である必要があるので注意！(手抜き)

	CALL	WIDTHPATCH

	POP HL
	LD (FILLDAT.RETADR),HL

	; Color
	POP	DE
	LD	A,E
	LD	HL,PAINTCOL
	LD	(HL),A
	AND	7
	LD	A,E
	AND	0F0h
	OR	A
	JR	NZ,.TILECOLOR
	LD	A,E
	RLA
	RLA
	RLA
	RLA
	OR	E
	LD	(HL),A
.TILECOLOR

	; 終端X,Y
	POP	HL	; Y2
	POP	DE	; X2
	POP	BC	; Y1
	LD	A,L
	SUB	C
	; 塗り行数を保存	 ※必ずY2がY1より大きい必要があるので注意
	INC	A
	LD	(FILLDAT.LINECOUNT),A
	POP	HL	; X1
	PUSH	DE	; X2の位置を保存しておく
	PUSH	BC	; Y1の位置も保存しておく

	LD	D,B	; Y1の値をDEに入れて
	LD	E,C

	EX	DE,HL	; X,Yをひっくりかえし
	CALL	CALCVRAMADR	; VRAM位置を算出

	; 開始アドレスと塗りビットを保存
	LD	(FILLDAT.STARTADR),HL
	LD	A,B
	LD	(FILLDAT.STARTBIT),A
	EX	AF,AF'
	LD	(FILLDAT.STARTDIV8),A
	EX	AF,AF'

	POP	HL
	POP	DE
	; これでX2,Y1の位置が得られているので
	CALL	CALCVRAMADR	; VRAM位置を算出
	LD	A,B
	LD	(RIGHTBIT),A	; 終了ビット
	LD	BC,(FILLDAT.STARTADR)
	OR	A		; Carry Clear
	SBC	HL,BC
	; 横残りX/8数を保存
	LD	A,L
	LD	(PAINTREMAIN),A

	; LINECOUNT行ぶん塗ってやる(EVENODDが切り替わるのでタイル状になる)
	LD	A,(FILLDAT.LINECOUNT)
	LD	B,A
.FILLLOOP
	PUSH	BC
	LD	HL,(FILLDAT.STARTADR)
	LD	A,(FILLDAT.STARTBIT)
	LD	B,A
	EX	AF,AF'
	LD	A,(FILLDAT.STARTDIV8)
	EX	AF,AF'
	CALL	SETUPPAINT
	CALL	DRAWLINE
	POP	BC
	DEC	B
	JR	Z,.FILLEND
	LD	HL,(FILLDAT.STARTADR)
	CALL	VRAMDOWN
	JR	C,.FILLEND
	LD	(FILLDAT.STARTADR),HL
	JR	.FILLLOOP
.FILLEND

	LD HL,(FILLDAT.RETADR)
	PUSH HL
	RET


FILLDAT:
; 塗り開始アドレス
.STARTADR
	DW	0
; 塗り開始ビット位置
.STARTBIT
	DB	0
.STARTDIV8
	DB	0
.LINECOUNT
	DB	0
.RETADR
	DW	0

#ENDLIB
