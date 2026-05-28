
VAR I, J, K, STR, CNT, HEXV;
VAR ERR;
VAR BYTE VAL;
ARRAY BYTE MOJI[32];

MAIN()
{
  I = 30;
  J = 5;
  STR = "MOJI";
  VAL = 128;
  PRINT(I*J, /, !(STR), /, VAL, /);

  CNT=100;
  HEXV = 123;
  PRINT("COUNT:", FORM$(CNT,5),/, HEX2$(HEXV), /);

  // I=INPUT();
  // PRINT(I);
  // LOOP
  // {
  //   K = INKEY(0);
  //   IF K == $1d THEN PRINT("left",/);
  //   IF K == $1c THEN PRINT("right",/);
  //   IF K == $1e THEN PRINT("up",/);
  //   IF K == $1f THEN PRINT("down",/);
  // }
  // GETLIN(MOJI, 32);
  // PRINT(!(MOJI));
  // FOPEN(0, "ASCII.TXT",3);
  // FPUTC(0, 'H');
  // FPUTC(0, 'E');
  // FPUTC(0, 'L');
  // FPUTC(0, 'L');
  // FPUTC(0, 'O');
  // FCLOSE(0);

  // ERR = FOPEN(0, "ASCII.TXT", 1);
  // IF ERR == 0
  // {
  //   FSEEK(0,0,2);
  //   FPUTC(0, '!');
  //   FPUTC(0, '!');

  //   //FPUTC(0, '!');
  //   //FPUTC(0, '!');
  //   //FPUTC(0, '!');
  //   FCLOSE(0);
  // }
  I = PORT[$0701];
  PORT[$1FA0] = 7;
}
