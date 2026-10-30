# HuBASIC から SLANG への変換 (アルファ版)

X1 の HuBASIC (CZ-8FB01 系) で書いた行番号付きの BASIC のテキストを SLANG のソースに変換し、
SLANG compiler の `x1native` / `x1native_slfs` 環境で X1 の機械語のプログラムにする変換ツールです。
変換したプログラムは、OS なしで起動するディスクイメージ (`.d88`) やテープ (`.tap`) になります。

**アルファ版です。** 対応している命令・関数はまだ一部で、実際の HuBASIC と動きが違う所があります (下の「HuBASIC との違い」)。

## 使い方

変換ツールの実行ファイルは `hubasic2sl` です。1 行で、BASIC からディスクイメージかテープを作ります。

```sh
hubasic2sl PROG.BAS --emit disk --wait line:480   # PROG.d88 (ディスクイメージ) を作る (--wait は下を参照)
hubasic2sl PROG.BAS --emit tape --wait line:480   # PROG.tap (テープ) を作る
```

- 中では、BASIC を SLANG に変換し (`PROG.SL`)、`slangbuild` で組み立て (`PROG.bin`・`PROG.LST`)、メモリ ($1000〜$FFDF) に収まるかを確かめています。
  できたファイルはどれも残ります。名前は、入力 (か `-o`) の拡張子を `.SL`・`.d88`・`.tap` にしたものです
  (`-o out/GAME` でも `-o out/GAME.d88` でも `out/GAME.SL`・`out/GAME.d88`)。
- 途中で止まったときは、最後の 1 行が「変換で止まりました」「組み立て (slangbuild) で止まりました」「大きさの検査で止まりました」のどれかになり、
  イメージは残りません (前の回のイメージも消えます)。終了コードは 1 (オプションの誤りは 2)。
- 変換のオプション (`--wait`・`--string-len`・`--no-line-numbers` など) は、`--emit` と一緒に、どの順でも書けます。
- `--emit disk` は `x1native_slfs`、`--emit tape` は `x1native` の環境で組み立てます。
- ディスクイメージでは、BASIC のファイル名 (拡張子なし。13 文字まで、ASCII だけ) が IPL の起動名になります
  (変換した SLANG の頭の `// disk-main-name:` の行。`--disk-main-name <name>` で変えられます。[SLFS.md](SLFS.md))。
- テープの名前は `PROG` です (`--tape-name <name>` で変えられます)。`--wav` で `.wav` も作れます ([X1.md](X1.md) を参照)。
- slangbuild は、hubasic2sl と同じ所、その下の `bin/`、開発で publish したもの、PATH、`dotnet run` (開発のとき) の順に探します。
  `--slangbuild <path>` で決めることもできます。
- 使える条件は slangbuild を手で使うときと同じです: 配布の zip を解いたフォルダで setup-tools (AILZ80ASM などを取ってくる) をしてから install する
  (順が逆なら install し直す)。または、解いたフォルダの中で動かす。install の `--config-dir` を変えたときは `SLANG_HOME` をそこに向けます。
  条件が足りないと、slangbuild の `env not found` などの後に「組み立て (slangbuild) で止まりました」になります。
- 途中を分けて動かすときは、今までどおり次の 3 行です (`--emit` は中でこれをしています)。

  ```sh
  hubasic2sl PROG.BAS -o PROG.SL --wait line:480    # 変換
  slangbuild --emit disk -E x1native_slfs PROG.SL -o PROG   # PROG.d88 を作る (テープは --emit tape -E x1native)
  hubasic2sl --check-bin PROG.bin --lst PROG.LST     # メモリ ($1000〜$FFDF) に収まるかを確かめる
  ```
- サンプル: [examples/hubasic/](../examples/hubasic/)

### 入力のファイル

- 行番号付きの、ふつうの BASIC のテキストです。文字コードは UTF-8 (半角カナはそのまま書けます)。
- X1 でアスキーセーブしたファイルを、そのまま (X1 の文字コードのまま) 渡すこともできます。UTF-8 として読めないファイルは
  X1 の文字コードとして読み、warning HB0110 が出ます (グラフィックの文字・カナ・カーソルの制御コードもそのまま)。
  `--input-encoding auto|utf8|sjis|x1` で決めることもできます。

### オプション

| オプション | 意味 |
|---|---|
| `-o <path>` | 出力する SLANG のファイル (既定: 入力の拡張子を `.SL` にしたもの。`--emit` のときは拡張子を除いた名前が、できるファイルの名前の元) |
| `--env <name>` | `x1native` (既定) / `x1native_slfs` (`--emit` のときは `--emit` で決まり、合わない値は誤り) |
| `--wait <mode>` | `none` (既定。待たない)、`line:N` (各行の頭で約 0.1ms × N 待つ。N=450〜500 で HuBASIC に近い速さの目安。ループが 1 行の中で回る場合は効かない)、`frame` (`'#VSYNC n` と書いた所で n フレーム待つ。n がなければ 1。`frame:N` とも書けるが N は使わない) |
| `--input-encoding <enc>` | `auto` (既定) / `utf8` / `sjis` / `x1` |
| `--emit-line-comments` | 元の BASIC の行をコメントで出す |
| `--assume-defint` | 接尾辞も DEFxxx もない変数を整数にする (HuBASIC の既定は浮動小数。整数のほうがずっと速い) |
| `--string-len <N>` | 文字列 1 本の長さの上限 (1〜255、既定 255)。文字列の変数と文字列の配列の要素は N+1 バイトずつ取るので、メモリが減る。N を超える文字列を変数に入れると、切り詰めずに String too long で止まる。文字列の定数・計算の途中・つなげたときの上限は 255 のまま |
| `--no-line-numbers` | 各行の頭 (と GOSUB の戻り先・`LABEL` の所) で実行中の行番号を書かない。1 か所 6 バイト・約 6.5 マイクロ秒の分、小さく速くなる。実行時エラーは「<メッセージ>」と「Ok」だけになり、` in <行番号>` が出ない |
| `--data-budget <bytes>` | 静的データの上限 (既定 24576) |
| `--check-bin <bin>` / `--lst <lst>` | 組み立てた後の大きさを検査する |
| `--emit disk` / `--emit tape` | 変換・組み立て・大きさの検査を続けてして、ディスクイメージ (`.d88`) かテープ (`.tap`) を作る (上の「使い方」) |
| `--disk-main-name <name>` | IPL の起動名 (`--emit disk` のとき。既定は BASIC のファイル名) |
| `--tape-name <name>` | テープの名前 (`--emit tape` のとき。既定は `PROG`) |
| `--wav` | `.wav` も作る (`--emit tape` のとき) |
| `--slangbuild <path>` | 呼ぶ slangbuild (`--emit` のとき。既定は上の順に探す) |

### 変換に失敗したとき

```
prog.bas:2:4: error HB0301 (BASIC 20): 未対応の命令 CIRCLE
```

- 「ファイル名:ファイルの行:桁」「エラーの番号」「(BASIC の行番号)」「内容」の順です。
- 構文のエラーがあると、そこで止まって型の検査まで進みません。直すと、次のエラーが出ることがあります。
- warning は変換を止めません (結果が HuBASIC と違うかもしれない、というお知らせ)。

### ブラウザで使う (wasm、X1Pen などに組み込むための材料)

変換だけを .NET の browser-wasm で動かす入口があります (`src/SLANGCompiler.HuBasic.Wasm/`。SLANGCompiler.sln には入っていません)。
組み立てと試験は次の 1 行です (要るもの: dotnet SDK、python3、AILZ80ASM (`AILZ80ASM_PATH`・PATH・`tools/` の順に探す)。
node 22.7 以上があれば、最後に wasm の変換が CLI と同じかを確かめます。`NODE` で指せます)。

```sh
scripts/build-hubasic-wasm.sh out/hubasic-wasm
```

できるフォルダ:

- `_framework/`: wasm の一式 (`dotnet.js` から読む)
- `runtime/`: `x1native`・`x1native_slfs` の env の libraries の `.asm` (LSX 用は入りません。混ぜないでください)
- `env/`: 2 つの env ファイルの写し
- `testdata/`: 試験用のプログラム 4 本 (HELLO・GRAPH・MUSIC・FLOAT) の `.BAS`・`.SL`・`.bin`・`.LST`・`.d88`。
  同じ木の hubasic2sl・slangbuild (`--emit disk -E x1native_slfs`)・slangc で作ったもので、ブラウザの側の道具と比べるのに使う
  (D88 は日付が 0 で、同じ入力からは同じバイトになる)
- `manifest.json`: 組み立てた SHA (`slangCompilerRevision`)、`dirty`、版、組の印の番号 (`basAbi`、下を参照)、`loadAddress` ($1000)・`reservedStart` ($FFE0)、
  env ごとの `envType`・`osType`・`defaultOrg`・`defines`・`libraries` (順番どおり)・`defaultDiskMainName`・`diskMainLoad`・`diskMainExec`・`diskVolume`、
  testdata を作ったアセンブラ (`assembler`: 版と sha256)、`testdata` (ケースごとのファイルと起動名)、
  束の全部のファイルの `files` (path・sha256・size)

スクリプトは、作ったあとに束が manifest と合っているか (ファイルの集まり・大きさ・sha256・testdata) を確かめます。
決めた SHA から作るときは、`git worktree add <一時フォルダ> <SHA>` をして、その中のスクリプトを動かします
(一時の木には AILZ80ASM が無いので、`AILZ80ASM_PATH` で指します)。

wasm とランタイムは、同じ SHA の一式をまとめて使います (`dirty: true` の一式は使わない)。SLANG のコンパイラとアセンブラに渡す値は、今の slangc・slangbuild と同じにします:
コンパイラには `ENV_TYPE`・`OS_TYPE` (前もって決めた定数)、`defines`、`defaultOrg`。アセンブラには `OS_TYPE`・`ENV_TYPE` と `defines`。

JS からの呼び方:

```js
const { dotnet } = await import('./hubasic-wasm/_framework/dotnet.js');
const runtime = await dotnet.create();
const api = (await runtime.getAssemblyExports('hubasic2sl-wasm')).SLANGCompiler.HuBasic.Wasm.HuBasicWeb;

const bytes = new Uint8Array(await (await fetch('PROG.BAS')).arrayBuffer());
const r = JSON.parse(api.Transpile(bytes, 'PROG.BAS', JSON.stringify({ wait: 'line:480' })));
// r = { ok, sl, diskMainName, diagnostics: [{ severity, code, message, sourceLine, column, basicLine, text }] }
// オプションの誤りは { ok: false, error }

const c = JSON.parse(api.CheckLayout(binLength, workEnd));   // workEnd はアセンブラの記号 __WORKEND__ の値
// c = { ok, message, loadAddress, reservedStart }。message は --check-bin と同じ 1 行。__WORKEND__ が無いときは呼ぶ側で失敗にする
const v = JSON.parse(api.Version());                          // { version, informational: "0.1.0-alpha+<SHA>", basAbi }
```

- `Transpile` の第 3 引数は、CLI のオプションに当たる項目の JSON です (省いた項目は CLI の既定。空文字列は `{}` と同じ):
  `inputEncoding` (`"auto"`・`"utf8"`・`"sjis"`・`"x1"`)、`wait` (`"none"`・`"line:N"`・`"frame"` など。`--wait` と同じ読み方)、
  `stringLen` (1〜255)、`dataBudget`、`noLineNumbers`・`assumeDefint`・`emitLineComments` (true / false)。
- 結果の SL・診断の文・大きさの検査の文は、同じ版の CLI と同じです。

### 出力とランタイムの組の印 (BAS_ABI_n)

変換した SLANG は、MAIN の最初に `BAS_ABI_<n>();` を呼びます (何もしない関数。足す命令は 4 バイト)。
ランタイム (`runtime/libx1_basic.asm`) には、同じ番号の `BAS_ABI_<n>` が 1 つだけあります。
番号の違う組 (古い変換の出力と新しいランタイム、またはその逆) を混ぜると、アセンブルで未定義のラベルになって止まります
(slangc は知らない関数をそのまま CALL にするので、止まるのはアセンブルです)。

- 見つけられるのは番号の違いだけです。印を入れる前の出力や、番号が同じで commit の違う組は止まりません (組は同じ SHA で使ってください)。
- 出力とランタイムの約束 (関数の引数・戻り値の意味、作業領域・バッファの作り、呼ぶ順番の前提) を変えたら、
  ランタイムの `@name BAS_ABI_<n>` と `SlangBackend.BasAbi` を一緒に 1 つ上げます (迷ったら上げる)。
  ランタイムの中だけの直し (出力が前提にしない振る舞いを HuBASIC に合わせるなど) では上げません。
- 番号は、ブラウザ向けの束の `manifest.json` の `basAbi` と、wasm の `Version()` の `basAbi` にも出ます。

## 変数と値

- 変数は HuBASIC と同じ決まりで、整数 (`A%` か `DEFINT`)・浮動小数 (`A` か `A!` か `DEFSNG`。倍精度の `A#` と `DEFDBL` も同じ浮動小数で扱う)・
  文字列 (`A$` か `DEFSTR`) です。接尾辞のない変数の既定は、HuBASIC と同じく浮動小数。`DEFINT A` の下の `A` と `A%` は同じ変数、`A!` と `A#` は別の変数。
- `FOR` の変数も浮動小数にできます (`STEP .25` のような小数の増分も)。整数の `FOR` より遅いので、速さが要るときは `DEFINT` か `%` で整数にしてください。
- 浮動小数は SLANG の FLOAT (24 ビット、有効数字 約 5 桁、約 ±1.8E+19 まで) で計算します。整数の変数に入れるときは四捨五入
  (65535 を超えると Overflow)。`PRINT` の表示は HuBASIC と同じ形 (`.5` / `-.25` / `1E+18`) で、桁数は 5 桁。
- 整数は 16 ビット (-32768〜32767。65535 までの数は回り込んだ値として入る)。文字列は 255 バイトまで (`--string-len N` なら変数に入れられるのは N バイトまで)。
- 配列は 2 次元まで (`DIM A(100)`・`DIM B(10,20)`)。整数・浮動小数・文字列の配列が使えます。上限は定数で書く (整数 0〜8191、浮動小数 0〜5460、文字列 0〜63。
  文字列は `--string-len N` なら 0〜16384/(N+1)-1。2 次元は要素の数 (上限+1)×(上限+1) がこの数+1 まで)。`DIM` なしで使うと上限 10 (2 次元は 11×11)。
  添字は次元ごとに調べます。置き場は `DIM` の上限の分だけ取ります
  (同じ配列の `DIM` が複数あれば大きい方 (2 次元は次元ごとに)。`DIM` がなければ 11 要素、2 次元は 121 要素)。文字列の配列は既定で 1 要素 256 バイト (`--string-len N` なら N+1 バイト)。
  メモリを減らしたいときは、使う分だけ `DIM` を書いてください。
- 3 次元以上の配列は使えません (error HB0303。HuBASIC は使える)。同じ名前の配列を 1 次元と 2 次元の両方で使うと error HB0314 (HuBASIC では実行時に Duplicate definition)。
- 文字列の 2 次元の配列を `DIM` なしで使うと、既定の 1 要素 256 バイトでは 121 要素が 1 つの配列の上限を超えます (error HB0305)。
  `DIM` で大きさを書くか、`--string-len` で文字列 1 本を短くしてください (134 以下で収まる)。静的なデータの合計が上限を超えたとき (error HB0309) も、
  文字列を短くすれば収まるなら、収まる `--string-len` の値をエラーの文に書きます。
- `VAL`・`READ`・`INPUT` の数は HuBASIC と同じ読み方 (読めた所までの値。`12X` は 12、読めなければ 0、数の中の空白は読み飛ばす)。
- 数値は 10 進・`&H`・`&O`・`&B`、小数・指数。32768 以上の 10 進の数は浮動小数。
- 演算子: `+ - * / \ MOD ^`、`= <> < > <= >=`、`NOT AND OR XOR`、文字列の `+` と比較。優先順位と丸め方は HuBASIC に合わせてあります。

## 対応している命令

```
REM (')  LET  GOTO  GOSUB  RETURN [行]  ON 式 GOTO/GOSUB/RESTORE  IF … THEN … ELSE …
LABEL "名前"  GOTO "名前"  GOSUB "名前"  ON 式 GOTO/GOSUB 行か "名前",…
FOR … TO … STEP / NEXT [変数,…]  REPEAT / UNTIL  WHILE / WEND  END
DIM  DEFINT/DEFSNG/DEFDBL/DEFSTR  DATA  READ  RESTORE [行]
PRINT (; , SPC() TAB() USING)  PRINT#0  LOCATE x,y  CLS [n]  COLOR [前景][,背景]  WIDTH 40/80[,行[,a,b]]
CONSOLE [ys,yl[,xs,xl]]   (テキストの窓。下の説明)
CFLASH [n]  CSIZE [n]  CGEN [n]  DEFCHR$(n)=文字列  LINE (x1,y1)-(x2,y2),"文字"[,B|BF]
LINE (x1,y1)-(x2,y2),PSET|PRESET[,色][,B|BF]   (グラフィック画面の線・箱・塗りつぶした箱)
PAINT (x,y),色[,境界色 (7 つまで)]
MID$(変数,位置[,長さ])=文字列  INPUT ["案内";|,] 変数,…  SWAP a,b
OUT ポート,値  INIT  CLICK ON/OFF  REPEAT ON/OFF  PALET [c,p]  PRW [n]
POKE 番地,値[,値…]  POKE@ ポート,値[,値…]  CALL 番地  TIME=秒
PLAY "MML"  PLAY n  MUSIC (PLAY と同じ)  TEMPO n  BEEP [0|1]  SOUND レジスタ,値  PAUSE n  RANDOMIZE [n]
VSYNC [n]   (HuBASIC にない命令。下の説明)
KBUF ON/OFF・SCREEN …・KEY n,文字列   (何もしない。warning が出ます)
KEYLIST n・KLIST n・KMODE n   (X1turbo の形。何もしない。0 以外は warning が出ます)
```

- 文の区切りの `:` は、HuBASIC と同じく、文が終わった所なら省けます (`COLOR 4 LOCATE 0,2 PRINT "A"`、`A=1 B=2`)。
  `PRINT "A" PRINT "B"` のように、文がまだ続きを読む所では error です (HuBASIC も Syntax error)。
- `GOTO "名前"` などは、`LABEL "名前"` の文の直後に飛びます (行の途中でも)。名前は大文字小文字と空白まで同じものだけが合います。
  同じ名前の `LABEL` が 2 つあれば前の方 (warning HB0505)。名前は文字列の定数だけ書けます。
- `CALL` は番地のマシン語を呼びます (RET で戻る。IX・IY は保存します)。マシン語は `POKE` で置いてください。変換したプログラムは
  $1000 から置かれ、変数などの作業領域がその後ろに続きます。マシン語や `POKE` で使う番地がそれと重ならないようにしてください
  (`CLEAR 番地` はありません。組み立てた `.bin` の大きさと、リストの `__WORKEND__` で終わりの番地が分かります)。
- `POKE@`・`PEEK@` は I/O 空間 (テキスト VRAM `&H3000`〜・属性 `&H2000`〜 など) の読み書きです。値は 0〜255 (外なら Illegal function call。`POKE` も同じ。
  HuBASIC と同じく、値は先に 16 ビットに回り込むので、`-65535` は 1 として書かれます)。
- `TIME` はサブ CPU の時計で数える秒です (`CALL` でマシン語を動かしている間も進みます)。
- `PRINT USING` は数値の書式だけ (`# . + - **` と `,` の区切り)。`PRINT#` は `#0` だけ。
- `WIDTH` の 2 つめから後 (`WIDTH 40,25,0,1` のような X1turbo の形) は数の定数だけ書けます。画面はいつも 25 行で、行が 25 のときは
  黙って受け、25 以外のときと 3 つめ・4 つめがあるときは warning HB0705 を出して読み飛ばします。
  `KEYLIST n`・`KLIST n` (ファンクションキーの表示)・`KMODE n` (漢字のモード) も数の定数だけで、何もしません (数のない `KEYLIST` は未対応)。
- `PLAY` の MML の音量 `V` は 0〜16 です。`V16` は PSG のハードウェアのエンベロープを使います (音量のレジスタに 16 を書く。
  エンベロープの形 (`SOUND 13,…` で決めた値、決めていなければ 0 = 1 回だけ小さくなって消える) は音符ごとに書き直して最初からやり直し、
  長さ (周期) は書かないので `SOUND 11,…`・`SOUND 12,…` で決めます)。
  数字は、先頭 2 桁が 16 以下なら 2 桁、そうでなければ先頭 1 桁を使い、後に続く数字は捨てます (`V17` は 1、`V107` は 10。HuBASIC と同じ)。
- `INPUT` は 1 行の入力 (BS で 1 文字消す、RETURN で終わり)。数が読めなくても聞き直さず 0 (HuBASIC と同じ)。
- `PRINT` の文字列の中のカーソルの制御コード (`CHR$(28)`〜`CHR$(31)`) でカーソルが動きます。`CHR$(11)` は窓の左上へ、`CHR$(12)` は窓の中を消します。
  `CHR$(14)` は窓の上の行からカーソルの行までを 1 行上にずらしてカーソルの行を空け、`CHR$(15)` はカーソルの行から窓の下の行までを
  1 行下にずらしてカーソルの行を空けます (カーソルは動かない。HuBASIC と同じ)。`PRINT#0` ではどれも文字として出ます。
- `PRINT ,` は 10 桁ごとの区切り (画面の桁) まで空白を出します。次の区切りが行 (窓) の右端を越えるときは、右端まで空白を書いて
  次の行へ折り返し、そこが区切りでなければ次の区切りまで進みます (HuBASIC と同じ)。
- `CONSOLE ys,yl,xs,xl` は、行 ys から yl 行・桁 xs から xl 桁の長方形を、文字を出す窓にします (`CONSOLE ys,yl` は桁が全部、
  引数のない `CONSOLE` は全画面に戻す)。窓を決めるとカーソルは窓の左上へ動きます。そのあとの表示・改行・折り返し・スクロール・
  `CLS` (`CLS 4` のテキストも)・`INPUT` は窓の中だけで行われ、窓の外は変わりません。`LOCATE`・`TAB`・`PRINT ,` の区切り・
  `CHARACTER$`・`SCRN$` の座標は画面の絶対の座標のままで、`LOCATE` で窓の外を指すと Illegal function call です。
  テキストの `LINE` は窓に関係なく描きます。`WIDTH`・`INIT` で窓は全画面に戻ります。範囲 (ys 0〜24、yl 1〜25-ys、
  xs 0〜桁数-1、xl 1〜桁数-xs) を外れると Illegal function call。どれも HuBASIC で確かめた動きです。
  `CONSOLE` の後に `:` なしで別の文を続けることはできません (`CLS` などと同じ)。
- グラフィックの `LINE` は、SLANG の X1 のグラフィックのライブラリ (`libx1_grp`) の線を使います (使うと約 2KB 増えます)。箱と `BF` は直接塗ります。
  `PRESET` は HuBASIC と同じく、指定の色のビットを落とします (箱と `BF`)。
- `VSYNC [n]` は HuBASIC にない、hubasic2sl だけの命令です。垂直帰線の始まりを n 回待ちます (1 秒に約 60 回。n がなければ 1)。
  ゲームを `--wait none` (ウェイトなし) で変換し、メインのループに `VSYNC 1` などを書いて速さをそろえる使い方を考えています
  (ループの中身が 1 フレームより短ければ、1 周が n フレームになる。長ければその分遅くなる)。`--wait` によらず効きます。
  n は数の引数と同じく整数にし、0 以下なら待ちません。`VSYNC` を書いたプログラムは本物の HuBASIC では動きません
  (本物でも動かしたいときは、コメントの `'#VSYNC n` と `--wait frame` を使ってください)。
  `VSYNC` が予約語なので、`VSYNC` で始まる名前は使えません (`VSYNCX=1` は `VSYNC` の文として読まれます)。
- `PAINT` は `libx1_grp` の塗りを使います (使うと約 1.3KB と作業領域 1KB が増えます)。境界色を書かなければ塗る色だけが境界。
  塗り始めが画面の外なら何もしません。HuBASIC よりずっと速く塗ります。

## 対応している関数

```
ASC  CHR$(a[,b,…])  MID$(s,n[,m])  LEFT$  RIGHT$  STRING$(n,s または 文字コード)  VAL  STR$  HEXCHR$  MIRROR$  CGPAT$
CHARACTER$(x,y)  INKEY$  INKEY$(0)  INPUT$(n)  INP  STICK  STRIG  RND  INT
ABS  SGN  SQR  SIN  COS  TAN  ATN  LOG  EXP  FIX  CINT  FRAC  CSNG  CDBL  PAI  RAD
LEN  INSTR([n,]a$,b$)  HEX$  BIN$  SPACE$  SCRN$(x,y,n)  PEEK(番地)  PEEK@(ポート)  TIME
```

- 数学の関数は FLOAT の精度で、HuBASIC とふつう 4〜5 桁合います。大きな引数の `SIN`・`COS`、0 に近い引数の `SIN`、1 に近い引数の `LOG` は合う桁が少なくなります。
- `HEXCHR$` は HuBASIC と同じく、スペースを区切りとして飛ばし、スペースか終わりの前の 1 桁は 1 バイトにします
  (`"0102 0304"` は 4 バイト、`"123 4"` は &H12・&H03・&H04)。空の文字列と、16 進の数字でもスペースでもない文字は Illegal function call。
- `MIRROR$(s)` は各バイトのビットを左右逆にします (&H83 は &HC1。PCG の模様を左右にひっくり返すのに使う)。
  HuBASIC と同じく、引数が文字列の変数か配列の要素なら、その変数の中身もその場で書き換わります (`B$=MIRROR$(A$)` の後は A$ も逆になっている)。
  定数や式 (`MIRROR$(A$+"")`) なら元は変わりません。HuBASIC では `MIRROR$(MID$(A$,1))` も A$ を書き換えますが、こちらは書き換えません。
- `BIN$(n)` は HuBASIC と同じく、16 ビットの値を頭の 0 なしの 2 進にします (`BIN$(5)` は `101`、`BIN$(-1)` は `1` が 16 個)。引数の扱いは `HEX$` と同じです。
- `PAI`・`RAD`・`SCRN$`・`PRW`・`LABEL` は HuBASIC と同じく予約語です (`RADIUS`・`PAIR`・`LABELX` のような変数名は使えません)。

## まだないもの (変換のときの error)

- 命令: `CIRCLE`・`PSET`・`POLY` などほかのグラフィックの命令、`LINE -(x,y)`・`STEP`、`DEF FN`、`ON ERROR`、`USR`、`ERASE`、
  `CLEAR`・`LOADM`・`RUN "ファイル"`、`TIME$`、
  ファイル・ディスク・テープの命令 (error HB0301)
- 関数: `OCT$` など (error HB0302)
- 3 次元以上の配列 (error HB0303。2 次元までにすると決めたもの)、`MID$` の文の配列の要素
- `PAINT` の色の省略、タイルパターン (`PAINT`・`BF` の文字列)
- 名前の飛び先の式 (`GOTO A$`)、`RESTORE "名前"`・`RETURN "名前"`

## HuBASIC との違い (分かっているもの)

実際の HuBASIC (エミュレータの上で動かしたもの) と比べて分かっている違いです。

- 数と計算
  - 整数の計算結果の絶対値が 65535 を超えても、Overflow で止まらず、回り込んだ値のまま続く。整数どうしの計算の途中の値も 16 ビットに回り込む
    (HuBASIC は途中の値を保つ)。整数の変数の -32768 の `ABS` も -32768。
  - 浮動小数は有効数字 約 5 桁・約 ±1.8E+19 まで (HuBASIC は 8 桁・E±38)。表示の桁数のほか、`INT(65535.9)` が 65536 になる、
    `A%=65535.4` が Overflow になる、などがあります。`EXP(88)` は Overflow、`EXP(-100)` は 0、`TAN(PAI(.5))` は大きな値。
  - 浮動小数のあふれは、整数に入れる・表示する・比べる時点でエラーになる (HuBASIC は計算した時点)。
  - 小数の `STEP` の `FOR` は、回る回数が HuBASIC と 1 回違うことがあります。
  - `PRINT USING`: 小数部の桁が多い (6 桁以上) と FLOAT の下の桁が出る。131072 を超える整数は正確に出ない。`1E18` のような大きな値も出す
    (HuBASIC は Format over)。書式に数値の欄がないと、HuBASIC は Illegal function call、こちらは値をふつうに出す。
  - `RANDOMIZE` の後の乱数の並びは HuBASIC と違います。
- プログラムの流れ
  - `DEFINT` などはプログラム全体に効きます (HuBASIC は実行した時点から。前の行で使っていると warning HB0411)。
  - 整数だけの代入で、配列の添字の範囲外と右辺のエラーが両方あると、右辺のエラーが先に出る。
  - 交差した `NEXT` (`FOR Y…FOR X…NEXT Y`) は続けて動く (HuBASIC は FOR without NEXT)。ループが終わった後の余分な `NEXT` は NEXT without FOR。
  - プログラムが終わったとき、HuBASIC は Ok を出しますが、こちらは画面をそのままにして止まります。実行時エラーの後にコマンドは打てません。
  - ループで行の頭に戻った直後の実行時エラーで、表示される行番号が 1 つ前の行のままのことがある (表示だけ)。
  - `SWAP` の型の違いは変換のときの error HB0404 (HuBASIC は実行したときの Type mismatch)。
  - `PALET`・`PRW`・`KEY`・`SCRN$` の引数で、絶対値が 32767 を超える値は、範囲を調べる前に回り込むか Overflow になる。
  - `TIME=` に 65535 を超える値を書くと Overflow (HuBASIC は 86399 まで書け、86400 以上は 1 日で回る)。負の値は回り込んだ大きな値になる
    (HuBASIC は Illegal function call)。
  - `POKE` の値の途中でエラーになったとき、その前の値は書いた後です (HuBASIC の順は確かめていない)。
- 画面
  - グラフィックの `LINE`: 画面からはみ出す線は、HuBASIC と同じく画面の中の点だけを描きます (点の位置も同じ)。ただし 1 点ずつ描くので
    遅く、座標の差がとても大きい線 (数万) は数秒かかります。座標の差が約 32000 を超える箱は、HuBASIC は何も描かず、こちらは画面にかかる部分を描く。
  - 画面の中に収まる線で、1 つ目の端の方が下にある線 (`LINE(100,105)-(300,5)` など) は、ちょうど半分の所の点が HuBASIC と 1 つ上下にずれる
    ことがあります (はみ出す線はずれません)。
  - 画面の中に収まる `PRESET` の箱でない線は色 0 で描きます (HuBASIC は色のビットを落とす。色の付いた点の上でだけ違う)。
    はみ出す `PRESET` の線と、箱・`BF` は HuBASIC と同じく色のビットを落とします。
  - 細かい模様などで `PAINT` の待ち行列があふれると Out of memory で止まります (HuBASIC も同じ模様で Out of memory。止まるまでに塗った範囲は違う)。
  - テキストの `LINE …,BF` で画面の外の大きな座標を書いたとき、HuBASIC より速く終わる (結果は同じ)。
  - `SCRN$` で画面の最後 (右下) の先を読むと、いつも空白。
  - `WIDTH` の直後 (`CLS` をしていないとき)、空白の升の属性の値が違う (見た目は同じ)。
  - HuBASIC の画面は、折り返して続いた行をつないで 1 つの行 (論理行) として覚えていて、改行はその論理行の次の行へ行きます
    (40 文字ずつ折り返して埋めた画面の途中で改行すると、下まで飛んでスクロールする)。こちらは行をつながないので、いつも次の行へ行きます。
  - `INPUT` は、HuBASIC は RETURN を押した行を画面から読み直します (窓の中や行の後ろに残っていた文字も値に入る)。また、入力が次の行に
    折り返すとき、上の行を押し上げて行を空けます。こちらは打った文字だけを値にし、行を押し上げません。
  - `CSIZE 1`・`3` (縦 2 倍) の `PRINT#0` が行の端で折り返すときの行の送り方が違うことがあります (画面の下の端や窓の中で)。
  - 文字列の配列の上限は既定で 63 (HuBASIC はメモリが続く限り。文字列 1 本は中身の分だけ使う)。
  - `DIM` の上限が 10 より小さい配列を、その `DIM` を通らずに使うと、`DIM` の上限を超える添字で Subscript out of range になる
    (HuBASIC は上限 10 で使える。なお HuBASIC でも、使った後にその `DIM` を通ると Duplicate definition)。2 次元の配列も次元ごとに同じです
    (`DIM A(2,20)` を通る前の `A(3,0)` は、HuBASIC は使えて、こちらは Subscript out of range)。
  - 配列の要素に代入するとき、代入先の添字の中の `MIRROR$` が変数を書き換えると、入る場所が違います。HuBASIC は代入先の添字を
    2 回計算するらしく (`MIRROR$` も 2 回効いて元に戻る)、こちらは左から 1 回だけ計算します (1 次元の配列でも同じ)。式の中で読むだけの添字は同じです。
- 入力と音
  - `INKEY$` の自動リピート: 1 周が 1 フレームより長いループでは、リピートの回数が少ない。
  - `KEY` は何もしません (`INKEY$` はファンクションキーでキーの文字コードを返す)。
  - 音のない MML (最後が数字のない `V`、音符のない `"v15:v15:v15"`) の次の `PLAY` は、HuBASIC は 60〜70ms ほど音が切れてから鳴るが、こちらは切れ目なく鳴る。
  - X1turbo の BASIC の動き (行数の切り替え、ファンクションキーの表示、漢字のモード、turbo の MML) は確かめていません。HuBASIC (CZ-8FB01) に合わせています。
- 速さ: ウェイトの付け方が「行ごと」なので、HuBASIC と同じ速さになるのは目安です。文字列の処理や `PAINT` は HuBASIC よりずっと速い。

合わせてあるもの (比べて確かめたもの): 実行時エラーの表示 (既定では「<メッセージ> in <行番号>」と「Ok」)、`PLAY` の音程・長さ・テンポ、`BEEP`、
文字の装飾と色、右端での折り返しとスクロール、`PRINT USING` の書式、テキストの `LINE` の点の置き方、グラフィックの `LINE` の点の置き方
(画面の中の線と箱、`BF`、はみ出す線)、`PAINT` の塗り方。

## 変換のときに出るもの (番号の分類)

| 番号 | 内容 |
|---|---|
| error HB01xx | 文字・数値の書き方 (変換できない文字、大きすぎる数、1 行 255 文字超え、DATA が大きすぎる など) |
| error HB02xx | 構文 (文として読めない、`:` や `)` が足りない、行番号がない など) |
| error HB03xx | 未対応 (命令 HB0301、関数 HB0302、3 次元以上の配列 HB0303、1 次元と 2 次元の両方で使う配列 HB0314 など)。HB0305 は配列が大きすぎる、HB0309・HB0312 はメモリに収まらない |
| error HB04xx | 型 (整数と文字列の取り違え HB0404、同じ頭文字に違う DEFxxx HB0410 など) |
| error HB05xx | 行番号 (飛び先の行がない HB0503、同じ行番号が 2 回 HB0502、飛び先の名前の `LABEL` がない HB0504) |
| error HB06xx | ループの対応 (FOR に NEXT がない、WEND に WHILE がない など) |
| warning HB0109 | `%` を付けた 32768〜65535 の数 (16 ビットの整数として扱う) |
| warning HB0110 | UTF-8 として読めないので X1 の文字コードとして読んだ |
| warning HB0112 | ファイル名が 13 文字を超えるので IPL の起動名を切った、または ASCII 以外の字があるので起動名にしない (env の名前、なければ `SLFSMAIN`) |
| warning HB0411 | DEFxxx より前の行で、その頭文字の変数を使っている |
| warning HB0412 | 倍精度の変数 (単精度と同じ浮動小数で扱う) |
| warning HB0501 | 行番号が昇順でない (並べ替えて続ける) |
| warning HB0505 | 同じ名前の `LABEL` が 2 つ以上ある (前の方に飛ぶ) |
| warning HB0701・HB0702・HB0704 | `KBUF`・`SCREEN`・`KEY` (何もしない) |
| warning HB0705 | `WIDTH` の行数 (25 以外) と 3 つめ・4 つめ (読み飛ばす) |
| warning HB0706 | `KEYLIST`・`KLIST`・`KMODE` の 0 以外 (何もしない) |
