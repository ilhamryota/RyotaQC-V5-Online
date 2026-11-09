#!/usr/bin/env bash
set -u
export PM2_HOME=/home/admin/.pm2
B=/home/esteraone/esteraone.green/bestlaptop-erp-be
TS=$(date +%Y%m%d-%H%M%S)
echo "=== 1. backup current .env ==="
cp -v "$B/.env" "$B/.env.bak-before-prod5438-$TS"
echo "=== 2. switch DATABASE_URL -> 5438/esteraone ==="
sed -i 's|^DATABASE_URL=.*|DATABASE_URL=postgresql://postgres:supersecurepassword123@202.74.74.229:5438/esteraone?schema=public|' "$B/.env"
grep -n "^DATABASE_URL\|^APP_PORT\|^POSTGRES_DB" "$B/.env"
echo "=== 3. update panel state file ==="
echo -n 5438 > /home/admin/esteraone.admin/logs/green_active_db.state 2>/dev/null || echo "state file not writable (non-fatal)"
cat /home/admin/esteraone.admin/logs/green_active_db.state 2>/dev/null; echo
echo "=== 4. restart green backend (8002) ==="
pm2 restart esteraone-green-be --update-env 2>&1 | tail -6
sleep 8
echo "=== 5. confirm DB_URL in startup log ==="
tail -40 /home/admin/.pm2/logs/esteraone-green-be-out.log | grep -E "DB_URL|listening|running|port" | tail -10
echo "=== 6. health checks ==="
curl -s -m 8 -o /dev/null -w "local8002 /api/auth/login -> %{http_code}\n" -X POST -H 'Content-Type: application/json' -d '{}' http://127.0.0.1:8002/api/auth/login
curl -s -m 10 -o /dev/null -w "prod https /api -> %{http_code}\n" -X POST -H 'Content-Type: application/json' -d '{}' https://esteraone.com/api/auth/login
curl -s -m 10 -o /dev/null -w "public ip 8002 -> %{http_code}\n" -X POST -H 'Content-Type: application/json' -d '{}' http://109.123.239.163:8002/api/auth/login
echo "=== 7. pm2 save ==="
pm2 save 2>&1 | tail -2
