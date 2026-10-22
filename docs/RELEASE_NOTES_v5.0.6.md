## RyotaQC V5 v5.0.6 — Perbaikan Kritis Auto-Update

### ⚠️ WAJIB UPDATE MANUAL SEKALI (user di v5.0.2 – v5.0.5)

Setup EXE **v5.0.3, v5.0.4, dan v5.0.5 berisi aplikasi versi LAMA (v5.0.3)** di
dalamnya. Jadi walau proses update tampak "berhasil" (UAC muncul, installer
selesai), aplikasi yang terpasang tetap versi lama.

**Penyebabnya:** di `installer\RyotaQC.Setup.iss`, baris
`#define AppSourceDir "..\publish"` tidak dibungkus `#ifndef`. Inno Setup
mengizinkan `#define` di dalam script **menimpa** nilai `/DAppSourceDir=...`
dari command line, sehingga compiler memakai folder `publish\` lokal (sisa build
v5.0.3) dan mengabaikan payload asli hasil pipeline. Bukti: ukuran ketiga
installer nyaris identik (350.012.951 / 350.012.950 / 350.013.031 byte) padahal
perubahan kodenya besar.

**Cara update:**
1. Unduh **`RyotaQC-V5-Setup-x64-v5.0.6.exe`** dari rilis ini
2. Verifikasi dengan `SHA256SUMS.txt` (lihat bagian bawah)
3. Tutup aplikasi RyotaQC, jalankan installer sebagai **Administrator**
4. Buka aplikasi — nomor versi harus **v5.0.6**

Setelah berada di v5.0.6, auto-update ke versi berikutnya berjalan normal.

### Perbaikan

- **Installer kini benar-benar berisi aplikasi v5.0.6**
  - semua `#define` sumber/output di `.iss` dibungkus `#ifndef`
  - default sumber diarahkan ke `release\staging\RyotaQC-V5` (hasil pipeline)
  - `scripts\02-build-setup.ps1` **gagal build** kalau versi payload != versi rilis
- **Installer memverifikasi payload-nya sendiri**: setelah instalasi, versi
  `RyotaQC.App.exe` yang terpasang dibandingkan dengan versi paket; kalau beda
  muncul pesan peringatan tegas — supaya kasus "installer isi versi lama" tidak
  pernah lagi lolos tanpa ketahuan
- **Verifikasi unduhan sebelum installer dijalankan**: ukuran + PE header +
  SHA-256 dicocokkan dengan `SHA256SUMS.txt` rilis. File terpotong ditolak —
  ini penyebab utama pesan *"This app can't run on your PC"*
- **Jejak diagnostik**: `%LOCALAPPDATA%\RyotaQC\logs\update.log` mencatat seluruh
  proses update (ukuran, hasil checksum, peluncuran installer, hasil update
  sebelumnya), plus marker `pending-update.json` untuk memastikan versi benar-
  benar berubah setelah restart
- **Bersih-bersih otomatis** folder `%TEMP%\RyotaQC_Update_*` yang sebelumnya
  menumpuk (~334 MB per update, ditemukan 3 folder sisa = ~1 GB)
- **Repositori bersih**: `RyotaQC.App.exe` (87 MB, build v5.0.3) dan ZIP lama
  yang sempat ter-commit di root repo dihapus, plus aturan `.gitignore` baru
- QC: "Running Test" dihapus dari Hasil QC; checklist ERP berisi 14 test

### Aset rilis ini

| File | Fungsi |
|------|--------|
| `RyotaQC-V5-Setup-x64-v5.0.6.exe` | Installer (install baru & update manual) |
| `RyotaQC-V5-Update-v5.0.6.zip` | Paket auto-update (fallback) |
| `SHA256SUMS.txt` | Checksum untuk memverifikasi unduhan |

Verifikasi unduhan:

```powershell
Get-FileHash .\RyotaQC-V5-Setup-x64-v5.0.6.exe -Algorithm SHA256
# cocokkan dengan isi SHA256SUMS.txt
```

Panduan lengkap: `docs\UPDATE_AND_INTEGRATION_GUIDE.md`
