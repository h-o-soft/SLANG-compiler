ARRAY BYTE MOJI[32];
VAR I;

MAIN()
{
  WIDTH(40);
  FOR I=0 TO 30 {
    GETLIN(MOJI, 32);
    PRINT(!(MOJI),/);
  }
  WIDTH(80);
  LOOP {
    GETLIN(MOJI, 32);
    PRINT(!(MOJI),/);
  }
}

