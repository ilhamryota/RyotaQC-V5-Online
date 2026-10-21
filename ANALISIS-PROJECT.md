# 📋 Analisis Project RyotaQC V5 Online

> Tanggal analisis: 21 Okt 2026
> Branch dianalisis: `Development` (source) & `Production` (EXE)
> Repo: github.com/ilhamryota/RyotaQC-V5-Online (PUBLIC, default branch = Production)

---

## 🟢 Ringkasan Eksekutif

| Area | Status | Keterangan |
|------|--------|------------|
| Build compile | ✅ Sukses | 0 warning, 0 error (dotnet build net10.0) |
| Koneksi ERP Hub | ✅ Terhubung & berfungsi | TCP open, `/health`=200, `/api/v1/qc/submit`=400 (validasi body) |
| Sync GitHub Dev vs Prod | ✅ Sinkron | Local `Development` & `Production` = `origin` (0 ahead / 0 behind) |
| Auto-update dari GitHub | ⚠️ Tidak berfungsi | **Tidak ada Release & Tag** di GitHub → `CheckForUpdateAsync` selalu null |
| Script install | ❌ Korup | `RyotaQC-Install-Run.bat` rusak (8 backspace chars, perintah `dotnet` & `start` hilang) |
| File tidak terpakai | ⚠️ Ada beberapa | DISKDATA leftover, AudioTest 2.mp4, dotnet-sdk installer (205 MB) |
| Keamanan | 🔴 KRITIS | Token GitHub ter-expose di chat & di remote URL git; repo PUBLIC |

---

## 1️⃣ Build, Bug & Error

### ✅ Yang berfungsi
- **Compile bersih**: `dotnet build src\RyotaQC.App\RyotaQC.App.csproj` → 0 warning, 0 error, output DLL di `\bin\Debug\net10.0-windows\`
- **Struktur MVVM rapi**: `Models/`, `ViewModels/`, `Views/`, `Services/`, `Converters/`, `Controls/`, `Themes/`, `Config/`
- **DI container** di `App.xaml.cs` — semua service & viewmodel teregistrasi dengan benar
- **373 file tracked** di branch Development, semua terpakai di codebase (cek cross-reference aman)

### ❌ Bug KRITIS 1 — `RyotaQC-Install-Run.bat` korup total (3532 bytes)

File ini **tidak akan pernah bisa meng-install atau menjalankan aplikasi**.

Bukti korupsi:
- **8 karakter backspace (0x08)** tertanam di dalam path → `%APP_DIR%\bin\Release\net10.0-windows\...` rusak menjadi `%APP_DIR%[BS]in\Release[NL]et10.0-windows...`
- **0 perintah `dotnet restore`** (seharusnya ada untuk download dependensi)
- **0 perintah `dotnet build`** (seharusnya ada untuk compile)
- **0 perintah `dotnet publish`** 
- **0 perintah `start "" "..."`** untuk launch EXE setelah build

Akibatnya:
- Block `if defined FOUND_EXE` selalu false (karena path detection rusak)
- Block `dotnet restore` / `dotnet build` hilang total → tidak ada build yang terjadi
- Tidak ada `start` untuk meluncurkan EXE hasil build

Contoh baris korup (line 17-18):
```
if exist "%APP_DIR%in\Release
et10.0-windows\RyotaQC.App.exe" set "FOUND_EXE=%APP_DIR%in\Release
et10.0-windows\RyotaQC.App.exe"
```
Seharusnya:
```
if exist "%APP_DIR%\bin\Release\net10.0-windows\RyotaQC.App.exe" set "FOUND_EXE=%APP_DIR%\bin\Release\net10.0-windows\RyotaQC.App.exe"
```

**Kemungkinan penyebab**: Editor/API lain memproses file `.bat` tanpa escape backslash, sehingga `\b` dianggap backspace dan `\n` dianggap newline.

### ❌ Bug KRITIS 2 — Auto-update tidak akan pernah muncul

`AppUpdateService.CheckForUpdateAsync()` (dipanggil di `App.xaml.cs:135`) membaca endpoint:
```
https://api.github.com/repos/ilhamryota/RyotaQC-V5-Online/releases/latest
```

Hasil pengecekan GitHub API:
- **0 Releases** di repo
- **0 Tags** di repo

Akibatnya endpoint return 404 → `CheckForUpdateAsync` return `null` → popup "Update Wajib" **tidak pernah muncul** meskipun README mengatakan fitur auto-update wajib.

### ⚠️ Bug Ringan 1 — `AppUpdateService.cs` tidak filter ekstensi asset

Baris 67-89 comment mengatakan "Find the first asset that looks like an update package (ZIP)", tapi kode hanya ambil `assets[0]` tanpa cek ekstensi. Kalau release pertama-tama upload checksum `.sha256` atau Release Notes, auto-update akan download file salah lalu gagal extract.

### ⚠️ Bug Ringan 2 — Version mismatch antar file

| File | Versi default | Status |
|------|---------------|--------|
| `src/RyotaQC.App/RyotaQC.App.csproj` | 5.0.1 | ✅ Benar (current) |
| `README.md` | 5.0.1 | ✅ Benar |
| `scripts/01-publish-release.ps1` | 5.0.0 | ❌ Outdated |
| `scripts/02-build-setup.ps1` | 5.1.0 | ❌ Loncat (bisa bikin installer salah label) |
| `scripts/03-build-portable.ps1` | 5.0.0 | ❌ Outdated |

---

## 2️⃣ Koneksi ERP Hub

### ✅ Status: TERHUBUNG & BERFUNGSI

| Pengecekan | Hasil |
|------------|-------|
| TCP `109.123.239.163:8005` | ✅ Open (TcpTestSucceeded: True) |
| `GET /` | 404 (tidak ada root handler — wajar) |
| `GET /health` | ✅ **200 OK** |
| `GET /api/v1/health` | ✅ **200 OK** |
| `POST /api/v1/qc/submit` (body `{}`) | ✅ **400 Bad Request** (route ada, validasi body jalan) |
| `GET /api/v1/qc/unit-info/test` | 404 (unit "test" tidak ditemukan — behavior benar) |
| `GET /docs` & `/openapi.json` | 404 (swagger dimatikan di production — wajar) |

### Implémentasi di kode
- **Service**: `src/RyotaQC.App/Services/ErpSyncService.cs`
- **DI registration**: `App.xaml.cs:190` → `services.AddSingleton<IErpSyncService, ErpSyncService>()`
- **Konsumsi**: `QualityControlPageViewModel.cs` (line 2269-2368)
  - `LookupUnitAsync(PraQcCode)` untuk lookup unit berdasarkan pra-QC code
  - `SubmitQcAsync(payload)` untuk submit hasil QC lengkap (specs, tests, status)
- **Hub URL**: Default `http://109.123.239.163:8005` (hardcoded di `ErpSyncService` ctor, bisa di-override via parameter `hubBaseUrl`)
- **Payload model** lengkap: `PraQcCode`, `SerialNumber`, `Brand`, `Model`, `Specs`, `BatteryHealth`, `StorageHealth`, `Status`, `Tests`, `TestedBy`, `Notes`

### ⚠️ Catatan keamanan ERP
- Hub pakai **HTTP (bukan HTTPS)** — payload QC (serial number, brand, model, specs) terkirim plain-text. Untuk infra production sebaiknya pakai HTTPS + reverse proxy.
- Tidak ada autentikasi (API key / token) di sisi client `ErpSyncService`. Siapa saja yang tahu URL bisa POST.

---

## 3️⃣ GitHub — Dev & Production

### ✅ Status sync branch
| Branch lokal | Local SHA | Origin SHA | Ahead/Behind |
|--------------|-----------|------------|--------------|
| `Development` | `088b1e5` | `088b1e5` | 0 / 0 ✅ |
| `Production` | `2d7c65b` | `2d7c65b` | 0 / 0 ✅ |

### Struktur branch
- **Development (373 files)**: source code lengkap — `.sln`, `src/`, `scripts/`, `tools/`, `installer/`, `docs/`
- **Production (270 files)**: hanya hasil publish self-contained — `RyotaQC.App.exe` (87.7 MB via LFS) + `Assets/`, `Config/`, `LatoFont/`, `tools/` yang dibutuhkan runtime

### Diff Development vs Production
- 189 file, 16.342 baris lebih banyak di Development (semua source code yang tidak dibundle ke Production)
- Per README memang strategi: Dev = source, Prod = EXE siap pakai ✅

### ⚠️ Yang perlu diperhatikan
1. **Default branch = `Production`** — saat user buka URL repo, lihat branch Production dulu (exe langsung terlihat). OK sesuai strategic.
2. **Repo PUBLIC** — siapa saja bisa clone source code + download EXE. Review apakah ini disengaja.
3. **Tidak ada Release/Tag** → fitur auto-update (yang di-claim mandatory) tidak bisa trigger update apapun.

---

## 4️⃣ File Tidak Terpakai / Bisa Dibersihkan

### 🗑️ Jelas harus diperhatikan

| File | Lokasi | Size | Alasan |
|------|--------|------|--------|
| `dotnet-sdk-win-x64.exe` | `installer/` | **205.64 MB** | SDK installer .NET — tidak dibutuhkan user akhir (dapat self-contained EXE); tidak di-reference script manapun |
| 22 file `DISKDATA_*.dat/.hdd/.hdi` | `tools/qc/Hard Disk Sentinel/` | bervariasi | Snapshot disk dari tes sebelumnya (Samsung MZVLB..., MTFDDAK..., SanDisk USB, dll) — leftover state, bukan file app |
| `AudioTest 2.mp4` | `tools/qc/Audio Test/` | bervariasi | Nama dengan " 2" — backup duplikat dari `AudioTest.mp4` |

### 📦 Folder lokal yang bisa dihapus (sudah gitignored)
- `publish/` (1.17 GB) — output publish local, gitignored, aman dihapus
- `src/RyotaQC.App/bin/` & `obj/` — output build, gitignored
- `logs/` — saat ini kosong

### ✅ Yang awalnya kelihatan unused tapi SEBENARNYA DIPAKAI
- `QcResultPopupWindow` (singular) vs `QcResultsPopupWindow` (plural) — **BUKAN duplikat**:
  - `QcResultPopupWindow` = popup hasil single-test (Pass/Skip/Fail) — dipakai di `QualityControlPageViewModel.cs` (line 387, 390, 439, 442, 492, 571, 573, 579, 2456, 2461, 2467, 2473) — untuk tiap test
  - `QcResultsPopupWindow` (plural) = popup summary semua hasil QC — dipakai di line 670
- Converters `ImagePathConverter`, `ZeroToVisibilityConverter`, `InverseBooleanToVisibilityConverter` — semua di-XAML reference
- Background images (Wallpapers/*.jpg, Tiles/*.jpg) — dimuat dinamis via `wallpaper-map.json` dan `ImagePathConverter`
- File `.gitkeep` di subfolder Assets — placeholder folder tracking, normal

---

## 5️⃣ 🔴 Security Concerns (URGENT)

### 1. Token GitHub ter-expose di chat
Anda share 2 token:
- `github_pat_11CDCI...` (Fine-Grained)
- `ghp_hsyS...` (Classic)

Karena chat bisa ter-log/ter-cache, **segera revoke & regenerate kedua token** di:
- https://github.com/settings/tokens
- https://github.com/settings/personal-access-tokens

### 2. Token tersimpan di URL git remote
```
origin https://ilhamryota:ghp_hsyS...@github.com/ilhamryota/RyotaQC-V5-Online.git
```
Token terlihat di `git remote -v`, di `.git/config`, dan di log shell. Setelah revoke, reconfigure remote tanpa credential inline:
```bash
git remote set-url origin https://github.com/ilhamryota/RyotaQC-V5-Online.git
git config credential.helper manager   # pakai Windows Credential Manager
```

### 3. Repo PUBLIC dengan EXE self-contained
Siapa saja bisa clone & analisis binary. Pastikan tidak ada secret/credential yang ter-embed di EXE.

### 4. ERP pakai HTTP plain-text (bukan HTTPS)
Payload QC (serial number, brand, model, status, nama teknisi) terkirim tanpa enkripsi. Untuk security, pass via reverse proxy dengan TLS.

---

## 6️⃣ Rekomendasi Tindakan (Prioritas)

### 🔴 Urgent (segera)
1. **Revoke & regenerate kedua GitHub token** yang ter-expose di chat
2. **Reconfigure git remote** tanpa inline token, pakai Windows Credential Manager
3. **Fix file `RyotaQC-Install-Run.bat`** — rewrite dari skratch dengan escape backslash yang benar, atau hapus saja kalau tidak dipakai (run.bat sudah cukup untuk dev)

### 🟠 High priority (minggu ini)
4. **Buat GitHub Release pertama** tag `v5.0.1` dengan ZIP asset dari `publish/` folder — supaya fitur auto-update yang "mandatory" bisa jalan
5. **Fix bug `AppUpdateService.cs:88`** — filter asset dengan ekstensi `.zip` sebelum dipakai
6. **Sync version default** di 3 file `scripts/*.ps1` ke `5.0.1`

### 🟡 Medium priority
7. **Hapus / gitignore** file leftover:
   - `tools/qc/Hard Disk Sentinel/DISKDATA_*` (22 file)
   - `tools/qc/Audio Test/AudioTest 2.mp4`
8. **Pertimbangkan apakah `installer/dotnet-sdk-win-x64.exe` (205 MB)** perlu di-repo — kalau tidak, pindah ke release asset atau hapus
9. **Setup HTTPS** untuk ERP Hub (reverse proxy Caddy/nginx + Let's Encrypt)

### 🟢 Low priority
10. Hapus folder `publish/` lokal (1.17 GB) untuk hemat disk space
11. Tambahkan GitHub Actions untuk auto-build (CI/CD) supaya saat tag baru dibuat, release otomatis ter-publish

---

## 7️⃣ Lampiran — Bukti Pengukuran

### Build output
```
> dotnet build src\RyotaQC.App\RyotaQC.App.csproj
Build succeeded.
    0 Warning(s)
    0 Error(s)
Time Elapsed 00:00:16.04
```

### ERP hub connectivity
```
GET  http://109.123.239.163:8005/                       -> 404
GET  http://109.123.239.163:8005/health                 -> 200 ✅
GET  http://109.123.239.163:8005/api/v1/health          -> 200 ✅
POST http://109.123.239.163:8005/api/v1/qc/submit ({})-> 400 ✅ (route valid, body validation)
GET  http://109.123.239.163:8005/api/v1/qc/unit-info/test -> 404 (unit not found, expected)
```

### GitHub API
```
GET /repos/ilhamryota/RyotaQC-V5-Online
  private: false  default_branch: Production
  pushed_at: 2026-10-20T15:27:41Z
  size: 98065 KB

GET /repos/.../branches
  Development -> 088b1e5  (sync with local)
  Production   -> 2d7c65b (sync with local)

GET /repos/.../releases  -> []  (KOSONG)
GET /repos/.../tags      -> []  (KOSONG)
```

### File `RyotaQC-Install-Run.bat` analysis
```
File size: 3532 bytes
Backspace chars (0x08) in content: 8   ← korupsi
"dotnet restore" count: 0              ← seharusnya 1
"dotnet build"   count: 0              ← seharusnya 1
"dotnet publish" count: 0
"start \"\""     count: 0              ← seharusnya 1 (launch exe)
```

---

*Laporan dibuat otomatis oleh GLM · Zhipu AI.*