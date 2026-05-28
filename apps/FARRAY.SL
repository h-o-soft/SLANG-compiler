
ARRAY FLOAT FA[10] = {
	1.0, 1.4, 1.82, 100.25, 400
};
ARRAY WORD WA[5] = {
1,2,3,5,7,9
};
ARRAY BYTE BA[5];

VAR WORD WVAL=1234;

VAR FLOAT KF[];

MAIN()
{
	// FA[0] = 1.23;
	// FA[1] = 2.34;
	FA[2] = FA[0] + FA[1];

	PRINT(FL$(FA[0]),",",FL$(FA[1]),",",FL$(FA[2]),/);

	KF=$9000;
	KF[0] = 1.234;
	KF[1] = 6.000;

	PRINT(FL$(KF[0]),",",FL$(KF[1]),",",HEX2$(MEM[$9000]),HEX2$(MEM[$9001]),HEX2$(MEM[$9002]),/);

}

