

static char mybuf[8];

void disp_hello(char val)
{
	puts("hello:");
	itoa((int)val, mybuf, 10);
	puts(mybuf);
}

void disp_helloptr(unsigned short *ptr)
{
	puts("word:");
	itoa((int)ptr[0], mybuf, 10);
	puts(mybuf);
}

