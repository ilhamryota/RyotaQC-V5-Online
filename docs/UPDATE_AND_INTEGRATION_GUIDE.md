# RyotaQC V5.0.7 - Panduan Update & Integrasi ERP

Dokumen ini menjelaskan:
1. Cara update aplikasi (auto-update & manual)
2. Troubleshooting masalah auto-update
3. Cara pakai integrasi QC → ERP (end-to-end)

> **PENTING - pengguna yang masih di v5.0.2 / v5.0.3 / v5.0.4 / v5.0.5**
>
> Installer **v5.0.3, v5.0.4, dan v5.0.5 berisi aplikasi versi LAMA** di dalamnya
> (penjelasan di bagian 2). Karena itu auto-update dari versi-versi tersebut
> **tidak akan pernah berhasil** — paket terpasang, tapi aplikasi tetap versi lama.
>
> **Lakukan update manual sekali** ke v5.0.7 (bagian 1.B). Setelah berada di
> v5.0.6 atau v5.0.7, auto-update ke versi berikutnya berjalan normal.

---

## 1. CARA UPDATE APLIKASI

### A. Auto-Update (dari dalam aplikasi)

Saat app dibuka, app akan cek GitHub Releases. Jika ada versi baru:

1. Dialog **"Update Wajib Tersedia!"** muncul
2. Klik tombol **"Update Sekarang"** (atau "Coba Lagi" jika gagal)
3. App mengunduh paket installer (~334 MB) — ukuran file dan **SHA-256**-nya
   dicocokkan dengan `SHA256SUMS.txt` di rilis tersebut
4. Setelah unduh selesai + lolos verifikasi, app menutup otomatis
5. Installer berjalan (silent, minta UAC), install, lalu app restart
6. Setelah app terbuka, **cek nomor versi** di halaman utama

**Estimasi waktu unduh (334 MB):**
| Koneksi | Perkiraan waktu |
|---------|-----------------|
| 10 Mbps | ~4.5 menit |
| 20 Mbps | ~2.2 menit |
| 50 Mbps | ~53 detik |

> Jika setelah restart nomor versi **tidak berubah**, kirimkan file
> `%LOCALAPPDATA%\RyotaQC\logs\update.log` ke pengembang. Sejak v5.0.6 setiap
> langkah update dicatat di situ (termasuk hasil verifikasi checksum dan
> apakah versi benar-benar berubah setelah restart).
>
> Sejak **v5.0.7** hal ini juga tampil di layar: kalau update sebelumnya ternyata
> tidak mengubah versi terpasang, saat app dibuka berikutnya muncul peringatan
> **"Update Tidak Terpasang"** yang menyebut versi sekarang + lokasi file log.
> Jadi kegagalan update tidak lagi lewat begitu saja.

### B. Update Manual (WAJIB untuk semua user di bawah v5.0.6)

1. Buka https://github.com/ilhamryota/RyotaQC-V5-Online/releases/latest
2. Unduh `RyotaQC-V5-Setup-x64-v5.0.7.exe` (~334 MB) **dan** `SHA256SUMS.txt`
3. **Verifikasi unduhan** (penting — unduhan terpotong adalah penyebab paling
   umum pesan "This app can't run on your PC"):

   ```powershell
   Get-FileHash .\RyotaQC-V5-Setup-x64-v5.0.7.exe -Algorithm SHA256
   # bandingkan hasilnya dengan baris di SHA256SUMS.txt
   ```

4. Tutup aplikasi RyotaQC yang sedang berjalan
5. Jalankan installer (klik kanan → **Run as administrator**), tunggu selesai
6. Buka app → nomor versi harus **v5.0.7**

---

## 2. TROUBLESHOOTING AUTO-UPDATE

### Masalah: "update selesai" tapi aplikasi masih versi lama

**Penyebab sebenarnya (ditemukan 22 Okt 2026) — installer berisi aplikasi versi lama:**

- Di `installer\RyotaQC.Setup.iss`, baris `#define AppSourceDir "..\publish"`
  **tidak dibungkus `#ifndef`**. Inno Setup mengizinkan `#define` di script
  **menimpa** nilai `/DAppSourceDir=...` dari command line.
- Akibatnya script build mengira folder sumbernya `release\staging\...`, tapi
  compiler tetap memakai `..\publish` — folder build lokal yang isinya
  **aplikasi v5.0.3** (file `publish\RyotaQC.App.exe` = FileVersion `5.0.3.0`).
- Jadi **Setup EXE v5.0.4 dan v5.0.5 berisi aplikasi 5.0.3**. Bukti kuat: ukuran
  ketiga installer nyaris identik (350.012.951 / 350.012.950 / 350.013.031 byte)
  padahal fiturnya jauh berbeda — hanya string versi installer yang berubah.
- User "berhasil" update, UAC muncul, installer selesai — tapi yang terpasang
  tetap aplikasi v5.0.3. Karena updater v5.0.3 hanya bisa ZIP dan tidak pernah
  minta elevasi, copy ke `C:\Program Files\...` gagal diam-diam.

**Sudah diperbaiki di v5.0.6:**

| Perbaikan | Lokasi |
|-----------|--------|
| Semua `#define` sumber/output dibungkus `#ifndef` sehingga `/D...` dari command line dipakai | `installer\RyotaQC.Setup.iss` |
| Default `AppSourceDir` diarahkan ke `release\staging\RyotaQC-V5` (hasil pipeline), bukan `publish\` | `installer\RyotaQC.Setup.iss` |
| Build **gagal** jika versi payload != versi rilis | `scripts\02-build-setup.ps1` |
| ZIP update + `SHA256SUMS.txt` diverifikasi isinya saat build | `scripts\04-build-update-zip.ps1` |
| Installer mengecek versi `RyotaQC.App.exe` yang terpasang dan memberi peringatan tegas jika berbeda dari versi paket | `[Code] VerifyInstalledPayloadVersion` |
| App menulis `update.log` + marker `pending-update.json`, lalu setelah restart mencatat apakah versi benar-benar berubah | `AppUpdateService.cs` |

### Masalah: "This app can't run on your PC"

**Ini bukan masalah arsitektur PC.** Binary kita: Setup = 32-bit (Inno Setup,
jalan di semua Windows), aplikasi = 64-bit (butuh Windows 64-bit).

**Penyebab: file `.exe` tidak utuh** (unduhan terputus / copy lewat drive yang
memotong file). File yang terpotong masih punya PE header yang valid — di mesin
developer pernah ditemukan salinan `RyotaQC-V5-Setup-x64-v5.0.3.exe` berukuran
**0,91 MB** padahal seharusnya **333,8 MB** — dan Windows menolaknya dengan pesan
persis ini.

**Cara memastikan & solusi:**
1. Cek ukuran file harus sama dengan yang tertera di `SHA256SUMS.txt` rilis.
2. Cocokkan hash: `Get-FileHash <file> -Algorithm SHA256`.
3. Unduh ulang dengan koneksi stabil (beberapa ISP membatasi/throttle unduhan
   besar dari GitHub — coba jaringan lain bila ukuran tetap tidak cocok).
4. Mulai v5.0.6 updater menolak menerapkan file yang bukan PE valid atau yang
   SHA-256-nya tidak cocok, jadi kerusakan ini tidak lagi bisa "lolos".

### Masalah: mau lihat apa yang terjadi saat update

| File | Isi |
|------|-----|
| `%LOCALAPPDATA%\RyotaQC\logs\update.log` | Jejak lengkap proses update (ukuran, checksum, peluncuran installer, hasil update sebelumnya) |
| `%LOCALAPPDATA%\RyotaQC\logs\pending-update.json` | Marker update yang sedang berjalan (dihapus setelah restart) |
| `%LOCALAPPDATA%\RyotaQC\logs\app.log` | Log aplikasi umum |
| `%TEMP%\RyotaQC_Update_*` | Folder sementara saat update. Sejak v5.0.6 otomatis dibersihkan saat app dibuka |

Installer manual bisa dijalankan dengan log detail:

```powershell
.\RyotaQC-V5-Setup-x64-v5.0.6.exe /SILENT /LOG="C:\temp\ryotaqc-install.log"
```

### Error: "Gagal mengunduh update. Periksa koneksi internet lalu coba lagi."

**Penyebab di versi lama:**
- v5.0.2: filter asset hanya menerima `.zip`, asset `.exe` diabaikan
- v5.0.2: timeout 20 detik terlalu pendek untuk file 300+ MB
- v5.0.3: setelah unduh, `xcopy` ke `C:\Program Files` dijalankan **tanpa
  elevasi** dan **tanpa cek error** (`>nul`) → gagal diam-diam

**Solusi:**
1. Pastikan rilis punya asset `.exe` (Setup) dan `.zip` (fallback)
2. Jika masih gagal, cek:
   - **Firewall/Proxy** — apakah `objects.githubusercontent.com` diblokir?
   - **Antivirus** — apakah memblokir proses updater?
   - **Koneksi** — apakah kecepatan cukup (≥ 10 Mbps)?
3. Fallback: **update manual** (bagian 1.B)

**Sejak v5.0.4 ke atas:** timeout unduhan 30 menit, installer `.exe` didukung
langsung, pesan error lebih detail (HTTP code / ukuran yang diterima).

---

## 3. INTEGRASI QC → ERP (End-to-End)

### Arsitektur

```
┌─────────────────────┐      HTTP :8005       ┌──────────────────┐
│  RyotaQC V5 App     │ ────────────────────► │  QC Hub (Node)   │
│  (laptop teknisi)   │                       │  Contabo VPS     │
└─────────────────────┘                       └────────┬─────────┘
                                                       │ SQL
                                                       ▼
                                              ┌──────────────────┐
                                              │  PostgreSQL      │
                                              │  DEV :5439       │
                                              │  esteraone_dev   │
                                              └──────────────────┘
                                                       ▲
                                                       │
                                              ┌────────┴─────────┐
                                              │  Web ERP         │
                                              │  dev.esteraone   │
                                              │  Menu QC Kanban  │
                                              └──────────────────┘
```

### Endpoint QC Hub (port 8005)

| Method | Path | Fungsi |
|--------|------|--------|
| GET | `/api/v1/health` | Cek status hub + target DB |
| GET | `/api/v1/switcher/target-db` | Cek DB aktif (dev/prod) |
| GET | `/api/v1/qc/po/:poNumber/units` | List unit per PO (filter LUNAS/DP) |
| GET | `/api/v1/qc/testers` | List tester tersimpan |
| POST | `/api/v1/qc/testers` | Daftarkan tester baru |
| POST | `/api/v1/qc/submit` | Kirim hasil QC + pindah Kanban |

### Alur Pemakaian

**STEP 1 — Web ERP (https://dev.esteraone.com/dashboard)**

1. **Manajemen PO** → Buat PO (supplier, item, qty)
2. **Finance** → Approval Pembayaran → status `LUNAS` atau `DP`
3. **Gudang** → Receive PO → unit otomatis masuk `QC_QUEUE`
4. Buka **Menu Quality Control** → unit muncul di kolom **ANTREAN**

**STEP 2 — Aplikasi RyotaQC V5.0.5**

Saat menu **Quality Control** dibuka, muncul popup **ESTERAONE ERP SYNC** (2 kolom):

1. **Kolom 1 — Via No PO:**
   - Isi **No PO** (misal `PO-0003`) → klik **Cari**
   - Dropdown **PILIH KODE UNIT EST** muncul (brand Lenovo diprioritaskan atas)
   - Pilih unit yang sesuai laptop yang sedang di-QC → klik **Hubungkan Unit Ini**
2. **Kolom 2 — Via Kode Unit:**
   - Isi **Kode Unit** (misal `EST-2610-0003-01` / serial / pra-QC code) → klik **Cari**
   - Detail unit muncul (pra-QC code, serial, produk, brand, PO, status kanban)
   - Klik **Hubungkan Kode Unit**
3. **Tester:** pilih dari dropdown, atau isi nama baru + klik **Tester+**
4. Bisa juga klik **Lanjut Tanpa ERP** (QC tetap jalan, hasil disimpan lokal)

Setelah terhubung, panel kecil **ESTERAONE ERP — UNIT TERHUBUNG** di halaman QC menampilkan unit terikat + tombol **Kirim ke ERP**.

5. Jalankan semua test QC di panel kanan. Beberapa test memakai popup khusus:
   - **Storage Health** → popup input angka → popup verdict **OK / "-" Optional / X Fail** (70–100% = PASS, <70% = FAIL)
   - **Battery Health** → popup **Battery Health 1 (wajib)** + **Battery Health 2 (opsional, hanya jika 2 battery)**, auto-isi dari health terbaca → popup verdict. Hasil tampil mis. `100% OK` atau `100% / 70% OK`
   - **Battery Report** → popup hasil + popup durasi (jam & menit) → mengisi **Estimasi Batt.lifetime** di ERP
   - **Fail (X)** → diminta catatan alasan, disimpan ke checklist QC test tersebut dengan status FAIL
   - **Touchscreen / Backlight / Fingerprint** tanpa hasil → dikirim sebagai `"-"` (opsional)
6. Pilih **STATUS QC**:
   | Pilihan | Kanban Tujuan | Keterangan |
   |---------|---------------|------------|
   | `LULUS` | SELESAI | Unit lolos QC |
   | `SERVICE` | SERVIS | Perlu perbaikan |
   | `RETUR` | RETUR | Masuk manajemen retur |
   | `RUNNING_TEST` | TES RUNNING | Masih diuji |
7. Klik **Kirim ke ERP**

**STEP 3 — Verifikasi di Web ERP**

- Refresh **Menu Quality Control**
- Kartu unit sudah pindah kolom sesuai STATUS QC
- Klik kartu → tab **Checklist QC** terisi lengkap: spec hardware, tiap test (PASS/FAIL/ISSUE/"-"),
  numeric (hddSsdHealth, batteryHealth, batteryHealth2), Estimasi Batt.lifetime, dan catatan fail per test

### Data yang Dikirim ke ERP

**Spec hardware (otomatis dari WMI):**
- Processor (CPU)
- RAM
- Storage HDD (terpisah, hanya HDD/SATA/M.2 SATA)
- Storage SSD (terpisah, NVMe/M.2 NVMe/SSD)
- VGA — support dual: `Intel HD 530 + RTX 3090`
- Display — resolusi + refresh rate (+ label Touchscreen jika touch)

**Checklist test (qcData.tests) — 14 key sesuai TEST_LIST ERP:**

| Test App | Key ERP | Wajib |
|----------|---------|-------|
| LCD Test | `lcdTest` | ✅ |
| Storage Health | `storageTest` | ✅ |
| Battery Health | `batteryTest` | ✅ |
| Keyboard | `keyboard` | ✅ |
| Audio | `audioSpeaker` | ✅ |
| Benchmark | `stressTest` | ✅ |
| Camera | `camera` | ✅ |
| Port | `port` | ✅ |
| Touchpad | `touchpad` | ✅ |
| Wifi | `wifiTest` | ✅ |
| Mic | `microphone` | ✅ |
| Touchscreen | `touchscreen` | opsional (`"-"` jika tidak ada) |
| Backlight | `backlight` | opsional (`"-"` jika tidak ada) |
| Fingerprint | `fingerprint` | opsional (selalu `"-"`) |

**Numeric tests (qcData.numericTests):**

| Nilai | Key ERP | Sumber |
|-------|---------|--------|
| Health storage | `hddSsdHealth` | input Storage Health |
| Health battery 1 | `batteryHealth` | input Battery Health 1 |
| Health battery 2 | `batteryHealth2` | input Battery Health 2 (jika ada) |
| Estimasi battery | `batteryLifetime` | durasi Battery Report (menit) |

**Status per test:**
- `PASS` (hijau) → OK
- `"-"` (minus) → opsional / tidak diuji
- `FAIL` (merah) → Fail + catatan alasan
- `ISSUE` → perlu perhatian

**Auto-kalkulasi:**
- `hppFinal` = hppInitial + repairCost
- `sellPriceOutlet` = hppFinal × 1.3

---

## 4. STATUS VERIFIKASI (22 Okt 2026)

### Sudah Teruji LIVE di DEV

| Test | Hasil |
|------|-------|
| QC Hub restart | ✅ uptime reset |
| Target DB = DEV | ✅ port 5439, esteraone_dev |
| PO-0003 units lookup | ✅ return unit Lenovo X395 |
| Register tester | ✅ tersimpan di testers.json |
| Submit LULUS → SELESAI | ✅ qcStatusId benar |
| Submit SERVICE → SERVIS | ✅ |
| Submit RETUR → RETUR | ✅ |
| Full 14 test key ke qcData.tests | ✅ semua key tersimpan benar |
| Numeric hddSsdHealth/batteryHealth/batteryHealth2/batteryLifetime | ✅ |
| overallStatus FAIL saat Mic FAIL → Kanban SERVIS | ✅ |
| UTF-8 (em-dash, titik tengah) di spec & notes | ✅ tidak rusak |
| Spec dual storage/VGA | ✅ "AMD Ryzen 7 / 16 GB / SSD M.2 NVMe — 238 GB" |
| HPP auto-calc | ✅ 4.5M → sellPrice 5.85M |
| Release v5.0.5 + asset Setup EXE & Update ZIP | ✅ ter-upload, latest |
| Fix auto-update relaunch versi lama | ⚠️ v5.0.5 belum cukup (lihat baris berikut) |
| **Akar masalah ditemukan**: installer v5.0.3/v5.0.4/v5.0.5 berisi aplikasi **v5.0.3** | ✅ terbukti (`publish\RyotaQC.App.exe` = FileVersion 5.0.3.0 + ukuran installer nyaris identik) |
| `.iss` `#define` tanpa `#ifndef` menimpa `/DAppSourceDir` | ✅ diperbaiki di v5.0.6 |
| Guard versi payload di build (`02-build-setup.ps1`) | ✅ gagal build jika payload ≠ versi rilis |
| Guard versi payload di installer (`VerifyInstalledPayloadVersion`) | ✅ peringatan tegas jika tidak cocok |
| ZIP update + `SHA256SUMS.txt` diverifikasi saat build (`04-build-update-zip.ps1`) | ✅ |
| `update.log` + `pending-update.json` (jejak update) | ✅ |
| Verifikasi ukuran + PE + SHA-256 sebelum installer dijalankan | ✅ |
| **Release v5.0.6** (installer berisi aplikasi v5.0.6) | ✅ ter-upload & terverifikasi (333.696.999 byte, 689/689 file staging ikut ter-paket, sampel byte GitHub = lokal) |
| Rilis lama v5.0.3 / v5.0.4 / v5.0.5 diberi peringatan di catatan rilisnya | ✅ |
| `SHA256SUMS.txt` dipublikasikan + cocok dengan file aslinya | ✅ |
| Badge versi menu utama dibaca live dari assembly yang berjalan | ✅ selalu menampilkan versi yang benar-benar terpasang |
| **v5.0.7**: peringatan "Update Tidak Terpasang" jika versi tidak berubah setelah update | ✅ ditambahkan (sebelumnya gagal tanpa jejak di layar) |
| **Release v5.0.7** (postur sama dengan v5.0.6 + peringatan update gagal) | ⏳ sedang dibangun & di-upload |

---

## 5. MASIH PENDING

| # | Task | Blocking |
|---|------|----------|
| 1 | Patch `warehouse.controller.ts` (auto-fill bank supplier) | Perlu rebuild BE |
| 2 | Restart ERP BE | `sudo -u admin /usr/bin/pm2 restart esteraone-green-be` |
| 3 | Test dari app v5.0.6 terinstall (manual update dulu untuk user < v5.0.6) | Perlu install di laptop teknisi |
| 4 | **Uji alur auto-update end-to-end: v5.0.6 → v5.0.7** (buka app → "Update Sekarang" → setujui UAC → cek badge menu utama = v5.0.7) | Butuh laptop teknisi yang sudah v5.0.6 |

### Cara Restart Service di VPS

```bash
# QC Hub
sudo -n -u admin /usr/bin/pm2 restart ryota-qc-be --update-env

# ERP Backend
sudo -n -u admin /usr/bin/pm2 restart esteraone-green-be --update-env

# ERP Frontend
sudo -n -u admin /usr/bin/pm2 restart esteraone-green-fe --update-env
```

Verifikasi:
```bash
curl -s http://127.0.0.1:8005/api/v1/health
curl -s http://127.0.0.1:8005/api/v1/switcher/target-db
```

---

## 6. REFERENSI CEPAT

**Repositori:** https://github.com/ilhamryota/RyotaQC-V5-Online
**Release terbaru (v5.0.7):** https://github.com/ilhamryota/RyotaQC-V5-Online/releases/tag/v5.0.7
**Release v5.0.6:** https://github.com/ilhamryota/RyotaQC-V5-Online/releases/tag/v5.0.6
**Release lama (jangan dipakai untuk install baru):** v5.0.3 / v5.0.4 / v5.0.5
**Web ERP DEV:** https://dev.esteraone.com/dashboard
**QC Hub:** http://109.123.239.163:8005

**Akun ADMIN ERP:** `ilhamryota28@gmail.com`
