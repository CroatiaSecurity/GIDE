@echo off
setlocal

echo.
echo ================================================
echo        GIDE C# Build Script (Framework 4.8)
echo ================================================
echo.

set CSC="C:\Windows\Microsoft.NET\Framework64\v4.0.30319\csc.exe"
set OUTPUT=dist\GIDE.exe

if not exist %CSC% (
    echo [!] .NET Framework 4.8 compiler not found.
    pause
    exit /b 1
)

if not exist dist mkdir dist

echo [+] Compiling GIDE.exe...

%CSC% /target:exe /out:%OUTPUT% /optimize+ /platform:x64 ^
    /reference:System.dll ^
    /reference:System.Core.dll ^
    /reference:System.Net.Http.dll ^
    /reference:System.Web.Extensions.dll ^
    Program.cs ^
    GIDEClient.cs ^
    ToolExecutor.cs ^
    HistoryManager.cs ^
    ToolParser.cs ^
    Config.cs ^
    Installer.cs

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
    "C:\Program Files (x86)\Inno Setup 6\ISCC.exe" Installer\setup.iss
) else (
    echo [!] Inno Setup not found, skipping installer build
)

echo.
echo ================================================
echo   Build Complete!
echo ================================================
pause