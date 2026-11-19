# x1 の env の見本 (画面・音・入力)

`x1` の env (LSX-Dodgers の上で動く X1 のプログラム) の見本です。どれもこのリポジトリのために新しく書いたものです。
ブラウザ向けの束 (`scripts/build-hubasic-wasm.sh`) の試験データにもなります。束の中の `testdata/slang/` に、
この SL と、slangbuild・AILZ80ASM で組んだ `.ASM`・`.bin` が入ります。

| ファイル | 内容 |
|---|---|
| `SCREEN.SL` | `WIDTH`・`LOCATE`・`TATTR` の文字と色、`PCGDEF` の PCG、`LINE`・`BFILL` のグラフィック |
| `SOUND.SL` | `PORT` で PSG のレジスタに書く音、`VSYNC` で待つ、`BEEP` |
| `INPUT.SL` | `INKEY` のキーと `STICK` のジョイスティック (ESC で終わる) |

組み立て:

```sh
slangbuild -E x1 -I include examples/x1pen/SCREEN.SL -o SCREEN --emit bin
```

`VSYNC` を使うプログラムは、フレームごとに呼ばれる `VSYNC_PROC()` を書きます (何もしなくても)。
