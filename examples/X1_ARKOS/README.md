# X1 Arkos Tracker AKG driver sample

[Arkos Tracker](https://www.julien-nevo.com/arkostracker/) の楽曲 (`.akg`) / 効果音 (`.akx`) を X1 の PSG で再生する sample。 全 X1 系 env (`x1` / `sosx1` / `x1native` / `x1native_slfs`) で **1 ソース** (`ARKOSMP.SL`) のまま動作する。

操作: BGM が鳴る → SPACE で効果音 → ENTER で終了。

## ビルド

driver の RASM ビルド・サイズ定数生成・アドレス重なりチェック・各 env の asset 同梱は Makefile が担当する。

```sh
make x1            # → ARKOSMP_x1.d88       (LSX-Dodgers、 ndc で asset 同梱)
make sosx1         # → ARKOSMP_sosx1.d88    (S-OS for X1、 HuDisk で asset 同梱)
make x1native      # → ARKOSMP_x1native.tap (OS なし cassette tape、 多段連結)
make x1native_slfs # → ARKOSMP_slfs.d88     (OS なし SLFS disk)
make all
make clean
```

ツールパスは override 可: `make x1native SLANGBUILD=... RASM=... NDC=... HUDISK=...`。

## ファイル

| file | 説明 |
|---|---|
| `ARKOSMP.SL` | sample 本体 (= 4 env 1 ソース、 実行時ロード) |
| `Makefile` | 4 env build orchestration |
| `BGM.AKG` | 楽曲データ (Arkos export、 `$B000` 前提) |
| `SE.AKX` | 効果音バンク (Arkos export、 `$C000` 前提) |

ビルド生成物 (`PSGAKG_8000.bin` / `ARKOSMP.sizes.inc` / `ARKOSMP.assets.inc` / `*.d88` / `*.tap` / `*.ASM` 等) は Makefile が再生成するため commit しない。

## 仕組み

driver / BGM / SFX は本体に embed せず、 各 env のネイティブ手段で実行時ロードする:

- `x1` / `sosx1`: `FOPEN` + `FREAD` でディスクから読む
- `x1native`: `MTREAD` で多段 tape の後続ステージを読む
- `x1native_slfs`: `FS_READ_BY_ID` で SLFS asset を読む

ロード後は env 非依存の中間層 (`AKG_*`) を呼ぶ。 CTC があれば割り込み駆動、 なければ VSYNC polling に自動 fallback。 詳細は [docs/X1.md](../../docs/X1.md) の「Arkos Tracker music driver」 を参照。

## ライセンス

driver 本体 (`runtime/x1/PlayerAkg_x1.asm` 等) は Targhan/Arkos の X1 player (MIT License)。 各 asm ファイルの header に attribution あり。
