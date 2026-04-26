/*
  cpm 環境 file I/O 検証 sample (libcpm_file scope B 用)

  Test 1: FPUTC で 200 byte 書く
    - I=128 で auto-flush 1 record
    - FCLOSE で残り 72 byte が dirty として flush
    - 残り 56 byte は ACTBUF の 0 padding (= stale data 漏れなし)
  Test 2: 同 fnum を reopen し FGETC で 200 byte 読む
    - read mode で再 open (write→read 切替は close 経由のみ)
    - 期待: 0..199 の値を順に読み出せる
  Test 3: FREAD multi-record (record-aligned 動作確認、256B = 2 records)
    - 期待: 256 が返る
  Test 4: FREAD partial (200B = 1.56 records)
    - 期待: 128 が返る (= floor(200/128) * 128)

  ★ 整合仕様準拠: 各テスト間で必ず FCLOSE → FOPEN している
    (= 同一 open file 内で FGETC/FPUTC と FREAD/FWRITE/FSEEK の混在は
    未サポート、reopen で切替)。
*/

VAR I, READ_VAL, READ_COUNT;

MAIN()
{
	/* === Test 1: FPUTC × 200 + close で flush === */
	FOPEN(0, "TEST.DAT", 3);   /* mode 3 = create */
	FOR I=0 TO 199 BEGIN
		FPUTC(0, I & $FF);
	END;
	FCLOSE(0);

	/* === Test 2: FGETC × 200 で同じ値が読める === */
	FOPEN(0, "TEST.DAT", 0);
	FOR I=0 TO 199 BEGIN
		READ_VAL = FGETC(0);
		IF READ_VAL != (I & $FF) THEN BEGIN
			PRINT("FGETC mismatch at ", I, ": got ", READ_VAL, /);
			FCLOSE(0);
			RETURN;
		END;
	END;
	FCLOSE(0);
	PRINT("FGETC/FPUTC OK", /);

	/* === Test 3: FREAD multi-record (256 byte = 2 records) === */
	FOPEN(0, "TEST.DAT", 0);
	READ_COUNT = FREAD(0, $5000, 256);
	PRINT("FREAD(256) returned ", READ_COUNT, /);  /* expected: 256 */
	FCLOSE(0);

	/* === Test 4: FREAD partial (200B = floor → 128) === */
	FOPEN(0, "TEST.DAT", 0);
	READ_COUNT = FREAD(0, $5100, 200);
	PRINT("FREAD(200) returned ", READ_COUNT, /);  /* expected: 128 */
	FCLOSE(0);
}
