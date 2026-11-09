# RyotaQC V5.1.2 — Tester QC Terpusat dari ERP & Log Hasil QC

Patch integrasi QC dengan ERP EsteraOne (menu Quality Control).

## Tester QC Kini Terpusat di ERP

- Daftar **Tester QC diambil langsung dari ERP** (role **Gudang HO**) secara
  **real-time**. Jika di ERP ditambah, diubah, atau dihapus, aplikasi otomatis
  memakai daftar terbaru — tanpa perlu update aplikasi.
- **Form "Daftar Tester Baru" di aplikasi dihapus**, begitu juga tombol hapus
  tester. Tester **hanya boleh** ditambah/dihapus di ERP oleh role Gudang HO.
- Percobaan menambah/menghapus tester dari aplikasi akan ditolak server.

## Tester Tidak Wajib di Aplikasi

- Label berubah menjadi **"Tester (opsional)"**. QC tetap bisa disimpan
  meskipun tester dikosongkan.

## Log Hasil QC di Aplikasi

- Setiap **Kirim Hasil QC ke ERP** (dari aplikasi maupun dari web kanban) kini
  otomatis **tersimpan sebagai Log Hasil QC**.
- Tersedia tombol **"Log Hasil QC"** di jendela "Hubungkan ke ERP" untuk
  **melihat riwayat QC** unit tersebut (via kode unit atau kode PO).
- Riwayat menampilkan hingga **7 log terbaru**; log paling lama otomatis
  dihapus saat bertambah lebih dari 7.

## Catatan Pembaruan

- Versi aplikasi naik dari **5.1.1** menjadi **5.1.2**.
- Update otomatis berjalan seperti biasa: aplikasi memeriksa rilis terbaru,
  mengunduh installer, memverifikasi (ukuran + SHA-256), lalu memasangnya
  setelah aplikasi ditutup.
