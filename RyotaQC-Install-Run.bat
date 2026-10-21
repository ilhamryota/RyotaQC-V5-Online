@echo off
setlocal EnableDelayedExpansion
title RyotaQC V5 Auto-Installer and Launcher

cd /d "%~dp0"
set "REPO_DIR=%~dp0"
set "APP_DIR=%REPO_DIR%src\RyotaQC.App"
set "PUBLISH_DIR=%REPO_DIR%publish"
set "EXE_NAME=RyotaQC.App.exe"

echo ============================================================
echo   RYOTAQC V5 ONLINE - ESTERAONE ERP
echo   Auto-Installer and Launcher
echo ============================================================
echo.

REM ============================================================
REM 1. Cari EXE yang sudah pernah dibangun (Release > Debug > Publish)
REM ============================================================
set "FOUND_EXE="

if exist "%APP_DIR%\bin\Release\net10.0-windows\win-x64\%EXE_NAME%" (
    set "FOUND_EXE=%APP_DIR%\bin\Release\net10.0-windows\win-x64\%EXE_NAME%"
) else if exist "%APP_DIR%\bin\Release\net10.0-windows\%EXE_NAME%" (
    set "FOUND_EXE=%APP_DIR%\bin\Release\net10.0-windows\%EXE_NAME%"
) else if exist "%APP_DIR%\bin\Debug\net10.0-windows\%EXE_NAME%" (
    set "FOUND_EXE=%APP_DIR%\bin\Debug\net10.0-windows\%EXE_NAME%"
) else if exist "%PUBLISH_DIR%\%EXE_NAME%" (
    set "FOUND_EXE=%PUBLISH_DIR%\%EXE_NAME%"
) else (
    REM Pencarian menyeluruh sebagai fallback
    for /r "%REPO_DIR%" %%f in (%EXE_NAME%) do (
        if not defined FOUND_EXE if exist "%%~f" set "FOUND_EXE=%%~f"
    )
)

if defined FOUND_EXE (
    echo [OK] Aplikasi RyotaQC V5 sudah siap!
    echo      Lokasi: %FOUND_EXE%
    echo.
    echo Membuka RyotaQC V5 Desktop...
    timeout /t 2 >nul
    start "" "%FOUND_EXE%"
    exit /b 0
)

echo [INFO] Aplikasi belum dibangun. Memulai proses build...
echo.

REM ============================================================
REM 2. Cari executable dotnet
REM ============================================================
set "DOTNET_CMD="
if exist "C:\Program Files\dotnet\dotnet.exe" (
    set "DOTNET_CMD=C:\Program Files\dotnet\dotnet.exe"
) else if exist "%ProgramFiles%\dotnet\dotnet.exe" (
    set "DOTNET_CMD=%ProgramFiles%\dotnet\dotnet.exe"
) else if exist "C:\Program Files (x86)\dotnet\dotnet.exe" (
    set "DOTNET_CMD=C:\Program Files (x86)\dotnet\dotnet.exe"
) else (
    where dotnet >nul 2>nul && set "DOTNET_CMD=dotnet"
)

if not defined DOTNET_CMD (
    echo ============================================================
    echo [ERROR] .NET SDK belum terpasang di sistem ini.
    echo.
    echo Silakan install .NET 10 SDK dari:
    echo   https://dotnet.microsoft.com/download
    echo.
    echo Setelah install selesai, jalankan kembali file ini.
    echo ============================================================
    pause
    exit /b 1
)

for /f "tokens=*" %%v in ('""!DOTNET_CMD!" --version 2>nul"') do (
    echo [OK] .NET Versi: %%v
    goto :dotnet_found
)
:dotnet_found
echo.

REM ============================================================
REM 3. Verifikasi file project
REM ============================================================
if not exist "%APP_DIR%\RyotaQC.App.csproj" (
    echo [ERROR] File project tidak ditemukan di:
    echo        %APP_DIR%\RyotaQC.App.csproj
    pause
    exit /b 1
)

REM ============================================================
REM 4. Restore dependensi NuGet
REM ============================================================
echo [1/2] Mengunduh dependensi (Restore NuGet)...
"!DOTNET_CMD!" restore "%APP_DIR%\RyotaQC.App.csproj"
if errorlevel 1 (
    echo.
    echo [ERROR] Restore dependensi gagal. Pastikan laptop terhubung internet.
    pause
    exit /b 1
)
echo [OK] Dependensi siap.
echo.

REM ============================================================
REM 5. Build aplikasi (Release)
REM ============================================================
echo [2/2] Membangun aplikasi RyotaQC V5 (Harap tunggu 1-2 menit)...
"!DOTNET_CMD!" build "%APP_DIR%\RyotaQC.App.csproj" -c Release --nologo
if errorlevel 1 (
    echo.
    echo ============================================================
    echo [ERROR] Build gagal! Periksa pesan error di atas.
    echo ============================================================
    pause
    exit /b 1
)

echo [OK] Build selesai.
echo.

REM ============================================================
REM 6. Cari EXE hasil build & launch
REM ============================================================
set "FOUND_EXE="
if exist "%APP_DIR%\bin\Release\net10.0-windows\win-x64\%EXE_NAME%" (
    set "FOUND_EXE=%APP_DIR%\bin\Release\net10.0-windows\win-x64\%EXE_NAME%"
) else if exist "%APP_DIR%\bin\Release\net10.0-windows\%EXE_NAME%" (
    set "FOUND_EXE=%APP_DIR%\bin\Release\net10.0-windows\%EXE_NAME%"
) else (
    for /r "%APP_DIR%\bin\Release" %%f in (%EXE_NAME%) do (
        if not defined FOUND_EXE if exist "%%~f" set "FOUND_EXE=%%~f"
    )
)

if defined FOUND_EXE (
    echo ============================================================
    echo [SUKSES] RyotaQC V5 berhasil dibangun!
    echo ============================================================
    echo.
    echo Membuka RyotaQC V5 Desktop...
    timeout /t 3 >nul
    start "" "%FOUND_EXE%"
    exit /b 0
)

echo [ERROR] File %EXE_NAME% tidak ditemukan setelah build.
pause
exit /b 1