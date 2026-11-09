#!/usr/bin/env bash
set -u
BE=/home/esteraone/esteraone.green/bestlaptop-erp-be
FE=/home/esteraone/esteraone.green/bestlaptop-erp-fe
echo "=== FE: cara menentukan base URL API ==="
grep -rn "NEXT_PUBLIC_USE_PROXY_API\|NEXT_PUBLIC_API_URL" $FE/src $FE/lib $FE/app 2>/dev/null | head -12
echo "=== FE: next.config rewrites/proxy? ==="
grep -rn "rewrites\|/api/" $FE/next.config.* 2>/dev/null | head -12
echo "=== BE: redis / queue / cron / setInterval ==="
grep -rln "ioredis\|from 'redis'\|createClient(" $BE/src 2>/dev/null | head -5
grep -rn "setInterval\|node-cron\|cron.schedule" $BE/src 2>/dev/null | head -10
echo "=== BE: uploads path dipakai di mana ==="
grep -rn "uploads" $BE/src/app.ts | head -5
echo "=== ukuran & jumlah file uploads ==="
du -sh $BE/uploads 2>/dev/null; find $BE/uploads -type f 2>/dev/null | wc -l
echo "=== FE proses & build id ==="
ls -la $FE/.next/BUILD_ID 2>/dev/null && cat $FE/.next/BUILD_ID 2>/dev/null; echo
echo "=== AUTH_SECRET sama untuk prod & dev? (satu .env) ==="
grep -n "^AUTH_SECRET=" $BE/.env | sed 's/=.*/=<redacted>/'
echo "=== sisa env DB ==="
grep -nE "^DATABASE_URL|^DATABASE_URL_PROD|^DATABASE_URL_DEV" $BE/.env
