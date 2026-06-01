# Third-Party Notices

このファイルは、SLANG Compiler の repo / 配布 zip に同梱される **外部バイナリ・テンプレート** の出典 (provenance) を記録します。

scope:
- 含まれる: 外部由来のバイナリ blob (= `tools/udostool.exe`、`runtime/templates/XBIOS.CMT`、`images/templates/*.D88`、Bookworm's Library 由来の `runtime/pc88mk2sr/*.bin` 等)、および明示的な利用許諾を著者から得た third-party source (= PCG 8253 サウンドドライバ等)
- 含まれない: 黙示的に利用してきた runtime code 中の third-party 由来ライブラリ (= MSX SPDRV、X1 SGL 等)。これらの attribution は `LICENSE` ファイルおよび各 ASM source の header コメントを参照してください
- LICENSE 全文を別ファイルで同梱している成果物 (= RunCPM、UI フォント等) については末尾の「LICENSE 別途同梱物」section を参照

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

## PCG ボード搭載 8253 用サウンドドライバ (PC-8001 系)

PCG-8100 後期 / PCG-8200 / PCG-8800 系互換ボード (PSA3.0 等の互換含む) が搭載する Intel 8253 PIT で 3 ch 矩形波出力を扱うサウンドドライバ。SLANG ランタイム形式への移植 + 不具合修正 + 出力周りの最適化を加えたものを `runtime/libpc80mk2_sound.asm` として同梱。

| 項目 | 内容 |
|---|---|
| 配置 | `runtime/libpc80mk2_sound.asm` (= SLANG ランタイム化版) |
| 元 source | `obsolete/lib/pc8001/soundv2.z80` (= 「8253 簡易サウンドドライバ V2 [ 最適化済 ]」、2020/11/27) |
| 著者 | 内藤 時浩 (Tokihiro Naito) |
| 許諾 | **PD (Public Domain) 扱いで利用許可**。著者本人より X (旧 Twitter) 上で 2023/8/17 に確認済 |
| 改変 | あり (= SLANG ランタイム形式への移植、KEYON 動的 mask、SNDOutput shadow 最適化、音長カウンタ修正、休符 `TONE.REST` 追加、`SND_ISPLAYING` 判定修正等) |

---

## mzd88 (MZ-2500 D88 image 操作ツール)

MZ-2500 用 BASIC システム / IOCS 環境 (`-E mz25iocs`) の `slangbuild --emit disk` 機能で使用される D88 image 構築ツール。空 D88 生成 (`-blank`) と main bin / 起動用 BASIC ローダの追加 (`-add`) を担当。

| 配布物名 | repo 配置先 | 配布 zip 配置先 | 取得日 | 改変有無 |
|---|---|---|---|---|
| `mzd88` (osx-arm64 binary) | `tools/mzd88-osx-arm64` | `tools/mzd88` (= rename) | 2026-05-10 | source 改変なし、ローカルで `cc -Os` build |
| `mzd88` (osx-x64 binary) | `tools/mzd88-osx-x64` | `tools/mzd88` (= rename) | 2026-05-10 | source 改変なし、ローカルで `clang -arch x86_64 -Os` build |
| `mzd88` (linux-x64 binary) | `tools/mzd88-linux-x64` | `tools/mzd88` (= rename) | 2026-05-10 | source 改変なし、ローカルで `zig cc -target x86_64-linux-musl -Os -Wl,-s` build |
| `mzd88.exe` (win-x64 binary) | `tools/mzd88-win-x64.exe` | `tools/mzd88.exe` (= rename) | 2026-05-10 | source 改変なし、ローカルで `zig cc -target x86_64-windows-gnu -Os -Wl,-s` build |

**取得元**: `https://github.com/issaUt/mz2500-tools` (= `mzd88.c` の C 実装)

**License**: MIT License (= `LICENSE` ファイルあり、Copyright (c) 2026 issaUt)

**用途**: MZ-2500 用 D88 disk image を `slangbuild --emit disk -E mz25iocs` で生成する際に、`mzd88 -blank` で空 D88 を作成し、`mzd88 -add` で main bin (`PROG.OBJ`) と起動用 BASIC ローダ (`runtime/mz2500/J8000.bas.bsd`) を格納します。

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

## banjo music driver (Furnace tracker → Z80)

X1 用 banjo driver sample / runtime integration で使用する third-party source。Furnace tracker の曲データを Z80 driver 用データへ変換し、AY/PSG や OPM/YM2151 等で再生する。

| 項目 | 内容 |
|---|---|
| 配置 | `runtime/x1/banjo/upstream/`, `runtime/x1/banjo/ay/`, `tools/json2sms_x1.py` |
| 取得元 | `https://github.com/joffb/banjo` |
| 著者 | Joe Kennedy |
| License | MIT License |
| 改変 | `upstream/` 配下は必要ファイルのみ vendoring。X1 用 wrapper / I/O adapter / AY port は `runtime/x1/banjo/` 側に別ファイルとして追加 |

vendoring 対象は Core (`music_driver/banjo/`)、OPM chip driver (`music_driver/opm/`)、AY chip driver (`runtime/x1/banjo/ay/` に X1 port として配置)、および変換ツール (`furnace2json.py` / `json2sms.py` / `tools/json2sms_x1.py`) のみ。他 chip、examples、旧スクリプト等は含めない。

LICENSE 全文は `runtime/x1/banjo/upstream/LICENSE.md` および repo root `LICENSE` を参照。

---

## LICENSE 別途同梱物

以下の同梱物は LICENSE 全文を別ファイルとして同梱しています。詳細は各 LICENSE ファイルを参照してください。

| 同梱物 | LICENSE ファイル | 概要 |
|---|---|---|
| RunCPM (`tools/runcpm/RunCPM-*` + `tools/runcpm/cpm/EXIT.COM` + `tools/runcpm/cpm/SUBMIT.COM`) | `tools/runcpm/LICENSE` | CP/M 2.2 互換エミュレータ |
| `assets/ui/` 配下の UI フォント | `assets/ui/LICENSE.font` | UILIB 用ピクセルフォント |
