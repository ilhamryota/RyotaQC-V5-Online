#!/usr/bin/env bash
set -u
B=/home/esteraone/esteraone.green/bestlaptop-erp-be
echo "=== src/db.ts ==="
cat $B/src/db.ts
echo "=== dist/db.js ==="
cat $B/dist/db.js
echo "=== PrismaClient instantiations ==="
grep -rn "new PrismaClient" $B/src $B/dist 2>/dev/null | head -20
echo "=== app entry ==="
ls $B/src | head -20
echo "=== index.ts head ==="
sed -n '1,60p' $B/src/index.ts 2>/dev/null
echo "=== build scripts ==="
cat $B/package.json | head -40
