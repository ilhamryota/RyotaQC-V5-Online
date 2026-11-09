#!/usr/bin/env bash
echo "=== esteraone.com conf ==="
cat /etc/nginx/sites-enabled/esteraone.com
echo "=== dev.esteraone.com conf ==="
cat /etc/nginx/sites-enabled/dev.esteraone.com
echo "=== upstream defs ==="
grep -rn "upstream" /etc/nginx/ 2>/dev/null | head -20
echo "=== which conf defines esteraone_active_backend ==="
grep -rln "esteraone_active_backend" /etc/nginx/ 2>/dev/null
echo "=== DATABASE_URL usage in backend ==="
B=/home/esteraone/esteraone.green/bestlaptop-erp-be
grep -rn "DATABASE_URL" "$B/src" 2>/dev/null | head -20
echo "--- prisma schema datasource ---"
sed -n '1,25p' "$B/prisma/schema.prisma" 2>/dev/null
