ECHO OFF

mkdir %homedrive%%homepath%\.config\SLANG\
mkdir %homedrive%%homepath%\.config\SLANG\include
mkdir %homedrive%%homepath%\.config\SLANG\lib
xcopy /E /Y include %homedrive%%homepath%\.config\SLANG\include
xcopy /E /Y lib %homedrive%%homepath%\.config\SLANG\lib