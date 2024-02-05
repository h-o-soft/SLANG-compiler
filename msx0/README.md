# MSX0用の環境について

## 概要
SLANGクロスコンパイラでMSX0のプログラムを開発する場合、いちいち手作業で実機のディスクイメージに実行ファイルをコピーするのが面倒なので、その作業を簡略化するためにツール及びMakefileを作りました。

ちくわ帝国さんのMSX0GETを活用させていただいております(ありがとうございます！)。

## 下準備
この環境を使うためには、下記の準備が必要になります。

* MSX-DOS(2)のディスクイメージを用意する(MSX0に付属しています)
* そのディスクイメージ内に、ちくわ帝国さんの「MSX0GET.COM」を格納する
  * [【MSX0用アプリ】HTTPファイル受信コマンド – MSX0GET](https://chikuwa-empire.com/computer/msx0-app-httpget/)
  * ndcで格納する場合は ```ndc P DOS2.DSK 0 MSX0GET.COM``` といった感じになります。
* MSX0GET.COMが入ったディスクイメージをMSX0に格納し、起動しておく
* MSX0をWi-Fi接続しておく
* ローカル環境にHTTPサーバを起動しておく
* SLANGで開発するマシンからそのHTTPサーバにファイルコピー出来るようにしておく

……結構面倒ですが頑張ってください。

## 使い方
このフォルダにあるMSX0用のMakefileを書き換えます。

```Makefile
# MSX0側がアクセスするHTTPサーバのアドレス
WEBIP = 192.168.0.5
# MSX0側がアクセスするHTTPサーバのポート
WEBPORT = 80
# MSX0側がアクセスするHTTPサーバのURL
WEBADR = /~user

# HTTPサーバのドキュメントルート(実行ファイルのコピー先。ローカルでHTTPサーバが稼動している想定)
WEBDIR = ~/Sites

# MSX0のIPアドレスとポート番号
MSX0IP = 192.168.0.10
MSX0PORT = 2223

# SLANGプログラム名称
SRC ?= PROG.SL
```

書き換えが必要なのはおおむね上記です。また、基本的にMac/Linux用なので、Windowsの方はトライ＆エラーで書き換えてください(テキトー)。

書き換えた後、makeコマンドを実行すると、

* SLANGクロスコンパイラでのコンパイル
* AILZ80ASMでのアセンブル
* 生成された実行ファイルをHTTPサーバの管理下にコピー
  * Makefileの WEBDIR に指定したフォルダに実行ファイルがコピーされますので、ローカルから参照可能なフォルダを指定してください
* msx0cmd.pyでの実機側へのコマンド送信
* MSX0側でMSX0GET.COMを使いHTTP通信にて実行ファイルを取得
* MSX0側での自動実行

が、順番に行われます。

ソースファイル名はデフォルトで「PROG.SL」になっていますが、変更したい場合は「make SRC=MSX0IOT.SL」のようにコマンドラインから引数を与えてください。

## msx0cmd.pyについて
msx0cmd.py は、MSX0に対して任意の文字列を送信するためのコマンドです。

本環境においては、MSX-DOSに対してコマンドライン文字列を送るために使っています。

```
usage: msx0cmd.py [-h] --ip IP [--port PORT] [--check-str CHECK_STR] [--error-str ERROR_STR] [--sleep SLEEP] message
```

* --ip IP
  * IPにMSX0のIPアドレスを指定します。必須オプションです。
* --port PORT
  * PORTにMSX0の接続ポートを指定します。デフォルトは2223です。省略可能。
* --check-str CHECK_STR
  * CHECK_STRに指定した文字列がコマンドの実行結果に含まれる場合、コマンドは正常終了します。含まれない場合は異常終了(エラーコード1)になります。
* --error-str ERROR_STR
  * ERROR_STRに指定した文字列がコマンドの実行結果に含まれる場合、コマンドは異常終了(エラーコード1)します
* --sleep SLEEP
  * コマンド実行後にSLEEPで指定した秒数待ちます

具体的にはMakefileの記述を参照してください(MSX0GETコマンドは正常時はDone、エラー時はErrorという文字列を返すので、それにより終了コードが変わります。シェルスクリプトなどで処理を分岐させる時などに便利です)。

## 謝辞
ちくわ帝国さんのMSX0GET.COMを普通に使ってるだけやんけ、的な意見がありますが、そのとおりです。ちくわ帝国さんありがとうございます……！

→ [ちくわ帝国](https://chikuwa-empire.com/)

# ライセンス
* msx0cmd.pyはMITライセンスになります
