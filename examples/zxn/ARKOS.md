# ZX Spectrum Next の Arkos Tracker (AKM) の見本

[Arkos Tracker](https://www.julien-nevo.com/arkostracker/) の曲 (`.akm`) と効果音 (`.akx`) を、Next の AY で鳴らす見本。
操作: BGM が鳴る → SPACE で効果音 → ENTER で止める。

## 組み方

```sh
make arkos            # PSGAKM.bin (RASM) → ARKOS.bin (slangbuild) → 置き場の検査 → ARKOS.nex (nexcreator) → CSpect
make ARKOS.nex RASM=/path/to/rasm   # 道具の場所は RASM= / SLANGBUILD= / NEXCREATOR= で渡せる
```

再生部 `PSGAKM.bin` は、`runtime/zxn/playerAkm_zxn_wrapper.asm` を RASM で `$E000` に組んだもの (組んだ bin は commit しない)。
曲と効果音は `../X1_ARKOS/BGM.AKM`・`SE.AKX` を使う。

| 番地 | 中身 |
|---|---|
| `$8000`〜 | 本体 `ARKOS.bin` (末尾 ≤ `$B000`) |
| `$B000`〜 | 曲 `BGM.AKM` (書き出したときの番地。末尾 ≤ `$C000`) |
| `$C000`〜 | 効果音 `SE.AKX` (書き出したときの番地。末尾 ≤ `$D000`) |
| `$D000`〜 | zxn のワーク (末尾 ≤ `$E000 − 512`。`ARKOS.sym` の `__WORKEND__` で調べる) |
| `$E000` から下 | スタック (zxn の初めに SP を `$E000` にする。cfg の `!PCSP` の SP は書き換わる) |
| `$E000`〜 | 再生部 `PSGAKM.bin` (末尾 ≤ `$FD00`) |

Makefile は、どれかが重なると止まる。使っている間は `$8000`〜`$FFFF` の割り当て (MMU) を変えない。

## 再生部の呼び方

`ARKOSZXN.SL` を `#INCLUDE` すると、`MACHINE` で番地を呼べる (Next の SLANG IDE でも同じ宣言を使える)。

| 番地 | 宣言 | 引数 |
|---|---|---|
| `$E000` | `ARKOS_INIT()` | なし。TurboSound を有効にして AY 0 を選び、音を止める |
| `$E003` | `ARKOS_BGM_PLAY(曲)` | 曲 (.akm) の番地 |
| `$E006` | `ARKOS_BGM_STOP()` | |
| `$E009` | `ARKOS_BGM_PAUSE()` | |
| `$E00C` | `ARKOS_BGM_RESUME()` | |
| `$E00F` | `ARKOS_SFX_INIT(表)` | 効果音 (.akx) の番地 |
| `$E012` | `ARKOS_SFX_PLAY(番号, チャンネル)` | 番号は 1 から、チャンネルは 0〜2 (L＝番号、E＝チャンネル) |
| `$E015` | `ARKOS_SFX_STOP(チャンネル)` | 0〜2 |
| `$E018` | `ARKOS_PSG_PROC()` | 1 フレームに 1 回 |
| `$E01B` | `ARKOS_PSG_END()` | `BGM_STOP` と同じ (この後 `PSG_PROC` を呼んでも鳴らない) |

- 表の並びは X1 の包み (`runtime/x1/playerAk[gm]_x1_wrapper.asm`) と同じ。ただし効果音のチャンネルは X1 は H、zxn は E で受けるので、bin どうしの入れ替えはできない。
  X1 の中間層 `ARKOS_*` (`runtime/libx1_arkos.asm`) は zxn では使わない (`ARKOS_SET_CTC_PORT` が zxn の包みを壊す)
- すべての入口で、呼んだときの AF・IX・IY・SP を保ち、割り込みの状態を元に戻す (中では割り込みを止める。再生部が SP を自分の表に切り替えるため)。
  `ARKOS_PSG_PROC` は、そのほかのレジスタ (HL・DE・BC と裏の組) もすべて保つ

## 呼ぶ側の決まり

- `ARKOS_PSG_PROC` は 1 フレームに 1 回呼ぶ。曲の速さは、1 秒に呼ぶ回数で決まる。曲の再生の周波数は Arkos の曲 (`.aks`) の性質で、書き出した `.akm` には入らない。
  Arkos の曲はふつう 50Hz なので、見本は画面を 50Hz にして 1 フレームに 1 回呼ぶ。60Hz で作った曲は 60Hz の画面で呼ぶ
  (`BGM.AKM` は X1 の見本では約 60Hz で鳴らしていたので、50Hz の Next では少しゆっくり鳴る)
- `ZXN_VSYNC` は `HALT` だけなので、ほかの割り込みがあると 1 フレームに 1 回にならない。見本は初めにフレームの割り込み (ULA) だけを許す
  (NextReg `$C4` = `$01`、`$C5`・`$C6` = 0)
- 効果音は BGM が動いている間だけ進む (`PSG_PROC` が曲の再生の中で効果音も進める)
- 使っている間、ほかの処理が AY の選択 (ポート `$FFFD`) を変えない

## 音の高さ

Next の AY は 1.75MHz なので、再生部は Pentagon の周期表 (`PLY_AKM_HARDWARE_PENTAGON`) で組む (Spectrum 128 の 1.7734MHz ではない)。
根拠は、公式の FPGA のソース (`cores/zxnext/src/zxnext_top_issue4.vhd` の `CLK_28_PSG_EN <= clkdiv_3_0; -- AY clock enable @ 1.75MHz`) と、
実機での測り (AY に周期 249 の音を出すとチューナーで A4 の約 −3 セント。1.7734MHz なら約 +20 セントになる)。

曲 (`.akm`) は音の番号で書かれているので、周期表で Next の高さになる。効果音 (`.akx`) は周期の値で書かれた音を持つことがあり、
その音は周期表では直らない。見本の `SE.AKX` は X1 の見本 (2MHz) のものなので、Next では高さがずれるかもしれない。Next 用には書き出し直す。

## AKG (Generic) について

いまは AKM だけ。AKG を足すときは、`playerAkm_zxn_wrapper.asm` を写して、include する再生部と `PLY_AKM_` の名前を AKG のものに替える。
表の並び・番地・`ARKOSZXN.SL` の宣言は共通 (AKM と AKG は同じ番地に置くので、同時には使えない)。

## ライセンス

再生部の本体は Targhan/Arkos の Arkos Tracker 3 player (MIT License)。リポジトリの `LICENSE` の Arkos の節を参照。
