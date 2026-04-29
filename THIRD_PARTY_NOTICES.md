# Third-Party Notices

このファイルは、SLANG Compiler の repo / 配布 zip に同梱される **外部成果物の出典 (provenance)** を記録します。新規追加または更新される同梱物から順次記録します (= 既存同梱物のうちまだ記載されていないものは別 PR で順次追記予定)。

各エントリには以下を記載します:
- 配布物名
- 取得元 URL
- 取得日
- サイト上の許諾文 (= license)
- 改変有無

---

## Bookworm's Library 由来 (PC-8801mkII SR 用)

PC-8801mkII SR 環境 (`-E pc88mk2sr`) の `slangbuild --emit disk` 機能で使用される以下の成果物は、**Bookworm's Library** で公開されている **汎用ディスクルーチン** (`filesys_20141128` 系) のサンプルアーカイブ由来。

| 配布物名 | repo 配置先 | 取得日 | 改変有無 |
|---|---|---|---|
| `udostool.exe` | `tools/udostool.exe` | 2026-04-29 | 改変なし |
| `ipl.bin` | `runtime/pc88mk2sr/ipl.bin` | 2026-04-29 | 改変なし (`ipl.z80` ソース付き) |
| `subsys.bin` | `runtime/pc88mk2sr/subsys.bin` | 2026-04-29 | 改変なし (`subsys.z80` ソース付き) |
| `iosys.bin` | `runtime/pc88mk2sr/iosys.bin` | 2026-04-29 | 改変なし (`iosys.z80` ソース付き) |
| `PC88MK2SR.D88` (boot disk template) | `images/templates/PC88MK2SR.D88` | 2026-04-29 | 改変なし |

**取得元**: Bookworm's Library で公開されているサンプルアーカイブ `filesys_20141128.zip` (= 2014 年公開の汎用ディスクルーチン)。

**元サイト URL**: `http://mydocuments.g2.xrea.com/index.html` (= 現在は閉鎖、アーカイブ経由でのみ取得可能)。`web.archive.org` で上記 URL を検索すると当時のページ snapshot が確認できる。

**サイト上の許諾文**: 「**改変含め自由に使ってください**」(= サイト記載文をそのまま引用)。これを license として信用し、repo + 配布 zip に同梱しています。

**用途**: PC-8801mkII SR 用 disk image を `slangbuild --emit disk -E pc88mk2sr` で生成する際に、`udostool.exe` で `ipl.bin` (boot loader) / `subsys.bin` (disk subsystem) / `iosys.bin` (main disk system) を template に書き込み、main bin と overlay bin を `D88` 内に格納します。

---

## XBIOS.CMT (PC-8001mkII XBIOS bootstrap binary)

PC-8001mkII XBIOS 直接環境 (`-E pc80mk2x`) の CMT 結合 build で使用される `XBIOS.CMT` (= 0000H に load される bootstrap binary、3,726 byte) の出典:

| 項目 | 内容 |
|---|---|
| 配置 | `runtime/templates/XBIOS.CMT` (repo 内のみ、配布 zip からは除外) |
| 用途 | pc80mk2x build 時に slangbuild が main.cmt の直後に結合する bootstrap binary (= 0000H 配置の XBIOS が main code から参照される) |
| 出典 | Oh!MZ 1987 年 9 月号掲載の PC-8001/8801 用 S-OS が原典 (= TITY SOFT 1986 の XBIOS for NEC PC-8801 SERIES, Version 1.00, Revision 1.00) |
| 改変者 | h-o-soft (= 本リポジトリの主作者) による SHARP X1 風カラー化改変版 |
| source | `obsolete/lib/pc8001/XBIOS/XBIOSMAIN.ASM` (= 改変済 source、`README.md` 同梱) |
| 同梱履歴 | 本 PR より前から `obsolete/lib/pc8001/XBIOS/` に同梱 |
| 取得日 | 不明 (= initial commit から存在) |
| 改変 | あり (= XBIOSMAIN.ASM 内 `README.md` 記載のカラー化改変) |
| 許諾 | 明示的記載なし |

**配布 zip の扱い**: 本 PR 時点では `publish.sh` で配布対象から除外
(= license 確認完了まで、`cp -r ../../runtime .` の直後に
`rm -f runtime/templates/XBIOS.CMT`)。配布 zip 解凍環境で pc80mk2x を使う
ユーザーは repo から `runtime/templates/XBIOS.CMT` を手動で installed
`~/.config/SLANG/runtime/templates/` にコピーする運用。license 確認完了
後は `publish.sh` の `rm -f` 行を削除するだけで配布対象化。

---

## 今後追記予定

以下の既存同梱物については本ファイルへの provenance 追記が未完了です。今後の PR で順次整理します:

- `tools/ndc` (euee 製、D88 操作ツール)
- `tools/HuDisk.exe` (ho-ogino/HuDisk fork、S-OS 系 D88 操作ツール)
- `tools/AILZ80ASM` (AILight 製、Z80 アセンブラ)
- `tools/runcpm/` (RunCPM、CP/M 2.2 互換エミュレータ)
- `images/templates/LSXPROG.D88` (LSX-Dodgers template)
- `images/templates/SOSPROG.D88` (S-OS template、setup-tools 経由取得)
