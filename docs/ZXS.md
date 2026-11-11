# 素の ZX Spectrum (zxs)

`-E zxs` は、素の ZX Spectrum (Next ではないもの) 向けの環境です。48K の機械として動くプログラムを作ります。
48K・128K・+2・+2A・+3 のどれでも、BASIC の読み込み口から起動すれば同じに動きます (128K・+2A でも、`RANDOMIZE USR` で入ったときは 0000H に 48 BASIC の ROM があります)。

## 組み方

```sh
slangbuild -E zxs NAME.SL -o NAME --emit tape
```

- `NAME.bin` と、テープの `NAME.tap`・`NAME.tzx` ができます
- テープは 4 つのブロック: BASIC のヘッダと本体 (`10 CLEAR VAL "org-1":LOAD ""CODE :RANDOMIZE USR VAL "org"`)、CODE のヘッダと本体
  - TZX は同じ 4 つのブロックを ID 10H (標準の速さ) で包んだもの
  - 番地は組み上がったコードの頭 (`__CODE__`) から作ります。ソースの `ORG` もそのまま効きます
  - テープの名前は `--tape-name`、env の `tape.name`、出力の名前の順で、大文字にして 10 文字まで
  - `--wav`・`--tape-add`・`--tape-load`・`--tape-exec`・`#MODULE` (多段のテープ) は使えません (エラー)

## 置き場

| 番地 | 中身 |
|---|---|
| 8000H〜 | コード (ORG) |
| D000H〜 | ワーク (WORK) |
| 〜FD00H | スタック (`CONST __STACKTOP` で変えられる) |
| FDFDH〜FF00H | 割り込みの表と処理 (始まりで作る) |

- slangbuild は、コード・ワーク・スタックが 8000H〜FCFFH に収まり、重ならないことを調べます (zxn と同じ検査)

## 使えるもの

- 表示 (`PRINT` など。字は 48 BASIC の ROM の 3D00H)、`LOCATE`、16 進・10 進
- キー: `INKEY`・`GETC` (キーのポートを読む。値は ROM の L モード・CAPS LOCK が切れているときと同じ)、`GETKEY(0〜7)` (半行のポートの値)
- `ZXN_VSYNC` (HALT。フレームの割り込みで戻る)、`STOP`、`STICK(0)` (Kempston、1FH)
- 計算・FLOAT・配列・制御 (ほかの env と同じ)
- `GETKEY(8・9)` と `STICK(1)` は Next だけのもの (素の Spectrum では意味の無い値)

**Next だけの部品** (Layer 2・スプライト・タイル・パレット・コッパー・NextReg・バンク・CPU の速さ・ファイル (esxDOS)・NextDAW・`ZXN_SETIM2`) を使うと、コンパイルのときにエラーになります:

```
slangc: error: SPR_MOVE needs ZX Spectrum Next (env zxs)
```

## しくみ

- zxn の部品をそのまま使い、env の `variants: [__NOROM, __ZXS]` で、いつも次のように差し替えます (名前と出力の順は元のまま)
  - `GETC` → `GETC__NOROM` (ポートを読む。ROM の `KEY_CODE` は IY を ROM の変数と思って読むので使わない)
  - `PRT` → `PRT__ZXS` (画面の番地を素の Z80 で計算、字は ROM の 3D00H)
  - `SLANGINIT` → `SLANGINIT__ZXS` (素の Z80 の IM 2。FE00H〜FF00H を FDH にし、FDFDH に `EI`・`RETI`)
- ROM の IM 1 (0038H) は使いません: ROM の割り込みの処理は IY を 5C3AH (ROM の変数) と思って書き替えますが、slangc の IY はローカル変数の場所です
- Next だけの部品には `; @requires next` の印があり、env の `features` (zxn は `[next]`、zxs は `[]`) に無いものを使うとエラーにします

## 128K だけのもの

AY (Arkos など)・バンクの切り替え・影の画面は、まだありません。バンクを替えると、C000H〜にある割り込みの表が消えて落ちます。

## 確かめ方

- ZEsarUX (`--machine 48k|128k|P2A41 --vo sdl --tape NAME.tap`) で、TAP・TZX を読み込んで走ります
- CSpect は `-zxnext` を付けなければ 48K として TAP を走らせられます (TZX と 128K の形はできない)
- `examples/zxs/ZXSTEST.SL` は、ROM・字・計算・FLOAT・HALT・割り込みの表を調べて、画面に OK/NG を出します
