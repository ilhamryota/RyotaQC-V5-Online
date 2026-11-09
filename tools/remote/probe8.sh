#!/usr/bin/env bash
A=/home/admin/esteraone.admin
echo "=== admin tree ==="
find $A -maxdepth 2 -name "*.js" -o -maxdepth 2 -name "*.cjs" -o -maxdepth 2 -name "*.json" 2>/dev/null | head -40
echo "=== grep POD_GREEN_BE_PORT ==="
grep -rn "POD_GREEN_BE_PORT\|POD_BLUE_BE_PORT" $A/src 2>/dev/null | head -30
echo "=== grep pm2/spawn in admin ==="
grep -rln "pm2\|spawn\|docker" $A/src 2>/dev/null | head -20
echo "=== api health 8002 ==="
curl -s -m 5 http://127.0.0.1:8002/api/health; echo
echo "=== api health 8004 ==="
curl -s -m 5 http://127.0.0.1:8004/api/health; echo
echo "=== blue be env ==="
cat /home/admin/esteraone.blue/bestlaptop-erp-be/.env 2>/dev/null | head -20
echo "=== admin home green-be .env ==="
cat /home/admin/esteraone.green/bestlaptop-erp-be/.env 2>/dev/null | head -20
