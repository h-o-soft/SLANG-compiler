
// ULAスクリーンのベースアドレス
CONST SCREEN_BASE = $4000;

// SEEKで指定するファイル位置(4バイト)
ARRAY WORD SEEKADR[2-1];
// FSTATで取得するファイル情報(11バイト)
ARRAY BYTE STATADR[11-1];

// 汎用バッファ
ARRAY BYTE DATABUF[128];

// 汎用アドレス値
VAR WORD ADR;

VAR FLOAT FX, FLOAT FY;
VAR X,Y;
VAR TX,TY;
VAR SX,SY;
VAR VSY;
VAR TMP;
VAR SADR;

// タイル、タイルマップ、パレットのアドレス
// アプリ埋め込みしているが、ファイルあるいはメモリバンクからそれらしく読み込んだ方が良いと思われます
CONST TILEADR = TILES;
CONST TILEMAPADR = TILE_MAP;
CONST PALADR = TILE_PAL;

// Hardware IM2での割り込みテーブル
CONST INTTABLE = InterrultVectorTable;

// Copperコード
CONST CPCODE = {
	$80, 0,
	$41, 00011100b,	// 0〜100ドット目までは緑
	$80, 100,
	$41, 11100000b,	// それ以降は赤
	$FF, $FF
};


// 割り込み中にカウントアップされる変数
VAR BYTE BCOUNTER:BYTECOUNTER;

// ファイルライブラリの読み込み
#include "ZXNFILE.SL"


main()
VAR I, J, F;
{
	// 28MHzに切り替え
	SET_CPU_SPEED(3);
	// 最初の16kをROMにリセット
	// (ファイルライブラリはこうしておかないと動かない)
	ZXN_BANK_SET_ESX();

	// $4000〜$5FFFをRAMの10バンクに切り替え(デフォルトのULA)
	ZXN_SET_BANK_8K(2, 10);

	// ULAスクリーンクリア
	ADR = $5800;
	FOR I = 0 TO $2FF
	{
		MEM[ADR] = $41;
		ADR++;
	}
	ADR = SCREEN_BASE;
	FOR I = 0 TO 192*32-1
	{
		MEM[ADR] = 0;
		ADR++;
	}

	// ULAシャドウスクリーンもクリアしておく
	ZXN_SET_BANK_8K(2, 14);

	ADR = $5800;
	FOR I = 0 TO $2FF
	{
		MEM[ADR] = $41;
		ADR++;
	}
	ADR = SCREEN_BASE;
	FOR I = 0 TO 192*32-1
	{
		MEM[ADR] = 0;
		ADR++;
	}

	// 2バンク目を通常のULAに戻しておく
	ZXN_SET_BANK_8K(2, 10);

	// 外部ファイルから19文字読み込んで表示
	F = FOPEN("test.txt", ESX_MODE_OPEN_EXIST OR ESX_MODE_READ);
	FREAD(F, DATABUF, 19);
	DATABUF[20] = 0;
	FCLOSE(F);
	LOCATE(2,20);
	PRINT(MSX$(DATABUF), /);

	// 書き込む時はこんな感じ(ただしエミュでは正常に動かない。実機でもファイルサイズが怪しいので、あまり書き込みは使わない方がいいかも)
	//I = FOPEN("test2.txt", ESX_MODE_OPEN_CREAT OR ESX_MODE_CREAT_TRUNC OR ESX_MODE_WRITE);
	//FWRITE(I, "Hello World!", 12);
	//FCLOSE(I);

	// ファイルシークはこんな感じ(4バイトの値を渡す必要があるので4バイト配列のアドレスを渡す必要がある)
	// SEEKADR[0] = 0;
	// SEEKADR[1] = 0;
	// FSEEK(I, SEEKADR, ESX_SEEK_SET);

	// FSTATで情報を取得し、ファイルサイズの下位2バイトを取得して表示
	// F = FOPEN("test.txt", ESX_MODE_OPEN_EXIST OR ESX_MODE_READ);
	// FSTAT(F, STATADR);
	// FCLOSE(F);
	// I = STATADR[7] + (STATADR[8] << 8);
	// PRINT(I);

	// 普通にLOCATEとPRINTで文字が表示可能
	LOCATE(7,3);
	PRINT("ZX Spectrum Next!",/,/);

	// もちろん浮動小数点も使える(精度は低い)
	FX = 1.23;
	FY = 2.34;
	FX = FX * FY;
	PRINT("Float Value:", FL$(FX),/);

	// パレット設定
	// SET_PAL(pal, index, color)
	// pal
	//   0 : ULA first palette
	//   4 : ULA second palette
	//   1 : Layer 2 first palette
	//   5 : Layer 2 second palette
	//   2 : Sprite first palette
	//   6 : Sprite second palette
	//   3 : Tilemap first palette
	//   7 : Tilemap second palette
	// index
	//   インデックス
	// color
	//   色値
	SET_PAL(0, 9, 3);

	// ULAシャドウスクリーンに文字を書き込む
	ZXN_SET_BANK_8K(2, 14);

	I = ZXN_READ_REG($52);
	PRINT("BANK 2:", I,/);

	ZXN_SET_BANK_8K(2, 10);

	// コメントを外すとULAシャドウスクリーン側が表示される
	// ZXN_ULA_SET_SHADOW(1);
	PRINT("",/);

	ZXN_SET_BANK_8K(2, 10);
	PRINT("",/);

	// Layer 2を表示
	L2_SCREEN(0);	// 0 = 256x192 / 1 = 320x256 / 2 = 640x256(4bpp)
	L2_SETRAM(22);	// 16KBの22番バンク(8KBの44番バンク)をLayer 2の開始バンクとして設定
	L2_CLIPWINDOW(0,180,0,192);	// クリッピングする
	L2_VISIBLE(1);	// Layer 2を描画する


	// 各レイヤーの優先順位設定を行う
	// 0 S L U
	// 1 L S U
	// 2 S U L
	// 3 L U S
	// 4 U S L
	// 5 U L S
	// 6 (U|T)S(T|U)(B+L)
	// 7 (U|T)S(T|U)(B+L-5)
	LAYER_PRIORITY(6);

	// ULA(本サンプルだと文字などを表示している画面)を表示する
	ULA_VISIBLE(1);

	// タイルマップ関連

	// タイルマップ初期化
	// 0 = 40x32 / 1 = 80x32
	// 1 = 1byte / 0 = 2byte
	TILE_INIT(0, 1);
	// タイルマップ表示有効
	TILE_VISIBLE(1);

	// タイルマップのグローバルアトリビュートを設定
	TILE_GLOBALATR($00);

	// タイルマップとタイル(画像)のアドレスを設定する
	// $20 → $2000 + $4000 → $6000
	// $26 → $2600 + $4000 → $6600
	// 上記のように$6000と$6600が正式なアドレスになる
	// 事前にアドレスの上位バイトから$40を引いた値を入れてやる必要がある
	TILE_SETADR($20, $26);

	// タイルマップのパレットを設定する
	//   3 → Tilemap first palette
	SET_PAL9ALL(3, PALADR);
	// タイルマップの0番の色を透明色として設定する
	ZXN_WRITE_REG($4C,$00);

	// タイルマップクリッピング設定
	TILE_CLIP(4,155,0,32*6);
	// タイルマップ左上位置設定
	TILE_OFFSET(0,0);

	PRINT("Red",/);

	// // タイル画像を1つずつ定義
	// FOR I = 0  TO 100
	// {
	// 	TILE_DEF(I, TILEADR+32);
	// }
	// タイル画像をまとめて定義
	TILE_DEFS(0,TILEADR,64);

	// タイルマップを設定
	SADR = TILEMAPADR;
	FOR Y = 0 TO 25-1
	{
		FOR X = 0 TO 40-1
		{
			// 滅茶苦茶遅いので注意
			// 通常はもう少し効率のいい方法でメモリに直接書いた方がいい
			// (下記メソッドは内部で横40 or 80、タイルの1byte、2byteを計算して、適切な位置に書いているが、
			//  通常ゲーム内では切り替えないと思うので、事前に計算出来るはず)
			TILE_SETMAP(X,Y,MEM[SADR]);
			SADR = SADR + 1;
		}
		// SADR = SADR + 256 - 40;
	}

	// スプライト関連
	// 8KBバンク2番($4000〜$7FFF)を8KBの38番(というか16KBバンクの19番)に切り替える
	ZXN_SET_BANK_16K(2, 38/2);
	// $4000に現れているスプライトデータをDMAで転送
	// SPR_LOAD(index, address, size)
	SPR_LOAD(0, $4000, 16384);
	// 16KBバンクを元に戻す
	ZXN_SET_BANK_16K(2, 5);

	// スプライトを表示する
	SPR_VISIBLE(1);
	ZXN_WRITE_REG($4B, 0);

	// 0番スプライトを設定
	SPR_SET(0,100,80,0 OR $4000);	// pattern 4
	// SPR_SCALE(0,0,0);
	// 0番スプライトをアンカーとする
	SPR_STARTANCHOR(0);
	// 0番スプライトの右下と真下に1つずつRelativeスプライトを追加(0番を動かすと勝手についてくる)
	SPR_SETREL(8,8,0);
	SPR_SETREL(0,24,0);

	// 10番スプライトを表示。横4倍、縦2倍
	SPR_SET(10,180,80,$40 OR 0);
	SPR_SCALE(10,2,1);
	// 非表示にする場合はこちら
	// SPR_HIDE(10);

	// スプライトのクリッピング
	SPR_CLIP(0,255, 0, 192);


	// Copperの実験
	// 99ライン目まで緑、11ライン以降は赤で文字が表示される(文字の途中で色が変わっている事が確認出来る)
	PRINT("Copper Test!",/);
	PRINT("Copper 0-99 : Green",/);
	PRINT("       100- : Red",/);
	PRINT("Red....",/);
	COPPER_SET(CPCODE, 10);
	// ↑COPPERでパレット設定する場合パレットのオートインクリメントを止めておく必要があるので、
	// ↓これでULA first paletteのオートインクリメントを止める
	SET_PAL(8, 9, 2);

	X = 0;
	Y = 10;
	TX = 0;
	TY = 0;
	SX = 80;
	SY = 120;
	VSY = -8;

	// ULA Control の Cancel entries in 8x5 matrix for extended keysを寝かせる
	// (よくわかってないが多分、extended keysを有効にする？)
	ZXN_WRITE_REG($68, (ZXN_READ_REG($68) AND $EF));

	// Hardware IM2の設定
	// 第二引数下位: INT and ULA Interrupt
	// 第二引数上位: CTC channel interrupts
	// 第三引数: UART interrupts
	ZXN_SETIM2(INTTABLE, $81, $00);	// INT and ULA Interruptを有効化

	loop
	{
		// ジョイスティック情報取得
		TMP = STICK(0); // PORT[$1F];
		IF TMP AND 2 THEN SX = SX - 1;
		IF TMP AND 1 THEN SX = SX + 1;
		IF TMP AND 4 THEN SY = SY + 1;
		IF TMP AND 8 THEN SY = SY - 1;
		// SE再生は無効化（NextDAW未使用のため）
		SX=SX+1;

		// BCOUNTERはIM2割り込み中にカウントアップされる
		LOCATE(10,23);
		PRINT(BCOUNTER);

		// キーボード取得関連は
		// INKEY : SLANGとおおむね互換
		// GETC  : リアルタイムキー入力
		// GETKEY(M) : マトリックス番号Mのキー情報を取得(0〜7。8、9はExtended Keysを取得する)

		// スプライト動かす
		SPR_MOVE(0,SX,SY);
		SPR_MOVE(10,180+X,X);
		ZXN_VSYNC();

		// Layer 2とタイルマップのスクロール
		L2_OFFSET(X,Y);
		TILE_OFFSET(TX,TY);
		X++;
		Y=Y+2;
		IF Y > 192 THEN Y = 0;

		TX++;
		IF TX >= 320 THEN TX = 0;
	}
}

// CALL VSYNC_JP のタイミングで呼ばれるSLANG関数
// 音楽なしバージョンでは何もしない
VSYNC_PROC()
{
}

// 埋め込みアセンブラコード
#ASM

; タイル関連のデータ
TILE_PAL:
include "block.nxp",B

TILE_MAP:
DB 1,2,1,2,1,2,1,2,1,2,1,2,1,2,1,2,1,2,1,2,1,2,1,2,1,2,1,2,1,2,1,2,1,2,1,2,1,2,1,2
DB 3,4,3,4,3,4,3,4,3,4,3,4,3,4,3,4,3,4,3,4,3,4,3,4,3,4,3,4,3,4,3,4,3,4,3,4,3,4,3,4
DB 1,2,0,0,0,0,0,0,0,0,0,0,5,6,0,0,0,0,0,0,0,0,5,6,0,0,5,6,0,0,0,0,0,0,0,0,0,0,1,2
DB 3,4,0,0,0,0,0,0,0,0,0,0,7,8,0,0,0,0,0,0,0,0,7,8,0,0,7,8,0,0,0,0,0,0,0,0,0,0,3,2
DB 1,2,1,2,1,2,0,0,0,0,1,2,1,2,1,2,1,2,1,2,1,2,1,2,1,2,1,2,1,2,1,2,1,2,1,2,1,2,1,2
DB 3,4,3,4,3,4,0,0,0,0,3,4,3,4,3,4,3,4,3,4,3,4,3,4,3,4,3,4,3,4,3,4,3,4,3,4,3,4,3,4
DB 1,2,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,1,2
DB 3,4,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,3,4
DB 1,2,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,1,2
DB 3,4,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,3,4
DB 1,2,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,1,2
DB 3,4,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,3,4
DB 1,2,0,0,0,0,0,0,0,0,0,0,0,0,5,6,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,1,2
DB 3,4,0,0,0,0,0,0,0,0,0,0,0,0,7,8,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,3,4
DB 1,2,0,0,0,0,0,0,0,0,0,0,0,0,1,2,1,2,1,2,1,2,1,2,0,0,0,0,0,0,0,0,0,0,0,0,0,0,1,4
DB 3,4,0,0,0,0,0,0,0,0,0,0,0,0,3,4,3,4,3,4,3,4,3,4,0,0,0,0,0,0,0,0,0,0,0,0,0,0,3,4
DB 1,2,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,5,6,0,0,0,0,1,2
DB 3,4,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,7,8,0,0,0,0,3,4
DB 1,2,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,1,2,1,2,1,2,1,2,1,2
DB 3,4,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,3,4,3,4,3,4,3,4,3,4
DB 1,2,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,1,2
DB 3,4,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,3,4
DB 1,2,1,2,1,2,1,2,1,2,1,2,1,2,1,2,1,2,1,2,1,2,1,2,1,2,1,2,1,2,1,2,1,2,1,2,1,2,1,2
DB 3,4,3,4,3,4,3,4,3,4,3,4,3,4,3,4,3,4,3,4,3,4,3,4,3,4,3,4,3,4,3,4,3,4,3,4,3,4,3,4

;include "lev1part1.map",B

TILES:
include "block.til",B

; IM2割り込みベクタ
ALIGN 32
InterrultVectorTable:
    DW InterruptHandler ; 0 = line interrupt
    DW InterruptHandler ; 1 = UART0 Rx
    DW InterruptHandler ; 2 = UART1 Rx
    DW InterruptHandler ; 3 = CTC channel 0
    DW InterruptHandler ; 4 = CTC channel 1
    DW InterruptHandler ; 5 = CTC channel 2
    DW InterruptHandler ; 6 = CTC channel 3
    DW InterruptHandler ; 7 = CTC channel 4
    DW InterruptHandler ; 8 = CTC channel 5
    DW InterruptHandler ; 9 = CTC channel 6
    DW InterruptHandler ; 10 = CTC channel 7
    DW InterruptHandlerULA  ; 11 = ULA
    DW InterruptHandler ; 12 = UART0 Tx
    DW InterruptHandler ; 13 = UART1 Tx
    DW InterruptHandler
    DW InterruptHandler

; 何もしない
InterruptHandler:
    EI
    RETI

; ULA割り込み
InterruptHandlerULA:
    PUSH AF
    ; BYTECOUNTERをカウントアップする
    LD A,(BYTECOUNTER)
    INC A
    LD (BYTECOUNTER),A
    POP AF
    PUSH AF
    PUSH HL
    PUSH DE
    PUSH BC
    PUSH IX
    PUSH IY
    ; このタイミングでVSYNC_PROCが呼ばれる
    CALL VSYNC_JP
    POP IY
    POP IX
    POP BC
    POP DE
    POP HL
    POP AF
    EI
    RETI

BYTECOUNTER:
    DB 0

#END
