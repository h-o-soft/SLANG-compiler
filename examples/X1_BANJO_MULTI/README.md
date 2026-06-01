# X1_BANJO_MULTI — banjo 複数楽曲・複数SFX バンドルサンプル

Sharp X1 で [banjo](https://github.com/joffb/banjo) (Joe Kennedy, MIT) を使い、
**複数の曲と複数の効果音 (SFX) を 1 アプリにまとめて**鳴らす SLANG サンプルです。
単一曲の [`examples/X1_BANJO`](../X1_BANJO) と違い、 manifest にアセットを列挙すると
ビルドが自動でアドレスを割り当て、 `MUSIC_*` / `SFX_*` のアドレス定数を生成します
(人手のアドレス計算は不要)。

## 仕組み

banjo の曲/SFX データは内部ポインタが**絶対アドレスで焼き込まれ**(位置依存)、
ビルドしたアドレスでしかロードできません。複数載せるには各データを固有アドレスで
ビルドする必要があります。これを `tools/banjo_pack_assets.py` が自動化します:

1. `assets/banjo_assets.txt` (manifest) のアセットを上から順に、 driver の直後から
   順次アドレスでビルド
2. 全部を 1 つの bundle (`banjodat.bin`) に連結
3. 各アセットの先頭アドレスを `BANJOMULTI.assets.addr.inc` に CONST として出力
   (例: `bgm1.fur` → `CONST MUSIC_BGM1 = $...;`、 `beep.fur` → `CONST SFX_BEEP = $...;`)

SLANG からは bundle を 1 回ロードし、 生成定数で `BANJO_PLAY(MUSIC_BGM1, ...)` /
`BANJO_SFX_PLAY(SFX_BEEP)` するだけです。

## 必要なもの

- `assets/` に置く `.fur` (Furnace tracker、 **AY/PSG**):
  - `bgm1.fur` / `bgm2.fur` … BGM (AY 3ch)
  - `beep.fur` … SFX (AY。 ch2 にノートを置いたもの。 manifest の `sfx beep.fur 2` の `2` がその ch)
- `wla-z80` / `wlalink` / `python3` / 各 env の同梱ツール (`ndc` / `HuDisk.exe` + `mono`)

`.fur` は配布に含めません。 Furnace tracker で用意して `assets/` に置いてください。

## ビルド

```sh
cd examples/X1_BANJO_MULTI
make x1              # LSX-Dodgers      → BANJOMULTI_x1.d88
make sosx1          # S-OS for X1       → BANJOMULTI_sosx1.d88
make x1native       # OS なし cassette  → BANJOMULTI_x1native.tap
make x1native_slfs  # OS なし SLFS disk → BANJOMULTI_slfs.d88
make all
```

既定は `CHIP=ay` (BGM + SFX)。OPM 曲にする場合は `CHIP=opm` (この場合 SFX は不可。
manifest から `sfx` 行を外し、 OPM 曲を並べ、 下記の定数名も合わせる)。

## manifest とアドレス定数

`assets/banjo_assets.txt`:

```
music bgm1.fur
music bgm2.fur
sfx   beep.fur 2
```

- 行の順 = ロード順 = アドレス順。`# コメント` / 空行は無視。
- 定数名は `.fur` の basename を大文字化・英数字以外を `_` にしたもの (`bgm1.fur` → `BGM1`)
  に `MUSIC_` / `SFX_` を付けたもの。`*_SIZE` と順序 alias (`BANJO_MUSIC_0` 等) も生成されます。
- **manifest を変更したら `BANJOMULTI.SL` の参照定数名 (`MUSIC_BGM1` 等) も合わせてください**
  (本サンプルは上記 manifest 前提。 ファイル名を変えると定数名が変わります)。
- bundle が RAM 領域 (`$C000`) を超える数のアセットを入れるとビルドが error で止まります
  (全アセット常駐モデルの上限)。

## メモリ配置

| 領域 | アドレス | 内容 |
|---|---|---|
| driver | `$8000` | banjo driver bin |
| bundle | driver 末尾〜 | 全曲/SFX を連結した `banjodat.bin` (各アセットは生成定数のアドレス) |
| banjo RAM | `$C000`〜 | driver の work RAM (プログラム使用不可) |

`BANJODAT_BASE` (bundle 先頭) は driver サイズから自動算出され、 ビルドと SLANG 側で一致します。

## 操作

- `1` / `2` … 曲を切り替え
- `SPACE` … SFX を再生 (`CHIP=ay` のみ)
- `ENTER` … 終了
