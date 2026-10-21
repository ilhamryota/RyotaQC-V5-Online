@echo off
chcp 65001 >nul
title RyotaQC V5 - Update Otomatis
setlocal EnableDelayedExpansion

REM ============================================================
REM   RyotaQC V5 - Script Update Otomatis
REM
REM   Gunakan script ini jika auto-update di aplikasi gagal.
REM   Script akan: tutup app -> unduh installer terbaru dari
REM   GitHub -> jalankan installer sebagai Administrator.
REM ============================================================

set "GH_REPO=ilhamryota/RyotaQC-V5-Online"
set "EXE_NAME=RyotaQC.App.exe"

echo ============================================================
echo   RyotaQC V5 - Update Otomatis
echo ============================================================
echo.

REM --- Minta elevasi Administrator lebih awal ---
net session >nul 2>&1
if errorlevel 1 (
    echo Meminta izin Administrator...
    powershell -NoProfile -ExecutionPolicy Bypass -Command "Start-Process -FilePath '%~f0' -Verb RunAs"
    exit /b
)

REM --- Ambil release terbaru dari GitHub API ---
echo [1/5] Mencari versi terbaru di GitHub...
set "TAG="
set "ASSET_URL="
set "ASSET_SIZE="

for /f "usebackq tokens=*" %%L in (`powershell -NoProfile -ExecutionPolicy Bypass -Command ^
  "$ErrorActionPreference='Stop';" ^
  "try {" ^
  "  $r = Invoke-RestMethod -Uri 'https://api.github.com/repos/%GH_REPO%/releases/latest' -Headers @{ 'User-Agent'='RyotaQC-Updater'; 'Accept'='application/vnd.github+json' } -TimeoutSec 30;" ^
  "  $a = $r.assets ^| Where-Object { $_.name -like '*Setup*.exe' } ^| Select-Object -First 1;" ^
  "  if (-not $a) { $a = $r.assets ^| Where-Object { $_.name -like '*.exe' } ^| Select-Object -First 1 };" ^
  "  Write-Output ($r.tag_name + '|' + $a.browser_download_url + '|' + $a.size)" ^
  "} catch { Write-Output 'ERROR|' + $_.Exception.Message + '|0' }"`) do (
    for /f "tokens=1,2,3 delims=|" %%A in ("%%L") do (
        set "TAG=%%A"
        set "ASSET_URL=%%B"
        set "ASSET_SIZE=%%C"
    )
)

if "!TAG!"=="" (
    echo.
    echo [ERROR] Tidak dapat mengambil informasi rilis dari GitHub.
    echo         Periksa koneksi internet.
    echo.
    echo Alternatif manual:
    echo   https://github.com/%GH_REPO%/releases/latest
    pause
    exit /b 1
)

if "!TAG!"=="ERROR" (
    echo.
    echo [ERROR] !ASSET_URL!
    echo.
    pause
    exit /b 1
)

echo       Versi terbaru: !TAG!
echo       Ukuran installer: !ASSET_SIZE! bytes

REM --- Cek versi terinstall dari registry ---
set "INSTALLED="
for /f "usebackq tokens=2,*" %%A in (`reg query "HKLM\SOFTWARE\Microsoft\Windows\CurrentVersion\Uninstall\RyotaQC V5_is1" /v DisplayVersion 2^>nul ^| findstr DisplayVersion`) do set "INSTALLED=%%B"
if "!INSTALLED!"=="" (
    for /f "usebackq tokens=2,*" %%A in (`reg query "HKLM\SOFTWARE\Microsoft\Windows\CurrentVersion\Uninstall\*" /v DisplayName 2^>nul ^| findstr /i "RyotaQC"`) do set "INSTALLED_FOUND=1"
)

echo.
echo       Versi terinstall: !INSTALLED!
echo       Versi target    : !TAG!

if "!INSTALLED!"=="!TAG!" (
    echo.
    echo       Versi sudah sama dengan yang terbaru.
    choice /C YN /M "       Tetap install ulang"
    if errorlevel 2 exit /b 0
)

REM --- Tutup aplikasi ---
echo.
echo [2/5] Menutup aplikasi RyotaQC...
taskkill /F /IM "%EXE_NAME%" >nul 2>&1
if errorlevel 1 (echo       Tidak ada aplikasi yang berjalan.) else (echo       Aplikasi ditutup.)
timeout /t 2 /nobreak >nul

REM --- Unduh installer ---
set "INSTALLER=%TEMP%\RyotaQC-Setup-%TAG%.exe"
echo.
echo [3/5] Mengunduh installer !TAG!...
echo       Tujuan: %INSTALLER%
echo.

if exist "%INSTALLER%" del /F /Q "%INSTALLER%"

powershell -NoProfile -ExecutionPolicy Bypass -Command ^
  "$ProgressPreference='Continue';" ^
  "$url='!ASSET_URL!'; $out='%INSTALLER%';" ^
  "$expect=[long]!ASSET_SIZE!;" ^
  "$maxTry=5; $ok=$false;" ^
  "for ($i=1; $i -le $maxTry; $i++) {" ^
  "  try {" ^
  "    Write-Host \"       Percobaan $i dari $maxTry...\";" ^
  "    Invoke-WebRequest -Uri $url -OutFile $out -UseBasicParsing -TimeoutSec 7200;" ^
  "    $len = (Get-Item $out).Length;" ^
  "    if ($expect -gt 0 -and $len -lt $expect) { throw \"Terputus: $len dari $expect bytes\" }" ^
  "    Write-Host \"       Selesai: $len bytes\";" ^
  "    $ok=$true; break" ^
  "  } catch {" ^
  "    Write-Host \"       Gagal: $($_.Exception.Message)\";" ^
  "    if (Test-Path $out) { Remove-Item $out -Force -ErrorAction SilentlyContinue }" ^
  "    Start-Sleep -Seconds 5" ^
  "  }" ^
  "}" ^
  "if (-not $ok) { exit 1 }"

if errorlevel 1 (
    echo.
    echo [ERROR] Gagal mengunduh installer setelah 5 percobaan.
    echo.
    echo Solusi manual:
    echo   1. Buka: https://github.com/%GH_REPO%/releases/latest
    echo   2. Unduh file installer (.exe)
    echo   3. Jalankan sebagai Administrator
    echo.
    pause
    exit /b 1
)

REM --- Verifikasi ukuran ---
for %%A in ("%INSTALLER%") do set "SIZE=%%~zA"
echo.
echo       Terunduh: !SIZE! bytes
if !SIZE! LSS 100000000 (
    echo.
    echo [ERROR] Ukuran file terlalu kecil - unduhan kemungkinan rusak.
    pause
    exit /b 1
)

REM --- Jalankan installer ---
echo.
echo [4/5] Menjalankan installer...
echo       (Installer akan menutup app dan membuka versi baru)
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
echo [5/5] Selesai. Installer sedang berjalan.
echo       Aplikasi RyotaQC !TAG! akan terbuka otomatis.
echo.
echo ============================================================
echo   UPDATE DILUNCURKAN - !TAG!
echo ============================================================
timeout /t 10 /nobreak >nul
exit /b 0
