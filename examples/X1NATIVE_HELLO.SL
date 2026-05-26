// X1 native runtime (= OS 非依存) の動作確認 sample
// build: slangbuild -E x1native -I include examples/X1NATIVE_HELLO.SL -o HELLO
// 実行: X1 emulator (xmil 等) の monitor で .bin を $1000 に load → PC=$1000 jump
// 期待: "HELLO, X1 NATIVE!" 表示 + キー入力で停止解除

MAIN()
BEGIN
    PRINT("HELLO, X1 NATIVE!");
    PRINT(/);
    PRINT("PRESS ANY KEY...");
    PRINT(/);
    INKEY(0);
END;
