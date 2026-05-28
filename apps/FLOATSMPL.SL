
CONST DEG2RAD = 3.1415926 / 180.0;

VAR I;
VAR FLOAT RAD;
VAR X,Y;

MAIN()
{
  WIDTH(40);
  FOR I=0 TO 360*3
  {
    RAD = I * DEG2RAD;
    X = 20.0 + FCOS(RAD) * (1.0 + I / 135.0);
    Y = 10.0 + FSIN(RAD) * (1.0 + I / 135.0);
    LOCATE(X,Y);
    PRINT("*");
  }
  INKEY(2);
}
