ECHO OFF

mkdir %homedrive%%homepath%\.config\SLANG\
copy *.env %homedrive%%homepath%\.config\SLANG\
copy *.yml %homedrive%%homepath%\.config\SLANG\
xcopy /E extlib %homedrive%%homepath%\.config\SLANG\
