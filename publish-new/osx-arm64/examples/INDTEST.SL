


VAR BYTE KBT[];
VAR BYTE KBT2[];
VAR I;

MAIN()
{
	KBT = $3000;
	KBT2 = $4000;
	KBT[0] = KBT2[0];
	KBT[1] = KBT2[1];
	KBT[2] = KBT2[2];
	KBT[3] = KBT2[3];
	TEST($5000);
}

TEST1(ADR)
VAR BYTE T[];
VAR BYTE B[4];
{
	T = ADR;
	B[0] = T[0];
	B[1] = T[1];
	B[2] = T[2];
	B[3] = T[3];
}


TEST2(BYTE T[])
VAR BYTE B[4];
{
	B[0] = T[0];
	B[1] = T[1];
	B[2] = T[2];
	B[3] = T[3];
}

TEST3()
VAR BYTE T[];
VAR BYTE B[4];
{
    T = $3000;
    B[0] = T[0];
    B[1] = T[1];
    B[2] = T[2];
    B[3] = T[3];
}
