## TILELIB — X1 PCG タイルマップライブラリ

8x8 の PCG タイルを 2x2 まとめた**スーパーチップ** (16x16) でマップを構成し、
テキスト VRAM + PCG に背景レイヤーとして描画するライブラリです。
PCG のみを使うので、グラフィック VRAM はスプライト等別用途に空けておけます。

- 基本単位: **タイル = 8x8 (PCG 1 文字)**, **スーパーチップ = 2x2 タイル (16x16)**
- マップは「スーパーチップ番号の配列」で持つ (圧縮形式)
- スクロール / アニメ / ダブルバッファ (テキスト VRAM 2 ページ) 対応

### 前提と呼び出し順序

```slang
WIDTH(40);                 // PCG 定義は WIDTH 設定後に行う
PCGDEFS(0, TILEPCG, 8);    // PCG 0..7 を連続定義 (各 24 バイト)

TILE_INIT();               // 内部状態 + テキスト VRAM 初期化
TILE_SET_MAP(TILE_MAP, TILE_IDX, 0);
TILE_SET_MAPSIZE(32, 32);  // マップサイズ (スーパーチップ単位)
TILE_SET_ANIM(255);        // 閾値。255 = アニメなし
TILE_FILL_MAP();           // 初回描画
```

PCG 定義 (`PCGDEFS` / `PCGDEF`) は PCG ハードウェアの都合上、1 タイルあたり
1 vblank を消費するので COUNT 個では約 COUNT/60 秒かかります。

### データフォーマット

#### PCG タイル (24 バイト / タイル)

8 行 × 3 バイトの BRG インターリーブ。

```
各行: B, R, G の順に 1 バイトずつ (ビット 7 = 左端)
1 タイル = 8 行 × 3 バイト = 24 バイト
```

例:

```slang
ARRAY BYTE TILEPCG[] = {
    // タイル 0: 黒 (全プレーン 0)
    $00,$00,$00, $00,$00,$00, $00,$00,$00, $00,$00,$00,
    $00,$00,$00, $00,$00,$00, $00,$00,$00, $00,$00,$00,
    // タイル 1: 赤レンガ (R プレーンのみ)
    $00,$FF,$00, $00,$FF,$00, $00,$FE,$00, $00,$00,$00,
    $00,$FF,$00, $00,$FF,$00, $00,$EF,$00, $00,$00,$00,
    // ... タイル 2..N
};
```

**連続確保されるので `PCGDEFS(STARTIDX, TILEPCG, COUNT)` で一括登録できます。**

#### スーパーチップ インデックス (4 バイト / スーパーチップ)

1 スーパーチップ = 2x2 タイル。構成タイル番号を TL, TR, BL, BR の順に
並べた 4 バイトエントリの配列で持ちます。

```slang
ARRAY BYTE TILE_IDX[] = {
    0, 1, 2, 3,    // SC 0: 左上=0, 右上=1, 左下=2, 右下=3
    4, 5, 6, 7,    // SC 1
    3, 3, 3, 3,    // SC 2 (全部同じタイル)
    // ...
};
```

#### マップ (W × H バイト, 圧縮形式)

1 バイト = 1 スーパーチップ番号。スーパーチップ単位で扱うので、表示が
32x32 スーパーチップなら 1024 バイトです。

```slang
ARRAY BYTE TILE_MAP[] = {
    0, 1, 2, 1, 0, /* ... 32 個 */,    // 1 行目 (32 スーパーチップ = 64 文字)
    2, 1, 0, 3, 1, /* ... */,          // 2 行目
    // ... 32 行
};
```

### API 一覧

| API | 引数 | 単位 | 動作 |
|---|---|---|---|
| `PCGDEF(idx, ptr)` | idx 0..255, 24 バイト先頭 | — | PCG 1 文字を定義 (※ランタイム関数) |
| `PCGDEFS(startidx, ptr, count)` | — | — | 連続 count 個の PCG を一括定義 (※ランタイム関数) |
| `TILE_INIT()` | — | — | 内部状態クリア + テキスト VRAM 両ページ初期化 |
| `TILE_SET_MAP(map, idx, 0)` | マップ, スーパーチップ表, 予約 (0) | — | 参照するマップとインデックス表を登録 |
| `TILE_SET_MAPSIZE(w, h)` | w, h | **スーパーチップ単位** | マップ全体のサイズ |
| `TILE_SET_SIZE(w, h)` | w, h | **キャラクタ単位** (1=8px) | 表示領域 (ウィンドウ) のサイズ。デフォルト 40x24。**W は偶数推奨** (スーパーチップ 2 単位アライン) |
| `TILE_SET_OFS(x, y)` | x, y | キャラクタ単位 | 表示領域のテキスト VRAM 上での開始位置 |
| `TILE_SET_SCROLL(x, y)` | x, y | キャラクタ単位 | マップスクロール位置。変化時のみ再描画 |
| `TILE_SET_ANIM(threshold)` | threshold | タイル番号 | これ以上のタイル番号をアニメ対象とする (255 で無効) |
| `TILE_SET_ANIM_RATE(ticks, period)` | ticks, period | vsync 回数 / フレーム数 | ticks vsync ごとにフレーム進行, period で循環 |
| `TILE_ANIM_TICK()` | — | — | **`VSYNC_PROC()` から呼ぶ**。アニメカウンタ進行 |
| `TILE_FILL_MAP()` | — | — | マップを VRAM に描画 (dirty スキップ付き)。毎フレーム呼んで OK |
| `TILE_SET_PAGE(offs)` | 0 または 4 | — | 描画ページを明示切り替え (ダブルバッファ初期化等で使用) |
| `TILE_INVALIDATE()` | — | — | 強制再描画フラグを立てる |

### アニメーションのしくみ

PCG 番号の**下位 2 ビット**をアニメフレーム番号 (0..3) で差し替える方式です。

- `TILE_SET_ANIM(T)`: タイル番号 ≧ T のタイルだけが差し替え対象
- `TILE_SET_ANIM_RATE(ticks, period)`:
  - `ticks` … フレーム進行間隔 (vsync 回数)
  - `period` … 1 サイクルのフレーム数 (下位 2 ビットの制約で実質 1〜4)
- `TILE_ANIM_TICK()` を `VSYNC_PROC()` から呼ぶことでカウンタが進む

例: 番号 4〜7 に「歩行 4 コマ」を定義して `TILE_SET_ANIM(4)` にすれば、
タイル 4 が 4→5→6→7→4→… と自動で切り替わります。

```slang
TILE_SET_ANIM(4);
TILE_SET_ANIM_RATE(8, 4);      // 8 vsync ごと, 4 コマ周期

VSYNC_PROC()
{
    TILE_ANIM_TICK();
}
```

### 制約・注意点

- **PCG 番号は 0〜255** (テキスト文字コード空間と共用)
- **スーパーチップ数はマップ byte 値の範囲まで** (1 バイト = 最大 256 種)
- `TILE_FILL_MAP()` は dirty フラグを見て必要な時だけ再描画するので毎フレーム
  呼んで問題なし (変化なしなら実質 no-op)
- ダブルバッファなのでアニメ / スクロール変化は両ページへ反映される
- **`PCGDEF` / `PCGDEFS` は呼んだ時点で PCG RAM へコピー**するので、登録後に
  元のパターンデータを捨てても構いません。一方 `TILE_SET_MAP` はマップと
  スーパーチップ表のポインタを保持するだけなので、**これらは毎フレーム参照
  されます**。動的に書き換えれば次回 `TILE_FILL_MAP` で反映されます
- **スプライトを重ねる場合は SPRLIB と組み合わせる** (TILESPR.LIB を参照)

### 最小サンプル

```slang
#include TILELIB.LIB

ARRAY BYTE TILEPCG[] = { /* 24 × N バイト */ };
ARRAY BYTE TILE_IDX[] = { /* 4 × M バイト */ };
ARRAY BYTE TILE_MAP[] = { /* W × H バイト */ };

VAR SCX, SCY;

VSYNC_PROC()
{
    TILE_ANIM_TICK();
}

MAIN()
{
    WIDTH(40);
    PCGDEFS(0, TILEPCG, 8);

    TILE_INIT();
    TILE_SET_MAP(TILE_MAP, TILE_IDX, 0);
    TILE_SET_MAPSIZE(32, 32);
    TILE_SET_ANIM(4);
    TILE_SET_ANIM_RATE(8, 4);

    SCX = 0; SCY = 0;
    TILE_FILL_MAP();

    LOOP {
        // 入力等に応じて SCX, SCY を更新
        TILE_SET_SCROLL(SCX, SCY);
        TILE_FILL_MAP();
        VSYNC(1);
    }
}
```

### 収録サンプル

| ファイル | 内容 |
|---|---|
| `TILETEST.SL` | PCG 8 タイル / スーパーチップ / 32x32 マップ / キーボードスクロール |

### SPRLIB と併用する場合

`TILESPR.LIB` (統合 shim) を `#include` すると、SPRLIB のダブルバッファ側に
描画ページを同期するヘルパー `TILE_SYNC_SPR_PAGE()` が使えます。
詳細は `examples/tilespr/` 配下を参照してください。
