ECHO OFF

REM コマンドがあるかチェック

SET CMDNAME=curl
curl --version
IF not %errorlevel%==0 goto ERROR

SET CMDNAME=unzip
unzip -h
IF not %errorlevel%==0 goto ERROR

SET CMDNAME=lha
lha --version
IF not %errorlevel%==0 goto ERROR

SET TOOLPATH=%~dp0tools\

mkdir images
mkdir tools

mkdir temp
cd temp

REM NDCをダウンロード
curl https://euee.web.fc2.com/tool/NDC0A07.LZH -fsLO
lha xf NDC0A07.LZH
DEL NDC0A07.LZH
copy NDC.exe %TOOLPATH%
DEL NDC.exe

REM HuDISKをダウンロード
REM curl https://github.com/BouKiCHi/HuDisk/raw/master/HuDisk.exe -OL
REM (ASCII書き込み可能版)
curl https://github.com/ho-ogino/HuDisk/raw/feature/write-ascii-mode/HuDisk.exe -OL
IF not %errorlevel%==0 goto ERROR
copy HuDisk.exe %TOOLPATH%
DEL HuDisk.exe

REM AILZ80ASMをダウンロード
SET AILZNAME=AILZ80ASM.win-x64.v1.0.1.zip
curl https://github.com/AILight/AILZ80ASM/releases/download/v1.0.1/%AILZNAME% -OL
IF not %errorlevel%==0 goto ERROR
unzip -xo %AILZNAME%
copy AILZ80ASM.exe %TOOLPATH%
DEL AILZ80ASM.exe
DEL %AILZNAME%

REM cpm.exeをダウンロード
curl https://ftp.vector.co.jp/57/78/2156/cpm32_04.zip -OL
IF not %errorlevel%==0 goto ERROR
unzip -xo cpm32_04.zip
copy cpm.exe %TOOLPATH%
DEL cpm32_04.zip
DEL cpm32_04.txt
DEL cpm.exe
DEL COPYING
DEL INFO0P.COM
DEL RCCP.COM
DEL 4GCLOCK.COM
del src\* /q
del utl\* /q
rmdir src /q
rmdir utl /q

REM REM S-OS(X1)をダウンロード
curl http://www.retropc.net/ohishi/s-os/SWXCV110.zip -OL
unzip -xo SWXCV110.zip
REM AUTOEXEC.BATを追加
REN SWXCV110.d88 SOSPROG.d88
%TOOLPATH%HuDisk SOSPROG.d88 -a ..\env\S-OS\AUTOEXEC.BAT --ascii
copy SOSPROG.d88 ..\images\
DEL SOSPROG.d88
DEL SWXCV110.zip

REM LSX-Dodgersは特殊フォーマットのため取得して加工する事が出来ない(NDCでアクセス不可の)ため対応しない
REM どうしたものか……
REM curl https://github.com/tablacus/LSX-Dodgers/releases/download/1.55/ldsys155.zip -OL
REM unzip ldsys155.zip

cd ..
rmdir temp /q

EXIT /B

:ERROR
ECHO             _________________
ECHO --------------------------------------------
ECHO Error! %CMDNAME%がインストールされていません
ECHO 
PAUSE
