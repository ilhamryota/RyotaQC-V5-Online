# Temuan & Rekomendasi — Jabatan dan Izin EsteraOne ERP

> **Dibuat**: 24 Oktober 2026, 11:12 WIB — dipisah dari dokumen acuan agar matriksnya tetap bersih.
> **Sumber**: database aktif (5438/`esteraone`) dan database dev (5439/`esteraone_dev`), kode `bestlaptop-erp-be` dan `bestlaptop-erp-fe`.

## Ringkasan prioritas

| # | Temuan | Dampak | Prioritas |
|---|---|---|---|
| 1 | Pintasan "super admin" tidak menyala (nama `Super Admin` vs kode `SUPER_ADMIN`) | Fitur/menu baru bisa tidak muncul untuk semua orang, termasuk Super Admin | Tinggi |
| 2 | Jabatan `HEAD_GUDANG HO` tidak dikenali aturan divisi | Satu akun bisa melihat menu Holding, HR, Best Laptop, Best Service, Bestcare, Marketing | Tinggi |
| 3 | Menu yang muncul tetapi aksi ditolak server (nama jabatan ditulis manual di backend, tidak konsisten) | Pengguna menemui error 403 seperti kasus Surat Jalan | Tinggi |
| 4 | Izin di database tidak sejalan dengan menu (HRD, PURCHASING, Teknisi) | Izin ada tapi tidak terpakai, atau menu ada tapi izinnya tidak ada | Sedang |
| 5 | 8 jabatan tanpa kode & beberapa jabatan kembar | Rawan salah pilih saat menugaskan pengguna | Sedang |
| 6 | 11 jabatan tanpa pengguna, sebagian berizin besar | Kewenangan "menganggur" yang bisa aktif tanpa sengaja | Sedang |
| 7 | Akun ganda & akun uji di data produksi | Sulit dilacak, risiko akses berlebih | Sedang |
| 8 | Rujukan menu yang mati (`menu_hr`, `menu_finance`, `menu_kpi`) | Fitur cadangan penampilan menu tidak berfungsi | Rendah |

## 1. Pintasan "super admin" tidak menyala

Nama jabatan di database: **`Super Admin`** (pakai spasi), kode: `SUPER_ADMIN`. Sedangkan daftar "super" di kode program memakai tulisan **`SUPER_ADMIN`** (garis bawah) dan `SUPERADMIN` — tidak ada bentuk dengan spasi. Di backend, pemeriksaan izin bahkan tidak mengambil kolom `code`, sehingga pembandingan kode selalu kosong.

**Akibatnya**: akses penuh Super Admin terjadi bukan karena kode, melainkan karena jabatan itu memiliki **328 baris izin**. 

**Risiko**: setiap menu atau halaman baru yang ditambahkan tanpa baris izin di database akan **tidak muncul untuk siapa pun, termasuk Super Admin**.

**Rekomendasi**: samakan penulisan (tambah bentuk `SUPER ADMIN` ke daftar) dan ambil kolom `code` pada query pemeriksaan izin; atau jadikan `isSystem` sebagai penanda bypass.

## 2. Jabatan `HEAD_GUDANG HO` tidak dikenali aturan divisi

Jabatan ini (1 orang: **Head Gudang BDG**) **tidak punya kode** dan namanya tidak terdaftar dalam daftar jabatan gudang (`Gudang HO`, `GUDANG_HO`, `HEAD_GUDANG`, `GUDANG`, `PURCHASING`). Karena itu seluruh aturan pemisahan divisi dilewati, dan hasil simulasi menu menunjukkan jabatan ini bisa melihat:

- Holding → Kelola Pengguna
- HR & Payroll → Data Karyawan, Shift Master, Shift Assignment
- Gudang HO (11 halaman)
- Best Laptop (5 halaman)
- Best Service (6 halaman)
- Bestcare (4 halaman)
- Marketing → Premi Marketing

**Rekomendasi**: beri kode resmi dan masukkan ke daftar jabatan gudang, lalu tetapkan izinnya sesuai kebutuhan nyata.

## 3. Menu muncul tetapi aksi ditolak server

Beberapa halaman masih membandingkan **nama jabatan secara manual** di backend dengan tulisan yang tidak seragam: `SUPER_ADMIN` vs `Super Admin`, `HO WAREHOUSE`, `HEAD SALES`, `ADMIN_BESTSERVICE` vs `ADMIN BEST SERVICE`. Ini akar masalah error 403 seperti kasus penerimaan Surat Jalan.

Contoh lokasi: `sales.controller.ts`, `service.controller.ts`, `bestcare-premium.controller.ts`, `user.controller.ts`.

**Rekomendasi**: ganti seluruh pemeriksaan manual menjadi pemeriksaan izin (`requirePermission`) dengan satu daftar rujukan jabatan di satu berkas.

## 4. Izin di database tidak sejalan dengan menu

| Kasus | Izin tersimpan | Kenyataan di aplikasi |
|---|---|---|
| HRD | Bisa mengelola pengguna & jabatan (`master_user`, `master_role`, `role`, `user`) | Menu Holding disembunyikan untuk jabatan HR |
| PURCHASING | Bisa menambah/ubah/hapus item & persediaan | Hampir tidak ada halaman yang muncul (hanya Gudang Bestlaptop) |
| Teknisi Best Service | Daftar menu program menyertakan Req Oprasional | Izin `feature_service_operational_request` tidak ada, menu tidak muncul |
| Best Laptop / Head Best Laptop | Punya izin Supplier, PO, QC, Retur Unit | Halaman disembunyikan dari panel kiri (`hiddenFromSidebar`) |

**Rekomendasi**: tentukan satu sumber kebenaran — izin di database seharusnya menjadi penentu menu, dan penyembunyian manual dipakai seminimal mungkin.

## 5. Jabatan tanpa kode dan jabatan kembar

Tanpa kode (8): `FINANCE`, `GUDANG HO`, `HEAD SALES`, `HEAD_GUDANG HO`, `HEAD_SERVICE`, `SALES`, `TEKNISI`, `PURCHASING`.

Kembar / beririsan:

| Pasangan | Orang |
|---|---|
| `Finance` vs `FINANCE` | 3 vs 0 |
| `Gudang HO` vs `GUDANG HO` | 4 vs 0 |
| `HEAD_GUDANG HO` vs `HEAD_GUDANG` (dev) | 1 vs 3 |
| `Sales Best Laptop` vs `SALES` | 6 vs 0 |
| `Teknisi Best Service` vs `TEKNISI` | 16 vs 0 |
| `Head Best Service` vs `HEAD_SERVICE` | 12 vs 0 |
| `Head Cabang (Laptop & Service)` | 1 (219 izin) |

**Rekomendasi**: tentukan daftar jabatan resmi, beri kode semua, dan pindahkan orang dari jabatan lama ke jabatan resmi sebelum menghapus yang lama.

## 6. Jabatan tanpa pengguna

| Jabatan | Izin |
|---|---|
| Service Advisor | 150 |
| Gudang Best Service | 85 |
| Gudang Best Laptop | 80 |
| Sales Best Service | 80 |
| HEAD_SERVICE | 72 |
| TEKNISI | 71 |
| FINANCE | 67 |
| GUDANG HO | 65 |
| SALES | 45 |
| Support Best Service | 35 |
| HEAD SALES | 15 |

Jabatan `Service Advisor` (150 izin) dan `Gudang Best Service` (85 izin) berizin besar tetapi tidak dipegang siapa pun.

## 7. Akun ganda dan akun uji

**Nama sama, akun berbeda**:

| Nama | Detail |
|---|---|
| Raihan Muhamad Rahmat | Admin Best Service di Head Office **dan** di Bestservice Sukabumi |
| Bima Pandu Pratama | Admin Best Service (`bimapandupratama...`) **dan** Head Best Service (`bimapp.31...`) di Service Point Bandung |
| Bakti Alpihuda | Sales Best Laptop Sukabumi **dan** Teknisi Best Service Head Office |

**Akun uji / akun bersama di data produksi**: `Test Gudang` (jabatan Gudang HO), `Managament`, `Tim Support HO`, serta 4 akun beremail jabatan (Head Gudang BGR/BJM/SMI, Head Service Bogor).

## 8. Rujukan menu yang mati

Program menyebut izin `menu_hr`, `menu_finance`, `menu_kpi`, `menu_service`, `menu_settings`, tetapi di database aktif **hanya `menu_dashboard`** yang ada. Artinya beberapa cadangan penampilan menu tidak pernah aktif; menu HR dan Keuangan saat ini muncul murni karena izin halaman satu per satu.

## Catatan lingkungan

- Aplikasi `dev.esteraone.com` **saat ini membaca database produksi** (5438/`esteraone`), sehingga seluruh dokumen ini menggambarkan data produksi, bukan data uji coba.
- Database dev (5439/`esteraone_dev`) berisi 70 akun dan 22 jabatan dengan perbedaan yang tercatat pada bagian 7 dokumen acuan. Akun `Ryota` hanya ada di database dev.
- Jika `DATABASE_URL` green dikembalikan ke database dev, perbedaan pada bagian 7 akan langsung terlihat pengguna.

## Urutan tindakan yang disarankan

1. Tetapkan daftar jabatan resmi + kode untuk semuanya (menutup temuan 2, 5).
2. Samakan penulisan nama jabatan pada kode (menutup temuan 1, 3).
3. Rapikan izin HRD/PURCHASING/Teknisi dan halaman tersembunyi (menutup temuan 4).
4. Bersihkan jabatan kosong dan akun ganda/uji (menutup temuan 6, 7).
5. Buat ulang dokumen acuan ini setelah setiap perubahan izin.

---

**Berkas pendamping**: `PERMISSION_MATRIX.md`, `permission-matrix.csv`, `permission-matrix-dev.csv`, `role-users.csv`.
