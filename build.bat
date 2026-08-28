@echo off
chcp 936 >nul
setlocal
set "ROOT=%~dp0"
set "MSBUILD=%ProgramFiles(x86)%\Microsoft Visual Studio\18\BuildTools\MSBuild\Current\Bin\MSBuild.exe"
if not exist "%MSBUILD%" set "MSBUILD=%ProgramFiles%\Microsoft Visual Studio\18\BuildTools\MSBuild\Current\Bin\MSBuild.exe"
if not exist "%MSBUILD%" set "MSBUILD=%WINDIR%\Microsoft.NET\Framework64\v4.0.30319\MSBuild.exe"
if not exist "%MSBUILD%" exit /b 1
"%MSBUILD%" "%ROOT%WiFitool.csproj" /t:Rebuild /p:Configuration=Release
if errorlevel 1 exit /b 1
copy /Y "%ROOT%bin\Release\WiFitool.exe" "%ROOT%WiFitool.exe" >nul
if errorlevel 1 exit /b 1
rmdir /S /Q "%ROOT%bin" >nul 2>nul
rmdir /S /Q "%ROOT%obj" >nul 2>nul
exit /b 0
