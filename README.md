# SLANG-compiler
SLANG Compiler (Z80) 0.10.0

# 概要

これは主に国産8bit PCで使われたOS「S-OS」オリジナルの構造型コンパイラ言語「SLANG」のクロスコンパイラです。

コンパイルする事で、Z80のアセンブラソースを出力するため、柔軟な活用が可能です。

現状、LSX-Dodgers及びS-OSで動作するように作られていますが、OS依存部分を個々作る事で、CPUにZ80を採用している様々な環境で動かす事が出来るはずです。

# 使い方

まだ作り途中なのであちこち怪しいです。

※環境構築と、簡単にビルドとエミュレータでの実行までを行える環境構築方法については [環境構築とSLANGプログラムのビルド方法](#環境構築とslangプログラムのビルド方法)を参照してください。

```
SLANGCompiler filename [-L library-name] [-O output-path]
SLANG Compiler 0.10.0
Copyright (c) 2022-2023 OGINO Hiroshi / H.O SOFT

  -E, --env               Environment name.
  -l, --lib               Library name(s). ( lib*.yml )
  -I, --include           Include path(s).
  -L, --library           Library path(s).
  -O, --output            Output file path.
  --use-symbol            Use original symbol name.
  --case-sensitive        Set symbols to be case-sensitive.
  --source-comment        Include source code as comments.
  --output-debug-symbol   Output original symbol name for debug.
  --help                  Display this help screen.
  --version               Display version information.
```

コマンドラインにSLANGのソースファイル(拡張子は通常は .SL )を渡す事で、ソースファイルの拡張子を .ASM にしたアセンブラソースが出力されます(-O オプションにファイル名を渡すと、そちらに出力されます)。

アセンブラソースは、Z80アセンブラ [AILZ80ASM](https://github.com/AILight/AILZ80ASM) でアセンブル出来るものが出力されますので、適宜ご利用ください。

-E オプションに続けて、環境名を設定する事で、各種環境用のORG値が設定され、対応するライブラリが読み込まれます。現在、環境は「lsx」「sos」「x1」「msx2」があり、デフォルト環境名は「lsx」になります。

-l オプションに続けて、ランタイムライブラリの名前を付与する事で、指定のライブラリを読み込む事が出来ます( -l x1 とする事で、 libx1.yml というライブラリを読み込みます)。ただし、こちらは-Eオプションでの環境名設定でおおむね行われますので、個別ライブラリを読み込ませたい時のみ指定するようにしてください。

-I オプションに続けてインクルードフォルダを指定する事で、SLANGソース内で読み込みを行うSLANGソースファイルのフォルダを追加出来ます。

-L オプションに続けてライブラリフォルダを指定する事で、ライブラリ定義ファイル(.yml)及び、ライブラリソースのあるライブラリフォルダを追加出来ます。

--use-symbol をつけると、変数名、関数名について、ソースコードで利用した名前をそのまま使ってアセンブラソースが出力されます。つけない場合は、「SYM(数字)」という名前に置き換えられますので、アセンブラがラベルとして識別出来ない変数名、関数名を使っている場合は、--use-symbolをつけないようにしてください。

--case-sensitiveをつけると、識別子について大文字小文字が区別されます。つけない場合は区別されません。

--source-commentをつけるとアセンブラソースにSLANGのソースがコメントとして追加されます。

--output-debug-symbolをつけるとソースコーソ内の変数名、関数名をデバッグ用に定義します( VAL という変数が _VAL_ としてシンボル定義されます)。

# 環境について

SLANG Compilerは-Eオプションで環境を指定する事で、様々な環境に向けたコンパイルが可能です。現在、指定出来る環境には下記があります。

## lsx ( LSX-Dodgers / MSX(2) / CP/M / 他 )

SLANGコンパイラの標準環境です。-Eオプションを指定しないとこの環境が選ばれます。

X1/turbo/ZやMZ-700/1500やPC-8801mkIISRでCP/M80やMSX-DOSのソフトを実行するためのOS [LSX-Dodgers](https://github.com/tablacus/LSX-Dodgers) 用になります。

LSX-Dodgersにて安定して動作する環境です。一部を除き、MSX(2)、CP/M環境などでも動作すると思われます。

ファイルオープンの際のMSX-DOS2の処理を省略しています。

WIDTH関数は動作しません。

## x1 ( SHARP X1 )

lsx環境をベースとし、テキスト表示関連についてX1専用にカスタマイズした環境です。

WIDTH関数が正常に動作し、文字表示について高速化されます。

ただし、LSX-Dodgers側との整合性は取っていないので、入力関連や、OSに戻ってからの挙動については保証しません。

また、PCG定義関数が追加されます。

## sos ( S-OS )

元々のSLANGの標準環境であるS-OS向けの環境です。

比較的安定して動作すると思われます。l

## MSX2 ( MSX / MSX2 )

LSX-Dodgers環境をベースとし、ファイル関連のみMSX-DOS2に対応させた環境です。

lsx環境でもMSX-DOS2であればファイル入出力ライブラリが使えますが、FCBを用いているためにMSX-DOS2ではカレントデディレクトリにしか対応していないので、サブディレクトリを使う場合はこちらを使用してください。

## msxrom ( MSX )

MSXのROMカートリッジ用の環境です。

現状では32kbのROM用となっており、ORGは$4000で、$8000からの16kbは、$4000からの16kbと同じスロットに設定されます。

RAM(WORK)は$C000からになります。

これらは、 msxrom.env 及び、 libmsxrom_base.yml のSLANGINIT: 内に記述されているので、書き換えを行えば、32kb ROM以外にも対応が可能となっています。

現状、MSXのROMで動かすためのお試し的な環境となっていますので、こちらを参考にカスタマイズしてみてください。

※もちろんROM領域は書き込みが出来ないため、初期値を持った変数については現状ROM領域に置かれてしまうため、書き換えが出来ません。初期値についてはCONST側に記述し、変数については全て初期値無しで使う事をオススメします。


## pc80mk2 (PC-8001mkII)

PC-8001mkII用の環境です。

基本的に前半の$0000～$7FFFはROMの想定で動作し、PRINT文などはBIOS部を利用して動作します。

ただし、BIOS部を使っている関数を使わない場合は、動的に該当部分をRAMに切り替える事で64KBの空間を自由に使う事が出来ます。

現状、PRINT文は動きますが、INPUTや、キー入力関連については未実装となります。

## pc80mk2x (PC-8001mkIIの全メモリRAM版)

PC-8001mkII用の環境ですが、「pc80mk2」環境と異なり、全てのメモリ領域がRAMになります。

その関係で、本来はROM部にある文字表示処理などのBIOS機能をRAM側に持ってきています。

BIOS処理については比較的汎用的に作られており、PC-8001版のOS「S-OS」をカスタマイズしたものです。

そのため、pc80mk2環境では(手抜きのため)未実装のキー入力関連の処理なども問題なく動作します。

内部的にXBIOS.CMTというBIOS部を多段ロードして動作させる仕組みになっています(ややこしいです)。

詳細は [XBIOSのドキュメント](lib/pc8001/XBIOS/README.md)を参照してください。

# ランタイムについて

SLANG Compilerはランタイムライブラリとして、複数のファイルを読み込む事が出来ます。

通常は -E オプションでの環境指定により、適切なライブラリが読み込まれますが、個別に独自ライブラリや、標準で読み込んでいないライブラリを読み込みたい場合は、-L オプション指定により読み込みが可能になっています

以下、ランタイムライブラリについてまとめます。

## runtime.yml (OS依存しないライブラリ)
全環境で読み込まれるライブラリです。

こちらは、一般的なZ80の環境であれば実行出来るライブラリコードが含まれるファイルです。

例えば掛け算や割り算など、OSなどの環境に関わらないルーチンはこちらに含まれています。普通のYAML形式のテキストファイルなので、
より高速なものに自分で差し替える事も可能です(というか、いいコードが出来たらプルリクください)。

## LSX-Dodgers関連ライブラリ
* liblsx_base.yml
  * LSX-Dodgers用の標準的な処理が含まれるライブラリ。
* liblsx_print.yml
  * LSX-Dodgers用の文字表示関連処理が含まれるライブラリ
* liblsx_input.yml
  * LSX-Dodgers用の入力関連処理が含まれるライブラリ
* liblsx_file.yml
  * LSX-Dodgers用のファイル入出力関連処理が含まれるライブラリ

## S-OS関連ライブラリ
* libsos_base.yml
  * S-OS用の標準的な処理が含まれるライブラリ。
* libsos_print.yml
  * S-OS用の文字表示関連処理が含まれるライブラリ
* libsos_input.yml
  * S-OS用の入力関連処理が含まれるライブラリ
* libsos_file.yml
  * S-OS用のファイル入出力関連処理が含まれるライブラリ
* libmag.yml
  * MAG形式の画像表示ライブラリ
  * 実体は extlib/MAG.ASMになります
* libm8a.yml
  * M8A形式の画像表示ライブラリ
  * 実体は extlib/m8a.asmになります

## 汎用ライブラリ
* libcompress.yml
  * 圧縮データの解凍ライブラリ。lze、LZEe、LZEee f5、ZX0に対応しています。
* libsoroban.yml
  * 実数演算ライブラリSOROBAN
  * SOROBAN.LIB経由で利用します

## X1関連ライブラリ
* libx1_base.yml
  * X1固有のライブラリ
  * 現状、VSYNC_CHECK()、VSYNC()、VSYNC1()関数のみです
  * 「X1におけるゲームループの処理」を参考にしてください
* libx1_pcg.yml
  * X1のPCG関連処理が含まれるライブラリ
* libmag.yml
  * MAG形式の画像表示ライブラリ
  * 実体は extlib/MAG.ASMになります
* libm8a.yml
  * M8A形式の画像表示ライブラリ
  * 実体は extlib/m8a.asmになります
* libx1_psg.yml
  * PSG音楽/効果音再生ライブラリ
  * [PSGSoundDriver for MSX](https://github.com/aburi6800/msx-PSGSoundDriver)のX1カスタマイズ版です。開発者のあぶり6800さん、ありがとうございます！
  * LSX-Dodgers / S-OS両対応になっています
* libx1_magic.yml
  * グラフィックパッケージMAGICのライブラリです
  * ^IX にMAGICのコマンドを保存したアドレスを代入後に CALLMAGIC() という関数を呼ぶ事で処理が行われます
* libx1_grp.yml
  * X1専用のグラフィックライブラリです(ついでにマウスライブラリも含まれます)
  * グラフィック関数一覧
    * GRPSETUP() <br/>WIDTH関数を呼んだあとに呼ぶ事でグラフィック処理を初期化
    * LINE(X1,Y1,X2,Y2,Color(0〜7)) <br/>ライン描画
    * PAINT(X,Y,中間色) <br/>ペイント
    * BFILL(X1,Y1,X2,Y2,中間色) <br/>矩形を中間色で塗る
  * 中間色は0〜7のデジタル8色を2つ組み合わせた値で「色1*16 + 色2」の値になります
  * 例えば青く塗りたい場合は「16 + 1」で、青と白の中間色の場合は「16 + 7」です
  * 0=黒、1=青、2=赤、3=紫、4=緑、5=水色、6=黄色、7=白
  * マウス関数一覧
    * MSINIT() <br/>WIDTH関数を呼んだあとに呼ぶ事でマウスを初期化
    * MSGET(アドレス) <br/>ARRAY MSDAT[3-1] などに MSGET(MSDAT) とする事で、MSDAT[0]にX座標、MSDAT[1]にY座標、MSDAT[2]の0ビット目にボタン1、1ビット目にボタン2の押下情報が入る

## MSX-DOS2関連ライブラリ
* libmsx2_file.yml
  * MSX-DOS2用のファイル入出力関連処理が含まれるライブラリ

## MSX ROM環境関連ライブラリ
* libmsxrom_print.yml
  * MSX ROM環境用の文字表示処理が含まれるライブラリ。検証甘め。
* libmsxrom_input.yml
  * MSX ROM環境用の入力関連処理が含まれるライブラリ。ただしINPUTやINKEYは現在なく、STICK()関数のみが入っています。
* libmsx_psg.yml
  * PSG音楽/効果音再生ライブラリ
  * あぶり6800さんの[PSGSoundDriver for MSX](https://github.com/aburi6800/msx-PSGSoundDriver)を、ほぼそのまま組み込んでいます

## PC-8001mkII関連ライブラリ
* libpc80mk2_base.yml
  * PC-8001mkII固有のライブラリ
  * MEMMODE()関数で$0000～$7FFFの領域の読み書きをROMに対して行うか、RAMに対して行うか指定出来ます
  * LOADCMT()でカセットからの読み込みを行います
  * その他SDカード読み書き用関数が用意されています(yanatakaさんの https://github.com/yanataka60/PC-8001mk2_SD こちらのハードウェアに対応しています)
* libpc80mk2_print.yml
  * PC-8001mkIIのBIOS部を使ったPRINT関連の処理を行うライブラリ

## X1におけるゲームループの処理

turboではないX1は、一定間隔でゲームなどの処理ループを回すのが大変面倒になっています。これはX1に一定時間おきに発生する割り込みが存在せず、全ての時間管理を自力で行う必要があるためです。

本コンパイラのライブラリには、処理ループを一定に保つための関数がいくつか用意されています。

* VSYNC(num)
  * numフレーム待ちます。例えば5を指定した場合、この関数に到達するまでに3フレーム経過していた場合、2フレーム待ちます。
  * 処理ループ内で一回呼ぶ事で、ここで指定したフレーム数でループを回す事が出来ます
* VSYNC_CHECK()
  * ゲームなどリアルタイムの処理を行いたい場合、必ず1/62秒より短い間隔でこの関数を呼び出し続けます。そうする事で、1/62秒おきに必ず特定の処理(PSGの演奏処理など)を呼び出す事が出来るため、例えばPSGの演奏のテンポが一定に保たれます(後述)
* VSYNC1()
  * 単純に1フレーム待つ関数です。ここまでで1フレーム以上経過していても強制的に1フレーム待ちます。

VSYNC(num)及びVSYNC_CHECK()関数内でVBLANK期間に入った場合、自動的にSLANGで定義した関数「VSYNC_PROC()」が呼ばれます(各自定義してください)。ですので、例えばその関数の中でPSG再生処理関数「PSG_PROC()」を呼び出す事で、割り込みを持たないX1において、PSGの演奏テンポを一定に保つ事が出来ます。

とはいえ、FM音源ボードを搭載していたり、turbo以上の場合は一定時間で割り込みをかける事が出来ますので、その場合は、PSG_INIT(1); と、0以外の値でPSGを初期化する事で、特に何もしなくてもPSGの再生テンポは保たれます(逆に PSG_INIT(0); とすると割り込みを使わなくなるため、割り込みを持つ機種であっても前述のVSYNC()、VSYNC_CHECK()、VSYNC_PROC()にて自力での再生テンポ維持を行う必要があります。非turboのX1(FM音源ボード等の搭載なし)もサポートする場合は、そのようにした方が良いでしょう)。

このあたりは、対象機種や制限(FM音源ボード必須、など)によって臨機応変にどうするかを決めて実装してください。

## 環境ファイル及びランタイムのパスについて
各環境ファイル(*.env)及び、runtime.yml と lib*.yml、ライブラリソース実体の含まれるextlibフォルダは、カレントパス、あるいはユーザーフォルダの .config/SLANG/ フォルダの下から読まれます。環境により、適宜配置してください。

## ランタイムの記述ルール(書き途中)

- param_countにパラメータの数を入れてください
  - パラメータは3個までの場合はレジスタ渡しされます(HL、DE、BCの順)。
  - 4個以上の場合は全てIYレジスタをポインタとして適宜渡されます(SLANGの仕様を確認してください)
- callsに、このルーチンが呼び出すランタイムの名称を記述してください
- codeに、コード本体を書いてください。インデントは変えないでください。
- ランタイムのソースを外部に追い出す場合は extlib: (ファイル名):(ライブラリ定義名) を定義してください
  - ファイル名で示されるライブラリソースファイルを extlib フォルダに入れてください
  - ライブラリソースファイルに #LIB (ライブラリ定義名) を書くと、そこから下が、該当のランタイム関数のコードになります
  - 終了は #ENDLIB となります(#LIB～#ENDLIBの外については無視されます)
  - 一つのソースに複数の関数の定義を埋め込む事が出来ます(MAG.ASM参照)
- AILZ80ASMのネームスペースを使い、ライブラリの名前空間と、SLANGソースの名前空間を分離したい場合は lib_name: (ネームスペース名) を定義してください
  - これを定義する事で、例えば「LOOP」であるとか「WORK」であるとか、他とカブりそうなラベルを自由に使えるようになります
  - 特に既存のソースを使う場合はネームスペースを指定し、名前空間を分離しておくと良いでしょう


# 環境構築とSLANGプログラムのビルド方法

SLANG Compilerについては、コンパイラの実行ファイルと関連ファイルを好きなところに置いてパスを通せばSLANGソースをアセンブラファイルにコンパイルする事が可能です。

しかし、コンパイル後、アセンブルして、エミュレーターのディスクイメージにファイルを格納し、エミュを起動して読み込む……といった、コンパイル後の手順が多いため、ビルドから実行までを省力化するためのWindows用のバッチファイル(ビルドバッチ)が提供されています。

ビルドバッチについては、下記手順にて環境を整えた上で実行される想定ですので、もしビルドバッチを使いたい場合は、手順どおりに環境を構築してから利用してください

## 環境構築の下準備

ビルドバッチを動かすために必要な作業をおおまかに説明すると、ビルドバッチ「slbuild.bat」が置かれるビルド用フォルダを起点として、「bin」フォルダにコンパイラを配置、「tools」フォルダにツール類を配置(アセンブラなど)、「images」フォルダにエミュレータのディスクイメージを配置する、という事になります。

それぞれ手順を解説します。

### コンパイル環境の構築

リポジトリの[リリースのページ](https://github.com/h-o-soft/SLANG-compiler/releases)、または自前でビルドいただいたバイナリファイル群を、「bin」フォルダに配置します。

続けて、取得したSLANGコンパイラのリポジトリフォルダに移動してから「copyruntime.bat」を実行します。

実行すると、実行したフォルダにある *.env *.yml ファイルが、ユーザーディレクトリの .config\SLANG\ 以下にコピーされます。

これは、ランタイムライブラリ更新のたびに行ってください。

### ツール類の配置

下記のツール類を「tools」フォルダに入れます。

* コンパイル用に「アセンブラ[AILZ80ASM](https://github.com/AILight/AILZ80ASM)」
  * AILZ80ASM.exe を取得してフォルダにコピーします
* ディスクイメージを編集するため「HuDisk」
  *  [BouKiCHiさんのgithubのHuDiskのページ](https://github.com/BouKiCHi/HuDisk)からHuDisk.exeをダウンロードし、フォルダにコピーします
* ディスクイメージを編集するため「NDC」
  * https://euee.web.fc2.com/tool/tool.html
  * 上記ページから「NDC」をダウンロードし、NDC.exeをフォルダにコピーします
* cpmエミュレーターでCP/Mのアプリを実行したい場合は「cpm.exe」
  * https://www.vector.co.jp/soft/win95/util/se378130.html
  * 上記からCP/Mエミュレーターをダウンロードし、cpm.exeをフォルダにコピーします

それぞれtoolsフォルダに入ればツールの設定は完了です。

### エミュレーター用ディスクイメージの作成

LSX-Dodgers及びS-OSのシステムディスクに、コンパイルしたSLANGの実行ファイルを書き込んで、それをエミュレータで実行させるため、最小限の起動ディスクイメージを用意します。

* LSX-Dodgers用のディスクイメージの作成
  * [LSX-Dodgersのページ](https://github.com/tablacus/LSX-Dodgers)からLSX-Dodgersのディスクイメージをダウンロードします
  * エミュレータでそのディスクイメージを起動後、Bドライブにブランクディスクイメージ(ファイル名: LSXPROG.D88 )を入れます
  * sys B: にて、Bドライブのディスクにシステムを転送します
  * エミュレータを終了します
  * [X1 DiskExplorer](https://ceeezet.syuriken.jp/)をダウンロードし、作成したディスクイメージを読み込みます
  * SLANGコンパイラリポジトリの「env/LSX-Dodgers/AUTOEXEC.BAT」を、ディスクイメージに書き込みます
  * X1 DiskExplorerを終了します
  * ディスクイメージを「images」フォルダにコピーします
* S-OS用のディスクイメージの作成
  * [THE SENTINEL](http://www.retropc.net/ohishi/s-os/)より「X1/C/D/Cs/Ck/F/G/Twin(高速版)」の「D88イメージ」をダウンロードします
  * X1 DiskExplorerでD88イメージを読み込みます
  * SLANGコンパイラリポジトリの「env/S-OS/AUTOEXEC.BAT」を、ディスクイメージに書き込みます
  * AUTOEXEC.BATを右クリックして「ファイル形式の変更」を選び、ファイル形式を「Asc」にします
  * X1 DiskExplorerを終了します
  * ディスクイメージを「images」フォルダにコピーします

### ビルドバッチの実行

ここまでの手順を行う事で、ビルドバッチの実行の準備が整いました。ビルドバッチ「slbuild.bat」を開き、下記のパスを必要に応じて修正し、ビルド用フォルダにコピーしてください。

ビルド用フォルダに「bin」「tools」「images」がある場合は、「EMULATOR」の項目を書き換えるだけで良いでしょう(エミュレーターはCommon Source Code ProjectのX1エミュレータの使用を想定しています)。


```
SET TOOLPATH=%CURPATH%tools
SET IMAGEPATH=%CURPATH%images

SET SLANGCOMPILER=%CURPATH%bin\SLANGCompiler.exe
SET ASM=%TOOLPATH%\AILZ80ASM.exe

SET EMULATOR=D:\emu\x1\x1.exe

SET NDCPATH=%TOOLPATH%\NDC.exe
SET HUDISKPATH=%TOOLPATH%\HuDisk.exe
SET CPMEMUPATH=%TOOLPATH%\cpm.exe
```

ビルド用フォルダに例えば「TEST.SL」というSLANGのソースファイルがある場合、

```
slbuild.bat TEST.SL
```

と、実行する事で、LSX-Dodgers環境にてビルドされ、X1エミュレータに読み込まれ、実行されます(実行後、エミュレータを終了してください)。

S-OS環境で実行したい場合は、ソースファイル名の隣に環境名を書き、

```
slbuild.bat TEST.SL sos
```

と、する事で、自動的にS-OS用にビルドされ、エミュレータがS-OSのイメージを読み込み、起動、実行されます。

また、特殊な環境としてCP/MエミュレータでLSX-Dodgers環境のアプリを動かしたい場合、環境名を「cpm」とする事で、CP/Mエミュレーターを使い、コマンドラインでSLANGのアプリが実行されます。

都度都度自前でコンパイル、アセンブル、実行ファイルのイメージ転送、エミュ起動、読み込み、などを行ってももちろん良いですが、ビルドバッチを使う事で、かなりスムーズに開発を進める事が出来ます。必要に応じてご活用ください。

### ビルド用Makefileの実行

(試験的な実装です)

make TARGET=examples/FMANDEL ENV=msx2  といった感じで、TARGETに拡張子抜きのファイル名、ENVに環境名を指定します(環境名としてlsx、x1、sos、msxrom、msx2、cpm、pc80mk2の指定が可能です)

今後はバッチファイルはメンテされず、Makefileのみ更新される予定ですので、極力こちらをお使いください。


# ライセンス
MIT

# 更新履歴
- Version 0.10.0
  - pc80mk2x環境の追加
    - PC-8001mkIIの独自BIOSを組み込んだ全RAM環境(メモリ前半もRAMになっている環境)です
    - 関連して汎用BIOS「XBIOS」を追加
  - X1のグラフィック関数の追加
    - GRPSETUP() 初期化
    - LINE(X1,Y1,X2,Y2,Color(0〜7)) ライン描画
    - PAINT(X,Y,中間色) ペイント
    - BFILL(X1,Y1,X2,Y2,中間色) 矩形を中間色で塗る
  - X1にマウス関数を追加
    - MSINIT() 初期化
    - MSGET(MSDAT) マウス情報の取得
  - 環境構築時に取得するAILZ80ASMのバージョンをv1.0.7に更新
  - X1のS-OS環境でグラフィック関連関数、マウス関連関数を呼べるよう対応
- Version 0.9.0
  - msxlsx環境の追加
  - ビルド用Makefileの追加
  - M8Aライブラリを20230325版に更新
  - MSXのBDOSコールでIYを保存するよう対応 
  - output-debug-symbolをつけると_(関数名/変数名)_というシンボルを定義するよう対応 
  - ビルドバッチをMSX2のフロッピーディスク環境に対応 
  - MSXにHRA!さんのsprite driverを追加( https://github.com/hra1129/msx_documents/tree/main/vdp/sprite_sample )
  - PC-8001mkII環境の追加
    - 漢字ライブラリの追加
    - アトリビュート設定処理の追加
    - PCG8100互換サウンドドライバの追加
    - CMT出力機能の追加(AILZ80ASM内のコードを使わせていただきました。ありがとうございます)
  - ModuleSplitterの追加
    - モジュール指定をすると複数のモジュールバイナリを出力可能(同一アドレスエリアで動作するバイナリを作成し、動的に読み替えをする想定)
- Version 0.8.3
  - インクルード／ライブラリフォルダを整理
    - 環境ファイル(.env)をライブラリフォルダ内のenvフォルダに移動
    - ライブラリ定義ファイル(.yml)をライブラリフォルダ内のlibdefフォルダに移動
    - extlib内のアセンブラソースをライブラリフォルダに移動
    - extlib内のSLANGソースをインクルードフォルダに移動
    - 環境変数SLANG_INCLUDEにインクルードフォルダを設定可能に
    - 環境変数SLANG_LIBRARYにライブラリフォルダを設定可能に
    - コマンドラインオプション-Iでインクルードフォルダを追加設定可能に
    - コマンドラインオプション-Lでライブラリフォルダを追加設定可能に(これまでの追加ライブラリを指定するコマンドラインオプションは小文字の -l に変更されたので注意してください)
    - インクルード／ライブラリの検索を「環境変数→カレントのincludeまたはlibフォルダ→.config/SLANG内のincludeまたはlibフォルダ→コマンドライン指定されたフォルダ」の順で都度検索するよう対応
  - syntaxフォルダにVisual Studio Code用のSLANGシンタックスハイライトを行う拡張機能(.vsix)を追加
- Version 0.8.2
  - X1 SGLライブラリを追加
    - x1turbo.agency さんの X1 SGLライブラリをSLANGから使えるようにして組み込みました
    - これでX1でも夢のスプライト(風)描画が可能になります！(メモリ食うので色々頑張りが必要です)
    - 詳細はサンプル examples/X1SGL.SL を参照してください
  - ライブラリとして複数ファイルを読み込めるよう対応(libx1_sgl.yml 参照)
- Version 0.8.1
  - MSX ROM用関数をいくつか追加
    - BIOS呼び出し、初期化、PCG定義、スプライト定義、VRAM書き込み、スプライト表示など
    - STICK2関数を追加
    - ※サンプル MSXROM.SL を参照してください
  - 標準関数にMEMCPYとMEMSETを追加
  - 関数パラメータの関数内での代入が不正な代入になる問題を修正
  - 文字列0x80から0xffまでをバイナリ値として扱うよう変更
  - MSX ROM環境でSOROBANが使えるよう対応
- Version 0.8.0
  - MAGICライブラリ追加
    - ^IXにコマンドのアドレスを入れてCALLMAGIC()を呼ぶとMAGICの処理を行います
    - CALLMAGIC()の呼び出しが1つでもあるとMAGIC本体がアセンブラソースに含まれます
  - SOROBANライブラリ追加
    - SOROBAN.LIBを#INCLUDEで読み込んで使ってください
    - 読み込みを行うとSOROBAN本体がアセンブラソースに含まれます
  - GRAPH.LIB、GRAPHF.LIB追加
    - V2.1のSLANGクロスコンパイラ対応版の通常のGRAPH.LIBと、V2.1のSOROBAN依存部分をFLOAT型に差し替えたGRAPHF.LIBが入っているので、お好きな方を#INCLUDEしてお使いください
    - また、直接8色の指定をしたい方のために、例えば @LINEC(X1,Y1,X2,Y2,COL) のように0～7の色を指定出来る関数を追加しています(CONST値_COLORに1を入れてください)
      - _COLORを2にすると、@LINEなど従来の関数をソースから除外します(@LINEC等の色指定可能な関数だけが残ります)。用途により使い分けてください
  - 圧縮データの展開ライブラリを追加
    - LZE_DECODE(FROM,TO) ※ lze
    - LZEE_DECODE(FROM,TO) ※ LZEe
    - LZEEE_DECODE(FROM,TO) ※ LZEee f5
    - ZX0_DECODE(FROM,TO)
    - 的な感じです(雑)
  - RND()の乱数ロジックを変更
  - M8A画像が実機で表示されない問題を修正
  - ライブラリ(YAML)にALIGNを指定出来るよう対応
  - #IF内に#IFがあった場合不正に処理が行われてしまう問題を修正
  - ソースコードの読み込みフォルダを変更(ソースパス→カレント→configのパス→config内のextlibパス)
  - 変数定義時のアドレス指定内の「^」の左をAILZ80ASMのネームスペースとして扱うよう対応
    - VAR WORD _ZAHYO[255][2]:MAGIC^OBJ_BUF; といった感じです
  - FLOAT同士の掛け算の最適化に失敗する事がある問題を修正
- Version 0.7.3
  - FLOAT→WORDの自動キャストが正常に動かない場合があったのを修正
  - CONSTでFLOAT値を定義出来るよう対応
  - LSX-DodgersのFSEEK関数でファイル末尾への移動が正しくされない問題を修正
- Version 0.7.2
  - コンパイラの更新ミスがあったのを急遽修正(速度を一定に保つ処理(自前割り込みの呼び出し)が正しく動きませんでした)
- Version 0.7.1
  - X1turbo版S-OSにおいてPSG再生が出来ない問題を修正しました
  - PSG再生のテンポを割り込みを持たないX1においても一定に保てるよう対応しました(「X1におけるゲームループの処理」参照)
  - PSG_END()でコンパイルエラーが出ていたのを修正
- Version 0.7.0
  - PSG再生ライブラリを追加(X1/MSX ROM)
    - PSG_INIT() 初期化
    - PSG_PLAY(ADDRESS) 再生
    - PSG_PROC() 1/60ごとに呼び出す再生処理(CTCのあるX1及びMSXでは呼び出す必要はない)
    - PSG_END() 終了
    - PSG_STOP() 再生停止
    - PSG_PAUSE() 一時停止
    - PSG_RESUME() 再生再開
  - 外部シンボルを格納アドレス指定及びCONSTにて指定可能に
    - ARRAY EXTARR[]:EXTERNALLABEL; とすると、アセンブラコードのラベル「EXTERNALLABEL」をEXTARRという配列変数としてアクセス可能になります
    - CONST EXTDAT = EXTERNALLABEL; とする事でも、EXTDATをラベル「EXTERNALLABEL」として扱えます
    - #ASM〜#ENDASMでアセンブラコードを囲んで、その中で定義したラベルなどを指定する想定です
  -  配列サイズが定数の計算により設定されていた場合不正になる問題を修正 
  - ビルドバッチ/スクリプトについてMSX ROM環境に対応(openMSXを使用)
  - コンパイラの実行ファイルを単一ファイルに変更(publish.shの追加)
- Version 0.6.0
  - Mac用のビルドスクリプト slbuild.sh を追加(copyruntime.shも追加)
  - MAG画像読み込みライブラリを追加(Gakuさんありがとうございます！)
  - M8A画像読み込みライブラリを追加(試験的実装。hex125さんありがとうございます！)
  - ライブラリソースをランタイムYAMLファイルの外に書けるよう対応
  - ライブラリのネームスペースの指定が出来るよう対応
  - MSXのROM環境をお試しで追加
  - 変数「WORKEND」がワークの末尾を指す変数として自動定義されるよう対応
- Version 0.5.0
  - FLOAT型を試験的に追加(SLANG非互換)
    - VAR定義時に「VAR FLOAT X;」「VAR %%X;」のようにFLOATまたは%%をつけるとFLOAT型になります
    - 1.0 のようにピリオドを含む数値はFLOAT型になります
    - 特定以外の使い方をするとおかしな動きをすると思います
    - 通常グローバル変数としての利用のみ確認しています
    - 24bit FLOAT型となっており、表現出来る値は-32768～32767です
    - 四則演算の他、下記関数があります(ほぼ未チェック)
    - FABS(X)
    - FACOSH(X)
    - FACOS(X)
    - FASINH(X)
    - FASIN(X)
    - FATANH(X)
    - FATAN(X)
    - FCOSH(X)
    - FCOS(X)
    - FLOG10(X)
    - FLOG2(X)
    - FLOGY(X)
    - FLOG(X)
    - FPOW10(X)
    - FPOW2(X)
    - FPOW(X)
    - FRAND()
    - FSINH(X)
    - FSIN(X)
    - FSQRT(X)
    - FTANH(X)
    - FTAN(X)
  - ランタイムを個々に読むのではなく環境ファイルを読むように変更(-Eオプションの追加)
  - ライブラリにworks指定を追加(データ領域をワーク部(RAM領域)に移せるように)
  - ランタイムをコピーするバッチファイルcopyruntime.batを追加
  - ビルド用バッチファイルslbuild.batを追加
- Version 0.3.0
  - S-OS環境で文字入力によりワークが壊れる問題を修正 
  - ファイル入出力ライブラリの追加(FOPEN、FCLOSE、FGETC、FPUTC、FREAD、FWRITE)
  - WHILEの条件式に定数の0以外の値が入った場合に無限ループと判断し条件判断をしないよう最適化 
  - ファイル入出力ライブラリのMSX-DOS2対応
  - X1のPCG定義関数を追加 
- Version 0.2.0
  - CONSTにCODEリストを与える事が出来るよう対応
  - MACHINE関数について定義のみで実装出来なかった不具合を修正
  - 変数宣言のアドレス指定にCODEアドレスを持つCONST値を指定出来るよう対応
  - CONST関連の処理を見直し
  - ^CARRYと^CYを同じものとして扱うよう対応
  - 関数名と変数名についてデフォルトで大文字小文字を区別しないよう変更(--case-sensitiveオプションの追加)
  - 「!」をBYTEの別名として扱うよう対応
  - プリプロセッサ命令の一部を大文字小文字区別しないよう対応
  - CONST定義にランタイム(ラベル)を指定出来るよう対応
  - LOOP文(無限ループ)に対応
  - 間接変数の二次元配列に対応
  - EXIT(num)でnum個ぶんループを抜けられるよう対応(numは定数である必要があります)
  - 不正なランタイム関数名の内部名称の変更(BIT/SET)
  - examplesフォルダ追加
  - 0x〜で16進数として扱われるよう対応
  - CASE文の中からEXITで抜けられなかったのを修正
- Version 0.1.0
  - --use-symbolオプションを追加
  - 配列の初期化をCODEリストで行うよう修正
  - プリプロセッサのIFの定数評価の仕組みを調整
  - OFFSET文対応(無視されます)
  - ソースファイルの文字コードを自動判別するよう対応
  - 出力されるアセンブラソースをShiftJIS固定に(どうするか検討中。このままかも)
  - CODE項についてアドレス値とその加算の構文に対応( (配列名)+1 で、配列+1のアドレスを埋め込むような対応)
  - ELSEIFを追加
  - 二次元配列の初期化が出来ない問題を修正
  - MACHINE関数呼び出しの引数設定がおかしくなる事がある問題の修正
  - SGNが正常に動作しないバグを修正
  - OS非依存ランタイムをruntime.ymlに移動
  - 通常関数呼び出し時にパラメータが正しく渡らない事がある問題を修正 
- Version 0.0.2
  - 符号反転(NEGHL)が正常に機能しない問題を修正
  - 減算処理が正常に機能しない事がある問題を修正
  - コマンドラインオプションをザッと実装
  - X1用ランタイムを追加(まだ仮)
- Version 0.0.1
  - 初版
  - 多分バグだらけ(特に配列や間接変数など全般が怪しいです)
