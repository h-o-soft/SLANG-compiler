## PCG ボード搭載 8253 サウンドドライバ用 MML サンプル

このフォルダは `runtime/libpc80mk2_sound.asm` 向けの音楽データを書くための
MML サンプルと、それを変換する `tools/mml2sound.py` の使用例を置く場所です。

サウンドドライバは **PCG-8100 (後期) / PCG-8200 / PCG-8800 系互換ボード**
(PSA3.0 等の互換も含む) に搭載されている **Intel 8253 PIT 3 ch 矩形波
出力**を対象にしています。PC-8001 / PC-8001mkII / PC-8801 本体内蔵の単音
BEEP (= 1 ch ON/OFF のみ) ではないので、対応 PCG ボード (もしくはエミュ
レータ上の同等機能) が必要です。

元ドライバは内藤時浩 (Tokihiro Naito) 氏作の「8253 簡易サウンドドライバ
V2」(2020/11/27、`obsolete/lib/pc8001/soundv2.z80` に同梱) を、著者の
許諾 (PD 扱い、2023/8/17) のもと SLANG ランタイム形式に組み込んだもの
です。詳細は `THIRD_PARTY_NOTICES.md` 参照。

- `chouchou.mml` — 「ちょうちょ」8 小節 verse (melody + bass + harmony 3ch)
- 変換ツール本体は `tools/mml2sound.py` (Python 3、追加依存なし)

### 変換と組込み

MML → ASM 変換:

```
python3 tools/mml2sound.py examples/pc80mk2/chouchou.mml > /tmp/chouchou.asm
```

出力された `SOUNDDATA: ... db 0x80` の塊 (3 ch ぶん) を SLANG ソース内の
`#ASM` ブロック既存の `SOUNDDATA:` 部分と置換して `SND_PLAY(MUSADR)` で
再生します (実装例は `examples/PC80mk2.SL`)。

`--binary <prefix>` で per-channel raw 出力も可能です (= `incbin` 用)。

### MML 構文 (mml2sound.py サポート範囲)

```
@1 o4 l4         ; channel 1 開始、octave 4、default length 4 (= quarter)
g e e2 r4        ; G・E・E (half) ・rest (quarter)
> c < c          ; octave up / down (= o5 c o4 c と等価)
c+ d# f-         ; sharp / flat
c4. e8.          ; dotted (= 1.5x)
;                ; 行末コメント (// と /* */ も可)
```

- 音名: `c d e f g a b`、シャープ `+` or `#`、フラット `-` or `_`
- 八度: `o<N>` (1..6) / `>` (up) / `<` (down)
- 長さ: `l<N>` (= default、whole=1 / half=2 / quarter=4 / eighth=8 / 16th=16)
  + per-note 上書き (`c4` 等) + dotted (`c4.` `c4..`)
- 休符: `r [length] [dot]`
- channel marker: `@<name>` (任意の英数字、出力 label は `.@<name>`)
- 駆動 tick: 1 tick = 1 VSYNC frame (≒ 16.67ms @ 60Hz)。`--quarter <N>` で
  quarter note の tick 数を変更可 (default = 16、whole = 64 ticks)

### 物理 channel と MML channel の対応 ⚠

driver の `SNDMusicStart` は `BGM` の最初のポインタを物理 CH3 に読み込む
構造になっているため、`mml2sound.py` は MML channel 順を自動的に逆順に
出力して以下のように対応させます:

| MML 順 | 物理 CH | 備考 |
|---|---|---|
| 1 番目 (`@1`) | CH1 | clean voice (SE 多重化なし) |
| 2 番目 (`@2`) | CH2 | clean voice |
| 3 番目 (`@3`) | CH3 | **SE 多重化対象** (`SND_SEPLAY` 中は SE 側が優先される) |

→ メロディは `@1`、効果音で一時的に消えても良い voice (= harmony 等) は
`@3` に置くのが自然です。逆に「SE で melody を一瞬中断する古典的ゲーム
スタイル」が欲しい場合は MML 内で channel 順を逆に書いてください。

`@1`〜`@3` のうち欠けたチャンネルは `.__empty: db 0x80` で自動 padding
されます (= driver の 3 ポインタ読込が壊れない)。

### SOUNDDATA byte format (driver が消費する byte 列)

```
length byte: -1..-127 (signed)  次の note の長さ (tick 数) を更新
note byte:   0x00..0x71         TONE.O<N> + TONE.<note> = (octave-1)*12 + semi
rest byte:   0x7F               TONE.REST、note と同じ長さで FREQ=0 (silence)
end marker:  0x80               この channel の MML データ終端
```

length byte は省略可 (= 直前の length が継続)。`mml2sound.py` は同 length
が続く場合は length byte を出力しません (= 圧縮)。

`TONE.O<N>` (N=1..6) と `TONE.<note>` (`.C` `.CP` `.D` ... `.B`) は
`runtime/libpc80mk2_sound.asm` の `TONE:` block で定義されています
(`.REST equ 0x7F` も同所)。

### サンプル: examples/PC80mk2.SL

`SOUNDDATA:` 直後の `BGM: dw .@3, .@2, .@1` 以下が `mml2sound.py` の出力を
そのまま貼ったもの。`MAIN()` 内で `SND_INIT()` → `SND_PLAY(MUSADR)` で
再生開始、`SND_PROC()` を VSYNC ごとに呼んで進行、`SND_ISPLAYING()` で
終端判定 → loop 再生、という basic な構成です。
