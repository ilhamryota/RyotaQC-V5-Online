# RyotaQC V5.1.0 — Popup Bisa Digeser, Shortcut Baru, Dropdown Bersih

## Popup QC

- **Semua popup QC bisa digeser (drag)** dengan menahan area kosong popup,
  supaya bisa dipindah kalau menutupi bagian unit yang sedang dites.
- **Ukuran popup otomatis menyesuaikan resolusi / aspect ratio layar** dan
  selalu dibatasi di dalam area kerja layar, sehingga tidak ada bagian yang
  terpotong atau turun keluar layar.
- Drag tetap aman: menahan tombol, ComboBox, atau input teks **tidak**
  memindahkan popup.

## Shortcut Hasil QC

- **Space = OK**
- **`-` (minus) = Optional**
- **Esc = Fail**

Tombol Enter tidak lagi dipakai untuk OK.

## Popup Hubungkan ke ERP

- **Dropdown Kode Unit** sekarang menampilkan tulisan yang benar:
  `Kode Unit - Brand - Model (posisi kanban)` — contoh
  `RQC-0001 - Lenovo - ThinkPad T14 (Antrean)`. Sebelumnya yang muncul hanya
  nama tipe data internal (`RyotaQC.App.Services.PoUnitItem`).
- Posisi kanban yang ditampilkan: **Antrean, Tes Running, Selesai, Service,
  Retur** (urutan kanban tidak diubah).
- **Dropdown Tester** juga menampilkan nama tester dengan benar (sebelumnya
  `RyotaQC.App.Services.TesterInfo`).
- **Tombol tempat sampah di samping nama tester** untuk menghapus tester.
  Tester tidak dihapus permanen: di ERP status user diubah menjadi
  **INACTIVE** dan namanya dihapus dari daftar tester, sehingga tidak muncul
  lagi di dropdown mana pun.
- **Kolom Catatan Tambahan ERP** hanya berisi catatan manual/retur. Template
  otomatis (Kode Unit, Tanggal Test, Model Laptop) dan ringkasan hasil QC tidak
  lagi dikirim ke kolom tersebut.

## Tampilan Aplikasi

- Badge **"? SELECTED"** saat navigasi keyboard **dihapus**.
- Shortcut **"RyotaQC V5"** di **Wallpaper > Shortcut Desktop** **dihapus**
  dari daftar (shortcut lain tetap ada).

## Catatan Pembaruan

- Update otomatis berjalan seperti biasa: aplikasi memeriksa rilis terbaru,
  mengunduh installer, memverifikasi (ukuran + SHA-256), lalu memasangnya
  setelah aplikasi ditutup.
- Versi aplikasi naik dari **5.0.9** menjadi **5.1.0**.
