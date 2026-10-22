# RyotaQC V5.0.9 — Popup ERP Baru, QC Wajib Lengkap, Tester dari ERP

## Yang baru

- **Popup "Hubungkan ke ERP" didesain ulang** mengikuti mockup: kartu navy
  gelap, ikon globe, dua tab (Via No PO / Via Kode Unit), input PO + tombol Cari,
  dropdown kode unit, kotak hint, tombol biru besar, dan footer tester.
- **Latar belakang diblur** saat popup ERP terbuka sehingga fokus jelas ke
  popup.
- **Teks pilihan dropdown dibuat putih/terbaca** ("Pilih Kode Unit" dan
  Tester).
- **Tester dari ERP**: daftar tester ditarik dari user ERP (role HEAD_GUDANG,
  sama seperti dropdown tester di ERP). Tester terpilih otomatis mengisi kolom
  **Tester QC** pada kanban.
- **Daftar Tester Baru**: tombol **Add New Tester** menyimpan tester baru ke
  database ERP, menampilkan konfirmasi, lalu tester langsung muncul di dropdown.
- **Tester wajib dipilih** sebelum menghubungkan unit, baik Via No PO maupun
  Via Kode Unit.

## Hasil QC

- Semua popup hasil diseragamkan: **OK** (hijau), **Optional** (abu-abu),
  **Fail** (merah + catatan).
- Shortcut: **Enter = OK**, **Space = Optional**, **Esc = Fail**.
- **Touchscreen**: ditanya lebih dulu "Apakah unit ini memiliki Touchscreen?".
  Jika **Tidak**, otomatis dicatat **Optional (-)** tanpa menjalankan tool test.
- **Benchmark Status** langsung tampil di badge kanan (hijau OK / abu-abu
  Optional / merah Fail) dan tersimpan di **Catatan Otomatis**.
- **Skor Fisik (%)** manual di atas Status QC, tersimpan sebagai
  `Fisik = 94%` dan mengisi **Skor Fisik kemulusan** di ERP.
- **Tipe / Model Laptop** otomatis terisi dari System Information setelah
  terhubung ERP, tetap bisa diedit, dan mengisi **Tipe Laptop** di ERP.
- **Kelengkapan** bebas diisi dan mengisi **Info Unit & Spesifikasi >
  Identitas & Spesifikasi Hardware > Kelengkapan** di ERP.

## Validasi wajib

Parameter wajib: Skor Fisik, Tipe/Model, Kelengkapan, LCD, Storage Health,
Battery Health, Battery Report, Keyboard, Audio, Port, Touchpad, WiFi, Mic,
Benchmark, Camera. Backlight dan Touchscreen opsional.

Jika ada yang belum diisi/ditest, pengiriman **ditolak** dengan popup
**"GAGAL Kirim ke ERP"** yang menyebutkan item yang kurang.

## Perbaikan storage

- **NVMe / SSD / M.2 NVMe** sekarang masuk kolom **Storage SSD**.
- **HDD / SATA 3 / SATA III** masuk kolom **Storage HDD**.
- Akar masalah: Windows melaporkan NVMe sebagai `Fixed hard disk media`,
  sehingga pengecekan "hard disk" mendahului pengecekan SSD. Urutan sudah
  dibalik (SSD/NVMe dicek lebih dulu).

## Catatan pemasangan

- In-place upgrade ke `C:\Program Files\RyotaQC\V5`.
- Auto-update dari v5.0.6+ tetap berjalan; pengguna v5.0.5 ke bawah perlu
  installer manual sekali.
