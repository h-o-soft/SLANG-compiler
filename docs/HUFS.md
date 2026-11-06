# HUFS (Hu-BASIC と同じ形のディスク)

`x1native_hufs` 環境のディスクです。 Hu-BASIC (CZ-8FB01) と同じ形の 2D D88 で、 OS なしで IPL から起動します。
作ったディスクは、 本物の Hu-BASIC の `FILES` や `INPUT#`、 HuDisk でも一覧・読み書きできます。

いまできること (段階 0):

- `slangbuild --emit disk -E x1native_hufs` でディスクを作る (`--hufs-add` でファイルを足す)
- ホストの道具 `slfs-pack hu` (作る・一覧・取り出す・足す・消す)
- SLANG から、 レコードを 1 つずつ直に読み書きする `FRAWREAD` / `FRAWWRITE`

ファイルを名前で読み書きする API (`FOPEN` など) と、 hubasic2sl のファイル命令は、 次の段階で足します。

## 使い方

```sh
slangbuild -E x1native_hufs --emit disk examples/X1NATIVE_HUFS/HUFSRAW.SL -o HUFSRAW \
  --hufs-add GREET.TXT=examples/X1NATIVE_HUFS/files/GREET.TXT,text
```

- `--hufs-add [名前=]ファイル[,bin|,text]` (くり返せる。 名前を省くとファイル名)
  - `,text`: LF・CRLF を CR にして、 終わりに `$1A` を付ける (種類 Asc。 Hu-BASIC の順ファイルと同じ形)
  - `,bin`: 種類 Bin。 大きさの欄に長さ (65535 バイトまで)
  - 何も付けない: 中身そのまま、 種類 Asc
- `--disk-main-name <name>` (と SL の頭の `// disk-main-name:`) で、 IPL の起動名 (= 本体のファイル名) を決めます (既定 `HUFSMAIN`。 `x1native_slfs` と同じ決まり)
- `--slfs-add` は使えません (`x1native_slfs` の物)

この環境には `libx1native_slfs` (`FS_READ_BY_ID` / `FS_SAVE_R` / `FS_SAVE_W`) を入れていません。 これらは "SLFS" の印を確かめずにディスクを読むので、 Hu の形のディスクでは壊れたものを読み書きします。 使うとリンクのエラーになります。

## SLANG の関数

### `FRAWREAD` / `FRAWWRITE`

```sl
ARRAY BYTE BUF[255];
R = FRAWREAD(rec, BUF);    // レコード rec (0〜1279) を BUF (256 バイト) に読む
R = FRAWWRITE(rec, BUF);   // BUF をレコード rec に書く
```

- 戻り値: `0` 成功、 `$FF09` FDC の失敗・時間切れ・ディスクなし、 `$FF0B` 書き込み禁止、 `$FF0E` レコード番号が範囲外
- ファイルの仕組みを通りません (FAT やディレクトリも書けるので、 気をつけて使う)
- レコード番号 = トラック × 16 + (セクタ − 1)、 トラック = シリンダ × 2 + 面 (`x1native_slfs` の logical sector と同じ)
- 割り込みは、 1 レコードを読み書きする間だけ止めて、 入口の状態に戻します
- ディスクが入っていないときは、 約 13 秒待ってから `$FF09` を返します

## slfs-pack hu

```sh
slfs-pack hu pack -o <out.d88> --main <main.bin> [--main-name <name>] [--main-load <a>] [--main-exec <a>] [--add <spec>]...
slfs-pack hu list <d88>
slfs-pack hu extract <d88> <name> -o <file> [--text]
slfs-pack hu add <d88> <spec>
slfs-pack hu delete <d88> <name>
```

- `<spec>` は `--hufs-add` と同じ形です
- `extract` の長さ: 種類 Bin は大きさの欄、 それ以外はレコード単位 (Hu-BASIC のデータのファイルは大きさの欄が 0 で、 長さは FAT でしか分からない)。 `--text` は `$1A` の前までにして CR を LF にします
- 書き込み禁止の印 (`$40`) のあるファイルは消せません

## ディスクの形

| レコード | 中身 |
|---|---|
| 0 | IPL (ブートセクタ。 [SLFS](SLFS.md) のブートセクタと同じ形) |
| 1〜13 | `$FF` |
| 14 | FAT (クラスタ 1 つに 1 バイト) |
| 15 | `$00` |
| 16〜31 | ディレクトリ (32 バイト × 128 項目) |

- **クラスタ:** 1 クラスタ = 16 レコード = 4 KB。 2D は 80 クラスタで、 0・1 はシステム。
- **FAT:**
  - `$00` 空き、 `$01`〜`$4F` 次のクラスタ、 `$80`+n 最後のクラスタで n+1 レコードを使う
  - 0・1 番は `01 8F`、 80〜127 番は `$8F`、 128〜255 番は `$00`
- **ディレクトリの項目:**
  - +00 種類 (`$01` Bin・`$02` Bas・`$04` Asc。 `$40` は書き込み禁止。 `$00` 消したもの、 `$FF` ここから未使用)
  - +01 名前 13、 +0E 拡張子 3、 +11 パスワード (`$20` = なし)、 +12 大きさ、 +14 読む番地、 +16 実行番地、 +18 日付 5、 +1E 先頭のクラスタ
- **本体:** クラスタ 2 から続けて置きます (IPL ROM は続いたレコードしか読めない)。 項目は種類 Bin・書き込み禁止 (`$41`)、 名前は起動名、 拡張子は `Sys` です。
- **足すファイル:** 本体の後ろに名前の順で置き、 クラスタは空きのうち番号の小さい方から取ります。
- **日付:** すべて 0 です (組み立てるたびに同じ中身になるように)。

### ファイル名

- 頭の `0:` は飛ばします。 最後の `.` で名前と拡張子に分けます (`.` がなければ拡張子は空白)。
- 名前 1〜13 文字、 拡張子 0〜3 文字。 超えたら切らずにエラー (本物の Hu-BASIC も Bad file descriptor)。
- 使える字は ASCII の印字できる字と半角カナです。 `"`・`:` は使えません。
- 比べるときは大文字と小文字を区別します (本物の Hu-BASIC と同じ)。
