ECHO OFF

mkdir %homedrive%%homepath%\.config\SLANG\
mkdir %homedrive%%homepath%\.config\SLANG\extlib
copy *.env %homedrive%%homepath%\.config\SLANG\
copy *.yml %homedrive%%homepath%\.config\SLANG\
xcopy /E /Y extlib %homedrive%%homepath%\.config\SLANG\extlib
