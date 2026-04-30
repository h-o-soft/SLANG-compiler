# Third-Party Notices

このファイルは、SLANG Compiler の repo / 配布 zip に同梱される **外部成果物の出典 (provenance)** を記録します。

LICENSE 全文を別ファイルで同梱している成果物 (= RunCPM、UI フォント等) については末尾の「LICENSE 別途同梱物」section を参照してください。

各エントリには判明している範囲で以下を記載します:
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

PC-8001mkII XBIOS 直接環境 (`-E pc80mk2x` / `-E pc80mk2xsd`) の build で `slangbuild` が使用する `XBIOS.CMT` (= 0000H に load される bootstrap binary、3,726 byte)。

| 項目 | 内容 |
|---|---|
| 配置 | `runtime/templates/XBIOS.CMT` |
| 出典 | Oh!MZ 1987 年 9 月号掲載の PC-8001/8801 用 S-OS が原典 (TITY SOFT 1986 の XBIOS for NEC PC-8801 SERIES, Version 1.00, Revision 1.00) |
| source | `obsolete/lib/pc8001/XBIOS/XBIOSMAIN.ASM` (改変済 source) |
| 同梱履歴 | initial commit より前から `obsolete/` に同梱 |
| 取得日 | 不明 |
| 改変 | あり (h-o-soft によるカラー化改変) |
| 許諾 | 明示的記載なし |

---

## LSXPROG.D88 (LSX-Dodgers boot disk template)

lsx / x1 環境 (`-E lsx` / `-E x1`) の `slangbuild --emit disk` 機能でテンプレート D88 として使用。

| 項目 | 内容 |
|---|---|
| 配置 | `images/templates/LSXPROG.D88` |
| 出典 | LSX-Dodgers (https://github.com/tablacus/LSX-Dodgers) 由来の boot disk image (LSX-Dodgers 1.62c) |
| 同梱履歴 | initial commit より前から repo 同梱 |
| 取得日 | 不明 |
| License | MIT (= LSX-Dodgers の LICENSE による) |
| 改変 | なし |

---

## LICENSE 別途同梱物

以下の同梱物は LICENSE 全文を別ファイルとして同梱しています。詳細は各 LICENSE ファイルを参照してください。

| 同梱物 | LICENSE ファイル | 概要 |
|---|---|---|
| RunCPM (`tools/runcpm/RunCPM-*` + `tools/runcpm/cpm/EXIT.COM` + `tools/runcpm/cpm/SUBMIT.COM`) | `tools/runcpm/LICENSE` | CP/M 2.2 互換エミュレータ |
| `assets/ui/` 配下の UI フォント | `assets/ui/LICENSE.font` | UILIB 用ピクセルフォント |
