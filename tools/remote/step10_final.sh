#!/usr/bin/env bash
set -u
export PM2_HOME=/home/admin/.pm2
export PGPASSWORD=supersecurepassword123
H=202.74.74.229
B=/home/esteraone/esteraone.green/bestlaptop-erp-be

echo "=== 1. restart final (probe sudah dihapus) ==="
pm2 restart esteraone-green-be --update-env >/dev/null 2>&1
sleep 9
pm2 jlist 2>/dev/null | jq -r '.[] | select(.name=="esteraone-green-be") | "app=\(.name) pid=\(.pid) status=\(.pm2_env.status) restarts=\(.pm2_env.restart_time)"'

echo "=== 2. probe endpoint harus 404 (sudah dibersihkan) ==="
curl -s -m 8 -o /dev/null -w "  /api/_dbprobe -> %{http_code}\n" https://esteraone.com/api/_dbprobe

echo "=== 3. health prod + dev ==="
curl -s -m 10 -o /dev/null -w "  https://esteraone.com/api/auth/login      -> %{http_code}\n" -X POST -H 'Content-Type: application/json' -d '{"email":"a@b.c","password":"x"}' https://esteraone.com/api/auth/login
curl -s -m 10 -o /dev/null -w "  https://dev.esteraone.com/api/auth/login  -> %{http_code}\n" -X POST -H 'Content-Type: application/json' -d '{"email":"a@b.c","password":"x"}' https://dev.esteraone.com/api/auth/login
curl -s -m 10 -o /dev/null -w "  https://esteraone.com/                    -> %{http_code}\n" https://esteraone.com/
curl -s -m 10 -o /dev/null -w "  https://dev.esteraone.com/                -> %{http_code}\n" https://dev.esteraone.com/

echo "=== 4. Ryota Hub target DB ==="
curl -s -m 8 http://127.0.0.1:8005/api/v1/switcher/target-db; echo

echo "=== 5. port aktif (tidak boleh ada 8003/8004/8006) ==="
ss -lnt 2>/dev/null | awk '$4 ~ /:(8000|8002|8003|8004|8005|8006|3001)$/ {print "  "$4}' | sort -u

echo "=== 6. aktivitas live per DB (5 menit terakhir) ==="
echo -n "  5438 esteraone     : "; psql -h $H -p 5438 -U postgres -d esteraone -Atc "select count(*)||' baris, terbaru '||coalesce(max(\"createdAt\")::text,'-') from app_activity_logs where \"createdAt\" > now() - interval '5 min';"
echo -n "  5439 esteraone_dev : "; psql -h $H -p 5439 -U postgres -d esteraone_dev -Atc "select count(*)||' baris, terbaru '||coalesce(max(\"createdAt\")::text,'-') from app_activity_logs where \"createdAt\" > now() - interval '5 min';"

echo "=== 7. error log sejak restart ==="
before=$(grep -ac "" /home/admin/.pm2/logs/esteraone-green-be-error.log)
echo "  total baris error log: $before"

echo "=== 8. pm2 save ==="
pm2 save >/dev/null 2>&1 && echo "  saved"
