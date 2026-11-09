#!/usr/bin/env bash
set -u
B=/home/esteraone/esteraone.green/bestlaptop-erp-be
export PM2_HOME=/home/admin/.pm2

echo "=== 1. syntax check ==="
node --check $B/dist/db.js && echo "db.js OK"
node --check $B/dist/app.js && echo "app.js OK"

echo "=== 2. patched headers ==="
sed -n '1,12p' $B/dist/db.js
echo "--- app.js middleware ---"
grep -n -A5 "DUALDB-PATCH" $B/dist/app.js | head -12

echo "=== 3. restart backend ==="
pm2 restart esteraone-green-be --update-env 2>&1 | grep -E "esteraone-green-be|online" | head -3
sleep 9

echo "=== 4. startup log ==="
tail -30 /home/admin/.pm2/logs/esteraone-green-be-out.log | grep -E "DB_URL_PROD|DB_URL_DEV|running on" | tail -5

echo "=== 5. error log check ==="
tail -20 /home/admin/.pm2/logs/esteraone-green-be-error.log | tail -8
