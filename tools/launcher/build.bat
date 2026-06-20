@echo off
call "C:\Program Files\Microsoft Visual Studio\18\Community\VC\Auxiliary\Build\vcvars64.bat" >nul
cd /d "%~dp0"
copy /Y "..\..\Assets\app.ico" app.ico >nul
rc /nologo /fo launcher.res launcher.rc
cl /nologo /O1 /MT launcher.c launcher.res /Fe:HDRSnap2.exe /link /SUBSYSTEM:WINDOWS shell32.lib user32.lib
del launcher.obj launcher.res app.ico 2>nul
echo BUILD_DONE
