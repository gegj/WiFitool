@echo off
chcp 936 >nul
setlocal
set "ROOT=%~dp0"
set "MSBUILD=%ProgramFiles(x86)%\Microsoft Visual Studio\2022\BuildTools\MSBuild\Current\Bin\MSBuild.exe"
if not exist "%MSBUILD%" set "MSBUILD=%ProgramFiles%\Microsoft Visual Studio\2022\Community\MSBuild\Current\Bin\MSBuild.exe"
if not exist "%MSBUILD%" set "MSBUILD=%WINDIR%\Microsoft.NET\Framework64\v4.0.30319\MSBuild.exe"
if not exist "%MSBUILD%" exit /b 1
"%MSBUILD%" "%ROOT%WiFitool.csproj" /t:Rebuild /p:Configuration=Release
if errorlevel 1 exit /b 1
exit /b 0
