// SLANG PC-8001mk2 test
VAR SUJI,C;

ARRAY BYTE MUSADR[]:SOUNDDATA;
//ARRAY BYTE MUSADR[]:BGMGame;
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

// #MODULE $1000
// TESTFNC(A,B)
// {
//   RETURN A*B;
// }
// #END
ORG $C000

ARRAY BYTE MOJIBUF[2];
VAR K,X,Y;

TEXTVIEW(FNAME)
VAR TCH,X,Y;
{
  SD_ROPEN(FNAME);
  X=0;
  Y=0;
  LOOP
  {
    TCH = SD_FGET();
    // PRINT(TCH,/);
    IF TCH == 0xFF THEN EXIT;
    IF((TCH >= 0x81 && TCH <= 0x9F) || (TCH >= 0xE0 && TCH <= 0xEF)) THEN
    {
      MOJIBUF[0] = TCH;
      MOJIBUF[1] = SD_FGET();
      MOJIBUF[2] = 0;
      SETGVRAM(1);
      KANJIPUT(MOJIBUF);
      SETGVRAM(0);
      X = X + 2;
    }
    ELSE IF (TCH == 0x0d || TCH == 0x0a) THEN
    {
      X=0;
      Y=Y+2; // 1;
      KANJILOCATE(X,Y);
    }
    ELSE IF (TCH == 0x09) THEN
    {
      X=X+4;
      KANJILOCATE(X,Y);
    }
    ELSE
    {
      MOJIBUF[0] = TCH;
      MOJIBUF[1] = 0;
      SETGVRAM(1);
      KANJIPUT(MOJIBUF);
      SETGVRAM(0);
      X = X + 1;
    }
    IF X >= 80 THEN {
      X=0;
      Y=Y+2; // 1;
    }
    //IF Y > 24 THEN
    //{
    //  SETGVRAM(1);
    //  // 一行スクロール
    //  MEMCPY(0x8000, 0x8000 + 80*8*1, 80 * 8 * 24);
    //  // 最終行は消す
    //  MEMSET(0x8000+80*8*24, 0, 80*8*1);
    //  SETGVRAM(0);
    //  Y=24;
    //}
    IF Y > 23 THEN
    {
      SETGVRAM(1);
      // 一行スクロール
      MEMCPY(0x8000, 0x8000 + 80*8*2, 80 * 8 * 23);
      // 最終行は消す
      MEMSET(0x8000+80*8*23, 0, 80*8*2);
      SETGVRAM(0);
      Y=23;
    }
    KANJILOCATE(X,Y);
  }

  SETGVRAM(0);
}

CONST PCGPTN=[
  00010000b,
  00010000b,
  00111000b,
  11111110b,
  00111000b,
  00010000b,
  00010000b,
  00000000b

//  01101100b,
//  11111110b,
//  11111110b,
//  11111110b,
//  01111100b,
//  00111000b,
//  00010000b,
//  00000000b
];


MAIN()
{
  //DUMMYFUNC();

  // ファンクション非表示、カラーモード
  TEXTMODE(0,1);
  WIDTH(40);

  PCGDEF(65, PCGPTN);
  PCGDEF2(65, PCGPTN);

  PRINT("ﾁﾁﾁ",/,"ﾁﾁﾁ",/,"ﾁﾁﾁ",/);
  PRINT("AAA",/,"AAA",/,"AAA",/);

  SETATR(3,0x80,ATRC_RED);
  SETATR(3,2,ATRC_RED);
  SETATR(3,12,ATRC_WHITE);
  SETATR(4,10,ATRC_BLUE);

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
      BEEP(1);
      MEMSET(0xf300,0,120*25); X=0; Y=0;
      // SND_SEPLAY(SEADR);
      SETATR(3,2,ATRC_RED);
      SETATR(3,12,ATRC_WHITE);
      BEEP(0);
    }
    MEM[0xf300 + X*2 + Y * 120]=65+128;
    VSYNC();
    SND_PROC();
  }

  PRINT("END",/);
}

DUMMYFUNC()
{
  CMDSCREEN(1,1,7);
  
//  SETGVRAM(1);
//
//  KANJILOCATE(1,3);
//  KANJIPUT("漢字ですよ。\x01ABC\n\n　大いなる二つの意識。いったいいつの頃から存在しているのかわからないが、\n二つの意識があった。\n\n表示出来てますね。");
//
//  KANJIPUT("追記するとどこに描かれるんだろう。画面外にハミ出すと改行するのかどうかを確認してみるか。どうなりますかねこれ");
//  SETGVRAM(0);
//
  FOR SUJI=0 TO 500000 { }
//
//  //FUNCTEST();

  KANJILOCATE(0,0);
  TEXTVIEW("KANJI.TXT");

  PRINT("PC-8001mkII module test",/);

//  // SD-Card File Open
//  SD_ROPEN("msg.bin");
//
//  LOOP {
//    C = SD_FGET();
//    IF C == 0 THEN EXIT;
//    PRINT(CHR$(C));
//  }
//  PRINT(/);
//
//  // ROM READ / RAM WRITE
//  MEMMODE(0,1);
//
//  ////////////////////
//  //// LOAD BINARY
//  // for CMT
//  // LOADCMT();
//  // for SD
//  SD_ROPEN("mod0.cmt");
//  SD_RREAD();
//
//  // RAM READ / RAM WRITE
//  MEMMODE(1,1);
//  SUJI = TESTFNC(15,5);
//
//  // ROM READ / ROM WRITE
//  MEMMODE(0,0);
//
//  PRINT(SUJI);
}



/*
上記定義により、

ORG $1000,$00000
MODULE0_START:
// TESTFNCのコード
MODULE0_END:

ORG $1000,$10000
MODULE1_START:
// TESTFNC2のコード
MODULE1_END:

ORG $C000,$20000
// MAIN以降のコード

という出力がされる。
また、moduleinfo.yml に、下記出力がされる

module0: $1000,(MOUDLE1のサイズ)
module1: $1000,(MOUDLE2のサイズ)
main: $c000

(しなくてもいいかも？symかlstから得た上で処理すれば十分な気がする)

これにより最終的に、

PC80mk2SAMPLE.bin (全てがまとまったバイナリ)

(↑上記からいい具合に分離する)
PC80mk2SAMPLE.module0.bin
PC80mk2SAMPLE.module1.bin
PC80mk2SAMPLE.main.bin

(どう作ろうかなあ……)
PC80mk2SAMPLE.main.cmt

の4ファイルが出力される。
(cmtはbinから生成するため、AILZ80ASMには-cmtオプションはつけない)
*/

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

BGMGame:        ; Tempo = 8 (48)
	DB	.@1 - BGMGame, .@2 - BGMGame - 1, .@3 - BGMGame - 2, 
.length	DB	$04, $08, $0C, $1C, $01, $03, 
.@1	DB	$E1, $0C, $06, $00, $06, $C2, $58, $06, 

	DB	$06, $18, $43, $18, $18, $03, $F6, $16, 

	DB	$41, $01, $01, $0E, $21, $00, $32, $01, 

	DB	$2E, $00, $2E, $00, $32, $00, $2E, $00, 

	DB	$61, $20, $06, $00, $06, $C0, $21, $85, 

	DB	$00, $84, $11, $F4, $48, $41, $2E, $01, 

	DB	$52, $41, $82, $23, $3B, $80, $23, $68, 

	DB	$20, $28, $08, $48, $08, $08, $08, $08, 

	DB	$20, $4E, $51, $11, $11, $11, $11, $0E, 

	DB	$01, $12, $03, $80, $26, $43, $03, $03, 

	DB	$03, $03, $20, $52, $41, $01, $01, $01, 

	DB	$01, $20, $FF, 
.@2	DB	$F2, $40, $C2, $14, $13, $14, $34, $13, 

	DB	$14, $19, $80, $2D, $34, $13, $13, $19, 

	DB	$10, $0A, $10, $19, $80, $2D, $14, $13, 

	DB	$34, $80, $2D, $30, $00, $30, $00, $F6, 

	DB	$0F, $30, $00, $30, $00, $04, $19, $04, 

	DB	$0B, $04, $19, $34, $00, $24, $00, $C0, 

	DB	$24, $00, $2B, $00, $0A, $00, $0C, $06, 

	DB	$06, $06, $06, $0A, $10, $14, $0A, $06, 

	DB	$42, $02, $02, $02, $02, $20, $10, $0A, 

	DB	$06, $4C, $0C, $0C, $0C, $0C, $20, $0B, 

	DB	$10, $80, $2D, $44, $04, $04, $04, $04, 

	DB	$20, $44, $4B, $0B, $0B, $0B, $0B, $20, 

	DB	$FF, 
.@3	DB	$47, $C2, $29, $00, $29, $00, $3A, $00, 

	DB	$3A, $00, $81, $20, $00, $81, $20, $00, 

	DB	$27, $00, $27, $00, $F6, $15, $3D, $00, 

	DB	$81, $20, $00, $29, $00, $81, $22, $00, 

	DB	$34, $00, $81, $22, $00, $33, $00, $81, 

	DB	$2C, $00, $C0, $81, $22, $00, $29, $00, 

	DB	$34, $00, $81, $2C, $00, $C2, $29, $00, 

	DB	$27, $00, $C0, $81, $20, $00, $29, $00, 

	DB	$33, $00, $29, $00, $3A, $00, $81, $2C, 

	DB	$00, $81, $22, $00, $39, $00, $CA, $34, 

	DB	$00, $C0, $14, $80, $2C, $17, $FF, 





#END

