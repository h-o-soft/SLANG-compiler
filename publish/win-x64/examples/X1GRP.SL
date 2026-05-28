//////////////////////////////
// X1 グラフィック関連サンプル
//////////////////////////////


// PAINT2関数に渡すパラメータ
// X,Y(16bit), 塗り色(8bit), 境界色(8bit)の順に並べる
// ※SLANGの仕様として%をつけると16bitになる
ARRAY PAINTDAT[]={
    %150,%70,$12,1,2,3,4,5,6,7
};

// 手抜きのBOX描画
BOX(X1,Y1,X2,Y2,COL)
{
    LINE(X1,Y1,X2,Y1,COL);
    LINE(X2,Y1,X2,Y2,COL);
    LINE(X2,Y2,X1,Y2,COL);
    LINE(X1,Y2,X1,Y1,COL);
}

MAIN()
VAR I,J,K;
{
    WIDTH(40);
    GRPSETUP();
    GRDISP(1);
    GRCLS();


    // LINEのテスト(以降、ESCキーで次に進む)
    LOOP
    {
        IF INKEY(0)==$1B THEN EXIT;
        LINE(RND(320),RND(200),RND(320),RND(200),RND(8));
    }

    BFILL(0,0,319,199,0);

    // とりあえずラインで囲んだところを塗ってみる
    LINE(10,30,200,60,1);
    LINE(200,60,300,40,1);
    LINE(300,40,230,120,1);
    LINE(230,120,300,180,1);
    LINE(300,180,150,100,1);
    LINE(150,100,60,140,1);
    LINE(60,140,90,70,1);
    LINE(90,70,10,30,1);

    // PAINT命令では塗り位置の色以外全てを境界色にする
    PAINT(150,70,$23);
    PAINT(150,10,$15);

    // // PAINT1()は第三引数上位に1つだけ境界色を指定出来る
    // PAINT1(150,70,$123);

    // // PAINT2()は第一引数に塗り情報、第二引数に塗り色と境界色の数の合計を指定して
    // // 任意の境界色で塗れる
    // PAINT2(PAINTDAT,8);

    LOOP
    {
        IF INKEY(0)==$1B THEN EXIT;
    }

    // タイルで塗れるBOX FILLのテスト
    LOOP
    {
        IF INKEY(0)==$1B THEN EXIT;
        J = RND(319);
        K = RND(199);
        BFILL(J,K,J+RND(320-J),K+RND(200-K),RND(8)*16+RND(8));
    }

    // WIDTH 80に切り替え
    GRDISP(0);
    WIDTH(80);
    GRPSETUP();
    BFILL(0,0,639,199,0);
    GRDISP(1);

    // ペイントルーチンで塗れる色全てのサンプル表示
    BOX(400,4*40+8,620,200-8,7);
    LOCATE(58,22);
    PRINT("COLOR SAMPLE");
    FOR I=0 TO 7
    {
        LOCATE(I*5+13,20);
        PRINT(I);
        LOCATE(52, 18-I*20/8);
        PRINT(I);
    }
    FOR I=0 TO 7
    {
        FOR J=I DOWNTO 0
        {
            IF I == 1 OR J == 1 THEN {
                IF I == 2 OR J == 2 THEN {
                    K = 3;
                } ELSE {
                    K = 2;
                }
            } ELSE {
                K = 1;
            }
            BFILL(I*40+97, 140-J*20, I*40 + 134,158-J*20,K);
            PAINT(I*40+97,140-J*20,I*16+J);
        }
    }

    LOOP{}
}