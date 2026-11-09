#!/usr/bin/env bash
set -u
export PGPASSWORD=supersecurepassword123
export PM2_HOME=/home/admin/.pm2
H=202.74.74.229
TS=$(date +%Y%m%d-%H%M%S)

echo "=== A. nginx refs to 8004 / 8003 ==="
grep -rn "8004\|8003" /etc/nginx/ 2>/dev/null | head -10
echo "(none above = no nginx route uses 8004)"

echo "=== B. pm2 process detail (prod-be) ==="
pm2 jlist 2>/dev/null | jq -r '.[] | select(.name=="esteraone-prod-be") | {name,pid,status,uptime:.pm2_env.pm_uptime,cwd:.pm2_env.pm_cwd} | tostring'
echo "=== C. disk space ==="
df -h /home | tail -1
echo "=== D. GREEN_ACTIVE_DB state file ==="
grep -n "GREEN_ACTIVE_DB_FILE" /home/admin/esteraone.admin/src/server.js | head -3

echo "=== E. BACKUP 5438 ==="
mkdir -p /home/dev_ilham/db-backups
OUT=/home/dev_ilham/db-backups/esteraone_PROD_5438_pre-merge_$TS.dump
pg_dump -h $H -p 5438 -U postgres -d esteraone -Fc -f "$OUT" 2>&1
ls -lh "$OUT"
echo "--- verify dump TOC ---"
pg_restore -l "$OUT" 2>/dev/null | grep -c "TABLE DATA" 
echo "--- md5 ---"
md5sum "$OUT"
OUTSQL=/home/dev_ilham/db-backups/esteraone_PROD_5438_pre-merge_$TS.sql.gz
pg_dump -h $H -p 5438 -U postgres -d esteraone | gzip > "$OUTSQL" 2>&1
ls -lh "$OUTSQL"
echo "$OUT" > /tmp/last_backup_path
echo "BACKUP_DONE=$OUT"
