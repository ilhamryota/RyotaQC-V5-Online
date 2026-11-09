#!/usr/bin/env bash
set -u
B=/home/esteraone/esteraone.green/bestlaptop-erp-be
echo "=== app.ts (first 120) ==="
sed -n '1,120p' $B/src/app.ts
echo "=== dist/app.js (first 80) ==="
sed -n '1,80p' $B/dist/app.js
echo "=== hub src files ==="
ls -la /home/esteraone/intergarasi.ryota/ryota-qc-hub/src
echo "=== hub TARGET_DB usage ==="
grep -rn "TARGET_DB\|DATABASE_URL\|5438\|5439" /home/esteraone/intergarasi.ryota/ryota-qc-hub/src | head -30
