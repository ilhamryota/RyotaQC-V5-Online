@echo off
setlocal EnableDelayedExpansion
title RyotaQC V5 Auto-Installer and Launcher

cd /d "%~dp0"
set "APP_DIR=%~dp0src\RyotaQC.App"
set "PUBLISH_DIR=%~dp0publish\win-x64"

echo ============================================================
echo   RYOTA QC V5 - ESTERAONE ERP
echo   Auto-Installer and Launcher
echo ============================================================
echo.

REM 1. Cek apakah .exe sudah pernah dibangun sebelumnya
set "FOUND_EXE="
if exist "%APP_DIR%in\Release
et10.0-windows\RyotaQC.App.exe" set "FOUND_EXE=%APP_DIR%in\Release
et10.0-windows\RyotaQC.App.exe"
if not defined FOUND_EXE if exist "%APP_DIR%in\Debug
et10.0-windows\RyotaQC.App.exe" set "FOUND_EXE=%APP_DIR%in\Debug
et10.0-windows\RyotaQC.App.exe"
if not defined FOUND_EXE if exist "%PUBLISH_DIR%\RyotaQC.App.exe" set "FOUND_EXE=%PUBLISH_DIR%\RyotaQC.App.exe"
if not defined FOUND_EXE (
    for /r "%~dp0" %%f in (RyotaQC.App.exe) do (
        if exist "%%~f" set "FOUND_EXE=%%~f"
    )
)

if defined FOUND_EXE (
    echo [OK] Aplikasi RyotaQC V5 sudah siap!
    timeout /t 3 >nul
    exit /b 0
)

REM 2. Cari executable dotnet (cek default folder dan PATH)
set "DOTNET_CMD="
if exist "C:\Program Files\dotnet\dotnet.exe" set "DOTNET_CMD=C:\Program Files\dotnet\dotnet.exe"
if not defined DOTNET_CMD if exist "%ProgramFiles%\dotnet\dotnet.exe" set "DOTNET_CMD=%ProgramFiles%\dotnet\dotnet.exe"
if not defined DOTNET_CMD if exist "C:\Program Files (x86)\dotnet\dotnet.exe" set "DOTNET_CMD=C:\Program Files (x86)\dotnet\dotnet.exe"
if not defined DOTNET_CMD (
    where dotnet >nul 2>nul
)

if not defined DOTNET_CMD (
    echo [ERROR] .NET SDK belum terpasang.
    pause
    exit /b 1
)

for /f "tokens=*" %%v in ('"!DOTNET_CMD!" --version 2^>nul') do echo [OK] .NET Versi: %%v
echo.

if not exist "%APP_DIR%\RyotaQC.App.csproj" (
    echo [ERROR] File project tidak ditemukan di: %APP_DIR%
    pause
    exit /b 1
)

echo [1/2] Mengunduh dependensi (Restore NuGet)...
    echo.
    echo [ERROR] Restore dependensi gagal. Pastikan laptop terhubung internet.
    pause
    exit /b 1
)
echo [OK] Dependensi siap.
echo.

echo [2/2] Membangun aplikasi RyotaQC V5 (Harap tunggu 1-2 menit)...
    echo.
    echo ============================================================
    echo [ERROR] Build gagal! Periksa pesan error merah di atas.
    echo ============================================================
    echo.
    pause
    exit /b 1
)

REM Cari file exe hasil build
set "FOUND_EXE="
if exist "%APP_DIR%in\Release
et10.0-windows\RyotaQC.App.exe" set "FOUND_EXE=%APP_DIR%in\Release
et10.0-windows\RyotaQC.App.exe"
if not defined FOUND_EXE if exist "%APP_DIR%in\Debug
et10.0-windows\RyotaQC.App.exe" set "FOUND_EXE=%APP_DIR%in\Debug
et10.0-windows\RyotaQC.App.exe"
if not defined FOUND_EXE if exist "%PUBLISH_DIR%\RyotaQC.App.exe" set "FOUND_EXE=%PUBLISH_DIR%\RyotaQC.App.exe"
if not defined FOUND_EXE (
    for /r "%~dp0" %%f in (RyotaQC.App.exe) do (
        if exist "%%~f" set "FOUND_EXE=%%~f"
    )
)

if defined FOUND_EXE (
    echo.
    echo ============================================================
    echo [SUKSES] RyotaQC V5 berhasil dibangun!
    echo ============================================================
    echo.
    echo Membuka RyotaQC V5 Desktop...
    timeout /t 3 >nul
    exit /b 0
) else (
    echo.
    echo [ERROR] File RyotaQC.App.exe tidak ditemukan setelah build.
    pause
    exit /b 1
)
