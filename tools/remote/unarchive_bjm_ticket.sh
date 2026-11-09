#!/usr/bin/env bash
set -u
export PGPASSWORD=supersecurepassword123
PSQL="psql -h 202.74.74.229 -p 5438 -U postgres -d esteraone -v ON_ERROR_STOP=1 -Atc"
TS=$(date +%Y%m%d-%H%M%S)
LOG=/home/dev_ilham/db-backups/unarchive_SRV-20261026-0001_$TS.log

exec > >(tee "$LOG") 2>&1

echo "=== SEBELUM ==="
$PSQL "select 'ticket='||\"ticketNumber\"||' | isArchived='||\"isArchived\"||' | pickedUpAt='||coalesce(\"pickedUpAt\"::text,'NULL')||' | subStatus='||(select name from \"ServiceSubStatus\" where id=t.\"subStatusId\") from \"ServiceTicket\" t where \"ticketNumber\"='SRV-20261026-0001';"

echo
echo "=== UPDATE (satu transaksi) ==="
$PSQL "
BEGIN;
UPDATE \"ServiceTicket\"
SET \"isArchived\"  = false,
    \"pickedUpAt\"  = NULL,
    \"updatedAt\"   = now(),
    \"subStatusId\" = (
      SELECT ss.id FROM \"ServiceSubStatus\" ss
      JOIN \"ServiceStatus\" s ON s.id = ss.\"statusId\"
      WHERE s.name = 'Selesai' AND ss.name = 'Selesai (Belum Diambil)' LIMIT 1
    )
WHERE \"ticketNumber\" = 'SRV-20261026-0001';
SELECT 'rows_affected='||count(*) FROM \"ServiceTicket\" WHERE \"ticketNumber\"='SRV-20261026-0001' AND \"isArchived\"=false AND \"pickedUpAt\" IS NULL;
COMMIT;
"

echo
echo "=== SESUDAH ==="
$PSQL "select 'ticket='||t.\"ticketNumber\"||' | isArchived='||t.\"isArchived\"||' | pickedUpAt='||coalesce(t.\"pickedUpAt\"::text,'NULL')||' | subStatus='||(select name from \"ServiceSubStatus\" where id=t.\"subStatusId\")||' | status='||(select s.name from \"ServiceStatus\" s join \"ServiceSubStatus\" ss on ss.\"statusId\"=s.id where ss.id=t.\"subStatusId\") from \"ServiceTicket\" t where t.\"ticketNumber\"='SRV-20261026-0001';"

echo
echo "=== VERIFIKASI: muncul di daftar AKTIF kanban (isArchived=false) outlet OUT-0002 ==="
$PSQL "select \"ticketNumber\"||' | '||\"outletId\"||' | '||coalesce(\"deviceModel\",'-')||' | selesai_belum_diambil='||(select name from \"ServiceSubStatus\" where id=t.\"subStatusId\") from \"ServiceTicket\" t where \"isArchived\"=false and \"outletId\"='OUT-0002';"

echo
echo "=== KESEHATAN: tiket aktif lain tidak tersentuh ==="
$PSQL "select 'total='||count(*)||' aktif='||count(*) filter (where not \"isArchived\")||' arsip='||count(*) filter (where \"isArchived\") from \"ServiceTicket\";"

echo
echo "=== Perlindungan dari auto-archive ulang ==="
$PSQL "select 'subStatus mengandung Sudah Diambil? '||case when exists(select 1 from \"ServiceSubStatus\" where id=(select \"subStatusId\" from \"ServiceTicket\" where \"ticketNumber\"='SRV-20261026-0001') and name ilike '%Sudah Diambil%') then 'YA (akan ter-arsip lagi!)' else 'TIDAK -> aman' end;"

echo
echo "log tersimpan: $LOG"
