/*

ファイル入出力ライブラリのテスト
(fopen/fgetc/fclose)

使い方:
sltype ファイル名

*/
ORG	$100

main ()
var	c;
{
	if (fopen(0, $81, 0) != 0) {
		return;
	}
	while (1) {
		c = fgetc(0);
		if (c > $ff OR c == $1a) {
			exit;
		}
		^DE = c;
		^BC = 2;					//コンソール出力(_CONOUT)
		CALL(5);
	}
	fclose(0);
}
