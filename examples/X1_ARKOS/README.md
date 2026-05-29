# X1 Arkos Tracker driver sample (AKG / AKM)

[Arkos Tracker](https://www.julien-nevo.com/arkostracker/) の楽曲 / 効果音 (`.akx`) を X1 の PSG で再生する sample。 楽曲フォーマット 2 種に対応する:

- **AKG** (generic、 高速) — `ARKOSMP.SL` + `BGM.AKG`
- **AKM** (minimalist、 軽量・driver が小さい) — `AKMMP.SL` + `BGM.AKM`

どちらも全 X1 系 env (`x1` / `sosx1` / `x1native` / `x1native_slfs`) で **1 ソース**のまま動作する。 中間層呼び出し (`ARKOS_*`) は AKG / AKM 共通で、 違いは driver bin と楽曲 data だけ (= 楽曲フォーマットの差は driver 内で吸収)。

操作: BGM が鳴る → SPACE で効果音 → ENTER で終了。

## ビルド

driver の RASM ビルド・サイズ定数生成・アドレス重なりチェック・各 env の asset 同梱は Makefile が担当する。

```sh
# AKG (generic)
make x1            # → ARKOSMP_x1.d88       (LSX-Dodgers、 ndc で asset 同梱)
make sosx1         # → ARKOSMP_sosx1.d88    (S-OS for X1、 HuDisk で asset 同梱)
make x1native      # → ARKOSMP_x1native.tap (OS なし cassette tape、 多段連結)
make x1native_slfs # → ARKOSMP_slfs.d88     (OS なし SLFS disk)

# AKM (minimalist) — 同じ 4 env を akm- prefix で
make akm-x1 / akm-sosx1 / akm-x1native / akm-x1native_slfs

make akg    # AKG 4 env まとめて
make akm    # AKM 4 env まとめて
make all    # AKG / AKM 両方
make clean
```

ツールパスは override 可: `make x1native SLANGBUILD=... RASM=... NDC=... HUDISK=...`。

## ファイル

| file | 説明 |
|---|---|
| `ARKOSMP.SL` | AKG sample 本体 (= 4 env 1 ソース、 実行時ロード) |
| `AKMMP.SL` | AKM sample 本体 (= ARKOSMP.SL と中間層・配置は同一、 driver/楽曲だけ差し替え) |
| `Makefile` | AKG / AKM × 4 env build orchestration |
| `BGM.AKG` | AKG 楽曲データ (Arkos export、 `$B000` 前提) |
| `BGM.AKM` | AKM 楽曲データ (Arkos export、 `$B000` 前提) |
| `SE.AKX` | 効果音バンク (Arkos export、 `$C000` 前提、 AKG / AKM 共通) |

ビルド生成物 (`PSGAK[GM]_8000.bin` / `*.sizes.inc` / `*.assets.inc` / `*.d88` / `*.tap` / `*.ASM` 等) は Makefile が再生成するため commit しない。

## 仕組み

driver / BGM / SFX は本体に embed せず、 各 env のネイティブ手段で実行時ロードする:

- `x1` / `sosx1`: `FOPEN` + `FREAD` でディスクから読む
- `x1native`: `MTREAD` で多段 tape の後続ステージを読む
- `x1native_slfs`: `FS_READ_BY_ID` で SLFS asset を読む

ロード後は env 非依存の中間層 (`ARKOS_*`、 AKG / AKM 共通) を呼ぶ。 CTC があれば割り込み駆動、 なければ VSYNC polling に自動 fallback。 詳細は [docs/X1.md](../../docs/X1.md) の「Arkos Tracker music driver」 を参照。

> 本 sample は実行時ロード方式なので driver/data の embed 変換は不要。 driver/data を**本体 binary に焼き込みたい**場合 (= 1 file 配布等) は `tools/arkos_bin_to_asm.py` で `.bin` を `#ASM INCLUDE` 用 asm に変換できる (= 補助ユーティリティ、 標準ビルドの主経路では未使用)。

## ライセンス

driver 本体 (`runtime/x1/PlayerAk[gm]_x1.asm` 等) は Targhan/Arkos の X1 player (MIT License)。 各 asm ファイルの header に attribution あり。
