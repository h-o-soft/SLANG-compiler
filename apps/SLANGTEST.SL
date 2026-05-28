
VAR %% A, FLOAT B;

ARRAY FLOAT FAR[9];

//CONST MAGICADR=MAGICENTRY+4;
// CONST
CONST CONV=123,CONV2=CONV;

VAR BYTE INDIR[];
VAR BYTE FIELD[][15];

// 単純変数
VAR VAL,NUM;

// 単純変数のアドレスつき
VAR AVAL:$7000;

// 間接変数およびアドレスつき間接変数
VAR IVAL[],AIVAL[]:$8000;

// 配列変数
ARRAY AR[32],BYTE ARB[32];

// 二次元配列
ARRAY AR2[5][10],BYTE ARB2[10][30];

// アドレスつき配列変数
ARRAY AAR[32]:$8100,BYTE AARB[256]:$8000;

ARRAY A2[10][20];

ARRAY ARI[32]={
  1,2,3,%5,%6,"MO\nJI",%7
};
VAR IVV=14;

(* テスト番号 *)
VAR TNUM;


//#INCLUDE SOROBAN.LIB.TXT


FLOATTEST()
VAR I,J;
BEGIN
  PRINT("FLOAT TEST",/);

  VAL=15;
  B=VAL;
  A = B * 2;    // 30
  A = A + B * 2.5 * 100;  // 30 + 3750 = 3780?

  A = A / 1.23;
  A = A / 4.0;

  IF A < 3000.0 THEN PRINT("3000 yori chiisai yo",/);

//  VAL=123;
//  A=1.23+2.45;
//  A=VAL;
//  A=123.5;
//  VAL=A;
//
//
//  A=123.456;
//  B=4.86;
//  A=A+B + 1000 - A;

  // PRINT(FL$(A+3),/);

  A = FSIN(0.0);
  PRINT("SIN(0):",FL$(A),/);
  A = FSIN(45.0*2*3.14/360);
  PRINT("SIN(90):",FL$(A),/);

  PRINT("FPOW2(3)",FL$(FPOW2(3.0)),/);

  FOR I=0 TO 10 {
    PRINT("RAND:",FL$(FRAND()*100.0),/);
  }

END;


MAIN()
  BEGIN
    ARRAY LAR[3][5];
    ARRAY BYTE LARB[1][3];

    // FLOATTEST();
    // GOTO ENDEND;

    VAL=FOPEN(0,"MAGIC.BIN",4);
    PRINT("ERROR:",VAL,":",/);
    FOR VAL=0 TO 16 {
      TNUM=FGETC(0);
      PRINT(HEX2$(TNUM)," ");
    }
    PRINT(/);
    FCLOSE(0);

    FOPEN(0,"MAGIC.BIN",4);
    FSEEK(0,4,0);
    FOR VAL=0 TO 8 {
      TNUM=FGETC(0);
      PRINT(HEX2$(TNUM)," ");
    }
    PRINT(/);
    FSEEK(0,3,1);
    FOR VAL=0 TO 8 {
      TNUM=FGETC(0);
      PRINT(HEX2$(TNUM)," ");
    }
    PRINT(/);
    FCLOSE(0);

    FOPEN(1,"TEST.TXT",3);
    FPUTC(1,65);
    FPUTC(1,66);
    FPUTC(1,67);
    FCLOSE(1);

    // FOPEN(1,"TEST.TXT",1);
    // FSEEK(1,1,0);
    // FPUTC(1,70);
    // FPUTC(1,71);
    // FPUTC(1,72);
    // FCLOSE(1);

    FOPEN(0,"MAGIC.BIN",4);
    FREAD(0,$B000,4748);
    FCLOSE(0);

    NUM=FOPEN(0,"MAGIC2.BIN",7);
    PRINT("M2:",NUM,/);
    NUM=FWRITE(0,$B000,4748);
    PRINT("FW:",NUM,/);
    NUM=FCLOSE(0);
    PRINT("FC:",NUM,/);

    NUM=INPUT();


    // VAL=FSIZE(0);



    //TNUM=AR2[3][2];
//    TNUM=AR2[VAL][TNUM];
//
    INDIR=$1F00;
    TNUM = INDIR[8];
    INDIR[3]=TNUM;
//    FIELD=$1FFF;
//    TNUM = FIELD[VAL][2] + TNUM;
//    IF FIELD[VAL][2] == 100 THEN PRINT("AAAA");
//    FIELD[VAL][2] = 100;
//    FIELD[VAL][2] += 20;

    // stmtがexpr sc
    FOR VAL=0 TO 9 TNUM=0; NEXT; 
    // 空ループ
    FOR VAL=0 TO 10; NEXT;

    TNUM=1;

    // CONST値が設定されているか
    PRINT("CONST TEST",/);
    IF CONV==123 THEN PRINT(TNUM,"OK",/) ELSE PRINT(TNUM,"NG",/);
    TNUM++;

    PRINT("VAR TEST",/);
    // 単純変数へのCONST値の代入が動くか
    VAL=CONV;
    IF VAL==123 THEN PRINT(TNUM,"OK",/) ELSE PRINT(TNUM,"NG",/);
    TNUM++;
    
    // アドレス指定の単純変数への代入が動くか
    AVAL=VAL;
    IF AVAL==123 THEN PRINT(TNUM,"OK",/) ELSE PRINT(TNUM,"NG",/);
    TNUM++;

    PRINT("INDIRECT VAR TEST",/);
    // 間接変数でAVALと同じ位置を指してその値を拾えるか
    IVAL=$7000;
    IF IVAL[0]==123 THEN PRINT(TNUM,"OK",/) ELSE PRINT(TNUM,"NG",/);
    TNUM++;

    // 間接変数側で書き換えた値をアドレス指定変数側で同様に拾えるか
    IVAL[0]=456;
    IF AVAL==456 THEN PRINT(TNUM,"OK",/) ELSE PRINT(TNUM,"NG",/);
    TNUM++;

    // アドレス指定間接変数でAVALの位置を指定して正しく拾えるか
    AIVAL=$7000;
    IF AIVAL[0]==456 THEN PRINT(TNUM,"OK",/) ELSE PRINT(TNUM,"NG",/);
    TNUM++;

    // 配列変数への単純な代入が動作するか
    AR[3]=10;
    AR[4]=20;
    IF AR[3]==10 AND AR[4]==20 THEN PRINT(TNUM,"OK",/) ELSE PRINT(TNUM,"NG",/);
    TNUM++;


    PRINT("Expr TEST",/);
    // 単純変数の単純な計算が正しく行われるか1
    NUM = 100;
    VAL=NUM*3+NUM*NUM/10;
    IF VAL==1300 THEN PRINT(TNUM,"OK",/) ELSE PRINT(TNUM,"NG",VAL,/);
    TNUM++;

    // 間接変数の単純な計算が正しく行われるか1
    NUM=1;
    IVAL[NUM] = 100;
    VAL=IVAL[NUM]*3+IVAL[NUM]*IVAL[NUM]/10;
    IF VAL==1300 THEN PRINT(TNUM,"OK",/) ELSE PRINT(TNUM,"NG",VAL,/);
    TNUM++;

    // 間接変数の単純な計算が正しく行われるか2
    NUM=0;
    IVAL[NUM+1] = 100;
    VAL=IVAL[NUM+1]*3+IVAL[NUM+1]*IVAL[NUM+1]/10;
    IF VAL==1300 THEN PRINT(TNUM,"OK",/) ELSE PRINT(TNUM,"NG",VAL,/);
    TNUM++;

    // 符号つき計算が正しく行われるか
    NUM = -100;
    VAL=NUM./.2;
    IF VAL==-50 THEN PRINT(TNUM,"OK",/) ELSE PRINT(TNUM,"NG",VAL,/);
    TNUM++;

    // 符号つき比較が正しく行われるか
    IF VAL.<.-10 AND VAL.>.-51 AND VAL.<=.-10 AND VAL.>=-.51 THEN PRINT(TNUM,"OK",/) ELSE PRINT(TNUM,"NG",VAL,/);
    TNUM++;

    // 符号なし比較が正しく行われるか
    VAL=50;
    IF VAL>10 AND VAL<51 AND VAL<=51 AND VAL>=50 THEN PRINT(TNUM,"OK",/) ELSE PRINT(TNUM,"NG",VAL,/);
    TNUM++;

    PRINT("IF TEST",/);
    // IF-ELIFが動くか
    VAL=50;
    IF VAL>50 THEN PRINT(TNUM,"NG",/) ELIF VAL>49 THEN PRINT(TNUM,"OK",/);
    TNUM++;

    // IF-ELIF-ELSEが動くか
    VAL=50;
    // ELSE
    IF VAL>50 THEN PRINT(TNUM,"NG",/); ELIF VAL>51 THEN PRINT(TNUM,"NG",/); ELSE PRINT(TNUM,"OK",/);
    // IF
    IF VAL>49 THEN PRINT(TNUM,"OK",/); ELIF VAL>51 THEN PRINT(TNUM,"NG",/); ELSE PRINT(TNUM,"NG",/);
    // ELIF
    IF VAL>50 THEN PRINT(TNUM,"NG",/); ELIF VAL<51 THEN PRINT(TNUM,"OK",/); ELSE PRINT(TNUM,"NG",/);
    TNUM++;

    // IFの中でIFを書いて動くか
    IF VAL==50 THEN
    {
      IF VAL>50 THEN
      {
        PRINT(TNUM,"NG",/);
      } ELSE {
        PRINT(TNUM,"OK",/);
      }
      PRINT(" -> OK",/);
    } ELSE {
      PRINT(TNUM,"NG2",/);
    }
    TNUM++;

    PRINT("WHILE TEST",/);
    // WHILEの判定が動くか
    VAL=20;
    PRINT("COUNT ");
    WHILE(VAL>10)
    {
      PRINT(VAL," ");
      VAL--;
    }
    PRINT(/);
    IF(VAL==10)
    {
      PRINT(TNUM,"OK",/);
    } ELSE {
      PRINT(TNUM,"NG",/);
    }
    TNUM++;

    // WHILE中のEXITが動くか
    VAL=10;
    WHILE(VAL>=0)
    {
      IF VAL==5 THEN EXIT;
      VAL--;
    }
    IF(VAL==5)
    {
      PRINT(TNUM,"OK",/);
    } ELSE {
      PRINT(TNUM,"NG",/);
    }
    TNUM++;

    // WHILE中のWHILEが動きBREAKが動くか
    VAL=10;
    PRINT("WHILE ");
    WHILE(VAL>0)
    {
      WHILE(VAL>5)
      {
        IF VAL==7 THEN EXIT;
        PRINT("[",VAL,"] ");
        VAL--;
      }
      PRINT(VAL," ");
      VAL--;
    }
    PRINT(/);
    IF(VAL==0)
    {
      PRINT(TNUM,"OK",/);
    }
    TNUM++;

    PRINT("REPEAT UNTIL TEST\n");
    VAL=0;
    REPEAT
    {
      PRINT(VAL," ");
      VAL++;
    } UNTIL VAL==10;
    PRINT(VAL,/);
    IF VAL==10 THEN PRINT(TNUM,"OK",/); ELSE PRINT(TNUM,"NG",/);
    TNUM++;

    PRINT("FOR TEST\n");
    // FOR TOが動くか
    FOR VAL=0 TO 10 PRINT(VAL," ");
    PRINT(/);
    IF VAL==11 THEN PRINT(TNUM,"OK",/); ELSE PRINT(TNUM,"NG",/);
    TNUM++;

    // FOR DOWNTOが動くか
    FOR VAL=10 DOWNTO 0 PRINT(VAL," ");
    PRINT(/);
    IF VAL==-1 THEN PRINT(TNUM,"OK",/); ELSE PRINT(TNUM,"NG",/);
    TNUM++;

    PRINT("LABEL TEST\n");
    GOTO LABEL1;
    PRINT(TNUM,"NG",/);
LABEL1:
    PRINT(TNUM,"OK",/);
    TNUM++;

    PRINT("CASE TEST\n");
    // 完全一致
    VAL = 5;
    CASE VAL
    {
      0 TO 4: PRINT(TNUM,"NG",/);
      5: PRINT(TNUM,"OK",/);
      OTHERS: PRINT(TNUM,"NG",/);
    }
    TNUM++;
    // TO範囲
    VAL = 5;
    CASE VAL
    {
      0: PRINT(TNUM,"NG",/);
      1 TO 5: PRINT(TNUM,"OK",/);
      OTHERS: PRINT(TNUM,"NG",/);
    }
    TNUM++;
    // OTHERS
    VAL = 6;
    CASE VAL
    {
      0: PRINT(TNUM,"NG",/);
      1 TO 5: PRINT(TNUM,"NG",/);
      OTHERS: PRINT(TNUM,"OK",/);
    }
    TNUM++;
    // どれか
    VAL = 8;
    CASE VAL
    {
      0: PRINT(TNUM,"NG",/);
      1 TO 5: PRINT(TNUM,"NG",/);
      6,7,8: PRINT(TNUM,"OK",/)
      OTHERS: PRINT(TNUM,"NG",/);
    }
    TNUM++;

    PRINT("FUNC TEST\n");
    VAL=5;
    NUM=FUNC1(VAL,10);
    PRINT("MAIN VAL:",VAL,/);
    IF VAL==5 THEN PRINT(TNUM,"OK",/) ELSE PRINT(TNUM,"NG",/);
    TNUM++;
    IF NUM==228 THEN PRINT(TNUM,"OK",/) ELSE PRINT(TNUM,"NG",/);
    TNUM++;

    PRINT("FUNC RETURN TEST\n");
    NUM=FUNC2(10);
    IF NUM==1 THEN PRINT(TNUM,"OK",/) ELSE PRINT(TNUM,"NG",/);
    TNUM++;
    NUM=FUNC2(100);
    IF NUM==10 THEN PRINT(TNUM,"OK",/) ELSE PRINT(TNUM,"NG",/);
    TNUM++;

    PRINT("MEM[] MEMW[] TEST\n");
    AVAL=1234;
    IF MEMW[$7000]==1234 THEN PRINT(TNUM,"OK ") ELSE PRINT(TNUM,"NG",/);
    TNUM++;
    IF MEM[$7000]==LOW 1234 AND MEM[$7001]==HIGH 1234 THEN PRINT(TNUM,"OK",/) ELSE PRINT(TNUM,"NG",/);
    TNUM++;

    MEMW[$8000]=$8000;
    VAL=MEMW[$8000];
    IF VAL==$8000 THEN PRINT(TNUM,"OK ") ELSE PRINT(TNUM,"NG",/);
    TNUM++;
    MEMW[VAL]=2;
    NUM=MEMW[VAL];
    IF NUM==2 THEN PRINT(TNUM,"OK ") ELSE PRINT(TNUM,"NG",/);
    TNUM++;
    MEMW[VAL+NUM]=100;
    MEMW[VAL+NUM+2]=23;
    VAL=MEMW[VAL+NUM]+MEMW[VAL+NUM+2];
    IF VAL==123 THEN PRINT(TNUM,"OK ") ELSE PRINT(TNUM,"NG",/);
    TNUM++;

    MEM[$8000]=1;
    MEM[$8001]=100;
    VAL=MEM[$8000];
    IF VAL==1 THEN PRINT(TNUM,"OK ") ELSE PRINT(TNUM,"NG",/);
    TNUM++;

    VAL=$8000;
    NUM=MEM[VAL];
    IF NUM==1 THEN PRINT(TNUM,"OK ") ELSE PRINT(TNUM,"NG",/);
    TNUM++;
    MEM[VAL]=123;
    IF MEM[VAL]==123 THEN PRINT(TNUM,"OK ") ELSE PRINT(TNUM,"NG",/);
    TNUM++;
    NUM=MEM[VAL];
    IF NUM==123 THEN PRINT(TNUM,"OK ") ELSE PRINT(TNUM,"NG",/);
    TNUM++;
    VAL=$8000;
    NUM=1;
    MEM[$8001]=30;
    MEM[$8003]=25;
    VAL=MEM[VAL+NUM]+MEM[VAL+NUM+2];
    IF VAL==55 THEN PRINT(TNUM,"OK",/) ELSE PRINT(TNUM,"NG",/);
    TNUM++;

    PRINT("ARRAY2 TEST",/);
    VAL=1;
    NUM=5;
    // 二次元配列にアクセス出来るか
    AR2[1][5]=10;
    IF AR2[1][5]==10 THEN PRINT(TNUM,"OK",/) ELSE PRINT(TNUM,"NG",/);
    TNUM++;

    // 二次元配列にアクセス出来るか(Byte)
    ARB2[1][5]=10;
    IF ARB2[1][5]==10 THEN PRINT(TNUM,"OK",/) ELSE PRINT(TNUM,"NG",/);
    TNUM++;

    // 二次元配列にアクセス出来るか(添字が変数)
    IF AR2[VAL][NUM]==10 THEN PRINT(TNUM,"OK",/) ELSE PRINT(TNUM,"NG",/);
    TNUM++;
    IF ARB2[VAL][NUM]==10 THEN PRINT(TNUM,"OK",/) ELSE PRINT(TNUM,"NG",/);
    TNUM++;

    // 二次元配列にアクセス出来るか(添字が式)
    VAL=2;
    NUM=4;
    IF AR2[VAL-1][NUM+VAL-1]==10 THEN PRINT(TNUM,"OK",/) ELSE PRINT(TNUM,"NG",/);
    TNUM++;
    IF ARB2[VAL-1][NUM+VAL-1]==10 THEN PRINT(TNUM,"OK",/) ELSE PRINT(TNUM,"NG",/);
    TNUM++;

    // ローカル二次元配列にアクセス出来るか
    VAL=1;
    NUM=3;
    LAR[1][3]=10;
    LARB[1][3]=10;
    IF LAR[1][3]==10 THEN PRINT(TNUM,"OK",/) ELSE PRINT(TNUM,"NG",/);
    TNUM++;
    IF LARB[1][3]==10 THEN PRINT(TNUM,"OK",/) ELSE PRINT(TNUM,"NG",/);
    TNUM++;
    
    // ローカル二次元配列にアクセス出来るか(添字が変数)
    IF LAR[VAL][NUM]==10 THEN PRINT(TNUM,"OK",/) ELSE PRINT(TNUM,"NG",/);
    TNUM++;
    IF LARB[VAL][NUM]==10 THEN PRINT(TNUM,"OK",/) ELSE PRINT(TNUM,"NG",/);
    TNUM++;

    // ローカル二次元配列にアクセス出来るか(添字が式)
    VAL=2;
    NUM=2;
    IF LAR[VAL-1][NUM+VAL-1]==10 THEN PRINT(TNUM,"OK",/) ELSE PRINT(TNUM,"NG",/);
    TNUM++;
    IF LARB[VAL-1][NUM+VAL-1]==10 THEN PRINT(TNUM,"OK",/) ELSE PRINT(TNUM,"NG",/);
    TNUM++;


#IF FALSE
    PRINT("KAZUATE GAME",/);
    VAL=RND(100);
    WHILE(TRUE)
    {
      PRINT("NUMBER?");
      NUM=INPUT();
      IF NUM==VAL THEN
      {
        PRINT("SEIKAI!\n");
        EXIT;
      } ELIF NUM<VAL THEN {
        PRINT("MOTTO OOKII\n");
      } ELSE {
        PRINT("MOTTO CHIISAI\n");
      }
    }
#END

    PRINT("INPUT 'START'\n");
    GETL(AARB);
    IF STRCMP(AARB,"START") == 0 THEN PRINT(TNUM,"OK",/) ELSE PRINT(TNUM,"NG",/);

    ENDEND:
  END;

STRCMP(A,B)
VAR I,J,D;
BEGIN
  I=A;
  J=B;
  WHILE(TRUE)
  {
    D = MEM[I] - MEM[J];
    IF D!=0 THEN RETURN(D);
    IF MEM[I] == 0 THEN RETURN(0);
    I++;
    J++;
  }
END;


  // パラメータ、静的宣言、局所宣言のテスト
FUNC1(I,J)
  VAR VAL;
  BEGIN
    // 局所宣言(ローカル変数)
    VAR LOCAL;

    LOCAL = 123;
    PRINT(I,",",J,/);
    VAL=100;
    PRINT("FUNC VAL:",VAL,/);
    // 5+123+100
    PRINT("I:",I,/);
    I=I+LOCAL+VAL;
    IF I==228 THEN PRINT(TNUM,"OK",/) ELSE {
      PRINT(TNUM,"NG",I,/);
      PRINT("LOCAL:",LOCAL,/);
      PRINT("VAL:",VAL,/);
    }
    TNUM++;

  END(I);

  // RETURNで値を返すテスト
  FUNC2(I)
  BEGIN
    IF I<100 THEN RETURN(1);
    RETURN 10;
  END;

