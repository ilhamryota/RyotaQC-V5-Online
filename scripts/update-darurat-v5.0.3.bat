@echo off
chcp 65001 >nul
title RyotaQC V5 - Update Darurat ke v5.0.3
setlocal EnableDelayedExpansion

echo ============================================================
echo   RyotaQC V5 - UPDATE DARURAT ke v5.0.3
echo ============================================================
echo.
echo Script ini akan:
echo   1. Menutup aplikasi RyotaQC yang sedang berjalan
echo   2. Mengunduh installer v5.0.3 dari GitHub (334 MB)
echo   3. Menjalankan installer sebagai Administrator
echo   4. Installer akan membuka aplikasi versi terbaru
echo.
echo CATATAN: Anda perlu koneksi internet dan akan muncul
echo          prompt UAC (Setujui) saat installer dijalankan.
echo.
pause

REM --- Cek hak Administrator ---
net session >nul 2>&1
if errorlevel 1 (
    echo.
    echo Meminta izin Administrator...
    powershell -NoProfile -ExecutionPolicy Bypass -Command "Start-Process -FilePath '%~f0' -Verb RunAs"
    exit /b
)

echo.
echo [1/4] Menutup aplikasi RyotaQC...
taskkill /F /IM RyotaQC.App.exe >nul 2>&1
if errorlevel 1 (
    echo       Tidak ada aplikasi yang berjalan.
) else (
    echo       Aplikasi ditutup.
)
timeout /t 2 /nobreak >nul

set "INSTALLER=%TEMP%\RyotaQC-V5-Setup-x64-v5.0.3.exe"
set "URL=https://github.com/ilhamryota/RyotaQC-V5-Online/releases/download/v5.0.3/RyotaQC-V5-Setup-x64-v5.0.3.exe"

echo.
echo [2/4] Mengunduh installer v5.0.3...
echo       URL: %URL%
echo       Tujuan: %INSTALLER%
echo.

if exist "%INSTALLER%" del /F /Q "%INSTALLER%"

REM --- Unduh via PowerShell dengan progress + retry ---
powershell -NoProfile -ExecutionPolicy Bypass -Command ^
  "$ProgressPreference='Continue';" ^
  "$url='%URL%'; $out='%INSTALLER%';" ^
  "$maxTry=3; $ok=$false;" ^
  "for ($i=1; $i -le $maxTry; $i++) {" ^
  "  try {" ^
  "    Write-Host \"       Percobaan $i dari $maxTry...\";" ^
  "    Invoke-WebRequest -Uri $url -OutFile $out -UseBasicParsing -TimeoutSec 3600;" ^
  "    $ok=$true; break" ^
  "  } catch {" ^
  "    Write-Host \"       Gagal: $($_.Exception.Message)\";" ^
  "    Start-Sleep -Seconds 3" ^
  "  }" ^
  "}" ^
  "if (-not $ok) { exit 1 }"

if errorlevel 1 (
    echo.
    echo [ERROR] Gagal mengunduh installer setelah 3 percobaan.
    echo.
    echo Solusi manual:
    echo   1. Buka: https://github.com/ilhamryota/RyotaQC-V5-Online/releases/tag/v5.0.3
    echo   2. Unduh: RyotaQC-V5-Setup-x64-v5.0.3.exe
    echo   3. Jalankan installer sebagai Administrator
    echo.
    pause
    exit /b 1
)

REM --- Verifikasi ukuran file ---
for %%A in ("%INSTALLER%") do set "SIZE=%%~zA"
echo.
echo       Ukuran terunduh: %SIZE% bytes
if %SIZE% LSS 300000000 (
    echo.
    echo [ERROR] Ukuran file terlalu kecil, kemungkinan unduhan terputus.
    echo         Diharapkan sekitar 350.000.000 bytes.
    echo.
    echo Silakan jalankan script ini lagi.
    pause
    exit /b 1
)
echo       Ukuran OK.

echo.
echo [3/4] Menjalankan installer sebagai Administrator...
echo       (Setujui prompt UAC jika muncul)
echo.
"%INSTALLER%" /SILENT /NORESTART /CLOSEAPPLICATIONS /RESTARTAPPLICATIONS

if errorlevel 1 (
    echo.
    echo [ERROR] Installer gagal dijalankan.
    echo         Coba jalankan manual: %INSTALLER%
    pause
    exit /b 1
)

echo.
echo [4/4] Installer sedang berjalan...
echo       Aplikasi RyotaQC v5.0.3 akan terbuka otomatis setelah selesai.
echo.
echo ============================================================
echo   SELESAI - Installer sudah diluncurkan
echo ============================================================
timeout /t 8 /nobreak >nul
exit /b 0
