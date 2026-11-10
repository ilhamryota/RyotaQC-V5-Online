# RyotaQC V5.1.4 — Flexible QC Save (Retur/Service) & Unblock Inbound PO

Pembaruan integrasi RyotaQC V5 Online dengan EsteraOne ERP (menu Quality Control).

## 1. Simpan Fleksibel ke ERP (Mendukung Retur, Servis, & QC Parsial)
- Menghapus pembatasan wajib lengkap (*mandatory completeness hard-gate*) saat mengirim hasil QC ke ERP.
- Teknisi kini dapat menyimpan hasil pemeriksaan ke ERP meskipun belum semua dari 12 parameter diuji, terutama saat menemukan indikasi **RETUR** atau **SERVICE** di awal/tengah pengetesan.
- Nilai parameter yang belum sempat diuji otomatis terkirim sebagai status netral (`"-"`), sehingga tidak merusak tampilan Kanban ERP.
- Skor Fisik bersifat fleksibel dan tidak lagi memblokir pengiriman jika teknisi belum menginput skor.
- Status hasil QC di ERP (`READY`, `SERVIS`, `RETUR`, atau `QC_PROCESS`) tersimpan akurat sesuai pilihan status dropdown teknisi (`LULUS`, `SERVICE`, `RETUR`, `RUNNING_TEST`).

## 2. Unblock Pencarian Unit Berdasarkan No PO
- Unit pada PO yang barangnya sudah diterima di gudang (status `receiptStatus = 'Selesai'` dan telah berada di antrean Kanban QC) kini **100% dapat dicari dan dipilih**, tanpa terblokir status pembayaran Finance (`Menunggu_Pembayaran`).
- Pencarian nomor PO dan kode unit kini mendukung *case-insensitive* (`PO-0021`, `po-0021`, dsb.).
- Auto-extract nama Brand jika relasi brand di database bernilai kosong.

## 3. Sinkronisasi Realtime Tester QC
- Dropdown Tester QC di jendela *Hubungkan ke ERP* otomatis memuat ulang (*refresh*) daftar tester aktif langsung dari ERP saat dropdown dibuka (`DropDownOpened`).
- Setiap penambahan/perubahan nama tester oleh role Gudang HO di Kanban ERP langsung tampil secara realtime tanpa perlu restart aplikasi.

---
**Versi:** 5.1.4  
**Tanggal:** 10 November 2026

