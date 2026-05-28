/*
  #MODULE RESIDENT のデモ + ディスクからの overlay 読み込み実機サンプル

  PR-C1 で runtime ライブラリに @resident shared/local を付与済みのため、
  #MODULE に RESIDENT ポリシーを付けると overlay 内の MPRNT/P10/PCRONE 等
  は overlay 本体に展開されず main 側シンボルへの EXTERN 参照に変換される
  (= overlay バイナリが大幅縮小)。

  サイズ実測 (slangbuild -E lsx 時):
    examples/MODTEST.SL          --> overlay 248 byte (Local モード)
    examples/MODTEST_RESIDENT.SL --> overlay  57 byte (RESIDENT モード, -77%)

  本サンプルはさらに、disk image に書き込まれた M0.BIN (= overlay バイナリ)
  を OVERLAY_ADDR にロードしてから MODFUNC を呼び出す最小 loader を持つ。

  ★ 動作確認済み環境:
    - lsx / x1: LSX-Dodgers d88 上で実機エミュレータ動作
    - cpm:      RunCPM 上で動作 (libcpm_file による CP/M 2.2 互換 FREAD 経由)

  ★ 本 loader の制約: overlay 0 が「128 byte (= CP/M 1 record) 以内に収まる」
  ことを前提にした最小実装。サンプルを拡張して 128 byte を超える overlay に
  なる場合は、loader 側の FREAD 第 3 引数を増やすか、ループでマルチ record
  読みに変えること (libcpm_file 側も scope B で multi-record 化が必要)。

  Makefile.dist の disk_image ターゲットは PROG._m0.bin を staging 経由で
  M0.BIN として d88 (lsx/x1) に書き込む。tools/runcpm.sh も同等の staging を
  行い、make ENV=cpm run で end-to-end 動作する。
*/

CONST OVERLAY_ADDR = $3000;   /* loader と #MODULE で共有するロードアドレス */

VAR MAIN_VAL = 10;
VAR FERR;

MAIN()
{
	PRINT("This is main.", /);
	PRINT(MAIN_VAL, /);

	/* Overlay 0 を M0.BIN から OVERLAY_ADDR にロード */
	FERR = FOPEN(0, "M0.BIN", 0);     /* fnum=0, mode=read (0..2=read) */
	/* FOPEN 戻り値: 0..3 = 成功 (directory code), 255 = ファイルなし */
	IF FERR == 255 THEN BEGIN
		PRINT("Open failed: ", FERR, /);
		RETURN;
	END;

	FREAD(0, OVERLAY_ADDR, 128);       /* 1 record (128B) で十分 */
	FCLOSE(0);

	MODFUNC();
}

#MODULE OVERLAY_ADDR RESIDENT

VAR MVAL;

MODFUNC()
{
	MVAL = 100;
	PRINT("Module value:", MVAL, /);
	PRINT("Main value:", MAIN_VAL, /);
}

#END
