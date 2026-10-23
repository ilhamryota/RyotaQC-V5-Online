# RyotaQC V5.1.1 — Tes LCD/Touchscreen/Touchpad Full Screen & Print BATT LIFETIME dalam Jam

Patch perbaikan untuk aplikasi QC.

## Tes LCD, Touchscreen, dan Touchpad Kembali Full Screen

- Ketiga jendela tes (**LCD Test**, **Touchscreen Test**, **Touchpad Test**)
  kembali tampil **full screen** menutupi seluruh layar.
- Sebelumnya jendela tes ikut terkena perilaku geser/clamp popup, sehingga:
  - ukuran jendela mengecil sekitar 24 px dari tepi layar, dan
  - begitu area tes diklik/ditekan, jendela **kembali ke ukuran normal**
    (tidak lagi full screen). Ini terutama mengganggu LCD Test yang memang
    harus menutupi seluruh layar untuk memeriksa dead pixel.
- Sekarang jendela full screen **dikecualikan** dari perilaku geser/clamp:
  ukuran dan posisinya tidak pernah diubah, dan ukuran maksimumnya tidak
  dibatasi, sehingga tetap full screen pada resolusi dan aspect ratio apa pun.

## Print Data QC — EST. BATT LIFETIME dalam Jam

- Pada halaman print Data QC, nilai **EST. BATT LIFETIME** sekarang ditampilkan
  dalam **Jam & Mnt**, sesuai input pada checklist QC (Jam & Mnt).
  Contoh: `352 Mnt` sekarang tercetak **`5 Jam 52 Mnt`**.
- Jika menitnya nol, hanya bagian jam yang dicetak (contoh: `6 Jam`).
- Nilai di database tetap disimpan dalam satuan menit — hanya tampilan print
  yang berubah, jadi data ERP dan aplikasi QC tetap kompatibel.

## Catatan Pembaruan

- Versi aplikasi naik dari **5.1.0** menjadi **5.1.1**.
- Update otomatis berjalan seperti biasa: aplikasi memeriksa rilis terbaru,
  mengunduh installer, memverifikasi (ukuran + SHA-256), lalu memasangnya
  setelah aplikasi ditutup.
