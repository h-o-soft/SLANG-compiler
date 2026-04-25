
VAR MAIN_VAL=10;

MAIN()
{
	PRINT("This is main.",/);
	PRINT(MAIN_VAL, /);
	MODFUNC();
}

#MODULE $3000

VAR MVAL;

MODFUNC()
{
	MVAL = 100;
	PRINT("Module value:", MVAL, /);
	PRINT("Main value:", MAIN_VAL, /);
}

#END

