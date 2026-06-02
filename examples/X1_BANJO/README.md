# X1_BANJO — banjo (Furnace tracker) driver sample

Sharp X1 で [banjo](https://github.com/joffb/banjo) サウンドドライバ (Joe Kennedy, MIT) を使い、
Furnace tracker の AY/PSG または OPM/FM 曲を鳴らす SLANG サンプルです。
`CHIP=ay` では AY/PSG の SFX (効果音) 再生も行います。

## 必要なもの

- 曲データ `assets/song.fur` (Furnace tracker、 AY/PSG または OPM/YM2151 用に作成したもの)
- (SFX を使う場合) `assets/sfx.fur` (Furnace tracker、 AY/PSG。 `CHIP=ay` のときのみ使用)
- `wla-z80` / `wlalink` (banjo driver と曲のアセンブル)
- `python3` (Furnace → Z80 変換ツール)
- 各 env の同梱ツール (`ndc` / `HuDisk.exe` + `mono` 等)

曲データ / SFX データは付属しません。 Furnace tracker で作り `assets/song.fur` (および SFX 用に `assets/sfx.fur`) として置いてください。

## ビルド

```sh
cd examples/X1_BANJO
make x1              # LSX-Dodgers      → BANJOMP_x1.d88
make sosx1          # S-OS for X1       → BANJOMP_sosx1.d88
make x1native       # OS なし cassette  → BANJOMP_x1native.tap
make x1native_slfs  # OS なし SLFS disk → BANJOMP_slfs.d88
make all            # 上記すべて
```

既定は `CHIP=opm` です。AY 曲を鳴らす場合は `CHIP=ay` を指定します。

```sh
make all CHIP=ay
```

`SONG_FUR` を指定すると、任意の `.fur` を直接使えます。

```sh
make x1native CHIP=ay SONG_FUR=../../refs/banjo/examples/cmajor_ay.fur
```

### SFX (効果音、 `CHIP=ay` のみ)

`CHIP=ay` のときだけ `assets/sfx.fur` が SFX として同梱され、実行中に **SPACE** で再生できます
(`CHIP=opm` / `both` は BGM のみ)。`SFX_CH=0..2` で SFX を載せる PSG ch を指定します (既定 `0`)。
SFX データは指定 ch にノートを置いた AY 曲で、その ch を `-s` で抜き出します。

```sh
# 3ch目 (index 2) に作った SFX を鳴らす例
make x1 CHIP=ay SFX_CH=2 SONG_FUR=../../refs/banjo/examples/cmajor_ay.fur
```

SFX を載せた PSG ch は、SFX 再生中だけ BGM 側が一時 mute されます (同じ物理 ch を借りるため)。

## 仕組み

driver と曲を本体に埋め込まず、 実行時にメモリへロードして再生します:

- **driver bin** (`driver.bin`, $8000)
  `runtime/x1/banjo/build_driver.sh` が banjo Core + chip driver (OPM / AY / both) + X1 用 jump table wrapper を
  固定アドレスでアセンブルした、 曲を含まない共有 bin。 chip は `--chip opm|ay|both` で選択。
- **曲 bin** (`song.bin`, $B000)
  `assets/song.fur` を `furnace2json.py` → `json2sms.py` / `json2sms_x1.py` で Z80 データ化し、 driver の関数
  アドレスを EQU 注入 (`banjo_extract_syms.py`) して曲単体でアセンブルしたもの。

SLANG からは env 非依存の中間層 (`BANJO_INIT` / `BANJO_PLAY` / `BANJO_UPDATE` / `BANJO_STOP` /
`BANJO_END`、 SFX は `BANJO_SFX_PLAY` / `BANJO_SFX_STOP`) を呼ぶだけです。 詳細は
`runtime/libx1_banjo.asm` と `docs/X1.md` を参照してください。

`CHIP=ay` では `sfx.bin` も同様に実行時ロードされ、`BANJO_SFX_PLAY(SFX_ADDR)` で再生します。

sample は CTC が見つかれば CTC ch1 で 60Hz 近似 tick を使い、見つからない場合は
`VSYNC_PROC` からの polling update に自動 fallback します (BGM と SFX は同じ update 経路で進む)。
終了時は `BANJO_END()` が CTC vector を復元し、SFX も止めてから曲を止めます。

## メモリ配置

| 構成 | driver | song | sfx | banjo RAM |
|---|---|---|---|---|
| `CHIP=opm` / `both` | `$8000` | `$B000` | — | `$C000`〜 |
| `CHIP=ay` (SFX 有) | `$8000` | `$9000` | `$B000` | `$C000`〜 |

`SONG_ADDR` / `SFX_ADDR` は Makefile が `BANJOMP.config.inc` に生成し、曲/SFX bin の焼込アドレス
(`--load-addr`) と SLANG 側ロード先を同一ソースから揃えます。配置を変える場合は Makefile の
`DRV_ORG` / `SONG_ORG` / `SFX_ORG` / `BANJO_RAM` を変更してください (config.inc 経由で SL に反映)。
`banjo RAM` ($C000〜) はプログラム使用不可です。

AY 曲では `json2sms_x1.py --ay-master-clock 4000000` を使い、X1 PSG の 2MHz 駆動に合わせて tone/envelope period を焼き込みます。

## 操作

曲が鳴ります。`CHIP=ay` では **SPACE** で SFX を再生できます。**ENTER** で終了します。
