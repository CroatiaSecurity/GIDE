@echo off
setlocal

echo.
echo ================================================
echo        GIDE C# Build Script (Framework 4.8)
echo ================================================
echo.

set CSC="C:\Windows\Microsoft.NET\Framework64\v4.0.30319\csc.exe"
set VERSION=0.4.0
set OUTPUT=dist\GIDE.exe

if not exist %CSC% (
    echo [!] .NET Framework 4.8 compiler not found.
    pause
    exit /b 1
)

if not exist dist mkdir dist

echo [+] Compiling GIDE.exe...

%CSC% /target:winexe /out:%OUTPUT% /optimize+ /platform:x64 ^
    /reference:System.dll ^
    /reference:System.Core.dll ^
    /reference:System.Net.Http.dll ^
    /reference:System.Web.Extensions.dll ^
    /reference:System.Management.dll ^
    /reference:System.Windows.Forms.dll ^
    /reference:System.Drawing.dll ^
    Program.cs ^
    GIDEClient.cs ^
    ToolExecutor.cs ^
    HistoryManager.cs ^
    ToolParser.cs ^
    Config.cs ^
    Installer.cs ^
    HardwareDetector.cs ^
    ModelManager.cs ^
    LocalModelEngine.cs ^
    GIDESettingsForm.cs ^
    GIDEMainForm.cs

if %ERRORLEVEL% NEQ 0 (
    echo.
    echo [!] Build FAILED. See errors above.
    pause
    exit /b 1
)

echo [+] Build successful: %OUTPUT%

echo.
echo Building installer...
if exist "C:\Program Files (x86)\Inno Setup 6\ISCC.exe" (
    REM Inno Setup outputs to releases\%VERSION%\ (defined in Installer\setup.iss OutputDir)
    "C:\Program Files (x86)\Inno Setup 6\ISCC.exe" Installer\setup.iss
) else (
    echo [!] Inno Setup not found, skipping installer build
)

if not exist "releases\%VERSION%" mkdir "releases\%VERSION%"
if exist dist\GIDE.exe copy /Y dist\GIDE.exe "releases\%VERSION%\"

echo.
echo ================================================
echo   Build Complete!
echo   Output: releases\%VERSION%\
echo ================================================