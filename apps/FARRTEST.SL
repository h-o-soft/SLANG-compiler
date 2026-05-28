/* FLOAT 配列テスト: T0 (regression) + T1-T12 カバレッジ */

VAR I;
ARRAY WORD WA[3];
ARRAY BYTE BA[3];
ARRAY FLOAT GA[5];

FX:FLOAT(FLOAT X)
{
  RETURN X * X;
}

LOCAL_TEST()
{
  VAR I;
  ARRAY FLOAT LA[5];

  /* T5, T6: local const index store/load */
  LA[2] = 3.5;
  PRINT("T5/T6 LA[2]=", FL$(LA[2]), /);

  /* T7, T8: local dynamic index store/load */
  FOR I = 0 TO 4
  {
    LA[I] = I + 1.0;
  }
  PRINT("T7/T8 LA[3]=", FL$(LA[3]), /);
}

STATIC_TEST()
ARRAY FLOAT SA[3];
{
  /* T12: static array store/load */
  SA[0] = 1.5;
  SA[1] = SA[0] * 2.0;
  PRINT("T12 SA[1]=", FL$(SA[1]), /);
}

MAIN()
{
  /* T0 regression: WORD/BYTE arrays */
  WA[0] = 100;
  WA[1] = 200;
  BA[0] = 7;
  BA[1] = 15;
  PRINT("T0 WA[0]=", WA[0], " WA[1]=", WA[1], /);
  PRINT("T0 BA[0]=", BA[0], " BA[1]=", BA[1], /);

  /* T1, T2: global const index store/load */
  GA[0] = 1.5;
  GA[1] = 2.5;
  PRINT("T1/T2 GA[0]=", FL$(GA[0]), " GA[1]=", FL$(GA[1]), /);

  /* T3, T4: global dynamic index store/load */
  FOR I = 0 TO 4
  {
    GA[I] = I * 0.5;
  }
  PRINT("T3/T4 GA[3]=", FL$(GA[3]), /);

  /* T9: int -> FLOAT auto conversion */
  GA[0] = 7;
  PRINT("T9 GA[0]=", FL$(GA[0]), /);

  /* T10: FLOAT return value into array */
  GA[1] = FX(2.5);
  PRINT("T10 GA[1]=", FL$(GA[1]), /);

  /* T11: array element as function argument */
  PRINT("T11 FX(GA[0])=", FL$(FX(GA[0])), /);

  /* Call local and static tests */
  LOCAL_TEST();
  STATIC_TEST();
}
