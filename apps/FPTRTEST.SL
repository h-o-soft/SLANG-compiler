/* FLOAT ポインタ (間接変数) テスト */

ARRAY FLOAT BUF[5];     /* バッファ確保用 */
VAR FLOAT FP[];         /* 間接変数 (FLOAT ポインタ): VAR ... [] が間接変数構文 */
VAR FLOAT R;
VAR I;

LOCAL_TEST()
VAR FLOAT LP[];
ARRAY FLOAT LBUF[5];
{
  LP = &LBUF[0];
  LP[0] = 11.5;
  LP[2] = 22.5;
  PRINT("T7 LP[0]=", FL$(LP[0]), " LP[2]=", FL$(LP[2]), /);
}

MAIN()
{
  FP = &BUF[0];

  /* T1, T2: 定数インデックス store/load */
  FP[0] = 1.5;
  FP[1] = 2.5;
  PRINT("T1/T2 FP[0]=", FL$(FP[0]), " FP[1]=", FL$(FP[1]), /);

  /* T3, T4: 動的インデックス store/load */
  FOR I = 0 TO 4 FP[I] = I * 0.5;
  PRINT("T3/T4 FP[3]=", FL$(FP[3]), /);

  /* T5: 整数→FLOAT 自動変換 */
  FP[0] = 7;
  PRINT("T5 FP[0]=", FL$(FP[0]), /);

  /* T8: ロードして FLOAT 変数に代入 (i16tof24 が挟まらない確認) */
  R = FP[0];
  PRINT("T8 R=", FL$(R), /);

  /* T7: ローカル間接変数 */
  LOCAL_TEST();
}
