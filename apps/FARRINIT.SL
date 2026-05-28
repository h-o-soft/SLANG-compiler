/* ARRAY FLOAT 初期値付き宣言テスト */

CONST PI = 3.1415926;

/* T1: FloatLiteral 初期値 */
ARRAY FLOAT GA[3] = {1.5, 2.5, 3.5};

/* T2: 整数→FLOAT 自動変換 */
ARRAY FLOAT GB[3] = {1, 2, 3};

/* T3: CONST + 定数式 */
ARRAY FLOAT GC[3] = {PI, PI / 2.0, PI * 2.0};

/* T4: 部分初期値 + 0 埋め (5 要素中 2 つだけ) */
ARRAY FLOAT GD[5] = {1.5, 2.5};

VAR I;

MAIN()
{
  PRINT("T1 GA=");
  FOR I = 0 TO 2 PRINT(FL$(GA[I]), " ");
  PRINT(/);

  PRINT("T2 GB=");
  FOR I = 0 TO 2 PRINT(FL$(GB[I]), " ");
  PRINT(/);

  PRINT("T3 GC=");
  FOR I = 0 TO 2 PRINT(FL$(GC[I]), " ");
  PRINT(/);

  PRINT("T4 GD=");
  FOR I = 0 TO 5 PRINT(FL$(GD[I]), " ");
  PRINT(/);
}
