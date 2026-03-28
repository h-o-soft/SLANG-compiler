# SLANG新コンパイラ 残タスク一覧

## 実装状況サマリ

- **Lexer**: 完成
- **Preprocessor**: 完成 (#INCLUDE, #IF/#ELSE/#ENDIF)
- **Parser**: 完成 (14/15 examples, SLANGTEST.SL全パース)
- **SemanticAnalyzer**: 完成 (シンボルテーブル, ビルトイン, IYオフセット)
- **IrGenerator**: 完成 (全制御フロー, 配列, MEM/MEMW, 定数畳み込み)
- **CodeGenerator**: 完成 (直接ロード最適化, 比較融合, INC/DEC, ランタイム結合)
- **MODULE/オーバーレイ**: 完成 (フルコード生成, .incファイル)
- **テスト**: 30件全パス
- **仕様書**: docs/SLANG-spec.md

---

## 優先度: 高

### H1. SOROBAN.LIBの特殊構文対応
- `@` プレフィックス関数名 (`@CVFTU`, `@MOVE` 等)
- `@@@+$offset` 形式のアドレス相対参照
- `[CODE(...)]` 形式の関数本体定義
- 影響: MANDEL.SL (SOROBAN.LIB依存) がコンパイルできない
- 対応量: Parser/Lexer修正

### H2. ピープホール最適化の実適用
- PeepholeOptimizer.cs は骨格のみ（CLI未組込み）
- CLIのパイプラインに組み込んでASM出力に適用
- PUSH HL/POP DE/EX DE,HL パターンの最適化
- 不要な連続JP除去 (JP label / label:)

### H3. 二項演算のスタック管理改善
- 現在は直接ロード最適化が効かない場合のみスタック経由
- POP DE / EX DE,HL のパターンが156回残っている
- src2だけ単純ロードの場合に直接DE化する最適化

---

## 優先度: 中

### M1. 間接変数の完全対応
- `VAR BYTE POINT[]` の間接配列アクセスのZ80コード精度
- 二次元間接変数 `VAR F[][15]`

### M2. 配列ローカル変数のアドレス計算
- ローカル配列 `ARRAY LAR[3][5]` のIYオフセット計算
- ローカル配列名がアドレス定数として機能する仕様

### M3. 他プラットフォーム向けランタイム変換
- 現在LSX-Dodgers向けのみ変換済み
- MSX, X1, PC-8001, PC-8801, ZX Next, VGS0 の各ランタイム
- tools/convert_runtime.py で変換可能

### M4. 定数式評価の拡張
- `$` (現在のアドレス) の対応
- 文字列定数の定数式使用不可チェック
- CONST宣言での前方参照エラー

### M5. エラーメッセージの改善
- 行番号/列番号の精度向上
- エラーリカバリ（1つのエラーで大量の連鎖エラーが出る問題）
- 仕様書記載のエラーメッセージに準拠

---

## 優先度: 低

### L1. FLOAT型の完全対応
- 24bit浮動小数の演算コード生成 (f24add/sub/mul/div)
- Word↔Float型変換コード (i16tof24/FTOI)
- FLOAT定数リテラル
- ランタイム libfloat.yml の変換

### L2. SOS[]/SOSW[] のコード生成
- S-OS特殊ワークエリアアクセス
- 実装パターンはMEM/MEMWと同等

### L3. PORT[]/PORTW[] の完全テスト
- IR命令 PortIn/PortOut は定義済み
- CodeGenerator EmitPortIn/EmitPortOut も実装済み
- IrGenerator → IR命令への変換が未接続

### L4. CODE関数の完全対応
- `CODE(...)` の式中使用（マシン語データ実行後のHL値）
- `[式]` 形式のCODEリスト項目
- `<ラベル名>` 形式のCODEリスト項目

### L5. AILZ80ASM出力フォーマット準拠
- ラベル命名規則 (__L番号 形式)
- ネームスペース記法 ([NAMESPACE])
- ワーク変数の __WORK__ ベース相対配置
- デバッグシンボル出力

### L6. 他プラットフォーム対応
- 環境設定ファイル(.env)の読み込み
- プラットフォーム固有ORG/WORKデフォルト値
- 環境別ランタイム選択

### L7. テスト拡充
- 制御フロー(IF/WHILE/FOR/CASE/REPEAT)のテスト
- 多次元配列のテスト
- MEM/MEMW/PORT のテスト
- オーバーレイのテスト
- エッジケース（空関数、深いネスト、大きな配列等）

### L8. 元コンパイラとの出力比較ツール
- 同一ソースを元/新でコンパイルして出力を比較
- 機能的等価性の自動検証

---

## 将来検討

### F1. 他言語への移植
- TypeScript/Node.js (この環境で完結)
- Go / Rust (パフォーマンス重視)
- C# で動作確認後に移植

### F2. 言語仕様拡張
- 構造体/レコード型
- 文字列型のネイティブサポート
- マクロ

### F3. IDE連携
- LSP (Language Server Protocol) サーバー
- VSCode拡張の強化 (syntax/ に既存vsix)
