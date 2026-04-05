# 更新履歴

## Version 0.20.1
- CASE文のカンマ区切り値（`6,7,8: body`）でbodyコードが重複生成される問題を修正
- FLOAT対応の強化
  - CONST定数式でFLOAT演算に対応（`CONST DEG2RAD = 3.14 / 180.0`）
  - ランタイム関数（FCOS, FSIN等）の戻り値型をFLOATとして正しく追跡
  - FL$()関数のPRINT出力に対応
  - FLOAT定数の即値ロード最適化（constant pool廃止、LD DE,imm / LD C,imm 方式）
  - FLOAT二項演算の直接レジスタロード最適化（halfDirectOps/reverseHalfDirectOps対応）
  - FLOAT比較演算の融合ジャンプ対応（fusedCompareJumps）
- x1環境の改善
  - _TXADRをLSX-Dodgers 1.62cの$EE8Eと共有化（GETLIN後のカーソル位置同期）
  - 改行時のスクロール処理をLSX-DodgersのBDOSに委譲（24行スクロール対応）
  - WIDTH(40/80)切替時にLSX-Dodgersのワーク変数を同期

## Version 0.20.0
- 新コンパイラ(slangc)への移行
  - IR(中間表現)ベースのコンパイラに全面書き換え（Parser → SemanticAnalyzer → IrGenerator → CodeGenerator）
  - ランタイムライブラリを .yml から .asm 形式に変更
  - #MODULE 指定時のモジュール分割ASMを直接出力（ModuleSplitter不要）
  - プリプロセッサ定数 ENV_TYPE / OS_TYPE を #IF 条件式で参照可能に
  - #IF プリプロセッサに定数式評価器を実装
  - 文字列データのShift-JIS変換対応
  - コード生成最適化: fusedCompareJumps、MACHINE直接レジスタロード、定数畳み込み等
- 旧コンパイラのソースは obsolete/ フォルダに移動

## Version 0.12.0
- ZX Spectrum Next環境（zxn）を追加
- PC-8801mkIISR環境（pc88mk2sr）を追加
- VGS-Zeroライブラリを1.24.0ベースに更新
  - vgs0_oam_set16()関数を追加
  - vgs0_debug()関数を追加
- Makefileの整理
  - ソースからのビルド・インストールを`make`、`make install`で行えるよう対応
  - `make publish`でリリースパッケージを作成可能に
- runtime.ymlにMIN()、MAX()関数を追加
- MEMSETのバグを修正（bcのデクリメント漏れ）

## Version 0.11.0
- VGS-Zero環境を追加
- 間接変数が正常に動作していなかったのを修正
- MSX0の開発環境を追加
- 最適化が効いていなかった問題を修正
- 引数スタック渡しの関数についてスタックの補正が漏れていたのを修正
- 二進数の解釈に失敗する事がある問題を修正
- BYTE変数でFORループした時に正しくループしない問題を修正
- 2のn乗での符号なしMODをANDに変換するよう対応
- シフト式の左右の値がWORDにキャストされない問題を修正

## Version 0.10.0
- pc80mk2x環境の追加
- X1のグラフィック関数の追加
- X1にマウス関数を追加
- 環境構築時に取得するAILZ80ASMのバージョンをv1.0.7に更新
- X1のS-OS環境でグラフィック関連関数、マウス関連関数を呼べるよう対応

## Version 0.9.0
- msxlsx環境の追加
- ビルド用Makefileの追加
- M8Aライブラリを20230325版に更新
- MSXのBDOSコールでIYを保存するよう対応
- output-debug-symbolをつけると_(関数名/変数名)_というシンボルを定義するよう対応
- PC-8001mkII環境の追加
- ModuleSplitterの追加

## Version 0.8.3
- インクルード／ライブラリフォルダを整理
- syntaxフォルダにVisual Studio Code用のSLANGシンタックスハイライト拡張機能を追加

## Version 0.8.2
- X1 SGLライブラリを追加
- ライブラリとして複数ファイルを読み込めるよう対応

## Version 0.8.1
- MSX ROM用関数をいくつか追加
- 標準関数にMEMCPYとMEMSETを追加
- 文字列0x80から0xffまでをバイナリ値として扱うよう変更
- MSX ROM環境でSOROBANが使えるよう対応

## Version 0.8.0
- MAGICライブラリ追加
- SOROBANライブラリ追加
- GRAPH.LIB、GRAPHF.LIB追加
- 圧縮データの展開ライブラリを追加
- RND()の乱数ロジックを変更

## Version 0.7.3
- FLOAT→WORDの自動キャストが正常に動かない場合があったのを修正
- CONSTでFLOAT値を定義出来るよう対応

## Version 0.7.2
- コンパイラの更新ミスがあったのを急遽修正

## Version 0.7.1
- X1turbo版S-OSにおいてPSG再生が出来ない問題を修正
- PSG再生のテンポを割り込みを持たないX1においても一定に保てるよう対応

## Version 0.7.0
- PSG再生ライブラリを追加(X1/MSX ROM)
- 外部シンボルを格納アドレス指定及びCONSTにて指定可能に

## Version 0.6.0
- Mac用のビルドスクリプト追加
- MAG画像読み込みライブラリを追加
- M8A画像読み込みライブラリを追加
- MSXのROM環境をお試しで追加
- 変数「WORKEND」がワークの末尾を指す変数として自動定義されるよう対応

## Version 0.5.0
- FLOAT型を試験的に追加(SLANG非互換)
- ランタイムを個々に読むのではなく環境ファイルを読むように変更(-Eオプションの追加)

## Version 0.3.0
- S-OS環境で文字入力によりワークが壊れる問題を修正
- ファイル入出力ライブラリの追加

## Version 0.2.0
- CONSTにCODEリストを与える事が出来るよう対応
- MACHINE関数について定義のみで実装出来なかった不具合を修正
- ELSEIFを追加
- 間接変数の二次元配列に対応
- EXIT(num)でnum個ぶんループを抜けられるよう対応

## Version 0.1.0
- --use-symbolオプションを追加
- プリプロセッサのIFの定数評価の仕組みを調整
- ELSEIFを追加

## Version 0.0.2
- 符号反転(NEGHL)が正常に機能しない問題を修正
- コマンドラインオプションをザッと実装

## Version 0.0.1
- 初版
