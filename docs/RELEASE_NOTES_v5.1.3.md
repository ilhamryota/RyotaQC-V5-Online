# RyotaQC V5.1.3 — Sinkronisasi Penuh dengan ERP Produksi

Patch integrasi QC dengan ERP EsteraOne (menu Quality Control). Fokus rilis ini:
memastikan **Via No PO**, **Via Kode Unit**, **Tester**, dan **Log Hasil QC**
selalu sinkron dengan data **ERP produksi** (bukan lagi database uji/DEV).

## Via No PO & Via Kode Unit Kembali Menemukan Unit

- Sebelumnya pencarian membaca database **DEV**, sehingga PO/unit yang **sudah ada
  di ERP produksi** tidak ditemukan ("tidak ada ditemukan").
- Kini pencarian memakai **data produksi**, sama seperti Kanban QC di ERP.
- Daftar unit pada **Via No PO** mengikuti kolom Kanban **Antrean** dan
  **Tes Running**, jadi unit yang menunggu QC langsung muncul.

## Tester QC Realtime dari Kanban ERP

- Daftar tester diambil langsung dari ERP (dikelola role **Gudang HO** di Kanban
  Quality Control) secara **realtime**.
- Saat ditambah/diubah/dihapus di Kanban, aplikasi otomatis memakai daftar
  terbaru. Daftar juga **dimuat ulang tepat saat dropdown Tester dibuka**, tanpa
  perlu menutup jendela.

## Log Hasil QC Sinkron

- Setiap **Kirim Hasil QC ke ERP** kini tersimpan ke data produksi dan langsung
  tampil sebagai kartu di **Kanban QC** (lengkap dengan nama Tester).
- Tombol **Log Hasil QC** menampilkan riwayat QC unit tersebut (maks **7** log
  terbaru) dari data produksi.

## Catatan Pembaruan

- Versi aplikasi naik dari **5.1.2** menjadi **5.1.3**.
- Update otomatis berjalan seperti biasa: aplikasi memeriksa rilis terbaru,
  mengunduh installer, memverifikasi (ukuran + SHA-256), lalu memasangnya.
