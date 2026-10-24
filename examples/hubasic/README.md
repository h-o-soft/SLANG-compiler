# Hu-BASIC のサンプル

Hu-BASIC のテキストを SLANG に変換して動かすサンプルです。使い方は [docs/HUBASIC.md](../../docs/HUBASIC.md) を見てください。

| ファイル | 内容 |
|---|---|
| `HELLO.BAS` | 文字の表示・色・浮動小数・文字列の関数・キー入力 |
| `GRAPH.BAS` | グラフィックの `LINE` (線・箱・塗りつぶした箱) と `PAINT` |
| `MUSIC.BAS` | `PLAY`・`TEMPO`・`BEEP` |

変換した SLANG (`*.SL`) や組み立てた結果は、このディレクトリに置かないでください (`*.SL` は `.gitignore` で除外しています)。

## このディレクトリに置くもの

**このリポジトリのために新しく書いたものだけ**を置きます。置くファイルは、すべて `MANIFEST.txt` に名前と出どころを書きます
(`MANIFEST.txt` に名前のないファイルは commit できません。`scripts/githooks/pre-commit` が止めます)。
