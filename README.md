# SLANG-compiler
SLANG Compiler (Z80) 0.23.0

# 概要

これは主に国産8bit PCで使われたOS「S-OS」オリジナルの構造型コンパイラ言語「SLANG」のクロスコンパイラです。

コンパイルする事で、Z80のアセンブラソースを出力するため、柔軟な活用が可能です。

現状、LSX-Dodgers及びS-OSで動作するように作られていますが、OS依存部分を個々作る事で、CPUにZ80を採用している様々な環境で動かす事が出来るはずです。

> **Note:** 旧コンパイラ(SLANGCompiler)のソースは `obsolete/` フォルダに移動しています。旧コンパイラを使用する場合は `obsolete/Makefile` を参照してください。

# 使い方

```
SLANG Compiler v0.23.0
Usage: slangc [options] <input.sl>

Options:
  -o <file>       Output file path
  -E <env>        Environment name (default: lsx)
  -I <path>       Add include search path (repeatable)
  -L <path>       Add library search path (repeatable)
  --dump-ast      Dump AST to stdout
  --dump-ir       Dump IR to stdout
  -h, --help      Show this help
  --version       Show version
```

コマンドラインにSLANGのソースファイル(拡張子は通常は .SL )を渡す事で、ソースファイルの拡張子を .ASM にしたアセンブラソースが出力されます(-o オプションにファイル名を渡すと、そちらに出力されます)。

アセンブラソースは、Z80アセンブラ [AILZ80ASM](https://github.com/AILight/AILZ80ASM) でアセンブル出来るものが出力されますので、適宜ご利用ください。

-E オプションに続けて、環境名を設定する事で、各種環境用のORG値が設定され、対応するライブラリが読み込まれます。現在、環境は「lsx」「sos」「x1」「msx2」などがあり、デフォルト環境名は「lsx」になります。

-I オプションに続けてインクルードフォルダを指定する事で、SLANGソース内で読み込みを行うSLANGソースファイルのフォルダを追加出来ます。

-L オプションに続けてライブラリフォルダを指定する事で、ランタイムライブラリ(.asm)のあるフォルダを追加出来ます。

## 旧コンパイラとの主な違い

- コマンド名が `SLANGCompiler` から `slangc` に変更
- 出力オプションが `-O` から `-o` に変更
- ランタイムライブラリが `.yml` 形式から `.asm` 形式に変更（`runtime/` フォルダ）
- `#MODULE` 指定時のモジュール分割ASMが直接出力されるため、ModuleSplitterは不要
- 関数名・変数名は常にソース名ベースのラベルで出力（`--use-symbol` オプションは廃止）
- プリプロセッサ定数 `ENV_TYPE` / `OS_TYPE` が `#IF` 条件式で参照可能
- `--case-sensitive`、`--source-comment`、`--output-debug-symbol` は未実装

# 環境について

SLANG Compilerは-Eオプションで環境を指定する事で、様々な環境に向けたコンパイルが可能です。現在、指定出来る環境には下記があります。

## lsx ( LSX-Dodgers / MSX(2) / CP/M / 他 )

SLANGコンパイラの標準環境です。-Eオプションを指定しないとこの環境が選ばれます。

X1/turbo/ZやMZ-700/1500やPC-8801mkIISRでCP/M80やMSX-DOSのソフトを実行するためのOS [LSX-Dodgers](https://github.com/tablacus/LSX-Dodgers) 用になります。

**LSX-Dodgers 1.62c が必要です。** ランタイムライブラリがLSX-Dodgers 1.62cの内部アドレスに依存しています（詳細は「LSX-Dodgersバージョン依存について」を参照）。

LSX-Dodgersにて安定して動作する環境です。一部を除き、MSX(2)、CP/M環境などでも動作すると思われます。

ファイルオープンの際のMSX-DOS2の処理を省略しています。

WIDTH関数は動作しません。

## x1 ( SHARP X1 )

lsx環境をベースとし、テキスト表示関連についてX1専用にカスタマイズした環境です。

**LSX-Dodgers 1.62c が必要です。** lsx環境と同様、ランタイムがLSX-Dodgers 1.62cに依存しています。

WIDTH関数が正常に動作し、文字表示について高速化されます。

ただし、LSX-Dodgers側との整合性は取っていないので、入力関連や、OSに戻ってからの挙動については保証しません。

また、PCG定義関数が追加されます。

## sos ( S-OS )

機種非依存のS-OS環境です。S-OSの標準BIOSコールのみを使用し、特定の機種（X1等）のハードウェアには依存しません。

S-OSを搭載した各種機種（X1、MZ-2500、PC-8801、FM-7等）で動作することを想定しています。

X1専用のグラフィックス（libx1_grp、libx1_sgl）、PSG、MAGIC、PCG、CTC割り込みパッチなどは含まれません。それらが必要な場合は `sosx1` 環境を使用してください。

## sosx1 ( S-OS for SHARP X1 )

従来の `sos` 環境相当。S-OSとX1固有のライブラリ（グラフィックス、PSG、MAGIC、PCG等）を同梱し、SLANGINIT時にX1 CTCの検出やX1turbo向け割り込みベクタパッチも行います。

X1/X1turbo上のS-OSで動作させる場合はこちらを選択してください。なお、他機種のS-OSでは動作しません。

## msx2 ( MSX / MSX2 )

LSX-Dodgers環境をベースとし、ファイル関連のみMSX-DOS2に対応させた環境です。

lsx環境でもMSX-DOS2であればファイル入出力ライブラリが使えますが、FCBを用いているためにMSX-DOS2ではカレントディレクトリにしか対応していないので、サブディレクトリを使う場合はこちらを使用してください。

## msxrom ( MSX )

MSXのROMカートリッジ用の環境です。

現状では32kbのROM用となっており、ORGは$4000で、$8000からの16kbは、$4000からの16kbと同じスロットに設定されます。

RAM(WORK)は$C000からになります。

※もちろんROM領域は書き込みが出来ないため、初期値を持った変数については現状ROM領域に置かれてしまうため、書き換えが出来ません。初期値についてはCONST側に記述し、変数については全て初期値無しで使う事をオススメします。

## pc80mk2 (PC-8001mkII)

PC-8001mkII用の環境です。

基本的に前半の$0000〜$7FFFはROMの想定で動作し、PRINT文などはBIOS部を利用して動作します。

ただし、BIOS部を使っている関数を使わない場合は、動的に該当部分をRAMに切り替える事で64KBの空間を自由に使う事が出来ます。

現状、PRINT文は動きますが、INPUTや、キー入力関連については未実装となります。

## pc80mk2x (PC-8001mkIIの全メモリRAM版)

PC-8001mkII用の環境ですが、「pc80mk2」環境と異なり、全てのメモリ領域がRAMになります。

その関係で、本来はROM部にある文字表示処理などのBIOS機能をRAM側に持ってきています。

BIOS処理については比較的汎用的に作られており、PC-8001版のOS「S-OS」をカスタマイズしたものです。

そのため、pc80mk2環境では(手抜きのため)未実装のキー入力関連の処理なども問題なく動作します。

## pc88mk2sr (PC-8801mkIISR)

PC-8801mkIISR用の環境です。

ORGは$1A00となっており、ディスクベーシックの環境上で動作する想定となっています。

PRINT文はテキストVRAMへの直接書き込みで実現しています。また、VSYNCはV-BLANK割り込みを利用して実装されています。

現状、基本的な文字表示とディスクアクセス機能が実装されていますが、キー入力関連については未実装となります。

## vgs0 (VGS-Zero)

VGS-Zero用の環境です。

## zxn (ZX Spectrum Next)

ZX Spectrum Next用の環境です。

ORGは$8000、ワーク領域は$D000からとなっています。

Layer 2グラフィックス、タイルマップ、スプライト、パレット設定、Copperプロセッサなど、ZX Spectrum Next固有の機能に対応したライブラリが用意されています。

サンプルは examples/zxn フォルダにあります。

# ランタイムについて

SLANG Compilerはランタイムライブラリとして、`runtime/` フォルダ内の `.asm` ファイルを読み込みます。

通常は -E オプションでの環境指定により、適切なライブラリが読み込まれます。

以下、ランタイムライブラリについてまとめます。

## core.asm / runtime.asm (OS依存しないライブラリ)
全環境で読み込まれるライブラリです。

一般的なZ80の環境であれば実行出来るライブラリコードが含まれるファイルです。

例えば掛け算や割り算など、OSなどの環境に関わらないルーチンはこちらに含まれています。

## LSX-Dodgers関連ライブラリ
* liblsx_base.asm — LSX-Dodgers用の標準的な処理
* liblsx_print.asm — 文字表示関連処理
* liblsx_input.asm — 入力関連処理
* liblsx_file.asm — ファイル入出力関連処理

## S-OS関連ライブラリ
* libsos_base.asm — 機種非依存S-OS用の標準的な処理
* libsosx1_base.asm — X1（およびX1turbo）用S-OSの標準処理（CTC検出・割り込みベクタパッチ付き）
* libsos_print.asm — 文字表示関連処理
* libsos_input.asm — 入力関連処理
* libsos_file.asm — ファイル入出力関連処理
* libsos_pcg.asm — PCG関連処理（X1依存のため `sosx1` 環境でのみ利用可）

## X1関連ライブラリ
* libx1_base.asm — X1固有のライブラリ（VSYNC_CHECK、VSYNC、VSYNC1）
* libx1_pcg.asm — PCG関連処理
* libx1_psg.asm — PSG音楽/効果音再生ライブラリ
* libx1_magic.asm — グラフィックパッケージMAGIC
* libx1_grp.asm — X1専用グラフィックライブラリ（マウスライブラリ含む）
* libx1_sgl.asm — X1 SGLライブラリ

## 汎用ライブラリ
* libfloat.asm — 24bit浮動小数点演算
* libcompress.asm — 圧縮データ解凍（lze、LZEe、LZEee f5、ZX0対応）
* libsoroban.asm — 実数演算ライブラリSOROBAN

## MSX関連ライブラリ
* libmsxlsx_base.asm — MSX + LSX-Dodgers用
* libmsxrom_base.asm — MSX ROMカートリッジ用
* libmsxrom_print.asm — MSX ROM環境用文字表示
* libmsxrom_input.asm — MSX ROM環境用入力
* libmsx2_file.asm — MSX-DOS2用ファイル入出力
* libmsx_grp.asm — MSXグラフィック
* libmsx_psg.asm — MSX PSG音楽/効果音
* libmsx_spdrv.asm — MSXスプライトドライバ
* libmsx_iot.asm — MSX IoT

## PC-8001mkII関連ライブラリ
* libpc80mk2_base.asm — PC-8001mkII固有のライブラリ
* libpc80mk2_print.asm — BIOS部を使ったPRINT関連処理
* libpc80mk2_sound.asm — サウンド関連
* libpc80mk2xbios_base.asm — 全RAM版XBIOS

## PC-8801mkIISR関連ライブラリ
* libp88_base.asm — PC-8801mkIISR固有のライブラリ
* libp88_print.asm — テキストVRAM直接書き込みPRINT処理
* libp88_file.asm — ディスクアクセス関連

## ZX Spectrum Next関連ライブラリ
* libzxn_base.asm — ZX Spectrum Next固有のライブラリ
* libzxn_print.asm — 文字表示関連処理
* libzxn_input.asm — 入力関連処理
* libzxn_file.asm — esxDOSファイルアクセス
* libzxn_nextdaw.asm — NextDAW音楽再生ドライバ

## VGS-Zero関連ライブラリ
* libvgs0_base.asm — VGS-Zero固有のライブラリ
* libvgs0_print.asm — VGS-Zero文字表示処理

## X1におけるゲームループの処理

turboではないX1は、一定間隔でゲームなどの処理ループを回すのが大変面倒になっています。これはX1に一定時間おきに発生する割り込みが存在せず、全ての時間管理を自力で行う必要があるためです。

本コンパイラのライブラリには、処理ループを一定に保つための関数がいくつか用意されています。

* VSYNC(num) — numフレーム待ちます
* VSYNC_CHECK() — 1/62秒より短い間隔で呼び続ける事でVBLANK検出を行います
* VSYNC1() — 単純に1フレーム待つ関数です

VSYNC(num)及びVSYNC_CHECK()関数内でVBLANK期間に入った場合、自動的にSLANGで定義した関数「VSYNC_PROC()」が呼ばれます(各自定義してください)。

## X1 グラフィックライブラリ (include/*.LIB)

ランタイム ASM とは別に、X1 用のグラフィック層 SLANG ライブラリを `include/` に用意しています。`#include NAME.LIB` で取り込みます。

| ライブラリ | 役割 | サンプル + リファレンス |
|---|---|---|
| `TILELIB.LIB` | PCG + テキスト VRAM を使った**背景タイルマップ**レイヤ | [examples/tile/README.md](examples/tile/README.md) |
| `SPRLIB.LIB` | グラフィック VRAM にダブルバッファで描く**前景スプライト**レイヤ (最大 8 枚 / 16x16) | [examples/spr/README.md](examples/spr/README.md) |
| `CHIPLIB.LIB` | マップ + チップスプライト + マスク + アニメを GVRAM 一括管理する**全部入り**総合描画 | [examples/chip/README.md](examples/chip/README.md) |
| `TILESPR.LIB` | TILELIB + SPRLIB 併用時のページ同期ヘルパー (統合 shim) | [examples/tilespr/](examples/tilespr/) |
| `UILIB.LIB` | GVRAM に文字・塗り・枠を描く**静的 HUD 専用**レイヤ (両ページ同時書き込み, 256 glyph フォント内蔵) | [examples/ui/README.md](examples/ui/README.md) |

各 README が API リファレンス兼チュートリアルを兼ねています。データ
フォーマット、引数の単位 (4 ドット / キャラクタ / スーパーチップ)、初期化
順序、VVRAM 上書き手順などはそちらを参照してください。

UILIB は SPRLIB と組み合わせる場合、UI 領域にスプライトを侵入させない
制約があります (画面端に HUD を寄せる運用)。詳細は UILIB の README を
参照してください。

## 環境ファイル及びランタイムのパスについて

各環境ファイル(*.env)は `runtime/env/` フォルダに、ランタイムライブラリ(*.asm)は `runtime/` フォルダに配置されています。

検索順序:
1. ソースファイルのあるディレクトリ
2. -I / -L で指定されたパス
3. $SLANG_HOME/{include,lib,runtime}
4. ~/.config/SLANG/
5. <compiler_dir>/../share/slang/

# 環境構築とSLANGプログラムのビルド方法

SLANG Compilerは、Windows/macOS/Linuxでクロスプラットフォームにインストール・ビルドできます。

## 配布 zip からのインストール

[GitHub Releases](https://github.com/h-o-soft/SLANG-compiler/releases) から OS 別 zip をダウンロード→解凍した dir で:

```
./install.sh                              # Linux / macOS (ユーザーローカル、sudo 不要)
install.bat                               # Windows
sudo ./install.sh --prefix /usr/local --config-dir /usr/local/share/slang --force
                                          # システムワイド (sudo 必須、CONFIG_DIR
                                          # も必ず明示しないと sudo の HOME=/root 問題)
./install.sh --uninstall                  # アンインストール
./install.sh --dry-run                    # 何が起こるか事前確認
./install.sh --help                       # 全オプション
```

default では Linux/macOS は `~/.local/bin` (binary) + `~/.config/SLANG/` (lib)、Windows は `%LOCALAPPDATA%\Programs\SLANG\` (binary) + `%USERPROFILE%\.config\SLANG\` (lib) に配置されます。

外部ツール (ndc / HuDisk / AILZ80ASM) はライセンス都合で配布 zip に同梱しないため、`make setup-tools` で別途ダウンロードします (= `~/.config/SLANG/tools/` 配下に配置)。

互換: `make install` / `make uninstall` も従来通り動作 (内部で install.sh / install.bat を呼ぶ wrapper)。**Make 経由は `--force` 既定 ON のため uninstall 時の確認 prompt は出ません**。

## ソースからビルドする場合

### 前提条件

- **.NET 8 SDK** (必須) — slangc / slangbuild のビルドに必要
- **Python 3.x** (一部の機能で必要)
  - PCG / フォント変換ツール (`tools/png_to_asm.py`, `tools/charmap-encode.py`)
  - これらを使わない通常のコンパイル / 単一バイナリ実行には不要
- **mono** (Linux / macOS で `make setup-tools` を実行する場合に必須)
  - `setupenv.sh` が S-OS template 生成のため `HuDisk.exe` を mono 経由で実行する。Windows は .NET Framework で直接実行されるため不要
  - sos / sosx1 環境で `slangbuild --emit disk` を使う場合も同じく mono が必要 (HuDisk.exe 起動)。lsx / x1 の ndc 経路だけ使うなら mono 不要

### ビルドとインストール

```
make compile              # コンパイルのみ（SLANGTEST.SL）
make asm                  # コンパイル＋アセンブル
make run                  # コンパイル＋アセンブル＋エミュレータ実行
```

別のソースファイルをビルドする場合:

```
make TARGET=examples/STARS ENV=lsx compile
make TARGET=examples/STARS ENV=x1 run
```

#### 利用可能な環境(ENV)

| ENV | 対象環境 |
|-----|---------|
| lsx | LSX-Dodgers (標準) |
| x1 | SHARP X1 (LSX-Dodgers + X1専用最適化) |
| sos | S-OS（機種非依存） |
| sosx1 | S-OS for SHARP X1（X1固有ライブラリ同梱・従来のsos互換） |
| msx2 | MSX-DOS2 |
| msxrom | MSX ROMカートリッジ |
| msxlsx | MSX + LSX-Dodgers |
| pc80mk2 | PC-8001mkII |
| pc80mk2x | PC-8001mkII (全RAM版) |
| pc88mk2sr | PC-8801mkIISR |
| vgs0 | VGS-Zero |
| zxn | ZX Spectrum Next |
| cpm | CP/Mエミュレータ (RunCPM 同梱) |

### エミュレータの設定

`Makefile` 内の `EMU` 変数を環境に合わせて編集してください。
ただし `ENV=cpm` および `ENV=lsx` は `tools/runcpm/` 以下に同梱された
RunCPM (MIT) を自動的に使うため、特別な設定は不要です。

### ディスクイメージの準備

`slangbuild --emit disk` (Makefile.dist の `disk_image` ターゲット経由) は
**pristine な template ディスクイメージ** を `images/templates/` から読み、
**出力先** `images/<env>PROG.d88` 等にコピーしてから main + overlay を書き込みます
(template 自体は不変、`Makefile.dist` の `DISK_IMAGE` 変数で出力先を上書き可能)。

template の入手:

| 環境 | 出力先 (`DISK_IMAGE`) | template (`disk.template`) | 入手方法 |
|------|----------------------|---------------------------|---------|
| lsx / x1 | `images/LSXPROG.d88` | `images/templates/LSXPROG.D88` | repo 同梱 |
| sos / sosx1 | `images/SOSPROG.D88` | `images/templates/SOSPROG.D88` | `make setup-tools` で取得 (S-OS 配布物 + AUTOEXEC.BAT 注入) |
| MSX-DOS 系 | `images/dosformsx.dsk` | (従来経路を維持、今後 `--emit disk` 経路へ移行予定) | `make setup-tools` で取得 |

# LSX-Dodgersバージョン依存について

lsx環境およびx1環境のランタイムは **LSX-Dodgers 1.62c** の内部アドレスに依存しています。他のバージョンでは正常に動作しない可能性があります。

依存箇所:

| ファイル | 内容 | 依存アドレス |
|----------|------|-------------|
| `runtime/liblsx_base.asm` | CTCベクタ初期化 | `$EEC0`（CTC0ベクタアドレス） |
| `runtime/liblsx_base.asm` | INKEY(0)リアルタイムキー入力 | `$EE92`（キーデータ）。初回呼び出し時にバージョン判定し、1.62c以外では無効化 |
| `runtime/libx1_print.asm` | テキストカーソルVRAMアドレス | `$EE8E`（`_TXADR`）。BDOS入力とVRAM直接出力のカーソル位置を同期 |
| `runtime/libx1_print.asm` | WIDTH 40/80切替時のワーク同期 | `$EEB1`（`_WIDTH`）、`$EEBC`（`_WIDTH_MINUS`）、`$EEBA`（`_PAGE_MINUS`） |
| `runtime/libx1_psg.asm` | PSG再生ライブラリ | LSX-Dodgers内部のCTCアドレス |

LSX-Dodgers 1.62c は [LSX-Dodgersのリリースページ](https://github.com/tablacus/LSX-Dodgers) から入手できます。

# 更新履歴

[CHANGELOG.md](CHANGELOG.md) を参照してください。

# ライセンス

MIT License

Copyright (c) 2022-2026 H.O SOFT / OGINO Hiroshi and contributors

詳細は [LICENSE](LICENSE) ファイルを参照してください。利用しているサードパーティライブラリのライセンスについても同ファイルに記載されています。
