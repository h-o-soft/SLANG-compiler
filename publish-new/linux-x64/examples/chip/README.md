## CHIPLIB — X1 総合グラフィックライブラリ (GVRAM)

TILELIB が PCG を前提にするのに対し、CHIPLIB は**グラフィック VRAM に直接**
タイル・スプライト・マスクを描画する総合ライブラリです。PCG を消費しない
代わりに、マップ描画・チップスプライト合成・マスク描画・アニメ・ダブル
バッファまでを一括で面倒見ます。

- 基本単位: **チップ = 8x8 (3 プレーン 24 バイト)**
- **仮想 VRAM (VVRAM)** をダブルバッファで持ち、差分だけを GVRAM に転送
- スーパーチップ (2x2 チップ = 16x16) 単位のマップ構造は TILELIB と同様

「PCG を他用途に使いたい」「VRAM 一層を全部使って派手な画面を作りたい」
といった用途向けです。ミニマム構成なら TILELIB + SPRLIB の方が軽量。

### 前提と呼び出し順序

```slang
WIDTH(40);
GRPSETUP();
GRDISP(1);

CHIP_INIT();
CHIP_SET_SIZE(W, H);                       // キャラクタ単位。VVRAM も自動設定される
CHIP_SET_OFS(OFS_X, OFS_Y);                // 画面上の描画開始位置
CHIP_SET_MAP(MAP_CHIP, MAP_IDX, MAP_GRP);  // マップ / インデックス / チップ画像
CHIP_SET_MAPSIZE(MW, MH);                  // マップ全体のスーパーチップ数

CHIP_SET_ANIM(threshold);                  // アニメ対象チップ番号の閾値
CHIP_SET_ANIM_RATE(8, 4);                  // 進行レート

CHIP_FILL_MAP();
CHIP_DRAW_VVRAM();
```

### データフォーマット

#### チップパターン (24 バイト / チップ)

8x8 × 3 プレーンを**プレーナ** (プレーンごとにまとまった) 配置で持ちます。
PCG (TILELIB) の BRG インターリーブとは異なるので注意。

```
 0..7  : B プレーン (8 行分, ビット 7 = 左端)
 8..15 : R プレーン
16..23 : G プレーン
```

マップ用チップ画像 (`MAP_GRP`) はチップ番号順に連続させて確保します。

```
chip N の開始アドレス = MAP_GRP + N * 24
```

#### マップ (3 層構造)

`CHIP_SET_MAP(map_chip, map_idx, map_grp)` で 3 本のアドレスを渡します。

1. **map_chip** — スーパーチップ番号の 2 次元配列 (W × H バイト)
2. **map_idx** — スーパーチップ番号 × 4 バイトのインデックス表
   (TL, TR, BL, BR の順にチップ番号)
3. **map_grp** — チップ画像本体。チップ番号 × 24 バイトで連続確保

TILELIB と同じ考え方ですが、`map_grp` をユーザーが直接渡す点が違います。

#### VVRAM (仮想 VRAM)

GVRAM への転送バッファ。ユーザーが触ることはありませんが、サイズ制約は
設計上意識しておく必要があります (後述)。

#### マスク付きチップ (スプライト用, 32 バイト / チップ)

```
 0..7  : マスク (1 ビット = 非描画画素)
 8..15 : B プレーン
16..23 : R プレーン
24..31 : G プレーン
```

複数チップスプライトは上記 32 バイト単位のチップを W (横) × H (縦) 個
row-major で並べた `W*H*32` バイトの連続データになります。
`CHIP_SPR_ADD(ptr, W, H)` で一度に流し込めます。

### 公開 API

#### 初期化 / 描画

| API | 引数 | 説明 |
|---|---|---|
| `CHIP_INIT()` | — | 内部状態と VVRAM をクリア |
| `CHIP_FILL_MAP()` | — | マップから VVRAM を再生成 (スクロール位置反映) |
| `CHIP_DRAW_VVRAM()` | — | VVRAM の差分を GVRAM へ転送。**毎フレーム呼ぶ** |
| `CHIP_DRAW(x, y, img24)` | チップ単位座標, 24 バイトのパターン | 単発描画 (マップ無視) |
| `CHIP_MASK_DRAW(x, y, img32)` | チップ単位座標, 32 バイトのマスク付きパターン | マスク付き単発描画 |

#### 設定

| API | 引数 | 単位 | 説明 |
|---|---|---|---|
| `CHIP_SET_SIZE(w, h)` | w, h | チップ単位 | 表示領域 + 内部で必要な VVRAM を自動設定 (マージン込みで MAX 超過は clamp) |
| `CHIP_SET_OFS(x, y)` | x, y | チップ単位 | 画面 (GVRAM) 上の表示開始位置 |
| `CHIP_SET_MAP(chip, idx, grp)` | 3 アドレス | — | マップ / スーパーチップ表 / チップ画像を登録 |
| `CHIP_SET_MAPSIZE(w, h)` | w, h | **スーパーチップ単位** | マップ全体のサイズ (圧縮マップの行列サイズ) |
| `CHIP_SET_SCROLL(x, y)` | x, y | チップ単位 | スクロール位置。毎フレーム可 |
| `CHIP_SET_VVRAMSIZE(w, h)` | w, h | チップ単位 | **上級者向け。** 通常は `CHIP_SET_SIZE` が自動設定する |

#### アニメ

| API | 引数 | 説明 |
|---|---|---|
| `CHIP_SET_ANIM(threshold)` | チップ番号 | この値以上のチップ番号の**下位 2 ビット**がアニメで自動差し替え。255 で無効化 |
| `CHIP_SET_ANIM_RATE(ticks, period)` | vsync 回数, フレーム数 | ticks vsync ごとに進行, period で循環 |
| `CHIP_ANIM_TICK()` | — | `VSYNC_PROC()` から毎フレーム呼ぶ |

アニメモデルは TILELIB と同じ。番号 N..N+3 に 4 コマを並べ、
`CHIP_SET_ANIM(N)` にすれば自動で循環します。

#### スプライト合成

描画命令を積み上げて一度に合成する形式です。

| API | 引数 | 説明 |
|---|---|---|
| `CHIP_SPR_START()` | — | 合成セッション開始 (内部インデックスリセット) |
| `CHIP_SPR_POS(x, y)` | チップ単位座標 | 次の `CHIP_SPR_ADD` の配置位置 |
| `CHIP_SPR_ADD(ptr, w, h)` | マスク付きパターン, 幅, 高さ (チップ単位) | スプライトを 1 個追加 (複数呼び出し可) |
| `CHIP_SPR_FLASH(planeMask)` | 3 ビットマスク (B/R/G) | 次の描画で対象プレーンを白く点滅 (`0` で通常, `7` で真っ白) |

典型パターン:

```slang
CHIP_SPR_START();
CHIP_SPR_POS(PLAYER_X, PLAYER_Y);
CHIP_SPR_ADD(PLAYER, 2, 2);

CHIP_SPR_POS(BULLET_X, BULLET_Y);
CHIP_SPR_ADD(BULLET, 1, 1);

CHIP_FILL_MAP();
CHIP_DRAW_VVRAM();
```

### VVRAM サイズのユーザー上書き

VVRAM は **各辺 +2 チップのスクロールマージン付きの画面サイズ**分だけ必要
です (`CHIP_SET_SIZE(W, H)` は内部で自動的に `W+2, H+2` を VVRAM として
確保)。画面サイズ以上にする意味は基本ありません。

画面の縦サイズはスーパーチップ (2×2) 単位にアライン(= 偶数)しておく
のが無難です。デフォルト MAX `42 x 28` は画面 `40 x 26` (画面 40 x 25 を
偶数に切り上げ) を想定した値: `(40+2) x (26+2) = 42 x 28`。

これより大きい画面で使う場合はインクルード前に MAX を上書きします。

```slang
// 例: 画面 48x28 で使う場合 (= VVRAM 50x30)
CONST _CHIP_VVRAM_OVERRIDE = 1;
CONST ASM _CHIP_VVRAMW_MAX = 50;
CONST ASM _CHIP_VVRAMH_MAX = 30;
#include CHIPLIB.LIB
```

値の決め方のガイド:

1. 画面の縦チップ数を **偶数に切り上げ** (例: 25 → 26)
2. 横・縦ともに **+2** したものが VVRAM MAX

逆に小さい画面しか使わないなら RAM を節約する目的で小さくできます
(VVRAM は `(W+2)*(H+2)*2` バイト使用。2 ページ分)。

`CHIP_SET_SIZE` は MAX 超過時に**黙って clamp**するので、画面を広げたつもり
が MAX 不足で描画範囲が切れる事故を防ぐには、画面サイズに合わせた MAX 上書き
を忘れないでください。

### 制約・注意点

- 1 チップパターン = 24 バイト固定 (B8 + R8 + G8 の planar)
- マスク付きスプライトは 32 バイト / チップ (8 バイトマスク + 24 バイト planar)
- `CHIP_DRAW_VVRAM()` を毎フレーム呼ばないと GVRAM 更新が行われない
- アニメ進行は `CHIP_ANIM_TICK()` を `VSYNC_PROC()` から呼ぶこと
- `CHIP_SET_SIZE` は「マージン自動加算」+「MAX clamp」の 2 段動作なので、
  大画面時は事前定義で `_CHIP_VVRAMW_MAX` / `_CHIP_VVRAMH_MAX` を上げておくこと
- **`CHIP_SET_MAP` で渡した 3 本のアドレスは参照保持**されます (毎フレームの
  `CHIP_FILL_MAP` から読まれる)。マップ / スーパーチップ表 / チップ画像は
  使い続ける間は確保したままにし、書き換えれば次フレームに反映されます。
  `CHIP_SPR_ADD` に渡すスプライトポインタも同様に合成時点で参照されます

### 最小サンプル

```slang
#include CHIPLIB.LIB

ARRAY BYTE MAP_GRP[] = { /* チップ 0..N を 24 バイト (B8+R8+G8 planar) ずつ連続 */ };
ARRAY BYTE MAP_IDX[] = { /* スーパーチップ × 4 バイト */ };
ARRAY BYTE MAP_CHIP[] = { /* スーパーチップ番号 */ };
ARRAY BYTE SPRITE[]   = { /* マスク付き W*H*32 バイト */ };

VSYNC_PROC()
{
    CHIP_ANIM_TICK();
}

MAIN()
{
    WIDTH(40); GRPSETUP(); GRDISP(1);

    CHIP_INIT();
    CHIP_SET_SIZE(40, 25);
    CHIP_SET_OFS(0, 0);
    CHIP_SET_MAP(MAP_CHIP, MAP_IDX, MAP_GRP);
    CHIP_SET_MAPSIZE(32, 32);    // 32x32 スーパーチップ = 64x64 チップ相当
    CHIP_SET_ANIM(4);
    CHIP_SET_ANIM_RATE(8, 4);

    CHIP_FILL_MAP();
    CHIP_DRAW_VVRAM();

    LOOP {
        // 入力・ロジック
        CHIP_SET_SCROLL(SCX, SCY);

        CHIP_SPR_START();
        CHIP_SPR_POS(PX, PY);
        CHIP_SPR_ADD(SPRITE, 2, 2);

        CHIP_FILL_MAP();
        CHIP_DRAW_VVRAM();

        VSYNC(2);
    }
}
```

### 収録サンプル

| ファイル | 内容 |
|---|---|
| `CHIPTEST.SL` | マップ描画 + スーパーチップ + スプライト + アニメ + スクロールの一通り |
