# Dokumen Acuan Jabatan & Izin — EsteraOne ERP

> **Dibuat**: 24 Oktober 2026, 11:12 WIB  
> **Sumber utama**: database yang dipakai `dev.esteraone.com` saat ini — PostgreSQL `202.74.74.229:5438`, database `esteraone` (**74 akun**)  
> **Pembanding**: database `202.74.74.229:5439`, database `esteraone_dev` (**70 akun**)  
> **Kode acuan**: `bestlaptop-erp-fe` (`src/config/modules.ts`, `src/constants/permission.ts`, `src/components/app-sidebar.tsx`) dan `bestlaptop-erp-be` (middleware izin)  
> **Sifat**: dokumen baca-saja. Dibuat tanpa mengubah satu pun data, jabatan, izin, atau pengguna.

---

## 1. Cara membaca dokumen ini

Sistem ini memakai dua lapis yang **tidak selalu sama**:

| Lapis | Artinya | Tempatnya |
|---|---|---|
| **Izin tersimpan** | Daftar "boleh/tidak boleh" yang tercatat | tabel izin di database |
| **Izin efektif** | Apa yang benar-benar muncul & bisa dilakukan di aplikasi | perhitungan program (panel kiri) + pemeriksaan server |

Dokumen ini menampilkan **keduanya**, karena menyalin database saja bisa menyesatkan. Contoh nyata: HRD punya izin mengelola jabatan di database, tetapi menunya tidak muncul di aplikasi.

Arti tanda pada matriks:

| Tanda | Arti |
|---|---|
| `v` | Menu muncul (izinnya ada di database) |
| `*` | Menu muncul tanpa perlu izin (bawaan semua karyawan) |
| `o` | Halaman disembunyikan dari panel kiri (sengaja), **tetapi jabatan ini punya izinnya** — halaman masih bisa dibuka lewat tautan langsung |
| `b` | Tidak muncul karena pembatasan khusus (mis. Gudang HO di Best Laptop hanya 3 halaman; Supplier & Req Operasional hanya untuk Kepala Divisi; Teknisi hanya halaman tertentu) |
| `x` | Tidak muncul karena aturan pemisahan divisi |
| `.` | Tidak muncul / tidak bisa dibuka karena izinnya tidak dimiliki |

---

## 2. Cara kerja izin di sistem (ringkas)

- Setiap izin adalah pasangan **sumber daya + aksi**, contoh: `feature_laptop_track_delivery` + `read` ("Halaman Manajemen SJ — boleh dilihat").
- Aksi yang ada di database: `read` (552 baris), `create` (467), `update` (462), `delete` (440), `view` (156).
- **Hierarki**: permintaan `read`/`view` dipenuhi oleh izin `read`, `view`, atau `manage`. Permintaan aksi lain dipenuhi oleh izin aksi yang sama atau `manage`.
- Ada **wildcard**: izin sumber daya `*` atau aksi `manage_all` membuat pemiliknya lolos semua pemeriksaan.
- Panel kiri disaring **dua kali**: (1) aturan pemisahan divisi (orang service tidak melihat menu laptop, dan sebaliknya), lalu (2) izin per halaman.
- **Perubahan izin baru terasa setelah pengguna logout lalu login kembali.**
- Catatan penting: pintasan "super admin" di kode memakai tulisan `SUPER_ADMIN`, sedangkan jabatan di database bernama `Super Admin` (pakai spasi). Lihat berkas temuan.

---

## 3. Identitas jabatan

### 3.1 Database AKTIF (5438 / `esteraone`) — 25 jabatan, 14 di antaranya terpakai

| Jabatan | Kode | Sistem? | Jumlah orang | Jumlah izin | Menu terlihat |
|---|---|---|---|---|---|
| Teknisi Best Service | TEKNISI_BESTSERVICE | tidak | 16 | 35 | 5 halaman |
| Admin Best Service | ADMIN_BESTSERVICE | tidak | 15 | 85 | 12 halaman |
| Head Best Service | HEAD_BESTSERVICE | tidak | 12 | 148 | 12 halaman |
| Super Admin | SUPER_ADMIN | ya | 10 | 328 | 69 halaman |
| Sales Best Laptop | SALES_BEST_LAPTOP | tidak | 6 | 42 | 7 halaman |
| Finance | FINANCE | tidak | 3 | 41 | 18 halaman |
| Gudang HO | GUDANG_HO | tidak | 3 | 60 | 17 halaman |
| Head Best Laptop | HEAD_BEST_LAPTOP | tidak | 3 | 111 | 9 halaman |
| Best Laptop | BESTLAPTOP | tidak | 1 | 60 | 8 halaman |
| Bestcare | BESTCARE | tidak | 1 | 24 | 7 halaman |
| HEAD_GUDANG HO | **(tanpa kode)** | tidak | 1 | 76 | 28 halaman |
| HRD | HRD | tidak | 1 | 69 | 9 halaman |
| Head Cabang (Laptop & Service) | HEAD_DUAL_OUTLET | tidak | 1 | 219 | 35 halaman |
| PURCHASING | **(tanpa kode)** | tidak | 1 | 14 | 4 halaman |
| FINANCE | **(tanpa kode)** | tidak | 0 | 67 | 14 halaman |
| GUDANG HO | **(tanpa kode)** | tidak | 0 | 65 | 17 halaman |
| Gudang Best Laptop | GUDANG_BEST_LAPTOP | tidak | 0 | 80 | 7 halaman |
| Gudang Best Service | GUDANG_BEST_SERVICE | tidak | 0 | 85 | 10 halaman |
| HEAD SALES | **(tanpa kode)** | tidak | 0 | 15 | 5 halaman |
| HEAD_SERVICE | **(tanpa kode)** | tidak | 0 | 72 | 12 halaman |
| SALES | **(tanpa kode)** | tidak | 0 | 45 | 7 halaman |
| Sales Best Service | SALES_BEST_SERVICE | tidak | 0 | 80 | 10 halaman |
| Service Advisor | SERVICE_ADVISOR | tidak | 0 | 150 | 31 halaman |
| Support Best Service | SUPPORT_BESTSERVICE | tidak | 0 | 35 | 5 halaman |
| TEKNISI | **(tanpa kode)** | tidak | 0 | 71 | 6 halaman |

### 3.2 Database DEV (5439 / `esteraone_dev`) — 22 jabatan

| Jabatan | Kode | Jumlah orang | Jumlah izin |
|---|---|---|---|
| Super Admin | SUPER_ADMIN | 16 | 133 |
| Teknisi Best Service | TEKNISI_BESTSERVICE | 16 | 23 |
| Admin Best Service | ADMIN_BESTSERVICE | 14 | 54 |
| Head Best Service | HEAD_BESTSERVICE | 7 | 64 |
| Sales Best Laptop | SALES_BEST_LAPTOP | 7 | 26 |
| HEAD_GUDANG | **(tanpa kode)** | 3 | 50 |
| Gudang HO | GUDANG_HO | 2 | 60 |
| Best Laptop | BESTLAPTOP | 1 | 33 |
| Finance | FINANCE | 1 | 41 |
| HRD | HRD | 1 | 93 |
| Head Best Laptop | HEAD_BEST_LAPTOP | 1 | 23 |
| Support Best Service | SUPPORT_BESTSERVICE | 1 | 20 |
| Bestcare | BESTCARE | 0 | 24 |
| FINANCE | **(tanpa kode)** | 0 | 88 |
| GUDANG | **(tanpa kode)** | 0 | 35 |
| HEAD SALES | **(tanpa kode)** | 0 | 21 |
| HEAD_SERVICE | **(tanpa kode)** | 0 | 52 |
| Head Cabang (Laptop & Service) | HEAD_DUAL_OUTLET | 0 | 81 |
| PURCHASING | **(tanpa kode)** | 0 | 16 |
| SALES | **(tanpa kode)** | 0 | 50 |
| Service Advisor | SERVICE_ADVISOR | 0 | 83 |
| TEKNISI | **(tanpa kode)** | 0 | 17 |

---

## 4. Matriks jabatan × halaman (database AKTIF)

Hanya jabatan **yang ada orangnya** yang ditampilkan sebagai kolom. Singkatan: **TEKNISI** = Teknisi Best Service, **ADM-SVC** = Admin Best Service, **HEAD-SVC** = Head Best Service, **SUPER** = Super Admin, **SALES-LPT** = Sales Best Laptop, **FINANCE** = Finance, **GDG-HO** = Gudang HO, **HEAD-LPT** = Head Best Laptop, **LAPTOP** = Best Laptop, **BESTCARE** = Bestcare, **H-GDG-HO** = HEAD_GUDANG HO, **HRD** = HRD, **HEAD-CBG** = Head Cabang (Laptop & Service), **PURCH** = PURCHASING.

### Menu Karyawan `EMPLOYEE_PORTAL`

| Halaman / fitur | Kunci izin | TEKNISI | ADM-SVC | HEAD-SVC | SUPER | SALES-LPT | FINANCE | GDG-HO | HEAD-LPT | LAPTOP | BESTCARE | H-GDG-HO | HRD | HEAD-CBG | PURCH |
|---|---|---|---|---|---|---|---|---|---|---|---|---|---|---|---|
| Profil Karyawan | `—` | * | * | * | * | * | * | * | * | * | * | * | * | * | * |
| Absensi Saya | `—` | * | * | * | * | * | * | * | * | * | * | * | * | * | * |
| Pengajuan Cuti / Izin | `—` | * | * | * | * | * | * | * | * | * | * | * | * | * | * |

### Holding `HOLDING`

| Halaman / fitur | Kunci izin | TEKNISI | ADM-SVC | HEAD-SVC | SUPER | SALES-LPT | FINANCE | GDG-HO | HEAD-LPT | LAPTOP | BESTCARE | H-GDG-HO | HRD | HEAD-CBG | PURCH |
|---|---|---|---|---|---|---|---|---|---|---|---|---|---|---|---|
| Profil Holding / HO | `master_company:read` | x | x | x | v | x | x | x | x | x | x | . | x | . | x |
| Outlet | `master_outlet:read` | x | x | x | v | x | x | x | x | x | x | . | x | . | x |
| Warehouse / Gudang | `master_warehouse:read` | x | x | x | v | x | x | x | x | x | x | . | x | . | x |
| Role Management | `master_role:read` | x | x | x | v | x | x | x | x | x | x | . | x | . | x |
| Kelola Pengguna | `master_user:read` | x | x | x | v | x | x | x | x | x | x | v | x | . | x |
| Config System | `master_config_system:read` | x | x | x | v | x | x | x | x | x | x | . | x | . | x |
| Activity Log | `activity_log:read` | x | x | x | v | x | x | x | x | x | x | . | x | . | x |

### KPI `KPI`

| Halaman / fitur | Kunci izin | TEKNISI | ADM-SVC | HEAD-SVC | SUPER | SALES-LPT | FINANCE | GDG-HO | HEAD-LPT | LAPTOP | BESTCARE | H-GDG-HO | HRD | HEAD-CBG | PURCH |
|---|---|---|---|---|---|---|---|---|---|---|---|---|---|---|---|
| Pusat Kontrol Penjualan | `kpi:read` | x | x | x | v | x | x | x | x | x | x | . | x | . | x |
| Input Funnel Harian | `kpi:read` | x | x | x | v | x | x | x | x | x | x | . | x | . | x |
| Kelola Target Cabang | `kpi:read` | x | x | x | v | x | x | x | x | x | x | . | x | . | x |

### HR & Payroll `HR_PAYROLL`

| Halaman / fitur | Kunci izin | TEKNISI | ADM-SVC | HEAD-SVC | SUPER | SALES-LPT | FINANCE | GDG-HO | HEAD-LPT | LAPTOP | BESTCARE | H-GDG-HO | HRD | HEAD-CBG | PURCH |
|---|---|---|---|---|---|---|---|---|---|---|---|---|---|---|---|
| Data Karyawan | `master_user:read` | x | x | x | v | x | x | x | x | x | x | v | v | . | x |
| Dashboard Absensi | `attendance_recap:read` | x | x | x | v | x | x | x | x | x | x | . | v | v | x |
| Rekap Absensi | `attendance_recap:read` | x | x | x | v | x | x | x | x | x | x | . | v | v | x |
| Payroll | `payroll:read` | x | x | x | v | x | x | x | x | x | x | . | v | . | x |
| Shift Master | `shift_master:read` | x | x | x | v | x | x | x | x | x | x | v | v | v | x |
| Shift Assignment | `shift_assignment:read` | x | x | x | v | x | x | x | x | x | x | v | v | v | x |

### Keuangan & Laporan `FINANCE_REPORTS`

| Halaman / fitur | Kunci izin | TEKNISI | ADM-SVC | HEAD-SVC | SUPER | SALES-LPT | FINANCE | GDG-HO | HEAD-LPT | LAPTOP | BESTCARE | H-GDG-HO | HRD | HEAD-CBG | PURCH |
|---|---|---|---|---|---|---|---|---|---|---|---|---|---|---|---|
| Dashboard Keuangan | `finance:read` | x | x | x | v | x | v | x | x | x | x | . | x | . | x |
| Chart of Accounts (COA) | `coa:read` | x | x | x | v | x | v | x | x | x | x | . | x | . | x |
| Buku Jurnal Umum | `finance:read` | x | x | x | v | x | v | x | x | x | x | . | x | . | x |
| Kas & Rekening Bank | `finance:read` | x | x | x | v | x | v | x | x | x | x | . | x | . | x |
| Modal & Investor | `finance:read` | x | x | x | v | x | v | x | x | x | x | . | x | . | x |
| Aset Tetap Perusahaan | `finance:read` | x | x | x | v | x | v | x | x | x | x | . | x | . | x |
| Valuasi Persediaan & Aset Dagang | `finance:read` | x | x | x | v | x | v | x | x | x | x | . | x | . | x |
| Utang Usaha (Payables) | `finance:read` | x | x | x | v | x | v | x | x | x | x | . | x | . | x |
| Piutang Usaha (Receivables) | `finance:read` | x | x | x | v | x | v | x | x | x | x | . | x | . | x |
| Approval PO | `feature_approval_po:read` | x | x | x | v | x | v | x | x | x | x | . | x | . | x |
| Beban Operasional | `feature_expenses:read` | x | x | x | v | x | v | x | x | x | x | . | x | . | x |
| Pembayaran Operasional | `feature_operational_payments:read` | x | x | x | v | x | v | x | x | x | x | . | x | . | x |
| Transaksi Penjualan Outlet | `feature_sales_transactions:read` | x | x | x | v | x | v | x | x | x | x | . | x | . | x |
| Laporan Keuangan & Neraca | `finance:read` | x | x | x | v | x | v | x | x | x | x | . | x | . | x |
| Pajak & Fiskal (Tax) | `finance:read` | x | x | x | v | x | v | x | x | x | x | . | x | . | x |

### Merchandising & Commercial `COMMERCIAL`

| Halaman / fitur | Kunci izin | TEKNISI | ADM-SVC | HEAD-SVC | SUPER | SALES-LPT | FINANCE | GDG-HO | HEAD-LPT | LAPTOP | BESTCARE | H-GDG-HO | HRD | HEAD-CBG | PURCH |
|---|---|---|---|---|---|---|---|---|---|---|---|---|---|---|---|
| Pricing Matrix Laptop | `commercial_pricing:read` | b | b | b | b | x | x | x | x | x | x | b | x | b | x |
| Tarif Jasa Servis | `commercial_service:read` | b | v | v | b | x | x | x | x | x | x | b | x | b | x |
| Garansi BestCare | `(halaman tersembunyi) commercial_bestcare:read` | . | o | o | o | . | . | . | . | . | o | . | . | o | . |
| Harga Sparepart & Non-Seri | `commercial_parts:read` | b | b | b | b | x | x | x | x | x | x | b | x | b | x |

### Gudang HO `WAREHOUSE_LOGISTICS`

| Halaman / fitur | Kunci izin | TEKNISI | ADM-SVC | HEAD-SVC | SUPER | SALES-LPT | FINANCE | GDG-HO | HEAD-LPT | LAPTOP | BESTCARE | H-GDG-HO | HRD | HEAD-CBG | PURCH |
|---|---|---|---|---|---|---|---|---|---|---|---|---|---|---|---|
| Supplier / Vendor | `master_supplier:read` | x | x | x | v | x | x | v | x | x | x | v | x | v | . |
| QC Checklist | `master_qc_checklist:read` | x | x | x | v | x | x | v | x | x | x | v | x | v | . |
| Manajemen Stok | `feature_stock_check:read` | x | x | x | v | x | x | v | x | x | x | v | x | v | . |
| Stock Opname | `feature_stock_check:read` | x | x | x | v | x | x | v | x | x | x | v | x | v | . |
| Manajemen PO | `feature_track_po:read` | x | x | x | v | x | x | v | x | x | x | v | x | v | . |
| Quality control | `feature_qc:read` | x | x | x | v | x | x | v | x | x | x | v | x | v | . |
| Manajemen Retur | `feature_rejected_po:read` | x | x | x | v | x | x | v | x | x | x | v | x | v | . |
| Manajemen SJ | `feature_track_delivery:read` | x | x | x | v | x | x | v | x | x | x | v | x | v | . |
| Request Part Teknisi | `feature_part_request:read` | x | x | x | v | x | x | v | x | x | x | v | x | v | . |
| Request Stok Gudang | `feature_stock_request:read` | x | x | x | v | x | x | v | x | x | x | v | x | v | . |
| Pencatatan Barang | `parts_logs:read` | x | x | x | v | x | x | v | x | x | x | v | x | v | . |

### Best Laptop `BEST_LAPTOP`

| Halaman / fitur | Kunci izin | TEKNISI | ADM-SVC | HEAD-SVC | SUPER | SALES-LPT | FINANCE | GDG-HO | HEAD-LPT | LAPTOP | BESTCARE | H-GDG-HO | HRD | HEAD-CBG | PURCH |
|---|---|---|---|---|---|---|---|---|---|---|---|---|---|---|---|
| Gudang Bestlaptop | `feature_laptop_stock:read` | x | x | x | v | v | x | v | v | v | x | v | x | v | v |
| POS Bestlaptop | `feature_laptop_pos:create` | x | x | x | v | v | x | b | v | v | x | . | x | v | b |
| Manajemen SJ | `feature_laptop_track_delivery:read` | x | x | x | v | . | x | v | v | v | x | v | x | v | . |
| Rek Oprasional | `feature_laptop_operational_request:read` | x | x | x | b | b | x | b | v | b | x | b | x | b | b |
| Reture Penjualan | `sales_return:read` | x | x | x | v | v | x | b | . | v | x | . | x | . | b |
| Stock Opname Laptop | `feature_laptop_stock_opname:read` | x | x | x | v | v | x | v | v | v | x | v | x | v | . |
| Jadwal Shift Toko | `shift_assignment:read` | x | x | x | v | . | x | b | v | . | x | v | x | v | b |
| Quality Control (QC) | `(halaman tersembunyi) feature_laptop_qc:read` | . | . | . | o | . | . | o | o | . | . | o | . | o | . |
| QC Checklist | `(halaman tersembunyi) feature_laptop_qc_checklist:read` | . | . | . | o | . | . | . | o | . | . | . | . | o | . |
| Manajemen PO Laptop | `(halaman tersembunyi) feature_laptop_track_po:read` | . | . | . | o | . | . | . | o | . | . | . | . | o | . |
| Manajemen Retur Unit | `(halaman tersembunyi) feature_laptop_rejected_po:read` | . | . | . | o | . | . | o | o | o | . | o | . | o | . |
| Supplier / Vendor | `(halaman tersembunyi) feature_laptop_supplier:read` | . | . | o | o | . | . | o | o | . | . | o | o | o | o |

### Best Service `BEST_SERVICE`

| Halaman / fitur | Kunci izin | TEKNISI | ADM-SVC | HEAD-SVC | SUPER | SALES-LPT | FINANCE | GDG-HO | HEAD-LPT | LAPTOP | BESTCARE | H-GDG-HO | HRD | HEAD-CBG | PURCH |
|---|---|---|---|---|---|---|---|---|---|---|---|---|---|---|---|
| Gudang Bestservice | `feature_service_stock:read` | b | v | v | v | x | x | x | x | x | x | v | x | v | x |
| POS Bestservice | `feature_service_pos:create` | b | v | v | v | x | x | x | x | x | x | . | x | v | x |
| Service Tiket (Service) | `feature_service_tickets:read` | v | v | v | v | x | x | x | x | x | x | . | x | v | x |
| Manajemen SJ | `feature_service_track_delivery:read` | b | v | v | v | x | x | x | x | x | x | v | x | v | x |
| Req Oprasional | `feature_service_operational_request:read` | . | v | v | v | x | x | x | x | x | x | v | x | v | x |
| Req Part | `feature_service_part_request:read` | v | v | v | v | x | x | x | x | x | x | v | x | v | x |
| Stock Opname Sparepart | `feature_service_stock_opname:read` | b | v | v | v | x | x | x | x | x | x | v | x | v | x |
| Jadwal Shift Service | `shift_assignment:read` | b | v | v | v | x | x | x | x | x | x | v | x | v | x |
| Request Stok Gudang | `(halaman tersembunyi) feature_service_stock_request:read` | . | . | o | o | . | . | o | . | . | . | o | . | o | . |
| Manajemen PO Sparepart | `(halaman tersembunyi) feature_service_track_po:read` | . | . | o | o | . | . | . | . | . | . | . | . | o | . |
| Pencatatan Barang | `(halaman tersembunyi) feature_service_parts_logs:read` | . | . | o | o | . | . | . | . | . | . | . | . | o | . |
| Supplier / Vendor | `(halaman tersembunyi) feature_service_supplier:read` | . | . | o | o | . | . | . | . | . | . | . | . | o | . |

### BESTCARE `BESTCARE`

| Halaman / fitur | Kunci izin | TEKNISI | ADM-SVC | HEAD-SVC | SUPER | SALES-LPT | FINANCE | GDG-HO | HEAD-LPT | LAPTOP | BESTCARE | H-GDG-HO | HRD | HEAD-CBG | PURCH |
|---|---|---|---|---|---|---|---|---|---|---|---|---|---|---|---|
| Data Pelanggan | `crm_contact:read` | x | x | x | v | x | x | x | x | x | v | . | x | v | x |
| Premi Basecare | `bestcare:read` | x | x | x | v | x | x | x | x | x | v | . | x | v | x |
| Tracking Garansi | `bestcare:read` | x | x | x | v | x | x | x | x | x | v | . | x | v | x |
| Bestcare Unit | `bestcare:read` | x | x | x | v | x | x | x | x | x | v | . | x | v | x |

### Marketing `MARKETING`

| Halaman / fitur | Kunci izin | TEKNISI | ADM-SVC | HEAD-SVC | SUPER | SALES-LPT | FINANCE | GDG-HO | HEAD-LPT | LAPTOP | BESTCARE | H-GDG-HO | HRD | HEAD-CBG | PURCH |
|---|---|---|---|---|---|---|---|---|---|---|---|---|---|---|---|
| Statistik Penjualan | `(halaman tersembunyi) statistik_penjualan:read` | . | . | . | o | . | . | . | . | . | . | . | . | . | . |
| Premi Marketing | `premi_marketing:read` | x | x | x | v | x | x | x | x | x | x | . | x | . | x |

### Lainnya `OTHERS`

| Halaman / fitur | Kunci izin | TEKNISI | ADM-SVC | HEAD-SVC | SUPER | SALES-LPT | FINANCE | GDG-HO | HEAD-LPT | LAPTOP | BESTCARE | H-GDG-HO | HRD | HEAD-CBG | PURCH |
|---|---|---|---|---|---|---|---|---|---|---|---|---|---|---|---|
| Settings | `master_config_system:read` | . | . | . | v | . | . | . | . | . | . | . | . | . | . |
| Feedback Aplikasi | `feedback:create` | . | . | . | v | . | . | . | . | . | . | . | . | . | . |
| Tanya Jawab & Q&A | `feedback:read` | . | . | . | v | . | . | . | . | . | . | . | . | . | . |
| Tutorial | `feedback:read` | . | . | . | v | . | . | . | . | . | . | . | . | . | . |
| Inbox Feedback & Q&A | `feedback:read` | . | . | . | v | . | . | . | . | . | . | . | . | . | . |

Hasil lengkap dalam bentuk tabel Excel: `permission-matrix.csv` (database aktif) dan `permission-matrix-dev.csv` (database dev).

---

## 5. Profil per jabatan (yang ada orangnya)

### Teknisi Best Service — 16 orang, 35 izin

- Kode: `TEKNISI_BESTSERVICE`
- Modul yang muncul: Menu Karyawan, Best Service

**Menu yang muncul**

- Menu Karyawan: Profil Karyawan, Absensi Saya, Pengajuan Cuti / Izin
- Best Service: Service Tiket (Service), Req Part


**Bisa melakukan (tambah/ubah/hapus)**

- Absensi (self-service) — create, delete, update
- Tarif jasa servis — create, delete, update
- Profil karyawan — create, delete, update
- Request part teknisi — create, delete, update
- Req part servis — create, delete, update
- Tiket servis — create, delete, update
- Pengajuan cuti/izin — create, delete, update
- Servis — create, delete, update

**Hanya boleh melihat (tidak boleh menambah/mengubah/menghapus)**

Config system.

**Modul yang tidak muncul sama sekali**: Holding, KPI, HR & Payroll, Keuangan & Laporan, Merchandising & Commercial, Gudang HO, Best Laptop, BESTCARE, Marketing, Lainnya.

**Orang di jabatan ini**

| Nama | Email | Outlet | Status |
|---|---|---|---|
| Abdul Khabir | abdulkhabir0206@gmail.com | Bestservice Banjarmasin | ACTIVE |
| Adzkiya Nur Yani | adzkiyanuryani252@gmail.com | Bestservice Banjarmasin | ACTIVE |
| Bakti Alpihuda | baktialpihuda@gmail.com | Head Office / Pusat | ACTIVE |
| Bakti Nugroho | nbakti11@gmail.com | Bestservice Bogor | ACTIVE |
| Beni (Teknisi) | beni.bestservice.id@gmail.com | Service Point Bandung | ACTIVE |
| Dimas Ardianysah | dimaspakot31@gmail.com | Bestservice Sukabumi | ACTIVE |
| Habib Soleh | h4bibsoleh@gmail.com | Bestservice Bogor | ACTIVE |
| Ibnu Ahdiat | ibnugraahdiat99@gmail.com | Bestservice Bogor | ACTIVE |
| Ilham Taufik  Yusuf | ilhamtyusuf@gmail.com | Bestservice Bogor | ACTIVE |
| Imam Syapaat | imamsyapaat@gmail.com | Bestservice Sukabumi | ACTIVE |
| Lukman Hakim | haq.iemm@gmail.com | Bestservice Sukabumi | ACTIVE |
| Mardani | mardanidani191919@gmail.com | Bestservice Banjarmasin | ACTIVE |
| Moch Hadiyansah  | hadiyansah34@gmail.com | Service Point Bandung | ACTIVE |
| Muhammad Arif Rizky | rizkylimzy@gmail.com | Bestservice Sukabumi | ACTIVE |
| Satria Muhammad Akmal Aulia | satriamuhammad0623@gmail.com | Bestservice Sukabumi | ACTIVE |
| Zikri Hasbi (intern) | muhammadzikrihasbialloh@gmail.com | Service Point Bandung | ACTIVE |

### Admin Best Service — 15 orang, 85 izin

- Kode: `ADMIN_BESTSERVICE`
- Modul yang muncul: Menu Karyawan, Merchandising & Commercial, Best Service

**Menu yang muncul**

- Menu Karyawan: Profil Karyawan, Absensi Saya, Pengajuan Cuti / Izin
- Merchandising & Commercial: Tarif Jasa Servis
- Best Service: Gudang Bestservice, POS Bestservice, Service Tiket (Service), Manajemen SJ, Req Oprasional, Req Part, Stock Opname Sparepart, Jadwal Shift Service


**Halaman tersembunyi yang tetap bisa dibuka lewat tautan langsung** (izinnya ada, tetapi tidak ditampilkan di panel kiri): Garansi BestCare.

**Bisa melakukan (tambah/ubah/hapus)**

- Absensi (self-service) — create, delete, update
- Bestcare — create, delete, update
- Komersial bestcare — create, delete, update
- Tarif jasa servis — create, delete, update
- Data pelanggan — create, delete, update
- Profil karyawan — create, delete, update
- Request operasional — create, delete, update
- Request part teknisi — create, delete, update
- Req operasional servis — create, delete, update
- Req part servis — create, delete, update
- POS Bestservice — create, delete, update
- Stock opname sparepart — create, delete, update
- Tiket servis — create, delete, update
- SJ sparepart — create, delete, update
- Manajemen stok — create, delete, update
- Manajemen SJ — create, delete, update
- Pengajuan cuti/izin — create, delete, update
- POS servis — create, delete, update
- Servis — create, delete, update

**Hanya boleh melihat (tidak boleh menambah/mengubah/menghapus)**

Config system, Gudang bestservice.

**Modul yang tidak muncul sama sekali**: Holding, KPI, HR & Payroll, Keuangan & Laporan, Gudang HO, Best Laptop, BESTCARE, Marketing, Lainnya.

**Orang di jabatan ini**

| Nama | Email | Outlet | Status |
|---|---|---|---|
| Agus Al-Ridha | agusalridha07@gmail.com | Bestservice Banjarmasin | ACTIVE |
| Bakti N | baktinugroho823@gmail.com | Bestservice Bogor | ACTIVE |
| Beni | benininie@gmail.com | Service Point Bandung | ACTIVE |
| Bima Pandu Pratama | bimapandupratama.bestservice.id@gmail.com | Service Point Bandung | ACTIVE |
| Dani (SA) | daniyahahaha@gmail.com | Bestservice Banjarmasin | ACTIVE |
| Dian Prasetyo | dprasmail@gmail.com | Head Office / Pusat | ACTIVE |
| Habib | utinaslebew@gmail.com | Bestservice Bogor | ACTIVE |
| Ibnu Ahdiat Miharja | ibnugraahdiat46@gmail.com | Bestservice Bogor | ACTIVE |
| Jovi Aplie Rukmana | joviaplierukmana.pfpt@gmail.com | Bestservice Bogor | ACTIVE |
| Nasiem Gahtan Degel | nasiemdegel21@gmail.com | Bestservice Bogor | ACTIVE |
| Raihan Muhamad Rahmat | raihanmuhamad246@gmail.com | Head Office / Pusat | ACTIVE |
| Raihan Muhamad Rahmat | yforger246@gmail.com | Bestservice Sukabumi | ACTIVE |
| SA Umum (lukman) | lukmanhakim3951@gmail.com | Bestservice Sukabumi | ACTIVE |
| Satriamaa | satriamaabest@gmail.com | Bestservice Sukabumi | ACTIVE |
| Zahrah Jelita | zahrahjelita25@gmail.com | Bestservice Bogor | ACTIVE |

### Head Best Service — 12 orang, 148 izin

- Kode: `HEAD_BESTSERVICE`
- Modul yang muncul: Menu Karyawan, Merchandising & Commercial, Best Service

**Menu yang muncul**

- Menu Karyawan: Profil Karyawan, Absensi Saya, Pengajuan Cuti / Izin
- Merchandising & Commercial: Tarif Jasa Servis
- Best Service: Gudang Bestservice, POS Bestservice, Service Tiket (Service), Manajemen SJ, Req Oprasional, Req Part, Stock Opname Sparepart, Jadwal Shift Service


**Halaman tersembunyi yang tetap bisa dibuka lewat tautan langsung** (izinnya ada, tetapi tidak ditampilkan di panel kiri): Garansi BestCare, Supplier / Vendor, Request Stok Gudang, Manajemen PO Sparepart, Pencatatan Barang, Supplier / Vendor.

**Bisa melakukan (tambah/ubah/hapus)**

- Absensi (self-service) — create, delete, update
- Rekap absensi — create, delete, update
- Bestcare — create, delete, update
- Komersial bestcare — create, delete, update
- Harga suku cadang — create, delete, update
- Tarif jasa servis — create, delete, update
- Data pelanggan — create, delete, update
- Profil karyawan — create, delete, update
- Request operasional — create, delete, update
- Request part teknisi — create, delete, update
- Req operasional servis — create, delete, update
- Req part servis — create, delete, update
- Pencatatan barang servis — create, delete, update
- POS Bestservice — create, delete, update
- Stock opname sparepart — create, delete, update
- Request stok servis — create, delete, update
- Supplier servis — create, delete, update
- Tiket servis — create, delete, update
- SJ sparepart — create, delete, update
- PO sparepart — create, delete, update
- Manajemen stok — create, delete, update
- Request stok — create, delete, update
- Manajemen SJ — create, delete, update
- Manajemen PO — create, delete, update
- Pengajuan cuti/izin — create, delete, update
- Master item — create, update
- Master supplier — create, delete, update
- Dashboard — create, delete, update
- Pencatatan barang — create, delete, update
- POS servis — create, delete, update
- Servis — create, delete, update
- Jadwal shift — create, delete, update
- Shift master — create, delete, update

**Hanya boleh melihat (tidak boleh menambah/mengubah/menghapus)**

Config system, Gudang bestservice, Item/barang, Outlet, Pengguna.

**Modul yang tidak muncul sama sekali**: Holding, KPI, HR & Payroll, Keuangan & Laporan, Gudang HO, Best Laptop, BESTCARE, Marketing, Lainnya.

**Orang di jabatan ini**

| Nama | Email | Outlet | Status |
|---|---|---|---|
| Bima Pandu Pratama | bimapp.31@gmail.com | Service Point Bandung | ACTIVE |
| Dede Harun | ddbestlaptop@gmail.com | Service Point Sukabumi | ACTIVE |
| Denis Andika | denisandika506@gmail.com | Bestservice Sukabumi | ACTIVE |
| Dimas Ukin | ucoksalehudin@gmail.com | Bestservice Sukabumi | ACTIVE |
| Head Gudang BGR | serviceadvisorbgr@gmail.com | Bestservice Bogor | ACTIVE |
| Head Gudang BJM | serviceadvisorbjm@gmail.com | Bestservice Banjarmasin | ACTIVE |
| Head Gudang SMI | serviceadvisorsmi@gmail.com | Bestservice Sukabumi | ACTIVE |
| Head Service Bogor | headservicebogor@gmail.com | Bestservice Bogor | ACTIVE |
| Issabella Muliawati | iswrrbella@gmail.com | Bestservice Banjarmasin | ACTIVE |
| Saskia Aulia | saskiaauliap.office@gmail.com | Bestservice Sukabumi | ACTIVE |
| Saskia Aulia Pebrianti | saskiaauliapeb@gmail.com | Bestservice Sukabumi | ACTIVE |
| rizky diego117 | rizkidiego117@gmail.com | Bestservice Sukabumi | ACTIVE |

### Super Admin — 10 orang, 328 izin

- Jabatan sistem: ya (tidak dapat dihapus).
- Kode: `SUPER_ADMIN`
- Modul yang muncul: Menu Karyawan, Holding, KPI, HR & Payroll, Keuangan & Laporan, Gudang HO, Best Laptop, Best Service, BESTCARE, Marketing, Lainnya

**Menu yang muncul**

- Menu Karyawan: Profil Karyawan, Absensi Saya, Pengajuan Cuti / Izin
- Holding: Profil Holding / HO, Outlet, Warehouse / Gudang, Role Management, Kelola Pengguna, Config System, Activity Log
- KPI: Pusat Kontrol Penjualan, Input Funnel Harian, Kelola Target Cabang
- HR & Payroll: Data Karyawan, Dashboard Absensi, Rekap Absensi, Payroll, Shift Master, Shift Assignment
- Keuangan & Laporan: Dashboard Keuangan, Chart of Accounts (COA), Buku Jurnal Umum, Kas & Rekening Bank, Modal & Investor, Aset Tetap Perusahaan, Valuasi Persediaan & Aset Dagang, Utang Usaha (Payables), Piutang Usaha (Receivables), Approval PO, Beban Operasional, Pembayaran Operasional, Transaksi Penjualan Outlet, Laporan Keuangan & Neraca, Pajak & Fiskal (Tax)
- Gudang HO: Supplier / Vendor, QC Checklist, Manajemen Stok, Stock Opname, Manajemen PO, Quality control, Manajemen Retur, Manajemen SJ, Request Part Teknisi, Request Stok Gudang, Pencatatan Barang
- Best Laptop: Gudang Bestlaptop, POS Bestlaptop, Manajemen SJ, Reture Penjualan, Stock Opname Laptop, Jadwal Shift Toko
- Best Service: Gudang Bestservice, POS Bestservice, Service Tiket (Service), Manajemen SJ, Req Oprasional, Req Part, Stock Opname Sparepart, Jadwal Shift Service
- BESTCARE: Data Pelanggan, Premi Basecare, Tracking Garansi, Bestcare Unit
- Marketing: Premi Marketing
- Lainnya: Settings, Feedback Aplikasi, Tanya Jawab & Q&A, Tutorial, Inbox Feedback & Q&A


**Halaman tersembunyi yang tetap bisa dibuka lewat tautan langsung** (izinnya ada, tetapi tidak ditampilkan di panel kiri): Garansi BestCare, Quality Control (QC), QC Checklist, Manajemen PO Laptop, Manajemen Retur Unit, Supplier / Vendor, Request Stok Gudang, Manajemen PO Sparepart, Pencatatan Barang, Supplier / Vendor, Statistik Penjualan.

**Bisa melakukan (tambah/ubah/hapus)**

- Activity log — create, delete, update
- Absensi (self-service) — create, delete, update
- Rekap absensi — create, delete, update
- Bestcare — create, delete, update
- Chart of Accounts — create, delete, update
- Komersial bestcare — create, delete, update
- Harga suku cadang — create, delete, update
- Harga komersial — create, delete, update
- Tarif jasa servis — create, delete, update
- Config system — create, delete, update
- Data pelanggan — create, delete, update
- Profil karyawan — create, delete, update
- Approval PO — create, delete, update
- Unit bestcare — create, delete, update
- Pembelian langsung — create, delete, update
- Beban operasional — create, delete, update
- Req operasional laptop — create, delete, update
- POS Bestlaptop — create, delete, update
- QC laptop — create, delete, update
- QC checklist laptop — create, delete, update
- Retur unit laptop — create, delete, update
- Stock opname laptop — create, delete, update
- Supplier laptop — create, delete, update
- SJ laptop — create, delete, update
- PO laptop — create, delete, update
- Pembayaran operasional — create, delete, update
- Request operasional — create, delete, update
- Request part teknisi — create, delete, update
- Quality control — create, delete, update
- Manajemen retur — create, delete, update
- Transaksi penjualan outlet — create, delete, update
- Req operasional servis — create, delete, update
- Req part servis — create, delete, update
- Pencatatan barang servis — create, delete, update
- POS Bestservice — create, delete, update
- Stock opname sparepart — create, delete, update
- Request stok servis — create, delete, update
- Supplier servis — create, delete, update
- Tiket servis — create, delete, update
- SJ sparepart — create, delete, update
- PO sparepart — create, delete, update
- Manajemen stok — create, delete, update
- Request stok — create, delete, update
- Manajemen SJ — create, delete, update
- Manajemen PO — create, delete, update
- Feedback & Q&A — create, delete, update
- Keuangan (jurnal, kas, laporan) — create, delete, update
- Administrasi HR — create, delete, update
- Persediaan — create, delete, update
- Item/barang — create, delete, update
- KPI — create, delete, update
- Pengajuan cuti/izin — create, delete, update
- Profil holding — create, delete, update
- Config system — create, delete, update
- Master item — create, delete, update
- Outlet — create, delete, update
- QC checklist — create, delete, update
- Jabatan & izin — create, delete, update
- Master supplier — create, delete, update
- Data karyawan / pengguna — create, delete, update
- Warehouse — create, delete, update
- Dashboard — create, delete, update
- Outlet — create, delete, update
- Pencatatan barang — create, delete, update
- Payroll — create, delete, update
- POS servis — create, delete, update
- Premi marketing — create, delete, update
- Jabatan — create, delete, update
- Penjualan — create, delete, update
- Retur penjualan pelanggan — create, delete, update
- Servis — create, delete, update
- Jadwal shift — create, delete, update
- Shift master — create, delete, update
- Statistik penjualan — create, delete, update
- Supplier — create, delete, update
- Pengguna — create, delete, update

**Hanya boleh melihat (tidak boleh menambah/mengubah/menghapus)**

Gudang bestlaptop, Gudang bestservice.

**Modul yang tidak muncul sama sekali**: Merchandising & Commercial.

**Orang di jabatan ini**

| Nama | Email | Outlet | Status |
|---|---|---|---|
| Ariij bangkit naufal | ariijbnaufal@gmail.com | Head Office / Pusat | ACTIVE |
| Fazri Maulana | fazrirm97@gmail.com | Head Office / Pusat | ACTIVE |
| Firman Hikmatullah | firmanhikmatullah@gmail.com | Head Office / Pusat | ACTIVE |
| Managament | management@bestlaptop.id | Head Office / Pusat | ACTIVE |
| Muhamad Ilham | ilhamryota28@gmail.com | Head Office Warehouse | ACTIVE |
| Rihad Aulia Rahman | rihadauliarahman88@gmail.com | Head Office / Pusat | ACTIVE |
| Rizal Permana Sidik | rizalpermanasidik@gmail.com | Head Office / Pusat | ACTIVE |
| Rizki Kurnia | rizkidiego614@gmail.com | Head Office / Pusat | ACTIVE |
| Tim Support HO | timsupportsmi@gmail.com | Head Office / Pusat | ACTIVE |
| faisal romli | faisalromliz@gmail.com | Head Office / Pusat | ACTIVE |

### Sales Best Laptop — 6 orang, 42 izin

- Kode: `SALES_BEST_LAPTOP`
- Modul yang muncul: Menu Karyawan, Best Laptop

**Menu yang muncul**

- Menu Karyawan: Profil Karyawan, Absensi Saya, Pengajuan Cuti / Izin
- Best Laptop: Gudang Bestlaptop, POS Bestlaptop, Reture Penjualan, Stock Opname Laptop


**Bisa melakukan (tambah/ubah/hapus)**

- Absensi (self-service) — create, delete, update
- Data pelanggan — create, update
- Profil karyawan — create, delete, update
- Req operasional laptop — create, delete, update
- POS Bestlaptop — create, delete, update
- Pengajuan cuti/izin — create, delete, update
- Dashboard — create, delete, update
- Penjualan — create, delete, update
- Retur penjualan pelanggan — create, delete, update

**Hanya boleh melihat (tidak boleh menambah/mengubah/menghapus)**

Bestcare, Harga komersial, Manajemen stok, Pengguna.

**Modul yang tidak muncul sama sekali**: Holding, KPI, HR & Payroll, Keuangan & Laporan, Merchandising & Commercial, Gudang HO, Best Service, BESTCARE, Marketing, Lainnya.

**Orang di jabatan ini**

| Nama | Email | Outlet | Status |
|---|---|---|---|
| Ahmad riza | ahmadriza946@gmail.com | Bestlaptop Banjarmasin | ACTIVE |
| Bakti Alpihuda | bedahdata.id@gmail.com | Bestlaptop Sukabumi | ACTIVE |
| Lutfia Hijriani | lutpiaaa2@gmail.com | Bestlaptop Banjarmasin | ACTIVE |
| Moch. Irsan Rivansyah | irsanrivan@gmail.com | Bestlaptop Sukabumi | ACTIVE |
| Nurrinawati | nurrinawati1618@gmail.com | Bestlaptop Banjarmasin | ACTIVE |
| Safwan Zakirin | zakirinchef04@gmail.com | Bestlaptop Banjarmasin | ACTIVE |

### Finance — 3 orang, 41 izin

- Kode: `FINANCE`
- Modul yang muncul: Menu Karyawan, Keuangan & Laporan

**Menu yang muncul**

- Menu Karyawan: Profil Karyawan, Absensi Saya, Pengajuan Cuti / Izin
- Keuangan & Laporan: Dashboard Keuangan, Chart of Accounts (COA), Buku Jurnal Umum, Kas & Rekening Bank, Modal & Investor, Aset Tetap Perusahaan, Valuasi Persediaan & Aset Dagang, Utang Usaha (Payables), Piutang Usaha (Receivables), Approval PO, Beban Operasional, Pembayaran Operasional, Transaksi Penjualan Outlet, Laporan Keuangan & Neraca, Pajak & Fiskal (Tax)


**Bisa melakukan (tambah/ubah/hapus)**

- Absensi (self-service) — create, delete, update
- Chart of Accounts — create, delete, update
- Profil karyawan — create, delete, update
- Approval PO — create, delete, update
- Beban operasional — create, delete, update
- Pembayaran operasional — create, delete, update
- Transaksi penjualan outlet — create, delete, update
- Keuangan (jurnal, kas, laporan) — create, delete, update
- Pengajuan cuti/izin — create, delete, update
- Dashboard — create, delete, update

**Modul yang tidak muncul sama sekali**: Holding, KPI, HR & Payroll, Merchandising & Commercial, Gudang HO, Best Laptop, Best Service, BESTCARE, Marketing, Lainnya.

**Orang di jabatan ini**

| Nama | Email | Outlet | Status |
|---|---|---|---|
| Bintang Rahmat Putra | bintangrp.bestlaptop@gmail.com | Head Office / Pusat | ACTIVE |
| Kevin Maulana | fazriramdhanmaulana@gmail.com | Bestlaptop Sukabumi | ACTIVE |
| Resha Apria Fatmawati | reshaaf04@gmail.com | Head Office / Pusat | ACTIVE |

### Gudang HO — 3 orang, 60 izin

- Kode: `GUDANG_HO`
- Modul yang muncul: Menu Karyawan, Gudang HO, Best Laptop

**Menu yang muncul**

- Menu Karyawan: Profil Karyawan, Absensi Saya, Pengajuan Cuti / Izin
- Gudang HO: Supplier / Vendor, QC Checklist, Manajemen Stok, Stock Opname, Manajemen PO, Quality control, Manajemen Retur, Manajemen SJ, Request Part Teknisi, Request Stok Gudang, Pencatatan Barang
- Best Laptop: Gudang Bestlaptop, Manajemen SJ, Stock Opname Laptop


**Halaman tersembunyi yang tetap bisa dibuka lewat tautan langsung** (izinnya ada, tetapi tidak ditampilkan di panel kiri): Quality Control (QC), Manajemen Retur Unit, Supplier / Vendor, Request Stok Gudang.

**Bisa melakukan (tambah/ubah/hapus)**

- Absensi (self-service) — create, delete, update
- Profil karyawan — create, delete, update
- Request part teknisi — create, delete, update
- Quality control — create, delete, update
- Manajemen retur — create, delete, update
- Manajemen stok — create, delete, update
- Request stok — create, delete, update
- Manajemen SJ — create, delete, update
- Manajemen PO — create, delete, update
- Persediaan — create, delete, update
- Pengajuan cuti/izin — create, delete, update
- QC checklist — create, delete, update
- Master supplier — create, delete, update
- Pencatatan barang — create, delete, update
- Supplier — create, delete, update

**Modul yang tidak muncul sama sekali**: Holding, KPI, HR & Payroll, Keuangan & Laporan, Merchandising & Commercial, Best Service, BESTCARE, Marketing, Lainnya.

**Orang di jabatan ini**

| Nama | Email | Outlet | Status |
|---|---|---|---|
| Rabbani Adina Rizkika | rabbani.raina@gmail.com | Head Office Warehouse | ACTIVE |
| Test Gudang | dianprasmail@gmail.com | Bestlaptop Sukabumi | ACTIVE |
| Wildan Muhammad Ramdani | mrwildan777@gmail.com | Head Office Warehouse | ACTIVE |

### Head Best Laptop — 3 orang, 111 izin

- Kode: `HEAD_BEST_LAPTOP`
- Modul yang muncul: Menu Karyawan, Best Laptop

**Menu yang muncul**

- Menu Karyawan: Profil Karyawan, Absensi Saya, Pengajuan Cuti / Izin
- Best Laptop: Gudang Bestlaptop, POS Bestlaptop, Manajemen SJ, Rek Oprasional, Stock Opname Laptop, Jadwal Shift Toko


**Halaman tersembunyi yang tetap bisa dibuka lewat tautan langsung** (izinnya ada, tetapi tidak ditampilkan di panel kiri): Quality Control (QC), QC Checklist, Manajemen PO Laptop, Manajemen Retur Unit, Supplier / Vendor.

**Bisa melakukan (tambah/ubah/hapus)**

- Absensi (self-service) — create
- Harga komersial — create, delete, update
- Data pelanggan — create, update
- Req operasional laptop — create, delete, update
- POS Bestlaptop — create, delete, update
- QC laptop — create, delete, update
- QC checklist laptop — create, delete, update
- Retur unit laptop — create, delete, update
- Stock opname laptop — create, delete, update
- Supplier laptop — create, delete, update
- SJ laptop — create, delete, update
- PO laptop — create, delete, update
- Request operasional — create, delete, update
- Quality control — create, delete, update
- Manajemen retur — create, delete, update
- Manajemen stok — create, delete, update
- Manajemen SJ — create, delete, update
- Manajemen PO — create, delete, update
- Persediaan — create, delete, update
- Pengajuan cuti/izin — update
- Master item — update
- QC checklist — create, delete, update
- Master supplier — create, delete, update
- Dashboard — create, delete, update
- Penjualan — create, delete, update
- Jadwal shift — create, update

**Hanya boleh melihat (tidak boleh menambah/mengubah/menghapus)**

Config system, Gudang bestlaptop, Shift master, Pengguna.

**Modul yang tidak muncul sama sekali**: Holding, KPI, HR & Payroll, Keuangan & Laporan, Merchandising & Commercial, Gudang HO, Best Service, BESTCARE, Marketing, Lainnya.

**Orang di jabatan ini**

| Nama | Email | Outlet | Status |
|---|---|---|---|
| Kintan S Anggrisky | kintansabill@gmail.com | Bestlaptop Sukabumi | ACTIVE |
| Mulyansyh | mulyansyh@gmail.com | Bestlaptop Sukabumi | ACTIVE |
| Nur Cholid Majid | cholid.kazuraba123@gmail.com | Bestlaptop Banjarmasin | ACTIVE |

### Best Laptop — 1 orang, 60 izin

- Kode: `BESTLAPTOP`
- Modul yang muncul: Menu Karyawan, Best Laptop

**Menu yang muncul**

- Menu Karyawan: Profil Karyawan, Absensi Saya, Pengajuan Cuti / Izin
- Best Laptop: Gudang Bestlaptop, POS Bestlaptop, Manajemen SJ, Reture Penjualan, Stock Opname Laptop


**Halaman tersembunyi yang tetap bisa dibuka lewat tautan langsung** (izinnya ada, tetapi tidak ditampilkan di panel kiri): Manajemen Retur Unit.

**Bisa melakukan (tambah/ubah/hapus)**

- Absensi (self-service) — create, delete, update
- Profil karyawan — create, delete, update
- Req operasional laptop — create, delete, update
- POS Bestlaptop — create, delete, update
- Retur unit laptop — create, delete, update
- Stock opname laptop — create, delete, update
- SJ laptop — create, delete, update
- Request operasional — create, delete, update
- Manajemen stok — create, delete, update
- Manajemen SJ — create, delete, update
- Pengajuan cuti/izin — create, delete, update
- Penjualan — create, delete, update
- Retur penjualan pelanggan — create, delete, update

**Hanya boleh melihat (tidak boleh menambah/mengubah/menghapus)**

Config system, Gudang bestlaptop.

**Modul yang tidak muncul sama sekali**: Holding, KPI, HR & Payroll, Keuangan & Laporan, Merchandising & Commercial, Gudang HO, Best Service, BESTCARE, Marketing, Lainnya.

**Orang di jabatan ini**

| Nama | Email | Outlet | Status |
|---|---|---|---|
| Rula Zalfaa Tsabita | rulazalfaa@gmail.com | Head Office / Pusat | ACTIVE |

### Bestcare — 1 orang, 24 izin

- Kode: `BESTCARE`
- Modul yang muncul: Menu Karyawan, BESTCARE

**Menu yang muncul**

- Menu Karyawan: Profil Karyawan, Absensi Saya, Pengajuan Cuti / Izin
- BESTCARE: Data Pelanggan, Premi Basecare, Tracking Garansi, Bestcare Unit


**Halaman tersembunyi yang tetap bisa dibuka lewat tautan langsung** (izinnya ada, tetapi tidak ditampilkan di panel kiri): Garansi BestCare.

**Bisa melakukan (tambah/ubah/hapus)**

- Absensi (self-service) — create, delete, update
- Bestcare — create, delete, update
- Komersial bestcare — create, delete, update
- Data pelanggan — create, delete, update
- Profil karyawan — create, delete, update
- Pengajuan cuti/izin — create, delete, update

**Modul yang tidak muncul sama sekali**: Holding, KPI, HR & Payroll, Keuangan & Laporan, Merchandising & Commercial, Gudang HO, Best Laptop, Best Service, Marketing, Lainnya.

**Orang di jabatan ini**

| Nama | Email | Outlet | Status |
|---|---|---|---|
| Azkia Qonita | azkiaqonita.w@gmail.com | Head Office / Pusat | ACTIVE |

### HEAD_GUDANG HO — 1 orang, 76 izin

- **Kode jabatan kosong** (bisa membingungkan saat penugasan).
- Kode: `(tidak ada)`
- Modul yang muncul: Menu Karyawan, Holding, HR & Payroll, Gudang HO, Best Laptop, Best Service

**Menu yang muncul**

- Menu Karyawan: Profil Karyawan, Absensi Saya, Pengajuan Cuti / Izin
- Holding: Kelola Pengguna
- HR & Payroll: Data Karyawan, Shift Master, Shift Assignment
- Gudang HO: Supplier / Vendor, QC Checklist, Manajemen Stok, Stock Opname, Manajemen PO, Quality control, Manajemen Retur, Manajemen SJ, Request Part Teknisi, Request Stok Gudang, Pencatatan Barang
- Best Laptop: Gudang Bestlaptop, Manajemen SJ, Stock Opname Laptop, Jadwal Shift Toko
- Best Service: Gudang Bestservice, Manajemen SJ, Req Oprasional, Req Part, Stock Opname Sparepart, Jadwal Shift Service


**Halaman tersembunyi yang tetap bisa dibuka lewat tautan langsung** (izinnya ada, tetapi tidak ditampilkan di panel kiri): Quality Control (QC), Manajemen Retur Unit, Supplier / Vendor, Request Stok Gudang.

**Bisa melakukan (tambah/ubah/hapus)**

- Absensi (self-service) — create
- Pembelian langsung — create, delete, update
- Request operasional — create, delete, update
- Request part teknisi — create, delete, update
- Quality control — create, delete, update
- Manajemen retur — create, delete, update
- Manajemen stok — create, delete, update
- Request stok — create, delete, update
- Manajemen SJ — create, delete, update
- Manajemen PO — create, delete, update
- Persediaan — create, delete, update
- Item/barang — create, delete, update
- Pengajuan cuti/izin — update
- Master item — create, delete, update
- QC checklist — create, delete, update
- Master supplier — create, delete, update
- Pencatatan barang — create, delete, update
- Jadwal shift — create, update
- Supplier — create, delete, update

**Hanya boleh melihat (tidak boleh menambah/mengubah/menghapus)**

Data karyawan / pengguna, Dashboard, Outlet, Shift master, Pengguna.

**Modul yang tidak muncul sama sekali**: KPI, Keuangan & Laporan, Merchandising & Commercial, BESTCARE, Marketing, Lainnya.

**Orang di jabatan ini**

| Nama | Email | Outlet | Status |
|---|---|---|---|
| Head Gudang BDG | serviceadvisorbdg@gmail.com | Service Point Bandung | ACTIVE |

### HRD — 1 orang, 69 izin

- Kode: `HRD`
- Modul yang muncul: Menu Karyawan, HR & Payroll

**Menu yang muncul**

- Menu Karyawan: Profil Karyawan, Absensi Saya, Pengajuan Cuti / Izin
- HR & Payroll: Data Karyawan, Dashboard Absensi, Rekap Absensi, Payroll, Shift Master, Shift Assignment


**Halaman tersembunyi yang tetap bisa dibuka lewat tautan langsung** (izinnya ada, tetapi tidak ditampilkan di panel kiri): Supplier / Vendor.

**Bisa melakukan (tambah/ubah/hapus)**

- Absensi (self-service) — create, delete, update
- Rekap absensi — create, delete, update
- Config system — create, delete, update
- Profil karyawan — create, delete, update
- Administrasi HR — create, delete, update
- Persediaan — create, delete, update
- Item/barang — create, delete, update
- Pengajuan cuti/izin — create, delete, update
- Jabatan & izin — create, delete, update
- Data karyawan / pengguna — create, delete, update
- Outlet — create, delete, update
- Payroll — create, delete, update
- Jabatan — create, delete, update
- Jadwal shift — create, delete, update
- Shift master — create, delete, update
- Supplier — create, delete, update
- Pengguna — create, delete, update

**Hanya boleh melihat (tidak boleh menambah/mengubah/menghapus)**

Dashboard.

**Modul yang tidak muncul sama sekali**: Holding, KPI, Keuangan & Laporan, Merchandising & Commercial, Gudang HO, Best Laptop, Best Service, BESTCARE, Marketing, Lainnya.

**Orang di jabatan ini**

| Nama | Email | Outlet | Status |
|---|---|---|---|
| Pirzal Mulyadi | hrd.esteraprima@gmail.com | Head Office / Pusat | ACTIVE |

### Head Cabang (Laptop & Service) — 1 orang, 219 izin

- Kode: `HEAD_DUAL_OUTLET`
- Modul yang muncul: Menu Karyawan, HR & Payroll, Gudang HO, Best Laptop, Best Service, BESTCARE

**Menu yang muncul**

- Menu Karyawan: Profil Karyawan, Absensi Saya, Pengajuan Cuti / Izin
- HR & Payroll: Dashboard Absensi, Rekap Absensi, Shift Master, Shift Assignment
- Gudang HO: Supplier / Vendor, QC Checklist, Manajemen Stok, Stock Opname, Manajemen PO, Quality control, Manajemen Retur, Manajemen SJ, Request Part Teknisi, Request Stok Gudang, Pencatatan Barang
- Best Laptop: Gudang Bestlaptop, POS Bestlaptop, Manajemen SJ, Stock Opname Laptop, Jadwal Shift Toko
- Best Service: Gudang Bestservice, POS Bestservice, Service Tiket (Service), Manajemen SJ, Req Oprasional, Req Part, Stock Opname Sparepart, Jadwal Shift Service
- BESTCARE: Data Pelanggan, Premi Basecare, Tracking Garansi, Bestcare Unit


**Halaman tersembunyi yang tetap bisa dibuka lewat tautan langsung** (izinnya ada, tetapi tidak ditampilkan di panel kiri): Garansi BestCare, Quality Control (QC), QC Checklist, Manajemen PO Laptop, Manajemen Retur Unit, Supplier / Vendor, Request Stok Gudang, Manajemen PO Sparepart, Pencatatan Barang, Supplier / Vendor.

**Bisa melakukan (tambah/ubah/hapus)**

- Absensi (self-service) — create, delete, update
- Rekap absensi — create, delete, update
- Bestcare — create, delete, update
- Komersial bestcare — create, delete, update
- Harga suku cadang — create, delete, update
- Harga komersial — create, delete, update
- Tarif jasa servis — create, delete, update
- Data pelanggan — create, delete, update
- Profil karyawan — create, delete, update
- Req operasional laptop — create, delete, update
- POS Bestlaptop — create, delete, update
- QC laptop — create, delete, update
- QC checklist laptop — create, delete, update
- Retur unit laptop — create, delete, update
- Stock opname laptop — create, delete, update
- Supplier laptop — create, delete, update
- SJ laptop — create, delete, update
- PO laptop — create, delete, update
- Request operasional — create, delete, update
- Request part teknisi — create, delete, update
- Quality control — create, delete, update
- Manajemen retur — create, delete, update
- Req operasional servis — create, delete, update
- Req part servis — create, delete, update
- Pencatatan barang servis — create, delete, update
- POS Bestservice — create, delete, update
- Stock opname sparepart — create, delete, update
- Request stok servis — create, delete, update
- Supplier servis — create, delete, update
- Tiket servis — create, delete, update
- SJ sparepart — create, delete, update
- PO sparepart — create, delete, update
- Manajemen stok — create, delete, update
- Request stok — create, delete, update
- Manajemen SJ — create, delete, update
- Manajemen PO — create, delete, update
- Persediaan — create, delete, update
- Pengajuan cuti/izin — create, delete, update
- Master item — create, update
- QC checklist — create, delete, update
- Master supplier — create, delete, update
- Dashboard — create, delete, update
- Pencatatan barang — create, delete, update
- POS servis — create, delete, update
- Penjualan — create, delete, update
- Servis — create, delete, update
- Jadwal shift — create, delete, update
- Shift master — create, delete, update

**Hanya boleh melihat (tidak boleh menambah/mengubah/menghapus)**

Config system, Gudang bestlaptop, Gudang bestservice, Item/barang, Outlet, Pengguna.

**Modul yang tidak muncul sama sekali**: Holding, KPI, Keuangan & Laporan, Merchandising & Commercial, Marketing, Lainnya.

**Orang di jabatan ini**

| Nama | Email | Outlet | Status |
|---|---|---|---|
| Ruddy Indra Frahasta | ruddyindra12@gmail.com | Bestlaptop Bogor | ACTIVE |

### PURCHASING — 1 orang, 14 izin

- **Kode jabatan kosong** (bisa membingungkan saat penugasan).
- Kode: `(tidak ada)`
- Modul yang muncul: Menu Karyawan, Best Laptop

**Menu yang muncul**

- Menu Karyawan: Profil Karyawan, Absensi Saya, Pengajuan Cuti / Izin
- Best Laptop: Gudang Bestlaptop


**Halaman tersembunyi yang tetap bisa dibuka lewat tautan langsung** (izinnya ada, tetapi tidak ditampilkan di panel kiri): Supplier / Vendor.

**Bisa melakukan (tambah/ubah/hapus)**

- Absensi (self-service) — create
- Persediaan — create, delete, update
- Item/barang — create, delete, update
- Outlet — update

**Hanya boleh melihat (tidak boleh menambah/mengubah/menghapus)**

Dashboard, Supplier.

**Modul yang tidak muncul sama sekali**: Holding, KPI, HR & Payroll, Keuangan & Laporan, Merchandising & Commercial, Gudang HO, Best Service, BESTCARE, Marketing, Lainnya.

**Orang di jabatan ini**

| Nama | Email | Outlet | Status |
|---|---|---|---|
| M Wahyu Putra | mwahyuputra.my@gmail.com | Head Office / Pusat | ACTIVE |

---

## 6. Seluruh pengguna (database AKTIF, 74 akun)

| Jabatan | Nama | Outlet | Status |
|---|---|---|---|
| Admin Best Service | Agus Al-Ridha | Bestservice Banjarmasin | ACTIVE |
| Admin Best Service | Bakti N | Bestservice Bogor | ACTIVE |
| Admin Best Service | Beni | Service Point Bandung | ACTIVE |
| Admin Best Service | Bima Pandu Pratama | Service Point Bandung | ACTIVE |
| Admin Best Service | Dani (SA) | Bestservice Banjarmasin | ACTIVE |
| Admin Best Service | Dian Prasetyo | Head Office / Pusat | ACTIVE |
| Admin Best Service | Habib | Bestservice Bogor | ACTIVE |
| Admin Best Service | Ibnu Ahdiat Miharja | Bestservice Bogor | ACTIVE |
| Admin Best Service | Jovi Aplie Rukmana | Bestservice Bogor | ACTIVE |
| Admin Best Service | Nasiem Gahtan Degel | Bestservice Bogor | ACTIVE |
| Admin Best Service | Raihan Muhamad Rahmat | Head Office / Pusat | ACTIVE |
| Admin Best Service | Raihan Muhamad Rahmat | Bestservice Sukabumi | ACTIVE |
| Admin Best Service | SA Umum (lukman) | Bestservice Sukabumi | ACTIVE |
| Admin Best Service | Satriamaa | Bestservice Sukabumi | ACTIVE |
| Admin Best Service | Zahrah Jelita | Bestservice Bogor | ACTIVE |
| Best Laptop | Rula Zalfaa Tsabita | Head Office / Pusat | ACTIVE |
| Bestcare | Azkia Qonita | Head Office / Pusat | ACTIVE |
| Finance | Bintang Rahmat Putra | Head Office / Pusat | ACTIVE |
| Finance | Kevin Maulana | Bestlaptop Sukabumi | ACTIVE |
| Finance | Resha Apria Fatmawati | Head Office / Pusat | ACTIVE |
| Gudang HO | Rabbani Adina Rizkika | Head Office Warehouse | ACTIVE |
| Gudang HO | Test Gudang | Bestlaptop Sukabumi | ACTIVE |
| Gudang HO | Wildan Muhammad Ramdani | Head Office Warehouse | ACTIVE |
| HEAD_GUDANG HO | Head Gudang BDG | Service Point Bandung | ACTIVE |
| HRD | Pirzal Mulyadi | Head Office / Pusat | ACTIVE |
| Head Best Laptop | Kintan S Anggrisky | Bestlaptop Sukabumi | ACTIVE |
| Head Best Laptop | Mulyansyh | Bestlaptop Sukabumi | ACTIVE |
| Head Best Laptop | Nur Cholid Majid | Bestlaptop Banjarmasin | ACTIVE |
| Head Best Service | Bima Pandu Pratama | Service Point Bandung | ACTIVE |
| Head Best Service | Dede Harun | Service Point Sukabumi | ACTIVE |
| Head Best Service | Denis Andika | Bestservice Sukabumi | ACTIVE |
| Head Best Service | Dimas Ukin | Bestservice Sukabumi | ACTIVE |
| Head Best Service | Head Gudang BGR | Bestservice Bogor | ACTIVE |
| Head Best Service | Head Gudang BJM | Bestservice Banjarmasin | ACTIVE |
| Head Best Service | Head Gudang SMI | Bestservice Sukabumi | ACTIVE |
| Head Best Service | Head Service Bogor | Bestservice Bogor | ACTIVE |
| Head Best Service | Issabella Muliawati | Bestservice Banjarmasin | ACTIVE |
| Head Best Service | Saskia Aulia | Bestservice Sukabumi | ACTIVE |
| Head Best Service | Saskia Aulia Pebrianti | Bestservice Sukabumi | ACTIVE |
| Head Best Service | rizky diego117 | Bestservice Sukabumi | ACTIVE |
| Head Cabang (Laptop & Service) | Ruddy Indra Frahasta | Bestlaptop Bogor | ACTIVE |
| PURCHASING | M Wahyu Putra | Head Office / Pusat | ACTIVE |
| Sales Best Laptop | Ahmad riza | Bestlaptop Banjarmasin | ACTIVE |
| Sales Best Laptop | Bakti Alpihuda | Bestlaptop Sukabumi | ACTIVE |
| Sales Best Laptop | Lutfia Hijriani | Bestlaptop Banjarmasin | ACTIVE |
| Sales Best Laptop | Moch. Irsan Rivansyah | Bestlaptop Sukabumi | ACTIVE |
| Sales Best Laptop | Nurrinawati | Bestlaptop Banjarmasin | ACTIVE |
| Sales Best Laptop | Safwan Zakirin | Bestlaptop Banjarmasin | ACTIVE |
| Super Admin | Ariij bangkit naufal | Head Office / Pusat | ACTIVE |
| Super Admin | Fazri Maulana | Head Office / Pusat | ACTIVE |
| Super Admin | Firman Hikmatullah | Head Office / Pusat | ACTIVE |
| Super Admin | Managament | Head Office / Pusat | ACTIVE |
| Super Admin | Muhamad Ilham | Head Office Warehouse | ACTIVE |
| Super Admin | Rihad Aulia Rahman | Head Office / Pusat | ACTIVE |
| Super Admin | Rizal Permana Sidik | Head Office / Pusat | ACTIVE |
| Super Admin | Rizki Kurnia | Head Office / Pusat | ACTIVE |
| Super Admin | Tim Support HO | Head Office / Pusat | ACTIVE |
| Super Admin | faisal romli | Head Office / Pusat | ACTIVE |
| Teknisi Best Service | Abdul Khabir | Bestservice Banjarmasin | ACTIVE |
| Teknisi Best Service | Adzkiya Nur Yani | Bestservice Banjarmasin | ACTIVE |
| Teknisi Best Service | Bakti Alpihuda | Head Office / Pusat | ACTIVE |
| Teknisi Best Service | Bakti Nugroho | Bestservice Bogor | ACTIVE |
| Teknisi Best Service | Beni (Teknisi) | Service Point Bandung | ACTIVE |
| Teknisi Best Service | Dimas Ardianysah | Bestservice Sukabumi | ACTIVE |
| Teknisi Best Service | Habib Soleh | Bestservice Bogor | ACTIVE |
| Teknisi Best Service | Ibnu Ahdiat | Bestservice Bogor | ACTIVE |
| Teknisi Best Service | Ilham Taufik  Yusuf | Bestservice Bogor | ACTIVE |
| Teknisi Best Service | Imam Syapaat | Bestservice Sukabumi | ACTIVE |
| Teknisi Best Service | Lukman Hakim | Bestservice Sukabumi | ACTIVE |
| Teknisi Best Service | Mardani | Bestservice Banjarmasin | ACTIVE |
| Teknisi Best Service | Moch Hadiyansah  | Service Point Bandung | ACTIVE |
| Teknisi Best Service | Muhammad Arif Rizky | Bestservice Sukabumi | ACTIVE |
| Teknisi Best Service | Satria Muhammad Akmal Aulia | Bestservice Sukabumi | ACTIVE |
| Teknisi Best Service | Zikri Hasbi (intern) | Service Point Bandung | ACTIVE |

---

## 7. Perbandingan database AKTIF vs DEV

| | AKTIF (5438/`esteraone`) | DEV (5439/`esteraone_dev`) |
|---|---|---|
| Jumlah akun | 74 (semua ACTIVE) | 70 (68 ACTIVE, 2 INACTIVE) |
| Jumlah jabatan | 25 | 22 |
| Jabatan terpakai | 14 | 12 |

**Hanya ada di database DEV**: Lukman SA, M.Ilham, Ryota, ilham

**Hanya ada di database AKTIF**: Bakti N, Head Gudang BDG, Head Gudang BGR, Head Gudang BJM, Head Gudang SMI, SA Umum (lukman), Saskia Aulia, Test Gudang

**Jabatan yang hanya ada di database AKTIF**: GUDANG HO, Gudang Best Laptop, Gudang Best Service, HEAD_GUDANG HO, Sales Best Service

**Jabatan yang hanya ada di database DEV**: GUDANG, HEAD_GUDANG

**Jabatan yang jumlah izinnya berbeda antar database**

| Jabatan | Izin di AKTIF | Izin di DEV |
|---|---|---|
| Super Admin | 328 | 133 |
| Head Cabang (Laptop & Service) | 219 | 81 |
| Head Best Laptop | 111 | 23 |
| Head Best Service | 148 | 64 |
| Service Advisor | 150 | 83 |
| TEKNISI | 71 | 17 |
| Admin Best Service | 85 | 54 |
| Best Laptop | 60 | 33 |
| HRD | 69 | 93 |
| FINANCE | 67 | 88 |
| HEAD_SERVICE | 72 | 52 |
| Sales Best Laptop | 42 | 26 |
| Support Best Service | 35 | 20 |
| Teknisi Best Service | 35 | 23 |
| HEAD SALES | 15 | 21 |
| SALES | 45 | 50 |
| PURCHASING | 14 | 16 |

---

## 8. Jabatan tanpa pengguna (database AKTIF)

Jabatan berikut tidak dipegang siapa pun, tetapi menyimpan izin yang cukup besar — perlu ditinjau, dibersihkan, atau dipertahankan dengan alasan yang jelas.

| Jabatan | Kode | Jumlah izin |
|---|---|---|
| Service Advisor | SERVICE_ADVISOR | 150 |
| Gudang Best Service | GUDANG_BEST_SERVICE | 85 |
| Gudang Best Laptop | GUDANG_BEST_LAPTOP | 80 |
| Sales Best Service | SALES_BEST_SERVICE | 80 |
| HEAD_SERVICE | **(tanpa kode)** | 72 |
| TEKNISI | **(tanpa kode)** | 71 |
| FINANCE | **(tanpa kode)** | 67 |
| GUDANG HO | **(tanpa kode)** | 65 |
| SALES | **(tanpa kode)** | 45 |
| Support Best Service | SUPPORT_BESTSERVICE | 35 |
| HEAD SALES | **(tanpa kode)** | 15 |

---

## 9. Batasan dokumen

- Daftar menu dihitung dari **aturan program + izin tersimpan**, bukan dari login satu per satu.
- Halaman yang ditandai `o` tidak muncul di panel kiri, tetapi masih dapat dibuka lewat tautan langsung bila izinnya dimiliki.
- Di sisi server masih ada pemeriksaan tambahan, sehingga bisa terjadi menu muncul namun aksinya ditolak (lihat berkas temuan).
- Dokumen menjadi kedaluwarsa setiap kali izin, jabatan, atau menu diubah — perlu dibuat ulang setelah perubahan.
- Nama karyawan dan email ditulis apa adanya sesuai permintaan.

---

**Berkas pendamping**: `permission-matrix.csv`, `permission-matrix-dev.csv`, `role-users.csv`, `TEMUAN_DAN_REKOMENDASI.md`.
