# SLANG-compiler
SLANG Compiler (Z80) 0.24.1

# 概要

これは主に国産8bit PCで使われたOS「S-OS」オリジナルの構造型コンパイラ言語「SLANG」のクロスコンパイラです。

コンパイルする事で、Z80のアセンブラソースを出力するため、柔軟な活用が可能です。

現状、LSX-Dodgers及びS-OSで動作するように作られていますが、OS依存部分を個々作る事で、CPUにZ80を採用している様々な環境で動かす事が出来るはずです。

> **Note:** 旧コンパイラ(SLANGCompiler)のソースは `obsolete/` フォルダに移動しています。旧コンパイラを使用する場合は `obsolete/Makefile` を参照してください。

# 使い方

```
SLANG Compiler v0.24.1
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

## sosmz2500 ( S-OS for SHARP MZ-2500 )

S-OSをMZ-2500上で動作させるための環境です。`sos` 環境をベースに標準の入出力・ファイル・SOROBAN ライブラリを使用します。MAGIC 系ライブラリは含まれません。

D88 イメージ作成は X1 用 SOSPROG.D88 (= sos / sosx1 と同じ template) を流用します。MZ-2500 実機 (またはエミュレータ) では、このディスクを B ドライブに挿入し、別途 X1 用の SOSPROG ディスクから呼び出して実行する運用を想定しています。

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

`slangbuild` (および `Makefile.dist build / run / disk_image ENV=pc80mk2x`) は、メイン .cmt の直後に **XBIOS.CMT (= 0000H 配置の bootstrap binary)** を自動的に結合します (= 旧 `COPY /B PROG.CMT+XBIOS.CMT GAME.CMT` 手動結合の内製化)。`#MODULE` を使った overlay も同じ .cmt に結合されます。

SD カード経由でロードする場合は `pc80mk2xsd` 環境を選択してください。`slangbuild` が main `.cmt` + 各 overlay (`M0.BIN`, `M1.BIN`, ...) + `XBIOS.CMT` を出力ディレクトリに**個別配置**するので、出力ディレクトリ全体を SD カードに置けば動作します。`PC8001_SD` 定数は env により自動的に定義されるため、SL コードで `CONST ASM PC8001_SD = 1;` を書く必要はありません。

## pc88mk2sr (PC-8801mkIISR)

PC-8801mkIISR用の環境です。

ORGは$1A00となっており、ディスクベーシックの環境上で動作する想定となっています。

PRINT文はテキストVRAMへの直接書き込みで実現しています。また、VSYNCはV-BLANK割り込みを利用して実装されています。

現状、基本的な文字表示とディスクアクセス機能が実装されていますが、キー入力関連については未実装となります。

## mz25iocs ( SHARP MZ-2500 BASIC / IOCS )

MZ-2500 の BASIC システム / IOCS 上で動作させるための最小環境です。

PRINT・INKEY のみ IOCS (`RST 18H`) 経由で実装しており、その他の入力系 (LINPUT / GETL / GETLIN / INPUT) は呼ばれた場合 ESC キャンセル相当の値を返す stub 実装になっています。グラフィック関連も未実装です。

D88 イメージ作成には外部ツール `mzd88` (issaUt/mz2500-tools の C 実装、MIT、`tools/mzd88-{rid}` として 4 platform binary を同梱) を使用し、`mzd88 -blank` で空 D88 を都度生成して main bin (`PROG.OBJ`) と起動用 BASIC ローダ (`runtime/mz2500/J8000.bas.bsd`) を格納します。BASIC ローダは `&H8000` にバイナリをロードして `CALL` する最小のコードで、`mz25iocs.env` の `default_org: "$8000"` と整合しています。

詳細は [docs/MZ2500.md](docs/MZ2500.md) を参照してください。

## vgs0 (VGS-Zero)

VGS-Zero用の環境です。

`slangbuild` (および `Makefile.dist build / run / disk_image ENV=vgs0`) は env file (`bin_pad_size: 16384` + `overlay_pad_align: 8192`) に従って、main を 16KB 固定 ROM、各 overlay (`#MODULE`) を 8KB の倍数に切り上げて末尾を 0 で埋めます。VGS-Zero は 8KB 単位の bank switching を持つため、overlay も 8KB 単位に揃える必要があります。bank 切替は `runtime/libvgs0_base.asm` 既存の helper から呼べます。

## zxn (ZX Spectrum Next)

ZX Spectrum Next用の環境です。

ORGは$8000、ワーク領域は$D000からとなっています。

Layer 2グラフィックス、タイルマップ、スプライト、パレット設定、Copperプロセッサなど、ZX Spectrum Next固有の機能に対応したライブラリが用意されています。

サンプルは examples/zxn フォルダにあります。`game.sl` (NextDAW を使う完全版) と `game_nomusic.sl` (NextDAW なし版) の 2 種類が含まれます。

`slangbuild` (および `Makefile.dist build TARGET=examples/zxn/game ENV=zxn`) で `.bin` を出力できます。`.nex` 形式 (= CSpect 等の実行可能形式) への変換は外部ツール `nexcreator` を使い、`examples/zxn/Makefile` で flow が完結します。

> **注**: `examples/zxn/Makefile` の `make build` (default) は NextDAW なし版 (`game_nomusic.nex`) を build します。NextDAW を含む完全版 (`game.nex`) を build するには `make music` を使い、`examples/zxn/NextDAW_RuntimePlayer_E000.bin` (= NextDAW Runtime Player) を `examples/zxn/` 配下に配置してください。NextDAW ([https://nextdaw.biasillo.com/](https://nextdaw.biasillo.com/)) は外部製品のため配布物には含まれません。**2026-04-30 時点で公式サイトでの入手はできない状態**で、再公開された場合も driver の仕様変更等により `examples/zxn/game.cfg` や `game.sl` の修正が必要となる可能性があります。

## c64 (Commodore 64 / oscar64) — experimental

Commodore 64 (6502) 用の環境です。SLANG コンパイラは 6502 アセンブラを直接出力する代わりに、**SLANG → C ソース → oscar64 → .prg** という二段変換で 6502 バイナリを生成します。

> **謝辞**: 本 backend は drmortalwombat 氏による 6502 C コンパイラ **oscar64** ([https://github.com/drmortalwombat/oscar64](https://github.com/drmortalwombat/oscar64)) の最適化に依存しています。高品質な C → 6502 コンパイラを公開してくださっている oscar64 プロジェクトに感謝します。oscar64 は本配布物には含まれないため、別途インストールが必要です。

### 事前準備

oscar64 をインストールし、以下のいずれかの方法で発見可能な状態にしてください:

- PATH を通す (例: `/usr/local/bin/oscar64` 等、`which oscar64` で見える状態)
- 環境変数 `$OSCAR64` に絶対パスを設定
- env file (`runtime/env/c64.env`) の `oscar_path:` に絶対パスを記述
- `slangbuild --oscar-path <path>` で明示指定 (CLI override)

解決順は上記の逆 (`--oscar-path` → `oscar_path:` → `$OSCAR64` → PATH) で、見つかった時点で確定します。

### 基本 build

`slangc -E c64` は `.c` ファイルを出力し、`slangbuild -E c64` は内部で `slangc` → `oscar64` を順に呼んで `.prg` を生成します。

```sh
# .c のみ生成 (oscar64 invoke なし)
slangc -E c64 -o examples/FMANDEL.c examples/FMANDEL.SL

# 一発で .prg まで生成
slangbuild -E c64 -o examples/FMANDEL examples/FMANDEL.SL
# → examples/FMANDEL.prg (+ oscar64 副産物 .asm/.map/.int/.lbl)
```

**`-o` のセマンティクス**: `slangc -o <path>` は完全パス (`.c` 拡張子込み)、`slangbuild -o <prefix>` は prefix (`<prefix>.c` と `<prefix>.prg` を生成) という Z80 経路と同じ慣行です。

### 提供 API

env file `c64.env` が以下を C backend builtin として公開しているため、SLANG コードからそのまま呼べます (CFUNC 宣言不要、`#IF BACKEND==1` の gate も不要 = env 提供 API は自動的に有効):

| 種別 | API |
|---|---|
| I/O | `PRINT` 全構文 (`"..."` / `/` / `%(v)` / `!(s)` / `HEX2$(v)` / `HEX4$(v)` / `DECI$(v)` / `FORM$(v,n)` / `MSG$(p)` / `MSX$(p)` / `STR$(c,n)` / `CHR$(n)` / `SPC$(n)` / `CR$(n)` / `TAB$(n)` / `FL$(f)` / `PN$(v)`) |
| 入力 | `INKEY(mode)` (即時状態取得、押下中=値、離した瞬間=0)、`INPUT` 系 |
| 端末 | `WIDTH(w)` (no-op = C64 は 40 桁固定)、`LOCATE(x, y)`、`SCREEN(x, y)`、`PRMODE(m)` |
| 数学 | `ABS / SQR / SIN / COS / TAN / LOG / EXP / ATN / RND / SRND` |
| メモリ | `MEM[addr]` / `MEMW[addr]` (= 絶対アドレス access、`SLANG_MEM` / `SLANG_MEMW` マクロに展開) |
| ビット | `BIT(v, b)` / `SET(p, b)` / `RESET(p, b)` |
| 文字列長 | `STRLEN(s)` |
| **VIC sprite** | `SPR_INIT(screen_addr)` / `SPR_SET(sp, show, x, y, image, color, multi, xex, yex)` / `SPR_MOVE(sp, x, y)` / `SPR_SHOW(sp, show)` / `SPR_POSX(sp)` / `SPR_POSY(sp)` / `SPR_COLOR(sp, c)` / `SPR_IMAGE(sp, image)` |
| **VIC 同期** | `VIC_WAIT()` (= 1 フレーム VSYNC 待ち、tearing 防止) |

VIC 色定数 (`VCOL_BLACK..VCOL_LT_GREY`、16 色) は `#INCLUDE "C64_VIC.LIB"` で取り込めます。

サンプル: `examples/c64/SPRITE.SL` (sprite 1 個を VSYNC 同期で画面端バウンス):

```sh
slangbuild -E c64 examples/c64/SPRITE.SL -o examples/c64/SPRITE
x64sc -autostart examples/c64/SPRITE.prg   # VICE
```

### CFUNC 宣言 (自前 C 関数を呼ぶ)

env が提供しない C 関数を SLANG から直接呼ぶには `CFUNC` 宣言を書きます。実体は `--c-source` で渡したユーザー C ファイルに置きます。

```slang
// myapp.SL
CFUNC HELLO() VOID :hello_func;                       // 型あり: 引数なし、戻り値 void
CFUNC PEEK(WORD addr) BYTE :peek;                     // 型あり: WORD → BYTE
CFUNC POKE(WORD addr, BYTE val) VOID :poke;           // 複数引数
CFUNC ADD(2):my_add;                                  // 略式: 引数 2 個 (= WORD)、戻り値 WORD 仮定

MAIN()
{
    HELLO();
    POKE($D020, PEEK($D020) + 1);   // 枠色 +1
    PRINT(ADD(3, 4));
}
```

ユーザー C ファイル:

```c
// mylib.c
#include <stdio.h>
void hello_func(void) { printf("HELLO!"); }
unsigned char peek(unsigned int addr) { return *(volatile unsigned char *)addr; }
void poke(unsigned int addr, unsigned char val) { *(volatile unsigned char *)addr = val; }
unsigned int my_add(unsigned int a, unsigned int b) { return a + b; }
```

build:

```sh
slangbuild -E c64 myapp.SL --c-source mylib.c -o myapp
```

`--c-source` は repeatable で複数 .c を渡せます。`#INCLUDE` のように SLANG プログラムにインライン埋め込みする syntax (`#CCODE`) は今後の検討項目です。

**CFUNC 文法**:

- 略式 (= MACHINE と同じ書き味): `CFUNC NAME(N):c_name;` — 引数 N 個すべて WORD、戻り値 WORD 仮定。簡易 interop 用。
- 型あり (= 推奨): `CFUNC NAME(BYTE x, WORD y, ...) RET :c_name;` — `RET` は `BYTE` / `WORD` / `FLOAT` / `VOID` (省略は WORD)。配列ポインタ引数は `BYTE buf[]` 形式。
- `c_name` は C 識別子規則 (`^[A-Za-z_][A-Za-z0-9_]*$`)、case preserve。
- セミコロン必須。複数宣言はカンマ区切り (`CFUNC A(1):a, B(WORD x) BYTE :b;`)。

### v1 スコープと制約

**動作確認済**: PRINT / INPUT / 整数算術 / FLOAT 演算 / リアルタイム key 入力 / sprite (1 個アニメ、VSYNC 同期) + ユーザー C 任意関数 (= CFUNC + `--c-source`)。`examples/FMANDEL.SL` / `examples/FURUI.SL` / `examples/STARS.SL` / `examples/c64/SPRITE.SL` が実機 (VICE) で動作。

**未対応 / 今後の拡張**: sprite multiplex / VIC bitmap mode / SID sound / KERNAL file I/O / CRT / overlay (`#MODULE`)。これらは bridge 関数を追加する形で順次対応予定。

**Z80 固有機能**: `MACHINE` 宣言 / inline `#ASM` ブロック / `PORT IN/OUT` / `#MODULE` を含む SLANG コードは C backend では診断 error。`#IF BACKEND==1` (= OscarC) または `#IF ENV_TYPE==7` (= c64) で C backend 専用コードを gate できます (`BACKEND` は env で自動定義: 0=Z80、1=OscarC)。

**文字列・PETSCII**: SLANG の文字列リテラルは oscar64 の `-psci` オプション (env file default で有効) により PETSCII エンコーディングで出力されます。**ASCII printable (0x20-0x7E) のみサポート**し、日本語・カナ・SJIS は未対応です (高位バイトは `\xNN` で出るが画面表示は崩れる)。

**FLOAT 精度**: SLANG FLOAT (24-bit f24) は oscar64 の `float` (32-bit IEEE 754) にマップされるため、Z80 backend と完全に同一の結果ではなくほぼ等価な精度になります。整数→FLOAT 変換は Z80 backend の `i16tof24` と同じ signed 解釈 (`(float)(short)(...)` 経由)。

**runtime 構成**: `runtime/c64/slang_runtime.{h,c}` (I/O + 数学 + ビット) + `runtime/c64/slang_sprite.{h,c}` (VIC sprite bridge + VSYNC)。slang_runtime.h は slang_sprite.h を chain include しているため、生成 C 側 extern と bridge 実装の signature drift が発生しません。

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
* libpc80mk2_sound.asm — サウンド関連 (PCG-8100 後期 / PCG-8200 / PCG-8800 系互換ボード = PSA3.0 等が搭載する 8253 PIT で 3 ch 矩形波同時発音、`SND_PLAY` / `SND_SEPLAY` で BGM + SE 同時再生に対応)。MML を ASM data に変換する `tools/mml2sound.py` と、サンプル MML `examples/pc80mk2/chouchou.mml` (+ 詳細は `examples/pc80mk2/README.md`) も同梱
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

default では Linux/macOS は `~/.local/bin` (binary) + `~/.config/SLANG/` (lib)、Windows は `%USERPROFILE%\.local\bin\` (binary) + `%USERPROFILE%\.config\SLANG\` (lib) に配置されます (= 両 OS で `~/.local/bin` 系に揃え、uv / pipx 等の CLI ツール慣習に整合)。

外部ツール (ndc / HuDisk / AILZ80ASM) はライセンス都合で配布 zip に同梱しないため、`make setup-tools` で別途ダウンロードします (= `~/.config/SLANG/tools/` 配下に配置)。

互換: `make install` / `make uninstall` も従来通り動作 (内部で install.sh / install.bat を呼ぶ wrapper)。**Make 経由は `--force` 既定 ON のため uninstall 時の確認 prompt は出ません**。

## ソースからビルドする場合

### 前提条件

- **.NET 8 SDK** (必須) — slangc / slangbuild のビルドに必要
- **Python 3.x** (一部の機能で必要)
  - PCG / フォント変換ツール (`tools/png_to_asm.py`, `tools/charmap-encode.py`)
  - これらを使わない通常のコンパイル / 単一バイナリ実行には不要
- **mono** (Linux / macOS で .NET assembly な disk ツール `HuDisk.exe` / `udostool.exe` を起動する場合に必須)
  - `setupenv.sh` が S-OS template 生成のため `HuDisk.exe` を mono 経由で実行する。Windows は .NET Framework で直接実行されるため不要
  - **sos / sosx1 / sosmz2500** 環境で `slangbuild --emit disk` を使う場合 (= HuDisk.exe 起動)
  - **pc88mk2sr** 環境で `slangbuild --emit disk` を使う場合 (= udostool.exe 起動、Bookworm's Library 由来の汎用ディスクルーチン用ツール)
  - lsx / x1 の ndc 経路だけ使うなら mono 不要
  - **Linux / WSL**: デフォルトの mono は日本語 (CP932) コードページを含まないため、HuDisk が `Encoding 932 data could not be found` で失敗します。Debian/Ubuntu では `sudo apt install libmono-i18n4.0-all` で別途インストールしてください。macOS の Homebrew mono にはデフォルトで含まれているため不要

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
| pc80mk2x | PC-8001mkII (XBIOS 直接環境) |
| pc80mk2xsd | PC-8001mkII (XBIOS 直接環境、SD カード経路) |
| pc88mk2sr | PC-8801mkIISR |
| sosmz2500 | S-OS for SHARP MZ-2500 (X1 SOSPROG 共用、HuDisk D88 作成) |
| mz25iocs | MZ-2500 BASIC / IOCS 直接環境 (mzd88 で D88 作成) |
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
| sos / sosx1 / sosmz2500 | `images/SOSPROG.D88` | `images/templates/SOSPROG.D88` | `make setup-tools` で取得 (S-OS 配布物 + AUTOEXEC.BAT 注入)。MZ-2500 では本ディスクを B ドライブに挿入し X1 用 SOSPROG から呼び出す運用 |
| pc88mk2sr | `images/PC88MK2SR.d88` | `images/templates/PC88MK2SR.D88` | repo 同梱 (Bookworm's Library 由来、`THIRD_PARTY_NOTICES.md` 参照) |
| mz25iocs | `<output dir>/M25PROG.d88` | (template 不要) | `mzd88 -blank` で都度生成、`runtime/mz2500/J8000.bas.bsd` を起動用 BASIC ローダとして格納 |
| MSX-DOS 系 | `images/dosformsx.dsk` | `tools/disk-add-overlays.py` 経路 | `make setup-tools` で取得 |

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

詳細は [LICENSE](LICENSE) ファイルを参照してください。

配布物に同梱される第三者由来の成果物 (= Bookworm's Library / XBIOS / LSXPROG 等) の出典・許諾情報は [THIRD_PARTY_NOTICES.md](THIRD_PARTY_NOTICES.md) に記載しています。RunCPM や UI フォントのように LICENSE 全文を別ファイルで同梱しているもの (= `tools/runcpm/LICENSE` / `assets/ui/LICENSE.font`) は各 LICENSE ファイルを参照してください。
