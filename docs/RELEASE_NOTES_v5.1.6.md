# RyotaQC V5 EsteraOne v5.1.6 — Auto-Download Update, Dukungan Scan QR Link Web & Perbaikan Sinkronisasi ERP

Pembaruan RyotaQC V5 Online dan Ryota QC Hub agar kompatibel dengan label QR terbaru serta proses pembaruan aplikasi otomatis.

## 1. Auto-Download Update Saat Versi Baru Terdeteksi
- Saat versi baru tersedia di GitHub Releases, jendela update otomatis mengunduh file installer beserta verifikasi checksum SHA-256 tanpa perlu klik tombol download terlebih dahulu.
- Setelah unduhan terverifikasi, teknisi tinggal memilih **Install Sekarang** atau **Nanti**.

## 2. Dukungan Scan QR Format Link Web ERP
- Pencarian unit melalui kode QR maupun input Pra-QC sekarang langsung mengekstrak kode unit dari URL penuh (`/scan/unit/...` maupun query `?code=...`), baik di aplikasi desktop (`RyotaQC V5`) maupun di `Ryota QC Hub`.

## 3. Perbaikan Keamanan & Sinkronisasi QC Hub -> ERP
- **Proteksi Unit SOLD**: Mencegah penimpaan data QC pada unit yang sudah berstatus `SOLD`.
- **Menjaga Metadata Returan & Harga Outlet**: Metadata returan (`isReturan`, `returnDecision`, dll.) dan `sellPriceOutlet` yang sudah diset tidak terhapus saat QC ulang.
- **Prioritas Unit Aktif**: Jika ditemukan Serial Number ganda, sistem memprioritaskan unit di antrean `QC_QUEUE` / `QC_PROCESS` atau unit aktif dibandingkan unit lama yang sudah terjual.
- **Resolusi Tester & Log History**: Mendukung tester dari tabel `qc_tester` ERP tanpa bentrok foreign key serta mencatat `fileName` pada `qc_history`.

---
**Versi:** 5.1.6  
**Tanggal:** 27 November 2026  
**Rilis:** RyotaQC V5 EsteraOne v5.1.6
