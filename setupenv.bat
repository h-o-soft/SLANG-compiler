ECHO OFF

REM �R�}���h�����邩�`�F�b�N

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

REM NDC���_�E�����[�h
curl https://euee.web.fc2.com/tool/NDC0A07.LZH -fsLO
lha xf NDC0A07.LZH
DEL NDC0A07.LZH
copy NDC.exe %TOOLPATH%
DEL NDC.exe

REM HuDISK���_�E�����[�h
REM curl https://github.com/BouKiCHi/HuDisk/raw/master/HuDisk.exe -OL
REM (ASCII�������݉\��)
curl https://github.com/ho-ogino/HuDisk/raw/feature/write-ascii-mode/HuDisk.exe -OL
IF not %errorlevel%==0 goto ERROR
copy HuDisk.exe %TOOLPATH%
DEL HuDisk.exe

REM AILZ80ASM���_�E�����[�h
SET AILZNAME=AILZ80ASM.win-x64.v1.0.7.zip
curl https://github.com/AILight/AILZ80ASM/releases/download/v1.0.7/%AILZNAME% -OL
IF not %errorlevel%==0 goto ERROR
unzip -xo %AILZNAME%
copy AILZ80ASM.exe %TOOLPATH%
DEL AILZ80ASM.exe
DEL %AILZNAME%

REM cpm.exe���_�E�����[�h
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

REM REM S-OS(X1)���_�E�����[�h
curl http://www.retropc.net/ohishi/s-os/SWXCV110.zip -OL
unzip -xo SWXCV110.zip
REM AUTOEXEC.BAT��ǉ�
REN SWXCV110.d88 SOSPROG.d88
%TOOLPATH%HuDisk SOSPROG.d88 -a ..\env\S-OS\AUTOEXEC.BAT --ascii
copy SOSPROG.d88 ..\images\
DEL SOSPROG.d88
DEL SWXCV110.zip

REM LSX-Dodgers�͓���t�H�[�}�b�g�̂��ߎ擾���ĉ��H���鎖���o���Ȃ�(NDC�ŃA�N�Z�X�s��)���ߑΉ����Ȃ�
REM �ǂ��������̂��c�c
REM curl https://github.com/tablacus/LSX-Dodgers/releases/download/1.55/ldsys155.zip -OL
REM unzip ldsys155.zip

REM ����DOS for MSX���_�E�����[�h
curl https://github.com/tablacus/dosformsx/releases/download/0.16/dosformsx_016.zip -OL
unzip -xo dosformsx_016.zip
REM AUTOEXEC.BAT��ǉ�
%TOOLPATH%NDC P dosformsx.dsk 0 ..\env\LSX-Dodgers\AUTOEXEC.BAT
copy dosformsx.dsk ..\images\
DEL dosformsx.dsk
DEL dos2formsx.dsk
DEL dosformsx_016.zip

cd ..
rmdir temp /q

EXIT /B

:ERROR
ECHO             _________________
ECHO --------------------------------------------
ECHO Error! %CMDNAME%���C���X�g�[������Ă��܂���
ECHO 
PAUSE
