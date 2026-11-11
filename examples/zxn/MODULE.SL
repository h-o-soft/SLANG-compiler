// MODULE.SL - #MODULE を Next のページに入れて使う見本 (docs/SLANG-spec.md の「Next のページ」)
//   slangbuild -E zxn MODULE.SL -o MODULE --emit nex   → MODULE.nex (モジュールはそれぞれのページに入る)
// C000H の窓 (MMU6) に 2 つのモジュールを入れ替えて呼ぶ。0000H〜3FFFH を RAM にして (__LOWRAM)、0010H にもモジュールを置く
CONST __NOROM = 1;
CONST __LOWRAM = 30;              // 0000H〜1FFFH にページ 30、2000H〜3FFFH にページ 31
CONST __STACKTOP = $C000;         // 本体のスタックと WORK を、C000H〜DFFFH の窓の外へ
WORK $A000;
VAR I, R;

MAIN()
BEGIN
	FOR I=0 TO 6143 BEGIN MEM[$4000+I] = 0; END;     // 画面を消して、白地に黒 (属性 38H)
	FOR I=0 TO 767 BEGIN MEM[$5800+I] = $38; END;
	PRINT("MODULE SAMPLE", /);
	ZXN_MODULE(0);                // モジュール 0 (ページ 40) を C000H の窓に付ける
	R = TENX(3);
	PRINT("TENX(3)=", R, /);
	ZXN_MODULE(1);                // モジュール 1 (ページ 41) に替える
	R = HUNDX(3);
	PRINT("HUNDX(3)=", R, /);
	ZXN_MODULE(0);                // モジュール 0 に戻す。私有の変数 CNT は残っている
	R = TENX(5);
	PRINT("TENX(5)=", R, " CALLS=", TENCALLS(), /);
	ZXN_MODULE(2);                // 0010H のモジュール (ページ 30。始まりで付いている)
	R = LOWX(7);
	PRINT("LOWX(7)=", R, /);
	LOOP {}
END;

#MODULE $C000 PAGE 40
VAR CNT;
TENX(X) BEGIN CNT++; RETURN X * 10 + 1; END;
TENCALLS() BEGIN RETURN CNT; END;
#END

#MODULE $C000 PAGE 41
HUNDX(X) BEGIN RETURN X * 100 + 2; END;
#END

#MODULE $0010 PAGE 30
LOWX(X) BEGIN RETURN X * 1000 + 3; END;
#END
