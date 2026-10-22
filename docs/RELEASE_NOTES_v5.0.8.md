# RyotaQC V5.0.8 — Update Otomatis Diperbaiki Total & Koneksi ERP di Menu QC

## Perbaikan utama

- **Update otomatis diperbaiki total.** Installer tidak lagi dijalankan dari
  folder `%TEMP%` (yang bisa diblokir antivirus/Defender sehingga installer
  tidak pernah benar-benar jalan), melainkan dari folder permanen
  `%LOCALAPPDATA%\RyotaQC\updates\`.
- **Hasil instalasi sekarang diverifikasi.** Setelah installer selesai, versi
  `RyotaQC.App.exe` yang benar-benar ada di disk dibaca ulang dan dibandingkan
  dengan versi target. Jika belum berubah, aplikasi menampilkan pesan gagal
  beserta kode keluar installer dan path file log — tidak lagi "diam-diam"
  tetap di versi lama.
- **Alur update bertahap (unduh dulu, install kemudian).** Klik Update →
  progress unduh → "Download update selesai, ingin update sekarang atau nanti?"
  - **Nanti**: aplikasi tetap berjalan di versi sekarang dan semua fitur tetap
    bisa dipakai. Saat aplikasi dibuka lagi, tawaran yang sama muncul kembali.
  - **Update sekarang**: aplikasi menampilkan proses "Menginstall aplikasi...",
    memasang versi terbaru, lalu memberi info bahwa update berhasil dan
    menanyakan **"Restart aplikasi sekarang? Ya/Tidak"**.
    - **Ya**: aplikasi ditutup dan dibuka otomatis.
    - **Tidak**: lanjut memakai aplikasi; saat ditutup dan dibuka lagi,
      aplikasi sudah memakai versi terbaru.
- **Tampilan popup update baru** mengikuti desain terbaru: kartu gelap,
  ikon sinkronisasi, info pembaruan, perbandingan Versi Sekarang / Versi
  Terbaru, daftar Detail Perubahan, persentase unduhan, progress bar tipis, dan
  tombol biru "Update Sekarang" / "Install Sekarang".

## Koneksi ERP di menu Quality Control

- **Popup "Hubungkan ke ERP" muncul setiap kali masuk menu Quality Control.**
- Dua cara koneksi, hanya satu yang dipakai:
  - **Via No PO**: isi No PO → pilih kode unit → **Hubungkan ke ERP**.
  - **Via Kode Unit**: cari kode unit langsung (tanpa No PO) → **Hubungkan
    ke ERP**.
- Status berjalan sampai **Terhubung**, lalu popup menutup dan menu Quality
  Control bisa dipakai.
- **Badge koneksi** tampil di layar Quality Control:
  - mode No PO: `PO PO-0003 · EST-2610-0003-04`
  - mode kode unit: `KODE UNIT EST-2610-0003-04`
- **Tombol "Tanpa ERP" tetap tersedia** agar Quality Control bisa dipakai tanpa
  menghubungkan unit.
- **Dropdown kode unit dibuat ringkas** sehingga tidak lagi memakan ruang
  popup.

## Perbaikan lain

- Judul jendela aplikasi tidak lagi menampilkan versi lama yang di-hardcode.
- Peningkatan stabilitas sistem dan optimasi performa aplikasi.

## Catatan pemasangan

- Instalasi di tempat (in-place upgrade) ke `C:\Program Files\RyotaQC\V5`.
- Jika update otomatis gagal karena kebijakan keamanan di komputer teknisi,
  installer bisa dijalankan manual dari file yang sudah diunduh pada
  `%LOCALAPPDATA%\RyotaQC\updates\`.
