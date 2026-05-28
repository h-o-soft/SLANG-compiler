## UILIB — X1 GVRAM 静的 HUD ライブラリ

`TILELIB` (PCG 背景) と `SPRLIB` (GVRAM スプライト) を組む構成だと、テキスト
VRAM は TILELIB が占有するのでスコア・メッセージ・枠などは GVRAM に直接
描画するしかありません。UILIB はそのための**静的 HUD 専用**レイヤです。

- 8x8 チップ単位座標 (x:0..39, y:0..24)
- 3 プレーン (B/R/G) 色 3 bit (0..7)
- 内蔵 256 glyph 8x8 フォント (`assets/ui/font_charset1.png` から生成)
- **両ページ (page 0 / page 4) に同時書き込み** — 1 回描けば SPRLIB の
  ダブルバッファ切り替えに耐える
- **OR 描画 / 選択プレーンのみ書き込み** — 色指定したプレーンだけ OR で
  足し込み、指定外のプレーンは既存ピクセルを保持する (詳細は後述)

### HUD 専用制約 (重要)

SPRLIB を併用する場合、UI 領域にスプライトを侵入させてはいけません。
SPRLIB の old1 erase はスプライトが通った領域を 0 クリアするため、UI を
GVRAM に焼き付けていてもスプライトが通過すると UI 下地が消えます。

**対応**: UI は画面端 (Y=0 や Y=24 等) に寄せて、スプライトの可動範囲と
物理的に分離すること。

### 前提と呼び出し順序

```slang
WIDTH(40);
GRPSETUP();
GRDISP(1);

// SPRLIB を併用するなら SPR_INIT を必ず先に呼ぶこと (GVRAM 全クリアする
// ため、UILIB 描画後に呼ぶと消える)。UILIB 単独なら省略可。
SPR_INIT();

// --- UILIB 描画 (静的) ---
UI_SET_COLOR(7);
UI_AT(0, 0);
UI_PUTS(MSG_TITLE);
UI_AT(10, 10);
UI_BOX(20, 3);

// --- メインループ ---
LOOP {
    // ゲームロジック
    VSYNC(1);
    SPR_UPDATE();   // UI 領域にはスプライトを入れない
}
```

順序は `WIDTH → GRPSETUP → GRDISP → SPR_INIT → UILIB 描画 → メインループ`。

### データフォーマット

#### フォント (256 glyph × 8 bytes = 2048 bytes)

内蔵フォントは UILIB.LIB 内のマーカー付きブロック `; === FONT DATA BEGIN`
〜 `; === FONT DATA END ===` に 2048 バイトで埋め込まれています。PNG
(`assets/ui/font_charset1.png`) を変更したら:

```
python3 tools/png_to_asm.py assets/ui/font_charset1.png --inplace include/UILIB.LIB
```

で UILIB.LIB を再生成 (マーカー間のみ差し替え)。

#### 文字列 (エンコード済み 8-bit zero 終端バイト列)

`UI_PUTS(ptr)` はエンコード済みの 8-bit zero 終端バイト列を受け取ります。
UTF-8 を直接は解釈しません。

**ASCII 範囲のみの文字列** (内蔵フォントの 0x20..0x7E が ASCII と一致して
いる限り): SLANG の文字列リテラルを直接渡せます。

```slang
UI_PUTS("HELLO PCG");   // ASCII のみなら直書き OK (内部で 'H'..0 の byte 列)
```

**日本語等を含む場合**: `uicharset2.json` 側は ひらがな / カタカナに独自
コードを割り当てているため、UTF-8 は直接使えません。`tools/charmap-encode.py`
で事前に変換します (charset2 への切替は後述):

```json
// strings.json
{
  "MSG_TITLE": "UILIB TEST",
  "MSG_JPN":   "こんにちは"
}
```

```
python3 tools/charmap-encode.py \
    --charmap assets/ui/uicharset1.json \
    --input examples/ui/strings.json \
    --out examples/ui/strings.sl
```

出力:

```slang
ARRAY BYTE MSG_TITLE[] = { 85, 73, 76, 73, 66, 32, 84, 69, 83, 84, 0 };
ARRAY BYTE MSG_JPN[] = { 10, 104, 22, 17, 26, 0 };
```

これを SLANG ソースに `#include` するか、直接貼り込めば使えます。

**文字コード空間:**

- glyph 0..255
- **glyph 0 は `UI_PUTS` の終端予約** (0 を文字列中に入れない)
- `UI_PUTC(0)` は許可 (単発描画なので終端問題なし、glyph 0 が描かれる)
- CHARMAP で値 0 / >255 は使わない (`charmap-encode.py` が検知してエラー)

### 色 (3 bit BRG)

| 値 | 色 | 値 | 色 |
|---|---|---|---|
| 0 | 黒 (クリア) | 4 | 緑 |
| 1 | 青 | 5 | シアン (B+G) |
| 2 | 赤 | 6 | 黄 (R+G) |
| 3 | 紫 (B+R) | 7 | 白 |

描画は **OR モード + 選択プレーンのみ書き込み**:

- **色 bit が立っているプレーン**: 既存ピクセルに font/fill パターンを OR
  で足し込む (`IN → OR → OUT`)
- **色 bit が立っていないプレーン**: 一切触らず既存ピクセルを保持

これにより、UI_FILL で背景を塗った上に UI_PUTS / UI_BOX を重ねても、
**文字の隙間や枠の「穴」部分で背景が潰れない**のがポイントです (SET モード
だと隙間ピクセルが 0 クリアされて PCG が透ける問題があった)。

**注意**:

- 色 0 は「どのプレーンも書かない」= 描画なし。クリアは SPR_INIT() 等
  でまとめて行う前提
- UI_PUTC(0) で glyph 0 (通常は全ゼロ) を描いてもクリアにならない
- 既に描画済みの上に別色で重ねると OR 合成になるので色が混ざる
  (例: 赤の上に青 → 紫)

### API

| API | 引数 | 単位 | 動作 |
|---|---|---|---|
| `UI_AT(x, y)` | x, y | チップ単位 (8px) | 描画カーソル位置を設定 |
| `UI_SET_COLOR(c)` | 0..7 | BRG bit | 描画色を設定 |
| `UI_PUTC(ch)` | 0..255 | — | 1 文字描画、カーソル X++ |
| `UI_PUTS(ptr)` | zero 終端バイト列先頭 | — | 文字列描画、画面右端 (X=40) で停止 |
| `UI_FILL(w, h)` | w, h | チップ単位 | カーソル位置を左上に w×h を塗る (色 0 は no-op) |
| `UI_BOX(w, h)` | w, h (≥2) | チップ単位 | **9-slice** で外周 8 箇所だけを描画 (内部は描かない。背景を塗りたければ先に UI_FILL を使う)。描画後カーソルは開始位置へ戻る |
| `UI_FONT_ADDR()` | — | — | 内蔵フォント先頭アドレスを返す (HL)。`addr + ch * 8` で各グリフ 8 バイトを直接編集可 |

### UI_BOX と 9-slice (window 画像)

`UI_BOX(w, h)` は**フォントとは独立した 9-slice 画像** `_UI_BOX_DATA`
(9 × 8 byte = 72 byte) を参照して外周 8 箇所を描画します。スロット配置:

```
 TL(0)  T(1)  TR(2)
 L (3)  M(4)  R (5)
 BL(6)  B(7)  BR(8)
```

- `T` / `B` は横エッジ用 (w-2 回並ぶ)
- `L` / `R` は縦エッジ用 (h-2 回並ぶ)
- `M` (内部) は `UI_BOX` では描画されない。背景を塗りたいときは事前に
  `UI_FILL` する

この 9-slice 画像は `assets/ui/window.png` (24×24 = 3×3 × 8×8) から
`tools/png_to_asm.py --box` で UILIB.LIB 内の該当マーカーブロックに反映
できます。フォント側のグリフ位置は消費しないので、通常の文字描画とは干渉
しません。

```
python3 tools/png_to_asm.py assets/ui/window.png --box --inplace include/UILIB.LIB
```

枠のデザインを変えたいときは `window.png` を編集して上記を再実行するだけ。

### フォント書き換え

`UI_FONT_ADDR()` で取得したアドレスに直接書き込めばフォントをカスタマイズ
できます。ユーザー用のフォントテーブルを別途持つ必要はありません (メモリ
2 倍になるので意図的に `SET_FONT` は提供していません)。

```slang
VAR FONT_ADDR;
FONT_ADDR = UI_FONT_ADDR();
// glyph 1 を全ピクセル点灯に書き換え
POKE(FONT_ADDR + 1*8 + 0, $FF);
POKE(FONT_ADDR + 1*8 + 1, $FF);
// ...
```

### 制約・注意点

- **HUD 専用**: UI 領域にスプライトを入れない (画面端寄せ推奨)
- `UI_FILL` / `UI_BOX` / `UI_PUTS` は OR 描画 + 選択プレーンのみ。
  色指定外のプレーンは保持されるので、`UI_FILL → UI_BOX → UI_PUTS` で
  重ねても背景が透けない
- `UI_BOX` の中身 (枠の内側) は描画しない。背景を色塗りしたい場合は
  事前に UI_FILL で塗ること (PCG 上では不透明化のためパレット remap 必須)
- `UI_PUTS` は右端 (x=40) で停止するだけでラップはしない
- `UI_PUTC(0)` は glyph 0 を描画 (`UI_PUTS` の終端とは別扱い)
- 1 文字の描画コスト ≈ 48 OUT (3 plane × 8 row × 2 page)。静的 HUD 用途
  では問題ないが、毎フレーム描き換えには向かない

### サンプル

| ファイル | 内容 |
|---|---|
| `UITEST.SL`    | 文字列 (ASCII + 日本語) / FILL / BOX の描画確認 |
| `strings.json` | 日本語文字列定義 (charmap-encode 入力) |
| `strings.sl`   | strings.json から生成した `ARRAY BYTE` 定義 |

### ビルド手順

デフォルトは **charset1 (標準, ASCII) + font_charset1.png + window.png**。

1. 文字列を再生成 (strings.json を編集したとき):
   ```
   python3 tools/charmap-encode.py \
       --charmap assets/ui/uicharset1.json \
       --input examples/ui/strings.json \
       --out examples/ui/strings.sl
   ```

2. フォントを再生成 (font_charset1.png を編集したとき):
   ```
   python3 tools/png_to_asm.py assets/ui/font_charset1.png --inplace include/UILIB.LIB
   ```

3. 9-slice 枠画像を再生成 (window.png を編集したとき):
   ```
   python3 tools/png_to_asm.py assets/ui/window.png --box --inplace include/UILIB.LIB
   ```

4. コンパイル:
   ```
   slangc -E x1 -I include -I examples/ui -o UITEST.ASM examples/ui/UITEST.SL
   AILZ80ASM UITEST.ASM -o UITEST.bin
   ```

### 日本語等拡張 charset (charset2) への切替

`uicharset2.json` + `font_charset2.png` は ひらがな / カタカナ / 一部漢字
を含む拡張セット。日本語を描画したい場合は以下で一式切り替え:

1. フォント差し替え:
   ```
   python3 tools/png_to_asm.py assets/ui/font_charset2.png --inplace include/UILIB.LIB
   ```

2. 文字列定義に日本語を入れて、charset2 でエンコード:
   ```
   python3 tools/charmap-encode.py \
       --charmap assets/ui/uicharset2.json \
       --input examples/ui/strings.json \
       --out examples/ui/strings.sl
   ```

charset1 と charset2 は ASCII 部分 (`0x20..0x7E`) は共通なので、charset1
で作った ASCII コンテンツはそのまま charset2 でも動きます。逆に charset2
専用の日本語は charset1 では `char not in charmap` エラーになるので、
必要になったタイミングで切替えてください。

### フォント出典 / ライセンス

`assets/ui/font_charset1.png` と `assets/ui/font_charset2.png` は、
meister68k さんの [X1_compatible_font](https://github.com/meister68k/X1_compatible_font)
(= Num Kadoma さんの [美咲フォント (Misaki Font)](https://littlelimit.net/misaki.htm)
を元にした X1 CZ-800 互換フォント) から採取・整形したものです。

美咲フォントのライセンスは、改変・商用・再配布すべて無制限許諾 (無保証) の
free software permit。本プロジェクトに同梱して再配布しています。

ライセンス原文と詳細は [`assets/ui/LICENSE.font`](../../assets/ui/LICENSE.font)
を参照してください。

9-slice 枠画像 (`assets/ui/window.png`) は本プロジェクト用に作成したもので、
SLANG-compiler 本体のライセンスが適用されます。
