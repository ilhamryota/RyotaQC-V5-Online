#!/usr/bin/env bash
set -u
export PGPASSWORD=supersecurepassword123
H=202.74.74.229
echo "=== which DB does live 8002 write to? (activity after switch 05:13 UTC) ==="
echo -n "5438 app_activity_logs newest : "; psql -h $H -p 5438 -U postgres -d esteraone -Atc "select max(\"createdAt\") from app_activity_logs;"
echo -n "5439 app_activity_logs newest : "; psql -h $H -p 5439 -U postgres -d esteraone_dev -Atc "select max(\"createdAt\") from app_activity_logs;"
echo -n "5438 rows written in last 60m : "; psql -h $H -p 5438 -U postgres -d esteraone -Atc "select count(*) from app_activity_logs where \"createdAt\" > now() - interval '60 min';"
echo -n "5439 rows written in last 60m : "; psql -h $H -p 5439 -U postgres -d esteraone_dev -Atc "select count(*) from app_activity_logs where \"createdAt\" > now() - interval '60 min';"
echo "=== running process DB_URL ==="
grep -m1 "DB_URL" /home/admin/.pm2/logs/esteraone-green-be-out.log | tail -1
tail -200 /home/admin/.pm2/logs/esteraone-green-be-out.log | grep "DB_URL" | tail -1
echo "=== listener 8002 pid/owner ==="
export PM2_HOME=/home/admin/.pm2
pm2 jlist 2>/dev/null | jq -r '.[] | select(.name=="esteraone-green-be") | "app=\(.name) pid=\(.pid) status=\(.pm2_env.status) restarts=\(.pm2_env.restart_time)"'
echo "=== prod http ==="
curl -s -m 10 -o /dev/null -w "https://esteraone.com/api/auth/login -> %{http_code}\n" -X POST -H 'Content-Type: application/json' -d '{}' https://esteraone.com/api/auth/login
echo "=== extra ports ==="
ss -lnt 2>/dev/null | awk '$4 ~ /:(8003|8004|8006)$/ {print "EXTRA:",$4}'
echo "--- listening app ports ---"
ss -lnt 2>/dev/null | awk '$4 ~ /:(8000|8002|8005|3001)$/ {print $4}' | sort -u
