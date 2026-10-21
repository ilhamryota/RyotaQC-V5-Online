# RyotaQC V5 Online

<div align="center">

![Main Menu](docs/main-menu.png)

**Aplikasi Quality Control (QC) untuk teknisi perbaikan & refurbish laptop Windows.**

Dibangun dengan C# + WPF + MVVM · .NET 10 · Auto-update dari GitHub Releases

</div>

---

## Tentang Project

**RyotaQC V5 Online** adalah aplikasi desktop Windows yang membantu teknisi QC melakukan checklist pengujian laptop secara terstruktur, lengkap dengan report PDF, sinkronisasi ke ERP, dan auto-update otomatis.

## 4 Menu Utama

| # | Menu | Fungsi |
|---|------|--------|
| 1 | **System Information** | Informasi hardware real dari WMI: CPU, OS, RAM, Storage, GPU, serial number |
| 2 | **Wallpaper Desktop** | Set wallpaper desktop standar RyotaQC dengan 1 klik |
| 3 | **Software Licensing** | Buka MAS activation script sebagai administrator |
| 4 | **Quality Control** | Checklist 15 test laptop + sinkronisasi hasil ke ERP database |

## Quality Control — 15 Test

LCD Test, Storage Health, Battery Health, Battery Report, Keyboard Test, Audio Test, Benchmark, Running Test, Camera, Touchscreen Test, Port Test, Touchpad, WiFi, Backlight, Mic.

Setiap test punya popup hasil: **OK** (Pass) / **-** (Skip) / **X** (Fail + catatan).

Hasil QC bisa:
- Disimpan sebagai **PDF** (QuestPDF)
- Disimpan sebagai **Notepad**
- **Sync ke ERP Hub** (`http://109.123.239.163:8005`) untuk update database pusat

## Fitur Auto-Update (Wajib)

Aplikasi **otomatis cek update** 5 detik setelah dibuka. Jika ada versi baru:
- Popup **"Update Wajib"** muncul
- User **tidak bisa menutup** popup sampai update selesai
- User klik "Update Sekarang" → download → restart otomatis

**Versi user tidak bisa dipakai sampai update versi terbaru diinstal.**

## Tech Stack

| Komponen | Teknologi |
|----------|-----------|
| Bahasa | C# 13 (.NET 10) |
| UI | WPF + MVVM |
| MVVM Toolkit | CommunityToolkit.Mvvm 8.4.2 |
| DI | Microsoft.Extensions.DependencyInjection 9.0.8 |
| PDF | QuestPDF 2025.7.4 |
| System Info | System.Management (WMI) |
| Target | Windows 10/11 x64 |

## Branch Strategy

| Branch | Isi | Untuk Siapa |
|--------|-----|-------------|
| **`Development`** | Source code project | Developer |
| **`Production`** | EXE self-contained siap pakai | User / teknisi |

## Cara Build EXE (Self-Contained)

```powershell
& "C:\Program Files\dotnet\dotnet.exe" publish src\RyotaQC.App\RyotaQC.App.csproj `
  -c Release -r win-x64 --self-contained true `
  -p:PublishSingleFile=true -p:IncludeNativeLibrariesForSelfExtract=true `
  -o publish
```

Output: `publish\RyotaQC.App.exe` (~170 MB, tidak butuh .NET terinstall di target).

## Versi

- Versi saat ini: **5.0.1**
- Setiap update naik 1 angka di belakang: 5.0.2 → 5.0.3 → 5.0.4 ...
- Versi ditampilkan di menu utama aplikasi.

## Lisensi & Credits

- **Author:** IllhamRyota
- **Copyright:** © RyotaQC V5
- Repo: https://github.com/ilhamryota/RyotaQC-V5-Online

---

<div align="center">

**RYOTAQC V5 ONLINE**

Dibuat oleh **IllhamRyota**

</div>