
VAR I;
VAR KAN[];
ARRAY BYTE BUF[256];

MAIN()
{
	KAN = &I;

	KAN[0] = 123;

	PRINT(I,/);
	PRINT(KAN[0],/);
	PRINT("hello, slang! (",DECI$(I),")", "(", STR$(65,3), ")", SPC$(2),"#", /);
	IF GETL(BUF) != -1 {
		PRINT("input:", !(BUF),/);
	}
}

