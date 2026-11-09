#!/usr/bin/env bash
set -u
export PGPASSWORD=supersecurepassword123
H=202.74.74.229
echo "=== [1] GREEN BACKEND 8002 (prod + dev vhost) ==="
grep -E "^APP_PORT|^DATABASE_URL" /home/esteraone/esteraone.green/bestlaptop-erp-be/.env
echo -n "proses aktif pid: "
PM2_HOME=/home/admin/.pm2 pm2 jlist 2>/dev/null | jq -r '.[] | select(.name=="esteraone-green-be") | "\(.pid) status=\(.pm2_env.status)"'
echo -n "DB_URL di log terakhir: "
grep "DB_URL" /home/admin/.pm2/logs/esteraone-green-be-out.log | tail -1 | sed 's/.*@/@/'

echo
echo "=== [2] RYOTA QC HUB 8005 ==="
grep -E "^PORT|^TARGET_DB|^DATABASE_URL" /home/esteraone/intergarasi.ryota/ryota-qc-hub/.env
curl -s -m 5 http://127.0.0.1:8005/api/v1/qc/metrics | head -c 400; echo

echo
echo "=== [3] ADMIN PANEL pools ==="
grep -nE "PGPORT|PGDATABASE|PGDEVPORT|PGDEVDATABASE" /home/admin/.pm2/dump.pm2 2>/dev/null | head -0
PM2_HOME=/home/admin/.pm2 pm2 jlist 2>/dev/null | jq -r '.[] | select(.name=="esteraone-admin") | .pm2_env.env | {PGHOST,PGPORT,PGDATABASE,PGDEVPORT,PGDEVDATABASE}'

echo
echo "=== [4] state file panel ==="
cat /home/admin/esteraone.admin/logs/green_active_db.state 2>/dev/null; echo

echo
echo "=== [5] aktivitas live 60 menit terakhir ==="
echo -n "5438 (prod) : "; psql -h $H -p 5438 -U postgres -d esteraone -Atc "select count(*)||' baris, terbaru '||max(\"createdAt\") from app_activity_logs where \"createdAt\" > now() - interval '60 min';"
echo -n "5439 (dev)  : "; psql -h $H -p 5439 -U postgres -d esteraone_dev -Atc "select count(*)||' baris, terbaru '||max(\"createdAt\") from app_activity_logs where \"createdAt\" > now() - interval '60 min';"

echo
echo "=== [6] nginx vhost -> backend ==="
grep -h "proxy_pass http" /etc/nginx/sites-enabled/esteraone.com /etc/nginx/sites-enabled/dev.esteraone.com | sort -u
grep -A2 "upstream esteraone_active_backend" /etc/nginx/conf.d/esteraone_upstream.conf | grep server
