/*
ファイル入出力ライブラリのテスト
(fopen/fread/fwrite/fclose)

使い方:
slcopy コピー元ファイル名 コピー先ファイル名

*/
ORG	$100

array byte buf[32768];

main ()
	var	n;
{
	n = $81;
	while (MEM[n] == $20) {
		++n;
	}
	if (fopen(0, n, 0) != 0) {
		return;
	}
	while (MEM[n] > $20) {
		++n;
	}
	if (fopen(1, n, 3) != 0) {
		return;
	}
	while (1) {
		n = fread(0, buf, 32768);
		if (n == -1 || n == 0) {
			exit;
		}
		if (fwrite(1, buf, n) != 0) {
			print("Write error",/);
			exit;
		}
	}
	fclose(1);
	fclose(0);
}
