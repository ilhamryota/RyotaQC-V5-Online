# Workflow Publish Update RyotaQC V5

Dokumentasi langkah-langkah untuk merilis versi baru RyotaQC V5 Online agar fitur **auto-update mandatory** berfungsi penuh.

> Target audience: Developer/Owner repo
> Prasyarat: .NET 10 SDK, git, akses push ke `ilhamryota/RyotaQC-V5-Online`

---

## Cara Kerja Auto-Update (Sekilas)

Saat aplikasi dibuka, `AppUpdateService` (di `src/RyotaQC.App/Services/AppUpdateService.cs`) otomatis request ke:

```
GET https://api.github.com/repos/ilhamryota/RyotaQC-V5-Online/releases/latest
```

Logika:
1. Baca `tag_name` (contoh `v5.0.2`), bandingkan dengan versi assembly
2. Cari asset dengan ekstensi `.zip` di release — ambil yang pertama
3. Jika versi baru → popup "Update Wajib" muncul (tidak bisa ditutup)
4. User klik "Update Sekarang" → download ZIP → extract → jalankan `apply_update.bat` → app restart otomatis dengan file baru

**Konsekuensi**: Tanpa Release di GitHub, auto-update tidak akan pernah trigger.

---

## Step 1: Update Version di csproj

Sebelum build, naikkan versi di `src/RyotaQC.App/RyotaQC.App.csproj`:

```xml
<Version>5.0.2</Version>
<FileVersion>5.0.2.0</FileVersion>
<AssemblyVersion>5.0.2.0</AssemblyVersion>
```

> Konvensi: minor bump (5.0.1 → 5.0.2 → 5.0.3). Naikkan major (5.1.0) kalau ada breaking change UX.

## Step 2: Sync Version di Script Build (Opsional, tapi konsisten)

Update default value `-Version` di 3 file:
- `scripts/01-publish-release.ps1`
- `scripts/02-build-setup.ps1`
- `scripts/03-build-portable.ps1`

## Step 3: Build EXE Single-File

```powershell
# Hapus publish lama dulu kalau ada
if (Test-Path publish) { Remove-Item publish -Recurse -Force }

# Build self-contained single-file
& "C:\Program Files\dotnet\dotnet.exe" publish src\RyotaQC.App\RyotaQC.App.csproj `
  -c Release -r win-x64 --self-contained true `
  -p:PublishSingleFile=true -p:IncludeNativeLibrariesForSelfExtract=true `
  -o publish
```

Output: `publish\RyotaQC.App.exe` (~87 MB, sudah include .NET runtime).

## Step 4: Buat ZIP Update Package

```powershell
$version = "5.0.2"
$zipPath = "$pwd\RyotaQC-V5-Update-v$version.zip"
if (Test-Path $zipPath) { Remove-Item $zipPath -Force }

Push-Location publish
tar -a -c -f $zipPath *
Pop-Location

# Verifikasi
Get-Item $zipPath | Select-Object Name, @{n='SizeMB';e={[math]::Round($_.Length/1MB,2)}}
```

ZIP akan berisi ~71 file: `RyotaQC.App.exe`, `Assets/`, `Config/`, `LatoFont/`, `Logs/`, `tools/`.

## Step 5: Commit & Push ke Branch Development

```bash
git add -A
git commit -m "release: v5.0.2 - deskripsi singkat perubahan"
git push origin Development
```

## Step 6: Buat GitHub Release

Pakai GitHub API (lebih reliable untuk file besar):

```powershell
$token = "ghp_xxxxx"  # PAT dengan scope repo:public_repo
$version = "5.0.2"

# Buat release (draft=true dulu, publish setelah asset upload)
$body = @{
    tag_name = "v$version"
    target_commitish = "Development"
    name = "RyotaQC V5 Online v$version"
    body = @"
# RyotaQC V5 Online - v$version

## Yang Baru
- [deskripsi fitur/bug fix]

## Perbaikan Bug
- [daftar bug yang diperbaiki]

## Cara Update
- **Otomatis**: Buka aplikasi lama → popup update wajib → klik Update Sekarang
- **Manual**: Download ZIP → extract ke folder aplikasi lama (overwrite)
"@
    draft = $true
    prerelease = $false
    make_latest = "true"
} | ConvertTo-Json -Depth 5

$r = Invoke-RestMethod -Uri "https://api.github.com/repos/ilhamryota/RyotaQC-V5-Online/releases" `
    -Method Post `
    -Headers @{ Authorization = "token $token"; Accept = "application/vnd.github+json"; "User-Agent" = "RyotaQC-Release" } `
    -Body $body `
    -ContentType "application/json"

$releaseId = $r.id
Write-Output "Release created (draft): id=$releaseId, tag=v$version"
```

## Step 7: Upload ZIP Asset ke Release

```powershell
$zipPath = "$pwd\RyotaQC-V5-Update-v$version.zip"
$assetName = "RyotaQC-V5-Update-v$version.zip"

$uploadUrl = "https://uploads.github.com/repos/ilhamryota/RyotaQC-V5-Online/releases/$releaseId/assets?name=$([Uri]::EscapeDataString($assetName))"

# Gunakan HttpClient untuk reliable upload file besar
Add-Type -AssemblyName System.Net.Http
$client = New-Object System.Net.Http.HttpClient
$client.Timeout = [TimeSpan]::FromMinutes(15)
$client.DefaultRequestHeaders.Add("Authorization", "token $token")
$client.DefaultRequestHeaders.Add("Accept", "application/vnd.github+json")
$client.DefaultRequestHeaders.Add("User-Agent", "RyotaQC-Upload")

$stream = [System.IO.File]::OpenRead($zipPath)
$content = New-Object System.Net.Http.StreamContent($stream)
$content.Headers.Add("Content-Type", "application/zip")

$response = $client.PostAsync($uploadUrl, $content).Result
$stream.Close()
$client.Dispose()

if ($response.IsSuccessStatusCode) {
    $asset = $response.Content.ReadAsStringAsync().Result | ConvertFrom-Json
    Write-Output "Asset uploaded: $($asset.name) ($([math]::Round($asset.size/1MB,2)) MB)"
    Write-Output "Download URL: $($asset.browser_download_url)"
} else {
    Write-Output "FAILED: $($response.StatusCode)"
    Write-Output $response.Content.ReadAsStringAsync().Result
}
```

## Step 8: Publish Release (ubah draft=false)

```powershell
$updateBody = @{
    draft = $false
    make_latest = "true"
} | ConvertTo-Json

Invoke-RestMethod -Uri "https://api.github.com/repos/ilhamryota/RyotaQC-V5-Online/releases/$releaseId" `
    -Method Patch `
    -Headers @{ Authorization = "token $token"; Accept = "application/vnd.github+json"; "User-Agent" = "RyotaQC-Release" } `
    -Body $updateBody `
    -ContentType "application/json" | Select-Object tag_name, draft, html_url

Write-Output "Release published! Auto-update aktif untuk user."
```

---

## Verifikasi

Cek endpoint yang dipakai aplikasi:

```powershell
$h = @{ "User-Agent" = "RyotaQC-Check"; Accept = "application/vnd.github+json" }
$r = Invoke-RestMethod -Uri "https://api.github.com/repos/ilhamryota/RyotaQC-V5-Online/releases/latest" -Headers $h
$r | Select-Object name, tag_name, published_at
$r.assets | Select-Object name, @{n='SizeMB';e={[math]::Round($_.size/1MB,2)}}, browser_download_url
```

Jika return JSON dengan `tag_name: "v5.0.2"` dan minimal 1 asset `.zip` → auto-update siap melayani user.

---

## Cheat Sheet — One-Liner untuk Update Berikutnya

Ganti `$newVer` lalu jalankan blok ini end-to-end:

```powershell
$newVer = "5.0.2"
$token  = "ghp_xxxxx"

# 1. Update csproj version
$f = "src\RyotaQC.App\RyotaQC.App.csproj"
(Get-Content $f) -replace '<Version>[^<]+</Version>', "<Version>$newVer</Version>" `
                 -replace '<FileVersion>[^<]+</FileVersion>', "<FileVersion>${newVer}.0</FileVersion>" `
                 -replace '<AssemblyVersion>[^<]+</AssemblyVersion>', "<AssemblyVersion>${newVer}.0</AssemblyVersion>" |
    Set-Content $f

# 2. Build & ZIP
Remove-Item publish -Recurse -Force -EA Ignore
dotnet publish src\RyotaQC.App\RyotaQC.App.csproj -c Release -r win-x64 --self-contained true -p:PublishSingleFile=true -p:IncludeNativeLibrariesForSelfExtract=true -o publish
$zip = "$pwd\RyotaQC-V5-Update-v$newVer.zip"
Remove-Item $zip -Force -EA Ignore
Push-Location publish; tar -a -c -f $zip *; Pop-Location

# 3. Commit & push
git add -A
git commit -m "release: v$newVer"
git push origin Development

# 4. Create release + upload asset + publish (pakai step 6-7-8 di atas)
# ... lanjut manual dengan token baru
```

---

## Troubleshooting

| Gejala | Penyebab | Solusi |
|--------|---------|---------|
| Popup update tidak muncul | Tidak ada release di GitHub | Buat release dengan tag `vX.Y.Z` |
| Popup muncul tapi download gagal | Tidak ada asset .zip | Upload ZIP ke release |
| Download OK tapi extract gagal | ZIP rusak / bukan .zip | Re-ZIP dan re-upload |
| App update tapi versi masih lama | tag_name bukan format `vX.Y.Z` | Pastikan tag diawali `v` |
| Auto-update lewati release | release ditandai `prerelease=true` | Set `prerelease=false` |
| Asset .zip tidak terdeteksi | Ada asset lain (e.g. .sha256) lebih dulu | Fix AppUpdateService filter `.zip` saja (sudah dikerjakan di commit `3e4535d`) |

---

*Update terakhir: 21 Okt 2026 — auto-update aktif untuk RyotaQC V5 v5.0.1*