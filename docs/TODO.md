# SLANG新コンパイラ 残タスク一覧

最終更新: 2026-03-29

## 実装状況サマリ

- **Lexer**: 完成
- **Preprocessor**: 完成 (#INCLUDE再帰展開, #IF/#ELSE/#ENDIF)
- **Parser**: 完成 (15/15 examples, SLANGTEST.SL全パース)
- **SemanticAnalyzer**: 完成 (ビルトイン登録, IYオフセット, 定数式評価)
- **IrGenerator**: 完成 (全制御フロー, FLOAT追跡, 定数畳み込み)
- **CodeGenerator**: 完成 (直接ロード/比較融合/INC最適化/halfDirect/ピープホール)
- **MODULE/オーバーレイ**: 完成 (フルZ80コード生成, .incファイル)
- **ランタイム**: 全48ファイル新形式変換済み
- **プラットフォーム**: 全8環境対応 (.env読込み)
- **テスト**: 30件全パス
- **仕様書**: docs/SLANG-spec.md

### 完了済みタスク (本セッション34コミット)

- [x] H0: 間接変数の完全対応 (BYTE/WORDスケーリング)
- [x] H1: FLOAT型 (24bit演算, f24ランタイム, 3バイトLD/ST)
- [x] H2: PORT[]/PORTW[]/SOS[]/SOSW[] のIR接続
- [x] H3: CODE関数 (DB/DW/[式]/<ラベル>/型指定)
- [x] H4: SOROBAN.LIB対応 (MACHINE関数の(定数)パターン)
- [x] M1: ピープホール最適化の実適用 (5ルール)
- [x] M2: src2のみ単純ロード時のDE直接化
- [x] M3: ローカル配列のIYオフセットアドレス計算
- [x] M4: 全48ランタイム新形式変換
- [x] M5: 定数式評価拡張 (CastExpr, ConditionalExpr)
- [x] L2: 全8プラットフォーム .env読込み対応

---

## 残タスク

### 優先度: 高

#### CODEリスト内 %定数 のバグ修正
- `ARRAY ARI[32]={1,2,3,%5,%6,...}` の `%5` が0になる
- ParseCodeItemのCastExpr → IrGeneratorの定数評価パスに問題

#### 静的宣言 vs 局所宣言の区別
- 仕様: BEGIN前のVAR(静的宣言)は静的メモリに配置
- 現状: 全てローカル変数(IYオフセット)として扱われている
- SemanticAnalyzer/IrGeneratorで区別が必要

### 優先度: 中

#### メモリレイアウトの統一 (docs/memory-layout-design.md)
- 変数: 実行時コピー方式 (DS + LD)
- 配列: コード内埋込み方式 (DB)
- 方式が不統一。ROM環境での配列書き換え不可問題
- Phase 1: 全部実行時コピーで統一
- Phase 2: env_type別戦略
- Phase 3: text/rodata/data/bssセクション分離

#### 定数条件IF文の最適化
- `IF CONV==123` (CONST同士) が定数畳み込みされて常にTRUEだが
  条件チェックコード(LD A,H; OR L; JP Z)が残る
- WHILE(TRUE)と同様にIrGeneratorで条件が定数の場合を処理

#### エラーメッセージの改善
- 行番号/列番号の精度向上
- エラーリカバリ（連鎖エラー抑制）
- 仕様書記載のエラーメッセージに準拠

### 優先度: 低

#### AILZ80ASM出力フォーマット準拠
- ラベル命名規則 (__L番号形式)
- ネームスペース記法 ([NAMESPACE])
- __WORK__ベース相対配置

#### テスト拡充
- 制御フロー、多次元配列、MEM/MEMW/PORT、オーバーレイ
- エッジケース（空関数、深いネスト、大きな配列等）

#### 元コンパイラとの出力比較ツール

---

## 将来検討

### F1. 他言語への移植 (TS/Go/Rust)
### F2. 言語仕様拡張 (構造体等)
### F3. IDE連携 (LSP)
