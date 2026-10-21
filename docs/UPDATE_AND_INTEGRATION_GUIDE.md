# RyotaQC V5.0.3 - Panduan Update & Integrasi ERP

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
2. Unduh `RyotaQC-V5-Setup-x64-v5.0.3.exe` (334 MB)
3. Tutup aplikasi RyotaQC yang sedang berjalan
4. Jalankan installer **as Administrator**
5. Ikuti wizard sampai selesai

---

## 2. TROUBLESHOOTING AUTO-UPDATE

### Error: "Gagal mengunduh update. Periksa koneksi internet lalu coba lagi."

**Penyebab di v5.0.2 (kode lama):**
- Filter asset hanya menerima `.zip` — asset `.exe` diabaikan
- Timeout 20 detik terlalu pendek untuk file besar

**Solusi:**
1. Pastikan release v5.0.3 punya asset `.zip` (sudah disiapkan: `RyotaQC-V5-Update-v5.0.3.zip`)
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

**STEP 2 — Aplikasi RyotaQC V5.0.3**

1. Buka app → menu **Quality Control**
2. Panel **ESTERAONE ERP SYNC**:
   - Isi **No PO** (misal `PO-0003`) → klik **Cari**
   - Dropdown **PILIH KODE UNIT (EST)** muncul (brand Lenovo diprioritaskan atas)
   - Pilih unit yang sesuai laptop yang sedang di-QC
3. **Tester**:
   - Pilih dari dropdown (jika sudah terdaftar), ATAU
   - Isi nama di kotak kanan → klik **Tester+** untuk daftar baru
4. Klik **Hubungkan** → app terikat ke unit (status hijau)
5. Jalankan semua test QC di panel kanan:
   - LCD Test, Storage Health, Battery Health, Keyboard, Audio, Benchmark, Camera, Touchscreen, dll
6. Pilih **STATUS QC**:
   | Pilihan | Kanban Tujuan | Keterangan |
   |---------|---------------|------------|
   | `LULUS` | SELESAI | Unit lolos QC |
   | `SERVICE` | SERVIS | Perlu perbaikan |
   | `RETUR` | RETUR | Masuk manajemen retur |
   | `RUNNING_TEST` | TES RUNNING | Masih diuji |
7. Klik **Hasil QC** → popup → klik **Kirim ke ERP**

**STEP 3 — Verifikasi di Web ERP**

- Refresh **Menu Quality Control**
- Kartu unit sudah pindah kolom sesuai STATUS QC
- Klik kartu → spec hardware, battery health, hasil test, tester terisi otomatis

### Data yang Dikirim ke ERP

**Spec hardware (otomatis dari WMI):**
- Processor (CPU)
- RAM
- Storage HDD (terpisah, jika ada)
- Storage SSD (terpisah)
- VGA — support dual: `Intel HD 530 + RTX 3090`
- Display — resolusi + refresh rate, contoh `1920 x 1080 @60Hz FHD` (+ label Touchscreen jika touch)

**Hasil test per parameter:**
- `PASS` (hijau) → OK
- `SKIP` (minus) → -
- `FAIL` (merah) → Fail + catatan

**Auto-kalkulasi:**
- `hppFinal` = hppInitial + repairCost
- `sellPriceOutlet` = hppFinal × 1.3

---

## 4. STATUS VERIFIKASI (21 Okt 2026)

### Sudah Teruji LIVE di DEV

| Test | Hasil |
|------|-------|
| QC Hub restart | ✅ uptime reset |
| Target DB = DEV | ✅ port 5439, esteraone_dev |
| PO-0003 units lookup | ✅ return 4 unit Lenovo X395 |
| Register tester | ✅ tersimpan di testers.json |
| Submit LULUS → SELESAI | ✅ qcStatusId benar |
| Submit SERVICE → SERVIS | ✅ |
| Submit RETUR → RETUR | ✅ |
| Submit RUNNING_TEST → TES RUNNING | ✅ |
| Spec dual storage/VGA | ✅ "Intel i5-8265U / 8GB DDR4 / 256GB NVMe" |
| HPP auto-calc | ✅ 4.5M → sellPrice 5.85M |

### Data Test di DEV DB

| Unit | Status | Kanban |
|------|--------|--------|
| EST-2610-0003-02 | READY | SELESAI |
| EST-2610-0003-03 | QC_PROCESS | TES RUNNING |
| EST-2610-0003-04 | SERVICE | SERVIS |
| EST-2610-0003-05 | RETUR | RETUR |

---

## 5. MASIH PENDING

| # | Task | Blocking |
|---|------|----------|
| 1 | Patch `warehouse.controller.ts` (auto-fill bank supplier) | Perlu rebuild BE |
| 2 | Restart ERP BE | `sudo -u admin /usr/bin/pm2 restart esteraone-green-be` |
| 3 | Test dari app v5.0.3 terinstall | Perlu install di laptop |

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
**Release v5.0.3:** https://github.com/ilhamryota/RyotaQC-V5-Online/releases/tag/v5.0.3
**Web ERP DEV:** https://dev.esteraone.com/dashboard
**QC Hub:** http://109.123.239.163:8005

**Akun ADMIN ERP:** `ilhamryota28@gmail.com`
