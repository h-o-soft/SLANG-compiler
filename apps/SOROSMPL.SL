
#INCLUDE SOROBAN.LIB

ARRAY BYTE A[@DBL],
      BYTE B[@DBL],
      BYTE C[@DBL];

MAIN()
{
  // 倍精度で計算
  @DOUBLE();

  // A = 1.2;
  @CVSTF(A,"1.2");
  // B = 3.4;
  @CVSTF(B,"3.4");
  @CVUTF(B,3);
  // C = A * 3;
  @MUL(C,A,B);
  // 4.08
  PRINT(MSX$(@CVFTS(C)),/);
}