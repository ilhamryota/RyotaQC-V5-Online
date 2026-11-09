#!/usr/bin/env bash
B=/home/esteraone/esteraone.green/bestlaptop-erp-be
echo "=== CURRENT .env (8002 backend) ==="
cat "$B/.env"
echo "=== .env.bak-8002-ke-5439-20261026-0035 ==="
cat "$B/.env.bak-8002-ke-5439-20261026-0035"
echo "=== .env.bak_before_cutover ==="
cat "$B/.env.bak_before_cutover"
echo "=== pm2-prod-be.config.cjs ==="
cat "$B/pm2-prod-be.config.cjs"
echo "=== pm2-dev-be.config.cjs ==="
cat "$B/pm2-dev-be.config.cjs"
echo "=== ryota hub .env ==="
cat /home/esteraone/intergarasi.ryota/ryota-qc-hub/.env
echo "=== green-fe env ==="
ls -la /home/esteraone/esteraone.green/bestlaptop-erp-fe/.env* 2>&1
