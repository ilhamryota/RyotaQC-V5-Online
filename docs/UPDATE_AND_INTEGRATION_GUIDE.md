# RyotaQC V5.0.5 - Panduan Update & Integrasi ERP

Dokumen ini menjelaskan:
1. Cara update aplikasi (auto-update & manual)
2. Troubleshooting masalah auto-update
3. Cara pakai integrasi QC → ERP (end-to-end)

---

## 1. CARA UPDATE APLIKASI

### A. Auto-Update (dari dalam aplikasi)

Saat app dibuka, app akan cek GitHub Releases. Jika ada versi baru:

1. Dialog **"Update Wajib Tersedia!"** muncul
2. Klik tombol **"Update Sekarang"** (atau "Coba Lagi" jika gagal)
3. App mengunduh paket installer (116–334 MB)
4. Setelah unduh selesai, app menutup otomatis
5. Installer berjalan (silent), install, lalu app restart

**Estimasi waktu unduh:**
| Koneksi | 116 MB (ZIP) | 334 MB (EXE installer) |
|---------|--------------|------------------------|
| 10 Mbps | ~1.5 menit | ~4.5 menit |
| 20 Mbps | ~47 detik | ~2.2 menit |
| 50 Mbps | ~19 detik | ~53 detik |

### B. Update Manual (jika auto-update gagal)

1. Buka https://github.com/ilhamryota/RyotaQC-V5-Online/releases/latest
2. Unduh `RyotaQC-V5-Setup-x64-v5.0.5.exe` (334 MB)
3. Tutup aplikasi RyotaQC yang sedang berjalan
4. Jalankan installer **as Administrator**
5. Ikuti wizard sampai selesai

---

## 2. TROUBLESHOOTING AUTO-UPDATE

### Masalah: setelah update, aplikasi yang terbuka masih versi lama

**Penyebab (kode lama v5.0.3/v5.0.4):**
- Updater menjalankan installer dengan flag `/RESTARTAPPLICATIONS`
- Flag ini menimpa setting installer -> Inno Setup membuka ulang **file exe lama**
  sebelum file baru selesai diganti -> aplikasi kembali ke versi lama

**Sudah diperbaiki di v5.0.5:**
- Updater tidak lagi mengirim `/CLOSEAPPLICATIONS` / `/RESTARTAPPLICATIONS`
- App keluar penuh setelah menjalankan installer
- Installer `RestartApplications=no` + `PrepareToInstall` menutup paksa instance lama
- Relaunch versi baru lewat **satu jalur** (entri `[Run]`)

> Catatan: kalau Anda sekarang di **v5.0.3/v5.0.4**, update ke **v5.0.5** akan benar
> karena installer v5.0.5 sudah mengabaikan flag lama dari versi sebelumnya.

### Error: "Gagal mengunduh update. Periksa koneksi internet lalu coba lagi."

**Penyebab di v5.0.2 (kode lama):**
- Filter asset hanya menerima `.zip` — asset `.exe` diabaikan
- Timeout 20 detik terlalu pendek untuk file besar

**Solusi:**
1. Pastikan release v5.0.5 punya asset `.zip` (sudah disiapkan: `RyotaQC-V5-Update-v5.0.5.zip`)
2. Jika masih gagal, cek:
   - **Firewall/Proxy** — apakah `objects.githubusercontent.com` diblokir?
   - **Antivirus** — apakah memblokir proses updater?
   - **Koneksi** — apakah kecepatan cukup (≥ 10 Mbps)?
3. Fallback: **update manual** (bagian 1.B)

**Setelah update ke v5.0.4+ ke atas:**
- Timeout unduhan jadi 30 menit
- Support installer `.exe` langsung
- Pesan error lebih detail (menampilkan HTTP code / timeout)

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
| Fix auto-update relaunch versi lama | ✅ diperbaiki di v5.0.5 |

---

## 5. MASIH PENDING

| # | Task | Blocking |
|---|------|----------|
| 1 | Patch `warehouse.controller.ts` (auto-fill bank supplier) | Perlu rebuild BE |
| 2 | Restart ERP BE | `sudo -u admin /usr/bin/pm2 restart esteraone-green-be` |
| 3 | Test dari app v5.0.5 terinstall | Perlu install di laptop teknisi |

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
**Release v5.0.5:** https://github.com/ilhamryota/RyotaQC-V5-Online/releases/tag/v5.0.5
**Web ERP DEV:** https://dev.esteraone.com/dashboard
**QC Hub:** http://109.123.239.163:8005

**Akun ADMIN ERP:** `ilhamryota28@gmail.com`
