#!/usr/bin/env bash
set -u
export PM2_HOME=/home/admin/.pm2
echo "=== 1. stop+delete pm2 app esteraone-prod-be (port 8004) ==="
pm2 delete esteraone-prod-be 2>&1 | tail -5
sleep 2
echo "=== 2. archive config file ==="
B=/home/esteraone/esteraone.green/bestlaptop-erp-be
mv -v "$B/pm2-prod-be.config.cjs" "$B/pm2-prod-be.config.cjs.disabled-20261026" 2>&1
ls -la "$B"/pm2-prod-be.config* 2>&1
echo "=== 3. pm2 list after ==="
pm2 jlist 2>/dev/null | jq -r '.[] | [.name,.pm2_env.status] | @tsv'
echo "=== 4. port 8004 status ==="
curl -s -m 3 -o /dev/null -w "8004 http_code=%{http_code}\n" http://127.0.0.1:8004/ || echo "8004 CLOSED (connection refused)"
ss -lnt 2>/dev/null | awk '$4 ~ /:8004$/ {print "STILL LISTENING:",$0}' 
echo "=== 5. prod 8002 still alive ==="
curl -s -m 5 -o /dev/null -w "8002 http_code=%{http_code}\n" http://127.0.0.1:8002/api/auth/login
echo "=== 6. save pm2 state ==="
pm2 save 2>&1 | tail -3
