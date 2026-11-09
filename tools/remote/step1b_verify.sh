#!/usr/bin/env bash
set -u
D=$(cat /tmp/last_backup_path)
echo "dump=$D"
echo "=== pg_restore -l head ==="
pg_restore -l "$D" 2>&1 | head -12
echo "=== TOC entries ==="
pg_restore -l "$D" 2>/dev/null | wc -l
echo "=== TABLE DATA entries ==="
pg_restore -l "$D" 2>/dev/null | grep -c "TABLE DATA"
echo "=== plain sql sanity ==="
zcat /home/dev_ilham/db-backups/esteraone_PROD_5438_pre-merge_20261026-120350.sql.gz | tail -5
echo "=== live write check: newest rows now ==="
export PGPASSWORD=supersecurepassword123
psql -h 202.74.74.229 -p 5438 -U postgres -d esteraone -Atc "select max(\"createdAt\") from app_activity_logs;"
psql -h 202.74.74.229 -p 5439 -U postgres -d esteraone_dev -Atc "select max(\"createdAt\") from app_activity_logs;"
echo "=== pre-merge counts (key tables) ==="
for t in Attendance EmployeeShift ServiceTicket ServiceTicketComment app_activity_logs Notification CRMContact ProfileUpdateRequest FinanceTask; do
  printf "%-24s 5438=%s\n" "$t" "$(psql -h 202.74.74.229 -p 5438 -U postgres -d esteraone -Atc "select count(*) from \"$t\";")"
done
