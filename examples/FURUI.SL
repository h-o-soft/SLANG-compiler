
VAR I,P;
ARRAY BYTE NT[10000];

MAIN()
{
	FOR I=0 TO 10000 NT[I]=0;
	FOR I=2 TO 10000/2 NT[I*2]=1;
	P=3;
	WHILE (P*P<=10000)
	{
		FOR I=P TO 10000/P
		{
			NT[I*P]=1;
			I++;
		}
		P++;
		WHILE NT[P]==1
		{
			P++;
		}
	}
	FOR I=2 TO 10000
	{
		IF NT[I]==0 THEN PRINT(I," ");
	}
}

