/*
   Osaka Denki Tsuushin Daigaku Koutou Gakkou
                                       . . . . . . . . . . .
         OOO  EEEEE  CCC              . #### ###  ###   ##  .
        O   O E     C   C             . #    #  # #  # #    .
        O   O EEEEE C                 . ###  #  # ###   ##  .
        O   O E     C   C             . #    #  # #       # .
         OOO  EEEEE  CCC              . #### ###  #     ##  .
                                       . . . . . . . . . . .
         BBBB  U   U NN  N K  K  AAA   SSS   AAA   III
         B   B U   U NN  N K K  A   A S     A   A   I
         BBBB  U   U N N N KK   AAAAA  SSS  AAAAA   I
         B   B U   U N  NN K K  A   A     S A   A   I
         BBBB   UUU  N  NN K  K A   A  SSS  A   A  III  Program

        << O-EDIT Ver 2.1  1992/06/13  23:32:47 >>

                Programed by KUROKI JUNICHI

                                                                 */

ORG     $3000;
OFFSET  $6000;
WORK    $D000;

CONST   _PLOTSW  = 1 , _SINCOS = 0 , _THREE = 1 ,
        _TILESW  = 1 , _FLOAT = 0  , _GHIN = $A8,
        _KPRINTF = 0;

#INCLUDE GRAPH.LIB

ARRAY   WORD    OBJD0[255][2]:$9000,
        BYTE    OBJW0[255][1]:$9600,
        WORD         MSTEP[3] = [%1,%2,%5,%10],
        WORD         LSTEP[5] = [%1,%5,%10,%40,%60,%100],
        WORD        DUMMYP[8] = [%0,%0,%0,%0,%0,%0,%0,%0,%0],
        BYTE        DUMMYW[5] = [0,1,1,2,2,0],
        BYTE       FLNAME[20];

VAR     PMAX:$9800 , WMAX:$9802 , PX , PY , PZ , KFLAG , NOWP ,
        SPEED , FNAD , FNLN , GENX , GENY , GENZ ,
        DTADR = $1F70 , SIZE = $1F72 , EXADR = $1F6E , FILE = $1FA3 ,
        WOPN  = $1FAF , ROPN = $2009 , WRD   = $1FAC , RDD  = $1FA6 , MPOT;

MAIN()
  VAR   KEY , R , FF;
  BEGIN
    GETREG(); FNAD = ^DE; FF = 0;
    START:
    @PALET(0 , 0 , 0 , 0 , 0 , 0 , 0 , 0);
    PMAX  = 0; WMAX = 0; PX   = 0; PY   = 0; PZ   = 0;
    SPEED = 2; NOWP = 0; GENX = 0; GENY = 0; GENZ = 0;
    IF (MEM[FNAD] > 0) AND (FF == 0) [ LOADFILE(FNAD); FF = 1; ]
    FNAD  = &FLNAME;
    FOR R = 0 TO 8 [_PAR[R] = 0;]
    _PAR[_HEAD ]  = 75;
    _PAR[_PITCH]  = 360 - 10;
    _SHEAD        = 15; _SPITCH = 360 - 10; _SBANK = 0;
    _OFSX         = 0;  _OFSY   = 0;        _OFSZ  = 200;
    WSCR(); REDRAW();
    MLOOP1:
    @MODE(2,1); @GRAD(0);
    @WINDOW(  1 ,   1 ,  98 ,  98); @FULL(   1 ,   1 , 198 ,  98);
    @WINDOW(200 ,   0 , 399 ,  98); @FULL( 200 ,   0 , 399 ,  99);
    @WINDOW(  1 , 101 , 198 , 198); @FULL(   1 , 101 , 198 , 198);
    @WINDOW(201 , 101 , 398 , 198); @FULL( 201 , 101 , 398 , 198);
    PRINT("\C");
    LOCATE(60 , 0);     PRINT("Select Menu");
    LOCATE(60 , 1);     PRINT("1) SET   POINT");
    LOCATE(60 , 2);     PRINT("2) SET   WIRE");
    LOCATE(60 , 3);     PRINT("3) ERASE POINT");
    LOCATE(60 , 4);     PRINT("4) ERASE WIRE");
    LOCATE(60 , 5);     PRINT("5) ROTATION");
    LOCATE(60 , 6);     PRINT("6) PALET ON");
    LOCATE(60 , 7);     PRINT("7) PALET OFF");
    LOCATE(60 , 8);     PRINT("8  POINT MOVE");
    LOCATE(60 , 9);     PRINT("S) SAVE DATA");
    LOCATE(60 , 10);    PRINT("L) LOAD DATA");
    LOCATE(60 , 11);    PRINT("C) CLEAR");
    LOCATE(60 , 12);    PRINT("R) ROTATION 2");
    LOCATE(60 , 13);    PRINT("M) MOVE      ______ ");
    LOCATE(60 , 14);    PRINT("E) END      (H:HELP)");
    LOCATE(73 , 15);    PRINT("~~~~~~");
    LOCATE(61 , 16);    PRINT("� O-EDIT �");
    LOCATE(75 , 17);    PRINT("_");
    LOCATE(61 , 18);    PRINT("OSAKA DENKI TSUSIN");
    LOCATE(61 , 19);    PRINT("DAIGAKU KOUTOU");
    LOCATE(64 , 20);    PRINT("GAKKOU");
    LOCATE(61 , 22);    PRINT("E.D.P.S. bu");
    LOCATE(61 , 23);    PRINT("Junichi Kuroki");
    MLOOP2:
    KEY = INKEY(0);
    IF (KEY == 0) GOTO MLOOP2;
    PRINT("\C");
    CASE KEY OF [
        '1'     SETP();
        '2'     SETW();
        '3'     ERAP();
        '4'     ERAW();
        '5'     ROTATION();
        '6'     @PALET(0 , 1 , 2 , 3 , 4 , 5 , 6 , 7);
        '7'     @PALET(0 , 0 , 0 , 0 , 0 , 0 , 0 , 0);
        '8'     PMOVE();
        'S'     SAVE();
        'L'     LOAD();
        'E'    [@PALET(0 , 0 , 0 , 0 , 0 , 0 , 0 , 0); RETURN; ]
        'C'    [READY(); IF (KFLAG == 1) GOTO START;]
        'R'     LOOK();
        'M'     OMOVE();
        'H'     HELP();
        ]
    GOTO MLOOP1;
  END;

HELP()
  BEGIN
  @PALET(0 , 0 , 0 , 0 , 0 , 0 , 0 , 0); PRINT("\C");

  PRINT(" HELP MODE\N\N\N"); PK();

  PRINT(" << �ް� ���� � ²� >>\N\N");
  PRINT(" �� °� ʤ MAGIC ֳ � 3D ��׸� �ް� � ���� �� ��� °� �޽� ��\N\N");
  PRINT("�� ���� �\N\N\N");
  PRINT("�̾�� ���ڽ 0000-05FF ��޶� ��ˮ� �ް��  [ WORD DATA ]\N\N");
  PRINT("�̾�� ���ڽ 0600-07FF ��޶� ����� �ް��  [ BYTE DATA ]\N\N");
  PRINT("�̾�� ���ڽ      0800    �� ��ˮ� � ���  [ WORD DATA ]\N\N");
  PRINT("�̾�� ���ڽ      0802    �� ����� � ���  [ WORD DATA ]\N\N");
  PK();
  PRINT("�ި�� � �۸ ��� خ���\N\N");
  PRINT("�̾�� ���ڽ 0000-0FFF ���� ů� ��Ͻ�\N\N\N");
  PRINT("�۸ ����\N\N");
  PRINT("��ˮ� �ް� : [ X1 ][ Y1 ][ Z1 ] [ X2 ][ Y2 ][ Z2 ]...\N\N");
  PRINT("����� �ް� : [P1][P2] [P1][P2] [P1][P2] [P1][P2]...\N\N");
  PRINT("��ˮ� ���  : [ PCT ]\N\N");
  PRINT("����� ���  : [ LCT ]\N\N");
  PRINT("*����\N\N");
  PRINT(" �ި�� � ���� ��ķ� ���޳÷ � ������ 'MOD' �� ·Ͻ ��ޤ �����\N\N");
  PRINT("� � ¹Ų�� ��޻�� ��� ۰�� ��ķ� ���޳÷ � ������ 'MOD' � ¹�\N\N");
  PRINT("̧�� ����� � ����ޤ ¹Ų�� ��޻��\N\N\N"); PK();

  PRINT(" << ���� � ²� >>\N\N");
  PRINT(" �� ��ި��ʤ����� � ��� � �ݷ� �޵�ŴϽ� ��ò � [5]�����ݾ� �\N\N");
  PRINT("� ů� ��Ͻ� ��� �ݷ� � ��� � ���ٷ�� [5]�� � ��� � [RETURN]��\N\N");
  PRINT("[0]�� � ��� � [SPACE]�� [1],[3],[7],[9]�� � ���� [J],[K],[U],\N\N");
  PRINT("[I]�� � �ֳ �� �� �� �޷Ͻ� ��� Ӹ÷ � �� � ���Ը ���� � ��޳\N\N");
  PRINT("��� �Ҥ ���� � ��޳خ� � ��� ���� ů�Ͻ� ����� �϶� ��׸� ��\N\N");
  PRINT("¸�Ų ��ޤ ��޳خ� � �ݶ � [S],[+]�� �� ��� �� �� �޷Ͻ�\N\N\N");
  PK();

  PRINT(" << �ޯ�� � ��ި�� � ²� >>\N\N");
  PRINT(" Ͻޤ�ޯ�� � ��޸ ���� �� � ˶Ź��� ��Ͼݡ ܲ԰�ڰ� � �� �޹\N\N");
  PRINT("� ���� �޽�פ �� � ˸�� � ��� ���޹��� ���� � �� °� � ¶��Ž\N\N");
  PRINT("�� �� �޷Ͻ�\N\N");
  PRINT(" Ͻޤ ˷�� �� � خ�ʼ � �� � ��� ��޻�� [1] � SET POINT �޽�\N\N");
  PRINT("���ٷ����� �ݷ� �� ���� � ��޶�� �� � [����]� ��� [5]���� ��\N\N");
  PRINT("� �º� �� �޷Ͻ� Ӽ� ���޴� ��� �� � �� � ��� �ϯ� �ޱ�� [0]\N\N");
  PRINT("�� ��� [��߰�]�� �� SET POINT � ǹ� [3]�� �� ERASE POINT � �\N\N");
  PRINT("�ؤ [8],[2] �� � ���� �� ��� �� �� � ���ޤ ��ò �� �� ���ޮ\N\N");
  PK();
  PRINT("�ø�޻�� Ӽ �� � ��ɶ� �� �޼�פ[8]�� � POINT MOVE ��, ERASE-\N\N");
  PRINT("-POINT � ķ �޳ֳ � �� � ���ޤ ·�� Ӹ÷ � �� � ���� � ��޶�\N\N");
  PRINT("��ò �� � ��ø�޻�� ��ϯ� ��� �� �� �� �� Ӹ÷ � �� � ��޳ �\N\N");
  PRINT("Ͻ�\N\N");
  PRINT(" ��� � �� � ���� ·�� ��ݾ� �� �� �ƭ� � ���ؤ [2] �� SET WI-\N\N");
  PRINT("-RE � ����� ��޻�� ERASE POINT � �ż� ֳ� �� � �ڸ� �޷Ͻ ���\N\N");
  PRINT("�� � ��� ����� ��޻�� �� �� ˹Ͻ� Ӽ ���޴� �� � ˲üϯ�פ �\N\N");
  PRINT("����� �� �� �ƭ� � ���ؤ ERASE POINT � ֳخ� �� �� � ���ޮ �\N\N");
  PRINT("ø�޻��\N\N\N"); PK();

  PRINT(" << ۰ð��� �ɳ � ²� >>\N\N");
  PRINT(" ��ި�� �ò� ����� � �� �� ���ů�ؤ ����޳ �� ���޽ ֳ� ����\N\N");
  PRINT("��׸� � ¸�� ��Ĥ ��� ���� � ��Ͻ� ��� ķʤ [5] � ROTATION �,\N\N");
  PRINT("[M] � MOVE � ����� ��޻�� ROTATION ��ʤ 3D ����޳ � ��׸� �\N\N");
  PRINT("���� ���ؤ ���� � ���� ��� �� �� �޷Ͻ� MOVE ��� ��� ���� ��\N\N");
  PRINT("��Ͻ ��ޤ �� � �� �� ��ò �� � ��Ĥ �� �� � ����� � �ä ��׸\N\N");
  PRINT("� � ��ˮ� � ��ò�� �Ͻ��\N\N\N"); PK();

  PRINT(" << ¸�� ��׸� � �ɼ��� �� � ��� >>\N\N");
  PRINT(" �� ��ި� � ¸�� ��׸� � �ƶ � ��ĳ�� �� ¶� ϴ � ���� �ݼ�\N\N");
  PRINT("�� ˮ��� ��ٶ ���� �� �� �� �޷Ͻ� �ƭ��� [R] � ROTATION 2 �\N\N");
  PRINT("����� ��޻�� ���� ���߲ � ܲ԰�ڰ� ˮ��� � �ø�Ͻ� ���� � ��\N\N");
  PRINT("� γ �� ˮ��� ��Ͻ ��ޤ �� � ���ޯ� �� � �� �� �ø�޻��\N\N\N");
  PK();

  PRINT(" << SAVE/LOAD �ɳ � ²� >>\N\N");
  PRINT(" �� °� � ���� �� �ް� � �ި�� � ο�� �ٺ� �� �޷Ͻ� MENU ��\N\N");
  PRINT("[S] � ���� ��� �ިڸ�� �� ˮ��� ��ä ̧�� Ȱ� � ƭ�خ� ��ֳ\N\N");
  PRINT("ֳ��� �÷Ͻ� ���ޤ ������ � �ڽ� � 13 Ӽ� �Ų � ̧�� Ȱ� � �\N\N");
  PRINT("ճخ� �� ��޻�� ���޳÷ � ������ 'MOD' �� ·� ���خ� �Ͻ�\N\N");
  PRINT(" LOAD � ²� � SAVE � �޳ֳ � ��� � [L] � ���� �� �� ��ů� ���\N\N");
  PRINT("��� ������ � ��Ͼݡ\N\N\N"); PK();

#IF (_PLOTSW == 1)

  PRINT(" << ��ۯ� ���خ� �ɳ � ²� >>\N\N");
  PRINT(" �� °� ��ʤ ROTATION 2 � ���� ��� ķ�� ��ۯ� � ��׸� � ����\N\N");
  PRINT("�� �� �޷Ͻ� ��׸� � ���� ���� ķ � �� ��� �ݸ�� � ů�� [P]\N\N");
  PRINT("�� ��ۯ� � ���خ� �� ��Ͻ� �ֳ �޷� ��ۯ� � ����� �߰� � ��\N\N");
  PRINT("�޸ �ɳ �ޤ 'H' , 'J1' , 'M x1 , y1' , 'D x1 , y2' �� ¶�� �\N\N");
  PRINT("� �޽�\N\N\N"); PK();

#ENDIF

  PRINT(" << ��޳ �� � �ް� �к� � ²� >>\N\N");
  PRINT(" �� °� � ������ ķ� �޳�� � �ް� � �к� �� �� �޷Ͻ�\N\N\N");
  PRINT("# O-EDIT:file name.MOD [CR]\N\N\N");
  PRINT(" � ��̳ � �� ���� ��޻�� file name � ����� �ʤ ̧�� Ȱ� ��\N\N");
  PRINT("ʲ�Ͻ� ������ 'MOD' � ���ج� �޷Ͼݡ\N\N\N"); PK();

  PRINT("[EOD]\N"); BEEP(); BEEP(); BEEP(); PK(); BEEP();

  PRINT("\C");
  @PALET(0 , 1 , 2 , 3 , 4 , 5 , 6 , 7);
  END;

PK()
  BEGIN
    WHILE(INKEY(0) == 0) []
  END;

WSCR()
  VAR   I;
  BEGIN
    @INIT();
    @MODE(2,0);
    FOR I = 0 TO 39 [ @LINE(I * 10 , 0 , I*10 , 199); ]
    FOR I = 0 TO 39 [ @LINE(0 , I * 5 , 399 , I * 5); ]
    @GRAD(0);
    @MODE(2 , 0); @FULL(200 , 0 , 399 , 99);
    @MODE(2 , 1); @FULL(200 , 0 , 399 , 99);
    @MODE(2 , 2); @FULL(200 , 0 , 399 , 99);
    @WINDOW( 200 , 0 , 399 , 99);
    @MODE(2 , 0);
    @TLINE(0 - _OFSZ ./. 2 , 0 , 0 , _OFSZ ./. 2 , 0 , 0);
    @TLINE(0 , 0 - _OFSZ ./. 2 , 0 , 0 , _OFSZ ./. 2 , 0);
    @TLINE(0 , 0 , 0 - _OFSZ ./. 2 , 0 , 0 , _OFSZ ./. 2);
    @WINDOW( 0 , 0 , 639 , 199);
    @CBOX(  0 ,   0 , 199 ,  99 , 6);
    @CBOX(  0 , 100 , 199 , 199 , 6);
    @CBOX(200 , 100 , 399 , 199 , 6);
  END;

CORSOR()
  VAR   KEY , IX , IY , IZ;
  BEGIN
    DCSR(2 , 1 , PX , PY , PZ);
    LOCATE(60 ,  7);    PRINT("[4]   [6]:X");
    LOCATE(60 ,  8);    PRINT("[8]   [2]:Y");
    LOCATE(60 ,  9);    PRINT("[Q]   [Z]:Z");
    LOCATE(60 , 10);    PRINT("[+]   [S]:SPEED");
    LOCATE(60 , 11);    PRINT("[5] [RET]:SET");
    LOCATE(60 , 12);    PRINT("[0]   [ ]:ESCAPE");
    CORSOR1:
    KEY = INKEY(0);
    IF (KEY == 0) GOTO CORSOR1;
    IX = PX;
    IY = PY;
    IZ = PZ;
    KEY = KEYCNV(KEY);
    CASE KEY OF [
        '4'     PX = PX - MSTEP[SPEED];
        '6'     PX = PX + MSTEP[SPEED];
        '2'     PY = PY + MSTEP[SPEED];
        '8'     PY = PY - MSTEP[SPEED];
        'Z'     PZ = PZ - MSTEP[SPEED];
        'Q'     PZ = PZ + MSTEP[SPEED];
        '+'    [SPEED++; SPEED = (SPEED MOD 4); ]
        'S'    [SPEED++; SPEED = (SPEED MOD 4); ]
        '5'    [KFLAG = 1; GOTO CORSOR2;        ]
        '0'    [KFLAG = 0; GOTO CORSOR2;        ]
        ]
    DCSR(0 , 1 , IX , IY , IZ);
    DCSR(2 , 1 , PX , PY , PZ);
    LOCATE(60 , 0); PRINT("X:");     PRINT(PN$( PX)); PRINT("     ");
    LOCATE(60 , 1); PRINT("Y:");     PRINT(PN$(-PY)); PRINT("     ");
    LOCATE(60 , 2); PRINT("Z:");     PRINT(PN$( PZ)); PRINT("     ");
    LOCATE(60 , 3); PRINT("POINT:"); PRINT(    PMAX); PRINT("    ");
    LOCATE(60 , 4); PRINT("WIRE :"); PRINT(    WMAX); PRINT("    ");
    LOCATE(60 , 5); PRINT("SPEED:"); PRINT(   SPEED); PRINT("   ");
  GOTO CORSOR1;
    CORSOR2:
  END;

KEYCNV(KEY)
  BEGIN
    CASE  KEY OF
    [
        ' '     KEY = '0';      (*      CANSEL KEY      *)
        $0D     KEY = '5';      (*      SET    KEY      *)
        $1E     KEY = '8';      (*      UP              *)
        $1F     KEY = '2';      (*      DOWN            *)
        $1C     KEY = '6';      (*      RIGHT           *)
        $1D     KEY = '4';      (*      LEFT            *)
        'J'     KEY = '1';      (*      LEFT,DOWN       *)
        'K'     KEY = '3';      (*      RIGHT,DOWN      *)
        'U'     KEY = '7';      (*      LEFT,UP         *)
        'I'     KEY = '9';      (*      RIGHT,UP        *)
    ]
  END(KEY);

DCSR(MM , MS , LX , LY , LZ)
  ARRAY WORD    COR[11]=[ %-2,% 2,% 0,
                          % 2,% 2,% 0,
                          % 2,%-2,% 0,
                          %-2,%-2,% 0],
        WORD    COS[11],
        BYTE     COW[7]=[ 0,1 , 1,2 , 2,3 , 3,0];
  VAR   I;
  BEGIN
    @MODE(MM , MS);
    @WINDOW(        1 ,                  1 , 198       ,                98);
    @BOX(    LX +  98 ,  49  -  LZ ./.   2 , LX  + 100 ,   51 - LZ ./.   2);
    @WINDOW(        1 , 101                , 198       ,               198);
    @BOX(    LX +  98 ,  LY ./.  2  +  149 , LX  + 100 ,  LY ./. 2  +  151);
    @WINDOW(      201 ,                101 ,       398 ,               198);
    @BOX(    LZ + 298 ,  LY ./.  2  +  149 , LZ  + 300 ,  LY ./. 2  +  151);
    @WINDOW(      201 ,                  1 ,       398 ,                98);
    FOR I = 0 TO 3
    [
        COS[I * 3    ]=COR[I * 3    ] + LX;
        COS[I * 3 + 1]=COR[I * 3 + 1] + LY;
        COS[I * 3 + 2]=COR[I * 3 + 2] + LZ;
    ]
    @SETOD(&COS , 3); @SETWD(&COW , 3);
    _PAR[    0] =  _OFSX; _PAR[     1] =   _OFSY; _PAR[    2] =  _OFSZ;
    _PAR[_HEAD] = _SHEAD; _PAR[_PITCH] = _SPITCH; _PAR[_BANK] = _SBANK;
    @MAGIC(0); @MAGIC(1);
  END;

OMOVE()
  VAR   I;
  BEGIN
    PX = 0; PY = 0; PZ = 0;
    OMOVE1:
      CORSOR();
      IF (KFLAG == 1) [
        FOR I = 0 TO 255
        [
          OBJD0[I][0] = OBJD0[I][0] - PX;
          OBJD0[I][1] = OBJD0[I][1] - PY;
          OBJD0[I][2] = OBJD0[I][2] - PZ;
        ]
        REDRAW();
        BEEP(); BEEP();
        PZ   =         0; PY   =         0; PX   =         0;
        GENZ = GENZ + PZ; GENY = GENY + PY; GENX = GENX + PX;
        GOTO OMOVE1;
      ]
      IF (KFLAG == 0) RETURN;
      GOTO OMOVE1;
  END;

SETP()
  BEGIN
    SETP1:
    CORSOR();
    IF (KFLAG == 1) [
      IF (PMAX < 250) [ DCSR(2 , 2 , PX , PY , PZ);
                        OBJD0[PMAX][0] = PX;
                        OBJD0[PMAX][1] = PY;
                        OBJD0[PMAX][2] = PZ;
                        BEEP(); BEEP();
                        PMAX++;                   ]
                                                  ]
    IF (KFLAG == 0) [ GOTO SETP2; ]
    GOTO SETP1;
    SETP2:
  END;

PMOVE()
  BEGIN
    PMOVE1:
    IF (PMAX == 0) RETURN;
    SELPOT(); BEEP(); BEEP();
    IF (KFLAG == 1) [
      PX = OBJD0[NOWP][0];
      PY = OBJD0[NOWP][1];
      PZ = OBJD0[NOWP][2];
      CORSOR();
      IF (KFLAG == 1) [
        OBJD0[NOWP][0] = PX;
        OBJD0[NOWP][1] = PY;
        OBJD0[NOWP][2] = PZ;
        REDRAW(); BEEP(); BEEP();
      ]
    ]
    IF (KFLAG == 0) RETURN;
    GOTO PMOVE1;
  END;

SETW()
  BEGIN
    SETW1:
    PRINT("\C");
    LOCATE(60 , 3); PRINT("[8] [2]:SELECT POINT");
    LOCATE(60 , 4); PRINT("    [5]:SET 1st,2nd ");
    LOCATE(60 , 5); PRINT("        POSITION    ");
    LOCATE(60 , 6); PRINT("    [0]:ESCAPE      ");
    LOCATE(60 , 7); PRINT("MAX:"); PRINT(PMAX);
    SELPOT();
      IF (KFLAG == 1) [
          IF (WMAX < 250) [
              OBJW0[WMAX][0] = NOWP; BEEP(); BEEP();
              LOCATE(60,10); PRINT("FIRST:" , NOWP);
              SELPOT();
              IF (KFLAG == 1) [
                  OBJW0[WMAX][1] = NOWP; BEEP(); BEEP();
                  WMAX++;
              ]
          ]
      ]
      IF (KFLAG == 0) GOTO SETW2;
      @MODE(2 , 2);
      DLINE(WMAX - 1);
      GOTO SETW1;
    SETW2:
  END;

SELPOT()
  VAR   KEY , OLDP;
  BEGIN
    IF (NOWP .>. PMAX - 1) NOWP = PMAX - 1;
    @PALET(0 , 1 , 2 , 3 , 4 , 5 , 2 , 7);
    DCSR(0 , 1 , PX , PY , PZ);
    DCSR(2 , 1 , OBJD0[NOWP][0] , OBJD0[NOWP][1] , OBJD0[NOWP][2]);
    SELPOT1:
    KEY = INKEY(0);
    IF (KEY == 0) GOTO SELPOT1;
    OLDP = NOWP;
    KEY = KEYCNV(KEY);
    CASE KEY OF [
        '8'     [IF (NOWP .<. PMAX - 1) NOWP++;]
        '2'     [IF (NOWP .>.        0) NOWP--;]
        '5'     [KFLAG = 1;       GOTO SELPOT2;]
        '0'     [KFLAG = 0;       GOTO SELPOT2;]
        ]
    LOCATE(60 , 0); PRINT("POINT NUMBER:" , NOWP , "     ");
    LOCATE(68 , 1); PRINT("WIRE:"         , WMAX , "     ");
    DCSR(0 , 1 , OBJD0[OLDP][0] , OBJD0[OLDP][1] , OBJD0[OLDP][2]);
    DCSR(2 , 1 , OBJD0[NOWP][0] , OBJD0[NOWP][1] , OBJD0[NOWP][2]);
    GOTO SELPOT1;
    SELPOT2:
    DCSR(0 , 1 , OBJD0[OLDP][0] , OBJD0[OLDP][1] , OBJD0[OLDP][2]);
    @PALET(0 , 1 , 2 , 3 , 4 , 5 , 6 , 7);
  END;

SAVE()
  VAR   PARA;
  BEGIN
    PARA = GETFNAME();
    IF (PARA == -1) RETURN;
    MEMW[DTADR] = 0; MEMW[SIZE] = $1000; MEMW[EXADR] = 0;
    ^A = 1; ^DE = FNAD; CALL(FILE);     (* FILE SET  *)
    CALL(WOPN);                         (* FILE OPEN *)
    GETREG(); IF (^CARRY == 1) RETURN;
    MEMW[DTADR] = $9000;
    CALL(WRD);                  (* SAVE      *)
  END;

LOAD()
  VAR   PARA;
  BEGIN
    PARA = GETFNAME();
    IF (PARA == -1) RETURN;
    LOADFILE(FNAD);
    REDRAW();
 END;

GETFNAME()
  VAR   PARA , I;
  BEGIN
    @PALET(0 , 0 , 0 , 0 , 0 , 0 , 0 , 0);
    CALL($2006);
    PRINT("INPUT FILE NAME\N");
    PARA  = LINPUT(FNAD , 18);
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
    @PALET(0 , 1 , 2 , 3 , 4 , 5 , 6 , 7);
  END(PARA);

LOADFILE(FADR)
  BEGIN
    ^A = 1; ^DE = FADR; CALL(FILE);     (* FILE SET  *)
    CALL(ROPN);                         (* FILE OPEN *)
    MEMW[DTADR] = $9000;
    GETREG(); IF (^CARRY == 1) RETURN;
    CALL(RDD);                          (* LOAD      *)
  END;

DLINE(POINT)
  VAR   X1 , X2 , Y1 , Y2 , Z1 , Z2 , I;
  BEGIN
    @WINDOW(200 , 0 , 399 , 99);
    _PAR[    0] = _OFSX;  _PAR[     1] =   _OFSY; _PAR[    2] =  _OFSZ;
    _PAR[_HEAD] = _SHEAD; _PAR[_PITCH] = _SPITCH; _PAR[_BANK] = _SBANK;
    FOR I = 0 TO 5
    [
      DUMMYP[I] = OBJD0[OBJW0[POINT][I/3]][(I MOD 3)];
    ]
    @SETOD(&DUMMYP , 1); @SETWD(&DUMMYW , 0);
    @MAGIC(0); @MAGIC(1);
    X1 = DUMMYP[0]; Y1 = DUMMYP[1]; Z1 = DUMMYP[2];
    X2 = DUMMYP[3]; Y2 = DUMMYP[4]; Z2 = DUMMYP[5];
    @WINDOW(  1,  1,198, 98); @LINE(X1+ 99, 50-Z1./.2,X2+ 99, 50-Z2./.2);
    @WINDOW(  1,101,198,198); @LINE(X1+ 99,Y1./.2+150,X2+ 99,Y2./.2+150);
    @WINDOW(201,101,398,198); @LINE(Z1+299,Y1./.2+150,Z2+299,Y2./.2+150);
  END;

REDRAW()
  VAR   I;
  BEGIN
    _GMASK = 6;
    @WINDOW(   1 ,   1 , 198 ,  98); @CFULL(   1 ,   1 , 198 ,  98 , 0,0,0);
    @WINDOW(   1 , 101 , 198 , 198); @CFULL(   1 , 101 , 198 , 198 , 0,0,0);
    @WINDOW( 201 , 101 , 398 , 198); @CFULL( 201 , 101 , 398 , 198 , 0,0,0);
    _GMASK = 7;
    @WINDOW( 200 , 0 , 399 , 99);
    @CFULL(  200 , 0 , 399 , 99 , 0 , 0 , 0);
    @MODE(2 , 0);
    @TLINE(0 - _OFSZ ./. 2 , 0 , 0 , _OFSZ ./. 2 , 0 , 0);
    @TLINE(0 , 0 - _OFSZ ./. 2 , 0 , 0 , _OFSZ ./. 2 , 0);
    @TLINE(0 , 0 , 0 - _OFSZ ./. 2 , 0 , 0 , _OFSZ ./. 2);
    IF (PMAX == 0) GOTO REDRAW2;
    FOR I = 0 TO PMAX - 1
    [
        DCSR(2 , 2 , OBJD0[I][0] , OBJD0[I][1] , OBJD0[I][2]);
    ]
    REDRAW2:
    IF (WMAX == 0) GOTO REDRAW1;
    FOR I = 0 TO WMAX - 1 [ DLINE(I); ]
    REDRAW1:
  END;

ROTATION()
  VAR   I,KEY;
  BEGIN
    @GRAD(0);
    LOCATE(60 , 0); PRINT("[4] [6]:HEAD");
    LOCATE(60 , 1); PRINT("[8] [2]:PITCH");
    LOCATE(60 , 2); PRINT("[1] [3]:BANK");
    LOCATE(60 , 3); PRINT("[Q] [Z]:MOVE");
    ROT1:
      LOCATE(60 , 5); PRINT(" HEAD:", _SHEAD); PRINT("   ");
      LOCATE(60 , 6); PRINT("PITCH:",_SPITCH); PRINT("   ");
      LOCATE(60 , 7); PRINT(" BANK:", _SBANK); PRINT("   ");
      LOCATE(60 , 8); PRINT("   OZ:",  _OFSZ); PRINT("   ");
      ROT2:
        KEY = INKEY(0);
        IF (KEY == 0) GOTO ROT2;
        @MODE(2 , 0);
        @WINDOW(200 , 0 , 399 , 99);
        KEY = KEYCNV(KEY);
        CASE KEY OF [
                $1E     KEY = '8';
                $1F     KEY = '2';
                $1D     KEY = '4';
                $1C     KEY = '6';
        ]
        CASE KEY OF [
                '4'     [_SHEAD=_SHEAD-5;
                          IF (  _SHEAD.<.0)  _SHEAD = _SHEAD+360;       ]
                '6'     [_SHEAD =_SHEAD+5;   _SHEAD =(_SHEAD  MOD 360); ]
                '8'     [_SPITCH=_SPITCH+5;  _SPITCH=(_SPITCH MOD 360); ]
                '2'     [_SPITCH=_SPITCH-5;
                          IF (_SPITCH.<.0)   _SPITCH= _SPITCH+360;      ]
                '3'     [_SBANK= _SBANK-5;
                          IF ( _SBANK.<.0)   _SBANK = _SBANK+360;       ]
                '1'     [_SBANK= _SBANK+5;   _SBANK =(_SBANK  MOD 360); ]
                'Q'     [IF (_OFSZ<1000) _OFSZ=_OFSZ+20;]
                'Z'     [IF (_OFSZ>0)    _OFSZ=_OFSZ-20;]
                '0'     GOTO ROT3;
        ]
        _PAR[ _HEAD] = _SHEAD;
        _PAR[_PITCH] = _SPITCH;
        _PAR[ _BANK] = _SBANK;
        _PAR[     2] = _OFSZ;
        @FULL(200 , 0 , 399 , 99);
        @TLINE(0-_OFSZ./.2,0,0,_OFSZ./.2,0,0);
        @TLINE(0,0-_OFSZ./.2,0,0,_OFSZ./.2,0);
        @TLINE(0,0,0-_OFSZ./.2,0,0,_OFSZ./.2);
        IF ((PMAX > 0) AND (WMAX > 0)) [
            @MODE(2 , 2); @FULL(200 , 0 , 399 , 99);
            @SETOD(&OBJD0 , PMAX    );
            @SETWD(&OBJW0 , WMAX - 1);
            @MAGIC(0);
            @FULL(200 , 0 , 399 , 99);
            @MAGIC(1);
            @MODE(2 , 0);
        ]
        GOTO ROT1;
    ROT3:
    REDRAW();
  END;

ERASEW(NUMBER)
  VAR   I;
  BEGIN
    @MODE(0 , 2); DLINE(NUMBER);
    @MODE(0 , 1); DLINE(NUMBER);
    FOR I = NUMBER TO WMAX [
        OBJW0[I][0] = OBJW0[I + 1][0];
        OBJW0[I][1] = OBJW0[I + 1][1];
    ]
    WMAX--; BEEP(); BEEP();
    IF (WMAX .<. 0) WMAX = 0;
  END;

ERASEP(NUMBER)
  VAR   I , F;
  BEGIN
    DCSR(0 , 2 , OBJD0[NUMBER][0] , OBJD0[NUMBER][1] , OBJD0[NUMBER][2]);
    FOR I = NUMBER TO PMAX - 1 [
        OBJD0[I][0] = OBJD0[I + 1][0];
        OBJD0[I][1] = OBJD0[I + 1][1];
        OBJD0[I][2] = OBJD0[I + 1][2];
    ]
    PMAX--; BEEP(); BEEP();
    F=0;
    IF (WMAX .>. 0) [
        FOR I = 0 TO WMAX [
            IF (OBJW0[I][0] == MEM[&NUMBER]) OR
               (OBJW0[I][1] == MEM[&NUMBER])
            [ ERASEW(I); F = 1; ]
        ]
    ]
    IF (WMAX .>. 0)[
        FOR I = 0 TO WMAX [
            IF (OBJW0[I][0] .>. MEM[&NUMBER]) OBJW0[I][0]--;
            IF (OBJW0[I][1] .>. MEM[&NUMBER]) OBJW0[I][1]--;
        ]
    ]
    IF (F == 1) REDRAW();
  END;

ERAW()
  VAR   KEY , NUMBER;
  BEGIN
    NUMBER = 0;
    @PALET(0 , 1 , 2 , 3 , 4  , 5 , 2 , 7);
    IF (WMAX == 0) GOTO ERAW3;
    LOCATE(60 , 0); PRINT("[8] [2]:SELECT");
    LOCATE(60 , 1); PRINT("    [5]:ERASE WIRE");
    LOCATE(60 , 2); PRINT("    [0]:ESCAPE");
    ERAW1:
    LOCATE(60 , 4); PRINT("   PMAX:",   PMAX,".    ");
    LOCATE(60 , 5); PRINT("   WMAX:",   WMAX,".    ");
    LOCATE(60 , 6); PRINT("  ERASE:", NUMBER,".    ");
    KEY=INKEY(0);
    IF (KEY == 0) GOTO ERAW1;
    @MODE(0 , 1); DLINE(NUMBER);
    KEY = KEYCNV(KEY);
    CASE KEY OF
    [
        '2'     [IF (NUMBER >        0) NUMBER--;         ]
        '8'     [IF (NUMBER < WMAX - 1) NUMBER++;         ]
        '5'     [ERASEW(NUMBER);
                 IF (NUMBER > WMAX - 1) NUMBER = WMAX - 1;]
        '0'     [GOTO ERAW2;]
    ]
    IF (WMAX == 0) GOTO ERAW2;
    @MODE(2 , 1); DLINE(NUMBER);
    GOTO ERAW1;
    ERAW2:
    REDRAW();
    ERAW3:
    @PALET(0 , 1 , 2 , 3 , 4 , 5 , 6 , 7);
  END;

ERAP()
  VAR   KEY , NUMBER;
  BEGIN
    IF (PMAX == 0) GOTO ERAP3;
    @PALET(0 , 1 , 2 , 3 , 4 , 5 , 2 , 7);
    NUMBER = 0;
    LOCATE(60 , 0); PRINT("[8] [2]:SELECT");
    LOCATE(60 , 1); PRINT("    [5]:ERASE WIRE");
    LOCATE(60 , 2); PRINT("    [0]:ESCAPE");
    ERAP1:
    LOCATE(60 , 4); PRINT("   PMAX:",  PMAX,".    ");
    LOCATE(60 , 5); PRINT("   WMAX:",  WMAX,".    ");
    LOCATE(60 , 6); PRINT("  ERASE:",NUMBER,".    ");
    KEY = INKEY(0);
    IF (KEY == 0) GOTO ERAP1;
    DCSR(0 , 1 , OBJD0[NUMBER][0] , OBJD0[NUMBER][1] , OBJD0[NUMBER][2]);
    KEY = KEYCNV(KEY);
    CASE KEY OF
    [
        '2'     [IF (NUMBER > 0)        NUMBER--;         ]
        '8'     [IF (NUMBER < PMAX - 1) NUMBER++;         ]
        '5'     [ERASEP(NUMBER);
                 IF (NUMBER > PMAX - 1) NUMBER = PMAX - 1;]
        '0'     [GOTO ERAP2;]
    ]
    IF (PMAX == 0) GOTO ERAP3;
    DCSR(2 , 1 , OBJD0[NUMBER][0] , OBJD0[NUMBER][1] , OBJD0[NUMBER][2]);
    GOTO ERAP1;
    ERAP2:
    REDRAW();
    ERAP3:
    @PALET(0 , 1 , 2 , 3 , 4 , 5 , 6 , 7);
  END;

READY()
  VAR   KEY;
  BEGIN
    LOCATE(60 , 15); PRINT("OK ?(Y/N)");
    READY1:
    KEY = INKEY(0);
    IF (KEY == 0) GOTO READY1;
    CASE KEY OF
    [
        'Y'     KFLAG = 1;
        'N'     KFLAG = 0;
        OTHERS  GOTO READY1;
    ]
    PRINT("\C");
  END;

LOOK()
  VAR   I , KEY , X1 , Y1 , X , Y , X2 , Y2;
  BEGIN
    IF (PMAX == 0) OR (WMAX == 0) RETURN;
    MPOT = 0;
    FOR I = 0 TO 8 [ _PAR[I] = 0; ]
    @INIT();
    @MODE(2 , 0);
    @SETOD(&OBJD0 , PMAX);
    @SETWD(&OBJW0 , WMAX - 1);
    @CRTKN(7 , 0 , 0); @CLS(); @MAGIC(0); @MAGIC(1);
    @CRTKN(7 , 0 , 0); @CLS();
    _PAR[2]=0;
    _PAR[1]=0;
    GAID();
    LOOP:
        KINP();
        @MAGIC(0); @MAGIC(1);
        @CRTKN(7 , 0 , 0);
        @CLS();
        IF (KFLAG == 1) GOTO LEND1;
        LOCATE(0 , 10);
        PRINT("SPEED : " , MPOT              ,      "\N");
        PRINT("HEAD  : " , PN$(_PAR[_HEAD])  , "     \N");
        PRINT("PITCH : " , PN$(_PAR[_PITCH]) , "     \N");
        PRINT("BANK  : " , PN$(_PAR[_BANK])  , "     \N");
        PRINT("X     : " , PN$(_PAR[0])      , "     \N");
        PRINT("Y     : " , PN$(_PAR[1])      , "     \N");
        PRINT("Z     : " , PN$(_PAR[2])      , "     \N");
    GOTO LOOP;
    LEND1:
    WSCR(); REDRAW();
  END;

GAID()
  BEGIN
    PRINT("[4] [6] : X-MOVE\N");
    PRINT("[8] [2] : Y-MOVE\N");
    PRINT("[W] [X] : Z-MOVE\N");
    PRINT("[1] [3] : HEAD ROTATION\N");
    PRINT("[Q] [Z] : PITCH ROTATION\N");
    PRINT("[7] [9] : BANK ROTATION\N");
    PRINT("[0]     : ESCAPE\N");
    PRINT("[+]     : SPEED CHANGE\N");
    PRINT("[H]     : HELP");

#IF (_PLOTSW == 1)

    PRINT("[P]     : PLOTTER");

#ENDIF

  END;

KINP()
  VAR   KEY;
  BEGIN
        KEY   = 0; WHILE(KEY == 0) KEY = INKEY(0);
        KFLAG = 0;
        KEY = KEYCNV(KEY);
        CASE KEY OF [
         'H'    [ HELP(); GAID(); ]
         '4'     _PAR[0] = _PAR[0] - LSTEP[MPOT];
         '6'     _PAR[0] = _PAR[0] + LSTEP[MPOT];
         '8'     _PAR[1] = _PAR[1] + LSTEP[MPOT];
         '2'     _PAR[1] = _PAR[1] - LSTEP[MPOT];
         'W'     _PAR[2] = _PAR[2] - LSTEP[MPOT];
         'X'     _PAR[2] = _PAR[2] + LSTEP[MPOT];
         '0'     KFLAG   = 1;
         '1'    [ _PAR[_HEAD] =_PAR[_HEAD]-5;
                   IF (_PAR[_HEAD].<.0)
                  _PAR[_HEAD] = _PAR[_HEAD] +360;       ]
         '3'    [ _PAR[_HEAD] = _PAR[_HEAD] +5;
                  _PAR[_HEAD] =(_PAR[_HEAD] MOD 360);   ]
         '7'    [ _PAR[_BANK] = _PAR[_BANK] +5;
                  _PAR[_BANK] =(_PAR[_BANK] MOD 360);   ]
         '9'    [ _PAR[_BANK] = _PAR[_BANK] -5;
                   IF (_PAR[_BANK].<.0)
                  _PAR[_BANK] = _PAR[_BANK] +360;       ]
         'Q'    [ _PAR[_PITCH]= _PAR[_PITCH]+5;
                  _PAR[_PITCH]=(_PAR[_PITCH] MOD 360);  ]
         'Z'    [ _PAR[_PITCH]= _PAR[_PITCH]-5;
                   IF (_PAR[_PITCH].<.0)
                  _PAR[_PITCH]= _PAR[_PITCH]+360;       ]

#IF (_PLOTSW == 1)

         'P'     @PLOT();

#ENDIF

         'S'    [ MPOT++; MPOT = (MPOT MOD 6);]
         '+'    [ MPOT++; MPOT = (MPOT MOD 6);]
                ]
END;
