# SLANG-compiler
SLANG Compiler (Z80) 0.5.0

# 概要

これは主に国産8bit PCで使われたOS「S-OS」オリジナルの構造型コンパイラ言語「SLANG」のクロスコンパイラです。

コンパイルする事で、Z80のアセンブラソースを出力するため、柔軟な活用が可能です。

現状、LSX-Dodgers及びS-OSで動作するように作られていますが、OS依存部分を個々作る事で、CPUにZ80を採用している様々な環境で動かす事が出来るはずです。

# 使い方

まだ作り途中なのであちこち怪しいです。

※環境構築と、簡単にビルドとエミュレータでの実行までを行える環境構築方法については [環境構築とSLANGプログラムのビルド方法](#環境構築とslangプログラムのビルド方法)を参照してください。

```
SLANGCompiler filename [-L library-name] [-O output-path]

SLANG Compiler 0.5.0
Copyright (c) 2022 OGINO Hiroshi / H.O SOFT

  -E, --env               Environment name.
  -L, --lib               Library name(s). ( lib*.yml )
  -O, --output            Output file path.
  --use-symbol            Use original symbol name.
  --case-sensitive        Set symbols to be case-sensitive.
  --source-comment        Include source code as comments.
  --help                  Display this help screen.
  --version               Display version information.
```

コマンドラインにSLANGのソースファイル(拡張子は通常は .SL )を渡す事で、ソースファイルの拡張子を .ASM にしたアセンブラソースが出力されます(-O オプションにファイル名を渡すと、そちらに出力されます)。

アセンブラソースは、Z80アセンブラ [AILZ80ASM](https://github.com/AILight/AILZ80ASM) でアセンブル出来るものが出力されますので、適宜ご利用ください。

-E オプションに続けて、環境名を設定する事で、各種環境用のORG値が設定され、対応するライブラリが読み込まれます。現在、環境は「lsx」「sos」「x1」「msx2」があり、デフォルト環境名は「lsx」になります。

-L オプションに続けて、ランタイムライブラリの名前を付与する事で、指定のライブラリを読み込む事が出来ます( -L x1 とする事で、 libx1.yml というライブラリを読み込みます)。ただし、こちらは-Eオプションでの環境名設定でおおむね行われますので、個別ライブラリを読み込ませたい時のみ指定するようにしてください。

--use-symbol をつけると、変数名、関数名について、ソースコードで利用した名前をそのまま使ってアセンブラソースが出力されます。つけない場合は、「SYM(数字)」という名前に置き換えられますので、アセンブラがラベルとして識別出来ない変数名、関数名を使っている場合は、--use-symbolをつけないようにしてください。

--case-sensitiveをつけると、識別子について大文字小文字が区別されます。つけない場合は区別されません。

--source-commentをつけるとアセンブラソースにSLANGのソースがコメントとして追加されます。

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

## X1関連ライブラリ
* libx1_pcg.yml
  * X1のPCG関連処理が含まれるライブラリ

## MSX-DOS2関連ライブラリ
* libmsx2_file.yml
  * MSX-DOS2用のファイル入出力関連処理が含まれるライブラリ

## 環境ファイル及びランタイムのパスについて
各環境ファイル(*.env)及び、runtime.yml と lib*.ymlは、カレントパス、あるいはユーザーフォルダの .config/SLANG/ フォルダの下から読まれます。環境により、適宜配置してください。

## ランタイムの記述ルール(書き途中)

- param_countにパラメータの数を入れてください
  - パラメータは3個までの場合はレジスタ渡しされます(HL、DE、BCの順)。
  - 4個以上の場合は全てIYレジスタをポインタとして適宜渡されます(SLANGの仕様を確認してください)
- callsに、このルーチンが呼び出すランタイムの名称を記述してください
- codeに、コード本体を書いてください。インデントは変えないでください。

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


# ライセンス
MIT

# 更新履歴
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
