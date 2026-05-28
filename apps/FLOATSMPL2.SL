CONST MMAX = 32767;

VAR I, COUNT;
VAR %%X, %%Y, %%Z, %%PI;
// VAR %%P2,%%N,%%N2;

MAIN()
{


  // P2 = 0.0;
  // N = 1.0;

  // REPEAT {
  //     LOCATE(0,1);
  //     N2 = N*N;
  //     X = 6.0 / N2;
  //     P2 = P2 + X;
  //     X = FSQRT(P2);
  //     PRINT(
  //         FL$(X)
  //     );
  //     N = N + 1.0;
  // } UNTIL INKEY(0)==$1B;



  COUNT = 0;
  FOR I=0 TO MMAX
  {
    X = FRAND();
    Y = FRAND();
    // PRINT(FL$(X),":",FL$(Y),/);
    Z = X*X + Y*Y;
    IF Z<1.0 THEN COUNT++;
    IF (I MOD (MMAX/10)) == 0 THEN PRINT(".");
  }
  PRINT(/);
  PI = COUNT;
  PI = PI / MMAX * 4.0;
  PRINT(FL$(PI),/);
}
