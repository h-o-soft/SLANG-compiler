

CFUNC DISP_HELLO(BYTE VAL) VOID:disp_hello;
CFUNC DISP_HELLOPTR(WORD p[]) VOID:disp_helloptr;

ARRAY AR[2];

MAIN()
{
	PRINT("cfunc test!",/);

	DISP_HELLO(65);
	AR[0] = 30005;
	DISP_HELLOPTR(AR);
}


