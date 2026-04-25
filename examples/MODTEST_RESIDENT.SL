/*
  #MODULE RESIDENT のデモ

  MODTEST.SL とソースはほぼ同一だが、#MODULE に RESIDENT ポリシーを付与する。
  PR-C1 で runtime ライブラリに @resident shared/local を付与済みのため、
  overlay 内の MPRNT/P10/PCRONE 等は overlay 本体に展開されず main 側
  シンボルへの EXTERN 参照に変換される (= overlay バイナリが大幅縮小)。

  サイズ実測 (slangbuild -E lsx 時):
    examples/MODTEST.SL          --> overlay 248 byte
    examples/MODTEST_RESIDENT.SL --> overlay  57 byte (-77%)

  注意: 本サンプルは loader を持たないため、main 起動後に overlay を
  $3000 にロードする仕組みは別途必要 (例: ディスクからの読み込み)。
  本ファイルはコンパイラ出力 + バイナリサイズ確認用のデモ。
*/

VAR MAIN_VAL=10;

MAIN()
{
	PRINT("This is main.",/);
	PRINT(MAIN_VAL, /);
	MODFUNC();
}

#MODULE $3000 RESIDENT

VAR MVAL;

MODFUNC()
{
	MVAL = 100;
	PRINT("Module value:", MVAL, /);
	PRINT("Main value:", MAIN_VAL, /);
}

#END
