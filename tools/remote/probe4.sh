#!/usr/bin/env bash
echo "=== curl 8002 root/health ==="
curl -s -m 5 -D - http://127.0.0.1:8002/ -o /tmp/b8002.html | head -20
head -c 300 /tmp/b8002.html; echo
echo "=== curl 8004 root/health ==="
curl -s -m 5 -D - http://127.0.0.1:8004/ -o /tmp/b8004.html | head -20
head -c 300 /tmp/b8004.html; echo
echo "=== curl 8005 (hub) ==="
curl -s -m 5 -D - http://127.0.0.1:8005/ -o /tmp/b8005.html | head -20
head -c 300 /tmp/b8005.html; echo
echo "=== nginx conf ==="
ls -la /etc/nginx/sites-enabled/ 2>&1
grep -rn "proxy_pass" /etc/nginx/sites-enabled/ 2>&1 | head -30
echo "=== grep DB switch in backend dist/src ==="
B=/home/esteraone/esteraone.green/bestlaptop-erp-be
grep -rln "DATABASE_URL_DEV" "$B/src" "$B/dist" 2>/dev/null | head -20
echo "=== ilham pm2 ==="
PM2_HOME=/home/dev_ilham/.pm2 pm2 jlist 2>/dev/null | jq -r '.[] | [.name,.pm2_env.status,(.pm2_env.PORT//"-"),(.pm2_env.pm_cwd//"-")] | @tsv'
