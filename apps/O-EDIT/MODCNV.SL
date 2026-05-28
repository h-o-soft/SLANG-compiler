ORG     $3000;
OFFSET  $6000;
WORK    $D000;



ARRAY   WORD    OBJD0[255][2]:$9000,
        BYTE    OBJW0[255][1]:$9600,
        BYTE          DEC2[4] = [  " 000",0],
        BYTE          DEC4[6] = [" 00000",0],
        BYTE       FLNAME[20],
        BYTE        LABEL[20];



VAR     PMAX:$9800 , WMAX:$9802 ,
        FNAD , FNLN ,
        DTADR = $1F70 , SIZE = $1F72 , EXADR = $1F6E , FILE = $1FA3 ,
        WOPN  = $1FAF , ROPN = $2009 , WRD   = $1FAC , RDD  = $1FA6 ;



MAIN()
  VAR   I,J,CT,PARA;
   BEGIN
    PRINT("\C <<<< MODCNV V1.00 >>>>\N");
    FNAD = FLNAME;
    PARA = GETFNAME();
    IF (PARA == -1) RETURN;
    LOADFILE(FNAD);
    IF (^CY<>0)
    [
        PRINT("READ ERROR!\N");
        BEEP();
        RETURN;
    ]
    FOR I=0 TO 20 [
      IF (FLNAME[I] == '.') [
        FLNAME[I + 1] = 'S';
        FLNAME[I + 2] = 'L';
        FLNAME[I + 3] =  0 ;
        I = 20;
      ]
    ]
    FOPEN(0,FLNAME,3);
    FPRINT(0,"// MAGIC 3D DATA\N\N");
    FPRINT(0,"CONST           ");

    FPRINT(0,LABEL);
    FPRINT(0,"_PMAX=");
    FPRINT(0,DEC2CNV(PMAX-1));
    FPRINT(0,",");

    FPRINT(0,LABEL);
    FPRINT(0,"_WMAX=");
    FPRINT(0,DEC2CNV(WMAX-1));
    FPRINT(0,";\N\N");
    FPRINT(0,"ARRAY           ");
    FPRINT(0,LABEL);
    FPRINT(0,"_D[");
    FPRINT(0,DEC2CNV(PMAX-1));
    FPRINT(0,"][2]=[\N");
    FOR I=0 TO PMAX-2 [
      FPRINT(0,"                ");
      FOR J=0 TO 2 [
        FPRINT(0," %");
        FPRINT(0,DEC4CNV(OBJD0[I][J]));
        FPRINT(0,",");
      ]
      FPRINT(0,"\N");
    ]
    FPRINT(0,"                ");
    FPRINT(0," %");
    FPRINT(0,DEC4CNV(OBJD0[I][0]));
    FPRINT(0,",");
    FPRINT(0," %");
    FPRINT(0,DEC4CNV(OBJD0[I][1]));
    FPRINT(0,",");
    FPRINT(0," %");
    FPRINT(0,DEC4CNV(OBJD0[I][2]));
    FPRINT(0,"],\N\N");
    FPRINT(0,"        BYTE    ");
    FPRINT(0,LABEL);
    FPRINT(0,"_W[");
    FPRINT(0,DEC2CNV(WMAX-1));
    FPRINT(0,"][1]=[\N");
    CT=0;
    FPRINT(0,"                ");
    FOR I=0 TO WMAX-2 [
      FOR J=0 TO 1 [
        FPRINT(0,DEC2CNV(OBJW0[I][J]));
        FPRINT(0,",");
      ]
      IF (++CT==5) [ FPRINT(0,"\N                "); CT=0; ]
    ]
    FPRINT(0,DEC2CNV(OBJW0[I][0]));
    FPRINT(0,",");
    FPRINT(0,DEC2CNV(OBJW0[I][1]));
    FPRINT(0,"];\N");
    FPUTC(0,0);
    FCLOSE(0);
 END;



GETFNAME()
  VAR   PARA , I;
  BEGIN
    CALL($2006);
    PRINT("INPUT FILE NAME\N");
    PARA  = LINPUT(FNAD , 18);
    FOR I = 0 TO 20  LABEL[I]=FLNAME[I];
    FOR I = 0 TO 20
    [
      IF (FLNAME[I] == 0)
      [
        FLNAME[I    ] = '.';
        FLNAME[I + 1] = 'M';
        FLNAME[I + 2] = 'O';
        FLNAME[I + 3] = 'D';
        FLNAME[I + 4] =   0;
        I = 20;
      ]
    ]
  END(PARA);



LOADFILE(FADR)
  BEGIN
    ^A = 1; ^DE = FADR; CALL(FILE);     (* FILE SET  *)
    CALL(ROPN);                         (* FILE OPEN *)
    MEMW[DTADR] = $9000;
    GETREG(); IF (^CARRY == 1) RETURN;
    CALL(RDD);                          (* LOAD      *)
  END;



FPRINT(FP,BYTE BUF[])
  VAR I;
  BEGIN
    I=0; WHILE ( BUF[I]>0 )
    [
        PRINT(STR$(BUF[I],1));
        FPUTC(FP,BUF[I++]);
        IF (INKEY(0)>0) PK();
    ]
  END;



DEC2CNV(D2)
  BEGIN
    IF (D2 .<. 0) DEC2[0] = '-'; ELSE DEC2[0] = ' ';
    D2 = ABS(D2);
    DEC2[3] = '0'+D2-(D2 / 10 * 10); D2 = D2 / 10;
    DEC2[2] = '0'+D2-(D2 / 10 * 10); D2 = D2 / 10;
    DEC2[1] = '0'+D2-(D2 / 10 * 10);
  END(DEC2);



DEC4CNV(D4)
  BEGIN
    IF (D4 .<. 0) DEC4[0] = '-'; ELSE DEC4[0] = ' ';
    D4 = ABS(D4);
    DEC4[5] = '0'+D4-(D4 / 10 * 10); D4 = D4 / 10;
    DEC4[4] = '0'+D4-(D4 / 10 * 10); D4 = D4 / 10;
    DEC4[3] = '0'+D4-(D4 / 10 * 10); D4 = D4 / 10;
    DEC4[2] = '0'+D4-(D4 / 10 * 10); D4 = D4 / 10;
    DEC4[1] = '0'+D4-(D4 / 10 * 10);
  END(DEC4);



PK()
  BEGIN
    WHILE(INKEY(0)>0)  []
    WHILE(INKEY(0)==0) []
    WHILE(INKEY(0)>0)  []
  END;
