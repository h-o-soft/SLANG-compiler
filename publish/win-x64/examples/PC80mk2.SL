// SLANG PC-8001mk2 test
ORG $C000

VAR SUJI,C;
VAR K,X,Y;

ARRAY BYTE MUSADR[]:SOUNDDATA;
ARRAY BYTE SEADR[]:SEDATA;

CONST ATRD_DECOLAT	=		00000000b,
ATRC_COLOR		=		00001000b,
ATRC_BLACK		=		00001000b,
ATRC_BLUE		=		00101000b,
ATRC_RED		=		01001000b,
ATRC_PURPLE		=		01101000b,
ATRC_GREEN		=		10001000b,
ATRC_CYAN		=		10101000b,
ATRC_YELLOW		=		11001000b,
ATRC_WHITE		=		11101000b,
ATRC_SEMIG		=		00011000b,
ATRC_CHR		=		00001000b,
ATRD_DLINE		=		00100000b,
ATRD_ULINE		=		00100000b,
ATRD_REVSECa	=		00000111b,
ATRD_REVBLK		=		00000110b,
ATRD_REVSEC		=		00000101b,
ATRD_REV		=		00000100b,
ATRD_SECa		=		00000011b,
ATRD_BLK		=		00000010b,
ATRD_SEC		=		00000001b,
ATRD_NOR		=		00000000b;

CONST PCGPTN=[
  00010000b,
  00010000b,
  00111000b,
  11111110b,
  00111000b,
  00010000b,
  00010000b,
  00000000b
];

MAIN()
{
  CMDSCREEN(0,1,7);
  // ファンクション非表示、カラーモード
  TEXTMODE(0,1);
  WIDTH(40);

  // 「ﾁ」(65+128)の定義
  PCGDEF(65, PCGPTN);
  // PCGDEF2だと前半の128文字の定義になるのでこれは「A」の定義
  PCGDEF2(65, PCGPTN);

  LOCATE(0,10);
  PRINT("ﾁﾁﾁ",/,"ﾁﾁﾁ",/,"ﾁﾁﾁ",/);
  PRINT("AAA",/,"AAA",/,"AAA",/);

  // テキストアトリビュート設定の例
  // 3行目リセット
  SETATR(3,0x80,ATRC_RED);
  // 3行目2文字目からを赤くする
  SETATR(3,2,ATRC_RED);
  // 12文字目(WIDTH40の場合は6文字目)で白に戻す
  SETATR(3,12,ATRC_WHITE);
  // 4行目10文字目以降を青とする
  SETATR(4,10,ATRC_BLUE);

  // 漢字表示テスト
  KANJILOCATE(8,0);
  // GVRAM転送時はSETGVRAM(1)を呼ぶ(0x8000～がVRAMになる)
  SETGVRAM(1);
  KANJIPUT("ＰＣ８００１ｍｋＩＩ各種テスト");
  // GVRAM戻す
  SETGVRAM(0);

  // BGM再生開始
  SND_INIT();
  SND_PLAY(MUSADR);

  X=0;
  Y=0;
  SUJI=0;
  LOOP
  {
    // ESCキーで終了
    IF NOT(KEYCHK(9) AND 0x80) THEN EXIT;
    // 終わってたら鳴らしなおし
    IF NOT SND_ISPLAYING() THEN SND_PLAY(MUSADR);

    K=STICK2();
    IF K AND 1 THEN Y=Y-1;
    IF K AND 2 THEN X=X+1;
    IF K AND 4 THEN Y=Y+1;
    IF K AND 8 THEN X=X-1;
    IF X.<.0 THEN X=0; ELIF X>39 THEN X=39;
    IF  Y.<.0 THEN Y=0; ELIF Y>24 THEN Y=24; 
    IF STRIG(1) THEN {
      // BEEP(1)でBEEP発音開始、BEEP(0)で発音終了(ある程度時間を空けないと音は聞こえない)
      // BEEP(1);
      MEMSET(0xf300,0,120*25); X=0; Y=0;

      // SE再生
      SND_SEPLAY(SEADR);

      SETATR(3,2,ATRC_RED);
      SETATR(3,12,ATRC_WHITE);
      // BEEP(0);
    }
    // カタカナの「チ」をテキストVRAMに書く
    MEM[0xf300 + X*2 + Y * 120]=65+128;

    // 時前でVSYNCを待ちたい場合は、SND_PROCを直後に呼ぶ事
    VSYNC();
    SND_PROC();

    // フルパワーで動かす場合はSND_SYNCを各所で呼ぶ事(そうする事でテンポが一定で鳴ります)
    // SND_SYNC();
  }
  SND_STOP();

  PRINT("END",/);
}


#ASM
;-----------------------------------------------------------------------
; 音階定義
;
TONE:
.C	equ	0
.CP	equ	1
.D	equ	2
.DP	equ	3
.E	equ	4
.F	equ	5
.FP	equ	6
.G	equ	7
.GP	equ	8
.A	equ	9
.AP	equ	10
.B	equ	11

.O1	equ	(0 * 12)
.O2	equ	(1 * 12)
.O3	equ	(2 * 12)
.O4	equ	(3 * 12)
.O5	equ	(4 * 12)
.O6	equ	(5 * 12)

;-----------------------------------------------------------------------
; サンプルデータ
;
SOUNDDATA:
BGM:	dw	.@1, .@2, .@3
.@1	db	-16, TONE.O4 + TONE.G
	db	TONE.O4 + TONE.E
	db	-32, TONE.O4 + TONE.E	; 64 ちょうちょ～

	db	-16, TONE.O4 + TONE.G
	db	TONE.O4 + TONE.E
	db	-32, TONE.O4 + TONE.E	; 64 ちょうちょ～

	db	-16, TONE.O4 + TONE.C
	db	TONE.O4 + TONE.D
	db	TONE.O4 + TONE.E
	db	TONE.O4 + TONE.F	; 64 なのはに

	db	-16, TONE.O4 + TONE.G
	db	-16, TONE.O4 + TONE.G
	db	-32, TONE.O4 + TONE.G	; 64 とまれ～

	db	0x80

.@2	db	-8
	db	TONE.O2 + TONE.D
	db	TONE.O2 + TONE.D
	db	TONE.O2 + TONE.D
	db	TONE.O2 + TONE.D
	db	TONE.O2 + TONE.D
	db	TONE.O2 + TONE.D
	db	TONE.O2 + TONE.D
	db	TONE.O2 + TONE.D	; 64

	db	TONE.O2 + TONE.F
	db	TONE.O2 + TONE.F
	db	TONE.O2 + TONE.F
	db	TONE.O2 + TONE.F
	db	TONE.O2 + TONE.F
	db	TONE.O2 + TONE.F
	db	TONE.O2 + TONE.F
	db	TONE.O2 + TONE.F	; 64

	db	TONE.O2 + TONE.G
	db	TONE.O2 + TONE.G
	db	TONE.O2 + TONE.G
	db	TONE.O2 + TONE.G
	db	TONE.O2 + TONE.G
	db	TONE.O2 + TONE.G
	db	TONE.O2 + TONE.G
	db	TONE.O2 + TONE.G	; 64

	db	TONE.O2 + TONE.F
	db	TONE.O2 + TONE.F
	db	TONE.O2 + TONE.F
	db	TONE.O2 + TONE.F
	db	TONE.O2 + TONE.F
	db	TONE.O2 + TONE.F
	db	TONE.O2 + TONE.F
	db	TONE.O2 + TONE.F	; 64

	db	0x80

.@3	db	-16, TONE.O5 + TONE.G
	db	TONE.O5 + TONE.E
	db	-32, TONE.O5 + TONE.E

	db	-16, TONE.O5 + TONE.G
	db	TONE.O5 + TONE.E
	db	-32, TONE.O5 + TONE.E

	db	-16, TONE.O5 + TONE.C
	db	TONE.O5 + TONE.D
	db	TONE.O5 + TONE.E
	db	TONE.O5 + TONE.F

	db	-16, TONE.O5 + TONE.G
	db	-16, TONE.O5 + TONE.G
	db	-32, TONE.O5 + TONE.G

	db	0x80

; o4l48dg>cfb>e
SEDATA:
SE:	db	0
	db	TONE.O4 + TONE.D
	db	TONE.O4 + TONE.G
	db	TONE.O3 + TONE.C
	db	TONE.O3 + TONE.F
	db	TONE.O3 + TONE.B
	db	TONE.O2 + TONE.E
	db	0x80
#END

