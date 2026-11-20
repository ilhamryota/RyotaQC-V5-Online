# RyotaQC V5 EsteraOne v5.1.5 — Dropdown Unit, Mute SFX Navigasi, Log QC Lengkap & Integrasi EsteraOne ERP

Pembaruan integrasi RyotaQC V5 Online dengan EsteraOne ERP (Green Pod & Kanban Quality Control).

## 1. Pembaruan Dropdown Pilih Kode Unit via No PO
- Format teks dropdown unit diperbarui menjadi terstandar, rapi, dan informatif:  
  `Kode Unit - Nama Item - Tujuan Pengiriman Outlet ( Status di kanban )`  
  *Contoh:* `PO-SUP-004-191126-002 - Lenovo Thinkpad T450 i5-5 8/256-BGR - Bestlaptop Bogor ( QC antrean )`
- Menghilangkan redundansi penulisan nama merk ganda pada nama item.
- Menampilkan tujuan outlet pengiriman unit secara realtime dari data purchase order ERP.

## 2. Icon & Fitur Mute Audio SFX Gerakan Panah
- Menambahkan icon toggle mute audio SFX di title bar aplikasi dan top bar dashboard home menu.
- Mematikan suara SFX navigasi pergerakan tombol panah kiri/kanan (`Left Right.mp3`) tanpa memengaruhi atau mematikan fungsi pengujian audio/speaker pada modul Quality Control.

## 3. Tampilan Log Hasil QC Lengkap & Detail
- Modal jendela **Log Hasil QC** diperbarui menampilkan seluruh ringkasan pengujian unit:
  - **Identitas & Spesifikasi Hardware**: Merk/Brand, Tipe Laptop, Serial Number, Prosesor (CPU), RAM, Storage SSD, Storage HDD, VGA/Grafis, Layar (LCD/Display), dan Kondisi Fisik (%).
  - **Hasil Pengujian Fungsi & Running Test**: LCD Test, Kinerja SSD/HDD (%), Remaining Lifetime, Lifetime Writes (TBW), Battery Test (%), Battery Life Time / Running Test Jam, Keyboard, Backlight, Camera, Microphone, Fingerprint, Wifi/Bluetooth, Audio/Speaker, Port, Touchpad, Touchscreen, dan Stress Test (Benchmark).
  - **Catatan Kanban QC**: Menampilkan catatan teknisi/kondisi fisik unit yang tersimpan di sistem ERP.
  - **Riwayat Log Sebelumnya**: Menampilkan riwayat FIFO hingga 7 log pengetesan terdahulu lengkap dengan tester, sumber, waktu, dan status kanban.

## 4. Penataan Teks Unit Terhubung ERP
- Panel "ESTERAONE ERP — UNIT TERHUBUNG" pada halaman Quality Control ditata ulang secara terstruktur:  
  `Unit : <Nama & Model Laptop>`  
  `Kode Unit : <Kode Internal Unit>`  
  `Status Saat ini : <Status Kanban>`  
  `Tujuan Pengiriman : <Outlet Tujuan>`

---
**Versi:** 5.1.5  
**Tanggal:** 20 November 2026  
**Rilis:** RyotaQC V5 EsteraOne v5.1.5
