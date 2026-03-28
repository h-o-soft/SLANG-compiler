# SLANG新コンパイラ 残タスク一覧

## 実装状況サマリ

- **Lexer**: 完成
- **Preprocessor**: 完成
- **Parser**: 完成 (14/15 examples, SLANGTEST.SL全パース)
- **SemanticAnalyzer**: 完成
- **IrGenerator**: 完成 (最適化4種含む)
- **CodeGenerator**: 完成 (直接ロード/比較融合/INC最適化)
- **MODULE/オーバーレイ**: 完成
- **テスト**: 30件全パス
- **仕様書**: docs/SLANG-spec.md

---

## 優先度: 最高

### H0. 間接変数の完全対応
- `VAR BYTE POINT[]` — 変数+配列の二面性を持つ型
- `POINT = $C000; I = POINT[3];` → *(POINT + 3*elemSize)
- 二次元間接変数 `VAR F[][15]`
- 基本仕様に含まれる必須機能

## 優先度: 高

### H1. FLOAT型の完全対応
- 24bit浮動小数の演算コード生成 (f24add/sub/mul/div/cmp/neg)
- Word↔Float型変換コード (i16tof24/FTOI)
- FLOAT定数リテラル
- ランタイム libfloat.yml の変換
- `%%` / `FLOAT` で宣言、3バイト確保

### H2. PORT[]/PORTW[] のIR接続
- CodeGeneratorのEmitPortIn/EmitPortOutは実装済み
- IrGeneratorからPortIn/PortOut IR命令への変換が未接続
- SOS[]/SOSW[]も同パターンで対応

### H3. CODE関数の完全対応
- `CODE(式)` — 直接データをオブジェクトに埋め込む関数
- CODEリスト: `"文字列"`, `[式]`(HLにロードするコード), `<ラベル名>`(2バイトアドレス), `型,定数式`
- 配列初期化 `ARRAY DT[4]={0,1,2,3,4}` で既に部分対応
- 式中で使用した場合、マシン語実行後のHLの値が値となる

### H4. SOROBAN.LIBの特殊構文
- `@` プレフィックス関数名 (`@CVFTU`, `@MOVE` 等)
- `@@@+$offset` 形式のアドレス相対参照
- `[CODE(...)]` 形式の関数本体定義
- 影響: MANDEL.SL (唯一の失敗example)

---

## 優先度: 中

### M1. ピープホール最適化の実適用
- PeepholeOptimizer.cs 骨格あり、CLI未組込み
- PUSH HL/POP DE/EX DE,HL パターン最適化

### M2. src2のみ単純ロード時のDE直接化
- 現在PUSH/POP経由が156回残存

### M3. ローカル配列のアドレス計算
- `ARRAY LAR[3][5]` のIYオフセット計算

### M4. 他プラットフォーム向けランタイム変換
- MSX, X1, PC-8001, PC-8801, ZX Next, VGS0

### M5. 定数式評価の拡張 (`$` アドレス等)

### M6. エラーメッセージの改善

---

## 優先度: 低

### L1. AILZ80ASM出力フォーマット準拠
### L2. 他プラットフォーム対応 (.env読み込み)
### L3. テスト拡充
### L4. 元コンパイラとの出力比較ツール

---

## 将来検討

### F1. 他言語への移植 (TS/Go/Rust)
### F2. 言語仕様拡張 (構造体等)
### F3. IDE連携 (LSP)
