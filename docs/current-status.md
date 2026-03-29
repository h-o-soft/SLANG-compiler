# 新SLANGコンパイラ 現在の動作状況

最終更新: 2026-03-29

## 確認方法

### ユニットテスト
```bash
dotnet test tests/SLANGCompiler.Tests/ -c Release
# 63テスト全通過
```

### SLANGTEST.SLのコンパイル→アセンブル→実行
```bash
# コンパイル+アセンブル
make -f Makefile.test clean && make -f Makefile.test asm

# CPMエミュで実行（wine経由、macOS）
cp PROG.bin PROG.COM
wine ~/projects/SLANG-compiler/tools/cpm.exe PROG.COM

# 旧コンパイラとの比較
make -f Makefile.test compare
```

### 個別テスト
```bash
# 任意のソースをコンパイル→アセンブル→実行
make -f Makefile.test TARGET=examples/HELLO asm
cp examples/PROG.bin examples/PROG.COM
wine ~/projects/SLANG-compiler/tools/cpm.exe examples/PROG.COM
```

---

## SLANGTESTの動作状況

### 旧コンパイラ（リファレンス）
テスト1-54まで全て正常通過。テスト55以降はINPUT待ち（対話的）。

### 新コンパイラ
**テスト1-35: 全て正常通過**（旧コンパイラと同一出力）

**テスト36以降: ハング**（出力が停止し、プログラムが応答しなくなる）

```
旧: ...33OK 34OK
    35OK 36OK 37OK 38OK 39OK 40OK 41OK 42OK
    ARRAY2 TEST
    43OK ... 54OK
    INPUT 'START'

新: ...33OK 34OK
    35OK
    (ここでハング)
```

### ハングの切り分け結果

| テストケース | 結果 |
|---|---|
| テスト33-37を単独で実行 | **正常動作**（33OK 34OK 35OK 36OK DONE） |
| テスト35-37を単独で実行 | **正常動作** |
| SLANGTEST.SL全体（テスト1-35後にテスト36） | **テスト36でハング** |

**結論**: テスト36の処理自体は正しい。SLANGTEST全体の実行でのみハングする。テスト1-35の累積的な影響（スタック不整合またはメモリ破壊）が疑われる。

### 疑われる原因

1. **スタック不整合**: 前のテストのIF文やAND/OR演算でPUSH/POPがバランスしていないケースが蓄積し、テスト36付近でスタックがコード/データ領域に食い込む
2. **レジスタ破壊**: ランタイム関数呼び出し（P10, PMSX等）後にIYレジスタが復元されていない等
3. **ワーク領域破壊**: ローカル変数やシステムワーク変数のオフセット計算に未発見のバグがあり、テスト数が多くなると破壊が表面化

### 調査方針案

1. SLANGTEST.SLを二分割して、ハングが発生する最小のテスト範囲を特定
2. SLANGTEST.ASMのPUSH/POP数をカウントし、全体のスタックバランスを確認
3. Z80デバッガ（z80simやzesarux等）でステップ実行し、ハング箇所を特定

---

## 修正済みのバグ一覧（本セッション）

| バグ | 症状 | 原因 | 修正 |
|------|------|------|------|
| PRINT文字列 | 文字化け | PSTRをCALLしていた | PMSX(null終端出力)に変更 |
| PRINT数値 | 1文字出力 | PRTをCALLしていた | P10(10進変換)に変更 |
| IYフレーム | ローカル配列でフレーム未確保 | ComputeLocalSizeがLoadLocal/StoreLocalのみスキャン | IrFunction.LocalSizeを導入 |
| ランタイム名 | MUL16等が未定義 | MULHLDE等と名前不一致 | ランタイム名を統一 |
| ランタイムリンク | 算術/比較のCALLがリンクされない | _calledFunctionsに追加されない | CallRuntime()ヘルパー導入 |
| 変数ラベル衝突 | VAR Aとシステム^Aの_A衝突 | 同一プレフィックス | ユーザー変数を__プレフィックスに |
| CODEブロックCONST | CONST X=[...]が出力されない | VisitConstDeclがコメントのみ | GlobalVarsに追加+LoadAddr参照 |
| ランタイム出力順 | フォールスルー破壊 | 依存解決順で出力 | LoadOrder(定義順)でソート |
| CmpLe(<=) | FOR文が1回で終了 | JR条件が反転 | JR C→NC修正 |
| PUSH漏れ | 複合式(X*Y+X*X/10)の結果が壊れる | directBinaryOps等のcontinueでPUSH判定スキップ | continue前にNeedsPushAfter追加 |
| FOR DOWNTO | 0を跨いで無限ループ | unsigned CmpGe使用 | signed CmpSGeに変更 |
| 関数引数IYオフセット | 引数値がずれる | 引数分がlocalSizeに含まれない | totalFrameSize=localOffset+params*2 |
| MemStore | MEM[]=で書込み先が逆 | EX DE,HL不要 | EX DE,HL削除 |
| CmpEq非融合版 | ==比較が常にfalse | SBC HL,DEが2回+JR条件反転 | 重複SBC削除+条件反転修正 |
| 全比較非融合版 | CmpNeq/CmpLt/CmpGe/CmpGtも反転 | JR cond,$+3のcondが逆 | InvertCond辞書で統一修正 |
| sWORKデータ混入 | sCRTCD(DB $6F)がコード領域に | worksではなくcodeに定義 | worksメタデータに移動 |

---

## 動作確認済みプログラム

| プログラム | アセンブル | CPM実行 |
|---|---|---|
| examples/HELLO.SL | OK | OK ("Hello, SLANG!") |
| examples/STARS.SL | OK | 未確認（X1エミュ必要） |
| examples/FURUI.SL | OK | 未確認 |
| examples/STARS_X1.SL | OK | 未確認（X1エミュ必要） |
| SLANGTEST.SL | OK | テスト1-35通過、36以降ハング |
