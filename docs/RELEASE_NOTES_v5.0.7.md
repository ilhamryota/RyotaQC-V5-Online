## RyotaQC V5 v5.0.7 — Verifikasi Alur Auto-Update

Rilis ini **fungsionalitasnya sama dengan v5.0.6**, dengan satu tambahan kecil.
Tujuannya: membuktikan alur update otomatis (download → verifikasi → install →
restart) berjalan penuh, dan memastikan nomor versi di **menu utama** benar-benar
berubah setelah update.

### Cara update ke v5.0.7

**Kalau Anda sudah di v5.0.6** — cukup pakai update dari dalam aplikasi:

1. Buka aplikasi → muncul dialog **"Update Wajib Tersedia!"**
2. Klik **"Update Sekarang"**
3. Tunggu unduhan (~318 MB) + verifikasi otomatis
4. Setujui prompt **UAC** (Run as administrator)
5. Aplikasi menutup sendiri → installer berjalan silent → **aplikasi terbuka
   kembali sendiri** dalam versi **v5.0.7**
6. Badge versi di menu utama harus menampilkan **v5.0.7**

**Kalau Anda masih di v5.0.2 – v5.0.5** — wajib install manual **sekali** dulu
(instruksi di rilis **v5.0.6**), karena updater di versi-versi itu rusak.

### Yang baru di v5.0.7

- **Update yang gagal tidak lagi diam-diam.** Kalau setelah proses update versi
  aplikasi ternyata belum berubah, saat dibuka berikutnya muncul peringatan yang
  menyebut versi sekarang + lokasi file log. Sebelumnya kejadian ini tidak
  terlihat sama sekali — inilah yang membuat kasus "sudah update tapi masih versi
  lama" sulit dilacak.
- Badge versi di menu utama sudah dibaca langsung dari assembly aplikasi yang
  sedang berjalan, jadi selalu menampilkan versi yang benar-benar terpasang.

### Cara memverifikasi hasil update

| Cek | Yang diharapkan |
|-----|-----------------|
| Menu utama (badge versi) | **v5.0.7** |
| `%LOCALAPPDATA%\RyotaQC\logs\update.log` | baris `previous update applied successfully: 5.0.6 -> 5.0.7` |
| `%TEMP%\RyotaQC_Update_*` | tidak menumpuk (dibersihkan otomatis saat app dibuka) |
| Buka aplikasi lagi | tidak ada prompt update lagi (sudah versi terbaru) |

### Kalau update gagal

Kirimkan `%LOCALAPPDATA%\RyotaQC\logs\update.log` ke pengembang. Log itu mencatat
ukuran unduhan, hasil verifikasi SHA-256, peluncuran installer, dan apakah versi
benar-benar berubah setelah restart.

### Aset rilis ini

| File | Fungsi |
|------|--------|
| `RyotaQC-V5-Setup-x64-v5.0.7.exe` | Installer (install baru & update manual) |
| `RyotaQC-V5-Update-v5.0.7.zip` | Paket auto-update (fallback) |
| `SHA256SUMS.txt` | Checksum untuk memverifikasi unduhan |

```powershell
Get-FileHash .\RyotaQC-V5-Setup-x64-v5.0.7.exe -Algorithm SHA256
# cocokkan dengan isi SHA256SUMS.txt
```

Panduan lengkap: `docs\UPDATE_AND_INTEGRATION_GUIDE.md`
