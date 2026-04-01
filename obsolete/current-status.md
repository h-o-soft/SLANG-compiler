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

**テスト1-54: 全通過（旧コンパイラと一致）**

```
新: ...53OK 54OK
    INPUT 'START'  (入力待ちで正常停止)
```

### 既知の残課題

- examples/STARS.SL, examples/STARS_X1.SL: X1エミュでの動作未確認
- その他のexamplesの動作確認が未完了

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
| CmpLe(<=) | FOR文が1回で終了 | JR条件反転 | JR C→NC修正 |
| PUSH漏れ | 複合式の計算結果が壊れる | directBinaryOps等のcontinueでPUSH判定スキップ | continue前にNeedsPushAfter追加 |
| FOR DOWNTO | 0を跨いで無限ループ | unsigned比較 | signed CmpSGeに変更 |
| 関数引数IYオフセット | 引数値がずれる | 引数分がlocalSizeに含まれない | totalFrameSize=localOffset+params*2 |
| MemStore | MEM[]=で書込み先が逆 | 不要なEX DE,HL | EX DE,HL削除 |
| CmpEq重複SBC | ==が常にfalse | SBC HL,DEが2回 | 重複削除 |
| 全比較非融合版 | 条件判定が反転 | JR cond,$+3のcondが逆 | InvertCond辞書で統一 |
| ローカル2D配列 | 書き込み値が反映されない | InlineAsmのDestにtemp未紐付 | Dest=tempでPUSH判定を有効化 |
| StoreVar後のPUSH漏れ | FOR文の終了条件でスタック破壊 | NeedsPushAfterがStoreVarで打ち切り | StoreVar/StoreLocal後はスキャン続行 |
| ビルトイン/ランタイム関数の呼出規約 | LOCATE等がIY渡しになりレジスタ渡しでない | SymbolKind.FunctionでなくMachineFunctionが必要 | ユーザー定義関数以外は全てMACHINE（レジスタ渡し）に |
| MACHINE引数評価順序 | 2引数で1番目が上書きされる | 全引数Accept後にPushArg | Accept直後にPushArgに変更 |
| PortOut addr/value逆転 | PORT書込みでaddr/valueが逆 | EX DE,HL不要 | MemStoreと同じ修正 |

---

## 動作確認済みプログラム

| プログラム | アセンブル | CPM実行 |
|---|---|---|
| examples/HELLO.SL | OK | OK ("Hello, SLANG!") |
| examples/STARS.SL | OK | 未確認（X1エミュ必要） |
| examples/FURUI.SL | OK | 未確認 |
| examples/STARS_X1.SL | OK | 未確認（X1エミュ必要） |
| SLANGTEST.SL | OK | テスト1-54全通過（旧コンパイラと一致） |
| examples/FURUI.SL | OK | OK（素数一覧出力、正常終了） |
| examples/STARS.SL | OK | OK（LSX-Dodgers） |
| examples/STARS_X1.SL | OK | OK（X1エミュ） |
